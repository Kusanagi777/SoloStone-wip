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

// Phase 8 family 15, Dalaran Heist bosses B (real engine games through the run setup path: DungeonRun.BuildPlan on the shipped
// row -> DungeonRunSetup.Configure / ApplyAfterStart): Alchemist Wendy (Equivalent Exchange, cast by rule), Tala Stonerage (Twin
// Paths), Disidra Stormglory (Totemic Summons, cast by rule), Linzi Redgrin (Lil' Eviscerate), Vas'no (Stormswell), Bookmaster
// Bae Chao (Shhh!), Sharky McFin (Sharkbite), Ranger Ar'ha (Ar'ha's Call), Jepetto Joybuzz (Assembly, Hoard of Toys), Dalaran
// Fountain Golem (Cold Water), Rasil Fireborne (Masterpiece!), Xur'ios (Scroll Savvy), Mama Diggs (Excavate) and The Rat King
// (A Tale of Kings, the Underbelly Rat's hero replacement), and the rows, admission rows and validator.
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

const string Yeti = "CS2_182", Croc = "CS2_120", Raptor = "CS2_172", Moonfire = "CS2_008", Fireball = "CS2_029", Wisp = "CS2_231";
const string Shieldbearer = "EX1_405", Engineer = "EX1_015", Roots = "AT_037", Sapling = "AT_037t", Bolt = "EX1_238", EarthElemental = "EX1_250";
const string Hellfire = "CS2_062", Rat = "DALA_BOSS_30t", Hoard = "DALA_BOSS_25t", Toy = "DALA_BOSS_25e", Shrine = "TRLA_154";
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
void FillHand(Game g, Controller c, string id = Yeti) { while (!c.HandZone.IsFull) Generic.AddHandPhase.Invoke(c, Entity.FromCard(c, C(id))); }
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
int Count(Controller c, string id) => c.BoardZone.Count(m => m.Card.Id == id);
bool Over(Game g) => g.State == State.COMPLETE;
string Hero(Controller c) => c.Hero.Card.Id + "/" + c.Hero.HeroPower.Card.Id + " " + c.Hero.Health + "hp";
bool Has(IPlayable p, string enchantmentId) => p.AppliedEnchantments?.Any(e => e.Card.Id == enchantmentId) == true;
void EmptyHand(Controller c) { foreach (var p in c.HandZone.ToArray()) { c.HandZone.Remove(p); c.SetasideZone.Add(p); } }
void EmptyBoard(Controller c) { foreach (var m in c.BoardZone.ToArray()) { c.BoardZone.Remove(m); c.SetasideZone.Add(m); } }
bool Attack(Game g, Minion m, ICharacter target) { m.IsExhausted = false; return g.Process(MinionAttackTask.Any(g.CurrentPlayer, m, target)); }
int Cost(string id) => C(id).Cost;
int Casts(Controller c) => DungeonRunEncounters.AutoCasts(c.Hero.HeroPower);
bool IsPassive(Controller c, string power) => c.Hero.HeroPower.Card.Id == power && c.Hero.HeroPower.IsPassiveHeroPower && c.Hero.HeroPower[EncounterRules.PowerActive] == 1
    && DungeonRunCardsGen.PassiveHeroPowers.Contains(power) && DungeonRunEncounters.PassivePowers.Contains(power);
(Game view, Controller boss, Controller player) FairView(Game g)
{
    var bot = FairBotState.Create(g, g.Player2.PlayerId, 1, DeckStyle.Tempo);
    var view = bot.FingerprintGame;
    var viewBoss = view.Player1.PlayerId == g.Player2.PlayerId ? view.Player1 : view.Player2;
    return (view, viewBoss, viewBoss.Opponent);
}

