using System;
using System.Collections.Generic;
using System.Diagnostics;
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

string root = VerifyPaths.Data;
// CATALOG_RUN=1: phase 3 I2. The data loads with the shared draft catalog, so every run is a catalog run (CatalogVersion 1):
// catalog drafts, the player's generated Secrets on, and the boss on its own pilot options with the run's widened Secret
// prior (DungeonRunAdmission.CatalogSecretPrior over DungeonRun.RunClasses; the dual-class row MAGE_ROGUE joins the sweep).
// ACT_RUN=1: phase 4 I1. The data loads with the catalog and the act/floor schedules (schedule-v1.json), so every run is an
// act run on the default schedule (this data has no encounter file, so schedule-v1.json "defaultWithoutEncounters": acts-v2; the "act run:" line prints run.DefaultSchedule(0), the schedule the walk follows; and a catalog run): the run walks its nodes (two-card drafts, taverns, modifiers) to the boss's floor,
// and the plan carries the act's health curve. A band-1 boss is fought on floor 1, a band-3 boss as the act I major (floor
// 5), a band-2 boss on floor 6 (act II, after its modifier) and a final as the act III major (floor 15, a 40-odd card deck).
bool actRun = Environment.GetEnvironmentVariable("ACT_RUN") == "1";
bool catalogRun = Environment.GetEnvironmentVariable("CATALOG_RUN") == "1" || actRun;
var data = DungeonRunData.Parse(File.ReadAllText(Path.Combine(root, "dungeon-run", "run-v1.json")),
    File.ReadAllText(Path.Combine(root, "dungeon-run", "pool-v1.json")), File.ReadAllText(Path.Combine(root, "npc", "decks-v1.json")),
    catalogRun ? File.ReadAllText(Path.Combine(root, "dungeon-run", "catalog-v1.json")) : null,
    actRun ? File.ReadAllText(Path.Combine(root, "dungeon-run", "schedule-v1.json")) : null,
    actRun ? File.ReadAllText(Path.Combine(root, "dungeon-run", "tavern-v1.json")) : null);
// ACT_RUN=1 (phase 5 I2): the walk buys every tavern offer it can (first candidate of each choice), so the plans carry deck
// enchantments and opening flags, and the engine applies them and the act's boss start-mana bonus.
// SECRET_FIXTURE=1: phase 2 review. Class Secret bundles join the run pool (as a restored pool would hold them), drafts take
// a Secret bundle when one is offered, and the boss runs with its own pilot options: the closed Secret prior from the data,
// the golden fair view (GoldenPilot.Options) and secret-worlds settlement. Run it twice and compare the output.
bool secretFixture = Environment.GetEnvironmentVariable("SECRET_FIXTURE") == "1";
if (secretFixture)
{
    var secretBundles = new (string cls, string[] ids)[] {
        ("MAGE", new[] { "EX1_287", "EX1_295", "EX1_594" }), ("HUNTER", new[] { "EX1_610", "EX1_611", "EX1_554" }),
        ("PALADIN", new[] { "EX1_136", "EX1_130", "EX1_132" }), ("ROGUE", new[] { "LOOT_204", "LOOT_210", "LOOT_214" }) };
    foreach (var (cls, ids) in secretBundles)
        for (int copy = 0; copy < 2; copy++)
            data.Bundles.Add(new DungeonRunBundle { Id = "fixture_secrets_" + cls.ToLowerInvariant() + "_" + copy, Class = cls, Theme = "secrets", Cards = ids.ToList() });
}
var run = new DungeonRun(data);
var all = new DungeonRunProfile(); all.Unlocked.AddRange(data.Unlocks.Keys);
int gamesPerBoss = args.Length > 0 ? int.Parse(args[0]) : 2;
// Reduced search for a headless smoke test (the live budget is NpcCatalog.All[0].Budget: 28000/7000/500).
var testBudget = new Budget(6000, 2000, 300, 6, 3, 100, 8, turnSearchSeconds: 6, turnWallSeconds: 8);

