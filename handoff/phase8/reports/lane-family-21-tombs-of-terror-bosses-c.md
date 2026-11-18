# Family 21 (Tombs of Terror bosses C): report

Family 21 is done on branch `wip/f21` in `/home/user/wt/f21`, as one commit, `b16bd33` (my earlier WIP commits are squashed into it). The tree is clean, nothing was pushed by me, and no harness is still running. 17 of the 18 encounters are done. Jythiros of Questions (58) is rejected with a stated reason. Every lane check passes twice with identical logs.

## Flags for the integrator
1. **One shared engine edit**, in `engine/SabberStoneCore/src/Actions/Draw.cs` (`PreDrawPhase`). The fatigue damage now goes through `DungeonRunEncounters.TombsC.FatigueDamage(controller, dmg)`, which doubles it only while the hero's power is `ULDA_BOSS_52p2` Broken Phylactery.
   - Why: "double damage from fatigue" can only be done exactly at the place where fatigue damage is computed.
   - Effect elsewhere: one string compare per fatigue hit, nothing else. Selfplay and randplay were not run, per the brief.
2. **Tag:** I used no private tag, not even my reserved 32656. All state sits in existing tags: the power's script number, the model's `FormIndex`/`FormChanges`, and family 09's AutoCast count.
3. **Power-only form copy:** `TombsC.PowerOnlyForm` is a copy of family 14's `EncounterRules.SwapPowerForm`. I gave it a different name so there is no duplicate method after the merge; you may fold the two into one.
4. **`tombs_tiers.py`** was checked out from `wip/f19` and is byte-identical to it.
5. **Shared Uldum collectibles:** I scripted none (nothing that family 20 might also touch). The unscripted cards in my decks were replaced by named scripted cards instead. They are:
   - EVIL Totem ULD_276, Riftcleaver ULD_165, Desert Hare ULD_719, Desert Obelisk ULD_703, Sahket Sapper ULD_280, Fishflinger ULD_289
   - also Murmy, Generous Mummy, Kobold Sandtrooper, Injured Tol'vir, Sunstruck Henchman, Sinister Deal, Livewire Lance, Expired Merchant, EVIL Recruiter, Quicksand Elemental
   - Gral, the Shark was replaced because its admission (`from-own-deck`) has no boss row.
6. **Eligibility question (like family 19's Squeamlish):** a run on the Heist hero Ol' Barkeye can meet the boss `ulda_50_barkeye`. The original adventure had no rule against this. Fixing it needs shared edits, so I left it open.
7. **Nothing forbidden was touched:** no schedule defaults, finale, acts, pool sets, save version, ReleasedShapes or golden sequences.

## Per id
**Done:**
- **41p Running Wild:** draws a random minion from the boss's own deck; a Beast adds a second copy to hand.
- **42p Sand Shapin':** shuffles a public copy of an enemy minion (never a shrine) into the boss's deck, marked `ULDA_BOSS_42e` Sand Rush. When drawn, it is summoned and its holder draws a card, using family 04's `UldumRules.SummonDrawn`.
- **43p Spoil the Ending:** copies a random spell from the run player's hand, costing (2) less.
  - Nothing links the copy to the source card, and the boss's fair view keeps the player's hand masked (tested).
  - Because the search only sees masked stand-ins, the boss AI rarely sees value in the power. That is why Jolene shows "power-unused" below.
- **45p Reliquary's Relics:** after the run player casts a spell, a copy goes to the boss's hand.
- **46p Maw of the Desert and 48p Arena Expert:** cast by rule at the start of the boss's turn, because the wiki marks them Auto-cast (46p also carries AI_MUST_PLAY). This uses family 09's `AutoCast`.
  - Maw destroys the lowest-Health minion on either board; a tie is broken by the game RNG, and a shrine goes dormant.
  - Arena Expert uses Duel!'s rule (family 02's `Duel`).
