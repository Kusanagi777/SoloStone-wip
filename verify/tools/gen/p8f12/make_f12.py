# Phase 8 family 12 (Monster Hunt bosses C): writes data/dungeon-run/reference/encounters/F12.json, the hand transcription
# of the thirteen boss pages of hearthstone.wiki.gg (read 2026-11-14) with the rows' tiers, flags, fillers, replacements
# and adaptation notes. The encounters step of tools/dungeon-run-catalog (verify/bin/gen.sh) reads it.
# Usage: python3 -I verify/tools/gen/p8f12/make_f12.py [<repo>]      (LF, no BOM; deterministic)
import json, os, sys, collections
V = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))  # verify/
REPO = sys.argv[1] if len(sys.argv) > 1 else os.path.dirname(V)
READ = "2026-11-14"
WIKI = "https://hearthstone.wiki.gg/wiki/"
O = collections.OrderedDict

def cards(*rows):
    out = []
    for r in rows:
        name, count = r[0], r[1]
        e = O([("name", name), ("count", count)])
        if len(r) > 2: e["id"] = r[2]
        out.append(e)
    return out

TOUCH = ("Witchwood's Touch", 2, "GILA_BOSS_99t2")

def tier_of(printed):
    return "1" if printed <= 15 else "2" if printed <= 30 else "3" if printed <= 40 else "4"

def schedule_note(rounds, printed):
    return ("The wiki's Monster Hunt page lists the boss in round %s of the hunt's eight; a boss's Health there follows the round. "
            "Here the tier comes from the printed Health of its hero card (%d: tier %s), as family 10's Monster Hunt rows, and the "
            "fight's Health is the act curve's." % (rounds, printed, tier_of(printed)))

TOUCH_NOTE = ("Witchwood's Touch (2) (GILA_BOSS_99t2, \"Draw 2 cards. Gain 8 Armor.\") is Hagatha's boss card, scripted by "
              "family 23; until then the listed copies are Shield Block (\"Gain 5 Armor. Draw a card.\"), the nearest scripted "
              "card, and the encounters step restores them once it is scripted.")

def short_note(listed):
    return ("The wiki lists a deck of %d cards. A combat tier fights with 30: the listed cards, a second copy of each "
            "non-Legendary single in table order, then the curated fillers named in the row." % listed)

