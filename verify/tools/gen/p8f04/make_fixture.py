# Phase 8 family 04: writes fixtures/encounters-f04.json = the lane's generated data/dungeon-run/encounters-v1.json plus
# eleven fixture rows (ids fix_04_* and fixp_04_*; never shipped).
#   fix_04_reno / fix_04_brann / fix_04_elise / fix_04_plagues (family "04"): curated 30-card boss decks that hold the family's 23
#     cards a boss may play, on the heroes and powers of three shipped family-01 rows; +4 start mana so the boss reaches
#     its expensive cards in a short game.
#   fixp_04_<quest> (family "04p"): War Master Voone's hero with a plain power and his shipped champion deck; the start
#     block gives the RUN PLAYER one Quest and the cards that advance it (hand), and more of them plus other cards of
#     the family (deck). The Quests are the run player's only (admission quest-hero-power).
# Usage: python -I verify/tools/gen/p8f04/make_fixture.py [<repo> <out file>]     (LF, no BOM; deterministic)
# (ported to verify/: defaults are the repository this file sits in and verify/fixtures/p8f04/fixtures/encounters-f04.json)
import json, os, sys, collections
V = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))  # verify/, from verify/tools/gen/<family>/

def deck(*pairs):
    out = []
    for cid, n in pairs: out += [cid] * n
    if len(out) != 30: raise SystemExit("a fixture deck has %d cards" % len(out))
    return out

SRC = {"url": "fixture (phase 8 family 04; never shipped)", "read": "2026-11-12", "list": "fixture"}

def boss_row(rid, name, hero, power, cards, hand, mana=4, player_board=None):
    return collections.OrderedDict([
        ("id", rid), ("name", name), ("provenance", "ULDA"), ("family", "04"), ("hero", hero), ("power", power), ("tier", "2"),
        ("archetype", "midrange"), ("level", {"source": "wiki-level-table", "original": 3}), ("bossManaCap", mana),
        ("flags", ["secrets", "start"]), ("start", collections.OrderedDict(([("playerBoard", player_board)] if player_board else []) + [("bossHandAdd", hand), ("bossMana", mana)])),
        ("deck", collections.OrderedDict([("id", "enc-" + rid), ("kind", "curated"), ("source", SRC)])),
    ]), cards

def player_row(quest, hand, to_deck):
    rid = "fixp_04_" + quest[4:]
    return collections.OrderedDict([
        ("id", rid), ("name", "Quest fixture " + quest + " (the run player holds it)"), ("provenance", "TRL"), ("family", "04p"),
        ("hero", "TRLA_200h"), ("power", "HERO_01bp"), ("tier", "2"), ("archetype", "midrange"),
        ("level", {"source": "rumble-round", "original": 3}), ("flags", ["start"]),
        ("start", collections.OrderedDict([("playerDeckAdd", to_deck), ("playerHandAdd", [quest] + hand)])),
        ("deck", collections.OrderedDict([("id", "enc-" + rid), ("kind", "curated"), ("source", SRC)])),
    ])

