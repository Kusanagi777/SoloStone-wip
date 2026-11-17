#!/bin/bash
# verify/tools/gen/p8f25/record_replay.sh <scratch folder>: records c-enc-25 section L's fixtures on the pre-close-out tree.
# Exports commit BASE (default 68e7163, the wave-6 baseline: the last tree whose new runs followed acts-v2 by default) with
# `git archive` into <scratch folder>/tree-<BASE> (an export, not a worktree), builds the recorder there from Record.cs and
# the suite's own verify/suites/p8f25/c-enc-25/Replay.cs against the export's plugin sources, and runs it: it writes
# verify/fixtures/p8f25/pre-closeout-1..8.xml and replay.txt of THIS checkout. The engine is built in the export unless its
# engine/ equals this checkout's (then this checkout's verify/.out/engine-out is copied). Run it through the harness lock.
# Deterministic: a second run writes the same bytes.
set -e
HERE="$(cd "$(dirname "$0")" && pwd)"; V="$(cd "$HERE/../../.." && pwd)"; REPO="$(dirname "$V")"
BASE=${BASE:-68e7163}
[ -n "$1" ] || { echo "usage: record_replay.sh <scratch folder>"; exit 2; }
X="$1/tree-$BASE"
if [ ! -d "$X/plugin" ]; then mkdir -p "$X"; git -C "$REPO" archive "$BASE" | tar -x -C "$X"; fi
mkdir -p "$X/verify/.out/engine-out"
if git -C "$REPO" diff --quiet "$BASE" -- engine; then cp "$V/.out/engine-out/"* "$X/verify/.out/engine-out/"
else dotnet build "$X/engine/SabberStoneCore/SabberStoneCore.csproj" -c Release -f net10.0 -nologo -v q -o "$X/verify/.out/engine-out" -p:UseSharedCompilation=false; fi
P="$X/verify/suites/p8f25/record"; mkdir -p "$P"
cp "$HERE/Record.cs" "$P/Program.cs"; cp "$HERE/Record.csproj" "$P/Record.csproj"; cp "$V/suites/p8f25/c-enc-25/Replay.cs" "$P/Replay.cs"
(cd "$P" && dotnet build -c Release -nologo -v q -o out -p:UseSharedCompilation=false > build.txt 2>&1) || { cat "$P/build.txt"; exit 1; }
(cd "$P" && dotnet out/AiHarness.dll "$V/fixtures/p8f25")
