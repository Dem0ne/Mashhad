using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mashhad.Core.Install;

/// <summary>
/// Where Mashhad lives on disk, following the BitSync family layout shared with WAZN, Hawas and
/// Khatwat: every BitSync program has its own folder under «Program Files\BitSync», its own
/// install marker and its own uninstall key, and a Start-menu shortcut in the shared «BitSync»
/// group. Uninstalling removes only Mashhad's own folders; the shared BitSync folder and group
/// are removed only when they are empty, so other BitSync apps are never touched.
/// installer/Mashhad.iss implements this; a unit test keeps the two in sync.
/// </summary>
public static class InstallLayout
{
    public const string Family = "BitSync";
    public const string ProductFolder = "Mashhad";
    public const string ProductName = "Mashhad";
    public const string MarkerFileName = "mashhad-install.json";
    /// <summary>Key name under HKLM\Software\Microsoft\Windows\CurrentVersion\Uninstall.</summary>
    public const string UninstallKeyName = Family + "." + ProductFolder;
    public const string UninstallKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + UninstallKeyName;
    /// <summary>Start-menu group shared by all BitSync apps (under the all-users Programs folder).</summary>
    public const string StartMenuGroup = Family;
    public const string ShortcutName = ProductName + ".lnk";

    /// <summary>The Program Files folder from the known folder (never a fixed drive letter).</summary>
    public static string ProgramFiles => Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

    /// <summary>«Program Files\BitSync».</summary>
    public static string FamilyDirectory(string? programFiles = null) => Path.Combine(programFiles ?? ProgramFiles, Family);

    /// <summary>«Program Files\BitSync\Mashhad».</summary>
    public static string InstallDirectory(string? programFiles = null) => Path.Combine(FamilyDirectory(programFiles), ProductFolder);

    public static string MarkerPath(string installDirectory) => Path.Combine(installDirectory, MarkerFileName);

    /// <summary>The shortcut in the shared group under the all-users Start menu «Programs» folder.</summary>
    public static string ShortcutPath(string? commonPrograms = null) =>
        Path.Combine(commonPrograms ?? Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), StartMenuGroup, ShortcutName);

    /// <summary>
    /// True only for Mashhad's own install folder («…\BitSync\Mashhad»): the one folder an
    /// uninstall may delete as a whole. Never the family folder or a sibling app's folder.
    /// </summary>
    public static bool IsOwnInstallDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var parent = Path.GetDirectoryName(full);
        return string.Equals(Path.GetFileName(full), ProductFolder, StringComparison.OrdinalIgnoreCase)
            && parent is not null
            && string.Equals(Path.GetFileName(parent), Family, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// After Mashhad's folder is gone: the shared folder (or Start-menu group) may be removed only
    /// when nothing else is left in it.
    /// </summary>
    public static bool CanRemoveShared(string sharedDirectory) =>
        Directory.Exists(sharedDirectory) && !Directory.EnumerateFileSystemEntries(sharedDirectory).Any();

    /// <summary>Reads the marker the installer writes; null for a portable copy or an unreadable file.</summary>
    public static InstallMarker? ReadMarker(string installDirectory)
    {
        try
        {
            var path = MarkerPath(installDirectory);
            if (!File.Exists(path)) return null;
            var marker = JsonSerializer.Deserialize<InstallMarker>(File.ReadAllText(path), JsonOptions);
            return marker is { Family: Family, Product: ProductFolder } ? marker : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }

    /// <summary>True when the running copy was installed by the installer (not unpacked from the zip).</summary>
    public static bool IsInstalled(string appDirectory) => ReadMarker(appDirectory) is not null;

    internal static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
}

/// <summary>Contents of mashhad-install.json.</summary>
public sealed record InstallMarker(
    [property: JsonPropertyName("family")] string Family,
    [property: JsonPropertyName("product")] string Product,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("installDir")] string InstallDir,
    [property: JsonPropertyName("uninstallKey")] string UninstallKey,
    [property: JsonPropertyName("installedAt")] string InstalledAt);
