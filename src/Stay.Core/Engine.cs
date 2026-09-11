namespace Stay.Core;

/// <summary>What one press of Apply would do, before it does it.</summary>
public sealed record Plan(IReadOnlyList<Guard> Guards, IReadOnlyList<RegChange> Registry)
{
    public bool IsEmpty => Registry.Count == 0;
    public bool NeedsRestart => Guards.Any(g => g.NeedsRestart);
}

public sealed record UndoResult(int Restored, IReadOnlyList<string> Errors);

/// <summary>
/// Works out where this PC stands, what is still being patched, whether it could move to Windows 11, and what is
/// worth shutting. Knows nothing about Windows itself: everything arrives through the ports, which is why the
/// whole of it can be tested against a PC that does not exist.
/// </summary>
public sealed class Engine
{
    private readonly IRegistry _reg;
    private readonly IMachine _machine;
    private readonly IReceiptStore _store;
    private readonly string _version, _host, _user;

    public Func<DateTimeOffset> Now { get; set; } = () => DateTimeOffset.Now;
    public IReceiptStore Store => _store;

    public Engine(IRegistry registry, IMachine machine, IReceiptStore store, string version, string host, string user)
    {
        _reg = registry; _machine = machine; _store = store; _version = version; _host = host; _user = user;
    }

    private DateOnly Today => DateOnly.FromDateTime(Now().Date);

    // ---------- where this PC stands ----------

    public Standing Scan()
    {
        var windows = _machine.Windows();
        var esu = ReadEsu(windows);
        return new Standing(windows, esu, Components(windows, esu), Elevenable.Check(_machine),
                            Guards.All.Select(StatusOf).ToList(), Today);
    }

    /// <summary>
    /// Whether ESU is switched on, read from the licences Windows itself holds rather than from a registry value
    /// somebody might have set by hand. An active licence whose name says ESU is the thing that makes updates
    /// arrive, so it is the thing worth asking about.
    /// </summary>
    public Esu ReadEsu(WindowsBuild windows)
    {
        if (windows.IsWindows11)
            return new Esu(EsuState.NotApplicable, "This is Windows 11. It is supported in the ordinary way.");
        if (windows.IsLtsc || windows.IsIotLtsc)
            return new Esu(EsuState.NotApplicable,
                "This is an LTSC edition, which has its own support date and does not use consumer ESU.");

        IReadOnlyList<(string Name, string Description, bool Active)> licences;
        try { licences = _machine.Licences(); }
        catch (Exception e) { return new Esu(EsuState.Unknown, $"The licence check could not be run: {e.Message}"); }

        if (licences.Count == 0)
            return new Esu(EsuState.Unknown,
                "Windows did not list any licences. This usually means the app is not running as an administrator.");

        var found = licences.FirstOrDefault(l =>
            l.Name.Contains("ESU", StringComparison.OrdinalIgnoreCase)
            || l.Description.Contains("Extended Security Update", StringComparison.OrdinalIgnoreCase));

        if (found.Name is null)
            return new Esu(EsuState.NotEnrolled,
                "No Extended Security Updates licence on this PC. Windows itself is not being patched.");

        return found.Active
            ? new Esu(EsuState.Enrolled,
                "Enrolled. Windows is getting critical and important security updates.",
                found.Name, new DateTimeOffset(Lifecycle.ConsumerEsuEnds.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero))
            : new Esu(EsuState.NotEnrolled,
                $"An ESU licence is present but not active ({found.Name}). Windows is not being patched.", found.Name);
    }

    /// <summary>Everything on this PC that is still getting security updates, and the day each one stops.</summary>
    public IReadOnlyList<Component> Components(WindowsBuild windows, Esu esu)
    {
        var list = new List<Component>();

        if (windows.IsWindows11)
        {
            list.Add(new("windows", "Windows 11", "Supported in the ordinary way.", null, true,
                         "Nothing here applies to this PC.", Lifecycle.SourceWindows));
            return list;
        }

        if (windows.IsIotLtsc)
            list.Add(new("ltsc", "Windows 10 IoT Enterprise LTSC 2021",
                "The long-term edition licensed for fixed-function machines: tills, kiosks, medical and industrial equipment.",
                Lifecycle.IotLtscEnds, true, "Security updates until then, without any of this ESU business.",
                Lifecycle.SourceLifecycle));
        else if (windows.IsLtsc)
            list.Add(new("ltsc", "Windows 10 Enterprise LTSC 2021",
                "The long-term edition. Not the same date as the IoT one, which is a common and expensive confusion.",
                Lifecycle.LtscEnds, true, "Security updates until then.", Lifecycle.SourceLifecycle));
        else if (esu.State == EsuState.Enrolled)
            list.Add(new("windows", "Windows 10, with Extended Security Updates",
                Lifecycle.WhatEsuIs, Lifecycle.ConsumerEsuEnds, true,
                "Enrolled, so security updates are still arriving.", Lifecycle.SourceEsu));
        else
            list.Add(new("windows", "Windows 10 itself",
                "Support ended on 14 October 2025. Without Extended Security Updates, no security fix has been "
                + "issued for this PC since then and none will be.",
                Lifecycle.Windows10Ended, true,
                esu.State == EsuState.Unknown ? esu.Detail : "Not enrolled. Nothing is patching Windows on this PC.",
                Lifecycle.SourceWindows));

        list.Add(new("edge", "Microsoft Edge and WebView2",
            "The browser, and the engine other programs use to show web pages inside themselves.",
            Lifecycle.EdgeEnds, true,
            "Still updating, and it is the part of this PC most exposed to the internet, so that matters more than it sounds.",
            Lifecycle.SourceLifecycle));

        list.Add(new("defender", "Microsoft Defender definitions",
            "The list of what to recognise, updated several times a day.",
            Lifecycle.DefenderEnds, true,
            "Still updating. Definitions are not patches: they catch what is known, not what the hole allows.",
            Lifecycle.SourceLifecycle));

        bool office = _machine.Installed("Microsoft 365") || _machine.Installed("Office");
        list.Add(new("m365", "Microsoft 365 Apps (Word, Excel, Outlook)",
            "Security updates only, after feature updates stop at version 2608.",
            Lifecycle.M365Ends, office,
            office ? "Installed on this PC, and still getting security updates."
                   : "Not installed on this PC, so this date does not affect you.",
            Lifecycle.SourceM365));

        return list;
    }

