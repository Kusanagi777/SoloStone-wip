// c-upgrades: phase 6 F0 evolving-artifact storage. The upgrade switch (DungeonRunTreasure.UpgradeOn: floors, actClear,
// bossWins:N) and slots (deck, power, passive), the power upgrade table (run-v1.json "powerUpgrades"), and the version-4 save
// round trip and migration with and without the new optional fields (Slot, Progress, Log, PowerOverride, NextFight, Loadout).
// Headless: compiles C:\SoloStone\plugin\DungeonRun\*.cs against p6f0/engine-out.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SabberStoneCore.CardSets.DungeonRun;
using SabberStoneCore.Model;

int failures = 0, passes = 0;
void Check(string name, bool ok, string detail = "")
{
    if (ok) passes++; else failures++;
    if (!ok || Environment.GetEnvironmentVariable("VERBOSE") == "1")
        Console.WriteLine((ok ? "PASS " : "FAIL ") + name + (detail.Length > 0 ? "  (" + detail + ")" : ""));
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
DungeonRunData Load() => DungeonRunData.Parse(R("dungeon-run/run-v1.json"), R("dungeon-run/pool-v1.json"), R("npc/decks-v1.json"),
    R("dungeon-run/catalog-v1.json"), R("dungeon-run/schedule-v1.json"), R("dungeon-run/tavern-v1.json"));
var ser = new System.Xml.Serialization.XmlSerializer(typeof(DungeonRunSave));
string Xml(DungeonRunSave save) { using var w = new StringWriter(); ser.Serialize(w, save); return w.ToString(); }
string StateXml(DungeonRunState s) => Xml(new DungeonRunSave { Active = s });
var fresh = new DungeonRunProfile();
string tmpRoot = Path.Combine(Path.GetTempPath(), "c-upgrades-" + System.Diagnostics.Process.GetCurrentProcess().Id);
Directory.CreateDirectory(tmpRoot);

// Wins fights (and takes the first option everywhere) until stop holds; returns the floors won in order.
List<int> Walk(DungeonRun run, DungeonRunState s, Func<DungeonRunState, bool> stop, Action<DungeonRunState, int> afterWin = null)
{
    var won = new List<int>();
    var scratch = new DungeonRunProfile();
    for (int guard = 0; guard < 400 && !s.Finished && !stop(s); guard++)
    {
        switch (s.Phase)
        {
            case DungeonRunPhase.PreMatch:
                int floor = s.Floor;
                run.BeginMatch(s); run.RecordOutcome(s, scratch, DungeonRunOutcome.Win);
                won.Add(floor); afterWin?.Invoke(s, floor); break;
            case DungeonRunPhase.TreasureDraft: run.PickTreasure(s, s.OpenDraft.Offered[0]); break;
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
    return won;
}
// An active treasure as AddActiveTreasure records it: the shell card (origin treasure), the held key, the upgrade by uid.
DungeonRunCard Grant(DungeonRunState s, string key)
{
    s.ActiveTreasures.Add(key);
    var card = DungeonRunDeck.Add(s, DungeonRunContent.Get(key).ShellId, DungeonRunDeck.OriginTreasure);
    s.Upgrades.Add(new DungeonRunUpgrade { Key = key, SinceFloor = s.Floor, Uid = card?.Uid ?? 0 });
    return card;
}
string Shell(string key) => DungeonRunContent.Get(key).ShellId;
int MajorFloor(DungeonRun run, DungeonRunState s, int act) => run.ScheduleFor(s).Nodes.Where(n => n.Kind == DungeonRunNodeKind.Major && n.Act == act).Single().Floor;

// ---------------------------------------------------------------- U1 actClear on the deck slot
{
    var data = Load(); data.Treasure("charm_seedling").UpgradeOn = DungeonRunTreasure.UpgradeOnActClear; data.Treasure("charm_sprouting").UpgradeOn = DungeonRunTreasure.UpgradeOnActClear;
    Check("U1 fixture validates", DungeonRunValidator.Validate(data, null).Count == 0, string.Join(" | ", DungeonRunValidator.Validate(data, null).Take(3)));
    var run = new DungeonRun(data);
    var s = run.NewRun(fresh, "MAGE", data.Class("MAGE").DefaultDeck, "CS2_034", 4711, signatureId: run.SignatureOffer(fresh, run.Data.FindClass("MAGE"), 4711).FirstOrDefault());
    var card = Grant(s, "charm_seedling");
    int major1 = MajorFloor(run, s, 1), major2 = MajorFloor(run, s, 2);
    var seenBefore = new List<string>();
    Walk(run, s, x => x.Floor > major2 + 1 || x.Finished, (st, floor) => seenBefore.Add(floor + ":" + DungeonRunDeck.Find(st, card.Uid).Id));
    var at = seenBefore.ToDictionary(x => int.Parse(x.Split(':')[0]), x => x.Split(':')[1]);
    Check("U1 actClear: unchanged on every combat win of act I (the floors rule would have fired at floor 3)",
        Enumerable.Range(1, major1 - 1).All(f => at[f] == Shell("charm_seedling")), string.Join(" ", seenBefore));
    Check("U1 actClear: the act I major upgrades it to charm_sprouting, by uid", at[major1] == Shell("charm_sprouting") && s.ActiveTreasures.Contains("charm_blooming") == (at[major2] == Shell("charm_blooming")));
    Check("U1 actClear: unchanged through act II's combat floors, upgraded again by its major, then the record ends",
        Enumerable.Range(major1 + 1, major2 - major1 - 1).All(f => at[f] == Shell("charm_sprouting")) && at[major2] == Shell("charm_blooming") && s.Upgrades.Count == 0
        && s.ActiveTreasures.Contains("charm_blooming") && !s.ActiveTreasures.Contains("charm_seedling"));
    Check("U1 the mirror stays consistent", DungeonRunDeck.MirrorError(s) == null);
    // A legacy run has no acts: an actClear upgrade never fires.
    var ls = run.NewRun(fresh, "MAGE", data.Class("MAGE").DefaultDeck, "CS2_034", 4711, schedule: DungeonRunSchedules.LegacyId, signatureId: run.SignatureOffer(fresh, run.Data.FindClass("MAGE"), 4711).FirstOrDefault());
    Grant(ls, "charm_seedling");
    Walk(run, ls, x => x.Finished);
    Check("U1 a legacy run never fires actClear", ls.Deck.Contains(Shell("charm_seedling")) && ls.Upgrades.Count == 1, string.Join(",", ls.ActiveTreasures));
}

// ---------------------------------------------------------------- U2 bossWins:N
{
    var data = Load(); data.Treasure("charm_seedling").UpgradeOn = "bossWins:2"; data.Treasure("charm_sprouting").UpgradeOn = "bossWins:2";
    Check("U2 bossWins:2 validates; bossWins:0 and bossWins:x do not", DungeonRunValidator.Validate(data, null).Count == 0
        && DungeonRunTreasure.BossWinsOf("bossWins:0") == 0 && DungeonRunTreasure.BossWinsOf("bossWins:x") == 0 && !DungeonRunTreasure.KnownTrigger("bossWins:"));
    var run = new DungeonRun(data);
    var s = run.NewRun(fresh, "MAGE", data.Class("MAGE").DefaultDeck, "CS2_034", 99, signatureId: run.SignatureOffer(fresh, run.Data.FindClass("MAGE"), 99).FirstOrDefault());
    var card = Grant(s, "charm_seedling");
    var progress = new List<string>();
    Walk(run, s, x => x.Floor >= 4, (st, floor) => progress.Add(floor + ":" + (st.Upgrades.FirstOrDefault()?.Progress ?? -1) + ":" + DungeonRunDeck.Find(st, card.Uid).Id));
    Check("U2 win 1: Progress 1, not upgraded; win 2: upgraded to charm_sprouting and Progress back to 0; win 3: Progress 1",
        progress.Count >= 3 && progress[0] == "1:1:" + Shell("charm_seedling") && progress[1] == "2:0:" + Shell("charm_sprouting") && progress[2] == "3:1:" + Shell("charm_sprouting"),
        string.Join(" ", progress));
    Check("U2 Progress is saved as an attribute while it is not 0", StateXml(s).Contains("Progress=\"1\""));
    var bad = Load(); bad.Treasure("charm_seedling").UpgradeOn = "every:2";
    Check("U2 an unknown upgradeOn is refused", DungeonRunValidator.Validate(bad, null).Any(e => e.StartsWith("charm_seedling:") && e.Contains("unknown upgradeOn every:2")));
    // A fault and a loss count no win.
    var f = run.NewRun(fresh, "MAGE", data.Class("MAGE").DefaultDeck, "CS2_034", 98, signatureId: run.SignatureOffer(fresh, run.Data.FindClass("MAGE"), 98).FirstOrDefault());
    Grant(f, "charm_seedling");
    run.BeginMatch(f); run.RecordOutcome(f, fresh, DungeonRunOutcome.Fault);
    Check("U2 a fault counts no boss win", f.Upgrades[0].Progress == 0);
}

// ---------------------------------------------------------------- U3 the passive slot
{
    var data = Load();
    data.Treasure("iron_skin").UpgradesTo = "mending_charm"; data.Treasure("iron_skin").UpgradeSlot = DungeonRunTreasure.SlotPassive;
    var errs = DungeonRunValidator.Validate(data, null);
    Check("U3 a passive treasure upgrading on the passive slot validates", errs.Count == 0, string.Join(" | ", errs.Take(3)));
    var wrong = Load(); wrong.Treasure("iron_skin").UpgradesTo = "mending_charm";
    Check("U3 a passive treasure upgrading on the deck slot is refused", DungeonRunValidator.Validate(wrong, null).Any(e => e.StartsWith("iron_skin:") && e.Contains("does not fit a passive")));
    var power = Load(); power.Treasure("iron_skin").UpgradesTo = "mending_charm"; power.Treasure("iron_skin").UpgradeSlot = DungeonRunTreasure.SlotPower;
    Check("U3 a treasure on the power slot is refused (power upgrades are powerUpgrades rows)", DungeonRunValidator.Validate(power, null).Any(e => e.StartsWith("iron_skin:") && e.Contains("upgradeSlot power")));
    var run = new DungeonRun(data);
    var s = run.NewRun(fresh, "MAGE", data.Class("MAGE").DefaultDeck, "CS2_034", 5, schedule: DungeonRunSchedules.LegacyId, signatureId: run.SignatureOffer(fresh, run.Data.FindClass("MAGE"), 5).FirstOrDefault());
    // PickTreasure schedules it: open a treasure draft offering it.
    s.Drafts.Add(new DungeonRunDraft { Floor = s.Floor, Kind = "treasure", Offered = new List<string> { "iron_skin" } });
    s.Phase = DungeonRunPhase.TreasureDraft;
    run.PickTreasure(s, "iron_skin");
    Check("U3 picking it records a passive-slot upgrade", s.Treasures.Contains("iron_skin") && s.Upgrades.Count == 1 && s.Upgrades[0].Slot == DungeonRunTreasure.SlotPassive && s.Upgrades[0].Uid == 0);
    if (s.Phase == DungeonRunPhase.BundleDraft) run.PickBundle(s, s.OpenDraft.Offered[0]);
    Walk(run, s, x => x.Floor >= 4);
    Check("U3 two floors later the passive becomes mending_charm in place, in pick order", s.Treasures.Contains("mending_charm") && !s.Treasures.Contains("iron_skin") && s.Upgrades.Count == 0,
        string.Join(",", s.Treasures));
    Check("U3 the run's plan carries the upgraded passive", run.BuildPlan(s).PlayerTreasures.Contains("mending_charm"));
}

// ---------------------------------------------------------------- U4 the power slot (powerUpgrades)
{
    var data = Load();
    data.PowerUpgrades["CS2_034"] = new DungeonRunPowerUpgrade { From = "CS2_034", To = "mage_rimebite", On = DungeonRunTreasure.UpgradeOnActClear };
    var errs = DungeonRunValidator.Validate(data, null);
    Check("U4 a powerUpgrades row validates", errs.Count == 0, string.Join(" | ", errs.Take(3)));
    var run = new DungeonRun(data);
    var s = run.NewRun(fresh, "MAGE", data.Class("MAGE").DefaultDeck, "CS2_034", 77, signatureId: run.SignatureOffer(fresh, run.Data.FindClass("MAGE"), 77).FirstOrDefault());
    Check("U4 NewRun records a power-slot upgrade for the run's power", s.Upgrades.Count == 1 && s.Upgrades[0].Slot == DungeonRunTreasure.SlotPower && s.Upgrades[0].Key == "CS2_034");
    int major1 = MajorFloor(run, s, 1);
    var powers = new List<string>();
    Walk(run, s, x => x.Floor > major1 + 1, (st, floor) => powers.Add(floor + ":" + st.Power));
    Check("U4 the act I major swaps the run's Power; earlier wins do not", powers.Take(major1 - 1).All(p => p.EndsWith(":CS2_034")) && powers[major1 - 1] == major1 + ":mage_rimebite"
        && s.Upgrades.Count == 0, string.Join(" ", powers));
    if (s.Phase == DungeonRunPhase.PreMatch) Check("U4 the next plan uses the upgraded power", run.BuildPlan(s).PlayerHeroPowerId == "mage_rimebite");
    var unlocked = new DungeonRunProfile(); unlocked.Unlocked.AddRange(data.Unlocks.Keys);
    var other = run.NewRun(unlocked, "MAGE", data.Class("MAGE").DefaultDeck, "carto_survey", 77, signatureId: run.SignatureOffer(unlocked, run.Data.FindClass("MAGE"), 77).FirstOrDefault());
    Check("U4 a run on another power records no power upgrade", other.Upgrades.Count == 0 || other.Upgrades.All(u => u.Slot != DungeonRunTreasure.SlotPower));
    var bad = Load(); bad.PowerUpgrades["CS2_034"] = new DungeonRunPowerUpgrade { From = "CS2_034", To = "mage_rimebite", On = "sometimes" };
    bad.PowerUpgrades["nope"] = new DungeonRunPowerUpgrade { From = "nope", To = "mage_rimebite" };
    var be = DungeonRunValidator.Validate(bad, null);
    Check("U4 an unknown trigger and a power no row has are refused", be.Any(e => e.Contains("power upgrade CS2_034") && e.Contains("unknown trigger")) && be.Any(e => e.Contains("power upgrade nope") && e.Contains("no row has power")));
}

// ---------------------------------------------------------------- U5 the floors rule is unchanged (legacy and act runs)
{
    var data = Load(); var run = new DungeonRun(data);
    foreach (string schedule in new[] { DungeonRunSchedules.LegacyId, null })
    {
        var s = run.NewRun(fresh, "MAGE", data.Class("MAGE").DefaultDeck, "CS2_034", 11, schedule: schedule, signatureId: run.SignatureOffer(fresh, run.Data.FindClass("MAGE"), 11).FirstOrDefault());
        Grant(s, "charm_seedling");
        int start = s.Floor;
        var trace = new List<string>();
        Walk(run, s, x => x.Floor >= 6, (st, floor) => trace.Add(floor + ":" + string.Join("+", st.ActiveTreasures)));
        // Granted on floor 1 (SinceFloor 1): the floors rule (Floor - SinceFloor >= 2) fires on the floor-3 and floor-5 wins.
        bool Holds(int i, string key) => trace[i].Split(':')[1].Split('+')[0] == key;
        Check($"U5 {(schedule ?? "acts")}: floors rule: charm_sprouting from the floor-3 win, charm_blooming from the floor-5 win, then no record",
            Holds(0, "charm_seedling") && Holds(1, "charm_seedling") && Holds(2, "charm_sprouting") && Holds(3, "charm_sprouting") && Holds(4, "charm_blooming") && s.Upgrades.Count == 0, string.Join(" ", trace));
        Check($"U5 {(schedule ?? "acts")}: a deck-slot upgrade writes no Slot attribute", !StateXml(s).Contains(" Slot="));
    }
}

// ---------------------------------------------------------------- U6 version-4 round trip and migration
{
    var data = Load(); var run = new DungeonRun(data);
    // A class-row run (legacy and act): the phase-6 fields are absent from the file.
    foreach (string schedule in new[] { DungeonRunSchedules.LegacyId, null })
    {
        var s = run.NewRun(fresh, "MAGE", data.Class("MAGE").DefaultDeck, "CS2_034", 123, schedule: schedule, signatureId: run.SignatureOffer(fresh, run.Data.FindClass("MAGE"), 123).FirstOrDefault());
        Walk(run, s, x => x.Floor >= 3);
        string text = StateXml(s);
        Check($"U6 {(schedule ?? "acts")} class run: no phase-6 field is written",
            !new[] { "<Loadout", "PowerOverride", "<NextFight", " Slot=", "Progress=", "<Log>" }.Any(text.Contains), text.Length.ToString());
    }
    // Every new field set, written and read back.
    // Since F11 MAGE_ROGUE starts with a picked signature: the hero row with no signatures is a Heist row.
    var full = run.NewRun(fresh, "DALA_Rakanishu", "heist_mage_1", "CS2_034", 321);
    full.PowerOverride = "mage_rimebite";
    full.NextFight = new DungeonRunNextFight { Mana = 4, Draws = 2 };
    full.Cards[2].Log.Add("CS2_029"); full.Cards[2].Log.Add("CS2_024");
    full.Upgrades.Add(new DungeonRunUpgrade { Key = "CS2_034", SinceFloor = 1, Slot = DungeonRunTreasure.SlotPower, Progress = 1 });
    full.Loadout.Signature.Add("sig_field_atlas");
    var save = new DungeonRunSave { Active = full };
    string dir = Path.Combine(tmpRoot, "full"); Directory.CreateDirectory(dir);
    DungeonRunStore.Save(dir, save);
    var loaded = DungeonRunStore.Load(dir);
    var a = loaded.Active;
    Check("U6 every phase-6 field round-trips (save version 6, phase 8)", loaded.Version == 6 && a.PowerOverride == "mage_rimebite" && a.NextFight.Mana == 4 && a.NextFight.Draws == 2
        && a.Cards[2].Log.SequenceEqual(new[] { "CS2_029", "CS2_024" }) && a.Upgrades.Last().Slot == "power" && a.Upgrades.Last().Progress == 1
        && a.Loadout.Id == "DALA_Rakanishu" && a.Loadout.Signature.SequenceEqual(new[] { "sig_field_atlas" }) && Xml(loaded) == Xml(save));
    Check("U6 the round-tripped run passes the mirror check and resumes", DungeonRunDeck.MirrorError(a) == null && Try(() => run.Resume(a)));
    Check("U6 a hero row's loadout with no signature writes no Signature element", !StateXml(run.NewRun(fresh, "DALA_Rakanishu", "heist_mage_1", "CS2_034", 9)).Contains("<Signature"));
    // A version-4 file of a class run written by this build is byte-identical after a load and a save, and needs no migration.
    var plain = new DungeonRunSave { Active = run.NewRun(fresh, "WARRIOR", data.Class("WARRIOR").DefaultDeck, data.Class("WARRIOR").Powers[0], 555, signatureId: run.SignatureOffer(fresh, run.Data.FindClass("WARRIOR"), 555).FirstOrDefault()) };
    string pd = Path.Combine(tmpRoot, "plain"); Directory.CreateDirectory(pd);
    DungeonRunStore.Save(pd, plain);
    byte[] first = File.ReadAllBytes(Path.Combine(pd, DungeonRunStore.FileName));
    var plainLoaded = DungeonRunStore.Load(pd);
    bool upgraded = DungeonRunMigration.Upgrade(plainLoaded, run, null);
    DungeonRunStore.Save(pd, plainLoaded);
    Check("U6 a version-4 save without the new fields loads unchanged, needs no migration and re-saves byte for byte",
        !upgraded && first.SequenceEqual(File.ReadAllBytes(Path.Combine(pd, DungeonRunStore.FileName))));
    // A hand-written version-4 file with the new attributes (as a later phase-6 build would write it) loads.
    string hand = "<?xml version=\"1.0\" encoding=\"utf-8\"?><DungeonRun Version=\"4\"><Profile /><Active Class=\"MAGE\" Power=\"CS2_034\" PowerOverride=\"carto_survey\" Floor=\"1\" Phase=\"PreMatch\">"
        + "<NextFight Mana=\"3\" Draws=\"1\" /><Upgrades><DungeonRunUpgrade Key=\"CS2_034\" SinceFloor=\"1\" Slot=\"power\" Progress=\"2\" /></Upgrades></Active></DungeonRun>";
    string hd = Path.Combine(tmpRoot, "hand"); Directory.CreateDirectory(hd);
    File.WriteAllText(Path.Combine(hd, DungeonRunStore.FileName), hand);
    var h = DungeonRunStore.Load(hd).Active;
    Check("U6 a version-4 file carrying the new attributes reads them", h.PowerOverride == "carto_survey" && h.NextFight.Mana == 3 && h.NextFight.Draws == 1
        && h.Upgrades[0].Slot == "power" && h.Upgrades[0].Progress == 2 && run.PlayerPower(h) == "carto_survey");
}

bool Try(Action a) { try { a(); return true; } catch { return false; } }
Console.WriteLine($"c-upgrades: {passes} passed, {failures} failed");
return failures == 0 ? 0 : 1;
