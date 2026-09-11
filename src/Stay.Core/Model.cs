namespace Stay.Core;

// ---------- the registry, as much of it as this app needs ----------
//
// Shared in shape with Quiet for Windows on purpose: the same author wrote both, the receipt is the same promise,
// and a person who has used one already knows what a change here looks like.

/// <summary>Which part of the registry an edit lives in.</summary>
public enum Hive { CurrentUser, LocalMachine, NetworkService }

/// <summary>A registry value as the engine sees it. Absent means "no such value".</summary>
///
/// Binary is here for one reason: the way Windows records which startup programs are switched off is a twelve
/// byte value under StartupApproved, and putting one back exactly is the whole promise of a receipt. It is held
/// as hexadecimal text so that a receipt written to JSON reads back as the same value it was.
public enum ValueKind { Absent, DWord, String, Binary }

public sealed record RegValue(ValueKind Kind, long Number = 0, string Text = "")
{
    public static readonly RegValue Absent = new(ValueKind.Absent);
    public static RegValue DWord(long n) => new(ValueKind.DWord, n);
    public static RegValue Str(string s) => new(ValueKind.String, 0, s);
    public static RegValue Bytes(byte[] b) => new(ValueKind.Binary, 0, Convert.ToHexString(b));
    public byte[] AsBytes() => Kind == ValueKind.Binary ? Convert.FromHexString(Text) : Array.Empty<byte>();

    public override string ToString() => Kind switch
    {
        ValueKind.Absent => "(absent)",
        ValueKind.DWord => Number.ToString(),
        ValueKind.Binary => Text.Length == 0 ? "(empty)" : Text,
        _ => $"\"{Text}\"",
    };
}

/// <summary>One registry value and what it should be when the change is in place.</summary>
public sealed record RegEdit(Hive Hive, string Key, string Name, RegValue Wanted)
{
    public string Path => $"{HiveName(Hive)}\\{Key}";
    public static string HiveName(Hive h) => h switch
    {
        Hive.CurrentUser => "HKCU",
        Hive.LocalMachine => "HKLM",
        _ => "HKU\\S-1-5-20",
    };
}

// ---------- where this PC stands ----------

