namespace TokenFish.Providers.Codex.Tests;

public sealed class CodexAppServerProcessFactoryTests
{
    [Fact]
    public void ShellExecutionIsDisabled()
    {
        var startInfo = CreateNativeStartInfo();

        Assert.False(startInfo.UseShellExecute);
    }

    [Fact]
    public void StandardStreamsAreRedirected()
    {
        var startInfo = CreateNativeStartInfo();

        Assert.True(startInfo.RedirectStandardInput);
        Assert.True(startInfo.RedirectStandardOutput);
        Assert.True(startInfo.RedirectStandardError);
    }

    [Fact]
    public void NoWindowIsCreated()
    {
        var startInfo = CreateNativeStartInfo();

        Assert.True(startInfo.CreateNoWindow);
    }

    [Fact]
    public void ArgumentsAreStoredThroughArgumentList()
    {
        var startInfo = CreateNativeStartInfo();

        Assert.Equal(["app-server", "--stdio"], startInfo.ArgumentList);
        Assert.Equal(string.Empty, startInfo.Arguments);
    }

    [Fact]
    public void NoWorkingDirectoryIsAssigned()
    {
        var startInfo = CreateNativeStartInfo();

        Assert.Equal(string.Empty, startInfo.WorkingDirectory);
    }

    [Fact]
    public void NoShellExecutableIsIntroduced()
    {
        var nativeStartInfo = CreateNativeStartInfo();
        var wslStartInfo = CodexAppServerProcessFactory.CreateStartInfo(
            CodexAppServerLaunchCommand.CreateWsl());

        Assert.Equal("codex.exe", nativeStartInfo.FileName);
        Assert.Equal("wsl.exe", wslStartInfo.FileName);
        Assert.DoesNotContain("cmd", nativeStartInfo.FileName, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("powershell", nativeStartInfo.FileName, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bash", nativeStartInfo.FileName, StringComparison.OrdinalIgnoreCase);
    }

    private static System.Diagnostics.ProcessStartInfo CreateNativeStartInfo() =>
        CodexAppServerProcessFactory.CreateStartInfo(
            CodexAppServerLaunchCommand.CreateNative("codex.exe"));
}
