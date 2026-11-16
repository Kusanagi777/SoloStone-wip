#!/usr/bin/env python3
# Phase 8 family 21 (Tombs of Terror bosses C, ULDA_BOSS_41-59 without 44): writes data/dungeon-run/reference/encounters/F21.json,
# the hand transcription of the boss pages of hearthstone.wiki.gg (read 2026-11-14). Run from anywhere:
#   python3 -I verify/tools/gen/p8f21/make_f21.py
# The tier rule is family 19's verify/tools/gen/p8f19/tombs_tiers.py (imported below; its header explains the sys.path line).
# Each Normal table is transcribed in table order with its own counts; a table of possible cards without counts ("10; 15") with
# count null; the Heroic table is the row's extend list. Jythiros of Questions (58) is rejected (see REJECTED). Output: LF, no
# BOM, one-space indent (as F19.json), keys in the order written here.
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", "..", ".."))
sys.path.insert(0, os.path.join(REPO, "verify", "tools", "gen", "p8f19"))
import tombs_tiers  # noqa: E402

OUT = os.path.join(REPO, "data", "dungeon-run", "reference", "encounters", "F21.json")
READ = "2026-11-14"
WIKI = "https://hearthstone.wiki.gg/wiki/"

UNSCRIPTED_NOTE = ("Listed cards the engine does not script (or does not admit for a boss) are replaced by a scripted card of the same "
                   "role, named in the row's replacements (the encounters step records each one): ")


def cards(names):
    out = []
    for item in names:
        name, count = item if isinstance(item, tuple) else (item, None)
        out.append({"name": name, "count": count})
    return out


