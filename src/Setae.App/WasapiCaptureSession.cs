using System.Buffers;
using System.Threading.Channels;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using Setae.Core;

namespace Setae.App;

internal sealed class WasapiCaptureSession
{
    private readonly MMDevice _device;
    private readonly WasapiCapture _capture;
    private readonly Channel<CapturedBuffer> _buffers;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly object _processorGate = new();
    private readonly Action<float, AlertState> _levelHandler;
    private readonly Action<AlertTriggeredEventArgs> _alertHandler;
    private readonly Action<Exception?> _stoppedHandler;
    private readonly Action<Exception> _faultHandler;
    private readonly WaveFormat _waveFormat;
    private readonly TaskCompletionSource<object?> _recordingStopped = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly object _lifecycleGate = new();
    private Task? _processingTask;
    private Task? _stopTask;
    private LevelSmoother _smoother;
    private AlertDetector _detector;
    private bool _beepEnabled;
    private int _acceptingBuffers;
    private int _disposed;
    private int _recordingStarted;
    private int _recordingStoppedCallbackReceived;

    public WasapiCaptureSession(
        MMDevice device,
        MonitoringPreferences preferences,
        Action<float, AlertState> levelHandler,
        Action<AlertTriggeredEventArgs> alertHandler,
        Action<Exception?> stoppedHandler,
        Action<Exception> faultHandler)
    {
        MonitoringPreferencesValidator.EnsureValid(preferences);

        _device = device;
        try
        {
            _capture = new WasapiCapture(device, useEventSync: true)
            {
                ShareMode = AudioClientShareMode.Shared
            };
        }
        catch
        {
            DisposeDeviceQuietly(_device);
            throw;
        }

        WaveFormat waveFormat;
        try
        {
            waveFormat = _capture.WaveFormat;
        }
        catch
        {
            DisposeCaptureAndDeviceQuietly();
            throw;
        }

        if (!SampleLevelConverter.IsSupported(waveFormat))
        {
            DisposeCaptureAndDeviceQuietly();
            throw new NotSupportedException(
                $"El formato de captura del micrófono no está soportado: {waveFormat.Encoding}, {waveFormat.BitsPerSample} bits.");
        }

        _waveFormat = waveFormat;

        try
        {
            _buffers = Channel.CreateBounded<CapturedBuffer>(new BoundedChannelOptions(3)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = true,
                AllowSynchronousContinuations = false
            });

            _levelHandler = levelHandler;
            _alertHandler = alertHandler;
            _stoppedHandler = stoppedHandler;
            _faultHandler = faultHandler;
            _smoother = new LevelSmoother();
            _detector = new AlertDetector(preferences);
            _beepEnabled = preferences.BeepEnabled;

            _capture.DataAvailable += CaptureOnDataAvailable;
            _capture.RecordingStopped += CaptureOnRecordingStopped;
        }
        catch
        {
            DisposeCaptureAndDeviceQuietly();
            throw;
        }
    }

    public string DeviceId => _device.ID;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        _processingTask = ProcessBuffersAsync();
        Volatile.Write(ref _acceptingBuffers, 1);
        Volatile.Write(ref _recordingStarted, 1);

        try
        {
            _capture.StartRecording();
        }
        catch
        {
            Volatile.Write(ref _acceptingBuffers, 0);
            Volatile.Write(ref _recordingStarted, 0);
            _recordingStopped.TrySetResult(null);
            throw;
        }
    }

    public void UpdatePreferences(MonitoringPreferences preferences)
    {
        MonitoringPreferencesValidator.EnsureValid(preferences);

        lock (_processorGate)
        {
            _smoother = new LevelSmoother();
            _detector = new AlertDetector(preferences);
            _beepEnabled = preferences.BeepEnabled;
        }
    }

    public Task StopAndDisposeAsync()
    {
        lock (_lifecycleGate)
        {
            if (_stopTask is not null)
            {
                return _stopTask;
            }

            Volatile.Write(ref _disposed, 1);
            _stopTask = StopAndDisposeCoreAsync();
            return _stopTask;
        }
    }

    private async Task StopAndDisposeCoreAsync()
    {
        Volatile.Write(ref _acceptingBuffers, 0);
        _capture.DataAvailable -= CaptureOnDataAvailable;
        _buffers.Writer.TryComplete();

        try
        {
            _cancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // A completed cleanup path already owns the cancellation source.
        }

        await Task.Run(() =>
        {
            try
            {
                _capture.StopRecording();
            }
            catch
            {
                // The capture may already have stopped because the endpoint disappeared.
            }

            try
            {
                _capture.Dispose();
            }
            catch
            {
                // Resource cleanup continues for the endpoint and processing task.
            }
        }).ConfigureAwait(false);

        if (Volatile.Read(ref _recordingStarted) == 0)
        {
            _recordingStopped.TrySetResult(null);
        }

        try
        {
            await _recordingStopped.Task
                .WaitAsync(TimeSpan.FromSeconds(1))
                .ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // A late synchronization-context callback remains harmless and idempotent.
        }

        _capture.RecordingStopped -= CaptureOnRecordingStopped;

        var processingTask = _processingTask;
        if (processingTask is not null && Task.CurrentId != processingTask.Id)
        {
            try
            {
                await processingTask.ConfigureAwait(false);
            }
            catch (Exception)
            {
                // The worker reports capture failures through _faultHandler.
            }
        }

        try
        {
            _device.Dispose();
        }
        catch
        {
            // All available resources have already been released.
        }

        if (_recordingStopped.Task.IsCompleted
            && (processingTask is null || processingTask.IsCompleted))
        {
            _cancellation.Dispose();
        }
    }

    private void CaptureOnDataAvailable(object? sender, WaveInEventArgs args)
    {
        if (Volatile.Read(ref _acceptingBuffers) == 0 || args.BytesRecorded <= 0)
        {
            return;
        }

        var rentedBuffer = ArrayPool<byte>.Shared.Rent(args.BytesRecorded);

        try
        {
            Buffer.BlockCopy(args.Buffer, 0, rentedBuffer, 0, args.BytesRecorded);

            if (!_buffers.Writer.TryWrite(new CapturedBuffer(rentedBuffer, args.BytesRecorded)))
            {
                ArrayPool<byte>.Shared.Return(rentedBuffer, clearArray: true);
            }
        }
        catch
        {
            ArrayPool<byte>.Shared.Return(rentedBuffer, clearArray: true);
        }
    }

    private void CaptureOnRecordingStopped(object? sender, StoppedEventArgs args)
    {
        if (Interlocked.Exchange(ref _recordingStoppedCallbackReceived, 1) != 0)
        {
            return;
        }

        Volatile.Write(ref _acceptingBuffers, 0);
        _buffers.Writer.TryComplete();

        try
        {
            if (Volatile.Read(ref _disposed) == 0)
            {
                _cancellation.Cancel();
                _stoppedHandler(args.Exception);
            }
        }
        finally
        {
            _recordingStopped.TrySetResult(null);
        }
    }

    private async Task ProcessBuffersAsync()
    {
        try
        {
            await foreach (var capturedBuffer in _buffers.Reader.ReadAllAsync(_cancellation.Token).ConfigureAwait(false))
            {
                try
                {
                    ProcessBuffer(capturedBuffer);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(capturedBuffer.Buffer, clearArray: true);
                }
            }
        }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Volatile.Write(ref _acceptingBuffers, 0);
            _buffers.Writer.TryComplete(exception);
            _faultHandler(exception);
        }
        finally
        {
            while (_buffers.Reader.TryRead(out var pendingBuffer))
            {
                ArrayPool<byte>.Shared.Return(pendingBuffer.Buffer, clearArray: true);
            }
        }
    }

    private void ProcessBuffer(CapturedBuffer capturedBuffer)
    {
        var relativeLevel = SampleLevelConverter.ToRelativeLevel(
            capturedBuffer.Buffer,
            capturedBuffer.BytesRecorded,
            _waveFormat);
        var elapsed = GetBufferDuration(capturedBuffer.BytesRecorded);
        bool triggered;
        bool beepEnabled;
        float smoothedLevel;
        AlertState state;

        lock (_processorGate)
        {
            smoothedLevel = _smoother.Update(relativeLevel, elapsed);
            triggered = _detector.Process(smoothedLevel, elapsed);
            state = _detector.State;
            beepEnabled = _beepEnabled;
        }

        _levelHandler(smoothedLevel, state);

        if (triggered && beepEnabled)
        {
            _alertHandler(new AlertTriggeredEventArgs(smoothedLevel));
        }
    }

    private TimeSpan GetBufferDuration(int bytesRecorded)
    {
        if (_waveFormat.AverageBytesPerSecond <= 0)
        {
            return TimeSpan.Zero;
        }

        return TimeSpan.FromSeconds(bytesRecorded / (double)_waveFormat.AverageBytesPerSecond);
    }

    private void DisposeCaptureAndDeviceQuietly()
    {
        try
        {
            _capture.Dispose();
        }
        catch
        {
            // Continue releasing the endpoint if capture initialization failed.
        }

        DisposeDeviceQuietly(_device);
    }

    private static void DisposeDeviceQuietly(MMDevice device)
    {
        try
        {
            device.Dispose();
        }
        catch
        {
            // There is no further resource to release from the endpoint adapter.
        }
    }

    private readonly record struct CapturedBuffer(byte[] Buffer, int BytesRecorded);
}
