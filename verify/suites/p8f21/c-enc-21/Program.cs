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

// Phase 8 family 21, Tombs of Terror bosses C (real engine games through the run setup path: DungeonRun.BuildPlan on the shipped
// row -> DungeonRunSetup.Configure / ApplyAfterStart): Alda Petrik (Running Wild), Sandshaper Rhogi (Sand Shapin', Sand Rush),
// Jolene Knottley (Spoil the Ending, with the boss's fair view), Belloc Brightblade (Reliquary's Relics), Wildtooth (Maw of the
// Desert, cast by rule), Ilza Grimdrum (Arena Expert, cast by rule), Totally Normal Jar (What's in the Jar?), Lich Baz'hial (Lich's
// Phylactery breaking into Broken Phylactery: the power swap inside the match, the double fatigue), Craz (Demon Emissary), Ol'
// Barkeye (Desert Warfare), Kasmut (Tol'vir Necromancy), Taskmaster Maru (E.V.I.L. Conscription), Janak (Cavalry of E.V.I.L.),
// Water-Toting Murlocs (Fish Food), Sand Puffer Frog (Sandy Surprise), Winged Guardian (Titan's Beast) and Dire Bat (Food Chain),
// with the shrine cases that touch them; then the rows, the tier rule, Jythiros's rejection, the admission rows and the validator.
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
int FactCost(string id) => factById[id].GetProperty("tags").EnumerateArray().Where(t => t[0].GetInt32() == 48).Select(t => t[1].GetInt32()).FirstOrDefault();
string root = VerifyPaths.Data;
string R(string f) => File.ReadAllText(Path.Combine(root, f));
DungeonRunData Load() => DungeonRunData.Parse(R("dungeon-run/run-v1.json"), R("dungeon-run/pool-v1.json"), R("npc/decks-v1.json"), R("dungeon-run/catalog-v1.json"),
    R("dungeon-run/schedule-v1.json"), R("dungeon-run/tavern-v1.json"), R("dungeon-run/encounters-v1.json"));
var data = Load();
var run = new DungeonRun(data);
var all = new DungeonRunProfile(); all.Unlocked.AddRange(data.Unlocks.Keys);

const string Yeti = "CS2_182", Croc = "CS2_120", Raptor = "CS2_172", Wisp = "CS2_231", Shrine = "TRLA_154";
const string Moonfire = "CS2_008", Fireball = "CS2_029", Frostbolt = "CS2_024", MicroMummy = "ULD_217", MurlocRaider = "CS2_168", Bluegill = "CS2_173";
const string WarAxe = "CS2_106", Voidwalker = "CS2_065";
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
// hero, power, forms, Health and every other encounter field stay the row's).
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
void FillHand(Controller c) { while (!c.HandZone.IsFull) Generic.AddHandPhase.Invoke(c, Entity.FromCard(c, C(Yeti))); }
void EmptyHand(Controller c) { foreach (var p in c.HandZone.ToArray()) { c.HandZone.Remove(p); c.SetasideZone.Add(p); } }
void EmptyDeck(Controller c) { foreach (var p in c.DeckZone.ToArray()) { c.DeckZone.Remove(p); c.SetasideZone.Add(p); } }
Minion Board(Game g, Controller c, string id) { var m = (Minion)Entity.FromCard(c, C(id)); Generic.SummonBlock(g, m, -1, c.Hero); g.DeathProcessingAndAuraUpdate(); return m; }
void FillBoard(Game g, Controller c) { while (!c.BoardZone.IsFull) Board(g, c, Wisp); }
void ClearBoard(Controller c) { foreach (var m in c.BoardZone.ToArray()) { c.BoardZone.Remove(m); c.SetasideZone.Add(m); } }
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
void Hit(Game g, ICharacter m, int n) { new DamageTask(n, EntityType.TARGET).Process(g, m.Controller.Opponent, m.Controller.Opponent.Hero, (IPlayable)m, null); g.DeathProcessingAndAuraUpdate(); }
bool Has(IPlayable p, string enchantmentId) => p.AppliedEnchantments?.Any(e => e.Card.Id == enchantmentId) == true;
bool HeroAttack(Game g, ICharacter target) { g.CurrentPlayer.Hero.IsExhausted = false; return g.Process(HeroAttackTask.Any(g.CurrentPlayer, target)); }
(Game view, Controller boss, Controller player) FairView(Game g)
{
    var bot = FairBotState.Create(g, g.Player2.PlayerId, 1, DeckStyle.Tempo);
    var view = bot.FingerprintGame;
    var viewBoss = view.Player1.PlayerId == g.Player2.PlayerId ? view.Player1 : view.Player2;
    return (view, viewBoss, viewBoss.Opponent);
}
bool Clickable(Game g) => !g.Player2.Hero.HeroPower.IsPassiveHeroPower;
// A passive power (or one cast by rule): never an option, started at match start (its trigger is live).
bool PassiveStarted(Game g, string power) => g.Player2.Hero.HeroPower.Card.Id == power && g.Player2.Hero.HeroPower.IsPassiveHeroPower
    && DungeonRunEncounters.PassivePowers.Contains(power) && g.Player2.Hero.HeroPower[EncounterRules.PowerActive] == 1;
bool SameText(string id) => C(id).Text == factById[id].GetProperty("text").GetString();

// ============================================================ A  Alda Petrik: Running Wild
Guard("Alda", () =>
{
    var g = Fight("ulda_41_alda", bossDeck: Croc);
    Controller boss = g.Player2;
    ToBoss(g);
    EmptyHand(boss);
    int deck = boss.DeckZone.Count;
    Check("A1 Running Wild costs (2), clickable, untargeted", C("ULDA_BOSS_41p").Cost == 2 && Clickable(g) && Offered(g) && PowerTargets(g).Count == 0);
    bool used = Power(g);
    Check("A2 a Beast (River Crocolisk) drawn from the boss's deck: it and a second, new copy are in its hand", used && boss.HandZone.Count == 2 && boss.HandZone.All(p => p.Card.Id == Croc)
        && boss.DeckZone.Count == deck - 1 && boss.UsedMana == 2, boss.HandZone.Count + " in hand, deck " + boss.DeckZone.Count);
    var g2 = Fight("ulda_41_alda", bossDeck: Yeti);
    ToBoss(g2);
    var boss2 = g2.Player2;
    foreach (var p in boss2.DeckZone.Take(10).ToArray()) { boss2.DeckZone.Remove(p); boss2.SetasideZone.Add(p); }
    foreach (var id in new[] { Moonfire, Fireball }) Entity.FromCard(boss2, C(id), zone: boss2.DeckZone);
    EmptyHand(boss2);
    Power(g2);
    Check("A3 a non-Beast minion drawn: one card only, and never a spell of the deck", boss2.HandZone.Count == 1 && boss2.HandZone[0].Card.Id == Yeti, string.Join(",", boss2.HandZone.Select(p => p.Card.Id)));
    var g3 = Fight("ulda_41_alda", bossDeck: Moonfire);
    ToBoss(g3);
    EmptyHand(g3.Player2);
    int spells = g3.Player2.DeckZone.Count;
    Power(g3);
    Check("A4 a deck without minions: nothing is drawn", g3.Player2.HandZone.Count == 0 && g3.Player2.DeckZone.Count == spells);
    Round(g3);
    FillHand(g3.Player2);
    Check("A5 with a full hand it is not offered", !Offered(g3));
    Round(g3);
    EmptyHand(g3.Player2); EmptyDeck(g3.Player2);
    Check("A6 with an empty deck it is not offered", !Offered(g3));
});

