using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.DungeonRun;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

// Step 9 C6 (handoff/phase9/plan.md section 1 C): pairs9, the covering array of verify/tools/balance/pairs.py played as real
// engine games on the shipped stamp-4 schedules. Each case names a twist, an anomaly, a modifier, a boss (one per flag set) and
// a run class; a run of that class on acts-v4h (acts-v4t for Tekahn's finale) walks its real nodes to the first floor that
// draws the boss (a finale row: its fixed floor), the boss is put there, and the case's rules are injected (RULES=id,id: the
// twist and the anomaly on the fight's act, a run-scope anomaly on the run; the modifier on the fight's act), replacing the
// walk's own. The plan is built by DungeonRun.BuildPlan and the game played through the run setup path with the fair AI on the
// boss and the local bot on the run player (the encounter harness's loop, with the plan's own turn cap), then recorded.
// Expected: 0 faults, 0 fallbacks, 0 failed checks, 0 swallowed leaf exceptions. Chunk 23 case 1722 (dala_07_belnaara, MAGE)
// logged 3 boss-AI fallbacks at turn 20 (exit 3) until the engine fix of SwitchingAura.RemoveInternal (an applied id of a hand
// card the boss's fair view had re-registered; handoff/phase9/tests.md section 2, c-gen-9 A): 0 since.
//   PAIRS=path     the case file (default verify/fixtures/p9/pairs9.txt)
//   CHUNK=n        only the cases of chunk n (the file's first column); CASES=a-b only case numbers a..b
//   RULES=id,id    instead of the file: one case with these rules (twists and anomalies), with BOSS=id, MODIFIER=id, CLASS=id
//   SEED_BASE=n    seed base (default 9900000; a case's seed is SEED_BASE + its number)
static object FromJson(System.Text.Json.JsonElement e)
{
    switch (e.ValueKind)
    {
        case System.Text.Json.JsonValueKind.Object: { var d = new Dictionary<string, object>(); foreach (var p in e.EnumerateObject()) d[p.Name] = FromJson(p.Value); return d; }
        case System.Text.Json.JsonValueKind.Array: { var l = new List<object>(); foreach (var x in e.EnumerateArray()) l.Add(FromJson(x)); return l; }
        case System.Text.Json.JsonValueKind.String: return e.GetString();
        case System.Text.Json.JsonValueKind.Number: return e.TryGetInt64(out long v) ? (object)v : e.GetDouble();
        case System.Text.Json.JsonValueKind.True: return true;
        case System.Text.Json.JsonValueKind.False: return false;
        default: return null;
    }
}
NpcWorldJson.Parser = text => { using var doc = System.Text.Json.JsonDocument.Parse(text); return FromJson(doc.RootElement); };

string Env(string name) => Environment.GetEnvironmentVariable(name) is string v && v.Length > 0 ? v : null;
string root = VerifyPaths.Data;
var data = DungeonRunData.Parse(File.ReadAllText(Path.Combine(root, "dungeon-run", "run-v1.json")),
    File.ReadAllText(Path.Combine(root, "dungeon-run", "pool-v1.json")), File.ReadAllText(Path.Combine(root, "npc", "decks-v1.json")),
    File.ReadAllText(Path.Combine(root, "dungeon-run", "catalog-v1.json")), File.ReadAllText(Path.Combine(root, "dungeon-run", "schedule-v1.json")),
    File.ReadAllText(Path.Combine(root, "dungeon-run", "tavern-v1.json")), File.ReadAllText(Path.Combine(root, "dungeon-run", "encounters-v1.json")));
var run = new DungeonRun(data);
var all = new DungeonRunProfile(); all.Unlocked.AddRange(data.Unlocks.Keys);
var testBudget = new Budget(6000, 2000, 300, 6, 3, 100, 8, turnSearchSeconds: 6, turnWallSeconds: 8);
long seedBase = Env("SEED_BASE") is string sb ? long.Parse(sb) : 9900000;
const string None = "-";

// The cases: (number, rules, modifier, boss, class).
var cases = new List<(int Number, List<string> Rules, string Modifier, string Boss, string Class)>();
if (Env("RULES") is string injected)
    cases.Add((1, injected.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0 && x != None).ToList(), Env("MODIFIER") ?? None,
        Env("BOSS") ?? throw new Exception("RULES needs BOSS"), Env("CLASS") ?? "MAGE"));
