#!/bin/bash
# verify/tools/gen/p9/record_sched9.sh [<commit>] [<output>]: records c-sched-9's reference (step 9 F1, plan D1): the parsed
# released objects of the tree before step 9. Exports <commit> (default f62275b, the close-out tip, before any step-9 change)
# to verify/.out/tmp/sched9-ref, builds that tree's engine there, compiles c-sched-9's Dump9.cs (which uses only that tree's
# APIs) with a one-line main against the export's own plugin sources, and runs it on the export's data. Output: default
# verify/fixtures/p9/sched9-f62275b.txt. Run it twice: the two outputs are byte-identical. Wrap it in the harness lock.
. "$(dirname "$0")/../../../lib/env.sh"
REF=${1:-f62275b}
DEST=$(realpath -m "${2:-$FIX/p9/sched9-$REF.txt}")
X=$OUT/tmp/sched9-ref
rm -rf "$X" && mkdir -p "$X" || exit 1
git -C "$ROOT" archive "$REF" | tar -x -C "$X" || exit 1
S=$X/verify/suites/p9/rec-sched9
mkdir -p "$S" "$X/verify/.out/engine-out"
cp "$VERIFY/suites/p9/c-sched-9/Dump9.cs" "$VERIFY/suites/p9/c-sched-9/Sched9.csproj" "$S/" || exit 1
printf 'Sched9.Record(args[0]);\n' > "$S/Record.cs"
( cd "$X" && dotnet build engine/SabberStoneCore/SabberStoneCore.csproj -c Release -f net10.0 -nologo -p:UseSharedCompilation=false \
    -o verify/.out/engine-out > verify/.out/engine-build.txt 2>&1 ) || { tail -n 5 "$X/verify/.out/engine-build.txt"; exit 1; }
( cd "$S" && dotnet build --no-incremental -c Release -nologo -v q -p:UseSharedCompilation=false -o out > build.txt 2>&1 ) || { grep -E 'error' "$S/build.txt" | head; exit 1; }
mkdir -p "$(dirname "$DEST")"
( cd "$S" && dotnet out/AiHarness.dll "$DEST" ) || exit 1
echo "reference $REF: $(md5sum < "$DEST" | cut -c1-32) $DEST"
