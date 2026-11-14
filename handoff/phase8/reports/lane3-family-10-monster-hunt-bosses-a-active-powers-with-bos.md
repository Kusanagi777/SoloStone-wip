PHASE 8 FAMILY 10 (Monster Hunt bosses A) - LANE 3 REPORT

All 39 ids are done; none rejected. Everything is in the lane copy only (C:\Users\user\AppData\Local\Temp\claude\0a564ffc8743aeec\d2a4e245-ca37-4433-9b55-c35ed325f081\scratchpad\L3), nothing committed, no directory deleted, nothing downloaded. Files are LF, no BOM added (Controller.cs keeps the BOM it had). No process of mine is running (one AiHarness.exe in tasklist belongs to another lane; left alone). Nothing was checked in the real client.

FLAGS FOR THE INTEGRATOR (read first)
1. Two shared engine files edited beyond the family files (details in "Shared files").
2. c-enc-model E3 was rewritten, not just re-pinned. With Monster Hunt rows now in the combat tiers, Hagatha is the act II major in 506 of 1000 seeds (was 1000); the other runs get a custom band3 boss. This is the provenance-mix rule working as written (a major slot takes the less-used provenance), but it changes how often a run meets Hagatha. Whirt, Voone and Icarax are still met in 1000 of 1000.
3. Hagatha's deck (family 01 row) is now `authentic`: its two Amalgamates are back because GILA_BOSS_27t is scripted. Family 01's enc01 evidence lines for Hagatha will move.
4. Only tag 32645 was taken (my reserved one). No schedule default, finale, act, pool set, save version, ReleasedShapes or golden sequence was touched.
5. Two judgement calls not backed by the wiki (its boss pages have no notes): the Scarecrow starts with the Scythe equipped (the wiki lists it as his special card, in no deck row; start-equipped is from memory), and tiers follow printed Health as instructed, which disagrees with the wiki's round table for some bosses (Grubb: printed 50 = tier 4, wiki rounds 3-4; Cozzlewurt: printed 15 = tier 1, wiki rounds 2-3). Both are recorded as adaptations per row.

1. PER ID (all done)
- Heroes GILA_BOSS_20h, 21h, 22h, 23h, 26h, 27h, 29h, 31h, 32h, 33h, 34h, 35h, 36h, 37h, 38h: rows only (no script needed).
- GILA_BOSS_20p Guttural Howl: +1/+1 to a random minion in the boss's hand; offered only while it holds one.
- GILA_BOSS_21p Fading Bite: hero +2 Attack this turn.
- GILA_BOSS_22p Shank: 1 damage to a minion, draw a card (no Spell Damage).
- GILA_BOSS_23p Chuck: destroy own minion, deal its Attack to the enemy hero. GILA_BOSS_23t Chuck (spell): discard all minions in hand, each one's Attack as one hit. The spell is in no wiki deck row; one copy is shuffled into Grubb's deck by the row's start block.
- GILA_BOSS_26p Nab / GILA_BOSS_26t Stuffed Sack: the minion itself goes to its controller's set-aside zone and returns (damage and enchantments kept) when the sack's Deathrattle resolves; lost if that board is full or the sack is silenced, transformed or bounced. A run shrine is never a legal target and the store refuses one. GILA_BOSS_26t2 Sack of Gnomes: fills the board from a fixed list of six collectible Gnomes (five scripted; Gnomish Experimenter is not).
- GILA_BOSS_27p Consume; GILA_BOSS_27t Amalgamate (destroys all minions, deaths resolve, then one Amalgamation with the combined Attack and remaining Health; a run shrine goes dormant); GILA_BOSS_27t2 Amalgamation (token, every minion type).
- GILA_BOSS_29p Infatuation / GILA_BOSS_29t Enchanting Tune: control change in board order; full board destroys the minion; a run shrine never changes sides.
- GILA_BOSS_31p Splinter (2/2 Treant GIL_663t; needs a free slot), GILA_BOSS_32p Unstable Growth, GILA_BOSS_33p Harvest.
- GILA_BOSS_33t Scythe: never loses Durability from an attack (weapon Immune), +1 Attack after its hero kills a minion with it; start-equipped.
- GILA_BOSS_34p Devour Soul: not damage; hero keeps the larger half, never below 1; Armor untouched.
- GILA_BOSS_35p Bloodthirst (only minions without Lifesteal), GILA_BOSS_35t Vampiric Fangs.
- GILA_BOSS_36p Witch's Kiss: random minion of either board becomes hexfrog; a run shrine picked goes dormant instead.
- GILA_BOSS_37p It's Raining Fin (offered by hand room, never by deck content), GILA_BOSS_37t Murloc Infestation.
- GILA_BOSS_38p Call of the Raven: random scripted 1-Cost minion behind the generation filter.
Also registered (not in the id list, needed by the scripts): enchantments GILA_BOSS_20e, 21e, 29e, 32e, 33e2, 35e2, 37e2.

