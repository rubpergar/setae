using Setae.Core;

namespace Setae.App;

public sealed record AppSettings
{
    public const double DefaultWindowWidth = 360;
    public const double DefaultWindowHeight = 250;

    public MonitoringPreferences Monitoring { get; init; } = MonitoringPreferences.Defaults;
    public double? WindowLeft { get; init; }
    public double? WindowTop { get; init; }
    public double WindowWidth { get; init; } = DefaultWindowWidth;
    public double WindowHeight { get; init; } = DefaultWindowHeight;
    public bool Topmost { get; init; } = true;

    public static AppSettings Defaults => new();
}

public sealed record SettingsLoadResult(AppSettings Settings, string? Warning);

internal static class AppSettingsValidator
{
    private const double MinimumWindowWidth = 300;
    private const double MaximumWindowWidth = 2_000;
    private const double MinimumWindowHeight = 210;
    private const double MaximumWindowHeight = 2_000;

    public static bool IsValid(AppSettings? settings)
    {
        if (settings is null || !MonitoringPreferencesValidator.IsValid(settings.Monitoring))
        {
            return false;
        }

        return IsFiniteOrNull(settings.WindowLeft)
            && IsFiniteOrNull(settings.WindowTop)
            && double.IsFinite(settings.WindowWidth)
            && settings.WindowWidth >= MinimumWindowWidth
            && settings.WindowWidth <= MaximumWindowWidth
            && double.IsFinite(settings.WindowHeight)
            && settings.WindowHeight >= MinimumWindowHeight
            && settings.WindowHeight <= MaximumWindowHeight;
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
