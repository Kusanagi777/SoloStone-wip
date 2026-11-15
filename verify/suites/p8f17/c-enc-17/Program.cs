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

// Phase 8 family 17, Dalaran Heist bosses D (real engine games through the run setup path: DungeonRun.BuildPlan on the shipped
// row -> DungeonRunSetup.Configure / ApplyAfterStart): Ichoron (Protective Bubble), Lavanthor (Lava Belch, Molten Rock), Zuramat
// the Obliterator (To The Void, Void Shift, the run player's two Void Shifts), Cyanigosa (Uncontrollable Energy), Nozari (Sand
// Breath, cast by rule), Millificent Manastorm (Tinker, Elementium Squirrel Bomb, Thorium Chicken), Lieutenant Sinclari (Hold the
// Gates!, Kirin Tor Guard), Dancin' Deryl (Boogie Woogie), Locksmith Zibb (Spell Lock), Captain Hannigan (Raise the Alarm, cast by
// rule), Queen Wagtoggle (Bribery, cast by rule; Rival Accomplice), Trade Prince Gallywix (Greed is Good), Boommaster Flark
// (Blast Powder, The Wire and The Other Wire on the run player's board), Madam Goya (Blackmail, cast by rule; Extortion, Lotus
// Bruiser, Fruit Slasher) and Infuriated Banker, each with the shrine and fair-view rules that touch it, and the rows, admission
// rows and validator.
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

const string Yeti = "CS2_182", Croc = "CS2_120", Wisp = "CS2_231", Moonfire = "CS2_008", Fireball = "CS2_029", Hoarder = "EX1_096";
const string Coin = "GAME_005", FakeCoin = "CFM_630", Squire = "EX1_008", Shrine = "TRLA_154", Spellstone = "LOOT_064", Bolt = "EX1_238", Trogg = "LOE_018";
const string Apprentice = "EX1_608", Arcane = "CS2_025", Intellect = "CS2_023", Frostbolt = "CS2_024", Tinyfin = "LOEA10_3", Howl = "GILA_BOSS_20e";
const string Rock = DungeonRunEncounters.MoltenRock, VoidShift = DungeonRunEncounters.VoidShiftCard, Voided = EncounterRules.VoidedCard;
const string Bomb = DungeonRunEncounters.SquirrelBomb, Chicken = DungeonRunEncounters.ThoriumChicken, Guard17 = DungeonRunEncounters.KirinTorGuard;
const string Wire = EncounterRules.TheWireCard, OtherWire = EncounterRules.TheOtherWireCard, Extortion = EncounterRules.ExtortionCard;
const string Bruiser = DungeonRunEncounters.LotusBruiser, Slasher = DungeonRunEncounters.FruitSlasher, Banker = DungeonRunEncounters.InfuriatedBanker;
const string Accomplice = DungeonRunEncounters.RivalAccomplice, GCoin = DungeonRunEncounters.GallywixCoin;
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
// hero, power, Health, start block and every other encounter field stay the row's).
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
void EmptyDeck(Controller c) { foreach (var p in c.DeckZone.ToArray()) c.SetasideZone.Add(c.DeckZone.Remove(p)); }
void EmptyHand(Controller c) { foreach (var p in c.HandZone.ToArray()) { c.HandZone.Remove(p); c.SetasideZone.Add(p); } }
void ToDeck(Controller c, string id) => Generic.ShuffleIntoDeck(c, c.Hero, Entity.FromCard(c, C(id)));
Minion Board(Game g, Controller c, string id) { var m = (Minion)Entity.FromCard(c, C(id)); Generic.SummonBlock(g, m, -1, c.Hero); g.DeathProcessingAndAuraUpdate(); return m; }
bool Play(Game g, IPlayable card, ICharacter target = null) { Mana(g.CurrentPlayer); return g.Process(PlayCardTask.Any(g.CurrentPlayer, card, target, -1, 0)); }
bool PlayKeep(Game g, IPlayable card, ICharacter target = null) => g.Process(PlayCardTask.Any(g.CurrentPlayer, card, target, -1, 0));
bool Cast(Game g, string id, ICharacter target = null) => Play(g, ToHand(g, g.CurrentPlayer, id), target);
void EndTurn(Game g) => g.Process(EndTurnTask.Any(g.CurrentPlayer));
bool Offered(Game g) => g.CurrentPlayer.Options().Any(o => o is HeroPowerTask);
List<ICharacter> PowerTargets(Game g) => g.CurrentPlayer.Options().OfType<HeroPowerTask>().Select(o => o.Target).Where(t => t != null).ToList();
bool Power(Game g, ICharacter target = null) => Offered(g) && g.Process(HeroPowerTask.Any(g.CurrentPlayer, target));
// The current player may use its hero power again this turn (a test helper: Mana refreshed, the power ready).
void Again(Game g) { Mana(g.CurrentPlayer); g.CurrentPlayer.Hero.HeroPower.IsExhausted = false; }
void ToBoss(Game g) { if (g.CurrentPlayer == g.Player1) EndTurn(g); Mana(g.Player2); }
void ToPlayer(Game g) { if (g.CurrentPlayer == g.Player2) EndTurn(g); Mana(g.Player1); }
// The boss's turn starts with full Mana (its rule-cast powers fire at that start).
void BossTurnFull(Game g) { if (g.CurrentPlayer == g.Player2) EndTurn(g); Mana(g.Player2); EndTurn(g); }
void Kill(Game g, Minion m) { new DestroyTask(EntityType.TARGET).Process(g, m.Controller, m.Controller.Hero, m, null); g.DeathProcessingAndAuraUpdate(); }
void Hit(Game g, ICharacter target, int amount, IPlayable source) { Generic.DamageCharFunc.Invoke(source, target, amount, false); g.DeathProcessingAndAuraUpdate(); }
int Count(Controller c, string id) => c.BoardZone.Count(m => m.Card.Id == id);
bool Has(IPlayable p, string enchantmentId) => p.AppliedEnchantments?.Any(e => e.Card.Id == enchantmentId) == true;
int Cost(string id) => C(id).Cost;
int Casts(Controller c) => DungeonRunEncounters.AutoCasts(c.Hero.HeroPower);
bool IsPassive(Controller c, string power) => c.Hero.HeroPower.Card.Id == power && c.Hero.HeroPower.IsPassiveHeroPower && c.Hero.HeroPower[EncounterRules.PowerActive] == 1
    && DungeonRunCardsGen.PassiveHeroPowers.Contains(power) && DungeonRunEncounters.PassivePowers.Contains(power);
bool ViewOffered(Game g)
{
    var (view, viewBoss, _) = FairView(g);
    return viewBoss.Options().Any(o => o is HeroPowerTask);
}
(Game view, Controller boss, Controller player) FairView(Game g)
{
    var bot = FairBotState.Create(g, g.Player2.PlayerId, 1, DeckStyle.Tempo);
    var view = bot.FingerprintGame;
    var viewBoss = view.Player1.PlayerId == g.Player2.PlayerId ? view.Player1 : view.Player2;
    return (view, viewBoss, viewBoss.Opponent);
}

