using System;
using System.Collections.Generic;
using System.Linq;

// Step 9 (handoff/phase9/plan.md section 3, stage 0 item 4 and lane S): the scored pick policy of the long-run walker
// (LongRunWalker.cs), shared by the step-9 test projects that link verify/suites/p9/common/. Every choice is scored, never taken
// in list order, so a pick does not depend on how an offer is ordered and different run seeds take different branches:
// - a treasure: a preferred id first (the walker's prefer list: the step-9 treasures it means to exercise, GILA_911 Rewind and
//   ULDA_111 Hearthstone), then rare before common, then the run's own adventure pool, then the seeded hash;
// - every other choice (power, signature, bundle, card, teammate, shrine upgrade, modifier, anomaly, tavern option or target)
//   by the seeded hash alone.
// The hash is StableSeed of the run seed, a per-choice salt, the node and floor, and the option id's FNV-1a hash: a pure
// function of the state, so a run reloaded from its save picks exactly as the run that was never reloaded.
internal static class PickPolicy
{
    internal const int SaltTreasure = 1, SaltReward = 2, SaltModifier = 3, SaltAnomaly = 4, SaltBundle = 5, SaltCard = 6,
        SaltTavernOption = 7, SaltTavernTarget = 8, SaltPower = 9, SaltSignature = 10, SaltOutcome = 11;

    /// <summary>FNV-1a of an id (the empty string for null).</summary>
    internal static long Fnv(string id)
    {
        uint h = 2166136261;
        foreach (char c in id ?? "") { h ^= c; h *= 16777619; }
        return h;
    }

    /// <summary>The seeded score of <paramref name="id"/> for a choice of kind <paramref name="salt"/> at the state's node and floor.</summary>
    internal static int Hash(DungeonRunState s, int salt, string id) =>
        StableSeed.Derive(s.Seed, 9200 + salt, s.Node * 64 + s.Floor, Fnv(id));

    /// <summary>A seeded roll in [0, n) keyed on the run, a salt and a component (the walker's outcome script).</summary>
    internal static int Roll(long seed, int salt, long component, int n) => StableSeed.Index(StableSeed.Derive(seed, 9200 + salt, component), Math.Max(1, n));

    /// <summary>The option with the highest seeded score (ties: ordinal id); null for an empty list.</summary>
    internal static string Best(DungeonRunState s, IEnumerable<string> options, int salt) =>
        options.OrderByDescending(x => Hash(s, salt, x)).ThenBy(x => x, StringComparer.Ordinal).FirstOrDefault();

    /// <summary>A treasure draft's pick: preferred first, then rare, then the run's adventure pool, then the seeded score.</summary>
    internal static string Treasure(DungeonRun run, DungeonRunState s, IEnumerable<string> offered, ICollection<string> prefer)
    {
        string pool = run.RunClass(s).Adventure;
        return offered.OrderByDescending(x => prefer != null && prefer.Contains(x))
            .ThenByDescending(x => run.Data.FindTreasure(x)?.Rarity == "rare")
            .ThenByDescending(x => pool != null && run.Data.FindTreasure(x)?.Pool == pool)
            .ThenByDescending(x => Hash(s, SaltTreasure, x)).ThenBy(x => x, StringComparer.Ordinal).FirstOrDefault();
    }
}
