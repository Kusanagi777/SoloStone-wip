using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SabberStoneCore.CardSets.DungeonRun;

// Step 9's released-schedule replay (c-enc-25 section L4; handoff/phase9/plan.md D5): runs of acts-v3h and acts-v3t saved at
// eight stops on the tree before step 9 (the close-out tip f62275b) are loaded and walked to their end by one fixed policy (every
// fight won, the first treasure, reward, modifier, anomaly option and bundle card, every tavern offer bought with its first
// candidate), and every step is written as a line holding the state's XML digest. verify/tools/gen/p9/record_replay_v3.sh
// compiles this file on an export of f62275b, which wrote the saves and verify/fixtures/p9/replay-v3/replay.txt; c-enc-25 walks
// the same saves on the current tree and compares line by line, attributing every moved line to a step-9 correctness fix.
// FROZEN: this file is compiled on f62275b, so it may use no member that tree lacks; it is never edited after the fixtures are
// recorded (an edit needs a new recording, and the recording is the reference).
internal static class ReplayV3
{
    /// <summary>
    /// The eight stops (the plan's L4 list): the schedule, the run row, the first seed tried and the stop's name. The recorder
    /// tries the seeds from the first one up and saves the first run that reaches its stop (<see cref="Reached"/>).
    /// </summary>
    internal static readonly (string Name, string Schedule, string Row, long Seed)[] Stops =
    {
        ("act1-anomaly", "acts-v3h", "MAGE", 31001),
        ("shrine-act1-anomaly", "acts-v3h", "TRLA_209h_Priest", 32001),
        ("finale-tavern", "acts-v3h", "DALA_Rakanishu", 33001),
        ("hagatha-prematch", "acts-v3h", "HUNTER", 34001),
        ("relay-prematch", "acts-v3t", "MAGE_ROGUE", 35001),
        ("lord-inmatch", "acts-v3t", "GILA_600h", 36001),
        ("modifier-node", "acts-v3t", "WARLOCK", 37001),
        ("mid-tavern", "acts-v3h", "DALA_Eudora", 38001),
    };

    internal const int SeedsTried = 400;

    internal static readonly System.Xml.Serialization.XmlSerializer Serializer = new System.Xml.Serialization.XmlSerializer(typeof(DungeonRunSave));

    internal static string Xml(DungeonRunSave save)
    {
        using var w = new StringWriter();
        Serializer.Serialize(w, save);
        return w.ToString().Replace("\r\n", "\n");
    }

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

    internal static string Ids(IEnumerable<string> ids) => string.Join(",", ids ?? Enumerable.Empty<string>());

    /// <summary>The file name of stop <paramref name="i"/>'s save.</summary>
    internal static string FileName(int i) => $"v3-{i + 1}-{Stops[i].Name}.xml";

    // The act of the run's current node (0 once the run has ended).
    static int Act(DungeonRun run, DungeonRunState s) => s.Finished ? 0 : run.CurrentNode(s).Act;

