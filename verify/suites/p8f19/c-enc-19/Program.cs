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

// Phase 8 family 19, Tombs of Terror bosses A (real engine games through the run setup path: DungeonRun.BuildPlan on the shipped
// row -> DungeonRunSetup.Configure / ApplyAfterStart): Waste Wanderer Cardish (Boom Bullets), Dark Ritualist Zafarr (Mummy Ritual),
// Vera Ridley (E.V.I.L. Promotion), Nash the Greatworm (Belch), Glack the Scorpid (Venomous Stinger), Mister Chu ("Confiscated"
// Goods), Squeamlish (Druid of the Rat), Sinkhole (Sand Surge, Sand Trap), Demolisher 3V-11 (Armor Repairs), Terraviss (Well
// Equipped), R.O.B.O.L.D. (Unstable Weaponry), Pyramad (Brick by Brick), Snakeflinger Scalesnout (Snakeshot), Sword Dancer Sirinell
// (Sword Dance), Lt. Herring (Puffertrooper), Battrund (Under Wraps), Ichabod the Cursed (Spreading the Curse) and Twizzleflux
// (Plagued Horde), with the shrine and hidden-zone cases that touch them, and the rows, the tier rule, the admission rows and the
// validator.
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

const string Yeti = "CS2_182", Croc = "CS2_120", Raptor = "CS2_172", Moonfire = "CS2_008", Wisp = "CS2_231", Shrine = "TRLA_154";
const string KoboldLackey = "DAL_614", MicroMummy = "ULD_217", MurlocRaider = "CS2_168", Mechwarper = "GVG_006", AnnoyOTron = "GVG_085";
const string WarAxe = "CS2_106", Reaper = "CS2_112", DeadlyPoison = "CS2_074", DeadlyPoisonE = "CS2_074e", Bomb = "BOT_031", SandTrap = "ULDA_030";
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
void EmptyHand(Controller c) { foreach (var p in c.HandZone.ToArray()) { c.HandZone.Remove(p); c.SetasideZone.Add(p); } }
Minion Board(Game g, Controller c, string id) { var m = (Minion)Entity.FromCard(c, C(id)); Generic.SummonBlock(g, m, -1, c.Hero); g.DeathProcessingAndAuraUpdate(); return m; }
void FillBoard(Game g, Controller c) { while (!c.BoardZone.IsFull) Board(g, c, Wisp); }
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
void Hit(Game g, Minion m, int n) { new DamageTask(n, EntityType.TARGET).Process(g, m.Controller.Opponent, m.Controller.Opponent.Hero, m, null); g.DeathProcessingAndAuraUpdate(); }
bool Has(IPlayable p, string enchantmentId) => p.AppliedEnchantments?.Any(e => e.Card.Id == enchantmentId) == true;
int Cost(string id) => C(id).Cost;
bool HeroAttack(Game g, ICharacter target) { g.CurrentPlayer.Hero.IsExhausted = false; return g.Process(HeroAttackTask.Any(g.CurrentPlayer, target)); }
bool Attack(Game g, Minion m, ICharacter target) => g.Process(MinionAttackTask.Any(g.CurrentPlayer, m, target));
(Game view, Controller boss, Controller player) FairView(Game g)
{
    var bot = FairBotState.Create(g, g.Player2.PlayerId, 1, DeckStyle.Tempo);
    var view = bot.FingerprintGame;
    var viewBoss = view.Player1.PlayerId == g.Player2.PlayerId ? view.Player1 : view.Player2;
    return (view, viewBoss, viewBoss.Opponent);
}
bool Clickable(Game g) => !g.Player2.Hero.HeroPower.IsPassiveHeroPower;
// A passive power: never an option, started at match start (its trigger is live).
bool PassiveStarted(Game g, string power) => g.Player2.Hero.HeroPower.Card.Id == power && g.Player2.Hero.HeroPower.IsPassiveHeroPower
    && DungeonRunEncounters.PassivePowers.Contains(power) && g.Player2.Hero.HeroPower[EncounterRules.PowerActive] == 1;

// ============================================================ A  Waste Wanderer Cardish: Boom Bullets
Guard("Cardish", () =>
{
    var g = Fight("ulda_01_cardish");
    Controller boss = g.Player2, player = g.Player1;
    var theirs = Board(g, player, Bomb);
    ToBoss(g);
    var own = Board(g, boss, Bomb);
    var yeti = Board(g, boss, Yeti);
    bool used = Power(g);
    var fresh = boss.BoardZone.LastOrDefault();
    Check("A1 Boom Bullets costs (2), is clickable and untargeted: it summons a 0/2 Goblin Bomb for the boss", Cost("ULDA_BOSS_01p") == 2 && Clickable(g) && used && fresh != null
        && fresh.Card.Id == Bomb && fresh != own && fresh.AttackDamage == 0 && fresh.Health == 2 && boss.UsedMana == 2 && boss.BoardZone.Count == 3, fresh?.Card.Id);
    Check("A2 every other Goblin Bomb on either board gains +1 Attack (ULDA_BOSS_01e); the new one and the other minions get nothing", own.AttackDamage == 1 && theirs.AttackDamage == 1
        && Has(own, "ULDA_BOSS_01e") && Has(theirs, "ULDA_BOSS_01e") && !Has(fresh, "ULDA_BOSS_01e") && yeti.AttackDamage == 4, own.AttackDamage + "/" + theirs.AttackDamage);
    Round(g);
    Power(g);
    Check("A3 used again: the older Bombs reach +2, the previous new one +1", own.AttackDamage == 2 && theirs.AttackDamage == 2 && fresh.AttackDamage == 1 && boss.BoardZone.Count(m => m.Card.Id == Bomb) == 3);
    Round(g);
    FillBoard(g, boss);
    Check("A4 with a full board it is not offered", !Offered(g));
});

// ============================================================ B  Dark Ritualist Zafarr: Mummy Ritual
Guard("Zafarr", () =>
{
    var g = Fight("ulda_02_zafarr");
    Controller boss = g.Player2, player = g.Player1;
    ToBoss(g);
    var yeti = Board(g, boss, Yeti);
    Kill(g, yeti);
    var theirs = Board(g, player, MicroMummy);
    Kill(g, theirs);
    Kill(g, player.BoardZone.FirstOrDefault(m => m.Card.Id == MicroMummy) ?? theirs);
    Check("B1 Mummy Ritual costs (3); it is not offered while only a non-Reborn minion of the boss's and a Reborn minion of the run player's died",
        Cost("ULDA_BOSS_02p") == 3 && Clickable(g) && !Offered(g) && boss.GraveyardZone.Any(p => p.Card.Id == Yeti) && player.GraveyardZone.Any(p => p.Card.Id == MicroMummy));
    var mummy = Board(g, boss, MicroMummy);
    Kill(g, mummy);
    var reborn = boss.BoardZone.FirstOrDefault(m => m.Card.Id == MicroMummy);
    bool rebornBack = reborn != null && reborn[GameTag.REBORN] == 0;
    Kill(g, reborn);
    Check("B2 a Reborn minion of the boss's that died (its Reborn return died too) makes it offered", rebornBack && boss.BoardZone.Count == 0 && Offered(g) && PowerTargets(g).Count == 0);
    bool used = Power(g);
    var back = boss.BoardZone.FirstOrDefault();
    Check("B3 it summons a new copy of that minion with 1 Health, keeping its card's Reborn", used && back != null && back.Card.Id == MicroMummy && back.Health == 1 && back.BaseHealth == C(MicroMummy).Health
        && back[GameTag.REBORN] == 1 && boss.UsedMana == 3, back == null ? "none" : back.Card.Id + " " + back.Health + "hp reborn " + back[GameTag.REBORN]);
    Kill(g, back);
    var again = boss.BoardZone.FirstOrDefault();
    Check("B4 the copy's own Reborn brings it back once more (1 Health, no Reborn)", again != null && again.Card.Id == MicroMummy && again.Health == 1 && again[GameTag.REBORN] == 0);
    Round(g);
    FillBoard(g, boss);
    Check("B5 with a full board it is not offered", !Offered(g));
});

