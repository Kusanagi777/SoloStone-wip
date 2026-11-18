## Family 14 report: Dalaran Heist bosses A (DALA_BOSS_01-15), branch wip/f14

All 37 ids are done and none is rejected. Everything is in one commit, `9b7b8a5` (on top of a95b97e), and the tree is clean. Nothing was pushed or downloaded except the 15 boss pages read from hearthstone.wiki.gg; those are kept in the gitignored `verify/.out/tmp/wiki`.

### Flags for the integrator
1. **No new tag.** Belnaara's form state uses the model's existing tags (FormIndex, FormChanges) and the power card's own public counter (TAG_SCRIPT_DATA_NUM_1). The tag-table comment in EncounterRules.cs is unchanged.
2. **Nothing forbidden was touched.** No schedule default, finale, acts, pool sets, save version, ReleasedShapes or golden sequence changed. Family 12's EchoCopyFloor is not on this branch; Reductomara has its own floor (an Echo or Repeatable spell at (1) is skipped).
3. **Shared edits** (minimal, append-only):
   - `tools/restoration-ledger/curation.json`: landedFamilies entry E14 appended after E10. The two tool-owned lists were rewritten by gen.sh.
   - `verify/lib/suites.sh`: `p8f14/c-enc-14` appended at the end of BUILD_LIST and of RUN_LIST. The "(67)/(54)" count comments are not updated.
   - `verify/bin/bp.sh`: a usage line, `CARDS14` next to the family 10 variables, and four case lines after enc10-hagatha.
   - Suite pins (shared suites), listed under "Pins moved" below.
4. **Two deviations from the inventory/design notes:**
   - "Scourgelord" Dazzik (06dk) is not a form. The wiki lists it as a Heroic-pool deck card: a hero card using Scourgelord Garrosh's rule (Bladestorm, Shadowmourne). The Normal pool fills all 30 cards first, so the row's start block shuffles one copy in (`bossDeckAdd`), as family 10 does for Grubb's Chuck.
   - Belnaara is a one-form "script" row, swapped with a new helper `EncounterRules.SwapPowerForm` in my own file. The existing `SwapForm` would replace the hero entity when returning to form 0 and reset her Health.
5. **No wiki level tables exist for Heist bosses.** My rule: a quantity list's note names level 6 (Noz's page names none; his 30-card list is treated as level 6). For a pool table, the "Total cards" sizes map to levels (10→1, 15→2, 20→3, 25→4/5, 30→6) and the row takes the lowest level holding the largest size. A hero printed at 10 Health is level 1. Tiers come out as 2 / 9 / 4 rows for tiers 1 / 2 / 3. The rule is recorded per row and in F14.json.
6. **Reductomara and the boss's view.** It records DeckRewritten facts for both decks. The boss's fair view samples its own deck at printed costs, because the ledger keeps card ids only. So the AI overestimates its future spell costs, which can never cause a fallback. This is stated in the admission reason.
7. **Other modes' bosspilot lines may move.** Fifteen more DALA rows in the pools change the act walks: c-enc-model G1's rolled anomalies went from DALA_860,DALA_856 to DALA_858,ULDA_710; Whirt now appears in 196 of 1000 runs; Hagatha is the act II major in 557 of 1000 (was 506). Other families' bosspilot modes may differ from their records for the same reason; I did not run them.
8. **randplay** regkeys and fingerprint will move (new registrations). Not run.
9. **Dark Iron Bouncer** (BRMA01_3, not my id) has no script, so it is left out of Tierra's deck. Avian Watcher is not admitted for a boss, so Belnaara's deck is "substituted".
10. **Belnaara is strong:** 8 boss wins in 8 games at tier 3. Flight games run long (up to 75 turns), but none hit the turn cap.

### Per id (all done)
- **Heroes** DALA_BOSS_01h–15h: rows only; they are portraits and need no script.
- **01p Chomp:** 1 damage to a minion, with the power's own Lifesteal.
- **02p The Feast:** passive; restores 3 to the boss's hero after it casts a spell.
- **03p Prestidigitation:** adds one of the four feats to the boss's hand; offered only while the hand has room.
  - **03t Reductomara:** (1) less on every spell in both hands and decks, via enchantment 03e.
  - **03t2 Bunnifitronus:** a random minion becomes a critter (Frog, Sheep or Squirrel); a run shrine goes dormant instead.
  - **03t3 Presto Legendaro:** each player gets a scripted collectible Legendary minion through their own generation filter (golden is not modelled).
  - **03t4 Yoggers Poggers:** three Scrolls of Wonder into each deck.
