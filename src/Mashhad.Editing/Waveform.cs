namespace Mashhad.Editing;

/// <summary>
/// Audio peaks for the editor's waveform lane. FFmpeg decodes the first audio track to mono
/// 16-bit PCM at a low sample rate on stdout; peaks are folded into a fixed number of buckets
/// while streaming, so memory stays constant for any file length.
/// </summary>
public static class Waveform
{
    public const int SampleRate = 4000;

    public static List<string> Args(string input) => new()
    {
        "-hide_banner", "-nostdin", "-v", "error", "-i", input, "-map", "0:a:0", "-vn", "-sn", "-dn",
        "-ac", "1", "-ar", SampleRate.ToString(System.Globalization.CultureInfo.InvariantCulture), "-f", "s16le", "-acodec", "pcm_s16le", "pipe:1",
    };

    /// <summary>
    /// Folds a stream of s16le samples into <paramref name="buckets"/> peak values in 0..1.
    /// <paramref name="expectedSamples"/> (duration × rate) maps samples to buckets; extra samples
    /// land in the last bucket.
    /// </summary>
    public static async Task<float[]> ComputeAsync(Stream pcm, long expectedSamples, int buckets, CancellationToken ct = default)
    {
        var peaks = new float[Math.Max(1, buckets)];
        if (expectedSamples <= 0) expectedSamples = 1;
        var buffer = new byte[64 * 1024];
        long sample = 0;
        int carry = -1;
        while (true)
        {
            int n = await pcm.ReadAsync(buffer, ct).ConfigureAwait(false);
            if (n <= 0) break;
            int i = 0;
            if (carry >= 0)
            {
                Add((short)(carry | (buffer[0] << 8)));
                i = 1;
                carry = -1;
            }
            for (; i + 1 < n; i += 2) Add((short)(buffer[i] | (buffer[i + 1] << 8)));
            if (i < n) carry = buffer[i];
        }
        return peaks;

        void Add(short v)
        {
            int b = (int)Math.Min(peaks.Length - 1, sample * peaks.Length / expectedSamples);
            float a = Math.Abs(v / 32768f);
            if (a > peaks[b]) peaks[b] = a;
            sample++;
        }
    }
}
