I've implemented family 22 on `wip/f22`, and all 37 ids are done; none is rejected. All the lane checks pass twice with identical logs, and every bosspilot mode ran clean (0 faults, fallbacks, failed checks or swallowed exceptions).

Commits: 6429b62 and d8b5f19 (the coordinator's WIP saves) and 9ea9e17 `feat(dungeon-run): family 22 Tombs of Terror bosses D (ULDA_BOSS_60-79, Servant of Siamat)`. The tree is clean and I pushed nothing myself. Nothing I started is still running.

## FLAGS for the integrator
1. **Shared edit: one new tag constant.** `EncounterRules.MirageIllusion = 32657` (my reserved number) is in the tag table of `engine/.../Actions/EncounterRules.cs`, after `SetAsideStore`. No extra tags.
2. **Shared edit, `c-admission/Program.cs`:** I added the same two lines family 19 added (the `boss-tombs-` exemption). The diff hunk and the resulting file are byte-identical to wip/f19's, so it should merge cleanly. My admission families are named `boss-tombs-d-*` so they don't move family 19's counts.
3. **Other shared edits:** the pins listed below, `verify/lib/suites.sh` (c-enc-22 appended to BUILD_LIST and RUN_LIST), and `verify/bin/bp.sh` (usage line, A22/B22/CARDS22 variables, four case lines after the family 10 lines, and the usage print range `2,29p` → `2,30p`). Expect textual conflicts with families 13, 17, 18, 19 and 23 in these files and in `curation.json` (landed family E22 is appended by `verify/tools/gen/p8f22/patch_curation.py`).
4. **No Uldum collectible was scripted.** Unscripted cards are replaced in the decks, as family 19 did, so there's no overlap with families 20/21 there. Wrath (EX1_154) is also unscripted in the engine.
5. **Mirage has no client token.** Neither the power text nor the wiki names an illusion card id, and the client data has none. I built it from the wiki's notes on A Beautiful Oasis (details under ULDA_BOSS_62p below). The boss knows which hand slot it made the illusion (its own power's private tag), but never the copied card.
6. **Not touched:** schedule defaults, finales, acts, pool sets, save version, ReleasedShapes and golden sequences. I didn't regenerate the p8f02–p8f10 family fixtures (as family 19 also didn't); rebuild them after the merge.

## Per id
- **Heroes ULDA_BOSS_60h–79h:** done, no script needed.
- **60p Forbidden Power (4):** N damage to a random enemy character; N is the power's own script number, starting at 1 and +1 whenever a friendly minion dies.
- **61p Chain Lightning (4):** 1 damage to a minion, then jumps to a random adjacent minion until one dies, no neighbour is left, or a hit does nothing. A dormant shrine is not a neighbour.
- **62p Mirage (0):** auto-cast at the start of the boss's turn while the run player's hand holds a card and has room.
  - A copy of a random card of that hand joins the hand, and one of the two (chosen at random) is the illusion.
  - Played, the illusion resolves normally and its player discards a random card.
  - At the end of that player's turn the illusion leaves the hand (with both still there, the copy leaves).
- **63p Plaguebringer (passive):** the boss's spells cost (2) less and each one it plays adds Overload (1); Tunnel Trogg counts the extra Overload.
- **65p Ogre-Sized Thief (3):** steals a random card of the run player's deck into the boss's hand, reusing family 01's deck-steal helper (`DungeonRunShrines.StealFromDeck`), the one Treasure from Below (TRLA_186) uses. Tests show:
  - the boss knows exactly the stolen card;
  - the player's other deck cards stay unknown;
  - the run player's own view sees a hidden card in the boss's hand;
  - a steal simulated in the boss's search takes a masked card without errors.
- **66p Masterful Djinn (2) and ULDA_026 Servant of Siamat:** a 1/4 servant with a random one of Taunt, Rush, Windfury or Divine Shield; Silence removes it.
- **68p "Upgraded" Fauna (2):** one hit on an enemy minion equal to the boss's Beast count; not offered without a Beast.
- **69p Mistress of Magic (passive, both players):** a copy of each player's first played spell goes to that player's hand. The boss's view keeps the run player's copy masked.
- **70p Lord of the Sun (1):** Spell Damage +3 this turn; offered only while the boss holds a spell.
- **71p Lord of Life (2):** restores a damaged minion to full Health.
- **72p Lord of Destruction (0):** auto-cast; it puts a stacking Cursed! (ULDA_BOSS_72e) on a random enemy minion, which deals 2 to its controller's hero at the end of that player's turn. Silence removes it.
- **73p Shockwave (passive):** the boss's minions have Rush.
- **74p Burst of Light (3):** 1 damage to each enemy minion; the power's Lifesteal heals the boss.
- **75p Hunt the Weak (2):** N damage to a minion, N then +1, or back to 1 if the minion died.
- **76p Noble Champion (4):** damage to a random enemy minion equal to its Attack.
- **77p Snapping Jaw (2):** doubles the hero's Attack this turn. I gave Strong Jaw (ULDA_BOSS_77e) the one-turn tag the client data lacks, so the enchantment itself ends with the turn.
- **78p Massive Tentacles (0):** auto-cast; destroys every minion when there are 8 or more. A run shrine counts while awake and goes dormant (stays on the board); a dormant shrine doesn't count.
- **79p Mech "Pilot" (1):** a Mech on either board gets +2 Attack this turn and is forced to attack a random enemy minion.

