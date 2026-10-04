using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using TokenFish.App.Platform;
using TokenFish.Core.Models;
using TokenFish.Infrastructure;
using Windows.Graphics;

namespace TokenFish.App;

internal sealed class DesktopWidgetWindow : Window
{
    private readonly Grid _root = new() { Padding = new(10,12,10,12), ColumnSpacing = 6 };
    private readonly StackPanel _rows = new() { Spacing = 9 };
    private readonly List<(ProviderKind Provider,TextBlock Label,TextBlock Value,FishQuotaRail Rail)> _quotaRows = [];
    private AppSettings _settings = new();
    private TrayPopupDisplayState? _state;
    private bool _allowClose;
    private bool _visible;
    private bool _isDragging;
    public event Action? OpenRequested;
    public event Action? SettingsRequested;
    public event Action? HideRequested;
    public event Action<DesktopWidgetCorner,int,int>? PlacementChanged;
    public bool IsVisible => _visible;
    public RectInt32 Rectangle => new(AppWindow.Position.X,AppWindow.Position.Y,AppWindow.Size.Width,AppWindow.Size.Height);

    public DesktopWidgetWindow()
    {
        Title = "TokenFish desktop widget";
        Content = _root;
        _root.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        _root.ColumnDefinitions.Add(new() { Width = new(1,GridUnitType.Star) });
        _root.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var grip = new Button { Content = "⋮", Width = 20, Padding = new(0), VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(grip,"Move widget; arrow keys change corner");
        // Button consumes pointer presses internally; listen to handled presses too.
        grip.AddHandler(UIElement.PointerPressedEvent, new Microsoft.UI.Xaml.Input.PointerEventHandler((_, args) =>
        {
            if (!args.GetCurrentPoint(grip).Properties.IsLeftButtonPressed) return;
            grip.Focus(FocusState.Pointer);
            _isDragging = true;
            try { PopupWindowPlacement.DragWindow(this); }
            finally { _isDragging = false; }
            SnapAfterDrag();
            args.Handled = true;
        }), true);
        grip.KeyDown += (_, args) =>
        {
            var corner = _settings.DesktopWidgetCorner;
            var left = corner is DesktopWidgetCorner.BottomLeft or DesktopWidgetCorner.TopLeft;
            var top = corner is DesktopWidgetCorner.TopLeft or DesktopWidgetCorner.TopRight;
            switch(args.Key)
            {
                case Windows.System.VirtualKey.Left: left=true; break;
                case Windows.System.VirtualKey.Right: left=false; break;
                case Windows.System.VirtualKey.Up: top=true; break;
                case Windows.System.VirtualKey.Down: top=false; break;
                default: return;
            }
            var newCorner = (left,top) switch { (true,true)=>DesktopWidgetCorner.TopLeft,(false,true)=>DesktopWidgetCorner.TopRight,(true,false)=>DesktopWidgetCorner.BottomLeft,_=>DesktopWidgetCorner.BottomRight };
            _settings = _settings with { DesktopWidgetCorner = newCorner };
            Position(); ReportPlacement(); args.Handled=true;
        };
        _root.Children.Add(grip);
        var open = new Button { Content = _rows, Padding = new(0), HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent), BorderThickness = new(0) };
        AutomationProperties.SetName(open,"Open full TokenFish usage view");
        open.Click += (_, _) => OpenRequested?.Invoke();
        Grid.SetColumn(open,1); _root.Children.Add(open);
        var options = new Button { Content = "⋯", Width = 24, Padding = new(0) };
        AutomationProperties.SetName(options,"Widget options");
        var menu = new MenuFlyout();
        void Item(string text,Action action) { var item=new MenuFlyoutItem {Text=text}; item.Click+=(_,_)=>action(); menu.Items.Add(item); }
        Item("Open full view",()=>OpenRequested?.Invoke());
        Item("Settings",()=>SettingsRequested?.Invoke());
        Item("Hide widget",()=>HideRequested?.Invoke());
        options.Flyout=menu; Grid.SetColumn(options,2); _root.Children.Add(options);
        PopupWindowPlacement.Configure(this);
        AppWindow.Closing += (_,args)=> { if(!_allowClose) {args.Cancel=true;HideRequested?.Invoke();} };
        Activated += (_,args)=>
        {
            PopupWindowPlacement.RemoveNativeFrameAfterShowing(this);
            if (args.WindowActivationState != WindowActivationState.Deactivated)
                grip.Focus(FocusState.Programmatic);
        };
        _root.ActualThemeChanged += (_,_)=> { BuildRows(); if (_state is { } state) UpdateState(state); Position(); };
    }