// ============================================================ A  Ichoron: Protective Bubble
Guard("Ichoron", () =>
{
    var g = Fight("dala_46_ichoron");
    Controller boss = g.Player2, player = g.Player1;
    var enemy = Board(g, player, Yeti);
    ToBoss(g);
    Check("A0 with no minion of its own Protective Bubble is not offered", !Offered(g));
    var own = Board(g, boss, Croc); var shielded = Board(g, boss, Squire);
    shielded.HasDivineShield = true;
    var targets = PowerTargets(g);
    Check("A1 Protective Bubble costs (1) and is offered only on a minion of the boss without Divine Shield (never an enemy, never a shielded one)",
        Cost("DALA_BOSS_46p") == 1 && targets.Count == 1 && targets[0] == own, string.Join(",", targets.Select(t => t.Card.Id)));
    bool used = Power(g, own);
    Check("A2 it gives that minion Divine Shield", used && own.HasDivineShield && !enemy.HasDivineShield && boss.UsedMana == 1);
    EndTurn(g); EndTurn(g); Mana(boss);
    Check("A3 with every minion of the boss shielded it is not offered", !Offered(g));
});

// ============================================================ B  Lavanthor: Lava Belch, Molten Rock
Guard("Lavanthor", () =>
{
    var g = Fight("dala_47_lavanthor");
    Controller boss = g.Player2, player = g.Player1;
    var yeti = Board(g, player, Yeti);
    ToBoss(g);
    bool used = Power(g);
    var rock = boss.BoardZone.FirstOrDefault(m => m.Card.Id == Rock);
    Check("B1 Lava Belch costs (1) and summons a 0/3 Molten Rock", Cost("DALA_BOSS_47p") == 1 && used && rock != null && rock.AttackDamage == 0 && rock.Health == 3 && boss.UsedMana == 1);
    var croc = Board(g, boss, Croc);
    int ph = player.Hero.Health, bh = boss.Hero.Health;
    EndTurn(g);
    Check("B2 at the end of the boss's turn the Rock deals 1 damage to all characters: both heroes, every minion, itself too",
        player.Hero.Health == ph - 1 && boss.Hero.Health == bh - 1 && yeti.Damage == 1 && croc.Damage == 1 && rock.Damage == 1,
        $"heroes {ph}>{player.Hero.Health} {bh}>{boss.Hero.Health} rock {rock.Damage}");
    ph = player.Hero.Health;
    EndTurn(g);
    Check("B3 at the end of the run player's turn it does nothing", player.Hero.Health == ph && rock.Damage == 1);
    Mana(boss);
    for (int i = boss.BoardZone.Count; i < 7; i++) Board(g, boss, Wisp);
    Check("B4 with a full board Lava Belch is not offered", !Offered(g));
});

// ============================================================ C  Zuramat the Obliterator: To The Void, Void Shift
Guard("Zuramat", () =>
{
    var g = Fight("dala_48_zuramat", shrine: Shrine);
    Controller boss = g.Player2, player = g.Player1;
    Check("C1 the run player starts with two Void Shifts in hand (the row's start block, after the mulligan); none in its deck",
        player.HandZone.Count(p => p.Card.Id == VoidShift) == 2 && player.DeckZone.All(p => p.Card.Id != VoidShift) && boss.HandZone.All(p => p.Card.Id != VoidShift),
        string.Join(",", player.HandZone.Select(p => p.Card.Id)));
    var shrine = ShrineRules.Of(player).FirstOrDefault(m => !m.IsDormant);
    var hoarder = Board(g, player, Hoarder); var yeti = Board(g, player, Yeti);
    yeti.Damage = 2;
    ToBoss(g);
    var own = Board(g, boss, Croc);
    var targets = PowerTargets(g);
    Check("C2 To The Void costs (2) and can be aimed at any minion of either board, never the run player's shrine",
        Cost("DALA_BOSS_48p") == 2 && shrine != null && targets.Contains(hoarder) && targets.Contains(yeti) && targets.Contains(own) && !targets.Contains(shrine),
        string.Join(",", targets.Select(t => t.Card.Id)));
    int hand = player.HandZone.Count, graveyard = player.GraveyardZone.Count;
    bool used = Power(g, hoarder);
    Check("C3 a banished minion leaves its board with no death and no Deathrattle (Loot Hoarder draws nothing) for its owner's set-aside zone, held by Voided",
        used && hoarder.Zone == player.SetasideZone && player.HandZone.Count == hand && player.GraveyardZone.Count == graveyard && Has(hoarder, Voided)
        && EncounterRules.VoidOf(g).SequenceEqual(new[] { hoarder }) && boss.UsedMana == 2, $"zone {hoarder.Zone?.Type} hand {hand}>{player.HandZone.Count}");
    Again(g);
    Power(g, yeti);
    var clone = g.Clone();
    var (view, _, _) = FairView(g);
    Check("C4 the Void holds both, in banishing order; a game clone and the boss's fair view carry it (the banished minions were public)",
        EncounterRules.VoidOf(g).Select(m => m.Id).SequenceEqual(new[] { hoarder.Id, yeti.Id }) && EncounterRules.VoidOf(clone).Select(m => m.Id).SequenceEqual(new[] { hoarder.Id, yeti.Id })
        && EncounterRules.VoidOf(view).Select(m => m.Card.Id).SequenceEqual(new[] { Hoarder, Yeti }), string.Join(",", EncounterRules.VoidOf(view).Select(m => m.Card.Id)));
    var probe = Fight("dala_48_zuramat");
    Board(probe, probe.Player1, Yeti);
    ToBoss(probe);
    Check("C4b To The Void is offered in the boss's fair view too (an enemy minion on the board): the option the boss's AI chooses from",
        Offered(probe) && ViewOffered(probe), "real " + Offered(probe));
    Check("C5 the run player's shrine cannot be banished even by the rule (the set-aside store refuses it)", !EncounterRules.Banish(boss.Hero.HeroPower, shrine) && shrine.Zone == player.BoardZone);
    var wisp = Board(g, player, Wisp);
    var shift = ToHand(g, boss, VoidShift);
    Play(g, shift);
    var mine = boss.BoardZone.Where(m => !m.Untouchable).Select(m => m.Card.Id).ToList();
    Check("C6 the boss's Void Shift destroys every minion of both boards (the Crocolisk, the Wisp) and summons a new copy of each banished minion for the boss, in order",
        own.IsDead && wisp.IsDead && mine.SequenceEqual(new[] { Hoarder, Yeti }) && boss.BoardZone.Where(m => m.Card.Id == Yeti).All(m => m.Damage == 0 && m != yeti),
        string.Join(",", mine));
    Check("C7 the run player's shrine is destroyed by it and goes dormant (it stays on its board); the Void is empty afterwards",
        ShrineRules.Of(player).Any(m => m.IsDormant) && EncounterRules.VoidOf(g).Count == 0 && hoarder.Zone == player.SetasideZone);
    // The run player's Void Shift: a banished minion of the boss comes back on the run player's side.
    var g2 = Fight("dala_48_zuramat");
    boss = g2.Player2; player = g2.Player1;
    var mine2 = Board(g2, player, Yeti);
    ToBoss(g2);
    var croc2 = Board(g2, boss, Croc);
    Power(g2, croc2);
    ToPlayer(g2);
    var held = player.HandZone.First(p => p.Card.Id == VoidShift);
    bool cast = Play(g2, held);
    Check("C8 the run player's own Void Shift (from the start block) summons the boss's banished Crocolisk for the run player and destroys the rest",
        cast && mine2.IsDead && player.BoardZone.Select(m => m.Card.Id).SequenceEqual(new[] { Croc }) && boss.BoardZone.Count == 0 && EncounterRules.VoidOf(g2).Count == 0);
    // A full board: copies fill it, the rest is lost.
    var g3 = Fight("dala_48_zuramat");
    boss = g3.Player2; player = g3.Player1;
    ToBoss(g3);
    for (int i = 0; i < 9; i++) { var m = Board(g3, player, Wisp); Again(g3); Power(g3, m); }
    for (int i = 0; i < 7; i++) Board(g3, boss, Wisp);
    int voided = EncounterRules.VoidOf(g3).Count;
    int summoned = EncounterRules.VoidShift(boss, boss.Hero);
    g3.DeathProcessingAndAuraUpdate();
    Check("C9 nine minions in the Void: Void Shift summons seven (a full board) and empties the Void", voided == 9 && summoned == 7 && boss.BoardZone.Count == 7
        && EncounterRules.VoidOf(g3).Count == 0, voided + " > " + summoned);
});

