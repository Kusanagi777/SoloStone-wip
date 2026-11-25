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

// Phase 8 encounter bosspilot (from audit-verify\c-bosspilot): every listed encounter is played in a real engine game through
// the run setup path (DungeonRun.BuildPlan -> DungeonRunBossPilot.Build -> DungeonRunSetup.Configure / ApplyAfterStart) with
// the fair AI piloting the boss. A run on acts-v3h walks its real nodes to the first floor whose pool set holds the boss's
// tier; the boss is then put on that floor (as the older harness does).
//   BOSS=id,id        only these encounters           FAMILY=NN        only rows of that family
//   BOSS_FROM=id      start at this row               BOSS_COUNT=n     at most n rows
//   RULE=id           that global rule is active in every fight (injected as an anomaly of the fight's act)
//   CARRIED=n         a persistent-damage boss starts with n damage banked in the profile (its phase follows)
//   SCHEDULE=id       the schedule to walk (default acts-v3h)          SEED_BASE=n   seed base (default 9000)
//   SCHEDULE_JSON / ENCOUNTERS_JSON = path   fixture data files instead of the shipped ones (finale and relay fixtures)
//   FINALE=1          play the schedule's finale act in order (one game per fixed boss) instead of the rows
//   PLAYER_HEALTH=n   the run hero starts with n Health left (relay fixtures: every relay hero enters)
//   GAMES=n           games per boss (or per variant); the first argument does the same (default 2)
//   WATCH=id,id       counts how often the boss played each of these cards from hand and how many Secrets the boss held at most
//                     (the final line then ends with "watched ..."; a fixture mode asserts the card under test was really played)
//   DEFAULT_SWEEP=n   the close-out (family 25): n runs started as a new run is, with no named schedule (the default roll:
//                     the schedule file's defaults, acts-v4h or acts-v4t since step 9), the hero rows in turn; run i walks its
//                     real nodes to floor 1 + 7i mod its floor count and plays the boss its sequence drew there (no row is put on
//                     a floor); SCHEDULE and FINALE are ignored (each run walks the schedule it rolled);
//                     DEFAULT_SWEEP_FROM=i0 starts at run i0 (the seed is SEED_BASE + i, the row data.Classes[i mod count])
// A boss with shrine variants plays its games once per variant (the seeds that roll that variant).
// Family 02 variant (p8f02/make_bp_collect.py):
//   PLAYER_CARDS=id,id  these cards replace the first cards of the run player's match deck (the deck size stays); the final
//                       line then also counts how often the run player played each WATCH card.
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
    File.ReadAllText(Path.Combine(root, "dungeon-run", "catalog-v1.json")),
    File.ReadAllText(Env("SCHEDULE_JSON") ?? Path.Combine(root, "dungeon-run", "schedule-v1.json")),
    File.ReadAllText(Path.Combine(root, "dungeon-run", "tavern-v1.json")),
    File.ReadAllText(Env("ENCOUNTERS_JSON") ?? Path.Combine(root, "dungeon-run", "encounters-v1.json")));
var run = new DungeonRun(data);
var all = new DungeonRunProfile(); all.Unlocked.AddRange(data.Unlocks.Keys);
int gamesPerBoss = args.Length > 0 ? int.Parse(args[0]) : Env("GAMES") is string gm ? int.Parse(gm) : 2;
var testBudget = new Budget(6000, 2000, 300, 6, 3, 100, 8, turnSearchSeconds: 6, turnWallSeconds: 8);
string scheduleId = Env("SCHEDULE") ?? "acts-v3h";
var schedule = data.Schedules.Find(scheduleId) ?? throw new Exception("unknown schedule " + scheduleId);
string forcedRule = Env("RULE");
int carried = Env("CARRIED") is string cd ? int.Parse(cd) : 0;
int playerHealth = Env("PLAYER_HEALTH") is string ph ? int.Parse(ph) : 0;
bool finale = Env("FINALE") == "1";
int defaultSweep = Env("DEFAULT_SWEEP") is string dsw ? int.Parse(dsw) : 0;
int sweepFrom = Env("DEFAULT_SWEEP_FROM") is string dsf ? int.Parse(dsf) : 0;
string[] watch = Env("WATCH")?.Split(',').Select(id => id.Trim()).ToArray();
var watched = new Dictionary<string, int>(StringComparer.Ordinal); int mostSecrets = 0;
string[] playerCards = Env("PLAYER_CARDS")?.Split(',').Select(id => id.Trim()).Where(id => id.Length > 0).ToArray();
var playerWatched = new Dictionary<string, int>(StringComparer.Ordinal);
long seedBase = Env("SEED_BASE") is string sb ? long.Parse(sb) : 9000;

