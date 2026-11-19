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

// Step 9 lane M (handoff/phase9/plan.md D3, stage 1a): the fair-player run harness. Real engine games through the run setup path
// (DungeonRun.BuildPlan -> DungeonRunBossPilot.Build -> DungeonRunSetup.Configure / ApplyAfterStart), the fair AI on BOTH sides
// (the boss as in the encounter harness; the run player through RunPilot: a deck plan derived from its run deck, the boss's
// Secrets as its prior, fight-only hero cards played at once, the pilot rebuilt when the relay or a hero switch changes its hero),
// the plan's own turn cap (Step9.TurnCap: 100 before step 9), the harvest reported as LocalLadder reports it (a restart as a
// Fault) and DungeonRun.RecordOutcome; a restarted floor is played again until it is decided. The game loop is the encounter
// harness's (PlayGame.cs, a copy). ULDA_111 Hearthstone is left out of the player's options (lane-x.md section 6: the AI would
// score the escape as a win; the cast is covered by lane ends: c-ends-9 section E and bp.sh ends9 escape / refuse): every fight's
// plan has its escape closed (Step9.CloseEscape), so the card is unplayable on the game and on the fair search's own copies of it.
//   MODE=slots     plan B1-B4: new runs (the default roll unless SCHEDULE) of rotating loadouts, each walked to a floor of the
//                  band and fought against the boss its run drew there. BANDS=I-combat,I-major,II-combat,II-major,III-combat,III-major
//                  (default all); game g of band b: loadout (37g + 11b) mod L, seed SEED_BASE + 1000000 b + g, a combat band's
//                  floor the act's combat floors in turn
//   MODE=outliers  plan B9: every t3/t4 row and targets.json "B9.named" (or BOSS=...), put on the first combat floor of acts-v4h
//                  (v4t on odd games) that draws it (else the first floor that does); ROWS_FROM/ROWS chunk the row list
//   MODE=finale    plan B6: the acts-v4h Heist finale, each fight (FIGHTS=0,1,2,3 default all) of run g walked to its floor
//   MODE=tekahn    plan B7: the acts-v4t finale on Tekahn with the hero relay, the run hero at full Health
//   MODE=longrun   plan B10/B11: whole runs (the default roll), every fight played; run i's origin is MonsterHunt, Heist, Rumble,
//                  Tombs, class in turn (stratified), its loadout the origin's (7 (i / 5)) mod count-th
//   MODE=treasure  plan E (D6): every "shared" treasure (TREASURES_FROM / N chunk) held by a loadout of another origin on acts-v4h,
//                  one game on floor 2; its active card's plays are counted
//   MODE=named     BOSS=id,id with GAMES each on SCHEDULE (default acts-v4h) at the first floor that draws the row; PLAYER_CLASS=row
//                  ids and PLAYER_DECK=deck ids pick the loadouts, PLAYER_CARDS=id,id join the run deck, PLAYER_TREASURES=id,id
//                  are held (as a pick gives them: GILA_911 Rewind replays a lost floor); TOKI48=1 puts
//                  GILA_BOSS_48t in Infinite Toki's deck in the loaded data (in memory: the shipped files are untouched) and adds
//                  the gate's line (plan stage 4 item 3)
// Common: N=games per unit (GAMES is the same), FROM=first index, SEED_BASE (default 9100000, targets.json S1), SCHEDULE_JSON /
// ENCOUNTERS_JSON fixture data, WATCH=ids (boss plays from hand, Secrets sprung), PLAYER_WATCH=ids (the run player's), PLAYER_HEALTH.
// Each fight prints one "G" line of key=value fields (verify/tools/balance/agg.py reads them); longrun adds an "R" line per run.
// Expected: 0 faults, 0 fallbacks (either side), 0 failed checks, 0 swallowed leaf exceptions.
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
string[] List(string name) => Env(name)?.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
string root = VerifyPaths.Data;
var data = DungeonRunData.Parse(File.ReadAllText(Path.Combine(root, "dungeon-run", "run-v1.json")),
    File.ReadAllText(Path.Combine(root, "dungeon-run", "pool-v1.json")), File.ReadAllText(Path.Combine(root, "npc", "decks-v1.json")),
    File.ReadAllText(Path.Combine(root, "dungeon-run", "catalog-v1.json")),
    File.ReadAllText(Env("SCHEDULE_JSON") ?? Path.Combine(root, "dungeon-run", "schedule-v1.json")),
    File.ReadAllText(Path.Combine(root, "dungeon-run", "tavern-v1.json")),
    File.ReadAllText(Env("ENCOUNTERS_JSON") ?? Path.Combine(root, "dungeon-run", "encounters-v1.json")));