// ============================================================ D  Cyanigosa: Uncontrollable Energy
Guard("Cyanigosa", () =>
{
    var g = Fight("dala_49_cyanigosa");
    Controller boss = g.Player2, player = g.Player1;
    Check("D1 Uncontrollable Energy is a passive power, started at match start", IsPassive(boss, "DALA_BOSS_49p"));
    var yeti = Board(g, player, Yeti);
    int bh = boss.Hero.Health;
    Cast(g, Moonfire, boss.Hero);
    Check("D2 the run player's spell sets nothing off", yeti.Damage == 0 && boss.Hero.Health == bh - 1);
    ToBoss(g);
    var own = Board(g, boss, Croc);
    int ph = player.Hero.Health;
    Cast(g, Intellect);
    Check("D3 after the boss casts a spell (Arcane Intellect), 1 damage to each enemy (the run player's hero and minions; its own untouched)",
        player.Hero.Health == ph - 1 && yeti.Damage == 1 && own.Damage == 0, $"{ph}>{player.Hero.Health} yeti {yeti.Damage}");
});

// ============================================================ E  Nozari: Sand Breath (cast by rule)
Guard("Nozari", () =>
{
    var g = Fight("dala_50_nozari");
    Controller boss = g.Player2, player = g.Player1;
    Check("E1 Sand Breath costs (2) and is cast by rule (a passive power, never an option of the boss)", Cost("DALA_BOSS_50p") == 2 && IsPassive(boss, "DALA_BOSS_50p"));
    var yeti = Board(g, player, Yeti);
    int ph = player.Hero.Health;
    BossTurnFull(g);
    Check("E2 at the start of the boss's turn it pays (2) and deals 2 damage to one enemy (the hero or a minion)", Casts(boss) == 1 && boss.UsedMana == 2 && !Offered(g)
        && (player.Hero.Health == ph - 2 && yeti.Damage == 0 || player.Hero.Health == ph && yeti.Damage == 2), $"casts {Casts(boss)} hero {ph}>{player.Hero.Health} yeti {yeti.Damage}");
    int heroHits = 0, minionHits = 0;
    for (int i = 0; i < 16; i++)
    {
        ToPlayer(g);
        player.Hero.Damage = 0; yeti.Damage = 0;
        BossTurnFull(g);
        if (player.Hero.Damage == 2) heroHits++;
        if (yeti.Damage == 2) minionHits++;
    }
    Check("E3 sixteen turns: each one hit, the hero and the minion both picked (the game RNG)", heroHits + minionHits == 16 && heroHits > 0 && minionHits > 0, heroHits + "/" + minionHits);
    ToPlayer(g);
    boss.BaseMana = 0; boss.UsedMana = 0;
    int casts = Casts(boss);
    EndTurn(g);
    Check("E4 with one Mana Crystal at its turn start it is not cast", Casts(boss) == casts && boss.UsedMana == 0 && boss.BaseMana == 1, "casts " + Casts(boss));
});

// ============================================================ F  Millificent Manastorm: Tinker, Squirrel Bomb, Thorium Chicken
Guard("Millificent", () =>
{
    var g = Fight("dala_51_millificent");
    Controller boss = g.Player2, player = g.Player1;
    var yeti = Board(g, player, Yeti);
    ToBoss(g);
    var croc = Board(g, boss, Croc);
    bool used = Power(g);
    var bomb = boss.BoardZone.FirstOrDefault(m => m.Card.Id == Bomb);
    Check("F1 Tinker costs (2) and summons a 0/4 Elementium Squirrel Bomb", Cost("DALA_BOSS_51p") == 2 && used && bomb != null && bomb.AttackDamage == 0 && bomb.Health == 4);
    int ph = player.Hero.Health, bh = boss.Hero.Health;
    EndTurn(g);
    Check("F2 at the start of the run player's turn the Bomb does nothing", !bomb.IsDead && player.Hero.Health == ph);
    EndTurn(g);
    Check("F3 at the start of the boss's turn it is destroyed and deals 2 damage to all enemies (the hero, the Yeti); the boss's side is untouched",
        bomb.IsDead && player.Hero.Health == ph - 2 && yeti.Damage == 2 && croc.Damage == 0 && boss.Hero.Health == bh, $"{ph}>{player.Hero.Health} yeti {yeti.Damage}");
    var chicken = Board(g, boss, Chicken);
    Check("F4 Thorium Chicken is a 5/1", chicken.AttackDamage == 5 && chicken.Health == 1);
    ph = player.Hero.Health;
    Cast(g, Arcane);
    Check("F5 the boss's own play sets the Chicken off for nothing", player.Hero.Health == ph);
    ToPlayer(g);
    Cast(g, Moonfire, boss.Hero);
    ToHand(g, player, Wisp);
    Play(g, player.HandZone.Last());
    Check("F6 after the run player plays a card, 3 damage to the run player's hero (two cards: 6)", player.Hero.Health == ph - 6, ph + ">" + player.Hero.Health);
    for (int i = boss.BoardZone.Count; i < 7; i++) Board(g, boss, Wisp);
    ToBoss(g);
    Check("F7 with a full board Tinker is not offered", !Offered(g));
});

// ============================================================ G  Lieutenant Sinclari: Hold the Gates!, Kirin Tor Guard
Guard("Sinclari", () =>
{
    var g = Fight("dala_52_sinclari");
    Controller boss = g.Player2, player = g.Player1;
    var enemy = Board(g, player, Wisp);
    ToBoss(g);
    var own = Board(g, boss, Croc);
    var targets = PowerTargets(g);
    Check("G1 Hold the Gates! costs (2) and can be aimed at any minion", Cost("DALA_BOSS_52p") == 2 && targets.Contains(own) && targets.Contains(enemy) && targets.All(t => t is Minion));
    Power(g, own);
    Check("G2 it gives +2 Health and Taunt (Hold Fast!)", own.Health == 5 && own.BaseHealth == 5 && own.HasTaunt && Has(own, DungeonRunEncounters.HoldFast) && own.AttackDamage == 2);
    var g1 = Board(g, boss, Guard17);
    Check("G3 a lone Kirin Tor Guard is a 1/4", g1.AttackDamage == 1 && g1.Health == 4);
    var g2 = Board(g, boss, Guard17);
    var g3 = Board(g, boss, Guard17);
    var enemyGuard = Board(g, player, Guard17);
    Check("G4 with three on the boss's board each has +1 Attack for each other friendly Guard (3); an enemy Guard counts none of them (1)",
        g1.AttackDamage == 3 && g2.AttackDamage == 3 && g3.AttackDamage == 3 && enemyGuard.AttackDamage == 1, $"{g1.AttackDamage}/{g2.AttackDamage}/{g3.AttackDamage}/{enemyGuard.AttackDamage}");
    Kill(g, g3);
    Check("G5 one dies: the other two have 2", g1.AttackDamage == 2 && g2.AttackDamage == 2);
});

