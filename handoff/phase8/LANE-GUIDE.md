# Lane guide (phase 8, families 02-24)

Verified on the real tree on 2026-11-12 in all three lanes (base stamp 20261112-184229). Everything below was
run as written; the numbers are what a fresh lane must show before you change anything.

## 1. Where things are

`<k>` is your lane number (1, 2 or 3), `<NN>` your two-digit family number.

| | Windows spelling | Git Bash spelling |
|---|---|---|
| shared scratchpad `SP` | `C:\Users\user\AppData\Local\Temp\claude\0a564ffc8743aeec\d2a4e245-ca37-4433-9b55-c35ed325f081\scratchpad` | `/c/Users/user/AppData/Local/Temp/claude/0a564ffc8743aeec/d2a4e245-ca37-4433-9b55-c35ed325f081/scratchpad` |
| your repo copy `R` | `SP\L<k>\repo` | `$SP/L<k>/repo` |
| your scratch copy `S` | `SP\L<k>\sp` | `$SP/L<k>/sp` |
| your engine output | `SP\L<k>\sp\audit-verify\engine-out` | `$S/audit-verify/engine-out` |
| lane tools (shared, read and execute only) | `SP\p8lanes` | `$SP/p8lanes` |

Every Bash call starts in a reset directory with no variables: begin each call (or your run script) with

```bash
SP=/c/Users/user/AppData/Local/Temp/claude/0a564ffc8743aeec/d2a4e245-ca37-4433-9b55-c35ed325f081/scratchpad
L=$SP/L<k>; R=$L/repo; S=$L/sp; V=$S/audit-verify; T=p8f<NN>
H="bash $SP/p8lanes/hrun.sh"        # the cross-lane harness lock, section 4
mkdir -p $S/p8f<NN>
```

The same commands are packaged as `bash $SP/p8lanes/lane.sh <k> <NN> <step>` (tested in a lane; section 3).

## 2. The lane commands

One build or harness process at a time inside your lane. Builds and generators do not take the harness lock;
every harness executable does.

### 2.1 Engine (5-8 s)

```bash
cd $R && dotnet build engine/SabberStoneCore/SabberStoneCore.csproj -c Release -f net10.0 -nologo -o $V/engine-out > $S/p8f<NN>/build-engine-$T.txt 2>&1
echo "exit=$? $(grep -E 'Warning\(s\)|Error\(s\)' $S/p8f<NN>/build-engine-$T.txt | tr '\n' ' ')"; grep -- '->' $S/p8f<NN>/build-engine-$T.txt
```

Expect `0 Warning(s) 0 Error(s)` and a `->` line that names `\L<k>\sp\audit-verify\engine-out\SabberStoneCore.dll`.
Your engine-out starts as a copy of the main tree's build: rebuild it after every engine edit, before the suites.

### 2.2 Plugin (9-14 s)

```bash
cd $R && dotnet build plugin/OfflineProbe.csproj -c Debug -p:EngineSource=true -p:Installer=true -p:NuGetAudit=false > $S/p8f<NN>/build-plugin-$T.txt 2>&1
echo "exit=$? $(grep -E 'Warning\(s\)|Error\(s\)' $S/p8f<NN>/build-plugin-$T.txt | tr '\n' ' ')"
```

Expect `75 Warning(s) 0 Error(s)` (the same 75 warnings as the main tree; the summary is printed twice in the log)
and `OfflineProbe -> ...\L<k>\repo\plugin\bin\Debug\net48\SoloStone.Plugin.dll`. It shares the lane's engine `obj`
folder with 2.1: never at the same time as 2.1 or 2.3 in your lane. Other lanes have their own `obj`.

### 2.3 Generators: encounters, catalog, ledger, in this order (10-12 s, 5 s, 11-13 s)

```bash
cd $R/tools/dungeon-run-catalog && dotnet run -c Release -- encounters
cd $R/tools/dungeon-run-catalog && dotnet run -c Release
cd $R/tools/restoration-ledger  && dotnet run -c Release
```

