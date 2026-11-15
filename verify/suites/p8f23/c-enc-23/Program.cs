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
using SabberStoneCore.Tasks.SimpleTasks;

// Phase 8 family 23, the Monster Hunt nemeses and Hagatha (real engine games through the run setup path: DungeonRun.BuildPlan on
// the shipped row -> DungeonRunSetup.Configure / ApplyAfterStart): Infinite Toki (Chronoacceleration), Glinda Crowskin (False
// Heart, Crowskin Faithful, Crowskin Pact), Lord Godfrey (Pistol Barrage, Hunter's Insight), Captain Shivers (Kingsbane at the
// start, Plunder auto-cast, Cursed Crewmember), Witchwood's Touch (1), (2), (3), Clockwork Assistant for a boss, Hagatha's
// shipped row with the run player's fight-only hero cards and the run-owned hero switch, the rejected Alternate Timeline, and
// the rows, admission rows and validator.
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
Card C(string id) => Cards.FromId(id);
var facts = System.Text.Json.JsonDocument.Parse(File.ReadAllText(VerifyPaths.Repo("facts/violet-hold-2026-09.json")));
var factById = new Dictionary<string, System.Text.Json.JsonElement>(StringComparer.Ordinal);
foreach (var c in facts.RootElement.GetProperty("cards").EnumerateArray()) factById[c.GetProperty("id").GetString()] = c;
string root = VerifyPaths.Data;
string R(string f) => File.ReadAllText(Path.Combine(root, f));
DungeonRunData Load() => DungeonRunData.Parse(R("dungeon-run/run-v1.json"), R("dungeon-run/pool-v1.json"), R("npc/decks-v1.json"), R("dungeon-run/catalog-v1.json"),
    R("dungeon-run/schedule-v1.json"), R("dungeon-run/tavern-v1.json"), R("dungeon-run/encounters-v1.json"));
var data = Load();
var run = new DungeonRun(data);
var all = new DungeonRunProfile(); all.Unlocked.AddRange(data.Unlocks.Keys);

const string Yeti = "CS2_182", Croc = "CS2_120", Raptor = "CS2_172", Moonfire = "CS2_008", Fireball = "CS2_029", Pyroblast = "EX1_279", Intellect = "CS2_023";
const string Hoarder = "EX1_096", Engineer = "EX1_015", Shrine = "TRLA_154", Axe = "CS2_106", Ooze = "EX1_066", Geomancer = "CS2_142", Arcane = "CS2_025";
string[] Thirty(string id) => Enumerable.Repeat(id, 30).ToArray();

// A run on acts-v3h at its first pre-match, with the boss under test on that floor (as the encounter bosspilot harness does).
DungeonRunState At(string cls, long seed, string bossId)
{
    var c = data.Class(cls);
    var s = run.NewRun(all, c.Id, c.Decks[0], c.Powers[0], seed, schedule: "acts-v3h");
    while (s.Phase != DungeonRunPhase.PreMatch)
    {
        if (s.Phase != DungeonRunPhase.Modifier) throw new Exception("walk stopped at " + s.Phase);
        run.PickModifier(s, s.ModifierOffer.Kind == DungeonRunGlobalRule.KindAnomaly ? DungeonRunGlobalRule.None : s.ModifierOffer.Offered[0]);
    }
    s.BossSequence[s.Floor - 1] = bossId;
    return s;
}
RunMatchPlan RowPlan(string bossId) => run.BuildPlan(At("MAGE", 4400, bossId), all);
Game Ready(RunMatchPlan plan, int seed = 7)
{
    var cfg = new GameConfig { RandomSeed = seed, Shuffle = true, SkipMulligan = false, History = true, Logging = false, FormatType = FormatType.FT_WILD };
    DungeonRunSetup.Configure(cfg, plan);
    var g = new Game(cfg);
    g.StartGame();
    DungeonRunSetup.ApplyAfterStart(g, plan);
    g.Process(ChooseTask.Mulligan(g.Player1, new List<int>()));
    g.Process(ChooseTask.Mulligan(g.Player2, new List<int>()));
    g.MainBegin();
    if (g.CurrentPlayer != g.Player1) throw new Exception("run player not first");
    return g;
}
// The shipped row's plan through the run layer; the decks are replaced by plain cards so a test controls what is drawn (the
// hero, power, Health, forms and every other encounter field stay the row's).
Game Fight(string bossId, string bossDeck = Yeti, string playerDeck = Yeti, string shrine = null, int seed = 7)
{
    var plan = RowPlan(bossId);
    plan.BossDeck = Thirty(bossDeck).Select(C).ToList();
    plan.PlayerDeck = Thirty(playerDeck).Select(C).ToList();
    plan.PlayerDeckSlots = null;
    if (shrine != null) plan.PlayerShrine = shrine;
    return Ready(plan, seed);
}
void Mana(Controller c, int n = 10) { c.BaseMana = n; c.UsedMana = 0; c.OverloadLocked = 0; c.OverloadOwed = 0; }
IPlayable ToHand(Game g, Controller c, string id)
{
    while (c.HandZone.Count >= 9) { var old = c.HandZone[0]; c.HandZone.Remove(old); c.SetasideZone.Add(old); }
    var e = Entity.FromCard(c, C(id)); Generic.AddHandPhase.Invoke(c, e); g.DeathProcessingAndAuraUpdate(); return e;
}
void FillHand(Game g, Controller c) { while (!c.HandZone.IsFull) Generic.AddHandPhase.Invoke(c, Entity.FromCard(c, C(Yeti))); }
void EmptyDeck(Controller c) { foreach (var p in c.DeckZone.ToArray()) c.SetasideZone.Add(c.DeckZone.Remove(p)); }
Minion Board(Game g, Controller c, string id) { var m = (Minion)Entity.FromCard(c, C(id)); Generic.SummonBlock(g, m, -1, c.Hero); g.DeathProcessingAndAuraUpdate(); return m; }
bool Play(Game g, IPlayable card, ICharacter target = null) { Mana(g.CurrentPlayer); return g.Process(PlayCardTask.Any(g.CurrentPlayer, card, target, -1, 0)); }
bool Cast(Game g, string id, ICharacter target = null) => Play(g, ToHand(g, g.CurrentPlayer, id), target);
void EndTurn(Game g) => g.Process(EndTurnTask.Any(g.CurrentPlayer));
bool Offered(Game g) => g.CurrentPlayer.Options().Any(o => o is HeroPowerTask);
List<ICharacter> PowerTargets(Game g) => g.CurrentPlayer.Options().OfType<HeroPowerTask>().Select(o => o.Target).Where(t => t != null).ToList();
bool Power(Game g, ICharacter target = null) => Offered(g) && g.Process(HeroPowerTask.Any(g.CurrentPlayer, target));
void ToBoss(Game g) { if (g.CurrentPlayer == g.Player1) EndTurn(g); Mana(g.Player2); }
void ToPlayer(Game g) { if (g.CurrentPlayer == g.Player2) EndTurn(g); Mana(g.Player1); }
void Round(Game g) { EndTurn(g); EndTurn(g); Mana(g.CurrentPlayer); }
void Kill(Game g, Minion m) { new DestroyTask(EntityType.TARGET).Process(g, m.Controller, m.Controller.Hero, m, null); g.DeathProcessingAndAuraUpdate(); }
int Count(Controller c, string id) => c.BoardZone.Count(m => m.Card.Id == id);
bool Over(Game g) => g.State == State.COMPLETE;
string Hero(Controller c) => c.Hero.Card.Id + "/" + c.Hero.HeroPower.Card.Id + " " + c.Hero.Health + "hp";
(Game view, Controller boss, Controller player) FairView(Game g)
{
    var bot = FairBotState.Create(g, g.Player2.PlayerId, 1, DeckStyle.Tempo);
    var view = bot.FingerprintGame;
    var viewBoss = view.Player1.PlayerId == g.Player2.PlayerId ? view.Player1 : view.Player2;
    return (view, viewBoss, viewBoss.Opponent);
}

