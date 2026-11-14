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
// A boss with shrine variants plays its games once per variant (the seeds that roll that variant).
// Family 06 variant (p8f06/make_bp_rules06.py): BOSS may name a custom boss; RULE is injected with its own kind, a run-scope
// rule from the start of the run; a run-layer rule (ULDA_720, ULDA_729) is expected out of the plan's rule list and checked
// on the run layer; a probe watches each game for the rule's own effect (RuleProbe below): a missing state is a failed
// check; each line ends with "seen N", the last line with "rule-seen N" and the bosses whose games never showed the rule.
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
var forced = forcedRule == null ? null : data.Schedules.Rule(forcedRule) ?? throw new Exception("unknown rule " + forcedRule);
bool runLayerRule = forced != null && forced.Effect == DungeonRunGlobalRule.EffectRun;
const int ScarsCarried = 25;
int carried = Env("CARRIED") is string cd ? int.Parse(cd) : 0;
int playerHealth = Env("PLAYER_HEALTH") is string ph ? int.Parse(ph) : 0;
bool finale = Env("FINALE") == "1";
string[] watch = Env("WATCH")?.Split(',').Select(id => id.Trim()).ToArray();
var watched = new Dictionary<string, int>(StringComparer.Ordinal); int mostSecrets = 0;
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
DungeonRunState ReachFloor(DungeonRunClass cls, int floor, long seed)
{
    int k = sweep++;
    int n = data.Classes.Count;
    var s = run.NewRun(all, cls.Id, cls.Decks[(k / n) % cls.Decks.Count], cls.Powers[((k / n) + (k / (3 * n))) % cls.Powers.Count], seed,
        schedule: scheduleId, signatureId: run.SignatureOffer(all, run.Data.FindClass(cls.Id), seed).FirstOrDefault());
    var scratch = new DungeonRunProfile();
    if (!s.IsActRun || s.ScheduleVersion != schedule.StampVersion) throw new Exception("not a run of " + scheduleId);
    if (forced != null && forced.RunScope) s.ActiveRules.Add(new DungeonRunRule { Id = forced.Id, Act = 0, FromNode = -1, Kind = forced.Kind });
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
if (!finale)
{
    if (Env("BOSS") is string only) bosses = only.Split(',').Select(id => data.Boss(id.Trim())).ToList();
    if (Env("FAMILY") is string fam) bosses = bosses.Where(b => b.Family == fam).ToList();
    if (Env("BOSS_FROM") is string from) bosses = bosses.SkipWhile(b => b.Id != from).ToList();
    if (Env("BOSS_COUNT") is string count) bosses = bosses.Take(int.Parse(count)).ToList();
}

int faults = 0, games = 0, bossWins = 0, totalFallbacks = 0, failed = 0;
var powerUnused = new List<string>(); var formUnreached = new List<string>();
int ruleSeen = 0; var ruleUnseen = new List<string>();
void Check(bool ok, string what, List<string> problems) { if (!ok) problems.Add(what); }
foreach (var boss in bosses)
{
    int floor = FloorOf(boss);
    int variants = Math.Max(1, boss.Variants.Count);
    int powerUses = 0, formChangesSeen = 0, bossSeen = 0; bool clickable = false;
    for (int v = 0; v < variants; v++)
    {
        int played = 0;
        for (int g = 0; played < gamesPerBoss && g < 400; g++)
        {
            var hero = data.Classes[sweep % data.Classes.Count];
            long seed = seedBase + data.Bosses.IndexOf(boss) * 1000 + v * 100 + g;
            var s = ReachFloor(hero, floor, seed);
            s.BossSequence[floor - 1] = boss.Id;
            // The seeds that roll this variant (a boss without variants plays every seed).
            if (boss.Variants.Count > 0 && run.CurrentVariant(s).Id != boss.Variants[v].Id) continue;
            played++;
            if (forced != null)
            {
                s.ActiveRules.RemoveAll(r => r.Kind != null);
                s.ActiveRules.Add(new DungeonRunRule { Id = forced.Id, Act = forced.RunScope ? 0 : run.CurrentNode(s).Act, FromNode = -1, Kind = forced.Kind });
                if (forced.Id == DungeonRun.BattleScars) s.HeroDamage = ScarsCarried;
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
                health = plan.BossHealth;
                mana = plan.BossStartManaBonus;
                var variant = run.CurrentVariant(s);
                Check(boss.IsEncounter ? plan.EncounterId == boss.Id && plan.EncounterDeck : plan.EncounterId == null && !plan.EncounterDeck, "plan is not the boss's", problems);
                Check(plan.BossGeneratedSecrets == boss.BossGeneratedSecrets, "plan boss generated Secrets", problems);
                Check(plan.GlobalRules.SequenceEqual(forcedRule == null || runLayerRule ? new string[0] : new[] { forcedRule }), "plan global rules", problems);
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
                if (forcedRule == DungeonRun.BattleScars)
                    Check(game.Player1.Hero.BaseHealth >= DungeonRun.BattleScarsHealth && game.Player1.Hero.Damage == ScarsCarried, "Battle Scars: the hero starts at " + game.Player1.Hero.Health + " of " + game.Player1.Hero.BaseHealth, problems);
                // Start assertions: the boss is the row (or the form its carried damage puts it in), with its power.
                var startForm = plan.BossFormStart > 0 ? boss.Forms[plan.BossFormStart - 1] : null;
                Check(game.Player2.Hero.Card.Id == (startForm?.Hero ?? boss.HeroCard), "boss hero at start is " + game.Player2.Hero.Card.Id, problems);
                Check(game.Player2.Hero.HeroPower.Card.Id == DungeonRunSetup.PowerCardId(startForm?.Power ?? boss.Power), "boss power at start is " + game.Player2.Hero.HeroPower.Card.Id, problems);
                Check(game.Player2.Hero.Health == health - plan.BossCarriedDamage, "boss Health at start is " + game.Player2.Hero.Health, problems);
                Check(game.Player2[EncounterRules.FormIndex] == plan.BossFormStart, "boss form at start", problems);
                if (DungeonRunEncounters.NeedsActivation(game.Player2.Hero.HeroPower.Card.Id))
                    Check(game.Player2.Hero.HeroPower[EncounterRules.PowerActive] == 1, "the boss power's trigger or aura is not started", problems);
                clickable |= !game.Player2.Hero.HeroPower.IsPassiveHeroPower;
                if (runLayerRule) Check(!DungeonRunGlobalRules.IsScripted(forcedRule) && game.EncounterOrNull?.GlobalRules.Contains(forcedRule) != true, "a run-layer rule reached the match", problems);
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
                game.MainBegin();
                // After MainBegin: the boss's shrine is on its board, the start block landed, the hero cards are in hand.
                if (variant?.Shrine != null)
                    Check(ShrineRules.Of(game.Player2).Any(m => m.Card.Id == variant.Shrine), "the boss shrine " + variant.Shrine + " is not on its board", problems);
                if (boss.Start != null)
                {
                    foreach (string id in boss.Start.BossBoard.Distinct())
                        Check(game.Player2.BoardZone.Count(m => m.Card.Id == id) >= 1, "start: " + id + " is not on the boss board", problems);
                    foreach (string id in boss.Start.PlayerBoard.Distinct())
                        Check(game.Player1.BoardZone.Count(m => m.Card.Id == id) >= 1, "start: " + id + " is not on the player board", problems);
                    if (boss.Start.BossWeapon != null) Check(game.Player2.Hero.Weapon?.Card.Id == boss.Start.BossWeapon, "start: the boss weapon", problems);
                }
                foreach (var option in plan.PlayerHeroCards)
                    Check(game.Player1.HandZone.Concat(game.Player1.DeckZone).Any(c => c.Card.Id == option.Hero.Id), "hero card " + option.Hero.Id + " was not given", problems);
                var probe = forcedRule == null ? null : new RuleProbe(forcedRule, game, RunHelpers.Bosses(game));
                probe?.Start(problems);
                int decisionIndex = 0, actions = 0;
                var recent = new Queue<string>();
                void Act(PlayerTask task, string who)
                {
                    recent.Enqueue($"t{game.Turn} {who}: {task}");
                    probe?.Before(task);
                    if (recent.Count > 10) recent.Dequeue();
                    if (++actions > 1500) throw new Exception("stuck after 1500 actions | " + string.Join(" | ", recent));
                    if (task is ChooseTask pick && game.Step != Step.BEGIN_MULLIGAN)
                    {
                        var decision = game.GetPendingDecisions().Single(d => d.ControllerId == task.Controller.Id);
                        if (!game.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(decision.Id, decision.ControllerId, pick.Choices.ToArray()), out var why))
                            throw new Exception($"engine rejected {who} choice ({why}) | " + string.Join(" | ", recent));
                    }
                    else if (!game.Process(task)) throw new Exception($"engine rejected {who} task | " + string.Join(" | ", recent));
                    probe?.After(task, problems);
                }
                while (game.State != State.COMPLETE && game.Turn <= 100)
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
                        Act(heroCard ?? LocalBot.Choose(options), "player");
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
                    if (form != null && form.HealthAtMost > 0 && !bossDead) Check(game.Player2.Hero.Health <= form.HealthAtMost, "the boss's Health is above its form's threshold", problems);
                    if (!bossDead && end.BossForm + 1 <= boss.Forms.Count && boss.Forms[end.BossForm].HealthAtMost > 0)
                        Check(game.Player2.Hero.Health > boss.Forms[end.BossForm].HealthAtMost, "the boss should be in its next form", problems);
                    formChangesSeen += end.BossFormChanges;
                }
                powerUses += uses;
                var outcome = result == "player" ? DungeonRunOutcome.Win : result == "turn-cap" ? DungeonRunOutcome.TurnLimit : result == "tie" ? DungeonRunOutcome.Tie : DungeonRunOutcome.Loss;
                run.RecordOutcome(s, profile, outcome, game.RunResult, end);
                if (forcedRule == DungeonRun.BattleScars)
                {
                    bool ownHero = end.HeroesUsed.Count <= 1;
                    Check(s.HeroDamage == (outcome == DungeonRunOutcome.Win && ownHero ? Math.Min(end.PlayerHeroDamage, DungeonRun.BattleScarsHealth - 1) : ScarsCarried), "Battle Scars: the run keeps " + s.HeroDamage + " damage", problems);
                    if (probe != null) probe.Seen++;
                }
                if (forcedRule == DungeonRun.Multiclassing && probe != null) probe.Seen += foreignCards;
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
                if (probe != null) { probe.End(problems); bossSeen += probe.Seen; ruleSeen += probe.Seen; extra += " rule " + forcedRule + " seen " + probe.Seen; }
            }
            catch (Exception e) { result = "FAULT " + e.GetType().Name + ": " + e.Message.Split('\n')[0]; faults++; }
            ms = sw.ElapsedMilliseconds; games++; totalFallbacks += fallbacks;
            if (problems.Count > 0) { failed += problems.Count; foreach (string p in problems) Console.Error.WriteLine($"  CHECK {boss.Id}: {p}"); }
            Console.WriteLine($"{boss.Id,-18} {boss.Provenance,-4} t{boss.Tier,-6} floor {floor,2} {hero.Id + "/" + s.StarterDeck + "/" + s.Power,-46} deck {s.Deck.Count,2} -> {result,-8} turns {turns,3} decisions {decisions,3} fallbacks {fallbacks,2} health {health,3} mana+{mana} power {uses,2} checks {(problems.Count == 0 ? "ok" : "FAIL " + problems.Count)}{extra} {ms / 1000.0,6:F1}s");
        }
        if (played < gamesPerBoss) { Console.Error.WriteLine($"  CHECK {boss.Id}: variant {v} rolled only {played} times in 400 seeds"); failed++; }
    }
    if (clickable && powerUses == 0) powerUnused.Add(boss.Id);
    if (forcedRule != null && bossSeen == 0) ruleUnseen.Add(boss.Id);
    if (boss.Forms.Count > 0 && formChangesSeen == 0 && carried == 0) formUnreached.Add(boss.Id);
}
foreach (var kv in SearchTelemetry.Details()) Console.Error.WriteLine($"SWALLOWED {kv.Value} x {kv.Key}");
Console.WriteLine($"encounter run: schedule {scheduleId}{(forcedRule != null ? ", rule " + forcedRule : "")}{(carried > 0 ? ", carried " + carried : "")}{(finale ? ", finale" : "")}; power unused: {(powerUnused.Count == 0 ? "-" : string.Join(",", powerUnused))}; form unreached: {(formUnreached.Count == 0 ? "-" : string.Join(",", formUnreached))}");
Console.WriteLine($"games {games}, boss wins {bossWins}, faults {faults}, fallbacks {totalFallbacks}, failed checks {failed}, swallowed leaf exceptions {SearchTelemetry.SwallowedLeafErrors}, power-unused {powerUnused.Count}, form-unreached {formUnreached.Count}"
    + (watch == null ? "" : ", watched " + string.Join(" ", watch.Select(id => id + " x" + (watched.TryGetValue(id, out int n) ? n : 0))) + ", most boss Secrets " + mostSecrets)
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
}