They find the repo by walking up from the current directory, so the `cd` into YOUR copy is what keeps them in the
lane: every `wrote ...` line must name `\L<k>\repo\`. Files written:

| step | writes |
|---|---|
| encounters | `data/dungeon-run/encounters-v1.json`; the lists `encounterAdaptations` / `encounterRejections` inside `tools/restoration-ledger/curation.json` |
| catalog | `data/dungeon-run/catalog-v1.json`, `docs/design/dungeon-run-catalog.md` |
| ledger | `docs/design/restoration-ledger.csv`, `docs/design/restoration-ledger.md` |

The five whole files are the `generated` list of `lanes.json`: the merge drops your copies and the integrator
regenerates them in the main tree. `curation.json` is merged as text (it also holds hand-written parts).
In a fresh lane all six files come out byte-identical to the main tree (checked in lanes 1, 2, 3):
encounters `8 rows, 12 decks (2 authentic, 10 changed), 19 adaptations, 1 rejected`; catalog `506 entries
(452 offerable pool, 2 withdrawn, 52 fixed), 6318 admitted and 191 excluded member references`; ledger
`rows 4579, implemented 2722, partial 39, missing 760, duplicate/support 1058, intentionally adapted 0`.

### 2.4 Reduced suites: build (2-4 s each, about 65 s for the nine with first-open virus-scan delays)

```bash
for d in $V/c-content $V/c-admission $V/c-runlayer $V/c-acts $V/c-deckops $S/p7/suites/c-shrine-core \
         $S/p8f01/suites/c-enc-model $S/p8f01/suites/c-enc-engine $S/p8f01/suites/c-bosspilot-encounter; do
  cd $d && dotnet build --no-incremental -c Release -nologo -v q -o out > build-$T.txt 2>&1
  echo "$(basename $d) build exit=$? $(grep -E 'Error\(s\)' build-$T.txt | tr '\n' ' ')"
