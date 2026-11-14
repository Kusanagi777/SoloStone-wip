READ-ONLY MAP for phase 8 (encounters / global rules). Nothing was written in C:\SoloStone. All line numbers are from the current uncommitted tree.

## 1. How a boss is defined today
- Data: `C:\SoloStone\data\dungeon-run\run-v1.json` "bosses" (line ~439): 20 rows, all SoloStone originals. Row = `{id, name, band|final, archetype, hero, power, deck, health?, challenge?, unlock?}`. No row uses the phase-4 hooks yet.
- Model: `C:\SoloStone\plugin\DungeonRun\DungeonRunData.cs:207-230` `DungeonRunBoss` (HeroCard, Power, Deck, Unlock, Archetype, Band, Health?, Final, Challenge, `Pools`, `Provenance`="solostone", `Family`, `FixedHealth`, `BossManaCap`, `IncompatibleModifiers`). Parser 552-575; absent "pools" defaults to `["band<n>"]` or `["final"]`.
- Decks: `pool-v1.json` "bossDecks" (18 lists of 30) or an NPC library deck expanded from `data/npc/decks-v1.json` with sideboards and archetype (DungeonRunData.cs:673-703).
- Health: legacy `DungeonRun.cs:561` (boss.Health ?? band/final default). Act run `DungeonRun.cs:479-487`: act curve[floorInAct] + `HealthOffset` (own health minus band default, 493), or own health if `fixedHealth`, plus modifiers' bossHealthDelta. Start mana 501-507 (curve + modifiers, capped by bossManaCap, default 3).
- Legacy rules: `rules` = 8 floors, bandSplit [3,3,1], bandHealth [30,40,50], finalHealth 60.
- Powers: content keys in `C:\SoloStone\engine\SabberStoneCore\src\CardSets\DungeonRun\DungeonRunContent.cs` (boss passives ~385-420 on borrowed TB_ shells, `Passive()` 148-159) or real card ids. No original adventure boss hero, power or card is scripted (only LOOTA_BOSS_35p has an admission row, unscripted).

## 2. Schedule, pools, modifiers, pinning
- `C:\SoloStone\data\dungeon-run\schedule-v1.json`: scheduleVersion 2, default acts-v2; acts-v1, acts-v2, act1. Each act: floors, combat `[{pool,count}]`, major `{pool}`, bossHealth[], bossMana[], treasureAfter, taverns, passiveAtStart, modifierAtStart, draft, tavern, (v2) teammateAfter and shrineUpgradeAfter. `finale {enabled:false, fixedBosses:[], heroRelay:false}` is parsed only (DungeonRunData.cs:1103-1108) and the validator fails if enabled (`DungeonRunValidator.cs:919`).
- The 4 modifiers are numeric knobs only: whitelist `ModifierKnobs` DungeonRunData.cs:1030-1031; unknown knobs are a validator error (892). They are stored as `DungeonRunRule {Id, Act, FromNode}` in `state.ActiveRules` (`DungeonRunState.cs:144-151, 296-297`), read by `ActiveModifiers` (DungeonRun.cs:460-465). Offers: `ModifierCandidates`/`OpenModifier`/`PickModifier` DungeonRun.cs:1423-1464 (2 options, ModifierStream keyed on Node).
- Boss pick: `ActBossSequence` DungeonRun.cs:374-397, pre-rolled at NewRun (ActBossStream, act*10+slot, major act*10+9), no repeats. "leftover" = any boss with a pool starting with "band" and not Final (DungeonRunData.cs:1036-1039). Legacy `BossSequence` 351-365 uses `Band` and `Final` directly, not pools.
- Node layout: `DungeonRunSchedule.Flatten` DungeonRunData.cs:951-976; `Shape`/`ShapeDigest` 1012-1023; `ReleasedShapes` 995-1002 (v1: acts-v1 3293D82FA6BD48B3, act1 3673E8AE7F914E4F; v2 adds acts-v2 8D594CBB45303A53). Validator pins them (`DungeonRunValidator.cs:864-884`). Tuning values are not in the shape.
- Save pinning: state stores ScheduleVersion, Schedule, Node, Floor and BossSequence (ids). `ScheduleFor` DungeonRun.cs:415-432 refuses a mismatch; `CurrentBoss` 555 throws on an unknown id; `MatchSeed` 558 = (Seed, MatchStream, Floor). Bundle offers are stored; modifier and treasure offers are drawn from current data when the node opens.

