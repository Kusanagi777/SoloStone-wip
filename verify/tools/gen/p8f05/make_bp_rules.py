# Phase 8 family 05: builds p8f05/suites/c-bosspilot-rules05 from the encounter bosspilot harness
# (p8f01/suites/c-bosspilot-encounter) with two additions for the global rules:
#   - BOSS may name a custom boss (band 1 to 3), so a rule is played against one custom boss beside the imported ones;
#   - with RULE set, a probe watches the game for the rule's own effect (RuleProbe at the end of the file): what the rule
#     places at the start is a failed check when it is missing, and what the rule does during play is counted per game
#     ("seen N" on each line, "rule-seen N" on the last, and the bosses whose games never showed it).
# The sources of truth stay the base harness and this script: rerun it after the base harness changes.
#   python -I verify/tools/gen/p8f05/make_bp_rules.py [<output folder>]
# (ported to verify/: base verify/suites/p8f01/c-bosspilot-encounter; output verify/suites/p8f05/c-bosspilot-rules05 by default)
import io, os, sys
V = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))  # verify/, from verify/tools/gen/<family>/
BASE = V + '/suites/p8f01/c-bosspilot-encounter'
OUT = sys.argv[1] if len(sys.argv) > 1 else V + '/suites/p8f05/c-bosspilot-rules05'

def once(text, old, new):
    if text.count(old) != 1: raise SystemExit('expected one match for %r, found %d' % (old[:70], text.count(old)))
    return text.replace(old, new)

