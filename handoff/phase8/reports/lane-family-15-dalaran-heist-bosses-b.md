FAMILY 15 (Dalaran Heist bosses B) — REPORT. Branch wip/f15 in /home/user/wt/f15. Commits: 3384c63 (feat), 078a0dc (test). Nothing is pushed and the tree is clean. All 30 ids are done and none is rejected.

## FLAGS for the integrator
1. **No private tag used.** 32650 is still free. Cold Water stores the turn of its last freeze in the hero power's TAG_SCRIPT_DATA_NUM_1, a public client tag. The cast-by-rule count is family 09's AutoCast NUM_2.
2. **Shared edits (each minimal):**
   - `engine/SabberStoneCore/src/Actions/EncounterRules.cs`: one line at the top of `ResolveHeroForms` calls `ResolveUnderbellyRats(game)`. The Rat King's hero replacement needs the death hook; this is the same pattern as family 08's `TagTeamDeath`.
   - `plugin/DungeonRun/DungeonRunAdmission.cs`: `GILA_500p2t` (A Small Rock) is removed from the core `run-treasure` row and moved to an F15 `run-treasure-boss-find` row. Its legacy verdict stays player-only; catalog runs also admit it for a boss (Mama Diggs's Excavate). This is family 09's precedent.
   - `tools/restoration-ledger/curation.json`: a landedFamilies entry `E15` appended after E10. It will conflict with family 14's E14.
   - `verify/lib/suites.sh`: `p8f15/c-enc-15` appended to the end of BUILD_LIST and RUN_LIST. `verify/bin/bp.sh`: a usage line, a `CARDS15` variable and the enc15 case block after the family 10 lines.
   - Existing suites: see "Pins moved".
3. **No new admission family names.** I reused families 08/09's names (boss-auto-cast, boss-public-power, boss-passive-trigger, boss-hidden-reveal, boss-treasure-find, boss-card, run-treasure-boss-find), so c-admission needs no new exemption statement.
4. **No shared Heist-tier helper.** Tiers are data in F15.json, written by my own `verify/tools/gen/p8f15/make_f15.py`, so nothing clashes with family 14.
5. **Nothing forbidden was touched.** No schedule default, finale, act, pool set, save version, ReleasedShapes or golden sequence. Raw wiki pages stay in `verify/.out/tmp/f15/wiki` (gitignored).
6. **New rows change acts-v3h runs.**
   - c-enc-model's act I anomaly offer moved (G1: DALA_860,DALA_856 → DALA_858,ULDA_710). Vas'no's `recast` flag and the new boss draws change which rules are compatible.
   - Expect every acts-v3h bosspilot mode of other families to move, and randplay's regkeys and fingerprint (+19 registered cards). I did not run those, as the brief says.
7. **Judgement calls**, each recorded as an adaptation in F15.json:
   - **Rat King revival Health.** A revived Rat King has the rat's Health, which is 1 for the printed 10/1 rat. In the Heist's Chapter 4 the twist swapped stats, so there the rat was a 1/10.
   - **Wendy and Disidra are cast by rule.** The wiki marks both powers auto-cast; they use family 09's AutoCast.
   - **Scroll Savvy covers both players.** Its text names no player, per the review's literal-scope lesson.
   - **Vas'no's recast copy owes Overload too.** The engine's rule for a spell cast by an effect applies.
   - **Jepetto is tier 4.** He is a chapter final, and the mode has no major slot for Heist chapter finals.
   - **A rat on the run player's side keeps that hero entity.** The hero takes the rat's Health.

## Per id
All done.
- **Heroes:** DALA_BOSS_16h–20h and 22h–30h are rows only; they need no script.
- **16p Equivalent Exchange:** cast by rule at the start of the boss's turn while both hands hold a card. One random card of each hand changes sides by GILA_906's hand-transfer rule (CardTransferredFacts; both leave first, so a full hand burns nothing).
- **17p Twin Paths:** Fandral's CHOOSE_BOTH controller aura. No new engine hook was needed: family 03's ULD_131p precedent already does this.
- **18p Totemic Summons:** cast by rule; a random Totem of any class, the four basic Totems included.
- **19p Lil' Eviscerate:** 1 damage, 2 with Combo.
- **20p Stormswell:** an Overload spell is recast on the same target through RecastRules.
- **22p Shhh!:** silences all minions on both boards.
- **23p Sharkbite:** destroys a random damaged minion; a run shrine picked goes dormant.
- **24p Ar'ha's Call:** player enchantment 24e adds one EXTRA_BATTLECRIES_ADDITIONAL until the next Battlecry minion, then removes itself.
- **25p Assembly:** passive for both players; the drawer of a minion summons a 1/1 Dorian-style copy (25e).
- **25t Hoard of Toys:** five random 1/1 minions.
- **26p Cold Water:** freezes the first minion each turn that attacks the boss's hero.
- **27p Masterpiece!:** spends all remaining Mana and summons a random minion of that Cost.
- **28p Scroll Savvy:** a drawn spell costs (0) that turn (28e).
- **29p Excavate:** 2 damage to a friendly Elemental, then one of the wiki's six listed results with equal odds (the wiki gives none): nothing, A Small Rock, 1–2 draws, a random Shaman card, a copy of the damaged minion, or one of Togwaggle's 16 treasures.
- **30p A Tale of Kings and 30t Underbelly Rat:** when a hero dies, its controller's eldest living rat leaves the board without dying. For the boss, a new Rat King enters through the row's script form (`SwapForm`) with the rat's Health, as many times as there are rats.

## Tiers and decks (wiki pages read 2026-11-14)
| Tier | Bosses | Level source |
|---|---|---|
| 1 | Wendy, Linzi | 10/15-card pools (levels 1–2) |
| 2 | Bae Chao, Mama Diggs | 20/25-card pools (level 3) |
| 2 | Sharky | 25-card pool (level 4) |
| 3 | Tala, Disidra, Fountain Golem, Xur'ios | page: "level 6 in Chapter 5" |
| 3 | Ar'ha | page: "levels 6-7" |
| 3 | Vas'no, Rasil, Rat King | 30-card tables, so level 6 or higher |
| 4 | Jepetto | Chapter 3 final, level 8 |

- **Decks:** 8 are the wiki's Normal list unchanged. 5 pools are extended to 30 (Wendy's with four curated Kabal/Mage fillers). Bae Chao loses Fel Reaver (unscripted). Xur'ios is substituted: Wee Spellstopper → Water Elemental (unscripted) and Glacial Mysteries → Flamestrike (not boss-admitted).

