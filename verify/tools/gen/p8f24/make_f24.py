# Phase 8 family 24: writes data/dungeon-run/reference/encounters/F24.json, the hand transcription of the family's encounters
# (the Plague Lords Xatma, K'zrath and Vesh beside family 01's Icarax, and Tekahn, the Tombs finale). The boss deck lists are the
# Normal tables of each boss's hearthstone.wiki.gg page (read 2026-11-14 with ?action=raw; rowspan-aware, table order; the
# "Extra cards in 33-deck" rows are left out as family 01 left them out for Icarax). Tekahn's relay decks are the client's twelve
# finale decks (data/dungeon-run/reference/adventures.json, adventure ULDA, otherPrefixedDecks ULDA_<Explorer>_Tekanh_Deck_1..3),
# by card id. Usage: python3 -I verify/tools/gen/p8f24/make_f24.py [<repo>]   (LF, no BOM; deterministic)
import json, os, sys, collections
O = collections.OrderedDict
HERE = os.path.dirname(os.path.abspath(__file__))
REPO = sys.argv[1] if len(sys.argv) > 1 else os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(HERE))))
READ = "2026-11-14"
WIKI = "https://hearthstone.wiki.gg/wiki/"

def cards(pairs):
    out = []
    for p in pairs:
        if len(p) == 3: out.append(O([("name", p[0]), ("id", p[2]), ("count", p[1])]))
        else: out.append(O([("name", p[0]), ("count", p[1])]))
    return out

def lst(label, pairs): return O([("label", label), ("cards", cards(pairs))])

