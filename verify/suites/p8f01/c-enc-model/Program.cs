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

// Phase 8 family 01, the encounter model's run layer: the encounter parser and validator, the schedule version 3 shapes and
// the released sequences (golden), the provenance-mix draw, the anomaly node and twist roll, save version 6 and its migration,
// the run-owned fight state (hero switch, persistent boss damage, Battle Scars) and the match plan of each encounter.
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
string golden = args.Length > 0 ? args[0] : VerifyPaths.Fixture("p8f01");
string R(string f) => File.ReadAllText(Path.Combine(root, f));
string scheduleJson = R("dungeon-run/schedule-v1.json"), encountersJson = R("dungeon-run/encounters-v1.json");
DungeonRunData Load(string schedule = null, string encounters = null, bool withEncounters = true) => DungeonRunData.Parse(R("dungeon-run/run-v1.json"),
    R("dungeon-run/pool-v1.json"), R("npc/decks-v1.json"), R("dungeon-run/catalog-v1.json"), schedule ?? scheduleJson, R("dungeon-run/tavern-v1.json"),
    withEncounters ? encounters ?? encountersJson : null);
var data = Load();
var run = new DungeonRun(data);
DungeonRunProfile All(DungeonRunData d = null) { var p = new DungeonRunProfile(); p.Unlocked.AddRange((d ?? data).Unlocks.Keys); return p; }
var all = All();
var xml = new System.Xml.Serialization.XmlSerializer(typeof(DungeonRunSave));
string Xml(DungeonRunSave save) { using var w = new StringWriter(); xml.Serialize(w, save); return w.ToString(); }
string XmlBytes(DungeonRunSave save) { using var m = new MemoryStream(); xml.Serialize(m, save); return System.Text.Encoding.UTF8.GetString(m.ToArray()); }

// Walks a run to the pre-match of a floor (first offers; anomaly nodes take pick(offer) or "none").
DungeonRunState Reach(DungeonRun r, DungeonRunState s, int floor, Func<DungeonRunModifierOffer, string> anomaly = null)
{
    var scratch = new DungeonRunProfile();
    while (!(s.Floor == floor && s.Phase == DungeonRunPhase.PreMatch))
    {
        switch (s.Phase)
        {
            case DungeonRunPhase.PreMatch: r.BeginMatch(s); r.RecordOutcome(s, scratch, DungeonRunOutcome.Win); break;
            case DungeonRunPhase.TreasureDraft: r.PickTreasure(s, s.OpenDraft.Offered[0]); break;
            case DungeonRunPhase.Modifier:
                r.PickModifier(s, s.ModifierOffer.Kind == DungeonRunGlobalRule.KindAnomaly ? anomaly?.Invoke(s.ModifierOffer) ?? DungeonRunGlobalRule.None : s.ModifierOffer.Offered[0]);
                break;
            case DungeonRunPhase.Tavern: r.LeaveTavern(s); break;
            case DungeonRunPhase.BundleDraft:
                if (s.OpenDraft.Takes == 0) { r.PickBundle(s, s.OpenDraft.Offered[0]); break; }
                string bundle = s.OpenDraft.Choosing ?? s.OpenDraft.Offered[0];
                var left = r.OfferCards(s.OpenDraft, bundle);
                foreach (string kept in s.OpenDraft.Chosen) left.Remove(kept);
                r.PickDraftCard(s, bundle, left[0]);
                break;
            default: throw new Exception("walk stopped at " + s.Phase);
        }
    }
    return s;
}
DungeonRunState V3(DungeonRun r, string cls, long seed, string schedule = "acts-v3h", bool typed = false)
{
    var c = r.Data.Class(cls);
    return r.NewRun(All(r.Data), c.Id, c.Decks[0], c.Powers[0], seed, typedSeed: typed, schedule: schedule);
}
int FloorOfBoss(DungeonRun r, DungeonRunState s, string bossId) => s.BossSequence.IndexOf(bossId) + 1;
// A run at the pre-match of the given boss (put on the first floor of its pool, as the bosspilot harness does).
DungeonRunState At(DungeonRun r, string cls, long seed, string bossId, int floor, bool typed = false)
{
    var s = Reach(r, V3(r, cls, seed, typed: typed), floor);
    s.BossSequence[floor - 1] = bossId;
    return s;
}

// ============================================================ A. the shipped data
Guard("A", () =>
{
    var errors = DungeonRunValidator.Validate(data, null);
    Check("A1 the six data files validate together", errors.Count == 0, string.Join(" | ", errors.Take(4)));
    var old = Load(withEncounters: false);
    var oldErrors = DungeonRunValidator.Validate(old, null);
    Check("A2 the data without the encounter file validates and holds the 20 custom bosses only", oldErrors.Count == 0 && old.Bosses.Count == 20 && old.EncountersVersion == 0,
        string.Join(" | ", oldErrors.Take(3)));
    Check("A3 the encounters follow the custom bosses, whose order is unchanged", data.Bosses.Take(20).Select(b => b.Id).SequenceEqual(old.Bosses.Select(b => b.Id))
        && data.Bosses.Skip(20).All(b => b.IsEncounter) && data.Bosses.Take(20).All(b => !b.IsEncounter && b.Tier == null));
    Check("A4 family 01's eight encounters come first, in their order: one per mechanism and the four Secret bosses; later families append their rows (wave 2: family 07's eighteen and family 08's seventeen; wave 3: family 09's eleven and family 10's fifteen; family 20's eighteen; the eight are filed under their mechanism's family: five 01, one each 13, 23, 24)", data.Encounters.Take(8).Select(b => b.Id).SequenceEqual(new[] { "ulda_40_icarax", "gila_61_hagatha", "dala_21_whirt", "trl_200_voone",
        "loot_35_brandlemar", "loot_48_trapped_room", "gila_25_ivan", "ulda_44_trap_room" }) && data.Encounters.Skip(8).All(b => b.Family != "01")
        && string.Join(",", data.Encounters.GroupBy(b => b.Family).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => g.Key + ":" + g.Count())) == "01:5,07:18,08:17,09:11,10:15,13:1,20:18,23:1,24:1",
        string.Join(",", data.Encounters.GroupBy(b => b.Family).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => g.Key + ":" + g.Count())));
    var icarax = data.Boss("ulda_40_icarax"); var hagatha = data.Boss("gila_61_hagatha"); var whirt = data.Boss("dala_21_whirt"); var voone = data.Boss("trl_200_voone");
    Check("A5 Icarax: 300 fixed Health, two more forms at 200 and 100 with their own heroes, powers and decks", icarax.Health == 300 && icarax.FixedHealth && icarax.Forms.Count == 2
        && icarax.Forms[0].HealthAtMost == 200 && icarax.Forms[1].HealthAtMost == 100 && icarax.Forms[0].Hero == "ULDA_BOSS_40h2" && icarax.Forms[1].Power == "ULDA_BOSS_40p3"
        && icarax.Forms.All(f => f.KeepExcess && f.Draw == 3 && data.BossDecks.ContainsKey(f.Deck)) && icarax.HasFlag("persistentDamage") && icarax.Pools.SequenceEqual(new[] { "enc-major3" }));
    Check("A6 Hagatha: 100 fixed Health, 2 start Mana Crystals, a 40-card deck, two hero cards with 12 cards and 3 passives each",
        hagatha.Health == 100 && hagatha.Start?.BossMana == 2 && data.BossDecks[hagatha.Deck].Count == 40 && hagatha.HeroCards.Count == 2
        && hagatha.HeroCards.All(h => h.Deck.Count == 12 && h.Passives.Count == 3 && h.SkipFor.Count == 1) && hagatha.Power == "GIL_504h" && hagatha.Pools[0] == "enc-major2");
    Check("A7 Whirt: tier 3, 30 cards, the boss's generated Secrets on", whirt.Tier == "3" && whirt.BossGeneratedSecrets && data.BossDecks[whirt.Deck].Count == 30 && whirt.Pools[0] == "enc-t3");
    Check("A8 Voone: tier 2, three shrine variants (the Warrior shrines), each a 30-card deck", voone.Variants.Select(v => v.Shrine).SequenceEqual(new[] { "TRLA_171", "TRLA_170", "TRLA_104" })
        && voone.Variants.All(v => data.BossDecks[v.Deck].Count == 30) && voone.HasFlag("shrine"));
    Check("A9 every deck states its kind and source; a deck is authentic exactly when nothing was substituted and it is not a curated one (family 09: the Treasure Vault, which has no deck on the wiki)",
        data.Encounters.All(b => b.Source != null && b.Source.Url != null && b.Source.Read != null && (b.Source.Kind == "authentic") == (b.Source.Substitutions.Count == 0 && b.Source.Kind != "curated")));
    Check("A10 the rejected list names Toki's hero card with its reason and lift", data.RejectedEncounters.Count == 1 && data.RejectedEncounters[0].Id == "GILA_900h2"
        && data.RejectedEncounters[0].Reason.Length > 0 && data.RejectedEncounters[0].Lift.StartsWith("phase 9"));
    Check("A11 PoolOf maps tiers to pools", DungeonRunEncounterData.PoolOf("1") == "enc-t1" && DungeonRunEncounterData.PoolOf("major2") == "enc-major2" && DungeonRunEncounterData.PoolOf("finale") == "finale");
});

