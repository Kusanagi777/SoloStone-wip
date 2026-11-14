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

int failures = 0, passes = 0;
void Check(string name, bool ok, string detail = "")
{
    if (ok) passes++; else failures++;
    Console.WriteLine((ok ? "PASS " : "FAIL ") + name + (detail.Length > 0 ? "  (" + detail + ")" : ""));
}
void Guard(string name, Action body)
{
    try { body(); }
    catch (Exception e) { failures++; Console.WriteLine("FAIL " + name + " threw " + e.GetType().Name + ": " + e.Message.Split('\n')[0] + " @ " + (e.StackTrace ?? "").Split('\n').FirstOrDefault()?.Trim()); }
}
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
string R(string f) => File.ReadAllText(Path.Combine(root, f));
string runJson = R("dungeon-run/run-v1.json"), scheduleJson = R("dungeon-run/schedule-v1.json");
DungeonRunData Load() => DungeonRunData.Parse(runJson, R("dungeon-run/pool-v1.json"), R("npc/decks-v1.json"),
    R("dungeon-run/catalog-v1.json"), scheduleJson, R("dungeon-run/tavern-v1.json"));
var data = Load();
var run = new DungeonRun(data);
var all = new DungeonRunProfile(); all.Unlocked.AddRange(data.Unlocks.Keys);
Card C(string id) => Cards.FromId(id);
var facts = System.Text.Json.JsonDocument.Parse(File.ReadAllText(VerifyPaths.Repo("facts/violet-hold-2026-09.json")));
var factById = new Dictionary<string, System.Text.Json.JsonElement>(StringComparer.Ordinal);
foreach (var c in facts.RootElement.GetProperty("cards").EnumerateArray()) factById[c.GetProperty("id").GetString()] = c;
string FactName(string id) => factById[id].GetProperty("name").GetString();
string FactText(string id) => factById[id].TryGetProperty("text", out var t) ? t.GetString() : null;

