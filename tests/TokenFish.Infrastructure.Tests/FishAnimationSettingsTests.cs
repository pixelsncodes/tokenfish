using TokenFish.Infrastructure;

namespace TokenFish.Infrastructure.Tests;

public sealed class FishAnimationSettingsTests
{
    [Fact]
    public void DisabledSystemAnimationsKeepTheFishStatic() =>
        Assert.False(FishAnimationSettings.ShouldAnimate(systemAnimationsEnabled: false));

    [Fact]
    public void EnabledSystemAnimationsAllowTheSubtleFishAnimation() =>
        Assert.True(FishAnimationSettings.ShouldAnimate(systemAnimationsEnabled: true));
}
