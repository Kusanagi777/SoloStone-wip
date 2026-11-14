# Phase 8 family 18 (Dalaran Heist bosses E and the Heist finale): writes data/dungeon-run/reference/encounters/F18.json, the
# hand transcription of the twelve encounters DALA_BOSS_61-66, 68, 70-74 and the four finale bosses DALA_BOSS_43, 67, 69, 60
# (Commander Bolan, DALA_BOSS_75, is rejected below). The deck tables were read from the hearthstone.wiki.gg boss pages
# (wikitext, ?action=raw, 2026-11-14; the raw pages are not kept) by the rowspan- and colspan-aware table reader of family 14
# (verify/tools/gen/p8f14/make_f14.py), column by column in table order, Normal and Heroic separately, and are embedded here
# as read: a "Quantity" table keeps its counts, a "Possible cards" pool lists each card once (count None). Names are the card
# names (the link target, the "(card)" suffix dropped); a boss-only or ambiguous card carries its id. Deterministic, LF, no
# BOM; no network.
# Usage: python3 -I verify/tools/gen/p8f18/make_f18.py [<out file>]
import collections, json, os, sys

V = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))  # verify/
REPO = os.path.dirname(V)
READ = "2026-11-14"
WIKI = "https://hearthstone.wiki.gg/wiki/"
OD = collections.OrderedDict