string mode = Env("MODE") ?? "slots";
const string Timeline = "GILA_BOSS_48t", Toki = "gila_48_infinite_toki";
bool toki48 = Env("TOKI48") == "1";
if (toki48)
{
    // The gate's data copy (plan stage 4 item 3): Alternate Timeline takes back the slot of its fill (the second EX1_287).
    var deck = data.BossDecks[data.Boss(Toki).Deck];
    int at = deck.LastIndexOf("EX1_287");
    if (at < 0 || deck.Contains(Timeline)) throw new Exception("TOKI48: Toki's deck is not the shipped one");
    deck[at] = Timeline;
}
var run = new DungeonRun(data);
var all = new DungeonRunProfile(); all.Unlocked.AddRange(data.Unlocks.Keys);
var testBudget = new Budget(6000, 2000, 300, 6, 3, 100, 8, turnSearchSeconds: 6, turnWallSeconds: 8);
int n = Env("N") is string ns ? int.Parse(ns) : Env("GAMES") is string gs ? int.Parse(gs) : 2;
int from = Env("FROM") is string fs ? int.Parse(fs) : 0;
long seedBase = Env("SEED_BASE") is string sb ? long.Parse(sb) : 9100000;
int playerHealth = Env("PLAYER_HEALTH") is string ph ? int.Parse(ph) : 0;
string[] watch = List("WATCH"), playerWatch = List("PLAYER_WATCH"), playerCards = List("PLAYER_CARDS");
string hearth = Step9.HearthstoneShell();

// The loadouts: every enabled hero row x each of its starter decks (plan section 1: 116), with the row's origin.
var loadouts = new List<(DungeonRunClass Cls, string Deck, string Origin)>();
foreach (var cls in data.Classes.Where(c => c.Enabled))
    foreach (string d in cls.Decks)
        loadouts.Add((cls, d, cls.Kind == DungeonRunClass.KindClass ? "class" : cls.Adventure));
if (List("PLAYER_CLASS") is string[] pc) loadouts = loadouts.Where(l => pc.Contains(l.Cls.Id)).ToList();
if (List("PLAYER_DECK") is string[] pd) loadouts = loadouts.Where(l => pd.Contains(l.Deck)).ToList();
if (loadouts.Count == 0) throw new Exception("no loadout");

DungeonRunState NewRun(int li, long seed, string schedule, int power)
{
    var lo = loadouts[li];
    var s = run.NewRun(all, lo.Cls.Id, lo.Deck, lo.Cls.Powers[power % lo.Cls.Powers.Count], seed, schedule: schedule,
        signatureId: run.SignatureOffer(all, lo.Cls, seed).FirstOrDefault());
    if (!s.IsActRun) throw new Exception("not an act run");
    return s;
}
// A run of loadout li or, when it refuses the schedule (DALA_Tekahn on acts-v4t), of the next loadout that takes it.
(DungeonRunState State, int Loadout) Start(int li, long seed, string schedule, int power)
{
    for (int k = 0; k < loadouts.Count; k++)
    {
        int at = (li + k) % loadouts.Count;
        try { return (NewRun(at, seed, schedule, power), at); }
        catch (InvalidOperationException) when (schedule != null) { }
    }
    throw new Exception("no loadout takes " + schedule);
}

// Fight floors of a schedule: (act number, kind "combat"/"major"/"finale", floor) in floor order.
List<(int Act, string Kind, int Floor, string Pool)> Floors(DungeonRunSchedule sched)
{
    var floors = new List<(int, string, int, string)>(); int f = 0, a = 0;
    foreach (var act in sched.Acts)
    {
        a++;
        if (act.IsFinale) { foreach (string id in act.FixedBosses) floors.Add((a, "finale", ++f, "=" + id)); continue; }
        foreach (var slot in act.Combat) for (int k = 0; k < slot.Count; k++) floors.Add((a, "combat", ++f, slot.Pool));
        floors.Add((a, "major", ++f, act.MajorPool));
    }
    return floors;
}

// The run holds treasure t from now on, as PickTreasure gives it (an active one's card joins the deck); returns that card's id.
string Hold(DungeonRunState s, DungeonRunTreasure t)
{
    string shell = t.Active ? DungeonRunContent.Get(t.Id).ShellId : null;
    if (!s.Treasures.Contains(t.Id) && !s.ActiveTreasures.Contains(t.Id))
    {
        if (t.Active) { s.ActiveTreasures.Add(t.Id); DungeonRunDeck.Add(s, shell, DungeonRunDeck.OriginTreasure); }
        else s.Treasures.Add(t.Id);
    }
    return shell;
}

