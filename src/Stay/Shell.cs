using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using Stay.Core;

namespace Stay;

/// <summary>One row on the "what to shut" pane.</summary>
public sealed class GuardRow : INotifyPropertyChanged
{
    public GuardStatus Status { get; }
    public IReadOnlyList<string> ExactChanges { get; }

    private bool _selected;
    public bool Selected
    {
        get => _selected;
        set { _selected = value; PropertyChanged?.Invoke(this, new(nameof(Selected))); }
    }

    public GuardRow(GuardStatus status, IReadOnlyList<RegEdit> edits, bool selected)
    {
        Status = status;
        _selected = selected;
        ExactChanges = edits.Select(e => $"{e.Path}\\{e.Name} = {e.Wanted}").ToList();
    }

    public string Id => Status.Guard.Id;
    public string Title => Status.Guard.Title;
    public string What => Status.Guard.What;
    public string Does => Status.Guard.Does;
    public string Costs => Status.Guard.Costs;
    public bool CanSelect => Status.NeedsDoing;
    public string StateWord => Status.State switch
    {
        GuardState.Closed => "shut",
        GuardState.Partly => "half shut",
        _ => "open",
    };
    public string StateKey => Status.State switch
    {
        GuardState.Closed => "Shut",
        GuardState.Partly => "Partly",
        _ => "Loud",
    };
    public string Note => Status.Guard.NeedsRestart ? "Takes effect after a restart." : "";
    public string CostWord => Status.Guard.Cost switch
    {
        Cost.None => "costs you nothing",
        Cost.Small => "small cost",
        _ => "real cost",
    };

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class ComponentRow
{
    public ComponentRow(Core.Component component, DateOnly today)
    {
        Title = component.Title;
        What = component.What;
        Detail = component.Detail;
        Source = "source: " + component.Source;
        Installed = component.Installed;
        Over = component.Over(today);
        When = component.Ends is null ? "no end announced"
             : Over ? $"ended {component.Ends:d MMMM yyyy}, {Lifecycle.HowLong(component.Ends.Value, today)}"
             : $"until {component.Ends:d MMMM yyyy}, {Lifecycle.HowLong(component.Ends.Value, today)}";
        StateKey = Over ? "Loud" : Installed ? "Shut" : "Partly";
        StateWord = Over ? "over" : Installed ? "still updating" : "not installed";
    }

    public string Title { get; }
    public string What { get; }
    public string Detail { get; }
    public string Source { get; }
    public string When { get; }
    public bool Installed { get; }
    public bool Over { get; }
    public string StateKey { get; }
    public string StateWord { get; }
}

public sealed class RequirementRow
{
    public RequirementRow(Requirement r)
    {
        Title = r.Title;
        Found = "found: " + r.Found;
        Needed = r.Result == CheckResult.Pass ? "" : "needs: " + r.Needed;
        Remedy = r.Remedy ?? "";
        StateKey = r.Result switch { CheckResult.Pass => "Shut", CheckResult.Fail => "Loud", _ => "Partly" };
        StateWord = r.Result switch { CheckResult.Pass => "yes", CheckResult.Fail => "no", _ => "cannot tell" };
    }

    public string Title { get; }
    public string Found { get; }
    public string Needed { get; }
    public string Remedy { get; }
    public string StateKey { get; }
    public string StateWord { get; }
}

/// <summary>One program that starts with the PC, on the speed pane.</summary>
/// <summary>One thing wrong with how this PC is being looked after.</summary>
/// <summary>One switch that has turned itself back on.</summary>
/// <summary>One browser on this PC, and what its maker has actually committed to.</summary>
public sealed class BrowserRow
{
    public BrowserRow(Browser browser, DateOnly today)
    {
        var (until, said) = Lifecycle.BrowserSupport(browser.Name);
        Name = browser.Name + (browser.Default ? "  (opens your links)" : "");
        Version = "Version " + browser.Version;
        Said = said;
        StateKey = until is null ? "Partly" : until > today ? "Shut" : "Loud";
        StateWord = until is null ? "no end date given" : $"until {until:MMMM yyyy}";
    }
    public string Name { get; }
    public string Version { get; }
    public string Said { get; }
    public string StateKey { get; }
    public string StateWord { get; }
}

public sealed class DriftRow : INotifyPropertyChanged
{
    public Drifted Drifted { get; }
    private bool _selected = true;
    public bool Selected { get => _selected; set { _selected = value; PropertyChanged?.Invoke(this, new(nameof(Selected))); } }

