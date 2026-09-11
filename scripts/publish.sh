#!/bin/bash
# Build both exes for both architectures into dist/, named the way a release names them.
set -euo pipefail
cd "$(dirname "$0")/.."
V=$(sed -n 's|.*<Version>\(.*\)</Version>.*|\1|p' src/Stay/Stay.csproj)
DOTNET=${DOTNET:-$HOME/.dotnet9/dotnet}
rm -rf dist; mkdir -p dist
for arch in x64 arm64; do
  $DOTNET publish src/Stay/Stay.csproj -c Release -r "win-$arch" --nologo -o "dist/win-$arch" >/dev/null
  $DOTNET publish src/Stay/Stay.csproj -c Release -r "win-$arch" --nologo -o "dist/win-$arch-cli" -p:CliBuild=true >/dev/null
  cp "dist/win-$arch/Stay for Windows 10.exe" "dist/Stay-for-Windows-10-$V-$arch.exe"
  cp "dist/win-$arch-cli/stay.exe" "dist/stay-$V-$arch.exe"
done
cd dist && shasum -a 256 ./*.exe | sed 's|\./||' > SHA256SUMS.txt
ls -la ./*.exe SHA256SUMS.txt