def boss(n, slug, name, page, chapter, shape, archetype, normal, heroic=None, flags=None, cards_=None, forms=None,
         replacements=None, why=None, notes=()):
    level = tombs_tiers.level(shape)
    label = "Normal (level %d)" % level
    lists = [{"label": label, "cards": cards(normal)}]
    if heroic is not None:
        lists.append({"label": "Heroic", "cards": cards(heroic)})
    row = {
        "id": "ulda_%02d_%s" % (n, slug),
        "name": name,
        "provenance": "ULDA",
        "family": "21",
        "hero": "ULDA_BOSS_%02dh" % n,
        "power": "ULDA_BOSS_%02dp" % n if n != 52 else "ULDA_BOSS_52p1",
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
    if forms:
        row["forms"] = forms
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
B.append(boss(41, "alda", "Alda Petrik", "Alda_Petrik", 3, "1", "midrange", [
    "Rapid Fire", "Secret Plan", "Tracking", "Kindly Grandmother", "Trogg Beastrager", "Wandering Monster", "Desert Spear", "Dire Frenzy",
    "Worgen Infiltrator", "Ironbeak Owl", "Raging Worgen"],
    heroic=["Timber Wolf", "Headhunter's Hatchet", "Scavenging Hyena", "Desert Camel", "Spirit of the Lynx", "Dire Frenzy",
            "Halazzi, the Lynx", "Dire Wolf Alpha", "Untamed Beastmaster", "Lifedrinker"],
    replacements={"Secret Plan": "Animal Companion"},
    why="Secret Plan (it Discovers a Secret for the boss; this row generates none) by Animal Companion.",
    notes=[("power", "Running Wild (2) draws a random minion of the boss's deck (its own deck, which the boss knows); if it is a Beast, "
                     "a second copy of that card goes to its hand (while the hand has room). Offered while its hand has room and its deck "
                     "is not empty (both counts are public).")]))
B.append(boss(42, "rhogi", "Sandshaper Rhogi", "Sandshaper_Rhogi", 2, "2", "midrange", [
    ("Dust Devil", 2), ("Lightning Bolt", 2), ("Ancestral Knowledge", 2), ("Crushing Hand", 2), ("EVIL Totem", 2), ("Maelstrom Portal", 2),
    ("Sandstorm Elemental", 2), ("Stormforged Axe", 1), ("Drakkari Defender", 1), ("Elemental Destruction", 2), ("Mana Tide Totem", 2),
    ("Wicked Witchdoctor", 2), ("Bogshaper", 1), ("Livewire Lance", 2)],
    heroic=[("Maelstrom Portal", 2), ("Sandstorm Elemental", 2), ("Feral Spirit", 2), ("Hot Spring Guardian", 2), ("Storm Chaser", 2),
            ("Tidal Surge", 2), ("Rain of Toads", 2), ("Mistress of Mixtures", 2), ("Murmy", 2), ("Annoy-o-Tron", 2), ("Bloodmage Thalnos", 1),
            ("Lifedrinker", 2), ("Khartut Defender", 2)],
    cards_=["ULDA_BOSS_42e"],
    replacements={"EVIL Totem": "Flametongue Totem", "Livewire Lance": "Fiery War Axe", "Murmy": "Bluegill Warrior"},
    why="EVIL Totem by Flametongue Totem (a 2-Cost Shaman Totem), Livewire Lance by Fiery War Axe (a 2-Cost weapon of the same Attack) "
        "and Murmy (Heroic list) by Bluegill Warrior.",
    notes=[("power", "Sand Shapin' (3) shuffles a plain copy of an enemy minion's card (the board is public; never a run shrine) into the "
                     "boss's deck as a public shuffle, marked \"Summon when Drawn\" (ULDA_BOSS_42e). When the boss draws it, it is summoned "
                     "from its hand while the board has room and the boss draws a card (the Casts When Drawn rule, as family 04's Shadow "
                     "of Death); with a full board it stays in hand as a playable card. Offered while the deck has room."),
           ("power", "The client's Sand Rush Player Enchantment (ULDA_BOSS_42e2), Sandy Speed (ULDA_BOSS_42e3, \"Has Rush\") and the Sand "
                     "Shapin Dummy (ULDA_BOSS_42d) are client script holders the text does not name; the summoned copy gets no Rush.")]))
B.append(boss(43, "jolene", "Jolene Knottley", "Jolene_Knottley", 2, "4", "control", [
    ("Mortal Coil", 1), ("Sinister Deal", 2), ("Soul Infusion", 1), ("Voidwalker", 2), ("Darkshire Librarian", 2), ("Defile", 2),
    ("Drain Soul", 2), ("EVIL Genius", 2), ("Expired Merchant", 2), ("Gnomeferatu", 2), ("Queen of Pain", 2), ("EVIL Recruiter", 2),
    ("Aranasi Broodmother", 2), ("Dr. Morrigan", 1), ("Siphon Soul", 2), ("Twisting Nether", 1), ("EVIL Cable Rat", 2)],
    heroic=[("Flame Imp", 2), ("Kobold Librarian", 2), ("Mortal Coil", 1), ("Defile", 2), ("Gnomeferatu", 2), ("Darkshire Councilman", 2),
            ("Imp Gang Boss", 2), ("Imp-losion", 2), ("Despicable Dreadlord", 2), ("Twisting Nether", 1), ("Bloodmage Thalnos", 1),
            ("Stonesplinter Trogg", 2), ("Auctionmaster Beardo", 1), ("Archmage Vargoth", 1), ("Burly Rockjaw Trogg", 2), ("Violet Teacher", 2),
            ("Loatheb", 1), ("Gadgetzan Auctioneer", 1), ("Troggzor the Earthinator", 1)],
    flags=["hiddenZone"],
    cards_=["ULDA_BOSS_43e1"],
    replacements={"Sinister Deal": "Kobold Lackey", "Expired Merchant": "Voidwalker", "EVIL Recruiter": "Imp Gang Boss"},
    why="Sinister Deal by Kobold Lackey (the Lackey it would Discover), Expired Merchant by Voidwalker and EVIL Recruiter by Imp Gang "
        "Boss (of the boss's Heroic list).",
    notes=[("power", "Spoil the Ending (2) puts into the boss's hand a new copy of the card of a random spell in the run player's hand (the "
                     "card, Mind Vision's rule, not its enchantments), Spoiled: it costs (2) less (ULDA_BOSS_43e1). The boss learns exactly "
                     "that card (the copy in its own hand); nothing links the copy to the run player's hand card, whose hand stays masked in "
                     "the boss's fair view. Offered while the run player holds a card and the boss's hand has room (both counts are public); "
                     "with no spell in that hand it does nothing (the boss cannot know beforehand).")]))
B.append(boss(45, "belloc", "Belloc Brightblade", "Belloc_Brightblade", 4, "3", "midrange", [
    ("Brazen Zealot", 2), ("Crystalsmith Kangor", 1), ("Sandwasp Queen", 2), ("Salhet's Pride", 2), ("Seal of Champions", 2),
    ("Wickerflame Burnbristle", 1), ("Ancestral Guardian", 2), ("Chillblade Champion", 2), ("Hammer of Wrath", 2), ("Keeper of Uldaman", 1),
    ("Pharaoh's Blessing", 1), ("Sunkeeper Tarim", 1), ("Lynessa Sunsorrow", 1), ("Mortal Coil", 1), ("Reliquary Seeker", 2),
    ("Sinister Deal", 2), ("Riftcleaver", 2), ("Blood Knight", 2), ("Khartut Defender", 2)],
    heroic=[("Brazen Zealot", 1), ("Crystology", 2), ("Righteous Protector", 2), ("Selfless Hero", 2), ("Crystalsmith Kangor", 1),
            ("Micro Mummy", 2), ("Shielded Minibot", 2), ("Subdue", 2), ("Divine Favor", 2), ("Muster for Battle", 2), ("Unidentified Maul", 2),
            ("Consecration", 2), ("Keeper of Uldaman", 2), ("Truesilver Champion", 1), ("Spikeridged Steed", 2), ("Sunkeeper Tarim", 1),
            ("Val'anyr", 1), ("Silver Sword", 1)],
    flags=["passive"],
    replacements={"Sinister Deal": "Kobold Lackey", "Riftcleaver": "Truesilver Champion"},
    why="Sinister Deal by Kobold Lackey (the Lackey it would Discover) and Riftcleaver by Truesilver Champion (of the boss's Heroic list).",
    notes=[("deck", "The wiki's level-6 table sums to 31 cards (its Paladin rows 20, Warlock 7, Neutral 4); a combat tier fights with 30, "
                    "so the last listed card's second copy (Khartut Defender) is left out."),
           ("power", "Reliquary's Relics (passive): after the run player casts a spell (a public cast; a Secret too: the cast tells its "
                     "card, as the original's copy did), a new copy of that card goes to the boss's hand while it has room.")]))
B.append(boss(46, "wildtooth", "Wildtooth", "Wildtooth", 2, "3", "midrange", [
    ("Hunter's Mark", 2), ("Carrion Grub", 2), ("Bearshark", 4), ("Desert Camel", 2), ("Exploding Bloatbat", 2), ("Baited Arrow", 2),
    ("Gral, the Shark", 1), ("Dire Mole", 2), ("Tentacle of N'Zoth", 2), ("Nat Pagle", 1), ("Serpent Egg", 2), ("Desert Hare", 2),
    ("Arena Treasure Chest", 2), ("Twilight Summoner", 2), ("Sated Threshadon", 2)],
    heroic=[("Fiery Bat", 2), ("Bearshark", 2), ("Nine Lives", 2), ("Spider Bomb", 2), ("Savannah Highmane", 2), ("Seeping Oozeling", 2),
            ("Gral, the Shark", 1), ("Nerubian Egg", 2), ("Serpent Egg", 2), ("Devilsaur Egg", 2), ("Piloted Shredder", 2),
            ("Twilight Summoner", 2), ("Carnivorous Cube", 2), ("Blatant Decoy", 2), ("Mechanical Whelp", 2), ("Sylvanas Windrunner", 1)],
    flags=["passive", "bothPlayers"],
    replacements={"Gral, the Shark": "Savannah Highmane", "Desert Hare": "Fiery Bat"},
    why="Gral, the Shark (it eats a minion of its owner's deck, which no boss row admits yet: admission from-own-deck) by Savannah "
        "Highmane and Desert Hare by Fiery Bat (both of the boss's Heroic list).",
    notes=[("deck", "The wiki lists four Bearsharks in the level-6 deck; all four are kept (a boss deck has no copy limit)."),
           ("power", "Maw of the Desert (0): the page marks it Auto-cast (and the client card carries AI_MUST_PLAY), so it is cast by rule, "
                     "not chosen by the boss's AI (family 09's rule-cast powers): at the start of each of the boss's turns, while any "
                     "minion is on either board, the minion with the lowest Health (either board; a tie by the game RNG; never a dormant "
                     "minion) is destroyed. A run shrine picked goes dormant, as from any destroy effect.")]))
B.append(boss(47, "jar", "Totally Normal Jar", "Totally_Normal_Jar", 4, "2", "midrange", [
    ("Grandmummy", 2), ("Mortal Coil", 2), ("Possessed Villager", 2), ("Spirit Bomb", 2), ("Expired Merchant", 1), ("Spreading Madness", 2),
    ("Eager Underling", 2), ("Kabal Trafficker", 1), ("Jar Dealer", 2), ("Kobold Sandtrooper", 2), ("Infested Goblin", 2),
    ("Desert Obelisk", 5), ("Khartut Defender", 1)],
    heroic=[("Micro Mummy", 2), ("Grandmummy", 2), ("Possessed Villager", 2), ("Sanguine Reveler", 2), ("Despicable Dreadlord", 2),
            ("Restless Mummy", 2), ("Tentacle of N'Zoth", 1), ("Haunted Creeper", 2), ("Lost Spirit", 2), ("Nerubian Egg", 2), ("Serpent Egg", 2),
            ("Emperor Thaurissan", 1), ("Khartut Defender", 2), ("Ragnaros the Firelord", 1)],
    cards_=["ULDA_BOSS_47e"],
    replacements={"Expired Merchant": "Possessed Villager", "Kobold Sandtrooper": "Leper Gnome", "Desert Obelisk": "Nerubian Egg"},
    why="Expired Merchant by Possessed Villager, Kobold Sandtrooper by Leper Gnome (the same Deathrattle, 2 damage) and the five Desert "
        "Obelisks by Nerubian Eggs (of the boss's Heroic list: a cheap body that pays off later).",
    notes=[("deck", "The wiki's level-4 table sums to 26 cards (five Desert Obelisks); the five Obelisks are kept."),
           ("power", "What's in the Jar? (2) summons for the boss a copy of any minion (the board is public; never a run shrine, which no "
                     "copy may duplicate), its stats set to 1/1 (ULDA_BOSS_47e); it needs a free slot.")]))
B.append(boss(48, "ilza", "Ilza Grimdrum", "Ilza_Grimdrum", 4, "2", "midrange", [
    ("Devastate", 2), ("Cruel Taskmaster", 2), ("Execute", 2), ("Frightened Flunky", 2), ("Bloodsworn Mercenary", 2), ("Fiery War Axe", 2),
    ("Mountainfire Armor", 2), ("Arathi Weaponsmith", 2), ("Brawl", 1), ("Injured Tol'vir", 2), ("Injured Blademaster", 1), ("Wolfrider", 2),
    ("Khartut Defender", 2), ("Wasteland Scorpid", 1)],
    heroic=[("Shield Slam", 1), ("Bring It On!", 2), ("Execute", 2), ("Slam", 2), ("Bash", 2), ("Shield Block", 2), ("Bloodhoof Brave", 2),
            ("Magnataur Alpha", 2), ("Brawl", 1), ("Festeroot Hulk", 2), ("Saronite Chain Gang", 1), ("Injured Tol'vir", 2),
            ("Injured Blademaster", 2), ("Hungry Ettin", 2)],
    flags=["passive", "bothPlayers", "hiddenZone"],
    replacements={"Injured Tol'vir": "Frostwolf Grunt"},
    why="Injured Tol'vir by Frostwolf Grunt (as family 19).",
    notes=[("power", "Arena Expert (0): the page marks it Auto-cast, so it is cast by rule (family 09's rule-cast powers): at the start of each "
                     "of the boss's turns, while its board has room, a random minion of each player's deck is summoned (Duel!'s rule, "
                     "DAL_731: the summon makes each public) and the boss's attacks the run player's.")]))
B.append(boss(49, "craz", "Craz", "Craz", 3, "4", "midrange", [
    ("Dark Possession", 2), ("Sinister Deal", 2), ("EVIL Genius", 2), ("Expired Merchant", 2), ("EVIL Recruiter", 2), ("Sense Demons", 1),
    ("Imp-losion", 2), ("Pit Lord", 2), ("Portal Keeper", 2), ("Demonheart", 2), ("Aranasi Broodmother", 2), ("Ectomancy", 1),
    ("Kabal Trafficker", 2), ("Riftcleaver", 2), ("Jumbo Imp", 2), ("EVIL Cable Rat", 2)],
    heroic=[("Lakkari Sacrifice", 1), ("Malchezaar's Imp", 2), ("The Soularium", 1), ("Clutchmother Zavas", 1), ("Darkshire Librarian", 2),
            ("Expired Merchant", 2), ("Tiny Knight of Evil", 2), ("High Priestess Jeklik", 1), ("Imp-losion", 1), ("Lakkari Felhound", 2),
            ("Voidcaller", 2), ("Blood-Queen Lana'thel", 1), ("Doomguard", 2), ("Riftcleaver", 1), ("Rin, the First Disciple", 1),
            ("Siegebreaker", 2), ("Soulwarden", 2), ("Mal'Ganis", 1), ("Voidlord", 2), ("Deathwing", 1)],
    replacements={"Sinister Deal": "Kobold Lackey", "Expired Merchant": "Voidwalker", "EVIL Recruiter": "Imp Gang Boss",
                  "Riftcleaver": "Doomguard"},
    why="Sinister Deal by Kobold Lackey, Expired Merchant by Voidwalker, EVIL Recruiter by Imp Gang Boss and Riftcleaver by Doomguard "
        "(of the boss's Heroic list; Demons for the Demon deck).",
    notes=[("power", "Demon Emissary (2) summons a random scripted Demon of any class that costs (3) or less and that the run's generation "
                     "filter allows for the boss; it needs a free slot.")]))
B.append(boss(50, "barkeye", "Ol' Barkeye", "Ol'_Barkeye_(boss)", 1, "3", "aggro", [
    ("Candleshot", 2), ("Snipe", 2), ("Animal Companion", 1), ("Bloodscalp Strategist", 2), ("Deadly Shot", 2), ("Desert Spear", 2),
    ("Hunter's Pack", 2), ("Flanking Strike", 2), ("Hyena Alpha", 2), ("Marked Shot", 2), ("Piranha Launcher", 1), ("Savannah Highmane", 1),
    ("Wild Bloodstinger", 2), ("Gladiator's Longbow", 1), ("Kobold Sandtrooper", 2), ("Earthen Ring Farseer", 2), ("Lifedrinker", 2)],
    heroic=[("Candleshot", 2), ("Bear Trap", 1), ("Cat Trick", 1), ("Explosive Trap", 2), ("Freezing Trap", 1), ("Pressure Plate", 1),
            ("Rat Trap", 1), ("Wandering Monster", 2), ("Animal Companion", 2), ("Deadly Shot", 2), ("Desert Spear", 2), ("Eaglehorn Bow", 2),
            ("Hunter's Pack", 2), ("Unleash the Hounds", 2), ("Marked Shot", 1), ("Lesser Emerald Spellstone", 2), ("To My Side!", 2),
            ("Unleash the Beast", 1), ("Rhok'delar", 1)],
    flags=["passive"],
    replacements={"Hunter's Pack": "Animal Companion", "Kobold Sandtrooper": "Leper Gnome"},
    why="Hunter's Pack (it can give the boss a random Hunter Secret; this row generates none) by Animal Companion and Kobold Sandtrooper by "
        "Leper Gnome (as family 19).",
    notes=[("power", "Desert Warfare (passive): after the boss's hero attacks, a random scripted Hunter card the run's generation filter "
                     "allows for the boss goes to its hand while it has room; Quests and Secrets are left out of the pool (the boss generates "
                     "no Secret on this row).")]))
B.append(boss(51, "kasmut", "Kasmut", "Kasmut", 3, "2", "control", [
    ("Anubisath Defender", 1), ("Embalming Ritual", 2), ("Grandmummy", 2), ("Psychopomp", 1), ("Shadow Essence", 2), ("Plague of Death", 1),
    ("Murmy", 2), ("Loot Hoarder", 2), ("Temple Berserker", 2), ("Candletaker", 2), ("Generous Mummy", 2), ("Bone Wraith", 2),
    ("Khartut Defender", 2), ("Wrapped Golem", 2)],
    heroic=[("Grandmummy", 2), ("Vivid Nightmare", 2), ("Wretched Reclaimer", 2), ("High Priest Amet", 1), ("Psychopomp", 2),
            ("Shadow Madness", 2), ("Mass Hysteria", 1), ("Onyx Bishop", 2), ("Candletaker", 2), ("Generous Mummy", 2), ("Bone Wraith", 2),
            ("Khartut Defender", 2), ("Wrapped Golem", 2), ("Colossus of the Moon", 1)],
    flags=["passive", "bothPlayers"],
    replacements={"Murmy": "Micro Mummy", "Generous Mummy": "Restless Mummy"},
    why="Murmy by Micro Mummy and Generous Mummy by Restless Mummy (both Reborn, as family 19), so Tol'vir Necromancy keeps its Reborn "
        "minions.",
    notes=[("power", "Tol'vir Necromancy (passive): after a minion of either player is Reborn (the engine's Reborn return), it is restored to "
                     "full Health (a heal from the boss's power: healing rules apply).")]))
B.append(boss(52, "bazhial", "Lich Baz'hial", "Lich_Baz'hial", 3, "4", "control", [
    ("Ultimate Infestation", 1), ("Ray of Frost", 2), ("Frostbolt", 2), ("Arcane Intellect", 2), ("Frost Nova", 2), ("Water Elemental", 2),
    ("Anomalus", 1), ("Sindragosa", 1), ("Plague of Death", 1), ("Shadow of Death", 2), ("Anub'arak", 1), ("Crystallizer", 2),
    ("Bloodmage Thalnos", 1), ("Acolyte of Pain", 1), ("Ancient Shade", 2), ("Bone Wraith", 2), ("Abomination", 2), ("Bittertide Hydra", 2),
    ("The Lich King", 1)],
    heroic=[("Ray of Frost", 2), ("Frostbolt", 2), ("Primordial Glyph", 2), ("Arcane Intellect", 2), ("Frost Nova", 2), ("Fireball", 2),
            ("Blizzard", 2), ("Astromancer", 1), ("Power of Creation", 2), ("Tortollan Pilgrim", 2), ("Archbishop Benedictus", 1),
            ("Dead Man's Hand", 2), ("Doomsayer", 2), ("Khartut Defender", 2), ("The Lich King", 1), ("Alexstrasza", 1), ("Arcane Giant", 2)],
    flags=["forms"],
    forms=[{"power": "ULDA_BOSS_52p2", "at": {"script": True}, "keepExcess": True}],
    notes=[("power", "Lich's Phylactery (0) restores @ Health to the boss's hero, @ starting at 20 (the card's script number) and falling by 1 "
                     "for every friendly minion that dies (never below 0). The page shows both powers for one fight and gives no rule; the "
                     "client card's mechanic is \"Modify Hero Power\" and its successor Broken Phylactery is passive, so using it once "
                     "breaks it: the boss enters form 1 (a script form that changes only the power: the hero, its Health, Armor and "
                     "weapon stay) with Broken Phylactery, \"You take double damage from fatigue\" (each fatigue hit on the boss's hero is "
                     "doubled; the count itself rises as usual).")]))
B.append(boss(53, "maru", "Taskmaster Maru", "Taskmaster_Maru", 1, "2", "tempo", [
    ("Daring Escape", 2), ("Togwaggle's Scheme", 2), ("Cavern Shinyfinder", 1), ("Whirlkick Master", 2), ("Edwin VanCleef", 1),
    ("EVIL Miscreant", 2), ("Hooked Scimitar", 2), ("Elven Minstrel", 2), ("Sahket Sapper", 1), ("Waggle Pick", 2), ("Jar Dealer", 2),
    ("EVIL Cable Rat", 2), ("Knife Juggler", 2), ("Vulpera Scoundrel", 2)],
    heroic=[("Backstab", 2), ("Counterfeit Coin", 1), ("Shadowstep", 2), ("Daring Escape", 1), ("Eviscerate", 2), ("Edwin VanCleef", 1),
            ("EVIL Miscreant", 2), ("SI:7 Agent", 2), ("Elven Minstrel", 2), ("Spirit of the Shark", 2), ("Waggle Pick", 2),
            ("Vilespine Slayer", 2), ("Heistbaron Togwaggle", 1), ("EVIL Cable Rat", 2), ("Barista Lynchen", 1)],
    replacements={"Sahket Sapper": "SI:7 Agent"},
    why="Sahket Sapper by SI:7 Agent (of the boss's Heroic list).",
    notes=[("deck", "The page gives a 25-card Normal list without a level note: the 25-card shape of levels 3-4 (tombs_tiers shape 2)."),
           ("power", "E.V.I.L. Conscription (1) adds a random Lackey (the engine's Lackey pool: the seven Lackeys, scripted ones only, "
                     "behind the run's generation filter) to the boss's hand; offered while its hand has room.")]))
B.append(boss(54, "janak", "Janak", "Janak", 3, "2", "aggro", [
    ("Improve Morale", 2), ("Battle Rage", 1), ("Bloodsworn Mercenary", 2), ("Fierce Monkey", 2), ("Kobold Barbarian", 2), ("Livewire Lance", 2),
    ("Val'kyr Soulclaimer", 2), ("Axe Flinger", 1), ("Bloodhoof Brave", 1), ("Crush", 2), ("EVIL Cable Rat", 2), ("Hench-Clan Hogsteed", 2),
    ("Acolyte of Pain", 2), ("Sunstruck Henchman", 2)],
    heroic=[("Warbot", 2), ("Rampage", 2), ("Warpath", 2), ("Bloodsworn Mercenary", 2), ("Frothing Berserker", 2), ("Axe Flinger", 2),
            ("Bloodhoof Brave", 2), ("Grommash Hellscream", 1), ("Hench-Clan Hogsteed", 2), ("Temple Berserker", 2), ("Acolyte of Pain", 2),
            ("Raging Worgen", 2), ("Vicious Fledgling", 2)],
    cards_=["ULDA_BOSS_54e"],
    replacements={"Livewire Lance": "Fiery War Axe", "Sunstruck Henchman": "Chillwind Yeti"},
    why="Livewire Lance by Fiery War Axe and Sunstruck Henchman by Chillwind Yeti (as family 19).",
    notes=[("power", "Cavalry of E.V.I.L. (1) deals 1 damage to any minion, then gives it Charge (ULDA_BOSS_54e) if it survived.")]))
B.append(boss(55, "murlocs", "Water-Toting Murlocs", "Water-Toting_Murlocs", 2, "1", "aggro", [
    "Zap!", "Totemic Smash", "Siltfin Spiritwalker", "Murloc Tinyfin", "Murloc Raider", "Murmy", "Fishflinger", "Puddlestomper",
    "Murloc Tastyfin", "Corrupted Seer"],
    heroic=["Grimscale Chum", "Murloc Knight", "Underbelly Angler", "Rockpool Hunter", "Coldlight Seer", "Murloc Warleader",
            "Nightmare Amalgam", "Old Murk-Eye"],
    flags=["bothPlayers"],
    replacements={"Murmy": "Bluegill Warrior", "Fishflinger": "Murloc Tidehunter"},
    why="Murmy by Bluegill Warrior (a Murloc, as family 19's Herring) and Fishflinger by Murloc Tidehunter (a 2-Cost Murloc).",
    notes=[("deck", "The page's table has no \"Total cards\" column, but its shape is the \"10; 15\" table of possible cards (ten Normal, "
                    "eight Heroic): levels 1-2."),
           ("power", "Fish Food (1) restores 2 Health to every Murloc of either board (\"all Murlocs\" names no side); offered while a "
                     "Murloc is on either board.")]))
B.append(boss(56, "frog", "Sand Puffer Frog", "Sand_Puffer_Frog", 1, "1", "aggro", [
    "Sludge Slurper", "Sandstorm Elemental", "Feral Gibberer", "Helpless Hatchling", "Murmy", "EVIL Cable Rat", "Huge Toad", "Jeweled Scarab",
    "Quicksand Elemental"],
    heroic=["Fiery Bat", "Springpaw", "Timber Wolf", "Webspinner", "Acherus Veteran", "Fire Fly", "Glacial Shard", "Huge Toad"],
    flags=["passive"],
    cards_=["ULDA_BOSS_56e"],
    replacements={"Murmy": "Micro Mummy", "Quicksand Elemental": "Tar Creeper"},
    why="Murmy by Micro Mummy (a 1-Cost minion for Sandy Surprise) and Quicksand Elemental by Tar Creeper (as family 19).",
    notes=[("power", "Sandy Surprise (passive): after the boss plays a minion whose card costs (1), it gets Stealth (ULDA_BOSS_56e).")]))
B.append(boss(57, "winged_guardian", "Winged Guardian", "Winged_Guardian_(boss)", 1, "2", "midrange", [
    ("Brazen Zealot", 2), ("Meanstreet Marshal", 2), ("Micro Mummy", 2), ("Aldor Peacekeeper", 2), ("Ancestral Guardian", 2),
    ("Hammer of Wrath", 2), ("Murloc Knight", 2), ("Mechano-Egg", 2), ("Pharaoh's Blessing", 2), ("Dragon Egg", 1), ("Swamp Dragon Egg", 2),
    ("Serpent Egg", 2), ("Devilsaur Egg", 2)],
    heroic=[("Lost in the Jungle", 2), ("Righteous Protector", 2), ("Meanstreet Marshal", 2), ("Rallying Blade", 1), ("Sunkeeper Tarim", 1),
            ("Argent Squire", 2), ("Dragon Egg", 2), ("Swamp Dragon Egg", 2), ("Echoing Ooze", 2), ("Nerubian Egg", 2), ("Scarab Egg", 2),
            ("Serpent Egg", 2), ("Devilsaur Egg", 2)],
    cards_=["ULDA_BOSS_57e"],
    notes=[("power", "Titan's Beast (2) sets any minion's Attack and Health to 3 (ULDA_BOSS_57e).")]))
B.append(boss(59, "dire_bat", "Dire Bat", "Dire_Bat", 3, "2", "aggro", [
    ("Fiery Bat", 2), ("Stampede", 1), ("Webspinner", 2), ("Grievous Bite", 2), ("Hunter's Mark", 1), ("Carrion Grub", 2), ("Dire Frenzy", 1),
    ("Exploding Bloatbat", 2), ("Wing Blast", 2), ("Starving Buzzard", 1), ("Swamp Leech", 2), ("River Crocolisk", 2), ("Vicious Scalehide", 2),
    ("Sightless Ranger", 2), ("Countess Ashmore", 1)],
    heroic=[("Springpaw", 2), ("Timber Wolf", 2), ("Crackling Razormaw", 2), ("Hunter's Mark", 2), ("Kindly Grandmother", 1),
            ("Scavenging Hyena", 2), ("Animal Companion", 2), ("Kill Command", 1), ("Unleash the Hounds", 2), ("Dire Frenzy", 2),
            ("Flanking Strike", 2), ("Wing Blast", 2), ("Tundra Rhino", 1), ("Soup Vendor", 2)],
    notes=[("power", "Food Chain (2) deals 2 damage to any minion; if it dies, the boss's hero is restored 5 Health.")]))

REJECTED = [{
    "id": "ULDA_BOSS_58h",
    "name": "Jythiros of Questions",
    "power": "ULDA_BOSS_58p",
    "reason": "Riddle of the Sphinx (\"Guess which card the Boss will play to banish it\") is a decision of the run player on the boss's "
              "turn (the wiki: it shows the cards in the boss's hand and the card it draws that turn) whose correct guess counters the "
              "boss's card of any type (the wiki: \"it is immediately countered\"; the first encounter to counter every card type). The "
              "engine's decision loop, the bosspilot harnesses and NativeMatch answer only the current player's decisions, and the engine "
              "counters spells only (Spell.IsCountered in PlayCard.PlaySpell); a guess moved to the run player's own turn and a counter "
              "limited to spells would be an invented mechanic. The encounter is left out",
    "lift": "phase 9 (economy): a decision of the non-current player in the engine's decision loop, NativeMatch (the client prompt) and the "
            "harnesses, and a Counter for minion, weapon, hero and location plays in PlayCard",
    "cards": [],
}]

doc = {
    "schemaVersion": 1,
    "family": "21",
    "description": "Tombs of Terror bosses C (phase 8 family 21): seventeen encounters of Saviors of Uldum's Tombs of Terror (ULDA_BOSS_41 to "
                   "ULDA_BOSS_59 without 44, Trap Room, which is family 01's, and without 58, Jythiros of Questions, rejected below: Alda "
                   "Petrik, Sandshaper Rhogi, Jolene Knottley, Belloc Brightblade, Wildtooth, Totally Normal Jar, Ilza Grimdrum, Craz, Ol' "
                   "Barkeye, Kasmut, Lich Baz'hial, Taskmaster Maru, Janak, Water-Toting Murlocs, Sand Puffer Frog, Winged Guardian, Dire "
                   "Bat). Boss deck lists are transcribed from hearthstone.wiki.gg boss pages (read " + READ + "), by card name; "
                   "tools/dungeon-run-catalog (step 'encounters') resolves the names against facts/violet-hold-2026-09.json, checks every "
                   "card and writes data/dungeon-run/encounters-v1.json. Every Tombs boss hero prints 10 Health: Health is the act curve's, "
                   "and the tier is the boss's level band read from its page's deck table (family 19's rule, "
                   "verify/tools/gen/p8f19/tombs_tiers.py).",
    "method": "The wikitext of each boss page (action=raw) was read and its deck tables transcribed in table order with their own counts; a "
              "table of possible cards without counts is transcribed with count null. The Heroic table is the row's extend list. A page's "
              "level note and its chapter line are recorded in the row's schedule adaptation. Ol' Barkeye's and Winged Guardian's pages are "
              "the boss pages \"Ol' Barkeye (boss)\" and \"Winged Guardian (boss)\". The file is written by "
              "verify/tools/gen/p8f21/make_f21.py.",
    "bosses": B,
    "rejected": REJECTED,
}

text = json.dumps(doc, indent=1, ensure_ascii=False) + "\n"
with open(OUT, "w", encoding="utf-8", newline="\n") as f:
    f.write(text)
print("wrote " + os.path.relpath(OUT, REPO) + ": " + str(len(B)) + " bosses, " + str(len(REJECTED)) + " rejected")