    public DriftRow(Drifted drifted) { Drifted = drifted; }

    public string What => Drifted.What;
    public string Why => Drifted.Why;
    public string When => $"You turned it off on {Drifted.When:d MMMM yyyy}.";
    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class WrongRow
{
    public WrongRow(Wrong wrong)
    {
        What = wrong.What; Why = wrong.Why; Fix = wrong.Fix;
        StateKey = wrong.Serious ? "Loud" : "Partly";
        StateWord = wrong.Serious ? "needs doing" : "worth knowing";
    }
    public string What { get; }
    public string Why { get; }
    public string Fix { get; }
    public string StateKey { get; }
    public string StateWord { get; }
}

public sealed class StartupRow : INotifyPropertyChanged
{
    public StartupEntry Entry { get; }
    private bool _selected;
    public bool Selected { get => _selected; set { _selected = value; PropertyChanged?.Invoke(this, new(nameof(Selected))); } }

    public StartupRow(StartupEntry entry) { Entry = entry; }

    public string Name => Entry.Name;
    public string Command => Entry.Command;
    public string Cost => Entry.Cost;
    public bool CanSelect => Entry.Enabled;
    public string StateKey => !Entry.Enabled ? "Shut" : Entry.Milliseconds is > 2000 ? "Loud" : "Partly";
    public string StateWord => !Entry.Enabled ? "already off" : Entry.Milliseconds is null ? "starts with the PC" : Entry.Cost;
    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>One bundled app on the junk pane.</summary>
public sealed class AppRow : INotifyPropertyChanged
{
    public AppState App { get; }
    private bool _selected;
    public bool Selected { get => _selected; set { _selected = value; PropertyChanged?.Invoke(this, new(nameof(Selected))); } }

    public AppRow(AppState app, bool selected) { App = app; _selected = selected; }

    public string Title => App.Title;
    public string What => App.What;
    public string Publisher => App.App.Publisher;
    public string StateKey => App.Advice switch { AppAdvice.Junk => "Loud", AppAdvice.Keep => "Shut", _ => "Partly" };
    public string StateWord => App.Advice switch
    {
        AppAdvice.Junk => "came with the PC",
        AppAdvice.Keep => "worth keeping",
        _ => "your call",
    };
    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class SpaceRow
{
    public SpaceRow(Reclaimable item) { What = item.What; Size = $"{item.Gb:0.#} GB"; How = item.How; }
    public string What { get; }
    public string Size { get; }
    public string How { get; }
}

public sealed class ReceiptRow
{
    public ReceiptRow(Receipt receipt)
    {
        Receipt = receipt;
        When = receipt.When.ToString("d MMMM yyyy, HH:mm");
        Summary = $"{receipt.Changed} change{(receipt.Changed == 1 ? "" : "s")}"
                + (receipt.Failed > 0 ? $", {receipt.Failed} failed" : "")
                + (receipt.RestorePoint ? ", restore point made" : "");
        Undone = receipt.Undone is not null;
        Lines = receipt.Registry.Select(c => $"{c.Path}: {c.Before} -> {c.After}").ToList();
    }

    public Receipt Receipt { get; }
    public string Id => Receipt.Id;
    public string When { get; }
    public string Summary { get; }
    public bool Undone { get; }
    public bool CanUndo => !Undone && Receipt.Changed > 0;
    public IReadOnlyList<string> Lines { get; }
}

/// <summary>
/// What the window shows, worked out from the engine once and then held. The window itself does no thinking:
/// everything it draws is a property here, so the same answers can be printed by the console twin.
/// </summary>
public sealed class Shell : INotifyPropertyChanged
{
    private readonly Engine _engine;
    private Standing _standing;
    private Pace _pace = null!;

    public Shell(Engine engine)
    {
        _engine = engine;
        _standing = engine.Scan();
        Guards = new ObservableCollection<GuardRow>();
        Junk = new ObservableCollection<GuardRow>();
        Speeds = new ObservableCollection<GuardRow>();
        Startup = new ObservableCollection<StartupRow>();
        Space = new ObservableCollection<SpaceRow>();
        Wrong = new ObservableCollection<WrongRow>();
        Apps = new ObservableCollection<AppRow>();
        Drift = new ObservableCollection<DriftRow>();
        Browsers = new ObservableCollection<BrowserRow>();
        Components = new ObservableCollection<ComponentRow>();
        Requirements = new ObservableCollection<RequirementRow>();
        Receipts = new ObservableCollection<ReceiptRow>();
        Fill();
    }