// ============================================================ H  Dancin' Deryl: Boogie Woogie
Guard("Deryl", () =>
{
    var g = Fight("dala_53_deryl");
    Controller boss = g.Player2, player = g.Player1;
    var enemy = Board(g, player, Yeti);
    ToBoss(g);
    Check("H0 with no minion of its own Boogie Woogie is not offered", !Offered(g));
    var own = Board(g, boss, Croc);
    var targets = PowerTargets(g);
    Check("H1 Boogie Woogie costs (3) and is aimed at a minion of the boss only", Cost("DALA_BOSS_53p") == 3 && targets.Count == 1 && targets[0] == own);
    Power(g, own);
    own.Damage = 1;
    Kill(g, own);
    var back = boss.BoardZone.FirstOrDefault(m => m.Card.Id == Croc);
    Check("H2 when it dies a new copy is summoned for the boss (undamaged, without Boogie Beyond)", own.IsDead && back != null && back != own && back.Damage == 0
        && !Has(back, DungeonRunEncounters.BoogieBeyond) && boss.BoardZone.Count == 1);
});

// ============================================================ I  Locksmith Zibb: Spell Lock
Guard("Zibb", () =>
{
    var g = Fight("dala_54_zibb");
    Controller boss = g.Player2, player = g.Player1;
    Check("I1 Spell Lock is a passive power, started at match start", IsPassive(boss, "DALA_BOSS_54p"));
    Mana(player);
    PlayKeep(g, ToHand(g, player, Fireball), boss.Hero);
    Check("I2 the run player's Fireball (4) Overloads the run player for 4", player.OverloadOwed == 4 && player.OverloadThisGame == 4, "owed " + player.OverloadOwed);
    Mana(player);
    PlayKeep(g, ToHand(g, player, Bolt), boss.Hero);
    Check("I3 Lightning Bolt (1, Overload: (1)) Overloads for 2 in all", player.OverloadOwed == 2, "owed " + player.OverloadOwed);
    Mana(player);
    var apprentice = Board(g, player, Apprentice);
    var reduced = ToHand(g, player, Fireball);
    int shown = reduced.Cost;
    PlayKeep(g, reduced, boss.Hero);
    Check("I4 a Fireball made cheaper by Sorcerer's Apprentice (3 in hand) Overloads for the Cost it was played for (3)", shown == 3 && player.OverloadOwed == 3, "owed " + player.OverloadOwed);
    Kill(g, apprentice);
    Mana(player);
    var stone = ToHand(g, player, Spellstone);
    PlayKeep(g, ToHand(g, player, Moonfire), boss.Hero);
    Check("I5 a spell played for (0) Overloads nothing (Moonfire: no Overload event, the Lesser Sapphire Spellstone in hand unchanged)", player.OverloadOwed == 0
        && player.HandZone.Any(p => p.Card.Id == Spellstone), "owed " + player.OverloadOwed);
    PlayKeep(g, ToHand(g, player, Fireball), boss.Hero);
    Check("I6 the Overload event is raised (Lesser Sapphire Spellstone, which upgrades when its player Overloads 3 crystals, is upgraded by Fireball's 4)",
        player.OverloadOwed == 4 && !player.HandZone.Any(p => p.Card.Id == Spellstone) && player.HandZone.Any(p => p.Card.Id.StartsWith(Spellstone + "t")),
        string.Join(",", player.HandZone.Select(p => p.Card.Id)));
    EndTurn(g);
    Check("I7 Fireball's (4) is still owed by the run player through the boss's turn", player.OverloadOwed == 4, "owed " + player.OverloadOwed);
    Mana(boss);
    PlayKeep(g, ToHand(g, boss, Arcane));
    Check("I8 the boss's own spell Overloads the boss (Arcane Explosion: 2; both players, the text names none)", boss.OverloadOwed == 2, "owed " + boss.OverloadOwed);
    EndTurn(g);
    Check("I9 at the run player's turn start its owed Overload locks 4 crystals", player.OverloadLocked == 4, "locked " + player.OverloadLocked);
    // Tunnel Trogg ("Whenever you Overload, gain +1 Attack per locked Mana Crystal", two in Zibb's deck): the engine's Trogg
    // answers a card with printed Overload, so Spell Lock's Overload reaches it through EncounterRules.OverloadFor.
    var g2 = Fight("dala_54_zibb");
    boss = g2.Player2; player = g2.Player1;
    var mine = Board(g2, player, Trogg); var quiet = Board(g2, player, Trogg);
    new SilenceTask(EntityType.TARGET).Process(g2, player, player.Hero, quiet, null); g2.DeathProcessingAndAuraUpdate();
    var theirs = Board(g2, boss, Trogg);
    int a0 = mine.AttackDamage, q0 = quiet.AttackDamage, t0 = theirs.AttackDamage;
    Mana(player);
    PlayKeep(g2, ToHand(g2, player, Fireball), boss.Hero);
    Check("I10 the run player's Fireball (4, no printed Overload): its Tunnel Trogg gains 4 Attack; a silenced Trogg and the boss's Trogg gain none",
        player.OverloadOwed == 4 && mine.AttackDamage == a0 + 4 && quiet.AttackDamage == q0 && theirs.AttackDamage == t0,
        $"{a0}>{mine.AttackDamage} silenced {q0}>{quiet.AttackDamage} boss {t0}>{theirs.AttackDamage}");
    Mana(player);
    a0 = mine.AttackDamage;
    PlayKeep(g2, ToHand(g2, player, Bolt), boss.Hero);
    Check("I11 Lightning Bolt (1, Overload: (1)): 2 crystals Overloaded, the Trogg gains 2 (1 from its own rule for the printed Overload, 1 from Spell Lock)",
        player.OverloadOwed == 2 && mine.AttackDamage == a0 + 2, $"owed {player.OverloadOwed} attack {a0}>{mine.AttackDamage}");
    ToBoss(g2);
    t0 = theirs.AttackDamage; a0 = mine.AttackDamage;
    PlayKeep(g2, ToHand(g2, boss, Arcane));
    Check("I12 the boss's Arcane Explosion (2): the boss's Trogg gains 2, the run player's none", theirs.AttackDamage == t0 + 2 && mine.AttackDamage == a0,
        $"boss {t0}>{theirs.AttackDamage} player {a0}>{mine.AttackDamage}");
});

// ============================================================ J  Captain Hannigan: Raise the Alarm (cast by rule)
Guard("Hannigan", () =>
{
    var g = Fight("dala_55_hannigan");
    Controller boss = g.Player2, player = g.Player1;
    Check("J1 Raise the Alarm costs (2) and is cast by rule (a passive power, never an option of the boss)", Cost("DALA_BOSS_55p") == 2 && IsPassive(boss, "DALA_BOSS_55p"));
    BossTurnFull(g);
    Check("J2 at the start of the boss's turn it pays (2) and summons a 1/4 Kirin Tor Guard", Casts(boss) == 1 && boss.UsedMana == 2 && Count(boss, Guard17) == 1 && !Offered(g));
    BossTurnFull(g);
    Check("J3 the next turn a second Guard: each has 2 Attack", Count(boss, Guard17) == 2 && boss.BoardZone.Where(m => m.Card.Id == Guard17).All(m => m.AttackDamage == 2));
    for (int i = boss.BoardZone.Count; i < 7; i++) Board(g, boss, Wisp);
    int casts = Casts(boss);
    BossTurnFull(g);
    Check("J4 with a full board it is not cast and costs nothing", Casts(boss) == casts && boss.UsedMana == 0);
});

