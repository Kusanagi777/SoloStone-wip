WAVE 6 INTEGRATION REPORT (families 20, 21, 22, 23, 24, merged on branch wip/int6 from e32fe81, the wave-5 tree; Linux
cloud container, .NET SDK 10.0.402; every harness call through the harness lock; nothing pushed by the integrator, no
directory deleted, nothing downloaded; hearthstone.wiki.gg pages read with ?action=raw for the same-character check)

All five branches are merged, the cross-family items of the brief are done, the four confirmed merge-review findings (two
of them the same Mirage defect) are fixed, each with a check that fails without the fix, and the whole regression ran after
the last source change.
The regression found two defects: Snowfury Giant's adaptive cost did not count an Overload that is not printed on its
source (a stable fault in `bp.sh enc22a`; an engine fix with c-w5fix O1-O3, which fail without it, section 8), and the
bosspilot harnesses' form-threshold check did not hold for a form without Health of its own (one failed check in
`bp.sh tekahn24`; a harness fix, narrower for such forms, section 9). Everything those changes can reach was run again
after them: 0 faults, 0 fallbacks, 0 failed checks, 0 swallowed leaf exceptions in every mode, run 1 = run 2 everywhere.
The wave's results are the new baseline (`verify/baseline`, recorded on Linux).