    /// <summary>Whether the run stands at stop <paramref name="name"/> (before the stop's own last move: see <see cref="Start"/>).</summary>
    internal static bool Reached(DungeonRun run, DungeonRunState s, string name)
    {
        if (s.Finished) return false;
        switch (name)
        {
            case "act1-anomaly":
                return s.Phase == DungeonRunPhase.Modifier && s.ModifierOffer?.Kind == DungeonRunGlobalRule.KindAnomaly && Act(run, s) == 1;
            case "shrine-act1-anomaly":
                // A shrine run's act I anomaly whose stored offer holds a rule the board shrine excludes (the offer the step-9
                // ordering fix no longer makes: before it, the board shrine was set after the first node opened).
                return s.Phase == DungeonRunPhase.Modifier && s.ModifierOffer?.Kind == DungeonRunGlobalRule.KindAnomaly && Act(run, s) == 1 && s.HasBoardShrine
                    && s.ModifierOffer.Offered.Any(id => run.Data.Schedules.Rule(id)?.NoBoardShrine == true);
            case "finale-tavern":
                return s.Phase == DungeonRunPhase.Tavern && run.CurrentAct(s).IsFinale;
            case "hagatha-prematch":
                return s.Phase == DungeonRunPhase.PreMatch && run.CurrentBoss(s).HeroCards.Count > 0;
            case "relay-prematch":
                return s.Phase == DungeonRunPhase.PreMatch && run.CurrentBoss(s).Relay.Count > 0;
            case "lord-inmatch":
                return s.Phase == DungeonRunPhase.PreMatch && run.CurrentBoss(s).HasFlag(DungeonRunEncounterData.PersistentDamage);
            case "modifier-node":
                return s.Phase == DungeonRunPhase.Modifier && s.ModifierOffer != null && s.ModifierOffer.Kind == null;
            case "mid-tavern":
                return s.Phase == DungeonRunPhase.Tavern && s.Tavern.Pending != null;
            default: throw new ArgumentException("unknown stop " + name);
        }
    }

    static DungeonRunProfile AllUnlocked(DungeonRun run)
    {
        var profile = new DungeonRunProfile();
        profile.Unlocked.AddRange(run.Data.Unlocks.Keys);
        return profile;
    }

