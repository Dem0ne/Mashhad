using System.Text;

namespace Mashhad.Editing;

public sealed record ExportProgress(int Step, int StepCount, string Description, double Fraction)
{
    /// <summary>Overall progress 0..1 across all steps.</summary>
    public double Overall => StepCount == 0 ? 0 : Math.Clamp((Step + Fraction) / StepCount, 0, 1);
}

/// <summary>Executes an <see cref="ExportPlan"/>, cleaning up partial output on failure or cancellation.</summary>
public sealed class Exporter
{
    private readonly FfmpegRunner _runner;
    public Exporter(FfmpegRunner runner) => _runner = runner;

    public async Task<IReadOnlyList<string>> RunAsync(ExportPlan plan, IProgress<ExportProgress>? progress = null,
        CancellationToken ct = default)
    {
        var produced = new List<string>();
        try
        {
            foreach (var step in plan.Steps)
            {
                var dir = Path.GetDirectoryName(step.Output);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            }
            if (plan.ConcatListPath is not null)
                await File.WriteAllTextAsync(plan.ConcatListPath, plan.ConcatListContent, new UTF8Encoding(false), ct).ConfigureAwait(false);

            for (int i = 0; i < plan.Steps.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var step = plan.Steps[i];
                int index = i;
                progress?.Report(new ExportProgress(index, plan.Steps.Count, step.Description, 0));
                var stepProgress = progress is null
                    ? null
                    : new SyncProgress(f => progress.Report(new ExportProgress(index, plan.Steps.Count, step.Description, f)));
                produced.Add(step.Output);
                await _runner.RunAsync(step.Arguments, step.Duration, stepProgress, ct).ConfigureAwait(false);
            }
        }
        catch
        {
            foreach (var f in produced) TryDelete(f); // never leave truncated files behind
            throw;
        }
        finally
        {
            if (plan.ConcatListPath is not null) TryDelete(plan.ConcatListPath);
        }

        foreach (var f in plan.TemporaryFiles) TryDelete(f);
        return plan.Outputs;
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>IProgress that invokes inline (Progress&lt;T&gt; would post to a captured context and reorder).</summary>
    private sealed class SyncProgress : IProgress<double>
    {
        private readonly Action<double> _action;
        public SyncProgress(Action<double> action) => _action = action;
        public void Report(double value) => _action(value);
    }
}