// The pool of each fight floor of the schedule, in floor order (a finale floor: its fixed boss id).
var floorPools = new List<string>();
foreach (var act in schedule.Acts)
{
    if (act.IsFinale) { floorPools.AddRange(act.FixedBosses.Select(id => "=" + id)); continue; }
    foreach (var slot in act.Combat) floorPools.AddRange(Enumerable.Repeat(slot.Pool, slot.Count));
    floorPools.Add(act.MajorPool);
}
int FloorOf(DungeonRunBoss boss)
{
    // FINALE=1 fights a fixed boss on its finale floor (it may sit in an earlier pool too); otherwise the first floor that draws it.
    int fixedFloor = floorPools.IndexOf("=" + boss.Id);
    if (fixedFloor >= 0 && (finale || !floorPools.Any(p => !p.StartsWith("=") && data.Schedules.Member(boss, p)))) return fixedFloor + 1;
    for (int f = 0; f < floorPools.Count; f++)
        if (!floorPools[f].StartsWith("=") && data.Schedules.Member(boss, floorPools[f])) return f + 1;
    throw new Exception("no floor of " + scheduleId + " draws " + boss.Id + " (tier " + boss.Tier + ")");
}

int sweep = 0;
DungeonRunState ReachFloor(DungeonRunClass cls, int floor, long seed, bool defaultRoll = false, string deck = null, string power = null)
{
    int k = sweep++;
    int n = data.Classes.Count;
    var s = run.NewRun(all, cls.Id, deck ?? cls.StartDecks(run.Data)[(k / n) % cls.StartDecks(run.Data).Count], power ?? cls.Powers[((k / n) + (k / (3 * n))) % cls.Powers.Count], seed,
        schedule: defaultRoll ? null : scheduleId, signatureId: run.SignatureOffer(all, run.Data.FindClass(cls.Id), seed).FirstOrDefault());
    if (defaultRoll && (!s.IsActRun || s.ScheduleVersion != run.ScheduleFor(s).StampVersion || !data.Schedules.Defaults.Contains(s.Schedule)))
        throw new Exception("not a run of a default schedule");
    var scratch = new DungeonRunProfile();
    // The run is of the schedule it walks: the named one, or on the default roll the default it rolled.
    var walked = defaultRoll ? run.ScheduleFor(s) : schedule;
    if (!s.IsActRun || s.ScheduleVersion != walked.StampVersion) throw new Exception("not a run of " + walked.Id);
    while (!(s.Floor == floor && s.Phase == DungeonRunPhase.PreMatch))
    {
        switch (s.Phase)
        {
            case DungeonRunPhase.PreMatch: run.BeginMatch(s); run.RecordOutcome(s, scratch, DungeonRunOutcome.Win); break;
            case DungeonRunPhase.TreasureDraft: run.PickTreasure(s, s.OpenDraft.Offered[0]); break;
            // An anomaly node: no rule on the walk (RULE injects the one under test at the fight); a modifier node: the first.
            case DungeonRunPhase.Modifier:
                run.PickModifier(s, s.ModifierOffer.Kind == DungeonRunGlobalRule.KindAnomaly ? DungeonRunGlobalRule.None : s.ModifierOffer.Offered[0]);
                break;
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

string Identity(DungeonRunState s) => string.Join("|", s.Class, s.StarterDeck, s.Power, s.PowerOverride ?? "-", string.Join(",", s.Deck),
    string.Join(",", s.Cards.Select(c => c.Uid + ":" + c.Id)), string.Join(",", s.Treasures), string.Join(",", s.ActiveTreasures), s.BoardShrine ?? "-");

var bosses = finale ? schedule.Acts.Where(a => a.IsFinale).SelectMany(a => a.FixedBosses).Select(data.Boss).ToList() : data.Encounters.ToList();
// DEFAULT_SWEEP: each run is rolled first (its hero, seed, schedule and floor), so the boss list is what the sweep meets.
var sweepRuns = new List<(DungeonRunClass Hero, string Deck, string Power, long Seed, int Floor, string Schedule, string Boss)>();
for (int i = sweepFrom; i < sweepFrom + defaultSweep; i++)
{
    int n = data.Classes.Count;
    var hero = data.Classes[i % n];
    string deck = hero.StartDecks(run.Data)[(i / n) % hero.StartDecks(run.Data).Count], power = hero.Powers[(i / n) % hero.Powers.Count];
    long seed = seedBase + i;
    var probe = run.NewRun(all, hero.Id, deck, power, seed, signatureId: run.SignatureOffer(all, run.Data.FindClass(hero.Id), seed).FirstOrDefault());
    int floor = 1 + 7 * i % probe.BossSequence.Count;
    sweepRuns.Add((hero, deck, power, seed, floor, probe.Schedule, probe.BossSequence[floor - 1]));
}
if (defaultSweep > 0) { bosses = sweepRuns.Select(r => data.Boss(r.Boss)).ToList(); gamesPerBoss = 1; }
else if (!finale)
{
    if (Env("BOSS") is string only) bosses = only.Split(',').Select(id => data.Boss(id.Trim())).ToList();
    if (Env("FAMILY") is string fam) bosses = bosses.Where(b => b.Family == fam).ToList();
    if (Env("BOSS_FROM") is string from) bosses = bosses.SkipWhile(b => b.Id != from).ToList();
    if (Env("BOSS_COUNT") is string count) bosses = bosses.Take(int.Parse(count)).ToList();
}

int faults = 0, games = 0, bossWins = 0, totalFallbacks = 0, failed = 0;
var powerUnused = new List<string>(); var formUnreached = new List<string>();
void Check(bool ok, string what, List<string> problems) { if (!ok) problems.Add(what); }
for (int bi = 0; bi < bosses.Count; bi++)
{
    var boss = bosses[bi];
    var swept = defaultSweep > 0 ? sweepRuns[bi] : default;
    int floor = defaultSweep > 0 ? swept.Floor : FloorOf(boss);
    // A sweep fight plays the variant its run rolled (one game per run).
    int variants = defaultSweep > 0 ? 1 : Math.Max(1, boss.Variants.Count);
    int powerUses = 0, formChangesSeen = 0; bool clickable = false;
    for (int v = 0; v < variants; v++)
    {
        int played = 0;
        for (int g = 0; played < gamesPerBoss && g < 400; g++)
        {
            var hero = defaultSweep > 0 ? swept.Hero : data.Classes[sweep % data.Classes.Count];
            long seed = defaultSweep > 0 ? swept.Seed : seedBase + data.Bosses.IndexOf(boss) * 1000 + v * 100 + g;
            var s = defaultSweep > 0 ? ReachFloor(hero, floor, seed, true, swept.Deck, swept.Power) : ReachFloor(hero, floor, seed);
            if (defaultSweep > 0 && (s.Schedule != swept.Schedule || s.BossSequence[floor - 1] != boss.Id)) throw new Exception("the sweep run " + bi + " rolled differently");
            s.BossSequence[floor - 1] = boss.Id;
            // The seeds that roll this variant (a boss without variants plays every seed).
            if (defaultSweep == 0 && boss.Variants.Count > 0 && run.CurrentVariant(s).Id != boss.Variants[v].Id) continue;
            played++;
            if (forcedRule != null)
            {
                s.ActiveRules.RemoveAll(r => r.Kind != null);
                s.ActiveRules.Add(new DungeonRunRule { Id = forcedRule, Act = run.CurrentNode(s).Act, FromNode = -1, Kind = DungeonRunGlobalRule.KindAnomaly });
            }
            var profile = new DungeonRunProfile(); profile.Unlocked.AddRange(data.Unlocks.Keys);
            if (carried > 0 && boss.HasFlag(DungeonRunEncounterData.PersistentDamage))
                profile.BossDamage.Add(new DungeonRunBossDamage { Boss = boss.Id, Remaining = (boss.Health ?? 0) - carried, Attempts = 1 });
            string result; int turns = 0, decisions = 0, fallbacks = 0, health = 0, uses = 0, mana = -1; long ms;
            var problems = new List<string>();
            string extra = "";
            var sw = Stopwatch.StartNew();
            try
            {
                var plan = run.BuildPlan(s, profile);
                if (playerCards != null)
                {
                    // The injected cards first, then the run deck's own cards, to the deck's size.
                    var injected = playerCards.Select(id => Cards.FromId(id) ?? throw new Exception("unknown card " + id)).ToList();
                    int size = Math.Max(plan.PlayerDeck.Count, injected.Count);
                    plan.PlayerDeck = injected.Concat(plan.PlayerDeck.Skip(injected.Count)).Take(size).ToList();
                }
                health = plan.BossHealth;
                mana = plan.BossStartManaBonus;
                var variant = run.CurrentVariant(s);
                // A sweep also meets the custom rows the provenance mix draws: their plan is no encounter's.
                if (defaultSweep > 0 && !boss.IsEncounter) Check(plan.EncounterId == null && !plan.EncounterDeck, "a custom row's plan names an encounter", problems); else
                Check(plan.EncounterId == boss.Id && plan.EncounterDeck, "plan is not the encounter's", problems);
                Check(plan.BossGeneratedSecrets == boss.BossGeneratedSecrets, "plan boss generated Secrets", problems);
                // A sweep run (no RULE) keeps the twists its new run rolled itself (stamp 4: a twist per act, from that act's twist
                // pool, FromNode -1; the walk takes no anomaly): the plan holds those of this act that act in the match, in the order
                // taken, and no other rule (an anomaly or a twist from outside its pool fails it). Elsewhere no rule but RULE's is active.
                var sweptActs = defaultSweep > 0 ? run.ScheduleFor(s).Acts : null;
                var rolledRules = defaultSweep > 0 && forcedRule == null ? s.ActiveRules.Where(r => r.Kind == DungeonRunGlobalRule.KindTwist && r.FromNode == -1
                        && (r.Act == 0 ? sweptActs.Any(a => a.TwistPool.Contains(r.Id)) : r.Act == run.CurrentNode(s).Act && sweptActs[r.Act - 1].TwistPool.Contains(r.Id)))
                    .Select(r => data.Schedules.Rule(r.Id)).Where(r => r.Effect == DungeonRunGlobalRule.EffectMatch).Select(r => r.Id).ToList() : null;
                if (rolledRules != null) Check(plan.GlobalRules.SequenceEqual(rolledRules), "plan global rules", problems); else
                Check(plan.GlobalRules.SequenceEqual(forcedRule == null ? new string[0] : new[] { forcedRule }), "plan global rules", problems);
                var pilot = DungeonRunBossPilot.Build(boss, plan, data, runClasses: run.MatchClasses(s));
                var npcProfile = pilot.Profile.WithBudget(testBudget);
                var golden = new GoldenPilot(npcProfile, pilot.Plan, null, pilot.SecretPrior);
                var cfg = new GameConfig { RandomSeed = run.MatchSeed(s), Shuffle = true, SkipMulligan = false, History = false, Logging = false, FormatType = FormatType.FT_WILD };
                DungeonRunSetup.Configure(cfg, plan);
                var game = new Game(cfg);
                game.StartGame();
                if (game.FirstPlayer != game.Player1) throw new Exception("player not first");
                DungeonRunSetup.ApplyAfterStart(game, plan);
                if (playerHealth > 0) game.Player1.Hero.Damage = Math.Max(0, game.Player1.Hero.BaseHealth - playerHealth);
                // Start assertions: the boss is the row (or the form its carried damage puts it in), with its power.
                var startForm = plan.BossFormStart > 0 ? boss.Forms[plan.BossFormStart - 1] : null;
                Check(game.Player2.Hero.Card.Id == (startForm?.Hero ?? boss.HeroCard), "boss hero at start is " + game.Player2.Hero.Card.Id, problems);
                Check(game.Player2.Hero.HeroPower.Card.Id == DungeonRunSetup.PowerCardId(startForm?.Power ?? boss.Power), "boss power at start is " + game.Player2.Hero.HeroPower.Card.Id, problems);
                Check(game.Player2.Hero.Health == health - plan.BossCarriedDamage, "boss Health at start is " + game.Player2.Hero.Health, problems);
                Check(game.Player2[EncounterRules.FormIndex] == plan.BossFormStart, "boss form at start", problems);
                if (DungeonRunEncounters.NeedsActivation(game.Player2.Hero.HeroPower.Card.Id))
                    Check(game.Player2.Hero.HeroPower[EncounterRules.PowerActive] == 1, "the boss power's trigger or aura is not started", problems);
                clickable |= !game.Player2.Hero.HeroPower.IsPassiveHeroPower;
                foreach (string rule in plan.GlobalRules)
                    Check(DungeonRunGlobalRules.IsActive(game.Player1, rule) && DungeonRunGlobalRules.IsActive(game.Player2, rule), "rule " + rule + " is not on both players", problems);
                string before = Identity(s);
                run.BeginMatch(s);
                Check((s.HeroSwitch != null) == (plan.PlayerHeroCards.Count + plan.PlayerRelay.Count > 0), "hero switch state", problems);
                game.Process(ChooseTask.Mulligan(game.Player1, new List<int>()));
                var choice = game.Player2.Choice;
                var view = new MulliganView(choice.Choices.Select(id => new MulliganCard(id, game.IdEntityDic[id].Card.Id, game.IdEntityDic[id].Card.Cost)),
                    false, pilot.Deck.CardIds, game.Player1.Hero.Card.Class);
                game.Process(ChooseTask.Mulligan(game.Player2, BotDriver.Mulligan(view, new DeckPlanMulligan(pilot.Plan)).ToList()));
                // MainBegin places the start block and then runs on through the first turn's start (its turn-start triggers and
                // draw: a drawn AT_130 Sea Reaver kills a 1-Health start minion). The boards the start block left are read when
                // that first turn starts, before its triggers and draw.
                List<string> startBossBoard = null, startPlayerBoard = null;
                TriggerManager.TriggerHandler firstTurnStart = sender =>
                {
                    if (startBossBoard != null) return;
                    startBossBoard = game.Player2.BoardZone.Select(m => m.Card.Id).ToList();
                    startPlayerBoard = game.Player1.BoardZone.Select(m => m.Card.Id).ToList();
                };
                game.TriggerManager.TurnStartTrigger += firstTurnStart;
                game.MainBegin();
                game.TriggerManager.TurnStartTrigger -= firstTurnStart;
                // After MainBegin: the boss's shrine is on its board, the start block landed, the hero cards are in hand.
                if (variant?.Shrine != null)
                    Check(ShrineRules.Of(game.Player2).Any(m => m.Card.Id == variant.Shrine), "the boss shrine " + variant.Shrine + " is not on its board", problems);
                if (boss.Start != null)
                {
                    Check(startBossBoard != null, "start: the first turn did not start in MainBegin", problems);
                    foreach (string id in boss.Start.BossBoard.Distinct())
                        Check(startBossBoard?.Contains(id) == true, "start: " + id + " is not on the boss board", problems);
                    foreach (string id in boss.Start.PlayerBoard.Distinct())
                        Check(startPlayerBoard?.Contains(id) == true, "start: " + id + " is not on the player board", problems);
                    if (boss.Start.BossWeapon != null) Check(game.Player2.Hero.Weapon?.Card.Id == boss.Start.BossWeapon, "start: the boss weapon", problems);
                }
                foreach (var option in plan.PlayerHeroCards)
                    Check(game.Player1.HandZone.Concat(game.Player1.DeckZone).Any(c => c.Card.Id == option.Hero.Id), "hero card " + option.Hero.Id + " was not given", problems);
                int decisionIndex = 0, actions = 0;
                var recent = new Queue<string>();
                void Act(PlayerTask task, string who)
                {
                    recent.Enqueue($"t{game.Turn} {who}: {task}");
                    if (recent.Count > 10) recent.Dequeue();
                    if (++actions > 1500) throw new Exception("stuck after 1500 actions | " + string.Join(" | ", recent));
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
                        if (trace?.Kind == "fallback") { fallbacks++; Console.Error.WriteLine($"  FALLBACK {boss.Id} t{game.Turn}: {trace.FallbackReason}"); }
                        if (task is HeroPowerTask) uses++;
                        if (watch != null && task is PlayCardTask played0 && played0.Source != null && watch.Contains(played0.Source.Card.Id))
                            watched[played0.Source.Card.Id] = watched.TryGetValue(played0.Source.Card.Id, out int seen0) ? seen0 + 1 : 1;
                        Act(task, "boss");
                        if (watch != null) mostSecrets = Math.Max(mostSecrets, game.Player2.SecretZone.Count);
                    }
                    else
                    {
                        // The run player plays a fight-only hero card as soon as it can (the hero-switch path), else the local bot's pick.
                        var options = game.Player1.Options();
                        var heroCard = options.FirstOrDefault(t => t is PlayCardTask play && play.Source is Hero);
                        // With injected cards the run player plays one of them whenever it can (the cards under test), else the local bot's pick.
                        var injectedPlay = playerCards == null ? null : options.FirstOrDefault(t => t is PlayCardTask play && play.Source != null && playerCards.Contains(play.Source.Card.Id));
                        var playerTask = heroCard ?? injectedPlay ?? LocalBot.Choose(options);
                        if (watch != null && playerTask is PlayCardTask mine && mine.Source != null && watch.Contains(mine.Source.Card.Id))
                            playerWatched[mine.Source.Card.Id] = playerWatched.TryGetValue(mine.Source.Card.Id, out int seenMine) ? seenMine + 1 : 1;
                        Act(playerTask, "player");
                    }
                }
                turns = game.Turn;
                result = game.State != State.COMPLETE ? "turn-cap" : game.Player2.PlayState == PlayState.WON ? "boss" : game.Player1.PlayState == PlayState.WON ? "player" : "tie";
                if (result == "boss") bossWins++;
                // The harvest and what the run keeps of it.
                var end = DungeonRunSetup.Harvest(game);
                bool bossDead = game.Player2.Hero.ToBeDestroyed || game.Player2.Hero.Health <= 0;
                Check(end.BossRemainingHealth == (bossDead ? 0 : game.Player2.Hero.Health), "harvest: boss remaining Health", problems);
                Check(end.BossForm == game.Player2[EncounterRules.FormIndex], "harvest: boss form", problems);
                if (boss.Forms.Count > 0)
                {
                    var form = end.BossForm > 0 ? boss.Forms[end.BossForm - 1] : null;
                    Check(game.Player2.Hero.Card.Id == (boss.Forms.Take(end.BossForm).Select(f => f.Hero).LastOrDefault(h => h != null) ?? boss.HeroCard), "the boss hero is not its form's", problems);
                    // Only a form with its own Health caps the hero at the threshold (the Plague Lords' 200 / 100 heroes); a form without
                    // one keeps the hero's maximum (Tekahn's power-only forms: the client's one 300-Health hero), so the boss may heal above
                    // the threshold and stays in the form (wave 6: tekahn24, healed 195 -> 219 in Curse of Flame). With the plan's form
                    // ratchet (step 9 D11: Tekahn on acts-v4t) a form without its own Health caps the hero at the threshold too.
                    if (form != null && form.HealthAtMost > 0 && (form.Health > 0 || plan.FormRatchet) && !bossDead) Check(game.Player2.Hero.Health <= form.HealthAtMost, "the boss's Health is above its form's threshold", problems);
                    if (!bossDead && end.BossForm + 1 <= boss.Forms.Count && boss.Forms[end.BossForm].HealthAtMost > 0)
                        Check(game.Player2.Hero.Health > boss.Forms[end.BossForm].HealthAtMost, "the boss should be in its next form", problems);
                    formChangesSeen += end.BossFormChanges;
                }
                powerUses += uses;
                var outcome = result == "player" ? DungeonRunOutcome.Win : result == "turn-cap" ? DungeonRunOutcome.TurnLimit : result == "tie" ? DungeonRunOutcome.Tie : DungeonRunOutcome.Loss;
                run.RecordOutcome(s, profile, outcome, game.RunResult, end);
                Check(s.HeroSwitch == null, "the hero switch outlived the fight", problems);
                if (outcome != DungeonRunOutcome.Win) Check(Identity(s) == before, "a lost fight changed the run's identity", problems);
                Check(s.Class == hero.Id && run.RunClass(s).HeroCard == hero.HeroCard, "the run's class changed", problems);
                if (boss.HasFlag(DungeonRunEncounterData.PersistentDamage))
                {
                    var record = profile.DamageOf(boss.Id);
                    if (outcome == DungeonRunOutcome.Win) Check(record == null, "a won fight left boss damage", problems);
                    else Check(end.BossRemainingHealth >= (boss.Health ?? 0) ? record == null || carried > 0 : record != null && record.Remaining <= Math.Max(1, end.BossRemainingHealth) && record.Remaining >= 1,
                        "persistent boss damage record", problems);
                    extra += $" form {plan.BossFormStart}>{end.BossForm} left {end.BossRemainingHealth} banked {profile.DamageOf(boss.Id)?.Remaining.ToString() ?? "-"}";
                }
                if (plan.PlayerHeroCards.Count + plan.PlayerRelay.Count > 0) extra += $" heroes {string.Join(">", end.HeroesUsed)}";
                if (variant != null) extra += $" variant {variant.Id} shrine {variant.Shrine ?? "-"}";
                if (forcedRule != null) extra += " rule " + forcedRule;
                if (defaultSweep > 0) extra += " schedule " + s.Schedule + (plan.GlobalRules.Count > 0 ? " rules " + string.Join(",", plan.GlobalRules) : "");
            }
            catch (Exception e) { result = "FAULT " + e.GetType().Name + ": " + e.Message.Split('\n')[0]; faults++; }
            ms = sw.ElapsedMilliseconds; games++; totalFallbacks += fallbacks;
            if (problems.Count > 0) { failed += problems.Count; foreach (string p in problems) Console.Error.WriteLine($"  CHECK {boss.Id}: {p}"); }
            Console.WriteLine($"{boss.Id,-18} {boss.Provenance,-4} t{boss.Tier,-6} floor {floor,2} {hero.Id + "/" + s.StarterDeck + "/" + s.Power,-46} deck {s.Deck.Count,2} -> {result,-8} turns {turns,3} decisions {decisions,3} fallbacks {fallbacks,2} health {health,3} mana+{mana} power {uses,2} checks {(problems.Count == 0 ? "ok" : "FAIL " + problems.Count)}{extra} {ms / 1000.0,6:F1}s");
        }
        if (played < gamesPerBoss) { Console.Error.WriteLine($"  CHECK {boss.Id}: variant {v} rolled only {played} times in 400 seeds"); failed++; }
        if (defaultSweep > 0) break;
    }
    if (clickable && powerUses == 0) powerUnused.Add(boss.Id);
    if (boss.Forms.Count > 0 && formChangesSeen == 0 && carried == 0) formUnreached.Add(boss.Id);
}
foreach (var kv in SearchTelemetry.Details()) Console.Error.WriteLine($"SWALLOWED {kv.Value} x {kv.Key}");
if (defaultSweep > 0)
    Console.WriteLine($"default sweep: {defaultSweep} runs from {sweepFrom} with no named schedule, " + string.Join(", ", sweepRuns.GroupBy(r => r.Schedule).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => g.Key + " " + g.Count()))
        + "; adventures met " + string.Join(" ", bosses.GroupBy(b => b.Provenance).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => g.Key + "=" + g.Count())));
Console.WriteLine($"encounter run: schedule {(defaultSweep > 0 ? "default" : scheduleId)}{(forcedRule != null ? ", rule " + forcedRule : "")}{(carried > 0 ? ", carried " + carried : "")}{(finale ? ", finale" : "")}; power unused: {(powerUnused.Count == 0 ? "-" : string.Join(",", powerUnused))}; form unreached: {(formUnreached.Count == 0 ? "-" : string.Join(",", formUnreached))}");
Console.WriteLine($"games {games}, boss wins {bossWins}, faults {faults}, fallbacks {totalFallbacks}, failed checks {failed}, swallowed leaf exceptions {SearchTelemetry.SwallowedLeafErrors}, power-unused {powerUnused.Count}, form-unreached {formUnreached.Count}"
    + (watch == null ? "" : ", watched " + string.Join(" ", watch.Select(id => id + " x" + (watched.TryGetValue(id, out int n) ? n : 0))) + ", most boss Secrets " + mostSecrets)
    + (watch == null || playerCards == null ? "" : ", player played " + string.Join(" ", watch.Select(id => id + " x" + (playerWatched.TryGetValue(id, out int n) ? n : 0)))));
return faults + failed + SearchTelemetry.SwallowedLeafErrors;
