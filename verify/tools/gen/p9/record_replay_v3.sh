#!/bin/bash
# verify/tools/gen/p9/record_replay_v3.sh <scratch folder> [<output folder>]: records c-enc-25 section L4's fixtures
# (step 9, handoff/phase9/plan.md D5) on the tree before step 9. Exports commit BASE (default f62275b, the close-out tip:
# acts-v3h and acts-v3t as released, before any step-9 change) with `git archive` into <scratch folder>/tree-<BASE> (an
# export, not a worktree), builds that tree's engine there unless its engine/ equals this checkout's (then this checkout's
# verify/.out/engine-out is copied), builds the recorder from Record9.cs and the suite's frozen
# verify/suites/p8f25/c-enc-25/ReplayV3.cs against the export's own plugin sources, and runs it on the export's data. It
# writes the eight saves v3-<n>-<stop>.xml, replay.txt and exclusions.txt (the export's exclusion data) into <output
# folder> (default verify/fixtures/p9/replay-v3 of THIS checkout; never p8f25). Run it through the harness lock.
# Deterministic: a second run writes the same bytes.
set -e
HERE="$(cd "$(dirname "$0")" && pwd)"; V="$(cd "$HERE/../../.." && pwd)"; REPO="$(dirname "$V")"
BASE=${BASE:-f62275b}
[ -n "$1" ] || { echo "usage: record_replay_v3.sh <scratch folder> [<output folder>]"; exit 2; }
X="$1/tree-$BASE"
DEST="$(realpath -m "${2:-$V/fixtures/p9/replay-v3}")"
if [ ! -d "$X/plugin" ]; then mkdir -p "$X"; git -C "$REPO" archive "$BASE" | tar -x -C "$X"; fi
mkdir -p "$X/verify/.out/engine-out"
if git -C "$REPO" diff --quiet "$BASE" -- engine; then cp "$V/.out/engine-out/"* "$X/verify/.out/engine-out/"
elif [ ! -f "$X/verify/.out/engine-out/SabberStoneCore.dll" ]; then
  dotnet build "$X/engine/SabberStoneCore/SabberStoneCore.csproj" -c Release -f net10.0 -nologo -v q -o "$X/verify/.out/engine-out" \
    -p:UseSharedCompilation=false > "$X/verify/.out/engine-build.txt" 2>&1 || { tail -n 5 "$X/verify/.out/engine-build.txt"; exit 1; }
fi
P="$X/verify/suites/p9/record9"; rm -rf "$P"; mkdir -p "$P"
cp "$HERE/Record9.cs" "$P/Program.cs"; cp "$HERE/Record9.csproj" "$P/Record9.csproj"; cp "$V/suites/p8f25/c-enc-25/ReplayV3.cs" "$P/ReplayV3.cs"
(cd "$P" && dotnet build -c Release -nologo -v q -o out -p:UseSharedCompilation=false > build.txt 2>&1) || { grep -E 'error' "$P/build.txt" | sort -u | head; exit 1; }
mkdir -p "$DEST"
(cd "$P" && dotnet out/AiHarness.dll "$DEST")
echo "replay.txt $(md5sum < "$DEST/replay.txt" | cut -c1-32) $DEST"
