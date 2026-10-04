using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using TokenFish.App.Platform;
using TokenFish.Core.Models;
using TokenFish.Infrastructure;

namespace TokenFish.App;

public sealed partial class MainWindow : Window
{
    private readonly PopupDisplayStateUpdatePlanner _updatePlanner = new();
    private readonly List<ProviderView> _cards = [];
    private TrayPopupDisplayState? _state;
    private bool _allowClose;
    private bool _isSurfaceVisible;
    public event Action? PopupDeactivated;
    public event Action? PopupActivated;
    public event Action? PopupCloseRequested;
    public event Action? ContentSizeInvalidated;
    public event Action? RefreshRequested;
    public event Action? SettingsRequested;
    public event Action? WidgetRequested;

    public MainWindow()
    {
        InitializeComponent();
        if (ApplicationIconPath.TryResolveExistingWindowIcon(AppContext.BaseDirectory, out var path))
            AppWindow.SetIcon(path);
        AppWindow.Closing += (_, args) =>
        {
            if (!_allowClose) { args.Cancel = true; PopupCloseRequested?.Invoke(); }
        };
        Activated += (_, args) =>
        {
            PopupWindowPlacement.RemoveNativeFrameAfterShowing(this);
            if (args.WindowActivationState == WindowActivationState.Deactivated) PopupDeactivated?.Invoke();
            else PopupActivated?.Invoke();
        };
        Content.KeyDown += (_, args) =>
        {
            if (args.Key == Windows.System.VirtualKey.Escape) { PopupCloseRequested?.Invoke(); args.Handled = true; }
        };
        RootGrid.ActualThemeChanged += (_, _) => RebuildCards();
    }

    public void AllowClose() => _allowClose = true;
    internal void SetSurfaceVisible(bool visible)
    {
        _isSurfaceVisible = visible;
        foreach (var card in _cards) card.SetSurfaceVisible(visible);
    }
    public void ApplySettings(AppSettings settings)
    {
        TokenFishAppearance.Apply(RootGrid, settings.ThemeMode);
        WidgetButton.Content = settings.IsDesktopWidgetVisible ? "Hide desktop widget" : "Show desktop widget";
    }

    public void UpdateState(TrayPopupDisplayState state)
    {
        _state = state;
        var plan = _updatePlanner.Plan(state);
        StatusText.Text = state.StatusText;
        StatusBannerText.Text = state.StatusText;
        StatusBanner.Visibility = state.ApplicationState is PopupApplicationDisplayState.StartupIssue or
            PopupApplicationDisplayState.RefreshIssue or PopupApplicationDisplayState.ShellIssue or PopupApplicationDisplayState.ShutdownIssue
            ? Visibility.Visible : Visibility.Collapsed;
        RefreshNowButton.IsEnabled = state.RefreshCommandState.IsEnabled;
        RefreshCommandStatusText.Text = state.RefreshCommandState.StatusText ?? "";
        RefreshCommandStatusText.Visibility = string.IsNullOrWhiteSpace(state.RefreshCommandState.StatusText) ? Visibility.Collapsed : Visibility.Visible;
        if (plan.RebuildProviderCards || _cards.Count != state.Providers.Count) RebuildCards();
        else for (var i = 0; i < _cards.Count; i++) _cards[i].Update(state.Providers[i]);
        if (plan.AffectsLayout) ContentSizeInvalidated?.Invoke();
    }

    public double MeasurePreferredHeightEffectivePixels()
    {
        RootGrid.Measure(new Windows.Foundation.Size(PopupWindowLayoutCalculator.WidthEffectivePixels, double.PositiveInfinity));
        return Math.Ceiling(RootGrid.DesiredSize.Height);
    }

    private void RebuildCards()
    {
        if (_state is null) return;
        var expanded = _cards.Where(card => card.Activity.IsExpanded).Select(card => card.Provider).ToHashSet();
        _cards.Clear(); ProvidersPanel.Children.Clear();
        foreach (var provider in _state.Providers)
        {
            var card = new ProviderView(RootGrid, provider, () => ContentSizeInvalidated?.Invoke());
            card.Activity.IsExpanded = expanded.Contains(provider.Provider);
            card.SetSurfaceVisible(_isSurfaceVisible);
            _cards.Add(card); ProvidersPanel.Children.Add(card.Root);
        }
    }