# ---------------------------------------------------------------- Xatma (Chapter 3 final boss)
XATMA = [
    ("Normal phase 1", [("Webspinner", 2), ("Corpse Widow", 2), ("Dead Ringer", 2), ("Grandmummy", 2), ("Shadow Word: Death", 2), ("Twilight's Call", 2),
        ("Shifting Shade", 1), ("Plague of Death", 1), ("Shadow of Death", 2), ("Undertaker", 2), ("Kobold Sandtrooper", 1), ("Deathlord", 2),
        ("Infested Goblin", 2), ("Polluted Hoarder", 2), ("Feugen", 1), ("Stalagg", 1), ("Sylvanas Windrunner", 1), ("The Beast", 1),
        ("Hakkar, the Soulflayer", 1)]),
    ("Normal phase 2", [("Dead Ringer", 2), ("Shadow Visions", 2), ("Dark Cultist", 2), ("Convincing Infiltrator", 2), ("Obsidian Statue", 2),
        ("Plague of Death", 2), ("Shadow of Death", 2), ("Bloodmage Thalnos", 1), ("Cursed Disciple", 2), ("Abomination", 2), ("Feugen", 1),
        ("Leeroy Jenkins", 1), ("Sludge Belcher", 2), ("Stalagg", 1), ("Sylvanas Windrunner", 1), ("Da Undatakah", 1), ("Octosari", 1),
        ("Sneed's Old Shredder", 2), ("The Lich King", 1), ("N'Zoth, the Corruptor", 1)]),
    ("Normal phase 3", [("Embalming Ritual", 2), ("Psychopomp", 2), ("Entomb", 2), ("Catrina Muerte", 1), ("Plague of Death", 2), ("Shadow of Death", 2),
        ("Murmy", 2), ("Bloodmage Thalnos", 1), ("Acolyte of Pain", 2), ("Clockwork Automaton", 2), ("Feugen", 1), ("Mortuary Machine", 2),
        ("Stalagg", 1), ("Wasteland Assassin", 2), ("Sylvanas Windrunner", 1), ("Dr. Boom", 1), ("Wrapped Golem", 2), ("Violet Wurm", 1),
        ("N'Zoth, the Corruptor", 1)]),
]
# ---------------------------------------------------------------- K'zrath (Chapter 2 final boss)
KZRATH = [
    ("Normal phase 1", [("Shadow Madness", 2), ("Surrender to Madness", 1), ("Mass Hysteria", 2), ("Conceal", 2), ("Plague of Madness", 2),
        ("Clever Disguise", 2), ("Blightnozzle Crawler", 2), ("Blade of C'Thun", 2), ("Spreading Madness", 2), ("Runic Egg", 2), ("Dirty Rat", 2),
        ("Serpent Egg", 2), ("Twilight Geomancer", 1), ("Twilight Elder", 2), ("Ticking Abomination", 1), ("Crazed Worshipper", 2),
        ("Sylvanas Windrunner", 1)]),
    ("Normal phase 2", [("Dark Cultist", 2), ("Kabal Talonpriest", 2), ("Surrender to Madness", 1), ("Mass Hysteria", 1), ("Vol'jin", 1),
        ("Psychic Scream", 2), ("Zerek's Cloning Gallery", 1), ("Plague of Madness", 2), ("Thistle Tea", 1), ("Blade of C'Thun", 2),
        ("Spreading Madness", 2), ("Beckoner of Evil", 2), ("Disciple of C'Thun", 2), ("Twilight Elder", 2), ("Cult Master", 2),
        ("Twilight Drake", 2), ("Twin Emperor Vek'lor", 1), ("Doomcaller", 1), ("C'Thun", 1)]),
    ("Normal phase 3", [("Mass Hysteria", 2), ("Psychic Scream", 1), ("Plague of Madness", 2), ("Fan of Knives", 2), ("Blade of C'Thun", 2),
        ("Tentacle of N'Zoth", 2), ("Beckoner of Evil", 2), ("Unstable Ghoul", 2), ("Disciple of C'Thun", 2), ("Twilight Elder", 2),
        ("C'Thun's Chosen", 2), ("Twin Emperor Vek'lor", 1), ("Doomcaller", 2), ("Splitting Festeroot", 2), ("C'Thun", 1),
        ("N'Zoth, the Corruptor", 1), ("Y'Shaarj, Rage Unbound", 1), ("Yogg-Saron, Hope's End", 1)]),
]
# ---------------------------------------------------------------- Vesh (Chapter 1 final boss)
MRGL_N, MRGL_H = ("Mrgl Mrgl Nyah Nyah", "LOEA10_5"), ("Mrgl Mrgl Nyah Nyah", "LOEA10_5H")
VESH = [
    ("Normal phase 1", [(MRGL_N[0], 1, MRGL_N[1]), ("Tip the Scales", 1), ("Anyfin Can Happen", 1), ("Primalfin Totem", 2), ("Underbelly Angler", 2),
        ("Plague of Murlocs", 2), ("Call in the Finishers", 2), ("Scargil", 1), ("Earthquake", 1), ("Neptulon", 1), ("Seadevil Stinger", 1),
        ("Fool's Bane", 2), ("Murmy", 2), ("Blowgill Sniper", 1), ("Fishflinger", 2), ("Murloc Warleader", 2), ("Primalfin Lookout", 2),
        ("Old Murk-Eye", 1), ("Finja, the Flying Star", 1), ("Corrupted Seer", 2)]),
    ("Normal phase 2", [("Demonic Presence", 2, "KARA_13_12H"), (MRGL_H[0], 2, MRGL_H[1]), ("Megafin", 3, "UNG_942t"), ("Murloc Knight", 2),
        ("Anyfin Can Happen", 2), ("Crushing Hand", 2), ("Primalfin Totem", 1), ("Plague of Murlocs", 2), ("Scargil", 1), ("Everyfin is Awesome", 1),
        ("Fishflinger", 1), ("Coldlight Oracle", 2), ("Murloc Tastyfin", 2), ("Old Murk-Eye", 2), ("Finja, the Flying Star", 1),
        ("Mukla's Champion", 2), ("Corrupted Seer", 2)]),
    ("Normal phase 3", [(MRGL_H[0], 2, MRGL_H[1]), ("Megafin", 2, "UNG_942t"), ("Giantfin", 2, "LOEA16_24H"), ("Tip the Scales", 2),
        ("Anyfin Can Happen", 2), ("Underbelly Angler", 2), ("Plague of Murlocs", 2), ("Scargil", 2), ("The Mistcaller", 1), ("Everyfin is Awesome", 2),
        ("Neptulon", 1), ("Murmy", 2), ("Fishflinger", 2), ("Coldlight Oracle", 2), ("Primalfin Lookout", 1), ("Murloc Tastyfin", 1),
        ("Old Murk-Eye", 1), ("Finja, the Flying Star", 1)]),
]
# ---------------------------------------------------------------- Tekahn (the Tombs finale)
TEKAHN = [
    ("Normal phase 1", [("Rakanishu", 1, "ULDA_033"), ("Sanctum Golem", 8, "ULDA_031"), ("EVIL Conscripter", 2), ("EVIL Miscreant", 2),
        ("Heistbaron Togwaggle", 1), ("Sludge Slurper", 2), ("EVIL Totem", 2), ("Plague of Flames", 2), ("Sinister Deal", 2), ("The Soularium", 1),
        ("EVIL Genius", 2), ("EVIL Recruiter", 2), ("Eager Underling", 1), ("Shadowflame", 1), ("Bane of Doom", 2), ("Glinda Crowskin", 1),
        ("Fel Lord Betrug", 1), ("EVIL Cable Rat", 2), ("Nerubian Egg", 2), ("Cult Master", 2), ("Arch-Thief Rafaam", 1)]),
    ("Normal phase 2", [("Plague of Flames", 2), ("Corrupting Mist", 2), ("Defile", 2), ("Ratcatcher", 2), ("Lord Godfrey", 1), ("Twisting Nether", 2),
        ("Serpent Egg", 2), ("Sunfury Protector", 2), ("Devilsaur Egg", 2), ("Baron Rivendare", 1), ("Defender of Argus", 2), ("Spiritsinger Umbra", 1),
        ("Weaponized Piñata", 1), ("Feugen", 1), ("Stalagg", 1), ("Tomb Lurker", 2), ("Bone Drake", 2), ("Piloted Sky Golem", 2),
        ("Sylvanas Windrunner", 1), ("The Skeleton Knight", 1), ("Dr. Boom", 1), ("Kel'Thuzad", 1), ("Octosari", 1), ("Sneed's Old Shredder", 1),
        ("Splitting Festeroot", 2), ("Colossus of the Moon", 1), ("N'Zoth, the Corruptor", 1)]),
    ("Normal phase 3", [("The Final Plague?!", 1, "ULDA_BOSS_67t"), ("Queen of Pain", 2), ("Duskbat", 2), ("Nethersoul Buster", 2), ("Diseased Vulture", 2),
        ("Deathweb Spider", 2), ("Floating Watcher", 2), ("Siphon Soul", 2), ("Demonbolt", 2), ("DOOM!", 1), ("Violet Illusionist", 2),
        ("Lifedrinker", 2), ("Bloodworm", 2), ("Rotten Applebaum", 2), ("Khartut Defender", 2), ("Countess Ashmore", 1), ("Chromaggus", 1),
        ("Ragnaros the Firelord", 1), ("The Lich King", 1), ("Anubisath Warbringer", 2), ("Hakkar, the Soulflayer", 1),
        ("Yogg-Saron, Hope's End", 1), ("Arcane Giant", 2), ("Molten Giant", 2)]),
]

