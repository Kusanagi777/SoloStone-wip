// c-shrines-S2: phase 7 family P7-S2, the Mage/Paladin/Priest board shrines: TRLA_108, TRLA_128, TRLA_129 (Mage), TRLA_105,
// TRLA_137, TRLA_138 (Paladin), TRLA_114, TRLA_146, TRLA_147 (Priest), their dormant forms, the client enchantment records
// TRLA_128e/114e/147e and Jan'alai's Progeny's Frostfire (TRLA_129s), plus the engine helpers they need (RecastRules, the
// freezer on a freeze event, the enemy-only heal inversion, the hero-power kill refresh). Every game plays a real engine game
// through the run setup path (NewRun -> BuildPlan -> DungeonRunSetup.Configure -> Game -> ApplyAfterStart -> mulligan ->
// MainBegin). A admission/data/plan/AI view; M Mage; P Paladin; R Priest; L dormancy and revival of each; X interactions with
// upgrades (TRLA_804/805), treasures (LOOTA_825/802/845/824/832) and the shrine guards.
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
DungeonRunData Load(string runText = null, string schedule = null) => DungeonRunData.Parse(runText ?? runJson, R("dungeon-run/pool-v1.json"), R("npc/decks-v1.json"),
    R("dungeon-run/catalog-v1.json"), schedule ?? scheduleJson, R("dungeon-run/tavern-v1.json"));
var data = Load();
var run = new DungeonRun(data);
var all = new DungeonRunProfile(); all.Unlocked.AddRange(data.Unlocks.Keys);
Card C(string id) => Cards.FromId(id);

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
const string Mage = "TRLA_209h_Mage", Paladin = "TRLA_209h_Paladin", Priest = "TRLA_209h_Priest";
string[] Nine = { "TRLA_108", "TRLA_128", "TRLA_129", "TRLA_105", "TRLA_137", "TRLA_138", "TRLA_114", "TRLA_146", "TRLA_147" };
string ClassOf(string id) => Array.IndexOf(Nine, id) < 3 ? "mage" : Array.IndexOf(Nine, id) < 6 ? "paladin" : "priest";
string DeckOf(string id) => "rumble_" + ClassOf(id) + "_" + id.Substring(5);
string RowOf(string deck) => deck.StartsWith("rumble_mage", StringComparison.Ordinal) ? Mage : deck.StartsWith("rumble_paladin", StringComparison.Ordinal) ? Paladin : Priest;
(DungeonRunState s, RunMatchPlan plan, Game g) F(string deck, long seed, string[] upgrades = null, string[] treasures = null)
{
    var s = NewRun(RowOf(deck), deck, seed);
    foreach (string u in upgrades ?? Array.Empty<string>()) s.ShrineUpgrades.Add(u);
    foreach (string t in treasures ?? Array.Empty<string>()) s.Treasures.Add(t);
    var plan = run.BuildPlan(s);
    var g = Start(plan, run.MatchSeed(s));
    g.Process(ChooseTask.Mulligan(g.Player1, new List<int>()));
    g.Process(ChooseTask.Mulligan(g.Player2, new List<int>()));
    g.MainBegin();
    if (g.CurrentPlayer != g.Player1) throw new Exception("run player not first");
    return (s, plan, g);
}
void Mana(Controller c, int n) { c.BaseMana = n; c.UsedMana = 0; c.OverloadLocked = 0; c.OverloadOwed = 0; }
IPlayable ToHand(Game g, Controller c, string id)
{
    while (c.HandZone.Count >= 9) { var old = c.HandZone[0]; c.HandZone.Remove(old); c.SetasideZone.Add(old); }
    var e = Entity.FromCard(c, C(id)); Generic.AddHandPhase.Invoke(c, e); g.DeathProcessingAndAuraUpdate(); return e;
}
// Empties a hand (into the set-aside zone), so a check counts only what it adds.
void ClearHand(Game g, Controller c) { foreach (var p in c.HandZone.ToArray()) { c.HandZone.Remove(p); c.SetasideZone.Add(p); } g.DeathProcessingAndAuraUpdate(); }
Minion Board(Game g, Controller c, string id) { var m = (Minion)Entity.FromCard(c, C(id)); Generic.SummonBlock(g, m, -1, c.Hero); g.DeathProcessingAndAuraUpdate(); return m; }
bool Play(Game g, IPlayable card, ICharacter target = null) => g.Process(PlayCardTask.Any(g.CurrentPlayer, card, target, -1, 0));
void EndTurn(Game g) => g.Process(EndTurnTask.Any(g.CurrentPlayer));
void ClearExceptShrines(Game g)
{
    foreach (var c in new[] { g.Player1, g.Player2 })
        foreach (var m in c.BoardZone.Slots.OfType<Minion>().Where(m => !ShrineRules.IsMarked(m)).ToArray()) { c.BoardZone.Remove(m); c.SetasideZone.Add(m); }
    g.DeathProcessingAndAuraUpdate();
}
Minion ShrineOf(Game g, Controller c) => ShrineRules.Of(c).FirstOrDefault();
void Kill(Game g, Minion m) { m.Destroy(); g.DeathProcessingAndAuraUpdate(); }
void Damage(Game g, ICharacter m, int n) { Generic.DamageCharFunc(m.Controller.Opponent.Hero, m, n, false); g.DeathProcessingAndAuraUpdate(); }
void ToTurn(Game g, int turn) { while (g.Turn < turn && g.State != State.COMPLETE) EndTurn(g); }
Minion Ready(Minion m) { m.IsExhausted = false; return m; }
bool Attack(Game g, Minion a, ICharacter t) => g.Process(MinionAttackTask.Any(g.CurrentPlayer, a, t));
bool Power(Game g, ICharacter target = null) => g.Process(HeroPowerTask.Any(g.CurrentPlayer, target));
int Frostfires(Controller c) => c.HandZone.Count(p => p.Card.Id == DungeonRunShrines.Frostfire);
IEnumerable<Enchantment> Ench(IEntity e) => (IEnumerable<Enchantment>)(e as IPlayable)?.AppliedEnchantments ?? Array.Empty<Enchantment>();

