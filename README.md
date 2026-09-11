# Stay for Windows 10

Where this PC stands now that Windows 10 is not supported, what on it is still being patched, and the doors worth
shutting on a machine nobody is fixing any more.

## Download

**[Download Stay-for-Windows-10-1.0.0-x64.exe](https://github.com/keithadler/staywin/releases/latest/download/Stay-for-Windows-10-1.0.0-x64.exe)**
(Windows 10, 64-bit Intel or AMD) · **[ARM64](https://github.com/keithadler/staywin/releases/latest/download/Stay-for-Windows-10-1.0.0-arm64.exe)**

One exe. No installer, no account, no cloud. Right-click it and choose **Run as administrator** — reading works
without, but changing anything under `HKEY_LOCAL_MACHINE` does not.

It is not signed, because a certificate costs money this does not make. SmartScreen will say so. The SHA-256 of
every release is published with it and the exes are built by GitHub from the commit you can read.

## Why this exists

Windows 10 stopped being supported on 14 October 2025. That is one sentence, and almost everything that follows
from it is wrong in the retelling.

Different pieces of the same PC stop getting security updates on different days:

| | Until |
|---|---|
| Windows itself, **not** enrolled in Extended Security Updates | it already stopped, 14 October 2025 |
| Windows itself, enrolled in consumer ESU | 12 October 2027 |
| Windows 10 Enterprise LTSC 2021 | 12 January 2027 |
| Windows 10 IoT Enterprise LTSC 2021 | 13 January 2032 |
| Microsoft Edge and WebView2 | October 2028 |
| Microsoft Defender's definitions | October 2028 |
| Microsoft 365 Apps (Word, Excel, Outlook) | 10 October 2028 |

Nothing on the PC puts those together for you, and nothing on the PC will tell you plainly whether ESU is actually
switched on. This app reads it off the machine and says so in one sentence.

> **Consumer ESU was extended.** It was originally a single year, ending 13 October 2026. Microsoft moved both
> enrolment and coverage to 12 October 2027. A lot of advice still says 2026.

## What it does

**Where you stand.** What Windows this is, whether it is being patched at all, and until when. Whether ESU is
enrolled — read from the licences Windows itself holds, not from a registry value somebody could have set by hand.

**Still being patched.** Every piece of this PC that still gets security updates, the day each one stops, and
where that date came from, so you can check it rather than trust it.

**Windows 11.** Whether this PC could take it, and *which check fails*. Three of the four usual failures are
settings somebody can change in ten minutes — a TPM switched off in firmware, Secure Boot off, a disk still
partitioned the old way — and one means a different PC. Knowing which is the whole question.

**What to shut.** Fifteen things worth closing on a PC that will not be patched again: SMBv1, the two shouted
name lookups that hand over password hashes, Remote Desktop, WebDAV, the print spooler on a PC with no printer,
scripts that run on double-click, Office macros from the internet, anything that starts itself from a USB stick,
Defender's attack-surface rules, SmartScreen set to block rather than warn. Each one says what it is, what
shutting it does, and what you lose — and every change is written to a receipt first, holding what each value was
before, so one button puts it all back.

**Receipts.** Every change this app has ever made on this PC, with the old values, and a button that restores them.

## What it cannot do

- **It cannot patch Windows.** Nothing can except Microsoft. Shutting doors is not the same as fixing holes, and
  the app says so on its own front screen.
- **It cannot enrol you in ESU.** That is a Microsoft account flow in Settings. The app tells you what you will be
  asked for and opens the page.
- **It will not get round Windows 11's hardware checks**, and will not tell you how. Windows 11 on an unsupported
  PC may stop receiving updates, which is the exact problem this app exists to deal with.
- **It does not remove or install anything**, does not touch your files, and makes no network request of any kind.

## The console twin

`stay.exe` is the same program without a window, for scripts and scheduled jobs.

```
stay status                  is Windows on this PC being patched, and until when
stay updates                 everything here that still gets updates, and the date each stops
stay eleven                  whether this PC could take Windows 11, and which check fails
stay guards                  what is open that this app can shut
stay harden --all            shut everything it suggests, with a receipt
stay harden --all --dry-run  show the exact changes and make none
stay receipts                every change this app has made on this PC
stay undo <id>               put a receipt's changes back exactly
stay report --json           all of it, for a fleet or for your own records
stay selftest
```

Exit codes: 0 fine, 1 something to look at, 2 problem, 64 usage. `--json` works on everything that reports.

## Privacy

No account, no cloud, no telemetry, no update check, no network request at all. Nothing about you or this PC
leaves it. See [PRIVACY.md](PRIVACY.md).

## Building it

```
dotnet build src/Stay.Selftest -c Release && dotnet src/Stay.Selftest/bin/Release/net9.0/Stay.Selftest.dll
dotnet publish src/Stay/Stay.csproj -c Release -r win-x64 -o dist/x64
dotnet publish src/Stay/Stay.csproj -c Release -r win-x64 -o dist/x64-cli -p:CliBuild=true
```

The engine knows nothing about Windows: every fact about the PC arrives through an interface, and the checks run
against PCs that do not exist — a machine with a 1.2 TPM, one with no printer, one already hardened, one where
every write is refused. That is why `Stay.Selftest` runs on any OS.

## Honest limits

- The dates are constants in [`Lifecycle.cs`](src/Stay.Core/Lifecycle.cs), each with its source, last checked on
  10 September 2026. The app shows that date on screen so nobody has to wonder whether it is stale.
- The Windows 11 processor check reads the generation out of the processor's name. When it cannot read one it
  says **cannot tell** rather than guessing.
- Shutting the print spooler, Remote Desktop and LSA protection can get in the way. Each is marked with what it
  costs, and the two most likely to bite are not ticked for you.

Free, MIT licensed, built by Keith Adler. More from the same maker: [keithadler.github.io](https://keithadler.github.io).
