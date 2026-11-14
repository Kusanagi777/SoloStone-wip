#!/bin/bash
# verify/bin/bp.sh: the bosspilot stage (wave-3 p8w3/bp.sh). One mode per call; an encounter mode runs twice and is
# compared without the timing column. Results: verify/.out/bp/bp-<name>-a.txt / -b.txt (logs in verify/.out/bp/logs).
# Each result is also compared with its record, expected 0 differing lines, the first that exists of:
#   verify/baseline/bp/bp-<name>-a.txt       "vs wave-3 baseline"   (the wave-3 main tree, p8w3)
#   verify/baseline/bp-port/bp-<name>-a.txt  "vs port record"       (modes wave 3 did not rerun, recorded by the port on
#                                                                    the wave-3 tree, 2026-11-13; README.md section 4)
#   verify/baseline/bp-w1/bp-<name>-a.txt    "vs wave-1 record"     (the seven shrine modes, fixture, catalog)
#   verify/baseline/bp-w2/bp-<name>-a.txt    "vs wave-2 reference"  (history only; every wave-2 mode has a port record)
# bp-w1 also keeps the wave-1 records of the family 01-04 modes and bp-w2 the wave-2 records, as history: README.md
# section 6 lists how far each one moved and why.
#   released modes:      bp.sh run|fixture|catalog|act|shrine|shrineS2|shrineS3|shrineU|shrineT1|shrineT2|shrineD
#   family 01 modes:     bp.sh enc01|old01|secret4|secretfix|rules01 [<rule>]|plague|finale|relay
#   family 02 modes:     bp.sh collectA | collectP <group 1..4>  (the fixture bosses play the 33 cards | the run player does)
#   family 03 modes:     bp.sh enc03 <slice a|b|c> | enc03-check | enc01-f03 | icarax2
#   family 04 modes:     bp.sh f04-boss | f04-player | f04-icarax <carried 0|110|210>
#   family 07 modes:     bp.sh enc07a|enc07b|shrine07|seeds07 <base>
#   family 08 modes:     bp.sh enc08|enc08-tag|enc08-cards|enc08-trapped|enc08-seeds
#   family 05 modes:     bp.sh rules05 <rule>          (c-bosspilot-rules05: the rule on one custom boss and one per adventure)
#   family 06 modes:     bp.sh rules06 <rule>          (c-bosspilot-rules06: the same six bosses; No-Face and Aleatory their own sets)
#   family 09 modes:     bp.sh enc09|enc09-seeds       (the shared harness, FAMILY=09)
#                        bp.sh enc09-auto|enc09-finals|enc09-rooms|enc09-cards   (c-bosspilot-enc09)
#   family 10 modes:     bp.sh enc10|enc10-nab|enc10-seeds|enc10-hagatha        (twice per call)
#                        bp.sh enc10a|enc10b|enc10-cards|enc10-shrine <k>         (one run per call, k = 1 or 2; then `bp.sh same <mode>`)
#   family 23 modes:     bp.sh enc23|enc23-seeds                                 (twice per call; FAMILY=23: the four nemeses and Hagatha)
#                        bp.sh enc23-cards <k>                                    (one run per call, k = 1 or 2; then `bp.sh same enc23-cards`)
#   cross-family:        bp.sh cross <rule> <tag> <boss,boss,...>     (c-bosspilot-rules05: a family 05 rule on other bosses;
#                                                                      cross05.sh drives the wave-2 set, tag w2)
#                        bp.sh cross06 <rule> <tag> <boss,boss,...>   (c-bosspilot-rules06: a family 06 rule on other bosses)
#   bp.sh same <name>    compare the two logs of a one-run-per-call mode (a vs b, and vs the baseline)
# A harness that overflows its stack does not exit: every run is wrapped in `timeout` (exit 124 = stopped by it; LIMIT=<s>).
. "$(dirname "$0")/../lib/env.sh"
m=$1
R="$OUT/bp"; LOGS="$R/logs"
E=$SUITES/p8f01/c-bosspilot-encounter
E5=$SUITES/p8f05/c-bosspilot-rules05
E6=$SUITES/p8f06/c-bosspilot-rules06
E9=$SUITES/p8f09/c-bosspilot-enc09
# fixture files handed to a harness through ENCOUNTERS_JSON: relative to the suite folder the harness runs in
FX=$UP/fixtures
ENC01=ulda_40_icarax,gila_61_hagatha,dala_21_whirt,trl_200_voone,loot_35_brandlemar,loot_48_trapped_room,gila_25_ivan,ulda_44_trap_room
OLD01=ulda_40_icarax,gila_61_hagatha,dala_21_whirt,trl_200_voone
# family 02 (scratchpad p8f02/run.sh): the fixture bosses and the 33 cards; the run player's four groups
BOSSES2=fix02_paladin,fix02_warrior,fix02_hunter,fix02_mage,fix02_priest,fix02_shaman,fix02_warlock,fix02_mech
WATCH2=AT_027,AT_079,AT_081,AT_115,BOT_436,DAL_573,DAL_729,DAL_731,EX1_112,GIL_655,GVG_017,GVG_025,GVG_050,GVG_054,GVG_097,GVG_107,GVG_108,GVG_111,GVG_113,GVG_119,LOE_009,LOE_022,LOE_029,LOE_047,LOE_053,LOE_061,LOE_092,LOE_107,LOE_119,TRL_318,DAL_431,GVG_014,GVG_049
G1=AT_079,DAL_573,EX1_130,EX1_610,AT_081,BOT_436,DAL_731,LOE_053,CS2_087,CS2_087,AT_115,LOE_061,LOE_107,GIL_655
G2=GVG_054,GVG_050,LOE_009,LOE_022,GVG_113,GVG_017,GVG_049,LOE_047,LOE_029,GVG_097,GVG_108,LOE_119,GVG_014,GVG_025,CS2_146
G3=TRL_318,GVG_119,EX1_112,GVG_107,DAL_729,DAL_431,GVG_111,GVG_006,GVG_006,AT_027,EX1_238,EX1_248
G4=LOE_092,LOE_092,LOE_119,LOE_119,LOE_022,LOE_022
# family 03 (scratchpad p8f03/run.sh): the thirty cards, the seven fixture bosses in three slices
FAMILY3=ULD_131,ULD_135,ULD_138,ULD_143,ULD_151,ULD_154,ULD_158,ULD_169,ULD_172,ULD_173,ULD_177,ULD_179,ULD_194,ULD_195,ULD_205,ULD_206,ULD_208,ULD_209,ULD_212,ULD_215,ULD_216,ULD_236,ULD_239,ULD_253,ULD_256,ULD_258,ULD_262,ULD_268,ULD_270,ULD_274
SLICE_a=fix03_druid,fix03_hunter,fix03_shaman;  GAMES_a=8
SLICE_b=fix03_warrior,fix03_mage,fix03_priest;  GAMES_b=8
SLICE_c=fix03_paladin;                          GAMES_c=16
# family 04 (scratchpad p8f04/run.sh): the 23 cards a boss may play, the six Quests and their rewards
BOSSCARDS4=ULD_282,ULD_286,ULD_288,ULD_290,ULD_292,ULD_293,ULD_304,ULD_327,ULD_329,ULD_410,ULD_435,ULD_438,ULD_707,ULD_709,ULD_713,ULD_714,ULD_716,ULD_718,ULD_720,ULD_721,ULD_726,ULD_727,ULD_728
QUESTS4=ULD_291,ULD_326,ULD_431,ULD_433,ULD_711,ULD_724
REWARDS4=ULD_291p,ULD_326p,ULD_431p,ULD_433p,ULD_711p3,ULD_724p
A7=loot_04_sturmi,loot_05_hamm,loot_06_blackseed,loot_09_frostfur,loot_10_graves,loot_11_mogark,loot_12_candlebeard,loot_13_thaddock,loot_18_giant_rat
B7=loot_21_gnosh,loot_23_bristlesnarl,loot_24_mothergloop,loot_27_sturmi,loot_28_hamm,loot_29_blackseed,loot_30_mogark,loot_44_wee_whelp,loot_45_bink
W7=loot_44_wee_whelp
PAD7=$W7,$W7,$W7,$W7,$W7,$W7,$W7,$W7,$W7,$W7,$W7,$W7,$W7,$W7,$W7,$W7,$W7,$W7,$W7,$W7,$W7,$W7,$W7,$W7,$W7,$W7,$W7,$W7
SHR7=loot_06_blackseed,loot_29_blackseed,loot_21_gnosh,loot_04_sturmi,loot_27_sturmi,loot_05_hamm,loot_28_hamm,loot_13_thaddock,loot_09_frostfur
CARDS8=LOOTA_102,LOOTA_104,LOOTA_105,LOOTA_107,LOOTA_109
R5BOSSES=tunnel_rat,loot_35_brandlemar,gila_25_ivan,trl_200_voone,dala_21_whirt,ulda_44_trap_room
R5NOSHRINE=tunnel_rat,loot_35_brandlemar,gila_25_ivan,loot_48_trapped_room,dala_21_whirt,ulda_44_trap_room
# family 06: Aleatory never meets a boss that plays Secrets from its deck
R6NOSECRETS=tunnel_rat,loot_04_sturmi,loot_19_gutmook,trl_200_voone,loot_47_lava_chamber,loot_21_gnosh
# family 09
TREASURES9=LOOTA_806,LOOTA_812,LOOTA_816,LOOTA_819,LOOTA_821,LOOTA_823,LOOTA_826,LOOTA_827,LOOTA_835,LOOTA_836,LOOTA_838,LOOTA_839,LOOTA_842b,LOOTA_843,LOOTA_847,LOOT_998k
BEAMS9=LOOTA_BOSS_26p2,LOOTA_BOSS_26p3,LOOTA_BOSS_26p4,LOOTA_BOSS_26p5,LOOTA_BOSS_26p6,LOOTA_BOSS_26p7
DECK9=LOOTA_BOSS_20t,BRMA12_8t,KARA_00_05,KARA_00_07,KARA_00_08,LOOTA_BOSS_50t,LOOTA_823,LOOTA_812,OG_254,LOEA_01,LOEA_01H
# family 10
A10=gila_20_mangy_wolf,gila_21_rottooth,gila_22_willie,gila_23_grubb,gila_26_gnomenapper,gila_27_experiment_3c,gila_29_sazzmi,gila_31_gnarlroot
B10=gila_32_niira,gila_33_scarecrow,gila_34_havnixx,gila_35_chupacabran,gila_36_griselda,gila_37_wharrgarbl,gila_38_cozzlewurt
CARDS10=GILA_BOSS_23t,GILA_BOSS_26t2,GILA_BOSS_27t,GILA_BOSS_29t,GILA_BOSS_35t,GILA_BOSS_37t
W10=gila_38_cozzlewurt
PAD10=$W10,$W10,$W10,$W10,$W10,$W10,$W10,$W10,$W10,$W10,$W10,$W10,$W10,$W10,$W10,$W10,$W10,$W10,$W10,$W10,$W10,$W10,$W10,$W10,$W10,$W10,$W10,$W10
SHR10=gila_26_gnomenapper,gila_27_experiment_3c,gila_29_sazzmi,gila_32_niira,gila_35_chupacabran,gila_36_griselda,gila_22_willie,gila_26_gnomenapper,gila_29_sazzmi
# family 23
CARDS23=GILA_BOSS_49t,GILA_BOSS_49t2,GILA_BOSS_60t,GILA_BOSS_99t,GILA_BOSS_99t2,GILA_BOSS_99t3,GILA_503,GILA_907,GILA_BOSS_61t,GILA_BOSS_61t3
FIX23=fix23_glinda,fix23_shivers,fix23_toki,fix23_godfrey

