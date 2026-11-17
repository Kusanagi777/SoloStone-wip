# Phase 8 family 11: writes data/dungeon-run/reference/encounters/F11.json, the hand transcription of the thirteen Monster Hunt
# boss pages of family 11 (hearthstone.wiki.gg, read 2026-11-14 with ?action=raw; each deck table transcribed in table order with
# its own counts). Deterministic: LF, no BOM, the same bytes on every run.
# Usage: python3 -I verify/tools/gen/p8f11/make_f11.py [<repo>]      (default: the repository this file sits in)
import json, os, sys, collections
OD = collections.OrderedDict
V = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))  # verify/
REPO = sys.argv[1] if len(sys.argv) > 1 else os.path.dirname(V)
READ = "2026-11-14"
WIKI = "https://hearthstone.wiki.gg/wiki/"
TOUCH = OD([("name", "Witchwood's Touch"), ("count", 2), ("id", "GILA_BOSS_99t2")])
# Witchwood's Touch (GILA_BOSS_99t2) is family 23's boss card, scripted since the wave-6 merge: its copies stay in the decks
# (the Shield Block stand-in, its replacement entries and the deck notes saying so were removed then).

def cards(*pairs):
    out = []
    for p in pairs:
        if isinstance(p, OD): out.append(p); continue
        name, n = p[0], p[1]
        e = OD([("name", name), ("count", n)])
        if len(p) > 2: e["id"] = p[2]
        out.append(e)
    return out

def schedule(rounds, printed, tier):
    return OD([("kind", "schedule"), ("note",
        "The wiki's Monster Hunt page lists the boss in round %s of the hunt's eight; a boss's Health there follows the round. Here the "
        "tier comes from the printed Health of its hero card (%d: tier %s), as family 10's Monster Hunt rows, and the fight's Health is "
        "the act curve's." % (rounds, printed, tier))])

def extended(listed):
    return OD([("kind", "deck"), ("note",
        "The wiki lists a deck of %d cards. A combat tier fights with 30: the listed cards, a second copy of each non-Legendary single "
        "in table order, then the curated fillers named in the row." % listed)])

def power(note): return OD([("kind", "power"), ("note", note)])

def row(n, rid, name, page, tier, printed, rounds, archetype, deck, adaptations, **extra):
    r = OD([("id", rid), ("name", name), ("provenance", "GIL"), ("family", "11"), ("hero", "GILA_BOSS_%sh" % n), ("power", "GILA_BOSS_%sp" % n),
            ("tier", tier), ("archetype", archetype), ("level", OD([("source", "printed-health"), ("original", printed)]))])
    for key in ("flags", "cards"):
        if key in extra: r[key] = extra.pop(key)
    r["source"] = OD([("url", WIKI + page), ("read", READ)])
    r["lists"] = extra.pop("lists", None) or [OD([("label", "Deck"), ("cards", deck)])]
    r["deck"] = extra.pop("deckSpec", OD([("list", "Deck")]))
    for key in ("forms", "replacements", "fillers", "v4"):
        if key in extra: r[key] = extra.pop(key)
    r["adaptations"] = [schedule(rounds, printed, tier)] + adaptations
    if extra: raise SystemExit("unknown keys " + ",".join(extra))
    return r

PETE_DECK = cards(("Backstab", 2), ("Counterfeit Coin", 1), ("Preparation", 1), ("Cold Blood", 2), ("Spore Hallucination", 1, "UNG_856"), ("Swashburglar", 2),
              ("Cheap Shot", 2), ("Cutpurse", 2), ("Defias Ringleader", 2), ("Eviscerate", 2), ("Fan of Knives", 1), ("Pick Pocket", 1), ("Burgle", 1),
              ("Edwin VanCleef", 1), ("SI:7 Agent", 1), ("Sprint", 1), ("Kidnapper", 1), ("Vanish", 1), ("Worgen Infiltrator", 1), ("Wretched Tiller", 1),
              ("Gilblin Stalker", 2), ("Gadgetzan Auctioneer", 1))

