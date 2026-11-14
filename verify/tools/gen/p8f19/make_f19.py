#!/usr/bin/env python3
# Phase 8 family 19 (Tombs of Terror bosses A, ULDA_BOSS_01-18): writes data/dungeon-run/reference/encounters/F19.json, the
# hand transcription of the eighteen boss pages of hearthstone.wiki.gg (read 2026-11-14). Run from anywhere:
#   python3 -I verify/tools/gen/p8f19/make_f19.py
# The deck tables were read twice (the raw wikitext and the rendered table of the MediaWiki API, action=parse) and agree;
# each Normal table sums to the total its level note names (25 or 30; the "10; 15" and "15; 20" tables list possible cards
# without counts). The tier rule is tombs_tiers.py (next to this file; families 20-22 import it too). Output: LF, no BOM,
# one-space indent (as F10.json), keys in the order written here.
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import tombs_tiers  # noqa: E402

REPO = os.path.abspath(os.path.join(HERE, "..", "..", "..", ".."))
OUT = os.path.join(REPO, "data", "dungeon-run", "reference", "encounters", "F19.json")
READ = "2026-11-14"
WIKI = "https://hearthstone.wiki.gg/wiki/"

# Replacements: a listed card the engine does not script (or admit for a boss) is replaced by a scripted card of the same role,
# named per row (the encounters step records each one). All the unscripted ones are Saviors of Uldum collectibles no phase-8
# family lists; a rerun of the encounters step picks the listed card up once it is scripted.
UNSCRIPTED_NOTE = ("Listed cards the engine does not script yet are replaced by a scripted card of the same role, named in the row's "
                   "replacements (the encounters step records each one): ")


def cards(names):
    out = []
    for item in names:
        name, count = item if isinstance(item, tuple) else (item, None)
        entry = {"name": name, "count": count}
        if name == "Mirage Blade":
            entry["id"] = "ULD_326t"
        out.append(entry)
    return out


def boss(n, slug, name, page, chapter, shape, archetype, normal, heroic=None, cards_=None, replacements=None, why=None, notes=()):
    level = tombs_tiers.level(shape)
    label = "Normal (level %d)" % level
    lists = [{"label": label, "cards": cards(normal)}]
    if heroic is not None:
        lists.append({"label": "Heroic", "cards": cards(heroic)})
    row = {
        "id": "ulda_%02d_%s" % (n, slug),
        "name": name,
        "provenance": "ULDA",
        "family": "19",
        "hero": "ULDA_BOSS_%02dh" % n,
        "power": "ULDA_BOSS_%02dp" % n,
        "tier": tombs_tiers.tier(shape),
        "archetype": archetype,
        "level": {"source": "wiki-level-table", "original": level},
    }
    if cards_:
        row["cards"] = cards_
    row["source"] = {"url": WIKI + page, "read": READ}
    row["lists"] = lists
    row["deck"] = {"list": label}
    if heroic is not None:
        row["extend"] = ["Heroic"]
    if replacements:
        row["replacements"] = replacements
    adaptations = [tombs_tiers.schedule_note(chapter, shape)]
    deck = tombs_tiers.deck_note(shape)
    if deck:
        adaptations.append(deck)
    if replacements:
        adaptations.append({"kind": "deck", "note": UNSCRIPTED_NOTE + why})
    for kind, note in notes:
        adaptations.append({"kind": kind, "note": note})
    row["adaptations"] = adaptations
    return row


B = []
B.append(boss(1, "cardish", "Waste Wanderer Cardish", "Waste_Wanderer_Cardish", 2, "4", "aggro", [
    ("Rapid Fire", 2), ("Bomb Toss", 1), ("Pressure Plate", 2), ("Snipe", 2), ("Hunter's Pack", 2), ("Hyena Alpha", 1), ("Marked Shot", 2),
    ("EVIL Miscreant", 2), ("Goblin Bomb", 1), ("EVIL Cable Rat", 2), ("Injured Tol'vir", 2), ("Kobold Sandtrooper", 2), ("Loot Hoarder", 2),
    ("Neferset Ritualist", 2), ("Acolyte of Pain", 2), ("Explodinator", 1), ("Sunstruck Henchman", 2)],
    cards_=["BOT_031", "ULDA_BOSS_01e"],
    replacements={"Hunter's Pack": "Animal Companion", "Injured Tol'vir": "Frostwolf Grunt", "Kobold Sandtrooper": "Leper Gnome",
                  "Sunstruck Henchman": "Chillwind Yeti"},
    why="Hunter's Pack (it can give the boss a random Hunter Secret; this row generates none) by Animal Companion, Injured Tol'vir "
        "by Frostwolf Grunt, Kobold Sandtrooper by Leper Gnome (the same Deathrattle, 2 damage), Sunstruck Henchman by Chillwind Yeti.",
    notes=[("power", "Boom Bullets (2) summons a 0/2 Goblin Bomb (Boomsday's BOT_031) for the boss and gives every other Goblin Bomb on "
                     "either board +1 Attack (\"all other Goblin Bombs\" names no side); it needs a free board slot.")]))
