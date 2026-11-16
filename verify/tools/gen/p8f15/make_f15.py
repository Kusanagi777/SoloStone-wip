# Phase 8 family 15 (Dalaran Heist bosses B): writes data/dungeon-run/reference/encounters/F15.json, the hand transcription of
# the fourteen encounters DALA_BOSS_16-20 and 22-30 (Whirt 21 is family 01's). The deck tables were read from the
# hearthstone.wiki.gg boss pages (wikitext, ?action=raw, 2026-11-14; the raw pages are not kept) and transcribed here in
# table order, Normal column, with the table's own counts (a "Possible cards" table has none: each card once). Names are
# the card names; a boss-only or ambiguous card carries its id. Deterministic, LF, no BOM; no network.
# Usage: python3 -I verify/tools/gen/p8f15/make_f15.py [<out file>]
import collections, json, os, sys

V = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))  # verify/
REPO = os.path.dirname(V)
READ = "2026-11-14"
WIKI = "https://hearthstone.wiki.gg/wiki/"
OD = collections.OrderedDict

# The Normal deck table of each page (the Heroic column is not used, as family 01 did for Whirt and Trap Room).
LISTS = {
    16: [('Freezing Potion', None), ('Potion of Polymorph', None), ('Volcanic Potion', None), ('Pint-Size Potion', None), ('Potion of Madness', None), ('Greater Healing Potion', None), ('Potion of Heroism', None), ('Blastcrystal Potion', None), ('Mistress of Mixtures', None), ('Potion Vendor', None), ('Ancient Watcher', None), ('Deranged Doctor', None), ('Kabal Chemist', None)],
    17: [('Moonfire', 2), ('Crystal Power', 2), ('Living Roots', 1), ('Darnassus Aspirant', 2), ('Druid of the Saber', 2), ('Grove Tender', 2), ('Wild Growth', 2), ('Forest Guide', 1), ('Keeper of the Grove', 1), ('Shellshifter', 2), ('Verdant Longneck', 2), ('Webweave', 2), ('Nourish', 1), ('Ancient of Lore', 1), ('Dreampetal Florist', 2), ('Malorne', 1), ('Dire Wolf Alpha', 2), ('Splitting Festeroot', 2)],
    18: [('Beakered Lightning', 1), ('Mutate', 1), ('Totemic Might', 1), ('Earth Shock', 1), ('Frost Shock', 1), ('Ancestral Knowledge', 2), ('Maelstrom Portal', 1), ('Primalfin Totem', 2), ('Windfury', 1), ('Elemental Destruction', 2), ('Far Sight', 2), ('Mana Tide Totem', 2), ('Primal Talismans', 1), ('Tuskarr Totemic', 2), ('Muckmorpher', 2), ('Volcano', 2), ('Windshear Stormcaller', 1), ('Thing from Below', 1), ('Dire Wolf Alpha', 2), ('Nightmare Amalgam', 2)],
    19: [('Biteweed', None), ('Defias Ringleader', None), ('Gang Up', None), ('Patient Assassin', None), ('Shiv', None), ('Fan of Knives', None), ('SI:7 Agent', None), ('Hench-Clan Burglar', None), ('Master of Disguise', None), ("Assassin's Blade", None), ('Shado-Pan Rider', None), ('Crazed Alchemist', None), ('Dozing Marksman', None), ('Frostwolf Grunt', None), ('Ironbeak Owl', None), ('Spellward Jeweler', None), ('Street Trickster', None), ('Ogre Magi', None)],
    20: [('Beakered Lightning', 2), ('Mutate', 1), ('Finders Keepers', 2), ('Forked Lightning', 2), ('Sludge Slurper', 2), ('Voltaic Burst', 2), ('Ancestral Knowledge', 2), ('Crushing Hand', 2), ('Stormcrack', 2), ('Elemental Destruction', 1), ('Feral Spirit', 1), ('Lightning Storm', 1), ('Unbound Elemental', 2), ('Thunderhead', 2), ('Wicked Witchdoctor', 2), ('Rain of Toads', 1), ("Witch's Cauldron", 2), ('Violet Spellsword', 1)],
    22: [('Silence', None), ('Chameleos', None), ('Mind Vision', None), ('Potion of Madness', None), ('Mirage Caller', None), ('Shadow Madness', None), ('Quartz Elemental', None), ('Ancient Watcher', None), ('Dalaran Librarian', None), ('Humongous Razorleaf', None), ('Icehowl', None), ('Eerie Statue', None), ('Rattling Rascal', None), ('Ticking Abomination', None), ('Bittertide Hydra', None), ('Bomb Squad', None), ('Fel Reaver', None), ('Ragnaros the Firelord', None)],
    23: [('Pounce', None), ('Claw', None), ('Earthen Scales', None), ('Enchanted Raven', None), ('Savage Striker', None), ('Feral Rage', None), ('Mounted Raptor', None), ('Wild Growth', None), ('Bite', None), ('Savage Combatant', None), ('Grizzled Guardian', None), ('Dire Mole', None), ('Swamp Leech', None), ('Haunted Creeper', None), ('Huge Toad', None)],
    24: [('Alleycat', 2), ('Springpaw', 2), ('Crackling Razormaw', 2), ("Headhunter's Hatchet", 1), ('Hidden Cache', 2), ("King's Elekk", 2), ('Snipe', 2), ('Jeweled Scarab', 2), ('Mad Bomber', 2), ('Coldlight Oracle', 2), ('King Mukla', 1), ('Sewer Crawler', 2), ('Lifedrinker', 2), ('Refreshment Vendor', 2), ('Volcanosaur', 2), ('Primordial Drake', 1), ('North Sea Kraken', 1)],
    25: [('Hoard of Toys', 2, 'DALA_BOSS_25t'), ('Target Dummy', 2), ('Goblin Bomb', 2), ('Annoy-o-Tron', 2), ('Mechwarper', 2), ('Alarm-o-Bot', 2), ('Demolisher', 2), ('Meat Wagon', 2), ('Piloted Reaper', 2), ('Piloted Shredder', 2), ("Mimiron's Head", 1), ('Zilliax', 1), ('Mechanical Whelp', 2), ('Missile Launcher', 2), ('Piloted Sky Golem', 2), ('Foe Reaper 4000', 1), ("Sneed's Old Shredder", 1)],
    26: [('Sludge Slurper', 2), ('Brrrloc', 2), ('Ghost Light Angler', 2), ('Soul of the Murloc', 2), ('Underbelly Angler', 2), ('Ice Breaker', 2), ('Storm Chaser', 2), ('Tidal Surge', 2), ('Voodoo Hexxer', 2), ('Moorabi', 1), ('The Mistcaller', 1), ('The Storm Bringer', 1), ('Everyfin is Awesome', 2), ('Neptulon', 1), ('Walking Fountain', 2), ('Primalfin Lookout', 2), ('Murloc Tastyfin', 2)],
    27: [('Mind Vision', 1), ('Burgle', 1), ('Murloc Tinyfin', 1, 'LOEA10_3'), ('Snowflipper Penguin', 1), ('Target Dummy', 2), ('Wisp', 2), ('Angry Chicken', 2), ('Emerald Hive Queen', 2), ('Zombie Chow', 2), ('Ancient Watcher', 2), ('Millhouse Manastorm', 1), ('Dirty Rat', 2), ('Doomsayer', 2), ('Alarm-o-Bot', 2), ('Deathlord', 2), ('Maiden of the Lake', 2), ('Ticking Abomination', 2), ('Mojomaster Zihi', 1)],
    28: [('Duplicate', 2), ('Mana Bind', 1), ('Simulacrum', 2), ('Vaporize', 2), ('Volcanic Potion', 2), ('Kirin Tor Tricaster', 2), ('Wee Spellstopper', 1), ('Curio Collector', 1), ('Flame Lance', 1), ('Anomalus', 1), ('Glacial Mysteries', 1), ('Arcane Anomaly', 2), ('Explosive Sheep', 1), ('Loot Hoarder', 2), ('Novice Engineer', 2), ('Acolyte of Pain', 2), ('Shimmering Courser', 2), ('Arcane Tyrant', 2), ('Red Mana Wyrm', 1)],
    29: [('Elemental Evocation', None), ('Flame Geyser', None), ('Crystalline Oracle', None), ('Mana Geode', None), ('Quartz Elemental', None), ('Earthen Might', None), ('Elementary Reaction', None), ('Lava Burst', None), ('Unbound Elemental', None), ('Rumbling Elemental', None), ('Earth Elemental', None), ('Volcano', None), ('Stone Sentinel', None), ('Magma Rager', None), ('Ozruk', None), ('Mountain Giant', None)],
    30: [('Claw', 2), ('Bite', 2), ('Hidden Cache', 2), ('Rat Trap', 2), ('Rat Pack', 2), ('Seeping Oozeling', 1), ('Swamp Leech', 2), ('Acidic Swamp Ooze', 1), ('Dirty Rat', 2), ('Echoing Ooze', 2), ('EVIL Cable Rat', 2), ('Giant Wasp', 1), ('Sewer Crawler', 2), ('Toxic Sewer Ooze', 1), ('Ticking Abomination', 2), ('Mossy Horror', 2), ('Tunnel Blaster', 2)],
}

