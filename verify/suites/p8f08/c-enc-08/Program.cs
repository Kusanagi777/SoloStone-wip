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

// Phase 8 family 08, Dungeon Run bosses B (real engine games through the run setup path: DungeonRun.BuildPlan on the shipped
// row -> DungeonRunSetup.Configure / ApplyAfterStart): the tag team of George and Karl (Divinity, Tag Team, the bench), the
// three both-player totems (Azun, Jin'zo, Vex), the two Gutmooks and Gluttonous Trogg, Candlebeard, Ixlid and Deadly Spore,
// Lyris, Kraxx, Seriona, Whompwhisker, Elder Jari, Jeeru, Flurgl, the Lava-Filled Chamber, Russell the Bard, the boss deck
// minions Starving Crab, Reverberating Ooze and Shroomsayer, and the rows, admission rows and validator.
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
const string Hoarder = "EX1_096", Engineer = "EX1_015", TagTeamCard = "LOOTA_BOSS_110", Shrine = "TRLA_154";
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

// ============================================================ A  George and Karl: Divinity, Tag Team, the bench
Guard("TagTeam", () =>
{
    var plan = RowPlan("loot_15_george_karl");
    Check("A1 the row's plan: George and Karl with Divinity at 30 Health (fixed), one script form (Karl and George, The Silver Hand, 30 Health), four Tag Teams in the 30-card deck",
        plan.EncounterId == "loot_15_george_karl" && plan.BossHero.Id == "LOOTA_BOSS_15h" && plan.BossHeroPowerId == "LOOTA_BOSS_15p" && plan.BossHealth == 30
        && plan.BossForms.Count == 2 && plan.BossForms[0].Hero.Id == "LOOTA_BOSS_15h" && plan.BossForms[1].Hero.Id == "LOOTA_BOSS_32h" && plan.BossForms[1].Power.Id == "AT_132_PALADIN"
        && plan.BossForms[1].Health == 30 && plan.BossForms[1].HealthAtMost == 0 && !plan.BossForms[1].OnDeath && plan.BossDeck.Count == 30 && plan.BossDeck.Count(c => c.Id == TagTeamCard) == 4
        && C(TagTeamCard).Type == CardType.SPELL && C(TagTeamCard).Cost == 0, plan.BossHealth + " " + plan.BossForms.Count);
    var g = Fight("loot_15_george_karl");
    Controller boss = g.Player2, player = g.Player1;
    var mine = Board(g, player, Yeti);
    ToBoss(g);
    bool none = !Offered(g);
    var a = Board(g, boss, Yeti); var b = Board(g, boss, Yeti);
    Check("A2 Divinity costs (2), is clickable, is not offered with no minion, and gives every minion of the boss (not the run player's) Divine Shield", boss.Hero.HeroPower.Cost == 2
        && !boss.Hero.HeroPower.IsPassiveHeroPower && none && EncounterRules.IsTagTeam(g) && Power(g) && a.HasDivineShield && b.HasDivineShield && !mine.HasDivineShield && boss.UsedMana == 2);
    // Tag Team in the same turn: the hero and the power change, the new power is fresh.
    boss.Hero.Damage = 7; boss.Hero.Armor = 3;
    bool swapped = Cast(g, TagTeamCard);
    Check("A4 Tag Team (0): Karl and George comes in with The Silver Hand at his own full 30 Health; George waits on the bench with his 7 damage; the Armor stays with the side",
        swapped && boss.Hero.Card.Id == "LOOTA_BOSS_32h" && boss.Hero.HeroPower.Card.Id == "AT_132_PALADIN" && boss.Hero.Health == 30 && boss.Hero.BaseHealth == 30 && boss.Hero.Armor == 3
        && boss[EncounterRules.FormIndex] == 1 && boss[EncounterRules.TagTeamBench] == 8 && EncounterRules.TagTeamBenchHealth(boss) == 23 && !Over(g), Hero(boss) + " bench " + boss[EncounterRules.TagTeamBench]);
    int recruitsBefore = Count(boss, "CS2_101t");
    Check("A5 the new power is fresh after the swap: The Silver Hand summons two 1/1 Recruits in the same turn", Power(g) && Count(boss, "CS2_101t") == recruitsBefore + 2);
    Round(g);
    Check("A3 Divinity is George's: as Karl the power is The Silver Hand, offered again next turn", Offered(g) && boss.Hero.HeroPower.Card.Id == "AT_132_PALADIN");
    boss.Hero.Damage = 5;
    Cast(g, TagTeamCard);
    Check("A6 a second Tag Team brings George back with the 23 Health he left with; Karl waits with his 5 damage", boss.Hero.Card.Id == "LOOTA_BOSS_15h" && boss.Hero.HeroPower.Card.Id == "LOOTA_BOSS_15p"
        && boss.Hero.Health == 23 && boss[EncounterRules.FormIndex] == 0 && boss[EncounterRules.TagTeamBench] == 6 && EncounterRules.TagTeamBenchHealth(boss) == 25, Hero(boss));
    // A clone (an AI simulation) and the boss's fair view carry the bench.
    var clone = g.Clone();
    Cast(clone, TagTeamCard);
    Check("A7 a game clone carries the bench: Tag Team in the clone brings Karl in at 25, the real game is untouched", clone.Player2.Hero.Card.Id == "LOOTA_BOSS_32h" && clone.Player2.Hero.Health == 25
        && clone.Player2[EncounterRules.TagTeamBench] == 8 && boss.Hero.Card.Id == "LOOTA_BOSS_15h" && boss.Hero.Health == 23, Hero(clone.Player2));
    ToHand(g, boss, TagTeamCard);
    var (view, viewBoss, viewPlayer) = FairView(g);
    Mana(viewBoss);
    string viewPlay = "ok";
    try { view.Process(PlayCardTask.Any(viewBoss, viewBoss.HandZone.First(p => p.Card.Id == TagTeamCard), null, -1, 0)); } catch (Exception e) { viewPlay = e.GetType().Name + ": " + e.Message; }
    Check("A8 the boss's fair view carries the bench too: Tag Team played there brings Karl in at 25", viewPlay == "ok" && viewBoss.Hero.Card.Id == "LOOTA_BOSS_32h" && viewBoss.Hero.Health == 25, viewPlay + " " + Hero(viewBoss));
    // The active hero dies: the benched one takes over. (The 3 Armor of A4 is still on the side: it is taken off first.)
    bool armorKept = boss.Hero.Armor == 3;
    boss.Hero.Armor = 0;
    boss.Hero.Damage = 29;
    ToPlayer(g);
    Cast(g, Moonfire, boss.Hero);
    Check("A9 George is defeated: Karl takes over with his own 25 Health, the fight goes on, and the bench is empty for good", !Over(g) && boss.PlayState == PlayState.PLAYING && boss.Hero.Card.Id == "LOOTA_BOSS_32h"
        && boss.Hero.Health == 25 && boss[EncounterRules.FormIndex] == 1 && boss[EncounterRules.TagTeamBench] == EncounterRules.TagTeamDefeated && EncounterRules.TagTeamBenchHealth(boss) == 0 && armorKept, Hero(boss) + " " + g.State);
    ToBoss(g);
    int hand = boss.HandZone.Count;
    bool played = Play(g, boss.HandZone.First(p => p.Card.Id == TagTeamCard));
    Check("A10 with George defeated Tag Team does nothing (the card is spent, Karl stays)", played && boss.HandZone.Count == hand - 1 && boss.Hero.Card.Id == "LOOTA_BOSS_32h" && boss.Hero.Health == 25
        && boss[EncounterRules.TagTeamBench] == EncounterRules.TagTeamDefeated);
    boss.Hero.Damage = 29;
    ToPlayer(g);
    Cast(g, Moonfire, boss.Hero);
    var end = DungeonRunSetup.Harvest(g);
    Check("A11 Karl defeated as well: the boss loses; the harvest reads form 1 after three form changes", Over(g) && player.PlayState == PlayState.WON && boss.PlayState == PlayState.LOST && end.BossRemainingHealth == 0
        && end.BossForm == 1 && end.BossFormChanges == 3, g.State + " form " + end.BossForm + " changes " + end.BossFormChanges);

    // Divinity is offered only while a minion of the boss lacks Divine Shield.
    var gd = Fight("loot_15_george_karl");
    ToBoss(gd);
    var shielded = Board(gd, gd.Player2, "EX1_008"); // Argent Squire: Divine Shield
    bool allShielded = shielded.HasDivineShield && !Offered(gd);
    Board(gd, gd.Player2, Yeti);
    Check("A2b Divinity is not offered while every minion of the boss has Divine Shield already, and is again once one lacks it", allShielded && Offered(gd));

    // George dies without a Tag Team: Karl enters at his full Health.
    var g2 = Fight("loot_15_george_karl");
    g2.Player2.Hero.Damage = 29;
    Cast(g2, Moonfire, g2.Player2.Hero);
    Check("A12 George defeated before any Tag Team: Karl takes over at his full 30 Health (\"switches to Karl on defeat\")", !Over(g2) && g2.Player2.Hero.Card.Id == "LOOTA_BOSS_32h" && g2.Player2.Hero.Health == 30
        && g2.Player2.Hero.HeroPower.Card.Id == "AT_132_PALADIN" && g2.Player2[EncounterRules.TagTeamBench] == EncounterRules.TagTeamDefeated, Hero(g2.Player2));
    g2.Player2.Hero.Damage = 29;
    Cast(g2, Moonfire, g2.Player2.Hero);
    Check("A12b then Karl: the boss loses", Over(g2) && g2.Player1.PlayState == PlayState.WON);

    // Karl dies first: George returns with his damage.
    var g3 = Fight("loot_15_george_karl");
    ToBoss(g3);
    g3.Player2.Hero.Damage = 10;
    Cast(g3, TagTeamCard);
    g3.Player2.Hero.Damage = 29;
    ToPlayer(g3);
    Cast(g3, Moonfire, g3.Player2.Hero);
    Check("A13 Karl defeated while George waits with 10 damage: George returns at 20 Health with Divinity", !Over(g3) && g3.Player2.Hero.Card.Id == "LOOTA_BOSS_15h" && g3.Player2.Hero.Health == 20
        && g3.Player2.Hero.HeroPower.Card.Id == "LOOTA_BOSS_15p" && g3.Player2[EncounterRules.FormIndex] == 0 && g3.Player2[EncounterRules.TagTeamBench] == EncounterRules.TagTeamDefeated, Hero(g3.Player2));
    // A copy of Tag Team in the run player's hands does nothing.
    string bossBefore = Hero(g3.Player2), playerBefore = Hero(g3.Player1);
    bool playerCast = Cast(g3, TagTeamCard);
    Check("A14 Tag Team cast by the run player does nothing (neither hero changes)", playerCast && Hero(g3.Player2) == bossBefore && Hero(g3.Player1) == playerBefore && !Over(g3));
});

