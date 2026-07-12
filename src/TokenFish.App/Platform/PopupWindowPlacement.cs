using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using WinRT.Interop;

namespace TokenFish.App.Platform;

internal static class PopupWindowPlacement
{
    private const int PopupWidthDip = 380;
    private const int PopupMaxHeightDip = 640;
    private const int PopupMarginPixels = 8;

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

    public static void PositionBesideIcon(Window window, RectInt32? iconRectangle)
    {
        ArgumentNullException.ThrowIfNull(window);

        var handle = WindowNative.GetWindowHandle(window);
        var dpi = NativeMethods.GetDpiForWindow(handle);
        var popupWidth = ScaleDip(PopupWidthDip, dpi);
        var popupHeight = Math.Min(
            ScaleDip(PopupMaxHeightDip, dpi),
            window.AppWindow.Size.Height > 0 ? window.AppWindow.Size.Height : ScaleDip(420, dpi));

        var anchor = iconRectangle ?? GetFallbackAnchor(window);
        var displayArea = DisplayArea.GetFromPoint(
            new PointInt32(anchor.X, anchor.Y),
            DisplayAreaFallback.Nearest);
        var workArea = displayArea.WorkArea;

        var left = anchor.X + (anchor.Width / 2) - (popupWidth / 2);
        var top = anchor.Y - popupHeight - PopupMarginPixels;

        if (anchor.Y <= workArea.Y + PopupMarginPixels)
        {
            top = anchor.Y + anchor.Height + PopupMarginPixels;
        }
        else if (anchor.X <= workArea.X + PopupMarginPixels)
        {
            left = anchor.X + anchor.Width + PopupMarginPixels;
            top = anchor.Y + (anchor.Height / 2) - (popupHeight / 2);
        }
        else if (anchor.X + anchor.Width >= workArea.X + workArea.Width - PopupMarginPixels)
        {
            left = anchor.X - popupWidth - PopupMarginPixels;
            top = anchor.Y + (anchor.Height / 2) - (popupHeight / 2);
        }

        left = Clamp(left, workArea.X, workArea.X + workArea.Width - popupWidth);
        top = Clamp(top, workArea.Y, workArea.Y + workArea.Height - popupHeight);

        window.AppWindow.Resize(new SizeInt32(popupWidth, popupHeight));
        window.AppWindow.Move(new PointInt32(left, top));
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

    private static int ScaleDip(int value, uint dpi) =>
        (int)Math.Round(value * dpi / 96.0);

    private static int Clamp(int value, int minimum, int maximum) =>
        Math.Min(Math.Max(value, minimum), maximum);

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
