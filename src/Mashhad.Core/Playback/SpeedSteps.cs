namespace Mashhad.Core.Playback;

public static class SpeedSteps
{
    public const double Min = 0.1, Max = 8.0;
    public static readonly double[] Presets = { 0.25, 0.5, 0.75, 1.0, 1.25, 1.5, 1.75, 2.0, 3.0, 4.0 };

    public static double Clamp(double speed) => Math.Clamp(speed, Min, Max);

    /// <summary>mpv-style multiplicative step ("[" and "]").</summary>
    public static double Multiply(double speed, double factor) => Clamp(Math.Round(speed * factor, 3));

    /// <summary>Next preset above/below the current speed.</summary>
    public static double StepPreset(double speed, int direction)
    {
        const double eps = 1e-6;
        if (direction > 0)
        {
            foreach (var p in Presets) if (p > speed + eps) return p;
            return Presets[^1];
        }
        for (int i = Presets.Length - 1; i >= 0; i--) if (Presets[i] < speed - eps) return Presets[i];
        return Presets[0];
    }
}
