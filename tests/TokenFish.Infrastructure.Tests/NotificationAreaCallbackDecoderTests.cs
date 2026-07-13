using TokenFish.Infrastructure;

namespace TokenFish.Infrastructure.Tests;

public sealed class NotificationAreaCallbackDecoderTests
{
    private const uint IconId = 1;

    [Fact]
    public void Version4NinSelectMapsToPrimaryActivation()
    {
        var action = NotificationAreaCallbackDecoder.DecodeVersion4(
            0,
            Version4(NotificationAreaCallbackDecoder.NinSelect),
            IconId);

        Assert.Equal(NotificationAreaCallbackAction.PrimaryActivate, action);
    }

    [Fact]
    public void Version4NinKeySelectMapsToPrimaryActivation()
    {
        var action = NotificationAreaCallbackDecoder.DecodeVersion4(
            0,
            Version4(NotificationAreaCallbackDecoder.NinKeySelect),
            IconId);

        Assert.Equal(NotificationAreaCallbackAction.PrimaryActivate, action);
    }

    [Fact]
    public void Version4ContextMenuMapsToContextMenu()
    {
        var action = NotificationAreaCallbackDecoder.DecodeVersion4(
            0,
            Version4(NotificationAreaCallbackDecoder.WmContextMenu),
            IconId);

        Assert.Equal(NotificationAreaCallbackAction.ContextMenu, action);
    }

    [Theory]
    [InlineData(NotificationAreaCallbackDecoder.WmLButtonUp)]
    [InlineData(NotificationAreaCallbackDecoder.WmRButtonUp)]
    public void Version4CompatibleMouseNotificationsAreIgnored(uint callback)
    {
        var action = NotificationAreaCallbackDecoder.DecodeVersion4(0, Version4(callback), IconId);

        Assert.Equal(NotificationAreaCallbackAction.None, action);
    }

    [Fact]
    public void UnknownVersion4NotificationIsIgnored()
    {
        var action = NotificationAreaCallbackDecoder.DecodeVersion4(0, Version4(0x9999), IconId);

        Assert.Equal(NotificationAreaCallbackAction.None, action);
    }

    [Fact]
    public void Version4NotificationForDifferentIconIsIgnored()
    {
        var action = NotificationAreaCallbackDecoder.DecodeVersion4(
            0,
            Version4(NotificationAreaCallbackDecoder.NinSelect, iconId: 2),
            IconId);

        Assert.Equal(NotificationAreaCallbackAction.None, action);
    }

    [Theory]
    [InlineData(NotificationAreaCallbackDecoder.WmLButtonUp, NotificationAreaCallbackAction.PrimaryActivate)]
    [InlineData(NotificationAreaCallbackDecoder.WmRButtonUp, NotificationAreaCallbackAction.ContextMenu)]
    public void LegacyMouseNotificationsMapCorrectly(uint callback, NotificationAreaCallbackAction expectedAction)
    {
        var action = NotificationAreaCallbackDecoder.DecodeLegacy((nint)IconId, (nint)callback, IconId);

        Assert.Equal(expectedAction, action);
    }

    [Fact]
    public void LegacyNotificationForDifferentIconIsIgnored()
    {
        var action = NotificationAreaCallbackDecoder.DecodeLegacy(
            2,
            (nint)NotificationAreaCallbackDecoder.WmLButtonUp,
            IconId);

        Assert.Equal(NotificationAreaCallbackAction.None, action);
    }

    [Fact]
    public void IconPathResolvesFromApplicationBaseDirectory()
    {
        var baseDirectory = Path.Combine("TokenFish", "out");
        var path = NotificationAreaIconPath.Resolve(baseDirectory);

        Assert.Equal(Path.Combine(baseDirectory, "Assets", "TrayIcon.ico"), path);
    }

    [Fact]
    public void IconPathResolutionDoesNotRequireIconFileToExist()
    {
        var baseDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        var path = NotificationAreaIconPath.Resolve(baseDirectory);

        Assert.Equal(Path.Combine(baseDirectory, "Assets", "TrayIcon.ico"), path);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void TrayAndApplicationIconPathsAreDistinct()
    {
        var baseDirectory = Path.Combine("TokenFish", "publish");

        var trayIconPath = NotificationAreaIconPath.Resolve(baseDirectory);
        var applicationIconPath = ApplicationIconPath.ResolveWindowIcon(baseDirectory);

        Assert.NotEqual(applicationIconPath, trayIconPath);
        Assert.EndsWith(Path.Combine("Assets", "TrayIcon.ico"), trayIconPath);
        Assert.EndsWith(Path.Combine("Assets", "AppIcon.ico"), applicationIconPath);
    }

    private static nint Version4(uint callback, uint iconId = IconId) =>
        (nint)(((iconId & 0xFFFF) << 16) | (callback & 0xFFFF));
}