Rows (reference F10.json, 15 rows): tier 1 Mangy Wolf, Rottooth, Cozzlewurt; tier 2 Willie, Gnarlroot, Niira, Chupacabran, Griselda, Wharrgarbl; tier 3 Gnomenapper, Sazzmi, Scarecrow, Hav'nixx; tier 4 Grubb, Experiment 3C. Decks read from hearthstone.wiki.gg (15 boss pages, 2026-11-13, each transcribed twice, both agree). Wiki lists are 10, 20, 30, 32 or 45 cards: 1 deck authentic (Scarecrow), 11 extended, 3 substituted. Unscripted cards replaced or left out, each recorded: Ogre Ninja, Ogre Brute, Mogor the Ogre, Mogor's Champion, Wee Spellstopper, Gnomish Experimenter, original Wrath and Power of the Wild, and Witchwood's Touch GILA_BOSS_99t2 (family 23's card; Shield Block until then). Experiment 3C loses the last 15 of its 45 cards to the 30-card rule.

2. FILES CHANGED
Own family files (repo):
- C:\Users\user\AppData\Local\Temp\claude\0a564ffc8743aeec\d2a4e245-ca37-4433-9b55-c35ed325f081\scratchpad\L3\repo\engine\SabberStoneCore\src\CardSets\DungeonRun\DungeonRunEncounters.F10.cs
- C:\Users\user\AppData\Local\Temp\claude\0a564ffc8743aeec\d2a4e245-ca37-4433-9b55-c35ed325f081\scratchpad\L3\repo\engine\SabberStoneCore\src\Actions\EncounterRules.F10.cs (new: the set-aside store, `CanStore`, `Store`, `StoredBy`, `Release`; for family 17's Zuramat)
- C:\Users\user\AppData\Local\Temp\claude\0a564ffc8743aeec\d2a4e245-ca37-4433-9b55-c35ed325f081\scratchpad\L3\repo\plugin\DungeonRun\DungeonRunAdmission.F10.cs (5 rows: boss-board-power 10 ids, boss-own-hand-power 3, boss-control-all 1, boss-setaside-store 1, boss-hunt-card 9 as Player|Boss, NeverGenerated)
- C:\Users\user\AppData\Local\Temp\claude\0a564ffc8743aeec\d2a4e245-ca37-4433-9b55-c35ed325f081\scratchpad\L3\repo\data\dungeon-run\reference\encounters\F10.json (written by p8f10\make_f10.py)

Shared files (repo), each minimal:
- engine\SabberStoneCore\src\Actions\EncounterRules.cs: (a) one constant `SetAsideStore = (GameTag)32645` in the tag table; (b) `ResolveStart` now activates a start weapon's aura and trigger before equipping it, as WeaponTask does. Reason: a start-block weapon with a trigger was never live; the Scythe needs it.
- engine\SabberStoneCore\src\Model\Entities\Controller.cs: hero-power options now apply `Card.SourceTargetingPredicate`, as hand cards and `Playable.TargetingRequirements` already do. Reason: without it Nab was offered on a shrine and Bloodthirst on a Lifesteal minion while the play itself was refused (an option the AI cannot take = a fallback). No other hero power has such a predicate.
- tools\restoration-ledger\curation.json: landedFamilies entry E10 appended after E08 (by p8f10\patch_curation.py); the two tool-owned lists were rewritten by the encounters step.

Existing suites edited (scratch), originals in L3\sp\p8f10\before\harness:
- p8f01\suites\c-enc-model\Program.cs: A4, B5, E3 (below).
- audit-verify\c-admission\Program.cs: one exemption statement for the five F10 family names.
- audit-verify\c-deckops\Program.cs: T5. audit-verify\c-catalog\Program.cs: ledger golden.
No shared run script was edited. Not edited: docs\design\dungeon-run.md (paragraph for it at the end).

3. PINS MOVED (suite, old -> new, cause)
- c-admission total 2854 -> 2938 (five F10 rows); NOTE generated lists 351 -> 375 (15 powers + 9 cards).
- c-enc-model A4 family counts "01:5,07:18,08:17,13:1,23:1,24:1" -> "...,08:17,10:15,13:1,...". Still 141 checks.
- c-enc-model B5: its "unscripted deck card" example was GILA_BOSS_27t, now scripted; changed to GILA_BOSS_48t (rejected to phase 9).
- c-enc-model E3: see flag 2. The check now asserts the rule (each major is of the less-used provenance, ties either way) instead of "Hagatha is always the act II major".
- c-deckops T5 ledger rows 4644 -> 4678.
- c-catalog ledger golden 4644 / 2813 / 165 / 608 -> 4678 / 2813 / 223 / 584: +34 rows, all partial (15 run:boss, 19 deck cards new to the ledger); 24 rows missing -> partial (15 powers, 9 boss cards). The 15 boss heroes stay `missing` with run:default-schedule, as in earlier families.
- Unchanged: c-content 904 and its passive list (no passive added), c-acts A6 digest, c-deckops L1 digest, catalog-v1.json (byte-identical to the base).

4. TESTS ADDED (paths after the merge)
Folder C:\Users\user\AppData\Local\Temp\claude\0a564ffc8743aeec\d2a4e245-ca37-4433-9b55-c35ed325f081\scratchpad\p8f10 :
- suites\c-enc-10 (Enc10.csproj, Program.cs): 116 checks, real engine games through DungeonRun.BuildPlan -> DungeonRunSetup. Every power, boss card and token; the store in a clone and in the boss's fair view; shrine cases for Nab, Amalgamate, Infatuation, Enchanting Tune, Unstable Growth, Vampiric Fangs, Witch's Kiss; boss cards in the run player's hands; rows, admission, validator.
- run.sh: reference | curation | fixture | build | suite <n> | bp <mode> <n> | same <mode>. Modes: enc10a, enc10b, enc10-nab, enc10-cards, enc10-shrine, enc10-seeds, enc10-hagatha. Every harness call goes through hrun.sh with a 400 s timeout.
- make_f10.py, make_fixture.py, patch_curation.py, fixtures\encounters-f10.json (six fix10_* rows that put each boss card in its boss's hand), bp-<mode>-a/b.txt evidence, before\ (originals).

5. RESULTS (after the last source change)
- Engine 0 warnings / 0 errors; plugin 0 errors / 75 warnings.
- Generators: encounters 58 rows, 62 decks (34 authentic, 28 changed), 117 adaptations, 1 rejected; catalog 506 entries, 6508 admitted / 1 excluded; ledger 4678 rows, implemented 2813, partial 223, missing 584, duplicate/support 1058, adapted 0. A rerun leaves all six files byte-identical.
- Suites: c-content 904, c-admission 2938, c-runlayer 244, c-acts 181 (log identical to the wave 2 log), c-deckops 174, c-shrine-core 131, c-enc-model 141, c-enc-engine 122, c-catalog 127 (extra, to check its pin), c-enc-10 116 twice with identical logs. All 0 failed.
- Bosspilot, each mode twice identical, 0 faults / 0 fallbacks / 0 failed checks / 0 swallowed, stderr empty:

| mode | games | boss wins | power-unused | boss cards played from hand |
|---|---|---|---|---|
| enc10a (8 rows x 4) | 32 | 23 | 0 | 26t2 x4, 27t x7, 29t x4 |
| enc10b (7 rows x 4) | 28 | 23 | 0 | 35t x4, 37t x2 |
| enc10-nab (Gnomenapper x 10) | 10 | 8 | 0 | 26t2 x12 |
| enc10-cards (6 fixture rows x 6) | 36 | 21 | 1 | 23t x3, 26t2 x17, 27t x11, 29t x15, 35t x21, 37t x3 |
| enc10-shrine (9 fights vs shrine rows, after 28 padding games) | 37 | 33 | 0 | - |
| enc10-seeds (FAMILY=10, base 19000) | 30 | 24 | 1 | 26t2 x4, 27t x4, 35t x1 |
| enc10-hagatha (6 games) | 6 | 6 | 0 | 27t x3 |

- The two power-unused are both Grubb: fix10_grubb (fixture whose purpose is the Chuck spell) and gila_23_grubb on seed base 19000 (2 games). The fair AI uses Chuck sparingly; it used it once in each of his four enc10a games.
- Grubb is weak as shipped: 1 win in 6 games at tier 4. His four wrong-target Ogres are unscripted and replaced.
- The Chuck spell was never played from a shipped deck (one copy in 31 cards); the fixture is its only AI evidence.

6. TO REDO IN THE MAIN TREE
- Run the three generators; then `p8f10/run.sh fixture` (the fixture is built from encounters-v1.json), build, suite and the seven modes.
- Recompute the pins of section 3. E3's counts and the bosspilot lines move again when later families add rows (seeds depend on a boss's index).
- When family 23 scripts GILA_BOSS_99t2, rerun the encounters step: Hav'nixx becomes authentic and c-enc-10 P3 (his kind) and P8 (substitution strings, Experiment 3C included) need new values. P8 also moves if anyone scripts the Ogres, Wee Spellstopper, Gnomish Experimenter, EX1_154 or EX1_160; E16 pins the Gnome pool at five.
- Take over 32645 in the tag-table comment of EncounterRules.cs if you keep the "taken" list there (I left the comment as it was).
- The p8f10\before copies contain lane paths, like earlier families' before folders.
- No mirror defect found: every build and run named \L3\.

7. PARAGRAPH FOR docs\design\dungeon-run.md (not written by me)
"Family 10 (Monster Hunt bosses A): fifteen Witchwood encounters with a clickable power, tier by the printed Health of the hero card (15 tier 1; 25, 30 tier 2; 35, 40 tier 3; 50, 60 tier 4). New engine rule: the set-aside store (Actions/EncounterRules.F10.cs; private tag SetAsideStore 32645 on the holder). Gnomenapper's Nab stores an enemy minion in a Stuffed Sack summoned for the boss; the sack's Deathrattle returns the minion itself to its own controller; a run shrine is never a legal target and never enters a store. A start-block weapon now starts its aura and trigger (the Scarecrow's Scythe: Immune to Durability loss, +1 Attack per kill). Hero-power options apply a card's SourceTargetingPredicate. Amalgamate resolves deaths before it summons; Infatuation and Enchanting Tune never take a shrine; Witch's Kiss makes a picked shrine dormant; Devour Soul halves Health without damage and never below 1. Decks are the wiki's lists brought to 30 cards (second copies, curated fillers, or the first 30 of a longer list), each change recorded."