// ============================================================ K  Queen Wagtoggle: Bribery (cast by rule), Rival Accomplice
Guard("Wagtoggle", () =>
{
    var g = Fight("dala_56_wagtoggle", playerDeck: Croc);
    Controller boss = g.Player2, player = g.Player1;
    Check("K1 the start block shuffled one Rival Accomplice (a vanilla 4/4) into the boss's deck", boss.DeckZone.Count(p => p.Card.Id == Accomplice) == 1
        && C(Accomplice).ATK == 4 && C(Accomplice).Health == 4 && IsPassive(boss, "DALA_BOSS_56p"));
    int deck = player.DeckZone.Count;
    BossTurnFull(g);
    var bribed = boss.BoardZone.FirstOrDefault(m => m.Card.Id == Croc);
    var summons = g.HistoryFacts.Records.OfType<MinionSummonedFact>().Count(f => bribed != null && f.EntityId == bribed.Id && f.DefinitionKnown && f.ControllerId == boss.Id);
    Check("K2 Bribery costs (3) and is cast by rule at the start of the boss's turn: a minion of the run player's deck is summoned for the boss and Bribed (a public summon: it is known as it arrives)",
        Cost("DALA_BOSS_56p") == 3 && Casts(boss) == 1 && boss.UsedMana == 3 && bribed != null && bribed.Controller == boss && Has(bribed, DungeonRunEncounters.Bribed)
        && player.DeckZone.Count == deck - 1 && summons == 1, $"casts {Casts(boss)} deck {deck}>{player.DeckZone.Count} summons {summons}");
    int hand = player.HandZone.Count;
    Kill(g, bribed);
    Check("K3 when it dies, a new copy goes back to the run player's hand", player.HandZone.Count == hand + 1 && player.HandZone.Last().Card.Id == Croc && player.HandZone.Last() != bribed);
    var g2 = Fight("dala_56_wagtoggle", playerDeck: Moonfire);
    boss = g2.Player2; player = g2.Player1;
    BossTurnFull(g2);
    Check("K4 a run player's deck with no minion: the power is still cast (a public condition: the deck holds cards) and nothing is summoned",
        Casts(boss) == 1 && boss.BoardZone.Count == 0, "board " + boss.BoardZone.Count);
    var g3 = Fight("dala_56_wagtoggle", playerDeck: Croc);
    var (view, viewBoss, viewPlayer) = FairView(g3);
    string outcome = "ok";
    Minion pulled = null;
    try { pulled = DungeonRunEncounters.Bribe(viewBoss, viewBoss.Hero.HeroPower); } catch (Exception e) { outcome = e.GetType().Name; }
    Check("K5 in the boss's fair view the run player's deck is masked: Bribery pulls nothing there and never throws", outcome == "ok" && pulled == null
        && viewPlayer.DeckZone.All(p => p is Unknown), outcome);
});

// ============================================================ L  Trade Prince Gallywix: Greed is Good
Guard("Gallywix", () =>
{
    var g = Fight("dala_57_gallywix");
    Controller boss = g.Player2, player = g.Player1;
    Check("L1 Greed is Good is a passive power, started at match start", IsPassive(boss, "DALA_BOSS_57p"));
    EmptyHand(boss); EmptyHand(player);
    Cast(g, Wisp);
    Check("L2 the first card the run player plays this turn: a new copy in the boss's hand and a Gallywix's Coin in the run player's",
        boss.HandZone.Count == 1 && boss.HandZone[0].Card.Id == Wisp && player.HandZone.Count(p => p.Card.Id == GCoin) == 1, string.Join(",", boss.HandZone.Select(p => p.Card.Id)));
    Cast(g, Moonfire, boss.Hero);
    Check("L3 the second card of the turn buys nothing", boss.HandZone.Count == 1 && player.HandZone.Count(p => p.Card.Id == GCoin) == 1);
    ToBoss(g);
    int held = boss.HandZone.Count, coins0 = player.HandZone.Count(p => p.Card.Id == GCoin);
    Cast(g, Wisp);
    Check("L4 the boss's own plays buy nothing", boss.HandZone.Count == held && player.HandZone.Count(p => p.Card.Id == GCoin) == coins0, held + ">" + boss.HandZone.Count);
    ToPlayer(g);
    EmptyHand(boss);
    var coin = player.HandZone.First(p => p.Card.Id == GCoin);
    PlayKeep(g, coin);
    Check("L5 Gallywix's Coin buys nothing (\"Won't trigger Gallywix\")", boss.HandZone.Count == 0 && player.HandZone.Count(p => p.Card.Id == GCoin) == 0);
    Cast(g, Squire);
    Check("L6 so the next card of the turn is the first: it is bought", boss.HandZone.Count == 1 && boss.HandZone[0].Card.Id == Squire && player.HandZone.Count(p => p.Card.Id == GCoin) == 1);
    ToBoss(g); ToPlayer(g);
    FillHand(g, boss);
    int coins = player.HandZone.Count(p => p.Card.Id == GCoin);
    Cast(g, Wisp);
    Check("L7 with the boss's hand full nothing is bought (no Coin either)", player.HandZone.Count(p => p.Card.Id == GCoin) == coins && boss.HandZone.Count == 10);
    EmptyHand(boss);
    Cast(g, Moonfire, boss.Hero);
    Check("L8 the turn's purchase stays open: the next card is bought", boss.HandZone.Count == 1 && boss.HandZone[0].Card.Id == Moonfire);
});