string Identity(DungeonRunState s) => string.Join("|", s.Class, s.StarterDeck, s.Power, s.PowerOverride ?? "-", string.Join(",", s.Deck),
    string.Join(",", s.Cards.Select(c => c.Uid + ":" + c.Id)), string.Join(",", s.Treasures), string.Join(",", s.ActiveTreasures), s.BoardShrine ?? "-");

int faults = 0, fights = 0, gamesPlayed = 0, playerWins = 0, bossWins = 0, caps = 0, ties = 0, escapes = 0, restartsAll = 0;
int bossFallbacks = 0, playerFallbacks = 0, failed = 0;
var watched = new SortedDictionary<string, int>(StringComparer.Ordinal);
var sprung = new SortedDictionary<string, int>(StringComparer.Ordinal);
var playerPlayed = new SortedDictionary<string, int>(StringComparer.Ordinal);
int timelineCasts = 0, gateMaxRestarts = 0, gateTurnExcess = 0;
void Bump(SortedDictionary<string, int> d, string id) => d[id] = d.TryGetValue(id, out int had) ? had + 1 : 1;
void Check(bool ok, string what, List<string> problems) { if (!ok) problems.Add(what); }

// One fight: the floor's games until it is decided (a restart restages the floor; at most 4 games).
(string Result, int Games, int Restarts, int Turns, int Cap, int Hp, int BossLeft, int BossStart, int Decisions, string Extra, List<string> Problems)
    Fight(DungeonRunState s, DungeonRunProfile profile, DungeonRunBoss boss)
{
    var problems = new List<string>();
    string result = "-", extra = ""; int games = 0, restarts = 0, turns = 0, cap0 = 0, hp = 0, left = 0, start = 0, decisions = 0;
    int floor = s.Floor, node = s.Node;
    bool capWrong = false, escapeClosed = false;
    var sources = new List<string>();
    for (int k = 0; k < 4 && s.Floor == floor && s.Node == node && s.Phase == DungeonRunPhase.PreMatch; k++)
    {
        var plan = run.BuildPlan(s, profile);
        int cap = Step9.TurnCap(plan);
        if (k == 0) { cap0 = cap; start = plan.BossHealth - plan.BossCarriedDamage; }
        Check(plan.EncounterId == (boss.IsEncounter ? boss.Id : null) && plan.EncounterDeck == boss.IsEncounter, "plan is not the boss row's", problems);
        // The floor's first game has the fight's own cap (100, or the act's / the relay's); a restarted game gets what is left of
        // the floor's 2 x cap budget (D13, DungeonRun.PlanMatchEnds: max(1, 2 x cap - the turns of the floor's earlier games)).
        if (k == 0) Check(cap >= 100, "turn cap " + cap, problems);
        else
        {
            int left0 = Math.Max(1, 2 * cap0 - turns);
            Check(cap == left0, "game " + k + "'s turn cap " + cap + ", not the floor budget's " + left0 + " (2 x " + cap0 + " - " + turns + ")", problems);
            if (cap != left0) capWrong = true;
        }
        var pilot = DungeonRunBossPilot.Build(boss, plan, data, runClasses: run.MatchClasses(s));
        var npcProfile = pilot.Profile.WithBudget(testBudget);
        var golden = new GoldenPilot(npcProfile, pilot.Plan, null, pilot.SecretPrior);
        var cfg = new GameConfig { RandomSeed = run.MatchSeed(s), Shuffle = true, SkipMulligan = false, History = false, Logging = false, FormatType = FormatType.FT_WILD };
        // The pilot leaves ULDA_111 Hearthstone out by the plan, not by a filter of its options alone: the fair search plans on its
        // own copy of the game, where an allowed escape stays playable, so it picked the cast and the driver found it nowhere in the
        // filtered options (BotAction.Resolve: "Sequence contains no matching element", a FALLBACK on each such decision).
        // A fight whose escape it closed says so on its line (escape=closed).
        if (Step9.CloseEscape(plan) && !escapeClosed) { escapeClosed = true; extra += " escape=closed"; }
        DungeonRunSetup.Configure(cfg, plan);
        var game = new Game(cfg);
        game.StartGame();
        if (game.FirstPlayer != game.Player1) throw new Exception("player not first");
        DungeonRunSetup.ApplyAfterStart(game, plan);
        if (playerHealth > 0) game.Player1.Hero.Damage = Math.Max(0, game.Player1.Hero.BaseHealth - playerHealth);
        var prior = RunPilot.BossSecretPrior(plan);
        var pp = RunPilot.Build(s.Class + "." + floor, game.Player1.Hero.Card, plan.PlayerDeck.Select(c => c.Id).ToList(), testBudget);
        var playerGolden = new GoldenPilot(pp.Profile, pp.Plan, null, prior);
        string before = Identity(s);
        run.BeginMatch(s);
        games++; gamesPlayed++;
        // Mulligans: the run player by its derived plan, then the boss as in the encounter harness.
        var pchoice = game.Player1.Choice;
        var pview = new MulliganView(pchoice.Choices.Select(id => new MulliganCard(id, game.IdEntityDic[id].Card.Id, game.IdEntityDic[id].Card.Cost)),
            false, pp.Deck.CardIds, game.Player2.Hero.Card.Class);
        game.Process(ChooseTask.Mulligan(game.Player1, BotDriver.Mulligan(pview, new DeckPlanMulligan(pp.Plan)).ToList()));
        var choice = game.Player2.Choice;
        var view = new MulliganView(choice.Choices.Select(id => new MulliganCard(id, game.IdEntityDic[id].Card.Id, game.IdEntityDic[id].Card.Cost)),
            false, pilot.Deck.CardIds, game.Player1.Hero.Card.Class);
        game.Process(ChooseTask.Mulligan(game.Player2, BotDriver.Mulligan(view, new DeckPlanMulligan(pilot.Plan)).ToList()));
        game.MainBegin();
        var bossSide = new FairSide(game, game.Player2.PlayerId, pilot.Deck.Style, golden, npcProfile, boss.Id);
        bossSide.OnTask = task =>
        {
            if (task is PlayCardTask p && p.Source != null)
            {
                if (watch != null && watch.Contains(p.Source.Card.Id)) Bump(watched, p.Source.Card.Id);
                if (p.Source.Card.Id == Timeline) timelineCasts++;
            }
        };
        var playerSide = new FairSide(game, game.Player1.PlayerId, pp.Deck.Style, playerGolden, pp.Profile, "player " + boss.Id);
        // The filter stays as a guard, and counts: with the escape closed the player is never offered the cast.
        int hearthOffered = 0;
        if (hearth != null) playerSide.Enabled = t =>
        {
            bool cast = t is PlayCardTask p && p.Source?.Card.Id == hearth;
            if (cast) hearthOffered++;
            return !cast;
        };
        playerSide.OnTask = task =>
        {
            if (playerWatch != null && task is PlayCardTask p && p.Source != null && playerWatch.Contains(p.Source.Card.Id)) Bump(playerPlayed, p.Source.Card.Id);
        };
        string hero = game.Player1.Hero.Card.Id;
        int heroChanges = 0;
        // Watched Secrets seen in a Secret zone, by entity: "boss:<id>" or "player:<id>" (the side that held it).
        var secrets = new Dictionary<int, string>();
        void Secrets()
        {
            if (watch == null && playerWatch == null) return;
            foreach (var e in game.Player2.SecretZone) if (watch?.Contains(e.Card.Id) ?? false) secrets[e.Id] = "boss:" + e.Card.Id;
            foreach (var e in game.Player1.SecretZone) if (playerWatch?.Contains(e.Card.Id) ?? false) secrets[e.Id] = "player:" + e.Card.Id;
        }
        PlayerTask Player()
        {
            Secrets();
            // The relay or a hero switch changed the run player's hero: the pilot follows the cards it now holds.
            if (game.Player1.Hero.Card.Id != hero)
            {
                hero = game.Player1.Hero.Card.Id; heroChanges++;
                var ids = game.Player1.DeckZone.Concat(game.Player1.HandZone).Select(c => c.Card.Id).ToList();
                var np = RunPilot.Build(s.Class + "." + floor + "." + heroChanges, game.Player1.Hero.Card, ids, testBudget);
                playerSide.Golden = new GoldenPilot(np.Profile, np.Plan, null, prior);
                playerSide.Profile = np.Profile;
            }
            // A fight-only hero card (Hagatha's hero switch) is played as soon as it can be.
            if (game.Player1.HandZone.Any(c => c is Hero))
            {
                var heroCard = game.Player1.Options().FirstOrDefault(t => t is PlayCardTask play && play.Source is Hero);
                if (heroCard != null) return heroCard;
            }
            return playerSide.Decide();
        }
        try { PlayGame.Loop(game, cap, bossSide.Decide, Player, Secrets, actionLimit: 15 * Math.Max(100, cap)); }
        finally { decisions += bossSide.Decisions + playerSide.Decisions; bossFallbacks += bossSide.Fallbacks; playerFallbacks += playerSide.Fallbacks; }
        Check(hearthOffered == 0, "game " + k + ": the player was offered ULDA_111 Hearthstone's cast " + hearthOffered + " times (its escape is closed)", problems);
        Secrets();
        foreach (var e in secrets) if (game.IdEntityDic.TryGetValue(e.Key, out var ent) && ent.Zone?.Type == Zone.GRAVEYARD) Bump(sprung, e.Value);
        turns += game.Turn;
        Check(game.Turn <= cap + 1, "game " + k + " ran past its cap " + cap, problems);
        string r = game.State != State.COMPLETE ? "turn-cap" : game.Player2.PlayState == PlayState.WON ? "boss" : game.Player1.PlayState == PlayState.WON ? "player" : "tie";
        var end = DungeonRunSetup.Harvest(game);
        // A script end marks the boss hero to leave the table without dying: Alternate Timeline's restart marks both heroes
        // (EncounterRules.RestartGame), Hearthstone's escape the boss's. The boss keeps the Health it has, and the harvest reports
        // it (D13: a persistent-damage boss banks it as a loss does; any other boss starts the restaged game at its own start).
        bool bossDead = !Step9.EndedByScript(game) && (game.Player2.Hero.ToBeDestroyed || game.Player2.Hero.Health <= 0);
        Check(end.BossRemainingHealth == (bossDead ? 0 : game.Player2.Hero.Health), "harvest: boss remaining Health", problems);
        Check(end.BossForm == game.Player2[EncounterRules.FormIndex], "harvest: boss form", problems);
        if (boss.Forms.Count > 0 && end.BossForm > 0 && !bossDead)
        {
            // A form with its own Health caps the hero at the threshold; with the plan's form ratchet (Tekahn on acts-v4t) a form
            // without its own Health does too (lane-x.md section 6).
            var form = boss.Forms[end.BossForm - 1];
            if (form.HealthAtMost > 0 && (form.Health > 0 || Step9.FormRatchet(plan)))
                Check(game.Player2.Hero.Health <= form.HealthAtMost, "the boss's Health is above its form's threshold", problems);
        }
        bool restarted = Step9.Flag(end, "Restarted"), escaped = Step9.Flag(end, "Escaped");
        var outcome = restarted ? DungeonRunOutcome.Fault : r == "player" ? DungeonRunOutcome.Win : r == "turn-cap" ? DungeonRunOutcome.TurnLimit
            : r == "tie" ? DungeonRunOutcome.Tie : DungeonRunOutcome.Loss;
        run.RecordOutcome(s, profile, outcome, game.RunResult, end);
        hp = Math.Max(0, game.Player1.Hero.Health); left = end.BossRemainingHealth;
        if (heroChanges > 0) extra += " heroes=" + string.Join(">", end.HeroesUsed);
        if (restarted)
        {
            restarts++; restartsAll++;
            string source = Step9.Text(end, "RestartSource") ?? "?";
            Check(!sources.Contains(source), "the source " + source + " restarted twice on floor " + floor, problems);
            sources.Add(source);
            extra += " restart=" + source + "@t" + game.Turn;
            Check(s.Phase == DungeonRunPhase.PreMatch && s.Floor == floor, "a restart did not restage the floor", problems);
            continue;
        }
        result = escaped ? "escape" : r;
        Check(s.HeroSwitch == null, "the hero switch outlived the fight", problems);
        if (outcome != DungeonRunOutcome.Win) Check(Identity(s) == before, "a lost fight changed the run's identity", problems);
    }
    if (restarts > 0)
    {
        // A restart is played inside its game's cap, and only the floor's last game can stop at its cap (on turn cap + 1), so with
        // the budget's caps the games take at most 2 x cap0 + 1 turns. The GATE field counts the floors over it or with a
        // restarted game whose cap is not the budget's (the per-game check above).
        Check(turns <= 2 * cap0 + 1, "the floor's games took " + turns + " turns, over 2 x " + cap0 + " (+1 for a game stopped at its cap)", problems);
        gateMaxRestarts = Math.Max(gateMaxRestarts, restarts);
        if (turns > 2 * cap0 + 1 || capWrong) gateTurnExcess++;
    }
    if (result == "-") problems.Add("the floor was not decided in 4 games");
    return (result, games, restarts, turns, cap0, hp, left, start, decisions, extra, problems);
}