// ============================================================ B. validator errors (each mutation of a fresh load must be refused)
void Refused(string name, Action<DungeonRunData> mutate, string expect, string schedule = null, string encounters = null)
{
    Guard(name, () =>
    {
        var d = Load(schedule, encounters);
        mutate(d);
        var errors = DungeonRunValidator.Validate(d, null);
        Check(name, errors.Any(e => e.Contains(expect)), errors.Count + " errors; first: " + (errors.FirstOrDefault() ?? "(none)"));
    });
}
Refused("B1 an unknown tier", d => d.Boss("dala_21_whirt").Tier = "9", "unknown tier");
Refused("B2 an unknown flag", d => d.Boss("dala_21_whirt").Flags.Add("sparkly"), "unknown flag sparkly");
Refused("B3 a combat-tier deck that is not 30 cards", d => d.BossDecks["enc-dala_21_whirt"].RemoveAt(0), "expected 30");
Refused("B4 a major's deck under 10 cards", d => d.BossDecks["enc-gila_61_hagatha"].RemoveRange(0, 35), "expected 10 to 60");
// GILA_BOSS_48t Alternate Timeline stays unscripted in phase 8 (rejected to phase 9); GILA_BOSS_27t Amalgamate, used here before, is family 10's now.
Refused("B5 an unscripted deck card", d => d.BossDecks["enc-dala_21_whirt"][0] = "GILA_BOSS_48t", "not scripted");
Refused("B6 a card that needs the boss's generated Secrets on a row that keeps them off", d => d.Boss("dala_21_whirt").BossGeneratedSecrets = false, "it would do nothing");
Refused("B7 bossGeneratedSecrets with no card that generates one", d => d.Boss("trl_200_voone").BossGeneratedSecrets = true, "no card that generates a Secret");
Refused("B8 persistentDamage with no Health of its own", d => { var b = d.Boss("ulda_40_icarax"); b.Health = null; b.FixedHealth = false; }, "persistentDamage needs");
Refused("B9 form thresholds that do not fall", d => d.Boss("ulda_40_icarax").Forms[1].HealthAtMost = 250, "does not fall below");
Refused("B10 a form with two entry conditions", d => d.Boss("ulda_40_icarax").Forms[0].OnDeath = true, "needs exactly one");
Refused("B11 flag forms without forms", d => d.Boss("dala_21_whirt").Flags.Add("forms"), "flag forms and the forms");
Refused("B12 a shrine variant the table does not admit for a boss", d => d.Boss("trl_200_voone").Variants[0].Shrine = "TRLA_115", "admission board-shrine");
Refused("B13 an unscripted shrine", d => d.Boss("trl_200_voone").Variants[0].Shrine = "TRLA_200h", "not a scripted board shrine");
Refused("B14 a hero card that is not scripted as one", d => d.Boss("gila_61_hagatha").HeroCards[0].Hero = "GILA_900h2", "not scripted as a fight-only hero card");
Refused("B15 a hero card passive that is no passive treasure", d => d.Boss("gila_61_hagatha").HeroCards[0].Passives[0] = "CS2_029", "is not a passive treasure");
Refused("B16 flag heroSwitch without hero cards", d => d.Boss("gila_61_hagatha").HeroCards.Clear(), "flag heroSwitch");
Refused("B17 a relay on a row that is no finale", d => { var b = d.Boss("dala_21_whirt"); b.Flags.Add("relay"); b.Relay.Add(new DungeonRunRelayHero { Hero = "HERO_08", Power = "CS2_034", Deck = "enc-dala_21_whirt", Health = 30 }); }, "a relay belongs to a finale row");
Refused("B18 a start board card that is no minion", d => d.Boss("dala_21_whirt").Start = new DungeonRunStart { BossBoard = { "CS2_029" } }, "a start board needs minions");
Refused("B19 an encounter with a band", d => d.Boss("dala_21_whirt").Band = 1, "an encounter has no band");
Refused("B20 an unknown provenance", d => d.Boss("dala_21_whirt").Provenance = "KARA", "unknown provenance");
Refused("B21 a deck kind that hides its substitutions", d => d.Encounters.First(b => b.Source.Kind != DungeonRunDeckSource.KindAuthentic).Source.Kind = "authentic", "does not match its");
Refused("B22 an admitted rule with no admission row", d => d.Schedules.Rules.Add(new DungeonRunGlobalRule { Id = "CS2_182" }), "no admission row");
Refused("B23 an admitted match rule that is not scripted", d => d.Schedules.Rules.Add(new DungeonRunGlobalRule { Id = "CS2_120" }), "not scripted");
Refused("B24 a rejected rule with no reason", d => d.Schedules.Rules.Add(new DungeonRunGlobalRule { Id = "DALA_854", Status = "rejected" }), "states its reason");
Refused("B25 a rule's unknown boss flag", d => d.Schedules.Rules[0].IncompatibleFlags.Add("sparkly"), "unknown boss flag");
Refused("B26 a boss's unknown incompatible rule", d => d.Boss("dala_21_whirt").IncompatibleRules.Add("DALA_999"), "incompatibleRules names unknown rule");
Refused("B27 a pool set that nests a pool set", d => d.Schedules.PoolSets["mix-1"].Add("mix-2"), "sets do not nest");
Refused("B28 an unknown default", d => d.Schedules.Defaults.Add("acts-v9"), "defaults names unknown schedule");
Refused("B29 an enabled finale naming a boss that is no finale row", d => { }, "is not a finale row",
    schedule: scheduleJson.Replace("\"finale\": {\"enabled\": false, \"fixedBosses\": [], \"heroRelay\": false, \"tavernBefore\": true,\n                 \"id\": \"IV\", \"name\": \"The Heist\"",
        "\"finale\": {\"enabled\": true, \"fixedBosses\": [\"dala_21_whirt\"], \"heroRelay\": false, \"tavernBefore\": true,\n                 \"id\": \"IV\", \"name\": \"The Heist\""));
Refused("B30 a version-3 schedule stamped 2", d => d.Schedules.Find("acts-v3h").StampVersion = 2, "never released it");
Refused("B31 a twist pool naming an anomaly", d => { var a = d.Schedules.Find("acts-v3h").Acts[0]; a.TwistPool.Add("DALA_866"); a.TwistChance = 50; }, "which is not a twist rule");
Guard("B32", () =>
{
    bool threw = false;
    try { Load(encounters: encountersJson.Replace("\"id\": \"dala_21_whirt\"", "\"id\": \"tunnel_rat\"")); } catch (InvalidDataException e) { threw = e.Message.Contains("already exists"); }
    Check("B32 an encounter that reuses a custom boss id is refused by the parser", threw);
});

