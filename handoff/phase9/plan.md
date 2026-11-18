# Step 9 plan (revised): one reward economy, act-aware scaling, prevention, long-run validation

Base: `/home/user/wt/c25` at `f62275b` (`wip/c25`). Until work starts in a new worktree, everything here is read-only. That worktree branches from the step-8 tip once its regression is merged. I re-checked these critique claims against the tree:
- the schedule `version` keys (`schedule-v1.json:20,48,79` carry `"version": 2`; acts-v3h and acts-v3t at `:93,:128` carry none; `DungeonRunData.cs:1250` defaults the stamp to the file version)
- the Version printed and digested in `Replay.cs:110,141`
- `OfferedOn` (`DungeonRunData.cs:199`)
- `SwapPowerForm`, used by Belnaara (`F14.cs:141,158`) and Bazhial (`F21.cs:290`); their forms are `at.script`, while Tekahn's is `at.healthAtMost 200` with no own hero or Health
- `record_replay.sh` and `Record.cs:37`
- the explicit passthrough at `Encounters.cs:243`
- where the encounter rows live, and which F-files have a `make_fNN.py`
- the bosspilot `LocalBot` and the `game.Turn <= 100` loop
- the `RemoveAll(r => r.Kind != null)` call
- the `LegacyGenerationV1.Get` call sites (9 files)
- `LackeyPool`'s `card.Implemented` filter and the TODO stubs for ULD_616 and DRG_052
- GILA_BOSS_43t, which is a boss-card spell (`F11.cs:182`)
- `MixesProvenance`
- ULDA_607 in `tavern-v1.json` `deferred`
- the `NoLoss` attribute pattern (`DungeonRunState.cs:116`)
- `RecordActWin` (`DungeonRun.cs:1012-1027`)
- HANDOFF.md:265-271

---

## 0. Ground rules

- **Released schedules are frozen, and so is their tuning.**
  - acts-v1, acts-v2, act1, acts-v3h and acts-v3t keep their acts, curves and keys.
  - Stage 0 adds one thing: an explicit `"version": 3` on the acts-v3h and acts-v3t rows. Without it, raising the file to `scheduleVersion` 4 would re-stamp both as 4 (`DungeonRunData.cs:1250`). `Tuned(state)` would then apply v4 tuning to v3h runs, and ReleasedShapes[4] would refuse them.
  - Step 9 adds **acts-v4h / acts-v4t** at stamp 4 and flips `defaults` to them.
- **Every new tuning value is read only through `DungeonRun.Tuned(state)`**, which is `state.IsActRun && state.ScheduleVersion >= 4`. That includes:
  - schedule, tavern and treasure keys
  - modifier semantics and the draw
  - **encounter-row tuning**: tier, `healthOffset` and `incompatibleModifiers` move only through a per-row `"v4": {…}` override block. `HealthOffset` (`DungeonRun.cs:640-642`) and `PoolOf` read the shared rows live, so a plain row edit would reach v3.
  - new treasure rows, through a new `runs: "v4"` value (an act run with stamp ≥ 4; refused in a version-3 file)
  - match-plan knobs (`TurnCap`, `FormRatchet`)
- **Correctness fixes are not frozen.** The rule exclusions, the new boss flags and the NewRun ordering fix apply to every schedule that has anomaly or twist nodes (v3 and v4; acts-v2 has none). Every line they move is attributed.
- **The save stays at version 6.** New fields are attributes guarded by `ShouldSerialize`, and no new enum member is written. Escape is `Outcome=Win` plus `Escaped="true"`, following the `NoLoss` precedent. Restart is `Outcome=Fault` plus `Restarted="true"`. This replaces the old D12 (see section 7).
- **Compile-unit discipline.** `DungeonRunData.cs` and the Validator are compiled alone by the catalog tool and by 39 hall harnesses, so they only parse and validate raw values, with the stamp passed in as an int. `Tuned`, `KnobsFor`, `ForbiddenPairs` and every v4 predicate live in `DungeonRun.*.cs` partial files. `DungeonRunRuntime.cs` is in no Linux suite, so its logic moves into testable helpers and the file itself is [WIN]-built.
- **Generated files are never hand-edited.**
  - F01-F10 are hand-kept JSON.
  - F11, F12 and F14-F24 are written by `verify/tools/gen/p8fNN/make_fNN.py`.
  - After either kind of edit: `gen.sh`, then the fixture generators.
- Nothing is pushed to the public repository. Items that need the Windows client are marked **[WIN]**.
- **"One schedule".** v4h and v4t share acts I-III (`sameActsAs`), so the economy is tuned once on the shared acts, and only the finales differ. Every economy and difficulty metric is reported pooled and per schedule.

---

## 1. Goals and acceptance criteria

Terms:
- **Loadout**: a hero row × a starter deck. There are 116: Monster Hunt 4, Heist 36, Rumble 27, the classes 33 and Tombs 16. v4t has 112, because DALA_Tekahn is excluded by FinaleExcludes.
- **Walk**: the deterministic run-layer walker from lane S, with no engine games.
- **Fair**: the new run harness (lane M) with the fair AI on the player side as well.
- **Seed bases**: S1 is the calibration base and S2 is the held-out base. Final acceptance runs on S2 only.

### A. Economy (deterministic; suite `c-econ-9`)

The run set is every loadout × {v4h, v4t} × 6 seeds, all wins, with the scored pick policy and the walker buying every tavern offer it can afford.

