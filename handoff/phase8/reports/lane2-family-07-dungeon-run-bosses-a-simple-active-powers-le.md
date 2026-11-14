PHASE 8 FAMILY 07 (Dungeon Run bosses A, 18 encounters) - LANE 2 REPORT

All 37 ids are done; nothing is rejected. All 18 bosses play through the run setup path with the fair AI as the boss, twice identical, 0 faults / fallbacks / failed checks / swallowed, power-unused 0. Nothing was checked in the real client.

FLAGS FOR THE INTEGRATOR (read first)
1. Shared engine file edited, outside the family: `engine\SabberStoneCore\src\CardSets\Client\Sets\EMERALD_DREAM\EmberrootDestroyer.cs`. The first seed sweeps (SEED_BASE 19000 and 29000) crashed the harness process with a stack overflow in Pathmaker Hamm's fight. Cause: Hamm's wiki deck has Axe Flinger (BRM_016); the boss's search gave the run player Emberroot Destroyer (FIR_955), which kept picking the mortally wounded Flinger, which kept answering. The fix: FIR_955 never picks a mortally wounded minion. RNG use is unchanged when no such minion is on the board. Not run here: selfplay, randplay, hall (check their digests).
2. A harness that overflows its stack does not exit and keeps the shared harness lock. I ended my own two hung processes by pid. My run script now wraps every bosspilot run in `timeout 400`; the shared scripts have no such guard.
3. I scripted two cards outside my id list: Growing Ooze `ICCA07_004` and its enchantment `ICCA07_004e` (The Mothergloop's wiki deck lists the Ooze twice; it was unscripted and in no family's list). If another lane registers them too, `DungeonRunEncounters` throws "registered twice".
4. No private tag used (32642 untouched). No schedule, save, pool-set or ReleasedShapes change.
5. Wiki pages were read through WebFetch, which returns a model's transcription, not raw wikitext (a direct API fetch got 403). Each page was transcribed twice with different prompts; both agree row for row and each table sums to the stated count.

