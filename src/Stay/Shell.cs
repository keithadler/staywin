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

    public Shell(Engine engine)
    {
        _engine = engine;
        _standing = engine.Scan();
        Guards = new ObservableCollection<GuardRow>();
        Components = new ObservableCollection<ComponentRow>();
        Requirements = new ObservableCollection<RequirementRow>();
        Receipts = new ObservableCollection<ReceiptRow>();
        Fill();
    }

    public ObservableCollection<GuardRow> Guards { get; }
    public ObservableCollection<ComponentRow> Components { get; }
    public ObservableCollection<RequirementRow> Requirements { get; }
    public ObservableCollection<ReceiptRow> Receipts { get; }

    private void Fill()
    {
        var suggested = _engine.Suggested(_standing).Select(g => g.Id).ToHashSet();

        Guards.Clear();
        foreach (var status in _standing.Guards.Where(g => _engine.Applies(g.Guard, _standing.Windows)))
            Guards.Add(new GuardRow(status, _engine.Expand(status.Guard), suggested.Contains(status.Guard.Id)));

        Components.Clear();
        foreach (var c in _standing.Components) Components.Add(new ComponentRow(c, _standing.Today));

        Requirements.Clear();
        foreach (var r in _standing.Eleven.Requirements) Requirements.Add(new RequirementRow(r));

        Receipts.Clear();
        foreach (var r in _engine.Store.List()) Receipts.Add(new ReceiptRow(r));

        foreach (var name in new[] { nameof(Headline), nameof(HeadlineDetail), nameof(Patched), nameof(EsuOffered),
                                     nameof(OpenCount), nameof(OpenSummary), nameof(ElevenSummary), nameof(WindowsLine),
                                     nameof(HasReceipts), nameof(ByHand), nameof(DatesChecked) })
            PropertyChanged?.Invoke(this, new(name));
    }

    public void Rescan() { _standing = _engine.Scan(); Fill(); }

    // ---------- the sentence at the top ----------

    public bool Patched => _standing.Patched;

    public string Headline => _standing.Windows.IsWindows11
        ? "This is Windows 11."
        : _standing.Patched
            ? "Windows on this PC is still getting security updates."
            : "Windows on this PC is not getting security updates.";

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

    // ---------- doing things ----------

    public Plan PlanSelected() => _engine.PlanFor(Guards.Where(g => g.Selected && g.CanSelect).Select(g => g.Status.Guard));

    public Receipt Apply(Plan plan, bool restorePoint) => _engine.Apply(plan, restorePoint);

    public UndoResult Undo(Receipt receipt) => _engine.Undo(receipt);

    public static void Open(string what)
    {
        try { Process.Start(new ProcessStartInfo(what) { UseShellExecute = true }); }
        catch (Exception e) { App.Log($"could not open {what}: {e.Message}"); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
