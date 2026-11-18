using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.DungeonRun;
using SabberStoneCore.Config;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Kettle;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;

// Phase 8 family 09, Dungeon Run bosses C (real engine games through the run setup path: DungeonRun.BuildPlan on the shipped
// row -> DungeonRunSetup.Configure / ApplyAfterStart): the five final bosses (Vustrasz, Xol, Azari, The Darkness, King
// Togwaggle), the special rooms (Treasure Vault, Tad, Brimstone Warden, A. F. Kay, Chronomancer Inara), Mushhuckster Max,
// their cards, the sixteen Treasures in a boss's hands, the run layer's no-loss ending and run deck edits, and the rows,
// admission rows and validator.
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

// ============================================================ family 09 helpers
const string Golem = "LOOTA_BOSS_20t", Chest = "LOOT_357l", Darkspawn = "LOOTA_BOSS_49t", Candle = "LOOTA_BOSS_49t2", Coffer = "LOOTA_BOSS_52t",
    SunkenChest = "LOOTA_103", Pole = "LOOTA_BOSS_22t", Potion = "LOOTA_BOSS_50t", Rakanishu = "LOOTA_BOSS_99t", Wisp = "CS2_231";
// The shipped row with its own boss deck (Tad, the Treasure Vault: the deck is the mechanic); the run player's deck is plain cards.
Game FightRow(string bossId, string playerDeck = Yeti, int seed = 7, string cls = "MAGE")
{
    var plan = run.BuildPlan(At(cls, 4400, bossId), all);
    plan.PlayerDeck = Thirty(playerDeck).Select(C).ToList();
    plan.PlayerDeckSlots = null;
    return Ready(plan, seed);
}
int Rules(Controller c) => c[EncounterRules.RoomRules];
int Casts(Controller c) => DungeonRunEncounters.AutoCasts(c.Hero.HeroPower);
bool PlayOptions(Controller c) => c.Options().Any(o => o is PlayCardTask);
bool AttackOptions(Controller c) => c.Options().Any(o => o is MinionAttackTask);
// The boss's next turn starts with exactly `mana` Mana Crystals (the turn start adds one).
void BossTurnWith(Game g, int mana) { if (g.CurrentPlayer == g.Player2) EndTurn(g); g.Player2.BaseMana = mana - 1; EndTurn(g); }
void ClearBoard(Game g, Controller c) { foreach (var m in c.BoardZone.ToArray()) { m.ActivatedTrigger?.Remove(); m.OngoingEffect?.Remove(); c.BoardZone.Remove(m); c.SetasideZone.Add(m); } g.DeathProcessingAndAuraUpdate(); }
bool Catalog(string id, AdmitScope scope) => DungeonRunAdmission.Classify(C(id), id, scope, null, catalogRun: true).Admitted;
bool Legacy(string id, AdmitScope scope) => DungeonRunAdmission.Classify(C(id), id, scope).Admitted;

// ============================================================ A  Brimstone Warden: four Stone Golems, Dispel Ward by rule
Guard("Brimstone", () =>
{
    var row = data.Boss("loot_20_brimstone");
    var plan = RowPlan("loot_20_brimstone");
    Check("A1 the row's plan: Brimstone Warden with Dispel Ward, four Stone Golems to start, no bonus Mana Crystals, the wiki's 30 cards (11 Stone Golems, 4 Looming Presences)",
        plan.BossHero.Id == "LOOTA_BOSS_20h" && plan.BossHeroPowerId == "LOOTA_BOSS_20p" && plan.Start.BossBoard.Count == 4 && plan.Start.BossBoard.All(c => c.Id == Golem)
        && plan.BossStartManaBonus == 0 && row.Start.BossMana == 0 && plan.BossDeck.Count == 30 && plan.BossDeck.Count(c => c.Id == Golem) == 11 && plan.BossDeck.Count(c => c.Id == "LOEA_01") == 4
        && row.Source.Kind == "authentic", plan.BossDeck.Count + " " + plan.BossStartManaBonus);
    var g = Fight("loot_20_brimstone");
    Controller boss = g.Player2, player = g.Player1;
    var golems = boss.BoardZone.Where(m => m.Card.Id == Golem).ToList();
    Check("A2 after both mulligans four Stone Golems (15/5, 2 Mana, Can't attack) stand on his board; Dispel Ward (5) is never an option", golems.Count == 4 && boss.BoardZone.Count == 4
        && golems.All(m => m.AttackDamage == 15 && m.Health == 5 && m.CantAttack && m.HasDeathrattle) && C(Golem).Cost == 2 && boss.Hero.HeroPower.Cost == 5
        && boss.Hero.HeroPower.IsPassiveHeroPower && boss.Hero.HeroPower[EncounterRules.PowerActive] == 1, golems.Count + " golems");
    BossTurnWith(g, 3);
    Check("A3 below 5 Mana nothing is cast: the Golems cannot attack and the power is not offered", boss.BaseMana == 3 && Casts(boss) == 0 && !AttackOptions(boss) && !Offered(g) && golems.All(m => !m.IsSilenced) && boss.UsedMana == 0);
    Kill(g, golems[0]);
    Check("A4 a Stone Golem that dies costs its owner a Mana Crystal (3 to 2)", boss.BaseMana == 2 && boss.BoardZone.Count == 3, "mana " + boss.BaseMana);
    BossTurnWith(g, 5);
    Check("A5 at the start of his turn with 5 Mana Dispel Ward is cast by rule: the 5 Mana are paid and his minions are silenced", Casts(boss) == 1 && boss.BaseMana == 5 && boss.RemainingMana == 0
        && golems.Skip(1).All(m => m.IsSilenced && !m.CantAttack && !m.HasDeathrattle), "casts " + Casts(boss) + " left " + boss.RemainingMana);
    Check("A6 a silenced Stone Golem can attack (15 Attack), and it no longer costs a Mana Crystal when it dies", AttackOptions(boss) && golems[1].CanAttack && golems[1].AttackDamage == 15);
    int health = player.Hero.Health;
    g.Process(MinionAttackTask.Any(boss, golems[1], player.Hero));
    Kill(g, golems[2]);
    Check("A7 it hits the run hero for 15; a silenced Golem's death leaves the Mana Crystals alone", player.Hero.Health == health - 15 && boss.BaseMana == 5);
    BossTurnWith(g, 6);
    Check("A8 with every minion silenced already the power is not cast again (the Mana stays)", Casts(boss) == 1 && boss.RemainingMana == 6);
    var fresh = Board(g, boss, Golem);
    var clone = g.Clone();
    BossTurnWith(g, 7);
    Check("A9 a new Stone Golem brings the next cast (its Can't attack lifted); a clone taken before replays it on its own", Casts(boss) == 2 && fresh.IsSilenced && boss.RemainingMana == 2
        && Casts(clone.Player2) == 1 && !clone.Player2.BoardZone.Last().IsSilenced, "casts " + Casts(boss) + "/" + Casts(clone.Player2));
});

