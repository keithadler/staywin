using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using Stay.Core;

namespace Stay;

/// <summary>The console twin. Exit codes: 0 fine, 1 something to look at, 2 problem, 64 usage.</summary>
public static class Cli
{
    public const string Version = "1.0.0";

    [DllImport("kernel32.dll")] private static extern bool AttachConsole(int pid);
    [DllImport("kernel32.dll")] private static extern bool AllocConsole();

    /// <summary>Entry point for the window's exe when it is given arguments: borrow the console it was started from.</summary>
    public static int Run(string[] args)
    {
        if (!AttachConsole(-1)) AllocConsole();
        Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
        try { return Dispatch(args, Console.Out); }
        catch (Exception ex) { Console.Error.WriteLine($"stay: {ex.Message}"); return 2; }
        finally { Console.Out.WriteLine(); }
    }

    public static Engine MakeEngine() => new(new WinRegistry(), new WinMachine(), new FileReceiptStore(),
                                             Version, Environment.MachineName, Environment.UserName);

    private static bool Flag(string[] args, string name) => args.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));

    private static string? Value(string[] args, string name)
    {
        int i = Array.FindIndex(args, a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
        return i >= 0 && i + 1 < args.Length && !args[i + 1].StartsWith("--") ? args[i + 1] : null;
    }

    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };
    private static void Json(TextWriter o, object what) => o.WriteLine(JsonSerializer.Serialize(what, Pretty));

    public static int Dispatch(string[] args, TextWriter o)
    {
        var verb = args.Length == 0 ? "help" : args[0].ToLowerInvariant();
        bool json = Flag(args, "--json");
        switch (verb)
        {
            case "help": case "--help": case "-h": o.WriteLine(Help); return 0;
            case "version": case "--version":
                o.WriteLine(json ? JsonSerializer.Serialize(new { version = Version }) : Version); return 0;
            case "selftest":
            {
                var filter = args.Skip(1).FirstOrDefault(a => !a.StartsWith("--"));
                return SelfTest.Run(o, filter, Flag(args, "--list")) == 0 ? 0 : 1;
            }
            case "status": return Status(o, json);
            case "updates": return Updates(o, json);
            case "eleven": case "11": return Eleven(o, json);
            case "guards": case "open": return GuardList(o, json);
            case "harden": return Harden(args, o, json);
            case "receipts": return Receipts(o, json);
            case "undo": return Undo(args, o);
            case "report": return Report(o, json);
            case "screenshots":
            {
                var dir = args.Skip(1).FirstOrDefault(a => !a.StartsWith("--"));
                if (dir is null) { o.WriteLine("stay screenshots <folder> [--dark]"); return 64; }
#if CLI_BUILD
                o.WriteLine("screenshots need the window: run \"Stay for Windows 10.exe\" screenshots <folder>");
                return 2;
#else
                return Screenshots.Render(dir, o, Flag(args, "--dark"));
#endif
            }
            default:
                o.WriteLine($"stay: no such command \"{verb}\"");
                o.WriteLine(Help);
                return 64;
        }
    }

    // ---------- where this PC stands ----------

    private static int Status(TextWriter o, bool json)
    {
        var standing = MakeEngine().Scan();
        var w = standing.Windows;

        if (json)
        {
            Json(o, new
            {
                windows = new { name = w.Name, w.Caption, w.Edition, w.Release, w.Version, w.Build, w.Architecture, w.IsWindows10, w.IsLtsc },
                esu = new { state = standing.Esu.State.ToString(), standing.Esu.Detail, standing.Esu.Until },
                patched = standing.Patched,
                openGuards = standing.Open,
                elevenReady = standing.Eleven.Ready,
                datesChecked = Lifecycle.Checked,
            });
            return standing.Patched ? 0 : 1;
        }

        o.WriteLine($"{w.Name} {w.Release}  ({w.Version}, {w.Architecture})");
        if (w.IsWindows11)
        {
            o.WriteLine("This is Windows 11. It is supported in the ordinary way and there is nothing here for you.");
            return 0;
        }
        if (w.IsWindows10 && !w.IsFinalRelease)
            o.WriteLine($"This is an older Windows 10 than 22H2. Nothing, including ESU, covers it. Update to 22H2 first.");

        o.WriteLine();
        o.WriteLine(standing.Patched
            ? "Windows on this PC is still getting security updates."
            : "Windows on this PC is NOT getting security updates.");
        o.WriteLine($"  {standing.Esu.Detail}");

        var windows = standing.Components.First(c => c.Id is "windows" or "ltsc");
        if (windows.Ends is { } ends)
            o.WriteLine(standing.Patched
                ? $"  That runs out on {ends:d MMMM yyyy}, {Lifecycle.HowLong(ends, standing.Today)}."
                : $"  Support ended on {ends:d MMMM yyyy}, {Lifecycle.HowLong(ends, standing.Today)}.");

        if (standing.Esu.State == EsuState.NotEnrolled)
        {
            o.WriteLine();
            o.WriteLine("Extended Security Updates are still open to join, three ways:");
            foreach (var route in Lifecycle.EsuRoutes) o.WriteLine($"  - {route}");
            o.WriteLine("  Settings > Update & Security > Windows Update, then \"Enroll now\".");
        }

        o.WriteLine();
        o.WriteLine(standing.Open == 0
            ? "Nothing is left open that this app would shut."
            : $"{standing.Open} thing{(standing.Open == 1 ? " is" : "s are")} open that this app can shut. Run: stay guards");
        o.WriteLine($"Windows 11: {(standing.Eleven.Ready ? "this PC could take it" : $"{standing.Eleven.Failing.Count} check(s) fail. Run: stay eleven")}");
        o.WriteLine();
        o.WriteLine($"Dates last checked against Microsoft's pages on {Lifecycle.Checked:d MMMM yyyy}.");
        return standing.Patched && standing.Open == 0 ? 0 : 1;
    }

    private static int Updates(TextWriter o, bool json)
    {
        var standing = MakeEngine().Scan();
        if (json) { Json(o, standing.Components.Select(c => new { c.Id, c.Title, c.Ends, c.Installed, c.Detail, c.Source, days = c.DaysLeft(standing.Today) })); return 0; }

        o.WriteLine("What on this PC is still getting security updates:");
        o.WriteLine();
        foreach (var c in standing.Components)
        {
            string when = c.Ends is null ? "no end announced"
                        : c.Over(standing.Today) ? $"ENDED {c.Ends:d MMM yyyy} ({Lifecycle.HowLong(c.Ends.Value, standing.Today)})"
                        : $"until {c.Ends:d MMM yyyy} ({Lifecycle.HowLong(c.Ends.Value, standing.Today)})";
            o.WriteLine($"  {(c.Installed ? " " : "-")} {c.Title}");
            o.WriteLine($"      {when}");
            o.WriteLine($"      {c.Detail}");
            o.WriteLine($"      source: {c.Source}");
            o.WriteLine();
        }
        return standing.Components.Any(c => c.Installed && c.Over(standing.Today)) ? 1 : 0;
    }

    private static int Eleven(TextWriter o, bool json)
    {
        var eleven = Elevenable.Check(new WinMachine());
        if (json) { Json(o, new { eleven.Ready, requirements = eleven.Requirements.Select(r => new { r.Id, r.Title, result = r.Result.ToString(), r.Found, r.Needed, r.Remedy }) }); return eleven.Ready ? 0 : 1; }

        foreach (var r in eleven.Requirements)
        {
            string mark = r.Result switch { CheckResult.Pass => "ok  ", CheckResult.Fail => "no  ", _ => "?   " };
            o.WriteLine($"{mark}{r.Title}");
            o.WriteLine($"      found: {r.Found}");
            if (r.Result != CheckResult.Pass) o.WriteLine($"      needs: {r.Needed}");
            if (r.Remedy is not null) o.WriteLine($"      {r.Remedy}");
        }
        o.WriteLine();
        o.WriteLine(eleven.Ready ? "This PC can take Windows 11."
                  : eleven.Fixable ? "Every failing check above is a setting, not a new PC."
                  : "At least one failing check cannot be changed on this PC.");
        return eleven.Ready ? 0 : 1;
    }

    // ---------- what is open ----------

    private static int GuardList(TextWriter o, bool json)
    {
        var engine = MakeEngine();
        var standing = engine.Scan();
        var applies = standing.Guards.Where(g => engine.Applies(g.Guard, standing.Windows)).ToList();

        if (json) { Json(o, applies.Select(g => new { g.Guard.Id, g.Guard.Group, g.Guard.Title, state = g.State.ToString(), g.Guard.DefaultOn, cost = g.Guard.Cost.ToString(), g.Guard.NeedsRestart })); return applies.Any(g => g.NeedsDoing && g.Guard.DefaultOn) ? 1 : 0; }

        foreach (var group in applies.GroupBy(g => g.Guard.Group))
        {
            o.WriteLine(group.Key.ToUpperInvariant());
            foreach (var g in group)
            {
                string state = g.State switch { GuardState.Closed => "shut  ", GuardState.Partly => "partly", _ => "OPEN  " };
                o.WriteLine($"  {state} {g.Guard.Id,-12} {g.Guard.Title}");
                if (g.NeedsDoing) o.WriteLine($"               costs you: {g.Guard.Costs}");
            }
            o.WriteLine();
        }
        o.WriteLine("Shut everything suggested:  stay harden --all");
        o.WriteLine("Shut one:                   stay harden --id rdp");
        o.WriteLine("See first, change nothing:  stay harden --all --dry-run");
        return applies.Any(g => g.NeedsDoing && g.Guard.DefaultOn) ? 1 : 0;
    }

    private static int Harden(string[] args, TextWriter o, bool json)
    {
        var engine = MakeEngine();
        var standing = engine.Scan();

        if (standing.Windows.IsWindows11)
        {
            o.WriteLine("This is Windows 11, which is supported in the ordinary way. Nothing to do.");
            return 0;
        }

        List<Guard> chosen;
        if (Value(args, "--id") is { } ids)
        {
            chosen = new List<Guard>();
            foreach (var id in ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var guard = Guards.Find(id);
                if (guard is null) { o.WriteLine($"stay: no such thing to shut: \"{id}\". Run: stay guards"); return 64; }
                chosen.Add(guard);
            }
        }
        else if (Flag(args, "--all")) chosen = engine.Suggested(standing).ToList();
        else { o.WriteLine("stay harden needs --all or --id <name>. Run: stay guards"); return 64; }

        var plan = engine.PlanFor(chosen);
        if (plan.IsEmpty) { o.WriteLine("Nothing to change: all of that is already shut."); return 0; }

        if (Flag(args, "--dry-run"))
        {
            if (json) { Json(o, plan.Registry.Select(c => new { c.GuardId, c.Path, before = c.Before.ToString(), after = c.After.ToString() })); return 0; }
            o.WriteLine($"Would change {plan.Registry.Count} value(s) and write a receipt:");
            foreach (var c in plan.Registry) o.WriteLine($"  {c.Path}: {c.Before} -> {c.After}");
            if (plan.NeedsRestart) o.WriteLine("Some of it takes effect after a restart.");
            return 0;
        }

        bool wantsPoint = Flag(args, "--restore-point");
        bool madePoint = false;
        if (wantsPoint)
        {
            var (ok, reason) = RestorePoint.Create();
            madePoint = ok;
            if (!ok) o.WriteLine($"No restore point: {reason}. The receipt still records every change.");
        }

        var receipt = engine.Apply(plan, madePoint);
        if (json) { Json(o, new { receipt.Id, receipt.Changed, receipt.Failed, restorePoint = madePoint }); return receipt.Failed > 0 ? 2 : 0; }

        o.WriteLine($"Changed {receipt.Changed} value(s). Receipt {receipt.Id}.");
        foreach (var c in receipt.Registry.Where(c => c.Failed)) o.WriteLine($"  failed: {c.Path}: {c.Error}");
        if (receipt.Failed > 0) o.WriteLine("Some changes need an administrator. Run this from an elevated prompt.");
        if (plan.NeedsRestart) o.WriteLine("Some of it takes effect after a restart.");
        o.WriteLine($"Put it all back with: stay undo {receipt.Id}");
        return receipt.Failed > 0 ? 2 : 0;
    }

    // ---------- receipts ----------

    private static int Receipts(TextWriter o, bool json)
    {
        var list = new FileReceiptStore().List();
        if (json) { Json(o, list.Select(r => new { r.Id, r.When, r.Changed, r.Failed, r.Undone })); return 0; }
        if (list.Count == 0) { o.WriteLine("No receipts. Nothing has been changed by this app on this PC."); return 0; }
        foreach (var r in list)
            o.WriteLine($"{r.Id}  {r.When:d MMM yyyy HH:mm}  {r.Changed} change(s){(r.Failed > 0 ? $", {r.Failed} failed" : "")}{(r.Undone is not null ? "  (undone)" : "")}");
        o.WriteLine();
        o.WriteLine("Put one back with: stay undo <id>");
        return 0;
    }

    private static int Undo(string[] args, TextWriter o)
    {
        var id = args.Skip(1).FirstOrDefault(a => !a.StartsWith("--"));
        if (id is null) { o.WriteLine("stay undo <receipt id>. Run: stay receipts"); return 64; }

        var store = new FileReceiptStore();
        var receipt = store.List().FirstOrDefault(r => r.Id == id);
        if (receipt is null) { o.WriteLine($"stay: no receipt {id}. Run: stay receipts"); return 64; }

        var result = MakeEngine().Undo(receipt);
        o.WriteLine($"Put back {result.Restored} value(s).");
        foreach (var e in result.Errors) o.WriteLine($"  failed: {e}");
        return result.Errors.Count > 0 ? 2 : 0;
    }

    // ---------- everything at once ----------

    private static int Report(TextWriter o, bool json)
    {
        var engine = MakeEngine();
        var standing = engine.Scan();
        if (json)
        {
            Json(o, new
            {
                host = Environment.MachineName,
                user = Environment.UserName,
                when = DateTimeOffset.Now,
                version = Version,
                datesChecked = Lifecycle.Checked,
                windows = standing.Windows,
                esu = new { state = standing.Esu.State.ToString(), standing.Esu.Detail },
                patched = standing.Patched,
                components = standing.Components,
                eleven = new { standing.Eleven.Ready, requirements = standing.Eleven.Requirements },
                guards = standing.Guards.Where(g => engine.Applies(g.Guard, standing.Windows))
                    .Select(g => new { g.Guard.Id, g.Guard.Title, state = g.State.ToString() }),
                receipts = engine.Store.List().Select(r => new { r.Id, r.When, r.Changed, r.Undone }),
            });
            return 0;
        }

        o.WriteLine($"Stay for Windows 10 {Version} — {Environment.MachineName}, {DateTimeOffset.Now:d MMMM yyyy HH:mm}");
        o.WriteLine(new string('-', 72));
        Status(o, false);
        o.WriteLine(new string('-', 72));
        Updates(o, false);
        o.WriteLine(new string('-', 72));
        Eleven(o, false);
        o.WriteLine(new string('-', 72));
        GuardList(o, false);
        return 0;
    }

    public const string Help = """
      Stay for Windows 10 — where this PC stands now that Windows 10 is not supported,
      and the things worth shutting on a PC nobody is patching any more.

        stay status                  is Windows on this PC being patched, and until when
        stay updates                 everything here that still gets security updates, and the date each stops
        stay eleven                  whether this PC could take Windows 11, and which check fails
        stay guards                  what is open that this app can shut
        stay harden --all            shut everything it suggests, with a receipt
        stay harden --id rdp,smb1    shut named ones
        stay harden --all --dry-run  show the exact changes and make none
        stay harden --all --restore-point   ask Windows for a restore point first
        stay receipts                every change this app has made on this PC
        stay undo <id>               put a receipt's changes back exactly
        stay report                  all of the above, for keeping or sending
        stay selftest                the engine's own checks
        stay version

      --json works on every command that reports something.
      Exit codes: 0 fine, 1 something to look at, 2 problem, 64 usage.

      Changing anything under HKLM needs an administrator. Everything else reads fine without.
      """;
}