// ============================================================ B  the totems: Azun, Jin'zo, Vex (both players)
Guard("Totems", () =>
{
    // Net hand change of playing a Novice Engineer (Battlecry: draw a card): 0 with one resolution, +1 with two.
    int EngineerNet(Game g) { var c = g.CurrentPlayer; var card = ToHand(g, c, Engineer); int h = c.HandZone.Count; Play(g, card); return c.HandZone.Count - h; }
    // Cards a dying Loot Hoarder (Deathrattle: draw a card) draws for its owner.
    int HoarderDraws(Game g, Controller c) { while (c.HandZone.Count > 6) { var old = c.HandZone[0]; c.HandZone.Remove(old); c.SetasideZone.Add(old); } var m = Board(g, c, Hoarder); int h = c.HandZone.Count; Kill(g, m); return c.HandZone.Count - h; }

    var az = Fight("loot_16_azun");
    Controller boss = az.Player2, player = az.Player1;
    Check("B1 Totem of the Dead is a passive hero power, started at match start and never offered", boss.Hero.HeroPower.Card.Id == "LOOTA_BOSS_16p" && boss.Hero.HeroPower.IsPassiveHeroPower
        && boss.Hero.HeroPower[EncounterRules.PowerActive] == 1 && DungeonRunCardsGen.PassiveHeroPowers.Contains("LOOTA_BOSS_16p") && !player.Hero.HeroPower.IsPassiveHeroPower);
    Check("B2 Azun: the run player's Deathrattle triggers twice (Loot Hoarder draws 2)", HoarderDraws(az, player) == 2);
    Board(az, player, "FP1_031");
    Check("B3 it does not stack with the run player's own Baron Rivendare (still twice)", HoarderDraws(az, player) == 2);
    Check("B4 Azun: Battlecries are not doubled (Novice Engineer draws 1)", EngineerNet(az) == 0);
    ToBoss(az);
    var cloneAz = az.Clone();
    Check("B5 Azun: the boss's Deathrattle triggers twice too (in the real game and in a game clone), and the power is not an option", HoarderDraws(cloneAz, cloneAz.Player2) == 2 && HoarderDraws(az, boss) == 2 && !Offered(az));

    var jz = Fight("loot_17_jinzo");
    boss = jz.Player2; player = jz.Player1;
    var target = Board(jz, boss, Yeti);
    Check("B6 Jin'zo: Battle Totem is passive and the run player's Battlecry triggers twice (Novice Engineer draws 2; Elven Archer deals 2)", boss.Hero.HeroPower.IsPassiveHeroPower
        && boss.Hero.HeroPower[EncounterRules.PowerActive] == 1 && EngineerNet(jz) == 1 && Cast(jz, "CS2_189", target) && target.Damage == 2, "yeti damage " + target.Damage);
    var brann = Board(jz, player, "LOE_077");
    int withBrann = EngineerNet(jz);
    Kill(jz, brann);
    Check("B7 it does not stack with the run player's Brann Bronzebeard (still twice), and Brann leaving does not end it", withBrann == 1 && EngineerNet(jz) == 1, "with Brann " + withBrann);
    Check("B8 Jin'zo: Deathrattles are not doubled (Loot Hoarder draws 1)", HoarderDraws(jz, player) == 1);
    ToBoss(jz);
    var cloneJz = jz.Clone();
    Check("B9 Jin'zo: the boss's Battlecry triggers twice, in the real game and in a game clone", EngineerNet(cloneJz) == 1 && EngineerNet(jz) == 1);

    var vx = Fight("loot_38_vex");
    boss = vx.Player2; player = vx.Player1;
    Check("B10 Vex: Totem of Chaos doubles the run player's Battlecries and Deathrattles", boss.Hero.HeroPower.Card.Id == "LOOTA_BOSS_38p" && boss.Hero.HeroPower.IsPassiveHeroPower && EngineerNet(vx) == 1 && HoarderDraws(vx, player) == 2);
    ToBoss(vx);
    Check("B11 Vex: and the boss's own", EngineerNet(vx) == 1 && HoarderDraws(vx, boss) == 2 && !Offered(vx));
});

