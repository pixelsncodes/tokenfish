using TokenFish.Infrastructure;

namespace TokenFish.Infrastructure.Tests;

public sealed class PopupWindowPresenterConfigurationTests
{
    [Fact]
    public void TrayPopupRequestsBorderlessTitlebarlessNonResizableChrome()
    {
        var configuration = PopupWindowPresenterConfiguration.TrayPopup;

        Assert.False(configuration.HasBorder);
        Assert.False(configuration.HasTitleBar);
        Assert.False(configuration.IsResizable);
        Assert.False(configuration.IsMaximizable);
        Assert.False(configuration.IsMinimizable);
    }
}
