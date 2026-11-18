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

// Step 9 lane X (handoff/phase9/plan.md section 3, stage 2, "bp mode: ends9"): real engine games of the match ends on the shipped
// stamp-4 schedules, each fight played until its floor is decided, as a host plays it: the plan from DungeonRun.BuildPlan (its own
// TurnCap, FormRatchet, EscapeAllowed and Restarts), the game through the run setup path, the fair AI on the boss and the local bot
// on the run player, the harvest reported as LocalLadder reports it (a restart mark as a Fault), then DungeonRun.RecordOutcome; a
// restarted floor is built and played again under its new seed. The old encounter harness (p8f01/c-bosspilot-encounter) cannot
// host this (plan D3): it plays one game per fight with a literal 100-turn cap, reports the engine's result as it stands and
// checks that a lost fight changes nothing, so it can neither restage a restarted floor nor take an escape tavern.
//   PART=escape   ULDA_111 Hearthstone held: a combat fight, a major and a Plague Lord; the run player casts it from its turn
//                 ESCAPE_TURN on (default 7; before that it is left out of the bot's options) and the run takes the escape tavern
//   PART=refuse   Hearthstone held on the acts-v4h Heist finale and the acts-v4t Tekahn finale: never playable, the fight as usual
//   PART=rewind   GILA_911 Rewind held, the run hero 18 Health down at the start of each game (the harness's handicap, so it dies):
//                 a death restarts the floor once under a fresh seed, and the restarted game decides it
//   PART=toki     Infinite Toki with GILA_BOSS_48t in his deck (a data copy: the stage-3 gate has not put it back) and Rewind held:
//                 the boss is given the card and casts it from turn TIMELINE_TURN on (default 9; a scripted cast, the gate's
//                 condition); the floor restarts with the run hero's damage carried, once per source
//   PART=tekahn   the acts-v4t Tekahn finale with the relay: TurnCap 150 and the form ratchet (no form above its threshold)
// CASES=a-b only those case numbers; SEED_BASE=n (default 9090000; a case's seed is SEED_BASE + 100 x its number).
// Expected: 0 faults, 0 fallbacks, 0 failed checks, 0 swallowed leaf exceptions.
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
string R(string f) => File.ReadAllText(Path.Combine(root, f));
DungeonRunData Load() => DungeonRunData.Parse(R("dungeon-run/run-v1.json"), R("dungeon-run/pool-v1.json"), R("npc/decks-v1.json"),
    R("dungeon-run/catalog-v1.json"), R("dungeon-run/schedule-v1.json"), R("dungeon-run/tavern-v1.json"), R("dungeon-run/encounters-v1.json"));
var data = Load();
var run = new DungeonRun(data);
const string Hearthstone = RunMatchEnds.Hearthstone, Rewind = RunMatchEnds.Rewind, Timeline = RunMatchEnds.AlternateTimeline;
const string Xatma = "ulda_37_xatma", Tekahn = "ulda_67_tekahn", Toki = "gila_48_infinite_toki", Norroa = "dala_43_norroa";
// Toki with Alternate Timeline in his deck, for the toki part only (the shipped deck lists a substitute in its place).
var tokiData = Load();
var tokiDeck = tokiData.BossDecks[tokiData.Boss(Toki).Deck];
tokiDeck[tokiDeck.Count - 1] = Timeline;
var tokiRun = new DungeonRun(tokiData);
string hearthShell = DungeonRunContent.Get(Hearthstone).ShellId;
var testBudget = new Budget(6000, 2000, 300, 6, 3, 100, 8, turnSearchSeconds: 6, turnWallSeconds: 8);
long seedBase = Env("SEED_BASE") is string sb ? long.Parse(sb) : 9090000;
int escapeTurn = Env("ESCAPE_TURN") is string et ? int.Parse(et) : 7;
int timelineTurn = Env("TIMELINE_TURN") is string tt ? int.Parse(tt) : 9;
const int Handicap = 18;
string part = Env("PART") ?? throw new Exception("PART=escape|refuse|rewind|toki|tekahn");