    private void OnSettingsClicked(object sender, RoutedEventArgs args) => SettingsRequested?.Invoke();
    private void OnRefreshNowClicked(object sender, RoutedEventArgs args) => RefreshRequested?.Invoke();
    private void OnHideClicked(object sender, RoutedEventArgs args) => PopupCloseRequested?.Invoke();
    private void OnWidgetClicked(object sender, RoutedEventArgs args) => WidgetRequested?.Invoke();

    private sealed class ProviderView
    {
        public ProviderKind Provider { get; }
        public Border Root { get; }
        public Expander Activity { get; }
        private readonly TextBlock _connection;
        private readonly TextBlock _footer;
        private readonly TextBlock _empty;
        private readonly List<(TextBlock Label, TextBlock Remaining, FishQuotaRail Rail, TextBlock Used, TextBlock Reset)> _quotas = [];
        private readonly List<(TextBlock Label, TextBlock Value)> _activity = [];
        private readonly StackPanel _daily = new() { Spacing = 8 };
        private readonly FrameworkElement _themeRoot;

        public ProviderView(FrameworkElement themeRoot, ProviderCardDisplayState state, Action invalidate)
        {
            Provider = state.Provider;
            _themeRoot=themeRoot;
            var stack = new StackPanel { Spacing = 12 };
            Root = new Border { Padding = new(15), CornerRadius = new(12), Background = TokenFishAppearance.Brush(themeRoot,"Raised"), Child = stack };
            TextBlock Text(string value, bool muted = false, double size = 12) => new()
            {
                Text = value, FontSize = size, TextWrapping = TextWrapping.Wrap,
                Foreground = TokenFishAppearance.Brush(themeRoot,muted ? "Muted" : "Text")
            };
            Grid Pair(FrameworkElement left, FrameworkElement right)
            {
                var grid = new Grid { ColumnSpacing = 8 };
                grid.ColumnDefinitions.Add(new() { Width = new(1,GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
                grid.Children.Add(left); Grid.SetColumn(right,1); grid.Children.Add(right); return grid;
            }
            var name = Text(state.ProviderName,false,14); name.FontWeight = FontWeights.SemiBold;
            AutomationProperties.SetHeadingLevel(name,AutomationHeadingLevel.Level2);
            _connection = Text(state.ConnectionState,true);
            stack.Children.Add(Pair(name,_connection));
            foreach (var quota in state.QuotaWindows)
            {
                var label = Text(quota.Label,true);
                var remaining = Text(quota.RemainingText,false,19); remaining.FontWeight = FontWeights.SemiBold;
                var rail = new FishQuotaRail(state.Provider);
                var used = Text(quota.PercentageText,true,11);
                var reset = Text(quota.RelativeResetText ?? "Reset time unavailable",true,11);
                stack.Children.Add(Pair(label,remaining)); stack.Children.Add(rail); stack.Children.Add(Pair(used,reset));
                _quotas.Add((label,remaining,rail,used,reset));
            }
            var activityStack = new StackPanel { Spacing = 10 };
            foreach (var metric in state.ActivityRows)
            {
                var label = Text(metric.Label,true); var value = Text(metric.ValueText);
                activityStack.Children.Add(Pair(label,value));
                _activity.Add((label,value));
            }
            Activity = new Expander { Header = $"{state.ProviderName} activity", HorizontalAlignment = HorizontalAlignment.Stretch,
                Content = activityStack, Visibility = _activity.Count > 0 ? Visibility.Visible : Visibility.Collapsed };
            activityStack.Children.Add(_daily);
            activityStack.Children.Add(Text("Token activity is separate from quota. Daily intervals use UTC.",true,11));
            Activity.Expanding += (_, _) => invalidate(); Activity.Collapsed += (_, _) => invalidate();
            Activity.SizeChanged += (_, _) => invalidate();
            stack.Children.Add(Activity);
            _empty = Text("",true); stack.Children.Add(_empty);
            _footer = Text("",true,11); stack.Children.Add(_footer);
            Update(state);
        }

        public void SetSurfaceVisible(bool visible)
        {
            foreach (var quota in _quotas) quota.Rail.SetSurfaceVisible(visible);
        }

        public void Update(ProviderCardDisplayState state)
        {
            _connection.Text = state.IsStale ? $"{state.ConnectionState} · Stale" : state.ConnectionState;
            _footer.Text = state.FooterText;
            _footer.Visibility = string.IsNullOrEmpty(state.FooterText) ? Visibility.Collapsed : Visibility.Visible;
            _empty.Text = state.EmptyUsageMessage ?? "";
            _empty.Visibility = state.EmptyUsageMessage is null ? Visibility.Collapsed : Visibility.Visible;
            for (var i=0; i<_quotas.Count; i++)
            {
                var data = state.QuotaWindows[i]; var ui = _quotas[i];
                ui.Label.Text = data.Label; ui.Remaining.Text = data.RemainingText; ui.Rail.Update(data);
                ui.Used.Text = data.PercentageText; ui.Reset.Text = data.RelativeResetText ?? "Reset time unavailable";
                ToolTipService.SetToolTip(ui.Reset,data.ExactResetText);
                AutomationProperties.SetName(ui.Remaining,$"{data.Label}: {data.RemainingText}");
            }
            for (var i=0; i<_activity.Count; i++)
            {
                var data = state.ActivityRows[i]; var ui = _activity[i];
                ui.Label.Text = data.IntervalText is null ? data.Label : $"{data.Label} · {data.IntervalText}";
                ui.Value.Text = data.ValueText; AutomationProperties.SetName(ui.Value,data.AutomationName);
            }
            RenderDailyActivity(state);
        }

        private void RenderDailyActivity(ProviderCardDisplayState state)
        {
            _daily.Children.Clear();
            if(state.DailyActivity.Count==0) return;
            var today=DateOnly.FromDateTime(DateTime.UtcNow);
            var todayTokens=state.DailyActivity.FirstOrDefault(day=>day.Date==today)?.Tokens;
            _daily.Children.Add(new TextBlock {Text=todayTokens is {} value ? $"Today (UTC) · {value:N0} tokens" : "Today (UTC) · Not reported",
                Foreground=TokenFishAppearance.Brush(_themeRoot,"Text"),FontSize=12});
            var chart=new Grid {ColumnSpacing=8,Height=90};
            var max=Math.Max(1,state.DailyActivity.Max(day=>day.Tokens));
            for(var i=0;i<7;i++)
            {
                chart.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});
                var date=today.AddDays(i-6); var tokens=state.DailyActivity.FirstOrDefault(day=>day.Date==date)?.Tokens;
                var column=new StackPanel {Spacing=5,VerticalAlignment=VerticalAlignment.Bottom};
                var bar=new Border {Height=tokens is {} count ? Math.Max(2,60.0*count/max) : 2,CornerRadius=new(3),
                    Background=TokenFishAppearance.Brush(_themeRoot,tokens is null ? "Track" : "Codex")};
                var tip=tokens is {} reported ? $"{date:yyyy-MM-dd} (UTC): {reported:N0} tokens" : $"{date:yyyy-MM-dd} (UTC): not reported";
                ToolTipService.SetToolTip(column,tip); AutomationProperties.SetName(column,tip);
                column.Children.Add(bar);column.Children.Add(new TextBlock {Text=date.Day.ToString(),FontSize=11,HorizontalAlignment=HorizontalAlignment.Center,
                    Foreground=TokenFishAppearance.Brush(_themeRoot,"Muted")});
                Grid.SetColumn(column,i);chart.Children.Add(column);
            }
            _daily.Children.Add(chart);
            _daily.Children.Add(new TextBlock {Text="Latest seven UTC dates · Gray marks mean not reported",TextWrapping=TextWrapping.Wrap,FontSize=11,
                Foreground=TokenFishAppearance.Brush(_themeRoot,"Muted")});
        }
    }
}
