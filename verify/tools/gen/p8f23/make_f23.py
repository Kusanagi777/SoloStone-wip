# Phase 8 family 23 (Monster Hunt nemeses): writes data/dungeon-run/reference/encounters/F23.json, the hand transcription of
# the four Final Nemesis boss pages of hearthstone.wiki.gg (read 2026-11-14 as wikitext, ?action=raw), by card name in the
# deck table's order with the table's own counts. Hagatha the Witch (gila_61_hagatha, family 23's major) keeps her row in
# F01.json (family 01 built the hero-card model on her); this file adds the nemeses and records the one rejected card.
# Usage: python3 -I verify/tools/gen/p8f23/make_f23.py [<repo>]      (LF, no BOM; deterministic)
import json, os, sys
from collections import OrderedDict as O

V = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))  # verify/
READ = "2026-11-14"
WIKI = "https://hearthstone.wiki.gg/wiki/"
TOUCH3 = ("Witchwood's Touch", 2, "GILA_BOSS_99t3")

def cards(*rows):
    out = []
    for r in rows:
        name, count = r[0], r[1]
        e = O([("name", name), ("count", count)])
        if len(r) > 2: e["id"] = r[2]
        out.append(e)
    return out

def total(rows): return sum(r["count"] for r in rows)

HEALTH_NOTE = ("A Final Nemesis of the Monster Hunt (the last fight of {hunter}'s hunt, {printed} Health on its hero card). In the one "
    "mode it is a major of act II beside Hagatha: Health is the act curve at the major's floor plus 10 (rule adaptation "
    "health-policy), not the printed {printed}.")

def boss(rid, name, hero, power, archetype, printed, hunter, page, deck_rows, flags=None, cards_=None, start=None, adaptations=()):
    b = O([("id", rid), ("name", name), ("provenance", "GIL"), ("family", "23"), ("hero", hero), ("power", power), ("tier", "major2"),
           ("archetype", archetype), ("level", O([("source", "printed-health"), ("original", printed)])), ("healthOffset", 10)])
    if start: b["start"] = start
    if flags: b["flags"] = flags
    if cards_: b["cards"] = cards_
    b["source"] = O([("url", WIKI + page), ("read", READ)])
    b["lists"] = [O([("label", "Deck"), ("cards", deck_rows)])]
    b["deck"] = O([("list", "Deck")])
    notes = [O([("kind", "health"), ("note", HEALTH_NOTE.format(hunter=hunter, printed=printed))])]
    notes += [O([("kind", k), ("note", n)]) for k, n in adaptations]
    b["adaptations"] = notes
    return b

toki = cards(("Clockwork Assistant", 1, "GILA_907"), ("Enhance-a-matic", 1, "GILA_903"), TOUCH3, ("Alternate Timeline", 1, "GILA_BOSS_48t"),
    ("Babbling Book", 2), ("Open the Waygate", 1), ("Arcanologist", 2), ("Primordial Glyph", 2), ("Sorcerer's Apprentice", 2),
    ("Unstable Portal", 2), ("Counterspell", 1), ("Effigy", 1), ("Frost Nova", 1), ("Ice Block", 1), ("Mana Bind", 1), ("Mirror Entity", 1),
    ("Spellslinger", 2), ("Cabalist's Tome", 2), ("Blizzard", 1), ("Kabal Crystal Runner", 2), ("Loatheb", 1))
glinda = cards(TOUCH3, ("Crowskin Faithful", 3, "GILA_BOSS_49t"), ("Crowskin Pact", 2, "GILA_BOSS_49t2"), ("Dark Cultist", 1),
    ("Kobold Librarian", 1), ("Fiendish Circle", 1), ("Hellfire", 2), ("Imp-losion", 2), ("Pit Lord", 2), ("Siphon Soul", 1),
    ("Kabal Trafficker", 2), ("Abyssal Enforcer", 2), ("Twisting Nether", 1), ("Voidlord", 2), ("DOOM!", 1), ("Deathlord", 1),
    ("Cult Master", 2), ("Splitting Festeroot", 2))
