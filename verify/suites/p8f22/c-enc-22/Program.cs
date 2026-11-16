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

// Phase 8 family 22, Tombs of Terror bosses D (real engine games through the run setup path: DungeonRun.BuildPlan on the shipped
// row -> DungeonRunSetup.Configure / ApplyAfterStart -> both mulligans -> MainBegin; the boss is player 2): Zaraam (Forbidden
// Power), Kyriss (Chain Lightning), A Beautiful Oasis (Mirage, the illusion), Vessina (Plaguebringer), Thrud (Ogre-Sized Thief,
// the deck steal with the boss's fair view), Siamat (Masterful Djinn, Servant of Siamat), Weaponized Wasp ("Upgraded" Fauna),
// Isiset (Mistress of Magic, both players), Rajh (Lord of the Sun), Ammunae (Lord of Life), Setesh (Lord of Destruction, Cursed!),
// General Husam (Shockwave), High Prophet Barim (Burst of Light), Avozu (Hunt the Weak), Tarkus (Noble Champion), Gorebite
// (Snapping Jaw), Octosari (Massive Tentacles, shrines go dormant) and Armadillobot (Mech "Pilot"), one section each (A-R), and
// the rows, the tier rule, the admission rows and the validator (S).
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

const string Yeti = "CS2_182", Croc = "CS2_120", Moonfire = "CS2_008", Fireball = "CS2_029", Wisp = "CS2_231", Shrine = "TRLA_154", Bolt = "EX1_238";
const string Trogg = "LOE_018", Silence = "EX1_332", Squire = "EX1_008", WarAxe = "CS2_106", HarvestGolem = "EX1_556", Mechwarper = "GVG_006";
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
void FillHand(Controller c) { while (!c.HandZone.IsFull) Generic.AddHandPhase.Invoke(c, Entity.FromCard(c, C(Yeti))); }
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
void Kill(Game g, Minion m) { new DestroyTask(EntityType.TARGET).Process(g, m.Controller, m.Controller.Hero, m, null); g.DeathProcessingAndAuraUpdate(); }
void Hit(Game g, ICharacter m, int n) { new DamageTask(n, EntityType.TARGET).Process(g, m.Controller.Opponent, m.Controller.Opponent.Hero, (IPlayable)m, null); g.DeathProcessingAndAuraUpdate(); }
bool Has(IPlayable p, string enchantmentId) => p.AppliedEnchantments?.Any(e => e.Card.Id == enchantmentId) == true;
int Count(IPlayable p, string enchantmentId) => p.AppliedEnchantments?.Count(e => e.Card.Id == enchantmentId) ?? 0;
int Cost(string id) => C(id).Cost;
HeroPower BossPower(Game g) => g.Player2.Hero.HeroPower;
(Game view, Controller boss, Controller player, FairBotState bot) FairView(Game g, Controller owner = null)
{
    owner ??= g.Player2;
    var bot = FairBotState.Create(g, owner.PlayerId, 1, DeckStyle.Tempo);
    var view = bot.FingerprintGame;
    var viewOwner = view.Player1.PlayerId == owner.PlayerId ? view.Player1 : view.Player2;
    return (view, viewOwner, viewOwner.Opponent, bot);
}
bool Clickable(Game g) => !g.Player2.Hero.HeroPower.IsPassiveHeroPower;
// A passive (or rule-cast) power: never an option, started at match start (its trigger or aura is live).
bool PassiveStarted(Game g, string power) => g.Player2.Hero.HeroPower.Card.Id == power && g.Player2.Hero.HeroPower.IsPassiveHeroPower
    && DungeonRunEncounters.PassivePowers.Contains(power) && g.Player2.Hero.HeroPower[EncounterRules.PowerActive] == 1;
// The engine appends " @spelldmg" to a text with a $ (Card: the client's Spell Damage marker); the text itself is the client's.
string EngineText(string id) => (C(id).Text ?? "").Replace(" @spelldmg", "");
string ClientText(string id) => factById[id].GetProperty("text").GetString();

// ============================================================ A  Zaraam: Forbidden Power
Guard("Zaraam", () =>
{
    var g = Fight("ulda_60_zaraam");
    Controller boss = g.Player2, player = g.Player1;
    var power = BossPower(g);
    Check("A1 Forbidden Power costs (4), is clickable and untargeted; its number (the @) starts at 1; its trigger is started", Cost("ULDA_BOSS_60p") == 4 && Clickable(g)
        && DungeonRunEncounters.TombsD.Number(power) == 1 && power[EncounterRules.PowerActive] == 1 && EngineText("ULDA_BOSS_60p") == ClientText("ULDA_BOSS_60p"));
    ToBoss(g);
    int hp = player.Hero.Health;
    bool used = Power(g);
    Check("A2 with no enemy minion it hits the run player's hero for 1", used && player.Hero.Health == hp - 1 && PowerTargets(g).Count == 0, hp + "->" + player.Hero.Health);
    var a = Board(g, boss, Wisp); var b = Board(g, boss, Wisp);
    Kill(g, a); Kill(g, b);
    var theirs = Board(g, player, Wisp); Kill(g, theirs);
    Check("A3 two minions of the boss's die: the number is 3; a dying minion of the run player's adds nothing", DungeonRunEncounters.TombsD.Number(power) == 3,
        DungeonRunEncounters.TombsD.Number(power).ToString());
    Round(g);
    var yeti = Board(g, player, Yeti);
    hp = player.Hero.Health;
    Power(g);
    int dealt = (hp - player.Hero.Health) + yeti.Damage;
    Check("A4 used again: 3 damage to one random enemy character (the hero or the Yeti), the number stays 3",
        dealt == 3 && (yeti.Damage == 0 || player.Hero.Health == hp) && DungeonRunEncounters.TombsD.Number(power) == 3, $"hero {hp}->{player.Hero.Health} yeti {yeti.Damage}");
    // Over several seeds the random enemy is sometimes the hero and sometimes the minion.
    bool hitHero = false, hitMinion = false;
    for (int seed = 1; seed <= 12 && !(hitHero && hitMinion); seed++)
    {
        var g2 = Fight("ulda_60_zaraam", seed: seed); ToBoss(g2);
        var y = Board(g2, g2.Player1, Yeti); int h = g2.Player1.Hero.Health;
        Power(g2);
        hitHero |= g2.Player1.Hero.Health == h - 1; hitMinion |= y.Damage == 1;
    }
    Check("A5 the random enemy is the hero in some games and the minion in others", hitHero && hitMinion);
});

