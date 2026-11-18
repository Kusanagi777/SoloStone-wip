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

// Phase 8 family 16, Dalaran Heist bosses C (real engine games through the run setup path: DungeonRun.BuildPlan on the shipped
// row -> DungeonRunSetup.Configure / ApplyAfterStart): Timothy Jones (Bling it On!), Kizi Copperclip (Makeover, auto-cast),
// Gold Elemental (Made of Coins), the Carousel Gryphon (Merry Go Round, the board rotation), Soothsayer Zoie (Healing Hands),
// Draemus (Import "Pet", the Exotic Pet), Ol' Toomba (Tales of Fortune and the treasures it shuffles in), Tipsi Wobblerune
// (Portal Party, PARTY PORTAL!, the Mime), Aki the Brilliant (For The Light!, auto-cast), Oxana Demonslay (Immolation Aura),
// Ungan Oddkind (Summon Companion), P.O.G.O. (Pogoshuffle), Erekem (Dark Tidings) and Moragg (Ray of Suffering), each with the
// shrine rules that touch it, the treasures newly admitted for a boss, and the rows, admission rows and validator.
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

const string Yeti = "CS2_182", Croc = "CS2_120", Raptor = "CS2_172", Moonfire = "CS2_008", Fireball = "CS2_029", Intellect = "CS2_023";
const string Wisp = "CS2_231", Squire = "EX1_008", Infiltrator = "EX1_010", Tiger = "EX1_028", Coin = "GAME_005", FakeCoin = "CFM_630";
const string FlashHeal = "AT_055", Auchenai = "EX1_591", Counterspell = "EX1_287", CheapShot = "GIL_506", Shrine = "TRLA_154", Covenant = "TRLA_147";
const string Portal = "DALA_BOSS_38t", Mime = "KARA_13_22", Pet = "DALA_BOSS_36t", Bling = "DALA_BOSS_31e", Light = "DALA_BOSS_39e", Forseen = "DALA_BOSS_44e";
const string Orb = "LOOTA_811", Horn = "LOOTA_837", Forge = "LOOTA_841", Blade = "LOOTA_842a", Hilt = "LOOTA_842b", QuelDelar = "LOOTA_842";
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
// hero, power, Health, forms, start block and every other encounter field stay the row's).
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
void EmptyHand(Controller c) { foreach (var p in c.HandZone.ToArray()) { c.HandZone.Remove(p); c.SetasideZone.Add(p); } }
void ToDeck(Controller c, string id) => Generic.ShuffleIntoDeck(c, c.Hero, Entity.FromCard(c, C(id)));
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
void Hit(Game g, ICharacter target, int amount, IPlayable source) { Generic.DamageCharFunc.Invoke(source, target, amount, false); g.DeathProcessingAndAuraUpdate(); }
void Silence(Game g, Minion m) { new SilenceTask(EntityType.TARGET).Process(g, m.Controller, m.Controller.Hero, m, null); g.DeathProcessingAndAuraUpdate(); }
bool Attack(Game g, Minion m, ICharacter target) { m.IsExhausted = false; return g.Process(MinionAttackTask.Any(g.CurrentPlayer, m, target)); }
int Count(Controller c, string id) => c.BoardZone.Count(m => m.Card.Id == id);
bool Has(IPlayable p, string enchantmentId) => p.AppliedEnchantments?.Any(e => e.Card.Id == enchantmentId) == true;
int Cost(string id) => C(id).Cost;
(Game view, Controller boss, Controller player) FairView(Game g)
{
    var bot = FairBotState.Create(g, g.Player2.PlayerId, 1, DeckStyle.Tempo);
    var view = bot.FingerprintGame;
    var viewBoss = view.Player1.PlayerId == g.Player2.PlayerId ? view.Player1 : view.Player2;
    return (view, viewBoss, viewBoss.Opponent);
}
bool ViewOffered(Game g)
{
    var (view, viewBoss, _) = FairView(g);
    return viewBoss.Options().Any(o => o is HeroPowerTask);
}
Game FightWith(string bossId, IEnumerable<string> bossDeck, string playerDeck = Yeti, string shrine = null, int seed = 7)
{
    var plan = RowPlan(bossId);
    plan.BossDeck = bossDeck.Select(C).ToList();
    plan.PlayerDeck = Thirty(playerDeck).Select(C).ToList();
    plan.PlayerDeckSlots = null;
    if (shrine != null) plan.PlayerShrine = shrine;
    return Ready(plan, seed);
}
int Casts(Controller c) => DungeonRunEncounters.AutoCasts(c.Hero.HeroPower);
// The boss's next turn starts with exactly `mana` Mana Crystals (the turn start adds one).
void BossTurnWith(Game g, int mana) { if (g.CurrentPlayer == g.Player2) EndTurn(g); g.Player2.BaseMana = mana - 1; EndTurn(g); }
void ClearBoard(Game g, Controller c) { foreach (var m in c.BoardZone.ToArray()) { m.ActivatedTrigger?.Remove(); m.OngoingEffect?.Remove(); c.BoardZone.Remove(m); c.SetasideZone.Add(m); } g.DeathProcessingAndAuraUpdate(); }
bool Catalog(string id, AdmitScope scope) => DungeonRunAdmission.Classify(C(id), id, scope, null, catalogRun: true).Admitted;
bool Legacy(string id, AdmitScope scope) => DungeonRunAdmission.Classify(C(id), id, scope).Admitted;
string Row(Controller c) => string.Join(",", c.BoardZone.Select(m => m.Id));
Dictionary<string, int> Ledger(Game g, Controller c) => OwnDeckLedger.Observe(g, c, null).Sample(g, c, 7);
Dictionary<string, int> Multiset(Controller c) => c.DeckZone.GroupBy(p => p.Card.Id).ToDictionary(x => x.Key, x => x.Count());
bool SameCounts(Dictionary<string, int> a, Dictionary<string, int> b) => a.Count == b.Count && a.All(p => b.TryGetValue(p.Key, out int n) && n == p.Value);
string Show(Dictionary<string, int> d) => string.Join(",", d.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + "x" + p.Value));

