using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using TokenFish.Infrastructure;
using Windows.Graphics;
using WinRT.Interop;

namespace TokenFish.App.Platform;

internal sealed class NativeNotificationAreaIcon : INotificationAreaIcon
{
    private const uint IconId = 1;
    private const uint CallbackMessage = NativeMethods.WmApp + 0x0546;
    private const uint RefreshCommandId = 1001;
    private const uint ExitCommandId = 1002;
    private const string Tooltip = "TokenFish";

    private readonly nint _windowHandle;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly NativeMethods.WndProc _windowProcedure;
    private readonly uint _taskbarCreatedMessage;

    private nint _previousWindowProcedure;
    private nint _iconHandle;
    private bool _subclassed;
    private bool _added;
    private bool _removed;
    private bool _disposed;

    public NativeNotificationAreaIcon(Window ownerWindow, DispatcherQueue dispatcherQueue)
    {
        ArgumentNullException.ThrowIfNull(ownerWindow);
        ArgumentNullException.ThrowIfNull(dispatcherQueue);

        _windowHandle = WindowNative.GetWindowHandle(ownerWindow);
        _dispatcherQueue = dispatcherQueue;
        _windowProcedure = WindowProcedure;
        _taskbarCreatedMessage = NativeMethods.RegisterWindowMessage("TaskbarCreated");
    }

    public event Action<NotificationAreaCommand>? CommandRequested;

    public event Action? ShellFaulted;

    public void Initialize()
    {
        if (_disposed || _added)
        {
            return;
        }

        try
        {
            EnsureSubclassed();
            EnsureIconLoaded();
            AddIcon();
        }
        catch
        {
            RaiseShellFaulted();
        }
    }

