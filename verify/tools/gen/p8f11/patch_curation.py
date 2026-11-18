# Phase 8 family 11: appends the landed-family entry E11 to tools/restoration-ledger/curation.json (after E10, the last entry of
# "landedFamilies"); idempotent (an existing E11 is left as it is). Only that one entry is written; the tool-owned lists are the
# encounters step's. LF, no BOM.
# Usage: python3 -I verify/tools/gen/p8f11/patch_curation.py [<repo>]
import json, os, sys
V = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
REPO = sys.argv[1] if len(sys.argv) > 1 else os.path.dirname(V)
PATH = os.path.join(REPO, "tools", "restoration-ledger", "curation.json")
N = ["40", "41", "42", "43", "44", "51", "52", "54", "55", "56", "57", "58", "62"]
IDS = [x for n in N for x in ("GILA_BOSS_%sh" % n, "GILA_BOSS_%sp" % n)] + ["GILA_BOSS_52p2", "GILA_BOSS_41t", "GILA_BOSS_43t", "GILA_BOSS_55t",
       "GILA_BOSS_55t2", "GILA_BOSS_57t"]
NOTE = ("Phase 8 family 11, Monster Hunt bosses B: thirteen encounters of The Witchwood's Monster Hunt with a clickable hero power, "
    "Crooked Pete's form change and their boss-only cards (reference/encounters/F11.json, scripts in DungeonRunEncounters.F11.cs, "
    "admission rows in DungeonRunAdmission.F11.cs, engine rules in Actions/EncounterRules.F11.cs). The tier is the printed Health of the "
    "hero card (20: tier 1; 30: tier 2; 35 and 40: tier 3; 50 and 60: tier 4). Splintergraft (Grafted Bark copies a minion into her hand "
    "as a 2/2 that costs (2), behind the generation filter), Brushwood Centurion (Survival of the Fittest: every minion attacks a random "
    "enemy minion, EncounterRules.AllAttackRandomEnemyMinions on family 06's ForcedAttack; Hack, five hits of 1), Baran the Blind "
    "(Bulldoze spends his Armor as damage to a random minion), Gravekeeper Damph (Grave Mistake resolves every minion's Deathrattles "
    "twice; the 0-Cost spell of that name, the hunt's treasure, is scripted and admitted but in no deck), Doctor Sezavo (Hollow Needle), "
    "Garrow, the Rancorous (Death and Taxes), Crooked Pete (Soot Up; at the start of his turn at half his Health or less he becomes "
    "Beastly Pete, the first form change inside a match: a script form of the encounter model with Beast Within, Beastly Pete's deck "
    "replacing his, his hand set aside, 6 cards drawn, 15 Armor, his Health kept), Cragtorr (One with the Trees), Azalina Soulthief "
    "(Unfinished Business summons three Wisps; Soul Assimilation destroys her Wisps and takes an enemy minion for each, never a run "
    "shrine), Face Collector (A New Face transforms a minion into a random one that costs (2) more; flag mirrorDeck: the boss plays a "
    "copy of the run deck less what a boss may not hold, DungeonRun.ApplyEncounter, with a curated fallback list), Raeth Ghostsong "
    "(Desecrate is readied again when it kills, EncounterRules.PowerRefresh 32646; Shallow Graves summons minions of either side that "
    "died this game, never a dead run shrine: ShrineRules.IsCorpse) and Grum (Terrify shuffles a minion into its owner's deck through "
    "the engine's MoveToDeck, Costs (4) less there; a run shrine goes dormant instead: ShrineRules.InterceptLeave) and Gustave, the "
    "Gutripper (Cull the Meek). Decks: the wiki's tables (30 cards, Damph's and Sezavo's 20 extended to 30); Witchwood's Touch is family "
    "23's card (scripted since the wave-6 merge), and three wrong-target Ogres are replaced, each recorded by the encounters step. "
    "GILA_BOSS_52h2 Beastly Pete is the form's portrait: a support record. A clickable power that would do nothing is not offered. Boss "
    "heroes need no script. None is rejected. Every row sits on acts-v3h and acts-v3t, which no new run follows until schedule-v1.json "
    "\"defaults\" names them (blocked_by run:default-schedule).")

def main():
    text = open(PATH, encoding="utf-8").read()
    if '{"id": "E11"' in text:
        print("E11 already present"); return
    anchor = '"GILA_BOSS_35t", "GILA_BOSS_37t"]}\n  ],'
    if text.count(anchor) != 1: raise SystemExit("the E10 entry's end was not found once")
    entry = ('    {"id": "E11", "note": ' + json.dumps(NOTE, ensure_ascii=False) + ',\n     "ids": ' + json.dumps(IDS)
             + ', "supportRecords": ["GILA_BOSS_52h2"]}')
    text = text.replace(anchor, '"GILA_BOSS_35t", "GILA_BOSS_37t"]},\n' + entry + '\n  ],')
    json.loads(text)
    with open(PATH, "w", encoding="utf-8", newline="\n") as f: f.write(text)
    print("appended E11 (%d ids, 1 support record)" % len(IDS))

main()
