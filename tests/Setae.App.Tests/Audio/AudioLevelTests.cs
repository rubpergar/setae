using Setae.App.Audio;

namespace Setae.App.Tests;

public class AudioLevelTests
{
    [Fact]
    public void DbfsToRelative_MapsTheReferenceRange()
    {
        Assert.Equal(0f, AudioLevel.DbfsToRelative(-60f));
        Assert.Equal(50f, AudioLevel.DbfsToRelative(-30f));
        Assert.Equal(100f, AudioLevel.DbfsToRelative(0f));
    }

    [Fact]
    public void DbfsToRelative_ClampsValuesOutsideTheRange()
    {
        Assert.Equal(0f, AudioLevel.DbfsToRelative(-80f));
        Assert.Equal(100f, AudioLevel.DbfsToRelative(6f));
    }

    [Fact]
    public void RmsToDbfs_SilenceIsBelowTheVisibleRange()
    {
        Assert.True(AudioLevel.RmsToDbfs(0f) < AudioLevel.MinimumDbfs);
    }

    [Fact]
    public void RmsToDbfs_FullScaleSignalReachesZeroDbfs()
    {
        Assert.Equal(0f, AudioLevel.RmsToDbfs(1f), 5);
    }
}