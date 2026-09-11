namespace Stay.Core.Tests;

public static class GuardsSuite
{
    public static Suite Run()
    {
        var s = new Suite("what can be shut");

        s.Check("there is a catalogue at all", Guards.All.Count >= 12);
        s.Check("every guard has an id nobody else has",
            Guards.All.Select(g => g.Id).Distinct().Count() == Guards.All.Count);
        s.Check("every guard says what the thing is", Guards.All.All(g => g.What.Length > 20));
        s.Check("every guard says what shutting it does", Guards.All.All(g => g.Does.Length > 20));
        s.Check("every guard says what it costs you", Guards.All.All(g => g.Costs.Length > 10));
        s.Check("no guard is in no group", Guards.All.All(g => g.Group.Length > 0));

        s.Check("the two that really get in the way are not on by default",
            Guards.All.Where(g => g.Id is "cloud" or "lsa").All(g => !g.DefaultOn));
        s.Check("the one that uploads your files is not on by default",
            !Guards.Find("cloud")!.DefaultOn,
            "sending files to Microsoft is a choice somebody makes, not one an app makes quietly");
        s.Check("and it says so in the cost",
            Guards.Find("cloud")!.Costs.Contains("uploaded"));

        s.Check("turning off printing is only suggested with no printer", Guards.Find("spooler")!.OnlyIf == "noprinter");
        s.Check("the Office guard is only suggested with Office", Guards.Find("macros")!.OnlyIf == "office");
        s.Check("the guards that need a restart say so",
            Guards.All.Where(g => g.Id is "smb1" or "webdav" or "spooler" or "lsa").All(g => g.NeedsRestart));

        s.Check("every guard except the per-adapter one carries its edits",
            Guards.All.Where(g => g.Id != "netbios").All(g => g.Edits.Count > 0));
        s.Check("NetBIOS is left to be read off the machine", Guards.Find("netbios")!.Edits.Count == 0);

        s.Check("no edit is missing a value name", Guards.All.SelectMany(g => g.Edits).All(e => e.Name.Length > 0));
        s.Check("no edit is missing a key", Guards.All.SelectMany(g => g.Edits).All(e => e.Key.Length > 0));
        s.Check("the ASR rules are the eight that matter",
            Guards.Find("asr")!.Edits.Count(e => e.Name.Length == 36) == 8);
        s.Check("and they are written as strings, which is what Defender reads",
            Guards.Find("asr")!.Edits.Where(e => e.Name.Length == 36).All(e => e.Wanted.Kind == ValueKind.String));

        s.Check("clearing a pause deletes values rather than setting them",
            Guards.Find("paused")!.Edits.All(e => e.Wanted.Kind == ValueKind.Absent));

        s.Check("the things it will not do for you are listed with how to do them",
            Guards.ByHand.Count >= 3 && Guards.ByHand.All(b => b.How.Length > 20));
        s.Check("and the list explains why each is worth doing",
            Guards.ByHand.All(b => b.Why.Length > 20));
        return s;
    }
}