DungeonRunState NewRun(string row, string deck, long seed)
{
    var cls = data.Class(row);
    return run.NewRun(all, cls.Id, deck ?? cls.DefaultDeck, cls.DefaultPower, seed,
        signatureId: run.SignatureOffer(all, data.FindClass(cls.Id), seed).FirstOrDefault());
}
Game Start(RunMatchPlan plan, int seed)
{
    var cfg = new GameConfig { RandomSeed = seed, Shuffle = true, SkipMulligan = false, History = true, Logging = false, FormatType = FormatType.FT_WILD };
    DungeonRunSetup.Configure(cfg, plan);
    var g = new Game(cfg);
    g.StartGame();
    DungeonRunSetup.ApplyAfterStart(g, plan);
    return g;
}
void Finish(Game g)
{
    g.Process(ChooseTask.Mulligan(g.Player1, new List<int>()));
    g.Process(ChooseTask.Mulligan(g.Player2, new List<int>()));
    g.MainBegin();
    if (g.CurrentPlayer != g.Player1) throw new Exception("run player not first");
}
string RowOf(string deck) => "TRLA_209h_" + char.ToUpperInvariant(deck[7]) + deck.Substring(8, deck.IndexOf('_', 7) - 8);
// A new run of the deck's Rikkar row, its shrine upgrades and passive treasures set, the teammates added as teammate records.
DungeonRunState Prepared(string deck, long seed, string[] upgrades = null, string[] treasures = null, string[] mates = null)
{
    var s = NewRun(RowOf(deck), deck, seed);
    foreach (string u in upgrades ?? Array.Empty<string>()) s.ShrineUpgrades.Add(u);
    foreach (string t in treasures ?? Array.Empty<string>()) s.Treasures.Add(t);
    foreach (string m in mates ?? Array.Empty<string>()) DungeonRunDeck.Add(s, m, DungeonRunDeck.OriginTeammate);
    return s;
}
(DungeonRunState s, RunMatchPlan plan, Game g) F(string deck, long seed, string[] upgrades = null, string[] treasures = null, string[] mates = null)
{
    var s = Prepared(deck, seed, upgrades, treasures, mates);
    var plan = run.BuildPlan(s);
    var g = Start(plan, run.MatchSeed(s));
    Finish(g);
    if (s.BoardShrine == null || ShrineRules.Of(g.Player1).Count == 0) throw new Exception("no board shrine on " + deck);
    return (s, plan, g);
}
void Mana(Controller c, int n) { c.BaseMana = n; c.UsedMana = 0; c.OverloadLocked = 0; c.OverloadOwed = 0; }
IPlayable ToHand(Game g, Controller c, string id)
{
    while (c.HandZone.Count >= 9) { var old = c.HandZone[0]; c.HandZone.Remove(old); c.SetasideZone.Add(old); }
    var e = Entity.FromCard(c, C(id)); Generic.AddHandPhase.Invoke(c, e); g.DeathProcessingAndAuraUpdate(); return e;
}
IPlayable InHand(Game g, string id) => g.Player1.HandZone.FirstOrDefault(p => p.Card.Id == id);
void SetDeck(Game g, Controller c, params string[] ids)
{
    foreach (var p in c.DeckZone.ToArray()) { c.DeckZone.Remove(p); c.SetasideZone.Add(p); }
    foreach (string id in ids) c.DeckZone.Add(Entity.FromCard(c, C(id)));
    g.DeathProcessingAndAuraUpdate();
}
Minion Board(Game g, Controller c, string id) { var m = (Minion)Entity.FromCard(c, C(id)); Generic.SummonBlock(g, m, -1, c.Hero); g.DeathProcessingAndAuraUpdate(); return m; }
bool Play(Game g, IPlayable card, ICharacter target = null, int chooseOne = 0) => g.Process(PlayCardTask.Any(g.CurrentPlayer, card, target, -1, chooseOne));
void EndTurn(Game g) => g.Process(EndTurnTask.Any(g.CurrentPlayer));
void ClearExceptShrines(Game g)
{
    foreach (var c in new[] { g.Player1, g.Player2 })
        foreach (var m in c.BoardZone.Slots.OfType<Minion>().Where(m => !ShrineRules.IsMarked(m)).ToArray()) { c.BoardZone.Remove(m); c.SetasideZone.Add(m); }
    g.DeathProcessingAndAuraUpdate();
}
Minion ShrineOf(Game g, Controller c) => ShrineRules.Of(c).FirstOrDefault();
void Kill(Game g, Minion m) { m.Destroy(); g.DeathProcessingAndAuraUpdate(); }
Minion Ready(Minion m) { m.IsExhausted = false; return m; }
bool Attack(Game g, Minion a, ICharacter t) => g.Process(MinionAttackTask.Any(g.CurrentPlayer, a, t));
bool HeroAttack(Game g, ICharacter t) => g.Process(HeroAttackTask.Any(g.CurrentPlayer, t));
bool Power(Game g, ICharacter target = null) => g.Process(HeroPowerTask.Any(g.CurrentPlayer, target));
int Heal(Controller c) => c.Hero.Health;
IEnumerable<Enchantment> Ench(IEntity e) => (IEnumerable<Enchantment>)(e as IPlayable)?.AppliedEnchantments ?? Array.Empty<Enchantment>();
int Count(IEntity e, string ench) => Ench(e).Count(x => x.Card.Id == ench);
Minion Mate(Game g, string id, ICharacter target = null) { Mana(g.Player1, 10); var m = (Minion)ToHand(g, g.Player1, id); Play(g, m, target); return m; }
Minion Fixture(Game g) { ShrineRules.Place(g.Player2, DungeonRunShrines.FixtureShrine); g.DeathProcessingAndAuraUpdate(); return ShrineOf(g, g.Player2); }
// The run player's companion as its Start of Game draw put it in hand, played with 10 Mana (the Overload state kept).
Minion PlayDrawn(Game g, string id, ICharacter target = null)
{
    var m = (Minion)InHand(g, id) ?? throw new Exception(id + " was not drawn at the start of the game");
    g.Player1.BaseMana = 10; g.Player1.UsedMana = 0;
    if (!Play(g, m, target)) throw new Exception(id + " could not be played");
    return m;
}
List<int> TriggerSources(Game g, int from) => g.PowerHistory.Full.Skip(from).OfType<SabberStoneCore.Kettle.PowerHistoryBlockStart>()
    .Where(b => b.BlockType == BlockType.TRIGGER).Select(b => b.Source).ToList();
// The minion stats of the run player's deck, by entity id.
Dictionary<int, (int A, int H)> DeckStats(Controller c) => c.DeckZone.OfType<Minion>().ToDictionary(m => m.Id, m => (m.AttackDamage, m.Health));


