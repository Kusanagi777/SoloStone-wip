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

// Phase 8 family 11, Monster Hunt bosses B (real engine games through the run setup path: DungeonRun.BuildPlan on the shipped
// row -> DungeonRunSetup.Configure / ApplyAfterStart): Splintergraft (Grafted Bark), Brushwood Centurion (Survival of the
// Fittest, Hack), Baran the Blind (Bulldoze), Gravekeeper Damph (Grave Mistake, power and spell), Doctor Sezavo (Hollow Needle),
// Garrow (Death and Taxes), Crooked Pete (Soot Up, his change into Beastly Pete, Beast Within), Cragtorr (One with the Trees),
// Azalina Soulthief (Unfinished Business, Soul Assimilation), Face Collector (A New Face, the mirror deck), Raeth Ghostsong
// (Desecrate, Shallow Graves), Grum (Terrify) and Gustave (Cull the Meek), each with the shrine rules that touch it, and the
// rows, admission rows and validator.
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

const string Yeti = "CS2_182", Croc = "CS2_120", Raptor = "CS2_172", Fireball = "CS2_029", Ogre = "CS2_200", Panther = "EX1_017";
const string Hoarder = "EX1_096", Golem = "EX1_556", DamagedGolem = "skele21", Armorsmith = "EX1_402", Shrine = "TRLA_154";
const string Wisp = "CS2_231", Geomancer = "CS2_142", Shieldbearer = "EX1_405", Molten = "EX1_620";
const string Hack = "GILA_BOSS_41t", GraveSpell = "GILA_BOSS_43t", AzWisp = "GILA_BOSS_55t", Assimilation = "GILA_BOSS_55t2", Graves = "GILA_BOSS_57t";
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
bool Over(Game g) => g.State == State.COMPLETE;
bool Has(IPlayable p, string enchantmentId) => p.AppliedEnchantments?.Any(e => e.Card.Id == enchantmentId) == true;
void EmptyHand(Controller c) { foreach (var p in c.HandZone.ToArray()) { c.HandZone.Remove(p); c.SetasideZone.Add(p); } }
void Silence(Game g, Minion m) { new SilenceTask(EntityType.TARGET).Process(g, m.Controller, m.Controller.Hero, m, null); g.DeathProcessingAndAuraUpdate(); }
int Cost(string id) => C(id).Cost;
(Game view, Controller boss, Controller player) FairView(Game g)
{
    var bot = FairBotState.Create(g, g.Player2.PlayerId, 1, DeckStyle.Tempo);
    var view = bot.FingerprintGame;
    var viewBoss = view.Player1.PlayerId == g.Player2.PlayerId ? view.Player1 : view.Player2;
    return (view, viewBoss, viewBoss.Opponent);
}
string Ids(IEnumerable<IPlayable> ps) => string.Join(",", ps.Select(p => p.Card.Id));

// ============================================================ A  Splintergraft: Grafted Bark
Guard("Splintergraft", () =>
{
    var g = Fight("gila_40_splintergraft");
    Controller boss = g.Player2, player = g.Player1;
    var yeti = Board(g, player, Yeti);
    Generic.AddEnchantmentBlock(g, C("CS2_092e"), player.Hero, yeti);
    ToBoss(g);
    var own = Board(g, boss, Raptor);
    var targets = PowerTargets(g);
    int hand = boss.HandZone.Count;
    Check("A1 Grafted Bark costs (2); it can be aimed at any minion of either board, never a hero", Cost("GILA_BOSS_40p") == 2 && boss.Hero.HeroPower.Cost == 2 && targets.Count == 2
        && targets.Contains(yeti) && targets.Contains(own), Ids(targets.Cast<IPlayable>()));
    bool used = Power(g, yeti);
    var copy = boss.HandZone.LastOrDefault();
    Check("A2 a fresh copy of the minion's card goes to the boss's hand as a 2/2 that costs (2) (Petrified); the minion itself is untouched", used && boss.HandZone.Count == hand + 1
        && copy is Minion cm && cm.Card.Id == Yeti && cm != yeti && cm.AttackDamage == 2 && cm.Health == 2 && cm.Cost == 2 && Has(cm, "GILA_BOSS_40e") && !Has(cm, "CS2_092e")
        && yeti.Zone == player.BoardZone && yeti.AttackDamage == 8 && boss.UsedMana == 2, copy?.Card.Id + " " + (copy as Minion)?.AttackDamage + "/" + (copy as Minion)?.Health + " cost " + copy?.Cost);
    Round(g);
    bool played = Play(g, copy);
    Check("A3 played, the copy stays a 2/2 for 2 Mana", played && copy.Zone == boss.BoardZone && ((Minion)copy).AttackDamage == 2 && ((Minion)copy).Health == 2, copy.Zone?.Type.ToString());
    FillHand(g, boss);
    Check("A4 with a full hand it is not offered", !Offered(g));
    // A minion whose card the run's generation filter keeps from the boss is no target.
    EmptyHand(boss);
    var blockedId = DungeonRunAdmission.GeneratedRejects(true).Concat(DungeonRunAdmission.GeneratedRejectsBoss(true))
        .Where(id => { try { var k = C(id); return k != null && k.Type == CardType.MINION && k.Implemented && !k.Untouchable; } catch { return false; } }).OrderBy(id => id, StringComparer.Ordinal).FirstOrDefault();
    var blocked = blockedId == null ? null : Board(g, player, blockedId);
    Check("A5 a minion whose card the boss may never be given (the run's generation filter) is not a choice", blocked != null && !PowerTargets(g).Contains(blocked) && PowerTargets(g).Contains(yeti),
        blockedId ?? "no blocked minion found");
});

