#!/bin/bash
# Put the ARM64 build in the Windows VM and drive it there: the engine's checks, the console twin, the Windows 10
# paths through the test hook, and a real write-then-undo against the live registry.
set -euo pipefail
cd "$(dirname "$0")/.."
VM=~/Downloads/win11arm/vmssh.sh
V=$(sed -n 's|.*<Version>\(.*\)</Version>.*|\1|p' src/Stay/Stay.csproj)

$VM ssh 'Stop-Process -Name "Stay for Windows 10",stay -Force -ErrorAction SilentlyContinue; New-Item -ItemType Directory -Force C:\Stay | Out-Null'
$VM scp "dist/win-arm64-cli/stay.exe" 'keith@VM:C:/Stay/stay.exe'
# scp chokes on spaces in the source name, so the window exe travels under a plain one and is renamed inside.
cp "dist/win-arm64/Stay for Windows 10.exe" /tmp/StayWindow.exe
$VM scp /tmp/StayWindow.exe 'keith@VM:C:/Stay/StayWindow.exe'
$VM ssh 'Move-Item -Force C:\Stay\StayWindow.exe "C:\Stay\Stay for Windows 10.exe"'

$VM ssh '
$env:STAY_PRETEND_WINDOWS10 = "1"; $env:STAY_HOME = "C:\StayTest"
Remove-Item -Recurse -Force C:\StayTest -ErrorAction SilentlyContinue
C:\Stay\stay.exe selftest | Select-Object -Last 1
C:\Stay\stay.exe status | Select-Object -First 3
C:\Stay\stay.exe harden --id autorun,wsh | Select-Object -First 1
reg query "HKLM\SOFTWARE\Microsoft\Windows Script Host\Settings" /v Enabled *>$null
if ($LASTEXITCODE -eq 0) { "ok    the value was really written" } else { "FAIL  the value was not written" }
$id = (C:\Stay\stay.exe receipts --json | ConvertFrom-Json)[0].Id
C:\Stay\stay.exe undo $id
reg query "HKLM\SOFTWARE\Microsoft\Windows Script Host\Settings" /v Enabled *>$null
if ($LASTEXITCODE -ne 0) { "ok    and taken away again by the receipt" } else { "FAIL  undo left it behind" }
'
