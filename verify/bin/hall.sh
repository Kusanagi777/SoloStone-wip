#!/bin/bash
# verify/bin/hall.sh: the audit hall (wave-3 p8w3/hall.sh + scratchpad audit-verify/one.sh). Each harness is built against
# verify/.out/engine-out and run with --all.
#   hall.sh list              the 38 audit harnesses, numbered
#   hall.sh harness           build and run the base harness (verify/suites/hall/audit-harness)
#   hall.sh <from> <to>       build and run audit harnesses <from>..<to>
#   hall.sh same              each harness's SUMMARY line against verify/baseline/hall.txt (the wave-3 hall)
# Logs: verify/.out/hall/<name>.txt (run), verify/.out/builds/hall-<name>.txt (build).
. "$(dirname "$0")/../lib/env.sh"
. "$VERIFY/lib/suites.sh"

hall_one() { # hall_one <suites-relative dir>
  local rel=$1 name d rc start
  name=$(basename "$rel"); start=$(date +%s)
  d=$(suite_dir "$rel") || { echo "$name NOT PORTED (no verify/suites/$rel/*.csproj)"; return 0; }
  (cd "$d" && dotnet build -c Release -nologo -v q -o out > "$OUT/builds/hall-$name.txt" 2>&1) || { echo "$name BUILD FAILED"; return 0; }
  cmp -s "$d/out/SabberStoneCore.dll" "$ENGINE_DLL" || echo "$name DLL MISMATCH"
  (cd "$d" && dotnet out/AiHarness.dll --all > "$OUT/hall/$name.txt" 2>&1); rc=$?
  echo "$name exit=$rc $(grep -E '^SUMMARY' "$OUT/hall/$name.txt" | tr -d '\r')"
  echo "  ($(( $(date +%s) - start ))s)"
}

case "$1" in
list) i=0; for n in $HALL_LIST; do i=$((i+1)); echo "$i $n"; done ;;
harness) need_engine; hall_one "$HALL_BASE" ;;
same)
  for n in audit-harness $HALL_LIST; do
    f="$OUT/hall/$n.txt"; [ -f "$f" ] || { echo "$n: not run"; continue; }
    now="$(grep -E '^SUMMARY' "$f" | tr -d '\r')"; was="$(grep -E "^$n exit=" "$BASE/hall.txt" | sed -E 's/^[^ ]+ exit=[0-9]+ //')"
    [ "$now" = "$was" ] && echo "$n: equal ($now)" || echo "$n: DIFFERS: now '$now', wave 3 '$was'"
  done ;;
[0-9]*)
  [ -n "$2" ] || { sed -n 2,8p "$0"; exit 2; }
  need_engine; i=0
  for n in $HALL_LIST; do i=$((i+1)); { [ $i -lt "$1" ] || [ $i -gt "$2" ]; } && continue; hall_one "hall/$n"; done ;;
*) sed -n 2,8p "$0"; exit 2 ;;
esac