// ============================================================ C. schedule shapes
Guard("C", () =>
{
    var sch = data.Schedules;
    Check("C1 scheduleVersion 3; the default is still acts-v2 and no defaults are rolled yet", sch.ScheduleVersion == 3 && sch.Default == "acts-v2" && sch.Defaults.Count == 0);
    foreach (var pair in DungeonRunSchedules.ReleasedShapes[2])
        Check("C2 " + pair.Key + " keeps its version-2 shape", DungeonRunSchedules.ShapeDigest(sch.Find(pair.Key)) == pair.Value, DungeonRunSchedules.ShapeDigest(sch.Find(pair.Key)));
    foreach (string id in new[] { "acts-v3h", "acts-v3t" })
        Check("C3 " + id + " has its pinned version-3 shape", DungeonRunSchedules.ReleasedShapes[3][id] == DungeonRunSchedules.ShapeDigest(sch.Find(id)), DungeonRunSchedules.ShapeDigest(sch.Find(id)));
    Check("C4 the three older schedules are stamped 2, the two new ones 3", new[] { "acts-v1", "acts-v2", "act1" }.All(id => sch.Find(id).StampVersion == 2)
        && sch.Find("acts-v3h").StampVersion == 3 && sch.Find("acts-v3t").StampVersion == 3);
    var v3 = sch.Find("acts-v3h");
    Check("C5 acts-v3h: 15 floors, an anomaly node opening each act, the provenance mix", v3.FloorCount == 15 && v3.MixesProvenance
        && v3.Nodes.Count(n => n.Kind == DungeonRunNodeKind.Anomaly) == 3 && v3.Nodes[0].Kind == DungeonRunNodeKind.Anomaly && v3.Nodes[0].Floor == 1,
        DungeonRunSchedules.Shape(v3));
    Check("C6 acts-v3t shares acts-v3h's acts", sch.Find("acts-v3t").Acts.SequenceEqual(v3.Acts) && DungeonRunSchedules.Shape(sch.Find("acts-v3t")) == DungeonRunSchedules.Shape(v3));
    Check("C7 the older schedules have no anomaly node, pool set or mix", new[] { "acts-v1", "acts-v2", "act1" }.All(id => !sch.Find(id).MixesProvenance
        && sch.Find(id).Nodes.All(n => n.Kind != DungeonRunNodeKind.Anomaly) && sch.Find(id).Acts.All(a => a.Combat.All(c => !sch.PoolSets.ContainsKey(c.Pool)))));
    Check("C8 pool sets resolve as unions (mix-4 = band2 + enc-t3; leftover inside a set keeps its meaning)",
        sch.Member(data.Boss("dala_21_whirt"), "mix-4") && sch.Member(data.Boss("golemancer"), "mix-4") && !sch.Member(data.Boss("tunnel_rat"), "mix-4")
        && sch.Member(data.Boss("tunnel_rat"), "mix-5") && !sch.Member(data.Boss("devouring_maw"), "mix-5") && sch.Member(data.Boss("ulda_40_icarax"), "major-3")
        && sch.Member(data.Boss("devouring_maw"), "major-3"));
    // A finale enabled by fixture data becomes one synthesized last act.
    var fx = Load(scheduleJson.Replace("\"finale\": {\"enabled\": false, \"fixedBosses\": [], \"heroRelay\": false, \"tavernBefore\": true,\n                 \"id\": \"IV\", \"name\": \"The Heist\", \"bossHealth\": [70], \"bossMana\": [3]",
        "\"finale\": {\"enabled\": true, \"fixedBosses\": [\"dala_21_whirt\", \"trl_200_voone\"], \"heroRelay\": false, \"tavernBefore\": true,\n                 \"id\": \"IV\", \"name\": \"The Heist\", \"bossHealth\": [70, 75], \"bossMana\": [3, 3]"));
    var fh = fx.Schedules.Find("acts-v3h");
    Check("C9 an enabled finale is a fourth act: its bosses in order, a tavern first, no anomaly, 17 floors", fh.Acts.Count == 4 && fh.Acts[3].IsFinale && fh.FloorCount == 17
        && fh.Acts[3].FixedBosses.SequenceEqual(new[] { "dala_21_whirt", "trl_200_voone" }) && fh.Nodes.Count(n => n.Act == 4) == 3
        && fh.Nodes.First(n => n.Act == 4).Kind == DungeonRunNodeKind.Tavern && fh.Nodes.Last().Kind == DungeonRunNodeKind.Major && fh.Nodes.Last().Floor == 17);
    Check("C10 the enabled finale changes acts-v3h's shape only (acts-v3t shares the three acts, not the finale)",
        DungeonRunSchedules.ShapeDigest(fh) != DungeonRunSchedules.ReleasedShapes[3]["acts-v3h"] && fx.Schedules.Find("acts-v3t").Acts.Count == 3
        && DungeonRunSchedules.ShapeDigest(fx.Schedules.Find("acts-v3t")) == DungeonRunSchedules.ReleasedShapes[3]["acts-v3t"]);
    var fr = new DungeonRun(fx);
    var fs = V3(fr, "MAGE", 5150);
    Check("C11 a run on it pre-rolls 17 bosses ending with the fixed two, and fights them after a tavern with no draft in between", fs.BossSequence.Count == 17
        && fs.BossSequence.Skip(15).SequenceEqual(new[] { "dala_21_whirt", "trl_200_voone" }) && fs.BossSequence.Take(15).All(id => id != "dala_21_whirt" && id != "trl_200_voone"));
    Reach(fr, fs, 16);
    fr.BeginMatch(fs); fr.RecordOutcome(fs, new DungeonRunProfile(), DungeonRunOutcome.Win);
    Check("C12 the finale's first win leads straight to its next fight", fs.Phase == DungeonRunPhase.PreMatch && fs.Floor == 17 && fr.CurrentBoss(fs).Id == "trl_200_voone" && fr.BossHealth(fs) == 75);
    fr.BeginMatch(fs); fr.RecordOutcome(fs, new DungeonRunProfile(), DungeonRunOutcome.Win);
    Check("C13 the finale's last win wins the run", fs.Phase == DungeonRunPhase.Victory);
});

// ============================================================ D. the released sequences are byte-identical
Guard("D", () =>
{
    var lines = new List<string>();
    var none = new DungeonRunProfile();
    foreach (var (name, profile) in new[] { ("none", none), ("all", all) })
        for (long seed = 1; seed <= 200; seed++)
        {
            long s = seed * 7919 + 13;
            lines.Add($"legacy {name} {s} {string.Join(",", run.BossSequence(profile, s))}");
            foreach (string id in new[] { "acts-v1", "acts-v2", "act1" })
                lines.Add($"{id} {name} {s} {string.Join(",", run.ActBossSequence(profile, s, data.Schedules.Find(id)))}");
        }
    foreach (var cls in data.Classes.Where(c => !c.IsHero && !c.IsDual))
        for (long seed = 1; seed <= 20; seed++)
            foreach (string id in new[] { null, "legacy", "acts-v1", "acts-v2", "act1" })
            {
                var st = run.NewRun(all, cls.Id, cls.Decks[0], cls.Powers[0], seed * 104729 + 7, schedule: id);
                lines.Add($"newrun {id ?? "(default)"} {cls.Id} {seed} v{st.ScheduleVersion} {st.Schedule ?? "-"} node {st.Node} floor {st.Floor} {st.Phase} rules {st.ActiveRules.Count} shrine {st.BoardShrine ?? "-"} | {string.Join(",", st.BossSequence)}");
            }
    var expected = File.ReadAllLines(Path.Combine(golden, "golden-sequences.txt"));
    int differ = Enumerable.Range(0, Math.Max(lines.Count, expected.Length)).Count(i => i >= lines.Count || i >= expected.Length || lines[i] != expected[i]);
    Check("D1 legacy, acts-v1, acts-v2 and act1 boss sequences for 200 seeds x 2 unlock states and 1100 new runs match the pre-phase-8 recording (2700 lines)",
        differ == 0 && expected.Length == 2700, differ + " lines differ of " + expected.Length);
    Check("D2 a new run with no named schedule is still an acts-v2 run stamped 2", run.NewRun(all, "MAGE", data.Class("MAGE").Decks[0], data.Class("MAGE").Powers[0], 99).Schedule == "acts-v2"
        && run.NewRun(all, "MAGE", data.Class("MAGE").Decks[0], data.Class("MAGE").Powers[0], 99).ScheduleVersion == 2);
});

