using NAudio.CoreAudioApi;
using NAudio.Wave;
using Setae.App.Monitoring;

namespace Setae.App.Audio;

internal sealed class WasapiCaptureSession : IDisposable
{
    private readonly MMDevice _device;
    private readonly WasapiCapture _capture;
    private readonly object _gate = new();
    private readonly Action<WasapiCaptureSession, float, AlertState> _levelHandler;
    private readonly Action<WasapiCaptureSession, AlertTriggeredEventArgs> _alertHandler;
    private readonly Action<WasapiCaptureSession, Exception?> _stoppedHandler;
    private readonly WaveFormat _waveFormat;

    private LevelSmoother _smoother;
    private AlertDetector _detector;
    private bool _beepEnabled;
    private int _disposed;

    public WasapiCaptureSession(
        MMDevice device,
        MonitoringPreferences preferences,
        Action<WasapiCaptureSession, float, AlertState> levelHandler,
        Action<WasapiCaptureSession, AlertTriggeredEventArgs> alertHandler,
        Action<WasapiCaptureSession, Exception?> stoppedHandler)
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

        try
        {
            _waveFormat = _capture.WaveFormat;

            if (!SampleLevelConverter.IsSupported(_waveFormat))
            {
                throw new NotSupportedException(
                    $"The microphone capture format is not supported: {_waveFormat.Encoding}, {_waveFormat.BitsPerSample} bits.");
            }

            _levelHandler = levelHandler;
            _alertHandler = alertHandler;
            _stoppedHandler = stoppedHandler;
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

    public void Start()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        _capture.StartRecording();
    }

    public void UpdatePreferences(MonitoringPreferences preferences)
    {
        MonitoringPreferencesValidator.EnsureValid(preferences);

        lock (_gate)
        {
            _detector = new AlertDetector(preferences);
            _beepEnabled = preferences.BeepEnabled;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _capture.DataAvailable -= CaptureOnDataAvailable;
        _capture.RecordingStopped -= CaptureOnRecordingStopped;

        try
        {
            _capture.StopRecording();
        }
        catch
        {
            // Capture may have stopped because the device disappeared.
        }

        try
        {
            _capture.Dispose();
        }
        catch
        {
            // Continue cleanup by releasing the device.
        }

        DisposeDeviceQuietly(_device);
    }

    private void CaptureOnDataAvailable(object? sender, WaveInEventArgs args)
    {
        if (Volatile.Read(ref _disposed) != 0 || args.BytesRecorded <= 0)
        {
            return;
        }

        float relativeLevel;

        try
        {
            relativeLevel = SampleLevelConverter.ToRelativeLevel(
                args.Buffer,
                args.BytesRecorded,
                _waveFormat);
        }
        catch
        {
            // An invalid buffer must not stop monitoring.
            return;
        }

        var elapsed = GetBufferDuration(args.BytesRecorded);
        float smoothedLevel;
        AlertState state;
        bool triggered;
        bool beepEnabled;

        lock (_gate)
        {
            smoothedLevel = _smoother.Update(relativeLevel, elapsed);
            triggered = _detector.Process(smoothedLevel, elapsed);
            state = _detector.State;
            beepEnabled = _beepEnabled;
        }

        _levelHandler(this, smoothedLevel, state);

        if (triggered && beepEnabled)
        {
            _alertHandler(this, new AlertTriggeredEventArgs(smoothedLevel));
        }
    }

    private void CaptureOnRecordingStopped(object? sender, StoppedEventArgs args)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        _stoppedHandler(this, args.Exception);
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
            // Continue releasing the device.
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
            // No other resources remain to release.
        }
    }
}
