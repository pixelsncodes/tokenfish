using TokenFish.Core.Models;

namespace TokenFish.Providers.Codex.Tests;

public sealed class CodexAppServerLaunchCommandFactoryTests
{
    [Fact]
    public void NativeModeMapsToNativeLaunchCommand()
    {
        var command = CreateCommand(CodexRuntimeMode.NativeWindows);

        Assert.Equal("codex.exe", command.Executable);
        Assert.Equal(["app-server", "--stdio"], command.Arguments);
    }

    [Fact]
    public void DirectWslModeWithoutDistributionMapsToDirectWslLaunchCommand()
    {
        var command = CreateCommand(CodexRuntimeMode.Wsl);

        Assert.Equal("wsl.exe", command.Executable);
        Assert.Equal(["--exec", "codex", "app-server", "--stdio"], command.Arguments);
    }

    [Fact]
    public void DirectWslModeWithDistributionMapsToDirectWslLaunchCommand()
    {
        var command = CreateCommand(CodexRuntimeMode.Wsl, "Ubuntu-24.04");

        Assert.Equal("wsl.exe", command.Executable);
        Assert.Equal(
            ["--distribution", "Ubuntu-24.04", "--exec", "codex", "app-server", "--stdio"],
            command.Arguments);
    }

    [Fact]
    public void LoginShellModeWithoutDistributionMapsToLoginShellLaunchCommand()
    {
        var command = CreateCommand(CodexRuntimeMode.WslLoginShell);

        Assert.Equal("wsl.exe", command.Executable);
        Assert.Equal(
            ["--exec", "bash", "-lc", "exec codex app-server --stdio"],
            command.Arguments);
    }

    [Fact]
    public void LoginShellModeWithDistributionMapsToLoginShellLaunchCommand()
    {
        var command = CreateCommand(CodexRuntimeMode.WslLoginShell, "Ubuntu-24.04");

        Assert.Equal("wsl.exe", command.Executable);
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
    public void DistributionNameRemainsSeparateArgument()
    {
        var distributionName = "Ubuntu; codex app-server --listen 127.0.0.1:9999";
        var command = CreateCommand(CodexRuntimeMode.WslLoginShell, distributionName);

        Assert.Equal(distributionName, command.Arguments[1]);
        Assert.Equal("exec codex app-server --stdio", command.Arguments[^1]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void EmptyWslDistributionNameNormalizesToDefaultDistribution(string? distributionName)
    {
        var command = CreateCommand(CodexRuntimeMode.Wsl, distributionName);

        Assert.Equal(["--exec", "codex", "app-server", "--stdio"], command.Arguments);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void EmptyLoginShellDistributionNameNormalizesToDefaultDistribution(
        string? distributionName)
    {
        var command = CreateCommand(CodexRuntimeMode.WslLoginShell, distributionName);

        Assert.Equal(
            ["--exec", "bash", "-lc", "exec codex app-server --stdio"],
            command.Arguments);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void EmptyNativeDistributionNameIsAcceptedAsDefault(string? distributionName)
    {
        var command = CreateCommand(CodexRuntimeMode.NativeWindows, distributionName);

        Assert.Equal("codex.exe", command.Executable);
        Assert.Equal(["app-server", "--stdio"], command.Arguments);
    }

    [Fact]
    public void NativeModeRejectsDistributionName()
    {
        var settings = new AppSettings
        {
            CodexRuntimeMode = CodexRuntimeMode.NativeWindows,
            CodexWslDistributionName = "Ubuntu"
        };

        var exception = Assert.Throws<CodexRuntimeConfigurationException>(() =>
            CodexAppServerLaunchCommandFactory.Create(settings));

        Assert.Equal(
            "Codex WSL distribution cannot be used with native runtime mode.",
            exception.Message);
    }

    [Fact]
    public void UnknownRuntimeModeIsRejected()
    {
        var settings = new AppSettings
        {
            CodexRuntimeMode = (CodexRuntimeMode)999
        };

        var exception = Assert.Throws<CodexRuntimeConfigurationException>(() =>
            CodexAppServerLaunchCommandFactory.Create(settings));

        Assert.Equal("Codex runtime mode is not supported.", exception.Message);
    }

    [Fact]
    public void NullSettingsAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            CodexAppServerLaunchCommandFactory.Create(null!));
    }

    private static CodexAppServerLaunchCommand CreateCommand(
        CodexRuntimeMode runtimeMode,
        string? distributionName = null) =>
        CodexAppServerLaunchCommandFactory.Create(
            new AppSettings
            {
                CodexRuntimeMode = runtimeMode,
                CodexWslDistributionName = distributionName
            });
}
