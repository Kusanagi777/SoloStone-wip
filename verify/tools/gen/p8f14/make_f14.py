# Phase 8 family 14 (Dalaran Heist bosses A): writes data/dungeon-run/reference/encounters/F14.json from the fifteen
# hearthstone.wiki.gg boss pages (wikitext, `curl -sS 'https://hearthstone.wiki.gg/wiki/<Page>?action=raw'`, read
# 2026-11-14) and the rows' hand-written fields below. The pages are not in git: save them as <pages>/bNN.txt (NN = the
# boss number) and pass that folder. The deck tables are read rowspan- and colspan-aware, column by column, in table
# order; a "Possible cards" pool lists each card once (count null), a "Quantity" table keeps its counts.
# Usage: python3 -I verify/tools/gen/p8f14/make_f14.py [<pages folder> [<out file>]]   (LF, no BOM; deterministic)
# (defaults: verify/.out/tmp/wiki and data/dungeon-run/reference/encounters/F14.json of the repository this file sits in)
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

LEVELS = ("Level: the boss pages have no level table. A quantity list names its level in the page's note (level 6); a pool table's "
          "'Total cards' column gives the deck sizes at the boss's levels, which grow with the level (10 at level 1, 15 at 2, 20 at 3, "
          "25 at 4 and 5, 30 at 6: the notes of Whirt the All-Knowing, Albin Eastoft and Awilo), and the row takes the lowest level that "
          "holds the largest listed size; a hero printed with 10 Health is a level-1 boss. Tier: levels 1-2 tier 1, 3-4 tier 2, 5-6 tier 3 "
          "(the design's mapping); the fight's Health is the act curve's, not the printed 30 or 10.")

def pool_deck(total):
    return ("The wiki lists pools of possible cards (Normal and Heroic), not decks: the boss plays " + total + " of them at its levels. "
            "A combat tier fights with 30: the Normal pool in table order, a second copy of each non-Legendary card, then the Heroic pool, "
            "then the curated fillers named in the row (the encounters step records each added card).")

Q_DECK = ("The wiki lists the level-6 deck with its quantities (30 cards, Normal and Heroic): the Normal list is the deck; an unusable "
          "card is replaced as recorded.")