// ============================================================ E. the provenance-mix draw
Guard("E", () =>
{
    var v3 = data.Schedules.Find("acts-v3h");
    bool distinct = true, known = true, deterministic = true, whirtActs = true, majors = true, voone = true; int withWhirt = 0, withVoone = 0, hagatha = 0, icarax = 0;
    // A major slot follows the mix as every slot does: it takes the provenance the run holds fewer bosses of so far (the
    // encounter major or a custom boss; a tie is the slot's seeded order). Family 10 put Monster Hunt rows into the combat
    // tiers, so Hagatha is no longer the act II major of every run (she was while no other GIL row could be drawn before her).
    bool MajorByMix(List<string> seq, int at, string encounter, string customPool)
    {
        string provenance = data.Boss(encounter).Provenance;
        int ofEncounter = seq.Take(at).Count(id => data.Boss(id).Provenance == provenance), ofCustom = seq.Take(at).Count(id => !data.Boss(id).IsEncounter);
        bool isEncounter = seq[at] == encounter, isCustom = !data.Boss(seq[at]).IsEncounter && data.Boss(seq[at]).Pools.Contains(customPool);
        return (isEncounter || isCustom) && (ofEncounter == ofCustom || isEncounter == (ofEncounter < ofCustom));
    }
    for (long seed = 1; seed <= 1000; seed++)
    {
        var seq = run.ActBossSequence(all, seed * 31 + 5, v3);
        distinct &= seq.Count == 15 && seq.Distinct().Count() == 15;
        known &= seq.All(id => data.Bosses.Any(b => b.Id == id));
        deterministic &= seq.SequenceEqual(run.ActBossSequence(all, seed * 31 + 5, v3));
        int w = seq.IndexOf("dala_21_whirt"), vo = seq.IndexOf("trl_200_voone");
        if (w >= 0) { withWhirt++; whirtActs &= w >= 7 && w <= 11 && w != 9; }          // mix-4 (floors 8-9) or mix-5 (floors 11-12)
        if (vo >= 0) { withVoone++; voone &= vo >= 2 && vo <= 6 && vo != 4; }             // mix-2 (floors 3-4) or mix-3 (floors 6-7)
        // Family 09: the act I major is a custom band-3 boss or one of the five Dungeon Run final bosses (enc-major1).
        majors &= MajorByMix(seq, 9, "gila_61_hagatha", "band3") && MajorByMix(seq, 14, "ulda_40_icarax", "final") && (data.Boss(seq[4]).Pools.Contains("band3") || data.Boss(seq[4]).Pools.Contains("enc-major1"));
        if (seq[9] == "gila_61_hagatha") hagatha++;
        if (seq[14] == "ulda_40_icarax") icarax++;
    }
    Check("E1 1000 seeds: 15 distinct known bosses each, and the same seed gives the same sequence", distinct && known && deterministic);
    Check("E2 an encounter lands only on floors whose pool set holds its tier", whirtActs && voone, $"whirt in {withWhirt}, voone in {withVoone}");
    Check("E3 the least-used provenance is drawn first: every run meets Whirt and Voone (the only Heist and Rumble rows), and each major is of the provenance the run holds fewer bosses of (act II: Hagatha or a custom band3 boss; act III: Icarax or a custom final)",
        withWhirt == 1000 && withVoone == 1000 && majors && hagatha > 0 && icarax > 0, $"whirt in {withWhirt}, voone in {withVoone}, Hagatha the act II major in {hagatha}, Icarax the act III major in {icarax}");
    // Persistent damage steers the act III major on a run that reads the ledger: a second lord (a fixture clone) with banked damage is taken first.
    var d2 = Load(encounters: encountersJson);
    var clone = d2.Boss("ulda_40_icarax");
    d2.Bosses.Add(new DungeonRunBoss { Id = "ulda_zz_fixture", Name = "Fixture Lord", Provenance = "ULDA", Tier = "major3", Pools = { "enc-major3" }, HeroCard = clone.HeroCard, Power = clone.Power,
        Deck = clone.Deck, Health = 300, FixedHealth = true, Flags = { "persistentDamage" }, Archetype = "midrange", Source = clone.Source });
    var r2 = new DungeonRun(d2);
    var banked = All(d2); banked.BossDamage.Add(new DungeonRunBossDamage { Boss = "ulda_zz_fixture", Remaining = 120, Attempts = 1 });
    int bankedFirst = 0, freshFixture = 0;
    for (long seed = 1; seed <= 200; seed++)
    {
        if (r2.ActBossSequence(banked, seed, d2.Schedules.Find("acts-v3h"), ledger: true)[14] == "ulda_zz_fixture") bankedFirst++;
        if (r2.ActBossSequence(banked, seed, d2.Schedules.Find("acts-v3h"), ledger: false)[14] == "ulda_zz_fixture") freshFixture++;
    }
    Check("E4 a ledger run's act III major is the lord with banked damage; a run that does not read the ledger draws it from the seed", bankedFirst == 200 && freshFixture > 40 && freshFixture < 160, $"banked {bankedFirst}, no-ledger {freshFixture}");
    var typed = r2.NewRun(banked, "MAGE", d2.Class("MAGE").Decks[0], d2.Class("MAGE").Powers[0], 12, typedSeed: true, schedule: "acts-v3h");
    var random = r2.NewRun(banked, "MAGE", d2.Class("MAGE").Decks[0], d2.Class("MAGE").Powers[0], 12, schedule: "acts-v3h");
    Check("E5 NewRun reads the ledger on a random seed only", random.BossSequence[14] == "ulda_zz_fixture" && typed.BossSequence.SequenceEqual(r2.ActBossSequence(banked, 12, d2.Schedules.Find("acts-v3h"))));
    Check("E6 a run on acts-v3h is stamped 3; ScheduleFor loads a stamped-2 run under the version-3 file", random.ScheduleVersion == 3
        && run.ScheduleFor(run.NewRun(all, "MAGE", data.Class("MAGE").Decks[0], data.Class("MAGE").Powers[0], 3, schedule: "acts-v1")).Id == "acts-v1");
    // The rolled default: a fixture with "defaults".
    var d3 = Load(scheduleJson.Replace("\"defaultsPending\"", "\"defaults\""));
    var r3 = new DungeonRun(d3);
    var picked = Enumerable.Range(1, 200).Select(i => r3.NewRun(All(d3), "MAGE", d3.Class("MAGE").Decks[0], d3.Class("MAGE").Powers[0], i).Schedule).ToList();
    Check("E7 with \"defaults\" a new run rolls its schedule from the seed (both appear, the same seed the same one)", picked.Contains("acts-v3h") && picked.Contains("acts-v3t")
        && picked.Count(x => x == "acts-v3h") > 60 && picked.Count(x => x == "acts-v3t") > 60 && r3.DefaultSchedule(77) == r3.DefaultSchedule(77), picked.Count(x => x == "acts-v3h") + " v3h");
});

// ============================================================ F. save version 6 and the 5 -> 6 migration
Guard("F", () =>
{
    Check("F1 the save version is 6", DungeonRunSave.CurrentVersion == 6);
    foreach (string name in new[] { "golden-save-legacy-v5.xml", "golden-save-acts-v5.xml" })
    {
        string v5 = VerifyPaths.ReadText(Path.Combine(golden, name));
        DungeonRunSave save;
        using (var stream = File.OpenRead(Path.Combine(golden, name))) save = (DungeonRunSave)xml.Deserialize(stream);
        save.LoadedVersion = save.Version;
        var logs = new List<string>();
        bool upgraded = DungeonRunMigration.Upgrade(save, run, logs.Add);
        string v6 = XmlBytes(save);
        Check("F2 " + name + ": upgraded to 6 with a body byte-identical to the version-5 file", upgraded && save.Version == 6 && v6 == v5.Replace("Version=\"5\"", "Version=\"6\""),
            "lengths " + v5.Length + " / " + v6.Length);
        Check("F3 " + name + ": upgrading again changes nothing", !DungeonRunMigration.Upgrade(save, run, logs.Add) && XmlBytes(save) == v6);
        var s = save.Active;
        Check("F4 " + name + ": the run keeps its schedule version, has no hero switch, no hero damage and no rule kind; the profile no boss damage",
            (s.ScheduleVersion == 0 || s.ScheduleVersion == 2) && s.HeroSwitch == null && s.HeroDamage == 0 && s.ActiveRules.All(r => r.Kind == null) && save.Profile.BossDamage.Count == 0);
        // The upgraded run still plays under the version-3 data: it resumes, and its boss sequence holds no encounter.
        run.Resume(s);
        Check("F5 " + name + ": it resumes under the version-3 schedules and can never meet an encounter or an anomaly node",
            s.BossSequence.All(id => !data.Boss(id).IsEncounter) && (!s.IsActRun || run.ScheduleFor(s).Nodes.All(n => n.Kind != DungeonRunNodeKind.Anomaly)));
    }
    // Round trip of every new field.
    var st = At(run, "MAGE", 808, "gila_61_hagatha", 10);
    st.ActiveRules.Add(new DungeonRunRule { Id = "DALA_866", Act = 2, FromNode = -1, Kind = "twist" });
    st.HeroDamage = 7;
    var profile = All(); profile.BossDamage.Add(new DungeonRunBossDamage { Boss = "ulda_40_icarax", Remaining = 180, Attempts = 2 });
    run.BeginMatch(st);
    st.Matches.Add(new DungeonRunMatchSummary { Floor = 10, Boss = "gila_61_hagatha", Outcome = DungeonRunOutcome.Fault, Attempt = 1, BossRemaining = 40, BossForm = 1, HeroesUsed = "HERO_08,GILA_600h2", Rules = "DALA_866", NoLoss = true });
    var save6 = new DungeonRunSave { Profile = profile, Active = st };
    string text = Xml(save6);
    var back = DungeonRunStore.Clone(save6);
    Check("F6 a version-6 save round-trips every new field", Xml(back) == text && back.Active.HeroSwitch != null && back.Active.HeroSwitch.Kind == "hunters" && back.Active.HeroSwitch.Options.Count == 2
        && back.Active.HeroDamage == 7 && back.Active.ActiveRules.Last().Kind == "twist" && back.Profile.BossDamage[0].Remaining == 180 && back.Active.Matches.Last().HeroesUsed == "HERO_08,GILA_600h2"
        && back.Active.Matches.Last().NoLoss && back.Active.Matches.Last().BossRemaining == 40);
    Check("F7 the new fields are written only when set", text.Contains("<HeroSwitch ") && text.Contains("HeroDamage=\"7\"") && text.Contains("<BossDamage>") && text.Contains("Kind=\"twist\"")
        && !Xml(new DungeonRunSave { Profile = All(), Active = V3(run, "MAGE", 1) }).Contains("HeroSwitch") && !Xml(new DungeonRunSave { Profile = All(), Active = V3(run, "MAGE", 1) }).Contains("BossDamage"));
});