// ================================================================ A admission, data, plan, AI view
Guard("A", () =>
{
    AdmissionVerdict V(string id, AdmitScope scope) => DungeonRunAdmission.Classify(null, id, scope, null, true);
    var gen = new HashSet<string>(DungeonRunAdmission.GeneratedRejects(true));
    foreach (string id in Nine)
    {
        var v = V(id, AdmitScope.Player);
        // Phase 8 family 13: every shrine is a Rumble Run champion's too (the boss-shrine row: the run player and a boss).
        Check($"A1 {id}: scripted with its form {id}t (3-turn sleeper pointing back), admitted for the run player and (phase 8) a boss, never generated",
            ShrineRules.IsScripted(id) && ShrineRules.FormOf(id) == id + "t" && C(id).Implemented && C(id + "t").Implemented && C(id + "t").Dormant?.Turns == 3
            && C(id + "t")[(GameTag)1132] == C(id).AssetId && v.Admitted && v.Family == "boss-shrine" && v.Rule.NeverGenerated
            && V(id, AdmitScope.Boss).Admitted && !V(id, AdmitScope.Generated).Admitted && gen.Contains(id) && gen.Contains(id + "t")
            && V(id + "t", AdmitScope.Player).Family == "board-shrine-form", $"{v.Family} {v.Admitted}");
        var s = NewRun(RowOf(DeckOf(id)), DeckOf(id), 10 + Array.IndexOf(Nine, id));
        var plan = run.BuildPlan(s);
        Check($"A2 a new Rikkar run on {DeckOf(id)} has board shrine {id} (plan: one copy; not a power, treasure or deck card)",
            s.BoardShrine == id && plan.PlayerShrine == id && plan.PlayerShrineCopies == 1 && !s.Deck.Contains(id) && !plan.PlayerTreasures.Contains(id) && s.Shrine == null
            && DungeonRun.OffersPhase7Drafts(s), $"{s.BoardShrine} {plan.PlayerShrine}");
    }
    foreach (string e in new[] { "TRLA_128e", "TRLA_114e", "TRLA_147e" })
        Check($"A3 the client enchantment record {e} is scripted (no effect of its own; the shrine carries it)", C(e).Implemented && C(e).Type == CardType.ENCHANTMENT);
    var ff = C(DungeonRunShrines.Frostfire);
    var fv = V(DungeonRunShrines.Frostfire, AdmitScope.Player);
    // Phase 8 family 13: Hex Lord Malacrass's Progeny makes Frostfires for a boss too (the boss-shrine-token row).
    Check("A4 TRLA_129s Frostfire: a scripted 0-cost targeted Mage spell, admitted for the run player and (phase 8) a boss (boss-shrine-token), never generated",
        ff.Implemented && ff.Type == CardType.SPELL && ff.Cost == 0 && ff.MustHaveTargetToPlay && ff.Class == CardClass.MAGE && fv.Admitted && fv.Family == "boss-shrine-token"
        && fv.Rule.NeverGenerated && V(DungeonRunShrines.Frostfire, AdmitScope.Boss).Admitted && gen.Contains(DungeonRunShrines.Frostfire),
        $"{ff.Cost} {ff.MustHaveTargetToPlay} {fv.Family}");
    Check("A4 the shrine-card table names Frostfire as Progeny's", DungeonRunShrines.ShrineCards["TRLA_129"].SequenceEqual(new[] { "TRLA_129s" }));
    var errors = DungeonRunValidator.Validate(data, null);
    Check("A5 the validator passes the shipped data (shrines included)", errors.Count == 0 && DungeonRunValidator.ValidateShrines(data).Count == 0, string.Join(" | ", errors.Take(3)));
    var (s2, p2, g) = F("rumble_paladin_137", 22);
    var bot = FairBotState.Create(g, g.Player2.PlayerId, 1, DeckStyle.Tempo);
    var seen = bot.Facts.Opponent.Board.FirstOrDefault(c => c.CardId == "TRLA_137");
    Check("A6 the boss's FairBotState sees Shirvallah's Vengeance as an awake run shrine on the run player's board", seen != null && seen.IsRunShrine && seen.ShrineDormantTurns == 0);
    Check("A7 each of the nine has its shrine engine value in the evaluator table (none falls back to the default)",
        DeckPlanEvaluator.ShrineEngine("TRLA_108") == 2 && DeckPlanEvaluator.ShrineEngine("TRLA_128") == 2.5 && DeckPlanEvaluator.ShrineEngine("TRLA_129") == 2
        && DeckPlanEvaluator.ShrineEngine("TRLA_105") == 2.5 && DeckPlanEvaluator.ShrineEngine("TRLA_137") == 3 && DeckPlanEvaluator.ShrineEngine("TRLA_138") == 1.5
        && DeckPlanEvaluator.ShrineEngine("TRLA_114") == 2.5 && DeckPlanEvaluator.ShrineEngine("TRLA_146") == 2.5 && DeckPlanEvaluator.ShrineEngine("TRLA_147") == 2);
    Check("A8 the run's generation never yields Frostfire, a shrine or a form (RunMatchPlan.ExcludedGenerated)",
        p2.ExcludedGenerated.Contains(DungeonRunShrines.Frostfire) && p2.ExcludedGenerated.Contains("TRLA_137") && p2.ExcludedGenerated.Contains("TRLA_137t"));
    Check("A9 the shrine-model private tags (32610-32629) cover the S2 working tags",
        ShrineRules.IsPrivateTag(RecastRules.Recasting) && ShrineRules.IsPrivateTag(ShrineRules.Freezer) && ShrineRules.IsPrivateTag(ShrineRules.VengeanceResolving)
        && ShrineRules.IsPrivateTag(DungeonRunShrines.FlameKill));
});

