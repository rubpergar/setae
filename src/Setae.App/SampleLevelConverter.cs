using System.Buffers;
using System.Buffers.Binary;
using NAudio.Wave;
using Setae.Core;

namespace Setae.App;

internal static class SampleLevelConverter
{
    private static readonly Guid PcmSubFormat = new("00000001-0000-0010-8000-00AA00389B71");
    private static readonly Guid IeeeFloatSubFormat = new("00000003-0000-0010-8000-00AA00389B71");

    public static bool IsSupported(WaveFormat format)
    {
        var isPcm = format.Encoding == WaveFormatEncoding.Pcm
                    || format is WaveFormatExtensible pcmExtensible
                    && pcmExtensible.SubFormat == PcmSubFormat;
        var hasSupportedPcmDepth = format.BitsPerSample is 8 or 16 or 24 or 32;
        var isFloat = format.Encoding == WaveFormatEncoding.IeeeFloat
                      || format is WaveFormatExtensible floatExtensible
                      && floatExtensible.SubFormat == IeeeFloatSubFormat;

        return isPcm && hasSupportedPcmDepth || isFloat && format.BitsPerSample == 32;
    }

    public static float ToRelativeLevel(ReadOnlySpan<byte> audioBytes, int bytesRecorded, WaveFormat format)
    {
        if (bytesRecorded < 0 || bytesRecorded > audioBytes.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(bytesRecorded));
        }

        if (!IsSupported(format))
        {
            throw new NotSupportedException(
                $"El formato de captura no está soportado: {format.Encoding}, {format.BitsPerSample} bits.");
        }

        var bytesPerSample = format.BitsPerSample / 8;
        var sampleCount = bytesRecorded / bytesPerSample;

        if (sampleCount == 0)
        {
            return 0f;
        }

        var normalizedSamples = ArrayPool<float>.Shared.Rent(sampleCount);

        try
        {
            var destination = normalizedSamples.AsSpan(0, sampleCount);
            var source = audioBytes[..(sampleCount * bytesPerSample)];

            if (IsIeeeFloat(format))
            {
                ConvertFloat32(source, destination);
            }
            else
            {
                switch (format.BitsPerSample)
                {
                    case 8:
                        ConvertPcm8(source, destination);
                        break;
                    case 16:
                        ConvertPcm16(source, destination);
                        break;
                    case 24:
                        ConvertPcm24(source, destination);
                        break;
                    case 32:
                        ConvertPcm32(source, destination);
                        break;
                    default:
                        throw new NotSupportedException("La profundidad PCM no está soportada.");
                }
            }

            return AudioLevel.SamplesToRelative(destination);
        }
        finally
        {
            ArrayPool<float>.Shared.Return(normalizedSamples, clearArray: true);
        }
    }

    private static void ConvertPcm8(ReadOnlySpan<byte> source, Span<float> destination)
    {
        for (var index = 0; index < destination.Length; index++)
        {
            destination[index] = (source[index] - 128) / 128f;
        }
    }

    private static void ConvertPcm16(ReadOnlySpan<byte> source, Span<float> destination)
    {
        for (var index = 0; index < destination.Length; index++)
        {
            destination[index] = BinaryPrimitives.ReadInt16LittleEndian(source[(index * 2)..]) / 32768f;
        }
    }

    private static void ConvertPcm24(ReadOnlySpan<byte> source, Span<float> destination)
    {
        for (var index = 0; index < destination.Length; index++)
        {
            var offset = index * 3;
            var sample = source[offset]
                         | source[offset + 1] << 8
                         | source[offset + 2] << 16;

            if ((sample & 0x0080_0000) != 0)
            {
                sample |= unchecked((int)0xFF00_0000);
            }

            destination[index] = sample / 8_388_608f;
        }
    }

    private static void ConvertPcm32(ReadOnlySpan<byte> source, Span<float> destination)
    {
        for (var index = 0; index < destination.Length; index++)
        {
            destination[index] = BinaryPrimitives.ReadInt32LittleEndian(source[(index * 4)..]) / 2_147_483_648f;
        }
    }

    private static void ConvertFloat32(ReadOnlySpan<byte> source, Span<float> destination)
    {
        for (var index = 0; index < destination.Length; index++)
        {
            var bits = BinaryPrimitives.ReadInt32LittleEndian(source[(index * 4)..]);
            var sample = BitConverter.Int32BitsToSingle(bits);
            destination[index] = float.IsFinite(sample) ? Math.Clamp(sample, -1f, 1f) : 0f;
        }
    }

    private static bool IsIeeeFloat(WaveFormat format)
    {
        return format.Encoding == WaveFormatEncoding.IeeeFloat
            || format is WaveFormatExtensible extensible && extensible.SubFormat == IeeeFloatSubFormat;
    }
}
