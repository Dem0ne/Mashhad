using System.Runtime.InteropServices;
using static Mashhad.App.Native.Win32;

namespace Mashhad.App.Native;

internal enum HostButton { Left, Right, Middle, Back, Forward }

/// <summary>
/// A plain Win32 child window that libmpv renders into (via <c>--wid</c>). WinUI 3 has no native
/// child-window control, so the host is positioned over a placeholder element and kept in sync
/// with its layout. mpv's own inner window is disabled for input, which makes Windows deliver
/// all mouse messages here; keyboard focus never leaves the XAML content.
/// </summary>
internal sealed class VideoHost : IDisposable
{
    private const string ClassName = "MashhadVideoHost";
    private const uint WM_DISABLE_CHILDREN = WM_APP + 1;

    // Keep the delegate alive for the lifetime of the process: the window class points at it.
    private static readonly WndProc s_wndProc = StaticWndProc;
    private static readonly Dictionary<IntPtr, VideoHost> s_instances = new();
    private static bool s_registered;

    private (int X, int Y, int W, int H) _bounds = (-1, -1, -1, -1);
    private bool _visible;
    private bool _capturing;

    public IntPtr Handle { get; private set; }
    public int Width => Math.Max(0, _bounds.W);
    public int Height => Math.Max(0, _bounds.H);
    /// <summary>When set, the pointer is hidden while over the video (fullscreen idle).</summary>
    public bool HideCursor { get; set; }

    public event Action<int, int>? MouseMove;
    public event Action<HostButton, int, int>? MouseDown;
    public event Action<HostButton, int, int>? MouseUp;
    public event Action<int, int>? DoubleClick;
    /// <summary>(notches, x, y, ctrl)</summary>
    public event Action<double, int, int, bool>? Wheel;
    public event Action<string[]>? FilesDropped;

    public VideoHost(IntPtr parent)
    {
        var instance = GetModuleHandleW(null);
        if (!s_registered)
        {
            var wc = new WNDCLASSEXW
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
                style = CS_DBLCLKS,
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(s_wndProc),
                hInstance = instance,
                hCursor = LoadCursorW(IntPtr.Zero, (IntPtr)IDC_ARROW),
                hbrBackground = GetStockObject(BLACK_BRUSH),
                lpszClassName = ClassName,
            };
            if (RegisterClassExW(ref wc) == 0)
                throw new InvalidOperationException("RegisterClassEx failed: " + Marshal.GetLastWin32Error());
            s_registered = true;
        }