// ============================================================ B  Tad: no hand, Catch and Release, the Sunken Chest, Tad's Pole
Guard("Tad", () =>
{
    var row = data.Boss("loot_22_tad");
    var plan = RowPlan("loot_22_tad");
    string[] order = { "UNG_085", "NEW1_029", "LOOTA_102", "OG_024", "EX1_250", "EX1_110", "UNG_919", "EX1_298", "EX1_543", "UNG_852" };
    Check("B1 the row's plan: Tad with Catch and Release at his own 20 Health, no bonus Mana Crystals, and the wiki's ten minions as a ten-card deck (flag listedDeck)",
        plan.BossHero.Id == "LOOTA_BOSS_22h" && plan.BossHeroPowerId == "LOOTA_BOSS_22p" && plan.BossHealth == 20 && row.FixedHealth && plan.BossStartManaBonus == 0
        && plan.BossDeck.Count == 10 && plan.BossDeck.Select(c => c.Id).OrderBy(i => i, StringComparer.Ordinal).SequenceEqual(order.OrderBy(i => i, StringComparer.Ordinal))
        && order.Select((id, k) => C(id).Cost == k + 1).All(x => x) && row.HasFlag("listedDeck") && row.Tier == "3" && row.Source.Kind == "authentic",
        string.Join(",", plan.BossDeck.Select(c => c.Id + ":" + c.Cost)));
    var g = FightRow("loot_22_tad");
    Controller boss = g.Player2, player = g.Player1;
    var fact = g.RunDeckFacts.LastOrDefault(f => f.ControllerId == boss.Id);
    Check("B2 at the first turn start the hand he was dealt is back in his deck: no hand, ten cards in the deck, the room's rules on him, and a deck fact gives his own-deck ledger the exact list",
        boss.HandZone.Count == 0 && boss.DeckZone.Count == 10 && Rules(boss) == (EncounterRules.RoomEntered | EncounterRules.RoomNoDraw | EncounterRules.RoomNoPlay)
        && EncounterRules.NeverDraws(boss) && EncounterRules.PlaysNoCards(boss) && !EncounterRules.NeverDraws(player) && Rules(player) == 0
        && fact != null && fact.Kind == RunDeckFactKind.Rewritten && fact.Definitions.OrderBy(i => i, StringComparer.Ordinal).SequenceEqual(order.OrderBy(i => i, StringComparer.Ordinal)),
        boss.HandZone.Count + "/" + boss.DeckZone.Count + " rules " + Rules(boss));
    EndTurn(g);
    Check("B3 his turn: he draws nothing and takes no Fatigue; his only options are the hero power (0 Mana, clickable) and ending the turn", boss.HandZone.Count == 0 && boss.DeckZone.Count == 10
        && boss.Hero.Damage == 0 && boss.Hero.HeroPower.Cost == 0 && !boss.Hero.HeroPower.IsPassiveHeroPower && Offered(g) && boss.Options().Count == 2 && boss.BaseMana == 1, boss.Options().Count + " options");
    bool first = Power(g);
    Check("B4 Catch and Release at 1 Mana Crystal pulls Emerald Hive Queen from his deck (the wiki's order, not a random minion); once a turn", first && boss.BoardZone.Count == 1
        && boss.BoardZone[0].Card.Id == "UNG_085" && boss.DeckZone.Count == 9 && !Offered(g));
    var pulled = new List<string> { boss.BoardZone[0].Card.Id };
    for (int turn = 2; turn <= 10; turn++)
    {
        ClearBoard(g, boss);
        boss.Hero.Damage = 0;
        Round(g);
        boss.UsedMana = 0; boss.BaseMana = turn;
        if (Power(g)) pulled.Add(boss.BoardZone.Last().Card.Id);
    }
    Check("B5 turn by turn the minion whose Cost equals his Mana Crystals: Queen, Millhouse, Starving Crab, Flamewreathed Faceless, Earth Elemental, Cairne, Swamp King Dred, Ragnaros, King Krush, Tyrantus",
        pulled.SequenceEqual(order) && boss.DeckZone.Count == 0, string.Join(",", pulled));
    ClearBoard(g, boss); boss.Hero.Damage = 0;
    Round(g);
    string b6 = "damage " + boss.Hero.Damage + " fatigue " + boss.Hero.Fatigue + " offered " + Offered(g) + " board " + boss.BoardZone.Count + " deck " + boss.DeckZone.Count;
    Check("B6 with his deck empty he still neither draws nor fatigues; on this eleventh turn the undamaged Tad has summoned his Sunken Chest, and the power (offered by board room alone) summons nothing more",
        boss.Hero.Damage == 0 && boss.Hero.Fatigue == 0 && Offered(g) && Count(boss, SunkenChest) == 1 && Power(g) && boss.BoardZone.Count == 1, b6);
    Check("B7 a draw effect does nothing for him either (no card, no Fatigue); the run player draws as ever", Generic.Draw(boss) == null && boss.Hero.Damage == 0 && Generic.Draw(player) != null);
    // No minion of the matching Cost: a random minion of the deck (the card's text). A full board: not offered.
    var g2 = FightRow("loot_22_tad");
    Controller boss2 = g2.Player2;
    EndTurn(g2);
    boss2.BaseMana = 0;
    bool any = Power(g2);
    Check("B8 with no minion of the matching Cost (0 Mana Crystals) a random minion of his deck is pulled", any && boss2.BoardZone.Count == 1 && boss2.DeckZone.Count == 9 && order.Contains(boss2.BoardZone[0].Card.Id));
    Round(g2);
    while (!boss2.BoardZone.IsFull) Board(g2, boss2, Wisp);
    Check("B9 with a full board the power is not offered", !Offered(g2));
    // The Sunken Chest: the start of his eleventh turn, undamaged.
    var g3 = FightRow("loot_22_tad");
    Controller boss3 = g3.Player2, player3 = g3.Player1;
    for (int turn = 1; turn <= 10; turn++) { EndTurn(g3); if (turn < 10) EndTurn(g3); }
    bool before = Count(boss3, SunkenChest) == 0 && boss3.Hero.HeroPower[GameTag.TAG_SCRIPT_DATA_NUM_1] == 10;
    EndTurn(g3); EndTurn(g3);
    var chest = boss3.BoardZone.FirstOrDefault(m => m.Card.Id == SunkenChest);
    Check("B10 an undamaged Tad summons the Sunken Chest (0/1) at the start of his eleventh turn, not before", before && chest != null && chest.AttackDamage == 0 && chest.Health == 1 && Count(boss3, SunkenChest) == 1
        && boss3.Hero.HeroPower[GameTag.TAG_SCRIPT_DATA_NUM_1] == 11, "turns " + boss3.Hero.HeroPower[GameTag.TAG_SCRIPT_DATA_NUM_1]);
    EndTurn(g3); EndTurn(g3);
    Check("B11 only once: no second Chest on his twelfth turn", Count(boss3, SunkenChest) == 1);
    EndTurn(g3);
    Kill(g3, chest);
    g3.Process(EndTurnTask.Any(g3.CurrentPlayer));
    var edits = g3.RunResult?.Edits.Select(e => e.Op + ":" + e.CardId).ToList() ?? new List<string>();
    Check("B12 breaking the Sunken Chest: Tad loses the game and Tad's Pole is asked for the run deck (a run deck edit)", Over(g3) && player3.PlayState == PlayState.WON && boss3.PlayState == PlayState.LOST
        && edits.SequenceEqual(new[] { "add:" + Pole }) && !DungeonRunSetup.Harvest(g3).NoLoss, g3.State + " " + string.Join(",", edits));
    var g4 = FightRow("loot_22_tad");
    g4.Player2.Hero.Damage = 1;
    for (int turn = 1; turn <= 12; turn++) { EndTurn(g4); EndTurn(g4); }
    Check("B13 a Tad that carries damage summons no Chest", Count(g4.Player2, SunkenChest) == 0 && g4.Player2.Hero.HeroPower[GameTag.TAG_SCRIPT_DATA_NUM_1] == 12);
    var g5 = Fight("loot_49_darkness");
    var stolen = Board(g5, g5.Player1, SunkenChest);
    Kill(g5, stolen);
    g5.Process(EndTurnTask.Any(g5.CurrentPlayer));
    Check("B14 a Sunken Chest on the run player's side: its owner loses the game, and nothing is added to the run deck", Over(g5) && g5.Player1.PlayState == PlayState.LOST && (g5.RunResult == null || g5.RunResult.Edits.Count == 0));
    // Tad's Pole in the run player's hands.
    var g6 = Fight("loot_49_darkness", bossDeck: Croc);
    Controller boss6 = g6.Player2, player6 = g6.Player1;
    var pole = ToHand(g6, player6, Pole);
    bool equipped = Play(g6, pole);
    int deck = boss6.DeckZone.Count;
    EndTurn(g6);
    var caught = player6.BoardZone.FirstOrDefault(m => m.Card.Id == Croc);
    Check("B15 Tad's Pole (a 1-Mana weapon): at the end of its owner's turn a minion of the opponent's deck leaves that deck and joins its owner's board", equipped && C(Pole).Type == CardType.WEAPON && C(Pole).Cost == 1
        && player6.Hero.Weapon?.Card.Id == Pole && caught != null && caught.Controller == player6 && boss6.DeckZone.Count == deck - 2 && player6.BoardZone.Count == 1,
        "deck " + deck + "->" + boss6.DeckZone.Count + " (one pulled, one drawn by its owner)");
    EndTurn(g6);
    Check("B16 only at the end of its owner's turn (nothing at the end of the opponent's)", player6.BoardZone.Count == 1 && player6.Hero.Weapon != null);
    while (!player6.BoardZone.IsFull) Board(g6, player6, Wisp);
    int deck6 = boss6.DeckZone.Count;
    EndTurn(g6);
    Check("B17 nothing with a full board (the opponent's deck loses only its own draw)", boss6.DeckZone.Count == deck6 - 1);
    var g7 = Fight("loot_49_darkness", bossDeck: Moonfire);
    Play(g7, ToHand(g7, g7.Player1, Pole));
    EndTurn(g7);
    Check("B18 nothing when the opponent's deck holds no minion", g7.Player1.BoardZone.Count == 0);
    var g8 = Fight("loot_49_darkness", playerDeck: Raptor);
    ToBoss(g8);
    Play(g8, ToHand(g8, g8.Player2, Pole));
    int pdeck = g8.Player1.DeckZone.Count;
    EndTurn(g8);
    Check("B19 in a boss's hands it pulls from the run player's deck the same way: one public minion, the deck one card shorter", g8.Player2.BoardZone.Count(m => m.Card.Id == Raptor) == 1 && g8.Player1.DeckZone.Count == pdeck - 1 - 1,
        pdeck + "->" + g8.Player1.DeckZone.Count);
});

// ============================================================ C  Vustrasz the Ancient: five Master Chests, Vindictive Breath
Guard("Vustrasz", () =>
{
    var row = data.Boss("loot_25_vustrasz");
    var plan = RowPlan("loot_25_vustrasz");
    Check("C1 the row's plan: Vustrasz with Vindictive Breath, a major of act I (the act curve's Health plus 10), five Master Chests to start, the wiki's 30 cards",
        plan.BossHero.Id == "LOOTA_BOSS_25h" && plan.BossHeroPowerId == "LOOTA_BOSS_25p" && row.Tier == "major1" && row.HealthOffset == 10 && plan.BossHealth == 40
        && plan.Start.BossBoard.Count == 5 && plan.Start.BossBoard.All(c => c.Id == Chest) && plan.BossDeck.Count == 30 && plan.BossDeck.Count(c => c.Id == "BRMA12_8t") == 2
        && plan.BossDeck.Count(c => c.Id == "LOEA_01H") == 2 && row.Source.Kind == "authentic", "health " + plan.BossHealth);
    var g = Fight("loot_25_vustrasz");
    Controller boss = g.Player2, player = g.Player1;
    var chests = boss.BoardZone.Where(m => m.Card.Id == Chest).ToList();
    var mine = Board(g, player, Yeti);
    ToBoss(g);
    Check("C2 five Master Chests (0/8) stand on his board; with none missing Vindictive Breath (0) is not offered", chests.Count == 5 && chests.All(m => m.AttackDamage == 0 && m.Health == 8)
        && DungeonRunEncounters.MissingChests(boss) == 0 && !Offered(g) && boss.Hero.HeroPower.Cost == 0 && !boss.Hero.HeroPower.IsPassiveHeroPower);
    int hand = player.HandZone.Count;
    Kill(g, chests[0]);
    string[] marin = { "LOOT_998h", "LOOT_998j", "LOOT_998k", "LOOT_998l" };
    Check("C3 a Master Chest that dies gives the run player one of Marin's four treasures", player.HandZone.Count == hand + 1 && marin.Contains(player.HandZone.Last().Card.Id), player.HandZone.Last().Card.Id);
    var own = Board(g, boss, Yeti);
    int hp = player.Hero.Health;
    bool used = Offered(g) && Power(g);
    Check("C4 with one Chest missing it deals 1 damage to all enemies (the run hero and its minions), not to his own side", used && player.Hero.Health == hp - 1 && mine.Damage == 1 && own.Damage == 0
        && chests.Skip(1).All(m => m.Damage == 0) && boss.Hero.Damage == 0);
    Check("C5 once a turn", !Offered(g));
    Kill(g, chests[1]); Kill(g, chests[2]);
    Round(g);
    hp = player.Hero.Health;
    Check("C6 with three missing: 3 damage to all enemies", DungeonRunEncounters.MissingChests(boss) == 3 && Power(g) && player.Hero.Health == hp - 3 && mine.Damage == 4, "missing " + DungeonRunEncounters.MissingChests(boss));
    var clone = g.Clone();
    Kill(clone, clone.Player2.BoardZone.First(m => m.Card.Id == Chest));
    Check("C7 a clone counts its own Chests (4 missing there, 3 here)", DungeonRunEncounters.MissingChests(clone.Player2) == 4 && DungeonRunEncounters.MissingChests(boss) == 3);
    var extra = Board(g, boss, Chest); Board(g, boss, Chest); Board(g, boss, Chest);
    Check("C8 never below none missing (more than five Chests count as five)", DungeonRunEncounters.MissingChests(boss) == 0);
    // Chromatic Dragonkin.
    var g2 = Fight("loot_25_vustrasz");
    var kin = Board(g2, g2.Player2, "BRMA12_8t");
    bool stats = kin.AttackDamage == 2 && kin.Health == 3 && C("BRMA12_8t").Cost == 2;
    Cast(g2, Moonfire, g2.Player2.Hero);
    Check("C9 Chromatic Dragonkin (2/3 for 2): whenever its owner's opponent casts a spell it gains +2/+2 (Draconic Lineage)", stats && kin.AttackDamage == 4 && kin.Health == 5
        && kin.AppliedEnchantments.Any(e => e.Card.Id == "BRMA12_8te"));
    ToBoss(g2);
    Cast(g2, Moonfire, g2.Player1.Hero);
    Check("C10 its owner's own spell gives it nothing", kin.AttackDamage == 4 && kin.Health == 5);
});

