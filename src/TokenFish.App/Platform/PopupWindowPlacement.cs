using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using TokenFish.Infrastructure;
using Windows.Graphics;
using WinRT.Interop;

namespace TokenFish.App.Platform;

internal static class PopupWindowPlacement
{
    public static void Configure(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        var presenter = OverlappedPresenter.Create();
        presenter.SetBorderAndTitleBar(false, false);
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        window.AppWindow.SetPresenter(presenter);

        var handle = WindowNative.GetWindowHandle(window);
        var extendedStyle = NativeMethods.GetWindowLongPtr(handle, NativeMethods.GwlExStyle);
        extendedStyle &= ~NativeMethods.WsExAppWindow;
        extendedStyle |= NativeMethods.WsExToolWindow;
        NativeMethods.SetWindowLongPtr(handle, NativeMethods.GwlExStyle, extendedStyle);
    }

    public static void PositionBesideIcon(
        Window window,
        RectInt32? iconRectangle,
        double measuredContentHeightEffectivePixels)
    {
        ArgumentNullException.ThrowIfNull(window);

        var handle = WindowNative.GetWindowHandle(window);
        var rasterizationScale = window.Content.XamlRoot?.RasterizationScale ??
            NativeMethods.GetDpiForWindow(handle) / 96.0;

        var anchor = iconRectangle ?? GetFallbackAnchor(window);
        var displayArea = DisplayArea.GetFromPoint(
            new PointInt32(anchor.X, anchor.Y),
            DisplayAreaFallback.Nearest);
        var workArea = displayArea.WorkArea;
        var layout = PopupWindowLayoutCalculator.Calculate(
            new PopupPhysicalRect(anchor.X, anchor.Y, anchor.Width, anchor.Height),
            new PopupPhysicalRect(workArea.X, workArea.Y, workArea.Width, workArea.Height),
            rasterizationScale,
            measuredContentHeightEffectivePixels);

        window.AppWindow.Resize(new SizeInt32(layout.Size.Width, layout.Size.Height));
        window.AppWindow.Move(new PointInt32(layout.Position.X, layout.Position.Y));
    }

    public static bool BringToForeground(Window window)
    {
        window.Activate();
        return NativeMethods.SetForegroundWindow(WindowNative.GetWindowHandle(window));
    }

    public static bool IsForeground(Window window) =>
        NativeMethods.GetForegroundWindow() == WindowNative.GetWindowHandle(window);

    private static RectInt32 GetFallbackAnchor(Window window)
    {
        var area = DisplayArea.GetFromWindowId(window.AppWindow.Id, DisplayAreaFallback.Primary);
        return new RectInt32(
            area.WorkArea.X + area.WorkArea.Width - 24,
            area.WorkArea.Y + area.WorkArea.Height - 24,
            24,
            24);
    }

    private static class NativeMethods
    {
        public const int GwlExStyle = -20;
        public const nint WsExAppWindow = 0x00040000;
        public const nint WsExToolWindow = 0x00000080;

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
        public static extern nint GetWindowLongPtr(nint hWnd, int index);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        public static extern nint SetWindowLongPtr(nint hWnd, int index, nint newLong);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(nint hWnd);

        [DllImport("user32.dll")]
        public static extern nint GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern uint GetDpiForWindow(nint hWnd);
    }
}
