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

// Phase 8 family 18, Dalaran Heist bosses E and the Heist finale (real engine games through the run setup path: DungeonRun.BuildPlan
// on the shipped row -> DungeonRunSetup.Configure / ApplyAfterStart): Sael'orn (Web Grab), Haro Setting-Sun (Darken), Dagg
// Cruelmight (Bully), Banker Biggs (Invest!), Valdera Highborne (Impervious), Marei Loom (Next...), Sky Captain Smiggs
// (Bombardment), Anarii Duskgrove (Summon Protectors), Lilayell Suntear (Mini Portal), Hesutu Stonewind (Earthmother's Rage),
// Kazamon Steelskin (Steelskin), Kara Stamper (Soul Weave); the finale bosses Magistrix Norroa (Kaironomaly), Archmage Vargoth
// (Arcane Runes), Archmage Kalec (Dragonwrath), Archmage Khadgar (Summon Elemental, Kirin Tor Elemental); the finale block on the
// fixture schedule (verify/fixtures/p8f18/fixtures/schedule-finale18.json) with its twist DALA_830; and the rows, admission rows,
// rejections and validator.
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
string fixtureSchedule = File.ReadAllText(VerifyPaths.Fixture("p8f18/fixtures/schedule-finale18.json"));
DungeonRunData Load(string schedule = null) => DungeonRunData.Parse(R("dungeon-run/run-v1.json"), R("dungeon-run/pool-v1.json"), R("npc/decks-v1.json"), R("dungeon-run/catalog-v1.json"),
    schedule ?? R("dungeon-run/schedule-v1.json"), R("dungeon-run/tavern-v1.json"), R("dungeon-run/encounters-v1.json"));
var data = Load();
var run = new DungeonRun(data);
var all = new DungeonRunProfile(); all.Unlocked.AddRange(data.Unlocks.Keys);

const string Yeti = "CS2_182", Croc = "CS2_120", Raptor = "CS2_172", Moonfire = "CS2_008", Fireball = "CS2_029", Wisp = "CS2_231", Hellfire = "CS2_062";
const string Geomancer = "CS2_142", Librarian = "LOOT_014", IceBarrier = "EX1_289", Arcane = "CS2_023";
const string Elemental = "DALA_504", Darkened = "DALA_BOSS_62e", Gains = "DALA_BOSS_64e", Rancor = "DALA_BOSS_73e", Treant = "EX1_573t";
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
// hero, power, Health and every other encounter field stay the row's).
Game Fight(string bossId, string bossDeck = Yeti, string playerDeck = Yeti, int seed = 7)
{
    var plan = RowPlan(bossId);
    plan.BossDeck = Thirty(bossDeck).Select(C).ToList();
    plan.PlayerDeck = Thirty(playerDeck).Select(C).ToList();
    plan.PlayerDeckSlots = null;
    return Ready(plan, seed);
}
void Mana(Controller c, int n = 10) { c.BaseMana = n; c.UsedMana = 0; c.OverloadLocked = 0; c.OverloadOwed = 0; }
IPlayable ToHand(Game g, Controller c, string id)
{
    while (c.HandZone.Count >= 9) { var old = c.HandZone[0]; c.HandZone.Remove(old); c.SetasideZone.Add(old); }
    var e = Entity.FromCard(c, C(id)); Generic.AddHandPhase.Invoke(c, e); g.DeathProcessingAndAuraUpdate(); return e;
}
void FillHand(Game g, Controller c, string id = Yeti) { while (!c.HandZone.IsFull) Generic.AddHandPhase.Invoke(c, Entity.FromCard(c, C(id))); }
void EmptyHand(Controller c) { foreach (var p in c.HandZone.ToArray()) { c.HandZone.Remove(p); c.SetasideZone.Add(p); } }
void EmptyDeck(Controller c) { foreach (var p in c.DeckZone.ToArray()) c.SetasideZone.Add(c.DeckZone.Remove(p)); }
Minion Board(Game g, Controller c, string id) { var m = (Minion)Entity.FromCard(c, C(id)); Generic.SummonBlock(g, m, -1, c.Hero); g.DeathProcessingAndAuraUpdate(); return m; }
void FillBoard(Game g, Controller c, string id = Wisp) { while (!c.BoardZone.IsFull) Board(g, c, id); }
bool Play(Game g, IPlayable card, ICharacter target = null) { Mana(g.CurrentPlayer); return g.Process(PlayCardTask.Any(g.CurrentPlayer, card, target, -1, 0)); }
bool Cast(Game g, string id, ICharacter target = null) => Play(g, ToHand(g, g.CurrentPlayer, id), target);
void EndTurn(Game g) => g.Process(EndTurnTask.Any(g.CurrentPlayer));
bool Offered(Game g) => g.CurrentPlayer.Options().Any(o => o is HeroPowerTask);
List<ICharacter> PowerTargets(Game g) => g.CurrentPlayer.Options().OfType<HeroPowerTask>().Select(o => o.Target).Where(t => t != null).ToList();
bool Power(Game g, ICharacter target = null) => Offered(g) && g.Process(HeroPowerTask.Any(g.CurrentPlayer, target));
void ToBoss(Game g) { if (g.CurrentPlayer == g.Player1) EndTurn(g); Mana(g.Player2); }
void ToPlayer(Game g) { if (g.CurrentPlayer == g.Player2) EndTurn(g); Mana(g.Player1); }
void Round(Game g) { EndTurn(g); EndTurn(g); Mana(g.CurrentPlayer); }
void Damage(Game g, ICharacter t, int n) { Generic.DamageCharFunc.Invoke(g.CurrentPlayer.Hero, t, n, false); g.DeathProcessingAndAuraUpdate(); }
bool Has(IPlayable p, string enchantmentId) => p.AppliedEnchantments?.Any(e => e.Card.Id == enchantmentId) == true;
int Cost(string id) => C(id).Cost;
bool IsPassive(Controller c, string id) => c.Hero.HeroPower.Card.Id == id && DungeonRunEncounters.PassivePowers.Contains(id) && c.Hero.HeroPower.IsPassiveHeroPower
    && c.Hero.HeroPower[EncounterRules.PowerActive] == 1;
(Game view, Controller boss, Controller player) FairView(Game g)
{
    var bot = FairBotState.Create(g, g.Player2.PlayerId, 1, DeckStyle.Tempo);
    var view = bot.FingerprintGame;
    var viewBoss = view.Player1.PlayerId == g.Player2.PlayerId ? view.Player1 : view.Player2;
    return (view, viewBoss, viewBoss.Opponent);
}

