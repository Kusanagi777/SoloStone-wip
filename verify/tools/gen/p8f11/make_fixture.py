# Phase 8 family 11: writes fixtures/encounters-f11.json = the generated data/dungeon-run/encounters-v1.json plus four fixture
# rows (ids fix11_*; family "11f"; never shipped). Each is one of the family's bosses (its own hero and power) with a curated
# deck that holds six copies of its boss card beside plain minions, and one copy in its hand after the mulligan, so the fair
# AI plays the card from hand as a boss through the run setup path whatever the shipped rows' seeds deal:
#   fix11_brushwood  Hack (GILA_BOSS_41t), with Armorsmiths and Frothing Berserkers that answer each hit
#   fix11_damph      Grave Mistake, the 0-Cost spell (GILA_BOSS_43t; the hunt's treasure, in no shipped deck), with Deathrattle minions
#   fix11_azalina    Soul Assimilation (GILA_BOSS_55t2), with Wisp makers
#   fix11_raeth      Shallow Graves (GILA_BOSS_57t)
# and one row for Crooked Pete's form change in play: fix11_pete is the shipped gila_52_pete row (its decks, its Beastly Pete
# form) at a fixed 16 Health, so the run player's bot takes him to half (8) and he becomes Beastly Pete at his next turn start.
# Usage: python3 -I verify/tools/gen/p8f11/make_fixture.py [<repo> <out file>]     (LF, no BOM; deterministic)
import json, os, sys, collections
V = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))  # verify/, from verify/tools/gen/<family>/

def deck(*pairs):
    out = []
    for cid, n in pairs: out += [cid] * n
    if len(out) != 30: raise SystemExit("a fixture deck has %d cards" % len(out))
    return out

SRC = {"url": "fixture (phase 8 family 11; never shipped)", "read": "2026-11-14", "list": "fixture"}
PLAIN = [("CS2_182", 6), ("CS2_120", 6), ("CS2_172", 6), ("CS2_189", 6)]   # Chillwind Yeti, River Crocolisk, Bloodfen Raptor, Elven Archer
ROWS = [
    ("fix11_brushwood", "Fixture 11 A (Brushwood Centurion with Hacks, Armorsmiths and Frothing Berserkers)", "41", "GILA_BOSS_41t",
     deck(("GILA_BOSS_41t", 6), ("EX1_402", 6), ("EX1_604", 6), ("CS2_182", 6), ("CS2_120", 6))),
    ("fix11_damph", "Fixture 11 B (Gravekeeper Damph with Grave Mistake spells and Deathrattle minions)", "43", "GILA_BOSS_43t",
     deck(("GILA_BOSS_43t", 6), ("EX1_556", 6), ("EX1_096", 6), ("FP1_007", 6), ("CS2_182", 6))),
    ("fix11_azalina", "Fixture 11 C (Azalina Soulthief with Soul Assimilations and Wisps of the Old Gods)", "55", "GILA_BOSS_55t2",
     deck(("GILA_BOSS_55t2", 6), ("CS2_231", 6), *PLAIN[:3])),
    ("fix11_raeth", "Fixture 11 D (Raeth Ghostsong with Shallow Graves)", "57", "GILA_BOSS_57t", deck(("GILA_BOSS_57t", 6), *PLAIN)),
]

def row(rid, name, n, card):
    return collections.OrderedDict([
        ("id", rid), ("name", name), ("provenance", "GIL"), ("family", "11f"), ("hero", "GILA_BOSS_%sh" % n), ("power", "GILA_BOSS_%sp" % n),
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
    pete = json.loads(json.dumps(next(e for e in data["encounters"] if e["id"] == "gila_52_pete")), object_pairs_hook=collections.OrderedDict)
    pete["id"], pete["name"], pete["family"] = "fix11_pete", "Fixture 11 E (Crooked Pete at 16 Health)", "11f"
    pete["health"], pete["fixedHealth"] = 16, True
    if any(e["id"] == pete["id"] for e in data["encounters"]): raise SystemExit("fix11_pete exists")
    data["encounters"].append(pete)
    data["generatedBy"] = "p8f11/make_fixture.py over tools/dungeon-run-catalog (encounters); a test fixture, never shipped"
    text = json.dumps(data, indent=1, ensure_ascii=False) + "\n"
    open(out, "w", encoding="utf-8", newline="\n").write(text)
    print("wrote %s: %d rows (%d fixture), %d decks" % (out, len(data["encounters"]), len(ROWS) + 1, len(data["decks"])))

main(sys.argv[1] if len(sys.argv) > 2 else os.path.dirname(V),
     sys.argv[2] if len(sys.argv) > 2 else V + '/fixtures/p8f11/fixtures/encounters-f11.json')
