namespace TokenFish.Infrastructure;

public static class ApplicationIconPath
{
    public const string WindowIconFileName = "AppIcon.ico";
    public const string HeaderIconFileName = "Square44x44Logo.targetsize-24_altform-unplated.png";

    public static string ResolveWindowIcon(string applicationBaseDirectory) =>
        ResolveAsset(applicationBaseDirectory, WindowIconFileName);

    public static string ResolveHeaderIcon(string applicationBaseDirectory) =>
        ResolveAsset(applicationBaseDirectory, HeaderIconFileName);

    public static bool TryResolveExistingWindowIcon(
        string applicationBaseDirectory,
        out string iconPath) =>
        TryResolveExistingAsset(applicationBaseDirectory, WindowIconFileName, out iconPath);

    public static bool TryResolveExistingHeaderIcon(
        string applicationBaseDirectory,
        out string iconPath) =>
        TryResolveExistingAsset(applicationBaseDirectory, HeaderIconFileName, out iconPath);

    private static bool TryResolveExistingAsset(
        string applicationBaseDirectory,
        string fileName,
        out string iconPath)
    {
        iconPath = ResolveAsset(applicationBaseDirectory, fileName);
        return File.Exists(iconPath);
    }

    private static string ResolveAsset(string applicationBaseDirectory, string fileName) =>
        Path.Combine(applicationBaseDirectory, "Assets", fileName);
}