// ============================================================ A  Sael'orn: Web Grab
Guard("Saelorn", () =>
{
    var g = Fight("dala_61_saelorn");
    Controller boss = g.Player2, player = g.Player1;
    EmptyHand(player);
    ToBoss(g);
    bool none = !Offered(g);
    var raptor = ToHand(g, player, Raptor);
    ToHand(g, player, Moonfire); ToHand(g, player, Fireball);
    Check("A1 Web Grab costs (0), is clickable, and is offered only while the run player's hand holds a card (an empty hand: not offered)",
        Cost("DALA_BOSS_61p") == 0 && !boss.Hero.HeroPower.IsPassiveHeroPower && none && Offered(g) && PowerTargets(g).Count == 0);
    int board = player.BoardZone.Count, hand = player.HandZone.Count;
    Check("A2 the run player's only minion in hand is summoned to that player's board; the two spells stay in hand", Power(g) && raptor.Zone == player.BoardZone && raptor.Controller == player
        && player.BoardZone.Count == board + 1 && player.HandZone.Count == hand - 1 && player.HandZone.All(p => p.Card.Type == CardType.SPELL), player.BoardZone.Count + " on board");
    Round(g);
    hand = player.HandZone.Count; board = player.BoardZone.Count;
    EmptyHand(player); ToHand(g, player, Moonfire); ToHand(g, player, Fireball);
    Check("A3 with only spells in that hand the power is offered (the hand's content is hidden) and summons nothing", Offered(g) && Power(g) && player.BoardZone.Count == board && player.HandZone.Count == 2);
    Round(g);
    ToHand(g, player, Croc); FillBoard(g, player);
    Check("A4 with the run player's board full it is not offered", !Offered(g) && player.HandZone.Any(p => p.Card.Id == Croc));
    var g2 = Fight("dala_61_saelorn");
    EmptyHand(g2.Player1); var croc = ToHand(g2, g2.Player1, Croc); ToHand(g2, g2.Player1, Moonfire);
    ToBoss(g2);
    var (view, viewBoss, viewPlayer) = FairView(g2);
    int viewBoard = viewPlayer.BoardZone.Count; string viewPlay = "ok";
    Mana(viewBoss); viewBoss.Hero.HeroPower.IsExhausted = false;
    try { if (!view.Process(HeroPowerTask.Any(viewBoss, null))) viewPlay = "refused"; } catch (Exception e) { viewPlay = e.GetType().Name + ": " + e.Message; }
    Check("A5 the boss's fair view masks the run player's hand (no Crocolisk there), and the power resolves there without pulling a hidden card",
        viewPlay == "ok" && !viewPlayer.HandZone.Any(p => p.Card.Id == Croc) && viewPlayer.HandZone.Count == 2 && viewPlayer.BoardZone.Count == viewBoard, viewPlay);
    Check("A6 the real pull reveals the minion as any summon does: the Crocolisk is on the run player's board", Power(g2) && croc.Zone == g2.Player1.BoardZone);
});

// ============================================================ B  Haro Setting-Sun: Darken
Guard("Haro", () =>
{
    var g = Fight("dala_62_haro");
    Controller boss = g.Player2, player = g.Player1;
    ToBoss(g);
    bool none = !Offered(g);
    var mine = Board(g, player, Yeti); var wisp = Board(g, player, Wisp); var his = Board(g, boss, Croc);
    Check("B1 Darken costs (2) and is offered only while a minion is on a board", Cost("DALA_BOSS_62p") == 2 && none && Offered(g) && PowerTargets(g).Count == 0);
    Check("B2 every minion of both boards gets Darkened: -2 Attack (a 1-Attack minion goes to 0)", Power(g) && mine.AttackDamage == 2 && wisp.AttackDamage == 0 && his.AttackDamage == 0
        && Has(mine, Darkened) && Has(his, Darkened) && boss.UsedMana == 2, mine.AttackDamage + "/" + wisp.AttackDamage + "/" + his.AttackDamage);
    EndTurn(g);
    Check("B3 it lasts through the run player's turn", mine.AttackDamage == 2 && Has(mine, Darkened));
    EndTurn(g);
    Check("B4 it is gone when the boss's next turn starts (both boards)", mine.AttackDamage == 4 && his.AttackDamage == 2 && !Has(mine, Darkened) && !Has(his, Darkened),
        mine.AttackDamage + "/" + his.AttackDamage);
});

// ============================================================ C  Dagg Cruelmight: Bully
Guard("Dagg", () =>
{
    var g = Fight("dala_63_dagg");
    Controller boss = g.Player2, player = g.Player1;
    var yeti = Board(g, player, Yeti); var croc = Board(g, player, Croc);
    ToBoss(g);
    var own = Board(g, boss, Yeti);
    Board(g, boss, Geomancer);
    bool none = !Offered(g);
    Damage(g, yeti, 1); Damage(g, own, 1);
    var targets = PowerTargets(g);
    Check("C1 Bully costs (2); it is offered only at a damaged minion, of either board (never an undamaged one or a hero)", Cost("DALA_BOSS_63p") == 2 && none
        && targets.Count == 2 && targets.Contains(yeti) && targets.Contains(own) && !targets.Contains(croc), string.Join(",", targets.Select(t => t.Card.Id)));
    Check("C2 it deals 2 damage to the target (a hero power: the boss's Spell Damage does not add)", Power(g, yeti) && yeti.Damage == 3 && boss.CurrentSpellPower == 1, "damage " + yeti.Damage);
});

// ============================================================ D  Banker Biggs: Invest!
Guard("Biggs", () =>
{
    var g = Fight("dala_64_biggs");
    Controller boss = g.Player2, player = g.Player1;
    var enemy = Board(g, player, Croc);
    ToBoss(g);
    bool none = !Offered(g);
    var yeti = Board(g, boss, Yeti);
    Damage(g, yeti, 2);
    var targets = PowerTargets(g);
    int hand = boss.HandZone.Count;
    Check("D1 Invest! costs (4) and is aimed only at a minion of the boss (none on its board: not offered)", Cost("DALA_BOSS_64p") == 4 && none && targets.Count == 1 && targets[0] == yeti);
    Check("D2 the minion returns to the boss's hand undamaged with Capital Gains: an 8/9 Yeti there", Power(g, yeti) && yeti.Zone == boss.HandZone && Has(yeti, Gains)
        && yeti.AttackDamage == 8 && yeti.Health == 9 && yeti.Damage == 0 && boss.HandZone.Count == hand + 1 && boss.UsedMana == 4, yeti.AttackDamage + "/" + yeti.Health);
    Round(g);
    Board(g, boss, Raptor); FillHand(g, boss);
    Check("D3 with the boss's hand full it is not offered (the bounce would destroy the minion)", !Offered(g));
});

// ============================================================ E  Valdera Highborne: Impervious
Guard("Valdera", () =>
{
    var g = Fight("dala_65_valdera");
    Controller boss = g.Player2, player = g.Player1;
    Check("E1 Impervious is passive (started at match start, never an option) and carries the hero damage cap 3", IsPassive(boss, "DALA_BOSS_65p")
        && EncounterRules.HeroDamageCapOf(boss) == 3 && EncounterRules.HeroDamageCapOf(player) == 0, "cap " + EncounterRules.HeroDamageCapOf(boss));
    int health = boss.Hero.Health;
    Check("E2 a 6-damage Fireball at the boss's hero deals 3", Cast(g, Fireball, boss.Hero) && boss.Hero.Health == health - 3, (health - boss.Hero.Health).ToString());
    health = boss.Hero.Health;
    Check("E3 a 1-damage hit deals 1", Cast(g, Moonfire, boss.Hero) && boss.Hero.Health == health - 1);
    boss.Hero.Armor = 5; health = boss.Hero.Health;
    Check("E4 the cap comes before Armor: a Fireball takes 3 Armor and no Health", Cast(g, Fireball, boss.Hero) && boss.Hero.Armor == 2 && boss.Hero.Health == health, "armor " + boss.Hero.Armor);
    ToBoss(g);
    int playerHealth = player.Hero.Health;
    Check("E5 the run player's hero is not capped: the boss's Fireball deals 6", Cast(g, Fireball, player.Hero) && player.Hero.Health == playerHealth - 6);
});

