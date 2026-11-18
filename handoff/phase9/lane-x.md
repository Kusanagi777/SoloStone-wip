# Step 9, lane X: match ends, restarts and finales

Plan: `handoff/phase9/plan.md` section 3, stage 2, lane X (D11 engine side, D12, D13, D14). Scope change (the user):
implementation only. No suite (`c-ends-9`), no bosspilot mode (`ends9`) and no harness run was written or run here; the
user tests.

## 1. What landed

Every piece is read through `DungeonRun.Tuned(state)` (stamp 4 and above) or needs a card that only a `runs: "v4"` row can
give, so every released schedule and every legacy save plays and replays as before.

| Item | Engine | Run layer |
|---|---|---|
| ULDA_111 Hearthstone (escape, D14) | `DungeonRunDeferrals.cs` (e): an active treasure keyed by its own id, a self-drawing Start of Game source, playable only while `RunMatchEnds.EscapeAllowed` (a card play requirement, checked when cast). Its cast (`EncounterRules.EscapeToTavern`, `EncounterRules.S9.cs`) marks the game (tag 32638), consumes the card (`RunMatchResult.ConsumedTreasures`) and destroys the boss hero (the Treasure Vault's ending). The harvest reports `Escaped` and the boss's real remaining Health. | `DungeonRun.Outcomes.cs` `RecordEscape`: Win plus `Escaped`; no MajorsCleared, no kill credit (`BossesDefeated` skips it), no match edits, upgrades or drafts. The fight's facts are recorded as a loss records them (persistent boss damage banked, Plague Lord damage spent, Gnomebliterator casts banked), Battle Scars keeps the hero's damage, the one-fight bonus is spent, Hearthstone leaves the deck (outcome-implied, bypassing the deck rules), then an escape tavern opens on the current node; leaving it advances. `EscapeAllowed(state)`: tuned, the deck holds the card and deckMin + 1 cards, not a finale act, not the last node. |
| GILA_911 Rewind (restart, D13) | A passive treasure keyed by its own id with no enchantment. `EncounterRules.ResolveRewind`, run after the boss forms and the relay in `ResolveHeroForms`: when the run hero is dead (not a concession) and the match lists Rewind, the game is marked (tag 32639 = 1) and Rewind is consumed; the game still ends as a loss. | `RecordRestart`: Fault plus `Restarted`; restaged at PreMatch with the hero switch and the one-fight bonus kept, under a fresh seed (`RestartMatchSeed`: `StableSeed(seed, MatchStream, floor, restartIndex)` when restartIndex > 0). Rewind leaves the run. Player Health: the fight's start (Battle Scars' earlier damage kept). |
| GILA_BOSS_48t Alternate Timeline (restart, D13) | Scripted as a boss card in `DungeonRunEncounters.F23.cs`: playable only while the match lists it; its cast (`EncounterRules.RestartGame`) marks the game (tag 32639 = 2) and ends it as a tie (both heroes leave). | Same branch; `RestartCarriedDamage` = the damage the fight's own run hero took in this game (its end damage less Battle Scars' start damage), added to `PlayerCarriedDamage` of the restarted game. |
| Per-source caps and turn budget (D13) | `RunMatchPlan.Restarts`: the sources the fight may still use. | Each source once per floor (`RestartSources`); the floor's games take at most 2 × TurnCap turns (`RestartTurns`, a new guarded attribute; the restarted game's TurnCap is the rest of the budget). A restart past its cap or budget is refused and recorded as a loss (TurnLimit when the budget is spent), with a note. A persistent boss banks its damage on a restart as a loss does, and the run's Plague Lord damage is then spent (the banked remaining already counts it; on a typed seed nothing is banked and the damage is kept). |
| Tekahn ratchet and turn cap (D11) | `RunMatchPlan.FormRatchet` reaches `EncounterRules.SwapForm` through `RunMatchEnds`: a form with `healthAtMost` and no Health of its own clamps the boss's maximum Health to the threshold. | `PlanMatchEnds`: a tuned relay fight (Tekahn on acts-v4t) gets `FormRatchet` true and `TurnCap` 150. |
| Plan and end fields | `RunMatchPlan.Restarts`; `RunMatchEnds` (`GameConfig.MatchEnds`, null on every plan with the defaults); `RunMatchEnd.RestartSource` and `Turns` (optional constructor parameters); `RunHelpers.ConsumeTreasure`. | `DungeonRunState.RestartTurns` (guarded attribute, save stays version 6). |

## 2. Rows handed to lane E (run-v1.json is lane E's)