bool InOrder(Func<bool> f) => f();
int Cost(string id) => C(id).Cost;
bool Has(IPlayable p, string enchantmentId) => p.AppliedEnchantments?.Any(e => e.Card.Id == enchantmentId) == true;
void ToDeck(Controller c, string id) => Generic.ShuffleIntoDeck(c, c.Hero, Entity.FromCard(c, C(id)));
void EmptyHand(Controller c) { foreach (var p in c.HandZone.ToArray()) { c.HandZone.Remove(p); c.SetasideZone.Add(p); } }
const string Toki = "gila_48_infinite_toki", Glinda = "gila_49_glinda", Godfrey = "gila_59_godfrey", Shivers = "gila_60_shivers", Hagatha = "gila_61_hagatha";
const string Faithful = "GILA_BOSS_49t", Pact = "GILA_BOSS_49t2", Crewmember = "GILA_BOSS_60t", Kingsbane = "LOOT_542", Insight = "GILA_503", Assistant = "GILA_907";
const string Touch1 = "GILA_BOSS_99t", Touch2 = "GILA_BOSS_99t2", Touch3 = "GILA_BOSS_99t3", Timeline = "GILA_BOSS_48t", Accelerated = "GILA_BOSS_48e";
string Identity(DungeonRunState s) => string.Join("|", s.Class, s.StarterDeck, s.Power, s.PowerOverride ?? "-", string.Join(",", s.Deck),
    string.Join(",", s.Cards.Select(c => c.Uid + ":" + c.Id)), string.Join(",", s.Treasures), string.Join(",", s.ActiveTreasures), s.BoardShrine ?? "-");
void Answer(Game g, Controller c, Func<IPlayable, bool> pick)
{
    var decision = g.GetPendingDecisions().Single(d => d.ControllerId == c.Id);
    int id = c.Choice.Choices.First(x => pick(g.IdEntityDic[x]));
    if (!g.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(decision.Id, decision.ControllerId, new[] { id }), out string why)) throw new Exception("choice refused: " + why);
}