B.append(boss(2, "zafarr", "Dark Ritualist Zafarr", "Dark_Ritualist_Zafarr", 3, "3", "control", [
    ("Ultimate Infestation", 1), ("Grandmummy", 2), ("Shadow Word: Death", 2), ("Shadow Word: Pain", 2), ("Dark Cultist", 2),
    ("Wretched Reclaimer", 2), ("High Priest Amet", 1), ("Convincing Infiltrator", 1), ("Entomb", 2), ("Herald Volazj", 1), ("Murmy", 2),
    ("Temple Berserker", 2), ("Generous Mummy", 2), ("Bone Wraith", 2), ("Body Wrapper", 2), ("Khartut Defender", 2), ("Wrapped Golem", 2)],
    replacements={"Murmy": "Micro Mummy", "Generous Mummy": "Restless Mummy"},
    why="Murmy by Micro Mummy and Generous Mummy by Restless Mummy (both Reborn, so Mummy Ritual keeps as many Reborn minions to "
        "raise).",
    notes=[("power", "Mummy Ritual (3) summons a new copy of a random minion of the boss's that died this game and has Reborn on its card "
                     "(the graveyard is public; a dead run shrine never), with 1 Health (its Health less one as damage, as a Reborn "
                     "return has); the copy keeps its card's Reborn, as the text does not take it away. It needs a free slot and such a "
                     "dead minion.")]))
B.append(boss(3, "vera", "Vera Ridley", "Vera_Ridley", 4, "2", "midrange", [
    ("Weaponized Wasp", 2), ("Sinister Deal", 2), ("Improve Morale", 2), ("Frightened Flunky", 2), ("Rocket Boots", 2), ("Slam", 2),
    ("Fiery War Axe", 2), ("Livewire Lance", 2), ("Alley Armorsmith", 2), ("Brawl", 1), ("EVIL Cable Rat", 2), ("Kobold Sandtrooper", 2),
    ("Vulpera Scoundrel", 2)],
    heroic=[("Heistbaron Togwaggle", 1), ("Sludge Slurper", 2), ("EVIL Totem", 2), ("Sinister Deal", 2), ("EVIL Genius", 2),
            ("Improve Morale", 2), ("Execute", 2), ("Bash", 2), ("Livewire Lance", 2), ("Restless Mummy", 2), ("Brawl", 2),
            ("EVIL Cable Rat", 2), ("Loot Hoarder", 2)],
    cards_=["ULDA_BOSS_03e"],
    replacements={"Weaponized Wasp": "EVIL Genius", "Sinister Deal": "Kobold Lackey", "Livewire Lance": "Goblin Lackey",
                  "Kobold Sandtrooper": "Leper Gnome"},
    why="the three Lackey cards Weaponized Wasp, Sinister Deal and Livewire Lance by Lackey cards the engine scripts, so that "
        "E.V.I.L. Promotion keeps its Lackeys (EVIL Genius, which adds two Lackeys; Kobold Lackey and Goblin Lackey themselves), "
        "Kobold Sandtrooper by Leper Gnome.",
    notes=[("power", "E.V.I.L. Promotion (passive): after the boss plays a Lackey (the seven Lackeys of the engine's Lackey pool) it gives "
                     "that Lackey +N/+N, N starting at 1 and rising by 1 with each Lackey played; N is the power's own script number "
                     "(the @ of its text), so the client shows the current value.")]))
