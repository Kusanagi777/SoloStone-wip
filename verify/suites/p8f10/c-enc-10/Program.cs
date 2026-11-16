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

// Phase 8 family 10, Monster Hunt bosses A (real engine games through the run setup path: DungeonRun.BuildPlan on the shipped
// row -> DungeonRunSetup.Configure / ApplyAfterStart): A Mangy Wolf, Rottooth, Cutthroat Willie, Grubb the Swampdrinker (Chuck,
// twice), Gnomenapper (Nab, Stuffed Sack and the set-aside store, Sack of Gnomes), Experiment 3C (Consume, Amalgamate),
// Sazzmi Gentlehorn (Infatuation, Enchanting Tune), Gnarlroot, Niira, The Scarecrow (Harvest, the Scythe), Inquisitor
// Hav'nixx, Chupacabran (Bloodthirst, Vampiric Fangs), Griselda, Wharrgarbl (It's Raining Fin, Murloc Infestation) and
// Ravencaller Cozzlewurt, each with the shrine rules that touch it, and the rows, admission rows and validator.
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
const string Hoarder = "EX1_096", Engineer = "EX1_015", Shrine = "TRLA_154";
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
const string Wisp = "CS2_231", Juggler = "NEW1_019", Kings = "CS2_092", Geomancer = "CS2_142", Shieldbearer = "EX1_405", Squire = "EX1_008", Ooze = "EX1_066";
const string MurlocRaider = "CS2_168", Baron = "FP1_031", Polymorph = "CS2_022", Sap = "EX1_581", Boar = "CS2_171";
const string Sack = "GILA_BOSS_26t", SackOfGnomes = "GILA_BOSS_26t2", ChuckSpell = "GILA_BOSS_23t", Amalgamate = "GILA_BOSS_27t", Amalgamation = "GILA_BOSS_27t2";
const string Tune = "GILA_BOSS_29t", Scythe = "GILA_BOSS_33t", Fangs = "GILA_BOSS_35t", Infestation = "GILA_BOSS_37t", Frog = "hexfrog", Treant = "GIL_663t";
bool Has(IPlayable p, string enchantmentId) => p.AppliedEnchantments?.Any(e => e.Card.Id == enchantmentId) == true;
int Enchants(IPlayable p, string enchantmentId) => p.AppliedEnchantments?.Count(e => e.Card.Id == enchantmentId) ?? 0;
void ToDeck(Controller c, string id) => Generic.ShuffleIntoDeck(c, c.Hero, Entity.FromCard(c, C(id)));
void EmptyHand(Controller c) { foreach (var p in c.HandZone.ToArray()) { c.HandZone.Remove(p); c.SetasideZone.Add(p); } }
void Silence(Game g, Minion m) { new SilenceTask(EntityType.TARGET).Process(g, m.Controller, m.Controller.Hero, m, null); g.DeathProcessingAndAuraUpdate(); }
bool HeroAttack(Game g, ICharacter target) => g.Process(HeroAttackTask.Any(g.CurrentPlayer, target));
bool Attack(Game g, Minion m, ICharacter target) { m.IsExhausted = false; return g.Process(MinionAttackTask.Any(g.CurrentPlayer, m, target)); }
int Cost(string id) => C(id).Cost;

// ============================================================ A  A Mangy Wolf: Guttural Howl
Guard("MangyWolf", () =>
{
    var g = Fight("gila_20_mangy_wolf", bossDeck: Moonfire);
    Controller boss = g.Player2, player = g.Player1;
    ToBoss(g);
    bool none = !Offered(g) && boss.HandZone.All(p => !(p is Minion));
    var yeti = (Minion)ToHand(g, boss, Yeti);
    Check("A1 Guttural Howl costs (1), is clickable, and is offered only while the boss holds a minion", Cost("GILA_BOSS_20p") == 1 && boss.Hero.HeroPower.Cost == 1
        && !boss.Hero.HeroPower.IsPassiveHeroPower && none && Offered(g) && PowerTargets(g).Count == 0);
    bool used = Power(g);
    Check("A2 it gives the one minion in the boss's hand +1/+1 (Howling); the spells it holds get nothing", used && yeti.AttackDamage == 5 && yeti.Health == 6 && Has(yeti, "GILA_BOSS_20e")
        && boss.UsedMana == 1 && boss.HandZone.Where(p => p != yeti).All(p => p.AppliedEnchantments == null || p.AppliedEnchantments.Count == 0), yeti.AttackDamage + "/" + yeti.Health);
    var croc = (Minion)ToHand(g, boss, Croc); var raptor = (Minion)ToHand(g, boss, Raptor);
    int total = 0;
    for (int i = 0; i < 6; i++) { Round(g); if (Power(g)) total++; }
    int buffs = Enchants(yeti, "GILA_BOSS_20e") + Enchants(croc, "GILA_BOSS_20e") + Enchants(raptor, "GILA_BOSS_20e");
    Check("A3 with several minions in hand each use buffs exactly one of them, picked by the game RNG (six uses: six more buffs, on more than one minion)", total == 6 && buffs == 7
        && new[] { yeti, croc, raptor }.Count(m => Has(m, "GILA_BOSS_20e")) >= 2, "buffs " + buffs);
    var (view, viewBoss, viewPlayer) = FairView(g);
    Mana(viewBoss);
    viewBoss.Hero.HeroPower.IsExhausted = false;
    int viewBuffs = viewBoss.HandZone.Sum(p => Enchants(p, "GILA_BOSS_20e"));
    string viewPlay = "ok";
    try { if (!view.Process(HeroPowerTask.Any(viewBoss, null))) viewPlay = "refused"; } catch (Exception e) { viewPlay = e.GetType().Name + ": " + e.Message; }
    Check("A4 the boss's fair view holds its own hand exactly: the power is offered and resolves there too", viewPlay == "ok" && viewBuffs == buffs
        && viewBoss.HandZone.Sum(p => Enchants(p, "GILA_BOSS_20e")) == buffs + 1 && viewBoss.HandZone.Count(p => p is Minion) == 3, viewPlay);
});

// ============================================================ B  Rottooth: Fading Bite
Guard("Rottooth", () =>
{
    var g = Fight("gila_21_rottooth");
    Controller boss = g.Player2, player = g.Player1;
    ToBoss(g);
    bool before = boss.Hero.AttackDamage == 0;
    Check("B1 Fading Bite costs (2) and gives the boss's hero +2 Attack (Fangs)", Cost("GILA_BOSS_21p") == 2 && before && Offered(g) && Power(g) && boss.Hero.AttackDamage == 2
        && Has(boss.Hero, "GILA_BOSS_21e") && boss.UsedMana == 2, "attack " + boss.Hero.AttackDamage);
    int health = player.Hero.Health;
    Check("B2 the hero can attack with it this turn: 2 damage to the enemy hero", HeroAttack(g, player.Hero) && player.Hero.Health == health - 2, (health - player.Hero.Health).ToString());
    EndTurn(g);
    Check("B3 the Attack is gone when the turn ends", boss.Hero.AttackDamage == 0 && !Has(boss.Hero, "GILA_BOSS_21e"), "attack " + boss.Hero.AttackDamage);
});

// ============================================================ C  Cutthroat Willie: Shank
Guard("Willie", () =>
{
    var g = Fight("gila_22_willie");
    Controller boss = g.Player2, player = g.Player1;
    var yeti = Board(g, player, Yeti);
    ToBoss(g);
    var own = Board(g, boss, Croc);
    var targets = PowerTargets(g);
    int hand = boss.HandZone.Count, deck = boss.DeckZone.Count;
    Check("C1 Shank costs (2); it can be aimed at any minion, never a hero; it deals 1 damage to the minion and draws the boss a card", Cost("GILA_BOSS_22p") == 2
        && targets.Count == 2 && targets.Contains(yeti) && targets.Contains(own) && Power(g, yeti) && yeti.Damage == 1 && boss.HandZone.Count == hand + 1 && boss.DeckZone.Count == deck - 1
        && boss.UsedMana == 2, string.Join(",", targets.Select(t => t.Card.Id)) + " dmg " + yeti.Damage);
    Board(g, boss, Geomancer);
    Round(g);
    Check("C2 a hero power: Spell Damage adds nothing", Power(g, yeti) && yeti.Damage == 2, "dmg " + yeti.Damage);
});

