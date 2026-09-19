using Setae.App.Audio;

namespace Setae.App.Monitoring;

/// <summary>
/// Smooths a relative level with independent attack and release speeds.
/// </summary>
public sealed class LevelSmoother
{
    public const float DefaultRisePerSecond = 300f;
    public const float DefaultFallPerSecond = 100f;

    private readonly float _risePerSecond;
    private readonly float _fallPerSecond;

    public LevelSmoother(
        float risePerSecond = DefaultRisePerSecond,
        float fallPerSecond = DefaultFallPerSecond)
    {
        if (!float.IsFinite(risePerSecond) || risePerSecond <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(risePerSecond));
        }

        if (!float.IsFinite(fallPerSecond) || fallPerSecond <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(fallPerSecond));
        }

        _risePerSecond = risePerSecond;
        _fallPerSecond = fallPerSecond;
    }

    private float Current { get; set; }

    /// <summary>
    /// Moves toward the target by a time-scaled amount and clamps to the target.
    /// </summary>
    public float Update(float target, TimeSpan elapsed)
    {
        ValidateLevel(target);

        if (elapsed < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsed));
        }

        if (elapsed == TimeSpan.Zero || target == Current)
        {
            return Current;
        }

        var rate = target > Current ? _risePerSecond : _fallPerSecond;
        var distance = rate * (float)elapsed.TotalSeconds;

        Current = target > Current
            ? MathF.Min(target, Current + distance)
            : MathF.Max(target, Current - distance);

        return Current;
    }

    private static void ValidateLevel(float level)
    {
        if (!float.IsFinite(level) || level < AudioLevel.MinimumRelativeLevel || level > AudioLevel.MaximumRelativeLevel)
        {
            throw new ArgumentOutOfRangeException(nameof(level), "El nivel relativo debe estar entre 0 y 100.");
        }
    }
}
