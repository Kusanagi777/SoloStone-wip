# Phase 8 family 17: writes fixtures/encounters-f17.json = the generated data/dungeon-run/encounters-v1.json plus five fixture
# rows (ids fix17_*; family "17f"; never shipped), so the fair AI plays each boss card of the family from hand as a boss through
# the run setup path whatever the shipped rows' seeds deal (one copy in hand after the mulligan, more in a 30-card deck):
#   fix17_zuramat      Zuramat the Obliterator with Void Shift (DALA_BOSS_48t)
#   fix17_millificent  Millificent Manastorm with Thorium Chicken (DALA_BOSS_51t2) and Kirin Tor Guard (DALA_503)
#   fix17_wagtoggle    Queen Wagtoggle with Rival Accomplice (DALA_BOSS_56t)
#   fix17_goya         Madam Goya with Fruit Slasher (DALA_BOSS_59t2) and Lotus Bruiser (DALA_BOSS_59t)
#   fix17_gallywix     Trade Prince Gallywix with Infuriated Banker (DALA_502)
# Usage: python3 -I verify/tools/gen/p8f17/make_fixture.py [<repo> <out file>]     (LF, no BOM; deterministic)
# (defaults: the repository this file sits in and verify/fixtures/p8f17/fixtures/encounters-f17.json)
import json, os, sys, collections
V = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))  # verify/, from verify/tools/gen/<family>/

def deck(*pairs):
    out = []
    for cid, n in pairs: out += [cid] * n
    if len(out) != 30: raise SystemExit("a fixture deck has %d cards" % len(out))
    return out

SRC = {"url": "fixture (phase 8 family 17; never shipped)", "read": "2026-11-14", "list": "fixture"}
# Chillwind Yeti, River Crocolisk, Bloodfen Raptor, Wisp (plain neutral cards)
PLAIN = (("CS2_182", 6), ("CS2_120", 6), ("CS2_172", 6))
ROWS = [
    ("fix17_zuramat", "Fixture 17 A (Zuramat with Void Shift)", "48", ["DALA_BOSS_48t"], deck(("DALA_BOSS_48t", 6), ("CS2_231", 6), *PLAIN)),
    ("fix17_millificent", "Fixture 17 B (Millificent with Thorium Chicken and Kirin Tor Guard)", "51", ["DALA_BOSS_51t2", "DALA_503"],
     deck(("DALA_BOSS_51t2", 6), ("DALA_503", 6), *PLAIN)),
    ("fix17_wagtoggle", "Fixture 17 C (Queen Wagtoggle with Rival Accomplice)", "56", ["DALA_BOSS_56t"], deck(("DALA_BOSS_56t", 6), ("CS2_231", 6), *PLAIN)),
    ("fix17_goya", "Fixture 17 D (Madam Goya with Fruit Slasher and Lotus Bruiser)", "59", ["DALA_BOSS_59t2", "DALA_BOSS_59t"],
     deck(("DALA_BOSS_59t2", 6), ("DALA_BOSS_59t", 6), *PLAIN)),
    ("fix17_gallywix", "Fixture 17 E (Trade Prince Gallywix with Infuriated Banker)", "57", ["DALA_502"], deck(("DALA_502", 6), ("CS2_231", 6), *PLAIN)),
]

def row(rid, name, n, hand):
    return collections.OrderedDict([
        ("id", rid), ("name", name), ("provenance", "DALA"), ("family", "17f"), ("hero", "DALA_BOSS_%sh" % n), ("power", "DALA_BOSS_%sp" % n),
        ("tier", "3"), ("archetype", "midrange"), ("level", {"source": "wiki-level-table", "original": 6}),
        ("flags", ["start"]), ("start", collections.OrderedDict([("bossHandAdd", hand)])),
        ("deck", collections.OrderedDict([("id", "enc-" + rid), ("kind", "curated"), ("source", SRC)])),
    ])

def main(repo, out):
    data = json.load(open(repo + "/data/dungeon-run/encounters-v1.json", encoding="utf-8"), object_pairs_hook=collections.OrderedDict)
    for rid, name, n, hand, cards in ROWS:
        r = row(rid, name, n, hand)
        if any(e["id"] == r["id"] for e in data["encounters"]): raise SystemExit(r["id"] + " exists")
        data["encounters"].append(r)
        data["decks"][r["deck"]["id"]] = list(cards)
    data["generatedBy"] = "p8f17/make_fixture.py over tools/dungeon-run-catalog (encounters); a test fixture, never shipped"
    text = json.dumps(data, indent=1, ensure_ascii=False) + "\n"
    os.makedirs(os.path.dirname(out), exist_ok=True)
    open(out, "w", encoding="utf-8", newline="\n").write(text)
    print("wrote %s: %d rows (%d fixture), %d decks" % (out, len(data["encounters"]), len(ROWS), len(data["decks"])))

main(sys.argv[1] if len(sys.argv) > 2 else os.path.dirname(V),
     sys.argv[2] if len(sys.argv) > 2 else V + '/fixtures/p8f17/fixtures/encounters-f17.json')
