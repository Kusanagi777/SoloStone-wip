PHASE 8 FAMILY 02 (Unscripted collectibles A) - LANE 1 REPORT

All 33 ids are done; none is rejected and nothing is deferred to phase 9. Evidence is engine games and harnesses only: nothing was checked in the real client. Hall, the released bosspilot modes, selfplay and randplay were not run (lane rules).

Paths: lane repo `C:\Users\user\AppData\Local\Temp\claude\0a564ffc8743aeec\d2a4e245-ca37-4433-9b55-c35ed325f081\scratchpad\L1\repo` (below R), lane scratch `...\scratchpad\L1\sp` (below S). After the merge my tests are under `C:\Users\user\AppData\Local\Temp\claude\0a564ffc8743aeec\d2a4e245-ca37-4433-9b55-c35ed325f081\scratchpad\p8f02`.

=====================================================================
FLAGS FOR THE INTEGRATOR (read first)
=====================================================================
- **Private tag 32637** (`EncounterRules.HeroDamageCap`) is declared in my partial file `R\engine\SabberStoneCore\src\Actions\EncounterRules.F02.cs`, not in the tag table of `EncounterRules.cs`. Move the constant into the table if you want one place; no other tag was taken.
- **No schedule, save version, ReleasedShapes or golden sequence was touched.** No encounter card was registered (`DungeonRunEncounters.All` is still family 01 only), so c-enc-model J13 and A4 are unchanged.
- **Engine edits outside my family files** (four, each one to three lines; listed under "Shared files").
- **Digests that will move in the main tree and were not run here:** randplay almost certainly, selfplay possibly. All 33 cards were already members of the frozen legacy pools (`LegacyGenerationV1.xml`) as blanks; now they act when generated. Bouncing Blade, Call Pet, Prismatic Lens and Duel! also become castable by random-spell effects (`CastRandomSpellTask` skips unscripted spells), which changes RNG consumption.
- **Released bosspilot modes** (catalog, act, the shrine modes) will move with the regenerated catalog, as the design expected.

=====================================================================
1. PER ID (all done)
=====================================================================
Every card is scripted in place in its card-set file, has an admission row, is asserted in c-collectibles-A, and was played from hand by the fair AI as the boss (collectA) and by the run player against a fair-AI boss (collectP1-4).

