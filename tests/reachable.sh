#!/usr/bin/env bash
# The mistake that keeps happening across these apps is working code wired to nothing. A unit test cannot see it,
# because the code works. What can see it is asking, of everything the app can do, whether there is a way to it.
set -uo pipefail
cd "$(dirname "$0")/.."
fail=0
say() { if [ "$1" = 0 ]; then echo "ok    $2"; else echo "FAIL  $2"; fail=1; fi }

C=src/Stay/Cli.cs

# Every verb the command line answers to has to be in the usage text, and the other way round.
verbs=$(sed -n '/switch (verb)/,/^        }$/p' "$C" | grep -oE 'case "[a-z0-9]+"' | grep -oE '"[a-z0-9]+"' | tr -d '"' | sort -u)
for v in $verbs; do
  case "$v" in --*|-h|11|scan|open|help) continue ;; esac   # aliases and help itself, not names to advertise twice
  grep -q "stay $v" "$C" || { echo "FAIL  \"$v\" is a command nothing tells you about"; fail=1; }
done
say 0 "every command is in the usage text"

for named in $(grep -oE '^ *stay [a-z]+' "$C" | awk '{print $2}' | sort -u); do
  printf '%s\n' "$verbs" | grep -qx "$named" || { echo "FAIL  the usage text offers \"$named\" and nothing answers it"; fail=1; }
done
say 0 "every command in the usage text is answered"

# Every guard in the catalogue has to be reachable in the window, and the window's list is what the engine offers,
# so what matters is that nothing filters a guard out for good.
guards=$(grep -oE '^        new\("[a-z0-9]+"' src/Stay.Core/Guards.cs | grep -oE '"[a-z0-9]+"' | tr -d '"')
conditions=$(grep -oE 'OnlyIf: "[a-z]+"' src/Stay.Core/Guards.cs | grep -oE '"[a-z]+"' | tr -d '"' | sort -u)
for c in $conditions; do
  grep -q "\"$c\" =>" src/Stay/../Stay.Core/Engine.cs || { echo "FAIL  guard condition \"$c\" is never decided, so those guards never show"; fail=1; }
done
say 0 "every guard condition is one the engine knows how to answer"

# Each pane in the window has to be shown by something.
panes=$(grep -oE 'x:Name="[A-Za-z]+Pane"' src/Stay/MainWindow.xaml | grep -oE '"[A-Za-z]+"' | tr -d '"' | sort -u)
for p in $panes; do
  grep -q "$p" src/Stay/MainWindow.xaml.cs || { echo "FAIL  pane $p is drawn and never shown"; fail=1; }
done
say 0 "every pane in the window has a way to it"

count=$(printf '%s\n' "$guards" | wc -l | tr -d ' ')
[ $fail -eq 0 ] && echo "reachable: all $count guards and every command have a way to them, 0 failed" || echo "reachable: FAILURES"
exit $fail
