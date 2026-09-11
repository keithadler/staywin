namespace Stay.Core;

/// <summary>
/// The things worth shutting on a PC nobody is fixing any more.
///
/// The rule for being in this list: it closes a way in that has been used against Windows 10 in the wild, it can
/// be closed with registry values so the receipt can put it back exactly, and the cost of closing it can be said
/// in one sentence. Anything that needs a feature turned off, a driver replaced or a reboot into firmware is not
/// here — it is in <see cref="ByHand"/>, with the exact words to type, because a half-done change the app cannot
/// undo is worse than advice it was honest about.
/// </summary>
public static class Guards
{
    private const string Services = @"SYSTEM\CurrentControlSet\Services";
    private const string Policies = @"SOFTWARE\Policies\Microsoft";
    private const string Asr = Policies + @"\Windows Defender\Windows Defender Exploit Guard\ASR";

    private static RegEdit Machine(string key, string name, long value) => new(Hive.LocalMachine, key, name, RegValue.DWord(value));
    private static RegEdit User(string key, string name, long value) => new(Hive.CurrentUser, key, name, RegValue.DWord(value));

    /// <summary>A Windows service set to Disabled. 4 is Disabled; anything less is some flavour of on.</summary>
    private static RegEdit ServiceOff(string service) => Machine($@"{Services}\{service}", "Start", 4);

