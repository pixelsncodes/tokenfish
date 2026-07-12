using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using TokenFish.Infrastructure;

namespace TokenFish.App;

public sealed partial class MainWindow : Window
{
    private bool _allowClose;

    public MainWindow()
    {
        InitializeComponent();

        InitializeIconSurfaces();
        AppWindow.Closing += OnAppWindowClosing;
        Activated += OnActivated;
        Content.KeyDown += OnKeyDown;
    }

    public event Action? PopupDeactivated;

    public event Action? PopupActivated;

    public event Action? PopupCloseRequested;

    public void AllowClose() => _allowClose = true;

    private void InitializeIconSurfaces()
    {
        TrySetWindowIcon();
        TrySetHeaderIcon();
    }

    private void TrySetWindowIcon()
    {
        if (!ApplicationIconPath.TryResolveExistingWindowIcon(
            AppContext.BaseDirectory,
            out var iconPath))
        {
            return;
        }

        try
        {
            AppWindow.SetIcon(iconPath);
        }
        catch
        {
        }
    }

    private void TrySetHeaderIcon()
    {
        HeaderIcon.ImageOpened += OnHeaderIconImageOpened;
        HeaderIcon.ImageFailed += OnHeaderIconImageFailed;

        if (!ApplicationIconPath.TryResolveExistingHeaderIcon(
            AppContext.BaseDirectory,
            out var iconPath))
        {
            HeaderIcon.Source = null;
            HeaderIcon.Visibility = Visibility.Collapsed;
            return;
        }

        try
        {
            HeaderIcon.Source = new BitmapImage(new Uri(iconPath));
        }
        catch
        {
            HeaderIcon.Source = null;
            HeaderIcon.Visibility = Visibility.Collapsed;
        }
    }

    private void OnHeaderIconImageOpened(object sender, RoutedEventArgs args)
    {
        HeaderIcon.Visibility = Visibility.Visible;
    }

    private void OnHeaderIconImageFailed(object sender, ExceptionRoutedEventArgs args)
    {
        HeaderIcon.Source = null;
        HeaderIcon.Visibility = Visibility.Collapsed;
    }

    public void UpdateState(TrayPopupDisplayState state)
    {
        StatusText.Text = state.StatusText;
        StatusBannerText.Text = state.StatusText;
        StatusBanner.Visibility = state.ApplicationState is
            PopupApplicationDisplayState.StartupIssue or
            PopupApplicationDisplayState.RefreshIssue or
            PopupApplicationDisplayState.ShutdownIssue or
            PopupApplicationDisplayState.ShellIssue
                ? Visibility.Visible
                : Visibility.Collapsed;

        ProvidersPanel.Children.Clear();
        foreach (var provider in state.Providers)
        {
            ProvidersPanel.Children.Add(CreateProviderCard(provider));
        }
    }

    private static UIElement CreateProviderCard(ProviderCardDisplayState provider)
    {
        var card = new Border
        {
            Padding = new Thickness(12),
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[
                "CardStrokeColorDefaultBrush"],
            Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[
                "CardBackgroundFillColorDefaultBrush"]
        };

        var stack = new StackPanel { Spacing = 8 };
        card.Child = stack;

        stack.Children.Add(CreateProviderHeader(provider));

        foreach (var quotaWindow in provider.QuotaWindows)
        {
            stack.Children.Add(CreateQuotaWindowSection(quotaWindow));
        }

        if (provider.ActivityRows.Count > 0)
        {
            stack.Children.Add(CreateActivitySection(provider.ActivityRows));
        }

        if (provider.EmptyUsageMessage is not null)
        {
            stack.Children.Add(new TextBlock
            {
                Text = provider.EmptyUsageMessage,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[
                    "TextFillColorSecondaryBrush"],
                TextWrapping = TextWrapping.WrapWholeWords
            });
        }

        if (!string.IsNullOrWhiteSpace(provider.FooterText))
        {
            stack.Children.Add(CreateFooter(provider.FooterText));
        }

        return card;
    }

    private static UIElement CreateProviderHeader(ProviderCardDisplayState provider)
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        grid.Children.Add(new TextBlock
        {
            Text = provider.ProviderName,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 15,
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        var connection = new TextBlock
        {
            Text = provider.ConnectionState,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[
                "TextFillColorSecondaryBrush"],
            TextAlignment = TextAlignment.Right,
            TextWrapping = TextWrapping.NoWrap
        };
        AutomationProperties.SetName(connection, $"Connection {provider.ConnectionState}");
        Grid.SetColumn(connection, 1);
        grid.Children.Add(connection);

        return grid;
    }

    private static UIElement CreateQuotaWindowSection(PopupQuotaWindowDisplayState quotaWindow)
    {
        var stack = new StackPanel { Spacing = 5 };

        stack.Children.Add(new TextBlock
        {
            Text = quotaWindow.Label,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.WrapWholeWords
        });

        stack.Children.Add(new TextBlock
        {
            Text = quotaWindow.PercentageText,
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.NoWrap
        });

        var progress = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = (double)quotaWindow.ProgressValue,
            Height = 5
        };
        AutomationProperties.SetName(progress, quotaWindow.ProgressAutomationName);
        stack.Children.Add(progress);

        if (quotaWindow.RelativeResetText is not null)
        {
            stack.Children.Add(new TextBlock
            {
                Text = quotaWindow.RelativeResetText,
                Margin = new Thickness(0, 2, 0, 0),
                TextWrapping = TextWrapping.WrapWholeWords
            });
        }

        if (quotaWindow.ExactResetText is not null)
        {
            stack.Children.Add(new TextBlock
            {
                Text = quotaWindow.ExactResetText,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[
                    "TextFillColorSecondaryBrush"],
                TextWrapping = TextWrapping.WrapWholeWords
            });
        }

        return stack;
    }

    private static UIElement CreateActivitySection(IReadOnlyList<PopupActivityDisplayState> activityRows)
    {
        var stack = new StackPanel { Spacing = 6 };
        stack.Children.Add(new TextBlock
        {
            Text = "Additional activity",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 4, 0, 0)
        });

        foreach (var activity in activityRows)
        {
            stack.Children.Add(CreateActivityRow(activity));
        }

        return stack;
    }

    private static UIElement CreateActivityRow(PopupActivityDisplayState activity)
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        AutomationProperties.SetName(grid, activity.AutomationName);

        var labelText = activity.IntervalText is null
            ? activity.Label
            : $"{activity.Label} · {activity.IntervalText}";
        grid.Children.Add(new TextBlock
        {
            Text = labelText,
            TextWrapping = TextWrapping.WrapWholeWords
        });

        var valueBlock = new TextBlock
        {
            Text = activity.ValueText,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextAlignment = TextAlignment.Right,
            TextWrapping = TextWrapping.NoWrap
        };
        Grid.SetColumn(valueBlock, 1);
        grid.Children.Add(valueBlock);

        return grid;
    }

    private static UIElement CreateFooter(string footerText) =>
        new TextBlock
        {
            Text = footerText,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[
                "TextFillColorSecondaryBrush"],
            FontSize = 12,
            Margin = new Thickness(0, 4, 0, 0),
            TextWrapping = TextWrapping.WrapWholeWords
        };

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            PopupDeactivated?.Invoke();
            return;
        }

        PopupActivated?.Invoke();
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key == Windows.System.VirtualKey.Escape)
        {
            args.Handled = true;
            PopupCloseRequested?.Invoke();
        }
    }

    private void OnAppWindowClosing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
    {
        if (_allowClose)
        {
            return;
        }

        args.Cancel = true;
        PopupCloseRequested?.Invoke();
    }
}