## Files
- **Own:**
  - `engine/SabberStoneCore/src/CardSets/DungeonRun/DungeonRunEncounters.F15.cs`
  - `engine/SabberStoneCore/src/Actions/EncounterRules.F15.cs`
  - `plugin/DungeonRun/DungeonRunAdmission.F15.cs`
  - `data/dungeon-run/reference/encounters/F15.json`
  - `verify/tools/gen/p8f15/make_f15.py`
  - `verify/suites/p8f15/c-enc-15/` (Enc15.csproj, Program.cs)
- **Shared:** as in the flags.
- **Regenerated:** encounters-v1.json, the curation.json tool lists, restoration-ledger.csv/.md. catalog-v1.json is byte-identical.

## Pins moved (suite, old → new, cause)
| Suite | Old → new | Cause |
|---|---|---|
| c-admission result | 3167 → 3235 | new rows |
| c-admission generated lists | 426 → 442 | 14 powers + 2 cards |
| c-admission catalog-lift check | 5 rows / 2 treasure rows / 15 ids → 6 / 3 / 16 | A Small Rock's row |
| c-content | passive list +7 (16p, 17p, 18p, 20p, 25p, 26p, 28p); still 904 | new passive powers |
| c-deckops T5 | 4704 → 4733 ledger rows | +29 rows |
| c-catalog ledger golden | 4704/2813/295/538 → 4733/2813/340/522 | +29 rows all partial (14 run:boss, 15 deck cards); 16 rows missing → partial |
| c-enc-model A4 | family counts + "15:14" | new rows |
| c-enc-model J13 | passive list +7 | new passive powers |
| c-enc-model E3 | rewritten (still 141 checks) | Whirt is no longer met in every run (103/1000) |
| c-treasures-F3 T2 | GILA_500p2t may be boss-admitted on catalog runs; still 283, log identical to baseline | Excavate |

- c-enc-model E3 now asserts that every run meets a Heist row and Voone and that Whirt appears in some; Hagatha is the act II major in 557 of 1000 runs. Family 14 will move this again.
- c-enc-model also prints different save lengths in F2 (14306 → 13954, 26370 → 25744). That is Linux line endings (the fixture has 352 lines, LF), not this family.

