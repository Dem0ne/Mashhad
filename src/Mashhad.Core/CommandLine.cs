using System.Text;

namespace Mashhad.Core;

public static class CommandLine
{
    /// <summary>
    /// Splits a Windows command line the way CommandLineToArgvW does for arguments
    /// (backslash-quote rules included). Used for activations redirected from a second instance.
    /// </summary>
    public static List<string> Split(string? commandLine)
    {
        var args = new List<string>();
        if (string.IsNullOrWhiteSpace(commandLine)) return args;

        var sb = new StringBuilder();
        bool inQuotes = false, hasToken = false;
        int i = 0;
        while (i < commandLine.Length)
        {
            char c = commandLine[i];
            if (c == '\\')
            {
                int slashes = 0;
                while (i < commandLine.Length && commandLine[i] == '\\') { slashes++; i++; }
                if (i < commandLine.Length && commandLine[i] == '"')
                {
                    sb.Append('\\', slashes / 2);
                    if (slashes % 2 == 1) { sb.Append('"'); i++; }
                }
                else
                {
                    sb.Append('\\', slashes);
                }
                hasToken = true;
                continue;
            }
            if (c == '"')
            {
                if (inQuotes && i + 1 < commandLine.Length && commandLine[i + 1] == '"')
                {
                    sb.Append('"');
                    i += 2;
                    continue;
                }
                inQuotes = !inQuotes;
                hasToken = true;
                i++;
                continue;
            }
            if (!inQuotes && (c == ' ' || c == '\t'))
            {
                if (hasToken) { args.Add(sb.ToString()); sb.Clear(); hasToken = false; }
                i++;
                continue;
            }
            sb.Append(c);
            hasToken = true;
            i++;
        }
        if (hasToken) args.Add(sb.ToString());
        return args;
    }

    /// <summary>Keeps the arguments that are things we can open; drops our own executable and switches.</summary>
    public static List<string> MediaArguments(IEnumerable<string> args)
    {
        var result = new List<string>();
        foreach (var a in args)
        {
            if (string.IsNullOrWhiteSpace(a) || a.StartsWith("--", StringComparison.Ordinal)) continue;
            var ext = Path.GetExtension(a);
            if (ext.Equals(".exe", StringComparison.OrdinalIgnoreCase) || ext.Equals(".dll", StringComparison.OrdinalIgnoreCase)) continue;
            result.Add(a);
        }
        return result;
    }
}