// ============================================================ B  Sandshaper Rhogi: Sand Shapin'
Guard("Rhogi", () =>
{
    var g = Fight("ulda_42_rhogi", shrine: Shrine);
    Controller boss = g.Player2, player = g.Player1;
    var croc = Board(g, player, Croc);
    ToBoss(g);
    var own = Board(g, boss, Raptor);
    var shrine = ShrineRules.Of(player).FirstOrDefault();
    var targets = PowerTargets(g);
    Check("B1 Sand Shapin' costs (3); its targets are enemy minions only, never a run shrine or a friendly minion", C("ULDA_BOSS_42p").Cost == 3 && shrine != null
        && targets.Contains(croc) && !targets.Contains(shrine) && !targets.Contains(own) && targets.Count == 1, string.Join(",", targets.Select(t => t.Card.Id)));
    int deck = boss.DeckZone.Count;
    bool used = Power(g, croc);
    var copy = boss.DeckZone.FirstOrDefault(p => p.Card.Id == Croc);
    Check("B2 a plain copy of the minion's card joins the boss's deck, marked Summon when Drawn (ULDA_BOSS_42e); the minion stays", used && copy != null
        && boss.DeckZone.Count == deck + 1 && DungeonRunEncounters.TombsC.SandShaped(copy) && croc.Zone == player.BoardZone && boss.UsedMana == 3);
    EmptyHand(boss);
    int before = boss.BoardZone.Count; deck = boss.DeckZone.Count;
    Generic.Draw(boss, copy);
    g.DeathProcessingAndAuraUpdate();
    Check("B3 drawn, it is summoned for the boss (the mark is gone) and the boss draws a card in its place", copy.Zone == boss.BoardZone && boss.BoardZone.Count == before + 1
        && !DungeonRunEncounters.TombsC.SandShaped(copy) && boss.HandZone.Count == 1 && boss.DeckZone.Count == deck - 2, copy.Zone?.Type + " hand " + boss.HandZone.Count);
    Round(g);
    Power(g, croc);
    var second = boss.DeckZone.FirstOrDefault(p => p.Card.Id == Croc && DungeonRunEncounters.TombsC.SandShaped(p));
    FillBoard(g, boss);
    EmptyHand(boss);
    Generic.Draw(boss, second);
    Check("B4 drawn with a full board, it stays in the hand as a playable card (no extra draw)", second != null && second.Zone == boss.HandZone && boss.HandZone.Count == 1);
    var view = FairView(g);
    Check("B5 the boss's fair view counts its own deck (the public shuffle is known to it)", view.boss.DeckZone.Count == boss.DeckZone.Count);
    Round(g);
    while (!boss.DeckZone.IsFull) Entity.FromCard(boss, C(Yeti), zone: boss.DeckZone);
    Check("B6 with a full deck it is not offered", !Offered(g));
});

// ============================================================ C  Jolene Knottley: Spoil the Ending
Guard("Jolene", () =>
{
    var g = Fight("ulda_43_jolene");
    Controller boss = g.Player2, player = g.Player1;
    EmptyHand(player);
    ToBoss(g);
    EmptyHand(boss);
    Check("C1 Spoil the Ending costs (2); with the run player's hand empty it is not offered", C("ULDA_BOSS_43p").Cost == 2 && Clickable(g) && !Offered(g));
    Round(g);
    EmptyHand(player);
    var fireball = ToHand(g, player, Fireball);
    ToHand(g, player, Yeti);
    EmptyHand(boss);
    bool used = Power(g);
    var copy = boss.HandZone.FirstOrDefault();
    Check("C2 a copy of the spell in the run player's hand goes to the boss's hand, Spoiled: it costs (2) less (ULDA_BOSS_43e1); the original stays",
        used && copy != null && copy.Card.Id == Fireball && copy != fireball && Has(copy, "ULDA_BOSS_43e1") && copy.Cost == C(Fireball).Cost - 2 && fireball.Zone == player.HandZone
        && player.HandZone.Count == 2, copy == null ? "none" : copy.Card.Id + " cost " + copy.Cost);
    var (view, viewBoss, viewPlayer) = FairView(g);
    Check("C3 the boss's fair view holds the copy (its own hand) and keeps the run player's hand masked (nothing links the copy to it)",
        viewBoss.HandZone.Any(p => p.Card.Id == Fireball) && viewPlayer.HandZone.Count == 2 && viewPlayer.HandZone.All(p => p is Unknown)
        && copy[GameTag.CREATOR] != fireball.Id && copy[GameTag.DISPLAYED_CREATOR] != fireball.Id, string.Join(",", viewPlayer.HandZone.Select(p => p.GetType().Name)));
    Round(g);
    EmptyHand(player); EmptyHand(boss);
    ToHand(g, player, Yeti); ToHand(g, player, Croc);
    used = Power(g);
    Check("C4 with no spell in the run player's hand it is offered (the boss cannot know) and does nothing", used && boss.HandZone.Count == 0);
    var seen = new HashSet<string>();
    var costs = new List<string>();
    bool spoiled = true;
    for (int i = 0; i < 12; i++)
    {
        Round(g);
        EmptyHand(player); EmptyHand(boss);
        ToHand(g, player, Moonfire); ToHand(g, player, Frostbolt); ToHand(g, player, Yeti);
        Power(g);
        if (boss.HandZone.Count != 1) continue;
        var got = boss.HandZone[0];
        seen.Add(got.Card.Id);
        costs.Add(got.Card.Id + "=" + got.Cost);
        spoiled &= Has(got, "ULDA_BOSS_43e1");
    }
    Check("C5 twelve uses with two spells in that hand: both are copied (random by the game RNG), each Spoiled; the Frostbolt copy costs 0 (2 less) and the Moonfire copy 0, not less",
        seen.SetEquals(new[] { Moonfire, Frostbolt }) && spoiled && costs.Count > 0
        && costs.All(x => x == Moonfire + "=0" || x == Frostbolt + "=" + Math.Max(0, C(Frostbolt).Cost - 2)) && C(Frostbolt).Cost == 2 && C(Moonfire).Cost == 0,
        string.Join(",", costs.Distinct()));
    Round(g);
    FillHand(boss);
    Check("C6 with the boss's hand full it is not offered", !Offered(g));
});