// ================================================================ M Mage
Guard("M108", () =>
{
    var (s, plan, g) = F("rumble_mage_108", 101);
    var shrine = ShrineOf(g, g.Player1);
    Check("M1 Jan'alai's Mantle lands at Start of Game: 0/5", shrine?.Card.Id == "TRLA_108" && shrine.AttackDamage == 0 && shrine.Health == 5, $"{shrine?.Card.Id} {shrine?.Health}");
    Mana(g.Player1, 10); ClearExceptShrines(g); ClearHand(g, g.Player1);
    Check("M2 no spell in hand: Spell Damage 0", shrine.SpellPower == 0 && g.Player1.CurrentSpellPower == 0, $"{shrine.SpellPower} {g.Player1.CurrentSpellPower}");
    ToHand(g, g.Player1, "CS2_182");
    var bolt = ToHand(g, g.Player1, "CS2_024"); ToHand(g, g.Player1, "CS2_025"); ToHand(g, g.Player1, "CS2_027");
    Check("M3 three spells (and a minion) in hand: Spell Damage +3", shrine.SpellPower == 3 && g.Player1.CurrentSpellPower == 3, $"{shrine.SpellPower} {g.Player1.CurrentSpellPower}");
    var target = Board(g, g.Player2, "CS2_186"); // War Golem 7/7
    Play(g, bolt, target);
    Check("M4 Frostbolt cast from that hand deals 3 + 2 (the cast spell has left the hand): War Golem 7/7 -> 7/2",
        target.Health == 2 && shrine.SpellPower == 2, $"golem {target.Health} sp {shrine.SpellPower}");
    var golem2 = Board(g, g.Player2, "CS2_186");
    Play(g, ToHand(g, g.Player1, "TRLA_129s"), golem2);
    Check("M5 a 0-cost Frostfire from the same hand also gets the count: 1 + 2 (Arcane Explosion, Mirror Image stay)", golem2.Health == 4, golem2.Health.ToString());
    EndTurn(g); Mana(g.Player2, 10);
    int bossSp = g.Player2.CurrentSpellPower;
    ToHand(g, g.Player2, "CS2_024"); ToHand(g, g.Player2, "CS2_024");
    Check("M6 the boss's spells in hand do not count (the shrine reads its owner's hand)", shrine.SpellPower == 2 && g.Player2.CurrentSpellPower == bossSp);
    Play(g, ToHand(g, g.Player2, "EX1_332"), shrine);
    Check("M7 silenced, it has no Spell Damage whatever the hand (still a run shrine)", shrine.SpellPower == 0 && g.Player1.CurrentSpellPower == 0 && ShrineRules.IsAwake(shrine));
});
Guard("M128", () =>
{
    var (s, plan, g) = F("rumble_mage_128", 102);
    var shrine = ShrineOf(g, g.Player1);
    var hp = g.Player1.Hero.HeroPower;
    Check("M8 Jan'alai's Flame lands: 0/8; the run's Fireblast costs (0) (printed 2)", shrine?.Card.Id == "TRLA_128" && shrine.Health == 8 && hp.Cost == 0 && hp.Card.Cost == 2,
        $"{shrine?.Card.Id} {hp.Card.Id} cost {hp.Cost}");
    Check("M8 the boss's hero power keeps its cost", g.Player2.Hero.HeroPower.Cost == g.Player2.Hero.HeroPower.Card.Cost);
    Mana(g.Player1, 0); ClearExceptShrines(g);
    var w1 = Board(g, g.Player2, "CS2_231");
    bool used = Power(g, w1);
    Check("M9 with no Mana, Fireblast kills a 1/1 Wisp and is refreshed (usable again this turn)", used && w1.IsDead && !hp.IsExhausted && hp.IsPlayableByPlayer, $"{used} exhausted {hp.IsExhausted}");
    var w2 = Board(g, g.Player2, "CS2_231");
    bool again = Power(g, w2);
    Check("M10 the refreshed power kills a second Wisp and refreshes again", again && w2.IsDead && !hp.IsExhausted);
    var yeti = Board(g, g.Player2, "CS2_182");
    bool third = Power(g, yeti);
    Check("M11 a hit that does not kill (Yeti 4/5 -> 4/4) leaves it spent", third && yeti.Health == 4 && hp.IsExhausted && !hp.IsPlayableByPlayer);
    EndTurn(g); EndTurn(g); Mana(g.Player1, 0);
    var squire = Board(g, g.Player2, "EX1_008"); // Argent Squire 1/1 Divine Shield
    Power(g, squire);
    Check("M12 a hit that only pops a Divine Shield kills nothing: spent", !squire.IsDead && !squire.HasDivineShield && hp.IsExhausted);
    EndTurn(g); EndTurn(g); Mana(g.Player1, 0);
    Power(g, g.Player2.Hero);
    Check("M13 a hit on the enemy hero kills no minion: spent", hp.IsExhausted);
    EndTurn(g); Mana(g.Player2, 10);
    var bossPower = g.Player2.Hero.HeroPower;
    var mine = Board(g, g.Player1, "CS2_231");
    int bossCost = bossPower.Cost;
    Check("M14 the boss's hero power is not made free on its own turn", bossCost == bossPower.Card.Cost, bossCost.ToString());
    EndTurn(g);
    Kill(g, shrine);
    Check("M15 the Flame dormant: Fireblast costs (2) again", hp.Cost == 2, hp.Cost.ToString());
});
Guard("M129", () =>
{
    var (s, plan, g) = F("rumble_mage_129", 103);
    var shrine = ShrineOf(g, g.Player1);
    Check("M16 Jan'alai's Progeny lands: 0/7", shrine?.Card.Id == "TRLA_129" && shrine.Health == 7);
    Mana(g.Player1, 10); ClearExceptShrines(g); ClearHand(g, g.Player1);
    var golem = Board(g, g.Player2, "CS2_186");
    Play(g, ToHand(g, g.Player1, "CS2_024"), golem); // Frostbolt: 3 and Freeze
    Check("M17 Frostbolt freezes a minion: one Frostfire into the owner's hand (created by the shrine)", golem.IsFrozen && Frostfires(g.Player1) == 1
        && g.Player1.HandZone.First(p => p.Card.Id == "TRLA_129s")[GameTag.DISPLAYED_CREATOR] == shrine.Id, Frostfires(g.Player1).ToString());
    Board(g, g.Player2, "CS2_231"); Board(g, g.Player2, "CS2_231");
    Play(g, ToHand(g, g.Player1, "CS2_026")); // Frost Nova: freeze all enemy minions (3)
    Check("M18 Frost Nova freezes three enemy minions: three more Frostfires", Frostfires(g.Player1) == 4, Frostfires(g.Player1).ToString());
    Play(g, ToHand(g, g.Player1, "CS2_024"), g.Player2.Hero);
    Check("M19 freezing the enemy hero counts too (a character)", Frostfires(g.Player1) == 5 && g.Player2.Hero.IsFrozen);
    var ff = g.Player1.HandZone.First(p => p.Card.Id == "TRLA_129s");
    var wisp = g.Player2.BoardZone.First(m => m.Card.Id == "CS2_231");
    int mana = g.Player1.RemainingMana;
    Play(g, ff, wisp);
    Check("M20 Frostfire costs (0), deals 1 (the Wisp dies) and freezes nothing (no new Frostfire)", wisp.IsDead && g.Player1.RemainingMana == mana && Frostfires(g.Player1) == 4,
        $"mana {mana}->{g.Player1.RemainingMana} ff {Frostfires(g.Player1)}");
    ClearExceptShrines(g); ClearHand(g, g.Player1);
    var elemental = Ready(Board(g, g.Player1, "CS2_033")); // Water Elemental: freezes what it damages
    var yeti = Board(g, g.Player2, "CS2_182");
    Attack(g, elemental, yeti);
    Check("M21 a Water Elemental's attack freezes (HistoryFacts.Freeze path): one Frostfire", yeti.IsFrozen && Frostfires(g.Player1) == 1, Frostfires(g.Player1).ToString());
    EndTurn(g); Mana(g.Player2, 10); ClearHand(g, g.Player1);
    var mine = Board(g, g.Player1, "CS2_182");
    Play(g, ToHand(g, g.Player2, "CS2_024"), mine);
    Check("M22 the boss freezing the run player's minion gives the run player nothing", mine.IsFrozen && Frostfires(g.Player1) == 0 && Frostfires(g.Player2) == 0);
    Check("M23 no freezer is left on a character after its freeze event", ShrineRules.FreezerOf(mine) == 0 && ShrineRules.FreezerOf(yeti) == 0);
});

