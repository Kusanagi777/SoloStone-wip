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

// Phase 8 family 14, Dalaran Heist bosses A (real engine games through the run setup path: DungeonRun.BuildPlan on the shipped
// row -> DungeonRunSetup.Configure / ApplyAfterStart): Chomper (Chomp), Awilo (The Feast), The Great Akazamzarak
// (Prestidigitation and the four magical feats), Kaye Toogie (Open Wormhole), The Amazing "Bonepaw" (Mana Echoes), Dazzik
// "Hellscream" ("Armor Up", "Scourgelord" Dazzik), Flight Master Belnaara (Take Flight! / Flying!, a power-only script form),
// Applebough (Apple Toss), Archivist Oshi (Repeat History), Mo Eniwhiskers (Street Smarts), Noz Timbertail (Backstabber),
// Cravitz Lorent (Forbidden Love), Albin Eastoft (Slice and Dice), Tierra Blythe (Order Up!, the "Kind" Waitress) and Moon
// Priestess Nici (Blessing of Elune), each with the shrine rules that touch it, and the rows, admission rows and validator.
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
const string Feat1 = "DALA_BOSS_03t", Feat2 = "DALA_BOSS_03t2", Feat3 = "DALA_BOSS_03t3", Feat4 = "DALA_BOSS_03t4", Scroll = "LOOT_106t";
const string Dazzik = "DALA_BOSS_06dk", Waitress = "DALA_BOSS_14t", TakeFlight = "DALA_BOSS_07p", Flying = "DALA_BOSS_07p2", FlyingE = "DALA_BOSS_07e";
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
// The golden pilot's own view (its own Secrets kept live, the run player's removed): the one that holds a boss's Secret zone.
bool PilotViewOffered(Game g)
{
    var options = new FairOptions { RemoveOpponentSecrets = true, TrackOwnDeck = true, MaterializeOwnDeckTriggers = true };
    var view = FairBotState.Create(g, g.Player2.PlayerId, 1, DeckStyle.Tempo, options).FingerprintGame;
    var viewBoss = view.Player1.PlayerId == g.Player2.PlayerId ? view.Player1 : view.Player2;
    return viewBoss.Options().Any(o => o is HeroPowerTask);
}

// ============================================================ A  Chomper: Chomp
Guard("Chomper", () =>
{
    var g = Fight("dala_01_chomper");
    Controller boss = g.Player2, player = g.Player1;
    var wisp = Board(g, player, Wisp); var yeti = Board(g, player, Yeti);
    ToBoss(g);
    var own = Board(g, boss, Croc);
    var targets = PowerTargets(g);
    Check("A1 Chomp costs (2), is clickable and can be aimed at any minion, never a hero", Cost("DALA_BOSS_01p") == 2 && !boss.Hero.HeroPower.IsPassiveHeroPower
        && targets.Count == 3 && targets.All(t => t is Minion) && targets.Contains(own), string.Join(",", targets.Select(t => t.Card.Id)));
    Hit(g, boss.Hero, 5, player.Hero);
    int health = boss.Hero.Health;
    bool used = Power(g, yeti);
    Check("A2 it deals 1 damage and its Lifesteal (the power card's own) heals the boss's hero by the damage dealt", used && yeti.Damage == 1 && boss.Hero.Health == health + 1
        && boss.Hero.HeroPower.HasLifeSteal && boss.UsedMana == 2, "health " + health + ">" + boss.Hero.Health);
    Round(g);
    health = boss.Hero.Health;
    Power(g, wisp);
    Check("A3 a 1-Health minion dies to it; Lifesteal heals 1", wisp.IsDead && boss.Hero.Health == health + 1, "health " + boss.Hero.Health);
});

// ============================================================ B  Awilo: The Feast
Guard("Awilo", () =>
{
    var g = Fight("dala_02_awilo");
    Controller boss = g.Player2, player = g.Player1;
    var feast = boss.Hero.HeroPower;
    Check("B1 The Feast is a passive power, never offered, started at match start", feast.Card.Id == "DALA_BOSS_02p" && feast.IsPassiveHeroPower && feast[EncounterRules.PowerActive] == 1
        && DungeonRunEncounters.PassivePowers.Contains("DALA_BOSS_02p"));
    Hit(g, boss.Hero, 10, player.Hero);
    int before = boss.Hero.Health;
    Cast(g, Moonfire, boss.Hero);
    Check("B2 the run player's spell does not heal the boss", boss.Hero.Health == before - 1, before + ">" + boss.Hero.Health);
    ToBoss(g);
    before = boss.Hero.Health;
    bool offered = Offered(g);
    Cast(g, Moonfire, player.Hero);
    Check("B3 after the boss casts a spell its hero is restored 3 Health", !offered && boss.Hero.Health == before + 3, before + ">" + boss.Hero.Health);
    boss.Hero.Damage = 1;
    Cast(g, Moonfire, player.Hero);
    Check("B4 a heal never passes the maximum Health", boss.Hero.Damage == 0 && boss.Hero.Health == boss.Hero.BaseHealth);
});

