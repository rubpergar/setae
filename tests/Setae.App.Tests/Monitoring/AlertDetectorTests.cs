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
        var events = 0;
        detector.AlertTriggered += (_, _) => events++;

        Assert.False(detector.Process(75f, TimeSpan.FromMilliseconds(250)));
        Assert.False(detector.Process(65f, TimeSpan.FromMilliseconds(100)));
        Assert.Equal(AlertState.Normal, detector.State);
        Assert.Equal(0, events);
    }

    [Fact]
    public void SustainedExcess_TriggersOnceAfterMinimumDuration()
    {
        var detector = new AlertDetector(Preferences());
        var events = 0;
        float? eventLevel = null;
        detector.AlertTriggered += (_, args) =>
        {
            events++;
            eventLevel = args.RelativeLevel;
        };

        Assert.False(detector.Process(80f, TimeSpan.FromMilliseconds(250)));
        Assert.Equal(AlertState.Pending, detector.State);
        Assert.True(detector.Process(80f, TimeSpan.FromMilliseconds(250)));
        Assert.Equal(1, events);
        Assert.Equal(80f, eventLevel!.Value);
        Assert.Equal(AlertState.Cooldown, detector.State);
        Assert.False(detector.Process(80f, TimeSpan.FromSeconds(1)));
        Assert.Equal(1, events);
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
        var events = 0;
        detector.AlertTriggered += (_, _) => events++;

        Trigger(detector);

        Assert.False(detector.Process(80f, TimeSpan.FromSeconds(2)));
        Assert.Equal(1, events);
        Assert.Equal(AlertState.Alerted, detector.State);

        Assert.False(detector.Process(80f, TimeSpan.FromMilliseconds(500)));
        Assert.Equal(1, events);
    }

    [Fact]
    public void Hysteresis_RequiresThreePointsBelowThresholdBeforeRearming()
    {
        var detector = new AlertDetector(Preferences(cooldown: TimeSpan.Zero));
        var events = 0;
        detector.AlertTriggered += (_, _) => events++;

        Trigger(detector);

        Assert.False(detector.Process(68f, TimeSpan.FromSeconds(1)));
        Assert.Equal(AlertState.Alerted, detector.State);
        Assert.False(detector.Process(80f, TimeSpan.FromMilliseconds(500)));
        Assert.Equal(1, events);

        Assert.False(detector.Process(67f, TimeSpan.FromSeconds(1)));
        Assert.Equal(AlertState.Normal, detector.State);
        Assert.False(detector.Process(80f, TimeSpan.FromMilliseconds(250)));
        Assert.True(detector.Process(80f, TimeSpan.FromMilliseconds(250)));
        Assert.Equal(2, events);
    }

    [Fact]
    public void RearmingDuringCooldown_AllowsANewSustainedAlertAfterCooldown()
    {
        var detector = new AlertDetector(Preferences(cooldown: TimeSpan.FromSeconds(2)));
        var events = 0;
        detector.AlertTriggered += (_, _) => events++;

        Trigger(detector);

        Assert.False(detector.Process(65f, TimeSpan.FromSeconds(1)));
        Assert.Equal(AlertState.Cooldown, detector.State);
        Assert.False(detector.Process(65f, TimeSpan.FromSeconds(1)));
        Assert.Equal(AlertState.Normal, detector.State);
        Assert.False(detector.Process(80f, TimeSpan.FromMilliseconds(250)));
        Assert.True(detector.Process(80f, TimeSpan.FromMilliseconds(250)));
        Assert.Equal(2, events);
    }

    [Fact]
    public void Reset_ClearsPendingAndCooldownState()
    {
        var detector = new AlertDetector(Preferences());
        var events = 0;
        detector.AlertTriggered += (_, _) => events++;

        Assert.False(detector.Process(80f, TimeSpan.FromMilliseconds(250)));
        detector.Reset();

        Assert.Equal(AlertState.Normal, detector.State);
        Assert.True(detector.Process(80f, TimeSpan.FromMilliseconds(500)));
        Assert.Equal(1, events);
    }

    private static void Trigger(AlertDetector detector)
    {
        Assert.False(detector.Process(80f, TimeSpan.FromMilliseconds(250)));
        Assert.True(detector.Process(80f, TimeSpan.FromMilliseconds(250)));
    }
}
