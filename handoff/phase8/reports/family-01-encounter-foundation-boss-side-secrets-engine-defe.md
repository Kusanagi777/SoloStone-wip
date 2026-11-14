PHASE 8 FAMILY 01 (Encounter foundation + boss-side Secrets + engine deferrals) - REPORT, second part

All 13 ids are done or covered; nothing is rejected. Everything is uncommitted in C:\SoloStone (HEAD 0f6a476, nothing staged, stash empty, same set of changed/untracked paths as at my start, no directory deleted, installer/out untouched). Edited files are LF; the three files that carry a BOM in HEAD (UldumCardsGen.cs, Game.cs, SpecificTask.cs) keep it, none added. No process of mine is running. Not verified in the real client: all evidence is engine games and harnesses.

Scratchpad of this work: C:\Users\user\AppData\Local\Temp\claude\0a564ffc8743aeec\d2a4e245-ca37-4433-9b55-c35ed325f081\scratchpad\p8f01b (below: B). The first part's scratchpad p8f01 is P8.

=====================================================================
1. PER ID
=====================================================================
- DALA_BOSS_21h / DALA_BOSS_21p (Whirt): done in the first part; unchanged (old01 bosspilot mode: 0 differing lines against P8's enc01).
- LOOTA_BOSS_35h / LOOTA_BOSS_35p (Elder Brandlemar, Dampen Magic): done. Row loot_35_brandlemar, LOOT, tier 1, the wiki's 30 cards unchanged (deck kind authentic). Puts a Counterspell into play (put, not cast); not offered while Counterspell is in play or the Secret zone is full. Recorded adaptation: the wiki's "auto-cast" is an AI choice here.
- LOOTA_BOSS_48h / LOOTA_BOSS_48p / LOOTA_BOSS_48t (Trapped Room, Alarm, Sawblade): done. Row loot_48_trapped_room, LOOT, tier 2. Alarm is the first passive ORIGINAL boss power: registered with the Passive helper, in DungeonRunCardsGen.PassiveHeroPowers, started at match start. Deck substituted: Shroomsayer x2 (LOOTA_107, unscripted, family 08) -> Mad Scientist x2, recorded.
- GILA_BOSS_25h / GILA_BOSS_25p (Manhunter Ivan, Trap Preparation): done. Row gila_25_ivan, GIL, tier 2, the wiki's 30 cards unchanged (authentic), bossGeneratedSecrets on for Professor Putricide.
- ULDA_BOSS_44h / ULDA_BOSS_44p (Trap Room, Tricks and Traps): done. Row ulda_44_trap_room, ULDA, tier 3, wiki level 6 list; Commander Rhyssa (DAL_573, unscripted, family 02) -> Mad Scientist, recorded; bossGeneratedSecrets on. Recorded adaptation: "Play" is an effect cast without payment (the engine's rule for effect plays), so Secretkeeper, Putricide and the Spellstone do not react to it and the player's Counterspell does not see it. The Heroic power ULDA_BOSS_44px is not scripted (not in the id list, no row uses it).
- ULD_152 Pressure Plate: done (engine/SabberStoneCore/src/CardSets/Standard/UldumCardsGen.cs, the rule of the scripted reprint CORE_ULD_152). Ledger partial -> implemented; now a candidate of two Brann catalog entries.
- TRLA_168 Streetsmuggler (boss row): done as an admission row plus a fixture. With ICC_204 Professor Putricide it has its own secret-generated row: catalog Player | Boss | Generated, BossNeeds = generatedSecrets, new field BossSecretClass = HUNTER. No shipped boss deck lists TRLA_168 (family 13's Hunter champion will); it is played by the boss in fixture row fix_168_smuggler (Ivan's deck with two Streetsmugglers). ICC_204 has shipped carriers (Ivan, Trap Room).

Deferrals of the task list:
- Boss-side generated Secrets: done (Whirt earlier; now named-Secret deck cards). New engine rule Game.BossNamesSecrets + a branch in SpecificTask.CastRandomSecret: a boss whose row turned its generated Secrets on reads DungeonRunContent.RandomSecretCandidates instead of its boss-only list (which blocks every Secret). The validator checks one closed pool per class a needing card draws from.
- Boss own-deck ledger, removals: plugin/Npc/OwnDeckLedger.cs now drops the exact Secret that went from its owner's deck into its owner's Secret zone (was: a random unknown card; this also covers Mad Scientist). NOT done: the ten older Player-only BossDeckLedgerLift rows (TRL_409, BOT_257, GIL_694, GIL_840, TRL_500, UNG_840, DALA_727, ULDA_024, ULDA_044, TRLA_809) stay Player-only. Each needs its script to report its rewrite (EncounterRules.DeckRewritten) and, for cost/stat rewrites, a sampled deck that carries the enchantment; no family 01 deck lists any of them.
- Typhoon / InterceptLeave and the c-bosspilot label: done in the first part, re-checked present.

=====================================================================
2. A FINDING THAT CHANGES A HELPER RULE
=====================================================================
A clickable boss power must be offered only by what is exact in the boss's fair view (boards, own hand and Secrets, zone counts), never by its deck. My first version offered Trap Preparation only when the deck held a Secret; the search samples the deck, picked the power in a sample, and the real game did not offer it: BotAction.Resolve threw and the turn became a fallback (3 fallbacks in 30 games). Now both deck powers are offered whenever the Secret zone has room and do nothing with no Secret left (as the originals). Stated on the Power helper's new `usable` parameter and in docs/design/dungeon-run.md.

=====================================================================
3. FILES
=====================================================================
Engine: C:\SoloStone\engine\SabberStoneCore\src\CardSets\DungeonRun\DungeonRunEncounters.F01.cs (four powers, Sawblade, public helpers PutSecret(Controller, Card) and SecretsToPlay(Controller, bool hand)); ...\DungeonRunEncounters.cs (Power(..., Func<Controller,bool> usable = null)); ...\DungeonRunContent.cs (PlayRandomSecrets reports the roll); C:\SoloStone\engine\SabberStoneCore\src\Model\Game.cs (BossNamesSecrets); ...\Tasks\SpecificTask.cs; ...\CardSets\Standard\UldumCardsGen.cs.
Plugin: C:\SoloStone\plugin\DungeonRun\DungeonRunAdmission.cs (AdmissionRule.BossSecretClass; the four boss powers and ICC_204/TRLA_168 moved out of the core rows), DungeonRunAdmission.F01.cs (three new rows), DungeonRunValidator.cs (pool per class), C:\SoloStone\plugin\Npc\OwnDeckLedger.cs.
Data/tools/docs: C:\SoloStone\data\dungeon-run\reference\encounters\F01.json (four rows appended, earlier rows keep their order), generated encounters-v1.json (8 rows, 12 decks, 2 authentic), catalog-v1.json and docs\design\dungeon-run-catalog.md, tools\restoration-ledger\curation.json (E01 ids and note, ULD_152 catalog note removed; encounterAdaptations 19, encounterRejections 1), tools\dungeon-run-catalog\curation.json (ULD_152 exclusion note removed), docs\design\restoration-ledger.csv/.md, docs\design\dungeon-run.md (phase 8: Boss Secrets). All three generators rerun at the end: byte-identical.

=====================================================================
4. TESTS ADDED
=====================================================================
- B\suites\c-enc-secrets: 60 checks (S1-S50), real engine games through DungeonRunSetup: each power's text and behaviour, Alarm (revealed vs destroyed unrevealed, player's Secret, full board), Pressure Plate, Putricide and Streetsmuggler for a boss with the row flag on and off, the player's side unchanged, own-deck ledger after both deck powers, shipped rows, BuildPlan/Configure, admission and validator refusals.
- c-bosspilot-encounter (P8\suites; new env GAMES=n and WATCH=id,id): modes in B\bp.sh: enc01 (now 8 rows), old01, secret4 (6 games per Secret boss), secretfix (fixture B\fixtures\encounters-secretfix.json).

=====================================================================
5. RESULTS (final cycle, after the last source change)
=====================================================================
Builds: engine 0 warnings / 0 errors (dll identical on rebuild); plugin per docs/BUILDING.md 0 errors / 75 warnings (same warning set); 51 suite builds + randplay OK.
Suites, run twice, run 1 = run 2 (c-secretsearch but for its timing lines, as before): c-runlayer 244, c-catalog 127, c-content 904, c-admission 2244, c-acts 181, c-deferrals 149, c-deckops 174, c-dkdh-supply 13, c-dkdh-play 5, c-heroes 347, F14 316, F6 566, F9 292, c-runctx 40, c-starters-F10 239, c-upgrades 34, treasures F1 229 F2 321 F3 283 F4 236 F5 404 F7 367 F8 256 F11 380 F12 321 F13 248, c-review-F18 35, c-shrine-core 131, S1 107 S2 157 S3 158, c-shrine-upgrades 92, T1 139 T2 207, companions-D 251, c-p7fix 40, c-enc-model 141, c-enc-engine 122, c-enc-secrets 60 (new), c-secretsearch 689, c-secretscan 0 faults, selfplay digest 2CE546A18F96DCBC (unchanged, game file identical to P8's). Hall: 39 harnesses, 955 tests / 8285 checks.
Bosspilot, each mode twice identical, 0 faults / 0 fallbacks / 0 swallowed everywhere:
- run, fixture: 0 differing lines against P8.
- catalog 10, act 8, shrine 14, S2 10, S3 2, U 8, T1 16, T2 14, D 12 differing lines against P8 (attributed below).
- enc01: 20 games over the 8 rows, power-unused 0, form-unreached 1 (Icarax at 0 carried, as before). old01, rules01 x3, plague x5, relay: 0 differing lines against P8. finale: 4 differing lines (attributed below).
- secret4: 24 games, boss wins 18. secretfix: 6 games, the boss played TRLA_168 3 times and ICC_204 once, held up to 4 Secrets.
- seeds: 6 bases x 20 games, 0 faults / fallbacks / failed checks.

Baseline moves, each attributed by experiment (logs in B\attr and B\attr-*.txt):
- One cause for all game-outcome moves: Pressure Plate is scripted, so it joins catalog-run generation pools (Hunter Secrets, random spells) and two Brann catalog entries. Rerun with the pre-change catalog and an engine lacking only the ULD_152 script (B\eng-attr): 0 differing lines against P8 for catalog, act, the seven shrine modes, finale, c-heroes-F14, treasures F1/F7/F8/F11/F12/F13, c-shrines-S1, c-teammates-T2, c-companions-D, c-deferrals; randplay returns to D2D7494D2AD50707. So the OwnDeckLedger change and everything else move nothing.
- New values: randplay digest 67AD6266DC530421 (regkeys 2150, exit 127 as before); c-acts A6 digest 9F91C1FE52A4B998 and c-deckops L1 digest 5169BABFF3951AA2 (both return to the P8 values with the old catalog alone).
- c-admission 2237 -> 2244: the three F01 rows.
- Ledger: 4567 -> 4579 rows (4 run:boss rows, 8 basic cards of Brandlemar's deck); implemented 2722, partial 39, missing 760, duplicate/support 1058, intentionally adapted 0. The four boss hero rows stay missing and the powers/Sawblade partial with blocker run:default-schedule (the default schedule is still acts-v2).

Expectations edited in older suites (originals in B\before\harness):
- c-admission: three secret-generated checks (third row; ICC_204/TRLA_168 boss-admitted), the Hunter pool check (ULD_152 now in it).
- c-catalog: ULD_152 admitted instead of excluded; the stale-catalog probe uses BOT_914; counts 6318 / 191 / 91; ledger golden 4579 rows; DALA_BOSS_21p row.
- c-content: passive registry = passive content + encounter passives. c-enc-model: A4 (8 rows), J13, and G4/J3 now put a custom boss on floor 1 explicitly (mix-1 can draw Brandlemar).
- c-teammates-T1 A3 (TRLA_168 boss-admitted). c-deckops T5 (4579 rows).
- c-treasures-F8 T11: I replaced one conjunct, which weakens it. It bounded how many non-Scroll cards had left the deck; with Pressure Plate in the random-spell pool the seeded casts take one more card. It now asserts that no non-Scroll card was added; the ten-Scrolls conditions are unchanged. The old check passes again when ULD_152 is blocked from generation (B\attr\c-treasures-F8).
- Two of these were already failing against the first part's own final ledger (c-catalog ledger golden and c-deckops T5 still pinned 4555 rows; the ledger was regenerated after those suites last ran).

=====================================================================
6. FOR THE NEXT FAMILIES
=====================================================================
- Use B\builds.sh, B\runs.sh, B\bp.sh, B\hall.sh, B\same.sh (tag p8f01b; bp.sh compares against P8). New baseline = section 5.
- Never leave a second output folder holding SabberStoneCore.dll inside a suite's project folder (I built a debug copy to out-debug): MSBuild resolves the engine reference from it before the HintPath and silently ships a stale engine into out. I removed that dll; check md5 of out\SabberStoneCore.dll against audit-verify\engine-out after a build if results look impossible.
- The harness budget has wall-clock limits: never run two harnesses at once.
- Rerun the encounters step when LOOTA_107 (family 08) and DAL_573 (family 02) get scripts: Trapped Room's and Trap Room's decks then become authentic. Every further collectible Secret that gets scripted will move the same digests as Pressure Plate did.
- A deck card that generates a Secret for a boss needs a row with BossNeeds (and BossSecretClass when it names a class) and bossGeneratedSecrets on the encounter row; DAL_141 and the Discover generators are still Player-only.
- Still open from the first part and untouched here: default schedule not flipped, finales disabled, no twist scripted, relay without a hero choice, DeckTopKnown unconsumed, c-nativeforms not written.