godfrey = cards(("Hunter's Insight", 1, "GILA_503"), ("Hunter's Mark", 2), ("Dart Trap", 1), ("Explosive Trap", 1), ("Hunting Mastiff", 2),
    ("Exploding Bloatbat", 2), ("Savannah Highmane", 2), ("Seeping Oozeling", 2), ("Crushing Walls", 2), ("Gahz'rilla", 1),
    ("Acolyte of Pain", 2), ("Saboteur", 2), ("Twilight Summoner", 2), ("Bittertide Hydra", 1), ("Clockwork Automaton", 2),
    ("Grim Patron", 2), ("Gurubashi Berserker", 2), ("Hogger, Doom of Elwynn", 1))
shivers = cards(("Cursed Crewmember", 2, "GILA_BOSS_60t"), ("Bearshark", 2), ("Deadly Poison", 2), ("Doomerang", 2), ("Leeching Poison", 1),
    ("Blade Flurry", 2), ("Envenom Weapon", 1), ("Southsea Squidface", 2), ("Tinker's Sharpsword Oil", 2), ("Trade Prince Gallywix", 1),
    ("Dead Man's Hand", 2), ("Acidic Swamp Ooze", 1), ("Lorewalker Cho", 1), ("Violet Illusionist", 2), ("Captain Greenskin", 1),
    ("Fel Reaver", 2), ("Loatheb", 1), ("Troggzor the Earthinator", 1), ("Clockwork Giant", 2))
for label, rows, n in (("toki", toki, 30), ("glinda", glinda, 30), ("godfrey", godfrey, 30), ("shivers", shivers, 30)):
    if total(rows) != n: raise SystemExit("%s sums to %d, the wiki table to %d" % (label, total(rows), n))

ROWS = [
    boss("gila_48_infinite_toki", "Infinite Toki", "GILA_BOSS_48h", "GILA_BOSS_48p", "control", 60, "Toki, Time-Tinker", "Infinite_Toki", toki,
         cards_=["GILA_BOSS_48e"], adaptations=[
            ("deck", "Alternate Timeline (GILA_BOSS_48t, \"Start a new game, but your opponent's Health stays the same.\") restarts the "
                "game: that needs a run-owned game restart (the turn-rewind family, phase 9), so the card is left out and rejected with "
                "that reason. Enhance-a-matic (GILA_903, Toki's treasure, \"Add a random spell from each class to your hand\") can "
                "yield a Secret of any class: a boss may hold such a card only on a row that turns its generated Secrets on with a "
                "closed, boss-admitted Secret pool for every class it names (eleven), which is not lifted here, so it is left out too. "
                "A major keeps its listed count: the encounters step fills the two places with second copies of the list's first "
                "singles (recorded with the deck)."),
            ("power", "Chronoacceleration (2), \"Give a friendly minion Mega-Windfury. At the end of your turn, it dies.\": the client's "
                "own enchantment Accelerated (GILA_BOSS_48e, \"Mega-Windfury. Will die at the end of this turn.\"); the minion dies at "
                "the end of the turn it was given it (Power Overwhelming's rule). The wiki's note that she rarely uses it is the AI's "
                "choice here, as for every clickable power."),
         ]),
    boss("gila_49_glinda", "Glinda Crowskin", "GILA_BOSS_49h", "GILA_BOSS_49p", "control", 50, "Houndmaster Shaw", "Glinda_Crowskin_(boss)", glinda,
         flags=["passive", "heroImmune"], adaptations=[
            ("power", "False Heart, \"Passive Hero Power. While you control a minion, your hero is Immune.\": family 06's hero aura "
                "(Actions.EncounterRules.HeroImmuneWhile, ULDA_706 No-Face's rule for one side), re-checked at every aura update; a "
                "dormant or permanent minion (an untouchable card) does not count."),
            ("deck", "Crowskin Pact (GILA_BOSS_49t2), \"Transform a minion into a 'Crowskin Faithful' and take control of it\": any "
                "minion; a run shrine picked is destroyed and goes dormant instead (the transform rule of Actions.Generic) and never "
                "changes sides; a Faithful taken with the caster's board full is destroyed (the engine's rule for a control change)."),
         ]),
    boss("gila_59_godfrey", "Lord Godfrey", "GILA_BOSS_59h", "GILA_BOSS_59p", "midrange", 60, "Darius Crowley", "Lord_Godfrey_(boss)", godfrey,
         adaptations=[
            ("power", "Pistol Barrage (1), \"Deal 2 damage to a minion and the minions next to it\": a hero power, so no Spell Damage; "
                "the three hits are one damage event (Explosive Shot's rule)."),
         ]),
    boss("gila_60_shivers", "Captain Shivers", "GILA_BOSS_60h", "GILA_BOSS_60p", "tempo", 75, "Tess Greymane", "Captain_Shivers", shivers,
         flags=["start", "passive"], start=O([("bossWeapon", "LOOT_542")]), adaptations=[
            ("start", "He starts with Kingsbane (LOOT_542) equipped (the wiki's note); its Deathrattle shuffles it into his deck."),
            ("power", "Plunder (0), \"Draw a weapon from your deck\", is auto-cast on the wiki (the client card carries AI_MUST_PLAY): "
                "it is used by rule, not by the AI's choice, at the start of his turn while his deck holds a weapon (a random one, by "
                "the game RNG): in practice Kingsbane after its Deathrattle. Not offered as a click (family 09's AutoCast rule)."),
         ]),
]