// ============================================================ A  Alchemist Wendy: Equivalent Exchange (cast by rule)
Guard("Wendy", () =>
{
    var g = Fight("dala_16_wendy");
    Controller boss = g.Player2, player = g.Player1;
    EmptyHand(player); EmptyHand(boss);
    var fireball = ToHand(g, player, Fireball);
    var moonfire = ToHand(g, boss, Moonfire);
    Mana(boss);
    EndTurn(g);
    bool passive = IsPassive(boss, "DALA_BOSS_16p");
    Check("A1 Equivalent Exchange costs (1) and is cast by rule (a passive power, never offered): at the start of the boss's turn, with both hands holding one card, the two change sides",
        Cost("DALA_BOSS_16p") == 1 && passive && !Offered(g) && Casts(boss) == 1 && boss.UsedMana == 1
        && fireball.Controller == boss && fireball.Zone == boss.HandZone && moonfire.Controller == player && moonfire.Zone == player.HandZone,
        "casts " + Casts(boss) + " used " + boss.UsedMana + " fireball " + fireball.Zone?.Type + "/" + fireball.Controller.PlayerId);
    var transfers = g.HistoryFacts.Records.OfType<CardTransferredFact>().Where(f => f.EntityId == fireball.Id || f.EntityId == moonfire.Id).ToList();
    Check("A2 each card moves by the hand-to-hand transfer rule (a CardTransferredFact each, so the side that gave a card knows it)", transfers.Count == 2, transfers.Count.ToString());
    var (view, viewBoss, viewPlayer) = FairView(g);
    Check("A3 the boss's fair view holds the taken card exactly in its own hand; the run player's hand keeps its count",
        viewBoss.HandZone.Any(p => p.Card.Id == Fireball) && viewPlayer.HandZone.Count == player.HandZone.Count, string.Join(",", viewBoss.HandZone.Select(p => p.Card.Id)));
    ToPlayer(g);
    int casts = Casts(boss);
    EmptyHand(player); EmptyHand(boss); ToHand(g, boss, Moonfire);
    Mana(boss);
    EndTurn(g);
    Check("A4 with the run player's hand empty at the start of the boss's turn it is not cast (nothing changes sides)", Casts(boss) == casts && player.HandZone.Count == 0
        && boss.HandZone.All(p => p.Controller == boss), "casts " + Casts(boss) + " vs " + casts);
    ToPlayer(g);
    EmptyHand(player); EmptyHand(boss);
    FillHand(g, player, Yeti); FillHand(g, boss, Moonfire);
    Mana(boss);
    EndTurn(g);
    Check("A5 with both hands full the exchange burns nothing: ten cards each, one of each kind moved", player.HandZone.Count == 10 && boss.HandZone.Count(p => p.Card.Id == Yeti) == 1
        && player.HandZone.Count(p => p.Card.Id == Moonfire) == 1 || player.HandZone.Count == 10 && boss.HandZone.Count == 10 && boss.HandZone.Count(p => p.Card.Id == Yeti) == 1,
        player.HandZone.Count + "/" + boss.HandZone.Count + " yeti at boss " + boss.HandZone.Count(p => p.Card.Id == Yeti));
    var clone = g.Clone();
    Check("A6 a game clone carries the rule-cast count (TAG_SCRIPT_DATA_NUM_2 of the power)", DungeonRunEncounters.AutoCasts(clone.Player2.Hero.HeroPower) == Casts(boss) && Casts(boss) >= 2, Casts(boss).ToString());
    // In the boss's fair view the run player's hand is masked (Unknown stand-ins): the exchange there moves a stand-in, never throws.
    var (view2, viewBoss2, viewPlayer2) = FairView(g);
    int bossCards = viewBoss2.HandZone.Count, playerCards = viewPlayer2.HandZone.Count, unknownBefore = viewPlayer2.HandZone.Count(p => p is Unknown);
    string viewSwap = "ok";
    try { if (!DungeonRunEncounters.ExchangeRandomCards(view2, viewBoss2, viewBoss2.Hero.HeroPower)) viewSwap = "nothing moved"; } catch (Exception e) { viewSwap = e.GetType().Name + ": " + e.Message; }
    Check("A7 the exchange resolves in the boss's fair view too (a masked card of the run player's hand for one of its own); both hands keep their counts",
        viewSwap == "ok" && unknownBefore > 0 && viewBoss2.HandZone.Count == bossCards && viewPlayer2.HandZone.Count == playerCards, viewSwap + " unknown " + unknownBefore);
});

// ============================================================ B  Tala Stonerage: Twin Paths
Guard("Tala", () =>
{
    var g = Fight("dala_17_tala");
    Controller boss = g.Player2, player = g.Player1;
    Check("B1 Twin Paths is a passive power, started at match start; the boss's Choose One cards have both effects, the run player's do not",
        IsPassive(boss, "DALA_BOSS_17p") && boss.ChooseBoth && !player.ChooseBoth);
    ToBoss(g);
    int health = player.Hero.Health;
    var roots = ToHand(g, boss, Roots);
    bool one = boss.Options().Count(o => o is PlayCardTask p && p.Source == roots && p.Target == player.Hero) >= 1;
    Play(g, roots, player.Hero);
    Check("B2 the boss's Living Roots deals 2 damage AND summons two Saplings (no option picked)", player.Hero.Health == health - 2 && Count(boss, Sapling) == 2 && one,
        (health - player.Hero.Health) + " saplings " + Count(boss, Sapling));
    ToPlayer(g);
    int bossHealth = boss.Hero.Health;
    var mine = ToHand(g, player, Roots);
    g.Process(PlayCardTask.Any(player, mine, boss.Hero, -1, 1));
    Check("B3 the run player's Living Roots still makes them choose: the damage only", boss.Hero.Health == bossHealth - 2 && Count(player, Sapling) == 0, (bossHealth - boss.Hero.Health).ToString());
});

// ============================================================ C  Disidra Stormglory: Totemic Summons (cast by rule)
Guard("Disidra", () =>
{
    var g = Fight("dala_18_disidra");
    Controller boss = g.Player2, player = g.Player1;
    var pool = DungeonRunEncounters.TotemPool(g, boss, boss.Hero.HeroPower);
    Check("C1 the Totem pool: scripted Totems of any class, the four basic Totems included", pool.Count >= 8 && pool.All(c => c.IsRace(Race.TOTEM) && c.Implemented && c.Type == CardType.MINION)
        && DungeonRunEncounters.BasicTotems.All(id => pool.Any(c => c.Id == id)), pool.Count + ": " + string.Join(",", pool.Select(c => c.Id)));
    Mana(boss);
    EndTurn(g);
    var totem = boss.BoardZone.LastOrDefault();
    Check("C2 Totemic Summons costs (2) and is cast by rule at the start of the boss's turn: one random Totem is summoned", Cost("DALA_BOSS_18p") == 2 && IsPassive(boss, "DALA_BOSS_18p")
        && !Offered(g) && Casts(boss) == 1 && boss.UsedMana == 2 && boss.BoardZone.Count == 1 && totem != null && totem.Card.IsRace(Race.TOTEM), totem?.Card.Id);
    var kinds = new HashSet<string>();
    for (int i = 0; i < 12; i++) { EmptyBoard(boss); Mana(boss); EndTurn(g); Mana(boss); EndTurn(g); kinds.UnionWith(boss.BoardZone.Select(m => m.Card.Id)); }
    Check("C3 twelve more casts: always a Totem of the pool, of several kinds", kinds.Count >= 4 && kinds.All(id => pool.Any(c => c.Id == id)), string.Join(",", kinds));
    ToPlayer(g);
    while (!boss.BoardZone.IsFull) Board(g, boss, Wisp);
    int casts = Casts(boss);
    Mana(boss);
    EndTurn(g);
    Check("C4 with a full board it is not cast", Casts(boss) == casts && boss.UsedMana == 0, "casts " + Casts(boss));
});

