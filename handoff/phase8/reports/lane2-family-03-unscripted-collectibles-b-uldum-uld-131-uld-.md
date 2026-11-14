PHASE 8 FAMILY 03 (Unscripted collectibles B, Uldum ULD_131..ULD_274) - LANE 2 REPORT

All 30 ids are scripted, admitted and tested; none is rejected. Not verified in the real client: every result is from engine games and harnesses. Nothing was flagged at the top: no private tag used (32638 untouched), no schedule, save-version, ReleasedShapes or golden-sequence change.

Lane paths below: R = C:\Users\user\AppData\Local\Temp\claude\0a564ffc8743aeec\d2a4e245-ca37-4433-9b55-c35ed325f081\scratchpad\L2\repo, S = C:\Users\user\AppData\Local\Temp\claude\0a564ffc8743aeec\d2a4e245-ca37-4433-9b55-c35ed325f081\scratchpad\L2\sp. After the merge the new tests are under C:\Users\user\AppData\Local\Temp\claude\0a564ffc8743aeec\d2a4e245-ca37-4433-9b55-c35ed325f081\scratchpad\p8f03.

=====================================================================
1. PER ID (all done)
=====================================================================
- ULD_131 Untapped Potential: Quest in the legacy quest slot. Counts the owner's turn ends with unspent Mana (Mana locked by Overload does not count); at 4 the owner's hero power becomes ULD_131p.
- ULD_131p Ossirian Tear (reward): passive power, controller aura CHOOSE_BOTH. Ends when its owner plays a hero card.
- ULD_135 Hidden Oasis (+ ULD_135a, ULD_135b, token ULD_135at): Choose One, scripted as Living Roots (AT_037) is.
- ULD_138 Anubisath Defender: costs (0) after its owner played, this turn, a spell whose paid cost was 5 or more (play history).
- ULD_143 Pharaoh's Blessing (+ ULD_143e).
- ULD_151 Ramkahen Wildtamer, ULD_154 Hyena Alpha (+ ULD_154t), ULD_158 Sandstorm Elemental.
- ULD_169 Mogu Fleshshaper: printed cost is 9 in the card facts (the stub comment said 7).
- ULD_172 Plague of Murlocs: each side's minions from that side's generation pool; a run board shrine goes dormant instead.
- ULD_173 Vessina (+ ULD_173e): aura on her other minions while her owner has locked or owed Overload.
- ULD_177 Octosari, ULD_179 Phalanx Commander (+ ULD_179e), ULD_208 Khartut Defender, ULD_215 Wrapped Golem, ULD_253 Tomb Warden.
- ULD_194, ULD_205, ULD_206, ULD_274: keyword-only (`new CardDef()`).
- ULD_195 Frightened Flunky: Discover of scripted Taunt minions through the generation filter.
- ULD_209 Vulpera Scoundrel (+ ULD_209t Mystery Choice!): three spells plus the mystery option, which adds a random spell of the same pool.
- ULD_212 Wild Bloodstinger: Dirty Rat's pull from the opponent's hand, then a forced attack.
- ULD_216 Puzzle Box of Yogg-Saron: ten random scripted spells of the generation pool, random mode and target.
- ULD_236 Tortollan Pilgrim: Discover among the different spells of the owner's deck; the pick is cast as a copy.
- ULD_239 Flame Ward: Secret, after an enemy minion's attack on the owner's hero has resolved.
- ULD_256 Into the Fray (+ ULD_256e), ULD_258 Armagedillo (+ ULD_258e).
- ULD_262 High Priest Amet (+ ULD_262e): sets Health on summon, including a minion returning by Reborn.
- ULD_268 Psychopomp: random dead friendly minion from the graveyard, never a dead run shrine, given Reborn.
- ULD_270 Sandhoof Waterbearer.

Deviations from the printed cards, each deliberate:
- Puzzle Box never casts another Puzzle Box (loop guard, stated on its admission row). The original can.
- Ossirian Tear's aura ends on a hero-card play only. A hero-power replacement by another effect (Sir Finley style) leaves the aura running; fixing that generally needs a line in the shared ReplaceHeroPower task, which I did not touch.
- Anubisath Defender counts spells played from hand (paid cost). A spell cast by an effect does not count.
- Printed races are left as the card facts give them (Candletaker, Restless Mummy and Wasteland Assassin carry no Undead type there).

