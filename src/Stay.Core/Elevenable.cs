namespace Stay.Core;

/// <summary>
/// Whether this PC can take Windows 11, and which check is the one stopping it.
///
/// Microsoft's own tool answers yes or no. That is the least useful shape the answer can have, because three of
/// the four common failures are settings somebody can change in ten minutes and the fourth means buying a PC.
/// This says which, and what to do about it when there is something to do.
///
/// It does not tell you how to get round the requirements, and it will not gain that feature. An install that
/// bypasses the processor check is unsupported by Microsoft, may stop receiving updates, and putting a PC on an
/// OS that might not be patched is the exact problem this app exists to deal with.
/// </summary>
public static class Elevenable
{
    public const long MinMemory = 4L * 1024 * 1024 * 1024;
    public const long MinDisk = 64L * 1024 * 1024 * 1024;

    public static Eleven Check(IMachine machine)
    {
        var checks = new List<Requirement>
        {
            Tpm(machine),
            SecureBoot(machine),
            Firmware(machine),
            Processor(machine),
            Memory(machine),
            Disk(machine),
        };
        return new Eleven(checks);
    }

    private static Requirement Tpm(IMachine machine)
    {
        var tpm = machine.Tpm();
        if (tpm is null)
            return new("tpm", "A TPM 2.0 security chip", CheckResult.Fail, "none found", "TPM 2.0",
                "Many PCs of this age have one switched off in firmware. Look for TPM, fTPM, PTT or "
                + "\"Security Device\" in the firmware settings before concluding there is none.");

        var (enabled, version) = tpm.Value;
        bool two = version.StartsWith("2.", StringComparison.Ordinal);
        if (two && enabled) return new("tpm", "A TPM 2.0 security chip", CheckResult.Pass, $"TPM {version}, on", "TPM 2.0");
        if (two) return new("tpm", "A TPM 2.0 security chip", CheckResult.Fail, $"TPM {version}, switched off", "TPM 2.0",
            "Switch it on in the firmware settings. It is there — this PC has the chip.");
        return new("tpm", "A TPM 2.0 security chip", CheckResult.Fail, $"TPM {version}", "TPM 2.0",
            "A 1.2 chip cannot be made into a 2.0 one. On some business PCs of this era the firmware can be "
            + "updated to switch modes; on most it cannot.");
    }

    private static Requirement SecureBoot(IMachine machine)
    {
        var on = machine.SecureBootEnabled();
        if (on is null)
            return new("secureboot", "Secure Boot", CheckResult.Unknown, "could not be read", "on",
                "This usually means the PC boots the old way. See the firmware check below.");
        return on.Value
            ? new("secureboot", "Secure Boot", CheckResult.Pass, "on", "on")
            : new("secureboot", "Secure Boot", CheckResult.Fail, "off", "on",
                "Turn it on in the firmware settings. If Windows will not start afterwards, the disk is still "
                + "partitioned the old way — see the firmware check.");
    }

    private static Requirement Firmware(IMachine machine)
        => machine.UefiBoot()
            ? new("uefi", "UEFI firmware, not the old BIOS way", CheckResult.Pass, "UEFI", "UEFI")
            : new("uefi", "UEFI firmware, not the old BIOS way", CheckResult.Fail, "legacy BIOS", "UEFI",
                "Windows has a converter, mbr2gpt, that changes the disk without reinstalling. Back up first: "
                + "this one rewrites the partition table.");

