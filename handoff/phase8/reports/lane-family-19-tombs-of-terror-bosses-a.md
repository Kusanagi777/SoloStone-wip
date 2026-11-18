FAMILY 19 (Tombs of Terror bosses A) REPORT. Worktree /home/user/wt/f19, branch wip/f19, one commit: e67bbdc feat(dungeon-run): family 19 Tombs of Terror bosses A (ULDA_BOSS_01-18, Sand Trap). The tree is clean, nothing was pushed and no harness is running.

All 37 ids are done; none is rejected. All checks below pass, twice, with identical logs.

## Flags for the integrator (read first)

1. **No model tag was used**, not even my reserved 32654. The only state is E.V.I.L. Promotion's count, which sits in the power's own public tag (TAG_SCRIPT_DATA_NUM_1). No extra tag, no save field, no schedule default, finale, act, pool set, ReleasedShapes or golden sequence was touched.

2. **Engine defect, worked around only in my own file.** `Game.ClearWeapons` stores the hero id, not the weapon.
   - If a weapon reaches 0 Durability and a new weapon is equipped before the death pass, the death pass removes the new weapon.
   - I found this through Mister Chu's "Confiscated" Goods (a kill with the last Durability point lost the new weapon). c-enc-19 F3 failed before the fix.
   - The fix is local: `TombsA.ReplaceBrokenWeapon` in DungeonRunEncounters.F19.cs drops the pending clear before equipping. It touches the engine-internal `game.ClearWeapons`, but only from F19.
   - Any other "equip after attack" card could hit the same thing. An engine-wide fix (in Hero.AddWeapon) is left for later.

3. **Open eligibility question (not fixed).** The run hero row DALA_Squeamlish (a Heist hero) can meet the boss ulda_07_squeamlish, the same character.
   - The original Tombs had no such rule (its heroes were the explorers), and boss rows have no hero-exclusion field.
   - Fixing it needs edits to the shared files DungeonRun.cs, DungeonRunData.cs and the tool. Recommended for the close-out or step 9.

4. **Unscripted Uldum collectibles are replaced, not scripted.** I did not edit UldumCardsGen.cs (a shared file). Those cards are swapped for named scripted replacements in the decks (list below).
   - Engine gap seen in passing: Titanic Lackey ULD_616 and Draconic Lackey DRG_052 are unscripted, but the engine's Lackey pool (AddLackeyTask, used by EVIL Cable Rat) can still hand them out with no generation filter. This was already true before my change.

5. **Sand Trap in the boss's view.** The boss's fair view masks the run player's whole deck, Sand Trap included. So the boss knows less than it could, never more. c-enc-19 H2 asserts this; the admission reason says so.

6. **Shared edits:** listed under "Files" below.

## Per id

All 37 are done. The 18 heroes ULDA_BOSS_01h–18h need no script.

**Clickable powers:**
- 01p Boom Bullets: summons a Goblin Bomb (BOT_031); every other Goblin Bomb on either board gets +1 Attack.
- 02p Mummy Ritual: a new copy of a dead Reborn minion of the boss's, at 1 Health; the copy keeps its card's Reborn.
- 04p Belch: a copy of a dead minion of either player. A run shrine is never raised (a dead shrine is a corpse).
- 05p Venomous Stinger: marks any minion not yet stung; its next damage destroys it. A stung shrine goes dormant.
- 07p Druid of the Rat: the boss's minions get +1/+1.
- 08p Sand Surge: a public shuffle of Sand Trap into the run player's deck.
- 09p Armor Repairs: 4 Armor below 10 Armor, else 2.
- 10p Well Equipped: a random Rogue spell or weapon through the generation filter; no Quests, no Secrets.
- 12p Brick by Brick: the boss Discovers an Elemental (its class or neutral, through the filter); the AI picks and the card costs (2) less.
- 13p Snakeshot: a 1/1 Snake with Poisonous.
- 15p Puffertrooper: Rush for the boss's Murlocs that lack it.
- 17p Spreading the Curse: a Leper Gnome with Rush.
- 18p Plagued Horde: gives a minion "Deathrattle: 2 damage to the enemy hero"; the enemy is the one of that minion's controller.