// ============================================================ B  Brushwood Centurion: Survival of the Fittest, Hack
Guard("Brushwood", () =>
{
    var g = Fight("gila_41_brushwood");
    Controller boss = g.Player2, player = g.Player1;
    var wisp = Board(g, player, Wisp); var croc = Board(g, player, Croc); var bearer = Board(g, player, Shieldbearer);
    ToBoss(g);
    bool none = !Offered(g);
    var yeti = Board(g, boss, Yeti);
    int bossHealth = boss.Hero.Health, playerHealth = player.Hero.Health;
    Check("B1 Survival of the Fittest costs (3), has no target and is offered only while both boards hold a minion", Cost("GILA_BOSS_41p") == 3 && none && Offered(g) && PowerTargets(g).Count == 0);
    bool used = Power(g);
    Check("B2 every minion attacks a random enemy minion, in play order: the Wisp, the Crocolisk and the 0-Attack Shieldbearer each attack the Yeti and die on it (3 damage); the Yeti has no enemy minion left; the heroes are never attacked",
        used && wisp.Zone != player.BoardZone && croc.Zone != player.BoardZone && bearer.Zone != player.BoardZone && yeti.Zone == boss.BoardZone && yeti.Damage == 3
        && boss.Hero.Health == bossHealth && player.Hero.Health == playerHealth
        && boss.UsedMana == 3, "wisp " + wisp.Zone?.Type + " croc " + croc.Zone?.Type + " bearer " + bearer.Zone?.Type + " " + bearer.Damage + " yeti " + yeti.Zone?.Type + " " + yeti.Damage);
    // Order of play, deaths in between: one boss minion against two that die on contact.
    var g2 = Fight("gila_41_brushwood");
    boss = g2.Player2; player = g2.Player1;
    var w1 = Board(g2, player, Wisp); var w2 = Board(g2, player, Wisp);
    ToBoss(g2);
    var ogre = Board(g2, boss, Ogre);
    Power(g2);
    Check("B3 each attack's deaths resolve before the next, and a minion with no enemy minion left does not attack: the two Wisps die on the Ogre, which takes 2 and attacks nothing",
        w1.Zone != player.BoardZone && w2.Zone != player.BoardZone && ogre.Damage == 2 && ogre.Zone == boss.BoardZone && player.Hero.Health == player.Hero.BaseHealth, "ogre " + ogre.Damage);
    var clone = g2.Clone();
    Check("B4 a clone carries the board after the attacks", clone.Player2.BoardZone.Count == 1 && ((Minion)clone.Player2.BoardZone[0]).Damage == 2 && clone.Player1.BoardZone.Count == 0);
    // Hack.
    Round(g2);
    var smith = Board(g2, boss, Armorsmith);
    var big = Board(g2, boss, Ogre);
    int armor = boss.Hero.Armor;
    bool hacked = Cast(g2, Hack, big);
    Check("B5 Hack costs (3): five hits of 1 on the chosen minion, each its own damage event (Armorsmith: +5 Armor)", Cost(Hack) == 3 && hacked && big.Damage == 5 && boss.Hero.Armor == armor + 5,
        big.Damage + " armor +" + (boss.Hero.Armor - armor));
    Board(g2, boss, Geomancer);
    var pyro = Board(g2, player, Ogre);
    armor = boss.Hero.Armor;
    Cast(g2, Hack, pyro);
    Check("B6 with Spell Damage +1 each hit deals 2, and the hits stop once the minion is dead (four hits on a 7-Health Ogre)", pyro.Zone != player.BoardZone && pyro.Damage == 8 && boss.Hero.Armor == armor,
        pyro.Zone?.Type + " " + pyro.Damage);
    // Wave 4 review: a Hack in the boss's hand, so the options are real (the earlier two were played).
    var hackInHand = ToHand(g2, boss, Hack); Mana(boss);
    var hackOptions = boss.Options().OfType<PlayCardTask>().Where(t => t.Source == hackInHand).ToList();
    Check("B7 Hack can be aimed at any minion, never a hero", C(Hack).Implemented && g2.CurrentPlayer == boss && hackOptions.Any(t => t.Target is Minion) && !hackOptions.Any(t => t.Target is Hero),
        string.Join(",", hackOptions.Select(t => t.Target?.Card.Id ?? "none")));
});

// ============================================================ C  Baran the Blind: Bulldoze
Guard("Baran", () =>
{
    var g = Fight("gila_42_baran");
    Controller boss = g.Player2, player = g.Player1;
    var yeti = Board(g, player, Yeti);
    ToBoss(g);
    bool noArmor = !Offered(g);
    boss.Hero.Armor = 6;
    Check("C1 Bulldoze costs (1), has no target and is offered only with Armor and a minion on a board", Cost("GILA_BOSS_42p") == 1 && noArmor && Offered(g) && PowerTargets(g).Count == 0);
    bool used = Power(g);
    Check("C2 all the Armor is spent as one hit on a random minion: the only minion, a Yeti, takes 6 and dies", used && boss.Hero.Armor == 0 && yeti.Zone != player.BoardZone && boss.UsedMana == 1);
    Round(g);
    boss.Hero.Armor = 3;
    Check("C3 with Armor but no minion on a board it is not offered", !Offered(g));
    var a = Board(g, player, Ogre); var b = Board(g, boss, Ogre);
    var hits = new HashSet<string>();
    for (int i = 0; i < 12; i++)
    {
        Round(g);
        boss.Hero.Armor = 1;
        Power(g);
        if (a.Damage > 0) hits.Add("player"); if (b.Damage > 0) hits.Add("boss");
        a.Damage = 0; b.Damage = 0;
    }
    Check("C4 the minion is random among both boards (twelve uses hit the run player's and the boss's own)", hits.SetEquals(new[] { "player", "boss" }), string.Join(",", hits));
});

// ============================================================ D  Gravekeeper Damph: Grave Mistake
Guard("Damph", () =>
{
    var g = Fight("gila_43_damph");
    Controller boss = g.Player2, player = g.Player1;
    var yeti = Board(g, player, Yeti);
    ToBoss(g);
    bool none = !Offered(g);
    var hoarder = Board(g, player, Hoarder);
    var golem = Board(g, boss, Golem);
    int playerHand = player.HandZone.Count, playerDeck = player.DeckZone.Count;
    Check("D1 Grave Mistake costs (3), has no target and is offered only while a minion with a Deathrattle is on a board", Cost("GILA_BOSS_43p") == 3 && none && Offered(g) && PowerTargets(g).Count == 0);
    bool used = Power(g);
    Check("D2 every minion's Deathrattle resolves twice for its own controller: the run player's Loot Hoarder draws the run player 2 cards, the boss's Harvest Golem summons 2 Damaged Golems for the boss; both stay alive",
        used && player.HandZone.Count == playerHand + 2 && player.DeckZone.Count == playerDeck - 2 && boss.BoardZone.Count(m => m.Card.Id == DamagedGolem) == 2
        && hoarder.Zone == player.BoardZone && golem.Zone == boss.BoardZone && boss.UsedMana == 3, "hand +" + (player.HandZone.Count - playerHand) + " golems " + boss.BoardZone.Count(m => m.Card.Id == DamagedGolem));
    Round(g);
    Silence(g, golem);
    int golems = boss.BoardZone.Count(m => m.Card.Id == DamagedGolem);
    playerHand = player.HandZone.Count;
    Power(g);
    Check("D3 a silenced minion's Deathrattle stays quiet (the Golem summons nothing more; the Hoarder still draws 2)", boss.BoardZone.Count(m => m.Card.Id == DamagedGolem) == golems
        && player.HandZone.Count == Math.Min(10, playerHand + 2));
    // An enchantment's Deathrattle counts too.
    Round(g);
    var raptor = Board(g, boss, Raptor);
    Generic.AddEnchantmentBlock(g, C("GILA_BOSS_37e2"), boss.Hero, raptor);
    int before = boss.BoardZone.Count;
    Power(g);
    Check("D4 a Deathrattle an enchantment gave resolves twice too (Murloc Infestation's: two Murlocs)", boss.BoardZone.Count == Math.Min(7, before + 2), (boss.BoardZone.Count - before).ToString());
    Check("D5 the AI's view: the power is no option without a Deathrattle on a board", !new Func<bool>(() =>
    {
        var g3 = Fight("gila_43_damph"); ToBoss(g3); Board(g3, g3.Player2, Yeti); Board(g3, g3.Player1, Croc); return Offered(g3);
    })());
    // The spell (the hunt's treasure).
    var g2 = Fight("gila_43_damph");
    boss = g2.Player2; player = g2.Player1;
    var h2 = Board(g2, player, Hoarder);
    ToBoss(g2);
    var gol2 = Board(g2, boss, Golem);
    playerHand = player.HandZone.Count;
    bool cast = Cast(g2, GraveSpell);
    Check("D6 Grave Mistake the spell costs (0) and does the same", Cost(GraveSpell) == 0 && cast && player.HandZone.Count == playerHand + 2 && boss.BoardZone.Count(m => m.Card.Id == DamagedGolem) == 2);
    ToPlayer(g2);
    var mine = player.HandZone.Count < 10 ? ToHand(g2, player, GraveSpell) : null;
    int bossGolems = boss.BoardZone.Count(m => m.Card.Id == DamagedGolem);
    playerHand = player.HandZone.Count;
    bool playerCast = mine != null && Play(g2, mine);
    Check("D7 in the run player's hand it resolves the same (the run player's Hoarder draws for the run player, the boss's Golem summons for the boss)", playerCast
        && boss.BoardZone.Count(m => m.Card.Id == DamagedGolem) == Math.Min(7, bossGolems + 2) && player.HandZone.Count == Math.Min(10, playerHand - 1 + 2));
    Check("D8 Grave Mistake the spell is in no shipped deck (the hunt's treasure)", data.BossDecks.Values.All(d => !d.Contains(GraveSpell)));
});

