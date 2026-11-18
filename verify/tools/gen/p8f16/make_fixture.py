# Phase 8 family 16: writes fixtures/encounters-f16.json = the generated data/dungeon-run/encounters-v1.json plus three fixture
# rows (ids fix16_*; family "16f"; never shipped), so the fair AI plays each boss card of the family and each treasure newly
# admitted for a boss from hand as a boss through the run setup path, whatever the shipped rows' seeds deal:
#   fix16_tipsi   Tipsi Wobblerune with PARTY PORTAL! (DALA_BOSS_38t): two in hand after the mulligan, six in the deck (the
#                 shipped row lists it in a 30-card pool that a short game may never draw)
#   fix16_toomba  Ol' Toomba with three of the five treasures his Tales of Fortune shuffles in that a boss had no row for
#                 before family 16: two Orbs of Destruction and a Portable Forge in hand after the mulligan, Orb of
#                 Destruction, Horn of Cenarius and Portable Forge three each in the deck
#   fix16_quel    Ol' Toomba with the Blade and the Hilt of Quel'Delar in hand after the mulligan (they combine there into
#                 Quel'Delar, LOOTA_842) and two each in the deck, and three Horns of Cenarius
# Usage: python3 -I verify/tools/gen/p8f16/make_fixture.py [<repo> <out file>]     (LF, no BOM; deterministic)
# (defaults: the repository this file sits in and verify/fixtures/p8f16/fixtures/encounters-f16.json)
import json, os, sys, collections
V = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))  # verify/, from verify/tools/gen/<family>/

def deck(*pairs):
    out = []
    for cid, n in pairs: out += [cid] * n
    if len(out) != 30: raise SystemExit("a fixture deck has %d cards" % len(out))
    return out

SRC = {"url": "fixture (phase 8 family 16; never shipped)", "read": "2026-11-14", "list": "fixture"}
# Chillwind Yeti, River Crocolisk, Bloodfen Raptor, Arcane Intellect, Frostbolt (plain cards)
ROWS = [
    ("fix16_tipsi", "Fixture 16 A (Tipsi Wobblerune with PARTY PORTAL!)", "38", ["DALA_BOSS_38t", "DALA_BOSS_38t"],
     deck(("DALA_BOSS_38t", 6), ("CS2_182", 6), ("CS2_120", 6), ("CS2_172", 6), ("CS2_023", 3), ("CS2_024", 3))),
    ("fix16_toomba", "Fixture 16 B (Ol' Toomba with Orb of Destruction, Horn of Cenarius and Portable Forge)", "37",
     ["LOOTA_811", "LOOTA_811", "LOOTA_841"],
     deck(("LOOTA_811", 3), ("LOOTA_837", 3), ("LOOTA_841", 3), ("CS2_182", 7), ("CS2_120", 7), ("CS2_172", 7))),
    ("fix16_quel", "Fixture 16 C (Ol' Toomba with the Blade and the Hilt of Quel'Delar)", "37",
     ["LOOTA_842a", "LOOTA_842b"],
     deck(("LOOTA_842a", 2), ("LOOTA_842b", 2), ("LOOTA_837", 3), ("CS2_182", 8), ("CS2_120", 8), ("CS2_172", 7))),
]

def row(rid, name, n, hand):
    return collections.OrderedDict([
        ("id", rid), ("name", name), ("provenance", "DALA"), ("family", "16f"), ("hero", "DALA_BOSS_%sh" % n), ("power", "DALA_BOSS_%sp" % n),
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
    data["generatedBy"] = "p8f16/make_fixture.py over tools/dungeon-run-catalog (encounters); a test fixture, never shipped"
    text = json.dumps(data, indent=1, ensure_ascii=False) + "\n"
    os.makedirs(os.path.dirname(out), exist_ok=True)
    open(out, "w", encoding="utf-8", newline="\n").write(text)
    print("wrote %s: %d rows (%d fixture), %d decks" % (out, len(data["encounters"]), len(ROWS), len(data["decks"])))

main(sys.argv[1] if len(sys.argv) > 2 else os.path.dirname(V),
     sys.argv[2] if len(sys.argv) > 2 else V + '/fixtures/p8f16/fixtures/encounters-f16.json')
