namespace Stay.Core;

/// <summary>
/// Whether this PC is actually being looked after, as opposed to entitled to be.
///
/// Enrolment is a claim about what should happen. This is what did happen: the day the last update really
/// arrived, whether a restart has been waiting for weeks, whether anything is watching for malware, whether the
/// firewall is on. An ESU enrolment that is not delivering is worse than none at all, because the person on the
/// other end believes they are covered.
/// </summary>
public sealed record Watch(
    DateOnly? LastUpdate,
    bool RestartPending,
    bool UpdatesReachable,
    DateTimeOffset? LastChecked,
    Guarded Guarded)
{
    /// <summary>How long since an update really landed. Null when Windows has no record of one.</summary>
    public int? DaysSinceUpdate(DateOnly today) => LastUpdate is null ? null : today.DayNumber - LastUpdate.Value.DayNumber;

    /// <summary>
    /// Two months without an update on a PC that is supposed to be getting them. Windows ships security fixes on
    /// the second Tuesday of every month, so one missed month is bad luck and two is something being wrong.
    /// </summary>
    public bool Stale(DateOnly today) => DaysSinceUpdate(today) is > 62;
}

/// <summary>What is watching this PC, and whether it is switched on.</summary>
public sealed record Guarded(
    string Antivirus,
    bool AntivirusOn,
    bool RealTimeOn,
    int? SignatureAgeDays,
    bool FirewallOn)
{
    /// <summary>Definitions older than a week are not definitions.</summary>
    public bool SignaturesStale => SignatureAgeDays is > 7;

    public bool AllWell => AntivirusOn && RealTimeOn && FirewallOn && !SignaturesStale;
}

/// <summary>One thing wrong with how this PC is being looked after, and what to do about it.</summary>
public sealed record Wrong(string What, string Why, string Fix, bool Serious);

public static class Watching
{
    /// <summary>
    /// Everything the app would put in front of somebody, worst first. Nothing here is a guess: each one is a
    /// state read off the PC that has a plain consequence.
    /// </summary>
    public static IReadOnlyList<Wrong> WhatIsWrong(Watch watch, Esu esu, DateOnly today, bool windows10)
    {
        var found = new List<Wrong>();
        if (!windows10) return found;

        if (watch.Stale(today) && esu.State == EsuState.Enrolled)
            found.Add(new Wrong(
                $"Enrolled in Extended Security Updates, but the last update landed {Months(watch.DaysSinceUpdate(today)!.Value)} ago.",
                "Enrolment is a claim about what should happen. This is what did. Something is stopping the updates "
                + "arriving, and until it is fixed the enrolment is buying nothing.",
                "Settings > Update & Security > Windows Update, then Check for updates, and look at what it says.",
                Serious: true));
        else if (watch.Stale(today) && esu.State != EsuState.Enrolled)
            found.Add(new Wrong(
                $"The last update this PC installed was {Months(watch.DaysSinceUpdate(today)!.Value)} ago.",
                "That is what an unsupported Windows looks like from the inside: nothing arriving, and no sign "
                + "that anything is missing.",
                "Joining Extended Security Updates is what starts them again.",
                Serious: false));

        if (watch.RestartPending)
            found.Add(new Wrong(
                "An update is installed and waiting for a restart.",
                "A security fix that has not restarted is a security fix that is not protecting anything yet.",
                "Restart the PC. If it has been asking for weeks, restart it from Start > Power > Restart rather "
                + "than closing the lid, which on Windows 10 is not the same thing.",
                Serious: true));

        if (!watch.UpdatesReachable)
            found.Add(new Wrong(
                "The Windows Update service is switched off.",
                "Nothing will arrive while it is, whatever this PC is entitled to. Something turned it off, and "
                + "on an old PC that is usually a tuning guide somebody followed years ago.",
                "It is one of the switches on the Speed page, in reverse: set the Windows Update service back to "
                + "Manual. This app will do it if you ask.",
                Serious: true));

        if (!watch.Guarded.AntivirusOn)
            found.Add(new Wrong(
                "Nothing is watching this PC for malware.",
                "On an OS that is still patched that is bad. On one that is not, it is the only thing standing "
                + "between a known hole and somebody using it.",
                "Windows Security > Virus & threat protection, and turn it on.",
                Serious: true));
        else if (!watch.Guarded.RealTimeOn)
            found.Add(new Wrong(
                $"{watch.Guarded.Antivirus} is installed but not watching in real time.",
                "It will find things when it scans, which is not the same as stopping them when they arrive.",
                "Windows Security > Virus & threat protection > Manage settings, and turn on real-time protection.",
                Serious: true));

        if (watch.Guarded.SignaturesStale)
            found.Add(new Wrong(
                $"Virus definitions are {watch.Guarded.SignatureAgeDays} days old.",
                "Definitions are updated several times a day and keep coming to Windows 10 until October 2028. "
                + "Ones this old mean they are not arriving.",
                "Windows Security > Virus & threat protection > Check for updates.",
                Serious: true));

        if (!watch.Guarded.FirewallOn)
            found.Add(new Wrong(
                "The firewall is off.",
                "The firewall is what stops the holes in an unpatched Windows being reachable from anywhere but "
                + "this PC. With it off, they are reachable from the network.",
                "Windows Security > Firewall & network protection, and turn it on for every profile.",
                Serious: true));

        return found.OrderByDescending(w => w.Serious).ToList();
    }

    private static string Months(int days) => days switch
    {
        < 45 => $"{days} days",
        < 365 => $"{days / 30} months",
        _ => $"{days / 365} year{(days / 365 == 1 ? "" : "s")}",
    };
}