// ============================================================ D  Xol the Unscathed: Beam Me Up! and the six Beams
Guard("Xol", () =>
{
    var plan = RowPlan("loot_26_xol");
    Check("D1 the row's plan: Xol with the passive Beam Me Up! (in the passive registry, never an option), a major of act I, the wiki's 30 cards (4 Howlfiends, Lakkari Sacrifice)",
        plan.BossHero.Id == "LOOTA_BOSS_26h" && plan.BossHeroPowerId == "LOOTA_BOSS_26p8" && DungeonRunCardsGen.PassiveHeroPowers.Contains("LOOTA_BOSS_26p8") && plan.BossHealth == 40
        && plan.BossDeck.Count == 30 && plan.BossDeck.Count(c => c.Id == "ICC_218") == 4 && plan.BossDeck.Count(c => c.Id == "UNG_829") == 1 && data.Boss("loot_26_xol").Source.Kind == "authentic");
    var g = Fight("loot_26_xol");
    Controller boss = g.Player2, player = g.Player1;
    int hand0 = boss.HandZone.Count;
    EndTurn(g);
    int hand1 = boss.HandZone.Count;
    bool notOffered = !Offered(g) && boss.Hero.HeroPower.IsPassiveHeroPower && boss.Hero.HeroPower[EncounterRules.PowerActive] == 1;
    EndTurn(g);
    var beam = boss.HandZone.Last();
    Check("D2 at the end of her turn one of the six Beams (3-Mana spells) joins her hand; nothing at the end of the run player's turn", notOffered && hand1 == hand0 + 1 && boss.HandZone.Count == hand1 + 1
        && DungeonRunEncounters.Beams.Contains(beam.Card.Id) && beam.Card.Type == CardType.SPELL && beam.Cost == 3 && DungeonRunEncounters.Beams.Count == 6
        && DungeonRunEncounters.Beams.All(id => C(id).Cost == 3 && C(id).Type == CardType.SPELL), beam.Card.Id);
    var seen = new HashSet<string>();
    for (int i = 0; i < 60; i++)
    {
        foreach (var c in boss.HandZone.ToArray()) { if (DungeonRunEncounters.Beams.Contains(c.Card.Id)) seen.Add(c.Card.Id); boss.HandZone.Remove(c); boss.SetasideZone.Add(c); }
        Round(g);
    }
    Check("D3 over sixty turns every one of the six Beams is given", seen.Count == 6, string.Join(",", seen.OrderBy(x => x)));
    foreach (var c in boss.HandZone.ToArray()) { boss.HandZone.Remove(c); boss.SetasideZone.Add(c); }
    FillHand(g, boss);
    Round(g);
    Check("D4 a full hand gets none", boss.HandZone.Count == 10 && boss.HandZone.Count(c => DungeonRunEncounters.Beams.Contains(c.Card.Id)) == 0);

    Game Beams() { var b = Fight("loot_26_xol"); ToBoss(b); return b; }
    // Frost.
    var gf = Beams();
    var row3 = new[] { Board(gf, gf.Player1, Yeti), Board(gf, gf.Player1, Yeti), Board(gf, gf.Player1, Yeti) };
    var ownYeti = Board(gf, gf.Player2, Yeti);
    Cast(gf, "LOOTA_BOSS_26p2");
    var frozen = row3.Select(m => m.IsFrozen).ToArray();
    Check("D5 Beam of Frost freezes a random enemy minion and the minions next to it (two at an edge, three in the middle), never her own", frozen.Count(f => f) >= 2 && frozen[1] && !ownYeti.IsFrozen
        && !gf.Player1.Hero.IsFrozen, string.Join(",", frozen));
    var gf2 = Beams();
    var lone = Board(gf2, gf2.Player1, Yeti);
    Cast(gf2, "LOOTA_BOSS_26p2");
    bool loneFrozen = lone.IsFrozen;
    var gf3 = Beams();
    bool emptyOk = Cast(gf3, "LOOTA_BOSS_26p2") && Cast(gf3, "LOOTA_BOSS_26p3") && Cast(gf3, "LOOTA_BOSS_26p4") && Cast(gf3, "LOOTA_BOSS_26p5") && Cast(gf3, "LOOTA_BOSS_26p6");
    Check("D6 a lone minion is frozen alone; with no enemy minion the Beams can be cast and do nothing", loneFrozen && emptyOk && gf3.Player2.BoardZone.Count == 0 && !Over(gf3));
    // Death.
    var gd = Beams();
    var hurt = Board(gd, gd.Player1, Yeti); var whole = Board(gd, gd.Player1, Yeti); var ownHurt = Board(gd, gd.Player2, Yeti);
    hurt.Damage = 1; ownHurt.Damage = 1;
    Cast(gd, "LOOTA_BOSS_26p3");
    Check("D7 Beam of Death destroys all damaged enemy minions: the undamaged one and her own damaged one stay", hurt.Zone != gd.Player1.BoardZone && whole.Zone == gd.Player1.BoardZone && ownHurt.Zone == gd.Player2.BoardZone);
    // Fire and Decay.
    var gz = Beams();
    var a = Board(gz, gz.Player1, Yeti); var b2 = Board(gz, gz.Player1, Yeti); var c2 = Board(gz, gz.Player2, Yeti);
    int ph = gz.Player1.Hero.Health;
    Cast(gz, "LOOTA_BOSS_26p6");
    gz.Player1.BaseMana = 4;
    Cast(gz, "LOOTA_BOSS_26p7");
    Check("D8 Beam of Fire deals 2 damage to all enemy minions (not the hero, not her own); Beam of Decay destroys one of the opponent's Mana Crystals (4 to 3)",
        a.Damage == 2 && b2.Damage == 2 && c2.Damage == 0 && gz.Player1.Hero.Health == ph && gz.Player1.BaseMana == 3, a.Damage + " mana " + gz.Player1.BaseMana);
    // Confusion.
    var gc = Beams();
    var prey = Board(gc, gc.Player1, Yeti);
    Cast(gc, "LOOTA_BOSS_26p4");
    Check("D9 Beam of Confusion takes control of a random enemy minion", prey.Controller == gc.Player2 && prey.Zone == gc.Player2.BoardZone && gc.Player1.BoardZone.Count == 0);
    var gs = Fight("loot_26_xol", shrine: Shrine);
    ToBoss(gs);
    var shrine = ShrineRules.Of(gs.Player1).First();
    Cast(gs, "LOOTA_BOSS_26p4");
    bool shrineStays = shrine.Controller == gs.Player1 && gs.Player2.BoardZone.Count == 0;
    var beside = Board(gs, gs.Player1, Yeti);
    Cast(gs, "LOOTA_BOSS_26p4");
    bool yetiTaken = beside.Controller == gs.Player2 && shrine.Controller == gs.Player1;
    var gfull = Beams();
    var safe = Board(gfull, gfull.Player1, Yeti);
    while (!gfull.Player2.BoardZone.IsFull) Board(gfull, gfull.Player2, Wisp);
    Cast(gfull, "LOOTA_BOSS_26p4");
    Check("D10 a run shrine is never the pick (alone it stays; beside a minion the minion is taken); nothing with a full board", shrineStays && yetiTaken && safe.Controller == gfull.Player1);
    // Fear.
    var ge = Beams();
    var feared = Board(ge, ge.Player1, Croc);
    int deckBefore = ge.Player1.DeckZone.Count;
    Cast(ge, "LOOTA_BOSS_26p5");
    Check("D11 Beam of Fear shuffles a random enemy minion into its owner's deck", ge.Player1.BoardZone.Count == 0 && ge.Player1.DeckZone.Count == deckBefore + 1 && ge.Player1.DeckZone.Any(p => p.Card.Id == Croc)
        && ge.Player2.DeckZone.All(p => p.Card.Id != Croc));
    var gs2 = Fight("loot_26_xol", shrine: Shrine);
    ToBoss(gs2);
    var shrine2 = ShrineRules.Of(gs2.Player1).First();
    int deck2 = gs2.Player1.DeckZone.Count;
    Cast(gs2, "LOOTA_BOSS_26p5");
    var form = ShrineRules.Of(gs2.Player1).FirstOrDefault();
    Check("D12 a run shrine is destroyed instead of shuffled: it goes dormant on the board and the deck is unchanged", gs2.Player1.DeckZone.Count == deck2 && ShrineRules.IsCorpse(shrine2) && form != null && form.IsDormant);
    // In the run player's hands a Beam works for the run player.
    var gp = Fight("loot_26_xol");
    var bossMinion = Board(gp, gp.Player2, Yeti);
    Cast(gp, "LOOTA_BOSS_26p6");
    Check("D13 a Beam in the run player's hands (a steal) works for the run player: Beam of Fire damages the boss's minions", bossMinion.Damage == 2);
    var gv = Beams();
    ToHand(gv, gv.Player2, "LOOTA_BOSS_26p6");
    var target = Board(gv, gv.Player1, Yeti);
    var (view, viewBoss, viewPlayer) = FairView(gv);
    Mana(viewBoss);
    view.Process(PlayCardTask.Any(viewBoss, viewBoss.HandZone.First(p => p.Card.Id == "LOOTA_BOSS_26p6"), null, -1, 0));
    Check("D14 the boss's fair view holds her Beams and plays them: Beam of Fire there damages the run player's minion", viewPlayer.BoardZone.Count == 1 && viewPlayer.BoardZone[0].Damage == 2 && target.Damage == 0);
});

// ============================================================ E  Azari, the Devourer: Devour by rule
Guard("Azari", () =>
{
    var plan = RowPlan("loot_34_azari");
    Check("E1 the row's plan: Azari with Devour (0; cast by rule, never an option), a major of act I, the wiki's 30 cards", plan.BossHero.Id == "LOOTA_BOSS_34h" && plan.BossHeroPowerId == "LOOTA_BOSS_34p"
        && plan.BossHealth == 40 && plan.BossDeck.Count == 30 && data.Boss("loot_34_azari").Source.Kind == "authentic" && DungeonRunCardsGen.PassiveHeroPowers.Contains("LOOTA_BOSS_34p") && C("LOOTA_BOSS_34p").Cost == 0);
    var g = Fight("loot_34_azari", playerDeck: Croc);
    Controller boss = g.Player2, player = g.Player1;
    var top = new[] { player.DeckZone.TopCard };
    int deck = player.DeckZone.Count, grave = player.GraveyardZone.Count, hand = player.HandZone.Count;
    EndTurn(g);
    Check("E2 at the start of his turn the top 2 cards of the run player's deck go to the run player's graveyard (face up); the power is not an option", player.DeckZone.Count == deck - 2
        && player.GraveyardZone.Count == grave + 2 && player.GraveyardZone.Contains(top[0]) && player.HandZone.Count == hand && Casts(boss) == 1 && !Offered(g) && boss.UsedMana == 0,
        deck + "->" + player.DeckZone.Count);
    var (view, viewBoss, viewPlayer) = FairView(g);
    Check("E3 the boss's fair view: the two burned cards are known in the graveyard, the deck is two shorter there too, and the rest of the deck and the hand stay masked",
        viewPlayer.GraveyardZone.Count(c => c.Card.Id == Croc) == 2 && viewPlayer.DeckZone.Count == player.DeckZone.Count && !viewPlayer.DeckZone.Any(p => p.Card.Id == Croc)
        && !viewPlayer.HandZone.Any(p => p.Card.Id == Croc), "view deck " + viewPlayer.DeckZone.Count + " known " + viewPlayer.DeckZone.Count(p => p.Card.Id == Croc));
    EndTurn(g);
    int afterDraw = player.DeckZone.Count;
    EndTurn(g);
    Check("E4 every turn of his: two more (the run player's own draw in between)", player.DeckZone.Count == afterDraw - 2 && Casts(boss) == 2);
    while (player.DeckZone.Count > 1) { var p = player.DeckZone.TopCard; player.DeckZone.Remove(p); player.SetasideZone.Add(p); }
    ToPlayer(g);
    bool emptied = player.DeckZone.Count == 0 || player.DeckZone.Count == 1;
    if (player.DeckZone.Count == 0) Entity.FromCard(player, C(Croc), zone: player.DeckZone);
    int g1 = player.GraveyardZone.Count;
    EndTurn(g);
    Check("E5 with one card left it burns that one", emptied && player.DeckZone.Count == 0 && player.GraveyardZone.Count == g1 + 1 && Casts(boss) == 3);
    EndTurn(g);
    int hp = player.Hero.Health, casts = Casts(boss);
    EndTurn(g);
    Check("E6 with the deck empty nothing is cast (and Devour itself never fatigues the run player)", Casts(boss) == casts && player.Hero.Health == hp);
    var g2 = Fight("loot_34_azari", playerDeck: Croc);
    var clone = g2.Clone();
    EndTurn(clone);
    Check("E7 a clone (an AI simulation) burns in the clone only", clone.Player1.DeckZone.Count == g2.Player1.DeckZone.Count - 2 && Casts(clone.Player2) == 1 && Casts(g2.Player2) == 0);
});

