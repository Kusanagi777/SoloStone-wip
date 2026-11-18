using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using SabberStoneCore.CardSets.DungeonRun;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;

// Step 9 lane S (handoff/phase9/plan.md section 3, stage 1a; criteria D1, D2, D4): the deterministic run-layer walker, with no
// engine games. It walks one run of the shipped data (encounters loaded) one atomic step at a time: a fight (BuildPlan,
// BeginMatch, RecordOutcome with a synthetic RunMatchEnd), one treasure, reward or modifier pick, one bundle or card pick, one
// tavern purchase, choice, cancel or leave. Every choice is the scored pick of PickPolicy.cs. The outcome script is seeded:
// every fight won (the run ends in Victory) or one fight lost (a scripted Defeat), and on a stamp-4 schedule an escape
// (ULDA_111 Hearthstone) or a restart (GILA_911 Rewind) whenever the fight's plan allows one and the run asked for it.
//
// The helpers below are what the tests check at each step: the save's digest (the whole save, profile included), the reload
// (Save to a file, Load, Upgrade, Resume), the restage of a crash at InMatch, the plan's digest, the card-legality and
// NativeMatch-limit invariants. Linked by verify/suites/p9/c-longrun-9 (and by any later step-9 project that walks runs).
internal sealed class LongRunWalker
{
    internal readonly DungeonRun Run;
    internal DungeonRunSave Save;
    /// <summary>The floor whose fight the script loses (a scripted Defeat); 0: every fight is won (Victory).</summary>
    internal int DefeatFloor;
    /// <summary>Escape (ULDA_111) from the first fight whose plan allows it (a stamp-4 run holding the card).</summary>
    internal bool TryEscape;
    /// <summary>Restart (GILA_911) the first fight whose plan offers Rewind, once.</summary>
    internal bool TryRestart;
    /// <summary>Treasures the treasure picks prefer (the step-9 ones the walk means to exercise).</summary>
    internal ICollection<string> Prefer = new[] { RunMatchEnds.Rewind, RunMatchEnds.Hearthstone };
    /// <summary>Called at every pre-match with the plan BuildPlan returned (null when it threw) and the exception (or null).</summary>
    internal Action<LongRunWalker, RunMatchPlan, Exception> OnPlan;
    /// <summary>Called at every pre-match before the fight starts (the crash check runs here, on a copy of the save).</summary>
    internal Action<LongRunWalker> OnPreMatch;

    internal int Steps, Fights, Escapes, Restarts, Losses, HeroSwitchFights, RelayFights, PersistentFights, RestagedFights;
    internal readonly List<string> Notes = new List<string>();
    // The tavern options tried at each visit (node and fight count): a failed purchase changes nothing, so it is not tried again.
    private readonly Dictionary<string, HashSet<string>> tried = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
    private bool escaped, restarted;

    internal LongRunWalker(DungeonRun run, DungeonRunSave save) { Run = run; Save = save; }

    internal DungeonRunState State => Save.Active;
    internal bool Done => State.Finished;

    /// <summary>One atomic step of the policy; returns what it saw and did.</summary>
    internal string Step()
    {
        var s = State;
        var profile = Save.Profile;
        Steps++;
        switch (s.Phase)
        {
            case DungeonRunPhase.PreMatch: return Fight(s, profile);
            case DungeonRunPhase.TreasureDraft:
            {
                var draft = s.OpenDraft;
                string pick = draft.Kind == "treasure" ? PickPolicy.Treasure(Run, s, draft.Offered, Prefer) : PickPolicy.Best(s, draft.Offered, PickPolicy.SaltReward);
                Run.PickTreasure(s, pick);
                return $"{draft.Kind} {Ids(draft.Offered)} > {pick}";
            }
            case DungeonRunPhase.Modifier:
            {
                var offer = s.ModifierOffer;
                bool anomaly = offer.Kind == DungeonRunGlobalRule.KindAnomaly;
                string pick = PickPolicy.Best(s, offer.Offered, anomaly ? PickPolicy.SaltAnomaly : PickPolicy.SaltModifier);
                Run.PickModifier(s, pick);
                return $"{(anomaly ? "anomaly" : "modifier")} {Ids(offer.Offered)} > {pick}";
            }
            case DungeonRunPhase.BundleDraft:
            {
                var draft = s.OpenDraft;
                if (draft.Takes == 0)
                {
                    string bundle = PickPolicy.Best(s, draft.Offered, PickPolicy.SaltBundle);
                    Run.PickBundle(s, bundle);
                    return $"bundle {Ids(draft.Offered)} > {bundle}";
                }
                string from = draft.Choosing ?? PickPolicy.Best(s, draft.Offered, PickPolicy.SaltBundle);
                var left = Run.OfferCards(draft, from);
                foreach (string kept in draft.Chosen) left.Remove(kept);
                string card = PickPolicy.Best(s, left.Distinct(), PickPolicy.SaltCard);
                Run.PickDraftCard(s, from, card);
                return $"draft {from} {Ids(left)} > {card}";
            }
            case DungeonRunPhase.Tavern: return Tavern(s);
            default: throw new InvalidOperationException("the walker cannot step from " + s.Phase);
        }
    }

