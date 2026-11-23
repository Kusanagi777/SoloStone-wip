# Dungeon Run (SoloStone mode)

> **Status**: In Design — initial specification, framework scope
> **Author**: Kusanagi777 + Claude (game-designer / systems-designer roles)
> **Last Updated**: 2026-11-17 (step 9: the economy, acts-v4h/v4t; save versions 4-6 documented)
> **Last Verified**: 2026-11-09, against SoloStone `main` @ `0f6a476`
> **Implements Pillar**: Deckdelve P1 "every run builds a deck you couldn't have planned", P3 "lose fast, learn fast", P4 "readable, deterministic rules"
> **Ported from**: `dungeon-run-ccg/design/gdd/` (Deckdelve) — `run-structure.md`, `reward-draft.md`, `treasures.md`, `content.md`, `rng-event-log.md`, `meta-progression.md`

## Summary

Dungeon Run is a single-player roguelike mode inside SoloStone. Each run is up to 8 fights against escalating bosses. The player starts with a thin 10-card class deck and grows it after each win by picking one of three curated 3-card bundles. Run-long passive treasures arrive after floors 1, 3 and 5. One loss ends the run. Every fight is an ordinary SabberStone match, started through SoloStone's existing local-match path, with the run layer adding:

- the run deck
- boss health and hero power
- start-of-match treasure effects

The mode is isolated. It never reads or writes the collection, ladder, gold, packs, dust or quests.