// ============================================================ A  Infinite Toki: Chronoacceleration
Guard("Toki", () =>
{
    var g = Fight(Toki);
    Controller boss = g.Player2, player = g.Player1;
    var plan = RowPlan(Toki);
    Check("A1 Infinite Toki's plan: her hero, the clickable Chronoacceleration (2), the act curve's Health plus 10 (not the printed 60)",
        boss.Hero.Card.Id == "GILA_BOSS_48h" && boss.Hero.HeroPower.Card.Id == "GILA_BOSS_48p" && !boss.Hero.HeroPower.IsPassiveHeroPower && Cost("GILA_BOSS_48p") == 2
        && plan.BossHealth == RowPlan("gila_34_havnixx").BossHealth + 10 && plan.BossHealth != 60 && boss.Hero.Health == plan.BossHealth, Hero(boss) + " plan " + plan.BossHealth);
    ToBoss(g);
    bool none = !Offered(g);
    var mine = Board(g, boss, Yeti); var theirs = Board(g, player, Yeti);
    var targets = PowerTargets(g);
    Check("A2 with no minion of her own it is not offered; with one, its only target is her minion (never the run player's)", none && targets.Count == 1 && targets[0] == mine);
    mine.IsExhausted = false;
    bool first = g.Process(MinionAttackTask.Any(boss, mine, player.Hero));
    int healthAfterOne = player.Hero.Health;
    bool used = Power(g, mine);
    Check("A3 used on a minion that attacked already this turn: it gains Accelerated (Mega-Windfury), costs 2 Mana and may attack again",
        first && used && Has(mine, Accelerated) && mine[GameTag.MEGA_WINDFURY] == 1 && boss.UsedMana == 2 && !mine.IsExhausted);
    int attacks = 1;
    while (attacks < 6 && g.Process(MinionAttackTask.Any(boss, mine, player.Hero))) attacks++;
    Check("A4 it attacks four times in all that turn (Mega-Windfury), not a fifth", attacks == 4 && player.Hero.Health == healthAfterOne - 12, attacks + " attacks, hero " + player.Hero.Health);
    EndTurn(g);
    Check("A5 at the end of her turn the minion dies; the run player's minion is untouched", mine.Zone != boss.BoardZone && theirs.Zone == player.BoardZone && boss.BoardZone.Count == 0);
    // A fresh minion: Mega-Windfury it cannot use yet; still dies at the end of the turn.
    ToBoss(g);
    var fresh = Board(g, boss, Yeti);
    Power(g, fresh);
    bool cannot = !g.Process(MinionAttackTask.Any(boss, fresh, player.Hero));
    EndTurn(g);
    Check("A6 on a minion played this turn (no Charge) it gives nothing usable and the minion dies at the end of the turn", cannot && fresh.Zone != boss.BoardZone);
    // The fair view: the boards are public; the power resolves there.
    var g2 = Fight(Toki);
    ToBoss(g2);
    var m2 = Board(g2, g2.Player2, Yeti);
    var (view, viewBoss, viewPlayer) = FairView(g2);
    Mana(viewBoss);
    var vm = viewBoss.BoardZone.First();
    string viewPlay = "ok";
    try { if (!view.Process(HeroPowerTask.Any(viewBoss, vm))) viewPlay = "refused"; } catch (Exception e) { viewPlay = e.GetType().Name + ": " + e.Message; }
    Check("A7 the power resolves in the boss's fair view (the boards are public): the minion gains Accelerated there", viewPlay == "ok" && Has(vm, Accelerated), viewPlay);
    var deck = data.BossDecks[data.Boss(Toki).Deck];
    string Subs(string id) => string.Join(",", data.Boss(id).Source.Substitutions.Select(x => (x.Listed ?? "-") + ">" + (x.Used ?? "-") + ":" + x.Reason));
    Check("A8 her deck: the wiki's 30 without Alternate Timeline (rejected, phase 9) and Enhance-a-matic (a Secret generator not lifted for a boss), filled back to 30 by second copies; two Witchwood's Touch (3)",
        deck.Count == 30 && !deck.Contains(Timeline) && !deck.Contains("GILA_903") && deck.Count(id => id == Touch3) == 2 && deck.Count(id => id == Assistant) == 2
        && Subs(Toki) == "GILA_903>-:admission:secret-generated,GILA_BOSS_48t>-:unscripted,->GILA_907:fill:second-copy,->EX1_287:fill:second-copy"
        && data.Boss(Toki).Source.Kind == DungeonRunDeckSource.KindSubstituted, Subs(Toki));
    var rejected = data.RejectedEncounters.FirstOrDefault(r => r.Id == Timeline);
    var timelineRule = DungeonRunAdmission.Classify(C(Timeline), Timeline, AdmitScope.Boss, null, catalogRun: true);
    Check("A9 Alternate Timeline is rejected with its reason and lift (a run-owned game restart, phase 9): the encounter file's rejection and an admission row that admits no scope",
        rejected != null && rejected.Reason.Contains("restart") && rejected.Lift.StartsWith("phase 9") && !C(Timeline).Implemented && !DungeonRunEncounters.IsScripted(Timeline)
        && !timelineRule.Admitted && DungeonRunAdmission.RuleFor(Timeline)?.Admit == AdmitScope.None && DungeonRunAdmission.RuleFor(Timeline)?.Lift == DungeonRunAdmission.TurnRestartLift);
});