// ============================================================ D  Linzi Redgrin: Lil' Eviscerate
Guard("Linzi", () =>
{
    var g = Fight("dala_19_linzi");
    Controller boss = g.Player2, player = g.Player1;
    var yeti = Board(g, player, Yeti);
    ToBoss(g);
    var targets = PowerTargets(g);
    int health = player.Hero.Health;
    Check("D1 Lil' Eviscerate costs (2) and can be aimed at any character; without Combo it deals 1 damage", Cost("DALA_BOSS_19p") == 2 && targets.Contains(player.Hero) && targets.Contains(yeti)
        && targets.Contains(boss.Hero) && Power(g, player.Hero) && player.Hero.Health == health - 1 && boss.UsedMana == 2, (health - player.Hero.Health).ToString());
    Round(g);
    Cast(g, Wisp);
    health = player.Hero.Health;
    Check("D2 after the boss played a card this turn (Combo) it deals 2", boss.IsComboActive && Power(g, player.Hero) && player.Hero.Health == health - 2, (health - player.Hero.Health).ToString());
});

// ============================================================ E  Vas'no: Stormswell
Guard("Vasno", () =>
{
    var g = Fight("dala_20_vasno");
    Controller boss = g.Player2, player = g.Player1;
    Check("E1 Stormswell is a passive power, started at match start", IsPassive(boss, "DALA_BOSS_20p"));
    ToBoss(g);
    int health = player.Hero.Health;
    Cast(g, Bolt, player.Hero);
    Check("E2 the boss's Lightning Bolt (Overload: 1) is cast twice on the same target: 6 damage; the copy overloads too (a spell cast by an effect overloads, the engine's rule)",
        player.Hero.Health == health - 6 && boss.OverloadOwed == 2,
        (health - player.Hero.Health) + " owed " + boss.OverloadOwed);
    health = player.Hero.Health;
    Cast(g, Moonfire, player.Hero);
    Check("E3 a spell without Overload is cast once", player.Hero.Health == health - 1, (health - player.Hero.Health).ToString());
    var croc = Board(g, player, Croc);
    Cast(g, Bolt, croc);
    health = player.Hero.Health;
    Check("E4 when the first cast kills its target, the copy has no target: it does nothing (Pirate's Mark's locked target)", croc.IsDead || croc.Zone != player.BoardZone, croc.Zone?.Type.ToString());
    ToPlayer(g);
    int bossHealth = boss.Hero.Health;
    Cast(g, Bolt, boss.Hero);
    Check("E5 the run player's Overload spells are cast once", boss.Hero.Health == bossHealth - 3, (bossHealth - boss.Hero.Health).ToString());
});

// ============================================================ F  Bookmaster Bae Chao: Shhh!
Guard("BaeChao", () =>
{
    var g = Fight("dala_22_bae_chao");
    Controller boss = g.Player2, player = g.Player1;
    ToBoss(g);
    Check("F1 Shhh! costs (3) and is not offered while no minion is on a board", Cost("DALA_BOSS_22p") == 3 && !Offered(g));
    var theirs = Board(g, player, Shieldbearer); var mine = Board(g, boss, Shieldbearer);
    Check("F2 it silences every minion of both boards", Power(g) && theirs.IsSilenced && mine.IsSilenced && !theirs.HasTaunt && !mine.HasTaunt && boss.UsedMana == 3);
    Round(g);
    EmptyBoard(boss); EmptyBoard(player);
    var permanent = Board(g, boss, "DALA_833t");
    Check("F3 a permanent (an untouchable Cart) is not a minion it can pick: alone on the boards it is not offered", permanent.Untouchable && !Offered(g));
});

// ============================================================ G  Sharky McFin: Sharkbite
Guard("Sharky", () =>
{
    var g = Fight("dala_23_sharky");
    Controller boss = g.Player2, player = g.Player1;
    var yeti = Board(g, player, Yeti);
    ToBoss(g);
    var croc = Board(g, boss, Croc);
    Check("G1 Sharkbite costs (2) and is not offered while no minion is damaged", Cost("DALA_BOSS_23p") == 2 && !Offered(g));
    yeti.Damage = 1;
    Check("G2 it destroys the one damaged minion; the undamaged ones stay", Power(g) && yeti.IsDead && croc.Zone == boss.BoardZone && boss.UsedMana == 2);
    Round(g);
    var a = Board(g, player, Yeti); var b = Board(g, boss, Yeti);
    a.Damage = 1; b.Damage = 1;
    var picked = new HashSet<string>();
    for (int i = 0; i < 12 && picked.Count < 2; i++)
    {
        var clone = g.Clone();
        clone.Process(HeroPowerTask.Any(clone.CurrentPlayer, null));
        picked.Add(clone.Player1.BoardZone.Any(m => m.Id == a.Id) ? "boss's" : "player's");
        Round(g);
        a.Damage = 1; b.Damage = 1;
    }
    Check("G3 with damaged minions on both boards the one destroyed is picked at random (both sides seen)", picked.Count == 2, string.Join(",", picked));
    var gs = Fight("dala_23_sharky", shrine: Shrine);
    var shrine = ShrineRules.Of(gs.Player1).First();
    ToBoss(gs);
    shrine.Damage = 1;
    bool bit = Power(gs);
    var form = ShrineRules.Of(gs.Player1).FirstOrDefault();
    Check("G4 a damaged run shrine picked goes dormant (as from any destroy effect): its dormant form holds its place", bit && ShrineRules.IsCorpse(shrine) && form != null && form.IsDormant
        && form.Controller == gs.Player1, form?.Card.Id);
    form.Damage = 1;
    Check("G5 a dormant shrine form is never a damaged minion it can pick: with only the (damaged) dormant form on the boards it is not offered", form.Damage == 1 && !Offered(gs));
});