BOSSES = [
    # ---------------------------------------------------------------- passive powers
    O([("id", "gila_24_groddo"), ("name", "Groddo the Bogwarden"), ("n", "24"), ("printed", 30), ("rounds", "6-8"), ("archetype", "midrange"),
       ("flags", ["passive"]), ("cards", ["GILA_BOSS_24t"]), ("page", "Groddo_the_Bogwarden"),
       ("list", cards(TOUCH, ("Ultimate Infestation", 1), ("Feign Death", 1), ("Twilight's Call", 2), ("Journey Below", 1), ("Roll the Bones", 2),
                      ("Anub'arak", 1), ("Death's Bite", 1), ("Leper Gnome", 1), ("Undertaker", 2), ("Unstable Ghoul", 2), ("Flesheating Ghoul", 2),
                      ("Shallow Gravedigger", 2), ("Arfus", 1), ("Baron Rivendare", 1), ("Abomination", 2), ("Muck Hunter", 1), ("Rotten Applebaum", 1),
                      ("Skelemancer", 1), ("Sludge Belcher", 2), ("N'Zoth, the Corruptor", 1))),
       ("replacements", O([("Witchwood's Touch", "Shield Block")])),
       ("fillers", ["Haunted Creeper", "Loot Hoarder", "Harvest Golem", "Dark Cultist"]),
       ("notes", [("deck", TOUCH_NOTE),
                  ("power", "From the Swamp raises one Bloated Zombie (GILA_BOSS_24t, 1/3, \"Deathrattle: Deal 1 damage to all characters.\") "
                            "for the boss whenever a minion of the run player dies (a run shrine that dies and goes dormant counts as a death, as for "
                            "every death trigger); nothing with a full board.")])]),
    O([("id", "gila_30_gretta"), ("name", "Blood Witch Gretta"), ("n", "30"), ("printed", 30), ("rounds", "4-5"), ("archetype", "control"),
       ("flags", ["passive"]), ("page", "Blood_Witch_Gretta"),
       ("list", cards(("Arcane Artificer", 2), ("Arcane Missiles", 1), ("Mirror Image", 2), ("Arcanologist", 1), ("Sorcerer's Apprentice", 2),
                      ("Unstable Portal", 2), ("Arcane Intellect", 2), ("Explosive Runes", 1), ("Flamewaker", 2), ("Ice Barrier", 1), ("Ice Block", 1),
                      ("Mirror Entity", 1), ("Vaporize", 1), ("Molten Reflection", 2), ("Duskbat", 1), ("Mistress of Mixtures", 1),
                      ("Violet Illusionist", 2), ("Refreshment Vendor", 1), ("Rotten Applebaum", 2), ("Arcane Giant", 2))),
       ("fillers", ["Frostbolt", "Arcane Explosion", "Water Elemental", "Mana Wyrm"]),
       ("notes", [("power", "Blood Red Apple is the rule of Blood Pact (TRLA_113) and Elixir of Vile on the boss's side only: the boss's spells "
                            "cost Health instead of Mana (the SPELLS_COST_HEALTH controller aura); paying Health is not damage, and the run "
                            "player's spells cost Mana as before.")])]),
    O([("id", "gila_39_forlorn_lovers"), ("name", "Forlorn Lovers"), ("n", "39"), ("printed", 70), ("rounds", "6-8"), ("archetype", "control"),
       ("flags", ["passive"]), ("page", "Forlorn_Lovers"),
       ("list", cards(("Naturalize", 2), ("Grove Tender", 2), ("Nourish", 1), ("Preparation", 2), ("Shadowstep", 2), ("Cheat Death", 1), ("Evasion", 1),
                      ("Gang Up", 1), ("Elven Minstrel", 2), ("Fal'dorei Strider", 2), ("Sprint", 1), ("Vilespine Slayer", 1), ("Vanish", 1),
                      ("Corrupting Mist", 1), ("Gnomeferatu", 1), ("Hellfire", 2), ("Youthful Brewmaster", 1), ("Brann Bronzebeard", 1),
                      ("Coldlight Oracle", 3), ("Dancing Swords", 1), ("Abomination", 1))),
       ("fillers", ["SI:7 Agent", "Earthen Ring Farseer", "Sap", "Fan of Knives"]),
       ("notes", [("schedule-health", "The hero card prints 70 Health, above every other Monster Hunt boss of the tiers; it is tier 4 like the 50 and "
                                      "60 Health rows and fights at the act curve's Health."),
                  ("power", "Together Forever shuffles a fresh copy of each friendly minion that dies into the boss's deck (a run shrine never "
                            "counts: its dormant form stays on the board); after each copy the boss's own-deck ledger is told the deck's exact "
                            "list again (EncounterRules.DeckRewritten), so the boss's search samples the copies it shuffled.")])]),
    O([("id", "gila_45_sthara"), ("name", "Cultist S'thara"), ("n", "45"), ("printed", 40), ("rounds", "2-3"), ("archetype", "midrange"),
       ("flags", ["passive", "bothPlayers"]), ("page", "Cultist_S%27thara"),
       ("list", cards(("Acolyte of Agony", 2), ("Dark Cultist", 2), ("Bladed Cultist", 2), ("Forbidden Ritual", 2), ("Dark Possession", 1),
                      ("Curse of Weakness", 1), ("Shadow Bolt", 1), ("Unwilling Sacrifice", 1), ("Summoning Portal", 1), ("Bane of Doom", 1),
                      ("Floating Watcher", 1), ("Nat, the Darkfisher", 1), ("Coldlight Oracle", 2), ("Dancing Swords", 2))),
       ("fillers", ["Voidwalker", "Flame Imp", "Dread Infernal", "Shadow Word: Pain", "Hellfire"]),
       ("notes", [("power", "Poisoned Drink deals 1 damage to a player's hero for every card that player draws, either player, from the first "
                            "turn on (the opening hand and the mulligan's replacement cards are dealt, not drawn); a card burned by a full hand "
                            "was drawn and still costs 1 Health, an empty deck's Fatigue draws no card and adds nothing.")])]),
    O([("id", "gila_46_vitus"), ("name", "Vitus the Exiled"), ("n", "46"), ("printed", 40), ("rounds", "6-7"), ("archetype", "aggro"),
       ("flags", ["passive"]), ("cards", ["GILA_BOSS_46e"]), ("page", "Vitus_the_Exiled"),
       ("list", cards(("Living Roots", 2), ("Savage Roar", 2), ("Branching Paths", 2), ("Dark Wispers", 2), ("Meanstreet Marshal", 3), ("Cold Blood", 2),
                      ("Bloodlust", 2), ("Crazed Alchemist", 1), ("Echoing Ooze", 2), ("Acolyte of Pain", 2), ("Imp Master", 1), ("Raging Worgen", 2),
                      ("Kooky Chemist", 1), ("Mogu'shan Warden", 1), ("Saronite Chain Gang", 2), ("Doppelgangster", 2), ("Onyxia", 1))),
       ("fillers", ["Bloodfen Raptor", "Chillwind Yeti", "Dire Wolf Alpha", "Shattered Sun Cleric"]),
       ("notes", [])]),
    O([("id", "gila_47_shudderwock"), ("name", "Shudderwock"), ("n", "47"), ("printed", 50), ("rounds", "4-5"), ("archetype", "midrange"),
       ("flags", ["passive", "bothPlayers", "doubleBattlecry"]), ("page", "Shudderwock_(boss)"),
       ("list", cards(("Echo of Medivh", 1), ("Shadowstep", 2), ("Vanish", 2), ("Kobold Librarian", 1), ("Gnomeferatu", 1), ("Abyssal Enforcer", 1),
                      ("Iron Juggernaut", 2), ("Gravelsnout Knight", 1), ("Voodoo Doctor", 1), ("Baleful Banker", 2), ("Echoing Ooze", 1), ("Mad Bomber", 1),
                      ("Coldlight Oracle", 1), ("Grimestreet Smuggler", 1), ("Kobold Apprentice", 1), ("Zola the Gorgon", 1), ("Hungry Dragon", 1),
                      ("Mad Hatter", 2), ("Saronite Chain Gang", 2), ("Antique Healbot", 1), ("Elise the Trailblazer", 1), ("Madder Bomber", 1),
                      ("Don Han'Cho", 1), ("Primordial Drake", 1), ("Tortollan Primalist", 1))),
       ("fillers", ["Earthen Ring Farseer", "Shattered Sun Cleric", "Defender of Argus", "Azure Drake"]),
       ("notes", [("deck", "The wiki's table sums to 31 cards; the combat deck keeps the first 30 in table order (the last row, Tortollan "
                           "Primalist, is left out)."),
                  ("power", "Frumiousity is the rule of Battle Totem (LOOTA_BOSS_17p) for both players: all Battlecries trigger twice "
                            "(one EXTRA_BATTLECRIES_BASE controller aura on each player, so it never stacks with a doubling a side already has).")])]),
    O([("id", "gila_50_whisperer"), ("name", "The Whisperer"), ("n", "50"), ("printed", 25), ("rounds", "6-7"), ("archetype", "tempo"),
       ("flags", ["passive", "bothPlayers", "recast"]), ("page", "The_Whisperer"),
       ("list", cards(("Mark of the Lotus", 1), ("Wrath", 1), ("Hunting Mastiff", 1), ("Animal Companion", 1), ("Deadly Shot", 1), ("Unstable Portal", 1),
                      ("Divine Strength", 1), ("Sound the Bells!", 1), ("Spirit Lash", 1), ("Squashling", 1), ("Backstab", 1), ("Evolve", 2), ("Devolve", 1),
                      ("Curse of Rafaam", 1), ("Whirlwind", 1), ("Arcane Anomaly", 1), ("Echoing Ooze", 1), ("Mana Addict", 2), ("Stonesplinter Trogg", 2),
                      ("Phantom Militia", 1), ("Saboteur", 1), ("Walnut Sprite", 1), ("Burly Rockjaw Trogg", 2), ("Azure Drake", 1), ("Loatheb", 1),
                      ("Troggzor the Earthinator", 1))),
       ("replacements", O([("Wrath", "Lesser Jasper Spellstone")])),
       ("fillers", ["Arcane Explosion", "Frostbolt", "Holy Smite", "Mortal Coil"]),
       ("notes", [("deck", "Wrath (EX1_154, Choose One) is a collectible card this engine does not script (its Core reprint is "
                           "scripted, but the encounters step reads the client's original printing): Lesser Jasper Spellstone takes its "
                           "slot, as in family 08's Druid decks, and the step restores it once Wrath is scripted."),
                  ("power", "Echoes of the Witchwood gives every spell in either player's hand Echo (a hand aura on both players): a spell "
                            "played from hand returns a copy to its owner's hand that is discarded when the turn ends. The Echo rule of the "
                            "client applies to those copies: an Echo copy never costs less than (1) (EncounterRules.EchoCopyFloor), so a "
                            "0-Cost spell such as The Coin or Backstab is played once for free and its copies cost (1). A player who has received "
                            "ten Echo copies in one turn has no Echo on its spells until the turn ends (EncounterRules.EchoCopiesPerTurn): the "
                            "client has no such number, it stands in for the client's turn timer, which is what ends a turn of endlessly "
                            "repeated copies (The Coin's copy costs (1) and returns that Mana).")])]),
    O([("id", "gila_63_sephira"), ("name", "Sephira Dusktalon"), ("n", "63"), ("printed", 40), ("rounds", "4-5"), ("archetype", "tempo"),
       ("flags", ["passive"]), ("page", "Sephira_Dusktalon"),
       ("list", cards(TOUCH[:1] + (1, TOUCH[2]), ("Claw", 2), ("Enchanted Raven", 2), ("Mark of the Lotus", 2), ("Mark of the Wild", 2), ("Power of the Wild", 2),
                      ("Wrath", 2), ("Wild Growth", 2), ("Swipe", 1), ("Starfall", 1), ("Nourish", 1), ("Ultimate Infestation", 1), ("Windfury", 2),
                      ("Bloodlust", 2), ("Dragon Egg", 1), ("Runic Egg", 1), ("Swamp Dragon Egg", 1), ("Nerubian Egg", 1), ("Devilsaur Egg", 1),
                      ("Eggnapper", 1), ("Windfury Harpy", 1))),
       ("replacements", O([("Witchwood's Touch", "Shield Block"), ("Wrath", "Lesser Jasper Spellstone"), ("Power of the Wild", "Mark of the Lotus")])),
       ("fillers", ["Savage Roar", "Druid of the Claw", "Keeper of the Grove", "Living Roots"]),
       ("notes", [("deck", TOUCH_NOTE),
                  ("deck", "Wrath (EX1_154) and Power of the Wild (EX1_160), both Choose One, are collectible cards this engine does "
                           "not script (their Core reprints are scripted, but the encounters step reads the client's original "
                           "printing): Lesser Jasper Spellstone and Mark of the Lotus take their slots, as in family 08's and family "
                           "10's Druid decks, and the step restores them once they are scripted."),
                  ("power", "Tangled Wrath draws the boss one random spell of its own deck whenever the run player casts a spell (an ordinary "
                            "draw of that card; nothing when the deck holds no spell; a full hand burns it). The printed Cost (2) of the passive "
                            "power is never paid.")])]),
    O([("id", "gila_65_gobbles"), ("name", "Gobbles"), ("n", "65"), ("printed", 10), ("rounds", "1"), ("archetype", "aggro"),
       ("flags", ["passive"]), ("page", "Gobbles"),
       ("list", cards(("Alleycat", 1), ("Fiery Bat", 1), ("Ball of Spiders", 1), ("Infest", 1), ("Rat Pack", 1), ("Tundra Rhino", 1), ("Stonetusk Boar", 1),
                      ("Bloodfen Raptor", 1), ("Duskboar", 1), ("Huge Toad", 1), ("Tinkmaster Overspark", 1), ("Night Prowler", 1))),
       ("fillers", ["Kindly Grandmother", "Scavenging Hyena", "Jungle Panther", "River Crocolisk", "Ironfur Grizzly", "Starving Buzzard",
                    "Dire Wolf Alpha", "Timber Wolf"]),
       ("notes", [("power", "Starving draws the boss a card whenever one of its Beasts dies (a minion of every type counts).")])]),
    O([("id", "gila_67_hannigul"), ("name", "Ratcatcher Hannigul"), ("n", "67"), ("printed", 40), ("rounds", "4-5"), ("archetype", "aggro"),
       ("flags", ["passive", "bothPlayers", "position"]), ("cards", ["GILA_BOSS_67e"]), ("page", "Ratcatcher_Hannigul"),
       ("list", cards(TOUCH, ("Rat Pack", 2), ("Infested Wolf", 2), ("Possessed Villager", 2), ("Power Overwhelming", 2), ("Spreading Madness", 2),
                      ("Void Terror", 2), ("Swamp Leech", 2), ("Haunted Creeper", 2), ("Flesheating Ghoul", 2), ("Sewer Crawler", 2), ("Mad Hatter", 2),
                      ("Bloodworm", 2), ("Moat Lurker", 2), ("Violet Wurm", 1), ("Onyxia", 1))),
       ("replacements", O([("Witchwood's Touch", "Shield Block")])),
       ("fillers", ["Flame Imp", "Voidwalker", "Dire Wolf Alpha", "Harvest Golem"]),
       ("notes", [("deck", TOUCH_NOTE),
                  ("power", "Cannibalism: whenever a minion of either board dies, the minions that stood next to it in that board when it died "
                            "gain +1 Attack (Mmm, so tasty!, GILA_BOSS_67e). Minions that die together are each other's neighbours only while "
                            "both stood; a neighbour that dies in the same death pass gains nothing, and a dormant or untouchable neighbour is "
                            "skipped.")])]),
    # ---------------------------------------------------------------- clickable powers
    O([("id", "gila_64_winslow"), ("name", "Winslow Tobtock"), ("n", "64"), ("printed", 40), ("rounds", "4-5"), ("archetype", "control"),
       ("flags", ["bothPlayers", "hiddenZone"]), ("page", "Winslow_Tobtock"),
       ("list", cards(("Curio Collector", 3), ("Mind Vision", 2), ("Shadow Word: Death", 1), ("Shadow Word: Pain", 2), ("Shadowfiend", 3), ("Vivid Nightmare", 2),
                      ("Shadow Word: Horror", 1), ("Dragonfire Potion", 1), ("Lightbomb", 1), ("Archbishop Benedictus", 1), ("Mind Control", 1),
                      ("Beneath the Grounds", 2), ("Fal'dorei Strider", 2), ("Pompous Thespian", 2), ("Daring Reporter", 1), ("Mind Control Tech", 2),
                      ("Gadgetzan Auctioneer", 1), ("Sleepy Dragon", 1), ("Ysera", 1))),
       ("fillers", ["Holy Nova", "Shadow Word: Pain", "Injured Blademaster", "Chillwind Yeti"]),
       ("notes", [("power", "Hypnotize (2): the boss first, then the run player, each shuffles its whole hand into its deck and draws as many "
                            "cards (the ordinary draw rules: a short deck fatigues). Both hands and both decks change while hidden: a belief "
                            "reset of hand and deck is recorded for each player (RunHelpers.BeliefReset), so what either side learned about "
                            "those cards before is dropped, and the boss's own-deck ledger is told its deck's exact list "
                            "(EncounterRules.DeckRewritten). Offered while either hand holds a card (hand sizes are public).")])]),
    O([("id", "gila_66_mute"), ("name", "The Mute"), ("n", "66"), ("printed", 25), ("rounds", "2-3"), ("archetype", "control"),
       ("flags", []), ("page", "The_Mute"),
       ("list", cards(("Circle of Healing", 2), ("Silence", 2), ("Northshire Cleric", 2), ("Power Word: Shield", 2), ("Purify", 2), ("Mass Dispel", 1),
                      ("Ancient Watcher", 2), ("Acolyte of Pain", 2), ("Humongous Razorleaf", 2), ("Eerie Statue", 1), ("Spellbreaker", 1), ("Wailing Soul", 1))),
       ("fillers", ["Holy Smite", "Shadow Word: Pain", "Injured Blademaster", "Shieldbearer", "Lightwell", "Holy Nova"]),
       ("notes", [("power", "... (1) silences any minion (the engine's Silence: a silenced run shrine loses its rule, as from any Silence).")])]),
    O([("id", "gila_68_rancel"), ("name", "Plaguemaster Rancel"), ("n", "68"), ("printed", 40), ("rounds", "4-5"), ("archetype", "midrange"),
       ("flags", []), ("cards", ["GILA_BOSS_68e"]), ("page", "Plaguemaster_Rancel"),
       ("list", cards(("Infected Quillflinger", 3, "GILA_BOSS_68t"), ("Spreading Plague", 2), ("Rat Trap", 1), ("Rat Pack", 2), ("Plague Scientist", 2),
                      ("Corruption", 1), ("Corrupting Mist", 2), ("Curse of Weakness", 1), ("Twisting Nether", 1), ("DOOM!", 1), ("Swamp Leech", 2),
                      ("Dirty Rat", 2), ("Sewer Crawler", 2), ("Lifedrinker", 2), ("Bloodworm", 2), ("Witchwood Grizzly", 2), ("Hungry Ettin", 2))),
       ("fillers", ["Hellfire", "Voidwalker", "Dread Infernal", "Chillwind Yeti"]),
       ("notes", [("power", "Poison Flask (2) deals 2 damage to any minion (a hero power: no Spell Damage) and, if the minion survives, gives it "
                            "Poisonous (Dripping Poison, GILA_BOSS_68e)."),
                  ("deck", "Infected Quillflinger (GILA_BOSS_68t, 3/7, \"Whenever this minion takes damage, deal 1 damage to a random enemy "
                           "minion.\") is the boss's own card, listed three times by the wiki.")])]),
]

