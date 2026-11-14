# Phase 8 family 02: the collectA bosspilot fixture. Copies the lane's generated encounters-v1.json and appends eight fixture
# boss rows whose 30-card decks hold the family's 33 collectibles (each at least twice across the rows), so the fair AI plays
# them as a boss through the run setup path (c-bosspilot-encounter with ENCOUNTERS_JSON and WATCH). The rows are test data
# only: they are never written into the repo's data files. Rerun after every encounters step (the base file is copied).
#   python -I verify/tools/gen/p8f02/make_fixture.py [<output file>]
# (ported to verify/: reads the repository this file sits in; writes verify/fixtures/p8f02/fixtures/encounters-collectA.json by default)
import json, os, sys
V = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))  # verify/, from verify/tools/gen/<family>/
# Main tree (integrated in wave 1): the repo's generated encounters-v1.json, the fixture beside this script.
SRC = os.path.dirname(V) + '/data/dungeon-run/encounters-v1.json'
OUT = sys.argv[1] if len(sys.argv) > 1 else V + '/fixtures/p8f02/fixtures/encounters-collectA.json'

def deck(*parts):
    cards = []
    for cid, n in parts: cards += [cid] * n
    if len(cards) != 30: raise SystemExit('deck has %d cards: %r' % (len(cards), parts))
    return cards

YETI, GOLEM = 'CS2_182', 'CS2_186'
ROWS = [
    # Secrets and the Secret cards (Mysterious Challenger, Commander Rhyssa), Eadric, Prismatic Lens, Duel!, Djinni of Zephyrs.
    ('fix02_paladin', 'Collectibles A fixture: Paladin', 'HERO_04', 'CS2_101', deck(
        ('AT_079', 2), ('DAL_573', 2), ('AT_081', 2), ('BOT_436', 2), ('DAL_731', 2), ('LOE_053', 2),
        ('EX1_130', 2), ('EX1_136', 2), ('FP1_020', 2), ('EX1_132', 1), ('EX1_379', 1),
        ('CS2_087', 2), ('CS2_092', 2), (YETI, 4), ('EX1_096', 2))),
    # Ogre Warmaul, Bouncing Blade, Festeroot Hulk, Obsidian Destroyer, Fierce Monkey, Foe Reaper, Fencing Coach, Eerie Statue,
    # Anubisath Sentinel.
    ('fix02_warrior', 'Collectibles A fixture: Warrior', 'HERO_01', 'CS2_102', deck(
        ('GVG_054', 2), ('GVG_050', 2), ('GIL_655', 2), ('LOE_009', 2), ('LOE_022', 2), ('GVG_113', 2), ('AT_115', 2), ('LOE_107', 2),
        ('LOE_061', 2), (YETI, 4), ('CS2_125', 4), ('EX1_096', 4))),
    # Call Pet, Gahz'rilla, Tomb Spider, Jeweled Scarab, Lil' Exorcist, Recombobulator.
    ('fix02_hunter', 'Collectibles A fixture: Hunter', 'HERO_05', 'DS1h_292', deck(
        ('GVG_017', 2), ('GVG_049', 2), ('LOE_047', 2), ('LOE_029', 2), ('GVG_097', 2), ('GVG_108', 2),
        ('DS1_178', 4), ('CS2_125', 4), ('CS2_120', 4), (YETI, 4), ('EX1_096', 2))),
    # Animated Armor, Hex Lord Malacrass, Arch-Thief Rafaam, Blingtron 3000, Gelbin Mekkatorque, Enhance-o Mechano.
    ('fix02_mage', 'Collectibles A fixture: Mage', 'HERO_08', 'CS2_034', deck(
        ('LOE_119', 2), ('TRL_318', 2), ('LOE_092', 2), ('GVG_119', 2), ('EX1_112', 2), ('GVG_107', 2),
        ('CS2_023', 2), ('CS2_029', 2), (YETI, 6), ('CS2_125', 4), ('EX1_096', 4))),
    # Madame Lazul, Vol'jin, One-eyed Cheat with Pirates.
    ('fix02_priest', 'Collectibles A fixture: Priest and Pirates', 'HERO_09', 'CS1h_001', deck(
        ('DAL_729', 2), ('GVG_014', 2), ('GVG_025', 2), ('CS2_146', 2), ('NEW1_018', 2), ('NEW1_027', 2),
        ('CS2_234', 2), ('CS2_004', 2), (YETI, 6), (GOLEM, 2), ('CS2_125', 4), ('EX1_096', 2))),
    # Swampqueen Hagatha (and Shaman spells beside what her Horror casts).
    ('fix02_shaman', 'Collectibles A fixture: Hagatha', 'HERO_02', 'CS2_049', deck(
        ('DAL_431', 2), ('EX1_238', 2), ('EX1_248', 2), ('CS2_045', 2),
        (YETI, 6), (GOLEM, 2), ('CS2_125', 4), ('EX1_096', 4), ('CS2_120', 6))),
    # Wilfred Fizzlebang with a drawing Hero Power (Life Tap) among cheap minions, so the 6-Cost 4/4 is the play at 6 mana.
    ('fix02_warlock', 'Collectibles A fixture: Wilfred', 'HERO_07', 'CS2_056', deck(
        ('AT_027', 2), ('CS2_120', 6), ('CS2_172', 6), ('EX1_096', 6), ('CS2_125', 6), ('CS2_189', 4))),
    # Mimiron's Head among Mechs, with the other Mech cards once more.
    ('fix02_mech', 'Collectibles A fixture: Mechs', 'HERO_08', 'CS2_034', deck(
        ('GVG_111', 2), ('GVG_006', 2), ('GVG_085', 2), ('GVG_013', 2), ('GVG_096', 2), ('GVG_107', 2), ('GVG_113', 2), ('GVG_119', 2),
        ('EX1_112', 2), ('GVG_108', 2), ('GVG_014', 2), ('GVG_049', 2), ('TRL_318', 2), ('LOE_092', 2), ('LOE_119', 2))),
]

