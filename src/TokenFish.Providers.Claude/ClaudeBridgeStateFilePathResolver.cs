namespace TokenFish.Providers.Claude;

internal sealed class ClaudeBridgeStateFilePathResolver
{
    private const string StateFileName = "claude-status-v1.json";

    private readonly Func<Environment.SpecialFolder, string> _getFolderPath;

    public ClaudeBridgeStateFilePathResolver()
        : this(Environment.GetFolderPath)
    {
    }

    internal ClaudeBridgeStateFilePathResolver(
        Func<Environment.SpecialFolder, string> getFolderPath)
    {
        _getFolderPath = getFolderPath ?? throw new ArgumentNullException(nameof(getFolderPath));
    }

    public string GetDefaultStateFilePath()
    {
        var localApplicationData = _getFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localApplicationData, "TokenFish", "bridge", StateFileName);
    }
}
