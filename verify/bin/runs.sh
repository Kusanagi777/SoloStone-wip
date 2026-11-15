#!/bin/bash
# verify/bin/runs.sh: the run stage (wave-3 p8w3/runs.sh): every suite harness once, as `dotnet out/<Name>.dll`, from its
# own folder. k is the run number (1 or 2; same.sh compares run 1 with run 2 and with the wave-3 baseline).
#   runs.sh list                 the 58 entries (the wave-3 order, then wave 4)
#   runs.sh <k> <from> <to>      entries <from>..<to>
#   runs.sh <k> all | pilot      all 58 | the pilot entries
#   runs.sh <k> only <dir>...    the named entries (paths relative to verify/suites)
# Logs: verify/.out/runs/<name>-<k>.txt (selfplay also writes selfplay-games-<k>.txt, randplay randplay-games-<k>.txt).
# Each entry prints "<i> <name> <k> exit=<rc> <result line>" and its time. On a shared machine wrap the call in the
# harness lock (README.md); the AI search budgets are wall-clock, so a busy machine can change what a bosspilot plays.
. "$(dirname "$0")/../lib/env.sh"
. "$VERIFY/lib/suites.sh"

run_one() { # run_one <number> <k> <suites-relative dir>
  local i=$1 k=$2 rel=$3 name d rc sum start log
  name=$(basename "$rel")
  d=$(suite_dir "$rel") || { echo "$i $name $k NOT PORTED (no verify/suites/$rel/*.csproj)"; return 0; }
  log="$OUT/runs/$name-$k.txt"; start=$(date +%s)
  cd "$d" || return 0
  case $name in
    c-secretscan)
      dotnet out/SecretScan.dll > "$log" 2> "$OUT/runs/$name-err-$k.txt"; rc=$?; sum="$(last_lines "$log" 2 | tr '\n' ' ')" ;;
    c-secretsearch)
      dotnet out/AiHarness.dll > "$log" 2>&1; rc=$?; sum="$(grep SUMMARY "$log" | tail -n 1 | tr -d '\r')" ;;
    selfplay)
      dotnet out/AiHarness.dll 200 "$UP/.out/runs/selfplay-games-$k.txt" > "$log" 2>&1; rc=$?; sum="$(last_lines "$log" 1)" ;;
    randplay)
      # The wave-3 runner passed no output-file argument, so EngineProof threw after printing its digest (exit 127 there);
      # it is given one here. The printed lines (cards, fingerprint, regkeys, games, digest) are what same.sh compares.
      dotnet out/EngineProof.dll 200 "$UP/.out/runs/randplay-games-$k.txt" > "$log" 2>&1; rc=$?; sum="$(grep digest "$log" | tr -d '\r')" ;;
    *)
      dotnet out/AiHarness.dll > "$log" 2>&1; rc=$?; sum="$(last_lines "$log" 1)" ;;
  esac
  echo "$i $name $k exit=$rc $sum"
  echo "   ($(( $(date +%s) - start ))s)"
}

[ "$1" = list ] && { i=0; for rel in $RUN_LIST; do i=$((i+1)); echo "$i $rel"; done; exit 0; }
k=$1
case "$k" in 1|2|[0-9]*) ;; *) sed -n 2,11p "$0"; exit 2 ;; esac
case "$2" in
pilot) for rel in $PILOT_LIST; do [ "$rel" = p8f01/c-bosspilot-encounter ] && continue; run_one "-" "$k" "$rel"; done; exit 0 ;;
only) shift 2; for rel in "$@"; do run_one "-" "$k" "$rel"; done; exit 0 ;;
all) set -- "$k" 1 999 ;;
esac
[ -n "$3" ] || { sed -n 2,11p "$0"; exit 2; }
i=0
for rel in $RUN_LIST; do
  i=$((i+1)); { [ $i -lt "$2" ] || [ $i -gt "$3" ]; } && continue
  run_one $i "$k" "$rel"
done