// ============================================================ D  Belloc Brightblade: Reliquary's Relics
Guard("Belloc", () =>
{
    var g = Fight("ulda_45_belloc");
    Controller boss = g.Player2, player = g.Player1;
    Check("D1 Reliquary's Relics is passive: started at match start, never an option", PassiveStarted(g, "ULDA_BOSS_45p") && SameText("ULDA_BOSS_45p"));
    EmptyHand(boss);
    var yeti = Board(g, boss, Yeti);
    Cast(g, Moonfire, yeti);
    Check("D2 the run player casts Moonfire: a new copy goes to the boss's hand", boss.HandZone.Count == 1 && boss.HandZone[0].Card.Id == Moonfire && yeti.Damage == 1);
    Cast(g, Wisp);
    Check("D3 a minion the run player plays gives nothing", boss.HandZone.Count == 1);
    ToBoss(g);
    EmptyHand(boss);
    Cast(g, Moonfire, player.Hero);
    Check("D4 a spell the boss casts gives nothing", boss.HandZone.Count == 0);
    ToPlayer(g);
    FillHand(boss);
    Cast(g, Frostbolt, yeti);
    Check("D5 with the boss's hand full the copy is lost", boss.HandZone.Count == 10 && boss.HandZone.All(p => p.Card.Id == Yeti));
});

// ============================================================ E  Wildtooth: Maw of the Desert (cast by rule)
Guard("Wildtooth", () =>
{
    var g = Fight("ulda_46_wildtooth");
    Controller boss = g.Player2, player = g.Player1;
    Check("E1 Maw of the Desert costs (0) and is cast by rule: passive in the registry (never an option), started at match start; the card carries AI_MUST_PLAY",
        C("ULDA_BOSS_46p").Cost == 0 && PassiveStarted(g, "ULDA_BOSS_46p") && C("ULDA_BOSS_46p")[(GameTag)367] == 1);
    var wisp = Board(g, player, Wisp);
    var yeti = Board(g, player, Yeti);
    var own = Board(g, boss, Croc);
    EndTurn(g);
    Check("E2 at the start of the boss's turn the minion with the lowest Health (the run player's Wisp) is destroyed; the others stay",
        wisp.Zone != player.BoardZone && yeti.Zone == player.BoardZone && own.Zone == boss.BoardZone && DungeonRunEncounters.AutoCasts(boss.Hero.HeroPower) == 1 && !Offered(g));
    Round(g);
    Check("E3 next turn it takes the boss's own River Crocolisk (3 Health) over the Yeti", own.Zone != boss.BoardZone && yeti.Zone == player.BoardZone && DungeonRunEncounters.AutoCasts(boss.Hero.HeroPower) == 2);
    ClearBoard(player); ClearBoard(boss);
    var seen = new HashSet<int>();
    for (int i = 0; i < 10; i++)
    {
        ToPlayer(g);
        ClearBoard(player); ClearBoard(boss);
        var a = Board(g, player, Croc); var b = Board(g, boss, Croc);
        EndTurn(g);
        seen.Add(a.Zone == player.BoardZone ? 2 : 1);
        if ((a.Zone == player.BoardZone) == (b.Zone == boss.BoardZone)) seen.Add(99);
    }
    Check("E4 a tie (one River Crocolisk on each side, ten times): exactly one dies each time, and each side's at least once (game RNG)", seen.SetEquals(new[] { 1, 2 }), string.Join(",", seen));
    ToPlayer(g);
    ClearBoard(player); ClearBoard(boss);
    int casts = DungeonRunEncounters.AutoCasts(boss.Hero.HeroPower);
    EndTurn(g);
    Check("E5 with no minion on either board it is not cast", DungeonRunEncounters.AutoCasts(boss.Hero.HeroPower) == casts);
    var g2 = Fight("ulda_46_wildtooth", shrine: Shrine);
    var shrine = ShrineRules.Of(g2.Player1).FirstOrDefault();
    EndTurn(g2);
    Check("E6 a run shrine, the only minion, is destroyed as a shrine is: its dormant form takes its slot", shrine != null && ShrineRules.IsCorpse(shrine)
        && ShrineRules.Of(g2.Player1).Count(ShrineRules.IsDormantForm) == 1);
});

// ============================================================ F  Ilza Grimdrum: Arena Expert (cast by rule)
Guard("Ilza", () =>
{
    var g = Fight("ulda_48_ilza", bossDeck: Yeti, playerDeck: Croc);
    Controller boss = g.Player2, player = g.Player1;
    Check("F1 Arena Expert costs (0) and is cast by rule: passive in the registry, started at match start", C("ULDA_BOSS_48p").Cost == 0 && PassiveStarted(g, "ULDA_BOSS_48p") && SameText("ULDA_BOSS_48p"));
    int bossDeck = boss.DeckZone.Count, playerDeck = player.DeckZone.Count;
    EndTurn(g);
    var yeti = boss.BoardZone.FirstOrDefault();
    Check("F2 at the start of the boss's turn a minion of each deck is summoned and the boss's attacks the run player's: the Yeti kills the River Crocolisk and keeps 2 damage",
        yeti != null && yeti.Card.Id == Yeti && yeti.Damage == 2 && player.BoardZone.Count == 0 && player.GraveyardZone.Any(p => p.Card.Id == Croc)
        && boss.DeckZone.Count == bossDeck - 2 && player.DeckZone.Count == playerDeck - 1 && DungeonRunEncounters.AutoCasts(boss.Hero.HeroPower) == 1,
        yeti?.Card.Id + " dmg " + yeti?.Damage + " deck " + boss.DeckZone.Count + "/" + player.DeckZone.Count);
    var g2 = Fight("ulda_48_ilza", bossDeck: Moonfire, playerDeck: Croc);
    EndTurn(g2);
    Check("F3 the boss's deck holds no minion: the run player's minion is summoned for its owner and nothing fights", g2.Player2.BoardZone.Count == 0 && g2.Player1.BoardZone.Count == 1
        && g2.Player1.BoardZone[0].Damage == 0);
    ToPlayer(g2);
    FillBoard(g2, g2.Player2);
    int casts = DungeonRunEncounters.AutoCasts(g2.Player2.Hero.HeroPower);
    EndTurn(g2);
    Check("F4 with the boss's board full it is not cast", DungeonRunEncounters.AutoCasts(g2.Player2.Hero.HeroPower) == casts);
});

