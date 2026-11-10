using Microsoft.UI.Dispatching;

namespace Mashhad.App;

/// <summary>
/// Frame-stepped animations on the UI thread, used where XAML animations cannot help: moving the
/// native video window between layouts, and the connected "tile ⇄ full window" expansion.
/// </summary>
internal sealed class Animator
{
    private readonly DispatcherQueue _ui;
    private DispatcherQueueTimer? _timer;

    public Animator(DispatcherQueue ui) => _ui = ui;

    public bool IsRunning => _timer is not null;

    /// <summary>Fluent "fast out, slow in" (cubic-bezier 0.1, 0.9, 0.2, 1) approximated in closed form.</summary>
    public static double Decelerate(double t) => 1 - Math.Pow(1 - Math.Clamp(t, 0, 1), 3.2);

    public void Run(int milliseconds, Action<double> step, Action? done = null)
    {
        Stop();
        var start = Environment.TickCount64;
        var timer = _ui.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(15);
        timer.IsRepeating = true;
        timer.Tick += (_, _) =>
        {
            double t = Math.Min(1, (Environment.TickCount64 - start) / (double)Math.Max(1, milliseconds));
            try { step(Decelerate(t)); }
            catch (Exception e) { Log.Write("Animation step: " + e.Message); t = 1; }
            if (t >= 1)
            {
                timer.Stop();
                if (ReferenceEquals(_timer, timer)) _timer = null;
                try { done?.Invoke(); } catch (Exception e) { Log.Write("Animation done: " + e); }
            }
        };
        _timer = timer;
        step(0);
        timer.Start();
    }

    public void Stop()
    {
        _timer?.Stop();
        _timer = null;
    }

    public static double Lerp(double a, double b, double t) => a + (b - a) * t;
}
