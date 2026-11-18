using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SabberStoneCore.Enums;

// Step 9 lane E (handoff/phase9/plan.md section 3, stage 2): the reward economy and act-aware scaling (decisions D4, D5 variant
// (b), D6, D9, D10, and the schedule side of D11). Everything here is read through DungeonRun.Tuned (an act run on a schedule of
// stamp 4 and above: acts-v4h, acts-v4t), so every released schedule (acts-v1, acts-v2, act1 at stamp 2; acts-v3h, acts-v3t at
// stamp 3) and every legacy run plays and replays exactly as released. The call sites in DungeonRun.cs are one-line hooks;
// the tavern's side (weightByAct, maxPerRun, banked gold) is in DungeonRunTavern.cs.
internal sealed partial class DungeonRun
{
    /// <summary>The TRL provenance (Rumble Run champions), capped per act by the v2 draw (schedule drawTuning.maxTrlPerAct).</summary>
    internal const string TrlProvenance = "TRL";

    // ------------------------------------------------------------------ knobs (D4)

    /// <summary>
    /// D4: caps the summed knobs of one fight, read through <see cref="Tuned"/>; nothing on a released schedule. On a stamp-4 run:
    /// an encounter row's v4 "healthOffset" replaces its own offset, the schedule's "offsetCap" (absent on the shipped v4
    /// schedules: D4's last lever) caps it, and the run's HealthBonus is capped at what the tavern's capped "health" options can
    /// give (DALA_904 Good Food: maxPerRun 2 x 5 Health), so a save that holds more plays with the cap.
    /// </summary>
    private void CapKnobs(DungeonRunState state, DungeonRunBoss boss, DungeonRunKnobs knobs)
    {
        if (!Tuned(state)) return;
        if (boss != null && boss.IsEncounter && boss.V4?.HealthOffset is int offset) knobs.HealthOffset = offset;
        if (Data.Schedules?.Find(state.Schedule)?.OffsetCap is int cap && knobs.HealthOffset > cap) knobs.HealthOffset = cap;
        int most = HealthBonusCap();
        if (most >= 0 && knobs.HealthBonus > most) knobs.HealthBonus = most;
    }

    /// <summary>
    /// The most HealthBonus a stamp-4 run keeps: the sum over the tavern's offered "health" options of maxPerRun x amount, or -1
    /// when one of them has no maxPerRun (or none is loaded). A tavern "health" option is the only HealthBonus writer.
    /// </summary>
    internal int HealthBonusCap()
    {
        var health = Data.Tavern?.Options.Where(o => o.Op == "health" && o.Weight + o.WeightByAct.Sum() > 0).ToList();
        if (health == null || health.Count == 0 || health.Any(o => o.MaxPerRun <= 0)) return -1;
        return health.Sum(o => o.MaxPerRun * Math.Max(0, o.Amount));
    }

    // ------------------------------------------------------------------ per-boss rewards (D9)

    /// <summary>
    /// D9: the won boss's own reward (an encounter row's v4 "reward": GILA_BOSS_43t for Damph), granted on a stamp-4 run after a
    /// won fight that is neither the last node nor a finale fight, before the treasure draft (RecordActWin). The reward row
    /// (run-v1.json "reward": true, runs "v4", offered false) is a card: it joins the run deck as a treasure card (origin
    /// treasure, never a tavern target) and the run's active treasures, once per run. A deck at deckMax has no room for it: the
    /// grant becomes 1 banked gold instead (<see cref="DungeonRunState.BankedGold"/>, spent at the next tavern).
    /// </summary>
    private void GrantEncounterReward(DungeonRunState state, DungeonRunNode node)
    {
        if (!Tuned(state) || node == null || !node.IsFight) return;
        var boss = CurrentBoss(state);
        string reward = boss.IsEncounter ? boss.V4?.Reward : null;
        if (reward == null) return;
        var treasure = Data.FindTreasure(reward);
        if (treasure == null || !treasure.Reward || !TreasureOfferedOn(treasure, state)) return;
        if (state.ActiveTreasures.Contains(reward) || state.Treasures.Contains(reward)) return;
        if (state.Deck.Count + 1 > DeckMax(state)) { state.BankedGold++; return; }
        state.ActiveTreasures.Add(reward);
        DungeonRunDeck.Add(state, reward, DungeonRunDeck.OriginTreasure);
    }

