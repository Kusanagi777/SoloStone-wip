# Phase 8 family 09: writes fixtures/encounters-f09.json = the generated data/dungeon-run/encounters-v1.json plus four fixture
# rows (ids fix09_*; family "09f"; never shipped). They put cards the family scripts or admits for a boss into a boss's
# deck, so the fair AI plays them as a boss through the run setup path:
#   fix09_treasures  King Togwaggle's hero and Magic Candle with a deck that holds each of his sixteen Treasures once
#   fix09_beams      Xol's hero and Beam Me Up! with three copies of each of the six Beams in the deck
#   fix09_chests     Vustrasz's hero, power and shipped deck with THREE Master Chests to start: Vindictive Breath is offered
#                    from his first turn (two Chests missing)
#   fix09_medivh     Chronomancer Inara's hero and power with Medivh's cards, Chromatic Dragonkin and Stone Golems in the deck
# Usage: python -I verify/tools/gen/p8f09/make_fixture.py [<repo> <out file>]     (LF, no BOM; deterministic)
# (ported to verify/: defaults are the repository this file sits in and verify/fixtures/p8f09/fixtures/encounters-f09.json)
import json, os, sys, collections
V = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))  # verify/, from verify/tools/gen/<family>/


def deck(*pairs):
    out = []
    for cid, n in pairs:
        out += [cid] * n
    if len(out) != 30:
        raise SystemExit("a fixture deck has %d cards" % len(out))
    return out


SRC = {"url": "fixture (phase 8 family 09; never shipped)", "read": "2026-11-13", "list": "fixture"}
TREASURES = ["LOOTA_806", "LOOTA_812", "LOOTA_816", "LOOTA_819", "LOOTA_821", "LOOTA_823", "LOOTA_826", "LOOTA_827", "LOOTA_835", "LOOTA_836",
             "LOOTA_838", "LOOTA_839", "LOOTA_842b", "LOOTA_843", "LOOTA_847", "LOOT_998k"]
BEAMS = ["LOOTA_BOSS_26p2", "LOOTA_BOSS_26p3", "LOOTA_BOSS_26p4", "LOOTA_BOSS_26p5", "LOOTA_BOSS_26p6", "LOOTA_BOSS_26p7"]
PLAIN = [("CS2_182", 3), ("CS2_120", 3), ("CS2_172", 3), ("CS2_189", 3), ("CS2_029", 2)]


def row(rid, name, hero, power, deck_id, flags=None, start=None):
    r = collections.OrderedDict([
        ("id", rid), ("name", name), ("provenance", "LOOT"), ("family", "09f"), ("hero", hero), ("power", power), ("tier", "2"),
        ("archetype", "midrange"), ("level", {"source": "wiki-level-table", "original": 4})])
    if flags:
        r["flags"] = flags
    if start:
        r["start"] = start
    r["deck"] = collections.OrderedDict([("id", deck_id), ("kind", "curated"), ("source", SRC)])
    return r


def main(repo, out):
    data = json.load(open(repo + "/data/dungeon-run/encounters-v1.json", encoding="utf-8"), object_pairs_hook=collections.OrderedDict)
    vustrasz = next(e for e in data["encounters"] if e["id"] == "loot_25_vustrasz")
    rows = [
        (row("fix09_treasures", "Fixture 09 A (King Togwaggle with each of his sixteen Treasures in the deck)", "LOOTA_BOSS_99h", "LOOTA_BOSS_99p", "enc-fix09_treasures"),
         deck(*([(t, 1) for t in TREASURES] + PLAIN))),
        (row("fix09_beams", "Fixture 09 B (Xol with three of each Beam in the deck)", "LOOTA_BOSS_26h", "LOOTA_BOSS_26p8", "enc-fix09_beams", flags=["passive"]),
         deck(*([(b, 3) for b in BEAMS] + [("CS2_182", 3), ("CS2_120", 3), ("CS2_172", 3), ("CS2_189", 3)]))),
        (row("fix09_chests", "Fixture 09 C (Vustrasz with three Master Chests: two missing from the start)", "LOOTA_BOSS_25h", "LOOTA_BOSS_25p", "enc-fix09_chests",
             flags=["start", "boardStart"], start=collections.OrderedDict([("bossBoard", ["LOOT_357l"] * 3)])),
         list(data["decks"][vustrasz["deck"]["id"]])),
        (row("fix09_medivh", "Fixture 09 D (Inara with Medivh's cards, Chromatic Dragonkin and Stone Golems)", "LOOTA_BOSS_53h", "LOOTA_BOSS_53p", "enc-fix09_medivh", flags=["passive"]),
         deck(("KARA_00_05", 3), ("KARA_00_07", 4), ("KARA_00_08", 4), ("BRMA12_8t", 4), ("LOOTA_BOSS_20t", 3), ("CS2_029", 4), ("CS2_024", 4), ("CS2_182", 4))),
    ]
    for r, cards in rows:
        if any(e["id"] == r["id"] for e in data["encounters"]):
            raise SystemExit(r["id"] + " exists")
        data["encounters"].append(r)
        data["decks"][r["deck"]["id"]] = cards
    data["generatedBy"] = "p8f09/make_fixture.py over tools/dungeon-run-catalog (encounters); a test fixture, never shipped"
    text = json.dumps(data, indent=1, ensure_ascii=False) + "\n"
    open(out, "w", encoding="utf-8", newline="\n").write(text)
    print("wrote %s: %d rows (%d fixture), %d decks" % (out, len(data["encounters"]), len(rows), len(data["decks"])))


main(sys.argv[1] if len(sys.argv) > 2 else os.path.dirname(V),
     sys.argv[2] if len(sys.argv) > 2 else V + '/fixtures/p8f09/fixtures/encounters-f09.json')