    public ObservableCollection<GuardRow> Guards { get; }
    public ObservableCollection<GuardRow> Junk { get; }
    public ObservableCollection<GuardRow> Speeds { get; }
    public ObservableCollection<StartupRow> Startup { get; }
    public ObservableCollection<SpaceRow> Space { get; }
    public ObservableCollection<WrongRow> Wrong { get; }
    public ObservableCollection<AppRow> Apps { get; }
    public ObservableCollection<DriftRow> Drift { get; }
    public ObservableCollection<BrowserRow> Browsers { get; }
    public ObservableCollection<ComponentRow> Components { get; }
    public ObservableCollection<RequirementRow> Requirements { get; }
    public ObservableCollection<ReceiptRow> Receipts { get; }

    private void Fill()
    {
        var suggested = _engine.Suggested(_standing).Select(g => g.Id).ToHashSet();

        Guards.Clear();
        foreach (var status in _standing.Guards.Where(g => _engine.Applies(g.Guard, _standing.Windows)))
            Guards.Add(new GuardRow(status, _engine.Expand(status.Guard), suggested.Contains(status.Guard.Id)));

        var loud = _engine.SuggestedJunk(_standing).Select(g => g.Id).ToHashSet();
        Junk.Clear();
        foreach (var status in _standing.Junk.Where(g => _engine.Applies(g.Guard, _standing.Windows)))
            Junk.Add(new GuardRow(status, status.Guard.Edits, loud.Contains(status.Guard.Id)));

        _pace = _engine.Pace();
        var faster = _engine.SuggestedPace(_pace).Select(g => g.Id).ToHashSet();
        Speeds.Clear();
        foreach (var status in _pace.Switches.Where(g => _engine.AppliesToPace(g.Guard, _pace.Disk)))
            Speeds.Add(new GuardRow(status, status.Guard.Edits, faster.Contains(status.Guard.Id)));

        Startup.Clear();
        foreach (var entry in _pace.Startup) Startup.Add(new StartupRow(entry));

        Space.Clear();
        foreach (var item in _pace.Space) Space.Add(new SpaceRow(item));

        Components.Clear();
        foreach (var c in _standing.Components) Components.Add(new ComponentRow(c, _standing.Today));

        Requirements.Clear();
        foreach (var r in _standing.Eleven.Requirements) Requirements.Add(new RequirementRow(r));

        Apps.Clear();
        foreach (var app in _standing.Apps) Apps.Add(new AppRow(app, app.Suggested));

        Browsers.Clear();
        foreach (var browser in _standing.Browsers) Browsers.Add(new BrowserRow(browser, _standing.Today));

        Drift.Clear();
        foreach (var drifted in _engine.Drift()) Drift.Add(new DriftRow(drifted));

        Wrong.Clear();
        foreach (var wrong in _standing.Wrong) Wrong.Add(new WrongRow(wrong));

        Receipts.Clear();
        foreach (var r in _engine.Store.List()) Receipts.Add(new ReceiptRow(r));

        foreach (var name in new[] { nameof(Headline), nameof(HeadlineDetail), nameof(Patched), nameof(EsuOffered),
                                     nameof(OpenCount), nameof(OpenSummary), nameof(ElevenSummary), nameof(WindowsLine),
                                     nameof(HasReceipts), nameof(ByHand), nameof(DatesChecked),
                                     nameof(BootLine), nameof(DiskLine), nameof(Truth), nameof(StartupSummary),
                                     nameof(SpaceSummary), nameof(HasSpace), nameof(JunkSummary),
                                     nameof(HasWrong), nameof(WrongSummary), nameof(LastUpdateLine),
                                     nameof(AppsSummary), nameof(HasApps),
                                     nameof(ElevenOnlyNote), nameof(HasElevenOnly),
                                     nameof(HasDrift), nameof(DriftSummary),
                                     nameof(HasBrowsers), nameof(BrowserNote),
                                     nameof(Because), nameof(HasBecause) })
            PropertyChanged?.Invoke(this, new(name));
    }

    public void Rescan() { _standing = _engine.Scan(); Fill(); }

    // ---------- the sentence at the top ----------

    public bool Patched => _standing.Patched;