    // ------------------------------------------------------------------ deck growth (D5 variant (b))

    /// <summary>
    /// D5: a bundle draft an act run skips because the deck has no room (deckMax) becomes 1 banked gold on a stamp-4 run
    /// (<see cref="DungeonRunState.BankedGold"/>, spent at the next tavern: DungeonRunTavernNode.Open). Called by OpenBundleDraft
    /// on that skip only; nothing on a released schedule.
    /// </summary>
    private void ConvertSkippedDraft(DungeonRunState state)
    {
        if (Tuned(state)) state.BankedGold++;
    }

    /// <summary>
    /// D5 variant (b): on a stamp-4 run whose act says "bundleAfterTreasure": false (acts II and III of acts-v4h), a won fight
    /// on a treasure-draft node (treasureAfter or the passiveAtStart floor) or on the act's major opens no bundle draft. A
    /// skip by design, so no banked gold (that is a skip for room only). False on every released schedule.
    /// </summary>
    internal bool NoBundleAfter(DungeonRunState state, DungeonRunNode node)
    {
        if (!Tuned(state) || node == null || !node.IsFight) return false;
        var schedule = Data.Schedules?.Find(state.Schedule);
        if (schedule == null || node.Act < 1 || node.Act > schedule.Acts.Count) return false;
        var act = schedule.Acts[node.Act - 1];
        return !act.BundleAfterTreasure && (node.TreasureDraft || node.Kind == DungeonRunNodeKind.Major);
    }

    /// <summary>
    /// A8: the floors between a growing treasure's stages (the "floors" trigger): the schedule's "upgradeEveryFloors" on a
    /// stamp-4 run (acts-v4h and acts-v4t: 4, so charm_seedling takes 8 floors to full growth), else rules.upgradeEveryFloors.
    /// </summary>
    internal int UpgradeEveryFloors(DungeonRunState state)
    {
        if (!Tuned(state)) return Rules.UpgradeEveryFloors;
        int floors = Data.Schedules?.Find(state.Schedule)?.UpgradeEveryFloors ?? 0;
        return floors > 0 ? floors : Rules.UpgradeEveryFloors;
    }

    // ------------------------------------------------------------------ one treasure economy (D6)

    /// <summary>
    /// D6: whether a run of <paramref name="cls"/> may be offered <paramref name="t"/> by its scope: the scope matches (every
    /// run), or the run is a stamp-4 run and the treasure is a "shared" one (the audit's generic adventure treasures), whose
    /// scope then no longer limits it.
    /// </summary>
    internal static bool TreasureInScope(DungeonRunTreasure t, DungeonRunClass cls, DungeonRunState state) =>
        t.Scope.Matches(cls.ClassSet, state.StarterDeck, state.Power) || t.Shared && Tuned(state);