// ============================================================ C  The Great Akazamzarak: Prestidigitation and the feats
Guard("Akazamzarak", () =>
{
    var g = Fight("dala_03_akazamzarak");
    Controller boss = g.Player2, player = g.Player1;
    ToBoss(g);
    EmptyHand(boss);
    bool used = Power(g);
    var feat = boss.HandZone.FirstOrDefault();
    Check("C1 Prestidigitation costs (1): one magical feat joins the boss's hand", Cost("DALA_BOSS_03p") == 1 && used && boss.HandZone.Count == 1 && DungeonRunEncounters.MagicalFeats.Contains(feat?.Card.Id)
        && boss.UsedMana == 1, feat?.Card.Id);
    var seen = new HashSet<string> { feat.Card.Id };
    for (int i = 0; i < 24; i++) { Round(g); EmptyHand(boss); Power(g); seen.Add(boss.HandZone.FirstOrDefault()?.Card.Id ?? "none"); }
    Check("C2 25 uses: all four feats, nothing else", seen.SetEquals(DungeonRunEncounters.MagicalFeats), string.Join(",", seen));
    Round(g);
    FillHand(g, boss);
    Check("C3 with a full hand it is not offered", !Offered(g));
    // Reductomara.
    var g2 = Fight("dala_03_akazamzarak", bossDeck: Fireball, playerDeck: Intellect);
    boss = g2.Player2; player = g2.Player1;
    ToBoss(g2);
    EmptyHand(boss); EmptyHand(player);
    var bossFire = ToHand(g2, boss, Fireball); var bossYeti = ToHand(g2, boss, Yeti); var bossEcho = ToHand(g2, boss, CheapShot);
    var playerFire = ToHand(g2, player, Fireball);
    ToDeck(boss, Yeti);
    int facts0 = g2.RunDeckFacts.Count;
    bool cast = Cast(g2, Feat1);
    Check("C4 Reductomara costs (1): every spell of both hands costs (1) less (Reductomara'd); minions keep their Cost", Cost(Feat1) == 1 && cast && bossFire.Cost == 3 && playerFire.Cost == 3
        && bossYeti.Cost == 4 && Has(bossFire, DungeonRunEncounters.Reductomarad) && Has(playerFire, DungeonRunEncounters.Reductomarad) && !Has(bossYeti, DungeonRunEncounters.Reductomarad),
        bossFire.Cost + "/" + playerFire.Cost + "/" + bossYeti.Cost);
    Check("C5 and every spell of both decks (Fireball 3, Arcane Intellect 2), never a minion", boss.DeckZone.Where(p => p is Spell).All(p => p.Cost == 3) && player.DeckZone.All(p => p.Cost == 2)
        && boss.DeckZone.Where(p => p is Minion).All(p => p.Cost == 4 && !Has(p, DungeonRunEncounters.Reductomarad)) && boss.DeckZone.Any(p => p is Minion));
    var rewrites = g2.RunDeckFacts.Skip(facts0).ToList();
    Check("C6 each owner's deck is recorded as rewritten (its own-deck ledger restarts from its list)", rewrites.Count == 2 && rewrites.All(f => f.Kind == RunDeckFactKind.Rewritten)
        && rewrites.Any(f => f.ControllerId == boss.Id && f.Definitions.Count == boss.DeckZone.Count) && rewrites.Any(f => f.ControllerId == player.Id && f.Definitions.Count == player.DeckZone.Count));
    Check("C7 an Echo spell goes from (2) to (1), and a second Reductomara leaves it at (1) (it could repeat without end at (0)) while Fireball goes to (2)",
        bossEcho.Cost == 1 && Cast(g2, Feat1) && bossEcho.Cost == 1 && bossFire.Cost == 2, bossEcho.Cost + "/" + bossFire.Cost);
    // Bunnifitronus.
    var g3 = Fight("dala_03_akazamzarak", shrine: Shrine);
    boss = g3.Player2; player = g3.Player1;
    var shrine = ShrineRules.Of(player).First();
    Check("C8 Bunnifitronus costs (1); with only the run player's awake shrine on the boards it picks the shrine, which goes dormant", Cost(Feat2) == 1);
    ToBoss(g3);
    Cast(g3, Feat2);
    var form = ShrineRules.Of(player).FirstOrDefault();
    Check("C9 the shrine was destroyed instead of transformed and its dormant form holds its place", ShrineRules.IsCorpse(shrine) && form != null && form.IsDormant && form.Controller == player
        && !DungeonRunEncounters.Critters.Any(id => Count(player, id) > 0));
    var critters = new HashSet<string>(); int hits = 0, bothSides = 0;
    for (int i = 0; i < 16; i++)
    {
        var mine = Board(g3, boss, Yeti); var theirs = Board(g3, player, Croc);
        Cast(g3, Feat2);
        var born = boss.BoardZone.Concat(player.BoardZone).Where(m => DungeonRunEncounters.Critters.Contains(m.Card.Id)).ToList();
        if (born.Count == 1 && (mine.Zone != boss.BoardZone ^ theirs.Zone != player.BoardZone)) hits++;
        if (born.Count == 1) { critters.Add(born[0].Card.Id); bothSides |= born[0].Controller == boss ? 1 : 2; }
        foreach (var m in boss.BoardZone.ToArray()) Kill(g3, m);
        foreach (var m in player.BoardZone.Where(x => !x.IsDormant).ToArray()) Kill(g3, m);
    }
    Check("C10 sixteen casts on a Yeti and a Crocolisk: each turns exactly one of them into a critter, the dormant shrine never; Frog, Sheep and Squirrel on both sides",
        hits == 16 && critters.SetEquals(DungeonRunEncounters.Critters) && bothSides == 3 && ShrineRules.Of(player).FirstOrDefault() == form, hits + " " + string.Join(",", critters) + " sides " + bothSides);
    // Presto Legendaro.
    var g4 = Fight("dala_03_akazamzarak");
    boss = g4.Player2; player = g4.Player1;
    ToBoss(g4);
    EmptyHand(boss); EmptyHand(player);
    bool ok = true; var kinds = new HashSet<string>();
    for (int i = 0; i < 8; i++)
    {
        Cast(g4, Feat3);
        var a = boss.HandZone.Count == 1 ? boss.HandZone[0] : null; var b = player.HandZone.Count == 1 ? player.HandZone[0] : null;
        foreach (var x in new[] { a, b })
            if (x == null || x.Card.Type != CardType.MINION || x.Card.Rarity != Rarity.LEGENDARY || !x.Card.Collectible || !x.Card.Implemented) ok = false; else kinds.Add(x.Card.Id);
        EmptyHand(boss); EmptyHand(player);
    }
    Check("C11 Presto Legendaro costs (1): each player gets one scripted collectible Legendary minion, eight times, of several kinds", Cost(Feat3) == 1 && ok && kinds.Count >= 6, string.Join(",", kinds));
    // Yoggers Poggers.
    var g5 = Fight("dala_03_akazamzarak");
    boss = g5.Player2; player = g5.Player1;
    ToBoss(g5);
    int bossDeck = boss.DeckZone.Count, playerDeck = player.DeckZone.Count, facts1 = g5.RunDeckFacts.Count;
    Cast(g5, Feat4);
    var yogFacts = g5.RunDeckFacts.Skip(facts1).ToList();
    Check("C12 Yoggers Poggers costs (1): three Scrolls of Wonder in each deck, each owner's deck recorded with them", Cost(Feat4) == 1 && boss.DeckZone.Count == bossDeck + 3
        && player.DeckZone.Count == playerDeck + 3 && boss.DeckZone.Count(p => p.Card.Id == Scroll) == 3 && player.DeckZone.Count(p => p.Card.Id == Scroll) == 3
        && yogFacts.Count == 2 && yogFacts.All(f => f.Kind == RunDeckFactKind.Rewritten && f.Definitions.Count(d => d == Scroll) == 3));
    EmptyDeck(boss);
    Cast(g5, Feat4);
    EmptyDeck(player);
    int hand = boss.HandZone.Count;
    Generic.Draw(boss);
    g5.DeathProcessingAndAuraUpdate();
    Check("C13 a Scroll of Wonder drawn casts itself (Casts When Drawn): it is not kept in hand and leaves the deck", boss.DeckZone.Count(p => p.Card.Id == Scroll) <= 2
        && boss.HandZone.All(p => p.Card.Id != Scroll) && C(Scroll).Implemented, "hand " + hand + ">" + boss.HandZone.Count + " deck " + boss.DeckZone.Count);
    // A feat in the run player's hands.
    ToPlayer(g5);
    EmptyHand(boss);
    var bossSpell = ToHand(g5, boss, Fireball);
    var mineSpell = ToHand(g5, player, Fireball);
    Check("C14 a feat the run player holds works from that side too: its Reductomara cuts both players' spells", Cast(g5, Feat1) && bossSpell.Cost == 3 && mineSpell.Cost == 3);
});

