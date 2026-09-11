**[Download Stay-for-Windows-10-1.0.0-x64.exe](https://github.com/keithadler/staywin/releases/download/v1.0.0/Stay-for-Windows-10-1.0.0-x64.exe)** — Windows 10, 64-bit Intel or AMD.
**[Stay-for-Windows-10-1.0.0-arm64.exe](https://github.com/keithadler/staywin/releases/download/v1.0.0/Stay-for-Windows-10-1.0.0-arm64.exe)** — Windows 10 on ARM.

One exe. No installer, no account, no cloud. Right-click it and choose **Run as administrator** — reading works without, but changing anything under `HKEY_LOCAL_MACHINE` does not. Windows will warn you that it is from an unknown publisher, because it is not signed; the SHA-256 of every file is published below and the build that made them is public.

---

Windows 10 stopped being supported on 14 October 2025, and your PC will not tell you where that leaves it. Stay does.

**Where you stand.** Whether Windows here is being patched at all, and until when. Extended Security Updates run to 12 October 2027 and you can still join, three ways, one of them free.

**Whether it is actually working**, which is a different question and the one nothing else asks. Being entitled to updates and receiving them come apart more often than anybody expects. Stay reads the day an update really installed, a restart left waiting, an update service switched off years ago by a tuning guide, whether anything is watching for malware, how old its definitions are, whether the firewall is on. *Enrolled in Extended Security Updates, but the last update landed 7 months ago* is a sentence somebody needs to see.

**It shows its working, and it will say it does not know.** Enrolment is read from two independent things — the licences Windows holds, and whether anything has installed since support ended. When they disagree the app says so and shows you both rather than guessing. Telling somebody who is enrolled that they are not is the worst mistake it could make.

**Still being patched.** Different pieces die on different days: Edge and WebView2 to October 2028, Defender's definitions to October 2028, Microsoft 365 Apps to October 2028, Windows itself only with ESU. Your browser too, with what each maker has actually committed to — and where nobody has promised a date, Stay says that instead of inventing one.

**Windows 11.** Whether this PC can take it and exactly which check fails. A TPM switched off in firmware is ten minutes; a processor too old is a new PC. It will not get around the checks and will not tell you how.

**Make it faster.** Windows times its own start and times the programs holding it up, in a log almost nobody knows exists. *This PC last took 94 seconds to start. OneDrive cost 4.2 of them.* Real measurements. Then the few switches that genuinely help, and what is eating the disk. If the PC has a spinning hard disk, it says so first and says an SSD would do more than everything else put together.

**Turn off the junk.** The advertising, the AI hooks, the telemetry, the noise, and the bundled apps. Eight switches are left out because they are Windows 11 features Windows 10 never had, and the app says how many and why rather than writing values that do nothing.

**What turned itself back on.** Windows restores its own defaults when it updates and does not mention it. The receipts remember what you switched off, so Stay can tell you what has come back.

**Receipts.** Every change, with the value it had before, and one button that puts it back. Removing a bundled app is the only thing here a receipt cannot undo, and the app says exactly that before it does it.

Free and MIT licensed. No account, no cloud, no telemetry. The only network request is a daily check with GitHub for a newer version, which you can turn off.

**Honest limits.** It cannot patch Windows, and nothing can except Microsoft. It cannot enrol you in ESU — that is a Microsoft account flow in Settings — so it takes you to the right page. It does not make Windows 10 fast; it stops work happening, which is not the same thing. And it has been tested on Windows 11 in ARM64, against fixtures and against a real registry, but **not yet on a real Windows 10 PC**. If you run it on one, the issues page is the place to say what happened.
