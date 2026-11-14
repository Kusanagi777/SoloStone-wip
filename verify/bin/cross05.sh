#!/bin/bash
# verify/bin/cross05.sh: each family 05 rule (Dalaran Heist anomalies) on fourteen bosses of families 07 and 08, 1 game
# each, twice (c-bosspilot-rules05 through bp.sh cross, tag w2): the wave-2 integration's cross-family run. The boss lists
# are those of the wave-2 records (verify/baseline/bp-w2/bp-cross-w2-<rule>-a.txt); two rules meet thirteen:
# DALA_852 skips loot_17_jinzo and DALA_853 skips loot_16_azun.
#   cross05.sh [<rule> ...]     default: all 18 family 05 rules; output appended to verify/.out/bp/cross05.log
# Results are compared with verify/baseline/bp-port/bp-cross-w2-<rule>-a.txt (README.md section 4).
. "$(dirname "$0")/../lib/env.sh"
RULES="DALA_829 DALA_831 DALA_832 DALA_833 DALA_851 DALA_852 DALA_853 DALA_854 DALA_855 DALA_856 DALA_858 DALA_859 DALA_860 DALA_861 DALA_862 DALA_863 DALA_865 DALA_867"
ALL=loot_06_blackseed,loot_21_gnosh,loot_04_sturmi,loot_28_hamm,loot_13_thaddock,loot_15_george_karl,loot_39_kraxx,loot_51_russell,loot_46_flurgl,loot_47_lava_chamber,loot_16_azun,loot_17_jinzo,loot_24_mothergloop,loot_41_whompwhisker
NO17=loot_06_blackseed,loot_21_gnosh,loot_04_sturmi,loot_28_hamm,loot_13_thaddock,loot_15_george_karl,loot_39_kraxx,loot_51_russell,loot_46_flurgl,loot_47_lava_chamber,loot_16_azun,loot_24_mothergloop,loot_41_whompwhisker
NO16=loot_06_blackseed,loot_21_gnosh,loot_04_sturmi,loot_28_hamm,loot_13_thaddock,loot_15_george_karl,loot_39_kraxx,loot_51_russell,loot_46_flurgl,loot_47_lava_chamber,loot_17_jinzo,loot_24_mothergloop,loot_41_whompwhisker
[ $# -gt 0 ] && RULES="$*"
for r in $RULES; do
  b=$ALL; case $r in DALA_852) b=$NO17 ;; DALA_853) b=$NO16 ;; esac
  s=$(date +%s); GAMES=1 bash "$VERIFY/bin/bp.sh" cross "$r" w2 "$b" 2>&1 | cut -c1-360 | tee -a "$OUT/bp/cross05.log"
  echo "($r $(( $(date +%s) - s ))s)" | tee -a "$OUT/bp/cross05.log"
done