// ============================================================ C  Gutmook: Digest Magic, Metabolized Magic, Gluttonous Trogg
Guard("Gutmook", () =>
{
    var g = Fight("loot_19_gutmook");
    Controller boss = g.Player2, player = g.Player1;
    bool passive = boss.Hero.HeroPower.Card.Id == "LOOTA_BOSS_19p" && boss.Hero.HeroPower.IsPassiveHeroPower && boss.Hero.HeroPower[EncounterRules.PowerActive] == 1 && boss.Hero.HeroPower.ActivatedTrigger != null;
    Cast(g, Intellect);
    var trogg = boss.BoardZone.FirstOrDefault(m => m.Card.Id == "LOE_018");
    Check("C1 Digest Magic is passive; the run player's spell summons one Tunnel Trogg (1/3) for the boss", passive && Count(boss, "LOE_018") == 1 && trogg.AttackDamage == 1 && trogg.Health == 3, Count(boss, "LOE_018") + " troggs");
    ToBoss(g);
    Cast(g, Intellect);
    Check("C2 the boss's own spell summons nothing, and the power is never an option", Count(boss, "LOE_018") == 1 && !Offered(g));
    ToPlayer(g);
    Cast(g, "CS2_032"); // Flamestrike: 4 damage to all enemy minions
    Check("C3 the Trogg arrives on the cast, before the spell resolves: Flamestrike kills the old one and the new one", Count(boss, "LOE_018") == 0 && boss.GraveyardZone.Count(p => p.Card.Id == "LOE_018") == 2,
        Count(boss, "LOE_018") + " on board, " + boss.GraveyardZone.Count(p => p.Card.Id == "LOE_018") + " dead");
    while (!boss.BoardZone.IsFull) Board(g, boss, Yeti);
    Cast(g, Intellect);
    Check("C4 with a full board nothing is summoned and nothing breaks", boss.BoardZone.Count == 7 && Count(boss, "LOE_018") == 0 && !Over(g));

    var g2 = Fight("loot_33_gutmook");
    boss = g2.Player2; player = g2.Player1;
    int hand = boss.HandZone.Count, deck = boss.DeckZone.Count;
    Cast(g2, Intellect);
    var drawn = boss.HandZone.Count > 0 ? boss.HandZone[boss.HandZone.Count - 1] : null;
    Check("C5 Metabolized Magic is passive; the run player's spell draws the boss one card and it costs (1)", boss.Hero.HeroPower.Card.Id == "LOOTA_BOSS_33p" && boss.Hero.HeroPower.IsPassiveHeroPower
        && boss.Hero.HeroPower[EncounterRules.PowerActive] == 1 && boss.HandZone.Count == hand + 1 && boss.DeckZone.Count == deck - 1 && drawn.Card.Id == Yeti && drawn.Cost == 1
        && boss.HandZone.Count(p => p.Cost == 1) == 1, "hand " + hand + "->" + boss.HandZone.Count + " cost " + drawn?.Cost);
    ToBoss(g2);
    hand = boss.HandZone.Count; deck = boss.DeckZone.Count;
    Cast(g2, Moonfire, player.Hero);
    Check("C6 the boss's own spell draws nothing", boss.HandZone.Count == hand && boss.DeckZone.Count == deck);
    FillHand(g2, boss);
    ToPlayer(g2);
    deck = boss.DeckZone.Count;
    Cast(g2, Intellect);
    Check("C7 with a full hand the drawn card is burned and nothing breaks", boss.HandZone.Count == 10 && boss.DeckZone.Count == deck - 1 && !Over(g2));

    var g3 = Fight("loot_19_gutmook");
    boss = g3.Player2;
    var glutton = Board(g3, boss, "LOOTA_109");
    bool printed = glutton.AttackDamage == 3 && glutton.Health == 3 && C("LOOTA_109").Cost == 3;
    Cast(g3, Moonfire, g3.Player2.Hero);
    bool grew = glutton.AttackDamage == 4 && glutton.Health == 4 && glutton.AppliedEnchantments.Any(e => e.Card.Id == "LOOTA_109e");
    ToBoss(g3);
    Cast(g3, Moonfire, g3.Player1.Hero);
    Check("C8 Gluttonous Trogg (3/3 for 3) gains +1/+1 whenever its owner's opponent casts a spell, not from its owner's spells", printed && grew && glutton.AttackDamage == 4 && glutton.Health == 4,
        glutton.AttackDamage + "/" + glutton.Health);
});

// ============================================================ D  Candlebeard: Charrrrrge!
Guard("Candlebeard", () =>
{
    var g = Fight("loot_31_candlebeard");
    Controller boss = g.Player2, player = g.Player1;
    ToBoss(g);
    var played = (Minion)ToHand(g, boss, Yeti);
    bool ok = Play(g, played);
    bool attacked = g.Process(MinionAttackTask.Any(boss, played, player.Hero));
    Check("D1 Charrrrrge! is passive; a minion the boss plays from hand gets Charge and attacks at once", boss.Hero.HeroPower.Card.Id == "LOOTA_BOSS_31p" && boss.Hero.HeroPower.IsPassiveHeroPower
        && boss.Hero.HeroPower[EncounterRules.PowerActive] == 1 && !Offered(g) && ok && played.HasCharge && attacked && player.Hero.Damage == 4, "charge " + played.HasCharge + " hero damage " + player.Hero.Damage);
    var summoned = Board(g, boss, Yeti);
    Check("D2 a summoned minion gets nothing", !summoned.HasCharge && !g.Process(MinionAttackTask.Any(boss, summoned, player.Hero)));
    ToPlayer(g);
    var theirs = (Minion)ToHand(g, player, Yeti);
    Play(g, theirs);
    Check("D3 a minion the run player plays gets nothing", theirs.Zone == player.BoardZone && !theirs.HasCharge);
});

