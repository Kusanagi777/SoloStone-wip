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
using TombsB = SabberStoneCore.CardSets.DungeonRun.DungeonRunEncounters.TombsB;

// Phase 8 family 20, Tombs of Terror bosses B (real engine games through the run setup path: DungeonRun.BuildPlan on the shipped
// row -> DungeonRunSetup.Configure / ApplyAfterStart): Sothis (Titan Ritual; Mogu Cultist and Highkeeper Ra), Kham (Spectral
// Swordsman), Illidara Sunsdawn (Staking A Claim), Colossus of the Sun (Power of Wrath), Skarik (Hatching Time), Suspicious Palm
// Tree (Corrupted Oasis), Beetle Herder Zenda (Beetle Stampede), Thudd Lockspring (Gatling Wand), Ol' Toomba ("Retired" Treasure
// Hunter), Sazin (Mighty Windstorm), Ozara (Mother of Sand), Ermavar (Fireshaper), Pillager Drasar (Pillaged Relics), Edra
// (Whispering Sentinel), Runaway Ruby (Out of Control), Dovo Fastfuse (Tomb Boomin'), Earthrager Ptah (Vengeful Construct) and
// Kasa Softspeaker (Madness Incarnate), with the shrine and hidden-hand cases that touch them, and the rows, the tier rule, the
// admission rows and the validator.
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
string Text(string id) => factById[id].TryGetProperty("text", out var t) ? t.GetString() : null;
int FactCost(string id) => factById[id].GetProperty("tags").EnumerateArray().Where(t => t[0].GetInt32() == 48).Select(t => t[1].GetInt32()).FirstOrDefault();
string root = VerifyPaths.Data;
string R(string f) => File.ReadAllText(Path.Combine(root, f));
DungeonRunData Load() => DungeonRunData.Parse(R("dungeon-run/run-v1.json"), R("dungeon-run/pool-v1.json"), R("npc/decks-v1.json"), R("dungeon-run/catalog-v1.json"),
    R("dungeon-run/schedule-v1.json"), R("dungeon-run/tavern-v1.json"), R("dungeon-run/encounters-v1.json"));
var data = Load();
var run = new DungeonRun(data);
var all = new DungeonRunProfile(); all.Unlocked.AddRange(data.Unlocks.Keys);

const string Yeti = "CS2_182", Croc = "CS2_120", Raptor = "CS2_172", Moonfire = "CS2_008", Wisp = "CS2_231", Shrine = "TRLA_154";
const string WarAxe = "CS2_106", Reaper = "CS2_112", Scarab = "LOE_029", Archer = "CS2_189", Bolt = "EX1_238", Cultist = "ULD_705", Ra = "ULD_705t";
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
void EmptyBoard(Controller c) { foreach (var m in c.BoardZone.ToArray()) { c.BoardZone.Remove(m); c.SetasideZone.Add(m); } }
Minion Board(Game g, Controller c, string id, int position = -1) { var m = (Minion)Entity.FromCard(c, C(id)); Generic.SummonBlock(g, m, position, c.Hero); g.DeathProcessingAndAuraUpdate(); return m; }
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
void Hit(Game g, Minion m, int n) { new DamageTask(n, EntityType.TARGET).Process(g, m.Controller.Opponent, m.Controller.Opponent.Hero, m, null); g.DeathProcessingAndAuraUpdate(); }
bool Has(IPlayable p, string enchantmentId) => p.AppliedEnchantments?.Any(e => e.Card.Id == enchantmentId) == true;
int Count(IPlayable p, string enchantmentId) => p.AppliedEnchantments?.Count(e => e.Card.Id == enchantmentId) ?? 0;
int Cost(string id) => C(id).Cost;
bool Attack(Game g, Minion m, ICharacter target) => g.Process(MinionAttackTask.Any(g.CurrentPlayer, m, target));
bool HeroAttack(Game g, ICharacter target) { g.CurrentPlayer.Hero.IsExhausted = false; return g.Process(HeroAttackTask.Any(g.CurrentPlayer, target)); }
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
    && DungeonRunEncounters.PassivePowers.Contains(power) && g.Player2.Hero.HeroPower[EncounterRules.PowerActive] == 1 && !Offered(g);
int Lost(Controller c) => c.Hero.Damage + c.BoardZone.Sum(m => m.Damage);

// ============================================================ A  Sothis: Titan Ritual, Mogu Cultist, Highkeeper Ra
Guard("Sothis", () =>
{
    var g = Fight("ulda_19_sothis");
    Controller boss = g.Player2, player = g.Player1;
    var yeti = Board(g, player, Yeti);
    ToBoss(g);
    bool used = Power(g);
    Check("A1 Titan Ritual costs (2), clickable and untargeted: two 1/1 Mogu Cultists (ULD_705) for the boss, whose Battlecry does not run (summoned)",
        Cost("ULDA_BOSS_19p") == 2 && FactCost("ULDA_BOSS_19p") == 2 && Clickable(g) && used && boss.BoardZone.Count == 2 && boss.BoardZone.All(m => m.Card.Id == Cultist && m.AttackDamage == 1 && m.Health == 1)
        && boss.UsedMana == 2, string.Join(",", boss.BoardZone.Select(m => m.Card.Id)));
    for (int i = 0; i < 4; i++) Board(g, boss, Cultist);
    Round(g);
    Power(g);
    Check("A2 with one slot left it summons one", boss.BoardZone.Count == 7 && boss.BoardZone.All(m => m.Card.Id == Cultist));
    Round(g);
    Check("A3 with a full board it is not offered", !Offered(g));
    Kill(g, boss.BoardZone.Last());
    var played = (Minion)ToHand(g, boss, Cultist);
    int hp = player.Hero.Health, bossHp = boss.Hero.Health;
    bool ok = Play(g, played);
    var ra = boss.BoardZone.FirstOrDefault();
    Check("A4 Mogu Cultist played onto a board of six Mogu Cultists (full of them): all seven are destroyed and Highkeeper Ra (ULD_705t, 20/20) is summoned",
        ok && boss.BoardZone.Count == 1 && ra?.Card.Id == Ra && ra.AttackDamage == 20 && ra.Health == 20 && boss.GraveyardZone.Count(p => p.Card.Id == Cultist) >= 7 && played.Zone == boss.GraveyardZone,
        string.Join(",", boss.BoardZone.Select(m => m.Card.Id)));
    EndTurn(g);
    Check("A5 at the end of its owner's turn Highkeeper Ra deals 20 damage to every enemy: the run player's hero and minions; the boss's hero is untouched",
        player.Hero.Health == hp - 20 && yeti.Zone != player.BoardZone && boss.Hero.Health == bossHp, hp + "->" + player.Hero.Health);
    hp = player.Hero.Health;
    EndTurn(g);
    Check("A6 the run player's turn end does not trigger it", player.Hero.Health == hp && boss.Hero.Health == bossHp && ra.Zone == boss.BoardZone);
    var g2 = Fight("ulda_19_sothis");
    ToBoss(g2);
    for (int i = 0; i < 5; i++) Board(g2, g2.Player2, Cultist);
    Board(g2, g2.Player2, Yeti);
    Play(g2, ToHand(g2, g2.Player2, Cultist));
    Check("A7 a full board with one other minion among the Cultists: nothing happens", g2.Player2.BoardZone.Count == 7 && g2.Player2.BoardZone.Count(m => m.Card.Id == Cultist) == 6
        && !g2.Player2.BoardZone.Any(m => m.Card.Id == Ra));
    var g3 = Fight("ulda_19_sothis");
    for (int i = 0; i < 6; i++) Board(g3, g3.Player1, Cultist);
    Play(g3, ToHand(g3, g3.Player1, Cultist));
    Check("A8 the collectible works the same for the run player (Highkeeper Ra on the run player's board)", g3.Player1.BoardZone.Count == 1 && g3.Player1.BoardZone[0].Card.Id == Ra);
    Check("A9 Mogu Cultist and Highkeeper Ra are scripted (their client texts kept) and admitted in every scope", C(Cultist).Implemented && C(Ra).Implemented && C(Cultist).Text == Text(Cultist)
        && C(Ra).Text == Text(Ra) && C(Cultist).Power?.PowerTask != null && C(Ra).Power?.Trigger != null
        && new[] { AdmitScope.Player, AdmitScope.Boss, AdmitScope.Generated }.All(s => DungeonRunAdmission.Classify(C(Cultist), Cultist, s, null, catalogRun: true).Admitted
            && DungeonRunAdmission.Classify(C(Ra), Ra, s, null, catalogRun: true).Admitted) && DungeonRunAdmission.RuleFor(Cultist)?.Family == "tombs-b-collectible");
});

