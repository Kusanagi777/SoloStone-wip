#!/usr/bin/env python3
# Phase 8 family 20 (Tombs of Terror bosses B, ULDA_BOSS_19-36): writes data/dungeon-run/reference/encounters/F20.json, the
# hand transcription of the eighteen boss pages of hearthstone.wiki.gg (read 2026-11-14). Run from anywhere:
#   python3 -I verify/tools/gen/p8f20/make_f20.py
# The deck tables were read from each page's wikitext with every rowspan and colspan expanded; each Normal table with counts
# sums to the total its level note names (25 or 30), except Ol' Toomba's (32: see its row). The "10; 15", "15; 20" and
# Skarik's tables list possible cards without counts. The tier rule is family 19's tombs_tiers.py (shared by the Tombs
# families 19-22), imported from verify/tools/gen/p8f19. Output: LF, no BOM, one-space indent, keys in the order written here.
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "p8f19"))
import tombs_tiers  # noqa: E402

REPO = os.path.abspath(os.path.join(HERE, "..", "..", "..", ".."))
OUT = os.path.join(REPO, "data", "dungeon-run", "reference", "encounters", "F20.json")
READ = "2026-11-14"
WIKI = "https://hearthstone.wiki.gg/wiki/"

# Replacements: a listed card the engine does not script (or admit for a boss) is replaced by a scripted card of the same role,
# named per row (the encounters step records each one). The unscripted ones are Saviors of Uldum collectibles no phase-8 family
# lists, and a few older cards; a rerun of the encounters step picks the listed card up once it is scripted.
UNSCRIPTED_NOTE = ("Listed cards the engine does not script yet are replaced by a scripted card of the same role, named in the row's "
                   "replacements (the encounters step records each one): ")


def cards(names):
    out = []
    for item in names:
        name, count = item if isinstance(item, tuple) else (item, None)
        out.append({"name": name, "count": count})
    return out


def boss(n, slug, name, page, chapter, shape, archetype, normal, heroic=None, cards_=None, flags=None, replacements=None, why=None,
         fillers=None, notes=()):
    level = tombs_tiers.level(shape)
    label = "Normal (level %d)" % level
    lists = [{"label": label, "cards": cards(normal)}]
    if heroic is not None:
        lists.append({"label": "Heroic", "cards": cards(heroic)})
    row = {
        "id": "ulda_%02d_%s" % (n, slug),
        "name": name,
        "provenance": "ULDA",
        "family": "20",
        "hero": "ULDA_BOSS_%02dh" % n,
        "power": "ULDA_BOSS_%02dp" % n,
        "tier": tombs_tiers.tier(shape),
        "archetype": archetype,
        "level": {"source": "wiki-level-table", "original": level},
    }
    if flags:
        row["flags"] = flags
    if cards_:
        row["cards"] = cards_
    row["source"] = {"url": WIKI + page, "read": READ}
    row["lists"] = lists
    row["deck"] = {"list": label}
    if heroic is not None:
        row["extend"] = ["Heroic"]
    if replacements:
        row["replacements"] = replacements
    if fillers:
        row["fillers"] = fillers
    adaptations = [tombs_tiers.schedule_note(chapter, shape)]
    deck = tombs_tiers.deck_note(shape)
    if deck:
        adaptations.append(deck)
    if replacements:
        adaptations.append({"kind": "deck", "note": UNSCRIPTED_NOTE + why})
    if fillers:
        adaptations.append({"kind": "deck", "note": "The Normal and Heroic tables share cards, so with two copies of each the deck stays short "
                            "of 30; the last cards are curated fillers of the boss's class and theme: " + ", ".join(fillers) + "."})
    for kind, note in notes:
        adaptations.append({"kind": kind, "note": note})
    row["adaptations"] = adaptations
    return row