// ============================================================ F  The Darkness: Darkspawn and the Luminous Candles
Guard("Darkness", () =>
{
    var row = data.Boss("loot_49_darkness");
    var state = At("MAGE", 4400, "loot_49_darkness");
    var plan = run.BuildPlan(state, all);
    Check("F1 the row's plan: The Darkness with Encroaching Darkness, a major of act I, three Luminous Candles for the run player's hand (never in the run deck), the wiki's 30 cards (4 Looming Presences)",
        plan.BossHero.Id == "LOOTA_BOSS_49h" && plan.BossHeroPowerId == "LOOTA_BOSS_49p" && plan.BossHealth == 40 && plan.Start.PlayerHandAdd.Count == 3 && plan.Start.PlayerHandAdd.All(c => c.Id == Candle)
        && !plan.PlayerDeck.Any(c => c.Id == Candle) && !state.Deck.Contains(Candle) && plan.BossDeck.Count == 30 && plan.BossDeck.Count(c => c.Id == "LOEA_01H") == 4 && row.Source.Kind == "authentic");
    var g = Fight("loot_49_darkness");
    Controller boss = g.Player2, player = g.Player1;
    Check("F2 after both mulligans the run player holds three Luminous Candles (0 Mana)", player.HandZone.Count(c => c.Card.Id == Candle) == 3 && C(Candle).Cost == 0 && C(Candle).Type == CardType.SPELL);
    ToBoss(g);
    bool used = Offered(g) && Power(g);
    var spawn = boss.BoardZone.LastOrDefault();
    Check("F3 Encroaching Darkness (0) summons a 5/5 Darkspawn; once a turn", used && spawn != null && spawn.Card.Id == Darkspawn && spawn.AttackDamage == 5 && spawn.Health == 5 && !Offered(g) && boss.UsedMana == 0
        && boss.Hero.HeroPower.Cost == 0);
    Round(g);
    Power(g);
    var keep = Board(g, boss, Yeti);
    ToPlayer(g);
    var strayed = Board(g, player, Darkspawn);
    var mine = Board(g, player, Yeti);
    bool cast = Play(g, player.HandZone.First(c => c.Card.Id == Candle));
    Check("F4 a Luminous Candle destroys all Darkspawns, on both boards, and nothing else", cast && Count(boss, Darkspawn) == 0 && Count(player, Darkspawn) == 0 && keep.Zone == boss.BoardZone
        && mine.Zone == player.BoardZone && player.HandZone.Count(c => c.Card.Id == Candle) == 2);
    Check("F5 with no Darkspawn in play a Candle does nothing", Play(g, player.HandZone.First(c => c.Card.Id == Candle)) && keep.Zone == boss.BoardZone && mine.Zone == player.BoardZone);
    ToBoss(g);
    while (!boss.BoardZone.IsFull) Board(g, boss, Wisp);
    Check("F6 with a full board the power is not offered", !Offered(g));
    var g2 = Fight("loot_49_darkness");
    FillHand(g2, g2.Player1);
    var plan2 = RowPlan("loot_49_darkness");
    plan2.PlayerDeck = Thirty(Yeti).Select(C).ToList(); plan2.PlayerDeckSlots = null; plan2.PlayerOpeningHandBonus = 5;
    var g3 = Ready(plan2);
    Check("F7 with a hand too full for all three the rest are shuffled into the deck for this fight (three Candles in hand and deck together)",
        g3.Player1.HandZone.Count(c => c.Card.Id == Candle) + g3.Player1.DeckZone.Count(c => c.Card.Id == Candle) == 3 && g3.Player1.HandZone.Count <= 10,
        g3.Player1.HandZone.Count(c => c.Card.Id == Candle) + "+" + g3.Player1.DeckZone.Count(c => c.Card.Id == Candle));
});

// ============================================================ G  Mushhuckster Max: the Mushroom Potion craft
Guard("Max", () =>
{
    var plan = RowPlan("loot_50_max");
    var ingredients = DungeonRunEncounters.MushroomIngredients;
    Check("G1 the row's plan: Mushhuckster Max with Mushroom, Mushroom (2), the wiki's 30 cards (2 Shroomsayers, 4 Fungalmancers, Eater of Secrets); nine ingredients, the 1-Cost Kazakus effects",
        plan.BossHero.Id == "LOOTA_BOSS_50h" && plan.BossHeroPowerId == "LOOTA_BOSS_50p" && plan.BossDeck.Count == 30 && plan.BossDeck.Count(c => c.Id == "LOOTA_107") == 2
        && plan.BossDeck.Count(c => c.Id == "LOOT_167") == 4 && plan.BossDeck.Count(c => c.Id == "OG_254") == 1 && data.Boss("loot_50_max").Source.Kind == "authentic"
        && ingredients.Count == 9 && ingredients.All(id => C(id).Cost == 1 && C(id).Type == CardType.SPELL) && C(Potion).Cost == 1 && C("LOOTA_BOSS_50p").Cost == 2,
        string.Join(",", plan.BossDeck.Select(c => c.Id).Distinct().Take(6)));
    var g = Fight("loot_50_max");
    Controller boss = g.Player2, player = g.Player1;
    ToBoss(g);
    int hand = boss.HandZone.Count;
    bool used = Offered(g) && Power(g);
    var first = boss.Choice;
    var offers1 = first?.Choices.Select(id => g.IdEntityDic[id].Card.Id).ToList() ?? new List<string>();
    Check("G2 the power opens a Discover among three different ingredients", used && first != null && offers1.Count == 3 && offers1.Distinct().Count() == 3 && offers1.All(ingredients.Contains) && boss.UsedMana == 2, string.Join(",", offers1));
    var cloneMid = g.Clone();
    g.Process(ChooseTask.Pick(boss, first.Choices[0]));
    var second = boss.Choice;
    var offers2 = second?.Choices.Select(id => g.IdEntityDic[id].Card.Id).ToList() ?? new List<string>();
    Check("G3 then a second Discover among three of the other ingredients (never the first pick)", second != null && offers2.Count == 3 && offers2.Distinct().Count() == 3 && offers2.All(ingredients.Contains)
        && !offers2.Contains(offers1[0]), string.Join(",", offers2));
    g.Process(ChooseTask.Pick(boss, second.Choices[1]));
    var potion = boss.HandZone.LastOrDefault();
    Check("G4 the Mushroom Potion (1 Mana) of the two picks, in order, joins his hand; no choice is left open", boss.Choice == null && boss.HandZone.Count == hand + 1 && potion != null && potion.Card.Id == Potion
        && potion.Cost == 1 && DungeonRunEncounters.PotionParts(potion).SequenceEqual(new[] { offers1[0], offers2[1] }) && !Offered(g), string.Join("+", DungeonRunEncounters.PotionParts(potion)));
    cloneMid.Process(ChooseTask.Pick(cloneMid.Player2, cloneMid.Player2.Choice.Choices[2]));
    cloneMid.Process(ChooseTask.Pick(cloneMid.Player2, cloneMid.Player2.Choice.Choices[0]));
    var clonePotion = cloneMid.Player2.HandZone.LastOrDefault();
    Check("G5 a clone taken in the middle of the craft finishes it on its own, with its own picks", clonePotion != null && clonePotion.Card.Id == Potion && DungeonRunEncounters.PotionParts(clonePotion).Count == 2
        && DungeonRunEncounters.PotionParts(clonePotion)[0] == offers1[2]);
    var seen = new HashSet<string>();
    for (int i = 0; i < 40; i++)
    {
        Entity.FromCard(player, C(Yeti), zone: player.DeckZone); Entity.FromCard(boss, C(Yeti), zone: boss.DeckZone);
        Round(g);
        foreach (var c in boss.HandZone.ToArray()) { boss.HandZone.Remove(c); boss.SetasideZone.Add(c); }
        if (!Power(g)) break;
        foreach (int id in boss.Choice.Choices) seen.Add(g.IdEntityDic[id].Card.Id);
        g.Process(ChooseTask.Pick(boss, boss.Choice.Choices[0]));
        foreach (int id in boss.Choice.Choices) seen.Add(g.IdEntityDic[id].Card.Id);
        g.Process(ChooseTask.Pick(boss, boss.Choice.Choices[0]));
    }
    FillHand(g, boss);
    Round(g);
    Check("G6 over forty crafts every one of the nine ingredients is offered; with a full hand the power is not offered", seen.Count == 9 && boss.HandZone.IsFull && !Offered(g) && !Over(g), seen.Count + " seen");

    // Each ingredient, in potions given directly.
    Game Lab() { var lab = Fight("loot_50_max"); ToBoss(lab); return lab; }
    IPlayable Give(Game lab, string a, string b) => DungeonRunEncounters.GivePotion(lab.CurrentPlayer, lab.CurrentPlayer.Hero.HeroPower, a, b);
    var l1 = Lab();
    var p1 = Give(l1, "CFM_621t2", "CFM_621t3");
    Mana(l1.Player2);
    var opts = l1.Player2.Options().OfType<PlayCardTask>().Where(o => o.Source == p1).ToList();
    var enemy = Board(l1, l1.Player1, Yeti);
    bool untargetedRefused = !l1.Player2.Options().OfType<PlayCardTask>().Any(o => o.Source == p1 && o.Target == null);
    bool played = Play(l1, p1, enemy);
    Check("G7 Heart of Fire + Stonescale Oil: the potion takes a target (any character; never untargeted), deals 3 damage to it and gives 4 Armor", opts.Count >= 2 && opts.All(o => o.Target != null)
        && untargetedRefused && played && enemy.Damage == 3 && l1.Player2.Hero.Armor == 4, opts.Count + " options, damage " + enemy.Damage);
    var l2 = Lab();
    var mineA = Board(l2, l2.Player2, Yeti); var theirs = Board(l2, l2.Player1, Yeti);
    var p2 = Give(l2, "CFM_621t4", "CFM_621t6");
    Mana(l2.Player2);
    var opts2 = l2.Player2.Options().OfType<PlayCardTask>().Where(o => o.Source == p2).ToList();
    Play(l2, p2);
    Check("G8 Felbloom + Goldthorn: no target (one untargeted option); 2 damage to all minions, then his own minions get +2 Health", opts2.Count == 1 && opts2[0].Target == null && theirs.Damage == 2
        && mineA.Damage == 2 && mineA.Health == 5 && theirs.Health == 3, mineA.Health + "/" + theirs.Health);
    var l3 = Lab();
    var cold = Board(l3, l3.Player1, Yeti);
    int handL3 = l3.Player2.HandZone.Count, deckL3 = l3.Player2.DeckZone.Count;
    Play(l3, Give(l3, "CFM_621t5", "CFM_621t8"));
    Check("G9 Icecap + Kingsblood: a random enemy minion is frozen and he draws a card", cold.IsFrozen && l3.Player2.DeckZone.Count == deckL3 - 1 && l3.Player2.HandZone.Count == handL3 + 1);
    var l4 = Lab();
    foreach (var c in l4.Player2.HandZone.ToArray()) { l4.Player2.HandZone.Remove(c); l4.Player2.SetasideZone.Add(c); }
    Play(l4, Give(l4, "CFM_621t9", "CFM_621t10"));
    var demon = l4.Player2.HandZone.FirstOrDefault();
    Check("G10 Shadow Oil + Netherbloom: a random Demon joins his hand and a 2/2 Demon is summoned", l4.Player2.HandZone.Count == 1 && demon is Minion && demon.Card.IsRace(Race.DEMON) && demon.Card.Implemented
        && l4.Player2.BoardZone.Count == 1 && l4.Player2.BoardZone[0].Card.Id == "CFM_621_m4" && l4.Player2.BoardZone[0].AttackDamage == 2, demon?.Card.Id);
    var l5 = Lab();
    var dead = Board(l5, l5.Player2, Croc);
    Kill(l5, dead);
    Play(l5, Give(l5, "CFM_621t37", "CFM_621t3"));
    Check("G11 Ichor of Undeath + Stonescale Oil: a friendly minion that died this game is summoned, and 4 Armor", l5.Player2.BoardZone.Count == 1 && l5.Player2.BoardZone[0].Card.Id == Croc && l5.Player2.Hero.Armor == 4);
    var l6 = Lab();
    var p6 = Give(l6, "CFM_621t3", "CFM_621t2");
    int php = l6.Player1.Hero.Health;
    Play(l6, p6, l6.Player1.Hero);
    Check("G12 the order is the potion's: Stonescale Oil then Heart of Fire, aimed at the run hero (3 damage)", l6.Player2.Hero.Armor == 4 && l6.Player1.Hero.Health == php - 3);
    string refused = "";
    try { Give(l6, "CFM_621t3", "CFM_621t3"); refused += "same "; } catch (ArgumentException) { }
    try { Give(l6, "CFM_621t3", "CS2_029"); refused += "foreign "; } catch (ArgumentException) { }
    FillHand(l6, l6.Player2);
    Check("G13 a potion is two different ingredients of the table (anything else is refused); none is made for a full hand", refused == "" && Give(l6, "CFM_621t3", "CFM_621t8") == null);
    var l7 = Lab();
    Give(l7, "CFM_621t4", "CFM_621t3");
    var theirs7 = Board(l7, l7.Player1, Yeti);
    var (view, viewBoss, viewPlayer) = FairView(l7);
    var viewPotion = viewBoss.HandZone.FirstOrDefault(c => c.Card.Id == Potion);
    Mana(viewBoss);
    bool viewPlayed = viewPotion != null && DungeonRunEncounters.PotionParts(viewPotion).SequenceEqual(new[] { "CFM_621t4", "CFM_621t3" }) && view.Process(PlayCardTask.Any(viewBoss, viewPotion, null, -1, 0));
    Check("G14 the boss's fair view holds the potion with its two ingredients and plays it there", viewPlayed && viewBoss.Hero.Armor == 4 && viewPlayer.BoardZone[0].Damage == 2 && theirs7.Damage == 0);
    var l8 = Fight("loot_50_max");
    var pp = DungeonRunEncounters.GivePotion(l8.Player1, l8.Player1.Hero, "CFM_621t2", "CFM_621t8");
    int bossHp = l8.Player2.Hero.Health;
    Play(l8, pp, l8.Player2.Hero);
    Check("G15 a potion in the run player's hands (a steal) works for the run player", l8.Player2.Hero.Health == bossHp - 3);
    var l9 = Lab();
    var p9 = Give(l9, "CFM_621t5", "CFM_621t10");
    Check("G16 a potion's ingredients are tags of its entity, carried by every clone", DungeonRunEncounters.PotionParts(l9.Clone().Player2.HandZone.Last()).SequenceEqual(new[] { "CFM_621t5", "CFM_621t10" })
        && DungeonRunEncounters.PotionParts(l9.Player2.Hero).Count == 0);
    // Eater of Secrets in his deck.
    var le = Fight("loot_50_max");
    Cast(le, "EX1_287"); Cast(le, "EX1_294");
    bool two = le.Player1.SecretZone.Count == 2;
    ToBoss(le);
    var eater = ToHand(le, le.Player2, "OG_254");
    Play(le, eater);
    Check("G17 Eater of Secrets played by the boss destroys the run player's Secrets and gains +1/+1 for each", two && le.Player1.SecretZone.Count == 0 && eater is Minion em && em.AttackDamage == 4 && em.Health == 6);
    Check("G18 Eater of Secrets is admitted for a boss deck only (its own row): the run player's pools and generation keep it out, as before", Catalog("OG_254", AdmitScope.Boss) && Legacy("OG_254", AdmitScope.Boss)
        && !Catalog("OG_254", AdmitScope.Player) && !Catalog("OG_254", AdmitScope.Generated) && DungeonRunAdmission.RuleFor("OG_254").Family == "secret-destroy"
        && DungeonRunAdmission.GeneratedRejects(true).Contains("OG_254") && DungeonRunAdmission.GeneratedRejects(false).Contains("OG_254"));
});

