using Microsoft.UI.Xaml.Controls;
using TokenFish.Core.Models;

namespace TokenFish.App;

public sealed partial class MainPage : Page
{
    public string SummaryText { get; } =
        $"Providers: {new AppSettings().ProviderSelectionMode}; Theme: {new AppSettings().ThemeMode}";

    public MainPage()
    {
        InitializeComponent();
    }
}