## Generators (gen.sh twice, all six files byte-identical)
- **Encounters:** 83 rows, 87 decks (51 authentic, 36 changed), 190 adaptations, 1 rejected.
- **Catalog:** 506 entries, 6508 admitted, 1 excluded (unchanged).
- **Ledger:** 4733 rows; implemented 2813, partial 340, missing 522, duplicate/support 1058, adapted 0.

## Tests added
- **c-enc-15:** 98 checks, 0 failed, 0 swallowed. Sections A–N cover each boss, including shrine/permanent cases, a clone and the fair view; section P covers rows, admission and the validator.
- **Bosspilot modes** (all run twice, identical; 0 faults / fallbacks / failed checks / swallowed; stderr 0; power-unused 0):

| Mode | Games | Boss wins | Notes |
|---|---|---|---|
| enc15 (FAMILY=15) | 28 | 17 | Hoard x3 |
| enc15-seeds (SEED_BASE=19000) | 28 | 20 | Hoard x1 |
| enc15-rat (Rat King x8) | 8 | 5 | form-unreached 0 |
| enc15-toys (Jepetto x8) | 8 | 7 | Hoard of Toys played x8 |

- form-unreached 1 in enc15 and enc15-seeds is the Rat King, who won his games there; enc15-rat reaches the form.
- The seven passive or cast-by-rule powers are not counted by the harness; c-enc-15 proves each of them.

## Lane verification
- **Engine:** 0 warnings / 0 errors (Linux md5 be102007998786e19420ea951ad5dd20).
- **Suites** (each twice, run 1 = run 2, 0 differing lines): c-content 904, c-admission 3235, c-runlayer 244, c-acts 181, c-deckops 174, c-catalog 127, c-shrine-core 131, c-enc-model 141, c-enc-engine 122, c-enc-15 98, c-treasures-F3 283.
- **Against the baseline logs:** 0 differing lines except c-admission (4, its count lines) and c-enc-model (12: A4, E2/E3, the F2 line endings, G1).
- **Also run once, all at baseline:** c-enc-07 161, c-enc-08 113, c-enc-09 166, c-enc-10 116, c-rules-05 170, c-rules-06 224, c-enc-secrets 60, c-w3fix 4, c-treasures-F2 321, c-treasures-F12 321.

## Paragraph for docs/design/dungeon-run.md
"Family 15 (Dalaran Heist bosses B): fourteen Heist encounters. The tier is the boss's wiki level: the page's own statement, else the size of its deck table (10/15/20/25/30 cards for levels 1/2/3/4-5/6+), because the hero cards print 30 Health. Equivalent Exchange and Totemic Summons are cast by rule (the wiki's auto-cast). Equivalent Exchange moves one random card each way by the hand-transfer rule, so each side knows the card it gave. Twin Paths uses Fandral's CHOOSE_BOTH aura. Stormswell recasts Overload spells on the same target, and the copy overloads too. Ar'ha's Call adds one Battlecry resolution to the next Battlecry minion. Assembly gives either drawer a 1/1 copy of a drawn minion. Scroll Savvy makes any drawn spell cost (0) that turn, because its text names no player. Excavate implements the wiki's six listed results with equal odds; A Small Rock is now also admitted for a boss on catalog runs. The Underbelly Rat replaces a dead hero (Actions/EncounterRules.F15.cs, called first in ResolveHeroForms): the eldest living rat leaves the board without dying, and the hero returns with its Health. For The Rat King that is a new hero through his row's script form."

## For the integrator after the merge
- Regenerate (gen.sh). Merge the E14/E15 entries in curation.json.
- Reconcile with family 14 (also Heist) in c-enc-model A4/E3/J13, the c-content passive list and the c-admission catalog-lift counts. Then recompute the ledger pins (c-deckops T5, c-catalog golden).
- Re-record acts-v3h bosspilot baselines and randplay regkeys.
- README numbers: c-enc-15 "RESULT 98 passed, 0 failed"; enc15 28/17, enc15-seeds 28/20, enc15-rat 8/5, enc15-toys 8/7.


---

# Family 15 review: confirmed findings (adversarially verified)

## F15.1 [medium] engine/SabberStoneCore/src/CardSets/DungeonRun/DungeonRunEncounters.F15.cs:259
Ar'ha's Call ("Your next Battlecry triggers an additional time") adds EXTRA_BATTLECRIES_ADDITIONAL through a controller aura, which the engine also applies to weapon and hero-card Battlecries (PlayCard.cs:731-733 and 327). But the enchantment only removes itself on AFTER_PLAY_MINION with IsBattlecryMinion, so a weapon Battlecry gets the extra resolution and does not use up the enchantment.