// ============================================================ D  Grubb the Swampdrinker: Chuck (the power) and Chuck (the spell)
Guard("Grubb", () =>
{
    var g = Fight("gila_23_grubb");
    Controller boss = g.Player2, player = g.Player1;
    Check("D3 the row's start block: one Chuck (the 6-Cost spell) is shuffled into Grubb's deck after the mulligan", boss.DeckZone.Concat(boss.HandZone).Count(p => p.Card.Id == ChuckSpell) == 1
        && boss.DeckZone.Count + boss.HandZone.Count(p => p.Card.Id != "GAME_005") == 31 && Cost(ChuckSpell) == 6 && C(ChuckSpell).Type == CardType.SPELL,
        boss.DeckZone.Count + "+" + boss.HandZone.Count);
    var theirs = Board(g, player, Croc);
    ToBoss(g);
    bool none = !Offered(g);
    var yeti = Board(g, boss, Yeti);
    var targets = PowerTargets(g);
    int health = player.Hero.Health;
    Check("D1 Chuck costs (1); its targets are the boss's own minions; the minion is destroyed and its Attack dealt to the enemy hero", Cost("GILA_BOSS_23p") == 1 && none
        && targets.Count == 1 && targets[0] == yeti && Power(g, yeti) && yeti.Zone != boss.BoardZone && player.Hero.Health == health - 4 && boss.UsedMana == 1 && theirs.Zone == player.BoardZone,
        "hp " + player.Hero.Health);
    var croc = Board(g, boss, Croc);
    Cast(g, Kings, croc);
    var wall = Board(g, boss, Shieldbearer);
    Round(g);
    health = player.Hero.Health;
    bool thrown = Power(g, croc);
    int afterBuffed = player.Hero.Health;
    Round(g);
    bool thrownWall = Power(g, wall);
    Check("D2 the Attack it has when it is thrown (a 2/3 with +4/+4: 6 damage); a 0-Attack minion is destroyed for no damage", thrown && afterBuffed == health - 6 && thrownWall
        && wall.Zone != boss.BoardZone && player.Hero.Health == afterBuffed, (health - afterBuffed) + " then " + (afterBuffed - player.Hero.Health));
    // The spell.
    EmptyHand(boss);
    var hy = ToHand(g, boss, Yeti); var hc = ToHand(g, boss, Croc); var spell = ToHand(g, boss, Moonfire);
    health = player.Hero.Health;
    bool cast = Cast(g, ChuckSpell);
    Check("D4 Chuck, the spell: every minion in the caster's hand is discarded and the Attack of each dealt to the enemy hero (4 + 2); a spell held is kept", cast && hy.Zone == boss.GraveyardZone
        && hc.Zone == boss.GraveyardZone && spell.Zone == boss.HandZone && boss.HandZone.Count == 1 && player.Hero.Health == health - 6, "hp -" + (health - player.Hero.Health));
    ToPlayer(g);
    int minions = player.HandZone.Count(p => p is Minion), attack = player.HandZone.Where(p => p is Minion).Sum(p => ((Minion)p).AttackDamage);
    int bossHealth = boss.Hero.Health;
    Check("D5 in the run player's hands (taken from the boss) it resolves the same: the player's minions are discarded, their Attack dealt to the boss", minions > 0 && Cast(g, ChuckSpell)
        && player.HandZone.All(p => !(p is Minion)) && boss.Hero.Health == bossHealth - attack, minions + " minions, " + attack + " attack, boss -" + (bossHealth - boss.Hero.Health));
});

