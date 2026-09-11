namespace Stay.Core;

/// <summary>The registry, as much of it as the engine needs. The app supplies the real one; tests supply a fake.</summary>
public interface IRegistry
{
    RegValue Read(Hive hive, string key, string name);
    /// <summary>Writes a value, creating the key if needed. Absent deletes the value.</summary>
    void Write(Hive hive, string key, string name, RegValue value);
    /// <summary>The names under a key, for the checks that have to look rather than ask.</summary>
    IReadOnlyList<string> SubKeys(Hive hive, string key);
}

/// <summary>What the PC is: the parts the engine reasons about but cannot work out for itself.</summary>
public interface IMachine
{
    WindowsBuild Windows();

    /// <summary>Every software licence Windows knows about, by name and whether it is active. ESU is one of these.</summary>
    IReadOnlyList<(string Name, string Description, bool Active)> Licences();

    /// <summary>TPM: present, enabled, and which spec version. Null when the PC has none at all.</summary>
    (bool Enabled, string Version)? Tpm();

    bool? SecureBootEnabled();
    bool UefiBoot();

    /// <summary>The processor as Windows names it, plus what it takes to judge it against Windows 11's list.</summary>
    (string Name, string Manufacturer, int Family, int Model, int Cores, double GHz) Cpu();

    long MemoryBytes();
    long SystemDiskBytes();

    /// <summary>Whether a program is installed, by the name it registers. Used for "is Office even on this PC".</summary>
    bool Installed(string what);

    /// <summary>Whether a printer other than the built-in writers is set up, so the spooler advice can be honest.</summary>
    bool HasPrinter();
}

/// <summary>How this PC performs, and what is making it slow. Separate from IMachine because it is a different
/// question asked of different parts of Windows, and because a PC can answer one and not the other.</summary>
public interface IPerformance
{
    /// <summary>How long Windows took to start, last time, from its own performance log. Null when it has not recorded one.</summary>
    Boot? LastBoot();

    /// <summary>Everything set to start when you sign in, with what Windows measured it costing where it knows.</summary>
    IReadOnlyList<StartupEntry> Startup();

    /// <summary>The disk Windows is on: spinning or solid, and how full.</summary>
    SystemDisk Disk();

    /// <summary>Space being held that could be given back, and what gives it back.</summary>
    IReadOnlyList<Reclaimable> Space();
}

/// <summary>Whether this PC is really being looked after: what arrived, and what is watching.</summary>
public interface IProtection
{
    /// <summary>The day the most recent update actually installed. Null when Windows has no record of one.</summary>
    DateOnly? LastUpdate();

    /// <summary>An update installed and waiting for a restart.</summary>
    bool RestartPending();

    /// <summary>Whether the Windows Update service could run at all.</summary>
    bool UpdatesReachable();

    /// <summary>When Windows last managed to ask for updates.</summary>
    DateTimeOffset? LastChecked();

    /// <summary>The antivirus and the firewall.</summary>
    Guarded Guarded();
}

/// <summary>Installed app packages. The app supplies the real one; tests supply a fake.</summary>
public interface IPackages
{
    IReadOnlyList<InstalledApp> List();
    /// <summary>Removes the package for every user and stops Windows adding it to new accounts.</summary>
    void Remove(InstalledApp app);
}

/// <summary>Where receipts live.</summary>
public interface IReceiptStore
{
    void Save(Receipt receipt);
    IReadOnlyList<Receipt> List();
}

// ---------- fakes, so every test runs on a made-up PC and never on this one ----------

public sealed class FakeRegistry : IRegistry
{
    private readonly Dictionary<string, RegValue> _values = new(StringComparer.OrdinalIgnoreCase);
    public int Writes { get; private set; }
    public bool RefuseWrites { get; set; }

    private static string K(Hive h, string key, string name) => $"{h}\\{key}\\{name}";

    public RegValue Read(Hive hive, string key, string name)
        => _values.TryGetValue(K(hive, key, name), out var v) ? v : RegValue.Absent;

    public void Write(Hive hive, string key, string name, RegValue value)
    {
        if (RefuseWrites) throw new UnauthorizedAccessException("the fake was told to refuse");
        Writes++;
        if (value.Kind == ValueKind.Absent) _values.Remove(K(hive, key, name));
        else _values[K(hive, key, name)] = value;
    }

    public IReadOnlyList<string> SubKeys(Hive hive, string key) => Array.Empty<string>();