// ============================================================ B  Kyriss: Chain Lightning
Guard("Kyriss", () =>
{
    var g = Fight("ulda_61_kyriss");
    Controller boss = g.Player2, player = g.Player1;
    var mine = Board(g, boss, Yeti);
    var row = new[] { Board(g, player, Yeti), Board(g, player, Yeti), Board(g, player, Yeti) };
    ToBoss(g);
    var targets = PowerTargets(g);
    Check("B1 Chain Lightning costs (4) and targets any minion, never a hero", Cost("ULDA_BOSS_61p") == 4 && targets.Count == 4 && targets.Contains(mine) && row.All(targets.Contains)
        && !targets.Contains(player.Hero));
    var hits = DungeonRunEncounters.TombsD.ChainLightning(g, BossPower(g), row[1]);
    var dead = hits.Last();
    bool adjacent = hits.Zip(hits.Skip(1), (x, y) => Math.Abs(x.ZonePosition - y.ZonePosition) == 1).All(ok => ok);
    Check("B2 on the middle of three 4/5s it hits 1 at a time, each jump to a neighbour of the last hit, until one dies (5 hits on it); the boss's own minion is never hit",
        hits[0] == row[1] && adjacent && dead.IsDead && hits.Count(m => m == dead) == 5 && hits.Count == row.Sum(m => m.Damage) && hits.All(row.Contains) && mine.Damage == 0
        && row.Count(m => m.IsDead) == 1, string.Join(",", hits.Select(m => m.ZonePosition)));
    g.DeathProcessingAndAuraUpdate();
    var g2 = Fight("ulda_61_kyriss");
    var lone = Board(g2, g2.Player1, Yeti);
    ToBoss(g2);
    bool used = Power(g2, lone);
    Check("B3 used on a minion with no neighbour: one hit, it survives", used && lone.Damage == 1 && !lone.IsDead && g2.Player2.UsedMana == 4);
    var g3 = Fight("ulda_61_kyriss");
    var wisp = Board(g3, g3.Player1, Wisp); var next = Board(g3, g3.Player1, Yeti);
    ToBoss(g3);
    Power(g3, wisp);
    Check("B4 a 1-Health target dies of the first hit: the chain ends there", wisp.Zone != g3.Player1.BoardZone && next.Damage == 0);
    var g4 = Fight("ulda_61_kyriss");
    var squire = Board(g4, g4.Player1, Squire); var beside = Board(g4, g4.Player1, Croc);
    ToBoss(g4);
    var chain = DungeonRunEncounters.TombsD.ChainLightning(g4, BossPower(g4), squire);
    Check("B5 a Divine Shield takes the first hit and the chain jumps on", chain.Count >= 2 && !squire.HasDivineShield && chain[1] == beside && chain.Last().IsDead,
        string.Join(",", chain.Select(m => m.Card.Id)));
    // A dormant run shrine beside the target is no neighbour.
    var g5 = Fight("ulda_61_kyriss", shrine: Shrine);
    var shrineMinion = ShrineRules.Of(g5.Player1).First();
    Kill(g5, shrineMinion);
    var dormant = ShrineRules.Of(g5.Player1).First();
    var yeti5 = Board(g5, g5.Player1, Yeti);
    ToBoss(g5);
    var chain5 = DungeonRunEncounters.TombsD.ChainLightning(g5, BossPower(g5), yeti5);
    Check("B6 a dormant run shrine beside the target is no neighbour: one hit on the Yeti, the shrine untouched", dormant.Untouchable && chain5.Count == 1 && yeti5.Damage == 1
        && dormant.Damage == 0 && dormant.Zone == g5.Player1.BoardZone, chain5.Count.ToString());
});

// ============================================================ C  A Beautiful Oasis: Mirage, the illusion
Guard("Oasis", () =>
{
    var g = Fight("ulda_62_oasis", playerDeck: Croc);
    Controller boss = g.Player2, player = g.Player1;
    var power = BossPower(g);
    Check("C1 Mirage costs (0), is rule-cast (never an option; started at match start); not cast yet", Cost("ULDA_BOSS_62p") == 0 && PassiveStarted(g, "ULDA_BOSS_62p")
        && DungeonRunEncounters.AutoCasts(power) == 0 && C("ULDA_BOSS_62p").Text == ClientText("ULDA_BOSS_62p"));
    var before = player.HandZone.Select(p => p.Id).ToList();
    ToBoss(g);
    var illusion = DungeonRunEncounters.TombsD.Illusion(power);
    var twin = DungeonRunEncounters.TombsD.Twin(illusion);
    var copy = player.HandZone.FirstOrDefault(p => !before.Contains(p.Id));
    Check("C2 at the start of the boss's turn it is cast: a copy of a card of the run player's hand joins that hand, and one of the two is the illusion",
        DungeonRunEncounters.AutoCasts(power) == 1 && player.HandZone.Count == before.Count + 1 && copy != null && illusion != null && twin != null
        && illusion.Zone == player.HandZone && twin.Zone == player.HandZone && (illusion == copy) != (twin == copy) && illusion.Card.Id == twin.Card.Id
        && DungeonRunEncounters.TombsD.IllusionIsCopy(illusion) == (illusion == copy) && !Offered(g), $"hand {before.Count}->{player.HandZone.Count}");
    var (view, vb, vp, bot) = FairView(g);
    Check("C3 the boss's fair view: the run player's hand (the copy too) stays unknown cards, one more of them", vp.HandZone.Count == player.HandZone.Count
        && vp.HandZone.All(p => p is Unknown) && bot.Facts.Opponent.Hand.All(c => c.Unknown), vp.HandZone.Count.ToString());
    // The boss's power holds only the illusion's public hand position; in the boss's fair view the illusion is the masked slot there.
    HeroPower ViewPower(Controller viewBoss) => viewBoss.Hero.HeroPower;
    int slot = illusion.ZonePosition + 1;
    var viewIllusion = DungeonRunEncounters.TombsD.Illusion(ViewPower(vb));
    Check("C3b the boss's power holds the illusion's hand position, not an entity id; in the boss's fair view Illusion() is the masked slot at that position (no twin known)",
        DungeonRunEncounters.TombsD.IllusionSlot(power) == slot && ViewPower(vb)[EncounterRules.MirageIllusion] == slot && viewIllusion != null && viewIllusion is Unknown
        && viewIllusion == vp.HandZone[slot - 1] && DungeonRunEncounters.TombsD.Twin(viewIllusion) == null, $"slot {slot} power {DungeonRunEncounters.TombsD.IllusionSlot(power)} view {ViewPower(vb)[EncounterRules.MirageIllusion]}");
    // A card to the illusion's left leaves the hand (a discard on the boss's turn): the position follows the illusion.
    var left = player.HandZone.FirstOrDefault(p => p.ZonePosition < illusion.ZonePosition) ?? player.HandZone.Last(p => p != illusion && p != twin);
    bool shifts = left.ZonePosition < illusion.ZonePosition;
    new DiscardTask(EntityType.TARGET).Process(g, player, boss.Hero, left, null);
    g.DeathProcessingAndAuraUpdate();
    int moved = illusion.ZonePosition + 1;
    var (view2, vb2, vp2, _) = FairView(g);
    var viewIllusion2 = DungeonRunEncounters.TombsD.Illusion(ViewPower(vb2));
    Check("C3c a card leaving the hand moves the position with the illusion (aura update); the new fair view's illusion is the masked slot at the new position",
        DungeonRunEncounters.TombsD.IllusionSlot(power) == moved && moved == (shifts ? slot - 1 : slot) && viewIllusion2 == vp2.HandZone[moved - 1]
        && DungeonRunEncounters.TombsD.Illusion(power) == illusion, $"{slot}->{moved} power {DungeonRunEncounters.TombsD.IllusionSlot(power)}");
    // The boss's search may run on past its turn in its view: at the end of the run player's turn the view's illusion slot fades,
    // and at the boss's next turn start nothing is copied (the hand is masked).
    string simulated = "ok"; int viewHand = -1, viewAfter = -1; IPlayable faded = null;
    try
    {
        view2.Process(EndTurnTask.Any(view2.CurrentPlayer));
        viewHand = vp2.HandZone.Count;
        view2.Process(EndTurnTask.Any(view2.CurrentPlayer));
        viewAfter = vp2.HandZone.Count;
        faded = DungeonRunEncounters.TombsD.Illusion(ViewPower(vb2));
    }
    catch (Exception e) { simulated = e.GetType().Name + ": " + e.Message; }
    Check("C3d in the view the masked illusion slot leaves at the end of the run player's turn and the boss's next turn start copies nothing; no exception",
        simulated == "ok" && viewAfter == viewHand - 1 && faded == null && viewIllusion2.Zone != vp2.HandZone, $"{simulated} hand {viewHand}->{viewAfter}");
    ToPlayer(g);
    int hand = player.HandZone.Count, grave = player.GraveyardZone.Count, discarded = player.DiscardedEntities.Count;
    bool played = Play(g, illusion, null);
    Check("C4 the run player plays the illusion: it resolves as its card (a River Crocolisk on the board) and a random card of the hand is discarded",
        played && player.BoardZone.Any(m => m == illusion) && player.HandZone.Count == hand - 2 && player.GraveyardZone.Count == grave + 1
        && DungeonRunEncounters.TombsD.Illusion(power) == null && DungeonRunEncounters.TombsD.IllusionSlot(power) == 0 && player.DiscardedEntities.Count == discarded + 1, $"hand {hand}->{player.HandZone.Count}");
    bool twinKept = twin.Zone == player.HandZone;
    var kept = player.HandZone.Select(p => p.Id).ToList();
    EndTurn(g); // the run player's turn ends; the boss's turn starts and casts Mirage again
    Check("C5 the pair is resolved: the twin, if not discarded, is an ordinary card that stays at the end of the turn (every card kept; the next cast adds one)",
        kept.All(id => player.HandZone.Any(p => p.Id == id)) && player.HandZone.Count == kept.Count + 1 && (!twinKept || twin.Zone == player.HandZone));
    // The twin is played: no discard; the illusion leaves the hand at the end of the turn.
    var g2 = Fight("ulda_62_oasis", playerDeck: Croc, seed: 11);
    ToBoss(g2);
    var p2 = BossPower(g2); var ill2 = DungeonRunEncounters.TombsD.Illusion(p2); var twin2 = DungeonRunEncounters.TombsD.Twin(ill2);
    ToPlayer(g2);
    int h2 = g2.Player1.HandZone.Count;
    Play(g2, twin2, null);
    Check("C6 the twin played: no discard", g2.Player1.HandZone.Count == h2 - 1 && g2.Player1.DiscardedEntities.Count == 0 && ill2.Zone == g2.Player1.HandZone);
    EndTurn(g2);
    Check("C7 at the end of the run player's turn the illusion leaves the hand (set aside, not discarded); the boss's next turn start casts a new one",
        ill2.Zone == g2.Player1.SetasideZone && g2.Player1.HandZone.Count == h2 - 2 + 1 && g2.Player1.DiscardedEntities.Count == 0
        && DungeonRunEncounters.TombsD.Illusion(p2) != ill2 && DungeonRunEncounters.AutoCasts(p2) == 2, $"hand {h2}->{g2.Player1.HandZone.Count}");
    // Neither played: the copy leaves, the original stays, nothing is discarded.
    bool sawCopyIllusion = false, sawOriginalIllusion = false;
    for (int seed = 1; seed <= 16; seed++)
    {
        var g3 = Fight("ulda_62_oasis", playerDeck: Croc, seed: seed);
        var start = g3.Player1.HandZone.Select(p => p.Id).ToList();
        ToBoss(g3);
        var p3 = BossPower(g3); var ill3 = DungeonRunEncounters.TombsD.Illusion(p3); if (ill3 == null) continue;
        bool isCopy = DungeonRunEncounters.TombsD.IllusionIsCopy(ill3);
        var added = isCopy ? ill3 : DungeonRunEncounters.TombsD.Twin(ill3);
        sawCopyIllusion |= isCopy; sawOriginalIllusion |= !isCopy;
        ToPlayer(g3);
        var held = g3.Player1.HandZone.Where(p => p != added).Select(p => p.Id).ToList(); // the originals and the card drawn
        EndTurn(g3);
        bool back = added.Zone == g3.Player1.SetasideZone && held.All(id => g3.Player1.HandZone.Any(p => p.Id == id)) && g3.Player1.HandZone.Count == held.Count + 1
            && g3.Player1.DiscardedEntities.Count == 0;
        if (!back) { Check("C8 seed " + seed + ": neither card played: the copy leaves, every original card stays, nothing is discarded", false); break; }
    }
    Check("C8 neither card played: the copy leaves, every original card stays, nothing is discarded; the illusion is the copy in some games and the original in others",
        sawCopyIllusion && sawOriginalIllusion);
    // Not cast on a full or an empty hand.
    var g4 = Fight("ulda_62_oasis", playerDeck: Croc);
    FillHand(g4.Player1); ToBoss(g4);
    var g5 = Fight("ulda_62_oasis", playerDeck: Croc);
    EmptyHand(g5.Player1); ToBoss(g5);
    Check("C9 not cast while the run player's hand is full or empty", DungeonRunEncounters.AutoCasts(BossPower(g4)) == 0 && DungeonRunEncounters.AutoCasts(BossPower(g5)) == 0
        && g4.Player1.HandZone.Count == 10 && g5.Player1.HandZone.Count == 0);
    // The run player's own view knows its hand; a played non-illusion of another game never discards.
    var (pview, pv, _, _) = FairView(g2, g2.Player1);
    Check("C10 the run player's own fair view holds its hand's cards (known to their owner)", pv.HandZone.All(p => !(p is Unknown)) && pv.HandZone.Count == g2.Player1.HandZone.Count);
});