done
```

### 2.5 Reduced suites: run (each through the lock)

```bash
cd $V/c-content   && $H ./out/AiHarness.exe > run-$T-1.txt 2>&1; echo "exit=$? $(tail -n 1 run-$T-1.txt)"
cd $V/c-admission && $H ./out/AiHarness.exe > run-$T-1.txt 2>&1; echo "exit=$? $(tail -n 1 run-$T-1.txt)"
cd $V/c-runlayer  && $H ./out/AiHarness.exe "$(cygpath -w $S/content2-fixtures)" > run-$T-1.txt 2>&1; echo "exit=$? $(tail -n 1 run-$T-1.txt)"
cd $V/c-acts      && $H ./out/AiHarness.exe > run-$T-1.txt 2>&1; echo "exit=$? $(tail -n 1 run-$T-1.txt)"
cd $V/c-deckops   && $H ./out/AiHarness.exe > run-$T-1.txt 2>&1; echo "exit=$? $(tail -n 1 run-$T-1.txt)"
cd $S/p7/suites/c-shrine-core   && $H ./out/AiHarness.exe > run-$T-1.txt 2>&1; echo "exit=$? $(tail -n 1 run-$T-1.txt)"
cd $S/p8f01/suites/c-enc-model  && $H ./out/AiHarness.exe > run-$T-1.txt 2>&1; echo "exit=$? $(tail -n 1 run-$T-1.txt)"
cd $S/p8f01/suites/c-enc-engine && $H ./out/AiHarness.exe > run-$T-1.txt 2>&1; echo "exit=$? $(tail -n 1 run-$T-1.txt)"
```

Exit 75 means the lock was busy and the harness did NOT run (the log then holds only the hrun message): call again.

| suite | fresh-lane result (= main-tree baseline) | quiet machine | busy machine |
|---|---|---|---|
| c-content | `904 passed, 0 failed` | 2-5 s | 3-13 s |
| c-admission | `2244 passed, 0 failed` | 6-8 s | 9-20 s |
| c-runlayer | `244 passed, 0 failed` | 10-14 s | 15-36 s |
| c-acts | `181 passed, 0 failed` (A6 digest 9F91C1FE52A4B998) | 44-56 s | 138-207 s |
| c-deckops | `174 passed, 0 failed` (L1 digest 5169BABFF3951AA2) | 10-15 s | 26-31 s |
| c-shrine-core | `SUMMARY checks passed 131 failed 0` | 3-5 s | 7-9 s |
| c-enc-model | `RESULT 141 passed, 0 failed` | 7-10 s | 15-18 s |
| c-enc-engine | `RESULT 122 passed, 0 failed` | 2-5 s | 2-7 s |
| all eight | | 85-114 s | 229-341 s |

In every lane, twice, the complete log of each suite was line-for-line equal to the main tree's last log
(`run-p8f01b-2.txt`, 0 differing lines after taking the lane prefix out of paths).

### 2.6 c-bosspilot-encounter by FAMILY, BOSS, RULE

```bash
cd $S/p8f01/suites/c-bosspilot-encounter
$H env FAMILY=<NN> ./out/AiHarness.exe > $S/p8f<NN>/run-bp-fam-$T-1.txt 2> $S/p8f<NN>/err-bp-fam-$T-1.txt; echo "exit=$? $(tail -n 1 $S/p8f<NN>/run-bp-fam-$T-1.txt)"
$H env BOSS=<row id>,<row id> ./out/AiHarness.exe > $S/p8f<NN>/run-bp-boss-$T-1.txt 2> $S/p8f<NN>/err-bp-boss-$T-1.txt
$H env RULE=<rule id> BOSS=<row id>,<row id>,<row id> ./out/AiHarness.exe > $S/p8f<NN>/run-bp-rule-$T-1.txt 2> $S/p8f<NN>/err-bp-rule-$T-1.txt
```

- `FAMILY=<NN>` plays every row of `encounters-v1.json` whose `family` is `<NN>`; `BOSS` takes row ids in the order
  given; `RULE` injects one scripted rule as an anomaly of the fight act. Also read: `GAMES` (per boss, default 2),
  `WATCH=<card id>,..`, `CARRIED`, `SCHEDULE` (default acts-v3h), `SEED_BASE` (default 9000), `FINALE=1`,
  `PLAYER_HEALTH`, `BOSS_FROM`, `BOSS_COUNT`, and `SCHEDULE_JSON` / `ENCOUNTERS_JSON` for fixtures. A fixture path
  must be a Windows path inside your lane: `SCHEDULE_JSON="$(cygpath -w $S/p8f<NN>/fixtures/schedule-x.json)"`.
- Run each mode twice and compare without the timing column:
  `strip() { sed -E 's/ +[0-9.]+s$//' "$1"; }; cmp -s <(strip a.txt) <(strip b.txt) && echo identical`.
  The last line must read `faults 0, fallbacks 0, failed checks 0, swallowed leaf exceptions 0` and the stderr file
  must be empty.
- Measured in a lane: the eight family-01 rows (`BOSS=ulda_40_icarax,gila_61_hagatha,dala_21_whirt,trl_200_voone,loot_35_brandlemar,loot_48_trapped_room,gila_25_ivan,ulda_44_trap_room`)
  `games 20, boss wins 15, faults 0, fallbacks 0, failed checks 0, swallowed leaf exceptions 0, power-unused 0,
  form-unreached 1` in 36-47 s (132-134 s on the busy machine), twice identical in each lane and 0 differing lines
  against the main tree's `p8f01b/bp-enc01-a.txt`; `FAMILY=01` 10 games in 27-29 s; `RULE=DALA_850` on three bosses
  10 games in 23 s, 0 differing lines against the main tree's log.

## 3. lane.sh

`bash $SP/p8lanes/lane.sh <k> <NN> <step>` runs exactly the commands of section 2 inside lane `<k>`:

```bash
bash $SP/p8lanes/lane.sh <k> <NN> engine
bash $SP/p8lanes/lane.sh <k> <NN> plugin
bash $SP/p8lanes/lane.sh <k> <NN> gen
bash $SP/p8lanes/lane.sh <k> <NN> build                      # or: build c-content c-acts   or: build $S/p8f<NN>/suites/<yours>
bash $SP/p8lanes/lane.sh <k> <NN> run 1 c-content c-admission c-runlayer
bash $SP/p8lanes/lane.sh <k> <NN> run 1 c-acts
bash $SP/p8lanes/lane.sh <k> <NN> run 1 c-deckops c-shrine-core c-enc-model c-enc-engine
bash $SP/p8lanes/lane.sh <k> <NN> bp fam 1 FAMILY=<NN>
bash $SP/p8lanes/lane.sh <k> <NN> bp fam 2 FAMILY=<NN>
bash $SP/p8lanes/lane.sh <k> <NN> same fam                   # compares the two runs, writes p8f<NN>/bp-fam-a.txt
bash $SP/p8lanes/lane.sh <k> <NN> bp rule 1 RULE=<rule id> BOSS=<id>,<id>,<id>
```

It takes each harness through the lock itself, writes its logs to `$S/p8f<NN>/` (and `build-$T.txt` / `run-$T-<n>.txt`
in the suite folders), and reports a busy lock as `NOT RUN`. `run <n>` with no suite names runs all eight; on a
busy machine that can pass the ten-minute limit of one foreground shell call, so slice it as above. The file is
shared: do not edit it; copy it into your `p8f<NN>` folder for a variant (your new suites, your fixture modes).

## 4. The harness lock (hrun.sh)

`bash $SP/p8lanes/hrun.sh <command...>` runs the command while holding `$SP/p8lanes/harness.lock`. Wrap every
harness executable (AiHarness.exe, SecretScan.exe, a bosspilot run), as shown in 2.5 and 2.6. Environment goes
through `env`: `$H env BOSS=x GAMES=6 ./out/AiHarness.exe`. Checked from lane folders, in separate shell calls:

- lane 1 held the lock; a call from lane 2 with `HRUN_WAIT=6` printed `hrun: harness lock busy for 6s (held by:
  69488 16:48:19 scratchpad/L1 sleep 40). Nothing was run; call again.` and returned 75;
- a second call with the default wait (240 s) waited 32 s and then ran;
- a lock left by a dead holder is taken over; the command's own exit code is passed through; no arguments = exit 2.

Rules: keep one locked call under about 6 minutes (lock one harness run, not a whole script of them); always look
at the exit code (75 = nothing ran, and a redirected log then holds only the hrun line); if your shell call is cut
off while a harness runs, the harness may live on after the lock is released: check `tasklist | grep AiHarness`
and end only the process you started before you run the next one.

## 5. What is merged, what is not

- Merged as text: every source file you change in `$R`, `curation.json`, the reference `F<NN>.json`, and every file
  in `$S` that is not listed below (so your whole `p8f<NN>` folder: suites, fixtures, scripts, `bp-*.txt` evidence).
- Dropped and regenerated in the main tree (`lanes.json` `generated`): `data/dungeon-run/encounters-v1.json`,
  `data/dungeon-run/catalog-v1.json`, `docs/design/dungeon-run-catalog.md`, `docs/design/restoration-ledger.csv`,
  `docs/design/restoration-ledger.md`.
- Never merged: anything under `obj`, `bin`, `out`, `__pycache__`, `engine-out`; `*.dll`, `*.exe`, `*.pdb`; logs
  named `build-*.txt`, `run-*.txt`, `err-*.txt`, `fixture-p8f*.txt`, `fixture-err-p8f*.txt` (anywhere in `$S`).
- A log with any other name in a mirrored folder IS merged. The lane copies of `p8f01\bp.sh` and `p8f01b\bp.sh`
  write `<mode>-p8f01b-<n>.txt` over mirrored logs in the suite folder: do not run them as they are. Use your own
  tag `p8f<NN>` and the names above, or `lane.sh bp`.
- Pins that move with any new encounter content (update them in your copy and list them): c-deckops T5 and the
  c-catalog ledger golden (ledger rows, now 4579), c-admission's total (2244), c-enc-model A4 (8 rows) and J13,
  c-content's passive registry check.

## 6. Gotchas that were hit while verifying

1. **Inputs outside the mirrored folders.** Three reduced suites read files by a relative path that climbs out of
   their folder. In the first mirror they were missing, so in a lane c-admission and c-acts crashed
   (`FileNotFoundException ...\L1\sp\ledger-before.csv`, `DirectoryNotFoundException ...\L1\sp\p4\golden-out\golden.txt`)
   and c-deckops showed `172 passed, 2 failed`. Now mirrored: `ledger-before.csv`, `p4/golden-out`,
   `p5/d12golden/d12-golden.txt`, `p6f0/catalog-v1.before.json`, `p6f9/run-v1.before.json`,
   `p6f10/before/catalog-v1.before.json` (and `p6f6/run-v1.before.json` for p6f15's c-heroes-F6). If you meet
   another "could not find" that names `\L<k>\sp\<a folder you do not have>`, it is a mirror gap, not your bug:
   report it; meanwhile pass the file as an argument from the shared scratchpad (reading there is allowed), e.g.
   c-acts takes `<golden dir> <fixtures dir>`, c-admission `<ledger-before.csv>`, c-deckops `<d12 golden file>`.
2. **The engine build is byte-identical in every lane and in the main tree** (md5 6d5a9d46d3d4911aa67ebc49a9ed0890
   for the family-01 tree), so comparing dll bytes cannot tell you which engine a suite picked up. What tells you:
   the suite's `.csproj` (HintPath `\L<k>\sp\audit-verify\engine-out\`), or one build with `-v n` and the `csc`
   command line. Checked in each lane for c-bosspilot-encounter: 116 source files under `\L<k>\repo\`, the engine
   reference under `\L<k>\sp\audit-verify\engine-out\`, 0 inputs from the main tree or another lane.
3. **A second folder with SabberStoneCore.dll inside a suite folder** (family 01's `out-debug`) is picked up before
   the HintPath. `lane.sh build` warns when `out/SabberStoneCore.dll` is not your engine-out build.
4. **Backslashes in a Bash heredoc are halved** (`'\\'` arrives as `'\'`, a grep for `C:\\SoloStone` matches nothing).
   Write scripts and C# with the Write tool, use forward slashes or `cygpath -w` in commands, and `C:.SoloStone`
   in a grep.