// ======================= phase-7 review fixes (c-p7fix) =======================
// Every game here is a real engine game through the run setup path: NewRun -> BuildPlan -> DungeonRunSetup.Configure ->
// StartGame -> ApplyAfterStart -> both mulligans -> MainBegin (F / G above), then the board is arranged with real summons.
Controller P(Game g) => g.Player1;
void Pick(Game g) { var c = g.CurrentPlayer; int n = 0; while (c.Choice != null && n++ < 5) g.Process(ChooseTask.Pick(c, c.Choice.Choices[0])); }
void Obs(string s) => Console.WriteLine("OBS  " + s);
string Hand(Controller c) => string.Join(",", c.HandZone.Select(x => x.Card.Id + "(" + x.Cost + ")"));
string Brd(Controller c) => string.Join(",", c.BoardZone.Select(m => m.Card.Id + " " + m.AttackDamage + "/" + m.Health + (m.IsDormant ? " Z" + ShrineRules.TurnsLeft(m) : "")));
Game G(string deck, long seed, string[] upgrades = null, string[] treasures = null)
{
    var (s, plan, g) = F(deck, seed, upgrades, treasures);
    ClearExceptShrines(g);
    return g;
}
Minion E(Game g, string id) => Board(g, g.Player2, id);
Minion M(Game g, string id) => Board(g, g.Player1, id);
void BossTurn(Game g) { EndTurn(g); Mana(g.Player2, 10); }
void BackToP1(Game g) { EndTurn(g); Mana(g.Player1, 10); }
var budget = new Budget(6000, 2000, 300, 6, 3, 100, 8, turnSearchSeconds: 6, turnWallSeconds: 8);
// The boss's own fair AI (DungeonRunBossPilot + GoldenPilot, as c-bosspilot) decides its whole turn on the real game;
// returns the decisions, the fallbacks and the leaf exceptions the beam swallowed meanwhile (SearchTelemetry).
(int decisions, int fallbacks, int swallowed, string reasons) BossPlays(Game g, RunMatchPlan plan, DungeonRunBoss boss)
{
    var pilot = DungeonRunBossPilot.Build(boss, plan);
    var profile = pilot.Profile.WithBudget(budget);
    var golden = new GoldenPilot(profile, pilot.Plan, null, null);
    int before = SearchTelemetry.SwallowedLeafErrors, decisionIndex = 0, decisions = 0, fallbacks = 0, guard = 0;
    var reasons = new List<string>();
    int turn = g.Turn;
    while (g.State != State.COMPLETE && g.CurrentPlayer == g.Player2 && g.Turn == turn && guard++ < 60)
    {
        var (task, trace) = BotDriver.Decide(g, g.Player2.PlayerId, pilot.Deck.Style,
            st => golden.DecidePilot(st, new GoldenContext(g.Turn, decisionIndex, unchecked(997 * g.Turn + decisionIndex * 31), profile)),
            ref decisionIndex, null);
        decisions++;
        if (trace?.Kind == "fallback") { fallbacks++; reasons.Add(trace.FallbackReason); }
        if (task is ChooseTask pick && g.Step != Step.BEGIN_MULLIGAN)
        {
            var decision = g.GetPendingDecisions().Single(d => d.ControllerId == task.Controller.Id);
            if (!g.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(decision.Id, decision.ControllerId, pick.Choices.ToArray()), out var why))
                throw new Exception("engine rejected the boss choice: " + why);
        }
        else if (!g.Process(task)) throw new Exception("engine rejected the boss task " + task);
    }
    return (decisions, fallbacks, SearchTelemetry.SwallowedLeafErrors - before, string.Join("; ", reasons));
}
// A run on the deck's Rikkar row whose current fight is against <bossId> (the boss's own deck, power and passive).
(DungeonRunState s, RunMatchPlan plan, Game g, DungeonRunBoss boss) Against(string deck, string bossId, long seed, string[] upgrades = null)
{
    var s = Prepared(deck, seed, upgrades);
    s.BossSequence[s.Floor - 1] = bossId;
    var plan = run.BuildPlan(s);
    var g = Start(plan, run.MatchSeed(s));
    Finish(g);
    if (ShrineRules.Of(g.Player1).Count == 0) throw new Exception("no board shrine on " + deck);
    return (s, plan, g, run.CurrentBoss(s));
}
T FairField<T>(FairBotState st, string name) => (T)typeof(FairBotState).GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(st);