**Passive powers:**
- 03p E.V.I.L. Promotion: a played Lackey gets +N/+N, N rising with each Lackey.
- 06p "Confiscated" Goods: after the boss's hero kills a minion by attacking, it equips a random scripted weapon.
- 11p Unstable Weaponry: when a Mech is summoned for the boss, 2 damage to a random enemy.
- 14p Sword Dance: a destroyed weapon of the boss's is copied into its deck with its enchantments (Kingsbane's rule, a public shuffle).
- 16p Under Wraps: a minion the boss plays gets Reborn.

**ULDA_030 Sand Trap:** Casts When Drawn. The top 2 cards of the holder's deck go to its graveyard, then the holder draws.

Ten client enchantments are scripted with them (01e, 03e, 05e, 07e, 12e, 13e, 15e, 16e, 17e, 18e).

## Tier rule and the shared Tombs tier helper

All Tombs heroes print 10 Health, so Health comes from the act curve and the tier comes from the boss's level in its chapter, read from the wiki deck table.

The rule lives in `verify/tools/gen/p8f19/tombs_tiers.py`, for families 20–22 to import:
- `SHAPES`, `tier(shape)`, `level(shape)`, `schedule_note(chapter, shape)`, `deck_note(shape)`.
- How to import it is in the file header (insert its folder into sys.path, since `-I` hides it).

| Wiki deck table | Levels | Tier |
|---|---|---|
| "10; 15" possible cards | 1–2 | 1 |
| "15; 20" | 2–3 (upper level 3 taken) | 2 |
| 25-card level-4 list | 3–4 | 2 |
| 30-card list with a "level 6" note | 5–6 | 3 |
| full 30-card list with no lower-level note | 7 | 4 |

The level-7 band is inferred from the deck shape (for example Sazin in chapter 1). Each row records its wiki chapter. The client's hero tags 1202–1205 match those chapters, and c-enc-19 S3 checks it.

Result: tier 1 Scalesnout, Herring; tier 2 Vera, Glack, Demolisher, Terraviss, R.O.B.O.L.D., Pyramad, Battrund, Ichabod, Twizzleflux; tier 3 Zafarr, Nash, Sinkhole, Sirinell; tier 4 Cardish, Chu, Squeamlish.

**Decks:**
- Nash and Sirinell are the wiki's lists unchanged. Short lists are brought to 30 with second copies, then the boss's Heroic list.
- Unscripted cards and their replacements:
  - Murmy → Micro Mummy (Zafarr), Bluegill Warrior (Herring)
  - Generous Mummy → Restless Mummy
  - Kobold Sandtrooper → Leper Gnome, Annoy-o-Tron (R.O.B.O.L.D.), Flame Imp (Twizzleflux)
  - Injured Tol'vir → Frostwolf Grunt; Sunstruck Henchman → Chillwind Yeti
  - Weaponized Wasp → EVIL Genius; Livewire Lance → Goblin Lackey (Vera), Fiery War Axe (Chu)
  - Sinister Deal → Kobold Lackey (Vera and Twizzleflux)
  - Crystal Merchant → Enchanted Raven; Quicksand Elemental → Tar Creeper
  - Ogre Brute → Ironfur Grizzly; Living Monument → Anubisath Sentinel; Spitting Camel → Loot Hoarder
  - Expired Merchant → Voidwalker; EVIL Recruiter → Imp Gang Boss
  - Hunter's Pack → Animal Companion (Hunter's Pack can give a Secret, and this row generates none)
- The Lackey replacements keep Vera's power fed.
- Wiki sources were read 2026-11-14, one boss page per row; raw pages are in the gitignored verify/.out/tmp/wiki.

## Files