// ============================================================ D  Kaye Toogie: Open Wormhole
Guard("Kaye", () =>
{
    var g = Fight("dala_04_kaye", bossDeck: Yeti, playerDeck: Raptor);
    Controller boss = g.Player2, player = g.Player1;
    ToBoss(g);
    int bossDeck = boss.DeckZone.Count, playerDeck = player.DeckZone.Count;
    bool used = Power(g);
    var mine = boss.BoardZone.FirstOrDefault();
    Check("D1 Open Wormhole costs (2): a minion of each deck is summoned and the boss's attacks the other (Yeti 4/5 kills Raptor 3/2, takes 3)", Cost("DALA_BOSS_04p") == 2 && used
        && boss.DeckZone.Count == bossDeck - 1 && player.DeckZone.Count == playerDeck - 1 && mine?.Card.Id == Yeti && mine.Damage == 3 && player.BoardZone.Count == 0
        && player.GraveyardZone.Any(p => p.Card.Id == Raptor), mine?.Card.Id + " dmg " + mine?.Damage);
    Round(g);
    EmptyDeck(player);
    int bossBoard = boss.BoardZone.Count;
    bool offered = Offered(g);
    Power(g);
    Check("D2 with the run player's deck empty only the boss's minion arrives and nothing fights; the power is offered anyway (it never reads a deck)", offered
        && boss.BoardZone.Count == bossBoard + 1 && boss.BoardZone.All(m => m.Damage == 0 || m == mine));
    var g2 = Fight("dala_04_kaye", bossDeck: Fireball, playerDeck: Raptor);
    boss = g2.Player2; player = g2.Player1;
    ToBoss(g2);
    // Before the power is used this turn: the real game and the boss's fair view must agree that it is offered.
    bool realOffered = Offered(g2);
    var (view, viewBoss, viewPlayer) = FairView(g2);
    bool viewOffered = viewBoss.Options().Any(o => o is HeroPowerTask);
    Power(g2);
    Check("D3 with no minion in the boss's deck only the run player's arrives (Duel!'s rule)", boss.BoardZone.Count == 0 && player.BoardZone.Count == 1 && player.BoardZone[0].Card.Id == Raptor);
    Check("D4 before it is used, the power is offered in the game and in the boss's fair view alike, where the run player's deck holds no known minion", realOffered && viewOffered
        && viewPlayer.DeckZone.All(p => !(p is Minion)), "real " + realOffered + " view " + viewOffered);
});

// ============================================================ E  The Amazing "Bonepaw": Mana Echoes
Guard("Bonepaw", () =>
{
    var g = Fight("dala_05_bonepaw");
    Controller boss = g.Player2, player = g.Player1;
    ToBoss(g);
    bool before = Offered(g);
    Check("E1 Mana Echoes costs (2) and is not offered before any spell was played this game, in the game and in the boss's fair view", Cost("DALA_BOSS_05p") == 2 && !before && !ViewOffered(g)
        && DungeonRunEncounters.ManaEchoes(boss).Count == 0);
    ToPlayer(g);
    Cast(g, Fireball, boss.Hero);
    ToBoss(g);
    int damage = boss.Hero.Damage + player.Hero.Damage, history = player.PlayHistory.Count + boss.PlayHistory.Count;
    bool offered = Offered(g), viewOffered = ViewOffered(g);
    bool used = Power(g);
    int dealt = boss.Hero.Damage + player.Hero.Damage - damage;
    Check("E2 after the run player played Fireball it is offered (also in the boss's fair view) and casts a new Fireball: 6 damage to a random target", offered && viewOffered && used
        && dealt == 6 && boss.UsedMana == 2, "dealt " + dealt);
    Check("E3 the echo is no play: the play history is unchanged", player.PlayHistory.Count + boss.PlayHistory.Count == history);
    Round(g);
    Cast(g, Counterspell);
    Check("E4 a Secret still in the boss's Secret zone is not echoed: its own Counterspell in play joins no echo list", DungeonRunEncounters.ManaEchoes(boss).All(card => card.Id == Fireball)
        && boss.SecretZone.Count == 1);
    var targets = new HashSet<string>();
    for (int i = 0; i < 12; i++)
    {
        Round(g);
        int bossBefore = boss.Hero.Damage, playerBefore = player.Hero.Damage;
        Power(g);
        if (boss.Hero.Damage > bossBefore) targets.Add("boss");
        if (player.Hero.Damage > playerBefore) targets.Add("player");
        boss.Hero.Damage = 0; player.Hero.Damage = 0;
    }
    Check("E5 targets are chosen randomly: twelve echoes hit both heroes", targets.SetEquals(new[] { "boss", "player" }), string.Join(",", targets));
    // Secrets: a boss's own Secret is echoed once it has left play; never while a copy is in play or the zone is full; a run
    // player's Secret is never (a boss-generated Secret outside a closed boss pool).
    const string IceBarrier = "EX1_289", Effigy = "AT_002", ExplosiveTrap = "EX1_610";
    var g3 = Fight("dala_05_bonepaw");
    boss = g3.Player2; player = g3.Player1;
    ToBoss(g3);
    Cast(g3, IceBarrier);
    Check("E6 the boss played Ice Barrier, which is in its Secret zone: no echo is possible (the power is not offered, in the game or the fair view)",
        boss.SecretZone.Count == 1 && boss.SecretZone[0].Card.Id == IceBarrier && DungeonRunEncounters.ManaEchoes(boss).Count == 0 && !Offered(g3) && !PilotViewOffered(g3));
    var gone = boss.SecretZone[0];
    gone.ActivatedTrigger?.Remove(); boss.SecretZone.Remove(gone); boss.GraveyardZone.Add(gone);
    bool secretOffered = Offered(g3), secretView = PilotViewOffered(g3);
    bool echoed = Power(g3);
    var again = boss.SecretZone.FirstOrDefault();
    Check("E7 once that Ice Barrier left play it is echoed: the power is offered (also in the fair view) and a new Ice Barrier enters the boss's Secret zone", secretOffered && secretView && echoed
        && boss.SecretZone.Count == 1 && again != null && again != gone && again.Card.Id == IceBarrier, again?.Card.Id);
    Round(g3);
    var other = new[] { "EX1_287", "EX1_294", "EX1_594" };
    foreach (string id in other) DungeonRunEncounters.PutSecret(boss, C(id));
    Cast(g3, Effigy);
    var effigy = boss.SecretZone.FirstOrDefault(x => x.Card.Id == Effigy);
    if (effigy != null) { effigy.ActivatedTrigger?.Remove(); boss.SecretZone.Remove(effigy); boss.GraveyardZone.Add(effigy); }
    DungeonRunEncounters.PutSecret(boss, C("FP1_018"));
    Check("E8 with a full Secret zone (five Secrets) no Secret is echoed, not even Effigy, which left play", boss.SecretZone.IsFull && DungeonRunEncounters.ManaEchoes(boss).Count == 0 && !Offered(g3),
        "zone " + boss.SecretZone.Count + " echoes " + string.Join(",", DungeonRunEncounters.ManaEchoes(boss).Select(x => x.Id)));
    var freed = boss.SecretZone.First(x => x.Card.Id == "FP1_018");
    freed.ActivatedTrigger?.Remove(); boss.SecretZone.Remove(freed); boss.GraveyardZone.Add(freed);
    Check("E9 with room again Effigy (played by the boss, out of play) is the one echo; Ice Barrier, whose echo is in play, is not",
        DungeonRunEncounters.ManaEchoes(boss).Select(x => x.Id).Distinct().OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(new[] { Effigy }), string.Join(",", DungeonRunEncounters.ManaEchoes(boss).Select(x => x.Id)));
    foreach (var x in boss.SecretZone.ToArray()) { x.ActivatedTrigger?.Remove(); boss.SecretZone.Remove(x); boss.GraveyardZone.Add(x); }
    ToPlayer(g3);
    Cast(g3, ExplosiveTrap);
    ToBoss(g3);
    Check("E10 a Secret the run player played (Explosive Trap) is never an echo for the boss", player.SecretZone.Any(x => x.Card.Id == ExplosiveTrap)
        && DungeonRunEncounters.ManaEchoes(boss).All(x => x.Id != ExplosiveTrap));
});