    public string Headline => _standing.Windows.IsWindows11
        ? "This is Windows 11."
        // An app that cannot tell should say so rather than pick the alarming answer. Telling somebody who is
        // enrolled that they are not is how they end up paying twice, or giving up on a PC that was fine.
        : _standing.Esu.State == EsuState.Unknown
            ? "This app cannot tell whether Windows on this PC is being patched."
        : _standing.Working
            ? "Windows on this PC is still getting security updates."
            : _standing.Patched
                ? "This PC is entitled to security updates, but something is wrong."
                : "Windows on this PC is not getting security updates.";

    public bool HasBrowsers => Browsers.Count > 0;

    /// <summary>
    /// The most reassuring true thing this app has to say, and the one people get wrong. On an OS nobody is
    /// patching, nearly all of the real risk arrives through the browser — and on Windows 10 the browser is
    /// still being patched.
    /// </summary>
    public string BrowserNote =>
        "Almost everything that gets onto a PC arrives through the browser, so on a Windows that is not being "
        + "patched the browser matters more than anything else on this page. The good news is that it is still "
        + "being updated here, by every maker, and will be for years.";

    public bool HasDrift => Drift.Count > 0;

    public string DriftSummary => Drift.Count == 1
        ? "One thing you turned off has turned itself back on."
        : $"{Drift.Count} things you turned off have turned themselves back on.";

    public Plan PlanDrift() => _engine.PlanFor(Drift.Where(d => d.Selected).Select(d => d.Drifted));

    public bool HasWrong => Wrong.Count > 0;

    public string WrongSummary => Wrong.Count == 1
        ? "One thing is stopping this PC being looked after properly."
        : $"{Wrong.Count} things are stopping this PC being looked after properly.";

    /// <summary>The evidence the ESU answer was reached on, so somebody can disagree with it.</summary>
    public string Because => _standing.Esu.Because ?? "";
    public bool HasBecause => Because.Length > 0;

    public string LastUpdateLine => _standing.Watch.LastUpdate is { } landed
        ? $"The last update actually installed on {landed:d MMMM yyyy}, {Lifecycle.HowLong(landed, _standing.Today)}."
        : "Windows has no record of an update ever installing on this PC.";

    public string HeadlineDetail
    {
        get
        {
            if (_standing.Windows.IsWindows11)
                return "It is supported in the ordinary way, and there is nothing here you need. This app is for "
                     + "PCs still on Windows 10.";

            var windows = _standing.Components.FirstOrDefault(c => c.Id is "windows" or "ltsc");
            var detail = _standing.Esu.Detail;
            if (windows?.Ends is { } ends)
                detail += _standing.Patched
                    ? $" That runs out on {ends:d MMMM yyyy}, {Lifecycle.HowLong(ends, _standing.Today)}."
                    : $" Support ended on {ends:d MMMM yyyy}, {Lifecycle.HowLong(ends, _standing.Today)}.";
            return detail;
        }
    }

    public string WindowsLine => $"{_standing.Windows.Name} {_standing.Windows.Release}  ·  "
                               + $"{_standing.Windows.Version}  ·  {_standing.Windows.Architecture}";

    /// <summary>Whether to show the "here is how to join" panel, which is only worth space when it can be acted on.</summary>
    public bool EsuOffered => _standing.Esu.State == EsuState.NotEnrolled && !_standing.Windows.IsLtsc;

    public IReadOnlyList<string> EsuRoutes => Lifecycle.EsuRoutes;
    public string WhatEsuIs => Lifecycle.WhatEsuIs;
    public string DatesChecked => $"Dates last checked against Microsoft's own pages on {Lifecycle.Checked:d MMMM yyyy}.";

    public int OpenCount => Guards.Count(g => g.Status.NeedsDoing && g.Status.Guard.DefaultOn);
    public string OpenSummary => OpenCount == 0
        ? "Nothing is left open that this app would shut."
        : $"{OpenCount} thing{(OpenCount == 1 ? " is" : "s are")} open that this app can shut, each with a receipt that undoes it.";

    public string ElevenSummary => _standing.Eleven.Ready
        ? "This PC meets Windows 11's requirements."
        : _standing.Eleven.Fixable
            ? $"{_standing.Eleven.Failing.Count} check(s) fail, and every one of them is a setting rather than a new PC."
            : $"{_standing.Eleven.Failing.Count} check(s) fail. At least one cannot be changed on this PC.";

