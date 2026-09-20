namespace Setae.App.Audio;

/// <summary>
/// Converts normalized signal RMS to the relative 0 to 100 level used by Setae.
/// </summary>
public static class AudioLevel
{
    public const float MinimumDbfs = -60f;
    public const float MaximumDbfs = 0f;
    public const float MinimumRelativeLevel = 0f;
    public const float MaximumRelativeLevel = 100f;

    private const float RmsEpsilon = 1e-12f;

    /// <summary>
    /// Converts RMS amplitude to dBFS. The minimum value avoids silence-related math issues.
    /// </summary>
    public static float RmsToDbfs(float rms)
    {
        return 20f * MathF.Log10(MathF.Max(rms, RmsEpsilon));
    }

    /// <summary>
    /// Maps dBFS to 0..100, clamping values outside the -60..0 range.
    /// </summary>
    public static float DbfsToRelative(float dbfs)
    {
        var relative = ((dbfs - MinimumDbfs) / (MaximumDbfs - MinimumDbfs)) * MaximumRelativeLevel;
        return Math.Clamp(relative, MinimumRelativeLevel, MaximumRelativeLevel);
    }
}
