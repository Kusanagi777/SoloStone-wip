// c-dkdh-play: phase 6 F16. Every card of the SoloStone-curated Death Knight / Demon Hunter pool entries
// (tools/dungeon-run-catalog/curation.json customBuckets) is played in a real engine game through the run setup path:
// a DK or DH act run (catalog + acts-v1) drafts its first floor preferring a curated entry, the card is added to the run deck
// (DungeonRunDeck.TryAddAll, origin draft, 6 copies so it is drawn), DungeonRun.BuildPlan -> DungeonRunSetup.Configure ->
// Game -> ApplyAfterStart, and the match is played to the end: the player side by LocalBot over the engine's legal options,
// the boss by its GoldenPilot (DungeonRunBossPilot.Build with the run classes, as NativeMatch does). A card passes when the
// player played it at least once and the game finished without an engine fault. Run twice: the output must be identical.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SabberStoneCore.CardSets.DungeonRun;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.PlayerTasks;

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

int failures = 0, passes = 0;
void Check(string name, bool ok, string detail = "")
{
    if (ok) passes++; else failures++;
    if (!ok || Environment.GetEnvironmentVariable("VERBOSE") == "1")
        Console.WriteLine((ok ? "PASS " : "FAIL ") + name + (detail.Length > 0 ? "  (" + detail + ")" : ""));
}

string root = VerifyPaths.Data;
string R(string f) => File.ReadAllText(Path.Combine(root, f));
var data = DungeonRunData.Parse(R("dungeon-run/run-v1.json"), R("dungeon-run/pool-v1.json"), R("npc/decks-v1.json"),
    R("dungeon-run/catalog-v1.json"), R("dungeon-run/schedule-v1.json"), R("dungeon-run/tavern-v1.json"));
var run = new DungeonRun(data);
var fresh = new DungeonRunProfile();
var testBudget = new Budget(6000, 2000, 300, 6, 3, 100, 8, turnSearchSeconds: 6, turnWallSeconds: 8);
const int Copies = 6, Attempts = 3;

var curated = data.Bundles.Where(b => b.Provenance.Any(p => p.Adventure == "SoloStone-curated")).OrderBy(b => b.Id, StringComparer.Ordinal).ToList();
Check("fourteen SoloStone-curated entries (7 Death Knight, 7 Demon Hunter; phase 6 review F18)", curated.Count == 14
    && curated.Count(b => b.ClassSet.SequenceEqual(new[] { "DEATHKNIGHT" })) == 7 && curated.Count(b => b.ClassSet.SequenceEqual(new[] { "DEMONHUNTER" })) == 7, string.Join(",", curated.Select(b => b.Id)));
var cards = curated.SelectMany(b => b.Candidates.Select(c => (Cls: b.ClassSet[0], Id: c.Id))).Distinct().OrderBy(x => x.Cls, StringComparer.Ordinal).ThenBy(x => x.Id, StringComparer.Ordinal).ToList();

// An act run of the class that has won floor 1 and drafted (preferring a curated entry), standing before floor 2.
DungeonRunState Reach(string cls, long seed, out bool curatedDraft)
{
    var row = data.Class(cls);
    var s = run.NewRun(fresh, cls, row.DefaultDeck, row.Powers[0], seed);
    if (!s.IsActRun) throw new Exception("not an act run");
    curatedDraft = false;
    var scratch = new DungeonRunProfile();
    while (!(s.Floor == 2 && s.Phase == DungeonRunPhase.PreMatch))
    {
        switch (s.Phase)
        {
            case DungeonRunPhase.PreMatch: run.BeginMatch(s); run.RecordOutcome(s, scratch, DungeonRunOutcome.Win); break;
            case DungeonRunPhase.TreasureDraft: run.PickTreasure(s, s.OpenDraft.Offered[0]); break;
            case DungeonRunPhase.Modifier: run.PickModifier(s, s.ModifierOffer.Offered[0]); break;
            case DungeonRunPhase.Tavern: run.LeaveTavern(s); break;
            case DungeonRunPhase.BundleDraft:
            {
                string pick = s.OpenDraft.Offered.FirstOrDefault(id => curated.Any(b => b.Id == id)) ?? s.OpenDraft.Offered[0];
                if (curated.Any(b => b.Id == pick)) curatedDraft = true;
                if (s.OpenDraft.Takes == 0) { run.PickBundle(s, pick); break; }
                string bundle = s.OpenDraft.Choosing ?? pick;
                var left = run.OfferCards(s.OpenDraft, bundle);
                foreach (string kept in s.OpenDraft.Chosen) left.Remove(kept);
                run.PickDraftCard(s, bundle, left[0]);
                break;
            }
            default: throw new Exception("walk stopped at " + s.Phase);
        }
    }
    return s;
}