// A run that reached the boss's floor: a class with its default (starter) deck and basic power, first-offer picks, treasures included.
// Game k of the sweep plays class k % 9 with deck (k / 9) % 3 and power ((k / 9) + (k / 27)) % 3: 40 games cover all 27 starter
// decks and all 27 powers ((k / 3) % 3 gave each class one power: k = c + 9m keeps k / 3 % 3 fixed).
int sweep = 0;
DungeonRunState ReachFloor(DungeonRunClass cls, int floor, long seed)
{
    int k = sweep++;
    int n = data.Classes.Count; // 11: k < 33 covers every class x deck and every class x power once
    var s = run.NewRun(all, cls.Id, cls.Decks[(k / n) % cls.Decks.Count], cls.Powers[((k / n) + (k / (3 * n))) % cls.Powers.Count], seed, signatureId: run.SignatureOffer(all, run.Data.FindClass(cls.Id), seed).FirstOrDefault());
    var scratch = new DungeonRunProfile();
    if (actRun)
    {
        if (!s.IsActRun) throw new Exception("not an act run");
        while (!(s.Floor == floor && s.Phase == DungeonRunPhase.PreMatch))
        {
            switch (s.Phase)
            {
                case DungeonRunPhase.PreMatch: run.BeginMatch(s); run.RecordOutcome(s, scratch, DungeonRunOutcome.Win); break;
                case DungeonRunPhase.TreasureDraft: run.PickTreasure(s, s.OpenDraft.Offered[0]); break;
                case DungeonRunPhase.Modifier: run.PickModifier(s, s.ModifierOffer.Offered[0]); break;
                case DungeonRunPhase.Tavern:
                    foreach (string offer in s.Tavern.Offers.ToList())
                    {
                        var bought = DungeonRunTavernNode.Buy(run, s, offer);
                        while (bought.Ok && s.Tavern.Pending != null) bought = DungeonRunTavernNode.Choose(run, s, s.Tavern.Pending.Candidates[0]);
                        if (s.Tavern.Pending != null) DungeonRunTavernNode.Back(s);
                    }
                    run.LeaveTavern(s);
                    break;
                case DungeonRunPhase.BundleDraft:
                    if (s.OpenDraft.Takes == 0) { run.PickBundle(s, s.OpenDraft.Offered[0]); break; }
                    string bundle = s.OpenDraft.Choosing ?? s.OpenDraft.Offered[0];
                    var left = run.OfferCards(s.OpenDraft, bundle);
                    foreach (string kept in s.OpenDraft.Chosen) left.Remove(kept);
                    run.PickDraftCard(s, bundle, left[0]);
                    break;
                default: throw new Exception("act walk stopped at " + s.Phase);
            }
        }
        return s;
    }
    while (s.Floor < floor)
    {
        if (s.Phase == DungeonRunPhase.PreMatch) { run.BeginMatch(s); run.RecordOutcome(s, scratch, DungeonRunOutcome.Win); }
        else if (s.Phase == DungeonRunPhase.TreasureDraft) run.PickTreasure(s, s.OpenDraft.Offered[0]);
        else run.PickBundle(s, secretFixture ? s.OpenDraft.Offered.FirstOrDefault(b => b.StartsWith("fixture_secrets_", StringComparison.Ordinal)) ?? s.OpenDraft.Offered[0] : s.OpenDraft.Offered[0]);
    }
    return s;
}

