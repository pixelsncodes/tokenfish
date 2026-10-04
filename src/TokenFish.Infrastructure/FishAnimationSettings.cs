namespace TokenFish.Infrastructure;

public static class FishAnimationSettings
{
    public static bool ShouldAnimate(bool systemAnimationsEnabled, bool isVisible = true, bool isLoaded = true) =>
        systemAnimationsEnabled && isVisible && isLoaded;
}