LISTS = {
    61: {
        'Normal': [
            ('Naturalize', 2), ('Webweave', 2), ('Spider Bomb', 2), ('Infested Wolf', 2), ('Vilebrood Skitterer', 2), ('Demonic Project', 2),
            ('Despicable Dreadlord', 2), ('Aranasi Broodmother', 2), ('Rin, the First Disciple', 1), ('Fearsome Doomguard', 2), ('Lord Godfrey', 1),
            ('Fel Lord Betrug', 1), ('Voidlord', 1), ('DOOM!', 1), ('Giant Wasp', 2), ('Stoneskin Basilisk', 2), ('Witchwood Grizzly', 2),
            ('Maexxna', 1)
        ],
        'Heroic': [
            ('Webweave', 2), ('Hadronox', 1), ('Spider Bomb', 2), ('Vilebrood Skitterer', 2), ('Gnomeferatu', 2), ('Voidcaller', 2),
            ('Despicable Dreadlord', 1), ('Rin, the First Disciple', 1), ('Lord Godfrey', 1), ('Twisting Nether', 2), ('Voidlord', 2), ('DOOM!', 1),
            ('Stubborn Gastropod', 1), ('Giant Wasp', 2), ('Stoneskin Basilisk', 2), ('Sludge Belcher', 2), ("N'Zoth, the Corruptor", 1)
        ],
    },
    62: {
        'Normal': [
            ('Forbidden Shaping', 2), ('Forbidden Words', 1), ("Lazul's Scheme", 1), ('Cloning Device', 2), ('Resurrect', 2),
            ('Eternal Servitude', 1), ('Mindgames', 2), ('Shadow Madness', 2), ('Shadow Word: Horror', 2), ('Convincing Infiltrator', 2),
            ('Devour Mind', 2), ('Holy Water', 1), ('Shadow Essence', 2), ('Lesser Diamond Spellstone', 2), ('Free From Amber', 1),
            ('Mass Resurrection', 1), ('Mind Control', 1), ('Deathlord', 2), ('Mossy Horror', 1)
        ],
        'Heroic': [
            ('Forbidden Words', 1), ("Lazul's Scheme", 1), ('Psionic Probe', 2), ('Confuse', 1), ('Resurrect', 2), ('Shadow Visions', 2),
            ('Spirit Lash', 2), ('Eternal Servitude', 1), ('Mindgames', 2), ('Shadow Word: Horror', 1), ('Devour Mind', 1), ('Entomb', 1),
            ('Shadow Essence', 1), ('Lesser Diamond Spellstone', 2), ('Psychic Scream', 1), ('Mass Resurrection', 2), ('Obsidian Statue', 1),
            ('Mind Control', 1), ('Grave Horror', 1), ('Cairne Bloodhoof', 1), ('Ragnaros the Firelord', 1), ('The Lich King', 1),
            ("Y'Shaarj, Rage Unbound", 1)
        ],
    },
    63: {
        'Normal': [
            ('Inner Rage', None), ('Blood To Ichor', None), ('Devastate', None), ('Whirlwind', None), ('Armorsmith', None), ('Cleave', None),
            ('Rampage', None), ('Sleep with the Fishes', None), ('Ravaging Ghoul', None), ('Bloodhoof Brave', None), ('Arcanite Reaper', None),
            ('Brawl', None), ('Sea Reaver', None), ('Rotface', None), ('Elven Archer', None), ('Cheaty Anklebiter', None),
            ('Defender of Argus', None), ('Big Game Hunter', None), ('The Black Knight', None)
        ],
        'Heroic': [
            ('Inner Rage', None), ('Warbot', None), ('Whirlwind', None), ('Commanding Shout', None), ('Rampage', None), ('Warpath', None),
            ('Frothing Berserker', None), ('Ravaging Ghoul', None), ("Val'kyr Soulclaimer", None), ('Blood Razor', None), ('Brawl', None),
            ('Security Rover', None), ('Gorehowl', None), ('Grommash Hellscream', None), ('Argent Squire', None), ('Amani Berserker', None),
            ('Acolyte of Pain', None), ('Raging Worgen', None), ('Gurubashi Berserker', None)
        ],
    },
    64: {
        'Normal': [
            ('I Know a Guy', 2), ('Slam', 2), ('Kobold Barbarian', 2), ('Ogre Warmaul', 2), ('Axe Flinger', 2), ('Blackhowl Gunspire', 1),
            ('Gravelsnout Knight', 1), ('Gurubashi Offering', 1), ('Baleful Banker', 2), ('Dirty Rat', 2), ('Echoing Ooze', 1), ('Alarm-o-Bot', 2),
            ('Blubber Baron', 2), ('Ogre Brute', 2), ('Toothy Chest', 2), ('Arena Treasure Chest', 2), ('Venture Co. Mercenary', 1),
            ('Safeguard', 1)
        ],
        'Heroic': [
            ('Ixlid, Fungal Lord', 1), ('Dispatch Kodo', 2), ('Doubling Imp', 2), ('I Know a Guy', 1), ('Town Crier', 2), ('Rabid Worgen', 2),
            ("Kor'kron Elite", 1), ('Akali, the Rhino', 1), ('Stonetusk Boar', 2), ('Echoing Ooze', 2), ('Vicious Scalehide', 2),
            ('Argent Horserider', 1), ('Doppelgangster', 2), ('Zilliax', 1), ('Argent Commander', 2), ('Safeguard', 1), ('Amani War Bear', 2),
            ('Batterhead', 2), ('Oondasta', 1)
        ],
    },
    65: {
        'Normal': [
            ('Blessing of Wisdom', 1), ('Eye for an Eye', 1), ('Getaway Kodo', 1), ('Humility', 1), ("Smuggler's Run", 1), ('Rebuke', 1),
            ('Call to Adventure', 2), ('Rallying Blade', 2), ('Sword of Justice', 2), ('Consecration', 2), ('Duel!', 2), ('A New Challenger...', 1),
            ('Lay on Hands', 1), ('Argent Squire', 2), ('Cloakscale Chemist', 2), ('Blood Knight', 1), ('Scarlet Crusader', 2),
            ('Silvermoon Guardian', 2), ('Dalaran Crusader', 2), ('Argent Commander', 1)
        ],
        'Heroic': [
            ('Eye for an Eye', 1), ('Getaway Kodo', 1), ('Crystalsmith Kangor', 1), ('Shielded Minibot', 2), ('Divine Favor', 2),
            ('Wickerflame Burnbristle', 1), ('Annoy-o-Module', 1), ('Truesilver Champion', 2), ('Glowstone Technician', 2),
            ('Grimestreet Enforcer', 2), ('Sunkeeper Tarim', 1), ("Val'anyr", 1), ('Lay on Hands', 1), ('Ragnaros, Lightlord', 1),
            ('Tirion Fordring', 1), ('Nozari', 1), ('Annoy-o-Tron', 1), ('Argent Horserider', 1), ('Blood Knight', 2), ('Zilliax', 1),
            ('Argent Commander', 1), ('Azalina Soulthief', 1), ("Kel'Thuzad", 1), ('Ysera', 1)
        ],
    },
    66: {
        'Normal': [
            ('Call of the Void', None), ('Kobold Librarian', None), ('Soulfire', None), ('The Soularium', None), ('Voidwalker', None),
            ('Curse of Weakness', None), ('Darkshire Librarian', None), ('Defile', None), ('Void Analyst', None), ('Drain Life', None),
            ('Impferno', None), ('Spreading Madness', None), ('Fiendish Circle', None), ('Summoning Portal', None)
        ],
        'Heroic': [
            ('Flame Imp', None), ('The Soularium', None), ('Voidwalker', None), ('Curse of Weakness', None), ('Dark Peddler', None),
            ('Darkbomb', None), ('Darkshire Councilman', None), ('Imp Gang Boss', None), ("Rafaam's Scheme", None), ('Eager Underling', None),
            ('Imp-losion', None), ('Despicable Dreadlord', None), ('Xavius', None)
        ],
    },
    68: {
        'Normal': [
            ('Babbling Book', 2), ('Research Project', 2), ('Shimmering Tempest', 2), ("Conjurer's Calling", 2), ('Duplicate', 2),
            ('Splitting Image', 2), ('Volcanic Potion', 1), ('Polymorph', 1), ('Blast Wave', 2), ("Cabalist's Tome", 1), ("Dragon's Fury", 1),
            ('Ethereal Conjurer', 1), ('Flame Lance', 1), ('Meteor', 1), ('Firelands Portal', 1), ('Greater Arcane Missiles', 1),
            ('Power of Creation', 1), ('Rhonin', 1), ('Knife Juggler', 2), ('Arcane Dynamo', 2), ("Yogg-Saron, Hope's End", 1)
        ],
        'Heroic': [
            ('Arcane Missiles', 2), ('Babbling Book', 2), ('Ray of Frost', 2), ('Flame Geyser', 2), ('Magic Dart Frog', 2),
            ('Shimmering Tempest', 2), ('Unstable Portal', 2), ('Spellslinger', 2), ('Ghastly Conjurer', 2), ("Dragon's Breath", 2),
            ('Archmage Antonidas', 1), ('Power of Creation', 2), ('Rhonin', 1), ('Clockwork Gnome', 2), ('Knife Juggler', 2), ('Mechanical Yeti', 2)
        ],
    },
    70: {
        'Normal': [
            ('Feign Death', 1), ('Fireworks Tech', 2), ('Spider Bomb', 1), ('Explosive Shot', 2), ('Oblivitron', 1), ('Cannon Barrage', 2),
            ('Bloodsail Corsair', 2), ('Mistress of Mixtures', 2), ('Patches the Pirate', 1), ('Zombie Chow', 2), ("Captain's Parrot", 2),
            ('Explosive Sheep', 2), ('Southsea Captain', 2), ('Voodoo Doll', 2), ('Explodinator', 1), ('Feugen', 1), ('Seaforium Bomber', 2),
            ('Stalagg', 1), ('Gelbin Mekkatorque', 1)
        ],
        'Heroic': [
            ('Deadly Shot', 2), ('Marked Shot', 2), ('Unleash the Beast', 2), ('Swashburglar', 2), ('Bloodsail Howler', 2), ('Shady Dealer', 2),
            ('Cannon Barrage', 2), ('Captain Hooktusk', 1), ("N'Zoth's First Mate", 2), ('Patches the Pirate', 1), ("Captain's Parrot", 2),
            ("Ship's Cannon", 2), ('Southsea Captain', 2), ('Cult Master', 2), ('Hench-Clan Hag', 2), ('Ticket Scalper', 1), ("Skycap'n Kragg", 1)
        ],
    },
    71: {
        'Normal': [
            ('Dendrologist', 2), ('Witchwood Apple', 2), ('Addled Grizzly', 2), ('Landscaping', 2), ('Wild Growth', 2), ('Forest Guide', 2),
            ('Poison Seeds', 2), ('Soul of the Forest', 1), ('Force of Nature', 2), ('Living Mana', 1), ('Lunar Visions', 2), ('Treespeaker', 1),
            ('Recycle', 2), ('Nourish', 2), ('Splintergraft', 1), ("The Forest's Aid", 1), ('Cenarius', 1), ('Mulchmuncher', 1),
            ('Volcanic Lumberer', 1)
        ],
        'Heroic': [
            ('Acornbearer', 2), ('Barkskin', 1), ('Forbidden Ancient', 2), ('Power of the Wild', 2), ('Witchwood Apple', 2),
            ('Blessing of the Ancients', 1), ('Healing Touch', 1), ('Landscaping', 2), ('Branching Paths', 1), ('Evolving Spores', 1),
            ('Poison Seeds', 1), ('Soul of the Forest', 1), ('Force of Nature', 1), ('Living Mana', 1), ('Treespeaker', 1), ('Tending Tauren', 1),
            ('Ancient of Lore', 2), ('Ancient of War', 1), ('Ironbark Protector', 1), ("The Forest's Aid", 1), ('Cenarius', 1), ('Mulchmuncher', 1),
            ('Volcanic Lumberer', 2), ('Ultimate Infestation', 1)
        ],
    },
    72: {
        'Normal': [
            ('Kobold Librarian', 2), ('Mortal Coil', 1), ('Demonic Project', 2), ('Queen of Pain', 2), ('Vulgar Homunculus', 2),
            ('Chittering Tunneler', 2), ('Spreading Madness', 2), ('Blood Witch', 2), ('Hellfire', 1), ('Bane of Doom', 2), ('Dread Infernal', 2),
            ('Kabal Trafficker', 1), ('Siphon Soul', 1), ("Cho'gall", 1), ('DOOM!', 1), ('Crystallizer', 2), ('Lifedrinker', 2), ('Bomb Squad', 2)
        ],
        'Heroic': [
            ('Flame Imp', 2), ('Kobold Librarian', 2), ('Spirit Bomb', 2), ('Vulgar Homunculus', 2), ('Wrathguard', 2), ('Unlicensed Apothecary', 2),
            ('Hellfire', 2), ('Pit Lord', 2), ('Despicable Dreadlord', 2), ('Felfire Potion', 2), ('Abyssal Enforcer', 2), ('Lifedrinker', 2),
            ('Bittertide Hydra', 2), ('Bomb Squad', 2), ('Baron Geddon', 1), ('Hakkar, the Soulflayer', 2)
        ],
    },
    73: {
        'Normal': [
            ('Beakered Lightning', None), ('Air Elemental', None), ('Dust Devil', None), ('Elementary Reaction', None), ('Fire Fly', None),
            ('Murmuring Elemental', None), ('Reincarnate', None), ('Bog Slosher', None), ('Far Sight', None), ('Hot Spring Guardian', None),
            ('Primal Talismans', None), ('Hammer of Twilight', None), ('Voodoo Hexxer', None), ('Fire Elemental', None), ('Grave Shambler', None),
            ('Arcane Tyrant', None), ('Frozen Crusher', None), ('Whirlwind Tempest', None), ('Ozruk', None)
        ],
        'Heroic': [
            ('Earthen Might', None), ('Elementary Reaction', None), ('Fire Plume Harbinger', None), ('Menacing Nimbus', None),
            ('Hot Spring Guardian', None), ('Hex', None), ('Earth Elemental', None), ('Fire Elemental', None), ('Stone Sentinel', None),
            ("Al'Akir the Windlord", None), ('Kalimos, Primal Lord', None), ('Walking Fountain', None), ('Vicious Fledgling', None),
            ('Fire Plume Phoenix', None), ('Blazecaller', None), ('Ravenholdt Assassin', None), ('Ragnaros the Firelord', None)
        ],
    },
    74: {
        'Normal': [
            ('Forbidden Ritual', 2), ('Dark Pact', 1), ("Malchezaar's Imp", 2), ('Sanguine Reveler', 1), ('Soulfire', 1), ('The Soularium', 1),
            ('Clutchmother Zavas', 1), ('Corrupting Mist', 1), ('Darkshire Librarian', 2), ('Tiny Knight of Evil', 2), ('Howlfiend', 2),
            ('Silverware Golem', 2), ('Unwilling Sacrifice', 1), ('High Priestess Jeklik', 1), ('Cruel Dinomancer', 2), ('Soulwarden', 2),
            ('Loot Hoarder', 2), ('Gnomish Inventor', 2), ('Jeeves', 2)
        ],
        'Heroic': [
            ('Kobold Librarian', 1), ("Malchezaar's Imp", 2), ('Soulfire', 1), ('The Soularium', 1), ('Clutchmother Zavas', 1),
            ('Darkshire Librarian', 2), ('Gnomeferatu', 2), ('Tiny Knight of Evil', 3), ('Silverware Golem', 2), ('Fist of Jaraxxus', 2),
            ('High Priestess Jeklik', 1), ("Blood-Queen Lana'thel", 1), ('Cruel Dinomancer', 1), ('Kabal Trafficker', 1), ('Soulwarden', 1),
            ('Mana Wraith', 2), ('Zola the Gorgon', 1), ('Arena Fanatic', 2), ('Deathaxe Punisher', 2), ('Nexus-Champion Saraad', 1)
        ],
    },
    43: {
        'Normal': [
            ('Kabal Lackey', 1), ('Arcanologist', 2), ("Medivh's Valet", 1), ('Research Project', 2), ('Counterspell', 2), ('Duplicate', 2),
            ('Explosive Runes', 2), ('Ice Block', 1), ('Kirin Tor Mage', 2), ('Mana Bind', 2), ('Mirror Entity', 2), ('Spellbender', 1),
            ('Splitting Image', 2), ('Ethereal Arcanist', 2), ('Kabal Crystal Runner', 2), ('Kalecgos', 1), ('Secretkeeper', 2),
            ('Mad Scientist', 2), ('Sunreaver Spy', 1), ('Masked Contender', 2), ('Subject 9', 1)
        ],
        'Heroic': [
            ('Kabal Lackey', 1), ('Arcanologist', 2), ("Medivh's Valet", 2), ('Counterspell', 2), ('Duplicate', 2), ('Explosive Runes', 2),
            ('Forgotten Torch', 2), ('Kirin Tor Mage', 2), ('Mana Bind', 1), ('Mirror Entity', 2), ('Spellbender', 1), ('Splitting Image', 2),
            ('Ethereal Arcanist', 2), ('Aluneth', 1), ('Kabal Crystal Runner', 2), ('Firelands Portal', 2), ('Secretkeeper', 2),
            ('Mad Scientist', 2), ('Sunreaver Spy', 1), ('Masked Contender', 1), ('Subject 9', 1)
        ],
    },
    67: {
        'Normal': [
            ('Mirror Image', 2), ('Shooting Star', 2), ('Arcane Explosion', 2), ('Pyros', 1), ('Snap Freeze', 2), ('Duplicate', 2),
            ('Spellslinger', 2), ('Twilight Flamecaller', 2), ('Unexpected Results', 2), ('Dalaran Aspirant', 2), ('Kirin Tor Tricaster', 2),
            ('Ethereal Conjurer', 2), ('Aluneth', 1), ('Archmage Antonidas', 1), ('Power of Creation', 2), ('Rhonin', 1), ('Spellzerker', 2),
            ('Red Mana Wyrm', 2), ('Medivh, the Guardian', 1), ('Arcane Giant', 2)
        ],
        'Heroic': [
            ('Arcane Blast', 2), ('Arcane Missiles', 2), ('Arcane Explosion', 2), ('Celestial Emissary', 2), ("Sorcerer's Apprentice", 2),
            ('Duplicate', 2), ('Soot Spewer', 1), ('Unexpected Results', 2), ('Cosmic Anomaly', 4), ('Dalaran Aspirant', 2),
            ('Kirin Tor Tricaster', 1), ('Aluneth', 1), ('Archmage Antonidas', 1), ('Rhonin', 1), ('Bloodmage Thalnos', 1), ('Spellzerker', 2),
            ('Tainted Zealot', 2), ('Azure Drake', 2), ('Medivh, the Guardian', 1), ('Arcane Giant', 2)
        ],
    },
    69: {
        'Normal': [
            ('Ray of Frost', 2), ('Shooting Star', 2), ('Pyros', 1), ('Research Project', 2), ('Explosive Runes', 2), ('Flamewaker', 2),
            ('Mana Bind', 2), ('Stargazer Luna', 1), ('Unexpected Results', 2), ('Twilight Flamecaller', 2), ('Vex Crow', 2), ("Cabalist's Tome", 2),
            ('Ethereal Conjurer', 2), ('Deck of Wonders', 2), ('Blizzard', 2), ('Firelands Portal', 2), ('Greater Arcane Missiles', 1),
            ('Rhonin', 1), ('Dragoncaller Alanna', 1), ('Kalecgos', 1), ('Pyroblast', 1), ('Archmage Vargoth', 1), ('Sunreaver Warmage', 2),
            ('Arcane Dynamo', 2)
        ],
        'Heroic': [
            ('Arcane Blast', 2), ('Arcane Missiles', 2), ('Babbling Book', 2), ('Shooting Star', 2), ('Arcane Explosion', 2),
            ('Primordial Glyph', 1), ("Sorcerer's Apprentice", 2), ('Cinderstorm', 2), ('Explosive Runes', 2), ('Stargazer Luna', 1),
            ('Unexpected Results', 2), ('Flamewaker', 2), ('Vex Crow', 2), ("Cabalist's Tome", 2), ('Ethereal Conjurer', 2), ('Firelands Portal', 2),
            ('Rhonin', 1), ('Dragoncaller Alanna', 1), ('Kalecgos', 1), ('Archmage Vargoth', 1), ('Azerite Elemental', 2), ('Azure Drake', 2),
            ('Arcane Dynamo', 1), ('Malygos', 1)
        ],
    },
    60: {
        'Normal': [
            ("Archmage's Apprentice", 2), ('Ray of Frost', 1), ('Shooting Star', 2), ('Research Project', 2), ('Snap Freeze', 2), ('Flamecannon', 2),
            ("Sorcerer's Apprentice", 2), ('Flamewaker', 2), ('Arcane Intellect', 1), ('Kirin Tor Tricaster', 2), ('Blast Wave', 1), ('Aluneth', 1),
            ('Blizzard', 2), ('Meteor', 2), ('Meteorologist', 1), ('Astromancer', 2), ('Firelands Portal', 1), ('Power of Creation', 1),
            ('Rhonin', 1), ('Kalecgos', 1), ('Banana Buffoon', 2), ('Twilight Drake', 2), ('Azure Drake', 2), ('Medivh, the Guardian', 1),
            ('Malygos', 1), ('Arcane Giant', 1)
        ],
        'Heroic': [
            ("Archmage's Apprentice", 2), ('Arcane Missiles', 2), ('Magic Trick', 2), ('Ray of Frost', 1), ('Shooting Star', 2),
            ('Tome of Intellect', 2), ('Flamecannon', 2), ("Sorcerer's Apprentice", 2), ('Arcane Intellect', 2), ('Flamewaker', 2),
            ('Forgotten Torch', 2), ('Echo of Medivh', 2), ('Molten Reflection', 2), ('Kirin Tor Tricaster', 1), ('Meteorologist', 1),
            ('Astromancer', 2), ('Firelands Portal', 1), ('Rhonin', 1), ('Kalecgos', 1), ('Banana Buffoon', 2), ('Twilight Drake', 2),
            ('Arcane Giant', 2), ('Molten Giant', 2)
        ],
    },
}

