namespace Stay.Core;

/// <summary>Where a startup program is written down. Windows keeps four such places and Task Manager shows them as one list.</summary>
public enum StartsIn { UserRun, MachineRun, MachineRun32, StartupFolder }

/// <summary>
/// One program set to start when you sign in.
///
/// Whether it is switched on is not held next to the program: Windows keeps that under StartupApproved, as twelve
/// bytes whose first byte says on or off. Switching one off is therefore an ordinary registry write, which means
/// it goes on a receipt and comes back exactly, the same as everything else this app does.
/// </summary>
public sealed record StartupEntry(
    string Name,
    string Command,
    StartsIn Where,
    bool Enabled,
    int? Milliseconds = null)   // what Windows itself measured this costing at boot, when it has measured it
{
    public string Publisher => Command.Contains("Microsoft", StringComparison.OrdinalIgnoreCase) ? "Microsoft" : "";
    public string Cost => Milliseconds is null ? "" : $"{Milliseconds.Value / 1000.0:0.0} seconds at sign-in";
}

public enum DiskKind { Spinning, Solid, Unknown }

public sealed record SystemDisk(DiskKind Kind, long Free, long Total)
{
    public double FreeGb => Free / 1024.0 / 1024 / 1024;
    public double TotalGb => Total / 1024.0 / 1024 / 1024;
    public bool Cramped => Total > 0 && Free < Total * 0.1;
}

/// <summary>Space something is holding that can be given back, and what gives it back.</summary>
public sealed record Reclaimable(string What, long Bytes, string How)
{
    public double Gb => Bytes / 1024.0 / 1024 / 1024;
}

/// <summary>What Windows' own performance log says about how this PC starts.</summary>
public sealed record Boot(DateTimeOffset When, int Milliseconds)
{
    public double Seconds => Milliseconds / 1000.0;
}

/// <summary>Everything the speed half found.</summary>
public sealed record Pace(
    Boot? LastBoot,
    IReadOnlyList<StartupEntry> Startup,
    SystemDisk Disk,
    long MemoryBytes,
    IReadOnlyList<Reclaimable> Space,
    IReadOnlyList<GuardStatus> Switches)
{
    public IReadOnlyList<StartupEntry> Running => Startup.Where(s => s.Enabled).ToList();
    public double SecondsAtStartup => Running.Sum(s => (s.Milliseconds ?? 0) / 1000.0);
    public long Reclaimable => Space.Sum(s => s.Bytes);
    public double MemoryGb => MemoryBytes / 1024.0 / 1024 / 1024;

    /// <summary>
    /// The one sentence worth saying first, which on most old PCs is not about software at all. Saying so costs
    /// this app nothing and is the difference between advice and a sales pitch.
    /// </summary>
    /// <summary>
    /// Six, not eight. Windows reports the memory it can use, which is always somewhat less than the memory that
    /// is fitted — a PC with 8 GB in it answers about 7.9 — so a threshold written at exactly 8 told people with
    /// enough memory that they did not have enough.
    /// </summary>
    public const long EnoughMemory = 6L * 1024 * 1024 * 1024;

    public string Truth => Disk.Kind == DiskKind.Spinning
        ? "This PC has a spinning hard disk. Replacing it with an SSD would do more for it than everything below "
        + "put together, and costs less than a new PC."
        : MemoryBytes > 0 && MemoryBytes < EnoughMemory
            ? $"This PC has {MemoryGb:0.#} GB of memory. On Windows 10 that is where the slowness comes from, and "
            + "more memory would help it more than anything below."
            : "The disk and the memory in this PC are not the problem, so the things below are worth doing.";
}

/// <summary>
/// The switches that make an old PC feel faster, and only the ones that really do.
///
/// Deliberately short. Most of what circulates as Windows tuning is folklore — page file sizes, "unnecessary"
/// services that turn out to be necessary, registry cleaners. Everything here either stops work happening or
/// stops something being drawn, and each one says what it costs.
/// </summary>
public static class Speed
{
    public const string Group = "Speed";
    private const string Explorer = @"Software\Microsoft\Windows\CurrentVersion\Explorer";

    private static RegEdit User(string key, string name, long value) => new(Hive.CurrentUser, key, name, RegValue.DWord(value));
    private static RegEdit UserText(string key, string name, string value) => new(Hive.CurrentUser, key, name, RegValue.Str(value));
    private static RegEdit ServiceOff(string service) =>
        new(Hive.LocalMachine, $@"SYSTEM\CurrentControlSet\Services\{service}", "Start", RegValue.DWord(4));