IDS = ['AT_027', 'AT_079', 'AT_081', 'AT_115', 'BOT_436', 'DAL_573', 'DAL_729', 'DAL_731', 'EX1_112', 'GIL_655', 'GVG_017', 'GVG_025', 'GVG_050',
       'GVG_054', 'GVG_097', 'GVG_107', 'GVG_108', 'GVG_111', 'GVG_113', 'GVG_119', 'LOE_009', 'LOE_022', 'LOE_029', 'LOE_047', 'LOE_053', 'LOE_061',
       'LOE_092', 'LOE_107', 'LOE_119', 'TRL_318', 'DAL_431', 'GVG_014', 'GVG_049']

data = json.load(open(SRC, encoding='utf-8'))
listed = set()
for rid, name, hero, power, cards in ROWS:
    listed.update(cards)
    data['decks']['enc-' + rid] = cards
    data['encounters'].append({
        'id': rid, 'name': name, 'provenance': 'DALA', 'family': '02', 'hero': hero, 'power': power, 'tier': '2', 'archetype': 'midrange',
        'level': {'source': 'printed-health', 'original': 30},
        'deck': {'id': 'enc-' + rid, 'kind': 'curated', 'source': {'url': 'fixture: p8f02/make_fixture.py', 'read': '2026-11-12', 'list': 'fixture'}},
    })
missing = [i for i in IDS if i not in listed]
if missing: raise SystemExit('fixture decks miss ' + ','.join(missing))
os.makedirs(os.path.dirname(OUT), exist_ok=True)
with open(OUT, 'w', encoding='utf-8', newline='\n') as f:
    json.dump(data, f, indent=1, ensure_ascii=False)
    f.write('\n')
print('wrote', OUT, len(ROWS), 'fixture rows')