// ---------------------------------------------------------------- 1 Bwonsamdi's Covenant (TRLA_147)
Guard("C1", () => {
    var g = G("rumble_priest_147", 518); var e = E(g, "CS2_182"); e.Damage = 2; var mine = M(g, "CS2_182"); mine.Damage = 2;
    Mana(P(g), 10); Play(g, ToHand(g, P(g), "EX1_621"));
    Check("C1 the run player's Circle of Healing: the damaged enemy Yeti takes 4 and dies, the run player's own Yeti is healed to 5", e.IsDead && mine.Health == 5, $"enemy dead {e.IsDead}, own {mine.Health}");
});
Guard("C2", () => {
    var g = G("rumble_priest_147", 519); var e2 = E(g, "CS2_182"); e2.Damage = 2;
    BossTurn(g); bool cast = Play(g, ToHand(g, g.Player2, "EX1_621"));
    Check("C2 the boss's own Circle of Healing on its damaged Yeti (3 Health) with the Covenant on the run board: 4 damage instead, the Yeti dies", cast && e2.IsDead, $"cast {cast}, Yeti {e2.Health} dead {e2.IsDead}");
});
Guard("C3", () => {
    var (s, plan, g, boss) = Against("rumble_priest_147", "ashen_abbess", 801);
    Check("C3 setup: the fight is the real ashen_abbess with its Vigil passive", boss.Id == "ashen_abbess" && plan.BossHeroPowerId == "passive_vigil", boss.Id + " " + plan.BossHeroPowerId);
    g.Player2.Hero.Damage = 10; int before = g.Player2.Hero.Health;
    EndTurn(g);
    int after = g.Player2.Hero.Health;
    Check("C3 Vigil ('restore 4 Health to your hero' at the boss's turn start) with the Covenant awake: the boss hero takes 4 instead (20 -> 16)", after == before - 4, $"{before} -> {after}");
    var plays = BossPlays(g, plan, boss);
    Check("C3 the boss's fair AI then plays its turn on the Covenant board: no fallback, no swallowed search exception", plays.fallbacks == 0 && plays.swallowed == 0, $"decisions {plays.decisions} fallbacks {plays.fallbacks} swallowed {plays.swallowed} {plays.reasons}");
});
Guard("C4", () => {
    var g = G("rumble_priest_147", 520); var y = M(g, "CS2_182");
    BossTurn(g); g.Player2.Hero.Damage = 10; int before = g.Player2.Hero.Health;
    bool cast = Play(g, ToHand(g, g.Player2, "LOOT_043"), y);
    Check("C4 the boss's Lifesteal spell (Lesser Amethyst Spellstone, 3 to the run player's Yeti): its Lifesteal heal of its own hero damages it instead", cast && y.Damage == 3 && g.Player2.Hero.Health == before - 3, $"cast {cast}, Yeti damage {y.Damage}, boss {before} -> {g.Player2.Hero.Health}");
});
Guard("C5", () => {
    var g = G("rumble_priest_147", 521); var sh = ShrineOf(g, P(g)); Mana(P(g), 10); Play(g, ToHand(g, P(g), "EX1_332"), sh);
    var e = E(g, "CS2_182"); e.Damage = 2;
    BossTurn(g); Play(g, ToHand(g, g.Player2, "EX1_621"));
    Check("C5 a silenced Covenant: the boss's Circle heals its Yeti normally (5)", sh.IsSilenced && !e.IsDead && e.Health == 5, $"silenced {sh.IsSilenced}, Yeti {e.Health}");
});
Guard("C6", () => {
    var g = G("rumble_priest_147", 522); var sh = ShrineOf(g, P(g)); Kill(g, sh);
    var e = E(g, "CS2_182"); e.Damage = 2;
    BossTurn(g); Play(g, ToHand(g, g.Player2, "EX1_621"));
    Check("C6 a dormant Covenant has no effect: the boss's Circle heals its Yeti (5)", ShrineRules.Of(P(g)).All(m => m.IsDormant) && e.Health == 5, $"Yeti {e.Health}");
});