// ============================================================ E  Doctor Sezavo: Hollow Needle
Guard("Sezavo", () =>
{
    var g = Fight("gila_44_sezavo");
    Controller boss = g.Player2, player = g.Player1;
    var wisp = Board(g, player, Wisp); var yeti = Board(g, player, Yeti);
    ToBoss(g);
    var own = Board(g, boss, Raptor);
    boss.Hero.Damage = 10;
    var targets = PowerTargets(g);
    Check("E1 Hollow Needle costs (2) and can be aimed at any minion, never a hero", Cost("GILA_BOSS_44p") == 2 && targets.Count == 3 && targets.All(t => t is Minion));
    int health = boss.Hero.Health;
    Power(g, yeti);
    Check("E2 1 damage to a minion that survives: no Health restored", yeti.Damage == 1 && boss.Hero.Health == health && boss.UsedMana == 2);
    Round(g);
    Power(g, wisp);
    Check("E3 a minion the hit kills: 3 Health restored to the boss's hero", wisp.Zone != player.BoardZone && boss.Hero.Health == health + 3, (boss.Hero.Health - health).ToString());
});

// ============================================================ F  Garrow: Death and Taxes
Guard("Garrow", () =>
{
    var g = Fight("gila_51_garrow", shrine: Shrine);
    Controller boss = g.Player2, player = g.Player1;
    var shrine = ShrineRules.Of(player).First();
    ToBoss(g);
    int health = player.Hero.Health;
    Check("F1 Death and Taxes costs (1); the awake board shrine is a minion the run player controls", Cost("GILA_BOSS_51p") == 1 && Offered(g) && Power(g) && player.Hero.Health == health - 1);
    ToPlayer(g);
    Board(g, player, Yeti); Board(g, player, Croc); Board(g, player, Raptor);
    Kill(g, shrine);
    var form = ShrineRules.Of(player).First();
    ToBoss(g);
    health = player.Hero.Health;
    Check("F2 one hit of 1 per minion the run player controls (three); the dormant shrine form is not counted", form.IsDormant && Power(g) && player.Hero.Health == health - 3, (health - player.Hero.Health).ToString());
    var g2 = Fight("gila_51_garrow");
    ToBoss(g2);
    Board(g2, g2.Player2, Yeti);
    Check("F3 with no minion on the run player's board it is not offered", !Offered(g2));
});

// ============================================================ G  Crooked Pete: Soot Up, Beastly Pete, Beast Within
Guard("Pete", () =>
{
    var plan = RowPlan("gila_52_pete");
    var row = data.Boss("gila_52_pete");
    var beastly = data.BossDecks[row.Forms[0].Deck];
    Check("G1 the row has one script form: Beastly Pete (GILA_BOSS_52h2) with Beast Within and his own 30-card deck, drawing 6", row.Forms.Count == 1 && row.Forms[0].Script && row.Forms[0].Hero == "GILA_BOSS_52h2"
        && row.Forms[0].Power == "GILA_BOSS_52p2" && row.Forms[0].Draw == 6 && beastly.Count == 30 && plan.BossForms.Count == 2 && plan.BossForms[1].Hero.Id == "GILA_BOSS_52h2"
        && plan.BossForms[1].Deck.Count == 30 && !plan.BossForms[1].OnDeath && plan.BossForms[1].HealthAtMost == 0 && row.HasFlag("forms") && row.HasFlag("deckReplace"));
    var g = Fight("gila_52_pete");
    Controller boss = g.Player2, player = g.Player1;
    var power = boss.Hero.HeroPower;
    Check("G2 the fight starts as Crooked Pete with Soot Up, its start-of-turn trigger started (EncounterRules.ActivatePower)", boss.Hero.Card.Id == "GILA_BOSS_52h" && power.Card.Id == "GILA_BOSS_52p"
        && power[EncounterRules.PowerActive] == 1 && boss[EncounterRules.FormIndex] == 0 && Cost("GILA_BOSS_52p") == 1);
    var panther = Board(g, player, Panther);
    ToBoss(g);
    var yeti = Board(g, boss, Yeti); var stealthy = Board(g, boss, Panther);
    var targets = PowerTargets(g);
    Check("G3 Soot Up's targets are the boss's own minions that have no Stealth yet", targets.Count == 1 && targets[0] == yeti, Ids(targets.Cast<IPlayable>()));
    Power(g, yeti);
    Check("G4 the minion has Stealth (Covered in Soot)", yeti.HasStealth && Has(yeti, "GILA_BOSS_52e2") && boss.UsedMana == 1);
    EndTurn(g);
    bool during = yeti.HasStealth;
    EndTurn(g);
    Check("G5 the Stealth lasts through the run player's turn and is gone at the start of the boss's next turn", during && !yeti.HasStealth && !Has(yeti, "GILA_BOSS_52e2"));
    // Above half Health: no change. At half: Beastly Pete at the start of his turn.
    int max = boss.Hero.BaseHealth;
    ToPlayer(g);
    boss.Hero.Damage = max - (max / 2 + 1);
    ToBoss(g);
    bool unchanged = boss.Hero.Card.Id == "GILA_BOSS_52h" && boss[EncounterRules.FormIndex] == 0;
    ToPlayer(g);
    boss.Hero.Damage = max - max / 2;
    int health = boss.Hero.Health, armor = boss.Hero.Armor;
    var oldHand = boss.HandZone.ToList();
    Check("G6 above half his maximum Health nothing happens at the start of his turn", unchanged && max == plan.BossHealth, "max " + max + " plan " + plan.BossHealth);
    EndTurn(g);
    var hero = boss.Hero;
    var drawnIds = boss.HandZone.Select(p => p.Card.Id).ToList();
    Check("G7 at half or less, at the start of his turn he becomes Beastly Pete with Beast Within: Health and maximum kept, +15 Armor", hero.Card.Id == "GILA_BOSS_52h2"
        && hero.HeroPower.Card.Id == "GILA_BOSS_52p2" && hero.Health == health && hero.BaseHealth == max && hero.Armor == armor + 15 && boss[EncounterRules.FormIndex] == 1
        && boss[EncounterRules.FormChanges] == 1 && !hero.HeroPower.IsExhausted, hero.Card.Id + " " + hero.Health + "/" + hero.BaseHealth + " armor " + hero.Armor);
    Check("G8 his hand is set aside, Beastly Pete's list replaces his deck and he draws 6 from it (then his turn's draw); the boss's own-deck fact is recorded",
        oldHand.All(p => p.Zone == boss.SetasideZone) && boss.HandZone.Count == 7 && drawnIds.All(id => beastly.Contains(id)) && boss.DeckZone.Count == 23
        && boss.DeckZone.All(p => beastly.Contains(p.Card.Id)) && boss.DeckZone.All(p => p.Card.Id != Yeti)
        && g.RunDeckFacts.Any(f => f.Kind == RunDeckFactKind.Replaced && f.ControllerId == boss.Id), "hand " + boss.HandZone.Count + " deck " + boss.DeckZone.Count);
    var sample = OwnDeckLedger.Observe(g, boss, null).Sample(g, boss, 11);
    Check("G9 the boss's own-deck ledger samples Beastly Pete's list (23 cards left, every one from his list), not Crooked Pete's deck", sample.Values.Sum() == 23
        && sample.Keys.All(id => beastly.Contains(id)) && !sample.ContainsKey(Yeti), string.Join(",", sample.Select(p => p.Key + "x" + p.Value)));
    Mana(boss);
    var mine = Board(g, boss, Raptor);
    bool beast = Power(g);
    Check("G10 Beast Within (1) gives the boss's minions +1 Attack for good (Beast Mode)", Cost("GILA_BOSS_52p2") == 1 && beast && mine.AttackDamage == 4 && yeti.AttackDamage == 5 && Has(mine, "GILA_BOSS_52e")
        && panther.AttackDamage == 4);
    Round(g);
    Check("G11 the change happens once: the next turn starts with no second change (the old power's trigger is stopped)", boss[EncounterRules.FormChanges] == 1 && boss.Hero == hero
        && power[EncounterRules.PowerActive] == 0 && power.Zone == boss.SetasideZone);
    var end = DungeonRunSetup.Harvest(g);
    Check("G12 the harvest reports form 1 and one form change", end.BossForm == 1 && end.BossFormChanges == 1 && end.BossRemainingHealth == boss.Hero.Health);
    // Killed outright from above half: he dies.
    var g2 = Fight("gila_52_pete");
    var boss2 = g2.Player2;
    boss2.Hero.Damage = boss2.Hero.BaseHealth - (boss2.Hero.BaseHealth / 2 + 1);
    Mana(g2.Player1);
    for (int i = 0; i < 10 && !Over(g2); i++) Cast(g2, Fireball, boss2.Hero);
    Check("G13 a Pete killed outright before such a turn start dies (no form change on death)", Over(g2) && g2.Player1.PlayState == PlayState.WON && boss2[EncounterRules.FormIndex] == 0);
    Check("G14 with no Beast Within target (no minion of his) the power is not offered", new Func<bool>(() =>
    {
        var g3 = Fight("gila_52_pete"); g3.Player2.Hero.Damage = g3.Player2.Hero.BaseHealth - g3.Player2.Hero.BaseHealth / 2; ToBoss(g3);
        return g3.Player2.Hero.Card.Id == "GILA_BOSS_52h2" && !Offered(g3);
    })());
});