// ============================================================ B  Kham: Spectral Swordsman
Guard("Kham", () =>
{
    var g = Fight("ulda_20_kham");
    Controller boss = g.Player2, player = g.Player1;
    Check("B1 Spectral Swordsman is passive: started at match start, never an option; no weapon, no Stealth", PassiveStarted(g, "ULDA_BOSS_20p") && !boss.Hero.HasStealth);
    var raptor = Board(g, player, Raptor);
    ToBoss(g);
    var yeti = Board(g, boss, Yeti);
    Cast(g, WarAxe);
    Check("B2 the boss equips a weapon: its hero has Stealth", boss.Hero.Weapon?.Card.Id == WarAxe && boss.Hero.HasStealth);
    bool attacked = HeroAttack(g, raptor);
    Check("B3 its hero attacks with the weapon: the Stealth stays (the text ties it to the weapon only)", attacked && boss.Hero.HasStealth && boss.Hero.Weapon.Durability == 1);
    ToPlayer(g);
    var raider = Board(g, player, "CS2_168");
    Round(g); ToPlayer(g);
    var fire = ToHand(g, player, Moonfire);
    var spellTargets = player.Options().OfType<PlayCardTask>().Where(o => o.Source == fire).Select(o => o.Target).ToList();
    var attackTargets = player.Options().OfType<MinionAttackTask>().Where(o => o.Source == raider).Select(o => o.Target).ToList();
    Check("B4 the run player cannot aim a spell or an attack at the Stealthed hero (its minion can still be)", spellTargets.Contains(yeti) && !spellTargets.Contains(boss.Hero)
        && attackTargets.Contains(yeti) && !attackTargets.Contains(boss.Hero) && spellTargets.Count > 0, spellTargets.Count + " / " + attackTargets.Count);
    int bossHp = boss.Hero.Health;
    new DamageTask(1, EntityType.ENEMIES).Process(g, player, player.Hero, null, null); g.DeathProcessingAndAuraUpdate();
    Check("B5 an area effect still reaches it", boss.Hero.Health == bossHp - 1);
    ToBoss(g);
    Cast(g, Reaper);
    Check("B6 the weapon replaced by another: the Stealth stays", boss.Hero.Weapon?.Card.Id == Reaper && boss.Hero.HasStealth);
    HeroAttack(g, raider);
    Round(g);
    var w2 = Board(g, player, Wisp);
    HeroAttack(g, w2);
    g.DeathProcessingAndAuraUpdate();
    Check("B7 the last weapon breaks (out of Durability): the Stealth ends", boss.Hero.Weapon == null && !boss.Hero.HasStealth);
    Cast(g, WarAxe);
    new DestroyTask(EntityType.WEAPON).Process(g, boss, boss.Hero, null, null); g.DeathProcessingAndAuraUpdate();
    Check("B8 a weapon destroyed by an effect: the Stealth ends", boss.Hero.Weapon == null && !boss.Hero.HasStealth);
    ToPlayer(g);
    Cast(g, WarAxe);
    Check("B9 the run player's weapon gives the run player's hero no Stealth", player.Hero.Weapon != null && !player.Hero.HasStealth);
});

// ============================================================ C  Illidara Sunsdawn: Staking A Claim
Guard("Illidara", () =>
{
    var g = Fight("ulda_21_illidara");
    Controller boss = g.Player2, player = g.Player1;
    Check("C1 Staking A Claim is passive: started at match start, never an option", PassiveStarted(g, "ULDA_BOSS_21p") && TombsB.IsDiscoverCard(Entity.FromCard(player, C(Scarab))));
    var yeti = Board(g, player, Yeti);
    var croc = Board(g, boss, Croc);
    Play(g, ToHand(g, player, Raptor));
    Check("C2 a card without Discover played: nothing", !Has(yeti, "ULDA_BOSS_21e") && !Has(croc, "ULDA_BOSS_21e") && yeti.AttackDamage == 4);
    Play(g, ToHand(g, player, Scarab));
    if (player.Choice != null) g.Process(ChooseTask.Pick(player, player.Choice.Choices[0]));
    Check("C3 the run player plays a Discover card (Jeweled Scarab): every minion of both boards gains +1/+1 (ULDA_BOSS_21e)", Has(yeti, "ULDA_BOSS_21e") && Has(croc, "ULDA_BOSS_21e")
        && yeti.AttackDamage == 5 && yeti.Health == 6 && croc.AttackDamage == 3 && croc.Health == 4, yeti.AttackDamage + "/" + croc.AttackDamage);
    ToBoss(g);
    Play(g, ToHand(g, boss, Scarab));
    if (boss.Choice != null) g.Process(ChooseTask.Pick(boss, boss.Choice.Choices[0]));
    Check("C4 the boss plays one: again (+2/+2 in all)", Count(yeti, "ULDA_BOSS_21e") == 2 && Count(croc, "ULDA_BOSS_21e") == 2 && yeti.AttackDamage == 6);
    var g2 = Fight("ulda_21_illidara", shrine: Shrine);
    var shrine = ShrineRules.Of(g2.Player1).FirstOrDefault();
    Kill(g2, shrine);
    var dormant = ShrineRules.Of(g2.Player1).FirstOrDefault(ShrineRules.IsDormantForm);
    Play(g2, ToHand(g2, g2.Player1, Scarab));
    Check("C5 a dormant run shrine is left out", dormant != null && !Has(dormant, "ULDA_BOSS_21e"));
});

