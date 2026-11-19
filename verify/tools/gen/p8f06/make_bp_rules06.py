# Phase 8 family 06: builds p8f06/suites/c-bosspilot-rules06 from the encounter bosspilot harness
# (p8f01/suites/c-bosspilot-encounter) with what the Tombs of Terror rules need:
#   - BOSS may name a custom boss (band 1 to 3), so a rule is played against one custom boss beside the imported ones;
#   - RULE is injected with its own kind (anomaly or twist); a run-scope rule (ULDA_720 Battle Scars, ULDA_729
#     Multiclassing) is injected when the run starts, so it is active on the whole walk (the card drafts on the way are
#     Multiclassing's), and a run-layer rule is expected OUT of the match plan's rule list;
#   - Battle Scars: the run enters the fight with 25 carried damage; the plan, the hero's Health at the start and what the
#     run keeps after a won fight are checked;
#   - with RULE a probe watches the game for the rule's own effect (RuleProbe at the end of the file): what the rule places
#     or holds is a failed check when it is missing, and what the rule does during play is counted per game ("seen N" on each
#     line, "rule-seen N" on the last, and the bosses whose games never showed it).
# The sources of truth stay the base harness and this script: rerun it after the base harness changes.
#   python -I verify/tools/gen/p8f06/make_bp_rules06.py [<output folder>]
# (ported to verify/: base verify/suites/p8f01/c-bosspilot-encounter; output verify/suites/p8f06/c-bosspilot-rules06 by default)
import io, os, sys
V = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))  # verify/, from verify/tools/gen/<family>/
BASE = V + '/suites/p8f01/c-bosspilot-encounter'
OUT = sys.argv[1] if len(sys.argv) > 1 else V + '/suites/p8f06/c-bosspilot-rules06'

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
// Family 06 variant (p8f06/make_bp_rules06.py): BOSS may name a custom boss; RULE is injected with its own kind, a run-scope
// rule from the start of the run; a run-layer rule (ULDA_720, ULDA_729) is expected out of the plan's rule list and checked
// on the run layer; a probe watches each game for the rule's own effect (RuleProbe below): a missing state is a failed
// check; each line ends with "seen N", the last line with "rule-seen N" and the bosses whose games never showed the rule.
''')
src = once(src,
'''string forcedRule = Env("RULE");
''',
'''string forcedRule = Env("RULE");
var forced = forcedRule == null ? null : data.Schedules.Rule(forcedRule) ?? throw new Exception("unknown rule " + forcedRule);
bool runLayerRule = forced != null && forced.Effect == DungeonRunGlobalRule.EffectRun;
const int ScarsCarried = 25;
''')
# a run-scope rule is active from the start of the run
src = once(src,
'''    var scratch = new DungeonRunProfile();
    // The run is of the schedule it walks: the named one, or on the default roll the default it rolled.
    var walked = defaultRoll ? run.ScheduleFor(s) : schedule;
    if (!s.IsActRun || s.ScheduleVersion != walked.StampVersion) throw new Exception("not a run of " + walked.Id);