- **04p Open Wormhole:** reuses Duel!'s rule.
- **05p Mana Echoes:** casts a new copy of a spell either player played this game, random targets, through the generation filter, never a Secret or Quest.
- **06p "Armor Up":** gain 2 Armor. **06dk** is the hero card described above.
- **07p / 07p2 Take Flight! / Flying!:** counts 15 Health damage, then she flies (Immune via 07e) for two of her own turns, then lands.
- **08p Apple Toss:** hits the lowest-Health enemy and repeats while the target dies (at most 10 hits).
- **09p Repeat History:** summons a copy of a friendly minion that died this turn; never a shrine's corpse.
- **10p Street Smarts:** draws when the boss plays The Coin or Counterfeit Coin.
- **11p Backstabber:** every Stealthed minion summoned, on either side, gets +1 Attack (enchantment 11e).
- **12p Forbidden Love:** destroys a random minion on each board.
- **13p Slice and Dice:** 1–3 damage each to three different random minions.
- **14p Order Up! / 14t "Kind" Waitress:** an enemy attacking her has a 50% chance to hit another character on her side (EncounterRules.WrongTarget50).
- **15p Blessing of Elune:** doubles healing for both players (the existing ALL_HEALING_DOUBLE aura); converted healing, including through TRLA_147 Bwonsamdi's Covenant, deals the doubled damage.

### Files changed
- **Own files:**
  - `engine/SabberStoneCore/src/CardSets/DungeonRun/DungeonRunEncounters.F14.cs`
  - `engine/SabberStoneCore/src/Actions/EncounterRules.F14.cs` (new)
  - `plugin/DungeonRun/DungeonRunAdmission.F14.cs`: six rows (heist-board-power 7, heist-own-hand-power 1, heist-hidden-deck-power 1, heist-recast-power 1, heist-passive 6, heist-boss-card 6)
  - `data/dungeon-run/reference/encounters/F14.json`
  - `verify/suites/p8f14/c-enc-14/` (Enc14.csproj, Program.cs)
  - `verify/fixtures/p8f14/fixtures/encounters-f14.json`
  - `verify/tools/gen/p8f14/make_f14.py` (writes F14.json from the saved pages) and `make_fixture.py`
- **Regenerated:** encounters-v1.json, curation.json lists, restoration-ledger.csv/.md. catalog-v1.json and dungeon-run-catalog.md are byte-identical to the base.
- **Shared:** see flag 3.

### Pins moved (suite: old → new, cause)
- **c-admission:** 3167 → 3247 passed; generated lists 426 → 448 for both sides. Cause: 22 new rows, plus the family 14 exemption statement.
- **c-catalog ledger golden:** 4704 / 2813 / 295 / 538 → 4740 / 2813 / 352 / 517. Cause: 15 run:boss rows and 21 deck cards added; 21 ids moved missing → partial. 06dk stays "missing", like the boss heroes.
- **c-deckops T5:** 4704 → 4740.
- **c-content:** passive registry list gains 6 powers; still 904.
- **c-enc-model** (still 141 passed):
  - A4 family counts gain "14:15".
  - E3 rewritten from "every run meets Whirt" to "every run meets a Heist row".
  - J13 passive list gains 6.
  - 12 differing lines vs baseline: A4 text, E2 and E3 details, G1 detail (flag 7), and F2 save lengths 14306 → 13954. The F2 lengths are a Linux port effect, not mine: the integrator's base-tree log shows the same 13954.

### Generators (gen.sh twice, all six files byte-identical)
- Encounters: "84 rows, 88 decks (45 authentic, 43 changed), 206 adaptations, 1 rejected".
- Catalog: "506 entries (452 offerable pool, 2 withdrawn, 52 fixed), 6508 admitted and 1 excluded" (unchanged).
- Ledger: rows 4740, implemented 2813, partial 352, missing 517, duplicate/support 1058, adapted 0.

### Tests added and registered
- **c-enc-14:** 112 checks, all passing.
- **Bosspilot modes** (each run twice, identical, stderr empty, 0 faults / fallbacks / failed checks / swallowed):

| Mode | Games | Boss wins | power-unused | form-unreached | Boss cards played from hand |
|---|---|---|---|---|---|
| enc14 (FAMILY=14) | 30 | 23 | 0 | 0 | 03t x2, 03t2 x1, 03t3 x3, 03t4 x5, 06dk x0 |
| enc14-seeds (base 19000) | 30 | 20 | 0 | 0 | 03t x2, 03t2 x3, 03t3 x3, 03t4 x2 |
| enc14-flight (Belnaara x8) | 8 | 8 | 0 | 0 | – |
| enc14-cards (fixture rows) | 12 | 7 | 0 | 0 | 03t x14, 03t2 x21, 03t3 x18, 03t4 x9, 06dk x12 (all five played) |