// ============================================================ E  Gnomenapper: Nab, Stuffed Sack, Sack of Gnomes
Guard("Gnomenapper", () =>
{
    var g = Fight("gila_26_gnomenapper", shrine: Shrine);
    Controller boss = g.Player2, player = g.Player1;
    var shrine = ShrineRules.Of(player).FirstOrDefault();
    var yeti = Board(g, player, Yeti); var croc = Board(g, player, Croc);
    Cast(g, Kings, yeti);
    yeti.Damage = 3;
    ToBoss(g);
    var own = Board(g, boss, Raptor);
    var targets = PowerTargets(g);
    Check("E1 Nab costs (2); its targets are the enemy minions only", Cost("GILA_BOSS_26p") == 2 && boss.Hero.HeroPower.Cost == 2 && targets.Count == 2 && targets.Contains(yeti) && targets.Contains(croc),
        string.Join(",", targets.Select(t => t.Card.Id)));
    Check("E2 the run player's board shrine is never a legal target (a shrine never enters a sack), and the store itself refuses one", shrine != null && shrine.Card.Id == Shrine && !shrine.IsDormant
        && !targets.Contains(shrine) && !EncounterRules.CanStore(shrine) && !EncounterRules.Store(own, shrine) && shrine.Zone == player.BoardZone && own[EncounterRules.SetAsideStore] == 0);
    bool nabbed = Power(g, yeti);
    var sack = boss.BoardZone.FirstOrDefault(m => m.Card.Id == Sack);
    Check("E3 the minion leaves play for the set-aside zone of its own controller and a Stuffed Sack (0/3, Deathrattle) is summoned for the boss, holding it by one private tag", nabbed && sack != null
        && sack.AttackDamage == 0 && sack.Health == 3 && sack.HasDeathrattle && yeti.Zone == player.SetasideZone && !player.BoardZone.Contains(yeti) && yeti.Controller == player
        && sack[EncounterRules.SetAsideStore] == yeti.Id && EncounterRules.StoredBy(sack) == yeti && EncounterRules.IsPrivateTag(EncounterRules.SetAsideStore) && boss.UsedMana == 2
        && croc.Zone == player.BoardZone, "yeti zone " + yeti.Zone?.Type);
    // A clone and the boss's fair view carry the store.
    var clone = g.Clone();
    var cloneSack = clone.Player2.BoardZone.First(m => m.Card.Id == Sack);
    Kill(clone, cloneSack);
    var cloneYeti = clone.IdEntityDic[yeti.Id] as Minion;
    Check("E4 a game clone carries the store: the sack dies in the clone and the minion is back on its controller's board there; the real game is untouched", cloneYeti != null
        && cloneYeti.Zone == clone.Player1.BoardZone && cloneYeti.Card.Id == Yeti && yeti.Zone == player.SetasideZone && sack.Zone == boss.BoardZone);
    var (view, viewBoss, viewPlayer) = FairView(g);
    var viewSack = viewBoss.BoardZone.FirstOrDefault(m => m.Card.Id == Sack);
    string viewKill = "ok";
    try { Kill(view, viewSack); } catch (Exception e) { viewKill = e.GetType().Name + ": " + e.Message; }
    var viewYeti = view.IdEntityDic.TryGetValue(yeti.Id, out var vy) ? vy as Minion : null;
    Check("E5 the boss's fair view carries it too (the stored minion was public): the sack dies there and the same minion returns, as it was", viewKill == "ok" && viewYeti != null
        && viewYeti.Zone == viewPlayer.BoardZone && viewYeti.Card.Id == Yeti && viewYeti.AttackDamage == 8 && viewYeti.Health == 6, viewKill + " " + viewYeti?.Zone?.Type);
    Kill(g, sack);
    Check("E6 Stuffed Sack's Deathrattle returns the minion to its own controller's board as it left: +4/+4 and its 3 damage kept; the link is consumed", yeti.Zone == player.BoardZone
        && yeti.Controller == player && yeti.AttackDamage == 8 && yeti.Health == 6 && yeti.Damage == 3 && Has(yeti, "CS2_092e") && EncounterRules.StoredBy(sack) == null
        && sack[EncounterRules.SetAsideStore] == 0 && EncounterRules.Release(sack, sack) == null, yeti.Zone?.Type + " " + yeti.AttackDamage + "/" + yeti.Health);
    // A silenced sack returns nothing.
    Round(g);
    Power(g, croc);
    var sack2 = boss.BoardZone.First(m => m.Card.Id == Sack);
    Silence(g, sack2);
    Kill(g, sack2);
    Check("E7 a silenced sack has no Deathrattle: the minion it held never returns", croc.Zone == player.SetasideZone && !player.BoardZone.Contains(croc));
    // The owner's board is full when the sack dies: the minion is lost.
    Round(g);
    Power(g, yeti);
    var sack3 = boss.BoardZone.First(m => m.Card.Id == Sack);
    while (!player.BoardZone.IsFull) Board(g, player, Wisp);
    Kill(g, sack3);
    Check("E8 with its controller's board full when the sack dies the minion is lost (it stays out of play; the link is consumed)", yeti.Zone == player.SetasideZone && player.BoardZone.IsFull
        && sack3[EncounterRules.SetAsideStore] == 0);

    // The stored minion's own triggers are out of play with it; a second Deathrattle run returns nothing more; control of the sack does not matter.
    var g2 = Fight("gila_26_gnomenapper", shrine: Shrine);
    boss = g2.Player2; player = g2.Player1;
    var shrine2 = ShrineRules.Of(player).First();
    var juggler = Board(g2, player, Juggler);
    ToBoss(g2);
    Power(g2, juggler);
    var s1 = boss.BoardZone.First(m => m.Card.Id == Sack);
    ToPlayer(g2);
    int bossDamage = boss.Hero.Damage + boss.BoardZone.Sum(m => m.Damage);
    Board(g2, player, Wisp);
    bool quiet = boss.Hero.Damage + boss.BoardZone.Sum(m => m.Damage) == bossDamage;
    new ControlTask(EntityType.TARGET).Process(g2, player, player.Hero, s1, null);
    g2.DeathProcessingAndAuraUpdate();
    var baron = Board(g2, player, Baron);
    int boardBefore = player.BoardZone.Count;
    Kill(g2, s1);
    Check("E9 a stored Knife Juggler throws no knife while it is in the sack", quiet);
    Check("E10 the run player took control of the sack and it died with Deathrattles doubled: the minion returns once, to its own controller", s1.Controller == player && juggler.Zone == player.BoardZone
        && player.BoardZone.Count == boardBefore && player.BoardZone.Count(m => m.Card.Id == Juggler) == 1, "board " + player.BoardZone.Count + " was " + boardBefore);
    bossDamage = boss.Hero.Damage + boss.BoardZone.Sum(m => m.Damage);
    Board(g2, player, Wisp);
    Check("E11 back in play its trigger works again (one knife for the next summon)", boss.Hero.Damage + boss.BoardZone.Sum(m => m.Damage) == bossDamage + 1);
    // A transformed sack, a bounced sack.
    ToBoss(g2);
    Power(g2, juggler);
    var s2 = boss.BoardZone.First(m => m.Card.Id == Sack);
    ToPlayer(g2);
    Cast(g2, Polymorph, s2);
    var sheep = boss.BoardZone.FirstOrDefault(m => m.Card.Id == "CS2_tk1");
    if (sheep != null) Kill(g2, sheep);
    Check("E12 a transformed sack returns nothing (the Sheep it became dies with no Deathrattle)", sheep != null && juggler.Zone == player.SetasideZone);
    Kill(g2, shrine2);
    var form = ShrineRules.Of(player).FirstOrDefault();
    var wisp = Board(g2, player, Wisp);
    ToBoss(g2);
    var dormantTargets = PowerTargets(g2);
    Check("E13 a dormant shrine form is no target either", form != null && form.IsDormant && !dormantTargets.Contains(form) && dormantTargets.Contains(wisp));
    while (!boss.BoardZone.IsFull) Board(g2, boss, Yeti);
    Check("E14 Nab needs a free slot for the sack: with the boss's board full it is not offered", !Offered(g2));

    // Sack of Gnomes.
    var g3 = Fight("gila_26_gnomenapper");
    boss = g3.Player2; player = g3.Player1;
    ToBoss(g3);
    Board(g3, boss, Yeti); Board(g3, boss, Yeti);
    bool cast = Cast(g3, SackOfGnomes);
    var summoned = boss.BoardZone.Where(m => m.Card.Id != Yeti).Select(m => m.Card.Id).ToList();
    var pool = DungeonRunEncounters.GnomePool(g3, boss).Select(c => c.Id).ToList();
    Check("E15 Sack of Gnomes costs (4) and fills the caster's board with random Gnomes: five free slots, five Gnomes of the pool, more than one kind", Cost(SackOfGnomes) == 4 && cast && boss.BoardZone.IsFull
        && summoned.Count == 5 && summoned.All(id => pool.Contains(id)) && summoned.Distinct().Count() > 1, string.Join(",", summoned));
    Check("E16 the Gnome pool is the scripted cards of the fixed list (Gnomish Experimenter has no script and is left out); with a full board the spell summons nothing", pool.Count == 5
        && pool.All(id => DungeonRunEncounters.Gnomes.Contains(id) && C(id).Implemented && C(id).Type == CardType.MINION && C(id).Name.Contains("Gnom")) && !pool.Contains("GVG_092")
        && DungeonRunEncounters.Gnomes.Count == 6 && Cast(g3, SackOfGnomes) && boss.BoardZone.Count == 7, string.Join(",", pool));
});

// ============================================================ F  Experiment 3C: Consume, Amalgamate, Amalgamation
Guard("Experiment3C", () =>
{
    var g = Fight("gila_27_experiment_3c", shrine: Shrine);
    Controller boss = g.Player2, player = g.Player1;
    var shrine = ShrineRules.Of(player).First();
    var theirs = Board(g, player, Raptor);
    ToBoss(g);
    bool none = !Offered(g);
    var yeti = Board(g, boss, Yeti);
    var targets = PowerTargets(g);
    int hand = boss.HandZone.Count, deck = boss.DeckZone.Count;
    Check("F1 Consume costs (1); its targets are the boss's own minions; the minion is destroyed, then the boss draws 3 cards", Cost("GILA_BOSS_27p") == 1 && targets.Count == 1 && targets[0] == yeti
        && Power(g, yeti) && yeti.Zone != boss.BoardZone && boss.HandZone.Count == Math.Min(10, hand + 3) && boss.DeckZone.Count == deck - 3 && boss.UsedMana == 1, boss.HandZone.Count + " from " + hand);
    Check("F2 with no minion of its own it is not offered", none);
    // Amalgamate.
    EmptyHand(boss);
    var a = Board(g, boss, Yeti); var b = Board(g, boss, Croc);
    b.Damage = 1;
    int attack = a.AttackDamage + b.AttackDamage + theirs.AttackDamage + shrine.AttackDamage, health = a.Health + b.Health + theirs.Health + shrine.Health;
    bool cast = Cast(g, Amalgamate);
    var amalgam = boss.BoardZone.FirstOrDefault(m => m.Card.Id == Amalgamation);
    var form = ShrineRules.Of(player).FirstOrDefault();
    Check("F3 Amalgamate costs (8): all minions are destroyed and one Amalgamation with their combined Attack and remaining Health is summoned for the caster", Cost(Amalgamate) == 8 && cast
        && amalgam != null && boss.BoardZone.Count == 1 && amalgam.AttackDamage == attack && amalgam.Health == health && attack == 9 + shrine.Card.ATK && a.Zone != boss.BoardZone
        && b.Zone != boss.BoardZone && theirs.Zone != player.BoardZone, amalgam?.AttackDamage + "/" + amalgam?.Health + " want " + attack + "/" + health);
    Check("F4 the run player's board shrine goes dormant (as from any destroy effect): its dormant form holds its place", ShrineRules.IsCorpse(shrine) && form != null && form.IsDormant && form.Controller == player
        && player.BoardZone.Count == 1 && !Over(g));
    var card = C(Amalgamation);
    Check("F5 the Amalgamation is a 1-Cost 1/1 of every minion type", card.Cost == 1 && card.ATK == 1 && card.Health == 1 && new[] { Race.ELEMENTAL, Race.MECHANICAL, Race.DEMON, Race.MURLOC, Race.DRAGON, Race.BEAST,
        Race.PIRATE, Race.TOTEM }.All(r => card.IsRace(r)) && C(Amalgamation).Power == null);
    Round(g);
    int amalgamAttack = amalgam.AttackDamage, amalgamHealth = amalgam.Health;
    bool again = Cast(g, Amalgamate);
    var second = boss.BoardZone.FirstOrDefault(m => m.Card.Id == Amalgamation);
    Check("F6 a dormant shrine form is neither counted nor destroyed: a second Amalgamate folds the first Amalgamation alone", again && second != null && second != amalgam && second.AttackDamage == amalgamAttack
        && second.Health == amalgamHealth && ShrineRules.Of(player).FirstOrDefault() == form && form.IsDormant, second?.AttackDamage + "/" + second?.Health);
    // Death processing comes first: with a full board there is room afterwards, and Deathrattles have resolved.
    var g2 = Fight("gila_27_experiment_3c");
    boss = g2.Player2; player = g2.Player1;
    ToBoss(g2);
    EmptyHand(boss);
    Board(g2, boss, Hoarder);
    while (!boss.BoardZone.IsFull) Board(g2, boss, Wisp);
    bool cast2 = Cast(g2, Amalgamate);
    var big = boss.BoardZone.FirstOrDefault(m => m.Card.Id == Amalgamation);
    Check("F7 the destroyed minions leave first (a full board has room again, Loot Hoarder's Deathrattle drew its card), then the Amalgamation enters: 2/1 and six 1/1s make an 8/7", cast2 && big != null
        && boss.BoardZone.Count == 1 && big.AttackDamage == 8 && big.Health == 7 && boss.HandZone.Count == 1, big?.AttackDamage + "/" + big?.Health + " hand " + boss.HandZone.Count);
    Kill(g2, big);
    Check("F8 with no minion on a board Amalgamate summons nothing; in the run player's hands it summons the Amalgamation for the player", Cast(g2, Amalgamate) && boss.BoardZone.Count == 0 && player.BoardZone.Count == 0
        && InOrder(() => { Board(g2, boss, Yeti); ToPlayer(g2); Board(g2, player, Croc); return Cast(g2, Amalgamate); })
        && boss.BoardZone.Count == 0 && player.BoardZone.Count == 1 && player.BoardZone[0].Card.Id == Amalgamation && player.BoardZone[0].AttackDamage == 6 && player.BoardZone[0].Health == 8);
});