TIERS = "levels 1-2: tier 1; 3-4: tier 2; 5-6: tier 3; 7-8: tier 4 (the encounter model's level table)"


def tier_note(level_text, level, tier):
    return ("The Heist hero card prints 30 Health (10 for a level-1 boss) whatever level the boss is met at, so the tier is "
            "the level from the wiki: %s. Level %d gives tier %d (%s); the fight's Health is the act curve's at the floor."
            % (level_text, level, tier, TIERS))


POOL_DECK = ("The wiki lists %d possible cards with no counts for a deck of %s cards (the boss draws its deck from them). A combat "
             "tier fights with 30: each possible card once, then a second copy of each non-Legendary card in table order%s.")


def boss(n, slug, name, page, tier, archetype, level, level_text, label, flags=(), cards=(), forms=None, fillers=(),
         replacements=None, adaptations=()):
    entries = []
    for e in LISTS[n]:
        row = OD([("name", e[0]), ("count", e[1])])
        if len(e) > 2: row["id"] = e[2]
        entries.append(row)
    r = OD()
    r["id"] = "dala_%d_%s" % (n, slug)
    r["name"] = name
    r["provenance"] = "DALA"
    r["family"] = "15"
    r["hero"] = "DALA_BOSS_%dh" % n
    r["power"] = "DALA_BOSS_%dp" % n
    r["tier"] = str(tier)
    r["archetype"] = archetype
    r["level"] = OD([("source", "wiki-level-table"), ("original", level)])
    if flags: r["flags"] = list(flags)
    if cards: r["cards"] = list(cards)
    if forms: r["forms"] = forms
    r["source"] = OD([("url", WIKI + page), ("read", READ)])
    r["lists"] = [OD([("label", label), ("cards", entries)])]
    r["deck"] = OD([("list", label)])
    if replacements: r["replacements"] = OD(replacements)
    if fillers: r["fillers"] = list(fillers)
    r["adaptations"] = [OD([("kind", "schedule"), ("note", tier_note(level_text, level, tier))])] + [OD([("kind", k), ("note", t)]) for k, t in adaptations]
    return r


