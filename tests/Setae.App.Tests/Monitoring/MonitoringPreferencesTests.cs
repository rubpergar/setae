using Setae.App.Monitoring;

namespace Setae.App.Tests;

public class MonitoringPreferencesTests
{
    [Fact]
    public void Defaults_MatchTheSpecification()
    {
        var defaults = MonitoringPreferences.Defaults;

        Assert.Equal(50f, defaults.Threshold);
        Assert.Equal(TimeSpan.FromMilliseconds(250), defaults.MinimumAlertDuration);
        Assert.Equal(TimeSpan.FromSeconds(2), defaults.Cooldown);
        Assert.True(defaults.BeepEnabled);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(100f)]
    public void IsValid_AcceptsThresholdBoundaries(float threshold)
    {
        var preferences = MonitoringPreferences.Defaults with { Threshold = threshold };

        Assert.True(MonitoringPreferencesValidator.IsValid(preferences));
    }

    [Fact]
    public void IsValid_RejectsValuesOutsideConfiguredRanges()
    {
        Assert.False(MonitoringPreferencesValidator.IsValid(
            MonitoringPreferences.Defaults with { Threshold = -1f }));
        Assert.False(MonitoringPreferencesValidator.IsValid(
            MonitoringPreferences.Defaults with { MinimumAlertDuration = TimeSpan.FromMilliseconds(99) }));
        Assert.False(MonitoringPreferencesValidator.IsValid(
            MonitoringPreferences.Defaults with { Cooldown = TimeSpan.FromSeconds(31) }));
        Assert.False(MonitoringPreferencesValidator.IsValid(
            MonitoringPreferences.Defaults with { MicrophoneId = " " }));
    }

    [Fact]
    public void RestoreDefaultsIfInvalid_ReturnsDefaults()
    {
        var invalid = MonitoringPreferences.Defaults with { Threshold = float.NaN };

        var restored = MonitoringPreferencesValidator.RestoreDefaultsIfInvalid(invalid);

        Assert.Equal(MonitoringPreferences.Defaults, restored);
    }
}