# Boss-only or ambiguous names: their ids.
IDS = {"Archmage's Apprentice": "KARA_00_08"}

TIERS = "levels 1-2: tier 1; 3-4: tier 2; 5-6: tier 3; 7-8: tier 4 (the encounter model's level table)"
LEVELS = ("Level: the Heist boss pages have no level table. The deck grows with the boss's level (10 cards at level 1, 15 at 2, 20 at 3, "
          "25 at 4 and 5, 30 from 6: the notes of Whirt the All-Knowing, Albin Eastoft and Awilo, the rule of families 14 and 15), so a "
          "quantity table of 30 cards (\"These decks correspond only to level 6\" where the page says so) is level 6, a pool table's "
          "'Total cards' gives the sizes at the boss's levels and the row takes the lowest level that holds the largest, and a hero "
          "printed with 10 Health is a level-1 boss; a chapter's final boss is tier 4. Tier: " + TIERS + "; the fight's Health is the "
          "act curve's, not the printed 30 or 10.")


def tier_note(text, level, tier):
    return "%s Level %d: tier %d." % (text, level, tier)


Q_DECK = ("The wiki lists the boss's 30-card deck with its quantities (Normal and Heroic columns): the Normal list is the deck; an "
          "unusable card is replaced as recorded.")


def pool_deck(total):
    return ("The wiki lists pools of possible cards (Normal and Heroic), not decks: the boss plays " + total + " of them at its levels. A "
            "combat tier fights with 30: the Normal pool in table order, a second copy of each non-Legendary card, then the Heroic pool "
            "(the encounters step records each added card).")


