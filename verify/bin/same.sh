#!/bin/bash
# verify/bin/same.sh: compares run 1 with run 2 (identical?) and run 1 with the recorded baseline
# (verify/baseline/runs/<name>.txt, the wave-6 tree's second run, Linux) as differing lines. CR is ignored on both sides.
#   same.sh                every entry of the run list that has logs (an entry with no run-1 log prints "not run")
#   same.sh only <dir>...  the named entries (paths relative to verify/suites)
# The expected wave-3 picture: every suite "1 vs 2 identical; vs baseline: 0 differing lines", except c-secretsearch,
# whose detail lines differ between any two runs (its SUMMARY line is what must match: 689 passed, 0 failed).
. "$(dirname "$0")/../lib/env.sh"
. "$VERIFY/lib/suites.sh"
L=$RUN_LIST
[ "$1" = only ] && { shift; L="$*"; }
lf() { tr -d '\r' < "$1"; }
for rel in $L; do
  name=$(basename "$rel"); a="$OUT/runs/$name-1.txt"; b="$OUT/runs/$name-2.txt"; base="$BASE/runs/$name.txt"
  [ -f "$a" ] || { echo "$name: not run"; continue; }
  case $name in
  selfplay)
    g1="$OUT/runs/selfplay-games-1.txt"; g2="$OUT/runs/selfplay-games-2.txt"
    s="games 1 vs 2 "; if [ -f "$g2" ]; then cmp -s <(lf "$g1") <(lf "$g2") && s="${s}identical" || s="${s}DIFFER"; else s="${s}(no run 2)"; fi
    cmp -s <(lf "$g1") <(lf "$BASE/runs/selfplay-games.txt") && s="$s; games identical to the baseline" || s="$s; games DIFFER from the baseline"
    [ "$(last_lines "$a" 1)" = "$(last_lines "$base" 1)" ] && s="$s; summary equal" || s="$s; summary DIFFERS"
    echo "selfplay: $s; $(last_lines "$a" 1)" ;;
  randplay)
    # compared: the printed lines (cards, TB, fingerprint, regkeys, games/digest); the baseline holds those five lines
    n=$(diff <(lf "$base") <(lf "$a" | grep -v -E '^(Unhandled exception|   at )') | grep -c '^[<>]')
    s=""; [ -f "$b" ] && { cmp -s <(lf "$a") <(lf "$b") && s="1 vs 2 identical; " || s="1 vs 2 DIFFER; "; }
    echo "randplay: ${s}vs baseline: $n differing lines; $(grep digest "$a" | tr -d '\r')" ;;
  *)
    s="1 vs 2 "; if [ -f "$b" ]; then cmp -s <(lf "$a") <(lf "$b") && s="${s}identical" || s="${s}DIFFER"; else s="${s}(no run 2)"; fi
    if [ -f "$base" ]; then n=$(diff <(lf "$base") <(lf "$a") | grep -c '^[<>]'); else n="no baseline"; fi
    echo "$name: $s; vs baseline: $n differing lines; $(last_lines "$a" 1 | cut -c1-90)" ;;
  esac
done
