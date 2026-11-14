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

// Phase 8 family 12, Monster Hunt bosses C (real engine games through the run setup path: DungeonRun.BuildPlan on the shipped
// row -> DungeonRunSetup.Configure / ApplyAfterStart): Groddo the Bogwarden (From the Swamp, Bloated Zombie), Blood Witch
// Gretta (Blood Red Apple), Forlorn Lovers (Together Forever and the boss's own-deck ledger), Cultist S'thara (Poisoned Drink),
// Vitus the Exiled (Monstrous Aura), Shudderwock (Frumiousity), The Whisperer (Echoes of the Witchwood, the Echo copy floor and
// the per-turn limit), Sephira Dusktalon (Tangled Wrath), Winslow Tobtock (Hypnotize: both hands into the decks, the belief
// resets of both players and the boss's fair view), Gobbles (Starving), The Mute (...), Ratcatcher Hannigul (Cannibalism:
// adjacency inside the death pass) and Plaguemaster Rancel (Poison Flask, Infected Quillflinger), each with the shrine rules
// that touch it, and the rows, admission rows and validator.
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

const string Yeti = "CS2_182", Croc = "CS2_120", Raptor = "CS2_172", Moonfire = "CS2_008", Fireball = "CS2_029", Frostbolt = "CS2_024", Intellect = "CS2_023";
const string Wisp = "CS2_231", Archer = "CS2_189", Engineer = "EX1_015", Squire = "EX1_008", Coin = "GAME_005", Backstab = "CS2_072", Sap = "EX1_581";
const string Tidehunter = "EX1_506", Amalgam = "GIL_681", Toad = "LOE_046", Shrine = "TRLA_154";
const string Zombie = "GILA_BOSS_24t", Quill = "GILA_BOSS_68t";
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
Game Ready(RunMatchPlan plan, int seed = 7, bool history = true)
{
    var cfg = new GameConfig { RandomSeed = seed, Shuffle = true, SkipMulligan = false, History = history, Logging = false, FormatType = FormatType.FT_WILD };
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
Game Fight(string bossId, string bossDeck = Yeti, string playerDeck = Yeti, string shrine = null, int seed = 7, bool history = true)
{
    var plan = RowPlan(bossId);
    plan.BossDeck = Thirty(bossDeck).Select(C).ToList();
    plan.PlayerDeck = Thirty(playerDeck).Select(C).ToList();
    plan.PlayerDeckSlots = null;
    if (shrine != null) plan.PlayerShrine = shrine;
    return Ready(plan, seed, history);
}
void Mana(Controller c, int n = 10) { c.BaseMana = n; c.UsedMana = 0; c.OverloadLocked = 0; c.OverloadOwed = 0; }
IPlayable ToHand(Game g, Controller c, string id)
{
    while (c.HandZone.Count >= 9) { var old = c.HandZone[0]; c.HandZone.Remove(old); c.SetasideZone.Add(old); }
    var e = Entity.FromCard(c, C(id)); Generic.AddHandPhase.Invoke(c, e); g.DeathProcessingAndAuraUpdate(); return e;
}
void FillHand(Game g, Controller c) { while (!c.HandZone.IsFull) Generic.AddHandPhase.Invoke(c, Entity.FromCard(c, C(Yeti))); g.DeathProcessingAndAuraUpdate(); }
void EmptyDeck(Controller c) { foreach (var p in c.DeckZone.ToArray()) c.SetasideZone.Add(c.DeckZone.Remove(p)); }
void EmptyHand(Controller c) { foreach (var p in c.HandZone.ToArray()) { c.HandZone.Remove(p); c.SetasideZone.Add(p); } }
void ToDeck(Controller c, string id) => Generic.ShuffleIntoDeck(c, c.Hero, Entity.FromCard(c, C(id)));
Minion Board(Game g, Controller c, string id) { var m = (Minion)Entity.FromCard(c, C(id)); Generic.SummonBlock(g, m, -1, c.Hero); g.DeathProcessingAndAuraUpdate(); return m; }
bool Play(Game g, IPlayable card, ICharacter target = null, bool refill = true) { if (refill) Mana(g.CurrentPlayer); return g.Process(PlayCardTask.Any(g.CurrentPlayer, card, target, -1, 0)); }
bool Cast(Game g, string id, ICharacter target = null) => Play(g, ToHand(g, g.CurrentPlayer, id), target);
void EndTurn(Game g) => g.Process(EndTurnTask.Any(g.CurrentPlayer));
bool Offered(Game g) => g.CurrentPlayer.Options().Any(o => o is HeroPowerTask);
List<ICharacter> PowerTargets(Game g) => g.CurrentPlayer.Options().OfType<HeroPowerTask>().Select(o => o.Target).Where(t => t != null).ToList();
bool Power(Game g, ICharacter target = null) => Offered(g) && g.Process(HeroPowerTask.Any(g.CurrentPlayer, target));
void ToBoss(Game g) { if (g.CurrentPlayer == g.Player1) EndTurn(g); Mana(g.Player2); }
void ToPlayer(Game g) { if (g.CurrentPlayer == g.Player2) EndTurn(g); Mana(g.Player1); }
void Kill(Game g, Minion m) { new DestroyTask(EntityType.TARGET).Process(g, m.Controller, m.Controller.Hero, m, null); g.DeathProcessingAndAuraUpdate(); }
void KillAll(Game g, params Minion[] ms) { foreach (var m in ms) { DeathContextRules.RecordDestroy(m, m.Controller.Hero); m.Destroy(); } g.DeathProcessingAndAuraUpdate(); }
int Count(Controller c, string id) => c.BoardZone.Count(m => m.Card.Id == id);
int InDeck(Controller c, string id) => c.DeckZone.Count(p => p.Card.Id == id);
bool Has(IPlayable p, string enchantmentId) => p.AppliedEnchantments?.Any(e => e.Card.Id == enchantmentId) == true;
int Enchants(IPlayable p, string enchantmentId) => p.AppliedEnchantments?.Count(e => e.Card.Id == enchantmentId) ?? 0;
int Cost(string id) => C(id).Cost;
bool PassiveStarted(Controller boss, string power) => boss.Hero.HeroPower.Card.Id == power && boss.Hero.HeroPower.IsPassiveHeroPower
    && boss.Hero.HeroPower[EncounterRules.PowerActive] == 1 && DungeonRunEncounters.PassivePowers.Contains(power) && DungeonRunEncounters.Find(power)?.Kind == EncounterCardKind.Passive;
(Game view, Controller boss, Controller player) FairView(Game g)
{
    var bot = FairBotState.Create(g, g.Player2.PlayerId, 1, DeckStyle.Tempo);
    var view = bot.FingerprintGame;
    var viewBoss = view.Player1.PlayerId == g.Player2.PlayerId ? view.Player1 : view.Player2;
    return (view, viewBoss, viewBoss.Opponent);
}
Dictionary<string, int> Ledger(Game g, Controller c) => OwnDeckLedger.Observe(g, c, null).Sample(g, c, 11);
int Known(Dictionary<string, int> sample, string id) => sample.TryGetValue(id, out int n) ? n : 0;

// ============================================================ A  Groddo the Bogwarden: From the Swamp, Bloated Zombie
Guard("Groddo", () =>
{
    var g = Fight("gila_24_groddo");
    Controller boss = g.Player2, player = g.Player1;
    Check("A1 From the Swamp is a passive power started at match start (never an option of the boss)", PassiveStarted(boss, "GILA_BOSS_24p"));
    ToBoss(g);
    Check("A1b the boss is offered no hero power", !Offered(g));
    var theirs = Board(g, player, Croc); var own = Board(g, boss, Wisp);
    Kill(g, theirs);
    var zombie = boss.BoardZone.FirstOrDefault(m => m.Card.Id == Zombie);
    Check("A2 a minion of the run player dies: one 1/3 Bloated Zombie rises for the boss", zombie != null && Count(boss, Zombie) == 1 && zombie.AttackDamage == 1 && zombie.Health == 3 && Count(player, Zombie) == 0,
        zombie == null ? "none" : zombie.AttackDamage + "/" + zombie.Health);
    Kill(g, own);
    Check("A3 a minion of the boss dies: nothing rises", Count(boss, Zombie) == 1 && boss.BoardZone.Count == 1);
    int bossHp = boss.Hero.Health, playerHp = player.Hero.Health;
    var yeti = Board(g, player, Yeti); var croc = Board(g, boss, Croc);
    Kill(g, zombie);
    Check("A4 the Bloated Zombie's Deathrattle deals 1 damage to all characters (both heroes, every minion)", boss.Hero.Health == bossHp - 1 && player.Hero.Health == playerHp - 1
        && yeti.Damage == 1 && croc.Damage == 1, $"boss {bossHp - boss.Hero.Health} player {playerHp - player.Hero.Health} yeti {yeti.Damage} croc {croc.Damage}");
    while (!boss.BoardZone.IsFull) Board(g, boss, Wisp);
    Kill(g, yeti);
    Check("A5 with the boss's board full nothing rises", Count(boss, Zombie) == 0 && boss.BoardZone.IsFull);
    var g2 = Fight("gila_24_groddo", shrine: Shrine);
    var shrine = ShrineRules.Of(g2.Player1).First();
    Kill(g2, shrine);
    Check("A6 a run shrine of the player that dies (and goes dormant) is a death: a Zombie rises", ShrineRules.IsCorpse(shrine) && Count(g2.Player2, Zombie) == 1
        && ShrineRules.Of(g2.Player1).FirstOrDefault()?.IsDormant == true);
});

// ============================================================ B  Blood Witch Gretta: Blood Red Apple
Guard("Gretta", () =>
{
    var g = Fight("gila_30_gretta");
    Controller boss = g.Player2, player = g.Player1;
    Check("B1 Blood Red Apple is a passive power started at match start: the boss's controller has the SPELLS_COST_HEALTH aura, the run player's not",
        PassiveStarted(boss, "GILA_BOSS_30p") && boss.ControllerAuraEffects[GameTag.SPELLS_COST_HEALTH] == 1 && player.ControllerAuraEffects[GameTag.SPELLS_COST_HEALTH] == 0);
    Mana(player, 10);
    int playerHp = player.Hero.Health;
    bool cast = Cast(g, Frostbolt, boss.Hero);
    Check("B2 the run player's spells cost Mana as before", cast && player.UsedMana == 2 && player.Hero.Health == playerHp, "used " + player.UsedMana);
    ToBoss(g);
    int hp = boss.Hero.Health, damage = boss.Hero.Damage;
    var fireball = ToHand(g, boss, Fireball);
    bool paid = Play(g, fireball, player.Hero);
    Check("B3 the boss's spell costs Health instead of Mana: Fireball takes 4 Health and no Mana", paid && boss.UsedMana == 0 && boss.Hero.Health == hp - 4, "used " + boss.UsedMana + " health " + (hp - boss.Hero.Health));
    var minion = ToHand(g, boss, Yeti);
    Check("B4 the boss's minions still cost Mana", Play(g, minion) && boss.UsedMana == 4, "used " + boss.UsedMana);
    var intellect = ToHand(g, boss, Intellect);
    Mana(boss, 0);
    int before = boss.Hero.Health;
    bool zeroMana = g.Process(PlayCardTask.Any(boss, intellect, null, -1, 0));
    Check("B5 with no Mana the boss still casts Arcane Intellect for 3 Health", zeroMana && boss.Hero.Health == before - 3 && boss.UsedMana == 0, (before - boss.Hero.Health).ToString());
});

// ============================================================ C  Forlorn Lovers: Together Forever and the own-deck ledger
Guard("Lovers", () =>
{
    var g = Fight("gila_39_forlorn_lovers", history: false);
    Controller boss = g.Player2, player = g.Player1;
    Check("C1 Together Forever is a passive power started at match start", PassiveStarted(boss, "GILA_BOSS_39p"));
    ToBoss(g);
    int deck = boss.DeckZone.Count;
    var croc = Board(g, boss, Croc);
    Generic.AddEnchantmentBlock(g, C("GILA_BOSS_46e"), boss.Hero, croc);
    Kill(g, croc);
    var copy = boss.DeckZone.Where(p => p.Card.Id == Croc).ToList();
    Check("C2 a friendly minion dies: a fresh copy (no buffs) is shuffled into the boss's deck", boss.DeckZone.Count == deck + 1 && copy.Count == 1 && copy[0] != croc
        && (copy[0].AppliedEnchantments == null || copy[0].AppliedEnchantments.Count == 0), "deck " + (boss.DeckZone.Count - deck));
    var fact = g.RunDeckFacts.LastOrDefault();
    Check("C3 the boss's own-deck ledger is told its deck's exact list (a Rewritten deck fact of the boss)", fact != null && fact.Kind == RunDeckFactKind.Rewritten && fact.ControllerId == boss.Id
        && fact.Definitions.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(boss.DeckZone.Select(p => p.Card.Id).OrderBy(x => x, StringComparer.Ordinal)));
    var ledger = Ledger(g, boss);
    Check("C4 the boss's own-deck ledger samples the copy: exactly its deck (29 Yetis and the River Crocolisk)", Known(ledger, Croc) == 1 && ledger.Values.Sum() == boss.DeckZone.Count
        && Known(ledger, Yeti) == boss.DeckZone.Count(p => p.Card.Id == Yeti), string.Join(",", ledger.Select(p => p.Key + "x" + p.Value)));
    var theirs = Board(g, player, Raptor);
    int facts0 = g.RunDeckFacts.Count;
    deck = boss.DeckZone.Count;
    Kill(g, theirs);
    Check("C5 a minion of the run player dies: nothing is shuffled, no fact", boss.DeckZone.Count == deck && player.DeckZone.All(p => p.Card.Id != Raptor) && g.RunDeckFacts.Count == facts0);
    var a = Board(g, boss, Wisp); var b = Board(g, boss, Raptor);
    KillAll(g, a, b);
    Check("C6 two friendly minions die together: one copy of each", InDeck(boss, Wisp) == 1 && InDeck(boss, Raptor) == 1 && boss.DeckZone.Count == deck + 2);
    while (!boss.DeckZone.IsFull) ToDeck(boss, Yeti);
    var c2 = Board(g, boss, Croc);
    int full = boss.DeckZone.Count, before = g.RunDeckFacts.Count;
    Kill(g, c2);
    Check("C7 with the deck full nothing is shuffled", boss.DeckZone.Count == full && g.RunDeckFacts.Count == before);
});

// ============================================================ D  Cultist S'thara: Poisoned Drink
Guard("Sthara", () =>
{
    var g = Fight("gila_45_sthara");
    Controller boss = g.Player2, player = g.Player1;
    Check("D1 Poisoned Drink is a passive power started at match start; the run player's turn-1 draw cost it 1 Health, the opening hand and the mulligan nothing",
        PassiveStarted(boss, "GILA_BOSS_45p") && player.Hero.Damage == 1 && boss.Hero.Damage == 0, "player " + player.Hero.Damage + " boss " + boss.Hero.Damage);
    EndTurn(g);
    Check("D2 the boss's own turn draw costs the boss 1 Health", boss.Hero.Damage == 1 && player.Hero.Damage == 1);
    Mana(boss);
    Cast(g, Intellect);
    Check("D3 a spell that draws two costs its caster 2", boss.Hero.Damage == 3, boss.Hero.Damage.ToString());
    EndTurn(g);
    Check("D4 the run player's next draw: 1 more", player.Hero.Damage == 2, player.Hero.Damage.ToString());
    FillHand(g, player);
    Mana(player);
    int hp = player.Hero.Damage, hand = player.HandZone.Count;
    Generic.Draw(player);
    g.DeathProcessingAndAuraUpdate();
    Check("D5 a card burned by a full hand was drawn: 1 damage", player.Hero.Damage == hp + 1 && player.HandZone.Count == hand, player.Hero.Damage - hp + "");
    EmptyDeck(player);
    hp = player.Hero.Damage;
    Generic.Draw(player);
    g.DeathProcessingAndAuraUpdate();
    Check("D6 an empty deck: Fatigue only (1), Poisoned Drink adds nothing", player.Hero.Damage == hp + 1, (player.Hero.Damage - hp).ToString());
});

// ============================================================ E  Vitus the Exiled: Monstrous Aura
Guard("Vitus", () =>
{
    var g = Fight("gila_46_vitus");
    Controller boss = g.Player2, player = g.Player1;
    Check("E1 Monstrous Aura is a passive power started at match start", PassiveStarted(boss, "GILA_BOSS_46p"));
    var theirs = Board(g, player, Yeti);
    ToBoss(g);
    var own = Board(g, boss, Yeti);
    Check("E2 the boss's minions have +1/+1 (Empowered), the run player's not", own.AttackDamage == 5 && own.Health == 6 && Has(own, "GILA_BOSS_46e") && theirs.AttackDamage == 4 && theirs.Health == 5
        && !Has(theirs, "GILA_BOSS_46e"), own.AttackDamage + "/" + own.Health);
    var raptor = Board(g, boss, Raptor);
    Check("E3 a minion that arrives later has it too", raptor.AttackDamage == 4 && raptor.Health == 3 && own.AttackDamage == 5);
});

// ============================================================ F  Shudderwock: Frumiousity
Guard("Shudderwock", () =>
{
    var g = Fight("gila_47_shudderwock");
    Controller boss = g.Player2, player = g.Player1;
    Check("F1 Frumiousity is a passive power started at match start: one EXTRA_BATTLECRIES_BASE on each player", PassiveStarted(boss, "GILA_BOSS_47p")
        && boss.ControllerAuraEffects[GameTag.EXTRA_BATTLECRIES_BASE] == 1 && player.ControllerAuraEffects[GameTag.EXTRA_BATTLECRIES_BASE] == 1);
    int hand = player.HandZone.Count;
    Cast(g, Engineer);
    Check("F2 the run player's Battlecry triggers twice (Novice Engineer draws two)", player.HandZone.Count == hand + 2, (player.HandZone.Count - hand).ToString());
    ToBoss(g);
    Cast(g, Tidehunter);
    Check("F3 the boss's Battlecry triggers twice (Murloc Tidehunter summons two Scouts)", Count(boss, "EX1_506a") == 2, Count(boss, "EX1_506a").ToString());
});

// ============================================================ G  The Whisperer: Echoes of the Witchwood
Guard("Whisperer", () =>
{
    var g = Fight("gila_50_whisperer");
    Controller boss = g.Player2, player = g.Player1;
    Check("G1 Echoes of the Witchwood is a passive power started at match start", PassiveStarted(boss, "GILA_BOSS_50p") && EncounterRules.EchoRuleActive(g));
    var spell = ToHand(g, player, Frostbolt); var minion = ToHand(g, player, Yeti);
    var bossSpell = ToHand(g, boss, Moonfire);
    Check("G2 every spell in either hand has Echo; a minion has none", spell.IsEcho && bossSpell.IsEcho && !minion.IsEcho);
    Mana(player, 10);
    bool played = Play(g, spell, boss.Hero, refill: false);
    var copy = player.HandZone.LastOrDefault(p => p.Card.Id == Frostbolt);
    Check("G3 a spell played from hand returns an Echo copy to its owner's hand (ghostly), which has Echo too and costs the printed (2)", played && copy != null && copy != spell && copy[GameTag.GHOSTLY] == 1
        && copy.IsEcho && copy.Cost == 2 && player.UsedMana == 2);
    EndTurn(g);
    Check("G4 the copy is discarded when the turn ends", copy.Zone != player.HandZone && player.HandZone.All(p => p.Card.Id != Frostbolt));
    Mana(boss, 10);
    var mf = boss.HandZone.First(p => p.Card.Id == Moonfire);
    Check("G5 the boss's 0-Cost Moonfire is played for free", Play(g, mf, player.Hero, refill: false) && boss.UsedMana == 0);
    var mcopy = boss.HandZone.LastOrDefault(p => p.Card.Id == Moonfire);
    Check("G6 its Echo copy costs (1), never less (the client's Echo rule, EncounterRules.EchoCopyFloor)", mcopy != null && mcopy[GameTag.GHOSTLY] == 1 && mcopy.Cost == 1, "cost " + mcopy?.Cost);
    // The Coin: its copy costs (1) and returns that Mana. The per-turn limit ends the chain.
    var coin = ToHand(g, boss, Coin);
    Mana(boss, 1);
    int plays = 0;
    IPlayable next = coin;
    while (next != null && plays < 40)
    {
        if (!g.Process(PlayCardTask.Any(boss, next, null, -1, 0))) break;
        plays++;
        next = boss.HandZone.LastOrDefault(p => p.Card.Id == Coin && p[GameTag.GHOSTLY] == 1 && p.Zone == boss.HandZone);
    }
    int copies = EncounterRules.EchoCopiesThisTurn(boss);
    Check("G7 The Coin, then its copies at (1) each: the chain ends when the boss has received ten Echo copies this turn (EncounterRules.EchoCopiesPerTurn: its Moonfire's and nine Coins'), the last copy has no Echo",
        EncounterRules.EchoCopiesPerTurn == 10 && copies == 10 && plays == 10 && boss.HandZone.Count(p => p.Card.Id == Coin) == 1 && !boss.HandZone.Last(p => p.Card.Id == Coin).IsEcho,
        "plays " + plays + " copies " + copies);
    var fb = ToHand(g, boss, Fireball);
    Check("G8 at the limit the boss's spells have no Echo; the run player's still do", !fb.IsEcho && ToHand(g, player, Fireball).IsEcho);
    EndTurn(g);
    var later = ToHand(g, player, Moonfire);
    Check("G9 the next turn: Echo again for the boss's hand, the copies of the last turn are gone", boss.HandZone.Where(p => p is Spell).All(p => p.IsEcho) && later.IsEcho
        && EncounterRules.EchoCopiesThisTurn(boss) == 0 && boss.HandZone.Count(p => p[GameTag.GHOSTLY] == 1) == 0,
        string.Join(",", boss.HandZone.Select(p => p.Card.Id + (p.IsEcho ? "+e" : "") + (p[GameTag.GHOSTLY] == 1 ? "+g" : ""))) + " copies " + EncounterRules.EchoCopiesThisTurn(boss));
    var other = Fight("gila_45_sthara");
    var ghost = Entity.FromCard(other.Player1, C(Moonfire), new EntityData { { GameTag.GHOSTLY, 1 } }, other.Player1.HandZone);
    Check("G10 outside The Whisperer's fight an Echo copy is unchanged (EncounterRules.EchoCopyFloor does nothing)", !EncounterRules.EchoRuleActive(other) && ghost.Cost == 0 && !ghost.IsEcho);
});

// ============================================================ H  Sephira Dusktalon: Tangled Wrath
Guard("Sephira", () =>
{
    var g = Fight("gila_63_sephira");
    Controller boss = g.Player2, player = g.Player1;
    Check("H1 Tangled Wrath is a passive power started at match start; its printed (2) is never asked", PassiveStarted(boss, "GILA_BOSS_63p") && C("GILA_BOSS_63p").Cost == 2);
    for (int i = 0; i < 3; i++) ToDeck(boss, Fireball);
    int hand = boss.HandZone.Count, deck = boss.DeckZone.Count;
    Cast(g, Moonfire, boss.Hero);
    var drawn = boss.HandZone.Skip(hand).ToList();
    Check("H2 the run player casts a spell: the boss draws a spell from its deck (one of its three Fireballs)", drawn.Count == 1 && drawn[0].Card.Id == Fireball && InDeck(boss, Fireball) == 2
        && boss.DeckZone.Count == deck - 1, string.Join(",", drawn.Select(p => p.Card.Id)));
    hand = boss.HandZone.Count;
    Board(g, player, Yeti);
    Check("H3 a minion played is no spell: nothing", boss.HandZone.Count == hand);
    ToBoss(g);
    hand = boss.HandZone.Count;
    Cast(g, Moonfire, player.Hero);
    Check("H4 the boss's own spell: nothing", boss.HandZone.Count == hand - 0 && InDeck(boss, Fireball) == 2);
    ToPlayer(g);
    foreach (var p in boss.DeckZone.Where(p => p.Card.Id == Fireball).ToArray()) { boss.DeckZone.Remove(p); boss.SetasideZone.Add(p); }
    hand = boss.HandZone.Count; deck = boss.DeckZone.Count;
    Cast(g, Moonfire, boss.Hero);
    Check("H5 no spell left in the boss's deck: nothing is drawn", boss.HandZone.Count == hand && boss.DeckZone.Count == deck);
});

// ============================================================ I  Winslow Tobtock: Hypnotize
Guard("Winslow", () =>
{
    var g = Fight("gila_64_winslow");
    Controller boss = g.Player2, player = g.Player1;
    var yeti = Board(g, player, Yeti);
    ToBoss(g);
    Check("I1 Hypnotize costs (2) and is a clickable power with no target", Cost("GILA_BOSS_64p") == 2 && !boss.Hero.HeroPower.IsPassiveHeroPower && Offered(g) && PowerTargets(g).Count == 0
        && DungeonRunEncounters.Find("GILA_BOSS_64p")?.Kind == EncounterCardKind.Power);
    // The run player's Yeti goes back to its hand by Sap: a public card in a hidden hand that the boss's fair view knows.
    bool sapped = Cast(g, Sap, yeti);
    var (view0, vb0, vp0) = FairView(g);
    bool knownBefore = vp0.HandZone.Any(p => p.Id == yeti.Id && p.Card.Id == Yeti);
    var bossCard = ToHand(g, boss, Fireball);
    Generic.AddEnchantmentBlock(g, C("GILA_BOSS_46e"), boss.Hero, ToHand(g, boss, Raptor));
    var bossHand = boss.HandZone.Select(p => p.Id).ToList(); var playerHand = player.HandZone.Select(p => p.Id).ToList();
    int bossDeck = boss.DeckZone.Count, playerDeck = player.DeckZone.Count;
    int resets = g.RunBeliefResets.Count, deckFacts = g.RunDeckFacts.Count;
    Mana(boss);
    bool used = Power(g);
    Check("I2 each player shuffles its hand into its deck and draws as many: hand and deck sizes are kept", sapped && used && boss.HandZone.Count == bossHand.Count && player.HandZone.Count == playerHand.Count
        && boss.DeckZone.Count == bossDeck && player.DeckZone.Count == playerDeck && boss.UsedMana == 2, $"boss {boss.HandZone.Count}/{bossHand.Count} player {player.HandZone.Count}/{playerHand.Count}");
    var raptor = (IPlayable)g.IdEntityDic[bossHand.Last()];
    Check("I3 the cards went into the decks as plain cards (a buffed card loses its buff), and the new hands come from the decks",
        bossHand.All(id => g.IdEntityDic[id].Zone == boss.DeckZone || g.IdEntityDic[id].Zone == boss.HandZone) && playerHand.All(id => g.IdEntityDic[id].Zone == player.DeckZone || g.IdEntityDic[id].Zone == player.HandZone)
        && !Has(raptor, "GILA_BOSS_46e") && ((Minion)raptor).AttackDamage == 3 && bossCard.Zone != null,
        "raptor " + raptor.Card.Id + " " + raptor.Zone?.Type + " buff " + Has(raptor, "GILA_BOSS_46e") + " zones " + string.Join(",", bossHand.Concat(playerHand).Select(id => g.IdEntityDic[id].Zone?.Type.ToString())));
    var newResets = g.RunBeliefResets.Skip(resets).ToList();
    Check("I4 a belief reset of hand and deck is recorded for each player, after the draws (the new hand and the whole deck)", newResets.Count == 2
        && newResets.Any(r => r.ControllerId == boss.PlayerId && r.Zones == RunBeliefZones.HandAndDeck && r.EntityIds.Count == boss.HandZone.Count + boss.DeckZone.Count)
        && newResets.Any(r => r.ControllerId == player.PlayerId && r.Zones == RunBeliefZones.HandAndDeck && r.EntityIds.Contains(yeti.Id)));
    var fact = g.RunDeckFacts.LastOrDefault();
    Check("I5 the boss's own-deck ledger is told its deck's exact list", g.RunDeckFacts.Count == deckFacts + 1 && fact.Kind == RunDeckFactKind.Rewritten && fact.ControllerId == boss.Id
        && fact.Definitions.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(boss.DeckZone.Select(p => p.Card.Id).OrderBy(x => x, StringComparer.Ordinal)));
    var (view, vb, vp) = FairView(g);
    bool knownAfter = vp.HandZone.Any(p => p.Id == yeti.Id && p.Card.Id == Yeti);
    Check("I6 the boss's fair view knew the sapped Yeti in the run player's hand before; after Hypnotize it does not, wherever the Yeti is now",
        knownBefore && !knownAfter && vp.HandZone.Count == player.HandZone.Count && vb.HandZone.Select(p => p.Card.Id).OrderBy(x => x).SequenceEqual(boss.HandZone.Select(p => p.Card.Id).OrderBy(x => x)),
        "before " + knownBefore + " after " + knownAfter);
    var g2 = Fight("gila_64_winslow");
    ToBoss(g2);
    EmptyHand(g2.Player1); EmptyHand(g2.Player2);
    Check("I7 with both hands empty it is not offered", !Offered(g2));
    ToHand(g2, g2.Player1, Wisp);
    Check("I8 one card in the run player's hand is enough", Offered(g2));
    var g3 = Fight("gila_64_winslow", history: false);
    ToBoss(g3);
    EmptyDeck(g3.Player1);
    int ph = g3.Player1.HandZone.Count;
    bool ok3 = Power(g3);
    Check("I9 a run player with an empty deck draws its own shuffled cards back (no Fatigue)", ok3 && g3.Player1.HandZone.Count == ph && g3.Player1.DeckZone.Count == 0 && g3.Player1.Hero.Damage == 0
        && g3.Player2.Hero.HeroPower.IsExhausted);
});

// ============================================================ J  Gobbles: Starving
Guard("Gobbles", () =>
{
    var g = Fight("gila_65_gobbles");
    Controller boss = g.Player2, player = g.Player1;
    Check("J1 Starving is a passive power started at match start", PassiveStarted(boss, "GILA_BOSS_65p"));
    ToBoss(g);
    int hand = boss.HandZone.Count;
    var raptor = Board(g, boss, Raptor);
    Kill(g, raptor);
    Check("J2 a friendly Beast dies: the boss draws a card", boss.HandZone.Count == hand + 1, (boss.HandZone.Count - hand).ToString());
    hand = boss.HandZone.Count;
    Kill(g, Board(g, boss, Yeti));
    Kill(g, Board(g, player, Croc));
    Check("J3 a friendly non-Beast or an enemy Beast: nothing", boss.HandZone.Count == hand);
    Kill(g, Board(g, boss, Amalgam));
    Check("J4 a minion of every type is a Beast", boss.HandZone.Count == hand + 1);
});

// ============================================================ K  The Mute: ...
Guard("Mute", () =>
{
    var g = Fight("gila_66_mute");
    Controller boss = g.Player2, player = g.Player1;
    var squire = Board(g, player, Squire);
    ToBoss(g);
    var own = Board(g, boss, Croc);
    Generic.AddEnchantmentBlock(g, C("GILA_BOSS_46e"), boss.Hero, own);
    var targets = PowerTargets(g);
    Check("K1 ... costs (1); any minion, never a hero", Cost("GILA_BOSS_66p") == 1 && !boss.Hero.HeroPower.IsPassiveHeroPower && targets.Count == 2 && targets.Contains(squire) && targets.Contains(own),
        string.Join(",", targets.Select(t => t.Card.Id)));
    Check("K2 it silences the minion: Divine Shield gone", Power(g, squire) && squire.IsSilenced && !squire.HasDivineShield && boss.UsedMana == 1);
    EndTurn(g); EndTurn(g); Mana(boss);
    Check("K3 a buffed friendly minion loses its buff", Power(g, own) && own.IsSilenced && own.AttackDamage == 2 && own.Health == 3);
});

// ============================================================ L  Ratcatcher Hannigul: Cannibalism
Guard("Hannigul", () =>
{
    var g = Fight("gila_67_hannigul");
    Controller boss = g.Player2, player = g.Player1;
    Check("L1 Cannibalism is a passive power started at match start", PassiveStarted(boss, "GILA_BOSS_67p"));
    var a = Board(g, boss, Wisp); var b = Board(g, boss, Croc); var c = Board(g, boss, Yeti); var d = Board(g, boss, Raptor); var e = Board(g, boss, Wisp);
    Kill(g, c);
    Check("L2 a minion dies: the two minions next to it gain +1 Attack (Mmm, so tasty!), no one else", b.AttackDamage == 3 && d.AttackDamage == 4 && Has(b, "GILA_BOSS_67e") && Has(d, "GILA_BOSS_67e")
        && a.AttackDamage == 1 && e.AttackDamage == 1 && b.Health == 3, $"{a.AttackDamage} {b.AttackDamage} {d.AttackDamage} {e.AttackDamage}");
    Kill(g, a);
    Check("L3 at the edge: its one neighbour", b.AttackDamage == 4 && d.AttackDamage == 4 && e.AttackDamage == 1);
    var t1 = Board(g, player, Croc); var t2 = Board(g, player, Wisp); var t3 = Board(g, player, Croc);
    Kill(g, t2);
    Check("L4 the run player's board too", t1.AttackDamage == 3 && t3.AttackDamage == 3);
    // Two neighbours die together: b d e on the boss's board, d and e die; b gains from d only (+1), e's only other neighbour d is dying.
    int bAttack = b.AttackDamage;
    KillAll(g, d, e);
    Check("L5 minions that die together: a surviving neighbour gains once per dying neighbour, a dying one nothing", b.AttackDamage == bAttack + 1 && boss.BoardZone.Count == 1, b.AttackDamage - bAttack + "");
    var x1 = Board(g, boss, Wisp); var x2 = Board(g, boss, Wisp); var x3 = Board(g, boss, Croc); var x4 = Board(g, boss, Wisp);
    bAttack = b.AttackDamage;
    KillAll(g, x1, x3);
    Check("L6 b x1 x2 x3 x4, x1 and x3 die together: b +1 (x1), x2 +2 (x1 and x3), x4 +1 (x3); the positions are those before the deaths",
        b.AttackDamage == bAttack + 1 && x2.AttackDamage == 3 && x4.AttackDamage == 2, $"b+{b.AttackDamage - bAttack} x2 {x2.AttackDamage} x4 {x4.AttackDamage}");
    var g2 = Fight("gila_67_hannigul", shrine: Shrine);
    var p2 = g2.Player1;
    var shrine = ShrineRules.Of(p2).First();
    var right = Board(g2, p2, Croc); var far = Board(g2, p2, Croc);
    int shrineAt = shrine.ZonePosition;
    Kill(g2, shrine);
    var form = ShrineRules.Of(p2).FirstOrDefault();
    Check("L7 a run shrine that dies is a death: its neighbour gains +1 Attack; its dormant form takes its slot", shrineAt == 0 && right.AttackDamage == 3 && far.AttackDamage == 2
        && form != null && form.IsDormant && form.ZonePosition == 0, $"{right.AttackDamage} {far.AttackDamage}");
    Kill(g2, right);
    Check("L8 a dormant shrine next to a minion that dies gains nothing; the other neighbour does", far.AttackDamage == 3 && form.AttackDamage == C(form.Card.Id).ATK && !Has(form, "GILA_BOSS_67e"));
});

// ============================================================ M  Plaguemaster Rancel: Poison Flask, Infected Quillflinger
Guard("Rancel", () =>
{
    var g = Fight("gila_68_rancel");
    Controller boss = g.Player2, player = g.Player1;
    var yeti = Board(g, player, Yeti); var squire = Board(g, player, Squire); var wisp = Board(g, player, Wisp);
    ToBoss(g);
    var targets = PowerTargets(g);
    Check("M1 Poison Flask costs (2); any minion, never a hero", Cost("GILA_BOSS_68p") == 2 && targets.Count == 3 && !targets.Any(t => t is Hero));
    Check("M2 2 damage; the minion survives: it gains Poisonous (Dripping Poison)", Power(g, yeti) && yeti.Damage == 2 && yeti.Poisonous && Has(yeti, "GILA_BOSS_68e") && boss.UsedMana == 2);
    EndTurn(g); EndTurn(g); Mana(boss);
    Check("M3 Divine Shield takes the hit: the minion survives and gains Poisonous", Power(g, squire) && squire.Damage == 0 && !squire.HasDivineShield && squire.Poisonous);
    EndTurn(g); EndTurn(g); Mana(boss);
    Check("M4 a minion the hit kills gains nothing", Power(g, wisp) && wisp.IsDead || wisp.Zone == player.GraveyardZone);
    // Infected Quillflinger.
    var g2 = Fight("gila_68_rancel");
    boss = g2.Player2; player = g2.Player1;
    var quill = Board(g2, boss, Quill);
    Check("M5 Infected Quillflinger is the client's 3/7 for (4)", quill.AttackDamage == 3 && quill.Health == 7 && C(Quill).Cost == 4 && C(Quill).Implemented);
    int hp = player.Hero.Health;
    Cast(g2, Moonfire, quill);
    Check("M6 it takes damage with no enemy minion: nothing (never the hero)", quill.Damage == 1 && player.Hero.Health == hp);
    var e1 = Board(g2, player, Yeti); var e2 = Board(g2, player, Yeti);
    Cast(g2, Moonfire, quill);
    Check("M7 it takes damage: 1 damage to one random enemy minion", quill.Damage == 2 && e1.Damage + e2.Damage == 1);
    var picks = new HashSet<int>();
    for (int i = 0; i < 10; i++) { int before1 = e1.Damage; Cast(g2, Moonfire, quill); picks.Add(e1.Damage > before1 ? 1 : 2); if (quill.Zone != boss.BoardZone) break; }
    Check("M8 the pick is random (both enemy minions are hit over several hits); the hit that kills it still triggers (six hits with an enemy minion on the board)", picks.Count == 2 && quill.Zone == boss.GraveyardZone && e1.Damage + e2.Damage == 6,
        "e1 " + e1.Damage + " e2 " + e2.Damage);
    var mine = Board(g2, player, Quill); var target = Board(g2, boss, Yeti);
    Cast(g2, Moonfire, mine);
    Check("M9 in the run player's hands (taken from a boss) it hits the boss's minion", target.Damage == 1);
});

// ============================================================ P  the rows, the admission rows, the validator
Guard("Rows", () =>
{
    var errors = DungeonRunValidator.Validate(data, null);
    Check("P1 the shipped data validates with the thirteen family 12 rows", errors.Count == 0, string.Join(" | ", errors.Take(4)));
    var rows = new (string id, string n, string name, string tier, int printed, string kind, string[] flags, bool passive)[]
    {
        ("gila_24_groddo", "24", "Groddo the Bogwarden", "2", 30, "substituted", new[] { "passive" }, true),
        ("gila_30_gretta", "30", "Blood Witch Gretta", "2", 30, "authentic", new[] { "passive" }, true),
        ("gila_39_forlorn_lovers", "39", "Forlorn Lovers", "4", 70, "authentic", new[] { "passive" }, true),
        ("gila_45_sthara", "45", "Cultist S'thara", "3", 40, "extended", new[] { "passive", "bothPlayers" }, true),
        ("gila_46_vitus", "46", "Vitus the Exiled", "3", 40, "authentic", new[] { "passive" }, true),
        ("gila_47_shudderwock", "47", "Shudderwock", "4", 50, "substituted", new[] { "passive", "bothPlayers", "doubleBattlecry" }, true),
        ("gila_50_whisperer", "50", "The Whisperer", "2", 25, "substituted", new[] { "passive", "bothPlayers", "recast" }, true),
        ("gila_63_sephira", "63", "Sephira Dusktalon", "3", 40, "substituted", new[] { "passive" }, true),
        ("gila_64_winslow", "64", "Winslow Tobtock", "3", 40, "authentic", new[] { "bothPlayers", "hiddenZone" }, false),
        ("gila_65_gobbles", "65", "Gobbles", "1", 10, "extended", new[] { "passive" }, true),
        ("gila_66_mute", "66", "The Mute", "2", 25, "extended", new string[0], false),
        ("gila_67_hannigul", "67", "Ratcatcher Hannigul", "3", 40, "substituted", new[] { "passive", "bothPlayers", "position" }, true),
        ("gila_68_rancel", "68", "Plaguemaster Rancel", "3", 40, "authentic", new string[0], false),
    };
    Check("P2 family 12 holds exactly these thirteen rows", data.Encounters.Where(b => b.Family == "12").Select(b => b.Id).OrderBy(i => i, StringComparer.Ordinal)
        .SequenceEqual(rows.Select(r => r.id).OrderBy(i => i, StringComparer.Ordinal)), string.Join(",", data.Encounters.Where(b => b.Family == "12").Select(b => b.Id)));
    foreach (var r in rows)
    {
        var b = data.Boss(r.id);
        var plan = RowPlan(r.id);
        string hero = "GILA_BOSS_" + r.n + "h", power = "GILA_BOSS_" + r.n + "p";
        var kind = DungeonRunEncounters.Find(power)?.Kind;
        Check("P3 " + r.id + ": a Monster Hunt row of tier " + r.tier + " (printed Health " + r.printed + ") whose plan is the row's (hero, " + (r.passive ? "passive" : "clickable") + " power, a 30-card " + r.kind
            + " deck, the act curve's Health), flags [" + string.Join(",", r.flags) + "]",
            b.Provenance == "GIL" && b.Name == r.name && b.Tier == r.tier && b.HeroCard == hero && b.Power == power && b.Source.Kind == r.kind && b.Flags.OrderBy(f => f).SequenceEqual(r.flags.OrderBy(f => f))
            && plan.EncounterId == r.id && plan.EncounterDeck && plan.BossHero.Id == hero && plan.BossHeroPowerId == power && plan.BossDeck.Count == 30 && data.BossDecks[b.Deck].Count == 30
            && !b.FixedHealth && b.Health == null && plan.BossHealth == 30 && C(hero).Type == CardType.HERO && C(hero).Health == r.printed && C(power).Type == CardType.HERO_POWER
            && DungeonRunEncounters.PassivePowers.Contains(power) == r.passive && kind == (r.passive ? EncounterCardKind.Passive : EncounterCardKind.Power) && C(hero).Name == r.name
            && data.Schedules.Member(b, DungeonRunEncounterData.PoolOf(b.Tier)) && b.Source.Url.StartsWith("https://hearthstone.wiki.gg/wiki/") && !b.BossGeneratedSecrets && b.Forms.Count == 0 && b.Start == null,
            b.Source.Kind + " health " + plan.BossHealth + " flags " + string.Join(",", b.Flags) + " tier " + b.Tier);
    }
    string[] powers = rows.Select(r => "GILA_BOSS_" + r.n + "p").ToArray();
    string[] cards = { Zombie, Quill };
    string[] enchantments = { "GILA_BOSS_46e", "GILA_BOSS_67e", "GILA_BOSS_68e" };
    var scripted = powers.Concat(cards).ToArray();
    var unscripted = scripted.Concat(enchantments).Where(id => !C(id).Implemented || DungeonRunEncounters.Find(id)?.Family != "12").ToList();
    // The engine's loader marks a text with a damage number ("$2") with a trailing " @spelldmg" (Poison Flask).
    var retexted = scripted.Concat(enchantments).Where(id => C(id).Text.Replace(" @spelldmg", "") != factById[id].GetProperty("text").GetString()).ToList();
    Check("P4 every power, boss card and enchantment of the family is scripted by family 12 and keeps the client's text (13 powers, 2 cards, 3 enchantments)", unscripted.Count == 0 && retexted.Count == 0
        && scripted.Length == 15, string.Join(",", unscripted.Concat(retexted.Select(id => id + " [" + C(id).Text + "] vs [" + factById[id].GetProperty("text").GetString() + "]"))));
    var wrongScope = scripted.Where(id =>
    {
        var v = DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss, null, catalogRun: true);
        return !(v.Admitted && v.Subject && v.Rule?.Ids != null && v.Rule.Ids.Contains(id) && DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss).Admitted
            && DungeonRunAdmission.Classify(C(id), id, AdmitScope.Player, null, catalogRun: true).Admitted == cards.Contains(id)
            && !DungeonRunAdmission.Classify(C(id), id, AdmitScope.Generated, null, catalogRun: true).Admitted
            && DungeonRunAdmission.GeneratedRejects(true).Contains(id) && DungeonRunAdmission.GeneratedRejects(false).Contains(id));
    }).ToList();
    Check("P5 each has its own admission row: the thirteen powers a boss's only, the two boss cards a boss's and (taken from a boss) the run player's; none is ever generated", wrongScope.Count == 0, string.Join(",", wrongScope));
    var families = scripted.Select(id => DungeonRunAdmission.RuleFor(id)?.Family).Distinct().OrderBy(f => f, StringComparer.Ordinal).ToList();
    int Ids(string family) => DungeonRunAdmission.Rules.Where(r => r.Family == family).Sum(r => r.Ids.Length);
    Check("P6 the six admission families of the family: boss-passive-public (5), boss-passive-both (4), boss-deck-rewrite (1), boss-hidden-shuffle (1), boss-minion-power (2), boss-hunt-minion (2)",
        families.SequenceEqual(new[] { "boss-deck-rewrite", "boss-hidden-shuffle", "boss-hunt-minion", "boss-minion-power", "boss-passive-both", "boss-passive-public" })
        && Ids("boss-passive-public") == 5 && Ids("boss-passive-both") == 4 && Ids("boss-deck-rewrite") == 1 && Ids("boss-hidden-shuffle") == 1 && Ids("boss-minion-power") == 2 && Ids("boss-hunt-minion") == 2,
        string.Join(",", families));
    string Subs(string id) => string.Join(",", data.Boss(id).Source.Substitutions.Where(x => x.Listed != null).Select(x => x.Listed + ">" + (x.Used ?? "-") + ":" + x.Reason).Distinct().OrderBy(x => x, StringComparer.Ordinal));
    Check("P7 the listed cards that are replaced or left out are only cards the engine does not script (or the 31st card of Shudderwock's list), each recorded",
        Subs("gila_24_groddo") == "GILA_BOSS_99t2>EX1_606:unscripted" && Subs("gila_67_hannigul") == "GILA_BOSS_99t2>EX1_606:unscripted"
        && Subs("gila_63_sephira") == "EX1_154>LOOT_051:unscripted,EX1_160>CFM_614:unscripted,GILA_BOSS_99t2>EX1_606:unscripted"
        && Subs("gila_50_whisperer") == "EX1_154>LOOT_051:unscripted" && Subs("gila_47_shudderwock") == "UNG_088>-:over-size"
        && rows.Where(r => r.kind == "authentic").All(r => data.Boss(r.id).Source.Substitutions.Count == 0)
        && rows.SelectMany(r => data.Boss(r.id).Source.Substitutions).Where(x => x.Reason == "unscripted").All(x => !C(x.Listed).Implemented),
        Subs("gila_24_groddo") + " | " + Subs("gila_63_sephira") + " | " + Subs("gila_50_whisperer") + " | " + Subs("gila_47_shudderwock"));
    int In(string boss, string card) => data.BossDecks[data.Boss(boss).Deck].Count(id => id == card);
    Check("P8 the boss cards are where the wiki lists them: three Infected Quillflingers in Rancel's deck; the Bloated Zombie only rises (in no deck)",
        In("gila_68_rancel", Quill) == 3 && rows.All(r => In(r.id, Zombie) == 0) && data.Boss("gila_24_groddo").Cards.Contains(Zombie));
    var playerPower = DungeonRunAdmission.Classify(C("GILA_BOSS_64p"), "GILA_BOSS_64p", AdmitScope.Player, null, catalogRun: true);
    Check("P9 a family 12 power asked for the run player is refused with its row's reason", !playerPower.Admitted && playerPower.Family == "boss-hidden-shuffle" && playerPower.Lift == DungeonRunAdmission.NoneBossPower);
    Check("P10 the tier pools: one row in enc-t1, four in enc-t2, six in enc-t3, two in enc-t4", rows.Count(r => r.tier == "1") == 1 && rows.Count(r => r.tier == "2") == 4 && rows.Count(r => r.tier == "3") == 6
        && rows.Count(r => r.tier == "4") == 2 && rows.All(r => data.Boss(r.id).Pools.SequenceEqual(new[] { "enc-t" + r.tier })));
    var wildMagic = data.Schedules.Rules.FirstOrDefault(x => x.Id == "DALA_856"); var echoes = data.Schedules.Rules.FirstOrDefault(x => x.Id == "DALA_852");
    Check("P11 the flags keep the anomalies that would double a rule away: Wild Magic (recast) from The Whisperer, Battlecries twice (doubleBattlecry) from Shudderwock",
        wildMagic != null && wildMagic.IncompatibleFlags.Contains("recast") && echoes != null && echoes.IncompatibleFlags.Contains("doubleBattlecry"));
});

Console.WriteLine($"SearchTelemetry swallowed leaf exceptions {SearchTelemetry.SwallowedLeafErrors}");
Console.WriteLine($"RESULT {passes} passed, {failures + SearchTelemetry.SwallowedLeafErrors} failed");
return failures + SearchTelemetry.SwallowedLeafErrors;