// ================================================================ P Paladin
Guard("P105", () =>
{
    var (s, plan, g) = F("rumble_paladin_105", 201);
    var shrine = ShrineOf(g, g.Player1);
    Check("P1 Shirvallah's Protection lands: 0/4", shrine?.Card.Id == "TRLA_105" && shrine.Health == 4);
    Mana(g.Player1, 10); ClearExceptShrines(g);
    var yeti = Board(g, g.Player1, "CS2_182");
    Damage(g, yeti, 2);
    Check("P2 a friendly Yeti survives 2 damage: it gains Divine Shield", yeti.Health == 3 && yeti.HasDivineShield);
    Damage(g, yeti, 2);
    Check("P3 the next hit is absorbed by that shield (Health stays 3)", yeti.Health == 3 && !yeti.HasDivineShield);
    Damage(g, yeti, 2);
    Check("P3 and surviving again shields it again", yeti.Health == 1 && yeti.HasDivineShield);
    var wisp = Board(g, g.Player1, "CS2_231");
    Damage(g, wisp, 3);
    Check("P4 a minion that does not survive gains nothing (the Wisp dies)", wisp.IsDead && !wisp.HasDivineShield);
    var enemy = Board(g, g.Player2, "CS2_182");
    Damage(g, enemy, 1);
    Check("P5 an enemy minion that survives gains nothing", !enemy.HasDivineShield && enemy.Health == 4);
    Damage(g, shrine, 1);
    Check("P6 the shrine itself is a friendly minion: survives 1 damage, gains Divine Shield", shrine.Health == 3 && shrine.HasDivineShield);
    var a = Board(g, g.Player1, "CS2_172"); var b = Board(g, g.Player1, "CS2_120");
    EndTurn(g); Mana(g.Player2, 10);
    Play(g, ToHand(g, g.Player2, "CS2_093")); // Consecration: 2 to all of the run player's characters
    Check("P7 a boss AoE: each friendly survivor gains Divine Shield (Raptor 3/2 dies; Croc 2/3 -> 2/1 shielded)", a.IsDead && b.Health == 1 && b.HasDivineShield, $"{a.IsDead} {b.Health} {b.HasDivineShield}");
});
Guard("P137", () =>
{
    var (s, plan, g) = F("rumble_paladin_137", 202);
    var shrine = ShrineOf(g, g.Player1);
    Check("P8 Shirvallah's Vengeance lands: 0/5", shrine?.Card.Id == "TRLA_137" && shrine.Health == 5);
    EndTurn(g); Mana(g.Player2, 10); ClearExceptShrines(g);
    int me = g.Player1.Hero.Health, boss = g.Player2.Hero.Health;
    Play(g, ToHand(g, g.Player2, "CS2_029"), g.Player1.Hero); // Fireball the run player's hero
    Check("P9 after the run player's hero takes 6, the boss's hero takes 5", g.Player1.Hero.Health == me - 6 && g.Player2.Hero.Health == boss - 5,
        $"me {me}->{g.Player1.Hero.Health} boss {boss}->{g.Player2.Hero.Health}");
    var mine = Board(g, g.Player1, "CS2_182"); int b2 = g.Player2.Hero.Health;
    Play(g, ToHand(g, g.Player2, "CS2_029"), mine);
    Check("P10 damage to a friendly minion does not answer", g.Player2.Hero.Health == b2);
    b2 = g.Player2.Hero.Health; Mana(g.Player2, 10);
    Play(g, ToHand(g, g.Player2, "CS2_029"), g.Player2.Hero);
    Check("P11 damage to the boss's own hero does not answer (one 6, no 5 back)", g.Player2.Hero.Health == b2 - 6, $"{b2}->{g.Player2.Hero.Health}");
    Mana(g.Player2, 10);
    Play(g, ToHand(g, g.Player2, "EX1_564"), shrine); // Faceless Manipulator copies the shrine for the boss
    var copy = g.Player2.BoardZone.FirstOrDefault(m => m.Card.Id == "TRLA_137");
    me = g.Player1.Hero.Health; boss = g.Player2.Hero.Health;
    Play(g, ToHand(g, g.Player2, "CS2_029"), g.Player1.Hero);
    Check("P12 a boss-side copy (plain minion): 6 to the run player, 5 back to the boss, the copy's answer to that 5 is held (recursion guard)",
        copy != null && !ShrineRules.IsMarked(copy) && g.Player1.Hero.Health == me - 6 && g.Player2.Hero.Health == boss - 5 && g.State != State.COMPLETE,
        $"me {me}->{g.Player1.Hero.Health} boss {boss}->{g.Player2.Hero.Health}");
    Check("P12 the guard is cleared after it", !g.NativeTags.ContainsKey(ShrineRules.VengeanceResolving));
    EndTurn(g); Mana(g.Player1, 10);
    boss = g.Player2.Hero.Health; me = g.Player1.Hero.Health;
    Play(g, ToHand(g, g.Player1, "CS2_029"), g.Player2.Hero);
    Check("P13 the run player's own Fireball on the boss: the copy answers with 5 to the run player, whose Vengeance does not answer that",
        g.Player2.Hero.Health == boss - 6 && g.Player1.Hero.Health == me - 5, $"boss {boss}->{g.Player2.Hero.Health} me {me}->{g.Player1.Hero.Health}");
});
Guard("P138", () =>
{
    var (s, plan, g) = F("rumble_paladin_138", 203);
    var shrine = ShrineOf(g, g.Player1);
    Check("P14 Shirvallah's Grace lands: 0/5", shrine?.Card.Id == "TRLA_138" && shrine.Health == 5);
    Mana(g.Player1, 10); ClearExceptShrines(g);
    var yeti = Board(g, g.Player1, "CS2_182");
    Mana(g.Player1, 10); Play(g, ToHand(g, g.Player1, "CS2_092"), yeti); // Blessing of Kings +4/+4
    Check("P15 Blessing of Kings on a friendly Yeti: the Yeti 8/9 and a copy cast on the shrine 4/9", yeti.AttackDamage == 8 && yeti.Health == 9 && shrine.AttackDamage == 4 && shrine.Health == 9,
        $"yeti {yeti.AttackDamage}/{yeti.Health} shrine {shrine.AttackDamage}/{shrine.Health}");
    int spells = g.Player1.NumSpellsPlayedThisGame;
    Mana(g.Player1, 10); Play(g, ToHand(g, g.Player1, "CS2_092"), shrine);
    Check("P16 cast on the shrine itself: the spell and its copy both land (4/9 -> 12/17), one hand cast counted, no copy of the copy",
        shrine.AttackDamage == 12 && shrine.Health == 17 && g.Player1.NumSpellsPlayedThisGame == spells + 1, $"{shrine.AttackDamage}/{shrine.Health}");
    var enemy = Board(g, g.Player2, "CS2_182");
    Mana(g.Player1, 10); Play(g, ToHand(g, g.Player1, "CS2_092"), enemy);
    Check("P17 cast on an enemy minion: no copy", shrine.AttackDamage == 12 && enemy.AttackDamage == 8);
    Mana(g.Player1, 10); Play(g, ToHand(g, g.Player1, "CS2_093")); // Consecration: no target
    Check("P18 an untargeted spell: no copy (the shrine takes nothing from Consecration)", shrine.Health == 17 && shrine.AttackDamage == 12);
    var raptor = Board(g, g.Player1, "CS2_172");
    Mana(g.Player1, 10); Play(g, ToHand(g, g.Player1, "EX1_371"), raptor); // Hand of Protection
    Check("P19 Hand of Protection on a friendly Raptor: both shielded", raptor.HasDivineShield && shrine.HasDivineShield);
    Mana(g.Player1, 10); Play(g, ToHand(g, g.Player1, "EX1_360"), raptor); // Humility: Attack becomes 1
    Check("P20 Humility on a friendly minion: the copy sets the shrine's Attack to 1 as well (a copy, not only buffs)", raptor.AttackDamage == 1 && shrine.AttackDamage == 1,
        $"{raptor.AttackDamage} {shrine.AttackDamage}");
    EndTurn(g); Mana(g.Player2, 10);
    var bossMinion = Board(g, g.Player2, "CS2_231");
    int sa = shrine.AttackDamage;
    Play(g, ToHand(g, g.Player2, "CS2_092"), bossMinion);
    Check("P21 the boss buffing its own minion casts nothing on the run player's shrine", shrine.AttackDamage == sa);
});