B = []
B.append(boss(19, "sothis", "Sothis", "Sothis", 4, "3", "midrange", [
    ("Call to Adventure", 2), ("Hammer of Twilight", 2), ("Mogu Fleshshaper", 2), ("Into the Fray", 1), ("Dead Man's Hand", 1), ("Execute", 2),
    ("Forge of Souls", 1), ("Heroic Strike", 2), ("Ogre Warmaul", 1), ("Restless Mummy", 2), ("Sul'thraze", 1), ("Mogu Cultist", 3),
    ("Temple Berserker", 2), ("Conjured Mirage", 1), ("Cult Master", 2), ("Mogu'shan Warden", 2), ("Khartut Defender", 2), ("Wrapped Golem", 1)],
    cards_=["ULD_705", "ULD_705t"],
    replacements={"Conjured Mirage": "Sen'jin Shieldmasta"},
    why="Conjured Mirage (a 4-Cost Taunt) by Sen'jin Shieldmasta (a 4-Cost Taunt).",
    notes=[("power", "Titan Ritual (2) summons two 1/1 Mogu Cultists (ULD_705) for the boss, as many as the board has room for (summoned, "
                     "so their Battlecry does not run). Mogu Cultist is scripted by this family: \"Battlecry: If your board is full of "
                     "Mogu Cultists, sacrifice them all and summon Highkeeper Ra\" (seven Mogu Cultists, the played one included, are "
                     "destroyed, then Highkeeper Ra ULD_705t, a 20/20 that deals 20 damage to all enemies at the end of its owner's turn, "
                     "is summoned). The power needs a free board slot.")]))
B.append(boss(20, "kham", "Kham", "Kham", 3, "3", "tempo", [
    ("Ancestral Guardian", 2), ("Ghostly Charger", 2), ("Beneath the Grounds", 2), ("Hooked Scimitar", 2), ("Blade Flurry", 2), ("Shadow of Death", 1),
    ("Spectral Cutlass", 2), ("Sprint", 1), ("Cursed Blade", 1), ("Forge of Souls", 2), ("Jar Dealer", 1), ("Murmy", 2), ("Lost Spirit", 2),
    ("Phantom Militia", 2), ("Ancient Shade", 2), ("Phantom Freebooter", 2), ("Wrapped Golem", 2)],
    flags=["passive"],
    replacements={"Cursed Blade": "Light's Justice", "Murmy": "Micro Mummy"},
    why="Cursed Blade by Light's Justice (a 1-Cost weapon, so Spectral Swordsman keeps as many weapons) and Murmy by Micro Mummy (a "
        "Reborn minion).",
    notes=[("power", "Spectral Swordsman (passive): while the boss has a weapon equipped its hero has Stealth (enemy attacks, spells, hero "
                     "powers and Battlecries cannot aim at it; area and random effects still reach it). The Stealth is set when a "
                     "weapon is equipped and cleared when the boss's last weapon is destroyed; the hero's own attacks do not end it "
                     "(the text ties it to the weapon only).")]))
B.append(boss(21, "illidara", "Illidara Sunsdawn", "Illidara_Sunsdawn", 3, "4", "midrange", [
    ("Brazen Zealot", 2), ("Meanstreet Marshal", 2), ("Never Surrender!", 1), ("Noble Sacrifice", 1), ("Redemption", 1), ("A Light in the Darkness", 2),
    ("Hydrologist", 2), ("Subdue", 2), ("Call to Adventure", 2), ("Muster for Battle", 1), ("Consecration", 1), ("Ivory Knight", 1),
    ("A New Challenger...", 2), ("Guardian of Kings", 2), ("Secretkeeper", 1), ("Dwarven Archaeologist", 2), ("Golden Scarab", 2),
    ("Vulpera Scoundrel", 1), ("Body Wrapper", 2)],
    cards_=["ULDA_BOSS_21e"],
    flags=["passive", "bothPlayers"],
    replacements={"Hydrologist": "Jeweled Scarab"},
    why="Hydrologist (its Discover offers Secrets, and this row generates no boss Secret) by Jeweled Scarab (a 2-Cost Discover minion, "
        "so Staking A Claim keeps as many Discover cards).",
    notes=[("power", "Staking A Claim (passive): whenever either player plays a card with the Discover keyword (the card's DISCOVER tag), "
                     "every minion on both boards gains +1/+1 (enchantment ULDA_BOSS_21e; a dormant run shrine is left out). The trigger "
                     "is the public play, never a hidden card.")]))
