using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// The close-out's legacy replay (c-enc-25 section L): runs saved before the close-out flipped the schedule defaults are loaded
// and walked to their end by one fixed policy (every fight won, the first treasure, modifier and bundle card, every tavern
// offer bought with its first candidate), and every step is written as a line holding the state's XML digest. The same file
// is compiled on the pre-close-out tree (wave 6, commit 68e7163) by verify/tools/gen/p8f25/record_replay.sh, which wrote the
// saves and verify/fixtures/p8f25/replay.txt; c-enc-25 walks the same saves on the current tree and compares line by line.
// Nothing here may use a member the pre-close-out tree lacks.
internal static class Replay
{
    /// <summary>The runs saved on the pre-close-out tree: a new run with no named schedule (then acts-v2), walked to a floor.</summary>
    internal static readonly (string Row, long Seed, int Floor)[] Starts =
    {
        ("MAGE", 101, 4), ("WARRIOR", 202, 7), ("PRIEST", 303, 10), ("HUNTER", 404, 1), ("DALA_Tekahn", 505, 6),
        ("MAGE_ROGUE", 606, 3), ("TRLA_209h_Druid", 707, 12), ("GILA_600h", 808, 5),
    };

    internal static readonly System.Xml.Serialization.XmlSerializer Serializer = new System.Xml.Serialization.XmlSerializer(typeof(DungeonRunSave));

    /// <summary>The save as XML with LF line ends (the digest is the same on every platform).</summary>
    internal static string Xml(DungeonRunSave save)
    {
        using var w = new StringWriter();
        Serializer.Serialize(w, save);
        return w.ToString().Replace("\r\n", "\n");
    }

    /// <summary>Writes a save as the fixture files hold it: UTF-8 without a BOM, indented, LF.</summary>
    internal static void Write(string path, DungeonRunSave save)
    {
        var settings = new System.Xml.XmlWriterSettings { Encoding = new System.Text.UTF8Encoding(false), Indent = true, NewLineChars = "\n" };
        using var stream = File.Create(path);
        using var writer = System.Xml.XmlWriter.Create(stream, settings);
        Serializer.Serialize(writer, save);
    }

    internal static DungeonRunSave Read(string path)
    {
        using var stream = File.OpenRead(path);
        return (DungeonRunSave)Serializer.Deserialize(stream);
    }

