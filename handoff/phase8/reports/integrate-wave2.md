WAVE 2 INTEGRATION REPORT (families 05, 07, 08) - main tree C:\SoloStone, everything uncommitted

Integration scripts, logs and evidence: C:\Users\user\AppData\Local\Temp\claude\0a564ffc8743aeec\d2a4e245-ca37-4433-9b55-c35ed325f081\scratchpad\p8w2 (builds.sh, runs.sh, same.sh, bp.sh, hall.sh, same.log, hall-1.txt, bp-*-a/b.txt, int-diff*/int-merge* outputs, gen1/gen2). Baseline for comparisons was the wave 1 logs (p8w1).

1. STATUS BEFORE THE FIRST MERGE
`lanes.py status`: base stamp 20261113-013347, main differs from base in 0 files. No lane wrote into main.

2. MERGE COUNTS (each lane ends with `merge <k> --apply` exit 0; final `diff 1/2/3` all exit 0, nothing left to apply)
- Lane 1 (05): CONFLICT 1, generated-skip 3, new-file 65, take-lane 13 -> final already-merged 79, generated-skip 3.
- Lane 2 (07): CONFLICT 3, generated-skip 3, merged-clean 1, new-file 20, take-lane 5 -> final already-merged 29, generated-skip 3.
- Lane 3 (08): CONFLICT 5, generated-skip 3, merged-clean 1, new-file 19, take-lane 5 -> final already-merged 30, generated-skip 3.
No lane deletions or renames. One tool note: p8f07\bp-damage-loop-evidence.txt (new scratch evidence log) has CRLF line ends; left as the lane wrote it (not a repo file). No BOM/CRLF convention changed in any repo file (checked all 26 changed/new repo files against the base; Controller.cs and GvgCardsGen.cs keep their pre-existing BOM).

3. CONFLICTS AND HOW EACH WAS RESOLVED
- L1 sp p8f05/run.sh (check: relative path ../repo): written by hand; repo root is now `R=${REPO:-/c/SoloStone}` (literal, so a lane copy is rewritten); `resolved`.
- L2 sp p8f07/run.sh (lane path L2/repo left): written by hand with R=/c/SoloStone and S=the scratchpad literal; `resolved`.
- L2 and L3 repo tools/restoration-ledger/curation.json (overlap at the end of landedFamilies and in the tool-owned lists): hand-inserted E07 then E08 after E05 (helper p8w2\cur_merge.py verifies that, outside the two tool-owned lists, the lane differs from base by exactly its entry); tool-owned lists regenerated in step 4; `resolved` for both lanes. landedFamilies now ends E04, E05, E07, E08.
- L2 and L3 sp p8f01/suites/c-enc-model/Program.cs (A4, J13): J13 keeps lane 2's family-range check (covers lane 3's "01 or 08"), lane 1's rule-registry lines and lane 3's eight-entry passive list; A4 keeps lane 2's family-agnostic order check plus a per-family count pin (see pins).
- L3 sp audit-verify/c-admission/Program.cs: both exemption statements kept (F07's three family names, F08's eight).
- L3 sp audit-verify/c-catalog/Program.cs and c-deckops/Program.cs (ledger pins moved by both lanes): recomputed from the merged tree (below).
Merged-clean files reviewed against the base: EncounterRules.cs (lane 1 tags + ResolveStart call, lane 3 TagTeamDeath line: both present), c-admission, c-enc-model.