// ============================================================ M  Boommaster Flark: Blast Powder, The Wire
Guard("Flark", () =>
{
    var g = Fight("dala_58_flark", shrine: Shrine);
    Controller boss = g.Player2, player = g.Player1;
    var order = player.BoardZone.Select(m => m.Card.Id).ToList();
    Check("M1 the run player starts with The Wire, a Murloc Tinyfin and The Other Wire on its board, in that order, right of its shrine; the wires are permanents",
        order.SequenceEqual(new[] { Shrine, Wire, Tinyfin, OtherWire }) && player.BoardZone.Where(EncounterRules.IsWire).All(m => m.Untouchable && m.AttackDamage == 3 && m.Health == 3)
        && boss.BoardZone.Count == 0, string.Join(",", order));
    int ph = player.Hero.Health;
    EndTurn(g); EndTurn(g);
    Check("M2 while the Tinyfin stands between them the wires do nothing", player.Hero.Health == ph, ph + ">" + player.Hero.Health);
    var tinyfin = player.BoardZone.First(m => m.Card.Id == Tinyfin);
    Kill(g, tinyfin);
    ph = player.Hero.Health;
    int bh = boss.Hero.Health;
    EndTurn(g);
    Check("M3 the wires touch: at the start of the boss's turn nothing (they are the run player's)", player.Hero.Health == ph && boss.Hero.Health == bh);
    EndTurn(g);
    Check("M4 at the start of the run player's turn they deal 5 damage to its hero, once for the touching pair", player.Hero.Health == ph - 5, ph + ">" + player.Hero.Health);
    var between = (Minion)Entity.FromCard(player, C(Yeti));
    Generic.SummonBlock(g, between, player.BoardZone.First(m => m.Card.Id == OtherWire).ZonePosition, player.Hero);
    ph = player.Hero.Health;
    EndTurn(g); EndTurn(g);
    Check("M5 a minion placed between them parts them again: no damage", player.Hero.Health == ph && player.BoardZone.Select(m => m.Card.Id).SequenceEqual(new[] { Shrine, Wire, Yeti, OtherWire }),
        string.Join(",", player.BoardZone.Select(m => m.Card.Id)));
    ToBoss(g);
    var targets = PowerTargets(g);
    var shrine = ShrineRules.Of(player).First();
    Check("M6 Blast Powder costs (2); the wires are never targets (permanents), the Yeti and the awake shrine are", Cost("DALA_BOSS_58p") == 2 && targets.Contains(between)
        && targets.Contains(shrine) && targets.All(t => !EncounterRules.IsWire(t)), string.Join(",", targets.Select(t => t.Card.Id)));
    Power(g, between);
    Mana(boss);
    Check("M7 a coated minion is not offered again", !PowerTargets(g).Contains(between) && Has(between, DungeonRunEncounters.BlastPowderMark));
    Hit(g, between, 1, boss.Hero);
    Check("M8 when the coated minion takes damage it is destroyed", between.IsDead);
    var shielded = Board(g, boss, Squire);
    shielded.HasDivineShield = true;
    Power(g, shielded);
    Hit(g, shielded, 1, player.Hero);
    Check("M9 Divine Shield takes the hit: no damage, not destroyed", !shielded.IsDead && !shielded.HasDivineShield);
    Hit(g, shielded, 1, player.Hero);
    Check("M10 the next hit destroys it", shielded.IsDead);
    Again(g);
    Power(g, shrine);
    Hit(g, shrine, 1, boss.Hero);
    Check("M11 a coated run shrine that takes damage is destroyed and its dormant form takes its slot (as from any destroy effect)",
        shrine.IsDead && ShrineRules.Of(player).Any(m => m.IsDormant && m.Zone == player.BoardZone), shrine.Zone?.Type + " forms " + ShrineRules.Of(player).Count);
});

// ============================================================ N  Madam Goya: Blackmail (cast by rule), Extortion, Lotus Bruiser, Fruit Slasher
Guard("Goya", () =>
{
    var g = Fight("dala_59_goya");
    Controller boss = g.Player2, player = g.Player1;
    Check("N0 Blackmail costs (2) and is cast by rule (a passive power, never an option of the boss)", Cost("DALA_BOSS_59p") == 2 && IsPassive(boss, "DALA_BOSS_59p"));
    EmptyHand(player);
    var taken = ToHand(g, player, Yeti);
    Generic.AddEnchantmentBlock(g, C(Howl), player.Hero, taken);
    var bruiser = Board(g, boss, Bruiser);
    BossTurnFull(g);
    var ext = player.HandZone.FirstOrDefault(p => p.Card.Id == Extortion);
    Check("N1 at the start of the boss's turn the run player's one card is replaced by an Extortion that holds it (in the run player's set-aside zone)",
        Casts(boss) == 1 && boss.UsedMana == 2 && player.HandZone.Count == 1 && ext != null && taken.Zone == player.SetasideZone && EncounterRules.EscrowOf(ext) == taken,
        string.Join(",", player.HandZone.Select(p => p.Card.Id)) + " " + taken.Zone?.Type);
    var (view, viewBoss, viewPlayer) = FairView(g);
    var seen = view.IdEntityDic.TryGetValue(taken.Id, out var e1) ? e1 : null;
    Check("N2 the boss's fair view never learns the taken card: no Yeti of the run player's anywhere in it (the escrowed card is masked)",
        view.IdEntityDic.Values.All(p => !(p.Card.Id == Yeti && p.Controller.PlayerId == player.PlayerId)) && (seen == null || seen is Unknown),
        seen == null ? "absent" : seen.GetType().Name + " " + seen.Card.Id);
    ToPlayer(g);
    int attack = bruiser.AttackDamage, health = bruiser.Health;
    bool played = Play(g, ext);
    Check("N3 the run player plays the Extortion (2): the card comes back itself, its +1/+1 kept", Cost(Extortion) == 2 && played && taken.Zone == player.HandZone
        && Has(taken, Howl) && ((Minion)taken).AttackDamage == 5, taken.Zone?.Type.ToString());
    Check("N4 Lotus Bruiser gains +2/+2 whenever an Extortion is played (by either player)", bruiser.AttackDamage == attack + 2 && bruiser.Health == health + 2, $"{attack}/{health} > {bruiser.AttackDamage}/{bruiser.Health}");
    EmptyHand(player);
    int casts = Casts(boss);
    BossTurnFull(g);
    Check("N5 with the run player's hand empty Blackmail is not cast", Casts(boss) == casts && boss.UsedMana == 0);
    // A replacement keeps the slot: hand [Wisp, Moonfire, Squire, Fireball], the Extortion takes the taken card's place.
    ToPlayer(g);
    EmptyHand(player);
    var before = new[] { Wisp, Moonfire, Squire, Fireball }.Select(id => ToHand(g, player, id)).ToList();
    BossTurnFull(g);
    var ext2 = player.HandZone.FirstOrDefault(p => p.Card.Id == Extortion);
    var gone = before.FirstOrDefault(p => p.Zone != player.HandZone);
    int slot = gone == null ? -1 : before.IndexOf(gone);
    var expected = before.Select(p => p == gone ? ext2 : p).ToList();
    Check("N5b the Extortion takes the taken card's slot: the other cards keep their order and places (taken: slot " + slot + ")",
        ext2 != null && gone != null && ext2.ZonePosition == slot && player.HandZone.ToList().SequenceEqual(expected)
        && player.HandZone.Select((p, i) => p.ZonePosition == i).All(x => x), string.Join(",", player.HandZone.Select(p => p.Card.Id)));
    // In the boss's own simulation the hand is masked: Blackmail takes a stand-in and never throws.
    ToPlayer(g);
    ToHand(g, player, Fireball); ToHand(g, player, Wisp);
    var (view2, viewBoss2, viewPlayer2) = FairView(g);
    string outcome = "ok";
    IPlayable made = null;
    try { made = EncounterRules.Blackmail(viewBoss2, viewBoss2.Hero.HeroPower); } catch (Exception e) { outcome = e.GetType().Name + ": " + e.Message; }
    Check("N6 in the boss's fair view Blackmail takes a masked card (the hand keeps its count) and never throws", outcome == "ok" && made != null
        && viewPlayer2.HandZone.Count == player.HandZone.Count, outcome);
    // Fruit Slasher: Battlecry 4 to an enemy, Combo 1 to all other enemies.
    var g2 = Fight("dala_59_goya");
    boss = g2.Player2; player = g2.Player1;
    var a = Board(g2, player, Yeti); var b = Board(g2, player, Croc);
    ToBoss(g2);
    var own = Board(g2, boss, Wisp);
    Mana(boss);
    PlayKeep(g2, ToHand(g2, boss, Slasher), a);
    Check("N7 Fruit Slasher (4/2) without Combo: 4 damage to the chosen enemy only", a.Damage == 4 && b.Damage == 0 && player.Hero.Damage == 0 && own.Damage == 0
        && C(Slasher).ATK == 4 && C(Slasher).Health == 2);
    a.Damage = 0;
    Mana(boss);
    PlayKeep(g2, ToHand(g2, boss, Moonfire), boss.Hero);
    int ph = player.Hero.Health;
    PlayKeep(g2, ToHand(g2, boss, Slasher), a);
    Check("N8 with Combo: 4 to the chosen enemy and 1 to every other enemy (the hero and the Crocolisk), none to the boss's side",
        a.Damage == 4 && b.Damage == 1 && player.Hero.Health == ph - 1 && own.Damage == 0, $"a {a.Damage} b {b.Damage} hero {ph}>{player.Hero.Health}");
});