// ============================================================ H  Ranger Ar'ha: Ar'ha's Call
Guard("Arha", () =>
{
    var g = Fight("dala_24_arha", bossDeck: Yeti);
    Controller boss = g.Player2, player = g.Player1;
    ToBoss(g);
    Check("H1 Ar'ha's Call costs (2); it attaches its player enchantment to the boss and is not offered while one waits", Cost("DALA_BOSS_24p") == 2 && Power(g)
        && boss.AppliedEnchantments.Count(e => e.Card.Id == DungeonRunEncounters.ArhasCall) == 1 && BattlecryRules.Additional(boss) == 1 && !Offered(g) && boss.UsedMana == 2);
    Cast(g, Yeti);
    Check("H2 a minion without a Battlecry leaves it waiting", BattlecryRules.Additional(boss) == 1);
    int hand = boss.HandZone.Count;
    var engineer = ToHand(g, boss, Engineer);
    Play(g, engineer);
    Check("H3 the next Battlecry minion resolves its Battlecry one more time (Novice Engineer draws 2), then the enchantment is gone", boss.HandZone.Count == hand + 2
        && BattlecryRules.Additional(boss) == 0 && boss.AppliedEnchantments.All(e => e.Card.Id != DungeonRunEncounters.ArhasCall), (boss.HandZone.Count - hand).ToString());
    hand = boss.HandZone.Count;
    Play(g, ToHand(g, boss, Engineer));
    Check("H4 the Battlecry after it resolves once", boss.HandZone.Count == hand + 1, (boss.HandZone.Count - hand).ToString());
    Round(g);
    Check("H5 offered again next turn", Offered(g));
});

// ============================================================ I  Jepetto Joybuzz: Assembly, Hoard of Toys
Guard("Jepetto", () =>
{
    var g = Fight("dala_25_jepetto", bossDeck: Yeti, playerDeck: Croc);
    Controller boss = g.Player2, player = g.Player1;
    Check("I1 Assembly is a passive power, started at match start; nothing was summoned for the opening hands or the mulligan, one copy for the run player's first draw of the game",
        IsPassive(boss, "DALA_BOSS_25p") && player.BoardZone.Count == 1 && player.BoardZone[0].Card.Id == Croc && Has(player.BoardZone[0], Toy) && boss.BoardZone.Count == 0,
        player.BoardZone.Count + "/" + boss.BoardZone.Count);
    Generic.Draw(player);
    var copy = player.BoardZone.LastOrDefault();
    Check("I2 the run player draws a minion: that player summons a 1/1 copy of it (Toyification)", copy != null && copy.Card.Id == Croc && copy.AttackDamage == 1 && copy.Health == 1 && Has(copy, Toy),
        copy?.Card.Id + " " + copy?.AttackDamage + "/" + copy?.Health);
    EndTurn(g);
    var own = boss.BoardZone.LastOrDefault();
    Check("I3 the boss's own draw of a minion gives it a 1/1 copy too", own != null && own.Card.Id == Yeti && own.AttackDamage == 1 && own.Health == 1 && Has(own, Toy), own?.Card.Id);
    var g2 = Fight("dala_25_jepetto", bossDeck: Moonfire, playerDeck: Moonfire);
    Generic.Draw(g2.Player1);
    EndTurn(g2);
    Check("I4 a drawn spell makes no copy (the run player's first draw and one more, the boss's turn draw)", g2.Player1.BoardZone.Count == 0 && g2.Player2.BoardZone.Count == 0);
    ToPlayer(g);
    while (!player.BoardZone.IsFull) Board(g, player, Wisp);
    int hand = player.HandZone.Count;
    Generic.Draw(player);
    Check("I5 with a full board the draw makes no copy and the card stays in hand", player.BoardZone.Count == 7 && player.HandZone.Count == hand + 1);
    ToBoss(g);
    EmptyBoard(boss);
    Cast(g, Hoard);
    var toys = boss.BoardZone.ToList();
    Check("I6 Hoard of Toys (4) summons 1/1 copies of five random scripted minions", Cost(Hoard) == 4 && toys.Count == 5 && toys.All(m => m.AttackDamage == 1 && m.Health == 1 && Has(m, Toy) && m.Card.Implemented)
        && toys.Select(m => m.Card.Id).Distinct().Count() >= 3, string.Join(",", toys.Select(m => m.Card.Id + " " + m.AttackDamage + "/" + m.Health)));
    EmptyBoard(boss);
    for (int i = 0; i < 4; i++) Board(g, boss, Wisp);
    Cast(g, Hoard);
    Check("I7 with three slots left it summons three", boss.BoardZone.Count == 7 && boss.BoardZone.Count(m => Has(m, Toy)) == 3);
});

// ============================================================ J  Dalaran Fountain Golem: Cold Water
Guard("FountainGolem", () =>
{
    var g = Fight("dala_26_fountain_golem");
    Controller boss = g.Player2, player = g.Player1;
    Check("J1 Cold Water is a passive power, started at match start", IsPassive(boss, "DALA_BOSS_26p"));
    var a = Board(g, player, Yeti); var b = Board(g, player, Croc); var c = Board(g, player, Raptor);
    var guard = Board(g, boss, Croc);
    bool hitMinion = Attack(g, c, guard);
    Check("J2 a minion attacking a boss minion is not frozen", hitMinion && !c.IsFrozen);
    Attack(g, a, boss.Hero);
    Attack(g, b, boss.Hero);
    Check("J3 the first minion that attacks the boss's hero this turn is frozen, the second is not", a.IsFrozen && !b.IsFrozen, a.IsFrozen + "/" + b.IsFrozen);
    Round(g);
    Attack(g, b, boss.Hero);
    Check("J4 next turn the first attacker is frozen again", b.IsFrozen);
});