def main(repo, out):
    data = json.load(open(repo + "/data/dungeon-run/encounters-v1.json", encoding="utf-8"), object_pairs_hook=collections.OrderedDict)
    rows, decks = [], {}
    for row, cards in [
        boss_row("fix_04_reno", "Fixture 04 A (Elder Brandlemar with the Reno-bucket cards)", "LOOTA_BOSS_35h", "LOOTA_BOSS_35p", deck(
            ("ULD_286", 2), ("ULD_288", 1), ("ULD_293", 2), ("ULD_726", 2), ("ULD_329", 2), ("ULD_435", 2), ("ULD_327", 2), ("ULD_304", 1), ("ULD_727", 2),
            ("EX1_287", 2), ("EX1_289", 2), ("EX1_096", 2), ("FP1_002", 2), ("CS2_029", 2), ("CS2_023", 2), ("CS2_182", 2)),
            # Wave 1 integration (main tree): Body Wrapper starts in this boss's hand. With the merged catalog's player decks
            # the boss never played its deck copies in 40 games (the lane's games: twice), so the fixture no longer leaves it to the draw.
            ["ULD_304", "ULD_288", "ULD_727"]),
        boss_row("fix_04_brann", "Fixture 04 B (Manhunter Ivan with the Brann-bucket cards)", "GILA_BOSS_25h", "GILA_BOSS_25p", deck(
            ("ULD_282", 2), ("ULD_290", 2), ("ULD_410", 2), ("ULD_707", 2), ("ULD_709", 2), ("ULD_713", 2), ("ULD_720", 2), ("ULD_721", 1),
            ("CS2_125", 2), ("EX1_534", 2), ("CS2_106", 2), ("EX1_610", 2), ("EX1_007", 2), ("EX1_604", 2), ("CS2_182", 3)), ["ULD_721", "ULD_709"]),
        boss_row("fix_04_elise", "Fixture 04 C (Trap Room with the Elise- and Finley-bucket cards)", "ULDA_BOSS_44h", "ULDA_BOSS_44p", deck(
            ("ULD_292", 2), ("ULD_438", 2), ("ULD_714", 2), ("ULD_716", 2), ("ULD_718", 2), ("ULD_728", 2),
            ("CS2_168", 2), ("EX1_506", 2), ("CS2_173", 2), ("EX1_507", 2), ("EX1_096", 2), ("CS2_189", 2), ("EX1_130", 2), ("CS2_182", 2), ("EX1_015", 2)),
            ["ULD_716", "ULD_718"]),
        boss_row("fix_04_plagues", "Fixture 04 D (Elder Brandlemar with the board clears and the two 10-Cost minions, 10 mana from turn 1)", "LOOTA_BOSS_35h", "LOOTA_BOSS_35p", deck(
            ("ULD_718", 2), ("ULD_707", 2), ("ULD_728", 2), ("ULD_714", 2), ("ULD_304", 1), ("ULD_721", 1), ("ULD_435", 2), ("ULD_727", 2),
            ("CS2_025", 2), ("CS2_093", 2), ("CS1_112", 2), ("CS2_032", 2), ("CS2_029", 2), ("CS2_023", 2), ("EX1_279", 2), ("EX1_096", 2)),
            ["ULD_304", "ULD_721", "ULD_718", "ULD_707", "ULD_728"], 9),
        boss_row("fix_04_wipe", "Fixture 04 E (Elder Brandlemar behind on board: the run player starts with four Boulderfist Ogres)", "LOOTA_BOSS_35h", "LOOTA_BOSS_35p", deck(
            ("ULD_718", 2), ("ULD_707", 2), ("ULD_728", 2), ("ULD_714", 2), ("ULD_286", 2), ("ULD_327", 2), ("ULD_410", 2), ("CS2_125", 2),
            ("CS2_025", 2), ("CS2_093", 2), ("CS1_112", 2), ("CS2_032", 2), ("CS2_029", 2), ("CS2_023", 2), ("CS2_182", 2)),
            ["ULD_718", "ULD_707", "ULD_286", "ULD_410"], 9, ["CS2_200"] * 4),
    ]:
        rows.append(row); decks[row["deck"]["id"]] = cards
    voone = data["decks"]["enc-trl_200_voone-champion"]
    for quest, hand, to_deck in [
        ("ULD_291", ["EX1_015", "EX1_015", "CS2_189", "CS2_189"], ["EX1_015"] * 4 + ["CS2_189"] * 4 + ["ULD_282", "ULD_290", "ULD_288", "ULD_410"]),
        ("ULD_326", ["ULD_327", "CS2_106", "EX1_534", "CS2_029"], ["CS2_093", "CS2_072", "CS2_234", "EX1_238", "CS2_057", "EX1_604", "ULD_327",
                                                                    "ULD_286", "ULD_329", "ULD_435", "ULD_304"]),
        ("ULD_431", ["ULD_275", "ULD_275", "ULD_207", "ULD_207"], ["ULD_275"] * 3 + ["ULD_207"] * 2 + ["ULD_721", "ULD_438", "ULD_716", "ULD_727", "ULD_728"]),
        ("ULD_433", ["EX1_277", "EX1_277", "EX1_277", "CS2_023"], ["EX1_277"] * 6 + ["CS2_025"] * 3 + ["ULD_726", "ULD_293", "ULD_718", "ULD_707"]),
        ("ULD_711", ["CS2_106", "CS2_106", "ULD_709"], ["CS2_106"] * 3 + ["CS2_112"] * 2 + ["EX1_398"] * 2 + ["ULD_720", "ULD_713", "ULD_292"]),
        ("ULD_724", ["GIL_622", "GIL_622", "ULD_714", "ULD_714"], ["GIL_622"] * 4 + ["ULD_714"] * 2 + ["ULD_207"] * 2 + ["ULD_721"]),
    ]:
        row = player_row(quest, hand, to_deck)
        rows.append(row); decks[row["deck"]["id"]] = list(voone)
    for row in rows:
        if any(e["id"] == row["id"] for e in data["encounters"]): raise SystemExit(row["id"] + " exists")
        data["encounters"].append(row)
    data["decks"].update(decks)
    data["generatedBy"] = "p8f04/make_fixture.py over tools/dungeon-run-catalog (encounters); a test fixture, never shipped"
    text = json.dumps(data, indent=1, ensure_ascii=False) + "\n"
    open(out, "w", encoding="utf-8", newline="\n").write(text)
    print("wrote %s: %d rows (%d fixture), %d decks" % (out, len(data["encounters"]), len(rows), len(data["decks"])))

main(sys.argv[1] if len(sys.argv) > 2 else os.path.dirname(V),
     sys.argv[2] if len(sys.argv) > 2 else V + '/fixtures/p8f04/fixtures/encounters-f04.json')