| Id | What the script does |
|---|---|
| AT_027 Wilfred Fizzlebang | A card drawn while the owner's Hero Power task is executing costs (0) (AT_027e) |
| AT_079 Mysterious Challenger | One of each Secret name from the deck into play; random subset when more names than room |
| AT_081 Eadric the Pure | Enemy minions' Attack set to 1 (AT_081e) |
| AT_115 Fencing Coach | Player enchantment AT_115e: Hero Power (2) cheaper until the owner's next use |
| BOT_436 Prismatic Lens | Draws a random minion and spell, swaps Costs (BOT_436e) |
| DAL_573 Commander Rhyssa | Owner's Secrets resolve their effect twice (`EncounterRules.SecretRepeats`, read in `ComplexTask.Secret`) |
| DAL_729 Madame Lazul | Discover among up to three different cards of the opponent's hand |
| DAL_731 Duel! | A random minion from each deck is summoned; the caster's attacks the other |
| EX1_112 Gelbin Mekkatorque | Summons one of Mekka1-4, all four scripted (plus Mekka3e, Mekka4t) |
| GIL_655 Festeroot Hulk | +1 Attack after any friendly minion attacks |
| GVG_014 Vol'jin | Swaps current Health with the target (GVG_014a) |
| GVG_017 Call Pet | Draw; a Beast costs (4) less |
| GVG_025 One-eyed Cheat | Stealth when its owner summons a Pirate |
| GVG_049 Gahz'rilla | Attack doubles on damage (GVG_049e) |
| GVG_050 Bouncing Blade | 1 damage to random minions until one is mortally wounded; bound 80 hits |
| GVG_054 Ogre Warmaul | The wielding hero has the 50% wrong-enemy rule (`EncounterRules.WrongTarget50`) |
| GVG_097 Lil' Exorcist | +1/+1 per enemy Deathrattle minion (client enchantment GVG_101e "Pure") |
| GVG_107 Enhance-o Mechano | Each other friendly minion gets Windfury, Taunt or Divine Shield at random |
| GVG_108 Recombobulator | Transform into a random scripted minion of the same Cost; a shrine goes dormant instead |
| GVG_111 Mimiron's Head | With 3 Mechs at turn start: they die first, then V-07-TR-0N is summoned (GVG_111t given Mega-Windfury) |
| GVG_113 Foe Reaper 4000 | The engine's cleave tag |
| GVG_119 Blingtron 3000 | A random scripted collectible weapon for each player, each under its own generation list |
| LOE_009 Obsidian Destroyer | Scarab (LOE_009t) at the owner's turn end |
| LOE_022 Fierce Monkey | Vanilla Taunt |
| LOE_029 Jeweled Scarab | Discover a scripted 3-Cost card of the owner's classes or neutral |
| LOE_047 Tomb Spider | Discover a scripted Beast |
| LOE_053 Djinni of Zephyrs | `RecastRules.Recast` onto itself after a spell on another friendly minion |
| LOE_061 Anubisath Sentinel | Deathrattle +3/+3 on a random friendly minion |
| LOE_092 Arch-Thief Rafaam | Discover among LOEA16_3 / LOEA16_4 / LOEA16_5, all scripted (plus LOEA16_3e, LOEA16_5t) |
| LOE_107 Eerie Statue | Can't attack while another non-dormant minion is in play |
| LOE_119 Animated Armor | Hero takes at most 1 damage per hit (`EncounterRules.HeroDamageCap`, applied before Armor) |
| TRL_318 Hex Lord Malacrass | Copies of the recorded opening hand, without himself and The Coin |
| DAL_431 Swampqueen Hagatha | Two Discovers among scripted Shaman spells, then a generated Drustvar Horror whose Battlecry casts both |
| GVG_014, GVG_049 | Also Tekahn finale-deck cards; see above |

Recorded adaptations (in the E02 note of `curation.json` and the admission reasons):
- The Horror settles a choice that one of its spells opens with the game RNG (the recast rule).
- Jeweled Scarab, Tomb Spider, Recombobulator and Blingtron yield scripted cards only, sampled uniformly (no class weighting).
- Commander Rhyssa doubles every Secret that resolves through `ComplexTask.Secret` (all run-pool Secrets, including LOOT_101 and ULDA_045t). The Core reprint CORE_LOOT_101 has its own reveal code and is not doubled; it is not a run card.
- Two stub play requirements were wrong and are removed: AT_115 and BOT_436 listed a minion target they do not have.

Helpers for later families (`EncounterRules.F02.cs`):
- `WrongTarget50(attacker)` and `WrongTargetTask`: use from an ATTACK trigger whose event source is the attacker (DALA_861, DALA_BOSS_14t). The unscripted Ogre minions (GVG_065, 066, 088, 112, AT_088) were left alone, as they are not in my list.
- `HeroDamageCap`: set the tag to N on a minion or hero power card, or on the hero entity (DALA_BOSS_65p).
- `SecretRepeats(controller)`, `CapturesOpening(game)`.
- `DungeonRunEncounters.ScriptedPool` / `DiscoverScripted` for filtered Discovers.

=====================================================================
2. FILES CHANGED
=====================================================================
Own family files:
- `R\engine\SabberStoneCore\src\Actions\EncounterRules.F02.cs` (new: the four rules and `SecretTriggerTask`)
- `R\engine\SabberStoneCore\src\CardSets\DungeonRun\DungeonRunEncounters.F02.cs` (task builders; `F02()` registers nothing)
- `R\plugin\DungeonRun\DungeonRunAdmission.F02.cs` (10 rows for 31 ids; AT_079 and DAL_573 keep their core secret-from-deck row)
- `R\data\dungeon-run\reference\encounters\F02.json` (new; no boss rows, description only)