| # | Criterion | Today (v3h) | Target (v4) | Source of the target |
|---|---|---|---|---|
| A1 | Deck never exceeds deckMax | holds | 0 violations | invariant |
| A2 | Deck size entering the finale, median / p95 | ~45-49 / 50 | median 34-42, p95 ≤ 48 | original 31 (R5/F4) plus the combined-mode extras (≤ 8 treasures, 4 teammates) |
| A3 | Bundle drafts skipped for room, per run | >0 in late act III | median 0, p95 ≤ 1; every skip converted to banked gold | D5 |
| A4 | Treasure-draft candidates per **loadout** | 67-69 (class and Rumble) vs 99-112 | ≥ 90 for every loadout that has treasure drafts | parity with the adventure rows |
| A5 | Adventure loadouts: share of non-passive offers holding ≥ 1 own-adventure treasure | ~0.8-0.9 per offer | ≥ 95% | D6 slot rule |
| A6 | Rare share of offered treasures by act, **pooled over audited offers**, with per-origin shares reported separately | flat 30% weight | I 25-35%, II 38-50%, III 52-65% | per-act weights computed from the audited pool's rare/common counts, not fixed at 70/30 |
| A7 | DALA_904 buys per run | up to 6 | ≤ 2 | D4 |
| A8 | charm_seedling: floors from pick to full growth | 4 | 8 | `upgradeEveryFloors` 4 on v4 |
| A9 | Tavern offer share, Heist : Tombs | 74 : 26 | 60-68 : 32-40 | the 14 Heist : 8 Tombs options at equal weight give 64:36 |
| A10 | Per-row meeting ratio (max/min) within each tier pool, 600 default-roll runs; median distinct provenances per run | ~4.2; 6 | ≤ 2.5; ≥ 5 | D10, checked in `sim.py` before it is fixed |
| A11 | c-enc-25 R reachability on v4 defaults | every row met | every row met at least once | unchanged rule |
| A12 | Per-origin reward counts per run (treasures, teammates, shrine upgrades, signatures, banked gold) | not measured | reported; input to B11 | critique |
| A13 | Death Knight and Demon Hunter: distinct catalog entries per run | not measured | reported; follow-up if < 10 | risk 10 |

### B. Difficulty (measured; `verify/tools/balance/check_targets.py`)

There is **one run-level target**. The per-slot targets are a decomposition of it, and their product is checked against it. This is the F7 method (`dungeon-run.md:270`) extended to 16 and 19 fights.

- **R0, run target**: a fresh-profile Victory rate of **1-3%** on each schedule. It is computed as the product of the measured slot rates, weighted by the draw's lord / non-lord share for the act III major.
- At the point targets below, the product is about 1.2% (v4h) and about 1.4% (v4t).

Acceptance and sampling:
- Every band needs **n ≥ 200** (95% CI about ±5-7 points).
- A band passes when its Wilson 95% interval overlaps the band and its point estimate lies within the band ± 3 points.
- Calibration runs on S1; acceptance runs on S2.
- A miss sends the work back into stage 3 rather than failing the regression.
- Every record must still be twice-identical.

| # | Slot | Point target | Band |
|---|---|---|---|
| B1 | Act I combat, per fight (floors 1-4) | 0.92 | 0.88-0.95 |
| B2 | Act I major | 0.70 | 0.60-0.80 |
| B3 | Act II combat / major | 0.88 / 0.60 | 0.83-0.92 / 0.50-0.70 |
| B4 | Act III combat / major (custom finals) | 0.84 / 0.55 | 0.78-0.89 / 0.45-0.65 |
| B5 | Plague Lord, act III major | mean damage per attempt ≥ 60; one-attempt kill 10-25% | — |
| B6 | Heist finale | 0.80 per fight; all four in one run ≥ 0.35 | 0.72-0.87 per fight |
| B7 | Tekahn finale (n = 100) | 0.45 | 0.35-0.55; turn caps ≤ 3 of 100 |

