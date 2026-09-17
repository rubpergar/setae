using NAudio.Wave;

namespace Setae.App;

internal sealed class BeepPlayer : IDisposable
{
    private static readonly WaveFormat BeepFormat = new(44_100, 16, 1);
    private static readonly byte[] BeepData = CreateBeepData();

    private readonly object _gate = new();
    private WaveOutEvent? _output;
    private BufferedWaveProvider? _buffer;
    private int _disposed;

    public event Action<Exception>? PlaybackFailed;

    public void Play()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        try
        {
            lock (_gate)
            {
                if (_disposed != 0)
                {
                    return;
                }

                var buffer = _buffer ??= new BufferedWaveProvider(BeepFormat)
                {
                    DiscardOnBufferOverflow = true,
                    ReadFully = false
                };
                var output = _output ??= CreateOutput(buffer);
                buffer.ClearBuffer();
                buffer.AddSamples(BeepData, 0, BeepData.Length);
                output.Play();
            }
        }
        catch (Exception exception)
        {
            try
            {
                PlaybackFailed?.Invoke(exception);
            }
            catch
            {
                // Playback failures must not terminate the capture worker.
            }
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        lock (_gate)
        {
            _buffer?.ClearBuffer();
            try
            {
                _output?.Dispose();
            }
            catch
            {
                // The output is being torn down during application exit.
            }

            _output = null;
            _buffer = null;
        }
    }

    private static WaveOutEvent CreateOutput(IWaveProvider provider)
    {
        var output = new WaveOutEvent
        {
            DesiredLatency = 80,
            DeviceNumber = -1
        };

        try
        {
            output.Init(provider);
            return output;
        }
        catch
        {
            try
            {
                output.Dispose();
            }
            catch
            {
                // Preserve the original initialization error.
            }

            throw;
        }
    }

    private static byte[] CreateBeepData()
    {
        const int sampleRate = 44_100;
        const int durationMilliseconds = 80;
        const double frequency = 880;
        var samples = sampleRate * durationMilliseconds / 1_000;
        var data = new byte[samples * 2];

        for (var index = 0; index < samples; index++)
        {
            var envelope = Math.Min(1d, Math.Min(index / 220d, (samples - index) / 220d));
            var sample = (short)(Math.Sin(2d * Math.PI * frequency * index / sampleRate) * 8_000d * envelope);
            data[index * 2] = (byte)(sample & 0xFF);
            data[index * 2 + 1] = (byte)((sample >> 8) & 0xFF);
        }

        return data;
    }
}