// ============================================================ G. the anomaly node, twists and the plan's global rules
Guard("G", () =>
{
    var s = V3(run, "MAGE", 2024);
    Check("G1 a run on acts-v3h opens at its act I anomaly node: two rolled rules and \"none\"", s.Phase == DungeonRunPhase.Modifier && s.ModifierOffer != null && s.ModifierOffer.Kind == "anomaly"
        && s.ModifierOffer.Offered.Count == 3 && s.ModifierOffer.Offered[2] == "none" && s.ModifierOffer.Offered.Take(2).All(id => data.Schedules.Rule(id) != null)
        && s.ModifierOffer.Offered.Take(2).Distinct().Count() == 2, string.Join(",", s.ModifierOffer?.Offered ?? new List<string>()));
    var again = V3(run, "MAGE", 2024);
    Check("G2 the same seed rolls the same offer; a clone of the save keeps it (no redraw)", again.ModifierOffer.Offered.SequenceEqual(s.ModifierOffer.Offered)
        && DungeonRunStore.Clone(new DungeonRunSave { Active = s }).Active.ModifierOffer.Offered.SequenceEqual(s.ModifierOffer.Offered));
    string taken = s.ModifierOffer.Offered[0];
    run.PickModifier(s, taken);
    Check("G3 taking a rule makes it an active anomaly of act I and moves on to floor 1", s.Phase == DungeonRunPhase.PreMatch && s.Floor == 1 && s.ModifierOffer == null
        && s.ActiveRules.Count == 1 && s.ActiveRules[0].Id == taken && s.ActiveRules[0].Kind == "anomaly" && s.ActiveRules[0].Act == 1 && run.RuleActive(s, taken));
    // Floor 1 draws from mix-1 (band 1 and tier-1 encounters): the check is about a custom boss, so one is put there.
    s.BossSequence[s.Floor - 1] = data.Bosses.First(b => !b.IsEncounter && b.Band == 1 && b.Unlock == null).Id;
    var plan = run.BuildPlan(s, all);
    Check("G4 a custom boss's plan carries the rule and nothing else of an encounter", plan.GlobalRules.SequenceEqual(new[] { taken }) && plan.EncounterId == null && !plan.EncounterDeck && plan.BossForms == null);
    Check("G5 the modifiers of the act do not count the rule; the rules line names it", run.ActiveModifiers(s).Count == 0 && run.ActiveGlobalRules(s).Single().Id == taken
        && NativeDungeonRunText.RuleName(taken).StartsWith("Anomaly - ") && NativeDungeonRunText.RuleText(taken).Length > 0);
    var skip = V3(run, "MAGE", 2025);
    run.PickModifier(skip, "none");
    Check("G6 \"none\" takes no rule", skip.ActiveRules.Count == 0 && skip.Phase == DungeonRunPhase.PreMatch && run.BuildPlan(skip, all).GlobalRules.Count == 0);
    bool refused = false;
    try { run.PickModifier(V3(run, "MAGE", 2026), "DALA_857"); } catch (InvalidOperationException) { refused = true; }
    Check("G7 a rule that is not on offer is refused", refused);
    Reach(run, s, 6);
    Check("G8 the rule ends with its act: act II's fights carry only what act II took", !run.RuleActive(s, taken) && run.BuildPlan(s, all).GlobalRules.Count == 0 && s.ActiveRules.Any(r => r.Id == taken));
    var walked = Reach(run, V3(run, "MAGE", 2030), 6, offer => offer.Offered[0]);
    Check("G9 a rule taken at each node: act II's node comes before its modifier node and its offer leaves nothing active out",
        walked.ActiveRules.Count(r => r.Kind == "anomaly") == 2 && walked.ActiveRules.Count(r => r.Kind == null) == 1 && run.ActiveGlobalRules(walked).Count == 1);
    // Incompatibility: flags, a board shrine, rule against rule, run scope.
    var d = Load();
    d.Schedules.Rule("DALA_850").IncompatibleFlags.Add("shrine");
    d.Schedules.Rule("DALA_864").NoBoardShrine = true;
    d.Schedules.Rule("DALA_866").IncompatibleRules.Add("DALA_864");
    var r = new DungeonRun(d);
    var withVoone = V3(r, "MAGE", 4001);
    int vooneAct = (withVoone.BossSequence.IndexOf("trl_200_voone") / 5) + 1;
    var sch = r.ScheduleFor(withVoone);
    Check("G10 a rule a boss flag excludes is left out of that boss's act only", !r.RuleCandidates(withVoone, sch, vooneAct, "anomaly").Any(x => x.Id == "DALA_850")
        && r.RuleCandidates(withVoone, sch, 3, "anomaly").Any(x => x.Id == "DALA_850"), "voone act " + vooneAct);
    var shrineDeck = d.Decks.First(x => x.Shrine != null && d.FindClass(x.Class) != null);
    var shrineRun = r.NewRun(All(d), shrineDeck.Class, shrineDeck.Id, d.Class(shrineDeck.Class).Powers[0], 4002, schedule: "acts-v3h");
    Check("G11 a board-shrine run on acts-v3h keeps its shrine and is never offered a rule that excludes shrine runs", shrineRun.BoardShrine == shrineDeck.Shrine
        && !r.RuleCandidates(shrineRun, r.ScheduleFor(shrineRun), 1, "anomaly").Any(x => x.Id == "DALA_864"));
    var mixed = V3(r, "MAGE", 4003);
    mixed.ActiveRules.Add(new DungeonRunRule { Id = "DALA_864", Act = 1, FromNode = -1, Kind = "anomaly" });
    Check("G12 rule against rule is checked both ways, and an active rule is not offered again", !r.RuleCandidates(mixed, r.ScheduleFor(mixed), 1, "anomaly").Any(x => x.Id == "DALA_866" || x.Id == "DALA_864"));
    d.Schedules.Rules.Add(new DungeonRunGlobalRule { Id = "ULDA_720", Kind = "anomaly", Scope = "run", Effect = "run" });
    var scoped = V3(r, "MAGE", 4004);
    Check("G13 a run-scope rule is offered at act I only", r.RuleCandidates(scoped, r.ScheduleFor(scoped), 1, "anomaly").Any(x => x.Id == "ULDA_720") && !r.RuleCandidates(scoped, r.ScheduleFor(scoped), 2, "anomaly").Any(x => x.Id == "ULDA_720"));
    // Twists: a fixture pool with chance 100 rolls one per act at NewRun.
    var dt = Load();
    foreach (var rule in dt.Schedules.Rules) rule.Kind = "twist";
    foreach (var act in dt.Schedules.Find("acts-v3h").Acts) { act.TwistPool = new List<string> { "DALA_850", "DALA_864", "DALA_866" }; act.TwistChance = 100; }
    var rt = new DungeonRun(dt);
    var tw = V3(rt, "MAGE", 6001);
    Check("G14 a twist is rolled for each act at NewRun (kind twist, from no node), and with no anomaly rule left the anomaly nodes are passed",
        tw.ActiveRules.Count == 3 && tw.ActiveRules.All(x => x.Kind == "twist" && x.FromNode == -1) && tw.ActiveRules.Select(x => x.Act).SequenceEqual(new[] { 1, 2, 3 })
        && tw.Phase == DungeonRunPhase.PreMatch && tw.Floor == 1 && rt.BuildPlan(tw, All(dt)).GlobalRules.Single() == tw.ActiveRules[0].Id);
    Check("G15 the same seed rolls the same twists", V3(rt, "MAGE", 6001).ActiveRules.Select(x => x.Id).SequenceEqual(tw.ActiveRules.Select(x => x.Id)));
    foreach (var act in dt.Schedules.Find("acts-v3h").Acts) act.TwistChance = 1;
    int rolled = Enumerable.Range(1, 300).Sum(i => V3(rt, "MAGE", 7000 + i).ActiveRules.Count);
    Check("G16 the chance is rolled per act (1 percent: about 9 twists in 300 runs of 3 acts)", rolled > 0 && rolled < 40, rolled + " twists");
});

// ============================================================ H. temporary hero switching (run-owned, one fight)
string Identity(DungeonRunState s) => string.Join("|", s.Class, s.StarterDeck, s.Power, s.PowerOverride ?? "-", string.Join(",", s.Deck),
    string.Join(",", s.Cards.Select(c => c.Uid + ":" + c.Id)), string.Join(",", s.Treasures), string.Join(",", s.ActiveTreasures), s.BoardShrine ?? "-");