// One fight with its G line; false when it faulted (the run cannot go on).
bool Play(string head, DungeonRunState s, DungeonRunProfile profile, DungeonRunBoss boss, int li, Func<string> tail = null)
{
    var lo = loadouts[li];
    int floor = s.Floor, deck = s.Deck.Count;
    // The floor's slot (agg.py groups by it): I-combat ... III-major, or finale.
    var at = Floors(run.ScheduleFor(s)).FirstOrDefault(x => x.Floor == floor);
    string slot = at.Kind == "finale" ? "finale" : new[] { "?", "I", "II", "III", "IV" }[Math.Min(at.Act, 4)] + "-" + at.Kind;
    var sw = Stopwatch.StartNew();
    string line; bool ok = true;
    fights++;
    try
    {
        var f = Fight(s, profile, boss);
        switch (f.Result) { case "player": playerWins++; break; case "boss": bossWins++; break; case "turn-cap": caps++; break; case "tie": ties++; break; case "escape": escapes++; break; }
        failed += f.Problems.Count;
        foreach (string p in f.Problems) Console.Error.WriteLine($"  CHECK {head} {boss.Id}: {p}");
        line = $"result={f.Result} games={f.Games} restarts={f.Restarts} turns={f.Turns} cap={f.Cap} hp={f.Hp} boss={f.BossLeft}/{f.BossStart} decisions={f.Decisions}"
            + $" checks={(f.Problems.Count == 0 ? "ok" : "FAIL" + f.Problems.Count)}{f.Extra}";
    }
    catch (Exception e) { faults++; ok = false; line = "result=FAULT " + e.GetType().Name + ": " + e.Message.Split('\n')[0]; }
    Console.WriteLine($"G {head} sched={s.Schedule} floor={floor} slot={slot} boss={boss.Id} prov={boss.Provenance} tier={boss.Tier} room={(boss.HasFlag("room") ? 1 : 0)}"
        + $" lord={(boss.HasFlag(DungeonRunEncounterData.PersistentDamage) ? 1 : 0)} origin={lo.Origin} loadout={lo.Cls.Id}/{s.StarterDeck}/{s.Power} deck={deck} {line}{tail?.Invoke() ?? ""} {sw.ElapsedMilliseconds / 1000.0,6:F1}s");
    return ok;
}