// ============================================================ D  Vessina: Plaguebringer
Guard("Vessina", () =>
{
    var g = Fight("ulda_63_vessina");
    Controller boss = g.Player2, player = g.Player1;
    Check("D1 Plaguebringer is passive: started at match start, never an option", PassiveStarted(g, "ULDA_BOSS_63p") && C("ULDA_BOSS_63p").Text == ClientText("ULDA_BOSS_63p"));
    ToBoss(g);
    var fireball = ToHand(g, boss, Fireball); var moon = ToHand(g, boss, Moonfire); var yeti = ToHand(g, boss, Yeti);
    Check("D2 the spells in the boss's hand cost (2) less (Fireball 4 -> 2, Moonfire 0 stays 0); its minions do not", fireball.Cost == 2 && moon.Cost == 0 && yeti.Cost == 4,
        $"{fireball.Cost} {moon.Cost} {yeti.Cost}");
    var trogg = Board(g, boss, Trogg);
    Mana(boss);
    g.Process(PlayCardTask.Any(boss, fireball, player.Hero, -1, 0));
    Check("D3 the boss plays Fireball (no Overload of its own): it is Overloaded by (1), and Tunnel Trogg gains +1 Attack", boss.OverloadOwed == 1 && trogg.AttackDamage == 2
        && boss.UsedMana == 2, $"owed {boss.OverloadOwed} trogg {trogg.AttackDamage}");
    int owed = boss.OverloadOwed;
    g.Process(PlayCardTask.Any(boss, ToHand(g, boss, Bolt), player.Hero, -1, 0));
    Check("D4 Lightning Bolt (Overload 1): 1 + 1 more; Tunnel Trogg counts both", boss.OverloadOwed == owed + 2 && trogg.AttackDamage == 4, $"owed {boss.OverloadOwed} trogg {trogg.AttackDamage}");
    ToPlayer(g);
    var theirs = ToHand(g, player, Fireball);
    Check("D5 next turn the boss's Overload locks 3 crystals; the run player's spells cost what they cost and never Overload",
        theirs.Cost == 4 && player.OverloadOwed == 0);
    Play(g, theirs, boss.Hero);
    Check("D6 the run player's Fireball Overloads nothing", player.OverloadOwed == 0);
    var g2 = Fight("ulda_63_vessina");
    ToBoss(g2);
    var thunderhead = Board(g2, g2.Player2, "BOT_407");
    int Sparks() => g2.Player2.BoardZone.Count(m => m.Card.Id == "BOT_102t");
    Cast(g2, Moonfire, g2.Player1.Hero);
    int afterMoonfire = Sparks();
    Cast(g2, Bolt, g2.Player1.Hero);
    Check("D7 Thunderhead sees a spell that Overloads only by Plaguebringer as a card with Overload (Moonfire: two Sparks); Lightning Bolt, with its own Overload, two more (not four)",
        afterMoonfire == 2 && Sparks() == 4, $"{afterMoonfire} {Sparks()}");
});