src = io.open(BASE + '/Program.cs', encoding='utf-8', newline='').read()
crlf = '\r\n' in src
src = src.replace('\r\n', '\n')
src = once(src,
'''// A boss with shrine variants plays its games once per variant (the seeds that roll that variant).
''',
'''// A boss with shrine variants plays its games once per variant (the seeds that roll that variant).
// Family 05 variant (p8f05/make_bp_rules.py): BOSS may name a custom boss, and with RULE a probe watches each game for the
// rule's own effect (RuleProbe below): a missing start state is a failed check; each line ends with "seen N", the last line
// with "rule-seen N" and the bosses whose games never showed the rule.
''')
src = once(src,
'''var powerUnused = new List<string>(); var formUnreached = new List<string>();
''',
'''var powerUnused = new List<string>(); var formUnreached = new List<string>();
int ruleSeen = 0; var ruleUnseen = new List<string>();
''')
src = once(src,
'''    int powerUses = 0, formChangesSeen = 0; bool clickable = false;
''',
'''    int powerUses = 0, formChangesSeen = 0, bossSeen = 0; bool clickable = false;
''')
src = once(src,
'''                Check(plan.EncounterId == boss.Id && plan.EncounterDeck, "plan is not the encounter's", problems);
''',
'''                Check(boss.IsEncounter ? plan.EncounterId == boss.Id && plan.EncounterDeck : plan.EncounterId == null && !plan.EncounterDeck, "plan is not the boss's", problems);
''')
src = once(src,
'''                int decisionIndex = 0, actions = 0;
''',
'''                var probe = forcedRule == null ? null : new RuleProbe(forcedRule, game);
                probe?.Start(problems);
                int decisionIndex = 0, actions = 0;
''')
src = once(src,
'''                    recent.Enqueue($"t{game.Turn} {who}: {task}");
''',
'''                    recent.Enqueue($"t{game.Turn} {who}: {task}");
                    probe?.Before(task);
''')
src = once(src,
'''                    else if (!game.Process(task)) throw new Exception($"engine rejected {who} task | " + string.Join(" | ", recent));
                }
''',
'''                    else if (!game.Process(task)) throw new Exception($"engine rejected {who} task | " + string.Join(" | ", recent));
                    probe?.After(task, problems);
                }
''')
src = once(src,
'''                if (forcedRule != null) extra += " rule " + forcedRule;
''',
'''                if (probe != null) { probe.End(problems); bossSeen += probe.Seen; ruleSeen += probe.Seen; extra += " rule " + forcedRule + " seen " + probe.Seen; }
''')
src = once(src,
'''    if (clickable && powerUses == 0) powerUnused.Add(boss.Id);
''',
'''    if (clickable && powerUses == 0) powerUnused.Add(boss.Id);
    if (forcedRule != null && bossSeen == 0) ruleUnseen.Add(boss.Id);
''')
src = once(src,
'''    + (watch == null ? "" : ", watched " + string.Join(" ", watch.Select(id => id + " x" + (watched.TryGetValue(id, out int n) ? n : 0))) + ", most boss Secrets " + mostSecrets));
return faults + failed + SearchTelemetry.SwallowedLeafErrors;''',
'''    + (watch == null ? "" : ", watched " + string.Join(" ", watch.Select(id => id + " x" + (watched.TryGetValue(id, out int n) ? n : 0))) + ", most boss Secrets " + mostSecrets)
    + (forcedRule == null ? "" : ", rule-seen " + ruleSeen + ", rule-unseen " + (ruleUnseen.Count == 0 ? "-" : string.Join(",", ruleUnseen))));
return faults + failed + SearchTelemetry.SwallowedLeafErrors;

// What a global rule visibly does in a game (family 05). Start: what the rule places after both mulligans (a failed check
// when it is missing). Before / After: one probe per rule around every action of either player; Seen counts the rule's own
// effect. End: what must still hold when the game is over.
sealed class RuleProbe
{
    private readonly string rule; private readonly Game game;
    private readonly HashSet<int> known = new HashSet<int>();
    private int handBefore, spellCost = -1, targetDamage, recasts; private bool hadPrison1, hadPrison2;
    private IPlayable played; private ICharacter aimed; private Controller actor;
    public int Seen;
    public RuleProbe(string rule, Game game) { this.rule = rule; this.game = game; }
    private IEnumerable<Minion> Boards => game.Player1.BoardZone.Concat(game.Player2.BoardZone);
    private static bool Has(IPlayable p, string enchantment) => p.AppliedEnchantments?.Any(e => e.Card.Id == enchantment) == true;
    private int Recasts() => game.IdEntityDic.Values.Count(e => e is Spell s && RecastRules.IsRecast(s));
    private void Fail(List<string> problems, bool ok, string what) { if (!ok) problems.Add("rule " + rule + ": " + what); }

    public void Start(List<string> problems)
    {
        Controller p1 = game.Player1, p2 = game.Player2;
        switch (rule)
        {
            case "DALA_829": Fail(problems, p2.BoardZone.Count(m => m.Card.Id == "DALA_829t") == 1 && p1.BoardZone.All(m => m.Card.Id != "DALA_829t"), "the boss does not start with one Cache of Cash"); break;
            case "DALA_832":
                // The run player's first turn has started: a prisoner that costs 0 or 1 is out already.
                hadPrison1 = p1.BoardZone.Any(m => m.Card.Id == "DALA_832t"); hadPrison2 = p2.BoardZone.Any(m => m.Card.Id == "DALA_832t");
                Fail(problems, (hadPrison1 || p1.BoardZone.Count >= 1) && hadPrison2 && EncounterRules.Imprisoned(p2.BoardZone.First(m => m.Card.Id == "DALA_832t")) != null, "each player does not start with a Violet Prison");
                if (!hadPrison1) Seen++;
                break;
            case "DALA_833":
                Fail(problems, EncounterRules.Blockers(p1, DungeonRunEncounters.Carts) == 3 && EncounterRules.Blockers(p2, DungeonRunEncounters.Carts) == 3, "each player does not start with three Carts");
                break;
            case "DALA_859":
                // The run player's first turn: 1 + 2 Mana Crystals; the boss starts with its 2 and what its floor gives.
                Fail(problems, p1.BaseMana >= 3 && p2.BaseMana >= 2, "the two extra Mana Crystals are missing");
                Seen++;
                break;
            case "DALA_863":
                Fail(problems, p1.DeckZone.Concat(p1.HandZone).Count(c => c.Card.Id == "UNG_856") == 10 && p2.DeckZone.Concat(p2.HandZone).Count(c => c.Card.Id == "UNG_856") == 10, "each deck does not hold ten Hallucinations");
                break;
            case "DALA_852": Fail(problems, p1.ExtraBattlecry && p2.ExtraBattlecry, "Battlecries do not trigger twice for both players"); break;
            // At least twice: a run shrine (TRLA_114 Bwonsamdi's Sanctum) adds two more for its owner.
            case "DALA_853": Fail(problems, DeathrattleRules.Repeats(p1, null) >= 2 && DeathrattleRules.Repeats(p2, null) >= 2, "Deathrattles do not trigger twice for both players"); break;
        }
        foreach (var m in Boards) known.Add(m.Id);
        Look(null);
    }

    public void Before(PlayerTask task)
    {
        actor = game.CurrentPlayer; played = null; aimed = null; spellCost = -1;
        handBefore = actor.HandZone.Count;
        if (task is PlayCardTask play && play.Source != null)
        {
            played = play.Source;
            if (rule == "DALA_856" && played is Spell) recasts = Recasts();
        }
        if (task is MinionAttackTask attack && attack.Target != null) { aimed = attack.Target; targetDamage = aimed.Damage + (aimed is Minion m && m.HasDivineShield ? 1000 : 0) + (aimed is Hero h ? 100000 - h.Armor : 0); }
    }

    public void After(PlayerTask task, List<string> problems)
    {
        switch (rule)
        {
            case "DALA_851":
                if (played is Spell spell && !spell.IsCountered)
                {
                    int cost = spell[GameTag.TAG_LAST_KNOWN_COST_IN_HAND];
                    if (actor.BoardZone.Any(m => !known.Contains(m.Id) && m.Card.Cost == cost)) Seen++;
                }
                break;
            case "DALA_852": if (played is Minion battlecry && battlecry.Card[GameTag.BATTLECRY] == 1 && actor.ExtraBattlecry) Seen++; break;
            case "DALA_855": if (task is EndTurnTask && actor.HandZone.Count > handBefore) Seen++; break;
            case "DALA_856": if (played is Spell && Recasts() > recasts) Seen++; break;
            case "DALA_858": if (task is EndTurnTask && game.CurrentPlayer.BoardZone.Any(m => !known.Contains(m.Id) && m.Card.IsRace(Race.MURLOC))) Seen++; break;
            case "DALA_861":
                // The aimed defender is untouched although the attack happened: the minion attacked the wrong enemy.
                if (aimed != null && game.State != State.COMPLETE && aimed.Zone?.Type == Zone.PLAY
                    && aimed.Damage + (aimed is Minion m && m.HasDivineShield ? 1000 : 0) + (aimed is Hero h ? 100000 - h.Armor : 0) == targetDamage) Seen++;
                break;
            case "DALA_862": if (task is EndTurnTask && actor.HandZone.Any(c => c.Cost < c.Card.Cost)) Seen++; break;
            case "DALA_863": if (played != null && played.Card.Id == "UNG_856") Seen++; break;
        }
        Look(problems);
    }

    // The boards after an action: what arrived, and what the rule gave it.
    private void Look(List<string> problems)
    {
        foreach (var m in Boards)
        {
            bool isNew = known.Add(m.Id);
            switch (rule)
            {
                case "DALA_854": if (isNew && !m.Card.Untouchable && m.AttackDamage == 1 && m.BaseHealth == 1 && (m.Card.ATK != 1 || m.Card.Health != 1) && !ReferenceEquals(played, m)) Seen++; break;
                case "DALA_860": if (isNew && m.Card.Id == "LOOT_209t") Seen++; break;
                case "DALA_865": if (isNew && Has(m, "DALA_865e2")) Seen++; break;
                case "DALA_867": if (isNew && !m.Untouchable && (m.HasTaunt && !m.Card.Taunt || m.HasDivineShield && m.Card[GameTag.DIVINE_SHIELD] == 0 || m.IsRush && !m.Card.Rush || m.HasWindfury && m.Card[GameTag.WINDFURY] == 0)) Seen++; break;
                case "DALA_831": if (isNew && !m.Untouchable && m.Card.ATK != m.Card.Health && m.BaseHealth == m.Card.ATK) Seen++; break;
            }
        }
        if (rule == "DALA_833" && problems != null)
            Fail(problems, game.Player1.BoardZone.CountExceptUntouchables <= 4 && game.Player2.BoardZone.CountExceptUntouchables <= 4, "a player has more than four minions");
        if (rule == "DALA_833" && (game.Player1.BoardZone.IsFull || game.Player2.BoardZone.IsFull)) Seen++;
        if (rule == "DALA_832")
        {
            if (hadPrison1 && game.Player1.BoardZone.All(m => m.Card.Id != "DALA_832t")) { hadPrison1 = false; Seen++; }
            if (hadPrison2 && game.Player2.BoardZone.All(m => m.Card.Id != "DALA_832t")) { hadPrison2 = false; Seen++; }
        }
    }

    public void End(List<string> problems)
    {
        switch (rule)
        {
            case "DALA_833": Fail(problems, EncounterRules.Blockers(game.Player1, DungeonRunEncounters.Carts) == 3 && EncounterRules.Blockers(game.Player2, DungeonRunEncounters.Carts) == 3, "a Cart left the board"); break;
            case "DALA_829": if (game.Player2.GraveyardZone.Any(c => c.Card.Id == "DALA_829t")) Seen++; break;
            case "DALA_853": Seen += game.Player1.GraveyardZone.Concat(game.Player2.GraveyardZone).Count(c => c is Minion m && m.Card[GameTag.DEATHRATTLE] == 1); break;
        }
    }
}''')
if crlf: src = src.replace('\n', '\r\n')
import os
os.makedirs(OUT, exist_ok=True)
io.open(OUT + '/Program.cs', 'w', encoding='utf-8', newline='').write(src)
proj = io.open(BASE + '/BossPilot.csproj', encoding='utf-8', newline='').read()
io.open(OUT + '/BossPilot.csproj', 'w', encoding='utf-8', newline='').write(proj)
print('wrote', OUT)
