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

// Phase 8 family 13, the Rumble Run opponents and boss shrines. Real engine games through the run setup path (DungeonRun.BuildPlan
// on the shipped row, with the seed that rolls the variant under test -> DungeonRunSetup.Configure / ApplyAfterStart -> both
// mulligans -> MainBegin), the boss being player 2:
//   A  the rows (the eight champions beside War Master Voone), their variants, decks and plans, the admission rows, the validator
//   V  the variant roll: every variant of every champion is drawn by the run seed and the floor
//   Z  Zul'jin's Steady Throw (TRLA_065p)
//   S  each of the 24 shrines this family lifts, on the boss's side (S105 ... S187): what it does for the boss, and that it does
//      nothing for the run player; TRLA_110 with the boss's generated Secrets, TRLA_186's steal with the boss's fair view, TRLA_146
//      and TRLA_178 through the boss's generation filter, TRLA_137/147/179 against the run player's hero
//   L  every boss shrine lands on the boss's board, goes dormant when it dies, revives on the boss's third turn after, never
//      changes sides and is never transformed; the boss's fair view sees it
//   X  shrines on both sides at once (two Vengeances, two Treasures from Below, Hunger against Vengeance)
//   F  the boss's fair view while a chained Discover waits (Griftah in Malacrass's early deck; plugin/FairBotState.cs guard)
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

const string Yeti = "CS2_182", Croc = "CS2_120", Raptor = "CS2_172", Wisp = "CS2_231", Golem = "CS2_186", Moonfire = "CS2_008", Fireball = "CS2_029";
const string Frostbolt = "CS2_024", ArcaneShot = "DS1_185", Kings = "CS2_092", Bolt = "EX1_238", Leper = "EX1_029", Archer = "CS2_189", Soulfire = "EX1_308";
const string HealingTouch = "CS2_007", Polymorph = "CS2_022", MindControl = "CS1_113", Smite = "CS1_130";
string[] Thirty(string id) => Enumerable.Repeat(id, 30).ToArray();

// The eight rows of this family, then Voone (family 01's row, filed under 13): id, hero, power, class, tier, round, the shrines
// of the early / middle / late variants (challenges 1-2, 3-5, 6-8), the extra flags.
var rows = new (string id, string hero, string power, CardClass cls, string tier, int round, string[] shrines, string[] flags)[]
{
    ("trl_201_zentimo", "TRLA_201h", "CS2_049", CardClass.SHAMAN, "1", 1, new[] { "TRLA_155", "TRLA_109", "TRLA_154" }, new[] { "doubleBattlecry" }),
    ("trl_202_hooktusk", "TRLA_202h", "CS2_083b", CardClass.ROGUE, "1", 2, new[] { "TRLA_106", "TRLA_186", "TRLA_187" }, new[] { "hiddenZone", "recast" }),
    ("trl_203_thekal", "TRLA_203h", "CS2_101", CardClass.PALADIN, "2", 4, new[] { "TRLA_138", "TRLA_137", "TRLA_105" }, new[] { "recast" }),
    ("trl_204_zuljin", "TRLA_204h", "TRLA_065p", CardClass.HUNTER, "3", 5, new[] { "TRLA_163", "TRLA_162", "TRLA_110" }, new[] { "secrets" }),
    ("trl_205_loti", "TRLA_205h", "CS2_017", CardClass.DRUID, "3", 6, new[] { "TRLA_116", "TRLA_115", "TRLA_112" }, new string[0]),
    ("trl_206_jeklik", "TRLA_206h", "CS2_056", CardClass.WARLOCK, "4", 7, new[] { "TRLA_113", "TRLA_178", "TRLA_179" }, new string[0]),
    ("trl_207_malacrass", "TRLA_207h", "HERO_08bp", CardClass.MAGE, "4", 8, new[] { "TRLA_128", "TRLA_108", "TRLA_129" }, new string[0]),
    ("trl_208_talanji", "TRLA_208h", "CS1h_001", CardClass.PRIEST, "4", 8, new[] { "TRLA_146", "TRLA_147", "TRLA_114" }, new[] { "doubleDeathrattle" }),
};
string[] variantIds = { "early", "middle", "late" };
string RowOf(string shrine) => rows.First(r => r.shrines.Contains(shrine)).id;

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
// The first run seed (from 4400) whose fight rolls the variant naming that shrine, and its plan through the run layer.
var seedOf = new Dictionary<string, long>(StringComparer.Ordinal);
(DungeonRunState s, RunMatchPlan plan) VariantPlan(string shrine)
{
    string bossId = RowOf(shrine);
    for (long seed = 4400; seed < 4800; seed++)
    {
        var s = At("MAGE", seed, bossId);
        if (run.CurrentVariant(s).Shrine != shrine) continue;
        seedOf[shrine] = seed;
        return (s, run.BuildPlan(s, all));
    }
    throw new Exception("no seed rolls " + shrine);
}
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
// The champion's plan with the variant of that shrine (hero, power, Health, shrine, generated Secrets: the row's); the decks are
// replaced by plain cards so a test controls what is drawn. A run player's shrine may be added (both sides at once).
Game Fight(string shrine, string bossDeck = Yeti, string playerDeck = Yeti, string playerShrine = null, int seed = 7)
{
    var (s, plan) = VariantPlan(shrine);
    if (plan.BossShrine != shrine) throw new Exception("plan shrine " + plan.BossShrine + " for " + shrine);
    plan.BossDeck = Thirty(bossDeck).Select(C).ToList();
    plan.PlayerDeck = Thirty(playerDeck).Select(C).ToList();
    plan.PlayerDeckSlots = null;
    if (playerShrine != null) plan.PlayerShrine = playerShrine;
    return Ready(plan, seed);
}
void Mana(Controller c, int n = 10) { c.BaseMana = n; c.UsedMana = 0; c.OverloadLocked = 0; c.OverloadOwed = 0; }
IPlayable ToHand(Game g, Controller c, string id)
{
    while (c.HandZone.Count >= 9) { var old = c.HandZone[0]; c.HandZone.Remove(old); c.SetasideZone.Add(old); }
    var e = Entity.FromCard(c, C(id)); Generic.AddHandPhase.Invoke(c, e); g.DeathProcessingAndAuraUpdate(); return e;
}
void ClearHand(Game g, Controller c) { foreach (var p in c.HandZone.ToArray()) { c.HandZone.Remove(p); c.SetasideZone.Add(p); } g.DeathProcessingAndAuraUpdate(); }
void FillHand(Controller c) { while (!c.HandZone.IsFull) Generic.AddHandPhase.Invoke(c, Entity.FromCard(c, C(Yeti))); }
void EmptyDeck(Controller c) { foreach (var p in c.DeckZone.ToArray()) c.SetasideZone.Add(c.DeckZone.Remove(p)); }
Minion Board(Game g, Controller c, string id) { var m = (Minion)Entity.FromCard(c, C(id)); Generic.SummonBlock(g, m, -1, c.Hero); g.DeathProcessingAndAuraUpdate(); return m; }
void ClearBoards(Game g)
{
    foreach (var c in new[] { g.Player1, g.Player2 })
        foreach (var m in c.BoardZone.Slots.OfType<Minion>().Where(m => !ShrineRules.IsMarked(m)).ToArray()) { c.BoardZone.Remove(m); c.SetasideZone.Add(m); }
    g.DeathProcessingAndAuraUpdate();
}
bool Play(Game g, IPlayable card, ICharacter target = null) => g.Process(PlayCardTask.Any(g.CurrentPlayer, card, target, -1, 0));
bool Cast(Game g, string id, ICharacter target = null) { Mana(g.CurrentPlayer); return Play(g, ToHand(g, g.CurrentPlayer, id), target); }
void EndTurn(Game g) => g.Process(EndTurnTask.Any(g.CurrentPlayer));
bool Power(Game g, ICharacter target = null) => g.Process(HeroPowerTask.Any(g.CurrentPlayer, target));
void ToBoss(Game g) { if (g.CurrentPlayer == g.Player1) EndTurn(g); Mana(g.Player2); }
void ToPlayer(Game g) { if (g.CurrentPlayer == g.Player2) EndTurn(g); Mana(g.Player1); }
void Kill(Game g, Minion m) { m.Destroy(); g.DeathProcessingAndAuraUpdate(); }
void Damage(Game g, ICharacter m, int n) { Generic.DamageCharFunc(m.Controller.Opponent.Hero, m, n, false); g.DeathProcessingAndAuraUpdate(); }
Minion ReadyM(Minion m) { m.IsExhausted = false; return m; }
bool Attack(Game g, Minion a, ICharacter t) => g.Process(MinionAttackTask.Any(g.CurrentPlayer, ReadyM(a), t));
bool Has(IPlayable p, string enchantmentId) => p.AppliedEnchantments?.Any(e => e.Card.Id == enchantmentId) == true;
Minion BossShrine(Game g) => ShrineRules.Of(g.Player2).FirstOrDefault();
int Hp(Controller c) => c.Hero.Health;
(Game view, Controller boss, Controller player, FairBotState bot) FairView(Game g, FairOptions options = null)
{
    var bot = FairBotState.Create(g, g.Player2.PlayerId, 1, DeckStyle.Tempo, options);
    var view = bot.FingerprintGame;
    var viewBoss = view.Player1.PlayerId == g.Player2.PlayerId ? view.Player1 : view.Player2;
    return (view, viewBoss, viewBoss.Opponent, bot);
}

