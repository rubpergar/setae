using Setae.App.Monitoring;

namespace Setae.App.Infrastructure;

public sealed record AppSettings
{
    public MonitoringPreferences Monitoring { get; init; } = MonitoringPreferences.Defaults;
    public double? WindowLeft { get; init; }
    public double? WindowTop { get; init; }
    public bool Topmost { get; init; } = true;

    public static AppSettings Defaults => new();
}

public sealed record SettingsLoadResult(AppSettings Settings, string? Warning);

internal static class AppSettingsValidator
{
    public static bool IsValid(AppSettings? settings)
    {
        return settings is not null
            && MonitoringPreferencesValidator.IsValid(settings.Monitoring)
            && IsFiniteOrNull(settings.WindowLeft)
            && IsFiniteOrNull(settings.WindowTop);
    }

    public static void EnsureValid(AppSettings settings)
    {
        if (!IsValid(settings))
        {
            throw new ArgumentException("Los ajustes de Setae no son válidos.", nameof(settings));
        }
    }

    private static bool IsFiniteOrNull(double? value)
    {
        return value is null || double.IsFinite(value.Value) && Math.Abs(value.Value) <= 1_000_000;
    }
}