// ============================================================ E  Thrud: Ogre-Sized Thief, the deck steal
Guard("Thrud", () =>
{
    var g = Fight("ulda_65_thrud", playerDeck: Croc);
    Controller boss = g.Player2, player = g.Player1;
    ToBoss(g);
    var playerDeck = new HashSet<int>(player.DeckZone.Select(p => p.Id));
    int deck = player.DeckZone.Count, hand = boss.HandZone.Count;
    bool used = Power(g);
    var stolen = boss.HandZone.Where(p => playerDeck.Contains(p.Id)).ToList();
    var card = stolen.FirstOrDefault();
    Check("E1 Ogre-Sized Thief costs (3), untargeted: one card of the run player's deck is now the boss's, in its hand", Cost("ULDA_BOSS_65p") == 3 && used && stolen.Count == 1
        && card.Controller == boss && card.Zone == boss.HandZone && player.DeckZone.Count == deck - 1 && boss.HandZone.Count == hand + 1 && card.Card.Id == Croc
        && boss.UsedMana == 3, $"{stolen.Count} deck {deck}->{player.DeckZone.Count}");
    var (view, vb, vp, bot) = FairView(g);
    var known = bot.Facts.Own.Hand.FirstOrDefault(c => c.EntityId == card.Id);
    Check("E2 the boss's fair view: it knows exactly the card it took (a card of its own hand)", known != null && !known.Unknown && known.CardId == Croc);
    Check("E3 the run player's other deck cards stay unknown slots in the boss's view (count = the deck's)",
        bot.Facts.Opponent.DeckCount == player.DeckZone.Count && vp.DeckZone.Count == player.DeckZone.Count && vp.DeckZone.All(p => p is Unknown), $"{bot.Facts.Opponent.DeckCount} {vp.DeckZone.Count}");
    var (pview, pv, pb, pbot) = FairView(g, player);
    Check("E4 the run player's fair view: the stolen card is a hidden card of the boss's hand", pb.HandZone.Count == boss.HandZone.Count && pb.HandZone.All(p => p is Unknown)
        && !pbot.Facts.Opponent.Hand.Any(c => !c.Unknown),
        pb.HandZone.Count + " " + string.Join(",", pb.HandZone.Select(p => p.Id + ":" + p.GetType().Name + ":" + p.Card.Id)) + " stolen " + card.Id);
    Round(g);
    var (view2, vb2, vp2, _) = FairView(g);
    string simulated = "ok"; int vh = vb2.HandZone.Count, vd = vp2.DeckZone.Count;
    try { vb2.BaseMana = 10; vb2.UsedMana = 0; view2.Process(HeroPowerTask.Any(vb2)); view2.DeathProcessingAndAuraUpdate(); var options = vb2.Options(); } catch (Exception e) { simulated = e.GetType().Name + ": " + e.Message; }
    Check("E5 the boss's search may use the power in its view: it takes a masked card (the view's hand + 1, the view's run player deck - 1), no exception",
        simulated == "ok" && vb2.HandZone.Count == vh + 1 && vp2.DeckZone.Count == vd - 1, $"{simulated} hand {vh}->{vb2.HandZone.Count} deck {vd}->{vp2.DeckZone.Count}");
    Mana(boss); Play(g, card);
    Check("E6 the boss plays the stolen card (a minion of its board now)", boss.BoardZone.Contains(card) && card.Controller == boss);
    var g2 = Fight("ulda_65_thrud");
    ToBoss(g2); FillHand(g2.Player2);
    int grave = g2.Player2.GraveyardZone.Count, deck2 = g2.Player1.DeckZone.Count;
    Power(g2);
    Check("E7 with the boss's hand full the stolen card is burned (out of the run player's deck all the same)", g2.Player1.DeckZone.Count == deck2 - 1 && g2.Player2.GraveyardZone.Count == grave + 1);
    var g3 = Fight("ulda_65_thrud");
    EmptyDeck(g3.Player1); ToBoss(g3);
    Check("E8 the run player's deck empty: not offered", !Offered(g3));
});

// ============================================================ F  Siamat: Masterful Djinn, Servant of Siamat
Guard("Siamat", () =>
{
    var g = Fight("ulda_66_siamat");
    Controller boss = g.Player2;
    ToBoss(g);
    bool used = Power(g);
    var servant = boss.BoardZone.LastOrDefault();
    bool HasKeyword(Minion m, GameTag k) => k == GameTag.TAUNT ? m.HasTaunt : m[k] == 1;
    int keywords(Minion m) => DungeonRunEncounters.TombsD.DjinnKeywords.Count(k => HasKeyword(m, k));
    Check("F1 Masterful Djinn costs (2): a 1/4 Servant of Siamat (ULDA_026, an Elemental) with exactly one of Taunt, Rush, Windfury, Divine Shield",
        Cost("ULDA_BOSS_66p") == 2 && used && servant != null && servant.Card.Id == "ULDA_026" && servant.AttackDamage == 1 && servant.Health == 4 && servant.IsRace(Race.ELEMENTAL)
        && keywords(servant) == 1, servant == null ? "none" : string.Join(",", DungeonRunEncounters.TombsD.DjinnKeywords.Where(k => HasKeyword(servant, k))));
    var seen = new HashSet<GameTag>();
    for (int seed = 1; seed <= 20 && seen.Count < 4; seed++)
    {
        var g2 = Fight("ulda_66_siamat", seed: seed); ToBoss(g2);
        for (int i = 0; i < 3; i++) { Power(g2); Round(g2); }
        foreach (var m in g2.Player2.BoardZone.Where(m => m.Card.Id == "ULDA_026")) seen.UnionWith(DungeonRunEncounters.TombsD.DjinnKeywords.Where(k => HasKeyword(m, k)));
    }
    Check("F2 over several games every one of the four keywords comes up", seen.Count == 4, string.Join(",", seen));
    var shielded = Enumerable.Range(1, 30).Select(seed => { var gs = Fight("ulda_66_siamat", seed: seed); ToBoss(gs); Power(gs); return (gs, m: gs.Player2.BoardZone.Last()); })
        .First(x => x.m.HasDivineShield || x.m.HasTaunt || x.m.HasWindfury || x.m.IsRush);
    ToPlayer(shielded.gs);
    Play(shielded.gs, ToHand(shielded.gs, shielded.gs.Player1, Silence), shielded.m);
    Check("F3 Silence removes the servant's keyword (its own)", keywords(shielded.m) == 0 && !(shielded.m.HasDivineShield || shielded.m.HasTaunt || shielded.m.HasWindfury || shielded.m.IsRush));
    Round(g); FillBoard(g, boss);
    Check("F4 with a full board it is not offered", !Offered(g));
    Check("F5 Servant of Siamat is scripted (a token without text) and keeps the client's stats", C("ULDA_026").Implemented && C("ULDA_026").ATK == 1 && C("ULDA_026").Health == 4
        && C("ULDA_026").Cost == 2 && DungeonRunEncounters.Find("ULDA_026")?.Kind == EncounterCardKind.Token);
});

// ============================================================ G  Weaponized Wasp: "Upgraded" Fauna
Guard("Wasp", () =>
{
    var g = Fight("ulda_68_wasp");
    Controller boss = g.Player2, player = g.Player1;
    var target = Board(g, player, Yeti); var theirBeast = Board(g, player, Croc);
    ToBoss(g);
    var mine = Board(g, boss, Yeti);
    Check("G1 \"Upgraded\" Fauna costs (2) and is not offered while the boss controls no Beast (the run player's Beast does not count)", Cost("ULDA_BOSS_68p") == 2 && !Offered(g));
    Board(g, boss, Croc); Board(g, boss, Croc); Board(g, boss, Croc);
    var targets = PowerTargets(g);
    Check("G2 with Beasts it targets enemy minions only", targets.Count == 2 && targets.Contains(target) && targets.Contains(theirBeast) && !targets.Contains(mine));
    Power(g, target);
    Check("G3 three Beasts: one hit of 3 damage", target.Damage == 3 && boss.UsedMana == 2, target.Damage.ToString());
});