BOSSES = [
    row("40", "gila_40_splintergraft", "Splintergraft", "Splintergraft_(boss)", "3", 40, "4-5", "midrange",
        cards(("Forbidden Ancient", 2), ("Naturalize", 2), ("Wild Growth", 4), ("Crypt Lord", 2), ("Grove Tender", 2), ("Soul of the Forest", 2),
              ("Poison Seeds", 2), ("Force of Nature", 2), ("Living Mana", 2), ("Nourish", 2), ("Ancient of Lore", 2), ("Ancient of War", 2),
              ("Rotten Applebaum", 2), ("Splitting Festeroot", 2)),
        [power("Grafted Bark (2) copies a minion of either board into the boss's hand as a fresh card of its kind, set to 2/2 and Cost (2) "
               "(Petrified, GILA_BOSS_40e); the copy is a generated card, so a minion whose card the run's generation filter keeps from the "
               "boss is not a legal choice. Offered only while the hand has room.")],
        cards=["GILA_BOSS_40e"], fillers=["Wild Growth", "Ancient of Lore", "Ancient of War"]),
    row("41", "gila_41_brushwood", "Brushwood Centurion", "Brushwood_Centurion", "4", 60, "4-5", "midrange",
        cards(("Hack", 4, "GILA_BOSS_41t"), ("Gahz'rilla", 1), ("Execute", 1), ("Slam", 1), ("Whirlwind", 2), ("Armorsmith", 2), ("Battle Rage", 1),
              ("Cleave", 1), ("Commanding Shout", 2), ("Rampage", 1), ("Frothing Berserker", 3), ("Val'kyr Soulclaimer", 2), ("Alley Armorsmith", 1),
              ("Brawl", 1), ("Rotface", 1), ("Grim Patron", 2), ("Gurubashi Berserker", 3), ("Hogger, Doom of Elwynn", 1)),
        [power("Survival of the Fittest (3; the wiki's Monster Hunt table calls it Axe Around): every minion of both boards, in the order "
               "they entered play, attacks a random enemy minion, as Mass Hysteria's attacks do (whatever its state; Taunt and Stealth do not "
               "matter); each attack's deaths resolve before the next; a minion with no enemy minion left does not attack; the heroes are "
               "never attacked. Offered only while both boards hold a minion."),
         power("Hack (3), \"Deal 1 damage to a minion. Then do it four more times\": five hits on the chosen minion, each its own damage "
               "event with Spell Damage (the deck's Frothing Berserkers and Armorsmiths count each); the hits stop once it is dead.")],
        flags=["forcedAttack"], fillers=["Fiery War Axe", "Shield Block"]),
    row("42", "gila_42_baran", "Baran the Blind", "Baran_the_Blind", "2", 30, "4-5", "control",
        cards(("Iron Hide", 1), ("Whirlwind", 2), ("Armorsmith", 2), ("Bring It On!", 1), ("Drywhisker Armorer", 1), ("Rampage", 1), ("Frothing Berserker", 2),
              ("Mountainfire Armor", 1), ("Reckless Flurry", 2), ("Shield Block", 1), ("Axe Flinger", 1), ("Kor'kron Elite", 2), ("Militia Commander", 2),
              ("Alley Armorsmith", 2), ("Ironforge Portal", 1), ("Gemstudded Golem", 2), ("Shieldmaiden", 1), ("Lesser Mithril Spellstone", 1),
              ("Geosculptor Yip", 1), ("Rotface", 1), ("Tauren Warrior", 2)),
        [power("Bulldoze (1) spends all the boss's Armor (it is gone) as one hit of that much damage on a random minion of either board (a "
               "hero power: no Spell Damage). Offered only while the boss has Armor and a minion is on a board.")],
        fillers=["Shield Block", "Armorsmith", "Fiery War Axe"]),
    row("43", "gila_43_damph", "Gravekeeper Damph", "Gravekeeper_Damph", "3", 35, "3", "midrange",
        cards(("Corpse Widow", 1), ("Resurrect", 1), ("Twilight's Call", 2), ("Coffin Crasher", 1), ("Entomb", 1), ("Lesser Diamond Spellstone", 1),
              ("Roll the Bones", 1), ("Undertaker", 2), ("Loot Hoarder", 1), ("Unstable Ghoul", 1), ("Flesheating Ghoul", 1), ("Shallow Gravedigger", 2),
              ("Piloted Shredder", 1), ("Feugen", 1), ("Stalagg", 1), ("Sneed's Old Shredder", 1), ("N'Zoth, the Corruptor", 1)),
        [extended(20),
         power("Grave Mistake (3), \"Trigger all minions' Deathrattles twice\": every minion of both boards, in the order they entered play, "
               "has each of its Deathrattles (its card's and its enchantments') resolve twice for its own controller, as Feign Death "
               "triggers them; a silenced minion's card Deathrattle stays quiet. Offered only while a minion with a Deathrattle is on a "
               "board. Grave Mistake the spell (GILA_BOSS_43t, 0 Cost) is the treasure the hunt drops after this fight and is in no deck "
               "row; it is scripted and admitted for both sides, and tested from a fixture boss's hand.")],
        flags=["doubleDeathrattle"], fillers=["Harvest Golem", "Haunted Creeper", "Kindly Grandmother", "Sludge Belcher", "Abomination"],
        # Step 9 (D9): on a stamp-4 run (acts-v4h, acts-v4t) beating Damph grants the hunt's drop, Grave Mistake the spell.
        v4=OD([("reward", "GILA_BOSS_43t")])),
    row("44", "gila_44_sezavo", "Doctor Sezavo", "Doctor_Sezavo", "1", 20, "2-3", "aggro",
        cards(("Northshire Cleric", 2), ("Power Word: Shield", 2), ("Shadowboxer", 2), ("Mortal Coil", 2), ("Leper Gnome", 1), ("Lightwarden", 1),
              ("Voodoo Doctor", 1), ("Amani Berserker", 1), ("Loot Hoarder", 2), ("Novice Engineer", 2), ("Backstreet Leper", 1), ("Earthen Ring Farseer", 1),
              ("Chillwind Yeti", 1), ("Refreshment Vendor", 1)),
        [extended(20),
         OD([("kind", "schedule"), ("note", "Printed Health 20 lies between family 10's tier 1 (15) and tier 2 (25, 30) values: 20 or less "
               "is tier 1 here, the tier of the hunt's other round 2-3 boss with low printed Health (Ravencaller Cozzlewurt, 15).")]),
         power("Hollow Needle (2) deals 1 damage to any minion (a hero power: no Spell Damage) and restores 3 Health to the boss's hero when "
               "that hit leaves the minion dead.")],
        fillers=["Flash Heal", "Zombie Chow", "Injured Blademaster", "Holy Smite"]),
    row("51", "gila_51_garrow", "Garrow, the Rancorous", "Garrow,_the_Rancorous", "3", 40, "4-5", "midrange",
        cards(("Forbidden Healing", 2), ("Cutpurse", 2), ("Pick Pocket", 2), ("Burgle", 2), ("Tomb Pillager", 2), ("Cornered Sentry", 4),
              ("Gravelsnout Knight", 2), ("Dirty Rat", 2), ("Hungry Dragon", 2), ("Rattling Rascal", 2), ("Burgly Bully", 2), ("Leeroy Jenkins", 1),
              ("Muck Hunter", 2), ("Hungry Ettin", 2), ("The Beast", 1)),
        [power("Death and Taxes (1) deals the enemy hero one hit of 1 damage for each minion its controller has on the board (a dormant "
               "minion and a permanent, such as a dormant run shrine, are not counted: they are not minions an effect can pick). Offered "
               "only while the enemy has such a minion.")],
        fillers=["Pick Pocket", "Cutpurse", "Dirty Rat"]),
    row("52", "gila_52_pete", "Crooked Pete", "Crooked_Pete", "3", 40, "4-5", "tempo",
        PETE_DECK,
        [OD([("kind", "power"), ("note",
               "The wiki (Crooked Pete, Notes): \"When Crooked Pete is at 25 Health or less, he transforms into Beastly Pete at the start "
               "of his turn. After that: Crooked Pete's deck is replaced by Beastly Pete's; Crooked Pete's hand is removed, then Beastly "
               "Pete draws 6 cards; Beastly Pete gains 15 Armor\"; the Monster Hunt table: \"At 1/2 Health\". Here: a script form "
               "(GILA_BOSS_52h2 with Beast Within, GILA_BOSS_52p2) entered by Soot Up's start-of-turn trigger when Pete's Health is half "
               "his maximum or less (the act curve's maximum, so the threshold scales with it). He keeps his Health and maximum (not the "
               "portrait's printed 50), his hand goes to the set-aside zone, Beastly Pete's list replaces his deck and he draws 6 from "
               "it, and he gains 15 Armor. A Pete killed outright before such a turn start dies, as in the original (the phase-8 design "
               "had sketched an on-death form; the wiki's rule is the one implemented).")]),
         power("Soot Up (1) gives a minion of the boss Stealth until his next turn (Covered in Soot); a minion that has Stealth already is "
               "not a choice. Beast Within (1 in the client data; 2 in the wiki's table) gives the boss's minions +1 Attack for good (Beast "
               "Mode).")],
        flags=["forms", "deckReplace"], cards=["GILA_BOSS_52e", "GILA_BOSS_52e2"],
        lists=[OD([("label", "Deck"), ("cards", PETE_DECK)]), OD([("label", "Beastly Pete"), ("cards", cards(TOUCH, ("Feral Rage", 2), ("Ferocious Howl", 2),
              ("Savage Roar", 2), ("Swipe", 2), ("Bite", 2), ("Branching Paths", 2), ("Savage Combatant", 2), ("Rat Pack", 3), ("Knuckles", 1),
              ("Savannah Highmane", 2), ("Haunted Creeper", 2), ("Meat Wagon", 2), ("Night Howler", 2), ("Bloodworm", 2)))])],
        forms=[OD([("hero", "GILA_BOSS_52h2"), ("power", "GILA_BOSS_52p2"), ("at", OD([("script", True)])), ("deck", OD([("list", "Beastly Pete")])), ("draw", 6)])],
        fillers=["Backstab", "Cutpurse", "SI:7 Agent"]),
    row("54", "gila_54_cragtorr", "Cragtorr", "Cragtorr", "3", 40, "6-7", "control",
        cards(TOUCH, ("Jungle Giants", 1), ("Wild Growth", 2), ("Grove Tender", 2), ("Poison Seeds", 2), ("Nourish", 2), ("Ancient of Lore", 1),
              ("Ancient of War", 2), ("Giant Anaconda", 2), ("Ironbark Protector", 1), ("Emperor Thaurissan", 1), ("Rotten Applebaum", 1), ("Sea Giant", 2),
              ("Y'Shaarj, Rage Unbound", 1), ("Arcane Giant", 2), ("Clockwork Giant", 2), ("Mountain Giant", 2), ("Molten Giant", 2)),
        [power("One with the Trees (2) makes every minion the boss holds cost (1) less (Assimilation, GILA_BOSS_54e). Offered only while it "
               "holds a minion that costs (1) or more.")],
        cards=["GILA_BOSS_54e"], fillers=["Ancient of Lore", "Ironbark Protector", "Wild Growth"]),
    row("55", "gila_55_azalina", "Azalina Soulthief", "Azalina_Soulthief_(boss)", "4", 50, "6-8", "midrange",
        cards(OD([("name", "Witchwood's Touch"), ("count", 1), ("id", "GILA_BOSS_99t2")]), ("Soul Assimilation", 2, "GILA_BOSS_55t2"), ("Wispering Woods", 2),
              ("Dark Wispers", 2), ("Wisps of the Old Gods", 2), ("Kun the Forgotten King", 1), ("Avenge", 2), ("Competitive Spirit", 2), ("Eye for an Eye", 1),
              ("Noble Sacrifice", 1), ("Divine Favor", 2), ("Sword of Justice", 1), ("Spirit Lash", 2), ("Spirit Echo", 2), ("Knife Juggler", 2),
              ("Lost Spirit", 3), ("Defender of Argus", 2)),
        [power("Unfinished Business (2) summons three 1/1 Wisps (GILA_BOSS_55t), as many as the board has room for. Soul Assimilation (4) "
               "destroys the caster's Wisps (every card the client names Wisp, the list EMERALD_DREAM Divination reads: the Wisps of this "
               "power, Wispering Woods and Wisps of the Old Gods among them), lets their deaths resolve, then takes control of one random "
               "enemy minion per Wisp destroyed (one taken while the caster's board is full is destroyed, the engine's control rule; a run "
               "shrine never changes sides).")],
        cards=["GILA_BOSS_55t"], fillers=["Knife Juggler", "Defender of Argus", "Divine Favor"]),
    row("56", "gila_56_face_collector", "Face Collector", "Face_Collector_(boss)", "4", 60, "6-7", "midrange",
        cards(("Backstab", 2), ("Deadly Poison", 2), ("SI:7 Agent", 2), ("Eviscerate", 2), ("Sap", 2), ("Fan of Knives", 2), ("Assassinate", 2), ("Sprint", 1),
              ("Defias Ringleader", 2), ("Loot Hoarder", 2), ("Harvest Golem", 2), ("Earthen Ring Farseer", 2), ("Chillwind Yeti", 2),
              ("Sen'jin Shieldmasta", 2), ("Boulderfist Ogre", 1), ("Azure Drake", 2)),
        [OD([("kind", "deck"), ("note",
               "The wiki: \"This boss plays a deck identical to the player's.\" Flag mirrorDeck: the boss's deck is a copy of the run deck "
               "as this fight plays it, less the cards the admission table does not admit for a boss (DungeonRun.ApplyEncounter); the row's "
               "own deck, a curated Rogue list of basic and classic cards (the hero's class) the wiki does not give, is used only when fewer "
               "than 10 such cards remain.")]),
         power("A New Face (0) transforms a minion of either board into a random scripted minion whose Cost is its card's Cost + 2 (any "
               "class, behind the run's generation filter for the side that controls it); a minion with no such minion is not a choice. A "
               "run shrine chosen is destroyed instead and goes dormant, as from any transform.")],
        flags=["mirrorDeck"], deckSpec=OD([("list", "Deck"), ("kind", "curated")])),
    row("57", "gila_57_raeth", "Raeth Ghostsong", "Raeth_Ghostsong", "4", 60, "6-8", "control",
        cards(("Shallow Graves", 4, "GILA_BOSS_57t"), ("Resurrect", 2), ("Acolyte of Agony", 2), ("Lesser Diamond Spellstone", 2), ("Corrupting Mist", 2),
              ("Twisting Nether", 2), ("Acolyte of Pain", 2), ("Flesheating Ghoul", 2), ("Shallow Gravedigger", 2), ("Cult Master", 2), ("Carnivorous Cube", 2),
              ("Gurubashi Berserker", 2), ("Cairne Bloodhoof", 1), ("Moat Lurker", 2), ("Sylvanas Windrunner", 1)),
        [power("Desecrate (1) deals 1 damage to every minion of both boards (a hero power: no Spell Damage); when a hit leaves one dead, the "
               "power is readied again after the use (its own Inspire trigger, as Darius Crowley's Fire! is), so each use still costs its "
               "Mana. Offered only while a minion is on a board."),
         power("Shallow Graves (9), \"Summon 7 random minions that died this game\": fresh minions of up to seven random deaths of either "
               "player this game (one per death; the graveyards are public), for the caster, while its board has room. A dead run shrine "
               "(ShrineRules.IsCorpse), a permanent and a card the run's generation filter keeps from the caster are never summoned.")],
        fillers=["Acolyte of Pain", "Flesheating Ghoul"]),
    row("58", "gila_58_grum", "Grum", "Grum", "3", 40, "4-5", "midrange",
        cards(("Ogre Ninja", 1), ("I Know a Guy", 2), ("Slam", 2), ("Bash", 2), ("Battle Rage", 2), ("Bring It On!", 2), ("Rampage", 2), ("Sparring Partner", 2),
              ("Crush", 2), ("Ogre Brute", 2), ("Ogre Magi", 1), ("Boulderfist Ogre", 1), ("Mogor's Champion", 1), ("Blood of The Ancient One", 2),
              ("Frost Giant", 1), ("Sea Giant", 1), ("Arcane Giant", 1), ("Clockwork Giant", 1), ("Mountain Giant", 1), ("Molten Giant", 1)),
        [OD([("kind", "deck"), ("note", "Three Ogres of the wiki's list are collectible cards with no script in this engine (Ogre Ninja, Ogre "
               "Brute, Mogor's Champion: the 50% wrong-target Ogres no phase 8 family scripts). Curated replacements, as for Grubb in family "
               "10: Stranglethorn Tiger, Ogre Magi and Hungry Ettin.")]),
         power("Terrify (4; 3 in the wiki's table) shuffles a minion of either board into its own controller's deck through the engine's "
               "MoveToDeck, as Recycle's returns are (the card itself, reset as that task resets it: its damage gone), and then makes it cost (4) less "
               "there (Terrified, GILA_BOSS_58e). A minion whose owner's deck is full is not a choice. A run shrine chosen is destroyed "
               "instead and goes dormant (ShrineRules.InterceptLeave); it never enters a deck.")],
        cards=["GILA_BOSS_58e"], replacements=OD([("Ogre Ninja", "Stranglethorn Tiger"), ("Ogre Brute", "Ogre Magi"), ("Mogor's Champion", "Hungry Ettin")]),
        fillers=["Slam", "Shield Block", "Boulderfist Ogre"]),
    row("62", "gila_62_gustave", "Gustave, the Gutripper", "Gustave,_the_Gutripper", "4", 50, "5", "midrange",
        cards(TOUCH, ("Addled Grizzly", 2), ("Malorne", 1), ("Bestial Wrath", 2), ("Smuggler's Crate", 2), ("Call Pet", 2), ("Trogg Beastrager", 2),
              ("Bearshark", 2), ("Kill Command", 2), ("Savannah Highmane", 2), ("Swamp King Dred", 1), ("Gahz'rilla", 1), ("King Krush", 1),
              ("River Crocolisk", 2), ("King Mukla", 1), ("Carnivorous Cube", 2), ("Mukla's Champion", 2), ("Stampeding Kodo", 1)),
        [power("Cull the Meek (1) destroys a minion with the lowest Attack among the minions of both boards an effect can pick; a tie is "
               "broken by the game RNG. The wiki marks it auto-cast; here the boss's AI chooses when to pay for it, as family 08's Ixlid and "
               "family 10's Sazzmi, and it is offered only while a minion is on a board.")],
        fillers=["Kill Command", "Bearshark", "River Crocolisk"]),
]

