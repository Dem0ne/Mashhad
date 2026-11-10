using Mashhad.Core;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Activation;

namespace Mashhad.App;

public partial class App : Application
{
    private readonly string[] _args;
    private MainWindow? _window;
    private DispatcherQueue? _dispatcher;

    public App(string[] args)
    {
        _args = args;
        int logIndex = Array.IndexOf(args, "--smoke-log");
        if (logIndex >= 0 && logIndex + 1 < args.Length) Log.FilePath = Path.GetFullPath(args[logIndex + 1]);

        UnhandledException += (_, e) =>
        {
            Log.Write("Unhandled UI exception: " + e.Exception);
            if (IsSmokeTest) { Log.Write("SMOKE RESULT: FAIL (unhandled exception)"); Log.Flush(); Environment.Exit(2); }
            e.Handled = true; // keep the player alive; the error is in the log
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Write("Unhandled exception: " + e.ExceptionObject);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Write("Unobserved task exception: " + e.Exception);
            e.SetObserved();
        };

        InitializeComponent();
    }

    private bool IsSmokeTest => _args.Contains("--smoke-test");

    protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        try
        {
            _dispatcher = DispatcherQueue.GetForCurrentThread();
            await Fonts.AppFonts.InitializeAsync(); // before any window, so every control picks up the UI font
            var files = CommandLine.MediaArguments(_args.Where((a, i) => i == 0 || _args[i - 1] != "--smoke-log"));
            _window = new MainWindow(files, IsSmokeTest);
            _window.Activate();

            // Files opened later (double-click in Explorer, "Open with") arrive from the second process.
            try { AppInstance.GetCurrent().Activated += OnRedirectedActivation; }
            catch (Exception e) { Log.Write("Activation hook failed: " + e.Message); }
        }
        catch (Exception e)
        {
            Log.Write("Startup failed: " + e);
            if (IsSmokeTest) Log.Write("SMOKE RESULT: FAIL (startup)");
            Log.Flush();
            Environment.Exit(3);
        }
    }

    private void OnRedirectedActivation(object? sender, AppActivationArguments e)
    {
        var files = new List<string>();
        if (e.Kind == ExtendedActivationKind.Launch && e.Data is ILaunchActivatedEventArgs launch)
        {
            files = CommandLine.MediaArguments(CommandLine.Split(launch.Arguments))
                .Where(p => File.Exists(p) || Directory.Exists(p)).ToList();
        }
        else if (e.Kind == ExtendedActivationKind.File && e.Data is IFileActivatedEventArgs fileArgs)
        {
            files = fileArgs.Files.Select(f => f.Path).Where(p => !string.IsNullOrEmpty(p)).ToList();
        }
        _dispatcher?.TryEnqueue(() => _window?.OpenFromActivation(files));
    }
}