B.append(boss(4, "nash", "Nash the Greatworm", "Nash_the_Greatworm", 2, "3", "midrange", [
    ("Stampede", 2), ("Webspinner", 2), ("Call Pet", 2), ("Grievous Bite", 2), ("Scavenging Hyena", 2), ("Carrion Grub", 2),
    ("Ramkahen Wildtamer", 2), ("Starving Buzzard", 2), ("Tundra Rhino", 1), ("Scarlet Webweaver", 2), ("Wild Bloodstinger", 1),
    ("Giant Sand Worm", 2), ("Golden Scarab", 2), ("Infested Goblin", 2), ("Wasteland Assassin", 2), ("Wasteland Scorpid", 2)],
    notes=[("power", "Belch (3) summons for the boss a new copy of a random minion of either player that died this game (both graveyards "
                     "are public; a dead run shrine never). It needs a free slot and such a dead minion.")]))
B.append(boss(5, "glack", "Glack the Scorpid", "Glack_the_Scorpid", 3, "2", "aggro", [
    ("BEEEES!!!", 2), ("Gnash", 2), ("Bite", 2), ("Spreading Plague", 1), ("The Beast Within", 2), ("Webspinner", 2), ("Call Pet", 2),
    ("Dreadscale", 1), ("Infest", 1), ("Huge Toad", 2), ("Vicious Scalehide", 2), ("Golden Scarab", 2), ("Infested Goblin", 2),
    ("Wasteland Scorpid", 2)],
    heroic=[("Addled Grizzly", 2), ("BEEEES!!!", 2), ("Crypt Lord", 2), ("Savage Roar", 2), ("Alleycat", 2), ("Timber Wolf", 2),
            ("Knife Juggler", 2), ("Scavenging Hyena", 2), ("Unleash the Hounds", 2), ("Halazzi, the Lynx", 1), ("Swarm of Locusts", 2),
            ("Wild Bloodstinger", 2), ("Corridor Creeper", 2)],
    cards_=["ULDA_BOSS_05e"],
    notes=[("power", "Venomous Stinger (2) stings any minion that is not stung yet (enchantment ULDA_BOSS_05e): the next time it takes "
                     "damage it is destroyed (a run shrine goes dormant, as from any destroy effect).")]))
B.append(boss(6, "chu", "Mister Chu", "Mister_Chu", 3, "4", "midrange", [
    ("Mirage Blade", 2), ("I Know a Guy", 2), ("Improve Morale", 2), ("Into the Fray", 1), ("Forge of Souls", 2), ("Frightened Flunky", 2),
    ("Hobart Grapplehammer", 1), ("Grimestreet Pawnbroker", 2), ("Livewire Lance", 2), ("Shield Block", 2), ("Arathi Weaponsmith", 2),
    ("Bloodhoof Brave", 2), ("Brass Knuckles", 1), ("Armored Goon", 2), ("Jar Dealer", 2), ("Hench-Clan Thug", 2), ("Blingtron 3000", 1)],
    replacements={"Livewire Lance": "Fiery War Axe"},
    why="Livewire Lance by Fiery War Axe (a weapon of the same Cost).",
    notes=[("power", "\"Confiscated\" Goods (passive): after the boss's hero attacks a minion that dies of it, the boss equips a random "
                     "scripted collectible weapon of any class the run's generation filter allows for it (\"a random weapon\" names "
                     "no class).")]))
B.append(boss(7, "squeamlish", "Squeamlish", "Squeamlish_(boss)", 2, "4", "midrange", [
    ("Crystal Merchant", 2), ("BEEEES!!!", 2), ("Feral Rage", 2), ("Greedy Sprite", 2), ("Bite", 2), ("Swipe", 2), ("Anubisath Defender", 2),
    ("Oasis Surger", 2), ("Nourish", 1), ("Overflow", 2), ("Ironbark Protector", 2), ("The Forest's Aid", 1), ("EVIL Conscripter", 2),
    ("EVIL Cable Rat", 2), ("Infested Goblin", 2), ("Wrapped Golem", 2)],
    cards_=["ULDA_BOSS_07e"],
    replacements={"Crystal Merchant": "Enchanted Raven"},
    why="Crystal Merchant by Enchanted Raven (a 1-Cost Druid minion).",
    notes=[("power", "Druid of the Rat (2) gives each of the boss's minions +1/+1 (enchantment ULDA_BOSS_07e); offered while it has one.")]))
