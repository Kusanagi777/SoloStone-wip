# Phase 8 family 17 (Dalaran Heist bosses D): writes data/dungeon-run/reference/encounters/F17.json from the fourteen
# hearthstone.wiki.gg boss pages (wikitext, `curl -sS 'https://hearthstone.wiki.gg/wiki/<Page>?action=raw'`, read 2026-11-14)
# and the rows' hand-written fields below. The pages are not in git: save them as <pages>/bNN.txt (NN = the boss number,
# 46..59) and pass that folder. The deck tables are read rowspan- and colspan-aware, column by column, in table order (the
# table reader is family 14's, verify/tools/gen/p8f14/make_f14.py, copied here); a "Possible cards" pool lists each card once
# (count null), a "Quantity" table keeps its counts.
# Usage: python3 -I verify/tools/gen/p8f17/make_f17.py [<pages folder> [<out file>]]   (LF, no BOM; deterministic)
# (defaults: verify/.out/tmp/wiki and data/dungeon-run/reference/encounters/F17.json of the repository this file sits in)
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

LEVELS = ("Level: the Heist boss pages have no level table and the hero cards print 30 Health, so the level is read from the page "
          "(family 14's rule): a chapter's final boss is its eighth fight (level 8); a 'Possible cards' pool's 'Total cards' column "
          "gives the deck size, which grows with the level (10 cards at level 1, 15 at 2, 20 at 3, 25 at 4 and 5, 30 from 6), and the "
          "row takes the lowest level that holds it; a counted table of 30 cards with no stated level is level 6. Tier: levels 1-2 "
          "tier 1, 3-4 tier 2, 5-6 tier 3, 7-8 tier 4 (the encounter model's table; a Heist chapter final is a combat encounter here, as "
          "family 15's Jepetto: the Heist's major is its finale, family 18). The fight's Health is the act curve's, not the printed 30.")

def tier_note(text, level, tier):
    return "%s Level %d: tier %d." % (text, level, tier)

Q_DECK = ("The wiki lists the boss's deck with its quantities (30 cards, Normal and Heroic): the Normal list is the deck; an unusable "
          "card is replaced as recorded.")

def pool_deck(total):
    return ("The wiki lists pools of possible cards (Normal and Heroic), not decks: the boss plays " + total + " of them. A combat tier "
            "fights with 30: the Normal pool in table order, a second copy of each non-Legendary card, then the Heroic pool (the "
            "encounters step records each added card).")

AUTO = ("The wiki marks %s as auto-cast, so it is cast by rule, not by a choice of the boss's AI (DungeonRunEncounters.AutoCast, "
        "family 09's rule): at the start of the boss's turn, when it can pay the Cost and %s, the Cost is paid and the power resolves. "
        "The engine never offers it as an option (it is registered with the passive powers).")
HIDDEN = ("%s is not auto-cast on the wiki, but its worth lies in the run player's hidden %s, which the boss's fair view cannot see "
          "(there the zone is masked), so the AI would never choose it: it is cast by rule (DungeonRunEncounters.AutoCast, family 09's "
          "rule for powers whose worth the AI's evaluation cannot see) at the start of the boss's turn, when it can pay the Cost and %s.")