> **Quick reference** — Layer: `Feature` · Priority: `MVP` (framework), content `Post-MVP` · Key deps: SabberStoneCore `Game`/`Cards`, `ClientBridge` match staging, `NativeMatch.Start`, NPC pilots, `StableSeed`, profile directory
> Category: Gameplay (inferred; SoloStone has no systems index). Workflow tier: standard.
> **Scope of this document:** the run engine, draft, save, match integration and UI flow. Boss and treasure *content* (Deckdelve's 16 bosses and 20 treasures) is a later data pass. This spec defines the data schemas and engine hooks that content must fit (§ Core Rules R7, R8).

## Overview

SoloStone today has one way to play: a 30-card constructed deck against an NPC, on the ladder or in Casual. Dungeon Run adds a second loop built from the same parts. The deck is drafted, not built. The opponent is a boss with a rule twist, not a mirror of a ladder deck. A run is a 20–45 minute arc that ends in victory or in a loss you learn from.

Technically, the mode is a pure-C# **run layer** (state, draft, seeds, save) sitting above the existing match pipeline. It hands `NativeMatch` a prepared player deck, boss deck, boss hero and a list of *match modifiers*. It then reads back only the match outcome. The run layer must not depend on Unity or client types, so it can be tested headlessly against `SabberStoneCore`.

Without the mode, SoloStone has no non-ladder progression and no reason to play the card pool outside constructed. The hidden home **Modes** button already records this as backlog *"#8 separate roguelike"* (`plugin/NativeHiddenEntries.cs:24`).

## Detailed Design

### Core Rules

**R1 — Run state.** A run is a `RunState` value with these fields:

- `runSeed` (ulong)
- `class`, `starterDeck` and `power` (the run setup, R4)
- `floor` (1–8)
- `deck` (ordered card-id list)
- `treasures` (ordered id list, in pick order)
- `takenBundles`
- `bundleCooldowns`
- `bossSequence` (8 boss ids, fixed at run start)
- `drafts` (an offer/pick record per draft)
- `matchSummaries`
- `phase`
- `dataVersion`

Nothing else persists between fights. Specifically, hero health, armor, board, hand and mana do not persist; each match starts fresh (Deckdelve RS R1).

**R2 — Phases.** `run_setup → pre_match → in_match → post_match → (treasure_draft) → bundle_draft → pre_match … → victory | defeat`. These are spelled out in § States and Transitions. `run_setup` is three choices in order: **class**, then one of the class's **starter decks**, then one of its **hero powers**, then a confirm page (R4).

**R3 — Structure.**

- 8 floors, one boss each.
- Bosses come in 3 bands: floors 1–3 are band 1, floors 4–6 band 2 and floor 7 band 3 (`band_split [3,3,1]`). Floor 8 is drawn from a separate finals pool. *(Changed 2026-11-09 from `[3,3,2]` to add the finals pool; see § Content.)*
- Each band draws its bosses without replacement from that band's pool. The draw is a seeded shuffle (Formula F2).
- **MVP cut:** 4 floors, `band_split [2,1,1]`.

**R4 — Class, starter deck and hero power.** *(Rewritten 2026-11-09: the 13 named heroes became 9 classes; Death Knight and Demon Hunter were added the same day, because the client's class tray offers them.)*

- The player picks one of the **11 classes**. Every class is always selectable. Its hero is the class's standard client hero, shown under the class's run title (The Wilder, The Ranger, ...).
- Each class has **3 themed 10-card starter decks** (`deckOptions`). Their cards pass the pool rules (R6) and the class rule `CardClassRules.IsAllowedInDeck` (so a multi-class card is allowed in any of its classes), and duplicates are allowed. Every deck has the target curve of three 1-drops, three 2s, two 3s, one 4 and one 5, except Lantern Road (two 1s, four 2s). The first deck is the default.
- Each class has **3 hero powers** (`powerOptions`): its standard class power first (`<hero>bp`, which always has a client definition), then two run powers. Run powers are scripts on unscripted client shells (§ Content); none is choose-one or a Secret, and none uses a scripted lambda, so they clone and replay deterministically.
- The starter is copied into the run deck at run start; the run never changes the data deck.
- Decks 2 and 3 and powers 2 and 3 unlock per class (R13). A locked deck or power shows greyed, with its unlock hint.
- The four version-1 special heroes are folded into their classes: their decks became deck 3 of Hunter (Snareline), Paladin (Lantern Road), Mage (Survey Party), Rogue (Wayfinder) and Warrior (Shieldhire Company), and their powers became class powers (Set Snare for Hunter, Survey for Mage, Shortcut and Ambush Point for Rogue, Shield Wall and War Cry for Warrior). Shrines and signature cards are no longer chosen; they stay registered for version-1 runs (§ Save versions).

**R5 — Rewards per floor.**

- After a win on floors 1–7: a **bundle draft**. The player sees 3 bundles and picks 1. All 3 cards in the bundle join the deck. There is no skip.
- After a win on floors 1, 3 and 5, a **treasure draft** comes first: 3 offered, pick 1.
- There is no reward after floor 8; the run is won.
- Deck size at the start of floor f is `10 + 3(f−1)`, rising to 31 by floor 8. There is no cap and no card removal (F4).

**R6 — Card pool and bundle data.**

- Bundles are hand-curated data in `data/dungeon-run/pool-v1.json` (`bundles`). Each bundle has an id, a `class` (or `NEUTRAL`), one `theme` tag and exactly 3 card ids.
- The shared draft catalog `data/dungeon-run/catalog-v1.json` is generated from those bundles and the original adventure buckets by `tools/dungeon-run-catalog` (report: `docs/design/dungeon-run-catalog.md`). The plugin loads the catalog; a catalog whose `poolBundlesHash` no longer matches `pool-v1.json` disables the mode as stale. Pool entries are checked with the same rules below (class set, grants, no rune cost), and a candidate the client lacks is dropped at load and logged.
- Each enabled class's pool is its own class bundles plus the shared neutral bundles. The Deckdelve shape is 8 class and 12 neutral bundles.
- Validation runs when the mode loads. Any failure disables the mode with a named reason; it never fails silently. Every card id in starters and bundles must pass all of these:
  1. It exists in `Cards` (`Cards.FromId` does not throw).
  2. The client has an entity definition for it (`ClientBridge.ClientKnowsCard`).
  3. It is in the active meta (`OfflinePoolPolicy.AllowsCard`).
  4. `Card.Implemented == true`. An unscripted card would play as a vanilla body.
  5. Its class is allowed in the bundle's or starter deck's class (`CardClassRules.IsAllowedInDeck`); a neutral bundle takes neutral cards only.
  6. The **admission table** admits it for the run player (Edge Cases E5).
- Run cards are not owned cards. Drafting never checks or changes the collection.

**R7 — Bosses (framework).** A boss is a `BossDef` in `data/dungeon-run/bosses-v1.json`. Its fields are:

| Field | Meaning | Client-def requirement |
|---|---|---|
| `id`, `band`, `theme` | Identity and pool placement | — |
| `name` | Display name, shown on SoloStone's own run screens and on the opponent nameplate | — |
| `heroCard` | Engine hero card for the boss's portrait | Must be a client-known hero entity |
| `health` | Starting health. Default by band is 30 / 40 / 50 (F3) | — |
| `heroPower` | Optional hero-power card id, which replaces the default | Must be client-known |
| `deck` | 30 card ids, piloted through the existing NPC `auto:` plan | Every card client-known, `Implemented` |
| `aiProfile` | NPC personality and skill ids from `data/npc/` | — |
| `modifiers[]` | Boss passives as match modifiers (R8), owned by the boss side | Display shell must be client-known |

**MVP content stand-in:** until the content pass, bosses are existing `data/npc/decks-v1.json` decks with band health and no modifiers. That is enough to exercise the whole framework. Deckdelve's boss-only cards have no client definitions. They must be re-expressed with existing client cards or as modifiers (Open Question Q1).

**R8 — Match modifiers (the treasure/passive framework).**

- A treasure, or a boss passive, is a list of **match modifiers**. The run layer applies them to the engine `Game` after `new EngineGame(config)`, in pick order, at a fixed point in `NativeMatch.Start`.
- They run in two phases around `StartGame()`. The pre-start phase holds stat and setup overrides. The post-start phase holds enchantments and summons. This mirrors the existing `DebugScenario.ApplyOpening` seam (`NativeMatch.cs:784-787`).
- The supported modifier kinds and the SabberStone primitive behind each are:

| Kind | Example (Deckdelve treasure) | Engine primitive |
|---|---|---|
| `hero_health_delta` | Iron Constitution, +10 health | `Hero.BaseHealth` (`Character.cs:778`) |
| `start_mana_crystals` | Hollow Crown, +1 crystal | Controller base mana before the first turn |
| `opening_hand_delta` | Bottomless Satchel, +2 cards | Extra draws after the mulligan, with no draw triggers |
| `hero_power_replace` | Worn Signet (cost change), boss powers | `Controller.ReplaceHeroPower` (already used by `DebugScenario.cs:390`) |
| `player_enchant` | Vanguard's Banner, Stampede Horn, Grave Tithe | `QuestRules.AddPlayerEffect(controller, source, enchantId)` (`QuestRules.cs:47`) |
| `start_in_hand` | Living Armory (random Legendary) | Generate the card into the hand from a seeded pool (F5 stream) |
| `start_on_board` | Boss setups | `EngineEntity.FromCard` + `SummonBlock`, the same as `DebugScenario` `Board()` |

- `player_enchant` and `hero_power_replace` need an engine card script and a client-known entity id to show. Content work picks a "display shell" entity per modifier (Q1).
- Modifiers are recorded in the match's playtest record with the run seed, so replay stays exact. This is the same rule `DebugScenario.Canonical()` follows today.

**R9 — Match rules.** These are standard SoloStone and SabberStone rules: 30 health, 10 mana, hand 10, board 7, standard fatigue, mulligan on. They have two run-specific differences:

1. **The player always goes first.** The `StartPlayer` override replaces `StableSeed.MatchFirstPlayer`. The boss therefore gets the standard second-player hand and The Coin.
2. **A tie (both heroes dead) or the engine's turn limit counts as a loss for the player.** This is Deckdelve DD-M12, enforced in the run layer and not in the engine.

The format stays `FT_WILD`, as for every SoloStone match.

**R10 — Outcome handling.**

- **Win:** advance to rewards.
- **Loss, concede or tie:** `defeat`. The run is archived and cannot be resumed.
- **Fault:** an engine exception or a refused staging. The floor is not lost; return to `pre_match` with the same match seed.
- **Abandon:** legal only outside `in_match`. It counts as a defeat.

**R11 — Save and resume.**

- Run state lives in a sidecar file in the active profile's directory: `<profileDir>/dungeon-run.xml`. It is not part of `LocalProfileData`, so the strict V15 profile schema and its `Commit` validation are untouched.
- Writes use the same pattern as `LocalProfile.Commit` (`LocalProfile.cs:1863`): a temp file, flushed, then `File.Replace`.
- The file is saved after run setup, after every match outcome, and after every draft pick.
- The file is versioned; § Save versions describes the upgrade from version 1.
- `in_match` is never saved as a resumable state. On load, a run whose phase is `in_match` resumes at that floor's `pre_match` with the same match seed. The player replays the same shuffle against the same boss (F1 makes this deterministic).
- Unlock state and the run archive (cap 200, as in Deckdelve) live in the same sidecar.

**R12 — Isolation from SoloStone systems.** A run match:

- never creates a `LocalMatchRecord` on the ladder
- never calls `Settle` reward rolls
- never feeds achievements, card mastery, ladder milestones or NPC memory

`LocalProfile.RecoverInterrupted` (`LocalProfile.cs:879`) must recognise run matches and leave them alone, the same way it already carves out `DebugScenario` matches. Otherwise a crash would book an `Abandoned` ladder loss.

**R13 — Meta-progression.** There are no currency and no cards. Persistent effects are:

- run archive (class, starter deck, power, seed, floor, last boss, treasures; since phase 7 the board shrine and its upgrades in pick order, kept apart from the treasures)
- lifetime stats by class (runs, wins, best floor)
- unlocks expressed as `{key, condition}`, using Deckdelve's grammar (`runsWon`, `floorReached`, `classesWon`, `anyOf`, `allOf`), optionally scoped to one `class`

**The class ladder** (`classUnlocks` in `run-v1.json`, expanded at load to `<class>_floor3`, `_floor4`, `_floor5`, `_win` for each class, 44 keys):

| Milestone with the class | Unlocks |
|---|---|
| default | deck 1 and the basic power |
| reach floor 3 | deck 2 |
| reach floor 4 | power 2 |
| reach floor 5 | deck 3 |
| win a run | power 3 |

"Reach floor N" is the floor a run ended on (`BestFloor`). The challenge boss unlocks after winning runs with **4 different classes** (`classesWon: 4`). Unlocks are re-evaluated when a run ends, when a save is loaded and when the run screen opens, so progress already in the stats (or carried over from version 1) shows at once. The run summary names what the run unlocked.

The unlocked pool is snapshotted at run start. Personal bests and unlocks count only for random seeds, not typed ones.

### States and Transitions

| From | Event | To | Save? |
|---|---|---|---|
| (none) | Player opens Dungeon Run with no active run | `run_setup` (class page) | — |
| `run_setup` | Class, deck, power chosen and confirmed (Back walks one step back) | `pre_match` (floor 1) | yes |
| `pre_match` | "Fight" pressed → staging succeeds | `in_match` | no |
| `pre_match` | Staging refused (missing def, invalid deck) | `pre_match` + error | — |
| `in_match` | Player win | `post_match` → `treasure_draft` (floors 1, 3, 5) or `bundle_draft` (other floors 1–7) or `victory` (floor 8) | yes |
| `in_match` | Loss / concede / tie / turn cap | `defeat` | yes |
| `in_match` | Fault or crash | `pre_match` (same floor, same seed) | — |
| `treasure_draft` | Treasure picked | `bundle_draft` | yes |
| `bundle_draft` | Bundle picked | `pre_match` (floor+1) | yes |
| `pre_match` / drafts | Abandon confirmed | `defeat` | yes |
| `victory` / `defeat` | Summary dismissed | (none); run archived | yes |

### Interactions with Other Systems

- **ClientBridge match staging.** `PrepareLocalMatch` builds `NativeMatch` from `LocalDeck`s, and `LocalDeck.Build` requires exactly 30 cards (`LocalDecks.cs:46-49`). A run deck is 10–31 cards, so the mode needs a parallel `PrepareRunMatch`. It applies the same client-def checks (`ClientBridge.cs:432-433`) to the run deck, boss deck, boss hero and every modifier display entity. It then calls `StartLocalMatch` with `GT_VS_AI`, as today.
- **NativeMatch.Start.** It gains an optional `RunMatchSetup` (player deck list, boss def, modifiers, `StartPlayer` override). It builds `GameConfig` exactly as now (`NativeMatch.cs:735-756`) and applies modifiers around `StartGame()`.
- **NPC pilot.** The boss deck is exposed through `LocalDecks.ExternalDecks`, its profile through `ProfileForDeck`, and it is piloted by the `auto:<deckId>` plan. A custom boss hero power is worth 0 to the evaluator, because `CardFrequencies.FaceDamage` is hard-coded (`DeckPlanEvaluator.cs:363`). The content pass must add values for each boss power.
- **Home UI.** The hidden `home.modes` entry is re-enabled and relabelled **Dungeon Run**, opening the run hub. Nothing else on Modes is exposed.
- **Results flow.** The client's own end-of-game screen plays as usual. The SoloStone reward presentation is skipped: there is no `PendingRewardPresentationId`. Control returns to the run hub, not the deck picker.
- **Format lock and collection.** These are untouched. The run never opens the deck editor or the collection.

## Formulas

**F1 — Seed derivation.** Every random choice in the run layer comes from `runSeed` through `StableSeed.Derive`. No `System.Random`, `Guid` or clock is used.

```
matchSeed(f)        = StableSeed.Derive(runSeed, "match", f)        → GameConfig.RandomSeed
bossShuffle(b)      = StableSeed.Derive(runSeed, "bosses", b)
draftSeed(f, step)  = StableSeed.Derive(runSeed, "draft", f, step)  step ∈ {treasure, bundle}
modifierSeed(f, i)  = StableSeed.Derive(runSeed, "mod", f, i)
```

| Variable | Type | Range | Meaning |
|---|---|---|---|
| `runSeed` | ulong | any; random at `run_setup` unless typed | One seed per run, shown as 16 hex digits |
| `f` | int | 1–8 | Floor |
| `b` | int | 1–3 | Band |
| `i` | int | 0–n | Modifier index in pick order |

Replay guarantee: the same seed, class, `dataVersion` and picks give the same bosses, offers and match shuffles. The NPC's wall-clock guards (`NpcCatalog.cs:14`) can make AI turns differ. That is accepted, and the run guarantee covers run-layer outputs only.

**F2 — Boss sequence.** For each band b, sort that band's boss pool by id and shuffle it with `bossShuffle(b)`. Take the first `s_b` bosses, where `s = band_split`. Pools must be at least as large as `s_b` (Deckdelve 6/6/4 for `[3,3,2]`).

**F3 — Boss health.** `health = bandHealth[band] + floorHealthBonus × (f − firstFloorOfBand)`. Defaults: `bandHealth = [30, 40, 50]`, `floorHealthBonus = 0`. A boss's own `health` field overrides both.

**F4 — Deck size.** `deckSize(f) = 10 + 3(f − 1)`. It is 10 at floor 1, 13 at floor 2 and 31 at floor 8. 21 cards and 3 treasures are drafted in a full run.

Cut from Deckdelve's 15-card start (2026-11-09). Drafted cards are then 23% of the floor-2 deck instead of 17%, and 68% of the final deck instead of 58%. So the draft shapes the deck sooner, which serves pillar P1. The cost is earlier fatigue on the first floors (E2).

**F5 — Bundle weight.** Ported from Deckdelve RD F1:

```
weight     = max(1, round(100 × affinity × bandMult / 100))
affinity   = 1 + 0.25 × min(nTag, 6)                 (1.0 – 2.5)
bandMult   = table[band][costBand(avgCost)]
```

| band | low (avg < 2.5) | mid (2.5–4.0) | high (> 4.0) |
|---|---|---|---|
| 1 | 120 | 100 | 70 |
| 2 | 100 | 110 | 100 |
| 3 | 80 | 100 | 130 |

`nTag` is the number of deck cards drawn from bundles sharing this bundle's theme. Starter cards carry no theme unless the starter data tags them.

The offer is filled slot by slot:

1. **Slot 1:** a weighted pick. It is forced to a dominant-theme bundle after `PITY_WINDOW = 2` drafts without one.
2. **Slot 2:** a weighted pick.
3. **Slot 3:** forced to a theme different from slot 1 if slots 1 and 2 share a theme.

Candidate rules:

- A taken bundle is never offered again.
- An offered but untaken bundle is on cooldown for `reofferAfterFloors = 2`.
- The displayed order is sorted by id.

**F6 — Treasure weight.** Ported from Deckdelve RD F2:

```
weight = round(rarityWeight × (100 + 50 × affine) / 100)
```

- `rarityWeight` is common 70 or rare 30.
- `affine` is 1 if the treasure shares a plan tag with the class's `classTags` plus the starter deck's `treasureTags`, or if it is a scoped treasure, else 0.
- A treasure with a `scope` (`classes`, `decks`, `powers`) is a candidate only when the run matches any listed class, starter deck or power: Keen Eye with Set Snare, Rimebite or the Rimeward deck; Hunting Net with Set Snare. A `legacy` treasure is never offered.
- Slot 1 is forced to a universal treasure when one exists.
- A held or already-offered treasure is never offered again in that run.

**F7 — Difficulty targets** (tuning goal, not a rule). Floor win-rate targets are `[0.90, 0.85, 0.80, 0.70, 0.65, 0.60, 0.50, 0.40]`. That gives about 3% full-clear and a median death on floors 4–5. The MVP has 4 floors, and its targets are the first four values. On the step-9 schedules (16 and 19 fights) the run target R0 and its slot decomposition replace these values (§ Step 9: the economy).

## Edge Cases

- **E1 — Client definition missing** after a client patch removes or renames an entity. Load-time validation (R6) disables the mode with the failing ids. `PrepareRunMatch` re-checks before every fight. A run in progress whose deck now fails stays saved and is marked *blocked*, never deleted.
- **E2 — Thin deck and fatigue.** The player goes first and draws 3 cards plus 1 each turn. A 10-card deck therefore runs out on turn 7 and takes fatigue from turn 8; the 13-card floor-2 deck takes fatigue from turn 11. Each card of opening-hand bonus brings fatigue a turn earlier. This is intended pressure on floors 1–2 and is not padded. If floor-1 losses come mostly from fatigue, raise the starter size; do not add padding. A boss's 30-card deck never fatigues first, so long control bosses are a band-1 risk: check them in the F7 tuning pass.
- **E2a — Small deck and the opening bonus.** With a 10-card deck, a second-player opening of 4 cards plus the 5-card maximum bonus deals 9 of the 10 cards. The engine allows this. The treasure content pass should keep any opening-hand treasure at +2 or less.
- **E3 — Duplicate legendaries.** Allowed in run decks, as in Deckdelve. The run deck bypasses `LocalDecks.CreateCustom` copy limits. Cards whose own text checks for duplicates (highlander checks) are decided by the admission table (E5).
- **E4 — Tie, or both heroes dead.** The client may show "Tie". The run layer records a defeat (R9).
- **E5 — Admission table.** Secrets and cards that depend on the deck are decided by one table, `plugin/DungeonRun/DungeonRunAdmission.cs` (phase 2):
  - A card is a *subject* when it is a Secret, has tag 997 or a sideboard, has a Secret or deck phrase in its text ("secret", "no duplicates", "start of game", "if your deck", "in your deck", "questline", "deckbuilding", "sideboard", the rewind phrases), or is listed. The phrases only detect; the verdict is the table's.
  - Each rule names a family (`secret-card`, `secret-from-deck`, `secret-generated`, `highlander`, `not-started-in-deck`, `from-own-deck`, `deck-wide-change`, `start-of-game-draw`, `empty-deck`, `turn-rewind`, ...), the scopes it is admitted in (player cards, boss decks and powers, generated cards), the reason, and the numbered goal phase that lifts a rejection (3 shared draft catalog, 5 deck mutation, 6 heroes/powers/treasures, 7 shrines/teammates). A subject with no rule is rejected as `unclassified`. A rule with a pending requirement (`Requires`: an AI or script step not done yet) admits no scope until its lift; the table refuses to load otherwise.
  - `DungeonRunValidator` applies it to starter and bundle cards, player powers and their tokens, legacy (version-1) powers, active treasures and their tokens, passive treasure ids and their effect shells, and every run content shell that no boss names (shrines included) in the player scope, and to boss decks, boss powers and the content they name in the boss scope. The message is `admission <family>: <reason>; lifted by <phase>`.
  - Generated cards: the Generated scope is classified over the engine's whole card pool, not only the table's ids, so a detected card with no rule is blocked too. `RunMatchPlan.ExcludedGenerated` (`DungeonRunAdmission.GeneratedRejects`: rejected and not admitted for the run player) becomes `GameConfig.ExcludedGeneratedIds` and applies to both sides; `RunMatchPlan.ExcludedGeneratedBoss` (`GeneratedRejectsBoss`: rejected for generation but admitted for the player, a boss AI limit such as the deck-wide changes) becomes `GameConfig.Player2ExcludedGeneratedIds` and applies only when the boss generates. The engine reads the generating controller from the task being processed (`TaskQueue.CurrentController`, else the current player).
  - The restoration ledger's `ai_support` column is derived from the same table.
  - Still rejected outright: deck-size changers (tag 997 Fabled and Renathal-style 40-card cards) and sideboard cards (they need `Player1Sideboards`), both by their own checks as well.
  - Death Knight runes: a Death Knight starter deck may need at most 3 runes in all (`RuneRules.IsLegalForClass`, checked at load), and no bundle card has a rune cost, so no draft can push a run deck past 3 runes. The engine has no in-match rune rule; runes are deckbuilding only.
- **E6 — Crash or quit during a fight.** On resume the player is at `pre_match` with the same seed, so the match is replayable but not a fresh roll. A concede is a loss and never a reset.
- **E7 — Profile switch or delete mid-run.** The run belongs to the profile directory. Switching profiles shows that profile's run. Deleting a profile deletes its sidecar along with the directory.
- **E8 — Empty or short pool.** Fewer than 3 eligible bundles: offer what remains. Zero: skip the bundle step and log it. A boss band pool smaller than `s_b`: validation failure (E1 path).
- **E9 — Modifier fails to apply** (script missing, entity refused). This is a fault (R10), not a loss. The floor is restaged, and the modifier id is logged.
- **E10 — Generated cards outside the pool.** Discover and random-generation cards can create cards outside the active meta. That is allowed in-match, as on the ladder. It never adds to the run deck.
- **E11 — `dataVersion` mismatch.** A saved run whose bundle or boss data version is older than the shipped data continues on its snapshotted ids if they still validate. Otherwise it is blocked (E1).

## Dependencies

**Upstream (this mode needs):**

- **SabberStoneCore:**
  - `GameConfig`, `Game.StartGame`, `Cards.FromId` and `Cards.All`, `Card.Implemented`
  - `Hero.BaseHealth`, `Controller.ReplaceHeroPower`, `QuestRules.AddPlayerEffect`
  - `OfflinePoolPolicy.AllowsCard`
- **facts/violet-hold-2026-09.json:** the only card source. No new cards are added, because the facts file is frozen per meta. Every run card, boss hero, hero power and modifier entity must already exist there and in the client.
- **Plugin:**
  - `ClientBridge.PrepareLocalMatch` and `StartLocalMatch` (a new run-aware sibling)
  - `NativeMatch.Start` (a new setup parameter)
  - `LocalDecks.ExternalDecks`, `NpcCatalog`, `NpcPilots`, `DeckPlans` `auto:`
  - `StableSeed`, `LocalProfile.RecoverInterrupted` (a carve-out)
  - `NativeHiddenEntries` (`home.modes`), the profile directory from `ProfileCatalog`
- **Data (new):** `data/dungeon-run/run-v1.json` (rules, classes, treasures, bosses, unlocks, version-1 save map; schema 2) and `pool-v1.json` (starter decks, bundles, band-1/2 boss decks; schema 2). The file names are fixed by the plugin's embedded resources.

**Downstream (depends on this mode):** Boss and treasure content pass (Q1). Future run-specific achievements (`LocalAchievementCatalog` would need new definitions; out of scope for v1).

**Not dependencies:** LocalEconomy, LocalShop, packs, crafting, ladder, card mastery and the deck editor. Isolation is a requirement (R12).

## Engine Changes (SabberStoneCore)

These were made on branch `feat/dungeon-run-engine` and are uncommitted. All of them are opt-in: a `GameConfig` that sets none of the new fields builds the same game as before.

| Change | Where | Serves |
|---|---|---|
| `Player1HeroPower` / `Player2HeroPower` (Card) | `Config/GameConfig.cs`; applied through `AddHeroAndPower(hero, power)` in the `Game` constructor | R7 boss hero power, `hero_power_replace` |
| `Player1HeroHealth` / `Player2HeroHealth` (int?) | Same; sets `Hero.BaseHealth`, which emits a HEALTH tag change to the client history | R7 boss health, F3, `hero_health_delta` |
| `Player1OpeningHandBonus` / `Player2OpeningHandBonus` (0–5) | `Game.BeginDraw`; dealt like ordinary opening cards, so they can be mulliganed | `opening_hand_delta` |
| Construction-time validation | `Game.ValidateRunSetup`: the power must be a HERO_POWER card, health at least 1, bonus between 0 and 5 | E9: a bad setup fails before any entity exists |
| Start-of-game passive hero powers | `Game.ActivateStartingPassiveHeroPowers` runs in `MainBegin` after Start of Game effects, first player first, in its own TRIGGER block. `HeroPower.IsPassiveHeroPower` also reads the registry | Boss passives shown as passive hero powers |
| `CardSets/DungeonRun/DungeonRunCardsGen` | Registered last in `CardDefs`. Holds `PassiveHeroPowers` (empty) and an empty `AddAll` | Home for boss and treasure scripts (Q1 content pass) |
| `Player1ExcludeGeneratedSecrets` / `Player2ExcludeGeneratedSecrets` (bool), with `ExcludeGeneratedSecrets` as an alias that sets both (and reads true only while both are set); read per generating controller (`Game.BlocksGenerated`, `Game.ExcludesGeneratedSecretsFor`) | `Config/GameConfig.cs`; filters `DiscoverTask`, `RandomCardTask.GetCardList` (so `RandomCardTask` and `ChangeEntityTask`, e.g. Shifting Scroll), `CastRandomSpellTask`, the class-pool and random-Secret helpers in `SpecificTask` (which now also honour `ExcludedGeneratedIds`), and the game-level `OfflineCardPools.Candidates` / `SampleDistinct` / `Pick`. `DungeonRunSetup.Configure` sets player 1 from `RunMatchPlan.PlayerGeneratedSecrets` and player 2 always | A legacy run blocks both sides (its boss's Secret worlds are complete only while the player's Secrets come from the run pool). A catalog run (phase 3) lets the player generate Secrets: the boss's prior adds every Secret that generation can yield (`DungeonRunAdmission.CatalogSecretPrior`). The boss side stays blocked (phase 8) |
| `ExcludedGeneratedIds` (sorted id set), read as `Game.ExcludedGeneratedIds` | `Config/GameConfig.cs`; `Game.BlocksGenerated` and `Game.FiltersGenerated` cover the same paths as `ExcludeGeneratedSecrets`. `DungeonRunSetup.Configure` copies `RunMatchPlan.ExcludedGenerated` | Admission table, generated scope: cards that would do nothing (Secret generators) or that the boss's own-deck ledger cannot follow are never generated in a run match. Empty outside run matches |
| Diamond Spellstone (`LOOT_507`, in a Priest bundle) cast with an empty graveyard | `SpecificTask.DiamondSpellstone` returned `list[0]` of an empty list | Was a match fault; now it resurrects nothing |

The construction fields are `[field: NonSerialized]` construction inputs: clones and snapshots carry the resulting entity state. `ExcludeGeneratedSecrets` and `ExcludedGeneratedIds` are the exception: they are rules for the whole game, so they are serialized, and clones and snapshots keep them. The engine source stamp changes with these files, as it does with any engine edit.

**Verified** with a scratch console harness against the built engine, using the real card data:

- The default game is unchanged: 30 health and the standard hands.
- 10-card and 15-card decks are accepted. A 10-card deck with the maximum +5 bonus, going second, deals 9 cards plus The Coin and leaves 1 card in the deck.
- Boss health 40 shows in the hero entity and in the power history, and survives `Clone`.
- The replaced power is owned by the boss and usable on the boss turn.
- The +2 opening bonus works.
- The same seed gives the same hands.
- All four kinds of invalid setup throw.
- None of the 11 class powers counts as a start-of-game passive.
- Life Tap registered as a passive fires once at game start (40 → 38, plus a card) and is not clickable. This holds with and without a mulligan and with either side going first.

**Not verified:** the in-client display of these changes, which is AC8 and needs a running client. The harness is throwaway; it is not checked in, since SoloStone's test suites are not public.

**Still needed outside the engine:** `RunMatchSetup` in `NativeMatch.Start` to fill these fields, and `PrepareRunMatch` in `ClientBridge` (§ Interactions). Treasure enchantments use the existing `QuestRules.AddPlayerEffect` and need no engine change beyond their scripts.

## UI Requirements

The client has no adventure scenes the plugin can drive (the plugin touches only HUB, TOURNAMENT, GAMEPLAY, COLLECTIONMANAGER, PACKOPENING and LOGIN). All run screens are plugin-built, using the existing UI kits (`ProfileFormKit`, `NativeButtonKit`, the patterns in `LocalCollectionUI`).

1. **Run setup:** class (6 per page), then deck, then hero power, then confirm. Locked decks and powers are greyed planks; their unlock hints are in the caption. A native Adventure picker can replace these planks through the same API (`DungeonRunRuntime.Classes`, `DecksFor`, `PowersFor`, `NewRun`; § Implementation Status).
2. **Run hub:**
   - the run line: class title, starter deck and power
   - floor track showing 8 floors in their bands and the next boss's name, health and passive text
   - deck list and treasure list
   - Fight and Abandon buttons
3. **Treasure picker:** 3 cards, pick 1. Text comes from treasure data.
4. **Bundle picker:** 3 bundles of 3 card faces each, with the theme label. It reuses client card visuals where the kit allows.
5. **Run summary:** victory or defeat, floor reached, bosses beaten, final deck, treasures, a copyable seed, and what the run unlocked.

The plank menu's caption is laid out by hand above the planks, not as a slice of the native slice stack (which dropped it to the first plank's position, behind the top planks); the board grows both ways to cover it, and the header plate is lifted by the board top's change so it stays on the top edge. Native layout never moves the board, border or header back, so every layout first restores their prefab positions: a rebuild (each click) starts from the same frame. Each build logs `DUNGEON_RUN_UI layout caption=... planks=... frame=... board=... header=... lift=... lines=...`; `frame`, `board` and `header` should be the same between two rebuilds of the same page.

In-match, the boss name appears on the opponent nameplate (`OpponentName`). Boss and treasure passives appear only through their display-shell entities; the run hub shows their full text before the fight.

## Acceptance Criteria

- **AC1** — *Given* a new run with seed S, class C, starter deck D and power P, *when* the run is started twice from scratch with the same picks, *then* the boss sequence, every draft offer and every `matchSeed(f)` are identical.
- **AC2** — *Given* floor f starts, *then* the player's engine deck has exactly `10 + 3(f−1)` cards plus one per active treasure or signature held, matching `RunState.deck`.
- **AC3** — *Given* a win on floor 1, 3 or 5, *then* a treasure draft of 3 distinct unheld treasures appears before the bundle draft. *Given* a win on floor 2, 4, 6 or 7, *then* only a bundle draft appears. *Given* a win on floor 8, *then* `victory`.
- **AC4** — *Given* any player loss, concede, tie or turn-limit end, *then* the run moves to `defeat`, is archived, and cannot be resumed.
- **AC5** — *Given* the game is killed during a fight, *when* SoloStone restarts and the run hub opens, *then* the run is at that floor's `pre_match` with the same match seed. *And* no ladder or Casual match record, abandon or loss was written.
- **AC6** — *Given* a run match completes, *then* gold, packs, dust, ladder rank, card mastery, achievements and NPC memory are byte-for-byte unchanged in `LocalProfileData`.
- **AC7** — *Given* bundle data containing a card that fails any R6 check, *when* the mode loads, *then* Dungeon Run is disabled and the reason names the card id and the failed check.
- **AC8** — *Given* a boss with `health: 40` and a `hero_power_replace` modifier, *when* the match starts, *then* the client shows the boss at 40 health with that hero power. A screenshot of this is retained as evidence.
- **AC9** — *Given* a treasure modifier is applied, *then* the playtest record holds the run seed and modifier list, and a replay from the record reproduces the opening state.
- **AC11** — *Given* a version-1 save with a run in progress, *when* it loads, *then* the run keeps its deck, power (or shrine), treasures, bosses and floor, and every later offer equals what version 1 would have offered (for runs of the 9 class heroes, the Trapper, the Pilgrim and the Sellsword). *Given* a file of a newer version, *then* it is refused and never overwritten.
- **AC12** — *Given* a fresh profile, *then* every class offers exactly its default deck and basic power; *given* a Mage run that ends on floor 3, *then* the second Mage deck unlocks and nothing else.
- **AC10** — *Given* the bundle offer algorithm, *when* run over 10,000 seeded drafts in a headless harness, *then* no taken bundle reappears, the pity rule fires within 3 drafts, and slot 3 coverage holds.

## Content (vertical slice, 2026-11-09)

All content is original and uses generic names. The client supplies only the art of each borrowed "shell" entity. Shells are unscripted Tavern Brawl entities that no other code references, so scripting them changes no other card. Names and text are overridden only during a run match (`DungeonRunRuntime.TryName` / `TryText`, hooked through `LocalNativeText`). The source of truth is `engine/.../CardSets/DungeonRun/DungeonRunContent.cs` for scripts, names and shells, plus `data/dungeon-run/run-v1.json` and `pool-v1.json` for rules, heroes, bosses and the card pool.

**Classes, decks and powers (11 classes, 33 decks, 33 powers).** The deck names and descriptions are in `pool-v1.json` (`starterDecks`); the power scripts in `DungeonRunContent.cs`. Deck 1 and power 1 are the defaults; the others unlock by the class ladder (R13).

| Class (title) | Decks (default, floor 3, floor 5) | Powers (basic, floor 4, win) |
|---|---|---|
| Druid (The Wilder) | Thicket Pack, Bark and Bough, Forked Path | Shapeshift, Rootcall (summon a 1/1 Sapling), Thornhide (1 Armor, 1 damage to a random enemy) |
| Hunter (The Ranger) | Kennel Run, Scrapyard Hounds, Snareline | Steady Shot, Set Snare, Pack Whistle (summon a 1/1 Hound with Charge) |
| Mage (The Spellwright) | Rimeward, Kindled Sparks, Survey Party | Fireblast, Survey, Rimebite (1 damage to a minion and Freeze it) |
| Paladin (The Vowkeeper) | Gleaming Wall, Supply Wagon, Lantern Road | Reinforce, Vow of Light (Divine Shield), Lantern March (restore 2 to all friendly characters) |
| Priest (The Mender) | Mending Circle, Grave Choir, Wyrmwatch | Lesser Heal, Ward of Faith (+2 Health), Penitent Lash (1 damage to an enemy minion, restore 2 to your hero) |
| Rogue (The Cutpurse) | Back-Alley Combo, Corsair Steel, Wayfinder | Dagger Mastery, Shortcut (draw, costs 3), Ambush Point |
| Shaman (The Stormcaller) | Storm Debt, Kindred Elements, Gale Riders | Totemic Call, Spirit Kindling (+1/+1 to a random friendly minion), Spark Lash (2 damage to a random enemy minion) |
| Warlock (The Hexer) | Fiend Pact, Ashen Offering, Leechwall | Life Tap, Fiend Gate (summon a 1/1 Imp), Sanguine Pact (2 damage to your hero, +2/+2 to a friendly minion) |
| Warrior (The Brawler) | Plated Ranks, Wounded Fury, Shieldhire Company | Armor Up!, Shield Wall, War Cry |
| Death Knight (The Wight) | Bone Legion, Rimeguard, Crimson Oath | Ghoul Charge, Grave Call (summon a 1/1 Ant Husk), Rime Wind (1 damage to all enemy minions, costs 3) |
| Demon Hunter (The Exile) | Glaive Rush, Edge of the Hand, Fel Hunger | Demon Claws, Glaivewhirl (+1 Attack this turn, draw a card), Felbat Call (summon a 1/1 Felbat with Rush) |

**Death Knight and Demon Hunter (added 2026-11-09).** Neither class was in any retail Dungeon Run adventure, so both have only original decks, powers and titles.

- Heroes and basic powers: the vanilla heroes `HERO_11` (Death Knight) and `HERO_10` (Demon Hunter) with their own powers `HERO_11bp` (Ghoul Charge) and `HERO_10bp` (Demon Claws). The engine and the client use the same ids, so `NativeMatch.TransportCardId` passes them through unchanged.
- Decks (each 10 cards, curve 3/3/2/1/1, at least 5 minions):
  - Bone Legion (default, Unholy, 2 runes): Body Bagger x2, Skeletal Sidekick, Battlefield Necromancer x2, Corpse Cannon, Acolyte of Death, Chillfallen Baron, Tomb Guardians, Army of the Dead.
  - Rimeguard (floor 3, Frost, 2 runes): Crypt Map, Cryofrozen Champion x2, Deathchiller, Harbinger of Winter, Timestop, Chillfallen Baron, Asphyxiate, Remorseless Winter, Hollow Direhorn.
  - Crimson Oath (floor 5, Blood with one Unholy card, 3 runes): Monstrous Mosquito x2, Body Bagger, Hematurge x2, Drink Blood, Death Metal Knight, Liferender, Death Strike, Emergency Surgery.
  - Glaive Rush (default): Battlefiend x2, Crimson Sigil Runner, Chaos Strike x2, Netherwalker, Aldrachi Warblades, Felrattler, Kayn Sunfury, Wrathspike Brute.
  - Edge of the Hand (floor 3, Outcast): Crimson Sigil Runner x2, Illidari Studies, Spectral Sight, Netherwalker, Cosmic Manifestations, Eye Beam, Felrattler, Horn of Feasting, Doomsday Prepper.
  - Fel Hunger (floor 5, Demons): Battlefiend, Void Soul x2, Netherwalker x2, Void Blast, Vicious Voidscale, Gorishi Tunneler, Eredar Deceptor, Ravenous Felhunter.
- Bundles, 4 per class like the others: Death Knight spells, undead, deathrattle and beast; Demon Hunter spells, demon, weapons and outcast. Every Death Knight bundle card is rune-free.
- `classTags`: Death Knight `minions`, `deathrattle`; Demon Hunter `minions`, `heropower`.
- Corpses: `DungeonRunSetup.Configure` sets `GameConfig.EnableCorpses`, so every run match counts Corpses. The live match already did (`NativeMatch`); headless checks now do too.
- Leech cards (Infested Breath, Sanguine Infestation, Hideous Husk) are left out until the Bloated Leech fix lands.

**Class tray.** The native picker borrows Dalaran Heist's class tray. `AbsDeckPickerTrayDisplay.UpdateValidHeroClasses` fills it from `GameUtils.ORDERED_HERO_CLASSES` (in ADVENTURE mode never the Classic list), which holds 11 classes: Death Knight, Demon Hunter, then the nine classic classes. It then removes the mission's `ClassExclusions`, and Dalaran's are empty. `DeckPickerTrayDisplay.InitHeroPickerButtons` adds no other filter. It locks a button only when `GameUtils.HasUnlockedClass` (a hero level exists) is false. `CollectionManager.GetVanillaHero(class)` is a local read of the first `CardHero` row with hero type VANILLA for the class, so it works offline for every class that has such a row. The playmat does not need DALARAN rows for a class: the picker hooks feed the deck and power rows. A class with no DALARAN `AdventureDeck` rows (Death Knight, Demon Hunter) just gets the default pouch texture.

The picker's guard (`NativeDungeonRunPicker.PickerClasses`) reads DALARAN's `AdventureDeck` rows, which list only the nine classic classes. It therefore missed the two classes the tray really shows, and picking one of them failed on the playmat ("no decks/powers"). With data for all 11 classes the guard can no longer refuse. It should still be changed to read the tray's own list.

The first 12 class powers use 12 of the 15 clickable free Tavern Brawl hero-power shells. Rime Wind, Glaivewhirl and Felbat Call use the last three (`TB_EVILBRM_OnyxiaHeroPower`, `TB_EVILBRM_RafaamHeroPower2` and `TB_TempleOutrun_Lazul_HP`). Their leftover `AI_MUST_PLAY`, `DISCOVER` and `DEATHRATTLE` tags are removed by the power's shape (`DungeonRunContent.Without`). Grave Call uses an unscripted adventure power, Bone Minions (`BRMA17_5`, Blackrock Mountain), so no free Tavern Brawl hero-power shell is left. Summoning powers list their minion (`RunContent.Tokens`) so the load check covers it (Sapling `AT_037t`, Hound `EX1_538t`, Imp `EX1_598`, Ant Husk `EDR_813at`, Invading Felbat `TTN_843t1`). All run powers cost 2 except Shortcut and Rime Wind (3). Outside a run match the shells keep their own client name, art and cost.

**Treasures (33):**

- 16 passive. 4 change the starting state: +10 Health, +1 opening card, +1 Mana Crystal, a Coin. 12 are triggers or auras on the player.
- 14 active cards: 10 spells and 3 minions, including one that grows over the run (Seedling → Sprouting → Blooming Charm every 2 floors), plus Hunting Net (Set Snare runs only).
- 3 version-1 signature cards (Cartographer), kept as `legacy` and never offered.
- Keen Eye is offered only with a Freeze source (Set Snare, Rimebite or Rimeward); Hunting Net only with Set Snare.

**Deck fixes after review (2026-11-09):** Kindled Sparks had no spells; it now has First Flame, Flame Geyser and Forgotten Torch (in place of Arcane Artificer, one Shimmering Tempest and Spire Security). Kindred Elements has Igneous Elemental in place of Volcanic Thrasher, whose Fire-spell draw had no target in an all-minion deck. Survey Party keeps its Discover cards: the engine rule above keeps Secrets out of what they offer.

**Bosses (20):** 6 in band 1, 6 in band 2, 4 in band 3, 3 finals, and a challenge boss (*The Hollow Sovereign*). The challenge boss joins the finals pool once runs have been won with 4 different classes.

- Band 1–2 bosses pilot generated 30-card decks.
- Band 3 bosses and finals pilot NPC library decks, including their sideboards.
- Boss twists are custom powers (5) or passive powers (11).

**Unlocks:** the per-class ladder (R13) and the challenge boss. The version-1 chapters are gone; § Save versions maps what they gave.

## Save versions

The sidecar is versioned (`DungeonRunSave.Version`).

- **Version 1** (hero select) held a hero id per run, stats per hero and the chapter unlocks.
- **Version 2** (class, deck, power) adds `Class` and `StarterDeck` to the run, `Class` to stats rows and archive entries, `StarterDeck` and `Power` to archive entries, and `UnlockedThisRun` to the run.
- **Version 3** (the shared draft catalog, phase 3) adds `CatalogVersion` to the run (absent = 0: a legacy run, drafting the 52 fixed bundles exactly as before), its `BucketTakes`, and each catalog bundle draft's stored `Offers` (entry, label, theme, kind, adventure tag and the three drawn cards). The upgrade from 2 changes no value: every run of an older save is a legacy run; it stamps `CatalogVersion` 0 and leaves the legacy drafts without stored offers. The first commit copies the version-2 file once to `dungeon-run.v2.xml`. A version-2 plugin refuses a version-3 file, so it can never drop the stored offers.
- **Version 4** (the act/floor schedule, phase 4) adds the run's `ScheduleVersion` (absent = 0: a legacy-schedule run, the fixed eight floors of `run-v1.json` `rules`, byte for byte), its `Schedule` id and `Node` (an index into the schedule's flattened node list; `Floor` stays the global combat floor), `NextUid` and the card records `Cards` (authoritative for an act run; `Deck` mirrors their ids and a mismatch is refused before anything is written), `ActiveRules` (the modifiers taken), the open `Tavern` and `ModifierOffer`. A run's `ScheduleVersion` is the schedule file's `scheduleVersion` (or the schedule row's own `version`) at NewRun and never changes: it is the *schedule stamp* (1 acts-v1, 2 acts-v2 and act1, 3 acts-v3h/v3t, 4 acts-v4h/v4t), a different number from the save version. Fields added later while version 4 was current are optional and omitted at their defaults: `HealthBonus` (tavern Good Food) and `MajorsCleared` (run, stats rows and profile) in phase 5; the named `Loadout`, `PowerOverride` and `NextFight` in phase 6. The upgrade from 3 changes no value: every run of an older save is a legacy-schedule run (`ScheduleVersion` 0, no schedule, node, card records, rules, tavern or modifier offer), so its body serializes as at version 3.
- **Version 5** (board shrines and teammates, phase 7) adds the run's `BoardShrine` (the Rumble Run shrine its starter deck names, set only on a new run of schedule stamp 2 or later) and `ShrineUpgrades` (kept apart from `Treasures`), the same two on archive entries, and the teammate and shrine-upgrade draft kinds. The upgrade from 4 is the identity: an older run has no board shrine, and its schedule never opens a teammate or shrine-upgrade draft.
- **Version 6** (encounters, anomalies and twists, phase 8) adds the run's `HeroSwitch` (the fight-only heroes of the floor being fought, match scope) and `HeroDamage` (ULDA_720 Battle Scars), rule kinds on `ActiveRules`, the match summary's encounter facts (`Family`, `BossRemaining`, `BossForm`, `HeroesUsed`, `Rules`, `NoLoss`) and the profile's `BossDamage` (persistent boss damage per encounter: the Plague Lords). The upgrade from 5 is the identity: an older run is on schedule stamp 0, 1 or 2, which never draws an encounter, opens an anomaly node or rolls a twist. Later additions while version 6 is current, each an attribute guarded by `ShouldSerialize` (omitted at its default, so every earlier save writes the same body and no version bump is needed):
  - `PlagueLordDamage` (family 24, ULDA_115 Gnomebliterator): damage banked for the run's next Plague Lord fight.
  - **`PowerChoices`** (the close-out, family 25): the starting Hero Powers the run's row offered when it began (the powers whose unlock the profile held then), space-separated in row order. NewRun records it on a run of schedule stamp 3 or later when two or more were open; ULDA_038 Explorer Retraining (a tavern option) offers those other than the current power (`DungeonRun.RetrainPowers`). Null (omitted) on every other run and on every run saved before the close-out, which therefore offers no retraining.
  - **Step 9** (plan D12; the save stays at version 6): on the run, `BankedGold` (gold banked for the next tavern: a bundle draft skipped for room, or an encounter reward with no room, D5/D9) and the match-scoped restart state of the floor being fought, `RestartIndex` (restarts so far; a restarted game's seed is `StableSeed(seed, MatchStream, floor, restartIndex)` only when it is above 0), `RestartSources` (the card ids used, for the per-source caps), `RestartCarriedDamage` (GILA_BOSS_48t: the damage the run hero carries into the restarted game) and `RestartTurns` (turns the floor's restarted games took, for the 2 x TurnCap budget); on the match summary, `Escaped` (ULDA_111 Hearthstone: `Outcome` Win with this mark, no major or kill credit, no drafts) and `Restarted` (GILA_911 Rewind, GILA_BOSS_48t: `Outcome` Fault with this mark). They follow the `NoLoss` precedent: no new enum member is written, so a version-6 reader of an older step meets only values it knows. Only a run on schedule stamp 4 can set them.

Loading accepts versions 1 to 6 (`DungeonRunSave.OldestVersion` to `CurrentVersion`). A newer version is refused (`InvalidDataException`), so a downgraded plugin never overwrites data it cannot read; the refusal of a version-7 file is pinned. The upgrade (`plugin/DungeonRun/DungeonRunMigration.cs`) needs the loaded data, so the runtime runs it when it first hands out the save with the rules loaded, never inside the store. Each step is caught and logged, so loading never throws on old content. Before the first commit of an upgraded file, the old file is copied once to `dungeon-run.v<N>.xml`, N the version it was loaded at (`DungeonRunStore.BackupName`).

**Resume.** After the upgrade, a run left unfinished is resumed (`DungeonRun.Resume`): a run left mid-match goes back to that floor's pre-match with the same seed. A run the loaded data cannot carry is refused there, at load, with the reason, and is not resumed: an act run whose schedule stamp, schedule id, boss count or node does not fit the loaded schedules (`ScheduleFor`, `CurrentNode`), and (step 9) any run whose `StarterDeck` or a `BossSequence` id the loaded data does not know (`DungeonRun.UnknownIdsError` names them; a version-1 run, which has no starter deck id, passes; a deck of a disabled row is named as such).

**Rollback.** Every change to the loaded save is applied in memory and then committed; if the change or the commit throws, the runtime rolls the save back to a copy taken before the change (`DungeonRunStore.Clone`, then `DungeonRunStore.Restore`: the profile, the run in progress and `Version`), so memory never runs ahead of the file. `Mutate` and `RecordFightOutcome` share that helper; before step 9 `RecordFightOutcome` restored the profile and run but not `Version` (D6).

1. **Run in progress:** the hero id maps to a class (`legacy.heroes` in `run-v1.json`; an unknown id falls back to a class id, then to the class of the run deck's first class card, then to the first class, and is logged). If this step fails, the save stays at version 1 and the upgrade runs again on the next load; a run whose class the data does not know can still be abandoned (it is archived and adds no stats row). The deck, treasures, power or shrine, signature, bosses, drafts, cooldowns, seed, floor and phase are kept exactly; a version-1 run records no starter deck id, because none of the version-1 starters is a version-2 deck. Its offers therefore match version 1 (verified for every class hero, the Trapper, the Pilgrim and the Sellsword); a Cartographer run is now single-class (Mage) and no longer gets Rogue bundles. A power the class no longer lists is kept (an old Cartographer run on Ambush Point keeps it under Mage).
2. **Stats:** rows merge by class (Trapper and Ranger become one Hunter row: runs and wins add, best floor is the maximum). A row of an unknown hero keeps its hero id and gets no class: it counts for class-free conditions, never for a class ladder.
3. **Archive:** entries learn their class (none for an unknown hero); the hero id stays.
4. **Unlocks:** each version-1 key adds the class keys that keep what it gave (`legacy.unlocks`: chapter 2 → Hunter floor 4 and 5, Paladin floor 5; chapter 3 → Warrior floor 5, Mage floor 4 and 5, Rogue floor 5; the Sellsword and Cartographer power keys → Warrior and Rogue power keys). Old keys stay; then the ladder is re-evaluated from the merged stats.

The upgrade is idempotent: every step only touches rows without a class (from version 1) or sets a value an older run already reads as (from version 2 on).

## Phase 3: catalog runs (2026-11-10)

- **Which runs.** A new run is stamped with the loaded catalog's `catalogVersion` (1). A run of an older save stays a legacy run (0): the pre-catalog draft over the 52 fixed bundles, the closed Secret prior and both sides' generated Secrets off, byte for byte.
- **Offers** (`DungeonRun.OpenCatalogDraft`). Candidates: the catalog entries eligible for the run's classes (`DungeonRunBundle.EligibleFor`; a Secret entry once `DungeonRunAdmission.MechanicHolds`), a fixed entry while not taken, a pool entry while taken fewer than `draft.bucketMaxTakes` (3) times (a distinct "Unique" entry once: a second take would repeat its no-duplicates cards and switch their condition off), none cooling down. Weight = entry weight × theme affinity (1 + 0.25 n over the entry's themes, capped at 6) × the F5 band multiplier of its average cost × `draft.dualMatchWeight` (2) for a Tombs entry of the run's own pair. Slots: pity, weighted, a different primary theme; within one draft no entry repeats, no label repeats within a shared class, and no two pool entries draw from the same class set and candidates (`DungeonRun.SameDraw`; 1 or 2 offers when fewer candidates remain; none opens no draft). A pool entry's three cards are three different candidates, each drawn by candidate weight from those not drawn yet, on their own stream (`BucketStep`, component slot × 16 + card): a bucket repeats across steps (a later take draws again), never inside one offer. A fixed entry offers its cards. The offers, their cards and their grant cards are stored on the draft (`DungeonRunDraft.Offers`, `DungeonRunOffer.Grants`), and a pick takes the stored cards, re-checking each against the current data with the grant flag as offered and the client check the catalog was loaded with (`DungeonRunData.ClientKnows`; a card that no longer passes is a validation error and the run rolls back). A pool pick adds a take; a fixed pick is taken once. Cooldown entries are `id@floor` parsed at the last `@`; a malformed one is an `InvalidDataException`.
- **Dual-class rows** (`run-v1.json` `catalogClasses`, loaded with the catalog only: legacy runs are single-class). `MAGE_ROGUE` (Reno) ships enabled with the one Tombs starter deck whose every card passes the pool checks (`tombs_reno_spellslinger`, ULDA_Reno_Deck1), the Mage portrait (the validator requires a scripted class portrait: ULDA_Reno is not scripted and has its adventure 15 Health) and the Mage powers on its own unlock ladder; `PALADIN_SHAMAN`, `DRUID_PRIEST` and `HUNTER_WARRIOR` ship disabled (`run:adventure-loadouts`: no Tombs starter passes after reprint resolution; the reasons name the cards still blocking). Their Tombs entries stay in the catalog marked `unreachable` and are counted apart. A dual row's class set is its two classes (treasure scopes, catalog eligibility, the Secret prior); engine generation follows the primary class only. The plank flow shows them ("Mage / Rogue"); the native Adventure tray (the guest-hero tray, 12 per page with Previous/Next since the picker pages its rows) shows them too, after the class rows.
- **Generated Secrets.** A catalog run's player may generate Secrets (`RunMatchPlan.PlayerGeneratedSecrets`); the five Secret-generating draft cards are admitted (`secret-generated` `CatalogAdmit`); unscripted Secrets are never generated (they join `GeneratedRejects` for catalog runs, since the engine's Secret helpers do not check `Implemented`). The boss's prior is `DungeonRunAdmission.CatalogSecretPrior`: the run's deck-source Secrets at 4 × (1 + count) plus, at 1, every Secret the player's generation can yield in its classes and in the class of each Secret generator its deck sources hold (`SecretGeneratorClasses`: Professor Putricide, granted to Rogue by Tess Greymane, casts Hunter Secrets). It orders the fair search and never certifies (`FairBotState` certifies only while the opponent's generation is Secret-free). The boss side keeps generated Secrets off (DALA_BOSS_21p waits on phase 8), also when the player's own effects generate for the boss: a task that generates for the other side (Spellslinger AT_007, Mulch AT_044, Merch Seller, Faceless Enigma, Encircle, Undefeated Champion) reads the pool inside `Game.GenerateFor(receiver)`, so a card passes only when both sides' filters allow it. The catalog lifts admit scripted cards only: an unscripted card (Hunter's Pack ULD_429) keeps its legacy Generated verdict.
- **Not lifted:** Zephrys (ULD_003) is unscripted in the engine and needs a wish script; its lift is phase 6 with ULDA_013.

## Phase 8: the encounter model (2026-11-12)

Imported adventure encounters are boss rows with a provenance (LOOT, GIL, TRL, DALA, ULDA) in
`data/dungeon-run/encounters-v1.json`, generated by `tools/dungeon-run-catalog -- encounters` from
`data/dungeon-run/reference/encounters/F<NN>.json`. They are appended after the twenty custom bosses; `run-v1.json` and
`pool-v1.json` are not edited.

- **Row**: tier (`1`-`4`, `major1`-`major3`, `finale`; its pool is `enc-t<n>`, `enc-major<n>` or `finale`), hero, power,
  deck (kind `authentic`, `substituted`, `extended` or `curated`, with its source page), flags from a closed vocabulary,
  and what makes the boss distinctive: `forms`, `start`, `variants` (shrine + deck), `heroCards`, `relay`,
  `bossGeneratedSecrets`, `incompatibleRules`.
- **Schedules**: `schedule-v1.json` is scheduleVersion 3. `acts-v1`, `acts-v2` and `act1` ship unchanged and stamp new runs
  2. `acts-v3h` and `acts-v3t` (stamp 3) draw from pool sets that mix the custom bands with the encounter tiers
  (`"draw": "provenance-mix"`: each pick takes the provenance the run has used least), open each act with an anomaly node,
  and end with a finale act once their `finale` is enabled. `default` stays `acts-v2` until the encounter families have
  landed; the close-out sets `defaults` and a new run then rolls one of them from its seed.
- **Global rules**: anomalies (offered at the anomaly node: two rolled rules or none) and twists (rolled per act at
  NewRun) are `schedule-v1.json` `rules`; an active rule is a `DungeonRunRule` with a `Kind`, and lands on both players as
  its player enchantment (`DungeonRunGlobalRules.Apply`).
- **Run-owned fight state** (save version 6): `DungeonRunState.HeroSwitch` (the fight-only heroes of the floor being fought;
  the run's class, power, deck and cards are never written by a switch), `DungeonRunState.HeroDamage` (ULDA_720), and
  `DungeonRunProfile.BossDamage` (persistent boss damage per encounter, at profile scope because a lost fight ends the run).
  `DungeonRunSetup.Harvest` reads what a fight left; `DungeonRun.RecordOutcome` keeps it.
- **Engine**: `Actions.EncounterRules` (forms, start block, relay, carried damage, own-deck facts, harvest; one tag table,
  32630-32699) and `CardSets.DungeonRun.DungeonRunEncounters` (the encounter cards on their own client ids, one file per
  family).
- **Boss Secrets** (family 01): four Secret bosses ship with their wiki decks. Elder Brandlemar (`LOOTA_BOSS_35p` puts a
  Counterspell into play), Trapped Room (the passive `LOOTA_BOSS_48p` summons a 3/3 Sawblade when a boss Secret is
  revealed), Manhunter Ivan (`GILA_BOSS_25p` puts a random Secret of the boss's deck into play) and Trap Room
  (`ULDA_BOSS_44p` plays one from the boss's hand or deck, as an effect cast). Pressure Plate (`ULD_152`) is scripted.
  - *The boss side's generated Secrets* stay off unless the row says `bossGeneratedSecrets`. A card with
    `AdmissionRule.BossNeeds` (Whirt's Prediction; Professor Putricide and Streetsmuggler, which name "a random Hunter
    Secret": `BossSecretClass`) is refused on a row without it, and the validator closes each yield pool
    (`DungeonRunValidator.BossSecretPool`: every scripted Secret of that class a catalog run's lists leave is admitted for
    a boss). The boss's Discovers and random cards still never yield a Secret (its own generation list holds them all);
    a card that names the Secret reads the closed pool instead (`Game.BossNamesSecrets`).
  - *A clickable boss power is offered by what is exact in the boss's fair view only* (boards, its own hand and Secrets,
    zone counts), never by its deck: the boss's search samples its deck, and an option that is legal in a sample but not
    in the real game cannot be taken. Trap Preparation and Tricks and Traps are therefore offered whenever the Secret
    zone has room and do nothing when no Secret is left.
  - *Own-deck ledger*: a Secret that goes from its owner's deck into its owner's Secret zone (those two powers, Mad
    Scientist) leaves the owner's sampled deck as that card, not as an unknown one (`OwnDeckLedger`).

### Wave 1: unscripted collectibles (families 02, 03, 04; 2026-11-13)

The 92 collectible cards the buckets and the imported boss decks listed without a script are scripted in their card-set
files, each with an admission row (`DungeonRunAdmission.F02.cs`, `.F03.cs`, `.F04.cs`): 33 outside Uldum (family 02), ULD_131
to ULD_274 (family 03) and ULD_282 to ULD_728 (family 04). No bucket member is left unscripted (catalog: 6508 admitted
references, 1 excluded for its class). They add no boss row; six of the twelve shipped encounter decks are now the wiki's
lists unchanged.

- **Engine rules for later families** (`Actions/EncounterRules.F02.cs`): `WrongTarget50` / `WrongTargetTask` (GVG_054; an
  ATTACK trigger whose event source is the attacker), `HeroDamageCap` (tag 32637 in the table of `EncounterRules.cs`, read
  on a minion, a hero power or the hero; applied in `Character.TakeDamage` before Armor), `SecretRepeats` (DAL_573: every
  Secret built by `ComplexTask.Secret` is a `SecretTriggerTask`), `CapturesOpening` (opening hands are recorded in every run
  match; `TurnQueueRules.RedactOpening` keeps the run player's out of the boss's view).
- **Helpers**: `DungeonRunEncounters.ScriptedPool` / `DiscoverScripted` (family 02), `UldumCollectiblesB.QuestStep` and
  `RestlessOthersAura` (family 03), `UldumRules` (family 04: `ShuffleSummonWhenDrawn`, `SummonDrawn`, `QuestProgress` /
  `Advance`, `ScriptedPool` / `RandomScripted`, `IsOtherClass`, `DiscoverDeadToDeck`).
- **Quests whose reward is a hero power** (admission family `quest-hero-power`, two rows): the six of family 04 are the
  run player's only (a boss's hero power is its encounter's identity); ULD_131 Untapped Potential of family 03 is admitted
  in every scope. No shipped boss deck lists a Quest. The two rows state different boss policies; the close-out decides
  whether ULD_131 joins the player-only row.
- **Fixes the regenerated catalog exposed** (released bosspilot modes; regression suite c-w1fix): FP1_009 Deathlord left
  the minion it summons listed in the opponent's deck; CFM_668 Doppelgangster's Battlecry threw when GIL_820 Shudderwock
  repeated it (now 2 copies of the repeater); LOOT_210 Sudden Betrayal answered a hero's attack and threw (now enemy
  minions only).

### Wave 2: global rules and the Dungeon Run bosses (families 05, 07, 08; 2026-11-13)

Three families landed together: the global-rule framework with the Dalaran Heist anomalies and twists (05), eighteen Dungeon
Run encounters with a clickable hero power (07) and seventeen with a passive, trigger or both-player power (08). The shipped
encounter list holds 43 rows (the foundation's 8, 18 of family 07, 17 of family 08) and 47 decks, 32 of them the wiki's list
unchanged; the schedule lists 22 rule rows (17 scripted anomalies, 4 scripted twists, DALA_857 rejected). All of it stays
behind acts-v3h and acts-v3t; no default changed.

#### Family 05: the global-rule framework and the Dalaran Heist rules

**Framework.** A global rule (an anomaly or a twist) is a row of `schedule-v1.json` "rules" whose id is the rule's client
card. Family 01 built its life cycle: offered at an act's anomaly node or rolled from an act's twist pool, stored in
`DungeonRunState.ActiveRules`, checked against the act's bosses and the other active rules, carried on
`RunMatchPlan.GlobalRules` and applied by `DungeonRunGlobalRules.Apply` as a player enchantment on both controllers.
Family 05 adds what an enchantment cannot carry:

- `RuleHooks` (engine, `DungeonRunEncounters.Rule(id, effectId, effect, hooks)`): `OpeningCards` (extra opening-hand cards
  for both players, added in `DungeonRunSetup.Configure` within the engine's cap), `OnApply` (once, at
  `ApplyAfterStart`), `OnStart` (once, after both mulligans and the encounter's own start block, from
  `EncounterRules.ResolveStart`) and `Cards` (the tokens and enchantments the rule creates; the validator checks the client
  knows each and that each is scripted). A rule whose text is a start condition has no "Player Enchantment" card in the
  client data and is hooks only: `effectId` null. Such a rule is active by its bit in the game tag
  `EncounterRules.RuleApplied` (32660), which also makes an apply hook run once in the game and in every clone.
- Private tags: `RuleApplied` 32660 (game), `RuleSpells` 32661 (a rule's player enchantment: spells cast this turn),
  `RuleSummons` 32662 (a rule's player enchantment: minions marked this turn; added by the integration for Explosive).
  Next free rule tag: 32663.
- Run side: a rule row may list `incompatibleClasses` (run class ids the rule is never active for), beside `noBoardShrine`,
  `incompatibleRules`, `incompatibleFlags` and `incompatibleModifiers`.
- Shared engine rules for later families (`Actions/EncounterRules.F05.cs`): `BattlecriesTwice(bothPlayers)` and
  `DeathrattlesTwice(bothPlayers)` (the controller auras of Battle Totem and Totem of the Dead; one aura of type CONTROLLERS
  on a boss power covers "All ... trigger twice"), `RecastRandom(spell, source)` (a copy cast with random targets, never of a
  copy, a Secret or a Quest), `PlaceBlockers` / `Blockers` (a board limit made of untouchable permanents), `Imprison` /
  `Imprisoned` / `PrisonTurns` / `TickPrison` (a permanent that holds a minion and releases it), `SwapStatsOnSummon`.

**Rules.** Texts are the client's; readings the text leaves open are recorded in `curation.json` "ruleAdaptations" and in
`data/dungeon-run/reference/encounters/F05.json` "rules".

| Rule | Kind | How it is scripted |
|---|---|---|
| DALA_851 Summons | anomaly | after a player plays a spell: a random scripted minion of the Cost the spell had in hand, through the generation filter |
| DALA_852 Crying / DALA_853 Rattling | anomaly | the Battlecry / Deathrattle doubling aura on each player |
| DALA_854 Nesting | anomaly | a played minion gains Nested: when it dies a fresh 1/1 of the same card takes its place |
| DALA_855 Refreshing | anomaly | at the end of a player's turn that player draws up to 5 cards (Jeeves's count; Fatigue from an empty deck) |
| DALA_856 Wild Magic | anomaly | after a player plays a spell a copy is cast with random targets (no copy of a Secret, a Quest or a countered spell) |
| DALA_858 Fishy | anomaly | 1 chance in 4 at the start of a player's turn: a random scripted Murloc for that player |
| DALA_859 Gorged | anomaly | hooks only: two more opening cards before the mulligan and two Mana Crystals, both players |
| DALA_860 Dragon Soul | anomaly | the third spell a player casts in a turn summons a 5/5 Dragon Spirit (LOOT_209t); the count starts again |
| DALA_861 Ogreish | anomaly | Ogre Warmaul's 50% wrong-enemy rule for every attacking minion |
| DALA_862 Reductive | anomaly | at the end of a player's turn its hand costs (1) less (Thaurissan's enchantment; Echo cards stay at (1)) |
| DALA_863 Hallucinating | anomaly | hooks only: ten Hallucinations (UNG_856) shuffled into each deck after both mulligans, recorded for the own-deck ledger |
| DALA_865 Explosive | anomaly | a summoned minion gains "Deathrattle: Deal 1 damage to all minions"; at most 30 minions of one player a turn (see the integration changes) |
| DALA_867 Infused | anomaly | a summoned minion gains one of Taunt, Divine Shield, Rush, Windfury at random |
| DALA_829 The Coffers | twist | hooks only: the boss starts with a 0/3 Cache of Cash (Deathrattle: two Coins for each player) |
| DALA_831 The Stench | twist | a minion's Attack and Health are swapped once as it enters play (a 0-Attack minion dies) |
| DALA_832 The Prisons | twist | hooks only: each player starts with a Violet Prison holding a random minion, released at the start of its owner's turn number Cost |
| DALA_833 The Carts | twist | hooks only: each player starts with three different Carts, permanents that take board slots (four minions each) |
| DALA_857 Glittering | anomaly | **rejected**: its Treasures are the Dalaran Heist active treasures, run rewards no boss and no generation pool may hold yet |

**Incompatibility (schedule-v1.json).** Crying excludes bosses flagged `doubleBattlecry`, Rattling `doubleDeathrattle`, Wild
Magic `recast`, Ogreish `forcedAttack`, Hallucinating `deckReplace` and `relay`. The board twists (Coffers, Prisons, Carts)
exclude one another and bosses flagged `boardStart`; The Stench excludes Coffers and Prisons (a 0/3 Cache would die on
arrival; a released prisoner is a transform, not a summon). The Stench and The Carts are never active on a run with a
board shrine or against a boss flagged `shrine` or `boardStart`; The Carts never for Darius Crowley (his Cannons).
A later family whose boss conflicts with a rule lists the rule in its row's `incompatibleRules` (Tierra Blythe's waitress
and DALA_861) or sets the matching flag.

**Not in a shipped twist pool yet.** The four twists are scripted, admitted and listed as twist rules, but the acts of
acts-v3h and acts-v3t ship with empty twist pools (changing an act is the close-out's): they are rolled only on the
fixture schedule of the tests. The ledger shows them `partial`, blocked by `run:global-rules`.

**Tests.** `p8f05/suites/c-rules-05` (each rule's text on both sides in real engine games through DungeonRunSetup, the
framework and the run layer) and the rules05 bosspilot mode (`p8f05/run.sh bp <n> <rule>`: the rule active against one
custom boss and one boss per adventure with the fair AI as the boss; a probe counts the rule's own effect in each game).

#### Family 07: Dungeon Run bosses A (clickable powers)

Rows of `reference/encounters/F07.json`, each with its wiki page: Waxmancer Sturmi (Sculpt Wax; Rank 2 Harden Sculpture),
Pathmaker Hamm (Unstable Explosion; Unstable Demolition), Blackseed (Evolvomancy; Greater Evolution), Frostfur, Graves the
Cleric, Overseer Mogark (Cruel Words; Searing Lash), Candlebeard (Charge!), Thaddock the Thief, Giant Rat, Gnosh the
Greatworm, Bristlesnarl, The Mothergloop, Wee Whelp and Bink the Burglar. The tier is the printed hero Health (10 and 20:
tier 1; 30 and 40: tier 2; 50 and 60: tier 3); the two ranks of a boss are two rows of different tiers and one run can
meet both. The powers the wiki marks as auto-cast are the AI's choice here. Scripts: `DungeonRunEncounters.F07.cs` (public
helpers `EvolvePool`, `HighestAttack`); admission rows `boss-power-public`, `boss-power-own-hand`, `boss-token`, all for
a boss only. Growing Ooze (ICCA07_004, The Mothergloop's deck) is scripted there too.

- A run player's board shrine is destroyed instead of transformed by Evolvomancy / Greater Evolution (it goes dormant), a
  copy of a shrine is a plain minion, and Swallow Whole and the random-damage powers never pick an untouchable card (a
  dormant shrine form, a Cart, a Violet Prison).
- Decks: twelve are the wiki's lists unchanged. Blackseed's two replace Wrath (EX1_154 has no script of its own; the
  encounters step does not take the Core reprint as a client card) with Swipe; Graves the Cleric's replaces ten later
  Core-set cards this client does not have; the three level-1 lists (Giant Rat, Wee Whelp, Bink) are the wiki's ten cards
  twice plus ten curated fillers.
- FIR_955 Emberroot Destroyer never picks a mortally wounded minion: with Axe Flinger (Hamm's deck) the two answered each
  other without end and the boss's search overflowed the stack.

#### Family 08: Dungeon Run bosses B (passive, trigger and both-player powers)

Rows of `reference/encounters/F08.json`: George and Karl (one tag-team encounter), Azun (all Deathrattles twice), Jinzo (all
Battlecries twice), Vex (both), the two Gutmooks, Candlebeard's passive rank, Ixlid, Lyris, Kraxx, Seriona, Whompwhisker,
Elder Jari, Jeeru, Fungalmancer Flurgl, the Lava-Filled Chamber and Russell the Bard; and the boss deck minions LOOTA_102
Starving Crab, LOOTA_104 Reverberating Ooze, LOOTA_105 Deadly Spore, LOOTA_107 Shroomsayer and LOOTA_109 Gluttonous Trogg
(admitted for both sides, never generated). Scripts: `DungeonRunEncounters.F08.cs` (`PoisonousToHeroes`, `RecruitOne`),
`Actions/EncounterRules.F08.cs`; eight admission rows.

- **The tag team.** George and Karl are one row with a script form (form 0 George and Karl with Divinity, form 1 Karl and
  George with The Silver Hand), each hero with his own 30 Health. LOOTA_BOSS_110 Tag Team swaps the active and the benched
  hero with their own damage (`EncounterRules.TagTeam`; the swap itself is `SwapForm`); a defeated active hero is replaced
  by the other inside the death check (`TagTeamDeath`, called first in `ResolveBoss`), and the boss loses when both are
  defeated. The bench is the private controller tag `TagTeamBench` (32643, in the tag table).
- A take-control hero power is never offered on a run shrine (`Controller` hero power options; LOOTA_BOSS_51p Alluring
  Tune). A clickable power that would do nothing is not offered; each such condition reads only public boards or the
  boss's own hand count.
- Rows that double Battlecries or Deathrattles carry the flags `doubleBattlecry` / `doubleDeathrattle`, so DALA_852 Crying
  and DALA_853 Rattling are never active against them. George and Karl and Ixlid keep their identity Health
  (`fixedHealth`; incompatible with mod_iron_bosses and mod_open_road).
- LOEA10_3 Murloc Tinyfin (Flurgl's deck), a collectible vanilla minion that had no card-database entry, is registered as
  scripted: it is now in every generation pool it qualifies for (0-Cost minions, Murlocs).
- Decks: thirteen are the wiki's lists unchanged; four replace Wrath, Power of the Wild, Mogor's Champion or Fel Reaver.

#### What the integration changed

- **Decks that became the wiki's lists.** With family 08's cards scripted, Trapped Room (two Shroomsayers), Gnosh the
  Greatworm (two Gluttonous Troggs) and The Mothergloop (five Reverberating Oozes, two Shroomsayers) play their lists
  unchanged; the replacements named in F01.json and F07.json are no longer used.
- **Explosive is bounded.** Two minions that summon each other when they die (Eternal Bloodpetal, 5/1, and its 0/1 Eternal
  Seedling) exploded on each other without end under DALA_865: the death phase never returned (the boss's search hung on
  that line against Blackseed). The rule now marks at most 30 minions of one player in one turn
  (`DungeonRunEncounters.ExplosiveMarksPerTurn`; the count is the tag `RuleSummons` on that player's copy of the rule and
  starts again at each turn end); recorded as rule adaptation `explosive-limit`.
- **GVG_054 Ogre Warmaul** redirects as the attack event reaches it (FastExecution), as DALA_861 Ogreish does: queued, a
  Secret that answers the attack (Splitting Image) was validated on the aimed minion and resolved on the hero
  (InvalidCastException). A later card with the 50% rule on an ATTACK trigger must do the same.
- **Tag table.** `TagTeamBench` 32643 moved from the family's file into the table of `EncounterRules.cs`; a family's
  reserved number is 32635 + NN (taken: 32637 by family 02, 32643 by family 08).

**Open for the close-out.** The twist pools of acts-v3h and acts-v3t are empty, so the four twists are rolled only on the
fixture schedule (`p8f05/fixtures/schedule-twists.json`); DALA_857 Glittering waits for treasures a boss may hold; the
flags `recast`, `forcedAttack` and `boardStart` are set on no shipped row yet (the families that own such bosses set them).

**Tests of the wave.** c-rules-05 (170 checks), c-enc-07 (161), c-enc-08 (113), and the bosspilot modes rules05 (each of
the 18 rules against six bosses), enc07a / enc07b / shrine07 and enc08 / enc08-tag / enc08-cards / enc08-trapped, each
twice identical with 0 faults, fallbacks, failed checks and swallowed leaf exceptions; every family 05 rule was also
played against fourteen bosses of families 07 and 08 (Crying and Rattling against the thirteen they may meet).

### Wave 3: Tombs anomalies, Dungeon Run finals and rooms, Monster Hunt bosses (families 06, 09, 10; 2026-11-13)

Three families landed together: the Tombs of Terror anomalies and plague twists on family 05's framework (06), the five final
bosses, special rooms and special starts of the Kobolds and Catacombs Dungeon Run (09), and fifteen Monster Hunt encounters
with a clickable hero power (10). The shipped encounter list holds 69 rows (the foundation's 8, 18 of family 07, 17 of
family 08, 11 of family 09, 15 of family 10) and 73 decks, 43 of them the wiki's list unchanged; the schedule lists 43 rule
rows (the 22 of wave 2 and 21 of family 06). All of it stays behind acts-v3h and acts-v3t; no default changed.

#### Family 06: the Tombs of Terror anomalies and plague twists

Rules of `reference/encounters/F06.json` (no boss rows), scripts in `DungeonRunEncounters.F06.cs`, engine rules in
`Actions/EncounterRules.F06.cs`, admission rows in `DungeonRunAdmission.F06.cs`; twenty-one rule rows of `schedule-v1.json`.

| Rule | Kind | What it does here |
|---|---|---|
| ULDA_702 Loot-Filled | anomaly | a played minion gives the other player a Coin (none into a full hand) |
| ULDA_705 Booms Away | anomaly | a dead minion shuffles a Bomb (BOT_511t) into its controller's deck |
| ULDA_706 No-Face | anomaly | a hero is Immune while its side controls a minion (permanents and dormant minions do not count); never on a shrine run or against the flags `shrine`, `heroImmune` |
| ULDA_707 Eternal Army | anomaly | a dead minion shuffles a new copy of its card into its controller's deck |
| ULDA_708 Dizzying Blows | anomaly | after a hero is damaged, each card in that player's hand costs a random 0 to 10 (Echo and repeatable cards never below 1) |
| ULDA_709 Angelic Presence | anomaly | a minion's Attack equals its Health (`AttackEqualsHealth`); not with DALA_831 |
| ULDA_710 CHARGE! / ULDA_711 Relentless | anomaly | board auras: all minions have Charge / Windfury |
| ULDA_712 Holograms | anomaly | a played minion gives the other side a 1/1 copy |
| ULDA_713 Whirling Winds | anomaly | 1 damage to all minions at each turn start |
| ULDA_714 In the Face | anomaly | hero damage is doubled before Armor (`HeroDamageDouble`, tag 32641; Animated Armor's cap applies after); not against the flag `damageCap` |
| ULDA_715 Bountiful | anomaly | both players start with nine cards (the boss's Coin is one of its nine); hooks only (`RuleHooks.OpeningHand`); not with mod_open_road |
| ULDA_719 Marin's Return | anomaly | each player starts with a Master Chest (LOOT_357l) on its own side; the breaker gets one of the four treasures; not with the other board-start rules or against the flag `boardStart` |
| ULDA_720 Battle Scars | anomaly, run scope | the run hero starts at 100 Health and keeps its damage between fights (family 01's run state) |
| ULDA_721 Aleatory | anomaly | both decks are mixed after the mulligans and each player gets half (`MixDecks`); a card the receiving side may not be given stays; not with ULDA_801 or against the flags `deckReplace`, `relay`, `heroSwitch`, `secrets` |
| ULDA_724 Spellshields | anomaly | 2 Armor after each spell that was not countered |
| ULDA_729 Multiclassing | anomaly, run scope | the card drafts offer every class and a deck copy accepts such a card (`DungeonRun.LootClasses`); class Discovers, tavern pools and the treasure pool keep the run's classes |
| ULDA_801 Plague of Murlocs | twist | Surprise! Murlocs! (ULDA_801t) shuffled into both decks after the mulligans: the run's fight number, at most 8, per deck |
| ULDA_802 Plague of Madness | twist | at each turn end the rightmost minion of the turn's player attacks a random enemy (`ForcedAttackRandomEnemy`) |
| ULDA_803 Plague of Wrath | twist | a minion that takes damage gains +1 Attack |
| ULDA_804 Plague of Death | twist | each player starts with an Eternal Tomb (ULDA_804t); it summons the minion it holds after as many friendly deaths as that minion costs, at most 7 summons a Tomb a turn (`RuleTombSummons`, tag 32680) |

- The two run-scope rules have no match script and never reach a plan's rule list; the ledger tool reads an admitted rule
  with effect `run` as carried by the run layer.
- Opening hands: `GameConfig.MaxFirstOpeningHandBonus` (6) holds for the player the config names as first;
  `MaxOpeningHandBonus` stays 5.
- The ten client "Dummy Hook Up" cards (ULDA_702d ... ULDA_803d) are the client's script holders and are never created;
  with the two tokens they are support records of E06.
- The four twists are listed, scripted and admitted, but no shipped act names them in its twist pool: they are rolled only
  on the fixture schedule `p8f06/fixtures/schedule-plagues.json`.
- Twelve rule adaptations are recorded in `curation.json` (`plague-twists-per-act` ... `eternal-tomb-rule`): the wiki states
  no number for the Murlocs of a Surprise or the Cost range of Dizzying Blows (1 to 3 and 0 to 10 here), and names no chest
  card for Marin's Return (the Master Chest).

#### Family 09: Dungeon Run bosses C (final bosses, special rooms, special starts)

Eleven encounters (data/dungeon-run/reference/encounters/F09.json; scripts DungeonRunEncounters.F09.cs; engine rules
Actions/EncounterRules.F09.cs; admission rows DungeonRunAdmission.F09.cs).

- **Final bosses** (tier `major1`: act I majors, the act curve's Health plus 10): Vustrasz the Ancient (five Master Chests to
  start; Vindictive Breath counts the missing ones), Xol the Unscathed (passive Beam Me Up!: a random Beam at the end of her
  turn; six Beam spells), Azari the Devourer (Devour burns the top two cards of the run player's deck into the public
  graveyard), The Darkness (Encroaching Darkness summons 5/5 Darkspawns; the run player starts with three Luminous Candles
  in hand, for that fight only), King Togwaggle (Magic Candle finds one of sixteen Treasures; Rakanishu at the start of his
  turn with a chance of 1 in 4).
- **Special rooms**: Treasure Vault (seven Treasure Coffers; a Coffer's death adds a random treasure of the run's own pool to
  the run deck; at 5 Mana Crystals the doors close and the fight ends with no loss), Tad (a ten-minion deck, no hand, no
  draw; Catch and Release pulls the minion whose Cost equals his Mana Crystals; an undamaged Tad summons the Sunken Chest
  on turn 11, whose death adds Tad's Pole to the run deck), Brimstone Warden (four Stone Golems; Dispel Ward at 5 Mana),
  A. F. Kay (plays no card before her sixth turn), Chronomancer Inara (Stolen Time at 10 Mana: an extra turn), and
  Mushhuckster Max (Mushroom Potions crafted from the nine 1-Cost Kazakus effects).

Rules the family adds to the model:

- **Room rules** (`EncounterRules.RoomRules`, private controller tag 32644, bits `RoomNoDraw`, `RoomNoPlay`): a boss that holds
  no cards never draws or fatigues (`Generic.Draw` returns at once) and a boss that is idle plays no card from its hand
  (`PlayLockRules.BlocksPlay`). The room's hero power puts them on at the first turn start; with `RoomNoDraw` the dealt hand
  returns to the deck and a deck fact (`DeckRewritten`) gives the boss's own-deck ledger the exact list.
- **Rule-cast powers** (`DungeonRunEncounters.AutoCast`): a power the wiki marks auto-cast whose worth the boss's AI cannot
  evaluate (Devour, Stolen Time, Dispel Ward, Doors Are Closing) is registered with the passive powers (never an option) and
  cast by a turn-start trigger that pays its Cost. Waves 5-6 and the close-out review cast most of the other auto-cast powers
  by rule too; an auto-cast power that remains an AI choice says so in its row's power note (c-enc-25 P1 checks every power
  whose client card carries AI_MUST_PLAY, the wiki's auto-cast marker).
- **No-loss ending** (`EncounterRules.EndWithNoLoss`): the script marks the game (`NoLoss`) and the closing side's hero is
  destroyed, so the table shows the run player's win; `DungeonRun.RecordOutcome` records a harvest with the mark as a win
  with no boss kill credit whatever outcome is reported.
- **Flag `listedDeck`**: a combat-tier row keeps its listed deck count (10 to 60) instead of 30 (Tad, the Treasure Vault). A
  deck spec may state `"kind": "curated"` (the Vault has no deck on the wiki).
- **Boss-held treasures**: the fifteen Kobolds and Catacombs treasures Magic Candle finds are admitted for a boss on catalog
  runs only (`CatalogAdmit`; the legacy verdict stays the run player's).
- A static field initializer in a `DungeonRunEncounters` partial file must not construct a task that reads the card database
  (an `AddEnchantmentTask`, a `SummonTask` by id): the card database registers the class's entries while it loads, and a
  re-entrant load leaves every encounter card unscripted. Build such tables on first use.

Decks: nine are the wiki's lists unchanged; A. F. Kay's replaces two Fel Reavers with Bittertide Hydras; the Treasure
Vault's (ten Coffers, never drawn) is curated. LOOTA_BOSS_53h2 (Inara's second portrait) is a support record.

#### Family 10: Monster Hunt bosses A (clickable powers)

Rows of `reference/encounters/F10.json`: A Mangy Wolf, Rottooth, Cutthroat Willie, Grubb the Swampdrinker, Gnomenapper,
Experiment 3C, Sazzmi Gentlehorn, Gnarlroot, Niira the Trickster, The Scarecrow, Inquisitor Hav'nixx, Chupacabran, Griselda,
Wharrgarbl and Cozzlewurt, with their nine boss-only cards. The tier is the printed Health of the hero card (15: tier 1;
25 and 30: tier 2; 35 and 40: tier 3; 50 and 60: tier 4); the wiki's round of each boss is recorded in its row. Scripts:
`DungeonRunEncounters.F10.cs`; five admission rows (`boss-board-power`, `boss-own-hand-power`, `boss-control-all`,
`boss-setaside-store`, `boss-hunt-card`).

- **The set-aside store** (`Actions/EncounterRules.F10.cs`: `CanStore`, `Store`, `StoredBy`, `Release`; private tag
  `SetAsideStore` 32645 on the holder). Gnomenapper's Nab stores an enemy minion in a Stuffed Sack summoned for the boss;
  the sack's Deathrattle returns the minion itself (damage and enchantments kept) to its own controller; it is lost when
  that board is full or the sack is silenced, transformed or bounced. A run shrine is never a legal target and never enters
  a store.
- A start-block weapon now starts its aura and trigger (`EncounterRules.ResolveStart`; the Scarecrow's Scythe: Immune to
  Durability loss, +1 Attack per kill; start-equipped here, the wiki lists it in no deck row).
- Hero-power options apply a card's `SourceTargetingPredicate` (`Controller`), as hand cards do: Nab is never offered on a
  shrine and Bloodthirst never on a Lifesteal minion.
- Amalgamate resolves the deaths before it summons the Amalgamation; Infatuation and Enchanting Tune never take a shrine;
  Witch's Kiss makes a picked shrine dormant; Devour Soul halves Health without damage and never below 1.
- Decks are the wiki's lists brought to 30 cards (second copies, curated fillers, or the first 30 of a longer list), each
  change recorded: one is the wiki's list unchanged (the Scarecrow), eleven are extended, three substitute an unscripted
  card (Ogre Ninja, Ogre Brute, Mogor the Ogre, Mogor's Champion, Wee Spellstopper, Gnomish Experimenter, the original
  Wrath and Power of the Wild, and Witchwood's Touch GILA_BOSS_99t2, which is family 23's). Chuck, the 6-Cost spell, is
  shuffled into Grubb's deck by his start block.

#### What the integration changed

- **Hagatha's deck is the wiki's list again.** With GILA_BOSS_27t Amalgamate scripted by family 10, the two Amalgamates of
  her list are back (the replacement named in F01.json is no longer used).
- **The provenance mix reaches the majors.** With Monster Hunt rows in the combat tiers, a run that already holds a GIL boss
  takes a custom band-3 boss as its act II major: Hagatha is the act II major in 506 of 1000 seeds (1000 before), and the
  act I major may be one of the five Dungeon Run finals (`enc-major1`). Whirt, Voone and Icarax are still met by every run
  (c-enc-model E3).
- **Aura removal and the fair view.** GIL_618 Glinda Crowskin's hand aura (Echo, a SET effect) was removed from the Unknown
  stand-ins of the run player's masked hand when she died in the boss's search: NullReferenceException, a fallback decision
  (Marin's Return against Chronomancer Inara). `Effect.RemoveAuraFrom` now returns for an entity that holds no aura effects
  (c-w3fix X1-X4).
- **Bountiful and the no-hand rooms.** Tad and the Treasure Vault put their nine opening cards back into their deck as any
  opening hand (the Coin is set aside); the rules06 bosspilot probe expects 0 cards for such a boss.
- **Tag table.** Taken numbers: 32637 (family 02), 32641 (06), 32643 (08), 32644 (09), 32645 (10); rule range 32660-32662
  (family 05 and wave 2) and 32680 (family 06, whose rule-state tags are 32680-32699).
- Hooked-rule bits: 11 of 31 are used (family 05: 5; family 06: ULDA_714, 715, 719, 721, 801, 804), in registration order.

**Open for the close-out.** The twist pools of acts-v3h and acts-v3t are still empty (ULDA_801-804 and the Heist twists
are rolled only on fixture schedules). A player who enters the mulligan with a full ten-card hand loses the first
replacement card (`Generic.ChoiceMulligan` adds before it removes): Bountiful therefore gives the boss eight cards and the
Coin, and a row with `start.bossOpeningHandBonus` 5 would meet the same limit. The five finals as act I majors won 20 of 20
bosspilot games (balance, not tuned); Grubb is weak at tier 4. LOOTA_103 and LOOTA_BOSS_52t keep the ledger blocker
`run:deck-mutation` from the ledger tool's phrase rule although their run deck edits are implemented. A fight's deck edits
are applied all or none within the deck maximum (a Vault that would pass it adds nothing).

**Tests of the wave.** c-rules-06 (224 checks), c-enc-09 (166), c-enc-10 (116), c-w3fix (4), and the bosspilot modes
rules06 (each of the 21 rules against six bosses), enc09 / enc09-seeds / enc09-auto / enc09-finals / enc09-rooms /
enc09-cards and enc10 / enc10a / enc10b / enc10-nab / enc10-cards / enc10-shrine / enc10-seeds / enc10-hagatha, each twice
identical with 0 faults, fallbacks, failed checks and swallowed leaf exceptions; every family 06 rule was also played
against twelve bosses of families 09 and 10 (Marin's Return and Plague of Death against the eleven they may meet).

### Wave 4: Monster Hunt bosses B and C, Rumble Run champions and boss shrines (families 11, 12, 13; 2026-11-14)

Three families landed together, with the DALA_850 fix: thirteen Monster Hunt encounters with a clickable power and Crooked
Pete's form change (11), thirteen Monster Hunt encounters, ten of them with a passive power (12), and the eight Rumble Run
champions beside War Master Voone with every board shrine admitted for a boss (13). The shipped encounter list holds 103
rows (wave 3's 69, 13 of family 11, 13 of family 12, 8 of family 13) and 124 decks, 54 of them the wiki's list unchanged.
The schedule is unchanged (43 rule rows). All of it stays behind acts-v3h and acts-v3t; no default changed. This is the
first wave integrated and verified on Linux (the cloud container); `verify/baseline` now holds its results.

#### Family 11: Monster Hunt bosses B (clickable powers, Crooked Pete's form change)

Thirteen Witchwood encounters with a clickable power, tier by printed Health (20: tier 1; 30: tier 2; 35, 40: tier 3; 50,
60: tier 4). Crooked Pete is the first boss whose form changes inside a match. At the start of his turn at half his maximum
Health or less, Soot Up's trigger makes him Beastly Pete (a script form of the encounter model: GILA_BOSS_52h2 with Beast
Within). His hand is set aside, Beastly Pete's deck replaces his, he draws 6, gains 15 Armor and keeps his Health. Face
Collector plays a copy of the run deck (flag `mirrorDeck`: less what a boss may not hold, Fabled bundles whole, the curated
row deck below 10 cards). Raeth's Desecrate is readied again after a use that killed (private tag `PowerRefresh` 32646, its
own Inspire trigger). Survival of the Fittest forces every minion to attack a random enemy minion
(`EncounterRules.AllAttackRandomEnemyMinions`). Grave Mistake resolves every Deathrattle twice. Shallow Graves never summons
a dead run shrine. Terrify shuffles through the engine's MoveToDeck (a shrine goes dormant instead). Soul Assimilation and A
New Face respect the shrine control and transform rules. Decks are the wiki's tables; Damph's and Sezavo's 20 cards are
extended to 30, and Witchwood's Touch is Shield Block until family 23 scripts it. Scripts: `DungeonRunEncounters.F11.cs`,
`Actions/EncounterRules.F11.cs`; five admission rows (`boss-hunt-power`, `boss-hunt-own-hand`, `boss-hunt-form`,
`boss-hunt-deck-shuffle`, `boss-hunt-card-b`). Grave Mistake the spell (GILA_BOSS_43t, the hunt's treasure) is in no deck:
scripted, admitted and tested from a fixture boss's hand.

#### Family 12: Monster Hunt bosses C (passive powers)

Thirteen Witchwood encounters, ten with a passive power started at match start, tier by the printed Health of the hero
card (10 tier 1; 25, 30 tier 2; 40 tier 3; 50, 70 tier 4). Groddo raises a Bloated Zombie when an enemy minion dies; Gretta's
Blood Red Apple makes every spell cost Health (Blood Pact's rule on both players since the review below); Forlorn Lovers
shuffles a copy of each friendly minion that dies into her deck, presented on the shuffle so her own-deck ledger learns it; S'thara's Poisoned Drink deals 1
damage per card drawn to either player; Vitus buffs his board; Shudderwock doubles all Battlecries for both players; The
Whisperer gives every spell in either hand Echo, with at most ten Echo copies per player per turn (standing in for the
client's turn timer; `EncounterRules.EchoCopiesPerTurn`); Sephira draws a spell when the run player casts one; Gobbles
draws when a friendly Beast dies; Hannigul's Cannibalism gives +1 Attack to the minions that stood next to a dying minion,
read inside the death pass (`EncounterRules.DeathNeighbours`). Winslow's Hypnotize shuffles both hands into their decks as
plain cards and redraws as many, recording a hand-and-deck belief reset for both players; The Mute
silences; Rancel's Poison Flask deals 2 and poisons a survivor, and his Infected Quillflinger hits a random enemy minion
whenever it takes damage. Decks are the wiki's lists brought to 30 cards, each change recorded. Scripts:
`DungeonRunEncounters.F12.cs`, `Actions/EncounterRules.F12.cs`; six admission rows. No private tag.

#### Family 13: Rumble Run opponents and boss shrines

The eight Rumble Run champions beside War Master Voone (Zentimo, Captain Hooktusk, High Priest Thekal, Zul'jin, Wardruid
Loti, High Priestess Jeklik, Hex Lord Malacrass, Princess Talanji), each a row whose three variants (early, middle, late =
challenges 1-2, 3-5, 6-8) are its class's three board shrines with that range's wiki deck. The fight's variant is drawn
from the run seed and the floor; the shrine is armed for the boss by `ShrineRules.Arm` exactly as the run player's, and
every shrine script reads its own controller, so the phase-7 scripts serve both sides unchanged. All 27 shrines are now
admitted for a boss: Halazzi's Trap needs the boss's generated Secrets (Zul'jin's row turns them on), Treasure from Below's
steal teaches the boss exactly the card it took and nothing else, and Bwonsamdi's Tome and Dark Reliquary generate through
the boss's filter. Zul'jin's Steady Throw is scripted as Steady Shot; the other champions use basic powers. Tiers are a
recorded spread (a champion meets the run in any round). The champions' teammate cards are not admitted for a boss and are
left out of their decks, each recorded. The boss's fair view now tolerates a chained Discover that is not yet dealt
(Griftah: two null guards in `plugin/FairBotState.cs`; the same hazard in `plugin/NativeMatch.cs` is left for the Windows
plugin build). The phase-7 admission rows board-shrine, hidden-steal and shrine-token, now empty, are removed; their ids
sit in `DungeonRunAdmission.F13.cs`.

#### What the integration changed

- **The Echo copy floor holds in every game.** The client's rule since Descent of Dragons is that copies of Echo cards
  "can't cost less than (1)" (hearthstone.wiki.gg/wiki/Echo). Family 12 added `EncounterRules.EchoCopyFloor` (the last step
  of `CostRules.AdjustCost`) for The Whisperer's fight only; family 11 then met a (0)-Cost Sound the Bells! (GIL_145) repeated
  without end by the Rumble Mage row's run player under a cost cut, with no Whisperer in the game. The floor now applies to
  every Echo copy (a GHOSTLY card: an Echo or "Repeatable this turn" copy) in every game; The Whisperer keeps only the
  per-turn copy limit and the Echo on every spell (c-enc-engine G7d, c-enc-12 G10). Family 11's shrine bosspilot order is
  restored (Gustave meets the Mage row again).
- **DALA_850 and Unstable Evolution.** The copy Unstable Evolution puts into the hand (LOOT_504t) lacks the client's
  "Repeatable this turn" tag 1114, so Anomaly - Arcane cut it to (0) and Hagatha's boss replayed it without end ("stuck after
  1500 actions" in `bp.sh rules01 DALA_850`, open since the port). `DungeonRunEncounters.Repeatable` names the copy, so the
  anomaly's floor keeps it at (1) (c-enc-engine G7c); the game ends normally (boss win, 22 turns).
- **Tag table.** Taken numbers: 32637 (family 02), 32641 (06), 32643 (08), 32644 (09), 32645 (10), 32646 (11); families 12
  and 13 took none.
- The ledger gains 56 rows, not 57: FP1_029 Dancing Swords is new in both family 12's and family 13's decks.
- **Review fixes (confirmed findings, each with a test).** Blood Red Apple ("Spells cost Health instead of Mana.", no
  "Your"; the wiki gives Gretta blood-magic lines for the player's casts) is both players' rule (row flag `bothPlayers`,
  admission row boss-passive-both). Hypnotize and Together Forever no longer record a deck fact read from the real deck:
  Archbishop Benedictus (in Winslow's deck) shuffles copies of the run player's hidden deck into the boss's, and the fact
  taught the boss's own-deck ledger what they were; the ledger now follows the moves, and the Lovers' public copy is
  presented on its shuffle. An Echo copy Hypnotize shuffles back leaves `Game.GhostlyCards`. A Rumble Run champion appears
  only "if the player has not chosen one of the <class> shrines" (the champions' wiki pages): a run with a board shrine
  never draws a row with a shrine variant of that class (`DungeonRun.ActBossSequence`, `excludeShrineClass`; runs without
  a board shrine draw exactly as before). Four tests that could not fail were made real (c-enc-11 B6/B7, c-enc-12 M4,
  c-enc-13 V2).
- **A cost callback and the fair view.** `bp.sh f04-player` met a fallback decision (KeyNotFoundException): the run
  player's TRL_313 Scorch keeps an end-of-turn cost callback (its adaptive cost), and the boss's fair view masks and
  renumbers the run player's hidden hand, so cloning the view for a search step found no entity under the callback's
  owner id (`TriggerManager.CloneHandlers` -> `AdaptiveCostEffect.CloneDetached`). A latent defect the rebuilt fixtures'
  seeds reached; such a detached callback is now dropped (c-w4fix Y1-Y4; every other result unchanged).
- **Steady Throw and Rikkar.** c-heroes-F14 T1/T2 pinned TRLA_065p as unscripted; it is now Zul'jin's scripted encounter
  power, still refused as a Rikkar power (by the admission table instead of as unscripted).

**Open after the wave.** Random generation can yield unscripted cards (the engine's random pools include them; the
generation filter drops only what admission rejects; seen: DRG_301, DRG_246, DRG_402). `plugin/NativeMatch.cs:6634` has the
chained-Discover null hazard family 13 guarded in `FairBotState.cs` (needs the Windows plugin build). The champions'
teammates are not admitted for a boss (their decks would be more authentic with them). Witchwood's Touch (GILA_BOSS_99t2)
is Shield Block in families 10-12 until family 23 scripts it; then rerun the encounters step and update c-enc-10 P3/P8,
c-enc-11 P3/P7 and c-enc-12 P3/P7.

**Tests of the wave.** c-enc-11 (109 checks), c-enc-12 (105), c-enc-13 (112), c-w4fix (4), c-enc-engine G7c/G7d, and the bosspilot modes
enc11a / enc11b / enc11-pete / enc11-cards / enc11-shrine, enc12 / enc12-seeds / enc12-hidden and enc13a / enc13b /
enc13-seeds, each twice identical with 0 faults, fallbacks, failed checks and swallowed leaf exceptions.

### Wave 5: Dalaran Heist bosses, the Heist finale and Tombs of Terror bosses A (families 14-19; 2026-11-16)

Six families landed together: the Dalaran Heist's regular encounters in five lanes (14: DALA_BOSS_01-15, 15: 16-30, 16:
31-45, 17: 46-59, 18: 60-75 with the four fixed bosses of the Heist finale and its rule DALA_830) and the first eighteen
Tombs of Terror encounters (19). The shipped encounter list holds 194 rows (wave 4's 103, 15 of family 14, 14 each of
families 15, 16 and 17, 16 of family 18, 18 of family 19) and 215 decks, 95 of them the wiki's list unchanged; the
schedule lists 44 rule rows (DALA_830 is new). Everything stays behind acts-v3h and acts-v3t: no default changed, the
acts-v3h finale block (now with the twist pool DALA_830) ships disabled with its bosses pending.

The Heist boss pages have no level table and their heroes print 30 Health (10 at level 1), so all five Heist families
tier by one rule: a page's stated level, else its deck table's size (10/15/20/25/30 cards: levels 1/2/3/4-5/6, the lowest
level holding the largest size), a chapter's final boss level 8 (tier 4); the fight's Health is the act curve's. The Tombs
heroes print 10 Health; family 19's tier rule (`verify/tools/gen/p8f19/tombs_tiers.py`) is shared with the later Tombs
families.

#### Family 14: Dalaran Heist bosses A

Fifteen encounters (DALA_BOSS_01-15); tiers 1/2/3 hold two, nine and four rows. Decks are the Normal pool with second
copies, then the Heroic pool, brought to 30; Oshi's and Albin's quantity lists are unchanged. New engine rule: a
power-only script form (`EncounterRules.SwapPowerForm`) keeps the hero entity, Health, Armor and enchantments while the
power changes: Belnaara's Take Flight! counts 15 Health damage on the power's public counter, then becomes Flying! with
her hero Immune for two of her turns, then lands. "Scourgelord" Dazzik is a Heroic deck card (Scourgelord Garrosh's rule)
shuffled in by the row's start block, not a form. Akazamzarak's four feats act on both players: Reductomara's (1) less
never takes an Echo or Repeatable spell below (1) and records each owner's deck as rewritten; Presto Legendaro uses each
side's generation filter. Open Wormhole is Duel!'s rule. Mana Echoes recasts a spell of the public play history through
the boss's generation filter: never a Quest, and a Secret only when it is the boss's own and its Secret zone has room and
no copy (the Monocle's rule; a run player's Secret would be a boss-generated Secret of any class, which only a row with
`bossGeneratedSecrets` may hold). The "Kind" Waitress uses the wrong-target rule (WrongTarget50) as a defender. Blessing of
Elune is ALL_HEALING_DOUBLE on both players; converted healing (Auchenai, Bwonsamdi's Covenant) deals the doubled damage.

#### Family 15: Dalaran Heist bosses B

Fourteen encounters (DALA_BOSS_16-20, 22-30); Jepetto, a chapter final, is tier 4. Equivalent Exchange and Totemic Summons
are cast by rule (the wiki's auto-cast). Equivalent Exchange moves one random card each way by the hand-transfer rule, so
each side knows the card it gave. Twin Paths uses Fandral's CHOOSE_BOTH aura. Stormswell recasts Overload spells on the
same target, and the copy overloads too. Ar'ha's Call adds one Battlecry resolution to the next friendly card with a
Battlecry (a minion, weapon or hero card: the cards `BattlecryRules.Additional` serves). Assembly gives either drawer a 1/1
copy of a drawn minion. Scroll Savvy makes any drawn spell cost (0) that turn (its text names no player). Excavate takes
the wiki's six listed results with equal odds; A Small Rock is also admitted for a boss on catalog runs. The Underbelly
Rat replaces a dead hero (`Actions/EncounterRules.F15.cs`, called first in `ResolveHeroForms`): the eldest living rat (by
entry into play; a Silenced rat has lost the text) leaves the board without dying and the hero returns with its Health;
for The Rat King that is a new hero through his row's script form.

#### Family 16: Dalaran Heist bosses C

Fourteen encounters (DALA_BOSS_31-42, 44, 45); Moragg is tier 4. New engine rule: the board rotation
(`Actions/EncounterRules.F16.cs`, `EncounterRules.Rotate(bottom, clockwise, source)`): the two rows are one ring, one step
moves one minion across each way as a control change, rows keep their size (an empty row gains the crossing minion), and
untouchable entities and run shrines never move. The Carousel Gryphon rotates counter-clockwise as the run player sees it
at the end of its turn; family 24's K'zrath reuses it. Kizi's Makeover and Aki's For The Light! are auto-cast (family
09's AutoCast). Bling it On! uses the client's Golden (PREMIUM) tag. Import "Pet" is Build-A-Beast's craft through the
boss's generation filter, the pet named Exotic Pet. Tales of Fortune shuffles one of twenty Kobolds treasures into Ol'
Toomba's deck, shown (Gloves of Mugging and Aleatoric Cube left out: hidden zones); five more treasures (Orb of
Destruction, Horn of Cenarius, Portable Forge, Quel'Delar and its Blade) are admitted for a boss on catalog runs.
Pogoshuffle's copies are shown as shuffled; Dark Tidings reveals a deck minion and puts it on top in place, recorded as
DeckTopKnown. PARTY PORTAL!'s Partygoers are the wiki's five (Mime enters silenced). Gold Elemental's Heroic pool begins
with Infuriated Banker, which family 17 scripts: in the merged deck it and Flame Imp are the two Heroic fills.

#### Family 17: Dalaran Heist bosses D

Fourteen encounters (DALA_BOSS_46-59) with ten boss cards. Zuramat's To The Void banishes a minion into the Void (family
10's set-aside store, held by its Voided enchantment): no death, and a run shrine never enters it. Void Shift destroys all
minions and summons new copies of the Void for its caster; the run player starts with two. Boommaster Flark: the run
player starts with The Wire, a Murloc Tinyfin and The Other Wire; touching wires deal 5 damage once per pair at the start
of that player's turn. Madam Goya's Blackmail puts a run player's card in hand escrow (the Extortion takes its slot),
linked by the private tag `ExtortionEscrow` 32652; the boss never sees it, and Extortion returns the card itself. Queen
Wagtoggle's Bribery summons a minion from the run player's deck (Tad's Pole's rule) that returns as a copy when it dies.
Spell Lock Overloads both players for the cost each spell was played for; the engine's Tunnel Trogg answers only printed
Overload, so `EncounterRules.OverloadFor` buffs each Trogg itself. Greed is Good buys the first card the run player plays
each turn for a Gallywix's Coin. Sand Breath and Raise the Alarm (wiki auto-cast) and Bribery and Blackmail (their value is
hidden from the boss's fair view) are cast by rule. Infuriated Banker replaces Gallywix's unscripted Fel Reavers.

#### Family 18: Dalaran Heist bosses E and the Heist finale

Twelve encounters (Sael'orn, Haro Setting-Sun, Dagg Cruelmight, Banker Biggs, Valdera Highborne, Marei Loom, Lilayell
Suntear, Sky Captain Smiggs, Anarii Duskgrove, Kazamon Steelskin, Hesutu Stonewind, Kara Stamper) and the four fixed
bosses of the Heist finale (Magistrix Norroa, Archmage Vargoth, Archmage Kalec, Archmage Khadgar; tier finale, levels
9-12, decks of 35/35/40/40). Twist - The Finale (DALA_830) is the rule of acts-v3h's finale block: a twist with effect
run, rolled with chance 100 for the finale act, never in a match plan. The block ships disabled; the fixture
`verify/fixtures/p8f18/fixtures/schedule-finale18.json` enables it (the close-out re-pins acts-v3h's shape digest when it
enables the block). New engine rules (`Actions/EncounterRules.F18.cs`): a player's short turn timer (`ShortTurnSeconds`,
read from the base TIMEOUT tag: Nozdormu the Eternal's 15, Kaironomaly's 25, which NativeMatch now enforces; classic
Nozdormu's aura neither makes nor cancels one), Steelskin's self-damage prevention (tag `SelfDamageImmune` 32653, read in
`Character.TakeDamage`; Fatigue still hurts), and Marei Loom's left-most hand/deck swap with belief resets for both
players (an Echo copy swapped into the deck stops being ghostly; cast by rule at the boss's turn start, the wiki's auto-cast,
since the close-out review). Kirin Tor Elemental recasts its controller's spells
(RecastRules). Rejected with reasons: Commander Bolan (75h/75p: his quests have no text, requirement or reward in the
client data), the unused Phase Spider (61t) and Cheerful Spirit (DALA_501).

#### Family 19: Tombs of Terror bosses A

Eighteen encounters (ULDA_BOSS_01-18) and Sinkhole's Sand Trap (ULDA_030). A row's Health is the act curve's and its tier
the boss's level in its chapter, read from its wiki deck table ("10; 15" levels 1-2 tier 1; "15; 20" and the 25-card
level-4 lists tier 2; the 30-card level-6 lists tier 3; a full 30-card list with no lower level tier 4). Twelve
clickable powers, Sand Surge cast by rule (the wiki's auto-cast, since the close-out review) and five passives (E.V.I.L. Promotion keeps its growing bonus in the power's own script number; Sword
Dance shuffles a copy of the boss's destroyed weapon, enchantments and their Durability kept, into its deck; Under Wraps,
Unstable Weaponry, "Confiscated" Goods). Mummy Ritual and Belch read the public graveyards and never raise a dead run
shrine; Venomous Stinger's destroy makes a shrine go dormant. Sand Surge puts Sand Trap into the run player's deck as a
public shuffle (match only, never a run deck record); drawn, it moves the top two cards of its holder's deck to the
graveyard and draws; the boss's fair view keeps that deck masked. Brick by Brick is a boss-side Discover through the
generation filter. Decks are the wiki's Normal lists, brought to 30 with second copies and the Heroic list; unscripted
Saviors of Uldum collectibles are replaced by named scripted cards of the same role (Lackeys for Vera's).

#### What the integration changed

- **Auto-cast powers act after the turn-start draw.** Family 09's `AutoCast` fired on TURN_START, before `MainDraw`, so a
  rule-cast power acted before its boss drew; the wiki puts it after the draw (Hero Power page: "the boss's first action
  each turn"; Auto-cast page: after start-of-turn triggered effects). A new hook, `EncounterRules.AfterTurnStartDraw`, runs
  in `Game.MainStart` right after `MainDraw()` and before the death and aura pass; `AutoCast` registers its power's
  `AfterDraw` task there (cost, condition, cast count and effect unchanged). All eleven auto-cast powers of families 09,
  15, 16 and 17 move; every bosspilot game where one plays moves with them. Checks c-enc-16 I6 (For The Light! buffs the
  minion drawn that turn) and I7 (a hand of spells plus a drawn minion still casts); c-enc-16 I2 and c-enc-15 A1 restage
  their premise after the draw.
- **A run never meets a boss row of the character it plays.** A hero row may name boss rows in `excludeBosses`
  (run-v1.json, `DungeonRunClass.ExcludeBosses`); `DungeonRun.NewRun` passes them to `ActBossSequence`, which treats them
  as already used. Squeamlish (DALA_Squeamlish) excludes the Tombs boss Squeamlish, Mr. Chu (DALA_Chu) the Tombs boss
  Mister Chu, George the Fallen (DALA_George) the Kobolds boss George and Karl (the wiki's George_the_Fallen lore: "They
  later make an appearance as a boss in Dungeon Run"). Rows without the key draw exactly as before; the validator refuses
  an unknown boss id and the key on a class row (c-w5fix Q1-Q6). Family 21's Barkeye will use the same key.
- **A weapon replaced before the death pass.** `Game.ClearWeapons` names the hero, not the weapon, so a weapon broken in
  an attack and replaced before the next death pass took the new weapon with it. `Hero.AddWeapon` now drops the hero's
  pending entry when it replaces the weapon; family 19's local workaround is removed (c-w5fix W1-W3, c-enc-19 F3/F4).
- **Unstable Evolution's copy has the Echo floor.** The regression met an endless chain in `bp.sh enc15`: against Rasil,
  whose Millhouse Manastorm makes the run player's spells cost (0), Vessina's Unstable Evolution on a Clockwork Giant (no
  minion costs one more, so it stays) put a (0)-Cost copy into the hand each time. The engine's copy (LOOT_504t) is no
  GHOSTLY card, so wave 4's Echo copy floor missed it; the client prints the card with Echo since patch 23.0.3 (the wiki:
  changed from "Repeatable this turn" to stop such a loop), so `EncounterRules.EchoCopyFloor` floors that copy at (1) too
  (c-w5fix E1-E3).
- **One chained-Discover guard.** Families 13 and 16 both guarded a queued Discover with no offers yet in
  `plugin/FairBotState.cs`; family 13's guard covers both cases (c-enc-16 A8), so the file is unchanged since wave 4.
  `plugin/NativeMatch.cs:6634` still waits for the Windows build.
- **Tag table.** Taken numbers: 32637 (family 02), 32641 (06), 32643 (08), 32644 (09), 32645 (10), 32646 (11), 32652
  (17, ExtortionEscrow), 32653 (18, SelfDamageImmune); families 12-16 and 19 took none.
- **Tunnel Trogg.** The shared LOE_018 script answers printed Overload only; family 17 buffs Troggs by hand for Spell
  Lock (a comment at the shared script says so; HANDOFF 3.4).
- **Merge-review fixes.** The Gold Elemental deck note said Infuriated Banker had no script (family 17 scripts it; the
  merged deck holds it); c-w5fix N1 checks that no encounter note says "no script yet" of a card a deck holds. c-enc-15 A1
  restages Equivalent Exchange so each hand holds one card after the boss's draw.

**Open after the wave.** Norroa's turn timer needs the Windows plugin build and a client check (NativeMatch). The
own-deck ledger under-informs the boss when revealed shuffled-in cards are later burned (family 16 flag 7; never a
leak). Kingsbane's engine helper drops enchantment Durability as Sword Dance did before its fix. The engine's Lackey
pool can hand out the unscripted Titanic Lackey and Draconic Lackey. Commander Bolan, Phase Spider and Cheerful Spirit
stay rejected.

**Tests of the wave.** c-enc-14 (117 checks), c-enc-15 (103), c-enc-16 (124), c-enc-17 (123), c-enc-18 (109), c-enc-19
(115), c-w5fix (19), and the bosspilot modes enc14 / enc14-seeds / enc14-flight / enc14-cards, enc15 / enc15-seeds /
enc15-rat / enc15-toys, enc16 / enc16-seeds / enc16-carousel / enc16-cards, enc17 / enc17-seeds / enc17-void / enc17-cards
(+ enc17-check), enc18a / enc18b / enc18-finale and enc19a / enc19b / enc19-seeds-a / enc19-seeds-b, each twice identical
with 0 faults, fallbacks, failed checks and swallowed leaf exceptions.

### Wave 6: Tombs of Terror bosses B-D, the Monster Hunt nemeses, the Plague Lords and Tekahn (families 20-24; 2026-11-16)

Five families landed together: the rest of the Tombs of Terror encounters in three lanes (20: ULDA_BOSS_19-36 with Mogu
Cultist, 21: 41-59, 22: 60-79 with Siamat's Servant), the four Final Nemesis fights of The Witchwood (23) and the three
other Plague Lords with Tekahn and ULDA_115 (24). The shipped encounter list holds 255 rows (wave 5's 194, 18 of family 20,
17 of family 21, 18 of family 22, 4 of family 23 and 4 of family 24) and 296 decks, 125 of them the wiki's list unchanged;
876 adaptations and 4 rejections (Toki's hero card, Jythiros of Questions, Commander Bolan, Alternate Timeline). Everything
stays behind acts-v3h and acts-v3t: no default changed, acts-v3t's finale (now with Tekahn pending) ships disabled.
The Tombs families tier by family 19's rule (`verify/tools/gen/p8f19/tombs_tiers.py`, unchanged); Health is the act curve's.

#### Family 20: Tombs of Terror bosses B

Eighteen encounters (ULDA_BOSS_19-36) with Sothis's Mogu Cultist (ULD_705, a Saviors of Uldum stub now scripted in
`UldumCardsGen.cs`: seven Cultists become Highkeeper Ra, ULD_705t). Eleven clickable powers, three cast by rule (Power of Wrath, Mighty Windstorm, Out of Control: the wiki's auto-cast, since the
close-out review) and four passives. Spectral
Swordsman gives the boss's hero Stealth while it holds a weapon; Staking A Claim gives every minion +1/+1 whenever either
player plays a Discover card; Mother of Sand summons a Dust Devil when the boss Overloads, resolved in place; Fireshaper is
Flamewaker's trigger. "Retired" Treasure Hunter copies a card of the run player's hand by the Copy rule (in-hand
enchantments kept, as Mind Vision) and Mighty Windstorm makes each player swap a hand minion with a board minion (never a
run shrine); both read the run player's hand only as their text says, and in the boss's fair view that hand is masked, so
the search uses only cards the boss saw go there. Out of Control hits the center-most minions of each board; Vengeful
Construct destroys a chosen minion with 2 or less Health and its neighbours (a shrine goes dormant); Madness Incarnate makes
the hero attack three different random enemy minions through `EncounterRules.ForcedAttack`. Decks are the wiki's Normal
lists, brought to 30 with the Heroic lists; unscripted Uldum collectibles, and Hydrologist (which offers Secrets), are
replaced by named scripted cards of the same role, each recorded. Mogu Cultist is a scripted 1-Cost collectible now, so it
is in every random pool of implemented cards.

#### Family 21: Tombs of Terror bosses C

Seventeen encounters (ULDA_BOSS_41-59 without 44, the Trap Room, an earlier row, and 58). Jythiros of Questions (58h/58p) is
rejected: Riddle of the Sphinx needs a decision of the run player on the boss's turn and a Counter for every card type
(phase 9 / client), recorded in F21.json, curation E21 and the admission row `boss-tombs-c-rejected`. Lich Baz'hial swaps
her power inside the match: Lich's Phylactery (restores 20, less 1 per friendly minion death) breaks when used into Broken
Phylactery, the row's script form 1 that changes only the power (family 14's `EncounterRules.SwapPowerForm`, shared since
the merge), and the boss then takes double fatigue damage (`Draw.PreDrawPhase` asks
`DungeonRunEncounters.TombsC.FatigueDamage`; one string compare per fatigue hit in every other game). Maw of the Desert and
Arena Expert are cast by rule after the boss's turn-start draw (the wiki's auto-cast). Spoil the Ending copies a random
spell of the run player's hand without linking the copy to its source; the boss's fair view keeps that hand masked. Sand
Shapin' shuffles a public copy of an enemy minion into the boss's deck, summoned when drawn. Run shrines are never copied
(What's in the Jar?, Sand Shapin') and go dormant when destroyed (Maw). Unscripted Saviors of Uldum collectibles and Secret
generators in the wiki lists are replaced by named scripted cards.

#### Family 22: Tombs of Terror bosses D

Eighteen encounters (ULDA_BOSS_60-79 without 64 and 67) and Siamat's Servant (ULDA_026). Twelve clickable powers:
Forbidden Power and Hunt the Weak keep their growing number in the power's own script number; Chain Lightning jumps to
random adjacent minions until one dies; Ogre-Sized Thief steals a card of the run player's deck by TRLA_186's deck-steal
rule, so the boss learns only that card; Mech "Pilot" forces a Mech to attack (`EncounterRules.ForcedAttack`). Three
passive powers: Plaguebringer (spells (2) cheaper, Overload (1) each; a Tunnel Trogg and Thunderhead answer that Overload,
the Trogg through family 17's `EncounterRules.OverloadFor` since the merge), Mistress of Magic (both players' first spell
copied) and Shockwave (Rush). Three powers the wiki lists as auto-cast are cast by rule after the boss's turn-start draw:
Lord of Destruction (stacking Cursed!), Massive Tentacles (destroys all at 8+ minions; a run shrine goes dormant) and Mirage.
The client data has no illusion token, so Mirage follows the wiki's notes: a copy of a random card of the run player's
hand joins it, one of the pair is the illusion (the card carries the private tag `MirageIllusion` 32657; the boss's power
holds only its public hand position), playing it discards a random card, and it fades at the end of that player's turn.
Decks are the wiki's Normal lists, extended with second copies and the Heroic list; unscripted cards are replaced by named
scripted cards.

#### Family 23: the Monster Hunt nemeses and Hagatha

The four Final Nemesis fights of The Witchwood join Hagatha the Witch as act II majors (pool enc-major2; Health is the act
curve plus 10). Infinite Toki's Chronoacceleration gives a friendly minion the client enchantment Accelerated (Mega-Windfury,
destroyed at the end of the turn). Glinda Crowskin's passive False Heart makes her hero Immune while she controls a minion
(family 06's HeroImmuneWhile); her row's flag heroImmune keeps ULDA_706 out. Crowskin Faithful draws and shuffles a new
Faithful into its controller's deck when it dies; Crowskin Pact transforms a minion into a Faithful and takes control of it
(a run shrine goes dormant instead). Lord Godfrey's Pistol Barrage deals 2 to a minion and its neighbours; he starts with
Hunter's Insight in hand (the Final Nemesis page), the wiki table's one copy, so his deck holds the other 29. Captain Shivers
starts with Kingsbane equipped, and his Plunder is cast by rule after his turn-start draw while his deck holds a weapon and
his hand has room. Cursed Crewmember draws two cards and shuffles a copy of its caster's hand into that caster's deck.
Witchwood's Touch (1), (2) and (3) are scripted; (2) replaces the Shield Block stand-in in the Monster Hunt decks of
families 10-12 (the merge did families 11 and 12). Hunter's Insight and Clockwork Assistant are admitted for a boss on
catalog runs (`run-treasure-nemesis-deck`). Alternate Timeline (a game restart) and Enhance-a-matic (a Secret generator of
every class) are left out of Toki's deck; the first is rejected to phase 9. Hagatha's temporary hero switching stays family
01's run-owned `DungeonRunState.HeroSwitch`, never written into the run's hero, power or deck records. Toki's hero card stays
rejected (Temporal Loop, phase 9). With five GIL majors Hagatha is the act II major in about a tenth of runs (118 of 1000).

#### Family 24: the Plague Lords and Tekahn

Xatma, K'zrath and Vesh join Icarax as major3 encounters of three hero forms (300/200/100; each phase replaces the deck with
its wiki list and draws 3, Vesh 5); a lost fight banks the lord's Health in the profile (`BossDamage`) and the next
random-seed run meets it in that phase. ULDA_115 "Gnomebliterator" is a Tombs treasure of encounter schedules: a cast inside
a Plague Lord fight deals its extra 10 at once; outside one, either player's cast banks 10 plus its Spell Damage in the run
(`DungeonRunState.PlagueLordDamage`, an append-only save field with a ShouldSerialize guard, save version unchanged), which
the next Plague Lord fight starts with and spends. Tekahn is the acts-v3t finale row: one hero, power-only forms at 200
(Curse of Flame) and 100 (Soul Pyre), 40-card phase decks, no persistent damage; the run player's hero fights first and then
the explorers relay at 50 Health each, each with one of its three client finale decks and the matching hero power, drawn by
the match's game RNG as it enters (`EncounterRules.ResolveRelay`), so the boss's fair view cannot know it. New engine rules
(`EncounterRules.F24.cs`): LoseHealth (Health lost, not damage; forms resolve at once), Evoke Panic's forced attack inside
one row, BringToOne; K'zrath rotates with family 16's `Rotate`. Private tag `PlagueCount` 32659 (ULDA_115 casts, Mur-nado's
Murloc deaths, Mind Meld's Lifesteal mark). Shared card fixes: Omega Mind's Mind Meld (BOT_543e) gives the spells cast that
turn Lifesteal and takes it back at the end of the turn; the Lackey aura ULD_168e/e3 (Rakanishu's ULDA_033 uses it); a cast
guard in `CopyCthun` (STOP instead of an InvalidCastException when the target is not a minion).

#### What the integration changed

- **A run never meets a boss row of its own character (continued).** Ol' Barkeye (DALA_Barkeye) excludes the Tombs boss
  Ol' Barkeye and Vessina (DALA_Vessina) the Tombs boss Vessina (the wiki's boss pages: "one of League of E.V.I.L.'s
  henchmen that made debut in The Dalaran Heist"); with wave 5's three pairs that is five, each run on 600 acts-v3h seeds
  in c-w5fix Q3-Q5 (0 meetings with the key, some without). Every hero row was checked against all 255 boss rows and the
  custom bosses; no other pair. The Heist hero Tekahn and the Plague Lord Tekahn are the same character, but that row is a
  finale row in no act pool and a finale's fixed bosses ignore `excludeBosses`; the merge review dropped stage A's inert
  key, and c-w5fix Q7 now fails on purpose once a shipped finale (or a pool) can hand the row to a run: the close-out
  decides. Infinite Toki stays eligible for the Toki hero row (the original makes her Toki's own Final Nemesis).
- **Auto-cast after the turn-start draw, everywhere.** The family 21-23 powers registered through family 09's `AutoCast`
  (46p Maw, 48p Arena Expert, 72p, 78p, GILA_BOSS_60p Plunder) act after the draw since the merge; c-enc-23 D3/D5 state
  Plunder's premise (the weapon not on top of the deck) and D3b shows a weapon on top is taken by the turn's draw. Mirage
  (ULDA_BOSS_62p), a hand-written TURN_START trigger, was missed by stage A and moved by the merge review: its cast is the
  registered entry's `AfterDraw` (c-enc-22 C9b: a 1-Health boss with an empty deck dies of Fatigue in its draw and casts
  nothing; with the old trigger it copied a card first).
- **Witchwood's Touch in the decks of families 11 and 12.** `make_f11.py` and `make_f12.py` no longer replace GILA_BOSS_99t2
  with Shield Block: Cragtorr, Azalina, Gustave, Beastly Pete's form deck, Groddo and Hannigul are authentic now, Sephira
  keeps her Wrath and Power of the Wild substitutions. c-enc-11 / c-enc-12 P3 and P7 follow.
- **Duplicates folded.** Family 21's `TombsC.PowerOnlyForm` was family 14's `SwapPowerForm` (removed; Broken Phylactery
  calls the shared one). Family 22's Plaguebringer Overload calls family 17's `EncounterRules.OverloadFor` for Tunnel Troggs
  and keeps only Thunderhead's Sparks. `EncounterRules.F16.cs` and `tombs_tiers.py` are byte-identical to wave 5's.
- **Tag table.** 32657 (family 22, MirageIllusion) and 32659 (family 24, PlagueCount); families 19-21 and 23 took none; the
  21 private constants in 32630-32699 are unique.
- **Snowfury Giant counts every Overload.** The regression met a fault in `bp.sh enc22a`: against Vessina, whose
  Plaguebringer Overloads each spell by 1 through `EncounterRules.OverloadFor`, the boss's search chose a Snowfury Giant
  (ICC_090) at 4 with 4 mana while the game held it at 6 ("engine rejected boss task"). The Giant's adaptive cost added the
  OVERLOAD event source's printed Overload, so an Overload not printed on its source (Plaguebringer's, Spell Lock's, Winged
  Aberration's) was not counted and a printed one could count twice; it now adds the event's amount, which is what
  `OverloadThisGame` (the Giant's initial count) counts. A printed Overload's event amount is the card's, so other games are
  unchanged (c-w5fix O1-O3).
- **A form without Health of its own.** Tekahn's two forms are power-only: the hero keeps its 300 maximum, as the client's
  one ULDA_BOSS_67h serves all three phases, so a form threshold does not cap his Health (the Plague Lords' phase heroes
  print 200 and 100, which do). The bosspilot harnesses' family 01 check "the boss's Health is above its form's threshold"
  now applies to forms with their own Health only; it had failed once in `bp.sh tekahn24`, a 101-turn game in which Tekahn
  entered Curse of Flame at 195 and healed to 219 in that form.
- **Incompatible modifiers.** Hagatha's `incompatibleModifiers` come from her own row (fixed 100 Health, the hero switch);
  the nemeses list none, since neither the wiki nor the client data covers SoloStone's modifiers. On a seed whose act II
  major is a nemesis the act's modifier node may offer Hagatha's three: a step-9 input (HANDOFF 3.4).

**Open after the wave.** The close-out enables acts-v3t's finale on Tekahn (with `heroRelay`) and decides what a
DALA_Tekahn run meets there. Jythiros of Questions and Alternate Timeline go to phase 9. Captain Shivers's Kingsbane uses
the engine's Kingsbane helper, which drops enchantment Durability (wave 5's open item). Unscripted Uldum collectibles in
the decks of families 19-22 are replaced; when they get scripts, rerun the encounters step.

**Tests of the wave.** c-enc-20 (111 checks), c-enc-21 (100), c-enc-22 (121), c-enc-23 (68), c-enc-24 (65), c-w5fix (29),
and the bosspilot modes enc20a / enc20b / enc20-seeds-a / enc20-seeds-b, enc21a / enc21b / enc21-seeds-a / enc21-seeds-b,
enc22a / enc22b / enc22-seeds-a / enc22-seeds-b, enc23 / enc23-seeds / enc23-cards, enc24, plague24 95 / 110 / 195 / 210
and tekahn24, each twice identical with 0 faults, fallbacks, failed checks and swallowed leaf exceptions.

## Step 9: the economy (2026-11-17)

Step 9 balances the combined reward economy on one schedule, with act-aware encounter and reward scaling, incompatible-
modifier prevention and long-run save/resume validation. The plan is `handoff/phase9/plan.md`; what the code does, per plan
item, is `handoff/phase9/implementation.md`. The step is **implemented and not yet measured**: every target below is a
target, and every number the step tunes is an uncalibrated initial value (`handoff/phase9/values.md`).

**Where it applies.** Two new schedules, **acts-v4h** and **acts-v4t**, at schedule stamp 4, are the defaults. They keep
acts-v3h's node shape (ReleasedShapes[4] equals [3]) and share acts I-III (`sameActsAs`), so the economy is tuned once and
only the finales differ (the Heist's four fights; Tekahn with the hero relay). Every step-9 value is read only through
`DungeonRun.Tuned(state)` (an act run of stamp 4 or later): schedule, tavern and treasure keys, modifier semantics, the
draw, encounter-row `v4` blocks, the new `runs: "v4"` treasure rows and the match-plan knobs. acts-v1, acts-v2, act1,
acts-v3h and acts-v3t play as released, except for correctness fixes (rule exclusions, new boss flags, the board-shrine
ordering at NewRun), which apply to every schedule with anomaly or twist nodes. The save stays at version 6 (§ Save
versions).

### The target: R0 and its slot decomposition

One run-level target, **R0: a fresh-profile Victory rate of 1-3% on each schedule**. The per-slot targets below are its
decomposition (F7's method, extended from 8 floors to 16 and 19 fights), and their product is checked against it, weighting
the act III major by the draw's lord / non-lord share. At the point targets the plan puts the product at about 1.2% (v4h)
and 1.4% (v4t).

| Slot | Point target | Band |
|---|---|---|
| Act I combat, per fight (floors 1-4) | 0.92 | 0.88-0.95 |
| Act I major | 0.70 | 0.60-0.80 |
| Act II combat / major | 0.88 / 0.60 | 0.83-0.92 / 0.50-0.70 |
| Act III combat / major (custom finals) | 0.84 / 0.55 | 0.78-0.89 / 0.45-0.65 |
| Plague Lord as the act III major | mean damage per attempt at least 60; one-attempt kill 10-25% | — |
| Heist finale | 0.80 per fight; all four in one run at least 0.35 | 0.72-0.87 per fight |
| Tekahn finale | 0.45 | 0.35-0.55; at most 3 turn caps in 100 |

Rules of the measurement (plan section 1 B): a fair AI on both sides; n of at least 200 per band (Tekahn 100); a band passes
when its Wilson 95% interval overlaps it and the point estimate lies within it plus or minus 3 points; act I combat beats act
III combat; every t3/t4 row and the named outliers are sampled by name, the four rooms (Vault, Tad, Lava Chamber, Trap Room;
flag `room`) exempt as reward nodes; each origin's rate of reaching the act II major within 15 points of the pooled rate.
The economy targets (plan section 1 A) are deterministic: a deck of at most deckMax 50, entering the finale at a median of
34-42 cards (p95 at most 48); bundle drafts skipped for room converted to gold; at least 90 treasure-draft candidates for
every loadout with treasure drafts; a rare share of 25-35% / 38-50% / 52-65% by act; DALA_904 at most twice a run; Heist :
Tombs tavern offers about 64 : 36; a per-row meeting ratio of at most 2.5 within a tier pool.

### Decisions

| # | Decision (as implemented) |
|---|---|
| D1 | New ids acts-v4h / acts-v4t at stamp 4 carry all tuning; acts-v3h/v3t carry an explicit `"version": 3`, so raising the file's `scheduleVersion` to 4 does not re-stamp them. |
| D2 | v4 keeps v3's node shape; the scaling is in tuning keys and the encounter rows' `v4` blocks, never in shared row fields (which v3 reads live). |
| D3 | Difficulty is measured with a fair player on both sides in a new run harness, not with the old bosspilot's LocalBot (not built yet: the user's). |
| D4 | Act-aware scaling through act keys: per-act treasure rarity, per-fight finale curves, `tavern.weightByAct`, DALA_904 Good Food `maxPerRun` 2 (`CapKnobs` caps HealthBonus at what the tavern can give). Player Health stays 30 and identity offsets stay (+10 for the Dungeon Run finals and the nemeses). Levers for an act I major wall, in order: the floor-5 curve value, a v4 tier override, `offsetCap` (last, an identity change). Curation `health-policy`. |
| D5 | deckMax stays 50. Variant (b): full-size bundles (takes 3/2/2), but no bundle draft after a treasure-draft floor or a major in acts II-III (`bundleAfterTreasure: false`); a bundle skipped for room becomes 1 `BankedGold`, spent at the next tavern. DALA_902 Remove a Card weighs more in act III. |
| D6 | One treasure economy: generic adventure treasures carry `"shared": true` after an audit (126 rows; hero-specific, signature and deck- or power-dependent texts stay scoped, each with a reason in the catalog curation), read only by stamp-4 runs. Draft slots: slot 1 universal, slot 2 the loadout's own origin, slot 3 weighted; per-act rarity weights derived from the audited pool's rare : common counts. |
| D7 | Modifiers on v4: per-fight neutralisation plus a "none" choice. A row's `v4.incompatibleModifiers` zeroes, for that fight only, the knob it cannot carry (bossHealthDelta on a fixedHealth row, bossManaDelta on a row with its own start Mana); the act's other fights keep the modifier, and its out-of-fight effects (Iron Bosses' extra treasure) always apply. Every entry has a `modifierReasons` text. A v4 row without a block keeps the act-wide ban. Released schedules keep the act-wide ban. |
| D8 | Twist pools mapped from the original chapters: act I [DALA_829, ULDA_801], act II [DALA_832, ULDA_802], act III [DALA_833, DALA_831, ULDA_803, ULDA_804]; the acts-v4h finale DALA_830. Chance 100 (each chapter's twist was always on), one twist per act, weighted; a hit with no compatible candidate rolls none. Curation `twists-per-act`, `plague-twists-per-act`. |
| D9 | Per-boss rewards (`reward`, v4 only): GILA_BOSS_43t is Damph's reward, a deck card granted once per run after his fight; at deckMax it becomes 1 `BankedGold`. |
| D10 | Draw `provenance-mix-v2` (stamp 4 only): provenance weight n_p^alpha / (1 + k drawn_p) in exact integer arithmetic, a ledger chance for a banked Plague Lord at the major, at most 1 Rumble Run champion per act. |
| D11 | Tekahn on acts-v4t only: the finale's `turnCap` 150 and `formRatchet` (entering a form with a Health threshold and no Health of its own clamps his maximum Health to it), read into `RunMatchPlan.TurnCap` / `FormRatchet`; Belnaara's and Bazhial's script forms are untouched. |
| D12 | The save stays at version 6; new fields are guarded attributes (`BankedGold`, the restart state, the summary's `Escaped` and `Restarted`). |
| D13 | Restart (GILA_911 Rewind, a v4 treasure; the GILA_BOSS_48t path, card held back until its gate passes): `Fault` plus `Restarted`, the floor restaged, a persistent boss's damage banked as a loss does, a new match seed only when the restart index is above 0, one restart per source per floor and at most 2 x TurnCap turns per floor. A restart from a source the fight could not use is recorded as a loss. |
| D14 | Escape (ULDA_111 Hearthstone, a v4 treasure): `Win` plus `Escaped`, with no major or kill credit and no drafts; an escape tavern opens on the node. Castable only when `EscapeAllowed` (deck above deckMin, not a finale act, not the last node); the card leaves the deck. |
| D15 | Generation hygiene in Dungeon Run games: every legacy generation pool goes through `OfflineCardPools.ForGame`, which drops unscripted ids (unfiltered games get the same array); ULD_616 and DRG_052 are scripted, so the Lackey pool holds 7. |
| D16 | Adaptation criterion for server-side gaps: adapt only when the client text states every effect, the missing parts are triggers or chances, the choice is recorded with its reason, and real games pass; otherwise reject for good (DALA_740: rejected; ULDA_607: rejected, no source decks). |
| D17 | Prevention is a pure predicate (`DungeonRun.ForbiddenPairs`, order-independent) plus gates in the candidate lists (rules, twists, modifiers, treasure drafts, tavern options); BuildPlan only logs a violation and never changes a plan. |

### Values

Nothing step 9 tunes has been measured. Every value is the plan's stated initial guess or one derived from the data, listed
with its source in `handoff/phase9/values.md`: the draw (alpha 0.7, k 1, ledger chance 50, 1 TRL per act), the per-act
rarity (rare 26 / 39 / 53 of 100), `upgradeEveryFloors` 4, the act and finale curves (v3's), twist chance 100, the tavern
weights by act and Good Food's cap, Tekahn's turnCap 150, the restart caps, and 1 banked gold per skipped draft or reward.
Calibration (plan stage 4: fair-player measurement of the slots above, then encounter `v4` tier and `healthOffset`
overrides and curve changes, in the D4 order) is still to do; until then R0 is a target, not a property of the defaults.

**Boss Health and deck size per round (owner, 2026-11-20).** The first boss starts at 10 Health with a 15-card deck, and
both grow with every fight. acts-v4h/v4t (`sameActsAs`) now set `bossHealth` to 10 14 18 22 26 / 30 34 38 42 46 /
50 54 58 62 66 (Heist finale 70 72 74 76; Tekahn 70) and the new stamp-4 act key `bossDeck` to 15-19 / 20-24 / 25-29.
`DungeonRun.SizeBossDeck` (read through `Tuned`) cuts the starting boss deck to its floor's size with a seeded pick
(stream 17, the floor), keeping Fabled bundles and sideboard cards; forms, relay decks, mirror decks and encounters with
identity Health (Plague Lords, Hagatha) keep their own decks, and a finale names no `bossDeck`, so its decks stay whole.
NativeMatch accepts a run boss deck of 10 to 30 cards. These replace the v3 curves the step-9 calibration started from.

## Implementation Status (2026-11-09)

All pieces are written and the plugin builds against client 37.0.2.256589 with SDK 10.0.402 (0 errors; client bindings check PASS, Mono guard PASS). Nothing has run inside the Hearthstone client yet.

| Piece | State | Verified (headless, against the built engine) |
|---|---|---|
| Engine: run setup fields, passive powers, `DungeonRunContent` / `DungeonRunCardsGen` / `DungeonRunSetup`; 16 class powers and `RunContent.Tokens`; `ExcludeGeneratedSecrets`; `EnableCorpses` | Done | 329 content checks (the 16 class powers cast, cloned and replayed; Corpse gain and spend with Body Bagger, Army of the Dead, Tomb Guardians, Battlefield Necromancer and the Ant Husk; the Blood cards Drink Blood, Death Strike, Liferender and Emergency Surgery) (Survey, Magic Trick, Ethereal Conjurer, Runed Orb and Shifting Scroll offer no Secret in 60 run matches each, against 9 to 44 in 60 without the rule); a scan of every starter, bundle and active-treasure card and every player power (60 seeded plays each, playing what they generate) found no Secret. Also: every shell, power, shrine tier, boss passive and treasure in real engine games; each new power cast through `HeroPowerTask` (legal targets, full-board and no-target refusals, effect, clone, same-seed replay, tokens); one-turn tags; determinism and cloning |
| Data: `run-v1.json` and `pool-v1.json`, schema 2, `dungeon-run/2` (11 classes, 33 decks, 52 bundles) | Done | Validator 0 errors, rune checks included (the client-definition check runs only in game); both files parse with the client JSON parser |
| Run layer `plugin/DungeonRun/` (rules, validator, state machine, F2/F5/F6, upgrades, class ladder, treasure scopes, sidecar save and version-1 upgrade, `DungeonRunCatalog`) | Done | 186 checks: all 99 class/deck/power starts and plans, a won run for each Death Knight and Demon Hunter deck and power (own-class or neutral bundles only, runes still legal), refusals, ladder, challenge, scopes over 10,000 drafts, migration of synthetic version-1 saves (offers replayed against version-1 golden lines), catalog; 141 random-play matches with no engine fault |
| Boss AI (`DungeonRunBossPilot`: namespaced deck and profile, derived plan, golden overlay) | Done | 40 games with the real `GoldenPilot` over all 33 starter decks and all 33 powers: 0 faults, 0 emergency fallbacks, no Secret in play |
| Match path: `LocalLadder.StartRun`, `ClientBridge.PrepareRunMatch`, `NativeMatch` run constructor, `DungeonRun` Pending flag (Settle/RecoverInterrupted/Copy), outcome recording with rollback and retry, turn cap, AI-fault restage | Done | Builds; final review found 400 seeded ladder games identical before and after the change |
| Run screen (`NativeDungeonRunUI`: Class → Deck → Power → Confirm, hub, drafts, summary with unlocks; caption laid out above the planks) and home entry | Done | Builds; untested in the client |
| Start-a-run API for the native picker (`DungeonRunRuntime.Classes`, `DecksFor`, `PowersFor`, `ClientCardId`, `NewRun`, `Mutate`); rows carry client ids (`RunClassOption.ClientHeroCardId`, `RunPowerOption.ClientCardId`) | Done | The pure part (`DungeonRunCatalog`) is in the 186 checks; the runtime wrapper builds |

**Decisions taken while wiring (2026-11-09):**

- A run fight reuses the ladder's staging on a Pending flagged `DungeonRun`, which Settle and RecoverInterrupted drop like `DebugScenario`.
- The end of a fight shows no reward screen. The outcome is recorded, the player returns to the hub, and the run screen reopens.
- Turn cap: 100 game turns, then the boss wins.
- A boss-AI exception is a Fault, and the floor is restaged.
- Secrets and deck-dependent cards are admitted by the admission table (E5). The boss's fair search samples the run player's hidden Secrets from the closed run pool (`DungeonRunAdmission.SecretPrior`, FairBotState Secret worlds, the `secret-worlds` fix), so Secret cards from the run's own pool are admitted for both sides. The pool reads every deck source a version-2 run offers (`DungeonRunAdmission.DeckSourceCards`: the class's starter decks, the neutral and class bundles, reachable active-treasure shells and tokens); it is null while the pool holds no Secret and for a migrated version-1 run, whose deck comes from no listed source, so the boss keeps the open class pool there. Settlement (`secret-worlds`) runs only with a closed pool, so ladder and world NPCs keep the Secret-blind search. The bot's own starting deck list stays visible in its fair view (only the opponent's is masked), so conditions on it (Elise the Navigator, Reno) resolve as in the real game. Run matches keep `ExcludeGeneratedSecrets` on, because those worlds are complete only while the player's Secrets come from the pool; cards that generate a Secret are therefore rejected (family `secret-generated`, lifted in phase 3). The detector reads card text with tags removed and `_`, `&` and line breaks as spaces (`DungeonRunValidator.MatchText`), so a phrase split over two lines still matches.
- Aya (`JAIL_504`) is refused, because its seat rule can put the player second.
- Boss portraits must match their deck's class and must not equal any player hero's portrait.
- Typed-seed runs do not count toward stats or unlocks.

**Known limits:**

- The caption fix and the new shells (names, art, client cost outside a match) are not checked in the client yet; the layout log line confirms the caption on first launch.

- Test-hook builds refuse administrator accounts (exit 40), so the run cannot be driven through test control on this machine.
- Shadows of Yesterday (`TIME_610`, a Death Knight Rewind spell) works as written. An early scan reported a fault when its Shades died ("Missing history board-entry annotation"), but the scan kept a `Player1` reference across a Rewind, which rebuilds the controllers; re-reading it gives 0 faults in 60 seeds, and random full games with Rewind are clean. Frost Strike was taken out of Rimeguard while this was open; the deck is fine either way.
- Band-2 bosses (generated decks) won 1 of 12 games against a simple greedy player. They are likely too weak, and need a tuning pass against the F7 targets.
## Open Questions

- **Q1 — Display shells (content pass, blocking for bosses and treasures).** The client must know every entity it shows. For each Deckdelve boss passive and treasure, which existing client entity is its shell? Candidates are generic enchantments, an existing hero-power card, or unused passive entities already in the facts file. This needs a per-entity check that the client renders it sensibly. It also decides which Deckdelve content survives the port.
- **Q2 — Hero health display.** Partly answered. `Player2HeroHealth` emits a HEALTH tag change in the engine's power history (§ Engine Changes, verified). It is still unconfirmed whether the client shows a 40/50-health hero correctly; that needs a run in the client (AC8).
- **Q3 — Launch classes.** The proposal is Warrior, Warlock and Mage as analogs of Deckdelve's War, Decay and Knowledge. Each class needs 8 curated bundles and a starter deck from the active-meta pool. Confirm which classes and who curates.
- **Q4 — Boss Coin.** R9 gives the boss the standard second-player Coin. Deckdelve has no Coin and a boss 4-card hand. Keep the standard engine behaviour, or strip The Coin in the pre-start phase?
- **Q5 — AI value for custom boss powers.** `CardFrequencies.FaceDamage` and the deck-plan evaluator assume known powers. Is extending those tables enough, or do boss decks need hand-written plans like `DeckPlans.DragonWarrior`?
- **Q6 — Build variants.** Pool policy, deck validation and `DebugScenario` are `ENGINE_SOURCE`-gated. Confirm the mode ships only in `ENGINE_SOURCE` builds, or define its behaviour without engine source.
- **Q7 — Telegraphed boss intent** (Deckdelve's intent banner). It is out of scope for v1, because the NPC pilot has no dry-run forecast API. Revisit after the MVP.