B.append(boss(8, "sinkhole", "Sinkhole", "Sinkhole", 1, "3", "control", [
    ("BEEEES!!!", 2), ("Swarm of Locusts", 2), ("Giant Sand Worm", 2), ("Sandwasp Queen", 2), ("Dust Devil", 2), ("Sandstorm Elemental", 2),
    ("Rumbling Elemental", 2), ("Earthquake", 2), ("Dire Mole", 1), ("Nat, the Darkfisher", 1), ("Quicksand Elemental", 2), ("Dancing Swords", 2),
    ("Golden Scarab", 2), ("Infested Goblin", 2), ("Squirming Tentacle", 2), ("Wasteland Scorpid", 2)],
    cards_=["ULDA_030"],
    replacements={"Quicksand Elemental": "Tar Creeper"},
    why="Quicksand Elemental by Tar Creeper (a 3-Cost Elemental).",
    notes=[("power", "Sand Surge (1) shuffles one Sand Trap (ULDA_030, the page's special card) into the run player's deck as a public "
                     "shuffle (the run player sees it go in; the boss's fair view keeps that deck masked, the Sand Trap included). Sand Trap: Casts When Drawn, the top 2 cards of "
                     "its holder's deck go to the graveyard (public, as Azari's Devour), then the holder draws a card. It is a match "
                     "card only: the run deck records never hold it. Offered while the run player's deck has room.")]))
B.append(boss(9, "demolisher", "Demolisher 3V-11", "Demolisher_3V-11", 2, "2", "control", [
    ("Eternium Rover", 2), ("Omega Assembly", 2), ("Shield Slam", 2), ("Vicious Scraphound", 2), ("Reckless Flurry", 2),
    ("Dr. Boom's Scheme", 1), ("Siege Engine", 2), ("Gemstudded Golem", 2), ("Heavy Metal!", 2), ("Mechwarper", 2), ("Demolisher", 6)],
    heroic=[("Eternium Rover", 2), ("Warbot", 2), ("Dyn-o-matic", 2), ("Security Rover", 1), ("Tomb Warden", 1), ("Mecharoo", 2),
            ("Galvanizer", 2), ("Mechwarper", 2), ("Demolisher", 1), ("Harvest Golem", 1), ("SN1P-SN4P", 1), ("Piloted Shredder", 2),
            ("Wargear", 2), ("Zilliax", 1), ("Piloted Sky Golem", 2), ("Sneed's Old Shredder", 1)],
    notes=[("power", "Armor Repairs (2): the boss gains 4 Armor while it has less than 10, else 2."),
           ("deck", "The wiki lists six Demolishers in the level-4 deck; all six are kept (a boss deck has no copy limit).")]))
B.append(boss(10, "terraviss", "Terraviss", "Terraviss", 3, "2a", "tempo", [
    "Backstab", "Pilfer", "Clever Disguise", "Defias Ringleader", "Eviscerate", "Whirlkick Master", "EVIL Miscreant", "SI:7 Agent",
    "Academic Espionage", "Hench-Clan Burglar", "Waggle Pick"],
    heroic=[("Preparation", 2), ("Beneath the Grounds", 2), ("Fan of Knives", 2), ("Academic Espionage", 2), ("Fal'dorei Strider", 2),
            ("Bloodmage Thalnos", 1), ("Loot Hoarder", 2), ("Coldlight Oracle", 2)],
    notes=[("power", "Well Equipped (2) adds a random scripted collectible Rogue spell or weapon the run's generation filter allows for the "
                     "boss to its hand; Quests and Secrets are left out of the pool (the boss generates no Secret on this row). Offered "
                     "while its hand has room.")]))
B.append(boss(11, "robold", "R.O.B.O.L.D.", "R.O.B.O.L.D.", 2, "2", "midrange", [
    ("Cybertech Chip", 1), ("Flamecannon", 2), ("Pyromaniac", 2), ("Soot Spewer", 2), ("Fireball", 1), ("Goblin Blastmage", 2),
    ("Flamestrike", 1), ("Tomb Warden", 1), ("Clockwork Gnome", 2), ("Cogmaster", 2), ("Galvanizer", 1), ("Kobold Geomancer", 2),
    ("Kobold Sandtrooper", 2), ("Spark Engine", 2), ("Missile Launcher", 2)],
    heroic=[("Flamecannon", 2), ("Soot Spewer", 2), ("Goblin Blastmage", 1), ("Flame Leviathan", 1), ("Flamestrike", 1), ("Micro Mummy", 2),
            ("Mecharoo", 2), ("Annoy-o-Tron", 2), ("Mechwarper", 2), ("Harvest Golem", 2), ("Tinkertown Technician", 2),
            ("Clockwork Automaton", 2), ("Mechanical Whelp", 2), ("Force-Tank MAX", 2)],
    replacements={"Kobold Sandtrooper": "Annoy-o-Tron"},
    why="Kobold Sandtrooper by Annoy-o-Tron (a 2-Cost Mech, which Unstable Weaponry answers).",
    notes=[("power", "Unstable Weaponry (passive): after a Mech is summoned for the boss (played or summoned), 2 damage to a random enemy "
                     "character (the run player's hero or a minion an effect can pick), by the game RNG.")]))