// ============================================================ F  Marei Loom: Next... (cast by rule)
Guard("Marei", () =>
{
    // The close-out review: the wiki marks Next... Auto-cast and the card carries AI_MUST_PLAY, so it is cast by rule (family 09's
    // AutoCast) at the start of the boss's turn, after the draw, never offered as a click.
    var g = Fight("dala_66_marei", bossDeck: Croc, playerDeck: Raptor);
    Controller boss = g.Player2, player = g.Player1;
    ToPlayer(g);
    EmptyHand(player);
    var fireball = ToHand(g, player, Fireball); ToHand(g, player, Yeti);
    EmptyHand(boss);
    var moonfire = ToHand(g, boss, Moonfire); ToHand(g, boss, Yeti);
    // The boss's turn-start draw adds a Crocolisk at the right end of its hand; the left-most card stays the Moonfire.
    int ph = player.HandZone.Count, pd = player.DeckZone.Count, bh = boss.HandZone.Count + 1, bd = boss.DeckZone.Count - 1;
    EndTurn(g);
    bool used = DungeonRunEncounters.AutoCasts(boss.Hero.HeroPower) == 1;
    Check("F1 Next... costs (0) and is cast by rule (passive in the registry, never an option; the card carries AI_MUST_PLAY) while a player has a card in hand and in deck",
        Cost("DALA_BOSS_66p") == 0 && IsPassive(boss, "DALA_BOSS_66p") && C("DALA_BOSS_66p")[(GameTag)367] == 1 && !Offered(g) && used);
    Check("F2 at the boss's turn start each left-most hand card goes into its owner's deck and a card of that deck takes the left-most slot: the boss now holds a Crocolisk first, the run player a Raptor",
        used && moonfire.Zone == boss.DeckZone && boss.HandZone[0].Card.Id == Croc && fireball.Zone == player.DeckZone && player.HandZone[0].Card.Id == Raptor
        && player.HandZone.Count == ph && player.DeckZone.Count == pd && boss.HandZone.Count == bh && boss.DeckZone.Count == bd,
        boss.HandZone[0].Card.Id + " / " + player.HandZone[0].Card.Id + " hand " + boss.HandZone.Count + "/" + bh + " deck " + boss.DeckZone.Count + "/" + bd);
    var resets = g.RunBeliefResets.Where(r => r.Zones == RunBeliefZones.HandAndDeck).Select(r => r.ControllerId).ToList();
    Check("F3 what each side believed about the other's hand and deck is reset (one belief reset per player)", resets.Contains(boss.PlayerId) && resets.Contains(player.PlayerId), string.Join(",", resets));
    var (view, viewBoss, viewPlayer) = FairView(g);
    Check("F4 the boss's fair view does not learn the run player's new card: its hand is masked there (no Raptor, no Fireball), its own new Crocolisk is exact",
        !viewPlayer.HandZone.Any(p => p.Card.Id == Raptor || p.Card.Id == Fireball) && viewPlayer.HandZone.Count == ph && viewBoss.HandZone[0].Card.Id == Croc,
        string.Join(",", viewPlayer.HandZone.Select(p => p.Card.Id)));
    ToPlayer(g);
    EmptyHand(boss); EmptyHand(player); EmptyDeck(player); EmptyDeck(boss);
    int casts = DungeonRunEncounters.AutoCasts(boss.Hero.HeroPower);
    EndTurn(g);
    Check("F5 with no card in either hand and both decks empty (the boss's turn-start draw finds none) it is not cast", DungeonRunEncounters.AutoCasts(boss.Hero.HeroPower) == casts && !Offered(g));
    ToPlayer(g);
    EmptyHand(boss);
    for (int k = 0; k < 2; k++) Entity.FromCard(boss, C(Croc), zone: boss.DeckZone);
    var reduced = ToHand(g, boss, Fireball);
    Generic.AddEnchantmentBlock(g, C("BRM_028e"), boss.Hero, reduced);
    int costBefore = reduced.Cost;
    EndTurn(g);
    Check("F6 a hand card shuffled into the deck loses its hand enchantments (Fireball back at (4))", DungeonRunEncounters.AutoCasts(boss.Hero.HeroPower) == casts + 1
        && reduced.Zone == boss.DeckZone && reduced.Cost == 4 && costBefore == 3, costBefore + " -> " + reduced.Cost);
    // An Echo copy lives only in the turn it was played, and the rule cast comes at the turn start, before any card of that turn:
    // the swap rule itself (EncounterRules.SwapLeftmostWithDeck, which the cast runs) still handles one.
    EmptyHand(boss);
    var echo = ToHand(g, boss, Moonfire);
    g.GhostlyCards.Add(echo.Id);   // as PlayCard marks an Echo copy
    bool swapped = EncounterRules.SwapLeftmostWithDeck(boss, boss.Hero.HeroPower);
    var (view2, viewBoss2, _) = FairView(g);
    string viewEnd = "ok";
    try { if (!view2.Process(EndTurnTask.Any(viewBoss2))) viewEnd = "refused"; } catch (Exception e) { viewEnd = e.GetType().Name + ": " + e.Message; }
    Check("F7 an Echo copy the swap rule moves into the deck is an ordinary card there (it leaves the ghostly list): the turn ends in the boss's fair view and in the game",
        swapped && echo.Zone == boss.DeckZone && !g.GhostlyCards.Contains(echo.Id) && viewEnd == "ok" && g.Process(EndTurnTask.Any(boss)) && echo.Zone == boss.DeckZone, viewEnd);
});

// ============================================================ G  Sky Captain Smiggs: Bombardment
Guard("Smiggs", () =>
{
    var g = Fight("dala_70_smiggs");
    Controller boss = g.Player2, player = g.Player1;
    ToBoss(g);
    bool none = !Offered(g);
    var yeti = Board(g, player, Yeti); var his = Board(g, boss, Yeti); var wisp = Board(g, player, Wisp);
    Check("G1 Bombardment costs (2) and is offered only while a minion is on a board", Cost("DALA_BOSS_70p") == 2 && none && Offered(g));
    Check("G2 it deals 2 damage to every minion of both boards", Power(g) && yeti.Damage == 2 && his.Damage == 2 && wisp.Zone != player.BoardZone, yeti.Damage + "/" + his.Damage);
    Round(g);
    Check("G3 reloading: the boss's next turn it is not offered (minions on the boards)", !Offered(g) && player.BoardZone.Count > 0 && !DungeonRunEncounters.BombardmentReady(boss.Hero.HeroPower));
    Round(g);
    Check("G4 the boss's turn after that it is ready again", Offered(g) && DungeonRunEncounters.BombardmentReady(boss.Hero.HeroPower));
    var clone = g.Clone();
    Check("G5 a game clone carries the shot's turn (a tag of the power)", clone.Player2.Hero.HeroPower[GameTag.TAG_SCRIPT_DATA_NUM_1] == boss.Hero.HeroPower[GameTag.TAG_SCRIPT_DATA_NUM_1]
        && boss.Hero.HeroPower[GameTag.TAG_SCRIPT_DATA_NUM_1] > 0);
});