Scenario: Ar'ha controls a Beast (Alleycat) and uses Ar'ha's Call, so DALA_BOSS_24e is attached. She then plays Headhunter's Hatchet, which is in her own wiki deck. Its Battlecry resolves twice (+2 Durability), but the enchantment stays because no minion was played. Her next Battlecry minion, for example Crackling Razormaw, also resolves twice. One use of the power doubles two Battlecries, where the card text says only the next one.

Verifier: Confirmed. The DALA_BOSS_24e aura adds EXTRA_BATTLECRIES_ADDITIONAL to the controller, and PlayCard.cs:731-733 applies BattlecryRules.Additional(c) to weapons whose card has BATTLECRY == 1 (it does the same for hero cards at 327-329). The enchantment's only removal trigger is AFTER_PLAY_MINION with SelfCondition.IsBattlecryMinion (DungeonRunEncounters.F15.cs:259-264), so a weapon play never removes it. Headhunter's Hatchet (TRL_111, a Battlecry weapon) is in Ar'ha's own list in F15.json, so Ar'ha's Call -> Hatchet (Battlecry twice) -> Crackling Razormaw (Battlecry twice) can happen: one use doubles two Battlecries. The weapon path does fire OnAfterPlayCardTrigger (PlayCard.cs:763), so a fix is available.

Fix: Remove the enchantment on the next card that has a Battlecry of any type. Change the trigger to `new Trigger(TriggerType.AFTER_PLAY_CARD) { TriggerSource = TriggerSource.FRIENDLY, Condition = new SelfCondition(p => p is Minion m ? m.HasBattleCry : (p is Weapon || p is Hero) && p.Card[GameTag.BATTLECRY] == 1), SingleTask = RemoveEnchantmentTask.Task }`, matching the cards that PlayCard.cs feeds BattlecryRules.Additional to.

## F15.2 [medium] engine/SabberStoneCore/src/Actions/EncounterRules.F15.cs:26
EldestUnderbellyRat counts a Silenced Underbelly Rat. Its "When your hero dies, the eldest Underbelly Rat replaces your hero" is the rat's own card text, so Silence should remove it. The filter checks ToBeDestroyed, Health and IsDormant, but not IsSilenced.

Scenario: The Rat King uses A Tale of Kings and has one Underbelly Rat on the board. The run player Silences it (Ironbeak Owl or Spellbreaker), then deals lethal damage to the Rat King. ResolveUnderbellyRats still finds the silenced rat, removes it, and brings back a new Rat King through SwapForm. The fight continues when the boss should have died.

Verifier: The replacement is the Underbelly Rat's own card text: the client text is on DALA_BOSS_30t, and the wiki tags it Triggered effect. Under Hearthstone rules, Silence removes a minion's card text. The engine's own Shhh! power in this same family silences minions. EldestUnderbellyRat (EncounterRules.F15.cs:26) filters on ToBeDestroyed, Health and IsDormant but not IsSilenced, and nothing in F15.json, the report or the code records keeping the ability after Silence as an adaptation. So a Silenced rat still brings back a new Rat King when the boss takes lethal damage.

Fix: In EldestUnderbellyRat, add `&& !m.IsSilenced` to the Where filter. Also add a suite check: Silence the only rat, kill the boss hero, and assert that the game ends.

## F15.3 [low] engine/SabberStoneCore/src/Actions/EncounterRules.F15.cs:24
EldestUnderbellyRat picks the rat with the lowest entity id (OrderBy(m => m.Id)), not the one that entered play first. The engine tracks play order separately: BoardZone.cs:130 sets entity.OrderOfPlay = Game.NextOop on every board entry. So a rat that was bounced and replayed counts as the eldest even though it is the youngest on the board.

Scenario: Rat King encounter. The boss summons rat A (id 50) on turn 3 and rat B (id 60) on turn 5. The player Saps A, which returns the same entity to the boss's hand, and the boss replays A on turn 7. The board order is now B (entered turn 5), then A (entered turn 7). When the boss's hero dies, ResolveUnderbellyRats takes A because of its lower id, although B is the eldest. B stays on the board and A leaves, which breaks the card text "the eldest Underbelly Rat replaces your hero". OrderBy(m => m.OrderOfPlay) would pick B.