    private string Tavern(DungeonRunState s)
    {
        var t = s.Tavern;
        if (t.Pending != null)
        {
            string option = t.Pending.Option;
            string target = PickPolicy.Best(s, t.Pending.Candidates, PickPolicy.SaltTavernTarget);
            var chosen = DungeonRunTavernNode.Choose(Run, s, target);
            if (chosen.Ok) return $"tavern {option} > {target} ok{(s.Tavern?.Pending != null ? ", pending again" : "")}";
            DungeonRunTavernNode.Back(s);
            return $"tavern choose {target} refused ({chosen.Error}), back";
        }
        string key = s.Node + "/" + s.Matches.Count;
        if (!tried.TryGetValue(key, out var done)) tried[key] = done = new HashSet<string>(StringComparer.Ordinal);
        string next = t.Offers.Where(o => !done.Contains(o)).OrderByDescending(o => PickPolicy.Hash(s, PickPolicy.SaltTavernOption, o))
            .ThenBy(o => o, StringComparer.Ordinal).FirstOrDefault();
        if (next == null)
        {
            int gold = t.Gold;
            Run.LeaveTavern(s);
            return $"tavern leave gold {gold} offers {Ids(t.Offers)} bought {Ids(t.Bought.Select(b => b.Option))}";
        }
        done.Add(next);
        var bought = DungeonRunTavernNode.Buy(Run, s, next);
        return $"tavern buy {next} {(bought.Ok ? s.Tavern?.Pending != null ? "pending" : "ok" : "no (" + bought.Error + ")")}";
    }