// ============================================================ G  Sazzmi Gentlehorn: Infatuation, Enchanting Tune
Guard("Sazzmi", () =>
{
    var g = Fight("gila_29_sazzmi", shrine: Shrine);
    Controller boss = g.Player2, player = g.Player1;
    var shrine = ShrineRules.Of(player).First();
    var croc = Board(g, player, Croc);
    ToBoss(g);
    Check("G2 with only a 2-Attack minion and the shrine (0 Attack, but it never changes sides) on the enemy board Infatuation is not offered", shrine.AttackDamage == 0 && !shrine.IsDormant && !Offered(g));
    var wisp = Board(g, player, Wisp); var wall = Board(g, player, Shieldbearer);
    bool offered = Offered(g) && PowerTargets(g).Count == 0;
    bool used = Power(g);
    Check("G1 Infatuation costs (1): the boss gains control of every enemy minion with 1 or less Attack; a 2-Attack minion stays", Cost("GILA_BOSS_29p") == 1 && offered && used && wisp.Controller == boss
        && wisp.Zone == boss.BoardZone && wall.Controller == boss && wall.Zone == boss.BoardZone && croc.Controller == player && croc.Zone == player.BoardZone && boss.UsedMana == 1);
    Check("G3 the run player's board shrine stays where it is, awake and the player's", shrine.Controller == player && shrine.Zone == player.BoardZone && !shrine.IsDormant && player.BoardZone.Count == 2);
    Round(g);
    while (boss.BoardZone.Count < 6) Board(g, boss, Yeti);
    var w1 = Board(g, player, Wisp); var w2 = Board(g, player, Wisp);
    Power(g);
    Check("G4 with one free slot the first minion is taken and the next is destroyed (the engine's rule for a control change with a full board)", w1.Controller == boss && w1.Zone == boss.BoardZone && boss.BoardZone.IsFull
        && w2.Zone != player.BoardZone && w2.Zone != boss.BoardZone, w2.Zone?.Type.ToString());
    Kill(g, shrine);
    var form = ShrineRules.Of(player).FirstOrDefault();
    Round(g);
    Check("G5 a dormant shrine form is not taken and does not make the power available", form != null && form.IsDormant && !Offered(g) && form.Controller == player);
    // Enchanting Tune.
    var g2 = Fight("gila_29_sazzmi", shrine: Shrine);
    boss = g2.Player2; player = g2.Player1;
    var shrine2 = ShrineRules.Of(player).First();
    var yeti = Board(g2, player, Yeti); var croc2 = Board(g2, player, Croc);
    yeti.Damage = 2;
    int shrineAttack = shrine2.AttackDamage, shrineHealth = shrine2.Health;
    ToBoss(g2);
    var own = Board(g2, boss, Raptor);
    bool cast = Cast(g2, Tune);
    Check("G6 Enchanting Tune costs (4): the caster gains control of all enemy minions and each is set to 1/1 (Enchanted), a damaged one included", Cost(Tune) == 4 && cast && yeti.Controller == boss
        && yeti.Zone == boss.BoardZone && croc2.Zone == boss.BoardZone && yeti.AttackDamage == 1 && yeti.Health == 1 && croc2.AttackDamage == 1 && croc2.Health == 1 && Has(yeti, "GILA_BOSS_29e")
        && own.AttackDamage == 3 && own.Health == 2, yeti.AttackDamage + "/" + yeti.Health);
    Check("G7 the run player's board shrine does not change sides and keeps its stats", shrine2.Controller == player && shrine2.Zone == player.BoardZone && shrine2.AttackDamage == shrineAttack && shrine2.Health == shrineHealth
        && !Has(shrine2, "GILA_BOSS_29e"));
    Round(g2);
    while (boss.BoardZone.Count < 6) Board(g2, boss, Yeti);
    var p1 = Board(g2, player, Yeti); var p2 = Board(g2, player, Yeti);
    Cast(g2, Tune);
    Check("G8 with one free slot: the first minion is taken and set to 1/1, the second is destroyed", p1.Zone == boss.BoardZone && p1.AttackDamage == 1 && p1.Health == 1 && p2.Zone != player.BoardZone && p2.Zone != boss.BoardZone);
    ToPlayer(g2);
    int bossMinions = boss.BoardZone.Count;
    while (player.BoardZone.Count > 1) Kill(g2, player.BoardZone.First(m => m != shrine2));
    foreach (var m in boss.BoardZone.Skip(3).ToArray()) Kill(g2, m);
    var taken = boss.BoardZone.ToArray();
    bool playerCast = Cast(g2, Tune);
    Check("G9 in the run player's hands (taken from the boss) it takes the boss's minions for the player, each at 1/1", playerCast && taken.Length == 3 && taken.All(m => m.Controller == player && m.Zone == player.BoardZone
        && m.AttackDamage == 1 && m.Health == 1) && boss.BoardZone.Count == 0, string.Join(",", taken.Select(m => m.Zone?.Type + ":" + m.AttackDamage + "/" + m.Health)));
});

// ============================================================ H  Gnarlroot: Splinter
Guard("Gnarlroot", () =>
{
    var g = Fight("gila_31_gnarlroot");
    Controller boss = g.Player2, player = g.Player1;
    ToBoss(g);
    int health = boss.Hero.Health;
    Check("H1 Splinter costs (1): the boss's hero takes 2 damage and a 2/2 Treant is summoned", Cost("GILA_BOSS_31p") == 1 && Power(g) && boss.Hero.Health == health - 2 && Count(boss, Treant) == 1
        && boss.BoardZone[0].AttackDamage == 2 && boss.BoardZone[0].Health == 2 && boss.UsedMana == 1 && C(Treant).Cost == 2);
    while (!boss.BoardZone.IsFull) Board(g, boss, Yeti);
    Round(g);
    Check("H2 it needs a free board slot: with a full board it is not offered", !Offered(g));
    Kill(g, boss.BoardZone[0]);
    boss.Hero.Damage = boss.Hero.BaseHealth - 2;
    bool offered = Offered(g);
    Power(g);
    Check("H3 the damage is real damage: at 2 Health the power is still offered and using it kills the boss", offered && Over(g) && boss.PlayState == PlayState.LOST && player.PlayState == PlayState.WON, g.State + " " + boss.PlayState);
});