strip() { sed -E "s/ +[0-9.]+s$//" "$1" | tr -d '\r'; }
need() { # need <suite dir>: cd into it, or stop when it is not ported yet
  [ -n "$(ls "$1"/*.csproj 2>/dev/null)" ] || { echo "bosspilot $m: NOT PORTED (no ${1#$VERIFY/}/*.csproj)"; exit 0; }
  [ -f "$ENGINE_DLL" ] || { echo "no engine build: run bash verify/bin/engine.sh first" >&2; exit 3; }
  cd "$1" || exit 1
}
compare() { # compare <name>: a vs b, stderr, and the recorded baseline
  local name=$1 base
  cmp -s "$R/bp-$name-a.txt" "$R/bp-$name-b.txt" && echo "bosspilot $name identical" || echo "bosspilot $name DIFFER"
  echo "stderr bytes: $(cat "$LOGS/$name-err-1.txt" "$LOGS/$name-err-2.txt" 2>/dev/null | wc -c | tr -d ' ')"
  if [ -f "$BASE/bp/bp-$name-a.txt" ]; then
    echo "bosspilot $name vs wave-3 baseline: $(diff <(tr -d '\r' < "$BASE/bp/bp-$name-a.txt") "$R/bp-$name-a.txt" | grep -c '^[<>]') differing lines"
  elif [ -f "$BASE/bp-port/bp-$name-a.txt" ]; then
    # a mode wave 3 did not rerun: recorded by the port on the wave-3 tree (README.md sections 4 and 6)
    echo "bosspilot $name vs port record (wave-3 tree): $(diff <(tr -d '\r' < "$BASE/bp-port/bp-$name-a.txt") "$R/bp-$name-a.txt" | grep -c '^[<>]') differing lines"
  elif [ -f "$BASE/bp-w1/bp-$name-a.txt" ]; then
    # the seven shrine modes, fixture, catalog: last run in wave 1 (p8w1); the port reproduced them exactly on the wave-3 tree
    echo "bosspilot $name vs wave-1 record (not rerun in waves 2 and 3): $(diff <(tr -d '\r' < "$BASE/bp-w1/bp-$name-a.txt") "$R/bp-$name-a.txt" | grep -c '^[<>]') differing lines"
  elif [ -f "$BASE/bp-w2/bp-$name-a.txt" ]; then
    echo "bosspilot $name vs wave-2 reference (not rerun in wave 3): $(diff <(tr -d '\r' < "$BASE/bp-w2/bp-$name-a.txt") "$R/bp-$name-a.txt" | grep -c '^[<>]') differing lines"
  else
    echo "bosspilot $name: no recorded baseline"
  fi
}
twice() { # twice <name> <env...>: two runs with the given environment, compared without the timing column
  local name=$1 k rc; shift
  for k in 1 2; do
    timeout "${LIMIT:-560}" env "$@" dotnet out/AiHarness.dll > "$LOGS/$name-$k.txt" 2> "$LOGS/$name-err-$k.txt"; rc=$?
    [ $rc -eq 124 ] && echo "bosspilot $name $k: STOPPED by the time limit (hung or too slow)"
    echo "bosspilot $name $k exit=$rc $(tail -n 1 "$LOGS/$name-$k.txt" | tr -d '\r')"
  done
  strip "$LOGS/$name-1.txt" > "$R/bp-$name-a.txt"; strip "$LOGS/$name-2.txt" > "$R/bp-$name-b.txt"
  compare "$name"
}
once() { # once <name> <k> <env...>: one run of a mode that is too long to run twice in one call; `same <name>` compares
  local name=$1 k=$2 rc; shift 2
  case "$k" in 1|2) ;; *) echo "bosspilot $name: give the run number (1 or 2)"; exit 2 ;; esac
  timeout "${LIMIT:-560}" env "$@" dotnet out/AiHarness.dll > "$LOGS/$name-$k.txt" 2> "$LOGS/$name-err-$k.txt"; rc=$?
  [ $rc -eq 124 ] && echo "bosspilot $name $k: STOPPED by the time limit (hung or too slow)"
  echo "bosspilot $name $k exit=$rc $(tail -n 1 "$LOGS/$name-$k.txt" | tr -d '\r'); stderr bytes $(wc -c < "$LOGS/$name-err-$k.txt" | tr -d ' ')"
}
released() { # released <name> <env...>: the released c-bosspilot / shrine harnesses, twice
  local name=$1 k rc; shift
  for k in 1 2; do
    env "$@" dotnet out/AiHarness.dll > "$LOGS/$name-$k.txt" 2> "$LOGS/$name-err-$k.txt"; rc=$?
    echo "bosspilot $name $k exit=$rc $(tail -n 1 "$LOGS/$name-$k.txt" | tr -d '\r')"
  done
  strip "$LOGS/$name-1.txt" > "$R/bp-$name-a.txt"; strip "$LOGS/$name-2.txt" > "$R/bp-$name-b.txt"
  compare "$name"
}

