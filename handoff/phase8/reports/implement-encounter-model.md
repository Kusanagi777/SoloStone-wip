PHASE 8 FAMILY 01 (ENCOUNTER MODEL) - REPORT

Everything is uncommitted in C:\SoloStone (HEAD still 0f6a476, nothing staged, stash list empty, no directory deleted, installer/out untouched, nothing downloaded). All files I wrote are LF without BOM; Game.cs, GameConfig.cs and HeroPower.cs carry a BOM that is already in HEAD (verified with git show), not added. No build or test process of mine is running (only an idle VBCSCompiler server, left alone).

Scratchpad for this work: C:\Users\user\AppData\Local\Temp\claude\0a564ffc8743aeec\d2a4e245-ca37-4433-9b55-c35ed325f081\scratchpad\p8f01 (below: P8).

=====================================================================
1. WHAT WAS IMPLEMENTED
=====================================================================

Model and data
- Boss row schema + parser + validator. Imported encounters are DungeonRunBoss rows (IsEncounter) with provenance LOOT/GIL/TRL/DALA/ULDA in generated C:\SoloStone\data\dungeon-run\encounters-v1.json, appended after the 20 custom bosses; run-v1.json and pool-v1.json are not edited. The model and parser live inside C:\SoloStone\plugin\DungeonRun\DungeonRunData.cs (not a separate file) because 46 hall harness projects and both tools compile that file alone. Validator: C:\SoloStone\plugin\DungeonRun\DungeonRunValidator.cs (ValidateEncounter, ValidateRules, schedule v3 checks, BossSecretPool).
- Generator step: C:\SoloStone\tools\dungeon-run-catalog\Encounters.cs (`dotnet run -c Release -- encounters`): reads data\dungeon-run\reference\encounters\F<NN>.json, resolves names against facts\violet-hold-2026-09.json, checks every card (engine card, client card, scripted, admitted for its side), builds decks, writes encounters-v1.json plus the tool-owned `encounterAdaptations` / `encounterRejections` lists in tools\restoration-ledger\curation.json. Reproduces byte-for-byte.
- Schedule version 3 (C:\SoloStone\data\dungeon-run\schedule-v1.json): poolSets (mix-1..6, major-1..3), `"draw": "provenance-mix"`, anomaly nodes (`anomalyAtStart`), `twist {pool, chance}`, `rules`, `sameActsAs`, per-schedule stamp. Legacy pinning: acts-v1, acts-v2, act1 keep their released shapes and stamp new runs 2; acts-v3h / acts-v3t stamp 3; ReleasedShapes[3] pins both (FF4FEC05953AA0DE, finales disabled). Golden recordings (P8\golden-sequences.txt, 2700 lines) prove legacy boss sequences unchanged.
- Save version 6 (identity migration 5 to 6, one-time backup as before): DungeonRunState.HeroSwitch, DungeonRunState.HeroDamage, DungeonRunRule.Kind, DungeonRunModifierOffer.Kind, match-summary extras (BossRemaining, BossForm, HeroesUsed, Rules, NoLoss), DungeonRunProfile.BossDamage. Golden v5 saves (P8\golden-save-legacy-v5.xml, golden-save-acts-v5.xml) load and upgrade.
- Engine: C:\SoloStone\engine\SabberStoneCore\src\Actions\EncounterRules.cs and EncounterRules.HeroCards.cs (forms, start block, relay, carried damage, deck replacement, own-deck facts, harvest, hero-power activation, dynamic power cost; all state in private tags 32630-32699), GameConfig.Encounter, three hooks in Game.cs, registry C:\SoloStone\engine\SabberStoneCore\src\CardSets\DungeonRun\DungeonRunEncounters.cs (+ F01..F25 partial files, F02..F25 empty stubs) and DungeonRunGlobalRules. No parallel combat engine, no plugin framework.
- Admission: AdmitScope.Rule = 8, AdmissionRule.BossNeeds, table split into CoreRules() + RulesF01()..RulesF25() (C:\SoloStone\plugin\DungeonRun\DungeonRunAdmission.F<NN>.cs).
- Ledger (C:\SoloStone\tools\restoration-ledger\Program.cs): encounter loading, encounter_use derivation, rule validation kind, pending reach suffix " (schedule not a default yet)" with blocker `run:default-schedule`. curation.json: landed family E01, hand-written `ruleAdaptations` x10, tool-written `encounterAdaptations` x14, `encounterRejections` x1. Regeneration is byte-identical on a second run.
- Docs: phase-8 section in C:\SoloStone\docs\design\dungeon-run.md; "Encounters (phase 8)" section in C:\SoloStone\tools\dungeon-run-catalog\README.md; regenerated docs\design\restoration-ledger.csv/.md.