// ============================================================ I  Niira the Trickster: Unstable Growth
Guard("Niira", () =>
{
    var g = Fight("gila_32_niira", shrine: Shrine);
    Controller boss = g.Player2, player = g.Player1;
    var shrine = ShrineRules.Of(player).First();
    Kill(g, shrine);
    var form = ShrineRules.Of(player).First();
    ToBoss(g);
    Check("I2 with no minion on a board (a dormant shrine form does not count) Unstable Growth is not offered", form.IsDormant && !Offered(g));
    var mine = Board(g, player, Croc);
    bool offered = Offered(g);
    var own = Board(g, boss, Yeti);
    int bossHealth = boss.Hero.Health, playerHealth = player.Hero.Health;
    Check("I1 Unstable Growth costs (1): all minions of both boards get +1/+1 (Growing Strong); the heroes get nothing", Cost("GILA_BOSS_32p") == 1 && offered && Power(g) && own.AttackDamage == 5 && own.Health == 6
        && mine.AttackDamage == 3 && mine.Health == 4 && Has(own, "GILA_BOSS_32e") && Has(mine, "GILA_BOSS_32e") && boss.UsedMana == 1 && boss.Hero.Health == bossHealth && player.Hero.Health == playerHealth
        && boss.Hero.AttackDamage == 0);
    Check("I3 the dormant shrine form gains nothing", !Has(form, "GILA_BOSS_32e") && form.IsDormant);
    Round(g);
    Power(g);
    Check("I4 it stacks: a second use makes the minions +2/+2", own.AttackDamage == 6 && own.Health == 7 && mine.AttackDamage == 4 && mine.Health == 5 && Enchants(own, "GILA_BOSS_32e") == 2);
});

// ============================================================ J  The Scarecrow: Harvest, Scythe
Guard("Scarecrow", () =>
{
    var plan = RowPlan("gila_33_scarecrow");
    var g = Fight("gila_33_scarecrow");
    Controller boss = g.Player2, player = g.Player1;
    var scythe = boss.Hero.Weapon;
    Check("J3 the row's start block: the Scarecrow starts the fight with the Scythe equipped (a 1/1 weapon that is Immune to Durability loss); the player starts with none", plan.Start.BossWeapon?.Id == Scythe
        && scythe != null && scythe.Card.Id == Scythe && scythe.AttackDamage == 1 && scythe.Durability == 1 && scythe.IsImmune && player.Hero.Weapon == null && Cost(Scythe) == 3
        && C(Scythe).Type == CardType.WEAPON && boss.Hero.AttackDamage == 0, scythe?.Card.Id + " " + scythe?.AttackDamage + "/" + scythe?.Durability);
    var theirs = Board(g, player, Wisp); var theirYeti = Board(g, player, Yeti); var squire = Board(g, player, Squire);
    ToBoss(g);
    bool none = !Offered(g);
    var own = Board(g, boss, Yeti);
    boss.Hero.Damage = 10;
    int health = boss.Hero.Health;
    var targets = PowerTargets(g);
    Check("J1 Harvest costs (1); its targets are the boss's own minions; the minion is destroyed and 8 Health restored to the boss's hero", Cost("GILA_BOSS_33p") == 1 && targets.Count == 1 && targets[0] == own
        && Power(g, own) && own.Zone != boss.BoardZone && boss.Hero.Health == health + 8 && boss.UsedMana == 1, "hp " + boss.Hero.Health + " from " + health);
    Check("J2 with no minion of its own it is not offered", none);
    // The Scythe.
    Check("J4 on the boss's turn its hero has the Scythe's 1 Attack", boss.Hero.AttackDamage == 1);
    bool hit = HeroAttack(g, theirs);
    Check("J5 the hero kills a minion with it: the Scythe loses no Durability and gains +1 Attack (Bloody)", hit && theirs.Zone != player.BoardZone && boss.Hero.Weapon == scythe && scythe.Durability == 1
        && scythe.Damage == 0 && scythe.AttackDamage == 2 && Enchants(scythe, "GILA_BOSS_33e2") == 1, scythe.AttackDamage + "/" + scythe.Durability);
    Round(g);
    HeroAttack(g, theirYeti);
    Check("J6 a minion that survives the attack gives nothing; still no Durability lost", theirYeti.Zone == player.BoardZone && theirYeti.Damage == 2 && scythe.AttackDamage == 2 && scythe.Durability == 1 && boss.Hero.Weapon == scythe);
    Round(g);
    HeroAttack(g, squire);
    Check("J7 a Divine Shield absorbs the hit: no kill, no gain", squire.Zone == player.BoardZone && !squire.HasDivineShield && scythe.AttackDamage == 2);
    Round(g);
    int playerHealth = player.Hero.Health;
    HeroAttack(g, player.Hero);
    Check("J8 an attack on the enemy hero gives nothing either (2 damage, the weapon stays 2/1)", player.Hero.Health == playerHealth - 2 && scythe.AttackDamage == 2 && scythe.Durability == 1 && boss.Hero.Weapon == scythe);
    var clone = g.Clone();
    clone.Process(EndTurnTask.Any(clone.CurrentPlayer)); clone.Process(EndTurnTask.Any(clone.CurrentPlayer));
    var cloneSquire = clone.Player1.BoardZone.First(m => m.Card.Id == Squire);
    clone.Process(HeroAttackTask.Any(clone.Player2, cloneSquire));
    Check("J9 a game clone carries the weapon's trigger: the kill in the clone makes its Scythe 3/1, the real one stays 2/1", clone.Player2.Hero.Weapon.AttackDamage == 3 && clone.Player2.Hero.Weapon.Durability == 1
        && scythe.AttackDamage == 2, clone.Player2.Hero.Weapon.AttackDamage.ToString());
    ToPlayer(g);
    Cast(g, Ooze);
    Check("J10 a destroy effect still removes it (Acidic Swamp Ooze)", boss.Hero.Weapon == null);
    bool equipped = Cast(g, Scythe);
    var mine = player.Hero.Weapon;
    var prey = Board(g, boss, Wisp);
    bool attacked = HeroAttack(g, prey);
    Check("J11 in the run player's hands (taken from the boss) it is the same weapon: played for (3), no Durability lost, +1 Attack for the kill", equipped && mine != null && mine.Card.Id == Scythe && attacked
        && prey.Zone != boss.BoardZone && player.Hero.Weapon == mine && mine.Durability == 1 && mine.AttackDamage == 2, mine?.AttackDamage + "/" + mine?.Durability);
});

// ============================================================ K  Inquisitor Hav'nixx: Devour Soul
Guard("Havnixx", () =>
{
    var g = Fight("gila_34_havnixx");
    Controller boss = g.Player2, player = g.Player1;
    ToBoss(g);
    player.Hero.Damage = 0; player.Hero.Armor = 5;
    int full = player.Hero.Health;
    bool used = Power(g);
    int first = player.Hero.Health;
    Check("K1 Devour Soul costs (4) and halves the enemy hero's Health; Armor is untouched and nothing else changes", Cost("GILA_BOSS_34p") == 4 && used && full == 30 && first == 15 && player.Hero.Armor == 5
        && boss.UsedMana == 4 && PowerTargets(g).Count == 0, full + " -> " + first);
    Round(g);
    Power(g);
    int second = player.Hero.Health;
    Round(g);
    Power(g); Round(g); Power(g); Round(g); Power(g);
    Check("K2 an odd Health keeps the larger half: 15 -> 8 -> 4 -> 2 -> 1", second == 8 && player.Hero.Health == 1 && !Over(g), second + " ... " + player.Hero.Health);
    Round(g);
    Check("K3 it never kills: at 1 Health it is not offered", !Offered(g) && player.PlayState == PlayState.PLAYING);
    player.Hero.Damage = 0;
    player.Hero.IsImmune = true;
    bool immune = Power(g);
    Check("K4 it is not damage: an Immune hero is halved as well", immune && player.Hero.Health == 15);
});

