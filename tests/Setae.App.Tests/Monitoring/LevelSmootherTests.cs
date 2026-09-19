using Setae.App.Monitoring;

namespace Setae.App.Tests;

public class LevelSmootherTests
{
    [Fact]
    public void Update_RisesTowardTargetWithoutOvershooting()
    {
        var smoother = new LevelSmoother(risePerSecond: 100f, fallPerSecond: 20f);

        var result = smoother.Update(80f, TimeSpan.FromMilliseconds(500));

        Assert.Equal(50f, result);
    }

    [Fact]
    public void Update_FallsMoreSlowlyThanItRises()
    {
        var smoother = new LevelSmoother(risePerSecond: 100f, fallPerSecond: 20f);
        smoother.Update(100f, TimeSpan.FromSeconds(1));

        var result = smoother.Update(0f, TimeSpan.FromMilliseconds(500));

        Assert.Equal(90f, result);
    }
}