// ============================================================ H  Anarii Duskgrove: Summon Protectors
Guard("Anarii", () =>
{
    var g = Fight("dala_71_anarii");
    Controller boss = g.Player2;
    ToBoss(g);
    Check("H1 Summon Protectors costs (2) and summons a 2/2 Treant with Taunt", Cost("DALA_BOSS_71p") == 2 && Power(g) && boss.BoardZone.Count(m => m.Card.Id == Treant) == 1
        && boss.BoardZone.First(m => m.Card.Id == Treant) is Minion t && t.AttackDamage == 2 && t.Health == 2 && t.HasTaunt);
    Round(g);
    FillBoard(g, boss);
    Check("H2 with the boss's board full it is not offered", !Offered(g));
});

// ============================================================ I  Lilayell Suntear: Mini Portal
Guard("Lilayell", () =>
{
    var g = Fight("dala_68_lilayell");
    Controller boss = g.Player2, player = g.Player1;
    Check("I1 Mini Portal is passive (started at match start, never an option)", IsPassive(boss, "DALA_BOSS_68p"));
    ToBoss(g);
    int board = boss.BoardZone.Count;
    Check("I2 after the boss casts a spell a random 1-Cost minion is summoned for it", Cast(g, Moonfire, player.Hero) && boss.BoardZone.Count >= board + 1
        && boss.BoardZone.Skip(board).First().Card.Cost == 1, string.Join(",", boss.BoardZone.Select(m => m.Card.Id + ":" + m.Card.Cost)));
    var pool = DungeonRunEncounters.RuleMinions(g, boss, boss.Hero, "cost1", card => card.Cost == 1);
    Check("I3 the pool is scripted 1-Cost minions the run's generation filter allows for the boss", pool.Count > 20 && pool.All(c => c.Implemented && c.Cost == 1 && c.Type == CardType.MINION), pool.Count + " minions");
    ToPlayer(g);
    int playerBoard = player.BoardZone.Count;
    Check("I4 the run player's spells summon nothing", Cast(g, Moonfire, boss.Hero) && player.BoardZone.Count == playerBoard);
    ToBoss(g);
    FillBoard(g, boss);
    Check("I5 with the boss's board full nothing is summoned", Cast(g, Moonfire, player.Hero) && boss.BoardZone.IsFull);
});

// ============================================================ J  Hesutu Stonewind: Earthmother's Rage
Guard("Hesutu", () =>
{
    var g = Fight("dala_73_hesutu");
    Controller boss = g.Player2, player = g.Player1;
    var enemy = Board(g, player, Yeti);
    ToBoss(g);
    bool none = !Offered(g);
    var yeti = Board(g, boss, Yeti);
    var targets = PowerTargets(g);
    Check("J1 Earthmother's Rage costs (2) and is aimed only at a minion of the boss", Cost("DALA_BOSS_73p") == 2 && none && targets.Count == 1 && targets[0] == yeti);
    Check("J2 it gives the minion Windfury (Earthmother's Rancor)", Power(g, yeti) && yeti.HasWindfury && Has(yeti, Rancor));
});

// ============================================================ K  Kazamon Steelskin: Steelskin
Guard("Kazamon", () =>
{
    var g = Fight("dala_72_kazamon");
    Controller boss = g.Player2, player = g.Player1;
    Check("K1 Steelskin is passive (started at match start, never an option)", IsPassive(boss, "DALA_BOSS_72p") && boss.Hero.HeroPower[EncounterRules.SelfDamageImmune] == 1);
    ToBoss(g);
    var his = Board(g, boss, Yeti);
    int bossHealth = boss.Hero.Health, playerHealth = player.Hero.Health;
    Check("K2 the boss's Hellfire deals 3 to every character but its own hero", Cast(g, Hellfire) && boss.Hero.Health == bossHealth && player.Hero.Health == playerHealth - 3 && his.Damage == 3,
        "boss " + (bossHealth - boss.Hero.Health) + " player " + (playerHealth - player.Hero.Health));
    boss.Hero.Armor = 4;
    Check("K3 its Kobold Librarian (2 damage to its own hero) costs it nothing, Armor included", Cast(g, Librarian) && boss.Hero.Health == bossHealth && boss.Hero.Armor == 4);
    Check("K4 its own Fireball at its own hero deals nothing", Cast(g, Fireball, boss.Hero) && boss.Hero.Health == bossHealth);
    ToPlayer(g);
    boss.Hero.Armor = 0;
    Check("K5 the run player's Fireball hurts it as usual (6)", Cast(g, Fireball, boss.Hero) && boss.Hero.Health == bossHealth - 6, (bossHealth - boss.Hero.Health).ToString());
    ToBoss(g);
    EmptyDeck(boss);
    bossHealth = boss.Hero.Health;
    Round(g);
    Check("K6 Fatigue is the game's rule, not its source: it still hurts", boss.Hero.Health < bossHealth, bossHealth + " -> " + boss.Hero.Health);
    Check("K7 the rule is the power's: the run player's own self-damage is not prevented", EncounterRules.PreventsSelfDamage(boss.Hero, boss.Hero.HeroPower)
        && !EncounterRules.PreventsSelfDamage(player.Hero, player.Hero.HeroPower) && !EncounterRules.PreventsSelfDamage(boss.Hero, player.Hero.HeroPower));
});

// ============================================================ L  Kara Stamper: Soul Weave
Guard("Kara", () =>
{
    var g = Fight("dala_74_kara");
    Controller boss = g.Player2;
    ToBoss(g);
    EmptyHand(boss);
    bool none = !Offered(g);
    var yeti = ToHand(g, boss, Yeti);
    int board = boss.BoardZone.Count;
    Check("L1 Soul Weave costs (3) and is offered only while the boss holds a card", Cost("DALA_BOSS_74p") == 3 && none && Offered(g));
    Check("L2 it discards the card (the only one) and summons a random minion of its Cost (4)", Power(g) && yeti.Zone == boss.GraveyardZone && boss.HandZone.Count == 0
        && boss.BoardZone.Count == board + 1 && boss.BoardZone.Last().Card.Cost == 4, string.Join(",", boss.BoardZone.Select(m => m.Card.Id + ":" + m.Card.Cost)));
    Round(g);
    ToHand(g, boss, Moonfire); FillBoard(g, boss);
    Check("L3 with the boss's board full it is not offered", !Offered(g));
});