case $m in
same) name=$2; [ -n "$name" ] || { echo "usage: bp.sh same <name>"; exit 2; }
  strip "$LOGS/$name-1.txt" > "$R/bp-$name-a.txt"; strip "$LOGS/$name-2.txt" > "$R/bp-$name-b.txt"
  compare "$name" ;;
enc01) need $E; twice enc01 BOSS=$ENC01 ;;
old01) need $E; twice old01 BOSS=$OLD01 ;;
secret4) need $E; twice secret4 BOSS=loot_35_brandlemar,loot_48_trapped_room,gila_25_ivan,ulda_44_trap_room GAMES=6 ;;
secretfix) # fixture row: Manhunter Ivan with two Streetsmugglers (TRLA_168 in a boss deck; boss-side generated Secrets on)
  need $E; twice secretfix GAMES=6 WATCH=TRLA_168,ICC_204 BOSS=fix_168_smuggler ENCOUNTERS_JSON=$FX/p8f01b/fixtures/encounters-secretfix.json ;;
rules01) need $E; for r in ${2:-DALA_850 DALA_864 DALA_866}; do twice rules01-$r RULE=$r BOSS=dala_21_whirt,trl_200_voone,gila_61_hagatha; done ;;
plague) # Icarax: each phase starts a game (carried 110, 210; 0 is in enc01) and each threshold is crossed in play (95, 195, 199)
  need $E; for c in 95 110 195 199 210; do twice plague-$c CARRIED=$c BOSS=ulda_40_icarax; done ;;
