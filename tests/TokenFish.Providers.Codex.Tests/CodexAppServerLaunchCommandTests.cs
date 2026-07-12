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

    [Fact]
    public void ArbitraryAdditionalArgumentsCannotBeSupplied()
    {
        var nativeCommand = CodexAppServerLaunchCommand.CreateNative("codex.exe");
        var wslCommand = CodexAppServerLaunchCommand.CreateWsl("Ubuntu");

        Assert.Equal(2, nativeCommand.Arguments.Count);
        Assert.Equal(6, wslCommand.Arguments.Count);
        Assert.DoesNotContain("--listen", nativeCommand.Arguments);
        Assert.DoesNotContain("--listen", wslCommand.Arguments);
    }
}