// ============================================================ B  Glinda Crowskin: False Heart, Crowskin Faithful, Crowskin Pact
Guard("Glinda", () =>
{
    var g = Fight(Glinda, shrine: Shrine);
    Controller boss = g.Player2, player = g.Player1;
    var power = boss.Hero.HeroPower;
    Check("B1 False Heart is passive: started at match start, never offered", power.Card.Id == "GILA_BOSS_49p" && DungeonRunEncounters.PassivePowers.Contains("GILA_BOSS_49p")
        && power[EncounterRules.PowerActive] == 1 && !player.Options().Any(o => o is HeroPowerTask && o.Controller == boss));
    bool bareAtStart = !boss.Hero.IsImmune;
    int before = boss.Hero.Health;
    Cast(g, Moonfire, boss.Hero);
    Check("B2 with no minion of hers her hero is not Immune: Moonfire hits it", bareAtStart && boss.Hero.Health == before - 1);
    var cultist = Board(g, boss, Yeti);
    bool immune = boss.Hero.IsImmune;
    Cast(g, Fireball, boss.Hero);
    Check("B3 with a minion of hers her hero is Immune: Fireball does nothing to it", immune && boss.Hero.Health == before - 1);
    Kill(g, cultist);
    Check("B4 when her last minion dies she loses it at the aura update", !boss.Hero.IsImmune);
    var (view, viewBoss, viewPlayer) = FairView(g);
    Board(view, viewBoss, Yeti);
    Check("B5 the aura is live in the boss's fair view too (a public board fact)", viewBoss.Hero.IsImmune);
    // The run player's dormant shrine does not count for its owner; a minion of the run player gives Glinda nothing.
    Board(g, player, Yeti);
    Check("B6 a minion of the run player's does not make Glinda Immune", !boss.Hero.IsImmune);

    // Crowskin Faithful: Deathrattle: draw a card, shuffle a new Faithful into its controller's deck.
    var g2 = Fight(Glinda);
    boss = g2.Player2; player = g2.Player1;
    ToBoss(g2);
    var faithful = ToHand(g2, boss, Faithful);
    Check("B7 Crowskin Faithful is a 3-Mana 6/3 with the client's text", faithful.Cost == 3 && ((Minion)faithful).AttackDamage == 6 && ((Minion)faithful).BaseHealth == 3);
    Play(g2, faithful);
    int hand = boss.HandZone.Count, deck = boss.DeckZone.Count;
    Kill(g2, (Minion)faithful);
    Check("B8 when it dies its controller draws a card and a new Faithful is shuffled into that deck", boss.HandZone.Count == hand + 1 && boss.DeckZone.Count == deck
        && boss.DeckZone.Count(p => p.Card.Id == Faithful) == 1 && boss.DeckZone.First(p => p.Card.Id == Faithful) != faithful);
    // Crowskin Pact on an enemy minion, on her own, with a full board, on a run shrine.
    var yeti = Board(g2, player, Yeti);
    var pact = ToHand(g2, boss, Pact);
    bool played = Play(g2, pact, yeti);
    var taken = boss.BoardZone.LastOrDefault();
    Check("B9 Crowskin Pact (5) transforms an enemy minion into a Faithful and takes control of it", Cost(Pact) == 5 && played && taken?.Card.Id == Faithful && taken.Controller == boss
        && player.BoardZone.Count == 0 && yeti.Zone != player.BoardZone);
    var own = Board(g2, boss, Raptor);
    Play(g2, ToHand(g2, boss, Pact), own);
    Check("B10 on one of her own minions: it becomes a Faithful where it stood, still hers", boss.BoardZone.Count(m => m.Card.Id == Faithful) == 2 && boss.BoardZone.All(m => m.Card.Id == Faithful));
    while (!boss.BoardZone.IsFull) Board(g2, boss, Raptor);
    var victim = Board(g2, player, Croc);
    Play(g2, ToHand(g2, boss, Pact), victim);
    Check("B11 with her board full the transformed minion is destroyed by the control rule (it is never on two boards)", player.BoardZone.Count == 0 && boss.BoardZone.Count == 7
        && boss.BoardZone.Count(m => m.Card.Id == Faithful) == 2);
    var g3 = Fight(Glinda, shrine: Shrine);
    boss = g3.Player2; player = g3.Player1;
    var shrine = ShrineRules.Of(player).First();
    ToBoss(g3);
    Play(g3, ToHand(g3, boss, Pact), shrine);
    var form = ShrineRules.Of(player).FirstOrDefault();
    Check("B12 aimed at the run player's board shrine: the shrine is destroyed instead of transformed and goes dormant; it never changes sides, no Faithful appears",
        ShrineRules.IsCorpse(shrine) && form != null && form.IsDormant && form.Controller == player && boss.BoardZone.Count == 0 && player.BoardZone.All(m => m.Card.Id != Faithful));
    Check("B13 the row carries the flag heroImmune, which keeps Anomaly ULDA_706 No-Face out of her fight", data.Boss(Glinda).HasFlag("heroImmune") && data.Boss(Glinda).HasFlag("passive")
        && data.Schedules.Rules.First(r => r.Id == "ULDA_706").IncompatibleFlags.Contains("heroImmune"));
});

// ============================================================ C  Lord Godfrey: Pistol Barrage, Hunter's Insight
Guard("Godfrey", () =>
{
    var g = Fight(Godfrey);
    Controller boss = g.Player2, player = g.Player1;
    var a = Board(g, player, Yeti); var b = Board(g, player, Yeti); var c = Board(g, player, Yeti); var d = Board(g, player, Yeti);
    var geo = Board(g, boss, Geomancer);
    ToBoss(g);
    var targets = PowerTargets(g);
    bool used = Power(g, b);
    Check("C1 Pistol Barrage costs (1) and may target any minion", Cost("GILA_BOSS_59p") == 1 && targets.Contains(a) && targets.Contains(geo) && used && boss.UsedMana == 1);
    Check("C2 2 damage to the minion and the minions next to it, none further; a hero power, so the boss's Spell Damage +1 adds nothing",
        a.Damage == 2 && b.Damage == 2 && c.Damage == 2 && d.Damage == 0 && player.Hero.Damage == 0, a.Damage + "," + b.Damage + "," + c.Damage + "," + d.Damage);
    Round(g);
    Power(g, a);
    Check("C3 at the edge of the board it hits the minion and its one neighbour", a.Damage == 4 && b.Damage == 4 && c.Damage == 2 && d.Damage == 0, a.Damage + "," + b.Damage + "," + c.Damage + "," + d.Damage);
    // Hunter's Insight in the boss's hand: its own hand costs (3) less.
    var g2 = Fight(Godfrey);
    boss = g2.Player2;
    ToBoss(g2);
    EmptyHand(boss);
    var big = ToHand(g2, boss, Pyroblast); var yeti = ToHand(g2, boss, Yeti);
    var insight = ToHand(g2, boss, Insight);
    bool cast = Play(g2, insight);
    Check("C4 Hunter's Insight played by the boss (Lord Godfrey's deck card): the cards in its own hand cost (3) less", cast && big.Cost == 7 && yeti.Cost == 1 && g2.Player1.HandZone.All(p => p.Cost == p.Card.Cost));
    // The row's start block (the wiki's Final Nemesis page): Godfrey holds Hunter's Insight after the mulligan; his deck is the table's other 29.
    var gplan = RowPlan(Godfrey);
    var g3 = Ready(gplan, 13);
    var gdeck = data.BossDecks[data.Boss(Godfrey).Deck];
    Check("C6 Lord Godfrey starts the game with Hunter's Insight in hand (one copy, after the mulligan); his deck holds the wiki table's other 29 cards, none a Hunter's Insight",
        gplan.Start?.BossHandAdd.Select(x => x.Id).SequenceEqual(new[] { Insight }) == true && g3.Player2.HandZone.Count(p => p.Card.Id == Insight) == 1
        && g3.Player2.DeckZone.All(p => p.Card.Id != Insight) && gdeck.Count == 29 && !gdeck.Contains(Insight) && g3.Player1.HandZone.All(p => p.Card.Id != Insight)
        && data.Boss(Godfrey).HasFlag("start") && data.Boss(Godfrey).Source.Kind == DungeonRunDeckSource.KindAuthentic,
        "hand " + string.Join(",", g3.Player2.HandZone.Select(p => p.Card.Id)) + " deck " + gdeck.Count);
    var verdictBoss = DungeonRunAdmission.Classify(C(Insight), Insight, AdmitScope.Boss, null, catalogRun: true);
    var verdictLegacy = DungeonRunAdmission.Classify(C(Insight), Insight, AdmitScope.Boss);
    Check("C5 Hunter's Insight and Clockwork Assistant: admitted for a boss on a catalog run (a nemesis deck lists them), the run player's only on a legacy run",
        verdictBoss.Admitted && !verdictLegacy.Admitted && DungeonRunAdmission.Classify(C(Assistant), Assistant, AdmitScope.Boss, null, catalogRun: true).Admitted
        && DungeonRunAdmission.Classify(C(Insight), Insight, AdmitScope.Player).Admitted && DungeonRunAdmission.RuleFor(Insight).Family == "run-treasure-nemesis-deck"
        && DungeonRunAdmission.RuleFor(Assistant) == DungeonRunAdmission.RuleFor(Insight));
});

