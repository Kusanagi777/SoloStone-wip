# Step 9 lane R tests: writes p9/schedule-twists9.json, the shipped schedule-v1.json with every act I-III twist pool of
# acts-v4h (shared by acts-v4t) and of acts-v3h (shared by acts-v3t) holding all eight act twists, so each twist meets the
# bosses of every act (c-rules-9 T: chance, exclusion loss and the pick). acts-v4h's chances become 70 / 85 / 55 (a roll that
# is not certain, so the chance itself is measured); acts-v3h's empty pools get chance 100. DALA_829 The Mint's weight becomes
# 4 (DALA_832 The Carts keeps 1, and the two share every exclusion), so the stamp-4 pick's weighting is measured against the
# released schedules' uniform pick. Rerun after schedule-v1.json changes.
#   python3 -I verify/tools/gen/p9/make_twists9.py [<output file>]
# (reads the repository this file sits in; writes verify/fixtures/p9/schedule-twists9.json by default)
import io, os, sys
V = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))  # verify/, from verify/tools/gen/p9/
repo = os.path.dirname(V)
s = io.open(repo + '/data/dungeon-run/schedule-v1.json', encoding='utf-8', newline='').read()
ALL = '["DALA_829", "DALA_831", "DALA_832", "DALA_833", "ULDA_801", "ULDA_802", "ULDA_803", "ULDA_804"]'
def once(old, new):
    global s
    if s.count(old) != 1: raise SystemExit('expected one %r in schedule-v1.json, found %d: update make_twists9.py' % (old, s.count(old)))
    s = s.replace(old, new)
v3 = '"twist": {"pool": [], "chance": 0},'
if s.count(v3) != 3: raise SystemExit('expected three empty twist pools (acts-v3h), found %d' % s.count(v3))
s = s.replace(v3, '"twist": {"pool": %s, "chance": 100},' % ALL)
once('"twist": {"pool": ["DALA_829", "ULDA_801"], "chance": 100},', '"twist": {"pool": %s, "chance": 70},' % ALL)
once('"twist": {"pool": ["DALA_832", "ULDA_802"], "chance": 100},', '"twist": {"pool": %s, "chance": 85},' % ALL)
once('"twist": {"pool": ["DALA_833", "DALA_831", "ULDA_803", "ULDA_804"], "chance": 100},', '"twist": {"pool": %s, "chance": 55},' % ALL)
once('{"id": "DALA_829", "kind": "twist", "provenance": "DALA", "scope": "act", "effect": "match", "weight": 1,',
     '{"id": "DALA_829", "kind": "twist", "provenance": "DALA", "scope": "act", "effect": "match", "weight": 4,')
out = sys.argv[1] if len(sys.argv) > 1 else V + '/fixtures/p9/schedule-twists9.json'
os.makedirs(os.path.dirname(out), exist_ok=True)
io.open(out, 'w', encoding='utf-8', newline='').write(s)
print('wrote', out)
