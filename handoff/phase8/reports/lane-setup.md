LANE SETUP VERIFICATION - REPORT

The three lanes are faithful, isolated copies of the family-01 tree once the mirror defects below were fixed. Final state: `status` 0 differing files, `diff 1/2/3` no items, nothing written into C:\SoloStone (no file or folder newer than the start of this work; HEAD 0f6a476, 120 status entries, nothing staged, stash empty). `--discard-unmerged` was never used. Not verified beyond the reduced list: hall, the released bosspilot modes, selfplay and randplay were not run in a lane.

Guide: C:\Users\user\AppData\Local\Temp\claude\0a564ffc8743aeec\d2a4e245-ca37-4433-9b55-c35ed325f081\scratchpad\p8lanes\LANE-GUIDE.md
(below, P = C:\Users\user\AppData\Local\Temp\claude\0a564ffc8743aeec\d2a4e245-ca37-4433-9b55-c35ed325f081\scratchpad\p8lanes)

=====================================================================
1. MIRROR DEFECTS FOUND AND FIXED
=====================================================================
1. Scratch inputs missing from spInclude (real failures in a lane before the fix). Three reduced suites read files through a relative path that climbs out of their folder:
   - c-admission crashed: FileNotFoundException ...\L1\sp\ledger-before.csv
   - c-acts crashed: DirectoryNotFoundException ...\L1\sp\p4\golden-out\golden.txt (it also reads p6f0, p6f9 and p6f10 "before" files)
   - c-deckops: 172 passed, 2 failed (p5\d12golden\d12-golden.txt)
   Fix in lanes.json spInclude: ledger-before.csv, p4/golden-out, p5/d12golden/d12-golden.txt, p6f0/catalog-v1.before.json, p6f9/run-v1.before.json, p6f10/before/catalog-v1.before.json, plus p6f6/run-v1.before.json (read by p6f15's c-heroes-F6). Re-synced; all three suites then at baseline.
2. `generated` lacked docs/design/dungeon-run-catalog.md, which the catalog step writes. Added.
3. lanes.py was too slow for one foreground shell call: a no-op sync took 9 min and 11 min (profiled: 584 of 660 s inside `open`, 50,000 files one by one through the on-access scanner), and `diff` took 273-293 s per lane on a busy machine. I added a read-only warm-up: each batch of 256 files is opened eight at a time right before the unchanged sequential code reads it (sync, status, and both merge loops). Nothing else changed.
   - After: sync 166 s to 4 min 45 s, status 1-3 s, diff 20-21 s.
   - Sandbox test rerun three times (after the lanes.json change, after each lanes.py change): 193 of 193 passed each time.
   - Old file: P\lanes.py.before-warm (byte size equals the original, 74644). Diff: P\verify\lanes-warm.diff (+45 / -4 lines). lanes.json backup: P\lanes.json.before-verify.
   - This change is my own and has not had the adversarial review the rest of the tool had.

Not defects, but decided and written in the guide:
- The four sync-warned files. The two launcher test files hold strings fed to path-safety predicates; a lane could run them, they open nothing in main, and phase 8 has no reason to. p7f/attr/mk_bp.py and mk_noleaf.py replace a main plugin path assembled from parts; in a lane the replacement does nothing, so the experiment would silently compile the lane's plugin. They write to main only if given main paths. The lane rules already forbid both.
- Not mirrored on purpose: hall sources (audit-harness, audit-chunk-*, audit-fix-*, audit-inventory) and the stale audit-verify copies of the F-suites that point at p6f2..p6f13 engine-out. The live p6f15 copies work: c-heroes-F6 566 and c-heroes-F9 292 in lane 3, 0 differing lines against main.

=====================================================================
2. GENERATED LIST (from the generators' source)
=====================================================================
- encounters (tools/dungeon-run-catalog/Encounters.cs:318-340): data/dungeon-run/encounters-v1.json, and the two tool-owned lists inside tools/restoration-ledger/curation.json
- catalog (Program.cs:167-168, 517-520): data/dungeon-run/catalog-v1.json, docs/design/dungeon-run-catalog.md
- ledger (tools/restoration-ledger/Program.cs:153-154, 530-532): docs/design/restoration-ledger.csv, docs/design/restoration-ledger.md

lanes.json "generated" is now exactly those five whole files; curation.json is not in it. All three tools find the repo by walking up from the current directory, so they stay in the lane when started from the lane copy.

=====================================================================
3. PER-LANE NUMBERS AGAINST THE BASELINE (identical in lanes 1, 2 and 3)
=====================================================================
| Check | Result in each lane | Baseline |
|---|---|---|
| Engine build into the lane's engine-out | 0 warnings / 0 errors | 0 / 0 |
| Plugin build | 0 errors / 75 warnings, same warning set | 0 / 75 |
| Generators | all six files byte-identical to main (8 rows / 12 decks / 19 adaptations / 1 rejected; 506 entries, 6318 / 191; ledger 4579 rows, 2722 / 39 / 760 / 1058 / 0) | same |
| c-content | 904 | 904 |
| c-admission | 2244 | 2244 |
| c-runlayer | 244 | 244 |
| c-acts | 181 (A6 digest 9F91C1FE52A4B998) | 181 |
| c-deckops | 174 (L1 digest 5169BABFF3951AA2) | 174 |
| c-shrine-core | 131 | 131 |
| c-enc-model | 141 | 141 |
| c-enc-engine | 122 | 122 |
| c-bosspilot-encounter enc01 | 20 games, boss wins 15, 0 faults / fallbacks / failed checks / swallowed, power-unused 0, form-unreached 1 | same |

- Every suite and enc01 ran twice per lane after the fix. Each complete log is line-for-line equal to main's last log (run-p8f01b-2.txt, p8f01b/bp-enc01-a.txt): 0 differing lines, stderr empty.
- The second runs happened while the machine was 2.5-4 times slower (c-acts 138-207 s instead of 44-56 s) and still matched.
- Also in lane 1: FAMILY=01 gave 10 games twice identical; RULE=DALA_850 on three bosses gave 0 differing lines against main's log.
- The engine dll is byte-identical in all lanes and main (md5 6d5a9d46d3d4911aa67ebc49a9ed0890), so dll bytes cannot show which engine a suite used; the compiler-input check below does.

=====================================================================
4. ISOLATION PROOF
=====================================================================
- **Probe, in each lane in turn:** Elder Brandlemar renamed to "(lane k probe)" with healthOffset 1 in the lane's data/dungeon-run/reference/encounters/F01.json. The lane generator wrote the marked row into the lane's encounters-v1.json and the lane harness played "health 31". Main, LB and the other two lanes showed 0 occurrences; main's own harness played "health 30"; `status` stayed at 0. `diff k` showed exactly take-lane for F01.json and generated-skip for encounters-v1.json. Restored byte for byte, regenerated, `diff k` empty again.
- **Compiler inputs** (c-bosspilot-encounter built with -v n in each lane): 116 source files under L<k>\repo, engine reference L<k>\sp\audit-verify\engine-out, 0 inputs from main or another lane.
- **Logs:** 0 lines name the main repo or the unlaned scratchpad, 174 name the own lane, 0 another lane.
- **Sources:** an independent scan of each lane (9984 files) finds 2 files that still name a main root, the launcher tests. A second scan finds no lane-rooted path that is missing in the lane apart from the 11 not-mirrored-on-purpose items.
- **Ambient discovery:** git inside a lane repo fails ("not a git repository"); there is no Directory.Build.props, nuget.config or global.json above the lane; global.json is in the lane copy.
- **Main untouched:** 0 files or folders in C:\SoloStone, and 0 files in main's mirrored scratch folders including obj/bin/out, are newer than the start marker.

=====================================================================
5. HARNESS LOCK
=====================================================================
hrun.sh works from lane folders across separate shell calls:
- Lane 1 held the lock; a call from lane 2 with HRUN_WAIT=6 returned 75 and named the holder ("69488 16:48:19 scratchpad/L1 sleep 40").
- A call with the default wait ran after 32 s.
- A dead holder's lock is taken over; the command's exit code passes through.
All my harness runs went through it. One limit, stated in the guide: if a shell call is cut off, the harness exe can outlive the released lock.

=====================================================================
6. ADDED FOR THE LANES
=====================================================================
- P\lane.sh `<k> <NN> engine|plugin|gen|build|run <n>|bp <name> <n> VAR=..|same <name>`: the guide's commands as one shared script. It writes only inside L<k>, takes the lock per harness run, and uses never-merged log names. Tested end to end in lane 1; lanes are told not to edit it.
- P\verify\: lv.sh (re-verification with logs outside the lane), probe.sh, gapscan.py, mainscan.py, and all logs under verify\L1..L3.

=====================================================================
7. TIMINGS (quiet machine; all in the guide)
=====================================================================
| Step | Time |
|---|---|
| Engine build | 5-8 s |
| Plugin build | 9-14 s |
| Generators (encounters / catalog / ledger) | 10-12 s / 5 s / 11-13 s |
| Nine suite builds | about 65 s |
| Eight suites | 85-114 s (229-341 s busy) |
| enc01 | 36-47 s (132-134 s busy) |
| FAMILY=01 | 27-29 s |

=====================================================================
8. GOTCHAS FOR THE ORCHESTRATOR
=====================================================================
- On a busy machine all eight suites plus lock waits can pass the ten-minute limit of one foreground call; the guide tells lanes to slice the run.
- A log not named build-*/run-*/err-* in a mirrored folder is merged. The lane copies of p8f01\bp.sh and p8f01b\bp.sh overwrite mirrored logs (`<mode>-p8f01b-<n>.txt`) and must not be run as they are.
- curation.json: every lane's encounters step rewrites the two tool-owned lists, so expect overlapping edits at merge. Rerunning the encounters step in main rebuilds them.
- Bash heredocs halve backslashes; write scripts with the Write tool.
- Lane leftovers: only build output (352 files in 26 obj/bin/out folders per lane, 406 in 30 in lane 3) and stale folders from syncs. An idle VBCSCompiler server from my last build is still running; I left it alone. No lock file is left.
