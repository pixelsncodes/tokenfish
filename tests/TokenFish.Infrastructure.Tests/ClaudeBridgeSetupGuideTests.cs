using System.Text.Json;
using TokenFish.Infrastructure;

namespace TokenFish.Infrastructure.Tests;

public sealed class ClaudeBridgeSetupGuideTests
{
    [Fact]
    public void RelativeExecutablePathUsesStableAppLayout()
    {
        Assert.Equal(
            @"tools\claude\TokenFish.ClaudeBridge.exe",
            ClaudeBridgeSetupGuide.RelativeExecutablePath);
    }

    [Fact]
    public void ExecutablePathResolvesFromApplicationBaseDirectory()
    {
        var path = ClaudeBridgeSetupGuide.ResolveExecutablePath(
            @"C:\Synthetic Apps\TokenFish");

        Assert.Equal(
            @"C:\Synthetic Apps\TokenFish\tools\claude\TokenFish.ClaudeBridge.exe",
            path);
    }

    [Fact]
    public void CommandUsesForwardSlashWindowsPathWhenPathContainsSpaces()
    {
        var command = ClaudeBridgeSetupGuide.CreateClaudeStatusLineCommand(
            @"C:\Synthetic Apps\TokenFish\tools\claude\TokenFish.ClaudeBridge.exe");

        Assert.Equal(
            "powershell -NoProfile -Command \"& 'C:/Synthetic Apps/TokenFish/tools/claude/TokenFish.ClaudeBridge.exe'\"",
            command);
    }

    [Fact]
    public void CommandEscapesPowerShellSingleQuotes()
    {
        var command = ClaudeBridgeSetupGuide.CreateClaudeStatusLineCommand(
            @"C:\Synthetic O'Brien\TokenFish\tools\claude\TokenFish.ClaudeBridge.exe");

        Assert.Contains("Synthetic O''Brien", command, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsSnippetEscapesJsonAndRoundTripsCommand()
    {
        var command = ClaudeBridgeSetupGuide.CreateClaudeStatusLineCommand(
            @"C:\Synthetic Apps\Tøken Fish\tools\claude\TokenFish.ClaudeBridge.exe");

        var snippet = ClaudeBridgeSetupGuide.CreateClaudeSettingsSnippet(command);

        using var document = JsonDocument.Parse(snippet);
        var statusLine = document.RootElement.GetProperty("statusLine");
        Assert.Equal("command", statusLine.GetProperty("type").GetString());
        Assert.Equal(command, statusLine.GetProperty("command").GetString());
        Assert.NotEqual(command, snippet);
    }

    [Fact]
    public void GeneratedSetupContainsNoRealUserOrRepositoryPath()
    {
        var setup = ClaudeBridgeSetupGuide.Create(@"C:\Synthetic Apps\Tøken Fish");

        Assert.DoesNotContain(@"C:\Users\pixel", setup.ExecutablePath, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/mnt/c/Users/pixel", setup.SettingsSnippet, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tokendock", setup.SettingsSnippet, StringComparison.OrdinalIgnoreCase);
    }
}
