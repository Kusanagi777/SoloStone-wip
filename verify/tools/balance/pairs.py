# Step 9 C6 (handoff/phase9/plan.md section 1 C): the pairwise covering array of the incompatibility prevention, for the
# bosspilot mode pairs9 (verify/suites/p9/c-bosspilot-pairs9, `bp.sh pairs9 <chunk> <k>`).
#
# Factors (each value read from the shipped data):
#   twist     the eight act twists (schedule-v1.json rules, kind twist, DALA_830 The Finale aside: it is the finale's own), or "-"
#   anomaly   every admitted anomaly (act and run scope), or "-"
#   modifier  every schedule modifier, or "-"
#   boss      one encounter row per distinct boss flag set (encounters-v1.json "flags"): the first row by id, a non-finale row
#             when the set has one
#   class     the hero-switch classes: MAGE (a plain class row), MAGE_ROGUE (Reno: a relay hero of his own is skipped),
#             GILA_600h (Darius: Hagatha's GILA_600h2 is skipped; The Carts names him), GILA_500h3 (Tess: GILA_500h4 skipped),
#             DALA_George (excludeBosses loot_15_george_karl)
# A pair of values is a declared exclusion when the data says the run never holds both, with the data's reason:
#   rule x rule      either lists the other in incompatibleRules (exclusionReasons of the listing rule)
#   rule x boss      a boss flag in the rule's incompatibleFlags, or the boss's incompatibleRules names the rule
#   rule x modifier  the rule's incompatibleModifiers
#   rule x class     the rule's incompatibleClasses
#   modifier x boss  on a stamp-4 run only a row without a v4 block keeps its act-wide ban (incompatibleModifiers); a row with
#                    one neutralises the modifier's conflicting knobs for its fight, so the pair is played
#   boss x class     the hero row's excludeBosses (the same character)
# Every other pair is covered by at least one case (one real game); no case holds a declared pair. The cases are built
# greedily in a fixed order (the factor pair with most uncovered pairs first, then the value covering most new pairs, ties to
# the first value), so the output is the same on every run. The declared hot spots (plan C6) are listed as triples with every
# value of the remaining factors, each resolved by its declared pair.
#
# Writes verify/fixtures/p9/pairs9.txt (one case per line: chunk, case number, twist, anomaly, modifier, boss, class) and
# verify/fixtures/p9/pairs9-declared.txt (every declared pair with its reason, then the hot-spot triples).
#   python3 -I verify/tools/balance/pairs.py [<output folder>]
import io, json, os, sys

V = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))  # verify/, from verify/tools/balance/
repo = os.path.dirname(V)
def load(rel): return json.load(io.open(os.path.join(repo, rel), encoding='utf-8'))
schedule = load('data/dungeon-run/schedule-v1.json')
encounters = load('data/dungeon-run/encounters-v1.json')['encounters']
run = load('data/dungeon-run/run-v1.json')

rules = {r['id']: r for r in schedule['rules']}
NONE = '-'
twists = [r['id'] for r in schedule['rules'] if r['kind'] == 'twist' and r.get('status') == 'admitted' and r['id'] != 'DALA_830']
anomalies = [r['id'] for r in schedule['rules'] if r['kind'] == 'anomaly' and r.get('status') == 'admitted']
modifiers = [m['id'] for m in schedule['modifiers']]
rows = {r['id']: r for r in encounters}
sets = {}
for r in sorted(encounters, key=lambda r: r['id']):
    sets.setdefault(tuple(sorted(r.get('flags', []))), []).append(r)
bosses = []
for key in sorted(sets):
    group = sets[key]
    pick = next((r for r in group if r.get('tier') != 'finale'), group[0])
    bosses.append(pick['id'])
classes = ['MAGE', 'MAGE_ROGUE', 'GILA_600h', 'GILA_500h3', 'DALA_George']
heroes = {h['id']: h for h in run['heroRows']}
heroes.update({c['id']: c for c in run['catalogClasses']})
for c in classes:
    if c not in heroes and c not in [x['id'] for x in run['classes']]: raise SystemExit('unknown class row ' + c)
