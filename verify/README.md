# verify/: the Dungeon Run regression suites

The headless suites, fixtures, recorded baselines and runners that verify the unified Dungeon Run work (phases 2-8).
They used to live in a session scratchpad on one Windows PC, with absolute paths; here they build and run from any
clone, on Linux or Windows, with only the .NET SDK pinned in `global.json` (10.0.402) and bash.

The current baseline is the wave-6 integration (`handoff/phase8/reports/integrate-wave6.md`), recorded on Linux. Every
number a runner prints must equal it (section 4 below). `MANIFEST.md` maps every file here to where it came from.

## 1. Quick start

```bash
bash verify/bin/build-all.sh            # engine + catalog tool + every suite + randplay + the hall (no plugin, no client files)
bash verify/bin/runs.sh 1 all           # every suite once (about 20 minutes); then:
bash verify/bin/runs.sh 2 all
bash verify/bin/same.sh                 # run 1 vs run 2, run 1 vs the baseline: expect identical / 0 differing lines
bash verify/bin/bp.sh enc09             # one bosspilot mode, twice, compared with the baseline
bash verify/bin/hall.sh harness && bash verify/bin/hall.sh 1 38 && bash verify/bin/hall.sh same
bash verify/bin/final.sh engine builds runs1 enc rules06 cross06 released runs2 same hall   # the wave-3 set, about 3 hours
bash verify/bin/final.sh released-all w1 w2 rules05 cross05 w4 w5 w6   # the other bosspilot modes, about 5-6 hours more
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
  lib/suites.sh                the suite registry: BUILD_LIST (83), RUN_LIST (70), HALL_BASE + HALL_LIST (38), PILOT_LIST
  lib/VerifyPaths.cs           compiled into every suite: VerifyPaths.Root/Repo/Data/Fixture/Out/ReadText/WindowsText
  bin/engine.sh                the one engine build -> verify/.out/engine-out/SabberStoneCore.dll
  bin/build-all.sh builds.sh runs.sh same.sh bp.sh cross05.sh cross06.sh hall.sh final.sh gen.sh
  suites/<origin>/<name>/      one folder per suite: Program.cs (+ other .cs), the .csproj; <origin> = the scratchpad
                               folder it came from (audit-verify, p6f15, p7, p8f01, ...); the hall is suites/hall/<name>
  fixtures/<old path>/         inputs the suites read, at the path they had under the old scratchpad
  baseline/runs/<name>.txt     the wave-6 log of each run entry (selfplay, selfplay-games, randplay), Linux
  baseline/bp/bp-<mode>-a.txt  the wave-6 bosspilot results, every mode (196 files), Linux
  baseline/bp-port/            history: the modes wave 3 did not rerun, recorded by the port on the wave-3 tree
  baseline/bp-w1/ bp-w2/       history: the wave-1 and wave-2 records (section 6); bp.sh reads them only for a mode
                               baseline/bp does not hold
  baseline/hall.txt            the hall result lines (unchanged since wave 3)
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

## 4. The baseline (wave 6, recorded on Linux; every number must match)

The baseline is the wave-6 integration (families 20-24; `handoff/phase8/reports/integrate-wave6.md`), run on Linux
(the cloud container, .NET SDK 10.0.402) on 2026-11-16/17: every suite twice, every bosspilot mode twice, the hall.
`baseline/runs`, `baseline/bp` and `baseline/hall.txt` hold those results; the wave-5 records they replaced are in git
history (`bp-port`, `bp-w1` and `bp-w2` stay as older history, section 6).

Suites (`same.sh`: run 1 = run 2, 0 differing lines vs `baseline/runs`, except c-secretsearch, whose timing lines
differ between any two runs; its SUMMARY line is what must match):

| Entry | Result | Entry | Result |
|---|---|---|---|
| c-runlayer | 244 passed, 0 failed | c-catalog | 127 passed, 0 failed |
| c-content | 905 passed, 0 failed | c-admission | 4120 passed, 0 failed |
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
| c-enc-engine | RESULT 124 passed, 0 failed | c-enc-secrets | 60 |
| c-collectibles A / B / C | 115 / 108 / 102 | c-w1fix | 23 |
| c-rules-05 | 170 | c-enc-07 | 161 |
| c-enc-08 | 113 | c-rules-06 | 224 |
| c-enc-09 | 166 | c-enc-10 | 116 |
| c-w3fix | 4 | c-secretscan | faults 0 (both generations; items 851) |
| c-secretsearch | SUMMARY checks passed 689 failed 0 | selfplay | decks 57 games 200 p1 103 p2 97 other 0 faults 0 digest 2CE546A18F96DCBC |
| randplay | games=200 digest=1DE562A92D22C987 (regkeys 2588) | c-enc-11 (wave 4) | RESULT 109 passed, 0 failed |
| c-enc-12 (wave 4) | RESULT 105 passed, 0 failed | c-enc-13 (wave 4) | RESULT 112 passed, 0 failed |
| c-w4fix (wave 4) | RESULT 4 passed, 0 failed | c-enc-14 (wave 5) | RESULT 117 passed, 0 failed |
| c-enc-15 (wave 5) | RESULT 103 passed, 0 failed | c-enc-16 (wave 5) | RESULT 124 passed, 0 failed |
| c-enc-17 (wave 5) | RESULT 123 passed, 0 failed | c-enc-18 (wave 5) | RESULT 109 passed, 0 failed |
| c-enc-19 (wave 5) | RESULT 115 passed, 0 failed | c-w5fix (wave 5, extended in wave 6) | RESULT 29 passed, 0 failed |
| c-enc-20 (wave 6) | RESULT 111 passed, 0 failed | c-enc-21 (wave 6) | RESULT 100 passed, 0 failed |
| c-enc-22 (wave 6) | RESULT 121 passed, 0 failed | c-enc-23 (wave 6) | RESULT 68 passed, 0 failed |
| c-enc-24 (wave 6) | RESULT 65 passed, 0 failed | | |

70 run entries (`runs.sh list`), 83 build projects (`builds.sh list`).

Hall (`hall.sh same`): every harness's SUMMARY line equal to `baseline/hall.txt`, all with 0 bad and 0 failed; 955
tests / 8285 checks in all = the base harness (8 tests, 114 checks) + the 38 audit harnesses (947 tests, 8171 checks).
Unchanged by waves 3, 4, 5 and 6.

Bosspilot (`bp.sh`; 196 results; each mode twice identical, 0 faults / 0 fallbacks / 0 failed checks / 0 swallowed leaf
exceptions, 0 differing lines vs `baseline/bp`; stderr empty for every encounter, rules and cross mode; the released
harnesses (run, act, fixture, catalog and the seven shrine modes) print one diagnostic line on stderr per run,
"pending-choice leaves scored as they stand N", 45 or 46 bytes on Linux (46 or 47 with CRLF on Windows)). Games / boss
wins:

| Mode | Result | Mode | Result | Mode | Result |
|---|---|---|---|---|---|
| enc09 | 22 / 15 | enc10 | 30 / 26 | enc11a (`<k>` 1, 2, `same`) | 28 / 18 |
| enc09-seeds | 22 / 17 | enc10a (same) | 32 / 21 | enc11b (same) | 24 / 19 |
| enc09-auto | 16 / 10 | enc10b (same) | 28 / 22 | enc11-pete | 6 / 5 |
| enc09-finals | 20 / 20 | enc10-nab | 10 / 9 | enc11-cards (same; the four cards played) | 24 / 22 |
| enc09-rooms | 16 / 6 | enc10-cards (same) | 36 / 19 | enc11-shrine (same; 9 of 9 shrine fights) | 37 / 28 |
| enc09-cards | 24 / 22 | enc10-shrine (same) | 37 / 29 | enc12 | 26 / 14 |
| run (released) | 40 / 27 | enc10-seeds | 30 / 21 | enc12-seeds | 26 / 18 |
| act (released) | 40 / 26 | enc10-hagatha | 6 / 6 | enc12-hidden | 18 / 14 |
| fixture (released) | 40 / 27 | catalog (released) | 40 / 27 | enc13a | 30 / 29 |
| shrine | 40 / 24 | shrineS2 / S3 / U | 40 / 23, 40 / 22, 40 / 25 | enc13b | 24 / 16 |
| shrineT1 / T2 / D | 40 / 13, 40 / 20, 40 / 14 | | | enc13-seeds | 27 / 25 |
| enc14 (w5) | 30 / 18 | enc14-seeds (w5) | 30 / 18 | enc14-flight (w5) | 8 / 7 |
| enc14-cards (w5; all 5 boss cards played) | 12 / 5 | enc15 (w5) | 28 / 17 | enc15-seeds (w5) | 28 / 14 |
| enc15-rat (w5) | 8 / 3 | enc15-toys (w5) | 8 / 6 | enc16 (w5) | 28 / 16 |
| enc16-seeds (w5) | 28 / 16 | enc16-carousel (w5) | 8 / 8 | enc16-cards (w5; see below) | 18 / 14 |
| enc17 (w5, `<k>`, `same`) | 28 / 20 | enc17-seeds (w5, same) | 28 / 17 | enc17-void (w5, same) | 6 / 4 |
| enc17-cards (w5, same; `enc17-check`: all 7 played) | 20 / 11 | enc18a (w5, same) | 12 / 7 | enc18b (w5, same) | 12 / 9 |
| enc18-finale (w5, same) | 8 / 6 | enc19a / enc19b (w5) | 18 / 14, 18 / 15 | enc19-seeds-a / -b (w5) | 18 / 12, 18 / 14 |
| enc20a / enc20b (w6) | 18 / 13, 18 / 14 | enc20-seeds-a / -b (w6) | 18 / 12, 18 / 12 | enc21a / enc21b (w6) | 16 / 13, 18 / 16 |
| enc21-seeds-a / -b (w6) | 16 / 13, 18 / 17 | enc22a / enc22b (w6) | 16 / 12, 20 / 17 | enc22-seeds-a / -b (w6) | 16 / 13, 20 / 18 |
| enc23 / enc23-seeds (w6) | 10 / 10, 10 / 10 | enc23-cards (w6, `<k>`, same; the eight family cards played) | 16 / 13 | enc24 (w6) | 6 / 6 |
| plague24 95 / 110 / 195 / 210 (w6) | 3 / 3 each | tekahn24 (w6) | 2 / 1 | | |
| enc01 (w2) | 20 / 18 | secret4 (w2) | 24 / 20 | rules01 DALA_850 / 864 / 866 (w2) | 10 / 8, 10 / 8, 10 / 10 |
| enc07a / enc07b (w2) | 36 / 31, 36 / 33 | shrine07 (w2) | 37 / 32 | seeds07 19000 (w2) | 36 / 30 |
| enc08 (w2) | 34 / 28 | enc08-tag (w2) | 8 / 7 | enc08-cards (w2; the four cards played) | 12 / 11 |
| enc08-trapped (w2) | 6 / 5 | enc08-seeds (w2) | 34 / 26 | old01 (w1) | 12 / 12 |
| secretfix (w1) | 6 / 5 | plague 95 / 110 / 195 / 199 / 210 (w1) | 2 / 2 each | finale (w1) | 8 / 8 |
| relay (w1) | 2 / 1 | collectA (w1; see below) | 32 / 15 | collectP 1 / 2 / 3 / 4 (w1) | 6 / 5, 6 / 6, 6 / 6, 3 / 3 |
| enc03 a / b / c (w1; `enc03-check`: every card played) | 24 / 24, 24 / 20, 16 / 16 | enc01-f03 (w1) | 20 / 18 | icarax2 (w1) | 4 / 4 |
| f04-boss (w1; see below) | 40 / 40 | f04-player (w1, REQUIRE=1) | 24 / 13 | f04-icarax 0 / 110 / 210 (w1) | 4 / 4 each |
| rules06 `<rule>`, 21 rules | 16 each (12 for ULDA_706) | cross06 (`cross06.sh`), 21 rules x 12 bosses | 12 each (11 for ULDA_719, ULDA_804) | | |
| rules05 `<rule>`, 18 rules | 16 each (12 for DALA_831, DALA_833) | cross05 (`cross05.sh`), 18 rules x 14 bosses | 14 each (13 for DALA_852, DALA_853) | | |

`final.sh` stages: engine builds runs1 enc rules06 cross06 released released-all w1 w2 rules05 cross05 w4 w5 w6 runs2
same hall (`w4` is the family 11/12/13 set, `w5` the family 14-19 set, `w6` the family 20-24 set). Three coverage checks
print a miss on this tree, because the fixture rows' seeds follow their row index (the fixture rows now sit after the 255
shipped rows) and the act walks met other treasures: `bp.sh enc16-cards` "UNPLAYED: LOOTA_811" (Orb of Destruction, as in
wave 5), `bp.sh collectA` "UNPLAYED: AT_081 x0 DAL_731 x0 GIL_655 x0 GVG_113 x0 LOE_119 x0" and `bp.sh f04-boss` "FAIL:
never played by the boss: ULD_290 x0". None is a fault. On the wave-5 fixture files (whose rows sit after 194 rows) the
same engine on the wave-5 data plays all 33 family 02 cards and all 23 family 04 cards (ULD_290 x2); with those fixtures
on the wave-6 data it plays ULD_290 x3 and the five family 02 cards, and misses only LOE_092, which the recorded collectA
plays x2; the run player plays ULD_290 x2 in f04-player (`integrate-wave6.md` section 7). enc03's ULD_169, wave 5's
other miss, is played on this tree.

**The close-out (family 25, stage A; not a baseline yet).** It adds the suite `p8f25/c-enc-25` (entry 71 of `runs.sh`,
84 build projects), the `final.sh` stage `c25` and three bosspilot modes on the SHIPPED schedules (no fixture):
`bp.sh finale25` (acts-v3h's Heist finale, twice per call), `bp.sh tekahn25` (acts-v3t's Tekahn finale with the hero
relay, twice per call) and `bp.sh sweep25 <a|b|c> <k>` (new runs with no named schedule, the default roll, each fought at
a floor of its own; `DEFAULT_SWEEP` in `p8f01/c-bosspilot-encounter`, then `bp.sh same sweep25-<x>`). Until the full
regression of the close-out re-records `baseline/`, expect the bosspilot modes that walk acts-v3h or acts-v3t through a
tavern to differ from their wave-6 records: ULDA_038 Explorer Retraining joined the tavern draw of stamp-3 runs (every
walk buys every offer, so a run may also change its power). The ledger is 4970 rows (implemented 3880, partial 10, missing
13, duplicate/support 1067); `p8f18`'s and `p8f24`'s finale fixtures are the shipped schedule byte for byte.
The close-out review (stage B) moved five powers the wiki marks Auto-cast from AI clicks to rule casts (Sand Surge, Power
of Wrath, Mighty Windstorm, Out of Control, Marei Loom's Next...): expect the enc18a, enc19a/b, enc20a/b and their seeds
modes, and any mode that meets those bosses (sweep25-a meets Sinkhole), to differ from their wave-6 records for that
reason too; their `power` column counts clicks only, so it reads 0 for a rule-cast power (c-enc-18 F, c-enc-19 H and
c-enc-20 D, J, O count the casts).

**Step 9's tests (the test phase; not a baseline yet; `handoff/phase9/tests.md`).** Six suites join the run list as entries
72-77 (`p9/c-sched-9`, `c-econ-9`, `c-rules-9`, `c-ends-9`, `c-gen-9`, `c-longrun-9`; 77 entries, 93 build projects with the
three step-9 bosspilot harnesses `p9/c-bosspilot-pairs9`, `c-bosspilot-ends9` and `c-bosspilot-run`). c-rules-9 fails T2
and K1 by design until calibration (two act III twists roll in about 10% of runs, below 15%; `mod_hot_start` is a no-op on
the floor-15 majors); c-longrun-9's `TIME` line is left out of `same.sh`'s comparison. The `final.sh` stage `p9` runs the
non-calibration modes: `bp.sh pairs9 1-25` (chunk 23 exited 3, three boss-AI fallbacks in case 1722, until the engine's
aura-removal fix, tests.md section 2), `bp.sh ends9 <part>` and `bp.sh treasure9`. The calibration modes (`slots9`, `outliers9`, `finale9`,
`tekahn9`, `longrun9`) and the 48t gate (`gate48t`, `uld152`) run as queues.

Generators (`gen.sh`): all six generated files byte-identical; encounters "255 rows, 296 decks (125 authentic, 171
changed), 876 adaptations, 4 rejected"; catalog "506 entries (452 offerable pool, 2 withdrawn, 52 fixed), 6508 admitted
and 1 excluded"; ledger rows 4992, implemented 2814, partial 817, missing 302, duplicate/support 1059, adapted 0. The
family fixture generators (`MANIFEST.md` section 6) reproduce every committed fixture byte for byte.
Engine build: 0 warnings, 0 errors (Linux md5 317cf8451d56e7eebcb20629de20708f; builds are not byte-identical across
platforms: the wave-3 tree gives 31d96fc6... on Linux, 07028f46... on Windows).

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
- **Linux (proven 2026-11-14/15; the baseline is now recorded on Linux).** The step-1 proof ran every `final.sh` stage on
  the unchanged wave-3 tree in the cloud container: every number equal to the Windows records except three platform
  differences, none of them behaviour: c-p7fix H1 prints `·` where the Windows log has a mangled console byte (2 lines);
  c-enc-model F2 prints the golden-save lengths 13954 / 25744 where Windows printed 14306 / 26370 (LF vs CRLF in the
  serialized save, 4 lines); c-secretsearch timing lines. The released harnesses' stderr line is 45 or 46 bytes per run on
  Linux, 46 or 47 on Windows (CR). The hall gave 39 of 39 SUMMARY lines equal (955 tests / 8285 checks). Engine builds
  are not byte-identical across platforms (wave-3 tree: 31d96fc6... on Linux, 07028f46... on Windows). The wave-4, wave-5
  and wave-6 baselines (`baseline/runs`, `baseline/bp`, `hall.txt`) were recorded on Linux, so c-p7fix H1 and c-enc-model F2
  hold the Linux text: on Windows expect those two to differ the other way (2 and 4 lines), and the released stderr one byte
  longer per run. Nothing in `verify/` needed a port fix for Linux.
- Wave 4 moved bosspilot lines from the wave-3 records as waves 2 and 3 did (`integrate-wave4.md` section 7 has every
  count): new rows in the pools move the act walks and seeds of every harness that walks acts-v3h; the fixture modes
  (enc09-cards, enc10-cards, collectA, enc03, f04-boss, f04-player, enc08-cards) move on every line because their
  fixtures were rebuilt from the merged data; shrine runs also move because a run never meets the champion of its own
  shrine class now. cross05 and the released/shrine modes did not move.
- Wave 5 moved bosspilot lines from the wave-4 records the same way (`integrate-wave5.md` section 7 has every count): the
  91 new rows enter the pools of acts-v3h, so every harness that walks the acts to a boss's floor fights there with another
  run deck (a different size, or the same size with other cards), and the fixture modes move on every line because the
  rebuilt fixtures put their rows after the new ones (a bosspilot seed follows the boss's row index). The engine changes
  of the wave move none of the recorded games by themselves: on the wave-4 data (encounters-v1.json and schedule-v1.json
  of e367c93) the wave-5 engine reproduces the wave-4 records of cross05 DALA_829 (14 games), cross06 ULDA_702 (12) and
  enc09-auto (16, the four auto-cast bosses of family 09) line for line. The released and shrine harnesses did not move.
- Wave 6 moved 146 of the 175 wave-5 bosspilot records (`integrate-wave6.md` section 7 has every count), for three
  reasons. (a) 60 of the 61 new rows enter the pools of acts-v3h (53 Tombs rows, four nemeses beside Hagatha, three
  Plague Lords beside Icarax; Tekahn's finale row is in none), so the act walks differ. (b) Family 24's ULDA_115 "Gnomebliterator" is a new Tombs treasure of
  encounter schedules (run-v1.json, `"runs": "encounters"`), so every Tombs explorer run on acts-v3h drafts from a larger
  treasure pool and reaches its fight with another run deck: on the wave-5 data/dungeon-run of e32fe81 (SOLOSTONE_ROOT on
  a scratch root) the wave-6 engine and harnesses reproduce the wave-5 records of cross05 DALA_829 (14 games), cross06
  ULDA_702 (12) and enc09-auto (16) line for line, and adding only that one run-v1.json row gives 4, 4 and 10 differing
  lines, exactly the lines the wave-6 engine gives on the wave-5 encounters and schedule. (c) The fixture modes move on
  nearly every line because the rebuilt fixtures put their rows after the 255 shipped rows. Two engine changes reach
  random pools in every game: Mogu Cultist (ULD_705, family 20) is now a scripted 1-Cost collectible, and Omega Mind's
  Mind Meld (BOT_543e, family 24) now acts. Mogu Cultist alone moves one f04-boss game by one decision on the wave-5 data
  and fixture, and the run-log lines that list or count random pools (c-heroes-F6 T22, c-treasures-F7 T33, c-treasures-F12
  T30, c-review-F18 R2, c-rules-05 Q14, c-rules-06 T7, c-enc-10 O2, c-enc-15 I6, c-enc-18 I3: 136 -> 137 minions); with
  it and Mind Meld both unscripted, randplay's game digest is wave 5's 40207800B289E504 again (checked on scratch engine
  builds, never committed). The released and shrine harnesses did not move.

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