// ============================================================ A  Timothy Jones: Bling it On!
Guard("Timothy", () =>
{
    var g = Fight("dala_31_timothy");
    Controller boss = g.Player2, player = g.Player1;
    ToBoss(g);
    Check("A1 with no minion on a board Bling it On! is not offered", !Offered(g));
    var yeti = Board(g, player, Yeti);
    var own = Board(g, boss, Croc);
    var targets = PowerTargets(g);
    Check("A2 Bling it On! costs (2), is clickable and can be aimed at any minion of either board, never a hero", Cost("DALA_BOSS_31p") == 2 && !boss.Hero.HeroPower.IsPassiveHeroPower
        && targets.Count == 2 && targets.Contains(own) && targets.Contains(yeti), string.Join(",", targets.Select(t => t.Card.Id)));
    bool used = Power(g, own);
    Check("A3 a minion that is not Golden becomes Golden (the client's PREMIUM tag, a public tag change) and keeps its stats", used && own[GameTag.PREMIUM] == 1 && DungeonRunEncounters.IsGolden(own)
        && own.AttackDamage == 2 && own.Health == 3 && !Has(own, Bling) && boss.UsedMana == 2);
    var (view, viewBoss, _) = FairView(g);
    var seen = viewBoss.BoardZone.FirstOrDefault(m => m.Id == own.Id);
    Check("A4 the boss's fair view sees the Golden tag (a board minion's public tag)", seen != null && seen[GameTag.PREMIUM] == 1);
    Round(g);
    Power(g, own);
    Check("A5 used on an already Golden minion it gives Blinged Out! (+4/+4) instead; the minion stays Golden", Has(own, Bling) && own.AttackDamage == 6 && own.Health == 7 && own[GameTag.PREMIUM] == 1);
    Round(g);
    Power(g, yeti);
    Round(g);
    Power(g, yeti);
    Check("A6 the run player's minion: Golden first, then +4/+4 (8/9)", yeti[GameTag.PREMIUM] == 1 && Has(yeti, Bling) && yeti.AttackDamage == 8 && yeti.Health == 9);
    var raptor = Board(g, player, Raptor);
    raptor[GameTag.PREMIUM] = 1;
    Round(g);
    Power(g, raptor);
    Check("A7 a minion that came into play Golden gets +4/+4 at the first use", Has(raptor, Bling) && raptor.AttackDamage == 7 && raptor.Health == 6);
    // Griftah (his deck): "Discover two cards" queues its second Discover with no offers until the first is answered; the boss's
    // fair view is built while that chain is open (it once threw ArgumentNullException on the empty queued choice: a fallback).
    var griftah = ToHand(g, boss, "TRL_096");
    if (g.CurrentPlayer != boss) ToBoss(g);
    Mana(boss);
    Play(g, griftah);
    var queued = boss.Choice?.NextChoice;
    bool built = false; string error = "";
    try { var (v2, b2, _) = FairView(g); built = b2.Choice != null; } catch (Exception e) { error = e.GetType().Name + ": " + e.Message; }
    Check("A8 Griftah's open Discover chain (a queued second choice without offers yet): the boss's fair view is built", boss.Choice != null && queued != null && queued.Choices == null && built, error);
    g.Process(ChooseTask.Pick(boss, boss.Choice.Choices[0]));
    g.Process(ChooseTask.Pick(boss, boss.Choice.Choices[0]));
    Check("A9 both Discovers answered: one card in each hand from Griftah, no choice left", boss.Choice == null);
});

// ============================================================ B  Kizi Copperclip: Makeover (auto-cast)
Guard("Kizi", () =>
{
    var g = Fight("dala_32_kizi");
    Controller boss = g.Player2, player = g.Player1;
    var makeover = boss.Hero.HeroPower;
    Check("B1 Makeover (1) is cast by rule (a registered passive, started at match start), never an option of the boss", makeover.Card.Id == "DALA_BOSS_32p" && makeover.IsPassiveHeroPower
        && makeover[EncounterRules.PowerActive] == 1 && Cost("DALA_BOSS_32p") == 1 && DungeonRunEncounters.PassivePowers.Contains("DALA_BOSS_32p"));
    BossTurnWith(g, 3);
    Check("B2 with no friendly minion nothing is cast and no Mana is paid", Casts(boss) == 0 && boss.UsedMana == 0 && !Offered(g));
    var croc = Board(g, boss, Croc);
    var yeti = Board(g, player, Yeti);
    BossTurnWith(g, 3);
    var cut = croc.AppliedEnchantments?.Select(e => e.Card.Id).Where(id => DungeonRunEncounters.KiziHaircuts.Contains(id)).ToList() ?? new List<string>();
    Check("B3 at the start of her turn with a friendly minion: (1) Mana is paid and it gets one of the four haircuts", Casts(boss) == 1 && boss.UsedMana == 1 && cut.Count == 1 && !Offered(g),
        string.Join(",", cut));
    Check("B4 the run player's minions never get a haircut", yeti.AppliedEnchantments == null || !yeti.AppliedEnchantments.Any(e => DungeonRunEncounters.KiziHaircuts.Contains(e.Card.Id)));
    var seen = new HashSet<string>();
    bool effects = true;
    for (int i = 0; i < 24; i++)
    {
        var m = Board(g, boss, Croc);
        foreach (var other in boss.BoardZone.Where(x => x != m).ToArray()) Kill(g, other);
        BossTurnWith(g, 2);
        var e = m.AppliedEnchantments?.FirstOrDefault(x => DungeonRunEncounters.KiziHaircuts.Contains(x.Card.Id));
        if (e == null) { effects = false; break; }
        seen.Add(e.Card.Id);
        switch (e.Card.Id)
        {
            case "DALA_BOSS_32e1": effects &= m.HasStealth && m.AttackDamage == 2 && m.Health == 3; break;
            case "DALA_BOSS_32e2": effects &= m.HasTaunt && m.AttackDamage == 2 && m.Health == 3; break;
            case "DALA_BOSS_32e3": effects &= m.Health == 5 && m.AttackDamage == 2; break;
            case "DALA_BOSS_32e4": effects &= m.AttackDamage == 4 && m.Health == 3; break;
        }
    }
    Check("B5 twenty-four casts: all four haircuts seen, each as its text (Stealth, Taunt, +2 Health, +2 Attack)", effects && seen.SetEquals(DungeonRunEncounters.KiziHaircuts), string.Join(",", seen));
});

// ============================================================ C  Gold Elemental: Made of Coins
Guard("Gold", () =>
{
    var g = Fight("dala_33_gold_elemental");
    Controller boss = g.Player2, player = g.Player1;
    var power = boss.Hero.HeroPower;
    Check("C1 Made of Coins is a passive power, never offered, started at match start", power.Card.Id == "DALA_BOSS_33p" && power.IsPassiveHeroPower && power[EncounterRules.PowerActive] == 1);
    EmptyHand(boss);
    Cast(g, Moonfire, boss.Hero);
    Cast(g, Moonfire, boss.Hero);
    Check("C2 each hit to his hero adds one Coin to his hand (two Moonfires, two Coins)", boss.HandZone.Count == 2 && boss.HandZone.All(p => p.Card.Id == Coin), string.Join(",", boss.HandZone.Select(p => p.Card.Id)));
    var croc = Board(g, boss, Croc);
    Cast(g, Moonfire, croc);
    Cast(g, Moonfire, player.Hero);
    Check("C3 damage to his minions or to the run player's hero adds none", boss.HandZone.Count == 2);
    boss.Hero.Armor = 5;
    Cast(g, Moonfire, boss.Hero);
    Check("C4 a hit his Armor absorbs whole still counts as damage taken (the wiki's Armor rule): one Coin", boss.HandZone.Count == 3 && boss.Hero.Armor == 4 && boss.Hero.Damage == 2);
    FillHand(g, boss);
    Cast(g, Moonfire, boss.Hero);
    Check("C5 with a full hand no Coin is added (the hand stays at 10)", boss.HandZone.Count == 10);
});