// ============================================================ H  Treasure Vault: seven Coffers, Doors Are Closing
Guard("Vault", () =>
{
    var row = data.Boss("loot_52_vault");
    var plan = RowPlan("loot_52_vault");
    Check("H1 the row's plan: the Treasure Vault with Doors Are Closing at its own 60 Health, seven Treasure Coffers to start, no bonus Mana Crystals, a curated ten-card deck it never draws",
        plan.BossHero.Id == "LOOTA_BOSS_52h" && plan.BossHeroPowerId == "LOOTA_BOSS_52p" && plan.BossHealth == 60 && row.FixedHealth && plan.BossStartManaBonus == 0
        && plan.Start.BossBoard.Count == 7 && plan.Start.BossBoard.All(c => c.Id == Coffer) && plan.BossDeck.Count == 10 && plan.BossDeck.All(c => c.Id == Coffer)
        && row.Source.Kind == "curated" && row.HasFlag("listedDeck") && row.HasFlag("noLoss") && row.HasFlag("boardStart") && plan.RunContext.TreasurePool.Count > 0, row.Source.Kind);
    var g = FightRow("loot_52_vault");
    Controller boss = g.Player2, player = g.Player1;
    var coffers = boss.BoardZone.ToList();
    Check("H2 seven Treasure Coffers (0/10, 1 Mana) fill its board; it holds no cards (ten in the deck), and its power (5) is never an option", coffers.Count == 7 && coffers.All(m => m.Card.Id == Coffer && m.AttackDamage == 0 && m.Health == 10)
        && C(Coffer).Cost == 1 && boss.HandZone.Count == 0 && boss.DeckZone.Count == 10 && EncounterRules.NeverDraws(boss) && EncounterRules.PlaysNoCards(boss)
        && boss.Hero.HeroPower.IsPassiveHeroPower && boss.Hero.HeroPower.Cost == 5);
    EndTurn(g);
    Check("H3 its turn: no draw, no Fatigue, and ending the turn is its only option", boss.HandZone.Count == 0 && boss.DeckZone.Count == 10 && boss.Hero.Damage == 0 && boss.Options().Count == 1 && boss.BaseMana == 1);
    var pool = RunHelpers.TreasurePool(g);
    Kill(g, coffers[0]);
    var edits = g.RunResult?.Edits.ToList() ?? new List<RunEdit>();
    Check("H4 a Treasure Coffer that dies asks the run for one random treasure of the run's own treasure pool (a run deck edit)", edits.Count == 1 && edits[0].Op == RunEdit.OpAdd && pool.Contains(edits[0].CardId) && pool.Count > 5,
        edits.Count + " edits, pool " + pool.Count);
    EndTurn(g);
    var mine = Board(g, player, Coffer);
    Kill(g, mine); Kill(g, coffers[1]);
    Check("H5 the Player is the run player whoever controls the Coffer: one on the run player's side adds a treasure too (three so far)", g.RunResult.Edits.Count == 3 && g.RunResult.Edits.All(e => pool.Contains(e.CardId)));
    var clone = g.Clone();
    Kill(clone, clone.Player2.BoardZone[0]);
    Check("H6 a clone's Coffer writes into the clone's own result (4 there, 3 here)", clone.RunResult.Edits.Count == 4 && g.RunResult.Edits.Count == 3);
    for (int turn = 2; turn <= 4; turn++) { EndTurn(g); EndTurn(g); }
    Check("H7 through its fourth turn (4 Mana Crystals) the fight goes on", !Over(g) && boss.BaseMana == 4 && g.CurrentPlayer == player && g[EncounterRules.NoLoss] == 0);
    EndTurn(g);
    var end = DungeonRunSetup.Harvest(g);
    Check("H8 at the start of its fifth turn (5 Mana Crystals) the doors close: the game ends with no loss for the run player (the run player is the winner on the table, the no-loss mark is set)",
        Over(g) && player.PlayState == PlayState.WON && boss.PlayState == PlayState.LOST && g[EncounterRules.NoLoss] == 1 && end.NoLoss && boss.BaseMana == 5 && Casts(boss) == 1, g.State + " " + player.PlayState);
    var g2 = FightRow("loot_52_vault");
    g2.Player2.Hero.Damage = 59;
    Cast(g2, Moonfire, g2.Player2.Hero);
    Check("H9 the Vault can be destroyed too: an ordinary win, no no-loss mark", Over(g2) && g2.Player1.PlayState == PlayState.WON && !DungeonRunSetup.Harvest(g2).NoLoss);
    var g3 = FightRow("loot_52_vault");
    var c3 = g3.Clone();
    for (int turn = 1; turn <= 5; turn++) { EndTurn(c3); if (!Over(c3)) EndTurn(c3); }
    Check("H10 a clone (an AI simulation) closes on its own; the real game goes on", Over(c3) && c3[EncounterRules.NoLoss] == 1 && !Over(g3) && g3[EncounterRules.NoLoss] == 0);
    var plain = new Game(new GameConfig { RandomSeed = 3, StartPlayer = 1, Player1HeroClass = CardClass.MAGE, Player2HeroClass = CardClass.MAGE, FillDecks = true, FillDecksPredictably = true, SkipMulligan = true, History = false, Logging = false });
    plain.StartGame();
    Check("H11 outside a run match a Coffer has no pool: nothing is added", DungeonRunEncounters.CofferTreasure(plain) == null && plain.RunResult == null);
    var g4 = FightRow("loot_52_vault");
    EndTurn(g4);
    g4.Player2.BaseMana = 10; g4.Player2.UsedMana = 0;
    Entity.FromCard(g4.Player2, C(Yeti), zone: g4.Player2.HandZone);
    Check("H12 a card that reaches its hand cannot be played (the room's rule), whatever its Mana", g4.Player2.HandZone.Count == 1 && !PlayOptions(g4.Player2));
});

// ============================================================ I  Chronomancer Inara: Stolen Time by rule, Medivh's cards
Guard("Inara", () =>
{
    var plan = RowPlan("loot_53_inara");
    Check("I1 the row's plan: Chronomancer Inara with Stolen Time (10; cast by rule, never an option), the wiki's 30 cards (Medivh's Astral Portal, Archmage's Apprentice and Archmage's Insight, 4 Looming Presences)",
        plan.BossHero.Id == "LOOTA_BOSS_53h" && plan.BossHeroPowerId == "LOOTA_BOSS_53p" && plan.BossDeck.Count == 30 && plan.BossDeck.Count(c => c.Id == "KARA_00_07") == 2
        && plan.BossDeck.Count(c => c.Id == "KARA_00_08") == 2 && plan.BossDeck.Count(c => c.Id == "KARA_00_05") == 2 && plan.BossDeck.Count(c => c.Id == "LOEA_01H") == 4
        && data.Boss("loot_53_inara").Source.Kind == "authentic" && C("LOOTA_BOSS_53p").Cost == 10 && DungeonRunCardsGen.PassiveHeroPowers.Contains("LOOTA_BOSS_53p"));
    var g = Fight("loot_53_inara");
    Controller boss = g.Player2, player = g.Player1;
    BossTurnWith(g, 9);
    Check("I2 at 9 Mana nothing is cast", Casts(boss) == 0 && boss.BaseMana == 9 && boss.RemainingMana == 9 && !Offered(g));
    BossTurnWith(g, 10);
    Check("I3 at the start of her turn with 10 Mana Stolen Time is cast by rule: all her Mana is spent and three Mana Crystals are destroyed (7 left, none to spend)", Casts(boss) == 1 && boss.BaseMana == 7
        && boss.RemainingMana == 0 && g.CurrentPlayer == boss, "base " + boss.BaseMana + " left " + boss.RemainingMana);
    EndTurn(g);
    Check("I4 she takes an extra turn, with 8 Mana and no second cast", g.CurrentPlayer == boss && boss.BaseMana == 8 && boss.RemainingMana == 8 && Casts(boss) == 1);
    EndTurn(g);
    bool playerNext = g.CurrentPlayer == player;
    var clone = Fight("loot_53_inara");
    var real = clone.Clone();
    BossTurnWith(clone, 10);
    Check("I5 then the run player plays; a clone replays the cast on its own", playerNext && Casts(clone.Player2) == 1 && Casts(real.Player2) == 0);
    // Medivh's cards.
    var g2 = Fight("loot_53_inara");
    ToBoss(g2);
    var spell = ToHand(g2, g2.Player2, Fireball); var minion = ToHand(g2, g2.Player2, Yeti);
    bool insight = Cast(g2, "KARA_00_05");
    bool free = spell.Cost == 0 && minion.Cost == 4;
    Round(g2);
    Check("I6 Archmage's Insight (3): her spells cost (0) this turn only; minions keep their Cost", insight && free && spell.Cost == 4 && C("KARA_00_05").Cost == 3, "cost " + spell.Cost);
    bool portal = Cast(g2, "KARA_00_07");
    var summoned = g2.Player2.BoardZone.LastOrDefault();
    while (!g2.Player2.BoardZone.IsFull) Board(g2, g2.Player2, Wisp);
    var held = ToHand(g2, g2.Player2, "KARA_00_07");
    Mana(g2.Player2);
    Check("I7 Astral Portal (1) summons a random Legendary minion; it needs a free board slot", portal && summoned != null && summoned.Card.Rarity == Rarity.LEGENDARY && summoned.Card.Implemented
        && !g2.Player2.Options().OfType<PlayCardTask>().Any(o => o.Source == held), summoned?.Card.Id);
    var g3 = Fight("loot_53_inara");
    ToBoss(g3);
    var apprentice = Board(g3, g3.Player2, "KARA_00_08");
    int deck = g3.Player2.DeckZone.Count;
    Cast(g3, Moonfire, g3.Player1.Hero);
    Check("I8 Archmage's Apprentice (2/4 for 2): whenever its owner casts a spell a copy of it is shuffled into its owner's deck", apprentice.AttackDamage == 2 && apprentice.Health == 4 && C("KARA_00_08").Cost == 2
        && g3.Player2.DeckZone.Count == deck + 1 && g3.Player2.DeckZone.Count(p => p.Card.Id == Moonfire) == 1);
    ToPlayer(g3);
    Cast(g3, Moonfire, g3.Player2.Hero);
    Check("I9 the opponent's spell is not copied", g3.Player2.DeckZone.Count(p => p.Card.Id == Moonfire) == 1 && g3.Player1.DeckZone.Count(p => p.Card.Id == Moonfire) == 0);
});