Guard("H", () =>
{
    var s = At(run, "MAGE", 9001, "gila_61_hagatha", 10);
    string before = Identity(s);
    var sw = run.HeroSwitchFor(s);
    Check("H1 Hagatha gives a Mage run both hunters' hero cards, each with its own power", sw != null && sw.Kind == "hunters" && sw.Floor == 10
        && sw.Options.Select(o => o.Hero + ":" + o.Power).SequenceEqual(new[] { "GILA_600h2:GILA_600p", "GILA_500h4:GILA_500p2" }));
    Check("H2 the fight's classes are the run's, then the hunters' (the boss's Secret prior covers a switched hero)", run.MatchClasses(s).SequenceEqual(new[] { "MAGE", "WARRIOR", "ROGUE" })
        && run.RunClasses(s).SequenceEqual(new[] { "MAGE" }));
    var id = run.MatchIdentity(s);
    Check("H3 MatchIdentity: the run's own hero, power and deck, and the switch", id.HeroCard == data.Class("MAGE").HeroCard && id.Power == s.Power && id.Deck.SequenceEqual(s.Deck) && id.Switch.Options.Count == 2);
    var plan = run.BuildPlan(s, all);
    Check("H4 the plan: the run's own hero and deck, two hero cards with 12 cards and 3 passives, 2 start Mana Crystals, 100 Health, a 40-card deck",
        plan.PlayerHero.Id == data.Class("MAGE").HeroCard && plan.PlayerDeck.Select(c => c.Id).SequenceEqual(s.Deck) && plan.PlayerHeroCards.Count == 2
        && plan.PlayerHeroCards.All(o => o.Deck.Count == 12 && o.Passives.Count == 3) && plan.BossStartManaBonus == 2 && plan.BossHealth == 100 && plan.BossDeck.Count == 40 && plan.EncounterDeck);
    Check("H5 no switch state exists before the fight begins", s.HeroSwitch == null);
    run.BeginMatch(s);
    Check("H6 BeginMatch records the switch of this floor", s.HeroSwitch != null && s.HeroSwitch.Floor == 10 && s.HeroSwitch.Options.Count == 2);
    run.RecordOutcome(s, All(), DungeonRunOutcome.Fault, null, new RunMatchEnd(60, 0, 0, 12, new[] { "HERO_08", "GILA_600h2" }, false));
    Check("H7 a fault restages the floor and keeps the switch; the run is unchanged", s.Phase == DungeonRunPhase.PreMatch && s.HeroSwitch != null && Identity(s) == before);
    run.BeginMatch(s);
    var profile = All();
    run.RecordOutcome(s, profile, DungeonRunOutcome.Loss, null, new RunMatchEnd(60, 0, 0, 12, new[] { "HERO_08", "GILA_600h2", "GILA_500h4" }, false));
    Check("H8 a loss ends the switch; the run's class, power, deck, cards and treasures are what they were; the summary names the heroes used",
        s.HeroSwitch == null && Identity(s) == before && s.Phase == DungeonRunPhase.Defeat && s.Matches.Last().HeroesUsed == "HERO_08,GILA_600h2,GILA_500h4" && s.Matches.Last().BossRemaining == 60);
    Check("H9 the archive names the run's own class and power", profile.Archive.Last().Class == "MAGE" && profile.Archive.Last().Power == s.Power);
    var won = At(run, "MAGE", 9002, "gila_61_hagatha", 10);
    var wonDeck = won.Deck.ToList();
    run.BeginMatch(won);
    run.RecordOutcome(won, All(), DungeonRunOutcome.Win, null, new RunMatchEnd(0, 0, 0, 3, new[] { "HERO_08", "GILA_500h4" }, false));
    Check("H10 a win as a switched hero: the switch is gone, the run goes on as itself (no power override, the same deck)", won.HeroSwitch == null && won.Class == "MAGE"
        && won.PowerOverride == null && won.Deck.SequenceEqual(wonDeck) && won.StarterDeck == data.Class("MAGE").Decks[0] && won.MajorsCleared == 2 && run.MatchIdentity(won).Switch == null);
    // The run's own hunter is not given its own card.
    var tessRow = data.Classes.FirstOrDefault(c => c.HeroCard == "GILA_500h3");
    var dariusRow = data.Classes.FirstOrDefault(c => c.HeroCard == "GILA_600h");
    Check("H11 the data has Tess's and Darius's hero rows", tessRow != null && dariusRow != null);
    if (tessRow != null && dariusRow != null)
    {
        var tess = Reach(run, run.NewRun(all, tessRow.Id, tessRow.Decks[0], tessRow.Powers[0], 9003, schedule: "acts-v3h", signatureId: run.SignatureOffer(all, tessRow, 9003).FirstOrDefault()), 10);
        tess.BossSequence[9] = "gila_61_hagatha";
        var darius = Reach(run, run.NewRun(all, dariusRow.Id, dariusRow.Decks[0], dariusRow.Powers[0], 9004, schedule: "acts-v3h", signatureId: run.SignatureOffer(all, dariusRow, 9004).FirstOrDefault()), 10);
        darius.BossSequence[9] = "gila_61_hagatha";
        Check("H12 a Tess run is given Darius's card only, a Darius run Tess's only", run.BuildPlan(tess, all).PlayerHeroCards.Single().Hero.Id == "GILA_600h2"
            && run.BuildPlan(darius, all).PlayerHeroCards.Single().Hero.Id == "GILA_500h4" && run.HeroSwitchFor(tess).Options.Single().Hero == "GILA_600h2");
    }
    var plain = At(run, "MAGE", 9005, "dala_21_whirt", 8);
    run.BeginMatch(plain);
    Check("H13 a fight without hero cards has no switch, and its classes are the run's", plain.HeroSwitch == null && run.MatchClasses(plain).SequenceEqual(new[] { "MAGE" }));
    // A legacy run and an acts-v2 run are untouched by all of it.
    var legacy = run.NewRun(all, "MAGE", data.Class("MAGE").Decks[0], data.Class("MAGE").Powers[0], 11, schedule: "legacy");
    run.BeginMatch(legacy);
    run.RecordOutcome(legacy, All(), DungeonRunOutcome.Win, null, null);
    Check("H14 a legacy run: no switch, no rules, no summary extras", legacy.HeroSwitch == null && legacy.Matches[0].HeroesUsed == null && legacy.Matches[0].Rules == null && legacy.Matches[0].BossRemaining == 0);
    // A relay is the same state with kind "relay" (a fixture finale row).
    var d = Load();
    var fin = new DungeonRunBoss { Id = "fx_finale", Name = "Fixture Finale", Provenance = "ULDA", Tier = "finale", Pools = { "finale" }, HeroCard = "ULDA_BOSS_40h", Power = "ULDA_BOSS_40p1",
        Deck = "enc-dala_21_whirt", Archetype = "midrange", Flags = { "relay" }, Source = d.Boss("dala_21_whirt").Source };
    fin.Relay.Add(new DungeonRunRelayHero { Hero = "HERO_01", Power = "CS2_102", Deck = "enc-dala_21_whirt", Health = 50 });
    fin.Relay.Add(new DungeonRunRelayHero { Hero = "HERO_08", Power = "CS2_034", Deck = "enc-dala_21_whirt", Health = 50, SkipFor = { "HERO_08" } });
    d.Bosses.Add(fin);
    var r = new DungeonRun(d);
    var rs = At(r, "MAGE", 9006, "fx_finale", 15);
    var rplan = r.BuildPlan(rs, All(d));
    Check("H15 a relay row: the run's own hero is skipped, the others enter in order with their Health and decks", r.HeroSwitchFor(rs).Kind == "relay" && rplan.PlayerRelay.Count == 1
        && rplan.PlayerRelay[0].Hero.Id == "HERO_01" && rplan.PlayerRelay[0].Health == 50 && rplan.PlayerRelay[0].Deck.Count == 30 && r.MatchClasses(rs).SequenceEqual(new[] { "MAGE", "WARRIOR" }));
});