Pinning hazards for this phase:
1. Adding a boss with `band` 1-3 or `final:true` changes legacy sequences; adding one to pools band1/band2/band3/final (or any pool named "band*") changes acts-v1/acts-v2/act1 sequences for every seed. New encounters need new pool names, band 0, final false, and a new schedule (acts-v3 under scheduleVersion 3 with a `ReleasedShapes[3]` entry that repeats the old three).
2. `schedules.Modifiers` is global. A new modifier or anomaly row changes offers on acts-v1/v2, including for saved runs at their next modifier node. Modifiers need version scoping in `ModifierCandidates`.
3. Never rename or remove the 20 existing boss ids; append new rows after them (the bosspilot harness seeds by `data.Bosses.IndexOf`).
4. `DungeonRun.cs:562` indexes `BandHealth[band-1]`: a band-0 boss must never reach the legacy path.
5. `catalog-v1.json` "sources" holds SHA-256 of run-v1.json and pool-v1.json. The loader checks only `poolBundlesHash` (DungeonRunData.cs:762, 817), but editing those files makes the recorded hashes stale, so regenerate the catalog or put encounters in a new embedded file (csproj lines 110-114, `DungeonRunRuntime.cs:16-20`, a new `Parse` parameter; both tools compile DungeonRunData.cs).

## 3. Match setup
- `RunMatchPlan` `C:\SoloStone\engine\SabberStoneCore\src\CardSets\DungeonRun\DungeonRunSetup.cs:16-89`. Boss side has only BossHero, BossHeroPowerId, BossHealth, BossDeck, BossSideboards, BossStartManaBonus. There is no boss shrine, start effect, board setup, opening-hand bonus, carried damage or global rule field.
- `Configure` 133-175: StartPlayer 1; `Player2ExcludeGeneratedSecrets = true` always (148); Player2HeroClass = BossHero.Class. `GameConfig.Player2OpeningHandBonus` exists (`Config\GameConfig.cs:139`) but is unused.
- `ApplyAfterStart` 181-224: deck mods, mana bonuses, treasures via `AttachEffect` (289-299, a player enchantment through `QuestRules.AddPlayerEffect`), Start of Game pending marks, player shrine `ShrineRules.Arm`. Extension point for a boss shrine, boss Start of Game hero mark, anomaly enchantments on both controllers and carried damage.
- Start of Game: `Actions\StartOfGameRules.cs` (`Declare`, `Pending` tag 32600, `Resolve` after both mulligans; Game.cs:1156), then `ActivateStartingPassiveHeroPowers` (Game.cs:1132-1144, 1157) for ids in `DungeonRunCardsGen.PassiveHeroPowers` (DungeonRunCardsGen.cs:19-20, built from `RunContentKind.PassivePower`). Darius's Cannons are the board-setup precedent (DungeonRunMonsterHunt.cs:213, 402; DungeonRunSetup.cs:219-223).
- A real passive boss power (88 boss-power texts say Passive) only starts if registered as PassivePower content keyed by its own id (pattern `DungeonRunTombsExplorers.cs:168-179`).
- Hero change in a match: `ReplaceHeroTask` (`Tasks\SimpleTasks\ReplaceHeroTask.cs`), `Controller.AddHeroAndPower` (Controller.cs:318; new hero at printed health, old hero to set-aside; player enchantments survive). Hook before death resolution: `CorpseRules.ResurrectHeroes` (`Actions\CorpseRules.cs:44-60`), called at Game.cs:851 and ~1618. That is the place for Plague Lord phases and an explorer relay. A new passive power must be activated by hand after the swap (as DungeonRunTombsActives.cs:339-340).
- Plugin: `C:\SoloStone\plugin\NativeMatch.cs:539-557` (ctor requires a 30-card boss deck, 546), 803-808 Configure, 848-853 ApplyAfterStart, 1232-1247 turn cap 100 and `EndDungeonRunFight`, 537 `DungeonRunResult`, 6760-6786 `TransportCardId`. Staging: `C:\SoloStone\plugin\LocalLadder.cs:165-225`; outcome 236-256 to `DungeonRunRuntime.RecordFightOutcome` (`DungeonRunRuntime.cs:307-333`).
- Engine to run channel: `RunMatchResult` (`DungeonRunContext.cs:134-162`: edits, slot logs, PowerOverride, NextFight), `Game.RunResult` NonSerialized (354), so it is outside the snapshot fingerprint. `RunContext` (15-95) is the read side.

