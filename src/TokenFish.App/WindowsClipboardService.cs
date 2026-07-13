using Windows.ApplicationModel.DataTransfer;

namespace TokenFish.App;

internal sealed class WindowsClipboardService : IClipboardService
{
    public void SetText(string text)
    {
        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);
    }
}
