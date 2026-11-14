# Phase 8 family 03: the bosspilot fixture. The family is collectible cards, so no boss row of its own ships; the fixture
# adds seven curated boss rows (tiers 1 to 4, one per class the cards belong to) whose decks hold all thirty cards, to the
# generated encounters file. The fair AI then plays the cards as a boss through the run setup path (ENCOUNTERS_JSON).
# These rows are test data: they are never shipped and name no wiki page.
# usage: python -I verify/tools/gen/p8f03/make_fixture.py [<encounters-v1.json> <out file>]
# (ported to verify/: defaults are the repository's data/dungeon-run/encounters-v1.json and verify/fixtures/p8f03/fixtures/encounters-f03.json)
import io, json, os, sys
V = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))  # verify/, from verify/tools/gen/<family>/

def deck(*pairs):
    out = []
    for cid, n in pairs:
        out += [cid] * n
    assert len(out) == 30, len(out)
    return out

ROWS = [
    # id, name, hero, power, tier, archetype, start boss mana (None: the act curve), deck
    ("fix03_druid", "Family 03 fixture: Druid (Untapped Potential, Hidden Oasis, Anubisath Defender)", "HERO_06a", "CS2_017", "1", "midrange", 4, deck(
        ("ULD_131", 1), ("ULD_135", 2), ("ULD_138", 2), ("ULD_194", 2), ("ULD_205", 2), ("ULD_274", 2),
        ("CS2_011", 2), ("CS2_012", 2), ("EX1_173", 2), ("EX1_164", 2), ("EX1_165", 2), ("CS2_182", 2), ("CS2_232", 2), ("CS2_013", 2), ("CS2_005", 2), ("CS2_008", 1))),
    ("fix03_paladin", "Family 03 fixture: Paladin (Pharaoh's Blessing, Phalanx Commander, Khartut Defender, Wrapped Golem)", "HERO_04a", "CS2_101", "2", "midrange", 4, deck(
        ("ULD_143", 2), ("ULD_179", 2), ("ULD_208", 2), ("ULD_215", 2), ("ULD_274", 2), ("ULD_205", 2),
        ("CS2_125", 2), ("CS2_179", 2), ("CS2_088", 2), ("CS2_087", 2), ("CS2_092", 2), ("CS2_089", 2), ("CS2_091", 2), ("CS2_162", 2), ("CS2_124", 2))),
    ("fix03_hunter", "Family 03 fixture: Hunter (Ramkahen Wildtamer, Hyena Alpha, Wild Bloodstinger, Octosari, Wasteland Scorpid)", "HERO_05a", "DS1h_292", "2", "midrange", 4, deck(
        ("ULD_151", 2), ("ULD_154", 2), ("ULD_212", 2), ("ULD_177", 1), ("ULD_194", 2),
        ("CS2_120", 2), ("CS2_125", 2), ("DS1_070", 2), ("EX1_531", 2), ("EX1_610", 2), ("EX1_611", 2), ("EX1_554", 1), ("ULD_152", 1),
        ("DS1_185", 2), ("EX1_539", 2), ("NEW1_031", 2), ("CS2_084", 1))),
    ("fix03_shaman", "Family 03 fixture: Shaman (Sandstorm Elemental, Mogu Fleshshaper, Plague of Murlocs, Vessina, Vulpera Scoundrel)", "HERO_02a", "CS2_049", "3", "midrange", 4, deck(
        ("ULD_158", 2), ("ULD_169", 2), ("ULD_172", 2), ("ULD_173", 1), ("ULD_209", 2), ("ULD_205", 2),
        ("EX1_238", 2), ("EX1_248", 2), ("EX1_251", 2), ("CS2_045", 2), ("CS2_046", 1), ("EX1_565", 2), ("CS2_042", 2), ("CS2_182", 2), ("EX1_246", 2), ("CS2_124", 2))),
    ("fix03_warrior", "Family 03 fixture: Warrior (Frightened Flunky, Restless Mummy, Tomb Warden, Into the Fray, Armagedillo)", "HERO_01b", "CS2_102", "3", "midrange", 4, deck(
        ("ULD_195", 2), ("ULD_206", 2), ("ULD_253", 2), ("ULD_256", 2), ("ULD_258", 1), ("ULD_179", 2), ("ULD_208", 2), ("ULD_274", 1),
        ("CS2_125", 2), ("CS2_179", 2), ("CS2_162", 2), ("CS2_187", 2), ("CS2_108", 2), ("CS2_105", 2), ("EX1_606", 2), ("CS2_106", 2))),
    ("fix03_mage", "Family 03 fixture: Mage (Puzzle Box of Yogg-Saron, Tortollan Pilgrim, Flame Ward)", "HERO_08c", "CS2_034", "4", "control", 6, deck(
        ("ULD_216", 2), ("ULD_236", 2), ("ULD_239", 2), ("ULD_209", 2), ("ULD_215", 2), ("ULD_177", 1),
        ("CS2_029", 2), ("CS2_032", 2), ("CS2_023", 2), ("CS2_024", 2), ("CS2_025", 2), ("EX1_277", 2), ("CS2_027", 2), ("CS2_033", 2), ("CS2_182", 2), ("EX1_287", 1))),
    ("fix03_priest", "Family 03 fixture: Priest (High Priest Amet, Psychopomp, Sandhoof Waterbearer)", "HERO_09a", "CS1h_001", "4", "midrange", 4, deck(
        ("ULD_262", 1), ("ULD_268", 2), ("ULD_270", 2), ("ULD_194", 2), ("ULD_208", 2),
        ("CS2_235", 2), ("CS2_234", 2), ("CS1_130", 2), ("CS1_112", 2), ("EX1_622", 2), ("CS2_236", 1), ("DS1_233", 2), ("CS2_182", 2), ("CS2_125", 2), ("CS2_120", 2), ("CS2_186", 2))),
]