    /// <summary>
    /// D6: the treasure offer of a stamp-4 run (OpenTreasureDraft's candidates, already filtered and in ordinal order). Slot 1 a
    /// universal treasure (as released); slot 2 one of the loadout's own adventure (a hero row's "adventure" that names a
    /// treasure pool: Heist, MonsterHunt, Tombs) when it has one and none is offered yet; then weighted picks up to three.
    /// Weight: the act's "treasureRarity" (released 30 rare : 70 common when absent) x 1.5 for an affine treasure, which is one
    /// whose scope matches the run natively or whose tags meet the run's treasure tags (a shared treasure offered outside its
    /// scope is affine by its tags only). The draws are the released ones (TreasureStep, the draw index).
    /// </summary>
    private List<string> TunedTreasureOffer(DungeonRunState state, DungeonRunClass cls, List<DungeonRunTreasure> candidates, HashSet<string> classTags)
    {
        var act = CurrentAct(state);
        int rare = act?.TreasureRareWeight ?? 0, common = act?.TreasureCommonWeight ?? 0;
        if (rare + common <= 0) { rare = 30; common = 70; }
        int Weight(DungeonRunTreasure t)
        {
            int rarity = t.Rarity == "rare" ? rare : common;
            bool native = !t.Scope.IsEmpty && t.Scope.Matches(cls.ClassSet, state.StarterDeck, state.Power);
            bool affine = native || t.Tags.Any(classTags.Contains);
            return Math.Max(1, (int)Math.Round(rarity * (100 + 50 * (affine ? 1 : 0)) / 100.0, MidpointRounding.AwayFromZero));
        }
        var offer = new List<string>();
        int draw = 0;
        var universal = candidates.Where(t => t.Universal).ToList();
        if (universal.Count > 0) offer.Add(WeightedPick(universal, Weight, state.Seed, state.Floor, TreasureStep, draw++).Id);
        string origin = cls.Adventure;
        var own = origin == null ? new List<DungeonRunTreasure>() : candidates.Where(t => t.Pool == origin && !offer.Contains(t.Id)).ToList();
        if (own.Count > 0 && offer.Count < 3 && !offer.Any(id => Data.FindTreasure(id)?.Pool == origin))
            offer.Add(WeightedPick(own, Weight, state.Seed, state.Floor, TreasureStep, draw++).Id);
        while (offer.Count < 3)
        {
            var rest = candidates.Where(t => !offer.Contains(t.Id)).ToList();
            if (rest.Count == 0) break;
            offer.Add(WeightedPick(rest, Weight, state.Seed, state.Floor, TreasureStep, draw++).Id);
        }
        return offer;
    }

    // ------------------------------------------------------------------ the v2 provenance mix (D10)

    /// <summary>
    /// D10, the "provenance-mix-v2" draw of one slot (component act * 10 + slot; the major: act * 10 + 9): <paramref name="count"/>
    /// bosses of the pool (membership by DungeonRunSchedules.MemberTuned, which reads an encounter row's v4 tier), one at a time.
    /// A major slot of a run that reads the ledger takes the candidate with the most banked persistent damage first with the
    /// schedule's ledgerMajorChance (percent; the v1 draw always does). Then each pick draws a provenance with weight floor(10^4 n_p^alpha) / (1 + k drawn_p), n_p the slot's
    /// remaining candidates of that provenance and drawn_p the run's bosses of it so far, then a seeded boss of it; a provenance
    /// TRL (the Rumble Run champions) at the act's maxTrlPerAct is left out while another provenance has a candidate. Every
    /// random number keys on <see cref="EncounterStream"/>: the ledger roll component * 1000 + 200, the provenance roll
    /// component * 1000 + 300 + pick, the boss pick component * 1000 + pick (the v1 index).
    /// </summary>
    private List<string> MixedDrawV2(DungeonRunProfile profile, long seed, DungeonRunSchedule schedule, DungeonRunAct act, long component, string pool,
        int count, HashSet<string> used, Dictionary<string, int> drawn, DungeonRunProfile ledgerProfile, CardClass? excludeShrineClass)
    {
        var picks = new List<string>();
        var candidates = Data.Bosses.Where(b => IsUnlocked(profile, b.Unlock) && !used.Contains(b.Id) && Data.Schedules.MemberTuned(b, pool)
                && !(excludeShrineClass != null && HasShrineOfClass(b, excludeShrineClass.Value)))
            .Select(b => b.Id).OrderBy(id => id, StringComparer.Ordinal).ToList();
        if (candidates.Count < count)
            throw new InvalidDataException($"Schedule {schedule.Id} act {act.Id}: pool {pool} needs {count} bosses, has {candidates.Count}.");
        // The act's TRL count lives in the run's drawn map under a key no provenance can be (it starts with a control character).
        string trlKey = "\u0001" + TrlProvenance + "@" + (component / 10).ToString(System.Globalization.CultureInfo.InvariantCulture);
        int Drawn(string key) => drawn.TryGetValue(key, out int n) ? n : 0;
        void Take(string id)
        {
            string provenance = Data.Boss(id).Provenance;
            picks.Add(id); used.Add(id); candidates.Remove(id);
            drawn[provenance] = Drawn(provenance) + 1;
            if (provenance == TrlProvenance) drawn[trlKey] = Drawn(trlKey) + 1;
        }
        if (ledgerProfile != null && picks.Count < count
            && StableSeed.Index(StableSeed.Derive(seed, EncounterStream, component * 1000 + 200), 100) < schedule.LedgerMajorChance)
        {
            var banked = candidates.Where(id => Data.Boss(id).HasFlag(DungeonRunEncounterData.PersistentDamage) && ledgerProfile.DamageOf(id) != null)
                .OrderBy(id => ledgerProfile.DamageOf(id).Remaining).ThenBy(id => id, StringComparer.Ordinal).FirstOrDefault();
            if (banked != null) Take(banked);
        }
        while (picks.Count < count)
        {
            bool capped = schedule.MaxTrlPerAct > 0 && Drawn(trlKey) >= schedule.MaxTrlPerAct;
            var open = capped ? candidates.Where(id => Data.Boss(id).Provenance != TrlProvenance).ToList() : candidates;
            if (open.Count == 0) open = candidates;
            var provenances = open.Select(id => Data.Boss(id).Provenance).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList();
            var weights = provenances.Select(p => ProvenanceWeight(open.Count(id => Data.Boss(id).Provenance == p), Drawn(p),
                schedule.DrawAlphaPermille, schedule.DrawPenaltyPermille)).ToList();
            long total = weights.Sum();
            string provenance = provenances[provenances.Count - 1];
            if (total > 0)
            {
                long roll = StableSeed.Index(StableSeed.Derive(seed, EncounterStream, component * 1000 + 300 + picks.Count), (int)Math.Min(total, int.MaxValue));
                for (int i = 0; i < provenances.Count; i++)
                {
                    roll -= weights[i];
                    if (roll < 0) { provenance = provenances[i]; break; }
                }
            }
            var group = open.Where(id => Data.Boss(id).Provenance == provenance).ToList();
            Take(group[StableSeed.Index(StableSeed.Derive(seed, EncounterStream, component * 1000 + picks.Count), group.Count)]);
        }
        return picks;
    }

