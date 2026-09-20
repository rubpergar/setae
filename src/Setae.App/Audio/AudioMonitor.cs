using Setae.App.Monitoring;

namespace Setae.App.Audio;

public sealed class AudioMonitor : IDisposable
{
    private readonly AudioDeviceService _deviceService;
    private readonly object _gate = new();
    private readonly object _lifecycleGate = new();
    private WasapiCaptureSession? _session;
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
        ValidateArguments(deviceId, preferences);
        return Task.Run(() => StartCore(deviceId, preferences, restart: false));
    }

    public Task RestartAsync(string deviceId, MonitoringPreferences preferences)
    {
        ValidateArguments(deviceId, preferences);
        return Task.Run(() => StartCore(deviceId, preferences, restart: true));
    }

    public Task StopAsync()
    {
        return Task.Run(StopCore);
    }

    public void UpdatePreferences(MonitoringPreferences preferences)
    {
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

        StopCore();
    }

    private static void ValidateArguments(string deviceId, MonitoringPreferences preferences)
    {
        MonitoringPreferencesValidator.EnsureValid(preferences);

        if (string.IsNullOrWhiteSpace(deviceId))
        {
            throw new ArgumentException("Select a microphone before starting.", nameof(deviceId));
        }
    }

    private void StartCore(string deviceId, MonitoringPreferences preferences, bool restart)
    {
        lock (_lifecycleGate)
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                return;
            }

            if (!restart && Snapshot.IsRunning)
            {
                return;
            }

            StopCore();

            WasapiCaptureSession? session = null;

            try
            {
                var device = _deviceService.OpenCaptureDevice(deviceId);
                session = new WasapiCaptureSession(
                    device,
                    preferences,
                    OnLevel,
                    OnAlert,
                    OnStopped);

                lock (_gate)
                {
                    _session = session;
                }

                session.Start();
                PublishSnapshot(new MonitorSnapshot(true, 0f, AlertState.Normal, null));
            }
            catch (Exception exception)
            {
                session?.Dispose();

                lock (_gate)
                {
                    if (ReferenceEquals(_session, session))
                    {
                        _session = null;
                    }
                }

                PublishSnapshot(MonitorSnapshot.Stopped(DescribeCaptureError(exception)));
            }
        }
    }

    private void StopCore()
    {
        lock (_lifecycleGate)
        {
            WasapiCaptureSession? session;

            lock (_gate)
            {
                session = _session;
                _session = null;
                PublishSnapshot(MonitorSnapshot.Stopped());
            }

            session?.Dispose();
        }
    }

    private void OnLevel(WasapiCaptureSession session, float level, AlertState state)
    {
        lock (_gate)
        {
            if (ReferenceEquals(_session, session))
            {
                PublishSnapshot(new MonitorSnapshot(true, level, state, null));
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

    private void OnStopped(WasapiCaptureSession session, Exception? exception)
    {
        lock (_gate)
        {
            if (!ReferenceEquals(_session, session))
            {
                return;
            }

            _session = null;
            var errorMessage = exception is null
                ? "Capture stopped. Check that the microphone is still connected."
                : DescribeCaptureError(exception);
            PublishSnapshot(MonitorSnapshot.Stopped(errorMessage));
        }

        _ = Task.Run(session.Dispose);
    }

    private void PublishSnapshot(MonitorSnapshot snapshot)
    {
        Volatile.Write(ref _snapshot, snapshot);
    }

    private static string DescribeCaptureError(Exception exception)
    {
        if (exception is UnauthorizedAccessException || exception.HResult == unchecked((int)0x80070005))
        {
            return "Microphone access is blocked. Check Settings > Privacy & security > Microphone.";
        }

        return $"Capture could not start or continue: {exception.Message} Check that the microphone is connected and try again.";
    }
}