REJECTED = [O([("id", "GILA_BOSS_48t"), ("name", "Alternate Timeline (Infinite Toki's boss card)"), ("power", None),
    ("reason", "\"Start a new game, but your opponent's Health stays the same\": a game restart inside a fight needs a run-owned "
        "restart that rebuilds the match, the client history and the bot state; left out of Infinite Toki's deck until then"),
    ("lift", "phase 9 (economy): a run-owned turn or game restart that rebuilds the client history and the bot state (the turn-rewind family with GILA_900p Temporal Loop and GILA_911 Rewind)"),
    ("cards", [])])]

DOC = O([
    ("schemaVersion", 1), ("family", "23"),
    ("description", "Monster Hunt nemeses (phase 8 family 23): the four Final Nemesis encounters of The Witchwood's Monster Hunt "
        "(Infinite Toki, Glinda Crowskin, Lord Godfrey, Captain Shivers) as act II majors beside Hagatha the Witch (her row is "
        "in F01.json: family 01 built the run player's fight-only hero cards on her). Boss deck lists are transcribed from "
        "hearthstone.wiki.gg boss pages (read " + READ + "), by card name; tools/dungeon-run-catalog (step 'encounters') resolves "
        "the names against facts/violet-hold-2026-09.json, checks every card and writes data/dungeon-run/encounters-v1.json."),
    ("method", "Each boss page was read as wikitext (?action=raw) by verify/tools/gen/p8f23/make_f23.py's author and its one "
        "deck table transcribed in table order with the table's own counts; every table sums to 30 (checked by the script). "
        "Boss-only and Monster Hunt treasure cards carry their id; the wiki's row name \"Witchwood's Touch (3)\" is the card "
        "GILA_BOSS_99t3 (the wiki's page for it lists exactly Glinda Crowskin and Infinite Toki). The pages' notes are "
        "followed: Infinite Toki casts Alternate Timeline as soon as she can (left out here, rejected), Captain Shivers starts "
        "with Kingsbane equipped and his Plunder is auto-cast."),
    ("bosses", ROWS), ("rejected", REJECTED),
])

repo = sys.argv[1] if len(sys.argv) > 1 else os.path.dirname(V)
out = os.path.join(repo, "data", "dungeon-run", "reference", "encounters", "F23.json")
text = json.dumps(DOC, indent=1, ensure_ascii=False) + "\n"
open(out, "w", encoding="utf-8", newline="\n").write(text)
print("wrote %s: %d rows, %d rejected" % (out, len(ROWS), len(REJECTED)))
