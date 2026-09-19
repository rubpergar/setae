namespace Setae.App.Audio;

/// <summary>
/// Convierte el RMS de una señal normalizada al nivel relativo de 0 a 100 que usa Setae.
/// </summary>
public static class AudioLevel
{
    public const float MinimumDbfs = -60f;
    public const float MaximumDbfs = 0f;
    public const float MinimumRelativeLevel = 0f;
    public const float MaximumRelativeLevel = 100f;

    private const float RmsEpsilon = 1e-12f;

    /// <summary>
    /// Convierte amplitud RMS a dBFS. El valor mínimo evita problemas con el silencio.
    /// </summary>
    public static float RmsToDbfs(float rms)
    {
        return 20f * MathF.Log10(MathF.Max(rms, RmsEpsilon));
    }

    /// <summary>
    /// Mapea dBFS a 0..100, limitando los valores fuera del rango -60..0.
    /// </summary>
    public static float DbfsToRelative(float dbfs)
    {
        var relative = ((dbfs - MinimumDbfs) / (MaximumDbfs - MinimumDbfs)) * MaximumRelativeLevel;
        return Math.Clamp(relative, MinimumRelativeLevel, MaximumRelativeLevel);
    }
}