// ================================================================ R Priest
Guard("R114", () =>
{
    var (s, plan, g) = F("rumble_priest_114", 301);
    var shrine = ShrineOf(g, g.Player1);
    Check("R1 Bwonsamdi's Sanctum lands: 0/3; the owner's Deathrattles repeat 1 + 2", shrine?.Card.Id == "TRLA_114" && shrine.Health == 3
        && DeathrattleRules.Repeats(g.Player1, shrine) == 3 && DeathrattleRules.Repeats(g.Player2, shrine) == 1, $"{DeathrattleRules.Repeats(g.Player1, shrine)}");
    Mana(g.Player1, 10); ClearExceptShrines(g);
    var gnome = Board(g, g.Player1, "EX1_029"); // Leper Gnome: Deathrattle 2 to the enemy hero
    int boss = g.Player2.Hero.Health;
    Kill(g, gnome);
    Check("R2 a Leper Gnome dies: its Deathrattle three times (6 to the boss's hero)", g.Player2.Hero.Health == boss - 6, $"{boss}->{g.Player2.Hero.Health}");
    var enemyGnome = Board(g, g.Player2, "EX1_029");
    int me = g.Player1.Hero.Health;
    Kill(g, enemyGnome);
    Check("R3 the boss's Leper Gnome: once (2 to the run player)", g.Player1.Hero.Health == me - 2, $"{me}->{g.Player1.Hero.Health}");
    var harvest = Board(g, g.Player1, "FP1_002"); // Haunted Creeper: Deathrattle two 1/1 Spiders
    Kill(g, harvest);
    Check("R4 a Haunted Creeper: three Deathrattles, six Spiders (the board holds them beside the shrine)", g.Player1.BoardZone.Count(m => m.Card.Id == "FP1_002t") == 6,
        g.Player1.BoardZone.Count.ToString());
    Kill(g, shrine);
    ClearExceptShrines(g);
    var gnome2 = Board(g, g.Player1, "EX1_029"); boss = g.Player2.Hero.Health;
    Kill(g, gnome2);
    Check("R5 the Sanctum dormant: Deathrattles trigger once", g.Player2.Hero.Health == boss - 2 && DeathrattleRules.Repeats(g.Player1, gnome2) == 1);
});
Guard("R146", () =>
{
    var (s, plan, g) = F("rumble_priest_146", 302);
    var shrine = ShrineOf(g, g.Player1);
    Check("R6 Bwonsamdi's Tome lands: 0/5", shrine?.Card.Id == "TRLA_146" && shrine.Health == 5);
    Mana(g.Player1, 10); ClearExceptShrines(g); ClearHand(g, g.Player1);
    Play(g, ToHand(g, g.Player1, "DS1_185"), g.Player2.Hero); // Arcane Shot
    var got = g.Player1.HandZone.ToList();
    Check("R7 after the owner casts a spell, one random Priest spell joins the hand", got.Count >= 1 && got.Last().Card.Type == CardType.SPELL && got.Last().Card.Class == CardClass.PRIEST,
        string.Join(",", got.Select(p => p.Card.Id)));
    var generated = new List<string>();
    for (int i = 0; i < 6; i++) { Mana(g.Player1, 10); ClearHand(g, g.Player1); Play(g, ToHand(g, g.Player1, "DS1_185"), g.Player2.Hero); generated.AddRange(g.Player1.HandZone.Select(p => p.Card.Id)); }
    var rejects = new HashSet<string>(plan.ExcludedGenerated);
    Check("R8 six more casts, six more Priest spells; none the run's generation filter rejects", generated.Count == 6
        && generated.All(id => C(id).Type == CardType.SPELL && C(id).Class == CardClass.PRIEST && !rejects.Contains(id)), string.Join(",", generated));
    var (s2, p2, g2) = F("rumble_priest_146", 302);
    Mana(g2.Player1, 10); ClearExceptShrines(g2); ClearHand(g2, g2.Player1);
    Play(g2, ToHand(g2, g2.Player1, "DS1_185"), g2.Player2.Hero);
    Check("R9 deterministic: the same run seed yields the same spell", g2.Player1.HandZone.LastOrDefault()?.Card.Id == got.Last().Card.Id);
    EndTurn(g); Mana(g.Player2, 10); ClearHand(g, g.Player2);
    Play(g, ToHand(g, g.Player2, "DS1_185"), g.Player1.Hero);
    Check("R10 the boss casting a spell adds nothing for the boss", g.Player2.HandZone.Count == 0);
});
Guard("R147", () =>
{
    var (s, plan, g) = F("rumble_priest_147", 303);
    var shrine = ShrineOf(g, g.Player1);
    Check("R11 Bwonsamdi's Covenant lands: 0/8", shrine?.Card.Id == "TRLA_147" && shrine.Health == 8);
    Mana(g.Player1, 10); ClearExceptShrines(g);
    var yeti = Board(g, g.Player2, "CS2_182");
    Play(g, ToHand(g, g.Player1, "AT_055"), yeti); // Flash Heal: restore 5
    Check("R12 Flash Heal on an enemy Yeti (4/5) deals 5 instead: it dies", yeti.IsDead);
    var mine = Board(g, g.Player1, "CS2_182"); Damage(g, mine, 3);
    Play(g, ToHand(g, g.Player1, "AT_055"), mine);
    Check("R13 Flash Heal on the owner's damaged Yeti heals it (4/2 -> 4/5)", mine.Health == 5);
    var e1 = Board(g, g.Player2, "CS2_182"); Damage(g, mine, 2);
    Play(g, ToHand(g, g.Player1, "EX1_621")); // Circle of Healing: restore 4 to all minions
    Check("R14 Circle of Healing: the enemy Yeti takes 4 (4/1), the owner's Yeti is healed (4/5)", e1.Health == 1 && mine.Health == 5, $"enemy {e1.Health} mine {mine.Health}");
    int bh = g.Player2.Hero.Health;
    Mana(g.Player1, 10);
    Power(g, g.Player2.Hero); // Lesser Heal on the boss's hero
    Check("R15 the Priest power (Lesser Heal, restore 2) aimed at the boss's hero deals 2", g.Player2.Hero.Health == bh - 2, $"{bh}->{g.Player2.Hero.Health}");
    EndTurn(g); Mana(g.Player2, 10);
    var bossYeti = Board(g, g.Player2, "CS2_182"); Damage(g, bossYeti, 1);
    Play(g, ToHand(g, g.Player2, "AT_055"), bossYeti);
    // Phase 7 review: "Healing enemies damages them instead" sits on the Covenant owner's enemies, whoever heals them.
    Check("R16 the boss healing its own Yeti damages it instead (4/4 -> Flash Heal 4 -> 4/0, dead): the Covenant turns any healing of its owner's enemies", bossYeti.IsDead, $"{bossYeti.Health}");
    Play(g, ToHand(g, g.Player2, "EX1_332"), shrine);
    bossYeti = Board(g, g.Player2, "CS2_182");
    EndTurn(g); Mana(g.Player1, 10);
    Damage(g, bossYeti, 3);
    Play(g, ToHand(g, g.Player1, "AT_055"), bossYeti);
    Check("R17 silenced, the Covenant turns nothing: the enemy Yeti is healed", bossYeti.Health == 5);
});