4. SEMANTIC CHECKS AND FIXES (main tree)
- Tags: unique. TagTeamBench 32643 moved from EncounterRules.F08.cs into the tag table of EncounterRules.cs. Table now: 32630-32637, 32643, 32660 RuleApplied, 32661 RuleSpells, 32662 RuleSummons (new, see below). Table comment now states the reservation rule (32635 + NN).
- No encounter card registered twice (110 registered ids, 88 of this wave; none also defined elsewhere in the engine); 637 admission ids, none twice; 22 rule rows, 43 encounter rows, no duplicate ids; schedule-v1.json and curation.json parse with no duplicate keys; no lane path in the main tree or the merged scratch suites.
- ENGINE FAULT FOUND BY CROSS-FAMILY PLAY, FIXED: DALA_865 Explosive never terminated with Eternal Bloodpetal (TLC_234, 5/1) and its Eternal Seedling (TLC_234t), which summon each other on death and explode on each other; the boss's search hung (no stack overflow, no exit) in Blackseed's fight. Not a merge artifact: it is in family 05's rule alone and a real game would hang too. Fix in DungeonRunEncounters.F05.cs: the rule marks at most 30 minions of one player per turn (ExplosiveMarksPerTurn; counter tag RuleSummons 32662 on that player's rule enchantment, reset at each turn end). Regression: c-rules-05 M5-M7. Recorded as ruleAdaptations "explosive-limit". Evidence: p8w2\dbg865\loop.txt.
- GVG_054 Ogre Warmaul (lane 1 flag 3): `FastExecution = true` added in GvgCardsGen.cs; c-rules-05 J7 is now a check (0 of 24 attacks fault; was 13 of 24).
- c-rules-05 Q14 fixed (test assumption, not engine): with Murloc Tinyfin scripted by family 08 the prisoner pool shifted and seed 6 drew a 1-Cost prisoner that is already released; the check now reads the released minion.
- c-enc-07 E2/E10 updated: Gnosh and The Mothergloop decks are authentic now (family 08 scripted LOOTA_104/107/109), as lane 2 predicted.
- Not changed: lane 2's FIR_955 edit and lane 3's Controller.cs edit were taken as written (hall and selfplay unchanged, below).
- docs\design\dungeon-run.md: added section "Wave 2" (family 05 text from p8f05\dungeon-run-family-05.md, families 07 and 08, integration changes); script p8w2\doc_wave2.py.

5. GENERATORS (encounters, catalog, ledger; run twice, six files byte-identical)
- encounters: 43 rows, 47 decks (32 authentic, 15 changed), 56 adaptations, 1 rejected.
- catalog: 506 entries, 6508 admitted / 1 excluded (byte-identical to the base).
- LEDGER: 4644 rows; implemented 2813, partial 165, missing 608, duplicate/support 1058, intentionally adapted 0.
Fixture steps rerun: p8f02, p8f03, p8f04, p8f08, p8f05 (schedule-twists.json), and `p8f05/run.sh harness` (no change). Not regenerated: p8f01/p8f01b fixtures (relay, finale, secretfix), which have no make step.

6. BUILDS
Engine 0 warnings / 0 errors; plugin 0 errors / 75 warnings (same warning set as wave 1); 61 suite builds + randplay 0 errors, every suite holds the final engine dll.

