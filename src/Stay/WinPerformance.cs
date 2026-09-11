using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Management;
using Microsoft.Win32;
using Stay.Core;

namespace Stay;

/// <summary>
/// How this PC actually performs, read from what Windows already measured rather than guessed at.
///
/// Windows times its own start and writes it down, and it times the programs that hold that start up. Almost
/// nobody knows the log is there. It is the difference between telling somebody their PC is slow and telling
/// them which four programs cost them thirty-one seconds this morning.
/// </summary>
public sealed class WinPerformance : IPerformance
{
    private const string DiagnosticsLog = "Microsoft-Windows-Diagnostics-Performance/Operational";
    private const string Run = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public Boot? LastBoot()
    {
        try
        {
            // Event 100 is the boot performance record. The newest one is the last time this PC started.
            var query = new EventLogQuery(DiagnosticsLog, PathType.LogName, "*[System[EventID=100]]")
            { ReverseDirection = true };
            using var reader = new EventLogReader(query);
            using var record = reader.ReadEvent();
            if (record is null) return null;

            var boot = Named(record, "BootTime");
            if (boot is null) return null;
            return new Boot(record.TimeCreated ?? DateTimeOffset.Now.DateTime, boot.Value);
        }
        catch { return null; }   // the log is off, or this account cannot read it
    }

    /// <summary>What Windows measured each slow-starting program costing, by name, most recent first.</summary>
    private static Dictionary<string, int> MeasuredCosts()
    {
        var costs = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        try
        {
            // Event 101: "this application took longer than usual to start". It carries the name and the time.
            var query = new EventLogQuery(DiagnosticsLog, PathType.LogName, "*[System[EventID=101]]")
            { ReverseDirection = true };
            using var reader = new EventLogReader(query);
            for (int seen = 0; seen < 200; seen++)
            {
                using var record = reader.ReadEvent();
                if (record is null) break;
                var name = NamedText(record, "Name");
                var total = Named(record, "TotalTime");
                if (name is null || total is null) continue;
                var key = Path.GetFileNameWithoutExtension(name);
                if (!costs.ContainsKey(key)) costs[key] = total.Value;   // newest wins
            }
        }
        catch { }
        return costs;
    }

    private static int? Named(EventRecord record, string name)
    {
        try
        {
            var value = record.ToXml();
            var tag = $"Name='{name}'>";
            int at = value.IndexOf(tag, StringComparison.Ordinal);
            if (at < 0) return null;
            int from = at + tag.Length;
            int to = value.IndexOf('<', from);
            return to > from && int.TryParse(value[from..to], out var number) ? number : null;
        }
        catch { return null; }
    }

    private static string? NamedText(EventRecord record, string name)
    {
        try
        {
            var value = record.ToXml();
            var tag = $"Name='{name}'>";
            int at = value.IndexOf(tag, StringComparison.Ordinal);
            if (at < 0) return null;
            int from = at + tag.Length;
            int to = value.IndexOf('<', from);
            return to > from ? value[from..to] : null;
        }
        catch { return null; }
    }