// ================================================================ L dormancy and revival of each shrine
foreach (string id in Nine)
    Guard("L " + id, () =>
    {
        var (s, plan, g) = F(DeckOf(id), 400 + Array.IndexOf(Nine, id));
        var shrine = ShrineOf(g, g.Player1);
        int slot = shrine.ZonePosition, oldId = shrine.Id;
        Mana(g.Player1, 10);
        Damage(g, shrine, 20);
        var form = ShrineOf(g, g.Player1);
        Check($"L1 {id} killed: its {id}t form in the slot it left, untouchable, 3 turns; the corpse stays out of resurrect pools",
            form?.Card.Id == id + "t" && form.ZonePosition == slot && form.Untouchable && ShrineRules.TurnsLeft(form) == 3
            && g.Player1.GraveyardZone.Any(e => e.Id == oldId && ShrineRules.IsCorpse(e)), $"{form?.Card.Id} {ShrineRules.TurnsLeft(form)}");
        Mana(g.Player1, 10); ClearExceptShrines(g); ClearHand(g, g.Player1);
        bool asleep;
        switch (id)
        {
            case "TRLA_108": ToHand(g, g.Player1, "CS2_024"); ToHand(g, g.Player1, "CS2_025"); asleep = g.Player1.CurrentSpellPower == 0; break;
            case "TRLA_128": asleep = g.Player1.Hero.HeroPower.Cost == 2; break;
            case "TRLA_129": Play(g, ToHand(g, g.Player1, "CS2_024"), Board(g, g.Player2, "CS2_186")); asleep = Frostfires(g.Player1) == 0; break;
            case "TRLA_105": { var m = Board(g, g.Player1, "CS2_182"); Damage(g, m, 1); asleep = !m.HasDivineShield; break; }
            case "TRLA_137": { int b = g.Player2.Hero.Health; Damage(g, g.Player1.Hero, 1); asleep = g.Player2.Hero.Health == b; break; }
            case "TRLA_138": { var m = Board(g, g.Player1, "CS2_182"); Play(g, ToHand(g, g.Player1, "CS2_092"), m); asleep = ShrineOf(g, g.Player1).AttackDamage == 0 && m.AttackDamage == 8; break; }
            case "TRLA_114": asleep = DeathrattleRules.Repeats(g.Player1, ShrineOf(g, g.Player1)) == 1; break;
            case "TRLA_146": Play(g, ToHand(g, g.Player1, "DS1_185"), g.Player2.Hero); asleep = g.Player1.HandZone.Count == 0; break;
            default: { var e = Board(g, g.Player2, "CS2_182"); Play(g, ToHand(g, g.Player1, "AT_055"), e); asleep = !e.IsDead && e.Health == 5; break; }
        }
        Check($"L2 {id}'s dormant form does nothing (its aura or trigger is asleep)", asleep);
        var seen = new List<string>();
        for (int t = 3; t <= 7; t += 2) { ToTurn(g, t); seen.Add($"t{t}:{ShrineOf(g, g.Player1)?.Card.Id}"); }
        Check($"L3 {id} revives at the start of the owner's turn 7", string.Join(" ", seen) == $"t3:{id}t t5:{id}t t7:{id}", string.Join(" ", seen));
        var back = ShrineOf(g, g.Player1);
        Check($"L4 {id} revived at full Health as a fresh entity, generation 1", back != null && back.Id != oldId && back.Damage == 0 && back.Health == C(id).Health
            && back[ShrineRules.Generation] == 1 && ShrineRules.IsAwake(back));
        Mana(g.Player1, 10); ClearExceptShrines(g); ClearHand(g, g.Player1);
        bool works;
        switch (id)
        {
            case "TRLA_108": ToHand(g, g.Player1, "CS2_024"); ToHand(g, g.Player1, "CS2_025"); works = g.Player1.CurrentSpellPower == 2; break;
            case "TRLA_128": works = g.Player1.Hero.HeroPower.Cost == 0; break;
            case "TRLA_129": Play(g, ToHand(g, g.Player1, "CS2_024"), Board(g, g.Player2, "CS2_186")); works = Frostfires(g.Player1) == 1; break;
            case "TRLA_105": { var m = Board(g, g.Player1, "CS2_182"); Damage(g, m, 1); works = m.HasDivineShield; break; }
            case "TRLA_137": { int b = g.Player2.Hero.Health; Damage(g, g.Player1.Hero, 1); works = g.Player2.Hero.Health == b - 5; break; }
            case "TRLA_138": { var m = Board(g, g.Player1, "CS2_182"); Play(g, ToHand(g, g.Player1, "CS2_092"), m); works = back.AttackDamage == 4; break; }
            case "TRLA_114": works = DeathrattleRules.Repeats(g.Player1, back) == 3; break;
            case "TRLA_146": Play(g, ToHand(g, g.Player1, "DS1_185"), g.Player2.Hero); works = g.Player1.HandZone.Count == 1; break;
            default: { var e = Board(g, g.Player2, "CS2_182"); Play(g, ToHand(g, g.Player1, "AT_055"), e); works = e.IsDead; break; }
        }
        Check($"L5 {id}'s aura or trigger works again after the revival", works);
    });
