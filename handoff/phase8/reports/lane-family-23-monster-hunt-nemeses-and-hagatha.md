# Family 23 report: Monster Hunt nemeses and Hagatha

Worktree `/home/user/wt/f23`, branch `wip/f23`. Commits: 9f25b53, 09156ff, c575aa3. Everything is committed and the tree is clean. Nothing was pushed, downloaded or deleted. All files are LF without BOM. No process of mine is still running.

## FLAGS (read first)
1. **No private tag used.** Reserved 32658 is still free.
   - No save field was added and the save version is unchanged.
   - No schedule default, finale, act, pool set, ReleasedShapes entry or golden sequence was touched.
2. **Shared edits** (each one minimal):
   - `plugin/DungeonRun/DungeonRunAdmission.cs`: GILA_503 (Hunter's Insight) left the Tess/Darius run-treasure row and GILA_907 (Clockwork Assistant) left the Shaw/Toki run-treasure row.
     - Both moved to my row `run-treasure-nemesis-deck` (legacy: Player; catalog: Player|Boss), the same pattern family 09 used for its Kobolds treasures.
     - Reason: Godfrey's and Toki's wiki decks list them, and an id may sit in only one admission row.
   - `data/dungeon-run/reference/encounters/F10.json`: removed the now-false "Witchwood's Touch … is Shield Block until it is scripted" notes and the two `replacements` entries (Hav'nixx, Experiment 3C).
   - `tools/restoration-ledger/curation.json`:
     - The E10 note's deck clause is updated: two decks are now unchanged (Scarecrow, Hav'nixx) and two substituted.
     - E23 is appended after E10.
     - The tool-owned lists were regenerated.
   - `verify/lib/suites.sh` (c-enc-23 appended to BUILD_LIST and RUN_LIST) and `verify/bin/bp.sh` (usage lines, CARDS23/FIX23 variables, three case lines after enc10-hagatha). Expect merge conflicts in both.
   - Family fixtures regenerated from the new encounters file: p8f02, p8f03, p8f04, p8f08, p8f09, p8f10. All their suites still pass with 0 differing lines against the baseline.
3. **Hagatha's share of act II changes.** There are now five GIL act II majors.
   - Hagatha is the act II major in 104 of 1000 seeds (was 506); a nemesis is the major in 402.
   - Hagatha's `incompatibleModifiers` (iron_bosses, open_road, hot_start) used to keep those modifiers out of the act II offer. On seeds whose major is now a nemesis they can be offered again.
   - The bosspilot walk takes the first offer, so boss Health on act II floors moves in other families' bosspilot records. c-enc-model J1 is one example: Whirt 46 → 51 with mod_iron_bosses, now re-pinned.
   - Expect enc09/enc10 and the rules/cross records to move after the merge.
   - The enc10 records will also move for Hav'nixx and Experiment 3C, whose decks now hold Witchwood's Touch instead of Shield Block. I did not run enc10.
4. **Families 11 and 12 (unmerged) still use Shield Block for GILA_BOSS_99t2.** After the merge, rerun the encounters step and update these pins:
   - **wip/f11, `verify/suites/p8f11/c-enc-11/Program.cs`:**
     - Rows table line ~681/682/686: cragtorr, azalina and gustave go "substituted" → "authentic" if 99t2 was their only change.
     - P7 (lines ~735-736): `Subs(...) == "GILA_BOSS_99t2>EX1_606:unscripted"` for those three becomes "".
     - Line ~738: Pete's form deck holds 0 EX1_606 (now 2 × 99t2), and `formSubs == "enc-gila_52_pete-f1"` will change.
   - **wip/f12, `verify/suites/p8f12/c-enc-12/Program.cs`:**
     - Rows lines ~530 and ~541: groddo and hannigul "substituted" → "authentic".
     - Lines ~587-588: groddo and hannigul Subs become ""; sephira becomes `"EX1_154>LOOT_051:unscripted,EX1_160>CFM_614:unscripted"`.
     - Line 537: sephira stays "substituted".
   - Their F11/F12.json "until it is scripted … Shield Block" notes and replacements, and the Shield Block wording in their E11/E12 notes, become stale. Remove them as I did for F10/E10.
   - c-enc-model A4's family counts combine (mine is `23:5`).
5. **Not run (allowed):**
   - randplay: its regkeys line will move because new cards are registered. Its game digest should not move, since every new card is NeverGenerated or a boss card.
   - enc10 and the other families' bosspilot modes (see flag 3).
6. **c-enc-model F2 platform difference (pre-existing).** It prints save lengths 13954/25744 against the Windows baseline's 14306/26370. That is LF vs CRLF (the files have 352 and 626 lines), and the integrator's checkout shows the same, so it is not mine.

## Per id
- **Done:**
  - GILA_BOSS_48h, 48p: Chronoacceleration (2), the client enchantment GILA_BOSS_48e (Mega-Windfury, dies at the end of the turn).
  - GILA_BOSS_49h, 49p: False Heart, passive, family 06's HeroImmuneWhile. The row carries flag `heroImmune`, so ULDA_706 is excluded from her fight.
  - GILA_BOSS_49t: Crowskin Faithful's Deathrattle draws a card and shuffles a new Faithful into its controller's deck.
  - GILA_BOSS_49t2: Crowskin Pact transforms any minion and takes control of it. A run shrine goes dormant instead, and a full board destroys the Faithful.
  - GILA_BOSS_59h, 59p: Pistol Barrage hits the target and its neighbours, no Spell Damage.
  - GILA_BOSS_60h, 60p: Plunder is auto-cast at turn start (family 09's AutoCast) while his deck holds a weapon and his hand has room. He starts with Kingsbane equipped.
  - GILA_BOSS_60t: Cursed Crewmember uses Dead Man's Hand's rule for the copies.
  - GILA_BOSS_99t, 99t2, 99t3: Witchwood's Touch (draw 1/2/3, gain 6/8/10 Armor). The wiki marks 99t as unused and no deck lists it; it is scripted, admitted and tested in c-enc-23. In the ledger it is partial with `run:encounter-roster`, a candidate for family 25.
  - GILA_BOSS_61h, 61t, 61t3, GILA_500h4, GILA_600h2: already scripted by family 01. I made no model change.
    - The hero switch was already complete in family 01's code: run-owned `DungeonRunState.HeroSwitch`, `MatchIdentity`/`MatchClasses`, and run records that are never written. I did not duplicate it.
    - My addition is regression coverage through the shipped row: c-enc-23 G1-G8, Hagatha in enc23 and enc23-seeds, and her row now sharing the act II pool with the nemeses.
- **Rejected:**
  - GILA_BOSS_48t Alternate Timeline: a game restart, phase 9 (turn-rewind family). It is left out of Toki's deck. Recorded in the F23.json "rejected" list, the admission row `nemesis-rejected-restart` (None, TurnRestartLift) and the E23 rejected list.
  - GILA_900h2 stays rejected (family 01's row; Temporal Loop, phase 9).
- **Decks** (wiki read 2026-11-14; the nemeses are act II majors, Health is the act curve plus 10):
  - Glinda and Godfrey: authentic, 30 cards.
  - Toki: substituted. Alternate Timeline and Enhance-a-matic are left out and refilled with second copies.
    - Enhance-a-matic (GILA_903) is a Secret generator of every class and is not lifted for a boss; it would need generated Secrets on with eleven closed pools.
  - Shivers: substituted. Fel Reaver (GVG_016, unscripted) appears twice and both copies are refilled with second copies.

## Files
- **Own files:**
  - `engine/SabberStoneCore/src/CardSets/DungeonRun/DungeonRunEncounters.F23.cs`
  - `plugin/DungeonRun/DungeonRunAdmission.F23.cs` (6 rows: nemesis-board-power, nemesis-passive-immune, nemesis-auto-cast, nemesis-hunt-card, run-treasure-nemesis-deck, nemesis-rejected-restart)
  - `data/dungeon-run/reference/encounters/F23.json`
  - `verify/tools/gen/p8f23/make_f23.py`, `make_fixture.py`
  - `verify/fixtures/p8f23/fixtures/encounters-f23.json`
  - `verify/suites/p8f23/c-enc-23/`
- **Generated (regenerated):** `encounters-v1.json`, `restoration-ledger.csv/.md`, curation lists. `catalog-v1.json` and `dungeon-run-catalog.md` are byte-identical to the base.

## Pins moved (suite: old → new, cause)
- **c-enc-10:**
  - P3 Hav'nixx deck kind: substituted → authentic (99t2 scripted).
  - P8: Hav'nixx Subs "GILA_BOSS_99t2>EX1_606:unscripted" → "", and Experiment 3C no longer holds the 99t2 substitution.
- **c-enc-model:**
  - A4 family counts `…,23:1,…` → `…,23:5,…`.
  - A10: 1 → 2 rejected (adds GILA_BOSS_48t).
  - E3: MajorByMix accepts any GIL enc-major2 row, and the detail now also counts nemesis majors.
  - J1: Whirt's Health 46 → 51 (mod_iron_bosses; flag 3).
  - J13: passive list + GILA_BOSS_49p, GILA_BOSS_60p.
  - Still 141 checks.
- **c-admission:**
  - 3167 → 3220 passed. Generated lists 426 → 437 for both sides; boss-only stays 311.
  - Added an exemption statement for the nemesis-* families.
  - The catalog-lift check covers 6 rules (was 5) and includes the nemesis treasure row.
- **c-catalog:** ledger golden 4704/2813/295/538 → 4708/2813/309/528. That is +4 run:boss rows, and 10 rows going from missing to partial (4 powers, 49t, 49t2, 60t, 99t, 99t2, 99t3).
- **c-deckops:** T5 4704 → 4708.
- **c-content:** passive registry list + GILA_BOSS_49p, GILA_BOSS_60p (still 904).

## Generators (gen.sh, twice, all six files byte-identical)
- Encounters: "73 rows, 77 decks (46 authentic, 31 changed), 157 adaptations, 2 rejected".
- Catalog: "506 entries (452 offerable pool, 2 withdrawn, 52 fixed), 6508 admitted and 1 excluded" (unchanged).
- Ledger: rows 4708; implemented 2813, partial 309, missing 528, duplicate/support 1058, adapted 0.

## Lane verification
- **Engine:** 0 warnings, 0 errors (Linux md5 eb2079cd76b1508f8bf7ae950f07f08e).
- **Suites** (each twice, run 1 = run 2):

| Suite | Result | Differing lines vs baseline |
|---|---|---|
| c-runlayer | 244 | 0 |
| c-catalog | 127 | 0 |
| c-content | 904 | 0 |
| c-admission | 3220 | 4 (count and NOTE lines) |
| c-acts | 181 | 0 |
| c-deckops | 174 | 0 |
| c-shrine-core | 131 | 0 |
| c-enc-model | 141 | 14 (relabels, J1, the F2 platform difference) |
| c-enc-engine | 122 | 0 |
| c-enc-10 | 116 | 2 |
| c-enc-23 | 66 (new) | no baseline |

- **Extra, run once, all 0 differing lines:** c-enc-secrets 60, c-collectibles A 115 / B 108 / C 102, c-enc-07 161, c-enc-08 113, c-enc-09 166, c-rules-05 170, c-rules-06 224, c-w3fix 4, c-w1fix 23.
- **Bosspilot** (each twice identical, stderr 0 bytes, 0 faults / 0 fallbacks / 0 failed checks / 0 swallowed, power-unused 0):

| Mode | Games | Boss wins | Cards played by the boss |
|---|---|---|---|
| enc23 (FAMILY=23: the 4 nemeses + Hagatha) | 10 | 10 | 49t ×4, 49t2 ×2, 60t ×3, 99t3 ×2, GILA_503 ×1, GILA_907 ×2, 61t3 ×1 |
| enc23-seeds (SEED_BASE 19000) | 10 | 10 | 49t, 49t2, 60t ×4, 99t3 ×5, 503, 907 ×2, 61t ×2 |
| enc23-cards (fixture, `once` 1 and 2, then `same`) | 16 | 13 | 49t ×26, 49t2 ×16, 60t ×24, 99t ×10, 99t2 ×15, 99t3 ×14, GILA_503 ×9, GILA_907 ×18 |

  - In enc23-cards all eight family cards were played from hand.
  - Hagatha's enc23 lines equal the first two lines of the enc10-hagatha baseline.
  - The majors are strong against the harness's starter decks (20 of 20 shipped games).
  - Plunder's auto-casts do not show in bosspilot lines; c-enc-23 D2-D6 prove them.

## For the integrator after the merge
- Rerun gen.sh and the family fixture generators, adding `python3 -I verify/tools/gen/p8f23/make_fixture.py`.
- Recompute the pins above and those of flag 4.
- Re-record the bosspilot baselines that move (flag 3).
- Optionally add the p8f23 entries to `verify/MANIFEST.md`.

**README section 4 numbers:** c-admission 3220, c-enc-23 66; bp enc23 10/10, enc23-seeds 10/10, enc23-cards 16/13.

## Paragraph for docs/design/dungeon-run.md
"Family 23 (Monster Hunt nemeses and Hagatha): the four Final Nemesis fights of The Witchwood join Hagatha the Witch as act II majors (pool enc-major2; Health is the act curve plus 10). Infinite Toki's Chronoacceleration gives a friendly minion the client enchantment Accelerated (Mega-Windfury, destroyed at the end of the turn). Glinda Crowskin's passive False Heart makes her hero Immune while she controls a minion (family 06's HeroImmuneWhile). Her row carries the flag heroImmune, which keeps ULDA_706 out. Crowskin Faithful draws and shuffles a new Faithful into its controller's deck when it dies. Crowskin Pact transforms a minion into a Faithful and takes control of it; a run shrine goes dormant instead. Lord Godfrey's Pistol Barrage deals 2 to a minion and its neighbours. Captain Shivers starts with Kingsbane equipped, and his Plunder is cast by rule at his turn start while his deck holds a weapon and his hand has room. Cursed Crewmember draws two cards and shuffles a copy of its caster's hand into that caster's deck. Witchwood's Touch (1), (2) and (3) are scripted; (2) replaces the Shield Block stand-in in the Monster Hunt decks of families 10-12. Hunter's Insight and Clockwork Assistant are admitted for a boss on catalog runs. Alternate Timeline (a game restart) and Enhance-a-matic (a Secret generator of every class) are left out of Toki's deck; the first is rejected to phase 9. Hagatha's temporary hero switching stays family 01's run-owned DungeonRunState.HeroSwitch, never written into the run's hero, power or deck records. Toki's hero card stays rejected (Temporal Loop, phase 9). With five GIL majors, Hagatha is the act II major in about a tenth of runs."

---

# Family 23 review: confirmed findings (adversarially verified)

## F23.1 [low] data/dungeon-run/reference/encounters/F23.json:282
Lord Godfrey's row is missing his opening-hand Hunter's Insight. The wiki's Final Nemesis page says "He starts the game with Hunter's Insight for a very aggressive start". The row has no start block, although the row format supports start.bossHandAdd (DungeonRunData.cs:1638). GILA_503 is only one random card in his 30-card deck.

Scenario: An act II Godfrey fight: Hunter's Insight (GILA_503, 1 copy in enc-gila_59_godfrey) reaches his opening hand only when the shuffle puts it in his first 3-4 cards (about 13% of games). In the original encounter he always holds it from the start and reduces his hand's Costs by (3) early. Most fights lose the aggressive start the wiki describes, and the card often comes too late to matter.

Verifier: The wiki's Final Nemesis page (Lord Godfrey section, line 15 of the raw text) says plainly: 'He starts the game with Hunter's Insight for a very aggressive start'. The Lord_Godfrey_(boss) page that the row cites has no Notes section, so it neither states this nor contradicts it. It only lists Hunter's Insight x1 in the deck table and the card under 'Special cards'. For Shivers, the implementer followed the equivalent note and added start.bossWeapon LOOT_542. Godfrey's row (F23.json:263 onward) has no start block, so GILA_503 is just one card in his 30-card deck and reaches his opening hand only by shuffle. The F23.json method line ('The pages' notes are followed') shows that this note was never read: the Final Nemesis overview page is not cited for Godfrey.

Fix: In data/dungeon-run/reference/encounters/F23.json, add to the gila_59_godfrey row: "start": {"bossHandAdd": ["GILA_503"]}, and the flag "start". Take the deck-list copy of Hunter's Insight out, or record that the hand card is that copy and fill the deck back to 30 as the encounters step does. Note the source (the Final Nemesis page, Godfrey section) in the row's notes. Regenerate encounters-v1.json and the p8f23 fixture, and add a c-enc-23 check that GILA_503 is in Godfrey's hand after the mulligan.

## F23.2 [medium] plugin/DungeonRun/DungeonRunAdmission.F23.cs:64
Moving GILA_503 (Hunter's Insight) and GILA_907 (Clockwork Assistant) into the new row `run-treasure-nemesis-deck` (CatalogAdmit = Player|Boss) breaks pins in two existing suites. Neither suite was updated, and neither was run: the implementer's report does not list c-treasures-F3 or c-treasures-F4. Both suites are registered in verify/lib/suites.sh and have baselines in verify/baseline/runs.

Scenario: c-treasures-F3, verify/suites/p6f15/c-treasures-F3/Program.cs:164-169: the loop over `f3` includes GILA_503 (it is not in `passives`). It calls `Classify(C("GILA_503"), ..., AdmitScope.Boss, null, catalogRun: true)` and asserts `!b.Admitted` ("a row admitting the player (catalog run), never the boss"). Classify now replaces verdict.Admit with the row's CatalogAdmit (Player|Boss). `Admitted` is `(Admit & Asked) == Asked`, so the Boss verdict is admitted and the check FAILs. c-treasures-F4, verify/suites/p6f15/c-treasures-F4/Program.cs:169-177: the `families` map pins `["GILA_907"] = "run-treasure"` and asserts `!b.Admitted && RuleFor(id)?.Family == pair.Value`. RuleFor("GILA_907").Family is now "run-treasure-nemesis-deck" and the catalog Boss verdict is admitted, so "T2 admission GILA_907" FAILs. Each suite returns a non-zero failure count after the merge. The new row's legacy Reason cites these same two suites as its tests. The new c-enc-23 C5 asserts the opposite: both are boss-admitted on a catalog run. The c-treasures pins need to be re-stated, for example by moving GILA_503 and GILA_907 into an expectation of "player and catalog boss".

Verifier: Confirmed. The diff moves GILA_503 out of the F3 run-treasure row and GILA_907 out of the F4 run-treasure row (DungeonRunAdmission.cs:576, 602) into run-treasure-nemesis-deck, whose CatalogAdmit is Player|Boss (DungeonRunAdmission.F23.cs:62-75). Classify swaps in CatalogAdmit when catalogRun is set (DungeonRunAdmission.cs:1102). In c-treasures-F3/Program.cs:164-169, the f3 array (line 69) includes GILA_503, which is not in passives (line 74), and the check asserts !b.Admitted for AdmitScope.Boss with catalogRun true. That check now fails. In c-treasures-F4/Program.cs:166-177, the map pins ["GILA_907"] = "run-treasure" and asserts !b.Admitted with RuleFor(id).Family == "run-treasure". Both conditions are now false. Neither suite appears in the family-23 diff (the stat shows no verify/suites/p6f15 changes). Both are registered in verify/lib/suites.sh:10,28 and have baselines (verify/baseline/runs/c-treasures-F3.txt and F4.txt).

Fix: In verify/suites/p6f15/c-treasures-F3/Program.cs, leave GILA_503 out of the 'never the boss' loop (add `&& i != "GILA_503"` to the Where clause). Add a check that RuleFor("GILA_503").Family == "run-treasure-nemesis-deck": admitted for the player on legacy and catalog runs, admitted for the boss on a catalog run only, and still in GeneratedRejectsBoss(true). In verify/suites/p6f15/c-treasures-F4/Program.cs, drop ["GILA_907"] from the families map and add the same check for GILA_907. Run both suites and update their baselines with the attribution to family 23.


I fixed both review findings on `wip/f23` and agree with both. Everything is in commit **aba643d**, the tree is clean and nothing of mine is still running. Every suite and bosspilot mode below ran twice through the lock, and each pair came out identical.

**F23.1, Godfrey's opening Hunter's Insight.** The wiki's Final Nemesis page confirms it ("He starts the game with Hunter's Insight"). That page links to "Hunter's Insight (boss)", which is only a redirect to the same card, GILA_503.
- Godfrey's row now has a start block that puts GILA_503 in his hand after the mulligan, plus the `start` flag. The note cites the Final Nemesis page.
- The wiki table's single copy is the card he holds, so his deck is the table's other 29. That is 30 cards in all, as on the wiki. A major keeps its listed count, so nothing is filled back, and the deck still counts as authentic.
- The E23 note in `curation.json` says this too.
- New check C6 in c-enc-23 plays a real game from the shipped row's plan. It checks that exactly one GILA_503 is in his hand after the mulligan, none is in his deck or the player's hand, the row deck has 29 cards, and the row has the `start` flag. H3 now expects 29 cards for Godfrey.
- I reran `gen.sh` twice (all six generated files byte-identical) and regenerated the p8f23 fixture and the other family fixtures:
  - encounters: "73 rows, 77 decks (46 authentic, 31 changed), 158 adaptations, 2 rejected" (adaptations were 157)
  - catalog: unchanged
  - ledger: unchanged at 4708 rows (2813 implemented / 309 partial / 528 missing / 1058 duplicate or support)

**F23.2, c-treasures-F3 and F4.** Both checks are re-stated for the new row, at least as strictly as before.
- **c-treasures-F3:** GILA_503 is taken out of the "never the boss" loop and gets its own pair of checks. The run-treasure-nemesis-deck row must admit it for the player on legacy and catalog runs, and for a boss on catalog runs only. A legacy boss must still be refused, and it must still be in `GeneratedRejectsBoss(true)`.
- **c-treasures-F4:** GILA_907 is dropped from the families map and gets the same two checks.

| Suite | Old | New | Vs baseline |
|---|---|---|---|
| c-enc-23 | 66 | 67 passed (C6 added) | new suite |
| c-treasures-F3 | 283 | 283 passed | 0 differing lines |
| c-treasures-F4 | 236 | 236 passed | 0 differing lines |
| c-admission | 3220 | 3220 passed | unchanged |

I also reran c-catalog 127, c-deckops 174, c-content 904, c-enc-model 141 and c-enc-10 116. All pass, and their differences from the baseline are the same as in my first report.

| Bosspilot mode | Games | Boss wins |
|---|---|---|
| enc23 | 10 | 10 |
| enc23-seeds | 10 | 10 |
| enc23-cards (once 1 and 2, then `same`) | 16 | 13 |

All three modes had 0 faults, 0 fallbacks, 0 failed checks, 0 swallowed leaf exceptions, power-unused 0 and empty stderr. enc23 now shows GILA_503 played twice (it was once).

**Commits.** The coordinator's WIP commit held exactly my review-fix changes; I reworded it as aba643d. The branch is now 9f25b53, 09156ff, c575aa3, aba643d. Nothing was pushed.

For the integrator: update the README section 4 numbers to c-enc-23 67 and encounters "158 adaptations". All my other flags from the first report still stand.