| # | Other criteria |
|---|---|
| B8 | Ordering. Act I combat beats act III combat (one-sided test, p < 0.05). Adjacent acts are ordered by point estimate, with a reversal of ≤ 3 points tolerated. |
| B9 | Outliers are **sampled by name**: every t3/t4 row plus the 17 named outliers, n = 20 each, on their act's combat slot. Each is flagged if its CI lies entirely above 0.98 or entirely below 0.45. Rows tagged `room` (Vault, Tad, Lava Chamber, Trap Room) are exempt and reported as reward nodes. |
| B10 | `longrun9` (240 runs, every fight played, stratified by origin). The run-depth distribution must match the depth predicted from the measured slot rates (each floor's survival within its CI). There is no Victory count requirement; the Victory path is a deterministic case in D1. |
| B11 | Per-origin balance. The fair-player rate of reaching the act II major, per origin (Monster Hunt, Heist, Rumble, Tombs, classes; n ≥ 40 each from `longrun9`), is within ±15 points of the pooled rate. |

Estimated cost per calibration round, at the measured 3.1 s per game and 38 s per Tekahn game: about 7 hours (slots ~1.6 h, B9 ~2.2 h, Tekahn ~1.1 h, longrun ~2 h). Three rounds plus the S2 acceptance come to about 28 hours, run one harness at a time on a quiet host.

### C. Incompatibility prevention (`c-rules-9`, plus invariants in `c-longrun-9`)

| # | Criterion |
|---|---|
| C1 | **0 forbidden pairs** in any reachable state across every lane-S walk. `ForbiddenPairs(state)` is a pure predicate covering twists, the act anomaly, run-scope anomalies, modifiers, the act's boss flags and rules, the run class, the fight-only hero classes, treasures, the board shrine and the run deck's opening-flag cards. It is **order-independent**: run-scope candidates offered in act I are checked against every act's rolled twists. |
| C2 | 27 Rumble shrine decks × 200 seeds: 0 stored act I anomaly offers and 0 rolled twists carrying `noBoardShrine` (the ordering fix) |
| C3 | Liveness on v4: every modifier node offers ≥ 1 modifier plus "none"; 0 forced single picks |
| C4 | Every modifier knob × every major, finale and fixedHealth row: the knob is visible in the built plan, or it is declared neutral with a reason. 0 silent no-ops. |
| C5 | Each of the 8 twists rolls in ≥ 15% of 2000 new v4 runs. The per-act roll rate equals the chance less the measured exclusion loss, ± 3 points. DALA_830 rolls 100% on the v4h finale. |
| C6 | **Pairwise** covering arrays over {twist, act anomaly, modifier, boss flag set, hero-switch class}, plus triples only for the declared hot spots (Stench × Marin, Coffers × No-Face, Gorged × Vault/Tad, Battle Scars × LOOTA_800, Carts × Hagatha). Each tuple is either a declared exclusion with a reason, or one real game with 0 faults, 0 fallbacks and 0 swallowed exceptions (`pairs9`, using `RULES=id,id` injection in the new harness). |
| C7 | Validator negatives for every new key. Every exclusion entry needs a reason. Rule-rule lists are symmetric or declared one-way. `modifierReasons` is required only for rows reachable on a stamp-4 schedule, so fixtures that hold no v4 schedule are untouched. |

### D. Long-run and save/resume (`c-longrun-9`, c-enc-25 L1/L4, c-enc-model)

| # | Criterion |
|---|---|
| D1 | Every loadout × {v4h, v4t} × 2 seeds, plus 22 rows × 10 seeds. Each is walked to Victory (seeded scripted wins, ≥ 1 per schedule) or to a scripted Defeat. After **every** step: Save, Load, Upgrade (no-op), Resume, then continue on the reloaded object. The digest sequence, which includes the profile, equals the unbroken twin's. |
| D2 | A crash at InMatch on every fight floor (including hero-switch, relay, Plague Lord and restart floors) restages identically. The allowed differences are Attempt and the documented PreMatch HeroSwitch quirk. |
| D3 | Synthetic `RunMatchEnd` values move every match-fed field: BossDamage, BossRemaining/BossForm, HeroesUsed, HeroDamage, PlagueLordDamage, Escaped, Restarted, RestartCarriedDamage. Three consecutive runs carry lord damage. |
| D4 | Card legality at every step: PoolCardFailures, runes and opening flags give 0 failures beyond the three documented exemptions. BuildPlan never throws at PreMatch, and the NativeMatch limits always hold. |
| D5 | c-enc-25 L1: all 341 lines byte-identical (there is no version bump). The new **L4** replays v3h/v3t saves recorded at `f62275b`. Each moved line is attributed to a correctness fix; anomaly offers only are expected. |
| D6 | The new attributes round-trip (c-enc-model F4/F5). A v6 file with them loads. The existing version-7 refusal (c-shrine-core V4) stays. `RecordFightOutcome`'s rollback restores Version, via a helper that a suite tests. |

### E. Deferred content and open items

| Item | Outcome |
|---|---|
| 8 twist pools | implemented on v4 (B7: 8 never-rolled → 0) |
| ULDA_111 Hearthstone | implemented (Escape), v4-gated row |
| GILA_911 Rewind | implemented (Restart), v4-gated row |
| GILA_BOSS_48t Alternate Timeline | implemented if the stage-3 gate passes; otherwise rejected with a final reason |
| GILA_900p Temporal Loop, GILA_900h2 | **final rejection**, recorded as a SoloStone adaptation: Toki keeps the CS2_034 stand-in, with the reason "NativeMatch has no client history re-sync". All ~30 "phase 9" lift texts in curation, admission, F01/F21/F23 and the deferrals are reworded. A [WIN] lane may reopen it later; that is not step 9. |
| ULDA_607 House Special | **final rejection**: there are no source decks, and authoring a Bombastic deck for each of 15 class sets is new content, not a one-line effect like VIP. `tavern-v1.json` `deferred` text updated. |
| DALA_740 | rechecked with the D16 criterion; implemented as a v4 adaptation or rejected with a final reason |
| ULD_152 | rechecked in named games (`ulda_01_cardish`, `ulda_44_trap_room` boss decks, Brann buckets); the stale "partial" in HANDOFF 3.4 is removed |
| BOT_543 | the stale "partial" note is removed (the ledger has it implemented) |
| GILA_BOSS_43t | implemented as Damph's reward (D9) |
| ULDA_BOSS_58p Jythiros | blocker moved from "phase 9 (economy)" to "client", with its reason |
| ULD_616, DRG_052 Lackeys | scripted with tests; the Lackey pool has 7 cards |
| Random generation | 0 unscripted ids generated in Dungeon Run games (`c-gen-9`) |
| Typhoon `InterceptLeave`, Kingsbane Durability (Shivers), Rumble champions' teammate slots, bosspilot ACT_RUN label, DALA_857 admission | each gets a disposition row in stage 0: fixed in a named lane, or given a final reason and owner |

### F. Non-regression

- c-acts (acts-v2) is unchanged.
- c-enc-25 L1 is unchanged.
- The selfplay and randplay digests are unchanged, unless the games generate ULD_616 or DRG_052 (scripting them changes their effect). Lane G counts this in a scratch copy first and attributes any move.
- Every bp mode runs twice identical with 0 faults, fallbacks or swallowed exceptions.
- Every moved line is attributed (section 4).

---

## 2. Design decisions

| # | Decision | Rejected alternatives and why |
|---|---|---|
| D1 | **New ids acts-v4h / acts-v4t** at stamp 4 carry all tuning. acts-v3h/v3t get an explicit `"version": 3`. Tripwire `c-sched-9` F1 checks four things: StampVersion == 3 for both v3 rows; hashes of the parsed released schedule objects, minus `version`; the v3-visible tavern and treasure fields; and the encounter rows minus their `v4` blocks. | *Retune v3 in place*: tuning is read live, so saved runs change mid-run. *Snapshot tuning into each save*: a large new save field. |
| D2 | **v4 keeps v3's node shape.** ReleasedShapes[4] pins v4h/v4t; the digests may equal v3's. The scaling is in tuning keys and the encounter `v4` blocks. | Moving t/p/m/u flags: doubles the review surface. Revisited only if B misses after stage 3. |
| D3 | **Measure with a fair player on both sides**, in a **new** harness `verify/suites/p9/c-bosspilot-run/`. It shares a factored `PlayGame` with the old harness, which stays byte-identical. The player pilot is built per fight from the run deck: an `AutoDeckPlan.Derive` deck plan, a prior over the boss's Secrets, and hero-switch and relay play. It lives in the harness, not in `plugin/`, so it also builds against an `f62275b` export. | *Env flags on the old harness*: its control flow (ReachFloor auto-wins and overrides `BossSequence`, LocalBot, literal cap 100) cannot express a long run. *Tune against LocalBot*: its rates are lower bounds. |
| D4 | **Act-aware scaling through act keys and encounter `v4` blocks.** Per-act curves, per-fight finale curves (C12c), per-act treasure rarity computed from the audited pool, `tavern.weightByAct`, and DALA_904 capped at 2. Player Health stays 30. **Identity offsets are kept** (design D8: +10 for Dungeon Run finals and nemeses). The order of levers for the act I major wall: (1) the floor-5 curve value; (2) a v4 tier override moving the LOOT finals (the original floor-8 final bosses) to major2; (3) `offsetCap`, only as a last resort, recorded as an identity change in curation `health-policy`. | *Player Health curve*: changes hero identity. *offsetCap 0 by default*: silently removes the finals' designed +10. |
| D5 | **Deck growth.** deckMax stays 50, and a bundle skipped for room becomes **+1 `BankedGold`**, spent at the next tavern. Two variants are compared on the walker in stage 1b: **(a)** takes [3,2,1]; **(b)** full-size bundles (3/2/2) but no bundle after treasure-draft floors and majors in acts II-III (a per-act key `bundleAfterTreasure: false`). Preference is (b), because it keeps the bundle draft's identity; (a) is used only if (b) misses A2/A3. DALA_902 weightByAct is [3,3,5]. | *Raise deckMax*: dilutes the deck. *Swap draft*: a new draft kind and UI [WIN]. |
| D6 | **One treasure economy.** Generic adventure treasures get `"shared": true` after an audit; only `runs: "v4"` runs read the flag. Hero-specific treasures (Monster Hunt per-hero, signatures, deck- or power-dependent texts) stay scoped. Draft slots: slot 1 universal; slot 2 own-origin when the loadout has one; slot 3 weighted. The per-act rarity weights are derived from the audited pool counts so that A6 holds pooled; Tombs (37 rare : 8 common) is reported separately. | *Unscope everything*: hero-specific texts break. *Keep per-row*: 67 vs 112 candidates is not one economy. |
| D7 | **Modifiers on v4: per-fight neutralisation plus "none".** A boss's `v4.incompatibleModifiers` removes only that modifier's conflicting knobs, for that fight, through the `KnobsFor(state, boss)` seam. Iron Bosses' extra treasure reads `ActiveModifiers` directly (`DungeonRun.cs:1021`), so it is always kept, after a Plague Lord too. Every entry needs a `modifierReasons` entry. The nemeses get `{}`. Hagatha, Tad, the Vault, the lords and Tekahn are re-derived by C4. v2/v3 keep the act-wide ban. | *Act-wide bans plus "none" only*: the reward disappears where fights are hardest. *Drop boss bans*: knobs become silently inert. |
| D8 | **Twist pools (v4, both schedules, chapter-mapped)**: act I [DALA_829, ULDA_801], act II [DALA_832, ULDA_802], act III [DALA_833, DALA_831, ULDA_803, ULDA_804]. **Chance 100**: in the originals each chapter's twist was always on (curation.json:484,486). One twist per act. `chance` is the chance of an attempt; a hit with no compatible candidate rolls nothing, which C5 measures. Weight is honoured. The chance is lowered only if a measured twist-on/off delta pushes a slot out of its B band. | *25/35/50*: no source. *Reroll on empty*: hides the exclusion loss. ULDA_801 stays in act I because deckReplace excludes every lord. |
| D9 | **Per-boss rewards (`reward` key, v4 only).** GILA_BOSS_43t gets a `run-v1.json` row: `offered: false`, `runs: "v4"`, granted only by Damph's `reward`. It is a spell, so the grant adds a deck card through `Add`. At deckMax the grant converts to +1 BankedGold, with a note. It also needs player-side admission of the boss card and one real game. | A generic "majors give a treasure": changes every run for one ledger row. |
| D10 | **Draw `provenance-mix-v2`.** `MixesProvenance` matches both draw ids, and the validator accepts v2 only on stamp ≥ 4. Provenance weight is `n_p(pool)^α / (1 + k·drawn_p)`, with α ≥ 0.68 so that (34/2)^(1-α) ≤ 2.5, and the drawn-count term keeping the mix. α, k and `ledgerMajorChance` are chosen with `sim.py` against A10 before lane E fixes them; the initial guess is α 0.7, k 1, chance 50. At most 1 TRL champion per act is kept as a guard, although it rarely binds at about 5% TRL share. | *sqrt weighting*: gives a ratio of 4.1 at t3. *Proportional*: TRL becomes invisible. *Keep v1*: about 4x over-sampling. |
| D11 | **Tekahn on v4t only.** New plan fields `RunMatchPlan.FormRatchet` (default false) and `TurnCap` (default 100) are set only when `Tuned(state)`. On v4t: FormRatchet true and TurnCap 150 for heroRelay. The ratchet clamps max Health to the threshold on entering a form that has `at.healthAtMost` and no own Health. Tekahn is the only such row; Belnaara's and Bazhial's script forms are untouched. NativeMatch and the harness read TurnCap from the plan. The bosspilot form check (`Program.cs:299-303`) applies the threshold check when `plan.FormRatchet` is set. Recorded as an adaptation. | *Engine-wide ratchet*: shared `SwapPowerForm` path, and it moves v3t. *Lower Tekahn's Health*: an identity change. |
| D12 | **Save stays version 6**, with guarded attributes only: `BankedGold`; match-scoped `RestartIndex`, `RestartSources`, `RestartCarriedDamage`; summary `Escaped` and `Restarted`. | *Bump to 7*: moves all 341 L1 lines (Version is printed and digested) plus c-runlayer, c-shrine-core V1, c-acts:753 and c-enc-model F1, for a downgrade case that cannot happen (nothing is public). |
| D13 | **Restart family.** Restart is `Fault` plus `Restarted`: it restages the floor and keeps HeroSwitch and PlagueLordDamage. For persistentDamage bosses it **banks BossDamage as a loss does** and starts the new game at the banked remaining. It does not Finish, and LastBoss skips it. The match seed is `StableSeed(seed, MatchStream, floor, restartIndex)` only when restartIndex > 0. Caps are **per source**: Rewind 1 per floor, Alternate Timeline 1 per floor; cumulative turns ≤ 2 × TurnCap. Player Health by source: **Rewind** restarts from the fight-start Health (HeroDamage from earlier fights under Battle Scars is kept; only damage within this fight is undone); **Alternate Timeline** sets `RestartCarriedDamage` to the damage the current match hero took, fed to `PlayerCarriedDamage` and added to the Battle Scars base. Rewind is consumed through a new `RunMatchResult.ConsumedTreasures`, applied on Restart. | *Fault-identical restart*: gives full Health, the opposite of 48t's text, and erases lord damage. *Shared cap*: lets Rewind block Toki's restart. |
| D14 | **Escape (ULDA_111).** `Win` plus `Escaped`: no MajorsCleared, no kill credit, no drafts. BossDamage is banked for persistent bosses, and PlagueLordDamage is zeroed. An escape tavern opens on the current node, then Advance. BuildPlan sets `RunMatchPlan.EscapeAllowed` (deck ≥ deckMin+1, not a finale act, not the last node). The engine makes the card unplayable when it is false, so the check happens at cast time. Removal is outcome-implied in the escape branch and bypasses `Commit`. It is legal because the deck cannot change mid-match. | *Remove via Commit*: refused at deckMin, so the card could be reused. *Escape as a win*: skipping Khadgar or Tekahn would win the run. |
| D15 | **Generation hygiene in Dungeon Run games only.** Every `LegacyGenerationV1.Get` call site goes through `OfflineCardPools.ForGame`: AddLackeyTask, DiscoverTask ×5, ReplaceTask, AdaptTask, PotionGenerating, SpecificTask, BuildABeast, F16, and the Cards.cs pools after review. The "unscripted" set is a generated admission list (not `Implemented`), with the existing explicit list as its seed. With no filter, the same array is returned, so selfplay RNG draws are unchanged. ULD_616 and DRG_052 are scripted. | *Only AddLackeyTask*: c-gen-9 then fails or under-enumerates. *Filter the two Lackeys*: two Tombs bosses keep losing 2 of 7 of a Saviors-era pool. |
| D16 | **Adaptation criterion** for server-side gaps (DALA_740, VIP, the Tekahn ratchet). Adapt only if (a) the client text or tokens state every effect, (b) the missing parts are triggers or chances, (c) a stated SoloStone choice is recorded in curation `adaptations` with its reason, and (d) real games pass. Otherwise reject with a final reason. | Ad-hoc decisions per item. |
| D17 | **Prevention is a pure predicate plus gates.** RuleCandidates and ModifierCandidates gain the new keys; run-scope candidates see every act's rolled twists. `ForbiddenPairs(state)` is checked by tests at every step; BuildPlan only logs a violation. | *Drop the rule at BuildPlan*: a silent change in released runs. *Throw*: bricks a save. |

---

## 3. Stages and lanes

### Stage 0: integrator, serial; identity changes only

1. **Seams in `DungeonRun.cs`** (one commit):
   - `sealed partial`
   - `Tuned(state)`
   - `KnobsFor(state, boss)`: for now it returns today's ActiveModifiers sum. BossHealth, BossStartManaBonus, PlayerHealthBonus and OpeningHandBonus read it.
   - stub methods in new files with fixed call sites:
     - `DungeonRun.Rules.cs` (R): `NeutraliseKnobs`, `ForbiddenPairs`
     - `DungeonRun.Economy.cs` (E): `CapKnobs`, `GrantEncounterReward` and `ConvertSkippedDraft`, called from RecordActWin and OpenBundleDraft
     - `DungeonRun.Outcomes.cs` (X): `EscapeOrRestartBranch`, called from RecordOutcome before the Win path
   - Each lane edits only its own file. `ActiveModifiers` is not touched.
2. **State and plan fields** (all inert):
   - D12's attributes with `ShouldSerialize`
   - `RunMatchPlan.TurnCap = 100`, `FormRatchet = false`, `EscapeAllowed = false`
   - `RunMatchEnd.Escaped` / `Restarted`, `RunMatchResult.ConsumedTreasures`
   - `OfferedOn` value `runs: "v4"`, with the stamp passed in
3. **`schedule-v1.json`**: `"version": 3` on acts-v3h and acts-v3t. `c-sched-9` F1 is pinned after this edit.
4. **Shared test code**: `verify/suites/p9/common/` with `PickPolicy.cs` (scored picks) and a `LongRunWalker.cs` skeleton, linked from both lane S and lane M projects.
5. **Records**:
   - Tag reservation 32638-32640, checked free.
   - HANDOFF 5 lists the reserved *ranges* (ShrineRules 32610-32629, RuleTags 32660-32699) as well as the single tags.
   - Disposition table for the section E open items.
   - Row → F-file → writer table for every row lane R touches: Candlebeard F07/F08, George/Karl and Ixlid F08, Tad/Vault/LOOT finals F09, Grubb F10 (all hand-kept); Tierra/Belnaara `make_f14.py`; Hesutu and the Heist finale `make_f18.py`; Janak/Bazhial `make_f21.py`; Hagatha and the nemeses `make_f23.py`; Icarax, the lords and Tekahn `make_f24.py`; plus the forcedAttack rows.
6. Check: the fast suites plus c-enc-25 L1 and golden-sequences are byte-identical.

### Stage 1a: "before" records (parallel; built on an `f62275b` export)

- **Lane M: the harness.**
  - Owns `verify/suites/p9/c-bosspilot-run/` (new project), its player pilot, `verify/tools/balance/{agg.py,check_targets.py}` and `verify/fixtures/p9/targets.json`.
  - In targets.json every target records its source and its derivation from R0.
  - Modes: `slots9`, `outliers9`, `finale9`, `tekahn9`, `longrun9`, `pairs9`, `treasure9`, `ends9`.
  - The turn cap is read through a shim that returns 100 on the base tree.
  - Fixes the ACT_RUN label in the old harness only if that is byte-neutral; otherwise it is recorded as a final label note.
  - Deliverable: **`bp9-before`**, the fair-player v3h/v3t measurement built on the `f62275b` export, so that it contains none of step 9's changes.
- **Lane S: walker and L4.**
  - Owns `verify/suites/p9/common/LongRunWalker.cs`: shipped data with encounters; scored picks; synthetic `RunMatchEnd` scripts; reload at every step; a digest that includes the profile.
  - Owns `c-longrun-9` and new `verify/tools/gen/p9/record_replay_v3.sh` + `Record9.cs`, which take an output directory, a named schedule and a stop predicate (phase plus floor). Output goes to `verify/fixtures/p9/replay-v3/`, never `p8f25`.
  - L4 stops: act I anomaly, a shrine run's act I anomaly, the finale tavern, Hagatha PreMatch, relay PreMatch, Plague Lord InMatch, a stamp-3 modifier node, mid-tavern.
  - L4 uses a frozen copy `ReplayV3.cs` restricted to `f62275b` APIs.
  - Code: the Version-rollback helper (Runtime call site [WIN]); Resume refuses clearly on unknown deck or BossSequence ids; `dungeon-run.md` "Save versions" covers versions 4-6 and PowerChoices.

### Stage 1b: derive the starting values (serial, integrator plus lane E)

Inputs: `bp9-before`, `sim.py` and the walker on v3 data.
- Compute the D5 variants (a) and (b) for A2/A3.
- Choose D10's α, k and `ledgerMajorChance` for A10.
- Compute the per-act rarity weights for A6 from the audited pool.
- Set the B point targets' first curve guess from the before rates.

Lane E's initial v4 table is written here, with its sources, instead of being fixed in advance.

### Stage 2: mechanism lanes (parallel; v4 not default yet)

**Lane R: prevention, ordering, twists.**
- Owns:
  - `DungeonRun.Rules.cs`; hooks in RuleCandidates, OpenAnomaly, RollTwists, ModifierCandidates, OpenModifier and PickModifier
  - `rules[]` and `modifiers[]` in `schedule-v1.json`
  - the boss flags and `v4.incompatibleModifiers`/`modifierReasons` in the stage-0 row table (hand-kept F07-F10 and F01, and `make_f14/18/21/23/24.py`)
  - the `Encounters.cs:243` passthrough list (`modifierReasons`, `incompatibleTreasures`, `v4`)
  - the rule/encounter parse region of `DungeonRunData.cs` and the rule section of the Validator
- Work items:
  1. **Ordering fix**: assign `state.BoardShrine = runShrine` before `DungeonRun.cs:185`.
  2. **Exclusions**:
     - ULDA_719 × DALA_831
     - ULDA_803 × ULDA_709
     - DALA_829 × ULDA_706
     - DALA_861 × `attackRedirect` (Tierra)
     - ULDA_802 + `forcedAttack`
     - ULDA_710 × `charge` (Candlebeard ×2, Janak)
     - ULDA_711 × `windfury` (Hesutu)
     - ULDA_709 and DALA_831 × `statSwap` (Icarax)
     - ULDA_714 × `fatigue` (Bazhial) and × `persistentDamage`
     - ULDA_721 + `listedDeck`, `mirrorDeck` and run-deck opening-flag cards (design.md:104)
     - DALA_859 × `listedDeck`, `noLoss`, `manaIdentity` and × mod_open_road
     - ULDA_715 × `handAdd`
     - DALA_833, DALA_863, ULDA_801 + `heroSwitch`
     - ULDA_720 `incompatibleTreasures [LOOTA_800]`
  3. Run-scope candidates are checked against all acts' twists.
  4. `room` tags on the four rooms.
  5. DALA_857 disposition.
- Tests: `c-rules-9` (C2-C5, C7) on `verify/fixtures/p9/schedule-twists9.json`; a stored-offer assertion in c-rules-06 W16. `pairs9` covering arrays are generated by `verify/tools/balance/pairs.py`.

**Lane E: economy and scaling.**
- Owns:
  - `DungeonRun.Economy.cs`: CapKnobs (DALA_904 count from HealthBonus, optional offsetCap), act rarity, origin slot, `shared`, BankedGold, draw v2, `reward`, `upgradeEveryFloors`, `bundleAfterTreasure`
  - `DungeonRunTavern.cs` (`weightByAct`, `maxPerRun`), `tavern-v1.json`
  - the acts-v4h/v4t objects, including the D8 pools
  - **all of `run-v1.json`**: lane X hands over its ULDA_111/GILA_911 rows; E adds the 43t row and the `shared` flags
  - the act/treasure/tavern/schedule parse region and the ValidateSchedule section (v4 keys refused on stamp < 4; `provenance-mix-v2` accepted only on stamp ≥ 4)
- Treasure audit: every generic Heist, Monster Hunt and Tombs treasure is classified shared or scoped with a reason in curation `adaptations`. Each shared one gets one `treasure9` game on a non-native loadout.
- DALA_740 is decided by the D16 criterion.
- Tests: `c-econ-9` (the A table; walker from stage 1a) and `c-sched-9`.

**Lane X: match ends, restarts and finales.**
- Owns:
  - `DungeonRun.Outcomes.cs`
  - engine: ULDA_111 (a StartOfGameRules self-draw; unplayable unless `EscapeAllowed`; on cast, tag 32638 and an end like EndWithNoLoss), GILA_911 (tag 32639), the ratchet behind `FormRatchet`, TurnCap and the RunMatchEnd/RunMatchResult fields (`DungeonRunSetup.cs`, `DungeonRunContext.cs`)
  - admission rows `DungeonRunAdmission.cs:130-131,406-424`, F01:88-93 and F23:76-80 reason texts, `DungeonRunDeferrals.cs`
  - the "phase 9" rewording for Temporal Loop
  - **[WIN]**: `NativeMatch.cs` (TurnCap from the plan; EndDungeonRunFight accepts escape/restart; the `:6634` null guard), `ClientBridge.cs:482`, `LocalLadder.cs:243-258`
- Tests: `c-ends-9`:
  - escape on combat, major and lord; refusal on finale and last node
  - the card unplayable at deckMin, and the deck at deckMin+1 → escape → card gone
  - Rewind consumed; per-source caps; fresh seed only when restartIndex > 0
  - lord damage banked on Restart
  - Rewind under Battle Scars keeps the fight-start HeroDamage
  - 48t carries player Health
  - ratchet only with `FormRatchet`; TurnCap 150 only on a v4 relay
  - every new field round-trips
- bp mode: `ends9`.

**Lane G: generation hygiene and engine leftovers.**
- Owns: the generation-filter file and every call site listed in D15; ULD_616 and DRG_052 with tests; the generated unscripted list and the generation exclusions region of `DungeonRunAdmission.cs`; plus the Typhoon `InterceptLeave` and Kingsbane Durability items, if stage 0 assigns them here.
- Tests: `c-gen-9`:
  - every Dungeon Run random pool enumerated: 0 unscripted ids
  - the Lackey pool has 7 cards
  - 200 seeded games: 0 unscripted generated
  - selfplay/randplay digests: unchanged, or each move attributed to the two scripted Lackeys

### Stage 3: integration, in two waves so each baseline move has one cause

- **Wave 9a** merges R, X, G and S. Defaults stay v3h/v3t.
  - Run gen.sh twice (byte-identical), then the fixture generators. This includes p8f05/06/18/24 `make_fixture.py`; their "three empty twist pools" asserts stay valid because v4 does not exist yet.
  - Full regression, then re-record with attribution. Expected movers: R (shrine-run anomaly offers, new flags in rules05/06 and the T16/W23 fixture loops, L4 anomaly lines), G (generation records, possibly selfplay).
  - X moves nothing on v3, because all its knobs are gated.
- **Wave 9b** merges lane E.
  - Flip `defaults`, pin ReleasedShapes[4], regenerate the schedule fixtures and update the p8f05/06 twist-pool asserts.
  - Re-pin golden-sequences (+v4 lines; the default newrun lines follow v4 and draw v2), c-enc-25 B1/B2/B7/R, c-w5fix Q7-Q11 and c-enc-model J1.
  - Full regression. `c-econ-9` must pass the A table.

### Stage 4: calibration loop (serial, at most 3 rounds on S1, then acceptance on S2)

1. Run `slots9`, `outliers9`, `finale9`, `tekahn9` and `longrun9` on v4, then `check_targets.py`, which includes the R0 product check.
2. **Lane E2** is the single owner of all encounter `v4` blocks and v4 curves in this stage:
   - re-tier or offset the flagged non-room outliers through `v4` blocks
   - apply the D4 levers in order
   - adjust curves, finale lists and twist chance (D8 rule)
   - each round regenerates and re-records only the p9 modes
3. **GILA_BOSS_48t gate** (lane X), after E2's round:
   - Pass conditions: a scripted cast condition; 0 faults in 40 fair games; restarts ≤ 1 per floor; cumulative turns ≤ 2 × TurnCap. On a pass, re-add it to Toki's deck (`make_f23.py`) and update the c-enc-23 pins.
   - Otherwise, a final rejection: "AI cannot value a game restart".
4. Final acceptance on S2. A remaining miss is written up with its numbers as a [WIN] playtest input.

### Stage 5: close-out

- Three-lens review, then one fix pass.
- Regenerate the ledger, with counts recomputed from the section E outcomes rather than predicted.
- HANDOFF §2/3/5:
  - Hagatha is about 11% (118/1000), not 506.
  - Remove the stale ULD_152/BOT_543 note.
  - Add the tag ranges.
- Update the curation `twists-per-act` and `plague-twists-per-act` notes and `health-policy`.
- Update README §4 numbers.
- Add a "Step 9: the economy" section to `dungeon-run.md`: R0 and the slot decomposition replace F7, plus the D-table.

---

## 4. What moves in existing baselines

| Pin | Moves? | Why |
|---|---|---|
| c-enc-25 L1 (341 lines) | **No** | no version bump; no v4 key read by acts-v2 or legacy runs; new rows are `runs: "v4"` |
| c-enc-25 L4 (new) | anomaly offers only | ordering fix and exclusions; each line attributed |
| c-runlayer, c-shrine-core V1/V4, c-acts, c-enc-model F1 | No | save version stays 6 |
| golden-sequences.txt | v4 lines added; default newrun lines change | defaults flip, draw v2. v3 lines are unchanged, because encounter tuning goes through `v4` blocks. |
| c-enc-25 B1/B2/B7, R | yes | content lifted; final blocker texts; v4 default roll |
| c-w5fix Q7-Q11, c-enc-model J1/G8-G13 | yes | default names; v4 modifier offer plus "none"; new flags |
| c-rules-05 T16, c-rules-06 W16/W23 | yes | new flags change the fixture rolls; W16 gains the stored-offer check |
| p8f05/06/18/24 schedule fixtures and their asserts | yes (9b) | rebuilt from the shipped file, which now holds v4 |
| c-enc-23 | only if 48t lands | Toki's deck |
| tekahn24/25 | **No** | ratchet and TurnCap are gated to v4 |
| bp records on defaults (sweep25 a/b/c, unnamed finale25) | yes (9b) | they now walk v4 |
| rules05/06, cross05/06 bp | some rows (9a) | exclusions and flags |
| generation-hitting records; selfplay/randplay only if they generate ULD_616/DRG_052 | yes (9a) | lane G |
| encounters-v1.json and its counts | yes | flags, reasons, `room`, `v4` blocks |
| ReleasedShapes | [4] added; [3] unchanged | — |

---

## 5. Risks

1. **Measurement cost.** About 7 hours per round and about 28 hours in total. Run one harness at a time; count `SearchStop.Clock` in a scratch copy before calling a moved line a regression; never change budgets.
2. **The fair AI is not a human.** R0 and the slot bands calibrate relative difficulty. Absolute feel needs a [WIN] playtest.
3. **Shared treasures may assume their hero.** Mitigated by the audit plus one `treasure9` game per row; anything doubtful stays scoped.
4. **Per-fight neutralisation is invisible to the player.** It needs NativeDungeonRunText [WIN] and C4's declared reasons.
5. **Restart loops.** Per-source caps, a cumulative turn budget and a gated seed policy.
6. **Escape exploits.** The cast-time `EscapeAllowed` check, no major credit, and lord attempts still banked.
7. **Twist exclusion loss.** Stench and Carts lose 13 rows each; C5 measures it.
8. **Data churn.** Ownership by file and region: R owns rules, modifiers and encounter flags; E owns schedules, tavern and all of run-v1; E2 alone owns `v4` blocks in stage 4. The integrator merges.
9. **The encounter `v4` block** is a new parse path in a file compiled by 39 hall harnesses. Parse-only in Data; read through `Tuned` in DungeonRun.
10. **Death Knight and Demon Hunter variety** (A13): a follow-up if under 10 distinct entries.
11. **Adaptation fidelity** (ratchet, DALA_740, 43t as a player card): each one passes D16 and is recorded.

---

## 6. Windows-only follow-ups [WIN]

- **Plugin build** (keeping the 75-warning set):
  - NativeMatch TurnCap from the plan, escape/restart end reasons and the `:6634` null guard
  - ClientBridge and LocalLadder
  - the Runtime Version-rollback call site
- **In-client checks**:
  - the escape screen and escape tavern
  - the Rewind flow
  - "none" and the neutralised-modifier text
  - BankedGold at the tavern
  - the 150-turn relay cap
  - the pending Norroa 25s / Nozdormu 15s timers
- **Human playtest** of R0 and B1-B11 on v4h and v4t.
- Optional and outside step 9: reopening Temporal Loop with `Game.CloneExact` and a full NativeMatch history re-sync.

---

## 7. Critique points rejected or changed, and why

- **Save version (blocker 2)**: I took option (a), no bump, rather than (b), bump and mask. The `NoLoss` attribute precedent covers Escape and Restart without new enum members. A downgrade is not a real case because nothing is public. This removes about six moved pins.
- **Merging lanes R and E serially (critique 6)**: rejected in favour of the stage-0 seams (`KnobsFor`, per-lane stub files). They give single ownership without losing the parallel lanes.
- **"Rewind under Battle Scars heals the hero"**: partly rejected. A restage starts from the fight-start HeroDamage, so only damage taken within the fight is undone, which is what Rewind is for. A c-ends-9 check pins it. Banking lord damage on Restart is accepted.
- **"n ≥ 40 everywhere with CI acceptance"**: changed to n ≥ 200 per band. At n = 40 a band is about ±14 points and cannot resolve 10-point bands. Tekahn goes to n = 100. The other critique's "raise B1-B4 and make the majors the gates" is used only as far as R0's decomposition supports it.
- **ULDA_607 "author the decks"**: rejected. It is 15 class decks of invented content with no source, unlike VIP's one-line effect. It gets a final rejection instead.
- **Temporal Loop "schedule a Windows lane"**: not in step 9. It gets a final adaptation, with the "phase 9" texts reworded. A later [WIN] lane remains possible.
- **LOOT finals "move them instead of offsetCap"**: partly accepted. Identity offsets are kept and the tier move is the second lever. A tier move is not the default, because emptying major1 leaves act I's major to 4 custom bosses.
- **Tags "32615, 32629, 32699 in use"**: corrected. They are inside the reserved ShrineRules (32610-32629) and RuleTags (32660-32699) ranges rather than single uses. The fix is to list the ranges in HANDOFF 5. 32638-32640 are free.
- **D1 run set "every loadout × 10 seeds with a reload at every step"**: reduced to every loadout × 2 seeds per schedule, plus 22 rows × 10 seeds. That covers every deck-scoped path at a feasible cost.