7. PINS RECOMPUTED FROM THE MERGED TREE
- c-catalog ledger golden: 4579 / 2813 / 40 / 668 -> 4644 / 2813 / 165 / 608. Attribution: +65 rows, all partial (35 run:boss rows: 18 of F07, 17 of F08; 30 deck cards new to the ledger); 60 rows missing -> partial (E05 18, E07 19, E08 23).
- c-deckops T5: 4579 -> 4644 rows.
- c-admission total: 2584 -> 2854 (F05 +111, F07 +69, F08 +90); NOTE generated lists 283 -> 351 for both sides.
- c-enc-model A4: family counts pinned as "01:5,07:18,08:17,13:1,23:1,24:1" (the foundation's eight rows carry their mechanism's family: five 01, one each 13, 23, 24). Still 141 checks.
- c-content passive list: 48p + F08's seven (lane 3's value, confirmed by the run). Still 904.
- c-enc-secrets S14: Trapped Room authentic (lane 3's value, confirmed). Still 60.
- randplay digest F6F0322332327894 -> 40207800B289E504 (Murloc Tinyfin LOEA10_3 is now scripted and playable; FIR_955 and GVG_054 changed). selfplay digest unchanged.
- Unchanged: c-acts A6, c-deckops L1.

8. SUITE RESULTS ON THE FINAL BUILD (run 1 = run 2 for every suite except c-secretsearch's timing lines, as in wave 1) - BASELINE FOR THE NEXT WAVE
c-runlayer 244, c-catalog 127, c-content 904, c-admission 2854, c-acts 181, c-deferrals 149, c-deckops 174, c-dkdh-supply 13, c-heroes 347, c-heroes-F14 316, c-heroes-F6 566, c-heroes-F9 292, c-runctx 40, c-starters-F10 239, c-treasures F1 229 F11 380 F12 321 F13 248 F2 321 F3 283 F4 236 F5 404 F7 367 F8 256, c-upgrades 34, c-dkdh-play 5, c-review-F18 35, c-shrine-core 131, c-shrines S1 107 S2 157 S3 158, c-shrine-upgrades 92, c-teammates T1 139 T2 207, c-companions-D 251, c-p7fix 40, c-enc-model 141, c-enc-engine 122, c-enc-secrets 60, c-collectibles A 115 B 108 C 102, c-w1fix 23, c-rules-05 170 (lane: 166; +J7, +M5-M7), c-enc-07 161, c-enc-08 113, c-secretscan 0 faults, c-secretsearch 689, selfplay 2CE546A18F96DCBC (identical to wave 1), randplay 40207800B289E504 (exit 127 pre-existing). All 0 failed.
Hall: 955 tests / 8285 checks, 0 bad, output identical to wave 1.
Informational differences vs wave 1 logs (c-treasures-F7/F11/F12, c-review-F18, c-collectibles-B) are all Murloc Tinyfin joining generation pools.

Bosspilot, each mode twice identical, 0 faults / 0 fallbacks / 0 failed checks / 0 swallowed leaf exceptions, stderr empty, power-unused 0:
- Released: run 40 games, boss wins 27; act 40 games, boss wins 26; both 0 differing lines vs wave 1.
- enc07a 36 games / 31 wins (0 lines vs lane); enc07b 36 / 32 (lane 30; only Gnosh and Mothergloop lines moved: authentic decks); shrine07 37 / 32, 9 of 9 shrine fights; seeds07 base 19000 36 / 30.
- enc08 34 / 27; enc08-tag 8 / 8 (Tag Team x13); enc08-cards 12 / 12 (Ooze, Spore, Shroomsayer, Trogg played from hand; Starving Crab x1); enc08-trapped 6 / 5 (0 lines vs lane); enc08-seeds 34 / 26. enc08 lines differ from the lane's because the harness seed uses the boss's index, which moved by family 07's 18 rows.
- rules05, 18 rules (16 games each, 12 for DALA_831/833), boss wins: 851 12, 852 11, 853 9, 854 12, 855 12, 856 11, 858 9, 859 10, 860 11, 861 9, 862 11, 863 12, 865 13, 867 7, 829 11, 831 9, 832 9, 833 8. Identical to lane evidence except 851, 858, 832 (Tinyfin in pools) and 831, 833 (Trapped Room's deck).
- Family 01: enc01 20 / 18 (wave 1: 17; only Trapped Room lines moved), secret4 24 / 17, rules01 x3 0 differing lines.
- Cross-family (added by me): each of the 18 rules against 14 bosses of families 07/08 (13 for Crying/Rattling), 1 game each, twice identical, 0 faults. Sixteen of these ran on the build before the Explosive fix; 865 and 867 on the final build (the fix touches only 865).

9. WHAT LATER FAMILIES MUST KNOW
- Tags taken: 32637 (F02), 32643 (F08), 32660-32662 (rules). Next free rule tag 32663. A family's reserved tag is 32635 + NN and goes into the table of EncounterRules.cs.
- New APIs: RuleHooks / Rule(id, effectId, effect, hooks), DungeonRunGlobalRules.HasStart / OpeningCards / CardsOf / ResolveStart, rule key incompatibleClasses; EncounterRules.F05.cs (BattlecriesTwice, DeathrattlesTwice, RecastRandom, PlaceBlockers, Imprison/TickPrison, SwapStatsOnSummon); F07 EvolvePool, HighestAttack; F08 PoisonousToHeroes, RecruitOne, TagTeam / TagTeamDeath / TagTeamBenchHealth.
- A trigger that changes an attack's defender must use FastExecution. A rule that grants a Deathrattle to summoned minions needs a bound (resummoning pairs exist: TLC_234 / TLC_234t).
- LOEA10_3 and ICCA07_004 / ICCA07_004e are registered (F08, F07): registering them again throws.
- Bosspilot harnesses can hang without exiting; p8w2\bp.sh wraps every run in `timeout`.
- Seeds in c-bosspilot-encounter depend on the boss's index, so a family's evidence moves when earlier rows are added.
- c-runlayer reads docs\design\dungeon-run.md.

10. OPEN / NOT DONE
- Twist pools of acts-v3h / acts-v3t are still empty (lane 1 flag 1): the four twists stay `partial`; left for the close-out.
- DALA_857 Glittering rejected (schedule row with reason), as lane 1 reported.
- Flags recast, forcedAttack, boardStart are on no shipped row yet.
- Nothing was checked in the real client.
- Debug folders I created and could not remove (directories are never deleted): p8w2\dbg865, p8w2\dbg-rules05, p8w2\probe (not mirrored to lanes).

MAIN_GREEN: yes
