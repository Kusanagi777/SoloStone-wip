# verify/: the Dungeon Run regression suites

The headless suites, fixtures, recorded baselines and runners that verify the unified Dungeon Run work (phases 2-8).
They used to live in a session scratchpad on one Windows PC, with absolute paths; here they build and run from any
clone, on Linux or Windows, with only the .NET SDK pinned in `global.json` (10.0.402) and bash.

The current baseline is the wave-3 integration (`handoff/phase8/reports/integrate-wave3.md`, section 7). Every number a
runner prints must equal it (section 4 below). `MANIFEST.md` maps every file here to where it came from.

## 1. Quick start

```bash
bash verify/bin/build-all.sh            # engine + catalog tool + every suite + randplay + the hall (no plugin, no client files)
bash verify/bin/runs.sh 1 all           # every suite once (about 20 minutes); then:
bash verify/bin/runs.sh 2 all
bash verify/bin/same.sh                 # run 1 vs run 2, run 1 vs the wave-3 baseline: expect identical / 0 differing lines
bash verify/bin/bp.sh enc09             # one bosspilot mode, twice, compared with the baseline
bash verify/bin/hall.sh harness && bash verify/bin/hall.sh 1 38 && bash verify/bin/hall.sh same
bash verify/bin/final.sh engine builds runs1 enc rules06 cross06 released runs2 same hall   # the wave-3 set, about 3 hours
bash verify/bin/final.sh released-all w1 w2 rules05 cross05   # the bosspilot modes wave 3 did not rerun, about 2-3 hours more
```

The runners work from any current directory. Requirements: .NET SDK 10.0.402, bash, coreutils (`timeout`, `cmp`,
`diff`, `paste`), git; python 3 only for `tools/port_paths.py` and the fixture generators. Nothing here needs the
Hearthstone client libraries: the plugin (`plugin/OfflineProbe.csproj`) is not built; the suites compile the plugin
sources they test directly.

On a Windows clone set `git config core.autocrlf false` before checkout (or clone with `-c core.autocrlf=false`): several
suites check that repository data files are LF only. `verify/.gitattributes` keeps everything under verify/ LF anyway.

## 2. Layout

```
verify/
  README.md  MANIFEST.md  .gitignore  .gitattributes
  Directory.Build.props        imported by every suite .csproj: $(SoloStoneRoot) $(VerifyRoot) $(EngineDll) $(EngineDir)
  lib/env.sh                   sourced by every runner: VERIFY ROOT SUITES FIX BASE OUT ENGINE_DLL UP
  lib/suites.sh                the suite registry: BUILD_LIST (67), RUN_LIST (54), HALL_BASE + HALL_LIST (38), PILOT_LIST
  lib/VerifyPaths.cs           compiled into every suite: VerifyPaths.Root/Repo/Data/Fixture/Out/ReadText/WindowsText
  bin/engine.sh                the one engine build -> verify/.out/engine-out/SabberStoneCore.dll
  bin/build-all.sh builds.sh runs.sh same.sh bp.sh cross05.sh cross06.sh hall.sh final.sh gen.sh
  suites/<origin>/<name>/      one folder per suite: Program.cs (+ other .cs), the .csproj; <origin> = the scratchpad
                               folder it came from (audit-verify, p6f15, p7, p8f01, ...); the hall is suites/hall/<name>
  fixtures/<old path>/         inputs the suites read, at the path they had under the old scratchpad
  baseline/runs/<name>.txt     the wave-3 log of each run entry (selfplay, selfplay-games, randplay)
  baseline/bp/bp-<mode>-a.txt  the wave-3 bosspilot results
  baseline/bp-port/            the bosspilot modes wave 3 did not rerun, recorded by the port on the wave-3 tree (section 4)
  baseline/bp-w1/ bp-w2/       older records: the shrine, fixture and catalog modes (still compared: equal on the wave-3
                               tree) and, as history only, the wave-1 and wave-2 records of the modes in bp-port (section 6)
  baseline/hall.txt            the wave-3 hall result lines
  tools/port_paths.py          the mechanical part of porting a suite
  .out/                        gitignored: engine-out/, tools/, builds/, runs/, bp/, hall/, tmp/, final-*.log
```