- **47p What's in the Jar?:** a 1/1 copy of any minion, never a shrine.
- **49p Demon Emissary:** a random scripted Demon costing (3) or less.
- **50p Desert Warfare:** after the boss's hero attacks, a random Hunter card, excluding Quests and Secrets.
- **51p Tol'vir Necromancy:** a minion of either player that is Reborn is healed to full.
- **52p1 Lich's Phylactery / 52p2 Broken Phylactery (the power swap):**
  - Phylactery restores 20, minus 1 for each friendly minion death. Used once, it breaks into the row's script form 1 (power only: hero, Health, Armor and weapon stay), which is Broken Phylactery with double fatigue.
  - The one-use rule is my reading, recorded as an adaptation: the wiki gives no rule, and the client card's "Modify Hero Power" tag plus a passive successor point to it.
  - The power is offered only while the boss's hero is damaged.
- **53p E.V.I.L. Conscription:** a random scripted Lackey.
- **54p Cavalry of E.V.I.L.:** 1 damage to a minion, then Charge if it survives.
- **55p Fish Food:** restores 2 to every Murloc on both boards.
- **56p Sandy Surprise:** a 1-Cost minion the boss plays gets Stealth.
- **57p Titan's Beast:** sets a minion to 3/3.
- **59p Food Chain:** 2 damage to a minion; if it dies, the boss's hero restores 5.
- **Heroes 41h–59h:** rows only, no script needed.
- **ULDA_BOSS_42d** (Sand Shapin Dummy): recorded as a support record.

**Rejected:** ULDA_BOSS_58h and 58p, Jythiros of Questions (Riddle of the Sphinx).
- Reason: the guess is a decision by the run player on the boss's turn, and a correct guess counters a card of any type.
- The engine's decision loop, NativeMatch and the harnesses only answer the current player's decisions, and the engine can only counter spells.
- Moving the guess to the player's own turn, or countering spells only, would be an invented mechanic.
- Lift: phase 9 (economy) — a decision by the non-current player in the decision loop, NativeMatch (the client prompt) and the harnesses, plus a Counter for minion, weapon, hero and location plays in PlayCard.
- The rejection is recorded in `F21.json`, in curation E21, and in a `boss-tombs-c-rejected` admission row.

**Tiers** (family 19's rule):
| Tier | Rows |
|---|---|
| 1 | Alda, Murlocs, Frog |
| 2 | Rhogi, Jar, Ilza, Kasmut, Maru, Janak, Winged Guardian, Dire Bat |
| 3 | Belloc, Wildtooth, Barkeye |
| 4 | Jolene, Craz, Baz'hial |

Wiki pages were read on 2026-11-14 (raw pages are in the gitignored `verify/.out/tmp/wiki`). Baz'hial's deck is authentic. Belloc's 31-card list loses one card to the 30-card rule.

## Files
**Own files:**
- `engine/SabberStoneCore/src/CardSets/DungeonRun/DungeonRunEncounters.F21.cs`
- `plugin/DungeonRun/DungeonRunAdmission.F21.cs`: 7 rows, families `boss-tombs-c-*`
- `data/dungeon-run/reference/encounters/F21.json`
- `verify/suites/p8f21/c-enc-21/`
- `verify/tools/gen/p8f21/make_f21.py` and `patch_curation.py`

**Shared edits** (each minimal):
- `Draw.cs`: the fatigue hook, flag 1.
- `curation.json`: E21 appended to `landedFamilies`; the tool-owned lists were regenerated.
- `c-admission`: one exemption line for `boss-tombs-c-` families.
- `c-content` and `c-enc-model` J13: the passive-list pins gain 45p, 46p, 48p, 50p, 51p, 52p2, 56p.
- `c-enc-model`: A4 gains "21:17"; A10 now expects 2 rejections (Toki, then Jythiros).
- `c-catalog` and `c-deckops`: the ledger pins.
- `suites.sh`: c-enc-21 appended to BUILD_LIST and RUN_LIST.
- `bp.sh`: A21/B21/CARDS21 variables, four mode lines, and a usage comment line.

Generated files (encounters, ledger csv/md) were regenerated, not hand-edited.

## Pins moved
- **c-admission:** 3167 → 3239; generated lists 426 → 445.
- **c-catalog ledger golden:** 4704/2813/295/538 → 4726/2813/335/520.
  - +22 rows: 17 `run:boss` rows plus DAL_614, DAL_743, GIL_816, LOE_110, TRL_525.
  - 18 powers move from missing to partial.
- **c-deckops T5:** 4704 → 4726 rows.
- **c-enc-model:** A4, A10 and J13 as above.
  - Detail lines only: E3 Hagatha as act II major 506 → 577, Icarax as act III major 1000 → 509; E4 no-ledger 96 → 49; G10 Voone act 1 → 2. These are the same moves family 19 saw (ULDA provenance mix).
  - The F2 file-length lines differ from the Windows baseline on Linux, as family 19 noted; that is not my change.
  - Still 141 passed.

**Generators** (run twice, byte-identical, equal to the commit):
- encounters: "86 rows, 90 decks (44 authentic, 46 changed), 231 adaptations, 2 rejected"
- catalog: unchanged
- ledger: 4726 rows, implemented 2813, partial 335, missing 520, duplicate/support 1058

## Tests
**c-enc-21** ("RESULT 100 passed, 0 failed"). It plays real engine games through `DungeonRun.BuildPlan` → `DungeonRunSetup`:
- sections A–R, one per boss, including shrine cases (Rhogi, Jar, Wildtooth), Jolene's fair-view masking, Baz'hial's swap/harvest/fatigue (2 then 4, the run player's fatigue unchanged) and the AutoCast counts;
- section S: rows, tiers, flags, texts and costs, admission, the rejection, and two validator negatives.

