PHASE 8 FAMILY 08 (Dungeon Run bosses B) - LANE 3 REPORT

All 41 ids are done; nothing is rejected. Everything is uncommitted and inside the lane (repo copy L3\repo, scratch L3\sp). Nothing was checked in the real client: all evidence is engine games and harnesses. No process of mine is running.

FLAGS FOR THE INTEGRATOR (read first)
- Private tag 32643 (`TagTeamBench`, my reserved tag) is declared in my own file `EncounterRules.F08.cs`, not in the tag table of `EncounterRules.cs`. Move it into the table as was done for 32637. No other tag was taken.
- Two engine edits outside family files (reasons in section 3): one line in `EncounterRules.cs`, three lines in `Model\Entities\Controller.cs`.
- `LOEA10_3` Murloc Tinyfin (a collectible vanilla minion with no card-database entry, so `Implemented` was false) is registered as `Token("LOEA10_3")` in F08. If another lane registers it too, the registry throws "registered twice" at load: keep one.
- No schedule default, finale, pool set, save version, ReleasedShapes or golden sequence was touched.
- The harness lock was starved for long stretches (lane 2 ran back-to-back slices); I used `HRUN_WAIT` up to 450. No run of mine overlapped another harness.

=====================================================================
1. PER ID
=====================================================================
Done, 17 encounter rows (all LOOT, family "08", sources hearthstone.wiki.gg boss pages read 2026-11-13; rounds from the Dungeon Run page's Bosses table):

| Row | Ids | Tier | Result |
|---|---|---|---|
| loot_15_george_karl | LOOTA_BOSS_15h, 15p, 32h, LOOTA_BOSS_110 | 3 | One tag-team encounter. Each hero has his own 30 Health (fixedHealth; the wiki: "30 HP each"). Tag Team swaps active and benched hero with their own damage; a defeated active hero is replaced by the other. 32h is the row's script form with AT_132_PALADIN. |
| loot_16_azun | 16h, 16p | 2 | Passive, all Deathrattles twice for both players |
| loot_17_jinzo | 17h, 17p | 2 | Passive, all Battlecries twice for both players |
| loot_38_vex | 38h, 38p | 3 | Passive, both of the above |
| loot_19_gutmook | 19h, 19p | 2 | Passive, Tunnel Trogg on the run player's spell |
| loot_33_gutmook | 33h, 33p | 3 | Passive, draw a card that costs (1) on the run player's spell |
| loot_31_candlebeard | 31h, 31p | 3 | Passive, Charge on minions the boss plays |
| loot_36_ixlid | 36h, 36p, LOOTA_105 | 3 | fixedHealth 20 (wiki). Deadly Spore: new `DungeonRunEncounters.PoisonousToHeroes()` |
| loot_37_lyris | 37h, 37p | 2 | Deck is the wiki's 20 Flamewakers / 5 Arcane Intellect / 4 Arcane Giant / Antonidas |
| loot_39_kraxx | 39h, 39p | 2 | |
| loot_40_seriona | 40h, 40p | 1 | |
| loot_41_whompwhisker | 41h, 41p | 2 | Both players Recruit; flags bothPlayers, hiddenZone |
| loot_42_jari | 42h, 42p | 2 | |
| loot_43_jeeru | 43h, 43p | 3 | Each player draws 3; flags bothPlayers, hiddenZone |
| loot_46_flurgl | 46h, 46p | 2 | |
| loot_47_lava_chamber | 47h, 47p | 3 | Passive, 2 damage to every minion played by either side |
| loot_51_russell | 51h, 51p | 2 | Control of a minion with 2 or less Attack; never a run shrine |

Boss deck minions, all scripted and admitted:
- LOOTA_109 Gluttonous Trogg: in both Gutmook decks.
- LOOTA_107 Shroomsayer: Trapped Room's deck (family 01) is now the wiki's list unchanged.
- LOOTA_105 Deadly Spore: summoned by Ixlid's power.
- LOOTA_104 Reverberating Ooze and LOOTA_102 Starving Crab: no shipped deck lists them yet. Their decks are The Mothergloop (family 07) and Tad (family 09). They are played through my fixture rows.

Decks: 13 of 17 are the wiki's list unchanged. Four replace cards the engine does not script, each recorded by the encounters step and reverting automatically once scripted:
- Wrath EX1_154 (Ixlid, Jari, Jeeru) -> Lesser Jasper Spellstone / Starfall
- Power of the Wild EX1_160 (Ixlid) -> Mark of the Lotus
- Mogor's Champion AT_088 (Whompwhisker) -> Boulderfist Ogre
- Fel Reaver GVG_016 (Jeeru) -> Bittertide Hydra
The Core reprints of Wrath and Power of the Wild are scripted, but the encounters step reads client ids from the facts "cards" list only, so a CORE_ id is "client-unknown" there.

Recorded adaptations (in F08.json, written to encounterAdaptations):
- The wiki's auto-cast powers (36p, 41p, 43p) are an AI choice.
- A clickable power that would do nothing is not offered (15p, 36p, 37p, 39p, 41p, 46p), and Fading Light needs a target with Attack left. Each condition reads only public boards or the boss's own hand count.
- Identity Health for George and Karl and for Ixlid; both rows list incompatibleModifiers mod_iron_bosses and mod_open_road, as Icarax does.

Admission: 8 rows, 23 ids. Powers and Tag Team are Boss only. The five boss deck minions are Player | Boss with NeverGenerated. I first made them Boss only; the ledger then validated the two unused ones (102, 104) as pool cards and c-catalog's "no ai_support ok:F with blocked_by ai:F" check failed. A boss deck minion can also reach the run player through a deck steal, and its effect is public either way (tested in the player's hands).