// ============================================================ G  Totally Normal Jar: What's in the Jar?
Guard("Jar", () =>
{
    var g = Fight("ulda_47_jar", shrine: Shrine);
    Controller boss = g.Player2, player = g.Player1;
    var yeti = Board(g, player, Yeti);
    var shrine = ShrineRules.Of(player).FirstOrDefault();
    ToBoss(g);
    var own = Board(g, boss, Croc);
    var targets = PowerTargets(g);
    Check("G1 What's in the Jar? costs (2); any minion is a target, never a run shrine", C("ULDA_BOSS_47p").Cost == 2 && shrine != null && targets.Contains(yeti) && targets.Contains(own)
        && !targets.Contains(shrine) && targets.Count == 2);
    Hit(g, yeti, 2);
    Generic.AddEnchantmentBlock(g, C("ULDA_BOSS_57e"), player.Hero, yeti);
    bool used = Power(g, yeti);
    var copy = boss.BoardZone.LastOrDefault();
    Check("G2 it summons for the boss a copy of the minion with its stats set to 1/1 (ULDA_BOSS_47e), undamaged; the original stays", used && copy != null && copy != yeti
        && copy.Card.Id == Yeti && copy.Controller == boss && copy.AttackDamage == 1 && copy.Health == 1 && Has(copy, "ULDA_BOSS_47e") && yeti.Zone == player.BoardZone && boss.UsedMana == 2,
        copy == null ? "none" : copy.AttackDamage + "/" + copy.Health);
    Round(g);
    FillBoard(g, boss);
    Check("G3 with a full board it is not offered", !Offered(g));
});

// ============================================================ H  Lich Baz'hial: Lich's Phylactery, Broken Phylactery (the power swap)
Guard("Bazhial", () =>
{
    var plan = RowPlan("ulda_52_bazhial");
    Check("H1 the row's plan has two forms: the row's own (Lich's Phylactery) and the script form 1 (Broken Phylactery, no hero)", plan.BossForms.Count == 2
        && plan.BossForms[0].Power?.Id == "ULDA_BOSS_52p1" && plan.BossForms[1].Power?.Id == "ULDA_BOSS_52p2" && plan.BossForms[1].Hero == null && plan.BossHeroPowerId == "ULDA_BOSS_52p1");
    var g = Fight("ulda_52_bazhial");
    Controller boss = g.Player2, player = g.Player1;
    var phylactery = boss.Hero.HeroPower;
    ToBoss(g);
    Check("H2 Lich's Phylactery costs (0), is clickable, is started (its death count runs) and restores 20 on the card; at full Health it is not offered",
        C("ULDA_BOSS_52p1").Cost == 0 && Clickable(g) && phylactery[EncounterRules.PowerActive] == 1 && DungeonRunEncounters.TombsC.PhylacteryHeal(phylactery) == 20 && !Offered(g));
    for (int i = 0; i < 3; i++) Kill(g, Board(g, boss, Wisp));
    Kill(g, Board(g, player, Wisp));
    Check("H3 three friendly minions die (and one of the run player's): it now restores 17", DungeonRunEncounters.TombsC.PhylacteryHeal(phylactery) == 17, DungeonRunEncounters.TombsC.PhylacteryHeal(phylactery).ToString());
    boss.Hero.Armor = 3;
    Hit(g, boss.Hero, 25);
    Cast(g, WarAxe);
    int hp = boss.Hero.Health, max = boss.Hero.BaseHealth;
    var heroBefore = boss.Hero;
    bool used = Power(g);
    Check("H4 used, it restores 17 Health and breaks: the boss is in form 1 with Broken Phylactery (passive, started), the same hero, Armor and weapon",
        used && boss.Hero == heroBefore && boss.Hero.Health == Math.Min(max, hp + 17) && boss.Hero.HeroPower.Card.Id == "ULDA_BOSS_52p2" && boss.Hero.HeroPower.IsPassiveHeroPower
        && boss.Hero.HeroPower[EncounterRules.PowerActive] == 1 && boss[EncounterRules.FormIndex] == 1 && boss[EncounterRules.FormChanges] == 1 && boss.Hero.Weapon?.Card.Id == WarAxe
        && boss.Hero.BaseHealth == max && phylactery.Zone == boss.SetasideZone, hp + "->" + boss.Hero.Health + " power " + boss.Hero.HeroPower.Card.Id);
    Round(g);
    Check("H5 Broken Phylactery is never an option", !Offered(g));
    EmptyDeck(boss);
    int before = boss.Hero.Health + boss.Hero.Armor;
    Generic.Draw(boss); g.DeathProcessingAndAuraUpdate();
    int first = before - boss.Hero.Health - boss.Hero.Armor;
    before = boss.Hero.Health + boss.Hero.Armor;
    Generic.Draw(boss); g.DeathProcessingAndAuraUpdate();
    int second = before - boss.Hero.Health - boss.Hero.Armor;
    Check("H6 the boss takes double fatigue damage: 2, then 4 (the fatigue count itself is 1, then 2)", first == 2 && second == 4 && boss.Hero.Fatigue == 2, first + "," + second);
    EmptyDeck(player);
    before = player.Hero.Health;
    Generic.Draw(player); g.DeathProcessingAndAuraUpdate();
    Check("H7 the run player's fatigue is not doubled", before - player.Hero.Health == 1);
    var end = DungeonRunSetup.Harvest(g);
    Check("H8 the harvest reads form 1 and one form change", end.BossForm == 1 && end.BossFormChanges == 1);
    var g2 = Fight("ulda_52_bazhial");
    EmptyDeck(g2.Player2);
    int hp2 = g2.Player2.Hero.Health;
    Generic.Draw(g2.Player2); g2.DeathProcessingAndAuraUpdate();
    Check("H9 before it breaks the boss's fatigue is the usual 1", hp2 - g2.Player2.Hero.Health == 1);
    ToBoss(g2);
    Hit(g2, g2.Player2.Hero, 5);
    for (int i = 0; i < 22; i++) Kill(g2, Board(g2, g2.Player2, Wisp));
    hp2 = g2.Player2.Hero.Health;
    bool used2 = Power(g2);
    Check("H10 after 22 friendly deaths it restores 0 (never below 0) and still breaks", DungeonRunEncounters.TombsC.PhylacteryHeal(g2.Player2.SetasideZone.OfType<HeroPower>().FirstOrDefault()) == 0
        && used2 && g2.Player2.Hero.Health == hp2 && g2.Player2.Hero.HeroPower.Card.Id == "ULDA_BOSS_52p2");
});

