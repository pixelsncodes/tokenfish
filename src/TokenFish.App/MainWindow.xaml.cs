using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using TokenFish.Infrastructure;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace TokenFish.App;

public sealed partial class MainWindow : Window
{
    private const double PopupRailWidth = 300;
    private const double FishWidth = 25;
    private static readonly Brush PopupTextPrimaryBrush = GetPopupBrush("PopupTextPrimaryBrush");
    private static readonly Brush PopupTextSecondaryBrush = GetPopupBrush("PopupTextSecondaryBrush");
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

    public event Action? RefreshRequested;

    public event Action? SettingsRequested;

    public void AllowClose() => _allowClose = true;

    private void InitializeIconSurfaces()
    {
        TrySetWindowIcon();
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

    public void UpdateState(TrayPopupDisplayState state)
    {
        var plan = _updatePlanner.Plan(state);
        SetTextIfChanged(StatusText, state.StatusText);
        SetTextIfChanged(StatusBannerText, state.StatusText);
        SetRefreshCommandState(state.RefreshCommandState);
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
        var accent = GetProviderAccent(provider.Provider);
        var card = new Border
        {
            Padding = new Thickness(14, 12, 14, 12),
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(1),
            BorderBrush = CreateBrush(0xFF, 0x29, 0x38, 0x4A),
            Background = CreateBrush(0xFF, 0x12, 0x1B, 0x26)
        };

        var stack = new StackPanel { Spacing = 8 };
        card.Child = stack;

        var header = CreateProviderHeader(provider, accent);
        stack.Children.Add(header.Root);

        var quotaViews = new List<QuotaWindowView>(provider.QuotaWindows.Count);
        foreach (var quotaWindow in provider.QuotaWindows)
        {
            var quotaView = CreateQuotaWindowSection(quotaWindow, accent);
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
                Foreground = PopupTextSecondaryBrush,
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

    private static ProviderHeaderView CreateProviderHeader(ProviderCardDisplayState provider, Brush accent)
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var nameRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7 };
        nameRow.Children.Add(new Border
        {
            Width = 7,
            Height = 7,
            CornerRadius = new CornerRadius(4),
            Background = accent,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false
        });
        var name = new TextBlock
        {
            Text = provider.ProviderName,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 15,
            Foreground = PopupTextPrimaryBrush,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        AutomationProperties.SetHeadingLevel(
            name,
            Microsoft.UI.Xaml.Automation.Peers.AutomationHeadingLevel.Level2);
        nameRow.Children.Add(name);
        grid.Children.Add(nameRow);

        var connection = new TextBlock
        {
            Text = provider.ConnectionState,
            Foreground = PopupTextSecondaryBrush,
            TextAlignment = TextAlignment.Right,
            TextWrapping = TextWrapping.NoWrap
        };
        AutomationProperties.SetName(connection, $"Connection {provider.ConnectionState}");
        Grid.SetColumn(connection, 1);
        grid.Children.Add(connection);

        return new ProviderHeaderView(grid, name, connection);
    }

    private static QuotaWindowView CreateQuotaWindowSection(
        PopupQuotaWindowDisplayState quotaWindow,
        Brush accent)
    {
        var stack = new StackPanel { Spacing = 5 };

        var header = new Grid { ColumnSpacing = 10 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var label = new TextBlock
        {
            Text = quotaWindow.Label,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = PopupTextPrimaryBrush,
            TextWrapping = TextWrapping.WrapWholeWords
        };
        header.Children.Add(label);

        var percentageText = new TextBlock
        {
            Text = quotaWindow.PercentageText,
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = accent,
            TextWrapping = TextWrapping.NoWrap
        };
        Grid.SetColumn(percentageText, 1);
        header.Children.Add(percentageText);
        stack.Children.Add(header);

        var rail = CreateQuotaRail(quotaWindow, accent);
        stack.Children.Add(rail.Root);

        TextBlock? relativeReset = null;
        if (quotaWindow.RelativeResetText is not null)
        {
            relativeReset = new TextBlock
            {
                Text = quotaWindow.RelativeResetText,
                Margin = new Thickness(0, 2, 0, 0),
                Foreground = PopupTextPrimaryBrush,
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
                Foreground = PopupTextSecondaryBrush,
                TextWrapping = TextWrapping.WrapWholeWords
            };
            stack.Children.Add(exactReset);
        }

        return new QuotaWindowView(
            stack,
            quotaWindow,
            label,
            percentageText,
            rail,
            relativeReset,
            exactReset);
    }

    private static QuotaRailView CreateQuotaRail(PopupQuotaWindowDisplayState quotaWindow, Brush accent)
    {
        const double railWidth = PopupRailWidth;
        const double fishWidth = FishWidth;
        var canvas = new Canvas
        {
            Width = railWidth,
            Height = 22,
            IsHitTestVisible = false
        };
        AutomationProperties.SetAccessibilityView(canvas, AccessibilityView.Raw);
        canvas.Children.Add(new Border
        {
            Width = railWidth,
            Height = 4,
            CornerRadius = new CornerRadius(2),
            Background = CreateBrush(0xFF, 0x29, 0x38, 0x4A),
            Margin = new Thickness(0, 9, 0, 0)
        });

        for (var index = 0; index < 12; index++)
        {
            var pellet = new Ellipse
            {
                Width = 3,
                Height = 3,
                Fill = CreateBrush(0xFF, 0x79, 0x8A, 0x9E),
                Opacity = 0.8
            };
            Canvas.SetLeft(pellet, 12 + (index * 23));
            Canvas.SetTop(pellet, 9.5);
            AutomationProperties.SetAccessibilityView(pellet, AccessibilityView.Raw);
            canvas.Children.Add(pellet);
        }

        var fish = new Canvas { Width = fishWidth, Height = 25 };
        AutomationProperties.SetAccessibilityView(fish, AccessibilityView.Raw);
        var tail = new Polygon
        {
            Points = CreateFishBodyPoints(isOpenMouth: true),
            Fill = accent
        };
        Canvas.SetLeft(tail, 1);
        Canvas.SetTop(tail, 1);
        AutomationProperties.SetAccessibilityView(tail, AccessibilityView.Raw);
        fish.Children.Add(tail);
        var fin = new Rectangle
        {
            Width = 8,
            Height = 8,
            Fill = accent,
            Opacity = 0.8,
            RenderTransform = new RotateTransform { Angle = 45, CenterX = 4, CenterY = 4 }
        };
        Canvas.SetLeft(fin, -3);
        Canvas.SetTop(fin, 9);
        AutomationProperties.SetAccessibilityView(fin, AccessibilityView.Raw);
        fish.Children.Add(fin);
        var eye = new Rectangle
        {
            Width = 4,
            Height = 4,
            Fill = CreateBrush(0xFF, 0x07, 0x10, 0x16)
        };
        Canvas.SetLeft(eye, 10);
        Canvas.SetTop(eye, 6);
        AutomationProperties.SetAccessibilityView(eye, AccessibilityView.Raw);
        fish.Children.Add(eye);
        canvas.Children.Add(fish);
        Canvas.SetTop(fish, -1.5);
        StartFishMouthAnimation(tail);

        var view = new QuotaRailView(canvas, fish, railWidth, fishWidth);
        view.Update(quotaWindow);
        return view;
    }

    private static Brush GetProviderAccent(TokenFish.Core.Models.ProviderKind provider) =>
        provider == TokenFish.Core.Models.ProviderKind.Claude
            ? CreateBrush(0xFF, 0xFF, 0x88, 0x5A)
            : CreateBrush(0xFF, 0x52, 0xD6, 0xD0);

    private static Brush CreateBrush(byte alpha, byte red, byte green, byte blue) =>
        new SolidColorBrush(Color.FromArgb(alpha, red, green, blue));

    private static Brush GetPopupBrush(string resourceKey) =>
        (Brush)Application.Current.Resources[resourceKey];

    private static PointCollection CreateFishBodyPoints(bool isOpenMouth) => new()
    {
        new Windows.Foundation.Point(0, 0),
        new Windows.Foundation.Point(17.94, 0),
        new Windows.Foundation.Point(23, isOpenMouth ? 7.82 : 10.12),
        new Windows.Foundation.Point(isOpenMouth ? 16.79 : 18.86, 11.5),
        new Windows.Foundation.Point(23, isOpenMouth ? 15.18 : 12.88),
        new Windows.Foundation.Point(17.94, 23),
        new Windows.Foundation.Point(0, 23),
        new Windows.Foundation.Point(2.76, 14.26),
        new Windows.Foundation.Point(0, 11.5),
        new Windows.Foundation.Point(2.76, 8.74)
    };

    private static void StartFishMouthAnimation(Polygon body)
    {
        if (!FishAnimationSettings.ShouldAnimate(SystemAnimationsEnabled()))
        {
            return;
        }

        var animation = new ObjectAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromMilliseconds(340),
            RepeatBehavior = RepeatBehavior.Forever,
            EnableDependentAnimation = true
        };
        animation.KeyFrames.Add(new DiscreteObjectKeyFrame
        {
            KeyTime = KeyTime.FromTimeSpan(TimeSpan.Zero),
            Value = CreateFishBodyPoints(isOpenMouth: true)
        });
        animation.KeyFrames.Add(new DiscreteObjectKeyFrame
        {
            KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(170)),
            Value = CreateFishBodyPoints(isOpenMouth: false)
        });
        var storyboard = new Storyboard();
        Storyboard.SetTarget(animation, body);
        Storyboard.SetTargetProperty(animation, "Points");
        storyboard.Children.Add(animation);
        storyboard.Begin();
    }

    private static bool SystemAnimationsEnabled()
    {
        try
        {
            return new UISettings().AnimationsEnabled;
        }
        catch
        {
            return false;
        }
    }

    private static ActivitySectionView CreateActivitySection(IReadOnlyList<PopupActivityDisplayState> activityRows)
    {
        var stack = new StackPanel { Spacing = 6 };
        stack.Children.Add(new TextBlock
        {
            Text = "Additional activity",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = PopupTextPrimaryBrush,
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
            Foreground = PopupTextPrimaryBrush,
            TextWrapping = TextWrapping.WrapWholeWords
        };
        grid.Children.Add(label);

        var valueBlock = new TextBlock
        {
            Text = activity.ValueText,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = PopupTextPrimaryBrush,
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
            Foreground = PopupTextSecondaryBrush,
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

    private void SetRefreshCommandState(ManualRefreshCommandState state)
    {
        if (!Equals(RefreshNowButton.Content, state.Label))
        {
            RefreshNowButton.Content = state.Label;
        }

        RefreshNowButton.IsEnabled = state.IsEnabled;
        SetTextIfChanged(RefreshCommandStatusText, state.StatusText);
        SetVisibilityIfChanged(
            RefreshCommandStatusText,
            string.IsNullOrWhiteSpace(state.StatusText)
                ? Visibility.Collapsed
                : Visibility.Visible);
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
            QuotaRailView rail,
            TextBlock? relativeReset,
            TextBlock? exactReset)
        {
            Root = root;
            State = state;
            Label = label;
            PercentageText = percentageText;
            Rail = rail;
            RelativeReset = relativeReset;
            ExactReset = exactReset;
        }

        public StackPanel Root { get; }

        private PopupQuotaWindowDisplayState State { get; set; }

        private TextBlock Label { get; }

        private TextBlock PercentageText { get; }

        private QuotaRailView Rail { get; }

        private TextBlock? RelativeReset { get; }

        private TextBlock? ExactReset { get; }

        public void Update(PopupQuotaWindowDisplayState state)
        {
            SetTextIfChanged(Label, state.Label);
            SetTextIfChanged(PercentageText, state.PercentageText);
            Rail.Update(state);

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

    private sealed class QuotaRailView
    {
        public QuotaRailView(Canvas root, Canvas crawler, double railWidth, double crawlerWidth)
        {
            Root = root;
            _crawler = crawler;
            _railWidth = railWidth;
            _crawlerWidth = crawlerWidth;
        }

        private readonly Canvas _crawler;
        private readonly double _railWidth;
        private readonly double _crawlerWidth;

        public Canvas Root { get; }

        public void Update(PopupQuotaWindowDisplayState state)
        {
            Canvas.SetLeft(
                _crawler,
                QuotaRailPositionCalculator.CalculateCrawlerOffset(
                    state.ProgressValue,
                    _railWidth,
                    _crawlerWidth));
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

    private void OnRefreshNowClicked(object sender, RoutedEventArgs args)
    {
        _ = sender;
        _ = args;
        RefreshRequested?.Invoke();
    }

    private void OnSettingsClicked(object sender, RoutedEventArgs args)
    {
        _ = sender;
        _ = args;
        SettingsRequested?.Invoke();
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