R = collections.OrderedDict
ROWS = [
    dict(n="46", key="ichoron", page="Ichoron", tier="3", level=6, archetype="control", flags=[],
         notes=[("schedule", tier_note("Ichoron's page: Chapter 2, a counted table of 30 cards, no stated level.", 6, 3)),
                ("power", "Protective Bubble gives a friendly minion Divine Shield (the tag, as Hand of Protection); offered only on a "
                 "minion of the boss without one. The Heroic power (all friendly minions, auto-cast) is not used.")]),
    dict(n="47", key="lavanthor", page="Lavanthor", tier="3", level=6, archetype="midrange", flags=[], cards=["DALA_BOSS_47t"],
         notes=[("schedule", tier_note("Lavanthor's page: Chapter 2, a counted table of 30 cards, no stated level.", 6, 3)),
                ("deck", "Xavius is not in the client's card data (no card of that name): the encounters step fills its slot from the "
                 "Heroic list."),
                ("power", "Lava Belch summons a 0/3 Molten Rock (DALA_BOSS_47t): at the end of its player's turn it deals 1 damage to all "
                 "characters (both heroes and every minion, itself included).")]),
    dict(n="48", key="zuramat", page="Zuramat_the_Obliterator_(boss)", tier="4", level=8, archetype="control", flags=["start", "bothPlayers"],
         ids={"Void Shift": "DALA_BOSS_48t"}, cards=["DALA_BOSS_48e"],
         start=R([("playerHandAdd", ["DALA_BOSS_48t", "DALA_BOSS_48t"])]),
         notes=[("schedule", tier_note("Zuramat is \"the final boss that can be encountered in Chapter 2\" (the chapter's eighth fight).", 8, 4)),
                ("power", "To The Void banishes a minion: it leaves its board with no death and no Deathrattle, held by its Voided "
                 "enchantment (DALA_BOSS_48e, \"In the Void.\") in family 10's set-aside store (EncounterRules.Banish). The wiki: "
                 "banished minions go to a list of their own, not the list of deaths. A run shrine is never a legal target."),
                ("power", "Void Shift (DALA_BOSS_48t, \"Destroy all minions. Summon all minions from the Void.\"): every minion of both "
                 "boards is destroyed, then a new copy of each banished minion is summoned for the caster, in banishing order, while its "
                 "board has room; the wiki: \"After using Void Shift, all minions in the list of banished are removed\" (the Void is "
                 "empty). A copy is summoned as the list of deaths resummons (fresh, without the damage or enchantments it had)."),
                ("start", "The wiki: \"The player is given two copies of Void Shift at Start of Game in Normal mode\" (the dialogue has a "
                 "line for the player casting it). Both are given to the run player's hand after the mulligan (the row's start block); "
                 "never in the run deck.")]),
    dict(n="49", key="cyanigosa", page="Cyanigosa", tier="4", level=8, archetype="control", flags=["passive"],
         notes=[("schedule", tier_note("Cyanigosa is \"the final boss that can be encountered in Chapter 2\" (the chapter's eighth fight).", 8, 4)),
                ("power", "Uncontrollable Energy: after the boss casts a spell, 1 damage to each enemy (the run player's hero and "
                 "minions); a hero power's damage, so no Spell Damage.")]),
    dict(n="50", key="nozari", page="Nozari_(boss)", tier="3", level=6, archetype="midrange", flags=["passive"],
         notes=[("schedule", tier_note("Nozari's page names no chapter or level; a counted table of 30 cards.", 6, 3)),
                ("power", AUTO % ("Sand Breath (2)", "an enemy can be hit") + " It deals 2 damage to a random enemy: the run player's hero "
                 "or a minion an effect can pick (the game RNG); a hero power's damage, so no Spell Damage.")]),
    dict(n="51", key="millificent", page="Millificent_Manastorm", tier="3", level=6, archetype="aggro", flags=[],
         ids={"Thorium Chicken": "DALA_BOSS_51t2"}, cards=["DALA_BOSS_51t"],
         notes=[("schedule", tier_note("Millificent Manastorm's page: Chapter 2, a counted table of 30 cards, no stated level.", 6, 3)),
                ("power", "Tinker summons a 0/4 Elementium Squirrel Bomb (DALA_BOSS_51t): at the start of its player's turn it is "
                 "destroyed and deals 2 damage to all enemies. Thorium Chicken (DALA_BOSS_51t2, the boss card of her deck) deals 3 damage "
                 "to the enemy hero after the enemy plays a card.")]),
    dict(n="52", key="sinclari", page="Lieutenant_Sinclari", tier="3", level=6, archetype="midrange", flags=[],
         ids={"Kirin Tor Guard": "DALA_503"}, cards=["DALA_BOSS_52e"],
         notes=[("schedule", tier_note("Lieutenant Sinclari's page: Chapter 2, a counted table of 30 cards, no stated level.", 6, 3)),
                ("power", "Hold the Gates! gives any minion +2 Health and Taunt (Hold Fast!, DALA_BOSS_52e). Kirin Tor Guard (DALA_503, "
                 "four in her deck) has +1 Attack for each other friendly Kirin Tor Guard.")]),
    dict(n="53", key="deryl", page="Dancin%27_Deryl", name="Dancin' Deryl", tier="2", level=4, archetype="aggro", flags=[], total="25",
         cards=["DALA_BOSS_53e"],
         notes=[("schedule", tier_note("Dancin' Deryl's page: Chapter 4, a pool table of 25 cards (levels 4-5).", 4, 2)),
                ("power", "Boogie Woogie gives a friendly minion Boogie Beyond (DALA_BOSS_53e, \"Deathrattle: Resummon this minion.\", "
                 "Ancestral Spirit's rule: a new copy is summoned for the minion's controller).")]),
    dict(n="54", key="zibb", page="Locksmith_Zibb", tier="2", level=4, archetype="tempo", flags=["passive", "bothPlayers"], total="25",
         notes=[("schedule", tier_note("Locksmith Zibb's page: Chapter 3, a pool table of 25 cards (levels 4-5).", 4, 2)),
                ("power", "Spell Lock, \"Spells Overload for their cost.\": the text names no player, so it holds for both (the lesson of "
                 "earlier families: an unqualified \"Spells\" is both players'). Each spell a player plays (cast from hand, not cancelled) "
                 "Overloads that player for the Cost it was played for (the Cost it last had in hand), on top of its own Overload, through the engine's Overload bookkeeping "
                 "and event (Likkim and Lava Shock of the deck see it; the engine's Tunnel Trogg answers only a card with printed "
                 "Overload, so Spell Lock gives each Tunnel Trogg of that player, not silenced, its Attack for the amount itself). A "
                 "spell cast by another effect is not played and "
                 "brings none.")]),
    dict(n="55", key="hannigan", page="Captain_Hannigan", tier="4", level=8, archetype="control", flags=["passive"], cards=["DALA_503"],
         notes=[("schedule", tier_note("Captain Hannigan is \"the final boss that can be encountered in Chapter 3\" (the chapter's eighth fight).", 8, 4)),
                ("power", AUTO % ("Raise the Alarm (2)", "its board has room") + " It summons a 1/4 Kirin Tor Guard (DALA_503: +1 Attack "
                 "for each other friendly Kirin Tor Guard).")]),
    dict(n="56", key="wagtoggle", page="Queen_Wagtoggle", tier="4", level=8, archetype="tempo", flags=["passive", "start", "hiddenZone"],
         ids={"Rival Accomplice": "DALA_BOSS_56t"}, cards=["DALA_BOSS_56e"],
         start=R([("bossDeckAdd", ["DALA_BOSS_56t"])]),
         notes=[("schedule", tier_note("Queen Wagtoggle is \"the final boss that can be encountered in Chapter 1\" (the chapter's eighth fight).", 8, 4)),
                ("power", HIDDEN % ("Bribery (3)", "deck", "its board has room and the run player's deck holds a card (a count, public)")
                 + " Tad's Pole's rule (family 09, DungeonRunEncounters.PoleCatch): a random minion of the run player's deck changes "
                 "sides and is summoned for the boss, a public summon; nothing when the deck holds no minion. It is Bribed (DALA_BOSS_56e, "
                 "\"Deathrattle: Return this to your opponent's hand.\"): when it dies a new copy goes to the hand of its controller's "
                 "opponent."),
                ("start", "Rival Accomplice (DALA_BOSS_56t, a vanilla 4/4) is the boss card of the Heroic list only; one copy is shuffled "
                 "into her deck after the mulligan (the row's start block), so the card is met in this mode, as family 14 does for "
                 "\"Scourgelord\" Dazzik.")]),
    dict(n="57", key="gallywix", page="Trade_Prince_Gallywix_(boss)", tier="4", level=8, archetype="tempo", flags=["passive"],
         cards=["GVG_028t"], replacements={"Fel Reaver": "DALA_502"},
         notes=[("schedule", tier_note("Trade Prince Gallywix is \"the final boss that can be encountered in Chapter 1\" (the chapter's eighth fight).", 8, 4)),
                ("deck", "Fel Reaver (two in the list) is a collectible card the engine does not script. Curated replacement: two "
                 "Infuriated Bankers (DALA_502, \"Whenever a Coin is played, gain +1/+1.\"), the Dalaran Bank's own card (the wiki lists it "
                 "for the bank's Gold Elemental; Gallywix is the bank's final boss) and one that answers the Coins Greed is Good hands "
                 "out; so the bank card is met in this mode."),
                ("power", "Greed is Good: the first card the run player plays in a turn puts a new copy of that card in the boss's hand "
                 "and a Gallywix's Coin (GVG_028t, the card the wiki names: \"Won't trigger Gallywix\") in the run player's; Gallywix's "
                 "Coin itself buys nothing (the next card is then the first). With the boss's hand full nothing is bought that time.")]),
    dict(n="58", key="flark", page="Boommaster_Flark_(Dalaran_boss)", tier="4", level=8, archetype="midrange",
         flags=["start", "boardStart", "position"], cards=["DALA_BOSS_58e"],
         start=R([("playerBoard", ["DALA_BOSS_58t", "LOEA10_3", "DALA_BOSS_58t2"])]),
         notes=[("schedule", tier_note("Boommaster Flark is \"the final boss that can be encountered in Chapter 4\" (the chapter's eighth fight).", 8, 4)),
                ("power", "Blast Powder coats a minion (DALA_BOSS_58e): when it takes damage it is destroyed (Divine Shield prevents the "
                 "damage, so it is not); offered only on a minion not coated yet."),
                ("start", "The wiki: \"The player starts with 1 The Wire, 1 Murloc Tinyfin and 1 The Other Wire respectively on their "
                 "board.\" They land on the run player's board in that order after the mulligan (the row's start block; a run shrine "
                 "stays left of them). The Wire and The Other Wire (DALA_BOSS_58t, 58t2) are 3/3 permanents: \"While wires are touching, "
                 "take 5 damage at the start of your turn.\" Both carry the text; a touching pair deals 5 damage once (counted by its "
                 "left wire), as the client's single Wire Watcher player enchantment (DALA_BOSS_58e2) suggests. The Murloc Tinyfin is "
                 "LOEA10_3.")]),
    dict(n="59", key="goya", page="Madam_Goya_(boss)", tier="4", level=8, archetype="midrange", flags=["passive", "hiddenZone"],
         ids={"Lotus Bruiser": "DALA_BOSS_59t", "Fruit Slasher": "DALA_BOSS_59t2"}, cards=["DALA_BOSS_59t3", "DALA_BOSS_59e"],
         notes=[("schedule", tier_note("Madam Goya is \"the final boss that can be encountered in Chapter 4\" (the chapter's eighth fight).", 8, 4)),
                ("power", HIDDEN % ("Blackmail (2)", "hand", "the run player holds a card (a count, public)") + " A random card of the run "
                 "player's hand goes to that player's set-aside zone (the hand escrow: what it holds stays on it) and an Extortion "
                 "(DALA_BOSS_59t3, \"Return your '{0}' to your hand.\") joins that hand; played, it returns that card itself (the wiki: "
                 "\"The card replaced by Extortion retains all increment attribute buffs after being given back to the player\"). The "
                 "boss never learns the card."),
                ("deck", "Lotus Bruiser (DALA_BOSS_59t) gains +2/+2 whenever an Extortion is played (by either player); Fruit Slasher "
                 "(DALA_BOSS_59t2) deals 4 damage to an enemy and, with Combo, 1 to all other enemies.")]),
]