## Tiers and decks
- **Tiers** come from family 19's `tombs_tiers.py`, imported as its header says:
  - tier 1: Weaponized Wasp, Gorebite (Gorebite's table has no "Total cards" column; its ten cards are treated as the "10; 15" shape, recorded as an adaptation);
  - tier 2: Thrud, Tarkus, Armadillobot;
  - tier 3: A Beautiful Oasis, Vessina, Barim, Octosari;
  - tier 4: Zaraam, Kyriss, Siamat, Isiset, Rajh, Ammunae, Setesh, Husam, Avozu.
- **Flags:** Oasis and Thrud `hiddenZone`; Vessina and Husam `passive`; Isiset `passive`, `bothPlayers`, `recast`; Armadillobot `forcedAttack`.
- **Wiki sources:** one page per boss, read 2026-11-14 (raw and rendered tables agree). Raw pages are in the gitignored `verify/.out/tmp/wiki`.
- **Replacements** (unscripted cards unless noted):
  - Faceless Lurker → Fen Creeper
  - Garden Gnome → Dragonling Mechanic
  - Fishflinger → Murloc Tidehunter
  - Conjured Mirage → Sen'jin Shieldmasta
  - Pit Crocolisk → Fire Elemental
  - EVIL Totem → Flametongue Totem
  - Mischief Maker → SI:7 Agent
  - Wrath → Living Roots
  - Desperate Measures → Power Word: Shield (it generates Secrets)
  - Family 19's choices for Sinister Deal, EVIL Recruiter, Injured Tol'vir, Ogre Brute, Spitting Camel, Hunter's Pack, Kobold Sandtrooper and Livewire Lance
  - Weaponized Wasp: Bloodfen Raptor fills the last two slots (Desert Hare is unscripted)

## Pins moved
| Suite | Old → new | Cause |
|---|---|---|
| c-admission | 3167 → 3244; generated lists 426 → 445 | the nine new rows |
| c-content passive list | still 904 | six passives added: 62p, 63p, 69p, 72p, 73p, 78p |
| c-catalog ledger golden | 4704/2813/295/538 → 4727/2813/337/519 | +18 run:boss rows and 5 new deck cards (DAL_614, EX1_025, EX1_179, GVG_093, ICC_023); 19 rows leave missing |
| c-deckops T5 | 4704 → 4727 | same rows |
| c-enc-model A4 | + "22:18" | the new rows |
| c-enc-model J13 | six passives added | same |

c-enc-model detail lines only (still 141 passed), the same moves family 19 saw because both add 18 ULDA rows:
- E3: Hagatha as act II major 506 → 577, Icarax as act III major 1000 → 509
- E4: no-ledger 96 → 49
- G10: Voone act 1 → 2

The F2 save lengths differ only by Linux vs Windows line ends.

Generators: encounters "87 rows, 91 decks (50 authentic, 41 changed), 215 adaptations, 1 rejected"; catalog unchanged; ledger 4727 / 2813 / 337 / 519 / 1058 / 0.

## Lane verification
- **Engine:** 0 warnings, 0 errors (md5 9111c2993e1c598f3e69484a9a4dfc92).
- **gen.sh:** the second and third runs are byte-identical. The first run only refreshed the committed ledger .md after I corrected section letters in two admission reasons.
- **Suites, twice:** run 1 = run 2 for every suite. c-content 904, c-admission 3244, c-runlayer 244, c-acts 181, c-deckops 174, c-catalog 127, c-shrine-core 131/0, c-enc-model 141, c-enc-engine 122, c-enc-22 116 (all 0 failed). Against the baseline, only c-admission (4 lines) and c-enc-model (12 lines) differ, both explained above; the rest have 0 differing lines.
- **New suite c-enc-22:** "RESULT 116 passed, 0 failed", sections A–R (one per boss) and S (rows, tier rule, client texts and costs, admission, validator).
- **Bosspilot** (each twice in one call, identical, stderr empty, power-unused 0, no recorded baseline yet):

| Mode | Games | Boss wins | Power uses |
|---|---|---|---|
| enc22a (60–69) | 16 | 7 | 47 |
| enc22b (70–79) | 20 | 16 | 48 |
| enc22-seeds-a (SEED_BASE 19000) | 16 | 11 | 50 |
| enc22-seeds-b (SEED_BASE 19000) | 20 | 13 | 40 |

The rows with 0 uses are the six passive or auto-cast powers, which are never options; c-enc-22 checks that they fire. Watched cards played: Tunnel Trogg x3, x0, x3, x0 and Harvest Golem x0, x1, x0, x2.

## Paragraph for docs/design/dungeon-run.md
"Family 22 (Tombs of Terror bosses D): eighteen Tombs encounters ULDA_BOSS_60–79 (without 64 and 67) and Siamat's Servant (ULDA_026), tiered by family 19's Tombs rule (tombs_tiers.py; Health from the act curve). Twelve clickable powers: Forbidden Power and Hunt the Weak keep their growing number in the power's own script number; Chain Lightning jumps to random adjacent minions until one dies; Ogre-Sized Thief steals a card of the run player's deck by TRLA_186's deck-steal rule, so the boss learns only that card; Mech "Pilot" forces a Mech to attack (EncounterRules.ForcedAttack). Three passive powers: Plaguebringer (spells (2) cheaper, Overload (1) each), Mistress of Magic (both players' first spell copied) and Shockwave (Rush). Three powers the wiki lists as auto-cast are cast by rule at the boss's turn start: Lord of Destruction (stacking Cursed!), Massive Tentacles (destroys all at 8+ minions; a run shrine goes dormant) and Mirage. The client data has no illusion token, so Mirage follows the wiki's notes: a copy of a random card of the run player's hand joins it, one of the pair is the illusion (private tag 32657), playing it discards a random card, and it fades at the end of that player's turn. Decks are the wiki's Normal lists, extended with second copies and the Heroic list; unscripted cards are replaced by named scripted cards."

## To redo after the merge
- Regenerate with gen.sh and recompute the pins above together with families 20 and 21, and with 19 for the ledger/A4/J13/passive-list values.
- Rebuild the family fixtures, then record the four enc22 modes on the merged tree: seeds depend on row index, and F22 rows sit after F19–F21.
- Nothing was added to `verify/MANIFEST.md`.

---

# Family 22 review: confirmed findings (adversarially verified)

## F22.1 [low] engine/SabberStoneCore/src/CardSets/DungeonRun/DungeonRunEncounters.F22.cs:287
Plaguebringer (ULDA_BOSS_63p) only adds Overload to the boss's mana (OverloadOwed, OverloadThisGame, OnOverloadTrigger, plus a hand-coded Tunnel Trogg buff in TombsD.Overload at lines 120-128). It never makes the spells count as cards with Overload. So Thunderhead (BOT_407), whose AFTER_PLAY_CARD condition is SelfCondition.IsOverloadCard (me.Card.HasOverload, BoomsdayCardsGen.cs:1810-1816), does not trigger for the boss's spells that have no Overload of their own. The row's adaptation note (verify/tools/gen/p8f22/make_f22.py:141, copied into F22.json and encounters-v1.json) says Thunderhead sees this Overload, and c-enc-22 has no Thunderhead test.

Scenario: Vessina (ulda_63_vessina; BOT_407 is in its deck in encounters-v1.json) has Thunderhead on board and plays Lava Shock, Haunting Visions or Earthquake. None of these prints Overload. Under "Your spells Overload (1)" each is a spell that Overloads, and the note says Thunderhead's Overload rule sees it. The engine still summons no Sparks because Card.HasOverload is false: Tunnel Trogg grows, Thunderhead does nothing.

Verifier: Confirmed. TombsD.Overload (DungeonRunEncounters.F22.cs:120-128) only raises OverloadOwed and OverloadThisGame, fires OnOverloadTrigger and hand-codes the Tunnel Trogg buff. Thunderhead (BoomsdayCardsGen.cs:1810-1818) is AFTER_PLAY_CARD with SelfCondition.IsOverloadCard, which is me.Card.HasOverload (SelfCondition.cs:153). That is a static card property the passive never changes. BOT_407 is in enc-ulda_63_vessina (encounters-v1.json:83). Several of her spells print no Overload, for example Haunting Visions (TRL_351) and Rain of Toads (ULD_181), so Thunderhead never fires for them. The shipped adaptation note (make_f22.py:141, copied into F22.json and encounters-v1.json) says Thunderhead's Overload rule sees this Overload. Retail also implies it: the card text says each spell Overloads, so each is a card with Overload. The implementer's own report (line 21) claims only Tunnel Trogg, so the note and the engine disagree. Eternal Sentinel does work, because it unlocks OverloadOwed. No test covers Thunderhead.

Fix: In TombsD.Overload, after the Trogg loop, handle spells that print no Overload of their own (so one that does is not counted twice): `if (!spell.Card.HasOverload) foreach (Minion th in c.BoardZone.Where(m => m.Card.Id == "BOT_407" && !m.IsSilenced).ToList()) new SummonTask("BOT_102t", 2, SummonSide.ALTERNATE).Process(c.Game, c, th, null, null);`. Add a c-enc-22 check: Vessina with Thunderhead on board plays Haunting Visions and two Sparks appear. If Thunderhead is meant to stay untriggered instead, drop 'Thunderhead's' from the note in make_f22.py:141 and regenerate F22.json and encounters-v1.json.

## F22.2 [medium] engine/SabberStoneCore/src/CardSets/DungeonRun/DungeonRunEncounters.F22.cs:171
Mirage saves the real entity id of a hidden card in the opponent's hand on the boss's own hero power (power[MirageIllusion] = illusion.Id). The fair view's CanonicalizePrivateActors (plugin/FairBotState.cs:1197-1220) renumbers every hidden slot. It strips CREATOR, PARENT_CARD, LAST_CARD_PLAYED and LAST_CARD_DRAWN from visible actors and remaps tag 32120, but leaves MirageIllusion alone. So in the boss's view the tag still holds the hidden card's id from before renumbering, and that id points at an unrelated entity.

Scenario: Fight ulda_62_oasis. At the boss's turn start Mirage gives an illusion, for example the original run-deck card with id 12. Build the boss's FairBotState with exact=false, so canonicalization runs. The run player's hand cards become Unknown slots with new ids taken from the free-id queue. The view's ULDA_BOSS_62p still has MirageIllusion=12. In the view, TombsD.Illusion(power) returns whatever now owns id 12: another renumbered hidden slot (possibly a deck card), a REMOVEDFROMGAME filler, or null (TryGetValue fails). It never returns the real illusion slot. Results: (1) the boss's search never models the illusion's discard. IllusionPlayed compares played.Id with the stale id. In reply views the opponent's hand is rebuilt by ReplaceUnknown with fresh ids, so the ids never match. Clear and IllusionFades act on the wrong entity. (2) The admission reason (DungeonRunAdmission.F22.cs, boss-tombs-d-hidden-illusion: "the boss knows only which hand slot it made the illusion") and the lane report's flag 5 are false in the fair view. (3) A visible tag in the view keeps a private allocation id. When the illusion is the original card, that is its run-deck slot id (Game.Player1DeckSlotIds order), which canonicalization exists to hide; CREATOR references to hidden ids are stripped for the same reason. Section C3 of c-enc-22 checks only that the hand is masked. It never reads Illusion() on the view's power, so the suite cannot catch this. Fix: remap or strip MirageIllusion in CanonicalizePrivateActors (as is done for 32120), or keep the illusion's hand position instead of its entity id.

Verifier: Confirmed. Mirage stores the illusion's real entity id on the boss's own hero power (F22.cs:171). In the fair view, Mask (FairBotState.cs:1479-1530) replaces the run player's hand cards with EngineUnknown under the same id and drops every tag, the illusion's own MirageIllusion link included. With exact=false (Create, line 522), CanonicalizePrivateActors (1172-1230) then removes every hidden id and gives each slot a new id from the free queue. Slots are sorted by controller, zone (DECK before HAND) and position, so the old id usually lands on a different slot: a deck slot, a REMOVEDFROMGAME filler, or no entry at all. Only CREATOR, PARENT_CARD, LAST_CARD_PLAYED, LAST_CARD_DRAWN, 32120, choices and assemble frames are fixed up. The opponent-only internal-tag strip at 836-840 skips the boss's own power, and nothing else touches 32657. Effects in the view: (1) TombsD.Illusion(power) resolves to an unrelated slot or to null. IllusionPlayed can never match the real illusion slot, so the search never models the discard. IllusionFades can move a different run-player hand slot to setaside, or do nothing. (2) The admission reason's claim that the boss knows only which hand slot is the illusion (DungeonRunAdmission.F22.cs:93-100) does not hold in the canonical view. (3) The power's visible tag value depends on the hidden private allocation id, which canonicalization is meant to erase. When the original card is the illusion, that id is its run-deck slot id. Section C3 of the suite (Program.cs:243) checks only that the hand is masked. It never calls Illusion() on the view's power, so the suite cannot catch this.

Fix: In CanonicalizePrivateActors, beside the 32120 remap: `foreach (var actor in visible) if (ids.TryGetValue(actor[EncounterRules.MirageIllusion], out int m)) actor[EncounterRules.MirageIllusion] = m; else if (privateIds.Contains(actor[EncounterRules.MirageIllusion])) actor.NativeTags.Remove(EncounterRules.MirageIllusion);`. Better still, store the illusion's hand position (or a public slot index) instead of its entity id. Extend c-enc-22 C3 to check that TombsD.Illusion(view power) is the view's HandZone slot at the illusion's position.


I agree with both review findings and fixed them on `wip/f22` in commit 9fe1fa5 (on top of 9ea9e17). `plugin/FairBotState.cs` is unchanged. c-enc-22 now passes 120 of 120 twice, and the four enc22 bosspilot modes give the same numbers as before.

## F22.1: Thunderhead and Plaguebringer
- **Engine:** when Vessina plays a spell that has no Overload of its own, Plaguebringer now also makes Thunderhead (BOT_407) summon its two 1/1 Sparks. A spell that already has Overload triggers Thunderhead once by itself, so it is not counted twice.
- **Note:** the row's adaptation note in `make_f22.py`, which says Thunderhead sees this Overload, is now true and stays as written.
- **Test D7:** Moonfire with Thunderhead on board gives two Sparks; Lightning Bolt then gives two more, not four.

## F22.2: Mirage stored a hidden entity id
I followed the preference to store a public position instead of the entity id.
- **The power:** the boss's hero power now holds only the illusion's 1-based hand position. Hand order is public, so no private id is left on anything the boss sees.
- **The card:** the illusion card itself carries the private mark (its twin's id and whether it is the copy). The boss's fair view masks it with the rest of the run player's hand.
- **Keeping the position right:** a small aura on the power moves the position with the illusion whenever a card to its left leaves the hand, and clears it once the illusion leaves.
- **In the boss's fair view:** the illusion is the masked slot at that position, and the aura marks that slot so the view's own triggers act on it. The twin is unknown there.
- **Reply views:** in a view where the hand has been resampled the position is cleared. The search then does not model the illusion's discard, so it knows less, never more.
- **New guard:** a search that runs into the boss's next turn start in its view now copies nothing instead of copying a masked card. Without the guard the view would copy a masked card with no card id.
- **Wording:** the `EncounterRules.MirageIllusion` comment, the hidden-illusion admission reason and the row note now describe the position-based design.
- **Tests:**
  - C3b: the power's tag equals the illusion's position, and `Illusion()` on the view's power is the view's masked slot at that position.
  - C3c: after a discard to its left the position follows the illusion, and a new fair view finds the slot at the new position.
  - C3d: in the view the illusion slot fades at the end of the run player's turn and the next boss turn start throws nothing.
  - C4 was adjusted for the extra discard C3c makes.

## New numbers
- **Engine:** 0 warnings, 0 errors; md5 2dd9b5f10b2fe1b5b92116db0ddb707f.
- **gen.sh:** twice byte-identical. Generator lines are unchanged: encounters "87 rows, 91 decks (50 authentic, 41 changed), 215 adaptations, 1 rejected"; ledger 4727 / 2813 / 337 / 519 / 1058. Only reason and note texts changed in the generated files.
- **Suites:** each run twice, run 1 identical to run 2. The other lane suites (c-runlayer, c-acts, c-deckops, c-shrine-core, c-enc-engine) were not rerun after this fix.

| Suite | Result | vs baseline |
|---|---|---|
| c-enc-22 | RESULT 120 passed, 0 failed (was 116) | new suite |
| c-content | 904 | 0 differing lines |
| c-admission | 3244 | 4 lines (as before) |
| c-catalog | 127 | 0 differing lines |
| c-enc-model | 141 | 12 lines (as before) |

- **Bosspilot:** run in one locked call with the suites. Each mode ran twice with identical results, stderr empty, 0 faults, fallbacks, failed checks and swallowed exceptions, and power-unused 0.

| Mode | Games | Boss wins |
|---|---|---|
| enc22a | 16 | 7 |
| enc22b | 20 | 16 |
| enc22-seeds-a | 16 | 11 |
| enc22-seeds-b | 20 | 13 |

No other shared file changed beyond what I reported before; the only other non-family file touched is the comment on the 32657 tag constant in `EncounterRules.cs`.