    public void ApplySettings(AppSettings settings)
    {
        var changed = _settings.DesktopWidgetCorner != settings.DesktopWidgetCorner ||
            _settings.DesktopWidgetMonitorX != settings.DesktopWidgetMonitorX || _settings.DesktopWidgetMonitorY != settings.DesktopWidgetMonitorY;
        _settings=settings;
        TokenFishAppearance.Apply(_root,settings.ThemeMode);
        _root.Background=TokenFishAppearance.Brush(_root,"Surface");
        if(AppWindow.Presenter is OverlappedPresenter presenter) presenter.IsAlwaysOnTop=settings.IsDesktopWidgetAlwaysOnTop;
        if(settings.IsDesktopWidgetVisible)
        {
            if(!_visible || changed) Position();
            if(!_visible) { AppWindow.Show(activateWindow:false); _visible=true; PopupWindowPlacement.RemoveNativeFrameAfterShowing(this); Position(); }
        }
        else { AppWindow.Hide(); _visible=false; }
        foreach (var row in _quotaRows) row.Rail.SetSurfaceVisible(_visible);
    }

    public void UpdateState(TrayPopupDisplayState state)
    {
        var rebuild=_state is null || !_state.Providers.Select(p=>p.Provider).SequenceEqual(state.Providers.Select(p=>p.Provider));
        _state=state;
        if(rebuild) { BuildRows(); Position(); }
        foreach(var row in _quotaRows)
        {
            var provider=state.Providers.First(p=>p.Provider==row.Provider);
            var quota=provider.QuotaWindows.FirstOrDefault();
            row.Label.Text=provider.ProviderName+(quota is null ? "" : " · "+quota.Label);
            row.Label.TextTrimming=TextTrimming.CharacterEllipsis;
            row.Value.Text=quota is null ? provider.ConnectionState : quota.RemainingText;
            if(provider.IsStale) row.Value.Text+=" · stale";
            row.Rail.Visibility=quota is null ? Visibility.Collapsed : Visibility.Visible;
            if(quota is not null) row.Rail.Update(quota);
            ToolTipService.SetToolTip(row.Label,quota?.Label);
            ToolTipService.SetToolTip(row.Value,provider.FooterText);
        }
        _root.Background=TokenFishAppearance.Brush(_root,"Surface");
        // Display changes (including a removed monitor) are resolved against the saved anchor.
        if(_visible) Position();
    }

    private void BuildRows()
    {
        _quotaRows.Clear(); _rows.Children.Clear();
        if(_state is null) return;
        foreach(var provider in _state.Providers)
        {
            var row=new StackPanel {Spacing=3};
            var labels=new Grid {ColumnSpacing=6};
            labels.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});
            labels.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
            var label=new TextBlock {FontSize=11,Foreground=TokenFishAppearance.Brush(_root,"Muted")};
            var value=new TextBlock {FontSize=12,FontWeight=FontWeights.SemiBold,Foreground=TokenFishAppearance.Brush(_root,"Text")};
            labels.Children.Add(label); Grid.SetColumn(value,1); labels.Children.Add(value);
            var rail=new FishQuotaRail(provider.Provider); row.Children.Add(labels);row.Children.Add(rail);_rows.Children.Add(row);
            rail.SetSurfaceVisible(_visible);
            _quotaRows.Add((provider.Provider,label,value,rail));
        }
    }

    private DisplayArea TargetDisplay() => _settings.DesktopWidgetMonitorX is {} x && _settings.DesktopWidgetMonitorY is {} y
        ? DisplayArea.GetFromPoint(new(x,y),DisplayAreaFallback.Nearest)
        : DisplayArea.GetFromWindowId(AppWindow.Id,DisplayAreaFallback.Primary);

    private void Position()
    {
        if(!_settings.IsDesktopWidgetVisible || _isDragging) return;
        var area=TargetDisplay().WorkArea;
        var scale=SettingsWindowSizing.GetRasterizationScale(this);
        var size=SettingsWindowSizing.ToPhysicalSize(282,_quotaRows.Count>1 ? 125 : 72,scale);
        var placement=DesktopWidgetLayoutCalculator.Calculate(new(area.X,area.Y,area.Width,area.Height),new(size.Width,size.Height),_settings.DesktopWidgetCorner,(int)Math.Ceiling(16*scale));
        AppWindow.MoveAndResize(new(placement.X,placement.Y,placement.Width,placement.Height));
        PopupWindowPlacement.RemoveNativeFrameAfterShowing(this);
    }

    private void SnapAfterDrag()
    {
        var area=DisplayArea.GetFromRect(Rectangle,DisplayAreaFallback.Nearest).WorkArea;
        var rect=Rectangle;
        _settings=_settings with {DesktopWidgetCorner=DesktopWidgetLayoutCalculator.ClosestCorner(new(rect.X,rect.Y,rect.Width,rect.Height),new(area.X,area.Y,area.Width,area.Height)),DesktopWidgetMonitorX=rect.X+rect.Width/2,DesktopWidgetMonitorY=rect.Y+rect.Height/2};
        Position();ReportPlacement();
    }
    private void ReportPlacement() {var rect=Rectangle;PlacementChanged?.Invoke(_settings.DesktopWidgetCorner,rect.X+rect.Width/2,rect.Y+rect.Height/2);}
    public void Shutdown() {_allowClose=true;Close();}
}