// The cases of the part: (number, schedule, floor, boss or null for the walk's own, class row index).
var cases = new List<(int Number, string Schedule, int Floor, string Boss, int Class)>();
switch (part)
{
    case "escape":
        cases.Add((1, "acts-v4h", 1, null, 0)); cases.Add((2, "acts-v4h", 2, null, 3));
        cases.Add((3, "acts-v4h", 5, null, 1)); cases.Add((4, "acts-v4h", 10, null, 4));
        cases.Add((5, "acts-v4h", 15, Xatma, 2)); cases.Add((6, "acts-v4h", 15, Xatma, 5)); break;
    case "refuse":
        cases.Add((1, "acts-v4h", 16, Norroa, 0)); cases.Add((2, "acts-v4t", 16, Tekahn, 1)); break;
    case "rewind":
        cases.Add((1, "acts-v4h", 1, null, 0)); cases.Add((2, "acts-v4h", 4, null, 2)); cases.Add((3, "acts-v4h", 7, null, 4)); cases.Add((4, "acts-v4h", 15, Xatma, 1)); break;
    case "toki":
        cases.Add((1, "acts-v4h", 10, Toki, 0)); cases.Add((2, "acts-v4h", 10, Toki, 3)); cases.Add((3, "acts-v4h", 10, Toki, 5)); break;
    case "tekahn":
        cases.Add((1, "acts-v4t", 16, Tekahn, 0)); cases.Add((2, "acts-v4t", 16, Tekahn, 2)); break;
    default: throw new Exception("unknown PART " + part);
}
if (Env("CASES") is string range)
{
    var p = range.Split('-'); int from = int.Parse(p[0]), to = int.Parse(p[p.Length - 1]);
    cases = cases.Where(c => c.Number >= from && c.Number <= to).ToList();
}

// pairs9's walk: won fights, first offers, "none" at a stamp-4 modifier node, every bundle card taken; the walk's own rules stay.
DungeonRunState Reach(DungeonRun r, DungeonRunClass cls, string schedule, int floor, long seed)
{
    var profile = new DungeonRunProfile(); profile.Unlocked.AddRange(r.Data.Unlocks.Keys);
    var s = r.NewRun(profile, cls.Id, cls.Decks[0], cls.Powers[0], seed, schedule: schedule, signatureId: r.SignatureOffer(profile, cls, seed).FirstOrDefault());
    if (!DungeonRun.Tuned(s)) throw new Exception("not a stamp-4 run");
    var scratch = new DungeonRunProfile();
    for (int guard = 0; guard < 800 && !(s.Floor == floor && s.Phase == DungeonRunPhase.PreMatch); guard++)
    {
        switch (s.Phase)
        {
            case DungeonRunPhase.PreMatch: r.BeginMatch(s); r.RecordOutcome(s, scratch, DungeonRunOutcome.Win); break;
            case DungeonRunPhase.TreasureDraft: r.PickTreasure(s, s.OpenDraft.Offered[0]); break;
            case DungeonRunPhase.Modifier:
                r.PickModifier(s, s.ModifierOffer.Kind == DungeonRunGlobalRule.KindAnomaly || s.ModifierOffer.Offered.Contains(DungeonRunGlobalRule.None) ? DungeonRunGlobalRule.None : s.ModifierOffer.Offered[0]);
                break;
            case DungeonRunPhase.Tavern: r.LeaveTavern(s); break;
            case DungeonRunPhase.BundleDraft:
                if (s.OpenDraft.Takes == 0) { r.PickBundle(s, s.OpenDraft.Offered[0]); break; }
                string bundle = s.OpenDraft.Choosing ?? s.OpenDraft.Offered[0];
                var left = r.OfferCards(s.OpenDraft, bundle);
                foreach (string kept in s.OpenDraft.Chosen) left.Remove(kept);
                r.PickDraftCard(s, bundle, left[0]);
                break;
            default: throw new Exception("walk stopped at " + s.Phase);
        }
    }
    if (!(s.Floor == floor && s.Phase == DungeonRunPhase.PreMatch)) throw new Exception("the walk did not reach floor " + floor);
    return s;
}