// ============================================================ I  Craz: Demon Emissary
Guard("Craz", () =>
{
    var g = Fight("ulda_49_craz");
    Controller boss = g.Player2;
    ToBoss(g);
    var pool = DungeonRunEncounters.TombsC.EmissaryPool(g, boss, boss.Hero.HeroPower);
    Check("I1 Demon Emissary costs (2); its pool is scripted Demons of any class costing (3) or less", C("ULDA_BOSS_49p").Cost == 2 && pool.Count >= 8
        && pool.All(c => c.Implemented && c.IsRace(Race.DEMON) && c.Cost <= 3 && c.Type == CardType.MINION) && pool.Select(c => c.Class).Distinct().Count() >= 2, pool.Count.ToString());
    var seen = new HashSet<string>();
    for (int i = 0; i < 10; i++)
    {
        ClearBoard(boss);
        Power(g);
        if (boss.BoardZone.Count == 1) seen.Add(boss.BoardZone[0].Card.Id);
        Round(g);
    }
    Check("I2 ten uses summon Demons of the pool, several kinds", seen.Count >= 4 && seen.All(id => pool.Any(c => c.Id == id)), string.Join(",", seen));
    FillBoard(g, boss);
    Check("I3 with a full board it is not offered", !Offered(g));
});

// ============================================================ J  Ol' Barkeye: Desert Warfare
Guard("Barkeye", () =>
{
    var g = Fight("ulda_50_barkeye");
    Controller boss = g.Player2, player = g.Player1;
    Check("J1 Desert Warfare is passive: started at match start, never an option", PassiveStarted(g, "ULDA_BOSS_50p") && SameText("ULDA_BOSS_50p"));
    var yeti = Board(g, player, Yeti);
    ToBoss(g);
    EmptyHand(boss);
    Cast(g, WarAxe);
    bool hit = HeroAttack(g, yeti);
    var got = boss.HandZone.FirstOrDefault();
    var pool = DungeonRunEncounters.TombsC.WarfarePool(g, boss, boss.Hero.HeroPower);
    Check("J2 after the boss's hero attacks, a random scripted Hunter card (no Quest, no Secret) goes to its hand", hit && boss.HandZone.Count == 1 && got.Card.Class == CardClass.HUNTER
        && got.Card.Implemented && !got.Card.IsSecret && !got.Card.IsQuest && pool.Any(c => c.Id == got.Card.Id) && pool.All(c => !c.IsSecret && !c.IsQuest) && pool.Count > 30,
        got?.Card.Id + " pool " + pool.Count);
    ToPlayer(g);
    int hand = boss.HandZone.Count;
    Cast(g, WarAxe);
    HeroAttack(g, boss.Hero);
    Check("J3 the run player's hero attacking gives nothing", boss.HandZone.Count == hand);
});

// ============================================================ K  Kasmut: Tol'vir Necromancy
Guard("Kasmut", () =>
{
    var g = Fight("ulda_51_kasmut");
    Controller boss = g.Player2, player = g.Player1;
    Check("K1 Tol'vir Necromancy is passive: started at match start, never an option", PassiveStarted(g, "ULDA_BOSS_51p") && SameText("ULDA_BOSS_51p"));
    ToBoss(g);
    var mummy = Board(g, boss, MicroMummy);
    Kill(g, mummy);
    var back = boss.BoardZone.FirstOrDefault(m => m.Card.Id == MicroMummy);
    Check("K2 the boss's Reborn minion returns (no Reborn) at full Health, not 1", back != null && back != mummy && back[GameTag.REBORN] == 0 && back.Health == back.BaseHealth && back.BaseHealth == 2,
        back == null ? "none" : back.Health + "/" + back.BaseHealth);
    var theirs = Board(g, player, MicroMummy);
    Kill(g, theirs);
    var theirsBack = player.BoardZone.FirstOrDefault(m => m.Card.Id == MicroMummy);
    Check("K3 the run player's Reborn minion is restored to full too (\"a minion\")", theirsBack != null && theirsBack.Health == 2);
    // A minion summoned already damaged with the Reborn-returned mark (as a copy can carry it, BonusEffects.CopyState) is no Reborn
    // return unless its creator is a dead minion of its card in a graveyard: with no creator, and with a living one of its card.
    // (Damage set as Game.ResolveReborns sets it: on the new entity, before its summon.)
    var marked = (Minion)Entity.FromCard(boss, C(Yeti), new EntityData { [GameTag.HAS_BEEN_REBORN] = 1 });
    marked.Damage = 2;
    Generic.SummonBlock(g, marked, -1, boss.Hero);
    g.DeathProcessingAndAuraUpdate();
    var living = Board(g, boss, Yeti);
    var linked = (Minion)Entity.FromCard(boss, C(Yeti), new EntityData { [GameTag.HAS_BEEN_REBORN] = 1, [GameTag.DISPLAYED_CREATOR] = living.Id });
    linked.Damage = 2;
    Generic.SummonBlock(g, linked, -1, boss.Hero);
    g.DeathProcessingAndAuraUpdate();
    Check("K4 a damaged minion summoned with the Reborn-returned mark but not by Reborn (no creator, or a living creator of its card) keeps its damage",
        marked.Zone == boss.BoardZone && marked.Damage == 2 && linked.Zone == boss.BoardZone && linked.Damage == 2 && living.Zone == boss.BoardZone,
        marked.Damage + "/" + linked.Damage);
});

// ============================================================ L  Taskmaster Maru: E.V.I.L. Conscription
Guard("Maru", () =>
{
    var g = Fight("ulda_53_maru");
    Controller boss = g.Player2;
    ToBoss(g);
    EmptyHand(boss);
    var pool = DungeonRunEncounters.TombsC.LackeyPool(g, boss);
    var seen = new HashSet<string>();
    for (int i = 0; i < 14; i++) { Power(g); Round(g); }
    foreach (var p in boss.HandZone) if (DungeonRunEncounters.TombsC.Lackeys.Contains(p.Card.Id)) seen.Add(p.Card.Id);
    Check("L1 E.V.I.L. Conscription costs (1); each use adds a random scripted Lackey (only Lackeys of the pool, several kinds)", C("ULDA_BOSS_53p").Cost == 1 && pool.Count >= 4
        && pool.All(c => c.Implemented) && seen.Count >= 3 && seen.All(id => pool.Any(c => c.Id == id)), "pool " + pool.Count + ": " + string.Join(",", seen));
    FillHand(boss);
    Check("L2 with a full hand it is not offered", !Offered(g));
});