B.append(boss(12, "pyramad", "Pyramad", "Pyramad", 4, "2", "midrange", [
    ("Earth Shock", 2), ("Crushing Hand", 2), ("Elementary Reaction", 2), ("Fire Plume Harbinger", 2), ("Kobold Hermit", 2),
    ("Rockbiter Weapon", 2), ("Sandstorm Elemental", 2), ("Rumbling Elemental", 2), ("Earthquake", 1), ("Fire Fly", 2),
    ("Quicksand Elemental", 2), ("Candletaker", 2), ("Sandbinder", 2)],
    heroic=[("Grandmummy", 2), ("Lightning Bolt", 1), ("Earthen Might", 2), ("Fire Plume Harbinger", 2), ("Sandstorm Elemental", 2),
            ("Hot Spring Guardian", 2), ("Hex", 2), ("Fire Elemental", 2), ("Fire Fly", 2), ("Tar Creeper", 2), ("Fire Plume Phoenix", 2),
            ("Tol'vir Stoneshaper", 2), ("Blazecaller", 2)],
    cards_=["ULDA_BOSS_12e"],
    replacements={"Quicksand Elemental": "Tar Creeper"},
    why="Quicksand Elemental by Tar Creeper (a 3-Cost Elemental of the boss's Heroic list).",
    notes=[("power", "Brick by Brick (3): the boss Discovers an Elemental among three distinct scripted collectible Elementals of its class "
                     "(Shaman) and neutral that the run's generation filter allows for it; the boss's AI picks, and the picked card costs "
                     "(2) less (enchantment ULDA_BOSS_12e). Offered while its hand has room.")]))
B.append(boss(13, "scalesnout", "Snakeflinger Scalesnout", "Snakeflinger_Scalesnout", 3, "1", "aggro", [
    "Arcane Shot", "Hunter's Mark", "Hunting Mastiff", "Snake Trap", "Venomstrike Trap", "Hyena Alpha", "Marked Shot", "Piranha Launcher",
    "Jar Dealer", "Hench-Clan Hogsteed", "Tauren Warrior"],
    heroic=["Alleycat", "Snake Trap", "Venomstrike Trap", "Animal Companion", "Houndmaster", "Hyena Alpha", "Cobra Shot", "Piranha Launcher",
            "Savannah Highmane", "Waterboy", "Emperor Cobra"],
    cards_=["EX1_554t", "ULDA_BOSS_13e"],
    notes=[("power", "Snakeshot (2) summons a 1/1 Snake (Snake Trap's EX1_554t) with Poisonous (enchantment ULDA_BOSS_13e); it needs a "
                     "free slot.")]))
B.append(boss(14, "sirinell", "Sword Dancer Sirinell", "Sword_Dancer_Sirinell", 4, "3", "tempo", [
    ("Shadowstep", 2), ("Deadly Poison", 2), ("Doomerang", 2), ("Cavern Shinyfinder", 2), ("Goblin Auto-Barber", 2), ("Stolen Steel", 1),
    ("Toxicologist", 2), ("Fan of Knives", 2), ("Hooked Scimitar", 2), ("Perdition's Blade", 1), ("Blade Flurry", 2), ("Spectral Cutlass", 1),
    ("Tinker's Sharpsword Oil", 2), ("Luckydo Buccaneer", 2), ("Hench-Clan Thug", 2), ("Dread Corsair", 2), ("Captain Greenskin", 1)],
    notes=[("power", "Sword Dance (passive): when a weapon of the boss is destroyed (out of Durability, by an effect, or replaced), a "
                     "new copy of it with its Attack and its enchantments is shuffled into the boss's deck as a public shuffle "
                     "(Kingsbane's rule; Durability is the card's again). The boss knows what it shuffled into its own deck.")]))