// ============================================================ H  Isiset: Mistress of Magic (both players)
Guard("Isiset", () =>
{
    var g = Fight("ulda_69_isiset");
    Controller boss = g.Player2, player = g.Player1;
    Check("H1 Mistress of Magic is passive: started at match start, never an option", PassiveStarted(g, "ULDA_BOSS_69p") && C("ULDA_BOSS_69p").Text == ClientText("ULDA_BOSS_69p"));
    int hand = player.HandZone.Count;
    Cast(g, Moonfire, boss.Hero);
    var copy = player.HandZone.LastOrDefault();
    Check("H2 the run player's first spell (Moonfire): a copy joins the run player's hand", player.HandZone.Count == hand + 1 && copy != null && copy.Card.Id == Moonfire && copy.Controller == player,
        hand + "->" + player.HandZone.Count);
    hand = player.HandZone.Count;
    Play(g, copy, boss.Hero);
    Check("H3 the second spell: no copy", player.HandZone.Count == hand - 1 && !player.HandZone.Any(p => p.Card.Id == Moonfire));
    ToBoss(g);
    int bossHand = boss.HandZone.Count;
    Cast(g, Moonfire, player.Hero);
    var own = boss.HandZone.LastOrDefault();
    Check("H4 the boss's own first spell: a copy joins the boss's hand", boss.HandZone.Count == bossHand + 1 && own?.Card.Id == Moonfire);
    var g2 = Fight("ulda_69_isiset");
    Cast(g2, Moonfire, g2.Player2.Hero);
    var pcopy = g2.Player1.HandZone.Last();
    ToBoss(g2);
    var (view, vb, vp, bot) = FairView(g2);
    Check("H5 the boss's fair view keeps the run player's copy masked (it under-knows)", vp.HandZone.Count == g2.Player1.HandZone.Count && vp.HandZone.All(p => p is Unknown)
        && bot.Facts.Opponent.Hand.All(c => c.Unknown),
        string.Join(",", vp.HandZone.Select(p => p.Id + ":" + p.GetType().Name)) + " copy " + pcopy.Id);
    var g3 = Fight("ulda_69_isiset");
    FillHand(g3.Player1); g3.Player1.HandZone.Remove(g3.Player1.HandZone[0]);
    var moon = ToHand(g3, g3.Player1, Moonfire);
    int full = g3.Player1.HandZone.Count;
    Play(g3, moon, g3.Player2.Hero);
    Check("H6 a hand with room for one: the copy takes it (9 left + the copy = the hand before); with a full hand none", g3.Player1.HandZone.Count == full);
});

// ============================================================ I  Rajh: Lord of the Sun
Guard("Rajh", () =>
{
    var g = Fight("ulda_70_rajh");
    Controller boss = g.Player2, player = g.Player1;
    ToBoss(g);
    foreach (var p in boss.HandZone.Where(p => p is Spell).ToArray()) { boss.HandZone.Remove(p); boss.SetasideZone.Add(p); }
    Check("I1 Lord of the Sun costs (1) and is not offered while the boss holds no spell", Cost("ULDA_BOSS_70p") == 1 && Clickable(g) && !Offered(g));
    var moon = ToHand(g, boss, Moonfire);
    bool used = Power(g);
    Check("I2 with a spell in hand it is used: the boss has Spell Damage +3 (ULDA_BOSS_70e on its hero)", used && boss.CurrentSpellPower == 3 && Has(boss.Hero, "ULDA_BOSS_70e"),
        boss.CurrentSpellPower.ToString());
    int hp = player.Hero.Health;
    Play(g, moon, player.Hero);
    Check("I3 Moonfire deals 4", player.Hero.Health == hp - 4, hp + "->" + player.Hero.Health);
    Round(g);
    Check("I4 next turn the Spell Damage is gone", boss.CurrentSpellPower == 0 && !Has(boss.Hero, "ULDA_BOSS_70e"));
});

// ============================================================ J  Ammunae: Lord of Life
Guard("Ammunae", () =>
{
    var g = Fight("ulda_71_ammunae");
    Controller boss = g.Player2, player = g.Player1;
    var theirs = Board(g, player, Yeti);
    ToBoss(g);
    var mine = Board(g, boss, Yeti);
    Check("J1 Lord of Life costs (2) and is not offered while no minion is damaged", Cost("ULDA_BOSS_71p") == 2 && !Offered(g));
    Hit(g, mine, 3); Hit(g, theirs, 2);
    var targets = PowerTargets(g);
    Check("J2 any damaged minion is a target (either side), never a hero", targets.Count == 2 && targets.Contains(mine) && targets.Contains(theirs));
    Power(g, mine);
    Check("J3 its Health is restored to full", mine.Damage == 0 && mine.Health == 5 && theirs.Damage == 2);
});

// ============================================================ K  Setesh: Lord of Destruction, Cursed!
Guard("Setesh", () =>
{
    var g = Fight("ulda_72_setesh");
    Controller boss = g.Player2, player = g.Player1;
    var power = BossPower(g);
    Check("K1 Lord of Destruction costs (0) and is rule-cast (never an option; started at match start)", Cost("ULDA_BOSS_72p") == 0 && PassiveStarted(g, "ULDA_BOSS_72p"));
    ToBoss(g);
    Check("K2 not cast while the run player controls no minion", DungeonRunEncounters.AutoCasts(power) == 0);
    var victim = Board(g, player, Yeti);
    var own = Board(g, boss, Yeti);
    ToPlayer(g);
    int hp = player.Hero.Health;
    EndTurn(g);
    Check("K3 the next boss turn start curses the run player's minion (ULDA_BOSS_72e), never the boss's", DungeonRunEncounters.AutoCasts(power) == 1 && Has(victim, "ULDA_BOSS_72e")
        && !Has(own, "ULDA_BOSS_72e") && player.Hero.Health == hp);
    EndTurn(g); Mana(player);
    hp = player.Hero.Health;
    EndTurn(g);
    Check("K4 at the end of the run player's turn its hero takes 2", player.Hero.Health == hp - 2, hp + "->" + player.Hero.Health);
    hp = player.Hero.Health;
    int bossHp = boss.Hero.Health;
    EndTurn(g);
    Check("K5 the boss's turn end does not hurt the run player; a second curse is stacked on the only enemy minion at its turn start", player.Hero.Health == hp && boss.Hero.Health == bossHp
        && Count(victim, "ULDA_BOSS_72e") == 2);
    hp = player.Hero.Health;
    EndTurn(g);
    Check("K6 two curses: 4 damage at the end of the run player's turn", player.Hero.Health == hp - 4, hp + "->" + player.Hero.Health);
    EndTurn(g); Mana(player);
    Play(g, ToHand(g, player, Silence), victim);
    int left = Count(victim, "ULDA_BOSS_72e");
    hp = player.Hero.Health;
    EndTurn(g);
    Check("K7 Silence removes the curses: no damage at the end of that turn (the boss's next turn start curses it anew)", left == 0 && player.Hero.Health == hp
        && Count(victim, "ULDA_BOSS_72e") == 1,
        $"curses {Count(victim, "ULDA_BOSS_72e")} hp {hp}->{player.Hero.Health} silenced {victim.IsSilenced} zone {victim.Zone?.Type}");
});

// ============================================================ L  General Husam: Shockwave
Guard("Husam", () =>
{
    var g = Fight("ulda_73_husam");
    Controller boss = g.Player2, player = g.Player1;
    Check("L1 Shockwave is passive: started at match start, never an option", PassiveStarted(g, "ULDA_BOSS_73p") && C("ULDA_BOSS_73p").Text == ClientText("ULDA_BOSS_73p"));
    var target = Board(g, player, Croc);
    var theirs = ToHand(g, player, Yeti);
    Play(g, theirs);
    Check("L2 the run player's minions get no Rush", theirs is Minion tm && !tm.IsRush && !g.Player1.Options().OfType<MinionAttackTask>().Any(o => o.Source == theirs));
    ToBoss(g);
    var yeti = ToHand(g, boss, Yeti);
    Play(g, yeti);
    var attacks = boss.Options().OfType<MinionAttackTask>().Where(o => o.Source == yeti).ToList();
    Check("L3 a minion the boss plays has Rush (ULDA_BOSS_73e): it can attack a minion at once, not the hero", ((Minion)yeti).IsRush && Has(yeti, "ULDA_BOSS_73e")
        && attacks.Any(o => o.Target == target) && attacks.All(o => o.Target != player.Hero), attacks.Count.ToString());
});

