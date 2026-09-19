using Setae.App.Monitoring;

namespace Setae.App.Tests;

public class AlertDetectorTests
{
    private static MonitoringPreferences Preferences(
        TimeSpan? minimumDuration = null,
        TimeSpan? cooldown = null,
        float threshold = 70f)
    {
        return MonitoringPreferences.Defaults with
        {
            Threshold = threshold,
            MinimumAlertDuration = minimumDuration ?? TimeSpan.FromMilliseconds(500),
            Cooldown = cooldown ?? TimeSpan.FromSeconds(3)
        };
    }

    [Fact]
    public void BriefPeak_DoesNotTriggerAnAlert()
    {
        var detector = new AlertDetector(Preferences());

        Assert.False(detector.Process(75f, TimeSpan.FromMilliseconds(250)));
        Assert.False(detector.Process(65f, TimeSpan.FromMilliseconds(100)));
        Assert.Equal(AlertState.Normal, detector.State);
    }

    [Fact]
    public void SustainedExcess_TriggersOnceAfterMinimumDuration()
    {
        var detector = new AlertDetector(Preferences());

        Assert.False(detector.Process(80f, TimeSpan.FromMilliseconds(250)));
        Assert.Equal(AlertState.Pending, detector.State);
        Assert.True(detector.Process(80f, TimeSpan.FromMilliseconds(250)));
        Assert.Equal(AlertState.Cooldown, detector.State);
        Assert.False(detector.Process(80f, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void LevelExactlyAtThreshold_CountsAsExcess()
    {
        var detector = new AlertDetector(Preferences(
            minimumDuration: TimeSpan.FromMilliseconds(100),
            cooldown: TimeSpan.Zero));

        Assert.True(detector.Process(70f, TimeSpan.FromMilliseconds(100)));
    }

    [Fact]
    public void Cooldown_PreventsASecondAlertUntilItExpires()
    {
        var detector = new AlertDetector(Preferences(cooldown: TimeSpan.FromSeconds(2)));

        Assert.True(Trigger(detector));

        Assert.False(detector.Process(80f, TimeSpan.FromSeconds(2)));
        Assert.Equal(AlertState.Alerted, detector.State);

        Assert.False(detector.Process(80f, TimeSpan.FromMilliseconds(500)));
    }

    [Fact]
    public void Hysteresis_RequiresThreePointsBelowThresholdBeforeRearming()
    {
        var detector = new AlertDetector(Preferences(cooldown: TimeSpan.Zero));

        Assert.True(Trigger(detector));

        Assert.False(detector.Process(68f, TimeSpan.FromSeconds(1)));
        Assert.Equal(AlertState.Alerted, detector.State);
        Assert.False(detector.Process(80f, TimeSpan.FromMilliseconds(500)));

        Assert.False(detector.Process(67f, TimeSpan.FromSeconds(1)));
        Assert.Equal(AlertState.Normal, detector.State);
        Assert.False(detector.Process(80f, TimeSpan.FromMilliseconds(250)));
        Assert.True(detector.Process(80f, TimeSpan.FromMilliseconds(250)));
    }

    [Fact]
    public void RearmingDuringCooldown_AllowsANewSustainedAlertAfterCooldown()
    {
        var detector = new AlertDetector(Preferences(cooldown: TimeSpan.FromSeconds(2)));

        Assert.True(Trigger(detector));

        Assert.False(detector.Process(65f, TimeSpan.FromSeconds(1)));
        Assert.Equal(AlertState.Cooldown, detector.State);
        Assert.False(detector.Process(65f, TimeSpan.FromSeconds(1)));
        Assert.Equal(AlertState.Normal, detector.State);
        Assert.False(detector.Process(80f, TimeSpan.FromMilliseconds(250)));
        Assert.True(detector.Process(80f, TimeSpan.FromMilliseconds(250)));
    }

    private static bool Trigger(AlertDetector detector)
    {
        detector.Process(80f, TimeSpan.FromMilliseconds(250));
        return detector.Process(80f, TimeSpan.FromMilliseconds(250));
    }
}