B.append(boss(22, "colossus", "Colossus of the Sun", "Colossus_of_the_Sun", 2, "4", "midrange", [
    ("Pharaoh's Blessing", 2), ("Ragnaros, Lightlord", 1), ("Whirlwind", 2), ("Battle Rage", 2), ("Frightened Flunky", 2), ("Rampage", 2),
    ("Blood Warriors", 2), ("Bloodsworn Mercenary", 2), ("Frothing Berserker", 2), ("Restless Mummy", 2), ("Plague of Wrath", 2), ("Tomb Warden", 2),
    ("Neferset Ritualist", 2), ("History Buff", 2), ("Phantom Militia", 2), ("Colossus of the Moon", 1)],
    flags=["bothPlayers"],
    notes=[("power", "Power of Wrath (0) deals 1 damage to every damaged minion on either board (the damaged minions are taken before any "
                     "damage is dealt, so a minion it damages is not hit twice). Offered while a damaged minion is on a board.")]))
B.append(boss(23, "skarik", "Skarik", "Skarik", 2, "1", "aggro", [
    "Claw", "Spirit of the Raptor", "Bite", "Stampede", "Snake Trap", "Pit Snake", "Serpent Egg", "Emperor Cobra", "Golden Scarab"],
    heroic=["Acornbearer", "Dire Mole", "Enchanted Raven", "Mark of the Lotus", "BEEEES!!!", "Savage Roar", "Dire Wolf Alpha", "Haunted Creeper"],
    cards_=["ULDA_BOSS_23e"],
    notes=[("power", "Hatching Time (2) reduces the Cost of each Beast in the boss's hand by (1) (enchantment ULDA_BOSS_23e). Offered "
                     "while the boss holds a Beast."),
           ("deck", "Skarik's page lists the possible cards of each table without the \"10; 15\" total line the other low-level pages "
                    "carry; the table has the same shape (nine possible cards, no counts), so the row takes the levels 1-2 band.")]))
B.append(boss(24, "palm_tree", "Suspicious Palm Tree", "Suspicious_Palm_Tree", 2, "2", "midrange", [
    ("Acornbearer", 1), ("Living Roots", 2), ("Dendrologist", 2), ("Addled Grizzly", 2), ("BEEEES!!!", 2), ("Landscaping", 2), ("Garden Gnome", 2),
    ("Poison Seeds", 1), ("Soul of the Forest", 2), ("Force of Nature", 2), ("Oasis Surger", 2), ("Hidden Oasis", 1), ("Tending Tauren", 1),
    ("Overflow", 1), ("Ironbark Protector", 1), ("The Forest's Aid", 1)],
    heroic=[("Acornbearer", 2), ("Mark of the Lotus", 2), ("Dendrologist", 2), ("BEEEES!!!", 2), ("Ferocious Howl", 2), ("Landscaping", 2),
            ("Soul of the Forest", 1), ("Wispering Woods", 2), ("Bewitched Guardian", 2), ("Mulchmuncher", 2), ("Arena Fanatic", 2),
            ("Twilight Drake", 2), ("Mountain Giant", 2)],
    cards_=["GIL_663t"],
    replacements={"Garden Gnome": "Keeper of the Grove"},
    why="Garden Gnome by Keeper of the Grove (a 4-Cost Druid minion).",
    notes=[("power", "Corrupted Oasis (2) adds two 2/2 Treants (Witchwood Apple's GIL_663t, the power's template) to the boss's hand, "
                     "as many as it has room for. Offered while its hand has room.")]))
B.append(boss(25, "zenda", "Beetle Herder Zenda", "Beetle_Herder_Zenda", 2, "1", "midrange", [
    "Biology Project", "Lesser Jasper Spellstone", "Crystal Merchant", "Bug Collector", "Jeweled Scarab", "Plated Beetle", "Golden Scarab",
    "Infested Goblin", "Snapjaw Shellfighter"],
    heroic=["Enchanted Raven", "Lesser Jasper Spellstone", "Mark of the Lotus", "Mark of Y'Shaarj", "Astral Tiger", "Ironwood Golem",
            "Oaken Summons", "Plated Beetle", "Infested Goblin"],
    cards_=["LOOT_413"],
    fillers=["Ironfur Grizzly"],
    replacements={"Bug Collector": "Murloc Tidehunter", "Crystal Merchant": "Enchanted Raven"},
    why="Bug Collector by Murloc Tidehunter (a 2-Cost minion that summons a 1/1) and Crystal Merchant by Enchanted Raven (a Druid minion of "
        "the boss's Heroic list).",
    notes=[("power", "Beetle Stampede (2) summons a Plated Beetle (LOOT_413, the 2/3 \"Deathrattle: Gain 3 Armor\") for the boss; it needs "
                     "a free slot.")]))