int faults = 0, games = 0, bossWins = 0, totalFallbacks = 0, failed = 0, caps = 0, escapes = 0, rewinds = 0, timelines = 0;
void Check(bool ok, string what, List<string> problems) { if (!ok) problems.Add(what); }
foreach (var c in cases)
{
    var r = part == "toki" ? tokiRun : run;
    var d = r.Data;
    var cls = d.Classes[c.Class % d.Classes.Count];
    var problems = new List<string>();
    var lines = new List<string>();
    string verdict = "-";
    var sw = Stopwatch.StartNew();
    string bossId = "-";
    try
    {
        var s = Reach(r, cls, c.Schedule, c.Floor, seedBase + 100 * c.Number);
        if (c.Boss != null) s.BossSequence[c.Floor - 1] = c.Boss;
        var boss = r.CurrentBoss(s);
        bossId = boss.Id;
        if (part == "escape" || part == "refuse")
        {
            s.ActiveTreasures.Add(Hearthstone);
            DungeonRunDeck.Add(s, hearthShell, DungeonRunDeck.OriginTreasure);
            while (s.Deck.Count < r.DeckMin(s) + 1) DungeonRunDeck.Add(s, "CS2_182", DungeonRunDeck.OriginStarter);
        }
        if (part == "rewind" || part == "toki") s.Treasures.Add(Rewind);
        var profile = new DungeonRunProfile(); profile.Unlocked.AddRange(d.Unlocks.Keys);
        int floor = s.Floor, node = s.Node, majors = s.MajorsCleared, floorTurns = 0, cap0 = 0;
        var seeds = new List<int>();
        for (int k = 0; k < 4 && s.Floor == floor && s.Node == node && s.Phase == DungeonRunPhase.PreMatch; k++)
        {
            var plan = r.BuildPlan(s, profile);
            if (k == 0) cap0 = r.TurnCapFor(s, boss);
            if (part == "rewind") plan.PlayerCarriedDamage = Math.Max(plan.PlayerCarriedDamage, Handicap);
            // What the plan must say for the part.
            if (part == "escape") Check(plan.EscapeAllowed, "game " + k + ": the plan allows no escape", problems);
            if (part == "refuse") Check(!plan.EscapeAllowed, "game " + k + ": the plan allows an escape on a finale", problems);
            if (part == "tekahn") Check(plan.TurnCap == 150 && plan.FormRatchet && plan.PlayerRelay.Count > 0, "Tekahn's plan: cap " + plan.TurnCap + " ratchet " + plan.FormRatchet, problems);
            if (k == 0 && (part == "rewind" || part == "toki")) Check(plan.Restarts.Contains(Rewind), "the first game does not list Rewind", problems);
            if (k == 0 && part == "toki") Check(plan.Restarts.Contains(Timeline), "Toki's first game does not list Alternate Timeline", problems);
            int seed = r.MatchSeed(s);
            Check(!seeds.Contains(seed), "game " + k + " replays an earlier game's seed", problems);
            seeds.Add(seed);
            var pilot = DungeonRunBossPilot.Build(boss, plan, d, runClasses: r.MatchClasses(s));
            var npcProfile = pilot.Profile.WithBudget(testBudget);
            var golden = new GoldenPilot(npcProfile, pilot.Plan, null, pilot.SecretPrior);
            var cfg = new GameConfig { RandomSeed = seed, Shuffle = true, SkipMulligan = false, History = false, Logging = false, FormatType = FormatType.FT_WILD };
            DungeonRunSetup.Configure(cfg, plan);
            var game = new Game(cfg);
            game.StartGame();
            if (game.FirstPlayer != game.Player1) throw new Exception("player not first");
            DungeonRunSetup.ApplyAfterStart(game, plan);
            r.BeginMatch(s);
            game.Process(ChooseTask.Mulligan(game.Player1, new List<int>()));
            var choice = game.Player2.Choice;
            var view = new MulliganView(choice.Choices.Select(id => new MulliganCard(id, game.IdEntityDic[id].Card.Id, game.IdEntityDic[id].Card.Cost)),
                false, pilot.Deck.CardIds, game.Player1.Hero.Card.Class);
            game.Process(ChooseTask.Mulligan(game.Player2, BotDriver.Mulligan(view, new DeckPlanMulligan(pilot.Plan)).ToList()));
            game.MainBegin();
            int decisionIndex = 0, actions = 0, decisions = 0, fallbacks = 0;
            bool hearthSeen = false, given = false, castHearth = false;
            var recent = new Queue<string>();
            void Act(PlayerTask task, string who)
            {
                recent.Enqueue($"t{game.Turn} {who}: {task}");
                if (recent.Count > 10) recent.Dequeue();
                if (++actions > 3000) throw new Exception("stuck after 3000 actions | " + string.Join(" | ", recent));
                if (task is ChooseTask pick && game.Step != Step.BEGIN_MULLIGAN)
                {
                    var decision = game.GetPendingDecisions().Single(x => x.ControllerId == task.Controller.Id);
                    if (!game.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(decision.Id, decision.ControllerId, pick.Choices.ToArray()), out var why))
                        throw new Exception($"engine rejected {who} choice ({why}) | " + string.Join(" | ", recent));
                }
                else if (!game.Process(task)) throw new Exception($"engine rejected {who} task | " + string.Join(" | ", recent));
            }
            while (game.State != State.COMPLETE && game.Turn <= plan.TurnCap)
            {
                if (game.CurrentPlayer == game.Player2)
                {
                    // The toki part's scripted cast: from TIMELINE_TURN on, the boss is given Alternate Timeline once and casts it.
                    if (part == "toki" && plan.Restarts.Contains(Timeline) && game.Turn >= timelineTurn)
                    {
                        if (!given && !game.Player2.HandZone.Any(p => p.Card.Id == Timeline) && game.Player2.HandZone.Count < 10)
                        {
                            var card = Entity.FromCard(game.Player2, Cards.FromId(Timeline));
                            Generic.AddHandPhase.Invoke(game.Player2, card);
                            game.DeathProcessingAndAuraUpdate();
                        }
                        given = true;
                        var cast = game.Player2.Options().OfType<PlayCardTask>().FirstOrDefault(t => t.Source?.Card.Id == Timeline);
                        if (cast != null) { Act(cast, "boss"); continue; }
                    }
                    var (task, trace) = BotDriver.Decide(game, game.Player2.PlayerId, pilot.Deck.Style,
                        st => golden.DecidePilot(st, new GoldenContext(game.Turn, decisionIndex, unchecked(997 * game.Turn + decisionIndex * 31), npcProfile)),
                        ref decisionIndex, golden.Options);
                    decisions++;
                    if (trace?.Kind == "fallback") { fallbacks++; Console.Error.WriteLine($"  FALLBACK {part} case {c.Number} {boss.Id} t{game.Turn}: {trace.FallbackReason}"); }
                    Act(task, "boss");
                }
                else
                {
                    var options = game.Player1.Options();
                    var hearth = options.OfType<PlayCardTask>().FirstOrDefault(t => t.Source?.Card.Id == hearthShell);
                    hearthSeen |= hearth != null;
                    if (hearth != null && part == "escape" && game.Turn >= escapeTurn) { castHearth = true; Act(hearth, "player"); continue; }
                    // The bot never sees Hearthstone (it would score the escape as a win and cast it at once: lane-x.md section 6).
                    var pool = options.Where(t => !(t is PlayCardTask p && p.Source?.Card.Id == hearthShell)).ToList();
                    var heroCard = pool.FirstOrDefault(t => t is PlayCardTask play && play.Source is Hero);
                    Act(heroCard ?? LocalBot.Choose(pool), "player");
                }
            }
            games++; totalFallbacks += fallbacks;
            string result = game.State != State.COMPLETE ? "turn-cap" : game.Player2.PlayState == PlayState.WON ? "boss" : game.Player1.PlayState == PlayState.WON ? "player" : "tie";
            if (result == "boss") bossWins++;
            if (result == "turn-cap") caps++;
            var end = DungeonRunSetup.Harvest(game);
            floorTurns += end.Turns;
            Check(game.Turn <= plan.TurnCap + 1, "game " + k + " ran past its cap " + plan.TurnCap, problems);
            if (part == "refuse") Check(!hearthSeen, "Hearthstone was playable on a finale", problems);
            if (part == "escape") Check(castHearth == end.Escaped, "Hearthstone cast " + castHearth + ", escaped " + end.Escaped, problems);
            if (part == "tekahn" || boss.Forms.Count > 0)
            {
                var form = end.BossForm > 0 ? boss.Forms[end.BossForm - 1] : null;
                bool bossDead = game.Player2.Hero.ToBeDestroyed || game.Player2.Hero.Health <= 0;
                if (plan.FormRatchet && form != null && form.HealthAtMost > 0 && !bossDead)
                    Check(game.Player2.Hero.Health <= form.HealthAtMost && game.Player2.Hero.BaseHealth <= form.HealthAtMost,
                        $"the ratchet: form {end.BossForm} at {game.Player2.Hero.Health}/{game.Player2.Hero.BaseHealth} above its threshold {form.HealthAtMost}", problems);
            }
            // Reported as LocalLadder reports it: a restart mark as a Fault, else the engine's result.
            var outcome = end.Restarted ? DungeonRunOutcome.Fault : result == "player" ? DungeonRunOutcome.Win : result == "turn-cap" ? DungeonRunOutcome.TurnLimit
                : result == "tie" ? DungeonRunOutcome.Tie : DungeonRunOutcome.Loss;
            bool heldRewind = s.Treasures.Contains(Rewind);
            int damageBefore = s.HeroDamage;
            var notes = r.RecordOutcome(s, profile, outcome, game.RunResult, end);
            var sum = s.Matches.Last();
            string what = result;
            if (end.Escaped)
            {
                escapes++; what = "escape";
                Check(sum.Escaped && sum.Outcome == DungeonRunOutcome.Win && s.Phase == DungeonRunPhase.Tavern && s.MajorsCleared == majors
                    && !s.Deck.Contains(hearthShell) && !s.ActiveTreasures.Contains(Hearthstone), "the escape was not recorded as one: " + string.Join(" | ", notes), problems);
                if (boss.HasFlag(DungeonRunEncounterData.PersistentDamage))
                    Check(profile.DamageOf(boss.Id)?.Remaining == Math.Max(1, end.BossRemainingHealth) || end.BossRemainingHealth >= (boss.Health ?? 0), "the escaped lord's damage was not banked", problems);
            }
            else if (end.Restarted)
            {
                if (end.RestartSource == Rewind) rewinds++; else timelines++;
                what = "restart:" + (end.RestartSource == Rewind ? "rewind" : "timeline");
                Check(sum.Restarted && s.Phase == DungeonRunPhase.PreMatch && s.RestartIndex == k + 1, "the restart was not recorded as one: " + string.Join(" | ", notes), problems);
                if (end.RestartSource == Rewind) Check(heldRewind && !s.Treasures.Contains(Rewind), "Rewind was not used up", problems);
                if (end.RestartSource == Timeline && end.HeroesUsed.Count <= 1) Check(s.RestartCarriedDamage == end.PlayerHeroDamage - (s.ActiveRules.Any(x => x.Id == "ULDA_720") ? damageBefore : 0),
                    "Alternate Timeline carried " + s.RestartCarriedDamage + " of " + end.PlayerHeroDamage, problems);
                Check(DungeonRun.RestartSourcesOf(s).GroupBy(x => x).All(g => g.Count() == 1), "a source restarted twice on one floor", problems);
            }
            else Check(s.RestartIndex == 0 && s.RestartSources == null, "a decided floor kept its restart state", problems);
            lines.Add($"g{k} {what} t{end.Turns}/{plan.TurnCap} hp {Math.Max(0, game.Player1.Hero.Health)} boss {end.BossRemainingHealth} d{decisions} f{fallbacks}");
            if (notes.Any(n => n.StartsWith("restart refused") || n.StartsWith("escape refused"))) lines.Add("(" + string.Join("; ", notes.Where(n => n.Contains("refused"))) + ")");
        }
        Check(floorTurns <= 2 * cap0, "the floor's games took " + floorTurns + " turns, more than 2 x " + cap0, problems);
        Check(!(s.Floor == floor && s.Node == node && s.Phase == DungeonRunPhase.PreMatch), "the floor was not decided in 4 games", problems);
        Check(s.RestartIndex == 0 && s.RestartCarriedDamage == 0 && s.RestartTurns == 0, "the restart state outlived the floor", problems);
        if (s.Phase == DungeonRunPhase.Tavern) { r.LeaveTavern(s); Check(s.Node > node, "leaving the escape tavern did not move on", problems); }
        verdict = s.Finished ? (s.Phase == DungeonRunPhase.Victory ? "victory" : "defeat") : "goes on (" + s.Phase + ")";
    }
    catch (Exception e) { verdict = "FAULT " + e.GetType().Name + ": " + e.Message.Split('\n')[0]; faults++; }
    if (problems.Count > 0) { failed += problems.Count; foreach (string p in problems) Console.Error.WriteLine($"  CHECK {part} case {c.Number} {bossId}: {p}"); }
    Console.WriteLine($"{part} case {c.Number} {c.Schedule} floor {c.Floor,2} {bossId,-24} {cls.Id,-11} {string.Join(", ", lines)} -> {verdict} checks {(problems.Count == 0 ? "ok" : "FAIL " + problems.Count)} {sw.ElapsedMilliseconds / 1000.0,6:F1}s");
}
foreach (var kv in SearchTelemetry.Details()) Console.Error.WriteLine($"SWALLOWED {kv.Value} x {kv.Key}");
Console.WriteLine($"ends9 {part}: games {games}, escapes {escapes}, restarts {rewinds} rewind / {timelines} timeline, boss wins {bossWins}, turn caps {caps}, faults {faults}, fallbacks {totalFallbacks}, failed checks {failed}, swallowed leaf exceptions {SearchTelemetry.SwallowedLeafErrors}");
return faults + failed + totalFallbacks + SearchTelemetry.SwallowedLeafErrors;