// ============================================================ I. persistent boss damage (profile scope) and Battle Scars
Guard("I", () =>
{
    var profile = All();
    var s = At(run, "MAGE", 3001, "ulda_40_icarax", 15);
    var plan = run.BuildPlan(s, profile);
    Check("I1 a fresh Icarax: phase 1 at 300 with its three forms in the plan, no carried damage, the modifiers ignored", plan.BossHealth == 300 && plan.BossForms.Count == 3 && plan.BossFormStart == 0
        && plan.BossCarriedDamage == 0 && plan.BossHero.Id == "ULDA_BOSS_40h" && plan.BossHeroPowerId == "ULDA_BOSS_40p1" && plan.BossForms[1].HealthAtMost == 200 && plan.BossForms[2].Deck.Count >= 10
        && plan.BossDeck.Select(c => c.Id).SequenceEqual(data.BossDecks["enc-ulda_40_icarax"]) && run.BossHealth(s) == 300);
    run.BeginMatch(s);
    run.RecordOutcome(s, profile, DungeonRunOutcome.Loss, null, new RunMatchEnd(244, 0, 0, 30, new[] { "HERO_08" }, false));
    Check("I2 a loss banks the lord's remaining Health in the profile and ends the run", profile.DamageOf("ulda_40_icarax")?.Remaining == 244 && profile.DamageOf("ulda_40_icarax").Attempts == 1
        && s.Phase == DungeonRunPhase.Defeat && s.Matches.Last().BossRemaining == 244);
    var s2 = At(run, "PRIEST", 3002, "ulda_40_icarax", 15);
    var plan2 = run.BuildPlan(s2, profile);
    Check("I3 the next run meets it at 244 in phase 1", plan2.BossHealth == 300 && plan2.BossCarriedDamage == 56 && plan2.BossFormStart == 0 && run.PersistentRemaining(s2, profile) == 244);
    run.BeginMatch(s2);
    run.RecordOutcome(s2, profile, DungeonRunOutcome.TurnLimit, null, new RunMatchEnd(180, 1, 1, 30, new[] { "HERO_09" }, false));
    Check("I4 a turn-limit loss in phase 2 banks 180 (attempt 2)", profile.DamageOf("ulda_40_icarax").Remaining == 180 && profile.DamageOf("ulda_40_icarax").Attempts == 2 && s2.Matches.Last().BossForm == 1);
    var s3 = At(run, "ROGUE", 3003, "ulda_40_icarax", 15);
    var plan3 = run.BuildPlan(s3, profile);
    Check("I5 the next run starts it in phase 2: that form's hero, power, Health and deck, with 20 damage", plan3.BossFormStart == 1 && plan3.BossHero.Id == "ULDA_BOSS_40h2"
        && plan3.BossHeroPowerId == "ULDA_BOSS_40p2" && plan3.BossHealth == 200 && plan3.BossCarriedDamage == 20 && plan3.BossDeck.Select(c => c.Id).SequenceEqual(data.BossDecks["enc-ulda_40_icarax-f1"]));
    run.BeginMatch(s3);
    run.RecordOutcome(s3, profile, DungeonRunOutcome.Loss, null, new RunMatchEnd(190, 1, 0, 30, new[] { "HERO_03" }, false));
    Check("I6 a record never rises (a harvest above it keeps the lower Health)", profile.DamageOf("ulda_40_icarax").Remaining == 180 && profile.DamageOf("ulda_40_icarax").Attempts == 3);
    profile.DamageOf("ulda_40_icarax").Remaining = 95;
    var s4 = At(run, "WARRIOR", 3004, "ulda_40_icarax", 15);
    var plan4 = run.BuildPlan(s4, profile);
    Check("I7 at 95 it starts in phase 3 (100 Health form, 5 damage, the phase-3 power and deck)", plan4.BossFormStart == 2 && plan4.BossHero.Id == "ULDA_BOSS_40h3" && plan4.BossHeroPowerId == "ULDA_BOSS_40p3"
        && plan4.BossHealth == 100 && plan4.BossCarriedDamage == 5 && plan4.BossDeck.Select(c => c.Id).SequenceEqual(data.BossDecks["enc-ulda_40_icarax-f2"]));
    var typed = At(run, "MAGE", 3005, "ulda_40_icarax", 15, typed: true);
    var tplan = run.BuildPlan(typed, profile);
    run.BeginMatch(typed);
    run.RecordOutcome(typed, profile, DungeonRunOutcome.Loss, null, new RunMatchEnd(10, 2, 2, 30, new[] { "HERO_08" }, false));
    Check("I8 a typed-seed run neither reads nor writes the ledger", tplan.BossFormStart == 0 && tplan.BossCarriedDamage == 0 && profile.DamageOf("ulda_40_icarax").Remaining == 95 && profile.DamageOf("ulda_40_icarax").Attempts == 3);
    var noProfile = run.BuildPlan(At(run, "MAGE", 3006, "ulda_40_icarax", 15));
    Check("I9 a plan built without a profile reads no ledger", noProfile.BossFormStart == 0 && noProfile.BossCarriedDamage == 0);
    run.BeginMatch(s4);
    run.RecordOutcome(s4, profile, DungeonRunOutcome.Win, null, new RunMatchEnd(0, 2, 0, 12, new[] { "HERO_01" }, false));
    Check("I10 a win clears the record and wins the run (the act III major)", profile.DamageOf("ulda_40_icarax") == null && s4.Phase == DungeonRunPhase.Victory && s4.Matches.Last().BossRemaining == 0);
    var gnome = At(run, "MAGE", 3007, "ulda_40_icarax", 15);
    gnome.ActiveTreasures.Add("ULDA_115");
    var gplan = run.BuildPlan(gnome, All());
    var gplan2 = run.BuildPlan(gnome, new DungeonRunProfile { BossDamage = { new DungeonRunBossDamage { Boss = "ulda_40_icarax", Remaining = 205, Attempts = 1 } } });
    Check("I11 ULDA_115 adds 10 damage to the Plague Lord's start (and can tip it into its next phase)", gplan.BossCarriedDamage == 10 && gplan.BossFormStart == 0
        && gplan2.BossFormStart == 1 && gplan2.BossHealth == 200 && gplan2.BossCarriedDamage == 5);
    var whirt = At(run, "MAGE", 3008, "dala_21_whirt", 8);
    whirt.ActiveTreasures.Add("ULDA_115");
    var wp = All();
    run.BeginMatch(whirt);
    var wplan = run.BuildPlan(At(run, "MAGE", 3008, "dala_21_whirt", 8), wp);
    run.RecordOutcome(whirt, wp, DungeonRunOutcome.Loss, null, new RunMatchEnd(20, 0, 0, 30, new[] { "HERO_08" }, false));
    Check("I12 a boss without persistent damage carries none and banks none", wplan.BossCarriedDamage == 0 && wp.BossDamage.Count == 0);
    // No-loss endings.
    var vault = At(run, "MAGE", 3009, "dala_21_whirt", 8);
    int defeated = DungeonRun.BossesDefeated(vault);
    run.BeginMatch(vault);
    run.RecordOutcome(vault, All(), DungeonRunOutcome.Loss, null, new RunMatchEnd(30, 0, 0, 0, new[] { "HERO_08" }, true));
    Check("I13 a fight a script ended with no loss counts as won with no boss kill credit", vault.Matches.Last().Outcome == DungeonRunOutcome.Win && vault.Matches.Last().NoLoss
        && !vault.Finished && DungeonRun.BossesDefeated(vault) == defeated);
    // Battle Scars (a fixture rule row; the rule itself is family 06's).
    var d = Load();
    d.Schedules.Rules.Add(new DungeonRunGlobalRule { Id = "ULDA_720", Kind = "anomaly", Scope = "run", Effect = "run" });
    var r = new DungeonRun(d);
    var scars = V3(r, "MAGE", 3010);
    scars.ModifierOffer.Offered[0] = "ULDA_720";
    r.PickModifier(scars, "ULDA_720");
    var splan = r.BuildPlan(scars, All(d));
    Check("I14 Battle Scars: a run-scope rule (act 0), 100 Health, no match rule in the plan", scars.ActiveRules[0].Act == 0 && splan.PlayerHeroHealth == 100 && splan.PlayerCarriedDamage == 0 && splan.GlobalRules.Count == 0);
    r.BeginMatch(scars);
    r.RecordOutcome(scars, All(d), DungeonRunOutcome.Win, null, new RunMatchEnd(0, 0, 0, 37, new[] { "HERO_08" }, false));
    Check("I15 a won fight keeps the run hero's damage", scars.HeroDamage == 37);
    Reach(r, scars, 2);
    Check("I16 the next fight starts with it, and it lasts the whole run", r.BuildPlan(scars, All(d)).PlayerCarriedDamage == 37 && r.RuleActive(Reach(r, scars, 6), "ULDA_720"));
    r.BeginMatch(scars);
    r.RecordOutcome(scars, All(d), DungeonRunOutcome.Win, null, new RunMatchEnd(0, 0, 0, 5, new[] { "HERO_08", "GILA_600h2" }, false));
    Check("I17 a fight finished as another hero does not rewrite the run hero's damage", scars.HeroDamage == 37);
});

