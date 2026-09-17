using Setae.Core;

namespace Setae.App;

public sealed record MonitorSnapshot(
    bool IsRunning,
    float Level,
    AlertState AlertState,
    string StatusMessage,
    string? ErrorMessage)
{
    public static MonitorSnapshot Stopped(string statusMessage = "Detenido")
    {
        return new MonitorSnapshot(false, 0f, AlertState.Normal, statusMessage, null);
    }
}