// ============================================================ N  Magistrix Norroa: Kaironomaly
Guard("Norroa", () =>
{
    var g = Fight("dala_43_norroa");
    Controller boss = g.Player2, player = g.Player1;
    Check("N1 Kaironomaly is passive; the run player's first turn runs under a 25-second timer (TIMEOUT), the boss's under the ordinary one",
        IsPassive(boss, "DALA_BOSS_43p") && player.TimeOut == 25 && EncounterRules.ShortTurnSeconds(player) == 25 && EncounterRules.ShortTurnSeconds(boss) == 0, player.TimeOut + "/" + boss.TimeOut);
    EndTurn(g); EndTurn(g);
    Check("N2 every turn of the run player's starts under it", player.TimeOut == 25 && g.CurrentPlayer == player);
    EndTurn(g);
    player.TimeOut = 15;
    EndTurn(g);
    Check("N3 a shorter timer stays (Nozdormu the Eternal's 15 is not lengthened to 25)", player.TimeOut == 15 && EncounterRules.ShortTurnSeconds(player) == 15);
    var plain = Fight("dala_70_smiggs");
    Check("N4 without the power the timer is the ordinary one (no short turn)", EncounterRules.ShortTurnSeconds(plain.Player1) == 0 && plain.Player1.TimeOut == 75, plain.Player1.TimeOut.ToString());
    Check("N5 a clone carries the timer", g.Clone().Player1.TimeOut == 15);
    // Classic Nozdormu (EX1_560: a CONTROLLERS aura TIMEOUT SET -55, so TIMEOUT reads 20 with it) is no short turn of its own,
    // and it does not cancel Kaironomaly's 25 (the rule reads the base tag an effect wrote, never the aura).
    var classic = Fight("dala_70_smiggs");
    Board(classic, classic.Player1, "EX1_560");
    Check("N6 classic Nozdormu alone gives no short turn (its aura is not a timer NativeMatch enforces)", classic.Player1.TimeOut == 20 && EncounterRules.ShortTurnSeconds(classic.Player1) == 0
        && EncounterRules.ShortTurnSeconds(classic.Player2) == 0, classic.Player1.TimeOut + " -> " + EncounterRules.ShortTurnSeconds(classic.Player1));
    var g2 = Fight("dala_43_norroa");
    Board(g2, g2.Player1, "EX1_560");
    EndTurn(g2); EndTurn(g2);
    Check("N7 with classic Nozdormu in play Norroa's 25 seconds hold for the run player's turn", g2.CurrentPlayer == g2.Player1 && EncounterRules.ShortTurnSeconds(g2.Player1) == 25
        && EncounterRules.ShortTurnSeconds(g2.Player2) == 0, EncounterRules.ShortTurnSeconds(g2.Player1).ToString());
    var eternal = Fight("dala_70_smiggs");
    eternal.Player1.TimeOut = 15;
    Board(eternal, eternal.Player1, "EX1_560");
    Check("N8 Nozdormu the Eternal's 15 (the base tag MainBegin writes) is unchanged, with or without classic Nozdormu's aura", EncounterRules.ShortTurnSeconds(eternal.Player1) == 15);
});

// ============================================================ O  Archmage Vargoth: Arcane Runes
Guard("Vargoth", () =>
{
    var g = Fight("dala_67_vargoth");
    Controller boss = g.Player2;
    ToBoss(g);
    EmptyHand(boss);
    var seen = new HashSet<string>(StringComparer.Ordinal);
    int uses = 0;
    for (int i = 0; i < 24; i++)
    {
        EmptyHand(boss);
        if (Power(g)) uses++;
        foreach (var p in boss.HandZone) seen.Add(p.Card.Id);
        EmptyHand(boss);
        Round(g);
    }
    Check("O1 Arcane Runes costs (0) and adds one Arcane spell of the wiki's five each use; 24 uses meet at least four of them and nothing else",
        Cost("DALA_BOSS_67p") == 0 && uses == 24 && seen.All(id => DungeonRunEncounters.ArcaneRunes.Contains(id)) && seen.Count >= 4, string.Join(",", seen));
    FillHand(g, boss);
    Check("O2 with the boss's hand full it is not offered", !Offered(g));
    Check("O3 the five are scripted and admitted for a boss", DungeonRunEncounters.ArcaneRunesPool(g, boss).Count == 5
        && DungeonRunEncounters.ArcaneRunes.All(id => DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss, null, catalogRun: true).Admitted));
});

// ============================================================ P  Archmage Kalec: Dragonwrath
Guard("Kalec", () =>
{
    var g = Fight("dala_69_kalec");
    Controller boss = g.Player2, player = g.Player1;
    ToBoss(g);
    Check("P1 Dragonwrath costs (2) and gives the boss's hero Spell Damage +1", Cost("DALA_BOSS_69p") == 2 && Power(g) && boss.CurrentSpellPower == 1 && player.CurrentSpellPower == 0);
    int health = player.Hero.Health;
    Check("P2 the boss's Moonfire then deals 2", Cast(g, Moonfire, player.Hero) && player.Hero.Health == health - 2);
    Round(g);
    Check("P3 a second use stacks: Spell Damage +2, kept across turns", Power(g) && boss.CurrentSpellPower == 2);
    Round(g);
    Check("P4 still +2 two turns later; a clone carries it", boss.CurrentSpellPower == 2 && g.Clone().Player2.CurrentSpellPower == 2);
});

// ============================================================ Q  Archmage Khadgar: Summon Elemental, Kirin Tor Elemental
Guard("Khadgar", () =>
{
    var g = Fight("dala_60_khadgar");
    Controller boss = g.Player2, player = g.Player1;
    ToBoss(g);
    Check("Q1 Summon Elemental costs (3) and summons a 2/3 Kirin Tor Elemental", Cost("DALA_BOSS_60p") == 3 && Power(g) && boss.BoardZone.Count(m => m.Card.Id == Elemental) == 1
        && boss.BoardZone.First(m => m.Card.Id == Elemental) is Minion e && e.AttackDamage == 2 && e.Health == 3);
    int health = player.Hero.Health;
    Check("Q2 the boss's Moonfire is cast an additional time on the same target (2 damage)", Cast(g, Moonfire, player.Hero) && player.Hero.Health == health - 2, (health - player.Hero.Health).ToString());
    Board(g, boss, Elemental);
    health = player.Hero.Health;
    Check("Q3 with two Elementals each adds one cast (3 damage; a copy never casts again)", Cast(g, Moonfire, player.Hero) && player.Hero.Health == health - 3, (health - player.Hero.Health).ToString());
    Check("Q4 a Secret is not cast twice (one Ice Barrier in play)", Cast(g, IceBarrier) && boss.SecretZone.Count == 1);
    EmptyHand(boss);
    int hand = boss.HandZone.Count, deck = boss.DeckZone.Count;
    bool cast = Cast(g, Arcane);
    Check("Q5 an untargeted spell copies too (Arcane Intellect three times: six cards)", cast && boss.HandZone.Count == hand + 6 && boss.DeckZone.Count == deck - 6,
        cast + " hand " + (boss.HandZone.Count - hand) + " deck " + deck + "->" + boss.DeckZone.Count + " " + string.Join(",", boss.HandZone.Select(p => p.Card.Id)));
    ToPlayer(g);
    int bossHealth = boss.Hero.Health;
    Check("Q6 the run player's spells are not copied (its Moonfire deals 1)", Cast(g, Moonfire, boss.Hero) && boss.Hero.Health == bossHealth - 1);
    Board(g, player, Elemental);
    bossHealth = boss.Hero.Health;
    Check("Q7 an Elemental the run player controls copies that player's spells", Cast(g, Moonfire, boss.Hero) && boss.Hero.Health == bossHealth - 2);
    var silenced = boss.BoardZone.First(m => m.Card.Id == Elemental);
    new SilenceTask(EntityType.TARGET).Process(g, player, player.Hero, silenced, null); g.DeathProcessingAndAuraUpdate();
    foreach (var m in boss.BoardZone.Where(m => m.Card.Id == Elemental && m != silenced).ToList()) { m.Damage = m.Health; }
    g.DeathProcessingAndAuraUpdate();
    ToBoss(g);
    health = player.Hero.Health;
    Check("Q8 a silenced Elemental copies nothing", Cast(g, Moonfire, player.Hero) && player.Hero.Health == health - 1, (health - player.Hero.Health).ToString());
});