## 4. Fair AI and admission
- Pilot: `C:\SoloStone\plugin\DungeonRun\DungeonRunBossPilot.cs:31-62` builds LocalDeck, NpcProfile, `AutoDeckPlan.Derive` (overlaid by a hand plan when `DeckPlans` has the deck id) and the Secret prior. `ParseArchetype` 64-75 throws on a missing archetype, so every new boss needs one.
- Boss powers are ordinary engine options to the search (`BotDriver.Decide`). `FairBotState.cs:289` does `new FairCard(player.Hero.HeroPower)` with no null guard (66-68): every boss must have a hero power. 4 of the 294 boss hero cards have no HERO_POWER tag 380.
- Table: `C:\SoloStone\plugin\DungeonRun\DungeonRunAdmission.cs`. Scopes 12-23; `Classify` 1025-1060. A card is a subject if it is a Secret, has tag 997 or a sideboard, matches a `Detect` phrase (972-979: secret, "start of game", "if your deck", "in your deck", "no duplicates", questline, deckbuilding, turn rewind), or is listed. A subject with no rule is rejected as "unclassified". The validator's boss check asks Boss scope for the power and every deck card. A rule with `Requires` must admit nothing (BuildIndex 985-999).
- Generated scope: `BuildGeneratedLists` 1072-1100 gives `GeneratedRejects` and `GeneratedRejectsBoss`, copied to the plan at DungeonRun.cs:659-660.
- Why boss-side generated Secrets are off:
  - The boss's certified Secret worlds (`FairBotState.PlausibleOpponentSecretWorlds` 1018-1068, used by `TurnPlanner.cs:694-697`) need a closed pool of the player's possible Secrets. `SecretPrior` (DungeonRunAdmission.cs:1327) is built from the player's deck sources and only certifies while the player cannot generate Secrets (FairBotState.cs:1063-1068).
  - Legacy runs got that by turning generated Secrets off for both sides with one flag. Phase 3 split the flag and lifted only the player side for catalog runs (`CatalogSecretPrior` 1303; it orders work, never certifies). The boss side stayed off because nothing needed it.
  - Consequences in the table: generator row 167-175 has CatalogAdmit Player|Generated only, so the generators land in `GeneratedRejectsBoss`; DALA_BOSS_21p row 177-183 admits nothing.
- To lift it: a per-plan flag that sets `Player2ExcludeGeneratedSecrets` off, on catalog runs only, because only `GeneratedRejects(catalogRun:true)` lists unscripted Secrets (1081-1083; the engine helpers at `Tasks\SpecificTask.cs:737-761` and `DungeonRunContent.cs:657-667` rely on that list). Then widen the rows for DALA_BOSS_21p and TRLA_168 (TRLA_110 too, if a boss holds that shrine) to Boss. A boss Secret the player later steals falls outside the prior; on catalog runs that only stops certification.
- Other pending lifts here: `BossDeckLedgerLift` 96 (`C:\SoloStone\plugin\Npc\OwnDeckLedger.cs` does not carry deck-wide rewrites; TIME_609t2 row 445-450 is the precedent), `BossShrineLift` 99 (rows 286, 312, 337, 355), `PlagueLordLift` 127 (ULDA_115 row 781-788).
- Fault counting: `C:\SoloStone\plugin\Npc\SearchTelemetry.cs`. New script state must be entity tags so clones replay it; 32630-32699 is unused. Any new private tag must join `NativeMatch.IsDecisionInternalTag` (~6795-6806).