// ============================================================ H  Cragtorr: One with the Trees
Guard("Cragtorr", () =>
{
    var g = Fight("gila_54_cragtorr");
    Controller boss = g.Player2, player = g.Player1;
    ToBoss(g);
    EmptyHand(boss);
    var spell = ToHand(g, boss, Fireball);
    bool spellsOnly = !Offered(g);
    var ogre = ToHand(g, boss, Ogre); var wisp = ToHand(g, boss, Wisp);
    Check("H1 One with the Trees costs (2) and is offered only while the boss holds a minion that costs (1) or more", Cost("GILA_BOSS_54p") == 2 && spellsOnly && Offered(g));
    Power(g);
    Check("H2 every minion the boss holds costs (1) less (Assimilation); the spell does not", ogre.Cost == 5 && Has(ogre, "GILA_BOSS_54e") && wisp.Cost == 0 && spell.Cost == 4 && !Has(spell, "GILA_BOSS_54e")
        && boss.UsedMana == 2, ogre.Cost + " " + spell.Cost);
    Round(g);
    Power(g);
    Check("H3 it stacks (the Ogre costs (4))", ogre.Cost == 4);
});

// ============================================================ I  Azalina Soulthief: Unfinished Business, Soul Assimilation
Guard("Azalina", () =>
{
    var g = Fight("gila_55_azalina", shrine: Shrine);
    Controller boss = g.Player2, player = g.Player1;
    var shrine = ShrineRules.Of(player).First();
    var yeti = Board(g, player, Yeti); var croc = Board(g, player, Croc);
    ToBoss(g);
    bool used = Power(g);
    Check("I1 Unfinished Business costs (2) and summons three 1/1 Wisps (GILA_BOSS_55t)", Cost("GILA_BOSS_55p") == 2 && used && boss.BoardZone.Count(m => m.Card.Id == AzWisp) == 3
        && boss.BoardZone.Where(m => m.Card.Id == AzWisp).All(m => m.AttackDamage == 1 && m.Health == 1) && boss.UsedMana == 2);
    Round(g);
    Board(g, boss, Raptor); Board(g, boss, Raptor);
    Power(g);
    Check("I2 with room for two only two are summoned", boss.BoardZone.Count == 7 && boss.BoardZone.Count(m => m.Card.Id == AzWisp) == 5);
    Round(g);
    Check("I3 with a full board it is not offered", !Offered(g));
    // Soul Assimilation: five Wisps of hers (and a classic Wisp) for two takeable minions and a shrine.
    var classic = boss.BoardZone.First(m => m.Card.Id == Raptor);
    Kill(g, classic);
    Board(g, boss, Wisp);
    int wisps = boss.BoardZone.Count(m => m.Card.Id == AzWisp || m.Card.Id == Wisp);
    bool cast = Cast(g, Assimilation);
    Check("I4 Soul Assimilation costs (4): every Wisp of the caster (hers and the classic one) is destroyed", Cost(Assimilation) == 4 && cast && wisps == 6
        && boss.BoardZone.All(m => m.Card.Id != AzWisp && m.Card.Id != Wisp), Ids(boss.BoardZone));
    Check("I5 one enemy minion per Wisp changes sides while one is left to take: the Yeti and the Crocolisk are the boss's now", yeti.Controller == boss && croc.Controller == boss
        && yeti.Zone == boss.BoardZone && croc.Zone == boss.BoardZone);
    Check("I6 the run player's board shrine never changes sides", shrine.Controller == player && shrine.Zone == player.BoardZone && !shrine.IsDormant);
    var g2 = Fight("gila_55_azalina");
    boss = g2.Player2; player = g2.Player1;
    var a = Board(g2, player, Yeti); var b = Board(g2, player, Croc); var c3 = Board(g2, player, Raptor);
    ToBoss(g2);
    Board(g2, boss, AzWisp);
    Cast(g2, Assimilation);
    Check("I7 one Wisp: one random enemy minion is taken", new[] { a, b, c3 }.Count(m => m.Controller == boss) == 1 && player.BoardZone.Count == 2);
    Cast(g2, Assimilation);
    Check("I8 with no Wisp nothing is taken", new[] { a, b, c3 }.Count(m => m.Controller == boss) == 1);
});

