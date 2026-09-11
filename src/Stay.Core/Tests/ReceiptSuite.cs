namespace Stay.Core.Tests;

public static class ReceiptSuite
{
    public static Suite Run()
    {
        var s = new Suite("receipts");
        var when = new DateTimeOffset(2026, 9, 10, 14, 30, 0, TimeSpan.Zero);
        var change = new RegChange("rdp", Hive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Terminal Server",
                                   "fDenyTSConnections", RegValue.Absent, RegValue.DWord(1));
        var receipt = new Receipt(Receipt.NewId(when), when, "PC", "sam", "1.0.0", new[] { change }, false);

        s.Equal("the id is the moment it happened", "20260910-143000", receipt.Id);
        s.Equal("one change counted", 1, receipt.Changed);

        var back = Receipt.FromJson(receipt.ToJson());
        s.Equal("a receipt survives being written and read", receipt.Id, back.Id);
        s.Equal("with its values intact", RegValue.DWord(1), back.Registry[0].After);
        s.Equal("including what was there before", RegValue.Absent, back.Registry[0].Before);
        s.Equal("and which guard it belonged to", "rdp", back.Registry[0].GuardId);
        s.Check("the path reads as a person would write it", change.Path.StartsWith("HKLM\\"));

        var failed = change with { Error = "no" };
        s.Check("a failed change knows it", failed.Failed);
        s.Equal("and is not counted as a change",
            0, new Receipt("x", when, "PC", "sam", "1.0.0", new[] { failed }, false).Changed);

        s.Throws<InvalidDataException>("something that is not a receipt is refused", () => Receipt.FromJson("null"));
        return s;
    }
}
