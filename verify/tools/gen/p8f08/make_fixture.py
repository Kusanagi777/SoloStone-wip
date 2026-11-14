# Phase 8 family 08: writes fixtures/encounters-f08.json = the generated data/dungeon-run/encounters-v1.json plus two fixture
# rows (ids fix08_*; family "08f"; never shipped). They put the boss deck minions the family scripts for OTHER families'
# decks in a boss's hands, so the fair AI plays them as a boss through the run setup path:
#   fix08_cards   Elder Jari's hero and power with a curated deck of Starving Crab, Reverberating Ooze, Shroomsayer,
#                 Gluttonous Trogg and Deadly Spore (three each) beside plain minions and small damage spells; one of each
#                 of the first four starts in the boss's hand, a Starving Crab and a Reverberating Ooze on its board.
#   fix08_kraxx   Kraxx's hero and power (Giant Stomp) with the same deck and hand, an empty start board.
# Usage: python -I verify/tools/gen/p8f08/make_fixture.py [<repo> <out file>]     (LF, no BOM; deterministic)
# (ported to verify/: defaults are the repository this file sits in and verify/fixtures/p8f08/fixtures/encounters-f08.json)
import json, os, sys, collections
V = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))  # verify/, from verify/tools/gen/<family>/

def deck(*pairs):
    out = []
    for cid, n in pairs: out += [cid] * n
    if len(out) != 30: raise SystemExit("a fixture deck has %d cards" % len(out))
    return out

SRC = {"url": "fixture (phase 8 family 08; never shipped)", "read": "2026-11-13", "list": "fixture"}
CARDS = deck(("LOOTA_102", 3), ("LOOTA_104", 3), ("LOOTA_107", 3), ("LOOTA_109", 3), ("LOOTA_105", 3),
             ("CS2_182", 3), ("CS2_120", 3), ("CS2_172", 3), ("CS2_008", 2), ("CS2_012", 2), ("CS2_189", 2))
HAND = ["LOOTA_102", "LOOTA_104", "LOOTA_107", "LOOTA_109"]

def row(rid, name, hero, power, board):
    start = collections.OrderedDict()
    if board: start["bossBoard"] = board
    start["bossHandAdd"] = HAND
    return collections.OrderedDict([
        ("id", rid), ("name", name), ("provenance", "LOOT"), ("family", "08f"), ("hero", hero), ("power", power), ("tier", "2"),
        ("archetype", "midrange"), ("level", {"source": "wiki-level-table", "original": 4}),
        ("flags", ["start"] + (["boardStart"] if board else [])), ("start", start),
        ("deck", collections.OrderedDict([("id", "enc-" + rid), ("kind", "curated"), ("source", SRC)])),
    ])

def main(repo, out):
    data = json.load(open(repo + "/data/dungeon-run/encounters-v1.json", encoding="utf-8"), object_pairs_hook=collections.OrderedDict)
    rows = [row("fix08_cards", "Fixture 08 A (Elder Jari with the Dungeon Run boss deck minions; a Crab and an Ooze on the board)", "LOOTA_BOSS_42h", "LOOTA_BOSS_42p", ["LOOTA_102", "LOOTA_104"]),
            row("fix08_kraxx", "Fixture 08 B (Kraxx with the Dungeon Run boss deck minions)", "LOOTA_BOSS_39h", "LOOTA_BOSS_39p", None)]
    for r in rows:
        if any(e["id"] == r["id"] for e in data["encounters"]): raise SystemExit(r["id"] + " exists")
        data["encounters"].append(r)
        data["decks"][r["deck"]["id"]] = list(CARDS)
    data["generatedBy"] = "p8f08/make_fixture.py over tools/dungeon-run-catalog (encounters); a test fixture, never shipped"
    text = json.dumps(data, indent=1, ensure_ascii=False) + "\n"
    open(out, "w", encoding="utf-8", newline="\n").write(text)
    print("wrote %s: %d rows (%d fixture), %d decks" % (out, len(data["encounters"]), len(rows), len(data["decks"])))

main(sys.argv[1] if len(sys.argv) > 2 else os.path.dirname(V),
     sys.argv[2] if len(sys.argv) > 2 else V + '/fixtures/p8f08/fixtures/encounters-f08.json')