// ============================================================ J  Face Collector: A New Face and the mirror deck
Guard("FaceCollector", () =>
{
    var g = Fight("gila_56_face_collector", shrine: Shrine);
    Controller boss = g.Player2, player = g.Player1;
    var shrine = ShrineRules.Of(player).First();
    var yeti = Board(g, player, Yeti);
    var molten = Board(g, player, Molten);
    ToBoss(g);
    var own = Board(g, boss, Wisp);
    var targets = PowerTargets(g);
    bool moltenOut = DungeonRunEncounters.FacePool(g, molten).Count == 0;
    Check("J1 A New Face costs (0); any minion is a choice that has a minion costing (2) more (a Molten Giant, Cost 20, has none)", Cost("GILA_BOSS_56p") == 0 && targets.Contains(yeti) && targets.Contains(own)
        && targets.Contains(shrine) && (moltenOut ? !targets.Contains(molten) : targets.Contains(molten)) && targets.All(t => t is Minion), Ids(targets.Cast<IPlayable>()) + " molten pool " + DungeonRunEncounters.FacePool(g, molten).Count);
    Power(g, yeti);
    var became = player.BoardZone.FirstOrDefault(m => m.Card.Id != Molten && ShrineRules.Of(player).All(s => s != m));
    Check("J2 the minion becomes a random scripted minion that costs (2) more, still the run player's", yeti.Zone != player.BoardZone && became != null && became.Card.Cost == 6 && became.Card.Implemented
        && became.Controller == player, became?.Card.Id);
    var kinds = new HashSet<string>();
    for (int i = 0; i < 10; i++)
    {
        Round(g);
        var w = Board(g, boss, Wisp);
        Power(g, w);
        var now = boss.BoardZone.Last();
        kinds.Add(now.Card.Cost == 2 && now.Card.Implemented ? now.Card.Id : "BAD:" + now.Card.Id);
        Kill(g, now);
    }
    Check("J3 ten more uses on the boss's own Wisp (Cost 0): always a scripted 2-Cost minion, of several kinds", kinds.Count >= 4 && !kinds.Any(k => k.StartsWith("BAD:")), string.Join(",", kinds));
    Round(g);
    Power(g, shrine);
    var form = ShrineRules.Of(player).FirstOrDefault();
    Check("J4 aimed at the run player's board shrine: the shrine is destroyed instead and goes dormant (the transform rule)", ShrineRules.IsCorpse(shrine) && form != null && form.IsDormant);
    // The mirror deck.
    var s = At("MAGE", 4400, "gila_56_face_collector");
    var plan = run.BuildPlan(s, all);
    var admitted = plan.PlayerDeck.Where(c => DungeonRunAdmission.Classify(c, c.Id, AdmitScope.Boss, null, s.IsCatalogRun).Admitted).Select(c => c.Id).ToList();
    Check("J5 flag mirrorDeck: the boss's deck is the run deck as this fight plays it, less what a boss may not hold (here all of it)", data.Boss("gila_56_face_collector").HasFlag("mirrorDeck")
        && plan.BossDeck.Select(c => c.Id).SequenceEqual(admitted) && admitted.Count >= 10 && plan.BossDeck.Select(c => c.Id).SequenceEqual(plan.PlayerDeck.Select(c => c.Id)),
        plan.BossDeck.Count + " of " + plan.PlayerDeck.Count);
    var s2 = At("MAGE", 4400, "gila_56_face_collector");
    var playerOnly = DungeonRunAdmission.Rules.Where(r => r.Family.StartsWith("run-treasure", StringComparison.Ordinal) && r.Ids != null).SelectMany(r => r.Ids)
        .First(id => { try { return C(id) != null && !DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss, null, true).Admitted; } catch { return false; } });
    foreach (string id in s2.Deck.Take(4).ToList()) DungeonRunDeck.Add(s2, id, DungeonRunDeck.OriginDraft);
    DungeonRunDeck.Add(s2, playerOnly, DungeonRunDeck.OriginDraft);
    var plan2 = run.BuildPlan(s2, all);
    int mirror2 = plan2.PlayerDeck.Count(c => DungeonRunAdmission.Classify(c, c.Id, AdmitScope.Boss, null, s2.IsCatalogRun).Admitted);
    Check("J6 a card the admission table keeps from a boss (a run treasure) is left out of the copy", plan2.BossDeck.All(c => c.Id != playerOnly)
        && plan2.BossDeck.Count == plan2.PlayerDeck.Count(c => c.Id != playerOnly), plan2.BossDeck.Count + " of " + plan2.PlayerDeck.Count + " " + playerOnly + " admitted " + mirror2 + " catalog " + s2.IsCatalogRun + " deck " + string.Join(",", s2.Deck));
    var s3 = At("MAGE", 4400, "gila_56_face_collector");
    for (int i = 4; i < s3.Deck.Count; i++) s3.Deck[i] = playerOnly;
    var plan3 = run.BuildPlan(s3, all);
    Check("J7 with fewer than 10 such cards (4 of 10 here) the row's own curated deck is played", plan3.BossDeck.Select(c => c.Id).SequenceEqual(data.BossDecks[data.Boss("gila_56_face_collector").Deck])
        && data.Boss("gila_56_face_collector").Source.Kind == DungeonRunDeckSource.KindCurated);
    // A Fabled bundle (the catalog's TIME_619) the copy would split is left out whole, so the boss's deck builds.
    var s4 = At("MAGE", 4400, "gila_56_face_collector");
    foreach (string id in new[] { "TIME_619" }.Concat(SabberStoneCore.Actions.FabledRules.Bundle("TIME_619"))) DungeonRunDeck.Add(s4, id, DungeonRunDeck.OriginDraft);
    var plan4 = run.BuildPlan(s4, all);
    string built4 = "ok";
    try { DungeonRunBossPilot.Build(data.Boss("gila_56_face_collector"), plan4, data); } catch (Exception e) { built4 = e.Message; }
    bool companionsAdmitted = SabberStoneCore.Actions.FabledRules.Bundle("TIME_619").Concat(new[] { "TIME_619" })
        .All(id => DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss, null, s4.IsCatalogRun).Admitted);
    Check("J9 a Fabled bundle in the run deck is copied whole when a boss may hold all of it, else left out whole; the boss's deck passes the Fabled rules and its pilot builds",
        built4 == "ok" && !SabberStoneCore.Actions.FabledRules.DeckErrors(plan4.BossDeck.Select(c => c.Id)).Any()
        && (companionsAdmitted ? plan4.BossDeck.Count(c => c.Id == "TIME_619") == 1 : plan4.BossDeck.All(c => c.Id != "TIME_619" && !SabberStoneCore.Actions.FabledRules.IsCompanion(c.Id))),
        built4 + " admitted " + companionsAdmitted + " " + string.Join(",", plan4.BossDeck.Select(c => c.Id).Where(id => id.StartsWith("TIME_"))));
    var s5 = At("MAGE", 4400, "gila_56_face_collector");
    DungeonRunDeck.Add(s5, "TIME_619", DungeonRunDeck.OriginDraft);
    var plan5 = run.BuildPlan(s5, all);
    string built5 = "ok";
    try { DungeonRunBossPilot.Build(data.Boss("gila_56_face_collector"), plan5, data); } catch (Exception e) { built5 = e.Message; }
    Check("J10 a Fabled card whose companions the copy would lack (the run deck holds it alone) is left out, the rest is copied", built5 == "ok" && plan5.BossDeck.All(c => c.Id != "TIME_619")
        && plan5.BossDeck.Count == plan5.PlayerDeck.Count - 1, built5 + " " + plan5.BossDeck.Count + "/" + plan5.PlayerDeck.Count);
    var g3 = Ready(plan);
    Check("J8 a real game starts from the mirror plan with the copied deck (30 cards less the opening hand)", g3.Player2.DeckZone.Count + g3.Player2.HandZone.Count - (g3.Player2.HandZone.Any(p => p.Card.Id == "GAME_005") ? 1 : 0) == plan.BossDeck.Count
        && g3.Player2.Hero.Card.Id == "GILA_BOSS_56h");
});