var rows = new List<string>();
int faults = 0, games = 0, bossWins = 0, totalFallbacks = 0, secretGames = 0, playerSecretGames = 0;
var priorClasses = new SortedSet<string>(StringComparer.Ordinal);
var powersPlayed = new HashSet<string>(); var decksPlayed = new HashSet<string>();
foreach (var boss in data.Bosses.SkipWhile(b => Environment.GetEnvironmentVariable("BOSS_FROM") is string from && b.Id != from))
{
    int floor = actRun ? (boss.Final ? 15 : boss.Band == 1 ? 1 : boss.Band == 2 ? 6 : 5) : boss.Final ? 8 : boss.Band == 1 ? 1 : boss.Band == 2 ? 4 : 7;
    for (int g = 0; g < gamesPerBoss; g++)
    {
        var hero = data.Classes[sweep % data.Classes.Count];
        var s = ReachFloor(hero, floor, (Environment.GetEnvironmentVariable("SEED_BASE") is string seedBase ? long.Parse(seedBase) : 9000) + data.Bosses.IndexOf(boss) * 10 + g);
        s.BossSequence[floor - 1] = boss.Id;
        string result; int turns = 0; long ms = 0; int decisions = 0, fallbacks = 0, health = 0; bool playerSecretSeen = false;
        var sw = Stopwatch.StartNew();
        try
        {
            var plan = run.BuildPlan(s);
            health = plan.BossHealth;
            // GEN_MODE (attribution only): none = no generation exclusions; table = the pre-review table-id list for both sides.
            if (Environment.GetEnvironmentVariable("GEN_MODE") == "none") { plan.ExcludedGenerated = null; plan.ExcludedGeneratedBoss = null; }
            else if (Environment.GetEnvironmentVariable("GEN_MODE") == "table") { plan.ExcludedGenerated = DungeonRunAdmission.Rules.Where(r => r.Ids != null && (r.Admit & AdmitScope.Generated) == 0).SelectMany(r => r.Ids).Distinct().ToList(); plan.ExcludedGeneratedBoss = null; }
            bool priored = secretFixture || catalogRun;
            if (catalogRun && (s.CatalogVersion != 1 || !plan.PlayerGeneratedSecrets)) throw new Exception("not a catalog run plan");
            var pilot = catalogRun ? DungeonRunBossPilot.Build(boss, plan, data, runClasses: run.RunClasses(s))
                : secretFixture ? DungeonRunBossPilot.Build(boss, plan, data) : DungeonRunBossPilot.Build(boss, plan);
            var profile = pilot.Profile.WithBudget(testBudget);
            var golden = new GoldenPilot(profile, pilot.Plan, null, priored ? pilot.SecretPrior : null);
            if (priored) priorClasses.Add((catalogRun ? s.Class : plan.PlayerHero.Class.ToString()) + "=" + (pilot.SecretPrior?.Count ?? 0));
            var cfg = new GameConfig { RandomSeed = run.MatchSeed(s), Shuffle = true, SkipMulligan = false, History = false, Logging = false, FormatType = FormatType.FT_WILD };
            DungeonRunSetup.Configure(cfg, plan);
            var game = new Game(cfg);
            game.StartGame();
            if (game.FirstPlayer != game.Player1) throw new Exception("player not first");
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
                if (task is ChooseTask pick && game.Step != Step.BEGIN_MULLIGAN)
                {
                    // Engine-source choices are answered as decisions, exactly as NativeMatch does (NativeMatch.cs:1669, 1620).
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
                    var (task, trace) = BotDriver.Decide(game, game.Player2.PlayerId, pilot.Deck.Style,
                        st => golden.DecidePilot(st, new GoldenContext(game.Turn, decisionIndex, unchecked(997 * game.Turn + decisionIndex * 31), profile)),
                        ref decisionIndex, priored ? golden.Options : null);
                    decisions++;
                    if (trace?.Kind == "fallback") { fallbacks++; Console.Error.WriteLine($"  FALLBACK {boss.Id} t{game.Turn}: {trace.FallbackReason} | p1 secrets [{string.Join(",", game.Player1.SecretZone.Select(x => x.Card.Id + " " + x.Card.Name))}] p2 secrets [{string.Join(",", game.Player2.SecretZone.Select(x => x.Card.Id + " " + x.Card.Name))}]"); }
                    Act(task, "boss");
                }
                else Act(LocalBot.Choose(game.Player1.Options()), "player");
                playerSecretSeen |= game.Player1.SecretZone.Any(x => x.IsSecret);
            }
            turns = game.Turn;
            if (game.IdEntityDic.Values.Any(x => x.Card.IsSecret && x.Zone?.Type == Zone.SECRET)) secretGames++;
            if (playerSecretSeen) playerSecretGames++;
            result = game.State != State.COMPLETE ? "turn-cap" : game.Player2.PlayState == PlayState.WON ? "boss" : game.Player1.PlayState == PlayState.WON ? "player" : "tie";
            if (result == "boss") bossWins++;
        }
        catch (Exception e) { result = "FAULT " + e.GetType().Name + ": " + e.Message.Split('\n')[0]; faults++; }
        ms = sw.ElapsedMilliseconds; games++; totalFallbacks += fallbacks; powersPlayed.Add(s.Power); decksPlayed.Add(s.StarterDeck);
        string row = $"{boss.Id,-18} b{(boss.Final ? "F" : boss.Band.ToString())} floor {floor} {hero.Id + "/" + s.StarterDeck + "/" + s.Power,-48} deck {s.Deck.Count,2} -> {result,-8} turns {turns,3} decisions {decisions,3} fallbacks {fallbacks,2}"
            + (actRun ? $" health {health,2} mana+{run.BossStartManaBonus(s)} mods {s.Cards.Count(c => c.Mods.Count > 0),2} flags {s.Cards.Count(c => c.Opening)}" : "") + $" {ms / 1000.0,6:F1}s";
        Console.WriteLine(row); rows.Add(row);
    }
}
if (secretFixture) Console.WriteLine("secret fixture: closed priors " + string.Join(" ", priorClasses) + "; games with a player Secret in play " + playerSecretGames);
if (actRun) Console.WriteLine("act run: " + run.DefaultSchedule(0) + " schedule, boss health from the act curve, the start-mana bonus applied (DungeonRunSetup.ApplyAfterStart), tavern purchases on the walk (deck enchantments and opening flags in the plan)");
if (catalogRun) Console.WriteLine("catalog run: widened priors " + string.Join(" ", priorClasses) + "; games with a player Secret in play " + playerSecretGames);
// Phase 7 review: a beam leaf exception the search swallows is counted (SearchTelemetry) and must be 0; details on stderr.
foreach (var kv in SearchTelemetry.Details()) Console.Error.WriteLine($"SWALLOWED {kv.Value} x {kv.Key}");
Console.Error.WriteLine($"pending-choice leaves scored as they stand {SearchTelemetry.PendingChoiceLeaves}");
Console.WriteLine($"games {games}, boss wins {bossWins}, faults {faults}, fallbacks {totalFallbacks}, games with a Secret in play {secretGames}, decks {decksPlayed.Count}, powers {powersPlayed.Count}, swallowed leaf exceptions {SearchTelemetry.SwallowedLeafErrors}");
return faults + SearchTelemetry.SwallowedLeafErrors;
