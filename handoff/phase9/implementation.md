# Step 9: implementation record (implemented, untested)

Plan: `handoff/phase9/plan.md`. The user changed the scope to **implementation only**: the user runs every test,
measurement, calibration and re-recording. This file records, per plan item, what the code and data now do, what was not
done and why, the deviations from the plan, the uncalibrated values, the Windows-only edits and the existing checks and
pins the user should expect to move.

Branch `wip/s9` (worktree `/home/user/wt/s9`), from `wip/c25` at `f62275b`:
- stage 0 `2fdf018` (record: `handoff/phase9/stage0.md`);
- lane R `wip/s9-r` (`0c63a26`, review fixes `8b8cb35`), lane X `wip/s9-x` (`839a83f`, review fix `aa3be3f`), lane G
  `wip/s9-g` (`ed72e5c`), lane E `wip/s9-e` (`1735b13`), merged in that order with `--no-ff` (wave 9a: R, X, G; wave 9b: E
  plus the default flip, in the last merge commit).

What was checked (no suite or harness was run):
- engine build 0 warnings, 0 errors (md5 `da3fa601007fd83b3ce2cfa7e5899060`);
- `bash verify/bin/build-all.sh`: exit 0, all 127 lines clean;
- `gen.sh` twice, byte-identical; the catalog and ledger validators report 0 errors;
- every `make_fixture.py` and every `make_fNN.py` (F11-F24; F14, F16 and F17 from the saved wiki pages) twice,
  byte-identical, with no drift on the generators step 9 did not touch;
- the full validator with all seven data files (`DungeonRunValidator.Validate`, schedules and tavern included, through a
  scratch loader): 0 errors, and the ShapeDigest of every shipped schedule equals its ReleasedShapes pin;
- after the lane-S and close-out follow-up (on top of `cf824de`): the engine build 0 warnings, 0 errors (md5 unchanged);
  `build-all.sh` exit 0, all 127 lines clean; `gen.sh` twice byte-identical, validators 0 errors (only the ledger's
  curation hash moved).

## 1. Plan items

### Stage 0 (integrator): done, with two items left to the user

Done as recorded in `stage0.md` (seams, inert fields, `"version": 3` on the v3 schedules, tag reservation 32638-32640,
disposition table, row table). Not done (scope change): shared test code `verify/suites/p9/common/` (item 4) and the
stage-0 check run (item 6). `c-sched-9` (the D1 F1 tripwire) is not written.

### Stage 1a (lanes M and S): the tests are the user's; lane S's code items done

Both lanes are test and harness lanes (`c-bosspilot-run`, `bp9-before`, `LongRunWalker`, `c-longrun-9`, L4,
`record_replay_v3.sh`): the user's under the scope change. Lane S's three code items, done after the integration:

| Item | State |
|---|---|
| Version-rollback helper (D6) | done: `DungeonRunStore.Restore(save, snapshot)` (`DungeonRunState.cs`) puts back the profile, the run in progress and `Version` (not `LoadedVersion`, which only a successful write moves). The runtime's `Mutate` and `RecordFightOutcome` call it; `RecordFightOutcome` used to leave `Version` out. The call sites in `DungeonRunRuntime.cs` are [WIN] (not built here). |
| Resume refuses unknown ids | done: `DungeonRun.Resume` throws an `InvalidDataException` for an unfinished run whose `StarterDeck` or a `BossSequence` id the loaded data does not know; `DungeonRun.UnknownIdsError` builds the message, naming every unknown id ("Dungeon Run save: the run names starter deck X (its row is disabled) and boss Y, unknown to the loaded data; the run is not resumed."). A version-1 run (no starter deck id) passes; a finished run is not checked. The runtime's load path already catches and logs a Resume refusal ("Dungeon Run save preparation failed"), as for the schedule refusal before it. |
| "Save versions" in `docs/design/dungeon-run.md` | done: versions 4, 5 and 6 with their upgrades, the fields added under version 4 and 6 without a bump, `PowerChoices`, the step-9 guarded attributes, loading 1-6 and the backups, Resume's refusals and the rollback. |

