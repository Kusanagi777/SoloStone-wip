FAMILY 13 REPORT: Rumble Run opponents and boss shrines. Worktree /home/user/wt/f13, branch wip/f13, one commit 2f0b228. The working tree is clean, nothing was pushed, and nothing is still running.

## FLAGS for the integrator (read first)
1. **No extra tag.** Reserved tag 32648 is unused. No new engine rule file and no private tags.
2. **Shared edit, `plugin/FairBotState.cs` (AI core).** I added two null guards for a chained Discover whose second choice has not been dealt yet (its `Choices` is null). Griftah (TRL_096), in Malacrass's authentic early deck, made `FairBotState.CreateCore` throw `ArgumentNullException`, which showed up as 2 bosspilot fallbacks. The guard only changes paths that used to throw. Regression test: c-enc-13 F1-F3. F2 failed with the old FairBotState and passes now.
3. **Same hazard left in `plugin/NativeMatch.cs:6634`.** `next.Choices.Contains(id)` would hit the same null for the human player's chained Discover. I left it: the plugin cannot be built here. It needs the Windows plugin build and a check.
4. **Shared edit, `plugin/DungeonRun/DungeonRunAdmission.cs`.** I removed the three phase-7 rows that became empty: board-shrine, hidden-steal (TRLA_186 only) and shrine-token. Their ids now sit in the F13 rows, so no id is in two rows. A comment marks the spot.
5. **Shared edit, `tools/restoration-ledger/curation.json`.** Landed-family entry E13 appended after E10. The tool-owned lists were regenerated. Stale text left in place: F14's `rejected` note for TRLA_065p ("waits unscripted"). The tool ignores it now that the card is scripted; the close-out may delete it.
6. **Registration (expect textual conflicts).**
   - `verify/lib/suites.sh`: p8f13/c-enc-13 appended to BUILD_LIST and RUN_LIST.
   - `verify/bin/bp.sh`: the A13/B13/CARDS13 variables, a usage line, the enc13a/enc13b/enc13-seeds case lines, and the usage-print range in the `*)` case changed from `2,29p` to `2,30p`.
7. **Family fixtures regenerated** because encounters-v1.json changed: p8f02, p8f03, p8f04, p8f08, p8f09 and p8f10. p8f05 and p8f06 came out unchanged.
8. **The brief's starting point was out of date.** `RunMatchPlan.BossShrine` and `ShrineRules.Arm` for the boss already existed from family 01. Voone stays in F01.json (row order and seeds unchanged); it is filed under family 13.
9. **Untouched:** schedule defaults, finales, acts, pool sets, save version, ReleasedShapes and golden sequences. Also EncounterRules*, DungeonRunEncounters.cs, DungeonRunData.cs, DungeonRunValidator.cs, DungeonRun.cs and DungeonRunState.cs.

## Per id (all done; none rejected)
- **TRLA_201h-208h:** eight new rows in F13.json; boss heroes need no script.
  - Zentimo, tier 1 (round 1); Captain Hooktusk, tier 1 (round 2); High Priest Thekal, tier 2 (round 4).
  - Zul'jin, tier 3 (round 5); Wardruid Loti, tier 3 (round 6).
  - High Priestess Jeklik, tier 4 (round 7); Hex Lord Malacrass and Princess Talanji, tier 4 (round 8).
  - **TRLA_200h** Voone is unchanged.
  - Each row has variants early / middle / late, one per challenge range (1-2, 3-5, 6-8), each with its class shrine and its own 30-card deck.
- **Tiers are a recorded spread, not a wiki fact:** a champion can appear in any Rumble Run round. Each row carries this as an adaptation.
- **Extra flags, so anomaly incompatibility works:** Zentimo doubleBattlecry, Talanji doubleDeathrattle, Hooktusk hiddenZone and recast, Thekal recast, Zul'jin secrets. Zul'jin's row also turns on the boss's generated Secrets (`bossGeneratedSecrets`).
- **TRLA_065p Steady Throw:** scripted in `DungeonRunEncounters.F13.cs` as Steady Shot is: 2 damage to the enemy hero, no target. Admission row `boss-power-public`, boss only.
- **The 24 shrines** TRLA_105, 106, 108, 109, 112-116, 128, 129, 137, 138, 146, 147, 154, 155, 162, 163, 178, 179, 186 and 187: lifted for the boss in `DungeonRunAdmission.F13.cs`, each with a boss-side test in c-enc-13 (S105-S187, L1-L4).
  - **TRLA_110:** Player on any run; Player and Boss on a catalog run, needing the boss's generated Hunter Secrets.
  - **TRLA_186:** row `boss-hidden-steal`. The boss knows exactly the stolen card; the player's other deck cards stay unknown slots in its fair view.
  - **TRLA_146 and TRLA_178:** go through the boss's generation filter (`ExcludedGeneratedBoss`).
  - **TRLA_137, 147 and 179:** redirect hero damage, tested against the run player's hero.
  - The three Warrior shrines were already boss-admitted by family 01.
