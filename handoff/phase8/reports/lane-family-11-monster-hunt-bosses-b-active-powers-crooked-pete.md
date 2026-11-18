FAMILY 11 REPORT: Monster Hunt bosses B (active powers, Crooked Pete form change)
Worktree /home/user/wt/f11, branch wip/f11. Commits: a55943c (the family), ccafab8 (mirror-deck Fabled fix and shrine-mode order). The tree is clean, nothing was pushed, and no process is still running.

## FLAGS for the integrator (read these first)
1. **Tag.** I used only my reserved tag, 32646 (`EncounterRules.PowerRefresh`, for Desecrate's "refresh this"). I did not touch the comment listing taken tags in EncounterRules.cs; please add 32646 to it when you merge.
2. **Crooked Pete follows the wiki, not the design's "onDeath".** The wiki's Notes say: at the start of his turn, at half Health or less (25 of 50), Pete becomes Beastly Pete. His hand is removed, Beastly Pete's deck replaces his, he draws 6 and gains 15 Armor. The Monster Hunt table says "At 1/2 Health".
   - Implemented as a **script form** (`at {script:true}`). Soot Up's start-of-turn trigger fires it when Health×2 ≤ max Health, through `EncounterRules.SwapForm`.
   - It uses the form's deck and draw 6, which records the DeckReplaced fact. He keeps his Health and the act curve's maximum, not the printed 50.
   - A Pete killed outright before that turn start dies, as in the original. This is the first form change inside a match.
3. **Face Collector plays a copy of the run player's deck.** The wiki says "This boss plays a deck identical to the player's". This needed two shared edits:
   - a new flag `mirrorDeck` in the closed flag list (`DungeonRunData.cs`);
   - 14 lines in `DungeonRun.ApplyEncounter`. The copy is the plan's PlayerDeck, filtered by Boss admission. A Fabled bundle the filter would split is left out whole. With fewer than 10 cards left, the row's curated 30-card deck is used.
4. **Engine defect reproduced, not fixed (same root as open issue 3.4).** An Echo card at 0 cost loops forever. Command: `BOSS=<28× gila_44_sezavo>,gila_62_gustave GAMES=1`.
   - In that game the run player (TRLA_209h_Mage / rumble_mage_108, local bot) plays Sound the Bells! (GIL_145, Echo) at 0 cost until the harness stops with "stuck after 1500 actions" (turn 31).
   - Family 11 has nothing to do with it; the engine has no general "Echo costs at least (1)" floor.
   - I reordered `SHR11` in bp.sh (Garrow meets the Mage row, Gustave the Druid row). With that order enc11-shrine has 0 faults. The original order still faults; that is the repro.
5. **Grave Mistake the spell (GILA_BOSS_43t, 0 Cost) is in no deck.** It is the hunt's post-fight treasure and the wiki doesn't list it in Damph's deck. It is scripted and admitted (Player|Boss) and tested from a fixture boss's hand. Its ledger row stays partial, blocked_by `run:encounter-roster`.
6. **Judgement calls, each recorded as an adaptation in F11.json:**
   - Sezavo's printed 20 Health → tier 1.
   - Client costs used where the wiki table differs: Beast Within 1 (wiki 2), Terrify 4 (wiki 3).
   - Gustave's Cull the Meek is auto-cast in the wiki; here the AI chooses, as Ixlid and Sazzmi.
   - Wiki "Spore Hallucination" = UNG_856 (the client still calls it Hallucination, dbf 42011).
   - Witchwood's Touch → Shield Block, as in family 10. Three wrong-target Ogres → Grubb's replacements.
7. **c-enc-model F2 save lengths differ from the Windows baseline log** (13954/25744 vs 14306/26370). This is a line-ending difference: your own Linux run in SoloStone-wip shows the same numbers. Not mine.
8. **Not checked in the real client:** Pete's hero swap on the native transport (the AddHeroAndPower path, same as family 01).
9. **MANIFEST.md not edited.** It needs entries for the new suite, fixture and three generators.

## Per id (all done, none rejected)
- **Heroes** 40h, 41h, 42h, 43h, 44h, 51h, 52h, 54h, 55h, 56h, 57h, 58h, 62h: rows only.
- **GILA_BOSS_52h2** (Beastly Pete): form hero, a ledger support record.
- **Powers:**
  - 40p Grafted Bark: a 2/2 copy that costs (2) goes to the boss's hand. The generation filter decides which minions are choices; needs hand room.
  - 41p Survival of the Fittest: every minion, in play order, attacks a random enemy minion (new `EncounterRules.AllAttackRandomEnemyMinions`, built on family 06's ForcedAttack).
  - 42p Bulldoze: spends all Armor as one hit on a random minion.
  - 43p Grave Mistake: every minion's Deathrattles twice, for its own controller.
  - 44p Hollow Needle: 1 damage, heal 3 if the minion dies.
  - 51p Death and Taxes: one hit of N, where N counts the enemy's minions (dormant forms not counted).
  - 52p Soot Up: Stealth until his next turn, plus the Pete trigger.
  - 52p2 Beast Within: friendly minions +1 Attack.
  - 54p One with the Trees: minions in hand cost (1) less.
  - 55p Unfinished Business: three 1/1 Wisps.
  - 56p A New Face: transform into a random scripted minion costing (2) more; a shrine goes dormant instead.
  - 57p Desecrate: 1 damage to all minions; readied again when one dies (Inspire trigger + tag 32646).
  - 58p Terrify: shuffle into the owner's deck via MoveToDeck, Costs (4) less there. A shrine goes dormant; a full deck is no choice.
  - 62p Cull the Meek: destroy a lowest-Attack minion, random tie-break.
- **Boss cards and token:**
  - 41t Hack: five hits of 1 with Spell Damage.
  - 43t Grave Mistake (spell).
  - 55t Wisp (token).
  - 55t2 Soul Assimilation: destroys the caster's Wisps (Divination's Wisp list), then steals one random enemy minion per Wisp, never a shrine.
  - 57t Shallow Graves: up to 7 deaths from both graveyards, never `ShrineRules.IsCorpse`, never a card the generation filter blocks.
- **Enchantments:** 40e, 52e, 52e2, 54e, 58e.
- **Rows (F11.json, 13; wiki pages read 2026-11-14):**
  - Tiers: t1 Sezavo; t2 Baran; t3 Splintergraft, Damph, Garrow, Pete, Cragtorr, Grum; t4 Brushwood, Azalina, Face Collector, Raeth, Gustave.
  - Decks: 5 authentic (Splintergraft, Brushwood, Baran, Garrow, Raeth), 1 authentic with its form deck substituted (Pete), 2 extended (Damph, Sezavo), 4 substituted (Cragtorr, Azalina, Grum, Gustave), 1 curated (Face Collector's fallback).
  - Flags: forcedAttack (Brushwood), doubleDeathrattle (Damph), forms + deckReplace (Pete), mirrorDeck (Face Collector).

## Files
**Own:**
- `engine/SabberStoneCore/src/CardSets/DungeonRun/DungeonRunEncounters.F11.cs`
- `engine/SabberStoneCore/src/Actions/EncounterRules.F11.cs` (new)
- `plugin/DungeonRun/DungeonRunAdmission.F11.cs`: five rows:

  | Family | Ids | Scope |
  |---|---|---|
  | boss-hunt-power | 10 | Boss |
  | boss-hunt-own-hand | 2 | Boss |
  | boss-hunt-form | 1 | Boss |
  | boss-hunt-deck-shuffle | 1 | Boss |
  | boss-hunt-card-b | 5 | Player\|Boss, never generated |

- `data/dungeon-run/reference/encounters/F11.json`
- `verify/suites/p8f11/c-enc-11/`
- `verify/fixtures/p8f11/fixtures/encounters-f11.json`
- `verify/tools/gen/p8f11/` (make_f11.py, make_fixture.py, patch_curation.py)

**Shared, each minimal:**
- `EncounterRules.cs`: one constant, `PowerRefresh` 32646.
- `DungeonRunData.cs`: flag `mirrorDeck` and its constant (Face Collector).
- `DungeonRun.cs`: the mirror-deck block in ApplyEncounter (Face Collector).
- `tools/restoration-ledger/curation.json`: landed family E11 appended after E10 by patch_curation.py (32 ids, supportRecords GILA_BOSS_52h2). The tool-owned lists are regenerated.
- `verify/lib/suites.sh`: c-enc-11 appended to BUILD_LIST and RUN_LIST.
- `verify/bin/bp.sh`: usage lines, family 11 variables, modes enc11a, enc11b, enc11-pete, enc11-cards, enc11-shrine.
- Existing suites, pins only:
  - c-admission: one exemption statement for the five F11 families.
  - c-catalog: ledger golden.
  - c-deckops: T5.
  - c-enc-model: A4.

**Generated (regenerated, committed):** encounters-v1.json, curation.json lists, restoration-ledger.csv / .md. catalog-v1.json and dungeon-run-catalog.md are unchanged.

## Pins moved (suite, old → new, cause)
- c-admission: result 3167 → 3236; NOTE "Generated lists" 426 → 445 for both sides (boss-only 311 unchanged). Cause: the F11 rows.
- c-catalog ledger golden: 4704 / 2813 / 295 / 538 → 4723 / 2813 / 333 / 519. Cause: +19 rows (13 run:boss, 6 deck cards) and 19 missing → partial (14 powers, 5 cards). Still 127 checks.
- c-deckops T5: 4704 → 4723 rows. Still 174 checks.
- c-enc-model A4: family counts gain "11:13". Still 141 checks; E3 unchanged.

## Generator lines (twice, all six files byte-identical)
- encounters: 82 rows, 87 decks (49 authentic, 38 changed), 192 adaptations, 1 rejected
- catalog: 506 entries (452 offerable pool, 2 withdrawn, 52 fixed), 6508 admitted and 1 excluded (unchanged)
- ledger: rows 4723, implemented 2813, partial 333, missing 519, duplicate/support 1058, adapted 0

## Tests added and registered
**c-enc-11:** 109 checks, real engine games through BuildPlan → DungeonRunSetup. It covers every power, card, token and enchantment, including:
- shrine cases for Garrow, Azalina, Face Collector, Raeth, Grum and Gustave;
- the Pete form: trigger, hand set aside, deck replaced, draw 6, +15 Armor, Health kept, own-deck ledger sample, single change, harvest;
- the mirror deck: copy, admission filter, fallback, Fabled whole/split, real game;
- Desecrate refresh and clone; Shallow Graves never summoning a shrine corpse; Terrify into both decks with the own-deck ledger;
- rows, admission and validator.

**Bosspilot modes.** Each was run twice and the two runs are identical, with 0 faults, fallbacks, failed checks and swallowed leaf exceptions, and empty stderr:

| Mode | Games | Boss wins | Power-unused | Boss cards played from hand / notes |
|---|---|---|---|---|
| enc11a (7 rows ×4) | 28 | 15 | 0 | form-unreached 0 (shipped Pete transformed); Hack ×4 |
| enc11b (6 rows ×4) | 24 | 18 | 0 | Soul Assimilation ×2, Shallow Graves ×11, most boss Secrets 2 |
| enc11-pete (fixture fix11_pete, 16 Health, ×6) | 6 | 1 | 0 | form-unreached 0 |
| enc11-cards (4 fixture rows ×6) | 24 | 21 | 0 | Hack ×15, Grave Mistake ×12, Soul Assimilation ×15, Shallow Graves ×15: all four played |
| enc11-shrine | 37 | 28 | 1 | 9 of 9 shrine-row fights; power-unused is the padding row gila_44_sezavo in one of its 28 padding games |

## Lane verification (final binaries)
- **Engine:** 0 warnings / 0 errors (Linux md5 d133100cbc6e6c565093337802bdd79e). Generators twice: byte-identical.
- **Suites, run twice, each run 1 = run 2:** c-content 904, c-admission 3236, c-runlayer 244, c-acts 181, c-deckops 174, c-catalog 127, c-shrine-core 131, c-enc-model 141, c-enc-engine 122, c-enc-11 109. All 0 failed.
- **Differences from the baseline logs:** c-admission 2 lines (result and NOTE); c-enc-model 3 lines (the A4 label, plus the two F2 Linux lengths from flag 7); all others 0.
- **Extra, run once:** c-enc-07 161, c-enc-08 113, c-enc-09 166, c-enc-10 116, each 0 lines different from the baseline.
- **Not run, per the brief:** hall, full runs.sh, selfplay, randplay, released modes.

## Paragraph for docs/design/dungeon-run.md
"Family 11 (Monster Hunt bosses B): thirteen Witchwood encounters with a clickable power, tier by printed Health (20: tier 1; 30: tier 2; 35, 40: tier 3; 50, 60: tier 4). Crooked Pete is the first boss whose form changes inside a match. At the start of his turn at half his maximum Health or less, Soot Up's trigger makes him Beastly Pete (a script form of the encounter model: GILA_BOSS_52h2 with Beast Within). His hand is set aside, Beastly Pete's deck replaces his, he draws 6, gains 15 Armor and keeps his Health. Face Collector plays a copy of the run deck (flag mirrorDeck: less what a boss may not hold, Fabled bundles whole, the curated row deck below 10 cards). Raeth's Desecrate is readied again after a use that killed (private tag PowerRefresh 32646, its own Inspire trigger). Survival of the Fittest forces every minion to attack a random enemy minion (EncounterRules.AllAttackRandomEnemyMinions). Grave Mistake resolves every Deathrattle twice. Shallow Graves never summons a dead run shrine. Terrify shuffles through the engine's MoveToDeck (a shrine goes dormant instead). Soul Assimilation and A New Face respect the shrine control and transform rules. Decks are the wiki's tables; Damph's and Sezavo's 20 cards are extended to 30, and Witchwood's Touch is Shield Block until family 23 scripts it."

## To redo after the merge
- Run gen.sh, then `python3 -I verify/tools/gen/p8f11/make_fixture.py`. The fixture is a copy of encounters-v1.json plus five rows.
- Recompute the four pins above, and resolve the textual conflicts in suites.sh and bp.sh.
- Add 32646 to the tag-table comment and MANIFEST entries.
- When family 23 scripts GILA_BOSS_99t2, rerun encounters: Pete's form deck, Cragtorr, Azalina and Gustave move, and c-enc-11 P3 (deck kinds) and P7 (substitutions) must be updated. P7 also moves if someone scripts Ogre Ninja, Ogre Brute or Mogor's Champion.
- Open issue 3.4's Echo floor should be fixed in the engine. Then restore `SHR11` to Gustave-first and re-record enc11-shrine.