### Lane verification
- **Engine:** 0 warnings / 0 errors (Linux md5 85bcffa3429e47c7d7902b8dc88f5d86).
- **Generators:** run twice, byte-identical.
- **Suites, each run twice with identical logs:** c-runlayer 244, c-catalog 127, c-content 904, c-admission 3247, c-acts 181, c-deckops 174, c-shrine-core 131, c-enc-model 141, c-enc-engine 122, c-enc-14 112. All have 0 differing lines vs baseline except c-admission (4) and c-enc-model (12), explained above.
- **Extra check, run once:** c-enc-07 161, c-enc-08 113, c-enc-09 166, c-enc-10 116, c-rules-05 170, c-rules-06 224, c-enc-secrets 60, c-w1fix 23, c-w3fix 4. All at their baseline counts.
- **Not run:** hall, full runs.sh, selfplay, randplay, released modes, other families' bosspilot modes.

### For the README / baseline
- c-enc-14: RESULT 112 passed, 0 failed.
- Bosspilot results as in the table above.
- `bp.sh enc14-cards` also prints "all 5 boss cards played by the boss".

### Paragraph for docs/design/dungeon-run.md
"Family 14 (Dalaran Heist bosses A): fifteen Heist encounters (DALA_BOSS_01 to 15). The boss pages have no level table. The level comes from a quantity list's stated level (6) or from the pool table's deck sizes (10/15/20/25/30 at levels 1/2/3/4-5/6, the lowest level holding the largest size); a hero printed with 10 Health is level 1. Tiers 1/2/3 hold two, nine and four rows, and the fight's Health is the act curve's. Decks are the Normal pool with second copies, then the Heroic pool, brought to 30; Oshi's and Albin's quantity lists are kept unchanged. New engine rule: a power-only script form (EncounterRules.SwapPowerForm) keeps the hero entity, Health, Armor and enchantments while the power changes. Belnaara's Take Flight! counts 15 Health damage on the power's public counter, then becomes Flying! with her hero Immune for two of her turns, then lands. "Scourgelord" Dazzik is a Heroic deck card (Scourgelord Garrosh's rule), shuffled in by the row's start block; it is not a form. Akazamzarak's four feats act on both players: Reductomara's (1) less never takes an Echo or Repeatable spell below (1) and records each owner's deck as rewritten; Presto Legendaro uses each side's generation filter. Open Wormhole is Duel!'s rule. Mana Echoes recasts a spell from the public play history through the boss's generation filter, never a Secret or Quest. The "Kind" Waitress uses the wrong-target rule (WrongTarget50) as a defender. Blessing of Elune is ALL_HEALING_DOUBLE on both players, and converted healing (Auchenai, Bwonsamdi's Covenant) deals the doubled damage."

### To redo after the merge
- Rerun gen.sh, then `python3 -I verify/tools/gen/p8f14/make_fixture.py`, because the fixture is built from encounters-v1.json.
- Recompute the pins above against the other lanes' rows: c-enc-model A4/E3/J13, the ledger golden, T5, c-content's passive list, the c-admission total.
- Add the enc14 modes to final.sh and MANIFEST.md if wanted; I did not edit either.
- Re-record the bosspilot baselines of other families if they move (flag 7).


---

# Review fixes

# Family 14 review: confirmed findings (adversarially verified)

## F14.1 [medium] engine/SabberStoneCore/src/CardSets/DungeonRun/DungeonRunEncounters.F14.cs:69
Mana Echoes (DALA_BOSS_05p) never picks a Secret, even one the boss does not have in play. The card text "Cast a random spell that was cast this game" has no such exclusion. The stated reason ("they could not enter play twice") holds only for a Secret already in the boss's Secret zone, and the engine can auto-cast Secrets: ULDA_503 Monocle in DungeonRunTombsActives.cs skips only a Secret already in play.

Scenario: Bonepaw's own deck holds four Secrets, two copies each: Duplicate FP1_018, Effigy AT_002, Ice Barrier EX1_289 and Mirror Entity EX1_294. Bonepaw plays Ice Barrier, it triggers and leaves play, and no other spell has been cast. ManaEchoes() returns an empty list, so usable (line 245) hides the power for the rest of that state. In the original, Mana Echoes would cast Ice Barrier again. The same applies to every Secret the run player casts (Counterspell, Explosive Trap and so on): none can ever be echoed, so the random pool and its odds differ from the card.

