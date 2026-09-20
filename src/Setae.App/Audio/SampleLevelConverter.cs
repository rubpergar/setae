using System.Buffers.Binary;
using NAudio.Wave;

namespace Setae.App.Audio;

internal static class SampleLevelConverter
{
    private static readonly Guid PcmSubFormat = new("00000001-0000-0010-8000-00AA00389B71");
    private static readonly Guid IeeeFloatSubFormat = new("00000003-0000-0010-8000-00AA00389B71");

    /// <summary>
    /// Determines whether shared-mode WASAPI emits a common microphone format:
    /// 32-bit IEEE float or 16-bit PCM.
    /// </summary>
    public static bool IsSupported(WaveFormat format)
    {
        var isFloat32 = (format.Encoding == WaveFormatEncoding.IeeeFloat
                         || format is WaveFormatExtensible floatExtensible
                         && floatExtensible.SubFormat == IeeeFloatSubFormat)
                        && format.BitsPerSample == 32;
        var isPcm16 = (format.Encoding == WaveFormatEncoding.Pcm
                       || format is WaveFormatExtensible pcmExtensible
                       && pcmExtensible.SubFormat == PcmSubFormat)
                      && format.BitsPerSample == 16;

        return isFloat32 || isPcm16;
    }

    /// <summary>
    /// Calculates the buffer's relative level (0..100) using RMS across all samples and channels.
    /// The samples are not retained or copied after calculation.
    /// </summary>
    public static float ToRelativeLevel(ReadOnlySpan<byte> audioBytes, int bytesRecorded, WaveFormat format)
    {
        if (bytesRecorded <= 0 || bytesRecorded > audioBytes.Length)
        {
            return 0f;
        }

        if (!IsSupported(format))
        {
            throw new NotSupportedException(
                $"The capture format is not supported: {format.Encoding}, {format.BitsPerSample} bits.");
        }

        var bytesPerSample = format.BitsPerSample / 8;
        var sampleCount = bytesRecorded / bytesPerSample;
        var sumOfSquares = 0d;

        if (format.BitsPerSample == 32)
        {
            for (var index = 0; index < sampleCount; index++)
            {
                var bits = BinaryPrimitives.ReadInt32LittleEndian(audioBytes[(index * 4)..]);
                var sample = BitConverter.Int32BitsToSingle(bits);
                var normalized = float.IsFinite(sample) ? Math.Clamp(sample, -1f, 1f) : 0f;
                sumOfSquares += normalized * normalized;
            }
        }
        else
        {
            for (var index = 0; index < sampleCount; index++)
            {
                var sample = BinaryPrimitives.ReadInt16LittleEndian(audioBytes[(index * 2)..]) / 32768f;
                sumOfSquares += sample * sample;
            }
        }

        var rms = (float)Math.Sqrt(sumOfSquares / sampleCount);
        return AudioLevel.DbfsToRelative(AudioLevel.RmsToDbfs(rms));
    }
}
