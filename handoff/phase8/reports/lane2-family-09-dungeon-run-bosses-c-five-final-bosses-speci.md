PHASE 8 FAMILY 09 (Dungeon Run bosses C: five final bosses, special rooms, special starts) - LANE 2 REPORT

All 37 ids are done except LOOTA_BOSS_53h2, which stays a portrait support record (reason below). Nothing is rejected to phase 9. Everything is uncommitted in the lane copy; nothing was written outside L2. All files I wrote are LF without BOM (48 files checked). No process of mine is running. Nothing was checked in the real client: all evidence is engine games and harnesses.

FLAGS FOR THE INTEGRATOR (read first)
1. Private tag: only my reserved 32644 (EncounterRules.RoomRules, in the tag table of EncounterRules.cs). No other tag taken.
2. No schedule default flipped, no finale enabled, no act or pool set changed, no save version bump, no ReleasedShapes or golden re-pin, no new save field.
3. Possible double registration with a parallel lane: F09 registers the adventure cards KARA_00_05, KARA_00_05e, KARA_00_07, KARA_00_08 (Inara's deck; family 18's notes mention KARA_00_08), BRMA12_8t and BRMA12_8te (Vustrasz's deck). If another lane registers one of them, the registry throws at start-up: keep one.
4. Engine pitfall found and fixed: a static field initializer in a DungeonRunEncounters partial file must not construct a task that reads the card database (AddEnchantmentTask, SummonTask by id). The card database registers the class's entries while it loads; with such an initializer every encounter card of ALL families came out unscripted (Implemented false, Power null) in harnesses that touch DungeonRunEncounters before Cards, with no exception. F09's ingredient table is built on first use. Other lanes should check their files for the same pattern.
5. Shared-file edits (each minimal, listed in section 3): flag vocabulary "listedDeck", the tool's curated kind, RecordOutcome's no-loss rule, two one-line engine guards (Draw.cs, PlayLockRules.cs), two core admission rows.
6. Four powers are cast by rule and registered with the passive powers (Dispel Ward, Devour, Stolen Time, Doors Are Closing): the fair AI never used Devour, Stolen Time or Dispel Ward in 5 to 9 games each when they were options, so the wiki's "auto-cast" is a turn-start trigger. This departs from families 07/08 ("auto-cast is an AI choice"); recorded per row as an adaptation.
7. Balance, not fixed: the five final bosses as act I majors won 20 of 20 games against the harness's local bot (enc09-finals).
8. Ledger: LOOTA_103 and LOOTA_BOSS_52t keep blocker run:deck-mutation from the ledger tool's text-phrase rule although their run deck edits are implemented (RunHelpers.EditAdd, c-enc-09 N1-N5). I did not edit tools/restoration-ledger/Program.cs.

1. PER ID
Done (script, row, admission, tests):
- LOOTA_BOSS_20h / 20p / 20t Brimstone Warden: row loot_20_brimstone, tier 2, wiki 30 cards unchanged; start block 4 Stone Golems, start bossMana 0. Dispel Ward cast by rule at 5 Mana. The engine's Silence does not lift a card's own Can't attack tag, so Dispel Ward clears it on the minions it silences (general engine gap, not fixed in Minion.Silence).
- LOOTA_BOSS_22h / 22p / 22t / LOOTA_103 Tad: row loot_22_tad, tier 3, 20 fixed Health, the wiki's ten minions as a 10-card deck (flag listedDeck), bossMana 0. No hand, no draw, no Fatigue (room rules). Catch and Release pulls the minion whose Cost equals his Mana Crystals (the wiki's order), else a random deck minion. Sunken Chest on his 11th turn if undamaged; its death adds Tad's Pole by a run deck edit and its owner loses. Tad's Pole scripted for either side.
- LOOTA_BOSS_25h / 25p Vustrasz: row loot_25_vustrasz, tier major1, healthOffset 10, wiki 30 cards unchanged; start block 5 Master Chests (LOOT_357l, already scripted). Chromatic Dragonkin BRMA12_8t scripted.
- LOOTA_BOSS_26h / 26p8 / 26p2..26p7 Xol: row loot_26_xol, major1, wiki 30 cards unchanged; passive Beam Me Up! and the six Beams (Confusion never takes a run shrine; Fear destroys a shrine instead of shuffling it).
- LOOTA_BOSS_34h / 34p Azari: row loot_34_azari, major1, wiki 30 cards unchanged. Devour by rule each turn: top two cards to the run player's public graveyard (Gnomeferatu's rule); the fair view keeps the rest of the deck and the hand masked (c-enc-09 E3).
- LOOTA_BOSS_49h / 49p / 49t / 49t2 The Darkness: row loot_49_darkness, major1, wiki 30 cards unchanged. Three Luminous Candles go to the run player's HAND (start playerHandAdd), as both wiki pages state; the brief said "shuffled into the player's deck": I followed the wiki. They never touch the run deck.
- LOOTA_BOSS_50h / 50p / 50t Mushhuckster Max: row loot_50_max, tier 2, wiki 30 cards unchanged. Option table source: the Dungeon Run page ("These potions are 1-Mana Kazakus Potions") and Max's page (special card: Kazakus Potion). Craft = two Discovers among the nine 1-Cost Kazakus effects; the potion is LOOTA_BOSS_50t with its two ingredients as entity tags (MODULAR_ENTITY_PART_1/2), targeted only with Heart of Fire. Eater of Secrets OG_254 in his deck got a boss-only admission row (it was "unclassified").
- LOOTA_BOSS_52h / 52p / 52t Treasure Vault: row loot_52_vault, tier 3, 60 fixed Health, 7 Coffers to start, bossMana 0, flags listedDeck and noLoss. The wiki's "no deck" is a curated deck of ten Coffers that is never drawn (kind curated). Doors Are Closing closes at the start of the Vault's turn with 5 Mana Crystals: EncounterRules.EndWithNoLoss marks the game and destroys the Vault's hero, so the table shows the run player's win; the run records Win with NoLoss and no kill credit. Coffer: random treasure of RunHelpers.TreasurePool by RunHelpers.EditAdd.
- LOOTA_BOSS_53h / 53p Chronomancer Inara: row loot_53_inara, tier 3, wiki 30 cards unchanged. Stolen Time by rule at 10 Mana (pays 10, destroys 3 crystals, extra turn at 8 Mana). KARA_00_05/05e/07/08 scripted.
- LOOTA_BOSS_54h / 54p A. F. Kay: row loot_54_kay, tier 2; deck substituted: Fel Reaver x2 (GVG_016, unscripted, not in my id list) -> Bittertide Hydra. She plays no card from hand before her 6th turn (room rule); Idle does nothing.
- LOOTA_BOSS_99h / 99p / 99t King Togwaggle: row loot_99_togwaggle, major1, wiki 30 cards unchanged. Magic Candle adds one of the wiki's sixteen Treasures at random. Rakanishu: the wiki says "occasionally ... at the start of his turn" with no frequency; scripted as 1 in 4 (adaptation). The client card has a hide-stats tag, so it cannot be played from a hand; the power casts it.
Support record, not scripted: LOOTA_BOSS_53h2 Inara the Mage. The wiki says she wears this portrait during her extra turn; a portrait-only hero swap would need a script form that preserves damage and the used power. Recorded as an adaptation on the row and as supportRecords of E09. Not phase 9 work; cosmetic.

