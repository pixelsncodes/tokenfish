namespace TokenFish.Infrastructure;

public static class NotificationAreaIconPath
{
    public static string Resolve(string applicationBaseDirectory) =>
        Path.Combine(applicationBaseDirectory, "Assets", "AppIcon.ico");
}
