#!/usr/bin/env bash
# Runs the project's EditMode tests in a mirror copy of the project, so a Unity editor open on the real project
# keeps its lock. Unity runs with a graphics device: the match smoke test renders real scenes, and without one URP
# crashes Unity as soon as the menu scene's cameras render.
#
# Usage:  tools/run-tests.sh [test-filter]   test-filter = NUnit class or test name, or a regex; default: every test
# Env:    DOA_MIRROR   mirror project (default: _doa_test_mirror/<project folder> next to the project; keep it at a
#                      short path - Windows paths over 260 characters break the import)
#         DOA_UNITY    Unity.exe (default: Unity Hub's 2022.3.62f3)
#         DOA_NO_SYNC  1 = test the mirror as it is, without copying the project into it first
# Output: Logs/test-results.xml and Logs/test-unity.log in the project; failures and a summary line on stdout.
# Exit:   0 only when at least one test ran and none failed.
set -uo pipefail

REAL="$(cd "$(dirname "$0")/.." && pwd)"
MIRROR="${DOA_MIRROR:-$(dirname "$REAL")/_doa_test_mirror/$(basename "$REAL")}"
UNITY="${DOA_UNITY:-C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe}"
OUT="$REAL/Logs"
filter="${1:-}"

mkdir -p "$OUT"
if [ "${DOA_NO_SYNC:-}" != "1" ]; then
  for d in Assets Packages ProjectSettings; do
    MSYS_NO_PATHCONV=1 robocopy "$(cygpath -w "$REAL/$d")" "$(cygpath -w "$MIRROR/$d")" /MIR /R:1 /W:1 /NFL /NDL /NP /NJH /NJS > /dev/null
    rc=$?
    if [ "$rc" -ge 8 ]; then echo "sync of $d failed (robocopy exit $rc)"; exit 1; fi
  done
fi

results="$OUT/test-results.xml"
log="$OUT/test-unity.log"
rm -f "$results"
args=(-batchmode -projectPath "$(cygpath -w "$MIRROR")" -runTests -testPlatform EditMode
      -testResults "$(cygpath -w "$results")" -logFile "$(cygpath -w "$log")")
[ -n "$filter" ] && args+=(-testFilter "$filter")
"$UNITY" "${args[@]}"
unity_rc=$?

if [ ! -f "$results" ]; then
  grep -E "error CS[0-9]+" "$log" | sort -u | head -20
  echo "NO RESULTS (Unity exit $unity_rc) - see $log"
  exit 1
fi

powershell.exe -NoProfile -Command "
  [xml]\$x = Get-Content -Raw '$(cygpath -w "$results")'
  foreach (\$tc in \$x.SelectNodes(\"//test-case[@result='Failed']\")) {
    \$m = \$tc.SelectSingleNode('failure/message')
    'FAIL ' + \$tc.fullname + ': ' + ((\$m.InnerText -replace '\s+',' ').Trim())
  }
  \$r = \$x.'test-run'
  'tests: ' + \$r.total + ' total, ' + \$r.passed + ' passed, ' + \$r.failed + ' failed, ' + \$r.skipped + ' skipped'
  if ([int]\$r.failed -gt 0 -or [int]\$r.total -eq 0) { exit 1 } else { exit 0 }
"
