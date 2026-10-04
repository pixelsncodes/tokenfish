using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using TokenFish.Core.Models;
using TokenFish.Infrastructure;
using Windows.UI.ViewManagement;

namespace TokenFish.App;

// Shared by the popup and desktop widget; movement uses consumed quota in both views.
internal sealed class FishQuotaRail : Grid
{
    private readonly Canvas _canvas = new() { Height = 26, IsHitTestVisible = false };
    private readonly Border _track = new() { Height = 4, CornerRadius = new(2) };
    private readonly Border _remaining = new() { Height = 4, CornerRadius = new(2), Opacity = .4 };
    private readonly Canvas _fish;
    private readonly List<Ellipse> _pellets = [];
    private readonly ProviderKind _provider;
    private decimal _used;
    private bool _isLoaded;
    private bool _isSurfaceVisible;
    private Storyboard? _mouthAnimation;

    public FishQuotaRail(ProviderKind provider)
    {
        _provider = provider;
        Height = 26;
        Children.Add(_canvas);
        _canvas.Children.Add(_track);
        _canvas.Children.Add(_remaining);
        Canvas.SetTop(_track, 11); Canvas.SetTop(_remaining, 11);
        for (var i = 0; i < 12; i++)
        {
            var pellet = new Ellipse { Width = 5, Height = 5 };
            Canvas.SetTop(pellet, 10.5);
            _pellets.Add(pellet); _canvas.Children.Add(pellet);
        }
        _fish = CreateFish();
        _canvas.Children.Add(_fish);
        AutomationProperties.SetAccessibilityView(_canvas, AccessibilityView.Raw);
        SizeChanged += (_, _) => Draw();
        ActualThemeChanged += (_, _) => Draw();
        Loaded += (_, _) => { _isLoaded = true; Draw(); UpdateAnimation(); };
        Unloaded += (_, _) => { _isLoaded = false; UpdateAnimation(); };
        RegisterPropertyChangedCallback(UIElement.VisibilityProperty, (_, _) => UpdateAnimation());
    }

    public void SetSurfaceVisible(bool visible)
    {
        _isSurfaceVisible = visible;
        UpdateAnimation();
    }

    public void Update(PopupQuotaWindowDisplayState window)
    {
        _used = window.ProgressValue;
        AutomationProperties.SetName(this, window.ProgressAutomationName);
        Draw();
        UpdateAnimation();
    }

    private void UpdateAnimation()
    {
        bool systemAnimationsEnabled;
        try { systemAnimationsEnabled = new UISettings().AnimationsEnabled; }
        catch { systemAnimationsEnabled = false; }
        if (!FishAnimationSettings.ShouldAnimate(systemAnimationsEnabled,
            _isSurfaceVisible && Visibility == Visibility.Visible, _isLoaded))
        {
            _mouthAnimation?.Stop();
            _mouthAnimation = null;
            return;
        }
        if (_mouthAnimation is not null) return;

        // Keep the fish at the real quota position while its mouth eats the pellets.
        var animation = new ObjectAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromMilliseconds(340),
            RepeatBehavior = RepeatBehavior.Forever,
            EnableDependentAnimation = true
        };
        animation.KeyFrames.Add(new DiscreteObjectKeyFrame
        {
            KeyTime = KeyTime.FromTimeSpan(TimeSpan.Zero), Value = CreateFishBodyPoints(true)
        });
        animation.KeyFrames.Add(new DiscreteObjectKeyFrame
        {
            KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(170)), Value = CreateFishBodyPoints(false)
        });
        Storyboard.SetTarget(animation, _fish.Children[0]);
        Storyboard.SetTargetProperty(animation, "Points");
        _mouthAnimation = new Storyboard();
        _mouthAnimation.Children.Add(animation);
        _mouthAnimation.Begin();
    }

    private void Draw()
    {
        var accent = TokenFishAppearance.Brush(this, _provider == ProviderKind.Claude ? "Claude" : "Codex");
        var neutral = TokenFishAppearance.Brush(this, "Track");
        var width = Math.Max(0, ActualWidth);
        var offset = QuotaRailPositionCalculator.CalculateCrawlerOffset(_used, width, 30);
        var mouth = Math.Min(width, offset + 30);
        _track.Width = width; _track.Background = neutral;
        _remaining.Width = Math.Max(0, width - mouth); _remaining.Background = accent;
        Canvas.SetLeft(_remaining, mouth); Canvas.SetLeft(_fish, offset);
        ((Polygon)_fish.Children[0]).Fill = accent;
        ((Rectangle)_fish.Children[1]).Fill = accent;
        ((Rectangle)_fish.Children[2]).Fill = TokenFishAppearance.Brush(this, "Surface");
        for (var i = 0; i < _pellets.Count; i++)
        {
            var x = width * (i + .5) / _pellets.Count;
            Canvas.SetLeft(_pellets[i], x - 2.5);
            _pellets[i].Fill = x >= mouth ? accent : neutral;
        }
    }

    public static Canvas CreateFish()
    {
        var fish = new Canvas { Width = 30, Height = 26, IsHitTestVisible = false };
        fish.Children.Add(new Polygon
        {
            Points = CreateFishBodyPoints(true)
        });
        Canvas.SetLeft(fish.Children[0], 6); Canvas.SetTop(fish.Children[0], 1.5);
        fish.Children.Add(new Rectangle { Width = 8, Height = 8, Opacity = .8,
            RenderTransform = new RotateTransform { Angle = 45, CenterX = 4, CenterY = 4 } });
        Canvas.SetLeft(fish.Children[1], 1); Canvas.SetTop(fish.Children[1], 9);
        fish.Children.Add(new Rectangle { Width = 4, Height = 4 });
        Canvas.SetLeft(fish.Children[2], 16); Canvas.SetTop(fish.Children[2], 7.5);
        AutomationProperties.SetAccessibilityView(fish, AccessibilityView.Raw);
        return fish;
    }

    private static PointCollection CreateFishBodyPoints(bool open) => new()
    {
        new(0,0), new(17.94,0), new(23,open ? 7.82 : 10.12),
        new(open ? 16.79 : 18.86,11.5), new(23,open ? 15.18 : 12.88),
        new(17.94,23), new(0,23), new(2.76,14.26), new(0,11.5), new(2.76,8.74)
    };
}
