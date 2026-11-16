# Phase 8 family 23: writes fixtures/encounters-f23.json = the generated data/dungeon-run/encounters-v1.json plus four fixture
# rows (ids fix23_*; family "23f"; never shipped). Each is one of the nemeses (its own hero and power) with a curated deck that
# holds six copies of each card under test beside plain minions, and one copy in its hand after the mulligan, so the fair AI
# plays the family's boss cards from hand as a boss through the run setup path whatever the shipped rows' seeds deal:
#   fix23_glinda   Crowskin Pact (GILA_BOSS_49t2) and Crowskin Faithful (GILA_BOSS_49t), with False Heart
#   fix23_shivers  Cursed Crewmember (GILA_BOSS_60t) and Witchwood's Touch (1) (GILA_BOSS_99t), Kingsbane at the start
#   fix23_toki     Clockwork Assistant (GILA_907) and Witchwood's Touch (3) (GILA_BOSS_99t3), with cheap spells
#   fix23_godfrey  Hunter's Insight (GILA_503) and Witchwood's Touch (2) (GILA_BOSS_99t2)
# Usage: python3 -I verify/tools/gen/p8f23/make_fixture.py [<repo> <out file>]     (LF, no BOM; deterministic)
import json, os, sys, collections
V = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))  # verify/

def deck(*pairs):
    out = []
    for cid, n in pairs: out += [cid] * n
    if len(out) != 30: raise SystemExit("a fixture deck has %d cards" % len(out))
    return out

SRC = {"url": "fixture (phase 8 family 23; never shipped)", "read": "2026-11-14", "list": "fixture"}
ROWS = [
    # id, name, boss number, hand card, start extras, deck
    ("fix23_glinda", "Fixture 23 A (Glinda Crowskin with Crowskin Pacts and Faithful)", "49", "GILA_BOSS_49t2", None,
     deck(("GILA_BOSS_49t2", 6), ("GILA_BOSS_49t", 6), ("CS2_182", 6), ("CS2_120", 6), ("CS2_172", 6))),
    ("fix23_shivers", "Fixture 23 B (Captain Shivers with Cursed Crewmembers and Witchwood's Touch (1))", "60", "GILA_BOSS_60t", "LOOT_542",
     deck(("GILA_BOSS_60t", 6), ("GILA_BOSS_99t", 6), ("CS2_182", 6), ("CS2_120", 6), ("CS2_172", 6))),
    ("fix23_toki", "Fixture 23 C (Infinite Toki with Clockwork Assistants, Witchwood's Touch (3) and Arcane Explosions)", "48", "GILA_907", None,
     deck(("GILA_907", 6), ("GILA_BOSS_99t3", 6), ("CS2_025", 6), ("CS2_182", 6), ("CS2_120", 6))),
    ("fix23_godfrey", "Fixture 23 D (Lord Godfrey with Hunter's Insights and Witchwood's Touch (2))", "59", "GILA_503", None,
     deck(("GILA_503", 6), ("GILA_BOSS_99t2", 6), ("CS2_182", 6), ("CS2_120", 6), ("CS2_172", 6))),
]

def row(rid, name, n, card, weapon):
    start = collections.OrderedDict([("bossHandAdd", [card])])
    if weapon: start["bossWeapon"] = weapon
    flags = ["start"]
    if n == "49": flags += ["passive", "heroImmune"]
    if n == "60": flags += ["passive"]
    return collections.OrderedDict([
        ("id", rid), ("name", name), ("provenance", "GIL"), ("family", "23f"), ("hero", "GILA_BOSS_%sh" % n), ("power", "GILA_BOSS_%sp" % n),
        ("tier", "3"), ("archetype", "midrange"), ("level", {"source": "printed-health", "original": 40}),
        ("flags", flags), ("start", start),
        ("deck", collections.OrderedDict([("id", "enc-" + rid), ("kind", "curated"), ("source", SRC)])),
    ])

def main(repo, out):
    data = json.load(open(repo + "/data/dungeon-run/encounters-v1.json", encoding="utf-8"), object_pairs_hook=collections.OrderedDict)
    for rid, name, n, card, weapon, cards in ROWS:
        r = row(rid, name, n, card, weapon)
        if any(e["id"] == r["id"] for e in data["encounters"]): raise SystemExit(r["id"] + " exists")
        data["encounters"].append(r)
        data["decks"][r["deck"]["id"]] = list(cards)
    data["generatedBy"] = "p8f23/make_fixture.py over tools/dungeon-run-catalog (encounters); a test fixture, never shipped"
    text = json.dumps(data, indent=1, ensure_ascii=False) + "\n"
    open(out, "w", encoding="utf-8", newline="\n").write(text)
    print("wrote %s: %d rows (%d fixture), %d decks" % (out, len(data["encounters"]), len(ROWS), len(data["decks"])))

main(sys.argv[1] if len(sys.argv) > 2 else os.path.dirname(V),
     sys.argv[2] if len(sys.argv) > 2 else V + '/fixtures/p8f23/fixtures/encounters-f23.json')