5. **No git in a lane.** `git` in `$R` answers `fatal: not a git repository`: nothing falls back to the main repo.
   `global.json` (SDK 10.0.402, rollForward disabled) is in your copy; there is no `Directory.Build.props`,
   `nuget.config` or `global.json` above the lane that could change a build.
6. **The first open of a new file is slow** (the virus scanner: 10-20 ms per file, 2-4 s for a fresh dll), so the
   first command after a build can sit for a while. On a busy machine the harnesses took 2.5-4 times longer
   (table in 2.5) and still gave identical output.
7. **c-acts writes under the shared `%TEMP%\c-acts`**, one folder per process id: safe, and one more reason to
   keep harness runs under the lock.
8. **c-catalog** (not in the reduced list) starts `$R\tools\dungeon-run-catalog\bin\Release\net10.0\DungeonRunCatalogTool.exe`:
   it needs step 2.3 to have run in your lane first.

## 7. What a lane cannot or must not run

- Not mirrored on purpose (the full regression runs in the main tree after the merge): the hall harness sources
  (`audit-harness`, `audit-chunk-*`, `audit-fix-*`, `audit-inventory`), and the old copies
  `audit-verify\c-heroes*`, `c-treasures-F*`, `c-dkdh-supply`, `c-runctx`, `c-starters-F10`, `c-upgrades`, whose
  projects point at `p6f2..p6f13\engine-out` (the live copies of those suites are `p6f15\suites\*`; two of them,
  c-heroes-F6 and c-heroes-F9, were built and run in lane 3: 566 and 292 passed, 0 differing lines vs main).