// ============================================================ K  Rasil Fireborne: Masterpiece!
Guard("Rasil", () =>
{
    var g = Fight("dala_27_rasil");
    Controller boss = g.Player2, player = g.Player1;
    ToBoss(g);
    boss.UsedMana = 6;
    Check("K1 Masterpiece! costs (0): with 4 Mana left it spends all of it and summons a random minion that costs (4)", Cost("DALA_BOSS_27p") == 0 && Power(g)
        && boss.RemainingMana == 0 && boss.BoardZone.Count == 1 && boss.BoardZone[0].Card.Cost == 4 && boss.BoardZone[0].Card.Implemented, boss.BoardZone.FirstOrDefault()?.Card.Id);
    var costs = new HashSet<int>();
    for (int i = 0; i < 6; i++)
    {
        Round(g);
        EmptyBoard(boss);
        boss.UsedMana = 10 - i;
        int left = boss.RemainingMana;
        Power(g);
        if (boss.BoardZone.Count == 1 && boss.BoardZone[0].Card.Cost == left && boss.RemainingMana == 0) costs.Add(left);
    }
    Check("K2 with 0 to 5 Mana left the minion always costs exactly that much", costs.SetEquals(new[] { 0, 1, 2, 3, 4, 5 }), string.Join(",", costs));
    Round(g);
    while (!boss.BoardZone.IsFull) Board(g, boss, Wisp);
    Check("K3 with a full board it is not offered", !Offered(g));
});

// ============================================================ L  Xur'ios: Scroll Savvy
Guard("Xurios", () =>
{
    var g = Fight("dala_28_xurios", bossDeck: Fireball, playerDeck: Fireball);
    Controller boss = g.Player2, player = g.Player1;
    Check("L1 Scroll Savvy is a passive power; the spells of the opening hand keep their Cost", IsPassive(boss, "DALA_BOSS_28p")
        && boss.HandZone.Where(p => p.Card.Id == Fireball).All(p => p.Cost == 4));
    var mine = Generic.Draw(player);
    Check("L2 the text names no player: the run player's drawn spell costs (0) this turn too", mine != null && mine.Card.Id == Fireball && mine.Cost == 0 && Has(mine, DungeonRunEncounters.Savvy));
    EndTurn(g);
    var drawn = boss.HandZone.Last();
    var given = ToHand(g, boss, Fireball);
    Check("L3 the spell the boss draws costs (0) this turn (Savvy); one added to its hand otherwise keeps its Cost", drawn.Card.Id == Fireball && drawn.Cost == 0 && Has(drawn, DungeonRunEncounters.Savvy)
        && given.Cost == 4, drawn.Cost + "/" + given.Cost);
    var more = Generic.Draw(boss);
    Check("L4 a draw in the middle of its turn too", more.Cost == 0);
    EndTurn(g);
    Check("L5 at the end of the turn the spells cost (4) again (the boss's two, and the run player's from its own turn)", drawn.Cost == 4 && more.Cost == 4 && mine.Cost == 4,
        drawn.Cost + "/" + more.Cost + "/" + mine.Cost);
});

// ============================================================ M  Mama Diggs: Excavate
Guard("MamaDiggs", () =>
{
    var g = Fight("dala_29_mama_diggs");
    Controller boss = g.Player2, player = g.Player1;
    var enemy = Board(g, player, EarthElemental);
    ToBoss(g);
    var yeti = Board(g, boss, Yeti);
    Check("M1 Excavate costs (1) and is not offered without a friendly Elemental", Cost("DALA_BOSS_29p") == 1 && !Offered(g));
    var earth = Board(g, boss, EarthElemental);
    var targets = PowerTargets(g);
    Check("M2 it can be aimed only at a friendly Elemental", targets.Count == 1 && targets[0] == earth, string.Join(",", targets.Select(t => t.Card.Id)));
    EmptyHand(boss);
    int deck = boss.DeckZone.Count;
    bool used = Power(g, earth);
    Check("M3 it deals 2 damage to the Elemental, then excavates (a random event)", used && earth.Damage == 2 && boss.UsedMana == 1);
    // Each result directly (the power picks one of the six with the game RNG).
    var power = boss.Hero.HeroPower;
    var got = new Dictionary<int, string>();
    for (int r = 0; r < DungeonRunEncounters.ExcavateResults; r++)
    {
        EmptyHand(boss);
        int before = boss.DeckZone.Count;
        DungeonRunEncounters.Excavated(g, boss, power, r, C(EarthElemental));
        got[r] = string.Join(",", boss.HandZone.Select(p => p.Card.Id)) + " drew " + (before - boss.DeckZone.Count);
        bool ok = r switch
        {
            DungeonRunEncounters.ExcavateNothing => boss.HandZone.Count == 0 && before == boss.DeckZone.Count,
            DungeonRunEncounters.ExcavateRock => boss.HandZone.Count == 1 && boss.HandZone[0].Card.Id == DungeonRunEncounters.SmallRock,
            DungeonRunEncounters.ExcavateDraw => (before - boss.DeckZone.Count) is 1 or 2 && boss.HandZone.Count == before - boss.DeckZone.Count,
            DungeonRunEncounters.ExcavateShaman => boss.HandZone.Count == 1 && boss.HandZone[0].Card.Class == CardClass.SHAMAN && boss.HandZone[0].Card.Implemented,
            DungeonRunEncounters.ExcavateCopy => boss.HandZone.Count == 1 && boss.HandZone[0].Card.Id == EarthElemental,
            _ => boss.HandZone.Count == 1 && DungeonRunEncounters.TogwaggleTreasures.Contains(boss.HandZone[0].Card.Id),
        };
        Check("M4 excavation " + r + " (" + new[] { "nothing", "A Small Rock", "one or two draws", "a random Shaman card", "a copy of the damaged minion", "a Dungeon Run treasure" }[r] + ")", ok, got[r]);
    }
    var seen = new HashSet<string>();
    for (int i = 0; i < 30; i++)
    {
        Round(g);
        earth.Damage = 0;
        EmptyHand(boss);
        int before = boss.DeckZone.Count;
        Power(g, earth);
        seen.Add(boss.HandZone.Count == 0 ? "nothing" : before != boss.DeckZone.Count ? "draw" : boss.HandZone[0].Card.Id == DungeonRunEncounters.SmallRock ? "rock"
            : boss.HandZone[0].Card.Id == EarthElemental ? "copy" : DungeonRunEncounters.TogwaggleTreasures.Contains(boss.HandZone[0].Card.Id) ? "treasure" : "shaman");
    }
    Check("M5 thirty uses: the results vary (at least four of the six seen)", seen.Count >= 4, string.Join(",", seen.OrderBy(x => x)));
    var rock = DungeonRunAdmission.Classify(C(DungeonRunEncounters.SmallRock), DungeonRunEncounters.SmallRock, AdmitScope.Boss, null, catalogRun: true);
    var rockLegacy = DungeonRunAdmission.Classify(C(DungeonRunEncounters.SmallRock), DungeonRunEncounters.SmallRock, AdmitScope.Boss, null);
    Check("M6 A Small Rock is admitted for a boss on catalog runs (run-treasure-boss-find), still the run player's only on legacy runs, never generated",
        rock.Admitted && rock.Family == "run-treasure-boss-find" && !rockLegacy.Admitted && DungeonRunAdmission.GeneratedRejectsBoss(true).Contains(DungeonRunEncounters.SmallRock));
});

