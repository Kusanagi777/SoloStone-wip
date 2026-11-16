# Phase 8 family 14: writes fixtures/encounters-f14.json = the generated data/dungeon-run/encounters-v1.json plus two fixture
# rows (ids fix14_*; family "14f"; never shipped), so the fair AI plays each boss card of the family from hand as a boss through
# the run setup path whatever the shipped rows' seeds deal:
#   fix14_dazzik       Dazzik "Hellscream" with "Scourgelord" Dazzik (DALA_BOSS_06dk): one in hand after the mulligan, three
#                      in the deck (the shipped row shuffles one into a 31-card deck, which a short game may never draw)
#   fix14_akazamzarak  The Great Akazamzarak with three copies of each magical feat (DALA_BOSS_03t to 03t4) in the deck and
#                      Reductomara in hand
# Usage: python3 -I verify/tools/gen/p8f14/make_fixture.py [<repo> <out file>]     (LF, no BOM; deterministic)
# (defaults: the repository this file sits in and verify/fixtures/p8f14/fixtures/encounters-f14.json)
import json, os, sys, collections
V = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))  # verify/, from verify/tools/gen/<family>/

def deck(*pairs):
    out = []
    for cid, n in pairs: out += [cid] * n
    if len(out) != 30: raise SystemExit("a fixture deck has %d cards" % len(out))
    return out

SRC = {"url": "fixture (phase 8 family 14; never shipped)", "read": "2026-11-14", "list": "fixture"}
# Chillwind Yeti, River Crocolisk, Bloodfen Raptor, Elven Archer, Shield Block, Execute (the Warrior's plain cards)
ROWS = [
    ("fix14_dazzik", "Fixture 14 A (Dazzik with \"Scourgelord\" Dazzik)", "06", "warrior", ["DALA_BOSS_06dk"],
     deck(("DALA_BOSS_06dk", 3), ("CS2_182", 6), ("CS2_120", 6), ("CS2_172", 6), ("EX1_606", 5), ("CS2_108", 4))),
    ("fix14_akazamzarak", "Fixture 14 B (The Great Akazamzarak with the four magical feats)", "03", "mage", ["DALA_BOSS_03t"],
     deck(("DALA_BOSS_03t", 3), ("DALA_BOSS_03t2", 3), ("DALA_BOSS_03t3", 3), ("DALA_BOSS_03t4", 3), ("CS2_182", 6), ("CS2_120", 6), ("CS2_172", 6))),
]

def row(rid, name, n, hand):
    return collections.OrderedDict([
        ("id", rid), ("name", name), ("provenance", "DALA"), ("family", "14f"), ("hero", "DALA_BOSS_%sh" % n), ("power", "DALA_BOSS_%sp" % n),
        ("tier", "3"), ("archetype", "midrange"), ("level", {"source": "wiki-level-table", "original": 6}),
        ("flags", ["start"]), ("start", collections.OrderedDict([("bossHandAdd", hand)])),
        ("deck", collections.OrderedDict([("id", "enc-" + rid), ("kind", "curated"), ("source", SRC)])),
    ])

def main(repo, out):
    data = json.load(open(repo + "/data/dungeon-run/encounters-v1.json", encoding="utf-8"), object_pairs_hook=collections.OrderedDict)
    for rid, name, n, _, hand, cards in ROWS:
        r = row(rid, name, n, hand)
        if any(e["id"] == r["id"] for e in data["encounters"]): raise SystemExit(r["id"] + " exists")
        data["encounters"].append(r)
        data["decks"][r["deck"]["id"]] = list(cards)
    data["generatedBy"] = "p8f14/make_fixture.py over tools/dungeon-run-catalog (encounters); a test fixture, never shipped"
    text = json.dumps(data, indent=1, ensure_ascii=False) + "\n"
    os.makedirs(os.path.dirname(out), exist_ok=True)
    open(out, "w", encoding="utf-8", newline="\n").write(text)
    print("wrote %s: %d rows (%d fixture), %d decks" % (out, len(data["encounters"]), len(ROWS), len(data["decks"])))

main(sys.argv[1] if len(sys.argv) > 2 else os.path.dirname(V),
     sys.argv[2] if len(sys.argv) > 2 else V + '/fixtures/p8f14/fixtures/encounters-f14.json')