def row(b):
    n = b["n"]
    r = O([("id", b["id"]), ("name", b["name"]), ("provenance", "GIL"), ("family", "12"), ("hero", "GILA_BOSS_%sh" % n), ("power", "GILA_BOSS_%sp" % n),
           ("tier", tier_of(b["printed"])), ("archetype", b["archetype"]), ("level", O([("source", "printed-health"), ("original", b["printed"])]))])
    if b["flags"]: r["flags"] = list(b["flags"])
    if b.get("cards"): r["cards"] = list(b["cards"])
    r["source"] = O([("url", WIKI + b["page"]), ("read", READ)])
    r["lists"] = [O([("label", "Deck"), ("cards", b["list"])])]
    r["deck"] = O([("list", "Deck")])
    if b.get("replacements"): r["replacements"] = b["replacements"]
    if b.get("fillers"): r["fillers"] = list(b["fillers"])
    listed = sum(c["count"] for c in b["list"])
    notes = [O([("kind", "schedule"), ("note", schedule_note(b["rounds"], b["printed"]))])]
    if listed < 30: notes.append(O([("kind", "deck"), ("note", short_note(listed))]))
    for kind, note in b["notes"]:
        notes.append(O([("kind", "schedule" if kind == "schedule-health" else kind), ("note", note)]))
    r["adaptations"] = notes
    return r