string[] Bands = { "I-combat", "I-major", "II-combat", "II-major", "III-combat", "III-major" };
switch (mode)
{
    case "slots":
        foreach (string band in List("BANDS") ?? Bands)
        {
            int bi = Array.IndexOf(Bands, band);
            if (bi < 0) throw new Exception("unknown band " + band);
            int act = bi / 2 + 1; string kind = bi % 2 == 0 ? "combat" : "major";
            for (int g = from; g < from + n; g++)
            {
                var (s, li) = Start((37 * g + 11 * bi) % loadouts.Count, seedBase + 1000000L * bi + g, Env("SCHEDULE"), g);
                var profile = new DungeonRunProfile(); profile.Unlocked.AddRange(data.Unlocks.Keys);
                var floors = Floors(run.ScheduleFor(s)).Where(x => x.Act == act && x.Kind == kind).ToList();
                int floor = floors[g % floors.Count].Floor;
                try { RunWalk.ToFloor(run, s, profile, floor); }
                catch (Exception e) { faults++; Console.WriteLine($"G mode=slots band={band} g={g} sched={s.Schedule} floor={floor} result=FAULT walk: {e.Message.Split('\n')[0]}"); continue; }
                Play($"mode=slots band={band} g={g}", s, profile, run.CurrentBoss(s), li);
            }
        }
        break;
    case "finale":
    case "tekahn":
    {
        string schedule = Env("SCHEDULE") ?? (mode == "finale" ? "acts-v4h" : "acts-v4t");
        var finale = Floors(data.Schedules.Find(schedule) ?? throw new Exception("unknown schedule " + schedule)).Where(x => x.Kind == "finale").ToList();
        if (finale.Count == 0) throw new Exception(schedule + " has no finale");
        var which = List("FIGHTS")?.Select(int.Parse).ToList() ?? Enumerable.Range(0, finale.Count).ToList();
        long baseSeed = seedBase + (mode == "finale" ? 3000000L : 4000000L);
        for (int g = from; g < from + n; g++)
            foreach (int k in which)
            {
                var (s, li) = Start((37 * g + 5) % loadouts.Count, baseSeed + g, schedule, g);
                var profile = new DungeonRunProfile(); profile.Unlocked.AddRange(data.Unlocks.Keys);
                try { RunWalk.ToFloor(run, s, profile, finale[k].Floor); }
                catch (Exception e) { faults++; Console.WriteLine($"G mode={mode} g={g} fight={k} sched={s.Schedule} result=FAULT walk: {e.Message.Split('\n')[0]}"); continue; }
                Play($"mode={mode} g={g} fight={k}", s, profile, run.CurrentBoss(s), li);
            }
        break;
    }
    case "outliers":
    {
        List<string> rows;
        if (List("BOSS") is string[] only) rows = only.ToList();
        else
        {
            rows = data.Bosses.Where(b => b.IsEncounter && (b.Tier == "3" || b.Tier == "4")).Select(b => b.Id).ToList();
            string targets = Env("TARGETS_JSON") ?? VerifyPaths.Fixture("p9/targets.json");
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(targets));
            foreach (var x in doc.RootElement.GetProperty("B9").GetProperty("named").EnumerateArray()) if (!rows.Contains(x.GetString())) rows.Add(x.GetString());
        }
        int rowsFrom = Env("ROWS_FROM") is string rf ? int.Parse(rf) : 0;
        rows = rows.Skip(rowsFrom).Take(Env("ROWS") is string rc ? int.Parse(rc) : rows.Count).ToList();
        for (int ri = 0; ri < rows.Count; ri++)
        {
            var boss = data.Boss(rows[ri]);
            for (int g = from; g < from + n; g++)
            {
                string schedule = g % 2 == 0 ? "acts-v4h" : "acts-v4t";
                var (s, li) = Start((37 * g + 13 * (rowsFrom + ri)) % loadouts.Count, seedBase + 5000000L + 1000L * data.Bosses.IndexOf(boss) + g, schedule, g);
                var floors = Floors(run.ScheduleFor(s));
                var at = floors.FirstOrDefault(x => x.Kind == "combat" && data.Schedules.Member(boss, x.Pool));
                string slot = "combat";
                if (at.Floor == 0) { at = floors.FirstOrDefault(x => x.Kind != "finale" && data.Schedules.Member(boss, x.Pool)); slot = "major"; }
                if (at.Floor == 0) { Console.WriteLine($"G mode=outliers row={boss.Id} g={g} result=SKIP no floor of {s.Schedule} draws it"); continue; }
                var profile = new DungeonRunProfile(); profile.Unlocked.AddRange(data.Unlocks.Keys);
                try { RunWalk.ToFloor(run, s, profile, at.Floor); }
                catch (Exception e) { faults++; Console.WriteLine($"G mode=outliers row={boss.Id} g={g} result=FAULT walk: {e.Message.Split('\n')[0]}"); continue; }
                s.BossSequence[at.Floor - 1] = boss.Id;
                Play($"mode=outliers row={boss.Id} g={g} at={slot}", s, profile, boss, li);
            }
        }
        break;
    }
    case "longrun":
    {
        string[] origins = { "MonsterHunt", "Heist", "Rumble", "Tombs", "class" };
        for (int i = from; i < from + n; i++)
        {
            string origin = origins[i % origins.Length];
            var mine = Enumerable.Range(0, loadouts.Count).Where(x => loadouts[x].Origin == origin).ToList();
            if (mine.Count == 0) continue;
            var (s, li) = Start(mine[7 * (i / origins.Length) % mine.Count], seedBase + 6000000L + i, Env("SCHEDULE"), i);
            var profile = new DungeonRunProfile(); profile.Unlocked.AddRange(data.Unlocks.Keys);
            int won = 0, last = 0; string end = "defeat";
            try
            {
                while (true)
                {
                    RunWalk.ToFight(run, s);
                    if (s.Phase == DungeonRunPhase.Victory) { end = "victory"; break; }
                    if (s.Phase == DungeonRunPhase.Defeat) break;
                    last = s.Floor;
                    if (!Play($"mode=longrun run={i}", s, profile, run.CurrentBoss(s), li)) { end = "fault"; break; }
                    if (s.Phase != DungeonRunPhase.Defeat) won++;
                }
            }
            catch (Exception e) { faults++; end = "fault"; Console.WriteLine($"G mode=longrun run={i} result=FAULT walk: {e.Message.Split('\n')[0]}"); }
            Console.WriteLine($"R mode=longrun run={i} origin={origin} loadout={loadouts[li].Cls.Id}/{s.StarterDeck}/{s.Power} sched={s.Schedule} fights={Floors(run.ScheduleFor(s)).Count} won={won} last={last} end={end}");
        }
        break;
    }
    case "treasure":
    {
        var shared = data.Treasures.Where(Step9.Shared).ToList();
        for (int i = from; i < Math.Min(shared.Count, from + n); i++)
        {
            var t = shared[i];
            var other = Enumerable.Range(0, loadouts.Count).Where(x => loadouts[x].Origin != t.Pool).ToList();
            var (s, li) = Start(other[7 * i % other.Count], seedBase + 7000000L + i, "acts-v4h", i);
            if (loadouts[li].Origin == t.Pool) throw new Exception("treasure " + t.Id + ": no loadout of another origin");
            var profile = new DungeonRunProfile(); profile.Unlocked.AddRange(data.Unlocks.Keys);
            try { RunWalk.ToFloor(run, s, profile, 2); }
            catch (Exception e) { faults++; Console.WriteLine($"G mode=treasure treasure={t.Id} result=FAULT walk: {e.Message.Split('\n')[0]}"); continue; }
            string shell = Hold(s, t);
            var plan = run.BuildPlan(s, profile);
            bool inPlan = t.Active ? plan.PlayerDeck.Any(c => c.Id == shell) : plan.PlayerTreasures.Contains(t.Id);
            if (!inPlan) { failed++; Console.Error.WriteLine($"  CHECK treasure {t.Id}: the fight's plan does not hold it"); }
            var keep = playerWatch;
            playerWatch = shell == null ? null : new[] { shell };
            int before = shell != null && playerPlayed.TryGetValue(shell, out int b0) ? b0 : 0;
            Play($"mode=treasure treasure={t.Id} pool={t.Pool} active={(t.Active ? 1 : 0)} inplan={(inPlan ? 1 : 0)}", s, profile, run.CurrentBoss(s), li,
                () => shell == null ? "" : " played=" + ((playerPlayed.TryGetValue(shell, out int b1) ? b1 : 0) - before));
            playerWatch = keep;
        }
        break;
    }
    case "named":
    {
        string schedule = Env("SCHEDULE") ?? "acts-v4h";
        var bosses = (List("BOSS") ?? throw new Exception("MODE=named needs BOSS")).Select(data.Boss).ToList();
        foreach (var boss in bosses)
            for (int g = from; g < from + n; g++)
            {
                var (s, li) = Start((37 * g + 3 * data.Bosses.IndexOf(boss)) % loadouts.Count, seedBase + 8000000L + 1000L * data.Bosses.IndexOf(boss) + g, schedule, g);
                var floors = Floors(run.ScheduleFor(s));
                var at = floors.FirstOrDefault(x => x.Pool == "=" + boss.Id);
                if (at.Floor == 0) at = floors.FirstOrDefault(x => x.Kind != "finale" && data.Schedules.Member(boss, x.Pool));
                if (at.Floor == 0) throw new Exception("no floor of " + schedule + " draws " + boss.Id);
                var profile = new DungeonRunProfile(); profile.Unlocked.AddRange(data.Unlocks.Keys);
                try { RunWalk.ToFloor(run, s, profile, at.Floor); }
                catch (Exception e) { faults++; Console.WriteLine($"G mode=named row={boss.Id} g={g} result=FAULT walk: {e.Message.Split('\n')[0]}"); continue; }
                s.BossSequence[at.Floor - 1] = boss.Id;
                foreach (string id in playerCards ?? new string[0]) DungeonRunDeck.Add(s, id, DungeonRunDeck.OriginDraft);
                foreach (string id in List("PLAYER_TREASURES") ?? new string[0]) Hold(s, data.Treasure(id));
                Play($"mode=named row={boss.Id} g={g}", s, profile, boss, li);
            }
        break;
    }
    default: throw new Exception("unknown MODE " + mode);
}