    internal static string Digest(string text)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text))).Replace("-", "").Substring(0, 16);
    }

    static string Ids(IEnumerable<string> ids) => string.Join(",", ids ?? Enumerable.Empty<string>());

    /// <summary>One step of the fixed policy; appends a line (what the step saw and the state's digest after it) to log.</summary>
    internal static void Step(DungeonRun run, DungeonRunSave save, List<string> log)
    {
        var s = save.Active;
        var profile = save.Profile;
        string seen;
        switch (s.Phase)
        {
            case DungeonRunPhase.PreMatch:
            {
                var plan = run.BuildPlan(s, profile);
                seen = $"fight floor {s.Floor} boss {run.CurrentBoss(s).Id} {plan.BossHero?.Id} {plan.BossHeroPowerId} health {plan.BossHealth}"
                    + $" | {plan.PlayerHero?.Id} {plan.PlayerHeroPowerId} deck {Digest(Ids(plan.PlayerDeck.Select(c => c.Id)))} boss deck {Digest(Ids(plan.BossDeck.Select(c => c.Id)))}"
                    + $" rules {Ids(plan.GlobalRules)}";
                run.BeginMatch(s);
                run.RecordOutcome(s, profile, DungeonRunOutcome.Win);
                break;
            }
            case DungeonRunPhase.TreasureDraft:
                seen = $"treasure {s.OpenDraft.Kind} {Ids(s.OpenDraft.Offered)}";
                run.PickTreasure(s, s.OpenDraft.Offered[0]);
                break;
            case DungeonRunPhase.Modifier:
                seen = $"modifier {Ids(s.ModifierOffer.Offered)}";
                run.PickModifier(s, s.ModifierOffer.Offered[0]);
                break;
            case DungeonRunPhase.Tavern:
            {
                seen = $"tavern offers {Ids(s.Tavern.Offers)} bob {Ids(s.Tavern.Bob)} friendly {Ids(s.Tavern.Friendly.Select(u => u.ToString()))}";
                foreach (string offer in s.Tavern.Offers.ToList())
                {
                    var bought = DungeonRunTavernNode.Buy(run, s, offer);
                    while (bought.Ok && s.Tavern.Pending != null) bought = DungeonRunTavernNode.Choose(run, s, s.Tavern.Pending.Candidates[0]);
                    if (s.Tavern.Pending != null) DungeonRunTavernNode.Back(s);
                    seen += $" | {offer} {(bought.Ok ? "ok" : "no")}";
                }
                run.LeaveTavern(s);
                break;
            }
            case DungeonRunPhase.BundleDraft:
            {
                if (s.OpenDraft.Takes == 0)
                {
                    seen = $"bundle {Ids(s.OpenDraft.Offered)}";
                    run.PickBundle(s, s.OpenDraft.Offered[0]);
                    break;
                }
                string bundle = s.OpenDraft.Choosing ?? s.OpenDraft.Offered[0];
                var left = run.OfferCards(s.OpenDraft, bundle);
                foreach (string kept in s.OpenDraft.Chosen) left.Remove(kept);
                seen = $"draft {bundle} {Ids(left)}";
                run.PickDraftCard(s, bundle, left[0]);
                break;
            }
            default: throw new InvalidOperationException("the replay stopped at " + s.Phase);
        }
        log.Add($"{seen} => {s.Phase} floor {s.Floor} node {s.Node} state {Digest(Xml(save))}");
    }

    /// <summary>A pre-close-out save of <see cref="Starts"/>[i]: a new run with no named schedule walked to the pre-match of its floor.</summary>
    internal static DungeonRunSave Start(DungeonRun run, int i)
    {
        var (row, seed, floor) = Starts[i];
        var profile = new DungeonRunProfile();
        profile.Unlocked.AddRange(run.Data.Unlocks.Keys);
        var cls = run.Data.FindClass(row);
        var save = new DungeonRunSave
        {
            Version = DungeonRunSave.CurrentVersion, Profile = profile,
            Active = run.NewRun(profile, cls.Id, cls.Decks[0], cls.Powers[0], seed, signatureId: run.SignatureOffer(profile, cls, seed).FirstOrDefault()),
        };
        var scratch = new List<string>();
        for (int guard = 0; guard < 400 && !(save.Active.Floor == floor && save.Active.Phase == DungeonRunPhase.PreMatch); guard++) Step(run, save, scratch);
        return save;
    }

    /// <summary>
    /// The replay of one saved file: loaded as the plugin loads it (version 5 files through DungeonRunMigration), resumed, then
    /// walked to the run's end. The first line names the file, the run's schedule and its state digest as loaded.
    /// </summary>
    internal static List<string> Walk(DungeonRun run, string path)
    {
        var save = Read(path);
        save.LoadedVersion = save.Version;
        DungeonRunMigration.Upgrade(save, run, _ => { });
        var s = save.Active;
        run.Resume(s);
        var log = new List<string> { $"save {Path.GetFileName(path)} v{save.Version} schedule v{s.ScheduleVersion} {s.Schedule ?? "(legacy)"} {s.Class} floor {s.Floor} {s.Phase} state {Digest(Xml(save))}" };
        for (int guard = 0; guard < 400 && !s.Finished; guard++) Step(run, save, log);
        log.Add($"end {s.Phase} floor {s.Floor} matches {s.Matches.Count} deck {s.Deck.Count} finished {s.Finished}");
        return log;
    }
}
