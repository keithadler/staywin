using System.Management;
using Microsoft.Win32;
using Stay.Core;

namespace Stay;

/// <summary>
/// What actually reached this PC, and what is watching it.
///
/// Every read here is wrapped and every failure means "could not tell" rather than "no". An app that cries wolf
/// about a firewall because it could not read a registry key is worse than one that says nothing.
/// </summary>
public sealed class WinProtection : IProtection
{
    private const string AutoUpdate = @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update";

    /// <summary>
    /// The day the newest update really installed, from the list of fixes Windows keeps about itself. It is the
    /// one record that cannot be argued with: a fix is either on the PC or it is not.
    /// </summary>
    public DateOnly? LastUpdate()
    {
        DateOnly? newest = null;
        try
        {
            using var search = new ManagementObjectSearcher("SELECT InstalledOn FROM Win32_QuickFixEngineering");
            foreach (ManagementObject fix in search.Get())
            {
                var text = fix["InstalledOn"]?.ToString();
                if (string.IsNullOrWhiteSpace(text)) continue;
                if (!DateTime.TryParse(text, out var when)) continue;
                var day = DateOnly.FromDateTime(when);
                if (newest is null || day > newest) newest = day;
            }
        }
        catch { }
        return newest;
    }

    /// <summary>
    /// A restart waiting. Windows records this in three different places depending on what asked for it, and
    /// any one of them counts.
    /// </summary>
    public bool RestartPending()
    {
        try
        {
            if (Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending") is not null) return true;
            if (Registry.LocalMachine.OpenSubKey(AutoUpdate + @"\RebootRequired") is not null) return true;

            using var session = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager");
            if (session?.GetValue("PendingFileRenameOperations") is string[] { Length: > 0 }) return true;
        }
        catch { }
        return false;
    }

    /// <summary>Whether the Windows Update service could run. Disabled is 4; anything else is some flavour of on.</summary>
    public bool UpdatesReachable()
    {
        try
        {
            using var k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\wuauserv");
            return k?.GetValue("Start") is not int start || start != 4;
        }
        catch { return true; }
    }

    public DateTimeOffset? LastChecked()
    {
        try
        {
            using var k = Registry.LocalMachine.OpenSubKey(AutoUpdate + @"\Results\Detect");
            if (k?.GetValue("LastSuccessTime") is string text && DateTime.TryParse(text, out var when))
                return new DateTimeOffset(when, TimeSpan.Zero);
        }
        catch { }
        return null;
    }

    public Guarded Guarded()
    {
        var (name, other) = WhoIsWatching();

        // Defender answers about itself properly. A third-party antivirus only tells the Security Centre whether
        // it is on, so for one of those the honest answer is what the Security Centre was told.
        if (other is null)
        {
            try
            {
                using var search = new ManagementObjectSearcher(
                    new ManagementScope(@"\\.\root\Microsoft\Windows\Defender"),
                    new ObjectQuery("SELECT * FROM MSFT_MpComputerStatus"));
                foreach (ManagementObject status in search.Get())
                {
                    bool on = status["AntivirusEnabled"] is bool a && a;
                    bool live = status["RealTimeProtectionEnabled"] is bool r && r;
                    int? age = status["AntivirusSignatureAge"] is uint days ? (int)days : null;
                    return new Guarded("Microsoft Defender", on, live, age, FirewallOn());
                }
            }
            catch { }
        }

        return new Guarded(name, other?.On ?? true, other?.On ?? true, null, FirewallOn());
    }

    /// <summary>The antivirus the Security Centre knows about, and whether it says it is on.</summary>
    private static (string Name, (bool On, bool UpToDate)? Other) WhoIsWatching()
    {
        try
        {
            using var search = new ManagementObjectSearcher(
                new ManagementScope(@"\\.\root\SecurityCenter2"),
                new ObjectQuery("SELECT displayName, productState FROM AntiVirusProduct"));
            foreach (ManagementObject product in search.Get())
            {
                var name = product["displayName"]?.ToString() ?? "";
                if (name.Length == 0 || name.Contains("Defender", StringComparison.OrdinalIgnoreCase)) continue;

                // productState packs three things into one number; the middle byte says whether it is watching.
                int state = Convert.ToInt32(product["productState"] ?? 0);
                bool on = ((state >> 12) & 0xF) is 1 or 0x1;
                bool current = ((state >> 4) & 0xFF) == 0;
                return (name, (on, current));
            }
        }
        catch { }
        return ("Microsoft Defender", null);
    }

    /// <summary>The firewall, which has three profiles and is only on when it is on for all of them.</summary>
    private static bool FirewallOn()
    {
        try
        {
            bool all = true;
            foreach (var profile in new[] { "DomainProfile", "StandardProfile", "PublicProfile" })
            {
                using var k = Registry.LocalMachine.OpenSubKey(
                    $@"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\{profile}");
                if (k?.GetValue("EnableFirewall") is int on && on == 0) all = false;
            }
            return all;
        }
        catch { return true; }
    }
}