// ============================================================ D  Colossus of the Sun: Power of Wrath (cast by rule)
Guard("Colossus", () =>
{
    // The close-out review: the wiki marks Power of Wrath Auto-cast and the card carries AI_MUST_PLAY, so it is cast by rule
    // (family 09's AutoCast) at the start of the boss's turn, after the draw, never offered as a click.
    var g = Fight("ulda_22_colossus");
    Controller boss = g.Player2, player = g.Player1;
    var theirs = Board(g, player, Yeti); var fresh = Board(g, player, Croc);
    ToPlayer(g);
    EndTurn(g);
    Check("D1 Power of Wrath costs (0) and is cast by rule (passive in the registry, never an option; the card carries AI_MUST_PLAY); with no damaged minion it is not cast",
        Cost("ULDA_BOSS_22p") == 0 && FactCost("ULDA_BOSS_22p") == 0 && PassiveStarted(g, "ULDA_BOSS_22p") && C("ULDA_BOSS_22p")[(GameTag)367] == 1 && !Offered(g)
        && DungeonRunEncounters.AutoCasts(boss.Hero.HeroPower) == 0 && theirs.Damage == 0 && fresh.Damage == 0);
    var own = Board(g, boss, Yeti); var weak = Board(g, boss, Raptor);
    Hit(g, theirs, 1); Hit(g, own, 2); Hit(g, weak, 1);
    Round(g);
    Check("D2 at the boss's next turn start it deals 1 damage to every damaged minion of both boards (each once), the boss's own too; the undamaged ones are untouched",
        DungeonRunEncounters.AutoCasts(boss.Hero.HeroPower) == 1 && theirs.Damage == 2 && own.Damage == 3 && fresh.Damage == 0 && weak.Zone != boss.BoardZone,
        theirs.Damage + "/" + own.Damage + "/" + fresh.Damage + " casts " + DungeonRunEncounters.AutoCasts(boss.Hero.HeroPower));
    Check("D3 once per turn, never a click: not offered after the cast", !Offered(g) && DungeonRunEncounters.AutoCasts(boss.Hero.HeroPower) == 1);
});

// ============================================================ E  Skarik: Hatching Time
Guard("Skarik", () =>
{
    var g = Fight("ulda_23_skarik");
    Controller boss = g.Player2, player = g.Player1;
    var theirs = (IPlayable)ToHand(g, player, Raptor);
    ToBoss(g);
    EmptyHand(boss);
    var yeti = ToHand(g, boss, Yeti);
    Check("E1 Hatching Time costs (2); with no Beast in the boss's hand (a Beast of the run player's does not count) it is not offered", Cost("ULDA_BOSS_23p") == 2 && !Offered(g));
    var raptor = ToHand(g, boss, Raptor); var croc = ToHand(g, boss, Croc);
    bool used = Power(g);
    Check("E2 each Beast in the boss's hand costs (1) less (ULDA_BOSS_23e); the other cards and the run player's Beast are unchanged", used && raptor.Cost == 1 && croc.Cost == 1
        && Has(raptor, "ULDA_BOSS_23e") && yeti.Cost == 4 && theirs.Cost == 2 && boss.UsedMana == 2);
    Round(g);
    Power(g);
    Check("E3 used again: (2) less", raptor.Cost == 0 && croc.Cost == 0);
});

// ============================================================ F  Suspicious Palm Tree: Corrupted Oasis
Guard("PalmTree", () =>
{
    var g = Fight("ulda_24_palm_tree");
    Controller boss = g.Player2;
    ToBoss(g);
    EmptyHand(boss);
    bool used = Power(g);
    Check("F1 Corrupted Oasis costs (2): two 2/2 Treants (GIL_663t) in the boss's hand", Cost("ULDA_BOSS_24p") == 2 && used && boss.HandZone.Count == 2
        && boss.HandZone.All(p => p.Card.Id == TombsB.OasisTreant && p.Card.ATK == 2 && p.Card.Health == 2));
    Round(g);
    while (boss.HandZone.Count < 9) Generic.AddHandPhase.Invoke(boss, Entity.FromCard(boss, C(Yeti)));
    Power(g);
    Check("F2 with room for one it adds one", boss.HandZone.Count == 10 && boss.HandZone.Count(p => p.Card.Id == TombsB.OasisTreant) == 3);
    Round(g);
    FillHand(g, boss);
    Check("F3 with a full hand it is not offered", !Offered(g));
});

// ============================================================ G  Beetle Herder Zenda: Beetle Stampede
Guard("Zenda", () =>
{
    var g = Fight("ulda_25_zenda");
    Controller boss = g.Player2;
    ToBoss(g);
    bool used = Power(g);
    var beetle = boss.BoardZone.FirstOrDefault();
    Check("G1 Beetle Stampede costs (2): a 2/3 Plated Beetle (LOOT_413) for the boss", Cost("ULDA_BOSS_25p") == 2 && used && beetle?.Card.Id == TombsB.PlatedBeetle && beetle.AttackDamage == 2 && beetle.Health == 3);
    int armor = boss.Hero.Armor;
    Kill(g, beetle);
    Check("G2 its Deathrattle gives the boss 3 Armor", boss.Hero.Armor == armor + 3);
    Round(g);
    FillBoard(g, boss);
    Check("G3 with a full board it is not offered", !Offered(g));
});

// ============================================================ H  Thudd Lockspring: Gatling Wand
Guard("Thudd", () =>
{
    var g = Fight("ulda_26_thudd");
    Controller boss = g.Player2, player = g.Player1;
    ToBoss(g);
    Check("H1 Gatling Wand costs (2); with no enemy minion it is not offered", Cost("ULDA_BOSS_26p") == 2 && !Offered(g));
    ToPlayer(g);
    Board(g, player, Yeti); Board(g, player, Yeti);
    ToBoss(g);
    var own = Board(g, boss, Yeti);
    int heroes = player.Hero.Damage + boss.Hero.Damage;
    bool used = Power(g);
    Check("H2 it deals 2 damage split among the enemy minions only (heroes and the boss's minions untouched)", used && player.BoardZone.Sum(m => m.Damage) == 2 && own.Damage == 0
        && player.Hero.Damage + boss.Hero.Damage == heroes);
    Round(g);
    EmptyBoard(player);
    var lone = Board(g, player, Wisp);
    Power(g);
    Check("H3 a single 1/1: the first hit kills it, the second has no target (no other character is hit)", lone.Zone != player.BoardZone && player.Hero.Damage + boss.Hero.Damage == heroes);
});