FAMILY = ("ULD_131 ULD_135 ULD_138 ULD_143 ULD_151 ULD_154 ULD_158 ULD_169 ULD_172 ULD_173 ULD_177 ULD_179 ULD_194 ULD_195 ULD_205 ULD_206 ULD_208 "
          "ULD_209 ULD_212 ULD_215 ULD_216 ULD_236 ULD_239 ULD_253 ULD_256 ULD_258 ULD_262 ULD_268 ULD_270 ULD_274").split()

def main(src, dst):
    data = json.load(io.open(src, encoding="utf-8"))
    held = set()
    for rid, name, hero, power, tier, archetype, mana, cards in ROWS:
        assert all(e["id"] != rid for e in data["encounters"]), rid
        deck_id = "enc-" + rid
        data["decks"][deck_id] = cards
        held.update(cards)
        row = {
            "id": rid, "name": name, "provenance": "ULDA", "family": "03", "hero": hero, "power": power, "tier": tier, "archetype": archetype,
            "level": {"source": "printed-health", "original": 30},
            "deck": {"id": deck_id, "kind": "curated", "source": {"url": "fixture: curated test deck of phase 8 family 03 (no wiki list)", "read": "2026-11-12", "list": "fixture"}},
        }
        if mana is not None:
            row["bossManaCap"] = mana
            row["start"] = {"bossMana": mana}
        data["encounters"].append(row)
    missing = [c for c in FAMILY if c not in held]
    assert not missing, missing
    text = json.dumps(data, indent=1, ensure_ascii=False) + "\n"
    io.open(dst, "w", encoding="utf-8", newline="\n").write(text)
    print("wrote %s: %d fixture rows, %d encounters in all" % (dst, len(ROWS), len(data["encounters"])))

main(sys.argv[1] if len(sys.argv) > 2 else os.path.dirname(V) + '/data/dungeon-run/encounters-v1.json',
     sys.argv[2] if len(sys.argv) > 2 else V + '/fixtures/p8f03/fixtures/encounters-f03.json')