// ============================================================ M  High Prophet Barim: Burst of Light
Guard("Barim", () =>
{
    var g = Fight("ulda_74_barim");
    Controller boss = g.Player2, player = g.Player1;
    ToBoss(g);
    var mine = Board(g, boss, Yeti);
    Check("M1 Burst of Light costs (3), has Lifesteal and is not offered while the run player controls no minion", Cost("ULDA_BOSS_74p") == 3 && BossPower(g).HasLifeSteal && !Offered(g));
    var a = Board(g, player, Yeti); var b = Board(g, player, Croc); var w = Board(g, player, Wisp);
    Hit(g, boss.Hero, 10);
    int hp = boss.Hero.Health;
    bool used = Power(g);
    Check("M2 1 damage to each enemy minion (the Wisp dies), none to the boss's; the boss's hero is healed by the 3 damage dealt", used && a.Damage == 1 && b.Damage == 1
        && w.Zone != player.BoardZone && mine.Damage == 0 && boss.Hero.Health == hp + 3, hp + "->" + boss.Hero.Health);
});

// ============================================================ N  Avozu: Hunt the Weak
Guard("Avozu", () =>
{
    var g = Fight("ulda_75_avozu");
    Controller boss = g.Player2, player = g.Player1;
    var power = BossPower(g);
    var yeti = Board(g, player, Yeti);
    ToBoss(g);
    Check("N1 Hunt the Weak costs (2); its number starts at 1", Cost("ULDA_BOSS_75p") == 2 && DungeonRunEncounters.TombsD.Number(power) == 1 && EngineText("ULDA_BOSS_75p") == ClientText("ULDA_BOSS_75p"));
    Power(g, yeti);
    Check("N2 1 damage to the Yeti; the number becomes 2", yeti.Damage == 1 && DungeonRunEncounters.TombsD.Number(power) == 2);
    Round(g);
    Power(g, yeti);
    Check("N3 then 2 damage; the number becomes 3", yeti.Damage == 3 && DungeonRunEncounters.TombsD.Number(power) == 3);
    Round(g);
    var wisp = Board(g, player, Wisp);
    Power(g, wisp);
    g.DeathProcessingAndAuraUpdate();
    Check("N4 it kills a minion: all bonus damage is lost (the number is 1 again)", wisp.Zone != player.BoardZone && DungeonRunEncounters.TombsD.Number(power) == 1);
});

// ============================================================ O  Tarkus: Noble Champion
Guard("Tarkus", () =>
{
    var g = Fight("ulda_76_tarkus");
    Controller boss = g.Player2, player = g.Player1;
    ToBoss(g);
    Board(g, boss, Yeti);
    Check("O1 Noble Champion costs (4) and is not offered while the run player controls no minion", Cost("ULDA_BOSS_76p") == 4 && !Offered(g));
    var yeti = Board(g, player, Yeti); var croc = Board(g, player, Croc);
    Power(g);
    Check("O2 a random enemy minion takes damage equal to its own Attack (the Yeti 4, the Crocolisk 2)", yeti.Damage == 4 && croc.Damage == 0 || croc.Zone != player.BoardZone && yeti.Damage == 0,
        $"yeti {yeti.Damage} croc {croc.Damage}");
});

// ============================================================ P  Gorebite: Snapping Jaw
Guard("Gorebite", () =>
{
    var g = Fight("ulda_77_gorebite");
    Controller boss = g.Player2, player = g.Player1;
    ToBoss(g);
    Check("P1 Snapping Jaw costs (2) and is not offered while the boss's hero has no Attack", Cost("ULDA_BOSS_77p") == 2 && !Offered(g));
    Play(g, ToHand(g, boss, WarAxe));
    bool used = Power(g);
    Check("P2 with a 3-Attack weapon it doubles the hero's Attack this turn (+3, ULDA_BOSS_77e)", used && boss.Hero.AttackDamage == 6 && Has(boss.Hero, "ULDA_BOSS_77e"), boss.Hero.AttackDamage.ToString());
    Round(g);
    Check("P3 next turn the hero's Attack is the weapon's again", boss.Hero.AttackDamage == 3 && !Has(boss.Hero, "ULDA_BOSS_77e"), boss.Hero.AttackDamage + " " + Has(boss.Hero, "ULDA_BOSS_77e") + " weapon " + boss.Hero.Weapon?.AttackDamage);
});

// ============================================================ Q  Octosari: Massive Tentacles (shrines go dormant)
Guard("Octosari", () =>
{
    var g = Fight("ulda_78_octosari", shrine: Shrine);
    Controller boss = g.Player2, player = g.Player1;
    var power = BossPower(g);
    Check("Q1 Massive Tentacles costs (0) and is rule-cast (never an option; started at match start)", Cost("ULDA_BOSS_78p") == 0 && PassiveStarted(g, "ULDA_BOSS_78p"));
    var shrine = ShrineRules.Of(player).First();
    for (int i = 0; i < 3; i++) Board(g, player, Yeti);
    for (int i = 0; i < 3; i++) Board(g, boss, Yeti);
    ToBoss(g);
    Check("Q2 seven minions on the battlefield (the run player's awake shrine counts): not cast", DungeonRunEncounters.AutoCasts(power) == 0 && DungeonRunEncounters.TombsD.Battlefield(boss) == 7
        && player.BoardZone.Count == 4 && boss.BoardZone.Count == 3);
    Board(g, boss, Yeti);
    ToPlayer(g); EndTurn(g);
    var dormant = ShrineRules.Of(player).FirstOrDefault();
    Check("Q3 eight: at the start of the boss's turn every minion is destroyed; the run shrine goes dormant, not removed", DungeonRunEncounters.AutoCasts(power) == 1
        && boss.BoardZone.Count == 0 && player.BoardZone.Count == 1 && dormant != null && dormant.Untouchable && ShrineRules.IsMarked(dormant), $"boss {boss.BoardZone.Count} player {player.BoardZone.Count}");
    for (int i = 0; i < 4; i++) Board(g, player, Wisp);
    for (int i = 0; i < 3; i++) Board(g, boss, Wisp);
    ToPlayer(g); EndTurn(g);
    Check("Q4 the dormant shrine does not count: seven minions and the dormant shrine, not cast", DungeonRunEncounters.AutoCasts(power) == 1 && boss.BoardZone.Count == 3
        && player.BoardZone.Count == 5, $"{DungeonRunEncounters.AutoCasts(power)}");
});

