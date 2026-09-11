using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Stay.Core;

namespace Stay;

/// <summary>
/// This PC, read from Windows itself. Every answer is wrapped, because the whole point of the app is to run on
/// machines in odd states: no TPM, no WMI, no rights. A check that throws tells you nothing; a check that says
/// "could not be read" tells you what to do next.
/// </summary>
public sealed class WinMachine : IMachine
{
    private const string CurrentVersion = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";

    public WindowsBuild Windows()
    {
        // A test hook, not a feature: it makes this PC report itself as Windows 10 22H2 so the Windows 10 paths
        // can be driven end to end — including real registry writes and real undo — on a machine that is not one.
        // Everything else still comes off the real PC. It is deliberately not in the app's help.
        if (Environment.GetEnvironmentVariable("STAY_PRETEND_WINDOWS10") == "1")
            return new WindowsBuild("Microsoft Windows 10 Pro", "Professional", "22H2", 10, 19045,
                                    Revision: 0, Architecture: RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant());

        using var k = Registry.LocalMachine.OpenSubKey(CurrentVersion);
        string Text(string name) => k?.GetValue(name)?.ToString() ?? "";
        int Number(string name) => k?.GetValue(name) is int i ? i : 0;

        int build = int.TryParse(Text("CurrentBuildNumber"), out var b) ? b : 0;
        // DisplayVersion is "22H2" on anything recent; ReleaseId is the old name and still there on older builds.
        string release = Text("DisplayVersion");
        if (release.Length == 0) release = Text("ReleaseId");

        return new WindowsBuild(
            Caption: Text("ProductName"),
            Edition: Text("EditionID"),
            Release: release,
            Major: build >= 10000 ? 10 : 0,
            Build: build,
            Revision: Number("UBR"),
            Architecture: RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant());
    }

    /// <summary>
    /// Every licence Windows holds, which is where an ESU enrolment actually shows up. Windows describes ESU as
    /// an add-on product with its own licence, so an active one is the thing that makes updates arrive.
    /// </summary>
    public IReadOnlyList<(string Name, string Description, bool Active)> Licences()
    {
        var list = new List<(string, string, bool)>();
        try
        {
            using var search = new ManagementObjectSearcher(
                "SELECT Name, Description, LicenseStatus FROM SoftwareLicensingProduct WHERE PartialProductKey IS NOT NULL");
            foreach (ManagementObject o in search.Get())
            {
                var name = o["Name"]?.ToString() ?? "";
                var description = o["Description"]?.ToString() ?? "";
                bool active = o["LicenseStatus"] is uint status && status == 1;
                list.Add((name, description, active));
            }
        }
        catch { /* no rights, or the licensing service is not answering: an empty list means "unknown", not "no" */ }
        return list;
    }

    public (bool Enabled, string Version)? Tpm()
    {
        try
        {
            using var search = new ManagementObjectSearcher(
                new ManagementScope(@"\\.\root\CIMV2\Security\MicrosoftTpm"),
                new ObjectQuery("SELECT * FROM Win32_Tpm"));
            foreach (ManagementObject o in search.Get())
            {
                bool enabled = o["IsEnabled_InitialValue"] is bool on && on;
                // SpecVersion arrives as "2.0, 0, 1.38"; the part before the first comma is the one that matters.
                var spec = (o["SpecVersion"]?.ToString() ?? "").Split(',')[0].Trim();
                return (enabled, spec.Length > 0 ? spec : "unknown");
            }
        }
        catch { /* no TPM namespace at all, which on a PC of this age usually means no TPM */ }
        return null;
    }

    public bool? SecureBootEnabled()
    {
        try
        {
            using var k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\SecureBoot\State");
            if (k?.GetValue("UEFISecureBootEnabled") is int v) return v == 1;
        }
        catch { }
        return null;   // the key is absent on a PC that boots the old way
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFirmwareType(out uint type);

    public bool UefiBoot()
    {
        try { if (GetFirmwareType(out uint type)) return type == 2; }  // 1 BIOS, 2 UEFI
        catch { }
        // Fall back to the Secure Boot key: it only exists on UEFI machines.
        return Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\SecureBoot\State") is not null;
    }

    public (string Name, string Manufacturer, int Family, int Model, int Cores, double GHz) Cpu()
    {
        try
        {
            using var k = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            var name = k?.GetValue("ProcessorNameString")?.ToString() ?? "";
            var maker = k?.GetValue("VendorIdentifier")?.ToString() ?? "";
            double ghz = k?.GetValue("~MHz") is int mhz ? mhz / 1000.0 : 0;
            return (name, maker, 0, 0, Environment.ProcessorCount, ghz);
        }
        catch { return ("", "", 0, 0, Environment.ProcessorCount, 0); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint Length, MemoryLoad;
        public ulong TotalPhys, AvailPhys, TotalPageFile, AvailPageFile, TotalVirtual, AvailVirtual, AvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);

    public long MemoryBytes()
    {
        var status = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        return GlobalMemoryStatusEx(ref status) ? (long)status.TotalPhys : 0;
    }

    public long SystemDiskBytes()
    {
        try
        {
            var root = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)) ?? "C:\\";
            return new DriveInfo(root).TotalSize;
        }
        catch { return 0; }
    }