// ============================================================ J  A. F. Kay: Idle
Guard("Kay", () =>
{
    var plan = RowPlan("loot_54_kay");
    var row = data.Boss("loot_54_kay");
    Check("J1 the row's plan: A. F. Kay with Idle (2), the wiki's 30 cards with Bag of Stuffing and Boots of Haste (2 each); the two Fel Reavers the engine does not script are Bittertide Hydras",
        plan.BossHero.Id == "LOOTA_BOSS_54h" && plan.BossHeroPowerId == "LOOTA_BOSS_54p" && plan.BossDeck.Count == 30 && plan.BossDeck.Count(c => c.Id == "LOOTA_823") == 2
        && plan.BossDeck.Count(c => c.Id == "LOOTA_812") == 2 && plan.BossDeck.Count(c => c.Id == "UNG_087") == 4 && row.Source.Kind == "substituted"
        && row.Source.Substitutions.Count == 2 && row.Source.Substitutions.All(s => s.Listed == "GVG_016" && s.Used == "UNG_087" && s.Reason == "unscripted") && !C("GVG_016").Implemented);
    var g = Fight("loot_54_kay");
    Controller boss = g.Player2, player = g.Player1;
    int hand = boss.HandZone.Count;
    EndTurn(g);
    Check("J2 the room's rule is on her from the first turn start; she keeps her hand and draws as ever", Rules(boss) == (EncounterRules.RoomEntered | EncounterRules.RoomNoPlay) && !EncounterRules.NeverDraws(boss)
        && boss.HandZone.Count == hand + 1 && boss.HandZone.Count > 0);
    Mana(boss);
    var given = Board(g, boss, "CS2_124"); // Wolfrider: Charge
    int hp = player.Hero.Health;
    Check("J3 her first turn with 10 Mana: no card of her hand can be played; Idle (2, clickable) and attacks stay", !PlayOptions(boss) && Offered(g) && AttackOptions(boss) && boss.Hero.HeroPower.Cost == 2
        && !boss.Hero.HeroPower.IsPassiveHeroPower);
    g.Process(MinionAttackTask.Any(boss, given, player.Hero));
    int board = boss.BoardZone.Count, cards = boss.HandZone.Count;
    bool idle = Power(g);
    Check("J4 Idle does nothing (2 Mana paid, nothing else changes); a minion given to her attacks", idle && boss.UsedMana == 2 && boss.BoardZone.Count == board && boss.HandZone.Count == cards && player.Hero.Health == hp - 3);
    bool blocked = true;
    for (int turn = 2; turn <= 5; turn++) { Round(g); blocked &= !PlayOptions(boss) && boss.Hero.HeroPower[GameTag.TAG_SCRIPT_DATA_NUM_1] == turn; }
    Check("J5 through her fifth turn she plays no card", blocked && EncounterRules.PlaysNoCards(boss));
    var clone = g.Clone();
    Round(g);
    Check("J6 from her sixth turn her hand is playable", boss.Hero.HeroPower[GameTag.TAG_SCRIPT_DATA_NUM_1] == 6 && !EncounterRules.PlaysNoCards(boss) && PlayOptions(boss) && Rules(boss) == EncounterRules.RoomEntered);
    Round(clone);
    Check("J7 a clone taken on her fifth turn frees her on its own sixth", PlayOptions(clone.Player2) && !EncounterRules.PlaysNoCards(clone.Player2));
});

// ============================================================ K  King Togwaggle: Magic Candle, Rakanishu
Guard("Togwaggle", () =>
{
    var plan = RowPlan("loot_99_togwaggle");
    var treasures = DungeonRunEncounters.TogwaggleTreasures;
    Check("K1 the row's plan: King Togwaggle with Magic Candle (3), a major of act I, the wiki's 30 cards; sixteen Treasures to find", plan.BossHero.Id == "LOOTA_BOSS_99h" && plan.BossHeroPowerId == "LOOTA_BOSS_99p"
        && plan.BossHealth == 40 && plan.BossDeck.Count == 30 && data.Boss("loot_99_togwaggle").Source.Kind == "authentic" && treasures.Count == 16 && treasures.Distinct().Count() == 16
        && treasures.All(id => C(id).Implemented) && C("LOOTA_BOSS_99p").Cost == 3);
    var g = Fight("loot_99_togwaggle");
    Controller boss = g.Player2, player = g.Player1;
    ToBoss(g);
    int hand = boss.HandZone.Count;
    bool used = Offered(g) && Power(g);
    Check("K2 Magic Candle puts one of his sixteen Treasures into his hand; once a turn; 3 Mana", used && boss.HandZone.Count == hand + 1 && treasures.Contains(boss.HandZone.Last().Card.Id) && !Offered(g)
        && boss.UsedMana == 3 && !boss.Hero.HeroPower.IsPassiveHeroPower && boss.Hero.HeroPower[EncounterRules.PowerActive] == 1, boss.HandZone.Last().Card.Id);
    var found = new HashSet<string>();
    int rakanishu = 0, turns = 0;
    for (int i = 0; i < 240 && !Over(g); i++)
    {
        foreach (var c in boss.HandZone.ToArray()) { boss.HandZone.Remove(c); boss.SetasideZone.Add(c); }
        ClearBoard(g, player);
        player.Hero.Damage = 0; boss.Hero.Damage = 0;
        int hp = player.Hero.Health;
        Entity.FromCard(player, C(Yeti), zone: player.DeckZone); Entity.FromCard(boss, C(Yeti), zone: boss.DeckZone);
        int castsBefore = boss.GraveyardZone.Count(c => c.Card.Id == Rakanishu);
        Round(g);
        turns++;
        int castNow = boss.GraveyardZone.Count(c => c.Card.Id == Rakanishu) - castsBefore;
        if (castNow == 1 && player.Hero.Health == hp - 4) rakanishu++;
        if (Power(g)) found.Add(boss.HandZone.Last().Card.Id);
    }
    Check("K3 over 240 turns every one of the sixteen is found", found.Count == 16, found.Count + ": " + string.Join(",", treasures.Where(id => !found.Contains(id))));
    Check("K4 at the start of his turn he casts Rakanishu (4 damage to a random enemy) now and then: about one turn in four", turns == 240 && rakanishu >= 40 && rakanishu <= 80, rakanishu + " of " + turns);
    FillHand(g, boss);
    Round(g);
    Check("K5 with a full hand the power is not offered", boss.HandZone.IsFull && !Offered(g));
    var g2 = Fight("loot_99_togwaggle");
    ToBoss(g2);
    var bystander = Board(g2, g2.Player2, Yeti);
    int php = g2.Player1.Hero.Health;
    void CastRakanishu(Game game) => new EffectContext(game, game.Player2.Id, game.Player2.Hero.HeroPower.Id).CastSpell(Entity.FromCard(game.Player2, C(Rakanishu)).Id, HistoryCastKind.Effect);
    CastRakanishu(g2);
    var inHand = ToHand(g2, g2.Player2, Rakanishu);
    Mana(g2.Player2);
    Check("K6 Rakanishu (0 Mana), cast by the power, deals 4 damage to a random enemy (the lone run hero here), never to his own side; the client card cannot be played from a hand",
        C(Rakanishu).Cost == 0 && g2.Player1.Hero.Health == php - 4 && bystander.Damage == 0 && !g2.Player2.Options().OfType<PlayCardTask>().Any(o => o.Source == inHand));
    var g3 = Fight("loot_99_togwaggle");
    ToBoss(g3);
    Board(g3, g3.Player2, "CS2_142"); // Kobold Geomancer: Spell Damage +1
    int php3 = g3.Player1.Hero.Health;
    CastRakanishu(g3);
    Check("K7 it is a spell: Spell Damage adds to it (5)", g3.Player1.Hero.Health == php3 - 5);
    var g4 = Fight("loot_99_togwaggle");
    var c4 = g4.Clone();
    int before = g4.Player2.HandZone.Count;
    ToBoss(c4); Power(c4);
    Check("K8 a clone finds its Treasure in the clone only", c4.Player2.HandZone.Count(c => treasures.Contains(c.Card.Id)) == 1 && g4.Player2.HandZone.Count == before);
});