B.append(boss(26, "thudd", "Thudd Lockspring", "Thudd_Lockspring", 4, "1", "tempo", [
    "Forbidden Flame", "Mirror Image", "Primordial Glyph", "Micro Mummy", "Clockwork Goblin", "Clockwork Gnome", "Cogmaster", "Skaterbot",
    "Clockwork Automaton"],
    heroic=["Goblin Blastmage", "Glow-Tron", "Micro Mummy", "Clockwork Goblin", "Mecharoo", "Annoy-o-Tron", "Galvanizer", "Mechwarper",
            "Tinkertown Technician"],
    notes=[("power", "Gatling Wand (2) deals 2 damage split among the run player's minions: two 1-damage hits, each on a random enemy "
                     "minion still alive (Arcane Missiles' rule, minions only; a dormant run shrine is left out). Offered while the run "
                     "player has a minion.")]))
B.append(boss(27, "toomba", "Ol' Toomba", "Ol'_Toomba_(Tombs_of_Terror)", 1, "4", "tempo", [
    ("Swashburglar", 2), ("Bloodsail Howler", 2), ("Clever Disguise", 2), ("Stolen Steel", 2), ("Edwin VanCleef", 1), ("Face Collector", 1),
    ("Fan of Knives", 2), ("Shady Dealer", 2), ("Hench-Clan Burglar", 2), ("Sahket Sapper", 2), ("Cannon Barrage", 2), ("Sprint", 1),
    ("Captain Hooktusk", 1), ("Patches the Pirate", 1), ("Captain's Parrot", 2), ("Ticket Scalper", 2), ("Salty Dog", 2), ("Skycap'n Kragg", 1)],
    flags=["hiddenZone"],
    replacements={"Sahket Sapper": "Vilespine Slayer"},
    why="Sahket Sapper by Vilespine Slayer (a Rogue minion that removes an enemy minion, of the boss's Heroic list).",
    notes=[("deck", "The wiki's Normal table sums to 32: it lists two copies each of the Legendaries Edwin VanCleef and Skycap'n Kragg. "
                    "The row keeps one of each (one copy of a Legendary per deck), which gives the 30 cards of a full list."),
           ("power", "\"Retired\" Treasure Hunter (2) puts a copy of a random card of the run player's hand into the boss's hand (Mind "
                     "Vision's rule). The boss learns that one card: the copy is its own; the run player sees which card was copied. In the "
                     "boss's fair-view search the run player's hand holds only masked cards, so there the copy is made only from a card "
                     "the boss already saw go into that hand (else nothing): the search never reads the hidden hand. Offered while the run "
                     "player's hand (a public count) holds a card and the boss's hand has room.")]))
B.append(boss(28, "sazin", "Sazin", "Sazin", 1, "4", "control", [
    ("Blazing Invocation", 2), ("Crackle", 2), ("Elementary Reaction", 2), ("Eternal Sentinel", 1), ("Murmuring Elemental", 2), ("Lightning Storm", 2),
    ("Unbound Elemental", 2), ("Hex", 1), ("Rumbling Elemental", 2), ("Thunderhead", 2), ("Earth Elemental", 2), ("Hallazeal the Ascended", 1),
    ("Grumble, Worldshaker", 1), ("Neptulon", 1), ("Stone Sentinel", 2), ("Al'Akir the Windlord", 1), ("Quicksand Elemental", 2), ("Tar Creeper", 2)],
    flags=["bothPlayers", "hiddenZone"],
    replacements={"Quicksand Elemental": "Tar Creeper"},
    why="Quicksand Elemental by Tar Creeper (an Elemental of the boss's list).",
    notes=[("power", "Mighty Windstorm (0): each player, the boss first, swaps a random minion of their hand with a random minion of their "
                     "board: the board minion returns to its owner's hand (as a fresh card) and the hand minion is put onto the board in "
                     "its place (summoned, so no Battlecry). A player with no minion in hand or none on board swaps nothing. A run shrine "
                     "is never chosen (a run shrine never leaves the board). The run player's hand minion becomes public as it is "
                     "summoned; the hidden hand is not read for anything else, and in the boss's fair-view search that hand holds only "
                     "masked cards, so there the run player swaps only a minion the boss already saw go into that hand. Offered while the "
                     "boss can swap (its own hand and board) or the run player has a minion on board and a card in hand (public facts).")]))
