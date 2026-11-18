# Family 20 report: Tombs B (ULDA_BOSS_19-36, plus Mogu Cultist ULD_705)

All 36 family ids are done, plus Mogu Cultist and its Highkeeper Ra; nothing is rejected. The lane verification passed: every suite run twice with identical logs, all four bosspilot modes twice with identical results. One mode shows power-unused 2, explained below. Everything is committed on wip/f20; nothing was pushed and nothing is still running.

## Flags for the integrator
1. **I scripted the shared Uldum collectible Mogu Cultist (ULD_705) and its Highkeeper Ra (ULD_705t).** Both were TODO stubs. I edited their two entries in `engine/SabberStoneCore/src/CardSets/Standard/UldumCardsGen.cs` (a shared engine file), the way families 03 and 04 did; the BOM is kept and the logic lives in my F20 file. Family 21 may also edit UldumCardsGen.cs, but on other lines. ULD_705 is now implemented and admitted in every scope, so it can show up in random generation pools. That can move seeded results in suites I did not run: randplay, selfplay and other families' bosspilots.
2. **No model tag used.** My reserved 32655 is untouched. No save field, schedule default, finale, act, pool set, ReleasedShapes or golden sequence was touched.
3. **Expect textual conflicts with family 19** in the same places it edited: `verify/lib/suites.sh`, `verify/bin/bp.sh`, the c-admission exemption line, the passive lists in c-content and c-enc-model, the c-catalog and c-deckops ledger goldens, and the curation.json `landedFamilies` list (E19 and E20). All the numbers need recomputing after the merge.
4. **Mother of Sand uses an in-place Overload trigger.** A task queued from the Overload event is never run (TRLA_158 has the same note), so the trigger runs at once. The Overload is raised as the card is played, so the Dust Devil arrives before the card's own effect.
5. **I used three non-family cards in the scripts:**
   - Whispering Sentinel's Lifesteal reuses Lightforged Blessing's enchantment DAL_568e, because the client has no enchantment of the power's own.
   - Corrupted Oasis adds Witchwood Apple's Treant (GIL_663t).
   - Beetle Stampede summons LOOT_413.

## Per id
- **Heroes ULDA_BOSS_19h-36h:** done; they need no script.
- **Clickable powers (14):**
  - 19p Titan Ritual: summons two Mogu Cultists, as many as fit; summoned, so no Battlecry.
  - 22p Power of Wrath: 1 damage to every damaged minion on either board.
  - 23p Hatching Time: Beasts in the boss's hand cost (1) less.
  - 24p Corrupted Oasis: two 2/2 Treants to the boss's hand.
  - 25p Beetle Stampede: summons a Plated Beetle.
  - 26p Gatling Wand: 2 damage split among enemy minions only.
  - 27p "Retired" Treasure Hunter: copies a random card of the run player's hand. In the boss's fair view the hand is masked, so the copy uses only cards the boss already saw go into it.
  - 28p Mighty Windstorm: each player swaps a random hand minion with a random board minion; a run shrine is never chosen.
  - 31p Pillaged Relics: a chosen minion gets +1/+1, Divine Shield or Taunt at random.
  - 32p Whispering Sentinel: summons a Wisp with Lifesteal.
  - 33p Out of Control: 1 damage to the center-most minions of each board. A dormant shrine counts for positions but takes no damage.
  - 34p Tomb Boomin': 4 damage split among all characters, both heroes included.
  - 35p Vengeful Construct: destroys a chosen minion with 2 or less Health and its neighbours; a shrine goes dormant.
  - 36p Madness Incarnate: hero gets +1 Attack this turn and makes forced attacks on up to 3 different random enemy minions. Stealthed and Immune minions are skipped, the weapon loses Durability, and the attacks stop when a hero dies.
- **Passive powers (4):**
  - 20p Spectral Swordsman: the hero has Stealth while it holds a weapon. It is set on equip and cleared when the last weapon leaves; the hero's own attacks don't end it.
  - 21p Staking A Claim: either player's Discover card gives every minion +1/+1.
  - 29p Mother of Sand: summons a Dust Devil after the boss Overloads.
  - 30p Fireshaper: Flamewaker's trigger.