// ============================================================ L  the sixteen Treasures in a boss's hands
Guard("Treasures", () =>
{
    Game Lair() { var lair = Fight("loot_99_togwaggle"); ToBoss(lair); lair.Player2.Hero.HeroPower.ActivatedTrigger?.Remove(); return lair; }
    var t = DungeonRunEncounters.TogwaggleTreasures;
    var notBoss = t.Where(id => !Catalog(id, AdmitScope.Boss) || !Catalog(id, AdmitScope.Player)).ToList();
    var legacyBoss = t.Where(id => id != "LOOT_998k" && (Legacy(id, AdmitScope.Boss) || !Legacy(id, AdmitScope.Player))).ToList();
    Check("L0 each of the sixteen is admitted for a boss on a catalog run and stays the run player's; the legacy verdict of the fifteen run treasures is unchanged (the run player's only)", notBoss.Count == 0 && legacyBoss.Count == 0
        && DungeonRunAdmission.RuleFor("LOOTA_806").Family == "run-treasure-boss-find" && DungeonRunAdmission.RuleFor("LOOTA_839").Family == "run-treasure-side-shuffle"
        // (family 16 moved LOOTA_811 to its run-treasure-boss-shuffle row: Wish is the core row's example now)
        && DungeonRunAdmission.RuleFor("LOOTA_814").Family == "run-treasure" && !Catalog("LOOTA_814", AdmitScope.Boss) && !Catalog("LOOTA_840", AdmitScope.Boss),
        string.Join(",", notBoss.Concat(legacyBoss)));
    var l1 = Lair();
    var theirs = Board(l1, l1.Player1, Yeti); var own = Board(l1, l1.Player2, Yeti);
    Cast(l1, "LOOTA_806");
    Check("L1 Wand of Disintegration: the run player's minions are silenced and destroyed, his own stay", theirs.Zone != l1.Player1.BoardZone && own.Zone == l1.Player2.BoardZone);
    var l2 = Lair();
    var m2 = ToHand(l2, l2.Player2, Yeti); var s2 = ToHand(l2, l2.Player2, Fireball);
    Cast(l2, "LOOTA_812");
    bool zero = m2.Cost == 0 && s2.Cost == 4;
    Round(l2);
    Check("L2 Boots of Haste: his minions cost (0) this turn only", zero && m2.Cost == 4);
    var l3 = Lair();
    Play(l3, ToHand(l3, l3.Player2, "LOOTA_816"));
    Cast(l3, Fireball, l3.Player1.Hero);
    var partied = l3.Player2.BoardZone.LastOrDefault();
    Check("L3 Party Portal: his spell summons a random minion of the same Cost (4)", l3.Player2.BoardZone.Count == 2 && partied.Card.Cost == 4 && partied.Card.Id != "LOOTA_816", partied?.Card.Id);
    var l4 = Lair();
    Play(l4, ToHand(l4, l4.Player2, "LOOTA_819"));
    foreach (var c in l4.Player2.HandZone.ToArray()) { l4.Player2.HandZone.Remove(c); l4.Player2.SetasideZone.Add(c); }
    EmptyDeck(l4.Player2);
    Round(l4);
    var staffed = l4.Player2.HandZone.FirstOrDefault();
    Check("L4 Archmage Staff: at the start of his turn a random scripted Mage spell joins his hand", l4.Player2.Hero.Weapon?.Card.Id == "LOOTA_819" && staffed != null && staffed.Card.Type == CardType.SPELL
        && staffed.Card.Class == CardClass.MAGE && staffed.Card.Implemented, staffed?.Card.Id);
    var l5 = Lair();
    Play(l5, ToHand(l5, l5.Player2, "LOOTA_821"));
    var dagger = l5.Player2.Hero.Weapon;
    Check("L5 Vorpal Dagger: a Poisonous weapon with Mega-Windfury on his hero", dagger?.Card.Id == "LOOTA_821" && dagger.Card[GameTag.POISONOUS] == 1 && dagger.Card[GameTag.MEGA_WINDFURY] == 1 && l5.Player2.Hero.AttackDamage == 1);
    var l6 = Lair();
    Cast(l6, "LOOTA_823");
    Check("L6 Bag of Stuffing: he draws until his hand is full", l6.Player2.HandZone.IsFull);
    var l7 = Lair();
    Play(l7, ToHand(l7, l7.Player2, "LOOTA_826"));
    var wall = l7.Player2.BoardZone.LastOrDefault();
    Check("L7 Portable Ice Wall: a 3/15 Taunt that cannot attack", wall?.Card.Id == "LOOTA_826" && wall.HasTaunt && wall.CantAttack && wall.Health == 15);
    var l8 = Lair();
    int hp8 = l8.Player1.Hero.Health; l8.Player1.Hero.BaseHealth = 100; l8.Player1.Hero.Damage = 0;
    Cast(l8, "LOOTA_827");
    Check("L8 Embers of Ragnaros: three fireballs of 8 at random enemies (24 at the lone run hero)", l8.Player1.Hero.Damage == 24, "damage " + l8.Player1.Hero.Damage);
    var l9 = Lair();
    Play(l9, ToHand(l9, l9.Player2, "LOOTA_835"));
    Mana(l9.Player2, 5);
    l9.Process(HeroAttackTask.Any(l9.Player2, l9.Player1.Hero));
    Check("L9 Greedy Pickaxe: after his hero attacks he gains an empty Mana Crystal (5 to 6)", l9.Player2.BaseMana == 6, "mana " + l9.Player2.BaseMana);
    var l10 = Lair();
    Cast(l10, "LOOTA_836");
    Check("L10 Bag of Coins: his hand is full of Coins", l10.Player2.HandZone.IsFull && l10.Player2.HandZone.Any(c => c.Card.Id == "GAME_005"));
    var l11 = Lair();
    Cast(l11, "LOOTA_838");
    Check("L11 Dr. Boom's Boombox: seven Boom Bots", Count(l11.Player2, "GVG_110t") == 7);
    var l12 = Fight("loot_99_togwaggle", shrine: Shrine);
    ToBoss(l12);
    for (int i = 0; i < 3; i++) { Board(l12, l12.Player1, Yeti); Board(l12, l12.Player2, Croc); }
    var shrine = ShrineRules.Of(l12.Player1).First();
    Cast(l12, "LOOTA_839");
    int total = l12.Player1.BoardZone.Count + l12.Player2.BoardZone.Count;
    Check("L12 Scroll of Confusion: every minion picks a side; none is lost and the run shrine never changes sides", total == 7 && shrine.Controller == l12.Player1
        && (l12.Player2.BoardZone.Any(m => m.Card.Id == Yeti) || l12.Player1.BoardZone.Any(m => m.Card.Id == Croc)), l12.Player1.BoardZone.Count + "/" + l12.Player2.BoardZone.Count);
    var l13 = Lair();
    var buffed = Board(l13, l13.Player2, Yeti);
    Cast(l13, "LOOTA_842b", buffed);
    Check("L13 Hilt of Quel'Delar: a minion gets +3/+3", buffed.AttackDamage == 7 && buffed.Health == 8);
    var l14 = Lair();
    var burned = Board(l14, l14.Player1, Yeti);
    int deck14 = l14.Player2.DeckZone.Count;
    Cast(l14, "LOOTA_843");
    Check("L14 THE CANDLE: 4 damage to the run player's minions, and it shuffles itself into his deck", burned.Damage == 4 && l14.Player2.DeckZone.Count == deck14 + 1 && l14.Player2.DeckZone.Count(p => p.Card.Id == "LOOTA_843") == 1);
    var l15 = Lair();
    var model = Board(l15, l15.Player1, Croc);
    var inHand = ToHand(l15, l15.Player2, Yeti);
    Cast(l15, "LOOTA_847", model);
    Check("L15 Mask of Mimicry: the minions in his hand become copies of the chosen minion", l15.Player2.HandZone.Where(c => c is Minion).All(c => c.Card.Id == Croc) && l15.Player2.HandZone.Any(c => c.Card.Id == Croc));
    var l16 = Lair();
    ToHand(l16, l16.Player2, Yeti); ToHand(l16, l16.Player2, Fireball);
    var kobold = ToHand(l16, l16.Player2, "LOOT_998k");
    int held = l16.Player2.HandZone.Count - 1;
    Play(l16, kobold);
    Check("L16 Golden Kobold (6/6 Taunt): his hand is replaced by Legendary minions", l16.Player2.BoardZone.Any(m => m.Card.Id == "LOOT_998k") && l16.Player2.HandZone.Count == held
        && l16.Player2.HandZone.All(c => c is Minion && c.Card.Rarity == Rarity.LEGENDARY), string.Join(",", l16.Player2.HandZone.Select(c => c.Card.Id)));
});

// ============================================================ N  the run layer: no-loss endings and run deck edits
Guard("RunLayer", () =>
{
    // The Treasure Vault through the run: two Coffers destroyed, the doors close, the run records a win with no kill credit.
    var s = At("MAGE", 4400, "loot_52_vault");
    var plan = run.BuildPlan(s, all);
    int deck = s.Deck.Count, defeated = DungeonRun.BossesDefeated(s);
    var g = Ready(plan);
    run.BeginMatch(s);
    Kill(g, g.Player2.BoardZone[0]); Kill(g, g.Player2.BoardZone[0]);
    var asked = g.RunResult.Edits.Select(e => e.CardId).ToList();
    for (int turn = 1; turn <= 5 && !Over(g); turn++) { EndTurn(g); if (!Over(g)) EndTurn(g); }
    var outcome = g.Player1.PlayState == PlayState.WON ? DungeonRunOutcome.Win : DungeonRunOutcome.Loss;
    var notes = run.RecordOutcome(s, all, outcome, g.RunResult, DungeonRunSetup.Harvest(g));
    var summary = s.Matches.Last();
    Check("N1 the Vault's closing is recorded as won with the no-loss mark and no boss kill credit; the run goes on", Over(g) && outcome == DungeonRunOutcome.Win && summary.Outcome == DungeonRunOutcome.Win
        && summary.NoLoss && DungeonRun.BossesDefeated(s) == defeated && !s.Finished && s.Phase != DungeonRunPhase.InMatch && notes.Count == 0, string.Join(" | ", notes));
    Check("N2 the two treasures its Coffers added are in the run deck (origin match)", s.Deck.Count == deck + 2 && asked.All(id => s.Deck.Contains(id)) && s.Cards.Count(c => c.Origin == DungeonRunDeck.OriginMatch) == 2,
        deck + "->" + s.Deck.Count + " " + string.Join(",", asked));
    // The same ending reported as a loss (the model's first convention) is a no-loss win too.
    var s2 = At("MAGE", 4401, "loot_52_vault");
    int defeated2 = DungeonRun.BossesDefeated(s2);
    run.BeginMatch(s2);
    run.RecordOutcome(s2, all, DungeonRunOutcome.Loss, null, new RunMatchEnd(60, 0, 0, 0, new[] { "HERO_08" }, true));
    Check("N3 a no-loss ending reported as a loss is recorded the same way", s2.Matches.Last().Outcome == DungeonRunOutcome.Win && s2.Matches.Last().NoLoss && !s2.Finished && DungeonRun.BossesDefeated(s2) == defeated2);
    // The Vault destroyed: an ordinary win with kill credit.
    var s3 = At("MAGE", 4402, "loot_52_vault");
    int defeated3 = DungeonRun.BossesDefeated(s3);
    var g3 = Ready(run.BuildPlan(s3, all));
    run.BeginMatch(s3);
    g3.Player2.Hero.Damage = 59; Mana(g3.Player1);
    g3.Process(HeroPowerTask.Any(g3.Player1, g3.Player2.Hero));
    run.RecordOutcome(s3, all, DungeonRunOutcome.Win, g3.RunResult, DungeonRunSetup.Harvest(g3));
    Check("N4 a destroyed Vault is an ordinary win with its kill credit", Over(g3) && !s3.Matches.Last().NoLoss && DungeonRun.BossesDefeated(s3) == defeated3 + 1);
    // Tad's Pole joins the run deck after the Sunken Chest.
    var s4 = At("MAGE", 4403, "loot_22_tad");
    int deck4 = s4.Deck.Count;
    var g4 = Ready(run.BuildPlan(s4, all));
    run.BeginMatch(s4);
    var chest = Board(g4, g4.Player2, SunkenChest);
    Kill(g4, chest);
    g4.Process(EndTurnTask.Any(g4.CurrentPlayer));
    var notes4 = run.RecordOutcome(s4, all, DungeonRunOutcome.Win, g4.RunResult, DungeonRunSetup.Harvest(g4));
    string poleError = DungeonRunDeck.AddError(run, s4, Pole, false, out string why);
    Check("N5 breaking the Sunken Chest wins the fight (with kill credit) and Tad's Pole joins the run deck through the run's deck checks", Over(g4) && g4.Player1.PlayState == PlayState.WON && notes4.Count == 0
        && s4.Deck.Count == deck4 + 1 && s4.Deck.Contains(Pole) && !s4.Matches.Last().NoLoss && poleError == null, string.Join(" | ", notes4) + " " + why);
    // A lost fight drops the edits.
    var s5 = At("MAGE", 4404, "loot_52_vault");
    int deck5 = s5.Deck.Count;
    var g5 = Ready(run.BuildPlan(s5, all));
    run.BeginMatch(s5);
    Kill(g5, g5.Player2.BoardZone[0]);
    run.RecordOutcome(s5, all, DungeonRunOutcome.Loss, g5.RunResult, DungeonRunSetup.Harvest(g5));
    Check("N6 a fight that is really lost (a concession) keeps nothing of the Coffers and ends the run", s5.Finished && s5.Deck.Count == deck5);
});