// ============================================================ L  Chupacabran: Bloodthirst, Vampiric Fangs
Guard("Chupacabran", () =>
{
    var g = Fight("gila_35_chupacabran", shrine: Shrine);
    Controller boss = g.Player2, player = g.Player1;
    var shrine = ShrineRules.Of(player).First();
    var theirs = Board(g, player, Yeti);
    ToBoss(g);
    bool none = !Offered(g);
    var a = Board(g, boss, Yeti); var b = Board(g, boss, Croc);
    var targets = PowerTargets(g);
    Check("L1 Bloodthirst costs (1); its targets are the boss's own minions; the minion gains Lifesteal (Bloodthirsty)", Cost("GILA_BOSS_35p") == 1 && none && targets.Count == 2 && targets.Contains(a) && targets.Contains(b)
        && !a.HasLifeSteal && Power(g, a) && a.HasLifeSteal && Has(a, "GILA_BOSS_35e2") && !b.HasLifeSteal && boss.UsedMana == 1);
    boss.Hero.Damage = 10;
    int health = boss.Hero.Health;
    bool hit = Attack(g, a, player.Hero);
    Check("L2 the Lifesteal works: the minion's 4 damage to the enemy hero restores 4 Health to the boss's hero", hit && boss.Hero.Health == health + 4, "hp " + boss.Hero.Health + " from " + health);
    Round(g);
    var again = PowerTargets(g);
    Power(g, b);
    Round(g);
    Check("L3 a minion that has Lifesteal is no target; once every minion of the boss has it the power is not offered", again.Count == 1 && again[0] == b && b.HasLifeSteal && !Offered(g));
    // Vampiric Fangs.
    theirs.Damage = 2;
    boss.Hero.Damage = 10;
    health = boss.Hero.Health;
    var fangs = ToHand(g, boss, Fangs);
    Mana(boss);
    var fangTargets = boss.Options().OfType<PlayCardTask>().Where(o => o.Source == fangs).Select(o => o.Target).ToList();
    bool cast = Play(g, fangs, theirs);
    Check("L4 Vampiric Fangs costs (3) and can be aimed at any minion: the minion is destroyed and its remaining Health (3) restored to the caster's hero", Cost(Fangs) == 3 && fangTargets.Contains(theirs)
        && fangTargets.Contains(a) && fangTargets.Contains(shrine) && !fangTargets.Contains(player.Hero) && !fangTargets.Contains(boss.Hero) && cast && theirs.Zone != player.BoardZone
        && boss.Hero.Health == health + 3, "hp " + boss.Hero.Health + " from " + health);
    boss.Hero.Damage = 10;
    health = boss.Hero.Health;
    int shrineHealth = shrine.Health;
    Cast(g, Fangs, shrine);
    var form = ShrineRules.Of(player).FirstOrDefault();
    Check("L5 aimed at the run player's board shrine: the shrine goes dormant (as from any destroy effect) and its Health is restored to the boss", ShrineRules.IsCorpse(shrine) && form != null && form.IsDormant
        && boss.Hero.Health == Math.Min(boss.Hero.BaseHealth, health + shrineHealth) && shrineHealth > 0, "shrine " + shrineHealth);
    ToPlayer(g);
    player.Hero.Damage = 10;
    health = player.Hero.Health;
    Cast(g, Fangs, a);
    Check("L6 in the run player's hands (taken from the boss) it heals the player: a 4/5 destroyed, 5 Health restored", a.Zone != boss.BoardZone && player.Hero.Health == health + 5);
});

// ============================================================ M  Griselda: Witch's Kiss
Guard("Griselda", () =>
{
    var g = Fight("gila_36_griselda", shrine: Shrine);
    Controller boss = g.Player2, player = g.Player1;
    var shrine = ShrineRules.Of(player).First();
    Kill(g, shrine);
    var form = ShrineRules.Of(player).First();
    ToBoss(g);
    Check("M2 with no minion on a board (a dormant shrine form does not count) Witch's Kiss is not offered", form.IsDormant && !Offered(g));
    var yeti = Board(g, player, Yeti);
    bool offered = Offered(g) && PowerTargets(g).Count == 0;
    bool used = Power(g);
    var frog = player.BoardZone.FirstOrDefault(m => m.Card.Id == Frog);
    Check("M1 Witch's Kiss costs (2): the one minion on the boards is transformed into a 0/1 Frog with Taunt, on its controller's board", Cost("GILA_BOSS_36p") == 2 && offered && used && frog != null
        && frog.AttackDamage == 0 && frog.Health == 1 && frog.HasTaunt && frog.Controller == player && !player.BoardZone.Contains(yeti) && boss.UsedMana == 2 && form.IsDormant && form.Zone == player.BoardZone);
    // Random among the minions of both boards, the boss's own included.
    int bossFrogs = 0, playerFrogs = 0;
    for (int seed = 1; seed <= 12; seed++)
    {
        var gs = Fight("gila_36_griselda", seed: seed);
        ToBoss(gs);
        Board(gs, gs.Player2, Yeti); Board(gs, gs.Player1, Yeti);
        Power(gs);
        int frogs = Count(gs.Player1, Frog) + Count(gs.Player2, Frog);
        if (frogs != 1 || gs.Player1.BoardZone.Count + gs.Player2.BoardZone.Count != 2) { bossFrogs = -100; break; }
        bossFrogs += Count(gs.Player2, Frog); playerFrogs += Count(gs.Player1, Frog);
    }
    Check("M3 with a minion on each board one of the two is picked by the game RNG (12 seeds: exactly one Frog each time, on either side)", bossFrogs > 0 && playerFrogs > 0 && bossFrogs + playerFrogs == 12,
        bossFrogs + " boss / " + playerFrogs + " player");
    // The shrine, awake and alone.
    var g2 = Fight("gila_36_griselda", shrine: Shrine);
    boss = g2.Player2; player = g2.Player1;
    var shrine2 = ShrineRules.Of(player).First();
    ToBoss(g2);
    bool offered2 = Offered(g2);
    Power(g2);
    var form2 = ShrineRules.Of(player).FirstOrDefault();
    Check("M4 the run player's awake board shrine can be picked: it is destroyed instead of transformed and goes dormant (no Frog)", offered2 && ShrineRules.IsCorpse(shrine2) && form2 != null && form2.IsDormant
        && Count(player, Frog) == 0 && player.BoardZone.Count == 1);
    var (view, viewBoss, viewPlayer) = FairView(g);
    Board(view, viewBoss, Yeti);
    Mana(viewBoss);
    viewBoss.Hero.HeroPower.IsExhausted = false;
    string viewPlay = "ok";
    try { if (!view.Process(HeroPowerTask.Any(viewBoss, null))) viewPlay = "refused"; } catch (Exception e) { viewPlay = e.GetType().Name + ": " + e.Message; }
    Check("M5 the power resolves in the boss's fair view (boards are public): two minions, one more Frog", viewPlay == "ok" && Count(viewBoss, Frog) + Count(viewPlayer, Frog) == 2, viewPlay);
});

