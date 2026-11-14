PHASE 8 FAMILY 04 (Unscripted collectibles C, Uldum ULD_282..ULD_728) - LANE 3 REPORT

All 29 ids are scripted, admitted, wired and tested. Nothing is rejected outright; the six Quests are admitted for the run player only (boss scope rejected with a stated reason). Not verified in the real client: all evidence is engine games and harnesses. Hall, the full runs.sh, the released bosspilot modes, selfplay and randplay were not run (lane rule).

Flags for the integrator (top):
- No schedule default, finale, act, pool set, save version, ReleasedShapes or golden sequence was touched. No new save field.
- Private tag 32639 was NOT used and no tag was taken: Quest state is the Quest's own progress tags, the Shadow keeps its minion in TAG_SCRIPT_DATA_NUM_1 (PayloadRules already masks that tag for ULD_286t). EncounterRules*.cs, DungeonRunEncounters.cs, DungeonRunData.cs, DungeonRunValidator.cs, DungeonRun.cs, DungeonRunState.cs, schedule-v1.json were not edited.
- No mirror defect met. Every harness run went through hrun.sh. No harness process of mine is left; no lock file left.

Lane root below: L = C:\Users\user\AppData\Local\Temp\claude\0a564ffc8743aeec\d2a4e245-ca37-4433-9b55-c35ed325f081\scratchpad\L3 (repo = L\repo, family folder = L\sp\p8f04; after the merge the family folder is ...\scratchpad\p8f04).