## 5. Validator constraints an original boss will hit
`C:\SoloStone\plugin\DungeonRun\DungeonRunValidator.cs:641-673`:
- Deck must be exactly 30 (647, 667; also NativeMatch.cs:546). Original lists vary in size.
- Every deck card must be scripted, client-known and admitted for Boss.
- Deck card class must fit the portrait's class (661-662). All 294 boss hero cards carry a class tag (one NEUTRAL); authentic mixed-class lists will fail unless this is relaxed per provenance.
- Archetype required (666). Portrait must not be a player row's (670-673).
- `Hero()` 285-291 needs only existence, client knowledge and type HERO, so real boss hero cards work unscripted.
- Content kind to card type map 705-711 needs a case for any new `RunContentKind` (the anomaly cards are SPELLs with "... Player Enchantment" `e` cards, the same shape as passive treasures).
- Any validation error disables the whole mode (`DungeonRunRuntime.cs:73-75`); there is no drop-unknown path for bosses as there is for catalog cards (DropClientUnknown 128).
- Legacy invariants 717-724 count only bands 1-3 and `Final`.

## 6. Run-owned cross-match state
- Save: `C:\SoloStone\plugin\DungeonRun\DungeonRunState.cs`, version 5 (444), append-only with `ShouldSerialize*` so an old run's body is byte-identical. Migration `DungeonRunMigration.cs:21-80`; 4 to 5 is identity, and 5 to 6 can be too.
- A non-win ends the run (`RecordOutcome` DungeonRun.cs:710-730, line 721) and `ApplyMatchResult` runs on a win only (723). Persistent boss damage therefore needs:
  - the boss hero's damage reported at match end (a new `RunMatchResult` field, or read from the live game in NativeMatch and in the harness);
  - a state list such as boss id, damage, phase, recorded on any outcome but Fault;
  - a rule for what a loss to a phased boss means. Decision for the build: either a retry or relay with the next hero, or one Plague Lord split over several majors. Without one of these, run-owned damage has no effect, since a loss ends the run.
  - a plan field for carried damage (set `Player2.Hero.Damage` in ApplyAfterStart, or reduce BossHealth), and a MatchSeed component for attempt or phase so a retry is not the same shuffle (558).
- ULDA_115 then needs only a result field plus that list. Battle Scars (ULDA_720) is the same mechanism on the player side.
- Temporary hero switch for one fight: `BuildPlan` 637-680 reads `RunClass(state)` (685), `PlayerPower` (568), `cls.Health`, and the deck. Other readers of the class to keep consistent: `BuildContext`/`RunClassesOf` 599-620, `TreasurePool` 583-597, `DungeonRunBossPilot.Build(runClasses)` (LocalLadder.cs:179), `DungeonRunRuntime.BeginMatch` names (286-297), pre-match UI (`NativeDungeonRunUI.cs:527-538`), and the `Finish` archive (818-851).
- Anomalies and twists: reuse `ActiveRules`; add a modifier field naming an engine rule, a plan list, and attach to both controllers in ApplyAfterStart. `DungeonRunMatchSummary.Family` is already written (DungeonRun.cs:717). UI text: `NativeDungeonRunText.ModifierEffect` 146 and `ModifierPage` (NativeDungeonRunUI.cs:559-575) print knobs only.

## 7. Boss shrine
- `C:\SoloStone\engine\SabberStoneCore\src\Actions\ShrineRules.cs`: `Arm(c, id, copies)` 117-129 and `Place(c, id)` 142-151 take any controller. No shrine script names Player1 or Player2. Dormancy, revival and guards (160-234) are controller-relative. TRLA_806 already targets enemy Shrines.
- Missing: a plan field, a boss row field, a validator check (`ShrineRules.IsScripted` plus Boss admission), Boss scope on the shrine rows, boss-side generated Secrets for TRLA_110, and a decision on TRLA_186 for a boss owner.
- Typhoon: `C:\SoloStone\engine\SabberStoneCore\src\CardSets\Client\Sets\EMERALD_DREAM\Typhoon.cs:19-34` calls `minion.Zone.Remove` and changes controller with no `ShrineRules.InterceptLeave`/`BlocksControl` (other call sites: Generic.cs:96, 292, 388; MoveToDeck.cs:44; MoveToSetaside.cs:38). The fix is a skip at the top of the loop.

