using NAudio.Wave;
using Setae.App.Audio;

namespace Setae.App.Tests;

public class SampleLevelConverterTests
{
    [Fact]
    public void ToRelativeLevel_EmptyBufferReturnsSilence()
    {
        var format = new WaveFormat(48_000, 16, 1);

        Assert.Equal(0f, SampleLevelConverter.ToRelativeLevel([], 0, format));
    }

    [Fact]
    public void ToRelativeLevel_ConvertsPcm16Samples()
    {
        var format = new WaveFormat(48_000, 16, 1);
        var bytes = new byte[]
        {
            0x00, 0x40,
            0x00, 0xC0
        };

        var level = SampleLevelConverter.ToRelativeLevel(bytes, bytes.Length, format);

        Assert.InRange(level, 89f, 91f);
    }

    [Fact]
    public void ToRelativeLevel_UsesAllChannelsInTheBuffer()
    {
        var format = new WaveFormat(48_000, 16, 2);
        var bytes = new byte[]
        {
            0x00, 0x40, 0x00, 0x00,
            0x00, 0x40, 0x00, 0x00
        };

        var level = SampleLevelConverter.ToRelativeLevel(bytes, bytes.Length, format);

        Assert.InRange(level, 84f, 86f);
    }

    [Fact]
    public void ToRelativeLevel_ConvertsFloatAndIgnoresNonFiniteSamples()
    {
        var format = WaveFormat.CreateIeeeFloatWaveFormat(48_000, 1);
        var bytes = new byte[sizeof(float) * 2];
        Buffer.BlockCopy(new[] { 0.5f, float.NaN }, 0, bytes, 0, bytes.Length);

        var level = SampleLevelConverter.ToRelativeLevel(bytes, bytes.Length, format);

        Assert.InRange(level, 84f, 86f);
    }

    [Fact]
    public void ToRelativeLevel_RejectsUnsupportedFormat()
    {
        var format = new WaveFormat(48_000, 24, 1);
        var bytes = new byte[6];

        Assert.False(SampleLevelConverter.IsSupported(format));
        Assert.Throws<NotSupportedException>(() => SampleLevelConverter.ToRelativeLevel(bytes, bytes.Length, format));
    }
}