1. MERGES (git merge --no-ff, in the brief's order; every side kept)
| Branch | Merge commit | Textual conflicts |
|---|---|---|
| wip/f20 37f5c9d | e12538f | curation.json, suites.sh, bp.sh, c-admission, c-catalog, c-content, c-deckops, c-enc-model; generated files |
| wip/f21 100850a | 5c88700 | the same set (c-enc-model A10 included) |
| wip/f22 9fe1fa5 | 434ab50 | the same set, plus Actions/EncounterRules.cs (the tag table) |
| wip/f23 aba643d | af48a06 | the same set, plus plugin/DungeonRun/DungeonRunAdmission.cs and the six family fixtures p8f02/03/04/08/09/10 |
| wip/f24 a4c5db3 | 485d10e | the same set, plus EncounterRules.cs (the tag table), catalog-v1.json and dungeon-run-catalog.md |

2. CONFLICTS AND RESOLUTIONS (by hand; every side kept)
- `tools/restoration-ledger/curation.json`: landedFamilies E20-E24 appended after E19 in order; E10 is family 23's reworded
  note (f24's E10 was the base one); the tool-owned lists taken as a union (the rejected list gains GILA_BOSS_48t), then
  rewritten by gen.sh.
- `verify/lib/suites.sh`: one new line `p8f20/c-enc-20 ... p8f24/c-enc-24` in both lists: 83 build projects, 70 run entries
  (labels in suites.sh, builds.sh, runs.sh, build-all.sh updated).
- `verify/bin/bp.sh`: every usage line, variable (A20/B20/CARDS20 ... LORDS24/CARDS24) and case block kept; the whole-block
  usage print kept (f22's `sed -n 2,30p` dropped).
- `EncounterRules.cs`: MirageIllusion 32657 and PlagueCount 32659 after SelfDamageImmune 32653; each conflict had lost the
  shared `/// <summary>` opener, restored.
- `plugin/DungeonRun/DungeonRunAdmission.cs`: the F3 run-treasure row loses both GILA_500p2t (family 15) and GILA_503
  (family 23); the comment names both.
- `c-admission`: family 22 carried family 19's `boss-tombs-` statement, already present, kept once; the catalog-lift check
  merges family 23's nemesis row with wave 5's rows (9 CatalogAdmit rows).
- `c-content` and `c-enc-model` J13: the passive lists concatenated in registration order 20-24.
- `c-enc-model`: A4 "...19:18,20:18,21:17,22:18,23:5,24:5" with a wave-6 clause; A10 four rejections (GILA_900h2,
  DALA_BOSS_75h, ULDA_BOSS_58h, GILA_BOSS_48t); E3 keeps wave 5's Heist and Rumble counts, adds family 23's nemesis counter
  and MajorByMix encounterPool, and counts each of family 24's four lords.
- `c-catalog` and `c-deckops`: every family comment kept, the numbers recomputed (section 5).
- Generated files and fixtures took the first parent and were regenerated after the last merge.
- Merge fidelity: the line count of every non-generated file equals e32fe81 plus each branch's delta from a95b97e, except
  the deliberate resolutions (DungeonRunAdmission.cs +1 comment line, suites.sh +2, c-admission -2 for the duplicate family
  19 statement, c-content +7 and c-enc-model +5 for list and comment lines and A10). After gen.sh every encounter row and
  deck equals its family branch or e32fe81, except the seven Witchwood's Touch decks (section 3). EncounterRules.F16.cs
  (family 24) and tombs_tiers.py (families 20-22) are byte-identical to the wave-5 files.

3. CROSS-FAMILY ITEMS OF THE BRIEF
- Same-character eligibility (25ad1b3, corrected by the review in 561a2cf). run-v1.json: DALA_Barkeye excludes
  ulda_50_barkeye (family 21 flag 6), DALA_Vessina excludes ulda_63_vessina (the wiki's Vessina_(boss) and
  Ol'_Barkeye_(boss), Lore: "one of League of E.V.I.L.'s henchmen that made debut in The Dalaran Heist"). Every hero row
  was checked against all 255 encounter rows and the 20 custom bosses (names, client flavor text, wiki lore); the only
  other pair is the Heist hero Tekahn (Dark Pharaoh Tekahn) and the Plague Lord Tekahn (ulda_67_tekahn, family 24's finale
  row), handled as an open item for the close-out (section 6, finding 3). Infinite Toki is not excluded for the Toki hero
  row: the original makes her Toki's own Final Nemesis (wiki Final_Nemesis). c-w5fix Q2 pins the five act-pool pairs (it
  fails without any one key); Q3-Q5 run the Barkeye and Vessina pairs on 600 acts-v3h seeds each (0 meetings with the key,
  some without; the Hunter and Shaman class rows still meet them); Q7 is the Tekahn tripwire. c-w5fix 19 -> 26 checks
  (stage A wrote 16, the README's wave-5 row, which predated the E checks; the baseline log says 19), 29 with section 8's
  O1-O3.
- Auto-cast after the draw (c949763, completed by the review in 1450cf9). The family 21-23 powers registered through family
  09's `AutoCast` act after the turn-start draw since the merge: 46p Maw of the Desert, 48p Arena Expert, 72p Lord of
  Destruction, 78p Massive Tentacles, GILA_BOSS_60p Plunder. Their suites pass unchanged and each text fits "after the
  draw"; only Arena Expert and Plunder read the boss's deck, and Arena Expert's F2 already counts both the draw and the
  pull. Plunder's c-enc-23 D3 and D5 passed partly by the shuffle seed (the weapon not on top of the deck); they now state
  that premise, and the new D3b shows a weapon on top is taken by the turn's draw, so Plunder finds none (it fails under
  the old order). Stage A listed Mirage (62p) among these powers; it was not one of them (a hand-written TURN_START
  trigger) and is fixed by the review (section 6, findings 1 and 2). No check was weakened.
- Witchwood's Touch (fde443d). make_f11.py and make_f12.py drop the GILA_BOSS_99t2 -> Shield Block replacements and their
  deck notes (each reproduces the old F11/F12.json byte for byte before the edit; F11/F12.json regenerated after it);
  Sephira keeps her Wrath and Power of the Wild substitutions. The E11/E12 notes and p8f11 patch_curation.py follow.
  c-enc-11: P3 Cragtorr, Azalina and Gustave authentic; P7 their Subs "", the decks hold 2/1/2 copies of 99t2, Beastly
  Pete's form deck 2 x 99t2 and 0 EX1_606; formSubs stays "enc-gila_52_pete-f1" (it lists the form deck ids, contrary to
  family 23's note). c-enc-12: P3 Groddo and Hannigul authentic; P7 their Subs "", Sephira's the two Druid cards, the decks
  hold 2/2/1 copies, no F12 deck holds EX1_606. Record moves: enc10 (family 23's edit) Hav'nixx authentic, Experiment 3C
  still substituted; enc11 three decks plus Pete's form authentic; enc12 two decks authentic, Sephira 5 -> 4
  substitutions. Adaptations 888 -> 882 (seven deck adaptations leave, Sephira's change), then -> 876 (six notes removed).
- Duplicates (cccb25a). `TombsC.PowerOnlyForm` (identical code) is removed; Broken Phylactery calls
  `EncounterRules.SwapPowerForm`. `TombsD.Overload` calls family 17's `EncounterRules.OverloadFor` and keeps only
  Thunderhead's Sparks; its Trogg filter had also skipped a minion at 0 Health not yet marked for destruction, which makes
  no difference in play. HANDOFF 3.4's Tunnel Trogg entry, the LoeCardsGen.cs comment and the OverloadFor summary follow.
  EncounterRules.F16.cs and tombs_tiers.py byte-identical to wave 5 (no drift).
- Engine and shared card edits (reviewed, no code change): Draw.cs (one string compare per fatigue hit); BOT_543e Mind Meld
  (Lifesteal on the spells cast that turn, marked with 32659 and cleared at turn end; in every game where Omega Mind is
  played with 10 crystals); ULD_168e/e3 (the Lackey aura; ULD_168 itself is unscripted, so only ULDA_033 uses it); the
  CopyCthun cast guard (STOP instead of an InvalidCastException; no change when the target is a minion); ULD_705/ULD_705t
  (properties, no static card read; Mogu Cultist is now a scripted 1-Cost collectible in every pool of implemented cards).
  No wave-6 family carries a ClearWeapons or ReplaceBrokenWeapon copy. Two stale comments calling ULDA_115 unscripted were
  fixed (589f7cd).
- Save field: `DungeonRunState.PlagueLordDamage` is append-only with ShouldSerialize; the save version stays 6. c-enc-24 G7
  passes (a save without the field re-saves byte-identical); c-enc-model F2 lengths 13954/13954 and 25744/25744, equal to
  the baseline.
- Hagatha's incompatible modifiers: her list comes from her own row (fixed 100 Health, the hero switch); the nemeses take the
  act curve's Health plus 10, and neither the wiki nor the client data covers SoloStone's modifiers, so nothing was added.
  Recorded in HANDOFF 3.4 as a step-9 input (e31c3f4; the J1 example corrected by the review, 561a2cf).
- Duplicate registration: section 4. Family 24's flag-5 cards LOEA10_5, LOEA10_5H, KARA_13_12H and LOEA16_24H are each
  registered once (F24). ULD_705, ULD_705t, BOT_543e and ULD_168e3 are each in one card set.
- Tag table (e31c3f4): 32657 (family 22) and 32659 (family 24); families 19-21 and 23 took none; all 21 private constants in
  32630-32699 unique.
- bp.sh / final.sh (e31c3f4): the usage print shows the whole block (through family 24); final.sh has a `w6` stage (20
  twice-per-call modes, then enc23-cards for k = 1 and 2 and `same`); the runner labels say 83 projects and 70 run entries.
- Fixtures (51ce8c6): 19 data-derived generators (the wave-5 set plus p8f23 and p8f24 make_fixture) ran twice,
  byte-identical. Rebuilt: p8f02, 03, 04, 08, 09, 10, 11, 14, 16, 17, 23 and p8f24's schedule-tekahn.json (which now
  carries wave 5's DALA_830 row and the finale twist pool). Unchanged: p8f05/06/18 and the four harnesses. After the
  review's run-v1.json change all 19 ran twice again: nothing changed.

4. SEMANTIC CHECKS (script in the integrator's scratchpad, run on the stage-A tree: ALL OK)
- Private tags unique; no family file holds a raw private tag number.
- Encounter registrations: 462, none twice, none also in `cards.Add`; F14-F24 each clean.
- Admission: 962 listed ids, none in two rows.
- 81 tracked JSON files parse with no duplicate keys. encounters-v1.json: 255 rows, 296 decks, ids unique; the reference F
  files hold the same 255 rows, no id in two files; schedule-v1.json: 44 rule rows, ids unique.
- No duplicate switch case or dictionary key in the 46 changed .cs files.
- gen.sh twice byte-identical after the merges, after run-v1.json, after F11/F12, on the stage-A tree, and after the
  review's run-v1.json change (561a2cf: only the run-v1.json hash in catalog-v1.json, dungeon-run-catalog.md and the ledger's
  source table moved; every count unchanged). The review fixes add no registration, tag, row, rule or admission id.

5. PINS RECOMPUTED FROM THE MERGED TREE (none weakened)
- Ledger (c-catalog golden): 4918 / 2813 / 660 / 387 / 1058 -> 4992 rows / 2814 implemented / 817 partial / 302 missing /
  1059 duplicate/support. +74 rows, not the families' 87: LOE_110 and DAL_614 are new in two families each, and 11 deck cards
  were already new in wave 5. All 74 are partial (61 run:boss, 13 cards); 83 rows go missing -> partial, ULDA_115 ->
  implemented, ULDA_Explorers -> support. c-deckops T5 4918 -> 4992.
- c-admission 3772 -> 4120 (+73, +72, +77, +53, +73: exactly the families' own deltas). c-content 904 -> 905.
- c-enc-model (141): A4 and A10 as above; E3 heist 1000, whirt 28, rumble 1000, voone 368, Hagatha 118, nemesis 449, lords
  Icarax 129 / Xatma 130 / K'zrath 127 / Vesh 124; E4 no-ledger 16 (inside 10-100); J1: seed 5001's modifier is
  mod_lean_supply on the merged tree, so Whirt's Health stays 46 (family 23's lane had mod_iron_bosses, 51); E2 and G10
  equal the baseline.
- c-enc-19 and c-enc-20 S2 assumed their rows end the file; they now assert one contiguous block followed only by later
  families' rows (the original intent; not weaker).
- c-w5fix 19 -> 26 (Q for Barkeye and Vessina, Q7 the Tekahn tripwire), then 29 with O1-O3 (section 8); c-enc-22
  120 -> 121 (C9b, the review).


6. MERGE-REVIEW FINDINGS (adversarially verified; four confirmed, two of them the same defect; none rejected; fixed in
   1450cf9 and 561a2cf)
- Findings 1 and 2 [medium], fixed (1450cf9): Mirage (ULDA_BOSS_62p) still auto-cast before the turn-start draw, and stage
  A's report said it had moved. Five of the six family 21-23 auto-cast powers are registered through family 09's
  `AutoCast` and moved with wave 5's hook; A Beautiful Oasis's Mirage was a hand-written `Passive` whose MultiTrigger began
  with a TURN_START trigger (fired in `Game.MainStartTriggers`, before `MainStart` -> `MainDraw` ->
  `EncounterRules.AfterTurnStartDraw`), although its comment said "as AutoCast does". The cast (Cost, `MirageReady`, the
  NUM_2 count, `TombsD.Mirage`) is now the registered entry's `AfterDraw` task; the passive keeps its `MirageSlot` aura and
  its AFTER_PLAY_CARD and TURN_END triggers. New check c-enc-22 C9b: a boss at 1 Health with an empty deck dies of Fatigue
  in its turn-start draw and casts nothing; with the old trigger it fails ("state COMPLETE uses 1 hand 4->5": the copy was
  made first). c-enc-22 120 -> 121. Effect on play is small (Mirage copies a card of the run player's hand, which the boss's
  draw does not touch), but the order, the history and the record now follow the rule.
- Finding 3 [low], fixed (561a2cf): the DALA_Tekahn `excludeBosses` key did nothing and no check could catch a
  Tekahn-vs-Tekahn fight. ulda_67_tekahn is a finale row in no act pool; `ActBossSequence` adds every finale's fixed bosses
  to `used` for the act draws and appends a finale's fixed bosses without reading `excludeBosses`, so the key changed no
  boss sequence, now or after the close-out enables the finale; c-w5fix Q7 asserted 0 meetings with and without it. The
  inert key is removed from run-v1.json (the catalog follows the file hash; gen.sh twice byte-identical, counts unchanged;
  the 19 fixture generators twice, nothing changed). Q2 now pins the five act-pool pairs. Q7 is the close-out's tripwire:
  it asserts the row is tier finale, in no shipped finale's fixed bosses and no act pool (600 DALA_Tekahn acts-v3h runs, 0
  meetings) and that DALA_Tekahn has no key. With the key back Q2 and Q7 fail; with acts-v3t's finale enabled on Tekahn in
  schedule-v1.json Q7 fails on purpose ("finales acts-v3t"). HANDOFF 3.4 records the pair as open for the close-out, which
  decides what a DALA_Tekahn run meets in that finale (not as covered by c-w5fix Q).
- Finding 4 [low], fixed (561a2cf): HANDOFF 3.4's wave-6 entry cited the family 23 lane's J1 value ("Whirt 46 -> 51 with
  mod_iron_bosses"), while c-enc-model J1 on the merged tree pins seed 5001's mod_lean_supply and Whirt at 46. The entry now
  gives both: the lane's value and the merged tree's, which J1 pins.
- Stage A's report, cross-family item 2, listed "62p Mirage" among the powers that act after the draw; section 3 above
  states it correctly (the five AutoCast powers moved at the merge, Mirage by the review).

7. THE REGRESSION (after the last source change, commit b074311; logs verify/.out, results verify/baseline)
- Order: the Snowfury Giant fix (c26c003, 16:52) came after a first pass of runs.sh 1 and 2 and the 14 family 20-23
  modes (section 8); then the engine build, gen.sh twice, build-all, runs.sh 1 and 2, same.sh, the family 20-24 modes,
  enc14 and enc15; then the harness fix (b074311, 18:32) and the rebuild of the six bosspilot harnesses (section 9);
  then every other bosspilot mode, rules06 x21, cross06, rules05 x18, cross05, the hall, and the 29 modes recorded
  before the rebuild again. Every harness call went through the harness lock, one at a time.
- Engine: 0 warnings / 0 errors (Linux md5 317cf8451d56e7eebcb20629de20708f, the build after c26c003; b074311 touches no
  engine file). build-all: 125 entries exit 0 with 0 errors (engine, catalog tool, 83 suites, randplay, 39 hall
  projects); the six harnesses b074311 edits were rebuilt after it (0 errors each). gen.sh twice byte-identical after
  the last data change (encounters "255 rows, 296 decks (125 authentic, 171 changed), 876 adaptations, 4 rejected";
  catalog "506 entries (452 offerable pool, 2 withdrawn, 52 fixed), 6508 admitted and 1 excluded member references";
  ledger rows 4992, implemented 2814, partial 817, missing 302, duplicate/support 1059; all six generated files
  unchanged), and twice again at the record (section 12). Fixture generators: twice byte-identical after the last data
  change (section 3).
- runs.sh 1 and 2 (70 entries, in three locked slices each): every entry exit 0, run 1 = run 2 (c-secretsearch: timing
  lines only, SUMMARY 689 / 0). same.sh vs the wave-5 logs, every difference explained:
  - c-content 2 lines: 904 -> 905 (one check per run content entry with a shell; family 24's ULDA_115 entry is new).
    c-admission 4 lines: 3772 -> 4120 (section 5) and the NOTE "Generated lists: 582 -> 670 for both sides, 311 -> 312
    boss-only" (the families' admission rows).
  - c-enc-model 14 lines (7 checks): A4, A10, E3, E4 (no-ledger 46 -> 16) and J1 as in section 5; I11 reworded by family
    24 ("a banked ULDA_115 cast adds 10 ..."); B5 "1 errors" -> "2 errors": the deck card it plants, GILA_BOSS_48t, is
    now also refused by its admission row (family 23's nemesis-rejected-restart, Alternate Timeline), besides "not
    scripted" (a scratch copy of the suite printing every validator error, verify/.out/tmp/w6probe-encmodel, never
    committed).
  - c-w5fix 24 lines: Q2 names the five pairs; Q4/Q5 Squeamlish 83 -> 14 and Chu 88 -> 8 meetings without the key over
    600 seeds (families 20-22's 53 Tombs rows share the Tombs pools, so each Tombs row is drawn in fewer runs; George, a
    Kobolds row, stays 21); the new Q3-Q5 for Barkeye (0 / 36 / 36) and Vessina (0 / 32 / 32); Q7; N1 498 -> 705 notes;
    O1-O3; RESULT 19 -> 29.
  - Witchwood's Touch kept (section 3): c-enc-10 P3 (Hav'nixx substituted -> authentic, family 23's edit), c-enc-11 G9
    (Beastly Pete's own-deck sample lists GILA_BOSS_99t2x1 where it listed EX1_606x1), P3 (Cragtorr, Azalina, Gustave
    authentic) and P7 (10 lines), c-enc-12 P3 (Groddo, Hannigul) and P7 (6 lines).
  - Mogu Cultist (ULD_705, family 20) is a scripted 1-Cost collectible now, so every random pool of implemented cards
    holds it: c-heroes-F6 T22 (decisions 27 -> 28), c-treasures-F7 T33 (15 -> 14 turns), c-treasures-F12 T30 (17 -> 15
    turns), c-review-F18 R2 (three offer counts; the neutral Discover pool 812 -> 813), c-rules-05 Q14 (the prisoner's
    Costs gain a 5), c-rules-06 T7 (the Tomb's new minion OG_216 -> OG_218), c-enc-10 O2 (the sixteen 1-Cost minions),
    c-enc-15 I6 (OG_207 -> OG_209), c-enc-18 I3 (136 -> 137 minions); 2 lines each, 6 for c-review-F18 and 4 for
    c-enc-10 with P3. Checked: with ULD_705's PowerTask removed in a scratch engine build (expG, never committed) every
    one of those lines equals its wave-5 line (c-heroes-F6, c-treasures-F7, c-treasures-F12 and c-review-F18 0 differing
    lines; the others differ only in checks that validate the shipped data, which then refuse Sothis's deck, and
    c-enc-10 P3).
  - c-secretscan 2 lines: items 849 -> 851 (one item per active treasure and class MAGE, PALADIN: ULDA_115).
  - randplay 6 lines: the snapshot fingerprint (a hash over the engine's state types and the types reachable from the
    installed card definitions) moved with the wave's definitions; regkeys 2479 -> 2588 (+109 snapshot keys for the new
    registrations) and its hash; the game digest 40207800B289E504 -> 1DE562A92D22C987. With ULD_705 unscripted the
    digest is 9260C731EEC75DCE (regkeys 2587); with ULD_705 unscripted and BOT_543e Mind Meld as at e32fe81 it is wave
    5's 40207800B289E504 again (regkeys 2585, the fingerprint unchanged): Mogu Cultist and Mind Meld are the two changes
    that move random games (expG2, never committed). cards=10713 and TB implemented=109 unchanged.
  - c-secretsearch: timing lines only. selfplay: games identical to the baseline, digest 2CE546A18F96DCBC.
  - New suites: c-enc-20 111, c-enc-21 100, c-enc-22 121, c-enc-23 68, c-enc-24 65. Every other entry: 0 differing
    lines.
- Bosspilot: 196 results (the 175 of wave 5 and 21 new), every mode twice identical, 0 faults / fallbacks / failed
  checks / swallowed leaf exceptions, stderr empty for every encounter, rules and cross mode (the released and shrine
  harnesses: their one diagnostic line, 45 or 46 bytes per run). 146 of the 175 wave-5 records moved, 29 did not. Games
  / boss wins (differing lines vs the wave-5 record):
  - New family modes (no record; the lanes' own results in parentheses): enc20a 18/13 (18/14), enc20b 18/14 (18/16),
    enc20-seeds-a 18/12 (18/12), enc20-seeds-b 18/12 (18/15), enc21a 16/13 (16/14), enc21b 18/16 (18/16), enc21-seeds-a
    16/13 (16/10), enc21-seeds-b 18/17 (18/16), enc22a 16/12 (16/7), enc22b 20/17 (20/16), enc22-seeds-a 16/13 (16/11),
    enc22-seeds-b 20/18 (20/13), enc23 10/10 (10/10), enc23-seeds 10/10 (10/10), enc23-cards 16/13 (16/13; the eight
    family cards played from hand), enc24 6/6 (6/6; form-unreached 3, the lane 1: lords won before reaching a
    threshold), plague24 95 / 110 / 195 / 210 3/3 each (3/3 each), tekahn24 2/1 (2/2). They differ from the lanes' runs
    as wave 5's did: a bosspilot seed is seedBase + 1000 x the boss's row index, and on the merged tree each family's
    rows sit after the 194 wave-5 rows and the families merged before it, so seeds and hero rotations moved; the games
    also run with the other families' rows, cards and rules.
  - vs wave 5, the w5 set: enc14 30/18 (14), enc14-seeds 30/18 (10), enc14-flight 8/7 (10), enc14-cards 12/5 (26; all 5
    boss cards played), enc15 28/17 (24), enc15-seeds 28/14 (24), enc15-rat 8/3 (8), enc15-toys 8/6 (12), enc16 28/16
    (16), enc16-seeds 28/16 (20), enc16-carousel 8/8 (0), enc16-cards 18/14 (38), enc17 28/20 (22), enc17-seeds 28/17
    (22), enc17-void 6/4 (6), enc17-cards 20/11 (42; enc17-check: all 7 boss cards played), enc18a 12/7 (6), enc18b 12/9
    (8), enc18-finale 8/6 (4), enc19a 18/14 (14), enc19b 18/15 (6), enc19-seeds-a 18/12 (16), enc19-seeds-b 18/14 (6).
  - the w4 set: enc12 26/14 (24), enc12-seeds 26/18 (22), enc12-hidden 18/14 (12), enc11-pete 6/5 (14), enc13a 30/29
    (10), enc13b 24/16 (14), enc13-seeds 27/25 (16), enc11a 28/18 (28), enc11b 24/19 (28), enc11-cards 24/22 (50; the
    four cards played), enc11-shrine 37/28 (16; 9 of 9 shrine fights).
  - enc set: enc09 22/15 (14), enc09-seeds 22/17 (10), enc09-auto 16/10 (14), enc09-finals 20/20 (8), enc09-rooms 16/6
    (18), enc09-cards 24/22 (50), enc10 30/26 (32), enc10-nab 10/9 (8), enc10-seeds 30/21 (22), enc10-hagatha 6/6 (2),
    enc10a 32/21 (24), enc10b 28/22 (24), enc10-cards 36/19 (74), enc10-shrine 37/29 (68).
  - released: run 40/27, act 40/26, fixture 40/27, catalog 40/27, shrine 40/24, S2 40/23, S3 40/22, U 40/25, T1 40/13,
    T2 40/20, D 40/14: 0 lines each.
  - w2: enc01 20/18 (4), secret4 24/20 (10), rules01 DALA_850 10/8 (2), DALA_864 10/8 (2), DALA_866 10/10 (2), enc07a
    36/31 (0), enc07b 36/33 (22), shrine07 37/32 (2), seeds07-19000 36/30 (8), enc08 34/28 (16), enc08-tag 8/7 (16),
    enc08-cards 12/11 (26; the four cards played), enc08-trapped 6/5 (0), enc08-seeds 34/26 (18).
  - w1: old01 12/12 (4), secretfix 6/5 (0), plague 95/110/195/199/210 2/2 each (0 each), finale 8/8 (8), relay 2/1 (0),
    collectA 32/15 (66), collectP1-4 6/5, 6/6, 6/6, 3/3 (0 each), enc03 a/b/c 24/24, 24/20, 16/16 (50/50/34;
    enc03-check: every family card played), enc01-f03 20/18 (4), icarax2 4/4 (0), f04-boss 40/40 (80), f04-player 24/13
    (50), f04-icarax 0/110/210 4/4 each (0 each).
  - rules06 x21: 16 games each (12 for ULDA_706), 6 lines each (the dala_21_whirt and trl_200_voone games and the
    summary), 8 for ULDA_710 and ULDA_719, 4 for ULDA_714, 24 for ULDA_804 (also trap room, tunnel rat, Brandlemar and
    Ivan games). cross06 x21: 12 each (11 for ULDA_719, ULDA_804), 14-18 lines each (16 for 14 rules; 14 for ULDA_720;
    18 for ULDA_708, 710, 715, 719, 803, 804): the loot_22_tad, loot_53_inara and gila_26/27/29/33/34 games and the
    summary. rules05 x18: 16 each (12 for DALA_831, DALA_833), 6 lines each (whirt, voone, summary), 8 for DALA_854, 20
    for DALA_832, 22 for DALA_851. cross05 x18: 14 each (13 for DALA_852, DALA_853), 6 lines for DALA_829, 852, 853, 8
    for 13 rules, 22 for DALA_832, 24 for DALA_851.
  - Why lines moved, the same causes as waves 2-5 plus one data row. (a) 60 of the 61 new rows enter the pools of
    acts-v3h (53 Tombs rows, the four nemeses beside Hagatha in enc-major2, three Plague Lords beside Icarax; Tekahn's
    finale row is in none), so every harness that walks the acts to a boss's floor meets them and reaches the fight with
    another run deck (a different size, or the same size with other cards) or another act modifier (Whirt fights at 51
    Health instead of 46 in the enc01, old01 and rules01 DALA_850 lines). (b) Family 24's ULDA_115 "Gnomebliterator" is
    a new Tombs treasure of encounter schedules (run-v1.json, "runs": "encounters", scoped to the sixteen Tombs explorer
    decks), so every Tombs explorer run on acts-v3h drafts from a larger treasure pool. (c) The fixture modes
    (enc09-cards, enc10-cards, enc11-cards, enc11-pete, enc14-cards, enc16-cards, enc17-cards, enc08-cards, collectA,
    enc03, f04-boss, f04-player) move on nearly every line because their fixtures were rebuilt and their rows sit after
    the 255 shipped rows (wave 5: 194), and seeds follow the row index. (d) Witchwood's Touch moves the enc10/11/12
    games of Hav'nixx, Cragtorr, Azalina, Gustave, Beastly Pete, Groddo and Hannigul.
  - The engine changes of the wave by themselves (experiment D in the integrator's scratchpad: the final engine and
    harnesses on the wave-5 encounters-v1.json and schedule-v1.json of e32fe81 through ENCOUNTERS_JSON / SCHEDULE_JSON,
    compared with the wave-5 records): cross05 DALA_829 4 lines (loot_17_jinzo decisions 34 -> 35, loot_24_mothergloop
    deck 30 -> 29), cross06 ULDA_702 4 lines (gila_34_havnixx deck 33 -> 32, a player win -> a boss win, and the summary
    9 -> 10 boss wins), enc09-auto 10 lines (loot_53_inara deck 33 -> 34 and its auto count, the three loot_52_vault
    decks 33/38/20 -> 32/35/24, the summary no-loss 3 -> 4). None of them is an engine change: experiment E ran the same
    three with SOLOSTONE_ROOT on a scratch root whose data/dungeon-run is e32fe81's (run-v1.json, catalog-v1.json and
    the rest too): 0 differing lines vs all three wave-5 records; adding only the ULDA_115 row of the wave-6 run-v1.json
    to that root gives exactly experiment D's output (0 differing lines vs D; 4 / 4 / 10 vs the records). So the
    Snowfury Giant count, Mirage, the folded helpers and the shared card edits move none of these three records; the
    moves are the ULDA_115 treasure entering the Tombs explorers' drafts (deck sizes at the fight). The engine changes
    seen to move ordinary games are Mogu Cultist and Mind Meld (random pools, above): on the wave-5 data and fixture the
    final engine moves one f04-boss game by one decision (fix_04_brann vs Finley's Murloc Crusade, decisions 20 -> 19,
    the same result), and with ULD_705 unscripted (expG) it reproduces the wave-5 f04-boss record line for line.
  - Coverage checks: three print a miss on this tree, none a fault. `enc16-cards` "UNPLAYED: LOOTA_811" (Orb of
    Destruction, as in wave 5). `collectA` "UNPLAYED: AT_081 x0 DAL_731 x0 GIL_655 x0 GVG_113 x0 LOE_119 x0" (wave 5:
    all 33 played) and `f04-boss` "FAIL: never played by the boss: ULD_290 x0" (wave 5: all 23 played; two copies each
    in enc-fix_04_brann and enc-ulda_22_colossus), new this wave. Both follow the seeds, not a defect (experiment F, the
    final engine and harness with the wave-5 fixture files, whose rows sit after 194 rows): on the wave-5 data collectA
    reproduces the wave-5 record line for line (all 33 played; AT_081 x1, DAL_731 x1, GIL_655 x3, GVG_113 x1, LOE_119
    x6) and f04-boss plays all 23 (ULD_290 x2; 2 differing lines, the Mogu Cultist game above); on the wave-6 data
    collectA differs from the record by 8 lines (the wave-6 run-v1.json: the ULDA_115 row and the two new excludeBosses
    keys) and plays the five but not LOE_092, which the recorded wave-6 collectA plays x2, and f04-boss plays all 23
    (ULD_290 x3). The run player plays ULD_290 x2 in f04-player. So every one of the 33 family 02 cards and the 23
    family 04 boss cards is played by a boss on this engine. Wave 5's enc03 miss (ULD_169) is played on this tree
    (enc03-check: every family card at least once).
- Hall: harness + 38 audits, 39 of 39 SUMMARY lines equal to baseline/hall.txt (the hall lines of this run are
  byte-identical to it); 955 tests / 8285 checks, 0 bad, 0 failed.

8. THE SNOWFURY GIANT FIX (commit c26c003), found by this regression
- First bosspilot pass (after the review fixes, before this fix): `bp.sh enc22a` had 1 fault, stable in both runs: "engine
  rejected boss task" in Vessina's (ulda_63_vessina) turn 10 against a Warlock run, the boss playing Snowfury Giant (ICC_090).
  A scratch copy of the harness (verify/.out/dbg, never committed) printed the state at the rejection: the Giant cost 6, the
  boss had 4 mana and had Overloaded 7 Crystals this game, so by its text the Giant costs 11 - 7 = 4. The boss's search
  state computed 4; the game held 6.
- Cause: ICC_090's AdaptiveCostEffect starts from -OverloadThisGame and on each OVERLOAD event adds the event source's
  printed Overload (`p.Card.Overload`). Vessina's Plaguebringer (family 22) Overloads every spell by 1 through family 17's
  `EncounterRules.OverloadFor`, whose event names the spell: for a spell without printed Overload the Giant counted 0, for
  one with it the printed amount a second time. Any copy of the Giant built from the game's count (as the search's) differed
  from the game's. The same held for Spell Lock (DALA_BOSS_54p, OverloadFor) and Winged Aberration (an Overload event of 2 on
  a card without printed Overload).
- Fix: the trigger adds the event's amount (`CurrentEventData.EventNumber`, set by `TriggerManager.OnOverloadTrigger`),
  which is exactly what `OverloadThisGame` counts. Generic's printed-Overload event passes the card's own amount, so every
  ordinary game is unchanged (no run log other than c-w5fix moved between the pre-fix and post-fix runs; randplay and
  selfplay identical).
- Checks: c-w5fix O1-O3 (Vessina's Plaguebringer: Moonfire, then Lightning Bolt, a Giant in hand throughout; then a Giant
  entering the hand). With the old script O1-O3 fail ("giant 11->11", "giant 11->9", "held 9 new 8"). The enc22a games on
  the fixed engine: 16 games, 0 faults (the faulted game is a boss win in 15 turns).
- Proof that nothing else moved: the pre-fix pass ran runs 1 and 2 and the 14 family-20-23 bosspilot results; after the fix
  every run log equals its pre-fix log except c-w5fix (the O checks) and c-secretsearch's timing lines, and every pre-fix
  bosspilot result equals the post-fix one except enc22a (4 lines: the faulted game and the summary). Pre-fix results:
  verify/.out/runs-pass1-w6, verify/.out/bp-pass1-w6.

9. THE TEKAHN FORM-THRESHOLD CHECK (commit b074311), found by this regression
- `bp.sh tekahn24` (family 24's fixture finale) failed one harness check in both runs: "the boss's Health is above its
  form's threshold" in a 101-turn game (the harness's turn cap; the encounter has no turn limit, wiki Rules). A Health
  trace (the scratch harness) shows Tekahn entering Curse of Flame at 195 (threshold 200) on turn 70, then healing to
  219 by turn 99 and staying in that form.
- Not an engine defect: Tekahn's two forms are power-only and carry no Health of their own (`SwapForm` keeps the hero's
  maximum), as the client has one ULDA_BOSS_67h hero of 300 Health for all three phases; the Plague Lords' phase heroes
  print 200 and 100, which is what caps them at their thresholds. The family 01 invariant holds only where a form's
  Health caps the hero, so the check now applies to forms with their own Health. That is narrower for a form without
  one, said plainly; Tekahn's are the only such forms. The "should be in its next form" check is unchanged.
- The four derived harnesses were regenerated by their generators (only those lines changed), c-bosspilot-f04 got the
  same edit, and all six were rebuilt under the lock; every bosspilot result recorded before the rebuild (the family
  20-24 modes, enc14 and enc15) was run again on the rebuilt harnesses: 29 modes (58 result files,
  verify/.out/bp-preharness: enc20a, enc20b, enc20-seeds-a, enc20-seeds-b, enc21a, enc21b, enc21-seeds-a, enc21-seeds-b,
  enc22a, enc22b, enc22-seeds-a, enc22-seeds-b, enc23, enc23-seeds, enc23-cards, enc24, plague24 95/110/195/210,
  tekahn24, enc14, enc14-seeds, enc14-flight, enc14-cards, enc15, enc15-seeds, enc15-rat, enc15-toys). Every rebuilt
  result (-a and -b) equals its pre-rebuild one byte for byte except tekahn24: 4 differing lines in each of -a and -b,
  the 101-turn ulda_67_tekahn game ("checks FAIL 1" -> "checks ok") and the summary ("failed checks 1" -> "failed checks
  0"); its stderr went from 144 bytes to 0 and its exit from 1 to 0. Every other mode recorded after the rebuild ran
  only on the rebuilt harnesses. No run entry uses the six harnesses (c-bosspilot-encounter is in the pilot list only),
  so the run logs needed no rerun.

10. FILES CHANGED (beyond the merged branches and the merge resolutions of section 2)
- Engine: Actions/EncounterRules.cs (tag comment), Actions/EncounterRules.F17.cs (OverloadFor summary),
  CardSets/DungeonRun/DungeonRunEncounters.F21.cs (PowerOnlyForm removed), DungeonRunEncounters.F22.cs (TombsD.Overload
  folded; Mirage's cast as the entry's AfterDraw), DungeonRunTombsActives.cs (ULDA_115 comments), CardSets/LoeCardsGen.cs
  (comment only), CardSets/IcecrownCardsGen.cs (ICC_090 Snowfury Giant).
- Plugin: DungeonRun/DungeonRunAdmission.cs (the F3 run-treasure row comment).
- Data: run-v1.json (excludeBosses on DALA_Barkeye and DALA_Vessina), reference/encounters/F11.json and F12.json (Witchwood's
  Touch kept), and the regenerated files (encounters-v1.json, catalog-v1.json, dungeon-run-catalog.md, the ledger,
  curation.json).
- verify: bin/build-all.sh, builds.sh, runs.sh, final.sh (labels, the w6 stage), bp.sh and same.sh (baseline labels);
  lib/suites.sh; suites c-catalog, c-deckops, c-enc-model, c-enc-11, c-enc-12, c-enc-19, c-enc-20, c-enc-22 (C9b), c-enc-23
  (D3, D3b, D5), c-w5fix (Q, Q7, O1-O3), the six bosspilot harnesses (b074311); tools/gen/p8f11/make_f11.py,
  patch_curation.py, p8f12/make_f12.py; fixtures p8f02-p8f04, p8f08-p8f11, p8f14, p8f16, p8f17, p8f23, p8f24 (section 3);
  baseline/runs (70 entries; 18 moved, 5 new), baseline/bp (196 results; 146 moved, 21 new), hall.txt unchanged;
  README.md sections 1, 2, 4, 6; MANIFEST.md sections 2, 4, 5, 6.
- Docs: docs/design/dungeon-run.md ("Wave 6"), HANDOFF.md sections 2-4 and 8; handoff/phase8/reports/lane-family-20 ...
  24 reports (copied unchanged) and this report.

11. LEFT FOR LATER (HANDOFF 3.2-3.4)
- The close-out enables acts-v3t's finale on ulda_67_tekahn with `heroRelay` and decides what a DALA_Tekahn run meets
  there (c-w5fix Q7 fails on purpose until then); acts-v3h's Heist finale (DALA_830) still ships disabled too.
- Jythiros of Questions (ULDA_BOSS_58h/58p) and Alternate Timeline (GILA_BOSS_48t) are rejected to phase 9; Enhance-a-matic
  is left out of Toki's deck.
- Captain Shivers starts with Kingsbane, whose engine helper drops enchantment Durability when the copy is shuffled back
  (wave 5's open item, now in a shipped encounter).
- Unscripted Saviors of Uldum collectibles (and Hydrologist) in the decks of families 19-22 are replaced by named scripted
  cards; rerun the encounters step when they get scripts (c-enc-19 S8, c-enc-21 S8).
- Hagatha's incompatible modifiers with the four nemeses as act II majors: a step-9 input.
- BOT_543 Omega Mind is in Tekahn's relay decks only; a finale that offers it is the close-out's.
- The plugin build (Windows only) was not run; the suites compile the plugin sources they test.

12. COMMITS ON wip/int6 (after the merges e12538f, 5c88700, 434ab50, af48a06, 485d10e)
- Stage A: b945aba regenerate and ledger pins; 25ad1b3 same-character exclusions; fde443d Witchwood's Touch in the
  family 11 and 12 decks; cccb25a the folded helpers; e31c3f4 tag comment, final.sh w6, runner counts, HANDOFF 3.4;
  51ce8c6 fixtures; 589f7cd ULDA_115 comments; c949763 merged-tree pins and Plunder after the draw; b507c83 ledger source
  hash.
- Stage B (the merge review's fixes): 1450cf9 Mirage after the draw (c-enc-22 C9b); 561a2cf the inert DALA_Tekahn key
  dropped, Q7 the tripwire, the HANDOFF J1 example; af0d06a the five lane reports.
- Stage C (the regression): c26c003 Snowfury Giant (c-w5fix O1-O3); b074311 the form-threshold check; then the record of
  the wave (baselines, README, MANIFEST, design record, HANDOFF, this report), after gen.sh ran twice more on the final
  tree: byte-identical, no generated-file diff.