R = collections.OrderedDict
ROWS = [
    dict(n="01", key="chomper", page="Chomper", tier="1", level=1, archetype="midrange", flags=[], total="10; 15",
         extend=True, fillers=["Bloodfen Raptor", "Murloc Tidehunter"], replacements={"Power of the Wild": "Mark of the Lotus"},
         notes=[("deck", "Power of the Wild's original printing has no script in this engine: Mark of the Lotus, as family 10."),
                ("schedule", "Chomper's hero card is printed with 10 Health (a level-1 boss of Chapter 4; his pool table gives 10 cards at "
                 "level 1 and 15 at level 2): tier 1.")]),
    dict(n="02", key="awilo", page="Awilo,_Cooking_Trainer", tier="2", level=3, archetype="aggro", flags=["passive"], total="15; 20",
         extend=True, fillers=[],
         notes=[("schedule", "Awilo's pool table gives 15 and 20 cards (levels 2-3); the page adds that at level 4 the deck stays at 20. "
                 "Level 3: tier 2.")]),
    dict(n="03", key="akazamzarak", page="The_Great_Akazamzarak", tier="1", level=1, archetype="tempo",
         flags=["bothPlayers", "hiddenZone"], total="15; 20", extend=True, fillers=[],
         cards=["DALA_BOSS_03t", "DALA_BOSS_03t2", "DALA_BOSS_03t3", "DALA_BOSS_03t4", "DALA_BOSS_03e", "LOOT_106t"],
         notes=[("schedule", "The Great Akazamzarak's hero card is printed with 10 Health (a level-1 boss), though his pool table gives 15 "
                 "and 20 cards; the printed level decides: tier 1."),
                ("power", "Prestidigitation adds one of the four magical feats (DALA_BOSS_03t Reductomara, 03t2 Bunnifitronus, 03t3 Presto "
                 "Legendaro, 03t4 Yoggers Poggers), each with the same chance. Reductomara's (1) less sits on every spell in both hands and "
                 "decks as Reductomara'd (DALA_BOSS_03e); an Echo or 'Repeatable this turn' spell is never brought below (1). Bunnifitronus's "
                 "critters are the wiki's three (Frog, Sheep, Squirrel). Presto Legendaro gives each player one random scripted collectible "
                 "Legendary minion its side's generation filter allows (golden is a cosmetic the engine does not model).")]),
    dict(n="04", key="kaye", page="Kaye_Toogie", tier="2", level=4, archetype="midrange", flags=["bothPlayers", "hiddenZone"],
         total="25", extend=True, fillers=[], replacements={"Anima Golem": "Piloted Sky Golem", "Fel Reaver": "Fel Cannon"},
         notes=[("schedule", "Kaye Toogie's pool table gives 25 cards (levels 4-5). Level 4: tier 2."),
                ("deck", "Anima Golem and Fel Reaver have no script in this engine: curated replacements Piloted Sky Golem and Fel Cannon "
                 "(Mechs of their Costs)."),
                ("power", "Open Wormhole is Duel!'s rule (DAL_731): a random minion of each player's deck is summoned and the boss's "
                 "attacks the other.")]),
    dict(n="05", key="bonepaw", page="The_Amazing_%22Bonepaw%22", name='The Amazing "Bonepaw"', tier="2", level=4, archetype="control",
         flags=["bothPlayers", "recast"], total="20; 25", extend=True, fillers=[],
         notes=[("schedule", "Bonepaw's pool table gives 20 and 25 cards (levels 3-5). Level 4: tier 2."),
                ("power", "Mana Echoes casts a new copy of a random spell either player played from hand this game (the play history), "
                 "targets and choices random; only a scripted spell, never a Quest. A Secret is echoed while the boss's Secret zone has "
                 "room and holds no copy of it, and only one the boss itself played (a card of its own, admitted deck); a Secret of the "
                 "run player's would be a boss-generated Secret outside any closed boss pool, which the generation filter blocks, so it "
                 "is never echoed. Every other spell passes the boss side's generation filter. Offered only while an echo is possible.")]),
    dict(n="06", key="dazzik", page="Dazzik_%22Hellscream%22", name='Dazzik "Hellscream"', tier="2", level=4, archetype="control",
         flags=["start"], total="25", extend=True, fillers=[], ids={'"Scourgelord" Dazzik': "DALA_BOSS_06dk"},
         start=R([("bossDeckAdd", ["DALA_BOSS_06dk"])]),
         notes=[("schedule", "Dazzik's pool table gives 25 cards (levels 4-5). Level 4: tier 2."),
                ("start", "\"Scourgelord\" Dazzik (DALA_BOSS_06dk) is the boss card of the Heroic pool, a hero card like Scourgelord "
                 "Garrosh: played, it replaces Dazzik's hero and power (Bladestorm, ICC_834h, the card's own hero power) and equips "
                 "Shadowmourne; his Health and damage carry over and the card's 10 Armor is gained. The Normal pool and its second copies "
                 "fill the 30 cards before the Heroic pool is reached, so one copy is shuffled into his deck after the mulligan (the "
                 "row's start block), as family 10 does for Grubb's Chuck. The inventory's note (a phased Dazzik) does not match the "
                 "wiki, which lists the card as a deck card: it is not a form of the row.")]),
    dict(n="07", key="belnaara", page="Flight_Master_Belnaara", tier="3", level=6, archetype="tempo",
         flags=["passive", "forms", "heroImmune"], quantity=True, cards=["DALA_BOSS_07e"],
         forms=[R([("power", "DALA_BOSS_07p2"), ("at", R([("script", True)])), ("keepExcess", True)])],
         notes=[("schedule", "The page's note: the deck is the level-6 deck of Chapter 5 (levels 4-5: five cards fewer). Level 6: tier 3."),
                ("deck", "Avian Watcher (KAR_037, \"Battlecry: If you control a Secret, gain +1/+1 and Taunt\") is not admitted in a boss "
                 "deck (its Secret condition has no boss row); a second copy of a single of the list takes its place."),
                ("power", "Take Flight! counts the damage her hero takes (Health lost; Armor absorbs) down from 15 on the power; at 0 "
                 "she takes flight: her power becomes Flying! (form 1, a script form: the hero, its Health and Armor stay) and her hero is "
                 "Immune (Flying!, DALA_BOSS_07e). Flying! counts 2 down at the start of each of her turns; at 0 she lands: Immune ends "
                 "and Take Flight! returns with a fresh count (form 0). The client text leaves the turn count open: her own turns.")]),
    dict(n="08", key="applebough", page="Applebough", tier="2", level=3, archetype="midrange", flags=[], total="15; 20",
         extend=True, fillers=[],
         notes=[("schedule", "Applebough's pool table gives 15 and 20 cards (levels 2-3). Level 3: tier 2."),
                ("power", "Apple Toss deals 1 damage to the enemy (hero or minion) with the lowest Health, a tie broken by the game RNG; "
                 "while its target dies it repeats on the next lowest (at most 10 times). A hero power: no Spell Damage.")]),
    dict(n="09", key="oshi", page="Archivist_Oshi", tier="3", level=6, archetype="control", flags=[], quantity=True,
         notes=[("schedule", "The page's note: the deck is the level-6 deck (levels 4-5: five cards fewer). Level 6: tier 3.")]),
    dict(n="10", key="mo", page="Mo_Eniwhiskers", tier="2", level=4, archetype="tempo", flags=["passive"], total="25",
         extend=True, fillers=[],
         notes=[("schedule", "Mo Eniwhiskers's pool table gives 25 cards (levels 4-5). Level 4: tier 2."),
                ("power", "Street Smarts: 'a Coin' is The Coin (GAME_005) or Counterfeit Coin (CFM_630), the cards whose text is "
                 "'Gain 1 Mana Crystal this turn only.'")]),
    dict(n="11", key="noz", page="Noz_Timbertail", tier="3", level=6, archetype="tempo", flags=["passive", "bothPlayers"], quantity=True,
         cards=["DALA_BOSS_11e"], replacements={"Ogre Ninja": "Stranglethorn Tiger", "Mogor the Ogre": "Boulderfist Ogre"},
         notes=[("deck", "Ogre Ninja and Mogor the Ogre (wrong-target Ogres no phase 8 family scripts) are replaced as family 10 does: "
                 "Stranglethorn Tiger (a Stealthed 5/5, for Backstabber) and Boulderfist Ogre."),
                ("schedule", "Noz Timbertail's page names no level; its quantity list is a 30-card deck like the level-6 lists of the "
                 "other Heist pages. Level 6: tier 3."),
                ("power", "Backstabber answers every Stealthed minion summoned on either side (the client text says 'a'), +1 Attack "
                 "(Backstabby, DALA_BOSS_11e).")]),
    dict(n="12", key="cravitz", page="Cravitz_Lorent", tier="2", level=4, archetype="midrange", flags=["bothPlayers"], total="25",
         extend=True, fillers=[],
         notes=[("schedule", "Cravitz Lorent's pool table gives 25 cards (levels 4-5). Level 4: tier 2.")]),
    dict(n="13", key="albin", page="Albin_Eastoft", tier="3", level=6, archetype="midrange", flags=[], quantity=True,
         notes=[("schedule", "The page's note: the deck is the level-6 deck (level 5: five cards fewer). Level 6: tier 3."),
                ("power", "Slice and Dice picks three different minions of both boards (all of them when fewer), then deals each "
                 "1 to 3 damage, each amount by the game RNG.")]),
    # Step 9 (lane R): attackRedirect, the Waitress's wrong-target roll (DALA_861 Ogreish excludes it).
    dict(n="14", key="tierra", page="Tierra_Blythe", tier="2", level=3, archetype="aggro", flags=["attackRedirect"], total="15; 20",
         extend=True, fillers=[], ids={"Dark Iron Bouncer": "BRMA01_3"}, cards=["DALA_BOSS_14t"],
         notes=[("schedule", "Tierra Blythe's pool table gives 15 and 20 cards (levels 2-3). Level 3: tier 2."),
                ("deck", "Dark Iron Bouncer (BRMA01_3, the Blackrock Mountain adventure's \"Always wins Brawls.\", the boss card of her "
                 "Normal pool) has no script in this engine (its rule changes Brawl's survivor); it is left out and the deck filled."),
                ("power", "\"Kind\" Waitress: an enemy attacking it has a 50% chance to attack another character of the Waitress's "
                 "side (the wrong-target rule of Ogre Warmaul and Anomaly - Ogreish, Actions.EncounterRules.WrongTarget50).")]),
    dict(n="15", key="nici", page="Moon_Priestess_Nici", tier="2", level=3, archetype="control", flags=["passive", "bothPlayers"],
         total="15; 20", extend=True, fillers=[],
         notes=[("schedule", "Moon Priestess Nici's pool table gives 15 and 20 cards (levels 2-3). Level 3: tier 2."),
                ("power", "Blessing of Elune doubles all healing of both players (the ALL_HEALING_DOUBLE controller aura Crystalsmith "
                 "Kangor uses; with Auchenai Soulpriest or Bwonsamdi's Covenant the converted damage is the doubled amount, as Prophet "
                 "Velen's doubling). The page's note on the AI (it avoids the player's hero after converting healing to damage) is the "
                 "original AI's, not a rule.")]),
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
            if row.get("quantity") and sum(e["count"] for e in entries) != 30: raise SystemExit("b%s.txt %s: not 30" % (n, label))
            out_lists.append(R([("label", label), ("cards", entries)]))
        b = R()
        b["id"] = "dala_%s_%s" % (n, row["key"])
        b["name"] = name
        b["provenance"] = "DALA"
        b["family"] = "14"
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
        if row.get("start"): b["start"] = row["start"]
        if row.get("forms"): b["forms"] = row["forms"]
        if row.get("extend"): b["extend"] = ["Heroic"]
        elif row.get("quantity"): b["extend"] = ["Heroic"]
        if row.get("replacements"): b["replacements"] = R(sorted(row["replacements"].items()))
        if row.get("fillers"): b["fillers"] = row["fillers"]
        notes = []
        notes.append(R([("kind", "deck"), ("note", Q_DECK if row.get("quantity") else pool_deck(row["total"]))]))
        for kind, note in row["notes"]: notes.append(R([("kind", kind), ("note", note)]))
        for kind, note in row.get("subs", []): notes.append(R([("kind", kind), ("note", note)]))
        b["adaptations"] = notes
        bosses.append(b)
    doc = R()
    doc["schemaVersion"] = 1
    doc["family"] = "14"
    doc["description"] = ("Dalaran Heist bosses A (phase 8 family 14): the first fifteen encounters of The Dalaran Heist (DALA_BOSS_01 to 15: "
        "Chomper, Awilo, The Great Akazamzarak, Kaye Toogie, The Amazing \"Bonepaw\", Dazzik \"Hellscream\", Flight Master Belnaara, "
        "Applebough, Archivist Oshi, Mo Eniwhiskers, Noz Timbertail, Cravitz Lorent, Albin Eastoft, Tierra Blythe, Moon Priestess Nici) "
        "and their boss-only cards (the four magical feats, \"Scourgelord\" Dazzik, Flying!, \"Kind\" Waitress). Boss deck lists are "
        "transcribed from hearthstone.wiki.gg boss pages (read " + READ + "), by card name; tools/dungeon-run-catalog (step 'encounters') "
        "resolves the names against facts/violet-hold-2026-09.json, checks every card and writes data/dungeon-run/encounters-v1.json. "
        + LEVELS)
    doc["method"] = ("Each page's wikitext (?action=raw) was read and its deck table transcribed by verify/tools/gen/p8f14/make_f14.py: "
        "rowspan- and colspan-aware, column by column in table order, Normal and Heroic separately. A 'Possible cards' pool lists each "
        "card once; a 'Quantity' table keeps its counts (each such list sums to 30). The wiki's disambiguated link names ('Shatter (card) "
        "(card)') are written as the card names; the boss-only cards carry their id.")
    doc["bosses"] = bosses
    doc["rejected"] = []
    text = json.dumps(doc, indent=1, ensure_ascii=False) + "\n"
    open(out, "w", encoding="utf-8", newline="\n").write(text)
    print("wrote %s: %d rows" % (out, len(bosses)))

main(sys.argv[1] if len(sys.argv) > 1 else os.path.join(V, ".out", "tmp", "wiki"),
     sys.argv[2] if len(sys.argv) > 2 else os.path.join(REPO, "data", "dungeon-run", "reference", "encounters", "F14.json"))
