#!/bin/bash
# hrun.sh <command...>   run a test harness (or a script of harness runs) while holding the cross-lane harness lock.
# Why: the AI search budgets have wall-clock limits, so a harness that shares the machine with another harness can
# stop a search on the clock and play differently. Builds and generators do not need the lock.
# Exit 75 = the lock stayed busy for HRUN_WAIT seconds (default 240) and NOTHING was run: call again.
# Keep one locked call under about 6 minutes so other lanes get a turn (split long runs into slices).
LOCK="/c/Users/user/AppData/Local/Temp/claude/0a564ffc8743aeec/d2a4e245-ca37-4433-9b55-c35ed325f081/scratchpad/p8lanes/harness.lock"
WAIT=${HRUN_WAIT:-240}
[ $# -eq 0 ] && { echo "usage: hrun.sh <command...>" >&2; exit 2; }
me="$$ $(date +%H:%M:%S) $(pwd | grep -o 'scratchpad/L[0-9B]*' | head -n 1) $*"
t=0
while :; do
  if ( set -o noclobber; echo "$me" > "$LOCK" ) 2>/dev/null; then break; fi
  holder=$(cut -d' ' -f1 "$LOCK" 2>/dev/null)
  if [ -n "$holder" ] && ! kill -0 "$holder" 2>/dev/null; then
    # the holder is gone (killed by a timeout): take the lock over, unless someone else just did
    [ "$(cut -d' ' -f1 "$LOCK" 2>/dev/null)" = "$holder" ] && rm -f "$LOCK"
    continue
  fi
  if [ $t -ge $WAIT ]; then
    echo "hrun: harness lock busy for ${WAIT}s (held by: $(cut -c1-160 "$LOCK" 2>/dev/null)). Nothing was run; call again." >&2
    exit 75
  fi
  sleep 2; t=$((t+2))
done
trap 'rm -f "$LOCK"' EXIT
"$@"