B.append(boss(29, "ozara", "Ozara", "Ozara", 2, "4", "midrange", [
    ("Tunnel Trogg", 2), ("Ancestral Knowledge", 2), ("Crushing Hand", 2), ("Earthen Might", 2), ("Menacing Nimbus", 2), ("Sandstorm Elemental", 2),
    ("Far Sight", 2), ("Unbound Elemental", 2), ("Thunderhead", 2), ("Earth Elemental", 2), ("Earthquake", 2), ("Lesser Sapphire Spellstone", 2),
    ("Mogu Fleshshaper", 2), ("Quicksand Elemental", 2), ("Cauldron Elemental", 2)],
    cards_=["EX1_243"],
    flags=["passive"],
    replacements={"Quicksand Elemental": "Tar Creeper"},
    why="Quicksand Elemental by Tar Creeper (a cheap Elemental).",
    notes=[("power", "Mother of Sand (passive): after the boss plays a card with Overload, it summons a Dust Devil (EX1_243; summoned, so "
                     "its own Overload does not apply); it needs a free slot.")]))
B.append(boss(30, "ermavar", "Ermavar", "Ermavar", 1, "2a", "control", [
    "Forbidden Flame", "Flame Geyser", "Lesser Ruby Spellstone", "Pyros", "Dune Sculptor", "Flame Ward", "Flamewaker", "Dragon's Fury", "Flamestrike",
    "Fire Fly", "Flame Juggler", "Injured Tol'vir", "Neferset Ritualist", "Fire Plume Phoenix"],
    heroic=["Mirror Image", "Flame Geyser", "Flamecannon", "Sorcerer's Apprentice", "Cinderstorm", "Flame Ward", "Flamewaker", "Fireball",
            "Fire Fly", "Flame Juggler"],
    flags=["passive"],
    replacements={"Injured Tol'vir": "Frostwolf Grunt"},
    why="Injured Tol'vir by Frostwolf Grunt (a 2-Cost Taunt).",
    notes=[("power", "Fireshaper (passive): after the boss casts a spell, 2 damage randomly split among all enemies (two 1-damage hits on "
                     "the run player's hero or minions; Flamewaker's rule, the power's template). A hero power deals no Spell Damage.")]))
B.append(boss(31, "drasar", "Pillager Drasar", "Pillager_Drasar", 4, "1", "midrange", [
    "Beakered Lightning", "Tunnel Trogg", "Ancestral Knowledge", "Likkim", "Sandstorm Elemental", "Murmy", "Injured Tol'vir", "Stonesplinter Trogg",
    "Candletaker", "Burly Rockjaw Trogg", "Trogg Gloomeater", "Troggzor the Earthinator"],
    heroic=["Sludge Slurper", "Tunnel Trogg", "Eternal Sentinel", "Likkim", "Primalfin Totem", "Totem Golem", "Unbound Elemental", "Doomhammer",
            "Nerubian Egg"],
    cards_=["ULDA_BOSS_31e1"],
    replacements={"Injured Tol'vir": "Frostwolf Grunt", "Murmy": "Micro Mummy"},
    why="Injured Tol'vir by Frostwolf Grunt (a 2-Cost Taunt) and Murmy by Micro Mummy (a Reborn minion).",
    notes=[("power", "Pillaged Relics (2): the boss chooses any minion; it gets, at random, +1/+1 (enchantment ULDA_BOSS_31e1), Divine Shield "
                     "or Taunt.")]))