// ============================================================ A  the rows, the admission rows, the validator
Guard("A", () =>
{
    var errors = DungeonRunValidator.Validate(data, null);
    Check("A1 the shipped data validates with the nine Rumble Run rows", errors.Count == 0, string.Join(" | ", errors.Take(4)));
    var family = data.Encounters.Where(b => b.Family == "13").Select(b => b.Id).ToList();
    Check("A2 family 13 holds War Master Voone (family 01's row) and the eight champions, in this order", family.SequenceEqual(new[] { "trl_200_voone" }.Concat(rows.Select(r => r.id))),
        string.Join(",", family));
    foreach (var r in rows)
    {
        var b = data.Boss(r.id);
        bool variantsOk = b.Variants.Select(v => v.Id).SequenceEqual(variantIds) && b.Variants.Select(v => v.Shrine).SequenceEqual(r.shrines)
            && b.Variants.All(v => data.BossDecks[v.Deck].Count == 30 && v.Deck == "enc-" + r.id + "-" + v.Id) && b.Deck == b.Variants[2].Deck;
        Check("A3 " + r.id + ": a Rumble Run row (TRL, tier " + r.tier + ", Rumble round " + r.round + ") with hero " + r.hero + " and power " + r.power
            + "; its three variants are the three " + r.cls + " shrines, each with its own 30-card deck; flags shrine," + string.Join(",", r.flags),
            b.Provenance == "TRL" && b.HeroCard == r.hero && b.Power == r.power && b.Tier == r.tier && b.LevelOriginal == r.round && b.LevelSource == "rumble-round" && C(r.hero).Class == r.cls
            && C(r.hero).Name == b.Name && C(r.hero).Type == CardType.HERO && !b.FixedHealth && b.Health == null && b.Forms.Count == 0 && b.Start == null
            && b.Pools.SequenceEqual(new[] { "enc-t" + r.tier }) && variantsOk && r.shrines.All(id => C(id).Class == r.cls)
            && b.Flags.OrderBy(f => f, StringComparer.Ordinal).SequenceEqual(new[] { "shrine" }.Concat(r.flags).OrderBy(f => f, StringComparer.Ordinal))
            && b.BossGeneratedSecrets == (r.id == "trl_204_zuljin") && b.Source.Url.StartsWith("https://hearthstone.wiki.gg/wiki/") && C(r.hero)[(GameTag)380] == C(r.power).AssetId,
            $"tier {b.Tier} flags {string.Join(",", b.Flags)} variants {string.Join(",", b.Variants.Select(v => v.Id + ":" + v.Shrine))}");
    }
    var shrinesOfRows = data.Encounters.Where(b => b.Family == "13").SelectMany(b => b.Variants.Select(v => v.Shrine)).OrderBy(x => x, StringComparer.Ordinal).ToList();
    Check("A4 the nine rows name each of the 27 scripted shrines exactly once (TRLA_107, the fixture, never)",
        shrinesOfRows.SequenceEqual(ShrineRules.Scripted.Where(id => id != DungeonRunShrines.FixtureShrine)) && shrinesOfRows.Count == 27, string.Join(",", shrinesOfRows));
    AdmissionVerdict V(string id, AdmitScope scope, bool catalog = true) => DungeonRunAdmission.Classify(null, id, scope, null, catalog);
    var gen = new HashSet<string>(DungeonRunAdmission.GeneratedRejects(true));
    var lifted = rows.SelectMany(r => r.shrines).ToList();
    var wrong = lifted.Where(id => !(V(id, AdmitScope.Player).Admitted && V(id, AdmitScope.Boss).Admitted && !V(id, AdmitScope.Generated).Admitted && gen.Contains(id)
        && V(id, AdmitScope.Player).Rule.NeverGenerated && V(id, AdmitScope.Boss).Rule.Ids.Contains(id)
        && V(id, AdmitScope.Boss).Family == (id == "TRLA_186" ? "boss-hidden-steal" : "boss-shrine") && V(id, AdmitScope.Boss).Reason.Contains("c-enc-13"))).ToList();
    Check("A5 the 24 shrines of the eight rows: admitted for the run player and a boss on a catalog run (boss-shrine; TRLA_186 boss-hidden-steal), never generated, each row naming c-enc-13",
        wrong.Count == 0 && lifted.Count == 24, string.Join(",", wrong));
    var trap = V("TRLA_110", AdmitScope.Boss);
    Check("A6 TRLA_110 needs the boss's generated Secrets (Hunter) and is a boss's on a catalog run only; the run player's on any run",
        trap.Admitted && trap.CatalogLifted && trap.Rule.BossNeeds == DungeonRunAdmission.BossNeedsGeneratedSecrets && trap.Rule.BossSecretClass == CardClass.HUNTER
        && !V("TRLA_110", AdmitScope.Boss, false).Admitted && V("TRLA_110", AdmitScope.Player, false).Admitted
        && lifted.Where(id => id != "TRLA_110").All(id => DungeonRunAdmission.RuleFor(id).BossNeeds == null));
    var ff = V(DungeonRunShrines.Frostfire, AdmitScope.Boss);
    Check("A7 TRLA_129s Frostfire is admitted for a boss and the run player (boss-shrine-token), never generated; Malacrass's row lists it",
        ff.Admitted && ff.Family == "boss-shrine-token" && V(DungeonRunShrines.Frostfire, AdmitScope.Player).Admitted && gen.Contains(DungeonRunShrines.Frostfire)
        && data.Boss("trl_207_malacrass").Cards.SequenceEqual(new[] { DungeonRunShrines.Frostfire }) && rows.Where(r => r.id != "trl_207_malacrass").All(r => data.Boss(r.id).Cards.Count == 0));
    var throwV = V(DungeonRunEncounters.SteadyThrow, AdmitScope.Boss);
    Check("A8 TRLA_065p Steady Throw is a boss's only (boss-power-public), never generated, never the run player's",
        throwV.Admitted && throwV.Family == "boss-power-public" && !V(DungeonRunEncounters.SteadyThrow, AdmitScope.Player).Admitted && gen.Contains(DungeonRunEncounters.SteadyThrow));
    Check("A9 TRLA_107 Tribal Shrine stays the test fixture: scripted, admitted for no side, in no row", ShrineRules.IsScripted("TRLA_107") && !V("TRLA_107", AdmitScope.Boss).Admitted
        && !V("TRLA_107", AdmitScope.Player).Admitted && !data.Bosses.Any(b => b.Variants.Any(v => v.Shrine == "TRLA_107")));
    DungeonRunData Changed(Action<DungeonRunData> change) { var d = Load(); change(d); return d; }
    var noSecrets = DungeonRunValidator.Validate(Changed(d => d.Boss("trl_204_zuljin").BossGeneratedSecrets = false), null);
    Check("A10 Zul'jin's row without the boss's generated Secrets is refused: Halazzi's Trap (and Streetsmuggler) would do nothing",
        noSecrets.Any(e => e.StartsWith("TRLA_110") && e.Contains("would do nothing")), string.Join(" | ", noSecrets.Take(2)));
    var fixture = DungeonRunValidator.Validate(Changed(d => d.Boss("trl_205_loti").Variants[0].Shrine = "TRLA_107"), null);
    Check("A11 a variant naming the fixture shrine is refused (no boss admission)", fixture.Any(e => e.Contains("admission board-shrine-form")), string.Join(" | ", fixture.Take(2)));
    // Deck changes: a listed card is left out only when the engine does not script it or the table keeps it from a boss; each is recorded.
    // The variant decks' records are in the generated file (the parser keeps the row deck's only).
    var subs = new List<(string Listed, string Reason)>();
    using (var enc = System.Text.Json.JsonDocument.Parse(R("dungeon-run/encounters-v1.json")))
        foreach (var e in enc.RootElement.GetProperty("encounters").EnumerateArray().Where(e => rows.Any(r => r.id == e.GetProperty("id").GetString())))
            foreach (var v in e.GetProperty("variants").EnumerateArray())
                if (v.GetProperty("deck").TryGetProperty("substitutions", out var list))
                    foreach (var x in list.EnumerateArray())
                        if (x.TryGetProperty("listed", out var listed)) subs.Add((listed.GetString(), x.GetProperty("reason").GetString()));
    var badSubs = subs.Where(x => x.Reason == "unscripted" ? C(x.Listed).Implemented
        : x.Reason.StartsWith("admission:") ? DungeonRunAdmission.Classify(C(x.Listed), x.Listed, AdmitScope.Boss, null, catalogRun: true).Admitted : x.Reason != "over-size").Select(x => x.Listed + ":" + x.Reason).ToList();
    var teammates = subs.Count(x => x.Reason == "admission:teammate");
    Check("A12 every listed card left out of a champion's deck is unscripted or not admitted for a boss (the Rumble Run teammates the champions play), each recorded",
        badSubs.Count == 0 && teammates >= 20 && subs.Where(x => x.Reason == "admission:teammate").All(x => x.Listed.StartsWith("TRLA_1")), string.Join(",", badSubs) + " teammates " + teammates);
    int In(string bossId, string variant, string card) => data.BossDecks["enc-" + bossId + "-" + variant].Count(id => id == card);
    Check("A13 the boss cards that stay: Streetsmuggler in Zul'jin's late deck (with the boss's generated Secrets), none of the teammates anywhere",
        In("trl_204_zuljin", "late", "TRLA_168") == 1 && rows.All(r => variantIds.All(v => data.BossDecks["enc-" + r.id + "-" + v].All(id => !id.StartsWith("TRLA_1") || id == "TRLA_168"))));
    Check("A14 the tier pools: two rows in enc-t1, one in enc-t2 (beside Voone), two in enc-t3, three in enc-t4", rows.Count(r => r.tier == "1") == 2 && rows.Count(r => r.tier == "2") == 1
        && rows.Count(r => r.tier == "3") == 2 && rows.Count(r => r.tier == "4") == 3 && data.Boss("trl_200_voone").Tier == "2");
    foreach (var r in rows)
    {
        bool ok = true; var detail = new List<string>();
        foreach (string shrine in r.shrines)
        {
            var (s, plan) = VariantPlan(shrine);
            var v = run.CurrentVariant(s);
            ok &= plan.EncounterId == r.id && plan.BossShrine == shrine && plan.BossHero.Id == r.hero && plan.BossHeroPowerId == r.power && plan.BossGeneratedSecrets == (r.id == "trl_204_zuljin")
                && plan.BossDeck.Select(x => x.Id).SequenceEqual(data.BossDecks[v.Deck]) && plan.EncounterDeck && plan.BossHealth == 30;
            detail.Add(v.Id + ":" + plan.BossShrine);
        }
        Check("A15 " + r.id + ": the plan of each variant is the row's (hero, power, Health 30 on floor 1) with that variant's shrine and deck", ok, string.Join(",", detail));
    }
});

