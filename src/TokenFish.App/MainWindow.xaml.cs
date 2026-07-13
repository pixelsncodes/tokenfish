using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using TokenFish.Infrastructure;

namespace TokenFish.App;

public sealed partial class MainWindow : Window
{
    private readonly PopupDisplayStateUpdatePlanner _updatePlanner = new();
    private readonly List<ProviderCardView> _providerCards = [];
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

    public event Action? ContentSizeInvalidated;

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
        ContentSizeInvalidated?.Invoke();
    }

    private void OnHeaderIconImageFailed(object sender, ExceptionRoutedEventArgs args)
    {
        HeaderIcon.Source = null;
        HeaderIcon.Visibility = Visibility.Collapsed;
    }

    public void UpdateState(TrayPopupDisplayState state)
    {
        var plan = _updatePlanner.Plan(state);
        SetTextIfChanged(StatusText, state.StatusText);
        SetTextIfChanged(StatusBannerText, state.StatusText);
        var statusBannerVisibility = state.ApplicationState is
            PopupApplicationDisplayState.StartupIssue or
            PopupApplicationDisplayState.RefreshIssue or
            PopupApplicationDisplayState.ShutdownIssue or
            PopupApplicationDisplayState.ShellIssue
                ? Visibility.Visible
                : Visibility.Collapsed;
        SetVisibilityIfChanged(StatusBanner, statusBannerVisibility);

        if (plan.RebuildProviderCards || _providerCards.Count != state.Providers.Count)
        {
            RebuildProviderCards(state.Providers);
        }
        else
        {
            for (var providerIndex = 0; providerIndex < state.Providers.Count; providerIndex++)
            {
                _providerCards[providerIndex].Update(state.Providers[providerIndex]);
            }
        }

        if (plan.AffectsLayout)
        {
            ContentSizeInvalidated?.Invoke();
        }
    }

    public double MeasurePreferredHeightEffectivePixels()
    {
        RootGrid.Measure(new Windows.Foundation.Size(
            PopupWindowLayoutCalculator.WidthEffectivePixels,
            double.PositiveInfinity));

        return Math.Ceiling(RootGrid.DesiredSize.Height);
    }

    private void RebuildProviderCards(IReadOnlyList<ProviderCardDisplayState> providers)
    {
        ProvidersPanel.Children.Clear();
        _providerCards.Clear();
        foreach (var provider in providers)
        {
            var providerView = CreateProviderCard(provider);
            _providerCards.Add(providerView);
            ProvidersPanel.Children.Add(providerView.Root);
        }
    }

    private static ProviderCardView CreateProviderCard(ProviderCardDisplayState provider)
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

        var header = CreateProviderHeader(provider);
        stack.Children.Add(header.Root);

        var quotaViews = new List<QuotaWindowView>(provider.QuotaWindows.Count);
        foreach (var quotaWindow in provider.QuotaWindows)
        {
            var quotaView = CreateQuotaWindowSection(quotaWindow);
            quotaViews.Add(quotaView);
            stack.Children.Add(quotaView.Root);
        }

        UIElement? activitySection = null;
        IReadOnlyList<ActivityRowView> activityViews = [];
        if (provider.ActivityRows.Count > 0)
        {
            var activityView = CreateActivitySection(provider.ActivityRows);
            activitySection = activityView.Root;
            activityViews = activityView.ActivityRows;
            stack.Children.Add(activitySection);
        }

        TextBlock? emptyUsageMessage = null;
        if (provider.EmptyUsageMessage is not null)
        {
            emptyUsageMessage = new TextBlock
            {
                Text = provider.EmptyUsageMessage,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[
                    "TextFillColorSecondaryBrush"],
                TextWrapping = TextWrapping.WrapWholeWords
            };
            stack.Children.Add(emptyUsageMessage);
        }

        TextBlock? footer = null;
        if (!string.IsNullOrWhiteSpace(provider.FooterText))
        {
            footer = CreateFooter(provider.FooterText);
            stack.Children.Add(footer);
        }

        return new ProviderCardView(
            card,
            provider,
            header,
            quotaViews,
            activityViews,
            emptyUsageMessage,
            footer);
    }

    private static ProviderHeaderView CreateProviderHeader(ProviderCardDisplayState provider)
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var name = new TextBlock
        {
            Text = provider.ProviderName,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 15,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        grid.Children.Add(name);

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

        return new ProviderHeaderView(grid, name, connection);
    }

    private static QuotaWindowView CreateQuotaWindowSection(PopupQuotaWindowDisplayState quotaWindow)
    {
        var stack = new StackPanel { Spacing = 5 };

        var label = new TextBlock
        {
            Text = quotaWindow.Label,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.WrapWholeWords
        };
        stack.Children.Add(label);

        var percentageText = new TextBlock
        {
            Text = quotaWindow.PercentageText,
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.NoWrap
        };
        stack.Children.Add(percentageText);

        var progress = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            IsIndeterminate = false,
            Value = (double)quotaWindow.ProgressValue,
            Height = 5
        };
        AutomationProperties.SetName(progress, quotaWindow.ProgressAutomationName);
        stack.Children.Add(progress);

        TextBlock? relativeReset = null;
        if (quotaWindow.RelativeResetText is not null)
        {
            relativeReset = new TextBlock
            {
                Text = quotaWindow.RelativeResetText,
                Margin = new Thickness(0, 2, 0, 0),
                TextWrapping = TextWrapping.WrapWholeWords
            };
            stack.Children.Add(relativeReset);
        }

        TextBlock? exactReset = null;
        if (quotaWindow.ExactResetText is not null)
        {
            exactReset = new TextBlock
            {
                Text = quotaWindow.ExactResetText,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[
                    "TextFillColorSecondaryBrush"],
                TextWrapping = TextWrapping.WrapWholeWords
            };
            stack.Children.Add(exactReset);
        }

        return new QuotaWindowView(
            stack,
            quotaWindow,
            label,
            percentageText,
            progress,
            relativeReset,
            exactReset);
    }

    private static ActivitySectionView CreateActivitySection(IReadOnlyList<PopupActivityDisplayState> activityRows)
    {
        var stack = new StackPanel { Spacing = 6 };
        stack.Children.Add(new TextBlock
        {
            Text = "Additional activity",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 4, 0, 0)
        });

        var rows = new List<ActivityRowView>(activityRows.Count);
        foreach (var activity in activityRows)
        {
            var row = CreateActivityRow(activity);
            rows.Add(row);
            stack.Children.Add(row.Root);
        }

        return new ActivitySectionView(stack, rows);
    }

    private static ActivityRowView CreateActivityRow(PopupActivityDisplayState activity)
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        AutomationProperties.SetName(grid, activity.AutomationName);

        var labelText = activity.IntervalText is null
            ? activity.Label
            : $"{activity.Label} · {activity.IntervalText}";
        var label = new TextBlock
        {
            Text = labelText,
            TextWrapping = TextWrapping.WrapWholeWords
        };
        grid.Children.Add(label);

        var valueBlock = new TextBlock
        {
            Text = activity.ValueText,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextAlignment = TextAlignment.Right,
            TextWrapping = TextWrapping.NoWrap
        };
        Grid.SetColumn(valueBlock, 1);
        grid.Children.Add(valueBlock);

        return new ActivityRowView(grid, activity, label, valueBlock);
    }

    private static TextBlock CreateFooter(string footerText) =>
        new TextBlock
        {
            Text = footerText,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[
                "TextFillColorSecondaryBrush"],
            FontSize = 12,
            Margin = new Thickness(0, 4, 0, 0),
            TextWrapping = TextWrapping.WrapWholeWords
        };

    private static void SetTextIfChanged(TextBlock textBlock, string? text)
    {
        text ??= string.Empty;
        if (textBlock.Text != text)
        {
            textBlock.Text = text;
        }
    }

    private static void SetVisibilityIfChanged(UIElement element, Visibility visibility)
    {
        if (element.Visibility != visibility)
        {
            element.Visibility = visibility;
        }
    }

    private sealed class ProviderCardView
    {
        public ProviderCardView(
            Border root,
            ProviderCardDisplayState state,
            ProviderHeaderView header,
            IReadOnlyList<QuotaWindowView> quotaWindows,
            IReadOnlyList<ActivityRowView> activityRows,
            TextBlock? emptyUsageMessage,
            TextBlock? footer)
        {
            Root = root;
            State = state;
            Header = header;
            QuotaWindows = quotaWindows;
            ActivityRows = activityRows;
            EmptyUsageMessage = emptyUsageMessage;
            Footer = footer;
        }

        public Border Root { get; }

        private ProviderCardDisplayState State { get; set; }

        private ProviderHeaderView Header { get; }

        private IReadOnlyList<QuotaWindowView> QuotaWindows { get; }

        private IReadOnlyList<ActivityRowView> ActivityRows { get; }

        private TextBlock? EmptyUsageMessage { get; }

        private TextBlock? Footer { get; }

        public void Update(ProviderCardDisplayState state)
        {
            Header.Update(state);

            for (var quotaIndex = 0; quotaIndex < state.QuotaWindows.Count; quotaIndex++)
            {
                QuotaWindows[quotaIndex].Update(state.QuotaWindows[quotaIndex]);
            }

            for (var activityIndex = 0; activityIndex < state.ActivityRows.Count; activityIndex++)
            {
                ActivityRows[activityIndex].Update(state.ActivityRows[activityIndex]);
            }

            if (EmptyUsageMessage is not null)
            {
                SetTextIfChanged(EmptyUsageMessage, state.EmptyUsageMessage);
            }

            if (Footer is not null)
            {
                SetTextIfChanged(Footer, state.FooterText);
            }

            State = state;
        }
    }

    private sealed class ProviderHeaderView
    {
        public ProviderHeaderView(Grid root, TextBlock name, TextBlock connection)
        {
            Root = root;
            Name = name;
            Connection = connection;
        }

        public Grid Root { get; }

        private TextBlock Name { get; }

        private TextBlock Connection { get; }

        public void Update(ProviderCardDisplayState state)
        {
            SetTextIfChanged(Name, state.ProviderName);
            SetTextIfChanged(Connection, state.ConnectionState);
            AutomationProperties.SetName(Connection, $"Connection {state.ConnectionState}");
        }
    }

    private sealed class QuotaWindowView
    {
        public QuotaWindowView(
            StackPanel root,
            PopupQuotaWindowDisplayState state,
            TextBlock label,
            TextBlock percentageText,
            ProgressBar progress,
            TextBlock? relativeReset,
            TextBlock? exactReset)
        {
            Root = root;
            State = state;
            Label = label;
            PercentageText = percentageText;
            Progress = progress;
            RelativeReset = relativeReset;
            ExactReset = exactReset;
        }

        public StackPanel Root { get; }

        private PopupQuotaWindowDisplayState State { get; set; }

        private TextBlock Label { get; }

        private TextBlock PercentageText { get; }

        private ProgressBar Progress { get; }

        private TextBlock? RelativeReset { get; }

        private TextBlock? ExactReset { get; }

        public void Update(PopupQuotaWindowDisplayState state)
        {
            SetTextIfChanged(Label, state.Label);
            SetTextIfChanged(PercentageText, state.PercentageText);
            if ((decimal)Progress.Value != state.ProgressValue)
            {
                Progress.Value = (double)state.ProgressValue;
            }

            if (State.ProgressAutomationName != state.ProgressAutomationName)
            {
                AutomationProperties.SetName(Progress, state.ProgressAutomationName);
            }

            if (RelativeReset is not null)
            {
                SetTextIfChanged(RelativeReset, state.RelativeResetText);
            }

            if (ExactReset is not null)
            {
                SetTextIfChanged(ExactReset, state.ExactResetText);
            }

            State = state;
        }
    }

    private sealed record ActivitySectionView(
        StackPanel Root,
        IReadOnlyList<ActivityRowView> ActivityRows);

    private sealed class ActivityRowView
    {
        public ActivityRowView(
            Grid root,
            PopupActivityDisplayState state,
            TextBlock label,
            TextBlock value)
        {
            Root = root;
            State = state;
            Label = label;
            Value = value;
        }

        public Grid Root { get; }

        private PopupActivityDisplayState State { get; set; }

        private TextBlock Label { get; }

        private TextBlock Value { get; }

        public void Update(PopupActivityDisplayState state)
        {
            var labelText = state.IntervalText is null
                ? state.Label
                : $"{state.Label} · {state.IntervalText}";
            SetTextIfChanged(Label, labelText);
            SetTextIfChanged(Value, state.ValueText);
            if (State.AutomationName != state.AutomationName)
            {
                AutomationProperties.SetName(Root, state.AutomationName);
            }

            State = state;
        }
    }

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