// ============================================================ M  Janak: Cavalry of E.V.I.L.
Guard("Janak", () =>
{
    var g = Fight("ulda_54_janak");
    Controller boss = g.Player2, player = g.Player1;
    var croc = Board(g, player, Croc);
    var wisp = Board(g, player, Wisp);
    ToBoss(g);
    var own = Board(g, boss, Yeti);
    Check("M1 Cavalry of E.V.I.L. costs (1); any minion is a target", C("ULDA_BOSS_54p").Cost == 1 && PowerTargets(g).Count == 3 && !PowerTargets(g).Contains(player.Hero));
    bool canBefore = own.CanAttack;
    Power(g, own);
    Check("M2 the boss's Yeti summoned this turn takes 1 damage and gets Charge (ULDA_BOSS_54e): it can attack the hero at once", !canBefore && own.Damage == 1 && Has(own, "ULDA_BOSS_54e")
        && own.HasCharge && own.CanAttack);
    Round(g);
    Power(g, wisp);
    Check("M3 a 1-Health minion dies of it and gets nothing", wisp.Zone != player.BoardZone && player.GraveyardZone.Contains(wisp));
    Round(g);
    Power(g, croc);
    Check("M4 the run player's River Crocolisk: 1 damage and Charge", croc.Damage == 1 && Has(croc, "ULDA_BOSS_54e"));
});

// ============================================================ N  Water-Toting Murlocs: Fish Food
Guard("Murlocs", () =>
{
    var g = Fight("ulda_55_murlocs");
    Controller boss = g.Player2, player = g.Player1;
    ToBoss(g);
    var yeti = Board(g, boss, Yeti);
    Check("N1 Fish Food costs (1); with no Murloc on either board it is not offered", C("ULDA_BOSS_55p").Cost == 1 && !Offered(g));
    var mine = Board(g, boss, "EX1_103");
    var theirs = Board(g, player, "EX1_507");
    Hit(g, mine, 1); Hit(g, theirs, 1); Hit(g, yeti, 3);
    bool used = Power(g);
    Check("N2 it restores 2 Health to every Murloc of either board (Coldlight Seer; the run player's Murloc Warleader too); the Yeti keeps its damage", used && mine.Damage == 0 && theirs.Damage == 0
        && yeti.Damage == 3 && boss.UsedMana == 1, $"used {used} {mine.Damage}/{theirs.Damage}/{yeti.Damage} mana {boss.UsedMana}");
});

// ============================================================ O  Sand Puffer Frog: Sandy Surprise
Guard("Frog", () =>
{
    var g = Fight("ulda_56_frog");
    Controller boss = g.Player2, player = g.Player1;
    Check("O1 Sandy Surprise is passive: started at match start, never an option", PassiveStarted(g, "ULDA_BOSS_56p") && SameText("ULDA_BOSS_56p"));
    var theirs = (Minion)ToHand(g, player, MurlocRaider);
    Play(g, theirs);
    ToBoss(g);
    var one = (Minion)ToHand(g, boss, MurlocRaider);
    Play(g, one);
    var two = (Minion)ToHand(g, boss, Raptor);
    Play(g, two);
    var summoned = Board(g, boss, MurlocRaider);
    Check("O2 a 1-Cost minion the boss plays gets Stealth (ULDA_BOSS_56e); a 2-Cost one, one summoned without play and the run player's do not",
        one.HasStealth && Has(one, "ULDA_BOSS_56e") && !two.HasStealth && !summoned.HasStealth && !theirs.HasStealth);
});

// ============================================================ P  Winged Guardian: Titan's Beast
Guard("WingedGuardian", () =>
{
    var g = Fight("ulda_57_winged_guardian");
    Controller boss = g.Player2, player = g.Player1;
    var yeti = Board(g, player, Yeti);
    ToBoss(g);
    var wisp = Board(g, boss, Wisp);
    Check("P1 Titan's Beast costs (2); any minion is a target", C("ULDA_BOSS_57p").Cost == 2 && PowerTargets(g).Count == 2);
    Hit(g, yeti, 1);
    Power(g, yeti);
    Check("P2 the run player's Yeti becomes 3/3 (ULDA_BOSS_57e)", yeti.AttackDamage == 3 && yeti.Health == 3 && yeti.BaseHealth == 3 && Has(yeti, "ULDA_BOSS_57e"), yeti.AttackDamage + "/" + yeti.Health);
    Round(g);
    Power(g, wisp);
    Check("P3 the boss's Wisp becomes 3/3", wisp.AttackDamage == 3 && wisp.Health == 3);
});

// ============================================================ R  Dire Bat: Food Chain
Guard("DireBat", () =>
{
    var g = Fight("ulda_59_dire_bat");
    Controller boss = g.Player2, player = g.Player1;
    var yeti = Board(g, player, Yeti);
    var wisp = Board(g, player, Wisp);
    ToBoss(g);
    Hit(g, boss.Hero, 10);
    int hp = boss.Hero.Health;
    Check("R1 Food Chain costs (2); any minion is a target", C("ULDA_BOSS_59p").Cost == 2 && PowerTargets(g).Count == 2);
    Power(g, yeti);
    Check("R2 2 damage to the Yeti, which lives: no healing", yeti.Damage == 2 && boss.Hero.Health == hp);
    Round(g);
    Power(g, wisp);
    Check("R3 2 damage kills the Wisp: the boss's hero is restored 5 Health", wisp.Zone != player.BoardZone && boss.Hero.Health == hp + 5, hp + "->" + boss.Hero.Health);
});