PER ID
- LOOTA_BOSS_04h/04p, 27h/27p (Waxmancer Sturmi; Sculpt Wax, Harden Sculpture): done. Rows loot_04_sturmi (tier 2), loot_27_sturmi (tier 3); deck authentic.
- LOOTA_BOSS_05h/05p, 28h/28p (Pathmaker Hamm; Unstable Explosion, Unstable Demolition): done. loot_05_hamm (tier 1), loot_28_hamm (tier 3); authentic (Mad Bomber x8 kept).
- LOOTA_BOSS_06h/06p, 29h/29p (Blackseed; Evolvomancy, Greater Evolution): done. loot_06_blackseed (tier 2), loot_29_blackseed (tier 3); substituted: Wrath x2 -> Swipe.
- LOOTA_BOSS_09h/09p (Frostfur; Freeze): done, tier 1, authentic.
- LOOTA_BOSS_10h/10p (Graves the Cleric; Light's Will): done, tier 1, substituted. The wiki table names ten later Core-set cards this client does not have (16 of 30 copies); each has a named classic replacement, recorded.
- LOOTA_BOSS_11h/11p, 30h/30p (Overseer Mogark; Cruel Words, Searing Lash): done. Tiers 1 and 3, authentic.
- LOOTA_BOSS_12h/12p (Candlebeard; Charge!): done, tier 1, authentic.
- LOOTA_BOSS_13h/13p (Thaddock the Thief; Tactical Retreat): done, tier 2, authentic.
- LOOTA_BOSS_18h/18p/18t (Giant Rat; Rat Race; Rat): done, tier 1, extended (wiki lists 10 cards: doubled, plus 5 curated fillers x2).
- LOOTA_BOSS_21h/21p (Gnosh the Greatworm; Swallow Whole): done, tier 2, substituted: Gluttonous Trogg x2 (LOOTA_109, family 08) -> Burly Rockjaw Trogg.
- LOOTA_BOSS_23h/23p (Bristlesnarl; Hunter's Call): done, tier 3, authentic. The client has no enchantment card for it; the discount shows as BRM_028e.
- LOOTA_BOSS_24h/24p (The Mothergloop; Gloop): done, tier 3, substituted: Reverberating Ooze x5 (LOOTA_104) -> Grim Patron, Shroomsayer x2 (LOOTA_107) -> Sludge Belcher (both family 08).
- LOOTA_BOSS_44h/44p (Wee Whelp; Baby Breath): done, tier 1, extended.
- LOOTA_BOSS_45h/45p (Bink the Burglar; Coin): done, tier 1, extended.
Tier is by printed hero Health (10/20 -> 1, 30/40 -> 2, 50/60 -> 3). Heroes need no script. Recorded adaptations (the reference file feeds encounterAdaptations): the wiki's auto-cast powers (Hamm both ranks, Gnosh, Bristlesnarl, Mothergloop, Bink) are the AI's choice; the two ranks of a boss are two rows and one run can meet both (the model has no exclusion key).
Shrines: an awake player shrine is destroyed instead of transformed by Evolvomancy / Greater Evolution and goes dormant; a copy of a shrine is a plain minion; Swallow Whole and the random-damage powers never pick a dormant form.

FILES CHANGED
Own family files (lane repo `C:\Users\user\AppData\Local\Temp\claude\0a564ffc8743aeec\d2a4e245-ca37-4433-9b55-c35ed325f081\scratchpad\L2\repo`):
- `engine\SabberStoneCore\src\CardSets\DungeonRun\DungeonRunEncounters.F07.cs` (18 powers, 6 enchantments, the Rat, Growing Ooze; public helpers `EvolvePool`, `HighestAttack`)
- `plugin\DungeonRun\DungeonRunAdmission.F07.cs` (families boss-power-public 15 ids, boss-power-own-hand 3 ids, boss-token 2 ids; all Admit Boss)
- `data\dungeon-run\reference\encounters\F07.json` (new, 18 rows, one wiki URL per row, read 2026-11-13)
Shared files:
- `engine\...\EMERALD_DREAM\EmberrootDestroyer.cs`: flag 1.
- `tools\restoration-ledger\curation.json`: landed-family entry E07 appended after E04 (other lanes append at the same place); the two tool-owned lists were regenerated.
- Existing suites (lane scratch `...\scratchpad\L2\sp`; originals in `p8f07\before\harness`):
  - `audit-verify\c-admission\Program.cs`: one statement adds my three family names to the "table lists only ledger rows" exemption.
  - `audit-verify\c-deckops\Program.cs`: T5 pin.
  - `audit-verify\c-catalog\Program.cs`: ledger golden pin.
  - `p8f01\suites\c-enc-model\Program.cs`: A4 now checks that family 01's eight rows come first; J13 accepts cards of any family 01..25.
Generated in the lane (dropped at merge): encounters-v1.json, restoration-ledger.csv/.md. catalog-v1.json came out byte-identical to the wave 1 main tree. A read-only diff against the main tree shows exactly this file set. All written files are LF, no BOM.

PINS MOVED
| Suite | Old | New | Cause |
|---|---|---|---|
| c-admission total | 2584 | 2653 | the three F07 rows (20 ids) |
| c-admission NOTE, generated lists for both sides | 283 | 303 | 18 powers, Rat, Growing Ooze are Boss-only (boss-only list stays 310) |
| c-deckops T5, ledger rows | 4579 | 4613 | 18 run:boss rows + 16 deck cards |
| c-catalog ledger golden | 4579 rows; 2813 / 40 / 668 | 4613 rows; 2813 / 93 / 649 (implemented / partial / missing) | same |
| c-enc-model A4, J13 | exact 8 rows; all cards family 01 | family-agnostic | new rows and cards |
Unchanged: c-acts A6 DDE3676B5A519BEE, c-deckops L1 8B1B4ED89144446B.

TESTS ADDED (after the merge under `C:\Users\user\AppData\Local\Temp\claude\0a564ffc8743aeec\d2a4e245-ca37-4433-9b55-c35ed325f081\scratchpad\p8f07`; in the lane under `...\scratchpad\L2\sp\p8f07`)
- `p8f07\suites\c-enc-07` (EncF07.csproj, Program.cs): 161 checks. A = each public power against its client text; B = the own-hand powers; C = Rat, Growing Ooze, enchantments; D = the player shrine against every relevant power; F = the Axe Flinger / Emberroot loop; E = rows, plans, pools, admission, validator, and every row drawn on acts-v3h and acts-v3t within 400 seeds.
- `p8f07\run.sh`: steps reference | build | suite | enc07a | enc07b | shrine | seeds | same. It takes REPO / SCRATCH from the environment; defaults are full literal paths so the lane tool rewrites them.
- `p8f07\make_reference.py` + `reference-curation.json`: the transcription source of F07.json (repo root is an argument).
- Evidence: `p8f07\bp-enc07a-a/b.txt`, `bp-enc07b-a/b.txt`, `bp-shrine-a/b.txt`, `bp-damage-loop-evidence.txt`.

RESULTS (final build, after the last source change)
- Engine 0 warnings / 0 errors; plugin 0 errors / 75 warnings. The three generators reproduce byte for byte on a second run: encounters 26 rows, 30 decks (16 authentic, 14 changed), 41 adaptations, 1 rejected; ledger 4613 rows, implemented 2813, partial 93, missing 649, duplicate/support 1058.
- Reduced suites, run once, logs compared with the wave 1 logs: c-content 904, c-runlayer 244, c-acts 181, c-deckops 174, c-shrine-core 131, c-enc-engine 122 (0 differing lines each); c-admission 2653 (4 lines: the NOTE and the total); c-enc-model 141 (4 lines: the two reworded labels). Extra: c-catalog 127, c-enc-secrets 60.
- c-enc-07: 161 passed, 0 failed, two runs identical.
- Bosspilot, 0 faults / fallbacks / failed checks / swallowed, stderr empty, power-unused 0:
  - enc07a: 36 games, boss wins 31, twice identical.
  - enc07b: 36 games, boss wins 30, twice identical.
  - shrine (nine rows, each against one of Rikkar's shrine rows): 37 games, boss wins 32, twice identical.
  - seeds 19000, 29000, 39000, 49000, 59000 (all 18 rows, 2 games each, run once): 36 games each, boss wins 29 / 30 / 27 / 29 / 30.
  - Family 01's eight rows on my build: 20 games, 0 differing lines against the wave 1 log.

TO REDO IN THE MAIN TREE
- Regenerate encounters, catalog, ledger; rerun the p8f02 / p8f03 / p8f04 fixture steps.
- When family 08 scripts LOOTA_104, LOOTA_107, LOOTA_109, Gnosh's and Mothergloop's decks turn authentic by themselves. Then c-enc-07's `rows` table (their kind), E10 and c-enc-secrets-style counts need updating, and encounters / adaptations counts move.
- Recompute the pins above after all lanes merge; settle the E07 entry position in curation.json; add `p8f07` to the wave scripts and to lanes.json `spInclude`.
- Wrath: EX1_154 has no script of its own and the encounters step rejects its Core reprint as not a client card, so Blackseed uses Swipe. If the step accepted the reprint, both Blackseed decks would be authentic.
- `docs\design\dungeon-run.md` is not edited (left to you, as in wave 1).
