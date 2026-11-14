// c-companions-D: phase 7 family P7-D, the deferrals of phase 7: the nine Rumble Run start-of-game companions (TRLA_121 Water
// Spirit, TRLA_164 Shadowmaw Panther, TRLA_135 Fan of Flames, TRLA_139 Parading Marshal, TRLA_148 Weaponized Zombie, TRLA_188
// Pesky Rascal, TRLA_156 Deepsea Diver, TRLA_180 Leering Bat, TRLA_172 Rallying Quilboar) with the client enchantment records
// TRLA_148e/164e/172e/180e, and TRLA_809 Windfall Blessing (TRLA_809e/809e2/809e3). Every game plays a real engine game through
// the run setup path (NewRun -> BuildPlan -> DungeonRunSetup.Configure -> Game -> ApplyAfterStart -> mulligan -> MainBegin), on
// the class's Rikkar row with a board shrine; the companion joins the run deck as a teammate record (DungeonRunDeck.Add, origin
// "teammate") and is drawn by its own Start of Game draw, never handed in by the test.
//   A admission, registry and data: scripted, a Start of Game draw source, admitted for the run player only, never generated;
//     TRLA_809's deck-wide-change row; the teammate and shrine-upgrade pools.
//   G the Start of Game draw: never in the opening hand or a mulligan replacement (an opening flag included), drawn after both
//     mulligans, after the board shrine and the Start of Game treasures (DALA_743 Dr. Boom's Remote).
//   E each companion's rule in real games, with its interactions: the class's board shrines, dormancy, the TRLA_107 fixture,
//     shrine upgrade TRLA_802, the treasures LOOTA_845 (Totem of the Dead), LOOTA_846 (Battle Totem) and LOOTA_802 (Justicar's
//     Ring), the teammates TRLA_132 (Frostweaver).
//   U TRLA_809: the grant on each run shrine, the deck-wide +1/+1 on dormancy, revival, two shrines (TRLA_804), the Deathrattle
//     doublers (LOOTA_845, TRLA_114), the deck-wide treasures (DALA_727, ULDA_044, ULDA_024), Silence and copies, the run deck
//     records unchanged, the boss side.
//   Q the drafts through the run flow: a companion offered and picked by the teammate draft (six per class now), drawn at the
//     next match's Start of Game and played; TRLA_809 offered and picked by the shrine-upgrade draft, carried into the match.
//   X full games against the real boss AI (DungeonRunBossPilot + GoldenPilot) with the companion played and TRLA_809 held:
//     no fault, no AI fallback.
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
string root = VerifyPaths.Data;
string R(string f) => File.ReadAllText(Path.Combine(root, f));
string runJson = R("dungeon-run/run-v1.json"), scheduleJson = R("dungeon-run/schedule-v1.json");
DungeonRunData Load() => DungeonRunData.Parse(runJson, R("dungeon-run/pool-v1.json"), R("npc/decks-v1.json"),
    R("dungeon-run/catalog-v1.json"), scheduleJson, R("dungeon-run/tavern-v1.json"));
var data = Load();
var run = new DungeonRun(data);
var all = new DungeonRunProfile(); all.Unlocked.AddRange(data.Unlocks.Keys);
Card C(string id) => Cards.FromId(id);
var facts = System.Text.Json.JsonDocument.Parse(File.ReadAllText(VerifyPaths.Repo("facts/violet-hold-2026-09.json")));
var factById = new Dictionary<string, System.Text.Json.JsonElement>(StringComparer.Ordinal);
foreach (var c in facts.RootElement.GetProperty("cards").EnumerateArray()) factById[c.GetProperty("id").GetString()] = c;
string FactName(string id) => factById[id].GetProperty("name").GetString();
string FactText(string id) => factById[id].TryGetProperty("text", out var t) ? t.GetString() : null;

