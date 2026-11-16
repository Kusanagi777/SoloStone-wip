#!/usr/bin/env python3
# Phase 8 family 22 (Tombs of Terror bosses D, ULDA_BOSS_60-79 without 64 and 67): writes
# data/dungeon-run/reference/encounters/F22.json, the hand transcription of the eighteen boss pages of hearthstone.wiki.gg (read
# 2026-11-14). Run from anywhere:
#   python3 -I verify/tools/gen/p8f22/make_f22.py
# Each deck table was read twice (the raw wikitext, action=raw, and the rendered table of the MediaWiki API, action=parse with
# rowspans and colspans expanded) and the two agree; each Normal table sums to the total its level note names (25 or 30; the
# "10; 15" and "15; 20" tables and Gorebite's table list possible cards without counts). The tier rule is family 19's shared
# verify/tools/gen/p8f19/tombs_tiers.py, imported below. Output: LF, no BOM, one-space indent, keys in the order written here.
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", "..", ".."))
sys.path.insert(0, os.path.join(REPO, "verify", "tools", "gen", "p8f19"))
import tombs_tiers  # noqa: E402

OUT = os.path.join(REPO, "data", "dungeon-run", "reference", "encounters", "F22.json")
READ = "2026-11-14"
WIKI = "https://hearthstone.wiki.gg/wiki/"

# Replacements: a listed card the engine does not script (or admit for a boss) is replaced by a scripted card of the same role,
# named per row (the encounters step records each one). The unscripted ones are Saviors of Uldum collectibles and a few older
# cards no phase-8 family lists; a rerun of the encounters step picks a listed card up once it is scripted.
UNSCRIPTED_NOTE = ("Listed cards the engine does not script yet are replaced by a scripted card of the same role, named in the row's "
                   "replacements (the encounters step records each one): ")


def cards(names):
    out = []
    for item in names:
        name, count = item if isinstance(item, tuple) else (item, None)
        out.append({"name": name, "count": count})
    return out