One encounter per mechanism (all sources hearthstone.wiki.gg, read 2026-11-12, recorded per row)
- Phased boss with persistent damage: Icarax, Plague Lord of Wrath (ulda_40_icarax; ULDA_BOSS_40h/h2/h3 at 300/200/100, powers 40p1/40p2/40p3, phase deck replacement + draw 3, damage banked per encounter in the profile).
- Hero-switch fight: Hagatha the Witch (gila_61_hagatha; passive GIL_504h, Ritual Dagger, Ominous Fog; the run player is given hero cards GILA_600h2 Darius and GILA_500h4 Tess for that fight only; run hero/power/deck records never written).
- Boss-side generated Secrets: Whirt the All-Knowing (dala_21_whirt; DALA_BOSS_21p Prediction, row `bossGeneratedSecrets: true`).
- Boss shrine: War Master Voone (trl_200_voone; three variants horn TRLA_171 / drum TRLA_170 / champion TRLA_104, armed with ShrineRules.Arm for the boss).
- Anomalies (global rules on both players): DALA_850 Arcane, DALA_864 Growing, DALA_866 Rejuvenating.
- Rejected with stated reason: GILA_900h2 (Toki hero card; Temporal Loop needs a run-owned turn restart, phase 9).

Deferred fixes done
- Typhoon vs ShrineRules.InterceptLeave: C:\SoloStone\engine\SabberStoneCore\src\CardSets\Client\Sets\EMERALD_DREAM\Typhoon.cs now skips a minion InterceptLeave handles.
- ACT_RUN label: c-bosspilot and its 7 copies print `"act run: " + data.Schedules.Default + " schedule, ..."`.

=====================================================================
2. VERIFICATION (final cycle, after the last source change)
=====================================================================

Builds: engine 0 warnings / 0 errors; plugin 0 errors / 75 warnings (baseline 75); 50 suite builds OK.

Suites: 40 suites run twice, run 1 and run 2 identical. All counts at baseline except these attributed moves:
- c-admission 2205 -> 2237 (new F01 admission rows; generated lists 277 -> 283 on both sides).
- c-acts A6 digest -> 7755E5589E3A15A3 and c-deckops L1 digest -> 6019C6E6F369F2D8. Both return to the baseline values (6042A666B05137DF / DC23E2F018BA605C) when the save root Version="6" is masked to 5, so the only cause is the save version.
- randplay registry line regkeys(nonTB) 2137 -> 2140, hash 14B641859E57DB8B -> 0F45C329AB01A8F3 (three new registered keys); the game digest is unchanged.
- The ACT_RUN label line (c-bosspilot act mode and the 7 shrine-mode copies): exactly one differing line each.
- Version pins / expectations updated in older suites as a consequence of save v6, schedule v3 and the admission table (c-runlayer, c-acts, c-admission, c-heroes, c-upgrades, c-shrine-core, c-shrines-S1); pass counts for those are unchanged (244, 181, 347, 34, 131, 107). Backups of the originals: P8\before\harness.
- Unchanged at baseline: c-runlayer 244, c-catalog 127, c-content 904, c-acts 181, c-deferrals 149, c-deckops 174, c-dkdh-supply 13, c-dkdh-play 5, c-heroes 347, F14 316, F6 566, F9 292, c-runctx 40, c-starters-F10 239, c-upgrades 34, treasures F1 229 F2 321 F3 283 F4 236 F5 404 F7 367 F8 256 F11 380 F12 321 F13 248, c-review-F18 35, c-shrine-core 131, S1 107 S2 157 S3 158, c-shrine-upgrades 92, T1 139 T2 207, companions-D 251, c-p7fix 40, c-secretsearch 689, c-secretscan 0 faults.
- New: c-enc-model 141 passed, c-enc-engine 122 passed.
- selfplay digest 2CE546A18F96DCBC (baseline). randplay digest D2D7494D2AD50707, exit 127 (pre-existing).
- Hall: 39 harnesses, 955 tests / 8285 checks, identical to the p7f baseline.