Guard("L6", () =>
{
    var (s, plan, g) = F("rumble_paladin_138", 410);
    var grace = ShrineOf(g, g.Player1);
    EndTurn(g); Mana(g.Player2, 10);
    Play(g, ToHand(g, g.Player2, "EX1_246"), grace); // Hex
    Check("L6 Hex on Shirvallah's Grace: dormant (no Frog)", ShrineOf(g, g.Player1)?.Card.Id == "TRLA_138t" && !g.Player1.BoardZone.Any(m => m.Card.Id == "hexfrog"));
    var (s2, p2, g2) = F("rumble_priest_147", 411);
    var cov = ShrineOf(g2, g2.Player1);
    EndTurn(g2); Mana(g2.Player2, 10);
    Play(g2, ToHand(g2, g2.Player2, "CS1_113"), cov); // Mind Control
    Check("L7 Mind Control on Bwonsamdi's Covenant does nothing: it stays the run player's", cov.Controller == g2.Player1 && ShrineRules.IsAwake(cov));
    Mana(g2.Player2, 10);
    Play(g2, ToHand(g2, g2.Player2, "EX1_564"), cov); // Faceless copy
    var copy = g2.Player2.BoardZone.FirstOrDefault(m => m.Card.Id == "TRLA_147");
    var mine = Board(g2, g2.Player1, "CS2_182"); Damage(g2, mine, 3);
    Mana(g2.Player2, 10);
    Play(g2, ToHand(g2, g2.Player2, "AT_055"), mine);
    Check("L8 Faceless copies the Covenant for the boss as a plain minion: the boss's heal on the run player's Yeti damages it", copy != null && !ShrineRules.IsMarked(copy) && mine.IsDead);
    if (copy != null) Kill(g2, copy);
    Check("L8 killed, the copy dies for good (no dormant form)", !g2.Player2.BoardZone.Any(m => m.Card.Id == "TRLA_147t"));
    var (s3, p3, g3) = F("rumble_mage_128", 412);
    var flame = ShrineOf(g3, g3.Player1);
    EndTurn(g3); Mana(g3.Player2, 10);
    Play(g3, ToHand(g3, g3.Player2, "EX1_564"), flame);
    var flameCopy = g3.Player2.BoardZone.FirstOrDefault(m => m.Card.Id == "TRLA_128");
    Check("L9 a boss-side copy of the Flame makes the boss's own hero power free, not the run player's twice",
        flameCopy != null && g3.Player2.Hero.HeroPower.Cost == 0 && g3.Player1.Hero.HeroPower.Cost == 0);
});