FINALE = ("The Heist's finale: Chapter 5, Kirin Tor Citadel, has twelve fights, and the last four are always the same, Magistrix Norroa, "
          "Archmage Vargoth, Archmage Kalec and Archmage Khadgar, in that order (the wiki's Dalaran Heist page, Twists: \"Four Additional "
          "Encounters: (Chapter 5) There are four additional bosses in the fifth chapter, for a total of twelve bosses. The last four "
          "bosses are always the same for every run\"; Twist - The Finale, DALA_830). Here the four are the fixed bosses of the schedule's "
          "finale (tier finale: never drawn by a combat or major slot), at levels 9-12; the fight's Health is the finale's curve. A finale "
          "deck keeps the wiki's count (%d cards, Normal column).")


def boss(n, slug, tier, archetype, level, page, notes, flags=(), cards=(), pool=None, replacements=None, finale=False):
    lists = []
    for label in ("Normal", "Heroic"):
        entries = []
        for name, count in LISTS[n][label]:
            e = OD([("name", name)])
            if name in IDS: e["id"] = IDS[name]
            if count is not None: e["count"] = count
            entries.append(e)
        lists.append(OD([("label", label), ("cards", entries)]))
    r = OD()
    r["id"] = "dala_%d_%s" % (n, slug)
    r["name"] = NAMES[n]
    r["provenance"] = "DALA"
    r["family"] = "18"
    r["hero"] = "DALA_BOSS_%dh" % n
    r["power"] = "DALA_BOSS_%dp" % n
    r["tier"] = tier
    r["archetype"] = archetype
    r["level"] = OD([("source", "wiki-level-table"), ("original", level)])
    if flags: r["flags"] = list(flags)
    if cards: r["cards"] = list(cards)
    r["source"] = OD([("url", WIKI + page), ("read", READ)])
    r["lists"] = lists
    r["deck"] = OD([("list", "Normal")])
    if pool: r["extend"] = ["Heroic"]
    if replacements: r["replacements"] = OD(sorted(replacements.items()))
    adapt = []
    if finale: adapt.append(OD([("kind", "schedule"), ("note", FINALE % sum(c for _, c in LISTS[n]["Normal"]))]))
    else: adapt.append(OD([("kind", "deck"), ("note", pool_deck(pool) if pool else Q_DECK)]))
    for kind, note in notes: adapt.append(OD([("kind", kind), ("note", note)]))
    r["adaptations"] = adapt
    return r


