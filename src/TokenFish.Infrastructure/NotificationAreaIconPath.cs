namespace TokenFish.Infrastructure;

public static class NotificationAreaIconPath
{
    public static string Resolve(string applicationBaseDirectory) =>
        ApplicationIconPath.ResolveWindowIcon(applicationBaseDirectory);
}