// ============================================================ D  Carousel Gryphon: Merry Go Round (the board rotation)
Guard("Carousel", () =>
{
    var g = Fight("dala_34_carousel");
    Controller boss = g.Player2, player = g.Player1;
    var power = boss.Hero.HeroPower;
    Check("D1 Merry Go Round is a passive power, never offered, started at match start", power.Card.Id == "DALA_BOSS_34p" && power.IsPassiveHeroPower && power[EncounterRules.PowerActive] == 1);
    var p1 = Board(g, player, Yeti); var p2 = Board(g, player, Croc); var p3 = Board(g, player, Raptor);
    ToBoss(g);
    var b1 = Board(g, boss, Wisp); var b2 = Board(g, boss, Squire);
    p2.Damage = 1; Generic.AddEnchantmentBlock(g, C("DALA_BOSS_44e"), boss.Hero.HeroPower, p3);
    int changes = g.HistoryFacts.Query.ControlChanges().Count();
    EndTurn(g);
    Check("D2 at the end of the boss's turn: counter-clockwise as the run player sees it (its row moves right, the boss's row moves left): run player [Wisp, Yeti, Croc], boss [Squire, Raptor]",
        player.BoardZone.Select(m => m.Id).SequenceEqual(new[] { b1.Id, p1.Id, p2.Id }) && boss.BoardZone.Select(m => m.Id).SequenceEqual(new[] { b2.Id, p3.Id }), Row(player) + " | " + Row(boss));
    Check("D3 the two that crossed changed controller (the CONTROLLER tag too); the others did not; each row keeps its size", b1.Controller == player && b1[GameTag.CONTROLLER] == player.PlayerId
        && p3.Controller == boss && p3[GameTag.CONTROLLER] == boss.PlayerId && p1.Controller == player && p2.Controller == player && b2.Controller == boss
        && player.BoardZone.Count == 3 && boss.BoardZone.Count == 2);
    Check("D4 a crossing minion keeps its enchantments; a moving one its damage; two control changes are recorded", Has(p3, "DALA_BOSS_44e") && p3.AttackDamage == 5 && p2.Damage == 1
        && g.HistoryFacts.Query.ControlChanges().Count() == changes + 2, (g.HistoryFacts.Query.ControlChanges().Count() - changes).ToString());
    Mana(player);
    string before = Row(player) + "|" + Row(boss);
    EndTurn(g);
    Check("D5 at the end of the run player's turn nothing rotates", Row(player) + "|" + Row(boss) == before);
    Check("D6 the minion that crossed to the boss's side can attack on the boss's turn", g.CurrentPlayer == boss && !p3.IsExhausted && boss.Options().OfType<MinionAttackTask>().Any(o => o.Source == p3));
    EndTurn(g);
    Check("D7 the minion that crossed to the run player's side can attack on its turn", g.CurrentPlayer == player && !b1.IsExhausted && player.Options().OfType<MinionAttackTask>().Any(o => o.Source == b1));

    var g2 = Fight("dala_34_carousel");
    boss = g2.Player2; player = g2.Player1;
    ToBoss(g2);
    var x1 = Board(g2, boss, Wisp); var x2 = Board(g2, boss, Croc); var x3 = Board(g2, boss, Yeti);
    EndTurn(g2);
    Check("D8 an empty run player's row gains the boss's leftmost minion: run player [Wisp], boss [Croc, Yeti]", player.BoardZone.Select(m => m.Id).SequenceEqual(new[] { x1.Id })
        && boss.BoardZone.Select(m => m.Id).SequenceEqual(new[] { x2.Id, x3.Id }), Row(player) + " | " + Row(boss));
    ClearBoard(g2, player); ClearBoard(g2, boss);
    var lone = Board(g2, player, Yeti);
    ToBoss(g2);
    EndTurn(g2);
    bool up = lone.Controller == boss;
    ToBoss(g2);
    EndTurn(g2);
    Check("D9 a single minion crosses at each of the boss's turn ends: up to the boss, then back", up && lone.Controller == player && boss.BoardZone.Count == 0 && player.BoardZone.Count == 1);
    ClearBoard(g2, player); ClearBoard(g2, boss);
    var full = new List<Minion>();
    for (int i = 0; i < 7; i++) full.Add(Board(g2, player, Wisp));
    var top = new List<Minion>();
    ToBoss(g2);
    for (int i = 0; i < 7; i++) top.Add(Board(g2, boss, Croc));
    EndTurn(g2);
    Check("D10 full rows (7 and 7): one minion crosses each way, both rows stay at 7", player.BoardZone.Count == 7 && boss.BoardZone.Count == 7 && full[6].Controller == boss && top[0].Controller == player
        && boss.BoardZone.Last() == full[6] && player.BoardZone.First() == top[0]);

    // The helper's other direction (family 24's K'zrath names counter-clockwise; clockwise is the mirror image).
    var g3 = Fight("dala_34_carousel");
    boss = g3.Player2; player = g3.Player1;
    var q1 = Board(g3, player, Yeti); var q2 = Board(g3, player, Croc);
    var r1 = Board(g3, boss, Wisp); var r2 = Board(g3, boss, Squire);
    var crossed = EncounterRules.Rotate(player, true, boss.Hero.HeroPower);
    Check("D11 EncounterRules.Rotate clockwise: the run player's leftmost goes up to the boss's left end, the boss's rightmost comes down to its right end; it returns the two that crossed",
        player.BoardZone.Select(m => m.Id).SequenceEqual(new[] { q2.Id, r2.Id }) && boss.BoardZone.Select(m => m.Id).SequenceEqual(new[] { q1.Id, r1.Id }) && crossed.Count == 2 && crossed.Contains(q1) && crossed.Contains(r2),
        Row(player) + " | " + Row(boss));

    // Shrines never change controller.
    var gs = Fight("dala_34_carousel", shrine: Shrine);
    boss = gs.Player2; player = gs.Player1;
    var shrine = ShrineRules.Of(player).FirstOrDefault();
    var s1 = Board(gs, player, Yeti); var s2 = Board(gs, player, Croc);
    ToBoss(gs);
    var t1 = Board(gs, boss, Wisp);
    EndTurn(gs);
    Check("D12 (shrine) the run player's awake shrine is left out of the ring: it stays the run player's; Croc goes up, Wisp comes down",
        shrine != null && !shrine.IsDormant && shrine.Controller == player && ShrineRules.Of(player).Contains(shrine) && s2.Controller == boss && t1.Controller == player && s1.Controller == player
        && player.BoardZone.Count == 3 && boss.BoardZone.Count == 1, Row(player) + " | " + Row(boss));
    ClearBoard(gs, boss);
    foreach (var m in player.BoardZone.Where(m => m != shrine).ToArray()) Kill(gs, m);
    ToBoss(gs);
    var t2 = Board(gs, boss, Wisp); var t3 = Board(gs, boss, Croc);
    EndTurn(gs);
    Check("D13 (shrine) a shrine alone on the run player's row: the boss's leftmost still comes down, the shrine never goes up", shrine.Controller == player && t2.Controller == player
        && boss.BoardZone.Single() == t3 && !boss.BoardZone.Any(m => ShrineRules.IsMarked(m)));
    Kill(gs, shrine);
    var dormant = ShrineRules.Of(player).FirstOrDefault();
    ToBoss(gs);
    EndTurn(gs);
    Check("D14 (shrine) a dormant shrine stays too: only the other minions rotate", dormant != null && dormant.IsDormant && dormant.Controller == player && ShrineRules.Of(boss).Count == 0
        && t2.Controller == boss, Row(player) + " | " + Row(boss));
});

// ============================================================ E  Soothsayer Zoie: Healing Hands
Guard("Zoie", () =>
{
    var g = Fight("dala_35_zoie");
    Controller boss = g.Player2, player = g.Player1;
    Check("E1 Healing Hands is a passive power, never offered, started at match start", boss.Hero.HeroPower.Card.Id == "DALA_BOSS_35p" && boss.Hero.HeroPower.IsPassiveHeroPower
        && boss.Hero.HeroPower[EncounterRules.PowerActive] == 1);
    var own = Board(g, boss, Yeti); var small = Board(g, boss, Croc); var theirs = Board(g, player, Yeti);
    own.Damage = 3; small.Damage = 1; theirs.Damage = 3; boss.Hero.Damage = 5;
    EndTurn(g);
    Check("E2 at the start of her turn each of her minions is restored 2 Health (not past its maximum); her hero and the run player's minions are not", own.Damage == 1 && small.Damage == 0
        && theirs.Damage == 3 && boss.Hero.Damage == 5, own.Damage + "/" + small.Damage + "/" + theirs.Damage);
    EndTurn(g);
    Check("E3 not at the start of the run player's turn", own.Damage == 1 && theirs.Damage == 3);
});