def lord(rid, name, n, page, lists, powers, archetype, draw, fillers, extra_adapt, extra_cards):
    hero = "ULDA_BOSS_%sh" % n
    forms = []
    for k, (h, p, hp) in enumerate([(hero + "2", powers[1], 200), (hero + "3", powers[2], 100)]):
        forms.append(O([("hero", h), ("power", p), ("health", hp), ("at", O([("healthAtMost", hp)])), ("keepExcess", True),
            ("deck", O([("list", "Normal phase %d" % (k + 2))])), ("draw", draw)]))
    adapt = [
        O([("kind", "health"), ("note", "The three phases are forms of one row with one falling Health total (300; phase 2 at 200 or less, "
            "phase 3 at 100 or less, the remaining Health kept); a lost fight's damage is banked per encounter at profile scope "
            "(DungeonRunProfile.BossDamage, as family 01's Icarax), because a lost fight ends the run; ULDA_115 casts bank 10 more "
            "per cast in the run's other fights (DungeonRunState.PlagueLordDamage).")]),
        O([("kind", "deck"), ("note", "A phase change replaces the deck with the phase's Normal list and draws %d cards from it (the wiki: "
            "\"draws %s extra cards before his main deck\"); the wiki's extra cards of the 33-card phase decks are not added, and a fight "
            "that starts in phase 2 or 3 (banked damage) starts with that phase's deck and no extra cards, as the wiki says." % (draw, {3: "three", 5: "five"}[draw]))]),
        O([("kind", "power"), ("note", "The wiki marks some phase powers \"Auto-cast\"; here every clickable power is the boss AI's choice "
            "(family 01's precedent for Elder Brandlemar).")]),
    ] + extra_adapt
    return O([
        ("id", rid), ("name", name), ("provenance", "ULDA"), ("family", "24"), ("hero", hero), ("power", powers[0]), ("tier", "major3"),
        ("archetype", archetype), ("level", O([("source", "printed-health"), ("original", 300)])), ("health", 300), ("fixedHealth", True),
        ("flags", ["forms", "persistentDamage", "deckReplace"]), ("cards", extra_cards), ("incompatibleModifiers", ["mod_iron_bosses", "mod_open_road"]),
        ("source", O([("url", WIKI + page), ("read", READ)])),
        ("lists", [lst(label, pairs) for label, pairs in lists]),
        ("deck", O([("list", "Normal phase 1")])), ("forms", forms), ("fillers", fillers), ("adaptations", adapt),
    ])

