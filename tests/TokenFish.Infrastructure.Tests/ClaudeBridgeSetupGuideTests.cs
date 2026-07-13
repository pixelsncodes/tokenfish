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

    [Theory]
    [InlineData(
        @"C:\Synthetic Apps\TokenFish\tools\claude\TokenFish.ClaudeBridge.exe",
        "/mnt/c/Synthetic Apps/TokenFish/tools/claude/TokenFish.ClaudeBridge.exe")]
    [InlineData(
        @"D:\Published Apps\Tøken Fish\tools\claude\TokenFish.ClaudeBridge.exe",
        "/mnt/d/Published Apps/Tøken Fish/tools/claude/TokenFish.ClaudeBridge.exe")]
    [InlineData(
        @"Z:\Bridge\TokenFish.ClaudeBridge.exe",
        "/mnt/z/Bridge/TokenFish.ClaudeBridge.exe")]
    public void WslMountedPathConvertsWindowsDrivesAndPreservesPathCharacters(
        string windowsPath,
        string expectedWslPath)
    {
        var wslPath = ClaudeBridgeSetupGuide.CreateWslMountedExecutablePath(windowsPath);

        Assert.Equal(expectedWslPath, wslPath);
    }

    [Theory]
    [InlineData(
        @"\\wsl.localhost\Ubuntu\tmp\Token Fish\TokenFish.ClaudeBridge.exe",
        "/tmp/Token Fish/TokenFish.ClaudeBridge.exe")]
    [InlineData(
        @"\\wsl$\Dëbian\home\naïve\TokenFish.ClaudeBridge.exe",
        "/home/naïve/TokenFish.ClaudeBridge.exe")]
    public void WslUncPathConvertsToTheDistributionLocalAbsolutePath(
        string windowsPath,
        string expectedWslPath)
    {
        var wslPath = ClaudeBridgeSetupGuide.CreateWslMountedExecutablePath(windowsPath);

        Assert.Equal(expectedWslPath, wslPath);
    }

    [Fact]
    public void WslSettingsSnippetUsesDirectQuotedExecutableInvocation()
    {
        var setup = ClaudeBridgeSetupGuide.Create(@"C:\Synthetic Apps\Tøken Fish");

        using var document = JsonDocument.Parse(setup.WslSettingsSnippet);
        var command = document.RootElement
            .GetProperty("statusLine")
            .GetProperty("command")
            .GetString();

        Assert.Equal(
            "\"/mnt/c/Synthetic Apps/Tøken Fish/tools/claude/TokenFish.ClaudeBridge.exe\"",
            command);
        Assert.DoesNotContain("powershell", setup.WslSettingsSnippet, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WindowsSettingsSnippetRemainsTheVerifiedPowerShellCommand()
    {
        var setup = ClaudeBridgeSetupGuide.Create(@"C:\Synthetic Apps\TokenFish");

        Assert.Equal(
            "powershell -NoProfile -Command \"& 'C:/Synthetic Apps/TokenFish/tools/claude/TokenFish.ClaudeBridge.exe'\"",
            setup.Command);
        Assert.Contains("powershell -NoProfile -Command", setup.SettingsSnippet, StringComparison.Ordinal);
    }

    [Fact]
    public void GeneratedSetupContainsNoRealUserOrRepositoryPath()
    {
        var setup = ClaudeBridgeSetupGuide.Create(@"C:\Synthetic Apps\Tøken Fish");

        Assert.DoesNotContain(@"C:\Users\pixel", setup.ExecutablePath, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/mnt/c/Users/pixel", setup.SettingsSnippet, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tokendock", setup.SettingsSnippet, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@"C:\Users\pixel", setup.WslSettingsSnippet, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/mnt/c/Users/pixel", setup.WslSettingsSnippet, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tokendock", setup.WslSettingsSnippet, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Path/To", setup.SettingsSnippet, StringComparison.Ordinal);
        Assert.DoesNotContain("Path/To", setup.WslSettingsSnippet, StringComparison.Ordinal);
    }
}