    /// <summary>
    /// The save of stop <paramref name="i"/>: the first seed from the stop's own that reaches it. A new run of the stop's schedule
    /// and row (its first deck and power, the first signature offered) is walked by <see cref="Step"/>; at a tavern the
    /// purchases are made one at a time, so "mid-tavern" stops with a purchase waiting for its target; "lord-inmatch" starts the
    /// Plague Lord's fight (BeginMatch) and is saved in the match, as a crash leaves it.
    /// </summary>
    internal static DungeonRunSave Start(DungeonRun run, int i)
    {
        var (name, schedule, rowId, first) = Stops[i];
        var cls = run.Data.FindClass(rowId) ?? throw new ArgumentException("unknown row " + rowId);
        for (long seed = first; seed < first + SeedsTried; seed++)
        {
            var profile = AllUnlocked(run);
            var save = new DungeonRunSave
            {
                Version = DungeonRunSave.CurrentVersion, Profile = profile,
                Active = run.NewRun(profile, cls.Id, cls.Decks[0], cls.Powers[0], seed, schedule: schedule, signatureId: run.SignatureOffer(profile, cls, seed).FirstOrDefault()),
            };
            var s = save.Active;
            var scratch = new List<string>();
            for (int guard = 0; guard < 800 && !s.Finished; guard++)
            {
                if (Reached(run, s, name))
                {
                    if (name == "lord-inmatch") run.BeginMatch(s);
                    return save;
                }
                if (name == "mid-tavern" && s.Phase == DungeonRunPhase.Tavern && s.Tavern.Pending == null)
                {
                    // One purchase at a time; a refused one is passed over, and the visit ends when none waits for a target.
                    bool waiting = false;
                    foreach (string offer in s.Tavern.Offers.ToList())
                    {
                        if (s.Tavern.Bought.Any(b => b.Option == offer)) continue;
                        DungeonRunTavernNode.Buy(run, s, offer);
                        if (s.Tavern.Pending != null) { waiting = true; break; }
                    }
                    if (!waiting) run.LeaveTavern(s);
                    continue;
                }
                Step(run, save, scratch);
            }
        }
        throw new InvalidOperationException($"no seed from {first} to {first + SeedsTried - 1} reaches stop {name} on {schedule} with {rowId}");
    }

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
                var boss = run.CurrentBoss(s);
                seen = $"fight floor {s.Floor} boss {boss.Id} {plan.BossHero?.Id} {plan.BossHeroPowerId} health {plan.BossHealth} carried {plan.BossCarriedDamage}"
                    + $" | {plan.PlayerHero?.Id} {plan.PlayerHeroPowerId} deck {Digest(Ids(plan.PlayerDeck.Select(c => c.Id)))} boss deck {Digest(Ids(plan.BossDeck.Select(c => c.Id)))}"
                    + $" rules {Ids(plan.GlobalRules)} heroes {Ids(plan.PlayerHeroCards.Select(h => h.Hero.Id))} relay {Ids(plan.PlayerRelay.Select(h => h.Hero.Id))}";
                run.BeginMatch(s);
                int forms = Math.Max(1, plan.BossForms?.Count ?? 1);
                var end = new RunMatchEnd(0, forms - 1, forms - 1, 0, new[] { plan.PlayerHero.Id }, false);
                run.RecordOutcome(s, profile, DungeonRunOutcome.Win, null, end);
                break;
            }
            case DungeonRunPhase.TreasureDraft:
                seen = $"treasure {s.OpenDraft.Kind} {Ids(s.OpenDraft.Offered)}";
                run.PickTreasure(s, s.OpenDraft.Offered[0]);
                break;
            case DungeonRunPhase.Modifier:
                seen = $"{(s.ModifierOffer.Kind == DungeonRunGlobalRule.KindAnomaly ? "anomaly" : "modifier")} {Ids(s.ModifierOffer.Offered)}";
                run.PickModifier(s, s.ModifierOffer.Offered[0]);
                break;
            case DungeonRunPhase.Tavern:
            {
                seen = $"tavern offers {Ids(s.Tavern.Offers)} bob {Ids(s.Tavern.Bob)} friendly {Ids(s.Tavern.Friendly.Select(u => u.ToString()))} gold {s.Tavern.Gold}";
                // A purchase saved waiting for its target is finished first, with its first candidate.
                while (s.Tavern.Pending != null)
                {
                    string option = s.Tavern.Pending.Option;
                    var chosen = DungeonRunTavernNode.Choose(run, s, s.Tavern.Pending.Candidates[0]);
                    if (!chosen.Ok) DungeonRunTavernNode.Back(s);
                    seen += $" | pending {option} {(chosen.Ok ? "ok" : "no")}";
                }
                foreach (string offer in s.Tavern.Offers.ToList())
                {
                    if (s.Tavern.Bought.Any(b => b.Option == offer)) { seen += $" | {offer} bought"; continue; }
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

    /// <summary>
    /// The replay of one saved file: loaded as the plugin loads it (DungeonRunMigration.Upgrade, the identity on a version-6
    /// file), resumed (a save made in a match goes back to that floor's pre-match), then walked to the run's end. The first line
    /// names the file, the run's schedule and its state digest as loaded. <paramref name="before"/> (optional) sees the save
    /// before each step, with the step's index in the returned lines.
    /// </summary>
    internal static List<string> Walk(DungeonRun run, string path, Action<int, DungeonRunSave> before = null)
    {
        var save = Read(path);
        save.LoadedVersion = save.Version;
        DungeonRunMigration.Upgrade(save, run, _ => { });
        var s = save.Active;
        string phase = s.Phase.ToString();
        run.Resume(s);
        var log = new List<string> { $"save {Path.GetFileName(path)} v{save.Version} schedule v{s.ScheduleVersion} {s.Schedule} {s.Class} floor {s.Floor} {phase} -> {s.Phase} state {Digest(Xml(save))}" };
        for (int guard = 0; guard < 800 && !s.Finished; guard++)
        {
            before?.Invoke(log.Count, save);
            Step(run, save, log);
        }
        log.Add($"end {s.Phase} floor {s.Floor} matches {s.Matches.Count} deck {s.Deck.Count} lord damage {Ids((save.Profile.BossDamage ?? new List<DungeonRunBossDamage>()).Select(d => d.Boss + ":" + d.Remaining))} finished {s.Finished}");
        return log;
    }
}
