# Phase 8 family 18: writes verify/fixtures/p8f18/fixtures/schedule-finale18.json, the shipped data/dungeon-run/schedule-v1.json with
# the Heist finale of acts-v3h enabled on its four pending bosses (Magistrix Norroa, Archmage Vargoth, Archmage Kalec, Archmage
# Khadgar, in that order) and nothing else changed: the finale's own keys (its tavern, Health and Mana curves, and its twist,
# DALA_830 The Finale) are the shipped ones. This is what the close-out will ship; until then the shipped schedule keeps the
# finale disabled. Pattern: verify/fixtures/p8f01/fixtures/schedule-finale.json (family 01's finale fixture on two stand-in rows).
# Text edit of the one line, so the rest of the file stays byte-identical. Deterministic, LF, no BOM.
# Usage: python3 -I verify/tools/gen/p8f18/make_fixture.py [<out file>]
import json, os, sys

V = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))  # verify/
REPO = os.path.dirname(V)
SRC = os.path.join(REPO, "data", "dungeon-run", "schedule-v1.json")
OLD = '"finale": {"enabled": false, "fixedBosses": [], "heroRelay": false, "tavernBefore": true,\n                 "id": "IV", "name": "The Heist"'
BOSSES = ["dala_43_norroa", "dala_67_vargoth", "dala_69_kalec", "dala_60_khadgar"]


def main(out):
    text = open(SRC, encoding="utf-8").read()
    if text.count(OLD) != 1: raise SystemExit("schedule-v1.json: the acts-v3h finale line is not where the fixture expects it")
    data = json.loads(text)
    finale = data["schedules"]["acts-v3h"]["finale"]
    if finale.get("pending") != BOSSES: raise SystemExit("schedule-v1.json: acts-v3h's pending finale bosses are %r" % finale.get("pending"))
    if finale.get("twist") != {"pool": ["DALA_830"], "chance": 100}: raise SystemExit("schedule-v1.json: acts-v3h's finale twist is %r" % finale.get("twist"))
    new = OLD.replace('"enabled": false, "fixedBosses": []', '"enabled": true, "fixedBosses": ' + json.dumps(BOSSES))
    text = text.replace(OLD, new)
    check = json.loads(text)["schedules"]["acts-v3h"]["finale"]
    assert check["enabled"] is True and check["fixedBosses"] == BOSSES
    os.makedirs(os.path.dirname(out), exist_ok=True)
    with open(out, "w", encoding="utf-8", newline="\n") as f: f.write(text)
    print("wrote %s: acts-v3h finale enabled on %s" % (out, ", ".join(BOSSES)))


main(sys.argv[1] if len(sys.argv) > 1 else os.path.join(V, "fixtures", "p8f18", "fixtures", "schedule-finale18.json"))
