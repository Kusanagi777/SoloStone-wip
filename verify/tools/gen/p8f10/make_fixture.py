# Phase 8 family 10: writes fixtures/encounters-f10.json = the generated data/dungeon-run/encounters-v1.json plus six fixture
# rows (ids fix10_*; family "10f"; never shipped). Each is one of the family's bosses (its own hero and power) with a curated
# deck that holds six copies of its boss card beside plain minions, and one copy in its hand after the mulligan, so the fair
# AI plays the card from hand as a boss through the run setup path whatever the shipped rows' seeds deal:
#   fix10_grubb        Chuck, the 6-Cost spell (GILA_BOSS_23t), with cheap high-Attack minions to throw
#   fix10_gnomenapper  Sack of Gnomes (GILA_BOSS_26t2)
#   fix10_experiment   Amalgamate (GILA_BOSS_27t)
#   fix10_sazzmi       Enchanting Tune (GILA_BOSS_29t)
#   fix10_chupacabran  Vampiric Fangs (GILA_BOSS_35t)
#   fix10_wharrgarbl   Murloc Infestation (GILA_BOSS_37t), with Murlocs for It's Raining Fin
# Usage: python -I verify/tools/gen/p8f10/make_fixture.py [<repo> <out file>]     (LF, no BOM; deterministic)
# (ported to verify/: defaults are the repository this file sits in and verify/fixtures/p8f10/fixtures/encounters-f10.json)
import json, os, sys, collections
V = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))  # verify/, from verify/tools/gen/<family>/

def deck(*pairs):
    out = []
    for cid, n in pairs: out += [cid] * n
    if len(out) != 30: raise SystemExit("a fixture deck has %d cards" % len(out))
    return out

SRC = {"url": "fixture (phase 8 family 10; never shipped)", "read": "2026-11-13", "list": "fixture"}
PLAIN = [("CS2_182", 6), ("CS2_120", 6), ("CS2_172", 6), ("CS2_189", 6)]   # Chillwind Yeti, River Crocolisk, Bloodfen Raptor, Elven Archer
ROWS = [
    ("fix10_grubb", "Fixture 10 A (Grubb with Chuck spells and Magma Ragers)", "23", "GILA_BOSS_23t",
     deck(("GILA_BOSS_23t", 6), ("CS2_118", 8), ("CS2_124", 6), ("CS2_173", 4), ("CS2_182", 6))),
    ("fix10_gnomenapper", "Fixture 10 B (Gnomenapper with Sacks of Gnomes)", "26", "GILA_BOSS_26t2", deck(("GILA_BOSS_26t2", 6), *PLAIN)),
    ("fix10_experiment", "Fixture 10 C (Experiment 3C with Amalgamates)", "27", "GILA_BOSS_27t", deck(("GILA_BOSS_27t", 6), *PLAIN)),
    ("fix10_sazzmi", "Fixture 10 D (Sazzmi Gentlehorn with Enchanting Tunes)", "29", "GILA_BOSS_29t", deck(("GILA_BOSS_29t", 6), *PLAIN)),
    ("fix10_chupacabran", "Fixture 10 E (Chupacabran with Vampiric Fangs)", "35", "GILA_BOSS_35t", deck(("GILA_BOSS_35t", 6), *PLAIN)),
    ("fix10_wharrgarbl", "Fixture 10 F (Wharrgarbl with Murloc Infestations and Murlocs)", "37", "GILA_BOSS_37t",
     deck(("GILA_BOSS_37t", 6), ("CS2_168", 8), ("CS2_173", 6), ("EX1_506", 6), ("CS2_182", 4))),
]

def row(rid, name, n, card):
    return collections.OrderedDict([
        ("id", rid), ("name", name), ("provenance", "GIL"), ("family", "10f"), ("hero", "GILA_BOSS_%sh" % n), ("power", "GILA_BOSS_%sp" % n),
        ("tier", "3"), ("archetype", "midrange"), ("level", {"source": "printed-health", "original": 40}),
        ("flags", ["start"]), ("start", collections.OrderedDict([("bossHandAdd", [card])])),
        ("deck", collections.OrderedDict([("id", "enc-" + rid), ("kind", "curated"), ("source", SRC)])),
    ])

def main(repo, out):
    data = json.load(open(repo + "/data/dungeon-run/encounters-v1.json", encoding="utf-8"), object_pairs_hook=collections.OrderedDict)
    for rid, name, n, card, cards in ROWS:
        r = row(rid, name, n, card)
        if any(e["id"] == r["id"] for e in data["encounters"]): raise SystemExit(r["id"] + " exists")
        data["encounters"].append(r)
        data["decks"][r["deck"]["id"]] = list(cards)
    data["generatedBy"] = "p8f10/make_fixture.py over tools/dungeon-run-catalog (encounters); a test fixture, never shipped"
    text = json.dumps(data, indent=1, ensure_ascii=False) + "\n"
    open(out, "w", encoding="utf-8", newline="\n").write(text)
    print("wrote %s: %d rows (%d fixture), %d decks" % (out, len(data["encounters"]), len(ROWS), len(data["decks"])))

main(sys.argv[1] if len(sys.argv) > 2 else os.path.dirname(V),
     sys.argv[2] if len(sys.argv) > 2 else V + '/fixtures/p8f10/fixtures/encounters-f10.json')