finale) # fixture schedule (the wave-1 acts-v3h) with its finale enabled on two shipped encounters
  need $E; twice finale FINALE=1 SCHEDULE_JSON=$FX/p8f01/fixtures/schedule-finale.json ;;
relay) # fixture finale row with two relay heroes; the run hero starts at 1 Health, so the relay heroes enter
  need $E; twice relay FINALE=1 PLAYER_HEALTH=1 SCHEDULE=acts-v3t SCHEDULE_JSON=$FX/p8f01/fixtures/schedule-relay.json ENCOUNTERS_JSON=$FX/p8f01/fixtures/encounters-relay.json ;;
# ---- wave 1, families 02-04 (scratchpad p8f02, p8f03, p8f04 run.sh), with their all-cards-played checks
collectA) need $E; twice collectA GAMES=4 WATCH=$WATCH2 BOSS=$BOSSES2 ENCOUNTERS_JSON=$FX/p8f02/fixtures/encounters-collectA.json
  # every one of the 33 cards was played from hand by the boss at least once
  unplayed=$(tail -n 1 "$R/bp-collectA-a.txt" | sed 's/.*, watched //; s/, most boss Secrets.*//' | grep -oE "[A-Z0-9_]+ x0( |$)" | xargs)
  [ -z "$unplayed" ] && echo "bosspilot collectA: all 33 cards played by the boss" || echo "bosspilot collectA UNPLAYED: $unplayed" ;;