// ---------------------------------------------------------------- 2 control changes and a run shrine
Guard("K1", () => {
    int stolen = 0, other = 0;
    for (int i = 0; i < 16; i++)
    {
        var g = G("rumble_druid_116", 700 + i); var w = M(g, "CS2_231"); var sy = E(g, "EX1_016"); Kill(g, sy);
        if (w.Controller == g.Player2 && ShrineOf(g, P(g)).Controller == g.Player1) stolen++; else other++;
    }
    Check("K1 Sylvanas Windrunner (boss) dies against shrine + Wisp: it always takes the Wisp, never picks the shrine (16 seeds)", stolen == 16, $"stolen {stolen}, otherwise {other}");
});
Guard("K2", () => {
    var g = G("rumble_druid_116", 720); var sh = ShrineOf(g, P(g)); var sy = E(g, "EX1_016"); Kill(g, sy);
    Check("K2 Sylvanas with only the run shrine on the enemy board: nothing is taken, the shrine stays the run player's", sh.Controller == g.Player1 && g.Player2.BoardZone.Count == 0, Brd(P(g)) + " | " + Brd(g.Player2));
});
Guard("K3", () => {
    int ok = 0, bad = 0;
    for (int i = 0; i < 16; i++)
    {
        var g = G("rumble_druid_116", 730 + i); M(g, "CS2_231"); M(g, "CS2_231"); M(g, "CS2_231");
        BossTurn(g); Play(g, ToHand(g, g.Player2, "EX1_085"));
        bool took = g.Player2.BoardZone.Count(m => m.Card.Id == "CS2_231") == 1 && ShrineOf(g, P(g)).Controller == g.Player1;
        if (took) ok++; else bad++;
    }
    Check("K3 Mind Control Tech against shrine + 3 Wisps (4 minions, so it is powered up): it always takes a Wisp, never the shrine (16 seeds)", ok == 16, $"{ok} took a Wisp, {bad} did not");
});
Guard("K4", () => {
    var g = G("rumble_druid_116", 740); var sh = ShrineOf(g, P(g)); var w = M(g, "CS2_231"); BossTurn(g);
    var mc = ToHand(g, g.Player2, "CS1_113"); int mana = g.Player2.RemainingMana;
    bool valid = mc.IsValidPlayTarget(sh), listed = mc.ValidPlayTargets.Contains(sh);
    bool option = g.Player2.Options().OfType<PlayCardTask>().Any(t => t.Source == mc && t.Target == sh);
    bool played = Play(g, mc, sh);
    Check("K4 Mind Control cannot target the run shrine: not a valid target, not a boss option, the play is refused, Mana and card kept",
        !valid && !listed && !option && !played && g.Player2.RemainingMana == mana && mc.Zone == g.Player2.HandZone && sh.Controller == g.Player1,
        $"valid {valid} listed {listed} option {option} played {played} mana {mana}->{g.Player2.RemainingMana}");
    bool onWisp = Play(g, mc, w);
    Check("K4 Mind Control still takes the Wisp beside it", onWisp && w.Controller == g.Player2, $"played {onWisp}");
});
Guard("K5", () => {
    var g = G("rumble_druid_116", 741); var sh = ShrineOf(g, P(g)); BossTurn(g);
    var ids = new[] { "EX1_091", "EX1_334", "CFM_603", "TIME_435", "ICC_849", "CATA_496", "LOOTA_805" };
    var refused = new List<string>(); var accepted = new List<string>();
    foreach (string id in ids)
    {
        IPlayable card;
        try { card = ToHand(g, g.Player2, id); } catch (Exception) { refused.Add(id + "(no card)"); continue; }
        if (card.IsValidPlayTarget(sh) || card.ValidPlayTargets.Contains(sh)) accepted.Add(id); else refused.Add(id);
        card.Zone?.Remove(card);
    }
    Check("K5 every targeted take-control card refuses the run shrine as its target (Cabal Shadow Priest, Shadow Madness, Potion of Madness, Eternus, Embrace Darkness, Cursed Chains, Amulet of Domination)", accepted.Count == 0, "accepted " + string.Join(",", accepted) + " refused " + string.Join(",", refused));
    var fb = ToHand(g, g.Player2, "CS2_029");
    Check("K5 a card that does not take control (Fireball) can still target the shrine", fb.IsValidPlayTarget(sh), "");
    Check("K5 the play-tree reader finds take-control cards and nothing else", ShrineRules.TakesControlOfTarget(Cards.FromId("CS1_113")) && !ShrineRules.TakesControlOfTarget(Cards.FromId("CS2_029"))
        && !ShrineRules.TakesControlOfTarget(Cards.FromId("EX1_085")) && !ShrineRules.TakesControlOfTarget(Cards.FromId("EX1_016")), "");
});
Guard("K6", () => {
    var (s, plan, g, boss) = Against("rumble_druid_116", "pit_champion", 742);
    var sh = ShrineOf(g, P(g)); EndTurn(g); Mana(g.Player2, 10);
    var mc = ToHand(g, g.Player2, "CS1_113");
    var plays = BossPlays(g, plan, boss);
    Check("K6 the boss's fair AI holding Mind Control against a shrine-only board never spends it on the shrine (it cannot), no fallback, no swallowed exception",
        sh.Controller == g.Player1 && plays.fallbacks == 0 && plays.swallowed == 0, $"mc {mc.Zone?.Type} decisions {plays.decisions} fallbacks {plays.fallbacks} swallowed {plays.swallowed}");
});

// ---------------------------------------------------------------- 3 Slamma Jamma (TRLA_169)
Guard("J1", () => {
    var g = G("rumble_hunter_110", 415); var w = M(g, "CS2_231"); var dwa = M(g, "EX1_162"); var sh = ShrineOf(g, P(g));
    int before = w.AttackDamage;
    Mate(g, "TRLA_169");
    int after = w.AttackDamage; int dwaAfter = dwa.AttackDamage;
    Kill(g, dwa);
    Check("J1 Slamma Jamma doubles current Attack, aura included: Wisp next to Dire Wolf Alpha 2 -> 4, after the Wolf dies 3", before == 2 && after == 4 && w.AttackDamage == 3, $"{before} -> {after} -> {w.AttackDamage}");
    Check("J1 the Wolf's own Attack doubles (2 -> 4), the 0-Attack shrine gains nothing", dwaAfter == 4 && sh.AttackDamage == 0 && Count(sh, "TRLA_169e") == 0, $"wolf {dwaAfter} shrine {sh.AttackDamage}");
});
Guard("J2", () => {
    var g = G("rumble_hunter_110", 416); var y = M(g, "CS2_182"); var sm = Mate(g, "TRLA_169");
    Check("J2 a lone Yeti 4 -> 8; Slamma Jamma itself is not doubled", y.AttackDamage == 8 && sm.AttackDamage == sm.Card.ATK && Count(sm, "TRLA_169e") == 0, $"Yeti {y.AttackDamage}, Slamma {sm.AttackDamage}");
});