def tekahn(repo):
    adv = json.load(open(os.path.join(repo, "data", "dungeon-run", "reference", "adventures.json"), encoding="utf-8"))
    ulda = [a for a in adv["adventures"] if a.get("code") == "ULDA" or a.get("key") == "ULDA"]
    ulda = ulda[0] if ulda else adv["adventures"][4]
    decks = {d["noteName"]: d for d in ulda["otherPrefixedDecks"]}
    facts = json.load(open(os.path.join(repo, "facts", "violet-hold-2026-09.json"), encoding="utf-8"))
    names = {c["id"]: c.get("name") for c in facts["cards"]}
    lists = [lst(label, pairs) for label, pairs in TEKAHN]
    relay = []
    # The wiki's player table order: Elise, Finley, Brann, Reno. Each explorer is listed three times, deck n with hero power n
    # (the run draws one entry per explorer: DungeonRun.RelayFor).
    for explorer in ("Elise", "Finley", "Brann", "Reno"):
        for n in (1, 2, 3):
            note = "ULDA_%s_Tekanh_Deck_%d" % (explorer, n)
            d = decks[note]
            counts = collections.Counter(d["cards"])
            order = []
            for cid in d["cards"]:
                if cid not in order: order.append(cid)
            label = "%s finale deck %d (%s, client DECK %d: %s)" % (explorer, n, note, d["deckId"], d["name"])
            lists.append(O([("label", label), ("cards", [O([("name", names[c]), ("id", c), ("count", counts[c])]) for c in order])]))
            relay.append(O([("hero", "ULDA_" + explorer), ("power", "ULDA_%s_HP%d" % (explorer, n)), ("deck", O([("list", label)])), ("health", 50),
                ("skipFor", ["ULDA_" + explorer])]))
    forms = [
        O([("power", "ULDA_BOSS_67p2"), ("at", O([("healthAtMost", 200)])), ("keepExcess", True), ("deck", O([("list", "Normal phase 2")])), ("draw", 0)]),
        O([("power", "ULDA_BOSS_67p3"), ("at", O([("healthAtMost", 100)])), ("keepExcess", True), ("deck", O([("list", "Normal phase 3")])), ("draw", 0)]),
    ]
    adapt = [
        O([("kind", "health"), ("note", "Tekahn keeps one hero (ULDA_BOSS_67h, 300 Health) through three phases; the wiki names no phase "
            "thresholds, so they are the Plague Lords' (phase 2 at 200 or less, phase 3 at 100 or less): power-only forms that replace the "
            "deck with the phase's Normal list (40 cards each) and draw no extra card (the wiki states none). Tekahn keeps no damage "
            "between attempts (the wiki), so the row has no persistentDamage.")]),
        O([("kind", "relay"), ("note", "The finale's hero relay (schedule finale heroRelay): the run's own hero fights first, then the four "
            "explorers less the run's own (skipFor), each at 50 Health with one of its three client finale decks (ULDA_<Explorer>_Tekanh_Deck_n) "
            "and its hero power n, the entry drawn from the run seed and the floor (DungeonRun.RelayFor). The original lets the player "
            "choose the next explorer and its hero power; here they enter in the wiki's order (Elise, Finley, Brann, Reno) and the "
            "entering hero draws the model's RelayDraw cards (family 01), not a full hand; collected signature treasures are not "
            "shuffled in.")]),
        O([("kind", "power"), ("note", "Curse of Flame's enchantment fires at the start of the boss's next turn while the cursed minion is "
            "in play, then the curse ends; Soul Pyre deals the Cost paid for the card (the engine's last known Cost in hand).")]),
    ]
    return O([
        ("id", "ulda_67_tekahn"), ("name", "Tekahn, Plague Lord of Flame"), ("provenance", "ULDA"), ("family", "24"), ("hero", "ULDA_BOSS_67h"),
        ("power", "ULDA_BOSS_67p1"), ("tier", "finale"), ("archetype", "control"), ("level", O([("source", "printed-health"), ("original", 300)])),
        ("health", 300), ("fixedHealth", True), ("flags", ["forms", "deckReplace", "relay"]),
        ("cards", ["ULDA_032", "ULDA_BOSS_67p2e", "ULD_168e", "ULD_168e3", "OG_280", "OG_133", "OG_042", "OG_134"]),
        ("incompatibleModifiers", ["mod_iron_bosses", "mod_open_road"]),
        ("source", O([("url", WIKI + "Tekahn,_Plague_Lord_of_Flame"), ("read", READ)])),
        ("lists", lists), ("deck", O([("list", "Normal phase 1")])), ("forms", forms), ("relay", relay),
        ("fillers", ["Voidwalker", "Flame Imp", "Hellfire", "Shadow Bolt", "Dread Infernal", "Abyssal Enforcer", "Lord Jaraxxus", "Siphon Soul"]),
        ("adaptations", adapt),
    ])

