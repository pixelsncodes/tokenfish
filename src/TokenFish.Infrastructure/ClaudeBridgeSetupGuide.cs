using System.Text.Json;

namespace TokenFish.Infrastructure;

public static class ClaudeBridgeSetupGuide
{
    public const string ExecutableFileName = "TokenFish.ClaudeBridge.exe";
    public const string RelativeExecutablePath = @"tools\claude\" + ExecutableFileName;

    public static ClaudeBridgeSetup Create(string applicationBaseDirectory)
    {
        var executablePath = ResolveExecutablePath(applicationBaseDirectory);
        var command = CreateClaudeStatusLineCommand(executablePath);
        var wslExecutablePath = CreateWslMountedExecutablePath(executablePath);
        var wslCommand = CreateClaudeWslStatusLineCommand(wslExecutablePath);
        return new ClaudeBridgeSetup(
            executablePath,
            command,
            CreateClaudeSettingsSnippet(command),
            wslExecutablePath,
            wslCommand,
            CreateClaudeSettingsSnippet(wslCommand));
    }

    public static string ResolveExecutablePath(string applicationBaseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationBaseDirectory);

        return Path.GetFullPath(Path.Combine(
            applicationBaseDirectory,
            "tools",
            "claude",
            ExecutableFileName));
    }

    public static string CreateClaudeStatusLineCommand(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

        var commandPath = executablePath
            .Replace('\\', '/')
            .Replace("'", "''", StringComparison.Ordinal);
        return $"powershell -NoProfile -Command \"& '{commandPath}'\"";
    }

    public static string CreateWslMountedExecutablePath(string windowsExecutablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(windowsExecutablePath);

        if (TryCreateWslUncPath(windowsExecutablePath, out var wslUncPath))
        {
            return wslUncPath;
        }

        if (windowsExecutablePath.Length < 3 ||
            !IsAsciiLetter(windowsExecutablePath[0]) ||
            windowsExecutablePath[1] != ':' ||
            (windowsExecutablePath[2] != '\\' && windowsExecutablePath[2] != '/'))
        {
            throw new ArgumentException(
                "The executable path must be an absolute Windows drive path or WSL UNC path.",
                nameof(windowsExecutablePath));
        }

        var drive = char.ToLowerInvariant(windowsExecutablePath[0]);
        var pathWithinDrive = windowsExecutablePath[3..].Replace('\\', '/');
        return $"/mnt/{drive}/{pathWithinDrive}";
    }

    public static string CreateClaudeWslStatusLineCommand(string wslExecutablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(wslExecutablePath);

        return $"\"{wslExecutablePath}\"";
    }

    public static string CreateClaudeSettingsSnippet(string command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);

        return
            "{\n" +
            "  \"statusLine\": {\n" +
            "    \"type\": \"command\",\n" +
            $"    \"command\": {CreateJsonString(command)}\n" +
            "  }\n" +
            "}";
    }

    private static string CreateJsonString(string value) =>
        $"\"{JsonEncodedText.Encode(value)}\"";

    private static bool IsAsciiLetter(char value) =>
        value is >= 'A' and <= 'Z' or >= 'a' and <= 'z';

    private static bool TryCreateWslUncPath(string windowsPath, out string wslPath)
    {
        var normalizedPath = windowsPath.Replace('/', '\\');
        if (!normalizedPath.StartsWith(@"\\", StringComparison.Ordinal))
        {
            wslPath = string.Empty;
            return false;
        }

        var segments = normalizedPath[2..].Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 3 ||
            (!string.Equals(segments[0], "wsl.localhost", StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(segments[0], "wsl$", StringComparison.OrdinalIgnoreCase)))
        {
            wslPath = string.Empty;
            return false;
        }

        wslPath = "/" + string.Join('/', segments[2..]);
        return true;
    }
}

public sealed record ClaudeBridgeSetup(
    string ExecutablePath,
    string Command,
    string SettingsSnippet,
    string WslExecutablePath,
    string WslCommand,
    string WslSettingsSnippet);