**Own files:**
- engine/SabberStoneCore/src/CardSets/DungeonRun/DungeonRunEncounters.F19.cs (helpers in a nested class `DungeonRunEncounters.TombsA`, so no name can collide with families 17/18)
- plugin/DungeonRun/DungeonRunAdmission.F19.cs — six rows: boss-tombs-board-power (8), -graveyard-power (2), -own-hand-power (2), -passive (5), -player-deck-insert (1), -card (ULDA_030, Player|Boss, never generated)
- data/dungeon-run/reference/encounters/F19.json (written by make_f19.py)
- verify/suites/p8f19/c-enc-19/ (Enc19.csproj, Program.cs)
- verify/tools/gen/p8f19/: make_f19.py, tombs_tiers.py, patch_curation.py

**Shared edits:**
- tools/restoration-ledger/curation.json: landed family E19 appended after E10 by patch_curation.py (the generator's self-check needs it). The two tool-owned lists were rewritten by gen.sh.
- verify/suites/audit-verify/c-admission/Program.cs: one statement admitting the `boss-tombs-*` families into the table-id exemption, as families 09/10 did.
- verify/suites/audit-verify/c-content/Program.cs: passive registry pin gets the five new passives.
- verify/suites/audit-verify/c-catalog/Program.cs: ledger golden.
- verify/suites/audit-verify/c-deckops/Program.cs: T5 row count.
- verify/suites/p8f01/c-enc-model/Program.cs: A4 family counts and J13 passive list.
- verify/lib/suites.sh: c-enc-19 appended to BUILD_LIST and RUN_LIST.
- verify/bin/bp.sh: usage line, A19/B19/CARDS19 variables, four case lines after family 10.
- Generated (regenerated, not hand-edited): encounters-v1.json, restoration-ledger.csv/.md.

## Pins moved (suite, old → new, cause)

- **c-admission:** 3167 → 3238 passed (six new rows); generated lists 426 → 445.
- **c-content passive list:** the five new passives (ULDA_BOSS_03p, 06p, 11p, 14p, 16p) appended; still 904.
- **c-catalog ledger golden:** 4704/2813/295/538 → 4735/2813/345/519.
  - +31 rows: 18 boss rows plus 13 deck cards new to the ledger (BOT_538, CFM_646, DAL_614, DAL_739, DAL_743, EX1_102, EX1_390, FP1_029, GVG_013, GVG_086, GVG_098, OG_327, ULD_326t).
  - 19 rows move from missing to partial (18 powers and Sand Trap). The boss heroes stay missing (run:default-schedule).
- **c-deckops T5:** 4704 → 4735.
- **c-enc-model:**
  - A4 gains "19:18".
  - J13 passive list gets the five passives.
  - Detail lines only: E3 Hagatha as act II major 506 → 577 and Icarax as act III major 1000 → 509 (the provenance-mix rule, now that ULDA has many combat rows); E4 no-ledger 96 → 49; G10 Voone act 1 → 2. Still 141 passed.
- Not my change: the F2 file lengths in c-enc-model already differ from the Windows baseline on Linux; they are identical to the main checkout's Linux run.

**Generators:** encounters "87 rows, 91 decks (45 authentic, 46 changed), 227 adaptations, 1 rejected"; catalog unchanged ("506 entries … 6508 admitted and 1 excluded"); ledger rows 4735, implemented 2813, partial 345, missing 519, duplicate/support 1058, adapted 0.

## Tests added and registered

- **c-enc-19:** 113 checks, sections A–R (one per boss) and S (rows, tier rule, client texts and Costs, admission, validator).
  - Covers both shrine cases (Belch never raises a dead shrine; a stung shrine dies and goes dormant).
  - Sand Trap: drawn by the player, by the boss, on a one-card deck (fatigue), with a full deck; plus the fair-view masking.
  - Sword Dance: through the own-deck ledger.
  - E.V.I.L. Promotion: the count carries into the fair view.
- **Bosspilot modes** (`bp.sh`, each twice per call; a = ULDA_BOSS_01–09, b = 10–18). Every mode: identical across both runs, 0 faults / 0 fallbacks / 0 failed checks / 0 swallowed, power-unused 0, stderr empty, no recorded baseline yet.

| Mode | Games | Boss wins | Watched cards played |
|---|---|---|---|
| enc19a | 18 | 10 | ULD_326t x3, DAL_606 x1, DAL_614 x3 |
| enc19b | 18 | 14 | DAL_614 x1 |
| enc19-seeds-a (SEED_BASE 19000) | 18 | 11 | ULD_326t x2, DAL_606 x2, DAL_614 x10, DAL_739 x3 |
| enc19-seeds-b (SEED_BASE 19000) | 18 | 13 | DAL_606 x3, DAL_614 x3, DAL_739 x3 |

## Lane verification (final binaries)

- Engine: 0 warnings / 0 errors (md5 3da2a3598177d77a806942bd6124c4be on Linux).
- gen.sh twice: all six files byte-identical, and equal to the commit.
- Suites, twice, run 1 = run 2:
  - c-content 904, c-admission 3238, c-runlayer 244, c-acts 181, c-deckops 174, c-catalog 127, c-shrine-core 131/0, c-enc-model 141, c-enc-engine 122, c-enc-19 113.
  - vs baseline: c-content, c-runlayer, c-acts, c-deckops, c-catalog, c-shrine-core and c-enc-engine have 0 differing lines; c-admission 4 and c-enc-model 12 lines, all explained above.
- Bosspilot: the four modes above, rerun after the final engine build.
- Not run, per the brief: hall, full runs.sh, selfplay, randplay, released modes, other families' modes.

## Paragraph for docs/design/dungeon-run.md

"Family 19 (Tombs of Terror bosses A): eighteen Tombs encounters ULDA_BOSS_01–18 and Sinkhole's Sand Trap (ULDA_030). Every Tombs boss hero prints 10 Health, so a row's Health is the act curve's and its tier is the boss's level in its chapter, read from its wiki deck table (verify/tools/gen/p8f19/tombs_tiers.py, shared with the later Tombs families: "10; 15" levels 1–2 tier 1, "15; 20" and the 25-card level-4 lists tier 2, the 30-card level-6 lists tier 3, a full 30-card list with no lower level tier 4). Thirteen clickable powers and five passives (E.V.I.L. Promotion keeps its growing bonus in the power's own script number; Sword Dance shuffles a copy of the boss's destroyed weapon, enchantments kept, into its deck; Under Wraps, Unstable Weaponry, "Confiscated" Goods). Mummy Ritual and Belch read the public graveyards and never raise a dead run shrine; Venomous Stinger's destroy makes a shrine go dormant. Sand Surge puts Sand Trap into the run player's deck as a public shuffle (match only, never a run deck record); drawn, it moves the top two cards of its holder's deck to the graveyard and draws; the boss's fair view keeps that deck masked. Brick by Brick is a boss-side Discover through the generation filter. "Confiscated" Goods replaces a weapon broken in the same attack and drops its pending death-pass removal (Game.ClearWeapons names the hero, not the weapon). Decks are the wiki's Normal lists, brought to 30 with second copies and the Heroic list; unscripted Saviors of Uldum collectibles are replaced by named scripted cards of the same role (Lackeys for Vera's)."

## For the README / baseline

- c-admission 3238; ledger 4735 / 2813 / 345 / 519 / 1058; encounters "87 rows, 91 decks (45 authentic, 46 changed), 227 adaptations, 1 rejected".
- New run entry c-enc-19: "RESULT 113 passed, 0 failed".
- New bp modes enc19a 18/10, enc19b 18/14, enc19-seeds-a 18/11, enc19-seeds-b 18/13.

## For the integrator to redo after the merge

- Rerun gen.sh and recompute the pins above together with families 17 and 18.
- Expect textual conflicts in curation.json landedFamilies, c-content/c-enc-model passive lists, c-catalog/c-deckops golden values, suites.sh and bp.sh.
- Rebuild the other families' fixtures (README), then re-record the enc19 modes on the merged tree. Seeds depend on row index; F19 rows sit after F17/F18 rows.
- Nothing was added to verify/MANIFEST.md (generators p8f19 and suite p8f19/c-enc-19 are new).
- Rerun the encounters step whenever the replaced Uldum collectibles get scripts; c-enc-19 S8 pins Vera's and Zafarr's substitutions.


---

# Family 19 review: confirmed findings (adversarially verified)

## F19.1 [low] engine/SabberStoneCore/src/CardSets/DungeonRun/DungeonRunEncounters.F19.cs:102
Sword Dance's copy keeps the weapon's Attack, because ATK is copied as a tag, but it loses the Durability that kept enchantments add. CopyAttachment copies an enchantment entity without re-applying its effect, so the copy shows Greenskin's Command (+1/+1) but has its card's base Durability. That breaks the card text "It keeps any enchantments". The F19.json adaptation's "Durability is the card's again" is accurate only for lost Durability (damage), not for Durability buffs.

Scenario: Sword Dancer Sirinell's own level-6 deck includes Captain Greenskin. She equips Hooked Scimitar (base Durability 2) and plays Captain Greenskin, so the weapon gets NEW1_024o, whose Enchant is ATK +1 and DURABILITY +1. When the weapon breaks, SwordDance builds Entity.FromCard(c, weapon.Card, {ATK = buffed attack}) and CopyAttachment(NEW1_024o), which does not activate the Enchant. When the copy is drawn and equipped it has +1 Attack and shows Greenskin's Command, but its Durability is 2 rather than the 3 the kept enchantment gives in the original. If something later removes that enchantment, the engine subtracts a Durability point the copy never received. The engine's Kingsbane helper has the same pattern, and this family copied it into a new script.

Verifier: Confirmed. SwordDance (DungeonRunEncounters.F19.cs:102-121) copies only ATK, POISONOUS and LIFESTEAL as tags. Enchantment.CopyAttachment (Enchantment.cs:177-192) calls GetInstance, which creates the entity but does not apply the Enchant effect; only ATTACHED auras are activated. Weapon.Durability is DURABILITY - DAMAGE (Weapon.cs:76), and an Enchant effect adds directly to the DURABILITY tag. So the copy of a Hooked Scimitar buffed by NEW1_024o has the card's 2 DURABILITY while it still carries Greenskin's Command, which reads +1/+1. Captain Greenskin is in Sirinell's shipped level-6 list (F19.json, Sirinell row), so this can happen in her own games. The ULDA_BOSS_14p text is 'It keeps any enchantments'. The adaptation's wording 'Durability is the card's again' gives no reason for dropping enchantment Durability, and it points to Kingsbane's rule, whose engine helper has the same gap. Removing that enchantment later would also subtract a Durability point the copy never got.

Fix: In SwordDance's EntityData, add { GameTag.DURABILITY, weapon[GameTag.DURABILITY] }. That tag already includes enchantment Durability buffs, and lost Durability lives in DAMAGE, so the copy still comes back undamaged while keeping the +Durability its kept enchantments give. Reword the F19 power note to match: 'Durability is the card's plus its enchantments' (no damage carried).

## F19.2 [low] verify/suites/p8f19/c-enc-19/Program.cs:699
S3's tier-rule clause (`ruleTier == r.tier`) works out `ruleTier` from the literal `r.level` and compares it with the literal `r.tier`. The row's shipped `level.original` (written by tombs_tiers.level) is never read from the data, so this part of the check only compares two literals in the test.

Scenario: Example: tombs_tiers.SHAPES["2a"] is edited to give level 2 instead of 3, or make_f19.py records the wrong shape for Terraviss. encounters-v1.json / F19.json then ship `"level": {"original": 2}` with tier "2". S3 still passes, because `r.level` is the test's own 3 and the row's level is never compared. The check named "S3 ... a Tombs row of tier N (wiki level L ...)" does not pin the level the data records, and families 20-22 import this rule.

Verifier: Confirmed. In S3 (Program.cs:697-699), ruleTier is computed from the literal r.level and compared with the literal r.tier. b.LevelOriginal and b.LevelSource are never read, although the check names 'wiki level L'. In tombs_tiers.py, level and tier are separate fields of SHAPES, so a wrong level (for example SHAPES['2a'] giving 2) ships level.original 2 with tier '2' and still passes S3. No validator code reads LevelOriginal either (grep shows only DungeonRunData.cs parses it), so nothing checks the recorded level against the tier rule. Family 07's suite does check b.LevelSource and b.LevelOriginal (c-enc-07 Program.cs:606), and families 20-22 import this rule.

Fix: In S3, read the level from the data: add `&& b.LevelSource == "wiki-level-table" && b.LevelOriginal == r.level` and compute ruleTier from b.LevelOriginal instead of r.level, so the tier rule is checked against the shipped level.


Both review findings are fixed on `wip/f19` in commit **11a9029**, on top of e67bbdc. I folded your saved WIP commit (21cd893) into it. I agree with both findings; I did not change `verify/tools/gen/p8f19/tombs_tiers.py`. c-enc-19 now passes 115 checks, and all four enc19 modes still give the same games and boss wins as before. The tree is clean, nothing was pushed and no process of mine is running.

**F19.1, Sword Dance lost enchantment Durability.** The copy of a destroyed weapon now also carries the weapon's Durability tag, so a buff such as Captain Greenskin's +1/+1 survives. Durability already used up is stored as damage, so it is still not carried over.
- I reworded the Sirinell power note to match: "Durability is the card's plus what its enchantments add, no damage carried".
- New checks N7 and N8: a War Axe buffed by Greenskin (+1/+1) that has lost one Durability point is copied as a 4/3 with no damage. Drawn and equipped again, it is a 4/3. Before the fix the copy came back 4/2.
- Kingsbane's own helper in the engine has the same gap. I left it alone, since it is shared code.

**F19.2, S3 compared two test literals.** S3 now reads each row's shipped level (`b.LevelSource == "wiki-level-table"` and `b.LevelOriginal == r.level`) and works out the tier rule from that shipped level. A wrong level in `tombs_tiers.py` or `make_f19.py` now fails S3.

**Data regenerated.** F19.json changed only in Sirinell's note. `gen.sh` was run twice and all six generated files came out byte-identical.
- The other changed files are `encounters-v1.json`, the matching note line in `curation.json` and two lines of `restoration-ledger.md`.
- Ledger and encounter counts are unchanged: 87 rows, 91 decks (45 authentic, 46 changed), 227 adaptations, 1 rejected; ledger 4735 / 2813 / 345 / 519 / 1058.

**New numbers.** The engine builds with 0 warnings and 0 errors (md5 7394be887d24588b395c5409db70b3b7). Both test stages below were run twice through the lock, on the final build.
- c-enc-19 gives `RESULT 115 passed, 0 failed` (was 113; N7 and N8 are new). Runs 1 and 2 are identical.
- Each bosspilot mode was twice identical: 0 faults, 0 fallbacks, 0 failed checks, 0 swallowed leaf exceptions, power-unused 0, empty stderr. These are the same results as before the fixes.

| Mode | Games | Boss wins |
|---|---|---|
| enc19a | 18 | 10 |
| enc19b | 18 | 14 |
| enc19-seeds-a | 18 | 11 |
| enc19-seeds-b | 18 | 13 |

**Correction to the earlier report.** It gave the engine md5 as 3da2a359…; that was before these fixes, and the new one is above. Otherwise it stands, apart from the c-enc-19 count, which is now 115 rather than 113.