// ============================================================ V  the variant roll
Guard("V", () =>
{
    var counts = new Dictionary<string, int>(StringComparer.Ordinal);
    foreach (var r in rows)
        for (long seed = 1; seed <= 300; seed++)
        {
            var s = At("MAGE", seed, r.id);
            string v = run.CurrentVariant(s).Id;
            counts[r.id + "/" + v] = counts.TryGetValue(r.id + "/" + v, out int n) ? n + 1 : 1;
        }
    Check("V1 300 run seeds draw every variant of every champion, each between 60 and 140 times", rows.All(r => variantIds.All(v => counts.TryGetValue(r.id + "/" + v, out int n) && n >= 60 && n <= 140)),
        string.Join(" ", counts.Select(p => p.Key.Substring(4) + "=" + p.Value)));
    // Wave 4 review: the restage is a real one (the state through the save file's serializer), and the variant is checked
    // against the seed-and-floor derivation and shown to change with the floor.
    var s1 = At("MAGE", 4401, "trl_203_thekal"); var s2 = At("MAGE", 4401, "trl_203_thekal");
    var restored = DungeonRunStore.Clone(new DungeonRunSave { Active = s1 }).Active;
    var thekal = data.Boss("trl_203_thekal");
    DungeonRunVariant Derived(DungeonRunState st) => thekal.Variants[StableSeed.Index(StableSeed.Derive(st.Seed, DungeonRun.EncounterStream, 1000 + st.Floor), thekal.Variants.Count)];
    var byFloor = new HashSet<string>(StringComparer.Ordinal);
    for (int floor = 1; floor <= 12; floor++) { var sf = At("MAGE", 4401, "trl_203_thekal"); sf.Floor = floor; sf.BossSequence[floor - 1] = "trl_203_thekal"; byFloor.Add(run.CurrentVariant(sf).Id); }
    Check("V2 the variant is a function of the run seed and the floor: the same seed gives the same variant, a state restored through the save serializer keeps it (and its plan's shrine), and other floors of the seed draw other variants",
        run.CurrentVariant(s1).Id == run.CurrentVariant(s2).Id && run.CurrentVariant(s1).Id == Derived(s1).Id && restored != s1 && run.CurrentVariant(restored).Id == run.CurrentVariant(s1).Id
        && run.BuildPlan(restored, all).BossShrine == run.CurrentVariant(s1).Shrine && run.BuildPlan(s1, all).BossShrine == run.CurrentVariant(s1).Shrine && byFloor.Count > 1,
        run.CurrentVariant(s1).Id + " restored " + run.CurrentVariant(restored).Id + " floors " + string.Join(",", byFloor));
    // Wave 4 review: a champion appears only "if the player has not chosen one of the <class> shrines" (its wiki page): a
    // Rikkar run with a board shrine never draws the champion of its shrine's class (Voone included), on acts-v3h.
    var mirror = new List<string>(); int shrineRuns = 0, champions = 0;
    foreach (var deck in data.Decks.Where(x => x.Shrine != null && data.FindClass(x.Class) != null).GroupBy(x => C(x.Shrine).Class).Select(gr => gr.First()))
        for (long seed = 1; seed <= 40; seed++)
        {
            var st = run.NewRun(all, deck.Class, deck.Id, data.Class(deck.Class).Powers[0], seed, schedule: "acts-v3h");
            if (st.BoardShrine == null) continue;
            shrineRuns++;
            var cls = C(st.BoardShrine).Class;
            foreach (string id in st.BossSequence)
            {
                var b = data.Boss(id);
                if (b.Variants.Count > 0) champions++;
                if (b.Variants.Any(v => v.Shrine != null && C(v.Shrine).Class == cls)) mirror.Add(deck.Id + "/" + seed + ":" + id);
            }
        }
    Check("V3 a run with a board shrine never meets the champion of its shrine's class (40 seeds per shrine class on acts-v3h); it still meets the other champions",
        shrineRuns >= 9 * 40 && mirror.Count == 0 && champions > 0, "shrine runs " + shrineRuns + " champion fights " + champions + " mirror " + string.Join(" ", mirror.Take(5)));
});