// ============================================================ K  Raeth Ghostsong: Desecrate, Shallow Graves
Guard("Raeth", () =>
{
    var g = Fight("gila_57_raeth", shrine: Shrine);
    Controller boss = g.Player2, player = g.Player1;
    var shrine = ShrineRules.Of(player).First();
    var yeti = Board(g, player, Yeti);
    ToBoss(g);
    var w1 = Board(g, boss, Wisp);
    var power = boss.Hero.HeroPower;
    Check("K1 Desecrate costs (1) and has no target; its Inspire trigger is started with it", Cost("GILA_BOSS_57p") == 1 && PowerTargets(g).Count == 0 && power[EncounterRules.PowerActive] == 1);
    Power(g);
    Check("K2 1 damage to every minion of both boards; one died (the Wisp), so it is ready again; the refresh mark is cleared", w1.Zone != boss.BoardZone && yeti.Damage == 1 && shrine.Damage == 1
        && !power.IsExhausted && power[EncounterRules.PowerRefresh] == 0 && Offered(g) && boss.UsedMana == 1);
    Power(g);
    Check("K3 again: nothing dies, so it stays used (it was paid twice)", yeti.Damage == 2 && shrine.Damage == 2 && power.IsExhausted && !Offered(g) && boss.UsedMana == 2);
    Round(g);
    Power(g);
    var form = ShrineRules.Of(player).FirstOrDefault();
    Check("K4 the run player's board shrine dying counts (it goes dormant) and readies it again", ShrineRules.IsCorpse(shrine) && form != null && form.IsDormant && !power.IsExhausted && Offered(g));
    var clone = g.Clone();
    Check("K5 a clone carries the ready power", !clone.Player2.Hero.HeroPower.IsExhausted && clone.Player2.Options().Any(o => o is HeroPowerTask));
    var g0 = Fight("gila_57_raeth");
    ToBoss(g0);
    Check("K6 with no minion on a board it is not offered", !Offered(g0));
    // Shallow Graves: two Yetis and the shrine died, a Crocolisk was discarded.
    var g2 = Fight("gila_57_raeth", shrine: Shrine);
    boss = g2.Player2; player = g2.Player1;
    var shrine2 = ShrineRules.Of(player).First();
    var py = Board(g2, player, Yeti);
    ToBoss(g2);
    var by = Board(g2, boss, Raptor);
    Kill(g2, py); Kill(g2, by); Kill(g2, shrine2);
    var croc = ToHand(g2, boss, Croc);
    Generic.DiscardBlock.Invoke(boss, croc);
    g2.DeathProcessingAndAuraUpdate();
    int before = boss.BoardZone.Count;
    bool cast = Cast(g2, Graves);
    var summoned = boss.BoardZone.Skip(before).ToList();
    Check("K7 Shallow Graves costs (9): fresh minions of the deaths of both sides this game, for the caster (a Yeti, a Raptor); never the dead run shrine, never the discarded Crocolisk",
        Cost(Graves) == 9 && cast && summoned.Count == 2 && summoned.Select(m => m.Card.Id).OrderBy(x => x).SequenceEqual(new[] { Yeti, Raptor }.OrderBy(x => x)) && summoned.All(m => m.Controller == boss)
        && ShrineRules.IsCorpse(shrine2) && player.GraveyardZone.Any(p => p.Card.Id == Shrine) && summoned.All(m => m.Card.Id != Shrine && m.Card.Id != Croc), Ids(summoned));
    Check("K8 the dead run shrine is in the graveyard and is never among the candidates", DungeonRunEncounters.DiedThisGame(boss).All(p => !ShrineRules.IsCorpse(p) && p.Card.Id != Shrine)
        && DungeonRunEncounters.DiedThisGame(boss).Count == 2);
    for (int i = 0; i < 10; i++) { var m = Board(g2, player, Wisp); Kill(g2, m); }
    foreach (var m in boss.BoardZone.ToArray()) Kill(g2, m);
    Round(g2);
    cast = Cast(g2, Graves);
    Check("K9 with more than seven deaths it summons seven", cast && boss.BoardZone.Count == 7, boss.BoardZone.Count.ToString());
});

// ============================================================ L  Grum: Terrify
Guard("Grum", () =>
{
    var g = Fight("gila_58_grum", shrine: Shrine);
    Controller boss = g.Player2, player = g.Player1;
    var shrine = ShrineRules.Of(player).First();
    var ogre = Board(g, player, Ogre);
    Generic.AddEnchantmentBlock(g, C("CS2_092e"), player.Hero, ogre);
    ogre.Damage = 2;
    ToBoss(g);
    var own = Board(g, boss, Croc);
    var targets = PowerTargets(g);
    int playerDeck = player.DeckZone.Count, bossDeck = boss.DeckZone.Count;
    Check("L1 Terrify costs (4); any minion is a choice (the awake shrine too)", Cost("GILA_BOSS_58p") == 4 && targets.Contains(ogre) && targets.Contains(own) && targets.Contains(shrine) && targets.All(t => t is Minion));
    Power(g, ogre);
    Check("L2 the run player's minion goes into the run player's deck, reset as the engine's MoveToDeck resets it (its damage gone), and costs (4) less there (Terrified)", ogre.Zone == player.DeckZone
        && ogre.Controller == player && player.DeckZone.Count == playerDeck + 1 && ogre.Cost == 2 && Has(ogre, "GILA_BOSS_58e") && ogre.Damage == 0 && boss.UsedMana == 4,
        ogre.Zone?.Type + " cost " + ogre.Cost + " kings " + Has(ogre, "CS2_092e") + " damage " + ogre.Damage + " deck " + player.DeckZone.Count + "/" + playerDeck + " mana " + boss.UsedMana);
    Round(g);
    Power(g, own);
    Check("L3 the boss's own minion goes into the boss's own deck, costing (0) now", own.Zone == boss.DeckZone && own.Controller == boss && own.Cost == 0 && boss.DeckZone.Count == bossDeck + 1 - 1,
        "deck " + boss.DeckZone.Count + " from " + bossDeck);
    var sample = OwnDeckLedger.Observe(g, boss, null).Sample(g, boss, 11);
    Check("L4 the boss's own-deck ledger knows the card it shuffled into its own deck (its sample holds the one Crocolisk among the Yetis)", sample.TryGetValue(Croc, out int crocs) && crocs == 1
        && sample.Values.Sum() == boss.DeckZone.Count, string.Join(",", sample.Select(p => p.Key + "x" + p.Value)));
    Round(g);
    Power(g, shrine);
    var form = ShrineRules.Of(player).FirstOrDefault();
    Check("L5 aimed at the run player's board shrine: it is destroyed instead and goes dormant, it never enters a deck", ShrineRules.IsCorpse(shrine) && form != null && form.IsDormant
        && player.DeckZone.All(p => p.Card.Id != Shrine));
    var g2 = Fight("gila_58_grum");
    boss = g2.Player2; player = g2.Player1;
    var y2 = Board(g2, player, Yeti);
    while (!player.DeckZone.IsFull) Generic.ShuffleIntoDeck(player, player.Hero, Entity.FromCard(player, C(Croc)));
    ToBoss(g2);
    var mine = Board(g2, boss, Croc);
    Check("L6 a minion whose owner's deck is full is no choice", !PowerTargets(g2).Contains(y2) && PowerTargets(g2).Contains(mine));
});