- The four files the sync warns about (their text still names the main tree inside a lane):
  - `installer/SoloStone.Launcher.Tests/RegressionTests.cs` and `Round2Tests.cs`: the strings are inputs to
    path-safety predicates (`Safety.Canonical` must refuse `\\?\C:\SoloStone`; `Safety.DestinationLocations` gets a
    fake resolver). A lane could run the launcher tests, they would behave as in the main tree and open nothing
    there; phase 8 has no reason to run them.
  - `p7f/attr/mk_bp.py` and `mk_noleaf.py`: attribution helpers that copy a suite project and replace
    `C:\SoloStone\plugin\` (assembled from parts) with another plugin folder. In a lane the project already says
    `\L<k>\repo\plugin\`, so the replacement does nothing and the copy silently compiles your lane's plugin: the
    experiment would be wrong, though nothing is written to the main tree unless you pass main paths. Do not run
    them; for an attribution run, copy the script into `p8f<NN>` and replace your lane's plugin path instead.
- `installer/stage.py` calls git and stages into `installer/out`: never in a lane.

## 8. For the integrator: sync, status, diff, merge

- `python -I lanes.py status` 1-3 s; `python -I lanes.py diff <k>` 20 s; `python -I lanes.py sync` 2 min 46 s to
  4 min 45 s (no-op syncs of 2374 repo + 7692 scratch files into the base and three lanes); the sandbox test 36 s.
- Before the change of 2026-11-12 a sync took 9-11 minutes (584 of 660 s inside `open`, the on-access scanner,
  50,000 files one by one), longer than one foreground shell call, and `diff` took up to 4 min 53 s on a busy
  machine. `lanes.py` now opens each batch of 256 files eight at a time right before the unchanged sequential code
  reads it (`warm`); nothing else changed (`verify/lanes-warm.diff`, the old file is `lanes.py.before-warm`;
  sandbox test 193 of 193 before and after). If a sync still runs long, start it in the background and wait for
  `lanes.lock` to disappear; a cut-off sync leaves `LB\sync-in-progress` and the next `sync` completes it.
- `lanes.json` now mirrors the extra inputs of gotcha 1 and lists `docs/design/dungeon-run-catalog.md` as generated.
- `tools/restoration-ledger/curation.json`: every lane's encounters step rewrites the two tool-owned lists, so
  expect overlapping edits there; settle them by keeping the hand-written parts and rerunning the encounters step
  in the main tree, which rebuilds both lists from the reference files.
- Re-verification of a lane after a sync: `bash $SP/p8lanes/verify/lv.sh <k> engine|plugin|gen|build|buildv|run <n>|enc01 <n>|cmp|grepmain`
  (logs under `p8lanes/verify/L<k>/`, outside the lane; `cmp` diffs the lane logs against the main tree's
  `run-p8f01b-2.txt` logs, so it only holds while main is the family-01 tree), `verify/probe.sh <k> on|off` (the
  isolation probe), `verify/gapscan.py <k>` (paths a lane source names that exist in main but not in the lane),
  `verify/mainscan.py <k>` (main-root mentions left in a lane).

## 9. Verification record (2026-11-12)

| check | lane 1 | lane 2 | lane 3 |
|---|---|---|---|
| engine build | 0 warnings, 0 errors | same | same |
| plugin build | 0 errors, 75 warnings (same set as main) | 0 / 75 | 0 / 75 |
| generators | 6 files byte-identical to main | same | same |
| eight suites, run 1 and run 2 | all at baseline, 0 differing lines vs main | same | same |
| bosspilot enc01, run 1 and run 2 | 20 games, 0 faults / fallbacks / failed checks / swallowed, 0 differing lines vs main | same | same |
| compiler inputs of c-bosspilot-encounter | 116 lane-repo sources, lane engine, 0 main | same | same |
| main paths in the run and build logs | 0 lines (174 lines name this lane, 0 another lane) | same | same |
| main-root mentions left in the copy | 2 files (the launcher tests of section 7) | same | same |
| probe: Elder Brandlemar renamed + healthOffset 1 in the lane's `reference/encounters/F01.json` | lane generator wrote the marked row, lane harness played `health 31`; main, base and the other two lanes unchanged (main harness `health 30`, `status` 0 files); restored byte for byte, `diff` empty | same | same |

End state: `status` 0 differing files; `diff 1`, `diff 2`, `diff 3` no items (only build output: 352 files in 26
`obj` / `bin` / `out` folders per lane, 406 in 30 in lane 3 after the two extra p6f15 builds); no file or folder in `C:\SoloStone` newer than the start of the
verification; `--discard-unmerged` was never needed.