FACTORS = [('twist', [NONE] + twists), ('anomaly', [NONE] + anomalies), ('modifier', [NONE] + modifiers), ('boss', bosses), ('class', classes)]
NAMES = [f[0] for f in FACTORS]

def why(rule, entry, fallback):
    return rule.get('exclusionReasons', {}).get(entry) or fallback
def rule_rule(a, b):
    ra, rb = rules[a], rules[b]
    if b in ra.get('incompatibleRules', []): return a + ' lists ' + b + ': ' + why(ra, b, why(rb, a, 'incompatibleRules'))
    if a in rb.get('incompatibleRules', []): return b + ' lists ' + a + ': ' + why(rb, a, why(ra, b, 'incompatibleRules'))
    return None
def rule_boss(a, boss):
    r, b = rules[a], rows[boss]
    if a in b.get('incompatibleRules', []): return boss + "'s incompatibleRules names " + a
    for flag in sorted(b.get('flags', [])):
        if flag in r.get('incompatibleFlags', []): return 'flag ' + flag + ': ' + why(r, flag, 'incompatibleFlags')
    return None
def rule_modifier(a, m):
    r = rules[a]
    return 'incompatibleModifiers: ' + why(r, m, m) if m in r.get('incompatibleModifiers', []) else None
def rule_class(a, c):
    r = rules[a]
    return 'incompatibleClasses: ' + why(r, c, c) if c in r.get('incompatibleClasses', []) else None
def modifier_boss(m, boss):
    b = rows[boss]
    if b.get('v4') is not None: return None  # neutralised per fight on stamp 4 (or nothing to neutralise): played
    if m in (b.get('incompatibleModifiers') or []): return 'act-wide ban (no v4 block): ' + (b.get('modifierReasons') or {}).get(m, 'incompatibleModifiers')
    return None
def boss_class(boss, c):
    return 'excludeBosses (the same character)' if boss in heroes.get(c, {}).get('excludeBosses', []) else None

def declared(i, a, j, b):
    """The reason values a (factor i) and b (factor j) are never held together, or None."""
    if a == NONE or b == NONE: return None
    if i > j: i, a, j, b = j, b, i, a
    ni, nj = NAMES[i], NAMES[j]
    if ni in ('twist', 'anomaly') and nj in ('twist', 'anomaly'): return rule_rule(a, b)
    if ni in ('twist', 'anomaly') and nj == 'modifier': return rule_modifier(a, b)
    if ni in ('twist', 'anomaly') and nj == 'boss': return rule_boss(a, b)
    if ni in ('twist', 'anomaly') and nj == 'class': return rule_class(a, b)
    if ni == 'modifier' and nj == 'boss': return modifier_boss(a, b)
    if ni == 'boss' and nj == 'class': return boss_class(a, b)
    return None

n = len(FACTORS)
uncovered = set()
excluded = []
for i in range(n):
    for j in range(i + 1, n):
        for a in FACTORS[i][1]:
            for b in FACTORS[j][1]:
                r = declared(i, a, j, b)
                if r: excluded.append((NAMES[i], a, NAMES[j], b, r))
                else: uncovered.add((i, a, j, b))
total = len(uncovered)
cases = []
def fits(row, k, v):
    return all(row[m] is None or declared(m, row[m], k, v) is None for m in range(n) if m != k)
def gain(row, k, v):
    g = 0
    for m in range(n):
        if m == k or row[m] is None: continue
        key = (m, row[m], k, v) if m < k else (k, v, m, row[m])
        if key in uncovered: g += 1
    return g
