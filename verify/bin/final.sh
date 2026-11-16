#!/bin/bash
# verify/bin/final.sh: the whole wave-3 verification, one harness at a time, in stages (wave-3 p8w3/final.sh).
#   final.sh <stage> [<stage> ...]   stages, in the wave-3 order:
#     engine builds runs1 enc rules06 cross06 released runs2 same hall
#   and the bosspilot modes wave 3 did not rerun (compared with their records in verify/baseline/bp-port or bp-w1):
#     released-all   fixture, catalog and the seven shrine modes (the released harnesses besides run and act)
#     w1             the family 01-04 modes: old01 secretfix plague finale relay collectA collectP 1-4 enc03 a-c
#                    (+ enc03-check) enc01-f03 icarax2 f04-boss f04-player f04-icarax 0/110/210
#     w2             the family 01/07/08 modes: enc01 secret4 rules01 enc07a enc07b shrine07 seeds07 19000 enc08
#                    enc08-tag enc08-cards enc08-trapped enc08-seeds
#     rules05        the 18 family 05 rules        cross05   the 18 rules on the 07/08 bosses (cross05.sh)
#     w4             the family 11/12/13 modes (wave 4): enc11-pete enc12 enc12-seeds enc12-hidden enc13a enc13b enc13-seeds,
#                    and enc11a enc11b enc11-cards enc11-shrine (one run per call, then `bp.sh same`)
#     w5             the family 14-19 modes (wave 5): enc14 enc14-seeds enc14-flight enc14-cards enc15 enc15-seeds enc15-rat
#                    enc15-toys enc16 enc16-seeds enc16-carousel enc16-cards enc19a enc19b enc19-seeds-a enc19-seeds-b, and
#                    enc17 enc17-seeds enc17-void enc17-cards enc18a enc18b enc18-finale (one run per call, then `bp.sh same`),
#                    then enc17-check
# Each stage writes verify/.out/final-<stage>.log (and prints it). The wave-3 set takes about 3 hours on a quiet machine
# (cross06 and rules06 are most of it); the five extra stages about 2 more. On a machine shared with other harness runs,
# run stages or slices through the harness lock instead (README.md), each call under about 6 minutes.
. "$(dirname "$0")/../lib/env.sh"
B="$VERIFY/bin"
RULES05="DALA_829 DALA_831 DALA_832 DALA_833 DALA_851 DALA_852 DALA_853 DALA_854 DALA_855 DALA_856 DALA_858 DALA_859 DALA_860 DALA_861 DALA_862 DALA_863 DALA_865 DALA_867"
RULES="ULDA_702 ULDA_705 ULDA_706 ULDA_707 ULDA_708 ULDA_709 ULDA_710 ULDA_711 ULDA_712 ULDA_713 ULDA_714 ULDA_715 ULDA_719 ULDA_720 ULDA_721 ULDA_724 ULDA_729 ULDA_801 ULDA_802 ULDA_803 ULDA_804"
[ $# -gt 0 ] || { sed -n '2,/^[^#]/{/^#/p}' "$0"; exit 2; }
for stage in "$@"; do
  L="$OUT/final-$stage.log"; : > "$L"
  case $stage in
  engine) bash "$B/engine.sh" >> "$L" 2>&1 ;;
  builds) { bash "$B/builds.sh" tool; bash "$B/builds.sh" all; bash "$B/builds.sh" randplay; } >> "$L" 2>&1 ;;
  runs1) bash "$B/runs.sh" 1 all 2>&1 | paste - - >> "$L" ;;
  runs2) bash "$B/runs.sh" 2 all 2>&1 | paste - - >> "$L" ;;
  same) bash "$B/same.sh" >> "$L" 2>&1 ;;
  enc)
    for m in enc09 enc09-seeds enc09-auto enc09-finals enc09-rooms enc09-cards enc10 enc10-nab enc10-seeds enc10-hagatha; do
      bash "$B/bp.sh" $m 2>&1 | cut -c1-700 >> "$L"
    done
    for m in enc10a enc10b enc10-cards enc10-shrine; do
      bash "$B/bp.sh" $m 1 2>&1 | cut -c1-700 >> "$L"; bash "$B/bp.sh" $m 2 2>&1 | cut -c1-700 >> "$L"; bash "$B/bp.sh" same $m >> "$L" 2>&1
    done ;;
  rules06) for r in $RULES; do s=$(date +%s); bash "$B/bp.sh" rules06 $r 2>&1 | cut -c1-330 >> "$L"; echo "($r $(( $(date +%s) - s ))s)" >> "$L"; done ;;
  cross06) : > "$OUT/bp/cross06.log"; bash "$B/cross06.sh" $RULES > /dev/null; cat "$OUT/bp/cross06.log" >> "$L" ;;
  released) { bash "$B/bp.sh" run; bash "$B/bp.sh" act; } >> "$L" 2>&1 ;;
  released-all) for m in fixture catalog shrine shrineS2 shrineS3 shrineU shrineT1 shrineT2 shrineD; do
      bash "$B/bp.sh" $m 2>&1 | cut -c1-700 >> "$L"; done ;;
  w1)
    for m in old01 secretfix plague finale relay collectA "collectP 1" "collectP 2" "collectP 3" "collectP 4" "enc03 a" "enc03 b" \
      "enc03 c" enc03-check enc01-f03 icarax2 f04-boss f04-player "f04-icarax 0" "f04-icarax 110" "f04-icarax 210"; do
      bash "$B/bp.sh" $m 2>&1 | cut -c1-700 >> "$L"
    done ;;
  w2)
    for m in enc01 secret4 rules01 enc07a enc07b shrine07 "seeds07 19000" enc08 enc08-tag enc08-cards enc08-trapped enc08-seeds; do
      bash "$B/bp.sh" $m 2>&1 | cut -c1-700 >> "$L"
    done ;;
  w4)
    for m in enc11-pete enc12 enc12-seeds enc12-hidden enc13a enc13b enc13-seeds; do bash "$B/bp.sh" $m 2>&1 | cut -c1-700 >> "$L"; done
    for m in enc11a enc11b enc11-cards enc11-shrine; do
      bash "$B/bp.sh" $m 1 2>&1 | cut -c1-700 >> "$L"; bash "$B/bp.sh" $m 2 2>&1 | cut -c1-700 >> "$L"; bash "$B/bp.sh" same $m >> "$L" 2>&1
    done ;;
  w5)
    for m in enc14 enc14-seeds enc14-flight enc14-cards enc15 enc15-seeds enc15-rat enc15-toys enc16 enc16-seeds enc16-carousel enc16-cards \
      enc19a enc19b enc19-seeds-a enc19-seeds-b; do bash "$B/bp.sh" $m 2>&1 | cut -c1-700 >> "$L"; done
    for m in enc17 enc17-seeds enc17-void enc17-cards enc18a enc18b enc18-finale; do
      bash "$B/bp.sh" $m 1 2>&1 | cut -c1-700 >> "$L"; bash "$B/bp.sh" $m 2 2>&1 | cut -c1-700 >> "$L"; bash "$B/bp.sh" same $m >> "$L" 2>&1
    done
    bash "$B/bp.sh" enc17-check >> "$L" 2>&1 ;;
  rules05) for r in $RULES05; do bash "$B/bp.sh" rules05 $r 2>&1 | cut -c1-330 >> "$L"; done ;;
  cross05) : > "$OUT/bp/cross05.log"; bash "$B/cross05.sh" $RULES05 > /dev/null; cat "$OUT/bp/cross05.log" >> "$L" ;;
  hall) { bash "$B/hall.sh" harness; bash "$B/hall.sh" 1 999; bash "$B/hall.sh" same; } >> "$L" 2>&1 ;;
  *) echo "unknown stage $stage" >> "$L" ;;
  esac
  cat "$L"
done