else
{
    string path = Env("PAIRS") ?? VerifyPaths.Fixture("p9/pairs9.txt");
    int chunk = Env("CHUNK") is string ch ? int.Parse(ch) : 0;
    int from = 1, to = int.MaxValue;
    if (Env("CASES") is string range) { var p = range.Split('-'); from = int.Parse(p[0]); to = int.Parse(p[p.Length - 1]); }
    foreach (string line in File.ReadAllLines(path))
    {
        if (line.StartsWith("#") || line.Trim().Length == 0) continue;
        var f = line.Split(' ');
        int c = int.Parse(f[0]), number = int.Parse(f[1]);
        if (chunk != 0 && c != chunk || number < from || number > to) continue;
        cases.Add((number, new[] { f[2], f[3] }.Where(x => x != None).ToList(), f[4], f[5], f[6]));
    }
}

// The schedule and floor a boss is fought on: a finale's fixed boss on its schedule's finale floor, every other row on the
// first floor of acts-v4h whose pool draws it on a stamp-4 run.
(string Schedule, int Floor) Placement(DungeonRunBoss boss)
{
    foreach (string id in new[] { "acts-v4h", "acts-v4t" })
    {
        var sc = data.Schedules.Find(id);
        int floor = 0;
        foreach (var act in sc.Acts)
        {
            if (act.IsFinale)
            {
                foreach (string b in act.FixedBosses) { floor++; if (b == boss.Id) return (id, floor); }
                continue;
            }
            foreach (var slot in act.Combat)
                for (int k = 0; k < slot.Count; k++) { floor++; if (id == "acts-v4h" && data.Schedules.MemberTuned(boss, slot.Pool)) return (id, floor); }
            floor++;
            if (id == "acts-v4h" && data.Schedules.MemberTuned(boss, act.MajorPool)) return (id, floor);
        }
    }
    throw new Exception("no stamp-4 floor draws " + boss.Id);
}

// The walk takes no rules, so it skips the treasures a case rule's incompatibleTreasures names: a real run holding one is never
// offered that rule (RuleCandidates), so a walk that drafted it would make a state no run reaches (review: ULDA_720 x LOOTA_800).
DungeonRunState Reach(DungeonRunClass cls, string schedule, int floor, long seed, ISet<string> avoid)
{
    var s = run.NewRun(all, cls.Id, cls.Decks[0], cls.Powers[0], seed, schedule: schedule, signatureId: run.SignatureOffer(all, cls, seed).FirstOrDefault());
    if (!DungeonRun.Tuned(s)) throw new Exception("not a stamp-4 run");
    var scratch = new DungeonRunProfile();
    for (int guard = 0; guard < 600 && !(s.Floor == floor && s.Phase == DungeonRunPhase.PreMatch); guard++)
    {
        switch (s.Phase)
        {
            case DungeonRunPhase.PreMatch: run.BeginMatch(s); run.RecordOutcome(s, scratch, DungeonRunOutcome.Win); break;
            case DungeonRunPhase.TreasureDraft: run.PickTreasure(s, s.OpenDraft.Offered.FirstOrDefault(t => !avoid.Contains(t)) ?? s.OpenDraft.Offered[0]); break;
            // An anomaly or modifier node takes none on the walk: the case injects its own at the fight.
            case DungeonRunPhase.Modifier:
                run.PickModifier(s, s.ModifierOffer.Kind == DungeonRunGlobalRule.KindAnomaly || s.ModifierOffer.Offered.Contains(DungeonRunGlobalRule.None) ? DungeonRunGlobalRule.None : s.ModifierOffer.Offered[0]);
                break;
            case DungeonRunPhase.Tavern: run.LeaveTavern(s); break;
            case DungeonRunPhase.BundleDraft:
                if (s.OpenDraft.Takes == 0) { run.PickBundle(s, s.OpenDraft.Offered[0]); break; }
                string bundle = s.OpenDraft.Choosing ?? s.OpenDraft.Offered[0];
                var left = run.OfferCards(s.OpenDraft, bundle);
                foreach (string kept in s.OpenDraft.Chosen) left.Remove(kept);
                run.PickDraftCard(s, bundle, left[0]);
                break;
            default: throw new Exception("walk stopped at " + s.Phase);
        }
    }
    if (!(s.Floor == floor && s.Phase == DungeonRunPhase.PreMatch)) throw new Exception("the walk did not reach floor " + floor);
    return s;
}