collectP) # the run player's deck holds a group of the cards against shipped bosses piloted by the fair AI
  g=$2; case "$g" in 1|2|3|4) ;; *) echo "usage: bp.sh collectP <group 1..4>"; exit 2 ;; esac
  cards=G$g; pb=gila_25_ivan,loot_48_trapped_room; [ "$g" = 4 ] && pb=loot_35_brandlemar
  need $SUITES/p8f02/c-bosspilot-collect; twice collectP$g GAMES=3 WATCH=$WATCH2 BOSS=$pb PLAYER_CARDS=${!cards} ;;
enc03) # the seven fixture bosses whose decks hold all thirty cards, one slice per call (a, b: 8 games each; c: 16)
  s=$2; case "$s" in a|b|c) ;; *) echo "usage: bp.sh enc03 <slice a|b|c>"; exit 2 ;; esac
  bosses=SLICE_$s; games=GAMES_$s
  need $E; twice enc03$s BOSS=${!bosses} GAMES=${!games} WATCH=$FAMILY3 ENCOUNTERS_JSON=$FX/p8f03/fixtures/encounters-f03.json ;;
enc03-check) # after the three slices: a boss played every family card at least once (the WATCH counts of the last lines)
  missing=""
  for id in ${FAMILY3//,/ }; do
    total=0
    for s in a b c; do
      [ -f "$R/bp-enc03$s-a.txt" ] || { echo "enc03: slice $s not run"; exit 1; }
      k=$(tail -n 1 "$R/bp-enc03$s-a.txt" | grep -o "$id x[0-9]*" | sed 's/.* x//'); total=$((total + ${k:-0}))
    done
    [ $total -ge 1 ] || missing="$missing $id"
  done
  [ -z "$missing" ] && echo "enc03: the boss played every family card at least once" || echo "enc03: NOT PLAYED by a boss:$missing" ;;
enc01-f03) need $E; twice enc01-f03 BOSS=$ENC01 WATCH=$FAMILY3 ;;
icarax2) need $E; twice icarax2 BOSS=ulda_40_icarax CARRIED=110 GAMES=4 WATCH=$FAMILY3 ;;
f04-boss) # the five boss fixture rows, 8 games each; every one of the 23 cards must be played by the fair-AI boss
  need $E; twice f04-boss FAMILY=04 GAMES=8 ENCOUNTERS_JSON=$FX/p8f04/fixtures/encounters-f04.json WATCH=$BOSSCARDS4
  unplayed=$(tail -n 1 "$R/bp-f04-boss-a.txt" | grep -o 'ULD_[0-9a-z]* x0\b' | xargs)
  [ -z "$unplayed" ] && echo "f04-boss: the boss played every one of the 23 cards" || echo "f04-boss FAIL: never played by the boss: $unplayed" ;;