    public static readonly IReadOnlyList<Guard> Switches = new List<Guard>
    {
        new("speed.visual", Group, "Animations, shadows and fades",
            "Windows draws menus fading in, windows sliding, shadows under everything. On a PC with a weak graphics chip that work is not free.",
            "The desktop responds the moment you click instead of a fraction of a second later. It is the single most noticeable change on an old machine.",
            "It looks plainer. Nothing moves that used to move.",
            new[]
            {
                User($@"{Explorer}\VisualEffects", "VisualFXSetting", 2),
                UserText(@"Control Panel\Desktop", "MenuShowDelay", "0"),
                UserText(@"Control Panel\Desktop", "DragFullWindows", "0"),
                User(@"Control Panel\Desktop\WindowMetrics", "MinAnimate", 0),
                User($@"{Explorer}\Advanced", "TaskbarAnimations", 0),
                User($@"{Explorer}\Advanced", "ListviewAlphaSelect", 0),
            },
            DefaultOn: true, NeedsSignOut: true, Cost: Cost.Small),

        new("speed.transparency", Group, "See-through menus and taskbar",
            "The blur behind the Start menu and taskbar is redrawn constantly while anything moves underneath it.",
            "Takes steady work off a graphics chip that has better things to do.",
            "Menus and the taskbar become solid instead of frosted.",
            new[] { User(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 0) },
            DefaultOn: true, Cost: Cost.None),

        new("speed.search", Group, "The search index, on a spinning disk",
            "Windows reads every document on the disk in the background so Start can search inside them.",
            "On a spinning disk this is the loudest background job on the PC, and stopping it gives the disk back to whatever you are actually doing.",
            "Searching for a file by name still works and is slower. Searching inside documents from Start stops working.",
            new[] { ServiceOff("WSearch") },
            DefaultOn: true, NeedsRestart: true, Cost: Cost.Real, OnlyIf: "spinning"),

        new("speed.sysmain", Group, "SysMain, on a solid-state disk",
            "The old Superfetch: Windows reading programs into memory before you ask for them, guessing from what you usually open.",
            "It was built for spinning disks. On an SSD the guessing costs more in memory and background reading than the head start is worth.",
            "Nothing you would notice on an SSD. On a PC with a spinning disk this would be the wrong thing to do, so it is only offered here when the disk is solid state.",
            new[] { ServiceOff("SysMain") },
            DefaultOn: true, NeedsRestart: true, Cost: Cost.Small, OnlyIf: "solid"),

        new("speed.storagesense", Group, "Storage Sense, so the disk stops filling",
            "Windows clearing temporary files and the recycle bin on its own.",
            "A Windows 10 disk with almost nothing free is slow in a way no other change fixes. This keeps it from getting there.",
            "Files in the recycle bin older than 30 days are emptied without asking.",
            new[]
            {
                User(@"Software\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy", "01", 1),
                User(@"Software\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy", "08", 1),
                User(@"Software\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy", "32", 30),
            },
            DefaultOn: true, Cost: Cost.Small),
    };

    public static Guard? Find(string id) => Switches.FirstOrDefault(g => string.Equals(g.Id, id, StringComparison.OrdinalIgnoreCase));

    // ---------- startup programs ----------

    /// <summary>Where Windows records whether a startup program is switched on.</summary>
    public static (Hive Hive, string Key) Approval(StartsIn where) => where switch
    {
        StartsIn.UserRun => (Hive.CurrentUser, $@"{Explorer}\StartupApproved\Run"),
        StartsIn.MachineRun => (Hive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run"),
        StartsIn.MachineRun32 => (Hive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32"),
        _ => (Hive.CurrentUser, $@"{Explorer}\StartupApproved\StartupFolder"),
    };

    /// <summary>The twelve bytes that mean "off": a flag, then the moment it was switched off.</summary>
    public static byte[] OffBytes(DateTimeOffset when)
    {
        var bytes = new byte[12];
        bytes[0] = 0x03;
        BitConverter.GetBytes(when.ToFileTime()).CopyTo(bytes, 4);
        return bytes;
    }

    /// <summary>The first byte says it all: an even one is on, an odd one is off.</summary>
    public static bool ReadsAsEnabled(RegValue value)
    {
        if (value.Kind != ValueKind.Binary) return true;  // no record of it being switched off means it runs
        var bytes = value.AsBytes();
        return bytes.Length == 0 || bytes[0] % 2 == 0;
    }

    /// <summary>
    /// Switching one startup program off, expressed as an ordinary switch so that it plans, applies, records and
    /// undoes through exactly the same path as everything else.
    /// </summary>
    public static Guard Stop(StartupEntry entry, DateTimeOffset when)
    {
        var (hive, key) = Approval(entry.Where);
        var cost = entry.Milliseconds is null ? "" : $" Windows measured it costing {entry.Cost}.";
        return new Guard(
            "startup:" + entry.Name,
            Group,
            entry.Name,
            $"Starts when you sign in.{cost}",
            "Stops it starting on its own. The program itself is untouched and opens normally when you open it.",
            "Anything it does in the background — syncing, updating, watching for a keypress — stops until you open it.",
            new[] { new RegEdit(hive, key, entry.Name, RegValue.Bytes(OffBytes(when))) },
            DefaultOn: false,
            Cost: Cost.Small);
    }
}