B.append(boss(32, "edra", "Edra", "Edra", 4, "1", "midrange", [
    "Moonfire", "Acornbearer", "Forbidden Ancient", "Worthy Expedition", "Crystal Merchant", "Dreamway Guardians", "Druid of the Saber",
    "Grove Tender", "Keeper of the Grove", "Wispering Woods"],
    heroic=["Acornbearer", "Living Roots", "Dreamway Guardians", "Druid of the Swarm", "Power of the Wild", "BEEEES!!!", "Crypt Lord",
            "Wardruid Loti"],
    cards_=["CS2_231", "DAL_568e"],
    fillers=["Ironbark Protector"],
    replacements={"Crystal Merchant": "Enchanted Raven"},
    why="Crystal Merchant by Enchanted Raven (a 1-Cost Druid minion).",
    notes=[("power", "Whispering Sentinel (1) summons a 1/1 Wisp (CS2_231) for the boss and gives it Lifesteal (Lightforged Blessing's "
                     "enchantment DAL_568e: the client has no enchantment of the power's own); it needs a free slot.")]))
B.append(boss(33, "ruby", "Runaway Ruby", "Runaway_Ruby", 2, "3", "midrange", [
    ("Boommaster Flark", 1), ("Eternium Rover", 2), ("Omega Assembly", 2), ("Warbot", 2), ("Battle Rage", 2), ("Vicious Scraphound", 2), ("Brawl", 2),
    ("Security Rover", 2), ("Cogmaster", 2), ("Mechwarper", 2), ("Galvanizer", 2), ("Flying Machine", 2), ("SN1P-SN4P", 1), ("Spider Tank", 2),
    ("Tinkertown Technician", 2), ("Clockwork Automaton", 2)],
    flags=["bothPlayers", "position"],
    notes=[("power", "Out of Control (1) deals 1 damage to the center-most minions of each board: on a board of an odd count the middle "
                     "minion, of an even count the two middle ones (positions are counted over every minion of that board, a dormant run "
                     "shrine included, which takes no damage). Offered while a board holds a minion.")]))
B.append(boss(34, "dovo", "Dovo Fastfuse", "Dovo_Fastfuse", 3, "1", "aggro", [
    "EVIL Miscreant", "Rocket Boots", "Clockwork Gnome", "Goblin Bomb", "Booty Bay Bookie", "EVIL Cable Rat", "Mad Bomber", "Whirliglider",
    "Explodinator"],
    heroic=["Devastate", "Eternium Rover", "Armorsmith", "Clockwork Goblin", "Frothing Berserker", "Axe Flinger", "Bloodhoof Brave", "Bomb Lobber",
            "Grim Patron"],
    flags=["bothPlayers"],
    notes=[("power", "Tomb Boomin' (2) deals 4 damage split randomly between all characters: four 1-damage hits, each on a random "
                     "character of either side still alive (both heroes included; Mad Bomber's rule, the power's template, with the boss's "
                     "own hero in the pool).")]))
B.append(boss(35, "ptah", "Earthrager Ptah", "Earthrager_Ptah", 4, "4", "control", [
    ("Tunnel Trogg", 2), ("Ancestral Knowledge", 2), ("Fire Plume Harbinger", 2), ("Lava Shock", 2), ("Flametongue Totem", 2), ("Magma Rager", 2),
    ("Unbound Elemental", 2), ("Fireguard Destroyer", 2), ("Earth Elemental", 2), ("Fire Elemental", 2), ("Earthquake", 1), ("Kalimos, Primal Lord", 1),
    ("The Runespear", 1), ("Vryghoul", 2), ("Bone Wraith", 2), ("Khartut Defender", 2), ("Troggzor the Earthinator", 1)],
    flags=["position"],
    notes=[("power", "Vengeful Construct (3): the boss chooses a minion of either side with 2 or less Health; it and the minions next to "
                     "it on its board are destroyed (a run shrine goes dormant, as from any destroy effect; a dormant one is left as it "
                     "is).")]))