// ============================================================ Z  Steady Throw
Guard("Z", () =>
{
    var g = Fight("TRLA_163");
    var power = g.Player2.Hero.HeroPower;
    Check("Z1 Zul'jin's hero power is Steady Throw (2), scripted by family 13 with the client's text", power.Card.Id == "TRLA_065p" && power.Card.Implemented && power.Cost == 2
        && DungeonRunEncounters.Find("TRLA_065p")?.Family == "13" && DungeonRunEncounters.Find("TRLA_065p")?.Kind == EncounterCardKind.Power
        && C("TRLA_065p").Text.Replace(" @spelldmg", "") == factById["TRLA_065p"].GetProperty("text").GetString(), C("TRLA_065p").Text);
    ToBoss(g);
    var options = g.Player2.Options().OfType<HeroPowerTask>().ToList();
    Check("Z2 it is offered once, with no target", options.Count == 1 && options[0].Target == null, string.Join(",", options.Select(o => o.Target?.Card.Id ?? "-")));
    int me = Hp(g.Player1), boss = Hp(g.Player2);
    Board(g, g.Player2, "CS2_197"); // Ogre Magi: Spell Damage +1
    bool used = Power(g);
    Check("Z3 used: the run player's hero takes 2 (a hero power: no Spell Damage), the boss's hero nothing", used && Hp(g.Player1) == me - 2 && Hp(g.Player2) == boss,
        $"{me}->{Hp(g.Player1)}");
    Check("Z4 once a turn: spent", !g.Player2.Options().Any(o => o is HeroPowerTask));
});

