#!/bin/bash
# verify/bin/build-all.sh: the plugin-free build of everything the verification needs, in one call: the engine, the catalog
# tool, the 78 suite projects, randplay and the 39 audit hall projects. Nothing here needs the Hearthstone client
# libraries (references/): the plugin itself (plugin/OfflineProbe.csproj) is NOT built; the suites compile the plugin
# sources they test directly. Runs no harness, so it needs no lock.
# Expect: engine 0 warnings / 0 errors; every other line "build exit=0 0 Error(s)" (or NOT PORTED for a suite not ported yet).
. "$(dirname "$0")/../lib/env.sh"
. "$VERIFY/lib/suites.sh"
B="$VERIFY/bin"
bash "$B/engine.sh" || exit 1
bash "$B/builds.sh" tool
bash "$B/builds.sh" all
bash "$B/builds.sh" randplay
for rel in $HALL_BASE $(for n in $HALL_LIST; do echo hall/$n; done); do
  name=$(basename "$rel"); d=$(suite_dir "$rel") || { echo "hall $name NOT PORTED"; continue; }
  (cd "$d" && dotnet build -c Release -nologo -v q -o out > "$OUT/builds/hall-$name.txt" 2>&1); rc=$?
  echo "hall $name build exit=$rc $(grep -E 'Error\(s\)' "$OUT/builds/hall-$name.txt" | tr -d '\r' | tr -s ' ' | tr '\n' ' ')"
done