Lane X adds no `run-v1.json` row. Until lane E adds these, both treasures are content with no run row (ledger: partial,
"no run-v1.json treasure entry"). Suggested rows (lane E sets rarity, tags and scope under D6; `runs: "v4"` needs
`scheduleVersion` 4):

```json
{"id": "ULDA_111", "rarity": "rare", "active": true, "tags": ["spells", "minions"], "runs": "v4", "pool": "Tombs", "scope": {"decks": ["tombs_reno_spellslinger", "tombs_reno_spell_seeker", "tombs_reno_secret_traps", "tombs_reno_random", "tombs_finley_murloc_crusade", "tombs_finley_mighty_morphing", "tombs_finley_back_in_action", "tombs_finley_random", "tombs_elise_healing_help", "tombs_elise_swarms", "tombs_elise_insight", "tombs_elise_random", "tombs_brann_taming_dinos", "tombs_brann_beastly_bodyguards", "tombs_brann_call_for_help", "tombs_brann_random"]}},
{"id": "GILA_911", "rarity": "rare", "tags": ["spells", "minions"], "runs": "v4", "pool": "MonsterHunt", "scope": {"decks": ["mh_toki_workshop"]}},
```

## 3. GILA_BOSS_48t gate (not run: it needs games)

The restart mechanism is implemented, but the card stays out of Infinite Toki's deck and its admission row stays rejected
(`nemesis-rejected-restart`, lift `DungeonRunAdmission.AlternateTimelineLift`, equal to F23.json's "rejected" lift). The
plan's stage-3 gate is a measurement, so it is the user's: a scripted cast condition, 0 faults in 40 fair games, at most one
restart per floor, cumulative turns at most 2 × TurnCap. On a pass: admit the card for the boss (`DungeonRunAdmission.F23.cs`),
put it back in Toki's deck and drop the "rejected" entry (`verify/tools/gen/p8f23/make_f23.py`), run gen.sh and the fixture
generators, and update the c-enc-23 pins. Otherwise: a final rejection, "AI cannot value a game restart" (a "none:" lift).
Note for the gate: the engine ends the game as a tie, so the boss AI scores the cast as a tie.

## 4. Final rejections and rewording

- GILA_900p Temporal Loop and GILA_900h2: rejected for good (a SoloStone adaptation: Toki keeps CS2_034; NativeMatch has no
  client history re-sync). `TurnRestartLift` is now a "none:" lift; the texts in the admission rows, F01.json, curation.json
  and the engine comments say so.
- ULDA_607 House Special: rejected for good (no source decks; 15 authored class decks would be new content):
  `tavern-v1.json` "deferred" reason, curation `data:bombastic-decks-absent` and E25.
- ULDA_BOSS_58p Jythiros: the blocker moves from "phase 9 (economy)" to "client" (`RiddleLift`, `make_f21.py`, curation E21).
- ULDA_111 and GILA_911 leave the "rejected" maps of curation F15 and E25 (they behave now) and the ledger's phase-9
  deck-edit list (`tools/restoration-ledger/Program.cs`).

## 5. [WIN] (Windows-only, not built here)

- `plugin/NativeMatch.cs`: `DungeonRunTurnLimit` (the plan's TurnCap); the chained-choice null guard
  (`next.Choices?.Contains(id) == true`, the old `:6634`, now `:6640`).
- `plugin/ClientBridge.cs` `TickDungeonRun`: the turn limit reads `match.DungeonRunTurnLimit`.
- `plugin/LocalLadder.cs` `FinishDungeonRun`: a harvest with `Restarted` is recorded as a Fault (the escape is the engine's
  Win as it is).
- `EndDungeonRunFight` takes no new reason: the engine ends an escape or a restart game by itself.
- Still to do on Windows: the escape and restart screens (the client shows a victory for an escape and a defeat or tie for a
  restart before the run screen restages the floor), the escape tavern, the 150-turn relay cap.

## 6. Notes for the harness (test code, the user's)

- A harness that reads the outcome from the engine game must report a restart as a Fault (as LocalLadder does) and pass
  the harvest; the run layer also accepts the mark on any reported outcome but a concession.
- The engine scores Hearthstone's cast as the run player's win, so a player pilot that searches will cast it at once
  whenever it is playable; the pilot should leave it out of its options or score it as an escape.
- The bosspilot form check (`Program.cs:299-303`) should apply the threshold clamp when `plan.FormRatchet` is set, and read
  `plan.TurnCap` instead of its literal 100.