// ============================================================ F  Dazzik "Hellscream": "Armor Up", "Scourgelord" Dazzik
Guard("Dazzik", () =>
{
    var g = Fight("dala_06_dazzik");
    Controller boss = g.Player2, player = g.Player1;
    ToBoss(g);
    int armor = boss.Hero.Armor;
    Check("F1 \"Armor Up\" costs (2) and gains 2 Armor", Cost("DALA_BOSS_06p") == 2 && Power(g) && boss.Hero.Armor == armor + 2);
    Check("F2 the row's start block shuffled one \"Scourgelord\" Dazzik into his deck", boss.DeckZone.Count(p => p.Card.Id == Dazzik) == 1 && data.Boss("dala_06_dazzik").Start.BossDeckAdd.SequenceEqual(new[] { Dazzik }));
    Round(g);
    Hit(g, boss.Hero, 7, player.Hero);
    int health = boss.Hero.Health; armor = boss.Hero.Armor;
    var oldHero = boss.Hero;
    bool played = Cast(g, Dazzik);
    var weapon = boss.Hero.Weapon;
    Check("F3 played (8), it becomes the boss's hero with Bladestorm, equips a 4/3 Shadowmourne and gains 10 Armor; the Health left carries over", Cost(Dazzik) == 8 && played
        && boss.Hero != oldHero && boss.Hero.Card.Id == Dazzik && boss.Hero.HeroPower.Card.Id == "ICC_834h" && weapon?.Card.Id == "ICC_834w" && weapon.AttackDamage == 4 && weapon.Durability == 3
        && boss.Hero.Armor == armor + 10 && boss.Hero.Health == health, $"{boss.Hero.Card.Id} {boss.Hero.HeroPower.Card.Id} {weapon?.Card.Id} armor {boss.Hero.Armor} health {health}>{boss.Hero.Health}");
    Round(g);
    var mine = Board(g, boss, Yeti); var theirs = Board(g, player, Croc);
    Check("F4 Bladestorm deals 1 damage to all minions", Power(g) && mine.Damage == 1 && theirs.Damage == 1);
});

