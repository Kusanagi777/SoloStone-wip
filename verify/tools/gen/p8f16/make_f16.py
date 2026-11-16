# Phase 8 family 16 (Dalaran Heist bosses C): writes data/dungeon-run/reference/encounters/F16.json from the fourteen
# hearthstone.wiki.gg boss pages (wikitext, `curl -sS 'https://hearthstone.wiki.gg/wiki/<Page>?action=raw'`, read
# 2026-11-14) and the rows' hand-written fields below. The pages are not in git: save them as <pages>/bNN.txt (NN = the
# boss number) and pass that folder. The deck tables are read rowspan- and colspan-aware, column by column, in table
# order; a "Possible cards" pool lists each card once (count null), a "Quantity" table keeps its counts. The table reader
# is family 14's (verify/tools/gen/p8f14/make_f14.py on its branch), copied so this file stands alone.
# Usage: python3 -I verify/tools/gen/p8f16/make_f16.py [<pages folder> [<out file>]]   (LF, no BOM; deterministic)
# (defaults: verify/.out/tmp/wiki16 and data/dungeon-run/reference/encounters/F16.json of the repository this file sits in)
import json, os, re, sys, collections

V = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))  # verify/
REPO = os.path.dirname(V)
READ = "2026-11-14"
WIKI = "https://hearthstone.wiki.gg/wiki/"

# ------------------------------------------------------------------ wikitext tables

def cells_of(line):
    mark, body = line[0], line[1:]
    out = []
    for p in re.split(r'\|\||!!', body):
        depth, split, i = 0, -1, 0
        while i < len(p):
            if p.startswith('[[', i) or p.startswith('{{', i): depth += 1; i += 2; continue
            if p.startswith(']]', i) or p.startswith('}}', i): depth -= 1; i += 2; continue
            if p[i] == '|' and depth == 0: split = i; break
            i += 1
        attrs, content = (p[:split], p[split + 1:]) if split >= 0 else ('', p)
        rs = re.search(r'rowspan="?(\d+)', attrs); cs = re.search(r'colspan="?(\d+)', attrs)
        out.append((mark, int(rs.group(1)) if rs else 1, int(cs.group(1)) if cs else 1, content.strip()))
    return out

def tables(text):
    res, lines, i = [], text.split('\n'), 0
    while i < len(lines):
        if lines[i].startswith('{|'):
            rows = [[]]; i += 1
            while i < len(lines) and not lines[i].startswith('|}'):
                l = lines[i]
                if l.startswith('|-'): rows.append([])
                elif l.startswith('|') or l.startswith('!'): rows[-1].extend(cells_of(l))
                i += 1
            res.append([r for r in rows if r])
        i += 1
    return res

def grid(rows):
    g, pending = [], {}
    for r in rows:
        line, col, cells = [], 0, list(r)
        while cells or pending.get(col):
            if pending.get(col):
                cnt, cell = pending[col]
                line.append(cell)
                if cnt > 1: pending[col] = (cnt - 1, cell)
                else: del pending[col]
                col += 1; continue
            mark, rs, cs, content = cells.pop(0)
            cell = [mark, content]
            for _ in range(cs):
                line.append(cell)
                if rs > 1: pending[col] = (rs - 1, cell)
                col += 1
        g.append(line)
    return g

def link(c):
    m = re.search(r'\[\[([^\]|]+)(\|[^\]]*)?\]\]', c)
    return re.sub(r'(\s*\((card|boss)\))+$', '', m.group(1).strip()) if m else None