// ============================================================ E  Ixlid: Sprouting Spore, Deadly Spore
Guard("Ixlid", () =>
{
    var plan = RowPlan("loot_36_ixlid");
    var g = Fight("loot_36_ixlid");
    Controller boss = g.Player2, player = g.Player1;
    ToBoss(g);
    bool used = Power(g);
    var spore = boss.BoardZone.FirstOrDefault(m => m.Card.Id == "LOOTA_105");
    Check("E1 Ixlid starts at his own 20 Health; Sprouting Spore costs (2) and summons a Deadly Spore, a 1/1 with Poisonous", plan.BossHealth == 20 && boss.Hero.Health == 20 && boss.Hero.HeroPower.Cost == 2
        && used && boss.UsedMana == 2 && spore != null && spore.AttackDamage == 1 && spore.Health == 1 && spore.Poisonous && spore.ActivatedTrigger != null, "health " + boss.Hero.Health);
    while (!boss.BoardZone.IsFull) Board(g, boss, Yeti);
    Round(g);
    Check("E2 with a full board the power is not offered", !Offered(g));
    Check("E3 the Spore deals damage to the run player's hero: the hero is destroyed and the boss wins", g.Process(MinionAttackTask.Any(boss, spore, player.Hero)) && Over(g) && boss.PlayState == PlayState.WON && player.PlayState == PlayState.LOST,
        g.State + " " + player.Hero.Health);

    var g2 = Fight("loot_36_ixlid");
    boss = g2.Player2; player = g2.Player1;
    ToBoss(g2);
    var s2 = Board(g2, boss, "LOOTA_105"); var s3 = Board(g2, boss, "LOOTA_105"); var s4 = Board(g2, boss, "LOOTA_105");
    var yeti = Board(g2, player, Yeti);
    Round(g2);
    // An Immune hero cannot be attacked; damage the Spore deals to it by an effect is ignored, and so is the hero rule.
    player.Hero.IsImmune = true;
    int dealt = player.Hero.TakeDamage(s2, 1);
    g2.DeathProcessingAndAuraUpdate();
    Check("E4 an Immune hero takes no damage and is not destroyed", dealt == 0 && !Over(g2) && !player.Hero.ToBeDestroyed && player.Hero.Damage == 0
        && !g2.Process(MinionAttackTask.Any(boss, s2, player.Hero)));
    player.Hero.IsImmune = false;
    Check("E5 Poisonous: the Spore destroys the minion it damages (and dies to the 4 damage it takes)", g2.Process(MinionAttackTask.Any(boss, s3, yeti)) && yeti.Zone != player.BoardZone && s3.Zone != boss.BoardZone);
    player.Hero.Armor = 5;
    Check("E6 damage Armor absorbs is still damage dealt to the hero: destroyed through 5 Armor", g2.Process(MinionAttackTask.Any(boss, s4, player.Hero)) && Over(g2) && boss.PlayState == PlayState.WON);

    var g3 = Fight("loot_36_ixlid");
    boss = g3.Player2; player = g3.Player1;
    var stolen = Board(g3, player, "LOOTA_105");
    Round(g3);
    Check("E7 a Deadly Spore in the run player's hands destroys the boss's hero the same way", g3.Process(MinionAttackTask.Any(player, stolen, boss.Hero)) && Over(g3) && player.PlayState == PlayState.WON);
    var card = C("LOOTA_105");
    Check("E8 the card: Deadly Spore, a scripted 1/1 for (1) with the Poisonous tag and the client's text", card.Implemented && card.Type == CardType.MINION && card.Cost == 1 && card.ATK == 1 && card.Health == 1 && card.Poisonous
        && card.Text == factById["LOOTA_105"].GetProperty("text").GetString());
});

// ============================================================ F  Lyris: Arcane Infusion
Guard("Lyris", () =>
{
    var g = Fight("loot_37_lyris");
    Controller boss = g.Player2;
    ToBoss(g);
    int hand = boss.HandZone.Count;
    Check("F1 Arcane Infusion costs (1) and adds Arcane Missiles to the boss's hand", boss.Hero.HeroPower.Cost == 1 && Power(g) && boss.HandZone.Count == hand + 1 && boss.HandZone[boss.HandZone.Count - 1].Card.Id == "EX1_277" && boss.UsedMana == 1);
    var g2 = Fight("loot_37_lyris");
    ToBoss(g2);
    FillHand(g2, g2.Player2);
    Check("F2 with a full hand it is not offered", g2.Player2.HandZone.IsFull && !Offered(g2));
});

// ============================================================ G  Kraxx: Giant Stomp
Guard("Kraxx", () =>
{
    var g = Fight("loot_39_kraxx");
    Controller boss = g.Player2, player = g.Player1;
    ToBoss(g);
    bool none = !Offered(g);
    var y1 = Board(g, player, Yeti); var y2 = Board(g, player, Yeti); var own = Board(g, boss, Yeti);
    Check("G1 Giant Stomp costs (2) and deals 1 damage to every enemy minion (not the hero, not the boss's minions)", boss.Hero.HeroPower.Cost == 2 && Power(g) && y1.Damage == 1 && y2.Damage == 1 && own.Damage == 0
        && player.Hero.Damage == 0 && boss.UsedMana == 2);
    Check("G2 it is not offered while the run player has no minion", none);
});

// ============================================================ H  Seriona: Fading Light
Guard("Seriona", () =>
{
    var g = Fight("loot_40_seriona");
    Controller boss = g.Player2, player = g.Player1;
    ToBoss(g);
    var theirs = Board(g, player, Yeti); var own = Board(g, boss, Yeti); var totem = Board(g, player, "EX1_565"); // Flametongue Totem, 0 Attack
    var targets = PowerTargets(g);
    bool legal = targets.Contains(theirs) && targets.Contains(own) && !targets.Contains(totem) && !targets.Any(t => t is Hero);
    int before = theirs.AttackDamage;
    Check("H1 Fading Light costs (2) and gives a minion -1 Attack (its own enchantment)", boss.Hero.HeroPower.Cost == 2 && Power(g, theirs) && theirs.AttackDamage == before - 1
        && theirs.AppliedEnchantments.Any(e => e.Card.Id == "LOOTA_BOSS_40pe") && theirs.Health == 5 && boss.UsedMana == 2, before + "->" + theirs.AttackDamage);
    Check("H2 any minion with Attack left is a legal target (either side); a 0-Attack minion and the heroes are not", legal, string.Join(",", targets.Select(t => t.Card.Id)));
});