// ============================================================ N  Wharrgarbl: It's Raining Fin, Murloc Infestation
Guard("Wharrgarbl", () =>
{
    var g = Fight("gila_37_wharrgarbl");
    Controller boss = g.Player2, player = g.Player1;
    ToBoss(g);
    EmptyHand(boss);
    for (int i = 0; i < 4; i++) ToDeck(boss, MurlocRaider);
    int deck = boss.DeckZone.Count;
    bool used = Power(g);
    Check("N1 It's Raining Fin costs (2): three Murlocs are drawn from the boss's deck (not its top cards)", Cost("GILA_BOSS_37p") == 2 && used && boss.HandZone.Count == 3
        && boss.HandZone.All(p => p.Card.Id == MurlocRaider) && boss.DeckZone.Count == deck - 3 && boss.DeckZone.Count(p => p.Card.Id == MurlocRaider) == 1 && boss.UsedMana == 2);
    Round(g);
    int hand = boss.HandZone.Count;
    Power(g);
    bool one = boss.HandZone.Count == hand + 1 && boss.DeckZone.All(p => p.Card.Id != MurlocRaider);
    Round(g);
    hand = boss.HandZone.Count; deck = boss.DeckZone.Count;
    bool offered = Offered(g);
    bool paid = Power(g);
    Check("N2 with one Murloc left it draws that one; with none it is still offered (the offer never reads the deck) and does nothing", one && offered && paid && boss.HandZone.Count == hand
        && boss.DeckZone.Count == deck && boss.UsedMana == 2);
    Round(g);
    FillHand(g, boss);
    Check("N3 with a full hand it is not offered", !Offered(g));
    EmptyHand(boss);
    ToDeck(boss, "GIL_681");
    bool amalgam = C("GIL_681").Implemented && Power(g) && boss.HandZone.Count == 1 && boss.HandZone[0].Card.Id == "GIL_681";
    Check("N4 a minion of every type (Nightmare Amalgam) is a Murloc for it", amalgam);
    // Murloc Infestation.
    var g2 = Fight("gila_37_wharrgarbl");
    boss = g2.Player2; player = g2.Player1;
    var theirs = Board(g2, player, Raptor);
    ToBoss(g2);
    var yeti = Board(g2, boss, Yeti); var croc = Board(g2, boss, Croc);
    bool cast = Cast(g2, Infestation);
    Check("N5 Murloc Infestation costs (3): the caster's minions gain \"Deathrattle: Summon a random Murloc\"; the enemy's get nothing", Cost(Infestation) == 3 && cast && Has(yeti, "GILA_BOSS_37e2")
        && Has(croc, "GILA_BOSS_37e2") && yeti.HasDeathrattle && !Has(theirs, "GILA_BOSS_37e2"));
    Kill(g2, yeti);
    var murloc = boss.BoardZone.FirstOrDefault(m => m != croc);
    Check("N6 a minion with it dies: one random scripted Murloc is summoned for its controller, with no such Deathrattle of its own", murloc != null && boss.BoardZone.Count == 2 && murloc.Card.IsRace(Race.MURLOC)
        && murloc.Card.Implemented && !Has(murloc, "GILA_BOSS_37e2"), murloc?.Card.Id);
    var kinds = new HashSet<string>();
    for (int i = 0; i < 12; i++)
    {
        var m = Board(g2, boss, Wisp);
        Generic.AddEnchantmentBlock(g2, C("GILA_BOSS_37e2"), boss.Hero, m);
        Kill(g2, m);
        var born = boss.BoardZone.Last();
        if (!born.Card.IsRace(Race.MURLOC) || !born.Card.Implemented || born.Card.Type != CardType.MINION) kinds.Add("BAD:" + born.Card.Id);
        kinds.Add(born.Card.Id);
        Kill(g2, born);
        foreach (var extra in boss.BoardZone.Where(x => x != croc && x != murloc).ToArray()) Kill(g2, extra);
    }
    Check("N7 twelve more deaths: every summoned minion is a scripted Murloc, of more than one kind", kinds.Count > 1 && !kinds.Any(k => k.StartsWith("BAD:")), string.Join(",", kinds));
});

// ============================================================ O  Ravencaller Cozzlewurt: Call of the Raven
Guard("Cozzlewurt", () =>
{
    var g = Fight("gila_38_cozzlewurt");
    Controller boss = g.Player2, player = g.Player1;
    ToBoss(g);
    EmptyHand(boss);
    bool used = Power(g);
    var got = boss.HandZone.FirstOrDefault();
    Check("O1 Call of the Raven costs (1): one random 1-Cost minion is added to the boss's hand", Cost("GILA_BOSS_38p") == 1 && used && boss.HandZone.Count == 1 && got is Minion && got.Card.Cost == 1
        && got.Card.Implemented && boss.UsedMana == 1, got?.Card.Id);
    var kinds = new HashSet<string>();
    bool allGood = true;
    for (int i = 0; i < 16; i++)
    {
        Round(g);
        EmptyHand(boss);
        Power(g);
        var card = boss.HandZone.Count == 1 ? boss.HandZone[0] : null;
        if (card == null || !(card is Minion) || card.Card.Cost != 1 || !card.Card.Implemented || !card.Card.Collectible) { allGood = false; kinds.Add("BAD:" + card?.Card.Id); }
        else kinds.Add(card.Card.Id);
    }
    Check("O2 sixteen more uses: always a scripted collectible 1-Cost minion, of several kinds", allGood && kinds.Count >= 5, string.Join(",", kinds));
    Round(g);
    FillHand(g, boss);
    Check("O3 with a full hand it is not offered", !Offered(g));
});

