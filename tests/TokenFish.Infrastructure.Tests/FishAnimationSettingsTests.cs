using TokenFish.Infrastructure;

namespace TokenFish.Infrastructure.Tests;

public sealed class FishAnimationSettingsTests
{
    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, true, true, false)]
    [InlineData(true, false, false, false)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, false, false)]
    [InlineData(true, true, true, true)]
    public void AnimationRequiresSystemMotionAndAVisibleLoadedRail(
        bool systemAnimationsEnabled, bool isVisible, bool isLoaded, bool expected) =>
        Assert.Equal(expected, FishAnimationSettings.ShouldAnimate(systemAnimationsEnabled, isVisible, isLoaded));

    [Fact]
    public void DisabledSystemAnimationsKeepTheFishStatic() =>
        Assert.False(FishAnimationSettings.ShouldAnimate(systemAnimationsEnabled: false));

    [Fact]
    public void EnabledSystemAnimationsAllowTheSubtleFishAnimation() =>
        Assert.True(FishAnimationSettings.ShouldAnimate(systemAnimationsEnabled: true));
}