// ============================================================ N  The Rat King: A Tale of Kings, Underbelly Rat
Guard("RatKing", () =>
{
    var g = Fight("dala_30_rat_king");
    Controller boss = g.Player2, player = g.Player1;
    ToBoss(g);
    Check("N1 A Tale of Kings costs (3) and summons an Underbelly Rat (10/1)", Cost("DALA_BOSS_30p") == 3 && Power(g) && Count(boss, Rat) == 1 && boss.BoardZone[0].AttackDamage == 10
        && boss.BoardZone[0].Health == 1 && boss.UsedMana == 3);
    var eldest = boss.BoardZone[0];
    var younger = Board(g, boss, Rat);
    var oldHero = boss.Hero;
    ToPlayer(g);
    boss.Hero.Armor = 0;
    boss.Hero.Damage = boss.Hero.BaseHealth - 1;
    Cast(g, Moonfire, boss.Hero);
    Check("N2 the boss's hero dies with rats on its board: the eldest leaves the board without dying and a new Rat King enters with its Health (1 of 1)",
        !Over(g) && boss.Hero != oldHero && boss.Hero.Card.Id == "DALA_BOSS_30h" && boss.Hero.HeroPower.Card.Id == "DALA_BOSS_30p" && boss.Hero.Health == 1 && boss.Hero.BaseHealth == 1
        && eldest.Zone == boss.SetasideZone && !eldest.IsDead && younger.Zone == boss.BoardZone && boss[EncounterRules.FormIndex] == 1 && boss[EncounterRules.FormChanges] == 1,
        Hero(boss) + " " + g.State + " eldest " + eldest.Zone?.Type);
    var clone = g.Clone();
    Cast(clone, Moonfire, clone.Player2.Hero);
    Check("N3 a game clone replays it: the second rat replaces the hero there; the real game is untouched", clone.State != State.COMPLETE && clone.Player2[EncounterRules.FormChanges] == 2
        && Count(clone.Player2, Rat) == 0 && Count(boss, Rat) == 1);
    Cast(g, Moonfire, boss.Hero);
    Check("N4 the second death: the younger rat replaces the hero", !Over(g) && Count(boss, Rat) == 0 && boss[EncounterRules.FormChanges] == 2 && boss.Hero.Health == 1, Hero(boss));
    var end = EncounterRules.Harvest(g);
    Cast(g, Moonfire, boss.Hero);
    Check("N5 with no rat left the hero dies and the boss loses; the harvest counted two form changes", Over(g) && boss.PlayState == PlayState.LOST && end.BossFormChanges == 2, g.State + " " + boss.PlayState);
    // A rat that dies with the hero does not save it.
    var g2 = Fight("dala_30_rat_king");
    Board(g2, g2.Player2, Rat);
    g2.Player2.Hero.Damage = g2.Player2.Hero.BaseHealth - 3;
    Cast(g2, Hellfire);
    Check("N6 a rat that dies in the same blow as the hero does not replace it", g2.State == State.COMPLETE && g2.Player2.PlayState == PlayState.LOST, g2.State + " " + g2.Player2.PlayState);
    // The run player who controls a rat: the hero stays and takes the rat's Health.
    var g3 = Fight("dala_30_rat_king");
    var mine = Board(g3, g3.Player1, Rat);
    var playerHero = g3.Player1.Hero;
    ToBoss(g3);
    g3.Player1.Hero.Armor = 0;
    g3.Player1.Hero.Damage = g3.Player1.Hero.BaseHealth - 1;
    Cast(g3, Moonfire, g3.Player1.Hero);
    Check("N7 a run player who controls a rat: the same hero comes back with the rat's Health and the rat leaves the board", g3.State != State.COMPLETE && g3.Player1.Hero == playerHero
        && g3.Player1.Hero.Health == 1 && mine.Zone == g3.Player1.SetasideZone && g3.Player1.PlayState == PlayState.PLAYING, Hero(g3.Player1) + " " + g3.State);
    // A buffed rat brings its buffed Health.
    var g4 = Fight("dala_30_rat_king");
    var big = Board(g4, g4.Player2, Rat);
    big.Damage = 0;
    Generic.AddEnchantmentBlock(g4, C("CS2_092e"), g4.Player2.Hero, big);
    g4.DeathProcessingAndAuraUpdate();
    int bigMax = big.BaseHealth, bigHealth = big.Health;
    g4.Player2.Hero.Damage = g4.Player2.Hero.BaseHealth - 1;
    Cast(g4, Moonfire, g4.Player2.Hero);
    Check("N8 the rat's Health includes its buffs (Blessing of Kings: 5 of 5)", g4.State != State.COMPLETE && g4.Player2.Hero.Health == bigHealth && g4.Player2.Hero.BaseHealth == bigMax && bigMax == 5,
        Hero(g4.Player2) + " rat " + bigHealth + "/" + bigMax);
    ToBoss(g);
});