def deck_table(text):
    """{'Normal' | 'Heroic' | 'Normal and Heroic': [(name, count or None)]} and the 'Total cards' cell, or (None, None)."""
    for tb in tables(text):
        g = grid(tb)
        heads = [r for r in g if all(m == '!' for m, _ in r)]
        if not heads: continue
        top = [c for _, c in heads[0]]
        if not any('Normal' in c or 'Heroic' in c for c in top): continue
        sub = [c for _, c in heads[1]] if len(heads) > 1 else top
        if not any('ard' in c for c in sub): continue
        out = collections.OrderedDict()
        for col, (side, what) in enumerate(zip(top, sub)):
            if 'ard' not in what: continue
            label = 'Normal and Heroic' if ' and ' in side else ('Normal' if 'Normal' in side else 'Heroic')
            qcol = col + 1 if col + 1 < len(sub) and 'Quantity' in sub[col + 1] else None
            seen, cards = set(), []
            for r in g:
                if all(m == '!' for m, _ in r) or col >= len(r): continue
                if id(r[col]) in seen: continue
                seen.add(id(r[col]))
                name = link(r[col][1])
                if not name: continue
                cnt = int(r[qcol][1].strip()) if qcol is not None and r[qcol][1].strip().isdigit() else None
                cards.append((name, cnt))
            out[label] = cards
        total = None
        if 'Total' in top[0]:
            total = next(r[0][1] for r in g if not all(m == '!' for m, _ in r))
        return out, total
    return None, None


# ------------------------------------------------------------------ the rows (hand-written fields)

LEVELS = ("Level: the boss pages have no level table (family 14's rule, kept for the whole Heist): a quantity list names its "
          "level in the page's note (level 6; a page without a note holds a 30-card list like those, level 6), a pool table's "
          "'Total cards' column gives the deck sizes at the boss's levels, which grow with the level (10 at level 1, 15 at 2, 20 "
          "at 3, 25 at 4 and 5, 30 at 6), and the row takes the lowest level that holds the largest listed size; a hero printed "
          "with 10 Health is a level-1 boss, and a chapter's final boss is its level 8. Tier: levels 1-2 tier 1, 3-4 tier 2, 5-6 "
          "tier 3, 7-8 tier 4 (the design's mapping); the fight's Health is the act curve's, not the printed 30, 10 or 40.")

def pool_deck(total):
    return ("The wiki lists pools of possible cards (Normal and Heroic), not decks: the boss plays " + total + " of them at its levels. "
            "A combat tier fights with 30: the Normal pool in table order, a second copy of each non-Legendary card, then the Heroic pool, "
            "then the curated fillers named in the row (the encounters step records each added card).")

Q_DECK = ("The wiki lists the level-6 deck with its quantities (30 cards, Normal and Heroic): the Normal list is the deck; an unusable "
          "card is replaced as recorded.")

# Ol' Toomba's Tales of Fortune: the wiki's 22 "old Treasures" (its generated cards: the Kobolds and Catacombs Dungeon Run's
# treasures) less the two that read or rewrite a hidden zone of the run player (see the row's power note).
TOOMBA_TREASURES = ["LOOTA_806", "LOOTA_811", "LOOTA_812", "LOOTA_816", "LOOTA_819", "LOOTA_821", "LOOTA_826", "LOOTA_827", "LOOTA_835",
                    "LOOTA_836", "LOOTA_837", "LOOTA_838", "LOOTA_839", "LOOTA_841", "LOOTA_842a", "LOOTA_842b", "LOOTA_843", "LOOTA_847",
                    "LOOT_998k", "LOOT_998l"]
# Tipsi Wobblerune's Portal Party: the wiki's seven Portals (its generated cards); PARTY PORTAL!'s Partygoers (its summoned minions).
PORTALS = ["KAR_073", "GVG_003", "DALA_BOSS_38t", "KAR_077", "KAR_091", "KAR_075", "KAR_076"]
PARTYGOERS = ["CFM_659", "BRM_019", "KARA_13_22", "GIL_805", "DAL_721"]