NAMES = {61: "Sael'orn", 62: "Haro Setting-Sun", 63: "Dagg Cruelmight", 64: "Banker Biggs", 65: "Valdera Highborne", 66: "Marei Loom",
         68: "Lilayell Suntear", 70: "Sky Captain Smiggs", 71: "Anarii Duskgrove", 72: "Kazamon Steelskin", 73: "Hesutu Stonewind",
         74: "Kara Stamper", 43: "Magistrix Norroa", 67: "Archmage Vargoth", 69: "Archmage Kalec", 60: "Archmage Khadgar"}

L6 = "The wiki lists a 30-card quantity table (the level-6 deck size; the page names no level)."
ARCANE = ["AT_004", "EX1_277", "CS2_025", "CS2_023", "CFM_623"]

ROWS = [
    boss(61, "saelorn", "3", "control", 6, "Sael%27orn", flags=["hiddenZone"], notes=[
        ("schedule", tier_note("A boss of Chapter 2. " + L6, 6, 3)),
        ("power", "Web Grab (0), \"Your opponent summons a minion from their hand\": Dirty Rat's pull (CFM_790's rule): one random minion of "
                  "the run player's hand (the game RNG) is summoned to that player's board, without its Battlecry; nothing when that hand "
                  "holds no minion or that board is full. The summoned minion becomes public as any summon does; the rest of the hand stays "
                  "hidden. It is offered while the run player's board has room and hand holds a card (both public); in the boss's fair "
                  "view the hidden cards are no minions, so its search pulls nothing (the real pull is a random public event)."),
        ("deck", "Phase Spider (DALA_BOSS_61t) is not in either of the wiki's deck lists: the wiki calls it \"an unused card intended for use "
                 "during Sael'orn encounter\" and lists its voice lines under Unused, so the row has no Phase Spider (family 18 records the "
                 "card as rejected: unused in the adventure)."),
    ]),
    boss(62, "haro", "3", "control", 6, "Haro_Setting-Sun", flags=["bothPlayers"], cards=["DALA_BOSS_62e"], notes=[
        ("schedule", tier_note(L6, 6, 3)),
        ("power", "Darken (2), \"Give all minions -2 Attack until your next turn\": every minion of both boards gets Darkened (DALA_BOSS_62e, "
                  "-2 Attack), which is removed at the start of the boss's next turn (Time Out!'s rule: the enchantment's turn-start "
                  "trigger is its creator's). Offered only while a minion is on a board."),
    ]),
    boss(63, "dagg", "2", "aggro", 4, "Dagg_Cruelmight", pool="20 or 25", notes=[
        ("schedule", tier_note("Dagg Cruelmight's pool table gives 20 and 25 cards (levels 3-5).", 4, 2)),
        ("power", "Bully (2), \"Deal $2 damage to a damaged minion\": aimed at a damaged minion of either board (the play requirement "
                  "REQ_DAMAGED_TARGET); a hero power, so no Spell Damage."),
    ]),
    boss(64, "biggs", "3", "midrange", 6, "Banker_Biggs", cards=["DALA_BOSS_64e"], notes=[
        ("schedule", tier_note("A boss of Chapter 1; the page's note: \"These decks correspond only to level 6\" (level 5: five cards fewer).", 6, 3)),
        ("power", "Invest! (4), \"Return a friendly minion to your hand. Give it +4/+4\": the boss's minion returns to its hand (the engine's "
                  "bounce: its enchantments are lost) and gets Capital Gains (DALA_BOSS_64e, +4/+4) there. Offered only while the boss's "
                  "hand has room (a full hand would destroy the minion and give nothing)."),
    ]),
    boss(65, "valdera", "3", "midrange", 6, "Valdera_Highborne", flags=["passive", "damageCap"], notes=[
        ("schedule", tier_note("A boss of Chapter 1. " + L6, 6, 3)),
        ("power", "Impervious: \"Your hero can only take up to 3 damage at a time\": Animated Armor's hero damage cap with 3 for 1 "
                  "(Actions.EncounterRules.HeroDamageCap on the hero power; one hit above 3 deals 3, before Armor), the boss's hero only."),
    ]),
    boss(66, "marei", "1", "tempo", 1, "Marei_Loom", flags=["bothPlayers", "hiddenZone"], pool="10 or 15", notes=[
        ("schedule", tier_note("Marei Loom's hero card is printed with 10 Health (a level-1 boss); her pool table gives 10 and 15 cards "
                               "(levels 1-2).", 1, 1)),
        ("power", "Next... (0), \"Swap the left-most card in each player's hand with one in their deck\": for each player whose hand and "
                  "deck both hold a card, the left-most hand card is shuffled into that player's deck (its hand enchantments are lost, "
                  "as a card returned to a deck loses them) and a random card of that deck (the game RNG, picked before the shuffle) takes "
                  "the left-most hand slot. Each player sees the card that came to the own hand; what the opponent believed about either "
                  "player's hand and deck is reset (RunHelpers.BeliefReset), so the boss never learns the run player's new card. Offered "
                  "while either player has a card in hand and in deck (zone counts, public)."),
        ("deck", "Xavius, in her Heroic pool, is not a card of the client data (no card of that name in facts/violet-hold-2026-09.json): "
                 "it is left out of the extension."),
    ]),
    boss(68, "lilayell", "3", "tempo", 6, "Lilayell_Suntear", flags=["passive"], notes=[
        ("schedule", tier_note("A boss of Chapter 5. " + L6, 6, 3)),
        ("power", "Mini Portal: \"After you cast a spell, summon a random 1-cost minion\": after the boss casts a spell (from hand or by "
                  "an effect), one random scripted minion that costs (1), of any class, from the pool the run's generation filter allows "
                  "for the boss, is summoned for it; nothing with a full board."),
    ]),
    boss(70, "smiggs", "3", "aggro", 6, "Sky_Captain_Smiggs", flags=["bothPlayers"], notes=[
        ("schedule", tier_note(L6, 6, 3)),
        ("power", "Bombardment (2), \"Deal $2 damage to all minions. Takes 2 turns to reload.\": 2 damage to every minion of both boards "
                  "(no Spell Damage). The reload: the two turns after the shot (the run player's and the boss's next one) the power is not "
                  "offered; the boss's turn after that it is ready again (the turn of the shot is kept on the hero power, "
                  "TAG_SCRIPT_DATA_NUM_1). The client's Bombardment Recharge enchantment (DALA_BOSS_70e, \"Disables Hero Power while "
                  "reloading\") is display only and is not attached. Offered only while a minion is on a board."),
    ]),
    boss(71, "anarii", "3", "midrange", 6, "Anarii_Duskgrove", cards=["EX1_573t"], notes=[
        ("schedule", tier_note(L6, 6, 3)),
        ("power", "Summon Protectors (2), \"Summon a 2/2 Treant with Taunt\": the Treant the wiki names (EX1_573t, Cenarius's); it needs a "
                  "free board slot."),
    ]),
    boss(72, "kazamon", "3", "aggro", 6, "Kazamon_Steelskin", flags=["passive"], notes=[
        ("schedule", tier_note(L6, 6, 3)),
        ("power", "Steelskin: \"You can't damage yourself\": damage that a source of the boss deals to the boss's own hero is prevented "
                  "(no damage, no Armor lost, no damage event; Actions.EncounterRules.PreventsSelfDamage, read by Character.TakeDamage "
                  "before the hero's other damage rules), so his deck's self-damage (Hellfire, Kobold Librarian, Chittering Tunneler, Blood "
                  "Witch, Dread Infernal, Crystallizer's 5 damage while it still gives Armor) costs him nothing. Health lost without damage "
                  "(a Health cost) is not damage, and Fatigue is the game's rule, not his source: it still hurts. The run player's sources "
                  "hurt him as usual."),
    ]),
    boss(73, "hesutu", "2", "aggro", 3, "Hesutu_Stonewind", pool="15 or 20", cards=["DALA_BOSS_73e"], notes=[
        ("schedule", tier_note("Hesutu Stonewind's pool table gives 15 and 20 cards (levels 2-3).", 3, 2)),
        ("power", "Earthmother's Rage (2), \"Give a friendly minion Windfury\": Earthmother's Rancor (DALA_BOSS_73e, Windfury) on a minion of "
                  "the boss."),
    ]),
    boss(74, "kara", "3", "midrange", 6, "Kara_Stamper", notes=[
        ("schedule", tier_note(L6, 6, 3)),
        ("power", "Soul Weave (3), \"Discard a card. Summon a random minion of the same cost\": one random card of the boss's own hand (the "
                  "game RNG; the engine's discard) and then one random scripted minion of any class that costs what the discarded card cost "
                  "in hand, from the pool the run's generation filter allows for the boss; nothing summoned when no minion has that Cost or "
                  "the board is full. Offered while the boss holds a card and its board has room."),
    ]),
    # ---------------------------------------------------------------- the finale (tier finale, levels 9-12)
    boss(43, "norroa", "finale", "tempo", 9, "Magistrix_Norroa", flags=["passive"], finale=True, notes=[
        ("power", "Kaironomaly: \"Your opponent has 25 seconds to take their turn\": at the start of each of the run player's turns that "
                  "player's turn timer (the client's TIMEOUT tag, which Nozdormu the Eternal sets to 15) is 25 seconds unless it is shorter "
                  "already (Actions.EncounterRules.KaironomalySeconds). The engine has no clock: the harnesses play the tag; the "
                  "client's match (NativeMatch) ends a turn whose timer is short, for 15 and 25 alike (EncounterRules.ShortTurnSeconds). "
                  "The wiki's note (Nozdormu on the board disrupts the power) is the old Nozdormu's aura, not a rule of the boss."),
    ]),
    boss(67, "vargoth", "finale", "tempo", 10, "Archmage_Vargoth_(boss)", cards=ARCANE, finale=True, notes=[
        ("power", "Arcane Runes (0), \"Add a random Arcane spell to your hand\": the pool is the wiki's list of the cards the power generates "
                  "(Arcane Blast, Arcane Missiles, Arcane Explosion, Arcane Intellect, Greater Arcane Missiles), one at random (the game "
                  "RNG), each behind the run's generation filter for the boss. Offered while the boss's hand has room."),
    ]),
    boss(69, "kalec", "finale", "control", 11, "Archmage_Kalec_(boss)", finale=True, notes=[
        ("power", "Dragonwrath (2), \"Give your Hero Spell Damage +1\": the boss's hero gains Spell Damage +1 for the rest of the match "
                  "(the hero's own SPELLPOWER, which the controller's Spell Damage counts), cumulative with each use."),
    ]),
    boss(60, "khadgar", "finale", "control", 12, "Archmage_Khadgar", flags=["recast"], cards=["DALA_504"], finale=True, notes=[
        ("power", "Summon Elemental (3), \"Summon a 2/3 Elemental. It copies your spells.\": the wiki's special card Kirin Tor Elemental "
                  "(DALA_504, \"Your spells cast an additional time\"). After its controller casts a spell, a copy is cast again on the "
                  "same target (RecastRules.Recast, Pirate's Mark's rule: a target that is gone loses only the targeted part, a Choose "
                  "One copy keeps the option picked); a copy never casts again, so each Elemental adds one cast; a Secret or a Quest is "
                  "not cast twice. It needs a free board slot."),
        ("deck", "Archmage's Apprentice (KARA_00_08, the wiki's \"Boss\" card, also in Chronomancer Inara's deck) is family 09's script."),
    ]),
]