DungeonRunState NewRun(string row, string deck, long seed)
{
    var cls = data.Class(row);
    return run.NewRun(all, cls.Id, deck ?? cls.DefaultDeck, cls.DefaultPower, seed,
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
void Finish(Game g)
{
    g.Process(ChooseTask.Mulligan(g.Player1, new List<int>()));
    g.Process(ChooseTask.Mulligan(g.Player2, new List<int>()));
    g.MainBegin();
    if (g.CurrentPlayer != g.Player1) throw new Exception("run player not first");
}
string RowOf(string deck) => "TRLA_209h_" + char.ToUpperInvariant(deck[7]) + deck.Substring(8, deck.IndexOf('_', 7) - 8);
// A new run of the deck's Rikkar row, its shrine upgrades and passive treasures set, the teammates added as teammate records.
DungeonRunState Prepared(string deck, long seed, string[] upgrades = null, string[] treasures = null, string[] mates = null)
{
    var s = NewRun(RowOf(deck), deck, seed);
    foreach (string u in upgrades ?? Array.Empty<string>()) s.ShrineUpgrades.Add(u);
    foreach (string t in treasures ?? Array.Empty<string>()) s.Treasures.Add(t);
    foreach (string m in mates ?? Array.Empty<string>()) DungeonRunDeck.Add(s, m, DungeonRunDeck.OriginTeammate);
    return s;
}
(DungeonRunState s, RunMatchPlan plan, Game g) F(string deck, long seed, string[] upgrades = null, string[] treasures = null, string[] mates = null)
{
    var s = Prepared(deck, seed, upgrades, treasures, mates);
    var plan = run.BuildPlan(s);
    var g = Start(plan, run.MatchSeed(s));
    Finish(g);
    if (s.BoardShrine == null || ShrineRules.Of(g.Player1).Count == 0) throw new Exception("no board shrine on " + deck);
    return (s, plan, g);
}
void Mana(Controller c, int n) { c.BaseMana = n; c.UsedMana = 0; c.OverloadLocked = 0; c.OverloadOwed = 0; }
IPlayable ToHand(Game g, Controller c, string id)
{
    while (c.HandZone.Count >= 9) { var old = c.HandZone[0]; c.HandZone.Remove(old); c.SetasideZone.Add(old); }
    var e = Entity.FromCard(c, C(id)); Generic.AddHandPhase.Invoke(c, e); g.DeathProcessingAndAuraUpdate(); return e;
}
IPlayable InHand(Game g, string id) => g.Player1.HandZone.FirstOrDefault(p => p.Card.Id == id);
void SetDeck(Game g, Controller c, params string[] ids)
{
    foreach (var p in c.DeckZone.ToArray()) { c.DeckZone.Remove(p); c.SetasideZone.Add(p); }
    foreach (string id in ids) c.DeckZone.Add(Entity.FromCard(c, C(id)));
    g.DeathProcessingAndAuraUpdate();
}
Minion Board(Game g, Controller c, string id) { var m = (Minion)Entity.FromCard(c, C(id)); Generic.SummonBlock(g, m, -1, c.Hero); g.DeathProcessingAndAuraUpdate(); return m; }
bool Play(Game g, IPlayable card, ICharacter target = null, int chooseOne = 0) => g.Process(PlayCardTask.Any(g.CurrentPlayer, card, target, -1, chooseOne));
void EndTurn(Game g) => g.Process(EndTurnTask.Any(g.CurrentPlayer));
void ClearExceptShrines(Game g)
{
    foreach (var c in new[] { g.Player1, g.Player2 })
        foreach (var m in c.BoardZone.Slots.OfType<Minion>().Where(m => !ShrineRules.IsMarked(m)).ToArray()) { c.BoardZone.Remove(m); c.SetasideZone.Add(m); }
    g.DeathProcessingAndAuraUpdate();
}
Minion ShrineOf(Game g, Controller c) => ShrineRules.Of(c).FirstOrDefault();
void Kill(Game g, Minion m) { m.Destroy(); g.DeathProcessingAndAuraUpdate(); }
Minion Ready(Minion m) { m.IsExhausted = false; return m; }
bool Attack(Game g, Minion a, ICharacter t) => g.Process(MinionAttackTask.Any(g.CurrentPlayer, a, t));
bool HeroAttack(Game g, ICharacter t) => g.Process(HeroAttackTask.Any(g.CurrentPlayer, t));
bool Power(Game g, ICharacter target = null) => g.Process(HeroPowerTask.Any(g.CurrentPlayer, target));
int Heal(Controller c) => c.Hero.Health;
IEnumerable<Enchantment> Ench(IEntity e) => (IEnumerable<Enchantment>)(e as IPlayable)?.AppliedEnchantments ?? Array.Empty<Enchantment>();
int Count(IEntity e, string ench) => Ench(e).Count(x => x.Card.Id == ench);
Minion Mate(Game g, string id, ICharacter target = null) { Mana(g.Player1, 10); var m = (Minion)ToHand(g, g.Player1, id); Play(g, m, target); return m; }
Minion Fixture(Game g) { ShrineRules.Place(g.Player2, DungeonRunShrines.FixtureShrine); g.DeathProcessingAndAuraUpdate(); return ShrineOf(g, g.Player2); }
// The run player's companion as its Start of Game draw put it in hand, played with 10 Mana (the Overload state kept).
Minion PlayDrawn(Game g, string id, ICharacter target = null)
{
    var m = (Minion)InHand(g, id) ?? throw new Exception(id + " was not drawn at the start of the game");
    g.Player1.BaseMana = 10; g.Player1.UsedMana = 0;
    if (!Play(g, m, target)) throw new Exception(id + " could not be played");
    return m;
}
List<int> TriggerSources(Game g, int from) => g.PowerHistory.Full.Skip(from).OfType<SabberStoneCore.Kettle.PowerHistoryBlockStart>()
    .Where(b => b.BlockType == BlockType.TRIGGER).Select(b => b.Source).ToList();
// The minion stats of the run player's deck, by entity id.
Dictionary<int, (int A, int H)> DeckStats(Controller c) => c.DeckZone.OfType<Minion>().ToDictionary(m => m.Id, m => (m.AttackDamage, m.Health));

var companions = new (string Class, string Row, string Deck, string Id)[]
{
    ("DRUID", "TRLA_209h_Druid", "rumble_druid_115", "TRLA_121"),
    ("HUNTER", "TRLA_209h_Hunter", "rumble_hunter_110", "TRLA_164"),
    ("MAGE", "TRLA_209h_Mage", "rumble_mage_128", "TRLA_135"),
    ("PALADIN", "TRLA_209h_Paladin", "rumble_paladin_105", "TRLA_139"),
    ("PRIEST", "TRLA_209h_Priest", "rumble_priest_146", "TRLA_148"),
    ("ROGUE", "TRLA_209h_Rogue", "rumble_rogue_187", "TRLA_188"),
    ("SHAMAN", "TRLA_209h_Shaman", "rumble_shaman_109", "TRLA_156"),
    ("WARLOCK", "TRLA_209h_Warlock", "rumble_warlock_178", "TRLA_180"),
    ("WARRIOR", "TRLA_209h_Warrior", "rumble_warrior_170", "TRLA_172"),
};
string[] Ids = companions.Select(c => c.Id).OrderBy(x => x, StringComparer.Ordinal).ToArray();
const string Windfall = "TRLA_809";

// ================================================================ A admission, registry, data
Guard("A", () =>
{
    AdmissionVerdict V(string id, AdmitScope scope, bool catalog = true) => DungeonRunAdmission.Classify(null, id, scope, null, catalog);
    var gen = new HashSet<string>(DungeonRunAdmission.GeneratedRejects(true));
    var genLegacy = new HashSet<string>(DungeonRunAdmission.GeneratedRejects(false));
    Check("A1 the nine companions, one per Rumble Run class (DungeonRunShrines.CompanionIds)",
        DungeonRunShrines.CompanionIds.SequenceEqual(Ids) && companions.Select(c => c.Class).Distinct().Count() == 9, string.Join(",", DungeonRunShrines.CompanionIds));
    foreach (var (cls, row, deck, id) in companions)
    {
        var card = C(id);
        var v = V(id, AdmitScope.Player);
        Check($"A2 {id} {card.Name}: scripted, a ScriptedTeammate, uncollectible, a {cls} minion in its class's teammate row, the client's name and text",
            card.Implemented && DungeonRunShrines.ScriptedTeammates.Contains(id) && DungeonRunShrines.IsCompanion(id) && !card.Collectible && card.Type == CardType.MINION
            && card.Class.ToString() == cls && data.Teammates[cls].Contains(id) && card.Name == FactName(id) && card.Text == FactText(id), card.Class.ToString());
        Check($"A3 {id}: a Start of Game source (StartOfGameRules), START_OF_GAME on the card", StartOfGameRules.HasEffect(card) && card[GameTag.START_OF_GAME] == 1);
        Check($"A4 {id}: admitted for the run player only (start-of-game-draw row, revealed to both players), never generated, no pending requirement",
            v.Admitted && v.Family == "start-of-game-draw" && v.Rule.NeverGenerated && v.Requires == null && v.Rule.Lift == DungeonRunAdmission.RunTeammateLift
            && v.Rule.Reason.Contains("revealed to both players") && v.Rule.Reason.Contains("c-companions-D")
            && !V(id, AdmitScope.Boss).Admitted && !V(id, AdmitScope.Generated).Admitted && gen.Contains(id) && genLegacy.Contains(id)
            && V(id, AdmitScope.Player, false).Admitted, $"{v.Family} {v.Admitted} {v.Requires} gen {gen.Contains(id)}/{genLegacy.Contains(id)} boss {V(id, AdmitScope.Boss).Admitted}");
    }
    var sog = DungeonRunAdmission.RuleFor("TRLA_121");
    Check("A5 the companions' row holds exactly the nine (the treasures' start-of-game-draw row and ULDA_111's keep theirs)",
        sog.Ids.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(Ids) && DungeonRunAdmission.RuleFor("DALA_707") != sog && DungeonRunAdmission.RuleFor("ULDA_111") != sog
        && DungeonRunAdmission.RuleFor("DALA_707").Family == "start-of-game-draw");
    var w = V(Windfall, AdmitScope.Player);
    Check("A6 TRLA_809 Windfall Blessing: scripted, a ScriptedUpgrade with its player enchantment TRLA_809e, whose grant is TRLA_809e2",
        C(Windfall).Implemented && DungeonRunShrines.ScriptedUpgrades.TryGetValue(Windfall, out string pe) && pe == "TRLA_809e"
        && DungeonRunShrines.Grants["TRLA_809e"] == "TRLA_809e2" && DungeonRunShrines.IsGrant("TRLA_809e2") && data.ShrineUpgrades.Contains(Windfall));
    Check("A7 TRLA_809: admitted for the run player (deck-wide-change, never generated), the boss side lifted by BossDeckLedgerLift (OwnDeckLedger)",
        w.Admitted && w.Family == "deck-wide-change" && w.Rule.NeverGenerated && w.Requires == null && w.Rule.Lift == DungeonRunAdmission.BossDeckLedgerLift
        && w.Rule.Reason.Contains("OwnDeckLedger") && !V(Windfall, AdmitScope.Boss).Admitted && !V(Windfall, AdmitScope.Generated).Admitted && gen.Contains(Windfall)
        && w.Rule.Ids.SequenceEqual(new[] { Windfall }), $"{w.Family} {w.Rule?.Lift}");
    foreach (string e in new[] { "TRLA_148e", "TRLA_164e", "TRLA_172e", "TRLA_180e", "TRLA_809e", "TRLA_809e2", "TRLA_809e3" })
        Check($"A8 enchantment {e} is scripted, the client's own record", C(e).Implemented && C(e).Type == CardType.ENCHANTMENT && C(e).Name == FactName(e));
    foreach (var (cls, row, deck, id) in companions)
    {
        var s = NewRun(row, deck, 21);
        var pool = run.TeammatePool(s);
        Check($"A9 a new {cls} Rikkar run's teammate pool is its class's six teammates, the companion {id} included",
            pool.Count == 6 && pool.Contains(id) && pool.SequenceEqual(data.Teammates[cls].OrderBy(x => x, StringComparer.Ordinal)), string.Join(",", pool));
        if (cls == "DRUID")
            Check("A10 a new Rikkar run's shrine-upgrade pool is all nine upgrades, TRLA_809 included", run.ShrineUpgradePool(s).SequenceEqual(data.ShrineUpgrades.OrderBy(x => x, StringComparer.Ordinal))
                && run.ShrineUpgradePool(s).Count == 9, string.Join(",", run.ShrineUpgradePool(s)));
    }
    var pending = data.Teammates.Values.SelectMany(v => v).Where(id => !DungeonRunShrines.ScriptedTeammates.Contains(id)).ToList();
    Check("A11 no teammate of the 54 is unscripted any more, and every one is admitted for the run player", pending.Count == 0
        && data.Teammates.Values.SelectMany(v => v).Count() == 54 && data.Teammates.Values.SelectMany(v => v).All(id => V(id, AdmitScope.Player).Admitted), string.Join(",", pending));
    Check("A12 no shrine upgrade is unscripted any more", data.ShrineUpgrades.All(DungeonRunShrines.ScriptedUpgrades.ContainsKey) && DungeonRunShrines.ScriptedUpgrades.Count == 9);
    var errors = DungeonRunValidator.Validate(data, null);
    Check("A13 the validator passes the shipped data (teammate table, registry, upgrades)", errors.Count == 0, string.Join(" | ", errors.Take(3)));
    var s2 = NewRun("TRLA_209h_Druid", "rumble_druid_115", 22);
    DungeonRunDeck.Add(s2, "TRLA_121", DungeonRunDeck.OriginTeammate);
    var plan = run.BuildPlan(s2);
    Check("A14 a companion and TRLA_809 are in the run's generation filter (never a random card for either side)", plan.ExcludedGenerated.Contains("TRLA_121")
        && plan.ExcludedGenerated.Contains(Windfall), $"{plan.ExcludedGenerated.Contains("TRLA_121")} {plan.ExcludedGenerated.Contains(Windfall)}");
});

// ================================================================ G the Start of Game draw
foreach (var (cls, row, deck, id) in companions)
    Guard("G-" + id, () =>
    {
        long seed = 100 + Array.FindIndex(companions, c => c.Id == id);
        var s = Prepared(deck, seed, treasures: new[] { "DALA_743" }, mates: new[] { id });
        var plan = run.BuildPlan(s);
        Check($"G1 {cls}: the plan deck holds the companion {id} once (a teammate record) and DALA_743 as a passive", plan.PlayerDeck.Count(c => c.Id == id) == 1
            && s.Cards.Count(c => c.Id == id && c.Origin == DungeonRunDeck.OriginTeammate) == 1 && plan.PlayerTreasures.Contains("DALA_743"));
        var g = Start(plan, run.MatchSeed(s));
        var p1 = g.Player1;
        Check($"G2 {cls}: before the mulligan {id} waits in the deck, not in the opening hand", p1.DeckZone.Any(p => p.Card.Id == id) && InHand(g, id) == null,
            string.Join(",", p1.HandZone.Select(p => p.Card.Id)));
        int opening = p1.HandZone.Count;
        g.Process(ChooseTask.Mulligan(p1, p1.Choice.Choices.ToList()));
        g.Process(ChooseTask.Mulligan(g.Player2, new List<int>()));
        Check($"G3 {cls}: a full mulligan never deals it as a replacement", InHand(g, id) == null && p1.HandZone.Count == opening);
        int before = g.PowerHistory.Full.Count;
        g.MainBegin();
        var drawn = InHand(g, id);
        Check($"G4 {cls}: after MainBegin it is in hand (drawn by its Start of Game), no longer in the deck", drawn != null && !p1.DeckZone.Any(p => p.Card.Id == id)
            && p1.HandZone.Count(p => p.Card.Id == id) == 1);
        var shrines = ShrineRules.Of(p1);
        var bots = p1.BoardZone.Where(m => !ShrineRules.IsMarked(m)).ToList();
        Check($"G5 {cls}: the board shrine stands left of the three Boom Bots", shrines.Count == 1 && shrines[0].ZonePosition == 0 && bots.Count == 3
            && bots.All(b => b.ZonePosition > 0), string.Join(",", p1.BoardZone.Select(m => m.Card.Id)));
        var order = TriggerSources(g, before);
        int shrineAt = order.IndexOf(shrines[0].Id);
        var remote = p1.AppliedEnchantments?.FirstOrDefault(e => e.Card.Id == DungeonRunContent.Get("fx_DALA_743").ShellId);
        int remoteAt = remote == null ? -1 : order.IndexOf(remote.Id);
        int drawAt = drawn == null ? -1 : order.IndexOf(drawn.Id);
        Check($"G6 {cls}: Start of Game order: the shrine lands, then the Boom Bots, then the companion is drawn (StartOfGameRules priorities -100, 0, 10)",
            shrineAt >= 0 && remoteAt > shrineAt && drawAt > remoteAt, $"shrine {shrineAt} remote {remoteAt} draw {drawAt}");
        Check($"G7 {cls}: on turn 1 the run player holds the opening hand, the companion and the turn's draw", p1.HandZone.Count == opening + 2, $"{p1.HandZone.Count} vs {opening}+2");
    });
Guard("G-opening", () =>
{
    // An opening flag (a tavern op) on the companion's record: a Start of Game draw is never dealt in the opening hand.
    var s = Prepared("rumble_druid_115", 130, mates: new[] { "TRLA_121" });
    s.Cards.Last(c => c.Id == "TRLA_121").Opening = true;
    var plan = run.BuildPlan(s);
    var g = Start(plan, run.MatchSeed(s));
    Check("G8 an opening-flagged companion is still not in the opening hand", InHand(g, "TRLA_121") == null && plan.PlayerDeckSlots != null
        && plan.PlayerDeckSlots.Any(x => x?.Opening == true));
    Finish(g);
    Check("G9 it is drawn at the start of the game", InHand(g, "TRLA_121") != null);
    // Elise's ULDA_302 and Heist's DALA_707 are Start of Game draws at priority 0: beside a companion they are drawn first.
    var s2 = Prepared("rumble_druid_116", 131, mates: new[] { "TRLA_121" });
    DungeonRunDeck.Add(s2, "DALA_707", DungeonRunDeck.OriginTreasure); s2.ActiveTreasures.Add("DALA_707");
    var g2 = Start(run.BuildPlan(s2), run.MatchSeed(s2));
    g2.Process(ChooseTask.Mulligan(g2.Player1, new List<int>())); g2.Process(ChooseTask.Mulligan(g2.Player2, new List<int>()));
    int before = g2.PowerHistory.Full.Count;
    g2.MainBegin();
    var order = TriggerSources(g2, before);
    var hench = InHand(g2, "DALA_707"); var spirit = InHand(g2, "TRLA_121");
    Check("G10 with a Start of Game treasure draw (DALA_707): shrine, then the treasure's draw, then the companion's", hench != null && spirit != null
        && order.IndexOf(ShrineOf(g2, g2.Player1).Id) >= 0 && order.IndexOf(ShrineOf(g2, g2.Player1).Id) < order.IndexOf(hench.Id) && order.IndexOf(hench.Id) < order.IndexOf(spirit.Id));
    // A full hand at the start of the game: the draw burns as an ordinary overdraw would not; the engine's draw rule decides.
    var s3 = Prepared("rumble_druid_112", 132, mates: new[] { "TRLA_121" });
    var g3 = Start(run.BuildPlan(s3), run.MatchSeed(s3));
    var p3 = g3.Player1;
    Finish(g3);
    var g4 = Start(run.BuildPlan(s3), run.MatchSeed(s3));
    Finish(g4);
    Check("G11 deterministic: the same run seed gives the same hand after the Start of Game draw", p3.HandZone.Select(p => p.Card.Id).SequenceEqual(g4.Player1.HandZone.Select(p => p.Card.Id)));
});

// ================================================================ E the companions in play
Guard("E121", () =>
{
    var (s, plan, g) = F("rumble_druid_115", 201, mates: new[] { "TRLA_121" });
    ClearExceptShrines(g);
    Mana(g.Player1, 3);
    var spirit = PlayDrawn(g, "TRLA_121"); Mana(g.Player1, 3);
    Check("E1 TRLA_121 Water Spirit (0 Cost, 2/1) lands with Wardruid Loa's +1/+1 (TRLA_115, after you summon a minion): 3/2",
        spirit.AttackDamage == 3 && spirit.Health == 2 && Count(spirit, "TRLA_115e") == 1, $"{spirit.AttackDamage}/{spirit.Health}");
    Kill(g, spirit);
    Check("E2 its Deathrattle gains 2 full Mana Crystals: 3 -> 5, all usable", g.Player1.BaseMana == 5 && g.Player1.RemainingMana == 5, $"{g.Player1.BaseMana} {g.Player1.RemainingMana}");
    Mana(g.Player1, 9);
    var sp9 = (Minion)ToHand(g, g.Player1, "TRLA_121"); Play(g, sp9);
    Kill(g, sp9);
    Check("E3 at 9 Mana Crystals stop at 10", g.Player1.BaseMana == 10, g.Player1.BaseMana.ToString());
    // Killed on the boss's turn (Flamestrike): the run player gains the crystals then.
    Mana(g.Player1, 4); var s2 = (Minion)ToHand(g, g.Player1, "TRLA_121"); Play(g, s2);
    EndTurn(g); Mana(g.Player2, 10);
    Play(g, ToHand(g, g.Player2, "CS2_032"));
    Check("E4 killed by the boss's Flamestrike on the boss's turn: the run player has 2 more Mana Crystals (4 -> 6)", s2.IsDead && g.Player1.BaseMana == 6, g.Player1.BaseMana.ToString());
    var (sT, pT, gT) = F("rumble_druid_112", 202, treasures: new[] { "LOOTA_845" }, mates: new[] { "TRLA_121" });
    ClearExceptShrines(gT); Mana(gT.Player1, 3);
    var sp = PlayDrawn(gT, "TRLA_121"); Mana(gT.Player1, 3);
    Kill(gT, sp);
    Check("E5 with Totem of the Dead (LOOTA_845, Deathrattles twice): 4 Mana Crystals (3 -> 7)", gT.Player1.BaseMana == 7, gT.Player1.BaseMana.ToString());
    Check("E6 Ancestral Fury (TRLA_112: refresh Mana on Armor gain) is untouched by the gain (no Armor)", gT.Player1.Hero.Armor == 0 && gT.Player1.RemainingMana == 7);
});
Guard("E164", () =>
{
    var (s, plan, g) = F("rumble_hunter_110", 211, mates: new[] { "TRLA_164" });
    ClearExceptShrines(g); Mana(g.Player1, 10);
    var yeti = Board(g, g.Player2, "CS2_182");
    var panther = PlayDrawn(g, "TRLA_164");
    var attacks = g.Player1.Options().OfType<MinionAttackTask>().Where(o => o.Source == panther).ToList();
    Check("E7 TRLA_164 Shadowmaw Panther is a Beast and has Rush itself (TRLA_164e): it may attack the boss's Yeti on the turn it lands, not the boss's hero",
        panther.Card.IsRace(Race.BEAST) && panther[GameTag.RUSH] == 1 && Count(panther, "TRLA_164e") == 1 && attacks.Any(o => o.Target == yeti) && !attacks.Any(o => o.Target == g.Player2.Hero),
        string.Join(",", attacks.Select(o => o.Target?.Card.Id)));
    Mana(g.Player1, 10);
    var raptor = (Minion)ToHand(g, g.Player1, "CS2_172"); Play(g, raptor);
    Check("E8 a Beast played after it (Bloodfen Raptor) has Rush", raptor.Card.IsRace(Race.BEAST) && raptor[GameTag.RUSH] == 1 && Count(raptor, "TRLA_164e") == 1);
    var mine = (Minion)ToHand(g, g.Player1, "CS2_182"); Play(g, mine);
    Check("E9 a non-Beast (Chillwind Yeti) has none", mine[GameTag.RUSH] == 0 && Count(mine, "TRLA_164e") == 0);
    Check("E10 the Raptor attacks the boss's Yeti at once", Attack(g, raptor, yeti) && yeti.Health == 2);
    Check("E11 the shrine (not a Beast) has no Rush", ShrineOf(g, g.Player1)[GameTag.RUSH] == 0);
    Play(g, ToHand(g, g.Player1, "EX1_332"), panther); // Silence
    Check("E12 the Panther silenced: its Beasts lose the Rush", raptor[GameTag.RUSH] == 0 && Count(raptor, "TRLA_164e") == 0 && panther[GameTag.RUSH] == 0);
    var (s2, p2, g2) = F("rumble_hunter_163", 212, mates: new[] { "TRLA_164" });
    ClearExceptShrines(g2); Mana(g2.Player1, 10);
    var p = PlayDrawn(g2, "TRLA_164");
    Check("E13 with Halazzi's Guise (TRLA_163) the Panther also gains one of Rush, Taunt or Poisonous, beside its own Rush",
        Count(p, "TRLA_164e") == 1 && DungeonRunShrines.GuiseGrants.Sum(e => Count(p, e)) == 1 && p[GameTag.RUSH] >= 1, string.Join(",", Ench(p).Select(e => e.Card.Id)) + " rush " + p[GameTag.RUSH]);
    Kill(g2, ShrineOf(g2, g2.Player1));
    var form = ShrineOf(g2, g2.Player1);
    Check("E14 the dormant shrine form gets no Rush from the Panther", ShrineRules.IsDormantForm(form) && Count(form, "TRLA_164e") == 0);
});
Guard("E135", () =>
{
    var (s, plan, g) = F("rumble_mage_128", 221, mates: new[] { "TRLA_135" });
    ClearExceptShrines(g); Mana(g.Player1, 10);
    var basic = g.Player1.Hero.HeroPower;
    Check("E15 the Mage Rikkar run's power is Fireblast (CS2_034), (0) with Jan'alai's Flame (TRLA_128)", basic.Card.Id == "CS2_034" && basic.Cost == 0, basic.Card.Id + " " + basic.Cost);
    Power(g, g.Player2.Hero);
    PlayDrawn(g, "TRLA_135");
    var up = g.Player1.Hero.HeroPower;
    Check("E16 TRLA_135 Fan of Flames upgrades it: Fireblast Rank 2 (AT_132_MAGE), a fresh ready power, still (0) with the shrine",
        up.Card.Id == "AT_132_MAGE" && up != basic && !up.IsExhausted && up.Cost == 0, $"{up.Card.Id} exhausted {up.IsExhausted} cost {up.Cost}");
    int boss = Heal(g.Player2);
    Check("E17 the upgraded power deals 2", Power(g, g.Player2.Hero) && Heal(g.Player2) == boss - 2, $"{boss} -> {Heal(g.Player2)}");
    Mate(g, "TRLA_135");
    Check("E18 a second Fan keeps the upgraded power (no further form)", g.Player1.Hero.HeroPower == up);
    Kill(g, ShrineOf(g, g.Player1));
    Check("E19 the shrine dormant: the upgraded power costs its own (2)", g.Player1.Hero.HeroPower.Cost == 2, g.Player1.Hero.HeroPower.Cost.ToString());
    // Justicar's Ring (LOOTA_802): the run's power starts upgraded; the Fan changes nothing.
    var (s2, p2, g2) = F("rumble_mage_108", 222, treasures: new[] { "LOOTA_802" }, mates: new[] { "TRLA_135" });
    var ringed = g2.Player1.Hero.HeroPower;
    PlayDrawn(g2, "TRLA_135");
    Check("E20 with Justicar's Ring the power is AT_132_MAGE from the start and the Fan keeps it (same entity, the Ring's (1))",
        ringed.Card.Id == "AT_132_MAGE" && g2.Player1.Hero.HeroPower == ringed && ringed.Cost == 1, ringed.Card.Id + " " + ringed.Cost);
    // Battle Totem (LOOTA_846, Battlecries twice): one upgrade.
    var (s3, p3, g3) = F("rumble_mage_129", 223, treasures: new[] { "LOOTA_846" }, mates: new[] { "TRLA_135" });
    PlayDrawn(g3, "TRLA_135");
    Check("E21 with Battle Totem two Battlecries: the power is AT_132_MAGE", g3.Player1.Hero.HeroPower.Card.Id == "AT_132_MAGE");
    // Frostweaver (TRLA_132: the hero power also Freezes) works on the upgraded power.
    var (s4, p4, g4) = F("rumble_mage_108", 224, mates: new[] { "TRLA_135" });
    ClearExceptShrines(g4);
    Mate(g4, "TRLA_132");
    PlayDrawn(g4, "TRLA_135");
    var yeti = Board(g4, g4.Player2, "CS2_182");
    Power(g4, yeti);
    Check("E22 with Frostweaver (TRLA_132) the upgraded power hits for 2 and Freezes", yeti.Health == 3 && yeti.IsFrozen && g4.Player1.Hero.HeroPower.Card.Id == "AT_132_MAGE");
});
Guard("E139", () =>
{
    var (s, plan, g) = F("rumble_paladin_105", 231, mates: new[] { "TRLA_139" });
    ClearExceptShrines(g); Mana(g.Player1, 10);
    Play(g, ToHand(g, g.Player1, "CS2_106")); // Fiery War Axe
    var axe = g.Player1.Hero.Weapon;
    var marshal = PlayDrawn(g, "TRLA_139");
    var w = g.Player1.Hero.Weapon;
    Check("E23 TRLA_139 Parading Marshal (3/3) equips a Truesilver Champion (CS2_097, 4/2), replacing the Fiery War Axe",
        marshal.AttackDamage == 3 && marshal.Health == 3 && w?.Card.Id == "CS2_097" && w.AttackDamage == 4 && w.Durability == 2 && axe != null && axe.Zone?.Type == Zone.GRAVEYARD,
        w?.Card.Id);
    g.Player1.Hero.Damage = 5;
    int boss = Heal(g.Player2);
    HeroAttack(g, g.Player2.Hero);
    Check("E24 the hero attacks: Truesilver restores 2 (25 -> 27) and deals 4", Heal(g.Player1) == 27 && Heal(g.Player2) == boss - 4 && g.Player1.Hero.Weapon.Durability == 1,
        $"{Heal(g.Player1)} {Heal(g.Player2)}");
    var (s2, p2, g2) = F("rumble_paladin_138", 232, treasures: new[] { "LOOTA_846" }, mates: new[] { "TRLA_139" });
    ClearExceptShrines(g2);
    PlayDrawn(g2, "TRLA_139");
    var w2 = g2.Player1.Hero.Weapon;
    Check("E25 with Battle Totem two Battlecries: one Truesilver equipped (the second replaced the first, 4/2), one in the graveyard",
        w2?.Card.Id == "CS2_097" && w2.Durability == 2 && g2.Player1.GraveyardZone.Count(p => p.Card.Id == "CS2_097") == 1);
    // TRLA_137: 5 damage to the boss's hero after the run player's hero takes damage.
    var (s3, p3, g3) = F("rumble_paladin_137", 233, mates: new[] { "TRLA_139" });
    ClearExceptShrines(g3);
    PlayDrawn(g3, "TRLA_139");
    var yeti = Board(g3, g3.Player2, "CS2_182");
    g3.Player1.Hero.Damage = 5; boss = Heal(g3.Player2);
    HeroAttack(g3, yeti);
    Check("E26 with TRLA_137 the hero attacks a Yeti: healed 2 then hit for 4 (25 -> 23), the shrine answers the hit with 5 to the boss's hero, the Yeti takes 4",
        Heal(g3.Player1) == 23 && Heal(g3.Player2) == boss - 5 && yeti.Health == 1, $"me {Heal(g3.Player1)} boss {boss}->{Heal(g3.Player2)} yeti {yeti.Health}");
});
Guard("E148", () =>
{
    var (s, plan, g) = F("rumble_priest_146", 241, mates: new[] { "TRLA_148" });
    ClearExceptShrines(g); Mana(g.Player1, 10);
    var shrine = ShrineOf(g, g.Player1);
    int a0 = shrine.AttackDamage, h0 = shrine.Health;
    var zombie = PlayDrawn(g, "TRLA_148");
    Check("E27 TRLA_148 Weaponized Zombie is a 2/3", zombie.AttackDamage == 2 && zombie.Health == 3);
    Kill(g, zombie);
    Check("E28 its Deathrattle: the only friendly minion, the run shrine, gets +1/+1 (TRLA_148e)", Count(shrine, "TRLA_148e") == 1 && shrine.AttackDamage == a0 + 1 && shrine.Health == h0 + 1,
        $"{shrine.AttackDamage}/{shrine.Health}");
    var yeti = Board(g, g.Player1, "CS2_182");
    Kill(g, Mate(g, "TRLA_148"));
    Check("E29 with a Yeti beside the shrine exactly one of them gets +1/+1", Count(shrine, "TRLA_148e") + Count(yeti, "TRLA_148e") == 2);
    var enemy = Board(g, g.Player2, "CS2_182");
    Kill(g, yeti); Kill(g, shrine);
    var form = ShrineOf(g, g.Player1);
    Kill(g, Mate(g, "TRLA_148"));
    Check("E30 only the dormant shrine form left: no target (it is untouchable), the boss's Yeti gets nothing", ShrineRules.IsDormantForm(form) && Count(form, "TRLA_148e") == 0
        && Count(enemy, "TRLA_148e") == 0);
    var (s2, p2, g2) = F("rumble_priest_114", 242, mates: new[] { "TRLA_148" });
    ClearExceptShrines(g2); Mana(g2.Player1, 10);
    var sanctum = ShrineOf(g2, g2.Player1);
    Kill(g2, PlayDrawn(g2, "TRLA_148"));
    Check("E31 with Bwonsamdi's Sanctum (TRLA_114: Deathrattles two extra times) the shrine gets +1/+1 three times", Count(sanctum, "TRLA_148e") == 3, Count(sanctum, "TRLA_148e").ToString());
    var (s3, p3, g3) = F("rumble_priest_114", 243, treasures: new[] { "LOOTA_845" }, mates: new[] { "TRLA_148" });
    ClearExceptShrines(g3); Mana(g3.Player1, 10);
    var sanctum3 = ShrineOf(g3, g3.Player1);
    Kill(g3, PlayDrawn(g3, "TRLA_148"));
    Check("E32 with the Sanctum and Totem of the Dead: four times", Count(sanctum3, "TRLA_148e") == 4, Count(sanctum3, "TRLA_148e").ToString());
    string Target(long seed)
    {
        var (sx, px, gx) = F("rumble_priest_147", seed, mates: new[] { "TRLA_148" });
        ClearExceptShrines(gx); Mana(gx.Player1, 10);
        var a = Board(gx, gx.Player1, "CS2_182"); var b = Board(gx, gx.Player1, "CS2_120");
        Kill(gx, PlayDrawn(gx, "TRLA_148"));
        return string.Join(",", gx.Player1.BoardZone.Select(m => m.Card.Id + ":" + Count(m, "TRLA_148e")));
    }
    Check("E33 deterministic: the same run seed buffs the same minion", Target(244) == Target(244));
});
Guard("E188", () =>
{
    var (s, plan, g) = F("rumble_rogue_187", 251, mates: new[] { "TRLA_188" });
    ClearExceptShrines(g); Mana(g.Player1, 10);
    var rascal = PlayDrawn(g, "TRLA_188");
    Check("E34 TRLA_188 Pesky Rascal: a 1/1 with Immune", rascal.AttackDamage == 1 && rascal.Health == 1 && rascal.IsImmune,
        $"{rascal.AttackDamage}/{rascal.Health} immune {rascal.IsImmune} tag {rascal[GameTag.IMMUNE]}");
    EndTurn(g); Mana(g.Player2, 10);
    var yeti = Ready(Board(g, g.Player2, "CS2_182"));
    var opts = g.Player2.Options();
    var fireball = ToHand(g, g.Player2, "CS2_029"); var hex = ToHand(g, g.Player2, "EX1_246");
    opts = g.Player2.Options();
    Check("E35 the boss can neither attack it nor aim Fireball or Hex at it", !opts.OfType<MinionAttackTask>().Any(o => o.Target == rascal)
        && !opts.OfType<PlayCardTask>().Any(o => o.Target == rascal) && opts.OfType<PlayCardTask>().Any(o => o.Source == fireball));
    Play(g, ToHand(g, g.Player2, "CS2_062")); // Hellfire: 3 to every character
    Check("E36 the boss's Hellfire does not hurt it", !rascal.IsDead && rascal.Health == 1 && rascal.Damage == 0);
    EndTurn(g); Mana(g.Player1, 10);
    Check("E37 it attacks the boss's Yeti and takes nothing back", Attack(g, rascal, yeti) && yeti.Health == 1 && !rascal.IsDead && rascal.Damage == 0, $"yeti {yeti.Health}");
    EndTurn(g); Mana(g.Player2, 10);
    Play(g, ToHand(g, g.Player2, "EX1_312")); // Twisting Nether: destroy all minions
    Check("E38 a destroy effect (Twisting Nether) still kills it; the run shrine goes dormant", rascal.IsDead && ShrineRules.IsDormantForm(ShrineOf(g, g.Player1)));
    var (s2, p2, g2) = F("rumble_rogue_106", 252, mates: new[] { "TRLA_188" });
    ClearExceptShrines(g2);
    var r2 = PlayDrawn(g2, "TRLA_188");
    Check("E39 with Bottled Terror (TRLA_106) the Rascal is also Stealthed (one of the shrine's other minions)", r2.IsImmune && r2.HasStealth);
    Mana(g2.Player1, 10);
    Play(g2, ToHand(g2, g2.Player1, "EX1_332"), r2); // Silence (its owner may target it)
    Check("E40s silenced, the Rascal loses Immune", !r2.IsImmune && r2[GameTag.IMMUNE] == 0);
});
Guard("E156", () =>
{
    var (s, plan, g) = F("rumble_shaman_109", 261, mates: new[] { "TRLA_156" });
    ClearExceptShrines(g); Mana(g.Player1, 10);
    var shrine = ShrineOf(g, g.Player1);
    Play(g, ToHand(g, g.Player1, "EX1_238"), g.Player2.Hero); // Lightning Bolt: Overload (1)
    int lure = shrine.AttackDamage;
    Check("E40 Lightning Bolt: 1 Overload owed; Krag'wa's Lure (TRLA_109) gives the shrine +1 Attack", g.Player1.OverloadOwed == 1 && lure == 1, $"{g.Player1.OverloadOwed} {lure}");
    var diver = PlayDrawn(g, "TRLA_156");
    Check("E41 TRLA_156 Deepsea Diver (2/2): the owed Overload is cleared (nothing locked), the Lure's Attack stays", diver.AttackDamage >= 2 && g.Player1.OverloadOwed == 0
        && g.Player1.OverloadLocked == 0 && shrine.AttackDamage == lure);
    EndTurn(g); EndTurn(g);
    Check("E42 the next turn no Mana Crystal is locked", g.Player1.OverloadLocked == 0 && g.Player1.RemainingMana == g.Player1.BaseMana);
    Mana(g.Player1, 5); g.Player1.OverloadLocked = 2;
    Check("E43 two Mana Crystals locked: 3 of 5 usable", g.Player1.RemainingMana == 3);
    var d2 = (Minion)ToHand(g, g.Player1, "TRLA_156"); Play(g, d2);
    Check("E44 a Diver (1) unlocks them: 4 of 5 usable", g.Player1.OverloadLocked == 0 && g.Player1.RemainingMana == 4, g.Player1.RemainingMana.ToString());
    var (s2, p2, g2) = F("rumble_shaman_154", 262, mates: new[] { "TRLA_156" });
    ClearExceptShrines(g2); Mana(g2.Player1, 10);
    Play(g2, ToHand(g2, g2.Player1, "EX1_238"), g2.Player2.Hero);
    PlayDrawn(g2, "TRLA_156");
    Check("E45 with Tribute from the Tides (TRLA_154, three Battlecries) the same unlock", g2.Player1.OverloadOwed == 0 && g2.Player1.OverloadLocked == 0);
});
Guard("E180", () =>
{
    var (s, plan, g) = F("rumble_warlock_178", 271, mates: new[] { "TRLA_180" });
    ClearExceptShrines(g); Mana(g.Player1, 10);
    var hero = g.Player1.Hero;
    Check("E46 before the Bat the hero is not Immune", !hero.IsImmune);
    var bat = PlayDrawn(g, "TRLA_180");
    Check("E47 TRLA_180 Leering Bat (3/3 Beast): the run player's hero is Immune on their turn (TRLA_180e)", bat.AttackDamage == 3 && hero.IsImmune && Count(hero, "TRLA_180e") == 1);
    int me = Heal(g.Player1), hand = g.Player1.HandZone.Count;
    Power(g);
    Check("E48 Life Tap: a card drawn, no Health lost", Heal(g.Player1) == me && g.Player1.HandZone.Count == hand + 1, $"{me} -> {Heal(g.Player1)}");
    EndTurn(g); Mana(g.Player2, 10);
    Check("E49 on the boss's turn the hero is not Immune", !hero.IsImmune && Count(hero, "TRLA_180e") == 0);
    me = Heal(g.Player1);
    Play(g, ToHand(g, g.Player2, "CS2_029"), hero);
    Check("E50 the boss's Fireball hits it for 6", Heal(g.Player1) == me - 6);
    EndTurn(g);
    Check("E51 the run player's next turn: Immune again", hero.IsImmune);
    Kill(g, bat);
    Check("E52 the Bat dead: no Immunity", !hero.IsImmune && Count(hero, "TRLA_180e") == 0);
    // Hir'eek's Hunger (TRLA_179): damage to the run player's hero on their turn is dealt to the boss instead; Immune means no
    // damage, so nothing is redirected.
    var (s2, p2, g2) = F("rumble_warlock_179", 272, mates: new[] { "TRLA_180" });
    ClearExceptShrines(g2);
    PlayDrawn(g2, "TRLA_180");
    me = Heal(g2.Player1); int boss = Heal(g2.Player2);
    Power(g2);
    Check("E53 with Hir'eek's Hunger and the Bat, Life Tap hurts nobody (no damage to redirect)", Heal(g2.Player1) == me && Heal(g2.Player2) == boss);
    // Blood Pact (TRLA_113): spells are paid with Health, which is a payment, not damage: Immune does not stop it.
    var (s3, p3, g3) = F("rumble_warlock_113", 273, mates: new[] { "TRLA_180" });
    ClearExceptShrines(g3);
    PlayDrawn(g3, "TRLA_180");
    me = Heal(g3.Player1); boss = Heal(g3.Player2);
    Play(g3, ToHand(g3, g3.Player1, "CS2_029"), g3.Player2.Hero);
    Check("E54 with Blood Pact and the Bat, Fireball still costs 4 Health (a payment, not damage) and deals 6", Heal(g3.Player1) == me - 4 && Heal(g3.Player2) == boss - 6,
        $"me {me}->{Heal(g3.Player1)} boss {boss}->{Heal(g3.Player2)}");
});
Guard("E172", () =>
{
    var (s, plan, g) = F("rumble_warrior_170", 281, mates: new[] { "TRLA_172" });
    ClearExceptShrines(g); Mana(g.Player1, 10);
    var shrine = ShrineOf(g, g.Player1);
    var quil = PlayDrawn(g, "TRLA_172");
    Check("E55 TRLA_172 Rallying Quilboar (2/4) beside the shrine: the shrine has Taunt (TRLA_172e), the Quilboar none", quil.ZonePosition == shrine.ZonePosition + 1
        && shrine.HasTaunt && Count(shrine, "TRLA_172e") == 1 && !quil.HasTaunt && quil.AttackDamage == 2 && quil.Health == 4);
    var yeti = (Minion)ToHand(g, g.Player1, "CS2_182"); Play(g, yeti);
    Check("E56 a Yeti on its other side has Taunt", yeti.HasTaunt && Count(yeti, "TRLA_172e") == 1);
    EndTurn(g); Mana(g.Player2, 10);
    var raider = Ready(Board(g, g.Player2, "CS2_182"));
    var targets = g.Player2.Options().OfType<MinionAttackTask>().Where(o => o.Source == raider).Select(o => o.Target).ToList();
    Check("E57 the boss's Yeti may attack only the two Taunts", targets.Count == 2 && targets.Contains(shrine) && targets.Contains(yeti), string.Join(",", targets.Select(t => t.Card.Id)));
    Kill(g, shrine);
    var form = ShrineOf(g, g.Player1);
    Check("E58 the shrine dormant: its form gets no Taunt, the Yeti keeps its own", ShrineRules.IsDormantForm(form) && !form.HasTaunt && Count(form, "TRLA_172e") == 0 && yeti.HasTaunt);
    var (s2, p2, g2) = F("rumble_warrior_104", 282, upgrades: new[] { "TRLA_802" }, mates: new[] { "TRLA_172" });
    ClearExceptShrines(g2);
    var sh2 = ShrineOf(g2, g2.Player1);
    var q2 = PlayDrawn(g2, "TRLA_172");
    Check("E59 with Favor of the Gods (TRLA_802) the Quilboar beside the shrine has Taunt too (TRLA_802e), the shrine has the Quilboar's", q2.HasTaunt && sh2.HasTaunt
        && Count(sh2, "TRLA_172e") == 1);
});

// ================================================================ U TRLA_809 Windfall Blessing
string[] DeckMix = { "CS2_182", "CS2_120", "CS2_029", "CS2_172", "CS2_106" }; // Yeti, Crocolisk, Fireball, Raptor, Fiery War Axe
string[] BigDeck = Enumerable.Repeat(DeckMix, 4).SelectMany(x => x).ToArray();
Guard("U809", () =>
{
    var (s, plan, g) = F("rumble_druid_115", 301, upgrades: new[] { Windfall });
    var p1 = g.Player1;
    var shrine = ShrineOf(g, p1);
    Check("U1 the plan carries TRLA_809; the run player holds TRLA_809e and the shrine its grant TRLA_809e2", plan.PlayerShrineUpgrades.Contains(Windfall)
        && p1.AppliedEnchantments.Any(e => e.Card.Id == "TRLA_809e") && Count(shrine, "TRLA_809e2") == 1);
    ClearExceptShrines(g);
    SetDeck(g, p1, DeckMix);
    SetDeck(g, g.Player2, "CS2_182", "CS2_182");
    var handYeti = (Minion)ToHand(g, p1, "CS2_182");
    var boardYeti = Board(g, p1, "CS2_182");
    var before = DeckStats(p1);
    Kill(g, shrine);
    var after = DeckStats(p1);
    Check("U2 the shrine goes dormant: every minion in the run player's deck has +1/+1 (TRLA_809e3)", before.Count == 3 && before.All(kv => after[kv.Key] == (kv.Value.A + 1, kv.Value.H + 1))
        && p1.DeckZone.OfType<Minion>().All(m => Count(m, "TRLA_809e3") == 1), string.Join(" ", after.Values));
    Check("U3 the spell and the weapon in the deck, the minion in hand and the one in play are unchanged; the boss's deck too",
        p1.DeckZone.Where(p => !(p is Minion)).All(p => Count(p, "TRLA_809e3") == 0) && Count(handYeti, "TRLA_809e3") == 0 && Count(boardYeti, "TRLA_809e3") == 0
        && handYeti.AttackDamage == 4 && g.Player2.DeckZone.All(p => Count(p, "TRLA_809e3") == 0 && p.Card.ATK == ((Minion)p).AttackDamage));
    var drawn = (Minion)Generic.Draw(p1, p1.DeckZone.First(p => p.Card.Id == "CS2_182"));
    Mana(p1, 10); Play(g, drawn);
    Check("U4 a buffed Yeti drawn and played keeps +1/+1 (5/6)", drawn.AttackDamage == 5 && drawn.Health == 6 && p1.BoardZone.Contains(drawn));
    // Revival: the fresh shrine has the grant again; its next death buffs again.
    var (sR, pR, gR) = F("rumble_druid_116", 302, upgrades: new[] { Windfall });
    var pr = gR.Player1;
    SetDeck(gR, pr, BigDeck);
    Kill(gR, ShrineOf(gR, pr));
    for (int i = 0; i < 6 && !ShrineRules.IsAwake(ShrineOf(gR, pr)); i++) EndTurn(gR);
    var revived = ShrineOf(gR, pr);
    Check("U5 the revived shrine (three owner's turns later) has the grant back", ShrineRules.IsAwake(revived) && Count(revived, "TRLA_809e2") == 1, gR.Turn.ToString());
    var once = pr.DeckZone.OfType<Minion>().Where(m => Count(m, "TRLA_809e3") == 1).ToList();
    var stats = once.ToDictionary(m => m.Id, m => (m.AttackDamage, m.Health));
    Kill(gR, revived);
    Check("U6 its second death: every minion still in the deck has +2/+2 in all", once.Count >= 3 && once.All(m => Count(m, "TRLA_809e3") == 2
        && m.AttackDamage == stats[m.Id].AttackDamage + 1 && m.Health == stats[m.Id].Health + 1 && m.AttackDamage == m.Card.ATK + 2), once.Count.ToString());
    Check("U7 the run deck records are unchanged (no mods written)", s.Cards.All(c => c.Mods.Count == 0) && s.Deck.SequenceEqual(s.Cards.Select(c => c.Id)));
});
Guard("U809-combos", () =>
{
    // Two shrines (TRLA_804): each carries the grant; both dying is +2/+2.
    var (s, plan, g) = F("rumble_hunter_162", 311, upgrades: new[] { "TRLA_804", Windfall });
    var p1 = g.Player1;
    SetDeck(g, p1, DeckMix);
    var shrines = ShrineRules.Of(p1).ToList();
    Check("U8 TRLA_804: two shrines, each with TRLA_809e2", shrines.Count == 2 && shrines.All(x => Count(x, "TRLA_809e2") == 1));
    foreach (var x in shrines) Kill(g, x);
    Check("U9 both dormant: the deck's Yeti is 6/7", p1.DeckZone.OfType<Minion>().First(m => m.Card.Id == "CS2_182") is Minion y && y.AttackDamage == 6 && y.Health == 7);
    // Totem of the Dead (LOOTA_845): the granted Deathrattle twice.
    var (s2, p2, g2) = F("rumble_mage_108", 312, upgrades: new[] { Windfall }, treasures: new[] { "LOOTA_845" });
    SetDeck(g2, g2.Player1, DeckMix);
    Kill(g2, ShrineOf(g2, g2.Player1));
    var y2 = g2.Player1.DeckZone.OfType<Minion>().First(m => m.Card.Id == "CS2_182");
    Check("U10 with Totem of the Dead: +2/+2 (6/7)", y2.AttackDamage == 6 && y2.Health == 7, $"{y2.AttackDamage}/{y2.Health}");
    // Bwonsamdi's Sanctum (TRLA_114: Deathrattles two extra times), the shrine's own Deathrattles included.
    var (s3, p3, g3) = F("rumble_priest_114", 313, upgrades: new[] { Windfall });
    SetDeck(g3, g3.Player1, DeckMix);
    Kill(g3, ShrineOf(g3, g3.Player1));
    var y3 = g3.Player1.DeckZone.OfType<Minion>().First(m => m.Card.Id == "CS2_182");
    Console.WriteLine($"INFO U11 a lone Sanctum's own death: the deck's Yeti {y3.AttackDamage}/{y3.Health}");
    Check("U11 a lone Bwonsamdi's Sanctum dying: its aura leaves with it (the Sanctum dormant: Deathrattles trigger once), so +1/+1 (5/6)",
        y3.AttackDamage == 5 && y3.Health == 6, $"{y3.AttackDamage}/{y3.Health}");
    var (s3b, p3b, g3b) = F("rumble_priest_114", 318, upgrades: new[] { "TRLA_804", Windfall });
    SetDeck(g3b, g3b.Player1, DeckMix);
    Kill(g3b, ShrineOf(g3b, g3b.Player1));
    var y3b = g3b.Player1.DeckZone.OfType<Minion>().First(m => m.Card.Id == "CS2_182");
    Check("U11b two Sanctums (TRLA_804): one dies while the other stands: three times (+3/+3, 7/8)", y3b.AttackDamage == 7 && y3b.Health == 8, $"{y3b.AttackDamage}/{y3b.Health}");
    // Deck-wide treasures: Sow the Seeds (DALA_727, +3/+3), Blade of the Burning Sun (ULDA_044, +2 Attack), Upgraded Pack Mule
    // (ULDA_024, Costs (1) less) stack with it.
    var (s4, p4, g4) = F("rumble_warrior_104", 314, upgrades: new[] { Windfall });
    var c4 = g4.Player1;
    SetDeck(g4, c4, DeckMix); Mana(c4, 10); ClearExceptShrines(g4);
    Play(g4, ToHand(g4, c4, "DALA_727")); Mana(c4, 10);
    Play(g4, ToHand(g4, c4, "ULDA_024")); Mana(c4, 10);
    Play(g4, ToHand(g4, c4, "ULDA_044")); Mana(c4, 10);
    var y4 = c4.DeckZone.OfType<Minion>().First(m => m.Card.Id == "CS2_182");
    int cost4 = y4.Cost;
    Kill(g4, ShrineOf(g4, c4));
    Check("U12 Sow the Seeds, Blade of the Burning Sun, Pack Mule, then the shrine's death: the Yeti is 10/9 at (3)", y4.AttackDamage == 4 + 3 + 2 + 1 && y4.Health == 5 + 3 + 1
        && y4.Cost == 3 && cost4 == 3, $"{y4.AttackDamage}/{y4.Health} ({y4.Cost})");
    // Silence: the grant is gone until the shrine revives; its death buffs nothing.
    var (s5, p5, g5) = F("rumble_paladin_105", 315, upgrades: new[] { Windfall });
    var c5 = g5.Player1;
    SetDeck(g5, c5, DeckMix); Mana(c5, 10);
    var sh5 = ShrineOf(g5, c5);
    Play(g5, ToHand(g5, c5, "EX1_332"), sh5);
    Kill(g5, sh5);
    Check("U13 a silenced shrine dies: no grant, no buff", c5.DeckZone.All(p => Count(p, "TRLA_809e3") == 0));
    // A copy (the boss's Faceless Manipulator) is a plain minion: its copied grant does nothing for anyone.
    var (s6, p6, g6) = F("rumble_paladin_105", 316, upgrades: new[] { Windfall });
    SetDeck(g6, g6.Player1, DeckMix); SetDeck(g6, g6.Player2, "CS2_182");
    EndTurn(g6); Mana(g6.Player2, 10);
    Play(g6, ToHand(g6, g6.Player2, "EX1_564"), ShrineOf(g6, g6.Player1));
    var copy = g6.Player2.BoardZone.FirstOrDefault(m => m.Card.Id == "TRLA_105");
    if (copy != null) Kill(g6, copy);
    Check("U14 the boss's copy of the shrine dies for good: neither deck changes", copy != null && !ShrineRules.IsMarked(copy)
        && g6.Player1.DeckZone.Concat(g6.Player2.DeckZone).All(p => Count(p, "TRLA_809e3") == 0));
    // The boss's TRLA_107 fixture (no upgrade) dies: nothing.
    var (s7, p7, g7) = F("rumble_paladin_137", 317, upgrades: new[] { Windfall });
    SetDeck(g7, g7.Player2, "CS2_182");
    var fx = Fixture(g7);
    Kill(g7, fx);
    Check("U15 the boss's fixture shrine dies: the boss's deck gets nothing (the boss holds no upgrade)", g7.Player2.DeckZone.All(p => Count(p, "TRLA_809e3") == 0)
        && ShrineRules.IsDormantForm(ShrineOf(g7, g7.Player2)));
});

// ================================================================ Q the drafts through the run flow
DungeonRunState Walk(DungeonRunState s, Func<DungeonRunState, bool> stop, List<string> log, Func<DungeonRunState, string> choose = null)
{
    var scratch = new DungeonRunProfile();
    for (int guard = 0; guard < 500 && !stop(s) && !s.Finished; guard++)
    {
        switch (s.Phase)
        {
            case DungeonRunPhase.PreMatch: run.BeginMatch(s); run.RecordOutcome(s, scratch, DungeonRunOutcome.Win); break;
            case DungeonRunPhase.TreasureDraft:
                log?.Add($"f{s.Floor}:{s.OpenDraft.Kind}[{string.Join(",", s.OpenDraft.Offered)}]");
                run.PickTreasure(s, choose?.Invoke(s) ?? s.OpenDraft.Offered[0]); break;
            case DungeonRunPhase.Modifier: run.PickModifier(s, s.ModifierOffer.Offered[0]); break;
            case DungeonRunPhase.Tavern: run.LeaveTavern(s); break;
            case DungeonRunPhase.BundleDraft:
                if (s.OpenDraft.Takes == 0) { run.PickBundle(s, s.OpenDraft.Offered[0]); break; }
                string bundle = s.OpenDraft.Choosing ?? s.OpenDraft.Offered[0];
                var left = run.OfferCards(s.OpenDraft, bundle);
                foreach (string kept in s.OpenDraft.Chosen) left.Remove(kept);
                run.PickDraftCard(s, bundle, left[0]);
                break;
            default: throw new Exception("walk stopped at " + s.Phase);
        }
    }
    return s;
}
foreach (var (cls, row, deck, id) in companions)
    Guard("Q-" + cls, () =>
    {
        DungeonRunState s = null; List<string> log = null; long used = 0;
        for (long seed = 650; seed < 700 && s == null; seed++)
        {
            var t = NewRun(row, data.Class(row).Decks[1], seed);
            var l = new List<string>();
            Walk(t, x => x.Floor > 13, l, x => x.OpenDraft.Kind == DungeonRun.TeammateKind && x.OpenDraft.Offered.Contains(id) ? id : null);
            if (t.Cards.Any(c => c.Id == id)) { s = t; log = l; used = seed; }
        }
        Check($"Q1 {cls}: a teammate draft offers the companion {id} (a seed of 650-699) and the pick joins the deck as a teammate record", s != null
            && s.Cards.Count(c => c.Id == id && c.Origin == DungeonRunDeck.OriginTeammate) == 1, used.ToString());
        if (s == null) return;
        var offers = log.Where(l => l.Contains(":teammate[")).ToList();
        var sets = offers.Select(l => l.Substring(l.IndexOf('[') + 1).TrimEnd(']').Split(',').ToList()).ToList();
        var taken = s.Cards.Where(c => c.Origin == DungeonRunDeck.OriginTeammate).Select(c => c.Id).ToList();
        Check($"Q2 {cls}: four teammate drafts (I.2, I.4, II.3, III.3) of 3 from its six, never one already taken", sets.Count == 4 && sets.All(x => x.Count == 3)
            && sets.Select((x, i) => x.All(m => !taken.Take(i).Contains(m))).All(b => b) && sets.All(x => x.All(data.Teammates[cls].Contains)) && taken.Distinct().Count() == 4,
            string.Join(" ", offers));
        var plan = run.BuildPlan(s);
        var g = Start(plan, run.MatchSeed(s));
        Check($"Q3 {cls}: the next match does not deal {id} in the opening hand", InHand(g, id) == null);
        Finish(g);
        var card = InHand(g, id);
        Check($"Q4 {cls}: {id} is drawn at the start of the match", card != null && !g.Player1.DeckZone.Any(p => p.Card.Id == id));
        Mana(g.Player1, 10);
        while (g.Player1.BoardZone.IsFull) { var m = g.Player1.BoardZone.Last(x => !ShrineRules.IsMarked(x)); g.Player1.BoardZone.Remove(m); g.Player1.SetasideZone.Add(m); }
        bool played = card != null && Play(g, card);
        Check($"Q5 {cls}: it plays from the hand onto the board", played && card.Zone == g.Player1.BoardZone, card?.Zone?.Type.ToString());
    });
Guard("Q-809", () =>
{
    DungeonRunState s = null; long used = 0;
    for (long seed = 700; seed < 760 && s == null; seed++)
    {
        var t = NewRun("TRLA_209h_Warrior", "rumble_warrior_171", seed);
        Walk(t, x => x.Floor > 6, null, x => x.OpenDraft.Kind == DungeonRun.ShrineUpgradeKind && x.OpenDraft.Offered.Contains(Windfall) ? Windfall : null);
        if (t.ShrineUpgrades.Contains(Windfall)) { s = t; used = seed; }
    }
    Check("Q6 a shrine-upgrade draft offers TRLA_809 (a seed of 700-759) and the pick joins the run's shrine upgrades (not its treasures)", s != null
        && s.ShrineUpgrades.Contains(Windfall) && !s.Treasures.Contains(Windfall), used.ToString());
    if (s == null) return;
    var plan = run.BuildPlan(s);
    var g = Start(plan, run.MatchSeed(s));
    Finish(g);
    Check("Q7 the next match: the run shrine carries Windfall Glyph", plan.PlayerShrineUpgrades.Contains(Windfall) && ShrineRules.Of(g.Player1).All(x => Count(x, "TRLA_809e2") == 1));
});

// ================================================================ X full games against the real boss AI
var testBudget = new Budget(6000, 2000, 300, 6, 3, 100, 8, turnSearchSeconds: 6, turnWallSeconds: 8);
string AiGame(string deck, string id, long seed, string[] upgrades)
{
    var s = Prepared(deck, seed, upgrades: upgrades, mates: new[] { id });
    var boss = run.CurrentBoss(s);
    var plan = run.BuildPlan(s);
    var pilot = DungeonRunBossPilot.Build(boss, plan, data, runClasses: run.RunClasses(s));
    var profile = pilot.Profile.WithBudget(testBudget);
    var golden = new GoldenPilot(profile, pilot.Plan, null, pilot.SecretPrior);
    var cfg = new GameConfig { RandomSeed = run.MatchSeed(s), Shuffle = true, SkipMulligan = false, History = false, Logging = false, FormatType = FormatType.FT_WILD };
    DungeonRunSetup.Configure(cfg, plan);
    var game = new Game(cfg);
    game.StartGame();
    DungeonRunSetup.ApplyAfterStart(game, plan);
    game.Process(ChooseTask.Mulligan(game.Player1, new List<int>()));
    var choice = game.Player2.Choice;
    var view = new MulliganView(choice.Choices.Select(x => new MulliganCard(x, game.IdEntityDic[x].Card.Id, game.IdEntityDic[x].Card.Cost)),
        false, pilot.Deck.CardIds, game.Player1.Hero.Card.Class);
    game.Process(ChooseTask.Mulligan(game.Player2, BotDriver.Mulligan(view, new DeckPlanMulligan(pilot.Plan)).ToList()));
    game.MainBegin();
    var p1 = game.Player1;
    var mate = p1.HandZone.FirstOrDefault(p => p.Card.Id == id) ?? throw new Exception(id + " not drawn at the start of the game");
    int cardId = mate.Id;
    int decisionIndex = 0, actions = 0, decisions = 0, fallbacks = 0;
    bool played = false;
    void Act(PlayerTask task, string who)
    {
        if (++actions > 1500) throw new Exception("stuck after 1500 actions");
        if (task is ChooseTask pick && game.Step != Step.BEGIN_MULLIGAN)
        {
            var decision = game.GetPendingDecisions().Single(dd => dd.ControllerId == task.Controller.Id);
            if (!game.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(decision.Id, decision.ControllerId, pick.Choices.ToArray()), out var why))
                throw new Exception($"engine rejected {who} choice ({why})");
        }
        else if (!game.Process(task)) throw new Exception($"engine rejected {who} task {task}");
    }
    int playerTurn = -1, turnActions = 0;
    while (game.State != State.COMPLETE && game.Turn <= 60)
    {
        if (game.CurrentPlayer == game.Player2 && game.Player1.Choice == null)
        {
            var (task, trace) = BotDriver.Decide(game, game.Player2.PlayerId, pilot.Deck.Style,
                st => golden.DecidePilot(st, new GoldenContext(game.Turn, decisionIndex, unchecked(997 * game.Turn + decisionIndex * 31), profile)),
                ref decisionIndex, golden.Options);
            decisions++;
            if (trace?.Kind == "fallback") fallbacks++;
            Act(task, "boss");
        }
        else
        {
            if (game.Turn != playerTurn) { playerTurn = game.Turn; turnActions = 0; }
            var options = game.Player1.Options();
            var mine = options.OfType<PlayCardTask>().Where(o => o.Source?.Id == cardId).OrderBy(o => o.Target == null ? 0 : o.Target.Controller == game.Player1 ? 1 : 0).FirstOrDefault();
            if (mine != null) { played = true; Act(mine, "player"); }
            else if (++turnActions > 60) Act(EndTurnTask.Any(game.Player1), "player");
            else Act(LocalBot.Choose(options), "player");
        }
    }
    string result = game.State != State.COMPLETE ? "turn-cap" : game.Player2.PlayState == PlayState.WON ? "boss" : game.Player1.PlayState == PlayState.WON ? "player" : "tie";
    int deathsOfShrine = game.Player1.GraveyardZone.Count(ShrineRules.IsCorpse);
    return $"{result} turns {game.Turn} played {(played ? 1 : 0)} decisions {decisions} fallbacks {fallbacks} shrine-deaths {deathsOfShrine} boss {boss.Id}";
}
foreach (var (cls, row, deck, id) in companions)
{
    string line;
    var ups = new[] { Windfall, cls == "DRUID" || cls == "ROGUE" ? "TRLA_804" : "TRLA_802" };
    try { line = AiGame(deck, id, 881 + Array.FindIndex(companions, c => c.Id == id), ups); }
    catch (Exception e) { line = "FAULT " + e.GetType().Name + ": " + e.Message.Split('\n')[0]; }
    Console.WriteLine($"X1 {id} {deck,-20} {line}");
    Check($"X1 {id} ({cls}, with TRLA_809): a full game against the boss AI with the companion played, no fault, no AI fallback",
        !line.StartsWith("FAULT") && line.Contains("fallbacks 0 ") && line.Contains("played 1 "), line);
}

Console.WriteLine($"SUMMARY checks passed {passes} failed {failures}");
return failures == 0 ? 0 : 1;