AUTO = ("The wiki marks %s as auto-cast, so it is cast by rule, not by a choice of the boss's AI (DungeonRunEncounters.AutoCast, "
        "family 09's rule): at the start of the boss's turn, when it can pay the Cost and %s, the Cost is paid and the power "
        "resolves. The engine never offers it as an option (it is registered with the passive powers).")

ROWS = [
    boss(16, "wendy", "Alchemist Wendy", "Alchemist_Wendy", 1, "tempo", 1, "a table of possible cards for decks of 10 and 15 cards, the sizes of levels 1 and 2",
         "Normal and Heroic (possible cards, levels 1-2)", flags=["passive", "hiddenZone"],
         fillers=["Kabal Lackey", "Kabal Songstealer", "Arcane Intellect", "Kabal Courier"],
         adaptations=[
             ("deck", POOL_DECK % (13, "10 or 15", ", then the curated Kabal and Mage fillers named in the row")),
             ("power", AUTO % ("Equivalent Exchange (1)", "both hands hold a card")
              + " One random card of each hand changes sides (the game RNG): both leave their hands first, so a full hand never burns one, and each "
                "keeps its enchantments and Cost. Each side then knows the card it received (its own hand) and the card it gave (a hand-transfer fact, as "
                "Gloves of Mugging's); nothing else of either hand is shown."),
         ]),
    boss(17, "tala", "Tala Stonerage", "Tala_Stonerage", 3, "midrange", 6, "\"These decks correspond only to level 6 in Chapter 5\" (levels 4-5 drop five random cards)",
         "Normal (level 6)", flags=["passive"],
         adaptations=[("power", "Twin Paths: the boss's Choose One cards and Choose One hero powers have both effects combined, Fandral Staghelm's rule "
                                "(the controller aura CHOOSE_BOTH, as ULD_131p Ossirian Tear); the boss never picks an option.")]),
    boss(18, "disidra", "Disidra Stormglory", "Disidra_Stormglory_(boss)", 3, "midrange", 6, "\"These decks correspond only to level 6 in Chapter 5\" (levels 4-5 drop five random cards)",
         "Normal (level 6)", flags=["passive"],
         adaptations=[("power", AUTO % ("Totemic Summons (2)", "its board has room")
                       + " \"ANY random Totem\": one random scripted Totem minion of any class and set the run's generation filter allows for the boss, "
                         "the four basic Totems (Healing, Searing, Stoneclaw, Wrath of Air) included.")]),
    boss(19, "linzi", "Linzi Redgrin", "Linzi_Redgrin", 1, "aggro", 1, "a table of possible cards for decks of 10 and 15 cards, the sizes of levels 1 and 2",
         "Normal (possible cards, levels 1-2)",
         adaptations=[("deck", POOL_DECK % (18, "10 or 15", "")),
                      ("power", "Lil' Eviscerate (2) is aimed at any character; its Combo is active when the boss played a card earlier this turn "
                                "(the engine's Combo rule). A hero power: no Spell Damage.")]),
    boss(20, "vasno", "Vas'no", "Vas'no", 3, "midrange", 6, "a counted table of 30 cards, the deck size from level 6 (the Heist deck grows by five cards a level to 30; the page names no level)",
         "Normal (30 cards)", flags=["passive", "recast"],
         adaptations=[("power", "Stormswell: after the boss casts a spell with Overload from its hand, a copy is cast again on the same target "
                                "(RecastRules.Recast, Pirate's Mark's rule: a target that is gone loses only the targeted part; a Choose One copy keeps the "
                                "option picked). The copy is a spell cast by an effect, so its Overload is owed too (the engine's rule, as for Yogg-Saron's casts), and it "
                                "never casts again; a Secret or a Quest is not cast twice.")]),
    boss(22, "bae_chao", "Bookmaster Bae Chao", "Bookmaster_Bae_Chao", 2, "control", 3, "a table of possible cards for decks of 20 and 25 cards, the sizes of levels 3 and 4",
         "Normal (possible cards, levels 3-4)",
         adaptations=[("deck", POOL_DECK % (18, "20 or 25", "") + " Fel Reaver is a collectible card the engine does not script: it is "
                               "left out, so the other seventeen fill the deck."),
                      ("power", "Shhh! (3) silences every minion of both boards an effect can pick (the engine's Silence); not offered while no minion "
                                "is on a board.")]),
    boss(23, "sharky", "Sharky McFin", "Sharky_McFin", 2, "aggro", 4, "a table of possible cards for a deck of 25 cards, the size of levels 4 and 5 (Chapter 4)",
         "Normal (possible cards, levels 4-5)",
         adaptations=[("deck", POOL_DECK % (15, "25", "")),
                      ("power", "Sharkbite (2) destroys one random damaged minion of either board (the game RNG); not offered while no minion is "
                                "damaged. A run shrine picked goes dormant, as from any destroy effect.")]),
    boss(24, "arha", "Ranger Ar'ha", "Ranger_Ar'ha", 3, "tempo", 6, "\"These decks correspond only to levels 6-7\" (level 5 drops five random cards)",
         "Normal (levels 6-7)", flags=["doubleBattlecry"], cards=["DALA_BOSS_24e"],
         adaptations=[("power", "Ar'ha's Call (2) attaches its player enchantment (DALA_BOSS_24e, \"Triggers additional Battlecries\"): the boss's "
                                "next Battlecry resolves one more time (EXTRA_BATTLECRIES_ADDITIONAL, the shrine Tribute from the "
                                "Tides' rule, on top of any doubling) and the next card with a Battlecry of any type (a minion, a weapon such as her "
                                "Headhunter's Hatchet, a hero card) uses it up: the enchantment is then removed. Not offered while one is waiting.")]),
    boss(25, "jepetto", "Jepetto Joybuzz", "Jepetto_Joybuzz_(boss)", 4, "midrange", 8, "\"the final boss that can be encountered in Chapter 3\", level 8",
         "Normal (level 8)", flags=["passive", "bothPlayers", "hiddenZone"], cards=["DALA_BOSS_25e"],
         adaptations=[("schedule", "Jepetto is a chapter's final boss in the Heist; this mode has no major slot for Heist chapter finals (the Heist's "
                                   "major is its finale, family 18), so he is a tier 4 combat encounter."),
                      ("power", "Assembly: after either player draws a minion (mulligan replacements are not draws of the game), that player "
                                "summons a 1/1 copy of the drawn card (Dollmaster Dorian's rule, Toyification DALA_BOSS_25e). The copy is a "
                                "public summon, so the run player's drawn minion is revealed as Dorian's copy would reveal it; the boss's fair view "
                                "does not use the reveal (its view of that hand stays masked)."),
                      ("deck", "Hoard of Toys (DALA_BOSS_25t, \"Summon 1/1 copies of five random minions\"): five random scripted minions of any "
                               "class the run's generation filter allows, each summoned with Toyification (1/1).")]),
    boss(26, "fountain_golem", "Dalaran Fountain Golem", "Dalaran_Fountain_Golem", 3, "midrange", 6, "\"These decks correspond only to level 6 in Chapter 5\" (levels 4-5 drop five random cards)",
         "Normal (level 6)", flags=["passive"],
         adaptations=[("power", "Cold Water: after the first enemy minion each turn attacks the boss's hero, that minion is Frozen (the hero "
                                "power's TAG_SCRIPT_DATA_NUM_1 holds the turn of the last freeze).")]),
    boss(27, "rasil", "Rasil Fireborne", "Rasil_Fireborne", 3, "midrange", 6, "a counted table of 30 cards, the deck size from level 6 (Chapter 3; the page names no level)",
         "Normal (30 cards)",
         adaptations=[("power", "Masterpiece! (0) spends all the boss's remaining Mana and summons one random scripted minion of any class that costs "
                                "exactly that much, from the pool the run's generation filter allows for the boss; it needs a free board slot.")]),
    boss(28, "xurios", "Xur'ios", "Xur'ios", 3, "tempo", 6, "\"These decks correspond only to level 6 in Chapter 5\" (levels 4-5 drop five random cards)",
         "Normal (level 6)", flags=["passive"], cards=["DALA_BOSS_28e"],
         replacements={"Wee Spellstopper": "Water Elemental", "Glacial Mysteries": "Flamestrike"},
         adaptations=[("deck", "Two listed cards cannot be in a boss deck here: Wee Spellstopper is a collectible card the engine does not "
                               "script, and Glacial Mysteries puts one copy of each Secret of its owner's deck into play, a deck read the "
                               "table does not admit for a boss. Curated replacements of the same class and a similar Cost: Water Elemental "
                               "and Flamestrike (a big spell, for Scroll Savvy)."),
                      ("power", "Scroll Savvy: a spell drawn costs (0) until the end of that turn (Savvy DALA_BOSS_28e, the rule of the Tombs passive "
                                "Fleeting Knowledge). The text names no player, so it holds for both players' draws (the page's tip, kill his drawing "
                                "minions on your turn, is about his own draws and says nothing against it). Mulligan replacements are not draws of the game.")]),
    boss(29, "mama_diggs", "Mama Diggs", "Mama_Diggs", 2, "midrange", 3, "a table of possible cards for decks of 20 and 25 cards, the sizes of levels 3 and 4 (Chapter 4)",
         "Normal (possible cards, levels 3-4)",
         cards=["GILA_500p2t", "LOOTA_806", "LOOTA_812", "LOOTA_816", "LOOTA_819", "LOOTA_821", "LOOTA_823", "LOOTA_826", "LOOTA_827", "LOOTA_835",
                "LOOTA_836", "LOOTA_838", "LOOTA_839", "LOOTA_842b", "LOOTA_843", "LOOTA_847", "LOOT_998k"],
         adaptations=[("deck", POOL_DECK % (16, "20 or 25", "")),
                      ("power", "Excavate (1), \"Deal 2 damage to a friendly Elemental. Find out what's inside.\": the client data has no result "
                                "table; the wiki's boss page lists the possible excavations (\"Nothing; A Small Rock; Drawing 1-2 cards; Random shaman "
                                "cards; Copy of the damaged minion; Dungeon Run treasures\") with no odds. Here one of the six, each equally likely "
                                "(the game RNG): nothing; A Small Rock (GILA_500p2t) to her hand; one or two draws (equally likely); one random "
                                "scripted Shaman card from the pool the run's generation filter allows for the boss to her hand; a copy of the "
                                "damaged minion's card to her hand; one of the sixteen Dungeon Run treasures King Togwaggle's Magic Candle finds "
                                "(boss-admitted on catalog runs) to her hand. The 2 damage comes first; a full hand burns what is added.")]),
    boss(30, "rat_king", "The Rat King", "The_Rat_King_(boss)", 3, "aggro", 6, "a counted table of 30 cards, the deck size from level 6 (Chapter 4; the page names no level)",
         "Normal (30 cards)", flags=["forms"], cards=["DALA_BOSS_30t"],
         forms=[OD([("hero", "DALA_BOSS_30h"), ("power", "DALA_BOSS_30p"), ("at", OD([("script", True)]))])],
         adaptations=[("power", "A Tale of Kings (3) summons an Underbelly Rat (DALA_BOSS_30t, a 10/1); it needs a free board slot."),
                      ("power", "Underbelly Rat, \"When your hero dies, the eldest Underbelly Rat replaces your hero\": when the boss's hero dies "
                                "with an Underbelly Rat on his board (alive, not dying, not Silenced: the rule is the rat's own text), the eldest (the first "
                                "to enter play; a rat bounced and replayed is young again) leaves the board without dying "
                                "and a new The Rat King enters through the row's script form (EncounterRules.SwapForm: hero and power fresh, Armor "
                                "and weapon kept) with the rat's Health (its remaining Health of its maximum), as many times as he has rats. The "
                                "same holds for a run player who controls a rat: that hero stays and takes the rat's Health. In the Heist's Chapter 4 "
                                "every minion's Attack and Health were swapped (the chapter twist), so the rat fought as a 1/10 there; here it is "
                                "the printed 10/1 unless a rule swaps it.")]),
]