Data wiring:
- No boss row ships with this family and there is no F03.json: the family is collectible cards, and I read no wiki page.
- Catalog: 29 of the 30 are bucket members and are now offerable (56 member references moved from excluded to admitted). ULD_216 is in no bucket.
- Encounters: Icarax's regenerated decks now hold six of the cards from his wiki lists (phase 1: Restless Mummy x2; phase 2: Into the Fray x2, Armagedillo, Tomb Warden x2, Phalanx Commander x2, Khartut Defender x2).
- Ledger: 4579 rows; implemented 2722 -> 2751, partial 39 -> 40, missing 760 -> 730. The one partial is ULD_216, blocked by run:special-finales (it appears only in the Tekahn finale decks; family 24 / close-out).

Admission: every one of the 30 has a row. ULD_154 (secret-from-deck), ULD_236 (from-own-deck) and ULD_239 (secret-card) keep their existing core rows. The other 27, plus ULD_131p, ULD_135a/b and ULD_209t, are in DungeonRunAdmission.F03.cs, all Admit Any: keyword-only, public-effect, own-hand, own-graveyard, generated-pool, random-cast (Guard "10 casts; a Box never casts a Box"), hidden-reveal (ULD_212), quest-hero-power.

=====================================================================
2. FILES CHANGED
=====================================================================
Own family files:
- R\engine\SabberStoneCore\src\CardSets\DungeonRun\DungeonRunEncounters.F03.cs: F03() stays empty. Adds `internal static class UldumCollectiblesB` (the card rules) and `internal sealed class RestlessOthersAura : Aura` (Vessina; Aura's Restless mode has no BOARD_EXCEPT_SOURCE case, and this avoids editing Aura.cs).
- R\plugin\DungeonRun\DungeonRunAdmission.F03.cs: eight rows.
- R\engine\SabberStoneCore\src\CardSets\Standard\UldumCardsGen.cs: 42 `cards.Add` statements replaced, only those of the family's ids and their sub-cards. No using, helper or other card touched; BOM kept, LF.

Shared files:
- R\tools\restoration-ledger\curation.json: (a) landedFamilies entry E03 appended after E01, which adds a comma to E01's line; required by the ledger self-check. (b) One stale example in the ULD_186 catalog note ("for example ULD_274 Wasteland Assassin" is no longer unscripted). (c) The tool rewrote the Icarax notes in encounterAdaptations. Expect the usual overlap with other lanes here.
- No other shared file edited: no EncounterRules, DungeonRunData, validator, schedule, DiscoverTask or Aura.cs change. docs\design\dungeon-run.md is not edited.

Generated (regenerated in the lane, dropped by the merge): encounters-v1.json, catalog-v1.json, dungeon-run-catalog.md, restoration-ledger.csv/.md. A second run of all three generators reproduced them byte for byte.

=====================================================================
3. PINS MOVED
=====================================================================
| Suite | Pin | Old | New | Cause |
|---|---|---|---|---|
| c-admission | total | 2244 | 2361 | F03 rows |
| c-admission | "table lists only ledger rows" check (edited) | - | - | exemption set extended by one added statement naming the eight F03 families; originals in S\p8f03\before\harness |
| c-catalog | admitted / excluded references (edited) | 6318 / 191 | 6374 / 135 | 29 cards scripted |
| c-catalog | distinct engine:unscripted ids (edited) | 91 | 62 | same |
| c-catalog | ledger golden (edited) | 2722 / 39 / 760 | 2751 / 40 / 730 | same |
| c-acts | A6 digest (printed) | 9F91C1FE52A4B998 | 4B42FEB0B688ED56 | catalog regeneration |
| c-deckops | L1 digest (printed) | 5169BABFF3951AA2 | 4C128BD7F3E6AC32 | catalog regeneration |
| c-enc-model | J12 Mage Secret pool (printed) | 13 | 14 | Flame Ward scripted |
| c-enc-engine | F4 / F5 (printed) | 13 vs 13 | 14 vs 14, pick sequence now includes ULD_239 | same |
| enc01 bosspilot | game lines vs p8f01b\bp-enc01-a.txt | - | 5 game lines and the summary differ (boss wins 15 -> 16) | Icarax's deck, Whirt's Mage Secret pool, drafted player decks |

- Attribution of A6 and L1: with the pre-change catalog and the new engine and plugin, both return to the baseline values (S\p8f03\attr-oldcatalog.txt).
- c-secretsearch, run once before the final cycle: 689 passed, count unchanged; four note lines differ (an open-pool world count, Secrets consumed, one catalog game).
- Not run in the lane and expected to move: randplay (the cards are now eligible for random decks and legacy pools), the released bosspilot catalog/act/shrine modes, and any suite whose seeded generation can now roll these cards (c-treasures-F8 T11 was sensitive to Pressure Plate).

=====================================================================
4. TESTS ADDED (in S\p8f03)
=====================================================================
- suites\c-collectibles-B (Program.cs, CollectiblesB.csproj): 108 checks, B1-B79, real engine games through DungeonRunSetup. Covers each card's text for the run player and, where the side matters, for a boss; clones (Ossirian Tear, Vessina); run-shrine interaction (Plague of Murlocs, Psychopomp); the generation filter (boss never gets a Secret from Scoundrel or the Box; a legacy-run player neither); admission rows and generation lists; shipped data validation; Icarax's decks; fixture validation. B79 is three games piloted by the fair AI with Ossirian Tear from the boss's first turn end.
- make_fixture.py -> fixtures\encounters-f03.json: the generated encounters plus seven curated fixture rows (fix03_druid/paladin/hunter/shaman/warrior/mage/priest, tiers 1-4) whose decks hold all 30 cards. Test data only, marked as fixture, never shipped.
- run.sh: `fixture | build | suite <n> | enc03 <n> | shipped <n> | same`. Every harness call goes through hrun.sh.
- Evidence: bp-enc03a-a.txt, bp-enc03b-a.txt, bp-enc03c-a.txt, bp-enc01-a.txt, bp-icarax2-a.txt, attr-oldcatalog.txt.
- patch_uldum.py and patch_curation.py are how the edits were made; they are not rerunnable on the patched files.

=====================================================================
5. RESULTS (final cycle, after the last source change)
=====================================================================
- Engine: 0 warnings / 0 errors. Plugin: 0 errors / 75 warnings. Every suite's out\SabberStoneCore.dll equals the lane's engine-out build.
- Suites, each run twice with 0 differing lines between runs:

| Suite | Result |
|---|---|
| c-content | 904 |
| c-admission | 2361 |
| c-runlayer | 244 |
| c-acts | 181 |
| c-deckops | 174 |
| c-shrine-core | 131 |
| c-enc-model | 141 |
| c-enc-engine | 122 |
| c-catalog | 127 |
| c-collectibles-B (new) | 108 |

- Bosspilot, each mode twice identical, stderr empty, 0 faults / fallbacks / failed checks / swallowed everywhere:
  - enc03 (fixture, fair AI as boss via BuildPlan -> DungeonRunBossPilot.Build -> Configure -> ApplyAfterStart): 64 games in three slices (24 + 24 + 16), boss wins 60, power-unused 0. The boss played every family card from hand at least once; lowest counts are ULD_258 x1 and ULD_212 x2.
  - The paladin fixture has its own 16-game slice because Pharaoh's Blessing was not played in its first 8-game slice (it was played 11 times in a 24-game probe).
  - enc01 (the eight family-01 rows): 20 games, boss wins 16, power-unused 0, form-unreached 1 (Icarax at 0 carried, as before).
  - icarax2 (Icarax started in phase 2, CARRIED=110): 4 games; the boss played Phalanx Commander, Khartut Defender, Tomb Warden, Into the Fray and Armagedillo.
- No download occurred. No mirror defect met: every build and run path contained \L2\. No harness process of mine is left running.

=====================================================================
6. FOR THE INTEGRATOR
=====================================================================
- Rerun the three generators in the main tree, then `run.sh fixture`: the fixture is derived from the generated encounters-v1.json.
- Add c-collectibles-B and the modes enc03 / shipped to the shared builds.sh / runs.sh / bp.sh.
- Recompute the pins of section 3 after all lanes merge; family 02 and 04 move the same c-catalog, c-admission, A6 and L1 numbers.
- curation.json: keep E03 and the ULD_186 note fix by hand; the tool lists rebuild themselves.
- If family 04 or 02 adds its own names for the same admission families, the c-admission exemption statement I added needs their names too.
- Family 04's six Quests can reuse `UldumCollectiblesB.QuestStep(g, source, rewardPowerId)` for a hero-power reward.
- Open, not done here: ULD_216 stays partial until a finale deck is reachable; the general Ossirian Tear replacement case; DungeonRunAdmission.NeutralSecretGeneratorClasses does not list ULD_216 (shared file), so on a catalog run the boss's Secret prior does not hold off-class Secrets a player's Puzzle Box may cast, the same as Yogg-Saron today.