// ============================================================ F  Draemus: Import "Pet", the Exotic Pet
Guard("Draemus", () =>
{
    var plan = RowPlan("dala_36_draemus");
    var blocked = new HashSet<string>((plan.ExcludedGenerated ?? new string[0]).Concat(plan.ExcludedGeneratedBoss ?? new string[0]));
    var g = Fight("dala_36_draemus");
    Controller boss = g.Player2, player = g.Player1;
    ToBoss(g);
    EmptyHand(boss);
    var first = DungeonRunEncounters.ExoticPetParts(boss, true); var second = DungeonRunEncounters.ExoticPetParts(boss, false);
    Check("F1 the two part pools are the engine's Build-A-Beast pools less what the boss's generation filter denies (none of them blocked, all scripted)",
        first.Count >= 10 && second.Count >= 10 && first.Concat(second).All(c => c.Implemented && !blocked.Contains(c.Id) && c.IsRace(Race.BEAST)), first.Count + "/" + second.Count);
    bool used = Power(g);
    var choice1 = boss.Choice;
    var offers1 = choice1?.Choices.Select(id => g.IdEntityDic[id].Card.Id).ToList() ?? new List<string>();
    Check("F2 Import \"Pet\" costs (2) and opens a Discover of three first-part Beasts for the boss", Cost("DALA_BOSS_36p") == 2 && used && offers1.Count == 3 && offers1.Distinct().Count() == 3
        && offers1.All(id => first.Any(c => c.Id == id)) && boss.UsedMana == 2, string.Join(",", offers1));
    var a = g.IdEntityDic[choice1.Choices[0]].Card;
    g.Process(ChooseTask.Pick(boss, choice1.Choices[0]));
    var choice2 = boss.Choice;
    var offers2 = choice2?.Choices.Select(id => g.IdEntityDic[id].Card.Id).ToList() ?? new List<string>();
    Check("F3 then a Discover of three second-part Beasts", offers2.Count == 3 && offers2.All(id => second.Any(c => c.Id == id)), string.Join(",", offers2));
    var b = g.IdEntityDic[choice2.Choices[1]].Card;
    g.Process(ChooseTask.Pick(boss, choice2.Choices[1]));
    var pet = boss.HandZone.LastOrDefault();
    Check("F4 one minion joins his hand named Exotic Pet (the client name of DALA_BOSS_36t), made of the two picks: summed Cost, Attack and Health",
        boss.Choice == null && boss.HandZone.Count == 1 && pet is Minion && pet.Card.Name == C(Pet).Name && pet.Card.Name == "Exotic Pet"
        && pet.Card[GameTag.MODULAR_ENTITY_PART_1] == a.AssetId && pet.Card[GameTag.MODULAR_ENTITY_PART_2] == b.AssetId
        && ((Minion)pet).AttackDamage == a.ATK + b.ATK && ((Minion)pet).Health == a.Health + b.Health && pet.Card.Cost == a.Cost + b.Cost,
        pet?.Card.Name + " " + a.Id + "+" + b.Id);
    Mana(boss);
    bool played = Play(g, pet);
    Check("F5 the Exotic Pet is played as a minion of the boss", played && boss.BoardZone.Contains((Minion)pet));
    FillHand(g, boss);
    Round(g);
    Check("F6 with a full hand Import \"Pet\" is not offered", !Offered(g));
});

// ============================================================ G  Ol' Toomba: Tales of Fortune
Guard("Toomba", () =>
{
    var g = Fight("dala_37_toomba");
    Controller boss = g.Player2, player = g.Player1;
    ToBoss(g);
    int deck = boss.DeckZone.Count;
    var before = boss.DeckZone.ToList();
    bool used = Power(g);
    var added = boss.DeckZone.Where(p => !before.Contains(p)).ToList();
    Check("G1 Tales of Fortune costs (2): one card joins his deck, an old Treasure of the list", Cost("DALA_BOSS_37p") == 2 && used && boss.DeckZone.Count == deck + 1 && added.Count == 1
        && DungeonRunEncounters.ToombaTreasures.Contains(added[0].Card.Id) && added[0].Controller == boss && boss.UsedMana == 2, added.FirstOrDefault()?.Card.Id);
    var sample = Ledger(g, boss);
    Check("G2 the treasure is shown as it is shuffled: his own-deck ledger holds it (the sample equals his deck)", SameCounts(sample, Multiset(boss)) && sample.ContainsKey(added[0].Card.Id),
        Show(sample));
    var seen = new HashSet<string> { added[0].Card.Id };
    // (his hand is emptied each turn, so no draw is burned)
    for (int i = 0; i < 59; i++) { Round(g); EmptyHand(boss); var had = boss.DeckZone.ToList(); Power(g); foreach (var p in boss.DeckZone.Where(p => !had.Contains(p))) seen.Add(p.Card.Id); }
    Check("G3 sixty uses: only the twenty listed treasures, most of them seen, never Gloves of Mugging or Aleatoric Cube", seen.All(id => DungeonRunEncounters.ToombaTreasures.Contains(id))
        && seen.Count >= 15 && !seen.Contains("LOOTA_834") && !seen.Contains("LOOTA_820"), seen.Count + " " + string.Join(",", seen.OrderBy(x => x)));
    var sample2 = Ledger(g, boss);
    Check("G4 after sixty shuffles his own-deck ledger still equals his deck", SameCounts(sample2, Multiset(boss)), Show(sample2) + " vs " + Show(Multiset(boss)));
    Check("G5 the twenty: the wiki's 22 less LOOTA_834 and LOOTA_820, each scripted and admitted for a boss on catalog runs", DungeonRunEncounters.ToombaTreasures.Count == 20
        && DungeonRunEncounters.ToombaTreasures.Distinct().Count() == 20 && DungeonRunEncounters.ToombaTreasures.All(id => C(id).Implemented && Catalog(id, AdmitScope.Boss)),
        string.Join(",", DungeonRunEncounters.ToombaTreasures.Where(id => !C(id).Implemented || !Catalog(id, AdmitScope.Boss))));
    Check("G6 Gloves of Mugging and Aleatoric Cube stay the run player's only (hidden-zone rows)", !Catalog("LOOTA_834", AdmitScope.Boss) && !Catalog("LOOTA_820", AdmitScope.Boss)
        && Catalog("LOOTA_834", AdmitScope.Player) && Catalog("LOOTA_820", AdmitScope.Player));
});

