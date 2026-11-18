#!/bin/bash
# verify/bin/builds.sh: the build stage (wave-3 p8w3/builds.sh), against verify/.out/engine-out. No harness runs, no lock.
#   builds.sh list              the 84 suite projects (the wave-3 order, then waves 4, 5 and 6 and the close-out)
#   builds.sh <from> <to>       build entries <from>..<to> (1-based)
#   builds.sh all               all 84
#   builds.sh pilot             the pilot set (lib/suites.sh PILOT_LIST)
#   builds.sh only <dir>...     the named suites (paths relative to verify/suites, e.g. p8f01/c-enc-model)
#   builds.sh randplay          randplay (not in the 83)
#   builds.sh tool              the catalog tool c-catalog runs (tools/dungeon-run-catalog -> verify/.out/tools/...)
# Build logs: verify/.out/builds/<name>.txt. Each line: "<i> <name> build exit=<rc> <n> Error(s)", plus a WARNING when the
# suite's out/SabberStoneCore.dll is not the engine build. A suite that has no .csproj under verify/suites yet prints NOT PORTED.
. "$(dirname "$0")/../lib/env.sh"
. "$VERIFY/lib/suites.sh"

build_one() { # build_one <number> <suites-relative dir>
  local i=$1 rel=$2 name d rc note
  name=$(basename "$rel")
  d=$(suite_dir "$rel") || { echo "$i $name NOT PORTED (no verify/suites/$rel/*.csproj)"; return 0; }
  (cd "$d" && dotnet build --no-incremental -c Release -nologo -v q -o out > "$OUT/builds/$name.txt" 2>&1); rc=$?
  note=""
  [ -f "$d/out/SabberStoneCore.dll" ] && ! cmp -s "$d/out/SabberStoneCore.dll" "$ENGINE_DLL" && note=" WARNING: out/SabberStoneCore.dll is not the verify/.out/engine-out build"
  echo "$i $name build exit=$rc $(grep -E 'Error\(s\)' "$OUT/builds/$name.txt" | tr -d '\r' | tr -s ' ' | tr '\n' ' ')$note"
}

case "$1" in
list) i=0; for rel in $BUILD_LIST; do i=$((i+1)); echo "$i $rel"; done; exit 0 ;;
tool)
  need_engine
  cd "$ROOT/tools/dungeon-run-catalog" || exit 1
  dotnet build -c Release -nologo -v q -o ../../verify/.out/tools/dungeon-run-catalog \
    -p:EngineDll=../../verify/.out/engine-out/SabberStoneCore.dll > "$OUT/builds/tool-dungeon-run-catalog.txt" 2>&1
  echo "catalog tool build exit=$? $(grep -E 'Error\(s\)' "$OUT/builds/tool-dungeon-run-catalog.txt" | tr -d '\r' | tr -s ' ' | tr '\n' ' ')"
  exit 0 ;;
randplay) need_engine; build_one 0 audit-verify/randplay; exit 0 ;;
pilot) need_engine; for rel in $PILOT_LIST; do build_one "-" "$rel"; done; exit 0 ;;
only) need_engine; shift; for rel in "$@"; do build_one "-" "$rel"; done; exit 0 ;;
all) set -- 1 999 ;;
esac
[ -n "$2" ] || { sed -n 2,13p "$0"; exit 2; }
need_engine
i=0
for rel in $BUILD_LIST; do
  i=$((i+1)); { [ $i -lt "$1" ] || [ $i -gt "$2" ]; } && continue
  build_one $i "$rel"
done
