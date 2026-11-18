# Phase 8 family 24: writes verify/fixtures/p8f24/fixtures/schedule-tekahn.json, the schedule `bp.sh tekahn24` walks: acts-v3t
# with its finale enabled on Tekahn and the hero relay on (family 01's schedule-relay.json pattern).
# Until the close-out (family 25) the shipped finale stayed disabled and this generator enabled it by a text edit of the one
# line. The close-out ships the finale exactly as this fixture had it, so the fixture is now the shipped
# data/dungeon-run/schedule-v1.json byte for byte; the generator checks the shipped finale block is the one family 24 built.
# Usage: python3 -I verify/tools/gen/p8f24/make_fixture.py [<repo> <out file>]     (LF, no BOM; deterministic)
import os, sys, json
V = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))  # verify/
repo = sys.argv[1] if len(sys.argv) > 2 else os.path.dirname(V)
out = sys.argv[2] if len(sys.argv) > 2 else os.path.join(V, "fixtures", "p8f24", "fixtures", "schedule-tekahn.json")
text = open(os.path.join(repo, "data", "dungeon-run", "schedule-v1.json"), encoding="utf-8").read()
finale = json.loads(text)["schedules"]["acts-v3t"]["finale"]
expected = {"enabled": True, "fixedBosses": ["ulda_67_tekahn"], "heroRelay": True, "tavernBefore": True, "id": "IV", "name": "The Inner Sanctum",
            "bossHealth": [70], "bossMana": [3], "tavern": {"gold": 3, "offers": 4}}
if finale != expected: raise SystemExit("schedule-v1.json: acts-v3t's finale is %r, not family 24's Tekahn finale" % finale)
os.makedirs(os.path.dirname(out), exist_ok=True)
open(out, "w", encoding="utf-8", newline="\n").write(text)
print("wrote %s" % out)