2. OWN FAMILY FILES
- C:\Users\user\AppData\Local\Temp\claude\0a564ffc8743aeec\d2a4e245-ca37-4433-9b55-c35ed325f081\scratchpad\L2\repo\engine\SabberStoneCore\src\CardSets\DungeonRun\DungeonRunEncounters.F09.cs (31 registrations; helpers AutoCast, AutoCasts, Room, CatchAndRelease, PoleCatch, CofferTreasure, GivePotion, PotionParts, MissingChests)
- ...\repo\engine\SabberStoneCore\src\Actions\EncounterRules.F09.cs (new: RoomEntered / RoomNoDraw / RoomNoPlay, NeverDraws, PlaysNoCards, EnterRoom, SetRoomRule, ReturnHandToDeck, EndWithNoLoss)
- ...\repo\plugin\DungeonRun\DungeonRunAdmission.F09.cs (14 rows)
- ...\repo\data\dungeon-run\reference\encounters\F09.json (written by p8f09\make_f09.py)

3. SHARED FILES (reason per edit)
- engine\...\Actions\EncounterRules.cs: one constant RoomRules = 32644 in the tag table.
- engine\...\Actions\Draw.cs: one guard in DrawBlock (a controller with RoomNoDraw draws nothing and takes no Fatigue). Tad and the Vault cannot be done without it.
- engine\...\Actions\PlayLockRules.cs: BlocksPlay also asks EncounterRules.PlaysNoCards (Kay's idle turns; Tad and the Vault).
- plugin\DungeonRun\DungeonRunData.cs: flag "listedDeck" in the vocabulary, const ListedDeck, helper ThirtyCardDeck(tier, flags).
- plugin\DungeonRun\DungeonRunValidator.cs: one line (combat = ThirtyCardDeck(...)), so a listedDeck row keeps 10 to 60 cards.
- tools\dungeon-run-catalog\Encounters.cs: the same rule, and a deck spec "kind": "curated" is recorded as curated (the tool had no curated path).
- plugin\DungeonRun\DungeonRun.cs: RecordOutcome treats a harvest with NoLoss as a no-loss win also when the reported outcome is Win (one condition removed; c-enc-model I13 still passes).
- plugin\DungeonRun\DungeonRunAdmission.cs: 14 treasure ids moved out of the core run-treasure row into F09's run-treasure-boss-find row; LOOTA_839's row gets CatalogAdmit Player|Boss. Both keep Admit = Player, so the legacy verdict, the catalog (byte-identical to main) and the generation lists for those ids are unchanged.
- tools\restoration-ledger\curation.json: landedFamilies entry E09 (p8f09\patch_curation.py, 36 ids, supportRecords LOOTA_BOSS_53h2); the two tool-owned lists regenerated.
- Generated, regenerated in my copy only: encounters-v1.json, catalog-v1.json, dungeon-run-catalog.md, restoration-ledger.csv/.md.

4. PINS MOVED (in my lane copies of existing suites)
- c-content passive registry list: + LOOTA_BOSS_20p, 26p8, 34p, 52p, 53p. Still 904.
- c-admission: exemption list of family names (+ 8 F09 families); the "only three rules carry a catalog lift" check now names five. Total 2854 -> 2989; NOTE generated lists 351 -> 379 both sides, boss-only 310 -> 311 (Tad's Pole, which must not be NeverGenerated or the run's deck checks refuse the edit).
- c-enc-model: A4 family counts "01:5,07:18,08:17,09:11,13:1,23:1,24:1"; A9 (a curated deck is not authentic); E3 (the act I major may be one of the five finals, pool enc-major1); J13 passive list. Still 141.
- c-deckops T5 and c-catalog ledger golden: 4644 -> 4670 rows; partial 165 -> 216; missing 608 -> 583; implemented 2813 unchanged. (+11 run:boss rows, +15 deck cards.)
- p6f15\suites\c-treasures-F2 T2 admission: the fifteen treasures Togwaggle finds are boss-admitted on a catalog run. Still 321.
- Unchanged: c-acts A6 DDE3676B5A519BEE, c-runlayer, c-shrine-core, c-enc-engine (0 differing lines against main's p8w2 logs).

5. TESTS ADDED (paths after the merge, under the shared scratchpad's p8f09)
- p8f09\suites\c-enc-09: 166 checks in real engine games through BuildPlan / Configure / ApplyAfterStart (sections A Brimstone, B Tad, C Vustrasz, D Xol, E Azari, F Darkness, G Max, H Vault, I Inara, J Kay, K Togwaggle, L sixteen treasures played by a boss, N run layer, M rows / admission / validator).
- p8f09\suites\c-bosspilot-enc09: the shared encounter bosspilot plus " auto N" / " no-loss" per game, AUTO=ids and a check that the match summary's no-loss mark equals the harvest's (built by p8f09\make_harness.py; the shared harness is untouched).
- p8f09\fixtures\encounters-f09.json (p8f09\make_fixture.py): fix09_treasures, fix09_beams, fix09_chests, fix09_medivh.
- p8f09\run.sh (reference, curation, fixture, harness, build, suite, bp <mode> <n>, same); evidence bp-<mode>-a/b.txt; p8f09\dungeon-run-family-09.md is text for docs\design\dungeon-run.md.

6. RESULTS (final engine build; engine 0 warnings / 0 errors; plugin 0 errors / 75 warnings)
- Generators run twice: 7 files byte-identical. encounters: 54 rows, 58 decks (41 authentic, 17 changed), 86 adaptations, 1 rejected. Family 09 decks: 9 authentic, Kay substituted, Vault curated. Ledger 4670 rows.
- Suites, each run twice, run 1 = run 2: c-content 904, c-admission 2989, c-runlayer 244, c-acts 181, c-deckops 174, c-shrine-core 131, c-enc-model 141, c-enc-engine 122, c-enc-09 166. Run once: c-catalog 127, c-treasures-F2 321.
- Bosspilot, each mode twice identical, 0 faults / 0 fallbacks / 0 failed checks / 0 swallowed, stderr empty:
  - enc09 (shared harness, FAMILY=09): 22 games, boss wins 14, power-unused 0.
  - enc09-seeds (shared harness, SEED_BASE=19000): 22 games, boss wins 17, power-unused 1 = Vustrasz: no Master Chest died in those two games, so Vindictive Breath was never offered (used 2 to 7 times per game in enc09, enc09-finals and the fix09_chests fixture).
  - enc09-auto: 16 games, auto-unused 0 (Brimstone 1 cast in 3 of 4 games, Azari 5-8 per game, Inara 1-2, Vault closes in all 4: no-loss 4).
  - enc09-finals: 20 games, boss wins 20. enc09-rooms: 16 games, boss wins 7, no-loss 4.
  - enc09-cards (fixtures): 24 games. The AI played from hand every Beam, Medivh's three cards, Chromatic Dragonkin, Stone Golem, Mushroom Potion (19 in enc09) and 14 of the 16 treasures across enc09-finals and enc09-cards.
- Not played by the AI in any bosspilot run (covered by c-enc-09 only): THE CANDLE (L14), Mask of Mimicry (L15), Eater of Secrets (G17), Tad's Pole (B15-B19, a player reward).
- Not run, per lane rules: hall, full runs.sh, released bosspilot modes, selfplay, randplay.

7. FOR THE INTEGRATOR TO REDO IN THE MAIN TREE
- Rerun the encounters, catalog and ledger steps; recompute c-deckops T5, c-catalog golden, c-admission total, c-enc-model A4/J13, c-content passive list after all lanes merge.
- randplay's regkeys line will move (31 new registered ids). I expect no selfplay or randplay game digest move (the new guards only read a tag; the new cards are not collectible), but this was not run.
- Other suites that name "run-treasure" rows (c-treasures-F1/F3..F13, c-heroes-F9) were not run in the lane; their ids are not the fifteen moved ones.
- Bosspilot seeds depend on the boss's row index, so the lines of my evidence move when other lanes add rows before family 09.
- Known limit: the run layer applies a fight's deck edits all or none within deckMax (50); a Vault that would push the deck past it loses all its Coffer treasures.