// ============================================================ H  Tipsi Wobblerune: Portal Party, PARTY PORTAL!, the Mime
Guard("Tipsi", () =>
{
    var g = Fight("dala_38_tipsi");
    Controller boss = g.Player2, player = g.Player1;
    ToBoss(g);
    EmptyHand(boss);
    bool used = Power(g);
    Check("H1 Portal Party costs (2): one Portal joins her hand", Cost("DALA_BOSS_38p") == 2 && used && boss.HandZone.Count == 1 && DungeonRunEncounters.TipsiPortals.Contains(boss.HandZone[0].Card.Id) && boss.UsedMana == 2);
    var seen = new HashSet<string> { boss.HandZone[0].Card.Id };
    for (int i = 0; i < 40; i++) { Round(g); EmptyHand(boss); Power(g); seen.Add(boss.HandZone.FirstOrDefault()?.Card.Id ?? "none"); }
    Check("H2 forty-one uses: all seven Portals of the wiki, nothing else", seen.SetEquals(DungeonRunEncounters.TipsiPortals) && seen.Contains(Portal), string.Join(",", seen));
    FillHand(g, boss);
    Round(g);
    Check("H3 with a full hand it is not offered", !Offered(g));
    var g2 = Fight("dala_38_tipsi");
    boss = g2.Player2; player = g2.Player1;
    ToBoss(g2);
    var guests = new HashSet<string>();
    bool silent = true, sides = true;
    for (int i = 0; i < 30; i++)
    {
        ClearBoard(g2, boss);
        Cast(g2, Portal);
        var guest = boss.BoardZone.SingleOrDefault();
        if (guest == null) { sides = false; break; }
        guests.Add(guest.Card.Id);
        if (guest.Card.Id == Mime) silent &= guest.IsSilenced && guest.AttackDamage == 5 && guest.Health == 6;
        else silent &= !guest.IsSilenced;
    }
    Check("H4 PARTY PORTAL! (4) summons one Partygoer: thirty casts, all five of the wiki's list, nothing else", Cost(Portal) == 4 && sides && guests.SetEquals(DungeonRunEncounters.TipsiPartygoers),
        string.Join(",", guests));
    Check("H5 a Mime enters play silenced (its text: 'Always silenced.'); the others do not", silent && guests.Contains(Mime));
    ClearBoard(g2, boss);
    for (int i = 0; i < 7; i++) Board(g2, boss, Wisp);
    var portal = ToHand(g2, boss, Portal);
    Mana(boss);
    Check("H6 with a full board PARTY PORTAL! cannot be played", !boss.Options().OfType<PlayCardTask>().Any(o => o.Source == portal));
    ToPlayer(g2);
    ClearBoard(g2, player);
    Cast(g2, Portal);
    Check("H7 the run player's PARTY PORTAL! (taken from a boss) summons a Partygoer for the run player", player.BoardZone.Count == 1 && DungeonRunEncounters.TipsiPartygoers.Contains(player.BoardZone[0].Card.Id));
    var deck = data.BossDecks[data.Boss("dala_38_tipsi").Deck];
    Check("H8 PARTY PORTAL! is a card of her shipped deck; the five Partygoers are scripted and admitted for a boss", deck.Contains(Portal)
        && DungeonRunEncounters.TipsiPartygoers.All(id => C(id).Implemented && Catalog(id, AdmitScope.Boss)) && C(Mime).Implemented && DungeonRunEncounters.Find(Mime)?.Family == "16");
});

// ============================================================ I  Aki the Brilliant: For The Light! (auto-cast)
Guard("Aki", () =>
{
    var g = Fight("dala_39_aki");
    Controller boss = g.Player2, player = g.Player1;
    var power = boss.Hero.HeroPower;
    Check("I1 For The Light! (2) is cast by rule (a registered passive, started at match start), never an option of the boss", power.Card.Id == "DALA_BOSS_39p" && power.IsPassiveHeroPower
        && power[EncounterRules.PowerActive] == 1 && Cost("DALA_BOSS_39p") == 2);
    EmptyHand(boss);
    var spell = ToHand(g, boss, Moonfire);
    // The cast comes after her turn-start draw (I6, I7), so for I2 her deck holds spells only: the card she draws is no minion.
    EmptyDeck(boss);
    for (int i = 0; i < 3; i++) ToDeck(boss, Moonfire);
    BossTurnWith(g, 3);
    Check("I2 with no minion in her hand after her turn-start draw (she drew a spell) nothing is cast and no Mana is paid", Casts(boss) == 0 && boss.UsedMana == 0
        && boss.HandZone.All(p => !(p is Minion)));
    EmptyDeck(boss);
    for (int i = 0; i < 6; i++) ToDeck(boss, Yeti);
    EmptyHand(boss);
    var m1 = ToHand(g, boss, Yeti); var m2 = ToHand(g, boss, Croc); var s1 = ToHand(g, boss, Moonfire);
    var theirs = ToHand(g, player, Yeti);
    BossTurnWith(g, 1);
    Check("I3 with (1) Mana nothing is cast", Casts(boss) == 0 && !Has(m1, Light));
    var handBefore = boss.HandZone.ToList();
    BossTurnWith(g, 3);
    var drawn = boss.HandZone.Where(p => !handBefore.Contains(p)).ToList();
    Check("I4 at the start of her turn with (2) Mana and minions in hand: the Mana is paid and each minion then in her hand gets Light-imbued (+1/+1)", Casts(boss) == 1 && boss.UsedMana == 2
        && Has(m1, Light) && Has(m2, Light) && ((Minion)m1).AttackDamage == 5 && ((Minion)m1).Health == 6 && ((Minion)m2).AttackDamage == 3 && !Has(s1, Light), "casts " + Casts(boss) + " used " + boss.UsedMana);
    Check("I5 the run player's hand is untouched", !Has(theirs, Light));
    // Wave 5 integration: auto-cast powers are used after the turn-start draw (the wiki: "the boss's first action each turn",
    // after the start-of-turn triggered effects; EncounterRules.AfterTurnStartDraw), so the card she draws that turn counts.
    Check("I6 the minion she drew at the start of that turn is Light-imbued too (the cast comes after the turn-start draw)", drawn.Count == 1 && drawn[0] is Minion
        && Has(drawn[0], Light) && ((Minion)drawn[0]).AttackDamage == 5, string.Join(",", drawn.Select(p => p.Card.Id + (Has(p, Light) ? "+light" : ""))));
    EmptyHand(boss);
    var onlySpell = ToHand(g, boss, Moonfire);
    int before = Casts(boss);
    BossTurnWith(g, 3);
    var drew = boss.HandZone.Where(p => p != onlySpell).ToList();
    Check("I7 a hand of spells only plus the minion she draws: the power is still cast, the drawn minion is Light-imbued and the spell is not", Casts(boss) == before + 1
        && boss.UsedMana == 2 && drew.Count == 1 && drew[0] is Minion && Has(drew[0], Light) && !Has(onlySpell, Light), "casts " + (Casts(boss) - before) + " drew " + string.Join(",", drew.Select(p => p.Card.Id)));
});

// ============================================================ J  Oxana Demonslay: Immolation Aura
Guard("Oxana", () =>
{
    var g = Fight("dala_40_oxana");
    Controller boss = g.Player2, player = g.Player1;
    Check("J1 Immolation Aura is a passive power, never offered, started at match start", boss.Hero.HeroPower.Card.Id == "DALA_BOSS_40p" && boss.Hero.HeroPower.IsPassiveHeroPower
        && boss.Hero.HeroPower[EncounterRules.PowerActive] == 1);
    var yeti = Board(g, player, Yeti);
    int health = boss.Hero.Health;
    Attack(g, yeti, boss.Hero);
    Check("J2 a Yeti that attacks her hero deals its 4 and then takes 2 from the power", boss.Hero.Health == health - 4 && yeti.Damage == 2, "yeti damage " + yeti.Damage);
    var raptor = Board(g, player, Raptor);
    Attack(g, raptor, boss.Hero);
    Check("J3 a 3/2 that attacks her hero dies to it", raptor.IsDead || raptor.Zone != player.BoardZone);
    var croc = Board(g, boss, Croc);
    var wisp = Board(g, player, Yeti);
    Attack(g, wisp, croc);
    Check("J4 an attack on one of her minions is not answered", wisp.Damage == 2 && croc.IsDead);
    ToBoss(g);
    var own = Board(g, boss, Yeti);
    int ph = player.Hero.Health;
    Attack(g, own, player.Hero);
    Check("J5 her own minion attacking the run player's hero takes nothing", own.Damage == 0 && player.Hero.Health == ph - 4);
});