// ============================================================ C  Vera Ridley: E.V.I.L. Promotion
Guard("Vera", () =>
{
    var g = Fight("ulda_03_vera");
    Controller boss = g.Player2, player = g.Player1;
    Check("C1 E.V.I.L. Promotion is passive: started at match start, never an option; its count starts at 1", PassiveStarted(g, "ULDA_BOSS_03p") && C("ULDA_BOSS_03p").Text == factById["ULDA_BOSS_03p"].GetProperty("text").GetString()
        && DungeonRunEncounters.TombsA.PromotionBonus(boss.Hero.HeroPower) == 1);
    ToBoss(g);
    Check("C1b the passive is not offered on the boss's turn", !Offered(g));
    var first = ToHand(g, boss, KoboldLackey);
    bool p1 = Play(g, first, player.Hero);
    var l1 = (Minion)first;
    Check("C2 the first Lackey the boss plays gets +1/+1 (ULDA_BOSS_03e), and the count rises to 2", p1 && l1.AttackDamage == 2 && l1.Health == 2 && Has(l1, "ULDA_BOSS_03e")
        && boss.Hero.HeroPower[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2, l1.AttackDamage + "/" + l1.Health);
    var yeti = ToHand(g, boss, Yeti);
    Play(g, yeti);
    Check("C3 a minion that is not a Lackey gets nothing and the count stays", !Has(yeti, "ULDA_BOSS_03e") && ((Minion)yeti).AttackDamage == 4 && boss.Hero.HeroPower[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2);
    var summoned = Board(g, boss, KoboldLackey);
    Check("C4 a Lackey summoned without being played gets nothing", !Has(summoned, "ULDA_BOSS_03e") && boss.Hero.HeroPower[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2);
    var second = (Minion)ToHand(g, boss, KoboldLackey);
    Play(g, second, player.Hero);
    Check("C5 the second Lackey played gets +2/+2; the count is 3", second.AttackDamage == 3 && second.Health == 3 && boss.Hero.HeroPower[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3, second.AttackDamage + "/" + second.Health);
    ToPlayer(g);
    var theirs = (Minion)ToHand(g, player, KoboldLackey);
    Play(g, theirs, boss.Hero);
    Check("C6 a Lackey the run player plays gets nothing", theirs.AttackDamage == 1 && !Has(theirs, "ULDA_BOSS_03e") && boss.Hero.HeroPower[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3);
    ToBoss(g);
    var (view, viewBoss, viewPlayer) = FairView(g);
    Check("C7 the boss's fair view carries the count (the power's own public tag)", viewBoss.Hero.HeroPower[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3, viewBoss.Hero.HeroPower[GameTag.TAG_SCRIPT_DATA_NUM_1].ToString());
});

// ============================================================ D  Nash the Greatworm: Belch
Guard("Nash", () =>
{
    var g = Fight("ulda_04_nash");
    Controller boss = g.Player2, player = g.Player1;
    ToBoss(g);
    Check("D1 Belch costs (3); with no minion dead yet it is not offered", Cost("ULDA_BOSS_04p") == 3 && Clickable(g) && !Offered(g));
    var croc = Board(g, player, Croc);
    Kill(g, croc);
    bool used = Power(g);
    var back = boss.BoardZone.FirstOrDefault();
    Check("D2 a minion of the run player's that died: the boss summons a new copy of it for itself", used && back != null && back.Card.Id == Croc && back.Controller == boss && back.Damage == 0
        && boss.UsedMana == 3, back?.Card.Id);
    var seen = new HashSet<string>();
    var raptor = Board(g, boss, Raptor);
    Kill(g, raptor);
    for (int i = 0; i < 12; i++)
    {
        Round(g);
        foreach (var m in boss.BoardZone.ToArray()) { boss.BoardZone.Remove(m); boss.SetasideZone.Add(m); }
        Power(g);
        if (boss.BoardZone.Count == 1) seen.Add(boss.BoardZone[0].Card.Id);
    }
    Check("D3 twelve more uses with a dead minion of each side: both are raised", seen.SetEquals(new[] { Croc, Raptor }), string.Join(",", seen));
    Round(g);
    FillBoard(g, boss);
    Check("D4 with a full board it is not offered", !Offered(g));
    var g2 = Fight("ulda_04_nash", shrine: Shrine);
    var shrine = ShrineRules.Of(g2.Player1).FirstOrDefault();
    ToBoss(g2);
    if (shrine != null) Kill(g2, shrine);
    Check("D5 a run shrine destroyed is a corpse in its owner's graveyard (its dormant form takes its slot): Belch never raises it and is not offered",
        shrine != null && ShrineRules.IsCorpse(shrine) && g2.Player1.GraveyardZone.Contains(shrine) && ShrineRules.Of(g2.Player1).Any(ShrineRules.IsDormantForm)
        && DungeonRunEncounters.TombsA.BelchCorpses(g2.Player2).Count == 0 && DungeonRunEncounters.TombsA.DiedThisGame(g2.Player1).Count == 0 && !Offered(g2));
});

// ============================================================ E  Glack the Scorpid: Venomous Stinger
Guard("Glack", () =>
{
    var g = Fight("ulda_05_glack");
    Controller boss = g.Player2, player = g.Player1;
    var yeti = Board(g, player, Yeti);
    ToBoss(g);
    var own = Board(g, boss, Croc);
    var targets = PowerTargets(g);
    Check("E1 Venomous Stinger costs (2); it can be aimed at any minion, never a hero", Cost("ULDA_BOSS_05p") == 2 && targets.Count == 2 && targets.Contains(yeti) && targets.Contains(own));
    bool used = Power(g, yeti);
    Check("E2 it stings the minion (ULDA_BOSS_05e) and does nothing else yet", used && Has(yeti, "ULDA_BOSS_05e") && yeti.Zone == player.BoardZone && yeti.Health == 5 && boss.UsedMana == 2);
    Round(g);
    targets = PowerTargets(g);
    Check("E3 a stung minion is no longer a target", !targets.Contains(yeti) && targets.Contains(own), string.Join(",", targets.Select(t => t.Card.Id)));
    Hit(g, yeti, 1);
    Check("E4 the stung minion takes 1 damage and is destroyed", yeti.Zone != player.BoardZone && player.GraveyardZone.Contains(yeti));
    var g2 = Fight("ulda_05_glack", shrine: Shrine);
    var shrine = ShrineRules.Of(g2.Player1).FirstOrDefault();
    ToBoss(g2);
    bool stung = shrine != null && Power(g2, shrine);
    if (stung) Hit(g2, shrine, 1);
    Check("E5 a stung run shrine that takes damage is destroyed as a shrine is: its dormant form takes its slot", stung && ShrineRules.IsCorpse(shrine)
        && ShrineRules.Of(g2.Player1).Count(ShrineRules.IsDormantForm) == 1);
});

// ============================================================ F  Mister Chu: "Confiscated" Goods
Guard("Chu", () =>
{
    var g = Fight("ulda_06_chu");
    Controller boss = g.Player2, player = g.Player1;
    Check("F1 \"Confiscated\" Goods is passive: started at match start, never an option", PassiveStarted(g, "ULDA_BOSS_06p"));
    var wisp = Board(g, player, Wisp);
    var yeti = Board(g, player, Yeti);
    ToBoss(g);
    Cast(g, WarAxe);
    var axe = boss.Hero.Weapon;
    bool hit = HeroAttack(g, yeti);
    Check("F2 the boss's hero attacks a minion that survives: no new weapon", axe != null && hit && yeti.Zone == player.BoardZone && boss.Hero.Weapon == axe && axe.Durability == 1);
    Round(g);
    var before = boss.Hero.Weapon;
    bool kill = HeroAttack(g, wisp);
    var got = boss.Hero.Weapon;
    var pool = DungeonRunEncounters.TombsA.ConfiscatedWeapons(g, boss, boss.Hero.HeroPower);
    Check("F3 it attacks and kills a minion with the axe's last Durability: the broken axe is replaced by a random scripted weapon (a new entity) of the pool the generation filter allows, which the death pass keeps",
        kill && wisp.Zone != player.BoardZone && got != null && got != before && got.Card.Implemented && got.Card.Type == CardType.WEAPON && pool.Any(c => c.Id == got.Card.Id) && pool.Count > 20
        && boss.GraveyardZone.Contains(before) && got.Durability > 0, got?.Card.Id + " pool " + pool.Count);
    var kinds = new HashSet<string>();
    for (int i = 0; i < 8; i++)
    {
        Round(g);
        foreach (var m in player.BoardZone.ToArray()) { player.BoardZone.Remove(m); player.SetasideZone.Add(m); }
        var w = Board(g, player, Wisp);
        HeroAttack(g, w);
        if (boss.Hero.Weapon != null) kinds.Add(boss.Hero.Weapon.Card.Id);
    }
    Check("F4 eight more kills: weapons of several kinds, every one scripted", kinds.Count >= 4 && kinds.All(id => C(id).Implemented), string.Join(",", kinds));
    ToPlayer(g);
    var theirs = Board(g, boss, Wisp);
    var weaponBefore = boss.Hero.Weapon;
    Cast(g, WarAxe);
    HeroAttack(g, theirs);
    Check("F5 the run player's hero killing a minion gives the boss nothing", theirs.Zone != boss.BoardZone && boss.Hero.Weapon == weaponBefore);
});

// ============================================================ G  Squeamlish: Druid of the Rat
Guard("Squeamlish", () =>
{
    var g = Fight("ulda_07_squeamlish");
    Controller boss = g.Player2, player = g.Player1;
    var theirs = Board(g, player, Yeti);
    ToBoss(g);
    Check("G1 Druid of the Rat costs (2); with no minion of the boss's it is not offered", Cost("ULDA_BOSS_07p") == 2 && Clickable(g) && !Offered(g));
    var a = Board(g, boss, Croc); var b = Board(g, boss, Raptor);
    bool used = Power(g);
    Check("G2 it gives each of the boss's minions +1/+1 (ULDA_BOSS_07e); the run player's get nothing", used && a.AttackDamage == 3 && a.Health == 4 && b.AttackDamage == 4 && b.Health == 3
        && Has(a, "ULDA_BOSS_07e") && !Has(theirs, "ULDA_BOSS_07e") && theirs.AttackDamage == 4 && boss.UsedMana == 2);
    Check("G3 untargeted", PowerTargets(g).Count == 0);
});

// ============================================================ H  Sinkhole: Sand Surge (cast by rule) and Sand Trap
Guard("Sinkhole", () =>
{
    // The close-out review: the wiki marks Sand Surge Auto-cast and the card carries AI_MUST_PLAY, so it is cast by rule (family
    // 09's AutoCast) at the start of the boss's turn, after the draw, never offered as a click.
    var g = Fight("ulda_08_sinkhole", playerDeck: Croc);
    Controller boss = g.Player2, player = g.Player1;
    ToPlayer(g);
    int deck = player.DeckZone.Count, bossDeck = boss.DeckZone.Count;
    EndTurn(g);
    Check("H1 Sand Surge costs (1) and is cast by rule (passive in the registry, never an option; the card carries AI_MUST_PLAY): at the boss's turn start one Sand Trap joins the run player's deck, (1) is paid; the boss's deck loses only its draw",
        Cost("ULDA_BOSS_08p") == 1 && PassiveStarted(g, "ULDA_BOSS_08p") && C("ULDA_BOSS_08p")[(GameTag)367] == 1 && !Offered(g) && DungeonRunEncounters.AutoCasts(boss.Hero.HeroPower) == 1
        && player.DeckZone.Count == deck + 1 && player.DeckZone.Count(p => p.Card.Id == SandTrap) == 1 && boss.DeckZone.Count == bossDeck - 1 && boss.UsedMana == 1,
        "casts " + DungeonRunEncounters.AutoCasts(boss.Hero.HeroPower) + " deck " + deck + "->" + player.DeckZone.Count + " boss deck " + bossDeck + "->" + boss.DeckZone.Count + " used " + boss.UsedMana);
    var (view, viewBoss, viewPlayer) = FairView(g);
    Check("H2 the boss's fair view keeps the run player's deck masked, the Sand Trap too (the boss's search never learns where it is or what else is there): as long as the real deck, no card shown",
        viewPlayer.DeckZone.Count == player.DeckZone.Count && !viewPlayer.DeckZone.Any(p => p.Card.Id == SandTrap || p.Card.Id == Croc) && player.DeckZone.Any(p => p.Card.Id == Croc),
        viewPlayer.DeckZone.Count(p => p.Card.Id == SandTrap) + " traps shown");
    Round(g);
    Check("H3 cast again at the boss's next turn start: a second Sand Trap", player.DeckZone.Count(p => p.Card.Id == SandTrap) == 2 && DungeonRunEncounters.AutoCasts(boss.Hero.HeroPower) == 2);
    ToPlayer(g);
    var trap = player.DeckZone.First(p => p.Card.Id == SandTrap);
    int d = player.DeckZone.Count, hand = player.HandZone.Count, grave = player.GraveyardZone.Count;
    Generic.Draw(player, trap);
    g.DeathProcessingAndAuraUpdate();
    int traps = player.DeckZone.Count(p => p.Card.Id == SandTrap);
    Check("H4 the run player draws a Sand Trap: it is cast (not kept), the top 2 cards of the deck go to the graveyard, then a card is drawn",
        trap.Zone != player.HandZone && trap.Zone != player.DeckZone && player.GraveyardZone.Count == grave + 2 && player.GraveyardZone.Skip(grave).All(p => p.Card.Id == Croc || p.Card.Id == SandTrap)
        && player.HandZone.Count == hand + 1 && player.DeckZone.Count == d - 4, "deck " + d + "->" + player.DeckZone.Count + " grave +" + (player.GraveyardZone.Count - grave) + " hand +" + (player.HandZone.Count - hand));
    var (view2, _, viewPlayer2) = FairView(g);
    Check("H5 in the boss's view the two removed cards are known in the graveyard, the deck is as long as the real one", viewPlayer2.GraveyardZone.Count == player.GraveyardZone.Count
        && viewPlayer2.GraveyardZone.Count(p => p.Card.Id == Croc) == player.GraveyardZone.Count(p => p.Card.Id == Croc) && viewPlayer2.DeckZone.Count == player.DeckZone.Count);
    var g3 = Fight("ulda_08_sinkhole", playerDeck: Croc);
    var p3 = g3.Player1;
    while (p3.DeckZone.Count > 1) { var x = p3.DeckZone.TopCard; p3.DeckZone.Remove(x); p3.SetasideZone.Add(x); }
    var lone = Entity.FromCard(p3, C(SandTrap));
    Generic.ShuffleIntoDeck(p3, p3.Hero, lone);
    int handBefore = p3.HandZone.Count;
    Generic.Draw(p3, lone);
    g3.DeathProcessingAndAuraUpdate();
    Check("H6 with one card under it, that card is removed and the draw finds an empty deck (fatigue)", p3.DeckZone.Count == 0 && p3.Hero.Fatigue == 1 && p3.HandZone.Count == handBefore,
        "fatigue " + p3.Hero.Fatigue);
    var g4 = Fight("ulda_08_sinkhole");
    ToPlayer(g4);
    while (!g4.Player1.DeckZone.IsFull) Entity.FromCard(g4.Player1, C(Yeti), zone: g4.Player1.DeckZone);
    EndTurn(g4);
    Check("H7 with the run player's deck full it is not cast", DungeonRunEncounters.AutoCasts(g4.Player2.Hero.HeroPower) == 0 && !g4.Player1.DeckZone.Any(p => p.Card.Id == SandTrap) && !Offered(g4));
    var g5 = Fight("ulda_08_sinkhole", bossDeck: Croc);
    var b5 = g5.Player2;
    var bossTrap = Entity.FromCard(b5, C(SandTrap));
    Generic.ShuffleIntoDeck(b5, b5.Hero, bossTrap);
    int bd = b5.DeckZone.Count, bg = b5.GraveyardZone.Count;
    Generic.Draw(b5, bossTrap);
    g5.DeathProcessingAndAuraUpdate();
    Check("H8 a Sand Trap in the boss's own deck works the same for the boss (its own top 2 cards)", b5.DeckZone.Count == bd - 4 && b5.GraveyardZone.Count == bg + 2 && g5.Player1.GraveyardZone.Count == 0);
    Check("H9 Sand Trap keeps the client's text and is a Casts When Drawn card", C(SandTrap).Text == factById[SandTrap].GetProperty("text").GetString() && C(SandTrap)[GameTag.TOPDECK] == 1
        && C(SandTrap).Power?.TopdeckTask != null);
});

// ============================================================ I  Demolisher 3V-11: Armor Repairs
Guard("Demolisher", () =>
{
    var g = Fight("ulda_09_demolisher");
    Controller boss = g.Player2;
    ToBoss(g);
    bool a = Power(g);
    int first = boss.Hero.Armor;
    Round(g);
    boss.Hero.Armor = 9;
    Power(g);
    int second = boss.Hero.Armor;
    Round(g);
    boss.Hero.Armor = 10;
    Power(g);
    int third = boss.Hero.Armor;
    Check("I1 Armor Repairs costs (2): 4 Armor from 0", Cost("ULDA_BOSS_09p") == 2 && a && first == 4, first.ToString());
    Check("I2 below 10 Armor (9): 4 more (13)", second == 13, second.ToString());
    Check("I3 at 10 Armor: 2 (12)", third == 12, third.ToString());
});

// ============================================================ J  Terraviss: Well Equipped
Guard("Terraviss", () =>
{
    var g = Fight("ulda_10_terraviss");
    Controller boss = g.Player2;
    ToBoss(g);
    EmptyHand(boss);
    bool used = Power(g);
    var got = boss.HandZone.FirstOrDefault();
    Check("J1 Well Equipped costs (2): one random Rogue spell or weapon joins the boss's hand", Cost("ULDA_BOSS_10p") == 2 && used && boss.HandZone.Count == 1 && got.Card.Class == CardClass.ROGUE
        && (got.Card.Type == CardType.SPELL || got.Card.Type == CardType.WEAPON) && got.Card.Implemented, got?.Card.Id);
    var kinds = new HashSet<string>();
    bool good = true; bool weapon = false, spell = false;
    for (int i = 0; i < 24; i++)
    {
        Round(g);
        EmptyHand(boss);
        Power(g);
        var card = boss.HandZone.FirstOrDefault()?.Card;
        if (card == null || card.Class != CardClass.ROGUE || !card.Implemented || card.IsQuest || card.IsSecret || !(card.Type == CardType.SPELL || card.Type == CardType.WEAPON)) { good = false; kinds.Add("BAD:" + card?.Id); }
        else { kinds.Add(card.Id); weapon |= card.Type == CardType.WEAPON; spell |= card.Type == CardType.SPELL; }
    }
    var pool = DungeonRunEncounters.TombsA.WellEquippedPool(g, boss, boss.Hero.HeroPower);
    Check("J2 twenty-four more uses: always a scripted Rogue spell (never a Quest or a Secret) or weapon, of many kinds, weapons and spells both", good && kinds.Count >= 10 && weapon && spell
        && pool.All(c => c.Class == CardClass.ROGUE && !c.IsSecret && !c.IsQuest) && pool.Any(c => c.Type == CardType.WEAPON), kinds.Count + " kinds, pool " + pool.Count);
    Round(g);
    FillHand(g, boss);
    Check("J3 with a full hand it is not offered", !Offered(g));
    Check("J4 the Rogue pool holds no Secret although the class has some (the generation filter of this row)", C("EX1_133").Implemented && pool.All(c => !c.IsSecret)
        && Cards.All.Any(c => c.Class == CardClass.ROGUE && c.IsSecret && c.Collectible));
});

// ============================================================ K  R.O.B.O.L.D.: Unstable Weaponry
Guard("Robold", () =>
{
    var g = Fight("ulda_11_robold");
    Controller boss = g.Player2, player = g.Player1;
    Check("K1 Unstable Weaponry is passive: started at match start, never an option", PassiveStarted(g, "ULDA_BOSS_11p"));
    var yeti = Board(g, player, Yeti);
    ToBoss(g);
    int Damage() => player.Hero.Damage + player.BoardZone.Sum(m => m.Damage);
    int before = Damage();
    Play(g, ToHand(g, boss, Yeti));
    int afterYeti = Damage();
    Play(g, ToHand(g, boss, AnnoyOTron));
    int afterMech = Damage();
    Check("K2 the boss plays a Mech: 2 damage to a random enemy (the run player's hero or a minion); a minion that is no Mech does nothing", afterYeti == before && afterMech == before + 2,
        before + ">" + afterYeti + ">" + afterMech);
    Board(g, boss, Mechwarper);
    g.ProcessTasks();
    Check("K3 a Mech summoned (not played) counts too", Damage() == before + 4, Damage().ToString());
    ToPlayer(g);
    int mine = Damage(); int bossSide = boss.Hero.Damage + boss.BoardZone.Sum(m => m.Damage);
    Play(g, ToHand(g, player, AnnoyOTron));
    Check("K4 a Mech of the run player's does nothing", Damage() == mine && boss.Hero.Damage + boss.BoardZone.Sum(m => m.Damage) == bossSide);
});

// ============================================================ L  Pyramad: Brick by Brick
Guard("Pyramad", () =>
{
    var g = Fight("ulda_12_pyramad");
    Controller boss = g.Player2;
    ToBoss(g);
    EmptyHand(boss);
    bool used = Power(g);
    var choice = boss.Choice;
    var offers = choice?.Choices.Select(id => g.IdEntityDic[id]).ToList() ?? new List<IPlayable>();
    Check("L1 Brick by Brick costs (3): a Discover of three distinct Elementals of the boss's class (Shaman) or neutral", Cost("ULDA_BOSS_12p") == 3 && used && offers.Count == 3
        && offers.Select(p => p.Card.Id).Distinct().Count() == 3 && offers.All(p => p.Card.IsRace(Race.ELEMENTAL) && p.Card.Implemented && (p.Card.Class == CardClass.SHAMAN || p.Card.Class == CardClass.NEUTRAL)),
        string.Join(",", offers.Select(p => p.Card.Id)));
    var pick = offers.FirstOrDefault();
    bool chose = pick != null && g.Process(ChooseTask.Pick(boss, pick.Id));
    var inHand = boss.HandZone.FirstOrDefault();
    Check("L2 the boss picks: the card joins its hand with \"Bricked\" (ULDA_BOSS_12e) and costs (2) less", chose && inHand != null && inHand.Card.Id == pick.Card.Id && Has(inHand, "ULDA_BOSS_12e")
        && inHand.Cost == Math.Max(0, pick.Card.Cost - 2) && boss.Choice == null, inHand == null ? "none" : inHand.Card.Id + " " + inHand.Cost + "/" + pick.Card.Cost);
    var kinds = new HashSet<string>();
    for (int i = 0; i < 10; i++)
    {
        Round(g);
        EmptyHand(boss);
        Power(g);
        foreach (int id in boss.Choice?.Choices ?? new List<int>()) kinds.Add(g.IdEntityDic[id].Card.Id);
        if (boss.Choice != null) g.Process(ChooseTask.Pick(boss, boss.Choice.Choices[0]));
    }
    var pool = DungeonRunEncounters.TombsA.BrickPool(g, boss, boss.Hero.HeroPower);
    Check("L3 ten more uses: many kinds offered, every one of the pool (scripted Shaman or neutral Elementals the generation filter allows)", kinds.Count >= 8 && kinds.All(id => pool.Any(c => c.Id == id))
        && pool.All(c => c.IsRace(Race.ELEMENTAL) && (c.Class == CardClass.SHAMAN || c.Class == CardClass.NEUTRAL)), kinds.Count + " kinds, pool " + pool.Count);
    Round(g);
    FillHand(g, boss);
    Check("L4 with a full hand it is not offered", !Offered(g));
});

// ============================================================ M  Snakeflinger Scalesnout: Snakeshot
Guard("Scalesnout", () =>
{
    var g = Fight("ulda_13_scalesnout");
    Controller boss = g.Player2, player = g.Player1;
    var yeti = Board(g, player, Yeti);
    ToBoss(g);
    bool used = Power(g);
    var snake = boss.BoardZone.FirstOrDefault();
    Check("M1 Snakeshot costs (2): a 1/1 Snake (EX1_554t) with Poisonous (ULDA_BOSS_13e)", Cost("ULDA_BOSS_13p") == 2 && used && snake != null && snake.Card.Id == "EX1_554t" && snake.AttackDamage == 1
        && snake.Health == 1 && snake.Poisonous && Has(snake, "ULDA_BOSS_13e"));
    Round(g);
    bool hit = Attack(g, snake, yeti);
    Check("M2 the Snake attacks a 4/5: the Poisonous Snake destroys it", hit && yeti.Zone != player.BoardZone);
    Round(g);
    FillBoard(g, boss);
    Check("M3 with a full board it is not offered", !Offered(g));
});

// ============================================================ N  Sword Dancer Sirinell: Sword Dance
Guard("Sirinell", () =>
{
    var g = Fight("ulda_14_sirinell", bossDeck: Croc);
    Controller boss = g.Player2, player = g.Player1;
    Check("N1 Sword Dance is passive: started at match start, never an option", PassiveStarted(g, "ULDA_BOSS_14p"));
    ToBoss(g);
    Cast(g, WarAxe);
    var axe = boss.Hero.Weapon;
    Cast(g, DeadlyPoison);
    int deck = boss.DeckZone.Count;
    bool poisoned = axe != null && axe.AttackDamage == 5 && Has(axe, DeadlyPoisonE);
    Cast(g, Reaper);
    var copy = boss.DeckZone.FirstOrDefault(p => p.Card.Id == WarAxe) as Weapon;
    Check("N2 a weapon of the boss's destroyed by a new one: a copy with its enchantments (Deadly Poison: 5 Attack) and full Durability is shuffled into the boss's deck",
        poisoned && boss.Hero.Weapon?.Card.Id == Reaper && boss.DeckZone.Count == deck + 1 && copy != null && copy != axe && Has(copy, DeadlyPoisonE) && copy.AttackDamage == 5
        && copy.Durability == C(WarAxe)[GameTag.DURABILITY], copy == null ? "none" : copy.AttackDamage + "/" + copy.Durability);
    var ledger = OwnDeckLedger.Observe(g, boss, null).Sample(g, boss, 11);
    Check("N2b the boss's own-deck ledger holds the shuffled copy (a public shuffle of its own card) and exactly its deck's cards", ledger.TryGetValue(WarAxe, out int axes) && axes == 1
        && ledger.Values.Sum() == boss.DeckZone.Count, string.Join(",", ledger.Select(p => p.Key + "x" + p.Value)));
    HeroAttack(g, player.Hero);
    Round(g);
    int deck2 = boss.DeckZone.Count;
    HeroAttack(g, player.Hero);
    Check("N3 a weapon out of Durability (Arcanite Reaper after its second attack) is shuffled back too", boss.Hero.Weapon == null && boss.DeckZone.Count == deck2 + 1
        && boss.DeckZone.Count(p => p.Card.Id == Reaper) == 1, "deck " + deck2 + "->" + boss.DeckZone.Count);
    var drawn = Generic.Draw(boss, copy);
    bool replay = drawn == copy && Play(g, copy);
    Check("N4 the shuffled copy drawn and played is the poisoned weapon (5 Attack)", replay && boss.Hero.Weapon == copy && boss.Hero.Weapon.AttackDamage == 5);
    ToPlayer(g);
    Cast(g, WarAxe);
    int bossDeck = boss.DeckZone.Count, playerDeck = player.DeckZone.Count;
    Cast(g, Reaper);
    Check("N5 the run player's weapon destroyed goes into no deck", boss.DeckZone.Count == bossDeck && player.DeckZone.Count == playerDeck);
    var g2 = Fight("ulda_14_sirinell");
    ToBoss(g2);
    while (!g2.Player2.DeckZone.IsFull) Entity.FromCard(g2.Player2, C(Yeti), zone: g2.Player2.DeckZone);
    Cast(g2, WarAxe);
    Cast(g2, Reaper);
    Check("N6 with the boss's deck full the copy is lost (nothing breaks)", g2.Player2.Hero.Weapon?.Card.Id == Reaper && g2.Player2.DeckZone.All(p => p.Card.Id == Yeti));
    // A Durability buff is kept with its enchantment: Captain Greenskin's +1/+1 (NEW1_024o) on a War Axe that lost Durability.
    var g3 = Fight("ulda_14_sirinell", bossDeck: Croc);
    Controller b3 = g3.Player2;
    ToBoss(g3);
    Cast(g3, WarAxe);
    var buffed = b3.Hero.Weapon;
    Play(g3, ToHand(g3, b3, "NEW1_024"));
    HeroAttack(g3, g3.Player1.Hero);
    bool greenskin = buffed != null && Has(buffed, "NEW1_024o") && buffed.AttackDamage == 4 && buffed.Durability == 2 && buffed[GameTag.DURABILITY] == 3;
    Cast(g3, Reaper);
    var kept = b3.DeckZone.FirstOrDefault(p => p.Card.Id == WarAxe) as Weapon;
    Check("N7 a weapon buffed by Captain Greenskin (+1/+1) that lost a Durability point: its copy keeps the enchantment's +1 Attack and +1 Durability, and no damage (4/3, not 4/2)",
        greenskin && kept != null && Has(kept, "NEW1_024o") && kept.AttackDamage == 4 && kept.Durability == 3 && kept.Damage == 0,
        kept == null ? "none" : kept.AttackDamage + "/" + kept.Durability + " dmg " + kept.Damage);
    var drawn3 = Generic.Draw(b3, kept);
    bool equipped3 = drawn3 == kept && Play(g3, kept);
    Check("N8 drawn and equipped, it is a 4/3", equipped3 && b3.Hero.Weapon == kept && kept.AttackDamage == 4 && kept.Durability == 3);
});

// ============================================================ O  Lt. Herring: Puffertrooper
Guard("Herring", () =>
{
    var g = Fight("ulda_15_herring");
    Controller boss = g.Player2, player = g.Player1;
    var theirs = Board(g, player, MurlocRaider);
    var yeti = Board(g, player, Yeti);
    ToBoss(g);
    var croc = Board(g, boss, Croc);
    Check("O1 Puffertrooper costs (2); with no Murloc of the boss's it is not offered (the run player's do not count)", Cost("ULDA_BOSS_15p") == 2 && !Offered(g));
    var murloc = Board(g, boss, MurlocRaider);
    bool attackBefore = murloc.CanAttack;
    bool used = Power(g);
    Check("O2 it gives the boss's Murlocs Rush (ULDA_BOSS_15e); the other minions and the run player's Murloc get nothing", used && murloc.IsRush && Has(murloc, "ULDA_BOSS_15e") && !croc.IsRush
        && !theirs.IsRush && !Has(theirs, "ULDA_BOSS_15e") && boss.UsedMana == 2);
    Check("O3 the Murloc summoned this turn can now attack a minion (Rush)", !attackBefore && Attack(g, murloc, yeti) && yeti.Damage == 2);
    Round(g);
    Check("O4 with every Murloc of the boss's already holding Rush it is not offered", !Offered(g));
});

// ============================================================ P  Battrund: Under Wraps
Guard("Battrund", () =>
{
    var g = Fight("ulda_16_battrund");
    Controller boss = g.Player2, player = g.Player1;
    Check("P1 Under Wraps is passive: started at match start, never an option", PassiveStarted(g, "ULDA_BOSS_16p"));
    var theirs = (Minion)ToHand(g, player, Croc);
    Play(g, theirs);
    ToBoss(g);
    var yeti = (Minion)ToHand(g, boss, Yeti);
    Play(g, yeti);
    var summoned = Board(g, boss, Raptor);
    Check("P2 a minion the boss plays gets Reborn (ULDA_BOSS_16e); one summoned without being played and the run player's do not", yeti[GameTag.REBORN] == 1 && Has(yeti, "ULDA_BOSS_16e")
        && summoned[GameTag.REBORN] == 0 && theirs[GameTag.REBORN] == 0);
    Kill(g, yeti);
    var back = boss.BoardZone.FirstOrDefault(m => m.Card.Id == Yeti);
    Check("P3 it dies and comes back with 1 Health and no Reborn", back != null && back.Health == 1 && back[GameTag.REBORN] == 0);
    var silenced = (Minion)ToHand(g, boss, Croc);
    Play(g, silenced);
    new SilenceTask(EntityType.TARGET).Process(g, player, player.Hero, silenced, null);
    g.DeathProcessingAndAuraUpdate();
    Check("P4 Silence removes it", silenced[GameTag.REBORN] == 0 && !Has(silenced, "ULDA_BOSS_16e"));
});

// ============================================================ Q  Ichabod the Cursed: Spreading the Curse
Guard("Ichabod", () =>
{
    var g = Fight("ulda_17_ichabod");
    Controller boss = g.Player2, player = g.Player1;
    var yeti = Board(g, player, Yeti);
    ToBoss(g);
    bool used = Power(g);
    var gnome = boss.BoardZone.FirstOrDefault();
    Check("Q1 Spreading the Curse costs (2): a Leper Gnome (EX1_029) with Rush (ULDA_BOSS_17e)", Cost("ULDA_BOSS_17p") == 2 && used && gnome != null && gnome.Card.Id == "EX1_029" && gnome.IsRush
        && Has(gnome, "ULDA_BOSS_17e"));
    int hp = player.Hero.Health;
    Check("Q2 it can attack a minion at once, not the hero", !g.Player2.Options().OfType<MinionAttackTask>().Any(o => o.Source == gnome && o.Target == player.Hero) && Attack(g, gnome, yeti));
    g.DeathProcessingAndAuraUpdate();
    Check("Q3 its Deathrattle hits the run player's hero for 2", gnome.Zone != boss.BoardZone && player.Hero.Health == hp - 2, hp + "->" + player.Hero.Health);
    Round(g);
    FillBoard(g, boss);
    Check("Q4 with a full board it is not offered", !Offered(g));
});

// ============================================================ R  Twizzleflux: Plagued Horde
Guard("Twizzleflux", () =>
{
    var g = Fight("ulda_18_twizzleflux");
    Controller boss = g.Player2, player = g.Player1;
    var theirs = Board(g, player, Croc);
    ToBoss(g);
    var own = Board(g, boss, Yeti);
    var targets = PowerTargets(g);
    Check("R1 Plagued Horde costs (2); any minion is a target, never a hero", Cost("ULDA_BOSS_18p") == 2 && targets.Count == 2 && targets.Contains(own) && targets.Contains(theirs));
    bool used = Power(g, own);
    int hp = player.Hero.Health;
    Kill(g, own);
    Check("R2 the boss's minion with \"Deathrattle: Deal 2 damage to the enemy hero\" (ULDA_BOSS_18e) dies: the run player's hero takes 2", used && player.Hero.Health == hp - 2, hp + "->" + player.Hero.Health);
    Round(g);
    Power(g, theirs);
    int bossHp = boss.Hero.Health; hp = player.Hero.Health;
    Kill(g, theirs);
    Check("R3 on the run player's minion it hits the boss's hero (the enemy of the minion's controller)", boss.Hero.Health == bossHp - 2 && player.Hero.Health == hp, bossHp + "->" + boss.Hero.Health);
    Round(g);
    var twice = Board(g, boss, Yeti);
    Power(g, twice); Round(g); Power(g, twice);
    hp = player.Hero.Health;
    Kill(g, twice);
    Check("R4 given twice, it deals 2 twice", player.Hero.Health == hp - 4, hp + "->" + player.Hero.Health);
});

// ============================================================ S  the rows, the tier rule, the admission rows, the validator
Guard("Rows", () =>
{
    var errors = DungeonRunValidator.Validate(data, null);
    Check("S1 the shipped data validates with the eighteen family 19 rows", errors.Count == 0, string.Join(" | ", errors.Take(4)));
    // (row, n, name, tier, wiki level, deck kind, chapter)
    var rows = new (string id, string n, string name, string tier, int level, string kind, int chapter)[]
    {
        ("ulda_01_cardish", "01", "Waste Wanderer Cardish", "4", 7, "substituted", 2),
        ("ulda_02_zafarr", "02", "Dark Ritualist Zafarr", "3", 6, "substituted", 3),
        ("ulda_03_vera", "03", "Vera Ridley", "2", 4, "extended", 4),
        ("ulda_04_nash", "04", "Nash the Greatworm", "3", 6, "authentic", 2),
        ("ulda_05_glack", "05", "Glack the Scorpid", "2", 4, "extended", 3),
        ("ulda_06_chu", "06", "Mister Chu", "4", 7, "substituted", 3),
        ("ulda_07_squeamlish", "07", "Squeamlish", "4", 7, "substituted", 2),
        ("ulda_08_sinkhole", "08", "Sinkhole", "3", 6, "substituted", 1),
        ("ulda_09_demolisher", "09", "Demolisher 3V-11", "2", 4, "extended", 2),
        ("ulda_10_terraviss", "10", "Terraviss", "2", 3, "extended", 3),
        ("ulda_11_robold", "11", "R.O.B.O.L.D.", "2", 4, "extended", 2),
        ("ulda_12_pyramad", "12", "Pyramad", "2", 4, "extended", 4),
        ("ulda_13_scalesnout", "13", "Snakeflinger Scalesnout", "1", 2, "extended", 3),
        ("ulda_14_sirinell", "14", "Sword Dancer Sirinell", "3", 6, "authentic", 4),
        ("ulda_15_herring", "15", "Lt. Herring", "1", 2, "extended", 1),
        ("ulda_16_battrund", "16", "Battrund", "2", 4, "extended", 4),
        ("ulda_17_ichabod", "17", "Ichabod the Cursed", "2", 4, "extended", 3),
        ("ulda_18_twizzleflux", "18", "Twizzleflux", "2", 4, "extended", 1),
    };
    Check("S2 family 19 holds exactly these eighteen rows, appended after the earlier families' rows", data.Encounters.Where(b => b.Family == "19").Select(b => b.Id)
        .SequenceEqual(rows.Select(r => r.id))
        // wave 6: later families' rows follow (families 20-24): the family's rows are one block and only later families come after it
        && data.Encounters.SkipWhile(b => b.Family != "19").TakeWhile(b => b.Family == "19").Count() == rows.Length
        && data.Encounters.SkipWhile(b => b.Family != "19").SkipWhile(b => b.Family == "19").All(b => string.CompareOrdinal(b.Family, "19") > 0),
        string.Join(",", data.Encounters.Where(b => b.Family == "19").Select(b => b.Id)));
    var chapterTag = new Dictionary<int, GameTag> { { 1, (GameTag)1202 }, { 2, (GameTag)1203 }, { 3, (GameTag)1204 }, { 4, (GameTag)1205 } };
    foreach (var r in rows)
    {
        var b = data.Boss(r.id);
        var plan = RowPlan(r.id);
        string hero = "ULDA_BOSS_" + r.n + "h", power = "ULDA_BOSS_" + r.n + "p";
        var cfg = new GameConfig { RandomSeed = 5, Shuffle = true, SkipMulligan = false, History = false, Logging = false, FormatType = FormatType.FT_WILD };
        DungeonRunSetup.Configure(cfg, plan);
        // The tier rule (verify/tools/gen/p8f19/tombs_tiers.py): level 1-2 tier 1, 3-4 tier 2 (the 2-3 table takes 3), 5-6 tier 3, 7 tier 4.
        // The shipped row's recorded level is read from the data, so a wrong level in tombs_tiers.py or make_f19.py fails here.
        int shipped = b.LevelOriginal;
        string ruleTier = shipped <= 2 ? "1" : shipped <= 4 ? "2" : shipped <= 6 ? "3" : "4";
        Check("S3 " + r.id + ": a Tombs row of tier " + r.tier + " (wiki level " + r.level + ", chapter " + r.chapter + ") whose plan is the row's (hero, power, a 30-card " + r.kind
            + " deck, the act curve's Health, not the printed 10)",
            b.Provenance == "ULDA" && b.Name == r.name && b.Tier == r.tier && b.LevelSource == "wiki-level-table" && shipped == r.level && ruleTier == r.tier && b.HeroCard == hero && b.Power == power && b.Source.Kind == r.kind && b.Flags.Count == 0
            && plan.EncounterId == r.id && plan.EncounterDeck && plan.BossHero.Id == hero && plan.BossHeroPowerId == power && plan.BossDeck.Count == 30 && data.BossDecks[b.Deck].Count == 30
            && !b.FixedHealth && b.Health == null && plan.BossHealth == 30 && C(hero).Type == CardType.HERO && C(hero).Health == 10 && C(hero)[chapterTag[r.chapter]] > 0
            && C(power).Type == CardType.HERO_POWER && C(hero).Name == r.name && data.Schedules.Member(b, DungeonRunEncounterData.PoolOf(b.Tier))
            && b.Source.Url.StartsWith("https://hearthstone.wiki.gg/wiki/") && !b.BossGeneratedSecrets && b.Forms.Count == 0 && b.Start == null && b.Pools.SequenceEqual(new[] { "enc-t" + r.tier }),
            b.Source.Kind + " health " + plan.BossHealth + " tier " + b.Tier + " level " + b.LevelSource + ":" + b.LevelOriginal + " pools " + string.Join(",", b.Pools));
    }
    // The five passive powers and Sand Surge, cast by rule since the close-out review (family 09's AutoCast registers it as a passive).
    string[] passives = { "ULDA_BOSS_03p", "ULDA_BOSS_06p", "ULDA_BOSS_08p", "ULDA_BOSS_11p", "ULDA_BOSS_14p", "ULDA_BOSS_16p" };
    string[] powers = rows.Select(r => "ULDA_BOSS_" + r.n + "p").ToArray();
    string[] enchantments = { "ULDA_BOSS_01e", "ULDA_BOSS_03e", "ULDA_BOSS_05e", "ULDA_BOSS_07e", "ULDA_BOSS_12e", "ULDA_BOSS_13e", "ULDA_BOSS_15e", "ULDA_BOSS_16e", "ULDA_BOSS_17e", "ULDA_BOSS_18e" };
    var scripted = powers.Concat(new[] { SandTrap }).ToArray();
    var unscripted = scripted.Concat(enchantments).Where(id => !C(id).Implemented || DungeonRunEncounters.Find(id)?.Family != "19").ToList();
    var retexted = scripted.Concat(enchantments).Where(id => C(id).Text != factById[id].GetProperty("text").GetString()).ToList();
    Check("S4 every power, Sand Trap and every enchantment of the family is scripted by family 19 and keeps the client's text (18 powers, 1 card, 10 enchantments)", unscripted.Count == 0
        && retexted.Count == 0 && scripted.Length == 19, string.Join(",", unscripted.Concat(retexted)));
    Check("S5 the five passive powers and the rule-cast Sand Surge are the registry's passive ones (never an option), the twelve others clickable; each power's Cost is the client's",
        powers.All(p => DungeonRunEncounters.PassivePowers.Contains(p) == passives.Contains(p) && DungeonRunEncounters.Find(p).Kind == (passives.Contains(p) ? EncounterCardKind.Passive : EncounterCardKind.Power)
            && C(p).Cost == (factById[p].GetProperty("tags").EnumerateArray().Where(t => t[0].GetInt32() == 48).Select(t => t[1].GetInt32()).FirstOrDefault())));
    var expectFamily = new Dictionary<string, string>
    {
        ["ULDA_BOSS_01p"] = "boss-tombs-board-power", ["ULDA_BOSS_05p"] = "boss-tombs-board-power", ["ULDA_BOSS_07p"] = "boss-tombs-board-power", ["ULDA_BOSS_09p"] = "boss-tombs-board-power",
        ["ULDA_BOSS_13p"] = "boss-tombs-board-power", ["ULDA_BOSS_15p"] = "boss-tombs-board-power", ["ULDA_BOSS_17p"] = "boss-tombs-board-power", ["ULDA_BOSS_18p"] = "boss-tombs-board-power",
        ["ULDA_BOSS_02p"] = "boss-tombs-graveyard-power", ["ULDA_BOSS_04p"] = "boss-tombs-graveyard-power", ["ULDA_BOSS_10p"] = "boss-tombs-own-hand-power", ["ULDA_BOSS_12p"] = "boss-tombs-own-hand-power",
        ["ULDA_BOSS_03p"] = "boss-tombs-passive", ["ULDA_BOSS_06p"] = "boss-tombs-passive", ["ULDA_BOSS_11p"] = "boss-tombs-passive", ["ULDA_BOSS_14p"] = "boss-tombs-passive",
        ["ULDA_BOSS_16p"] = "boss-tombs-passive", ["ULDA_BOSS_08p"] = "boss-tombs-player-deck-insert", [SandTrap] = "boss-tombs-card",
    };
    var wrong = scripted.Where(id =>
    {
        var v = DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss, null, catalogRun: true);
        bool card = id == SandTrap;
        return !(v.Admitted && v.Subject && v.Rule?.Ids != null && v.Rule.Ids.Contains(id) && DungeonRunAdmission.RuleFor(id)?.Family == expectFamily[id]
            && DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss).Admitted
            && DungeonRunAdmission.Classify(C(id), id, AdmitScope.Player, null, catalogRun: true).Admitted == card
            && !DungeonRunAdmission.Classify(C(id), id, AdmitScope.Generated, null, catalogRun: true).Admitted
            && DungeonRunAdmission.GeneratedRejects(true).Contains(id) && DungeonRunAdmission.GeneratedRejects(false).Contains(id));
    }).ToList();
    Check("S6 each has its own admission row of its family: the powers a boss's only, Sand Trap a boss's and the run player's (it lives in the run player's deck); none is ever generated",
        wrong.Count == 0 && expectFamily.Count == 19, string.Join(",", wrong));
    var playerPower = DungeonRunAdmission.Classify(C("ULDA_BOSS_08p"), "ULDA_BOSS_08p", AdmitScope.Player, null, catalogRun: true);
    Check("S7 a family 19 power asked for the run player is refused with its row's reason", !playerPower.Admitted && playerPower.Family == "boss-tombs-player-deck-insert"
        && playerPower.Lift == DungeonRunAdmission.NoneBossPower);
    string Subs(string id) => string.Join(",", data.Boss(id).Source.Substitutions.Where(x => x.Listed != null).Select(x => x.Listed + ">" + (x.Used ?? "-") + ":" + x.Reason).Distinct().OrderBy(x => x, StringComparer.Ordinal));
    var replaced = rows.SelectMany(r => data.Boss(r.id).Source.Substitutions).Where(x => x.Listed != null).ToList();
    Check("S8 the listed cards replaced are only cards the engine does not script (or Hunter's Pack, which could give a boss a Secret), each with its named replacement",
        replaced.All(x => x.Used != null && (x.Reason == "unscripted" && !C(x.Listed).Implemented || x.Listed == "ULD_429" && x.Reason.StartsWith("admission:")))
        && Subs("ulda_03_vera") == "ULD_160>DAL_614:unscripted,ULD_170>DAL_606:unscripted,ULD_184>EX1_029:unscripted,ULD_708>DAL_739:unscripted"
        && Subs("ulda_02_zafarr") == "ULD_214>ULD_206:unscripted,ULD_723>ULD_217:unscripted" && Subs("ulda_04_nash") == "" && Subs("ulda_14_sirinell") == "",
        Subs("ulda_03_vera") + " | " + Subs("ulda_02_zafarr") + " | " + Subs("ulda_01_cardish"));
    int In(string boss, string card) => data.BossDecks[data.Boss(boss).Deck].Count(id => id == card);
    Check("S9 the decks keep the wiki's signature cards: Mister Chu's two Mirage Blades (ULD_326t), Demolisher's six Demolishers, Vera's Lackey cards (EVIL Cable Rat, EVIL Genius, Kobold and Goblin Lackey), Zafarr's Reborn minions",
        In("ulda_06_chu", "ULD_326t") == 2 && In("ulda_09_demolisher", "EX1_102") == 6 && In("ulda_03_vera", "DAL_400") == 2 && In("ulda_03_vera", "DAL_606") == 2 && In("ulda_03_vera", KoboldLackey) == 2
        && In("ulda_03_vera", "DAL_739") == 2 && data.BossDecks[data.Boss("ulda_02_zafarr").Deck].Count(id => C(id)[GameTag.REBORN] == 1) == 14,
        "zafarr reborn " + data.BossDecks[data.Boss("ulda_02_zafarr").Deck].Count(id => C(id)[GameTag.REBORN] == 1));
    Check("S10 the tier pools: two rows in enc-t1, nine in enc-t2, four in enc-t3, three in enc-t4", rows.Count(r => r.tier == "1") == 2 && rows.Count(r => r.tier == "2") == 9
        && rows.Count(r => r.tier == "3") == 4 && rows.Count(r => r.tier == "4") == 3);
    DungeonRunData Changed(Action<DungeonRunData> change) { var d = Load(); change(d); return d; }
    var badPower = DungeonRunValidator.Validate(Changed(d => d.Boss("ulda_08_sinkhole").Power = "ULDA_BOSS_08px"), null);
    Check("S11 the validator refuses a row whose power is not scripted (the Heroic Sand Surge ULDA_BOSS_08px)", badPower.Count > 0, string.Join(" | ", badPower.Take(2)));
});

Console.WriteLine($"SearchTelemetry swallowed leaf exceptions {SearchTelemetry.SwallowedLeafErrors}");
Console.WriteLine($"RESULT {passes} passed, {failures + SearchTelemetry.SwallowedLeafErrors} failed");
return failures + SearchTelemetry.SwallowedLeafErrors;