// ============================================================ D  Captain Shivers: Kingsbane, Plunder, Cursed Crewmember
Guard("Shivers", () =>
{
    var plan = RowPlan(Shivers);
    var g = Fight(Shivers);
    Controller boss = g.Player2, player = g.Player1;
    var power = boss.Hero.HeroPower;
    Check("D1 Captain Shivers starts with Kingsbane equipped (the row's start block); the run player has no weapon", plan.Start.BossWeapon?.Id == Kingsbane && boss.Hero.Weapon?.Card.Id == Kingsbane
        && player.Hero.Weapon == null && plan.BossHealth == RowPlan("gila_34_havnixx").BossHealth + 10);
    Check("D2 Plunder is cast by rule: never offered as an option, its trigger started at match start", DungeonRunEncounters.PassivePowers.Contains("GILA_BOSS_60p")
        && power[EncounterRules.PowerActive] == 1 && Cost("GILA_BOSS_60p") == 0);
    ToDeck(boss, Axe);
    int uses = DungeonRunEncounters.AutoCasts(power);
    ToBoss(g);
    Check("D3 at the start of his turn with a weapon in his deck, the weapon is drawn (one use)", DungeonRunEncounters.AutoCasts(power) == uses + 1 && boss.HandZone.Any(p => p.Card.Id == Axe)
        && boss.DeckZone.All(p => !(p is Weapon)));
    int used = DungeonRunEncounters.AutoCasts(power);
    Round(g);
    Check("D4 with no weapon left in the deck it is not cast", DungeonRunEncounters.AutoCasts(power) == used);
    // Kingsbane's cycle: destroyed by the run player's Acidic Swamp Ooze, shuffled into his deck, drawn back by Plunder.
    ToPlayer(g);
    Play(g, ToHand(g, player, Ooze));
    bool shuffled = boss.Hero.Weapon == null && boss.DeckZone.Any(p => p.Card.Id == Kingsbane);
    ToBoss(g);
    Check("D5 Kingsbane's Deathrattle shuffles it into his deck and Plunder draws it back at his next turn start", shuffled && boss.HandZone.Any(p => p.Card.Id == Kingsbane)
        && DungeonRunEncounters.AutoCasts(power) == used + 1);
    // A full hand: not cast (the weapon would burn).
    ToPlayer(g);
    ToDeck(boss, Axe);
    FillHand(g, boss);
    int before = DungeonRunEncounters.AutoCasts(power);
    ToBoss(g);
    Check("D6 with his hand full it is not cast", DungeonRunEncounters.AutoCasts(power) == before);
    // Cursed Crewmember.
    var g2 = Fight(Shivers);
    boss = g2.Player2;
    ToBoss(g2);
    EmptyHand(boss);
    ToHand(g2, boss, Fireball); ToHand(g2, boss, Raptor);
    var crew = ToHand(g2, boss, Crewmember);
    int deck = boss.DeckZone.Count;
    bool played = Play(g2, crew);
    var handIds = boss.HandZone.Select(p => p.Card.Id).OrderBy(x => x).ToList();
    var added = boss.DeckZone.Where(p => p.Card.Id != Yeti).Select(p => p.Card.Id).OrderBy(x => x).ToList();
    Check("D7 Cursed Crewmember (2 Mana 2/1 Pirate): draws 2 cards, then a copy of each card in its caster's hand is shuffled into that deck",
        played && Cost(Crewmember) == 2 && ((Minion)crew).AttackDamage == 2 && boss.HandZone.Count == 4 && boss.DeckZone.Count == deck - 2 + 4
        && added.SequenceEqual(new[] { Fireball, Raptor }.OrderBy(x => x)) && boss.DeckZone.Count(p => p.Card.Id == Yeti) == deck - 2 + 2,
        "hand " + string.Join(",", handIds) + " deck " + boss.DeckZone.Count + " added " + string.Join(",", added));
    // The run player casting it: the copies go into the run player's deck; the boss's fair view does not learn which cards they are.
    var g3 = Fight(Shivers);
    var p3 = g3.Player1;
    EmptyHand(p3);
    ToHand(g3, p3, Pyroblast); ToHand(g3, p3, Fireball);
    var crew3 = ToHand(g3, p3, Crewmember);
    Play(g3, crew3);
    var (view, viewBoss, viewPlayer) = FairView(g3);
    int knownCopies = viewPlayer.DeckZone.Count(p => p.Card.Id == Pyroblast);
    Check("D8 cast by the run player: its copies join the run player's deck; the boss's fair view of that deck does not name them", p3.DeckZone.Count(p => p.Card.Id == Pyroblast) == 1
        && knownCopies == 0 && viewPlayer.DeckZone.Count == p3.DeckZone.Count, "view copies " + knownCopies);
    string Subs(string id) => string.Join(",", data.Boss(id).Source.Substitutions.Select(x => (x.Listed ?? "-") + ">" + (x.Used ?? "-") + ":" + x.Reason));
    var sdeck = data.BossDecks[data.Boss(Shivers).Deck];
    Check("D9 his deck: the wiki's 30 less the unscripted Fel Reaver (twice), filled by second copies; two Cursed Crewmembers; no weapon listed (Kingsbane starts equipped)",
        sdeck.Count == 30 && sdeck.Count(id => id == Crewmember) == 2 && !sdeck.Contains("GVG_016") && sdeck.All(id => C(id).Type != CardType.WEAPON)
        && Subs(Shivers) == "GVG_016>-:unscripted,GVG_016>-:unscripted,->ICC_221:fill:second-copy,->UNG_823:fill:second-copy", Subs(Shivers));
});