// ============================================================ R  the Heist finale on the fixture schedule, and DALA_830 The Finale
Guard("Finale", () =>
{
    string[] fixedBosses = { "dala_43_norroa", "dala_67_vargoth", "dala_69_kalec", "dala_60_khadgar" };
    var shipped = data.Schedules.Find("acts-v3h");
    var rule = data.Schedules.Rule("DALA_830");
    // The close-out (family 25) ships the finale as this family built it on the fixture (which is now the shipped file).
    Check("R1 the shipped acts-v3h ends with the Heist finale (the close-out: four acts, the four bosses fixed) and DALA_830 is an admitted twist with effect run, in the finale's twist pool only",
        shipped.FinaleEnabled && shipped.Acts.Count == 4 && shipped.Acts[3].IsFinale && shipped.Acts[3].FixedBosses.SequenceEqual(fixedBosses) && rule != null && rule.Kind == DungeonRunGlobalRule.KindTwist
        && rule.Effect == DungeonRunGlobalRule.EffectRun && rule.Admitted && !DungeonRunGlobalRules.IsScripted("DALA_830")
        && data.Schedules.Schedules.Values.SelectMany(x => x.Acts.Where(a => a.TwistPool.Contains("DALA_830")).Select(a => x.Id + ":" + a.IsFinale)).OrderBy(x => x).SequenceEqual(new[] { "acts-v3h:True" })
        && DungeonRunAdmission.Classify(C("DALA_830"), "DALA_830", AdmitScope.Rule).Admitted && !DungeonRunAdmission.Classify(C("DALA_830"), "DALA_830", AdmitScope.Player).Admitted);
    var fx = Load(fixtureSchedule);
    var fxErrors = DungeonRunValidator.Validate(fx, null);
    var v3 = fx.Schedules.Find("acts-v3h");
    var finale = v3.Acts.LastOrDefault();
    // Until the close-out the one error was the released-shape pin (digest 09B43E71F9440039 against FF4FEC05953AA0DE); the close-out
    // re-pinned ReleasedShapes[3] to this shape, so the fixture (the shipped file byte for byte since then) validates cleanly.
    Check("R2 the fixture (the shipped schedule since the close-out) validates; acts-v3h's shape is the released one, 09B43E71F9440039; it ends with the finale act: the four bosses in order, its twist DALA_830 at 100",
        fxErrors.Count == 0 && fixtureSchedule == R("dungeon-run/schedule-v1.json") && DungeonRunSchedules.ShapeDigest(v3) == "09B43E71F9440039"
        && DungeonRunSchedules.ReleasedShapes[3]["acts-v3h"] == "09B43E71F9440039" && v3.Acts.Count == 4 && finale != null && finale.IsFinale && finale.FixedBosses.SequenceEqual(fixedBosses) && finale.TwistPool.SequenceEqual(new[] { "DALA_830" })
        && finale.TwistChance == 100 && finale.TavernAtStart && v3.FloorCount == 19, string.Join(" | ", fxErrors.Take(3)));
    var bad = Load(fixtureSchedule.Replace("\"twist\": {\"pool\": [\"DALA_830\"], \"chance\": 100}", "\"twist\": {\"pool\": [\"DALA_850\"], \"chance\": 100}"));
    var badErrors = DungeonRunValidator.Validate(bad, null);
    Check("R3 the validator checks the finale's twist as an act's: an anomaly named there is refused", badErrors.Any(e => e.Contains("twist pool names DALA_850")), string.Join(" | ", badErrors.Take(2)));
    var frun = new DungeonRun(fx);
    int withTwist = 0, inOrder = 0, neverEarly = 0;
    for (long seed = 1; seed <= 40; seed++)
    {
        var cls = fx.Class("MAGE");
        var s = frun.NewRun(all, cls.Id, cls.Decks[0], cls.Powers[0], 7000 + seed, schedule: "acts-v3h");
        if (s.ActiveRules.Count(r => r.Id == "DALA_830" && r.Kind == DungeonRunGlobalRule.KindTwist && r.Act == 4 && r.FromNode == -1) == 1
            && s.ActiveRules.Count(r => r.Kind == DungeonRunGlobalRule.KindTwist) == 1) withTwist++;
        if (s.BossSequence.Count == 19 && s.BossSequence.Skip(15).SequenceEqual(fixedBosses)) inOrder++;
        if (!s.BossSequence.Take(15).Any(fixedBosses.Contains)) neverEarly++;
    }
    Check("R4 40 new runs on the fixture: each rolls DALA_830 as the finale act's twist (chance 100) and no other twist", withTwist == 40, withTwist + " of 40");
    Check("R5 the four fixed bosses are floors 16-19 in order, and never drawn on floors 1-15", inOrder == 40 && neverEarly == 40, inOrder + "/" + neverEarly);
    var shippedRun = run.NewRun(all, data.Classes[0].Id, data.Classes[0].Decks[0], data.Classes[0].Powers[0], 7001, schedule: "acts-v3h");
    Check("R6 a run on the shipped acts-v3h has 19 floors, ending with the four finale bosses, and holds DALA_830 for act IV (the close-out)", shippedRun.BossSequence.Count == 19
        && shippedRun.ActiveRules.Count(r => r.Id == "DALA_830" && r.Act == 4) == 1 && shippedRun.BossSequence.Skip(15).SequenceEqual(fixedBosses));
    // Walk a fixture run to floor 16 (the finale's first fight), as the bosspilot harness does.
    var cls0 = fx.Class("MAGE");
    var w = frun.NewRun(all, cls0.Id, cls0.Decks[0], cls0.Powers[0], 7100, schedule: "acts-v3h");
    var scratch = new DungeonRunProfile();
    var phasesSeen = new HashSet<DungeonRunPhase>();
    while (!(w.Floor == 16 && w.Phase == DungeonRunPhase.PreMatch))
    {
        phasesSeen.Add(w.Phase);
        switch (w.Phase)
        {
            case DungeonRunPhase.PreMatch: frun.BeginMatch(w); frun.RecordOutcome(w, scratch, DungeonRunOutcome.Win); break;
            case DungeonRunPhase.TreasureDraft: frun.PickTreasure(w, w.OpenDraft.Offered[0]); break;
            case DungeonRunPhase.Modifier: frun.PickModifier(w, w.ModifierOffer.Kind == DungeonRunGlobalRule.KindAnomaly ? DungeonRunGlobalRule.None : w.ModifierOffer.Offered[0]); break;
            case DungeonRunPhase.Tavern: frun.LeaveTavern(w); break;
            case DungeonRunPhase.BundleDraft:
                if (w.OpenDraft.Takes == 0) { frun.PickBundle(w, w.OpenDraft.Offered[0]); break; }
                string bundle = w.OpenDraft.Choosing ?? w.OpenDraft.Offered[0];
                var left = frun.OfferCards(w.OpenDraft, bundle);
                foreach (string kept in w.OpenDraft.Chosen) left.Remove(kept);
                frun.PickDraftCard(w, bundle, left[0]);
                break;
            default: throw new Exception("walk stopped at " + w.Phase);
        }
    }
    var active = frun.ActiveGlobalRules(w);
    var plan = frun.BuildPlan(w, all);
    Check("R7 at the finale's first fight (after its tavern) DALA_830 is the act's active rule, the boss is Norroa, and the rule never reaches the match plan (effect run)",
        frun.CurrentNode(w).Act == 4 && active.Any(r => r.Id == "DALA_830") && plan.EncounterId == "dala_43_norroa" && !plan.GlobalRules.Contains("DALA_830")
        && phasesSeen.Contains(DungeonRunPhase.Tavern), "act " + frun.CurrentNode(w).Act + " rules " + string.Join(",", active.Select(r => r.Id)) + " plan " + plan.EncounterId);
    Check("R8 the finale fight's Health is the finale's curve (70) and its deck the row's own count (35 cards)", plan.BossHealth == 70 && plan.BossDeck.Count == 35 && plan.EncounterDeck,
        plan.BossHealth + " / " + plan.BossDeck.Count);
});