- **TRLA_129s Frostfire:** row `boss-shrine-token`, Player and Boss; listed in Malacrass's `cards`.
- **TRLA_107 Tribal Shrine:** stays the test fixture (support record, `supportRecords` of E13); admitted for no side, in no row.

**Decks.** Sources are hearthstone.wiki.gg `<Name>_(boss)` pages, read 2026-11-14; the URL is on each row. My transcription of Voone's page matches family 01's.
- Challenge 1-2 tables are card pools without counts; they are extended to 30 cards.
- The Rumble Run teammates the champions play are not admitted for a boss and are left out: 29 listed slots in all, each recorded. Streetsmuggler (TRLA_168) stays in Zul'jin's late deck.
- Other cards left out: Krag'wa (TRL_345) and Mosh'Ogg Announcer (TRL_532), which are unscripted; Prince Keleseth, Secret Plan and Gral, which the table keeps from a boss.

## Files
**Own files:**
- `engine/SabberStoneCore/src/CardSets/DungeonRun/DungeonRunEncounters.F13.cs`
- `plugin/DungeonRun/DungeonRunAdmission.F13.cs`
- `data/dungeon-run/reference/encounters/F13.json`
- `verify/suites/p8f13/c-enc-13/{Enc13.csproj,Program.cs}`

**Shared:** see flags 2, 4, 5 and 6.

**Generated (regenerated, not hand-edited):** encounters-v1.json, curation.json lists, restoration-ledger.csv and restoration-ledger.md. catalog-v1.json and dungeon-run-catalog.md are byte-identical to before.

