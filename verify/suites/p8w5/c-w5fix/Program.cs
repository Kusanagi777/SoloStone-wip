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

// Phase 8 wave 5 integration (families 14-19): the fixes the merged tree needed, asserted in real engine games through the
// run setup path (DungeonRunSetup.Configure / ApplyAfterStart) and on the run layer; the helpers are c-w4fix's.
//   W1-W3  Game.ClearWeapons names the hero, not the weapon: a weapon broken in an attack (its removal pending until the
//          next death pass) that is replaced before that pass took the new weapon with it (family 19 found it through
//          Mister Chu's "Confiscated" Goods, c-enc-19 F3). Hero.AddWeapon now drops the replaced weapon's pending removal.
//   Q1-Q6  A run never meets a boss row of the character it plays (family 19 flag 3: the run hero DALA_Squeamlish against
//          the Tombs boss ulda_07_squeamlish; the merge review added DALA_Chu / ulda_06_chu and DALA_George /
//          loot_15_george_karl): a hero row's "excludeBosses" (run-v1.json) keeps those rows out of the act draw. Wave 6
//          (families 20-24) adds DALA_Barkeye / ulda_50_barkeye (family 21 flag 6) and DALA_Vessina / ulda_63_vessina (family
//          22). The close-out (family 25) closes DALA_Tekahn / ulda_67_tekahn (family 24's finale row, the fixed boss of
//          acts-v3t's finale, which excludeBosses cannot filter): the key makes the default roll leave acts-v3t out for that
//          row (DungeonRun.DefaultSchedule), NewRun refuses acts-v3t named for it, and a DALA_Tekahn run follows acts-v3h,
//          whose finale is the Heist's (Q7-Q10; Q7 was the close-out's tripwire until then).
//   E1-E3  Unstable Evolution's hand copy (LOOT_504t) never costs less than (1) (the Echo copy floor; the client prints the
//          card with Echo since patch 23.0.3): under Millhouse Manastorm's "enemy spells cost (0)" the run player's copies
//          looped without end against Rasil (bp.sh enc15, "stuck after 1500 actions", found by this regression).
//   N1     No encounter note says a card "has no script yet" while an encounter deck holds it (the merge review: family 16's
//          Gold Elemental note said so of Infuriated Banker, which family 17 scripts and the merged deck holds).
//   O1-O3  Wave 6: Snowfury Giant (ICC_090) counts the Mana Crystals each OVERLOAD event Overloads, as OverloadThisGame does,
//          not its source's printed Overload: under Vessina's Plaguebringer the game's cost drifted from the cost a copy of the
//          game computes, and the boss's fair-view search chose a Giant the game could not pay for (bp.sh enc22a, "engine
//          rejected boss task", found by the wave-6 regression).
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
string root = VerifyPaths.Data;
string R(string f) => File.ReadAllText(Path.Combine(root, f));
var data = DungeonRunData.Parse(R("dungeon-run/run-v1.json"), R("dungeon-run/pool-v1.json"), R("npc/decks-v1.json"), R("dungeon-run/catalog-v1.json"),
    R("dungeon-run/schedule-v1.json"), R("dungeon-run/tavern-v1.json"), R("dungeon-run/encounters-v1.json"));
var run = new DungeonRun(data);
var all = new DungeonRunProfile(); all.Unlocked.AddRange(data.Unlocks.Keys);

