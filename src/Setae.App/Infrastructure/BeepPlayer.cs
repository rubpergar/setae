using System.IO;
using System.Media;

namespace Setae.App.Infrastructure;

/// <summary>
/// Plays a two-note chime generated in code without depending on a specific output device.
/// </summary>
internal sealed class BeepPlayer : IDisposable
{
    private const double TauSeconds = 0.08;
    private const double PeakAmplitude = 0.35;
    private static readonly short PeakSample = (short)(PeakAmplitude * short.MaxValue);

    private static readonly Note[] Notes =
    {
        new(660, 0.000, 0.110),
        new(990, 0.100, 0.250)
    };

    private static readonly byte[] WavData = CreateWavData();

    private readonly SoundPlayer _player;
    private readonly MemoryStream _stream;
    private bool _disposed;

    public BeepPlayer()
    {
        _stream = new MemoryStream(WavData);
        _player = new SoundPlayer(_stream);
    }

    /// <summary>
    /// Plays the chime asynchronously without throwing when no output device is available.
    /// </summary>
    public void Play()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _player.Play();
        }
        catch
        {
            // Monitoring must continue when no output device is available.
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            _player.Dispose();
        }
        catch
        {
            // Ignore errors during application shutdown.
        }

        try
        {
            _stream.Dispose();
        }
        catch
        {
            // Ignore errors during application shutdown.
        }
    }

    /// <summary>
    /// Generates a 44,100 Hz mono PCM16 WAV containing the two chime notes.
    /// </summary>
    internal static byte[] CreateWavData()
    {
        const int sampleRate = 44_100;
        const int totalMilliseconds = 350;
        var totalSamples = sampleRate * totalMilliseconds / 1_000;
        var samples = new short[totalSamples];

        foreach (var note in Notes)
        {
            var startSample = (int)Math.Round(note.StartSeconds * sampleRate);
            var durationSamples = (int)Math.Round(note.DurationSeconds * sampleRate);
            var releaseSamples = (int)(durationSamples * 0.15);

            for (var index = 0; index < durationSamples; index++)
            {
                var sampleIndex = startSample + index;
                if (sampleIndex >= totalSamples)
                {
                    break;
                }

                var time = index / (double)sampleRate;
                var value = PeakSample
                    * Math.Sin(2d * Math.PI * note.Frequency * time)
                    * Math.Exp(-time / TauSeconds);

                if (durationSamples - index <= releaseSamples)
                {
                    var releaseProgress = 1d - (durationSamples - index) / (double)releaseSamples;
                    value *= 0.5d * (1d + Math.Cos(Math.PI * releaseProgress));
                }

                samples[sampleIndex] = (short)Math.Clamp(
                    samples[sampleIndex] + value,
                    short.MinValue,
                    short.MaxValue);
            }
        }

        var dataSize = totalSamples * 2;
        var wav = new byte[44 + dataSize];

        WriteAscii(wav, 0, "RIFF");
        WriteInt32(wav, 4, 36 + dataSize);
        WriteAscii(wav, 8, "WAVE");
        WriteAscii(wav, 12, "fmt ");
        WriteInt32(wav, 16, 16);
        WriteInt16(wav, 20, 1);
        WriteInt16(wav, 22, 1);
        WriteInt32(wav, 24, sampleRate);
        WriteInt32(wav, 28, sampleRate * 2);
        WriteInt16(wav, 32, 2);
        WriteInt16(wav, 34, 16);
        WriteAscii(wav, 36, "data");
        WriteInt32(wav, 40, dataSize);

        for (var index = 0; index < totalSamples; index++)
        {
            var sample = samples[index];
            wav[44 + index * 2] = (byte)(sample & 0xFF);
            wav[44 + index * 2 + 1] = (byte)((sample >> 8) & 0xFF);
        }

        return wav;
    }

    private static void WriteInt16(byte[] buffer, int offset, short value)
    {
        buffer[offset] = (byte)(value & 0xFF);
        buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
    }

    private static void WriteInt32(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value & 0xFF);
        buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
        buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
        buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
    }

    private static void WriteAscii(byte[] buffer, int offset, string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            buffer[offset + index] = (byte)value[index];
        }
    }

    private readonly record struct Note(double Frequency, double StartSeconds, double DurationSeconds);
}
