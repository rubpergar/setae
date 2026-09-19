namespace Setae.App.Monitoring;

public sealed record MonitorSnapshot(
    bool IsRunning,
    float Level,
    AlertState AlertState,
    string? ErrorMessage)
{
    public static MonitorSnapshot Stopped(string? errorMessage = null)
    {
        return new MonitorSnapshot(false, 0f, AlertState.Normal, errorMessage);
    }
}
