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
        return new ClaudeBridgeSetup(
            executablePath,
            command,
            CreateClaudeSettingsSnippet(command));
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
}

public sealed record ClaudeBridgeSetup(
    string ExecutablePath,
    string Command,
    string SettingsSnippet);
