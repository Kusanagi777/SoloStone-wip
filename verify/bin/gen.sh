#!/bin/bash
# verify/bin/gen.sh: the three generators, in this order: encounters, catalog, ledger (lane guide section 2.3), built
# against verify/.out/engine-out and run from the repository root. They WRITE into the repository:
#   encounters  data/dungeon-run/encounters-v1.json; encounterAdaptations / encounterRejections in tools/restoration-ledger/curation.json
#   catalog     data/dungeon-run/catalog-v1.json, docs/design/dungeon-run-catalog.md
#   ledger      docs/design/restoration-ledger.csv, docs/design/restoration-ledger.md
# On an unchanged tree all six files come out byte-identical (wave 3: encounters "69 rows, 73 decks (43 authentic, 30
# changed), 147 adaptations, 1 rejected"; catalog "506 entries ... 6508 admitted and 1 excluded"; ledger "rows 4704,
# implemented 2813, partial 295, missing 538, duplicate/support 1058"). After a data change, run this, then rebuild
# the family fixtures (README.md), then the suites. Output: verify/.out/gen-<step>.txt; the last line is `git status`
# of the six files (empty = nothing changed).
. "$(dirname "$0")/../lib/env.sh"
need_engine
for t in dungeon-run-catalog restoration-ledger; do
  (cd "$ROOT/tools/$t" && dotnet build -c Release -nologo -v q -o ../../verify/.out/tools/$t \
     -p:EngineDll=../../verify/.out/engine-out/SabberStoneCore.dll > "$OUT/builds/tool-$t.txt" 2>&1)
  echo "tool $t build exit=$? $(grep -E 'Error\(s\)' "$OUT/builds/tool-$t.txt" | tr -d '\r' | tr -s ' ' | tr '\n' ' ')"
done
cd "$ROOT" || exit 1
dotnet verify/.out/tools/dungeon-run-catalog/DungeonRunCatalogTool.dll encounters > "$OUT/gen-encounters.txt" 2>&1
echo "encounters exit=$? $(grep -E '^encounters:' "$OUT/gen-encounters.txt" | tr -d '\r')"
dotnet verify/.out/tools/dungeon-run-catalog/DungeonRunCatalogTool.dll > "$OUT/gen-catalog.txt" 2>&1
echo "catalog exit=$? $(grep -E 'catalog-v1.json:' "$OUT/gen-catalog.txt" | sed 's/.*catalog-v1.json: //' | tr -d '\r')"
dotnet verify/.out/tools/restoration-ledger/RestorationLedger.dll > "$OUT/gen-ledger.txt" 2>&1
echo "ledger exit=$? $(grep -E '^(rows|  [a-z/ ]+):' "$OUT/gen-ledger.txt" | tr -d '\r' | tr '\n' ' ' | tr -s ' ')"
if command -v git >/dev/null 2>&1; then
  echo "git status of the six generated files: [$(git status --short -- data/dungeon-run/encounters-v1.json tools/restoration-ledger/curation.json \
    data/dungeon-run/catalog-v1.json docs/design/dungeon-run-catalog.md docs/design/restoration-ledger.csv docs/design/restoration-ledger.md | grep -v '^[AM]  ' | tr '\n' ' ')]"
fi