// ============================================================ P  the rows, the admission rows, the validator
Guard("Rows", () =>
{
    var errors = DungeonRunValidator.Validate(data, null);
    Check("P1 the shipped data validates with the fourteen family 15 rows", errors.Count == 0, string.Join(" | ", errors.Take(4)));
    var rows = new (string id, string n, string name, string tier, int level, int printed, string kind, string[] flags)[]
    {
        ("dala_16_wendy", "16", "Alchemist Wendy", "1", 1, 30, "extended", new[] { "passive", "hiddenZone" }),
        ("dala_17_tala", "17", "Tala Stonerage", "3", 6, 30, "authentic", new[] { "passive" }),
        ("dala_18_disidra", "18", "Disidra Stormglory", "3", 6, 30, "authentic", new[] { "passive" }),
        ("dala_19_linzi", "19", "Linzi Redgrin", "1", 1, 10, "extended", new string[0]),
        ("dala_20_vasno", "20", "Vas'no", "3", 6, 30, "authentic", new[] { "passive", "recast" }),
        ("dala_22_bae_chao", "22", "Bookmaster Bae Chao", "2", 3, 30, "extended", new string[0]),
        ("dala_23_sharky", "23", "Sharky McFin", "2", 4, 30, "extended", new string[0]),
        ("dala_24_arha", "24", "Ranger Ar'ha", "3", 6, 30, "authentic", new[] { "doubleBattlecry" }),
        ("dala_25_jepetto", "25", "Jepetto Joybuzz", "4", 8, 30, "authentic", new[] { "passive", "bothPlayers", "hiddenZone" }),
        ("dala_26_fountain_golem", "26", "Dalaran Fountain Golem", "3", 6, 30, "authentic", new[] { "passive" }),
        ("dala_27_rasil", "27", "Rasil Fireborne", "3", 6, 30, "authentic", new string[0]),
        ("dala_28_xurios", "28", "Xur'ios", "3", 6, 30, "substituted", new[] { "passive" }),
        ("dala_29_mama_diggs", "29", "Mama Diggs", "2", 3, 40, "extended", new string[0]),
        ("dala_30_rat_king", "30", "The Rat King", "3", 6, 10, "authentic", new[] { "forms" }),
    };
    Check("P2 family 15 holds exactly these fourteen rows", data.Encounters.Where(b => b.Family == "15").Select(b => b.Id).OrderBy(i => i, StringComparer.Ordinal)
        .SequenceEqual(rows.Select(r => r.id).OrderBy(i => i, StringComparer.Ordinal)), string.Join(",", data.Encounters.Where(b => b.Family == "15").Select(b => b.Id)));
    var passives = new[] { "16", "17", "18", "20", "25", "26", "28" };
    foreach (var r in rows)
    {
        var b = data.Boss(r.id);
        var plan = RowPlan(r.id);
        string hero = "DALA_BOSS_" + r.n + "h", power = "DALA_BOSS_" + r.n + "p";
        bool passive = passives.Contains(r.n);
        Check("P3 " + r.id + ": a Dalaran Heist row of tier " + r.tier + " (wiki level " + r.level + "; the hero prints " + r.printed + " Health) whose plan is the row's (hero, "
            + (passive ? "passive" : "clickable") + " power, a 30-card " + r.kind + " deck, the act curve's Health), flags [" + string.Join(",", r.flags) + "]",
            b.Provenance == "DALA" && b.Name == r.name && b.Tier == r.tier && b.HeroCard == hero && b.Power == power && b.Source.Kind == r.kind && b.Flags.OrderBy(f => f).SequenceEqual(r.flags.OrderBy(f => f))
            && plan.EncounterId == r.id && plan.EncounterDeck && plan.BossHero.Id == hero && plan.BossHeroPowerId == power && plan.BossDeck.Count == 30 && data.BossDecks[b.Deck].Count == 30
            && !b.FixedHealth && b.Health == null && plan.BossHealth == 30 && C(hero).Type == CardType.HERO && C(hero).Health == r.printed && C(power).Type == CardType.HERO_POWER
            && DungeonRunEncounters.PassivePowers.Contains(power) == passive && C(hero).Name == r.name
            && data.Schedules.Member(b, DungeonRunEncounterData.PoolOf(b.Tier)) && b.Source.Url.StartsWith("https://hearthstone.wiki.gg/wiki/") && !b.BossGeneratedSecrets
            && b.Forms.Count == (r.n == "30" ? 1 : 0),
            b.Source.Kind + " health " + plan.BossHealth + " flags " + string.Join(",", b.Flags) + " tier " + b.Tier);
    }
    string[] powers = rows.Select(r => "DALA_BOSS_" + r.n + "p").ToArray();
    string[] cards = { Hoard, Rat };
    string[] enchantments = { DungeonRunEncounters.ArhasCall, Toy, DungeonRunEncounters.Savvy };
    var scripted = powers.Concat(cards).ToArray();
    var unscripted = scripted.Concat(enchantments).Where(id => !C(id).Implemented || DungeonRunEncounters.Find(id)?.Family != "15").ToList();
    var retexted = scripted.Concat(enchantments).Where(id => C(id).Text.Replace(" @spelldmg", "") != factById[id].GetProperty("text").GetString()).ToList();
    Check("P4 every power, boss card and enchantment of the family is scripted by family 15 and keeps the client's text (14 powers, 2 cards, 3 enchantments)", unscripted.Count == 0 && retexted.Count == 0
        && scripted.Length == 16, string.Join(",", unscripted.Concat(retexted.Select(id => id + " [" + C(id).Text + "] vs [" + factById[id].GetProperty("text").GetString() + "]"))));
    var wrongScope = scripted.Where(id =>
    {
        var v = DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss, null, catalogRun: true);
        return !(v.Admitted && v.Subject && v.Rule?.Ids != null && v.Rule.Ids.Contains(id) && DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss).Admitted
            && DungeonRunAdmission.Classify(C(id), id, AdmitScope.Player, null, catalogRun: true).Admitted == cards.Contains(id)
            && !DungeonRunAdmission.Classify(C(id), id, AdmitScope.Generated, null, catalogRun: true).Admitted
            && DungeonRunAdmission.GeneratedRejects(true).Contains(id) && DungeonRunAdmission.GeneratedRejects(false).Contains(id));
    }).ToList();
    Check("P5 each has its own admission row: the fourteen powers a boss's only, the two boss cards a boss's and (taken from a boss) the run player's; none is ever generated", wrongScope.Count == 0, string.Join(",", wrongScope));
    var families = scripted.Select(id => DungeonRunAdmission.RuleFor(id)?.Family).Distinct().OrderBy(f => f, StringComparer.Ordinal).ToList();
    Check("P6 the six admission families (the names families 08 and 09 introduced): boss-auto-cast, boss-card, boss-hidden-reveal, boss-passive-trigger, boss-public-power, boss-treasure-find",
        families.SequenceEqual(new[] { "boss-auto-cast", "boss-card", "boss-hidden-reveal", "boss-passive-trigger", "boss-public-power", "boss-treasure-find" }), string.Join(",", families));
    string Subs(string id) => string.Join(",", data.Boss(id).Source.Substitutions.Where(x => x.Listed != null).Select(x => x.Listed + ">" + (x.Used ?? "-") + ":" + x.Reason).Distinct().OrderBy(x => x, StringComparer.Ordinal));
    Check("P7 the listed cards replaced or left out: Fel Reaver (Bae Chao, unscripted), Wee Spellstopper and Glacial Mysteries (Xur'ios: unscripted, not boss-admitted), each recorded",
        Subs("dala_22_bae_chao") == "GVG_016>-:unscripted" && Subs("dala_28_xurios") == "GVG_122>CS2_033:unscripted,ICC_086>CS2_032:admission:unclassified"
        && rows.Where(r => r.id != "dala_22_bae_chao" && r.id != "dala_28_xurios").All(r => Subs(r.id) == ""), Subs("dala_22_bae_chao") + " | " + Subs("dala_28_xurios"));
    int In(string boss, string card) => data.BossDecks[data.Boss(boss).Deck].Count(id => id == card);
    Check("P8 the boss cards are where the wiki lists them: two Hoards of Toys in Jepetto's deck; the Underbelly Rat in no deck (A Tale of Kings summons it; the row lists it as a card)",
        In("dala_25_jepetto", Hoard) == 2 && data.Encounters.All(b => data.BossDecks[b.Deck].All(id => id != Rat)) && data.Boss("dala_30_rat_king").Cards.Contains(Rat));
    var rat = data.Boss("dala_30_rat_king");
    Check("P9 The Rat King's one script form is a new Rat King with A Tale of Kings (the rat's hero replacement enters through it)", rat.Forms.Count == 1 && rat.Forms[0].Script
        && rat.Forms[0].Hero == "DALA_BOSS_30h" && rat.Forms[0].Power == "DALA_BOSS_30p" && rat.Forms[0].Deck == null);
    var mama = data.Boss("dala_29_mama_diggs");
    Check("P10 Mama Diggs's row lists what Excavate can find (A Small Rock, the sixteen treasures) as cards, so the validator checks each", mama.Cards.Contains(DungeonRunEncounters.SmallRock)
        && DungeonRunEncounters.TogwaggleTreasures.All(id => mama.Cards.Contains(id)));
    var playerPower = DungeonRunAdmission.Classify(C("DALA_BOSS_16p"), "DALA_BOSS_16p", AdmitScope.Player, null, catalogRun: true);
    Check("P11 a family 15 power asked for the run player is refused with its row's reason", !playerPower.Admitted && playerPower.Family == "boss-auto-cast" && playerPower.Lift == DungeonRunAdmission.NoneBossPower);
    DungeonRunData Changed(Action<DungeonRunData> change) { var d = Load(); change(d); return d; }
    var noForm = DungeonRunValidator.Validate(Changed(d => d.Boss("dala_30_rat_king").Forms.Clear()), null);
    Check("P12 the validator holds the flag and the form together: The Rat King without his form is refused", noForm.Count > 0, string.Join(" | ", noForm.Take(2)));
    Check("P13 the tier pools: two rows in enc-t1, three in enc-t2, eight in enc-t3, one in enc-t4", rows.Count(r => r.tier == "1") == 2 && rows.Count(r => r.tier == "2") == 3
        && rows.Count(r => r.tier == "3") == 8 && rows.Count(r => r.tier == "4") == 1 && rows.All(r => data.Boss(r.id).Pools.SequenceEqual(new[] { "enc-t" + r.tier })));
});

Console.WriteLine($"SearchTelemetry swallowed leaf exceptions {SearchTelemetry.SwallowedLeafErrors}");
Console.WriteLine($"RESULT {passes} passed, {failures + SearchTelemetry.SwallowedLeafErrors} failed");
return failures + SearchTelemetry.SwallowedLeafErrors;