=====================================================================
1. PER ID (all done)
=====================================================================
Quests, reward = hero power for the match (admission quest-hero-power: Player only; boss lift "none: a boss's hero power is its encounter's identity; no boss deck lists a Quest and the boss's generation never yields one"):
- ULD_291 Corrupt the Waters (6 Battlecry cards) -> ULD_291p Heart of Vir'naal (Battlecries twice this turn, ULD_291pe).
- ULD_326 Bazaar Burglary (4 cards of another class entering the hand, drawn ones included; "another class" excludes every class of a dual run row) -> ULD_326p Ancient Blades -> ULD_326t Mirage Blade 3/2.
- ULD_431 Making Mummies (5 Reborn minions played) -> ULD_431p Emperor Wraps (2/2 copy, ULD_431e).
- ULD_433 Raid the Sky Temple (10 spells; the Quest itself is not one) -> ULD_433p Ascendant Scroll (random scripted Mage spell, (2) less, ULD_433e).
- ULD_711 Hack the System (5 hero attacks) -> ULD_711p3 Anraphet's Core (4/3 ULD_711t; refreshed after the hero attacks).
- ULD_724 Activate the Obelisk (15 Health actually restored by its owner's sources) -> ULD_724p Obelisk's Eye (restore 3; +3/+3 on a minion, ULD_724e).
Other 23 (admitted Any):
- own-deck-insert: ULD_286 Shadow of Death (three ULD_286t Shadows; each summons a plain copy when drawn, then draws), ULD_727 Body Wrapper.
- own-deck-pull: ULD_438 Salhet's Pride, ULD_716 Tip the Scales.
- own-hand-rewrite: ULD_288 Anka, ULD_290 History Buff, ULD_410 Scarlet Webweaver, ULD_435 Naga Sand Witch.
- scripted-generation: ULD_282 Jar Dealer, ULD_304 King Phaoris, ULD_327 Bazaar Mugger, ULD_329 Dune Sculptor.
- public-board: ULD_292 Oasis Surger (ULD_292a/b/ae), ULD_707 Plague of Wrath, ULD_709 Armored Goon, ULD_713 Swarm of Locusts, ULD_714 Penance, ULD_718 Plague of Death, ULD_720 Bloodsworn Mercenary, ULD_721 Colossus of the Moon (vanilla keywords), ULD_728 Subdue.
- ULD_293 Cloud Prince, ULD_726 Ancient Mysteries: scripted; they keep their phase-2 secret-from-deck row in DungeonRunAdmission.cs (not moved).

Data wiring: the family has no boss of its own. After regeneration all 29 are ledger "implemented", offered through their Tombs bucket entries (catalog 6362 admitted / 147 excluded references, was 6318 / 191), and three are back in Icarax's shipped decks (ULD_720, ULD_707 in all three; ULD_721 in phase 3, which is now authentic: encounters 3 authentic / 9 changed, 18 adaptations, was 2 / 10 / 19). Ledger: 4579 rows, implemented 2751 (+29), partial 39, missing 731, blocker run:special-finales 23 -> 20 (ULD_286, 329, 713 of the Tekahn decks).

SummonWhenDrawn (owned here, for families 06/19/21): public class SabberStoneCore.CardSets.DungeonRun.UldumRules in DungeonRunEncounters.F04.cs: ShuffleSummonWhenDrawn(c, source, shellId, card, copies), SummonedCard(entity), SummonNamed, SummonWhenDrawn (Topdeck task: summon, then draw), SummonDrawn(c, drawnMinion) for "summon it when drawn" on a minion card. Also QuestProgress / Advance (hero-power reward), ScriptedPool / RandomScripted, IsOtherClass, SummonForSpellsInHand, DiscoverDeadToDeck.

Decisions and limits to know:
- Shadow of Death copies the chosen minion's CARD (no enchantments, no damage), my reading of the original; the Shadow's client text "Summon a {0}." will not name the minion (the existing PayloadRules mask).
- Mirage Blade reuses Gladiator's Longbow's enchantment DS1_188e for "Immune while attacking" (the client data has no enchantment of its own).
- ULD_716's stub header had a stale REQ_MINION_TARGET and InfoCardId ULD_716e2; it is now REQ_NUM_MINION_SLOTS 1 with no info card (ULD_716e2 itself untouched, unscripted, not in the list).
- A Quest completed by the run player replaces its hero power through the engine's ReplaceHeroPower for that match only; the old power's trigger/aura is stopped (EncounterRules.DeactivatePower), the new one's started (ActivatePower). Native transport of that replacement is the existing hero-power-replacement path, not exercised here.
- docs/design/dungeon-run.md was not updated (shared).

=====================================================================
2. FILES CHANGED
=====================================================================
Own family files (repo):
- L\repo\engine\SabberStoneCore\src\CardSets\DungeonRun\DungeonRunEncounters.F04.cs (F04() stays empty; class UldumRules)
- L\repo\plugin\DungeonRun\DungeonRunAdmission.F04.cs (six rows, 27 ids)
- L\repo\data\dungeon-run\reference\encounters\F04.json (no boss rows; documents the family)
Shared files, each with reason:
- L\repo\engine\SabberStoneCore\src\CardSets\Standard\UldumCardsGen.cs: the card-set file; only the stub bodies of the 29 ids and their 22 support cards (ULD_286t, 288e, 290e, 291p, 291pe, 292a, 292ae, 292b, 326p, 326t, 410e, 431p, 431e, 433p, 433e, 435e, 711p3, 711t, 724p, 724e, 726e, 728e) are replaced, plus the two header lines of ULD_716/711t/721; BOM and LF kept. Family 03 edits other stubs of the same file. The edit is reproducible: python -I p8f04\patch_uldum.py <file> (fails if a stub is already scripted); the pre-change file is p8f04\UldumCardsGen.cs.before.
- L\repo\tools\restoration-ledger\curation.json: landed family E04 added after E01 (E01's line gains a comma: expect a trivial conflict with other lanes' E0x lines); reproducible with p8f04\patch_curation.py. The two tool-owned lists were rewritten by the encounters step.
- L\sp\audit-verify\c-admission\Program.cs: two lines added to the "table lists only ledger rows" check naming the six new families (the check refuses any other table id). Original in p8f04\before\harness.
- L\sp\audit-verify\c-catalog\Program.cs: four golden pins (below). Original in p8f04\before\harness.
Generated in the lane (dropped by the merge): encounters-v1.json, catalog-v1.json, dungeon-run-catalog.md, restoration-ledger.csv/.md. A second generator run was byte-identical for all six files.

=====================================================================
3. PINS MOVED (suite, old -> new, cause)
=====================================================================
- c-admission total 2244 -> 2344 passed; NOTE boss-only generated list 304 -> 310 (the six Quests); cause: the six F04 rows.
- c-catalog: admitted references 6318 -> 6362 (two checks), excluded 191 -> 147, distinct engine:unscripted 91 -> 62, ledger golden implemented 2722 -> 2751 / missing 760 -> 731; cause: 29 cards scripted. Updated in my copy; 127 passed twice.
- c-acts A6 digest 9F91C1FE52A4B998 -> C9F53F2B7989B6F8 and c-deckops L1 digest 5169BABFF3951AA2 -> FD4C8FF512664F26 (printed, not asserted). Attribution: with the previous catalog-v1.json and the new engine both print the old values again, so the catalog regeneration alone moves them. (That c-acts attribution run stopped after its digest line with an IOException in File.Replace under %TEMP%; the two regular c-acts runs passed. I did not rerun it.)
- Unchanged, 0 differing lines against the family-01 logs: c-content 904, c-runlayer 244, c-shrine-core 131, c-enc-model 141, c-enc-engine 122; c-deckops 174 and c-acts 181 pass (only the digest lines differ).
- enc01 (the eight shipped rows): 6 differing lines against p8f01b\bp-enc01-a.txt (Icarax's decks and catalog drafts changed).

=====================================================================
4. TESTS ADDED (in L\sp\p8f04; after the merge ...\scratchpad\p8f04)
=====================================================================
- p8f04\suites\c-collectibles-C: 102 checks, every card's text in real engine games through DungeonRunSetup (Quest progress, completion, each reward power, clone safety; Shadow chain and full-board draw; admission, catalog, Icarax decks, validator refusal of a Quest in a boss deck; the fixture validates).
- p8f04\fixtures\encounters-f04.json, built by p8f04\make_fixture.py from the generated encounters-v1.json: five boss fixture rows (family "04") whose curated decks hold the 23 boss-admitted cards, six rows (family "04p") whose start block gives the run player one Quest plus enablers.
- p8f04\suites\c-bosspilot-f04: a copy of c-bosspilot-encounter with PLAYER_WATCH (the run player plays listed cards / presses listed reward powers first; counts them and the games ending with a reward power) and REQUIRE=1. The shared harness is unchanged.
- p8f04\run.sh: fixture | build | suite n | boss n | player n | icarax n [carried] | same <mode>. Evidence files bp-f04-*-a/b.txt.

=====================================================================
5. RESULTS
=====================================================================
- Engine build 0 warnings / 0 errors; plugin 0 errors / 75 warnings.
- Eight reduced suites, c-catalog and c-collectibles-C: each run twice, run 1 = run 2, all pass (numbers above).
- f04-boss (shared c-bosspilot-encounter, FAMILY=04 on the fixture, 8 games per row): 40 games, twice identical, faults 0 / fallbacks 0 / failed checks 0 / swallowed 0, power-unused 0, stderr empty. The fair-AI boss played every one of the 23 cards (fewest: ULD_290, 713, 721, 727 twice each).
- f04-player (variant, 4 games per Quest row, REQUIRE=1): 24 games, twice identical, 0 faults / fallbacks / failed checks / swallowed. Every Quest played (4-5 times), 18 games ended with a reward power, every reward power pressed (fewest: Obelisk's Eye twice).
- Shipped Icarax row, carried 0 / 110 / 210, 4 games each, twice identical, 0 faults / fallbacks / swallowed: the boss played Bloodsworn Mercenary 3 and 6 times and Colossus twice; it never chose Plague of Wrath there (played 7 times in f04-boss). form-unreached 1 at carried 0, as in the baseline.
- enc01 rows: 20 games, twice identical, 0 faults / fallbacks / failed checks / swallowed.
- Fixture bosses start with +4 or +9 mana and won all 40 games; they exercise the cards, they are not balanced fights.

=====================================================================
6. FOR THE INTEGRATOR
=====================================================================
- Rerun the three generators in the main tree, then `bash p8f04/run.sh fixture` before the family's tests: the fixture embeds the generated encounter rows, and check R10 expects "generated rows + 11".
- Recompute the pins of section 3 after all lanes merge; every catalog-run digest (bosspilot catalog/act/shrine modes, randplay registry line) will move with the 29 newly offerable cards, as Pressure Plate did.
- c-admission: other lanes will add their families to the same expression; keep all.
- c-bosspilot-f04 is a copy: fold PLAYER_WATCH / REQUIRE into the shared harness or keep the copy in step with it.
- If family 03 also defines a Quest reward helper for ULD_131, UldumRules.QuestProgress can serve both.
- p8f04\gen1 is an empty folder left behind (directories are never deleted).
