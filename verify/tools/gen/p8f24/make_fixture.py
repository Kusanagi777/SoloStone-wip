# Phase 8 family 24: writes verify/fixtures/p8f24/fixtures/schedule-tekahn.json = the shipped data/dungeon-run/schedule-v1.json
# with the acts-v3t finale enabled on Tekahn and its hero relay on (family 01's schedule-relay.json pattern; a test fixture,
# never shipped: the shipped finale stays disabled until the close-out). Only the finale line of acts-v3t changes.
# Usage: python3 -I verify/tools/gen/p8f24/make_fixture.py [<repo> <out file>]     (LF, no BOM; deterministic)
import os, sys, json
V = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))  # verify/
repo = sys.argv[1] if len(sys.argv) > 2 else os.path.dirname(V)
out = sys.argv[2] if len(sys.argv) > 2 else os.path.join(V, "fixtures", "p8f24", "fixtures", "schedule-tekahn.json")
text = open(os.path.join(repo, "data", "dungeon-run", "schedule-v1.json"), encoding="utf-8").read()
head = '    "acts-v3t": {\n'
i = text.index(head)
old = '"finale": {"enabled": false, "fixedBosses": [], "heroRelay": false, "tavernBefore": true,'
j = text.index(old, i)
if j - i > 300: raise SystemExit("acts-v3t finale not where expected")
text = text[:j] + '"finale": {"enabled": true, "fixedBosses": ["ulda_67_tekahn"], "heroRelay": true, "tavernBefore": true,' + text[j + len(old):]
doc = json.loads(text)
assert doc["schedules"]["acts-v3t"]["finale"]["fixedBosses"] == ["ulda_67_tekahn"]
os.makedirs(os.path.dirname(out), exist_ok=True)
open(out, "w", encoding="utf-8", newline="\n").write(text)
print("wrote %s" % out)