// ============================================================ M  the rows, the admission rows, the validator
Guard("Rows", () =>
{
    var errors = DungeonRunValidator.Validate(data, null);
    Check("M1 the shipped data validates with the eleven family 09 rows", errors.Count == 0, string.Join(" | ", errors.Take(4)));
    var rows = new (string id, string hero, string power, string tier, int health, int offset, string kind, int deck, string[] flags)[]
    {
        ("loot_20_brimstone", "LOOTA_BOSS_20h", "LOOTA_BOSS_20p", "2", 0, 0, "authentic", 30, new[] { "start", "boardStart", "passive" }),
        ("loot_22_tad", "LOOTA_BOSS_22h", "LOOTA_BOSS_22p", "3", 20, 0, "authentic", 10, new[] { "listedDeck" }),
        ("loot_50_max", "LOOTA_BOSS_50h", "LOOTA_BOSS_50p", "2", 0, 0, "authentic", 30, new string[0]),
        ("loot_52_vault", "LOOTA_BOSS_52h", "LOOTA_BOSS_52p", "3", 60, 0, "curated", 10, new[] { "start", "boardStart", "listedDeck", "noLoss", "passive" }),
        ("loot_53_inara", "LOOTA_BOSS_53h", "LOOTA_BOSS_53p", "3", 0, 0, "authentic", 30, new[] { "passive" }),
        ("loot_54_kay", "LOOTA_BOSS_54h", "LOOTA_BOSS_54p", "2", 0, 0, "substituted", 30, new string[0]),
        ("loot_25_vustrasz", "LOOTA_BOSS_25h", "LOOTA_BOSS_25p", "major1", 0, 10, "authentic", 30, new[] { "start", "boardStart" }),
        ("loot_26_xol", "LOOTA_BOSS_26h", "LOOTA_BOSS_26p8", "major1", 0, 10, "authentic", 30, new[] { "passive" }),
        ("loot_34_azari", "LOOTA_BOSS_34h", "LOOTA_BOSS_34p", "major1", 0, 10, "authentic", 30, new[] { "hiddenZone", "passive" }),
        ("loot_49_darkness", "LOOTA_BOSS_49h", "LOOTA_BOSS_49p", "major1", 0, 10, "authentic", 30, new[] { "start" }),
        ("loot_99_togwaggle", "LOOTA_BOSS_99h", "LOOTA_BOSS_99p", "major1", 0, 10, "authentic", 30, new string[0]),
    };
    Check("M2 family 09 holds exactly these eleven rows", data.Encounters.Where(b => b.Family == "09").Select(b => b.Id).OrderBy(i => i, StringComparer.Ordinal)
        .SequenceEqual(rows.Select(r => r.id).OrderBy(i => i, StringComparer.Ordinal)), string.Join(",", data.Encounters.Where(b => b.Family == "09").Select(b => b.Id)));
    foreach (var r in rows)
    {
        var b = data.Boss(r.id);
        var plan = RowPlan(r.id);
        bool passive = r.flags.Contains("passive");
        Check("M3 " + r.id + ": a Dungeon Run row of tier " + r.tier + " whose plan is the row's (hero, power, a " + r.deck + "-card " + r.kind + " deck"
            + (r.health > 0 ? ", its own " + r.health + " Health" : ", the act curve's Health" + (r.offset > 0 ? " plus " + r.offset : "")) + "), flags [" + string.Join(",", r.flags) + "]",
            b.Provenance == "LOOT" && b.Tier == r.tier && b.HeroCard == r.hero && b.Power == r.power && b.Source.Kind == r.kind && b.Flags.OrderBy(f => f).SequenceEqual(r.flags.OrderBy(f => f))
            && plan.EncounterId == r.id && plan.EncounterDeck && plan.BossHero.Id == r.hero && plan.BossHeroPowerId == r.power && plan.BossDeck.Count == r.deck && data.BossDecks[b.Deck].Count == r.deck
            && b.HealthOffset == r.offset && (r.health > 0 ? b.FixedHealth && b.Health == r.health && plan.BossHealth == r.health : !b.FixedHealth && b.Health == null && plan.BossHealth == 30 + r.offset)
            && C(r.hero).Type == CardType.HERO && C(r.power).Type == CardType.HERO_POWER && DungeonRunEncounters.PassivePowers.Contains(r.power) == passive
            && data.Schedules.Member(b, DungeonRunEncounterData.PoolOf(b.Tier)) && b.Source.Url.StartsWith("https://hearthstone.wiki.gg/wiki/") && !b.BossGeneratedSecrets,
            b.Source.Kind + " health " + plan.BossHealth + " flags " + string.Join(",", b.Flags));
    }
    string[] scripted = { "LOOTA_BOSS_20p", "LOOTA_BOSS_22p", "LOOTA_BOSS_25p", "LOOTA_BOSS_26p8", "LOOTA_BOSS_34p", "LOOTA_BOSS_49p", "LOOTA_BOSS_50p", "LOOTA_BOSS_52p", "LOOTA_BOSS_53p",
        "LOOTA_BOSS_54p", "LOOTA_BOSS_99p", "LOOTA_BOSS_20t", "LOOTA_BOSS_22t", "LOOTA_103", "LOOTA_BOSS_26p2", "LOOTA_BOSS_26p3", "LOOTA_BOSS_26p4", "LOOTA_BOSS_26p5", "LOOTA_BOSS_26p6",
        "LOOTA_BOSS_26p7", "LOOTA_BOSS_49t", "LOOTA_BOSS_49t2", "LOOTA_BOSS_50t", "LOOTA_BOSS_52t", "LOOTA_BOSS_99t", "BRMA12_8t", "KARA_00_05", "KARA_00_07", "KARA_00_08" };
    string[] powers = scripted.Take(11).ToArray();
    var unscripted = scripted.Where(id => !C(id).Implemented || DungeonRunEncounters.Find(id)?.Family != "09").ToList();
    string Plain(string text) => (text ?? "").Replace("$", "").Replace("#", "").Replace(" @spelldmg", "");
    var retexted = scripted.Where(id => Plain(factById[id].TryGetProperty("text", out var text) ? text.GetString() : null) != Plain(C(id).Text)).ToList();
    Check("M4 every power and card of the family is scripted by family 09 and keeps the client's text", unscripted.Count == 0 && retexted.Count == 0,
        string.Join(",", unscripted.Concat(retexted.Select(id => id + " [" + C(id).Text + "]"))));
    var wrongScope = scripted.Where(id =>
    {
        var v = DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss, null, catalogRun: true);
        return !(v.Admitted && v.Subject && v.Rule?.Ids != null && v.Rule.Ids.Contains(id) && Legacy(id, AdmitScope.Boss)
            && Catalog(id, AdmitScope.Player) == !powers.Contains(id) && !Catalog(id, AdmitScope.Generated)
            // Tad's Pole joins the run deck by a run deck edit, which a never-generated card could not: the boss's generation alone rejects it.
            && (id == Pole ? DungeonRunAdmission.GeneratedRejectsBoss(true).Contains(id) && !DungeonRunAdmission.GeneratedRejects(true).Contains(id)
                : DungeonRunAdmission.GeneratedRejects(true).Contains(id) && DungeonRunAdmission.GeneratedRejects(false).Contains(id)));
    }).ToList();
    Check("M5 each has its own admission row: the eleven powers a boss's only, the boss cards a boss's and (held by the run player) the run player's; none is ever generated", wrongScope.Count == 0, string.Join(",", wrongScope));
    Check("M6 the five rule-cast and passive powers are in the passive registry (never offered); Catch and Release, Idle and Magic Candle are clickable powers whose trigger is started at match start",
        new[] { "LOOTA_BOSS_20p", "LOOTA_BOSS_26p8", "LOOTA_BOSS_34p", "LOOTA_BOSS_52p", "LOOTA_BOSS_53p" }.All(id => DungeonRunCardsGen.PassiveHeroPowers.Contains(id) && DungeonRunEncounters.NeedsActivation(id))
        && new[] { "LOOTA_BOSS_22p", "LOOTA_BOSS_54p", "LOOTA_BOSS_99p" }.All(id => !DungeonRunCardsGen.PassiveHeroPowers.Contains(id) && DungeonRunEncounters.NeedsActivation(id))
        && new[] { "LOOTA_BOSS_25p", "LOOTA_BOSS_49p", "LOOTA_BOSS_50p" }.All(id => !DungeonRunCardsGen.PassiveHeroPowers.Contains(id) && !DungeonRunEncounters.NeedsActivation(id)));
    DungeonRunData Changed(Action<DungeonRunData> change) { var d = Load(); change(d); return d; }
    var noFlag = DungeonRunValidator.Validate(Changed(d => d.Boss("loot_22_tad").Flags.Remove("listedDeck")), null);
    Check("M7 Tad's ten-card deck is allowed by the flag listedDeck alone: without it the combat-tier row is refused (30 cards expected)", noFlag.Any(e => e.Contains("has 10 cards, expected 30")), string.Join(" | ", noFlag.Take(3)));
    var tooSmall = DungeonRunValidator.Validate(Changed(d => d.BossDecks[d.Boss("loot_22_tad").Deck].RemoveAt(0)), null);
    Check("M8 a listedDeck row still needs 10 to 60 cards", tooSmall.Any(e => e.Contains("has 9 cards, expected 10 to 60")), string.Join(" | ", tooSmall.Take(3)));
    var kayTreasure = DungeonRunValidator.Validate(Changed(d => d.BossDecks[d.Boss("loot_54_kay").Deck][0] = "LOOTA_840"), null);
    Check("M9 a run treasure no row admits for a boss (Wax Rager) is refused in a boss deck", kayTreasure.Any(e => e.Contains("LOOTA_840") && e.Contains("run-treasure")), string.Join(" | ", kayTreasure.Take(3)));
    var playerPower = DungeonRunAdmission.Classify(C("LOOTA_BOSS_52p"), "LOOTA_BOSS_52p", AdmitScope.Player, null, catalogRun: true);
    Check("M10 a family 09 power asked for the run player is refused with its row's reason", !playerPower.Admitted && playerPower.Family == "boss-room" && playerPower.Lift == DungeonRunAdmission.NoneBossPower);
    Check("M11 the start blocks: Brimstone 4 Stone Golems, Vustrasz 5 Master Chests, the Vault 7 Coffers (boss board); The Darkness 3 Candles (the run player's hand); Tad, Brimstone and the Vault pin the boss's bonus Mana at 0",
        data.Boss("loot_20_brimstone").Start.BossBoard.Count == 4 && data.Boss("loot_25_vustrasz").Start.BossBoard.Count == 5 && data.Boss("loot_52_vault").Start.BossBoard.Count == 7
        && data.Boss("loot_49_darkness").Start.PlayerHandAdd.Count == 3 && data.Boss("loot_22_tad").Start.BossMana == 0 && data.Boss("loot_20_brimstone").Start.BossMana == 0
        && data.Boss("loot_52_vault").Start.BossMana == 0 && data.Boss("loot_25_vustrasz").Start.BossMana == null);
    Check("M12 Inara the Mage (LOOTA_BOSS_53h2) is a portrait support card: a hero card with her Health and power, no row of its own", C("LOOTA_BOSS_53h2").Type == CardType.HERO && C("LOOTA_BOSS_53h2").Health == 50
        && data.Bosses.All(b => b.HeroCard != "LOOTA_BOSS_53h2" && b.Forms.All(f => f.Hero != "LOOTA_BOSS_53h2")));
    Check("M13 the private tag of the room rules is in the encounter range (never sent to the client)", EncounterRules.IsPrivateTag(EncounterRules.RoomRules) && (int)EncounterRules.RoomRules == 32644);
});

Console.WriteLine($"SearchTelemetry swallowed leaf exceptions {SearchTelemetry.SwallowedLeafErrors}");
Console.WriteLine($"RESULT {passes} passed, {failures + SearchTelemetry.SwallowedLeafErrors} failed");
return failures + SearchTelemetry.SwallowedLeafErrors;