    public bool Installed(string what)
    {
        if (what == "third-party antivirus") return OtherAntivirus();

        foreach (var (hive, key) in new[]
        {
            (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
            (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
            (Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
        })
        {
            try
            {
                using var root = hive.OpenSubKey(key);
                if (root is null) continue;
                foreach (var name in root.GetSubKeyNames())
                {
                    using var entry = root.OpenSubKey(name);
                    var display = entry?.GetValue("DisplayName")?.ToString() ?? "";
                    if (display.Contains(what, StringComparison.OrdinalIgnoreCase)) return true;
                }
            }
            catch { }
        }
        return false;
    }

    /// <summary>Whether something other than Defender is the antivirus, since Defender's own switches do nothing then.</summary>
    private static bool OtherAntivirus()
    {
        try
        {
            using var search = new ManagementObjectSearcher(
                new ManagementScope(@"\\.\root\SecurityCenter2"),
                new ObjectQuery("SELECT displayName FROM AntiVirusProduct"));
            foreach (ManagementObject o in search.Get())
            {
                var name = o["displayName"]?.ToString() ?? "";
                if (name.Length > 0 && !name.Contains("Defender", StringComparison.OrdinalIgnoreCase)) return true;
            }
        }
        catch { }
        return false;
    }

    /// <summary>A real printer, not one of the writers Windows installs for everybody.</summary>
    /// <summary>
    /// The browsers on this PC, read from the version each one writes down for its own updater, and which of
    /// them Windows opens links with.
    /// </summary>
    public IReadOnlyList<Browser> Browsers()
    {
        var chosen = DefaultBrowser() ?? "";
        var found = new List<Browser>();

        void Look(string name, string[] keys, string value)
        {
            foreach (var key in keys)
                foreach (var root in new[] { Registry.LocalMachine, Registry.CurrentUser })
                {
                    try
                    {
                        using var k = root.OpenSubKey(key);
                        var version = k?.GetValue(value)?.ToString();
                        if (string.IsNullOrWhiteSpace(version)) continue;
                        var stem = name.Split(' ')[1];
                        found.Add(new Browser(name, version, chosen.Contains(stem, StringComparison.OrdinalIgnoreCase)));
                        return;
                    }
                    catch { }
                }
        }

        Look("Microsoft Edge", new[] { @"SOFTWARE\WOW6432Node\Microsoft\Edge\BLBeacon", @"Software\Microsoft\Edge\BLBeacon" }, "version");
        Look("Google Chrome", new[] { @"SOFTWARE\WOW6432Node\Google\Chrome\BLBeacon", @"Software\Google\Chrome\BLBeacon" }, "version");
        Look("Mozilla Firefox", new[] { @"SOFTWARE\Mozilla\Mozilla Firefox", @"SOFTWARE\WOW6432Node\Mozilla\Mozilla Firefox" }, "CurrentVersion");

        return found.OrderByDescending(b => b.Default).ToList();
    }

    /// <summary>What Windows opens an https link with, as the person chose it.</summary>
    private static string? DefaultBrowser()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\Shell\Associations\UrlAssociations\https\UserChoice");
            return k?.GetValue("ProgId")?.ToString();
        }
        catch { return null; }
    }

    public bool HasPrinter()
    {
        var builtIn = new[] { "Microsoft Print to PDF", "Microsoft XPS Document Writer", "Fax", "OneNote" };
        try
        {
            using var k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Print\Printers");
            if (k is null) return false;
            return k.GetSubKeyNames().Any(name => !builtIn.Any(b => name.Contains(b, StringComparison.OrdinalIgnoreCase)));
        }
        catch { return true; }   // cannot tell: do not suggest turning printing off
    }
}