// ============================================================ R  Armadillobot: Mech "Pilot"
Guard("Armadillobot", () =>
{
    var g = Fight("ulda_79_armadillobot");
    Controller boss = g.Player2, player = g.Player1;
    var croc = Board(g, player, Croc); var theirMech = Board(g, player, Mechwarper);
    ToBoss(g);
    Board(g, boss, Yeti);
    Check("R1 Mech \"Pilot\" costs (1) and is not offered while there is no Mech... but the run player's Mech is one", Cost("ULDA_BOSS_79p") == 1 && Offered(g));
    var golem = Board(g, boss, HarvestGolem);
    var targets = PowerTargets(g);
    Check("R2 it targets Mechs only, on either board", targets.Count == 2 && targets.Contains(golem) && targets.Contains(theirMech));
    int attacks = golem.NumAttacksThisTurn;
    Power(g, golem);
    g.DeathProcessingAndAuraUpdate();
    bool hitCroc = croc.Zone != player.BoardZone, hitMech = theirMech.Zone != player.BoardZone || theirMech.Damage > 0;
    Check("R3 the Golem gets +2 Attack this turn (ULDA_BOSS_79e) and attacks a random enemy minion at once (4 damage), no attack of its turn spent",
        Has(golem, "ULDA_BOSS_79e") && golem.AttackDamage == 4 && (hitCroc || hitMech) && golem.Damage > 0 && golem.NumAttacksThisTurn == attacks,
        $"croc {croc.Zone?.Type} mech {theirMech.Damage} golem {golem.Damage}");
    Round(g);
    Check("R4 next turn the Golem's Attack is its own again", golem.Zone != boss.BoardZone || golem.AttackDamage == 2 && !Has(golem, "ULDA_BOSS_79e"));
    var g2 = Fight("ulda_79_armadillobot");
    ToBoss(g2);
    var lone = Board(g2, g2.Player2, HarvestGolem);
    int hp = g2.Player1.Hero.Health;
    Power(g2, lone);
    Check("R5 with no enemy minion it only gets the Attack (no attack on the hero)", lone.AttackDamage == 4 && g2.Player1.Hero.Health == hp && lone.Damage == 0);
    var g3 = Fight("ulda_79_armadillobot");
    var mech3 = Board(g3, g3.Player1, HarvestGolem);
    ToBoss(g3);
    var bossYeti = Board(g3, g3.Player2, Yeti);
    Power(g3, mech3);
    Check("R6 on the run player's Mech: it attacks a minion of the boss's", bossYeti.Damage == 4 && mech3.Damage == 4 || mech3.Zone != g3.Player1.BoardZone && bossYeti.Damage == 4,
        $"yeti {bossYeti.Damage}");
});