doc = O([
    ("schemaVersion", 1), ("family", "12"),
    ("description", "Monster Hunt bosses C (phase 8 family 12): thirteen encounters of The Witchwood's Monster Hunt, ten of them with a "
                    "passive hero power (Groddo the Bogwarden, Blood Witch Gretta, Forlorn Lovers, Cultist S'thara, Vitus the Exiled, "
                    "Shudderwock, The Whisperer, Sephira Dusktalon, Gobbles, Ratcatcher Hannigul) and three with a clickable one (Winslow "
                    "Tobtock, The Mute, Plaguemaster Rancel), and their two boss-only cards (Bloated Zombie, Infected Quillflinger). Boss "
                    "deck lists are transcribed from hearthstone.wiki.gg boss pages (read " + READ + "), by card name; tools/dungeon-run-catalog "
                    "(step 'encounters') resolves the names against facts/violet-hold-2026-09.json, checks every card and writes "
                    "data/dungeon-run/encounters-v1.json. The tier is the printed Health of the hero card (10 and 15: tier 1; 25 and 30: "
                    "tier 2; 35 and 40: tier 3; 50 and more: tier 4)."),
    ("method", "Each boss page was read as wikitext (action=raw) and its one deck table transcribed in table order with the table's own "
               "counts by verify/tools/gen/p8f12/make_f12.py; a second, independent parse of the same pages gave the same lists and "
               "totals (30 for Groddo, Gretta, Forlorn Lovers, Vitus, The Whisperer, Sephira, Winslow, Hannigul and Rancel; 31 for "
               "Shudderwock; 20 for S'thara and The Mute; 12 for Gobbles). Boss-only cards carry their id; the wiki's names "
               "\"Witchwood's Touch (2)\", \"Windfury (card)\" and \"Silence (card)\" are written as the card names. The boss pages state "
               "no rules beyond the card texts: what a text leaves open is decided in the row's adaptation notes. The round of each boss "
               "is from " + WIKI + "Monster_Hunt (read " + READ + ")."),
    ("bosses", [row(b) for b in sorted(BOSSES, key=lambda b: int(b["n"]))]),
    ("rejected", []),
])
out = os.path.join(REPO, "data", "dungeon-run", "reference", "encounters", "F12.json")
text = json.dumps(doc, indent=1, ensure_ascii=False) + "\n"
open(out, "w", encoding="utf-8", newline="\n").write(text)
print("wrote %s: %d rows" % (out, len(doc["bosses"])))
