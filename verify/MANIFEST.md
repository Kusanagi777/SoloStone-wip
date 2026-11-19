# verify/ manifest: where every piece came from

Everything the wave-3 baseline (`handoff/phase8/reports/integrate-wave3.md`, section 7) builds, runs or compares
against, mapped from its old place to its place here. "Old place" paths are relative to the old session scratchpad
(`SP` below), which lived only on the original Windows PC; `C:\SoloStone` is this repository. Columns: source ->
destination -> purpose. "Group" says who ports a suite that is not ported yet (pilot = ported and proved; G1, G2, G3 =
the three porting groups of README.md section 7).

## 1. Runners

| Old place | Here | Purpose |
|---|---|---|
| `SP/p8w3/builds.sh` | `verify/bin/builds.sh` | build stage: the 67 suite projects in the wave-3 order, plus `randplay`, `tool`, `pilot`, `only` |
| `SP/p8w3/runs.sh` | `verify/bin/runs.sh` | run stage: the 54 entries in the wave-3 order, `dotnet out/<Name>.dll` from each suite folder |
| `SP/p8w3/same.sh` | `verify/bin/same.sh` | run 1 vs run 2, and run 1 vs `verify/baseline/runs` (was: vs the wave-2 logs `run-p8w2-2.txt`) |
| `SP/p8w3/hall.sh` + `SP/audit-verify/one.sh` | `verify/bin/hall.sh` | the audit hall: base harness, 38 audit harnesses, `same` against `verify/baseline/hall.txt` |
| `SP/p8w3/bp.sh` | `verify/bin/bp.sh` | bosspilot modes (all modes of the wave-3 script); results compared with `verify/baseline/bp` |
| `SP/p8w3/cross06.sh` | `verify/bin/cross06.sh` | family 06 rules on the twelve family 09/10 bosses |
| `SP/p8w3/rules06-rest.sh` | (folded into `verify/bin/final.sh rules06` and `bp.sh rules06 <rule>`) | |
| `SP/p8w3/final.sh` | `verify/bin/final.sh` | all stages in order: engine builds runs1 enc rules06 cross06 released runs2 same hall |
| lane guide 2.1 (engine build command) | `verify/bin/engine.sh` | the one engine build, into `verify/.out/engine-out` |
| lane guide 2.3 (generators) | `verify/bin/gen.sh` | encounters, catalog, ledger generators (write into the repository) |
| (new) | `verify/bin/build-all.sh` | plugin-free build of everything: engine, catalog tool, 67 suites, randplay, 39 hall projects |
| (new) | `verify/lib/env.sh`, `verify/lib/suites.sh` | repo-relative locations; the suite registry (build, run, hall, pilot lists) |
| (new) | `verify/Directory.Build.props`, `verify/lib/VerifyPaths.cs` | `$(SoloStoneRoot)`, `$(EngineDll)`, `$(EngineDir)` for every .csproj; how a Program.cs finds files |
| (new) | `verify/tools/port_paths.py` | the mechanical part of porting a suite (path literals, csproj includes) |
| `SP/p8lanes/hrun.sh` (copy in `handoff/phase8/hrun.sh`) | not moved | the cross-lane harness lock of the original PC; the runners do not call it (README.md section 5) |

## 2. Suites

Each suite folder holds its `*.cs` and `*.csproj` only (no `Program.cs.pre-*` backups, no logs, no `obj/`, `out/`).