// ============================================================ E  Witchwood's Touch (1), (2), (3) and Clockwork Assistant
Guard("Touch", () =>
{
    foreach (var (id, draw, armor) in new[] { (Touch1, 1, 6), (Touch2, 2, 8), (Touch3, 3, 10) })
    {
        var g = Fight(Glinda);
        var boss = g.Player2;
        ToBoss(g);
        EmptyHand(boss);
        int deck = boss.DeckZone.Count, armorBefore = boss.Hero.Armor;
        var touch = ToHand(g, boss, id);
        bool played = Play(g, touch);
        Check("E1 " + id + " Witchwood's Touch costs (2): draws " + draw + " and gains " + armor + " Armor, its client text", played && touch.Cost == 2 && boss.HandZone.Count == draw
            && boss.DeckZone.Count == deck - draw && boss.Hero.Armor == armorBefore + armor && C(id).Text == factById[id].GetProperty("text").GetString());
        var g2 = Fight(Glinda);
        var p = g2.Player1;
        EmptyHand(p);
        int pd = p.DeckZone.Count, pa = p.Hero.Armor;
        Play(g2, ToHand(g2, p, id));
        Check("E2 " + id + " cast by the run player (taken from a boss): the same for its caster", p.HandZone.Count == draw && p.DeckZone.Count == pd - draw && p.Hero.Armor == pa + armor);
    }
    int In(string boss, string card) => data.BossDecks[data.Boss(boss).Deck].Count(id => id == card);
    Check("E3 where the wiki lists them: two (3) in Glinda's and Infinite Toki's decks; two (2) in Inquisitor Hav'nixx's and Experiment 3C's (family 10, now the card itself, no Shield Block); (1) in none",
        In(Glinda, Touch3) == 2 && In(Toki, Touch3) == 2 && In("gila_34_havnixx", Touch2) == 2 && In("gila_27_experiment_3c", Touch2) >= 1 && In("gila_34_havnixx", "EX1_606") == 0
        && data.BossDecks.Values.All(d => !d.Contains(Touch1)) && data.Boss("gila_34_havnixx").Source.Kind == DungeonRunDeckSource.KindAuthentic);
    // Clockwork Assistant for a boss: +1/+1 for each spell its owner has cast this game.
    var g3 = Fight(Toki);
    var b3 = g3.Player2;
    ToBoss(g3);
    Cast(g3, Arcane); Cast(g3, Arcane);
    var assistant = (Minion)ToHand(g3, b3, Assistant);
    Play(g3, assistant);
    int atk = assistant.AttackDamage, hp = assistant.Health;
    Cast(g3, Arcane);
    Check("E4 Clockwork Assistant played by the boss (Infinite Toki's deck card): +1/+1 for each spell she cast this game (2, then 3)", atk == 3 && hp == 3 && assistant.AttackDamage == 4 && assistant.Health == 4,
        atk + "/" + hp + " then " + assistant.AttackDamage + "/" + assistant.Health);
});

