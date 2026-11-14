#!/bin/bash
# verify/bin/engine.sh: builds the engine (net10.0, Release) into verify/.out/engine-out, the one engine build every suite,
# the catalog tool and randplay reference. Run it first, and again after every engine edit. Never run two at once: it
# shares engine/SabberStoneCore/obj with any other engine or plugin build of this clone.
# Expect "0 Warning(s) 0 Error(s)". The wave-3 engine build was byte-identical across machines (the engine maps its source
# paths): md5 07028f461d526950718e3df4c8401d8d on the export copy, the same as the wave-3 main tree.
. "$(dirname "$0")/../lib/env.sh"
cd "$ROOT" || exit 1
dotnet build engine/SabberStoneCore/SabberStoneCore.csproj -c Release -f net10.0 -nologo -o verify/.out/engine-out > "$OUT/builds/engine.txt" 2>&1
rc=$?
echo "engine build exit=$rc $(grep -E 'Warning\(s\)|Error\(s\)' "$OUT/builds/engine.txt" | tr -d '\r' | tr -s ' ' | tr '\n' ' ')"
if command -v md5sum >/dev/null 2>&1; then echo "engine md5 $(md5sum < "$ENGINE_DLL" | cut -c1-32)"; fi
exit $rc