def main(pages, out):
    from_facts = json.load(open(os.path.join(REPO, "facts", "violet-hold-2026-09.json"), encoding="utf-8"))["cards"]
    by_id = {c["id"]: c for c in from_facts}
    bosses = []
    for row in ROWS:
        n = row["n"]
        text = open(os.path.join(pages, "b%s.txt" % n), encoding="utf-8").read()
        if "dbfId=%d" % by_id["DALA_BOSS_%sh" % n]["dbf"] not in text.split("\n", 2)[1]: raise SystemExit("b%s.txt is not the boss page" % n)
        lists, total = deck_table(text)
        if lists is None: raise SystemExit("no deck table in b%s.txt" % n)
        pool = row.get("total") is not None
        if pool and total != row["total"]: raise SystemExit("b%s.txt: total %r, expected %r" % (n, total, row["total"]))
        if not pool and total is not None: raise SystemExit("b%s.txt: a pool table" % n)
        hero, power = "DALA_BOSS_%sh" % n, "DALA_BOSS_%sp" % n
        name = row.get("name") or by_id[hero]["name"]
        if by_id[hero]["name"] != name: raise SystemExit("%s is %r, not %r" % (hero, by_id[hero]["name"], name))
        out_lists = []
        for label in ("Normal", "Heroic"):
            entries = []
            for cname, cnt in lists[label]:
                e = R([("name", cname)])
                if cname in row.get("ids", {}): e["id"] = row["ids"][cname]
                if cnt is not None: e["count"] = cnt
                entries.append(e)
            if not pool and sum(e["count"] for e in entries) != 30: raise SystemExit("b%s.txt %s: not 30" % (n, label))
            out_lists.append(R([("label", label), ("cards", entries)]))
        b = R()
        b["id"] = "dala_%s_%s" % (n, row["key"])
        b["name"] = name
        b["provenance"] = "DALA"
        b["family"] = "17"
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
        b["extend"] = ["Heroic"]
        if row.get("replacements"): b["replacements"] = R(sorted(row["replacements"].items()))
        if row.get("fillers"): b["fillers"] = row["fillers"]
        notes = [R([("kind", "deck"), ("note", pool_deck(row["total"]) if pool else Q_DECK)])]
        for kind, note in row["notes"]: notes.append(R([("kind", kind), ("note", note)]))
        b["adaptations"] = notes
        bosses.append(b)
    doc = R()
    doc["schemaVersion"] = 1
    doc["family"] = "17"
    doc["description"] = ("Dalaran Heist bosses D (phase 8 family 17): fourteen encounters of The Dalaran Heist (DALA_BOSS_46 to 59: "
        "Ichoron, Lavanthor, Zuramat the Obliterator, Cyanigosa, Nozari, Millificent Manastorm, Lieutenant Sinclari, Dancin' Deryl, "
        "Locksmith Zibb, Captain Hannigan, Queen Wagtoggle, Trade Prince Gallywix, Boommaster Flark, Madam Goya) and their boss cards "
        "(Molten Rock, Void Shift, Elementium Squirrel Bomb, Thorium Chicken, Kirin Tor Guard, Rival Accomplice, The Wire, The Other "
        "Wire, Lotus Bruiser, Fruit Slasher, Extortion; Infuriated Banker, the bank card of family 16's Gold Elemental, is scripted "
        "here too). Boss deck lists are transcribed from hearthstone.wiki.gg boss pages (read " + READ + "), by card name; "
        "tools/dungeon-run-catalog (step 'encounters') resolves the names against facts/violet-hold-2026-09.json, checks every card "
        "and writes data/dungeon-run/encounters-v1.json. " + LEVELS)
    doc["method"] = ("Each page's wikitext (?action=raw) was read and its deck table transcribed by verify/tools/gen/p8f17/make_f17.py: "
        "rowspan- and colspan-aware, column by column in table order, Normal and Heroic separately. A 'Possible cards' pool lists each "
        "card once; a 'Quantity' table keeps its counts (each such list sums to 30). The boss-only cards carry their id. The boss "
        "pages are the '(boss)' or '(Dalaran boss)' pages where a collectible card has the plain name.")
    doc["bosses"] = bosses
    doc["rejected"] = []
    text = json.dumps(doc, indent=1, ensure_ascii=False) + "\n"
    open(out, "w", encoding="utf-8", newline="\n").write(text)
    print("wrote %s: %d rows" % (out, len(bosses)))

main(sys.argv[1] if len(sys.argv) > 1 else os.path.join(V, ".out", "tmp", "wiki"),
     sys.argv[2] if len(sys.argv) > 2 else os.path.join(REPO, "data", "dungeon-run", "reference", "encounters", "F17.json"))