        Handle = CreateWindowExW(WS_EX_ACCEPTFILES, ClassName, "", WS_CHILD | WS_CLIPCHILDREN | WS_CLIPSIBLINGS,
            0, 0, 16, 16, parent, IntPtr.Zero, instance, IntPtr.Zero);
        if (Handle == IntPtr.Zero)
            throw new InvalidOperationException("CreateWindowEx failed: " + Marshal.GetLastWin32Error());
        s_instances[Handle] = this;
    }

    /// <summary>Moves the host to the given rectangle (physical pixels, parent client coordinates).</summary>
    public void SetBounds(int x, int y, int width, int height)
    {
        if (Handle == IntPtr.Zero) return;
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        if (_bounds == (x, y, width, height)) return;
        _bounds = (x, y, width, height);
        // HWND_TOP keeps the video above the XAML island, which is a sibling child window.
        SetWindowPos(Handle, HWND_TOP, x, y, width, height, SWP_NOACTIVATE);
    }

    public void SetVisible(bool visible)
    {
        if (Handle == IntPtr.Zero || visible == _visible) return;
        _visible = visible;
        ShowWindow(Handle, visible ? SW_SHOWNA : SW_HIDE);
        if (visible) SetWindowPos(Handle, HWND_TOP, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    /// <summary>Disables mpv's inner window(s) so that mouse input falls through to this host.</summary>
    public void DisableChildInput()
    {
        if (Handle == IntPtr.Zero) return;
        EnumChildWindows(Handle, (child, _) =>
        {
            EnableWindow(child, false);
            return true;
        }, IntPtr.Zero);
    }

    private void ResizeChildren()
    {
        if (!GetClientRect(Handle, out var rc)) return;
        EnumChildWindows(Handle, (child, _) =>
        {
            SetWindowPos(child, IntPtr.Zero, 0, 0, rc.Right, rc.Bottom, SWP_NOZORDER | SWP_NOACTIVATE);
            return true;
        }, IntPtr.Zero);
    }

    private static IntPtr StaticWndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (s_instances.TryGetValue(hwnd, out var host))
        {
            try
            {
                if (host.HandleMessage(msg, wParam, lParam, out var result)) return result;
            }
            catch (Exception e)
            {
                // Never let a managed exception unwind into the window manager.
                Log.Write("VideoHost: " + e);
            }
        }
        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    /// <summary>Set by the smoke test to log the first occurrence of each window message.</summary>
    public static Action<string>? Trace;
    private readonly HashSet<uint> _seen = new();

    private bool HandleMessage(uint msg, IntPtr wParam, IntPtr lParam, out IntPtr result)
    {
        if (Trace is not null && _seen.Add(msg)) Trace($"host: first message 0x{msg:X4}");
        result = IntPtr.Zero;
        int x = LoWord(lParam), y = HiWord(lParam);
        switch (msg)
        {
            case WM_PARENTNOTIFY:
                // mpv created its window (on its own thread): disable it once that call has returned.
                if (LoWord(wParam) == WM_CREATE) PostMessageW(Handle, WM_DISABLE_CHILDREN, IntPtr.Zero, IntPtr.Zero);
                return false;
            case WM_DISABLE_CHILDREN:
                DisableChildInput();
                ResizeChildren();
                return true;
            case WM_SIZE:
                ResizeChildren();
                return false;
            case WM_SETCURSOR:
                if (HideCursor && LoWord(lParam) == HTCLIENT)
                {
                    SetCursor(IntPtr.Zero);
                    result = (IntPtr)1;
                    return true;
                }
                return false;
            case WM_MOUSEMOVE:
                MouseMove?.Invoke(x, y);
                return true;
            case WM_LBUTTONDOWN:
                SetCapture(Handle);
                _capturing = true;
                MouseDown?.Invoke(HostButton.Left, x, y);
                return true;
            case WM_LBUTTONUP:
                if (_capturing) { _capturing = false; ReleaseCapture(); }
                MouseUp?.Invoke(HostButton.Left, x, y);
                return true;
            case WM_CAPTURECHANGED:
                if (_capturing) { _capturing = false; MouseUp?.Invoke(HostButton.Left, int.MinValue, int.MinValue); }
                return false;
            case WM_LBUTTONDBLCLK:
                DoubleClick?.Invoke(x, y);
                return true;
            case WM_RBUTTONDOWN:
                MouseDown?.Invoke(HostButton.Right, x, y);
                return true;
            case WM_RBUTTONUP:
                MouseUp?.Invoke(HostButton.Right, x, y);
                return true;
            case WM_MBUTTONDOWN:
                MouseDown?.Invoke(HostButton.Middle, x, y);
                return true;
            case WM_XBUTTONDOWN:
                MouseDown?.Invoke(HiWord(wParam) == 1 ? HostButton.Back : HostButton.Forward, x, y);
                result = (IntPtr)1;
                return true;
            case WM_MOUSEWHEEL:
            {
                // Wheel coordinates are in screen space.
                var pt = new POINT { X = x, Y = y };
                ScreenToClient(Handle, ref pt);
                bool ctrl = (LoWord(wParam) & 0x0008) != 0;
                Wheel?.Invoke(HiWord(wParam) / 120.0, pt.X, pt.Y, ctrl);
                return true;
            }
            case WM_DROPFILES:
            {
                var files = new List<string>();
                try
                {
                    uint count = DragQueryFileW(wParam, 0xFFFFFFFF, null, 0);
                    for (uint i = 0; i < count; i++)
                    {
                        uint len = DragQueryFileW(wParam, i, null, 0);
                        var buffer = new char[len + 1];
                        DragQueryFileW(wParam, i, buffer, len + 1);
                        files.Add(new string(buffer, 0, (int)len));
                    }
                }
                finally { DragFinish(wParam); }
                if (files.Count > 0) FilesDropped?.Invoke(files.ToArray());
                return true;
            }
        }
        return false;
    }

    public void Dispose()
    {
        if (Handle == IntPtr.Zero) return;
        s_instances.Remove(Handle);
        DestroyWindow(Handle);
        Handle = IntPtr.Zero;
    }
}