    public void Set(Hive hive, string key, string name, RegValue value) => _values[K(hive, key, name)] = value;
    public IReadOnlyDictionary<string, RegValue> Snapshot() => new Dictionary<string, RegValue>(_values, StringComparer.OrdinalIgnoreCase);
}

/// <summary>A made-up PC. Every property is settable, so a test can build the exact machine it wants to reason about.</summary>
public sealed class FakeMachine : IMachine
{
    public WindowsBuild Build { get; set; } =
        new("Microsoft Windows 10 Pro", "Professional", "22H2", 10, 19045, 4291, "x64");
    public List<(string Name, string Description, bool Active)> Licences_ { get; } = new();
    public (bool Enabled, string Version)? TpmChip { get; set; } = (true, "2.0");
    public bool? SecureBoot { get; set; } = true;
    public bool Uefi { get; set; } = true;
    public (string Name, string Manufacturer, int Family, int Model, int Cores, double GHz) Processor { get; set; }
        = ("Intel(R) Core(TM) i5-8250U CPU @ 1.60GHz", "GenuineIntel", 6, 142, 4, 1.6);
    public long Memory { get; set; } = 8L * 1024 * 1024 * 1024;
    public long Disk { get; set; } = 256L * 1024 * 1024 * 1024;
    public HashSet<string> Programs { get; } = new(StringComparer.OrdinalIgnoreCase);
    public bool Printer { get; set; }

    public WindowsBuild Windows() => Build;
    public IReadOnlyList<(string Name, string Description, bool Active)> Licences() => Licences_;
    public (bool Enabled, string Version)? Tpm() => TpmChip;
    public bool? SecureBootEnabled() => SecureBoot;
    public bool UefiBoot() => Uefi;
    public (string, string, int, int, int, double) Cpu() => Processor;
    public long MemoryBytes() => Memory;
    public long SystemDiskBytes() => Disk;
    public bool Installed(string what) => Programs.Contains(what);
    public bool HasPrinter() => Printer;
}

/// <summary>A made-up PC's performance, so the speed half can be reasoned about without a slow PC to hand.</summary>
public sealed class FakePerformance : IPerformance
{
    public Boot? Booted { get; set; } = new(new DateTimeOffset(2026, 9, 10, 8, 41, 0, TimeSpan.Zero), 94_000);
    public List<StartupEntry> Starters { get; } = new();
    public SystemDisk TheDisk { get; set; } = new(DiskKind.Spinning, 18L * 1024 * 1024 * 1024, 238L * 1024 * 1024 * 1024);
    public List<Reclaimable> Reclaimables { get; } = new();

    public Boot? LastBoot() => Booted;
    public IReadOnlyList<StartupEntry> Startup() => Starters.ToList();
    public SystemDisk Disk() => TheDisk;
    public IReadOnlyList<Reclaimable> Space() => Reclaimables.ToList();
}

/// <summary>A made-up PC that is being looked after properly, which tests then break in one way at a time.</summary>
public sealed class FakeProtection : IProtection
{
    public DateOnly? Installed { get; set; } = new(2026, 9, 9);
    public bool Waiting { get; set; }
    public bool Reachable { get; set; } = true;
    public DateTimeOffset? Checked { get; set; } = new DateTimeOffset(2026, 9, 10, 6, 0, 0, TimeSpan.Zero);
    public Guarded Watching { get; set; } = new("Microsoft Defender", true, true, 0, true);

    public DateOnly? LastUpdate() => Installed;
    public bool RestartPending() => Waiting;
    public bool UpdatesReachable() => Reachable;
    public DateTimeOffset? LastChecked() => Checked;
    public Guarded Guarded() => Watching;
}

public sealed class FakePackages : IPackages
{
    private readonly List<InstalledApp> _apps;
    public List<string> Removed { get; } = new();
    public FakePackages(IEnumerable<InstalledApp> apps) => _apps = apps.ToList();
    public IReadOnlyList<InstalledApp> List() => _apps.ToList();
    public void Remove(InstalledApp app)
    {
        if (app.Name == "Fail.OnPurpose") throw new InvalidOperationException("removal refused by the fake");
        _apps.RemoveAll(a => a.FamilyName == app.FamilyName);
        Removed.Add(app.FamilyName);
    }
}

public sealed class MemoryReceiptStore : IReceiptStore
{
    private readonly List<Receipt> _list = new();
    public void Save(Receipt receipt) { _list.RemoveAll(r => r.Id == receipt.Id); _list.Add(receipt); }
    public IReadOnlyList<Receipt> List() => _list.OrderByDescending(r => r.When).ToList();
}
