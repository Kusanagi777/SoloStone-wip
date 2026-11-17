# Phase 8 family 18: writes verify/fixtures/p8f18/fixtures/schedule-finale18.json, the schedule `bp.sh enc18-finale` walks:
# acts-v3h with the Heist finale enabled on its four bosses (Magistrix Norroa, Archmage Vargoth, Archmage Kalec, Archmage
# Khadgar, in that order), its own tavern, Health and Mana curves, and its twist DALA_830 The Finale.
# Until the close-out (family 25) the shipped schedule kept that finale disabled, and this generator enabled it by a text edit
# of the one line. The close-out ships the finale exactly as this fixture had it, so the fixture is now the shipped
# data/dungeon-run/schedule-v1.json byte for byte; the generator checks the shipped finale block is the one family 18 built.
# Deterministic, LF, no BOM.
# Usage: python3 -I verify/tools/gen/p8f18/make_fixture.py [<out file>]
import json, os, sys

V = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))  # verify/
REPO = os.path.dirname(V)
SRC = os.path.join(REPO, "data", "dungeon-run", "schedule-v1.json")
BOSSES = ["dala_43_norroa", "dala_67_vargoth", "dala_69_kalec", "dala_60_khadgar"]


def main(out):
    text = open(SRC, encoding="utf-8").read()
    finale = json.loads(text)["schedules"]["acts-v3h"]["finale"]
    expected = {"enabled": True, "fixedBosses": BOSSES, "heroRelay": False, "tavernBefore": True, "id": "IV", "name": "The Heist",
                "bossHealth": [70], "bossMana": [3], "tavern": {"gold": 3, "offers": 4}, "twist": {"pool": ["DALA_830"], "chance": 100}}
    if finale != expected: raise SystemExit("schedule-v1.json: acts-v3h's finale is %r, not family 18's Heist finale" % finale)
    os.makedirs(os.path.dirname(out), exist_ok=True)
    with open(out, "w", encoding="utf-8", newline="\n") as f: f.write(text)
    print("wrote %s: the shipped schedule (acts-v3h finale enabled on %s)" % (out, ", ".join(BOSSES)))


main(sys.argv[1] if len(sys.argv) > 1 else os.path.join(V, "fixtures", "p8f18", "fixtures", "schedule-finale18.json"))