// ============================================================ I  Ol' Toomba: "Retired" Treasure Hunter
Guard("Toomba", () =>
{
    var g = Fight("ulda_27_toomba");
    Controller boss = g.Player2, player = g.Player1;
    EmptyHand(player);
    ToBoss(g);
    EmptyHand(player);
    Check("I1 \"Retired\" Treasure Hunter costs (2); with the run player's hand empty it is not offered", Cost("ULDA_BOSS_27p") == 2 && !Offered(g));
    ToHand(g, player, Moonfire); ToHand(g, player, Archer);
    EmptyHand(boss);
    var (view, viewBoss, viewPlayer) = FairView(g);
    Check("I2 in the boss's fair view the run player's hand is masked: as many cards, none it may copy (the search never reads the hidden hand)", viewPlayer.HandZone.Count == 2
        && TombsB.CopyableHand(viewBoss).Count == 0 && TombsB.CopyableHand(boss).Count == 2);
    bool used = Power(g);
    var copy = boss.HandZone.FirstOrDefault();
    Check("I3 it puts a copy of a random card of the run player's hand into the boss's hand (a new entity of the boss's); the run player's hand is unchanged",
        used && boss.HandZone.Count == 1 && (copy.Card.Id == Moonfire || copy.Card.Id == Archer) && player.HandZone.Count == 2 && player.HandZone.All(p => p != copy)
        && copy.Controller == boss && boss.UsedMana == 2, copy?.Card.Id);
    var (view2, viewBoss2, _) = FairView(g);
    Check("I4 the boss knows its copy (its own hand)", viewBoss2.HandZone.Any(p => p.Card?.Id == copy.Card.Id && !(p is Unknown)));
    var seen = new HashSet<string> { copy.Card.Id };
    for (int i = 0; i < 6; i++)
    {
        Round(g);
        EmptyHand(player); EmptyHand(boss);
        Generic.AddHandPhase.Invoke(player, Entity.FromCard(player, C(Moonfire))); Generic.AddHandPhase.Invoke(player, Entity.FromCard(player, C(Archer)));
        Power(g);
        seen.Add(boss.HandZone.Last().Card.Id);
    }
    Check("I5 over seven uses both cards of the run player's hand were copied (the game RNG picks)", seen.SetEquals(new[] { Moonfire, Archer }), string.Join(",", seen));
    Round(g);
    FillHand(g, boss);
    Check("I6 with the boss's hand full it is not offered", !Offered(g));
    var g2 = Fight("ulda_27_toomba");
    ToBoss(g2);
    EmptyHand(g2.Player1); EmptyHand(g2.Player2);
    var buffed = ToHand(g2, g2.Player1, Yeti);
    Generic.AddEnchantmentBlock(g2, C("ULDA_BOSS_23e"), g2.Player1.Hero, buffed);
    Power(g2);
    var copied = g2.Player2.HandZone.FirstOrDefault();
    Check("I7 the copy keeps the card's in-hand enchantments (Mind Vision's rule): a Yeti made to cost (1) less is copied at (3), with the enchantment, marked created by the power",
        buffed.Cost == 3 && copied?.Card.Id == Yeti && copied != buffed && copied.Cost == 3 && Has(copied, "ULDA_BOSS_23e")
        && copied[GameTag.DISPLAYED_CREATOR] == g2.Player2.Hero.HeroPower.Id, copied == null ? "none" : copied.Cost + " creator " + copied[GameTag.DISPLAYED_CREATOR]);
});

// ============================================================ J  Sazin: Mighty Windstorm
Guard("Sazin", () =>
{
    // The close-out review: the wiki marks Mighty Windstorm Auto-cast and the card carries AI_MUST_PLAY, so it is cast by rule
    // (family 09's AutoCast) at the start of the boss's turn, after the draw, never offered as a click. The boss's deck is
    // Moonfires, so its turn-start draw adds no minion to the hand the swap picks from.
    var g = Fight("ulda_28_sazin", bossDeck: Moonfire, shrine: Shrine);
    Controller boss = g.Player2, player = g.Player1;
    var shrine = ShrineRules.Of(player).FirstOrDefault();
    var raptor = Board(g, player, Raptor);
    ToPlayer(g);
    EmptyHand(boss); EmptyHand(player);
    var croc = (Minion)Entity.FromCard(player, C(Croc));
    Generic.AddHandPhase.Invoke(player, croc); Generic.AddHandPhase.Invoke(player, Entity.FromCard(player, C(Moonfire)));
    Check("J1 Mighty Windstorm costs (0) and is cast by rule (passive in the registry; the card carries AI_MUST_PLAY); with no minion in the boss's hand a cast is still due while the run player can swap (a board minion and a card in hand, public counts)",
        Cost("ULDA_BOSS_28p") == 0 && FactCost("ULDA_BOSS_28p") == 0 && boss.Hero.HeroPower.IsPassiveHeroPower && DungeonRunEncounters.PassivePowers.Contains("ULDA_BOSS_28p")
        && C("ULDA_BOSS_28p")[(GameTag)367] == 1 && TombsB.WindstormUsable(boss));
    var yeti = Board(g, boss, Yeti);
    Hit(g, yeti, 2);
    int yetiPos = yeti.ZonePosition;
    var archer = (Minion)ToHand(g, boss, Archer);
    ToHand(g, boss, Moonfire);
    var (view, viewBoss, viewPlayer) = FairView(g);
    Check("J2 in the boss's fair view the run player's hand minion is masked (the view swaps only a minion the boss saw go there)", TombsB.SwappableHand(viewPlayer).Count == 0
        && TombsB.SwappableHand(player).Count == 1 && viewPlayer.HandZone.Count == player.HandZone.Count);
    int hp = player.Hero.Health, bossHp = boss.Hero.Health;
    EndTurn(g);
    bool used = DungeonRunEncounters.AutoCasts(boss.Hero.HeroPower) == 1 && !Offered(g);
    Check("J3 at the boss's turn start each player swaps: the boss's hand minion (Elven Archer) is put onto the board in the place of its board minion (the damaged Yeti), which returns to the boss's hand as a fresh, undamaged card",
        used && archer.Zone == boss.BoardZone && boss.BoardZone.Count == 1 && yeti.Zone == boss.HandZone && yeti.Damage == 0 && boss.HandZone.Any(p => p.Card.Id == Moonfire),
        string.Join(",", boss.BoardZone.Select(m => m.Card.Id)) + " | " + string.Join(",", boss.HandZone.Select(p => p.Card.Id)));
    Check("J4 the put minion takes its place and its Battlecry does not run (no character is damaged)", archer.ZonePosition == yetiPos && player.Hero.Health == hp && boss.Hero.Health == bossHp
        && croc.Damage == 0 && player.BoardZone.All(m => m.Damage == 0));
    Check("J5 the run player's hand minion is on the board, the board minion in hand; the run shrine is never chosen", croc.Zone == player.BoardZone && raptor.Zone == player.HandZone
        && shrine.Zone == player.BoardZone && ShrineRules.IsAwake(shrine) && player.Hero.Health == hp && player.HandZone.Any(p => p.Card.Id == Moonfire));
    var g2 = Fight("ulda_28_sazin", bossDeck: Moonfire, shrine: Shrine);
    ToPlayer(g2);
    EmptyHand(g2.Player2); EmptyHand(g2.Player1);
    Generic.AddHandPhase.Invoke(g2.Player1, Entity.FromCard(g2.Player1, C(Croc)));
    EndTurn(g2);
    Check("J6 the run player's board holds only the shrine and the boss has no minion in hand (it drew a Moonfire): not cast", ShrineRules.Of(g2.Player1).Count == 1 && g2.Player1.BoardZone.Count == 1
        && DungeonRunEncounters.AutoCasts(g2.Player2.Hero.HeroPower) == 0 && !Offered(g2));
    Board(g2, g2.Player2, Yeti); ToHand(g2, g2.Player2, Raptor);
    EndTurn(g2);
    int playerHand = g2.Player1.HandZone.Count;
    EndTurn(g2);
    Check("J7 the boss swaps alone; the shrine stays (nothing of the run player's moves)", DungeonRunEncounters.AutoCasts(g2.Player2.Hero.HeroPower) == 1 && g2.Player1.BoardZone.Count == 1
        && ShrineRules.IsAwake(g2.Player1.BoardZone[0]) && g2.Player1.HandZone.Count == playerHand && g2.Player2.BoardZone.Count == 1 && g2.Player2.BoardZone[0].Card.Id == Raptor,
        "casts " + DungeonRunEncounters.AutoCasts(g2.Player2.Hero.HeroPower) + " board " + string.Join(",", g2.Player2.BoardZone.Select(m => m.Card.Id)));
});