f04-player) # the six Quest fixture rows (the run player holds the Quest), 4 games each; REQUIRE=1 as in the family run
  need $SUITES/p8f04/c-bosspilot-f04; twice f04-player FAMILY=04p GAMES=4 REQUIRE=1 ENCOUNTERS_JSON=$FX/p8f04/fixtures/encounters-f04.json PLAYER_WATCH=$QUESTS4,$REWARDS4,$BOSSCARDS4 ;;
f04-icarax) # the shipped Icarax row (its three decks list ULD_720, ULD_707, ULD_721); carried 110 / 210 = phase 2 / 3
  c=$2; case "$c" in 0|110|210) ;; *) echo "usage: bp.sh f04-icarax <0|110|210>"; exit 2 ;; esac
  need $E; twice f04-icarax$c BOSS=ulda_40_icarax GAMES=4 CARRIED=$c WATCH=ULD_720,ULD_707,ULD_721 ;;
enc07a) need $E; twice enc07a BOSS=$A7 GAMES=4 ;;
enc07b) need $E; twice enc07b BOSS=$B7 GAMES=4 ;;
shrine07) need $E; twice shrine07 BOSS=$PAD7,$SHR7 GAMES=1
  echo "fights against a shrine row (TRLA_209h_*) by the nine rows: $(grep -v '^loot_44' "$R/bp-shrine07-a.txt" | grep -c 'TRLA_209h_') of 9" ;;
