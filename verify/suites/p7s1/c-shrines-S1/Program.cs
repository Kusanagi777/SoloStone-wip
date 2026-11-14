// c-shrines-S1: phase 7 family P7-S1, the Druid/Hunter/Warrior board shrines beyond the model's three Druid ones: TRLA_104,
// TRLA_170, TRLA_171 (Warrior), TRLA_110, TRLA_162, TRLA_163 (Hunter), their dormant forms and enchantments, and the engine's
// aura-granted Overkill (OverkillRules). Every game plays a real engine game through the run setup path (NewRun -> BuildPlan ->
// DungeonRunSetup.Configure -> Game -> ApplyAfterStart -> mulligan -> MainBegin). A admission/data/plan/AI view; W Warrior;
// H Hunter; L dormancy and revival of each; X interactions with upgrades (TRLA_804/805), treasures (LOOTA_833/828/824), teammates.
// Headless: compiles C:\SoloStone\plugin\DungeonRun\*.cs and the NPC AI against audit-verify/engine-out. Run it twice and compare.
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

int failures = 0, passes = 0;
void Check(string name, bool ok, string detail = "")
{
    if (ok) passes++; else failures++;
    Console.WriteLine((ok ? "PASS " : "FAIL ") + name + (detail.Length > 0 ? "  (" + detail + ")" : ""));
}
void Guard(string name, Action body)
{
    try { body(); }
    catch (Exception e) { failures++; Console.WriteLine("FAIL " + name + " threw " + e.GetType().Name + ": " + e.Message.Split('\n')[0]); }
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
string root = VerifyPaths.Data;
string R(string f) => File.ReadAllText(Path.Combine(root, f));
string runJson = R("dungeon-run/run-v1.json"), scheduleJson = R("dungeon-run/schedule-v1.json");
DungeonRunData Load(string run = null, string schedule = null) => DungeonRunData.Parse(run ?? runJson, R("dungeon-run/pool-v1.json"), R("npc/decks-v1.json"),
    R("dungeon-run/catalog-v1.json"), schedule ?? scheduleJson, R("dungeon-run/tavern-v1.json"));
var data = Load();
var run = new DungeonRun(data);
var all = new DungeonRunProfile(); all.Unlocked.AddRange(data.Unlocks.Keys);
Card C(string id) => Cards.FromId(id);
const string Druid = "TRLA_209h_Druid";

// The first single-class row's deck whose board shrine the engine does not script yet (rows of a shrine family still pending),
// or null once every shrine is scripted: the "unscripted shrine" checks read it instead of naming a family's shrine.
(string Row, string Deck, string Shrine)? PendingShrine()
{
    foreach (var c in data.Classes.Where(c => c.ClassSet.Count == 1))
        foreach (var d in c.Decks.Select(data.FindDeck))
            if (d?.Shrine != null && !ShrineRules.IsScripted(d.Shrine)) return (c.Id, d.Id, d.Shrine);
    return null;
}
var pending = PendingShrine();
DungeonRunState NewRun(string row, string deck, long seed, string schedule = null)
{
    var cls = data.Class(row);
    return run.NewRun(all, cls.Id, deck ?? cls.DefaultDeck, cls.DefaultPower, seed, schedule: schedule,
        signatureId: run.SignatureOffer(all, data.FindClass(cls.Id), seed).FirstOrDefault());
}
Game Start(RunMatchPlan plan, int seed)
{
    var cfg = new GameConfig { RandomSeed = seed, Shuffle = true, SkipMulligan = false, History = true, Logging = false, FormatType = FormatType.FT_WILD };
    DungeonRunSetup.Configure(cfg, plan);
    var g = new Game(cfg);
    g.StartGame();
    DungeonRunSetup.ApplyAfterStart(g, plan);
    return g;
}
(DungeonRunState s, RunMatchPlan plan, Game g) Fight(string deck, long seed, string[] upgrades = null, string row = Druid)
{
    var s = NewRun(row, deck, seed);
    foreach (string u in upgrades ?? Array.Empty<string>()) s.ShrineUpgrades.Add(u);
    var plan = run.BuildPlan(s);
    var g = Start(plan, run.MatchSeed(s));
    g.Process(ChooseTask.Mulligan(g.Player1, new List<int>()));
    g.Process(ChooseTask.Mulligan(g.Player2, new List<int>()));
    g.MainBegin();
    if (g.CurrentPlayer != g.Player1) throw new Exception("run player not first");
    return (s, plan, g);
}
void Mana(Controller c, int n) { c.BaseMana = n; c.UsedMana = 0; c.OverloadLocked = 0; c.OverloadOwed = 0; }
// Adds a card to a hand (making room first: a full hand would burn it).
IPlayable ToHand(Game g, Controller c, string id)
{
    while (c.HandZone.Count >= 9) { var old = c.HandZone[0]; c.HandZone.Remove(old); c.SetasideZone.Add(old); }
    var e = Entity.FromCard(c, C(id)); Generic.AddHandPhase.Invoke(c, e); g.DeathProcessingAndAuraUpdate(); return e;
}
Minion Board(Game g, Controller c, string id) { var m = (Minion)Entity.FromCard(c, C(id)); Generic.SummonBlock(g, m, -1, c.Hero); g.DeathProcessingAndAuraUpdate(); return m; }
bool Play(Game g, IPlayable card, ICharacter target = null) => g.Process(PlayCardTask.Any(g.CurrentPlayer, card, target, -1, 0));
void EndTurn(Game g) => g.Process(EndTurnTask.Any(g.CurrentPlayer));
// Clears both boards except the run shrines, so a check sees only what it set up.
void ClearExceptShrines(Game g)
{
    foreach (var c in new[] { g.Player1, g.Player2 })
        foreach (var m in c.BoardZone.Slots.OfType<Minion>().Where(m => !ShrineRules.IsMarked(m)).ToArray()) { c.BoardZone.Remove(m); c.SetasideZone.Add(m); }
    g.DeathProcessingAndAuraUpdate();
}
Minion ShrineOf(Game g, Controller c) => ShrineRules.Of(c).FirstOrDefault();
void Kill(Game g, Minion m) { m.Destroy(); g.DeathProcessingAndAuraUpdate(); }
void Damage(Game g, Minion m, int n) { Generic.DamageCharFunc(m.Controller.Opponent.Hero, m, n, false); g.DeathProcessingAndAuraUpdate(); }
// Ends turns until the run player's turn number <paramref name="turn"/> starts.
void ToTurn(Game g, int turn) { while (g.Turn < turn && g.State != State.COMPLETE) EndTurn(g); }

const string Warrior = "TRLA_209h_Warrior", Hunter = "TRLA_209h_Hunter";
string RowOf(string deck) => deck.StartsWith("rumble_warrior", StringComparison.Ordinal) ? Warrior : Hunter;
(DungeonRunState s, RunMatchPlan plan, Game g) F(string deck, long seed, string[] upgrades = null, string[] treasures = null)
{
    if (treasures == null) return Fight(deck, seed, upgrades, RowOf(deck));
    var s = NewRun(RowOf(deck), deck, seed);
    foreach (string u in upgrades ?? Array.Empty<string>()) s.ShrineUpgrades.Add(u);
    foreach (string t in treasures) s.Treasures.Add(t);
    var plan = run.BuildPlan(s);
    var g = Start(plan, run.MatchSeed(s));
    g.Process(ChooseTask.Mulligan(g.Player1, new List<int>()));
    g.Process(ChooseTask.Mulligan(g.Player2, new List<int>()));
    g.MainBegin();
    if (g.CurrentPlayer != g.Player1) throw new Exception("run player not first");
    return (s, plan, g);
}
Minion Ready(Minion m) { m.IsExhausted = false; return m; }
bool Attack(Game g, Minion a, ICharacter t) => g.Process(MinionAttackTask.Any(g.CurrentPlayer, a, t));
string Costs(Controller c) => string.Join(",", c.HandZone.Select(p => p.Card.Id + ":" + p.Cost));
string[] Six = { "TRLA_104", "TRLA_170", "TRLA_171", "TRLA_110", "TRLA_162", "TRLA_163" };
string DeckOf(string id) => (id == "TRLA_104" || id == "TRLA_170" || id == "TRLA_171" ? "rumble_warrior_" : "rumble_hunter_") + id.Substring(5);
IEnumerable<Enchantment> Ench(IEntity e) => (IEnumerable<Enchantment>)(e as IPlayable)?.AppliedEnchantments ?? Array.Empty<Enchantment>();
bool HasGuise(Minion m) => Ench(m).Any(e => DungeonRunShrines.GuiseGrants.Contains(e.Card.Id));
int GuiseCount(Minion m) => Ench(m).Count(e => DungeonRunShrines.GuiseGrants.Contains(e.Card.Id));

// ================================================================ A admission, data, plan, AI view
Guard("A", () =>
{
    AdmissionVerdict V(string id, AdmitScope scope) => DungeonRunAdmission.Classify(null, id, scope, null, true);
    foreach (string id in Six)
    {
        var v = V(id, AdmitScope.Player);
        // Phase 8 family 01: the three Warrior shrines are a boss's too (War Master Voone's variants; the boss-shrine row).
        bool bossShrine = id == "TRLA_104" || id == "TRLA_170" || id == "TRLA_171";
        Check($"A1 {id}: scripted with its form {id}t (3-turn sleeper pointing back), admitted for the run player ({(bossShrine ? "and, since phase 8, a boss" : "only")}), never generated",
            ShrineRules.IsScripted(id) && C(id).Implemented && C(id + "t").Implemented && C(id + "t").Dormant?.Turns == 3 && C(id + "t")[(GameTag)1132] == C(id).AssetId
            && v.Admitted && v.Family == (bossShrine ? "boss-shrine" : "board-shrine") && v.Rule.NeverGenerated && V(id, AdmitScope.Boss).Admitted == bossShrine && !V(id, AdmitScope.Generated).Admitted
            && DungeonRunAdmission.GeneratedRejects(true).Contains(id) && DungeonRunAdmission.GeneratedRejects(false).Contains(id)
            && V(id + "t", AdmitScope.Player).Family == "board-shrine-form", $"{v.Family} {v.Admitted}");
        var s = NewRun(RowOf(DeckOf(id)), DeckOf(id), 10 + Array.IndexOf(Six, id));
        var plan = run.BuildPlan(s);
        Check($"A2 a new Rikkar run on {DeckOf(id)} has board shrine {id} (plan: one copy; not a power, treasure or deck card)",
            s.BoardShrine == id && plan.PlayerShrine == id && plan.PlayerShrineCopies == 1 && !s.Deck.Contains(id) && !plan.PlayerTreasures.Contains(id) && s.Shrine == null,
            $"{s.BoardShrine} {plan.PlayerShrine}");
    }
    foreach (string e in new[] { "TRLA_171e", "TRLA_162e", "TRLA_162e2", "TRLA_163e", "TRLA_163e3", "TRLA_163e4" })
        Check($"A3 enchantment {e} is scripted", C(e).Implemented && C(e).Power?.Enchant != null);
    Check("A3 TRLA_171e and TRLA_162e carry an Overkill task (the aura grants it)", C("TRLA_171e").Power.OverkillTask != null && C("TRLA_162e").Power.OverkillTask != null);
    var hs = NewRun(Hunter, "rumble_hunter_110", 20);
    Check("A4 a TRLA_110 run is a catalog run whose player may generate Secrets", hs.IsCatalogRun && run.BuildPlan(hs).PlayerGeneratedSecrets);
    var errors = DungeonRunValidator.Validate(data, null);
    Check("A5 the validator passes the shipped data", errors.Count == 0, string.Join(" | ", errors.Take(3)));
    var rejected = data.Decks.Where(d => d.Shrine != null && !ShrineRules.IsScripted(d.Shrine)).Select(d => d.Shrine).ToList();
    Check("A6 shrines still unscripted stay rejected (no script): a deck naming one plays without a shrine (vacuous once all are scripted)",
        rejected.All(id => !V(id, AdmitScope.Player).Admitted) && (pending == null || NewRun(pending.Value.Row, pending.Value.Deck, 21).BoardShrine == null),
        string.Join(",", rejected));
    var (s2, plan2, g) = F("rumble_warrior_171", 22);
    var bot = FairBotState.Create(g, g.Player2.PlayerId, 1, DeckStyle.Tempo);
    var seen = bot.Facts.Opponent.Board.FirstOrDefault(c => c.CardId == "TRLA_171");
    Check("A7 the boss's FairBotState sees Akali's Horn as a run shrine on the run player's board", seen != null && seen.IsRunShrine && seen.ShrineDormantTurns == 0);
    Check("A8 each of the six has its shrine engine value in the evaluator table",
        DeckPlanEvaluator.ShrineEngine("TRLA_104") == 1 && DeckPlanEvaluator.ShrineEngine("TRLA_170") == 1.5 && DeckPlanEvaluator.ShrineEngine("TRLA_171") == 2.5
        && DeckPlanEvaluator.ShrineEngine("TRLA_110") == 3 && DeckPlanEvaluator.ShrineEngine("TRLA_162") == 2 && DeckPlanEvaluator.ShrineEngine("TRLA_163") == 2.5);
    Kill(g, ShrineOf(g, g.Player1));
    var bot2 = FairBotState.Create(g, g.Player2.PlayerId, 1, DeckStyle.Tempo);
    var form = bot2.Facts.Opponent.Board.FirstOrDefault(c => c.CardId == "TRLA_171t");
    Check("A9 the boss sees the dormant form as a dormant run shrine (3 turns) holding its slot", form != null && form.IsRunShrine && form.ShrineDormantTurns == 3
        && bot2.Facts.Opponent.Board.Count == g.Player1.BoardZone.Count, $"{form?.ShrineDormantTurns}");
});

// ================================================================ W Warrior
Guard("W104", () =>
{
    var (s, plan, g) = F("rumble_warrior_104", 101);
    var shrine = ShrineOf(g, g.Player1);
    Check("W1 Akali's Champion lands at Start of Game: 0/8 with no Armor", shrine?.Card.Id == "TRLA_104" && shrine.AttackDamage == 0 && shrine.Health == 8 && g.Player1.Hero.Armor == 0,
        $"{shrine?.Card.Id} {shrine?.AttackDamage}/{shrine?.Health}");
    Mana(g.Player1, 10); ClearExceptShrines(g);
    bool used = g.Process(HeroPowerTask.Any(g.Player1));
    Check("W2 Armor Up! (2 Armor): its Attack is 2", used && g.Player1.Hero.Armor == 2 && shrine.AttackDamage == 2, $"{g.Player1.Hero.Armor} {shrine.AttackDamage}");
    Play(g, ToHand(g, g.Player1, "EX1_606")); // Shield Block: 5 Armor, draw
    Check("W2 Shield Block (+5): its Attack is 7", g.Player1.Hero.Armor == 7 && shrine.AttackDamage == 7, $"{g.Player1.Hero.Armor} {shrine.AttackDamage}");
    var victim = Board(g, g.Player2, "CS2_182"); // 4/5 Yeti
    bool attacked = Attack(g, Ready(shrine), victim);
    Check("W3 it attacks with that Attack (Yeti 4/5 dies; the shrine takes 4)", attacked && victim.IsDead && shrine.Health == 4, $"{attacked} {shrine.Health}");
    EndTurn(g); Mana(g.Player2, 10);
    Play(g, ToHand(g, g.Player2, "CS2_029"), g.Player1.Hero); // Fireball the hero: Armor absorbs 6
    Check("W4 damage to the hero eats the Armor and the Attack follows (7 - 6 = 1)", g.Player1.Hero.Armor == 1 && shrine.AttackDamage == 1, $"{g.Player1.Hero.Armor} {shrine.AttackDamage}");
    Play(g, ToHand(g, g.Player2, "EX1_332"), shrine); // Silence
    Check("W5 silenced, it is a 0-Attack minion again whatever the Armor (still a run shrine)", shrine.AttackDamage == 0 && ShrineRules.IsAwake(shrine), shrine.AttackDamage.ToString());
    var enemyChamp = ShrineRules.Place(g.Player2, "TRLA_104"); g.DeathProcessingAndAuraUpdate();
    Check("W6 a boss-side copy reads the boss's own Armor, not the run player's", enemyChamp.AttackDamage == g.Player2.Hero.Armor, enemyChamp.AttackDamage + " vs " + g.Player2.Hero.Armor);
});
Guard("W170", () =>
{
    var (s, plan, g) = F("rumble_warrior_170", 102);
    var shrine = ShrineOf(g, g.Player1);
    Check("W7 Akali's War Drum lands: 0/8", shrine?.Card.Id == "TRLA_170" && shrine.Health == 8);
    Mana(g.Player1, 10); ClearExceptShrines(g);
    var drake = ToHand(g, g.Player1, "EX1_284"); var whelp = ToHand(g, g.Player1, "BRM_004"); var yeti = ToHand(g, g.Player1, "CS2_182");
    int d0 = drake.Cost, w0 = whelp.Cost, y0 = yeti.Cost;
    Play(g, ToHand(g, g.Player1, "CS2_172")); // a Beast: no cut
    Check("W8 summoning a non-Dragon changes nothing", drake.Cost == d0 && whelp.Cost == w0 && yeti.Cost == y0, Costs(g.Player1));
    Play(g, ToHand(g, g.Player1, "NEW1_023")); // Faerie Dragon
    Check("W9 after the owner summons a Dragon, the Dragons in hand cost (1) less (Azure Drake 5 -> 4, Twilight Whelp 1 -> 0), others not",
        drake.Cost == d0 - 1 && whelp.Cost == Math.Max(0, w0 - 1) && yeti.Cost == y0 && Ench(drake).Any(e => e.Card.Id == DungeonRunShrines.WarDrumEnchantment), Costs(g.Player1));
    Play(g, whelp);
    Check("W10 a second Dragon summon cuts again", drake.Cost == d0 - 2 && yeti.Cost == y0, Costs(g.Player1));
    Board(g, g.Player1, "BRM_004"); // a Dragon summoned by an effect also counts
    Check("W11 a Dragon summoned by an effect counts too", drake.Cost == d0 - 3, Costs(g.Player1));
    Board(g, g.Player2, "NEW1_023");
    Check("W12 the boss summoning a Dragon does not cut the run player's Dragons", drake.Cost == d0 - 3, Costs(g.Player1));
    Play(g, ToHand(g, g.Player1, "BRM_004"));
    Check("W12 the cut floors at (0)", drake.Cost == Math.Max(0, d0 - 4) && drake.Cost >= 0, Costs(g.Player1));
});
Guard("W171", () =>
{
    var (s, plan, g) = F("rumble_warrior_171", 103);
    var shrine = ShrineOf(g, g.Player1);
    Check("W13 Akali's Horn lands: 0/5", shrine?.Card.Id == "TRLA_171" && shrine.Health == 5);
    Mana(g.Player1, 10); ClearExceptShrines(g);
    var yeti = Ready(Board(g, g.Player1, "CS2_182"));
    Check("W14 the owner's minions are granted Overkill (TRLA_171e on them, from the aura)", yeti.HasOverkill && OverkillRules.HasGranted(yeti)
        && Ench(yeti).Any(e => e.Card.Id == "TRLA_171e"));
    var wisp = Board(g, g.Player2, "CS2_231");
    bool a1 = Attack(g, yeti, wisp);
    Check("W15 Overkill (4 into a 1/1): the minion can attack again", a1 && wisp.IsDead && !yeti.IsExhausted && yeti.CanAttack && yeti[OverkillRules.AttackAgain] == 0, $"{a1} exhausted {yeti.IsExhausted}");
    bool a2 = Attack(g, yeti, g.Player2.Hero);
    Check("W15 its second attack (no Overkill) exhausts it", a2 && yeti.IsExhausted && !yeti.CanAttack);
    var raptor = Ready(Board(g, g.Player1, "CS2_172")); var croc = Board(g, g.Player2, "CS2_120"); // 3 into exactly 3 Health
    Attack(g, raptor, croc);
    Check("W16 an exact kill is no Overkill: exhausted", croc.IsDead && raptor.IsExhausted);
    var farseer = Ready(Board(g, g.Player1, "EX1_021")); // Thrallmar Farseer 2/3 Windfury
    var w1 = Board(g, g.Player2, "CS2_231"); var w2 = Board(g, g.Player2, "CS2_231");
    Attack(g, farseer, w1); Attack(g, farseer, w2);
    bool third = !farseer.IsExhausted && Attack(g, farseer, g.Player2.Hero);
    bool fourth = third && !farseer.IsExhausted && Attack(g, farseer, g.Player2.Hero);
    Check("W17 Windfury keeps its own two attacks: Overkill, Overkill (neither counts), then two more attacks and exhausted", third && fourth && farseer.IsExhausted, $"{third} {fourth}");
    ClearExceptShrines(g);
    var fan = Ready(Board(g, g.Player1, "TRLA_125"));
    int heroAtk = g.Player1.Hero.AttackDamage;
    var w3 = Board(g, g.Player2, "CS2_231");
    Attack(g, fan, w3);
    Check("W18 a card's own Overkill (teammate TRLA_125) and the granted one both fire: hero +10 and attack again",
        g.Player1.Hero.AttackDamage == heroAtk + 10 && !fan.IsExhausted, $"hero {g.Player1.Hero.AttackDamage} exhausted {fan.IsExhausted}");
    EndTurn(g); Mana(g.Player2, 10); ClearExceptShrines(g);
    var enemy = Ready(Board(g, g.Player2, "CS2_231"));
    var guard = Board(g, g.Player1, "CS2_182");
    Attack(g, enemy, guard);
    Check("W19 not on the enemy's turn: our defender's 4 into an attacking 1/1 is no Overkill (no mark)", guard[OverkillRules.AttackAgain] == 0 && enemy.IsDead);
    var enemyYeti = Ready(Board(g, g.Player2, "CS2_182")); var target = Board(g, g.Player1, "CS2_231");
    Attack(g, enemyYeti, target);
    Check("W19 the boss's minions are not granted it", enemyYeti.IsExhausted && !OverkillRules.HasGranted(enemyYeti));
    Play(g, ToHand(g, g.Player2, "EX1_332"), shrine); // Silence the Horn
    Check("W20 Silence on the Horn removes the grant (it stays a run shrine)", !OverkillRules.HasGranted(guard) && ShrineRules.IsAwake(shrine));
});

// ================================================================ H Hunter
Guard("H110", () =>
{
    var (s, plan, g) = F("rumble_hunter_110", 104);
    var shrine = ShrineOf(g, g.Player1);
    Check("H1 Halazzi's Trap lands: 0/8", shrine?.Card.Id == "TRLA_110" && shrine.Health == 8);
    Mana(g.Player1, 10); ClearExceptShrines(g);
    int secrets = g.Player1.SecretZone.Count, spells = g.Player1.NumSpellsPlayedThisGame;
    Play(g, ToHand(g, g.Player1, "DS1_185"), g.Player2.Hero); // Arcane Shot
    var put = g.Player1.SecretZone.Skip(secrets).ToList();
    Check("H2 after the owner casts a spell, a random scripted Hunter Secret is put into the battlefield (not cast: the spell count rises by 1)",
        put.Count == 1 && put[0].Card.IsSecret && put[0].Card.Class == CardClass.HUNTER && put[0].Card.Implemented && g.Player1.NumSpellsPlayedThisGame == spells + 1,
        $"{string.Join(",", put.Select(p => p.Card.Id))} spells {g.Player1.NumSpellsPlayedThisGame - spells}");
    for (int i = 0; i < 6; i++) { Mana(g.Player1, 10); Play(g, ToHand(g, g.Player1, "DS1_185"), g.Player2.Hero); }
    var ids = g.Player1.SecretZone.Select(p => p.Card.Id).ToList();
    Check("H3 the Secret zone fills to 5 different Secrets and stops", g.Player1.SecretZone.Count == 5 && ids.Distinct().Count() == ids.Count, string.Join(",", ids));
    EndTurn(g); Mana(g.Player2, 10);
    int bossSecrets = g.Player2.SecretZone.Count, mine = g.Player1.SecretZone.Count(p => p.Zone != null);
    Play(g, ToHand(g, g.Player2, "CS2_029"), g.Player2.Hero);
    Check("H4 the boss casting a spell puts no Secret for the boss", g.Player2.SecretZone.Count == bossSecrets);
    var (s2, plan2, g2) = F("rumble_hunter_110", 104);
    Mana(g2.Player1, 10); ClearExceptShrines(g2);
    Play(g2, ToHand(g2, g2.Player1, "DS1_185"), g2.Player2.Hero);
    Check("H5 deterministic: the same run seed puts the same Secret", g2.Player1.SecretZone.Skip(secrets).FirstOrDefault()?.Card.Id == put[0].Card.Id);
    Kill(g2, ShrineOf(g2, g2.Player1));
    int n = g2.Player1.SecretZone.Count; Mana(g2.Player1, 10);
    Play(g2, ToHand(g2, g2.Player1, "DS1_185"), g2.Player2.Hero);
    Check("H6 the dormant form puts nothing", g2.Player1.SecretZone.Count == n);
});
Guard("H162", () =>
{
    var (s, plan, g) = F("rumble_hunter_162", 105);
    var shrine = ShrineOf(g, g.Player1);
    Check("H7 Halazzi's Hunt lands: 0/5", shrine?.Card.Id == "TRLA_162" && shrine.Health == 5);
    Mana(g.Player1, 10); ClearExceptShrines(g);
    ToHand(g, g.Player1, "CS2_182"); ToHand(g, g.Player1, "CS2_231");
    var before = g.Player1.HandZone.ToDictionary(p => p.Id, p => p.Cost);
    var raptor = Ready(Board(g, g.Player1, "CS2_172")); var croc = Board(g, g.Player2, "CS2_120");
    Attack(g, raptor, croc);
    Check("H8 an exact kill is no Overkill: costs unchanged", g.Player1.HandZone.All(p => p.Cost == before[p.Id]), Costs(g.Player1));
    var yeti = Ready(Board(g, g.Player1, "CS2_182")); var wisp = Board(g, g.Player2, "CS2_231");
    Attack(g, yeti, wisp);
    Check("H9 Overkill: every card in the owner's hand costs (1) less, floor (0) (TRLA_162e2)",
        g.Player1.HandZone.All(p => p.Cost == Math.Max(0, before[p.Id] - 1)) && g.Player1.HandZone.All(p => Ench(p).Any(e => e.Card.Id == "TRLA_162e2")), Costs(g.Player1));
    Check("H9 the minion stays exhausted (no Horn)", yeti.IsExhausted);
    var later = ToHand(g, g.Player1, "CS2_182");
    Check("H10 the cut is on the cards then in hand only (a later card is full price)", later.Cost == 4, later.Cost.ToString());
    EndTurn(g); Mana(g.Player2, 10);
    var bossHand = g.Player2.HandZone.ToDictionary(p => p.Id, p => p.Cost);
    var e = Ready(Board(g, g.Player2, "CS2_182")); Attack(g, e, Board(g, g.Player1, "CS2_231"));
    Check("H11 the boss's Overkill cuts nothing (the grant is the owner's)", g.Player2.HandZone.Where(p => bossHand.ContainsKey(p.Id)).All(p => p.Cost == bossHand[p.Id]));
});
Guard("H163", () =>
{
    var (s, plan, g) = F("rumble_hunter_163", 106);
    var shrine = ShrineOf(g, g.Player1);
    Check("H12 Halazzi's Guise lands: 0/6", shrine?.Card.Id == "TRLA_163" && shrine.Health == 6);
    Mana(g.Player1, 10); ClearExceptShrines(g);
    var grants = new List<string>();
    bool exactlyOne = true;
    for (int i = 0; i < 12; i++)
    {
        if (g.Player1.BoardZone.Count >= 6) ClearExceptShrines(g);
        Mana(g.Player1, 10);
        var beast = (Minion)ToHand(g, g.Player1, "CS2_172"); Play(g, beast);
        var mine = Ench(beast).Where(e => DungeonRunShrines.GuiseGrants.Contains(e.Card.Id)).Select(e => e.Card.Id).ToList();
        exactlyOne &= mine.Count == 1;
        string id = mine.FirstOrDefault();
        bool keyword = id == "TRLA_163e" ? beast.IsRush : id == "TRLA_163e3" ? beast.HasTaunt : id == "TRLA_163e4" && beast.Poisonous;
        exactlyOne &= keyword;
        grants.Add(id);
    }
    Check("H13 every Beast the owner summons gains exactly one of Rush, Taunt, Poisonous, and has it", exactlyOne, string.Join(",", grants));
    Check("H14 over 12 summons all three appear (game RNG)", grants.Distinct().Count() == 3, string.Join(",", grants));
    ClearExceptShrines(g); Mana(g.Player1, 10);
    var yeti = (Minion)ToHand(g, g.Player1, "CS2_182"); Play(g, yeti);
    Check("H15 a non-Beast gains nothing", !HasGuise(yeti) && !yeti.HasTaunt && !yeti.IsRush);
    var boar = Board(g, g.Player1, "CS2_171");
    Check("H16 a Beast summoned by an effect gains one too", GuiseCount(boar) == 1);
    var enemy = Board(g, g.Player2, "CS2_172");
    Check("H17 the boss's Beasts gain nothing", !HasGuise(enemy));
    var (s2, p2, g2) = F("rumble_hunter_163", 106);
    Mana(g2.Player1, 10); ClearExceptShrines(g2);
    Minion rusher = null;
    for (int i = 0; i < 12 && rusher == null; i++)
    {
        if (g2.Player1.BoardZone.Count >= 6) ClearExceptShrines(g2);
        Mana(g2.Player1, 10); var b = (Minion)ToHand(g2, g2.Player1, "CS2_172"); Play(g2, b);
        if (Ench(b).Any(e => e.Card.Id == "TRLA_163e")) rusher = b;
    }
    var victim = Board(g2, g2.Player2, "CS2_231");
    Check("H18 a Beast given Rush attacks a minion the turn it is summoned", rusher != null && Attack(g2, rusher, victim) && victim.IsDead);
});

// ================================================================ L dormancy and revival of each shrine
foreach (string id in Six)
    Guard("L " + id, () =>
    {
        var (s, plan, g) = F(DeckOf(id), 400 + Array.IndexOf(Six, id));
        var shrine = ShrineOf(g, g.Player1);
        int slot = shrine.ZonePosition, oldId = shrine.Id;
        Mana(g.Player1, 10);
        Damage(g, shrine, 20);
        var form = ShrineOf(g, g.Player1);
        Check($"L1 {id} killed: its {id}t form in the slot it left, untouchable, 3 turns; the corpse stays out of resurrect pools",
            form?.Card.Id == id + "t" && form.ZonePosition == slot && form.Untouchable && ShrineRules.TurnsLeft(form) == 3
            && g.Player1.GraveyardZone.Any(e => e.Id == oldId && ShrineRules.IsCorpse(e)), $"{form?.Card.Id} {ShrineRules.TurnsLeft(form)}");
        var seen = new List<string>();
        for (int t = 3; t <= 7; t += 2) { ToTurn(g, t); seen.Add($"t{t}:{ShrineOf(g, g.Player1)?.Card.Id}"); }
        Check($"L2 {id} revives at the start of the owner's turn 7", string.Join(" ", seen) == $"t3:{id}t t5:{id}t t7:{id}", string.Join(" ", seen));
        var back = ShrineOf(g, g.Player1);
        Check($"L3 {id} revived at full Health as a fresh entity, generation 1", back != null && back.Id != oldId && back.Damage == 0 && back.Health == C(id).Health
            && back[ShrineRules.Generation] == 1 && ShrineRules.IsAwake(back));
        Mana(g.Player1, 10); ClearExceptShrines(g);
        bool works;
        switch (id)
        {
            case "TRLA_104": g.Process(HeroPowerTask.Any(g.Player1)); works = back.AttackDamage == g.Player1.Hero.Armor && back.AttackDamage > 0; break;
            case "TRLA_170": { var d = ToHand(g, g.Player1, "EX1_284"); int c0 = d.Cost; Play(g, ToHand(g, g.Player1, "NEW1_023")); works = d.Cost == c0 - 1; break; }
            case "TRLA_171": { var m = Board(g, g.Player1, "CS2_182"); works = OverkillRules.HasGranted(m); break; }
            case "TRLA_162": { var m = Board(g, g.Player1, "CS2_182"); works = OverkillRules.HasGranted(m) && Ench(m).Any(e => e.Card.Id == "TRLA_162e"); break; }
            case "TRLA_110": { int n = g.Player1.SecretZone.Count; Play(g, ToHand(g, g.Player1, "DS1_185"), g.Player2.Hero); works = g.Player1.SecretZone.Count == Math.Min(5, n + 1); break; }
            default: { var b = Board(g, g.Player1, "CS2_172"); works = GuiseCount(b) == 1; break; }
        }
        Check($"L4 {id}'s aura or trigger works again after the revival", works);
    });
Guard("L5", () =>
{
    var (s, plan, g) = F("rumble_hunter_162", 410);
    EndTurn(g); Mana(g.Player2, 10);
    Play(g, ToHand(g, g.Player2, "EX1_246"), ShrineOf(g, g.Player1)); // Hex
    var seen = new List<string>();
    for (int t = 3; t <= 7; t += 2) { ToTurn(g, t); seen.Add($"t{t}:{ShrineOf(g, g.Player1)?.Card.Id}"); }
    Check("L5 Hex on Halazzi's Hunt on the boss's turn: dormant, no Frog, revived on turn 7", string.Join(" ", seen) == "t3:TRLA_162t t5:TRLA_162t t7:TRLA_162"
        && !g.Player1.BoardZone.Any(m => m.Card.Id == "hexfrog"), string.Join(" ", seen));
    var (s2, p2, g2) = F("rumble_warrior_171", 411);
    var horn = ShrineOf(g2, g2.Player1);
    EndTurn(g2); Mana(g2.Player2, 10);
    Play(g2, ToHand(g2, g2.Player2, "CS1_113"), horn); // Mind Control
    Check("L6 Mind Control on Akali's Horn does nothing; the grant stays with its owner", horn.Controller == g2.Player1 && ShrineRules.IsAwake(horn) && g2.Player2.BoardZone.Count(m => m.Card.Id == "TRLA_171") == 0);
    Mana(g2.Player2, 10);
    Play(g2, ToHand(g2, g2.Player2, "EX1_564"), horn); // Faceless Manipulator
    var copy = g2.Player2.BoardZone.FirstOrDefault(m => m.Card.Id == "TRLA_171");
    var bossYeti = Board(g2, g2.Player2, "CS2_182");
    Check("L7 Faceless copies the Horn as a plain minion whose aura grants the boss's minions Overkill",
        copy != null && !ShrineRules.IsMarked(copy) && OverkillRules.HasGranted(bossYeti));
    if (copy != null) Kill(g2, copy);
    Check("L7 killed, the copy dies for good (no dormant form) and its grant ends", !g2.Player2.BoardZone.Any(m => m.Card.Id == "TRLA_171t") && !OverkillRules.HasGranted(bossYeti));
});

// ================================================================ X interactions with treasures, upgrades and teammates
Guard("X1", () =>
{
    var (s, plan, g) = F("rumble_warrior_171", 501, upgrades: new[] { DungeonRunShrines.EverlastingDevotion });
    var horns = ShrineRules.Of(g.Player1);
    Mana(g.Player1, 10); ClearExceptShrines(g);
    var yeti = Ready(Board(g, g.Player1, "CS2_182"));
    Check("X1 TRLA_804: two Horns grant TRLA_171e twice", horns.Count == 2 && OverkillRules.Granted(yeti).Count == 2, $"{horns.Count} {OverkillRules.Granted(yeti).Count}");
    Attack(g, yeti, Board(g, g.Player2, "CS2_231"));
    bool second = Attack(g, yeti, g.Player2.Hero);
    Check("X1 two grants still give one extra attack (the minion is then exhausted)", second && yeti.IsExhausted);
    Kill(g, horns[0]);
    Check("X1 one Horn dormant: the other still grants it", OverkillRules.Granted(yeti).Count == 1);
    var (s2, p2, g2) = F("rumble_hunter_162", 502, upgrades: new[] { DungeonRunShrines.EverlastingDevotion });
    Mana(g2.Player1, 10); ClearExceptShrines(g2);
    var card = ToHand(g2, g2.Player1, "CS2_182");
    Attack(g2, Ready(Board(g2, g2.Player1, "CS2_182")), Board(g2, g2.Player2, "CS2_231"));
    Check("X2 TRLA_804 with Halazzi's Hunt: two grants, two cuts (Yeti 4 -> 2)", card.Cost == 2, card.Cost.ToString());
});
Guard("X3", () =>
{
    var (s, plan, g) = F("rumble_warrior_104", 503, upgrades: new[] { DungeonRunShrines.PactWithTheLoa });
    Kill(g, ShrineOf(g, g.Player1));
    var seen = new List<string> { "t1:" + ShrineRules.TurnsLeft(ShrineOf(g, g.Player1)) };
    for (int t = 3; t <= 5; t += 2) { ToTurn(g, t); seen.Add($"t{t}:{ShrineOf(g, g.Player1)?.Card.Id}"); }
    Check("X3 TRLA_805 with Akali's Champion: 2 turns, revived at the start of turn 5", string.Join(" ", seen) == "t1:2 t3:TRLA_104t t5:TRLA_104", string.Join(" ", seen));
});
Guard("X4", () =>
{
    var (s, plan, g) = F("rumble_hunter_110", 504, treasures: new[] { "LOOTA_833" });
    int start = g.Player1.SecretZone.Count;
    Mana(g.Player1, 10); ClearExceptShrines(g);
    for (int i = 0; i < 4; i++) { Mana(g.Player1, 10); Play(g, ToHand(g, g.Player1, "DS1_185"), g.Player2.Hero); }
    var ids = g.Player1.SecretZone.Select(p => p.Card.Id).ToList();
    Check("X4 Mysterious Tome's 3 Secrets, then Halazzi's Trap to a full zone of 5 distinct Secrets", plan.PlayerTreasures.Contains("LOOTA_833") && start == 3 && ids.Count == 5 && ids.Distinct().Count() == 5,
        $"start {start} [{string.Join(",", ids)}]");
});
Guard("X5", () =>
{
    var (s, plan, g) = F("rumble_hunter_163", 505, treasures: new[] { "LOOTA_828" });
    Mana(g.Player1, 10); ClearExceptShrines(g);
    var raptor = (Minion)ToHand(g, g.Player1, "CS2_172"); Play(g, raptor);
    Check("X5 Captured Flag and Halazzi's Guise: Raptor 4/3 with one keyword grant", raptor.AttackDamage == 4 && raptor.Health == 3 && GuiseCount(raptor) == 1, $"{raptor.AttackDamage}/{raptor.Health}");
    var shrine = ShrineOf(g, g.Player1);
    Check("X5 the Flag's +1/+1 also reaches the shrine (an ordinary minion): 1/7", shrine.AttackDamage == 1 && shrine.Health == 7, $"{shrine.AttackDamage}/{shrine.Health}");
});
Guard("X6", () =>
{
    var (s, plan, g) = F("rumble_hunter_162", 506, treasures: new[] { "LOOTA_824" });
    Mana(g.Player1, 10); ClearExceptShrines(g);
    var shot = ToHand(g, g.Player1, "DS1_185"); var multi = ToHand(g, g.Player1, "DS1_183"); // Arcane Shot 1, Multi-Shot 4
    int s0 = shot.Cost, m0 = multi.Cost;
    Attack(g, Ready(Board(g, g.Player1, "CS2_182")), Board(g, g.Player2, "CS2_231"));
    Check("X6 Scrying Orb then Halazzi's Hunt: Arcane Shot 0 -> 0, Multi-Shot 3 -> 2", s0 == 0 && shot.Cost == 0 && m0 == 3 && multi.Cost == 2, $"{s0}->{shot.Cost} {m0}->{multi.Cost}");
});
Guard("X7", () =>
{
    var (s, plan, g) = F("rumble_hunter_163", 507);
    Mana(g.Player1, 10); ClearExceptShrines(g);
    var raptor = (Minion)ToHand(g, g.Player1, "TRLA_127"); Play(g, raptor);
    var victim = Board(g, g.Player2, "CS2_231");
    bool attacked = Attack(g, raptor, victim);
    var raptors = g.Player1.BoardZone.Where(m => m.Card.Id == "TRLA_127").ToList();
    Check("X7 teammate Astral Raptor (Rush, no minion type in the client data) overkills and summons another; neither is a Beast, so neither gets a Guise keyword",
        attacked && raptors.Count == 2 && raptors.All(m => GuiseCount(m) == 0 && !SabberStoneCore.Conditions.SelfCondition.IsRace(Race.BEAST).Eval(m)),
        $"{attacked} {raptors.Count}");
});
Guard("X8", () =>
{
    var (s, plan, g) = F("rumble_warrior_171", 508);
    var fixture = ShrineRules.Place(g.Player2, DungeonRunShrines.FixtureShrine); g.DeathProcessingAndAuraUpdate();
    Mana(g.Player1, 10); ClearExceptShrines(g);
    var yeti = Ready(Board(g, g.Player1, "CS2_182"));
    bool hit = Attack(g, yeti, fixture);
    Check("X8 a Yeti overkills the 0/1 Taunt fixture: it goes dormant on the boss's board and the Yeti may attack again",
        hit && ShrineOf(g, g.Player2)?.Card.Id == "TRLA_107t" && !yeti.IsExhausted, $"{ShrineOf(g, g.Player2)?.Card.Id} {yeti.IsExhausted}");
});

Console.WriteLine($"SUMMARY checks passed {passes} failed {failures}");
return failures == 0 ? 0 : 1;