// ============================================================ K  Ozara: Mother of Sand
Guard("Ozara", () =>
{
    var g = Fight("ulda_29_ozara");
    Controller boss = g.Player2, player = g.Player1;
    Check("K1 Mother of Sand is passive: started at match start, never an option", PassiveStarted(g, "ULDA_BOSS_29p"));
    Cast(g, Bolt, boss.Hero);
    Check("K2 the run player's Overload summons nothing", boss.BoardZone.Count == 0 && player.BoardZone.Count == 0);
    ToBoss(g);
    Cast(g, Moonfire, player.Hero);
    Check("K3 a card of the boss's without Overload: nothing", boss.BoardZone.Count == 0);
    Cast(g, Bolt, player.Hero);
    var devil = boss.BoardZone.FirstOrDefault();
    Check("K4 the boss Overloads (Lightning Bolt): a Dust Devil (EX1_243) is summoned for it, and its own Overload does not apply", devil?.Card.Id == TombsB.DustDevil && boss.BoardZone.Count == 1
        && boss.OverloadOwed == 1, boss.OverloadOwed + " owed, board " + string.Join(",", boss.BoardZone.Select(m => m.Card.Id)));
    Cast(g, "EX1_248");
    Check("K5 Feral Spirit (two wolves, Overload 2): a second Dust Devil and the two wolves", boss.BoardZone.Count(m => m.Card.Id == TombsB.DustDevil) == 2 && boss.BoardZone.Count == 4);
    FillBoard(g, boss);
    Cast(g, Bolt, player.Hero);
    Check("K6 with a full board none (nothing breaks)", boss.BoardZone.Count == 7);
});

// ============================================================ L  Ermavar: Fireshaper
Guard("Ermavar", () =>
{
    var g = Fight("ulda_30_ermavar");
    Controller boss = g.Player2, player = g.Player1;
    Check("L1 Fireshaper is passive: started at match start, never an option", PassiveStarted(g, "ULDA_BOSS_30p"));
    Board(g, player, Yeti);
    Cast(g, Moonfire, boss.Hero);
    Check("L2 the run player's spell triggers nothing (only the boss's hero took Moonfire's 1)", boss.Hero.Damage == 1 && Lost(player) == 0);
    ToBoss(g);
    var own = Board(g, boss, Yeti);
    Cast(g, Moonfire, player.Hero);
    Check("L3 the boss casts a spell: Moonfire's 1 and 2 more split among the enemies (3 in all on the run player's side); the boss's side takes none", Lost(player) == 3
        && own.Damage == 0 && boss.Hero.Damage == 1, Lost(player).ToString());
    var played = ToHand(g, boss, Croc);
    bool ok = Play(g, played);
    Check("L4 a minion the boss plays from hand is no spell: nothing", ok && played.Zone == boss.BoardZone && Lost(player) == 3, Lost(player).ToString());
});

// ============================================================ M  Pillager Drasar: Pillaged Relics
Guard("Drasar", () =>
{
    var g = Fight("ulda_31_drasar");
    Controller boss = g.Player2, player = g.Player1;
    var theirs = Board(g, player, Yeti);
    ToBoss(g);
    var own = Board(g, boss, Yeti);
    var targets = PowerTargets(g);
    Check("M1 Pillaged Relics costs (2); any minion is a target, never a hero", Cost("ULDA_BOSS_31p") == 2 && targets.Count == 2 && targets.Contains(own) && targets.Contains(theirs));
    var outcomes = new HashSet<string>();
    bool single = true;
    for (int i = 0; i < 16; i++)
    {
        var m = Board(g, boss, Yeti);
        Power(g, m);
        bool stats = Has(m, "ULDA_BOSS_31e1") && m.AttackDamage == 5 && m.Health == 6, shield = m.HasDivineShield, taunt = m.HasTaunt;
        if ((stats ? 1 : 0) + (shield ? 1 : 0) + (taunt ? 1 : 0) != 1) single = false;
        outcomes.Add(stats ? "stats" : shield ? "shield" : taunt ? "taunt" : "none");
        Round(g);
        EmptyBoard(boss);
    }
    Check("M2 each use gives exactly one of +1/+1 (ULDA_BOSS_31e1), Divine Shield or Taunt; over sixteen uses all three come up", single && outcomes.SetEquals(new[] { "stats", "shield", "taunt" }),
        string.Join(",", outcomes));
    Power(g, theirs);
    Check("M3 on the run player's minion too", theirs.HasDivineShield || theirs.HasTaunt || Has(theirs, "ULDA_BOSS_31e1"));
});

// ============================================================ N  Edra: Whispering Sentinel
Guard("Edra", () =>
{
    var g = Fight("ulda_32_edra");
    Controller boss = g.Player2, player = g.Player1;
    ToBoss(g);
    bool used = Power(g);
    var wisp = boss.BoardZone.FirstOrDefault();
    Check("N1 Whispering Sentinel costs (1): a 1/1 Wisp (CS2_231) with Lifesteal (DAL_568e)", Cost("ULDA_BOSS_32p") == 1 && used && wisp?.Card.Id == Wisp && wisp.HasLifeSteal && Has(wisp, TombsB.SentinelLifesteal));
    boss.Hero.Damage = 5;
    Round(g);
    int hp = player.Hero.Health;
    Attack(g, wisp, player.Hero);
    Check("N2 its attack heals the boss's hero for the damage it deals", player.Hero.Health == hp - 1 && boss.Hero.Damage == 4);
    FillBoard(g, boss);
    Check("N3 with a full board it is not offered", !Offered(g));
});

// ============================================================ O  Runaway Ruby: Out of Control
Guard("Ruby", () =>
{
    // The close-out review: the wiki marks Out of Control Auto-cast and the card carries AI_MUST_PLAY, so it is cast by rule
    // (family 09's AutoCast) at the start of the boss's turn, after the draw, never offered as a click.
    var g = Fight("ulda_33_ruby");
    Controller boss = g.Player2, player = g.Player1;
    ToPlayer(g);
    EndTurn(g);
    Check("O1 Out of Control costs (1) and is cast by rule (passive in the registry, never an option; the card carries AI_MUST_PLAY); with both boards empty it is not cast",
        Cost("ULDA_BOSS_33p") == 1 && PassiveStarted(g, "ULDA_BOSS_33p") && C("ULDA_BOSS_33p")[(GameTag)367] == 1 && DungeonRunEncounters.AutoCasts(boss.Hero.HeroPower) == 0);
    ToPlayer(g);
    var p1 = Board(g, player, Yeti); var p2 = Board(g, player, Yeti); var p3 = Board(g, player, Yeti);
    var b = Enumerable.Range(0, 4).Select(_ => Board(g, boss, Yeti)).ToList();
    EndTurn(g);
    bool used = DungeonRunEncounters.AutoCasts(boss.Hero.HeroPower) == 1 && boss.UsedMana == 1;
    Check("O2 at the boss's turn start, paying (1), it deals 1 damage to the center-most minions: the middle one of the run player's three, the middle two of the boss's four", used && p1.Damage == 0 && p2.Damage == 1 && p3.Damage == 0
        && b[0].Damage == 0 && b[1].Damage == 1 && b[2].Damage == 1 && b[3].Damage == 0, string.Join("", player.BoardZone.Select(m => m.Damage)) + "|" + string.Join("", boss.BoardZone.Select(m => m.Damage)));
    var g2 = Fight("ulda_33_ruby", shrine: Shrine);
    var c2 = g2.Player1;
    Kill(g2, ShrineRules.Of(c2).First());
    var front = Board(g2, c2, Yeti, 0); var back = Board(g2, c2, Yeti);
    ToPlayer(g2);
    EndTurn(g2);
    var dormant = c2.BoardZone[1];
    Check("O3 a dormant run shrine counts for the positions but takes no damage: [Yeti, dormant shrine, Yeti] has the shrine in the center, nothing is damaged there, and with nothing else it is not cast",
        ShrineRules.IsDormantForm(dormant) && TombsB.CenterMost(c2).SequenceEqual(new[] { dormant }) && TombsB.OutOfControlTargets(g2.Player2).Count == 0
        && DungeonRunEncounters.AutoCasts(g2.Player2.Hero.HeroPower) == 0 && front.Damage == 0 && back.Damage == 0);
    var lone = Board(g2, g2.Player2, Yeti);
    Round(g2);
    Check("O4 with one minion on the boss's board it is that one (cast at the boss's next turn start)", DungeonRunEncounters.AutoCasts(g2.Player2.Hero.HeroPower) == 1 && lone.Damage == 1
        && front.Damage == 0 && back.Damage == 0, "casts " + DungeonRunEncounters.AutoCasts(g2.Player2.Hero.HeroPower) + " lone " + lone.Damage);
});

