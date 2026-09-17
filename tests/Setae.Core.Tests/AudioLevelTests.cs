using Setae.Core;

namespace Setae.Core.Tests;

public class AudioLevelTests
{
    [Fact]
    public void CalculateRms_EmptySamples_ReturnsSilence()
    {
        Assert.Equal(0f, AudioLevel.CalculateRms(ReadOnlySpan<float>.Empty));
        Assert.Equal(0f, AudioLevel.SamplesToRelative(ReadOnlySpan<float>.Empty));
    }

    [Fact]
    public void CalculateRms_KnownSamples_ReturnsRootMeanSquare()
    {
        var samples = new[] { 1f, -1f, 0f, 0f };

        var rms = AudioLevel.CalculateRms(samples);

        Assert.Equal(0.70710677f, rms, 5);
    }

    [Fact]
    public void CalculateRms_UsesAllChannelsInTheBuffer()
    {
        var samples = new[] { 0.5f, 0.5f, -0.5f, -0.5f };

        Assert.Equal(0.5f, AudioLevel.CalculateRms(samples), 6);
    }

    [Fact]
    public void Pcm16Conversion_MapsSignedFullScaleToNormalizedFloat()
    {
        var pcm = new short[] { short.MinValue, 0, short.MaxValue };
        Span<float> normalized = stackalloc float[pcm.Length];

        AudioLevel.ConvertPcm16ToNormalized(pcm, normalized);

        Assert.Equal(-1f, normalized[0]);
        Assert.Equal(0f, normalized[1]);
        Assert.Equal(short.MaxValue / 32768f, normalized[2]);
    }

    [Fact]
    public void DbfsToRelative_MapsAndClampsTheConfiguredRange()
    {
        Assert.Equal(0f, AudioLevel.DbfsToRelative(-60f));
        Assert.Equal(50f, AudioLevel.DbfsToRelative(-30f));
        Assert.Equal(100f, AudioLevel.DbfsToRelative(0f));
        Assert.Equal(0f, AudioLevel.DbfsToRelative(-80f));
        Assert.Equal(100f, AudioLevel.DbfsToRelative(6f));
    }

    [Fact]
    public void RmsToDbfs_SilenceIsBelowTheVisibleRange()
    {
        Assert.True(AudioLevel.RmsToDbfs(0f) < AudioLevel.MinimumDbfs);
    }
}