**Bosspilot**, each mode twice identical, 0 faults / 0 fallbacks / 0 failed checks / 0 swallowed, stderr empty, no recorded baseline yet:

| Mode | Games | Boss wins | Power-unused | Notes |
|---|---|---|---|---|
| enc21a (41–49) | 16 | 14 | 1 (Jolene) | ULD_217 x2, DAL_614 x9, FP1_007 x4 |
| enc21b (50–59) | 18 | 16 | 0 | form-unreached 0: Baz'hial broke the Phylactery in play |
| enc21-seeds-a | 16 | 10 | 2 (Jolene; Rhogi on this seed base) | Rhogi used his power twice in enc21a |
| enc21-seeds-b | 18 | 16 | 0 | |

Maw and Arena Expert are cast by rule, so the harness does not count them as power uses. The suite's sections E and F prove they fire.

## Lane verification
- Engine: 0 warnings / 0 errors (md5 920afde95814aa00335cfaffb635e73c on Linux).
- Suites, run twice with run 1 = run 2: c-content 904, c-admission 3239, c-runlayer 244, c-acts 181, c-deckops 174, c-catalog 127, c-shrine-core 131/0, c-enc-model 141, c-enc-engine 122, c-enc-21 100.
- Against the wave-3 baseline: 0 differing lines everywhere except c-admission (4 lines) and c-enc-model (14 lines), all explained above.

## Paragraph for docs/design/dungeon-run.md
"Family 21 (Tombs of Terror bosses C): seventeen Tombs encounters ULDA_BOSS_41–59 (without 44, Trap Room, and 58, Jythiros of Questions, rejected: Riddle of the Sphinx needs a decision of the run player on the boss's turn and a Counter for every card type, phase 9 / client). Tiers by family 19's level rule. Lich Baz'hial swaps her power inside the match: Lich's Phylactery (restores 20, less 1 per friendly minion death) breaks when used into Broken Phylactery, the row's script form 1 that changes only the power, and the boss then takes double fatigue damage (Draw.PreDrawPhase). Maw of the Desert and Arena Expert are cast by rule at the boss's turn start (the wiki's Auto-cast). Spoil the Ending copies a random spell of the run player's hand without linking the copy to its source; the boss's fair view keeps that hand masked. Sand Shapin' shuffles a public copy of an enemy minion into the boss's deck, summoned when drawn. Run shrines are never copied (What's in the Jar?, Sand Shapin') and go dormant when destroyed (Maw). Unscripted Saviors of Uldum collectibles and Secret generators in the wiki lists are replaced by named scripted cards."

## For the integrator after the merge
- Rerun gen.sh and recompute the pins above together with the other families.
- Expect textual conflicts in curation `landedFamilies`, the c-content and c-enc-model passive lists, c-enc-model A4/A10, the c-catalog and c-deckops goldens, `suites.sh`, `bp.sh`, and the c-admission exemption lines.
- Re-record the enc21 modes on the merged tree (seeds depend on row index).
- Rerun the encounters step when any of the replaced Uldum collectibles gets a script; c-enc-21 S8 pins Jolene's and the Jar's substitutions.
- Nothing was added to `verify/MANIFEST.md`.
- I did not add README or baseline lines; the new numbers to record are c-enc-21 "RESULT 100 passed, 0 failed" and the four bosspilot modes in the table above.# Family 21 review: confirmed findings (adversarially verified)