// ---------------------------------------------------------------- 4 Naga Tonguelasher (TRLA_160) and Overload over the crystals
Guard("O1", () => {
    var s = Prepared("rumble_shaman_155", 802);
    var plan = run.BuildPlan(s);
    var g = Start(plan, run.MatchSeed(s)); Finish(g); ClearExceptShrines(g);
    g.Player1.BaseMana = 1; g.Player1.UsedMana = 0;
    var t = Entity.FromCard(g.Player1, Cards.FromId("TRLA_160")); Generic.AddHandPhase.Invoke(g.Player1, t);
    bool ok = g.Process(PlayCardTask.Any(g.Player1, t));
    int owed = g.Player2.OverloadOwed;
    g.Process(EndTurnTask.Any(g.Player1));
    var b = g.Player2;
    Check("O1 Tonguelasher on turn 1: the boss owes 3 but has 1 crystal on its turn: 1 locked, 0 Mana left, never negative",
        ok && owed == 3 && b.BaseMana == 1 && b.OverloadLocked == 1 && b.RemainingMana == 0 && b[GameTag.OVERLOAD_LOCKED] == 1,
        $"owed {owed} BaseMana {b.BaseMana} locked {b.OverloadLocked} remaining {b.RemainingMana} tag {b[GameTag.OVERLOAD_LOCKED]}");
});
Guard("O2", () => {
    var g = G("rumble_shaman_155", 464); Mate(g, "TRLA_160"); g.Player2.BaseMana = 4; EndTurn(g);
    var b = g.Player2;
    Check("O2 Tonguelasher against a boss with 5 crystals on its turn: 3 locked, 2 left", b.BaseMana == 5 && b.OverloadLocked == 3 && b.RemainingMana == 2, $"{b.BaseMana} locked {b.OverloadLocked} remaining {b.RemainingMana}");
});
Guard("O3", () => {
    var g = G("rumble_shaman_154", 466); Mate(g, "TRLA_160"); int owed = g.Player2.OverloadOwed; g.Player2.BaseMana = 1; EndTurn(g);
    var b = g.Player2;
    Check("O3 Tonguelasher with Tribute from the Tides (three Battlecries): the boss owes 9, has 2 crystals: 2 locked, 0 left", owed == 9 && b.BaseMana == 2 && b.OverloadLocked == 2 && b.RemainingMana == 0,
        $"owed {owed} BaseMana {b.BaseMana} locked {b.OverloadLocked} remaining {b.RemainingMana}");
    EndTurn(g); EndTurn(g);
    Check("O3 the excess is dropped, not carried: the boss's next turn has nothing locked", g.Player2.OverloadLocked == 0 && g.Player2.RemainingMana == g.Player2.BaseMana, $"locked {g.Player2.OverloadLocked}");
});
Guard("O4", () => {
    var g = G("rumble_shaman_155", 467); Mana(P(g), 10); P(g).BaseMana = 3; P(g).UsedMana = 0;
    Play(g, ToHand(g, P(g), "EX1_238"), g.Player2.Hero); // Lightning Bolt: Overload (1)
    EndTurn(g); EndTurn(g);
    Check("O4 a player's own Overload within its crystals is unchanged (1 locked of 4)", P(g).OverloadLocked == 1 && P(g).BaseMana == 4, $"locked {P(g).OverloadLocked} of {P(g).BaseMana}");
});

