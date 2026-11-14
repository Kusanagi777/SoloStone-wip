using System; using System.Collections.Generic; using System.IO; using System.Linq; using System.Text; using System.Text.Json;
using System.Security.Cryptography;
using SabberStoneCore.Config; using SabberStoneCore.Enums; using SabberStoneCore.Model; using SabberStoneCore.Tasks.PlayerTasks;

// Seeded AI-vs-AI (LocalBot both sides) normal games with NPC library decks, no Dungeon Run setup.
int n = int.Parse(args[0]); string outPath = args[1];
var doc = JsonDocument.Parse(File.ReadAllText(VerifyPaths.Repo("data/npc/decks-v1.json")));
var decks = new List<(string id, CardClass cls, List<Card> cards)>();
foreach (var d in doc.RootElement.GetProperty("decks").EnumerateArray())
{
    if (d.TryGetProperty("sideboards", out var sb) && ((sb.ValueKind == JsonValueKind.Array && sb.GetArrayLength() > 0) || (sb.ValueKind == JsonValueKind.Object && sb.EnumerateObject().Any()))) continue;
    var cards = new List<Card>(); bool ok = true;
    foreach (var c in d.GetProperty("cards").EnumerateArray())
    { var card = Cards.FromId(c.GetProperty("id").GetString()); if (card == null) { ok = false; break; } for (int i = 0; i < c.GetProperty("count").GetInt32(); i++) cards.Add(card); }
    if (ok) decks.Add((d.GetProperty("id").GetString(), Enum.Parse<CardClass>(d.GetProperty("class").GetString()), cards));
}
var log = new StringBuilder(); int faults = 0, p1w = 0, p2w = 0, other = 0;
for (int g = 0; g < n; g++)
{
    var a = decks[g % decks.Count]; var b = decks[(g * 7 + 3) % decks.Count];
    string line;
    try
    {
        var cfg = new GameConfig { StartPlayer = g % 2 + 1, Player1HeroClass = a.cls, Player2HeroClass = b.cls, Player1Deck = a.cards.ToList(), Player2Deck = b.cards.ToList(),
            Shuffle = true, SkipMulligan = true, RandomSeed = 7000 + g, History = false, Logging = false, FormatType = FormatType.FT_WILD };
        var game = new Game(cfg); game.StartGame();
        int actions = 0; var trace = new StringBuilder();
        while (game.State != State.COMPLETE && game.Turn <= 90)
        {
            if (++actions > 3000) throw new Exception("stuck");
            var cp = game.CurrentPlayer; PlayerTask task;
            var pending = game.GetPendingDecisions();
            var task0 = LocalBot.Choose(cp.Choice != null ? cp.Options() : cp.Options());
            task = task0;
            trace.Append(task.PlayerTaskType).Append(task.Source?.Card.Id).Append(task.Target?.Id).Append(';');
            if (task is ChooseTask pick)
            {
                var decision = game.GetPendingDecisions().First(x => x.ControllerId == task.Controller.Id);
                if (!game.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(decision.Id, decision.ControllerId, pick.Choices.ToArray()), out var why)) throw new Exception("choice rejected " + why);
            }
            else if (!game.Process(task)) throw new Exception("task rejected " + task);
        }
        string res = game.State != State.COMPLETE ? "cap" : game.Player1.PlayState == PlayState.WON ? "p1" : game.Player2.PlayState == PlayState.WON ? "p2" : "tie";
        if (res == "p1") p1w++; else if (res == "p2") p2w++; else other++;
        line = $"{g} {a.id} vs {b.id} -> {res} t{game.Turn} hp {game.Player1.Hero.Health}/{game.Player2.Hero.Health} acts {actions} th {H(trace.ToString())}";
    }
    catch (Exception e) { faults++; line = $"{g} {a.id} vs {b.id} -> FAULT {e.GetType().Name}: {e.Message.Split('\n')[0]}"; }
    log.Append(line).Append('\n');
}
File.WriteAllText(outPath, log.ToString());
Console.WriteLine($"decks {decks.Count} games {n} p1 {p1w} p2 {p2w} other {other} faults {faults} digest {H(log.ToString())}");
return faults;
static string H(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s))).Substring(0, 16);