// ============================================================ I  Whompwhisker: Join the Fray
Guard("Whompwhisker", () =>
{
    var g = Fight("loot_41_whompwhisker", bossDeck: Yeti, playerDeck: Croc);
    Controller boss = g.Player2, player = g.Player1;
    ToBoss(g);
    var bossDeckIds = new HashSet<int>(boss.DeckZone.Select(p => p.Id)); var playerDeckIds = new HashSet<int>(player.DeckZone.Select(p => p.Id));
    int bd = boss.DeckZone.Count, pd = player.DeckZone.Count, ph = player.HandZone.Count;
    bool used = Power(g);
    Check("I1 Join the Fray costs (2): each player Recruits one minion of its own deck", boss.Hero.HeroPower.Cost == 2 && used && boss.UsedMana == 2 && boss.BoardZone.Count == 1 && player.BoardZone.Count == 1
        && boss.DeckZone.Count == bd - 1 && player.DeckZone.Count == pd - 1 && player.HandZone.Count == ph, boss.BoardZone.Count + "/" + player.BoardZone.Count);
    Check("I2 the summoned minions are the deck's own cards (the run player's becomes a public board minion; nothing else of the deck moves)", bossDeckIds.Contains(boss.BoardZone[0].Id) && boss.BoardZone[0].Card.Id == Yeti
        && playerDeckIds.Contains(player.BoardZone[0].Id) && player.BoardZone[0].Card.Id == Croc);
    var g2 = Fight("loot_41_whompwhisker", bossDeck: Yeti, playerDeck: Intellect);
    ToBoss(g2);
    int deck2 = g2.Player1.DeckZone.Count;
    Check("I3 a deck with no minion Recruits none: only the boss's minion arrives", Power(g2) && g2.Player2.BoardZone.Count == 1 && g2.Player1.BoardZone.Count == 0 && g2.Player1.DeckZone.Count == deck2);
    var g3 = Fight("loot_41_whompwhisker", bossDeck: Yeti, playerDeck: Croc);
    ToBoss(g3);
    while (!g3.Player2.BoardZone.IsFull) Board(g3, g3.Player2, Yeti);
    bool offeredOneFull = Offered(g3);
    int bd3 = g3.Player2.DeckZone.Count;
    bool used3 = Power(g3);
    bool onlyPlayer = g3.Player2.DeckZone.Count == bd3 && g3.Player1.BoardZone.Count == 1;
    while (!g3.Player1.BoardZone.IsFull) Board(g3, g3.Player1, Croc);
    Round(g3);
    Check("I4 with the boss's board full only the run player Recruits; with both boards full the power is not offered", offeredOneFull && used3 && onlyPlayer && !Offered(g3));
    // The boss's fair view: the run player's deck is hidden there, so the power takes nothing from it.
    var g4 = Fight("loot_41_whompwhisker", bossDeck: Yeti, playerDeck: Croc);
    ToBoss(g4);
    var (view, viewBoss, viewPlayer) = FairView(g4);
    Mana(viewBoss);
    int viewDeck = viewPlayer.DeckZone.Count;
    string result = "ok";
    try { view.Process(HeroPowerTask.Any(viewBoss)); } catch (Exception e) { result = e.GetType().Name + ": " + e.Message; }
    Check("I5 in the boss's fair view the run player's deck is masked: Join the Fray takes nothing from it there (the search never reads the hidden deck)", result == "ok" && !viewPlayer.DeckZone.Any(p => p is Minion)
        && viewPlayer.DeckZone.Count == viewDeck && viewPlayer.BoardZone.Count == 0, result + " deck " + viewDeck + "->" + viewPlayer.DeckZone.Count);
    var row = data.Boss("loot_41_whompwhisker");
    Check("I6 the row is flagged bothPlayers and hiddenZone", row.HasFlag("bothPlayers") && row.HasFlag("hiddenZone"));
});

// ============================================================ J  Elder Jari: Mystic Barrier
Guard("Jari", () =>
{
    var g = Fight("loot_42_jari");
    ToBoss(g);
    Check("J1 Mystic Barrier costs (1) and gains 3 Armor", g.Player2.Hero.HeroPower.Cost == 1 && Power(g) && g.Player2.Hero.Armor == 3 && g.Player2.UsedMana == 1);
});

// ============================================================ K  Jeeru: Three Wee Wishes
Guard("Jeeru", () =>
{
    var g = Fight("loot_43_jeeru");
    Controller boss = g.Player2, player = g.Player1;
    ToBoss(g);
    int bh = boss.HandZone.Count, bd = boss.DeckZone.Count, ph = player.HandZone.Count, pd = player.DeckZone.Count;
    Check("K1 Three Wee Wishes costs (1): each player draws 3 cards", boss.Hero.HeroPower.Cost == 1 && Power(g) && boss.UsedMana == 1 && boss.HandZone.Count == bh + 3 && boss.DeckZone.Count == bd - 3
        && player.HandZone.Count == ph + 3 && player.DeckZone.Count == pd - 3, $"boss {bh}->{boss.HandZone.Count}, player {ph}->{player.HandZone.Count}");
    Round(g);
    FillHand(g, player);
    pd = player.DeckZone.Count;
    Check("K2 with the run player's hand full its three cards are burned", Power(g) && player.HandZone.Count == 10 && player.DeckZone.Count == pd - 3 && !Over(g));
    var g2 = Fight("loot_43_jeeru");
    ToBoss(g2);
    EmptyDeck(g2.Player1);
    int damage = g2.Player1.Hero.Damage, bossDamage = g2.Player2.Hero.Damage;
    Check("K3 an empty deck draws fatigue: the run player takes 1 + 2 + 3, the boss draws normally", Power(g2) && g2.Player1.Hero.Damage == damage + 6 && g2.Player2.Hero.Damage == bossDamage);
    var g3 = Fight("loot_43_jeeru");
    ToBoss(g3);
    EmptyDeck(g3.Player2);
    int ph3 = g3.Player1.HandZone.Count;
    Check("K4 the boss's own empty deck: the boss takes the fatigue, the run player draws 3", Power(g3) && g3.Player2.Hero.Damage == 6 && g3.Player1.HandZone.Count == ph3 + 3);
    var g4 = Fight("loot_43_jeeru");
    ToBoss(g4);
    EmptyDeck(g4.Player2);
    g4.Player2.Hero.Damage = g4.Player2.Hero.BaseHealth - 2;
    bool used = Power(g4);
    Check("K5 fatigue that kills the boss mid-power ends the fight cleanly (the run player wins)", used && Over(g4) && g4.Player1.PlayState == PlayState.WON);
});

// ============================================================ L  Fungalmancer Flurgl: Fungal Infection
Guard("Flurgl", () =>
{
    var g = Fight("loot_46_flurgl");
    Controller boss = g.Player2, player = g.Player1;
    ToBoss(g);
    bool none = !Offered(g);
    var a = Board(g, boss, Yeti); var b = Board(g, boss, Croc); var theirs = Board(g, player, Yeti);
    Check("L1 Fungal Infection costs (2) and gives the boss's minions +1/+1 (its own enchantment), not the run player's", boss.Hero.HeroPower.Cost == 2 && Power(g) && a.AttackDamage == 5 && a.Health == 6
        && b.AttackDamage == 3 && b.Health == 4 && a.AppliedEnchantments.Any(e => e.Card.Id == "LOOTA_BOSS_46pe") && theirs.AttackDamage == 4 && theirs.Health == 5 && boss.UsedMana == 2);
    Check("L2 it is not offered while the boss has no minion", none);
});