// ============================================================ P  Dovo Fastfuse: Tomb Boomin'
Guard("Dovo", () =>
{
    var g = Fight("ulda_34_dovo");
    Controller boss = g.Player2, player = g.Player1;
    Board(g, player, Yeti); Board(g, player, Yeti);
    ToBoss(g);
    Board(g, boss, Yeti);
    bool every = true, bossHit = false, playerHit = false, minionsHit = false;
    for (int i = 0; i < 6; i++)
    {
        int before = Lost(boss) + Lost(player), bossBefore = boss.Hero.Damage, playerBefore = player.Hero.Damage, minionsBefore = player.BoardZone.Sum(m => m.Damage) + boss.BoardZone.Sum(m => m.Damage);
        if (!Power(g)) every = false;
        if (Lost(boss) + Lost(player) != before + 4) every = false;
        bossHit |= boss.Hero.Damage > bossBefore; playerHit |= player.Hero.Damage > playerBefore;
        minionsHit |= player.BoardZone.Sum(m => m.Damage) + boss.BoardZone.Sum(m => m.Damage) > minionsBefore;
        Round(g);
        foreach (var m in boss.BoardZone.Concat(player.BoardZone)) m.Damage = 0;
    }
    Check("P1 Tomb Boomin' costs (2): each use deals 4 damage split among all characters; over six uses the boss's own hero, the run player's hero and minions were all hit",
        Cost("ULDA_BOSS_34p") == 2 && every && bossHit && playerHit && minionsHit, $"{every} {bossHit} {playerHit} {minionsHit}");
});

// ============================================================ Q  Earthrager Ptah: Vengeful Construct
Guard("Ptah", () =>
{
    var g = Fight("ulda_35_ptah");
    Controller boss = g.Player2, player = g.Player1;
    var left = Board(g, player, Yeti); var wisp = Board(g, player, Wisp); var right = Board(g, player, Croc); var far = Board(g, player, Yeti);
    ToBoss(g);
    var raptor = Board(g, boss, Raptor); var own = Board(g, boss, Yeti);
    var targets = PowerTargets(g);
    Check("Q1 Vengeful Construct costs (3); its targets are the minions of either side with 2 or less Health (the Wisp, the Raptor), never a hero", Cost("ULDA_BOSS_35p") == 3 && FactCost("ULDA_BOSS_35p") == 3
        && targets.Count == 2 && targets.Contains(wisp) && targets.Contains(raptor), string.Join(",", targets.Select(t => t.Card.Id)));
    bool used = Power(g, wisp);
    Check("Q2 it destroys the chosen minion and its two neighbours; the others stay", used && wisp.Zone != player.BoardZone && left.Zone != player.BoardZone && right.Zone != player.BoardZone
        && far.Zone == player.BoardZone && raptor.Zone == boss.BoardZone && own.Zone == boss.BoardZone);
    Round(g);
    Power(g, raptor);
    Check("Q3 on its own board's leftmost minion: it and its one neighbour", raptor.Zone != boss.BoardZone && own.Zone != boss.BoardZone);
    var g2 = Fight("ulda_35_ptah", shrine: Shrine);
    var shrine = ShrineRules.Of(g2.Player1).First();
    var w2 = Board(g2, g2.Player1, Wisp);
    ToBoss(g2);
    Power(g2, w2);
    Check("Q4 a run shrine next to the chosen minion is destroyed as a shrine is: it goes dormant in its slot", w2.Zone != g2.Player1.BoardZone && ShrineRules.IsCorpse(shrine)
        && ShrineRules.Of(g2.Player1).Count(ShrineRules.IsDormantForm) == 1);
});

// ============================================================ R  Kasa Softspeaker: Madness Incarnate
Guard("Kasa", () =>
{
    var g = Fight("ulda_36_kasa");
    Controller boss = g.Player2, player = g.Player1;
    ToBoss(g);
    Check("R1 Madness Incarnate costs (1); with no enemy minion it is not offered", Cost("ULDA_BOSS_36p") == 1 && !Offered(g));
    ToPlayer(g);
    var mins = new[] { Board(g, player, Yeti), Board(g, player, Yeti), Board(g, player, Yeti), Board(g, player, Yeti) };
    ToBoss(g);
    boss.Hero.IsFrozen = true;
    int hp = boss.Hero.Health, options = boss.NumOptionsPlayedThisTurn;
    bool used = Power(g);
    var hit = mins.Where(m => m.Damage > 0).ToList();
    // A forced attack spends no option of the turn (EncounterRules.ForcedAttack undoes the count AttackPhase adds; the engine's
    // hero power path adds none), so three forced attacks leave NUM_OPTIONS_PLAYED_THIS_TURN as it was (+3 without the undo).
    Check("R2b the forced attacks spend no option of the turn: NUM_OPTIONS_PLAYED_THIS_TURN is unchanged by the three attacks", boss.NumOptionsPlayedThisTurn == options,
        options + " -> " + boss.NumOptionsPlayedThisTurn);
    Check("R2 the hero gains +1 Attack this turn (ULDA_BOSS_36e) and attacks three different random enemy minions (forced: even frozen), taking 4 from each",
        used && hit.Count == 3 && hit.All(m => m.Damage == 1) && boss.Hero.Health == hp - 12 && boss.Hero.AttackDamage == 1 && Has(boss.Hero, "ULDA_BOSS_36e"),
        hit.Count + " hit, hero " + hp + "->" + boss.Hero.Health);
    boss.Hero.IsFrozen = false;
    Round(g);
    Check("R3 the next turn the Attack is gone", boss.Hero.AttackDamage == 0 && !Has(boss.Hero, "ULDA_BOSS_36e"));
    EmptyBoard(player);
    var a = Board(g, player, Yeti); var hidden = Board(g, player, Yeti);
    hidden.HasStealth = true;
    Cast(g, WarAxe);
    hp = boss.Hero.Health;
    Power(g);
    Check("R4 with a weapon (3 Attack, 2 Durability) and one target it can see: one attack of 4, a Stealthed minion is never picked, the weapon loses one Durability",
        a.Damage == 4 && hidden.Damage == 0 && boss.Hero.Health == hp - 4 && boss.Hero.Weapon?.Durability == 1);
    var g2 = Fight("ulda_36_kasa");
    Board(g2, g2.Player1, Yeti); Board(g2, g2.Player1, Yeti);
    ToBoss(g2);
    g2.Player2.Hero.Damage = g2.Player2.Hero.Health - 3;
    Power(g2);
    Check("R5 the hero dies on its first attack: the attacks stop and the game ends (the run player wins)", g2.State == State.COMPLETE && g2.Player1.PlayState == PlayState.WON
        && g2.Player1.BoardZone.Count(m => m.Damage > 0) == 1);
});