R = collections.OrderedDict
ROWS = [
    dict(n="31", key="timothy", page="Timothy_Jones", tier="2", level=4, archetype="midrange", flags=[], quantity=25,
         cards=["DALA_BOSS_31e"],
         notes=[("schedule", "Timothy Jones's page (Chapter 3) names no level; his quantity lists hold 25 cards (Normal and Heroic), the "
                 "size of levels 4 and 5. Level 4: tier 2."),
                ("power", "Bling it On! targets any minion (the client text says 'a minion'). 'Golden' is the client's PREMIUM tag (12) on "
                 "the minion entity, which the client draws as golden; the engine gives it no rule. A minion that is already Golden "
                 "(PREMIUM set, by this power or as it came into play) gets Blinged Out! (DALA_BOSS_31e, +4/+4) instead.")]),
    dict(n="32", key="kizi", page="Kizi_Copperclip", tier="1", level=1, archetype="aggro", flags=["passive"], total="10; 15",
         extend=True, fillers=[], cards=["DALA_BOSS_32e1", "DALA_BOSS_32e2", "DALA_BOSS_32e3", "DALA_BOSS_32e4"],
         notes=[("schedule", "Kizi Copperclip's hero card is printed with 10 Health and her pool table gives 10 and 15 cards (levels 1-2): "
                 "level 1, tier 1."),
                ("power", "Makeover is auto-cast (the wiki's Hero Power table: 'Auto-cast'), as family 09's rule-cast powers: at the start "
                 "of her turn, with (1) Mana and a friendly minion, the Mana is paid and a random friendly minion gets one of the four "
                 "haircuts, each with the same chance: Nondescript Fade (Stealth), Intrusive Pompadour (Taunt), Incredible Quiff (+2 "
                 "Health), Aggressive Mohawk (+2 Attack) (DALA_BOSS_32e1 to 32e4). Never an option of the boss's AI.")]),
    dict(n="33", key="gold_elemental", page="Gold_Elemental", tier="2", level=4, archetype="midrange", flags=["passive"], total="25",
         extend=True, fillers=[], cards=["GAME_005"],
         notes=[("schedule", "Gold Elemental's pool table gives 25 cards (levels 4-5; the page places him in Chapter 1 and his hero card is "
                 "printed with 40 Health). Level 4: tier 2."),
                ("deck", "Infuriated Banker (DALA_502, the boss card of his Heroic pool) is family 17's card and has no script yet; the "
                 "Normal pool and its second copies fill the 30 cards before the Heroic pool is reached."),
                ("power", "Made of Coins: each time his hero takes damage one Coin (GAME_005) joins his hand (a hit his Armor absorbs "
                 "counts too: the wiki's Armor page, 'damage taken while protected by Armor still counts as taking damage'); "
                 "nothing with a full hand.")]),
    dict(n="34", key="carousel", page="Carousel_Gryphon_(boss)", name="Carousel Gryphon", tier="2", level=3, archetype="midrange",
         flags=["passive", "bothPlayers", "position"], total="15; 20", extend=True, fillers=[], ids={"Hyena": "EX1_534t"},
         notes=[("schedule", "The Carousel Gryphon's pool table gives 15 and 20 cards (levels 2-3). Level 3: tier 2."),
                ("deck", "The Hunter pool's 'Hyena' is Savannah Highmane's 2/2 Beast (EX1_534t, the only Hyena of the Heist's time)."),
                ("power", "Merry Go Round: at the end of the boss's turn every minion on the board moves one place around a ring made of "
                 "both rows, counter-clockwise as the run player sees the board (the client text names no direction; Ulduar's K'zrath, "
                 "ULDA_BOSS_38p2, names counter-clockwise for the same rotation): the run player's row moves right and its rightmost "
                 "minion crosses to the right end of the boss's row, the boss's row moves left and its leftmost minion crosses to the "
                 "left end of the run player's row (Actions.EncounterRules.Rotate). A minion that crosses changes controller as a "
                 "control change does (it keeps its damage, enchantments and age). A run shrine never changes controller "
                 "(ShrineRules.BlocksControl): it is left out of the ring and keeps its row; the other minions rotate around it.")]),
    dict(n="35", key="zoie", page="Soothsayer_Zoie", tier="2", level=3, archetype="control", flags=["passive"], total="15; 20",
         extend=True, fillers=[],
         notes=[("schedule", "Soothsayer Zoie's pool table gives 15 and 20 cards (levels 2-3). Level 3: tier 2."),
                ("power", "Healing Hands restores 2 Health to each of her minions at the start of her turn (a hero power's healing: the "
                 "healing rules, such as Auchenai Soulpriest's, apply).")]),
    dict(n="36", key="draemus", page="Draemus", tier="3", level=6, archetype="midrange", flags=[], quantity=30, cards=["DALA_BOSS_36t"],
         notes=[("schedule", "The page's note: the deck is the level-6 deck (level 5: five cards fewer). Level 6: tier 3."),
                ("power", "Import \"Pet\" is Build-A-Beast (the page's note: 'functionally identical to Build-A-Beast', Deathstalker "
                 "Rexxar's ICC_828p): a Discover among three of the engine's Build-A-Beast first-part Beasts, then among three of its "
                 "second-part Beasts, each pool limited to the cards the run's generation filter allows the boss; the two make one "
                 "minion (the Zombeast rule: summed Cost, Attack and Health, the first card's text and the second's keywords) named "
                 "Exotic Pet (DALA_BOSS_36t, the client's untexted card of the crafted pet) in the boss's hand. The boss's AI answers "
                 "both Discovers. Not offered with a full hand.")]),
    dict(n="37", key="toomba", page="Ol%27_Toomba", name="Ol' Toomba", tier="3", level=6, archetype="tempo", flags=[], quantity=30,
         cards=TOOMBA_TREASURES,
         notes=[("schedule", "Ol' Toomba's page names no level; his quantity list is a 30-card deck like the level-6 lists of the other "
                 "Heist pages. Level 6: tier 3."),
                ("power", "Tales of Fortune shuffles one random 'old Treasure' into his deck, shown as it is shuffled (so his own-deck "
                 "ledger holds it): the treasures the wiki lists as the power's generated cards, the Kobolds and Catacombs Dungeon Run's, "
                 "each admitted for a boss on catalog runs. Two of the wiki's 22 are left out: Gloves of Mugging (LOOTA_834, steals "
                 "three cards of the run player's hidden hand) and Aleatoric Cube (LOOTA_820, deals both hidden decks anew) read or "
                 "rewrite a hidden zone of the run player, which a boss's fair view cannot hold yet (rows run-treasure-hand-steal and "
                 "run-treasure-belief-reset stay the run player's only); twenty remain, each with the same chance. Not offered with a "
                 "full deck.")]),
    dict(n="38", key="tipsi", page="Tipsi_Wobblerune", tier="2", level=4, archetype="tempo", flags=[], total="20; 25", extend=True,
         fillers=[], ids={"PARTY PORTAL! (Dalaran Heist)": "DALA_BOSS_38t"}, cards=[p for p in PORTALS if p != "DALA_BOSS_38t"] + PARTYGOERS,
         notes=[("schedule", "Tipsi Wobblerune's pool table gives 20 and 25 cards (levels 3-5). Level 4: tier 2."),
                ("power", "Portal Party adds one of the seven Portals the wiki lists as its generated cards (Maelstrom Portal, Unstable "
                 "Portal, PARTY PORTAL!, Silvermoon Portal, Ironforge Portal, Moonglade Portal, Firelands Portal), each with the same "
                 "chance; not offered with a full hand."),
                ("power", "PARTY PORTAL! (DALA_BOSS_38t, the boss card of her Normal pool) summons one 'random Partygoer': the client text "
                 "names none; the wiki's PARTY PORTAL! page lists its five summoned minions (Gadgetzan Socialite, Grim Patron, Mime, "
                 "Coffin Crasher, Catrina Muerte), each with the same chance. Mime (KARA_13_22, One Night in Karazhan's 'Always "
                 "silenced.' 5/6) is scripted by this family: it enters play silenced.")]),
    dict(n="39", key="aki", page="Aki_the_Brilliant", tier="3", level=6, archetype="midrange", flags=["passive"], quantity=30,
         cards=["DALA_BOSS_39e"],
         notes=[("schedule", "Aki the Brilliant's page names no level; her quantity list is a 30-card deck like the level-6 lists of the "
                 "other Heist pages. Level 6: tier 3."),
                ("power", "For The Light! is auto-cast (the wiki's Hero Power table: 'Auto-cast'), as family 09's rule-cast powers: at the "
                 "start of her turn, with (2) Mana and a minion in her hand, the Mana is paid and each minion in her hand gets "
                 "Light-imbued (DALA_BOSS_39e, +1/+1). Never an option of the boss's AI.")]),
    dict(n="40", key="oxana", page="Oxana_Demonslay", tier="3", level=6, archetype="control", flags=["passive"], quantity=30,
         notes=[("schedule", "The page's note: the deck is the deck of levels 6-8 (level 5: five cards fewer). Level 6: tier 3."),
                ("power", "Immolation Aura: after a minion attacks her hero (the run player's minions; the attack has resolved), it takes "
                 "2 damage from the power (a hero power's damage: no Spell Damage); nothing when the attacker already died.")]),
    dict(n="41", key="ungan", page="Ungan_Oddkind", tier="3", level=6, archetype="midrange", flags=[], quantity=30,
         cards=["NEW1_032", "NEW1_033", "NEW1_034"],
         notes=[("schedule", "The page's note: the deck is the level-6 deck (level 5: five cards fewer). Level 6: tier 3."),
                ("power", "Summon Companion summons one of Animal Companion's three (Huffer, Leokk, Misha: the wiki's summoned minions), "
                 "each with the same chance; needs a free board slot.")]),
    dict(n="42", key="pogo", page="P.O.G.O.", tier="3", level=6, archetype="tempo", flags=[], quantity=30,
         replacements={"Fel Reaver": "Fel Cannon"},
         notes=[("schedule", "The page's note: the deck is the level-6 deck of Chapter 5 (levels 4-5: five cards fewer). Level 6: tier 3."),
                ("deck", "The wiki's list holds four Pogo-Hoppers (Normal; six in the Heroic list): an encounter deck keeps the listed "
                 "count."),
                ("deck", "Fel Reaver has no script in this engine: Fel Cannon (a Mech of its Cost), as family 14 replaces it."),
                ("power", "Pogoshuffle targets any minion (the client text says 'a minion'): three new copies of its card (the printed "
                 "card, without its enchantments) are shuffled into the boss's deck, shown as they are shuffled (the target was public, "
                 "and the boss's own-deck ledger holds the three); nothing past a full deck.")]),
    dict(n="44", key="erekem", page="Erekem", tier="3", level=6, archetype="midrange", flags=["hiddenZone"], quantity=30,
         cards=["DALA_BOSS_44e"],
         notes=[("schedule", "Erekem's page (Chapter 2) names no level; his quantity list is a 30-card deck like the level-6 lists of the "
                 "other Heist pages. Level 6: tier 3."),
                ("power", "Dark Tidings picks a random minion of the boss's own deck (the game RNG), reveals it to both players, gives it "
                 "Forseen (DALA_BOSS_44e, +2/+2) and moves it to the top of the deck, where it is the next card drawn; the top card is "
                 "recorded for the own-deck ledger (EncounterRules.DeckTopKnown). With no minion in the deck nothing happens: whether "
                 "one is left is the boss's own estimate, so the power is offered while the deck holds a card.")]),
    dict(n="45", key="moragg", page="Moragg_(boss)", name="Moragg", tier="4", level=8, archetype="control", flags=[], quantity=30,
         notes=[("schedule", "The page: 'the final boss that can be encountered in Chapter 2' (a chapter's level 8; his quantity list is "
                 "the 30-card deck). Level 8: tier 4."),
                ("power", "Ray of Suffering deals the number on the power (TAG_SCRIPT_DATA_NUM_1, printed 1, the client's '@') to the "
                 "run player's hero, then the number grows by 1 ('increases each use'): 1, 2, 3, ... A hero power's damage (no Spell "
                 "Damage).")]),
]

