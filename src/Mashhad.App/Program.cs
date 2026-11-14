using System.Diagnostics;
using Mashhad.App.Native;
using Mashhad.Core.Settings;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace Mashhad.App;

public static class Program
{
    internal const string InstanceKey = "Mashhad.MainInstance";

    [STAThread]
    private static int Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        try
        {
            if (TryRedirectToRunningInstance(args)) return 0;
        }
        catch (Exception e)
        {
            // Single-instance hand-off is a convenience; never let it prevent startup.
            Log.Write("Single-instance redirection failed: " + e);
        }

        Application.Start(p =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App(args);
        });
        return Environment.ExitCode;
    }

    /// <summary>
    /// When another Mashhad is already running, hands the files over to it (so double-clicking
    /// a second file reuses the open window) and returns true.
    /// </summary>
    private static bool TryRedirectToRunningInstance(string[] args)
    {
        if (args.Any(a => a is "--new-window" or "--smoke-test")) return false;
        if (!new SettingsStore(AppPaths.SettingsFile).Current.SingleInstance) return false;

        var main = AppInstance.FindOrRegisterForKey(InstanceKey);
        if (main.IsCurrent) return false;

        var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
        var done = Win32.CreateEventW(IntPtr.Zero, true, false, null);
        Task.Run(() =>
        {
            try { main.RedirectActivationToAsync(activation).AsTask().Wait(TimeSpan.FromSeconds(10)); }
            catch (Exception e) { Log.Write("RedirectActivationToAsync: " + e); }
            finally { Win32.SetEvent(done); }
        });
        // Wait on the STA without blocking COM calls.
        Win32.CoWaitForMultipleObjects(0, 15_000, 1, new[] { done }, out _);

        try
        {
            using var process = Process.GetProcessById((int)main.ProcessId);
            Win32.SetForegroundWindow(process.MainWindowHandle);
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException) { }
        return true;
    }
}