## Pins moved (suite, old -> new, cause)
- **c-admission:** 3167 -> 3173 passed; generated lists 426 -> 427 (TRLA_065p's row).
  - Checks updated: the TRLA_110 family in the Secret rows; catalog-lift rows 5 -> 6 (TRLA_110's row); the TRLA_110 check; a family-13 exemption line for its two new families.
- **c-catalog ledger golden:** 4704/2813/295/538 -> 4719/2813/311/537. That is 8 run:boss rows and 7 deck cards added, and TRLA_065p moving from missing to partial.
- **c-deckops T5:** 4704 -> 4719.
- **c-enc-model** (still 141):
  - A4: "13:1" -> "13:9".
  - B12: now uses TRLA_107 ("admission board-shrine-form").
  - E3 rewritten: Voone is met in 1000 -> 559 runs; the check is now "a Rumble Run row in 1000". Hagatha is the act II major in 506 -> 601 runs.
  - G10: looks for an act without a shrine row (seed 4001, act 2).
  - J9 rewritten.
  - G1 rolls different anomalies (detail line only).
- **c-shrine-core D15**, **c-shrines-S1 A1**, **S2 A1/A4** and **S3 A1/A4**, **c-teammates-T2 A4:** family names and boss admission updated.
- **c-rules-05 T16:** threshold `> 20` -> `>= 15`. DALA_833 is rolled 20 times and DALA_831 25 times (was 142 and 148), because nine shrine rows exclude them from more acts. T17 now counts 769 acts with a shrine boss (was 276).
- **randplay (not run):** expect regkeys +1 for the TRLA_065p registration.

## Generators
- Encounters: "77 rows, 97 decks (43 authentic, 54 changed), 189 adaptations, 1 rejected".
- Catalog: "506 entries (452 offerable pool, 2 withdrawn, 52 fixed), 6508 admitted and 1 excluded" (unchanged).
- Ledger: rows 4719, implemented 2813, partial 311, missing 537, duplicate/support 1058, adapted 0.

## Tests added and registered
- **c-enc-13: RESULT 111 passed, 0 failed.** It covers rows and admission (A1-A15), the variant roll (V1-V2), Steady Throw (Z1-Z4), each shrine on the boss's side (S105-S187), lifecycle and guards for all 24 (L1-L6), shrines on both sides (X1-X4) and the Griftah fair view (F1-F3).
- **Bosspilot modes**, each run twice in one call, identical, stderr empty, 0 faults / 0 fallbacks / 0 failed checks / 0 swallowed, power-unused 0:

| Mode | Games | Boss wins | Hero power uses | Boss played Frostfire | Boss played Streetsmuggler | Most boss Secrets | Fights vs a Rikkar shrine row |
|---|---|---|---|---|---|---|---|
| enc13a (Voone to Zul'jin) | 30 | 28 | 84 | 0 | 0 | 5 | 6 |
| enc13b (Loti to Talanji) | 24 | 20 | 149 | 17 | 0 | 2 | 4 |
| enc13-seeds (FAMILY=13, seed base 19000) | 27 | 22 | 97 | 4 | 0 | 3 | 4 |

## Lane verification
- Engine: 0 warnings, 0 errors (md5 5858e485f298cdd68e60f2cda1a3ec39).
- gen.sh run twice: all six files byte-identical.
- Suites, each run twice with run 1 identical to run 2: c-content 904, c-admission 3173, c-runlayer 244, c-acts 181, c-deckops 174, c-catalog 127, c-shrine-core 131, c-enc-model 141, c-enc-engine 122, c-enc-13 111, S1 107, S2 157, S3 158, T2 207, c-rules-05 170, c-p7fix 40.
- Run once only, all 0 failed: c-enc-secrets 60, c-enc-07 161, c-enc-08 113, c-enc-09 166, c-enc-10 116, c-rules-06 224, c-collectibles A/B/C 115/108/102 (with the regenerated fixtures), c-w1fix 23, c-w3fix 4, c-shrine-upgrades 92, T1 139, D 251, c-deferrals 149.
- Differences vs the baseline logs that are not from this family: c-p7fix H1 shows `·` where the baseline has a mangled byte, and c-enc-model F2 save lengths differ (Windows vs Linux line ends).

## Open issues
- **Random generation can yield unscripted cards.** The engine's random pools (`Cards.AllWild`) include unscripted cards, and the generation filter only drops ids that admission rejects. So Bwonsamdi's Tome, Dark Reliquary (either side) and random generation in general can produce a card with no script; seen: DRG_301, DRG_246, DRG_402. This predates family 13, and the phase-7 TRLA_178 comment saying "no unscripted card" is wrong. Suggest close-out or phase 9.
- Teammates could be admitted for a boss later, which would make the champions' decks more authentic.
- Other families' bosspilot lines that walk acts-v3h will move with the eight new rows (run histories and anomaly rolls), as they did in waves 2 and 3.

## Paragraph for docs/design/dungeon-run.md
"Family 13 (Rumble Run opponents and boss shrines): the eight Rumble Run champions beside War Master Voone (Zentimo, Captain Hooktusk, High Priest Thekal, Zul'jin, Wardruid Loti, High Priestess Jeklik, Hex Lord Malacrass, Princess Talanji), each a row whose three variants (early, middle, late = challenges 1-2, 3-5, 6-8) are its class's three board shrines with that range's wiki deck. The fight's variant is drawn from the run seed and the floor; the shrine is armed for the boss by ShrineRules.Arm exactly as the run player's, and every shrine script reads its own controller, so the phase-7 scripts serve both sides unchanged. All 27 shrines are now admitted for a boss: Halazzi's Trap needs the boss's generated Secrets (Zul'jin's row turns them on), Treasure from Below's steal teaches the boss exactly the card it took and nothing else, and Bwonsamdi's Tome and Dark Reliquary generate through the boss's filter. Zul'jin's Steady Throw is scripted as Steady Shot; the other champions use basic powers. Tiers are a recorded spread (a champion meets the run in any round). The champions' teammate cards are not admitted for a boss and are left out of their decks, each recorded. The boss's fair view now tolerates a chained Discover that is not yet dealt (Griftah)."