Verifier: Confirmed. BoardZone.Add (BoardZone.cs:130) sets OrderOfPlay = Game.NextOop on every board entry, and other engine scripts that pick the first or eldest minion by entry order use OrderBy(m => m.OrderOfPlay) (BlackwaterBehemoth, MythicalTerror, TwilightMistress). EldestUnderbellyRat uses OrderBy(m => m.Id), so a rat that is bounced (Sap or Shadowstep keeps the same entity) and replayed, or one taken by Mind Control, still counts as the eldest even though it re-entered play last. The scenario (Sap rat A, the boss replays it, the boss dies) makes A replace the hero instead of B, which goes against "the eldest Underbelly Rat".

Fix: Use `.OrderBy(m => m.OrderOfPlay).ThenBy(m => m.Id)` in EldestUnderbellyRat, and update the comment, which says the eldest is the lowest entity id.

## F15.4 [low] verify/suites/p8f15/c-enc-15/Program.cs:262
Check E4 ("when the first cast kills its target, the copy has no target: it does nothing") cannot fail on the behaviour it names. It only asserts that the Crocolisk is dead or off the board, which the first 3-damage Lightning Bolt guarantees on a 2/3 either way. `health = player.Hero.Health` is captured after the cast (line 261) and never compared.

Scenario: Suppose a regression in Stormswell or RecastRules made the copy re-aim at a random character, or at the run player's hero, once the locked target is dead. The copy would then deal 3 more damage somewhere, and E4 would still pass because the croc is dead from the first cast. Nothing checks the copy's effect, such as the player hero's Health being unchanged or the boss's OverloadOwed.

Verifier: Confirmed (Program.cs:258-262). `health = player.Hero.Health` is assigned after `Cast(g, Bolt, croc)` and is never used in E4's condition. E4 only asserts that the croc is dead or off the board, which the first 3-damage Bolt guarantees on a 2/3 either way. If a regression made the copy re-aim at another character, E4 would still pass, so it cannot fail on the behaviour it names (the copy does nothing).

Fix: Capture state before the cast and compare after: `int ph = player.Hero.Health, bh = boss.Hero.Health, owed = boss.OverloadOwed; Cast(g, Bolt, croc); Check("E4 ...", (croc.IsDead || croc.Zone != player.BoardZone) && player.Hero.Health == ph && boss.Hero.Health == bh, ...)`. Add an OverloadOwed assertion for whatever the intended rule is for an untargeted copy.

## F15.5 [low] verify/suites/p8f15/c-enc-15/Program.cs:167
The fair-view masking check for Equivalent Exchange (A7) asserts only `unknownBefore > 0`. At that point the run player's hand holds 9 hidden Yetis and 1 Moonfire the boss gave away (known). The check should require that every card except the transferred one is still an Unknown stand-in. Design section 6.2 files Wendy under boss-hidden-steal with "test asserts the fair view's other hidden cards stay unknown". The row instead sits in DungeonRunAdmission.F15.cs under boss-auto-cast, and no check makes that assertion. A3 only re-checks the boss's own hand and the hand count, which a fair view always gets right.

Scenario: Suppose a change to the transfer facts made the boss's fair view reveal the run player's hand (for example, treating every card of a hand involved in a CardTransferredFact as known). The fair view would then expose 8 of the 9 hidden Yetis to the boss's search. A7 would still pass with unknownBefore == 1, and A3 would also pass, so the hidden-information leak this family's design asks the suite to guard against would go unnoticed.

Verifier: Confirmed. Design.md:153 says that for the boss-hidden-steal family, Wendy included, the "test asserts the fair view's other hidden cards stay unknown". The suite's only masking assertion is `unknownBefore > 0` (Program.cs:175-179), at a point where the run player holds 9 Yetis and 1 transferred Moonfire. A fair view that leaked 8 of the 9 Yetis would still pass A7, and A3 checks only the boss's own hand and the hand count. So the guard the design requires is missing, and a hidden-information leak into the boss's search would go undetected.

Fix: In A7, require every non-transferred card to stay masked. For example, assert `unknownBefore == player.HandZone.Count(p => p.Card.Id == Yeti)` (9 here), or equivalently `viewPlayer2.HandZone.Where(p => !(p is Unknown)).All(p => p.Card.Id == Moonfire)`. Make the same assertion after the A1 exchange: every card in the player's view hand other than the Moonfire it received is Unknown.