def boss(n, slug, name, page, chapter, shape, archetype, normal, heroic=None, cards_=None, flags=None, replacements=None, why=None,
         notes=(), level_note=None, fillers=None, fill_note=None):
    level = tombs_tiers.level(shape)
    label = "Normal (level %d)" % level
    lists = [{"label": label, "cards": cards(normal)}]
    if heroic is not None:
        lists.append({"label": "Heroic", "cards": cards(heroic)})
    row = {
        "id": "ulda_%02d_%s" % (n, slug),
        "name": name,
        "provenance": "ULDA",
        "family": "22",
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
    if level_note:
        adaptations.append({"kind": "schedule", "note": level_note})
    deck = tombs_tiers.deck_note(shape)
    if deck:
        adaptations.append(deck)
    if replacements:
        adaptations.append({"kind": "deck", "note": UNSCRIPTED_NOTE + why})
    if fill_note:
        adaptations.append({"kind": "deck", "note": fill_note})
    for kind, note in notes:
        adaptations.append({"kind": kind, "note": note})
    row["adaptations"] = adaptations
    return row


B = []
B.append(boss(60, "zaraam", "Zaraam", "Zaraam", 3, "4", "control", [
    ("Dark Pact", 2), ("Kobold Librarian", 2), ("Sinister Deal", 2), ("EVIL Genius", 2), ("Drain Life", 2), ("EVIL Recruiter", 2),
    ("Void Terror", 2), ("Usher of Souls", 2), ("Aranasi Broodmother", 2), ("Kabal Trafficker", 1), ("Voidlord", 2), ("Beckoner of Evil", 2),
    ("Injured Tol'vir", 2), ("Faceless Lurker", 2), ("Faceless Manipulator", 2), ("C'Thun", 1)],
    replacements={"Sinister Deal": "Kobold Lackey", "EVIL Recruiter": "Imp Gang Boss", "Injured Tol'vir": "Frostwolf Grunt",
                  "Faceless Lurker": "Fen Creeper"},
    why="Sinister Deal by Kobold Lackey (the Lackey it would Discover), EVIL Recruiter by Imp Gang Boss, Injured Tol'vir by Frostwolf "
        "Grunt (family 19's replacements of the same cards), Faceless Lurker by Fen Creeper (a 5-Cost 3/6 Taunt: the Lurker's body "
        "after its Battlecry).",
    notes=[("power", "Forbidden Power (4) deals N damage to a random enemy character (the run player's hero or a minion an effect can "
                     "pick, game RNG); N is the power's own script number (the @ of its text), 1 at the start and +1 whenever a minion "
                     "of the boss's dies (a public death), so the client shows the current value. The hero power damage rules apply "
                     "to the hit."),
           ("schedule", "The page's trivia names Zaraam a seventh-floor encounter, which matches the level-7 shape of its full "
                        "30-card list.")]))
B.append(boss(61, "kyriss", "Kyriss", "Kyriss", 4, "4", "control", [
    ("Sindragosa", 1), ("Crackle", 2), ("Devastate", 2), ("Bring It On!", 2), ("Dragon Roar", 2), ("War Master Voone", 1),
    ("Emberscale Drake", 2), ("Acolyte of Pain", 1), ("Hungry Dragon", 2), ("Twilight Guardian", 2), ("Cobalt Scalebane", 2),
    ("Bone Drake", 2), ("Scaled Nightmare", 2), ("Wrathion", 1), ("Chillmaw", 1), ("Crowd Roaster", 2), ("Primordial Drake", 2),
    ("Onyxia", 1)],
    notes=[("power", "Chain Lightning (4) deals 1 damage to a minion (any minion; the hero power damage rules apply to each hit); while "
                     "the minion it hit last survives, it jumps to a minion adjacent to it on its board (a random one of the two "
                     "neighbours, game RNG; a dormant or permanent minion is no neighbour) and hits again, until one dies or no "
                     "neighbour is left. A hit that does nothing (an Immune minion) ends the chain.")]))
B.append(boss(62, "oasis", "A Beautiful Oasis", "A_Beautiful_Oasis", 2, "3", "midrange", [
    ("BEEEES!!!", 2), ("Landscaping", 2), ("Garden Gnome", 2), ("Oasis Surger", 2), ("Overflow", 2), ("The Forest's Aid", 1), ("Cenarius", 1),
    ("Desert Camel", 2), ("Swarm of Locusts", 2), ("Gahz'rilla", 1), ("Sandwasp Queen", 2), ("Fishflinger", 2), ("Neferset Ritualist", 2),
    ("River Crocolisk", 2), ("Conjured Mirage", 2), ("Pit Crocolisk", 2), ("Colossus of the Moon", 1)],
    flags=["hiddenZone"],
    replacements={"Garden Gnome": "Dragonling Mechanic", "Fishflinger": "Murloc Tidehunter", "Conjured Mirage": "Sen'jin Shieldmasta",
                  "Pit Crocolisk": "Fire Elemental"},
    why="Garden Gnome by Dragonling Mechanic (a 4-Cost minion that summons a second body), Fishflinger by Murloc Tidehunter (a 2-Cost "
        "Murloc that brings another Murloc), Conjured Mirage by Sen'jin Shieldmasta (a 4-Cost Taunt) and Pit Crocolisk by Fire "
        "Elemental (a 6-Cost minion whose Battlecry deals damage).",
    notes=[("power", "Mirage (0), auto-cast as the page states (DungeonRunEncounters.TombsD: at the start of the boss's turn while the "
                     "run player's hand holds a card and has room): it gives the run player an illusion card, a copy of a random card "
                     "of the run player's own hand (game RNG); one of the two (the original or the copy, game RNG) is the illusion. "
                     "The client data has no illusion token (the power's text names none, the page names none), so the illusion is "
                     "that copy, as the page's notes describe: \"Mirage creates a copy of a random card in the player's hand "
                     "(illusion). This effect lasts until the end of the player's turn\"; \"only one random copy of the illusion "
                     "discards a card. The other copy can be played as a normal card\"; \"the illusion can discard its second copy\". "
                     "Played, the illusion resolves as its card and its player discards a random card of the hand (the second copy "
                     "too, if chosen). At the end of the run player's turn the illusion leaves the hand if it is still there; when "
                     "both are still there the copy leaves and the original stays, so the hand is as before (\"if the player does not "
                     "play any of the two copies ... none of the player's cards will be discarded\"). The illusion card is marked by a "
                     "private encounter tag; the boss's power holds only the illusion's public hand position, so the boss knows which "
                     "slot is the illusion and never learns the copied card (a hidden card of the run player's hand).")]))
B.append(boss(63, "vessina", "Vessina", "Vessina_(boss)", 4, "3", "midrange", [
    ("Sludge Slurper", 2), ("Tunnel Trogg", 2), ("Witch's Apprentice", 2), ("Ancestral Knowledge", 2), ("Eternal Sentinel", 2),
    ("EVIL Totem", 2), ("Lava Shock", 2), ("Likkim", 2), ("Haunting Visions", 2), ("Jinyu Waterspeaker", 2), ("Thunderhead", 1),
    ("Rain of Toads", 2), ("Earthquake", 2), ("Lesser Sapphire Spellstone", 2), ("Neptulon", 1), ("Snowfury Giant", 2)],
    flags=["passive"],
    replacements={"EVIL Totem": "Flametongue Totem"},
    why="EVIL Totem by Flametongue Totem (a 2-Cost Totem).",
    notes=[("power", "Plaguebringer (passive): the spells in the boss's hand cost (2) less (a hand aura), and each spell the boss plays "
                     "(not countered) Overloads it by (1) more, on top of its own Overload, so Tunnel Trogg, Thunderhead's and "
                     "Eternal Sentinel's Overload rules see it. Spells cast by an effect are not played and do not Overload.")]))
B.append(boss(65, "thrud", "Thrud", "Thrud", 1, "2", "tempo", [
    ("Pilfer", 2), ("Swashburglar", 2), ("Clever Disguise", 2), ("Cutpurse", 2), ("Pick Pocket", 1), ("Shiv", 2), ("Underbelly Fence", 2),
    ("Burgle", 2), ("EVIL Miscreant", 2), ("Obsidian Shard", 2), ("Ethereal Peddler", 2), ("Mischief Maker", 2), ("Ogre Brute", 2)],
    heroic=[("Spore Hallucination", 2), ("Swashburglar", 2), ("Clever Disguise", 2), ("Eviscerate", 2), ("Underbelly Fence", 2),
            ("Undercity Huckster", 2), ("EVIL Miscreant", 2), ("Hench-Clan Burglar", 2), ("Obsidian Shard", 2), ("Vendetta", 2),
            ("Ethereal Peddler", 2), ("Tess Greymane", 1), ("Mischief Maker", 2)],
    flags=["hiddenZone"],
    replacements={"Mischief Maker": "SI:7 Agent", "Ogre Brute": "Ironfur Grizzly"},
    why="Mischief Maker by SI:7 Agent (a 3-Cost 3/3 Rogue minion) and Ogre Brute by Ironfur Grizzly (family 19's replacement of the "
        "same card).",
    notes=[("power", "Ogre-Sized Thief (3) takes a random card of the run player's deck (game RNG) into the boss's hand: the deck-steal "
                     "rule of the boss shrine TRLA_186 (DungeonRunShrines.TakeFromDeck: a deck exit and a transfer whose definition "
                     "only the new owner sees). The boss learns exactly the card it took and nothing else; the run player sees a card "
                     "leave its deck. A full hand burns it. Offered while the run player's deck, whose count is public, holds a "
                     "card.")]))
B.append(boss(66, "siamat", "Siamat", "Siamat_(boss)", 1, "4", "midrange", [
    ("Polymorph", 2), ("Benevolent Djinn", 2), ("Lightning Bolt", 2), ("Crackle", 2), ("Menacing Nimbus", 2), ("Sandstorm Elemental", 2),
    ("Bog Slosher", 2), ("Electra Stormsurge", 1), ("Far Sight", 1), ("Lightning Storm", 2), ("Unbound Elemental", 2), ("Thunderhead", 2),
    ("Bloodlust", 2), ("Earth Elemental", 2), ("Neptulon", 1), ("Al'Akir the Windlord", 1), ("Walking Fountain", 2)],
    cards_=["ULDA_026"],
    notes=[("power", "Masterful Djinn (2) summons a 1/4 Servant of Siamat (ULDA_026, the client's token: a vanilla 1/4 Elemental) and "
                     "gives it one of Taunt, Rush, Windfury or Divine Shield at random (game RNG; the keyword is the minion's own, "
                     "so Silence removes it). It needs a free board slot.")]))
B.append(boss(68, "wasp", "Weaponized Wasp", "Weaponized_Wasp_(boss)", 1, "1", "aggro", [
    "Enchanted Raven", "Crystalsong Portal", "BEEEES!!!", "Raptor Hatchling", "Sandwasp Queen", "Dire Mole", "Emerald Hive Queen",
    "Emerald Reaver", "Giant Wasp", "Spitting Camel"],
    heroic=["Dire Mole", "Enchanted Raven", "Mark of Y'Shaarj", "BEEEES!!!", "Knight of the Wild", "Snowflipper Penguin", "Knife Juggler",
            "Desert Hare"],
    replacements={"Spitting Camel": "Loot Hoarder"},
    why="Spitting Camel by Loot Hoarder (family 19's replacement of the same card).",
    fillers=["Bloodfen Raptor"],
    fill_note="The Heroic table's Desert Hare is not scripted yet, so the ten possible cards, their second copies and the Heroic "
              "cards reach 28: Bloodfen Raptor (a Beast, which \"Upgraded\" Fauna counts) fills the last two slots.",
    notes=[("power", "\"Upgraded\" Fauna (2) deals N damage to an enemy minion, N the number of Beasts on the boss's board (\"1 damage ... "
                     "for each Beast you control\": one hit, the hero power damage rules applied once). Offered while the boss "
                     "controls a Beast (a dormant or permanent one does not count).")]))
B.append(boss(69, "isiset", "Isiset", "Isiset_(boss)", 4, "4", "control", [
    ("Babbling Book", 2), ("Mirror Image", 1), ("Ray of Frost", 2), ("Arcane Flakmage", 2), ("Astral Rift", 2), ("Frostbolt", 2), ("Icicle", 1),
    ("Primordial Glyph", 2), ("Pyros", 1), ("Unstable Portal", 2), ("Arcane Intellect", 1), ("Counterspell", 1), ("Dune Sculptor", 2),
    ("Duplicate", 1), ("Effigy", 1), ("Frozen Clone", 1), ("Mana Bind", 1), ("Splitting Image", 1), ("Cone of Cold", 2),
    ("Ethereal Conjurer", 2)],
    flags=["passive", "bothPlayers", "recast"],
    notes=[("power", "Mistress of Magic (passive, both players: \"whenever a player casts their first spell\"): when a player plays the "
                     "first spell of their game from hand, a copy of it goes into that player's hand (a full hand takes none). Spells "
                     "cast by an effect are not played and do not count. The copy of the run player's spell is a card of that "
                     "player's hand: the boss's fair view keeps it masked like the rest of that hand.")]))
B.append(boss(70, "rajh", "Rajh", "Rajh_(boss)", 4, "4", "tempo", [
    ("Arcane Blast", 2), ("Arcane Missiles", 2), ("Mana Wyrm", 2), ("Shooting Star", 2), ("Pyros", 1), ("Arcane Intellect", 2),
    ("Cinderstorm", 2), ("Dune Sculptor", 2), ("Flame Ward", 2), ("Flamewaker", 2), ("Twilight Flamecaller", 2), ("Unexpected Results", 2),
    ("Vaporize", 2), ("Scorch", 2), ("Cabalist's Tome", 1), ("Ethereal Conjurer", 2)],
    cards_=["ULDA_BOSS_70e"],
    notes=[("power", "Lord of the Sun (1): the boss has Spell Damage +3 this turn (enchantment ULDA_BOSS_70e on its hero, removed at the "
                     "end of the turn). Offered while the boss's hand holds a spell (its own hand). The Heroic version (ULDA_BOSS_70px, "
                     "auto-cast at 0) is not used.")]))
B.append(boss(71, "ammunae", "Ammunae", "Ammunae_(boss)", 4, "4", "control", [
    ("Biology Project", 1), ("Forbidden Ancient", 2), ("Naturalize", 2), ("Wrath", 2), ("BEEEES!!!", 2), ("Lifeweaver", 2),
    ("Branching Paths", 2), ("Swipe", 2), ("Crystal Stag", 2), ("Ixlid, Fungal Lord", 1), ("Oasis Surger", 2), ("Hidden Oasis", 2),
    ("Ancient of Lore", 1), ("Ancient of War", 2), ("Malorne", 1), ("Ironbark Protector", 2), ("Lucentbark", 1), ("Colossus of the Moon", 1)],
    replacements={"Wrath": "Living Roots"},
    why="Wrath by Living Roots (a cheap Choose One damage spell of the Druid's).",
    notes=[("power", "Lord of Life (2) restores a damaged minion's Health to full (any minion; the healing rules of a hero power "
                     "apply).")]))
B.append(boss(72, "setesh", "Setesh", "Setesh_(boss)", 4, "4", "control", [
    ("Embalming Ritual", 2), ("Grandmummy", 2), ("Shadow Ascendant", 2), ("Shadow Word: Death", 2), ("Spirit Lash", 2), ("Mirage Caller", 2),
    ("Shadow Madness", 2), ("Psychopomp", 2), ("Devour Mind", 2), ("Entomb", 2), ("Plague of Death", 1), ("Acolyte of Pain", 2),
    ("Anubisath Sentinel", 2), ("Khartut Defender", 2), ("Da Undatakah", 1), ("Deathwing", 1), ("Hakkar, the Soulflayer", 1)],
    cards_=["ULDA_BOSS_72e"],
    notes=[("power", "Lord of Destruction (0), auto-cast as the page states (at the start of the boss's turn while the run player "
                     "controls a minion): a random enemy minion (game RNG) gets Cursed! (ULDA_BOSS_72e, \"Deal 2 damage to your hero at "
                     "the end of your turn\"): at the end of each turn of the minion's controller its hero takes 2 damage. Curses "
                     "stack on one minion, as the page notes; Silence removes them.")]))
B.append(boss(73, "husam", "General Husam", "General_Husam", 1, "4", "aggro", [
    ("Explosive Runes", 1), ("Commanding Shout", 2), ("Execute", 2), ("Heroic Strike", 2), ("Vicious Scraphound", 2), ("Bash", 2),
    ("Bloodsworn Mercenary", 2), ("Frothing Berserker", 2), ("Axe Flinger", 2), ("Brawl", 2), ("Supercollider", 1), ("Security Rover", 2),
    ("Sul'thraze", 1), ("Malkorok", 1), ("Obsidian Destroyer", 2), ("Acolyte of Pain", 2), ("Cursed Disciple", 2)],
    flags=["passive"],
    cards_=["ULDA_BOSS_73e"],
    notes=[("power", "Shockwave (passive): every minion on the boss's board has Rush (an aura, enchantment ULDA_BOSS_73e \"Sonic "
                     "Boom\"), so a minion it plays can attack a minion at once.")]))
B.append(boss(74, "barim", "High Prophet Barim", "High_Prophet_Barim", 1, "3", "control", [
    ("Brazen Zealot", 2), ("Desperate Measures", 2), ("EVIL Conscripter", 2), ("Grandmummy", 2), ("Penance", 2), ("Acolyte of Agony", 2),
    ("Dark Cultist", 2), ("Twilight's Call", 2), ("Holy Champion", 2), ("Psychopomp", 2), ("Shadow Madness", 2), ("Convincing Infiltrator", 1),
    ("Vol'jin", 1), ("Shadow Essence", 2), ("Museum Curator", 2), ("Acolyte of Pain", 2)],
    replacements={"Desperate Measures": "Power Word: Shield"},
    why="Desperate Measures by Power Word: Shield (a 1-Cost spell; Desperate Measures casts random Paladin Secrets, which this row "
        "does not generate for the boss).",
    notes=[("power", "Burst of Light (3): 1 damage to each enemy minion (the hero power damage rules apply), with Lifesteal (the power's "
                     "client tag): the boss's hero is healed by the damage dealt. Offered while the run player controls a minion.")]))
B.append(boss(75, "avozu", "Avozu", "Avozu", 1, "4", "midrange", [
    ("Feign Death", 2), ("Kindly Grandmother", 2), ("Desert Spear", 2), ("Hunter's Pack", 2), ("Kill Command", 2), ("Exploding Bloatbat", 2),
    ("Houndmaster Shaw", 1), ("Infested Wolf", 2), ("Savannah Highmane", 2), ("Swarm of Locusts", 2), ("Unleash the Beast", 2),
    ("Wild Bloodstinger", 2), ("Gladiator's Longbow", 1), ("Kobold Sandtrooper", 2), ("Anubisath Sentinel", 2), ("Wasteland Scorpid", 2)],
    replacements={"Hunter's Pack": "Animal Companion", "Kobold Sandtrooper": "Leper Gnome"},
    why="Hunter's Pack by Animal Companion (Hunter's Pack can give the boss a random Hunter Secret; this row generates none) and "
        "Kobold Sandtrooper by Leper Gnome (family 19's replacements of the same cards).",
    notes=[("power", "Hunt the Weak (2) deals N damage to a minion (any minion; the hero power damage rules apply), N the power's own "
                     "script number (the @ of its text), 1 at the start; then N rises by 1, or, when the minion dies of it, falls back "
                     "to 1 (\"lose all bonus damage\"). The page's note that the original AI tried not to kill minions with it is not "
                     "copied: the boss's AI decides.")]))
B.append(boss(76, "tarkus", "Tarkus", "Tarkus", 1, "2", "midrange", [
    ("Brazen Zealot", 2), ("Divine Strength", 2), ("Righteous Protector", 2), ("Dark Conviction", 2), ("Sandwasp Queen", 2), ("Subdue", 2),
    ("Ancestral Guardian", 2), ("Chillblade Champion", 2), ("Keeper of Uldaman", 2), ("Truesilver Champion", 2), ("Duel!", 1),
    ("Pharaoh's Blessing", 1), ("Sunkeeper Tarim", 1), ("Eadric the Pure", 1), ("Guardian of Kings", 1)],
    heroic=[("Righteous Protector", 2), ("Rebuke", 2), ("Subdue", 2), ("Muster for Battle", 2), ("Keeper of Uldaman", 2), ("Quartermaster", 2),
            ("Righteousness", 1), ("Stand Against Darkness", 2), ("Level Up!", 1), ("Pharaoh's Blessing", 1), ("Sunkeeper Tarim", 1),
            ("Vinecleaver", 2), ("Silver Sword", 1), ("Knife Juggler", 2), ("Silver Hand Regent", 2)],
    notes=[("power", "Noble Champion (4) deals damage to a random enemy minion (game RNG) equal to that minion's Attack (the hero power "
                     "damage rules apply). Offered while the run player controls a minion.")]))
B.append(boss(77, "gorebite", "Gorebite", "Gorebite", 2, "1", "aggro", [
    "Pounce", "Claw", "Savagery", "Predatory Instincts", "Ixlid, Fungal Lord", "Swamp Leech", "Acidic Swamp Ooze", "River Crocolisk",
    "Ironbeak Owl", "Scaleworm"],
    heroic=["Pounce", "Claw", "Enchanted Raven", "Power of the Wild", "Gnash", "Bite", "Gonk, the Raptor", "Sharkfin Fan", "Hench-Clan Thug",
            "Stranglethorn Tiger", "Maexxna", "Pit Crocolisk"],
    cards_=["ULDA_BOSS_77e"],
    level_note="Gorebite's page gives a table of possible cards without a \"Total cards\" column; its Normal side lists ten cards, "
               "the shape of a \"10; 15\" table, so the row takes that band (levels 1-2).",
    notes=[("power", "Snapping Jaw (2): the boss's hero gets +N Attack this turn, N its Attack when the power is used (\"double your "
                     "hero's attack\"; enchantment ULDA_BOSS_77e, removed at the end of the turn). Offered while the boss's hero has "
                     "Attack.")]))
B.append(boss(78, "octosari", "Octosari", "Octosari_(boss)", 1, "3", "midrange", [
    ("Acornbearer", 2), ("Claw", 2), ("Living Roots", 2), ("Landscaping", 2), ("BEEEES!!!", 2), ("Bite", 2), ("Poison Seeds", 2),
    ("Soul of the Forest", 2), ("Force of Nature", 2), ("Living Mana", 2), ("The Forest's Aid", 2), ("Cenarius", 1), ("Mulchmuncher", 2),
    ("Volcanic Lumberer", 2), ("Ultimate Infestation", 1), ("Sea Giant", 2)],
    notes=[("power", "Massive Tentacles (0), auto-cast as the page states (at the start of the boss's turn when the condition holds): "
                     "if there are 8 or more minions on the battlefield (both boards; a dormant or permanent minion does not count), "
                     "every one of them is destroyed. A run shrine destroyed so goes dormant (ShrineRules), it is not removed.")]))
B.append(boss(79, "armadillobot", "Armadillobot", "Armadillobot", 4, "2a", "aggro", [
    "Slam", "Vicious Scraphound", "Bouncing Blade", "Livewire Lance", "Armagedillo", "Target Dummy", "Jar Dealer", "EVIL Cable Rat",
    "Flying Machine", "Harvest Golem", "Dragonling Mechanic"],
    heroic=["Eternium Rover", "Warbot", "Bash", "Livewire Lance", "Restless Mummy", "Dyn-o-matic", "Mechwarper", "Micro Machine",
            "Upgradeable Framebot", "SN1P-SN4P", "Piloted Shredder", "Piloted Sky Golem"],
    flags=["forcedAttack"],
    cards_=["ULDA_BOSS_79e"],
    replacements={"Livewire Lance": "Fiery War Axe"},
    why="Livewire Lance by Fiery War Axe (family 19's replacement of the same card).",
    notes=[("power", "Mech \"Pilot\" (1) gives a Mech (any Mech: the text names no side) +2 Attack this turn (enchantment "
                     "ULDA_BOSS_79e) and makes it attack a random enemy minion of its own (game RNG), at once, whatever its state "
                     "(EncounterRules.ForcedAttack: Mass Hysteria's rule, no attack of the turn spent); with no enemy minion that can "
                     "fight it only gets the Attack.")]))

doc = {
    "schemaVersion": 1,
    "family": "22",
    "description": "Tombs of Terror bosses D (phase 8 family 22): eighteen encounters of Saviors of Uldum's Tombs of Terror (ULDA_BOSS_60 to "
                   "ULDA_BOSS_79 without 64 and 67: Zaraam, Kyriss, A Beautiful Oasis, Vessina, Thrud, Siamat, Weaponized Wasp, Isiset, "
                   "Rajh, Ammunae, Setesh, General Husam, High Prophet Barim, Avozu, Tarkus, Gorebite, Octosari, Armadillobot) and "
                   "Siamat's Servant (ULDA_026). Boss deck lists are transcribed from hearthstone.wiki.gg boss pages (read " + READ + "), "
                   "by card name; tools/dungeon-run-catalog (step 'encounters') resolves the names against facts/violet-hold-2026-09.json, "
                   "checks every card and writes data/dungeon-run/encounters-v1.json. Every Tombs boss hero prints 10 Health (A Beautiful "
                   "Oasis 30): Health is the act curve's, and the tier is the boss's level band read from its page's deck table (levels "
                   "1-2: tier 1; 2-3 and 3-4: tier 2; 5-6: tier 3; 7: tier 4; family 19's verify/tools/gen/p8f19/tombs_tiers.py).",
    "method": "The wiki's MediaWiki API was read for each page twice: the wikitext (action=raw) and the rendered deck table (action=parse, "
              "rowspans and colspans expanded); the two agree. Each Normal table is transcribed in table order with its own counts (25 "
              "or 30 cards); a table of possible cards without counts (\"10; 15\", \"15; 20\", Gorebite's) is transcribed with count "
              "null. Where the Normal list is shorter than 30 the Heroic table is transcribed too and is the row's extend list. A page's "
              "level note (\"These decks correspond only to level N\") and its chapter line are recorded in the row's schedule "
              "adaptation. The file is written by verify/tools/gen/p8f22/make_f22.py.",
    "bosses": B,
    "rejected": [],
}

text = json.dumps(doc, indent=1, ensure_ascii=False) + "\n"
with open(OUT, "w", encoding="utf-8", newline="\n") as f:
    f.write(text)
print("wrote " + os.path.relpath(OUT, REPO) + ": " + str(len(B)) + " bosses")
