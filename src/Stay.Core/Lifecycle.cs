namespace Stay.Core;

/// <summary>
/// The dates. Every one of them is a published Microsoft date, written here with where it came from, because the
/// whole app is an argument about dates and an argument about dates is worth nothing without its sources.
///
/// These are facts about the world, not about this PC, so they are constants rather than something read off the
/// machine. They were last checked on <see cref="Checked"/>; the app says that date on screen, so nobody has to
/// wonder whether they are looking at something stale.
/// </summary>
public static class Lifecycle
{
    /// <summary>When a person last read these dates off Microsoft's own pages.</summary>
    public static readonly DateOnly Checked = new(2026, 9, 10);

    /// <summary>Windows 10 stopped being supported. Everything else in this file is measured from here.</summary>
    public static readonly DateOnly Windows10Ended = new(2025, 10, 14);

    /// <summary>
    /// Consumer Extended Security Updates. Originally a single year to 13 October 2026; Microsoft extended both
    /// enrolment and coverage to 12 October 2027. Critical and important security updates only — no bug fixes,
    /// no features, no support.
    /// </summary>
    public static readonly DateOnly ConsumerEsuEnds = new(2027, 10, 12);

    /// <summary>Windows 10 Enterprise LTSC 2021.</summary>
    public static readonly DateOnly LtscEnds = new(2027, 1, 12);

    /// <summary>Windows 10 IoT Enterprise LTSC 2021. Five years longer, and licensed for fixed-function devices.</summary>
    public static readonly DateOnly IotLtscEnds = new(2032, 1, 13);

    /// <summary>Microsoft Edge and the WebView2 runtime on Windows 10 22H2.</summary>
    public static readonly DateOnly EdgeEnds = new(2028, 10, 31);

    /// <summary>Microsoft Defender Antivirus security intelligence — the definitions, not the engine's host OS.</summary>
    public static readonly DateOnly DefenderEnds = new(2028, 10, 31);

    /// <summary>Microsoft 365 Apps: security updates only, after feature updates stop at version 2608.</summary>
    public static readonly DateOnly M365Ends = new(2028, 10, 10);

    public const string SourceWindows = "microsoft.com/windows/end-of-support";
    public const string SourceEsu = "microsoft.com/windows/extended-security-updates";
    public const string SourceLifecycle = "learn.microsoft.com/lifecycle/products";
    public const string SourceM365 = "learn.microsoft.com/microsoft-365-apps/end-of-support/windows-10-support";

    /// <summary>What ESU is, said plainly, because most of the disappointment about it comes from expecting more.</summary>
    public const string WhatEsuIs =
        "Critical and important security updates for Windows itself. Not bug fixes, not new features, not "
        + "technical support, and nothing for any other program on the PC.";

    /// <summary>The three ways in, as Microsoft offers them.</summary>
    public static readonly string[] EsuRoutes =
    {
        "Free, if you let Windows Backup sync your PC settings to a Microsoft account.",
        "Free, for 1,000 Microsoft Rewards points.",
        "$30 once, or the same in local money, plus tax. It covers up to ten PCs on the same account.",
    };

    /// <summary>
    /// What each browser maker has actually committed to for Windows 10, which is not the same as what people
    /// assume. Only Microsoft has named a date. Google has not published one at all, and Mozilla has said there
    /// is no end date rather than giving one.
    ///
    /// This matters more than any switch in this app. On an OS nobody is patching, the browser is where almost
    /// all of the real risk is, and on Windows 10 the browser is still being patched. Saying so is the most
    /// reassuring true thing there is to say.
    /// </summary>
    public static (DateOnly? Until, string Said) BrowserSupport(string name)
    {
        if (name.Contains("Edge", StringComparison.OrdinalIgnoreCase))
            return (EdgeEnds, "Microsoft has committed to updating Edge on Windows 10 until this date.");

        if (name.Contains("Chrome", StringComparison.OrdinalIgnoreCase))
            return (null, "Google has not published an end date for Chrome on Windows 10. Chrome and Edge are "
                        + "built on the same engine, and Microsoft is updating Edge here until October 2028, so "
                        + "Chrome is expected to keep going for a good while yet. That is an expectation, not a "
                        + "promise anybody has made.");

        if (name.Contains("Firefox", StringComparison.OrdinalIgnoreCase))
            return (null, "Mozilla has said it will keep supporting Windows 10 and has not set an end date. "
                        + "Firefox does not use Chrome's engine, so it does not depend on what Google or "
                        + "Microsoft decide.");

        return (null, "This app does not know what its maker has said about Windows 10. Worth asking them.");
    }

    /// <summary>How many days until a date, counted from a day the caller supplies rather than from the clock.</summary>
    public static int DaysUntil(DateOnly when, DateOnly today) => when.DayNumber - today.DayNumber;

    /// <summary>"in 3 weeks", "in 13 months", "8 days ago" — a length of time a person can feel.</summary>
    public static string HowLong(DateOnly when, DateOnly today)
    {
        int days = DaysUntil(when, today);
        int magnitude = Math.Abs(days);
        string size = magnitude switch
        {
            0 => "today",
            1 => "1 day",
            < 14 => $"{magnitude} days",
            < 60 => $"{magnitude / 7} weeks",
            < 730 => $"{magnitude / 30} months",
            _ => $"{magnitude / 365} years",
        };
        if (magnitude == 0) return "today";
        return days > 0 ? $"in {size}" : $"{size} ago";
    }
}