## 8. Native UI and client-known checks
- Client check: `clientKnows = id => ClientBridge.ClientKnowsCard(NativeMatch.TransportCardId(id))` (DungeonRunRuntime.cs:68), used by `Validate` for boss hero, power, deck and content shells. The DBF and facts file contain all LOOTA/GILA/TRLA/DALA/ULDA boss heroes, including the phase heroes ULDA_BOSS_37h2/h3 etc.
- The run hub shows bosses as text only (NativeDungeonRunUI.cs:527-534, 293-296); there is no boss portrait there. In a match the client renders the hero card itself. Name overrides come from `matchNames` (DungeonRunRuntime.cs:286-297, 344-364); real boss cards already carry the right names.
- Mid-match hero replacement on the native transport is proven only for played hero cards (Jaraxxus handling, NativeMatch.cs:3343-3350, 4380+). A boss phase swap or explorer relay via ReplaceHeroTask is untested there.

## 9. Scope numbers
From `C:\SoloStone\docs\design\restoration-ledger.csv` (776 missing or partial rows):
- 294 boss hero rows (18 are the HERO_ portraits in use). 257 rows are blocked only by `run:encounter-roster;data:original-boss-decks-absent`.
- 366 boss power rows: 261 unscripted, 20 implemented, 85 support.
- 64 boss cards, all unscripted (Monster Hunt 25, Heist 20, Dungeon Run 16, Tombs 3).
- 35 anomalies, 9 twists.
- Special finales:
  - Hagatha: GILA_BOSS_61h, 100 Health, power GIL_504h.
  - Plague Lords: ULDA_BOSS_37h-40h and 67h at 300 Health, with client phase heroes h2 (200) and h3 (100) and powers p1/p2/p3.
  - Explorer relay: ULDA_Explorers "Saviors of Uldum" (200 Health, no power); the finale decks are DECK rows in `data\dungeon-run\reference\adventures.json` "otherPrefixedDecks".
  - Heist finale: DALA_830.
  - Rumble finals: TRLA_200h-208h; TRLA_065p belongs to TRLA_204h.
- Boss decks are not in the client. Provenance pattern to follow: `data\dungeon-run\reference\buckets.json` (description and method name the wiki and date). Curation: `C:\SoloStone\tools\restoration-ledger\curation.json` (finalBosses, finaleRecords, encounterPowers, landedFamilies with a generator self-check).
- Ledger tool reads bosses at `tools\restoration-ledger\Program.cs:790-800, 878-890` (labels final or band only) and validates boss powers on a synthetic band-1 boss (1139-1150); blocker tags at 1459-1464, 1512-1513.

## 10. Tests and digests
- Suites live in the scratchpad, not the repo. Template for "a real game through the run setup path with the fair AI": `C:\Users\user\AppData\Local\Temp\claude\0a564ffc8743aeec\d2a4e245-ca37-4433-9b55-c35ed325f081\scratchpad\audit-verify\c-bosspilot\Program.cs` lines 112-203 (BuildPlan, Build pilot, Configure, StartGame, ApplyAfterStart, BotDriver.Decide loop; counts faults, fallbacks, swallowed).
- That harness loops every `data.Bosses` row and picks the floor from Band/Final (line 119): new band-0 bosses would be fought at floor 5 (act) or 7 (legacy) unless it is updated or filtered by provenance.
- The stale label "act run: acts-v1 schedule" (it runs the default, acts-v2) is at line 197 and comment 31-32 of that file, and in 7 copies: p7\suites\c-bosspilot-shrine, p7s2\...-shrine-S2, p7s3\...-shrine-S3, p7u\...-shrine-U, p7t1\...-teammates-T1, p7t2\...-teammates-T2, p7d\...-companions-D. Fixing it changes the last-but-one output line of the act mode.
- The harness csproj globs `plugin\DungeonRun\*.cs` and `plugin\Npc\**`; a new plugin file elsewhere must be added to each csproj.
- selfplay/randplay digests: new scripts on non-collectible adventure cards kept them stable in phases 6-7. Risks are new serialized Game/Controller fields and any behaviour change on the `ResurrectHeroes` path for non-run games.