// ============================================================ G  Hagatha: the shipped row, the hero cards and the run-owned hero switch
Guard("Hagatha", () =>
{
    var row = data.Boss(Hagatha);
    Check("G1 Hagatha is family 23's act II major (family 01 scripted her): the passive Bewitch, 100 fixed Health, 2 starting Mana Crystals, the wiki's 40 cards unchanged",
        row.Family == "23" && row.Tier == "major2" && row.Power == "GIL_504h" && row.FixedHealth && row.Health == 100 && row.Start?.BossMana == 2
        && data.BossDecks[row.Deck].Count == 40 && row.Source.Kind == DungeonRunDeckSource.KindAuthentic && row.HasFlag("heroSwitch"));
    var s = At("MAGE", 4410, Hagatha);
    var profile = new DungeonRunProfile(); profile.Unlocked.AddRange(data.Unlocks.Keys);
    var plan = run.BuildPlan(s, profile);
    string before = Identity(s);
    var deckBefore = s.Deck.ToList();
    Check("G2 a Mage run is given Darius's and Tess's hero cards with their 12 wiki cards and three passives each; Toki's card (GILA_900h2, Temporal Loop) is not given",
        plan.PlayerHeroCards.Select(h => h.Hero.Id).SequenceEqual(new[] { "GILA_600h2", "GILA_500h4" }) && plan.PlayerHeroCards.All(h => h.Deck.Count == 12 && h.Passives.Count == 3)
        && plan.PlayerHeroCards.All(h => h.Hero.Id != "GILA_900h2") && DungeonRunAdmission.RuleFor("GILA_900h2")?.Admit == AdmitScope.None);
    run.BeginMatch(s);
    Check("G3 BeginMatch records the switch as run-owned state of this floor (hunters, two options), the run's own records untouched",
        s.HeroSwitch != null && s.HeroSwitch.Kind == DungeonRunHeroSwitch.KindHunters && s.HeroSwitch.Floor == s.Floor && s.HeroSwitch.Options.Select(o => o.Hero).SequenceEqual(new[] { "GILA_600h2", "GILA_500h4" })
        && Identity(s) == before);
    var g = Ready(plan, 11);
    var p = g.Player1;
    var tess = p.HandZone.FirstOrDefault(c => c.Card.Id == "GILA_500h4");
    var golem = Board(g, g.Player2, "CS2_186");
    Mana(p);
    int deck = p.DeckZone.Count;
    bool played = Play(g, tess);
    Answer(g, p, e => e.Card.Id == "GILA_511");
    var tessCards = row.HeroCards.First(h => h.Hero == "GILA_500h4").Deck;
    var added = p.DeckZone.Select(c => c.Card.Id).Where(id => tessCards.Contains(id)).OrderBy(x => x).ToList();
    Check("G4 Tess's card played in the real fight: hero and power change for this match, a passive is chosen, 8 damage to all minions, her 12 wiki cards join the deck",
        played && p.Hero.Card.Id == "GILA_500h4" && p.Hero.HeroPower.Card.Id == "GILA_500p2" && golem.Zone != g.Player2.BoardZone && p.DeckZone.Count == deck + 12
        && added.SequenceEqual(tessCards.OrderBy(x => x)), p.DeckZone.Count + " vs " + deck);
    // Ritual Dagger and Ominous Fog from the shipped row's game.
    ToBoss(g);
    var boss = g.Player2;
    var dagger = ToHand(g, boss, "GILA_BOSS_61t");
    Play(g, dagger);
    int durability = boss.Hero.Weapon.Durability;
    Cast(g, Moonfire, p.Hero);
    Check("G5 Ritual Dagger (Lifesteal) gains +1 Durability after Hagatha casts a spell", boss.Hero.Weapon.Card.Id == "GILA_BOSS_61t" && boss.Hero.Weapon.Durability == durability + 1 && boss.Hero.Weapon.HasLifeSteal);
    var mineB = Board(g, boss, Yeti); var theirsB = Board(g, p, Yeti);
    Play(g, ToHand(g, boss, "GILA_BOSS_61t3"));
    bool survives = theirsB.Zone == p.BoardZone;
    EndTurn(g); EndTurn(g);
    Check("G6 Ominous Fog corrupts the enemy minions only; they are destroyed at the start of Hagatha's next turn", survives && theirsB.Zone != p.BoardZone && mineB.Zone == boss.BoardZone);
    var end = DungeonRunSetup.Harvest(g);
    run.RecordOutcome(s, profile, DungeonRunOutcome.Loss, g.RunResult, end);
    Check("G7 after the fight the switch is gone; the run's class, hero, power, deck and cards are what they were (Tess's cards never joined the run deck); the summary names the heroes",
        s.HeroSwitch == null && Identity(s) == before && s.Deck.SequenceEqual(deckBefore) && s.Deck.All(id => !tessCards.Contains(id) || deckBefore.Contains(id))
        && s.Matches.Last().HeroesUsed == string.Join(",", end.HeroesUsed) && end.HeroesUsed.Contains("GILA_500h4"), s.Matches.Last().HeroesUsed);
    // A win: the run goes on as itself.
    var s2 = At("MAGE", 4411, Hagatha);
    string before2 = Identity(s2);
    run.BeginMatch(s2);
    run.RecordOutcome(s2, profile, DungeonRunOutcome.Win, null, new RunMatchEnd(0, 0, 0, 3, new[] { "HERO_08", "GILA_600h2" }, false));
    Check("G8 a win as a switched hero: the switch is cleared and the run's class, power and deck are its own", s2.HeroSwitch == null && s2.Class == "MAGE" && s2.PowerOverride == null
        && Identity(s2).Split('|').Take(5).SequenceEqual(before2.Split('|').Take(5)));
});

