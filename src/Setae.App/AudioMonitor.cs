using Setae.Core;

namespace Setae.App;

public sealed class AudioMonitor : IDisposable
{
    private readonly AudioDeviceService _deviceService;
    private readonly object _gate = new();
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private WasapiCaptureSession? _session;
    private Task? _pendingCleanup;
    private MonitorSnapshot _snapshot = MonitorSnapshot.Stopped();
    private int _disposed;

    public AudioMonitor(AudioDeviceService deviceService)
    {
        ArgumentNullException.ThrowIfNull(deviceService);
        _deviceService = deviceService;
    }

    public event EventHandler<AlertTriggeredEventArgs>? AlertTriggered;

    public MonitorSnapshot Snapshot => Volatile.Read(ref _snapshot);

    public Task StartAsync(string deviceId, MonitoringPreferences preferences)
    {
        MonitoringPreferencesValidator.EnsureValid(preferences);

        if (string.IsNullOrWhiteSpace(deviceId))
        {
            throw new ArgumentException("Selecciona un micrófono antes de iniciar.", nameof(deviceId));
        }

        return StartOrRestartAsync(deviceId, preferences, restart: false);
    }

    public Task RestartAsync(string deviceId, MonitoringPreferences preferences)
    {
        MonitoringPreferencesValidator.EnsureValid(preferences);

        if (string.IsNullOrWhiteSpace(deviceId))
        {
            throw new ArgumentException("Selecciona un micrófono antes de iniciar.", nameof(deviceId));
        }

        return StartOrRestartAsync(deviceId, preferences, restart: true);
    }

    private async Task StartOrRestartAsync(
        string deviceId,
        MonitoringPreferences preferences,
        bool restart)
    {
        await _lifecycleGate.WaitAsync().ConfigureAwait(false);

        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

            if (!restart && Snapshot.IsRunning)
            {
                return;
            }

            await StopCurrentSessionAsync().ConfigureAwait(false);
            await Task.Run(() => StartSession(deviceId, preferences)).ConfigureAwait(false);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private void StartSession(string deviceId, MonitoringPreferences preferences)
    {
        WasapiCaptureSession? session = null;

        try
        {
            var device = _deviceService.OpenCaptureDevice(deviceId);
            session = new WasapiCaptureSession(
                device,
                preferences,
                (level, state) => OnLevel(session!, level, state),
                alert => OnAlert(session!, alert),
                exception => OnCaptureStopped(session!, exception),
                exception => OnCaptureFaulted(session!, exception));

            lock (_gate)
            {
                _session = session;
                PublishSnapshot(new MonitorSnapshot(true, 0f, AlertState.Normal, "Iniciando", null));
            }

            session.Start();
        }
        catch (Exception exception)
        {
            if (session is not null)
            {
                session.StopAndDisposeAsync().GetAwaiter().GetResult();
            }

            lock (_gate)
            {
                if (ReferenceEquals(_session, session))
                {
                    _session = null;
                }

                var errorMessage = DescribeCaptureError(exception);
                PublishSnapshot(MonitorSnapshot.Stopped(errorMessage) with
                {
                    ErrorMessage = errorMessage
                });
            }

            throw;
        }
    }

    public async Task StopAsync()
    {
        await _lifecycleGate.WaitAsync().ConfigureAwait(false);

        try
        {
            await StopCurrentSessionAsync().ConfigureAwait(false);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private async Task StopCurrentSessionAsync()
    {
        WasapiCaptureSession? session;
        Task? pendingCleanup;

        lock (_gate)
        {
            session = _session;
            _session = null;
            pendingCleanup = _pendingCleanup;
            _pendingCleanup = null;
            PublishSnapshot(MonitorSnapshot.Stopped());
        }

        if (session is not null)
        {
            await Task.Run(() => session.StopAndDisposeAsync()).ConfigureAwait(false);
        }

        if (pendingCleanup is not null)
        {
            await pendingCleanup.ConfigureAwait(false);
        }
    }

    public void UpdatePreferences(MonitoringPreferences preferences)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        MonitoringPreferencesValidator.EnsureValid(preferences);

        lock (_gate)
        {
            _session?.UpdatePreferences(preferences);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            StopAsync().GetAwaiter().GetResult();
        }
        finally
        {
            _lifecycleGate.Dispose();
        }
    }

    private void OnLevel(WasapiCaptureSession session, float level, AlertState state)
    {
        lock (_gate)
        {
            if (ReferenceEquals(_session, session))
            {
                PublishSnapshot(new MonitorSnapshot(true, level, state, "Monitorizando", null));
            }
        }
    }

    private void OnAlert(WasapiCaptureSession session, AlertTriggeredEventArgs alert)
    {
        EventHandler<AlertTriggeredEventArgs>? alertHandler;

        lock (_gate)
        {
            alertHandler = ReferenceEquals(_session, session) ? AlertTriggered : null;
        }

        alertHandler?.Invoke(this, alert);
    }

    private void OnCaptureStopped(WasapiCaptureSession session, Exception? exception)
    {
        Task cleanup;

        lock (_gate)
        {
            if (!ReferenceEquals(_session, session))
            {
                return;
            }

            _session = null;
            var errorMessage = exception is null
                ? "La captura se detuvo inesperadamente. Comprueba que el micrófono siga conectado."
                : DescribeCaptureError(exception);
            PublishSnapshot(new MonitorSnapshot(
                false,
                0f,
                AlertState.Normal,
                errorMessage,
                errorMessage));

            cleanup = session.StopAndDisposeAsync();
            _pendingCleanup = cleanup;
        }

        _ = ObserveCleanupAsync(cleanup);
    }

    private void OnCaptureFaulted(WasapiCaptureSession session, Exception exception)
    {
        Task cleanup;

        lock (_gate)
        {
            if (!ReferenceEquals(_session, session))
            {
                return;
            }

            _session = null;
            var errorMessage = DescribeCaptureError(exception);
            PublishSnapshot(new MonitorSnapshot(false, 0f, AlertState.Normal, errorMessage, errorMessage));

            cleanup = session.StopAndDisposeAsync();
            _pendingCleanup = cleanup;
        }

        _ = ObserveCleanupAsync(cleanup);
    }

    private static async Task ObserveCleanupAsync(Task cleanup)
    {
        try
        {
            await cleanup.ConfigureAwait(false);
        }
        catch
        {
            // Cleanup is best effort after the capture error has been reported.
        }
    }

    private void PublishSnapshot(MonitorSnapshot snapshot)
    {
        Volatile.Write(ref _snapshot, snapshot);
    }

    private static string DescribeCaptureError(Exception exception)
    {
        if (exception is UnauthorizedAccessException
            || exception.HResult == unchecked((int)0x80070005)
            || exception.Message.Contains("access", StringComparison.OrdinalIgnoreCase)
            || exception.Message.Contains("permiso", StringComparison.OrdinalIgnoreCase))
        {
            return "Acceso al micrófono bloqueado. Revisa Configuración > Privacidad y seguridad > Micrófono.";
        }

        return $"No se pudo iniciar o mantener la captura: {exception.Message} Comprueba que el micrófono siga conectado y vuelve a intentarlo.";
    }
}
