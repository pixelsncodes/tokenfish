namespace TokenFish.Providers.Codex.Tests;

public sealed class CodexAppServerLaunchCommandTests
{
    [Fact]
    public void NativeLaunchUsesRequestedExecutable()
    {
        var command = CodexAppServerLaunchCommand.CreateNative(@"C:\tools\codex.exe");

        Assert.Equal(@"C:\tools\codex.exe", command.Executable);
    }

    [Fact]
    public void NativeArgumentsAreFixed()
    {
        var command = CodexAppServerLaunchCommand.CreateNative("codex.exe");

        Assert.Equal(["app-server", "--stdio"], command.Arguments);
    }

    [Fact]
    public void WslLaunchUsesWslExecutable()
    {
        var command = CodexAppServerLaunchCommand.CreateWsl();

        Assert.Equal("wsl.exe", command.Executable);
    }

    [Fact]
    public void WslArgumentsWithoutDistributionAreFixed()
    {
        var command = CodexAppServerLaunchCommand.CreateWsl();

        Assert.Equal(["--exec", "codex", "app-server", "--stdio"], command.Arguments);
    }

    [Fact]
    public void WslArgumentsWithDistributionAreFixed()
    {
        var command = CodexAppServerLaunchCommand.CreateWsl("Ubuntu-24.04");

        Assert.Equal(
            ["--distribution", "Ubuntu-24.04", "--exec", "codex", "app-server", "--stdio"],
            command.Arguments);
    }

    [Fact]
    public void WslLoginShellLaunchUsesWslExecutable()
    {
        var command = CodexAppServerLaunchCommand.CreateWslLoginShell();

        Assert.Equal("wsl.exe", command.Executable);
    }

    [Fact]
    public void WslLoginShellArgumentsWithoutDistributionAreFixed()
    {
        var command = CodexAppServerLaunchCommand.CreateWslLoginShell();

        Assert.Equal(
            ["--exec", "bash", "-lc", "exec codex app-server --stdio"],
            command.Arguments);
    }

    [Fact]
    public void WslLoginShellArgumentsWithDistributionAreFixed()
    {
        var command = CodexAppServerLaunchCommand.CreateWslLoginShell("Ubuntu-24.04");

        Assert.Equal(
            [
                "--distribution",
                "Ubuntu-24.04",
                "--exec",
                "bash",
                "-lc",
                "exec codex app-server --stdio"
            ],
            command.Arguments);
    }

    [Fact]
    public void WslLoginShellDistributionNameRemainsSeparateArgument()
    {
        var distributionName = "Ubuntu; codex app-server --listen 127.0.0.1:9999";
        var command = CodexAppServerLaunchCommand.CreateWslLoginShell(distributionName);

        Assert.Equal(distributionName, command.Arguments[1]);
        Assert.Equal("exec codex app-server --stdio", command.Arguments[^1]);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void EmptyNativeExecutableIsRejected(string executable)
    {
        Assert.Throws<ArgumentException>(() =>
            CodexAppServerLaunchCommand.CreateNative(executable));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void EmptyWslDistributionIsRejectedWhenSupplied(string distributionName)
    {
        Assert.Throws<ArgumentException>(() =>
            CodexAppServerLaunchCommand.CreateWsl(distributionName));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void EmptyWslLoginShellDistributionIsRejectedWhenSupplied(string distributionName)
    {
        Assert.Throws<ArgumentException>(() =>
            CodexAppServerLaunchCommand.CreateWslLoginShell(distributionName));
    }

    [Fact]
    public void ArbitraryAdditionalArgumentsCannotBeSupplied()
    {
        var nativeCommand = CodexAppServerLaunchCommand.CreateNative("codex.exe");
        var wslCommand = CodexAppServerLaunchCommand.CreateWsl("Ubuntu");
        var wslLoginShellCommand =
            CodexAppServerLaunchCommand.CreateWslLoginShell("Ubuntu");

        Assert.Equal(2, nativeCommand.Arguments.Count);
        Assert.Equal(6, wslCommand.Arguments.Count);
        Assert.Equal(6, wslLoginShellCommand.Arguments.Count);
        Assert.DoesNotContain("--listen", nativeCommand.Arguments);
        Assert.DoesNotContain("--listen", wslCommand.Arguments);
        Assert.DoesNotContain("--listen", wslLoginShellCommand.Arguments);
    }
}