B.append(boss(15, "herring", "Lt. Herring", "Lt._Herring", 1, "1", "aggro", [
    "Beakered Lightning", "Zap!", "Primalfin Totem", "Siltfin Spiritwalker", "Murloc Tinyfin", "Deadscale Knight", "Murloc Raider", "Murmy",
    "Puddlestomper", "Murloc Tastyfin"],
    heroic=["Sludge Slurper", "Underbelly Angler", "Grimscale Oracle", "Murloc Tidecaller", "Rockpool Hunter", "Coldlight Seer",
            "Primalfin Lookout", "Gentle Megasaur"],
    cards_=["ULDA_BOSS_15e"],
    replacements={"Murmy": "Bluegill Warrior"},
    why="Murmy by Bluegill Warrior (a Murloc that Puffertrooper's Rush does not need).",
    notes=[("power", "Puffertrooper (2) gives each Murloc on the boss's board that has no Rush yet Rush (enchantment ULDA_BOSS_15e; the "
                     "client's enchantment text reads \"is granting\", but the power is a one-time gift: Murlocs played later do not "
                     "get it). Offered while such a Murloc is on its board.")]))
B.append(boss(16, "battrund", "Battrund", "Battrund", 4, "2", "midrange", [
    ("Pharaoh Cat", 2), ("Improve Morale", 2), ("Whirlwind", 2), ("Battle Rage", 2), ("Cleave", 2), ("Slam", 2), ("Ogre Warmaul", 2),
    ("Restless Mummy", 2), ("Crush", 1), ("Temple Berserker", 2), ("Ogre Brute", 2), ("Bone Wraith", 1), ("Body Wrapper", 1),
    ("Wasteland Scorpid", 1), ("Living Monument", 1)],
    heroic=[("Warbot", 2), ("Battle Rage", 2), ("Bloodsworn Mercenary", 2), ("Frothing Berserker", 2), ("Ravaging Ghoul", 1),
            ("Restless Mummy", 2), ("Grommash Hellscream", 1), ("Mecharoo", 2), ("Mistress of Mixtures", 2), ("Amani Berserker", 2),
            ("Haunted Creeper", 2), ("Abomination", 2), ("Anubisath Sentinel", 1), ("Cairne Bloodhoof", 1), ("Wasteland Scorpid", 1)],
    cards_=["ULDA_BOSS_16e"],
    replacements={"Ogre Brute": "Ironfur Grizzly", "Living Monument": "Anubisath Sentinel"},
    why="Ogre Brute by Ironfur Grizzly and Living Monument by Anubisath Sentinel (of the boss's Heroic list).",
    notes=[("power", "Under Wraps (passive): after the boss plays a minion from its hand, the minion gets Reborn (enchantment "
                     "ULDA_BOSS_16e; Silence removes it).")]))
B.append(boss(17, "ichabod", "Ichabod the Cursed", "Ichabod_the_Cursed", 3, "2", "aggro", [
    ("Journey Below", 2), ("Cheat Death", 2), ("Shiv", 2), ("Sudden Betrayal", 1), ("Violet Haze", 2), ("Necrium Blade", 1),
    ("Plague Scientist", 2), ("Tomb Pillager", 1), ("Belligerent Gnome", 2), ("Spitting Camel", 2), ("Gnomeregan Infantry", 2),
    ("Backstreet Leper", 2), ("Infested Goblin", 2), ("Infested Tauren", 2)],
    heroic=[("Backstab", 2), ("Counterfeit Coin", 2), ("Fan of Knives", 2), ("Necrium Blade", 2), ("Necrium Vial", 2), ("Leper Gnome", 2),
            ("Undertaker", 2), ("Kobold Sandtrooper", 2), ("Lost Spirit", 2), ("Backstreet Leper", 2), ("Baron Rivendare", 1),
            ("Spiritsinger Umbra", 1), ("Abomination", 2), ("Mekgineer Thermaplugg", 1)],
    cards_=["EX1_029", "ULDA_BOSS_17e"],
    replacements={"Spitting Camel": "Loot Hoarder"},
    why="Spitting Camel by Loot Hoarder (a 2-Cost minion).",
    notes=[("power", "Spreading the Curse (2) summons a Leper Gnome (EX1_029) with Rush (enchantment ULDA_BOSS_17e); it needs a free slot.")]))