// ============================================================ K  Ungan Oddkind: Summon Companion
Guard("Ungan", () =>
{
    var g = Fight("dala_41_ungan");
    Controller boss = g.Player2;
    ToBoss(g);
    bool used = Power(g);
    Check("K1 Summon Companion costs (4): one of Huffer, Leokk, Misha joins his board", Cost("DALA_BOSS_41p") == 4 && used && boss.BoardZone.Count == 1
        && DungeonRunEncounters.UnganCompanions.Contains(boss.BoardZone[0].Card.Id) && boss.UsedMana == 4);
    var seen = new HashSet<string> { boss.BoardZone[0].Card.Id };
    for (int i = 0; i < 20; i++) { Round(g); ClearBoard(g, boss); Power(g); seen.Add(boss.BoardZone.FirstOrDefault()?.Card.Id ?? "none"); }
    Check("K2 twenty-one uses: all three companions, nothing else", seen.SetEquals(DungeonRunEncounters.UnganCompanions), string.Join(",", seen));
    ClearBoard(g, boss);
    for (int i = 0; i < 7; i++) Board(g, boss, Wisp);
    Round(g);
    Check("K3 with a full board it is not offered", !Offered(g));
    ClearBoard(g, boss);
    Generic.SummonBlock(g, (Minion)Entity.FromCard(boss, C("NEW1_034")), -1, boss.Hero);
    Check("K4 Huffer has Charge: it can attack at once", boss.Options().OfType<MinionAttackTask>().Any());
});

// ============================================================ L  P.O.G.O.: Pogoshuffle
Guard("Pogo", () =>
{
    var g = Fight("dala_42_pogo");
    Controller boss = g.Player2, player = g.Player1;
    ToBoss(g);
    Check("L1 with no minion on a board Pogoshuffle is not offered", !Offered(g));
    var croc = Board(g, boss, Croc);
    Generic.AddEnchantmentBlock(g, C("DALA_BOSS_44e"), boss.Hero.HeroPower, croc);
    var yeti = Board(g, player, Raptor);
    var targets = PowerTargets(g);
    Check("L2 Pogoshuffle costs (2) and can be aimed at any minion of either board", Cost("DALA_BOSS_42p") == 2 && targets.Count == 2 && targets.Contains(croc) && targets.Contains(yeti));
    var before = boss.DeckZone.ToList();
    Power(g, croc);
    var added = boss.DeckZone.Where(p => !before.Contains(p)).ToList();
    Check("L3 three new copies of its card join his deck: the printed card, without its enchantment; the board minion stays", added.Count == 3 && added.All(p => p.Card.Id == Croc && p.Controller == boss
        && (p.AppliedEnchantments == null || p.AppliedEnchantments.Count == 0)) && croc.Zone == boss.BoardZone && boss.UsedMana == 2);
    Round(g);
    Power(g, yeti);
    var raptors = boss.DeckZone.Where(p => p.Card.Id == Raptor).ToList();
    Check("L4 aimed at the run player's minion: three copies of that card join the boss's deck, the boss's own", raptors.Count == 3 && raptors.All(p => p.Controller == boss) && yeti.Controller == player);
    var sample = Ledger(g, boss);
    Check("L5 the copies are shown as they are shuffled: his own-deck ledger holds them (the sample equals his deck)", SameCounts(sample, Multiset(boss)) && sample[Croc] == 3 && sample[Raptor] == 3,
        Show(sample));
});

// ============================================================ M  Erekem: Dark Tidings
Guard("Erekem", () =>
{
    var mixed = Enumerable.Repeat(Moonfire, 20).Concat(Enumerable.Repeat(Croc, 5)).Concat(Enumerable.Repeat(Yeti, 5)).ToList();
    var g = FightWith("dala_44_erekem", mixed);
    Controller boss = g.Player2, player = g.Player1;
    ToBoss(g);
    int deck = boss.DeckZone.Count;
    var minions = boss.DeckZone.Where(p => p is Minion).ToList();
    int residences = g.HistoryFacts.Query.Events<ResidenceChangedFact>().Count();
    var ledgerBefore = Ledger(g, boss);
    bool used = Power(g);
    var top = boss.DeckZone.TopCard;
    Check("M1 Dark Tidings costs (1): a minion of his deck gets Forseen (+2/+2) and is on top of the deck; the deck keeps its size", Cost("DALA_BOSS_44p") == 1 && used && top is Minion
        && minions.Contains(top) && Has(top, Forseen) && boss.DeckZone.Count == deck && boss.UsedMana == 1, top?.Card.Id);
    Check("M2 the move is a reorder inside the deck: no zone change is recorded", g.HistoryFacts.Query.Events<ResidenceChangedFact>().Count() == residences);
    var fact = g.RunDeckFacts.LastOrDefault();
    Check("M3 the top card is recorded for the own-deck ledger (a TopKnown fact of the boss naming it)", fact != null && fact.Kind == RunDeckFactKind.TopKnown && fact.ControllerId == boss.Id
        && fact.Definitions.SequenceEqual(new[] { top.Card.Id }));
    var ledgerAfter = Ledger(g, boss);
    Check("M4 his own-deck ledger keeps the deck's exact multiset (nothing gained or lost by the reveal)", SameCounts(ledgerAfter, Multiset(boss)) && SameCounts(ledgerBefore, ledgerAfter), Show(ledgerAfter));
    EndTurn(g);
    var drawnBefore = boss.HandZone.ToList();
    EndTurn(g);
    var drawn = boss.HandZone.Where(p => !drawnBefore.Contains(p)).ToList();
    Check("M5 his next draw is that card, with its +2/+2", drawn.Count == 1 && drawn[0] == top && Has(drawn[0], Forseen) && ((Minion)drawn[0]).AttackDamage == top.Card.ATK + 2);
    var g2 = FightWith("dala_44_erekem", Enumerable.Repeat(Moonfire, 30));
    boss = g2.Player2;
    ToBoss(g2);
    var order = boss.DeckZone.Select(p => p.Id).ToList();
    bool used2 = Power(g2);
    Check("M6 a deck without minions: offered (its count is public), nothing changes", used2 && boss.DeckZone.Select(p => p.Id).SequenceEqual(order) && boss.DeckZone.All(p => !Has(p, Forseen))
        && !g2.RunDeckFacts.Any());
    EmptyDeck(boss);
    Round(g2);
    Check("M7 with an empty deck it is not offered", !Offered(g2));
});