    public static readonly IReadOnlyList<Guard> All = new List<Guard>
    {
        // ---------- ways in ----------
        new("smb1", "Ways in", "File sharing's oldest protocol (SMBv1)",
            "The 1990s version of Windows file sharing. Still switched on for compatibility with equipment nobody has any more.",
            "Closes the hole WannaCry and NotPetya went through. Both spread over SMBv1 on machines that were behind on patches, which is what this PC now permanently is.",
            "Windows XP machines and printers or scanners from before about 2010 will not see this PC's shared folders.",
            Cost.Small,
            new[] { ServiceOff("mrxsmb10"), Machine($@"{Services}\LanmanServer\Parameters", "SMB1", 0) },
            DefaultOn: true, NeedsRestart: true),

        new("llmnr", "Ways in", "Name lookups shouted across the network (LLMNR)",
            "When a name will not resolve, Windows shouts it to the whole local network and trusts whoever answers first.",
            "Stops the standard trick for collecting password hashes on a shared network: answer the shout, receive the credentials.",
            "Nothing on a home network. On a small office network without a proper DNS server, machines may be harder to find by name.",
            Cost.Small,
            new[] { Machine($@"{Policies}\Windows NT\DNSClient", "EnableMulticast", 0) },
            DefaultOn: true),

        new("netbios", "Ways in", "The other shouted name lookup (NetBIOS over TCP/IP)",
            "Older than LLMNR and answers the same way, per network adapter.",
            "Closes the second half of the same trick. Turning off LLMNR alone leaves this one listening.",
            "Same as above, plus any program still finding PCs by their old-style network name.",
            Cost.Small,
            Array.Empty<RegEdit>(),  // filled in per adapter at scan time; see Engine.Expand
            DefaultOn: true, OnlyIf: "netbios"),

        new("webdav", "Ways in", "Files over the web (the WebClient service)",
            "Lets Windows open a web address as if it were a folder. Almost nobody uses it; phishing attachments do.",
            "Stops a class of attachment that reaches out to a server on the internet and runs what it finds.",
            "If you map a drive to SharePoint or a web host with a \\\\server\\ address, that stops working.",
            Cost.Small,
            new[] { ServiceOff("WebClient") },
            DefaultOn: true, NeedsRestart: true),

        new("rdp", "Ways in", "Remote Desktop",
            "Lets somebody sign in to this PC over the network and use it as if they were sitting at it.",
            "Remote Desktop left on is how a great many PCs are taken over. On an unpatched OS it is the single biggest door.",
            "You cannot connect to this PC from another one. Connecting out from this PC to others still works.",
            Cost.Real,
            new[] { Machine(@"SYSTEM\CurrentControlSet\Control\Terminal Server", "fDenyTSConnections", 1) },
            DefaultOn: true),

        new("assist", "Ways in", "Remote Assistance invitations",
            "The older \"let me help you\" feature, separate from Remote Desktop and separate from Quick Assist.",
            "Removes a second remote path that almost nobody uses and that is rarely noticed when it is abused.",
            "Nobody can accept a Remote Assistance invitation from this PC.",
            Cost.None,
            new[] { Machine(@"SYSTEM\CurrentControlSet\Control\Remote Assistance", "fAllowToGetHelp", 0) },
            DefaultOn: true),

        new("spooler", "Ways in", "The print spooler, on a PC with no printer",
            "The service that queues printing. It has had a long run of serious holes, PrintNightmare among them.",
            "Removes a service that runs as the system account, accepts work from the network, and that this PC does not appear to need.",
            "You cannot print or add a printer until it is turned back on. The app only suggests this when it finds no real printer set up.",
            Cost.Real,
            new[] { ServiceOff("Spooler") },
            DefaultOn: true, NeedsRestart: true, OnlyIf: "noprinter"),

        // ---------- what is allowed to run ----------
        new("wsh", "What can run", "Scripts that run by double-click (Windows Script Host)",
            "The part of Windows that runs .vbs and .js files when you open them.",
            "Stops the oldest still-working email attachment in the world: a script file that runs the moment it is opened.",
            "Any .vbs or .js file you rely on stops running, including some old installers and login scripts.",
            Cost.Small,
            new[] { Machine(@"SOFTWARE\Microsoft\Windows Script Host\Settings", "Enabled", 0) },
            DefaultOn: true),

        new("macros", "What can run", "Office macros in files from the internet",
            "A Word or Excel file that arrived by email or download, carrying code.",
            "Blocks the single most common way a document turns into a break-in. Files you made yourself are unaffected.",
            "A macro workbook a colleague emails you will not run until you save it and unblock it in its properties.",
            Cost.Small,
            new[]
            {
                User(@"SOFTWARE\Policies\Microsoft\office\16.0\word\security", "blockcontentexecutionfrominternet", 1),
                User(@"SOFTWARE\Policies\Microsoft\office\16.0\excel\security", "blockcontentexecutionfrominternet", 1),
                User(@"SOFTWARE\Policies\Microsoft\office\16.0\powerpoint\security", "blockcontentexecutionfrominternet", 1),
            },
            DefaultOn: true, OnlyIf: "office"),

        new("autorun", "What can run", "Anything that starts itself from a USB stick",
            "AutoRun and AutoPlay: Windows looking at what you plugged in and starting something from it.",
            "A USB stick left in a car park is still a way into an office. This makes plugging one in do nothing on its own.",
            "You open the drive yourself in File Explorer instead of a window appearing. That is the whole difference.",
            Cost.None,
            new[]
            {
                Machine(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoDriveTypeAutoRun", 255),
                Machine(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoAutorun", 1),
            },
            DefaultOn: true),

        new("asr", "What can run", "Defender's attack surface rules",
            "Eight rules in Microsoft Defender that block particular moves rather than particular files: Office starting a program, a script starting a download, a program reading passwords out of memory.",
            "These are the closest thing to a patch for an unpatched PC. They stop the step in the middle of an attack rather than trying to recognise the file at the start of it.",
            "A few unusual installers and macro-heavy workbooks will be blocked. Defender shows what it stopped, and a rule can be turned back off here.",
            Cost.Small,
            new[]
            {
                Machine(Asr, "ExploitGuard_ASR_Rules", 1),
                // Executable content from email and webmail
                new RegEdit(Hive.LocalMachine, Asr + @"\Rules", "BE9BA2D9-53EA-4CDC-84E5-9B1EEEE46550", RegValue.Str("1")),
                // Office applications creating child processes
                new RegEdit(Hive.LocalMachine, Asr + @"\Rules", "D4F940AB-401B-4EFC-AADC-AD5F3C50688A", RegValue.Str("1")),
                // Office applications creating executable content
                new RegEdit(Hive.LocalMachine, Asr + @"\Rules", "3B576869-A4EC-4529-8536-B80A7769E899", RegValue.Str("1")),
                // JavaScript or VBScript launching downloaded executable content
                new RegEdit(Hive.LocalMachine, Asr + @"\Rules", "D3E037E1-3EB8-44C8-A917-57927947596D", RegValue.Str("1")),
                // Obfuscated scripts
                new RegEdit(Hive.LocalMachine, Asr + @"\Rules", "5BEB7EFE-FD9A-4556-801D-275E5FFC04CC", RegValue.Str("1")),
                // Credential stealing from LSASS
                new RegEdit(Hive.LocalMachine, Asr + @"\Rules", "9E6C4E1F-7D60-472F-BA1A-A39EF669E4B2", RegValue.Str("1")),
                // Persistence through WMI event subscription
                new RegEdit(Hive.LocalMachine, Asr + @"\Rules", "E6DB77E5-3DF2-4CF1-B95A-636979351E5B", RegValue.Str("1")),
                // Untrusted and unsigned processes that run from USB
                new RegEdit(Hive.LocalMachine, Asr + @"\Rules", "B2B3F03D-6A65-4F7B-A9C7-1C7EF74A9BA4", RegValue.Str("1")),
            },
            DefaultOn: true, OnlyIf: "defender"),

        // ---------- what is watching ----------
        new("smartscreen", "What is watching", "SmartScreen set to block, not warn",
            "The check Windows runs on a program the first time you open it.",
            "On a PC that will not be patched again, the warning that can be clicked through is worth less than the one that cannot.",
            "A program Microsoft has not seen before needs an extra step to run. You can still run it.",
            Cost.Small,
            new[]
            {
                Machine($@"{Policies}\Windows\System", "EnableSmartScreen", 1),
                new RegEdit(Hive.LocalMachine, $@"{Policies}\Windows\System", "ShellSmartScreenLevel", RegValue.Str("Block")),
            },
            DefaultOn: true),

        new("cloud", "What is watching", "Defender's cloud checks",
            "Defender asking Microsoft about a file it has not seen before, and sending a copy when it is suspicious.",
            "Definitions keep coming to this PC until October 2028, and the cloud check is the part of Defender that knows about something released this morning.",
            "Suspicious files are uploaded to Microsoft. That is a real privacy cost and the reason this is not switched on quietly for you.",
            Cost.Real,
            new[]
            {
                Machine($@"{Policies}\Windows Defender\Spynet", "SpynetReporting", 2),
                Machine($@"{Policies}\Windows Defender\Spynet", "SubmitSamplesConsent", 1),
            },
            DefaultOn: false, OnlyIf: "defender"),

        new("lsa", "What is watching", "Password memory locked down (LSA protection)",
            "Makes the part of Windows holding your signed-in credentials refuse to be read by other programs.",
            "Stops the standard tool for lifting passwords out of a running PC from working at all.",
            "An old fingerprint reader, smartcard driver or password manager plug-in can stop working. It needs a restart, and this is the one here most likely to need undoing.",
            Cost.Real,
            new[] { Machine(@"SYSTEM\CurrentControlSet\Control\Lsa", "RunAsPPL", 1) },
            DefaultOn: false, NeedsRestart: true),

        // ---------- the updates themselves ----------
        new("paused", "Updates", "Updates not paused",
            "Windows Update can be paused for up to 35 days, and a paused PC stays paused quietly.",
            "If you are paying for or enrolled in Extended Security Updates, a pause is the one thing that stops them arriving.",
            "Nothing. Updates resume.",
            Cost.None,
            new[]
            {
                new RegEdit(Hive.LocalMachine, @"SOFTWARE\Microsoft\WindowsUpdate\UX\Settings", "PauseUpdatesExpiryTime", RegValue.Absent),
                new RegEdit(Hive.LocalMachine, @"SOFTWARE\Microsoft\WindowsUpdate\UX\Settings", "PauseFeatureUpdatesStartTime", RegValue.Absent),
                new RegEdit(Hive.LocalMachine, @"SOFTWARE\Microsoft\WindowsUpdate\UX\Settings", "PauseQualityUpdatesStartTime", RegValue.Absent),
            },
            DefaultOn: true),
    };

    public static Guard? Find(string id) => All.FirstOrDefault(g => g.Id == id);

    /// <summary>
    /// The things worth doing that this app will not do for you, with what to type. They need a feature turned
    /// off or a restart into firmware, and an app that cannot undo a change should not be making it.
    /// </summary>
    public static readonly IReadOnlyList<(string Title, string Why, string How)> ByHand = new[]
    {
        ("Remove the SMBv1 feature entirely",
         "Turning the service off closes it. Removing the feature means it cannot be turned back on by something else.",
         "Windows Features (optionalfeatures.exe) → clear \"SMB 1.0/CIFS File Sharing Support\"."),

        ("Remove the PowerShell 2.0 engine",
         "Windows still ships an old PowerShell that skips the logging and safety the current one has. It is what attackers ask for by name.",
         "Windows Features (optionalfeatures.exe) → clear \"Windows PowerShell 2.0\"."),

        ("Turn on BitLocker, or check it is on",
         "It does nothing against a break-in over the network, and everything against the laptop being stolen.",
         "Control Panel → BitLocker Drive Encryption. Keep the recovery key somewhere off this PC."),

        ("Sign in as a standard user, not an administrator",
         "The single biggest reduction in what a mistake can do, and the one nobody wants to hear.",
         "Settings → Accounts → Other users. Make a second administrator account first, then change yours to Standard."),
    };
}
