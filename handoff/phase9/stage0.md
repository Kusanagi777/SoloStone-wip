# Step 9, stage 0: seams, inert fields, records

Plan: `handoff/phase9/plan.md` section 3, stage 0. Scope change (the user): implementation only. Stage 0 item 4 (shared test
code `verify/suites/p9/common/`) and item 6 (the fast suites, c-enc-25 L1 and golden-sequences) are not done here; the user
runs the tests. `c-sched-9` (the F1 tripwire) is not written.

## 1. What stage 0 put in place (behaviour-neutral on every released schedule)

- `DungeonRun` is `sealed partial`. `DungeonRun.Tuned(state)` is `state.IsActRun && state.ScheduleVersion >= 4`.
- `DungeonRun.KnobsFor(state, boss)` returns a `DungeonRunKnobs`: the active modifiers' deltas (each passed through
  `NeutraliseKnobs` first), summed, plus the run's HealthBonus and the boss's `HealthOffset`, then `CapKnobs`.
  BossHealth, BossStartManaBonus, PlayerHealthBonus and OpeningHandBonus read it. `ActiveModifiers` is unchanged
  (DraftOptions, TavernGold and Iron Bosses' extra treasure still read it directly).
- Lane stub files, each changing nothing yet, with their call sites fixed:

| File | Lane | Methods | Called from |
|---|---|---|---|
| `plugin/DungeonRun/DungeonRun.Rules.cs` | R | `NeutraliseKnobs`, `ForbiddenPairs` | `KnobsFor` (once per active modifier); tests |
| `plugin/DungeonRun/DungeonRun.Economy.cs` | E | `CapKnobs`, `GrantEncounterReward`, `ConvertSkippedDraft` | `KnobsFor`; `RecordActWin` (after the finale check, before the treasure draft); `OpenBundleDraft` (the deckMax skip only) |
| `plugin/DungeonRun/DungeonRun.Outcomes.cs` | X | `EscapeOrRestartBranch`, `PlanMatchEnds`, `RestartMatchSeed` | `RecordOutcome` (after the summary is added, before the fault/loss/win paths); end of `BuildPlan`; `MatchSeed` |

- Inert fields (save version stays 6; every attribute is omitted at its default):
  - `DungeonRunState`: `BankedGold`, `RestartIndex`, `RestartSources`, `RestartCarriedDamage`.
  - `DungeonRunMatchSummary`: `Escaped`, `Restarted`.
  - Engine `RunMatchPlan`: `TurnCap = 100`, `FormRatchet = false`, `EscapeAllowed = false`.
  - Engine `RunMatchEnd`: `Escaped`, `Restarted` (optional constructor parameters, default false).
  - Engine `RunMatchResult`: `ConsumedTreasures` (internal `Consume`, copied by `Clone`, counted by `IsEmpty`).
  - `DungeonRunTreasure.RunsV4 = "v4"`: `OfferedOn(actRun, encounters, stamp)` offers it on an act run of stamp 4 and
    above; the validator accepts `runs: "v4"` only in a schedule file of `scheduleVersion` 4 and above.
- `schedule-v1.json`: `"version": 3` on acts-v3h and acts-v3t (their stamp was already 3, the file version; it now stays 3
  when the file goes to 4). The four schedule fixtures (p8f05, p8f06, p8f18, p8f24) are regenerated with their
  `make_fixture.py`; their text equals the generators' output.

Not added here: a `runs: "v4"` value for tavern options (`DungeonRunTavernOption.OfferedOn`, `KnownRuns`); lane E owns the
tavern and adds it if a v4 tavern row needs it.

## 2. Tag reservation

32638-32640 are free (no `GameTag` constant in `engine/` or `plugin/`; inside `EncounterRules.IsPrivateTag`, so the match
host never sends them). Reserved for lane X: 32638 ULDA_111 Hearthstone's escape, 32639 GILA_911 Rewind's restart, 32640
spare for the restart family. Recorded in the tag table comment of `EncounterRules.cs` and in HANDOFF section 5, which now
also lists the reserved ranges (StartOfGameRules 32600-32602, ShrineRules 32610-32629, the encounter table 32630-32659,
RuleTags 32660-32699, SwapRules 32710-32712).

## 3. Disposition of the open items (plan section 1 E)

Under the scope change every "tests", "real games" and "gate" part below is the user's.

| Item | Disposition | Owner |
|---|---|---|
| 8 act I-III twist pools | D8 pools on acts-v4h/v4t (chance 100, uncalibrated initial value); prevention gates them | lane E (schedule objects), lane R (exclusions) |
| ULDA_111 Hearthstone | Escape (D14): engine card, tag 32638, `EscapeAllowed`, `EscapeOrRestartBranch`; `runs: "v4"` row handed to lane E | lane X (+ E for the row) |
| GILA_911 Rewind | Restart (D13): engine card, tag 32639, `ConsumedTreasures`; `runs: "v4"` row handed to lane E | lane X (+ E for the row) |
| GILA_BOSS_48t Alternate Timeline | lane X implements its restart path (`RestartCarriedDamage`); re-adding it to Toki's deck (`make_f23.py`) waits for the stage-3 gate, which is a measurement: the user's | lane X, then the user |
| GILA_900p Temporal Loop, GILA_900h2 | final rejection (SoloStone adaptation: Toki keeps the CS2_034 stand-in, "NativeMatch has no client history re-sync"); reword the "phase 9" lift texts | lane X |
| ULDA_607 House Special | final rejection (no source decks; 15 authored class decks would be new content); `tavern-v1.json` `deferred` text | lane E |
| DALA_740 Wondrous Wisdomball | D16 criterion: a v4 adaptation or a final rejection | lane E |
| ULD_152 | the stale "partial" note in HANDOFF 3.4 goes at the close-out; the named-game recheck is the user's | close-out, the user |
| BOT_543 | the stale "partial" note goes at the close-out (the ledger has it implemented) | close-out |
| GILA_BOSS_43t | Damph's reward (D9): `run-v1.json` row (`offered: false`, `runs: "v4"`), the `reward` key, player-side admission | lane E |
| ULDA_BOSS_58p Jythiros | blocker moves from "phase 9 (economy)" to "client", with its reason | lane X (admission and deferral texts) |
| ULD_616, DRG_052 Lackeys | scripted; the Lackey pool then has 7 cards | lane G |
| Random generation | every `LegacyGenerationV1.Get` call site through `OfflineCardPools.ForGame` (D15) | lane G |
| Typhoon (EDR_232) without `ShrineRules.InterceptLeave` | fixed: its board removal asks `InterceptLeave` as every other leave path does | lane G |
| Kingsbane Durability (Captain Shivers) | fixed: the shuffled-back copy keeps enchantment Durability (the family-19 Sword Dance fix, applied to the shared helper) | lane G |
| Rumble champions' teammate slots | final reason, no lane: teammates stay run-player-only by admission and the 29 slots stay recorded substitutions; admitting them boss-side needs a boss teammate model, which changes no economy or scaling item of step 9 | none (later content work) |
| bosspilot ACT_RUN label | a harness label (test code): the user's, and only if byte-neutral; otherwise it stays a label note | the user |
| DALA_857 Glittering | rechecked by lane R; it stays rejected with its reason unless a boss may hold Heist treasures (D6 shares treasures player-side only) | lane R |

## 4. Rows lane R touches: F-file and writer

Hand-kept files are edited directly; a generated file is changed only through its writer, then `gen.sh`.

| Row(s) | id | F-file | Writer |
|---|---|---|---|
| Candlebeard (tier 1) | loot_12_candlebeard | F07 | hand-kept |
| Candlebeard (tier 3) | loot_31_candlebeard | F08 | hand-kept |
| George and Karl | loot_15_george_karl | F08 | hand-kept |
| Ixlid | loot_36_ixlid | F08 | hand-kept |
| Lava Chamber (room) | loot_47_lava_chamber | F08 | hand-kept |
| Tad (room) | loot_22_tad | F09 | hand-kept |
| Treasure Vault (room) | loot_52_vault | F09 | hand-kept |
| the LOOT finals | loot_25_vustrasz, loot_26_xol, loot_34_azari, loot_49_darkness, loot_99_togwaggle | F09 | hand-kept |
| Grubb | gila_23_grubb | F10 | hand-kept |
| Hagatha | gila_61_hagatha | **F01** | hand-kept |
| Icarax | ulda_40_icarax | **F01** | hand-kept |
| Trap Room (room) | ulda_44_trap_room (and loot_48_trapped_room) | F01 | hand-kept |
| Brushwood Centurion (forcedAttack) | gila_41_brushwood | F11 | `verify/tools/gen/p8f11/make_f11.py` |
| Tierra, Belnaara | dala_14_tierra, dala_07_belnaara | F14 | `verify/tools/gen/p8f14/make_f14.py` |
| Hesutu, the Heist finale | dala_73_hesutu; dala_43_norroa, dala_67_vargoth, dala_69_kalec, dala_60_khadgar | F18 | `verify/tools/gen/p8f18/make_f18.py` |
| Kasa Softspeaker (forcedAttack) | ulda_36_kasa | F20 | `verify/tools/gen/p8f20/make_f20.py` |
| Janak, Bazhial | ulda_54_janak, ulda_52_bazhial | F21 | `verify/tools/gen/p8f21/make_f21.py` |
| Armadillobot (forcedAttack) | ulda_79_armadillobot | F22 | `verify/tools/gen/p8f22/make_f22.py` |
| the nemeses | gila_48_infinite_toki, gila_49_glinda, gila_59_godfrey, gila_60_shivers | F23 | `verify/tools/gen/p8f23/make_f23.py` |
| the other lords, Tekahn | ulda_37_xatma, ulda_38_kzrath, ulda_39_vesh; ulda_67_tekahn | F24 | `verify/tools/gen/p8f24/make_f24.py` |

Deviation from the plan's table: Hagatha and Icarax are in the hand-kept F01.json (family 01 built the hero-card and
phase models on them), not in `make_f23.py` / `make_f24.py`.