// ============================================================ M  Gustave: Cull the Meek
Guard("Gustave", () =>
{
    var g = Fight("gila_62_gustave", shrine: Shrine);
    Controller boss = g.Player2, player = g.Player1;
    var shrine = ShrineRules.Of(player).First();
    var raptor = Board(g, player, Raptor);
    ToBoss(g);
    var wisp = Board(g, boss, Wisp);
    Check("M1 Cull the Meek costs (1) and has no target", Cost("GILA_BOSS_62p") == 1 && Offered(g) && PowerTargets(g).Count == 0);
    Power(g);
    var form = ShrineRules.Of(player).FirstOrDefault();
    Check("M2 the minion with the lowest Attack of both boards dies: the 0-Attack board shrine (it goes dormant), not the 1-Attack Wisp", ShrineRules.IsCorpse(shrine) && form != null && form.IsDormant
        && wisp.Zone == boss.BoardZone && raptor.Zone == player.BoardZone && boss.UsedMana == 1);
    Round(g);
    Power(g);
    Check("M3 next the Wisp (1 Attack) before the Raptor (3); a dormant shrine form is not counted", wisp.Zone != boss.BoardZone && raptor.Zone == player.BoardZone && form.IsDormant);
    var g2 = Fight("gila_62_gustave");
    boss = g2.Player2; player = g2.Player1;
    var a = Board(g2, player, Croc); var b = Board(g2, player, Croc);
    ToBoss(g2);
    var c = Board(g2, boss, Croc);
    Check("M4 with no minion on a board it is not offered", new Func<bool>(() => { var g3 = Fight("gila_62_gustave"); ToBoss(g3); return !Offered(g3); })());
    var dead = new HashSet<string>();
    for (int i = 0; i < 12; i++)
    {
        var gg = Fight("gila_62_gustave", seed: 100 + i);
        var x = Board(gg, gg.Player1, Croc); var y = Board(gg, gg.Player1, Croc);
        ToBoss(gg);
        var z = Board(gg, gg.Player2, Croc);
        Power(gg);
        dead.Add(x.Zone != gg.Player1.BoardZone ? "x" : y.Zone != gg.Player1.BoardZone ? "y" : z.Zone != gg.Player2.BoardZone ? "z" : "none");
    }
    Check("M5 a tie is broken by the game RNG (twelve seeds kill different Crocolisks, always exactly one)", dead.Count >= 2 && !dead.Contains("none"), string.Join(",", dead));
});