// ============================================================ G  Flight Master Belnaara: Take Flight! / Flying!
Guard("Belnaara", () =>
{
    var g = Fight("dala_07_belnaara");
    Controller boss = g.Player2, player = g.Player1;
    var take = boss.Hero.HeroPower;
    var bossRow = data.Boss("dala_07_belnaara");
    Check("G1 Take Flight! is a passive power, started, with 15 on its counter; the row has one power-only script form (Flying!)", take.Card.Id == TakeFlight && take.IsPassiveHeroPower
        && take[EncounterRules.PowerActive] == 1 && take[GameTag.TAG_SCRIPT_DATA_NUM_1] == 15 && bossRow.Forms.Count == 1 && bossRow.Forms[0].Power == Flying && bossRow.Forms[0].Hero == null
        && bossRow.Forms[0].Script && boss[EncounterRules.FormIndex] == 0);
    var hero = boss.Hero;
    Hit(g, hero, 10, player.Hero);
    Check("G2 10 damage: the counter shows 5, no flight yet", boss.Hero.HeroPower == take && take[GameTag.TAG_SCRIPT_DATA_NUM_1] == 5 && !hero.IsImmune);
    hero.Armor = 3;
    Hit(g, hero, 3, player.Hero);
    Check("G3 a hit the Armor absorbs is not counted (Health lost only)", take[GameTag.TAG_SCRIPT_DATA_NUM_1] == 5 && hero.Armor == 0);
    int health = hero.Health;
    Hit(g, hero, 6, player.Hero);
    var flying = boss.Hero.HeroPower;
    Check("G4 the 15th point of damage: she takes flight: power Flying! (passive, 2 on its counter, started), hero Immune, the same hero entity with its Health; form 1",
        boss.Hero == hero && flying.Card.Id == Flying && flying.IsPassiveHeroPower && flying[EncounterRules.PowerActive] == 1 && flying[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2 && hero.IsImmune
        && Has(hero, FlyingE) && hero.Health == health - 6 && boss[EncounterRules.FormIndex] == 1 && boss[EncounterRules.FormChanges] == 1 && take.Zone == boss.SetasideZone,
        $"{flying.Card.Id} immune {hero.IsImmune} health {hero.Health} form {boss[EncounterRules.FormIndex]}");
    health = hero.Health;
    Hit(g, hero, 8, player.Hero);
    Check("G5 while flying she takes no damage", hero.Health == health && flying[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2);
    ToBoss(g);
    Check("G6 at the start of her turn the counter falls to 1; still Immune", flying[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1 && hero.IsImmune && boss.Hero.HeroPower == flying && !Offered(g));
    ToPlayer(g);
    Check("G7 the run player's turn does not count", flying[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1 && hero.IsImmune);
    ToBoss(g);
    var landed = boss.Hero.HeroPower;
    Check("G8 at the start of her second turn she lands: Immune ends, Take Flight! returns with a fresh 15 (form 0, a second form change), the same hero, Health kept",
        landed.Card.Id == TakeFlight && landed != take && landed[GameTag.TAG_SCRIPT_DATA_NUM_1] == 15 && landed[EncounterRules.PowerActive] == 1 && !hero.IsImmune && !Has(hero, FlyingE)
        && boss.Hero == hero && hero.Health == health && boss[EncounterRules.FormIndex] == 0 && boss[EncounterRules.FormChanges] == 2,
        $"{landed.Card.Id} count {landed[GameTag.TAG_SCRIPT_DATA_NUM_1]} active {landed[EncounterRules.PowerActive]} immune {hero.IsImmune}/{Has(hero, FlyingE)} same {boss.Hero == hero} health {health}>{hero.Health} form {boss[EncounterRules.FormIndex]} changes {boss[EncounterRules.FormChanges]}");
    hero.Damage = 0;
    Hit(g, hero, 15, player.Hero);
    var end = EncounterRules.Harvest(g);
    Check("G9 she can take flight again (healed to full, then 15 damage); the harvest reads the form and the three changes", boss.Hero.HeroPower.Card.Id == Flying && hero.IsImmune && end.BossForm == 1
        && end.BossFormChanges == 3 && end.BossRemainingHealth == hero.Health,
        $"{boss.Hero.HeroPower.Card.Id} count {boss.Hero.HeroPower[GameTag.TAG_SCRIPT_DATA_NUM_1]} immune {hero.IsImmune} form {end.BossForm} changes {end.BossFormChanges} health {hero.Health} landed {landed[GameTag.TAG_SCRIPT_DATA_NUM_1]} trig {landed.ActivatedTrigger != null}");
    var (view, viewBoss, _) = FairView(g);
    Check("G10 the boss's fair view holds the same form, power, counter and Immune", viewBoss.Hero.HeroPower.Card.Id == Flying && viewBoss[EncounterRules.FormIndex] == 1
        && viewBoss.Hero.HeroPower[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2 && viewBoss.Hero.IsImmune);
    var g2 = Fight("dala_07_belnaara");
    boss = g2.Player2; player = g2.Player1;
    Hit(g2, boss.Hero, boss.Hero.Health, player.Hero);
    Check("G11 a hit that kills her does not take her into flight: she dies", boss.Hero.HeroPower.Card.Id == TakeFlight && g2.State == State.COMPLETE && boss.PlayState == PlayState.LOST);
});

// ============================================================ H  Applebough: Apple Toss
Guard("Applebough", () =>
{
    var g = Fight("dala_08_applebough");
    Controller boss = g.Player2, player = g.Player1;
    var w1 = Board(g, player, Wisp); var w2 = Board(g, player, Wisp); var croc = Board(g, player, Croc);
    ToBoss(g);
    var own = Board(g, boss, Wisp);
    bool used = Power(g);
    Check("H1 Apple Toss costs (2): it hits the lowest Health enemy (a 1-Health Wisp), which dies, so it repeats on the other Wisp, then on the Crocolisk (3 Health), which survives: it stops",
        Cost("DALA_BOSS_08p") == 2 && used && w1.IsDead && w2.IsDead && croc.Damage == 1 && player.Hero.Damage == 0 && own.Damage == 0 && boss.UsedMana == 2, "croc " + croc.Damage);
    Round(g);
    var squire = Board(g, player, Squire);
    Power(g);
    Check("H2 a Divine Shield takes the hit: the target lives, the power stops", !squire.HasDivineShield && !squire.IsDead && croc.Damage == 1);
    Round(g);
    foreach (var m in player.BoardZone.ToArray()) Kill(g, m);
    player.Hero.Damage = player.Hero.BaseHealth - 2;
    var tiny = Board(g, player, Wisp);
    Power(g);
    Check("H3 the hero is an enemy too: Wisp (1) first, then the hero (2 Health), which survives the 1 damage", tiny.IsDead && player.Hero.Health == 1);
    int heroFirst = 0, wispFirst = 0;
    for (int i = 0; i < 12; i++)
    {
        Round(g);
        foreach (var m in player.BoardZone.ToArray()) Kill(g, m);
        player.Hero.Damage = player.Hero.BaseHealth - 3;
        var a = Board(g, player, Croc);
        Power(g);
        if (a.Damage == 1) wispFirst++; else if (player.Hero.Health == 2) heroFirst++;
    }
    Check("H4 a tie (the hero and a Crocolisk at 3) is broken by the game RNG: twelve uses hit each of them", heroFirst > 0 && wispFirst > 0 && heroFirst + wispFirst == 12, heroFirst + "/" + wispFirst);
    Round(g);
    player.Hero.Damage = player.Hero.BaseHealth - 1;
    foreach (var m in player.BoardZone.ToArray()) Kill(g, m);
    Power(g);
    Check("H5 a hero it kills ends the game", g.State == State.COMPLETE && player.PlayState == PlayState.LOST);
});

// ============================================================ I  Archivist Oshi: Repeat History
Guard("Oshi", () =>
{
    var g = Fight("dala_09_oshi", shrine: Shrine);
    Controller boss = g.Player2, player = g.Player1;
    ToBoss(g);
    Check("I1 Repeat History costs (2) and is not offered while no friendly minion died this turn", Cost("DALA_BOSS_09p") == 2 && !Offered(g) && !ViewOffered(g));
    var theirs = Board(g, player, Croc);
    Kill(g, theirs);
    Check("I2 an enemy minion's death does not count", !Offered(g));
    var yeti = Board(g, boss, Yeti);
    Hit(g, yeti, 2, player.Hero);
    Kill(g, yeti);
    bool offered = Offered(g), view = ViewOffered(g);
    bool used = Power(g);
    var back = boss.BoardZone.FirstOrDefault();
    Check("I3 a friendly Yeti died this turn: it is offered (in the fair view too) and summons a new Yeti at full Health", offered && view && used && back != null && back != yeti
        && back.Card.Id == Yeti && back.Damage == 0 && boss.UsedMana == 2);
    Round(g);
    Check("I4 the next turn it is not offered", !Offered(g));
    var kinds = new HashSet<string>();
    for (int i = 0; i < 8; i++)
    {
        foreach (var m in boss.BoardZone.ToArray()) Kill(g, m);
        Round(g);
        var a = Board(g, boss, Croc); var b = Board(g, boss, Raptor);
        Kill(g, a); Kill(g, b);
        Power(g);
        kinds.Add(boss.BoardZone.FirstOrDefault()?.Card.Id ?? "none");
    }
    Check("I5 with two that died it summons one of them at random (eight uses: both seen)", kinds.SetEquals(new[] { Croc, Raptor }), string.Join(",", kinds));
    // A run shrine that changed sides is not possible; a shrine's corpse in the boss's graveyard never comes back.
    foreach (var m in boss.BoardZone.ToArray()) Kill(g, m);
    Round(g);
    var corpse = Board(g, boss, Wisp);
    corpse[ShrineRules.RunShrine] = 1;
    Kill(g, corpse);
    bool corpseOffered = ShrineRules.IsCorpse(corpse) && Offered(g);
    Check("I6 a run shrine's corpse is never summoned again (the power is not offered for it)", ShrineRules.IsCorpse(corpse) && !corpseOffered && DungeonRunEncounters.DiedThisTurn(boss).Count == 0,
        "corpse " + ShrineRules.IsCorpse(corpse));
});

// ============================================================ J  Mo Eniwhiskers: Street Smarts
Guard("Mo", () =>
{
    var g = Fight("dala_10_mo");
    Controller boss = g.Player2, player = g.Player1;
    Check("J1 Street Smarts is a passive power, started", boss.Hero.HeroPower.IsPassiveHeroPower && boss.Hero.HeroPower[EncounterRules.PowerActive] == 1);
    ToBoss(g);
    EmptyHand(boss);
    int deck = boss.DeckZone.Count;
    Cast(g, Coin);
    Check("J2 the boss plays The Coin: it draws a card", boss.DeckZone.Count == deck - 1 && boss.HandZone.Count == 1, "deck " + deck + ">" + boss.DeckZone.Count);
    deck = boss.DeckZone.Count;
    Cast(g, FakeCoin);
    Cast(g, Moonfire, player.Hero);
    Check("J3 Counterfeit Coin draws too; another card does not", boss.DeckZone.Count == deck - 1, "deck " + deck + ">" + boss.DeckZone.Count);
    ToPlayer(g);
    deck = boss.DeckZone.Count; int playerDeck = player.DeckZone.Count;
    Cast(g, Coin);
    Check("J4 the run player's Coin draws nobody a card", boss.DeckZone.Count == deck && player.DeckZone.Count == playerDeck);
});

// ============================================================ K  Noz Timbertail: Backstabber
Guard("Noz", () =>
{
    var g = Fight("dala_11_noz");
    Controller boss = g.Player2, player = g.Player1;
    Check("K1 Backstabber is a passive power, started", boss.Hero.HeroPower.IsPassiveHeroPower && boss.Hero.HeroPower[EncounterRules.PowerActive] == 1);
    var theirs = Board(g, player, Infiltrator);
    var plain = Board(g, player, Croc);
    Check("K2 a Stealthed minion summoned for the run player gains +1 Attack (Backstabby); a minion without Stealth does not", theirs.AttackDamage == 3 && Has(theirs, "DALA_BOSS_11e")
        && plain.AttackDamage == 2 && !Has(plain, "DALA_BOSS_11e"));
    ToBoss(g);
    var tiger = (Minion)ToHand(g, boss, Tiger);
    Play(g, tiger);
    Check("K3 a Stealthed minion the boss plays gains it too (Stranglethorn Tiger 6/5)", tiger.AttackDamage == 6 && Has(tiger, "DALA_BOSS_11e"));
    Check("K4 a minion that loses Stealth keeps the Attack it was given", Attack(g, tiger, player.Hero) && !tiger.HasStealth && tiger.AttackDamage == 6);
});

// ============================================================ L  Cravitz Lorent: Forbidden Love
Guard("Cravitz", () =>
{
    var g = Fight("dala_12_cravitz", shrine: Shrine);
    Controller boss = g.Player2, player = g.Player1;
    var shrine = ShrineRules.Of(player).First();
    ToBoss(g);
    var a = Board(g, boss, Yeti); var b = Board(g, boss, Croc);
    bool used = Power(g);
    Check("L1 Forbidden Love costs (3): one random minion of each board is destroyed (the run player's only minion is its shrine, which goes dormant)", Cost("DALA_BOSS_12p") == 3 && used
        && boss.BoardZone.Count == 1 && ShrineRules.IsCorpse(shrine) && ShrineRules.Of(player).FirstOrDefault()?.IsDormant == true && boss.UsedMana == 3);
    Round(g);
    var survivor = boss.BoardZone.First();
    Power(g);
    Check("L2 a board with no minion an effect can pick (only the dormant form) loses nothing; the boss's loses one", survivor.IsDead && ShrineRules.Of(player).FirstOrDefault()?.IsDormant == true
        && player.BoardZone.Count == 1);
    Round(g);
    Check("L3 with no minion on either board (the dormant form is not one) it is not offered", !Offered(g) && !ViewOffered(g));
    // (A dormant shrine wakes after its turns; the rest of the checks use a fight without one.)
    g = Fight("dala_12_cravitz");
    boss = g.Player2; player = g.Player1;
    ToBoss(g);
    var seen = new HashSet<string>();
    for (int i = 0; i < 10; i++)
    {
        Round(g);
        var x = Board(g, player, Croc); var y = Board(g, player, Raptor);
        Power(g);
        seen.Add(x.IsDead && !y.IsDead ? "croc" : y.IsDead && !x.IsDead ? "raptor" : "bad:" + x.IsDead + "/" + y.IsDead + "/" + boss.BoardZone.Count + "/" + boss.UsedMana);
        foreach (var m in player.BoardZone.Where(m => !m.IsDormant).ToArray()) Kill(g, m);
    }
    Check("L4 with two enemy minions exactly one dies, picked at random (ten uses: both)", seen.SetEquals(new[] { "croc", "raptor" }), string.Join(",", seen));
    Round(g);
    Board(g, boss, Wisp);
    Check("L5 one minion on the boss's own board is enough to offer it", Offered(g));
});

// ============================================================ M  Albin Eastoft: Slice and Dice
Guard("Albin", () =>
{
    var g = Fight("dala_13_albin");
    Controller boss = g.Player2, player = g.Player1;
    ToBoss(g);
    Check("M1 Slice and Dice costs (2) and is not offered with no minion on a board", Cost("DALA_BOSS_13p") == 2 && !Offered(g));
    var ms = new List<Minion> { Board(g, boss, Yeti), Board(g, boss, Yeti), Board(g, player, Yeti), Board(g, player, Yeti), Board(g, player, Yeti) };
    bool ok = true; var amounts = new HashSet<int>(); var sides = new HashSet<string>();
    for (int i = 0; i < 12; i++)
    {
        foreach (var m in ms) m.Damage = 0;
        if (!Power(g)) ok = false;
        var hit = ms.Where(m => m.Damage > 0).ToList();
        if (hit.Count != 3 || hit.Any(m => m.Damage < 1 || m.Damage > 3)) ok = false;
        foreach (var m in hit) { amounts.Add(m.Damage); sides.Add(m.Controller == boss ? "boss" : "player"); }
        Round(g);
    }
    foreach (var m in ms) m.Damage = 0;
    Check("M2 five minions: each use hits exactly three different ones for 1 to 3 each (twelve uses: 1, 2 and 3 seen, both boards)", ok && amounts.SetEquals(new[] { 1, 2, 3 })
        && sides.Count == 2, string.Join(",", amounts));
    foreach (var m in ms.Skip(1)) Kill(g, m);
    Power(g);
    Check("M3 with fewer minions each is hit once", ms[0].Damage >= 1 && ms[0].Damage <= 3);
    Check("M4 a hero is never hit", boss.Hero.Damage == 0 && player.Hero.Damage == 0);
});

// ============================================================ N  Tierra Blythe: Order Up!, the "Kind" Waitress
Guard("Tierra", () =>
{
    var g = Fight("dala_14_tierra");
    Controller boss = g.Player2, player = g.Player1;
    ToBoss(g);
    bool used = Power(g);
    var waitress = boss.BoardZone.FirstOrDefault();
    Check("N1 Order Up! costs (2) and summons a 2/2 Kind Waitress; with a full board it is not offered", Cost("DALA_BOSS_14p") == 2 && used && waitress?.Card.Id == Waitress
        && waitress.AttackDamage == 2 && waitress.Health == 2 && boss.UsedMana == 2);
    var yeti = Board(g, boss, Yeti);
    for (int i = boss.BoardZone.Count; i < 7; i++) Board(g, boss, Wisp);
    Round(g);
    Check("N1b full board", !Offered(g));
    foreach (var m in boss.BoardZone.Where(m => m.Card.Id == Wisp).ToArray()) Kill(g, m);
    ToPlayer(g);
    int toWaitress = 0, toHero = 0, toYeti = 0;
    for (int i = 0; i < 40; i++)
    {
        waitress.Damage = 0; yeti.Damage = 0; boss.Hero.Damage = 0;
        var wisp = Board(g, player, Wisp);
        Attack(g, wisp, waitress);
        if (waitress.Damage > 0) toWaitress++;
        if (boss.Hero.Damage > 0) toHero++;
        if (yeti.Damage > 0) toYeti++;
        foreach (var m in player.BoardZone.ToArray()) Kill(g, m);
        if (waitress.IsDead || yeti.IsDead) break;
    }
    Check("N2 forty attacks on the Waitress: about half go to someone else of her side (her hero or the Yeti, both seen)", toWaitress + toHero + toYeti == 40 && toWaitress >= 10 && toWaitress <= 30
        && toHero > 0 && toYeti > 0, toWaitress + "/" + toHero + "/" + toYeti);
    int toYetiOnly = 0;
    for (int i = 0; i < 10; i++)
    {
        waitress.Damage = 0; yeti.Damage = 0; boss.Hero.Damage = 0;
        var wisp = Board(g, player, Wisp);
        Attack(g, wisp, yeti);
        if (yeti.Damage > 0) toYetiOnly++;
        foreach (var m in player.BoardZone.ToArray()) Kill(g, m);
    }
    Check("N3 an attack on another of her side's characters is never redirected", toYetiOnly == 10);
    Silence(g, waitress);
    int stays = 0;
    for (int i = 0; i < 12; i++)
    {
        waitress.Damage = 0; yeti.Damage = 0; boss.Hero.Damage = 0;
        var wisp = Board(g, player, Wisp);
        Attack(g, wisp, waitress);
        if (waitress.Damage > 0) stays++;
        foreach (var m in player.BoardZone.ToArray()) Kill(g, m);
    }
    Check("N4 a silenced Waitress has no such rule: every attack lands on her", stays == 12, stays.ToString());
});

// ============================================================ O  Moon Priestess Nici: Blessing of Elune
Guard("Nici", () =>
{
    var g = Fight("dala_15_nici");
    Controller boss = g.Player2, player = g.Player1;
    Check("O1 Blessing of Elune is a passive power, started: both players' healing is doubled (ALL_HEALING_DOUBLE on both controllers)", boss.Hero.HeroPower.IsPassiveHeroPower
        && boss.Hero.HeroPower[EncounterRules.PowerActive] == 1 && boss.ControllerAuraEffects[GameTag.ALL_HEALING_DOUBLE] == 1 && player.ControllerAuraEffects[GameTag.ALL_HEALING_DOUBLE] == 1);
    Hit(g, player.Hero, 20, boss.Hero);
    int health = player.Hero.Health;
    Cast(g, FlashHeal, player.Hero);
    Check("O2 the run player's Flash Heal restores 10", player.Hero.Health == health + 10, health + ">" + player.Hero.Health);
    ToBoss(g);
    Hit(g, boss.Hero, 20, player.Hero);
    health = boss.Hero.Health;
    Cast(g, FlashHeal, boss.Hero);
    Check("O3 the boss's Flash Heal restores 10", boss.Hero.Health == health + 10, health + ">" + boss.Hero.Health);
    Board(g, boss, Auchenai);
    health = player.Hero.Health;
    Cast(g, FlashHeal, player.Hero);
    Check("O4 with Auchenai Soulpriest the boss's Flash Heal deals the doubled amount: 10 damage", player.Hero.Health == health - 10, health + ">" + player.Hero.Health);
    var g2 = Fight("dala_15_nici", shrine: Covenant);
    boss = g2.Player2; player = g2.Player1;
    Check("O5 (shrine) the run player's Bwonsamdi's Covenant is on its board", ShrineRules.Of(player).Any(m => m.Card.Id == Covenant && !m.IsDormant));
    ToBoss(g2);
    Hit(g2, boss.Hero, 5, player.Hero);
    health = boss.Hero.Health;
    Cast(g2, FlashHeal, boss.Hero);
    Check("O6 against Bwonsamdi's Covenant the boss's own Flash Heal damages its hero for the doubled amount (10)", boss.Hero.Health == health - 10, health + ">" + boss.Hero.Health);
});

// ============================================================ P  the rows, the admission rows, the validator
Guard("Rows", () =>
{
    var errors = DungeonRunValidator.Validate(data, null);
    Check("P1 the shipped data validates with the fifteen family 14 rows", errors.Count == 0, string.Join(" | ", errors.Take(4)));
    var rows = new (string id, string n, string name, string tier, int level, string kind, string[] flags, bool passive)[]
    {
        ("dala_01_chomper", "01", "Chomper", "1", 1, "extended", new string[0], false),
        ("dala_02_awilo", "02", "Awilo, Cooking Trainer", "2", 3, "extended", new[] { "passive" }, true),
        ("dala_03_akazamzarak", "03", "The Great Akazamzarak", "1", 1, "extended", new[] { "bothPlayers", "hiddenZone" }, false),
        ("dala_04_kaye", "04", "Kaye Toogie", "2", 4, "extended", new[] { "bothPlayers", "hiddenZone" }, false),
        ("dala_05_bonepaw", "05", "The Amazing \"Bonepaw\"", "2", 4, "extended", new[] { "bothPlayers", "recast" }, false),
        ("dala_06_dazzik", "06", "Dazzik \"Hellscream\"", "2", 4, "extended", new[] { "start" }, false),
        ("dala_07_belnaara", "07", "Flight Master Belnaara", "3", 6, "substituted", new[] { "passive", "forms", "heroImmune" }, true),
        ("dala_08_applebough", "08", "Applebough", "2", 3, "extended", new string[0], false),
        ("dala_09_oshi", "09", "Archivist Oshi", "3", 6, "authentic", new string[0], false),
        ("dala_10_mo", "10", "Mo Eniwhiskers", "2", 4, "extended", new[] { "passive" }, true),
        ("dala_11_noz", "11", "Noz Timbertail", "3", 6, "substituted", new[] { "passive", "bothPlayers" }, true),
        ("dala_12_cravitz", "12", "Cravitz Lorent", "2", 4, "extended", new[] { "bothPlayers" }, false),
        ("dala_13_albin", "13", "Albin Eastoft", "3", 6, "authentic", new string[0], false),
        ("dala_14_tierra", "14", "Tierra Blythe", "2", 3, "extended", new string[0], false),
        ("dala_15_nici", "15", "Moon Priestess Nici", "2", 3, "extended", new[] { "passive", "bothPlayers" }, true),
    };
    Check("P2 family 14 holds exactly these fifteen rows", data.Encounters.Where(b => b.Family == "14").Select(b => b.Id).OrderBy(i => i, StringComparer.Ordinal)
        .SequenceEqual(rows.Select(r => r.id).OrderBy(i => i, StringComparer.Ordinal)), string.Join(",", data.Encounters.Where(b => b.Family == "14").Select(b => b.Id)));
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
            && b.Forms.Count == (r.id == "dala_07_belnaara" ? 1 : 0),
            b.Source.Kind + " health " + plan.BossHealth + " flags " + string.Join(",", b.Flags) + " tier " + b.Tier + " level " + b.LevelOriginal);
    }
    string[] powers = rows.Select(r => "DALA_BOSS_" + r.n + "p").Concat(new[] { Flying }).ToArray();
    string[] cards = { Feat1, Feat2, Feat3, Feat4, Dazzik, Waitress };
    string[] enchantments = { DungeonRunEncounters.Reductomarad, FlyingE, "DALA_BOSS_11e" };
    var scripted = powers.Concat(cards).ToArray();
    var unscripted = scripted.Concat(enchantments).Where(id => !C(id).Implemented || DungeonRunEncounters.Find(id)?.Family != "14").ToList();
    // The engine's loader marks a text with a damage number ("$1") with a trailing " @spelldmg" (Apple Toss, Slice and Dice).
    var retexted = scripted.Concat(enchantments).Where(id => C(id).Text.Replace(" @spelldmg", "") != factById[id].GetProperty("text").GetString()).ToList();
    Check("P4 every power, boss card and enchantment of the family is scripted by family 14 and keeps the client's text (16 powers, 6 cards, 3 enchantments)", unscripted.Count == 0 && retexted.Count == 0
        && powers.Length == 16, string.Join(",", unscripted.Concat(retexted.Select(id => id + " [" + C(id).Text + "] vs [" + factById[id].GetProperty("text").GetString() + "]"))));
    var heroes = rows.Select(r => "DALA_BOSS_" + r.n + "h").ToList();
    Check("P4b the fifteen boss heroes need no script (portraits: their text is the encounter description); the hero card \"Scourgelord\" Dazzik is scripted",
        heroes.All(id => DungeonRunEncounters.Find(id) == null && C(id).Type == CardType.HERO) && C(Dazzik).Type == CardType.HERO && C(Dazzik).Implemented);
    var wrongScope = scripted.Where(id =>
    {
        var v = DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss, null, catalogRun: true);
        // The powers are a boss's only; a boss card is admitted in the run player's hands too (taken from a boss).
        return !(v.Admitted && v.Subject && v.Rule?.Ids != null && v.Rule.Ids.Contains(id) && DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss).Admitted
            && DungeonRunAdmission.Classify(C(id), id, AdmitScope.Player, null, catalogRun: true).Admitted == cards.Contains(id)
            && !DungeonRunAdmission.Classify(C(id), id, AdmitScope.Generated, null, catalogRun: true).Admitted
            && DungeonRunAdmission.GeneratedRejects(true).Contains(id) && DungeonRunAdmission.GeneratedRejects(false).Contains(id));
    }).ToList();
    Check("P5 each has its own admission row: the sixteen powers a boss's only, the six boss cards a boss's and (taken from a boss) the run player's; none is ever generated", wrongScope.Count == 0, string.Join(",", wrongScope));
    var families = scripted.Select(id => DungeonRunAdmission.RuleFor(id)?.Family).Distinct().OrderBy(f => f, StringComparer.Ordinal).ToList();
    int In(string family) => DungeonRunAdmission.Rules.Where(r => r.Family == family).Sum(r => r.Ids.Length);
    Check("P6 the six admission families of the family: heist-board-power (7), heist-own-hand-power (1), heist-hidden-deck-power (1), heist-recast-power (1), heist-passive (6), heist-boss-card (6)",
        families.SequenceEqual(new[] { "heist-board-power", "heist-boss-card", "heist-hidden-deck-power", "heist-own-hand-power", "heist-passive", "heist-recast-power" })
        && In("heist-board-power") == 7 && In("heist-own-hand-power") == 1 && In("heist-hidden-deck-power") == 1 && In("heist-recast-power") == 1 && In("heist-passive") == 6
        && In("heist-boss-card") == 6, string.Join(",", families));
    var dazzikRow = data.Boss("dala_06_dazzik");
    Check("P7 the one start block: Dazzik's deck add (one \"Scourgelord\" Dazzik); no other family 14 row has one", dazzikRow.Start != null && dazzikRow.Start.BossDeckAdd.SequenceEqual(new[] { Dazzik })
        && dazzikRow.Start.BossWeapon == null && rows.Where(r => r.id != "dala_06_dazzik").All(r => data.Boss(r.id).Start == null));
    string Subs(string id) => string.Join(",", data.Boss(id).Source.Substitutions.Where(x => x.Listed != null).Select(x => x.Listed + ">" + (x.Used ?? "-") + ":" + x.Reason).Distinct().OrderBy(x => x, StringComparer.Ordinal));
    Check("P8 the listed cards that are replaced or left out are only cards the engine does not script or a boss deck does not admit, each recorded",
        Subs("dala_01_chomper") == "EX1_160>CFM_614:unscripted" && Subs("dala_04_kaye") == "GVG_016>GVG_020:unscripted,GVG_077>GVG_105:unscripted"
        && Subs("dala_07_belnaara") == "KAR_037>-:admission:unclassified" && Subs("dala_11_noz") == "GVG_088>EX1_028:unscripted,GVG_112>CS2_200:unscripted"
        && Subs("dala_14_tierra") == "BRMA01_3>-:unscripted"
        && rows.Where(r => !new[] { "dala_01_chomper", "dala_04_kaye", "dala_07_belnaara", "dala_11_noz", "dala_14_tierra" }.Contains(r.id)).All(r => Subs(r.id) == "")
        && rows.SelectMany(r => data.Boss(r.id).Source.Substitutions).Where(x => x.Reason == "unscripted").All(x => !C(x.Listed).Implemented),
        Subs("dala_01_chomper") + " | " + Subs("dala_04_kaye") + " | " + Subs("dala_07_belnaara") + " | " + Subs("dala_11_noz") + " | " + Subs("dala_14_tierra"));
    int Deck(string boss, string card) => data.BossDecks[data.Boss(boss).Deck].Count(id => id == card);
    Check("P9 the quantity lists are the wiki's: Oshi and Albin unchanged (Prophet Velen x1, Kingsbane x1), Belnaara's King Krush x1; Mo's deck holds Counterfeit Coins; Noz's Stranglethorn Tigers",
        Deck("dala_09_oshi", "EX1_350") == 1 && Deck("dala_13_albin", "LOOT_542") == 1 && Deck("dala_07_belnaara", "EX1_543") == 1 && Deck("dala_10_mo", FakeCoin) >= 1
        && Deck("dala_11_noz", Tiger) == 2);
    var playerPower = DungeonRunAdmission.Classify(C("DALA_BOSS_08p"), "DALA_BOSS_08p", AdmitScope.Player, null, catalogRun: true);
    Check("P10 a family 14 power asked for the run player is refused with its row's reason", !playerPower.Admitted && playerPower.Family == "heist-board-power" && playerPower.Lift == DungeonRunAdmission.NoneBossPower);
    DungeonRunData Changed(Action<DungeonRunData> change) { var d = Load(); change(d); return d; }
    var badForm = DungeonRunValidator.Validate(Changed(d => d.Boss("dala_07_belnaara").Forms[0].Power = "DALA_BOSS_07x"), null);
    Check("P11 the validator checks a form's power: an unknown one is refused", badForm.Count > 0, string.Join(" | ", badForm.Take(2)));
    Check("P12 the tier pools: two rows in enc-t1, nine in enc-t2, four in enc-t3", rows.Count(r => r.tier == "1") == 2 && rows.Count(r => r.tier == "2") == 9 && rows.Count(r => r.tier == "3") == 4
        && rows.All(r => data.Boss(r.id).Pools.SequenceEqual(new[] { "enc-t" + r.tier })));
    Check("P13 the extra cards each row names (the feats, the Scroll, the enchantments, the Waitress) are its 'cards'", data.Boss("dala_03_akazamzarak").Cards.Contains(Feat1)
        && data.Boss("dala_03_akazamzarak").Cards.Contains(Scroll) && data.Boss("dala_07_belnaara").Cards.Contains(FlyingE) && data.Boss("dala_14_tierra").Cards.Contains(Waitress)
        && data.Boss("dala_11_noz").Cards.Contains("DALA_BOSS_11e"));
});

Console.WriteLine($"SearchTelemetry swallowed leaf exceptions {SearchTelemetry.SwallowedLeafErrors}");
Console.WriteLine($"RESULT {passes} passed, {failures + SearchTelemetry.SwallowedLeafErrors} failed");
return failures + SearchTelemetry.SwallowedLeafErrors;
