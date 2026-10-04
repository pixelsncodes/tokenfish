using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using TokenFish.Infrastructure;
using Windows.Graphics;
using WinRT.Interop;

namespace TokenFish.App.Platform;

internal static class PopupWindowPlacement
{
    private static readonly NativeMethods.SubclassProcedure BorderlessProcedure = ProcessBorderlessMessage;

    public static void Configure(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        var configuration = PopupWindowPresenterConfiguration.TrayPopup;
        var presenter = OverlappedPresenter.Create();
        window.AppWindow.SetPresenter(presenter);
        presenter.SetBorderAndTitleBar(configuration.HasBorder, configuration.HasTitleBar);
        presenter.IsResizable = configuration.IsResizable;
        presenter.IsMaximizable = configuration.IsMaximizable;
        presenter.IsMinimizable = configuration.IsMinimizable;

        var handle = WindowNative.GetWindowHandle(window);
        if (!NativeMethods.SetWindowSubclass(handle, BorderlessProcedure, 1, 0))
            System.Diagnostics.Debug.WriteLine("TokenFish: borderless client-area hook failed.");
        RemoveDwmBorder(handle);
        var extendedStyle = NativeMethods.GetWindowLongPtr(handle, NativeMethods.GwlExStyle);
        if (Environment.GetCommandLineArgs().Contains("--inspect-windows", StringComparer.OrdinalIgnoreCase))
        {
            // Opt-in development inspection: make borderless surfaces discoverable to desktop tools.
            extendedStyle &= ~NativeMethods.WsExToolWindow;
            extendedStyle |= NativeMethods.WsExAppWindow;
        }
        else
        {
            extendedStyle &= ~NativeMethods.WsExAppWindow;
            extendedStyle |= NativeMethods.WsExToolWindow;
        }
        NativeMethods.SetWindowLongPtr(handle, NativeMethods.GwlExStyle, extendedStyle);
    }

    private static nint ProcessBorderlessMessage(nint hwnd, uint message, nint wParam, nint lParam,
        nuint subclassId, nuint referenceData)
    {
        // WM_NCCALCSIZE: the client must occupy the complete borderless window.
        // Merely hiding the presenter chrome can leave a native rim on WinUI windows.
        if (message == 0x0083 && wParam != 0) return 0;
        if (message == 0x0082)
            NativeMethods.RemoveWindowSubclass(hwnd, BorderlessProcedure, subclassId);
        return NativeMethods.DefSubclassProc(hwnd, message, wParam, lParam);
    }

    private static void RemoveDwmBorder(nint handle)
    {
        if (handle == 0)
        {
            return;
        }

        try
        {
            // These surfaces draw their whole client area; DWM must not add a native rim.
            var nonClientPolicy = NativeMethods.DwmNcrpDisabled;
            var policyResult = NativeMethods.DwmSetWindowAttribute(handle,
                NativeMethods.DwmwaNcRenderingPolicy, ref nonClientPolicy, (uint)Marshal.SizeOf<uint>());
            if (policyResult < 0)
                System.Diagnostics.Debug.WriteLine($"TokenFish: DWM frame policy failed (0x{policyResult:X8}).");
            var borderColor = NativeMethods.DwmColorNone;
            var result = NativeMethods.DwmSetWindowAttribute(
                handle,
                NativeMethods.DwmwaBorderColor,
                ref borderColor,
                (uint)Marshal.SizeOf<uint>());
            if (result < 0)
                System.Diagnostics.Debug.WriteLine($"TokenFish: DWM border preference failed (0x{result:X8}).");
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }
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
        var nonClientHeight = GetNonClientHeight(handle);
        var layout = PopupWindowLayoutCalculator.Calculate(
            new PopupPhysicalRect(anchor.X, anchor.Y, anchor.Width, anchor.Height),
            new PopupPhysicalRect(workArea.X, workArea.Y, workArea.Width, workArea.Height),
            rasterizationScale,
            measuredContentHeightEffectivePixels,
            nonClientHeight);

        window.AppWindow.Resize(new SizeInt32(layout.Size.Width, layout.Size.Height));
        window.AppWindow.Move(new PointInt32(layout.Position.X, layout.Position.Y));
        // AppWindow placement can refresh its native frame after the first show.
        RemoveNativeFrameAfterShowing(window);
    }

    public static bool BringToForeground(Window window)
    {
        window.Activate();
        return NativeMethods.SetForegroundWindow(WindowNative.GetWindowHandle(window));
    }