B.append(boss(36, "kasa", "Kasa Softspeaker", "Kasa_Softspeaker", 2, "2", "aggro", [
    ("Plague of Madness", 2), ("Cursed Blade", 1), ("Whirlwind", 1), ("Bring It On!", 1), ("Cleave", 2), ("Cornered Sentry", 1), ("Heroic Strike", 2),
    ("Weapons Project", 1), ("Fiery War Axe", 2), ("Mountainfire Armor", 2), ("Shield Block", 2), ("Arcanite Reaper", 1), ("Swamp Leech", 2),
    ("Voodoo Doctor", 2), ("Earthen Ring Farseer", 2), ("Hench-Clan Thug", 1)],
    heroic=[("Eternium Rover", 2), ("Whirlwind", 1), ("Armorsmith", 2), ("Vicious Scraphound", 2), ("Frothing Berserker", 2), ("Shield Block", 2),
            ("Restless Mummy", 2), ("Alley Armorsmith", 2), ("Armored Goon", 2), ("Sharkfin Fan", 1), ("Hench-Clan Thug", 2), ("Violet Illusionist", 2),
            ("Zilliax", 1), ("Khartut Defender", 2)],
    cards_=["ULDA_BOSS_36e"],
    flags=["forcedAttack"],
    replacements={"Plague of Madness": "Upgrade!", "Cursed Blade": "Light's Justice"},
    why="Plague of Madness (\"Each player equips a 2/2 Knife with Poisonous\") by Upgrade! (a 1-Cost card that equips or upgrades the boss's "
        "weapon) and Cursed Blade by Light's Justice (a 1-Cost weapon).",
    notes=[("power", "Madness Incarnate (1): the boss's hero gains +1 Attack this turn (enchantment ULDA_BOSS_36e), then attacks up to "
                     "three different random enemy minions, one after another (forced attacks: a frozen or exhausted hero attacks too, a "
                     "Taunt does not redirect, a Stealthed or Immune minion is not picked, a minion that died is not picked; the hero "
                     "takes each minion's damage, an equipped weapon loses Durability, and the attacks stop when either hero dies). "
                     "Offered while the run player has a minion it can attack.")]))

doc = {
    "schemaVersion": 1,
    "family": "20",
    "description": "Tombs of Terror bosses B (phase 8 family 20): eighteen encounters of Saviors of Uldum's Tombs of Terror (ULDA_BOSS_19 to "
                   "ULDA_BOSS_36: Sothis, Kham, Illidara Sunsdawn, Colossus of the Sun, Skarik, Suspicious Palm Tree, Beetle Herder Zenda, "
                   "Thudd Lockspring, Ol' Toomba, Sazin, Ozara, Ermavar, Pillager Drasar, Edra, Runaway Ruby, Dovo Fastfuse, Earthrager "
                   "Ptah, Kasa Softspeaker). Boss deck lists are transcribed from hearthstone.wiki.gg boss pages (read " + READ + "), by "
                   "card name; tools/dungeon-run-catalog (step 'encounters') resolves the names against facts/violet-hold-2026-09.json, "
                   "checks every card and writes data/dungeon-run/encounters-v1.json. Every Tombs boss hero prints 10 Health: Health is the "
                   "act curve's, and the tier is the boss's level band read from its page's deck table (levels 1-2: tier 1; 2-3 and 3-4: "
                   "tier 2; 5-6: tier 3; 7: tier 4; verify/tools/gen/p8f19/tombs_tiers.py, family 19's rule).",
    "method": "Each page's wikitext (action=raw) was read and its deck table expanded (rowspans and colspans) into a grid; the Normal "
              "and Heroic columns are transcribed in table order with their counts (25 or 30 cards); a table of possible cards without "
              "counts (\"10; 15\", \"15; 20\", Skarik's) is transcribed with count null. Where the Normal list is shorter than 30 the "
              "Heroic table is transcribed too and is the row's extend list. A page's level note (\"These decks correspond only to level "
              "N\") and its chapter line are recorded in the row's schedule adaptation. The file is written by "
              "verify/tools/gen/p8f20/make_f20.py.",
    "bosses": B,
    "rejected": [],
}

text = json.dumps(doc, indent=1, ensure_ascii=False) + "\n"
with open(OUT, "w", encoding="utf-8", newline="\n") as f:
    f.write(text)
print("wrote " + os.path.relpath(OUT, REPO) + ": " + str(len(B)) + " bosses")
