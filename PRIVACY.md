# Privacy

Stay for Windows 10 makes no network request of any kind. Not one. There is no update check, no telemetry, no
analytics, no account, and nothing to sign in to.

**What it reads on this PC.** The Windows version and edition, the licences Windows holds (to answer whether
Extended Security Updates are switched on), the TPM and Secure Boot state, the processor name, how much memory and
disk there is, whether a printer and whether Office are installed, and the specific registry values named in
[`Guards.cs`](src/Stay.Core/Guards.cs).

**What it writes.** Only the registry values named in that same file, only when you press the button, and only
after writing a receipt holding what each value was before.

**Where receipts live.** `C:\ProgramData\Stay for Windows 10\receipts`, as plain JSON you can read. They stay on
this PC. Nothing sends them anywhere; `stay report --json` prints to your screen so you can decide where it goes.

**What it never touches.** Your files, your documents, your browser, your accounts, your network.
