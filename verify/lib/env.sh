# verify/lib/env.sh: sourced by every runner in verify/bin. Finds the repository from this file's own location, so a
# runner works from any current directory, in any clone, on Linux (bash) and on Windows (Git Bash). No absolute path,
# no drive letter, no cygpath.
#   VERIFY    verify/                       ROOT      the repository root (holds engine/, plugin/, data/)
#   SUITES    verify/suites                 FIX       verify/fixtures          BASE   verify/baseline
#   OUT       verify/.out (gitignored)      ENGINE_DLL  verify/.out/engine-out/SabberStoneCore.dll
#   UP        ../../..  = verify/, seen from a suite folder (every suite sits at verify/suites/<origin>/<name>)
VERIFY="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ROOT="$(cd "$VERIFY/.." && pwd)"
SUITES="$VERIFY/suites"
FIX="$VERIFY/fixtures"
BASE="$VERIFY/baseline"
OUT="$VERIFY/.out"
ENGINE_OUT="$OUT/engine-out"
ENGINE_DLL="$ENGINE_OUT/SabberStoneCore.dll"
UP=../../..
mkdir -p "$OUT/builds" "$OUT/runs" "$OUT/bp/logs" "$OUT/hall" "$OUT/tmp"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

# suite_dir <suites-relative path, e.g. p8f01/c-enc-model> -> the absolute folder, or nothing when no .csproj is there
# yet (a suite not ported yet: the runners print NOT PORTED for it and go on)
suite_dir() { ls "$SUITES/$1"/*.csproj >/dev/null 2>&1 && echo "$SUITES/$1"; }
# last_lines <file> <n>: the last n lines with any CR removed
last_lines() { tail -n "$2" "$1" 2>/dev/null | tr -d '\r'; }
# need_engine: stop when bin/engine.sh has not been run
need_engine() {
  [ -f "$ENGINE_DLL" ] || { echo "no engine build at verify/.out/engine-out: run bash verify/bin/engine.sh first" >&2; exit 3; }
}