Bosspilot
- The 11 released modes: each twice identical, 0 faults / fallbacks / swallowed; 0 differing lines against p7f for run, fixture, catalog; exactly the label line for act and the 7 shrine modes.
- Encounter modes (fair AI pilots the boss through the run setup path), each twice identical, 0 faults / fallbacks / failed checks / swallowed:
  - enc01: 12 games over the four rows. power-unused 0; form-unreached 1 (Icarax started at 0 carried damage does not reach a later form inside a game; covered by the plague modes).
  - plague: carried 95, 110, 195, 210. Natural 0>1 and 1>2 transitions observed in play, and each phase starts a game.
  - rules01: DALA_850, DALA_864, DALA_866, 10 games each on Whirt/Voone/Hagatha.
  - finale: fixture schedule with acts-v3h finale enabled on two shipped encounters, 8 games.
  - relay: fixture finale row with two relay heroes, 2 games.
  - seeds sweep: 6 seed bases x 12 games, 0 faults.

Ledger after regeneration: 4567 rows; implemented 2721, partial 23, missing 765, duplicate/support 1058, intentionally adapted 0.

Bugs found and fixed during the work (each has a regression check): DALA_850 with Unstable Evolution repeated without end (bosspilot "stuck after 1500 actions") - Echo / "Repeatable this turn" spells are now floored at (1); GIL_504h was clickable for the boss; finale fixed bosses could also be drawn earlier; Hunter-card passive not in force before its AoE; dynamic hero-power cost did not update outside the hand.

=====================================================================
3. NOT DONE / LIMITS (read before relying on this)
=====================================================================