// ============================================================ J. the plans of the other encounters, the boss pilot on odd deck sizes
Guard("J", () =>
{
    var s = At(run, "MAGE", 5001, "dala_21_whirt", 8);
    var plan = run.BuildPlan(s, all);
    Check("J1 Whirt's plan: the boss's generated Secrets on, the act curve's Health, no forms, no shrine", plan.BossGeneratedSecrets && plan.BossHealth == 46 && plan.BossForms == null && plan.BossShrine == null
        && plan.EncounterId == "dala_21_whirt" && plan.BossHeroPowerId == "DALA_BOSS_21p");
    var cfg = new GameConfig { RandomSeed = 5, Shuffle = true, SkipMulligan = false, History = false, Logging = false, FormatType = FormatType.FT_WILD };
    DungeonRunSetup.Configure(cfg, plan);
    Check("J2 Configure turns the boss side's generated Secrets on for it and leaves the encounter config unset", !cfg.Player2ExcludeGeneratedSecrets && !cfg.Player1ExcludeGeneratedSecrets && cfg.Encounter == null);
    var custom = Reach(run, V3(run, "MAGE", 5002), 1);
    custom.BossSequence[0] = data.Bosses.First(b => !b.IsEncounter && b.Band == 1 && b.Unlock == null).Id; // a custom boss, whatever mix-1 drew
    var cplan = run.BuildPlan(custom, all);
    var ccfg = new GameConfig { RandomSeed = 5, Shuffle = true, SkipMulligan = false, History = false, Logging = false, FormatType = FormatType.FT_WILD };
    DungeonRunSetup.Configure(ccfg, cplan);
    Check("J3 a custom boss on acts-v3h: generated Secrets off for the boss, no encounter fields", ccfg.Player2ExcludeGeneratedSecrets && ccfg.Encounter == null && !cplan.EncounterDeck && cplan.BossDeck.Count == 30);
    var seen = new HashSet<string>();
    for (long seed = 1; seed <= 60; seed++)
    {
        var v = At(run, "MAGE", 5100 + seed, "trl_200_voone", 3);
        var variant = run.CurrentVariant(v);
        var vplan = run.BuildPlan(v, all);
        seen.Add(variant.Id);
        if (vplan.BossShrine != variant.Shrine || !vplan.BossDeck.Select(c => c.Id).SequenceEqual(data.BossDecks[variant.Deck]) || run.CurrentVariant(v).Id != variant.Id) seen.Add("MISMATCH");
    }
    Check("J4 Voone's variant comes from the seed and the floor: all three occur, each with its shrine and its deck, and a restage keeps it", seen.SetEquals(new[] { "horn", "drum", "champion" }), string.Join(",", seen));
    Check("J5 an encounter's Health offset is its own (no band lookup); Hagatha's 2 Mana Crystals replace the curve and are capped at her cap",
        run.HealthOffset(data.Boss("dala_21_whirt")) == 0 && run.HealthOffset(data.Boss("pit_champion")) == 10 && run.BossStartManaBonus(At(run, "MAGE", 5003, "gila_61_hagatha", 10)) == 2);
    // The boss pilot derives a plan for a 10-card and a 40-card boss deck (no 30-card assumption in AutoDeckPlan).
    var forty = run.BuildPlan(At(run, "MAGE", 5004, "gila_61_hagatha", 10), all);
    var pilot40 = DungeonRunBossPilot.Build(data.Boss("gila_61_hagatha"), forty, data, runClasses: new[] { "MAGE", "WARRIOR", "ROGUE" });
    var ten = run.BuildPlan(At(run, "MAGE", 5005, "dala_21_whirt", 8), all);
    ten.BossDeck = ten.BossDeck.Take(10).ToList();
    var pilot10 = DungeonRunBossPilot.Build(data.Boss("dala_21_whirt"), ten, data, runClasses: new[] { "MAGE" });
    Check("J6 DungeonRunBossPilot.Build derives a deck plan for a 40-card and for a 10-card boss deck", pilot40.Deck.CardIds.Count() == 40 && pilot40.Plan != null && pilot10.Deck.CardIds.Count() == 10 && pilot10.Plan != null);
    Check("J7 the Secret prior of a switched fight covers the hunters' classes", pilot40.SecretPrior != null && pilot40.SecretPrior.Keys.Any(id => Cards.FromId(id).Class == CardClass.ROGUE)
        && pilot40.SecretPrior.Keys.Any(id => Cards.FromId(id).Class == CardClass.MAGE));
    // The admission table: the new rows.
    var p21 = DungeonRunAdmission.Classify(null, "DALA_BOSS_21p", AdmitScope.Boss, null, catalogRun: true);
    var p21legacy = DungeonRunAdmission.Classify(null, "DALA_BOSS_21p", AdmitScope.Boss, null);
    Check("J8 DALA_BOSS_21p: admitted for a boss on a catalog run with the generated-Secrets need, rejected on a legacy run", p21.Admitted && p21.Rule.BossNeeds == DungeonRunAdmission.BossNeedsGeneratedSecrets && !p21legacy.Admitted);
    Check("J9 the three Warrior shrines are admitted for the run player and for a boss; the other shrines for the run player only",
        new[] { "TRLA_104", "TRLA_170", "TRLA_171" }.All(id => DungeonRunAdmission.Classify(null, id, AdmitScope.Boss, null, catalogRun: true).Admitted && DungeonRunAdmission.Classify(null, id, AdmitScope.Player, null, catalogRun: true).Admitted)
        && !DungeonRunAdmission.Classify(null, "TRLA_115", AdmitScope.Boss, null, catalogRun: true).Admitted && DungeonRunAdmission.Classify(null, "TRLA_115", AdmitScope.Player, null, catalogRun: true).Admitted);
    Check("J10 the three anomalies are admitted as rules only and never generated", new[] { "DALA_850", "DALA_864", "DALA_866" }.All(id => DungeonRunAdmission.Classify(null, id, AdmitScope.Rule).Admitted
        && !DungeonRunAdmission.Classify(null, id, AdmitScope.Player).Admitted && !DungeonRunAdmission.Classify(null, id, AdmitScope.Boss).Admitted && DungeonRunAdmission.GeneratedRejects(true).Contains(id)));
    Check("J11 the hunters' hero cards are the run player's only; Toki's is rejected with the phase-9 lift", DungeonRunAdmission.Classify(null, "GILA_500h4", AdmitScope.Player).Admitted
        && !DungeonRunAdmission.Classify(null, "GILA_500h4", AdmitScope.Boss).Admitted && !DungeonRunAdmission.Classify(null, "GILA_900h2", AdmitScope.Player).Admitted
        && DungeonRunAdmission.RuleFor("GILA_900h2").Lift == DungeonRunAdmission.TurnRestartLift);
    var pool = DungeonRunValidator.BossSecretPool(CardClass.MAGE);
    Check("J12 a Mage boss's generated-Secret pool is closed: scripted, boss-admitted Mage Secrets only", pool.Count >= 5 && pool.All(c => c.IsSecret && c.Implemented && c.Class == CardClass.MAGE
        && DungeonRunAdmission.Classify(c, c.Id, AdmitScope.Boss, null, catalogRun: true).Admitted), pool.Count + " secrets");
    Check("J13 the engine registry: 25 family slots (every card is of a family 01..25), family 01's cards, the passive list unchanged for the run content", DungeonRunEncounters.All.Any(e => e.Family == "01")
        && DungeonRunEncounters.All.All(e => e.Family.Length == 2 && string.CompareOrdinal(e.Family, "01") >= 0 && string.CompareOrdinal(e.Family, "25") <= 0)
        && DungeonRunEncounters.Find("ULDA_BOSS_40p2").NeedsActivation && DungeonRunEncounters.NeedsActivation("GIL_504h") && !DungeonRunEncounters.NeedsActivation("ULDA_BOSS_40p1")
        && DungeonRunEncounters.All.Where(e => e.Kind == EncounterCardKind.Rule && e.Family == "01").Select(e => e.Id).OrderBy(id => id, StringComparer.Ordinal).SequenceEqual(new[] { "DALA_850", "DALA_864", "DALA_866" })
        && DungeonRunGlobalRules.Scripted.SequenceEqual(DungeonRunGlobalRules.Scripted.OrderBy(id => id, StringComparer.Ordinal)) && DungeonRunGlobalRules.EffectOf("DALA_866") == "DALA_866e"
        && DungeonRunEncounters.PassivePowers.SequenceEqual(new[] { "LOOTA_BOSS_48p", "LOOTA_BOSS_16p", "LOOTA_BOSS_17p", "LOOTA_BOSS_38p", "LOOTA_BOSS_19p", "LOOTA_BOSS_33p", "LOOTA_BOSS_31p", "LOOTA_BOSS_47p",
            "LOOTA_BOSS_20p", "LOOTA_BOSS_26p8", "LOOTA_BOSS_34p", "LOOTA_BOSS_52p", "LOOTA_BOSS_53p",
            "ULDA_BOSS_20p", "ULDA_BOSS_21p", "ULDA_BOSS_29p", "ULDA_BOSS_30p" })); // Trapped Room's Alarm, the first passive registered here; then family 08's seven, family 09's five and family 20's four
    Check("J14 every scripted encounter card is implemented in the card database", DungeonRunEncounters.All.All(e => Cards.FromId(e.Id).Implemented), string.Join(",", DungeonRunEncounters.All.Where(e => !Cards.FromId(e.Id).Implemented).Select(e => e.Id)));
});

Console.WriteLine($"RESULT {passes} passed, {failures} failed");
return failures;
