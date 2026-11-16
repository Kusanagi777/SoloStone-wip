WAVE 5 INTEGRATION REPORT (families 14, 15, 16, 17, 18, 19, merged on branch wip/int5 from e367c93, the wave-4 tree;
Linux cloud container, .NET SDK 10.0.402; every harness call through the harness lock; nothing pushed by the integrator,
no directory deleted, nothing downloaded; hearthstone.wiki.gg pages read for the review's George the Fallen question)

All six branches are merged, the cross-family items of the brief are done, the three confirmed merge-review findings are
fixed (each with a check that fails without the fix), and the whole regression ran after the last source change.
The regression found one engine defect
(Unstable Evolution's copy escaped the Echo copy floor: an endless (0)-Cost chain in `bp.sh enc15`), fixed with a test;
everything was run again after that fix: 0 faults, 0 fallbacks, 0 failed checks, 0 swallowed leaf exceptions in every
mode, run 1 = run 2 everywhere.
The wave's results are the new baseline (`verify/baseline`, recorded on Linux).

1. MERGES (git merge --no-ff, in the brief's order; every side kept)
| Branch | Merge commit | Textual conflicts |
|---|---|---|
| wip/f14 753f74a | 1ed6a61 | curation.json, suites.sh, bp.sh, c-admission, c-catalog, c-content, c-deckops, c-enc-model; generated files |
| wip/f15 9f3cf38 | 32a0c35 | the same set |
| wip/f16 a074315 | b1515cd | the same set, plus plugin/FairBotState.cs |
| wip/f17 945172a | f16e293 | the same set, plus Actions/EncounterRules.cs (the tag table) |
| wip/f18 796f5dd | dc18a5a | the same as f17 |
| wip/f19 11a9029 | 4c6001e | the standard set |

2. CONFLICTS AND RESOLUTIONS (by hand; every side kept)
- `tools/restoration-ledger/curation.json`: landedFamilies E14-E19 appended after E13 (commas fixed); the tool-owned
  encounterAdaptations taken as a union, then rewritten by gen.sh (618 entries).
- `verify/lib/suites.sh`: c-enc-14 ... c-enc-19 after c-w4fix, then `p8w5/c-w5fix` (this integration): 78 build projects,
  65 run entries (comments in suites.sh, builds.sh, runs.sh, build-all.sh updated).
- `verify/bin/bp.sh`: every usage line, variable (CARDS14-17, AUTO17, A18/B18, A19/B19/CARDS19) and mode block kept.
- `c-admission`: the exemption statements of families 14, 16, 17, 18 and 19 appended (family 15 reused existing names);
  the catalog-lift check covers family 13's TRLA_110 row, family 15's A Small Rock row and family 16's Ol' Toomba row
  (8 CatalogAdmit rows, 16 boss-treasure ids, a Toomba row with 5 ids).
- `c-catalog`: every family comment block kept. `c-content` and `c-enc-model` J13: the passive lists concatenated in
  registration order (12, 14, 15, 16, 17, 18, 19).
- `c-enc-model` A4 "...13:9,14:15,15:14,16:14,17:14,18:16,19:18,23:1,24:1"; E3 merged (every run meets a Heist row and a
  Rumble Run row; Whirt and Voone in some runs only).
- `EncounterRules.cs`: PowerRefresh 32646, ExtortionEscrow 32652 and SelfDamageImmune 32653 all kept.
- `plugin/FairBotState.cs`: one guard kept (section 3).
- Auto-merged shared edits checked by hand: `ResolveUnderbellyRats` first in `ResolveHeroForms`; A Small Rock and Ol'
  Toomba's five treasures leave the core run-treasure rows (c-enc-09 L0 and c-treasures-F2 T2 follow); family 18's
  `Character.TakeDamage`, NativeMatch timer, DungeonRunData/Validator finale twist, schedule-v1.json (DALA_830 and the
  inert finale twist key), c-enc-model A10, c-rules-05.
- Generated files took the first parent and were regenerated after the last merge (gen.sh twice byte-identical: 194 rows,
  215 decks, 618 adaptations, 2 rejected; row, deck and adaptation totals equal wave 4 plus the six families' own).
- Merge fidelity (from the merge review): expected line counts (e367c93 plus each branch's delta from a95b97e) match the
  merged tree for every non-generated file; the encounter rows, decks and sources and the curation lists compare
  structurally; no hunk lost or doubled; every wave-4 row and deck identical to e367c93. The one row that differs from its
  family branch is dala_33_gold_elemental (section 6, finding 1).

3. CROSS-FAMILY ITEMS OF THE BRIEF
- Auto-cast after the turn-start draw (663f81b). New hook `EncounterRules.AfterTurnStartDraw`, called in `Game.MainStart`
  right after `MainDraw()` and before `DeathProcessingAndAuraUpdate`; it runs the current player's started (PowerActive)
  power's `EncounterCard.AfterDraw` task (TaskQueue.Execute + ProcessTasks), skipping a completed game or a hero that is
  ToBeDestroyed; in every other game it costs one dictionary lookup. `AutoCast` (F09.cs) registers
  `Passive(id, empty Power).AfterDraw` instead of a TURN_START trigger (Cost check, condition, NUM_2 count and effect
  unchanged). The 11 auto-cast powers of families 09 (LOOTA_BOSS_20p, 34p, 53p), 15 (DALA_BOSS_16p, 18p), 16 (32p, 39p)
  and 17 (50p, 55p, 56p, 59p) all move. New checks c-enc-16 I6 (the minion drawn that turn is buffed by For The Light!)
  and I7 (a hand of spells plus a drawn minion still casts), both failing with the engine change reverted; c-enc-16 I2
  restaged (Aki's deck is spells, so "no minion in hand" still holds after the draw); c-enc-15 A1 restaged by the review
  (section 6). c-enc-09 166, c-enc-15 103, c-enc-17 123 otherwise unchanged.
- FairBotState chained-Discover guard (b1515cd): family 13's `continue` keeps a queued choice with null Choices out of
  queuedChoices, so family 16's two later guards were redundant; the file is byte-identical to e367c93 and c-enc-16 A8
  passes. `plugin/NativeMatch.cs:6634` is left for the Windows PC.
- Same-character eligibility (d59a522, extended by the review in 008c120): run-v1.json hero-row key `excludeBosses`
  (`DungeonRunClass.ExcludeBosses`); `DungeonRun.NewRun` passes it to `ActBossSequence` (new optional `excludeBosses`),
  which seeds `used` with it. DALA_Squeamlish excludes ulda_07_squeamlish, DALA_Chu ulda_06_chu, DALA_George
  loot_15_george_karl; every row without the key draws exactly as before. The validator refuses an unknown boss id (when
  encounters are loaded) and the key on a class row. c-w5fix Q1-Q6: 600 acts-v3h runs per hero meet the excluded row 0
  times; with the key removed the same seeds meet it 83 (Squeamlish), 88 (Chu), 21 (George) times, so Q3 can fail; the
  Druid, Warrior and Paladin class rows still meet them 83, 88 and 21 times.
- Tunnel Trogg (c66c950): a comment at LOE_018 in LoeCardsGen.cs and an entry in HANDOFF 3.4; no code change.
- `Game.ClearWeapons` (55d8b45): a real one-statement defect, fixed. `Hero.AddWeapon`, after `RemoveWeapon()`, drops
  this hero's pending ClearWeapons entry (and nulls the list when empty). Family 19's `TombsA.ReplaceBrokenWeapon` and its
  call are removed; behaviour identical (c-enc-19 115; F3/F4 fail with the engine fix reverted). New c-w5fix W1-W3 (W1
  fails without the fix).
- Tag table (bbbae7f): 32652 (family 17) and 32653 (family 18) named; families 12-16 and 19 took none; all 19 private
  constants in 32630-32699 unique.
- bp.sh / final.sh (b507033): the usage print shows the whole comment block (41 lines through family 19); final.sh has a
  `w5` stage (14/15/16/19 modes twice per call; 17/18 modes once per call for k = 1 and 2, then `same`; then enc17-check).
  Also fixed: bp.sh enc11-cards used `[A-Z0-9_]+ x0`, which never matches its lowercase-suffixed watched ids (41t, 43t,
  55t2, 57t), the defect of review F14.2; now `[A-Za-z0-9_]+`.
- Pins (9ba625b, be997ff): section 5.
- Fixtures (800f0b2, a6b4bea): after gen.sh the 17 data-derived generators (every make_fixture.py, make_bp_collect,
  make_bp_rules, make_bp_rules06, make_harness) ran twice, byte-identical: the encounter fixtures of p8f02, 03, 04, 08, 09,
  10, 11, 14, 16, 17 and the schedule fixtures of p8f05 and p8f06 (family 18's DALA_830 row) were rebuilt; p8f18's finale
  schedule and the four generated harnesses came out unchanged. After the review's F16.json change they ran twice again:
  only the sources line of the ten encounter fixtures changed. The wiki make_fNN.py generators need gitignored pages and
  were not run, except make_f16.py on the family 16 lane's pages (byte-identical to the corrected F16.json).

4. SEMANTIC CHECKS (script in the integrator's scratchpad, run on the final tree: ALL OK)
- Private tags unique; no family file holds a raw private tag number.
- Encounter registrations: 361, none twice (AutoCast and TagPassive included); none also scripted by `cards.Add`.
- Admission: 870 listed ids, none in two rows.
- 74 tracked JSON files parse with no duplicate keys. encounters-v1.json: 194 rows, 215 decks, ids unique; the reference
  F files hold the same 194 rows, no id in two files; schedule-v1.json: 44 rule rows, ids unique.
- No duplicate switch case or dictionary key in the 47 changed .cs files. No CR and no BOM added (the four engine files
  with a BOM already had it at e367c93).
- gen.sh twice byte-identical after the merges, after the run-v1.json change and after the review fixes.

5. PINS RECOMPUTED FROM THE MERGED TREE (none weakened)
- c-catalog ledger golden: 4760 / 2813 / 386 / 503 -> 4918 rows / 2813 implemented / 660 partial / 387 missing / 1058
  duplicate/support. +158 rows, not the families' 189: 18 deck cards are new in two or three families (20 repeats) and 11
  were already new in wave 4. All 158 new rows are partial, and 116 rows went from missing to partial.
- c-deckops T5: 4760 -> 4918 (91 run:boss rows, 67 deck cards).
- c-admission: 3301 -> 3772 passed (+80, +68, +71, +92, +89, +71 for families 14-19); NOTE generated lists 461 -> 582.
- c-content: the passive list merged; 904.
- c-enc-model (141): A4 as above; A10 family 18's; E3 "heist in 1000, whirt in 28, a Rumble Run row in 1000, voone in
  368, Hagatha the act II major in 567, Icarax the act III major in 510"; E2 whirt 28, voone 368; E4 no-ledger 96 -> 46;
  G10 seed 4001 -> 4002 (Voone in act 2, a shrine-free act 1); G1 equals the baseline (DALA_866,DALA_853,none); B12, J9,
  J13 pass.
- c-enc-10 P6 and c-enc-12 P6 (be997ff): family 18 reuses the admission family names boss-own-hand-power and
  boss-hidden-shuffle, so P6 now sums only the rows of that name holding an id of the family; the same rows as before,
  pins 3 and 1, and the family-name set check is unchanged (a family row that lost its ids still fails it). Not weaker.
- Weaker than before, said plainly: c-enc-model E3 and E2 no longer require Whirt (or Voone) in every run, because the
  Heist rows of families 14-18 share Whirt's provenance; E3 still requires a Heist row and a Rumble Run row in every one of 1000 runs.

6. MERGE-REVIEW FINDINGS (adversarially verified; three confirmed, none rejected; fixed in 008c120, 11f8dbb, a6b4bea)
- Finding 1 [low], fixed: Gold Elemental's deck changed in the merge and family 16's adaptation note said the opposite.
  Family 16's Heroic pool for dala_33_gold_elemental begins with Infuriated Banker (DALA_502), which family 17 scripts and
  admits (heist-d-boss-card), so the merged gen.sh took it as a Heroic fill: wip/f16's fills EX1_319, LOOT_014 became
  DALA_502, EX1_319 (LOOT_014 out, DALA_502 in; the ledger row DALA_502 now lists DALA:boss-deck:enc-dala_33_gold_elemental).
  The note ("has no script yet; the Normal pool and its second copies fill the 30 cards before the Heroic pool is
  reached", wrong in its second clause on wip/f16 already) now reads "Infuriated Banker (DALA_502, the boss card and first
  card of his Heroic pool) is family 17's card, scripted since the wave-5 merge: the Normal pool and its second copies
  give 28 cards, the Heroic pool's first two (Infuriated Banker, Flame Imp) the last two." in make_f16.py and F16.json
  (make_f16.py on the family 16 lane's saved pages reproduces F16.json byte for byte) and, through gen.sh, curation.json.
  New check c-w5fix N1: no encounter note (498 notes in the F files) says "no script yet" of a card an encounter deck
  holds; with the old note it fails ("1 of them say so", DALA_502). Family 16's and 17's bosspilot lines also move with
  this deck (section 7).
- Finding 2 [medium], fixed: the same-character rule covered Squeamlish only. Mr. Chu (DALA_Chu, "This black-market
  bodyguard is the new muscle for E.V.I.L.") is the Tombs boss ulda_06_chu "Mister Chu" (family 19's wiki page: a Heist
  henchman, Madam Goya's bodyguard at the Black Market), the same case as Squeamlish. Checking the other hero rows as the
  review asked: George the Fallen (DALA_George) is the George of the Kobolds boss loot_15_george_karl "George and Karl"
  (hearthstone.wiki.gg/wiki/George_the_Fallen, Lore, read 2026-11-16: "George and his friend Karl ... They later make an
  appearance as a boss in Dungeon Run"); no other run hero row (Heist, Monster Hunt, Rikkar, the Tombs explorers) has a
  boss row of its character. DALA_Chu now excludes ulda_06_chu and DALA_George loot_15_george_karl. c-w5fix Q2 asserts
  exactly the three pairs (it pinned Squeamlish alone before); Q3-Q5 run 600 acts-v3h seeds for each pair: 0 meetings with
  the key, 83 / 88 / 21 without it, and the Druid / Warrior / Paladin class rows still meet those bosses 83 / 88 / 21
  times. With the two new keys removed Q2 and both new Q3 checks fail (88 and 21 meetings). The catalog follows the new
  run-v1.json hash (gen.sh twice byte-identical, counts unchanged).
- Finding 3 [medium], fixed: c-enc-15 A1 (Equivalent Exchange) passed only by the seed after the auto-cast move: Alchemist
  Wendy drew a Yeti before the power acted, so her hand was {Moonfire, Yeti} and ExchangeRandomCards picked index 0 by
  seed 7 (A1 and A3b both depended on it). Her deck is now set aside for that turn (restored before A4), and A1 also
  asserts that each hand holds exactly one card after the exchange; without the set-aside A1 fails ("hands 2/1"). The only
  other auto-cast condition reading the boss's own hand is DALA_BOSS_39p, whose check c-enc-16 I2 stage A had restaged.

7. THE REGRESSION (after the last source change, commit e3e8d42; logs verify/.out, results verify/baseline)
- Engine: 0 warnings / 0 errors (Linux md5 a9483c3c69d3444c0608c0aea06409ba). build-all: 120 entries exit 0 with 0 errors
  (engine, catalog tool, 78 suites, randplay, 39 hall projects). gen.sh twice byte-identical (section 4 numbers, all six
  generated files unchanged). Fixture generators: twice byte-identical after the last data change (section 3).
- runs.sh 1 and 2 (65 entries, in three locked slices each): every entry exit 0, run 1 = run 2 (c-secretsearch: timing
  lines only, SUMMARY 689 / 0). same.sh vs the wave-4 logs, every difference explained:
  - c-admission 4 lines (3301 -> 3772 and the NOTE 461 -> 582); c-enc-model 12 lines (section 5: A4, E2, E3 and its
    details, E4, G10).
  - c-rules-05 16 lines: T2's label (family 18's The Finale, a run-layer twist); T7, T10 and T12 list DALA_830 among the
    twist candidates their acts may still roll; T8 "Icarax act 3" -> "act 1" and T9's act-I candidate list (no ULDA_706: a
    No-Face-excluding boss is now in that act), both because the new rows move the test runs' act walks; T16/T17 the
    fixture rolls DALA_829x357, DALA_832x366, DALA_831x57, DALA_833x71 (threshold >= 15) over 572 acts with a shrine boss
    (Voone is met in fewer runs).
  - c-rules-06 4 lines: W15's act-I candidate list (no DALA_852 now, the same act-walk cause) and W23's plague roll counts
    over 300 runs (804x208, 801x190, 803x251, 802x251).
  - c-enc-11 J6 (2 lines): the example core-row treasure is LOOTA_814, since family 16 moved LOOTA_811 to its own row (as
    c-enc-09 L0). c-enc-13 V3 (2 lines): 1116 -> 846 champion fights over the 360 shrine runs (the champions are a smaller
    share of the pools), still no mirror. randplay 2 lines: regkeys 2363 -> 2479 (+116 snapshot keys for the new
    registrations) and its hash; cards, fingerprint, TB count and the game digest 40207800B289E504 unchanged.
  - New suites: c-enc-14 117, c-enc-15 103, c-enc-16 124, c-enc-17 123, c-enc-18 109, c-enc-19 115, c-w5fix 19. selfplay:
    games identical to the baseline, digest 2CE546A18F96DCBC.
  - Every other entry: 0 differing lines (c-heroes-F6, c-treasures-F7/F8 and the other run-layer suites included).
- Bosspilot: 175 results (the 152 of wave 4 and 23 new), every mode twice identical, 0 faults / fallbacks / failed checks /
  swallowed leaf exceptions, stderr empty for every encounter, rules and cross mode (the released and shrine harnesses:
  their one diagnostic line, 45 or 46 bytes per run). Games / boss wins (differing lines vs the wave-4 record):
  - New family modes (no record; the lanes' own results in parentheses): enc14 30/15 (30/23), enc14-seeds 30/18 (30/21),
    enc14-flight 8/7 (8/8), enc14-cards 12/4 (12/7; all 5 boss cards played), enc15 28/18 (28/17), enc15-seeds 28/15
    (28/20), enc15-rat 8/3 (8/5), enc15-toys 8/5 (8/7), enc16 28/14 (28/16), enc16-seeds 28/17 (28/15), enc16-carousel
    8/8 (8/8), enc16-cards 18/12 (18/14), enc17 28/21 (28/18), enc17-seeds 28/15 (28/22), enc17-void 6/4 (6/6),
    enc17-cards 20/6 (20/9; enc17-check: all 7 boss cards played), enc18a 12/7 (12/10), enc18b 12/9 (12/8), enc18-finale
    8/6 (8/6), enc19a 18/13 (18/10), enc19b 18/15 (18/14), enc19-seeds-a 18/12 (18/11), enc19-seeds-b 18/13 (18/13). They
    differ from the lanes' runs on every game: a bosspilot seed is seedBase + 1000 x the boss's row index, and on the merged
    tree each family's rows sit after wave 4's 34 rows and the families merged before it (the lanes branched from a95b97e,
    before wave 4), so every seed and hero rotation moved; the games also run with wave 4's rules (the Echo floor, the
    mirror-champion exclusion), the other families' cards and rows, the auto-cast move (families 15, 16, 17), Gold
    Elemental's merged deck (enc16, enc16-seeds) and the LOOT_504t floor.
  - vs wave 4, the w4 set: enc12 26/16 (16), enc12-seeds 26/19 (12), enc12-hidden 18/13 (12), enc11-pete 6/2 (12),
    enc13a 30/27 (2), enc13b 24/15 (32), enc13-seeds 27/24 (18), enc11a 28/16 (6), enc11b 24/17 (30), enc11-cards 24/21
    (50; the four cards played), enc11-shrine 37/29 (10; 9 of 9 shrine fights).
  - enc set: enc09 22/15 (6), enc09-seeds 22/17 (8), enc09-auto 16/11 (10), enc09-finals 20/20 (0), enc09-rooms 16/7 (2),
    enc09-cards 24/24 (50), enc10 30/25 (16), enc10-nab 10/9 (10), enc10-seeds 30/25 (4), enc10-hagatha 6/6 (0), enc10a
    32/18 (16), enc10b 28/21 (6), enc10-cards 36/16 (74), enc10-shrine 37/32 (4).
  - released: run 40/27, act 40/26, fixture 40/27, catalog 40/27, shrine 40/24, S2 40/23, S3 40/22, U 40/25, T1 40/13,
    T2 40/20, D 40/14: 0 lines each.
  - w2: enc01 20/18 (10), secret4 24/19 (6), rules01 DALA_850 10/8 (4), DALA_864 10/8 (4), DALA_866 10/10 (4), enc07a
    36/31 (0), enc07b 36/33 (16), shrine07 37/32 (4), seeds07-19000 36/30 (8), enc08 34/27 (8), enc08-tag 8/8 (10),
    enc08-cards 12/11 (26; the four cards played), enc08-trapped 6/5 (0), enc08-seeds 34/26 (6).
  - w1: old01 12/12 (8), secretfix 6/5 (0), plague 95/110/195/199/210 2/2 each (4 each), finale 8/8 (6), relay 2/1 (0),
    collectA 32/21 (66; all 33 cards played), collectP1-4 6/5, 6/6, 6/6, 3/3 (0 each), enc03 a/b/c 24/24, 24/23, 16/16
    (50/50/34), enc01-f03 20/18 (10), icarax2 4/4 (8), f04-boss 40/40 (82; all 23 cards played), f04-player 24/17 (50),
    f04-icarax 0/110/210 4/4 each (8/6/8).
  - rules06 x21: 16 games each (12 for ULDA_706), 8 lines each (6 for ULDA_714, 0 for ULDA_721): the dala_21_whirt and
    ulda_44_trap_room games and the summary. cross06 x21: 12 each (11 for ULDA_719, ULDA_804), 10-14 lines each: the
    gila_27/29/33/34 and loot_53_inara games. rules05 x18: 16 each (12 for DALA_831, DALA_833), 6-8 lines (whirt, trap
    room). cross05 x18: 14 each (13 for DALA_852, DALA_853), 4-6 lines (loot_15_george_karl, loot_24_mothergloop).
  - Why lines moved: 130 of the 152 records moved, the same causes as waves 2-4. (a) Every harness that walks acts-v3h to a
    boss's floor meets the 91 new rows in the pools, so it fights with another run deck: in the moved games either the run
    deck's size at the fight differs or, at the same size, its cards do. (b) The fixture modes (enc09-cards, enc10-cards,
    enc11-cards, enc11-pete, enc08-cards, collectA, enc03, f04-boss, f04-player) move on nearly every line because their
    fixtures were rebuilt with the new rows before the fixture rows (seeds follow the row index). (c) The engine changes
    of the wave move none of these records by themselves: run on the wave-4 data (encounters-v1.json and
    schedule-v1.json of e367c93, through ENCOUNTERS_JSON / SCHEDULE_JSON) the merged engine reproduces the wave-4 records
    of cross05 DALA_829 (all 14 games), cross06 ULDA_702 (all 12, Inara included) and enc09-auto (all 16 games of family
    09's four auto-cast bosses) line for line (logs in the integrator's scratchpad). Family 09's auto-cast powers do not read
    the boss's hand, so acting after the draw changes nothing there; the move matters for Equivalent Exchange and For The
    Light! (c-enc-15 A1, c-enc-16 I6/I7), whose modes have no record. The released, shrine and Aleatory (ULDA_721) runs
    did not move.
  - Two coverage checks print a miss, neither a fault: `enc16-cards` "UNPLAYED: LOOTA_811" (Orb of Destruction, three
    copies in fix16_toomba's deck) and `enc03-check` "NOT PLAYED by a boss: ULD_169" (one copy in fix03_shaman's deck of
    eight games). fix03_shaman in 16 games plays ULD_169 x4 (verify/.out/tmp/enc03-uld169.txt). Orb of Destruction is a
    legal option (a scratch copy of the harness, never committed, printed it among the boss's options from turn 6) that the
    boss's AI does not pick in these seeds: 0 in 12 games of the row (verify/.out/tmp/enc16-loota811.txt) and 0 in 12 with
    SEED_BASE 19000; on the family 16 lane's own fixture file (the lane's row indices, so the lane's seeds) the merged
    engine plays it x2 in 6 games, and c-enc-16 T plays every one of the five treasures for a boss directly. Wave 4's two
    misses (collectA GIL_655, enc03 ULD_173) are played on this tree.
- Hall: harness + 38 audits, 39 of 39 SUMMARY lines equal to baseline/hall.txt; 955 tests / 8285 checks, 0 bad, 0 failed.

8. THE UNSTABLE EVOLUTION FIX (commit e3e8d42), found by this regression
- First bosspilot pass (before the fix, after the review fixes): enc15 and enc15-seeds had 1 fault each, stable in both
  runs: "Exception: stuck after 1500 actions" in the run player's turn 21 against Rasil (dala_27_rasil), DALA_Vessina
  playing Unstable Evolution on a Clockwork Giant again and again. Rasil's Millhouse Manastorm made the run player's
  spells cost (0); no minion costs (1) more than Clockwork Giant, so it stays, and each cast added a new (0)-Cost copy
  (LOOT_504t) to the hand. Wave 4's Echo copy floor (EncounterRules.EchoCopyFloor) covers GHOSTLY cards only, and the
  engine's LOOT_504t copy is none (wave 4 named it for Anomaly - Arcane's floor through DungeonRunEncounters.Repeatable).
  The client prints Unstable Evolution with Echo since patch 23.0.3 (hearthstone.wiki.gg/wiki/Unstable_Evolution, read
  2026-11-16: changed from "Repeatable this turn" to stop a (0)-Cost loop), so the floor now covers LOOT_504t too; the
  card itself still costs (0) under Millhouse.
- Check: c-w5fix E1-E3 (Millhouse played by the boss, the run player's Unstable Evolution at (0), its copy at (1), three
  copies with 3 Mana, then the copy waits). Without the fix E2 and E3 fail ("copy cost 0"; "casts 50 mana 3").
- Proof that nothing else moved: the fix came after a first pass of runs.sh 1 and 2 and the 23 family modes; after it,
  every run log equals its pre-fix log except c-w5fix (the new E checks) and c-secretsearch's timing lines, and every
  pre-fix bosspilot result equals the post-fix one except enc15 and enc15-seeds (4 lines each: the faulted game, now a
  boss win in 30 and 22 turns, and the summary). Pre-fix results: verify/.out/runs-prefix5, verify/.out/bp-prefix5.

9. FILES CHANGED (beyond the merged branches)
- Engine: Actions/EncounterRules.cs (AfterTurnStartDraw, tag comment), Actions/EncounterRules.F12.cs (the LOOT_504t floor),
  Model/Game.cs (one hook line), Model/Entities/Hero.cs (ClearWeapons), CardSets/DungeonRun/DungeonRunEncounters.cs
  (AfterDraw field; Passive returns its entry), DungeonRunEncounters.F09.cs (AutoCast), DungeonRunEncounters.F19.cs
  (workaround removed), CardSets/LoeCardsGen.cs (comment only).
- Plugin: DungeonRun/DungeonRunData.cs, DungeonRun.cs, DungeonRunValidator.cs (excludeBosses).
- Data: run-v1.json (excludeBosses on DALA_Squeamlish, DALA_Chu, DALA_George), reference/encounters/F16.json (the Gold
  Elemental note), and the regenerated files.
- verify: bin/bp.sh, final.sh, builds.sh, runs.sh, build-all.sh, same.sh (labels); lib/suites.sh; suites c-catalog,
  c-deckops, c-enc-10, c-enc-12, c-enc-15, c-enc-16, plus the merge resolutions; new p8w5/c-w5fix; tools/gen/p8f16/
  make_f16.py; fixtures p8f02-p8f17 (section 3); baseline/runs (65 entries), baseline/bp (175 results); README.md
  sections 1, 2, 4, 6; MANIFEST.md sections 2, 4, 5, 6.
- Docs: docs/design/dungeon-run.md ("Wave 5"), HANDOFF.md sections 2-4 and 8; handoff/phase8/reports/lane-family-14 ... 19
  reports (copied unchanged) and this report.

10. LEFT FOR LATER (HANDOFF 3.4)
- The plugin build (Windows only) was not run; the suites compile the plugin sources they test. Family 18's NativeMatch
  turn timer (Norroa's 25 seconds, Nozdormu's 15) needs that build and a check in the client; NativeMatch.cs:6634's
  chained-Discover hazard waits for it too.
- Family 16 flag 7: the own-deck ledger under-informs the boss when revealed shuffled-in cards are later burned (never a
  leak). Kingsbane's engine helper has Sword Dance's old Durability gap. The engine's Lackey pool can hand out the
  unscripted Titanic and Draconic Lackeys. Family 18's rejections (Commander Bolan 75h/75p, Phase Spider 61t, Cheerful
  Spirit DALA_501) are recorded with their reasons for the close-out.
- The acts-v3h finale block (with DALA_830) ships disabled; the close-out enables it and re-pins the shape digest.
- Not checked in the real client: PREMIUM (Bling it On!) and the renamed Exotic Pet, Norroa's timer, Blackmail's escrow
  for the human player.

11. COMMITS ON wip/int5 (after the merges 1ed6a61, 32a0c35, b1515cd, f16e293, dc18a5a, 4c6001e)
- Stage A: 8869ed1 regenerate; 9ba625b ledger pins; 663f81b auto-cast after the draw; 55d8b45 ClearWeapons; d59a522
  excludeBosses; 776347d c-w5fix and its registration; c66c950 Tunnel Trogg note; bbbae7f tag comment; b507033 final.sh w5
  and the enc11-cards pattern; 691f016 catalog regenerated; 800f0b2 fixtures; be997ff P6.
- Stage C: 008c120 the review's Chu / George exclusions and the Gold Elemental note (with c-w5fix Q, N1); 11f8dbb c-enc-15
  A1; a6b4bea fixtures for the new F16.json hash; e3e8d42 the LOOT_504t Echo floor (c-w5fix E1-E3); 54116a7 the six lane
  reports; then the record of the wave (baselines, README, MANIFEST, design record, HANDOFF, this report).
