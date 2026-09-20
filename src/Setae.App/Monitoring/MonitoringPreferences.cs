using Setae.App.Audio;

namespace Setae.App.Monitoring;

/// <summary>
/// User-configurable values used by the monitor and alert detector.
/// </summary>
public sealed record MonitoringPreferences
{
    public const float DefaultThreshold = 50f;
    public static readonly TimeSpan DefaultMinimumAlertDuration = TimeSpan.FromMilliseconds(250);
    public static readonly TimeSpan DefaultCooldown = TimeSpan.FromSeconds(2);

    public string? MicrophoneId { get; init; }
    public float Threshold { get; init; } = DefaultThreshold;
    public TimeSpan MinimumAlertDuration { get; init; } = DefaultMinimumAlertDuration;
    public TimeSpan Cooldown { get; init; } = DefaultCooldown;
    public bool BeepEnabled { get; init; } = true;

    public static MonitoringPreferences Defaults => new();
}

public static class MonitoringPreferencesValidator
{
    public const float MinimumThreshold = AudioLevel.MinimumRelativeLevel;
    public const float MaximumThreshold = AudioLevel.MaximumRelativeLevel;
    public static readonly TimeSpan MinimumAlertDuration = TimeSpan.FromMilliseconds(100);
    public static readonly TimeSpan MaximumAlertDuration = TimeSpan.FromSeconds(3);
    public static readonly TimeSpan MinimumCooldown = TimeSpan.Zero;
    public static readonly TimeSpan MaximumCooldown = TimeSpan.FromSeconds(30);

    public static bool IsValid(MonitoringPreferences? preferences)
    {
        if (preferences is null)
        {
            return false;
        }

        return (preferences.MicrophoneId is null || !string.IsNullOrWhiteSpace(preferences.MicrophoneId))
            && float.IsFinite(preferences.Threshold)
            && preferences.Threshold >= MinimumThreshold
            && preferences.Threshold <= MaximumThreshold
            && preferences.MinimumAlertDuration >= MinimumAlertDuration
            && preferences.MinimumAlertDuration <= MaximumAlertDuration
            && preferences.Cooldown >= MinimumCooldown
            && preferences.Cooldown <= MaximumCooldown;
    }

    public static void EnsureValid(MonitoringPreferences? preferences)
    {
        if (preferences is null)
        {
            throw new ArgumentNullException(nameof(preferences));
        }

        if (preferences.MicrophoneId is not null && string.IsNullOrWhiteSpace(preferences.MicrophoneId))
        {
            throw new ArgumentException("The microphone identifier cannot be empty.", nameof(preferences));
        }

        if (!float.IsFinite(preferences.Threshold)
            || preferences.Threshold < MinimumThreshold
            || preferences.Threshold > MaximumThreshold)
        {
            throw new ArgumentOutOfRangeException(nameof(preferences), "The threshold must be between 0 and 100.");
        }

        if (preferences.MinimumAlertDuration < MinimumAlertDuration
            || preferences.MinimumAlertDuration > MaximumAlertDuration)
        {
            throw new ArgumentOutOfRangeException(nameof(preferences), "The minimum duration must be between 100 ms and 3 s.");
        }

        if (preferences.Cooldown < MinimumCooldown || preferences.Cooldown > MaximumCooldown)
        {
            throw new ArgumentOutOfRangeException(nameof(preferences), "Cooldown must be between 0 and 30 s.");
        }
    }
}