seeds07) need $E; twice seeds07-$2 FAMILY=07 GAMES=2 SEED_BASE=$2 ;;
enc08) need $E; twice enc08 FAMILY=08 WATCH=LOOTA_BOSS_110,LOOTA_109,LOEA10_3 ;;
enc08-tag) need $E; twice enc08-tag BOSS=loot_15_george_karl GAMES=8 WATCH=LOOTA_BOSS_110 ;;
enc08-cards) need $E; twice enc08-cards BOSS=fix08_cards,fix08_kraxx GAMES=6 WATCH=$CARDS8 ENCOUNTERS_JSON=$FX/p8f08/fixtures/encounters-f08.json
  unplayed=$(tail -n 1 "$R/bp-enc08-cards-a.txt" | sed 's/.*, watched //; s/, most boss Secrets.*//' | grep -oE "[A-Z0-9_]+ x0( |$)" | grep -v LOOTA_102 | xargs)
  [ -z "$unplayed" ] && echo "bosspilot enc08-cards: the boss played Reverberating Ooze, Deadly Spore, Shroomsayer and Gluttonous Trogg from hand" || echo "bosspilot enc08-cards UNPLAYED: $unplayed" ;;
enc08-trapped) need $E; twice enc08-trapped BOSS=loot_48_trapped_room GAMES=6 WATCH=LOOTA_107 ;;
enc08-seeds) need $E; twice enc08-seeds FAMILY=08 SEED_BASE=19000 WATCH=LOOTA_BOSS_110,LOOTA_109,LOEA10_3 ;;
rules05)
  rule=$2; bosses=$R5BOSSES; case $rule in DALA_831|DALA_833) bosses=$R5NOSHRINE ;; esac
  need $E5; twice rules05-$rule GAMES=2 RULE=$rule BOSS=$bosses ;;
cross) need $E5; twice cross-$3-$2 GAMES=${GAMES:-1} RULE=$2 BOSS=$4 ;;
# ---- wave 3
rules06)
  rule=$2; bosses=$R5BOSSES; case $rule in ULDA_706) bosses=$R5NOSHRINE ;; ULDA_721) bosses=$R6NOSECRETS ;; esac
  need $E6; twice rules06-$rule GAMES=2 RULE=$rule BOSS=$bosses ;;