Every suite sits exactly three levels below verify/ (`suites/<origin>/<name>`), so `$UP` (`../../..`) is verify/ seen
from a suite folder; runners use it for paths they hand to a harness.

## 3. Conventions (every port follows them)

1. **Runners** are bash, in `verify/bin`, and start with `. "$(dirname "$0")/../lib/env.sh"`: they find the repository
   from their own location. No absolute path, no drive letter, no `cygpath`, forward slashes only.
2. **Harnesses run as `dotnet out/<AssemblyName>.dll`** from their own suite folder (never `.exe`): `AiHarness.dll`,
   `SecretScan.dll` (c-secretscan), `EngineProof.dll` (randplay). Built with
   `dotnet build --no-incremental -c Release -nologo -v q -o out` (the hall without `--no-incremental`).
3. **.csproj paths** come from `verify/Directory.Build.props` (which uses `$(MSBuildThisFileDirectory)`):
   `$(SoloStoneRoot)plugin/...`, `$(SoloStoneRoot)data/...`, `<HintPath>$(EngineDll)</HintPath>` (hall:
   `$(EngineDir)/SabberStoneCore.dll`). Forward slashes. No other property is needed on the command line.
4. **Program.cs finds files through `VerifyPaths`** (lib/VerifyPaths.cs): `VerifyPaths.Root` walks up from
   `AppContext.BaseDirectory` (then the current directory) to the folder holding both `engine/SabberStoneCore` and
   `verify/`; the environment variable `SOLOSTONE_ROOT` overrides it.
   - `@"C:\SoloStone\data"` -> `VerifyPaths.Data`; `@"C:\SoloStone"` -> `VerifyPaths.Root`;
     `@"C:\SoloStone\a\b"` -> `VerifyPaths.Repo("a/b")`
   - a scratchpad path `@"...\scratchpad\X\Y"` (absolute, cwd-relative `..\..\X\Y`, or `BaseDirectory\..\...`) ->
     `VerifyPaths.Fixture("X/Y")`, the file copied to `verify/fixtures/X/Y`
   - a relative path literal with backslashes (`R(@"dungeon-run\run-v1.json")`) -> forward slashes
     (`R("dungeon-run/run-v1.json")`); Windows accepts `/`, Linux does not accept `\`
   - scratch output -> `VerifyPaths.Out("tmp/...")`; a tool -> `VerifyPaths.Out("tools/...")`, started through
     `Environment.ProcessPath` (the dotnet host) when it is a `.dll`
   - `args[0]` defaults move into the Program.cs (`VerifyPaths.Fixture(...)`), so a runner passes no paths
5. **Line ends.** Every file here is LF without BOM. The fixtures were recorded on Windows with CRLF and are stored LF;
   where a check compares a fixture with XML written at run time (XmlSerializer writes `Environment.NewLine`), read
   the fixture with `VerifyPaths.ReadText(path)`, which returns the platform's line ends (CRLF on Windows = the old
   bytes). `ReadAllLines`, JSON and XML parsing do not care. Compare logs with CR removed (the runners do).
   Where a check compares a hash with one recorded on Windows over such text (c-acts: the golden run hashes and the A6
   digest over XmlSerializer output; c-deckops: the D12 PowerHistory digests, whose `Print` uses `AppendLine`, and the
   L1 note), the suite hashes `VerifyPaths.WindowsText(text)`, which turns every line end into CRLF: the recorded bytes
   on every platform, unchanged on Windows (c-acts 181 and c-deckops 174 with 0 differing lines after the change). The
   recorded hashes were checked: each golden.txt run hash equals SHA-256 of the CRLF form of its v3-*-end.xml, never the
   LF form, so without this both A4 checks and D12 would fail on Linux.
6. **Logs** go to `verify/.out/` (gitignored), never next to the sources.
7. **File names are case-sensitive on Linux**: match the case of every file and folder exactly.
8. **Never change what a test asserts**, only how it finds files. If a number moves after porting, find out why and
   fix the port. `tools/port_paths.py` does the mechanical rewrites and lists every line left to look at.

## 4. The baseline (wave 3; every number must match)

Suites (`same.sh`: run 1 = run 2, 0 differing lines vs `baseline/runs`, except c-secretsearch, whose detail lines
differ between any two runs; its SUMMARY line is what must match):

| Entry | Result | Entry | Result |
|---|---|---|---|
| c-runlayer | 244 passed, 0 failed | c-catalog | 127 passed, 0 failed |
| c-content | 904 passed, 0 failed | c-admission | 3167 passed, 0 failed |
| c-acts | 181 passed, 0 failed | c-deferrals | 149 passed, 0 failed |
| c-deckops | 174 passed, 0 failed | c-dkdh-supply | 13 passed |
| c-dkdh-play | 5 passed | c-heroes | 347 passed |
| c-heroes-F14 | 316 | c-heroes-F6 | 566 |
| c-heroes-F9 | 292 | c-runctx | 40 |
| c-starters-F10 | 239 | c-upgrades | 34 |
| c-treasures F1 / F2 / F3 / F4 / F5 | 229 / 321 / 283 / 236 / 404 | c-treasures F7 / F8 / F11 / F12 / F13 | 367 / 256 / 380 / 321 / 248 |
| c-review-F18 | 35 | c-shrine-core | SUMMARY checks passed 131 failed 0 |
| c-shrines S1 / S2 / S3 | 107 / 157 / 158 | c-shrine-upgrades | 92 |
| c-teammates T1 / T2 | 139 / 207 | c-companions-D | 251 |
| c-p7fix | RESULT 40 passed | c-enc-model | RESULT 141 passed, 0 failed |
| c-enc-engine | RESULT 122 passed, 0 failed | c-enc-secrets | 60 |
| c-collectibles A / B / C | 115 / 108 / 102 | c-w1fix | 23 |
| c-rules-05 | 170 | c-enc-07 | 161 |
| c-enc-08 | 113 | c-rules-06 | 224 |
| c-enc-09 | 166 | c-enc-10 | 116 |
| c-w3fix | 4 | c-secretscan | faults 0 (both generations) |
| c-secretsearch | SUMMARY checks passed 689 failed 0 | selfplay | decks 57 games 200 p1 103 p2 97 other 0 faults 0 digest 2CE546A18F96DCBC |
| randplay | games=200 digest=40207800B289E504 (regkeys 2322) | | |

Hall (`hall.sh same`): every harness's SUMMARY line equal to `baseline/hall.txt`, all with 0 bad and 0 failed; 955
tests / 8285 checks in all = the base harness (8 tests, 114 checks) + the 38 audit harnesses (947 tests, 8171 checks).

Bosspilot (`bp.sh`; each mode twice identical, 0 faults / 0 fallbacks / 0 failed checks / 0 swallowed leaf exceptions,
0 differing lines vs `baseline/bp`; stderr empty for every encounter, rules06 and cross06 mode; the released harnesses
(run, act, fixture, catalog and the shrine modes) print one diagnostic line on stderr, "pending-choice leaves scored as
they stand N", 46 or 47 bytes per run, byte-identical to the wave-3 and wave-1 stderr files):

| Mode | Games | Boss wins | Mode | Games | Boss wins |
|---|---|---|---|---|---|
| enc09 | 22 | 14 | enc10 | 30 | 25 |
| enc09-seeds | 22 | 17 | enc10a (`<k>` 1 and 2, then `same`) | 32 | 20 |
| enc09-auto | 16 | 10 | enc10b (same) | 28 | 21 |
| enc09-finals | 20 | 20 | enc10-nab | 10 | 9 |
| enc09-rooms | 16 | 7 | enc10-cards (same) | 36 | 19 |
| enc09-cards | 24 | 21 | enc10-shrine (same) | 37 | 32 |
| run (released) | 40 | 27 | enc10-seeds | 30 | 24 |
| act (released) | 40 | 26 | enc10-hagatha | 6 | 6 |
| rules06 `<rule>`, 21 rules | 16 each (12 for ULDA_706) | | cross06 (`cross06.sh`), 21 rules x 12 bosses | 12 each (11 for ULDA_719, ULDA_804) | |
| shrine (released) | 40 | 24 | shrineT1 | 40 | 13 |
| shrineS2 | 40 | 23 | shrineT2 | 40 | 20 |
| shrineS3 | 40 | 22 | shrineD | 40 | 14 |
| shrineU | 40 | 25 | fixture (released) | 40 | 27 |
| catalog (released) | 40 | 27 | | | |

The seven shrine modes and the released fixture and catalog modes were last run in wave 1 (`p8w1`); `bp.sh` compares
them with those records in `baseline/bp-w1` (0 differing lines on the wave-3 tree). Their stderr is not empty: each run prints one diagnostic line, "pending-choice
leaves scored as they stand 1" (46 or 47 bytes per run, the same as in wave 1). `final.sh released` runs only run and
act, as the wave-3 final.sh did; `final.sh released-all` runs fixture, catalog and the seven shrine modes.

**The modes wave 3 did not rerun** (families 01-05, 07, 08: 74 results). The port ran each one twice on the wave-3 tree
(export copy, 2026-11-13, through the lock): every one twice identical, 0 fallbacks, 0 failed checks, 0 swallowed leaf
exceptions, and every one 0 faults except rules01-DALA_850 (below). Those results are the records in `baseline/bp-port`;
`bp.sh` compares with them ("vs port record", expected 0 differing lines). Eleven of them were also run with the
ORIGINAL wave-3 builds of the four harnesses involved (c-bosspilot-encounter, -rules05, -collect, -f04, built
2026-11-13 11:54-11:56 against the same engine md5, reading the wave-3 tree): rules01-DALA_850, enc01, enc08-cards,
seeds07-19000, cross-w2-DALA_829, old01, finale, collectA, collectP2, enc03a and f04-player, each byte-identical to the
port. How far each moved from its older record is in section 6. Stderr is empty for all but rules01-DALA_850.

| Mode (`bp.sh ...`, final.sh stage) | Games / boss wins | Mode | Games / boss wins |
|---|---|---|---|
| enc01 (w2) | 20 / 18 | old01 (w1) | 12 / 12 |
| secret4 (w2) | 24 / 18 | secretfix (w1) | 6 / 5 |
| rules01 DALA_850 (w2) | 10 / 7, **faults 1** | plague: 95 / 110 / 195 / 199 / 210 (w1) | 2 / 2 each |
| rules01 DALA_864 / DALA_866 (w2) | 10 / 8, 10 / 10 | finale (w1) | 8 / 8 |
| enc07a / enc07b (w2) | 36 / 31, 36 / 32 | relay (w1) | 2 / 1 |
| shrine07 (w2) | 37 / 32 | collectA (w1; all 33 cards played by the boss) | 32 / 15 |
| seeds07 19000 (w2) | 36 / 31 | collectP 1 / 2 / 3 / 4 (w1) | 6 / 5, 6 / 6, 6 / 6, 3 / 3 |
| enc08 (w2) | 34 / 26 | enc03 a / b / c (w1; `enc03-check`: every family card played) | 24 / 24, 24 / 22, 16 / 16 |
| enc08-tag (w2) | 8 / 8 | enc01-f03 (w1) | 20 / 18 |
| enc08-cards (w2; the four cards played from hand) | 12 / 12 | icarax2 (w1) | 4 / 4 |
| enc08-trapped (w2) | 6 / 5 | f04-boss (w1; all 23 cards played by the boss) | 40 / 40 |
| enc08-seeds (w2) | 34 / 25 | f04-player (w1, REQUIRE=1) | 24 / 16 |
| rules05 `<rule>`, 18 rules (rules05) | 16 each | f04-icarax 0 / 110 / 210 (w1) | 4 / 4 each |
| cross05 (`cross05.sh`), 18 rules x 14 bosses (13 for DALA_852, DALA_853) | 14 each (13) | | |

**rules01-DALA_850 fails on the wave-3 tree** (exit 1 both runs, the same with the original wave-3 binary): with
Anomaly - Arcane ("All spells cost (2) less") Hagatha's game against the DALA_Squeamlish run ends in "FAULT Exception:
stuck after 1500 actions": the boss plays Unstable Evolution (Echo) at 0 cost on Arcane Giant again and again in one
turn. Each run also takes about 5 minutes instead of 30 s and prints "fair-hand-knowledge-limit facts=8197 packets=0;
opponent hand reconstruction skipped" on stderr (86 bytes). In wave 2 the same game ended normally (boss won, 48
turns). This is a game defect for the next session to fix (the engine or the encounter rules), not a port defect: the
record keeps the faulting output so that drift stays visible, and the fix will change it (re-record then).

Generators (`gen.sh`): all six generated files byte-identical; encounters "69 rows, 73 decks (43 authentic, 30 changed),
147 adaptations, 1 rejected"; catalog "506 entries (452 offerable pool, 2 withdrawn, 52 fixed), 6508 admitted and 1
excluded"; ledger rows 4704, implemented 2813, partial 295, missing 538, duplicate/support 1058, adapted 0.
Engine build: 0 warnings, 0 errors (md5 07028f461d526950718e3df4c8401d8d on Windows, identical to the wave-3 build).

## 5. Running on a shared machine

The AI searches have wall-clock budgets: a harness that shares the CPU with another harness can stop a search on the
clock and play differently (bosspilot lines then differ from the baseline). Run one harness at a time, on a quiet
machine. On the original PC every harness call went through the cross-lane lock, which the runners do not call
themselves; wrap the runner instead and keep each locked call under about 6 minutes (exit 75 = busy, nothing ran):

```bash
bash /c/Users/user/AppData/Local/Temp/claude/0a564ffc8743aeec/d2a4e245-ca37-4433-9b55-c35ed325f081/scratchpad/p8lanes/hrun.sh bash verify/bin/runs.sh 1 1 7
```

In a cloud container there is no lock: run the stages one after another, never two harness runs at once. Builds and
`gen.sh` run no harness and need no lock, but never run two engine builds (or an engine and a plugin build) at once:
they share `engine/SabberStoneCore/obj`.

The budgets also make a bosspilot result depend on the host's speed, not only on sharing: a slower (or faster) machine
can stop a search on the clock where the recording machine stopped on nodes, and play differently. On Windows the
bosspilot wall times varied 1.5-4x between runs with identical results, so node budgets usually bind first, but that is
not guaranteed on a small container. A bosspilot line that differs on a new host must first be checked for searches
stopped by the clock (`SearchStop.Clock` in `plugin/BotSearch.cs`, the turn wall budget in `plugin/Npc/NpcTurnRunner.cs`;
the harness output does not show stop reasons, so count them in a scratch copy of the harness, never in the committed
one) before it is treated as a regression. Never change budgets or baselines to make a line match.

## 6. Known differences from the old runners

- randplay: the wave-3 runner passed no output-file argument, so EngineProof threw after printing its digest (exit 127).
  `runs.sh` passes one, so it exits 0; the printed lines (cards, fingerprint, regkeys, games, digest) are unchanged and
  are what `same.sh` compares (`baseline/runs/randplay.txt` holds those five lines).
- c-catalog runs the catalog tool built by `builds.sh tool` (`verify/.out/tools/dungeon-run-catalog`, against the
  engine build) instead of `tools/dungeon-run-catalog/bin/Release/net10.0/DungeonRunCatalogTool.exe`; its scratch
  output goes to `verify/.out/tmp/c-catalog-tool-out` instead of the suite folder.
- The base hall harness (`SP/audit-harness`) was built in wave 3 against its own older engine copy
  (`audit-harness/engine-out`, 2026-11-10, md5 f1c3f142...); the port builds it against the current engine. Its
  expected result is still 8 tests, 114 checks; if it moves, that is the engine difference, to be explained, not ported away.
- `same.sh` compares with the recorded wave-3 logs; the old one compared with the wave-2 logs (`run-p8w2-2.txt`).
- `bp.sh` compares every mode with the wave-3 main-tree result; the old one compared some modes with the lanes'
  evidence (p8f06/p8f09/p8f10), where differing lines were expected.
- `bp.sh rules05 <rule>`: every one of the 18 rules differs from its wave-2 reference (`baseline/bp-w2`) in the
  dala_21_whirt and ulda_44_trap_room games and the summary line (4 differing lines for DALA_829, DALA_852, DALA_859; 6
  for the other 15; 16 games, 0 faults, 0 fallbacks, 0 failed checks, stderr empty, run 1 = run 2). This is wave 3, not
  the port: the ORIGINAL wave-3 build of the harness (`SP/p8f05/suites/c-bosspilot-rules05/out`, built 2026-11-13
  against the same engine md5) run on the same tree gives output identical to the port for DALA_852 and DALA_829. Wave 3
  did not rerun family 05 bosspilots; the port's results are now the records in `baseline/bp-port` (0 differing lines).
- The other modes wave 3 did not rerun (section 4) moved from their older records as follows (differing lines of the
  `-a` result against `baseline/bp-w2` or `bp-w1`; every one confirmed as the wave-3 tree's behaviour, not the port's,
  by the eleven original-binary runs listed in section 4). Seeds of c-bosspilot-encounter depend on the boss's row
  index and run history, and waves 2 and 3 changed shared rules (opening hands, hero-power targeting, start weapons,
  Effect.RemoveAuraFrom) and rebuilt the family fixtures from regenerated data, so these moves are expected:
  - vs wave 2: enc01 12, secret4 6, rules01 DALA_850 8 (the new fault), DALA_864 6, DALA_866 6, enc07a 0, enc07b 14,
    shrine07 4, seeds07-19000 14, enc08 14, enc08-tag 6, enc08-cards 26, enc08-trapped 0, enc08-seeds 6; cross05: 8 for
    every rule but DALA_859 (6): the same loot_28_hamm, loot_15_george_karl and loot_24_mothergloop games and the summary
    line (for DALA_859 the summary line is unchanged).
  - vs wave 1: old01 24, secretfix 0, plague 95 / 110 / 195 / 199 / 210: 4 / 8 / 8 / 4 / 4, finale 4, relay 0,
    collectA 66 (every line: its fixture was rebuilt in wave 3), collectP1-4 2 / 6 / 4 / 0, enc03a/b/c 50 / 50 / 34
    (every line; fixture rebuilt), enc01-f03 18, icarax2 10, f04-boss 80 (every line; fixture rebuilt), f04-player 50,
    f04-icarax 0 / 110 / 210: 8 each.
  - The checks those families ran still pass: all 33 family 02 cards played by the boss (collectA), every family 03 card
    played by a boss (enc03-check), every one of the 23 family 04 boss cards played (f04-boss), the four enc08 cards
    played from hand.
- Not yet run on Linux (no Linux on the original PC): the layout, paths and line ends are written for it, but the first
  Linux run is the proof. Things to watch there: case of file names, `VerifyPaths.ReadText` where a fixture meets
  XmlSerializer output, any check that reads a repository file and expects CRLF or `\` in a path.

## 7. Porting status and the plan for the rest

**Full verification, 2026-11-13 (Windows, export copy only, every harness call through the lock): every suite is
ported and the whole section 4 baseline is reproduced.** Engine 0 warnings / 0 errors (md5 07028f46...); the catalog
tool, randplay and all 67 suite projects build with 0 errors; the 54 run entries twice, `same.sh`: every entry 1 = 2 and
0 differing lines vs `baseline/runs` (c-secretsearch: only its timing lines differ, SUMMARY 689 / 0); every bosspilot
mode of section 4 (the 14 encounter modes, rules06 x21, cross06 x21, run, act, fixture, catalog, the seven shrine modes)
twice identical, 0 differing lines vs its record; hall 39 of 39 equal to `baseline/hall.txt` (955 tests / 8285 checks;
the base harness gives 8 / 114 on the current engine). Not rerun in that pass: rules05 (section 6) and `gen.sh`.

**Review fixes, 2026-11-13** (Windows, export copy, through the lock): c-acts and c-deckops hash Windows line ends on
every platform (section 3, rule 5): 181 and 174 passed, run 1 = run 2, 0 differing lines vs `baseline/runs`. The
modes wave 3 did not rerun were ported or restored (family 01: old01, secretfix, plague, finale, relay with their four
fixtures; families 02-04: collectA, collectP, enc03, enc01-f03, icarax2, f04-boss, f04-player, f04-icarax), the
family 05 cross run got its driver (`cross05.sh`), and all 74 results were run twice and recorded in `baseline/bp-port`
(section 4); `final.sh` got the stages released-all, w1, w2, rules05 and cross05. These stages were exercised through
the same `bp.sh` / `cross05.sh` calls they make, not through `final.sh` itself.

Pilot (ported, proved on Windows from the export copy on 2026-11-13, through the lock, twice): c-runlayer 244, c-catalog
127, c-content 904, c-admission 3167, c-acts 181, c-deckops 174, c-shrine-core 131, c-enc-model 141, c-enc-engine 122,
selfplay digest 2CE546A18F96DCBC (game file identical), randplay digest 40207800B289E504; every log 1 = 2 and 0 differing
lines vs the wave-3 logs. c-bosspilot-encounter: enc09 22/14, enc10-nab 10/9, enc10-hagatha 6/6, enc10-cards 36/19
(fixture through ENCOUNTERS_JSON), each twice identical, stderr empty, 0 differing lines vs `baseline/bp`.
`gen.sh`: all six generated files byte-identical. Engine: md5 identical to the wave-3 build.

The rest is split in three groups (the suite lists are in `MANIFEST.md` section 2, column Group):

- **G1** (24 projects): the rest of audit-verify (c-secretscan, c-secretsearch, c-bosspilot, c-deferrals), the 18
  p6f15 suites, c-dkdh-play, c-review-F18. Proof: their run entries; `bp.sh run`, `bp.sh act`.
- **G2** (19 projects): the p7 suites and the seven shrine/teammate/companion bosspilots, p8f05 and p8f06 (c-rules-05,
  c-bosspilot-rules05, c-rules-06, c-bosspilot-rules06), and their generators. Proof: the run entries; `bp.sh rules06
  <rule>` for the 21 rules and `cross06.sh` (about 75 minutes of bosspilot; the shrine bosspilots have no wave-3 run:
  build them, and run `bp.sh shrine*` only to show they work).
  **Done 2026-11-13** (Windows, export copy, through the lock): all 19 built (0 errors); the 10 run entries twice, 1 = 2,
  0 differing lines vs `baseline/runs` (S1 107, S2 157, S3 158, upgrades 92, T1 139, T2 207, D 251, c-p7fix 40,
  c-rules-05 170, c-rules-06 224); `bp.sh rules06` 21 of 21 and `cross06.sh` 21 of 21: twice identical, stderr empty,
  0 faults/fallbacks/failed checks/swallowed, 0 differing lines vs `baseline/bp`; the seven shrine modes twice identical
  and equal to their wave-1 records (`baseline/bp-w1`); rules05 18 rules twice identical (section 6). The generators in
  `verify/tools/gen/p8f05` and `p8f06` take no arguments and reproduce both fixtures and both harnesses byte for byte.
- **G3** (13 projects + the hall): c-enc-secrets, c-collectibles A/B/C, c-bosspilot-collect, c-bosspilot-f04, c-w1fix,
  c-enc-07, c-enc-08, c-enc-09, c-bosspilot-enc09, c-enc-10, c-w3fix; the base hall harness and the 38 audit harnesses;
  the family 02/03/04/08/09/10 generators. Proof: the run entries; `bp.sh` enc09-seeds, enc09-auto, enc09-finals,
  enc09-rooms, enc09-cards, enc10, enc10a/b, enc10-shrine, enc10-seeds; `hall.sh`.

Porting one suite:

1. Copy its `*.cs` and `*.csproj` (and `Tests/` for the hall) from the old place to `verify/suites/<origin>/<name>`
   (MANIFEST.md section 2). No backups, logs, `obj/`, `out/`, `engine-out/`.
2. `python -I verify/tools/port_paths.py verify/suites/<origin>/<name>`; read every `CHECK` line it prints and fix
   those by hand (section 3, rule 4); look for fixture-vs-runtime-XML comparisons (rule 5).
3. If it reads a file not yet in `verify/fixtures`, copy it there (LF, no BOM) and add it to MANIFEST.md section 4.
4. `bash verify/bin/builds.sh only <origin>/<name>`; run it twice (`runs.sh 1 only ...`, `runs.sh 2 only ...`, through
   the lock on the original PC) and `same.sh only <origin>/<name>`: identical, 0 differing lines.
5. `git add verify/suites/<origin>/<name>` (sources only; .gitignore keeps obj/ and out/ out).
