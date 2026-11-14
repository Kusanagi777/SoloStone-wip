PHASE 8 FAMILY 05 (global-rule framework + Dalaran Heist anomalies and twists) - LANE 1 REPORT

FLAGS FIRST
1. The four twists are scripted, admitted and listed as twist rules, but NO shipped act names them: the twist pools of acts-v3h / acts-v3t are still empty (I may not change acts). The close-out must fill `"twist": {"pool": [...], "chance": n}` (pattern: p8f05\fixtures\schedule-twists.json). Twist pools are not part of Shape(), so ReleasedShapes[3] does not move, but NewRun then rolls twists, which moves c-enc-model's golden-sequences "rules N" lines for acts-v3h/v3t. Until then the ledger shows the twists `partial`, blocked by run:global-rules.
2. DALA_857 Glittering is REJECTED (reason below). Everything else in the id list is done.
3. A fault in a card that is NOT mine, found by my bosspilot run and left unfixed (another family's card, shared file): GVG_054 Ogre Warmaul (family 02, already in main). Equipped before the opponent plays TRL_400 Splitting Image, a redirect to the hero makes SummonCopyTask cast a Hero to Minion: 13 of 24 hero attacks throw InvalidCastException (c-rules-05 prints this as `NOTE J7`, not a check). Fix: `FastExecution = true` on GVG_054's ATTACK trigger in GvgCardsGen.cs (what I did for DALA_861); the NOTE then reads 0. Family 14's DALA_BOSS_14t needs the same.
4. Tags: I used 32660 (RuleApplied) and 32661 (RuleSpells), both inside my range 32660-32679 and declared in the table of EncounterRules.cs. 32640 is unused. Next free rule tag: 32662.
5. No schedule default flipped, no finale enabled, no act or pool set changed, no save-version bump, no ReleasedShapes or golden re-pin, nothing downloaded, nothing committed. Nothing was checked in the real client (engine games and harnesses only).
6. I could not delete a debug folder I created (directories are never deleted): L1\sp\p8f05-dbg\c-bp-debug holds only obj/out build output (never merged); its sources and logs are removed.

=====================================================================
1. PER ID
=====================================================================
Done (script + schedule row + admission row + c-rules-05 section + rules05 bosspilot, fair AI as the boss):
- DALA_851 Summons: after a player plays a spell, a random scripted minion of the Cost the spell had in hand, through the generation filter.
- DALA_852 Crying / DALA_853 Rattling: Battle Totem's / Totem of the Dead's controller aura on each player (shared helpers BattlecriesTwice / DeathrattlesTwice).
- DALA_854 Nesting: a played minion gains DALA_854e2; when it dies a fresh 1/1 of the same card (DALA_854e3) takes its place.
- DALA_855 Refreshing: at the end of a player's turn that player draws up to 5 (Jeeves's count; Fatigue from an empty deck).
- DALA_856 Wild Magic: after a played spell, a copy is cast with random targets (RecastRandom); no copy of a copy, a Secret, a Quest or a countered spell.
- DALA_858 Fishy: 1 chance in 4 at each player's turn start, a random scripted Murloc for that player (the chance is an adaptation: the wiki gives no number).
- DALA_859 Gorged: hooks only; +2 opening cards before the mulligan (engine cap 5) and +2 Mana Crystals, both players.
- DALA_860 Dragon Soul: the third spell a player casts in a turn summons LOOT_209t Dragon Spirit (the wiki's related card); the count restarts.
- DALA_861 Ogreish: WrongTarget50 for every attacking minion, with FastExecution (see flag 3: without it the run faulted once, on Whirt's Splitting Image; fixed and covered by check J6).
- DALA_862 Reductive: hand costs (1) less at the owner's turn end (BRM_028e; Echo / repeatable cards never below (1)).
- DALA_863 Hallucinating: hooks only; ten UNG_856 shuffled into each deck after both mulligans, each owner's deck recorded with EncounterRules.DeckRewritten.
- DALA_865 Explosive, DALA_867 Infused: after-summon triggers (DALA_865e2 Deathrattle; one of Taunt / Divine Shield / Rush / Windfury).
- DALA_829 The Coffers + DALA_829t: the boss starts with a 0/3 Cache of Cash (Deathrattle: two Coins each).
- DALA_831 The Stench: Attack and Health swapped once as a minion enters play (DALA_831e2); a 0-Attack minion dies.
- DALA_832 The Prisons + DALA_832t: each player starts with a Violet Prison holding a random scripted minion; the minion (ALTERNATE_MOUSE_OVER_CARD) and the turns left (SCORE_VALUE_1) are public tags; released at the start of its owner's turn number Cost by a transform in place.
- DALA_833 The Carts + DALA_833t, t2, t3, t4: three different Carts per player, untouchable permanents (the original's own board limit; no MaxBoard hook was needed).
- DALA_850, DALA_864, DALA_866: family 01's, untouched; their rules01 logs are line-for-line equal to the main tree's p8w1 logs after my framework edits.

Rejected:
- DALA_857 Glittering: the wiki says the pool is the Dalaran Heist treasures. Those are run rewards admitted for the run player's run deck only (RunTreasureLift: no boss and no generation pool may hold one) and several write run state (DALA_718, 719, 721). A rule for both players needs each treasure admitted for a boss and as a generation result. Schedule row `status: rejected` with the reason; admission row Admit None with lift RuleTreasureLift (phase 8: the treasure-on-boss work of the families that give a boss treasure cards, LOOTA_BOSS_99p / Ol' Toomba); landed-family E05 lists the rejection. Not phase 9.

Data gaps settled: DALA_858 "occasionally" (wiki: a small chance at each turn start, no number: 1 in 4, recorded as adaptation fishy-chance); DALA_860 token (LOOT_209t); DALA_857 pool (the cause of the rejection); Violet Prison rule (wiki Twists section); Hallucination = UNG_856. Sources per rule are in reference\encounters\F05.json "rules" and 11 new curation.json ruleAdaptations. Wiki pages read 2026-11-13: The_Dalaran_Heist, Anomaly_-_Fishy, Anomaly_-_Glittering, Anomaly_-_Dragon_Soul, Anomaly_-_Hallucinating, Anomaly_-_Gorged, Violet_Prison, Twist_-_The_Prisons, Anomaly.

=====================================================================
2. FILES CHANGED (repo copy C:\Users\user\AppData\Local\Temp\claude\0a564ffc8743aeec\d2a4e245-ca37-4433-9b55-c35ed325f081\scratchpad\L1\repo; all LF, no BOM; a read-only diff against the lane base shows exactly these)
=====================================================================
Own family files
- engine\SabberStoneCore\src\CardSets\DungeonRun\DungeonRunEncounters.F05.cs (18 rules, tokens, enchantments; RuleMinions pool cache)
- engine\SabberStoneCore\src\Actions\EncounterRules.F05.cs (NEW: BattlecriesTwice / DeathrattlesTwice(bothPlayers), RecastRandom, PlaceBlockers / Blockers, Imprison / Imprisoned / PrisonTurns / TickPrison, SwapStatsOnSummon)
- plugin\DungeonRun\DungeonRunAdmission.F05.cs (7 rows: global-rule-symmetric, -generated, -recast, -start, -hidden, -rejected, -token)
- data\dungeon-run\reference\encounters\F05.json (NEW: no boss rows; "rules" records each rule's source)

Shared files (each edit minimal and local)
- engine ...\CardSets\DungeonRun\DungeonRunEncounters.cs: class RuleHooks (OpeningCards, OnApply, OnStart, Cards), EncounterCard.Hooks / HookIndex, `Rule(id, effectId, effect, hooks = null)` (effectId may be null), DungeonRunGlobalRules.HasStart / OpeningCards / CardsOf / ResolveStart, Apply and IsActive for hooks-only rules. Reason: five rules have no client "Player Enchantment" card and are start conditions.
- engine ...\Actions\EncounterRules.cs: two tag constants in the table; one call `DungeonRunGlobalRules.ResolveStart(game)` at the end of ResolveStart. Reason: rule boards and deck inserts land with the start block.
- engine ...\CardSets\DungeonRun\DungeonRunSetup.cs: RunEncounter.HasStart also true for a rule with a start hook; Configure adds a rule's opening cards to both players within the cap. Reason: Gorged, and start hooks on a boss with no start block.
- plugin\DungeonRun\DungeonRunData.cs: DungeonRunGlobalRule.IncompatibleClasses + its parse line ("incompatibleClasses"). Reason: The Carts and Darius Crowley's Cannons (the design's "run itself" check had no hero hook).
- plugin\DungeonRun\DungeonRun.cs: one line in RuleCandidates for that key.
- plugin\DungeonRun\DungeonRunValidator.cs: client + scripted check of DungeonRunGlobalRules.CardsOf(rule); unknown-class check for incompatibleClasses.
- data\dungeon-run\schedule-v1.json: 19 rows appended to "rules" only (14 anomalies admitted, DALA_857 rejected, 4 twists) with incompatibleFlags / incompatibleRules / noBoardShrine / incompatibleClasses.
- tools\restoration-ledger\curation.json: landedFamilies E05 (ids, supportRecords for the six tokens, rejected DALA_857) and 11 ruleAdaptations. The two tool-owned lists come out unchanged.
Generated in my copy (dropped by the merge): encounters-v1.json (only the F05.json source hash), restoration-ledger.csv / .md. catalog-v1.json and dungeon-run-catalog.md are byte-identical to the base.

Likely merge collisions (same insertion points as other lanes): the tag table of EncounterRules.cs (my two constants sit just above RuleTagsFrom), the end of schedule-v1.json "rules", landedFamilies in curation.json, and the three suite lines below.

=====================================================================
3. PINS / EXPECTATIONS MOVED IN OLDER SUITES (my copy; script p8f05\update_suites.py documents each)
=====================================================================
- c-admission total: 2584 -> 2695 (7 new rows, 25 ids). Its "table lists only ledger rows" exemption: `r.Family == "global-rule-symmetric"` -> `r.Family.StartsWith("global-rule")`.
- c-enc-engine C6: the example of an unscripted rule DALA_852 -> DALA_857 (852 is scripted now). Still 122.
- c-enc-model (still 141): B22 / B23 used DALA_852 / DALA_853 as cards with no admission row / no script -> CS2_182 / CS2_120; G7 "a rule not on offer" DALA_852 -> DALA_857; J13 no longer pins "every registered card is family 01's" and the exact scripted list (now: families 01..25, family 01's rules are exactly its three, Scripted is ordinal).
- Not run in the lane, will move after the merge: c-catalog's ledger golden 2813 / 40 / 668 -> 2813 / 58 / 650 (18 rule rows go missing -> partial); ledger rows stay 4579, so c-deckops T5 holds.
- Unchanged: c-acts A6 DDE3676B5A519BEE, c-deckops L1 8B1B4ED89144446B.

=====================================================================
4. TESTS ADDED (paths after the merge: C:\Users\user\AppData\Local\Temp\claude\0a564ffc8743aeec\d2a4e245-ca37-4433-9b55-c35ed325f081\scratchpad\p8f05\...)
=====================================================================
- p8f05\suites\c-rules-05 (166 checks): sections A-R one per rule, each rule's text on both sides in real engine games through DungeonRunSetup (History on); S the framework (hooks once, marks in clones, texts equal the client's, tokens, private tags); T the run layer (shipped data validates, admission, the anomaly offer over 400 seeds, incompatibility by boss flag / board shrine / Darius / rule against rule, validator refusals, the twist roll on the fixture schedule, plan, rule page text). Takes the fixtures folder as its argument.
- p8f05\suites\c-bosspilot-rules05: generated by p8f05\make_bp_rules.py from the shared encounter harness (rerun `run.sh harness` when that harness changes). Adds: BOSS may be a custom boss; with RULE a probe checks what the rule places at the start (failed check when missing) and counts the rule's own effect per game ("seen N", last line "rule-seen N, rule-unseen <bosses>").
- p8f05\run.sh: fixture | harness | build [suite] | suite <n> | bp <n> <rule> [games] | same <rule> | rules. Roots are derived from the script's location; the `fixture` step needs REPO=<repo root> in the main tree. p8f05\make_fixture.py <repo> <scratch> rebuilds fixtures\schedule-twists.json (rerun after schedule-v1.json changes; it stops once the shipped twist pools are filled).
- Evidence kept for the merge: p8f05\bp-rules05-<rule>-a.txt / -b.txt (18 rules). Doc text for docs\design\dungeon-run.md: p8f05\dungeon-run-family-05.md (I left the shared doc to the integrator, as wave 1 did).

=====================================================================
5. RESULTS (final build; every harness through hrun.sh; three calls returned 75 and were repeated)
=====================================================================
Builds: engine 0 warnings / 0 errors (md5 85e5255496cc04080e268b680fc40217); plugin 0 errors / 75 warnings; 11 suite builds 0 errors, each holding the final engine dll.
Generators (encounters, catalog, ledger), run twice, six files byte-identical: encounters 8 rows, 12 decks (6 authentic, 6 changed), 15 adaptations, 1 rejected; catalog 506 entries, 6508 admitted / 1 excluded; ledger 4579 rows: implemented 2813, partial 58, missing 650, duplicate/support 1058, intentionally adapted 0.
Suites, run twice, run 1 = run 2 for every one: c-content 904, c-admission 2695, c-runlayer 244, c-acts 181, c-deckops 174, c-shrine-core 131, c-enc-model 141, c-enc-engine 122, c-rules-05 166.
Shared harness, once each, 0 differing lines against the main tree's p8w1 logs: enc01 (20 games, boss wins 17, form-unreached 1 as before), rules01 DALA_850 / 864 / 866 (10 games each).
rules05 bosspilot: each rule on tunnel_rat (custom), loot_35_brandlemar, gila_25_ivan, trl_200_voone (three shrine variants), dala_21_whirt, ulda_44_trap_room, 2 games each; The Stench and The Carts meet loot_48_trapped_room instead of Voone (they exclude shrine bosses). Every rule twice, the two runs identical, 0 faults / 0 fallbacks / 0 failed checks / 0 swallowed leaf exceptions, power-unused 0, stderr empty:

| Rule | Games | Boss wins | rule-seen | rule-unseen |
|---|---|---|---|---|
| DALA_851 | 16 | 13 | 149 | - |
| DALA_852 | 16 | 11 | 90 | - |
| DALA_853 | 16 | 9 | 37 | tunnel_rat (no Deathrattle minion died in its two games) |
| DALA_854 | 16 | 12 | 217 | - |
| DALA_855 | 16 | 12 | 63 | - |
| DALA_856 | 16 | 11 | 99 | - |
| DALA_858 | 16 | 9 | 64 | - |
| DALA_859 | 16 | 10 | 16 | - |
| DALA_860 | 16 | 11 | 9 | tunnel_rat, dala_21_whirt (nobody cast three spells in a turn there) |
| DALA_861 | 16 | 9 | 104 | - |
| DALA_862 | 16 | 11 | 177 | - |
| DALA_863 | 16 | 12 | 105 | - |
| DALA_865 | 16 | 13 | 355 | - |
| DALA_867 | 16 | 7 | 275 | - |
| DALA_829 | 16 | 11 | 15 | - |
| DALA_831 | 12 | 9 | 123 | - |
| DALA_832 | 16 | 9 | 30 | - |
| DALA_833 | 12 | 8 | 346 | - |

Not run, as the lane rules say: hall, the full runs.sh, the released bosspilot modes, selfplay, randplay, c-catalog.

=====================================================================
6. FOR THE INTEGRATOR AND LATER FAMILIES
=====================================================================
- Regenerate encounters, catalog and ledger in the main tree; recompute c-admission's total and c-catalog's ledger golden; add c-rules-05, c-bosspilot-rules05 and the rules05 mode to builds.sh / runs.sh / bp.sh; paste p8f05\dungeon-run-family-05.md into the design doc.
- Decide flag 1 (twist pools and chance) and flag 3 (GVG_054) in the close-out.
- Incompatibility is data on the rule rows; it only works when boss rows carry the flags. No shipped row has doubleBattlecry, doubleDeathrattle, recast, forcedAttack or boardStart yet: families 08, 11, 12, 15, 18 and the board-start bosses must set them. Family 14 should list DALA_861 in Tierra Blythe's incompatibleRules. Family 06 should list DALA_831 on ULDA_709 and DALA_829 / DALA_832 / DALA_833 on its board-start rules (ULDA_804, ULDA_719); incompatibleRules is checked both ways.
- Rule authoring: `Rule(id, effectId, effect, hooks)`; a hooks-only rule passes null, null. An apply hook runs once per match (bit in RuleApplied; at most 31 hooked rules, registration throws beyond that). A trigger that changes the attack's defender must use FastExecution.
- Limits to know: a rule's boards and deck inserts land after both mulligans (a Hallucination is never in an opening hand); a released prisoner is a transform (no summon event, so after-summon rules do not touch it); The Stench swaps once on entry, not continuously; the Violet Prison's public tags and the hooks-only rules' missing enchantment were never looked at in the client; Drakkari Enchanter doubles the rules' end-of-turn triggers like any other end-of-turn effect.