(string Result, int Played, int Turns, string Fault) Play(DungeonRunState s, string cardId)
{
    int played = 0, turns = 0;
    try
    {
        var plan = run.BuildPlan(s);
        if (plan.PlayerDeck.Count(c => c.Id == cardId) < Copies) throw new Exception("plan deck lacks the card copies");
        var boss = data.Bosses.First(b => b.Id == s.BossSequence[s.Floor - 1]);
        var pilot = DungeonRunBossPilot.Build(boss, plan, data, runClasses: run.RunClasses(s));
        var profile = pilot.Profile.WithBudget(testBudget);
        var golden = new GoldenPilot(profile, pilot.Plan, null, pilot.SecretPrior);
        var cfg = new GameConfig { RandomSeed = run.MatchSeed(s), Shuffle = true, SkipMulligan = false, History = false, Logging = false, FormatType = FormatType.FT_WILD };
        DungeonRunSetup.Configure(cfg, plan);
        var game = new Game(cfg);
        game.StartGame();
        DungeonRunSetup.ApplyAfterStart(game, plan);
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
            if (++actions > 1500) throw new Exception("stuck after 1500 actions | " + string.Join(" | ", recent));
            if (who == "player" && task is PlayCardTask pc && pc.Source?.Card.Id == cardId) played++;
            if (task is ChooseTask pick && game.Step != Step.BEGIN_MULLIGAN)
            {
                var decision = game.GetPendingDecisions().Single(d => d.ControllerId == task.Controller.Id);
                if (!game.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(decision.Id, decision.ControllerId, pick.Choices.ToArray()), out var why))
                    throw new Exception($"engine rejected {who} choice ({why}) | " + string.Join(" | ", recent));
            }
            else if (!game.Process(task)) throw new Exception($"engine rejected {who} task | " + string.Join(" | ", recent));
        }
        while (game.State != State.COMPLETE && game.Turn <= 100)
        {
            if (game.CurrentPlayer == game.Player2)
            {
                var (task, _) = BotDriver.Decide(game, game.Player2.PlayerId, pilot.Deck.Style,
                    st => golden.DecidePilot(st, new GoldenContext(game.Turn, decisionIndex, unchecked(997 * game.Turn + decisionIndex * 31), profile)),
                    ref decisionIndex, golden.Options);
                Act(task, "boss");
            }
            else Act(LocalBot.Choose(game.Player1.Options()), "player");
        }
        turns = game.Turn;
        string result = game.State != State.COMPLETE ? "turn-cap" : game.Player2.PlayState == PlayState.WON ? "boss" : game.Player1.PlayState == PlayState.WON ? "player" : "tie";
        return (result, played, turns, null);
    }
    catch (Exception e) { return ("fault", played, turns, e.GetType().Name + ": " + e.Message.Split('\n')[0]); }
}

int gamesPlayed = 0, curatedDrafts = 0;
var notPlayed = new List<string>(); var faults = new List<string>(); var addFailed = new List<string>();
for (int i = 0; i < cards.Count; i++)
{
    var (cls, id) = cards[i];
    bool ok = false;
    for (int attempt = 0; attempt < Attempts && !ok; attempt++)
    {
        long seed = 104729L * (i + 1) + 7L * attempt;
        var s = Reach(cls, seed, out bool cd);
        if (cd) curatedDrafts++;
        var add = DungeonRunDeck.TryAddAll(run, s, Enumerable.Repeat(id, Copies), DungeonRunDeck.OriginDraft);
        if (!add.Ok) { addFailed.Add(id + ":" + add.Error); break; }
        var r = Play(s, id);
        gamesPlayed++;
        Console.WriteLine($"{cls,-12} {id,-14} {Cards.FromId(id).Name,-28} seed {seed,8} boss {s.BossSequence[s.Floor - 1],-18} -> {r.Result,-8} turns {r.Turns,3} played {r.Played}" + (r.Fault != null ? " " + r.Fault : ""));
        if (r.Fault != null) { faults.Add(id + ": " + r.Fault); break; }
        ok = r.Played > 0;
    }
    if (!ok && !faults.Any(f => f.StartsWith(id + ":")) && !addFailed.Any(f => f.StartsWith(id + ":"))) notPlayed.Add(id);
}
Check($"every curated card ({cards.Count}) joins a Death Knight / Demon Hunter act-run deck through DungeonRunDeck.TryAddAll", addFailed.Count == 0, string.Join(" ", addFailed));
Check("no engine fault in any game", faults.Count == 0, string.Join(" | ", faults.Take(5)));
Check("every curated card is played by the player in a real engine game through the run setup path", notPlayed.Count == 0, string.Join(" ", notPlayed));
Check("the first-floor draft took a curated entry on most walks (the entries are on offer)", curatedDrafts * 2 > gamesPlayed, curatedDrafts + " of " + gamesPlayed);
Console.WriteLine($"cards {cards.Count} games {gamesPlayed} curated first drafts {curatedDrafts} faults {faults.Count} not played {notPlayed.Count}");
Console.WriteLine($"c-dkdh-play: {passes} passed, {failures} failed");
return failures == 0 ? 0 : 1;