// ============================================================ M  Lava-Filled Chamber: The Floor is Lava
Guard("Lava", () =>
{
    var g = Fight("loot_47_lava_chamber");
    Controller boss = g.Player2, player = g.Player1;
    var mine = (Minion)ToHand(g, player, Yeti);
    Play(g, mine);
    var raptor = (Minion)ToHand(g, player, Raptor);
    Play(g, raptor);
    Check("M1 The Floor is Lava is passive; a minion the run player plays takes 2 damage (a 2-Health minion dies)", boss.Hero.HeroPower.Card.Id == "LOOTA_BOSS_47p" && boss.Hero.HeroPower.IsPassiveHeroPower
        && boss.Hero.HeroPower[EncounterRules.PowerActive] == 1 && mine.Damage == 2 && mine.Zone == player.BoardZone && raptor.Zone != player.BoardZone, "yeti damage " + mine.Damage);
    var squire = (Minion)ToHand(g, player, "EX1_008");
    Play(g, squire);
    var summoned = Board(g, player, Yeti);
    Check("M2 a Divine Shield absorbs it; a summoned minion is untouched", squire.Zone == player.BoardZone && !squire.HasDivineShield && squire.Damage == 0 && summoned.Damage == 0);
    ToBoss(g);
    var bossYeti = (Minion)ToHand(g, boss, Yeti);
    Play(g, bossYeti);
    var bossSummoned = Board(g, boss, Yeti);
    Check("M3 the boss's own played minions take the 2 damage too; the power is never an option", bossYeti.Damage == 2 && bossSummoned.Damage == 0 && !Offered(g));
    int hand = boss.HandZone.Count;
    var engineer = (Minion)ToHand(g, boss, Engineer);
    Play(g, engineer);
    Check("M4 the Battlecry resolves first, then the lava (Novice Engineer draws, then dies)", boss.HandZone.Count == hand + 1 && engineer.Zone != boss.BoardZone);
});

// ============================================================ N  Russell the Bard: Alluring Tune
Guard("Russell", () =>
{
    var g = Fight("loot_51_russell", shrine: Shrine);
    Controller boss = g.Player2, player = g.Player1;
    var shrine = ShrineRules.Of(player).FirstOrDefault();
    var croc = Board(g, player, Croc); var yeti = Board(g, player, Yeti);
    ToBoss(g);
    var own = Board(g, boss, Croc);
    var targets = PowerTargets(g);
    Check("N1 Alluring Tune costs (2); its only legal targets are enemy minions with 2 or less Attack", g.Player2.Hero.HeroPower.Cost == 2 && targets.Count == 1 && targets[0] == croc, string.Join(",", targets.Select(t => t.Card.Id)));
    Check("N2 the run player's board shrine (0 Attack) is never a legal target: a shrine does not change sides", shrine != null && shrine.Card.Id == Shrine && shrine.AttackDamage == 0 && !shrine.IsDormant && !targets.Contains(shrine));
    Check("N3 the boss gains control of the minion", Power(g, croc) && croc.Controller == boss && croc.Zone == boss.BoardZone && boss.UsedMana == 2 && player.BoardZone.Contains(yeti) && own.Controller == boss);
    Round(g);
    Check("N4 with no legal target (a 4-Attack minion and the shrine) it is not offered", !Offered(g) && shrine.Controller == player && shrine.Zone == player.BoardZone);
    Kill(g, shrine);
    var croc2 = Board(g, player, Croc);
    var dormantTargets = PowerTargets(g);
    var form = ShrineRules.Of(player).FirstOrDefault();
    Check("N5 a dormant shrine is no target either", form != null && form.IsDormant && ShrineRules.IsCorpse(shrine) && dormantTargets.Count == 1 && dormantTargets[0] == croc2);
    while (!boss.BoardZone.IsFull) Board(g, boss, Yeti);
    Check("N6 with the boss's board full it is not offered", !Offered(g));
});

// ============================================================ O  boss deck minions: Starving Crab, Reverberating Ooze, Shroomsayer
Guard("BossMinions", () =>
{
    var g = Fight("loot_42_jari");
    Controller boss = g.Player2, player = g.Player1;
    var crab = Board(g, boss, "LOOTA_102");
    var crabCard = C("LOOTA_102");
    EndTurn(g);
    bool atOwnTurn = boss.Hero.Damage == 5;
    EndTurn(g);
    Check("O1 Starving Crab (a 4/1 Beast for 3): at the start of its owner's turn it deals 5 damage to its owner's hero, not at the other turn", crabCard.Cost == 3 && crabCard.ATK == 4 && crabCard.Health == 1
        && crabCard.IsRace(Race.BEAST) && atOwnTurn && boss.Hero.Damage == 5 && player.Hero.Damage == 0, "boss damage " + boss.Hero.Damage);
    Kill(g, crab);
    Board(g, player, "LOOTA_102");
    Round(g);
    Check("O2 in the run player's hands it damages the run player's hero", player.Hero.Damage == 5 && boss.Hero.Damage == 5);

    var g2 = Fight("loot_42_jari");
    boss = g2.Player2; player = g2.Player1;
    var ooze = Board(g2, boss, "LOOTA_104");
    var oozeCard = C("LOOTA_104");
    Cast(g2, Moonfire, ooze);
    var copy = boss.BoardZone.FirstOrDefault(m => m.Card.Id == "LOOTA_104" && m != ooze);
    Check("O3 Reverberating Ooze (2/4 for 4): after it survives damage a copy of it is summoned beside it, with the same damage", oozeCard.Cost == 4 && oozeCard.ATK == 2 && oozeCard.Health == 4 && Count(boss, "LOOTA_104") == 2
        && ooze.Health == 3 && copy != null && copy.Health == 3 && copy.AttackDamage == 2 && copy.ZonePosition == ooze.ZonePosition + 1, Count(boss, "LOOTA_104") + " oozes");
    Cast(g2, Moonfire, copy);
    Check("O4 the copy reverberates in turn", Count(boss, "LOOTA_104") == 3 && boss.BoardZone.Count(m => m.Card.Id == "LOOTA_104" && m.Health == 2) == 2);
    Cast(g2, Fireball, ooze);
    Check("O5 lethal damage summons nothing", Count(boss, "LOOTA_104") == 2 && ooze.Zone != boss.BoardZone);
    while (!boss.BoardZone.IsFull) Board(g2, boss, Yeti);
    Cast(g2, Moonfire, copy);
    Check("O6 with a full board there is no copy and nothing breaks", boss.BoardZone.Count == 7 && Count(boss, "LOOTA_104") == 2 && !Over(g2));

    var g3 = Fight("loot_42_jari", shrine: Shrine);
    boss = g3.Player2; player = g3.Player1;
    var shrine = ShrineRules.Of(player).First();
    var shroom = Board(g3, boss, "LOOTA_107"); var shroomCard = C("LOOTA_107");
    var bossYeti = Board(g3, boss, Yeti); var playerYeti = Board(g3, player, Yeti);
    Cast(g3, Pyroblast, shroom);
    bool lethalQuiet = shroom.Zone != boss.BoardZone && bossYeti.Zone == boss.BoardZone && playerYeti.Zone == player.BoardZone && !shrine.IsDormant;
    Check("O7 Shroomsayer (7/7 for 4): lethal damage destroys nothing else", shroomCard.Cost == 4 && shroomCard.ATK == 7 && shroomCard.Health == 7 && lethalQuiet);
    var shroom2 = Board(g3, boss, "LOOTA_107");
    Cast(g3, Moonfire, shroom2);
    Check("O8 after it survives damage all minions are destroyed, on both boards, itself included", shroom2.Zone != boss.BoardZone && bossYeti.Zone != boss.BoardZone && playerYeti.Zone != player.BoardZone
        && boss.BoardZone.Count == 0 && !player.BoardZone.Any(m => !m.IsDormant), boss.BoardZone.Count + "/" + player.BoardZone.Count);
    var form = ShrineRules.Of(player).FirstOrDefault();
    Check("O9 the run player's board shrine goes dormant instead (as from any destroy effect): its dormant form holds its place on the board", ShrineRules.IsCorpse(shrine) && form != null && form.IsDormant
        && form.Controller == player && player.BoardZone.Count == 1 && !Over(g3));
});