=====================================================================
2. FILES CHANGED - OWN FAMILY FILES
=====================================================================
- C:\Users\user\AppData\Local\Temp\claude\0a564ffc8743aeec\d2a4e245-ca37-4433-9b55-c35ed325f081\scratchpad\L3\repo\engine\SabberStoneCore\src\CardSets\DungeonRun\DungeonRunEncounters.F08.cs
- C:\Users\user\AppData\Local\Temp\claude\0a564ffc8743aeec\d2a4e245-ca37-4433-9b55-c35ed325f081\scratchpad\L3\repo\engine\SabberStoneCore\src\Actions\EncounterRules.F08.cs (new; the tag team's bench: `IsTagTeam`, `TagTeam`, `TagTeamBenchHealth`, the death hook)
- C:\Users\user\AppData\Local\Temp\claude\0a564ffc8743aeec\d2a4e245-ca37-4433-9b55-c35ed325f081\scratchpad\L3\repo\plugin\DungeonRun\DungeonRunAdmission.F08.cs
- C:\Users\user\AppData\Local\Temp\claude\0a564ffc8743aeec\d2a4e245-ca37-4433-9b55-c35ed325f081\scratchpad\L3\repo\data\dungeon-run\reference\encounters\F08.json (written by p8f08\make_f08.py)

=====================================================================
3. FILES CHANGED - SHARED FILES (reason each)
=====================================================================
- `engine\SabberStoneCore\src\Actions\EncounterRules.cs`, ResolveBoss, one line: `if (TagTeamDeath(boss)) return;`. An onDeath form always enters at full Health and only moves forward; the benched hero must come back with his own damage, from either side, inside the death check.
- `engine\SabberStoneCore\src\Model\Entities\Controller.cs`, hero power options, three lines: a take-control hero power is not offered on a run shrine (the filter card options already have). Without it Alluring Tune was offered on a 0-Attack shrine and then refused by Card.TargetingRequirements, which would be an AI fallback. The file's BOM is pre-existing and kept.
- `tools\restoration-ledger\curation.json`: landed-family entry E08 appended after E04 (40 ids, supportRecords LOOTA_BOSS_32h); the two tool-owned lists were rewritten by the encounters step. Expect the usual overlap with other lanes at the end of landedFamilies.
- Generated in my copy (dropped by the merge): encounters-v1.json, catalog-v1.json (byte-identical to before), dungeon-run-catalog.md, restoration-ledger.csv/.md.
- docs\design\dungeon-run.md was not edited.

=====================================================================
4. PINS MOVED IN EXISTING SUITES (my copy)
=====================================================================
| Suite | Pin | Old | New | Cause |
|---|---|---|---|---|
| c-admission | total | 2584 | 2674 | 8 rows / 23 ids |
| c-admission | "table lists only ledger rows" | - | one added statement exempting my eight family names | new families boss-public-power, boss-form, boss-both-players, boss-passive-trigger, boss-hidden-reveal, boss-both-draw, boss-control, boss-card |
| c-content | passive registry list | LOOTA_BOSS_48p | + 16p, 17p, 38p, 19p, 33p, 31p, 47p (registration order) | seven passives; count stays 904 |
| c-enc-model | A4 | 8 rows exactly | first 8 unchanged, 25 rows, the rest family 08 | 17 rows |
| c-enc-model | J13 | every entry family "01", passive list 48p | "01" or "08", passive list as above | registry |
| c-deckops | T5 ledger rows | 4579 | 4613 | 17 run:boss rows + 17 deck cards new to the ledger |
| c-catalog | ledger golden | 4579 / 2813 / 40 / 668 | 4613 / 2813 / 97 / 645 | same; 23 ids go missing -> partial |
| c-enc-secrets | S14 | Trapped Room substituted, 2 substitutions | authentic, 2 LOOTA_107 | Shroomsayer scripted |

Unchanged: c-acts A6 digest DDE3676B5A519BEE, c-deckops L1 digest 8B1B4ED89144446B.
Generators: encounters 8 rows / 12 decks (6 authentic, 6 changed) / 15 adaptations -> 25 rows / 29 decks (20 authentic, 9 changed) / 32 adaptations / 1 rejected. Catalog 506 entries, 6508 / 1, unchanged. Ledger 4613 rows: implemented 2813, partial 97, missing 645, duplicate/support 1058, adapted 0. A second run of all three reproduced all six files byte for byte.

=====================================================================
5. TESTS ADDED (paths after the merge)
=====================================================================
Folder C:\Users\user\AppData\Local\Temp\claude\0a564ffc8743aeec\d2a4e245-ca37-4433-9b55-c35ed325f081\scratchpad\p8f08 (in the lane: ...\scratchpad\L3\sp\p8f08):
- `suites\c-enc-08` (Program.cs, Enc08.csproj): 113 checks, real engine games through DungeonRun.BuildPlan on the shipped rows. Covers every power and card, the tag team in a game clone and in the boss's fair view, Join the Fray in the fair view, the rows, admission and validator.
- `run.sh`: reference | curation | fixture | build | suite <n> | bp <mode> <n> | same <mode>. Its repo and scratch roots are full literal lane paths.
- `make_f08.py`, `patch_curation.py`, `make_fixture.py` (each takes the repo root as an argument).
- `fixtures\encounters-f08.json`: two fixture rows, fix08_cards and fix08_kraxx (family "08f"), whose decks hold the five boss minions.
- Evidence: `bp-enc08-a/b.txt`, `bp-enc08-tag-a/b.txt`, `bp-enc08-cards-a/b.txt`, `bp-enc08-trapped-a/b.txt`, `bp-enc08-seeds-a/b.txt`.
The suites were not added to the shared builds.sh / runs.sh / bp.sh.

=====================================================================
6. RESULTS (final build)
=====================================================================
Builds: engine 0 warnings / 0 errors; plugin 0 errors / 75 warnings.

Suites:
- c-content 904, c-admission 2674, c-runlayer 244, c-acts 181, c-deckops 174, c-shrine-core 131, c-enc-model 141, c-enc-engine 122: all 0 failed.
- Also run because their pins moved: c-catalog 127, c-enc-secrets 60.
- c-enc-08 113 passed, 0 failed, twice, logs identical.

Bosspilot (c-bosspilot-encounter, fair AI as the boss), each mode twice and identical, stderr empty, 0 faults / 0 fallbacks / 0 failed checks / 0 swallowed leaf exceptions:
| Mode | Games | Boss wins | Notes |
|---|---|---|---|
| enc08 (FAMILY=08) | 34 | 29 | power-unused 0, form-unreached 0; Tag Team played 3 times, Gluttonous Trogg 4, Murloc Tinyfin 2 |
| enc08-tag (George and Karl, 8 games) | 8 | 7 | Tag Team played 12 times |
| enc08-cards (fixture) | 12 | 12 | from hand: Ooze 7, Deadly Spore 8, Shroomsayer 7, Gluttonous Trogg 14, Starving Crab 0 |
| enc08-trapped (Trapped Room) | 6 | 5 | Shroomsayer played 9 times |
| enc08-seeds (SEED_BASE=19000) | 34 | 25 | power-unused 1: Seriona |

Two things to know about those numbers:
- **Starving Crab is never played from hand** by the fair AI (it costs its owner 5 Health a turn). fix08_cards starts one on the boss's board, so its trigger runs in real bosspilot games, and c-enc-08 O1-O2 test it directly.
- **Seriona's power-unused on the second seed base** is a two-game sample. A separate 10-game run used Fading Light in 9 of 10 games (21 uses).

Ixlid can win on game turn 4 (Coin, Spore, attack) against the harness's simple player bot, which does not remove a 1/1.

=====================================================================
7. FOR THE INTEGRATOR TO REDO IN THE MAIN TREE
=====================================================================
- Move `TagTeamBench = 32643` into the tag table; next free then follows the other lanes.
- Regenerate encounters, catalog, ledger; then rerun the fixture steps that copy encounters-v1.json (p8f02, p8f03, p8f04, and `p8f08/run.sh fixture`).
- Family 01's bosspilot pins for Trapped Room (enc01, secret4) will move: its deck holds two Shroomsayers instead of two Mad Scientists. I did not run those modes.
- Recompute the pins of section 4 once all lanes are in (the passive list and the c-enc-model A4 / J13 literals will collide with other lanes' edits of the same lines).
- LOOTA_102 and LOOTA_104 stay "encounter_use none" in the ledger until families 09 (Tad) and 07 (The Mothergloop) list them.
- The lanes left docs\design\dungeon-run.md to you; nothing for family 08 is written there.
- Unverified in the client: the Tag Team hero swap uses EncounterRules.SwapForm (Controller.AddHeroAndPower), the same untested native transport as Icarax's forms.
- Wiki lists were read through the fetch tool's extraction of the MediaWiki wikitext, not copied raw. Every list totals 30, and every name resolved in the card data. The rounds table was read twice with the same values.
