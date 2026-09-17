namespace Setae.Core;

public enum AlertState
{
    Normal,
    Pending,
    Alerted,
    Cooldown
}

public sealed class AlertTriggeredEventArgs : EventArgs
{
    public AlertTriggeredEventArgs(float relativeLevel)
    {
        RelativeLevel = relativeLevel;
    }

    public float RelativeLevel { get; }
}

/// <summary>
/// Detects sustained levels above the threshold without repeating an event until rearmed.
/// </summary>
public sealed class AlertDetector
{
    private readonly float _threshold;
    private readonly TimeSpan _minimumAlertDuration;
    private readonly TimeSpan _cooldown;
    private readonly float _rearmThreshold;

    private TimeSpan _pendingDuration;
    private TimeSpan _cooldownRemaining;
    private bool _requiresRearm;

    public AlertDetector(MonitoringPreferences preferences)
    {
        MonitoringPreferencesValidator.EnsureValid(preferences);

        _threshold = preferences.Threshold;
        _minimumAlertDuration = preferences.MinimumAlertDuration;
        _cooldown = preferences.Cooldown;
        _rearmThreshold = MathF.Max(AudioLevel.MinimumRelativeLevel, _threshold - 3f);
    }

    public AlertState State { get; private set; } = AlertState.Normal;

    public event EventHandler<AlertTriggeredEventArgs>? AlertTriggered;

    /// <summary>
    /// Processes one smoothed level and returns true exactly when it triggers an event.
    /// </summary>
    public bool Process(float relativeLevel, TimeSpan elapsed)
    {
        ValidateInput(relativeLevel, elapsed);

        if (_requiresRearm && relativeLevel <= _rearmThreshold)
        {
            _requiresRearm = false;
        }

        var cooldownExpired = false;

        if (State == AlertState.Cooldown)
        {
            _cooldownRemaining = _cooldownRemaining > elapsed
                ? _cooldownRemaining - elapsed
                : TimeSpan.Zero;

            cooldownExpired = _cooldownRemaining == TimeSpan.Zero;

            if (!cooldownExpired)
            {
                return false;
            }
        }

        if (_requiresRearm)
        {
            State = AlertState.Alerted;
            _pendingDuration = TimeSpan.Zero;
            return false;
        }

        State = AlertState.Normal;

        if (relativeLevel < _threshold)
        {
            _pendingDuration = TimeSpan.Zero;
            return false;
        }

        if (!cooldownExpired)
        {
            _pendingDuration += elapsed;
        }

        if (_pendingDuration < _minimumAlertDuration)
        {
            State = AlertState.Pending;
            return false;
        }

        _pendingDuration = TimeSpan.Zero;
        _requiresRearm = true;
        _cooldownRemaining = _cooldown;
        State = _cooldown > TimeSpan.Zero ? AlertState.Cooldown : AlertState.Alerted;

        AlertTriggered?.Invoke(this, new AlertTriggeredEventArgs(relativeLevel));
        return true;
    }

    public void Reset()
    {
        _pendingDuration = TimeSpan.Zero;
        _cooldownRemaining = TimeSpan.Zero;
        _requiresRearm = false;
        State = AlertState.Normal;
    }

    private static void ValidateInput(float relativeLevel, TimeSpan elapsed)
    {
        if (!float.IsFinite(relativeLevel)
            || relativeLevel < AudioLevel.MinimumRelativeLevel
            || relativeLevel > AudioLevel.MaximumRelativeLevel)
        {
            throw new ArgumentOutOfRangeException(nameof(relativeLevel), "El nivel relativo debe estar entre 0 y 100.");
        }

        if (elapsed < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsed));
        }
    }
}