    /// <summary>
    /// The processor list. Microsoft publishes thousands of model names rather than a rule, so this reads the
    /// name for the generation and says Unknown rather than guessing when the name is not one it can read.
    /// The line it draws is the published one: Intel 8th generation, AMD Zen 2, Qualcomm 8c and later.
    /// </summary>
    private static Requirement Processor(IMachine machine)
    {
        var (name, maker, _, _, cores, ghz) = machine.Cpu();
        const string needed = "Intel 8th generation, AMD Zen 2 (Ryzen 3000) or newer";

        if (cores < 2 || ghz < 1.0)
            return new("cpu", "A processor on Windows 11's list", CheckResult.Fail,
                $"{name} — {cores} core{(cores == 1 ? "" : "s")} at {ghz:0.0} GHz", needed,
                null);

        var generation = Generation(name, maker);
        return generation switch
        {
            true => new("cpu", "A processor on Windows 11's list", CheckResult.Pass, name, needed),
            false => new("cpu", "A processor on Windows 11's list", CheckResult.Fail, name, needed,
                null), // nothing to do about it: this is the one that means a different PC
            _ => new("cpu", "A processor on Windows 11's list", CheckResult.Unknown, name, needed,
                "This app could not read a generation out of that name. Microsoft publishes the full list by "
                + "model; search it for the name above."),
        };
    }

    /// <summary>True on the list, false off it, null when the name cannot be read.</summary>
    public static bool? Generation(string name, string maker)
    {
        // Windows reports these with the trademark marks in the middle of words: "Intel(R) Core(TM)2 Duo".
        // Taking them out first means one spelling to match rather than several.
        name = name.Replace("(R)", "", StringComparison.OrdinalIgnoreCase)
                   .Replace("(TM)", "", StringComparison.OrdinalIgnoreCase)
                   .Trim();

        // Intel Core i3/i5/i7/i9-8250U, -10510U, -12700K: the first one or two digits after the dash are the
        // generation. Eighth and later are on the list.
        // No word boundary after the digits: the model number is usually followed by a letter (8250U, 12900K),
        // and requiring one there meant this matched nothing at all.
        var intel = System.Text.RegularExpressions.Regex.Match(name, @"\bi[3579][- ](\d{4,5})");
        if (intel.Success)
        {
            var digits = intel.Groups[1].Value;
            int gen = digits.Length == 5 ? int.Parse(digits[..2]) : int.Parse(digits[..1]);
            return gen >= 8;
        }

        // AMD Ryzen 5 3600, Ryzen 7 5800X: the thousands digit is the family. Zen 2 is 3000 and up, but the
        // 3000-series APUs ending in G are Zen+ and are not on the list, which is why that case is separate.
        var amd = System.Text.RegularExpressions.Regex.Match(name, @"\bRyzen\s+\d\s+(\d{4})(\w*)\b");
        if (amd.Success)
        {
            int model = int.Parse(amd.Groups[1].Value);
            string suffix = amd.Groups[2].Value;
            if (model is >= 3000 and < 4000 && suffix.StartsWith("G", StringComparison.OrdinalIgnoreCase)) return false;
            return model >= 3000;
        }

        // Anything from before the naming schemes above is old enough to answer without reading further.
        if (name.Contains("Core2", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Core 2", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Pentium", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Celeron", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Atom", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Phenom", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Athlon", StringComparison.OrdinalIgnoreCase))
            return false;

        return null;
    }

    private static Requirement Memory(IMachine machine)
    {
        long bytes = machine.MemoryBytes();
        string found = $"{bytes / 1024.0 / 1024 / 1024:0.#} GB";
        return bytes >= MinMemory
            ? new("ram", "4 GB of memory", CheckResult.Pass, found, "4 GB")
            : new("ram", "4 GB of memory", CheckResult.Fail, found, "4 GB",
                "Memory is the cheapest of these to fix, if this PC's memory can be replaced at all.");
    }

    private static Requirement Disk(IMachine machine)
    {
        long bytes = machine.SystemDiskBytes();
        string found = $"{bytes / 1024.0 / 1024 / 1024:0.#} GB";
        return bytes >= MinDisk
            ? new("disk", "64 GB of storage", CheckResult.Pass, found, "64 GB")
            : new("disk", "64 GB of storage", CheckResult.Fail, found, "64 GB",
                "A larger drive, or a bigger card if this is a small tablet.");
    }
}