// ============================================================ P  the rows, the admission rows, the validator
Guard("Rows", () =>
{
    var errors = DungeonRunValidator.Validate(data, null);
    Check("P1 the shipped data validates with the fifteen family 10 rows", errors.Count == 0, string.Join(" | ", errors.Take(4)));
    var rows = new (string id, string n, string name, string tier, int printed, string kind, string[] flags)[]
    {
        ("gila_20_mangy_wolf", "20", "A Mangy Wolf", "1", 15, "extended", new string[0]),
        ("gila_21_rottooth", "21", "Rottooth", "1", 15, "extended", new string[0]),
        ("gila_22_willie", "22", "Cutthroat Willie", "2", 25, "extended", new string[0]),
        ("gila_23_grubb", "23", "Grubb the Swampdrinker", "4", 50, "extended", new[] { "start" }),
        ("gila_26_gnomenapper", "26", "Gnomenapper", "3", 40, "substituted", new string[0]),
        ("gila_27_experiment_3c", "27", "Experiment 3C", "4", 60, "substituted", new string[0]),
        ("gila_29_sazzmi", "29", "Sazzmi Gentlehorn", "3", 35, "extended", new string[0]),
        ("gila_31_gnarlroot", "31", "Gnarlroot", "2", 30, "extended", new string[0]),
        ("gila_32_niira", "32", "Niira the Trickster", "2", 25, "extended", new string[0]),
        ("gila_33_scarecrow", "33", "The Scarecrow", "3", 40, "authentic", new[] { "start" }),
        ("gila_34_havnixx", "34", "Inquisitor Hav'nixx", "3", 40, "substituted", new string[0]),
        ("gila_35_chupacabran", "35", "Chupacabran", "2", 30, "extended", new string[0]),
        ("gila_36_griselda", "36", "Griselda", "2", 30, "extended", new string[0]),
        ("gila_37_wharrgarbl", "37", "Wharrgarbl", "2", 30, "extended", new string[0]),
        ("gila_38_cozzlewurt", "38", "Ravencaller Cozzlewurt", "1", 15, "extended", new string[0]),
    };
    Check("P2 family 10 holds exactly these fifteen rows", data.Encounters.Where(b => b.Family == "10").Select(b => b.Id).OrderBy(i => i, StringComparer.Ordinal)
        .SequenceEqual(rows.Select(r => r.id).OrderBy(i => i, StringComparer.Ordinal)), string.Join(",", data.Encounters.Where(b => b.Family == "10").Select(b => b.Id)));
    foreach (var r in rows)
    {
        var b = data.Boss(r.id);
        var plan = RowPlan(r.id);
        string hero = "GILA_BOSS_" + r.n + "h", power = "GILA_BOSS_" + r.n + "p";
        var cfg = new GameConfig { RandomSeed = 5, Shuffle = true, SkipMulligan = false, History = false, Logging = false, FormatType = FormatType.FT_WILD };
        DungeonRunSetup.Configure(cfg, plan);
        Check("P3 " + r.id + ": a Monster Hunt row of tier " + r.tier + " (printed Health " + r.printed + ") whose plan is the row's (hero, clickable power, a 30-card " + r.kind
            + " deck, the act curve's Health), flags [" + string.Join(",", r.flags) + "]",
            b.Provenance == "GIL" && b.Name == r.name && b.Tier == r.tier && b.HeroCard == hero && b.Power == power && b.Source.Kind == r.kind && b.Flags.OrderBy(f => f).SequenceEqual(r.flags.OrderBy(f => f))
            && plan.EncounterId == r.id && plan.EncounterDeck && plan.BossHero.Id == hero && plan.BossHeroPowerId == power && plan.BossDeck.Count == 30 && data.BossDecks[b.Deck].Count == 30
            && !b.FixedHealth && b.Health == null && plan.BossHealth == 30 && C(hero).Type == CardType.HERO && C(hero).Health == r.printed && C(power).Type == CardType.HERO_POWER
            && !DungeonRunEncounters.PassivePowers.Contains(power) && DungeonRunEncounters.Find(power)?.Kind == EncounterCardKind.Power && C(hero).Name == r.name
            && data.Schedules.Member(b, DungeonRunEncounterData.PoolOf(b.Tier)) && b.Source.Url.StartsWith("https://hearthstone.wiki.gg/wiki/") && !b.BossGeneratedSecrets && b.Forms.Count == 0,
            b.Source.Kind + " health " + plan.BossHealth + " flags " + string.Join(",", b.Flags) + " tier " + b.Tier);
    }
    string[] powers = rows.Select(r => "GILA_BOSS_" + r.n + "p").ToArray();
    string[] cards = { ChuckSpell, Sack, SackOfGnomes, Amalgamate, Amalgamation, Tune, Scythe, Fangs, Infestation };
    string[] enchantments = { "GILA_BOSS_20e", "GILA_BOSS_21e", "GILA_BOSS_29e", "GILA_BOSS_32e", "GILA_BOSS_33e2", "GILA_BOSS_35e2", "GILA_BOSS_37e2" };
    var scripted = powers.Concat(cards).ToArray();
    var unscripted = scripted.Concat(enchantments).Where(id => !C(id).Implemented || DungeonRunEncounters.Find(id)?.Family != "10").ToList();
    // The engine's loader marks a text with a damage number ("$1") with a trailing " @spelldmg" (Shank).
    var retexted = scripted.Concat(enchantments).Where(id => C(id).Text.Replace(" @spelldmg", "") != factById[id].GetProperty("text").GetString()).ToList();
    Check("P4 every power, boss card and enchantment of the family is scripted by family 10 and keeps the client's text (15 powers, 9 cards, 7 enchantments)", unscripted.Count == 0 && retexted.Count == 0
        && scripted.Length == 24, string.Join(",", unscripted.Concat(retexted.Select(id => id + " [" + C(id).Text + "] vs [" + factById[id].GetProperty("text").GetString() + "]"))));
    var wrongScope = scripted.Where(id =>
    {
        var v = DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss, null, catalogRun: true);
        // The powers are a boss's only; a boss card or token is admitted in the run player's hands too (taken from a boss).
        return !(v.Admitted && v.Subject && v.Rule?.Ids != null && v.Rule.Ids.Contains(id) && DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss).Admitted
            && DungeonRunAdmission.Classify(C(id), id, AdmitScope.Player, null, catalogRun: true).Admitted == cards.Contains(id)
            && !DungeonRunAdmission.Classify(C(id), id, AdmitScope.Generated, null, catalogRun: true).Admitted
            && DungeonRunAdmission.GeneratedRejects(true).Contains(id) && DungeonRunAdmission.GeneratedRejects(false).Contains(id));
    }).ToList();
    Check("P5 each has its own admission row: the fifteen powers a boss's only, the nine boss cards a boss's and (taken from a boss) the run player's; none is ever generated", wrongScope.Count == 0, string.Join(",", wrongScope));
    var families = scripted.Select(id => DungeonRunAdmission.RuleFor(id)?.Family).Distinct().OrderBy(f => f, StringComparer.Ordinal).ToList();
    // The family's own rows of each family name: family 18 (wave 5) reuses boss-own-hand-power for its own rows.
    int Own(string family) => DungeonRunAdmission.Rules.Where(r => r.Family == family && r.Ids != null && r.Ids.Any(scripted.Contains)).Sum(r => r.Ids.Length);
    Check("P6 the five admission families of the family: boss-board-power (10), boss-own-hand-power (3), boss-control-all (1), boss-setaside-store (1), boss-hunt-card (9)",
        families.SequenceEqual(new[] { "boss-board-power", "boss-control-all", "boss-hunt-card", "boss-own-hand-power", "boss-setaside-store" })
        && Own("boss-board-power") == 10 && Own("boss-own-hand-power") == 3 && Own("boss-hunt-card") == 9, string.Join(",", families));
    var grubb = data.Boss("gila_23_grubb"); var scarecrow = data.Boss("gila_33_scarecrow");
    Check("P7 the two start blocks: Grubb's deck add (one Chuck spell), the Scarecrow's weapon (the Scythe); no other family 10 row has one", grubb.Start != null && grubb.Start.BossDeckAdd.SequenceEqual(new[] { ChuckSpell })
        && grubb.Start.BossWeapon == null && scarecrow.Start != null && scarecrow.Start.BossWeapon == Scythe && scarecrow.Start.BossDeckAdd.Count == 0
        && rows.Where(r => r.flags.Length == 0).All(r => data.Boss(r.id).Start == null));
    string Subs(string id) => string.Join(",", data.Boss(id).Source.Substitutions.Where(x => x.Listed != null).Select(x => x.Listed + ">" + (x.Used ?? "-") + ":" + x.Reason).Distinct().OrderBy(x => x, StringComparer.Ordinal));
    Check("P8 the listed cards that are replaced or left out are only cards the engine does not script (or the over-size tail of a longer list), each recorded",
        Subs("gila_23_grubb") == "AT_088>LOOT_383:unscripted,GVG_065>CS2_197:unscripted,GVG_088>EX1_028:unscripted,GVG_112>CS2_200:unscripted"
        && Subs("gila_26_gnomenapper") == "GVG_116>-:over-size,GVG_122>-:unscripted" && Subs("gila_31_gnarlroot") == "EX1_160>CFM_614:unscripted" && Subs("gila_32_niira") == "EX1_160>CFM_614:unscripted"
        && Subs("gila_34_havnixx") == "GILA_BOSS_99t2>EX1_606:unscripted" && Subs("gila_38_cozzlewurt") == "EX1_154>AT_037:unscripted,GVG_092>CS2_147:unscripted"
        && Subs("gila_27_experiment_3c").StartsWith("CFM_344>-:over-size,") && Subs("gila_27_experiment_3c").Contains("GILA_BOSS_99t2>EX1_606:unscripted")
        && data.Boss("gila_27_experiment_3c").Source.Substitutions.Count(x => x.Reason == "over-size") == 15
        && rows.SelectMany(r => data.Boss(r.id).Source.Substitutions).Where(x => x.Reason == "unscripted").All(x => !C(x.Listed).Implemented),
        Subs("gila_23_grubb") + " | " + Subs("gila_26_gnomenapper") + " | " + Subs("gila_38_cozzlewurt"));
    int In(string boss, string card) => data.BossDecks[data.Boss(boss).Deck].Count(id => id == card);
    Check("P9 the boss cards are where the wiki lists them: 4 Sacks of Gnomes, 3 Amalgamates, 2 Enchanting Tunes, 2 Vampiric Fangs, 2 Murloc Infestations; the Scarecrow's 30 cards are the wiki's unchanged",
        In("gila_26_gnomenapper", SackOfGnomes) == 4 && In("gila_27_experiment_3c", Amalgamate) == 3 && In("gila_29_sazzmi", Tune) == 2 && In("gila_35_chupacabran", Fangs) == 2
        && In("gila_37_wharrgarbl", Infestation) == 2 && scarecrow.Source.Kind == DungeonRunDeckSource.KindAuthentic && In("gila_33_scarecrow", "EX1_556") == 3 && In("gila_33_scarecrow", Scythe) == 0
        && In("gila_37_wharrgarbl", "LOEA10_3") == 2);
    var playerPower = DungeonRunAdmission.Classify(C("GILA_BOSS_26p"), "GILA_BOSS_26p", AdmitScope.Player, null, catalogRun: true);
    Check("P10 a family 10 power asked for the run player is refused with its row's reason", !playerPower.Admitted && playerPower.Family == "boss-setaside-store" && playerPower.Lift == DungeonRunAdmission.NoneBossPower);
    DungeonRunData Changed(Action<DungeonRunData> change) { var d = Load(); change(d); return d; }
    var unknownWeapon = DungeonRunValidator.Validate(Changed(d => d.Boss("gila_33_scarecrow").Start.BossWeapon = "GILA_BOSS_33x"), null);
    Check("P11 the validator checks a start block's cards: an unknown start weapon is refused", unknownWeapon.Count > 0, string.Join(" | ", unknownWeapon.Take(2)));
    Check("P12 the tier pools: three rows in enc-t1, six in enc-t2, four in enc-t3, two in enc-t4", rows.Count(r => r.tier == "1") == 3 && rows.Count(r => r.tier == "2") == 6 && rows.Count(r => r.tier == "3") == 4
        && rows.Count(r => r.tier == "4") == 2 && rows.All(r => data.Boss(r.id).Pools.SequenceEqual(new[] { "enc-t" + r.tier })));
});

Console.WriteLine($"SearchTelemetry swallowed leaf exceptions {SearchTelemetry.SwallowedLeafErrors}");
Console.WriteLine($"RESULT {passes} passed, {failures + SearchTelemetry.SwallowedLeafErrors} failed");
return failures + SearchTelemetry.SwallowedLeafErrors;