// ============================================================ S  the shrines on the boss's side
Guard("S105", () =>
{
    var g = Fight("TRLA_105"); ToBoss(g); ClearBoards(g);
    var yeti = Board(g, g.Player2, Yeti);
    Damage(g, yeti, 2);
    Check("S105a Shirvallah's Protection (Thekal, late): a boss minion that survives damage gains Divine Shield", yeti.Health == 3 && yeti.HasDivineShield);
    var mine = Board(g, g.Player1, Yeti);
    Damage(g, mine, 2);
    Check("S105b a run player's minion that survives gains nothing", mine.Health == 3 && !mine.HasDivineShield);
});
Guard("S106", () =>
{
    var g = Fight("TRLA_106"); ToBoss(g); ClearBoards(g);
    var shrine = BossShrine(g);
    var yeti = Board(g, g.Player2, Yeti); var mine = Board(g, g.Player1, Yeti);
    Check("S106a Bottled Terror (Hooktusk, early): the boss's other minions have Stealth, the shrine and the run player's minions none",
        yeti.HasStealth && !shrine.HasStealth && !mine.HasStealth);
    ToPlayer(g);
    var arrow = ToHand(g, g.Player1, ArcaneShot);
    var targets = g.Player1.Options().OfType<PlayCardTask>().Where(o => o.Source == arrow).Select(o => o.Target).ToList();
    Check("S106b Arcane Shot from the run player's hand is offered on the boss's hero and shrine, never on the stealthed Yeti", targets.Count > 0 && !targets.Contains(yeti) && targets.Contains(shrine),
        string.Join(",", targets.Select(t => t?.Card.Id)));
    Kill(g, shrine);
    Check("S106c when the shrine goes dormant its Deathrattle gives the boss's minions +2 Attack (TRLA_106e), not the run player's", yeti.AttackDamage == 6 && mine.AttackDamage == 4
        && Has(yeti, "TRLA_106e") && BossShrine(g)?.Card.Id == "TRLA_106t" && !yeti.HasStealth);
});
Guard("S108", () =>
{
    var g = Fight("TRLA_108"); ToBoss(g); ClearBoards(g); ClearHand(g, g.Player2);
    var shrine = BossShrine(g);
    Check("S108a Jan'alai's Mantle (Malacrass, middle): no spell in the boss's hand, Spell Damage 0", shrine.SpellPower == 0 && g.Player2.CurrentSpellPower == 0);
    ToHand(g, g.Player2, Yeti); var bolt = ToHand(g, g.Player2, Frostbolt); ToHand(g, g.Player2, Fireball); ToHand(g, g.Player2, Moonfire);
    Check("S108b three spells in the boss's hand: Spell Damage +3 for the boss", shrine.SpellPower == 3 && g.Player2.CurrentSpellPower == 3 && g.Player1.CurrentSpellPower == 0,
        $"{shrine.SpellPower} {g.Player2.CurrentSpellPower}");
    var golem = Board(g, g.Player1, Golem);
    Play(g, bolt, golem);
    Check("S108c Frostbolt cast from that hand deals 3 + 2 (War Golem 7/7 -> 7/2)", golem.Health == 2 && shrine.SpellPower == 2, $"{golem.Health} {shrine.SpellPower}");
    ToPlayer(g); ToHand(g, g.Player1, Frostbolt); ToHand(g, g.Player1, Frostbolt);
    Check("S108d the run player's spells do not count", shrine.SpellPower == 2 && g.Player1.CurrentSpellPower == 0);
});
Guard("S109", () =>
{
    var g = Fight("TRLA_109"); ToBoss(g); ClearBoards(g);
    var shrine = BossShrine(g);
    var yeti = Board(g, g.Player2, Yeti); var mine = Board(g, g.Player1, Yeti);
    Cast(g, Bolt, g.Player1.Hero); // Lightning Bolt: Overload (1)
    Check("S109a Krag'wa's Lure (Zentimo, middle): after the boss Overloads (1), its minions (the shrine too) gain 1 Attack (TRLA_109e); the run player's none",
        yeti.AttackDamage == 5 && shrine.AttackDamage == 1 && mine.AttackDamage == 4 && Has(yeti, "TRLA_109e"), $"{yeti.AttackDamage} {shrine.AttackDamage}");
    ToPlayer(g); Cast(g, Bolt, g.Player2.Hero);
    Check("S109b the run player's Overload gives the boss's minions nothing", yeti.AttackDamage == 5 && mine.AttackDamage == 4);
});
Guard("S110", () =>
{
    var (s, plan) = VariantPlan("TRLA_110");
    Check("S110a Halazzi's Trap (Zul'jin, late): the plan turns the boss's generated Secrets on", plan.BossGeneratedSecrets && plan.BossShrine == "TRLA_110");
    var g = Fight("TRLA_110"); ToBoss(g); ClearBoards(g);
    int spells = g.Player2.NumSpellsPlayedThisGame;
    Cast(g, ArcaneShot, g.Player1.Hero);
    var secrets = g.Player2.SecretZone.ToList();
    var pool = DungeonRunValidator.BossSecretPool(CardClass.HUNTER).Select(x => x.Id).ToList();
    Check("S110b after the boss casts a spell, a random scripted Hunter Secret is put into play for the boss (not cast: one spell counted), from the closed boss pool",
        secrets.Count == 1 && secrets[0].Card.IsSecret && secrets[0].Card.Class == CardClass.HUNTER && secrets[0].Card.Implemented && pool.Contains(secrets[0].Card.Id)
        && DungeonRunAdmission.Classify(secrets[0].Card, secrets[0].Card.Id, AdmitScope.Boss, null, catalogRun: true).Admitted && g.Player2.NumSpellsPlayedThisGame == spells + 1,
        string.Join(",", secrets.Select(x => x.Card.Id)) + " pool " + pool.Count);
    for (int i = 0; i < 6; i++) Cast(g, ArcaneShot, g.Player1.Hero);
    var ids = g.Player2.SecretZone.Select(x => x.Card.Id).ToList();
    Check("S110c the boss's Secret zone fills to 5 different Secrets and stops", ids.Count == 5 && ids.Distinct().Count() == 5, string.Join(",", ids));
    var (view, vb, vp, bot) = FairView(g, new FairOptions { TrackOwnDeck = true });
    Check("S110d the boss knows its own Secrets (its fair view holds the real ones)", vb.SecretZone.Select(x => x.Card.Id).SequenceEqual(ids));
    ToPlayer(g);
    int bossSecrets = g.Player2.SecretZone.Count, mine = g.Player1.SecretZone.Count;
    Cast(g, Moonfire, g.Player2.Hero);
    Check("S110e the run player casting a spell puts no Secret for anyone", g.Player2.SecretZone.Count <= bossSecrets && g.Player1.SecretZone.Count == mine);
    var g2 = Fight("TRLA_110"); ToBoss(g2); ClearBoards(g2);
    Cast(g2, ArcaneShot, g2.Player1.Hero);
    Check("S110f deterministic: the same match seed puts the same Secret", g2.Player2.SecretZone.FirstOrDefault()?.Card.Id == secrets[0].Card.Id);
});
Guard("S112", () =>
{
    var g = Fight("TRLA_112"); ToBoss(g); ClearBoards(g);
    Mana(g.Player2, 10);
    Play(g, ToHand(g, g.Player2, Yeti));
    int used = g.Player2.UsedMana;
    bool shapeshift = Power(g);
    Check("S112a Gonk's Armament (Loti, late): Shapeshift gains the boss 1 Armor, which refreshes its Mana Crystals (4 for the Yeti + 2 for the power -> 0 used)",
        shapeshift && used == 4 && g.Player2.Hero.Armor >= 1 && g.Player2.UsedMana == 0 && g.Player2.RemainingMana == 10, $"used {used} -> {g.Player2.UsedMana} armor {g.Player2.Hero.Armor}");
    ToPlayer(g); Play(g, ToHand(g, g.Player1, Yeti));
    int mineUsed = g.Player1.UsedMana;
    Cast(g, "EX1_606", null); // Shield Block: 5 Armor for the run player
    Check("S112b the run player's Armor gain refreshes nothing", g.Player1.UsedMana > 0, g.Player1.UsedMana.ToString());
});
Guard("S113", () =>
{
    var g = Fight("TRLA_113"); ToBoss(g); ClearBoards(g);
    Mana(g.Player2, 10);
    int boss = Hp(g.Player2), mana = g.Player2.RemainingMana, me = Hp(g.Player1);
    Play(g, ToHand(g, g.Player2, Fireball), g.Player1.Hero);
    Check("S113a Blood Pact (Jeklik, early): the boss's Fireball costs 4 Health instead of Mana", Hp(g.Player2) == boss - 4 && g.Player2.RemainingMana == mana && Hp(g.Player1) == me - 6,
        $"boss {boss}->{Hp(g.Player2)} mana {mana}->{g.Player2.RemainingMana}");
    ToPlayer(g);
    int pm = g.Player1.RemainingMana, ph = Hp(g.Player1);
    var frost = ToHand(g, g.Player1, Frostbolt); int cost = frost.Cost;
    Play(g, frost, g.Player2.Hero);
    Check("S113b the run player's spells cost Mana as ever", cost > 0 && g.Player1.RemainingMana == pm - cost && Hp(g.Player1) == ph, $"cost {cost} mana {pm}->{g.Player1.RemainingMana} hp {ph}->{Hp(g.Player1)}");
});
Guard("S114", () =>
{
    var g = Fight("TRLA_114"); ToBoss(g); ClearBoards(g);
    var gnome = Board(g, g.Player2, Leper);
    int me = Hp(g.Player1);
    Kill(g, gnome);
    Check("S114a Bwonsamdi's Sanctum (Talanji, late): the boss's Leper Gnome's Deathrattle triggers three times (6 to the run player's hero)", Hp(g.Player1) == me - 6, $"{me}->{Hp(g.Player1)}");
    var mineGnome = Board(g, g.Player1, Leper); int boss = Hp(g.Player2);
    Kill(g, mineGnome);
    Check("S114b the run player's Deathrattle triggers once", Hp(g.Player2) == boss - 2, $"{boss}->{Hp(g.Player2)}");
});
Guard("S115", () =>
{
    var g = Fight("TRLA_115"); ToBoss(g); ClearBoards(g);
    Mana(g.Player2, 10);
    var yeti = ToHand(g, g.Player2, Yeti); Play(g, yeti);
    var summoned = Board(g, g.Player2, Wisp);
    var mine = Board(g, g.Player1, Wisp);
    Check("S115a Gonk's Mark (Loti, middle): every minion the boss plays or summons gets +1/+1 (TRLA_115e); the run player's none",
        ((Minion)yeti).AttackDamage == 5 && ((Minion)yeti).Health == 6 && summoned.AttackDamage == 2 && mine.AttackDamage == 1 && Has(summoned, "TRLA_115e"));
});
Guard("S116", () =>
{
    var g = Fight("TRLA_116"); ToBoss(g); ClearBoards(g);
    var shrine = BossShrine(g);
    Power(g); // Shapeshift: +1 Attack this turn, +1 Armor
    int heroAttack = g.Player2.Hero.AttackDamage;
    EndTurn(g);
    Check("S116a Bonds of Balance (Loti, early): at the end of the boss's turn the shrine gains its hero's Attack (1)", heroAttack == 1 && shrine.AttackDamage == 1 && Has(shrine, "TRLA_116e"),
        $"hero {heroAttack} shrine {shrine.AttackDamage}");
    Mana(g.Player1); Play(g, ToHand(g, g.Player1, "CS2_106")); // Fiery War Axe for the run player
    EndTurn(g);
    Check("S116b the run player's hero Attack at the end of its turn gives the boss's shrine nothing", shrine.AttackDamage == 1);
});
Guard("S128", () =>
{
    var g = Fight("TRLA_128"); ToBoss(g); ClearBoards(g);
    var hp = g.Player2.Hero.HeroPower;
    Check("S128a Jan'alai's Flame (Malacrass, early): the boss's Fireblast costs (0) (printed 2); the run player's power keeps its cost",
        hp.Card.Id == "HERO_08bp" && hp.Cost == 0 && g.Player1.Hero.HeroPower.Cost == g.Player1.Hero.HeroPower.Card.Cost, hp.Cost.ToString());
    Mana(g.Player2, 0);
    var w1 = Board(g, g.Player1, Wisp);
    bool used = Power(g, w1);
    Check("S128b with no Mana, Fireblast kills a Wisp and is refreshed", used && w1.IsDead && !hp.IsExhausted);
    var yeti = Board(g, g.Player1, Yeti);
    Power(g, yeti);
    Check("S128c a hit that does not kill leaves it spent", yeti.Health == 4 && hp.IsExhausted);
});
Guard("S129", () =>
{
    var g = Fight("TRLA_129"); ToBoss(g); ClearBoards(g); ClearHand(g, g.Player2);
    var shrine = BossShrine(g);
    var golem = Board(g, g.Player1, Golem);
    Cast(g, Frostbolt, golem);
    var ff = g.Player2.HandZone.Where(p => p.Card.Id == DungeonRunShrines.Frostfire).ToList();
    Check("S129a Jan'alai's Progeny (Malacrass, late): the boss freezing a minion puts a Frostfire into the boss's hand (created by its shrine)",
        golem.IsFrozen && ff.Count == 1 && ff[0][GameTag.DISPLAYED_CREATOR] == shrine.Id);
    int mana = g.Player2.RemainingMana; var wisp = Board(g, g.Player1, Wisp);
    Play(g, ff[0], wisp);
    Check("S129b the boss plays it: (0) Mana, 1 damage (the Wisp dies)", wisp.IsDead && g.Player2.RemainingMana == mana);
    ToPlayer(g); ClearHand(g, g.Player1);
    var bossYeti = Board(g, g.Player2, Yeti);
    Cast(g, Frostbolt, bossYeti);
    Check("S129c the run player freezing the boss's minion gives nobody a Frostfire", bossYeti.IsFrozen && !g.Player1.HandZone.Any(p => p.Card.Id == DungeonRunShrines.Frostfire)
        && !g.Player2.HandZone.Any(p => p.Card.Id == DungeonRunShrines.Frostfire));
});
Guard("S137", () =>
{
    var g = Fight("TRLA_137"); ClearBoards(g); Mana(g.Player1);
    int me = Hp(g.Player1), boss = Hp(g.Player2);
    Play(g, ToHand(g, g.Player1, Moonfire), g.Player2.Hero);
    Check("S137a Shirvallah's Vengeance (Thekal, middle): after the boss's hero takes 1, the run player's hero takes 5", Hp(g.Player2) == boss - 1 && Hp(g.Player1) == me - 5,
        $"boss {boss}->{Hp(g.Player2)} me {me}->{Hp(g.Player1)}");
    ToBoss(g); me = Hp(g.Player1); boss = Hp(g.Player2);
    Cast(g, Moonfire, g.Player1.Hero);
    Check("S137b the boss damaging the run player's hero does not answer", Hp(g.Player1) == me - 1 && Hp(g.Player2) == boss);
    Check("S137c the guard is cleared", !g.NativeTags.ContainsKey(ShrineRules.VengeanceResolving));
});
Guard("S138", () =>
{
    var g = Fight("TRLA_138"); ToBoss(g); ClearBoards(g);
    var shrine = BossShrine(g);
    var yeti = Board(g, g.Player2, Yeti);
    Cast(g, Kings, yeti);
    Check("S138a Shirvallah's Grace (Thekal, early): Blessing of Kings on the boss's Yeti is cast again on the shrine (0/5 -> 4/9)",
        yeti.AttackDamage == 8 && shrine.AttackDamage == 4 && shrine.Health == 9, $"{shrine.AttackDamage}/{shrine.Health}");
    var mine = Board(g, g.Player1, Yeti);
    Cast(g, Kings, mine);
    Check("S138b cast on a run player's minion: no copy", shrine.AttackDamage == 4 && mine.AttackDamage == 8);
});
Guard("S146", () =>
{
    var (s, plan) = VariantPlan("TRLA_146");
    var g = Fight("TRLA_146"); ToBoss(g); ClearBoards(g);
    var made = new List<IPlayable>();
    for (int i = 0; i < 30; i++)
    {
        ClearHand(g, g.Player2); ClearBoards(g);
        Cast(g, Moonfire, Board(g, g.Player1, Golem));
        made.AddRange(g.Player2.HandZone);
        if (g.State == State.COMPLETE) break;
    }
    // Step 9 (lane G, D15): the both-sides list (ExcludedGenerated) now holds the generated unscripted cards (five Priest spells of
    // this pool), so the filter the boss's generation reads is both lists; before, the Priest spells it dropped were all boss-side.
    var excluded = new HashSet<string>((plan.ExcludedGeneratedBoss ?? new string[0]).Concat(plan.ExcludedGenerated ?? new string[0]));
    // Before step 9 the engine's random pools held unscripted cards too (as for the run player's Tome and every random card since
    // phase 2); step 9 lists them, so neither an unscripted nor an excluded card comes.
    var bad = made.Where(p => p.Card.Type != CardType.SPELL || p.Card.Class != CardClass.PRIEST || p.Card.IsSecret || excluded.Contains(p.Card.Id)
        || !DungeonRunAdmission.Classify(p.Card, p.Card.Id, AdmitScope.Boss, null, catalogRun: true).Admitted).Select(p => p.Card.Id).ToList();
    Check("S146a Bwonsamdi's Tome (Talanji, early): each spell the boss casts adds one random Priest spell to its hand, always through the boss's generation filter (admitted for a boss, never a Secret or a listed exclusion)",
        made.Count == 30 && bad.Count == 0 && made.Select(p => p.Card.Id).Distinct().Count() >= 8 && excluded.Count > 0, $"{made.Count} made, {made.Select(p => p.Card.Id).Distinct().Count()} kinds, bad {string.Join(",", bad.Select(id => id + "/" + DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss, null, catalogRun: true).Family + "/" + excluded.Contains(id)))}");
    // The pool the boss's generation reads on its turn leaves out every Priest spell the boss's lists exclude (there are some).
    var raw = Cards.AllWild.Where(c => c.Type == CardType.SPELL && c.Class == CardClass.PRIEST && c[GameTag.QUEST] == 0).Select(c => c.Id).ToList();
    var bossPool = RandomCardTask.GetCardList(BossShrine(g), CardType.SPELL, CardClass.PRIEST).Select(c => c.Id).ToList();
    var kept = raw.Where(excluded.Contains).ToList();
    Check("S146c on the boss's turn its generation pool of Priest spells drops every id the boss's lists exclude (" + kept.Count + " of " + raw.Count + "), and keeps the rest",
        kept.Count > 0 && !bossPool.Any(excluded.Contains) && bossPool.Count == raw.Count - kept.Count, $"{bossPool.Count} of {raw.Count}; excluded {string.Join(",", kept.Take(6))}");
    ToPlayer(g); ClearHand(g, g.Player1); int bossHand = g.Player2.HandZone.Count;
    Cast(g, Moonfire, g.Player2.Hero);
    Check("S146b the run player's spell adds nothing for anyone", g.Player1.HandZone.Count == 0 && g.Player2.HandZone.Count == bossHand);
});
Guard("S147", () =>
{
    var g = Fight("TRLA_147"); ClearBoards(g); Mana(g.Player1);
    Damage(g, g.Player1.Hero, 10);
    int me = Hp(g.Player1);
    Play(g, ToHand(g, g.Player1, HealingTouch), g.Player1.Hero);
    Check("S147a Bwonsamdi's Covenant (Talanji, middle): the run player healing its own hero (8) damages it instead", Hp(g.Player1) == me - 8, $"{me}->{Hp(g.Player1)}");
    ToBoss(g); Damage(g, g.Player2.Hero, 5); int boss = Hp(g.Player2);
    Power(g, g.Player2.Hero); // Lesser Heal on its own hero
    Check("S147b the boss's own heals still heal (Lesser Heal: +2)", Hp(g.Player2) == boss + 2, $"{boss}->{Hp(g.Player2)}");
    me = Hp(g.Player1);
    Cast(g, HealingTouch, g.Player1.Hero);
    Check("S147c the boss healing the run player's hero damages it too", Hp(g.Player1) == me - 8, $"{me}->{Hp(g.Player1)}");
});
Guard("S154", () =>
{
    var g = Fight("TRLA_154"); ToBoss(g); ClearBoards(g);
    int me = Hp(g.Player1);
    Mana(g.Player2); Play(g, ToHand(g, g.Player2, Archer), g.Player1.Hero);
    Check("S154a Tribute from the Tides (Zentimo, late): the boss's Elven Archer's Battlecry triggers three times (3 to the run player's hero)", Hp(g.Player1) == me - 3, $"{me}->{Hp(g.Player1)}");
    ToPlayer(g); int boss = Hp(g.Player2);
    Play(g, ToHand(g, g.Player1, Archer), g.Player2.Hero);
    Check("S154b the run player's Battlecry triggers once", Hp(g.Player2) == boss - 1, $"{boss}->{Hp(g.Player2)}");
});
Guard("S155", () =>
{
    var g = Fight("TRLA_155"); ToBoss(g);
    var fb = ToHand(g, g.Player2, Fireball); var mb = ToHand(g, g.Player1, Fireball); var y = ToHand(g, g.Player2, Yeti);
    Check("S155a Krag'wa's Grace (Zentimo, early): the boss's spells cost (2) less (Fireball 4 -> 2), its minions and the run player's spells full price",
        fb.Cost == 2 && mb.Cost == 4 && y.Cost == 4, $"{fb.Cost} {mb.Cost} {y.Cost}");
});
Guard("S162", () =>
{
    var g = Fight("TRLA_162"); ToBoss(g); ClearBoards(g); ClearHand(g, g.Player2);
    ToHand(g, g.Player2, Yeti); ToHand(g, g.Player2, Fireball);
    var golem = Board(g, g.Player2, Golem); var wisp = Board(g, g.Player1, Wisp);
    Attack(g, golem, wisp);
    Check("S162a Halazzi's Hunt (Zul'jin, middle): the boss's War Golem Overkills a Wisp: the cards in the boss's hand cost (1) less (TRLA_162e2)",
        g.Player2.HandZone.All(p => p.Cost == p.Card.Cost - 1 && Has(p, "TRLA_162e2")), string.Join(",", g.Player2.HandZone.Select(p => p.Card.Id + ":" + p.Cost)));
    ToPlayer(g); ClearHand(g, g.Player1); ToHand(g, g.Player1, Yeti);
    var mine = Board(g, g.Player1, Golem); Attack(g, mine, Board(g, g.Player2, Wisp));
    Check("S162b the run player's Overkill cuts nothing", g.Player1.HandZone.All(p => p.Cost == p.Card.Cost));
});
Guard("S163", () =>
{
    var g = Fight("TRLA_163"); ToBoss(g); ClearBoards(g);
    var grants = new List<string>();
    for (int i = 0; i < 6; i++)
    {
        var beast = Board(g, g.Player2, Raptor);
        grants.AddRange(beast.AppliedEnchantments?.Where(e => DungeonRunShrines.GuiseGrants.Contains(e.Card.Id)).Select(e => e.Card.Id) ?? new string[0]);
        if (g.Player2.BoardZone.IsFull) ClearBoards(g);
    }
    var mine = Board(g, g.Player1, Raptor);
    Check("S163a Halazzi's Guise (Zul'jin, early): each Beast the boss summons gains one of Rush, Taunt or Poisonous; the run player's Beast none",
        grants.Count == 6 && grants.All(id => DungeonRunShrines.GuiseGrants.Contains(id)) && grants.Distinct().Count() >= 2 && !DungeonRunShrines.GuiseGrants.Any(e => Has(mine, e)),
        string.Join(",", grants));
});
Guard("S178", () =>
{
    var (s, plan) = VariantPlan("TRLA_178");
    var g = Fight("TRLA_178"); ToBoss(g); ClearBoards(g);
    var excluded = new HashSet<string>(plan.ExcludedGeneratedBoss ?? new string[0]);
    var demons = new List<string>();
    for (int i = 0; i < 12 && g.State != State.COMPLETE; i++)
    {
        ClearHand(g, g.Player2); ClearBoards(g);
        ToHand(g, g.Player2, Yeti);
        Cast(g, Soulfire, g.Player1.Hero.Health > 10 ? (ICharacter)g.Player1.Hero : g.Player2.Hero);
        demons.AddRange(g.Player2.BoardZone.Where(m => !ShrineRules.IsMarked(m)).Select(m => m.Card.Id));
    }
    var bad = demons.Where(id => !C(id).IsRace(Race.DEMON) || excluded.Contains(id) || !DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss, null, catalogRun: true).Admitted).ToList();
    Check("S178a Dark Reliquary (Jeklik, middle): each discard of the boss summons one random Demon for the boss, through the boss's generation filter",
        demons.Count == 12 && bad.Count == 0 && demons.Distinct().Count() >= 4, $"{demons.Count}: {string.Join(",", demons.Take(12))} bad {string.Join(",", bad.Select(id => id + "/" + DungeonRunAdmission.Classify(C(id), id, AdmitScope.Boss, null, catalogRun: true).Family + "/" + excluded.Contains(id)))}");
    ToPlayer(g); ClearBoards(g); ToHand(g, g.Player1, Yeti);
    Cast(g, Soulfire, g.Player2.Hero);
    Check("S178b the run player's discard summons nothing", g.Player1.BoardZone.Count == 0 && g.Player2.BoardZone.Count(m => !ShrineRules.IsMarked(m)) == 0);
});
Guard("S179", () =>
{
    var g = Fight("TRLA_179"); ToBoss(g); ClearBoards(g);
    int me = Hp(g.Player1), boss = Hp(g.Player2), hand = g.Player2.HandZone.Count;
    Mana(g.Player2); Power(g); // Life Tap: draw a card and take 2 damage
    Check("S179a Hir'eek's Hunger (Jeklik, late): Life Tap's 2 damage on the boss's turn goes to the run player's hero instead; the card is drawn",
        Hp(g.Player2) == boss && Hp(g.Player1) == me - 2 && g.Player2.HandZone.Count == hand + 1, $"boss {boss}->{Hp(g.Player2)} me {me}->{Hp(g.Player1)}");
    ToPlayer(g); boss = Hp(g.Player2); me = Hp(g.Player1);
    Play(g, ToHand(g, g.Player1, Moonfire), g.Player2.Hero);
    Check("S179b on the run player's turn the boss's hero takes its damage", Hp(g.Player2) == boss - 1 && Hp(g.Player1) == me);
});
Guard("S186", () =>
{
    var g = Fight("TRLA_186", playerDeck: Croc);
    var shrine = BossShrine(g);
    var playerDeck = new HashSet<int>(g.Player1.DeckZone.Select(p => p.Id));
    int deckCount = g.Player1.DeckZone.Count, bossHand = g.Player2.HandZone.Count;
    EndTurn(g);
    var stolen = g.Player2.HandZone.Where(p => playerDeck.Contains(p.Id)).ToList();
    var card = stolen.FirstOrDefault();
    Check("S186a Treasure from Below (Hooktusk, middle): at the start of the boss's turn one card of the run player's deck is now the boss's, in its hand (the run player's deck - 1)",
        stolen.Count == 1 && card.Controller == g.Player2 && card.Zone == g.Player2.HandZone && g.Player1.DeckZone.Count == deckCount - 1 && g.Player2.HandZone.Count == bossHand + 2,
        $"{stolen.Count} {card?.Card.Id} deck {deckCount}->{g.Player1.DeckZone.Count}");
    Check("S186b it costs (2) less (LOOT_204e): River Crocolisk 2 -> 0", card != null && card.Card.Id == Croc && card.Cost == 0 && Has(card, "LOOT_204e"));
    var (view, vb, vp, bot) = FairView(g);
    var known = bot.Facts.Own.Hand.FirstOrDefault(c => c.EntityId == card.Id);
    Check("S186c the boss's fair view: it knows exactly the card it took (a card of its own hand)", known != null && !known.Unknown && known.CardId == Croc);
    Check("S186d the run player's other deck cards stay unknown slots in the boss's view (count = the deck's)",
        bot.Facts.Opponent.DeckCount == g.Player1.DeckZone.Count && vp.DeckZone.Count == g.Player1.DeckZone.Count && vp.DeckZone.All(p => p is SabberStoneCore.Model.Entities.Unknown),
        $"{bot.Facts.Opponent.DeckCount} {vp.DeckZone.Count}");
    string simulated = "ok"; int viewBossHand = vb.HandZone.Count;
    try { view.Process(EndTurnTask.Any(view.CurrentPlayer)); } catch (Exception e) { simulated = e.GetType().Name + ": " + e.Message; }
    Check("S186e in the boss's view the run player's next turn start steals nothing (the shrine works on its owner's turn)", simulated == "ok" && vb.HandZone.Count == viewBossHand,
        $"{simulated} {viewBossHand}->{vb.HandZone.Count}");
    Mana(g.Player2); Play(g, card);
    Check("S186f the boss plays the stolen card (a minion of its board now)", g.Player2.BoardZone.Contains(card) && card.Controller == g.Player2);
    // A full hand burns it; an empty deck steals nothing.
    var g2 = Fight("TRLA_186");
    FillHand(g2.Player2); int grave = g2.Player2.GraveyardZone.Count, deck2 = g2.Player1.DeckZone.Count;
    EndTurn(g2);
    Check("S186g with the boss's hand full the stolen card is burned (out of the run player's deck all the same)", g2.Player1.DeckZone.Count == deck2 - 1 && g2.Player2.GraveyardZone.Count >= grave + 1,
        $"deck {deck2}->{g2.Player1.DeckZone.Count}");
    var g3 = Fight("TRLA_186");
    EmptyDeck(g3.Player1); int hand3 = g3.Player2.HandZone.Count;
    EndTurn(g3);
    Check("S186h the run player's deck empty: nothing is stolen (the boss only draws)", g3.Player2.HandZone.Count == hand3 + 1 && g3.State != State.COMPLETE);
});
Guard("S187", () =>
{
    var g = Fight("TRLA_187"); ToBoss(g); ClearBoards(g);
    int me = Hp(g.Player1), spells = g.Player2.NumSpellsPlayedThisGame;
    Cast(g, Moonfire, g.Player1.Hero);
    Check("S187a Pirate's Mark (Hooktusk, late): the boss's Moonfire is cast again on the same target (2 to the run player's hero), one hand cast counted",
        Hp(g.Player1) == me - 2 && g.Player2.NumSpellsPlayedThisGame == spells + 1, $"{me}->{Hp(g.Player1)}");
    ToPlayer(g); int boss = Hp(g.Player2);
    Cast(g, Moonfire, g.Player2.Hero);
    Check("S187b the run player's spell is cast once", Hp(g.Player2) == boss - 1);
});

