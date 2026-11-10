using System.Globalization;
using Mashhad.Core.Settings;

namespace Mashhad.App;

/// <summary>Tiny append-only diagnostic log in %LOCALAPPDATA%\Mashhad.</summary>
internal static class Log
{
    private static readonly object Gate = new();
    /// <summary>Overridden by --smoke-log so CI can collect the output.</summary>
    public static string FilePath { get; set; } = AppPaths.LogFile;

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                var info = new FileInfo(FilePath);
                if (info.Exists && info.Length > 1_000_000) info.Delete();
                File.AppendAllText(FilePath,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ", CultureInfo.InvariantCulture) + message + Environment.NewLine);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}