- **Enchantments scripted:** ULDA_BOSS_21e, 23e, 31e1, 36e. The Heroic-only 27e and 36ex are not needed.
- **Decks:**
  - Unchanged wiki lists: Colossus of the Sun, Runaway Ruby, Earthrager Ptah.
  - Other short lists are brought to 30 by second copies and the Heroic list. Zenda and Edra each needed one curated filler (Ironfur Grizzly, Ironbark Protector).
  - Ol' Toomba's table sums to 32 (two copies each of Edwin VanCleef and Skycap'n Kragg). I hand-corrected the transcription in F20.json to one copy of each (30 cards) rather than let the tool trim the last two Kraggs; the row's adaptation note says so.
  - Unscripted cards replaced: Conjured Mirage→Sen'jin Shieldmasta, Cursed Blade→Light's Justice, Murmy→Micro Mummy, Garden Gnome→Keeper of the Grove, Bug Collector→Murloc Tidehunter, Crystal Merchant→Enchanted Raven, Sahket Sapper→Vilespine Slayer, Quicksand Elemental→Tar Creeper, Injured Tol'vir→Frostwolf Grunt, Plague of Madness→Upgrade!.
  - Hydrologist (its Discover offers Secrets) → Jeweled Scarab, a Discover card, so Staking A Claim keeps as many triggers.
- **Tiers** use family 19's `tombs_tiers.py`: t1 ×6, t2 ×3, t3 ×3, t4 ×6. Flags set:
  - passive: 20, 21, 29, 30
  - bothPlayers: 21, 22, 28, 33, 34
  - hiddenZone: 27, 28
  - position: 33, 35
  - forcedAttack: 36. This keeps Anomaly DALA_861 Ogreish out of Kasa's act.

## Files and commits
- **Own files:**
  - `engine/SabberStoneCore/src/CardSets/DungeonRun/DungeonRunEncounters.F20.cs`
  - `plugin/DungeonRun/DungeonRunAdmission.F20.cs` — six rows: `boss-tombs-b-board-power` (9), `-own-hand-power` (2), `-hidden-hand` (2), `-forced-attack` (1), `-passive` (4), and `tombs-b-collectible` (ULD_705 and ULD_705t, every scope)
  - `data/dungeon-run/reference/encounters/F20.json`
  - `verify/suites/p8f20/c-enc-20/` (Enc20.csproj, Program.cs)
  - `verify/tools/gen/p8f20/make_f20.py`, `patch_curation.py`
  - `verify/tools/gen/p8f19/tombs_tiers.py`: checked out from wip/f19, byte-identical
- **Shared edits:**
  - UldumCardsGen.cs: ULD_705 and ULD_705t (flag 1).
  - curation.json: E20 appended to `landedFamilies`; the generator's self-check needs it.
  - c-admission: one exemption statement for the `boss-tombs-b-*` and `tombs-b-collectible` rows.
  - c-content and c-enc-model J13: the four passives added to the passive lists.
  - c-enc-model A4: the family count string.
  - c-catalog: the ledger golden.
  - c-deckops: the T5 row count.
  - suites.sh: c-enc-20 appended to BUILD_LIST and RUN_LIST.
  - bp.sh: usage line, the A20/B20/CARDS20 variables, four case lines after family 10.
- **Generated:** encounters-v1.json, restoration-ledger.csv/.md, and curation.json's tool-owned lists.
- **Commits:** f5d0a82 and 09d44b4 (WIP saves), d7dabb0, d0c0e41 (final). I did not squash them.

## Pins moved
| Suite | Old → new | Cause |
|---|---|---|
| c-admission | 3167 → 3240; generated lists 426 → 444 | the new F20 rows |
| c-catalog ledger golden | 4704/2813/295/538 → 4730/2813/339/520 | +18 run:boss rows and 8 new deck cards (GVG_013, GVG_102, ICC_067, LOE_110, LOOT_315, OG_031, TRL_504, ULD_705); 18 powers move from missing to partial |
| c-deckops T5 | 4704 → 4730 | same rows |
| c-content | 904 (unchanged count) | passive list extended |
| c-enc-model | 141 (unchanged count) | A4 gains 20:18 and J13 the four passives |

c-enc-model has 12 differing lines against the baseline, all detail lines: E3 Hagatha as act II major 506→577, Icarax as act III major 1000→509, E4 no-ledger 96→49, G10 Voone act 1→2. These are the same moves family 19 saw from adding 18 ULDA rows. The F2 file lengths differ only because the baseline was recorded on Windows.

**Generators:** encounters "87 rows, 91 decks (46 authentic, 45 changed), 226 adaptations, 1 rejected"; catalog unchanged ("506 entries … 6508 admitted and 1 excluded"); ledger rows 4730, implemented 2813, partial 339, missing 520, duplicate/support 1058.

## Tests
- **c-enc-20:** 109 checks.
  - Sections A–R: one per boss, each through BuildPlan → DungeonRunSetup. They cover Mogu Cultist and Highkeeper Ra, shrine cases (Illidara, Sazin, Ruby, Ptah) and the fair view masking the run player's hand (Toomba, Sazin).
  - Section S: the rows, including the shipped level against the tier rule (the family 19 review's point); flags, Costs and client texts; admission; substitutions; the validator.
- **Bosspilot modes** (each run twice per call, identical, 0 faults / fallbacks / failed checks / swallowed, stderr empty, no baseline yet):

| Mode | Bosses | Games | Boss wins | Power-unused |
|---|---|---|---|---|
| enc20a | ULDA_BOSS_19-27 | 18 | 14 | 0 |
| enc20b | 28-36 | 18 | 16 | 0 |
| enc20-seeds-a | 19-27, SEED_BASE 20000 | 18 | 12 | 2 |
| enc20-seeds-b | 28-36, SEED_BASE 20000 | 18 | 15 | 0 |

In enc20-seeds-a the two unused powers are Thudd and Ol' Toomba:
- **Thudd:** both of his games were short boss wins, and Gatling Wand is only offered while the run player has a minion.
- **Ol' Toomba:** "Retired" Treasure Hunter copies nothing in the masked fair view, so the search undervalues it by design (no hidden information is read). It is still used in the base mode (4 times in one game).

Mogu Cultist was played 3 times as a watched card. Highkeeper Ra is summoned, not played, so the watch cannot count it; c-enc-20 covers it.

## Lane verification
- Engine: 0 warnings / 0 errors (md5 447fceaa999dd2d9083f338c283cc84a).
- gen.sh twice: all six files byte-identical, and equal to the commit.
- Suites, twice, run 1 = run 2: c-content 904, c-admission 3240, c-runlayer 244, c-acts 181, c-deckops 174, c-catalog 127, c-shrine-core 131/0, c-enc-model 141, c-enc-engine 122, c-enc-20 109.
- Against the baseline: 0 differing lines everywhere except c-admission (4) and c-enc-model (12), both explained above.
- Not run, per the brief: hall, full runs.sh, selfplay, randplay, released modes.

## Paragraph for docs/design/dungeon-run.md
"Family 20 (Tombs of Terror bosses B): eighteen Tombs encounters ULDA_BOSS_19–36, with Sothis's Mogu Cultist (ULD_705, a Saviors of Uldum stub now scripted: seven Cultists become Highkeeper Ra, ULD_705t). Tier and Health follow family 19's Tombs rule (tombs_tiers.py). There are fourteen clickable powers and four passives. Spectral Swordsman gives the boss's hero Stealth while it holds a weapon; Staking A Claim gives every minion +1/+1 whenever either player plays a Discover card; Mother of Sand summons a Dust Devil when the boss Overloads, resolved in place; Fireshaper is Flamewaker's trigger. "Retired" Treasure Hunter (copy a card from the run player's hand) and Mighty Windstorm (each player swaps a hand minion with a board minion, never a run shrine) read the run player's hand only as their text says. In the boss's fair view that hand is masked, so the search uses only cards the boss saw go there. Out of Control hits the center-most minions of each board; Vengeful Construct destroys a chosen minion with 2 or less Health and its neighbours, and a shrine goes dormant; Madness Incarnate makes the hero attack three different random enemy minions as forced attacks. Decks are the wiki's Normal lists, extended to 30 with the Heroic lists. Unscripted Uldum collectibles, and Hydrologist (which offers Secrets), are replaced by named scripted cards of the same role, each recorded."

## For the README / baseline
New run entry c-enc-20 "RESULT 109 passed, 0 failed"; c-admission 3240; ledger 4730/2813/339/520/1058; the four enc20 bp results above.

## For the integrator to redo after the merge
- Rerun gen.sh and recompute the pins together with families 19 and 21.
- Re-record the enc20 modes on the merged tree: seeds depend on row order.
- Rerun the encounters step once the replaced collectibles get scripts.
- Nothing was added to verify/MANIFEST.md.

---

# Family 20 review: confirmed findings (adversarially verified)

## F20.1 [low] engine/SabberStoneCore/src/CardSets/DungeonRun/DungeonRunEncounters.F20.cs:302
"Retired" Treasure Hunter (ULDA_BOSS_27p) makes its copy with Entity.FromCard(c, pick.Card), a new base card, so the copy loses the original's in-hand enchantments. The power is built on Mind Vision (its client tag 858 = 1099). On the wiki's Copy page a hand-to-hand copy keeps its enchantments ("eg. Mind Vision"), and the engine's own Mind Vision (CS2_003, via CopyTask/Generic.Copy) and Monster Hunt's Spyglass both keep them.

Scenario: The run player holds a minion buffed in hand (for example +2/+2 from a hand-buff effect) or a card made cheaper by a cost enchantment (Far Sight's -3). Ol' Toomba uses the power and picks that card. The boss gets the unbuffed or full-Cost version, but by the Copy rule (and by the engine's Mind Vision) it should get the buffed or discounted copy. The fix is Generic.Copy(c, s, pick, Zone.HAND) followed by MarkCopy.

Verifier: Confirmed. F20.cs:302 builds the copy with Entity.FromCard(c, pick.Card), a new base card with no tags, so in-hand enchantments, cost changes and DISPLAYED_CREATOR are all lost. The wiki Copy page says a hand-to-hand copy keeps its enchantments ("eg. Mind Vision"). The engine's own Mind Vision (CS2_003, RandomCardCopyToHandFrom) and Spyglass (DungeonRunMonsterHuntShared.cs:212-220) both go through Generic.Copy, which sets copyEnchantments for HAND->HAND (Copy.cs:39-40). No adaptation in F20.json or the implementer's report records the new-base-card choice.

Fix: In ULDA_BOSS_27p, replace the three lines `IPlayable copy = Entity.FromCard(c, pick.Card); PayloadRules.MarkCopy(c.Opponent, copy); Generic.AddHandPhase.Invoke(c, copy);` with `Generic.Copy(c, s, pick, Zone.HAND);`. Generic.Copy already calls PayloadRules.MarkCopy(source, copy) (Copy.cs:255), sets DISPLAYED_CREATOR and adds the card to the hand. Fix the 'as a fresh copy' comment to match.

## F20.2 [low] engine/SabberStoneCore/src/CardSets/DungeonRun/DungeonRunEncounters.F20.cs:186
Madness Incarnate's ForceHeroAttack calls Generic.AttackBlock directly and never undoes the option count that AttackPhase adds for every attack, forced ones included (Attack.cs:320, c.NumOptionsPlayedThisTurn++). The engine's other forced attacks undo it: EncounterRules.ForcedAttack (EncounterRules.F06.cs:47, whose header names families 11, 20, 22 and 24 as its users) and AttackTask.cs:47 both run NumOptionsPlayedThisTurn--. This file copied PastColossalCombat.Force instead, which runs inside a real attack and does not undo it.

Scenario: Kasa Softspeaker (ULDA_BOSS_36p) is used with four enemy minions on the board. The hero makes three forced attacks, so the boss controller's NUM_OPTIONS_PLAYED_THIS_TURN (tag 358) ends the action at 4 (1 for the hero power plus 3 for the attacks) instead of 1. Every other forced-attack path in the engine leaves it at 1. Nothing in the engine reads the tag today, so the visible effect is a wrong tag in the game state and history. Calling EncounterRules.ForcedAttack(hero, victim), the helper meant for this family, would fix it and also apply its CanFight checks.

Verifier: Confirmed. ForceHeroAttack (F20.cs:182-188) calls Generic.AttackBlock with forcedAttack=true. AttackPhase still runs c.NumOptionsPlayedThisTurn++ unconditionally (Attack.cs:320). Every other forced-attack path in the engine undoes it: EncounterRules.ForcedAttack (EncounterRules.F06.cs:47, whose header at line 17 names family 20 as a user) and AttackTask.cs:47. With three forced attacks, tag 358 ends at 4 instead of 1. Nothing in the engine or plugin reads the tag today, so the only effect is a wrong tag in the game state and history. Low severity, but concrete, and the shared helper exists for this purpose.

Fix: Make ForceHeroAttack call `EncounterRules.ForcedAttack(hero, target);` (it restores CurrentEventData, decrements NumOptionsPlayedThisTurn and applies the CanFight guards), or add `hero.Controller.NumOptionsPlayedThisTurn--;` after the AttackBlock call.

## F20.3 [low] verify/suites/p8f20/c-enc-20/Program.cs:468
Check L4 ('a minion played is no spell: nothing') cannot fail. The Croc reaches the board through the suite's Board() helper, which calls Generic.SummonBlock. No card is played or cast, so no PLAY_CARD or AFTER_CAST trigger can fire, whatever trigger type Fireshaper uses.

Scenario: Change Fireshaper's trigger (F20.cs, ULDA_BOSS_30p) from AFTER_CAST to a trigger that wrongly fires on any card the boss plays, such as PLAY_CARD or AFTER_PLAY_CARD. Board(g, boss, Croc) still only summons, Lost(player) stays 3 and L4 still passes. The case it names, the boss playing a minion, is never run. It needs Play(g, ToHand(g, boss, Croc)).

Verifier: Confirmed. Board() (Program.cs:113) only runs Generic.SummonBlock, so no PLAY_CARD, AFTER_PLAY_CARD or AFTER_CAST event fires and L4 holds whatever trigger type ULDA_BOSS_30p uses. No other check covers it either: L2 is the run player's spell (blocked by TriggerSource.FRIENDLY) and L3 is a boss spell, which fires under either trigger type. A regression of Fireshaper to PLAY_CARD would go unnoticed.

Fix: Change line 467 to `Play(g, ToHand(g, boss, Croc));` (the boss is the current player after ToBoss, and Play calls Mana first), and keep the assertion Lost(player) == 3.


I fixed all three review findings; c-enc-20 now passes 111 of 111. The fix commit is **37f5c9d** on wip/f20 (worktree clean, nothing pushed).

**The fixes:**
1. **F20.1, "Retired" Treasure Hunter (ULDA_BOSS_27p):** the copy is now made with `Generic.Copy(c, s, pick, Zone.HAND)`. It keeps the card's in-hand enchantments (Mind Vision's rule), is marked as a copy of an opponent's card, and records the power as its creator. New check **I7**: a run-player Yeti made to cost (1) less is copied at (3), with the enchantment and the power as creator. I also updated the power note in `make_f20.py`, so F20.json and the generated data changed.
2. **F20.2, Madness Incarnate (ULDA_BOSS_36p):** the hero's attacks now go through `EncounterRules.ForcedAttack`, which spends no option of the turn and applies the shared fight checks. New check **R2b**: the option count (`NUM_OPTIONS_PLAYED_THIS_TURN`) is unchanged by the three attacks; the old code added 3. One detail differs from the review: it expected the count to end at 1 for the hero power, but the hero power adds nothing to it in this engine. So R2b checks "unchanged", and the first version of the check (expecting +1) failed until I corrected it.
3. **F20.3, c-enc-20 L4:** the boss now plays Croc from its hand (`Play(g, ToHand(g, boss, Croc))`) and the check asserts no damage. A Fireshaper wrongly triggering on card plays would now fail it.

**Results:**

Generators, run twice: byte-identical. Counts unchanged:
- encounters "87 rows, 91 decks (46 authentic, 45 changed), 226 adaptations, 1 rejected"
- catalog unchanged
- ledger 4730 / 2813 / 339 / 520 / 1058

Engine build: 0 warnings, 0 errors; md5 d0011b4c21f689e34e6861e928920094.

| Run (through the lock) | Result |
|---|---|
| c-enc-20, twice | RESULT 111 passed, 0 failed; run 1 = run 2 |
| c-enc-model (spot check after the data change, run once) | 141 passed; the same 12 detail lines differ from the baseline as before |
| c-catalog (spot check, run once) | 127 passed; 0 differing lines vs baseline |

Bosspilot modes, each run twice: both runs identical, stderr empty, 0 faults, fallbacks, failed checks and swallowed leaf exceptions. The summary lines are the same as before the fixes; I did not compare the per-game lines.

| Mode | Games | Boss wins | Power-unused |
|---|---|---|---|
| enc20a | 18 | 14 | 0 |
| enc20b | 18 | 16 | 0 |
| enc20-seeds-a | 18 | 12 | 2 |
| enc20-seeds-b | 18 | 15 | 0 |

The 2 unused powers in enc20-seeds-a are Thudd and Ol' Toomba, the same as in my first report. Thudd's games were short boss wins, and his power is only offered while the run player has a minion. Ol' Toomba's power copies nothing in the boss's masked view of the run player's hand, so the AI undervalues it; it is used in the base mode.

**Numbers that change from my first report:**
- c-enc-20: 109 → 111 checks (I7 and R2b).
- Everything else, including the shared-suite pins, is unchanged.