Verifier: Confirmed. In DungeonRunEncounters.F14.cs:69, ManaEchoes() drops every card with card.IsSecret before anything else is checked. The stated reason in the code comment and in the F14.json 'power' adaptation is 'they could not enter play twice', but that is only true for a Secret already in the caster's SecretZone. The engine does auto-cast Secrets elsewhere: ULDA_503 Monocle (DungeonRunTombsActives.cs:407) skips only a Secret that is already in play. Bonepaw's own pools (F14.json) hold Duplicate, Effigy, Ice Barrier and Mirror Entity in Normal, plus Counterspell and Explosive Runes in Heroic. So the most common spells he plays can never be echoed. If he has played only a Secret, usable (line 76) hides the power, and the random pool differs from 'Cast a random spell that was cast this game'. The adaptation's reason is wrong, so it does not count as a justified adaptation.

Fix: In ManaEchoes, remove !card.IsSecret from the filter and keep !card.IsQuest. Then, as Monocle does, also drop a Secret whose id is already in c.SecretZone (c.SecretZone.Any(p => p.Card.Id == card.Id)), and drop all Secrets while c.SecretZone is full. Correct the comment and the F14.json 'power' adaptation note to say this.

## F14.2 [medium] verify/bin/bp.sh:225
The enc14-cards 'every boss card played' check uses the regex [A-Z0-9_]+ x0, which has no lowercase letters. Watched ids that end in a lowercase letter, DALA_BOSS_03t (Reductomara) and DALA_BOSS_06dk ("Scourgelord" Dazzik), can never match, so the check cannot fail for those two cards. 03t2/03t3/03t4 are still caught, but their ids come out cut down to '2 x0' and so on.

Scenario: The harness's last line ends 'watched DALA_BOSS_03t x0 DALA_BOSS_03t2 x5 DALA_BOSS_03t3 x0 DALA_BOSS_03t4 x9 DALA_BOSS_06dk x0, most boss Secrets 0'. The sed|grep pipeline returns only '3 x0' (checked with the same pipeline). If 03t3 had been played as well, the output would be empty and bp.sh would print 'all 5 boss cards played by the boss' even though Reductomara and Scourgelord Dazzik were never played. The fix is a pattern that allows lowercase ([A-Za-z0-9_]+), like the f04 mode's 'ULD_[0-9a-z]*'.

Verifier: Confirmed by running the exact pipeline on the harness's output format (c-bosspilot prints 'id xN' with ids as-is). On '..., watched DALA_BOSS_03t x0 DALA_BOSS_03t2 x5 DALA_BOSS_03t3 x0 DALA_BOSS_03t4 x9 DALA_BOSS_06dk x0, most boss Secrets 0' it prints only '3 x0'. The pattern [A-Z0-9_]+ x0 cannot match 'DALA_BOSS_03t x0' or 'DALA_BOSS_06dk x0', because the lowercase letter sits between the id and ' x0'. So bp.sh:225-226 cannot report Reductomara or Scourgelord Dazzik as unplayed. With 03t3 played, the check prints 'all 5 boss cards played by the boss' even though those two were never played.

Fix: In verify/bin/bp.sh line 225, use grep -oE "[A-Za-z0-9_]+ x0( |$)" (or "DALA_BOSS_[0-9a-z]+ x0( |$)") so the lowercase suffixes are matched and the full ids are reported.

## F14.3 [low] verify/suites/p8f14/c-enc-14/Program.cs:313
D4's first part, 'in the boss's fair view the power is offered too', compares viewBoss.Options().Any(HeroPowerTask) with Offered(g2) only after Power(g2) already used Open Wormhole this turn (line 310). Both sides are therefore always false and the comparison is always true.

Scenario: Suppose Open Wormhole had a usable condition that held in the real game but not in the projected fair view, for example one that reads the opponent's hidden deck, which the projection empties. The real game would offer the power and the fair view would refuse it, which is exactly the mismatch D4 says it guards against. D4 still passes, because at the moment of the check the power is exhausted in both games. The check has to read the fair view before the power is used, or on a fresh turn.

Verifier: Confirmed. At line 310, Power(g2) uses Open Wormhole on the boss's turn (D3 relies on it having resolved), and FairView(g2) is taken after that. viewBoss.Options().Any(HeroPowerTask) and Offered(g2) are therefore both false, because the hero power is spent for the turn, and the equality always holds. The first half of D4 ('in the boss's fair view the power is offered too') cannot fail whatever the fair view does, so the check verifies nothing. Today Open Wormhole has no usable predicate, so the behaviour itself is right, but the claimed check is empty.