FAMILY 15 — REVIEW FIX REPORT. All five findings were right, and all five are fixed on wip/f15 in commit 9f3cf38. The earlier commits 3384c63 and 078a0dc are unchanged. The tree is clean, nothing is pushed, and none of my processes is running.

## Fixes
1. **F15.1 [medium], Ar'ha's Call (`DungeonRunEncounters.F15.cs`).** DALA_BOSS_24e is now used up by the next friendly card with a Battlecry of any type: a minion with a Battlecry, or a weapon or hero card whose card has BATTLECRY. The trigger is AFTER_PLAY_CARD, the same set of cards PlayCard feeds to `BattlecryRules.Additional`.
   - New checks H6 and H7: with a Beast in play, Headhunter's Hatchet after the power resolves its Battlecry twice (4 Durability) and removes the enchantment; the next Novice Engineer then draws only 1.
2. **F15.2 [medium], a Silenced rat (`EncounterRules.F15.cs`).** `EldestUnderbellyRat` now skips Silenced rats, since the replacement is the rat's own text.
   - New check N9: after Ironbeak Owl silences the only rat, a lethal Moonfire ends the game and the boss loses.
3. **F15.3 [low], which rat is eldest (`EncounterRules.F15.cs`).** The eldest is now the rat that entered play first (`OrderBy(OrderOfPlay).ThenBy(Id)`), and the comment says so.
   - New check N10: rat A is Sapped and replayed, so it is younger than rat B. B replaces the hero and A stays on the board, even though A has the lower entity id.
4. **F15.4 [low], check E4.** E4 now records both heroes' Health and a bystander minion on each side before the cast. It asserts that the recast copy hits nothing once the Crocolisk is dead.
5. **F15.5 [low], fair-view masking.**
   - A7 now requires that every card of the run player's hand except the ones the boss gave is an Unknown stand-in. The run shows 9 Unknown, with only CS2_008 shown.
   - New check A3b, after the A1 exchange: the run player draws three Yetis, and the boss's view shows them as three Unknown stand-ins with only the given Moonfire known.

All four new behaviour checks (H6, H7, N9, N10) fail on the old engine and pass on the fixed one. I confirmed this by rebuilding the engine with the three engine fixes reverted (99 passed, 4 failed), then restored them; the engine md5 matches the fixed build.

One more change: G3's detail string is now sorted. Its HashSet order differed between two runs (the check itself passed both times), so run 1 and run 2 were not byte-identical before.

## Data
- The rat and Ar'ha's Call adaptation notes are updated in `make_f15.py` and F15.json, and the matching reasons in `DungeonRunAdmission.F15.cs`.
- Regenerated: encounters-v1.json, the tool-written lists in curation.json, and restoration-ledger.md. gen.sh runs twice byte-identical.
- The generator counts are unchanged: encounters 83 rows, 87 decks (51 authentic, 36 changed), 190 adaptations, 1 rejected; catalog 506 entries, 6508 admitted, 1 excluded; ledger 4733 rows (implemented 2813, partial 340, missing 522, duplicate/support 1058).
- No new shared-file edits and no tag; the shared edits from the first report are unchanged.

## New numbers (all through the lock)
- **Engine:** 0 warnings / 0 errors, md5 19743203085f364dae2368e64744d2b9.
- **Suites, each run twice with identical logs:**

| Suite | Result |
|---|---|
| c-enc-15 | RESULT 103 passed, 0 failed, 0 swallowed (was 98) |
| c-admission | 3235 |
| c-content | 904 |
| c-enc-model | 141 |

- **Bosspilot modes, each twice identical**, with 0 faults, 0 fallbacks, 0 failed checks, 0 swallowed leaf exceptions, stderr 0 and power-unused 0. These are the same numbers as before the fixes:

| Mode | Games | Boss wins | Notes |
|---|---|---|---|
| enc15 | 28 | 17 | Hoard of Toys played x3; form-unreached 1 is the Rat King, who won both games |
| enc15-seeds | 28 | 20 | Hoard x1; form-unreached 1 for the same reason |
| enc15-rat | 8 | 5 | form-unreached 0 (the rat replacement happens) |
| enc15-toys | 8 | 7 | Hoard x8 |

## For the README
c-enc-15 is now "RESULT 103 passed, 0 failed". The bosspilot numbers are unchanged.