    public void Restore()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            EnsureSubclassed();
            EnsureIconLoaded();
            AddIcon();
        }
        catch
        {
            RaiseShellFaulted();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_added && !_removed)
        {
            var data = CreateNotifyIconData();
            NativeMethods.ShellNotifyIcon(NativeMethods.NimDelete, ref data);
            _removed = true;
        }

        if (_subclassed)
        {
            NativeMethods.SetWindowLongPtr(
                _windowHandle,
                NativeMethods.GwlpWndProc,
                _previousWindowProcedure);
            _subclassed = false;
        }

        if (_iconHandle != 0)
        {
            NativeMethods.DestroyIcon(_iconHandle);
            _iconHandle = 0;
        }
    }

    public bool TryGetIconRectangle(out RectInt32 rectangle)
    {
        rectangle = default;

        var identifier = new NativeMethods.NotifyIconIdentifier
        {
            Size = (uint)Marshal.SizeOf<NativeMethods.NotifyIconIdentifier>(),
            WindowHandle = _windowHandle,
            Id = IconId
        };

        var result = NativeMethods.ShellNotifyIconGetRect(ref identifier, out var nativeRectangle);
        if (result != 0)
        {
            return false;
        }

        rectangle = new RectInt32(
            nativeRectangle.Left,
            nativeRectangle.Top,
            nativeRectangle.Right - nativeRectangle.Left,
            nativeRectangle.Bottom - nativeRectangle.Top);
        return true;
    }

    private void EnsureSubclassed()
    {
        if (_subclassed)
        {
            return;
        }

        var windowProcedurePointer = Marshal.GetFunctionPointerForDelegate(_windowProcedure);
        _previousWindowProcedure = NativeMethods.SetWindowLongPtr(
            _windowHandle,
            NativeMethods.GwlpWndProc,
            windowProcedurePointer);
        _subclassed = true;
    }

    private void EnsureIconLoaded()
    {
        if (_iconHandle != 0)
        {
            return;
        }

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        _iconHandle = NativeMethods.LoadImage(
            0,
            iconPath,
            NativeMethods.ImageIcon,
            0,
            0,
            NativeMethods.LrLoadFromFile | NativeMethods.LrDefaultSize);

        if (_iconHandle == 0)
        {
            throw new InvalidOperationException();
        }
    }

    private void AddIcon()
    {
        var data = CreateNotifyIconData();

        if (!NativeMethods.ShellNotifyIcon(NativeMethods.NimAdd, ref data) && _added)
        {
            data = CreateNotifyIconData();
            if (!NativeMethods.ShellNotifyIcon(NativeMethods.NimModify, ref data))
            {
                throw new InvalidOperationException();
            }
        }
        else
        {
            _added = true;
            _removed = false;
        }

        data = CreateNotifyIconData();
        data.Version = NativeMethods.NotifyIconVersion4;
        NativeMethods.ShellNotifyIcon(NativeMethods.NimSetVersion, ref data);
    }

    private NativeMethods.NotifyIconData CreateNotifyIconData() =>
        new()
        {
            Size = (uint)Marshal.SizeOf<NativeMethods.NotifyIconData>(),
            WindowHandle = _windowHandle,
            Id = IconId,
            Flags = NativeMethods.NifMessage | NativeMethods.NifIcon | NativeMethods.NifTip,
            CallbackMessage = CallbackMessage,
            IconHandle = _iconHandle,
            Tip = Tooltip
        };

    private nint WindowProcedure(nint windowHandle, uint message, nint wParam, nint lParam)
    {
        try
        {
            if (message == _taskbarCreatedMessage)
            {
                Restore();
                return 0;
            }

            if (message == CallbackMessage)
            {
                HandleIconCallback((uint)lParam);
                return 0;
            }
        }
        catch
        {
            RaiseShellFaulted();
        }

        return NativeMethods.CallWindowProc(
            _previousWindowProcedure,
            windowHandle,
            message,
            wParam,
            lParam);
    }

    private void HandleIconCallback(uint callback)
    {
        switch (callback)
        {
            case NativeMethods.WmLButtonUp:
            case NativeMethods.NinSelect:
            case NativeMethods.NinKeySelect:
                DispatchCommand(NotificationAreaCommand.PrimaryActivate);
                break;
            case NativeMethods.WmRButtonUp:
            case NativeMethods.WmContextMenu:
                ShowContextMenu();
                break;
        }
    }

    private void ShowContextMenu()
    {
        var menuHandle = NativeMethods.CreatePopupMenu();
        if (menuHandle == 0)
        {
            throw new InvalidOperationException();
        }

        try
        {
            NativeMethods.AppendMenu(menuHandle, NativeMethods.MfString, RefreshCommandId, "Refresh");
            NativeMethods.AppendMenu(menuHandle, NativeMethods.MfSeparator, 0, null);
            NativeMethods.AppendMenu(menuHandle, NativeMethods.MfString, ExitCommandId, "Exit");

            NativeMethods.GetCursorPos(out var point);
            NativeMethods.SetForegroundWindow(_windowHandle);
            var command = NativeMethods.TrackPopupMenuEx(
                menuHandle,
                NativeMethods.TpmReturnCmd | NativeMethods.TpmRightButton,
                point.X,
                point.Y,
                _windowHandle,
                0);

            if (command == RefreshCommandId)
            {
                DispatchCommand(NotificationAreaCommand.Refresh);
            }
            else if (command == ExitCommandId)
            {
                DispatchCommand(NotificationAreaCommand.Exit);
            }
        }
        finally
        {
            NativeMethods.DestroyMenu(menuHandle);
        }
    }

    private void DispatchCommand(NotificationAreaCommand command)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                CommandRequested?.Invoke(command);
            }
            catch
            {
                RaiseShellFaulted();
            }
        });
    }

    private void RaiseShellFaulted()
    {
        _dispatcherQueue.TryEnqueue(() => ShellFaulted?.Invoke());
    }

    private static class NativeMethods
    {
        public const int GwlpWndProc = -4;
        public const uint WmApp = 0x8000;
        public const uint WmLButtonUp = 0x0202;
        public const uint WmRButtonUp = 0x0205;
        public const uint WmContextMenu = 0x007B;
        public const uint NinSelect = 0x0400;
        public const uint NinKeySelect = 0x0401;
        public const uint NimAdd = 0x00000000;
        public const uint NimModify = 0x00000001;
        public const uint NimDelete = 0x00000002;
        public const uint NimSetVersion = 0x00000004;
        public const uint NifMessage = 0x00000001;
        public const uint NifIcon = 0x00000002;
        public const uint NifTip = 0x00000004;
        public const uint NotifyIconVersion4 = 4;
        public const uint ImageIcon = 1;
        public const uint LrLoadFromFile = 0x00000010;
        public const uint LrDefaultSize = 0x00000040;
        public const uint MfString = 0x00000000;
        public const uint MfSeparator = 0x00000800;
        public const uint TpmRightButton = 0x0002;
        public const uint TpmReturnCmd = 0x0100;

        public delegate nint WndProc(nint hWnd, uint msg, nint wParam, nint lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct NotifyIconData
        {
            public uint Size;
            public nint WindowHandle;
            public uint Id;
            public uint Flags;
            public uint CallbackMessage;
            public nint IconHandle;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string Tip;

            public uint State;
            public uint StateMask;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string Info;

            public uint Version;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string InfoTitle;

            public uint InfoFlags;
            public Guid GuidItem;
            public nint BalloonIconHandle;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Point
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct NativeRectangle
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct NotifyIconIdentifier
        {
            public uint Size;
            public nint WindowHandle;
            public uint Id;
            public Guid GuidItem;
        }

        [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconW", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ShellNotifyIcon(uint message, ref NotifyIconData data);

        [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconGetRect", SetLastError = true)]
        public static extern int ShellNotifyIconGetRect(
            ref NotifyIconIdentifier identifier,
            out NativeRectangle iconLocation);

        [DllImport("user32.dll", EntryPoint = "RegisterWindowMessageW", SetLastError = true)]
        public static extern uint RegisterWindowMessage(
            [MarshalAs(UnmanagedType.LPWStr)] string message);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        public static extern nint SetWindowLongPtr(nint hWnd, int index, nint newLong);

        [DllImport("user32.dll", EntryPoint = "CallWindowProcW")]
        public static extern nint CallWindowProc(
            nint previousWindowProcedure,
            nint hWnd,
            uint msg,
            nint wParam,
            nint lParam);

        [DllImport("user32.dll", EntryPoint = "LoadImageW", SetLastError = true)]
        public static extern nint LoadImage(
            nint instance,
            [MarshalAs(UnmanagedType.LPWStr)] string name,
            uint type,
            int desiredWidth,
            int desiredHeight,
            uint load);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DestroyIcon(nint icon);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern nint CreatePopupMenu();

        [DllImport("user32.dll", EntryPoint = "AppendMenuW", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool AppendMenu(
            nint menu,
            uint flags,
            uint idNewItem,
            [MarshalAs(UnmanagedType.LPWStr)] string? newItem);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DestroyMenu(nint menu);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetCursorPos(out Point point);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(nint hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint TrackPopupMenuEx(
            nint menu,
            uint flags,
            int x,
            int y,
            nint hWnd,
            nint parameters);
    }
}
