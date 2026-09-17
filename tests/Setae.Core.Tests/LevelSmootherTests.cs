using Setae.Core;

namespace Setae.Core.Tests;

public class LevelSmootherTests
{
    [Fact]
    public void Update_RisesTowardTargetWithoutOvershooting()
    {
        var smoother = new LevelSmoother(risePerSecond: 100f, fallPerSecond: 20f);

        var result = smoother.Update(80f, TimeSpan.FromMilliseconds(500));

        Assert.Equal(50f, result);
        Assert.Equal(50f, smoother.Current);
    }

    [Fact]
    public void Update_FallsMoreSlowlyThanItRises()
    {
        var smoother = new LevelSmoother(risePerSecond: 100f, fallPerSecond: 20f);
        smoother.Update(100f, TimeSpan.FromSeconds(1));

        var result = smoother.Update(0f, TimeSpan.FromMilliseconds(500));

        Assert.Equal(90f, result);
    }

    [Fact]
    public void Reset_SetsTheCurrentLevel()
    {
        var smoother = new LevelSmoother();

        smoother.Reset(42f);

        Assert.Equal(42f, smoother.Current);
    }
}