// ============================================================ S  the rows, the tier rule, Jythiros's rejection, the admission rows, the validator
Guard("Rows", () =>
{
    var errors = DungeonRunValidator.Validate(data, null);
    Check("S1 the shipped data validates with the seventeen family 21 rows", errors.Count == 0, string.Join(" | ", errors.Take(4)));
    // (row, n, name, tier, wiki level, deck kind, chapter, flags)
    var rows = new (string id, string n, string name, string tier, int level, string kind, int chapter, string flags)[]
    {
        ("ulda_41_alda", "41", "Alda Petrik", "1", 2, "extended", 3, ""),
        ("ulda_42_rhogi", "42", "Sandshaper Rhogi", "2", 4, "extended", 2, ""),
        ("ulda_43_jolene", "43", "Jolene Knottley", "4", 7, "substituted", 2, "hiddenZone"),
        ("ulda_45_belloc", "45", "Belloc Brightblade", "3", 6, "substituted", 4, "passive"),
        ("ulda_46_wildtooth", "46", "Wildtooth", "3", 6, "substituted", 2, "passive,bothPlayers"),
        ("ulda_47_jar", "47", "Totally Normal Jar", "2", 4, "extended", 4, ""),
        ("ulda_48_ilza", "48", "Ilza Grimdrum", "2", 4, "extended", 4, "passive,bothPlayers,hiddenZone"),
        ("ulda_49_craz", "49", "Craz", "4", 7, "substituted", 3, ""),
        ("ulda_50_barkeye", "50", "Ol' Barkeye", "3", 6, "substituted", 1, "passive"),
        ("ulda_51_kasmut", "51", "Kasmut", "2", 4, "extended", 3, "passive,bothPlayers"),
        ("ulda_52_bazhial", "52", "Lich Baz'hial", "4", 7, "authentic", 3, "forms,fatigue"), // step 9 (lane R) adds the new boss flags: fatigue
        ("ulda_53_maru", "53", "Taskmaster Maru", "2", 4, "extended", 1, ""),
        ("ulda_54_janak", "54", "Janak", "2", 4, "extended", 3, "charge"), // step 9 (lane R) adds the new boss flags: charge
        ("ulda_55_murlocs", "55", "Water-Toting Murlocs", "1", 2, "extended", 2, "bothPlayers"),
        ("ulda_56_frog", "56", "Sand Puffer Frog", "1", 2, "extended", 1, "passive"),
        ("ulda_57_winged_guardian", "57", "Winged Guardian", "2", 4, "extended", 1, ""),
        ("ulda_59_dire_bat", "59", "Dire Bat", "2", 4, "extended", 3, ""),
    };
    Check("S2 family 21 holds exactly these seventeen rows, in this order", data.Encounters.Where(b => b.Family == "21").Select(b => b.Id).SequenceEqual(rows.Select(r => r.id)),
        string.Join(",", data.Encounters.Where(b => b.Family == "21").Select(b => b.Id)));
    var chapterTag = new Dictionary<int, GameTag> { { 1, (GameTag)1202 }, { 2, (GameTag)1203 }, { 3, (GameTag)1204 }, { 4, (GameTag)1205 } };
    foreach (var r in rows)
    {
        var b = data.Boss(r.id);
        var plan = RowPlan(r.id);
        string hero = "ULDA_BOSS_" + r.n + "h", power = r.n == "52" ? "ULDA_BOSS_52p1" : "ULDA_BOSS_" + r.n + "p";
        // The tier rule (verify/tools/gen/p8f19/tombs_tiers.py): level 1-2 tier 1, 3-4 tier 2, 5-6 tier 3, 7 tier 4.
        string ruleTier = r.level <= 2 ? "1" : r.level <= 4 ? "2" : r.level <= 6 ? "3" : "4";
        Check("S3 " + r.id + ": a Tombs row of tier " + r.tier + " (wiki level " + r.level + ", chapter " + r.chapter + ") whose plan is the row's (hero, power, a 30-card " + r.kind
            + " deck, the act curve's Health, not the printed 10)",
            b.Provenance == "ULDA" && b.Name == r.name && b.Tier == r.tier && ruleTier == r.tier && b.HeroCard == hero && b.Power == power && b.Source.Kind == r.kind
            && string.Join(",", b.Flags) == r.flags && b.LevelOriginal == r.level
            && plan.EncounterId == r.id && plan.EncounterDeck && plan.BossHero.Id == hero && plan.BossHeroPowerId == power && plan.BossDeck.Count == 30 && data.BossDecks[b.Deck].Count == 30
            && !b.FixedHealth && b.Health == null && plan.BossHealth == 30 && C(hero).Type == CardType.HERO && C(hero).Health == 10 && C(hero)[chapterTag[r.chapter]] > 0
            && C(power).Type == CardType.HERO_POWER && C(hero).Name == r.name && data.Schedules.Member(b, DungeonRunEncounterData.PoolOf(b.Tier))
            && b.Source.Url.StartsWith("https://hearthstone.wiki.gg/wiki/") && !b.BossGeneratedSecrets && b.Forms.Count == (r.n == "52" ? 1 : 0) && b.Start == null
            && b.Pools.SequenceEqual(new[] { "enc-t" + r.tier }),
            b.Source.Kind + " flags " + string.Join(",", b.Flags) + " health " + plan.BossHealth + " tier " + b.Tier + " pools " + string.Join(",", b.Pools));
    }
    string[] passives = { "ULDA_BOSS_45p", "ULDA_BOSS_46p", "ULDA_BOSS_48p", "ULDA_BOSS_50p", "ULDA_BOSS_51p", "ULDA_BOSS_52p2", "ULDA_BOSS_56p" };
    string[] powers = rows.Select(r => r.n == "52" ? "ULDA_BOSS_52p1" : "ULDA_BOSS_" + r.n + "p").Concat(new[] { "ULDA_BOSS_52p2" }).ToArray();
    string[] enchantments = { "ULDA_BOSS_42e", "ULDA_BOSS_43e1", "ULDA_BOSS_47e", "ULDA_BOSS_54e", "ULDA_BOSS_56e", "ULDA_BOSS_57e" };
    var unscripted = powers.Concat(enchantments).Where(id => !C(id).Implemented || DungeonRunEncounters.Find(id)?.Family != "21").ToList();
    // The engine's card text of Lich's Phylactery carries its loader's " @spelldmg" marker after the client text.
    var retexted = powers.Concat(enchantments).Where(id => !SameText(id) && C(id).Text != factById[id].GetProperty("text").GetString() + " @spelldmg").ToList();
    Check("S4 every power and enchantment of the family is scripted by family 21 and keeps the client's text (18 powers, 6 enchantments); Jythiros's power and the dummy are not scripted",
        unscripted.Count == 0 && retexted.Count == 0 && powers.Length == 18 && !DungeonRunEncounters.IsScripted("ULDA_BOSS_58p") && !DungeonRunEncounters.IsScripted("ULDA_BOSS_42d")
        && DungeonRunEncounters.All.Count(e => e.Family == "21") == 24, string.Join(",", unscripted.Concat(retexted)) + " | " + string.Join(" | ", retexted.Select(id => C(id).Text)));
    Check("S5 the seven passive or rule-cast powers are the registry's passive ones (never an option), the eleven others clickable; each power's Cost is the client's",
        powers.All(p => DungeonRunEncounters.PassivePowers.Contains(p) == passives.Contains(p) && DungeonRunEncounters.Find(p).Kind == (passives.Contains(p) ? EncounterCardKind.Passive : EncounterCardKind.Power)
            && C(p).Cost == FactCost(p)));
    var expectFamily = new Dictionary<string, string>
    {
        ["ULDA_BOSS_47p"] = "boss-tombs-c-board-power", ["ULDA_BOSS_49p"] = "boss-tombs-c-board-power", ["ULDA_BOSS_54p"] = "boss-tombs-c-board-power",
        ["ULDA_BOSS_55p"] = "boss-tombs-c-board-power", ["ULDA_BOSS_57p"] = "boss-tombs-c-board-power", ["ULDA_BOSS_59p"] = "boss-tombs-c-board-power",
        ["ULDA_BOSS_41p"] = "boss-tombs-c-own-zone-power", ["ULDA_BOSS_42p"] = "boss-tombs-c-own-zone-power", ["ULDA_BOSS_53p"] = "boss-tombs-c-own-zone-power",
        ["ULDA_BOSS_43p"] = "boss-tombs-c-hidden-steal",
        ["ULDA_BOSS_45p"] = "boss-tombs-c-passive", ["ULDA_BOSS_50p"] = "boss-tombs-c-passive", ["ULDA_BOSS_51p"] = "boss-tombs-c-passive", ["ULDA_BOSS_56p"] = "boss-tombs-c-passive",
        ["ULDA_BOSS_52p2"] = "boss-tombs-c-passive", ["ULDA_BOSS_46p"] = "boss-tombs-c-rule-cast", ["ULDA_BOSS_48p"] = "boss-tombs-c-rule-cast", ["ULDA_BOSS_52p1"] = "boss-tombs-c-form-power",
    };
    var wrong = powers.Where(id =>
    {
        var v = DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss, null, catalogRun: true);
        return !(v.Admitted && v.Subject && v.Rule?.Ids != null && v.Rule.Ids.Contains(id) && DungeonRunAdmission.RuleFor(id)?.Family == expectFamily[id]
            && DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss).Admitted
            && !DungeonRunAdmission.Classify(C(id), id, AdmitScope.Player, null, catalogRun: true).Admitted
            && !DungeonRunAdmission.Classify(C(id), id, AdmitScope.Generated, null, catalogRun: true).Admitted
            && DungeonRunAdmission.GeneratedRejects(true).Contains(id) && DungeonRunAdmission.GeneratedRejects(false).Contains(id));
    }).ToList();
    Check("S6 each power has its own admission row of its family: a boss's only, never the run player's, never generated", wrong.Count == 0 && expectFamily.Count == 18, string.Join(",", wrong));
    var riddle = DungeonRunAdmission.Classify(C("ULDA_BOSS_58p"), "ULDA_BOSS_58p", AdmitScope.Boss, null, catalogRun: true);
    Check("S7 Jythiros of Questions is rejected: no row uses his hero, his power is admitted for no side with its lift, and the encounters file records the rejection",
        !data.Bosses.Any(b => b.HeroCard == "ULDA_BOSS_58h") && !riddle.Admitted && DungeonRunAdmission.RuleFor("ULDA_BOSS_58p")?.Family == "boss-tombs-c-rejected"
        && DungeonRunAdmission.RuleFor("ULDA_BOSS_58p")?.Lift == DungeonRunAdmission.RiddleLift
        && R("dungeon-run/encounters-v1.json").Contains("\"ULDA_BOSS_58h\"") && R("dungeon-run/reference/encounters/F21.json").Contains("Riddle of the Sphinx"));
    string Subs(string id) => string.Join(",", data.Boss(id).Source.Substitutions.Where(x => x.Listed != null).Select(x => x.Listed + ">" + (x.Used ?? "-") + ":" + x.Reason).Distinct().OrderBy(x => x, StringComparer.Ordinal));
    var replaced = rows.SelectMany(r => data.Boss(r.id).Source.Substitutions).Where(x => x.Listed != null && x.Reason != "over-size").ToList();
    Check("S8 the listed cards replaced are only cards the engine does not script (or Secret generators, or Gral, the Shark, whose deck pull no boss row admits), each with its named replacement",
        replaced.All(x => x.Used != null && (x.Reason == "unscripted" && !C(x.Listed).Implemented || x.Reason.StartsWith("admission:")))
        && Subs("ulda_43_jolene") == "ULD_160>DAL_614:unscripted,ULD_162>BRM_006:unscripted,ULD_163>CS2_065:unscripted"
        && Subs("ulda_47_jar") == "ULD_163>OG_241:unscripted,ULD_184>EX1_029:unscripted,ULD_703>FP1_007:unscripted" && Subs("ulda_52_bazhial") == "",
        Subs("ulda_43_jolene") + " | " + Subs("ulda_47_jar"));
    int In(string boss, string card) => data.BossDecks[data.Boss(boss).Deck].Count(id => id == card);
    Check("S9 the decks keep the wiki's signature cards: Wildtooth's four Bearsharks, Baz'hial's two Shadows of Death and Ancient Shades, Kasmut's Reborn minions, the Jar's Desert Obelisks as five Nerubian Eggs",
        In("ulda_46_wildtooth", "ICC_419") == 4 && In("ulda_52_bazhial", "ULD_286") == 2 && In("ulda_52_bazhial", "LOE_110") == 2 && In("ulda_47_jar", "FP1_007") == 5
        && data.BossDecks[data.Boss("ulda_51_kasmut").Deck].Count(id => C(id)[GameTag.REBORN] == 1) >= 12,
        "bearshark " + In("ulda_46_wildtooth", "ICC_419") + " shade " + In("ulda_52_bazhial", "LOE_110"));
    Check("S10 the tier pools: three rows in enc-t1, eight in enc-t2, three in enc-t3, three in enc-t4", rows.Count(r => r.tier == "1") == 3 && rows.Count(r => r.tier == "2") == 8
        && rows.Count(r => r.tier == "3") == 3 && rows.Count(r => r.tier == "4") == 3);
    DungeonRunData Changed(Action<DungeonRunData> change) { var d = Load(); change(d); return d; }
    var badPower = DungeonRunValidator.Validate(Changed(d => d.Boss("ulda_59_dire_bat").Power = "ULDA_BOSS_59px"), null);
    Check("S11 the validator refuses a row whose power is not scripted (the Heroic Food Chain ULDA_BOSS_59px)", badPower.Count > 0, string.Join(" | ", badPower.Take(2)));
    var noForm = DungeonRunValidator.Validate(Changed(d => d.Boss("ulda_52_bazhial").Forms.Clear()), null);
    Check("S12 the validator refuses Baz'hial's row without its form (flag forms and the forms go together)", noForm.Count > 0, string.Join(" | ", noForm.Take(2)));
});

Console.WriteLine($"SearchTelemetry swallowed leaf exceptions {SearchTelemetry.SwallowedLeafErrors}");
Console.WriteLine($"RESULT {passes} passed, {failures + SearchTelemetry.SwallowedLeafErrors} failed");
return failures + SearchTelemetry.SwallowedLeafErrors;
