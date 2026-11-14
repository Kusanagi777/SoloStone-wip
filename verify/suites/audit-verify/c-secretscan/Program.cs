using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.DungeonRun;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
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
string root = VerifyPaths.Data;
var data = DungeonRunData.Parse(File.ReadAllText(Path.Combine(root, "dungeon-run", "run-v1.json")),
    File.ReadAllText(Path.Combine(root, "dungeon-run", "pool-v1.json")), File.ReadAllText(Path.Combine(root, "npc", "decks-v1.json")));
int trials = args.Length > 0 ? int.Parse(args[0]) : 30;
const string Yeti = "CS2_182";
List<Card> Deck(int n, string id = Yeti) => Enumerable.Repeat(Cards.FromId(id), n).ToList();

// (where, class, cardId or null, power or null, treasure or null)
var items = new List<(string where, string cls, string card, string power)>();
foreach (var d in data.Decks) foreach (var id in d.Cards.Distinct()) items.Add(("deck " + d.Id, d.Class, id, null));
foreach (var b in data.Bundles) foreach (var id in b.Cards) items.Add(("bundle " + b.Id, b.Class == "NEUTRAL" ? "MAGE" : b.Class, id, null));
foreach (var c in data.Classes) foreach (var p in c.Powers) items.Add(("power " + c.Id, c.Id, null, p));
if (Environment.GetEnvironmentVariable("EXTRA_CARD") is string extra) foreach (var x in extra.Split(",")) items.Add(("extra", "DEATHKNIGHT", x, null));
foreach (var t in DungeonRunContent.All.Where(x => x.Kind == RunContentKind.ActiveTreasure)) foreach (var c in new[] { "MAGE", "PALADIN" }) items.Add(("active " + t.Key, c, t.ShellId, null));
items = items.GroupBy(i => (i.cls, i.card, i.power)).Select(g => (string.Join(";", g.Select(x => x.where).Distinct()), g.Key.cls, g.Key.card, g.Key.power)).ToList();

