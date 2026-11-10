using System.Runtime.InteropServices;
using Microsoft.UI.Composition;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace Mashhad.App.Native;

/// <summary>
/// XAML cannot draw on top of the native video window, so Play mode's floating chrome lives in a
/// separate borderless, fully transparent window that is owned by the main window (stays above it,
/// minimises with it, has no taskbar button) and is kept exactly over the main window's client area.
/// </summary>
internal sealed class PlayOverlay
{
    public Window Window { get; }
    public IntPtr Handle { get; }
    private RectInt32 _bounds;
    private bool _visible;

    private PlayOverlay(Window window, IntPtr handle)
    {
        Window = window;
        Handle = handle;
    }

    public static PlayOverlay? TryCreate(IntPtr owner, UIElement content, out string? error)
    {
        try
        {
            var w = new Window { Content = content, SystemBackdrop = new TransparentBackdrop() };
            var presenter = OverlappedPresenter.Create();
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            w.AppWindow.SetPresenter(presenter);
            w.AppWindow.IsShownInSwitchers = false;
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(w);
            SetWindowLongPtrW(hwnd, GWLP_HWNDPARENT, owner);
            int corner = 1; // DWMWCP_DONOTROUND
            DwmSetWindowAttribute(hwnd, 33, ref corner, sizeof(int));
            int noBorder = unchecked((int)0xFFFFFFFE); // DWMWA_COLOR_NONE
            DwmSetWindowAttribute(hwnd, 34, ref noBorder, sizeof(int));
            MakeTransparent(hwnd);
            error = null;
            return new PlayOverlay(w, hwnd);
        }
        catch (Exception e)
        {
            error = e.Message;
            return null;
        }
    }

    public void SetBounds(RectInt32 screen)
    {
        if (screen.Width <= 0 || screen.Height <= 0 || screen.Equals(_bounds)) return;
        _bounds = screen;
        Window.AppWindow.MoveAndResize(screen);
    }

    public bool Visible => _visible;

    private bool _activatedOnce;

    public void Show(bool activate)
    {
        _visible = true;
        // The first time, Activate() so that WinUI starts rendering the window's content.
        if (!_activatedOnce) { _activatedOnce = true; Window.Activate(); }
        else Window.AppWindow.Show(activate);
        if (activate) Win32.SetForegroundWindow(Handle);
    }

    public void Hide()
    {
        _visible = false;
        Window.AppWindow.Hide();
    }

    public void Close()
    {
        try { Window.Close(); } catch (Exception) { }
    }

    // Without these, DWM composes the window's redirection surface as opaque and GDI erases it
    // white: per-pixel alpha needs blur-behind (with an empty, off-screen blur region so nothing
    // is actually blurred) and a background erase that paints alpha 0 (black).
    private static readonly SubclassProc s_subclass = Subclass;

    private static void MakeTransparent(IntPtr hwnd)
    {
        var region = CreateRectRgn(-2, -2, -1, -1);
        var blur = new DWM_BLURBEHIND { dwFlags = 0x1 | 0x2 /* ENABLE | BLURREGION */, fEnable = true, hRgnBlur = region };
        DwmEnableBlurBehindWindow(hwnd, ref blur);
        DeleteObject(region);
        SetWindowSubclass(hwnd, s_subclass, (UIntPtr)0x4D534844, UIntPtr.Zero);
    }

    private static IntPtr Subclass(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, UIntPtr id, UIntPtr data)
    {
        const uint WM_ERASEBKGND = 0x0014;
        if (msg == WM_ERASEBKGND && GetClientRect(hwnd, out var rc))
        {
            FillRect(wParam, ref rc, GetStockObject(4 /* BLACK_BRUSH */));
            return (IntPtr)1;
        }
        return DefSubclassProc(hwnd, msg, wParam, lParam);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DWM_BLURBEHIND
    {
        public uint dwFlags;
        [MarshalAs(UnmanagedType.Bool)] public bool fEnable;
        public IntPtr hRgnBlur;
        [MarshalAs(UnmanagedType.Bool)] public bool fTransitionOnMaximized;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    private delegate IntPtr SubclassProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, UIntPtr id, UIntPtr data);

    [DllImport("dwmapi.dll")] private static extern int DwmEnableBlurBehindWindow(IntPtr hwnd, ref DWM_BLURBEHIND blur);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateRectRgn(int l, int t, int r, int b);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] private static extern IntPtr GetStockObject(int index);
    [DllImport("user32.dll")] private static extern int FillRect(IntPtr hdc, ref RECT rc, IntPtr brush);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hwnd, out RECT rc);
    [DllImport("comctl32.dll")] private static extern bool SetWindowSubclass(IntPtr hwnd, SubclassProc proc, UIntPtr id, UIntPtr data);
    [DllImport("comctl32.dll")] private static extern IntPtr DefSubclassProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    private const int GWLP_HWNDPARENT = -8;
    [DllImport("user32.dll")] private static extern IntPtr SetWindowLongPtrW(IntPtr hwnd, int index, IntPtr value);
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
}

/// <summary>
/// A fully transparent system backdrop (the window shows whatever is behind it, here the video).
/// It needs a Windows.UI.Composition compositor, which needs a Windows.System dispatcher queue on
/// the UI thread; WinUI only creates the Microsoft.UI one, so one is created here if missing.
/// </summary>
internal sealed class TransparentBackdrop : SystemBackdrop
{
    private static Windows.UI.Composition.Compositor? s_compositor;
    private static IntPtr s_controller;

    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop connectedTarget, XamlRoot xamlRoot)
    {
        base.OnTargetConnected(connectedTarget, xamlRoot);
        if (s_compositor is null)
        {
            if (Windows.System.DispatcherQueue.GetForCurrentThread() is null)
            {
                var options = new DispatcherQueueOptions { dwSize = Marshal.SizeOf<DispatcherQueueOptions>(), threadType = 2, apartmentType = 0 };
                CreateDispatcherQueueController(options, out s_controller);
            }
            s_compositor = new Windows.UI.Composition.Compositor();
        }
        connectedTarget.SystemBackdrop = s_compositor.CreateColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
    }

    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop disconnectedTarget)
    {
        disconnectedTarget.SystemBackdrop = null;
        base.OnTargetDisconnected(disconnectedTarget);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DispatcherQueueOptions
    {
        public int dwSize;
        public int threadType;    // DQTYPE_THREAD_CURRENT = 2
        public int apartmentType; // DQTAT_COM_NONE = 0
    }

    [DllImport("CoreMessaging.dll")]
    private static extern int CreateDispatcherQueueController(DispatcherQueueOptions options, out IntPtr controller);
}