REJECTED = [
    OD([("id", "DALA_BOSS_75h"), ("name", "Commander Bolan"), ("power", "DALA_BOSS_75p"),
        ("reason", "A Quest! (\"Give your opponent a Quest!\") hands the run player one of five quests (DALA_BOSS_75e1 Bear Liver Quest, 75e2 "
                   "Murloc Quest, 75e3 Turtle Escort Quest, 75e4 Herbalist Quest, 75e5 Hogger Quest) whose completion ends the fight; the "
                   "client data gives the five enchantments a name and no text, no requirement and no reward tags, and the wiki gives none "
                   "either (its page: no deck, \"will instead summon minions from turn to turn like Tad\", \"Upon completion of any event, the "
                   "reward choice is given and the battle ends\"; the quest names have no page, only rows marked Unknown in the Rise of "
                   "Shadows enchantment list), nor the minions he summons. Neither the quests nor the fight can be built from a source"),
        ("lift", "data: a source for the five quests' requirements and Bolan's summon list (a transcription of the original encounter), then "
                 "a script of the quests as run-player enchantments that end the fight with a no-loss win (EncounterRules.EndWithNoLoss)"),
        ("cards", ["DALA_BOSS_75e1", "DALA_BOSS_75e2", "DALA_BOSS_75e3", "DALA_BOSS_75e4", "DALA_BOSS_75e5"])]),
]


