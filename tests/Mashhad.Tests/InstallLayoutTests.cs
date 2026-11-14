using System.Text.Json;
using Mashhad.Core.Install;
using Xunit;

namespace Mashhad.Tests;

public class InstallLayoutTests
{
    [Fact]
    public void Lives_in_its_own_folder_under_the_BitSync_family_folder()
    {
        var pf = Path.Combine(Path.GetTempPath(), "pf");
        Assert.Equal(Path.Combine(pf, "BitSync"), InstallLayout.FamilyDirectory(pf));
        Assert.Equal(Path.Combine(pf, "BitSync", "Mashhad"), InstallLayout.InstallDirectory(pf));
        Assert.Equal(Path.Combine(pf, "BitSync", "Mashhad", "mashhad-install.json"), InstallLayout.MarkerPath(InstallLayout.InstallDirectory(pf)));
        Assert.Equal("BitSync.Mashhad", InstallLayout.UninstallKeyName);
        Assert.Equal(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\BitSync.Mashhad", InstallLayout.UninstallKeyPath);
        Assert.Equal(Path.Combine("progs", "BitSync", "Mashhad.lnk"), InstallLayout.ShortcutPath("progs"));
        // Default comes from the Program Files known folder, not a hard-coded drive.
        Assert.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), InstallLayout.InstallDirectory());
    }

    [Fact]
    public void Only_its_own_folder_may_be_deleted_whole()
    {
        var pf = Path.Combine(Path.GetTempPath(), "pf");
        Assert.True(InstallLayout.IsOwnInstallDirectory(Path.Combine(pf, "BitSync", "Mashhad")));
        Assert.True(InstallLayout.IsOwnInstallDirectory(Path.Combine(pf, "bitsync", "MASHHAD") + Path.DirectorySeparatorChar));
        Assert.False(InstallLayout.IsOwnInstallDirectory(Path.Combine(pf, "BitSync")));
        Assert.False(InstallLayout.IsOwnInstallDirectory(Path.Combine(pf, "BitSync", "Wazn")));
        Assert.False(InstallLayout.IsOwnInstallDirectory(Path.Combine(pf, "Mashhad")));
        Assert.False(InstallLayout.IsOwnInstallDirectory(Path.Combine(pf, "Other", "Mashhad")));
        Assert.False(InstallLayout.IsOwnInstallDirectory(""));
    }

    [Fact]
    public void Shared_folder_is_removed_only_when_empty()
    {
        var root = Path.Combine(Path.GetTempPath(), "mashhad-layout-" + Guid.NewGuid().ToString("N"));
        var family = InstallLayout.FamilyDirectory(root);
        try
        {
            Directory.CreateDirectory(Path.Combine(family, "Wazn"));
            File.WriteAllText(Path.Combine(family, "Wazn", "wazn-install.json"), "{}");
            Assert.False(InstallLayout.CanRemoveShared(family));
            Directory.Delete(Path.Combine(family, "Wazn"), recursive: true);
            Assert.True(InstallLayout.CanRemoveShared(family));
            Assert.False(InstallLayout.CanRemoveShared(Path.Combine(root, "missing")));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Marker_marks_an_installed_copy()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mashhad-marker-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            Assert.False(InstallLayout.IsInstalled(dir)); // portable copy
            // The exact shape the installer writes (installer/Mashhad.iss, CurStepChanged).
            File.WriteAllText(InstallLayout.MarkerPath(dir), """
                {
                  "family": "BitSync",
                  "product": "Mashhad",
                  "version": "0.1.0",
                  "installDir": "C:\\Program Files\\BitSync\\Mashhad",
                  "uninstallKey": "HKLM\\Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\BitSync.Mashhad",
                  "installedAt": "2026-11-10 10:00:00"
                }
                """);
            var marker = InstallLayout.ReadMarker(dir);
            Assert.NotNull(marker);
            Assert.Equal(@"C:\Program Files\BitSync\Mashhad", marker!.InstallDir);
            Assert.EndsWith(InstallLayout.UninstallKeyPath, marker.UninstallKey);
            Assert.True(InstallLayout.IsInstalled(dir));

            File.WriteAllText(InstallLayout.MarkerPath(dir), JsonSerializer.Serialize(new { family = "BitSync", product = "Wazn" }));
            Assert.Null(InstallLayout.ReadMarker(dir)); // another app's marker
            File.WriteAllText(InstallLayout.MarkerPath(dir), "{ not json");
            Assert.Null(InstallLayout.ReadMarker(dir));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Installer_script_follows_the_layout()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Mashhad.sln"))) dir = dir.Parent;
        var iss = File.ReadAllText(Path.Combine(dir!.FullName, "installer", "Mashhad.iss"));

        Assert.Contains($"#define Family \"{InstallLayout.Family}\"", iss);
        Assert.Contains($"#define ProductFolder \"{InstallLayout.ProductFolder}\"", iss);
        Assert.Contains($"#define UninstallKey \"{InstallLayout.UninstallKeyPath}\"", iss);
        Assert.Contains(@"DefaultDirName={commonpf64}\{#Family}\{#ProductFolder}", iss);
        Assert.Contains("DefaultGroupName={#Family}", iss);
        Assert.Contains(@"Name: ""{group}\Mashhad""", iss);
        Assert.Contains("PrivilegesRequired=admin", iss);
        Assert.Contains("CreateUninstallRegKey=no", iss);
        Assert.Contains(@"Root: HKLM; Subkey: ""{#UninstallKey}""; Flags: uninsdeletekey", iss);
        Assert.Contains($@"{{app}}\{InstallLayout.MarkerFileName}", iss);
        Assert.Contains(@"RemoveDir(ExpandConstant('{commonpf64}\{#Family}'))", iss);
        Assert.Contains(@"RemoveDir(ExpandConstant('{commonprograms}\{#Family}'))", iss);
        Assert.Contains("if IsOwnInstallDir(AppDir)", iss);
        // No per-user or fixed-drive locations, and nothing deletes the shared folder recursively.
        Assert.DoesNotContain("{autopf}", iss);
        Assert.DoesNotContain(@"C:\", iss);
        Assert.DoesNotContain(@"DelTree(ExpandConstant('{commonpf64}", iss);
    }
}
