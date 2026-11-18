# Step 9: the test phase (lanes, defects, pins, review findings)

Branch `wip/t-int` (worktree `/home/user/wt/t-int`), from `ab48ab1` (steps 1-9 merged, step 9 untested). Seven test lanes,
each on `wip/t-<lane>` from `ab48ab1`, reviewed (findings verified one by one), the CONFIRMED findings fixed in the lane's
own worktree, then merged here with `--no-ff` in this order: pins, rules, econ, ends, gen, long, harness. Conflicts:
`verify/MANIFEST.md` (rules, long, harness: generator rows) and `verify/bin/bp.sh` (rules' and harness's usage and mode
blocks); every side kept. `verify/lib/suites.sh` and `final.sh` were touched by no lane: the step-9 suites are registered
here (builds 85-93, runs 72-77, `final.sh` stage `p9`, `same.sh` leaves c-longrun-9's `TIME` line out).

The full regression of this tree, the reduced calibration and the 48t/ULD_152 gate are queues for the coordinator:
- `scratchpad/t/all.q` (185 lines): build-all, `runs.sh 1` and `2` in slices (entries 1-77), `same.sh`, every `final.sh`
  stage's bp.sh calls (c25, enc, rules06, cross06, released, released-all, w1, w2, rules05, cross05, w4, w5, w6), the new
  non-calibration modes (`pairs9` 1-25, `ends9` five parts, `treasure9` over the 126 shared treasures in 6 chunks, each run
  1, run 2, `same`), then the hall (harness, 1-38 in slices, `same`). Runner `scratchpad/t/q.sh` (works in
  `/home/user/wt/t-int`).
- `scratchpad/t/calib.q` (lane harness's queue (a), the reduced calibration: slots9, finale9, tekahn9, agg + check_targets);
  `scratchpad/t/gate.q` (lane harness's queue (b): gate48t and uld152). Same runner.
(`scratchpad` = `/tmp/claude-0/-home-user-SoloStone-wip/17cd0c92-5ca0-5f23-99aa-03b6556206e0/scratchpad`.)
The calibration modes `outliers9` (B9) and `longrun9` (B10/B11) are in no queue (stage 4 sizes them).

## 1. Lanes

| Lane | Branch, head | What it added |
|---|---|---|
| pins | `wip/t-pins` 3d16669 | the pins section 5 of implementation.md predicted, moved in 22 suites (section 3 below); section-3 cleanups: c-rules-05 T16 back to `> 20`, c-enc-model E3/G10 restored as far as the data allows (both shown failing), the ACT_RUN label (correct; comment and HANDOFF 3.4 fixed), F14's TRLA_065p note (already gone); golden-sequences.txt re-recorded (the 220 default lines only). randplay digest 1DE562A92D22C987 -> 22043E377F1097F3 (games 50, 102, 103: ULD_616 / DRG_052, lane G's scripted Lackeys; the f62275b engine reproduces the old digest), selfplay unchanged |
| rules | `wip/t-rules` 94a1978 | `p9/c-rules-9` (58 checks: V1-V19, F1-F15, S1-S4, T1-T12, W1-W7, K1), fixture `p9/schedule-twists9.json` (`make_twists9.py`), c-rules-06 W16b, the C6 covering array (`tools/balance/pairs.py`, `fixtures/p9/pairs9.txt`, 1903 cases in 25 chunks) and its harness `p9/c-bosspilot-pairs9` (`bp.sh pairs9`) |
| econ | `wip/t-econ` 699594d | `p9/c-sched-9` (23 checks, F1 tripwire against an f62275b dump, `record_sched9.sh`), `p9/c-econ-9` (27 checks plus MEASURE lines, plan 1 A) |
| ends | `wip/t-ends` d1d4dfb | `p9/c-ends-9` (58 checks: P, E, R, K, D, F, S), `p9/c-bosspilot-ends9` (`bp.sh ends9 <part>`) |
| gen | `wip/t-gen` f9ef6ba | `p9/c-gen-9` (61 checks: U, P, C, L, K, G) |
| long | `wip/t-long` c1e3ea9 | `p9/common` (PickPolicy, LongRunWalker), `p9/c-longrun-9` (10 checks, D1/D2/D4/C1 over 1800 walks), c-enc-25 L4 (4 checks) with the f62275b replay fixtures `p9/replay-v3/` (`record_replay_v3.sh`, frozen `ReplayV3.cs`) |
| harness | `wip/t-harness` 66f80c4 | the fair-player run harness `p9/c-bosspilot-run` (modes slots9, outliers9, finale9, tekahn9, longrun9, treasure9, gate48t, uld152), `tools/balance/agg.py`, `check_targets.py`, `fixtures/p9/targets.json` |

## 2. Defects found and fixed

In step-9 code:
- **RuleCandidates and fight-only heroes** (lane rules, 44fe21b, `plugin/DungeonRun/DungeonRun.cs`, 3 lines): ForbiddenPairs
  reported a rule whose `incompatibleClasses` names an act boss's fight-only hero (hero cards, relay heroes, by character),
  but the gate (anomaly offer, twist roll) only checked the run class. The gate now applies the same check. Test c-rules-9
  F14 (failed before, passes after). Nothing moves on the shipped data (Hagatha's `heroSwitch` flag already excludes
  DALA_833): c-rules-05/06, c-enc-model and c-enc-25 give byte-identical output with and without it.

No other step-9 defect was found by any lane. Defects in the lanes' own tests, found by the review and fixed (section 4).

Found and NOT fixed (pre-step-9, not step-9 code):
- **Engine: SwitchingAura.RemoveInternal throws KeyNotFoundException** (`engine/SabberStoneCore/src/Auras/SwitchingAura.cs`
  line 123, `idDict[id]` for an entity no longer in `IdEntityDic`), inside the boss AI's search simulation
  (FairBotState.Apply -> EndTurnTask -> MainEnd -> AuraUpdate). The search falls back (3 FALLBACK lines at turn 20). Seen in
  pairs9 chunk 23 case 1722 (dala_07_belnaara, MAGE, seed 9901722) with or without the case's rule, so chunk 23 exits 3. Not
  fixed here: an engine change moves every simulation-dependent record; noted in the pairs9 harness header and README.

## 3. Pins moved (lane pins; every one listed in implementation.md section 5)

- c-runlayer: treasures 77 -> 78 (GILA_BOSS_43t's reward row); the rejected stand-ins GILA_911 / ULDA_111 -> DALA_740 /
  GILA_900h2 (lane X admitted both). 244.
- c-catalog: ledger 3880/10/13 -> 3890/3/10 (8 twists, ULDA_111, GILA_911 implemented; 48t missing -> partial); the
  stale-catalog candidate ULDA_111 -> GILA_900p. 127.
- c-admission: 4109 + 10 failed -> 4117/0 (GILA_911 to game-restart, Player, NeverGenerated; GILA_900p the only
  deck/rewind row; ULDA_111 and GILA_911 RunTreasureLift; new: the lifts past phase 9 are exactly DALA_857, ULDA_BOSS_58p,
  GILA_BOSS_48t; the extra Generated cards must each be one of lane G's UnscriptedGenerated).
- c-acts: 175 + 6 failed -> 181/0 (defaults v3h/v3t -> v4h/v4t, file version 3 -> 4, R2 future version 4 -> 5, R3 refused
  schedules 5 -> 7; the A2/R3 anchors through First(), the old ones had become no-op edits).
- c-deferrals: 146 + 2 -> 150/0 (phase-9 ids [900p, 911, 111] -> [900p] plus MatchEndIds [911, 111]; start-of-game draws
  5 -> 6; new T12 per match end).
- c-deckops 178 + 1 -> 179/0; c-treasures-F4 233 + 3 -> 236/0.
- c-shrine-core 128 + 3 -> 132/0 (D5 to v4; D8's anchor `"scheduleVersion": 3` -> 4, plus a guard that the edit took; V3
  future version 4 -> 5).
- lane R's new boss flags: c-enc-08 113, c-enc-09 166, c-enc-14 117, c-enc-17 123, c-enc-18 109, c-enc-21 100, c-enc-23 68.
- c-enc-13 S146: the generation filter is the union of both lists, kept 1 -> 6 of 80 (lane G). 112.
- c-rules-06 W10: "no shipped plague" -> exactly acts-v4h/v4t acts I-III, chapter-mapped. 224 (225 with lane rules' W16b).
- c-w5fix Q7-Q10: Tekahn's default pair to acts-v4t/v4h, stamp 4. 30 + 3 -> 33/0.
- c-enc-model: crashed before (Once on the repeated finale text, now OnceIn); C1/C4/D2/E7 to v4; A10 lift texts; B5's
  unscripted card 48t -> DRG_246; new D1c, D1d. 155/0.
- golden-sequences.txt: the 220 "(default)" lines to acts-v4h/v4t stamped 4, same picks; the other 3280 lines byte-identical.
- c-enc-25: 31 + 7 -> 38/0 (A6/A7/B1 13/10 -> 10/3, B2, B3, B6 E25 rejected {DALA_740, GILA_900p, ULDA_607}, B7 8 idle
  twists -> 0); 42 with lane long's L4.
- randplay digest 1DE562A92D22C987 -> 22043E377F1097F3, regkeys 2578 -> 2582 (3 of 200 games: generated Lackeys).

No lane moved a pin of its own; this phase moved none either (the review fixes add checks or sharpen new ones).

## 4. Review findings

Every lane's work was reviewed and each finding verified; the verifier's verdicts are in the lane reports. 27 confirmed
(all fixed, below), 1 rejected (harness: "the 'no game runs past its cap' check restates the loop bound and cannot fail").

### rules (5 confirmed)
1. pairs9 false "forbidden pair" failures (high): the walk to the fight floor drafted LOOTA_800 before ULDA_720 was injected
   (cases 1712, 1713, 1726, 1730). Fixed: Reach() skips the treasures the case rules' `incompatibleTreasures` name. Chunk 23
   cases 1710-1730: failed checks 4 -> 0, twice identical.
2. Chunk 23 case 1722 fallbacks (medium): the engine defect in section 2, left open; the harness header and README state
   it as the known exception.
3. C1's run-scope half untested (medium): new F15 on a data copy (ULDA_729 given `incompatibleFlags` charge, a charge boss
   on act III): act I's gate leaves it out, offers it without that boss, ForbiddenPairs reports it once on act 3. Shown
   failing with RuleCandidates' run-scope branch reverted to the act's own lists.
4. Compatible() (T3/T8's reading) lacked the fight-only-hero exclusion (low): added (same character rule).
5. W7 named ULDA_721 (act scope) as run-scope (low): W7 now requires both run-scope anomalies, ULDA_720 and ULDA_729.
c-rules-9: 56 passed, 2 failed (T2: DALA_831 9.8%, DALA_833 10.2% below 15%; K1: 16 `mod_hot_start` no-ops on floor-15
majors; both calibration items), twice identical.

### econ (1 confirmed)
1. The walker's missed-grant check could fail nothing and A12 counted predicted rewards (low): RewardBad counted, new W10
   (every reward with room granted; 70 grants), Rewards counts engine grants only, W5's vacuous BankedEarned clause dropped.
   Shown failing: GrantEncounterReward returning before the grant fails W10 (70 walks) and K6. c-econ-9 27/0, twice
   identical.

### ends (4 confirmed)
1. About 20 checks never shown failing (medium). Breaks run now (plugin unless marked; c-ends-9 sections): R10 (no
   RestartSourceAvailable), R18 (EscapeOrRestartBranch ungated), R16 (a tie not taken as a restart), D1 (BossRemaining not
   set), D2 (Battle Scars damage not kept), D3 (summary.Restarted not set), D4 (a won lord fight keeps the damage), S1
   (Restore copies LoadedVersion), P1 (PlanMatchEnds and RestartsFor both ungated: P1 is defence in depth, one gate alone
   never fails it), K2 (engine: the ratchet without the plan's flag), and the ends9 harness (Alternate Timeline's carry +1:
   "carried 33 of 32" in the toki part). Each failed the named check; restored, engine md5 back to da3fa601.
   Still not shown failing (positive paths and defaults): P2, P3, P9, E1, E8, R2, R6, R15, R19, R21, S7.
2. The encounter harness's literal 100-turn cap (medium): `p8f01/c-bosspilot-encounter` runs to `plan.TurnCap` and its
   form-threshold check applies to a power-only form under `plan.FormRatchet`. On released schedules the cap is 100 and the
   ratchet off: enc10-hagatha and plague24 110 are byte-identical before and after. The four generators that patch this
   harness rerun twice (byte-identical) and their outputs committed (rules05, rules06, collect, enc09: 7 lines each).
3. ends9's floor budget off by one for a capped restarted game (low): a capped game counts `cap` turns.
4. Vacuous sub-claims (low): K1/K2 heal through the engine (TakeHeal); R21 drops "carries nothing"; S1 sets LoadedVersion
   apart from the snapshot's; the always-true per-game cap check in ends9 dropped.
c-ends-9 58/0, twice identical.

### gen (5 confirmed)
1. P3 picked pools by name (medium): it now takes every static method of the Dungeon Run card sets returning cards or ids
   (44 methods, was 30; 260 calls) and requires every one to be called (a method whose arguments cannot be built fails it:
   shown with ChooseOneOptionIds(Card) and Scripted(..., Race) before their argument types were added).
2. G3 missed cards made at setup (low): entities present after MainBegin that the plan did not put there are checked too.
3. Kazakus's ingredient sites untested (low): new C site picks the cost-1 potion and its first ingredient through the
   engine's choice path; shown failing with PotionGenerating's two ingredient ForGame calls removed (engine break; 5 stray).
4. U3 asserted only > 1000 (low): pinned to 2924 ids / 88 pools.
5. c-gen-9 not registered (low): registered here (run 76). The selfplay/randplay attribution the finding asks for was
   done by lane pins (randplay games 50/103 ULD_616, 102 DRG_052; selfplay unchanged).
c-gen-9 61/0, twice identical (games digest FA19ACD009E32E15).

### long (2 confirmed)
1. L4 attributed only the first moved line per replay (medium) and 2. never checked that the step changed only the offer
   (low): L4 now walks each replay step by step; a moved line must be an offer whose state, with the recorded offer put in,
   has the recorded digest, and the offer must redraw as recorded with the step-9 exclusions lifted; the recorded offer is
   put back and every later line compared. 281 of 281 lines walked, 5 moved offers all attributed (v3-2's act III anomaly
   at line 40 was hidden before), no tavern, treasure or modifier line moves. Shown failing: the offer not put back (48 of
   281 walked) and Tuned >= 3 (10 of 281). `C_ENC25_ONLY` added for single sections.

### harness (5 confirmed, 1 rejected)
1. The PlayGame.cs factoring broke the four generators that patch the encounter harness (high): the factoring is undone
   (the encounter harness is ab48ab1's again, then lane ends' TurnCap change); PlayGame.cs is the run harness's own copy.
   All four generators byte-identical to the committed outputs (before lane ends' change).
2. The floor budget and the GATE field could not fail with one restart (medium): a restarted game's cap must equal
   max(1, 2 x cap0 - the floor's earlier turns); the cumulative bound is 2 x cap0 + 1; GATE counts either. Shown failing
   with PlanMatchEnds' reduction removed ("game 1's turn cap 100, not the floor budget's 198").
3. `cap >= 100` on every game (low): game 0 only.
4. check_targets passed B6 all-four and B5 on the interval alone (low): both now need the point within the tolerance (12 of
   50 and 1 of 30 now MISS on synthetic data).
5. Stale comments (low): the ULDA_111 cast is lane ends' (c-ends-9 E, `bp.sh ends9`); uld152 puts two drafted ULD_152 in
   the deck.

## 5. Checks on this tree

All on this tree (`wip/t-int`), each harness call through the harness lock; logs under `scratchpad/t/s3/`.
- Engine: 0 warnings, 0 errors, md5 `da3fa601007fd83b3ce2cfa7e5899060` (unchanged by the test phase).
- Build-all in slices: the catalog tool, all 93 suite projects, randplay and the 39 hall projects: every line exit 0, 0
  errors (138 clean lines, the gen tools included).
- `gen.sh` twice: the six generated files unchanged (encounters 255 rows, 296 decks, 876 adaptations, 4 rejected; catalog
  506 entries, 6508 admitted, 1 excluded; ledger 4970 rows, 3890 implemented, 3 partial, 10 missing, 1067
  duplicate/support).
- The fixture and harness generators twice, in place: every `p8f*/make_fixture.py`, `make_bp_collect.py`,
  `make_bp_rules.py`, `make_bp_rules06.py`, `make_harness.py`, `p9/make_twists9.py`, `balance/pairs.py`: no file changed
  either time (so the four generated bosspilot harnesses are exactly their generators' output from the merged base
  harness). `record_sched9.sh` and `record_replay_v3.sh` record from an f62275b export and were each run twice or three
  times byte-identical by their lanes; not rerun here.
- Suites, twice, `same.sh only`: every one "1 vs 2 identical":
  c-sched-9 23/0, c-econ-9 27/0, c-rules-9 56 passed / 2 failed (T2, K1: calibration), c-ends-9 58/0, c-gen-9 61/0,
  c-longrun-9 10/0 (TIME excepted; 245 s), c-enc-25 42/0 (pins' 38 + L4's 4), c-rules-06 225/0 (W16b), c-rules-05 170/0,
  c-enc-model 155/0.
- Harnesses the lanes fixed, twice: the run harness's restart smoke (named Vustrasz, GILA_911 held, PLAYER_HEALTH 1, N 2):
  2 restarts, 0 faults, 0 fallbacks, 0 failed checks, identical; pairs9 chunk 23 cases 1710-1730: 0 failed checks, 3
  fallbacks (case 1722, section 2), identical; `bp.sh ends9 rewind`: 4 Rewind restarts, 0 faults, fallbacks or failed
  checks, identical; `bp.sh enc10-hagatha` (the base harness, now reading plan.TurnCap): twice identical and byte-equal to
  the record of the ab48ab1 base harness build.
- Everything else (the other 67 run entries, every bosspilot stage, pairs9's other chunks, ends9's other parts,
  treasure9, the hall) is the full regression in `all.q`. Expected there: the bosspilot records and the run entries that
  pins listed as moved differ from `verify/baseline` (wave 6) with 0 failures; pairs9 chunk 23 exits 3 (case 1722);
  every other new mode 0 faults, 0 fallbacks, 0 failed checks, run 1 = run 2.

## 6. Left open

- Calibration (stage 4): c-rules-9 T2 (twist weights; lane rules measured 2/2 -> 12.3/14.3%, 3/3 -> 14.1/17.1%), K1
  (`mod_hot_start` against `DefaultBossManaCap` 3 on act III majors), c-econ-9 A2 (finale deck median 32, band 34-42) and
  A10 (per-row ratio 4.52 at 6000 runs, band 2.5: lower k or raise alpha), B9's 17 named outliers (targets.json B9.named
  empty), every B criterion (calib.q).
- The engine defect in section 2.
- `verify/baseline` is not re-recorded (the full regression first).
- The harness was not built on an f62275b export (bp9-before); the run harness reads the step-9 fields by reflection.