''',
'''    var scratch = new DungeonRunProfile();
    // The run is of the schedule it walks: the named one, or on the default roll the default it rolled.
    var walked = defaultRoll ? run.ScheduleFor(s) : schedule;
    if (!s.IsActRun || s.ScheduleVersion != walked.StampVersion) throw new Exception("not a run of " + walked.Id);
    if (forced != null && forced.RunScope) s.ActiveRules.Add(new DungeonRunRule { Id = forced.Id, Act = 0, FromNode = -1, Kind = forced.Kind });
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
'''            if (forcedRule != null)
            {
                s.ActiveRules.RemoveAll(r => r.Kind != null);
                s.ActiveRules.Add(new DungeonRunRule { Id = forcedRule, Act = run.CurrentNode(s).Act, FromNode = -1, Kind = DungeonRunGlobalRule.KindAnomaly });
            }
''',
'''            if (forced != null)
            {
                s.ActiveRules.RemoveAll(r => r.Kind != null);
                s.ActiveRules.Add(new DungeonRunRule { Id = forced.Id, Act = forced.RunScope ? 0 : run.CurrentNode(s).Act, FromNode = -1, Kind = forced.Kind });
                if (forced.Id == DungeonRun.BattleScars) s.HeroDamage = ScarsCarried;
            }
''')
src = once(src,
'''                Check(plan.EncounterId == boss.Id && plan.EncounterDeck, "plan is not the encounter's", problems);
''',
'''                Check(boss.IsEncounter ? plan.EncounterId == boss.Id && plan.EncounterDeck : plan.EncounterId == null && !plan.EncounterDeck, "plan is not the boss's", problems);
''')
src = once(src,
'''                Check(plan.GlobalRules.SequenceEqual(forcedRule == null ? new string[0] : new[] { forcedRule }), "plan global rules", problems);
''',
'''                Check(plan.GlobalRules.SequenceEqual(forcedRule == null || runLayerRule ? new string[0] : new[] { forcedRule }), "plan global rules", problems);
                if (forced != null) Check(run.RuleActive(s, forced.Id), "the rule is not active on the run", problems);
                int foreignCards = 0;
                if (forcedRule == DungeonRun.BattleScars)
                    Check(plan.PlayerHeroHealth == DungeonRun.BattleScarsHealth && plan.PlayerCarriedDamage == ScarsCarried, "Battle Scars: the plan's hero Health and carried damage", problems);
                if (forcedRule == DungeonRun.Multiclassing)
                {
                    var own = run.RunClasses(s);
                    foreignCards = plan.PlayerDeck.Count(c => c.Class != CardClass.NEUTRAL && !own.Contains(c.Class.ToString()) && s.Cards.Any(x => x.Id == c.Id && x.Origin == DungeonRunDeck.OriginDraft));
                    Check(run.LootClasses(s).Count == data.ClassRows.Count() && run.MatchClasses(s).Count >= run.LootClasses(s).Count, "Multiclassing: the loot classes", problems);
                }
''')
src = once(src,
'''                if (playerHealth > 0) game.Player1.Hero.Damage = Math.Max(0, game.Player1.Hero.BaseHealth - playerHealth);
''',
'''                if (playerHealth > 0) game.Player1.Hero.Damage = Math.Max(0, game.Player1.Hero.BaseHealth - playerHealth);
                if (forcedRule == DungeonRun.BattleScars)
                    Check(game.Player1.Hero.BaseHealth >= DungeonRun.BattleScarsHealth && game.Player1.Hero.Damage == ScarsCarried, "Battle Scars: the hero starts at " + game.Player1.Hero.Health + " of " + game.Player1.Hero.BaseHealth, problems);
''')
src = once(src,
'''                foreach (string rule in plan.GlobalRules)
''',
'''                if (runLayerRule) Check(!DungeonRunGlobalRules.IsScripted(forcedRule) && game.EncounterOrNull?.GlobalRules.Contains(forcedRule) != true, "a run-layer rule reached the match", problems);
                foreach (string rule in plan.GlobalRules)
''')
src = once(src,
'''                int decisionIndex = 0, actions = 0;
''',
'''                var probe = forcedRule == null ? null : new RuleProbe(forcedRule, game, RunHelpers.Bosses(game));
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
'''                run.RecordOutcome(s, profile, outcome, game.RunResult, end);
''',
'''                run.RecordOutcome(s, profile, outcome, game.RunResult, end);
                if (forcedRule == DungeonRun.BattleScars)
                {
                    bool ownHero = end.HeroesUsed.Count <= 1;
                    Check(s.HeroDamage == (outcome == DungeonRunOutcome.Win && ownHero ? Math.Min(end.PlayerHeroDamage, DungeonRun.BattleScarsHealth - 1) : ScarsCarried), "Battle Scars: the run keeps " + s.HeroDamage + " damage", problems);
                    if (probe != null) probe.Seen++;
                }
                if (forcedRule == DungeonRun.Multiclassing && probe != null) probe.Seen += foreignCards;
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

// What a global rule visibly does in a game (family 06). Start: what the rule places or holds after both mulligans (a failed
// check when it is missing). Before / After: one probe per rule around every action of either player; a rule that must hold
// at every moment (Angelic Presence, CHARGE!, Relentless, No-Face) is a failed check when it does not; Seen counts the rule's
// own effect. End: what must still hold when the game is over.
sealed class RuleProbe
{
    private readonly string rule; private readonly Game game; private readonly int bossesDefeated;
    private readonly HashSet<int> known = new HashSet<int>();
    private readonly Dictionary<int, int> held = new Dictionary<int, int>();
    private int coinsBefore, armorBefore, deadBefore, deckBefore, hero1, hero2; private string costs1, costs2; private bool fighter;
    private IPlayable played; private Controller actor;
    public int Seen;
    public RuleProbe(string rule, Game game, int bossesDefeated) { this.rule = rule; this.game = game; this.bossesDefeated = bossesDefeated; }
    private IEnumerable<Minion> Boards => game.Player1.BoardZone.Concat(game.Player2.BoardZone);
    private IEnumerable<Minion> Real => Boards.Where(m => !m.Untouchable && !m.ToBeDestroyed && m.Health > 0);
    private static int Coins(Controller c) => c.HandZone.Count(p => p.Card.Id == "GAME_005");
    private static int Dead(Controller c) => c.GraveyardZone.Count(p => p is Minion);
    private static string Costs(Controller c) => string.Join(",", c.HandZone.Select(p => p.Id + ":" + p.Cost));
    private static int Hurt(Controller c) => c.Hero.Damage - c.Hero.Armor;
    private int Count(string id, Func<IPlayable, bool> where = null) => game.IdEntityDic.Values.Count(e => e.Card.Id == id && (where == null || where(e)));
    private void Fail(List<string> problems, bool ok, string what) { if (!ok) problems.Add("rule " + rule + ": " + what); }

    public void Start(List<string> problems)
    {
        Controller p1 = game.Player1, p2 = game.Player2;
        switch (rule)
        {
            case "ULDA_714": Fail(problems, p1[EncounterRules.HeroDamageDouble] == 1 && p2[EncounterRules.HeroDamageDouble] == 1, "the heroes do not take double damage"); Seen++; break;
            // Nine cards each before the mulligan (the boss's Coin is one of its nine); the run player has drawn for its first turn since.
            // A special-room boss that holds no cards (family 09: Tad, the Treasure Vault) has put its nine back into its deck by now
            // (wave 3 integration: EncounterRules.EnterRoom at the first turn start).
            case "ULDA_715": Fail(problems, p1.HandZone.Count == 10 && p2.HandZone.Count == (EncounterRules.NeverDraws(p2) ? 0 : 9), "the hands are " + p1.HandZone.Count + " and " + p2.HandZone.Count + " cards"); Seen++; break;
            case "ULDA_719":
                Fail(problems, p1.BoardZone.Count(m => m.Card.Id == "LOOT_357l") == 1 && p2.BoardZone.Count(m => m.Card.Id == "LOOT_357l") == 1, "each player does not start with a Treasure Chest");
                break;
            case "ULDA_721":
                Fail(problems, game.RunBeliefResets.Count(r => r.Zones == RunBeliefZones.Deck) >= 2, "the deck beliefs were not reset");
                Fail(problems, p1.DeckZone.All(c => c.Controller == p1) && p2.DeckZone.All(c => c.Controller == p2), "a deck holds the other player's card entity");
                // The run player's deck now holds cards its run deck does not list: the boss's.
                var mine = new HashSet<string>(p1.DeckCards.Select(c => c.Id), StringComparer.Ordinal);
                if (p1.DeckZone.Any(c => !mine.Contains(c.Card.Id)) || p2.DeckZone.Any(c => mine.Contains(c.Card.Id))) Seen++;
                break;
            case "ULDA_801":
                int copies = Math.Min(DungeonRunEncounters.MurlocPlagueMax, 1 + bossesDefeated);
                Fail(problems, Count("ULDA_801t", e => e.Controller == p1) == copies && Count("ULDA_801t", e => e.Controller == p2) == copies, "each deck did not get " + copies + " Surprise! Murlocs!");
                break;
            case "ULDA_804":
                Fail(problems, new[] { p1, p2 }.All(c => c.BoardZone.Count(m => m.Card.Id == "ULDA_804t" && DungeonRunEncounters.Entombed(m) != null) == 1), "each player does not start with an Eternal Tomb that holds a minion");
                foreach (var tomb in Boards.Where(m => m.Card.Id == "ULDA_804t")) held[tomb.Id] = tomb[GameTag.ALTERNATE_MOUSE_OVER_CARD];
                break;
        }
        foreach (var m in Boards) known.Add(m.Id);
        Look(problems);
    }

    public void Before(PlayerTask task)
    {
        actor = game.CurrentPlayer; played = null;
        coinsBefore = Coins(actor.Opponent); armorBefore = actor.Hero.Armor; deadBefore = Dead(game.Player1) + Dead(game.Player2);
        deckBefore = game.Player1.DeckZone.Count + game.Player2.DeckZone.Count;
        hero1 = Hurt(game.Player1); hero2 = Hurt(game.Player2); costs1 = Costs(game.Player1); costs2 = Costs(game.Player2);
        fighter = EncounterRules.RightmostMinion(actor) != null;
        if (task is PlayCardTask play && play.Source != null) played = play.Source;
    }

    public void After(PlayerTask task, List<string> problems)
    {
        bool died = Dead(game.Player1) + Dead(game.Player2) > deadBefore;
        switch (rule)
        {
            case "ULDA_702": if (played is Minion && Coins(actor.Opponent) > coinsBefore) Seen++; break;
            case "ULDA_705": if (died && Count("BOT_511t") > 0) Seen++; break;
            case "ULDA_707": if (died && game.Player1.DeckZone.Count + game.Player2.DeckZone.Count > deckBefore) Seen++; break;
            case "ULDA_708":
                if (Hurt(game.Player1) > hero1 && Costs(game.Player1) != costs1 || Hurt(game.Player2) > hero2 && Costs(game.Player2) != costs2) Seen++;
                Fail(problems, game.Player1.HandZone.Concat(game.Player2.HandZone).All(c => c.Cost >= 0), "a negative Cost");
                break;
            case "ULDA_712": if (played is Minion copy && actor.Opponent.BoardZone.Any(m => !known.Contains(m.Id) && m.Card.Id == copy.Card.Id && m.BaseHealth == 1)) Seen++; break;
            case "ULDA_713": if (task is EndTurnTask && Real.Any(m => m.Damage > 0)) Seen++; break;
            case "ULDA_724": if (played is Spell && actor.Hero.Armor >= armorBefore + 2) Seen++; break;
            case "ULDA_802": if (task is EndTurnTask && fighter) Seen++; break;
            case "ULDA_803": if (Real.Any(m => m.Damage > 0 && m.AttackDamage > m.Card.ATK)) Seen++; break;
        }
        Look(problems);
    }

    // The boards after an action: what must hold at every moment, and what arrived.
    private void Look(List<string> problems)
    {
        if (game.State == State.COMPLETE) return;
        switch (rule)
        {
            case "ULDA_706":
                foreach (var c in new[] { game.Player1, game.Player2 })
                    if (EncounterRules.ControlsMinion(c)) { Fail(problems, c.Hero.IsImmune, "a hero with a minion is not Immune"); Seen++; }
                break;
            case "ULDA_709":
                foreach (var m in Real) { Fail(problems, m.AttackDamage == m.Health, m.Card.Id + " has " + m.AttackDamage + " Attack and " + m.Health + " Health"); if (m.Health != m.Card.ATK) Seen++; }
                break;
            case "ULDA_710": foreach (var m in Real) { Fail(problems, m.HasCharge, m.Card.Id + " has no Charge"); Seen++; } break;
            case "ULDA_711": foreach (var m in Real) { Fail(problems, m.HasWindfury, m.Card.Id + " has no Windfury"); Seen++; } break;
            case "ULDA_719": Seen = game.Player1.GraveyardZone.Concat(game.Player2.GraveyardZone).Count(c => c.Card.Id == "LOOT_357l"); break;
            case "ULDA_801": Seen = Count("ULDA_801t", e => e.Zone?.Type != Zone.DECK); break;
            case "ULDA_804":
                foreach (var tomb in Boards.Where(m => m.Card.Id == "ULDA_804t"))
                {
                    int now = tomb[GameTag.ALTERNATE_MOUSE_OVER_CARD];
                    if (held.TryGetValue(tomb.Id, out int before) && before != now) Seen++;
                    held[tomb.Id] = now;
                }
                if (problems != null) Fail(problems, game.Player1.BoardZone.Count(m => m.Card.Id == "ULDA_804t") == 1 && game.Player2.BoardZone.Count(m => m.Card.Id == "ULDA_804t") == 1, "an Eternal Tomb left the board");
                break;
        }
        foreach (var m in Boards) known.Add(m.Id);
    }

    public void End(List<string> problems)
    {
        switch (rule)
        {
            case "ULDA_705": Seen = Math.Max(Seen, Count("BOT_511t") > 0 ? 1 : 0); break;
        }
    }
}''')
if crlf: src = src.replace('\n', '\r\n')
os.makedirs(OUT, exist_ok=True)
io.open(OUT + '/Program.cs', 'w', encoding='utf-8', newline='').write(src)
proj = io.open(BASE + '/BossPilot.csproj', encoding='utf-8', newline='').read()
io.open(OUT + '/BossPilot.csproj', 'w', encoding='utf-8', newline='').write(proj)
print('wrote', OUT)