Card-set files (only my cards and their tokens and enchantments):
- `R\engine\SabberStoneCore\src\CardSets\TgtCardsGen.cs`, `GvgCardsGen.cs`, `LoeCardsGen.cs`, `HofCardsGen.cs`
- `R\engine\SabberStoneCore\src\CardSets\Standard\BoomsdayCardsGen.cs`, `DalaranCardsGen.cs`, `GilneasCardsGen.cs`, `TrollCardsGen.cs`
- LoeCardsGen gains five new entries (LOEA16_3, 3e, 4, 5, 5t) right after LOE_092.

Shared files (reason each):
- `R\engine\SabberStoneCore\src\Model\Entities\Character.cs`: 3 lines in `TakeDamage`, the hero damage cap before Armor (a pre-damage trigger cannot change how much Armor is lost).
- `R\engine\SabberStoneCore\src\Tasks\ComplexTasks.cs`: `Secret(...)` returns `SecretTriggerTask` (same chain rule, effect repeated for Rhyssa); there is no other hook before the reveal.
- `R\engine\SabberStoneCore\src\Actions\TurnQueueRules.cs`: one condition in `CaptureOpening`, so opening hands are recorded in run matches and when a deck lists TRL_318. The tags are already private on the transport, and the fair view already redacts the opponent's.
- `R\engine\SabberStoneCore\src\Model\HistoryFacts.ResolutionRecorder.cs`: `internal IEntity ExecutingSource` (2 lines), which AT_027 reads.
- `R\tools\restoration-ledger\curation.json`: one hand-written line, landed family E02 (and a comma after E01). The two tool-owned lists were rewritten by the encounters step.

Generated in my copy (dropped at merge): encounters-v1.json, catalog-v1.json, dungeon-run-catalog.md, restoration-ledger.csv / .md. All three generators reproduce byte for byte.

=====================================================================
3. PINS MOVED
=====================================================================
| Suite | Old | New | Cause |
|---|---|---|---|
| c-admission total | 2244 | 2367 | The F02 rows |
| c-admission "table lists only ledger rows" | - | 2 lines added after the phase7Ids statement | Admits rows whose lift is `NoneCollectible` |
| c-acts A6 digest (printed) | 9F91C1FE52A4B998 | CD456BFD4A8B8FF5 | Regenerated catalog only: returns to the old value with the baseline catalog and my engine |
| c-deckops L1 digest (NOTE) | 5169BABFF3951AA2 | 8D3877F2BE950F2F | Same attribution, same experiment |
| c-enc-model B21 | mutates dala_21_whirt | mutates the first non-authentic row | Whirt's deck is authentic now (Animated Armor scripted) |
| c-enc-secrets S27 (p8f01b) | 1 substitution, listed DAL_573 | authentic, 0 substitutions, DAL_573 once | Rhyssa scripted |
| c-catalog admitted / excluded references | 6318 / 191 | 6408 / 101 | 90 member references of the 33 cards |
| c-catalog engine:unscripted distinct ids | 91 | 58 | 33 scripted |
| c-catalog ledger golden | 2722 implemented, 760 missing | 2755, 727 | 33 rows; 4579 rows, 39 partial, 1058 duplicate/support unchanged |

- The c-acts run with the baseline catalog printed the baseline A6 digest and then stopped at Program.cs line 769 (exit 127); only the digest line was used.
- Generation lists are unchanged (283 both sides, 304 boss-only).
- c-deckops T5 and c-enc-model A4 / J13 did not move.