// ============================================================ S  the rows, the tier rule, the admission rows, the validator
Guard("Rows", () =>
{
    var errors = DungeonRunValidator.Validate(data, null);
    Check("S1 the shipped data validates with the eighteen family 20 rows", errors.Count == 0, string.Join(" | ", errors.Take(4)));
    // (row, n, name, tier, wiki level, deck kind, chapter, flags)
    var rows = new (string id, string n, string name, string tier, int level, string kind, int chapter, string flags)[]
    {
        ("ulda_19_sothis", "19", "Sothis", "3", 6, "substituted", 4, ""),
        ("ulda_20_kham", "20", "Kham", "3", 6, "substituted", 3, "passive"),
        ("ulda_21_illidara", "21", "Illidara Sunsdawn", "4", 7, "substituted", 3, "passive,bothPlayers"),
        ("ulda_22_colossus", "22", "Colossus of the Sun", "4", 7, "authentic", 2, "bothPlayers"),
        ("ulda_23_skarik", "23", "Skarik", "1", 2, "extended", 2, ""),
        ("ulda_24_palm_tree", "24", "Suspicious Palm Tree", "2", 4, "extended", 2, ""),
        ("ulda_25_zenda", "25", "Beetle Herder Zenda", "1", 2, "extended", 2, ""),
        ("ulda_26_thudd", "26", "Thudd Lockspring", "1", 2, "extended", 4, ""),
        ("ulda_27_toomba", "27", "Ol' Toomba", "4", 7, "substituted", 1, "hiddenZone"),
        ("ulda_28_sazin", "28", "Sazin", "4", 7, "substituted", 1, "bothPlayers,hiddenZone"),
        ("ulda_29_ozara", "29", "Ozara", "4", 7, "substituted", 2, "passive"),
        ("ulda_30_ermavar", "30", "Ermavar", "2", 3, "extended", 1, "passive"),
        ("ulda_31_drasar", "31", "Pillager Drasar", "1", 2, "extended", 4, ""),
        ("ulda_32_edra", "32", "Edra", "1", 2, "extended", 4, ""),
        ("ulda_33_ruby", "33", "Runaway Ruby", "3", 6, "authentic", 2, "bothPlayers,position"),
        ("ulda_34_dovo", "34", "Dovo Fastfuse", "1", 2, "extended", 3, "bothPlayers"),
        ("ulda_35_ptah", "35", "Earthrager Ptah", "4", 7, "authentic", 4, "position"),
        ("ulda_36_kasa", "36", "Kasa Softspeaker", "2", 4, "extended", 2, "forcedAttack"),
    };
    Check("S2 family 20 holds exactly these eighteen rows, appended after the earlier families' rows", data.Encounters.Where(b => b.Family == "20").Select(b => b.Id)
        .SequenceEqual(rows.Select(r => r.id))
        // wave 6: later families' rows follow (families 20-24): the family's rows are one block and only later families come after it
        && data.Encounters.SkipWhile(b => b.Family != "20").TakeWhile(b => b.Family == "20").Count() == rows.Length
        && data.Encounters.SkipWhile(b => b.Family != "20").SkipWhile(b => b.Family == "20").All(b => string.CompareOrdinal(b.Family, "20") > 0),
        string.Join(",", data.Encounters.Where(b => b.Family == "20").Select(b => b.Id)));
    var chapterTag = new Dictionary<int, GameTag> { { 1, (GameTag)1202 }, { 2, (GameTag)1203 }, { 3, (GameTag)1204 }, { 4, (GameTag)1205 } };
    foreach (var r in rows)
    {
        var b = data.Boss(r.id);
        var plan = RowPlan(r.id);
        string hero = "ULDA_BOSS_" + r.n + "h", power = "ULDA_BOSS_" + r.n + "p";
        // The tier rule (verify/tools/gen/p8f19/tombs_tiers.py), applied to the level the row records: level 1-2 tier 1, 3-4 tier 2,
        // 5-6 tier 3, 7 tier 4.
        string ruleTier = b.LevelOriginal <= 2 ? "1" : b.LevelOriginal <= 4 ? "2" : b.LevelOriginal <= 6 ? "3" : "4";
        Check("S3 " + r.id + ": a Tombs row of tier " + r.tier + " (wiki level " + r.level + ", chapter " + r.chapter + ") whose plan is the row's (hero, power, a 30-card " + r.kind
            + " deck, the act curve's Health, not the printed 10; flags " + (r.flags.Length == 0 ? "none" : r.flags) + ")",
            b.Provenance == "ULDA" && b.Name == r.name && b.Tier == r.tier && b.LevelSource == "wiki-level-table" && b.LevelOriginal == r.level && ruleTier == r.tier
            && b.HeroCard == hero && b.Power == power && b.Source.Kind == r.kind && string.Join(",", b.Flags) == r.flags
            && plan.EncounterId == r.id && plan.EncounterDeck && plan.BossHero.Id == hero && plan.BossHeroPowerId == power && plan.BossDeck.Count == 30 && data.BossDecks[b.Deck].Count == 30
            && !b.FixedHealth && b.Health == null && plan.BossHealth == 30 && C(hero).Type == CardType.HERO && C(hero).Health == 10 && C(hero)[chapterTag[r.chapter]] > 0
            && C(power).Type == CardType.HERO_POWER && C(hero).Name == r.name && data.Schedules.Member(b, DungeonRunEncounterData.PoolOf(b.Tier))
            && b.Source.Url.StartsWith("https://hearthstone.wiki.gg/wiki/") && !b.BossGeneratedSecrets && b.Forms.Count == 0 && b.Start == null && b.Pools.SequenceEqual(new[] { "enc-t" + r.tier }),
            b.Source.Kind + " level " + b.LevelSource + "/" + b.LevelOriginal + " health " + plan.BossHealth + " tier " + b.Tier + " flags " + string.Join(",", b.Flags));
    }
    // The four passive powers and the three cast by rule since the close-out review (family 09's AutoCast registers them as passives).
    string[] passives = { "ULDA_BOSS_20p", "ULDA_BOSS_21p", "ULDA_BOSS_29p", "ULDA_BOSS_30p" };
    string[] ruleCast = { "ULDA_BOSS_22p", "ULDA_BOSS_28p", "ULDA_BOSS_33p" };
    string[] powers = rows.Select(r => "ULDA_BOSS_" + r.n + "p").ToArray();
    string[] enchantments = { "ULDA_BOSS_21e", "ULDA_BOSS_23e", "ULDA_BOSS_31e1", "ULDA_BOSS_36e" };
    var unscripted = powers.Concat(enchantments).Where(id => !C(id).Implemented || DungeonRunEncounters.Find(id)?.Family != "20").ToList();
    // The loader appends " @spelldmg" to a text with "$" (Fireshaper's "$2"); the client's text is what precedes it.
    var retexted = powers.Concat(enchantments).Concat(new[] { Cultist, Ra }).Where(id => C(id).Text.Replace(" @spelldmg", "") != Text(id)).ToList();
    Check("S4 every power and enchantment of the family is scripted by family 20, and they, Mogu Cultist and Highkeeper Ra keep the client's text (18 powers, 4 enchantments)",
        unscripted.Count == 0 && retexted.Count == 0 && powers.Length == 18, string.Join(",", unscripted.Concat(retexted)));
    Check("S5 the four passive powers and the three rule-cast ones are the registry's passive ones (never an option), the eleven others clickable; each power's Cost is the client's",
        powers.All(p => DungeonRunEncounters.PassivePowers.Contains(p) == (passives.Contains(p) || ruleCast.Contains(p))
            && DungeonRunEncounters.Find(p).Kind == (passives.Contains(p) || ruleCast.Contains(p) ? EncounterCardKind.Passive : EncounterCardKind.Power)
            && C(p).Cost == FactCost(p)));
    var expectFamily = new Dictionary<string, string>();
    foreach (var p in new[] { "19", "25", "26", "31", "32", "34", "35" }) expectFamily["ULDA_BOSS_" + p + "p"] = "boss-tombs-b-board-power";
    foreach (var p in new[] { "23", "24" }) expectFamily["ULDA_BOSS_" + p + "p"] = "boss-tombs-b-own-hand-power";
    expectFamily["ULDA_BOSS_27p"] = "boss-tombs-b-hidden-hand";
    foreach (var p in ruleCast) expectFamily[p] = "boss-tombs-b-rule-cast";
    foreach (var p in passives) expectFamily[p] = "boss-tombs-b-passive";
    expectFamily["ULDA_BOSS_36p"] = "boss-tombs-b-forced-attack";
    var wrong = powers.Where(id =>
    {
        var v = DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss, null, catalogRun: true);
        return !(v.Admitted && v.Subject && v.Rule?.Ids != null && v.Rule.Ids.Contains(id) && DungeonRunAdmission.RuleFor(id)?.Family == expectFamily[id]
            && DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss).Admitted
            && !DungeonRunAdmission.Classify(C(id), id, AdmitScope.Player, null, catalogRun: true).Admitted
            && !DungeonRunAdmission.Classify(C(id), id, AdmitScope.Generated, null, catalogRun: true).Admitted
            && DungeonRunAdmission.GeneratedRejects(true).Contains(id) && DungeonRunAdmission.GeneratedRejects(false).Contains(id));
    }).ToList();
    Check("S6 each power has its own admission row of its family, a boss's only; none is ever generated", wrong.Count == 0 && expectFamily.Count == 18, string.Join(",", wrong));
    var playerPower = DungeonRunAdmission.Classify(C("ULDA_BOSS_27p"), "ULDA_BOSS_27p", AdmitScope.Player, null, catalogRun: true);
    Check("S7 a family 20 power asked for the run player is refused with its row's reason", !playerPower.Admitted && playerPower.Family == "boss-tombs-b-hidden-hand"
        && playerPower.Lift == DungeonRunAdmission.NoneBossPower);
    string Subs(string id) => string.Join(",", data.Boss(id).Source.Substitutions.Where(x => x.Listed != null).Select(x => x.Listed + ">" + (x.Used ?? "-") + ":" + x.Reason).Distinct().OrderBy(x => x, StringComparer.Ordinal));
    var replaced = rows.SelectMany(r => data.Boss(r.id).Source.Substitutions).Where(x => x.Listed != null).ToList();
    Check("S8 the listed cards replaced are only cards the engine does not script (or Hydrologist, whose Discover offers Secrets), each with its named replacement",
        replaced.Count > 0 && replaced.All(x => x.Used != null && (x.Reason == "unscripted" && !C(x.Listed).Implemented || x.Listed == "UNG_011" && x.Reason.StartsWith("admission:")))
        && Subs("ulda_36_kasa") == "LOE_118>CS2_091:unscripted,ULD_715>EX1_409:unscripted" && Subs("ulda_21_illidara") == "UNG_011>LOE_029:admission:secret-generated"
        && Subs("ulda_19_sothis") == "ULD_198>CS2_179:unscripted" && Subs("ulda_22_colossus") == "" && Subs("ulda_33_ruby") == "" && Subs("ulda_35_ptah") == "",
        Subs("ulda_36_kasa") + " | " + Subs("ulda_21_illidara") + " | " + Subs("ulda_19_sothis"));
    int In(string boss, string card) => data.BossDecks[data.Boss(boss).Deck].Count(id => id == card);
    int Where(string boss, Func<Card, bool> f) => data.BossDecks[data.Boss(boss).Deck].Count(id => f(C(id)));
    Check("S9 the decks keep the wiki's signature cards: Sothis's three Mogu Cultists, Kham's five weapons (Light's Justice for Cursed Blade), Illidara's Discover cards, Ozara's Overload cards, Ol' Toomba's single Legendaries",
        In("ulda_19_sothis", Cultist) == 3 && Where("ulda_20_kham", c => c.Type == CardType.WEAPON) == 5 && Where("ulda_21_illidara", c => c[GameTag.DISCOVER] == 1) >= 5
        && Where("ulda_29_ozara", c => c.HasOverload) >= 8 && In("ulda_27_toomba", "EX1_613") == 1 && In("ulda_27_toomba", "AT_070") == 1,
        $"kham weapons {Where("ulda_20_kham", c => c.Type == CardType.WEAPON)} illidara discover {Where("ulda_21_illidara", c => c[GameTag.DISCOVER] == 1)} ozara overload {Where("ulda_29_ozara", c => c.HasOverload)}");
    Check("S10 the tier pools: six rows in enc-t1, three in enc-t2, three in enc-t3, six in enc-t4", rows.Count(r => r.tier == "1") == 6 && rows.Count(r => r.tier == "2") == 3
        && rows.Count(r => r.tier == "3") == 3 && rows.Count(r => r.tier == "4") == 6);
    DungeonRunData Changed(Action<DungeonRunData> change) { var d = Load(); change(d); return d; }
    var badPower = DungeonRunValidator.Validate(Changed(d => d.Boss("ulda_36_kasa").Power = "ULDA_BOSS_36px"), null);
    Check("S11 the validator refuses a row whose power is not scripted (the Heroic Madness Incarnate ULDA_BOSS_36px)", badPower.Count > 0, string.Join(" | ", badPower.Take(2)));
});

Console.WriteLine($"SearchTelemetry swallowed leaf exceptions {SearchTelemetry.SwallowedLeafErrors}");
Console.WriteLine($"RESULT {passes} passed, {failures + SearchTelemetry.SwallowedLeafErrors} failed");
return failures + SearchTelemetry.SwallowedLeafErrors;