def main(pages, out):
    from_facts = json.load(open(os.path.join(REPO, "facts", "violet-hold-2026-09.json"), encoding="utf-8"))["cards"]
    by_id = {c["id"]: c for c in from_facts}
    bosses = []
    for row in ROWS:
        n = row["n"]
        text = open(os.path.join(pages, "b%s.txt" % n), encoding="utf-8").read()
        lists, total = deck_table(text)
        if lists is None: raise SystemExit("no deck table in b%s.txt" % n)
        if not row.get("quantity") and total != row["total"]: raise SystemExit("b%s.txt: total %r, expected %r" % (n, total, row["total"]))
        hero, power = "DALA_BOSS_%sh" % n, "DALA_BOSS_%sp" % n
        name = row.get("name") or by_id[hero]["name"]
        if by_id[hero]["name"] != name: raise SystemExit("%s is %r, not %r" % (hero, by_id[hero]["name"], name))
        out_lists = []
        for label, cards in lists.items():
            entries = []
            for cname, cnt in cards:
                e = R([("name", cname)])
                if cname in row.get("ids", {}): e["id"] = row["ids"][cname]
                if cnt is not None: e["count"] = cnt
                entries.append(e)
            if row.get("quantity") and sum(e["count"] for e in entries) != row["quantity"]:
                raise SystemExit("b%s.txt %s: not %d" % (n, label, row["quantity"]))
            out_lists.append(R([("label", label), ("cards", entries)]))
        b = R()
        b["id"] = "dala_%s_%s" % (n, row["key"])
        b["name"] = name
        b["provenance"] = "DALA"
        b["family"] = "16"
        b["hero"] = hero
        b["power"] = power
        b["tier"] = row["tier"]
        b["archetype"] = row["archetype"]
        b["level"] = R([("source", "wiki-level-table"), ("original", row["level"])])
        if row["flags"]: b["flags"] = row["flags"]
        if row.get("cards"): b["cards"] = row["cards"]
        b["source"] = R([("url", WIKI + row["page"]), ("read", READ)])
        b["lists"] = out_lists
        b["deck"] = R([("list", "Normal")])
        b["extend"] = ["Heroic"]
        if row.get("replacements"): b["replacements"] = R(sorted(row["replacements"].items()))
        if row.get("fillers"): b["fillers"] = row["fillers"]
        notes = []
        if row.get("quantity") == 30: deck_note = Q_DECK
        elif row.get("quantity"):
            deck_note = ("The wiki lists a %d-card deck with its quantities (Normal and Heroic): a combat tier fights with 30: the Normal "
                         "list, a second copy of each non-Legendary single, then the Heroic list (the encounters step records each added "
                         "card)." % row["quantity"])
        else: deck_note = pool_deck(row["total"])
        notes.append(R([("kind", "deck"), ("note", deck_note)]))
        for kind, note in row["notes"]: notes.append(R([("kind", kind), ("note", note)]))
        b["adaptations"] = notes
        bosses.append(b)
    doc = R()
    doc["schemaVersion"] = 1
    doc["family"] = "16"
    doc["description"] = ("Dalaran Heist bosses C (phase 8 family 16): fourteen encounters of The Dalaran Heist (DALA_BOSS_31 to 42, 44 "
        "and 45: Timothy Jones, Kizi Copperclip, Gold Elemental, Carousel Gryphon, Soothsayer Zoie, Draemus, Ol' Toomba, Tipsi "
        "Wobblerune, Aki the Brilliant, Oxana Demonslay, Ungan Oddkind, P.O.G.O., Erekem, Moragg) and their boss-only cards (Exotic "
        "Pet, PARTY PORTAL!, the enchantments). Boss deck lists are transcribed from hearthstone.wiki.gg boss pages (read " + READ +
        "), by card name; tools/dungeon-run-catalog (step 'encounters') resolves the names against facts/violet-hold-2026-09.json, "
        "checks every card and writes data/dungeon-run/encounters-v1.json. " + LEVELS)
    doc["method"] = ("Each page's wikitext (?action=raw) was read and its deck table transcribed by verify/tools/gen/p8f16/make_f16.py: "
        "rowspan- and colspan-aware, column by column in table order, Normal and Heroic separately. A 'Possible cards' pool lists each "
        "card once; a 'Quantity' table keeps its counts (each such list sums to its deck size). The wiki's disambiguated link names are "
        "written as the card names; the boss-only cards carry their id.")
    doc["bosses"] = bosses
    doc["rejected"] = []
    text = json.dumps(doc, indent=1, ensure_ascii=False) + "\n"
    open(out, "w", encoding="utf-8", newline="\n").write(text)
    print("wrote %s: %d rows" % (out, len(bosses)))

main(sys.argv[1] if len(sys.argv) > 1 else os.path.join(V, ".out", "tmp", "wiki16"),
     sys.argv[2] if len(sys.argv) > 2 else os.path.join(REPO, "data", "dungeon-run", "reference", "encounters", "F16.json"))