    private string Fight(DungeonRunState s, DungeonRunProfile profile)
    {
        OnPreMatch?.Invoke(this);
        RunMatchPlan plan = null;
        Exception thrown = null;
        try { plan = Run.BuildPlan(s, profile); } catch (Exception e) { thrown = e; }
        OnPlan?.Invoke(this, plan, thrown);
        if (plan == null) throw new InvalidOperationException("BuildPlan threw at floor " + s.Floor + ": " + thrown?.Message, thrown);
        var boss = Run.CurrentBoss(s);
        Fights++;
        if (s.Attempt > 0 || s.RestartIndex > 0) RestagedFights++;
        if (plan.PlayerHeroCards.Count > 0) HeroSwitchFights++;
        if (plan.PlayerRelay.Count > 0) RelayFights++;
        if (boss.HasFlag(DungeonRunEncounterData.PersistentDamage)) PersistentFights++;
        Run.BeginMatch(s);
        long k = s.Floor * 16 + s.Attempt + 4 * s.RestartIndex;
        int forms = Math.Max(1, plan.BossForms?.Count ?? 1);
        var heroes = new List<string> { plan.PlayerHero.Id };
        if (plan.PlayerHeroCards.Count > 0 && PickPolicy.Roll(s.Seed, PickPolicy.SaltOutcome, k, 2) == 0)
            heroes.Add(plan.PlayerHeroCards[PickPolicy.Roll(s.Seed, PickPolicy.SaltOutcome, k + 1, plan.PlayerHeroCards.Count)].Hero.Id);
        if (plan.PlayerRelay.Count > 0)
            heroes.AddRange(plan.PlayerRelay.Take(PickPolicy.Roll(s.Seed, PickPolicy.SaltOutcome, k + 2, plan.PlayerRelay.Count + 1)).Select(h => h.Hero.Id));
        int damage = PickPolicy.Roll(s.Seed, PickPolicy.SaltOutcome, k + 3, 25);
        int gnome = !boss.HasFlag(DungeonRunEncounterData.PersistentDamage) && (s.Treasures.Contains("ULDA_115") || s.ActiveTreasures.Contains("ULDA_115")) ? 10 : 0;
        string head = $"fight floor {s.Floor} {boss.Id} health {plan.BossHealth} plan {PlanDigest(plan)}";
        if (TryRestart && !restarted && plan.Restarts.Contains(RunMatchEnds.Rewind))
        {
            restarted = true; Restarts++;
            var end = new RunMatchEnd(Math.Max(1, plan.BossHealth / 2), 0, 0, 30, heroes, false, restarted: true, restartSource: RunMatchEnds.Rewind, turns: 24);
            var notes = Run.RecordOutcome(s, profile, DungeonRunOutcome.Fault, null, end);
            Notes.AddRange(notes);
            return head + " => restart " + string.Join("; ", notes);
        }
        if (TryEscape && !escaped && plan.EscapeAllowed && s.Floor != DefeatFloor)
        {
            escaped = true; Escapes++;
            var end = new RunMatchEnd(Math.Max(1, plan.BossHealth - 7), 0, 0, damage, heroes, false, escaped: true, turns: 12);
            var notes = Run.RecordOutcome(s, profile, DungeonRunOutcome.Win, null, end);
            Notes.AddRange(notes);
            return head + " => escape " + string.Join("; ", notes);
        }
        if (s.Floor == DefeatFloor)
        {
            Losses++;
            int left = 1 + PickPolicy.Roll(s.Seed, PickPolicy.SaltOutcome, k + 4, Math.Max(1, plan.BossHealth - 1));
            var end = new RunMatchEnd(left, 0, 0, 30, heroes, false, turns: 30);
            Run.RecordOutcome(s, profile, DungeonRunOutcome.Loss, null, end);
            return head + " => loss, boss left at " + left;
        }
        var won = new RunMatchEnd(0, forms - 1, forms - 1, damage, heroes, false, gnome, turns: 20);
        var wonNotes = Run.RecordOutcome(s, profile, DungeonRunOutcome.Win, null, won);
        Notes.AddRange(wonNotes);
        return head + " => win" + (heroes.Count > 1 ? " heroes " + Ids(heroes) : "");
    }

    // ------------------------------------------------------------------ the save: digest, reload, crash

    private static readonly System.Xml.Serialization.XmlSerializer Serializer = new System.Xml.Serialization.XmlSerializer(typeof(DungeonRunSave));

    /// <summary>The save as XML with LF line ends.</summary>
    internal static string Xml(DungeonRunSave save)
    {
        using var w = new StringWriter();
        Serializer.Serialize(w, save);
        return w.ToString().Replace("\r\n", "\n");
    }