// ============================================================ M  the rows, the admission rows, the rejections, the validator
Guard("Rows", () =>
{
    var errors = DungeonRunValidator.Validate(data, null);
    Check("M1 the shipped data validates with the sixteen family 18 rows", errors.Count == 0, string.Join(" | ", errors.Take(4)));
    var rows = new (string id, int n, string name, string tier, int level, int printed, string kind, int deck, string[] flags)[]
    {
        ("dala_61_saelorn", 61, "Sael'orn", "3", 6, 30, "authentic", 30, new[] { "hiddenZone" }),
        ("dala_62_haro", 62, "Haro Setting-Sun", "3", 6, 30, "authentic", 30, new[] { "bothPlayers" }),
        ("dala_63_dagg", 63, "Dagg Cruelmight", "2", 4, 30, "extended", 30, new string[0]),
        ("dala_64_biggs", 64, "Banker Biggs", "3", 6, 30, "substituted", 30, new string[0]),
        ("dala_65_valdera", 65, "Valdera Highborne", "3", 6, 30, "authentic", 30, new[] { "passive", "damageCap" }),
        ("dala_66_marei", 66, "Marei Loom", "1", 1, 10, "extended", 30, new[] { "bothPlayers", "hiddenZone" }),
        ("dala_68_lilayell", 68, "Lilayell Suntear", "3", 6, 30, "authentic", 30, new[] { "passive" }),
        ("dala_70_smiggs", 70, "Sky Captain Smiggs", "3", 6, 30, "authentic", 30, new[] { "bothPlayers" }),
        ("dala_71_anarii", 71, "Anarii Duskgrove", "3", 6, 30, "authentic", 30, new string[0]),
        ("dala_72_kazamon", 72, "Kazamon Steelskin", "3", 6, 30, "authentic", 30, new[] { "passive" }),
        ("dala_73_hesutu", 73, "Hesutu Stonewind", "2", 3, 30, "extended", 30, new string[0]),
        ("dala_74_kara", 74, "Kara Stamper", "3", 6, 30, "authentic", 30, new string[0]),
        ("dala_43_norroa", 43, "Magistrix Norroa", "finale", 9, 30, "authentic", 35, new[] { "passive" }),
        ("dala_67_vargoth", 67, "Archmage Vargoth", "finale", 10, 30, "authentic", 35, new string[0]),
        ("dala_69_kalec", 69, "Archmage Kalec", "finale", 11, 30, "authentic", 40, new string[0]),
        ("dala_60_khadgar", 60, "Archmage Khadgar", "finale", 12, 30, "authentic", 40, new[] { "recast" }),
    };
    Check("M2 family 18 holds exactly these sixteen rows", data.Encounters.Where(b => b.Family == "18").Select(b => b.Id).OrderBy(i => i, StringComparer.Ordinal)
        .SequenceEqual(rows.Select(r => r.id).OrderBy(i => i, StringComparer.Ordinal)), string.Join(",", data.Encounters.Where(b => b.Family == "18").Select(b => b.Id)));
    // The passive powers and Marei Loom's Next..., cast by rule since the close-out review (family 09's AutoCast registers it as a passive).
    string[] passives = { "DALA_BOSS_65p", "DALA_BOSS_66p", "DALA_BOSS_68p", "DALA_BOSS_72p", "DALA_BOSS_43p" };
    foreach (var r in rows)
    {
        var b = data.Boss(r.id);
        var plan = RowPlan(r.id);
        string hero = "DALA_BOSS_" + r.n + "h", power = "DALA_BOSS_" + r.n + "p";
        bool passive = passives.Contains(power);
        Check("M3 " + r.id + ": a Heist row of tier " + r.tier + " (level " + r.level + ", printed Health " + r.printed + ") whose plan is the row's (hero, " + (passive ? "passive" : "clickable") + " power, a "
            + r.deck + "-card " + r.kind + " deck, the curve's Health), flags [" + string.Join(",", r.flags) + "]",
            b.Provenance == "DALA" && b.Name == r.name && b.Tier == r.tier && b.HeroCard == hero && b.Power == power && b.Source.Kind == r.kind && b.Flags.OrderBy(f => f).SequenceEqual(r.flags.OrderBy(f => f))
            && b.LevelOriginal == r.level && plan.EncounterId == r.id && plan.EncounterDeck && plan.BossHero.Id == hero && plan.BossHeroPowerId == power && plan.BossDeck.Count == r.deck
            && data.BossDecks[b.Deck].Count == r.deck && !b.FixedHealth && b.Health == null && C(hero).Type == CardType.HERO && C(hero).Health == r.printed
            && C(power).Type == CardType.HERO_POWER && DungeonRunEncounters.PassivePowers.Contains(power) == passive
            && DungeonRunEncounters.Find(power)?.Kind == (passive ? EncounterCardKind.Passive : EncounterCardKind.Power) && C(hero).Name == r.name
            && b.Pools.SequenceEqual(new[] { DungeonRunEncounterData.PoolOf(r.tier) }) && b.Source.Url.StartsWith("https://hearthstone.wiki.gg/wiki/") && !b.BossGeneratedSecrets && b.Forms.Count == 0 && b.Start == null,
            b.Source.Kind + " deck " + data.BossDecks[b.Deck].Count + " flags " + string.Join(",", b.Flags) + " tier " + b.Tier + " level " + b.LevelOriginal);
    }
    Check("M4 the four finale rows are tier finale (pool finale only): the combat and major slots never draw them; the twelve others sit in the combat tiers",
        rows.Where(r => r.tier == "finale").All(r => data.Boss(r.id).Pools.SequenceEqual(new[] { "finale" }) && !data.Schedules.Member(data.Boss(r.id), "mix-6"))
        && rows.Where(r => r.tier != "finale").All(r => data.Schedules.Schedules["acts-v3h"].Acts.SelectMany(a => a.Combat).Any(slot => data.Schedules.Member(data.Boss(r.id), slot.Pool))));
    string[] powers = rows.Select(r => "DALA_BOSS_" + r.n + "p").ToArray();
    string[] cards = { Elemental };
    string[] enchantments = { Darkened, Gains, Rancor };
    var scripted = powers.Concat(cards).ToArray();
    var unscripted = scripted.Concat(enchantments).Where(id => !C(id).Implemented || DungeonRunEncounters.Find(id)?.Family != "18").ToList();
    var retexted = scripted.Concat(enchantments).Where(id => C(id).Text.Replace(" @spelldmg", "") != factById[id].GetProperty("text").GetString()).ToList();
    Check("M5 every power, the boss card and the three enchantments are scripted by family 18 and keep the client's text (16 powers, 1 card, 3 enchantments)", unscripted.Count == 0 && retexted.Count == 0
        && scripted.Length == 17, string.Join(",", unscripted.Concat(retexted.Select(id => id + " [" + C(id).Text + "] vs [" + factById[id].GetProperty("text").GetString() + "]"))));
    var wrongScope = scripted.Where(id =>
    {
        var v = DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss, null, catalogRun: true);
        return !(v.Admitted && v.Subject && v.Rule?.Ids != null && v.Rule.Ids.Contains(id) && DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss).Admitted
            && DungeonRunAdmission.Classify(C(id), id, AdmitScope.Player, null, catalogRun: true).Admitted == cards.Contains(id)
            && !DungeonRunAdmission.Classify(C(id), id, AdmitScope.Generated, null, catalogRun: true).Admitted
            && DungeonRunAdmission.GeneratedRejects(true).Contains(id) && DungeonRunAdmission.GeneratedRejects(false).Contains(id));
    }).ToList();
    Check("M6 each has its own admission row: the sixteen powers a boss's only, Kirin Tor Elemental a boss's and (taken from a boss) the run player's; none is ever generated", wrongScope.Count == 0, string.Join(",", wrongScope));
    var families = scripted.Select(id => DungeonRunAdmission.RuleFor(id)?.Family).Distinct().OrderBy(f => f, StringComparer.Ordinal).ToList();
    Check("M7 the admission families: boss-public-power (8), boss-own-hand-power (2), boss-passive-trigger (4), boss-hidden-reveal (1), boss-hidden-shuffle (1), boss-card (1)",
        families.SequenceEqual(new[] { "boss-card", "boss-hidden-reveal", "boss-hidden-shuffle", "boss-own-hand-power", "boss-passive-trigger", "boss-public-power" })
        && powers.Count(id => DungeonRunAdmission.RuleFor(id).Family == "boss-public-power") == 8 && powers.Count(id => DungeonRunAdmission.RuleFor(id).Family == "boss-passive-trigger") == 4
        && powers.Count(id => DungeonRunAdmission.RuleFor(id).Family == "boss-own-hand-power") == 2, string.Join(",", families));
    var bolan = data.RejectedEncounters.FirstOrDefault(x => x.Id == "DALA_BOSS_75h");
    Check("M8 Commander Bolan is rejected (no row; the rejected list states the data gap and its lift); his power has an admission row that admits nothing",
        !data.Bosses.Any(b => b.HeroCard == "DALA_BOSS_75h") && bolan != null && bolan.Power == "DALA_BOSS_75p" && bolan.Reason.Contains("no text") && bolan.Lift.StartsWith("none: a data gap")
        && DungeonRunAdmission.RuleFor("DALA_BOSS_75p")?.Admit == AdmitScope.None && !C("DALA_BOSS_75p").Implemented && DungeonRunEncounters.Find("DALA_BOSS_75p") == null);
    Check("M9 the two unused cards (Phase Spider, Cheerful Spirit) are rejected: unscripted, an admission row that admits nothing, in no deck",
        new[] { "DALA_BOSS_61t", "DALA_501" }.All(id => !C(id).Implemented && DungeonRunAdmission.RuleFor(id)?.Family == "unused-card" && DungeonRunAdmission.RuleFor(id).Admit == AdmitScope.None
            && !data.BossDecks.Values.Any(d => d.Contains(id))));
    string Subs(string id) => string.Join(",", data.Boss(id).Source.Substitutions.Where(x => x.Listed != null).Select(x => x.Listed + ">" + (x.Used ?? "-") + ":" + x.Reason).Distinct().OrderBy(x => x, StringComparer.Ordinal));
    Check("M10 the listed cards replaced are only cards the engine does not script, each recorded: Biggs's two Ogre Brutes, Hesutu's Whirlwind Tempest",
        Subs("dala_64_biggs") == "GVG_065>-:unscripted" && Subs("dala_73_hesutu") == "DAL_742>-:unscripted"
        && rows.Where(r => r.id != "dala_64_biggs" && r.id != "dala_73_hesutu").All(r => Subs(r.id) == "")
        && rows.SelectMany(r => data.Boss(r.id).Source.Substitutions).Where(x => x.Reason == "unscripted").All(x => !C(x.Listed).Implemented),
        Subs("dala_64_biggs") + " | " + Subs("dala_73_hesutu"));
    int In(string boss, string card) => data.BossDecks[data.Boss(boss).Deck].Count(id => id == card);
    Check("M11 Khadgar's deck holds the wiki's two Archmage's Apprentices (family 09's card); the four finale decks keep the wiki's counts (35, 35, 40, 40)",
        In("dala_60_khadgar", "KARA_00_08") == 2 && C("KARA_00_08").Implemented && data.Boss("dala_60_khadgar").Cards.Contains(Elemental)
        && data.Boss("dala_67_vargoth").Cards.OrderBy(i => i, StringComparer.Ordinal).SequenceEqual(DungeonRunEncounters.ArcaneRunes.OrderBy(i => i, StringComparer.Ordinal)));
    var lines = File.ReadAllLines(VerifyPaths.Repo("plugin/NativeMatch.cs"));
    Check("M12 the client's match reads the short turn timer from the engine rule (NativeMatch: EncounterRules.ShortTurnSeconds, no fixed 15 left)",
        lines.Any(l => l.Contains("EncounterRules.ShortTurnSeconds(Game.CurrentPlayer)")) && !lines.Any(l => l.Contains("[GameTag.TIMEOUT] == 15")));
});

Console.WriteLine($"RESULT {passes} passed, {failures} failed");
return failures == 0 ? 0 : 1;
