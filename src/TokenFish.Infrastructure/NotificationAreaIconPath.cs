namespace TokenFish.Infrastructure;

public static class NotificationAreaIconPath
{
    public const string IconFileName = "TrayIcon.ico";

    public static string Resolve(string applicationBaseDirectory) =>
        Path.Combine(applicationBaseDirectory, "Assets", IconFileName);
}