def main():
    doc = OD([("schemaVersion", 1), ("family", "11"),
        ("description", "Monster Hunt bosses B (phase 8 family 11): thirteen encounters of The Witchwood's Monster Hunt with a clickable hero "
            "power (Splintergraft, Brushwood Centurion, Baran the Blind, Gravekeeper Damph, Doctor Sezavo, Garrow, the Rancorous, Crooked "
            "Pete, Cragtorr, Azalina Soulthief, Face Collector, Raeth Ghostsong, Grum, Gustave, the Gutripper), Crooked Pete's form change "
            "into Beastly Pete, and their boss-only cards. Boss deck lists are transcribed from hearthstone.wiki.gg boss pages (read "
            "2026-11-14), by card name; tools/dungeon-run-catalog (step 'encounters') resolves the names against "
            "facts/violet-hold-2026-09.json, checks every card and writes data/dungeon-run/encounters-v1.json. The tier is the printed "
            "Health of the hero card (20: tier 1; 30: tier 2; 35 and 40: tier 3; 50 and 60: tier 4)."),
        ("method", "Each boss page was read as wikitext (?action=raw) and its one deck table transcribed in table order with the table's own "
            "counts (Crooked Pete's page has two tables, Crooked Pete and Beastly Pete); every table sums to 30 except Gravekeeper Damph's "
            "and Doctor Sezavo's (20 each). Face Collector's page has no table (\"This boss plays a deck identical to the player's\"). "
            "Boss-only cards carry their id; the wiki's row name \"Witchwood's Touch (2)\" is written as the card's name with its id, and "
            "\"Spore Hallucination\" (Crooked Pete), the wiki's current name of the card the client data still calls Hallucination (dbf "
            "42011), carries the id UNG_856. "
            "Rules beyond the card texts come from the pages' Notes (Crooked Pete) and the Monster Hunt page's boss table (read "
            "2026-11-14); what a text leaves open is decided in the row's adaptation notes. Generated by "
            "verify/tools/gen/p8f11/make_f11.py."),
        ("bosses", BOSSES), ("rejected", [])])
    text = json.dumps(doc, indent=1, ensure_ascii=False) + "\n"
    out = os.path.join(REPO, "data", "dungeon-run", "reference", "encounters", "F11.json")
    with open(out, "w", encoding="utf-8", newline="\n") as f: f.write(text)
    for b in BOSSES:
        for l in b["lists"]:
            n = sum(c["count"] for c in l["cards"])
            print("%-26s %-14s %2d" % (b["id"], l["label"], n))
    print("wrote %s: %d rows" % (out, len(BOSSES)))

main()