=====================================================================
4. TESTS ADDED (under `S\p8f02`, own script `run.sh`)
=====================================================================
- `suites\c-collectibles-A`: 115 checks. Every card's text on the run player's side and, where it differs, the boss's; all tokens; six fair-view checks; admission; catalog membership.
- `fixtures\encounters-collectA.json` (built by `make_fixture.py` from the lane's encounters-v1.json): eight fixture boss rows, family "02", whose decks hold all 33 cards. Rerun `run.sh fixture` after every encounters step.
- `suites\c-bosspilot-collect` (built by `make_bp_collect.py` from c-bosspilot-encounter): adds `PLAYER_CARDS`, which puts cards into the run player's match deck and counts the player's plays.
- `run.sh` steps: `bp` / `same` (collectA), `pbp` / `psame` (collectP1-4), `suite`, `build`, `fixture`, `harness`.

=====================================================================
5. RESULTS (final cycle, after the last engine change)
=====================================================================
Builds: engine 0 warnings / 0 errors (dll identical on rebuild); plugin 0 errors / 75 warnings; all suite builds 0 errors.

Generators: encounters 8 rows, 12 decks (4 authentic, was 2), 17 adaptations (was 19), 1 rejected. Whirt and Trap Room are now authentic; Voone's drum deck lists Fierce Monkey, his horn deck Festeroot Hulk and Foe Reaper. Ledger 4579 rows: 2755 implemented, 39 partial, 727 missing, 1058 duplicate/support, 0 adapted.

Suites, each run twice with 0 differing lines between runs:
| Suite | Result |
|---|---|
| c-content | 904 passed (0 lines differ from the family-01 log) |
| c-admission | 2367 passed |
| c-runlayer | 244 passed (0 lines differ) |
| c-acts | 181 passed |
| c-deckops | 174 passed |
| c-shrine-core | 131 passed (0 lines differ) |
| c-enc-model | 141 passed |
| c-enc-engine | 122 passed (0 lines differ) |
| c-collectibles-A | 115 passed |
| c-enc-secrets, c-catalog (run once) | 60 passed, 127 passed |

Bosspilot, all under the harness lock, each mode twice identical, stderr empty, 0 faults / 0 fallbacks / 0 failed checks / 0 swallowed leaf exceptions, power-unused 0:
- **collectA** (fair AI as boss, 8 fixture rows, 32 games): every one of the 33 cards was played by the boss at least once. Two more seed bases (70000, 123000), one run each, 32 games each: clean.
- **collectP1-P4** (run player holds the cards against Ivan, Trapped Room and Brandlemar piloted by the fair AI; 6, 6, 8 and 6 games): every one of the 33 cards was played by the run player at least once.
- **enc01** (the eight shipped rows, 20 games): boss wins 16 (baseline 15), form-unreached 1 as before. Most lines differ from the family-01 log, as expected from the changed boss decks and catalog-drafted player decks.

Fair-view checks that passed (c-collectibles-A FV1-FV6): the boss's view masks the player's hand and deck; Lazul offers nothing there; Duel! takes nothing from the player's deck there; the player's opening hand is absent and the boss's kept; the boss's own-deck ledger stays exact after Duel! and Mysterious Challenger.

=====================================================================
6. FOR THE INTEGRATOR TO REDO IN THE MAIN TREE
=====================================================================
1. Rerun encounters, catalog and ledger, then `p8f02\make_fixture.py` and `make_bp_collect.py`, and rebuild the two p8f02 suites.
2. Add to the shared scripts: c-collectibles-A (builds.sh / runs.sh) and the bp modes collectA and collectP1-4 (`run.sh` shows the exact environment).
3. Re-baseline the pins in section 3, the enc01 / secret4 / secretfix logs, the catalog-driven released bosspilot modes, and check selfplay and randplay.
4. Decide whether tag 32637 moves into the `EncounterRules.cs` table.

Not verified anywhere: the client's rendering of the Horror's text (it carries MODULAR_ENTITY_PART_1/2 as card and entity tags), GVG_101e shown as Lil' Exorcist's buff, and the Mega-Windfury tag on V-07-TR-0N. `docs\design\dungeon-run.md` was not edited, to avoid a three-lane conflict; the helper notes above are its content. No process of mine is running and no lock is left.