// ============================================================ H  rows, scripts, admission, validator
Guard("Rows", () =>
{
    var errors = DungeonRunValidator.Validate(data, null);
    Check("H1 the shipped data validates with the family 23 rows", errors.Count == 0, string.Join(" | ", errors.Take(4)));
    var rows = new (string id, string n, string name, int printed, string kind, string[] flags)[]
    {
        (Toki, "48", "Infinite Toki", 60, "substituted", new string[0]),
        (Glinda, "49", "Glinda Crowskin", 50, "authentic", new[] { "heroImmune", "passive" }),
        (Godfrey, "59", "Lord Godfrey", 60, "authentic", new[] { "start" }),
        (Shivers, "60", "Captain Shivers", 75, "substituted", new[] { "passive", "start" }),
    };
    Check("H2 family 23 holds the four nemeses and Hagatha, all act II majors", data.Encounters.Where(b => b.Family == "23").Select(b => b.Id).OrderBy(i => i, StringComparer.Ordinal)
        .SequenceEqual(rows.Select(r => r.id).Append(Hagatha).OrderBy(i => i, StringComparer.Ordinal)) && data.Encounters.Where(b => b.Family == "23").All(b => b.Tier == "major2" && b.Pools.SequenceEqual(new[] { "enc-major2" })),
        string.Join(",", data.Encounters.Where(b => b.Family == "23").Select(b => b.Id)));
    foreach (var r in rows)
    {
        var b = data.Boss(r.id);
        var plan = RowPlan(r.id);
        string hero = "GILA_BOSS_" + r.n + "h", power = "GILA_BOSS_" + r.n + "p";
        Check("H3 " + r.id + ": a Monster Hunt major of printed Health " + r.printed + " whose plan is the row's (hero, power, a " + (r.id == Godfrey ? 29 : 30) + "-card " + r.kind + " deck, the curve's Health plus 10), flags [" + string.Join(",", r.flags) + "]",
            b.Provenance == "GIL" && b.Name == r.name && b.HeroCard == hero && b.Power == power && b.Source.Kind == r.kind && b.Flags.OrderBy(f => f).SequenceEqual(r.flags.OrderBy(f => f))
            && plan.EncounterId == r.id && plan.EncounterDeck && plan.BossHero.Id == hero && plan.BossHeroPowerId == power && plan.BossDeck.Count == (r.id == Godfrey ? 29 : 30) && data.BossDecks[b.Deck].Count == plan.BossDeck.Count
            && !b.FixedHealth && b.Health == null && b.HealthOffset == 10 && C(hero).Health == r.printed && C(hero).Name == r.name && b.HeroCards.Count == 0 && b.Forms.Count == 0
            && data.Schedules.Member(b, "major-2") && b.Source.Url.StartsWith("https://hearthstone.wiki.gg/wiki/") && !b.BossGeneratedSecrets,
            b.Source.Kind + " flags " + string.Join(",", b.Flags) + " offset " + b.HealthOffset);
    }
    string[] powers = { "GILA_BOSS_48p", "GILA_BOSS_49p", "GILA_BOSS_59p", "GILA_BOSS_60p" };
    string[] cards = { Faithful, Pact, Crewmember, Touch1, Touch2, Touch3 };
    var scripted = powers.Concat(cards).Append(Accelerated).ToArray();
    var unscripted = scripted.Where(id => !C(id).Implemented || DungeonRunEncounters.Find(id)?.Family != "23").ToList();
    var retexted = scripted.Where(id => C(id).Text.Replace(" @spelldmg", "") != factById[id].GetProperty("text").GetString()).ToList();
    Check("H4 every power, boss card and enchantment of the family is scripted by family 23 and keeps the client's text (4 powers, 6 cards, 1 enchantment)", unscripted.Count == 0 && retexted.Count == 0,
        string.Join(",", unscripted.Concat(retexted.Select(id => id + " [" + C(id).Text + "]"))));
    var wrongScope = powers.Concat(cards).Where(id =>
    {
        var v = DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss, null, catalogRun: true);
        return !(v.Admitted && v.Subject && v.Rule?.Ids != null && v.Rule.Ids.Contains(id)
            && DungeonRunAdmission.Classify(C(id), id, AdmitScope.Player, null, catalogRun: true).Admitted == cards.Contains(id)
            && !DungeonRunAdmission.Classify(C(id), id, AdmitScope.Generated, null, catalogRun: true).Admitted
            && DungeonRunAdmission.GeneratedRejects(true).Contains(id));
    }).ToList();
    Check("H5 each has its own admission row: the four powers a boss's only, the six boss cards a boss's and (taken from a boss) the run player's; none is ever generated", wrongScope.Count == 0, string.Join(",", wrongScope));
    var families = powers.Concat(cards).Select(id => DungeonRunAdmission.RuleFor(id)?.Family).Distinct().OrderBy(f => f, StringComparer.Ordinal).ToList();
    Check("H6 the admission families: nemesis-board-power (Chronoacceleration, Pistol Barrage), nemesis-passive-immune, nemesis-auto-cast, nemesis-hunt-card (6)",
        families.SequenceEqual(new[] { "nemesis-auto-cast", "nemesis-board-power", "nemesis-hunt-card", "nemesis-passive-immune" }) && DungeonRunAdmission.RuleFor("GILA_BOSS_48p").Ids.Length == 2
        && DungeonRunAdmission.RuleFor(Faithful).Ids.Length == 6, string.Join(",", families));
    var playerPower = DungeonRunAdmission.Classify(C("GILA_BOSS_59p"), "GILA_BOSS_59p", AdmitScope.Player, null, catalogRun: true);
    Check("H7 a family 23 power asked for the run player is refused with its row's lift", !playerPower.Admitted && playerPower.Lift == DungeonRunAdmission.NoneBossPower);
    var enhance = DungeonRunAdmission.Classify(C("GILA_903"), "GILA_903", AdmitScope.Boss, null, catalogRun: true);
    Check("H8 Enhance-a-matic stays the run player's (a Secret generator of every class; not lifted for a boss), so Infinite Toki's deck leaves it out",
        !enhance.Admitted && enhance.Family == "secret-generated" && DungeonRunAdmission.Classify(C("GILA_903"), "GILA_903", AdmitScope.Player, null, catalogRun: true).Admitted);
    DungeonRunData Changed(Action<DungeonRunData> change) { var d = Load(); change(d); return d; }
    var noFlag = DungeonRunValidator.Validate(Changed(d => d.Boss(Shivers).Start.BossWeapon = "CS2_182"), null);
    Check("H9 the validator checks a start block's weapon: a minion as Shivers' start weapon is refused", noFlag.Count > 0, string.Join(" | ", noFlag.Take(2)));
    var timeline = Changed(d => d.BossDecks[d.Boss(Toki).Deck][0] = Timeline);
    var timelineErrors = DungeonRunValidator.Validate(timeline, null);
    Check("H10 the validator refuses Alternate Timeline in a boss deck (not scripted, not admitted)", timelineErrors.Any(e => e.Contains(Timeline)), string.Join(" | ", timelineErrors.Take(2)));
    var mage = At("MAGE", 4420, Glinda);
    Check("H11 a nemesis fight gives no fight-only heroes: no switch, the run's own classes", run.HeroSwitchFor(mage) == null && run.MatchClasses(mage).SequenceEqual(new[] { "MAGE" }));
});

Console.WriteLine($"SearchTelemetry swallowed leaf exceptions {SearchTelemetry.SwallowedLeafErrors}");
Console.WriteLine($"RESULT {passes} passed, {failures + SearchTelemetry.SwallowedLeafErrors} failed");
return failures + SearchTelemetry.SwallowedLeafErrors;
