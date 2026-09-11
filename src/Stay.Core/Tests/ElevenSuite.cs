namespace Stay.Core.Tests;

public static class ElevenSuite
{
    public static Suite Run()
    {
        var s = new Suite("could this PC take Windows 11");

        var good = new FakeMachine();
        var ready = Elevenable.Check(good);
        s.Check("a PC that meets everything is ready", ready.Ready);
        s.Equal("and nothing is failing", 0, ready.Failing.Count);

        var noTpm = new FakeMachine { TpmChip = null };
        var missing = Elevenable.Check(noTpm);
        s.Check("no TPM fails", !missing.Ready);
        s.Equal("and names the check", "tpm", missing.Failing[0].Id);
        s.Check("and says to look in the firmware before giving up",
            missing.Failing[0].Remedy!.Contains("fTPM"));

        var offTpm = new FakeMachine { TpmChip = (false, "2.0") };
        s.Check("a TPM switched off is a fixable failure", Elevenable.Check(offTpm).Fixable);

        var oldTpm = new FakeMachine { TpmChip = (true, "1.2") };
        var old = Elevenable.Check(oldTpm);
        s.Check("a 1.2 chip fails", old.Failing.Any(r => r.Id == "tpm"));
        s.Check("and is honest that it usually cannot be changed",
            old.Failing.First(r => r.Id == "tpm").Remedy!.Contains("cannot"));

        var bios = new FakeMachine { Uefi = false, SecureBoot = null };
        var legacy = Elevenable.Check(bios);
        s.Check("legacy BIOS fails", legacy.Failing.Any(r => r.Id == "uefi"));
        s.Check("and names the converter", legacy.Failing.First(r => r.Id == "uefi").Remedy!.Contains("mbr2gpt"));
        s.Equal("Secure Boot that cannot be read is unknown, not failed",
            CheckResult.Unknown, legacy.Requirements.First(r => r.Id == "secureboot").Result);

        var small = new FakeMachine { Memory = 2L * 1024 * 1024 * 1024, Disk = 32L * 1024 * 1024 * 1024 };
        var tight = Elevenable.Check(small);
        s.Check("2 GB of memory fails", tight.Failing.Any(r => r.Id == "ram"));
        s.Check("32 GB of storage fails", tight.Failing.Any(r => r.Id == "disk"));
        s.Check("and the found value is shown, not just the verdict",
            tight.Failing.First(r => r.Id == "ram").Found.StartsWith("2"));

        // ---- the processor list ----
        s.Equal("Intel 8th generation is on it", true, Elevenable.Generation("Intel(R) Core(TM) i5-8250U CPU @ 1.60GHz", "GenuineIntel"));
        s.Equal("Intel 7th is not", false, Elevenable.Generation("Intel(R) Core(TM) i7-7700K CPU @ 4.20GHz", "GenuineIntel"));
        s.Equal("five digits are read as a two-digit generation", true, Elevenable.Generation("Intel(R) Core(TM) i9-12900K", "GenuineIntel"));
        s.Equal("Ryzen 3000 is on it", true, Elevenable.Generation("AMD Ryzen 5 3600 6-Core Processor", "AuthenticAMD"));
        s.Equal("a Ryzen 3000G is the Zen+ exception and is not", false, Elevenable.Generation("AMD Ryzen 5 3400G with Radeon Vega Graphics", "AuthenticAMD"));
        s.Equal("Ryzen 2000 is not", false, Elevenable.Generation("AMD Ryzen 5 2600 Six-Core Processor", "AuthenticAMD"));
        s.Equal("a Core 2 Duo needs no lookup", false, Elevenable.Generation("Intel(R) Core(TM)2 Duo CPU E8400", "GenuineIntel"));
        s.Equal("a Celeron is not on it", false, Elevenable.Generation("Intel(R) Celeron(R) N4020 CPU", "GenuineIntel"));
        s.Equal("a name it cannot read is unknown, not a guess", null, Elevenable.Generation("Fictional CPU 9000", "Other"));

        var strange = new FakeMachine { Processor = ("Fictional CPU 9000", "Other", 6, 1, 8, 3.0) };
        var unsure = Elevenable.Check(strange);
        s.Equal("an unreadable processor is reported as unsure", 1, unsure.Unsure.Count);
        s.Check("a PC with an unsure check is not called ready", !unsure.Ready);
        s.Check("and it is not called failing either", unsure.Failing.Count == 0);

        var slow = new FakeMachine { Processor = ("Intel(R) Core(TM) i7-8550U", "GenuineIntel", 6, 142, 1, 0.8) };
        s.Check("one slow core fails whatever the name says", Elevenable.Check(slow).Failing.Any(r => r.Id == "cpu"));

        var hopeless = new FakeMachine { Processor = ("Intel(R) Core(TM) i7-7700K CPU @ 4.20GHz", "GenuineIntel", 6, 158, 4, 4.2) };
        s.Check("a failing processor is not called fixable", !Elevenable.Check(hopeless).Fixable);
        return s;
    }
}
