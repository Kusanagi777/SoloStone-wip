// content2-runlayer: the Dungeon Run rules over the class -> deck -> power model (docs/design/dungeon-run.md), headless.
// Compiles C:\SoloStone\plugin\DungeonRun\*.cs (minus the client-only runtime and boss pilot) and NativeDungeonRunText.cs
// against the rebuilt engine (content2-engine-out). Migration checks read SYNTHETIC version-1 sidecars written by
// content2-goldenv1 with the pre-change rules (content2-fixtures); no user data.
// usage: AiHarness <fixturesDir>
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.DungeonRun;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.PlayerTasks;

int failures = 0, passes = 0;
void Check(string name, bool ok, string detail = "")
{
    if (ok) passes++; else failures++;
    if (!ok || Environment.GetEnvironmentVariable("VERBOSE") == "1")
        Console.WriteLine((ok ? "PASS " : "FAIL ") + name + (detail.Length > 0 ? "  (" + detail + ")" : ""));
}
void Throws<T>(string name, Action action, string expectMessage = null) where T : Exception
{
    try { action(); Check(name, false, "did not throw"); }
    catch (T e) { Check(name, expectMessage == null || e.Message == expectMessage, e.Message); }
    catch (Exception e) { Check(name, false, e.GetType().Name + ": " + e.Message); }
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
string fixtures = args.Length > 0 ? args[0] : VerifyPaths.Fixture("content2-fixtures");
var data = DungeonRunData.Parse(File.ReadAllText(Path.Combine(root, "dungeon-run", "run-v1.json")),
    File.ReadAllText(Path.Combine(root, "dungeon-run", "pool-v1.json")), File.ReadAllText(Path.Combine(root, "npc", "decks-v1.json")));
var run = new DungeonRun(data);
string[] classIds = { "DRUID", "HUNTER", "MAGE", "PALADIN", "PRIEST", "ROGUE", "SHAMAN", "WARLOCK", "WARRIOR", "DEATHKNIGHT", "DEMONHUNTER" };

// ---------------------------------------------------------------- 1. data validates
var errors = DungeonRunValidator.Validate(data, null);
Check("data validates (client check not run headless)", errors.Count == 0, errors.Count + " errors: " + string.Join(" | ", errors.Take(12)));
// Phase 6 F1 adds the 14 Dungeon Run passive treasures (LOOTA_800 ... LOOTA_846, act runs only): 33 + 14 = 47; phase 6 F2 the
// 30 Dungeon Run active treasures (LOOTA_805 ... LOOTA_847 with the Quel'Delar pieces, act runs only): 47 + 30 = 77. Step 9
// (lane E, D9) adds GILA_BOSS_43t, Damph's encounter reward: a v4-only, never-offered row with no scope, so it loads without the
// catalog too (78); the other two v4 rows (ULDA_111, GILA_911) are scoped to catalog decks and stay out here.
// Build Deck (Task 3): each class row adds its build deck (no cards, never locked): 33 + 11 = 44 decks.
Check("11 classes, 44 decks (33 + 11 build decks), 20 bosses, 78 treasures (33 + phase 6 F1's 14 act-run passives + F2's 30 act-run actives + step 9's GILA_BOSS_43t reward)", data.Classes.Count == 11 && data.Decks.Count == 44 && data.Decks.Count(d => d.Build) == 11 && data.Bosses.Count == 20 && data.Treasures.Count == 78
    && data.Treasures.Count(t => t.Runs == DungeonRunTreasure.RunsAct) == 44 && data.Treasures.Count(t => t.Runs == DungeonRunTreasure.RunsAct && t.Active) == 30
    && data.Treasures.Where(t => t.Runs == DungeonRunTreasure.RunsV4).Select(t => t.Id + (t.Reward && !t.Offered ? ":reward" : "")).SequenceEqual(new[] { "GILA_BOSS_43t:reward" }),
    $"{data.Classes.Count}/{data.Decks.Count}/{data.Bosses.Count}/{data.Treasures.Count} v4 {string.Join(",", data.Treasures.Where(t => t.Runs == DungeonRunTreasure.RunsV4).Select(t => t.Id))}");
Check("classes are the 11 CardClass names in data order", data.Classes.Select(c => c.Id).SequenceEqual(classIds));
Check("class ladder expands to 44 keys + challenge", data.Unlocks.Count == 45 && classIds.All(c => new[] { "floor3", "floor4", "floor5", "win" }
    .All(step => data.Unlocks.TryGetValue(c.ToLowerInvariant() + "_" + step, out var cond) && cond.Class == c)), string.Join(",", data.Unlocks.Keys.Take(6)));
Check("data versions bumped (save version 6: phase 8 encounters and global rules)", data.DataVersion == "dungeon-run/2" && DungeonRunSave.CurrentVersion == 6, data.DataVersion);
// A build deck lists no cards (c-sched-9 BD9 checks it): the fixed decks are the 33 ten-card starters.
foreach (var deck in data.Decks.Where(d => !d.Build))
{
    var cls = (CardClass)Enum.Parse(typeof(CardClass), deck.Class);
    var bad = deck.Cards.Where(id => !CardClassRules.IsAllowedInDeck(Cards.FromId(id), cls)).ToList();
    Check($"deck {deck.Id}: 10 cards, class-legal ({deck.Class}), validator-clean", deck.Cards.Count == 10 && bad.Count == 0
        && !errors.Any(e => e.Contains("deck " + deck.Id)), string.Join(",", bad));
}
{
    // A one-card corruption of a deck is caught (the validator really checks decks).
    var broken = DungeonRunData.Parse(File.ReadAllText(Path.Combine(root, "dungeon-run", "run-v1.json")),
        File.ReadAllText(Path.Combine(root, "dungeon-run", "pool-v1.json")), File.ReadAllText(Path.Combine(root, "npc", "decks-v1.json")));
    broken.Decks[0].Cards[0] = "CS2_024";           // Frostbolt in a Druid deck
    broken.Decks[1].Cards.RemoveAt(0);               // 9 cards
    broken.Classes[2].Powers[1] = "shrine_dawn_1";   // a passive as a player power
    var found = DungeonRunValidator.Validate(broken, null);
    Check("validator catches off-class card, short deck, passive player power",
        found.Any(e => e.StartsWith("CS2_024") && e.Contains("not allowed in DRUID")) && found.Any(e => e.Contains("has 9 cards"))
        && found.Any(e => e.Contains("shrine_dawn_1") && e.Contains("PassivePower")), string.Join(" | ", found.Take(6)));
}

// ---------------------------------------------------------------- 2. fresh profile, refusals
var fresh = new DungeonRunProfile();
foreach (var cls in data.Classes)
    // The build deck is never locked, so a fresh profile has it beside the default deck.
    Check($"fresh {cls.Id}: only the default deck, the build deck and the basic power", run.AvailableDecks(cls, fresh).Select(d => d.Id).SequenceEqual(new[] { cls.Decks[0], cls.BuildDeck(data).Id })
        && run.AvailablePowers(cls, fresh).SequenceEqual(new[] { cls.Powers[0] }));
Throws<InvalidOperationException>("locked deck refused", () => run.NewRun(fresh, "MAGE", "mage_kindled", "CS2_034", 1, signatureId: run.SignatureOffer(fresh, run.Data.FindClass("MAGE"), 1).FirstOrDefault()), DungeonRun.LockedDeck);
Throws<InvalidOperationException>("locked power refused", () => run.NewRun(fresh, "MAGE", "mage_rimeward", "mage_rimebite", 1, signatureId: run.SignatureOffer(fresh, run.Data.FindClass("MAGE"), 1).FirstOrDefault()), DungeonRun.LockedPower);
Throws<InvalidOperationException>("deck of another class refused", () => run.NewRun(fresh, "MAGE", "warrior_plated", "CS2_034", 1, signatureId: run.SignatureOffer(fresh, run.Data.FindClass("MAGE"), 1).FirstOrDefault()), DungeonRun.OtherClassDeck);
Throws<ArgumentException>("unknown class refused", () => run.NewRun(fresh, "BARD", "mage_rimeward", "CS2_034", 1, signatureId: run.SignatureOffer(fresh, run.Data.FindClass("BARD"), 1).FirstOrDefault()));
Throws<ArgumentException>("unknown deck refused", () => run.NewRun(fresh, "MAGE", "mage_nope", "CS2_034", 1, signatureId: run.SignatureOffer(fresh, run.Data.FindClass("MAGE"), 1).FirstOrDefault()));
Throws<ArgumentException>("power of another class refused", () => run.NewRun(fresh, "MAGE", "mage_rimeward", "CS2_102", 1, signatureId: run.SignatureOffer(fresh, run.Data.FindClass("MAGE"), 1).FirstOrDefault()));
Throws<ArgumentException>("null power refused", () => run.NewRun(fresh, "MAGE", "mage_rimeward", null, 1, signatureId: run.SignatureOffer(fresh, run.Data.FindClass("MAGE"), 1).FirstOrDefault()));

// ---------------------------------------------------------------- 3. every class x deck x power starts and builds a plan
var all = new DungeonRunProfile(); all.Unlocked.AddRange(data.Unlocks.Keys);
int combos = 0, plans = 0;
// A build deck has no cards to start a run on (R2): every deck but the build deck.
foreach (var cls in data.Classes)
    foreach (string deckId in cls.StartDecks(data))
        foreach (string power in cls.Powers)
        {
            combos++;
            try
            {
                var s = run.NewRun(all, cls.Id, deckId, power, 42 + combos, signatureId: run.SignatureOffer(all, run.Data.FindClass(cls.Id), 42 + combos).FirstOrDefault());
                var deck = data.Deck(deckId);
                var seq = s.BossSequence.Select(data.Boss).ToList();
                bool bands = seq.Take(3).All(b => b.Band == 1) && seq.Skip(3).Take(3).All(b => b.Band == 2) && seq[6].Band == 3 && seq[7].Final;
                var plan = run.BuildPlan(s);
                bool ok = s.Class == cls.Id && s.StarterDeck == deckId && s.Power == power && s.Shrine == null && s.Signature == null && s.Hero == null
                    && s.Deck.SequenceEqual(deck.Cards) && s.Deck.Count == 10 && bands && s.BossSequence.Distinct().Count() == 8
                    && plan.PlayerHero.Id == cls.HeroCard && plan.PlayerHeroPowerId == power
                    && plan.PlayerDeck.Select(c => c.Id).SequenceEqual(deck.Cards)
                    && Cards.FromId(DungeonRunSetup.PowerCardId(power)).Type == CardType.HERO_POWER;
                s.Deck.Add("CS2_024");
                ok &= deck.Cards.Count == 10;
                if (ok) plans++; else Check($"combo {cls.Id}/{deckId}/{power}", false, $"deck {s.Deck.Count} power {plan.PlayerHeroPowerId} hero {plan.PlayerHero.Id}");
            }
            catch (Exception e) { Check($"combo {cls.Id}/{deckId}/{power}", false, e.GetType().Name + ": " + e.Message); }
        }
Check($"all {combos} class/deck/power combos: NewRun + BuildPlan, deck is a copy (99 expected)", combos == 99 && plans == 99, $"{plans}/{combos}");

// ---------------------------------------------------------------- 4. seeded determinism (AC1)
DungeonRunState NewFor(DungeonRunProfile p, string cls, long seed, string deck = null, string power = null, bool typed = false)
{
    var c = data.Class(cls);
    return run.NewRun(p, cls, deck ?? c.DefaultDeck, power ?? c.DefaultPower, seed, typed, signatureId: run.SignatureOffer(p, run.Data.FindClass(cls), seed).FirstOrDefault());
}
{
    var a = NewFor(all, "WARRIOR", 123456789); var b = NewFor(all, "WARRIOR", 123456789); var c = NewFor(all, "WARRIOR", 987654321);
    Check("same seed: same boss sequence", a.BossSequence.SequenceEqual(b.BossSequence));
    Check("different seed: different sequence", !a.BossSequence.SequenceEqual(c.BossSequence));
    Check("match seed fixed per floor", run.MatchSeed(a) == run.MatchSeed(b));
}

// ---------------------------------------------------------------- 5. a full winning run with drafts, upgrades and deck growth
DungeonRunState PlayOut(DungeonRunProfile profile, DungeonRunState s, Func<DungeonRunState, int, DungeonRunOutcome> outcome,
    Func<DungeonRunDraft, string> pickTreasure = null, List<string> log = null)
{
    while (!s.Finished)
    {
        if (s.Phase == DungeonRunPhase.PreMatch)
        {
            int baseSize = data.Rules.StarterSize + 3 * (s.Floor - 1);
            if (s.Deck.Count != baseSize + s.ActiveTreasures.Count) log?.Add($"floor {s.Floor}: deck {s.Deck.Count}, expected {baseSize}+{s.ActiveTreasures.Count}");
            run.BeginMatch(s);
            run.RecordOutcome(s, profile, outcome(s, s.Floor));
        }
        else if (s.Phase == DungeonRunPhase.TreasureDraft) run.PickTreasure(s, (pickTreasure ?? (d => d.Offered[0]))(s.OpenDraft));
        else if (s.Phase == DungeonRunPhase.BundleDraft) run.PickBundle(s, s.OpenDraft.Offered[s.Floor % s.OpenDraft.Offered.Count]);
    }
    return s;
}
{
    var profile = new DungeonRunProfile();
    var log = new List<string>();
    var s = PlayOut(profile, NewFor(profile, "WARRIOR", 2024), (_, __) => DungeonRunOutcome.Win, d => d.Offered.Contains("charm_seedling") ? "charm_seedling" : d.Offered[0], log);
    Check("winning run ends in Victory on floor 8", s.Phase == DungeonRunPhase.Victory && s.Floor == 8);
    Check("deck = 10 + 3(f-1) + held active treasures on every floor (AC2)", log.Count == 0, string.Join("; ", log));
    var treasureDrafts = s.Drafts.Where(d => d.Kind == "treasure").ToList();
    Check("treasure drafts on floors 1, 3, 5 only (AC3)", treasureDrafts.Select(d => d.Floor).SequenceEqual(new[] { 1, 3, 5 }));
    Check("treasure offers have 3 distinct, never repeated", treasureDrafts.All(d => d.Offered.Count == 3 && d.Offered.Distinct().Count() == 3)
        && treasureDrafts.SelectMany(d => d.Offered).Distinct().Count() == 9);
    Check("bundle drafts on floors 1-7", s.Drafts.Where(d => d.Kind == "bundle").Select(d => d.Floor).SequenceEqual(Enumerable.Range(1, 7)));
    Check("bundles offered: Warrior or neutral only", s.Drafts.Where(d => d.Kind == "bundle").SelectMany(d => d.Offered).All(id => data.Bundles.First(b => b.Id == id).Class is "WARRIOR" or "NEUTRAL"));
    Check("a win unlocks the whole Warrior ladder and nothing else", profile.Unlocked.OrderBy(k => k).SequenceEqual(new[] { "warrior_floor3", "warrior_floor4", "warrior_floor5", "warrior_win" }),
        string.Join(",", profile.Unlocked));
    Check("UnlockedThisRun lists them", s.UnlockedThisRun.OrderBy(k => k).SequenceEqual(profile.Unlocked.OrderBy(k => k)));
    var row = profile.Heroes.Single();
    var entry = profile.Archive.Single();
    Check("stats by class, archive records class/deck/power", row.Class == "WARRIOR" && row.Hero == null && row.Wins == 1 && row.BestFloor == 8
        && entry.Class == "WARRIOR" && entry.StarterDeck == "warrior_plated" && entry.Power == "CS2_102" && entry.Hero == null);
}
{
    DungeonRunState s = null; long seed = 1;
    for (; seed < 400; seed++)
    {
        var probe = NewFor(all, "WARRIOR", seed); run.BeginMatch(probe); run.RecordOutcome(probe, new DungeonRunProfile(), DungeonRunOutcome.Win);
        if (probe.OpenDraft.Offered.Contains("charm_seedling")) { s = probe; break; }
    }
    Check("found a seed offering Seedling Charm", s != null, "seed " + seed);
    if (s != null)
    {
        var profile = new DungeonRunProfile(); var stages = new List<string>();
        run.PickTreasure(s, "charm_seedling");
        while (!s.Finished)
        {
            if (s.Phase == DungeonRunPhase.PreMatch) { stages.Add($"{s.Floor}:{s.ActiveTreasures.FirstOrDefault(a => a.StartsWith("charm"))}"); run.BeginMatch(s); run.RecordOutcome(s, profile, DungeonRunOutcome.Win); }
            else if (s.Phase == DungeonRunPhase.TreasureDraft) run.PickTreasure(s, s.OpenDraft.Offered[0]);
            else run.PickBundle(s, s.OpenDraft.Offered[0]);
        }
        Check("upgrade: seedling -> sprouting after floor 3 -> blooming after floor 5",
            stages.Contains("3:charm_seedling") && stages.Contains("4:charm_sprouting") && stages.Contains("6:charm_blooming"), string.Join(" ", stages));
        Check("upgraded card is in the deck", s.Deck.Contains(DungeonRunContent.Get("charm_blooming").ShellId) && !s.Deck.Contains(DungeonRunContent.Get("charm_seedling").ShellId));
    }
}
{
    var profile = new DungeonRunProfile();
    var s = PlayOut(profile, NewFor(profile, "MAGE", 77), (st, f) => f == 3 ? DungeonRunOutcome.Tie : DungeonRunOutcome.Win);
    Check("tie is a defeat (R9, AC4)", s.Phase == DungeonRunPhase.Defeat && s.Floor == 3);
    var s2 = PlayOut(profile, NewFor(profile, "MAGE", 78), (st, f) => f == 2 && st.Attempt == 1 ? DungeonRunOutcome.Fault : f == 4 ? DungeonRunOutcome.Concede : DungeonRunOutcome.Win);
    Check("fault restages the floor, concede ends the run", s2.Phase == DungeonRunPhase.Defeat && s2.Floor == 4 && s2.Matches.Count(m => m.Floor == 2) == 2);
    var typed = new DungeonRunProfile();
    var t = NewFor(typed, "WARRIOR", 5, typed: true);
    run.BeginMatch(t); run.RecordOutcome(t, typed, DungeonRunOutcome.Loss);
    Check("typed seed: no personal best, no unlocks", typed.Heroes.Single().BestFloor == 0 && typed.Unlocked.Count == 0 && t.UnlockedThisRun.Count == 0);
    var typedWin = new DungeonRunProfile();
    PlayOut(typedWin, NewFor(typedWin, "WARRIOR", 6, typed: true), (_, __) => DungeonRunOutcome.Win);
    var later = NewFor(typedWin, "WARRIOR", 7); run.BeginMatch(later); run.RecordOutcome(later, typedWin, DungeonRunOutcome.Loss);
    Check("typed-seed win feeds no stats or unlocks", typedWin.Heroes.Single().Wins == 0 && !typedWin.Unlocked.Contains("warrior_win"),
        $"wins={typedWin.Heroes.Single().Wins} unlocked={string.Join(",", typedWin.Unlocked)}");
}
{
    var s = NewFor(all, "WARRIOR", 4242);
    run.BeginMatch(s); run.RecordOutcome(s, new DungeonRunProfile(), DungeonRunOutcome.Win);
    if (s.Phase == DungeonRunPhase.TreasureDraft) run.PickTreasure(s, s.OpenDraft.Offered[0]);
    run.PickBundle(s, s.OpenDraft.Offered[0]);
    Check("first bundle draft does not count against pity", s.DraftsWithoutDominant == 0, $"{s.DraftsWithoutDominant}");
}

// ---------------------------------------------------------------- 6. the class unlock ladder
{
    DungeonRunProfile Ended(int floor, bool win = false)
    {
        var p = new DungeonRunProfile();
        PlayOut(p, NewFor(p, "MAGE", 9100 + floor), (st, f) => win ? DungeonRunOutcome.Win : f == floor ? DungeonRunOutcome.Loss : DungeonRunOutcome.Win);
        return p;
    }
    var f2 = Ended(2); var f3 = Ended(3); var f4 = Ended(4); var f5 = Ended(5); var w = Ended(8, true);
    Check("Mage run ending on floor 2 unlocks nothing", f2.Unlocked.Count == 0, string.Join(",", f2.Unlocked));
    Check("ending on floor 3 unlocks exactly mage_floor3 (deck 2)", f3.Unlocked.SequenceEqual(new[] { "mage_floor3" }), string.Join(",", f3.Unlocked));
    Check("floor 4 adds mage_floor4 (power 2)", f4.Unlocked.OrderBy(k => k).SequenceEqual(new[] { "mage_floor3", "mage_floor4" }), string.Join(",", f4.Unlocked));
    Check("floor 5 adds mage_floor5 (deck 3)", f5.Unlocked.OrderBy(k => k).SequenceEqual(new[] { "mage_floor3", "mage_floor4", "mage_floor5" }), string.Join(",", f5.Unlocked));
    Check("a win unlocks all four Mage keys", w.Unlocked.OrderBy(k => k).SequenceEqual(new[] { "mage_floor3", "mage_floor4", "mage_floor5", "mage_win" }), string.Join(",", w.Unlocked));
    var mage = data.Class("MAGE");
    Check("floor 3: Kindled Sparks available, power list unchanged", run.AvailableDecks(mage, f3).Select(d => d.Id).SequenceEqual(new[] { "mage_rimeward", "mage_kindled", "mage_build" })
        && run.AvailablePowers(mage, f3).SequenceEqual(new[] { "CS2_034" }));
    Check("floor 4: Survey available", run.AvailablePowers(mage, f4).SequenceEqual(new[] { "CS2_034", "carto_survey" }));
    Check("win: all 3 decks (and the never-locked build deck) and 3 powers", run.AvailableDecks(mage, w).Count() == 4 && run.AvailablePowers(mage, w).Count() == 3);
    Check("Mage progress opens nothing for another class (its default and build decks only)", data.Classes.Where(c => c.Id != "MAGE").All(c => run.AvailableDecks(c, w).Count() == 2 && run.AvailablePowers(c, w).Count() == 1));
    Check("a newly unlocked deck starts a run", run.NewRun(f3, "MAGE", "mage_kindled", "CS2_034", 5, signatureId: run.SignatureOffer(f3, run.Data.FindClass("MAGE"), 5).FirstOrDefault()).Deck.Count == 10);
}
{
    var p = new DungeonRunProfile(); p.Unlocked.AddRange(data.Unlocks.Keys.Where(k => k != "challenge"));
    bool seenWithout = Enumerable.Range(0, 200).Any(i => run.BossSequence(p, i).Last() == "hollow_sovereign");
    p.Unlocked.Add("challenge");
    bool seenWith = Enumerable.Range(0, 200).Any(i => run.BossSequence(p, i).Last() == "hollow_sovereign");
    Check("challenge boss only after its unlock", !seenWithout && seenWith);
    var c = new DungeonRunProfile();
    foreach (var cls in new[] { "DRUID", "HUNTER", "MAGE" }) c.StatsFor(cls).Wins = 1;
    c.StatsFor("PRIEST").Wins = 0; c.StatsFor("PRIEST").BestFloor = 8;
    run.RefreshUnlocks(c);
    Check("wins with 3 classes: challenge still locked", !c.Unlocked.Contains("challenge"));
    c.StatsFor("MAGE").Wins = 3; run.RefreshUnlocks(c);
    Check("more wins with the same classes: still locked", !c.Unlocked.Contains("challenge"));
    var typedP = new DungeonRunProfile();
    foreach (var cls in new[] { "DRUID", "HUNTER", "MAGE" }) typedP.StatsFor(cls).Wins = 1;
    PlayOut(typedP, NewFor(typedP, "ROGUE", 31, typed: true), (_, __) => DungeonRunOutcome.Win);
    Check("a typed-seed win with a 4th class does not count", !typedP.Unlocked.Contains("challenge"));
    var s = PlayOut(c, NewFor(c, "ROGUE", 32), (_, __) => DungeonRunOutcome.Win);
    Check("a 4th class's random-seed win unlocks the challenge", c.Unlocked.Contains("challenge") && s.UnlockedThisRun.Contains("challenge"));
    Check("hint: challenge", DungeonRunCatalog.Hint(run, "challenge") == "Win runs with 4 different classes to unlock", DungeonRunCatalog.Hint(run, "challenge"));
    Check("summary names: deck, power, boss", DungeonRunCatalog.UnlockName(run, "mage_floor3") == "Kindled Sparks (Mage deck)"
        && DungeonRunCatalog.UnlockName(run, "mage_floor4") == "Survey (Mage power)" && DungeonRunCatalog.UnlockName(run, "challenge") == "The Hollow Sovereign (challenge boss)",
        DungeonRunCatalog.UnlockName(run, "mage_floor4"));
}

// ---------------------------------------------------------------- 7. treasure scope (keen_eye, hunting_net, signatures)
{
    var combosList = data.Classes.SelectMany(c => c.Decks.SelectMany(d => c.Powers.Select(p => (cls: c.Id, deck: d, power: p)))).ToList();
    var keenWith = new HashSet<string>(); var netWith = new HashSet<string>(); bool sig = false; int drafts = 0;
    for (int seed = 0; seed < 10000; seed++)
    {
        var (cls, deck, power) = combosList[seed % combosList.Count];
        var s = new DungeonRunState { Seed = seed * 7919L + 13, Class = cls, StarterDeck = deck, Power = power, Floor = 1 + 2 * (seed % 3) };
        if (!run.OpenTreasureDraft(s)) continue;
        drafts++;
        var offer = s.OpenDraft.Offered;
        if (offer.Contains("keen_eye")) keenWith.Add(power + "|" + deck);
        if (offer.Contains("hunting_net")) netWith.Add(power + "|" + deck);
        if (offer.Any(o => o.StartsWith("sig_"))) sig = true;
    }
    bool keenOk = keenWith.All(k => k.StartsWith("trapper_snare|") || k.StartsWith("mage_rimebite|") || k.EndsWith("|mage_rimeward"));
    Check($"keen_eye offered only with Set Snare, Rimebite or Rimeward ({drafts} drafts)", keenOk && keenWith.Count > 0, string.Join(" ", keenWith.Take(8)));
    Check("keen_eye reaches all three scopes", keenWith.Any(k => k.StartsWith("trapper_snare|")) && keenWith.Any(k => k.StartsWith("mage_rimebite|"))
        && keenWith.Any(k => k.StartsWith("CS2_034|mage_rimeward")));
    Check("hunting_net offered only with Set Snare", netWith.Count > 0 && netWith.All(k => k.StartsWith("trapper_snare|")), string.Join(" ", netWith));
    Check("signature cards are never offered", !sig);
}

// ---------------------------------------------------------------- 8. save, load, resume (R11, AC5)
{
    string dir = Path.Combine(Path.GetTempPath(), "dr-save-" + Guid.NewGuid().ToString("N"));
    var save = new DungeonRunSave();
    save.Active = NewFor(all, "PALADIN", 31337, "paladin_lantern", "paladin_lantern_march");
    run.BeginMatch(save.Active);
    DungeonRunStore.Save(dir, save);
    var loaded = DungeonRunStore.Load(dir);
    Check("save round-trips (v6; a legacy run writes no CatalogVersion)", loaded.Version == 6 && loaded.LoadedVersion == 6 && loaded.Active.CatalogVersion == 0 && loaded.Active.Seed == 31337 && loaded.Active.Class == "PALADIN"
        && loaded.Active.StarterDeck == "paladin_lantern" && loaded.Active.Power == "paladin_lantern_march" && loaded.Active.Deck.SequenceEqual(save.Active.Deck)
        && loaded.Active.Phase == DungeonRunPhase.InMatch);
    run.Resume(loaded.Active);
    Check("mid-match load resumes at pre-match, same match seed", loaded.Active.Phase == DungeonRunPhase.PreMatch && run.MatchSeed(loaded.Active) == run.MatchSeed(save.Active));
    DungeonRunStore.Save(dir, loaded);
    Check("second save replaces the file, no backup for a v3 save", File.Exists(Path.Combine(dir, DungeonRunStore.FileName))
        && !File.Exists(Path.Combine(dir, DungeonRunStore.FileName + ".tmp")) && !File.Exists(Path.Combine(dir, DungeonRunStore.V1BackupName))
        && !File.Exists(Path.Combine(dir, DungeonRunStore.BackupName(2))) && !File.Exists(Path.Combine(dir, DungeonRunStore.BackupName(3))));
    Directory.Delete(dir, true);
}

// ---------------------------------------------------------------- 9. migration of synthetic version-1 sidecars
var migrationLog = new List<string>();
(DungeonRunSave save, string dir) LoadFixture(string name)
{
    string dir = Path.Combine(Path.GetTempPath(), "dr-mig-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir);
    File.Copy(Path.Combine(fixtures, name, DungeonRunStore.FileName), Path.Combine(dir, DungeonRunStore.FileName));
    return (DungeonRunStore.Load(dir), dir);
}
DungeonRunSave Migrated(string name)
{
    var (save, dir) = LoadFixture(name);
    Directory.Delete(dir, true);
    DungeonRunMigration.Upgrade(save, run, migrationLog.Add);
    if (save.Active != null) run.Resume(save.Active);
    return save;
}
List<string> Replay(DungeonRunSave save)
{
    var copy = DungeonRunStore.Clone(save);
    var s = copy.Active;
    var lines = new List<string>();
    var plan = run.BuildPlan(s);
    lines.Add($"plan hero={plan.PlayerHero.Id} power={plan.PlayerHeroPowerId} deck={string.Join(",", plan.PlayerDeck.Select(c => c.Id))} boss={run.CurrentBoss(s).Id} matchSeed={run.MatchSeed(s)}");
    int guard = 0;
    while (!s.Finished && guard++ < 100)
    {
        if (s.Phase == DungeonRunPhase.PreMatch) { lines.Add($"floor {s.Floor} boss={run.CurrentBoss(s).Id} power={run.PlayerPower(s)} matchSeed={run.MatchSeed(s)}"); run.BeginMatch(s); run.RecordOutcome(s, copy.Profile, DungeonRunOutcome.Win); }
        else if (s.Phase == DungeonRunPhase.TreasureDraft) { lines.Add($"floor {s.Floor} treasure offered={string.Join(",", s.OpenDraft.Offered)}"); run.PickTreasure(s, s.OpenDraft.Offered[0]); }
        else if (s.Phase == DungeonRunPhase.BundleDraft) { lines.Add($"floor {s.Floor} bundle offered={string.Join(",", s.OpenDraft.Offered)}"); run.PickBundle(s, s.OpenDraft.Offered[0]); }
    }
    lines.Add($"end phase={s.Phase} deck={s.Deck.Count} treasures={string.Join(",", s.Treasures.Concat(s.ActiveTreasures))}");
    return lines;
}
string Diff(List<string> a, string[] b)
{
    for (int i = 0; i < Math.Max(a.Count, b.Length); i++)
        if (i >= a.Count || i >= b.Length || a[i] != b[i]) return $"line {i + 1}: got '{(i < a.Count ? a[i] : "")}' want '{(i < b.Length ? b[i] : "")}'";
    return "";
}
string NoPortrait(string line) => System.Text.RegularExpressions.Regex.Replace(line, @"^plan hero=\S+", "plan hero=*");
var heroClass = new Dictionary<string, string> { ["wilder"] = "DRUID", ["ranger"] = "HUNTER", ["spellwright"] = "MAGE", ["vowkeeper"] = "PALADIN",
    ["mender"] = "PRIEST", ["cutpurse"] = "ROGUE", ["stormcaller"] = "SHAMAN", ["hexer"] = "WARLOCK", ["brawler"] = "WARRIOR" };
foreach (var pair in heroClass)
{
    var (v1, dir) = LoadFixture("class-" + pair.Key); Directory.Delete(dir, true);
    var deckBefore = v1.Active.Deck.ToList(); string power = v1.Active.Power;
    var save = Migrated("class-" + pair.Key);
    var golden = File.ReadAllLines(Path.Combine(fixtures, "class-" + pair.Key, "golden.txt"));
    var replay = Replay(save);
    Check($"migrated {pair.Key} run: class {pair.Value}, deck and power kept, no starter id, every offer as in version 1",
        save.Version == 6 && save.Active.CatalogVersion == 0 && save.Active.Class == pair.Value && save.Active.StarterDeck == null && save.Active.Power == power && save.Active.Hero == pair.Key
        && save.Active.Deck.SequenceEqual(deckBefore) && save.Active.Floor == 3 && replay.SequenceEqual(golden), Diff(replay, golden));
}
foreach (var name in new[] { "trapper", "pilgrim-bulwark", "sellsword-warcry" })
{
    var save = Migrated(name);
    var golden = File.ReadAllLines(Path.Combine(fixtures, name, "golden.txt")).Select(NoPortrait).ToArray();
    var replay = Replay(save).Select(NoPortrait).ToList();
    Check($"migrated {name} run: every offer as in version 1 (portrait now the class's)", replay.SequenceEqual(golden), Diff(replay, golden));
}
{
    var save = Migrated("trapper");
    Check("Trapper run: HUNTER, Set Snare kept, plan on the Hunter portrait", save.Active.Class == "HUNTER" && save.Active.Power == "trapper_snare"
        && run.BuildPlan(save.Active).PlayerHero.Id == "HERO_05");
    var p = Migrated("pilgrim-bulwark").Active;
    int floor = p.Floor;
    bool tiers = run.PlayerPower(p) == "shrine_bulwark_2";
    p.Floor = 1; tiers &= run.PlayerPower(p) == "shrine_bulwark_1";
    p.Floor = 8; tiers &= run.PlayerPower(p) == "shrine_bulwark_3";
    p.Floor = floor;
    Check("Pilgrim run: PALADIN, shrine kept and tiered by band, plan builds", p.Class == "PALADIN" && p.Power == null && p.Shrine == "shrine_bulwark" && tiers
        && run.BuildPlan(p).PlayerHeroPowerId == "shrine_bulwark_2");
    var c = Migrated("cartographer-ambush");
    var plan = run.BuildPlan(c.Active);
    var offers = Replay(c);
    Check("Cartographer run: MAGE, Ambush Point and Field Atlas kept, plan builds", c.Active.Class == "MAGE" && c.Active.Power == "carto_ambush"
        && c.Active.Signature == "sig_field_atlas" && c.Active.ActiveTreasures.Contains("sig_field_atlas")
        && c.Active.Deck.Contains(DungeonRunContent.Get("sig_field_atlas").ShellId) && plan.PlayerHeroPowerId == "carto_ambush" && plan.PlayerHero.Id == "HERO_08");
    Check("Cartographer run: no Rogue bundles any more (single class)", offers.Where(l => l.Contains("bundle offered=")).All(l => !l.Contains("bundle_rogue")),
        string.Join(" | ", offers.Where(l => l.Contains("bundle"))));
    var h = Migrated("hexer-inmatch").Active;
    Check("a run saved mid-match resumes at pre-match after the upgrade", h.Class == "WARLOCK" && h.Phase == DungeonRunPhase.PreMatch && run.BuildPlan(h) != null);
}
{
    var save = Migrated("finished-spellwright-floor3");
    var row = save.Profile.Heroes.Single();
    Check("finished v1 profile: one MAGE row (runs 1, best floor 3), archive gets its class", row.Class == "MAGE" && row.Hero == null && row.Runs == 1 && row.BestFloor == 3
        && save.Profile.Archive.Single().Class == "MAGE" && save.Profile.Archive.Single().Hero == "spellwright" && save.Active == null);
    Check("progress carries over: best Mage floor 3 grants mage_floor3 only", save.Profile.Unlocked.SequenceEqual(new[] { "mage_floor3" }), string.Join(",", save.Profile.Unlocked));
}
{
    var (v1, dir) = LoadFixture("finished-merge"); Directory.Delete(dir, true);
    var before = v1.Profile.Heroes.ToDictionary(r => r.Hero);
    var save = Migrated("finished-merge");
    var hunter = save.Profile.Heroes.Single(r => r.Class == "HUNTER");
    Check("Ranger + Trapper merge into one HUNTER row (runs and wins add, best floor max)",
        hunter.Runs == before["ranger"].Runs + before["trapper"].Runs && hunter.Wins == before["ranger"].Wins + before["trapper"].Wins
        && hunter.BestFloor == Math.Max(before["ranger"].BestFloor, before["trapper"].BestFloor) && save.Profile.Heroes.Count == 3
        && save.Profile.Heroes.All(r => r.Hero == null && r.Archetype == null), string.Join(" ", save.Profile.Heroes.Select(r => $"{r.Class}:{r.Runs}/{r.Wins}/{r.BestFloor}")));
    var u = new HashSet<string>(save.Profile.Unlocked);
    var expectedLegacy = new[] { "hunter_floor4", "hunter_floor5", "paladin_floor5", "warrior_floor5", "mage_floor4", "mage_floor5", "rogue_floor5", "warrior_floor4", "warrior_win" };
    Check("version-1 unlocks keep what they gave (chapter2/3, sellsword keys) and old keys stay", expectedLegacy.All(u.Contains)
        && new[] { "chapter2", "chapter3", "sellsword_floor5", "sellsword_win" }.All(u.Contains), string.Join(",", save.Profile.Unlocked));
    Check("merged stats unlock the full Hunter and Warrior ladders, Paladin none by stats", new[] { "hunter_floor3", "hunter_win", "warrior_floor3" }.All(u.Contains)
        && !u.Contains("paladin_floor3") && !u.Contains("challenge"));
}
{
    migrationLog.Clear();
    var save = Migrated("unknown-hero");
    var row = save.Profile.Heroes.Single();
    Check("unknown hero: run class from its deck; its stats row keeps the hero id and no class (not credited to Druid); logged, no throw",
        save.Active.Class == "MAGE" && save.Version == 6 && row.Class == null && row.Hero == "bogus"
        && migrationLog.Any(l => l.Contains("unknown hero bogus")), string.Join(" | ", migrationLog));
    Check("unknown hero: no class ladder key from its stats (no druid_*), and a later Druid run gets its own row",
        !save.Profile.Unlocked.Any(k => k.StartsWith(data.Classes[0].Id.ToLowerInvariant() + "_")) && save.Profile.StatsFor("DRUID") != row
        && save.Profile.Heroes.Count == 2, string.Join(",", save.Profile.Unlocked));
}
{
    // Review 2 defect 3: a failed active-run step leaves the save at version 1 (the upgrade retries), and a run with a class the
    // data does not know can still be abandoned: archived, no stats row.
    migrationLog.Clear();
    var (v1, dir) = LoadFixture("class-spellwright"); Directory.Delete(dir, true);
    var deck = v1.Active.Deck; v1.Active.Deck = null;
    bool upgraded = DungeonRunMigration.Upgrade(v1, run, migrationLog.Add);
    Check("active-run step fails -> the save stays at version 1, logged", upgraded && v1.Version == 1
        && migrationLog.Any(l => l.Contains("active run failed")) && migrationLog.Any(l => l.Contains("stays at version 1")), string.Join(" | ", migrationLog));
    v1.Active.Deck = deck;
    bool retried = DungeonRunMigration.Upgrade(v1, run, migrationLog.Add);
    Check("the next load retries and upgrades it", retried && v1.Version == 6 && v1.Active.Class == "MAGE" && v1.Active.Deck.SequenceEqual(deck));
    var orphan = NewFor(all, "MAGE", 777); orphan.Class = "BARD";
    var profile = new DungeonRunProfile();
    Throws<InvalidDataException>("a run with an unknown class cannot be fought", () => run.BuildPlan(orphan));
    run.Abandon(orphan, profile);
    Check("... but it can be abandoned: defeat, archived with its class id, no stats row", orphan.Phase == DungeonRunPhase.Defeat && orphan.Finished
        && profile.Archive.Count == 1 && profile.Archive[0].Class == "BARD" && profile.Heroes.Count == 0);
}
{
    // Review 1 defect 4: the detector phrases match across line breaks, hard spaces and tags. Phase 2: a phrase only makes a
    // card an admission subject; the verdict is DungeonRunAdmission's table (a subject with no rule is "unclassified").
    Check("validator text: line breaks, '_', '&' and tags read as spaces", DungeonRunValidator.MatchText("Reveal a spell in" + (char)10 + "your deck.") == "reveal a spell in your deck."
        && DungeonRunValidator.MatchText("<b>Battlecry:</b> Add a_Secret&to your hand") == "battlecry: add a secret to your hand");
    var spire = Cards.FromId("JAIL_379");
    Check("Spire Security (split over two lines) is detected as an admission subject with no rule", DungeonRunValidator.MatchText(spire.Text).Contains("in your deck") && !(spire.Text ?? "").ToLowerInvariant().Contains("in your deck")
        && DungeonRunAdmission.Classify(spire, spire.Id, AdmitScope.Player) is var sv && sv.Subject && sv.Family == "unclassified" && !sv.Admitted);
    var bad = DungeonRunData.Parse(File.ReadAllText(Path.Combine(root, "dungeon-run", "run-v1.json")),
        File.ReadAllText(Path.Combine(root, "dungeon-run", "pool-v1.json")).Replace("\"CORE_SW_108\"", "\"JAIL_379\""), File.ReadAllText(Path.Combine(root, "npc", "decks-v1.json")));
    var errs = DungeonRunValidator.Validate(bad, null);
    Check("a deck holding Spire Security fails validation as admission unclassified", errs.Any(e => e.StartsWith("JAIL_379: admission unclassified:") && e.Contains("lifted by " + DungeonRunAdmission.AddRule)), string.Join(" | ", errs));
}
{
    // Version gate, idempotence, backup.
    string dir = Path.Combine(Path.GetTempPath(), "dr-ver-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir);
    string path = Path.Combine(dir, DungeonRunStore.FileName);
    File.WriteAllText(path, File.ReadAllText(Path.Combine(fixtures, "class-spellwright", DungeonRunStore.FileName)).Replace("Version=\"1\"", "Version=\"7\""));
    Throws<InvalidDataException>("a version-7 file is refused (never overwritten by a downgrade)", () => DungeonRunStore.Load(dir));
    File.Copy(Path.Combine(fixtures, "class-spellwright", DungeonRunStore.FileName), path, true);
    byte[] original = File.ReadAllBytes(path);
    var save = DungeonRunStore.Load(dir);
    Check("a version-1 file loads as version 1", save.Version == 1 && save.LoadedVersion == 1);
    bool first = DungeonRunMigration.Upgrade(save, run, migrationLog.Add);
    string once; using (var w = new StringWriter()) { new System.Xml.Serialization.XmlSerializer(typeof(DungeonRunSave)).Serialize(w, save); once = w.ToString(); }
    bool second = DungeonRunMigration.Upgrade(save, run, migrationLog.Add);
    save.Version = 1; bool forced = DungeonRunMigration.Upgrade(save, run, migrationLog.Add); // steps alone: only rows without a class change
    string thrice; using (var w = new StringWriter()) { new System.Xml.Serialization.XmlSerializer(typeof(DungeonRunSave)).Serialize(w, save); thrice = w.ToString(); }
    Check("upgrade runs once; a second upgrade changes nothing; the steps are idempotent", first && !second && forced && once == thrice);
    DungeonRunStore.Save(dir, save);
    string backup = Path.Combine(dir, DungeonRunStore.V1BackupName);
    Check("first save of an upgraded file keeps the old file as dungeon-run.v1.xml", File.Exists(backup) && File.ReadAllBytes(backup).SequenceEqual(original));
    var reloaded = DungeonRunStore.Load(dir);
    Check("serialize -> load: version 6, same content", reloaded.Version == 6 && reloaded.Active.Class == "MAGE" && reloaded.Active.Deck.SequenceEqual(save.Active.Deck)
        && reloaded.Active.Seed == save.Active.Seed && reloaded.Profile.Unlocked.SequenceEqual(save.Profile.Unlocked));
    reloaded.Active.Floor = 4; DungeonRunStore.Save(dir, reloaded);
    Check("later saves leave the backup alone", File.ReadAllBytes(backup).SequenceEqual(original));
    Directory.Delete(dir, true);
}

// ---------------------------------------------------------------- 10. catalog (the native picker's API) and plank text
{
    var classes = DungeonRunCatalog.Classes(run, fresh);
    Check("catalog: 11 classes in order, portraits and defaults", classes.Count == 11 && classes.Select(c => c.Id).SequenceEqual(classIds)
        // Build Deck: the build deck is the row's fourth deck, never locked.
        && classes.All(c => c.DecksUnlocked == 2 && c.PowersUnlocked == 1 && c.DeckCount == 4 && c.PowerCount == 3 && c.HeroCardId == data.Class(c.Id).HeroCard)
        && classes.Select(c => c.SortOrder).SequenceEqual(Enumerable.Range(0, 11)));
    Check("catalog: client ids default to the engine ids headless", classes.All(c => c.ClientHeroCardId == c.HeroCardId)
        && classes.SelectMany(c => DungeonRunCatalog.Powers(run, fresh, c.Id)).All(p => p.ClientCardId == p.ShellCardId));
    Func<string, string> transport = id => id == "CS2_034" ? "HERO_08bp" : id == "HERO_08" ? "HERO_08x" : id;
    var mage = DungeonRunCatalog.Powers(run, fresh, "MAGE", transport);
    Check("catalog: ClientCardId / ClientHeroCardId go through the transport map (CS2_034 -> HERO_08bp)",
        mage[0].ShellCardId == "CS2_034" && mage[0].ClientCardId == "HERO_08bp" && mage[1].ClientCardId == mage[1].ShellCardId
        && DungeonRunCatalog.Classes(run, fresh, transport).Single(c => c.Id == "MAGE").ClientHeroCardId == "HERO_08x");
    Check("catalog: unknown class -> empty", DungeonRunCatalog.Decks(run, fresh, "BARD").Count == 0 && DungeonRunCatalog.Powers(run, fresh, "BARD").Count == 0
        && DungeonRunCatalog.Classes(null, fresh).Count == 0);
    var labels = new List<string>();
    foreach (var c in classes)
    {
        // The Build Deck option is listed last (c-sched-9 BD10 checks it); the three fixed decks are checked here.
        var options = DungeonRunCatalog.Decks(run, fresh, c.Id);
        var decks = options.Where(d => !d.IsBuild).ToList(); var powers = DungeonRunCatalog.Powers(run, fresh, c.Id);
        var decksAll = DungeonRunCatalog.Decks(run, all, c.Id); var powersAll = DungeonRunCatalog.Powers(run, all, c.Id);
        bool ok = options.Count == 4 && options[3].IsBuild && options[3].Unlocked && decks.Count == 3 && powers.Count == 3 && decks[0].IsDefault && decks[0].Unlocked && powers[0].IsDefault && powers[0].Unlocked
            && decks.Skip(1).All(d => !d.Unlocked && !d.IsDefault) && powers.Skip(1).All(p => !p.Unlocked && !p.IsDefault)
            && decks.Concat<object>(powers).All(o => o is RunDeckOption d ? (d.UnlockHint == null) == d.Unlocked : (((RunPowerOption)o).UnlockHint == null) == ((RunPowerOption)o).Unlocked)
            && decksAll.All(d => d.Unlocked && d.UnlockHint == null) && powersAll.All(p => p.Unlocked && p.UnlockHint == null)
            && decks.All(d => d.CardIds.Count == 10 && d.ClassId == c.Id && d.CardIds.SequenceEqual(data.Deck(d.Id).Cards))
            && powers.All(p => p.ShellCardId == DungeonRunSetup.PowerCardId(p.Id) && p.Cost > 0 && !string.IsNullOrEmpty(p.Name) && !string.IsNullOrEmpty(p.PlainText)
                && !p.PlainText.Contains("<") && !p.PlainText.StartsWith("Hero Power"))
            && decks.Select(d => d.SortOrder).SequenceEqual(new[] { 0, 1, 2 }) && powers.Select(p => p.SortOrder).SequenceEqual(new[] { 0, 1, 2 })
            && decks[1].UnlockHint == "Reach floor 3 with " + c.ClassName + " to unlock" && decks[2].UnlockHint == "Reach floor 5 with " + c.ClassName + " to unlock"
            && powers[1].UnlockHint == "Reach floor 4 with " + c.ClassName + " to unlock" && powers[2].UnlockHint == "Win a run with " + c.ClassName + " to unlock";
        Check($"catalog {c.Id}: 3 decks + 3 powers, defaults first, hints exactly when locked, shell ids", ok,
            string.Join(" / ", powers.Select(p => $"{p.Id}:{p.Cost}:{p.UnlockHint}")));
        labels.Add(NativeDungeonRunText.ClassLabel(c.Name, c.Id));
        foreach (var d in decks) { labels.Add(d.Name); labels.Add(NativeDungeonRunText.LockedChoiceLabel(d.Name)); }
        foreach (var p in powers) { labels.Add(p.Name); labels.Add(NativeDungeonRunText.LockedChoiceLabel(p.Name)); }
    }
    var targeted = DungeonRunCatalog.Powers(run, fresh, "MAGE").Concat(DungeonRunCatalog.Powers(run, fresh, "PRIEST")).ToDictionary(p => p.Id, p => p.Targeted);
    Check("catalog: Targeted follows the shell's reqs", targeted["CS2_034"] && !targeted["carto_survey"] && targeted["mage_rimebite"] && targeted["priest_ward_of_faith"]);
    // The longest plank the version-1 hero list showed was "The Cartographer - Mage/Rogue" (29 characters).
    var longest = labels.OrderByDescending(l => l.Length).First();
    Check("every class, deck and power plank label is ASCII and at most 29 characters", labels.All(l => l.Length <= 29 && l.All(ch => ch < 128)), longest + " " + longest.Length);
    Check("unlocked plank labels at most 24 characters", labels.Where(l => !l.EndsWith("(locked)")).All(l => l.Length <= 24),
        labels.Where(l => !l.EndsWith("(locked)")).OrderByDescending(l => l.Length).First());
    var captions = data.Decks.Select(d => NativeDungeonRunText.Described(d.Name, d.Description)).ToList();
    Check("deck captions ASCII", captions.All(l => l.All(ch => ch < 128)));
    Check("hub line, version 2 and version 1", NativeDungeonRunText.ChoiceLine("The Spellwright", "Rimeward", "Fireblast") == "The Spellwright \u00b7 Rimeward \u00b7 Fireblast"
        && NativeDungeonRunText.ChoiceLine("The Spellwright", null, "Fireblast") == "The Spellwright \u00b7 Fireblast");
    Check("deck card line folds repeats", NativeDungeonRunText.DeckCardsLine(new[] { "Ray of Frost", "Ray of Frost", "Frostbolt" }) == "2x Ray of Frost, Frostbolt");
}

// ---------------------------------------------------------------- 11. real matches: every boss and every class/deck/power, random legal play
int matches = 0, faults = 0;
var faultLog = new List<string>();
void PlayMatch(DungeonRunState s, int rngSeed)
{
    var plan = run.BuildPlan(s);
    var cfg = new GameConfig { RandomSeed = run.MatchSeed(s), Shuffle = true, SkipMulligan = false, History = true, Logging = false, FormatType = FormatType.FT_WILD };
    DungeonRunSetup.Configure(cfg, plan);
    var g = new Game(cfg);
    try
    {
        g.StartGame();
        DungeonRunSetup.ApplyAfterStart(g, plan);
        g.Process(ChooseTask.Mulligan(g.Player1, new List<int>()));
        g.Process(ChooseTask.Mulligan(g.Player2, new List<int>()));
        g.MainBegin();
        var rnd = new Random(rngSeed);
        int steps = 0;
        while (g.State != State.COMPLETE && steps++ < 600 && g.Turn < 60)
        {
            var options = g.CurrentPlayer.Options();
            var end = options.FirstOrDefault(o => o is EndTurnTask); var choice = end == null || (options.Count > 1 && rnd.Next(5) > 0) ? options[rnd.Next(options.Count)] : end;
            g.Process(choice);
        }
        matches++;
    }
    catch (Exception e)
    {
        faults++;
        faultLog.Add($"{s.Class}/{s.StarterDeck}/{s.Power} vs {s.BossSequence[s.Floor - 1]} floor {s.Floor}: {e.GetType().Name}: {e.Message.Split('\n')[0]}");
    }
}
foreach (var boss in data.Bosses)
{
    var cls = data.Classes[Math.Abs(boss.Id.Length * 31 + data.Bosses.IndexOf(boss)) % data.Classes.Count];
    var s = NewFor(all, cls.Id, 1000 + data.Bosses.IndexOf(boss), cls.Decks[data.Bosses.IndexOf(boss) % 3], cls.Powers[data.Bosses.IndexOf(boss) % 3]);
    s.Floor = boss.Final ? 8 : boss.Band == 1 ? 1 : boss.Band == 2 ? 4 : 7;
    s.BossSequence[s.Floor - 1] = boss.Id;
    for (int k = 0; k < 3; k++) PlayMatch(s, k * 7919 + s.Floor);
}
int combo = 0;
foreach (var cls in data.Classes)
    foreach (string deckId in cls.StartDecks(data))
        foreach (string power in cls.Powers)
        {
            var s = NewFor(all, cls.Id, 555 + combo, deckId, power);
            s.Treasures.AddRange(new[] { "spark_lantern", "iron_skin", "deep_satchel", "hearty_tonic", "old_map" });
            PlayMatch(s, 31 * combo++ + 1);
        }
// ---------------------------------------------------------------- audit-classes: Death Knight and Demon Hunter
foreach (string newClass in new[] { "DEATHKNIGHT", "DEMONHUNTER" })
{
    var cls = data.Class(newClass);
    Check($"{newClass}: hero {cls.HeroCard} is a {newClass} HERO, basic power {cls.Powers[0]}",
        Cards.FromId(cls.HeroCard).Type == CardType.HERO && Cards.FromId(cls.HeroCard).Class.ToString() == newClass
        && cls.Powers[0] == Cards.FromAssetId(Cards.FromId(cls.HeroCard)[GameTag.HERO_POWER])?.Id, cls.HeroCard + " " + Cards.FromAssetId(Cards.FromId(cls.HeroCard)[GameTag.HERO_POWER])?.Id);
    Check($"{newClass}: 4 class bundles (legacy fixed entries)", data.LegacyBundles.Count(b => b.Class == newClass && !b.IsPool) == 4);
    Check($"{newClass}: classTags", data.ClassTags.ContainsKey(newClass));
    foreach (string deckId in cls.StartDecks(data))
        foreach (string power in cls.Powers)
        {
            var profile = new DungeonRunProfile(); profile.Unlocked.AddRange(data.Unlocks.Keys);
            var s = PlayOut(profile, run.NewRun(profile, newClass, deckId, power, 4242 + deckId.Length * 7 + power.Length, signatureId: run.SignatureOffer(profile, run.Data.FindClass(newClass), 4242 + deckId.Length * 7 + power.Length).FirstOrDefault()), (_, __) => DungeonRunOutcome.Win);
            var runes = RuneRules.FromCards(s.Deck.Select(Cards.FromId));
            Check($"{newClass}/{deckId}/{power}: winning run, bundles {newClass} or neutral, deck {s.Deck.Count}, runes legal",
                s.Phase == DungeonRunPhase.Victory && s.Drafts.Where(d => d.Kind == "bundle").SelectMany(d => d.Offered).All(id => data.Bundles.First(b => b.Id == id).Class is "NEUTRAL" || data.Bundles.First(b => b.Id == id).Class == newClass)
                && RuneRules.IsLegalForClass(runes, cls.Id == "DEATHKNIGHT" ? CardClass.DEATHKNIGHT : CardClass.DEMONHUNTER),
                $"phase {s.Phase} deck {s.Deck.Count} runes B{runes.Blood}F{runes.Frost}U{runes.Unholy}");
        }
}
{
    // The rune checks are real: a Death Knight deck needing 4 runes, and a Death Knight bundle with a rune card, are refused.
    var broken = DungeonRunData.Parse(File.ReadAllText(Path.Combine(root, "dungeon-run", "run-v1.json")),
        File.ReadAllText(Path.Combine(root, "dungeon-run", "pool-v1.json")), File.ReadAllText(Path.Combine(root, "npc", "decks-v1.json")));
    broken.Decks.First(d => d.Id == "deathknight_bones").Cards[0] = "TLC_435";   // Crypt Map, FF: with UU, 4 runes
    broken.Bundles.First(b => b.Id == "bundle_deathknight_spells").Cards[0] = "RLK_024"; // Death Strike, B
    var found = DungeonRunValidator.Validate(broken, null);
    Check("validator refuses a 4-rune Death Knight deck and a rune card in a bundle",
        found.Any(e => e.StartsWith("deck deathknight_bones") && e.Contains("runes")) && found.Any(e => e.StartsWith("bundle_deathknight_spells") && e.Contains("rune")),
        string.Join(" | ", found.Take(6)));
}
Check($"random-play matches run without engine faults ({matches} games)", faults == 0, string.Join(" | ", faultLog.Take(6)));
{
    // Phase 2 review: the validator applies the admission table to passive treasures, their effect shells, legacy powers and
    // every content shell (shrines included), not only to pool cards, player powers and active treasures.
    var passiveTreasure = new RunContent { Key = "probe_passive", Kind = RunContentKind.PassiveTreasure, EffectKey = "probe_fx", Name = "probe", Text = "probe" };
    // Phase 7 D admitted TRLA_809 (a shrine upgrade); step 9 admitted ULDA_111 (the escape), so GILA_900h2 (fight-hero-card, a
    // final rejection) stands in as the rejected shell.
    var rejectedEffect = new RunContent { Key = "probe_fx", Kind = RunContentKind.Effect, ShellId = "GILA_900h2", Name = "probe", Text = "probe" };
    var rejectedShrine = new RunContent { Key = "probe_shrine_1", Kind = RunContentKind.PassivePower, ShellId = "GILA_900p", Name = "probe", Text = "probe" };
    var synthetic = new DungeonRunData { DataVersion = "runlayer", Rules = data.Rules };
    synthetic.Treasures.Add(new DungeonRunTreasure { Id = "probe_passive", Rarity = "common", Universal = true });
    // Phase 6 F15 admitted GILA_511, phase 7 T2 TRLA_191 (a teammate) and step 9 GILA_911 (the Rewind restart); DALA_740
    // (treasure-data-gap, rejected for good in step 9) is a treasure id the table still rejects.
    synthetic.Treasures.Add(new DungeonRunTreasure { Id = "DALA_740", Rarity = "common", Universal = true });
    var errs = DungeonRunValidator.Validate(synthetic, null, DungeonRunContent.All.Concat(new[] { passiveTreasure, rejectedEffect, rejectedShrine }).ToList());
    Check("validator: a passive treasure whose effect shell the table rejects fails admission",
        errs.Any(e => e.StartsWith("GILA_900h2: admission fight-hero-card:") && e.Contains("(probe_passive effect)")), string.Join(" | ", errs.Where(e => e.Contains("probe"))));
    Check("validator: a passive treasure id the table rejects (DALA_740) fails admission",
        errs.Any(e => e.StartsWith("DALA_740: admission treasure-data-gap:")), string.Join(" | ", errs.Where(e => e.StartsWith("DALA_740"))));
    Check("validator: a shrine whose shell the table rejects fails admission",
        errs.Any(e => e.StartsWith("GILA_900p: admission turn-rewind:") && e.Contains("(content probe_shrine_1)")), string.Join(" | ", errs.Where(e => e.Contains("probe_shrine"))));
    Check("validator: the real data with the real content still has 0 errors", DungeonRunValidator.Validate(data, null).Count == 0);
}


// ---------------------------------------------------------------- 12. phase 3 I2: catalog runs, save version 3, dual-class rows
{
    string catalogJson = File.ReadAllText(Path.Combine(root, "dungeon-run", "catalog-v1.json"));
    string runJson = File.ReadAllText(Path.Combine(root, "dungeon-run", "run-v1.json"));
    string poolJson = File.ReadAllText(Path.Combine(root, "dungeon-run", "pool-v1.json"));
    string npcJson = File.ReadAllText(Path.Combine(root, "npc", "decks-v1.json"));
    DungeonRunData CatalogData() => DungeonRunData.Parse(runJson, poolJson, npcJson, catalogJson);
    var cdata = CatalogData();
    var crun = new DungeonRun(cdata);
    var call = new DungeonRunProfile(); call.Unlocked.AddRange(cdata.Unlocks.Keys);
    string Xml(object o) { using var w = new StringWriter(); new System.Xml.Serialization.XmlSerializer(o.GetType()).Serialize(w, o); return w.ToString(); }
    DungeonRunState CNew(string cls, long seed) { var c = cdata.Class(cls); return crun.NewRun(call, cls, c.DefaultDeck, c.DefaultPower, seed, signatureId: crun.SignatureOffer(call, crun.Data.FindClass(cls), seed).FirstOrDefault()); }
    // Plays a catalog run to its end, winning every floor; pick(draft) chooses the bundle (default: slot floor % count).
    DungeonRunState CPlay(DungeonRun r, DungeonRunState s, Func<DungeonRunState, DungeonRunDraft, string> pick = null, List<string> log = null)
    {
        var scratch = new DungeonRunProfile();
        while (!s.Finished)
        {
            if (s.Phase == DungeonRunPhase.PreMatch)
            {
                int expect = r.Data.Rules.StarterSize + 3 * (s.Floor - 1) + s.ActiveTreasures.Count;
                if (s.Deck.Count != expect) log?.Add($"{s.Class} floor {s.Floor}: deck {s.Deck.Count}, expected {expect}");
                r.BeginMatch(s); r.RecordOutcome(s, scratch, DungeonRunOutcome.Win);
            }
            else if (s.Phase == DungeonRunPhase.TreasureDraft) r.PickTreasure(s, s.OpenDraft.Offered[0]);
            else r.PickBundle(s, pick != null ? pick(s, s.OpenDraft) : s.OpenDraft.Offered[s.Floor % s.OpenDraft.Offered.Count]);
        }
        return s;
    }
    bool Shares(DungeonRunBundle a, DungeonRunBundle b) => a.ClassSet.Any(b.ClassSet.Contains);

    // Data: the catalog data validates with its dual-class row; disabled rows stay out of play.
    var cerr = DungeonRunValidator.Validate(cdata, null);
    Check("catalog data (with the dual-class rows) validates", cerr.Count == 0, cerr.Count + ": " + string.Join(" | ", cerr.Take(8)));
    // Phase 6 F3 adds the Monster Hunt hero rows Tess (GILA_500h3) and Darius (GILA_600h), one authored starter each.
    // Phase 6 F4 adds Shaw (GILA_400h) and Toki (GILA_900h), one authored starter each.
    // Phase 6 F6 adds the nine Dalaran Heist villains (DALA_Rakanishu ... DALA_George), each with its authentic starters
    // DALA_<Class>_Deck1-3 (Hunter: Deck1-2, Deck3 waits for DAL_377) and a generated Random deck: 35 decks.
    // Phase 6 F10 scripts the starter collectibles: PALADIN_SHAMAN, DRUID_PRIEST and HUNTER_WARRIOR ship enabled with
    // ULDA_<Explorer>_Deck1-3 and their Random decks, MAGE_ROGUE adds Deck2-3, Ol' Barkeye adds DALA_Hunter_Deck3: 89 decks.
    // Phase 6 F14 adds Rikkar's nine class forms (Rumble) with their 27 shrine starters: 37 classes, 116 decks.
    Check("catalog data: 37 playable classes (11 + the 4 Tombs dual rows + the 4 Monster Hunt heroes + the 9 Heist villains + Rikkar's 9 forms), no row disabled, 140 decks (phase 6 F14's 116 + 24 build decks)",
        cdata.Classes.Count == 37 && cdata.Classes.Count(c => c.Adventure == "Heist") == 9 && cdata.Classes.Count(c => c.Adventure == "Rumble") == 9 && cdata.Classes.Count(c => c.IsDual) == 4 && cdata.FindClass("GILA_500h3")?.IsHero == true && cdata.FindClass("GILA_600h")?.IsHero == true && cdata.FindClass("GILA_400h")?.IsHero == true && cdata.FindClass("GILA_900h")?.IsHero == true && cdata.FindClass("MAGE_ROGUE")?.ClassSet.SequenceEqual(new[] { "MAGE", "ROGUE" }) == true
        // Build Deck: 24 build decks (the 11 class rows, the 4 Monster Hunt heroes, Rikkar's 9 forms): 116 + 24 = 140.
        && cdata.DisabledClasses.Count == 0 && cdata.Decks.Count == 140 && cdata.Decks.Count(d => d.Build) == 24 && cdata.DisabledDecks.Count == 0,
        $"{cdata.Classes.Count} classes, {cdata.DisabledClasses.Count} disabled, {cdata.Decks.Count} decks");
    Check("legacy data (no catalog) has no dual row: legacy runs are single-class", data.Classes.All(c => c.ClassSet.Count == 1) && data.DisabledClasses.Count == 0);
    Check("draft knobs read from run-v1.json: bucketMaxTakes 3, dualMatchWeight 2", cdata.Rules.BucketMaxTakes == 3 && cdata.Rules.DualMatchWeight == 2);
    Throws<ArgumentException>("a disabled dual row cannot start a run", () => crun.NewRun(call, "PALADIN_SHAMAN", "x", "CS2_101", 1, signatureId: crun.SignatureOffer(call, crun.Data.FindClass("PALADIN_SHAMAN"), 1).FirstOrDefault()));
    var mr = cdata.Class("MAGE_ROGUE");
    // Phase 6 F0: ULDA_Reno is scripted as a run hero (30 Health) and MAGE_ROGUE is a Tombs hero row with that portrait.
    // Phase 6 F9: Reno's own powers ULDA_Reno_HP1-3 replace the Mage stand-ins, and his Random deck (ULDA_901) follows the starter.
    Check("MAGE_ROGUE: the ULDA_Reno portrait (a scripted run hero, phase 6), Reno's powers, its own unlock ladder, its three Tombs starters (F10: Deck2-3) and the Random deck",
        mr.HeroCard == "ULDA_Reno" && Cards.FromId("ULDA_Reno").Implemented && mr.IsHero && mr.Health == 30 && mr.Powers.SequenceEqual(new[] { "ULDA_Reno_HP1", "ULDA_Reno_HP2", "ULDA_Reno_HP3" })
        && mr.PowerUnlocks.Values.All(k => k.StartsWith("mage_rogue_") && cdata.Unlocks.ContainsKey(k)) && mr.Decks.SequenceEqual(new[] { "tombs_reno_spellslinger", "tombs_reno_spell_seeker", "tombs_reno_secret_traps", "tombs_reno_random" })
        && cdata.Unlocks["mage_rogue_win"].Class == "MAGE_ROGUE");
    Check("ClassName of a dual row: Mage / Rogue", NativeDungeonRunText.ClassName("MAGE_ROGUE") == "Mage / Rogue" && NativeDungeonRunText.ClassName("DEMONHUNTER") == "Demon Hunter"
        // Phase 6: the class list holds the class rows; a dual row is listed under its primary class (DungeonRunCatalog.Heroes).
        && DungeonRunCatalog.Heroes(crun, call, "MAGE").Any(o => o.Id == "MAGE_ROGUE" && o.ClassName == "Mage / Rogue")
        && !DungeonRunCatalog.Classes(crun, call).Any(o => o.Id == "MAGE_ROGUE"));
    Check("the native picker's tray classes are TAG_CLASS values: no dual row id is a class name", cdata.Classes.Where(c => c.IsDual).All(c => !Enum.TryParse<CardClass>(c.Id, out _))
        && File.ReadAllText(VerifyPaths.Repo("plugin/NativeDungeonRunPicker.cs")).Contains("GameDbf.AdventureDeck.GetRecords().Where(d => d.AdventureId == (int)Adventure).Select(d => (TAG_CLASS)d.ClassId)"));
    {
        // The Tombs starter import: every client starter deck of a guest hero (the "Random" placeholders skipped) ships exactly
        // when each card passes the pool checks for the hero's two classes; a row is enabled exactly when one ships.
        using var adv = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "dungeon-run", "reference", "adventures.json")));
        var tombs = adv.RootElement.GetProperty("adventures").EnumerateArray().First(a => a.GetProperty("key").GetString() == "tombs-of-terror");
        var heroRow = new Dictionary<string, string> { ["Reno"] = "MAGE_ROGUE", ["Finley"] = "PALADIN_SHAMAN", ["Elise"] = "DRUID_PRIEST", ["Brann"] = "HUNTER_WARRIOR" };
        var allRows = cdata.Classes.Concat(cdata.DisabledClasses).ToList();
        var policy = OfflinePoolPolicy.Load();
        var shipped = new Dictionary<string, int>();
        bool importOk = true; var importDetail = new List<string>();
        foreach (var d in tombs.GetProperty("starterDecks").EnumerateArray())
        {
            string note = d.GetProperty("noteName").GetString();
            if (note.EndsWith("_Random", StringComparison.Ordinal)) continue;
            var row = allRows.First(r => r.Id == heroRow[note.Split('_')[1]]);
            // A card plays as its CORE_ reprint when only the reprint passes (the catalog's resolution rule; phase 6 F10).
            var cards = d.GetProperty("cards").EnumerateArray().Select(x => x.GetString())
                .Select(id => DungeonRunValidator.PoolCardReason(id, row.ClassSet, false, policy, catalogRun: true) != null
                    && DungeonRunValidator.PoolCardReason("CORE_" + id, row.ClassSet, false, policy, catalogRun: true) == null ? "CORE_" + id : id).ToList();
            bool deckPasses = cards.All(id => DungeonRunValidator.PoolCardReason(id, row.ClassSet, false, policy, catalogRun: true) == null);
            var deck = cdata.Decks.FirstOrDefault(x => x.Class == row.Id && x.Cards.SequenceEqual(cards));
            if (deckPasses != (deck != null)) { importOk = false; importDetail.Add(note + (deckPasses ? " passes but is not shipped" : " fails but is shipped")); }
            if (deck != null) shipped[row.Id] = shipped.TryGetValue(row.Id, out int n) ? n + 1 : 1;
        }
        Check("Tombs starters: a deck ships exactly when every card passes the pool checks after reprint resolution (since phase 6 F10 all twelve do, three per explorer)",
            importOk && shipped.Count == 4 && shipped.Values.All(n => n == 3), string.Join("; ", importDetail));
        Check("Tombs rows: enabled exactly when a starter ships", allRows.Where(r => r.IsDual).All(r => r.Enabled == shipped.ContainsKey(r.Id)));
    }
    {
        var scope = new DungeonRunScope(); scope.Classes.Add("ROGUE");
        Check("treasure scope: a class scope matches a dual run with that class", scope.Matches(new[] { "MAGE", "ROGUE" }, null, null) && !scope.Matches(new[] { "MAGE" }, null, null)
            && scope.Matches("ROGUE", null, null) == scope.Matches(new[] { "ROGUE" }, null, null));
    }

    // New runs: stamped with the loaded catalog; a run on data without a catalog stays legacy.
    var c1 = CNew("MAGE", 4242);
    Check("NewRun stamps CatalogVersion 1 with the catalog loaded, 0 without", c1.CatalogVersion == 1 && c1.IsCatalogRun && NewFor(all, "MAGE", 4242).CatalogVersion == 0);
    {
        var lp = run.BuildPlan(NewFor(all, "MAGE", 4242)); var cp = crun.BuildPlan(c1);
        var lifted = new[] { "BOT_402", "DAL_141", "GIL_116", "ICC_204", "UNG_011" };
        Check("BuildPlan: a legacy run keeps generated Secrets off and the legacy generation lists", !lp.PlayerGeneratedSecrets
            && lp.ExcludedGenerated.SequenceEqual(DungeonRunAdmission.GeneratedRejects()) && lp.ExcludedGeneratedBoss.SequenceEqual(DungeonRunAdmission.GeneratedRejectsBoss())
            && lifted.All(lp.ExcludedGenerated.Contains));
        Check("BuildPlan: a catalog run lets its player generate Secrets; the lifted generators leave the shared list for the boss's",
            cp.PlayerGeneratedSecrets && lifted.All(id => !cp.ExcludedGenerated.Contains(id) && cp.ExcludedGeneratedBoss.Contains(id))
            && cp.ExcludedGenerated.Contains("DALA_BOSS_21p"));
        var cfgL = new GameConfig(); DungeonRunSetup.Configure(cfgL, lp);
        var cfgC = new GameConfig(); DungeonRunSetup.Configure(cfgC, cp);
        Check("Configure: legacy blocks both sides' generated Secrets; catalog blocks the boss's only", cfgL.ExcludeGeneratedSecrets && cfgL.Player1ExcludeGeneratedSecrets && cfgL.Player2ExcludeGeneratedSecrets
            && !cfgC.ExcludeGeneratedSecrets && !cfgC.Player1ExcludeGeneratedSecrets && cfgC.Player2ExcludeGeneratedSecrets);
        var alias = new GameConfig { ExcludeGeneratedSecrets = true };
        Check("GameConfig.ExcludeGeneratedSecrets is an alias that sets both sides", alias.Player1ExcludeGeneratedSecrets && alias.Player2ExcludeGeneratedSecrets
            && !new GameConfig().ExcludeGeneratedSecrets && !new GameConfig().Player1ExcludeGeneratedSecrets);
    }

    // Catalog drafts over many runs: eligibility, slot rules, takes, cards.
    int cruns = 0, repeatRuns = 0, tombsSeen = 0, poolOffers = 0, fixedOffers = 0, secretEntrySeen = 0, dupCardOffers = 0, maxTakes = 0, distinctTaken = 0;
    var bad = new List<string>(); var deckLog = new List<string>();
    foreach (var cls in cdata.Classes)
        for (int k = 0; k < 6; k++)
        {
            var s = crun.NewRun(call, cls.Id, cls.StartDecks(cdata)[k % cls.StartDecks(cdata).Count], cls.Powers[k % cls.Powers.Count], 70000 + 97 * cruns, signatureId: crun.SignatureOffer(call, crun.Data.FindClass(cls.Id), 70000 + 97 * cruns).FirstOrDefault());
            // Prefer a pool entry taken before (the repeat path), else the slot by floor.
            CPlay(crun, s, (st, d) => d.Offered.FirstOrDefault(id => st.TakesOf(id) > 0) ?? d.Offered[st.Floor % d.Offered.Count], deckLog);
            cruns++;
            var runClasses = cls.ClassSet;
            bool secretHolds = DungeonRunAdmission.MechanicHolds(cdata, runClasses, "secret");
            foreach (var d in s.Drafts.Where(x => x.Kind == "bundle"))
            {
                var entries = d.Offered.Select(id => cdata.Bundles.First(b => b.Id == id)).ToList();
                if (d.Offers.Count != d.Offered.Count || !d.Offers.Select(o => o.Id).SequenceEqual(d.Offered)) bad.Add($"{cls.Id} f{d.Floor}: Offers do not match Offered");
                if (d.Offered.Distinct().Count() != d.Offered.Count) bad.Add($"{cls.Id} f{d.Floor}: an entry repeats");
                for (int i = 0; i < entries.Count; i++) for (int j = i + 1; j < entries.Count; j++)
                {
                    if (entries[i].LabelKey == entries[j].LabelKey && Shares(entries[i], entries[j])) bad.Add($"{cls.Id} f{d.Floor}: label {entries[i].LabelKey} twice in a shared class");
                    if (DungeonRun.SameDraw(entries[i], entries[j])) bad.Add($"{cls.Id} f{d.Floor}: {entries[i].Id} and {entries[j].Id} draw from the same cards");
                }
                foreach (var (e, o) in entries.Zip(d.Offers))
                {
                    if (!e.EligibleFor(runClasses, m => m == "secret" && secretHolds)) bad.Add($"{cls.Id} f{d.Floor}: {e.Id} not eligible");
                    if (o.Cards.Count != 3) bad.Add($"{e.Id}: {o.Cards.Count} cards");
                    if (e.IsPool)
                    {
                        poolOffers++;
                        if (o.Cards.Any(c => !e.Candidates.Any(x => x.Id == c))) bad.Add($"{e.Id}: a card not among its candidates");
                        if (e.Distinct && o.Cards.Distinct().Count() != 3) bad.Add($"{e.Id}: a distinct entry repeated a card");
                        if (o.Cards.Distinct().Count() < 3) dupCardOffers++;
                        if (o.Kind != "pool" || o.Theme != e.PrimaryTheme || o.Label != e.Label) bad.Add($"{e.Id}: stored kind/theme/label");
                    }
                    else { fixedOffers++; if (!o.Cards.SequenceEqual(e.Cards) || o.Kind != "fixed") bad.Add($"{e.Id}: fixed cards differ"); }
                    if (e.Id.StartsWith("tot.", StringComparison.Ordinal)) tombsSeen++;
                    if (e.RequiresMechanic == "secret") secretEntrySeen++;
                    if (e.Id.StartsWith("tot.", StringComparison.Ordinal) && !cls.IsDual) bad.Add($"{cls.Id}: a single-class run offered Tombs entry {e.Id}");
                }
            }
            foreach (var t in s.BucketTakes)
            {
                maxTakes = Math.Max(maxTakes, t.Count);
                if (t.Count > cdata.Rules.BucketMaxTakes) bad.Add($"{t.Id} taken {t.Count} times");
                if (cdata.Bundles.First(b => b.Id == t.Id).Distinct) { distinctTaken++; if (t.Count > 1) bad.Add($"{t.Id}: a distinct entry taken {t.Count} times"); }
            }
            if (s.BucketTakes.Any(t => t.Count >= 2)) repeatRuns++;
            if (s.TakenBundles.Distinct().Count() != s.TakenBundles.Count) bad.Add($"{cls.Id}: a fixed entry taken twice");
            if (s.Phase != DungeonRunPhase.Victory || s.Deck.Count != 10 + 21 + s.ActiveTreasures.Count) bad.Add($"{cls.Id}: phase {s.Phase} deck {s.Deck.Count}");
        }
    Check($"catalog runs ({cruns}): every offer eligible, Offers = Offered in slot order, 3 cards each, no entry or shared-class label twice in a step, takes within bucketMaxTakes, fixed entries once",
        bad.Count == 0 && deckLog.Count == 0, string.Join(" | ", bad.Concat(deckLog).Take(8)));
    Check("catalog runs draft both pool entries and fixed SoloStone bundles", poolOffers > 0 && fixedOffers > 0, $"pool {poolOffers} fixed {fixedOffers}");
    Check("a pool entry repeats across floors (Monster Hunt style), up to bucketMaxTakes", repeatRuns > 0 && maxTakes >= 2, $"{repeatRuns} runs, max {maxTakes}");
    Check("draws are without replacement inside an offer: no pool offer repeats a card", dupCardOffers == 0 && poolOffers > 0, $"{dupCardOffers} of {poolOffers}");
    {
        // DrawCards over every offerable pool entry and many seeds: three different candidates, every time.
        int draws = 0, repeats = 0;
        foreach (var e in cdata.Bundles.Where(b => b.IsPool && b.Offerable))
            for (int seed = 1; seed <= 40; seed++)
            {
                var cards = DungeonRun.DrawCards(e, seed * 7919L, 1 + seed % 8, seed % 3);
                draws++;
                if (cards.Count != 3 || cards.Distinct().Count() != 3) repeats++;
            }
        Check("DrawCards: three different candidates for every offerable pool entry (40 seeds each)", repeats == 0 && draws > 0, $"{repeats} of {draws}");
    }
    Check("a distinct (Unique) entry is taken at most once (MaxTakes 1)", cdata.Bundles.Where(b => b.Distinct).All(b => crun.MaxTakes(b) == 1)
        && cdata.Bundles.Where(b => b.IsPool && !b.Distinct).All(b => crun.MaxTakes(b) == cdata.Rules.BucketMaxTakes), $"distinct takes seen {distinctTaken}");
    {
        // A distinct entry taken once is never offered again in that run.
        var ue = cdata.Bundles.First(b => b.Distinct && b.Offerable && b.ClassSet.SequenceEqual(new[] { "MAGE" }));
        bool offeredAgain = false;
        for (int i = 0; i < 8; i++)
        {
            var s = CNew("MAGE", 64000 + i);
            s.BucketTakes.Add(new DungeonRunTake { Id = ue.Id, Count = 1 });
            CPlay(crun, s, (st, d) => d.Offered[0]);
            offeredAgain |= s.Drafts.Any(d => d.Offered.Contains(ue.Id));
        }
        Check($"a distinct entry with one take is never offered again ({ue.Id})", !offeredAgain);
    }
    Check("the MAGE_ROGUE run is offered Tombs entries; single-class runs never are", tombsSeen > 0);
    Check("Secret entries (requires.mechanic secret) are offered once the widened prior holds", secretEntrySeen > 0, secretEntrySeen.ToString());
    {
        // The union: a dual run sees Mage, Rogue and Tombs pair entries; the eligibility predicate is the draft's.
        var dual = cdata.Bundles.Where(b => b.EligibleFor(mr.ClassSet, m => true)).ToList();
        Check("MAGE_ROGUE eligibility = Mage entries + Rogue entries + their Tombs pair + neutral", dual.Any(b => b.ClassSet.SequenceEqual(new[] { "MAGE" })) && dual.Any(b => b.ClassSet.SequenceEqual(new[] { "ROGUE" }))
            && dual.Any(b => b.ClassesAll.SequenceEqual(new[] { "MAGE", "ROGUE" })) && dual.Any(b => b.IsNeutral)
            && !dual.Any(b => b.ClassesAll.Count > 0 && !b.ClassesAll.SequenceEqual(new[] { "MAGE", "ROGUE" })));
    }
    {
        // bucketMaxTakes honoured: with 1, no pool entry is taken twice; an entry at its maximum is never a candidate.
        var one = CatalogData(); one.Rules.BucketMaxTakes = 1; var r1 = new DungeonRun(one);
        bool twice = false;
        for (int i = 0; i < 6; i++)
        {
            var s = r1.NewRun(call, "HUNTER", "hunter_kennel", "DS1h_292", 81000 + i, signatureId: r1.SignatureOffer(call, r1.Data.FindClass("HUNTER"), 81000 + i).FirstOrDefault());
            CPlay(r1, s, (st, d) => d.Offered.FirstOrDefault(id => st.TakesOf(id) > 0) ?? d.Offered[0]);
            twice |= s.BucketTakes.Any(t => t.Count > 1);
        }
        Check("bucketMaxTakes 1: no pool entry is taken twice", !twice);
        var s2 = CNew("WARRIOR", 82000);
        while (s2.Phase != DungeonRunPhase.BundleDraft) { if (s2.Phase == DungeonRunPhase.PreMatch) { crun.BeginMatch(s2); crun.RecordOutcome(s2, new DungeonRunProfile(), DungeonRunOutcome.Win); } else crun.PickTreasure(s2, s2.OpenDraft.Offered[0]); }
        var first = s2.OpenDraft.Offered.First(id => cdata.Bundles.First(b => b.Id == id).IsPool);
        var redo = DungeonRunStore.Clone(new DungeonRunSave { Active = s2 }).Active;
        redo.Drafts.RemoveAt(redo.Drafts.Count - 1);
        redo.BucketTakes.Add(new DungeonRunTake { Id = first, Count = cdata.Rules.BucketMaxTakes });
        crun.OpenBundleDraft(redo);
        Check("an entry at bucketMaxTakes is not offered again", !redo.OpenDraft.Offered.Contains(first), first);
    }
    {
        // Save and resume: the stored offers survive a version-3 file; picking after a reload gives the same run.
        var s = CNew("PRIEST", 90210);
        while (s.Phase != DungeonRunPhase.BundleDraft || s.Floor < 3)
        {
            if (s.Phase == DungeonRunPhase.PreMatch) { crun.BeginMatch(s); crun.RecordOutcome(s, new DungeonRunProfile(), DungeonRunOutcome.Win); }
            else if (s.Phase == DungeonRunPhase.TreasureDraft) crun.PickTreasure(s, s.OpenDraft.Offered[0]);
            else crun.PickBundle(s, s.OpenDraft.Offered[0]);
        }
        string dir = Path.Combine(Path.GetTempPath(), "dr-cat-" + Guid.NewGuid().ToString("N"));
        var save = new DungeonRunSave { Active = s };
        DungeonRunStore.Save(dir, save);
        string text = File.ReadAllText(Path.Combine(dir, DungeonRunStore.FileName));
        var loaded = DungeonRunStore.Load(dir);
        Check("v3 round trip: CatalogVersion, BucketTakes and the open draft's Offers with their cards", loaded.Version == 6 && loaded.Active.CatalogVersion == 1
            && text.Contains("CatalogVersion=\"1\"") && text.Contains("<Offers>") && Xml(loaded) == Xml(save)
            && loaded.Active.OpenDraft.Offers.Select(o => string.Join(",", o.Cards)).SequenceEqual(s.OpenDraft.Offers.Select(o => string.Join(",", o.Cards))));
        // Reopening the same draft from the same state draws the same offers and cards (seeded, no clock).
        var again = DungeonRunStore.Clone(save).Active; var open = again.Drafts[again.Drafts.Count - 1]; again.Drafts.RemoveAt(again.Drafts.Count - 1);
        crun.OpenBundleDraft(again);
        Check("a catalog draft is a pure function of the run state: same offers, same cards", Xml(again.OpenDraft) == Xml(open));
        var a = CPlay(crun, s); var b = CPlay(crun, loaded.Active);
        Check("save, resume and play on: identical runs", Xml(a) == Xml(b));
        Directory.Delete(dir, true);
    }
    {
        // A stored offer whose entry left the data is still picked from its stored cards; a stored card that no longer passes
        // is a validation error and the run is not changed.
        var s = CNew("SHAMAN", 31415);
        while (s.Phase != DungeonRunPhase.BundleDraft) { if (s.Phase == DungeonRunPhase.PreMatch) { crun.BeginMatch(s); crun.RecordOutcome(s, new DungeonRunProfile(), DungeonRunOutcome.Win); } else crun.PickTreasure(s, s.OpenDraft.Offered[0]); }
        var pick = s.OpenDraft.Offers.First(o => o.Kind == "pool");
        var gone = CatalogData(); gone.Bundles.RemoveAll(b => b.Id == pick.Id); var rg = new DungeonRun(gone);
        var copy = DungeonRunStore.Clone(new DungeonRunSave { Active = s }).Active;
        int before = copy.Deck.Count;
        rg.PickBundle(copy, pick.Id);
        Check("pick of a stored offer whose entry was removed: the stored cards, one take, next floor", copy.Deck.Skip(before).SequenceEqual(pick.Cards)
            && copy.TakesOf(pick.Id) == 1 && copy.Floor == 2 && copy.Phase == DungeonRunPhase.PreMatch && copy.DraftedThemes.Skip(copy.DraftedThemes.Count - 3).All(t => t == pick.Theme));
        var tampered = DungeonRunStore.Clone(new DungeonRunSave { Active = s }).Active;
        tampered.OpenDraft.Offer(pick.Id).Cards[0] = "CS2_106"; // Fiery War Axe: a Warrior card in a Shaman offer
        string snapshot = Xml(tampered);
        Throws<InvalidDataException>("a stored card that no longer passes the pool checks refuses the pick", () => crun.PickBundle(tampered, pick.Id));
        Check("... and leaves the run unchanged", Xml(tampered) == snapshot);
    }
    {
        // A stored offer of a granting entry (Monster Hunt Tess or Shaw) whose grant candidate the client later drops: the
        // stored grant flag keeps the card legal (no class failure), and the pick's client check refuses a card the client no
        // longer knows with a clear reason instead of drafting it.
        var ge = cdata.Bundles.First(b => b.IsPool && b.Offerable && b.Candidates.Any(c => c.Grant) && b.ClassSet.Count == 1);
        string grant = ge.Candidates.First(c => c.Grant).Id;
        var others = ge.Candidates.Where(c => !c.Grant).Select(c => c.Id).Take(2).ToList();
        var s = CNew(ge.ClassSet[0], 27182);
        while (s.Phase != DungeonRunPhase.BundleDraft) { if (s.Phase == DungeonRunPhase.PreMatch) { crun.BeginMatch(s); crun.RecordOutcome(s, new DungeonRunProfile(), DungeonRunOutcome.Win); } else crun.PickTreasure(s, s.OpenDraft.Offered[0]); }
        var d = s.OpenDraft;
        d.Offered[0] = ge.Id;
        d.Offers[0] = new DungeonRunOffer { Id = ge.Id, Label = ge.Label, Theme = ge.PrimaryTheme, Kind = "pool", Source = DungeonRun.SourceTag(ge),
            Cards = new List<string> { grant }.Concat(others).ToList(), Grants = new List<string> { grant } };
        // Opened offers record their grant cards.
        var opened = CNew(ge.ClassSet[0], 27183);
        CPlay(crun, opened, (st, dd) => dd.Offered[0]);
        bool recorded = opened.Drafts.SelectMany(x => x.Offers).All(o => o.Grants.All(o.Cards.Contains)
            && o.Cards.Where(c => cdata.Bundles.First(b => b.Id == o.Id).Candidates.Any(x => x.Grant && x.Id == c)).All(o.Grants.Contains));
        Check("opened offers store exactly their grant cards (DungeonRunOffer.Grants)", recorded);
        string text = Xml(new DungeonRunSave { Active = s });
        int withGrants = s.Drafts.SelectMany(x => x.Offers).Count(o => o.Grants.Count > 0);
        Check("a stored grant is written (<Grants>) and an offer without one writes no element", text.Contains("<Grants>") && text.Split("<Grants>").Length - 1 == withGrants, withGrants.ToString());
        // Dropped from the data by hand, no client check: the stored flag admits it.
        var dropped = CatalogData(); dropped.Bundles.First(b => b.Id == ge.Id).Candidates.RemoveAll(c => c.Id == grant);
        var copy = DungeonRunStore.Clone(new DungeonRunSave { Active = s }).Active;
        new DungeonRun(dropped).PickBundle(copy, ge.Id);
        Check("pick of a stored grant whose candidate was dropped: the grant flag as offered, the card drafted", copy.Deck.Contains(grant) && copy.TakesOf(ge.Id) == 1);
        // Dropped because the client no longer knows it: DropClientUnknown keeps the client check for the pick.
        var unknown = CatalogData();
        var lines = DungeonRunValidator.DropClientUnknown(unknown, id => id != grant);
        var copy2 = DungeonRunStore.Clone(new DungeonRunSave { Active = s }).Active;
        string snap = Xml(copy2);
        string refusal = null;
        try { new DungeonRun(unknown).PickBundle(copy2, ge.Id); } catch (InvalidDataException e) { refusal = e.Message; }
        Check("pick of a stored card the client no longer knows is refused (client-unknown)", refusal != null && refusal.Contains("(client-unknown)") && refusal.Contains(grant), refusal ?? "did not throw");
        Check("... the run is unchanged, and the drop was logged", Xml(copy2) == snap && lines.Any(l => l.Contains("dropped " + grant + " client-unknown")) && unknown.ClientKnows != null);
        // A save from before Grants were stored: the current candidate's flag still admits a live grant.
        var old = DungeonRunStore.Clone(new DungeonRunSave { Active = s }).Active;
        old.OpenDraft.Offer(ge.Id).Grants.Clear();
        crun.PickBundle(old, ge.Id);
        Check("an offer stored without Grants falls back to the current candidate's grant flag", old.Deck.Contains(grant));
    }
    {
        // The disabled dual rows' reasons name only cards that still block after the catalog's reprint resolution: no listed
        // id has a scripted reprint (CORE_<id>, or a buckets.json alternate of it).
        var alternates = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        using (var bdoc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(VerifyPaths.Repo("data/dungeon-run/reference/buckets.json"))))
            foreach (var ad in bdoc.RootElement.GetProperty("adventures").EnumerateArray())
                foreach (var bk in ad.GetProperty("buckets").EnumerateArray())
                    foreach (var m in bk.GetProperty("members").EnumerateArray())
                    {
                        string id = m.GetProperty("cardId").GetString();
                        if (!alternates.TryGetValue(id, out var l)) alternates[id] = l = new List<string>();
                        foreach (var a in m.GetProperty("alternates").EnumerateArray()) l.Add(a.GetString());
                    }
        bool Scripted(string id) { try { return Cards.FromId(id)?.Implemented == true; } catch { return false; } }
        var wrong = new List<string>(); int named = 0;
        foreach (var cls in cdata.DisabledClasses)
            foreach (System.Text.RegularExpressions.Match seg in System.Text.RegularExpressions.Regex.Matches(cls.DisabledReason ?? "", @"ULDA_\w+_Deck\d((?: [A-Z0-9]+_[A-Za-z0-9_]+)+) not scripted"))
                foreach (string id in seg.Groups[1].Value.Trim().Split(' '))
                {
                    named++;
                    var alts = new List<string> { "CORE_" + id };
                    if (alternates.TryGetValue(id, out var l)) alts.AddRange(l);
                    if (Scripted(id) || alts.Any(Scripted)) wrong.Add(cls.Id + ":" + id);
                }
        // Phase 6 F10 scripted every id the disabled rows named: no row ships disabled, so no reason names one.
        Check("disabledReason ids are unscripted with no scripted reprint (the catalog's resolution rule); since phase 6 F10 no row ships disabled",
            named == 0 && wrong.Count == 0 && cdata.DisabledClasses.Count == 0, $"{named} named; {string.Join(",", wrong)}");
    }
    {
        // Fewer than 3 candidates: 1 or 2 offers; none: no draft opens.
        var few = CatalogData();
        var keep = few.Bundles.Where(b => b.Class == "WARLOCK" && b.IsPool).Take(2).Select(b => b.Id).ToList();
        few.Bundles.RemoveAll(b => !keep.Contains(b.Id));
        var rf = new DungeonRun(few);
        var s = rf.NewRun(call, "WARLOCK", "warlock_fiendpact", "CS2_056", 2718, signatureId: rf.SignatureOffer(call, rf.Data.FindClass("WARLOCK"), 2718).FirstOrDefault());
        rf.BeginMatch(s); rf.RecordOutcome(s, new DungeonRunProfile(), DungeonRunOutcome.Win);
        if (s.Phase == DungeonRunPhase.TreasureDraft) rf.PickTreasure(s, s.OpenDraft.Offered[0]);
        Check("two candidates: a two-offer draft", s.Phase == DungeonRunPhase.BundleDraft && s.OpenDraft.Offered.Count == 2 && s.OpenDraft.Offers.Count == 2);
        few.Bundles.Clear();
        var s0 = rf.NewRun(call, "WARLOCK", "warlock_fiendpact", "CS2_056", 2719, signatureId: rf.SignatureOffer(call, rf.Data.FindClass("WARLOCK"), 2719).FirstOrDefault());
        rf.BeginMatch(s0); rf.RecordOutcome(s0, new DungeonRunProfile(), DungeonRunOutcome.Win);
        if (s0.Phase == DungeonRunPhase.TreasureDraft) rf.PickTreasure(s0, s0.OpenDraft.Offered[0]);
        Check("no candidate: no bundle draft, the run advances", s0.Phase == DungeonRunPhase.PreMatch && s0.Floor == 2 && !s0.Drafts.Any(d => d.Kind == "bundle"));
    }
    {
        // Cooldowns: the id is everything before the last '@'; a malformed entry is a data error, never a parse crash.
        var s = CNew("ROGUE", 1);
        s.BundleCooldowns.AddRange(new[] { "bundle_rogue_spells@3", "dr.rogue.x@1" });
        Check("cooldown parser: id before the last '@', active while its floor is ahead", DungeonRun.Cooling(s).SetEquals(new[] { "bundle_rogue_spells" }));
        s.BundleCooldowns.Add("broken@soon");
        Throws<InvalidDataException>("a malformed cooldown is an InvalidDataException", () => DungeonRun.Cooling(s));
        s.BundleCooldowns[s.BundleCooldowns.Count - 1] = "noseparator";
        Throws<InvalidDataException>("a cooldown with no '@' is an InvalidDataException", () => DungeonRun.Cooling(s));
    }
    {
        // A legacy run (CatalogVersion 0) on catalog data drafts exactly as on legacy data.
        bool same = true;
        foreach (string cls in new[] { "MAGE", "WARRIOR", "DEATHKNIGHT" })
        {
            var a = NewFor(all, cls, 6060); var b = DungeonRunStore.Clone(new DungeonRunSave { Active = a }).Active;
            PlayOut(new DungeonRunProfile(), a, (_, __) => DungeonRunOutcome.Win);
            CPlay(crun, b);
            same &= Xml(a) == Xml(b) && b.CatalogVersion == 0 && b.Drafts.All(d => d.Offers.Count == 0);
        }
        Check("a legacy run on catalog data drafts byte for byte as on legacy data (fixed bundles, no stored offers)", same);
    }
    {
        // Version 2 -> 3: a version-2 file (a legacy run with an open legacy draft) upgrades in value-free steps, keeps a
        // dungeon-run.v2.xml backup, and the legacy draft still picks from the fixed bundles.
        var s = NewFor(all, "PALADIN", 5150);
        while (s.Phase != DungeonRunPhase.BundleDraft) { if (s.Phase == DungeonRunPhase.PreMatch) { run.BeginMatch(s); run.RecordOutcome(s, new DungeonRunProfile(), DungeonRunOutcome.Win); } else run.PickTreasure(s, s.OpenDraft.Offered[0]); }
        var v2 = new DungeonRunSave { Version = 2, Active = s };
        string dir = Path.Combine(Path.GetTempPath(), "dr-v2-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string text = Xml(v2);
        Check("a version-2 file has no version-3 field", text.Contains("Version=\"2\"") && !text.Contains("CatalogVersion") && !text.Contains("<Offers") && !text.Contains("BucketTakes"));
        DungeonRunStore.Save(dir, v2); // a Version="2" file, as a version-2 plugin wrote it
        byte[] original = File.ReadAllBytes(Path.Combine(dir, DungeonRunStore.FileName));
        var loaded = DungeonRunStore.Load(dir);
        var log2 = new List<string>();
        bool up = DungeonRunMigration.Upgrade(loaded, run, log2.Add);
        Check("v2 -> v6: upgraded, CatalogVersion 0 stamped, the legacy draft keeps no stored offers, logged", up && loaded.LoadedVersion == 2 && loaded.Version == 6
            && loaded.Active.CatalogVersion == 0 && loaded.Active.OpenDraft.Offers.Count == 0 && log2.Any(l => l.Contains("from version 2 to 6")), string.Join(" | ", log2));
        Check("v2 -> v6 changes no value: the run serializes as before but for the version", Xml(loaded).Replace("Version=\"6\"", "Version=\"2\"") == text);
        Check("a second upgrade does nothing", !DungeonRunMigration.Upgrade(loaded, run, log2.Add));
        DungeonRunStore.Save(dir, loaded);
        string backup = Path.Combine(dir, DungeonRunStore.BackupName(2));
        Check("first save of the upgraded file keeps it as dungeon-run.v2.xml", File.Exists(backup) && File.ReadAllBytes(backup).SequenceEqual(original) && !File.Exists(Path.Combine(dir, DungeonRunStore.V1BackupName)));
        var reloaded = DungeonRunStore.Load(dir);
        string offered = reloaded.Active.OpenDraft.Offered[0];
        run.PickBundle(reloaded.Active, offered);
        Check("the migrated legacy draft picks from the fixed bundles", reloaded.Version == 6 && reloaded.Active.TakenBundles.Contains(offered)
            && reloaded.Active.Deck.Skip(reloaded.Active.Deck.Count - 3).SequenceEqual(data.LegacyBundles.First(b => b.Id == offered).Cards));
        Directory.Delete(dir, true);
    }
    {
        // A catalog run's matches start and play (random legal play) for every playable class, MAGE_ROGUE included.
        int cm = 0, cf = 0; var clog = new List<string>();
        foreach (var cls in cdata.Classes)
        {
            var s = crun.NewRun(call, cls.Id, cls.DefaultDeck, cls.DefaultPower, 123 + cm, signatureId: crun.SignatureOffer(call, crun.Data.FindClass(cls.Id), 123 + cm).FirstOrDefault());
            while (s.Floor < 7)
            {
                if (s.Phase == DungeonRunPhase.PreMatch) { crun.BeginMatch(s); crun.RecordOutcome(s, new DungeonRunProfile(), DungeonRunOutcome.Win); }
                else if (s.Phase == DungeonRunPhase.TreasureDraft) crun.PickTreasure(s, s.OpenDraft.Offered[0]);
                else crun.PickBundle(s, s.OpenDraft.Offered[s.Floor % s.OpenDraft.Offered.Count]);
            }
            var plan = crun.BuildPlan(s);
            var cfg = new GameConfig { RandomSeed = crun.MatchSeed(s), Shuffle = true, SkipMulligan = false, History = true, Logging = false, FormatType = FormatType.FT_WILD };
            DungeonRunSetup.Configure(cfg, plan);
            try
            {
                var g = new Game(cfg); g.StartGame(); DungeonRunSetup.ApplyAfterStart(g, plan);
                g.Process(ChooseTask.Mulligan(g.Player1, new List<int>())); g.Process(ChooseTask.Mulligan(g.Player2, new List<int>())); g.MainBegin();
                var rnd = new Random(cm); int steps = 0;
                while (g.State != State.COMPLETE && steps++ < 600 && g.Turn < 60)
                {
                    var options = g.CurrentPlayer.Options();
                    var end = options.FirstOrDefault(o => o is EndTurnTask);
                    g.Process(end == null || (options.Count > 1 && rnd.Next(5) > 0) ? options[rnd.Next(options.Count)] : end);
                }
                cm++;
            }
            catch (Exception e) { cf++; clog.Add($"{cls.Id}: {e.GetType().Name}: {e.Message.Split('\n')[0]}"); }
        }
        Check($"catalog-run matches start and play without engine faults ({cm} games, every playable class)", cf == 0 && cm == cdata.Classes.Count, string.Join(" | ", clog));
    }
}

Console.WriteLine($"{passes} passed, {failures} failed");
return failures;