// ============================================================ P  the rows, the admission rows, the validator
Guard("Rows", () =>
{
    var errors = DungeonRunValidator.Validate(data, null);
    Check("P1 the shipped data validates with the seventeen family 08 rows", errors.Count == 0, string.Join(" | ", errors.Take(4)));
    var rows = new (string id, string hero, string power, string tier, int health, string kind, string[] flags)[]
    {
        ("loot_15_george_karl", "LOOTA_BOSS_15h", "LOOTA_BOSS_15p", "3", 30, "authentic", new[] { "forms" }),
        ("loot_16_azun", "LOOTA_BOSS_16h", "LOOTA_BOSS_16p", "2", 0, "authentic", new[] { "passive", "bothPlayers", "doubleDeathrattle" }),
        ("loot_17_jinzo", "LOOTA_BOSS_17h", "LOOTA_BOSS_17p", "2", 0, "authentic", new[] { "passive", "bothPlayers", "doubleBattlecry" }),
        ("loot_19_gutmook", "LOOTA_BOSS_19h", "LOOTA_BOSS_19p", "2", 0, "authentic", new[] { "passive" }),
        ("loot_33_gutmook", "LOOTA_BOSS_33h", "LOOTA_BOSS_33p", "3", 0, "authentic", new[] { "passive" }),
        ("loot_31_candlebeard", "LOOTA_BOSS_31h", "LOOTA_BOSS_31p", "3", 0, "authentic", new[] { "passive" }),
        ("loot_36_ixlid", "LOOTA_BOSS_36h", "LOOTA_BOSS_36p", "3", 20, "substituted", new string[0]),
        ("loot_37_lyris", "LOOTA_BOSS_37h", "LOOTA_BOSS_37p", "2", 0, "authentic", new string[0]),
        ("loot_38_vex", "LOOTA_BOSS_38h", "LOOTA_BOSS_38p", "3", 0, "authentic", new[] { "passive", "bothPlayers", "doubleBattlecry", "doubleDeathrattle" }),
        ("loot_39_kraxx", "LOOTA_BOSS_39h", "LOOTA_BOSS_39p", "2", 0, "authentic", new string[0]),
        ("loot_40_seriona", "LOOTA_BOSS_40h", "LOOTA_BOSS_40p", "1", 0, "authentic", new string[0]),
        ("loot_41_whompwhisker", "LOOTA_BOSS_41h", "LOOTA_BOSS_41p", "2", 0, "substituted", new[] { "bothPlayers", "hiddenZone" }),
        ("loot_42_jari", "LOOTA_BOSS_42h", "LOOTA_BOSS_42p", "2", 0, "substituted", new string[0]),
        ("loot_43_jeeru", "LOOTA_BOSS_43h", "LOOTA_BOSS_43p", "3", 0, "substituted", new[] { "bothPlayers", "hiddenZone" }),
        ("loot_46_flurgl", "LOOTA_BOSS_46h", "LOOTA_BOSS_46p", "2", 0, "authentic", new string[0]),
        ("loot_47_lava_chamber", "LOOTA_BOSS_47h", "LOOTA_BOSS_47p", "3", 0, "authentic", new[] { "passive", "bothPlayers" }),
        ("loot_51_russell", "LOOTA_BOSS_51h", "LOOTA_BOSS_51p", "2", 0, "authentic", new string[0]),
    };
    Check("P2 family 08 holds exactly these seventeen rows", data.Encounters.Where(b => b.Family == "08").Select(b => b.Id).OrderBy(i => i, StringComparer.Ordinal)
        .SequenceEqual(rows.Select(r => r.id).OrderBy(i => i, StringComparer.Ordinal)), string.Join(",", data.Encounters.Where(b => b.Family == "08").Select(b => b.Id)));
    foreach (var r in rows)
    {
        var b = data.Boss(r.id);
        var plan = RowPlan(r.id);
        var cfg = new GameConfig { RandomSeed = 5, Shuffle = true, SkipMulligan = false, History = false, Logging = false, FormatType = FormatType.FT_WILD };
        DungeonRunSetup.Configure(cfg, plan);
        bool passive = r.flags.Contains("passive");
        Check("P3 " + r.id + ": a Dungeon Run row of tier " + r.tier + " whose plan is the row's (hero, power, a 30-card " + r.kind + " deck"
            + (r.health > 0 ? ", its own " + r.health + " Health" : ", the act curve's Health") + "), flags [" + string.Join(",", r.flags) + "]",
            b.Provenance == "LOOT" && b.Tier == r.tier && b.HeroCard == r.hero && b.Power == r.power && b.Source.Kind == r.kind && b.Flags.OrderBy(f => f).SequenceEqual(r.flags.OrderBy(f => f))
            && plan.EncounterId == r.id && plan.EncounterDeck && plan.BossHero.Id == r.hero && plan.BossHeroPowerId == r.power && plan.BossDeck.Count == 30 && data.BossDecks[b.Deck].Count == 30
            && (r.health > 0 ? b.FixedHealth && b.Health == r.health && plan.BossHealth == r.health : !b.FixedHealth && b.Health == null && plan.BossHealth == 30)
            && C(r.hero).Type == CardType.HERO && C(r.power).Type == CardType.HERO_POWER && DungeonRunEncounters.PassivePowers.Contains(r.power) == passive
            && data.Schedules.Member(b, DungeonRunEncounterData.PoolOf(b.Tier)) && b.Source.Url.StartsWith("https://hearthstone.wiki.gg/wiki/") && !b.BossGeneratedSecrets,
            b.Source.Kind + " health " + plan.BossHealth + " flags " + string.Join(",", b.Flags));
    }
    string[] scripted = { "LOOTA_BOSS_15p", "LOOTA_BOSS_16p", "LOOTA_BOSS_17p", "LOOTA_BOSS_19p", "LOOTA_BOSS_31p", "LOOTA_BOSS_33p", "LOOTA_BOSS_36p", "LOOTA_BOSS_37p", "LOOTA_BOSS_38p",
        "LOOTA_BOSS_39p", "LOOTA_BOSS_40p", "LOOTA_BOSS_41p", "LOOTA_BOSS_42p", "LOOTA_BOSS_43p", "LOOTA_BOSS_46p", "LOOTA_BOSS_47p", "LOOTA_BOSS_51p", "LOOTA_BOSS_110",
        "LOOTA_102", "LOOTA_104", "LOOTA_105", "LOOTA_107", "LOOTA_109" };
    var unscripted = scripted.Where(id => !C(id).Implemented || DungeonRunEncounters.Find(id)?.Family != "08").ToList();
    var retexted = scripted.Where(id => C(id).Text != factById[id].GetProperty("text").GetString()).ToList();
    Check("P4 every power and card of the family is scripted by family 08 and keeps the client's text", unscripted.Count == 0 && retexted.Count == 0, string.Join(",", unscripted.Concat(retexted)));
    string[] minions = { "LOOTA_102", "LOOTA_104", "LOOTA_105", "LOOTA_107", "LOOTA_109" };
    var wrongScope = scripted.Where(id =>
    {
        var v = DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss, null, catalogRun: true);
        // The powers and Tag Team are a boss's only; a boss deck minion is admitted in the run player's hands too (taken from a boss).
        return !(v.Admitted && v.Subject && v.Rule?.Ids != null && v.Rule.Ids.Contains(id) && DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss).Admitted
            && DungeonRunAdmission.Classify(C(id), id, AdmitScope.Player, null, catalogRun: true).Admitted == minions.Contains(id)
            && !DungeonRunAdmission.Classify(C(id), id, AdmitScope.Generated, null, catalogRun: true).Admitted
            && DungeonRunAdmission.GeneratedRejects(true).Contains(id) && DungeonRunAdmission.GeneratedRejects(false).Contains(id));
    }).ToList();
    Check("P5 each has its own admission row: the powers and Tag Team a boss's only, the five boss deck minions a boss's and (taken from a boss) the run player's; none is ever generated", wrongScope.Count == 0, string.Join(",", wrongScope));
    Check("P6 the seven passive powers are in the passive registry (started at match start, never offered)", new[] { "LOOTA_BOSS_16p", "LOOTA_BOSS_17p", "LOOTA_BOSS_19p", "LOOTA_BOSS_31p",
        "LOOTA_BOSS_33p", "LOOTA_BOSS_38p", "LOOTA_BOSS_47p" }.All(id => DungeonRunCardsGen.PassiveHeroPowers.Contains(id) && DungeonRunEncounters.NeedsActivation(id)));
    var subs = rows.Where(r => r.kind == "substituted").ToDictionary(r => r.id, r => string.Join(",", data.Boss(r.id).Source.Substitutions.Select(x => x.Listed + ">" + x.Used).Distinct().OrderBy(x => x, StringComparer.Ordinal)));
    Check("P7 the four substituted decks: only cards the engine does not script are replaced, each recorded (Wrath, Power of the Wild, Mogor's Champion, Fel Reaver)",
        subs["loot_36_ixlid"] == "EX1_154>LOOT_051,EX1_160>CFM_614" && subs["loot_41_whompwhisker"] == "AT_088>CS2_200" && subs["loot_42_jari"] == "EX1_154>NEW1_007"
        && subs["loot_43_jeeru"] == "EX1_154>LOOT_051,GVG_016>UNG_087" && rows.Where(r => r.kind == "substituted").All(r => data.Boss(r.id).Source.Substitutions.All(x => x.Reason == "unscripted" && !C(x.Listed).Implemented)),
        string.Join(" | ", subs.Select(p => p.Key + ": " + p.Value)));
    var gk = data.Boss("loot_15_george_karl");
    Check("P8 the tag team's row: one script form (Karl and George, The Silver Hand, 30 Health), 4 Tag Teams; both Gutmooks share the wiki's one list (2 Gluttonous Troggs)", gk.Forms.Count == 1 && gk.Forms[0].Script
        && gk.Forms[0].Hero == "LOOTA_BOSS_32h" && gk.Forms[0].Power == "AT_132_PALADIN" && gk.Forms[0].Health == 30 && data.BossDecks[gk.Deck].Count(id => id == TagTeamCard) == 4
        && data.BossDecks[data.Boss("loot_19_gutmook").Deck].SequenceEqual(data.BossDecks[data.Boss("loot_33_gutmook").Deck]) && data.BossDecks[data.Boss("loot_19_gutmook").Deck].Count(id => id == "LOOTA_109") == 2);
    var lyris = data.BossDecks[data.Boss("loot_37_lyris").Deck];
    Check("P9 Lyris's deck is the wiki's as it is: 20 Flamewakers, 5 Arcane Intellects, 4 Arcane Giants, Archmage Antonidas; Flurgl's holds 2 Murloc Tinyfins", lyris.Count(id => id == "BRM_002") == 20
        && lyris.Count(id => id == Intellect) == 5 && lyris.Count(id => id == "KAR_711") == 4 && lyris.Count(id => id == "EX1_559") == 1
        && data.BossDecks[data.Boss("loot_46_flurgl").Deck].Count(id => id == "LOEA10_3") == 2 && C("LOEA10_3").Implemented && C("LOEA10_3").Power == null, string.Join(",", lyris.Distinct()));
    var trapped = data.Boss("loot_48_trapped_room");
    Check("P10 Trapped Room (family 01) now plays its two Shroomsayers: its deck is the wiki's list unchanged", data.BossDecks[trapped.Deck].Count(id => id == "LOOTA_107") == 2 && trapped.Source.Kind == DungeonRunDeckSource.KindAuthentic);
    // The validator on a broken tag-team row and on a power in the wrong hands.
    DungeonRunData Changed(Action<DungeonRunData> change) { var d = Load(); change(d); return d; }
    var noFlag = DungeonRunValidator.Validate(Changed(d => d.Boss("loot_15_george_karl").Flags.Remove("forms")), null);
    Check("P11 the tag team's row without its forms flag is refused", noFlag.Any(e => e.Contains("flag forms and the forms must go together")), string.Join(" | ", noFlag.Take(3)));
    var playerPower = DungeonRunAdmission.Classify(C("LOOTA_BOSS_51p"), "LOOTA_BOSS_51p", AdmitScope.Player, null, catalogRun: true);
    Check("P12 a family 08 power asked for the run player is refused with its row's reason", !playerPower.Admitted && playerPower.Family == "boss-control" && playerPower.Lift == DungeonRunAdmission.NoneBossPower);
});

Console.WriteLine($"SearchTelemetry swallowed leaf exceptions {SearchTelemetry.SwallowedLeafErrors}");
Console.WriteLine($"RESULT {passes} passed, {failures + SearchTelemetry.SwallowedLeafErrors} failed");
return failures + SearchTelemetry.SwallowedLeafErrors;
