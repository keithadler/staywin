#!/usr/bin/env bash
# The README makes claims about dates and counts. This checks them against the code, because a README that has
# drifted from the app is worse than no README: it is the app lying with a straight face.
set -uo pipefail
cd "$(dirname "$0")/.."
fail=0
say() { if [ "$1" = 0 ]; then echo "ok    $2"; else echo "FAIL  $2"; fail=1; fi }

L=src/Stay.Core/Lifecycle.cs
G=src/Stay.Core/Guards.cs

# Every date the README prints has to be the date the code holds.
check_date() { grep -q "$2" README.md && grep -q "$3" "$L"; say $? "the README's $1 matches Lifecycle.cs"; }
check_date "end of Windows 10"        "14 October 2025"   "2025, 10, 14"
check_date "consumer ESU end"         "12 October 2027"   "2027, 10, 12"
check_date "LTSC end"                 "12 January 2027"   "2027, 1, 12"
check_date "IoT LTSC end"             "13 January 2032"   "2032, 1, 13"
check_date "Microsoft 365 Apps end"   "10 October 2028"   "2028, 10, 10"

# The number of things it can shut, said in words in the README and counted in the catalogue.
guards=$(grep -cE '^        new\("' "$G")
[ "$guards" = 15 ]; say $? "the catalogue holds 15 guards (found $guards)"
grep -q "Fifteen things worth closing" README.md; say $? "and the README says fifteen"

byhand=$(sed -n '/ByHand = new/,/};/p' "$G" | grep -c '^        ("')
[ "$byhand" = 4 ]; say $? "there are 4 things it leaves to you (found $byhand)"
grep -q "Four more worth doing" CHANGELOG.md; say $? "and the changelog says four"

# Claims that would be embarrassing to get wrong.
grep -q "no network request" README.md && grep -q "no network request" PRIVACY.md
say $? "both documents make the same promise about the network"
! grep -rnE 'HttpClient|WebClient\(|WebRequest|Socket\(' src --include=*.cs >/dev/null
say $? "and nothing in the source can make one"

grep -q "13 October 2026" README.md; say $? "the README names the old ESU date people still repeat"
grep -q "was extended" README.md; say $? "and says it moved"

# The test hook must not be advertised as a feature.
! grep -q "STAY_PRETEND" src/Stay/Cli.cs; say $? "the pretend hook is not in the app's help"

[ $fail -eq 0 ] && echo "docs: all passed, 0 failed" || echo "docs: FAILURES"
exit $fail