// ============================================================ N  Moragg: Ray of Suffering
Guard("Moragg", () =>
{
    var g = Fight("dala_45_moragg");
    Controller boss = g.Player2, player = g.Player1;
    ToBoss(g);
    var power = boss.Hero.HeroPower;
    int h = player.Hero.Health;
    Check("N1 Ray of Suffering costs (3), clickable, its number (TAG_SCRIPT_DATA_NUM_1) starts at 1", Cost("DALA_BOSS_45p") == 3 && !power.IsPassiveHeroPower && power[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1 && Offered(g));
    Power(g);
    Check("N2 the first use deals 1 to the run player's hero; the number becomes 2", player.Hero.Health == h - 1 && power[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2 && boss.UsedMana == 3);
    Round(g); Power(g); Round(g); Power(g);
    Check("N3 the next uses deal 2 and 3 (6 in all); the number is 4", player.Hero.Health == h - 6 && power[GameTag.TAG_SCRIPT_DATA_NUM_1] == 4, (h - player.Hero.Health).ToString());
    var (view, viewBoss, _) = FairView(g);
    Check("N4 the boss's fair view holds the number on the power (a public tag)", viewBoss.Hero.HeroPower[GameTag.TAG_SCRIPT_DATA_NUM_1] == 4);
    player.Hero.Armor = 2;
    int armor = player.Hero.Armor; h = player.Hero.Health;
    Round(g); Power(g);
    Check("N5 Armor absorbs it as any damage (4: 2 Armor, 2 Health)", player.Hero.Armor == 0 && player.Hero.Health == h - 2);
});

// ============================================================ T  the treasures newly admitted for a boss (played by Ol' Toomba)
Guard("Treasures", () =>
{
    var g = Fight("dala_37_toomba");
    Controller boss = g.Player2, player = g.Player1;
    ToBoss(g);
    for (int i = 0; i < 4; i++) ToHand(g, player, Yeti);
    player.BaseMana = 6;
    int hand = player.HandZone.Count;
    Cast(g, Orb);
    Check("T1 Orb of Destruction cast by the boss: the run player loses 2 Mana Crystals and discards 2 cards", player.BaseMana == 4 && player.HandZone.Count == hand - 2);
    EmptyDeck(boss);
    for (int i = 0; i < 4; i++) ToDeck(boss, Croc);
    ToDeck(boss, Moonfire);
    ClearBoard(g, boss);
    Cast(g, Horn);
    Check("T2 Horn of Cenarius cast by the boss: three minions of his own deck join his board", boss.BoardZone.Count == 3 && boss.BoardZone.All(m => m.Card.Id == Croc) && boss.DeckZone.Count == 2);
    var plan = RowPlan("dala_37_toomba");
    var blocked = new HashSet<string>((plan.ExcludedGenerated ?? new string[0]).Concat(plan.ExcludedGeneratedBoss ?? new string[0]));
    Cast(g, Forge);
    var offers = boss.Choice?.Choices.Select(id => g.IdEntityDic[id].Card).ToList() ?? new List<Card>();
    Check("T3 Portable Forge cast by the boss: a Discover of three scripted Legendary weapons, none the boss's generation filter denies", offers.Count == 3 && offers.All(c => c.Type == CardType.WEAPON
        && c.Rarity == Rarity.LEGENDARY && c.Implemented && !blocked.Contains(c.Id)), string.Join(",", offers.Select(c => c.Id)));
    var pick = offers.FirstOrDefault();
    if (boss.Choice != null) g.Process(ChooseTask.Pick(boss, boss.Choice.Choices[0]));
    Check("T4 the chosen weapon is equipped", pick != null && boss.Hero.Weapon?.Card.Id == pick.Id);
    EmptyHand(boss);
    ToHand(g, boss, Blade);
    ToHand(g, boss, Hilt);
    var quel = boss.HandZone.FirstOrDefault(p => p.Card.Id == QuelDelar);
    Check("T5 the Blade and the Hilt of Quel'Delar in the boss's hand combine into Quel'Delar", quel != null && !boss.HandZone.Any(p => p.Card.Id == Blade || p.Card.Id == Hilt),
        string.Join(",", boss.HandZone.Select(p => p.Card.Id)));
    var enemy = Board(g, player, Yeti);
    Mana(boss);
    Play(g, quel);
    int ph = player.Hero.Health;
    bool attacked = g.Process(HeroAttackTask.Any(boss, player.Hero));
    Check("T6 Quel'Delar equipped by the boss: after its hero attacks, 6 damage to all enemies", attacked && enemy.Damage >= 5 && player.Hero.Health < ph - 6, "health " + ph + ">" + player.Hero.Health);
    string[] five = { Orb, Horn, Forge, QuelDelar, Blade };
    Check("T7 the five are admitted for a boss on catalog runs only (their legacy verdict stays the run player's) by the run-treasure-boss-shuffle row",
        five.All(id => Catalog(id, AdmitScope.Boss) && Catalog(id, AdmitScope.Player) && !Legacy(id, AdmitScope.Boss) && Legacy(id, AdmitScope.Player)
            && DungeonRunAdmission.RuleFor(id)?.Family == "run-treasure-boss-shuffle"));
    Check("T8 the core run-treasure row keeps Wish and Wax Rager", DungeonRunAdmission.RuleFor("LOOTA_814")?.Family == "run-treasure" && DungeonRunAdmission.RuleFor("LOOTA_840")?.Family == "run-treasure");
});

// ============================================================ P  the rows, the admission rows, the validator
Guard("Rows", () =>
{
    var errors = DungeonRunValidator.Validate(data, null);
    Check("P1 the shipped data validates with the fourteen family 16 rows", errors.Count == 0, string.Join(" | ", errors.Take(4)));
    var rows = new (string id, string n, string name, string tier, int level, string kind, string[] flags, bool passive)[]
    {
        ("dala_31_timothy", "31", "Timothy Jones", "2", 4, "extended", new string[0], false),
        ("dala_32_kizi", "32", "Kizi Copperclip", "1", 1, "extended", new[] { "passive" }, true),
        ("dala_33_gold_elemental", "33", "Gold Elemental", "2", 4, "extended", new[] { "passive" }, true),
        ("dala_34_carousel", "34", "Carousel Gryphon", "2", 3, "extended", new[] { "passive", "bothPlayers", "position" }, true),
        ("dala_35_zoie", "35", "Soothsayer Zoie", "2", 3, "extended", new[] { "passive" }, true),
        ("dala_36_draemus", "36", "Draemus", "3", 6, "authentic", new string[0], false),
        ("dala_37_toomba", "37", "Ol' Toomba", "3", 6, "authentic", new string[0], false),
        ("dala_38_tipsi", "38", "Tipsi Wobblerune", "2", 4, "extended", new string[0], false),
        ("dala_39_aki", "39", "Aki the Brilliant", "3", 6, "authentic", new[] { "passive" }, true),
        ("dala_40_oxana", "40", "Oxana Demonslay", "3", 6, "authentic", new[] { "passive" }, true),
        ("dala_41_ungan", "41", "Ungan Oddkind", "3", 6, "authentic", new string[0], false),
        ("dala_42_pogo", "42", "P.O.G.O.", "3", 6, "substituted", new string[0], false),
        ("dala_44_erekem", "44", "Erekem", "3", 6, "authentic", new[] { "hiddenZone" }, false),
        ("dala_45_moragg", "45", "Moragg", "4", 8, "authentic", new string[0], false),
    };
    Check("P2 family 16 holds exactly these fourteen rows", data.Encounters.Where(b => b.Family == "16").Select(b => b.Id).OrderBy(i => i, StringComparer.Ordinal)
        .SequenceEqual(rows.Select(r => r.id).OrderBy(i => i, StringComparer.Ordinal)), string.Join(",", data.Encounters.Where(b => b.Family == "16").Select(b => b.Id)));
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
            && b.Forms.Count == 0 && b.Start == null,
            b.Source.Kind + " health " + plan.BossHealth + " flags " + string.Join(",", b.Flags) + " tier " + b.Tier + " level " + b.LevelOriginal);
    }
    string[] powers = rows.Select(r => "DALA_BOSS_" + r.n + "p").ToArray();
    string[] cards = { Portal, Pet, Mime };
    string[] enchantments = { Bling, "DALA_BOSS_32e1", "DALA_BOSS_32e2", "DALA_BOSS_32e3", "DALA_BOSS_32e4", Light, Forseen };
    var scripted = powers.Concat(cards).ToArray();
    var unscripted = scripted.Concat(enchantments).Where(id => !C(id).Implemented || DungeonRunEncounters.Find(id)?.Family != "16").ToList();
    var retexted = scripted.Concat(enchantments).Where(id => factById.ContainsKey(id) && factById[id].TryGetProperty("text", out var tx)
        && C(id).Text.Replace(" @spelldmg", "") != tx.GetString()).ToList();
    Check("P4 every power, boss card and enchantment of the family is scripted by family 16 and keeps the client's text (14 powers, 3 cards, 7 enchantments)", unscripted.Count == 0 && retexted.Count == 0
        && powers.Length == 14, string.Join(",", unscripted.Concat(retexted.Select(id => id + " [" + C(id).Text + "]"))));
    var heroes = rows.Select(r => "DALA_BOSS_" + r.n + "h").ToList();
    Check("P4b the fourteen boss heroes need no script (portraits: their text is the encounter description)", heroes.All(id => DungeonRunEncounters.Find(id) == null && C(id).Type == CardType.HERO));
    var wrongScope = scripted.Where(id =>
    {
        var v = DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss, null, catalogRun: true);
        return !(v.Admitted && v.Subject && v.Rule?.Ids != null && v.Rule.Ids.Contains(id) && DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss).Admitted
            && DungeonRunAdmission.Classify(C(id), id, AdmitScope.Player, null, catalogRun: true).Admitted == cards.Contains(id)
            && !DungeonRunAdmission.Classify(C(id), id, AdmitScope.Generated, null, catalogRun: true).Admitted
            && DungeonRunAdmission.GeneratedRejects(true).Contains(id) && DungeonRunAdmission.GeneratedRejects(false).Contains(id));
    }).ToList();
    Check("P5 each has its own admission row: the fourteen powers a boss's only, the three boss cards a boss's and (taken from a boss) the run player's; none is ever generated", wrongScope.Count == 0, string.Join(",", wrongScope));
    var families = scripted.Select(id => DungeonRunAdmission.RuleFor(id)?.Family).Distinct().OrderBy(f => f, StringComparer.Ordinal).ToList();
    int In(string family) => DungeonRunAdmission.Rules.Where(r => r.Family == family).Sum(r => r.Ids.Length);
    Check("P6 the seven admission families of the family: heist-c-board-power (3), heist-c-auto-cast (2), heist-c-passive (4), heist-c-craft (1), heist-c-own-deck-power (3), heist-c-own-hand-power (1), heist-c-boss-card (3)",
        families.SequenceEqual(new[] { "heist-c-auto-cast", "heist-c-board-power", "heist-c-boss-card", "heist-c-craft", "heist-c-own-deck-power", "heist-c-own-hand-power", "heist-c-passive" })
        && In("heist-c-board-power") == 3 && In("heist-c-auto-cast") == 2 && In("heist-c-passive") == 4 && In("heist-c-craft") == 1 && In("heist-c-own-deck-power") == 3
        && In("heist-c-own-hand-power") == 1 && In("heist-c-boss-card") == 3, string.Join(",", families));
    string Subs(string id) => string.Join(",", data.Boss(id).Source.Substitutions.Where(x => x.Listed != null).Select(x => x.Listed + ">" + (x.Used ?? "-") + ":" + x.Reason).Distinct().OrderBy(x => x, StringComparer.Ordinal));
    Check("P7 the listed cards that are replaced or left out are only cards the engine does not script or a boss deck does not admit, each recorded",
        Subs("dala_31_timothy") == "KAR_096>-:admission:unclassified,PRO_001>-:unscripted" && Subs("dala_34_carousel") == "GIL_130>-:admission:unclassified"
        && Subs("dala_42_pogo") == "GVG_016>GVG_020:unscripted"
        && rows.Where(r => !new[] { "dala_31_timothy", "dala_34_carousel", "dala_42_pogo" }.Contains(r.id)).All(r => Subs(r.id) == "")
        && rows.SelectMany(r => data.Boss(r.id).Source.Substitutions).Where(x => x.Reason == "unscripted").All(x => !C(x.Listed).Implemented),
        Subs("dala_31_timothy") + " | " + Subs("dala_34_carousel") + " | " + Subs("dala_42_pogo"));
    int Deck(string boss, string card) => data.BossDecks[data.Boss(boss).Deck].Count(id => id == card);
    Check("P8 the quantity lists are the wiki's: P.O.G.O. holds four Pogo-Hoppers and two Fel Cannons, Moragg one DOOM!, Draemus one Oondasta; Tipsi's pool holds PARTY PORTAL!; the Carousel's Hyena is EX1_534t",
        Deck("dala_42_pogo", "BOT_283") == 4 && Deck("dala_42_pogo", "GVG_020") == 2 && Deck("dala_45_moragg", "OG_239") == 1 && Deck("dala_36_draemus", "TRL_542") == 1
        && Deck("dala_38_tipsi", Portal) >= 1 && Deck("dala_34_carousel", "EX1_534t") >= 1);
    var playerPower = DungeonRunAdmission.Classify(C("DALA_BOSS_45p"), "DALA_BOSS_45p", AdmitScope.Player, null, catalogRun: true);
    Check("P9 a family 16 power asked for the run player is refused with its row's reason", !playerPower.Admitted && playerPower.Family == "heist-c-board-power" && playerPower.Lift == DungeonRunAdmission.NoneBossPower);
    Check("P10 the tier pools: one row in enc-t1, five in enc-t2, seven in enc-t3, one in enc-t4", rows.Count(r => r.tier == "1") == 1 && rows.Count(r => r.tier == "2") == 5 && rows.Count(r => r.tier == "3") == 7
        && rows.Count(r => r.tier == "4") == 1 && rows.All(r => data.Boss(r.id).Pools.SequenceEqual(new[] { "enc-t" + r.tier })));
    Check("P11 the extra cards each row names are its 'cards': the haircuts, Blinged Out!, the Coin, the Exotic Pet, the twenty treasures, the Portals and Partygoers, the companions, Light-imbued, Forseen",
        DungeonRunEncounters.KiziHaircuts.All(id => data.Boss("dala_32_kizi").Cards.Contains(id)) && data.Boss("dala_31_timothy").Cards.Contains(Bling) && data.Boss("dala_33_gold_elemental").Cards.Contains(Coin)
        && data.Boss("dala_36_draemus").Cards.Contains(Pet) && data.Boss("dala_37_toomba").Cards.SequenceEqual(DungeonRunEncounters.ToombaTreasures)
        && DungeonRunEncounters.TipsiPortals.Where(id => id != Portal).Concat(DungeonRunEncounters.TipsiPartygoers).All(id => data.Boss("dala_38_tipsi").Cards.Contains(id))
        && DungeonRunEncounters.UnganCompanions.All(id => data.Boss("dala_41_ungan").Cards.Contains(id)) && data.Boss("dala_39_aki").Cards.Contains(Light) && data.Boss("dala_44_erekem").Cards.Contains(Forseen));
    DungeonRunData Changed(Action<DungeonRunData> change) { var d = Load(); change(d); return d; }
    var badCard = DungeonRunValidator.Validate(Changed(d => d.Boss("dala_37_toomba").Cards.Add("LOOTA_834")), null);
    Check("P12 the validator checks a row's cards: Gloves of Mugging (not admitted for a boss) in Toomba's cards is refused", badCard.Count > 0 && badCard.Any(e => e.Contains("LOOTA_834")), string.Join(" | ", badCard.Take(2)));
    Check("P13 the reserved model tag of the family is unused: no family 16 state is a private tag (Golden and Ray of Suffering's number are public tags)",
        !typeof(EncounterRules).GetFields().Any(f => f.FieldType == typeof(GameTag) && (int)(GameTag)f.GetValue(null) == 32651));
});

Console.WriteLine($"SearchTelemetry swallowed leaf exceptions {SearchTelemetry.SwallowedLeafErrors}");
Console.WriteLine($"RESULT {passes} passed, {failures + SearchTelemetry.SwallowedLeafErrors} failed");
return failures + SearchTelemetry.SwallowedLeafErrors;