def main(out):
    data = OD()
    data["schemaVersion"] = 1
    data["family"] = "18"
    data["description"] = ("Dalaran Heist bosses E and the Heist finale (phase 8 family 18): twelve encounters of The Dalaran Heist (Sael'orn, "
                           "Haro Setting-Sun, Dagg Cruelmight, Banker Biggs, Valdera Highborne, Marei Loom, Lilayell Suntear, Sky Captain "
                           "Smiggs, Anarii Duskgrove, Kazamon Steelskin, Hesutu Stonewind, Kara Stamper) and the four fixed bosses of the "
                           "Heist finale (Magistrix Norroa, Archmage Vargoth, Archmage Kalec, Archmage Khadgar; Twist - The Finale, DALA_830, "
                           "is the rule of the schedule's finale block), with the boss card Kirin Tor Elemental. Commander Bolan is rejected "
                           "(data gap). Boss deck lists are transcribed from hearthstone.wiki.gg boss pages (read " + READ + "), by card name; "
                           "tools/dungeon-run-catalog (step 'encounters') resolves the names against facts/violet-hold-2026-09.json, checks "
                           "every card and writes data/dungeon-run/encounters-v1.json. " + LEVELS)
    data["method"] = ("Each page's wikitext (?action=raw) was read and its deck table transcribed by the family 14 table reader: rowspan- and "
                      "colspan-aware, column by column in table order, Normal and Heroic separately. A 'Possible cards' pool lists each card "
                      "once; a 'Quantity' table keeps its counts. The wiki's link target is the card name; the boss-only card carries its id. "
                      "Generated by verify/tools/gen/p8f18/make_f18.py.")
    data["bosses"] = ROWS
    data["rejected"] = REJECTED
    text = json.dumps(data, indent=1, ensure_ascii=False) + "\n"
    with open(out, "w", encoding="utf-8", newline="\n") as f: f.write(text)
    print("wrote %s: %d rows, %d rejected" % (out, len(ROWS), len(REJECTED)))


main(sys.argv[1] if len(sys.argv) > 1 else os.path.join(REPO, "data", "dungeon-run", "reference", "encounters", "F18.json"))
