#!/bin/bash
# The one test that matters: Stay on a real Windows 10, with no pretending.
#
# Every other check in this repo runs against fixtures, or against Windows 11 wearing a fake version number
# through STAY_PRETEND_WINDOWS10. This one runs the x64 build on genuine Windows 10 22H2, where the version is
# real, the licences are real, the event log is real and the registry is real. Nothing here sets the pretend
# hook — if it is set, the whole point is lost, so this refuses to run with it.
set -uo pipefail
cd "$(dirname "$0")/.."
VM=~/Downloads/win10x64/vmssh.sh
[ -x "$VM" ] || { echo "no Windows 10 VM helper at $VM"; exit 2; }

fail=0
say() { [ "$1" -eq 0 ] && echo "ok    $2" || { echo "FAIL  $2"; fail=1; }; }

echo "==> building x64"
./scripts/publish.sh >/dev/null 2>&1 || { echo "build failed"; exit 2; }

echo "==> copying to the VM"
$VM ssh 'Stop-Process -Name "Stay for Windows 10",stay -Force -ErrorAction SilentlyContinue; New-Item -ItemType Directory -Force C:\Stay | Out-Null' || exit 2
$VM scp "dist/win-x64-cli/stay.exe" 'keith@VM:C:/Stay/stay.exe' || exit 2
cp "dist/win-x64/Stay for Windows 10.exe" /tmp/StayWindow.exe
$VM scp /tmp/StayWindow.exe 'keith@VM:C:/Stay/StayWindow.exe' || exit 2
$VM ssh 'Move-Item -Force C:\Stay\StayWindow.exe "C:\Stay\Stay for Windows 10.exe"'

echo "==> what this machine actually is"
$VM ssh '(Get-CimInstance Win32_OperatingSystem).Caption + "  " + (Get-CimInstance Win32_OperatingSystem).BuildNumber + "  " + $env:PROCESSOR_ARCHITECTURE'

out=$($VM ssh '
$env:STAY_HOME = "C:\StayTest"
Remove-Item -Recurse -Force C:\StayTest -ErrorAction SilentlyContinue
if ($env:STAY_PRETEND_WINDOWS10) { "FAIL  the pretend hook is set; this test is worthless with it on"; exit }

C:\Stay\stay.exe selftest | Select-Object -Last 1

$j = C:\Stay\stay.exe status --json | ConvertFrom-Json

# The whole reason this VM exists. On a real Windows 10 the app must know it is one, without being told.
if ($j.windows.isWindows10) { "ok    it knows this is Windows 10 on its own" } else { "FAIL  it does not recognise real Windows 10" }
if (-not $j.windows.isWindows11) { "ok    and does not think it is Windows 11" } else { "FAIL  it thinks Windows 10 is Windows 11" }

# ESU: never once exercised against a real licence store until now.
"info  esu state: " + $j.esu.state
"info  esu says:  " + $j.esu.detail
if ($j.esu.because) { "info  because:   " + $j.esu.because }
if ($j.esu.state -in @("NotEnrolled","Enrolled","Unknown")) { "ok    the licence read produced an answer, not a crash" } else { "FAIL  no usable ESU answer" }
if ($j.esu.because) { "ok    and it showed its working" } else { "FAIL  no reasoning given" }

# A fresh unactivated Windows 10 has no ESU licence and no updates since October 2025, so the two signals
# should agree and the answer should be the confident one rather than the hedge.
if ($j.esu.state -eq "NotEnrolled" -and $j.esu.because -match "Both point the same way") {
  "ok    both signals agree, so it answers confidently"
} elseif ($j.esu.state -eq "Unknown") {
  "info  it will not guess here, which is allowed: " + $j.esu.because
} else { "info  unexpected but not wrong: " + $j.esu.state }

"info  last update: " + $j.lastUpdate

# The guards have to apply on a machine where the version is not faked.
$g = C:\Stay\stay.exe guards --json | ConvertFrom-Json
if ($g.Count -gt 0) { "ok    $($g.Count) guards apply on real Windows 10" } else { "FAIL  no guards apply" }

# The eight Windows 11 switches must be absent, for real this time.
$k = C:\Stay\stay.exe junk --json | ConvertFrom-Json
$eleven = @("ai.recall","ai.clicktodo","ai.paint","ai.notepad","ai.service","noise.spotlight","noise.suggested_actions","ads.settings_home")
$leaked = $k.switches | Where-Object { $_.id -in $eleven }
if (-not $leaked) { "ok    no Windows 11 switch is offered on real Windows 10" } else { "FAIL  offered: " + ($leaked.id -join ", ") }
if ($k.switches.Count -gt 0) { "ok    $($k.switches.Count) junk switches apply here" } else { "FAIL  no junk switches apply" }

# Speed, read off a real Windows 10 event log and a real Run key.
$s = C:\Stay\stay.exe speed --json | ConvertFrom-Json
if ($s.boot) { "ok    it read a real boot time: $([math]::Round($s.boot.seconds)) seconds" } else { "info  Windows has not logged a boot time yet" }
if ($s.disk.kind) { "ok    it read the disk: " + $s.disk.kind }
"info  startup programs found: " + $s.startup.Count

# Windows 11 checks against hardware that genuinely is not Windows 11 capable.
$e = C:\Stay\stay.exe eleven --json | ConvertFrom-Json
"info  windows 11 checks failing: " + (($e | Where-Object { -not $_.passes }).Count)

# And the promise: a real write to a real Windows 10 registry, and a real undo.
C:\Stay\stay.exe harden --id autorun,wsh | Select-Object -First 1
reg query "HKLM\SOFTWARE\Microsoft\Windows Script Host\Settings" /v Enabled *>$null
if ($LASTEXITCODE -eq 0) { "ok    a real value was written to a real Windows 10" } else { "FAIL  nothing was written" }
$id = (C:\Stay\stay.exe receipts --json | ConvertFrom-Json)[0].Id
C:\Stay\stay.exe undo $id | Out-Null
reg query "HKLM\SOFTWARE\Microsoft\Windows Script Host\Settings" /v Enabled *>$null
if ($LASTEXITCODE -ne 0) { "ok    and the receipt took it away again" } else { "FAIL  undo left it behind" }

# Drift, against the value that was just put back.
$d = C:\Stay\stay.exe drift --json | ConvertFrom-Json
"info  drift entries after an undo: " + $d.Count
')
echo "$out"
echo "$out" | grep -q "^FAIL" && fail=1
echo
[ $fail -eq 0 ] && echo "real-win10: all passed, 0 failed" || echo "real-win10: FAILURES"
exit $fail