cross06) need $E6; twice cross06-$3-$2 GAMES=${GAMES:-1} RULE=$2 BOSS=$4 ;;
enc09) need $E; twice enc09 FAMILY=09 WATCH=$DECK9,$BEAMS9 ;;
enc09-seeds) need $E; twice enc09-seeds FAMILY=09 SEED_BASE=19000 WATCH=$DECK9,$BEAMS9 ;;
enc09-auto) need $E9; twice enc09-auto BOSS=loot_20_brimstone,loot_34_azari,loot_53_inara,loot_52_vault AUTO=loot_20_brimstone,loot_34_azari,loot_53_inara,loot_52_vault GAMES=4 ;;
enc09-finals) need $E9; twice enc09-finals BOSS=loot_25_vustrasz,loot_26_xol,loot_34_azari,loot_49_darkness,loot_99_togwaggle AUTO=loot_34_azari GAMES=4 WATCH=$BEAMS9,BRMA12_8t,LOEA_01H,$TREASURES9 ;;
enc09-rooms) need $E9; twice enc09-rooms BOSS=loot_22_tad,loot_52_vault,loot_54_kay,loot_50_max AUTO=loot_52_vault GAMES=4 WATCH=LOOTA_BOSS_50t,LOOTA_823,LOOTA_812,OG_254,LOOTA_107 ;;
enc09-cards) need $E9; twice enc09-cards BOSS=fix09_treasures,fix09_beams,fix09_chests,fix09_medivh GAMES=6 WATCH=$TREASURES9,$BEAMS9,KARA_00_05,KARA_00_07,KARA_00_08,BRMA12_8t,LOOTA_BOSS_20t ENCOUNTERS_JSON=$FX/p8f09/fixtures/encounters-f09.json ;;
enc10) need $E; twice enc10 FAMILY=10 WATCH=$CARDS10 ;;
enc10a) need $E; once enc10a "$2" BOSS=$A10 GAMES=4 WATCH=$CARDS10 ;;
enc10b) need $E; once enc10b "$2" BOSS=$B10 GAMES=4 WATCH=$CARDS10 ;;
enc10-nab) need $E; twice enc10-nab BOSS=gila_26_gnomenapper GAMES=10 WATCH=GILA_BOSS_26t2 ;;
enc10-cards) need $E; once enc10-cards "$2" BOSS=fix10_grubb,fix10_gnomenapper,fix10_experiment,fix10_sazzmi,fix10_chupacabran,fix10_wharrgarbl GAMES=6 WATCH=$CARDS10 ENCOUNTERS_JSON=$FX/p8f10/fixtures/encounters-f10.json ;;
enc10-shrine) need $E; once enc10-shrine "$2" BOSS=$PAD10,$SHR10 GAMES=1
  echo "fights against a shrine row (TRLA_209h_*) by the nine rows: $(grep -v "^$W10" "$LOGS/enc10-shrine-$2.txt" | grep -c 'TRLA_209h_') of 9" ;;
enc10-seeds) need $E; twice enc10-seeds FAMILY=10 SEED_BASE=19000 WATCH=$CARDS10 ;;
enc10-hagatha) need $E; twice enc10-hagatha BOSS=gila_61_hagatha GAMES=6 WATCH=GILA_BOSS_27t ;;
enc23) need $E; twice enc23 FAMILY=23 WATCH=$CARDS23 ;;
enc23-seeds) need $E; twice enc23-seeds FAMILY=23 SEED_BASE=19000 WATCH=$CARDS23 ;;
enc23-cards) need $E; once enc23-cards "$2" BOSS=$FIX23 GAMES=4 WATCH=$CARDS23 ENCOUNTERS_JSON=$FX/p8f23/fixtures/encounters-f23.json ;;
# ---- the released harnesses
run) need $SUITES/audit-verify/c-bosspilot; released run ;;
fixture) need $SUITES/audit-verify/c-bosspilot; released fixture SECRET_FIXTURE=1 ;;
catalog) need $SUITES/audit-verify/c-bosspilot; released catalog CATALOG_RUN=1 ;;
act) need $SUITES/audit-verify/c-bosspilot; released act ACT_RUN=1 ;;
shrine) need $SUITES/p7/c-bosspilot-shrine; released shrine SHRINE_RUN=1 ;;
shrineS2) need $SUITES/p7s2/c-bosspilot-shrine-S2; released shrineS2 SHRINE_RUN=1 ;;
shrineS3) need $SUITES/p7s3/c-bosspilot-shrine-S3; released shrineS3 SHRINE_RUN=1 ;;
shrineU) need $SUITES/p7u/c-bosspilot-shrine-U; released shrineU SHRINE_RUN=1 ;;
shrineT1) need $SUITES/p7t1/c-bosspilot-teammates-T1; released shrineT1 SHRINE_RUN=1 ;;
shrineT2) need $SUITES/p7t2/c-bosspilot-teammates-T2; released shrineT2 SHRINE_RUN=1 ;;
shrineD) need $SUITES/p7d/c-bosspilot-companions-D; released shrineD SHRINE_RUN=1 ;;
*) sed -n 2,29p "$0"; exit 2 ;;
esac
