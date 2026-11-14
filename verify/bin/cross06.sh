#!/bin/bash
# verify/bin/cross06.sh: each family 06 rule on twelve bosses of families 09 and 10 (1 game each, twice), the fair AI as the
# boss (c-bosspilot-rules06 through bp.sh cross06). A rule meets only the bosses its schedule row allows: the two
# board-start rules (ULDA_719 Marin's Return, ULDA_804 Plague of Death) skip the Treasure Vault (flag boardStart).
#   cross06.sh [<rule> ...]     default: all 21 family 06 rules; output appended to verify/.out/bp/cross06.log
. "$(dirname "$0")/../lib/env.sh"
RULES="ULDA_702 ULDA_705 ULDA_706 ULDA_707 ULDA_708 ULDA_709 ULDA_710 ULDA_711 ULDA_712 ULDA_713 ULDA_714 ULDA_715 ULDA_719 ULDA_720 ULDA_721 ULDA_724 ULDA_729 ULDA_801 ULDA_802 ULDA_803 ULDA_804"
ALL=loot_22_tad,loot_52_vault,loot_54_kay,loot_53_inara,loot_49_darkness,loot_34_azari,loot_99_togwaggle,gila_26_gnomenapper,gila_27_experiment_3c,gila_29_sazzmi,gila_33_scarecrow,gila_34_havnixx
NOBOARD=loot_22_tad,loot_54_kay,loot_53_inara,loot_49_darkness,loot_34_azari,loot_99_togwaggle,gila_26_gnomenapper,gila_27_experiment_3c,gila_29_sazzmi,gila_33_scarecrow,gila_34_havnixx
[ $# -gt 0 ] && RULES="$*"
for r in $RULES; do
  b=$ALL; case $r in ULDA_719|ULDA_804) b=$NOBOARD ;; esac
  s=$(date +%s); GAMES=1 bash "$VERIFY/bin/bp.sh" cross06 "$r" w3 "$b" 2>&1 | cut -c1-360 | tee -a "$OUT/bp/cross06.log"
  echo "($r $(( $(date +%s) - s ))s)" | tee -a "$OUT/bp/cross06.log"
done