- The default schedule is NOT flipped: `default` is still acts-v2, `defaults` is absent and `defaultsPending` lists acts-v3h and acts-v3t. No new live run reaches an encounter yet. Ledger rows for the encounter content are therefore `partial` with blocker `run:default-schedule`, and boss hero rows stay `missing` until reachable.
- Both finales ship disabled with empty `fixedBosses` (no finale bosses exist yet). Whoever enables them (families 18 / 24 or the close-out) must re-pin ReleasedShapes[3].
- No twist is scripted (the twist roll and plumbing exist and are unit-tested; all twist pools are empty).
- Not scripted from the task's wider list: ULD_152, LOOTA_BOSS_35, LOOTA_BOSS_48, GILA_BOSS_25, ULDA_BOSS_44, TRLA_065p, BOT_543; ULDA_115 has its run-side constant (10 extra carried damage) but no card script; ULDA_720 has the HeroDamage state but no rule row.
- Relay has no hero choice: the next hero enters in list order.
- DeckTopKnown facts are recorded but nothing consumes them yet (OwnDeckLedger uses Replaced and Rewritten only).
- NOT verified in the real client: native transport of a form or relay swap (it uses Controller.AddHeroAndPower, the Jaraxxus path), c-nativeforms was not written, and the anomaly page UI compiles but was never exercised in the client. All verification here is engine games and harnesses.
- All four encounter decks are `substituted` or extended, none `authentic`: several Uldum collectibles are unscripted (Bloodsworn Mercenary, Restless Mummy, Plague of Wrath, Siamat), Rumble teammates are not boss-admitted (so Voone's variant decks contain no teammates), Amalgamate (GILA_BOSS_27t) and Animated Armor are dropped. Each is one `encounterAdaptations` entry. Rerun the encounters step when those cards get scripts.
- Icarax: the wiki phase-1 list has 29 cards; the wiki's "33-card deck" extras are not modelled; Commanding Scream shows Commanding Shout's enchantment (NEW1_036e2), not ULDA_BOSS_40p1e1.
- Hagatha: the run keeps its own hero instead of starting as Shaw; Toki's card is left out.

=====================================================================
4. HOW TO BUILD AND RUN (serialized; never two engine builds at once)
=====================================================================

All in P8:
- builds.sh - engine, plugin, all suite builds.
- runs.sh - the 40 suites, twice, compared.
- hall.sh - the hall harnesses.
- bp.sh <mode> - one bosspilot mode per call: run | fixture | catalog | act | shrine | shrineS2 | shrineS3 | shrineU | shrineT1 | shrineT2 | shrineD (released, compared with p7f) and enc01 | rules01 | plague | finale | relay (each twice, compared) and seeds.
- Encounter harness: P8\suites\c-bosspilot-encounter (out\AiHarness.exe). Environment: BOSS (comma list of row ids), FAMILY, BOSS_FROM, BOSS_COUNT, RULE, CARRIED, SCHEDULE, SEED_BASE, SCHEDULE_JSON, ENCOUNTERS_JSON, FINALE, PLAYER_HEALTH. A family runs its own bosses with FAMILY=<NN> or BOSS=<ids>.
- New suites: P8\suites\c-enc-model, P8\suites\c-enc-engine. Fixtures: P8\fixtures\schedule-finale.json, schedule-relay.json, encounters-relay.json.
- Generator: in C:\SoloStone\tools\dungeon-run-catalog, `dotnet run -c Release -- encounters`; then the ledger tool in C:\SoloStone\tools\restoration-ledger.

=====================================================================
5. HELPER APIS AND DATA CONVENTIONS FOR FAMILY IMPLEMENTERS
=====================================================================

5.1 Files a family touches (one of each, pre-created as empty stubs)
- Engine scripts: C:\SoloStone\engine\SabberStoneCore\src\CardSets\DungeonRun\DungeonRunEncounters.F<NN>.cs - fill `private static void F<NN>()`. Do not add a Register call; the static constructor already registers "01".."25".
- Admission rows: C:\SoloStone\plugin\DungeonRun\DungeonRunAdmission.F<NN>.cs - fill `private static AdmissionRule[] RulesF<NN>()`.
- Reference data: C:\SoloStone\data\dungeon-run\reference\encounters\F<NN>.json (hand-written transcription). Never edit encounters-v1.json or the `encounterAdaptations` / `encounterRejections` lists by hand; they are generated.
- Use C:\SoloStone\engine\SabberStoneCore\src\CardSets\DungeonRun\DungeonRunEncounters.F01.cs, C:\SoloStone\plugin\DungeonRun\DungeonRunAdmission.F01.cs and C:\SoloStone\data\dungeon-run\reference\encounters\F01.json as the worked pattern.

5.2 Engine registration helpers (private, inside partial class DungeonRunEncounters; encounter cards use their own client ids)
- `Power(string id, PlayReq[] reqs, Func<ISimpleTask> task, Func<Game,int> cost = null)` - an active boss hero power; `reqs` may be null; `cost` is a dynamic extra cost recomputed in AuraUpdate (EncounterRules.RefreshCosts).
- `Passive(string id, Func<Power> power)` - a passive boss hero power defined here (auto-listed in PassivePowers, started at match start).
- `ExistingPassive(string id)` - an already-scripted hero power that must behave as a passive for the boss (not clickable).
- `BossCard(string id, Func<CardDef> script, Action<Card> shape = null)` - a boss-only deck card.
- `Token(string id)` - a card that needs no script.
- `Enchantment(string id, Func<Power> power)`.
- `PlayerHeroCard(string id, Func<ISimpleTask> battlecry)` - a fight-only hero card for the run player.
- `Rule(string id, string effectId, Func<Power> effect)` - a global rule; `effectId` is the player enchantment attached to BOTH controllers.
- Public: `DungeonRunEncounters.All, Find(id), IsScripted(id), PassivePowers, NeedsActivation(powerId), IsExistingPassive(powerId)`.
- `DungeonRunGlobalRules.IsScripted(ruleId)`, `EffectOf(ruleId)`, `Apply(Game, ruleId)`, `IsActive(Controller, ruleId)`.

5.3 SabberStoneCore.Actions.EncounterRules (public API)
- `ActivatePower(HeroPower)`, `DeactivatePower(HeroPower)`.
- `SwapForm(Controller boss, int index)` - forced form change from a script (automatic changes come from the row's `forms[].at`).
- `ReplaceDeck(Controller c, IReadOnlyList<Card> cards, int draw)`.
- `DeckReplaced(Controller, IEnumerable<string>)`, `DeckRewritten(Controller, IEnumerable<string>)`, `DeckTopKnown(Controller, IEnumerable<string> topFirst)` - REQUIRED whenever a script replaces or rewrites a deck, so the own-deck ledger stays truthful.
- `HeroCardOf(Game, string heroCardId) -> RunFightHero`, `PlayHunterCard(Game, Controller, IPlayable source, int damage, TaskStack)`, `AttachPassive(Controller, string key)`.
- `Harvest(Game) -> RunMatchEnd` (also `DungeonRunSetup.Harvest(Game)`): BossRemainingHealth, BossForm, BossFormChanges, PlayerHeroDamage, HeroesUsed, NoLoss.
- `Game.RunDeckFacts`, `Game.EncounterOrNull` (the immutable RunEncounter of the match, null outside encounters).
- `RelayDraw = 4`.
- Tag table (the single place tags are allocated): FormIndex 32630 (boss controller), StartDone 32631 (game), RelayIndex 32632 (player controller), NoLoss 32633 (game), PowerActive 32634 (hero power), FormChanges 32635, CostWatch 32636. 32637-32659 are free for new model tags and must be added as constants in EncounterRules.cs only. 32660-32699 (RuleTagsFrom/RuleTagsTo) are for global-rule state. `IsPrivateTag(tag)` covers 32630..32699 and NativeMatch already hides that range from the client; do not use tags outside it for encounter state.

5.4 Run-layer plan fields (RunMatchPlan, set by DungeonRun.ApplyEncounter from the row; scripts never set them)
BossShrine, BossGeneratedSecrets, BossOpeningHandBonus, BossForms (RunForm {Hero, Power, Health, HealthAtMost, OnDeath, KeepExcess, Deck, Draw}), BossFormStart, BossCarriedDamage, PlayerCarriedDamage, Start (RunStart {BossBoard, PlayerBoard, BossDeckAdd, PlayerDeckAdd, BossHandAdd, PlayerHandAdd, BossWeapon, BossArmor}), GlobalRules, PlayerHeroCards / PlayerRelay (RunFightHero {Hero, Power, Health, Deck, Passives}), EncounterDeck, EncounterId.
Run-layer entry points that callers must use: `DungeonRun.BuildPlan(state, profile)`, `DungeonRun.MatchClasses(state)`, `DungeonRun.RecordOutcome(state, profile, outcome, result, DungeonRunSetup.Harvest(game))`.

5.5 Reference file F<NN>.json
Top level: `schemaVersion: 1`, `family`, `description`, `method`, `bosses: []`, `rejected: []`.
Boss row keys:
- `id` (lowercase `<prov>_<n>_<name>`, e.g. dala_21_whirt), `name`, `provenance` (LOOT|GIL|TRL|DALA|ULDA), `family` (the owning family number as a string), `hero`, `power`.
- `tier`: "1".."4" (pool enc-t<n>), "major1".."major3" (pool enc-major<n>), "finale" (pool finale).
- `archetype`, `level {source, original}`.
- Health: default is the act curve at the floor plus `healthOffset`; use `health` + `fixedHealth: true` only when Health is part of the boss's identity (fixed Health ignores modifier bossHealthDelta).
- `bossManaCap` (default 3); `start.bossMana` replaces the curve.
- `flags`: closed vocabulary (used so far: forms, persistentDamage, deckReplace, passive, heroSwitch, secrets, shrine, relay); the validator rejects unknown flags and a flag without its matching block.
- `cards`: extra ids the row needs that are in no deck (e.g. enchantments), so the tool checks them.
- `start`: the start block (boards, deck/hand adds, weapon, armor, bossMana).
- `bossGeneratedSecrets: true` when a boss power or card generates Secrets for the boss.
- `incompatibleModifiers`, `incompatibleRules`.
- `source {url, read}` - mandatory, one wiki page per boss.
- `lists: [{label, cards: [{name, count, id?}]}]` - by card NAME as on the wiki; give `id` only for a boss-only or ambiguous card.
- `deck {list: <label>}`.
- `forms: [{hero, power, health, at {healthAtMost}, keepExcess, deck {list}, draw}]`.
- `variants: [{id, shrine, deck {list}}]`.
- `heroCards: [{hero, deck {list}, passives [], skipFor []}]`; `relay: [{hero, power, deck, health, skipFor []}]`.
- `replacements` (named substitute per unusable card), `extend` (extra lists), `fillers` (names), `adaptations: [{kind, note}]`.
`rejected: [{id, name, power, reason, lift, cards}]`.
Deck rule: a combat tier deck is exactly 30 cards; a major, finale boss and form deck keep the listed count (10 to 60). An unusable card is replaced by the row's replacement or dropped; a short deck is filled with second copies of non-Legendary singles, then `extend`, then `fillers`. The step fails and writes nothing when a combat deck cannot reach 30. Where no authentic list exists, give a curated list and an `adaptations` entry; the generated deck `kind` is authentic | substituted | extended | curated.

5.6 Admission conventions
- Every new scripted id needs an admission row with a Reason naming the test that proves it; a rejected id gets Admit = AdmitScope.None with a Lift.
- A global rule card needs a row admitting `AdmitScope.Rule` (family "global-rule-symmetric" is the pattern), otherwise the ledger blocks it.
- A boss power or card that generates Secrets for the boss: `CatalogAdmit = AdmitScope.Boss` with `BossNeeds = BossNeedsGeneratedSecrets`, and the row must set `bossGeneratedSecrets: true`.
- A boss shrine id must be admitted `Player | Boss` (family "boss-shrine").

5.7 Schedule conventions (C:\SoloStone\data\dungeon-run\schedule-v1.json)
- Rule rows: `{id, kind: "anomaly"|"twist", provenance, scope: "act"|"run", effect: "match", weight, status: "admitted"}`. A rule row must be scripted (DungeonRunGlobalRules.IsScripted) and admitted. Anomalies are offered at an act's anomaly node (two rolled or none); twists go into an act's `twist.pool` with a `chance`.
- Do not edit acts-v1, acts-v2, act1 or the pool sets. Do not flip `defaults`, enable a finale, or change acts-v3h / acts-v3t acts: any of those changes the pinned shapes and belongs to the close-out, which must re-pin ReleasedShapes[3] and re-record the golden sequences.
- New rows enter the mixed pools only through their `tier`.

5.8 Save and compile conventions
- New save fields are append-only with ShouldSerialize guards; anything that changes the serialized shape needs a version bump and an identity-safe migration.
- Anything new in the encounter data model goes into DungeonRunData.cs itself so that projects compiling that file alone keep building.
- Ledger: add a landed-family entry in curation.json (`ids`, `rejected`, `supportRecords` for non-card support ids); the generator's self-check requires every id to be scripted, vanilla, or rejected with a reason.

5.9 Required per-family verification
A regression test per new boss / power / rule that plays it in a real engine game through the run setup path with the fair AI piloting the boss (c-bosspilot-encounter with FAMILY or BOSS, twice identical, 0 faults / fallbacks / swallowed, power-unused 0 or explained), then builds.sh, runs.sh, the 11 released bosspilot modes and hall.sh against the numbers in section 2, which are the new baseline (c-admission 2237, c-acts A6 7755E5589E3A15A3, c-deckops L1 6019C6E6F369F2D8, randplay regkeys 2140 / 0F45C329AB01A8F3, c-enc-model 141, c-enc-engine 122; everything else as before).