| Old place (under SP) | Here | Group | Files |
|---|---|---|---|
| `audit-verify/c-runlayer` | `verify/suites/audit-verify/c-runlayer` | pilot | Program.cs RunLayer.csproj |
| `audit-verify/c-catalog` | `verify/suites/audit-verify/c-catalog` | pilot | Catalog.csproj Program.cs |
| `audit-verify/c-content` | `verify/suites/audit-verify/c-content` | pilot | Content.csproj Program.cs |
| `audit-verify/c-admission` | `verify/suites/audit-verify/c-admission` | pilot | Admission.csproj Program.cs |
| `audit-verify/c-secretscan` | `verify/suites/audit-verify/c-secretscan` | G1 | Program.cs SecretScan.csproj |
| `audit-verify/c-secretsearch` | `verify/suites/audit-verify/c-secretsearch` | G1 | Program.cs SecretSearch.csproj |
| `audit-verify/c-bosspilot` | `verify/suites/audit-verify/c-bosspilot` | G1 | BossPilot.csproj Program.cs |
| `audit-verify/c-acts` | `verify/suites/audit-verify/c-acts` | pilot | Acts.csproj Program.cs |
| `audit-verify/c-deckops` | `verify/suites/audit-verify/c-deckops` | pilot | D12.cs DeckOps.csproj Program.cs |
| `audit-verify/selfplay` | `verify/suites/audit-verify/selfplay` | pilot | Program.cs SelfPlay.csproj |
| `audit-verify/c-deferrals` | `verify/suites/audit-verify/c-deferrals` | G1 | Deferrals.csproj Program.cs |
| `p6f15/suites/c-dkdh-supply` | `verify/suites/p6f15/c-dkdh-supply` | G1 | Program.cs Supply.csproj |
| `p6f15/suites/c-heroes` | `verify/suites/p6f15/c-heroes` | G1 | Heroes.csproj Program.cs |
| `p6f15/suites/c-heroes-F14` | `verify/suites/p6f15/c-heroes-F14` | G1 | HeroesF14.csproj Program.cs |
| `p6f15/suites/c-heroes-F6` | `verify/suites/p6f15/c-heroes-F6` | G1 | HeroesF6.csproj Program.cs |
| `p6f15/suites/c-heroes-F9` | `verify/suites/p6f15/c-heroes-F9` | G1 | HeroesF9.csproj Program.cs |
| `p6f15/suites/c-runctx` | `verify/suites/p6f15/c-runctx` | G1 | Program.cs RunCtx.csproj |
| `p6f15/suites/c-starters-F10` | `verify/suites/p6f15/c-starters-F10` | G1 | Program.cs StartersF10.csproj |
| `p6f15/suites/c-treasures-F1` | `verify/suites/p6f15/c-treasures-F1` | G1 | Program.cs TreasuresF1.csproj |
| `p6f15/suites/c-treasures-F11` | `verify/suites/p6f15/c-treasures-F11` | G1 | Program.cs TreasuresF11.csproj |
| `p6f15/suites/c-treasures-F12` | `verify/suites/p6f15/c-treasures-F12` | G1 | Program.cs TreasuresF12.csproj |
| `p6f15/suites/c-treasures-F13` | `verify/suites/p6f15/c-treasures-F13` | G1 | Program.cs TreasuresF13.csproj |
| `p6f15/suites/c-treasures-F2` | `verify/suites/p6f15/c-treasures-F2` | G1 | Program.cs TreasuresF2.csproj |
| `p6f15/suites/c-treasures-F3` | `verify/suites/p6f15/c-treasures-F3` | G1 | Program.cs TreasuresF3.csproj |
| `p6f15/suites/c-treasures-F4` | `verify/suites/p6f15/c-treasures-F4` | G1 | Program.cs TreasuresF4.csproj |
| `p6f15/suites/c-treasures-F5` | `verify/suites/p6f15/c-treasures-F5` | G1 | Program.cs TreasuresF5.csproj |
| `p6f15/suites/c-treasures-F7` | `verify/suites/p6f15/c-treasures-F7` | G1 | Program.cs TreasuresF7.csproj |
| `p6f15/suites/c-treasures-F8` | `verify/suites/p6f15/c-treasures-F8` | G1 | Program.cs TreasuresF8.csproj |
| `p6f15/suites/c-upgrades` | `verify/suites/p6f15/c-upgrades` | G1 | Program.cs Upgrades.csproj |
| `p6f16/suites/c-dkdh-play` | `verify/suites/p6f16/c-dkdh-play` | G1 | DkdhPlay.csproj Program.cs |
| `p6f18/suites/c-review-F18` | `verify/suites/p6f18/c-review-F18` | G1 | Program.cs ReviewF18.csproj |
| `p7/suites/c-shrine-core` | `verify/suites/p7/c-shrine-core` | pilot | Program.cs ShrineCore.csproj |
| `p7s1/suites/c-shrines-S1` | `verify/suites/p7s1/c-shrines-S1` | G2 | Program.cs ShrinesS1.csproj |
| `p7s2/suites/c-shrines-S2` | `verify/suites/p7s2/c-shrines-S2` | G2 | Program.cs ShrinesS2.csproj |
| `p7s3/suites/c-shrines-S3` | `verify/suites/p7s3/c-shrines-S3` | G2 | Program.cs ShrinesS3.csproj |
| `p7u/suites/c-shrine-upgrades` | `verify/suites/p7u/c-shrine-upgrades` | G2 | Program.cs ShrineUpgrades.csproj |
| `p7t1/suites/c-teammates-T1` | `verify/suites/p7t1/c-teammates-T1` | G2 | Program.cs TeammatesT1.csproj |
| `p7t2/suites/c-teammates-T2` | `verify/suites/p7t2/c-teammates-T2` | G2 | Program.cs TeammatesT2.csproj |
| `p7d/suites/c-companions-D` | `verify/suites/p7d/c-companions-D` | G2 | CompanionsD.csproj Program.cs |
| `p7f/suites/c-p7fix` | `verify/suites/p7f/c-p7fix` | G2 | P7Fix.csproj Program.cs |
| `p8f01/suites/c-enc-model` | `verify/suites/p8f01/c-enc-model` | pilot | EncModel.csproj Program.cs |
| `p8f01/suites/c-enc-engine` | `verify/suites/p8f01/c-enc-engine` | pilot | EncEngine.csproj Program.cs |
| `p8f01b/suites/c-enc-secrets` | `verify/suites/p8f01b/c-enc-secrets` | G3 | EncSecrets.csproj Program.cs |
| `p8f01/suites/c-bosspilot-encounter` | `verify/suites/p8f01/c-bosspilot-encounter` | pilot | BossPilot.csproj Program.cs |
| `p7/suites/c-bosspilot-shrine` | `verify/suites/p7/c-bosspilot-shrine` | G2 | BossPilot.csproj Program.cs |
| `p7s2/suites/c-bosspilot-shrine-S2` | `verify/suites/p7s2/c-bosspilot-shrine-S2` | G2 | BossPilot.csproj Program.cs |
| `p7s3/suites/c-bosspilot-shrine-S3` | `verify/suites/p7s3/c-bosspilot-shrine-S3` | G2 | BossPilot.csproj Program.cs |
| `p7u/suites/c-bosspilot-shrine-U` | `verify/suites/p7u/c-bosspilot-shrine-U` | G2 | BossPilot.csproj Program.cs |
| `p7t1/suites/c-bosspilot-teammates-T1` | `verify/suites/p7t1/c-bosspilot-teammates-T1` | G2 | BossPilot.csproj Program.cs |
| `p7t2/suites/c-bosspilot-teammates-T2` | `verify/suites/p7t2/c-bosspilot-teammates-T2` | G2 | BossPilot.csproj Program.cs |
| `p7d/suites/c-bosspilot-companions-D` | `verify/suites/p7d/c-bosspilot-companions-D` | G2 | BossPilot.csproj Program.cs |
| `p8f02/suites/c-collectibles-A` | `verify/suites/p8f02/c-collectibles-A` | G3 | CollectiblesA.csproj Program.cs |
| `p8f02/suites/c-bosspilot-collect` | `verify/suites/p8f02/c-bosspilot-collect` | G3 | BossPilotCollect.csproj Program.cs |
| `p8f03/suites/c-collectibles-B` | `verify/suites/p8f03/c-collectibles-B` | G3 | CollectiblesB.csproj Program.cs |
| `p8f04/suites/c-collectibles-C` | `verify/suites/p8f04/c-collectibles-C` | G3 | CollectiblesC.csproj Program.cs |
| `p8f04/suites/c-bosspilot-f04` | `verify/suites/p8f04/c-bosspilot-f04` | G3 | BossPilot.csproj Program.cs |
| `p8w1/suites/c-w1fix` | `verify/suites/p8w1/c-w1fix` | G3 | Program.cs W1Fix.csproj |
| `p8f05/suites/c-rules-05` | `verify/suites/p8f05/c-rules-05` | G2 | Program.cs Rules05.csproj |
| `p8f05/suites/c-bosspilot-rules05` | `verify/suites/p8f05/c-bosspilot-rules05` | G2 | BossPilot.csproj Program.cs |
| `p8f07/suites/c-enc-07` | `verify/suites/p8f07/c-enc-07` | G3 | EncF07.csproj Program.cs |
| `p8f08/suites/c-enc-08` | `verify/suites/p8f08/c-enc-08` | G3 | Enc08.csproj Program.cs |
| `p8f06/suites/c-rules-06` | `verify/suites/p8f06/c-rules-06` | G2 | Program.cs Rules06.csproj |
| `p8f06/suites/c-bosspilot-rules06` | `verify/suites/p8f06/c-bosspilot-rules06` | G2 | BossPilot.csproj Program.cs |
| `p8f09/suites/c-enc-09` | `verify/suites/p8f09/c-enc-09` | G3 | Enc09.csproj Program.cs |
| `p8f09/suites/c-bosspilot-enc09` | `verify/suites/p8f09/c-bosspilot-enc09` | G3 | BossPilot.csproj Program.cs |
| `p8f10/suites/c-enc-10` | `verify/suites/p8f10/c-enc-10` | G3 | Enc10.csproj Program.cs |
| `p8w3/suites/c-w3fix` | `verify/suites/p8w3/c-w3fix` | G3 | Program.cs W3Fix.csproj |
| `audit-verify/randplay` | `verify/suites/audit-verify/randplay` | pilot | EngineProof.csproj Program.cs |
| (new in wave 4, family 11 lane) | `verify/suites/p8f11/c-enc-11` | wave 4 | Enc11.csproj Program.cs |
| (new in wave 4, family 12 lane) | `verify/suites/p8f12/c-enc-12` | wave 4 | Enc12.csproj Program.cs |
| (new in wave 4, family 13 lane) | `verify/suites/p8f13/c-enc-13` | wave 4 | Enc13.csproj Program.cs |
| (new in wave 4, the integration's fix) | `verify/suites/p8w4/c-w4fix` | wave 4 | Program.cs W4Fix.csproj |
| (new in wave 5, family 14 lane) | `verify/suites/p8f14/c-enc-14` | wave 5 | Enc14.csproj Program.cs |
| (new in wave 5, family 15 lane) | `verify/suites/p8f15/c-enc-15` | wave 5 | Enc15.csproj Program.cs |
| (new in wave 5, family 16 lane) | `verify/suites/p8f16/c-enc-16` | wave 5 | Enc16.csproj Program.cs |
| (new in wave 5, family 17 lane) | `verify/suites/p8f17/c-enc-17` | wave 5 | Enc17.csproj Program.cs |
| (new in wave 5, family 18 lane) | `verify/suites/p8f18/c-enc-18` | wave 5 | Enc18.csproj Program.cs |
| (new in wave 5, family 19 lane) | `verify/suites/p8f19/c-enc-19` | wave 5 | Enc19.csproj Program.cs |
| (new in wave 5, the integration's fixes) | `verify/suites/p8w5/c-w5fix` | wave 5 | Program.cs W5Fix.csproj (wave 6 added the Barkeye/Vessina pairs to Q and the Tekahn tripwire Q7) |
| (new in wave 6, family 20 lane) | `verify/suites/p8f20/c-enc-20` | wave 6 | Enc20.csproj Program.cs |
| (new in wave 6, family 21 lane) | `verify/suites/p8f21/c-enc-21` | wave 6 | Enc21.csproj Program.cs |
| (new in wave 6, family 22 lane) | `verify/suites/p8f22/c-enc-22` | wave 6 | Enc22.csproj Program.cs |
| (new in wave 6, family 23 lane) | `verify/suites/p8f23/c-enc-23` | wave 6 | Enc23.csproj Program.cs |
| (new in wave 6, family 24 lane) | `verify/suites/p8f24/c-enc-24` | wave 6 | Enc24.csproj Program.cs |
| (new in step 9, test lane econ) | `verify/suites/p9/c-sched-9` | step 9 | Sched9.csproj Program.cs Dump9.cs (F1 tripwire against `fixtures/p9/sched9-f62275b.txt`, recorded by `tools/gen/p9/record_sched9.sh`) |
| (new in step 9, test lane econ) | `verify/suites/p9/c-econ-9` | step 9 | Econ9.csproj Program.cs (plan 1 A on the run layer) |
| (new in step 9, test lane rules) | `verify/suites/p9/c-rules-9` | step 9 | Rules9.csproj Program.cs (plan 1 C: C1-C5, C7; fixture `fixtures/p9/schedule-twists9.json`) |
| (new in step 9, test lane rules) | `verify/suites/p9/c-bosspilot-pairs9` | step 9 | BossPilot.csproj Program.cs (C6, `bp.sh pairs9 <chunk> <k>`) |
| (new in step 9, test lane ends) | `verify/suites/p9/c-ends-9` | step 9 | Ends9.csproj Program.cs (lane X: escapes, restarts, ratchet, D3, save fields and checks) |
| (new in step 9, test lane ends) | `verify/suites/p9/c-bosspilot-ends9` | step 9 | BossPilot.csproj Program.cs (`bp.sh ends9 <escape|refuse|rewind|toki|tekahn> <k>`) |
| (new in step 9, test lane gen) | `verify/suites/p9/c-gen-9` | step 9 | Gen9.csproj Program.cs (lane G: generation hygiene) |
| (new in step 9, test lane long) | `verify/suites/p9/c-longrun-9` | step 9 | LongRun9.csproj Program.cs, links `p9/common/PickPolicy.cs` and `LongRunWalker.cs` (D1, D2, D4, C1 long walks) |
| (new in step 9, lane M) | `verify/suites/p9/c-bosspilot-run` | step 9 | BossPilotRun.csproj Program.cs RunPilot.cs PlayGame.cs (the encounter harness's game loop, copied: the four generators patch that loop by its text, so it stays inline in `p8f01/c-bosspilot-encounter`); built by `builds.sh only p9/c-bosspilot-run`, run by `bp.sh slots9`, `outliers9`, `finale9`, `tekahn9`, `longrun9`, `treasure9`, `gate48t`, `uld152` |

## 3. The audit hall (all G3)

The hall harnesses compile `plugin/DungeonRun/DungeonRunData.cs` and `plugin/Npc/World/NpcWorldJson.cs` and reference `$(EngineDir)`.

| Old place (under SP) | Here | Group | Files |
|---|---|---|---|
| `audit-harness` (AuditHarness.csproj, Lib.cs, Runner.cs, README.md, Tests/*.cs; not engine-out/) | `verify/suites/hall/audit-harness` | G3 | base harness of the hall (`hall.sh harness`) |
| `audit-verify/h-audit-chunk-1` (= `audit-chunk-1` as copied by audit-verify/one.sh) | `verify/suites/hall/audit-chunk-1` | G3 | AuditHarness.csproj Lib.cs Runner.cs Tests/: Chunk1.cs Examples.cs |
| `audit-verify/h-audit-chunk-10` (= `audit-chunk-10` as copied by audit-verify/one.sh) | `verify/suites/hall/audit-chunk-10` | G3 | AuditHarness.csproj Lib.cs Runner.cs Tests/: Chunk10Priest.cs Examples.cs |
| `audit-verify/h-audit-chunk-11` (= `audit-chunk-11` as copied by audit-verify/one.sh) | `verify/suites/hall/audit-chunk-11` | G3 | AuditHarness.csproj Lib.cs Runner.cs Tests/: Examples.cs RogueChunk11.cs |
| `audit-verify/h-audit-chunk-12` (= `audit-chunk-12` as copied by audit-verify/one.sh) | `verify/suites/hall/audit-chunk-12` | G3 | AuditHarness.csproj Lib.cs Runner.cs Tests/: Examples.cs ShamanChunk12.cs |
| `audit-verify/h-audit-chunk-13` (= `audit-chunk-13` as copied by audit-verify/one.sh) | `verify/suites/hall/audit-chunk-13` | G3 | AuditHarness.csproj Lib.cs Runner.cs Tests/: Chunk13Warlock.cs Examples.cs |
| `audit-verify/h-audit-chunk-14` (= `audit-chunk-14` as copied by audit-verify/one.sh) | `verify/suites/hall/audit-chunk-14` | G3 | AuditHarness.csproj Lib.cs Runner.cs Tests/: Chunk14Warrior.cs Examples.cs |
| `audit-verify/h-audit-chunk-15` (= `audit-chunk-15` as copied by audit-verify/one.sh) | `verify/suites/hall/audit-chunk-15` | G3 | AuditHarness.csproj Lib.cs Runner.cs Tests/: Chunk15.cs Examples.cs |
| `audit-verify/h-audit-chunk-16` (= `audit-chunk-16` as copied by audit-verify/one.sh) | `verify/suites/hall/audit-chunk-16` | G3 | AuditHarness.csproj Lib.cs Runner.cs Tests/: Chunk16.cs Examples.cs |
| `audit-verify/h-audit-chunk-17` (= `audit-chunk-17` as copied by audit-verify/one.sh) | `verify/suites/hall/audit-chunk-17` | G3 | AuditHarness.csproj Lib.cs Runner.cs Tests/: Chunk17.cs Dump17.cs Examples.cs |
| `audit-verify/h-audit-chunk-18` (= `audit-chunk-18` as copied by audit-verify/one.sh) | `verify/suites/hall/audit-chunk-18` | G3 | AuditHarness.csproj Lib.cs Runner.cs Tests/: Chunk18.cs Examples.cs |
| `audit-verify/h-audit-chunk-19` (= `audit-chunk-19` as copied by audit-verify/one.sh) | `verify/suites/hall/audit-chunk-19` | G3 | AuditHarness.csproj Lib.cs Runner.cs Tests/: Chunk19.cs Examples.cs |
| `audit-verify/h-audit-chunk-2` (= `audit-chunk-2` as copied by audit-verify/one.sh) | `verify/suites/hall/audit-chunk-2` | G3 | AuditHarness.csproj Lib.cs Runner.cs Tests/: Chunk2.cs Examples.cs |
| `audit-verify/h-audit-chunk-3` (= `audit-chunk-3` as copied by audit-verify/one.sh) | `verify/suites/hall/audit-chunk-3` | G3 | AuditHarness.csproj Lib.cs Runner.cs Tests/: Chunk3.cs Examples.cs |
| `audit-verify/h-audit-chunk-4` (= `audit-chunk-4` as copied by audit-verify/one.sh) | `verify/suites/hall/audit-chunk-4` | G3 | AuditHarness.csproj Lib.cs Runner.cs Tests/: Chunk4.cs Examples.cs |
| `audit-verify/h-audit-chunk-5` (= `audit-chunk-5` as copied by audit-verify/one.sh) | `verify/suites/hall/audit-chunk-5` | G3 | AuditHarness.csproj Lib.cs Runner.cs Tests/: Druid1.cs Examples.cs |
| `audit-verify/h-audit-chunk-6` (= `audit-chunk-6` as copied by audit-verify/one.sh) | `verify/suites/hall/audit-chunk-6` | G3 | AuditHarness.csproj Lib.cs Runner.cs Tests/: DruidChunk2.cs Examples.cs |
| `audit-verify/h-audit-chunk-7` (= `audit-chunk-7` as copied by audit-verify/one.sh) | `verify/suites/hall/audit-chunk-7` | G3 | AuditHarness.csproj Lib.cs Runner.cs Tests/: Chunk7Hunter.cs Examples.cs |
| `audit-verify/h-audit-chunk-8` (= `audit-chunk-8` as copied by audit-verify/one.sh) | `verify/suites/hall/audit-chunk-8` | G3 | AuditHarness.csproj Lib.cs Runner.cs Tests/: Examples.cs MageChunk.cs |
| `audit-verify/h-audit-chunk-9` (= `audit-chunk-9` as copied by audit-verify/one.sh) | `verify/suites/hall/audit-chunk-9` | G3 | AuditHarness.csproj Lib.cs Runner.cs Tests/: Chunk9Paladin.cs Examples.cs |
| `audit-verify/h-audit-fix-Amirdrassil_cs` (= `audit-fix-Amirdrassil_cs` as copied by audit-verify/one.sh) | `verify/suites/hall/audit-fix-Amirdrassil_cs` | G3 | AuditHarness.csproj Lib.cs Runner.cs Tests/: AmirdrassilFix.cs Examples.cs |
| `audit-verify/h-audit-fix-BoomsdayCardsGen_cs` (= `audit-fix-BoomsdayCardsGen_cs` as copied by audit-verify/one.sh) | `verify/suites/hall/audit-fix-BoomsdayCardsGen_cs` | G3 | AuditHarness.csproj Lib.cs Runner.cs Tests/: Examples.cs StormChaser.cs |
| `audit-verify/h-audit-fix-DalaranCardsGen_cs` (= `audit-fix-DalaranCardsGen_cs` as copied by audit-verify/one.sh) | `verify/suites/hall/audit-fix-DalaranCardsGen_cs` | G3 | AuditHarness.csproj Lib.cs Runner.cs Tests/: DalaranFix.cs Examples.cs |
| `audit-verify/h-audit-fix-EruptingVolcano_cs` (= `audit-fix-EruptingVolcano_cs` as copied by audit-verify/one.sh) | `verify/suites/hall/audit-fix-EruptingVolcano_cs` | G3 | AuditHarness.csproj Lib.cs Runner.cs Tests/: EruptingVolcano.cs Examples.cs |
| `audit-verify/h-audit-fix-Expert1CardsGen_cs` (= `audit-fix-Expert1CardsGen_cs` as copied by audit-verify/one.sh) | `verify/suites/hall/audit-fix-Expert1CardsGen_cs` | G3 | AuditHarness.csproj Lib.cs Runner.cs Tests/: EX1_258.cs Examples.cs |
| `audit-verify/h-audit-fix-Generic_cs` (= `audit-fix-Generic_cs` as copied by audit-verify/one.sh) | `verify/suites/hall/audit-fix-Generic_cs` | G3 | AuditHarness.csproj Lib.cs Runner.cs Tests/: Examples.cs JoustFix.cs |
| `audit-verify/h-audit-fix-GvgCardsGen_cs` (= `audit-fix-GvgCardsGen_cs` as copied by audit-verify/one.sh) | `verify/suites/hall/audit-fix-GvgCardsGen_cs` | G3 | AuditHarness.csproj Lib.cs Runner.cs Tests/: Examples.cs GvgFix.cs |
| `audit-verify/h-audit-fix-HideousHusk_cs` (= `audit-fix-HideousHusk_cs` as copied by audit-verify/one.sh) | `verify/suites/hall/audit-fix-HideousHusk_cs` | G3 | AuditHarness.csproj Lib.cs Runner.cs Tests/: Examples.cs Leech.cs |
| `audit-verify/h-audit-fix-IcecrownCardsGen_cs` (= `audit-fix-IcecrownCardsGen_cs` as copied by audit-verify/one.sh) | `verify/suites/hall/audit-fix-IcecrownCardsGen_cs` | G3 | AuditHarness.csproj Lib.cs Runner.cs Tests/: Examples.cs IcecrownFix.cs |
| `audit-verify/h-audit-fix-LadyAzshara_cs` (= `audit-fix-LadyAzshara_cs` as copied by audit-verify/one.sh) | `verify/suites/hall/audit-fix-LadyAzshara_cs` | G3 | AuditHarness.csproj Lib.cs Runner.cs Tests/: Examples.cs LadyAzshara.cs |
| `audit-verify/h-audit-fix-LoeCardsGen_cs` (= `audit-fix-LoeCardsGen_cs` as copied by audit-verify/one.sh) | `verify/suites/hall/audit-fix-LoeCardsGen_cs` | G3 | AuditHarness.csproj Lib.cs Runner.cs Tests/: Examples.cs LoeFix.cs |
| `audit-verify/h-audit-fix-LootapaloozaCardsGen_cs` (= `audit-fix-LootapaloozaCardsGen_cs` as copied by audit-verify/one.sh) | `verify/suites/hall/audit-fix-LootapaloozaCardsGen_cs` | G3 | AuditHarness.csproj Lib.cs Runner.cs Tests/: Examples.cs LootFix.cs ZProbe.cs |
| `audit-verify/h-audit-fix-NaxxCardsGen_cs` (= `audit-fix-NaxxCardsGen_cs` as copied by audit-verify/one.sh) | `verify/suites/hall/audit-fix-NaxxCardsGen_cs` | G3 | AuditHarness.csproj Lib.cs Runner.cs Tests/: Examples.cs NaxxFix.cs |
| `audit-verify/h-audit-fix-TgtCardsGen_cs` (= `audit-fix-TgtCardsGen_cs` as copied by audit-verify/one.sh) | `verify/suites/hall/audit-fix-TgtCardsGen_cs` | G3 | AuditHarness.csproj Lib.cs Runner.cs Tests/: Examples.cs TgtFix.cs ZQuery.cs |
| `audit-verify/h-audit-fix-TrollCardsGen_cs` (= `audit-fix-TrollCardsGen_cs` as copied by audit-verify/one.sh) | `verify/suites/hall/audit-fix-TrollCardsGen_cs` | G3 | AuditHarness.csproj Lib.cs Runner.cs Tests/: Examples.cs FixTroll.cs |
| `audit-verify/h-audit-fix-UnderbellyNetwork_cs` (= `audit-fix-UnderbellyNetwork_cs` as copied by audit-verify/one.sh) | `verify/suites/hall/audit-fix-UnderbellyNetwork_cs` | G3 | AuditHarness.csproj Lib.cs Runner.cs Tests/: Chunk18.cs Examples.cs Jail877.cs |
| `audit-verify/h-audit-fix-UngoroCardsGen_cs` (= `audit-fix-UngoroCardsGen_cs` as copied by audit-verify/one.sh) | `verify/suites/hall/audit-fix-UngoroCardsGen_cs` | G3 | AuditHarness.csproj Lib.cs Runner.cs Tests/: Examples.cs UngoroFix.cs |
| `audit-verify/h-audit-fix-_Game_DeathProcessingAndAuraUpdate__` (= `audit-fix-_Game_DeathProcessingAndAuraUpdate__` as copied by audit-verify/one.sh) | `verify/suites/hall/audit-fix-_Game_DeathProcessingAndAuraUpdate__` | G3 | AuditHarness.csproj Lib.cs Runner.cs Tests/: Examples.cs SummonTiming.cs SummonTiming2.cs |
| `audit-verify/h-audit-fix-run_v1_json` (= `audit-fix-run_v1_json` as copied by audit-verify/one.sh) | `verify/suites/hall/audit-fix-run_v1_json` | G3 | AuditHarness.csproj Lib.cs Runner.cs Tests/: BossPowerFix.cs Examples.cs |

## 4. Fixtures (copied; stored LF, `verify/fixtures/<path it had under SP>`)

The originals were CRLF (recorded on Windows); the copies are LF with no BOM and are otherwise byte-identical. Where a
check compares a fixture with XML written at run time it reads the fixture through `VerifyPaths.ReadText` (pilot:
c-acts A10, c-enc-model F2), which gives back exactly the Windows bytes on Windows.

| Old place (under SP) | Here (under verify/fixtures) | Read by | Purpose |
|---|---|---|---|
| `content2-fixtures/` (17 folders: `dungeon-run.xml`, `golden.txt`) | `content2-fixtures/` | c-runlayer (was args[0]), c-acts (was `..\..\content2-fixtures`) | version-1 saves and their expected plans |
| `p4/golden-out/` (`golden.txt`, 12 `v3-*.xml`) | `p4/golden-out/` | c-acts (was `..\..\p4\golden-out`) | scripted all-win runs and version-3 saves |
| `p5/d12golden/d12-golden.txt` | `p5/d12golden/d12-golden.txt` | c-deckops (was `..\..\p5\d12golden\d12-golden.txt`) | D12 golden |
| `ledger-before.csv` (= `audit-verify/c-admission/ledger-before.csv`) | `ledger-before.csv` | c-admission (was `BaseDirectory\..\..\..\ledger-before.csv`) | the pre-change ledger |
| `p6f0/catalog-v1.before.json` | `p6f0/catalog-v1.before.json` | c-acts (was cwd `..\..\p6f0\...`) | pre-phase-6 catalog |
| `p6f9/run-v1.before.json` | `p6f9/run-v1.before.json` | c-acts (cwd-relative), c-heroes-F9 (absolute) | pre-F9 run data |
| `p6f10/before/catalog-v1.before.json` | `p6f10/before/catalog-v1.before.json` | c-acts (cwd-relative) | pre-F10 catalog |
| `p6f6/run-v1.before.json` | `p6f6/run-v1.before.json` | c-heroes-F6 (G1) | pre-F6 run data |
| `p6f15/run-v1.before-f14-with-f15.json` | `p6f15/run-v1.before-f14-with-f15.json` | c-heroes-F14 (G1) | pre-F14 run data |
| `p7/before/run-v1.json`, `catalog-v1.json`, `schedule-v1.json` | `p7/before/` | c-shrine-core (was absolute) | phase-6 data for the save migration check (the folder's `SabberStoneCore-p6f18.dll` is NOT copied) |
| `p8f01/golden-sequences.txt`, `golden-save-legacy-v5.xml`, `golden-save-acts-v5.xml` | `p8f01/` | c-enc-model (was absolute `SP\p8f01`) | released sequences, version-5 saves |
| `p8f01/fixtures/schedule-finale.json`, `schedule-relay.json`, `encounters-relay.json` | `p8f01/fixtures/` | `bp.sh finale`, `bp.sh relay` (SCHEDULE_JSON / ENCOUNTERS_JSON) | the wave-1 finale and relay fixture schedules (LF, no BOM: byte-identical copies) |
| `p8f01b/fixtures/encounters-secretfix.json` | `p8f01b/fixtures/` | `bp.sh secretfix` (ENCOUNTERS_JSON) | Manhunter Ivan with two Streetsmugglers (hand-kept, no generator: the wave-1 copy plus the four shipped acts-v3h finale rows and decks, copied verbatim from `encounters-v1.json` in step 9's test phase, because a run on the shipped acts-v3h resolves its finale's fixed bosses at NewRun since the close-out enabled it) |
| `p8f02/fixtures/encounters-collectA.json` | `p8f02/fixtures/` | `bp.sh collectA` (ENCOUNTERS_JSON) | kept with its generator |
| `p8f03/fixtures/encounters-f03.json` | `p8f03/fixtures/` | c-collectibles-B (G3), `bp.sh enc03 <slice>` | |
| `p8f04/fixtures/encounters-f04.json` | `p8f04/fixtures/` | c-collectibles-C (G3), `bp.sh f04-boss`, `bp.sh f04-player` | |
| `p8f05/fixtures/schedule-twists.json` | `p8f05/fixtures/` | c-rules-05 (G2; was args[0]) | |
| `p8f06/fixtures/schedule-plagues.json` | `p8f06/fixtures/` | c-rules-06 (G2; was args[0]) | |
| `p8f08/fixtures/encounters-f08.json` | `p8f08/fixtures/` | `bp.sh enc08-cards` (ENCOUNTERS_JSON) | |
| `p8f09/fixtures/encounters-f09.json` | `p8f09/fixtures/` | `bp.sh enc09-cards` (ENCOUNTERS_JSON) | |
| `p8f10/fixtures/encounters-f10.json` | `p8f10/fixtures/` | `bp.sh enc10-cards` (ENCOUNTERS_JSON; proved in the pilot) | |
| (new in wave 4) `p8f11/fixtures/encounters-f11.json` | `p8f11/fixtures/` | `bp.sh enc11-pete`, `bp.sh enc11-cards` (ENCOUNTERS_JSON) | the merged encounters-v1.json plus five fixture rows (fix11_*), written by `verify/tools/gen/p8f11/make_fixture.py` |
| (new in wave 5) `p8f14/fixtures/encounters-f14.json` | `p8f14/fixtures/` | `bp.sh enc14-cards` (ENCOUNTERS_JSON) | the merged encounters-v1.json plus two fixture rows (fix14_dazzik, fix14_akazamzarak), written by `verify/tools/gen/p8f14/make_fixture.py` |
| (new in wave 5) `p8f16/fixtures/encounters-f16.json` | `p8f16/fixtures/` | `bp.sh enc16-cards` (ENCOUNTERS_JSON) | the merged encounters-v1.json plus three fixture rows (fix16_tipsi, fix16_toomba, fix16_quel), written by `verify/tools/gen/p8f16/make_fixture.py` |
| (new in wave 5) `p8f17/fixtures/encounters-f17.json` | `p8f17/fixtures/` | `bp.sh enc17-cards`, `bp.sh enc17-check` (ENCOUNTERS_JSON) | the merged encounters-v1.json plus five fixture rows (fix17_zuramat, fix17_millificent, fix17_wagtoggle, fix17_goya, fix17_gallywix), written by `verify/tools/gen/p8f17/make_fixture.py` |
| (new in wave 5) `p8f18/fixtures/schedule-finale18.json` | `p8f18/fixtures/` | c-enc-18, `bp.sh enc18-finale` (SCHEDULE_JSON) | schedule-v1.json with the acts-v3h finale block enabled (fixedBosses from pending), written by `verify/tools/gen/p8f18/make_fixture.py` |
| (new in wave 6) `p8f23/fixtures/encounters-f23.json` | `p8f23/fixtures/` | `bp.sh enc23-cards` (ENCOUNTERS_JSON) | the merged encounters-v1.json plus four fixture rows (fix23_glinda, fix23_shivers, fix23_toki, fix23_godfrey), written by `verify/tools/gen/p8f23/make_fixture.py` |
| (new in wave 6) `p8f24/fixtures/schedule-tekahn.json` | `p8f24/fixtures/` | c-enc-24, `bp.sh tekahn24` (SCHEDULE_JSON) | schedule-v1.json with the acts-v3t finale enabled on Tekahn and its hero relay on, written by `verify/tools/gen/p8f24/make_fixture.py` |
| (new in step 9, lane M) `p9/targets.json` | `p9/` | `verify/tools/balance/check_targets.py`, `bp.sh outliers9` (B9.named) | the step-9 difficulty targets (plan section 1 B), hand-kept: each with its source and its derivation from R0; the seed bases S1 and S2 |
| `audit-inventory/cards.tsv` | `audit-inventory/cards.tsv` | hall audit-chunk-9 (G3; was absolute) | the audit inventory |

Repository files the suites read (already in git, not copied): `data/**`, `facts/violet-hold-2026-09.json`,
`plugin/**/*.cs` (compiled in, and read as text by c-runlayer and c-admission), `docs/design/restoration-ledger.csv`,
`docs/design/dungeon-run-catalog.md`, `tools/restoration-ledger/curation.json`,
`engine/SabberStoneCore/resources/Data/OfflinePoolCatalog.xml` (one hall harness).

## 5. Baselines (what the runners compare against; LF)

Since wave 6 (2026-11-16/17) `runs/`, `bp/` and `hall.txt` hold the wave-6 results recorded on Linux in the cloud container
(`runs/<name>.txt` = run 2 of each of the 70 entries, CR removed; `bp/bp-<mode>-a.txt` = every one of the 196 bosspilot
results, each equal to its `-b`; `hall.txt` unchanged, byte-identical to the wave-6 hall lines). The wave-5 versions (65
entries, 175 results) and the wave-4 versions (58 entries, 152 results) are in git history. The rows below say where the
wave-3 versions of those files came from; those versions are in git history. `bp-port/`, `bp-w1/` and `bp-w2/` are history
now: `bp.sh` reads one only for a mode `bp/` does not hold.

| Old place (under SP) | Here (under verify/baseline) | Compared by |
|---|---|---|
| `<suite>/run-p8w3-2.txt` for the 50 plain run entries | `runs/<name>.txt` | `same.sh` (0 differing lines expected) |
| `audit-verify/selfplay/selfplay-p8w3-2.log`, `runp8w3-2.txt` | `runs/selfplay.txt`, `runs/selfplay-games.txt` | `same.sh` (summary line; game file identical) |
| `audit-verify/randplay/p8w3-2.txt`, the lines before its stack trace | `runs/randplay.txt` | `same.sh` (cards, TB, fingerprint, regkeys, games/digest) |
| `p8w3/bp-*-a.txt` (58 files, each identical to its `-b`) | `bp/` | `bp.sh` ("vs wave-3 baseline") |
| `p8w2/bp-*-a.txt` for modes wave 3 did not rerun (50 files) | `bp-w2/` | `bp.sh` ("vs wave-2 reference"; not the current baseline) |
| `p7*/suites/c-bosspilot-*/shrine*-p8w1-1.txt` (7 files, timing column removed, CR removed; each equal to its `-p8w1-2`) | `bp-w1/bp-<mode>-a.txt` | `bp.sh shrine*` ("vs wave-1 record"; the shrine modes were not rerun in waves 2 and 3) |
| `p8w1/bp-fixture-a.txt`, `p8w1/bp-catalog-a.txt` (timing column and CR removed; each equal to its `-b`) | `bp-w1/bp-fixture-a.txt`, `bp-w1/bp-catalog-a.txt` | `bp.sh fixture`, `bp.sh catalog` ("vs wave-1 record"; the two released modes were not rerun in waves 2 and 3) |
| the port's own results on the wave-3 tree (export copy, 2026-11-13), `verify/.out/bp/bp-<mode>-a.txt` (each equal to its `-b`) for every mode wave 3 did not rerun: rules05 x18, cross05 x18 (`bp-cross-w2-<rule>`), enc01, secret4, rules01 x3, enc07a, enc07b, shrine07, seeds07-19000, enc08 x5, and the family 01-04 modes | `bp-port/` | `bp.sh` ("vs port record"; README.md sections 4 and 6) |
| `p8w1/bp-{plague-95,plague-110,plague-195,plague-199,plague-210,finale,relay,secretfix}-a.txt`, `p8f01b/bp-old01-a.txt`, `p8f02/bp-collect{A,P1,P2,P3,P4}-a.txt`, `p8f03/bp-enc03{a,b,c}-a.txt`, `p8f03/bp-icarax2-a.txt`, `p8f03/bp-enc01-a.txt` (as `bp-enc01-f03-a.txt`), `p8f04/bp-f04-{boss,player,icarax0,icarax110,icarax210}-a.txt` (timing and CR removed) | `bp-w1/` | history only (bp.sh prefers `bp-port`); README.md section 6 lists how far each moved |
| `p8w3/hall-1.txt`, the result lines | `hall.txt` | `hall.sh same` |

## 6. Generators (not run by the baseline runners; needed after a data change)

| Old place (under SP) | Port to | Group | Purpose |
|---|---|---|---|
| repo `tools/dungeon-run-catalog`, `tools/restoration-ledger` | (in the repository) run by `verify/bin/gen.sh` | done | encounters, catalog, ledger; proved byte-identical in the export copy |
| `p8f02/make_fixture.py`, `p8f02/make_bp_collect.py` | `verify/tools/gen/p8f02/` | G3 (ported; output byte-identical) | family 02 fixture; c-bosspilot-collect harness |
| `p8f03/make_fixture.py` | `verify/tools/gen/p8f03/` | G3 (ported; output byte-identical) | `encounters-f03.json` |
| `p8f04/make_fixture.py` | `verify/tools/gen/p8f04/` | G3 (ported; output byte-identical) | `encounters-f04.json` |
| `p8f05/make_fixture.py`, `p8f05/make_bp_rules.py` | `verify/tools/gen/p8f05/` | done (G2) | `schedule-twists.json`; c-bosspilot-rules05 harness; `python -I verify/tools/gen/p8f05/<script> [<output>]`, no arguments needed; both outputs proved byte-identical to `verify/fixtures` and `verify/suites` |
| `p8f06/make_fixture.py`, `p8f06/make_bp_rules06.py` | `verify/tools/gen/p8f06/` | done (G2) | `schedule-plagues.json`; c-bosspilot-rules06 harness; as for p8f05; both outputs proved byte-identical |
| `p8f08/make_fixture.py` | `verify/tools/gen/p8f08/` | G3 (ported; output byte-identical) | `encounters-f08.json` |
| `p8f09/make_fixture.py`, `p8f09/make_harness.py` | `verify/tools/gen/p8f09/` | G3 (ported; output byte-identical) | `encounters-f09.json`; c-bosspilot-enc09 harness |
| `p8f10/make_fixture.py` | `verify/tools/gen/p8f10/` | G3 (ported; output byte-identical) | `encounters-f10.json` |
| (new in wave 4) `make_fixture.py`, `make_f11.py`, `patch_curation.py` | `verify/tools/gen/p8f11/` | wave 4 | `encounters-f11.json`; `data/dungeon-run/reference/encounters/F11.json` (the wiki transcription); the E11 entry of curation.json (idempotent; not rerun by the integration) |
| (new in wave 4) `make_f12.py` | `verify/tools/gen/p8f12/` | wave 4 | `data/dungeon-run/reference/encounters/F12.json` (the wiki transcription; rerun after the review's Blood Red Apple change) |
| (new in wave 5) `make_f14.py`, `make_fixture.py` | `verify/tools/gen/p8f14/` | wave 5 | `data/dungeon-run/reference/encounters/F14.json` (the wiki transcription; reads gitignored pages); `encounters-f14.json` |
| (new in wave 5) `make_f15.py` | `verify/tools/gen/p8f15/` | wave 5 | `data/dungeon-run/reference/encounters/F15.json` (the wiki transcription) |
| (new in wave 5) `make_f16.py`, `make_fixture.py` | `verify/tools/gen/p8f16/` | wave 5 | `F16.json` (the wiki transcription; rerun by the integration for the review's Gold Elemental note, byte-identical to the committed file); `encounters-f16.json` |
| (new in wave 5) `make_f17.py`, `make_fixture.py` | `verify/tools/gen/p8f17/` | wave 5 | `F17.json` (the wiki transcription); `encounters-f17.json` |
| (new in wave 5) `make_f18.py`, `make_fixture.py` | `verify/tools/gen/p8f18/` | wave 5 | `F18.json` (the wiki transcription); `schedule-finale18.json` (the acts-v3h finale enabled) |
| (new in wave 5) `make_f19.py`, `tombs_tiers.py`, `patch_curation.py` | `verify/tools/gen/p8f19/` | wave 5 | `F19.json` (the wiki transcription); the Tombs tier rule shared with families 20-22 (byte-identical on every family branch); the E19 entry of curation.json (idempotent; not rerun by the integration) |
| (new in wave 6) `make_f20.py`, `patch_curation.py` | `verify/tools/gen/p8f20/` | wave 6 | `F20.json` (the wiki transcription; uses `p8f19/tombs_tiers.py`); the E20 entry of curation.json (idempotent; not rerun by the integration) |
| (new in wave 6) `make_f21.py`, `patch_curation.py` | `verify/tools/gen/p8f21/` | wave 6 | `F21.json` (the wiki transcription; uses `p8f19/tombs_tiers.py`); the E21 entry of curation.json (idempotent; not rerun) |
| (new in wave 6) `make_f22.py`, `patch_curation.py` | `verify/tools/gen/p8f22/` | wave 6 | `F22.json` (the wiki transcription; uses `p8f19/tombs_tiers.py`); the E22 entry of curation.json (idempotent; not rerun) |
| (new in wave 6) `make_f23.py`, `make_fixture.py` | `verify/tools/gen/p8f23/` | wave 6 | `F23.json` (the wiki transcription); `encounters-f23.json` |
| (new in wave 6) `make_f24.py`, `make_fixture.py` | `verify/tools/gen/p8f24/` | wave 6 | `F24.json` (the wiki transcription); `schedule-tekahn.json` (the acts-v3t finale enabled on Tekahn) |
| (new in step 9, lane R tests) `make_twists9.py` | `verify/tools/gen/p9/` | step 9 | `verify/fixtures/p9/schedule-twists9.json` (c-rules-9 T: all eight act twists in every act of acts-v4h at chances 70 / 85 / 55 and of acts-v3h at 100, DALA_829 weight 4) |
| (new in step 9, lane R tests) `pairs.py` | `verify/tools/balance/` | step 9 | `verify/fixtures/p9/pairs9.txt` and `pairs9-declared.txt`: the C6 covering array for `bp.sh pairs9 <chunk> <k>` (c-bosspilot-pairs9) and its declared pairs |
| (new in step 9, lane S tests) `record_replay_v3.sh`, `Record9.cs` | `verify/tools/gen/p9/` | step 9 | `verify/fixtures/p9/replay-v3/` (c-enc-25 L4): eight acts-v3h/v3t saves at the L4 stops, `replay.txt` and `exclusions.txt`, recorded on an f62275b export with the frozen `c-enc-25/ReplayV3.cs` (`record_replay_v3.sh <scratch folder>`, under the harness lock; twice byte-identical) |
| (new in step 9, lane M) `agg.py`, `check_targets.py` | `verify/tools/balance/` | step 9 | not generators: `agg.py <logs>` aggregates the run harness's G/R lines into JSON, `check_targets.py <agg.json> [--strict]` checks it against `verify/fixtures/p9/targets.json` |

The G3 generators take no arguments: each reads the repository it sits in and writes its default output under verify/
(`verify/fixtures/<family>/fixtures/...`, or the suite folder for the two harness generators); an optional argument
(fixtures: `<repo or encounters file> <out file>`; harnesses: `<output folder>`) writes elsewhere. Run them with
`python -I verify/tools/gen/<family>/<script>.py` after `gen.sh` has regenerated `encounters-v1.json`, or after the base
harness `suites/p8f01/c-bosspilot-encounter` changes. On the export copy (2026-11-13) all six fixtures and both harnesses
came out byte-identical to the files here. Wave 4 (Linux, 2026-11-14): every generator here except `patch_curation.py`
was run twice after the last data change, each output byte-identical between the runs; the seven encounter fixtures
(p8f02, p8f03, p8f04, p8f08, p8f09, p8f10, p8f11) were rebuilt from the merged data, the rest came out unchanged.
Wave 5 (Linux, 2026-11-16): the 17 data-derived generators (every `make_fixture.py`, `make_bp_collect.py`,
`make_bp_rules.py`, `make_bp_rules06.py`, `make_harness.py`) ran twice after the merges and twice again after the last
data change, byte-identical between the runs; the encounter fixtures of p8f02-p8f04, p8f08-p8f11, p8f14, p8f16 and p8f17
and the schedule fixtures of p8f05/p8f06 (family 18's DALA_830 row) were rebuilt; p8f18's finale schedule and the four
generated harnesses came out unchanged. The wiki `make_fNN.py` generators read gitignored pages and were not rerun,
except `make_f16.py` (from the family 16 lane's pages) to prove the corrected F16.json.
Wave 6 (Linux, 2026-11-16): the 19 data-derived generators (the wave-5 set plus `p8f23/make_fixture.py` and
`p8f24/make_fixture.py`) ran twice after the merges and twice again after the merge review's run-v1.json change,
byte-identical between the runs; the encounter fixtures of p8f02-p8f04, p8f08-p8f11, p8f14, p8f16, p8f17 and p8f23 and
p8f24's schedule-tekahn.json (wave 5's DALA_830 row and the finale twist pool) were rebuilt; p8f05, p8f06, p8f18 and the
four generated harnesses came out unchanged. `make_f11.py` and `make_f12.py` (Witchwood's Touch kept) were run on the
wave-4 lanes' pages: before the edit each reproduced the committed F11/F12.json byte for byte. The regression's
form-threshold change in `p8f01/c-bosspilot-encounter` (b074311) was carried into the four generated harnesses by
rerunning `make_bp_collect.py`, `make_bp_rules.py`, `make_bp_rules06.py` and `make_harness.py` (each harness moved by
the same five-line hunk); `p8f04/c-bosspilot-f04`, a hand copy, got the same edit.

## 7. Not moved, and why

- Every run, build and bosspilot log except the baselines above (pure logs), and the `Program.cs.pre-*` / `.p3fix` /
  `.p4i1` backups in the suite folders.
- Binaries of any kind: `SP/audit-verify/engine-out`, `SP/audit-harness/engine-out` (an older engine build, see
  README.md section 6), `SP/p7/before/SabberStoneCore-p6f18.dll`, `SP/p8w3/engine-before*.dll`, every `obj/`, `out/`.
- Lane evidence that wave 3 superseded: `SP/p8f06`, `SP/p8f09`, `SP/p8f10` `bp-*-a.txt` (the lanes' runs; the wave-3
  main-tree results in `baseline/bp` replace them), `SP/p8w2/run-*` logs.
- Suite generators whose output is the committed suite: `p8w1/make_w1fix.py`, `p8w3/make_w3fix.py`, `p8f07/make_reference.py`,
  `p8f08/make_f08.py`, `p8f09/make_f09.py`, `p8f10/make_f10.py`, `p8f01/make_scripts.py`.
- Debug leftovers: `SP/p8w3/dbg719`, `SP/p8w3/prefix`.