int totalHits = 0, totalFaults = 0;
var rowsOut = new List<string>();
var onlyCard = Environment.GetEnvironmentVariable("ONLY_CARD");
foreach (var item in items)
{
    if (onlyCard != null && item.card != onlyCard) continue;
    int hits = 0, faults = 0; var seen = new SortedSet<string>();
    for (int s = 0; s < trials; s++)
    {
        try
        {
            var cls = data.FindClass(item.cls);
            var plan = new RunMatchPlan
            {
                PlayerHero = Cards.FromId(cls.HeroCard), PlayerDeck = Deck(10), PlayerHeroPowerId = item.power ?? cls.DefaultPower,
                BossHero = Cards.FromId("HERO_01"), BossDeck = Deck(30), BossHealth = 30,
            };
            var cfg = new GameConfig { RandomSeed = 5000 + s * 7919, Shuffle = true, SkipMulligan = false, History = Environment.GetEnvironmentVariable("HISTORY") == "1", Logging = false, FormatType = FormatType.FT_WILD };
            DungeonRunSetup.Configure(cfg, plan);
            var g = new Game(cfg); g.StartGame(); DungeonRunSetup.ApplyAfterStart(g, plan);
            g.Process(ChooseTask.Mulligan(g.Player1, new List<int>())); g.Process(ChooseTask.Mulligan(g.Player2, new List<int>())); g.MainBegin();
            var rnd = new Random(s);
            var trace = new List<string>();
            bool Proc(PlayerTask t) { if (Environment.GetEnvironmentVariable("TRACE") == "1") Console.Error.WriteLine("seed " + s + " t" + g.Turn + " " + t.FullPrint() + " hand[" + string.Join(",", g.Player1.HandZone.Select(h => h.Card.Id)) + "]"); bool ok = g.Process(t); if (onlyCard != null) { var bad = g.IdEntityDic.Values.FirstOrDefault(x => x.Controller == g.Player1 && x.Card.IsSecret); trace.Add(string.Join(",", g.Player1.HandZone.Select(h => h.Card.Id + "/" + h.Card.Name)) + " :: " + t.FullPrint() + (bad != null ? "  => SECRET " + bad.Card.Id + " zone " + bad.Zone?.Type : "")); if (bad != null && trace.Count(z => z.Contains("=> SECRET")) == 1) Console.WriteLine("TRACE seed " + s + " | " + string.Join(" | ", trace)); } return ok; }
            var p1 = g.Player1;
            // some enemy and friendly bodies for targets
            foreach (var who in new[] { g.Player2, p1 }) { var m = (Minion)Entity.FromCard(who, Cards.FromId(Yeti)); Generic.SummonBlock(g, m, -1, who.Hero); }
            void Resolve()
            {
                int guard = 0;
                while (g.Player1.Choice != null && guard++ < 10)
                {
                    var ch = g.Player1.Choice.Choices;
                    var opts = g.Player1.Options().OfType<ChooseTask>().ToList();
                    if (opts.Count == 0) break;
                    Proc(opts[rnd.Next(opts.Count)]);
                }
            }
            for (int round = 0; round < 3 && g.State != State.COMPLETE; round++)
            {
                p1.BaseMana = 10; p1.UsedMana = 0;
                if (item.card != null)
                {
                    var e = Entity.FromCard(p1, Cards.FromId(item.card));
                    if (p1.HandZone.IsFull) break;
                    Generic.AddHandPhase.Invoke(p1, e);
                    var plays = p1.Options().Where(o => o is PlayCardTask pc && pc.Source == e).ToList();
                    if (plays.Count > 0) { Proc(plays[rnd.Next(plays.Count)]); Resolve(); }
                }
                else
                {
                    p1.Hero.HeroPower.IsExhausted = false;
                    var hp = p1.Options().OfType<HeroPowerTask>().ToList();
                    if (hp.Count > 0) { Proc(hp[rnd.Next(hp.Count)]); Resolve(); }
                }
                // play anything generated (spells/minions) to trigger chains
                for (int k = 0; k < 4; k++)
                {
                    var more = p1.Options().Where(o => o is PlayCardTask).ToList();
                    if (more.Count == 0) break;
                    Proc(more[rnd.Next(more.Count)]); Resolve();
                }
                foreach (var m in p1.BoardZone.ToList()) m.Destroy();
                g.DeathProcessingAndAuraUpdate(); Resolve();
                if (g.State == State.COMPLETE) break;
                Proc(EndTurnTask.Any(g.CurrentPlayer)); Resolve();
                if (g.State == State.COMPLETE) break;
                Proc(EndTurnTask.Any(g.CurrentPlayer)); Resolve();
            }
            // Phase 2: Secrets that started in the run player's deck are admitted (closed run pool); only a generated or
            // transferred one is a hit.
            var secrets = g.IdEntityDic.Values.Where(x => x.Controller == p1 && x.Card[GameTag.SECRET] != 0 && g.HistoryFacts.Query.KnownOrigin(x.Id, p1.Id) != true).Select(x => { int cr = x[GameTag.CREATOR] != 0 ? x[GameTag.CREATOR] : x[GameTag.DISPLAYED_CREATOR]; var ce = cr != 0 && g.IdEntityDic.ContainsKey(cr) ? g.IdEntityDic[cr].Card.Id + "/" + g.IdEntityDic[cr].Card.Name : "?"; return x.Card.Id + " " + x.Card.Name + " <- " + ce + " zone " + x.Zone?.Type; }).ToList();
            if (secrets.Count > 0) { hits++; foreach (var x in secrets) seen.Add(x); }
        }
        catch (Exception ex) { faults++; if (faults == 1) Console.Error.WriteLine("fault " + item.where + " " + (item.card ?? item.power) + ": " + ex.ToString()); }
    }
    totalHits += hits; totalFaults += faults;
    if (hits > 0)
    {
        string name = item.card != null ? Cards.FromId(item.card).Name : item.power;
        string row = $"SECRET {item.where} {item.card ?? item.power} '{name}' {hits}/{trials}: {string.Join(", ", seen.Take(6))}";
        Console.WriteLine(row);
    }
}
// Phase 3 review: in a catalog run match (the player may generate Secrets, the boss may not), the player's effects that generate
// for the boss (Spellslinger AT_007, Mulch AT_044, Merch Seller CORE_ETC_111) must pass the boss side's filters too: the boss
// never receives a Secret, nor a card of either generated-reject list.
var catAll = new HashSet<string>(DungeonRunAdmission.GeneratedRejects(true), StringComparer.Ordinal);
var catBoss = new HashSet<string>(DungeonRunAdmission.GeneratedRejectsBoss(true), StringComparer.Ordinal);
int oppTrials = 0, oppSecrets = 0, oppRejected = 0, oppFaults = 0, oppReceived = 0; var oppSeen = new SortedSet<string>(StringComparer.Ordinal);
int oppEach = Math.Max(60, trials * 5);
foreach (string gen in new[] { "AT_007", "AT_044", "CORE_ETC_111" })
    for (int s = 0; s < oppEach; s++)
    {
        try
        {
            var plan = new RunMatchPlan { PlayerHero = Cards.FromId("HERO_08"), PlayerDeck = Deck(10), BossHero = Cards.FromId("HERO_01"), BossDeck = Deck(30), BossHealth = 30,
                PlayerGeneratedSecrets = true, ExcludedGenerated = DungeonRunAdmission.GeneratedRejects(true), ExcludedGeneratedBoss = DungeonRunAdmission.GeneratedRejectsBoss(true) };
            var cfg = new GameConfig { RandomSeed = 9100 + s * 7919, Shuffle = false, SkipMulligan = false, History = false, Logging = false, FormatType = FormatType.FT_WILD };
            DungeonRunSetup.Configure(cfg, plan);
            var g = new Game(cfg); g.StartGame(); DungeonRunSetup.ApplyAfterStart(g, plan);
            g.Process(ChooseTask.Mulligan(g.Player1, new List<int>())); g.Process(ChooseTask.Mulligan(g.Player2, new List<int>())); g.MainBegin();
            var p1 = g.Player1; var p2 = g.Player2; p1.BaseMana = 10; p1.UsedMana = 0;
            var before = new HashSet<int>(p2.HandZone.Concat(p2.DeckZone).Select(x => x.Id));
            if (gen == "CORE_ETC_111")
            {
                var m = (Minion)Entity.FromCard(p1, Cards.FromId(gen)); Generic.SummonBlock(g, m, -1, p1.Hero);
                g.Process(EndTurnTask.Any(p1));
            }
            else
            {
                var target = (Minion)Entity.FromCard(p2, Cards.FromId(Yeti)); Generic.SummonBlock(g, target, -1, p2.Hero);
                var e = Entity.FromCard(p1, Cards.FromId(gen)); Generic.AddHandPhase.Invoke(p1, e);
                var play = p1.Options().OfType<PlayCardTask>().FirstOrDefault(o => o.Source == e && (gen != "AT_044" || o.Target == target));
                if (play == null) throw new Exception(gen + " not playable");
                g.Process(play);
            }
            oppTrials++;
            foreach (var x in p2.HandZone.Concat(p2.DeckZone).Where(x => !before.Contains(x.Id)))
            {
                oppReceived++;
                if (x.Card.IsSecret) { oppSecrets++; oppSeen.Add(gen + ">" + x.Card.Id); }
                if (catAll.Contains(x.Card.Id) || catBoss.Contains(x.Card.Id)) { oppRejected++; oppSeen.Add(gen + ">rejected:" + x.Card.Id); }
            }
        }
        catch (Exception ex) { oppFaults++; if (oppFaults < 3) Console.Error.WriteLine("opposite fault " + gen + " " + s + ": " + ex.GetType().Name + " " + ex.Message); }
    }
Console.WriteLine($"opposite generation (catalog plan): trials {oppTrials} boss-received {oppReceived} boss-secrets {oppSecrets} boss-rejected {oppRejected} faults {oppFaults} {string.Join(",", oppSeen.Take(10))}");
Console.WriteLine($"items {items.Count} trials {trials} secret-items-hit {totalHits} faults {totalFaults} opposite-boss-secrets {oppSecrets} opposite-boss-rejected {oppRejected} opposite-faults {oppFaults}");
return oppSecrets + oppRejected + oppFaults > 0 || oppReceived == 0 ? 1 : 0;