    internal static string Hash16(string text)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").Substring(0, 16);
    }

    /// <summary>The digest of the whole save: the run in progress and the profile (stats, archive, unlocks, boss damage).</summary>
    internal static string Digest(DungeonRunSave save) => Hash16(Xml(save));

    /// <summary>
    /// The reload of the plugin's load path: the save is written to <paramref name="dir"/> (DungeonRunStore.Save, the file the
    /// runtime writes), loaded back (DungeonRunStore.Load), upgraded (DungeonRunMigration.Upgrade: a version-6 file is the
    /// identity, reported in <paramref name="upgraded"/>) and resumed (DungeonRun.Resume, which also refuses unknown ids).
    /// </summary>
    internal static DungeonRunSave Reload(DungeonRun run, DungeonRunSave save, string dir, out bool upgraded)
    {
        DungeonRunStore.Save(dir, save);
        var loaded = DungeonRunStore.Load(dir);
        upgraded = DungeonRunMigration.Upgrade(loaded, run, _ => { });
        if (loaded.Active != null) run.Resume(loaded.Active);
        return loaded;
    }

    /// <summary>
    /// D2: a crash at InMatch on the current pre-match, on a copy of the save: the fight starts (BeginMatch), the save is written
    /// and reloaded (<see cref="Reload"/>: Resume puts the floor back at pre-match), and the restaged state and plan are compared
    /// with the pre-match before the crash. The only differences allowed are Attempt (one more) and the floor's HeroSwitch
    /// (BeginMatch stores it and Resume keeps it: the documented PreMatch HeroSwitch quirk, equal to HeroSwitchFor). Returns null
    /// when the restage is identical, else what differs.
    /// </summary>
    internal static string CrashRestage(DungeonRun run, DungeonRunSave save, string dir)
    {
        var before = DungeonRunStore.Clone(save);
        string planBefore = PlanDigest(run.BuildPlan(before.Active, before.Profile)) + " seed " + run.MatchSeed(before.Active);
        var crash = DungeonRunStore.Clone(save);
        run.BeginMatch(crash.Active);
        var expectSwitch = run.HeroSwitchFor(before.Active);
        var loaded = Reload(run, crash, dir, out bool upgraded);
        var s = loaded.Active;
        var problems = new List<string>();
        if (upgraded) problems.Add("the reload upgraded a version-6 save");
        if (s.Phase != DungeonRunPhase.PreMatch) problems.Add("phase " + s.Phase + " after the reload");
        if (s.Attempt != before.Active.Attempt + 1) problems.Add($"Attempt {before.Active.Attempt} -> {s.Attempt}");
        if (Hash16(SwitchXml(s.HeroSwitch)) != Hash16(SwitchXml(expectSwitch))) problems.Add("HeroSwitch " + SwitchXml(s.HeroSwitch) + " is not HeroSwitchFor's " + SwitchXml(expectSwitch));
        // Mask the two allowed differences, then the whole save must be the pre-match's byte for byte.
        int attempt = s.Attempt; var heroSwitch = s.HeroSwitch;
        s.Attempt = before.Active.Attempt; s.HeroSwitch = before.Active.HeroSwitch;
        string masked = Xml(loaded), original = Xml(before);
        s.Attempt = attempt; s.HeroSwitch = heroSwitch;
        if (masked != original) problems.Add("the restaged save differs beyond Attempt and HeroSwitch: " + FirstDiff(original, masked));
        string planAfter = null;
        try { planAfter = PlanDigest(run.BuildPlan(s, loaded.Profile)) + " seed " + run.MatchSeed(s); }
        catch (Exception e) { problems.Add("BuildPlan threw after the restage: " + e.Message); }
        if (planAfter != null && planAfter != planBefore) problems.Add("plan " + planBefore + " -> " + planAfter);
        return problems.Count == 0 ? null : string.Join("; ", problems);
    }

    private static string SwitchXml(DungeonRunHeroSwitch hs)
    {
        if (hs == null) return "(none)";
        var ser = new System.Xml.Serialization.XmlSerializer(typeof(DungeonRunHeroSwitch));
        using var w = new StringWriter();
        ser.Serialize(w, hs);
        return w.ToString().Replace("\r\n", "\n");
    }

    internal static string FirstDiff(string a, string b)
    {
        var x = a.Split('\n'); var y = b.Split('\n');
        for (int i = 0; i < Math.Max(x.Length, y.Length); i++)
        {
            string l = i < x.Length ? x[i] : "(none)", r = i < y.Length ? y[i] : "(none)";
            if (l != r) return $"line {i + 1}: {Trim(l)} | {Trim(r)}";
        }
        return "(same)";
    }

    private static string Trim(string s) { s = s.Trim(); return s.Length > 160 ? s.Substring(0, 160) + "..." : s; }

    // ------------------------------------------------------------------ the plan's digest

    /// <summary>
    /// The digest of everything a match plan holds (every public property, by reflection: cards as ids, lists in order, nested
    /// objects by their properties), so two plans with the same digest start the same match.
    /// </summary>
    internal static string PlanDigest(RunMatchPlan plan) => Hash16(Dump(plan, 0));

    internal static string Dump(object o, int depth)
    {
        if (o == null) return "~";
        if (o is string str) return "\"" + str + "\"";
        if (o is Card card) return card.Id;
        var type = o.GetType();
        if (type.IsPrimitive || type.IsEnum || o is decimal) return Convert.ToString(o, System.Globalization.CultureInfo.InvariantCulture);
        if (depth > 6) return "...";
        if (o is IDictionary dict)
        {
            var parts = new List<string>();
            foreach (DictionaryEntry e in dict) parts.Add(Dump(e.Key, depth + 1) + ":" + Dump(e.Value, depth + 1));
            parts.Sort(StringComparer.Ordinal);
            return "{" + string.Join(",", parts) + "}";
        }
        if (o is IEnumerable seq)
        {
            var parts = new List<string>();
            foreach (object x in seq) parts.Add(Dump(x, depth + 1));
            return "[" + string.Join(",", parts) + "]";
        }
        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.GetIndexParameters().Length == 0)
            .OrderBy(p => p.Name, StringComparer.Ordinal);
        return type.Name + "{" + string.Join(",", props.Select(p => p.Name + "=" + Dump(p.GetValue(o), depth + 1))) + "}";
    }

    // ------------------------------------------------------------------ invariants (D4)

    private static OfflinePoolPolicy policy;
    private static readonly Dictionary<string, string[]> legalityCache = new Dictionary<string, string[]>(StringComparer.Ordinal);

    /// <summary>
    /// D4: the card-legality failures of the run deck now, one line each: "&lt;origin&gt; &lt;code&gt; &lt;id&gt;", or
    /// "exempt:&lt;name&gt; &lt;origin&gt; &lt;code&gt; &lt;id&gt;" for a failure one of the three documented exemptions covers
    /// (<see cref="Exemptions"/>). Every card record goes through the drafted-card predicate with no grant
    /// (DungeonRunValidator.PoolCardFailures: client, scripting, meta, deck size, sideboard, seat order, admission, and the run's
    /// loot classes); then the Death Knight runes of the whole deck, the opening-flag limit (and no flag on a treasure card), the
    /// deck mirror and deckMax. A legacy run has no records.
    /// </summary>
    internal static List<string> Legality(DungeonRun run, DungeonRunState s)
    {
        var lines = new List<string>();
        if (!s.IsActRun) return lines;
        policy ??= OfflinePoolPolicy.Load();
        var classes = run.LootClasses(s);
        string classKey = string.Join("/", classes) + (s.IsCatalogRun ? "+catalog" : "");
        HashSet<string> grants = null;
        foreach (var c in s.Cards)
        {
            // The predicate is a pure function of the id, the class set and the catalog flag: memoized for the process.
            string key = c.Id + "|" + classKey;
            if (!legalityCache.TryGetValue(key, out var codes))
                legalityCache[key] = codes = DungeonRunValidator.PoolCardFailures(c.Id, "run deck", classes, false, policy, run.Data.ClientKnows, s.IsCatalogRun)
                    .Select(f => f.Code).ToArray();
            foreach (string code in codes)
            {
                grants ??= new HashSet<string>(s.Drafts.SelectMany(d => d.Offers).SelectMany(o => o.Grants), StringComparer.Ordinal);
                string exempt = Exemption(c, code, grants);
                lines.Add((exempt != null ? "exempt:" + exempt + " " : "") + c.Origin + " " + code + " " + c.Id);
            }
        }
        if (!DungeonRunDeck.RunesLegal(run, s, s.Deck)) lines.Add("deck runes " + Ids(s.Deck.Where(DungeonRunValidator.HasRuneCost)));
        int flags = s.Cards.Count(c => c.Opening);
        if (flags > DungeonRunDeck.MaxOpeningFlags) lines.Add("deck opening-flags " + flags);
        if (s.Cards.Any(c => c.Opening && DungeonRunDeck.IsTreasureOrigin(c.Origin))) lines.Add("deck opening-flag-on-treasure");
        string mirror = DungeonRunDeck.MirrorError(s);
        if (mirror != null) lines.Add("deck mirror " + mirror);
        if (s.Deck.Count > run.DeckMax(s)) lines.Add("deck over-max " + s.Deck.Count);
        return lines;
    }

    /// <summary>
    /// D4's three documented exemptions, each a class failure the run allows by design (any other code is never exempt):
    /// "bundle-grant", a drafted card its catalog bundle offer granted (catalog-v1.json grants: the offer stores it in Grants
    /// and PickCatalogBundle checks it as a grant); "friendly-smith", a tavern weapon of any class with ULDA_603e (tavern-v1.json
    /// ULDA_603 discoverBuff, pool anyClassWeapon: class waived, runes still apply); "run-grant", a card the run itself grants
    /// (a treasure shell, a signature treasure, a Rumble teammate).
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, string> Exemptions = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["bundle-grant"] = "a drafted card its catalog bundle offer granted (the offer's Grants; PickCatalogBundle checks it as a grant)",
        ["friendly-smith"] = "a tavern weapon of any class carrying ULDA_603e (ULDA_603 Friendly Smith, discoverBuff: class waived)",
        ["run-grant"] = "a card the run grants (a treasure shell, a signature, a Rumble teammate: never checked for class)",
    };

    private static string Exemption(DungeonRunCard c, string code, HashSet<string> grants)
    {
        if (code != "class") return null;
        if (c.Origin == DungeonRunDeck.OriginDraft && grants.Contains(c.Id)) return "bundle-grant";
        if (c.Origin == DungeonRunDeck.OriginTavern && c.Mods.Contains(FriendlySmithEnchantment)) return "friendly-smith";
        if (DungeonRunDeck.IsTreasureOrigin(c.Origin) || c.Origin == DungeonRunDeck.OriginTeammate) return "run-grant";
        return null;
    }

    internal const string FriendlySmithEnchantment = "ULDA_603e";

    /// <summary>
    /// D4: the checks NativeMatch's run-fight constructor makes on a plan (plugin/NativeMatch.cs: a hero card, a run deck of 10
    /// to deckMax real cards, a boss deck of 30, or 10 to 60 for an imported encounter), plus the run deck within the schedule's
    /// deckMin..deckMax at pre-match. Null when every one holds.
    /// </summary>
    internal static string NativeLimits(DungeonRun run, DungeonRunState s, RunMatchPlan plan)
    {
        var problems = new List<string>();
        int deckMax = run.DeckMax(s);
        if (plan.PlayerHero == null || plan.PlayerHero.Type != CardType.HERO) problems.Add("run hero " + plan.PlayerHero?.Id);
        if (plan.PlayerDeck == null || plan.PlayerDeck.Count < 10 || plan.PlayerDeck.Count > deckMax || plan.PlayerDeck.Any(c => c == null))
            problems.Add("run deck " + (plan.PlayerDeck?.Count ?? -1) + " of 10.." + deckMax);
        if (plan.BossDeck == null || plan.BossDeck.Any(c => c == null)
            || (plan.EncounterDeck ? plan.BossDeck.Count < 10 || plan.BossDeck.Count > 60 : plan.BossDeck.Count != 30))
            problems.Add((plan.EncounterDeck ? "encounter" : "boss") + " deck " + (plan.BossDeck?.Count ?? -1));
        if (s.IsActRun && (s.Deck.Count < run.DeckMin(s) || s.Deck.Count > deckMax)) problems.Add("run deck " + s.Deck.Count + " outside " + run.DeckMin(s) + ".." + deckMax);
        if (plan.PlayerDeckSlots != null && plan.PlayerDeckSlots.Count > 0 && plan.PlayerDeckSlots.Count != plan.PlayerDeck?.Count)
            problems.Add("deck slots " + plan.PlayerDeckSlots.Count + " for " + plan.PlayerDeck?.Count + " cards");
        if (plan.TurnCap < 1) problems.Add("turn cap " + plan.TurnCap);
        return problems.Count == 0 ? null : string.Join("; ", problems);
    }

    internal static string Ids(IEnumerable<string> ids) => string.Join(",", ids ?? Enumerable.Empty<string>());
}