int faults = 0, games = 0, bossWins = 0, totalFallbacks = 0, failed = 0, caps = 0;
void Check(bool ok, string what, List<string> problems) { if (!ok) problems.Add(what); }
foreach (var c in cases)
{
    var boss = data.Boss(c.Boss);
    var cls = data.FindClass(c.Class) ?? throw new Exception("unknown class " + c.Class);
    string result; int turns = 0, decisions = 0, fallbacks = 0, health = 0, cap = 0; string schedule = "-"; int floor = 0;
    var problems = new List<string>();
    var sw = Stopwatch.StartNew();
    try
    {
        (schedule, floor) = Placement(boss);
        var avoid = new HashSet<string>(c.Rules.SelectMany(id => data.Schedules.Rule(id)?.IncompatibleTreasures ?? new List<string>()), StringComparer.Ordinal);
        var s = Reach(cls, schedule, floor, seedBase + c.Number, avoid);
        s.BossSequence[floor - 1] = boss.Id;
        int act = run.CurrentNode(s).Act;
        s.ActiveRules.RemoveAll(r => r.Kind != null || r.Act == act);
        foreach (string id in c.Rules)
        {
            var rule = data.Schedules.Rule(id) ?? throw new Exception("unknown rule " + id);
            s.ActiveRules.Add(new DungeonRunRule { Id = id, Act = rule.RunScope ? 0 : act, FromNode = -1, Kind = rule.Kind });
        }
        if (c.Modifier != None) s.ActiveRules.Add(new DungeonRunRule { Id = c.Modifier, Act = act, FromNode = -1 });
        // pairs.py leaves out every declared pair, so the run layer's predicate finds none with the case's boss, run and rules
        // (lines naming another boss the walk put on the act are not the case's).
        var others = s.BossSequence.Where(id => id != boss.Id).ToList();
        var pairLines = run.ForbiddenPairs(s).Where(l => l.Contains(" " + boss.Id + " ") || !others.Any(id => l.Contains(" " + id + " "))).ToList();
        Check(pairLines.Count == 0, "a forbidden pair the covering array should have declared: " + string.Join(" | ", pairLines), problems);
        var profile = new DungeonRunProfile(); profile.Unlocked.AddRange(data.Unlocks.Keys);
        var plan = run.BuildPlan(s, profile);
        health = plan.BossHealth; cap = plan.TurnCap;
        var matchRules = c.Rules.Where(id => data.Schedules.Rule(id).Effect == DungeonRunGlobalRule.EffectMatch).ToList();
        Check(plan.GlobalRules.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(matchRules.OrderBy(x => x, StringComparer.Ordinal)), "plan global rules " + string.Join(",", plan.GlobalRules), problems);
        Check(plan.EncounterId == boss.Id, "plan is not the encounter's", problems);
        var pilot = DungeonRunBossPilot.Build(boss, plan, data, runClasses: run.MatchClasses(s));
        var npcProfile = pilot.Profile.WithBudget(testBudget);
        var golden = new GoldenPilot(npcProfile, pilot.Plan, null, pilot.SecretPrior);
        var cfg = new GameConfig { RandomSeed = run.MatchSeed(s), Shuffle = true, SkipMulligan = false, History = false, Logging = false, FormatType = FormatType.FT_WILD };
        DungeonRunSetup.Configure(cfg, plan);
        var game = new Game(cfg);
        game.StartGame();
        if (game.FirstPlayer != game.Player1) throw new Exception("player not first");
        DungeonRunSetup.ApplyAfterStart(game, plan);
        foreach (string rule in plan.GlobalRules)
            Check(DungeonRunGlobalRules.IsActive(game.Player1, rule) && DungeonRunGlobalRules.IsActive(game.Player2, rule), "rule " + rule + " is not on both players", problems);
        run.BeginMatch(s);
        game.Process(ChooseTask.Mulligan(game.Player1, new List<int>()));
        var choice = game.Player2.Choice;
        var view = new MulliganView(choice.Choices.Select(id => new MulliganCard(id, game.IdEntityDic[id].Card.Id, game.IdEntityDic[id].Card.Cost)),
            false, pilot.Deck.CardIds, game.Player1.Hero.Card.Class);
        game.Process(ChooseTask.Mulligan(game.Player2, BotDriver.Mulligan(view, new DeckPlanMulligan(pilot.Plan)).ToList()));
        game.MainBegin();
        int decisionIndex = 0, actions = 0;
        var recent = new Queue<string>();
        void Act(PlayerTask task, string who)
        {
            recent.Enqueue($"t{game.Turn} {who}: {task}");
            if (recent.Count > 10) recent.Dequeue();
            if (++actions > 2500) throw new Exception("stuck after 2500 actions | " + string.Join(" | ", recent));
            if (task is ChooseTask pick && game.Step != Step.BEGIN_MULLIGAN)
            {
                var decision = game.GetPendingDecisions().Single(d => d.ControllerId == task.Controller.Id);
                if (!game.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(decision.Id, decision.ControllerId, pick.Choices.ToArray()), out var why))
                    throw new Exception($"engine rejected {who} choice ({why}) | " + string.Join(" | ", recent));
            }
            else if (!game.Process(task)) throw new Exception($"engine rejected {who} task | " + string.Join(" | ", recent));
        }
        while (game.State != State.COMPLETE && game.Turn <= plan.TurnCap)
        {
            if (game.CurrentPlayer == game.Player2)
            {
                var (task, trace) = BotDriver.Decide(game, game.Player2.PlayerId, pilot.Deck.Style,
                    st => golden.DecidePilot(st, new GoldenContext(game.Turn, decisionIndex, unchecked(997 * game.Turn + decisionIndex * 31), npcProfile)),
                    ref decisionIndex, golden.Options);
                decisions++;
                if (trace?.Kind == "fallback") { fallbacks++; Console.Error.WriteLine($"  FALLBACK case {c.Number} {boss.Id} t{game.Turn}: {trace.FallbackReason}"); }
                Act(task, "boss");
            }
            else
            {
                // The run player plays a fight-only hero card as soon as it can (the hero-switch path), else the local bot's pick.
                var options = game.Player1.Options();
                var heroCard = options.FirstOrDefault(t => t is PlayCardTask play && play.Source is Hero);
                Act(heroCard ?? LocalBot.Choose(options), "player");
            }
        }
        turns = game.Turn;
        result = game.State != State.COMPLETE ? "turn-cap" : game.Player2.PlayState == PlayState.WON ? "boss" : game.Player1.PlayState == PlayState.WON ? "player" : "tie";
        if (result == "boss") bossWins++;
        if (result == "turn-cap") caps++;
        var end = DungeonRunSetup.Harvest(game);
        var outcome = result == "player" ? DungeonRunOutcome.Win : result == "turn-cap" ? DungeonRunOutcome.TurnLimit : result == "tie" ? DungeonRunOutcome.Tie : DungeonRunOutcome.Loss;
        run.RecordOutcome(s, profile, outcome, game.RunResult, end);
        Check(s.HeroSwitch == null, "the hero switch outlived the fight", problems);
    }
    catch (Exception e) { result = "FAULT " + e.GetType().Name + ": " + e.Message.Split('\n')[0]; faults++; }
    games++; totalFallbacks += fallbacks;
    if (problems.Count > 0) { failed += problems.Count; foreach (string p in problems) Console.Error.WriteLine($"  CHECK case {c.Number} {boss.Id}: {p}"); }
    Console.WriteLine($"case {c.Number,4} {schedule} floor {floor,2} {boss.Id,-24} {c.Class,-11} rules {(c.Rules.Count == 0 ? "-" : string.Join(",", c.Rules)),-17} mod {c.Modifier,-15} -> {result,-8} turns {turns,3}/{cap} decisions {decisions,3} fallbacks {fallbacks,2} health {health,3} checks {(problems.Count == 0 ? "ok" : "FAIL " + problems.Count)} {sw.ElapsedMilliseconds / 1000.0,6:F1}s");
}
foreach (var kv in SearchTelemetry.Details()) Console.Error.WriteLine($"SWALLOWED {kv.Value} x {kv.Key}");
Console.WriteLine($"pairs9: games {games}, boss wins {bossWins}, turn caps {caps}, faults {faults}, fallbacks {totalFallbacks}, failed checks {failed}, swallowed leaf exceptions {SearchTelemetry.SwallowedLeafErrors}");
return faults + failed + totalFallbacks + SearchTelemetry.SwallowedLeafErrors;
