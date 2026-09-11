# Changelog

## 1.0.0 — unreleased

First version.

- Where this PC stands: which Windows, whether it is being patched at all, and until when. ESU enrolment is read
  from the licences Windows holds rather than from a registry value.
- Every piece of this PC that still gets security updates, with the date each one stops and the source of that
  date: Windows with and without ESU, both LTSC editions, Edge and WebView2, Defender's definitions, Microsoft 365
  Apps.
- Whether this PC could take Windows 11, and which check fails — with what to do about the ones that are settings
  rather than a new PC. No bypass, deliberately.
- Fifteen things worth shutting on a PC that will not be patched again, each with what it is, what shutting it
  does and what you lose, and every change recorded in a receipt that puts it back exactly.
- Four more worth doing that the app will not do for you, with what to type, because a change it could not undo
  is not one it should make.
- Enrolment read from two independent signals rather than one, and an honest "two things about this PC
  disagree, so it will not guess" when they conflict. Every answer carries the evidence it was reached on.
- What turned itself back on: every value this app ever set, checked against what is there now, so a Windows
  feature update quietly restoring its own defaults is something you find out about.
- The browsers on this PC and what each maker has actually committed to for Windows 10 — a date for Edge, no
  published date for Chrome, and no end date for Firefox. Where nobody has promised, the app says so.
- Whether this PC is really being looked after, as opposed to entitled to be: the day an update last actually
  installed, a restart left waiting, the update service switched off, no antivirus, stale definitions, the
  firewall off. Each one says why it matters and what to do. An ESU enrolment that has stopped delivering is
  worse than none, because the person believes they are covered.
- The bundled apps, listed and removable — the one thing here a receipt cannot undo, which the app says before
  it does it, keeping a Store link for each.
- Make it faster: how long this PC really took to start and what each startup program cost, read from Windows'
  own performance log; five switches that genuinely help an old machine; and what is holding disk space, without
  deleting anything, because deleting is the one thing a receipt cannot undo.
- Turn off the junk: the 34 advertising, AI, telemetry and noise switches from Quiet for Windows, brought here
  whole so an old PC needs one app rather than two.
- A console twin with `--json` on everything, for scripts and for a fleet.