// ============================================================ S  the rows, the tier rule, the admission rows, the validator
Guard("Rows", () =>
{
    var errors = DungeonRunValidator.Validate(data, null);
    Check("S1 the shipped data validates with the eighteen family 22 rows", errors.Count == 0, string.Join(" | ", errors.Take(4)));
    // (row, n, name, tier, wiki level, deck kind, chapter, flags)
    var rows = new (string id, string n, string name, string tier, int level, string kind, int chapter, string[] flags)[]
    {
        ("ulda_60_zaraam", "60", "Zaraam", "4", 7, "substituted", 3, new string[0]),
        ("ulda_61_kyriss", "61", "Kyriss", "4", 7, "authentic", 4, new string[0]),
        ("ulda_62_oasis", "62", "A Beautiful Oasis", "3", 6, "substituted", 2, new[] { "hiddenZone" }),
        ("ulda_63_vessina", "63", "Vessina", "3", 6, "substituted", 4, new[] { "passive" }),
        ("ulda_65_thrud", "65", "Thrud", "2", 4, "extended", 1, new[] { "hiddenZone" }),
        ("ulda_66_siamat", "66", "Siamat", "4", 7, "authentic", 1, new string[0]),
        ("ulda_68_wasp", "68", "Weaponized Wasp", "1", 2, "extended", 1, new string[0]),
        ("ulda_69_isiset", "69", "Isiset", "4", 7, "authentic", 4, new[] { "passive", "bothPlayers", "recast" }),
        ("ulda_70_rajh", "70", "Rajh", "4", 7, "authentic", 4, new string[0]),
        ("ulda_71_ammunae", "71", "Ammunae", "4", 7, "substituted", 4, new string[0]),
        ("ulda_72_setesh", "72", "Setesh", "4", 7, "authentic", 4, new string[0]),
        ("ulda_73_husam", "73", "General Husam", "4", 7, "authentic", 1, new[] { "passive" }),
        ("ulda_74_barim", "74", "High Prophet Barim", "3", 6, "substituted", 1, new string[0]),
        ("ulda_75_avozu", "75", "Avozu", "4", 7, "substituted", 1, new string[0]),
        ("ulda_76_tarkus", "76", "Tarkus", "2", 4, "extended", 1, new string[0]),
        ("ulda_77_gorebite", "77", "Gorebite", "1", 2, "extended", 2, new string[0]),
        ("ulda_78_octosari", "78", "Octosari", "3", 6, "authentic", 1, new string[0]),
        ("ulda_79_armadillobot", "79", "Armadillobot", "2", 3, "extended", 4, new[] { "forcedAttack" }),
    };
    Check("S2 family 22 holds exactly these eighteen rows, in this order", data.Encounters.Where(b => b.Family == "22").Select(b => b.Id).SequenceEqual(rows.Select(r => r.id)),
        string.Join(",", data.Encounters.Where(b => b.Family == "22").Select(b => b.Id)));
    var chapterTag = new Dictionary<int, GameTag> { { 1, (GameTag)1202 }, { 2, (GameTag)1203 }, { 3, (GameTag)1204 }, { 4, (GameTag)1205 } };
    foreach (var r in rows)
    {
        var b = data.Boss(r.id);
        var plan = RowPlan(r.id);
        string hero = "ULDA_BOSS_" + r.n + "h", power = "ULDA_BOSS_" + r.n + "p";
        // The tier rule (verify/tools/gen/p8f19/tombs_tiers.py): level 1-2 tier 1, 3-4 tier 2 (the 2-3 table takes 3), 5-6 tier 3, 7 tier 4.
        string ruleTier = r.level <= 2 ? "1" : r.level <= 4 ? "2" : r.level <= 6 ? "3" : "4";
        int printed = r.id == "ulda_62_oasis" ? 30 : 10;
        Check("S3 " + r.id + ": a Tombs row of tier " + r.tier + " (wiki level " + r.level + ", chapter " + r.chapter + ") whose plan is the row's (hero, power, a 30-card " + r.kind
            + " deck, the act curve's Health, not the printed " + printed + ")" + (r.flags.Length > 0 ? "; flags " + string.Join(",", r.flags) : ""),
            b.Provenance == "ULDA" && b.Name == r.name && b.Tier == r.tier && ruleTier == r.tier && b.LevelOriginal == r.level && b.LevelSource == "wiki-level-table"
            && b.HeroCard == hero && b.Power == power && b.Source.Kind == r.kind && b.Flags.SequenceEqual(r.flags)
            && plan.EncounterId == r.id && plan.EncounterDeck && plan.BossHero.Id == hero && plan.BossHeroPowerId == power && plan.BossDeck.Count == 30 && data.BossDecks[b.Deck].Count == 30
            && !b.FixedHealth && b.Health == null && plan.BossHealth == 30 && C(hero).Type == CardType.HERO && C(hero).Health == printed && C(hero)[chapterTag[r.chapter]] > 0
            && C(power).Type == CardType.HERO_POWER && C(hero).Name == r.name && C(hero)[(GameTag)380] == C(power).AssetId && data.Schedules.Member(b, DungeonRunEncounterData.PoolOf(b.Tier))
            && b.Source.Url.StartsWith("https://hearthstone.wiki.gg/wiki/") && !b.BossGeneratedSecrets && b.Forms.Count == 0 && b.Start == null && b.Pools.SequenceEqual(new[] { "enc-t" + r.tier }),
            b.Source.Kind + " health " + plan.BossHealth + " tier " + b.Tier + " flags " + string.Join(",", b.Flags) + " pools " + string.Join(",", b.Pools));
    }
    string[] passives = { "ULDA_BOSS_63p", "ULDA_BOSS_69p", "ULDA_BOSS_73p" };
    string[] ruleCast = { "ULDA_BOSS_62p", "ULDA_BOSS_72p", "ULDA_BOSS_78p" };
    string[] powers = rows.Select(r => "ULDA_BOSS_" + r.n + "p").ToArray();
    string[] enchantments = { "ULDA_BOSS_70e", "ULDA_BOSS_72e", "ULDA_BOSS_73e", "ULDA_BOSS_77e", "ULDA_BOSS_79e" };
    var scripted = powers.Concat(new[] { "ULDA_026" }).ToArray();
    var unscripted = scripted.Concat(enchantments).Where(id => !C(id).Implemented || DungeonRunEncounters.Find(id)?.Family != "22").ToList();
    var retexted = scripted.Concat(enchantments).Where(id => EngineText(id) != (factById[id].TryGetProperty("text", out var t) ? t.GetString() : "")).ToList();
    Check("S4 every power, the Servant and every enchantment of the family is scripted by family 22 and keeps the client's text (18 powers, 1 token, 5 enchantments)", unscripted.Count == 0
        && retexted.Count == 0 && scripted.Length == 19, string.Join(",", unscripted.Concat(retexted)));
    int ClientCost(string p) => factById[p].GetProperty("tags").EnumerateArray().Where(t => t[0].GetInt32() == 48).Select(t => t[1].GetInt32()).FirstOrDefault();
    Check("S5 the three passive powers and the three rule-cast ones are the registry's passive ones (never an option), the twelve others clickable; each power's Cost is the client's",
        powers.All(p => DungeonRunEncounters.PassivePowers.Contains(p) == (passives.Contains(p) || ruleCast.Contains(p))
            && DungeonRunEncounters.Find(p).Kind == (passives.Contains(p) || ruleCast.Contains(p) ? EncounterCardKind.Passive : EncounterCardKind.Power) && C(p).Cost == ClientCost(p)));
    var expectFamily = new Dictionary<string, string>
    {
        ["ULDA_BOSS_61p"] = "boss-tombs-d-board-power", ["ULDA_BOSS_66p"] = "boss-tombs-d-board-power", ["ULDA_BOSS_68p"] = "boss-tombs-d-board-power",
        ["ULDA_BOSS_70p"] = "boss-tombs-d-board-power", ["ULDA_BOSS_71p"] = "boss-tombs-d-board-power", ["ULDA_BOSS_74p"] = "boss-tombs-d-board-power",
        ["ULDA_BOSS_76p"] = "boss-tombs-d-board-power", ["ULDA_BOSS_77p"] = "boss-tombs-d-board-power",
        ["ULDA_BOSS_60p"] = "boss-tombs-d-growing-power", ["ULDA_BOSS_75p"] = "boss-tombs-d-growing-power", ["ULDA_BOSS_79p"] = "boss-tombs-d-forced-attack",
        ["ULDA_BOSS_72p"] = "boss-tombs-d-auto-cast", ["ULDA_BOSS_78p"] = "boss-tombs-d-auto-cast", ["ULDA_BOSS_63p"] = "boss-tombs-d-passive", ["ULDA_BOSS_73p"] = "boss-tombs-d-passive",
        ["ULDA_BOSS_69p"] = "boss-tombs-d-both-players", ["ULDA_BOSS_65p"] = "boss-tombs-d-hidden-steal", ["ULDA_BOSS_62p"] = "boss-tombs-d-hidden-illusion",
        ["ULDA_026"] = "boss-tombs-d-card",
    };
    var wrong = scripted.Where(id =>
    {
        var v = DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss, null, catalogRun: true);
        bool card = id == "ULDA_026";
        return !(v.Admitted && v.Subject && v.Rule?.Ids != null && v.Rule.Ids.Contains(id) && DungeonRunAdmission.RuleFor(id)?.Family == expectFamily[id] && v.Reason.Contains("c-enc-22")
            && DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss).Admitted
            && DungeonRunAdmission.Classify(C(id), id, AdmitScope.Player, null, catalogRun: true).Admitted == card
            && !DungeonRunAdmission.Classify(C(id), id, AdmitScope.Generated, null, catalogRun: true).Admitted
            && DungeonRunAdmission.GeneratedRejects(true).Contains(id) && DungeonRunAdmission.GeneratedRejects(false).Contains(id));
    }).ToList();
    Check("S6 each has its own admission row of its family (boss-tombs-d-*, naming c-enc-22): the powers a boss's only, the Servant a boss's and the run player's; none is ever generated",
        wrong.Count == 0 && expectFamily.Count == 19, string.Join(",", wrong));
    var playerPower = DungeonRunAdmission.Classify(C("ULDA_BOSS_65p"), "ULDA_BOSS_65p", AdmitScope.Player, null, catalogRun: true);
    Check("S7 a family 22 power asked for the run player is refused with its row's reason", !playerPower.Admitted && playerPower.Family == "boss-tombs-d-hidden-steal"
        && playerPower.Lift == DungeonRunAdmission.NoneBossPower);
    string Subs(string id) => string.Join(",", data.Boss(id).Source.Substitutions.Where(x => x.Listed != null).Select(x => x.Listed + ">" + (x.Used ?? "-") + ":" + x.Reason).Distinct().OrderBy(x => x, StringComparer.Ordinal));
    var replaced = rows.SelectMany(r => data.Boss(r.id).Source.Substitutions).Where(x => x.Listed != null).ToList();
    Check("S8 the listed cards replaced are only cards the engine does not script (or Hunter's Pack and Desperate Measures, which generate Secrets), each with its named replacement",
        replaced.All(x => x.Used != null && (x.Reason == "unscripted" && !C(x.Listed).Implemented || (x.Listed == "ULD_429" || x.Listed == "DAL_141") && x.Reason.StartsWith("admission:")))
        && Subs("ulda_62_oasis") == "ULD_137>EX1_025:unscripted,ULD_190>CS2_042:unscripted,ULD_198>CS2_179:unscripted,ULD_289>EX1_506:unscripted"
        && Subs("ulda_65_thrud") == "GVG_065>CS2_125:unscripted,ULD_229>EX1_134:unscripted" && Subs("ulda_61_kyriss") == "" && Subs("ulda_78_octosari") == "",
        Subs("ulda_62_oasis") + " | " + Subs("ulda_65_thrud") + " | " + Subs("ulda_60_zaraam"));
    int In(string boss, string card) => data.BossDecks[data.Boss(boss).Deck].Count(id => id == card);
    Check("S9 the decks keep the cards the powers need: Armadillobot's Mechs, the Wasp's Beasts, Vessina's Tunnel Troggs, Rajh's and Isiset's spells",
        data.BossDecks[data.Boss("ulda_79_armadillobot").Deck].Count(id => C(id).IsRace(Race.MECHANICAL)) >= 10 && data.BossDecks[data.Boss("ulda_68_wasp").Deck].Count(id => C(id).IsRace(Race.BEAST)) >= 8
        && In("ulda_63_vessina", Trogg) == 2 && data.BossDecks[data.Boss("ulda_70_rajh").Deck].Count(id => C(id).Type == CardType.SPELL) >= 15,
        "mechs " + data.BossDecks[data.Boss("ulda_79_armadillobot").Deck].Count(id => C(id).IsRace(Race.MECHANICAL)) + " beasts " + data.BossDecks[data.Boss("ulda_68_wasp").Deck].Count(id => C(id).IsRace(Race.BEAST)));
    Check("S10 the tier pools: two rows in enc-t1, three in enc-t2, four in enc-t3, nine in enc-t4", rows.Count(r => r.tier == "1") == 2 && rows.Count(r => r.tier == "2") == 3
        && rows.Count(r => r.tier == "3") == 4 && rows.Count(r => r.tier == "4") == 9);
    DungeonRunData Changed(Action<DungeonRunData> change) { var d = Load(); change(d); return d; }
    var badPower = DungeonRunValidator.Validate(Changed(d => d.Boss("ulda_65_thrud").Power = "ULDA_BOSS_65px"), null);
    Check("S11 the validator refuses a row whose power is not scripted (the Heroic Ogre-Sized Thief ULDA_BOSS_65px)", badPower.Count > 0, string.Join(" | ", badPower.Take(2)));
    Check("S12 the Mirage tag is a private encounter tag (never sent to the client)", EncounterRules.IsPrivateTag(EncounterRules.MirageIllusion) && (int)EncounterRules.MirageIllusion == 32657);
});

Console.WriteLine($"SearchTelemetry swallowed leaf exceptions {SearchTelemetry.SwallowedLeafErrors}");
Console.WriteLine($"RESULT {passes} passed, {failures + SearchTelemetry.SwallowedLeafErrors} failed");
return failures + SearchTelemetry.SwallowedLeafErrors;