    public IReadOnlyList<StartupEntry> Startup()
    {
        var costs = MeasuredCosts();
        var found = new List<StartupEntry>();

        void ReadRun(RegistryKey root, string key, StartsIn where)
        {
            try
            {
                using var k = root.OpenSubKey(key);
                if (k is null) return;
                foreach (var name in k.GetValueNames())
                {
                    var command = k.GetValue(name)?.ToString() ?? "";
                    found.Add(new StartupEntry(name, command, where, Enabled(where, name), Cost(costs, name, command)));
                }
            }
            catch { }
        }

        ReadRun(Registry.CurrentUser, Run, StartsIn.UserRun);
        ReadRun(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", StartsIn.MachineRun);
        ReadRun(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", StartsIn.MachineRun32);

        foreach (var folder in new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup),
        })
        {
            try
            {
                if (!Directory.Exists(folder)) continue;
                foreach (var file in Directory.GetFiles(folder))
                {
                    var name = Path.GetFileName(file);
                    if (name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                    found.Add(new StartupEntry(name, file, StartsIn.StartupFolder,
                                               Enabled(StartsIn.StartupFolder, name), Cost(costs, name, file)));
                }
            }
            catch { }
        }

        return found.OrderByDescending(e => e.Milliseconds ?? 0)
                    .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
    }

    private static int? Cost(Dictionary<string, int> costs, string name, string command)
    {
        if (costs.TryGetValue(name, out var byName)) return byName;
        // The log names the executable; the registry names the entry, and the two often differ.
        try
        {
            var exe = command.Trim('"').Split('"')[0];
            var stem = Path.GetFileNameWithoutExtension(exe);
            if (stem.Length > 0 && costs.TryGetValue(stem, out var byExe)) return byExe;
        }
        catch { }
        return null;
    }

    private static bool Enabled(StartsIn where, string name)
    {
        var (hive, key) = Speed.Approval(where);
        try
        {
            var root = hive == Hive.LocalMachine ? Registry.LocalMachine : Registry.CurrentUser;
            using var k = root.OpenSubKey(key);
            if (k?.GetValue(name) is byte[] bytes) return Speed.ReadsAsEnabled(RegValue.Bytes(bytes));
        }
        catch { }
        return true;   // nothing recorded means Windows runs it
    }

    public SystemDisk Disk()
    {
        var kind = DiskKind.Unknown;
        try
        {
            // MediaType on the physical disk: 3 is a spinning disk, 4 is solid state. The system drive is the
            // one Windows is on, so the boot disk is the one worth asking about.
            using var search = new ManagementObjectSearcher(
                new ManagementScope(@"\\.\root\Microsoft\Windows\Storage"),
                new ObjectQuery("SELECT MediaType, IsBoot FROM MSFT_PhysicalDisk"));
            foreach (ManagementObject disk in search.Get())
            {
                var media = Convert.ToInt32(disk["MediaType"] ?? 0);
                if (media == 3) kind = DiskKind.Spinning;
                else if (media is 4 or 5) kind = DiskKind.Solid;
                if (kind != DiskKind.Unknown) break;
            }
        }
        catch { }

        long free = 0, total = 0;
        try
        {
            var root = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)) ?? "C:\\";
            var drive = new DriveInfo(root);
            free = drive.AvailableFreeSpace;
            total = drive.TotalSize;
        }
        catch { }

        return new SystemDisk(kind, free, total);
    }

    public IReadOnlyList<Reclaimable> Space()
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var drive = Path.GetPathRoot(windows) ?? "C:\\";
        var found = new List<Reclaimable>();

        void Look(string what, string path, string how)
        {
            var bytes = SizeOf(path);
            if (bytes > 100L * 1024 * 1024) found.Add(new Reclaimable(what, bytes, how));
        }

        Look("Windows Update's downloaded installers",
             Path.Combine(windows, "SoftwareDistribution", "Download"),
             "Disk Cleanup, ticking \"Windows Update Cleanup\". Windows downloads them again if it needs them.");
        Look("Temporary files Windows made",
             Path.Combine(windows, "Temp"),
             "Disk Cleanup, or Storage settings.");
        Look("Temporary files your programs made",
             Path.GetTempPath(),
             "Storage settings, under Temporary files.");
        Look("Delivery Optimization's cache of updates for other PCs",
             Path.Combine(windows, "SoftwareDistribution", "DeliveryOptimization"),
             "Storage settings, under Temporary files. Turning the sharing itself off is on the junk list.");
        Look("The previous Windows installation",
             Path.Combine(drive, "Windows.old"),
             "Disk Cleanup, ticking \"Previous Windows installation(s)\". You cannot go back to it afterwards.");

        return found.OrderByDescending(r => r.Bytes).ToList();
    }

    /// <summary>Adds a folder up, giving up rather than throwing on the parts this account cannot read.</summary>
    private static long SizeOf(string path)
    {
        try
        {
            if (!Directory.Exists(path)) return 0;
            long total = 0;
            var stack = new Stack<string>();
            stack.Push(path);
            int looked = 0;
            while (stack.Count > 0 && looked < 5000)
            {
                var here = stack.Pop();
                looked++;
                try
                {
                    foreach (var file in Directory.EnumerateFiles(here))
                        try { total += new FileInfo(file).Length; } catch { }
                    foreach (var sub in Directory.EnumerateDirectories(here)) stack.Push(sub);
                }
                catch { }
            }
            return total;
        }
        catch { return 0; }
    }
}