    public bool HasReceipts => Receipts.Count > 0;
    public IReadOnlyList<(string Title, string Why, string How)> ByHand => Core.Guards.ByHand;

    // ---------- what is making it slow ----------

    public string BootLine => _pace.LastBoot is { } boot
        ? $"This PC last took {boot.Seconds:0} seconds to start, on {boot.When:d MMMM} at {boot.When:HH:mm}."
        : "Windows has not recorded how long this PC takes to start.";

    public string DiskLine
    {
        get
        {
            var kind = _pace.Disk.Kind switch
            {
                DiskKind.Spinning => "a spinning hard disk",
                DiskKind.Solid => "solid state",
                _ => "of a kind Windows would not say",
            };
            return $"The disk is {kind}, with {_pace.Disk.FreeGb:0} GB free of {_pace.Disk.TotalGb:0}. "
                 + $"There is {_pace.MemoryGb:0.#} GB of memory.";
        }
    }

    public string Truth => _pace.Truth;

    public string StartupSummary => _pace.Running.Count == 0
        ? "Nothing starts with this PC."
        : $"{_pace.Running.Count} of {_pace.Startup.Count} programs start with this PC"
          + (_pace.SecondsAtStartup > 0
              ? $". Windows measured {_pace.SecondsAtStartup:0.0} seconds of that."
              : ". Windows has not measured what they cost.");

    public bool HasSpace => _pace.Space.Count > 0;
    public string SpaceSummary => $"{_pace.Reclaimable / 1024.0 / 1024 / 1024:0.#} GB is being held that could come back. "
        + "This app does not delete files: deleting cannot be undone by a receipt, so it tells you where it is instead.";

    /// <summary>
    /// Said out loud rather than quietly left out: the list this came from covers Windows 11 too, and a person
    /// comparing the two apps should know why this one is shorter.
    /// </summary>
    public string ElevenOnlyNote
    {
        get
        {
            int hidden = _standing.Junk.Count - Junk.Count;
            return hidden == 0 ? "" :
                $"{hidden} more switches in this list are Windows 11 features — Recall, Click to Do, the AI in "
                + "Paint and Notepad — which Windows 10 never had. They are not shown, because writing a value "
                + "that does nothing would leave you believing you had turned something off.";
        }
    }

    public bool HasElevenOnly => ElevenOnlyNote.Length > 0;

    public string JunkSummary => _standing.Loud == 0
        ? "Every advertising, AI and telemetry switch this app knows about is already off."
        : $"{_standing.Loud} of them are still on.";

    public Plan PlanSpeed()
    {
        var chosen = Speeds.Where(g => g.Selected && g.CanSelect).Select(g => g.Status.Guard).ToList();
        chosen.AddRange(Startup.Where(r => r.Selected && r.CanSelect).Select(r => _engine.Stop(r.Entry)));
        return _engine.PlanFor(chosen);
    }

    public Plan PlanJunk() => _engine.PlanFor(Junk.Where(g => g.Selected && g.CanSelect).Select(g => g.Status.Guard));

    public IReadOnlyList<AppState> AppsToRemove() => Apps.Where(a => a.Selected).Select(a => a.App).ToList();

    public bool HasApps => Apps.Count > 0;
    public string AppsSummary
    {
        get
        {
            int junk = Apps.Count(a => a.App.Suggested);
            return junk == 0
                ? "None of the apps on this PC are ones the list calls junk."
                : $"{junk} of the {Apps.Count} apps on this PC came with it and are the kind most people remove. "
                + "Removing one is the only thing this app does that a receipt cannot undo — the receipt keeps a "
                + "link to put it back from the Store instead.";
        }
    }

    // ---------- doing things ----------

    public Plan PlanSelected() => _engine.PlanFor(Guards.Where(g => g.Selected && g.CanSelect).Select(g => g.Status.Guard));

    public Receipt Apply(Plan plan, bool restorePoint) => _engine.Apply(plan, restorePoint);

    public Receipt Apply(Plan plan, IReadOnlyList<AppState> removing, bool restorePoint)
        => _engine.Apply(plan, removing, restorePoint);

    public UndoResult Undo(Receipt receipt) => _engine.Undo(receipt);

    public static void Open(string what)
    {
        try { Process.Start(new ProcessStartInfo(what) { UseShellExecute = true }); }
        catch (Exception e) { App.Log($"could not open {what}: {e.Message}"); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