/// <summary>What Windows says about itself. Read once, then everything else is arithmetic on it.</summary>
public sealed record WindowsBuild(
    string Caption,      // "Microsoft Windows 10 Pro"
    string Edition,      // "Professional", "EnterpriseS" for LTSC, "IoTEnterpriseS" for IoT LTSC
    string Release,      // "22H2"
    int Major,           // 10
    int Build,           // 19045
    int Revision,        // 4291, the bit after the dot
    string Architecture) // "x64" / "arm64"
{
    public bool IsWindows10 => Major == 10 && Build < 22000;
    public bool IsWindows11 => Build >= 22000;
    /// <summary>22H2 is the only Windows 10 release still eligible for anything at all.</summary>
    public bool IsFinalRelease => Build >= 19045;
    public bool IsLtsc => Edition.Contains("EnterpriseS", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// What to call this Windows on screen.
    ///
    /// Windows 11 still writes "Windows 10 Pro" into ProductName in the registry, and has since it shipped. An
    /// app about the difference between 10 and 11 cannot repeat that back to somebody: the build number is the
    /// thing that is actually true, so where the two disagree the build wins.
    /// </summary>
    public string Name => IsWindows11 && Caption.Contains("Windows 10", StringComparison.OrdinalIgnoreCase)
        ? Caption.Replace("Windows 10", "Windows 11", StringComparison.OrdinalIgnoreCase)
        : Caption;
    public bool IsIotLtsc => Edition.Contains("IoTEnterpriseS", StringComparison.OrdinalIgnoreCase);
    public string Version => $"{Major}.0.{Build}.{Revision}";
}

/// <summary>Whether this PC is enrolled in Extended Security Updates, and how sure we are.</summary>
public enum EsuState
{
    /// <summary>A licence is present and active. The OS is being patched.</summary>
    Enrolled,
    /// <summary>Nothing says it is enrolled. The OS is not being patched.</summary>
    NotEnrolled,
    /// <summary>This edition does not use consumer ESU, so the question does not apply.</summary>
    NotApplicable,
    /// <summary>The check could not be run — not enough rights, or the service did not answer.</summary>
    Unknown,
}

public sealed record Esu(EsuState State, string Detail, string? LicenceName = null, DateTimeOffset? Until = null);

/// <summary>One thing on this PC that gets security updates, and the date it stops getting them.</summary>
public sealed record Component(
    string Id,
    string Title,
    string What,
    DateOnly? Ends,
    bool Installed,
    string Detail,
    string Source)
{
    public int? DaysLeft(DateOnly today) => Ends is null ? null : Ends.Value.DayNumber - today.DayNumber;
    public bool Over(DateOnly today) => Ends is not null && Ends.Value < today;
}

// ---------- can this PC take Windows 11 ----------

public enum CheckResult { Pass, Fail, Unknown }

/// <summary>One of Windows 11's hardware requirements, and whether this PC meets it.</summary>
public sealed record Requirement(string Id, string Title, CheckResult Result, string Found, string Needed, string? Remedy = null);

public sealed record Eleven(IReadOnlyList<Requirement> Requirements)
{
    public bool Ready => Requirements.All(r => r.Result == CheckResult.Pass);
    public IReadOnlyList<Requirement> Failing => Requirements.Where(r => r.Result == CheckResult.Fail).ToList();
    public IReadOnlyList<Requirement> Unsure => Requirements.Where(r => r.Result == CheckResult.Unknown).ToList();
    /// <summary>A failing check somebody can do something about, rather than one that needs a new PC.</summary>
    public bool Fixable => Failing.Count > 0 && Failing.All(r => r.Remedy is not null);
}

// ---------- what can be shut off ----------

/// <summary>How much a change is likely to get in the way, so the window can say so before it is made.</summary>
public enum Cost { None, Small, Real }

/// <summary>One thing the app can turn off or on, as a group of registry edits with a plain-English account of it.</summary>
public sealed record Guard(
    string Id,
    string Group,
    string Title,
    string What,     // what the thing is
    string Does,     // what turning it off does for you
    string Costs,    // what you lose
    IReadOnlyList<RegEdit> Edits,
    bool DefaultOn,
    bool NeedsSignOut = false,
    bool NeedsRestart = false,
    Cost Cost = Cost.Small,
    string? OnlyIf = null); // a condition the engine checks before suggesting it

public enum GuardState { Closed, Open, Partly }

public sealed record GuardStatus(Guard Guard, GuardState State, IReadOnlyList<RegValue> Current)
{
    public bool NeedsDoing => State != GuardState.Closed;
}

/// <summary>Everything the app found, in one answer.</summary>
/// <summary>What the engine knows about a bundled app, from the catalogue.</summary>
public enum AppAdvice { Junk, Optional, Keep }

public sealed record AppEntry(string Name, string Title, string What, AppAdvice Advice);

/// <summary>An app package installed on this PC.</summary>
public sealed record InstalledApp(
    string FamilyName,
    string Name,
    string FullName,
    string DisplayName,
    string Publisher,
    string Version,
    bool IsFramework,
    bool IsSystem);

public sealed record AppState(InstalledApp App, AppEntry? Entry)
{
    public string Title => Entry?.Title ?? (string.IsNullOrWhiteSpace(App.DisplayName) ? App.Name : App.DisplayName);
    public string What => Entry?.What ?? "Not in the list this app carries. It came with the PC or was installed "
                                       + "later; leave it unless you know what it is.";
    public AppAdvice Advice => Entry?.Advice ?? AppAdvice.Optional;
    public bool Suggested => Advice == AppAdvice.Junk;
    public string StoreLink => $"ms-windows-store://pdp/?PFN={App.FamilyName}";
}

public sealed record Standing(
    WindowsBuild Windows,
    Esu Esu,
    IReadOnlyList<Component> Components,
    Eleven Eleven,
    IReadOnlyList<GuardStatus> Guards,
    IReadOnlyList<GuardStatus> Junk,
    IReadOnlyList<AppState> Apps,
    Watch Watch,
    DateOnly Today)
{
    public int Open => Guards.Count(g => g.NeedsDoing && g.Guard.DefaultOn);
    public int Closed => Guards.Count(g => !g.NeedsDoing);
    /// <summary>Counted over what actually applies here, so the number matches what is on the screen.</summary>
    public int Loud => Junk.Count(g => g.NeedsDoing && g.Guard.DefaultOn && g.Guard.OnlyIf != "win11");

    /// <summary>What is wrong with how this PC is being looked after, worst first.</summary>
    public IReadOnlyList<Wrong> Wrong => Watching.WhatIsWrong(Watch, Esu, Today, Windows.IsWindows10);

    /// <summary>Entitled to updates, which is not the same as getting them; see <see cref="Working"/>.</summary>
    public bool Patched => Windows.IsWindows11
        || Esu.State == EsuState.Enrolled
        || (Windows.IsLtsc && Components.FirstOrDefault(c => c.Id == "ltsc") is { } l && !l.Over(Today));

    /// <summary>
    /// Entitled to updates and actually receiving them. The two come apart more often than anybody expects, and
    /// the gap between them is the most useful thing this app can tell somebody.
    /// </summary>
    public bool Working => Patched && !Wrong.Any(w => w.Serious);
}