## F21.1 [low] verify/suites/p8f21/c-enc-21/Program.cs:480
K4 cannot fail. It summons an undamaged minion with HAS_BEEN_REBORN=1 and only damages it after the summon. Tol'vir Necromancy's AFTER_SUMMON trigger already requires m.Damage > 0 at summon time, and it never reacts to later damage. So the DISPLAYED_CREATOR/graveyard guard the check is named for (F21.cs:293-295) is never exercised.

Scenario: Reduce the condition at DungeonRunEncounters.F21.cs:293 to `p is Minion m && m[GameTag.HAS_BEEN_REBORN] == 1 && m.Damage > 0`, so that any damaged minion carrying the mark (for example one copied by BonusEffects.cs:127, which copies HAS_BEEN_REBORN) is healed to full when summoned. K4 still passes: the copy is summoned with Damage 0, the trigger condition is false whatever the guard says, and Hit(g, copy, 2) comes afterwards, so copy.Damage == 2 always holds. To test the guard, the minion must already be damaged when it is summoned (for example set Damage in its EntityData).

Verifier: Confirmed. The Tol'vir Necromancy trigger at F21.cs:288-301 is AFTER_SUMMON, and its condition requires m.Damage > 0 at summon time. K4 (Program.cs:477-480) summons an undamaged Yeti with HAS_BEEN_REBORN=1, so the condition is false whatever the DISPLAYED_CREATOR/graveyard guard says. Hit(g, copy, 2) runs after SummonBlock returns, when the trigger has already been evaluated and no longer reacts. If the guard on lines 293-295 were deleted, K4 would still pass. No other check exercises that guard.

Fix: Summon the marked minion already damaged, so that only the guard can keep the heal from happening. For example: `var copy = (Minion)Entity.FromCard(boss, C(Yeti), new EntityData { [GameTag.HAS_BEEN_REBORN] = 1, [GameTag.DAMAGE] = 2 }); Generic.SummonBlock(g, copy, -1, boss.Hero); Check("K4 ...", copy.Zone == boss.BoardZone && copy.Damage == 2);` Drop the later Hit.

## F21.2 [low] verify/suites/p8f21/c-enc-21/Program.cs:249
The C5 check name says "the Moonfire copy costs 0, not less", but the assertion only tests seen.SetEquals({Moonfire, Frostbolt}). The cost floor for a (1)-cost spell discounted by (2) is never checked anywhere in the suite.

Scenario: If Spoil the Ending's ULDA_BOSS_43e1 (F21.cs:203) produced a negative or wrong Cost for Moonfire (Cost 0 expected after the 2 discount), C5 would still print PASS with a name that claims the cost was checked. C2 only checks Fireball (4 -> 2), so the 0 floor the name promises is not pinned.

Verifier: Confirmed. The C5 name (Program.cs:248) promises that 'the Moonfire copy costs 0, not less', but the assertion is only seen.SetEquals({Moonfire, Frostbolt}). The loop never reads the copy's Cost. C2 checks only Fireball 4 -> 2. If ULDA_BOSS_43e1 (F21.cs:203) gave Moonfire a wrong or negative cost, C5 would still pass while its name claims the 0-cost floor was checked.

Fix: Inside the loop, record the cost as well: `if (boss.HandZone.Count == 1) { var p = boss.HandZone[0]; seen.Add(p.Card.Id); if (p.Card.Id == Moonfire) moonfireCosts.Add(p.Cost); }`. Then assert `seen.SetEquals(...) && moonfireCosts.Count > 0 && moonfireCosts.All(x => x == 0) && Has(copy, "ULDA_BOSS_43e1")`. Otherwise, remove the cost claim from the check name.


Review fixes: commit 100850a on wip/f21 rewrote K4 and C5 (each confirmed to fail against broken code); c-enc-21 still 100 passed, 0 failed; nothing else moved.