// ============================================================ L  the boss shrine's lifecycle, guards and fair view, for each of the 24
Guard("L", () =>
{
    var landed = new List<string>(); var dormant = new List<string>(); var revived = new List<string>(); var viewed = new List<string>();
    foreach (string id in rows.SelectMany(r => r.shrines))
    {
        var g = Fight(id);
        var shrine = BossShrine(g);
        if (shrine?.Card.Id == id && ShrineRules.IsAwake(shrine) && ShrineRules.Of(g.Player1).Count == 0 && shrine.ZonePosition == 0 && shrine.Controller == g.Player2) landed.Add(id);
        ToBoss(g);
        var (view, vb, vp, bot) = FairView(g);
        if (ShrineRules.Of(vb).Any(m => m.Card.Id == id) && bot.Facts.Own.Board.Any(c => c.CardId == id) && !bot.IsTerminal) viewed.Add(id);
        Kill(g, shrine);
        var form = BossShrine(g);
        if (form?.Card.Id == id + "t" && ShrineRules.IsDormantForm(form)) dormant.Add(id);
        // Dormant on the boss's turn 2: back at the start of the boss's third turn after it (turns are counted per owner).
        int boss = 0;
        while (g.State != State.COMPLETE && boss < 3) { EndTurn(g); if (g.CurrentPlayer == g.Player2) boss++; }
        if (BossShrine(g)?.Card.Id == id && ShrineRules.IsAwake(BossShrine(g)) && ShrineRules.Of(g.Player2).Count == 1) revived.Add(id);
    }
    Check("L1 each of the 24 shrines lands on the boss's board at Start of Game (left-most slot, awake, marked), none on the run player's", landed.Count == 24, string.Join(",", landed));
    Check("L2 the boss's fair view sees its shrine on its board", viewed.Count == 24, string.Join(",", viewed));
    Check("L3 each goes dormant in its slot when it dies (its <id>t form)", dormant.Count == 24, string.Join(",", dormant));
    Check("L4 each revives at the start of the boss's third turn after its death", revived.Count == 24, string.Join(",", revived));
    var gp = Fight("TRLA_105"); var target = BossShrine(gp);
    Cast(gp, Polymorph, target);
    Check("L5 the run player's Polymorph on a boss shrine destroys it instead (dormant, no Sheep)", BossShrine(gp)?.Card.Id == "TRLA_105t" && !gp.Player2.BoardZone.Any(m => m.Card.Id == "CS2_tk1"));
    var gm = Fight("TRLA_105"); var mc = ToHand(gm, gm.Player1, MindControl); Mana(gm.Player1); var bossYeti = Board(gm, gm.Player2, Yeti);
    var mcTargets = gm.Player1.Options().OfType<PlayCardTask>().Where(o => o.Source == mc).Select(o => o.Target).ToList();
    Check("L6 Mind Control takes the boss's Yeti but never its shrine (not a target)", mcTargets.Contains(bossYeti) && !mcTargets.Contains(BossShrine(gm)), string.Join(",", mcTargets.Select(t => t?.Card.Id)));
});

