# Phase 8 family 06: writes p8f06/fixtures/schedule-plagues.json, the shipped schedule-v1.json with the four Tombs of
# Terror plague twists in every twist pool of acts-v3h (chance 100), so the twist roll is tested end to end (c-rules-06
# W23-W25) and the bosspilot harness can walk a run that rolls them. The shipped acts keep empty twist pools until the
# close-out fills them. Rerun after schedule-v1.json changes.
#   python -I verify/tools/gen/p8f06/make_fixture.py [<output file>]
# (ported to verify/: reads the repository this file sits in; writes verify/fixtures/p8f06/fixtures/schedule-plagues.json by default)
import io, os, sys
V = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))  # verify/, from verify/tools/gen/<family>/
repo = os.path.dirname(V)
s = io.open(repo + '/data/dungeon-run/schedule-v1.json', encoding='utf-8', newline='').read()
old = '"twist": {"pool": [], "chance": 0},'
if s.count(old) != 3: raise SystemExit('expected three empty twist pools in acts-v3h, found %d: the close-out filled them; point c-rules-06 at the shipped schedule' % s.count(old))
s = s.replace(old, '"twist": {"pool": ["ULDA_801", "ULDA_802", "ULDA_803", "ULDA_804"], "chance": 100},')
out = sys.argv[1] if len(sys.argv) > 1 else V + '/fixtures/p8f06/fixtures/schedule-plagues.json'
os.makedirs(os.path.dirname(out), exist_ok=True)
io.open(out, 'w', encoding='utf-8', newline='').write(s)
print('wrote', out)