def main():
    bosses = [
        lord("ulda_37_xatma", "Xatma, Plague Lord of Death", "37", "Xatma,_Plague_Lord_of_Death", XATMA,
            ["ULDA_BOSS_37p1", "ULDA_BOSS_37p2", "ULDA_BOSS_37p3"], "control", 3,
            ["Shadow Word: Pain", "Northshire Cleric", "Holy Nova", "Cabal Shadow Priest", "Loot Hoarder", "Harvest Golem", "Haunted Creeper", "Kel'Thuzad"], [],
            ["ULDA_BOSS_37t"]),
        lord("ulda_38_kzrath", "K'zrath, Plague Lord of Madness", "38", "K%27zrath,_Plague_Lord_of_Madness", KZRATH,
            ["ULDA_BOSS_38p2", "ULDA_BOSS_38p1", "ULDA_BOSS_38p3"], "tempo", 3,
            ["Shadow Word: Pain", "Shadow Word: Death", "Cabal Shadow Priest", "Sap", "SI:7 Agent", "Faceless Manipulator", "Loot Hoarder", "Harvest Golem"],
            [O([("kind", "power"), ("note", "The client links the 300 form to Systematic Betrayal (ULDA_BOSS_38p2) and the 200 form to Evoke "
                "Panic (ULDA_BOSS_38p1), as the wiki's phase table does; the forms follow it. Counter-clockwise is as the run player sees the "
                "boards (family 16's EncounterRules.Rotate).")])], ["ULDA_BOSS_38p2e1"]),
        lord("ulda_39_vesh", "Vesh, Plague Lord of Murlocs", "39", "Vesh,_Plague_Lord_of_Murlocs", VESH,
            ["ULDA_BOSS_39p1", "ULDA_BOSS_39p2", "ULDA_BOSS_39p3"], "aggro", 5,
            ["Murloc Raider", "Bluegill Warrior", "Murloc Tidehunter", "Coldlight Seer", "Grimscale Oracle", "Murloc Tidecaller", "Puddlestomper", "Lightning Bolt"],
            [O([("kind", "power"), ("note", "Made of Murlocs: any Murloc of either board; the 3 Health is lost, not dealt as damage, so the "
                "Immune hero still loses it. The Heroic powers ULDA_BOSS_39px2 and 39px3 are not used (support rows).")])],
            ["ULDA_BOSS_39m", "ULDA_BOSS_39me"]),
        tekahn(REPO),
    ]
    doc = O([
        ("schemaVersion", 1), ("family", "24"),
        ("description", "Phase 8 family 24: the Plague Lords Xatma, K'zrath and Vesh (Tombs of Terror chapter 1-3 final bosses; family 01's "
            "F01.json keeps Icarax, chapter 4, family 24's own row), phased bosses with persistent damage, and Tekahn, the Tombs finale fought "
            "with the explorers' hero relay. Boss deck lists are transcribed from hearthstone.wiki.gg boss pages (read 2026-11-14), by card "
            "name; Tekahn's relay decks are the client's finale decks, by card id."),
        ("method", "The wiki's raw wikitext (?action=raw) was read for each page and the Normal deck tables transcribed in table order, "
            "rowspan-aware, up to the \"Extra cards in 33-deck\" rows; a boss-only or ambiguous card carries its id. Written by "
            "verify/tools/gen/p8f24/make_f24.py; tools/dungeon-run-catalog (step 'encounters') resolves and checks every card."),
        ("bosses", bosses), ("rejected", []),
    ])
    out = os.path.join(REPO, "data", "dungeon-run", "reference", "encounters", "F24.json")
    text = json.dumps(doc, indent=1, ensure_ascii=False) + "\n"
    open(out, "w", encoding="utf-8", newline="\n").write(text)
    print("wrote %s: %d bosses" % (out, len(bosses)))

main()