// ---------------------------------------------------------------- 5 pre-existing fair-view faults the fresh seeds found
Guard("W1", () => {
    var (s, plan, g, boss) = Against("rumble_warrior_104", "gilded_king", 70027, new[] { "TRLA_809", "TRLA_803" });
    Mana(P(g), 10);
    var forge = ToHand(g, P(g), "LOOTA_841");
    bool played = Play(g, forge);
    Pick(g);
    var weapon = P(g).Hero.Weapon;
    Check("W1 Portable Forge: the picked Legendary weapon is equipped and leaves the set-aside zone", played && weapon != null && !P(g).SetasideZone.Contains(weapon) && weapon.Zone?.Type != Zone.SETASIDE
        && g.IdEntityDic[P(g).Hero[GameTag.WEAPON]] == weapon, $"played {played} weapon {weapon?.Card.Id} zone {weapon?.Zone?.Type}");
    EndTurn(g);
    var view = FairBotState.Create(g, g.Player2.PlayerId, 1, DeckStyle.Value);
    var fair = FairField<Game>(view, "game");
    bool clones = true; try { fair.CloneExact(history: true); fair.CloneExact(history: true).CloneExact(history: true); } catch (Exception) { clones = false; }
    Check("W1 the boss's fair view holds the run player's weapon in play and clones", clones && fair.Player1.Hero.Weapon != null && fair.IdEntityDic.ContainsKey(fair.Player1.Hero.Weapon.Id)
        && ReferenceEquals(fair.IdEntityDic[fair.Player1.Hero.Weapon.Id], fair.Player1.Hero.Weapon), $"clones {clones}");
    var plays = BossPlays(g, plan, boss);
    Check("W1 the boss's fair AI plays its turn: no fallback, no swallowed exception", plays.fallbacks == 0 && plays.swallowed == 0, $"decisions {plays.decisions} fallbacks {plays.fallbacks} swallowed {plays.swallowed} {plays.reasons}");
});
Guard("W2", () => {
    var (s, plan, g, boss) = Against("rumble_druid_116", "lady_of_thorns", 70036);
    EndTurn(g);
    var oh = E(g, "EDR_031"); Mana(g.Player2, 10);
    var view = FairBotState.Create(g, g.Player2.PlayerId, 1, DeckStyle.Value);
    var fair = FairField<Game>(view, "game");
    // The boss's own deck order is hidden from its search too; the root view may still hold its own deck, so the hidden card
    // auto-played here is the first Unknown deck card of either side (the run player's deck is always masked).
    var top = fair.Player2.DeckZone.Concat(fair.Player1.DeckZone).FirstOrDefault(x => x is Unknown);
    bool threw = false, played = true; string why = "";
    if (top != null) { try { played = SabberStoneCore.Actions.AutoPlayRules.Play(top, fair.Player2.BoardZone.First(m => m.Card.Id == "EDR_031")); } catch (Exception e) { threw = true; why = e.GetType().Name + ": " + e.Message; } }
    Check("W2 an end-of-turn auto-play (Ohn'ahra's) of an Unknown deck card in the boss's fair view: nothing is played, the card stays, no exception",
        top != null && !threw && !played && top.Zone?.Type == Zone.DECK, $"top {top?.GetType().Name} threw {threw} {why} played {played}");
    var plays = BossPlays(g, plan, boss);
    Check("W2 the boss's fair AI plays a turn with Ohn'ahra on its board (end-of-turn auto-plays in its search): no fallback, no swallowed exception",
        plays.fallbacks == 0 && plays.swallowed == 0, $"decisions {plays.decisions} fallbacks {plays.fallbacks} swallowed {plays.swallowed} {plays.reasons}");
});

// ---------------------------------------------------------------- 6 Unknown card transfer in a Tombs fight (S3's RecordTransfer guard)
Guard("U1", () => {
    var p = all;
    var cls = data.FindClass("MAGE_ROGUE");
    var st = run.NewRun(p, cls.Id, "tombs_reno_spellslinger", cls.Powers[0], 9301, signatureId: run.SignatureOffer(p, cls, 9301).FirstOrDefault());
    var plan = run.BuildPlan(st);
    var g = Start(plan, run.MatchSeed(st)); Finish(g);
    EndTurn(g);
    var view = FairBotState.Create(g, g.Player2.PlayerId, 1, DeckStyle.Value);
    var fair = FairField<Game>(view, "game");
    var hidden = fair.Player1.DeckZone.IsEmpty ? null : fair.Player1.DeckZone.TopCard;
    bool threw = false; IPlayable moved = null; string why = "";
    try { moved = Generic.DrawFrom(fair.Player2, fair.Player1); } catch (Exception e) { threw = true; why = e.GetType().Name + ": " + e.Message; }
    Check("U1 Tombs (Reno) fight, the boss's fair view: a run-player deck card it holds as Unknown changes controller (a draw from the other deck) with no exception",
        hidden is Unknown && !threw && moved == hidden && moved.Controller == fair.Player2 && fair.Player2.HandZone.Contains(moved), $"hidden {hidden?.GetType().Name} threw {threw} {why}");
    var plays = BossPlays(g, plan, run.CurrentBoss(st));
    Check("U1 the boss's fair AI plays its Tombs turn: no fallback, no swallowed exception", plays.fallbacks == 0 && plays.swallowed == 0, $"decisions {plays.decisions} fallbacks {plays.fallbacks} swallowed {plays.swallowed} {plays.reasons}");
});

