## Family 18 report: Dalaran Heist bosses E and the Heist finale

Branch `wip/f18` in worktree `/home/user/wt/f18`, two commits: `2403e1b` (feat) and `bbfe616` (fix). Everything is committed and nothing is running. 15 of the 38 inventory ids are done with tests (the 12 regular encounters, the 4 finale bosses, DALA_504 and DALA_830). Commander Bolan (75h/75p), Phase Spider (61t) and Cheerful Spirit (DALA_501) are rejected with reasons. All lane checks passed: suites twice identical, generators byte-identical, three bosspilot modes twice identical with 0 faults. Nothing was checked in the real client, and the plugin was not built.

### FLAGS for the integrator
1. **Tag:** only my reserved 32653 (`EncounterRules.SelfDamageImmune`, one line in the tag table of `EncounterRules.cs`). No extra tag.
2. **NativeMatch.cs edited (plugin, cannot be built here).** Norroa's 25-second timer needs it. The three places that hard-coded the 15-second timer (`TIMEOUT == 15`) now ask `EncounterRules.ShortTurnSeconds(Game.CurrentPlayer)`. That function returns the TIMEOUT tag when it is between 1 and 44, else 0. Nozdormu the Eternal's 15 behaves exactly as before. This needs the Windows plugin build and a check in the client; the engine side is tested (c-enc-18 N1-N5, M12).
3. **Engine core edit, `Character.cs`:** one line in `TakeDamage` (Kazamon's Steelskin: damage from his own sources to his own hero is prevented; Fatigue still hurts).
4. **`schedule-v1.json`:**
   - DALA_830 appended at the end of `rules` as a twist with effect `run` (no match script).
   - The acts-v3h finale block gains `"twist": {"pool": ["DALA_830"], "chance": 100}`. The block stays **disabled**; this key is inert until the close-out enables it.
   - No default flipped, no finale enabled, no act or pool set changed, no save version bump, no re-pin.
5. **`DungeonRunData.cs` / `DungeonRunValidator.cs`:** the parser reads the finale block's `twist`, and the validator checks it like an act's twist pool. Without this, DALA_830 cannot be the finale act's rule.
6. **Close-out must re-pin.** Enabling the acts-v3h finale changes its shape digest:
   - acts-v3h becomes `09B43E71F9440039` (shipped `FF4FEC05953AA0DE`). On the fixture this pin is the only validator error, and c-enc-18 R2 asserts exactly that.
   - acts-v3t is unchanged while its own finale stays disabled.
   - To enable: set `enabled: true` and `fixedBosses` from `pending`. `verify/tools/gen/p8f18/make_fixture.py` makes exactly that edit.
7. **Engine fix inside my own rule:** an Echo copy that Marei Loom's swap puts into the deck is removed from `Game.GhostlyCards`. Before the fix, the boss's fair view threw `KeyNotFoundException` in `MainCleanUp` (3 fallbacks in the first enc18a run). Regression check: c-enc-18 F7.
8. **Likely merge conflicts** with wip/f14 and wip/f15:
   - c-enc-model E3: my wording is f15's apart from the comment.
   - The passive lists in c-content and c-enc-model J13.
   - The ledger pins in c-catalog and c-deckops.
   - `curation.json` landedFamilies, `bp.sh`, `suites.sh`, and the tag table.

### Per id
**Done** (script, row, admission, test):
- **Regular encounters:**
  - DALA_BOSS_61 Sael'orn (Web Grab: Dirty Rat's pull)
  - 62 Haro (Darken + DALA_BOSS_62e)
  - 63 Dagg (Bully)
  - 64 Biggs (Invest! + 64e)
  - 65 Valdera (Impervious: hero damage cap 3)
  - 66 Marei Loom (Next...: swap with a belief reset)
  - 68 Lilayell (Mini Portal)
  - 70 Smiggs (Bombardment: one power use every other boss turn)
  - 71 Anarii (Summon Protectors, EX1_573t)
  - 72 Kazamon (Steelskin)
  - 73 Hesutu (Earthmother's Rage + 73e)
  - 74 Kara Stamper (Soul Weave)
- **Finale (tier `finale`, levels 9-12):**
  - 43 Norroa (Kaironomaly: run player's TIMEOUT set to 25 each turn)
  - 67 Vargoth (Arcane Runes, from the wiki's 5-card pool: AT_004, EX1_277, CS2_025, CS2_023, CFM_623)
  - 69 Kalec (Dragonwrath: the hero's own Spell Damage, cumulative)
  - 60 Khadgar (Summon Elemental)
- **DALA_504** Kirin Tor Elemental: recasts its controller's spells; not Secrets or Quests.
- **DALA_830** The Finale: admitted as a run-scope rule (family `global-rule-run`). It is rolled at NewRun with chance 100 for the finale act; it is never in a match plan.
- **Tiers:** f15's deck-size rule. Quantity tables of 30 cards are level 6 (tier 3). Dagg (20/25 cards) is level 4 (tier 2), Hesutu (15/20) level 3 (tier 2). Marei is printed with 10 Health, so level 1 (tier 1). None of my regular bosses is a chapter final.
- **Decks:**
  - 12 authentic.
  - Dagg, Marei and Hesutu extended from the wiki's pools.
  - Biggs substituted: Ogre Brute GVG_065 is unscripted.
  - Hesutu also loses Whirlwind Tempest (DAL_742, unscripted).
  - "Xavius" in Marei's Heroic pool is not a client card; it is left out.
  - The finale decks keep the wiki's counts (35, 35, 40, 40).
  - Khadgar's Archmage's Apprentice (KARA_00_08) is family 09's script.

**Rejected:**
- **DALA_BOSS_75h / 75p Commander Bolan:** data gap. His five quest enchantments (75e1-e5) have no text, requirement or reward in the client data, and the wiki gives neither them nor his summon list. Lift: "none: a data gap no phase owns", following DALA_740's convention.
- **DALA_BOSS_61t Phase Spider:** unused in the adventure (the wiki: "an unused card intended for use during Sael'orn encounter"; in neither deck).
- **DALA_501 Cheerful Spirit:** an unused Rise of Shadows card (the wiki). No Heist content names it.
- **Norroa's timer** is implemented, not rejected (flag 2).

### Files changed
- **Own files:**
  - `engine/SabberStoneCore/src/CardSets/DungeonRun/DungeonRunEncounters.F18.cs`
  - `engine/SabberStoneCore/src/Actions/EncounterRules.F18.cs` (new)
  - `plugin/DungeonRun/DungeonRunAdmission.F18.cs`
  - `data/dungeon-run/reference/encounters/F18.json`
  - `verify/tools/gen/p8f18/make_f18.py`, `verify/tools/gen/p8f18/make_fixture.py`
  - `verify/fixtures/p8f18/fixtures/schedule-finale18.json`
  - `verify/suites/p8f18/c-enc-18/` (Enc18.csproj, Program.cs)
- **Shared files:** `EncounterRules.cs` (tag), `Character.cs` (hook), `NativeMatch.cs` (timer), `DungeonRunData.cs` / `DungeonRunValidator.cs` (finale twist), `schedule-v1.json` (rule row, inert twist key), `curation.json` (landed-family entry E18 appended), `bp.sh` and `suites.sh` (registration).
- **Regenerated:** `encounters-v1.json`, `restoration-ledger.csv/.md`, and the tool-owned lists in `curation.json`.

### Pins moved
| Suite | Old | New | Cause |
|---|---|---|---|
| c-content | passive list | + DALA_BOSS_65p, 68p, 72p, 43p | four passive powers (still 904) |
| c-enc-model A4 | family counts | + `18:16` | 16 new rows |
| c-enc-model A10 | 1 rejected encounter | 2 (+ Bolan) | Bolan rejected |
| c-enc-model E3 | every run meets Whirt | every run meets a Heist row, Whirt in some | Heist rows in the combat tiers (as f15) |
| c-enc-model J13 | passive list | + the four passives | (still 141) |
| c-catalog ledger golden | 4704 / 2813 / 295 / 538 | 4734 / 2813 / 343 / 520 | +30 rows (16 run:boss, 14 deck cards); 18 rows missing → partial |
| c-deckops T5 | 4704 | 4734 | same ledger rows |
| c-admission | 3167 | 3256 | new admission rows; generated lists 426 → 447; one exemption statement for my families boss-hidden-shuffle, boss-rejected-data, unused-card |
| c-rules-05 T2/T9/T11 | 4 Heist twists | DALA_830 excluded (still 170) | a run-layer twist, not family 05's |

### Generators
Twice byte-identical:
- encounters "85 rows, 89 decks (55 authentic, 34 changed), 198 adaptations, 2 rejected"
- catalog unchanged: "506 entries ... 6508 admitted and 1 excluded"
- ledger: 4734 rows, implemented 2813, partial 343, missing 520, duplicate/support 1058

### Tests and lane verification
- **Engine:** 0 warnings / 0 errors (md5 ba963530f53ea7234b57548e3e17b076).
- **c-enc-18:** 106 checks (sections A-R), registered at the end of BUILD_LIST and RUN_LIST. Every power and card is tested in real engine games through BuildPlan → DungeonRunSetup. Fair-view checks cover Sael'orn and Marei (the hidden card is never learned). R covers the fixture finale: the four bosses on floors 16-19 in order and never earlier, DALA_830 rolled in 40 of 40 runs and kept out of the plan, and the validator refusing an anomaly in the finale twist.
- **Suites, run twice, logs identical:**

| Suite | Result |
|---|---|
| c-content | 904 |
| c-admission | 3256 |
| c-runlayer | 244 |
| c-acts | 181 |
| c-deckops | 174 |
| c-catalog | 127 |
| c-shrine-core | 131 |
| c-enc-model | 141 |
| c-enc-engine | 122 |
| c-enc-18 | 106 |
| c-rules-05 (extra) | 170 |

  Against `verify/baseline/runs`: 0 differing lines except c-admission (4: the total and generated-list lines), c-enc-model (14) and c-rules-05 (12), which are the pin and check-name changes above.
- **Bosspilot** (`bp.sh <mode> 1`, `bp.sh <mode> 2`, `bp.sh same <mode>`; each run twice, identical, 0 faults / 0 fallbacks / 0 failed checks / 0 swallowed, stderr empty, power-unused 0):

| Mode | Games | Boss wins | Note |
|---|---|---|---|
| enc18a (61-66) | 12 | 10 | |
| enc18b (68, 70-74) | 12 | 8 | |
| enc18-finale (fixture schedule, floors 16-19) | 8 | 6 | Archmage's Apprentice played x2; at most 5 boss Secrets |

  The finale rows have no floor on the shipped schedule, so they run only through enc18-finale.
- Not run, per the lane rules: hall, full runs.sh, selfplay, randplay, released modes. Randplay's regkeys line will move (new registered ids).

### Paragraph for docs/design/dungeon-run.md
"Family 18 (Dalaran Heist bosses E and the Heist finale): twelve Heist encounters (Sael'orn, Haro Setting-Sun, Dagg Cruelmight, Banker Biggs, Valdera Highborne, Marei Loom, Lilayell Suntear, Sky Captain Smiggs, Anarii Duskgrove, Kazamon Steelskin, Hesutu Stonewind, Kara Stamper), tiered by the Heist deck-size rule of families 14/15, and the four fixed bosses of the Heist finale (Magistrix Norroa, Archmage Vargoth, Archmage Kalec, Archmage Khadgar; tier finale, levels 9-12, decks of 35/35/40/40). Twist - The Finale (DALA_830) is the rule of acts-v3h's finale block: a twist with effect run, rolled with chance 100 for the finale act, never in a match plan. The block ships disabled with its bosses pending; the fixture verify/fixtures/p8f18/fixtures/schedule-finale18.json enables it. New engine rules (Actions/EncounterRules.F18.cs): a player's short turn timer (ShortTurnSeconds: Nozdormu the Eternal's 15, Kaironomaly's 25, which NativeMatch now enforces), Steelskin's self-damage prevention (tag SelfDamageImmune, read in Character.TakeDamage; Fatigue still hurts), and Marei Loom's left-most hand/deck swap with belief resets for both players (an Echo copy swapped into the deck stops being ghostly). Kirin Tor Elemental recasts its controller's spells (RecastRules). Commander Bolan is rejected (his quests have no source), as are the unused Phase Spider and Cheerful Spirit."

### For the integrator after the merge
- Regenerate, then recompute: c-catalog golden, c-deckops T5, c-admission total, c-enc-model A4/E3/J13, c-content passive list.
- Close-out: enable the acts-v3h finale, re-pin ReleasedShapes[3], re-record the goldens.
- Windows: build the plugin to cover the NativeMatch edit, and check the 25-second timer in the client.
- Bosspilot lines will move when other families add rows, because seeds depend on a boss's row index.


---

# Family 18 review: confirmed findings (adversarially verified)

## F18.1 [medium] engine/SabberStoneCore/src/Actions/EncounterRules.F18.cs:35
The new rule treats any TIMEOUT from 1 to 44 as a short turn, and NativeMatch now enforces it. Classic Nozdormu (EX1_560, playable through LocalCardCatalog) adds a -55 TIMEOUT aura to both controllers, so TIMEOUT reads 75 - 55 = 20 and becomes a forced 20-second turn in every game. The report's claim that 'Nozdormu the Eternal's 15 behaves exactly as before' misses this card.

Scenario: Any NativeMatch game, Dungeon Run or not: someone plays EX1_560 Nozdormu. Its aura (AuraType.CONTROLLERS, TIMEOUT SET -55) passes through ControllerAuraEffects.Set, so Controller[TIMEOUT] = 75 + (-55) = 20. Before this change, NativeMatch only reacted to TIMEOUT == 15, so this did nothing. Now ShortTurnSeconds returns 20, and NativeMatch.cs:1305 ShortTurn > 0 arms a 20 s deadline for the human (in TickTurnTimer and RefreshTurnDeadline) even with the Turn Timer option off, and for the NPC (instead of 75 s). Both players' turns are cut to 20 s, an unrequested change to an unrelated card in every game mode.

Verifier: Confirmed. EX1_560 is in LocalCardCatalog. Its aura is CONTROLLERS TIMEOUT SET -55 (Expert1CardsGen.cs:5006), and Controller's indexer (Controller.cs:173-174) adds ControllerAuraEffects to the base 75 set at Game.cs:1028. So TIMEOUT reads 20. Before this change NativeMatch reacted only to == 15, so classic Nozdormu had no clock effect. Now ShortTurnSeconds (EncounterRules.F18.cs:34-35) returns 20, and NativeMatch.cs (HumanTurnSeconds, the deadline at ~1409 and the shortened check at ~1434) forces a 20 s turn on both players in every mode, even with the Turn Timer option off. That is an unintended global change from a shared-file edit that the lane rules say must stay minimal and local. The value is also not the card's 15, and it contradicts the report's claim that behaviour is unchanged.

Fix: Have ShortTurnSeconds read only the base TIMEOUT tag, without controller auras, so only effects that write the tag count (Nozdormu the Eternal's 15 and Kaironomaly's 25), as before: `int t = c[GameTag.TIMEOUT] - c.ControllerAuraEffects[GameTag.TIMEOUT]; return t > 0 && t < ShortTurnBelow ? t : 0;`

## F18.2 [medium] engine/SabberStoneCore/src/Actions/EncounterRules.F18.cs:47
Kaironomaly's 25-second timer is lost while classic Nozdormu is on the board. ShortenTurn writes 25 to the base TIMEOUT tag, but ShortTurnSeconds reads the value with auras applied: 25 + (-55) = -30, which counts as no short turn. The run player then gets the ordinary or unlimited clock.

Scenario: Magistrix Norroa fight: either side plays EX1_560 Nozdormu. At the run player's next turn start, ShortenTurn sees ShortTurnSeconds(player) == 20 and returns, so the base stays 25. The effective TIMEOUT is 25 - 55 = -30, ShortTurnSeconds returns 0, and NativeMatch gives the human the normal 75 s turn, or no deadline when the Turn Timer option is off. On later turns ShortenTurn sees 0 and writes base 25 again, and the effective value is still -30. So 'Your opponent has 25 seconds to take their turn' is never enforced while Nozdormu lives, which contradicts the comment that a shorter timer is kept.

Verifier: Confirmed; it has the same root cause as [1]. ShortenTurn writes the base tag (opponent.TimeOut = 25, which goes through Controller's setter into _data). ShortTurnSeconds reads the aura-applied value, so with EX1_560 in play it reads 25 + (-55) = -30 and returns 0. On the turn the aura first applies, ShortenTurn sees 20 and returns early, leaving the base at 75 or 25. On later turns it rewrites 25, and the effective value is still -30. NativeMatch then gives the human the normal 75 s turn, or no deadline when the Turn Timer option is off. So Kaironomaly's 25 s is never enforced while classic Nozdormu is on the board, which contradicts the 'unless a shorter one holds already' contract at EncounterRules.F18.cs:39-47.

Fix: Use the same fix as [1]: ShortTurnSeconds reads the base tag only (`c[GameTag.TIMEOUT] - c.ControllerAuraEffects[GameTag.TIMEOUT]`). ShortenTurn and NativeMatch then both see 25, and classic Nozdormu's aura no longer cancels it.


## Family 18 review fix: the short turn timer now ignores TIMEOUT auras (commit 796f5dd on wip/f18)

I agree with both review findings, F18.1 and F18.2; they had one cause and are fixed on `wip/f18` (worktree `/home/user/wt/f18`). The new checks pass, c-enc-18 ran twice with identical logs, and enc18a, enc18b and enc18-finale ran twice each through the lock with identical results.

**Fix** (`engine/SabberStoneCore/src/Actions/EncounterRules.F18.cs`, `ShortTurnSeconds`): it now reads only the base TIMEOUT tag, as the verifier suggested: `c[GameTag.TIMEOUT] - c.ControllerAuraEffects[GameTag.TIMEOUT]`. A TIMEOUT aura is no longer read, so:
- **Classic Nozdormu (EX1_560, aura SET -55):** never makes a short turn on its own. NativeMatch again ignores it, as it did before family 18.
- **Kaironomaly with classic Nozdormu in play:** Norroa's 25 seconds hold for the run player's turn.
- **Nozdormu the Eternal:** still 15, since the engine writes that into the base tag.

NativeMatch needed no further change.

**New checks in c-enc-18** (section N):
- **N6:** with classic Nozdormu alone the TIMEOUT tag reads 20, and there is no short turn for either player.
- **N7:** Norroa with classic Nozdormu in play: the run player's next turn runs under 25 seconds, and the boss's turn is not short.
- **N8:** Nozdormu the Eternal's 15 stays 15, with classic Nozdormu's aura present too.

All three failed against the pre-fix engine (N6 gave 20, N7 gave 0, N8 gave 0), so they can catch the bug.

**Results after the fix:**
- **Engine:** 0 warnings / 0 errors, md5 b4b00a329f556e5ea8ee4f1074341ccd.
- **Generators:** all six generated files unchanged (git status empty).
- **c-enc-18:** 109 passed, 0 failed (was 106), both runs.

| Mode | Games | Boss wins | Watched / note |
|---|---|---|---|
| enc18a | 12 | 10 | |
| enc18b | 12 | 8 | |
| enc18-finale | 8 | 6 | Archmage's Apprentice (KARA_00_08) played x2; at most 5 boss Secrets; result file identical to the run before the fix |

Every mode: run 1 = run 2, 0 faults, 0 fallbacks, 0 failed checks, 0 swallowed leaf exceptions, power-unused 0, stderr empty. The game counts and boss wins are the same as before the fix.

**Commits on wip/f18:** `2403e1b` (feat), `bbfe616` (fix), `796f5dd` (this fix). Everything is committed and nothing is running.

**Corrections to my earlier report:**
- c-enc-18 now has 109 checks, not 106.
- The engine md5 is now b4b00a329f556e5ea8ee4f1074341ccd.
- The claim that the change leaves the old timer behaviour alone now holds for classic Nozdormu too.
- The NativeMatch edit still needs the Windows plugin build and a check in the client.
