namespace Stay.Core.Tests;

public static class LifecycleSuite
{
    public static Suite Run()
    {
        var s = new Suite("dates");
        var today = new DateOnly(2026, 9, 10);

        s.Check("Windows 10 support has already ended", Lifecycle.Windows10Ended < today);
        s.Check("consumer ESU has not ended yet", Lifecycle.ConsumerEsuEnds > today);
        s.Check("ESU runs past the original one-year window",
            Lifecycle.ConsumerEsuEnds > new DateOnly(2026, 10, 13),
            "Microsoft extended it to 2027; an app saying 2026 would send people off a cliff a year early");

        s.Check("LTSC and IoT LTSC are not the same date", Lifecycle.LtscEnds != Lifecycle.IotLtscEnds);
        s.Check("IoT LTSC is the long one", Lifecycle.IotLtscEnds > Lifecycle.LtscEnds);
        s.Check("Edge outlives Windows 10's own support", Lifecycle.EdgeEnds > Lifecycle.ConsumerEsuEnds);
        s.Check("Defender definitions outlive it too", Lifecycle.DefenderEnds > Lifecycle.ConsumerEsuEnds);
        s.Check("Office outlives it as well", Lifecycle.M365Ends > Lifecycle.ConsumerEsuEnds);

        s.Check("the dates say when they were last checked", Lifecycle.Checked.Year == 2026);
        s.Check("every enrolment route is named", Lifecycle.EsuRoutes.Length == 3);
        s.Check("what ESU is not is said out loud",
            Lifecycle.WhatEsuIs.Contains("Not bug fixes"));

        s.Equal("a year away reads as months", "in 12 months", Lifecycle.HowLong(today.AddDays(365), today));
        s.Equal("next week reads as days", "in 8 days", Lifecycle.HowLong(today.AddDays(8), today));
        s.Equal("a fortnight reads as weeks", "in 2 weeks", Lifecycle.HowLong(today.AddDays(20), today));
        s.Equal("the past reads as the past", "8 days ago", Lifecycle.HowLong(today.AddDays(-8), today));
        s.Equal("today is today", "today", Lifecycle.HowLong(today, today));
        s.Equal("days left is arithmetic, not the clock", 10, Lifecycle.DaysUntil(today.AddDays(10), today));
        return s;
    }
}