    // ---------- what is open, and what closing it would take ----------

    public GuardStatus StatusOf(Guard guard)
    {
        var edits = Expand(guard);
        if (edits.Count == 0)
            return new GuardStatus(guard, GuardState.Closed, Array.Empty<RegValue>());

        var current = edits.Select(e => _reg.Read(e.Hive, e.Key, e.Name)).ToList();
        int matching = current.Where((c, i) => Matches(c, edits[i].Wanted)).Count();
        var state = matching == edits.Count ? GuardState.Closed
                  : matching == 0 ? GuardState.Open
                  : GuardState.Partly;
        return new GuardStatus(guard, state, current);
    }

    public static bool Matches(RegValue current, RegValue wanted) => wanted.Kind switch
    {
        ValueKind.Absent => current.Kind == ValueKind.Absent,
        ValueKind.DWord => current.Kind == ValueKind.DWord && current.Number == wanted.Number,
        _ => current.Kind == ValueKind.String && string.Equals(current.Text, wanted.Text, StringComparison.OrdinalIgnoreCase),
    };

    /// <summary>
    /// A guard's edits, including the ones that can only be known on the machine. NetBIOS is set per network
    /// adapter under a key whose names are different on every PC, so its edits are read off this PC rather than
    /// written in the catalogue.
    /// </summary>
    public IReadOnlyList<RegEdit> Expand(Guard guard)
    {
        if (guard.Id != "netbios") return guard.Edits;

        const string interfaces = @"SYSTEM\CurrentControlSet\Services\NetBT\Parameters\Interfaces";
        return _reg.SubKeys(Hive.LocalMachine, interfaces)
            .Where(name => name.StartsWith("Tcpip_", StringComparison.OrdinalIgnoreCase))
            .Select(name => new RegEdit(Hive.LocalMachine, $@"{interfaces}\{name}", "NetbiosOptions", RegValue.DWord(2)))
            .ToList();
    }

    /// <summary>Whether a guard applies to this PC at all. A printer check is not advice for a PC with a printer.</summary>
    public bool Applies(Guard guard, WindowsBuild windows)
    {
        if (windows.IsWindows11) return false;
        return guard.OnlyIf switch
        {
            "office" => _machine.Installed("Microsoft 365") || _machine.Installed("Office"),
            "defender" => !_machine.Installed("third-party antivirus"),
            "noprinter" => !_machine.HasPrinter(),
            "netbios" => Expand(guard).Count > 0,
            _ => true,
        };
    }

    /// <summary>What the app suggests: every default-on guard that applies here and is not already closed.</summary>
    public IReadOnlyList<Guard> Suggested(Standing standing)
        => standing.Guards
            .Where(g => g.Guard.DefaultOn && g.NeedsDoing && Applies(g.Guard, standing.Windows))
            .Select(g => g.Guard)
            .ToList();

    public Plan PlanFor(IEnumerable<Guard> guards)
    {
        var chosen = guards.ToList();
        var changes = new List<RegChange>();
        foreach (var guard in chosen)
            foreach (var edit in Expand(guard))
            {
                var before = _reg.Read(edit.Hive, edit.Key, edit.Name);
                if (Matches(before, edit.Wanted)) continue;  // already as it should be: not a change
                changes.Add(new RegChange(guard.Id, edit.Hive, edit.Key, edit.Name, before, edit.Wanted));
            }
        return new Plan(chosen, changes);
    }

    // ---------- doing it, and undoing it ----------

    public Receipt Apply(Plan plan, bool restorePoint = false)
    {
        var done = new List<RegChange>();
        foreach (var change in plan.Registry)
        {
            try
            {
                _reg.Write(change.Hive, change.Key, change.Name, change.After);
                done.Add(change);
            }
            catch (Exception e)
            {
                done.Add(change with { Error = e.Message });
            }
        }

        var when = Now();
        var receipt = new Receipt(Receipt.NewId(when), when, _host, _user, _version, done, restorePoint);
        _store.Save(receipt);
        return receipt;
    }

    /// <summary>Puts back exactly what a receipt recorded, value by value, in reverse.</summary>
    public UndoResult Undo(Receipt receipt)
    {
        var errors = new List<string>();
        int restored = 0;
        foreach (var change in receipt.Registry.Where(c => !c.Failed).Reverse())
        {
            try
            {
                _reg.Write(change.Hive, change.Key, change.Name, change.Before);
                restored++;
            }
            catch (Exception e)
            {
                errors.Add($"{change.Path}: {e.Message}");
            }
        }
        _store.Save(receipt with { Undone = Now() });
        return new UndoResult(restored, errors);
    }
}