// ============================================================ X  shrines on both sides
Guard("X", () =>
{
    var g = Fight("TRLA_137", playerShrine: "TRLA_137"); ClearBoards(g); Mana(g.Player1);
    // X1-X3 are engine staging: since the wave 4 review the run layer never pairs a Rikkar run with the champion of its own
    // shrine class (V3), but the engine must still keep two shrines of one class apart.
    Check("X1 (engine staging) a Rikkar Paladin's Vengeance against Thekal's: one shrine on each board", ShrineRules.Of(g.Player1).Single().Card.Id == "TRLA_137" && BossShrine(g)?.Card.Id == "TRLA_137");
    int me = Hp(g.Player1), boss = Hp(g.Player2);
    Play(g, ToHand(g, g.Player1, Moonfire), g.Player2.Hero);
    Check("X2 the run player's Moonfire on the boss: the boss's Vengeance answers with 5; the run player's does not answer that (recursion guard)",
        Hp(g.Player2) == boss - 1 && Hp(g.Player1) == me - 5 && g.State != State.COMPLETE && !g.NativeTags.ContainsKey(ShrineRules.VengeanceResolving), $"boss {boss}->{Hp(g.Player2)} me {me}->{Hp(g.Player1)}");
    var g2 = Fight("TRLA_186", bossDeck: Wisp, playerDeck: Croc, playerShrine: "TRLA_186");
    var bossDeck = new HashSet<int>(g2.Player2.DeckZone.Select(p => p.Id)); var playerDeck = new HashSet<int>(g2.Player1.DeckZone.Select(p => p.Id));
    EndTurn(g2); EndTurn(g2);
    Check("X3 (engine staging) two Treasures from Below: each owner steals one card of the other's deck at its own turn start",
        g2.Player2.HandZone.Count(p => playerDeck.Contains(p.Id)) == 1 && g2.Player1.HandZone.Count(p => bossDeck.Contains(p.Id)) == 1);
    var g3 = Fight("TRLA_179", playerShrine: "TRLA_137"); ToBoss(g3); ClearBoards(g3);
    me = Hp(g3.Player1); boss = Hp(g3.Player2);
    Power(g3); // the boss's Life Tap: its 2 damage go to the run player's hero (Hunger), and the run player's Vengeance answers that hit with 5
    Check("X4 Jeklik's Hunger against a run player's Vengeance: Life Tap's 2 go to the run player's hero, whose Vengeance answers with 5 at the boss while the redirected hit resolves, so the Hunger lets it through (a hit inside a redirect is never redirected); the chain stops, both guards cleared",
        g3.State != State.COMPLETE && Hp(g3.Player2) == boss - 5 && Hp(g3.Player1) == me - 2 && !g3.NativeTags.ContainsKey(ShrineRules.VengeanceResolving)
        && !g3.NativeTags.ContainsKey(DungeonRunShrines.HungerResolving), $"boss {boss}->{Hp(g3.Player2)} me {me}->{Hp(g3.Player1)}");
});

