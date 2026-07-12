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
    public void LoginShellArgumentsAreStoredThroughArgumentList()
    {
        var startInfo = CodexAppServerProcessFactory.CreateStartInfo(
            CodexAppServerLaunchCommand.CreateWslLoginShell("Ubuntu-24.04"));

        Assert.Equal(
            [
                "--distribution",
                "Ubuntu-24.04",
                "--exec",
                "bash",
                "-lc",
                "exec codex app-server --stdio"
            ],
            startInfo.ArgumentList);
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

    [Fact]
    public void LoginShellProcessSafetySettingsRemainUnchanged()
    {
        var startInfo = CodexAppServerProcessFactory.CreateStartInfo(
            CodexAppServerLaunchCommand.CreateWslLoginShell());

        Assert.Equal("wsl.exe", startInfo.FileName);
        Assert.False(startInfo.UseShellExecute);
        Assert.True(startInfo.RedirectStandardInput);
        Assert.True(startInfo.RedirectStandardOutput);
        Assert.True(startInfo.RedirectStandardError);
        Assert.True(startInfo.CreateNoWindow);
        Assert.Equal(string.Empty, startInfo.WorkingDirectory);
    }

    private static System.Diagnostics.ProcessStartInfo CreateNativeStartInfo() =>
        CodexAppServerProcessFactory.CreateStartInfo(
            CodexAppServerLaunchCommand.CreateNative("codex.exe"));
}