    public static bool RemoveNativeFrameAfterShowing(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        var handle = WindowNative.GetWindowHandle(window);
        if (handle == 0)
        {
            return false;
        }

        Marshal.SetLastPInvokeError(0);
        var style = NativeMethods.GetWindowLongPtr(handle, NativeMethods.GwlStyle);
        if (style == 0 && Marshal.GetLastPInvokeError() != 0)
        {
            return false;
        }

        var maskedStyle = PopupWindowStyleMask.RemovePopupNonClientFlags(style);
        if (maskedStyle != style)
        {
            Marshal.SetLastPInvokeError(0);
            _ = NativeMethods.SetWindowLongPtr(handle, NativeMethods.GwlStyle, maskedStyle);
            if (Marshal.GetLastPInvokeError() != 0)
            {
                return false;
            }
        }

        Marshal.SetLastPInvokeError(0);
        var extendedStyle = NativeMethods.GetWindowLongPtr(handle, NativeMethods.GwlExStyle);
        if (extendedStyle == 0 && Marshal.GetLastPInvokeError() != 0) return false;
        var maskedExtendedStyle = PopupWindowStyleMask.RemovePopupExtendedEdges(extendedStyle);
        if (extendedStyle != maskedExtendedStyle)
        {
            Marshal.SetLastPInvokeError(0);
            NativeMethods.SetWindowLongPtr(handle, NativeMethods.GwlExStyle, maskedExtendedStyle);
            if (Marshal.GetLastPInvokeError() != 0) return false;
        }

        var updated = NativeMethods.SetWindowPos(
            handle,
            0,
            0,
            0,
            0,
            0,
            NativeMethods.SwpNoMove |
            NativeMethods.SwpNoSize |
            NativeMethods.SwpNoZOrder |
            NativeMethods.SwpNoActivate |
            NativeMethods.SwpFrameChanged);
        RemoveDwmBorder(handle);
        if (!updated)
            System.Diagnostics.Debug.WriteLine($"TokenFish: native frame update failed ({Marshal.GetLastPInvokeError()}).");
        return updated;
    }

    public static bool IsForeground(Window window) =>
        NativeMethods.GetForegroundWindow() == WindowNative.GetWindowHandle(window);

    public static void DragWindow(Window window)
    {
        NativeMethods.ReleaseCapture();
        NativeMethods.SendMessage(WindowNative.GetWindowHandle(window), 0x00A1, 2, 0);
    }

    private static RectInt32 GetFallbackAnchor(Window window)
    {
        var area = DisplayArea.GetFromWindowId(window.AppWindow.Id, DisplayAreaFallback.Primary);
        return new RectInt32(
            area.WorkArea.X + area.WorkArea.Width - 24,
            area.WorkArea.Y + area.WorkArea.Height - 24,
            24,
            24);
    }

    private static int GetNonClientHeight(nint handle)
    {
        if (handle == 0 ||
            !NativeMethods.GetWindowRect(handle, out var windowRect) ||
            !NativeMethods.GetClientRect(handle, out var clientRect))
        {
            return 0;
        }

        return Math.Max(0, windowRect.Height - clientRect.Height);
    }

    private static class NativeMethods
    {
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate nint SubclassProcedure(nint hwnd, uint message, nint wParam, nint lParam,
            nuint subclassId, nuint referenceData);
        [DllImport("comctl32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowSubclass(nint hwnd, SubclassProcedure procedure, nuint subclassId, nuint referenceData);
        [DllImport("comctl32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool RemoveWindowSubclass(nint hwnd, SubclassProcedure procedure, nuint subclassId);
        [DllImport("comctl32.dll")]
        public static extern nint DefSubclassProc(nint hwnd, uint message, nint wParam, nint lParam);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        public static extern nint SendMessage(nint hwnd, uint message, nint wParam, nint lParam);
        public const int GwlStyle = -16;
        public const int GwlExStyle = -20;
        public const nint WsExAppWindow = 0x00040000;
        public const nint WsExToolWindow = 0x00000080;
        public const uint DwmwaBorderColor = 34;
        public const uint DwmwaNcRenderingPolicy = 2;
        public const uint DwmNcrpDisabled = 1;
        public const uint DwmColorNone = 0xFFFFFFFE;
        public const uint SwpNoSize = 0x0001;
        public const uint SwpNoMove = 0x0002;
        public const uint SwpNoZOrder = 0x0004;
        public const uint SwpNoActivate = 0x0010;
        public const uint SwpFrameChanged = 0x0020;

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(
            nint hwnd,
            uint attribute,
            ref uint value,
            uint valueSize);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
        public static extern nint GetWindowLongPtr(nint hWnd, int index);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        public static extern nint SetWindowLongPtr(nint hWnd, int index, nint newLong);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(
            nint hWnd,
            nint hWndInsertAfter,
            int x,
            int y,
            int cx,
            int cy,
            uint flags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(nint hWnd);

        [DllImport("user32.dll")]
        public static extern nint GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern uint GetDpiForWindow(nint hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(nint hWnd, out NativeRect rect);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetClientRect(nint hWnd, out NativeRect rect);

        [StructLayout(LayoutKind.Sequential)]
        public struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public readonly int Height => Bottom - Top;
        }
    }
}