// ============================================================ F  the boss's fair view with a chained Discover (Griftah, Malacrass's early deck)
Guard("F", () =>
{
    var g = Fight("TRLA_128"); ToBoss(g); ClearBoards(g); ClearHand(g, g.Player2);
    int bossHand = g.Player2.HandZone.Count, playerHand = g.Player1.HandZone.Count;
    Cast(g, "TRL_096");   // Griftah: "Battlecry: Discover two cards. Give one to your opponent at random."
    var first = g.Player2.Choice;
    Check("F1 the boss's Griftah opens a Discover with a second one chained and not yet dealt", first != null && first.Choices?.Count > 0 && first.NextChoice != null && first.NextChoice.Choices == null,
        $"{first?.Choices?.Count} next {(first?.NextChoice == null ? "none" : first.NextChoice.Choices == null ? "undealt" : "dealt")}");
    string made = "ok";
    try { FairBotState.Create(g, g.Player2.PlayerId, 1, DeckStyle.Tempo); FairBotState.Create(g, g.Player2.PlayerId, 1, DeckStyle.Tempo, new FairOptions { TrackOwnDeck = true }); }
    catch (Exception e) { made = e.GetType().Name + ": " + e.Message; }
    Check("F2 the boss's fair view is built with the undealt chained choice (no entity to mask; was an ArgumentNullException and a fallback)", made == "ok", made);
    for (int i = 0; i < 2 && g.Player2.Choice != null; i++)
    {
        var d = g.GetPendingDecisions().Single(x => x.ControllerId == g.Player2.Id);
        g.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(d.Id, d.ControllerId, new[] { g.Player2.Choice.Choices[0] }), out _);
    }
    Check("F3 both Discovers resolve: one card to the boss's hand, one to the run player's", g.Player2.Choice == null && g.Player2.HandZone.Count == bossHand + 1 && g.Player1.HandZone.Count == playerHand + 1,
        $"boss {bossHand}->{g.Player2.HandZone.Count} player {playerHand}->{g.Player1.HandZone.Count}");
});

Console.WriteLine($"SearchTelemetry swallowed leaf exceptions {SearchTelemetry.SwallowedLeafErrors}");
Console.WriteLine($"RESULT {passes} passed, {failures + SearchTelemetry.SwallowedLeafErrors} failed");
return failures + SearchTelemetry.SwallowedLeafErrors;