while uncovered:
    # the factor pair with the most uncovered pairs, and its first uncovered pair (in factor value order)
    count = {}
    for (i, a, j, b) in uncovered: count[(i, j)] = count.get((i, j), 0) + 1
    i, j = min(count, key=lambda p: (-count[p], p))
    order = {f: {v: x for x, v in enumerate(FACTORS[f][1])} for f in range(n)}
    a, b = min(((x[1], x[3]) for x in uncovered if x[0] == i and x[2] == j), key=lambda p: (order[i][p[0]], order[j][p[1]]))
    row = [None] * n
    row[i], row[j] = a, b
    for k in range(n):
        if row[k] is not None: continue
        best = None
        for v in FACTORS[k][1]:
            if not fits(row, k, v): continue
            g = gain(row, k, v)
            if best is None or g > best[0]: best = (g, v)
        if best is None: raise SystemExit('no value of %s fits the case %r' % (NAMES[k], row))
        row[k] = best[1]
    for p in range(n):
        for q in range(p + 1, n):
            uncovered.discard((p, row[p], q, row[q]))
    cases.append(row)

# Chunks: each run of the harness stays well under the bosspilot time limit (a relay finale costs about ten ordinary games).
COST_LIMIT = 90
chunks, cost, chunk = [], 0, 1
for row in cases:
    c = 10 if 'relay' in rows[row[3]].get('flags', []) else 1
    if cost + c > COST_LIMIT: chunk += 1; cost = 0
    cost += c
    chunks.append(chunk)

out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(V, 'fixtures', 'p9')
os.makedirs(out, exist_ok=True)
lines = ['# pairs9 cases (verify/tools/balance/pairs.py): chunk case twist anomaly modifier boss class; "-" = none',
         '# %d cases cover %d allowed pairs of %s; %d pairs are declared exclusions (pairs9-declared.txt); %d chunks'
         % (len(cases), total, ' x '.join('%s %d' % (f[0], len(f[1])) for f in FACTORS), len(excluded), chunks[-1] if chunks else 0)]
for k, row in enumerate(cases):
    lines.append('%d %d %s' % (chunks[k], k + 1, ' '.join(row)))
io.open(os.path.join(out, 'pairs9.txt'), 'w', encoding='utf-8', newline='\n').write('\n'.join(lines) + '\n')

HOT = [('The Stench x Marin\'s Return', 'twist', 'DALA_831', 'anomaly', 'ULDA_719'),
       ('The Coffers x No-Face', 'twist', 'DALA_829', 'anomaly', 'ULDA_706'),
       ('Gorged x the Vault', 'anomaly', 'DALA_859', 'boss', 'loot_52_vault'),
       ('Gorged x Tad', 'anomaly', 'DALA_859', 'boss', 'loot_22_tad'),
       ('The Carts x Hagatha', 'twist', 'DALA_833', 'boss', 'gila_61_hagatha')]
dl = ['# pairs9 declared exclusions (verify/tools/balance/pairs.py): factor value x factor value: reason']
for (ni, a, nj, b, r) in excluded:
    dl.append('%s %s x %s %s: %s' % (ni, a, nj, b, r))
dl.append('# hot-spot triples (plan C6): each holds its declared pair, so every triple is declared by it')
for (label, fi, a, fj, b) in HOT:
    i, j = NAMES.index(fi), NAMES.index(fj)
    if b not in FACTORS[j][1]: raise SystemExit('hot spot %s: %s is not a value of %s (a flag set of its own)' % (label, b, fj))
    r = declared(i, a, j, b)
    if not r: raise SystemExit('hot spot %s is not a declared exclusion: give it real games' % label)
    rest = [k for k in range(n) if k not in (i, j)]
    triples = sum(len(FACTORS[k][1]) for k in rest)
    dl.append('%s (%s x %s): %d triples, declared: %s' % (label, a, b, triples, r))
r720 = rules['ULDA_720']
if 'LOOTA_800' not in r720.get('incompatibleTreasures', []): raise SystemExit('hot spot Battle Scars x LOOTA_800 is not declared')
dl.append('Battle Scars x Potion of Vitality (ULDA_720 x treasure LOOTA_800): every triple, declared: incompatibleTreasures: ' + why(r720, 'LOOTA_800', 'LOOTA_800'))
io.open(os.path.join(out, 'pairs9-declared.txt'), 'w', encoding='utf-8', newline='\n').write('\n'.join(dl) + '\n')
print('wrote %s: %d cases in %d chunks, %d allowed pairs covered, %d declared pairs' % (out, len(cases), chunks[-1] if chunks else 0, total, len(excluded)))