// ============================================================ P  the rows, the admission rows, the validator
Guard("Rows", () =>
{
    var errors = DungeonRunValidator.Validate(data, null);
    Check("P1 the shipped data validates with the thirteen family 11 rows", errors.Count == 0, string.Join(" | ", errors.Take(4)));
    var rows = new (string id, string n, string name, string tier, int printed, string kind, string[] flags)[]
    {
        ("gila_40_splintergraft", "40", "Splintergraft", "3", 40, "authentic", new string[0]),
        ("gila_41_brushwood", "41", "Brushwood Centurion", "4", 60, "authentic", new[] { "forcedAttack" }),
        ("gila_42_baran", "42", "Baran the Blind", "2", 30, "authentic", new string[0]),
        ("gila_43_damph", "43", "Gravekeeper Damph", "3", 35, "extended", new[] { "doubleDeathrattle" }),
        ("gila_44_sezavo", "44", "Doctor Sezavo", "1", 20, "extended", new string[0]),
        ("gila_51_garrow", "51", "Garrow, the Rancorous", "3", 40, "authentic", new string[0]),
        ("gila_52_pete", "52", "Crooked Pete", "3", 40, "authentic", new[] { "forms", "deckReplace" }),
        ("gila_54_cragtorr", "54", "Cragtorr", "3", 40, "authentic", new string[0]),
        ("gila_55_azalina", "55", "Azalina Soulthief", "4", 50, "authentic", new string[0]),
        ("gila_56_face_collector", "56", "Face Collector", "4", 60, "curated", new[] { "mirrorDeck" }),
        ("gila_57_raeth", "57", "Raeth Ghostsong", "4", 60, "authentic", new string[0]),
        ("gila_58_grum", "58", "Grum", "3", 40, "substituted", new string[0]),
        ("gila_62_gustave", "62", "Gustave, the Gutripper", "4", 50, "authentic", new string[0]),
    };
    Check("P2 family 11 holds exactly these thirteen rows", data.Encounters.Where(b => b.Family == "11").Select(b => b.Id).OrderBy(i => i, StringComparer.Ordinal)
        .SequenceEqual(rows.Select(r => r.id).OrderBy(i => i, StringComparer.Ordinal)), string.Join(",", data.Encounters.Where(b => b.Family == "11").Select(b => b.Id)));
    foreach (var r in rows)
    {
        var b = data.Boss(r.id);
        var plan = RowPlan(r.id);
        string hero = "GILA_BOSS_" + r.n + "h", power = "GILA_BOSS_" + r.n + "p";
        bool mirror = r.flags.Contains("mirrorDeck");
        Check("P3 " + r.id + ": a Monster Hunt row of tier " + r.tier + " (printed Health " + r.printed + ") whose plan is the row's (hero, clickable power, a 30-card " + r.kind
            + " deck" + (mirror ? " the run deck's copy replaces" : "") + ", the act curve's Health), flags [" + string.Join(",", r.flags) + "]",
            b.Provenance == "GIL" && b.Name == r.name && b.Tier == r.tier && b.HeroCard == hero && b.Power == power && b.Source.Kind == r.kind && b.Flags.OrderBy(f => f).SequenceEqual(r.flags.OrderBy(f => f))
            && plan.EncounterId == r.id && plan.EncounterDeck && plan.BossHero.Id == hero && plan.BossHeroPowerId == power && data.BossDecks[b.Deck].Count == 30
            && (mirror ? plan.BossDeck.Count == plan.PlayerDeck.Count : plan.BossDeck.Count == 30)
            && !b.FixedHealth && b.Health == null && plan.BossHealth == 30 && C(hero).Type == CardType.HERO && C(hero).Health == r.printed && C(power).Type == CardType.HERO_POWER
            && !DungeonRunEncounters.PassivePowers.Contains(power) && DungeonRunEncounters.Find(power)?.Kind == EncounterCardKind.Power && C(hero).Name == r.name
            && data.Schedules.Member(b, DungeonRunEncounterData.PoolOf(b.Tier)) && b.Source.Url.StartsWith("https://hearthstone.wiki.gg/wiki/") && !b.BossGeneratedSecrets
            && b.Forms.Count == (r.n == "52" ? 1 : 0),
            b.Source.Kind + " health " + plan.BossHealth + " flags " + string.Join(",", b.Flags) + " tier " + b.Tier + " deck " + plan.BossDeck.Count);
    }
    string[] powers = rows.Select(r => "GILA_BOSS_" + r.n + "p").Concat(new[] { "GILA_BOSS_52p2" }).ToArray();
    string[] cards = { Hack, GraveSpell, AzWisp, Assimilation, Graves };
    string[] enchantments = { "GILA_BOSS_40e", "GILA_BOSS_52e", "GILA_BOSS_52e2", "GILA_BOSS_54e", "GILA_BOSS_58e" };
    var scripted = powers.Concat(cards).ToArray();
    var unscripted = scripted.Concat(enchantments).Where(id => !C(id).Implemented || DungeonRunEncounters.Find(id)?.Family != "11").ToList();
    // The engine's loader marks a text with a damage number ("$1") with a trailing " @spelldmg".
    string Text(string id) => factById.TryGetValue(id, out var f) && f.TryGetProperty("text", out var t) ? t.GetString() : null;
    var retexted = scripted.Concat(enchantments).Where(id => (C(id).Text ?? "").Replace(" @spelldmg", "") != (Text(id) ?? "")).ToList();
    Check("P4 every power, boss card, token and enchantment of the family is scripted by family 11 and keeps the client's text (14 powers, 5 cards, 5 enchantments)", unscripted.Count == 0 && retexted.Count == 0
        && scripted.Length == 19, string.Join(",", unscripted.Concat(retexted.Select(id => id + " [" + C(id).Text + "]"))));
    var wrongScope = scripted.Where(id =>
    {
        var v = DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss, null, catalogRun: true);
        return !(v.Admitted && v.Subject && v.Rule?.Ids != null && v.Rule.Ids.Contains(id) && DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss).Admitted
            && DungeonRunAdmission.Classify(C(id), id, AdmitScope.Player, null, catalogRun: true).Admitted == cards.Contains(id)
            && !DungeonRunAdmission.Classify(C(id), id, AdmitScope.Generated, null, catalogRun: true).Admitted
            && DungeonRunAdmission.GeneratedRejects(true).Contains(id) && DungeonRunAdmission.GeneratedRejects(false).Contains(id));
    }).ToList();
    Check("P5 each has its own admission row: the fourteen powers a boss's only, the five boss cards a boss's and (taken from a boss) the run player's; none is ever generated", wrongScope.Count == 0, string.Join(",", wrongScope));
    var families = scripted.Select(id => DungeonRunAdmission.RuleFor(id)?.Family).Distinct().OrderBy(f => f, StringComparer.Ordinal).ToList();
    int Sum(string f) => DungeonRunAdmission.Rules.Where(r => r.Family == f).Sum(r => r.Ids.Length);
    Check("P6 the five admission families of the family: boss-hunt-power (10), boss-hunt-own-hand (2), boss-hunt-form (1), boss-hunt-deck-shuffle (1), boss-hunt-card-b (5)",
        families.SequenceEqual(new[] { "boss-hunt-card-b", "boss-hunt-deck-shuffle", "boss-hunt-form", "boss-hunt-own-hand", "boss-hunt-power" })
        && Sum("boss-hunt-power") == 10 && Sum("boss-hunt-own-hand") == 2 && Sum("boss-hunt-form") == 1 && Sum("boss-hunt-deck-shuffle") == 1 && Sum("boss-hunt-card-b") == 5, string.Join(",", families));
    string Subs(string id) => string.Join(",", data.Boss(id).Source.Substitutions.Where(x => x.Listed != null).Select(x => x.Listed + ">" + (x.Used ?? "-") + ":" + x.Reason).Distinct().OrderBy(x => x, StringComparer.Ordinal));
    var pete = data.Boss("gila_52_pete");
    int In7(string boss, string card) => data.BossDecks[data.Boss(boss).Deck].Count(id => id == card);
    var formSubs = string.Join(",", data.Encounters.Where(b => b.Family == "11").SelectMany(b => b.Forms).Select(f => f.Deck));
    // Wave 6: family 23 scripts Witchwood's Touch (GILA_BOSS_99t2), so Cragtorr's, Azalina's, Gustave's and Beastly Pete's listed
    // copies stay in their decks (they were Shield Block, EX1_606, until then): only Grum's three Ogres are still replaced.
    Check("P7 the listed cards that are replaced are only cards the engine does not script (three Ogres; Witchwood's Touch since wave 6 is scripted and kept), each recorded",
        Subs("gila_58_grum") == "AT_088>LOOT_383:unscripted,GVG_065>CS2_197:unscripted,GVG_088>EX1_028:unscripted" && Subs("gila_54_cragtorr") == ""
        && Subs("gila_55_azalina") == "" && Subs("gila_62_gustave") == ""
        && In7("gila_54_cragtorr", "GILA_BOSS_99t2") == 2 && In7("gila_55_azalina", "GILA_BOSS_99t2") == 1 && In7("gila_62_gustave", "GILA_BOSS_99t2") == 2
        && data.Encounters.Where(b => b.Family == "11").SelectMany(b => b.Source.Substitutions).Where(x => x.Reason == "unscripted").All(x => !C(x.Listed).Implemented)
        && data.BossDecks[pete.Forms[0].Deck].Count(id => id == "EX1_606") == 0 && data.BossDecks[pete.Forms[0].Deck].Count(id => id == "GILA_BOSS_99t2") == 2
        && formSubs == "enc-gila_52_pete-f1",
        Subs("gila_58_grum") + " | " + Subs("gila_54_cragtorr") + " | " + Subs("gila_55_azalina") + " | " + Subs("gila_62_gustave"));
    int In(string boss, string card) => data.BossDecks[data.Boss(boss).Deck].Count(id => id == card);
    Check("P8 the boss cards are where the wiki lists them: 4 Hacks, 2 Soul Assimilations, 4 Shallow Graves; Crooked Pete's Spore Hallucination is Hallucination (UNG_856)",
        In("gila_41_brushwood", Hack) == 4 && In("gila_55_azalina", Assimilation) == 2 && In("gila_57_raeth", Graves) == 4 && In("gila_52_pete", "UNG_856") == 1
        && data.Boss("gila_55_azalina").Cards.Contains(AzWisp));
    var playerPower = DungeonRunAdmission.Classify(C("GILA_BOSS_58p"), "GILA_BOSS_58p", AdmitScope.Player, null, catalogRun: true);
    Check("P9 a family 11 power asked for the run player is refused with its row's reason", !playerPower.Admitted && playerPower.Family == "boss-hunt-deck-shuffle" && playerPower.Lift == DungeonRunAdmission.NoneBossPower);
    DungeonRunData Changed(Action<DungeonRunData> change) { var d = Load(); change(d); return d; }
    var badForm = DungeonRunValidator.Validate(Changed(d => d.Boss("gila_52_pete").Forms[0].OnDeath = true), null);
    Check("P10 the validator checks the form: a form named both script and onDeath is refused", badForm.Any(e => e.Contains("needs exactly one")), string.Join(" | ", badForm.Take(2)));
    Check("P11 the tier pools: one row in enc-t1, one in enc-t2, six in enc-t3, five in enc-t4", rows.Count(r => r.tier == "1") == 1 && rows.Count(r => r.tier == "2") == 1 && rows.Count(r => r.tier == "3") == 6
        && rows.Count(r => r.tier == "4") == 5 && rows.All(r => data.Boss(r.id).Pools.SequenceEqual(new[] { "enc-t" + r.tier })));
    Check("P12 the private tag PowerRefresh is the family's reserved 32646, in the private range", (int)EncounterRules.PowerRefresh == 32646 && EncounterRules.IsPrivateTag(EncounterRules.PowerRefresh));
});

Console.WriteLine($"SearchTelemetry swallowed leaf exceptions {SearchTelemetry.SwallowedLeafErrors}");
Console.WriteLine($"RESULT {passes} passed, {failures + SearchTelemetry.SwallowedLeafErrors} failed");
return failures + SearchTelemetry.SwallowedLeafErrors;