// ================================================================ X interactions with treasures, upgrades and guards
Guard("X1", () =>
{
    var (s, plan, g) = F("rumble_mage_108", 501, treasures: new[] { "LOOTA_825" }); // Robe of the Magi: Spell Damage +3
    Mana(g.Player1, 10); ClearExceptShrines(g); ClearHand(g, g.Player1);
    ToHand(g, g.Player1, "CS2_025"); var bolt = ToHand(g, g.Player1, "CS2_024");
    var golem = Board(g, g.Player2, "CS2_186");
    Play(g, bolt, golem);
    Check("X1 Robe of the Magi (+3) and the Mantle (+1 for the spell left in hand): Frostbolt deals 3 + 4 (golem 7/7 -> 7/0)", golem.IsDead && g.Player1.CurrentSpellPower == 4,
        $"{golem.Health} sp {g.Player1.CurrentSpellPower}");
});
Guard("X2", () =>
{
    var (s, plan, g) = F("rumble_mage_128", 502, treasures: new[] { "LOOTA_802" }); // Justicar's Ring: upgraded, costs (1)
    var hp = g.Player1.Hero.HeroPower;
    Check("X2 Justicar's Ring upgrades Fireblast and the Flame still makes it cost (0)", plan.PlayerHeroPowerId != "CS2_034" && hp.Cost == 0, $"{hp.Card.Id} cost {hp.Cost}");
    Mana(g.Player1, 0); ClearExceptShrines(g);
    var y = Board(g, g.Player2, "CS2_120"); // River Crocolisk 2/3: the upgraded Fireblast deals 2
    Power(g, y);
    var w = Board(g, g.Player2, "CS2_231");
    Check("X2 the upgraded power kills nothing on the Croc first (2 into 3): spent", y.Health == 1 && hp.IsExhausted);
});
Guard("X3", () =>
{
    var (s, plan, g) = F("rumble_priest_114", 503, treasures: new[] { "LOOTA_845" }); // Totem of the Dead: Deathrattles trigger twice
    Check("X3 Totem of the Dead with the Sanctum: 1 + 1 + 2 = 4", DeathrattleRules.Repeats(g.Player1, ShrineOf(g, g.Player1)) == 4);
    Mana(g.Player1, 10); ClearExceptShrines(g);
    int boss = g.Player2.Hero.Health;
    Kill(g, Board(g, g.Player1, "EX1_029"));
    Check("X3 a Leper Gnome: 4 x 2 = 8 to the boss's hero", g.Player2.Hero.Health == boss - 8, $"{boss}->{g.Player2.Hero.Health}");
    var (s2, p2, g2) = F("rumble_priest_114", 504, upgrades: new[] { DungeonRunShrines.EverlastingDevotion });
    Check("X4 TRLA_804: two Sanctums, 1 + 2 + 2 = 5", ShrineRules.Of(g2.Player1).Count == 2 && DeathrattleRules.Repeats(g2.Player1, ShrineOf(g2, g2.Player1)) == 5,
        DeathrattleRules.Repeats(g2.Player1, ShrineOf(g2, g2.Player1)).ToString());
    Kill(g2, ShrineRules.Of(g2.Player1)[0]);
    Check("X4 one Sanctum dormant: 1 + 2 = 3", DeathrattleRules.Repeats(g2.Player1, ShrineOf(g2, g2.Player1)) == 3);
});
Guard("X5", () =>
{
    var (s, plan, g) = F("rumble_priest_146", 505, treasures: new[] { "LOOTA_824" }); // Khadgar's Scrying Orb: spells cost (1) less
    Mana(g.Player1, 10); ClearExceptShrines(g); ClearHand(g, g.Player1);
    Play(g, ToHand(g, g.Player1, "DS1_185"), g.Player2.Hero);
    var made = g.Player1.HandZone.LastOrDefault();
    Check("X5 Scrying Orb reaches the Tome's spell in hand: (1) less, floor (0)", made != null && made.Cost == Math.Max(0, made.Card.Cost - 1), $"{made?.Card.Id} {made?.Cost}");
});
Guard("X6", () =>
{
    var (s, plan, g) = F("rumble_paladin_137", 506, upgrades: new[] { DungeonRunShrines.PactWithTheLoa });
    Kill(g, ShrineOf(g, g.Player1));
    var seen = new List<string> { "t1:" + ShrineRules.TurnsLeft(ShrineOf(g, g.Player1)) };
    for (int t = 3; t <= 5; t += 2) { ToTurn(g, t); seen.Add($"t{t}:{ShrineOf(g, g.Player1)?.Card.Id}"); }
    Check("X6 TRLA_805 with Shirvallah's Vengeance: 2 turns, revived at the start of turn 5", string.Join(" ", seen) == "t1:2 t3:TRLA_137t t5:TRLA_137", string.Join(" ", seen));
    int b = g.Player2.Hero.Health;
    Damage(g, g.Player1.Hero, 1);
    Check("X6 the revived Vengeance answers again", g.Player2.Hero.Health == b - 5);
});
Guard("X7", () =>
{
    var (s, plan, g) = F("rumble_paladin_105", 507, treasures: new[] { "LOOTA_832" }); // Cloak of Invisibility: permanent Stealth
    var shrine = ShrineOf(g, g.Player1);
    Mana(g.Player1, 10); ClearExceptShrines(g);
    var yeti = Board(g, g.Player1, "CS2_182");
    Check("X7 Cloak of Invisibility: the shrine and the Yeti are stealthed", shrine.HasStealth && yeti.HasStealth);
    EndTurn(g); Mana(g.Player2, 10);
    Play(g, ToHand(g, g.Player2, "CS2_093")); // Consecration still hits stealthed minions
    Check("X7 a boss AoE on stealthed minions: both survive and are shielded", yeti.HasDivineShield && shrine.HasDivineShield && yeti.Health == 3 && shrine.Health == 2,
        $"yeti {yeti.Health} shrine {shrine.Health}");
});
Guard("X8", () =>
{
    var (s, plan, g) = F("rumble_paladin_138", 508, upgrades: new[] { DungeonRunShrines.EverlastingDevotion });
    var graces = ShrineRules.Of(g.Player1);
    Mana(g.Player1, 10); ClearExceptShrines(g);
    var yeti = Board(g, g.Player1, "CS2_182");
    Play(g, ToHand(g, g.Player1, "CS2_092"), yeti);
    Check("X8 TRLA_804: two Graces, each casts its own copy on itself (4/9 each), never on the other", graces.Count == 2 && graces.All(m => m.AttackDamage == 4 && m.Health == 9) && yeti.AttackDamage == 8,
        string.Join(",", graces.Select(m => m.AttackDamage + "/" + m.Health)));
    Play(g, ToHand(g, g.Player1, "CS2_092"), graces[0]);
    Check("X8 cast on one Grace: it gets the spell and its own copy (12/17), the other one copy (8/13)", graces[0].AttackDamage == 12 && graces[1].AttackDamage == 8,
        string.Join(",", graces.Select(m => m.AttackDamage + "/" + m.Health)));
});
Guard("X9", () =>
{
    var (s, plan, g) = F("rumble_mage_129", 509, upgrades: new[] { DungeonRunShrines.EverlastingDevotion });
    Mana(g.Player1, 10); ClearExceptShrines(g); ClearHand(g, g.Player1);
    Play(g, ToHand(g, g.Player1, "CS2_024"), Board(g, g.Player2, "CS2_186"));
    Check("X9 TRLA_804: two Progenies, one freeze, two Frostfires", ShrineRules.Of(g.Player1).Count == 2 && Frostfires(g.Player1) == 2, Frostfires(g.Player1).ToString());
});

Console.WriteLine($"SUMMARY checks passed {passes} failed {failures}");
return failures == 0 ? 0 : 1;