def main(out):
    data = OD()
    data["schemaVersion"] = 1
    data["family"] = "15"
    data["description"] = ("Dalaran Heist bosses B (phase 8 family 15): fourteen encounters of The Dalaran Heist (Alchemist Wendy, Tala "
                           "Stonerage, Disidra Stormglory, Linzi Redgrin, Vas'no, Bookmaster Bae Chao, Sharky McFin, Ranger Ar'ha, Jepetto "
                           "Joybuzz, Dalaran Fountain Golem, Rasil Fireborne, Xur'ios, Mama Diggs, The Rat King) and their two boss cards "
                           "(Hoard of Toys, Underbelly Rat). Boss deck lists are transcribed from hearthstone.wiki.gg boss pages (read "
                           + READ + "), by card name; tools/dungeon-run-catalog (step 'encounters') resolves the names against "
                           "facts/violet-hold-2026-09.json, checks every card and writes data/dungeon-run/encounters-v1.json. The Heist hero "
                           "cards print 30 Health (10 at level 1), so the tier is the boss's level from its wiki page: the page's own "
                           "statement where it has one (\"level 6 in Chapter 5\", \"levels 6-7\", the final boss of Chapter 3), else the deck "
                           "size of its table (the Heist boss deck grows with the level: 10 cards at level 1, 15 at 2, 20 at 3, 25 at 4-5, "
                           "30 from 6, as the pages' \"missing 5 random cards\" notes state); " + TIERS + ".")
    data["method"] = ("The wiki's raw wikitext (?action=raw) was read for each boss page and the Normal column of its one deck table "
                      "transcribed in table order, rowspan-aware, with the table's own counts; a \"Possible cards\" table has no counts "
                      "and lists each card once. The page's \"(card)\" disambiguation suffix is dropped from a name. Boss-only cards and "
                      "Murloc Tinyfin (no collectible engine card of that name) carry their id. Generated by "
                      "verify/tools/gen/p8f15/make_f15.py.")
    data["bosses"] = ROWS
    data["rejected"] = []
    text = json.dumps(data, indent=1, ensure_ascii=False) + "\n"
    with open(out, "w", encoding="utf-8", newline="\n") as f: f.write(text)
    print("wrote %s: %d rows" % (out, len(ROWS)))


main(sys.argv[1] if len(sys.argv) > 1 else os.path.join(REPO, "data", "dungeon-run", "reference", "encounters", "F15.json"))