string yeti = "CS2_182"; // Chillwind Yeti 4/5, cost 4
string[] Deck(params (string id, int n)[] cards) => cards.SelectMany(c => Enumerable.Repeat(c.id, c.n)).ToArray();
string[] filler = Deck((yeti, 30));
RunMatchPlan Plan(string[] playerDeck = null, string[] bossDeck = null, string playerHero = "HERO_08", string playerPower = "CS2_034") => new RunMatchPlan
{
    PlayerHero = C(playerHero), PlayerHeroPowerId = playerPower, PlayerDeck = (playerDeck ?? filler).Select(C).ToList(),
    BossHero = C("LOOTA_BOSS_35h"), BossHeroPowerId = "LOOTA_BOSS_35p", BossHealth = 30, BossDeck = (bossDeck ?? filler).Select(C).ToList(),
    PlayerGeneratedSecrets = true, EncounterId = "test", EncounterDeck = true,
    ExcludedGenerated = DungeonRunAdmission.GeneratedRejects(true), ExcludedGeneratedBoss = DungeonRunAdmission.GeneratedRejectsBoss(true),
};
Game Ready(RunMatchPlan plan, int seed = 7, bool history = true)
{
    var cfg = new GameConfig { RandomSeed = seed, Shuffle = true, SkipMulligan = false, History = history, Logging = false, FormatType = FormatType.FT_WILD };
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
void Mana(Controller c, int n = 10) { c.BaseMana = n; c.UsedMana = 0; c.OverloadLocked = 0; c.OverloadOwed = 0; }
IPlayable ToHand(Game g, Controller c, string id)
{
    while (c.HandZone.Count >= 9) { var old = c.HandZone[0]; c.HandZone.Remove(old); c.SetasideZone.Add(old); }
    var e = Entity.FromCard(c, C(id)); Generic.AddHandPhase.Invoke(c, e); g.DeathProcessingAndAuraUpdate(); return e;
}
Minion Board(Game g, Controller c, string id, bool ready = true)
{
    var m = (Minion)Entity.FromCard(c, C(id)); Generic.SummonBlock(g, m, -1, c.Hero); g.DeathProcessingAndAuraUpdate();
    if (ready) m.IsExhausted = false;
    return m;
}
bool Play(Game g, IPlayable card, ICharacter target = null) { Mana(g.CurrentPlayer); return g.Process(PlayCardTask.Any(g.CurrentPlayer, card, target, -1, 0)); }
bool PlayNew(Game g, string id, ICharacter target = null) => Play(g, ToHand(g, g.CurrentPlayer, id), target);
void EndTurn(Game g) => g.Process(EndTurnTask.Any(g.CurrentPlayer));
bool Power(Game g, ICharacter target = null) { Mana(g.CurrentPlayer); return g.Process(HeroPowerTask.Any(g.CurrentPlayer, target)); }
bool Attack(Game g, Minion m, ICharacter target) { m.IsExhausted = false; return g.Process(MinionAttackTask.Any(g.CurrentPlayer, m, target)); }
void ToBoss(Game g) { if (g.CurrentPlayer == g.Player1) EndTurn(g); Mana(g.Player2); }
void ToPlayer(Game g) { if (g.CurrentPlayer == g.Player2) EndTurn(g); Mana(g.Player1); }
void Round(Game g) { EndTurn(g); EndTurn(g); Mana(g.CurrentPlayer); }
List<string> Hand(Controller c) => c.HandZone.Select(p => p.Card.Id).ToList();
List<string> Secrets(Controller c) => c.SecretZone.Select(s => s.Card.Id).ToList();
void SecretsToDeck(Controller c) { foreach (var s in c.HandZone.Where(p => p.Card.IsSecret).ToList()) { c.HandZone.Remove(s); c.DeckZone.Add(s); } }
bool Pick(Game g, Controller c, int index = 0) => c.Choice != null && g.Process(ChooseTask.Pick(c, c.Choice.Choices[index]));
List<Card> Offers(Game g, Controller c) => c.Choice == null ? new List<Card>() : c.Choice.Choices.Select(id => g.IdEntityDic[id].Card).ToList();
void ClearHand(Controller c) { foreach (var p in c.HandZone.ToList()) { c.HandZone.Remove(p); c.SetasideZone.Add(p); } }


const string Axe = "CS2_106", Reaper = "CS2_112"; // Fiery War Axe 3/2, Arcanite Reaper 5/2

// ============================================================ W. a weapon broken in an attack, replaced before the death pass
Guard("W", () =>
{
    var g = Ready(Plan());
    Controller p1 = g.Player1;
    PlayNew(g, Axe);
    var axe = p1.Hero.Weapon;
    // What a weapon's last Durability point does (Weapon.ToBeDestroyed): its removal waits for the next death pass.
    axe.ToBeDestroyed = true;
    bool pending = g.ClearWeapons != null && g.ClearWeapons.Contains(p1.Hero.Id);
    var reaper = (Weapon)Entity.FromCard(p1, C(Reaper));
    new SabberStoneCore.Tasks.SimpleTasks.WeaponTask().Process(g, p1, p1.Hero, null, new TaskStack { Playables = new List<IPlayable> { reaper } });
    g.DeathProcessingAndAuraUpdate();
    Check("W1 a weapon whose removal is pending (Game.ClearWeapons names the hero) is replaced before the death pass: the pass keeps the new weapon, the old one is in the graveyard",
        pending && p1.Hero.Weapon == reaper && reaper.Zone == null && p1.GraveyardZone.Contains(axe) && !p1.GraveyardZone.Contains(reaper) && g.ClearWeapons == null,
        $"pending {pending} weapon {p1.Hero.Weapon?.Card.Id ?? "none"} axe in graveyard {p1.GraveyardZone.Contains(axe)}");
    reaper.ToBeDestroyed = true;
    g.DeathProcessingAndAuraUpdate();
    Check("W2 with no replacement the pending removal still breaks the weapon at the death pass", p1.Hero.Weapon == null && p1.GraveyardZone.Contains(reaper) && g.ClearWeapons == null);
    var g2 = Ready(Plan());
    Controller q1 = g2.Player1, q2 = g2.Player2;
    PlayNew(g2, Axe);
    var axe2 = q1.Hero.Weapon;
    axe2[GameTag.DAMAGE] = 1;
    var foe = Board(g2, q2, "CS2_231"); // Wisp 1/1
    q1.Hero.IsExhausted = false;
    bool attacked = g2.Process(HeroAttackTask.Any(q1, foe));
    Check("W3 an ordinary attack with the last Durability point: the minion dies and the axe breaks (no weapon left)", attacked && foe.IsDead && q1.Hero.Weapon == null && q1.GraveyardZone.Contains(axe2),
        $"attacked {attacked} weapon {q1.Hero.Weapon?.Card.Id ?? "none"}");
});

// ============================================================ Q. a run never meets a boss row of its own character
Guard("Q", () =>
{
    // Every hero row whose character is also an encounter's boss, with a class row of the same class (Q5): the Heist villains
    // Squeamlish and Mr. Chu are the Tombs bosses Squeamlish and Mister Chu; George the Fallen is the George of the Kobolds
    // boss George and Karl (hearthstone.wiki.gg George_the_Fallen, Lore: "They later make an appearance as a boss in Dungeon Run").
    // Wave 6: the Heist villains Ol' Barkeye and Vessina are the Tombs bosses Ol' Barkeye and Vessina (hearthstone.wiki.gg
    // Ol'_Barkeye_(boss) and Vessina_(boss), Lore: "one of League of E.V.I.L.'s henchmen that made debut in The Dalaran Heist");
    // the Heist villain Tekahn (Dark Pharaoh Tekahn) is Tekahn, Plague Lord of Flame, family 24's finale row: no act pool holds
    // it, and a finale's fixed bosses ignore excludeBosses. The close-out enabled acts-v3t's finale on him and decided that a
    // DALA_Tekahn run never follows acts-v3t: its key (ulda_67_tekahn) takes acts-v3t out of its default roll (Q7-Q10).
    var pairs = new[] { (Hero: "DALA_Squeamlish", Boss: "ulda_07_squeamlish", Class: "DRUID"), (Hero: "DALA_Chu", Boss: "ulda_06_chu", Class: "WARRIOR"),
        (Hero: "DALA_George", Boss: "loot_15_george_karl", Class: "PALADIN"), (Hero: "DALA_Barkeye", Boss: "ulda_50_barkeye", Class: "HUNTER"),
        (Hero: "DALA_Vessina", Boss: "ulda_63_vessina", Class: "SHAMAN") };
    var finalePairs = new[] { (Hero: "DALA_Tekahn", Boss: "ulda_67_tekahn", Class: "WARLOCK", Schedule: "acts-v3t") }; // the close-out (Q7-Q10)
    var errors = DungeonRunValidator.Validate(data, null);
    Check("Q1 the shipped data validates", errors.Count == 0, string.Join(" | ", errors.Take(3)));
    var excluding = data.Classes.Where(c => c.ExcludeBosses.Count > 0).Select(c => c.Id + ":" + string.Join(",", c.ExcludeBosses)).OrderBy(x => x).ToList();
    Check("Q2 exactly the hero rows of a boss's character exclude that boss row (Squeamlish, Mr. Chu, George the Fallen; since wave 6 Ol' Barkeye and Vessina; since the close-out Tekahn, a finale row), each a hero row naming one encounter row",
        excluding.SequenceEqual(pairs.Select(p => p.Hero + ":" + p.Boss).Concat(finalePairs.Select(p => p.Hero + ":" + p.Boss)).OrderBy(x => x))
        && pairs.Select(p => (p.Hero, p.Boss)).Concat(finalePairs.Select(p => (p.Hero, p.Boss))).All(p => data.Class(p.Hero).IsHero && data.Boss(p.Boss).IsEncounter),
        string.Join(" ", excluding));
    DungeonRunData Copy() => DungeonRunData.Parse(R("dungeon-run/run-v1.json"), R("dungeon-run/pool-v1.json"), R("npc/decks-v1.json"), R("dungeon-run/catalog-v1.json"),
        R("dungeon-run/schedule-v1.json"), R("dungeon-run/tavern-v1.json"), R("dungeon-run/encounters-v1.json"));
    const int Seeds = 600;
    foreach (var (Hero, Boss, Class) in pairs)
    {
        var row = data.Class(Hero);
        var without = Copy();
        without.Class(Hero).ExcludeBosses.Clear();
        var runWithout = new DungeonRun(without);
        var cls = data.Class(Class);
        int met = 0, metWithout = 0, metClass = 0;
        for (long seed = 1; seed <= Seeds; seed++)
        {
            if (run.NewRun(all, Hero, row.Decks[0], row.Powers[0], seed, schedule: "acts-v3h").BossSequence.Contains(Boss)) met++;
            if (runWithout.NewRun(all, Hero, row.Decks[0], row.Powers[0], seed, schedule: "acts-v3h").BossSequence.Contains(Boss)) metWithout++;
            if (run.NewRun(all, Class, cls.Decks[0], cls.Powers[0], seed, schedule: "acts-v3h").BossSequence.Contains(Boss)) metClass++;
        }
        Check($"Q3 {Seeds} {Hero} runs on acts-v3h: none meets {Boss}", met == 0, "met in " + met);
        Check($"Q4 the same {Seeds} seeds with {Hero}'s exclusion removed: some run meets {Boss} (Q3 can fail)", metWithout > 0, "met in " + metWithout);
        Check($"Q5 the exclusion is the hero row's only: {Seeds} {Class} class-row runs meet {Boss} in some runs", metClass > 0, "met in " + metClass);
    }
    // Tekahn's row is the acts-v3t finale (tier finale): no act pool holds it, so no act draw meets it, and a finale's fixed
    // bosses are not filtered by excludeBosses. The close-out ships that finale, so the hero row's key works on the schedule:
    // the default roll of a DALA_Tekahn run leaves out every default whose finale fixes the excluded row, and NewRun refuses
    // such a schedule by name. A DALA_Tekahn run therefore always follows acts-v3h and meets the Heist finale.
    foreach (var (Hero, Boss, Class, Schedule) in finalePairs)
    {
        var row = data.Class(Hero);
        var cls = data.Class(Class);
        var naming = data.Schedules.Schedules.Values.Where(sc => sc.Acts.Any(x => x.IsFinale && x.FixedBosses.Contains(Boss))).Select(sc => sc.Id).OrderBy(x => x).ToList();
        var inPools = data.Schedules.Schedules.Values.Where(sc => sc.Acts.Any(x => !x.IsFinale && (x.Combat.Any(c => data.Schedules.Member(data.Boss(Boss), c.Pool))
            || data.Schedules.Member(data.Boss(Boss), x.MajorPool)))).Select(sc => sc.Id).ToList();
        var without = Copy();
        without.Class(Hero).ExcludeBosses.Clear();
        var runWithout = new DungeonRun(without);
        int met = 0, followed = 0, metWithout = 0, rolledWithout = 0, metClass = 0, classRolled = 0;
        for (long seed = 1; seed <= Seeds; seed++)
        {
            var s = run.NewRun(all, Hero, row.Decks[0], row.Powers[0], seed);
            if (s.BossSequence.Contains(Boss)) met++;
            if (s.Schedule == "acts-v3h" && s.ScheduleVersion == 3) followed++;
            var w = runWithout.NewRun(all, Hero, row.Decks[0], row.Powers[0], seed);
            if (w.BossSequence.Contains(Boss)) metWithout++;
            if (w.Schedule == Schedule) rolledWithout++;
            var c = run.NewRun(all, Class, cls.Decks[0], cls.Powers[0], seed);
            if (c.Schedule == Schedule) classRolled++;
            if (c.BossSequence.Contains(Boss)) metClass++;
        }
        Check($"Q7 {Boss} (the character of {Hero}) is a finale row in no act pool, and the fixed boss of the shipped finale of {Schedule} only; {Seeds} {Hero} new runs on the default roll all follow acts-v3h (stamped 3) and none meets it",
            data.Boss(Boss).Tier == "finale" && inPools.Count == 0 && naming.SequenceEqual(new[] { Schedule }) && data.Schedules.Defaults.Contains(Schedule)
            && row.ExcludeBosses.SequenceEqual(new[] { Boss }) && met == 0 && followed == Seeds,
            "met " + met + ", acts-v3h " + followed + ", pools " + string.Join(",", inPools) + ", finales " + string.Join(",", naming));
        Check($"Q8 the same {Seeds} seeds with {Hero}'s key removed: some runs roll {Schedule} and meet {Boss} in its finale (Q7 can fail)",
            metWithout > 0 && metWithout == rolledWithout, "met " + metWithout + ", rolled " + rolledWithout);
        Check($"Q9 the exclusion is the hero row's only: {Seeds} {Class} class-row new runs roll {Schedule} and meet {Boss} as often as {Hero} would without its key",
            classRolled > 0 && metClass == classRolled && classRolled == rolledWithout, "rolled " + classRolled + ", met " + metClass);
        string refusal = null;
        try { run.NewRun(all, Hero, row.Decks[0], row.Powers[0], 1, schedule: Schedule); } catch (InvalidOperationException e) { refusal = e.Message; }
        var named = run.NewRun(all, Class, cls.Decks[0], cls.Powers[0], 1, schedule: Schedule);
        var onV3h = run.NewRun(all, Hero, row.Decks[0], row.Powers[0], 1, schedule: "acts-v3h");
        Check($"Q10 NewRun refuses {Schedule} named for {Hero} (\"{DungeonRun.ExcludedFinale}\"), takes it for {Class} (its finale is {Boss}) and takes acts-v3h for {Hero}",
            refusal == DungeonRun.ExcludedFinale && named.BossSequence.Last() == Boss && onV3h.BossSequence.Last() == "dala_60_khadgar" && !onV3h.BossSequence.Contains(Boss),
            "refusal " + (refusal ?? "(none)"));
        // The validator keeps a default open to every hero row: with acts-v3t the only default, DALA_Tekahn would have none.
        var only = Copy();
        only.Schedules.Defaults.Clear(); only.Schedules.Defaults.Add(Schedule);
        var e3 = DungeonRunValidator.Validate(only, null);
        Check($"Q11 the validator refuses defaults whose every finale fixes a row a hero row excludes ({Schedule} alone: {Hero} has no schedule)",
            e3.Any(e => e.Contains(Hero) && e.Contains("the finale of every default schedule fixes an excluded boss row")), string.Join(" | ", e3.Take(3)));
    }
    var bogus = Copy();
    bogus.Class("DALA_Squeamlish").ExcludeBosses.Add("ulda_99_nobody");
    var classRow = Copy();
    classRow.Class("DRUID").ExcludeBosses.Add("ulda_07_squeamlish");
    var e1 = DungeonRunValidator.Validate(bogus, null); var e2 = DungeonRunValidator.Validate(classRow, null);
    Check("Q6 the validator refuses an excluded id that is no boss row, and excludeBosses on a class row", e1.Any(e => e.Contains("excludeBosses names unknown boss ulda_99_nobody"))
        && e2.Any(e => e.Contains("only a hero row may exclude a boss row")), string.Join(" | ", e1.Concat(e2).Take(3)));
});

// ============================================================ E. Unstable Evolution's copy and the Echo copy floor
Guard("E", () =>
{
    const string Millhouse = "NEW1_029", Evolution = "LOOT_504", Copy = "LOOT_504t", Giant = "GVG_121"; // Clockwork Giant: no minion costs (1) more
    var g = Ready(Plan());
    Controller p1 = g.Player1, p2 = g.Player2;
    ToBoss(g);
    PlayNew(g, Millhouse);
    ToPlayer(g);
    var giant = Board(g, p1, Giant);
    var evolution = ToHand(g, p1, Evolution);
    Check("E1 under Millhouse Manastorm (enemy spells cost (0) next turn) Unstable Evolution itself costs (0)", evolution.Cost == 0, evolution.Cost.ToString());
    p1.BaseMana = 10; p1.UsedMana = 7; p1.OverloadLocked = 0; p1.OverloadOwed = 0;
    bool played = g.Process(PlayCardTask.Any(p1, evolution, giant, -1, 0));
    var copy = p1.HandZone.FirstOrDefault(p => p.Card.Id == Copy);
    Check("E2 its hand copy costs (1), not (0): the Echo copy floor", played && copy != null && copy.Cost == 1, $"played {played} copy cost {copy?.Cost.ToString() ?? "none"}");
    int casts = 0;
    while (casts < 50)
    {
        var next = p1.HandZone.FirstOrDefault(p => p.Card.Id == Copy);
        if (next == null || next.Cost > p1.RemainingMana || g.State == State.COMPLETE) break;
        var target = p1.BoardZone.FirstOrDefault();
        if (target == null || !g.Process(PlayCardTask.Any(p1, next, target, -1, 0))) break;
        casts++;
    }
    Check("E3 with 3 Mana left the run player casts the copy three times, then the copy waits (no endless chain)", casts == 3 && p1.RemainingMana == 0, $"casts {casts} mana {p1.RemainingMana}");
});

// ============================================================ N. encounter notes agree with the merged decks
Guard("N", () =>
{
    var enc = (Dictionary<string, object>)NpcWorldJson.Parser(R("dungeon-run/encounters-v1.json"));
    var inDecks = new HashSet<string>(((Dictionary<string, object>)enc["decks"]).Values.SelectMany(d => ((List<object>)d).Cast<string>()));
    var idPattern = new System.Text.RegularExpressions.Regex(@"\b[A-Z][A-Za-z0-9]*_[A-Za-z0-9_]+\b");
    var notes = new List<string>();
    void Walk(object o) { if (o is Dictionary<string, object> d) { foreach (var kv in d) { if (kv.Key == "note" && kv.Value is string t) notes.Add(t); else Walk(kv.Value); } } else if (o is List<object> l) foreach (var x in l) Walk(x); }
    foreach (var f in Directory.GetFiles(Path.Combine(root, "dungeon-run/reference/encounters"), "F*.json").OrderBy(f => f, StringComparer.Ordinal))
        Walk(NpcWorldJson.Parser(File.ReadAllText(f)));
    var pending = notes.Where(t => t.Contains("no script yet")).ToList();
    var wrong = pending.SelectMany(t => idPattern.Matches(t).Select(m => m.Value)).Where(inDecks.Contains).Distinct().ToList();
    Check($"N1 no encounter note says a card has no script yet while an encounter deck holds it ({notes.Count} notes, {pending.Count} of them say so)",
        notes.Count > 100 && wrong.Count == 0, string.Join(",", wrong));
});

// ============================================================ O. Snowfury Giant counts every Overload (wave 6)
Guard("O", () =>
{
    const string Giant = "ICC_090", Moonfire = "CS2_008", Bolt = "EX1_238"; // Snowfury Giant (11); Moonfire (no Overload); Lightning Bolt (Overload 1)
    var plan = Plan(bossDeck: filler);
    plan.BossHero = C("ULDA_BOSS_63h"); plan.BossHeroPowerId = "ULDA_BOSS_63p"; // Vessina, Plaguebringer: spells (2) less, Overload (1) each
    var g = Ready(plan);
    Controller boss = g.Player2;
    ToBoss(g);
    var giant = ToHand(g, boss, Giant);
    var moon = ToHand(g, boss, Moonfire);
    int giant0 = giant.Cost, moonCost = moon.Cost;
    bool cast1 = Play(g, moon, g.Player1.Hero);
    int over1 = boss.OverloadThisGame, giant1 = giant.Cost;
    bool cast2 = PlayNew(g, Bolt, g.Player1.Hero);
    int over2 = boss.OverloadThisGame, giant2 = giant.Cost;
    Check("O1 under Plaguebringer a spell without printed Overload Overloads (1), and a Snowfury Giant in hand costs (1) less for it (11 - Overloaded this game)",
        giant0 == 11 && moonCost == 0 && cast1 && over1 == 1 && giant1 == 10, $"giant {giant0}->{giant1}, Moonfire cost {moonCost}, overloaded {over1}");
    Check("O2 a spell with printed Overload (1) then Overloads (2): the Giant counts both, never the printed amount twice (cost = 11 - Overloaded this game)",
        cast2 && over2 == 3 && giant2 == 8 && giant2 == 11 - over2, $"giant {giant1}->{giant2}, overloaded {over2}");
    var fresh = ToHand(g, boss, Giant); // its cost initialised from the game's count now, as a rebuilt entity's is
    Check("O3 a Giant that enters the hand now (its cost initialised from Overloaded this game) costs what the Giant held through the Overloads costs",
        fresh.Cost == giant.Cost && fresh.Cost == 11 - boss.OverloadThisGame, $"held {giant.Cost} new {fresh.Cost}");
});

Console.WriteLine($"RESULT {passes} passed, {failures} failed");
return failures;