    /// <summary>
    /// D10: a provenance's weight in the v2 draw, floor(10^4 x n^(alpha/1000)) x 1000 / (1000 + k x drawn), in exact integer
    /// arithmetic (the same on every platform): <paramref name="n"/> the slot's remaining candidates of the provenance,
    /// <paramref name="drawn"/> the run's bosses of it so far, alpha and k in permille. 0 when n is 0.
    /// </summary>
    internal static long ProvenanceWeight(int n, int drawn, int alphaPermille, int penaltyPermille)
    {
        long scaled = ScaledPower(n, alphaPermille);
        return scaled * 1000 / (1000 + (long)Math.Max(0, penaltyPermille) * Math.Max(0, drawn));
    }

    /// <summary>floor(10^4 x n^(alpha/1000)) for n at least 0 and alpha 0 to 1000, exactly (an integer q-th root by bisection).</summary>
    internal static long ScaledPower(int n, int alphaPermille)
    {
        if (n <= 0) return 0;
        int a = Math.Max(0, Math.Min(1000, alphaPermille));
        int g = Gcd(a, 1000);
        int p = a / g, q = 1000 / g;
        // The largest y with y^q <= n^p * (10^4)^q.
        var target = System.Numerics.BigInteger.Pow(n, p) * System.Numerics.BigInteger.Pow(10000, q);
        long lo = 0, hi = 10000L * n + 1;
        while (lo < hi)
        {
            long mid = lo + (hi - lo + 1) / 2;
            if (System.Numerics.BigInteger.Pow(mid, q) <= target) lo = mid; else hi = mid - 1;
        }
        return lo;
    }

    private static int Gcd(int a, int b)
    {
        while (b != 0) { int t = a % b; a = b; b = t; }
        return Math.Max(1, a);
    }
}
