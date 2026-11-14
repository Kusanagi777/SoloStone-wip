PHASE 8 FAMILY 06 (Tombs of Terror anomalies and plague twists) - LANE 1 REPORT

All 33 ids are done or covered as support; none is rejected. Everything is in the lane copy only (repo `C:\Users\user\AppData\Local\Temp\claude\0a564ffc8743aeec\d2a4e245-ca37-4433-9b55-c35ed325f081\scratchpad\L1\repo`, scratch `...\scratchpad\L1\sp`). Nothing was written, built or run in the main tree or another lane (main was only read, to diff logs). No harness process of mine is running and the harness lock is free. Not verified in the real client: all evidence is engine games and harnesses.

FLAGS FOR THE INTEGRATOR (read first)
- Tags: only my reserved ones. `HeroDamageDouble = 32641` (model tag) and `RuleTombSummons = 32680` (first of my rule-state range), both added to the tag table in `EncounterRules.cs`. No tag outside my reservation.
- No schedule default flipped, no finale enabled, no act or pool set changed, no save field added, no ReleasedShapes or golden re-pin. The four twists are rule rows only; the shipped twist pools stay empty (close-out).
- Core engine files edited beyond the listed shared files (each minimal, reasons in section 3): `Character.cs` (1 statement), `GameConfig.cs` (1 new constant), `Game.cs` (opening-bonus validation), and the ledger tool `tools\restoration-ledger\Program.cs` (1 statement).
- Engine finding, not fixed (core mulligan code): a player who enters the mulligan with a full 10-card hand loses a card. `Generic.ChoiceMulligan` adds the replacement before removing the old card, so the first replacement is burnt. This is why Bountiful gives the boss 8 cards plus The Coin. It also affects any boss row with `start.bossOpeningHandBonus` 5 (4 + 5 + Coin = 10).
- Data sparsity, not a defect: every acts-v3 run currently draws War Master Voone (the only TRL row, shrine) and Manhunter Ivan (the only GIL combat row, secrets) in act I, so ULDA_706 No-Face and ULDA_721 Aleatory are never offered at act I until more TRL / GIL rows exist. An act without those bosses offers all 17 (c-rules-06 W11b).
- Mirror defect corrected in my copy: `L1\sp\p8f05\run.sh` fell back to `/c/SoloStone` when REPO was unset (the Git Bash spelling was not rewritten). I never ran it; its default is now `$(cygpath -u '<Windows repo literal>')`, which the lane tool rewrites both ways. This is a one-line diff in a family-05 file.