// ============================================================ O  Infuriated Banker
Guard("Banker", () =>
{
    var g = Fight("dala_57_gallywix");
    Controller boss = g.Player2, player = g.Player1;
    var banker = Board(g, boss, Banker);
    Check("O1 Infuriated Banker is a 1/1", banker.AttackDamage == 1 && banker.Health == 1);
    Cast(g, Coin);
    Check("O2 the run player's Coin: +1/+1", banker.AttackDamage == 2 && banker.Health == 2, banker.AttackDamage + "/" + banker.Health);
    var gcoin = player.HandZone.FirstOrDefault(p => p.Card.Id == GCoin);
    if (gcoin != null) PlayKeep(g, gcoin);
    Check("O3 the Gallywix's Coin the Coin bought: +1/+1 again", gcoin != null && banker.AttackDamage == 3);
    ToBoss(g);
    Cast(g, FakeCoin);
    Check("O4 the boss's Counterfeit Coin: +1/+1", banker.AttackDamage == 4 && banker.Health == 4);
    Cast(g, Moonfire, player.Hero);
    Check("O5 any other card: nothing", banker.AttackDamage == 4);
});

// ============================================================ P  the rows, the admission rows, the validator
Guard("Rows", () =>
{
    var errors = DungeonRunValidator.Validate(data, null);
    Check("P1 the shipped data validates with the fourteen family 17 rows", errors.Count == 0, string.Join(" | ", errors.Take(4)));
    var rows = new (string id, string n, string name, string tier, int level, string kind, string[] flags, bool passive)[]
    {
        ("dala_46_ichoron", "46", "Ichoron", "3", 6, "authentic", new string[0], false),
        ("dala_47_lavanthor", "47", "Lavanthor", "3", 6, "substituted", new string[0], false),
        ("dala_48_zuramat", "48", "Zuramat the Obliterator", "4", 8, "authentic", new[] { "start", "bothPlayers" }, false),
        ("dala_49_cyanigosa", "49", "Cyanigosa", "4", 8, "authentic", new[] { "passive" }, true),
        ("dala_50_nozari", "50", "Nozari", "3", 6, "authentic", new[] { "passive" }, true),
        ("dala_51_millificent", "51", "Millificent Manastorm", "3", 6, "authentic", new string[0], false),
        ("dala_52_sinclari", "52", "Lieutenant Sinclari", "3", 6, "authentic", new string[0], false),
        ("dala_53_deryl", "53", "Dancin' Deryl", "2", 4, "extended", new string[0], false),
        ("dala_54_zibb", "54", "Locksmith Zibb", "2", 4, "extended", new[] { "passive", "bothPlayers" }, true),
        ("dala_55_hannigan", "55", "Captain Hannigan", "4", 8, "authentic", new[] { "passive" }, true),
        ("dala_56_wagtoggle", "56", "Queen Wagtoggle", "4", 8, "authentic", new[] { "passive", "start", "hiddenZone" }, true),
        ("dala_57_gallywix", "57", "Trade Prince Gallywix", "4", 8, "substituted", new[] { "passive" }, true),
        ("dala_58_flark", "58", "Boommaster Flark", "4", 8, "authentic", new[] { "start", "boardStart", "position" }, false),
        ("dala_59_goya", "59", "Madam Goya", "4", 8, "authentic", new[] { "passive", "hiddenZone" }, true),
    };
    Check("P2 family 17 holds exactly these fourteen rows", data.Encounters.Where(b => b.Family == "17").Select(b => b.Id).OrderBy(i => i, StringComparer.Ordinal)
        .SequenceEqual(rows.Select(r => r.id).OrderBy(i => i, StringComparer.Ordinal)), string.Join(",", data.Encounters.Where(b => b.Family == "17").Select(b => b.Id)));
    foreach (var r in rows)
    {
        var b = data.Boss(r.id);
        var plan = RowPlan(r.id);
        string hero = "DALA_BOSS_" + r.n + "h", power = "DALA_BOSS_" + r.n + "p";
        Check("P3 " + r.id + ": a Dalaran Heist row of tier " + r.tier + " (level " + r.level + ") whose plan is the row's (hero, " + (r.passive ? "passive" : "clickable") + " power, a 30-card " + r.kind
            + " deck, the act curve's Health), flags [" + string.Join(",", r.flags) + "]",
            b.Provenance == "DALA" && b.Name == r.name && b.Tier == r.tier && b.LevelOriginal == r.level && b.LevelSource == "wiki-level-table" && b.HeroCard == hero && b.Power == power
            && b.Source.Kind == r.kind && b.Flags.OrderBy(f => f).SequenceEqual(r.flags.OrderBy(f => f))
            && plan.EncounterId == r.id && plan.EncounterDeck && plan.BossHero.Id == hero && plan.BossHeroPowerId == power && plan.BossDeck.Count == 30 && data.BossDecks[b.Deck].Count == 30
            && !b.FixedHealth && b.Health == null && plan.BossHealth == 30 && C(hero).Type == CardType.HERO && C(power).Type == CardType.HERO_POWER && C(hero).Name == r.name
            && DungeonRunEncounters.PassivePowers.Contains(power) == r.passive && DungeonRunEncounters.Find(power)?.Kind == (r.passive ? EncounterCardKind.Passive : EncounterCardKind.Power)
            && data.Schedules.Member(b, DungeonRunEncounterData.PoolOf(b.Tier)) && b.Source.Url.StartsWith("https://hearthstone.wiki.gg/wiki/") && !b.BossGeneratedSecrets
            && b.Forms.Count == 0,
            b.Source.Kind + " health " + plan.BossHealth + " flags " + string.Join(",", b.Flags) + " tier " + b.Tier + " level " + b.LevelOriginal);
    }
    string[] powers = rows.Select(r => "DALA_BOSS_" + r.n + "p").ToArray();
    string[] cards = { Rock, VoidShift, Bomb, Chicken, Accomplice, Wire, OtherWire, Bruiser, Slasher, Extortion, Banker, Guard17 };
    string[] enchantments = { Voided, DungeonRunEncounters.HoldFast, DungeonRunEncounters.BoogieBeyond, DungeonRunEncounters.Bribed, DungeonRunEncounters.BlastPowderMark,
        DungeonRunEncounters.Payday, DungeonRunEncounters.BankerRich };
    var scripted = powers.Concat(cards).ToArray();
    var unscripted = scripted.Concat(enchantments).Where(id => !C(id).Implemented || DungeonRunEncounters.Find(id)?.Family != "17").ToList();
    // The engine's loader marks a text with a damage number ("$1") with a trailing " @spelldmg" (Uncontrollable Energy, Sand Breath).
    var retexted = scripted.Concat(enchantments).Where(id => (C(id).Text ?? "").Replace(" @spelldmg", "") != (factById[id].TryGetProperty("text", out var t) ? t.GetString() ?? "" : "")).ToList();
    Check("P4 every power, boss card and enchantment of the family is scripted by family 17 and keeps the client's text (14 powers, 12 cards, 7 enchantments)",
        unscripted.Count == 0 && retexted.Count == 0 && powers.Length == 14 && cards.Length == 12,
        string.Join(",", unscripted.Concat(retexted.Select(id => id + " [" + C(id).Text + "]"))));
    var heroes = rows.Select(r => "DALA_BOSS_" + r.n + "h").ToList();
    Check("P4b the fourteen boss heroes need no script (portraits)", heroes.All(id => DungeonRunEncounters.Find(id) == null && C(id).Type == CardType.HERO));
    var wrongScope = scripted.Where(id =>
    {
        var v = DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss, null, catalogRun: true);
        return !(v.Admitted && v.Subject && v.Rule?.Ids != null && v.Rule.Ids.Contains(id) && DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss).Admitted
            && DungeonRunAdmission.Classify(C(id), id, AdmitScope.Player, null, catalogRun: true).Admitted == cards.Contains(id)
            && !DungeonRunAdmission.Classify(C(id), id, AdmitScope.Generated, null, catalogRun: true).Admitted
            && DungeonRunAdmission.GeneratedRejects(true).Contains(id) && DungeonRunAdmission.GeneratedRejects(false).Contains(id));
    }).ToList();
    Check("P5 each has its own admission row: the fourteen powers a boss's only, the twelve boss cards a boss's and the run player's; none is ever generated", wrongScope.Count == 0, string.Join(",", wrongScope));
    var families = scripted.Select(id => DungeonRunAdmission.RuleFor(id)?.Family).Distinct().OrderBy(f => f, StringComparer.Ordinal).ToList();
    int In(string family) => DungeonRunAdmission.Rules.Where(r => r.Family == family).Sum(r => r.Ids.Length);
    Check("P6 the six admission families of the family: heist-d-board-power (6), heist-d-void-power (1), heist-d-passive (3), heist-d-auto-cast (2), heist-d-hidden-auto-cast (2), heist-d-boss-card (12)",
        families.SequenceEqual(new[] { "heist-d-auto-cast", "heist-d-board-power", "heist-d-boss-card", "heist-d-hidden-auto-cast", "heist-d-passive", "heist-d-void-power" })
        && In("heist-d-board-power") == 6 && In("heist-d-void-power") == 1 && In("heist-d-passive") == 3 && In("heist-d-auto-cast") == 2 && In("heist-d-hidden-auto-cast") == 2
        && In("heist-d-boss-card") == 12, string.Join(",", families));
    var zur = data.Boss("dala_48_zuramat"); var wag = data.Boss("dala_56_wagtoggle"); var fla = data.Boss("dala_58_flark");
    Check("P7 the three start blocks: two Void Shifts to the run player's hand (Zuramat), one Rival Accomplice into the boss's deck (Wagtoggle), The Wire, Murloc Tinyfin, The Other Wire on the run player's board (Flark); no other row has one",
        zur.Start.PlayerHandAdd.SequenceEqual(new[] { VoidShift, VoidShift }) && zur.Start.BossDeckAdd.Count == 0 && wag.Start.BossDeckAdd.SequenceEqual(new[] { Accomplice })
        && fla.Start.PlayerBoard.SequenceEqual(new[] { Wire, Tinyfin, OtherWire }) && fla.Start.BossBoard.Count == 0
        && rows.Where(r => r.id != zur.Id && r.id != wag.Id && r.id != fla.Id).All(r => data.Boss(r.id).Start == null));
    string Subs(string id) => string.Join(",", data.Boss(id).Source.Substitutions.Where(x => x.Listed != null || x.Wiki != null).Select(x => (x.Listed ?? x.Wiki) + ">" + (x.Used ?? "-") + ":" + x.Reason).Distinct().OrderBy(x => x, StringComparer.Ordinal));
    Check("P8 the listed cards that are replaced or left out are only Xavius (not in the client data; filled from the Heroic list) and Fel Reaver (unscripted; the bank's Infuriated Banker), each recorded",
        Subs("dala_47_lavanthor") == "Xavius>-:unknown" && Subs("dala_57_gallywix") == "GVG_016>DALA_502:unscripted" && !C("GVG_016").Implemented
        && rows.Where(r => r.id != "dala_47_lavanthor" && r.id != "dala_57_gallywix").All(r => Subs(r.id) == ""),
        Subs("dala_47_lavanthor") + " | " + Subs("dala_57_gallywix"));
    int Deck(string boss, string card) => data.BossDecks[data.Boss(boss).Deck].Count(id => id == card);
    Check("P9 the boss cards of the decks: Zuramat 3 Void Shifts, Millificent 2 Thorium Chickens, Sinclari 4 Kirin Tor Guards, Goya 2 Lotus Bruisers and 1 Fruit Slasher, Gallywix 2 Infuriated Bankers",
        Deck("dala_48_zuramat", VoidShift) == 3 && Deck("dala_51_millificent", Chicken) == 2 && Deck("dala_52_sinclari", Guard17) == 4 && Deck("dala_59_goya", Bruiser) == 2
        && Deck("dala_59_goya", Slasher) == 1 && Deck("dala_57_gallywix", Banker) == 2);
    var playerPower = DungeonRunAdmission.Classify(C("DALA_BOSS_58p"), "DALA_BOSS_58p", AdmitScope.Player, null, catalogRun: true);
    Check("P10 a family 17 power asked for the run player is refused with its row's reason", !playerPower.Admitted && playerPower.Family == "heist-d-board-power" && playerPower.Lift == DungeonRunAdmission.NoneBossPower);
    DungeonRunData Changed(Action<DungeonRunData> change) { var d = Load(); change(d); return d; }
    var badStart = DungeonRunValidator.Validate(Changed(d => d.Boss("dala_58_flark").Start.PlayerBoard[1] = "DALA_BOSS_48t"), null);
    Check("P11 the validator checks a start board: a spell there is refused", badStart.Count > 0, string.Join(" | ", badStart.Take(2)));
    Check("P12 the tier pools: two rows in enc-t2, five in enc-t3, seven in enc-t4", rows.Count(r => r.tier == "2") == 2 && rows.Count(r => r.tier == "3") == 5 && rows.Count(r => r.tier == "4") == 7
        && rows.All(r => data.Boss(r.id).Pools.SequenceEqual(new[] { "enc-t" + r.tier })));
    Check("P13 the extra cards each row names (the tokens, the enchantments, Extortion, Gallywix's Coin, the Kirin Tor Guard) are its 'cards'",
        data.Boss("dala_47_lavanthor").Cards.Contains(Rock) && data.Boss("dala_48_zuramat").Cards.Contains(Voided) && data.Boss("dala_51_millificent").Cards.Contains(Bomb)
        && data.Boss("dala_55_hannigan").Cards.Contains(Guard17) && data.Boss("dala_57_gallywix").Cards.Contains(GCoin) && data.Boss("dala_59_goya").Cards.Contains(Extortion)
        && data.Boss("dala_58_flark").Cards.Contains(DungeonRunEncounters.BlastPowderMark) && data.Boss("dala_56_wagtoggle").Cards.Contains(DungeonRunEncounters.Bribed));
    Check("P14 the escrow tag is the family's reserved private tag (32652), inside the encounter range NativeMatch never sends",
        (int)EncounterRules.ExtortionEscrow == 32652 && EncounterRules.IsPrivateTag(EncounterRules.ExtortionEscrow));
});

Console.WriteLine($"SearchTelemetry swallowed leaf exceptions {SearchTelemetry.SwallowedLeafErrors}");
Console.WriteLine($"RESULT {passes} passed, {failures + SearchTelemetry.SwallowedLeafErrors} failed");
return failures + SearchTelemetry.SwallowedLeafErrors;