// ---------------------------------------------------------------- 7 run layer: acts-v1 is not a shrine schedule
Guard("R1", () => {
    var cls = data.Class("TRLA_209h_Druid");
    var v1 = run.NewRun(all, cls.Id, "rumble_druid_115", cls.DefaultPower, 811, schedule: "acts-v1", signatureId: run.SignatureOffer(all, data.FindClass(cls.Id), 811).FirstOrDefault());
    var v2 = run.NewRun(all, cls.Id, "rumble_druid_115", cls.DefaultPower, 811, signatureId: run.SignatureOffer(all, data.FindClass(cls.Id), 811).FirstOrDefault());
    Check("R1 a new Rikkar run on acts-v1 (kept for version-1 saves) gets no board shrine and no phase-7 draft; the default acts-v2 run gets its shrine",
        v1.Schedule == "acts-v1" && v1.BoardShrine == null && !DungeonRun.OffersPhase7Drafts(v1) && v2.Schedule == "acts-v2" && v2.BoardShrine == "TRLA_115" && DungeonRun.OffersPhase7Drafts(v2),
        $"v1 {v1.Schedule} shrine {v1.BoardShrine ?? "-"} | v2 {v2.Schedule} shrine {v2.BoardShrine ?? "-"}");
    Check("R1 HasPhase7Drafts: acts-v2 yes, acts-v1 and act1 no", DungeonRun.HasPhase7Drafts(data.Schedules.Find("acts-v2")) && !DungeonRun.HasPhase7Drafts(data.Schedules.Find("acts-v1")) && !DungeonRun.HasPhase7Drafts(data.Schedules.Find("act1")), "");
    var plan = run.BuildPlan(v1);
    Check("R1 the acts-v1 run's plan has no shrine", plan.PlayerShrine == null && plan.PlayerShrineUpgrades.Count == 0, "");
});

// ---------------------------------------------------------------- 8 the shrine outside a match: hub, summary, archive
Guard("H1", () => {
    Check("H1 ShrineLine: shrine and upgrades in pick order; null with no shrine",
        NativeDungeonRunText.ShrineLine("Gonk's Mark", new[] { "Pact with the Loa", "Windfall" }) == "Shrine: Gonk's Mark \u00b7 Upgrades: Pact with the Loa, Windfall"
        && NativeDungeonRunText.ShrineLine("Gonk's Mark", new string[0]) == "Shrine: Gonk's Mark" && NativeDungeonRunText.ShrineLine(null, new[] { "x" }) == null,
        NativeDungeonRunText.ShrineLine("Gonk's Mark", new[] { "Pact with the Loa", "Windfall" }));
    var profile = new DungeonRunProfile(); profile.Unlocked.AddRange(data.Unlocks.Keys);
    var s = Prepared("rumble_druid_115", 821, new[] { "TRLA_805", "TRLA_809" });
    run.Abandon(s, profile);
    var entry = profile.Archive.Last();
    Check("H1 the archive entry keeps the board shrine and its upgrades in pick order, apart from the treasures",
        entry.BoardShrine == "TRLA_115" && string.Join(",", entry.ShrineUpgrades) == "TRLA_805,TRLA_809" && !entry.Treasures.Contains("TRLA_805"), $"{entry.BoardShrine} [{string.Join(",", entry.ShrineUpgrades)}] treasures [{string.Join(",", entry.Treasures)}]");
    var save = new DungeonRunSave { Profile = profile };
    var copy = DungeonRunStore.Clone(save);
    var back = copy.Profile.Archive.Last();
    Check("H1 the archive's shrine upgrades survive a save round trip", string.Join(",", back.ShrineUpgrades) == "TRLA_805,TRLA_809" && back.BoardShrine == "TRLA_115", string.Join(",", back.ShrineUpgrades));
    var ser = new System.Xml.Serialization.XmlSerializer(typeof(DungeonRunSave));
    var plain = new DungeonRunSave { Profile = new DungeonRunProfile() };
    plain.Profile.Archive.Add(new DungeonRunArchiveEntry { Class = "MAGE", Seed = "1" });
    using var w = new StringWriter(); ser.Serialize(w, plain);
    Check("H1 an archive entry with no shrine writes no ShrineUpgrades element (the body is unchanged)", !w.ToString().Contains("ShrineUpgrades") && !w.ToString().Contains("BoardShrine"), "");
    string ui = File.ReadAllText(VerifyPaths.Repo("plugin/NativeDungeonRunUI.cs"));
    Check("H1 the hub, the summary and the Treasures view read the shrine line / entries (NativeDungeonRunUI)",
        System.Text.RegularExpressions.Regex.Matches(ui, @"string shrineLine = ShrineLine\(state\);").Count == 2 && ui.Contains("all.AddRange(ShrineEntries(state));"), "");
});

Console.WriteLine($"SearchTelemetry swallowed leaf exceptions {SearchTelemetry.SwallowedLeafErrors}");
foreach (var kv in SearchTelemetry.Details()) Console.WriteLine($"  swallowed {kv.Value} x {kv.Key}");
Console.WriteLine($"RESULT {passes} passed, {failures} failed");
return failures;