1. PER ID
Anomalies with a match script (rule card + client player enchantment unless noted), all admitted, all in schedule-v1.json "rules":
- ULDA_702 Loot-Filled: done. A played minion gives the other player a Coin; a full hand takes none.
- ULDA_705 Booms Away: done. Seaforium Bomber's Bomb (BOT_511t) into the deck of the dead minion's controller, as a public generated shuffle.
- ULDA_706 No-Face: done (`EncounterRules.HeroImmuneWhile`). Permanents and dormant minions do not count. Excluded on shrine runs and with flags shrine / heroImmune.
- ULDA_707 Eternal Army: done. A new card of the dead minion's card into its controller's deck, public shuffle.
- ULDA_708 Dizzying Blows: done. Each hand card gets a random Cost 0..10 (enchantment ULDA_708e2); Echo / repeatable cards never below 1.
- ULDA_709 Angelic Presence: done (`EncounterRules.AttackEqualsHealth`, Lightspawn's rule for a whole board). Excludes DALA_831.
- ULDA_710 CHARGE! and ULDA_711 Relentless: done. Board auras with the client enchantments ULDA_710e2 / ULDA_711e2.
- ULDA_712 Holograms: done. A 1/1 copy of the played minion's card for the other side (Barnes's KAR_114e; the client has no 1/1 enchantment for it).
- ULDA_713 Whirling Winds: done. 1 damage to all minions at each turn start.
- ULDA_714 In the Face: done. Hero damage doubled before Armor; Animated Armor's cap applies after. Excludes flag damageCap.
- ULDA_715 Bountiful: done, hooks only (new `RuleHooks.OpeningHand`). Run player 3 + 6 cards; boss 4 + 4 and The Coin (9 in hand each). Excludes mod_open_road.
- ULDA_719 Marin's Return: done, hooks only. Each player starts with Marin's Master Chest LOOT_357l on its own side; the breaker gets one of the four treasures. Excludes the other board-start rules and flag boardStart.
- ULDA_721 Aleatory: done, hooks only (`EncounterRules.MixDecks`). Runs after both mulligans; the card entities change sides; a card the receiving side's generation filter blocks stays with its owner (a boss is never handed a rejected card, a run treasure or a Secret). Both deck beliefs are reset. Excludes ULDA_801 and flags deckReplace, relay, heroSwitch, secrets.
- ULDA_724 Spellshields: done. 2 Armor after each spell that was not countered.
Run-layer anomalies (schedule effect "run", scope "run"; no match script, never in a plan's rule list):
- ULDA_720 Battle Scars: done. Family 01's run state (HeroDamage, 100 Health) now has its rule row, admission row and tests.
- ULDA_729 Multiclassing: done. New `DungeonRun.LootClasses`: the catalog card drafts offer every class, a deck copy accepts such a card, and the boss's Secret prior covers every class. Class Discovers, tavern pools and the treasure pool keep the run's classes.
Twists (scripted, admitted, listed; no shipped pool names them yet):
- ULDA_801 Plague of Murlocs: done. Copies per deck = the run's fight number capped at 8, shuffled after both mulligans, each owner's deck recorded with `DeckRewritten`. Excludes flags deckReplace, relay.
- ULDA_802 Plague of Madness: done (`EncounterRules.ForcedAttackRandomEnemy` / `RightmostMinion`).
- ULDA_803 Plague of Wrath: done (+1 Attack, client enchantment ULDA_803e2).
- ULDA_804 Plague of Death: done, hooks only. Excludes the board-start rules and flag boardStart.
Tokens:
- ULDA_801t Surprise! Murlocs!: done. Casts When Drawn; 1 to 3 random scripted Murlocs for the drawer, then a draw. Public helper `DungeonRunEncounters.ShuffleSurpriseMurlocs` for family 24.
- ULDA_804t Eternal Tomb: done. Holds a random scripted minion (public mouse-over card), summons it after as many friendly deaths as it costs, then holds a new one; at most 7 summons per Tomb per turn.
Support records, not created: ULDA_702d, 705d, 707d, 708d, 712d, 713d, 721d, 724d, 801d, 803d. They are the client's "Dummy Hook Up" script holders; the scripts sit on the player enchantments. Listed under `supportRecords` of E06 with the two tokens.

Data gaps from the brief, as resolved (wiki read 2026-11-13, recorded in F06.json and as ruleAdaptations):
- ULDA_719 "Treasure Chest": the wiki names no chest card but lists Master Chest and Marin's four treasures as related cards, so LOOT_357l is used.
- ULDA_804t: the facts file text is complete ("When @ friendly minions die, summon a random minion", counter tag 3); the Tombs wiki page gives the cost-based rule that is implemented.
- ULDA_801t "random amount" and ULDA_708's Cost range: no source gives a number. 1 to 3 Murlocs and 0 to 10 are my choices, recorded as adaptations.

2. OWN FAMILY FILES
- `...\L1\repo\engine\SabberStoneCore\src\CardSets\DungeonRun\DungeonRunEncounters.F06.cs` (19 rules, 2 tokens, 4 card enchantments)
- `...\L1\repo\engine\SabberStoneCore\src\Actions\EncounterRules.F06.cs` (new: ForcedAttack, ForcedAttackRandomEnemy, RightmostMinion, ControlsMinion, HeroImmuneWhile, HeroDamageTaken, AttackEqualsHealth, MixDecks)
- `...\L1\repo\plugin\DungeonRun\DungeonRunAdmission.F06.cs` (5 rows, 23 ids)
- `...\L1\repo\data\dungeon-run\reference\encounters\F06.json` (no boss rows; per-rule sources)

3. SHARED FILES (each edit is one block, applied by an asserting script in `L1\sp\p8f06`)
- `engine\...\Actions\EncounterRules.cs`: two tag constants. Reason: the table is the only place tags may be declared.
- `engine\...\Model\Entities\Character.cs`: one statement in TakeDamage before family 02's cap line. Reason: doubling must happen before Armor; a pre-damage trigger only sees the post-Armor amount.
- `engine\...\Config\GameConfig.cs`: `MaxFirstOpeningHandBonus = 6`. `engine\...\Model\Game.cs`: the bonus validation allows 6 only for the player `StartPlayer` names. Reason: nine cards for the first player need 6; `MaxOpeningHandBonus` stays 5, so no existing clamp changes.
- `engine\...\CardSets\DungeonRun\DungeonRunEncounters.cs`: `RuleHooks.OpeningHand` and `DungeonRunGlobalRules.OpeningHand`. `...\DungeonRunSetup.cs`: Configure raises both bonuses to reach it. Reason: `OpeningCards` adds the same number to both players; Bountiful states a size.
- `plugin\DungeonRun\DungeonRun.cs`: constant `Multiclassing`, `LootClasses`, three one-line uses (OpenCatalogDraft's classes, its dual-pair weight kept on the run's own pair, MatchClasses). `plugin\DungeonRun\DungeonRunDeck.cs`: CopyError reads LootClasses. Reason: ULDA_729.
- `data\dungeon-run\schedule-v1.json`: 21 rule rows appended after DALA_833. Nothing else.
- `tools\restoration-ledger\curation.json`: landed family E06 (one line after E05) and 12 ruleAdaptations appended after `explosive-limit`. The two tool-owned lists were only rewritten by the tool.
- `tools\restoration-ledger\Program.cs`: one statement in EngineBehavior: an admitted rule with effect "run" is `run-service`. Reason: without it the landed-family self-check fails ULDA_720 / ULDA_729 as "engine unscripted".
- Suites and scripts: `L1\sp\p8f05\suites\c-rules-05\Program.cs`, `L1\sp\audit-verify\c-catalog\Program.cs` (pins, section 4), `L1\sp\p8f05\run.sh` (mirror defect).
Encoding: every file LF. Character.cs, GameConfig.cs and Game.cs keep the BOM they already had; none added.

4. PINS MOVED (updated in my copy; originals in `L1\sp\p8f06\before\suites`)
- c-admission total: 2854 -> 2948; generated lists 351 -> 374 for both sides. Cause: 23 new ids. This is a result line, not a check.
- c-catalog ledger golden: 165 partial / 608 missing -> 186 / 587 (rows 4644 and implemented 2813 unchanged). Cause: 21 rule rows missing -> partial.
- c-rules-05 T2, T6, T9, T11: these counted every rule in the schedule; they now filter to provenance DALA. Still 170 passed.
- c-enc-model G1: detail text only (the act I offer for that seed changed with the larger candidate set); 141 passed.
- Unchanged: c-acts A6 digest, c-deckops L1 digest and T5 (ledger still 4644 rows), c-content passive list, c-enc-model A4 / J13.

5. TESTS ADDED (paths after the merge)
- `C:\Users\user\AppData\Local\Temp\claude\0a564ffc8743aeec\d2a4e245-ca37-4433-9b55-c35ed325f081\scratchpad\p8f06\suites\c-rules-06` (Program.cs, Rules06.csproj): 224 checks. Sections A-T, one per scripted rule, in real engine games through DungeonRunSetup; U Battle Scars and V Multiclassing on the run layer; W framework, admission, incompatibility, anomaly offer, twist roll through the fixture.
- `...\scratchpad\p8f06\suites\c-bosspilot-rules06`: generated from the base encounter harness by `...\scratchpad\p8f06\make_bp_rules06.py`. It accepts a custom boss, injects the rule with its own kind (run-scope rules from the start of the run), checks the run-layer rules on the plan and after the fight, and has a per-rule probe.
- `...\scratchpad\p8f06\fixtures\schedule-plagues.json`, made by `make_fixture.py` (the four plagues in every acts-v3h twist pool, chance 100).
- `...\scratchpad\p8f06\run.sh`: steps fixture | harness | build | suite | bp | same | rules.
- Evidence: `...\scratchpad\p8f06\bp-rules06-<rule>-a.txt` and `-b.txt` for the 21 rules.

6. RESULTS (final source state)
- Builds: engine 0 warnings / 0 errors; plugin 0 errors / 75 warnings; all suite builds 0 errors, each holding the final engine dll.
- Generators, run twice, six files byte-identical: encounters 43 rows / 47 decks / 56 adaptations / 1 rejected; catalog identical to the main tree; ledger 4644 rows, implemented 2813, partial 186, missing 587, duplicate/support 1058, adapted 0. The 17 anomalies are partial with blocker run:default-schedule; the 4 twists are partial with run:global-rules until a pool names them.
- Reduced suites, three runs each, identical: c-content 904, c-admission 2948, c-runlayer 244, c-deckops 174, c-shrine-core 131, c-enc-model 141, c-enc-engine 122. c-acts 181, two runs identical. Against the main tree's wave 2 logs: 0 differing lines for c-content, c-runlayer, c-acts, c-deckops, c-shrine-core, c-enc-engine; only the lines named in section 4 for c-admission and c-enc-model.
- c-rules-06: 224 passed, 0 failed, three runs identical. c-rules-05 170 and c-catalog 127 with the updated pins.
- rules06 bosspilot: each of the 21 rules on six bosses, 2 games per boss or variant (16 games; 12 for ULDA_706), run twice, identical. Every rule: 0 faults, 0 fallbacks, 0 failed checks, 0 swallowed leaf exceptions, power-unused 0, stderr empty. Boss sets: default `tunnel_rat, loot_35_brandlemar, gila_25_ivan, trl_200_voone, dala_21_whirt, ulda_44_trap_room`; ULDA_706 swaps Voone for the Trapped Room; ULDA_721 meets `tunnel_rat, loot_04_sturmi, loot_19_gutmook, trl_200_voone, loot_47_lava_chamber, loot_21_gnosh` because no GIL / DALA / ULDA row is free of its excluded flags yet.
- Every rule was seen in play. The one exception is expected: ULDA_729 is unseen on tunnel_rat and Brandlemar, which are fought on floor 1 before any card draft.
- Existing play is unchanged by the shared edits: the base harness on the eight family-01 rows (20 games) and RULE=DALA_850 (10 games) gave 0 differing lines against the main tree's `p8w2\bp-enc01-a.txt` and `bp-rules01-DALA_850-a.txt`.
- Not run, per the lane rules: hall, full runs.sh, the released bosspilot modes, selfplay, randplay. Twists were not played by the bosspilot through the real roll; the roll, the plan and the match start are covered by c-rules-06 W23-W25, and each twist was played as an injected rule.

7. WHAT THE INTEGRATOR MUST REDO IN THE MAIN TREE
- Regenerate encounters, catalog and ledger; recompute the c-catalog golden and c-admission total after all lanes.
- Add c-rules-06 and c-bosspilot-rules06 to builds.sh / runs.sh / bp.sh. Run `run.sh harness` again if the base encounter harness changed in the merge, and `run.sh fixture` after schedule-v1.json is final. `make_fixture.py` stops on purpose once the close-out fills the twist pools; W23-W25 should then point at the shipped schedule.
- Close-out: put ULDA_801..804 into act twist pools. Docs: `docs\design\dungeon-run.md` was not edited; the E06 note and the 12 ruleAdaptations in curation.json hold the text.
- Hooked-rule bits: 11 of 31 are used (family 05: 5; mine: ULDA_714, 715, 719, 721, 801, 804). Bits follow registration order.
- Helpers for later families: ForcedAttack (11, 20, 22, 24), HeroImmuneWhile with a predicate (23 Glinda, 24 Vesh), AttackEqualsHealth, HeroDamageDouble, MixDecks, ShuffleSurpriseMurlocs (24).
- Limit to know: in a game without presentation history (the harnesses), a public generated shuffle (Bomb, Eternal Army copy) reaches the owner's own-deck ledger as an unknown card, as Seaforium Bomber's Bomb already does; with history on, the ledger takes the card from the shown shuffle (c-rules-06 B5, D5). Plague of Murlocs uses DeckRewritten and is known in both modes (Q4).