Fix: In Program.cs D, take the fair view and compare before Power(g2): after ToBoss(g2), set bool real = Offered(g2) and var (v0, vb0, _) = FairView(g2), then check vb0.Options().Any(o => o is HeroPowerTask) == real && real. Alternatively, do the comparison on a fresh boss turn.


## Family 14 review fixes: commit `753f74a` on wip/f14 (on top of `9b7b8a5`)

I fixed all three findings, with one deliberate limit on F14.1: a Secret the run player played is still never echoed by the boss. The reason is below. Everything is committed and the tree is clean.

### F14.1 Mana Echoes and Secrets (agreed, one part kept on purpose)
- **What changed:** `ManaEchoes` in `DungeonRunEncounters.F14.cs` no longer drops every Secret. A Secret the boss itself played is now an echo while its Secret zone has room and holds no copy of it, as the Monocle (ULDA_503) does. Quests stay excluded.
- **What I kept:** a run player's Secret stays excluded.
  - Casting one for the boss makes it a boss-generated Secret. The framework allows those only through a row with `bossGeneratedSecrets` and a validator-closed pool of the boss's class (Whirt, Streetsmuggler).
  - The engine's generation filter already blocks every boss-generated Secret: `Player2ExcludeGeneratedSecrets` is on, or, when a row turns Secrets on, the boss's id list holds every Secret.
  - Lifting it would let a Bonepaw boss hold any class's Secrets, including ones not admitted for a boss.
  - The boss's own Secrets are cards of its own deck and admitted, so echoing those is safe.
  - The reviewer's main case (Bonepaw's Ice Barrier, Duplicate, Effigy, Mirror Entity, Counterspell, Explosive Runes) now works.
- **Where it is recorded:** the code comment, the F14.json power note (via `make_f14.py`) and the heist-recast-power admission reason all state this; the reason now names E1-E10.
- **Tests in c-enc-14:**
  - E4 reworded: the boss's own Counterspell in play is not an echo.
  - E6: while the boss's Ice Barrier is in play, no echo is possible, in the game or the pilot's fair view.
  - E7: once it leaves play, the power is offered (also in the fair view) and a new Ice Barrier enters the boss's Secret zone.
  - E8: with a full Secret zone, nothing is echoed.
  - E9: with room again, Effigy is the only echo.
  - E10: the run player's Explosive Trap is never an echo.
  - E6 and E7 use the golden pilot's own fair-view options (TrackOwnDeck, own Secrets kept), because the default view refuses a boss holding Secrets.

### F14.2 enc14-cards check (agreed)
`bp.sh` line 225 now uses `[A-Za-z0-9_]+ x0`. On the reviewer's sample line it reports `DALA_BOSS_03t x0 DALA_BOSS_03t3 x0 DALA_BOSS_06dk x0`.

### F14.3 D4 (agreed)
The real offer and the fair-view offer are now read before Open Wormhole is used. D4 now passes with both true (real True, view True).

### Regenerated
- `gen.sh` output: encounters "84 rows, 88 decks (45 authentic, 43 changed), 206 adaptations, 1 rejected"; catalog unchanged; ledger 4740 / 2813 / 352 / 517 / 1058.
- A further `gen.sh` run plus the fixture generator left the tree byte-identical to the commit. That run happened after I committed.
- Engine: 0 warnings / 0 errors, md5 e28a99f5e45257ae94a2f123781d43c0.

### New numbers (all through the lock)
- **c-enc-14:** 117 passed, 0 failed (was 112), run 1 = run 2 identical.
- **Pin check, run once:** c-admission 3247, c-catalog 127, c-content 904, c-enc-model 141, all 0 failed. No pin moved.
- **Bosspilot**, each mode twice and identical, stderr empty, 0 faults / fallbacks / failed checks / swallowed, power-unused 0, form-unreached 0:

| Mode | Games | Boss wins | Watched cards played | Most boss Secrets |
|---|---|---|---|---|
| enc14 | 30 | 23 | 03t x2, 03t2 x1, 03t3 x3, 03t4 x5, 06dk x0 | 3 (was 2; Bonepaw's two games changed, power uses now 6 and 4) |
| enc14-seeds | 30 | 21 (was 20) | 03t x2, 03t2 x3, 03t3 x3, 03t4 x2 | 4 |
| enc14-cards | 12 | 7 | 03t x14, 03t2 x21, 03t3 x18, 03t4 x9, 06dk x12 | – |

enc14-cards still prints "all 5 boss cards played by the boss", now with the corrected pattern. enc14-flight was not rerun; this change does not touch Belnaara (earlier result: 8 games, 8 boss wins).

The rest of my earlier report stands.