foreach (var kv in SearchTelemetry.Details()) Console.Error.WriteLine($"SWALLOWED {kv.Value} x {kv.Key}");
string Counts(string[] ids, SortedDictionary<string, int> d) => string.Join(" ", ids.Distinct().Select(id => id + " x" + (d.TryGetValue(id, out int had) ? had : 0)));
if (watch != null) Console.WriteLine("watched (boss plays from hand) " + Counts(watch, watched) + "; boss Secrets sprung " + Counts(watch.Select(id => "boss:" + id).ToArray(), sprung));
if (playerWatch != null) Console.WriteLine("player watched (plays) " + Counts(playerWatch, playerPlayed) + "; player Secrets sprung " + Counts(playerWatch.Select(id => "player:" + id).ToArray(), sprung));
if (toki48) Console.WriteLine($"GATE GILA_BOSS_48t: fights {fights}, faults {faults}, Alternate Timeline casts {timelineCasts}, restarts {restartsAll}, most restarts on one floor {gateMaxRestarts}, floors over 2 x TurnCap {gateTurnExcess}");
Console.WriteLine($"run harness: mode {mode}, loadouts {loadouts.Count}, seed base {seedBase}, from {from}, n {n}");
Console.WriteLine($"fights {fights}, games {gamesPlayed}, player wins {playerWins}, boss wins {bossWins}, turn caps {caps}, ties {ties}, escapes {escapes}, restarts {restartsAll}, faults {faults}, fallbacks {bossFallbacks + playerFallbacks} (boss {bossFallbacks}, player {playerFallbacks}), failed checks {failed}, swallowed leaf exceptions {SearchTelemetry.SwallowedLeafErrors}");
return faults + failed + bossFallbacks + playerFallbacks + SearchTelemetry.SwallowedLeafErrors;