### Stage 1b (derive the starting values): replaced

No measurement. Every value is the plan's stated initial guess or one derived from the data, listed in
`handoff/phase9/values.md` with its source (section 3 below).

### Stage 2, lane R (prevention, ordering, twists): done

| Item | State |
|---|---|
| 1 Ordering fix (`state.BoardShrine` before RollTwists and the first EnterNode) | done (`DungeonRun.NewRun`) |
| 2 Exclusions (the plan's whole list) | done in `schedule-v1.json` `rules[]`, each entry with an `exclusionReasons` text; new boss flags `attackRedirect` (Tierra), `charge` (both Candlebeards, Janak), `windfury` (Hesutu), `statSwap` (Icarax), `fatigue` (Baz'hial), `handAdd`, `manaIdentity`, `room`; rule keys `incompatibleTreasures` (ULDA_720 x LOOTA_800) and `noOpeningFlags` (ULDA_721) |
| 3 Run-scope candidates against every act's twists and bosses | done (`RuleCandidates`) |
| 4 `room` tags | done, on 5 rows (both Trap Rooms, the Vault, Tad, the Lava Chamber) |
| 5 DALA_857 | rechecked: stays rejected, reason final (D6 shares treasures player-side only) |
| D7 per-fight neutralisation plus "none" | done (`NeutraliseKnobs`, `ActWideModifierBans`, "none" at OpenModifier/PickModifier on stamp 4); v4 blocks and `modifierReasons` on Hagatha, Icarax, the other three lords, Tekahn, George and Karl, Ixlid, Tad, the Vault, Brimstone; empty `"v4": {}` on the four nemeses and the four Heist finale rows |
| D8 twist pick | done (`PickTwist`: weighted on stamp 4, uniform as before on released schedules); the pools are in `handoff/phase9/twist-pools.json` and placed in acts-v4h/v4t |
| D17 `ForbiddenPairs` | done (pure, order-independent; BuildPlan logs through `DungeonRun.ForbiddenPairLog`, never changes the plan) |
| C7 validator | done: a reason per exclusion entry from scheduleVersion 4; rule-rule lists symmetric or declared one-way (review fix); `modifierReasons` only for rows a stamp-4 schedule reaches |
| Tests `c-rules-9`, `schedule-twists9.json`, `pairs9`, `pairs.py`, the c-rules-06 W16 stored-offer assertion | not done (the user's) |

Review fixes on lane R (commit `8b8cb35`), all confirmed by the review:
- **D7 per knob.** `NeutraliseKnobs` now zeroes only the knob the row cannot carry: `bossHealthDelta` on a `fixedHealth`
  row, `bossManaDelta` on a row with `start.bossMana`. Open Road's extra opening card and Hot Start's +10 player Health
  reach those fights (the lane had zeroed all four knobs). The `modifierReasons` texts in F01, F08, F09 and `make_f24.py`
  say so.
- **C7 symmetry.** New validator check, from scheduleVersion 4.
- **Treasure exclusion floor.** `RuleExcludesTreasure` starts at `NextFightFloor` (a post-fight draft no longer scans the
  boss just beaten); `ForbiddenPairs` checks the next fight's boss on the same numbering. The gate covers every later
  boss (a treasure is held for the rest of the run); the predicate checks one fight at each step.

### Stage 2, lane E (economy and scaling): done

| Item | State |
|---|---|
| acts-v4h / acts-v4t at stamp 4 (v3's acts and node shape; v4t `sameActsAs` v4h) | done |
| D10 draw `provenance-mix-v2` (`MixedDrawV2`, exact integer weights, ledger lord chance, TRL cap, `MemberTuned`) | done |
| D4 act-aware scaling: per-act treasure rarity, per-fight finale curves, `tavern.weightByAct`, DALA_904 `maxPerRun` 2, `CapKnobs` (v4 `healthOffset`, optional `offsetCap`, HealthBonus cap) | done; no encounter row carries a v4 `tier` or `healthOffset` (stage 4 levers) |
| D5 variant (b): `bundleAfterTreasure: false` on acts II-III; a skip for room becomes +1 `BankedGold`, spent at the next tavern | done |
| D6 treasure audit: 125 generic rows `shared`, the rest scoped with reasons (catalog curation `step9-shared-treasures`, `step9-scoped-treasures`); slot rule (universal, own origin, weighted) | done; the integrator adds ULDA_111 (shared) and GILA_911 (scoped), so the shared list is 126 |
| D9 GILA_BOSS_43t as Damph's reward (`reward` row, encounter v4 `reward`, `GrantEncounterReward`) | done |
| A8 `upgradeEveryFloors` 4 | done |
| DALA_740 (D16) | rejected for good (criterion a: DALA_740e2 and DALA_740e3 have no text) |
| ULDA_607 | rejected for good (texts in `tavern-v1.json` and curation) |
| ReleasedShapes[4] | added by lane E (deviation: before wave 9b, because the validator refuses a stamp-4 schedule without one) |
| Tests `c-econ-9`, `c-sched-9`, the `treasure9` games, the stage-1b walker comparison of D5 (a) and (b) | not done (the user's) |

### Stage 2, lane X (match ends, restarts, finales): done, the 48t gate excepted

| Item | State |
|---|---|
| ULDA_111 Hearthstone (D14): Start of Game self-draw, unplayable unless `EscapeAllowed`, tag 32638, `RecordEscape` (Win plus Escaped, no credit or drafts, escape tavern on the node) | done |
| GILA_911 Rewind (D13): passive, tag 32639, `RecordRestart` (Fault plus Restarted, restaged, new seed for restartIndex > 0, consumed) | done |
| GILA_BOSS_48t restart path (`RestartCarriedDamage`) | done; the card stays out of Toki's deck and its admission stays rejected until the stage-3 gate (real games: the user's) |
| Per-source caps, 2 x TurnCap budget, seed rule | done |
| D11 Tekahn ratchet and TurnCap | done in the engine; the integrator wired `PlanMatchEnds`/`TurnCapFor` to the act's `turnCap`/`formRatchet` keys (lane E's acts-v4t finale: 150, true), with lane X's relay rule as the fallback |
| Final rejections and texts: GILA_900p / GILA_900h2 (Toki keeps CS2_034), Jythiros blocker "client", ULDA_607, the "phase 9" lift texts | done (the integrator also rewords Toki's `run-v1.json` source text) |
| [WIN] NativeMatch TurnCap from the plan and the `:6634` null guard; ClientBridge turn limit; LocalLadder restart as a Fault | edited, not built |
| Tests `c-ends-9`, bp mode `ends9`, the harness reading `plan.TurnCap`/`FormRatchet` | not done (the user's; notes in `handoff/phase9/lane-x.md` section 6) |

Review fix on lane X (commit `aa3be3f`): `RecordRestart` refuses a restart from a source the fight could not use
(`RestartSourceAvailable`: Rewind held, or GILA_BOSS_48t in this fight's boss deck or a form's deck), as `RecordEscape`
re-checks `EscapeAllowed`; it is recorded as the loss it is, with a note.

### Stage 2, lane G (generation hygiene, engine leftovers): done

| Item | State |
|---|---|
| D15 `OfflineCardPools.ForGame` at every `LegacyGenerationV1.Get` site (AddLackeyTask, DiscoverTask x5, ReplaceTask, AdaptTask, PotionGenerating, SpecificTask, BuildABeast, F16) and the Cards.cs pools (RandomMinionTask, RandomMinionNumberTask, TransformRandomTask) | done; unfiltered games get the same array |
| Generated unscripted list (`DungeonRunAdmission.UnscriptedGenerated()`, 230 ids, computed at load) | done; GeneratedRejects 670 -> 883 (legacy), 660 -> 873 (catalog) |
| ULD_616 Titanic Lackey, DRG_052 Draconic Lackey | scripted; LackeyPool holds 7 |
| Kingsbane Durability | fixed (copies enchantment Durability) |
| Typhoon `InterceptLeave` | nothing to change (already there since `a95b97e`) |
| Tests `c-gen-9` | not done (the user's) |

### Stage 3 (integration): done, the regression excepted

- Merges R, X, G, E with every side kept (conflict notes in the merge commits).
- `schedule-v1.json` `default` acts-v4h, `defaults` [acts-v4h, acts-v4t]. The twist pools of `twist-pools.json` were
  already in lane E's schedule objects and match it.
- ReleasedShapes[4]: acts-v4h `09B43E71F9440039`, acts-v4t `A866AD976CEBBDC8` (equal to v3, as D2 allows), the
  ShapeDigest of the shipped objects, computed the way family 01 and the close-out did (`DungeonRunSchedules.ShapeDigest`
  of the parsed schedule).
- `run-v1.json`: lane X's ULDA_111 and GILA_911 rows (`runs: "v4"`; rarity "rare", lane X's suggestion, since the client
  data has none).
- Restoration ledger: the eight twist `unreachedReasons` are dropped (they roll on the defaults now); the ledger tool
  offers an encounter's v4 `reward` (`encounter reward:gila_43_damph`); blocker texts updated.
- Regenerated: make_f11/14/17/18/21/23/24, `gen.sh`, every `make_fixture.py` (the 11 encounter fixtures and the four
  schedule fixtures p8f05/06/18/24). The p8f05/06 generator asserts ("three empty twist pools in acts-v3h") still hold,
  since acts-v3h is unchanged, so the generators are not edited.
- Not done: the full regression and every re-record (the user's).

### Stage 4 (calibration loop): not done

The user's: `slots9`, `outliers9`, `finale9`, `tekahn9`, `longrun9`, `check_targets.py`, lane E2's encounter v4 blocks and
curves, and the GILA_BOSS_48t gate.

### Stage 5 (close-out): partly

Done: ledger regenerated with recomputed counts; HANDOFF sections 2 and 3 updated. Done after the integration:
- the "Step 9: the economy" section of `docs/design/dungeon-run.md` (where it applies; R0 and the slot decomposition as the
  target, replacing F7 on the stamp-4 schedules, which F7 now says; the economy targets; the D-table as implemented; the
  values, pointing to `values.md`);
- the curation notes `twists-per-act`, `plague-twists-per-act` (the v4 chapter mapping, chance 100, one twist per act, an
  excluded roll rolls none; v3 keeps its empty pools) and `health-policy` (identity offsets; the v4 healthOffset override,
  offsetCap, the HealthBonus cap, per-fight neutralisation and Tekahn's form ratchet, all on stamp 4 only); the ledger is
  regenerated with them;
- HANDOFF 3.4: the stale "BOT_543 and ULD_152 partial" note is replaced (both implemented in the ledger; ULD_152's
  named-game recheck stays the user's); Hagatha's "506 of 1000" is replaced by what the data shows (one of five Monster Hunt
  rows of the act II major pool, picked uniformly within its provenance, so at most one run in five; the plan's estimate is
  about 118 of 1000, not re-measured); HANDOFF 3.3's left-over list no longer names these items.

Not done: the three-lens review, README section 4 numbers, the tag ranges in HANDOFF 5.

### Section E open items

| Item | Outcome |
|---|---|
| 8 twist pools | implemented on v4 (defaults now): ledger implemented, `run:twist-pools` 8 -> 0 |
| ULDA_111 Hearthstone | implemented (Escape), `runs: "v4"` row; ledger implemented |
| GILA_911 Rewind | implemented (Restart), `runs: "v4"` row; ledger implemented |
| GILA_BOSS_48t | restart path implemented; deck and admission wait for the user's gate (ledger partial, `AlternateTimelineLift`) |
| GILA_900p, GILA_900h2 | final rejection, texts reworded |
| ULDA_607 | final rejection |
| DALA_740 | final rejection (D16 a) |
| ULD_152 | implemented in the ledger; stale "partial" note removed from HANDOFF 3.4; the named-game recheck is the user's |
| BOT_543 | implemented in the ledger (Tekahn's relay deck); stale "partial" note removed from HANDOFF 3.4 |
| GILA_BOSS_43t | implemented as Damph's reward; ledger partial for its boss-card role only (offered: encounter reward) |
| Jythiros | blocker "client" |
| ULD_616, DRG_052 | scripted |
| Random generation | filtered in Dungeon Run games (D15) |
| Typhoon, Kingsbane | Typhoon already fine; Kingsbane fixed |
| Rumble teammate slots, ACT_RUN label | final reason (stage 0) / the user's |
| DALA_857 | stays rejected, final reason |

## 2. Deviations from the plan

| # | Where | Deviation | Reason |
|---|---|---|---|
| 1 | `schedule-v1.json` rules, `DungeonRunData`, Validator | C7's reasons are a new rule key `exclusionReasons`; required only from scheduleVersion 4 | released files and fixtures keep validating |
| 2 | rules, `DungeonRunTavern.Applicable` | ULDA_721 vs opening-flag cards is a new rule boolean `noOpeningFlags`; the tavern sets no opening flag while it is active | no existing key expressed it |
| 3 | Validator | `handAdd` and `manaIdentity` are checked one way (flag needs data) | fixture rows lack the flags |
| 4 | `DungeonRun.Rules.cs` | a v4 row without a v4 block keeps its act-wide ban | safe fallback instead of a silent no-op |
| 5 | `DungeonRunData` ReleasedShapes | [4] added by lane E, not in wave 9b | the validator refuses a stamp-4 schedule without it |
| 6 | Validator | "runs v4 needs scheduleVersion 4" fires only when schedule-v1.json is loaded | tools that load run-v1.json alone (gen.sh) have no schedule |
| 7 | `DungeonRun.Economy.cs` | D9's note at deckMax is not surfaced (GrantEncounterReward has no notes channel); the +1 BankedGold happens silently | stage-0 signature |
| 8 | run-v1.json | GILA_BOSS_43t has no content entry; the row carries `reward: true` and the validator checks the card itself | the card is the boss card |
| 9 | schedule finale | `turnCap` and `formRatchet` are schedule keys of the finale block (lane E); lane X's relay rule is the fallback | one source of truth in the data |
| 10 | Engine, run layer | escape and restart are new marks (`RunMatchPlan.Restarts`, `GameConfig.MatchEnds`); Alternate Timeline ends the engine game as a tie; a restart is accepted on any outcome but a concession | engine ends these games itself |
| 11 | `RecordRestart` | a persistent boss's damage is banked and PlagueLordDamage is then zeroed (the plan said "keeps PlagueLordDamage") | the banked remaining already counts it; keeping it would count it twice |
| 12 | `RecordRestart` | a refused restart (cap, spent budget, or a source the fight could not use) is recorded as a loss | no silent restage |
| 13 | lane G | the unscripted generated list is computed at load, not a gen.sh file; Kobold Hermit's AT_132_SHAMANa-d stay exempt; the Entourage sites and BasicTotems/JadeGolem are not routed | fixed lists of scripted cards; mapping Kobold Hermit would change every game with it |
| 14 | ledger | GILA_BOSS_43t stays partial (its boss-card role: no boss deck holds it) | the ledger's reach rule needs both roles |
| 15 | lane R | `room` on five rows (both Trap Rooms) | stage 0's table lists both |

## 3. Uncalibrated values

All in `handoff/phase9/values.md` with sources; read only on stamp 4 (`DungeonRun.Tuned`):
- draw v2: alpha 0.7, k 1, `ledgerMajorChance` 50, `maxTrlPerAct` 1;
- `upgradeEveryFloors` 4; treasure rarity I 26:74, II 39:61, III 53:47; `bundleAfterTreasure` false on acts II-III;
- act and finale curves: v3's values (Heist finale [70,70,70,70] / [3,3,3,3]);
- twist chance 100 on acts I-III and the v4h finale (DALA_830); twist weights 1;
- tavern `weightByAct` 3 for every option, DALA_902 [3,3,5]; DALA_904 `maxPerRun` 2;
- Tekahn finale `turnCap` 150, `formRatchet` true; restart budget 2 x TurnCap; 1 restart per source per floor;
- BankedGold +1 per skipped bundle or reward with no room;
- ULDA_111 and GILA_911 rarity "rare".

## 4. Windows-only edits [WIN] (not built here)

- `plugin/NativeMatch.cs`: `DungeonRunTurnLimit` reads the plan's TurnCap; the `next.Choices?.Contains(id) == true` guard.
- `plugin/ClientBridge.cs` `TickDungeonRun`: the turn limit uses `match.DungeonRunTurnLimit`.
- `plugin/LocalLadder.cs` `FinishDungeonRun`: a harvest marked `Restarted` is recorded as a Fault.
- `plugin/NativeDungeonRunUI.cs`: the modifier page shows the v4 "none" row (text in `NativeDungeonRunText.cs`).
- `plugin/DungeonRun/DungeonRunRuntime.cs`: sets `DungeonRun.ForbiddenPairLog` to the runtime log; `Mutate` and
  `RecordFightOutcome` roll back through `DungeonRunStore.Restore` (lane S; `RecordFightOutcome` now restores `Version` too).
- Still to build in the client: the escape screen and escape tavern, the Rewind flow, the neutralised-modifier text, BankedGold
  at the tavern, the 150-turn relay cap check, and the plugin build itself (75-warning set).

## 5. Existing checks and pins expected to move

Engine and generation:
- the engine md5 pin: `fcbddbaeb4862b95ca20f5b11299c7b0` -> `da3fa601007fd83b3ce2cfa7e5899060` (lanes X and G);
- GeneratedRejects / ExcludedGenerated size or content pins (+213 ids; lane G);
- c-enc-21's LackeyPool 5 -> 7; c-enc-19 / c-enc-21 S8 if they pin ULD_616 or DRG_052 unscripted; hall chunks
  (Chunk10Priest, ShamanChunk12) only if they play a generated Lackey;
- selfplay / randplay digests only for games that generate ULD_616/DRG_052 or shuffle back an enchanted Kingsbane;
- every run-game record that generates from the legacy pools (rules05/06, cross05/06, sweep, finale, tekahn bp records,
  golden game lines).

The default flip (wave 9b):
- `golden-sequences.txt`: the default newrun lines follow acts-v4h/v4t and draw v2;
- c-enc-25 B1/B2/B7/R (ledger counts and blockers; B7's list of never-rolled twists is empty now and the HANDOFF 3.3
  paragraph it reads is reworded; R's reachability now walks v4 with draw v2);
- c-w5fix Q7-Q11 (default names), c-enc-model J1 and G8-G13 (v4 modifier offer with "none", new flags);
- bp records on the defaults (sweep25 a/b/c, the unnamed finale25);
- c-acts:107 pins `ScheduleVersion == 3` (now 4); c-enc-model F1, c-shrine-core, c-w5fix, c-enc-24, c-enc-25 where they pin
  the file version, the schedule list or ReleasedShapes.

Released schedules (v3h/v3t) move only through correctness fixes (lane R):
- anomaly offers on shrine runs at act I (the ordering fix), and the new exclusions against act bosses (ULDA_709 or
  DALA_831 with Icarax, ULDA_714 with a Plague Lord, ULDA_715 with Godfrey or Darkness, ULDA_710/711 with the charge and
  windfury rows, DALA_859 with Tad, the Vault, Hagatha or Brimstone, ULDA_721 after an opening flag);
- tavern offers while ULDA_721 is active (no opening-flag options); treasure drafts while ULDA_720 is active (no LOOTA_800);
- so: c-enc-25 L4 anomaly lines (when written), c-rules-05 T16, c-rules-06 W16/W23, the rules05/06 and cross05/06 bp rows,
  and any default-run golden line with an affected anomaly offer (now moot for default lines, which follow v4).
- c-enc-25 L1 should not move (legacy runs have no rules; save version unchanged); tekahn24/25 should not move (ratchet and
  TurnCap are gated to stamp 4).

Data and text pins:
- `encounters-v1.json` (255 rows, 296 decks unchanged): text and hashes (flags, `room`, `modifierReasons`, v4 blocks,
  F-file hashes); F11, F14, F17, F18, F21, F23, F24 and hand-kept F01, F07, F08, F09;
- the 11 encounter fixtures (p8f02/03/04/08/09/10/11/14/16/17/23) and the four schedule fixtures p8f05/06/18/24 (now the
  shipped file with the v4 schedules and the v4 defaults; p8f24's schedule-tekahn.json is the shipped file byte for byte);
- `run-v1.json`: 268 treasures (was 265), new keys `shared`, `reward`, `runs: "v4"`; `tavern-v1.json` new keys
  `weightByAct`, `maxPerRun`; `schedule-v1.json` scheduleVersion 4, defaults, the v4 schedules, the rule keys;
- ledger: 4970 rows; implemented 3880 -> 3890, partial 10 -> 3 (GILA_BOSS_43t, GILA_BOSS_48t, GILA_BOSS_99t), missing
  13 -> 10, duplicate/support 1067; blocked counts: `run:twist-pools` 8 -> 0 and `ai:start-of-game-draw` 1 -> 0 (both rows leave
  the blocker table), `ai:turn-rewind` 2 -> 1, `engine:unscripted` 11 -> 8, `run:treasure-content` 3 -> 1; the inputs table hashes
  (catalog, encounters, run, schedule, tavern, curation); DALA_830's row now reads "acts-v3h act IV (schedule not a
  default yet), acts-v4h act IV";
- admission and deferral pins (lane X): c-admission (292-293, 378), c-deferrals (163, 607), c-enc-model (730), c-enc-23
  (198), c-enc-21 (695): ULDA_111/GILA_911 now admitted, `DeferredToPhase9Ids` is GILA_900p only, new `MatchEndIds`, the
  TurnRestartLift / MatchExitLift / RiddleLift texts, 48t's `AlternateTimelineLift`, family counts (turn-rewind 1, new
  game-restart);
- ULDA_607 / DALA_740 exact-text pins (c-enc-25 checks "Bombastic" and "data-gap", which still match).

Lane S's code items and the stage-5 documents (after the integration):
- `docs/design/restoration-ledger.md`: the inputs table's `curation.json` hash only (`e4fdfb1152c9cfab` -> `8f8bba3bd14d9237`;
  the three rule-adaptation notes are not printed in the ledger); any pin of that hash or of curation.json's bytes;
- c-enc-25 B7 reads HANDOFF 3.3 (it already moves with the twist pools: 8 idle twists -> 0); nothing else reads HANDOFF;
- Resume's refusal moves no check that resumes a run made with the data it loads (c-runlayer, c-acts, c-upgrades,
  c-shrine-core, c-shrine-upgrades, c-teammates-T1, c-enc-model, c-enc-25 L1). Every StarterDeck and BossSequence id in the
  fixture saves under `verify/fixtures` (37 files) is in the shipped data; a check that resumes a save whose bosses are
  encounter rows while loading data without `encounters-v1.json` would now get the refusal (none was found, not run);
- a new check for D6 (the rollback restores Version) and for the Resume refusal is the user's (c-longrun-9, c-enc-model).

## 6. Follow-ups found during integration

- Lane S's three code items (section 1, stage 1a): done after the integration.
- D9's deckMax note has no channel (deviation 7).
- Kobold Hermit's AT_132_SHAMANa-d choices lack race and keywords (lane G deviation; a follow-up).
- HANDOFF 3.4 stale lines (Typhoon, Kingsbane, Lackeys, ULD_152/BOT_543) are updated in place where step 9 closed them;
  the close-out removes the rest.