B.append(boss(18, "twizzleflux", "Twizzleflux", "Twizzleflux", 1, "2", "aggro", [
    ("Possessed Villager", 2), ("Sinister Deal", 2), ("Corrupting Mist", 1), ("Curse of Rafaam", 1), ("Curse of Weakness", 2), ("Defile", 1),
    ("EVIL Genius", 2), ("Expired Merchant", 1), ("Darkshire Councilman", 2), ("Doubling Imp", 2), ("EVIL Recruiter", 1),
    ("Rafaam's Scheme", 2), ("Leper Gnome", 2), ("Kobold Sandtrooper", 2), ("Infested Goblin", 2)],
    heroic=[("Flame Imp", 2), ("Possessed Villager", 2), ("Sinister Deal", 2), ("Voidwalker", 2), ("EVIL Genius", 2), ("Vulgar Homunculus", 2),
            ("Darkshire Councilman", 2), ("Doubling Imp", 2), ("Imp Gang Boss", 2), ("Leper Gnome", 2), ("Kobold Sandtrooper", 2),
            ("Blackwald Pixie", 2), ("Baron Rivendare", 1)],
    cards_=["ULDA_BOSS_18e"],
    replacements={"Sinister Deal": "Kobold Lackey", "Expired Merchant": "Voidwalker", "EVIL Recruiter": "Imp Gang Boss",
                  "Kobold Sandtrooper": "Flame Imp"},
    why="Sinister Deal by Kobold Lackey (the Lackey it would Discover), Expired Merchant by Voidwalker, EVIL Recruiter by Imp Gang Boss "
        "and Kobold Sandtrooper by Flame Imp (of the boss's Heroic list).",
    notes=[("power", "Plagued Horde (2) gives any minion \"Deathrattle: Deal 2 damage to the enemy hero\" (enchantment ULDA_BOSS_18e): "
                     "the enemy of that minion's controller when it dies. The page's note that the original never used the power while "
                     "the run player's hero was Immune is not copied: the boss's AI decides.")]))

doc = {
    "schemaVersion": 1,
    "family": "19",
    "description": "Tombs of Terror bosses A (phase 8 family 19): eighteen encounters of Saviors of Uldum's Tombs of Terror (ULDA_BOSS_01 to "
                   "ULDA_BOSS_18: Waste Wanderer Cardish, Dark Ritualist Zafarr, Vera Ridley, Nash the Greatworm, Glack the Scorpid, Mister "
                   "Chu, Squeamlish, Sinkhole, Demolisher 3V-11, Terraviss, R.O.B.O.L.D., Pyramad, Snakeflinger Scalesnout, Sword Dancer "
                   "Sirinell, Lt. Herring, Battrund, Ichabod the Cursed, Twizzleflux) and Sinkhole's Sand Trap (ULDA_030). Boss deck lists "
                   "are transcribed from hearthstone.wiki.gg boss pages (read " + READ + "), by card name; tools/dungeon-run-catalog (step "
                   "'encounters') resolves the names against facts/violet-hold-2026-09.json, checks every card and writes "
                   "data/dungeon-run/encounters-v1.json. Every Tombs boss hero prints 10 Health: Health is the act curve's, and the tier is "
                   "the boss's level band read from its page's deck table (levels 1-2: tier 1; 2-3 and 3-4: tier 2; 5-6: tier 3; 7: tier 4; "
                   "verify/tools/gen/p8f19/tombs_tiers.py).",
    "method": "The wiki's MediaWiki API was read for each page twice: the wikitext (action=parse, prop=wikitext) and the rendered deck "
              "table (prop=text, rowspans and colspans expanded); the two agree. Each Normal table is transcribed in table order with its "
              "own counts (25 or 30 cards); a table of possible cards without counts (\"10; 15\", \"15; 20\") is transcribed with count "
              "null. Where the Normal list is shorter than 30 the Heroic table is transcribed too and is the row's extend list. A page's "
              "level note (\"These decks correspond only to level N\") and its chapter line are recorded in the row's schedule "
              "adaptation. Mister Chu's Mirage Blade is the Bazaar Burglary token ULD_326t (the page's special card); Sinkhole's special "
              "card is Sand Trap (ULDA_030). The file is written by verify/tools/gen/p8f19/make_f19.py.",
    "bosses": B,
    "rejected": [],
}

text = json.dumps(doc, indent=1, ensure_ascii=False) + "\n"
with open(OUT, "w", encoding="utf-8", newline="\n") as f:
    f.write(text)
print("wrote " + os.path.relpath(OUT, REPO) + ": " + str(len(B)) + " bosses")
