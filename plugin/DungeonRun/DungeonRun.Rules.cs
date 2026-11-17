using System;
using System.Collections.Generic;
using System.Linq;

// Step 9 lane R (handoff/phase9/plan.md section 3, stage 2): incompatibility prevention. The run-layer logic of the lane lives
// here; DungeonRun.cs holds only its hooks (RuleCandidates, RollTwists, ModifierCandidates, OpenModifier, PickModifier, NewRun's
// board shrine, BuildPlan's log line, the treasure draft's filter) and DungeonRunTavern.cs the opening-flag gate.
//
// Two kinds of change, as the plan's ground rules separate them:
// - correctness fixes (every schedule with anomaly or twist nodes, v3 and v4): the rule exclusions of schedule-v1.json and the
//   step-9 boss flags, the board shrine set before the twist roll and the first anomaly offer, run-scope rules checked against
//   every act, the treasure and opening-flag exclusions;
// - v4 tuning, read only through Tuned (a stamp-4 schedule): D7's per-fight neutralisation and the "none" modifier option, and
//   D8's weighted twist pick.
internal sealed partial class DungeonRun
{
    /// <summary>
    /// D17: where <see cref="BuildPlan"/> reports a state that holds a forbidden pair (<see cref="ForbiddenPairs"/>), one line
    /// each. Null (the default: every suite and tool) computes nothing. The plan is built as before either way: a violation is
    /// logged, never dropped or thrown, so a released run is never changed and a save is never bricked.
    /// </summary>
    internal static Action<string> ForbiddenPairLog;

    /// <summary>
    /// D7: removes, for the fight against <paramref name="boss"/> only, the knobs of <paramref name="modifier"/> that the boss
    /// row's v4 "incompatibleModifiers" names (read through <see cref="Tuned"/>). Called by <see cref="KnobsFor"/> once per
    /// active modifier, before the sum. A neutralised modifier keeps everything outside the fight (DraftOptions, TavernGold and
    /// Iron Bosses' extra treasure read <see cref="ActiveModifiers"/> directly); its four fight knobs are 0 for this fight, so
    /// neither its cost nor its benefit reaches a fight whose row cannot carry one of them (a fixed Health, its own start Mana).
    /// A released schedule never reads the block (its act-wide ban stays: <see cref="ModifierCandidates"/>).
    /// </summary>
    private void NeutraliseKnobs(DungeonRunState state, DungeonRunBoss boss, DungeonRunModifier modifier, DungeonRunKnobs knobs)
    {
        if (!Neutralises(state, boss, modifier.Id)) return;
        knobs.BossHealthDelta = 0;
        knobs.BossManaDelta = 0;
        knobs.PlayerHealthDelta = 0;
        knobs.OpeningHandBonus = 0;
    }

    /// <summary>Whether the fight against <paramref name="boss"/> neutralises <paramref name="modifierId"/> (D7, stamp 4 and above only).</summary>
    internal static bool Neutralises(DungeonRunState state, DungeonRunBoss boss, string modifierId) =>
        Tuned(state) && boss?.V4 != null && boss.V4.IncompatibleModifiers.Contains(modifierId);

    /// <summary>
    /// The act-wide modifier ban a boss of the act puts on the modifier offer: its "incompatibleModifiers" on a released
    /// schedule; on a stamp-4 schedule (D7) none when the row has a v4 block, which neutralises per fight instead, and the
    /// released list when it has none (a row nobody reviewed for v4 keeps the safe act-wide ban).
    /// </summary>
    internal static IEnumerable<string> ActWideModifierBans(DungeonRunState state, DungeonRunBoss boss) =>
        boss == null ? Enumerable.Empty<string>() : Tuned(state) && boss.V4 != null ? Enumerable.Empty<string>() : boss.IncompatibleModifiers;

    /// <summary>
    /// Whether a rule is blocked by what the run itself holds (step 9): a treasure the rule names in "incompatibleTreasures",
    /// or a run-deck card with an opening flag for a rule with "noOpeningFlags".
    /// </summary>
    internal static bool RuleBlockedByRun(DungeonRunState state, DungeonRunGlobalRule rule) =>
        rule.IncompatibleTreasures.Any(id => HoldsTreasure(state, id)) || rule.NoOpeningFlags && state.Cards.Any(c => c.Opening);

    /// <summary>The run holds the treasure (passive, active or a signature: <see cref="DungeonRunState.Treasures"/>, ActiveTreasures).</summary>
    internal static bool HoldsTreasure(DungeonRunState state, string treasureId) =>
        state.Treasures.Contains(treasureId) || state.ActiveTreasures.Contains(treasureId);

    /// <summary>
    /// Step 9: a treasure the treasure drafts leave out: one an active global rule of the current act (or a run-scope one) names
    /// in "incompatibleTreasures" (ULDA_720 Battle Scars and LOOTA_800 Potion of Vitality), or one a boss still to be fought
    /// names. False on a run with no such rule or row (every run before step 9).
    /// </summary>
    internal bool RuleExcludesTreasure(DungeonRunState state, string treasureId)
    {
        if (!state.IsActRun || Data.Schedules == null) return false;
        if (state.ActiveRules.Any(r => r.Kind != null) && ActiveGlobalRules(state).Any(r => r.IncompatibleTreasures.Contains(treasureId))) return true;
        for (int floor = Math.Max(1, state.Floor); floor <= state.BossSequence.Count; floor++)
            if (Data.Bosses.FirstOrDefault(b => b.Id == state.BossSequence[floor - 1])?.IncompatibleTreasures.Contains(treasureId) ?? false) return true;
        return false;
    }

    /// <summary>
    /// Step 9: while an active global rule has "noOpeningFlags" (ULDA_721 Aleatory), the tavern sets no opening flag
    /// (DungeonRunTavernNode.Applicable: the "opening" and "discoverOpening" ops). False on every run without such a rule.
    /// </summary>
    internal bool RuleBlocksOpeningFlags(DungeonRunState state) =>
        state.IsActRun && state.ActiveRules.Any(r => r.Kind != null) && ActiveGlobalRules(state).Any(r => r.NoOpeningFlags);

    /// <summary>
    /// The twist an act rolls from its compatible <paramref name="candidates"/> (NewRun's <see cref="RollTwists"/>, draw
    /// (TwistStream, act, 1)). A released schedule picks uniformly, as phase 8 shipped; a stamp-4 schedule honours each rule's
    /// "weight" (D8), as the anomaly offer does.
    /// </summary>
    private DungeonRunGlobalRule PickTwist(DungeonRunState state, List<DungeonRunGlobalRule> candidates, int actNo)
    {
        int draw = StableSeed.Derive(state.Seed, TwistStream, actNo, 1);
        if (!Tuned(state)) return candidates[StableSeed.Index(draw, candidates.Count)];
        int roll = StableSeed.Index(draw, candidates.Sum(r => Math.Max(1, r.Weight)));
        foreach (var rule in candidates)
        {
            roll -= Math.Max(1, rule.Weight);
            if (roll < 0) return rule;
        }
        return candidates[candidates.Count - 1];
    }

    // BuildPlan's D17 check: the state's forbidden pairs go to ForbiddenPairLog; nothing is computed while no sink is set.
    private void LogForbiddenPairs(DungeonRunState state)
    {
        var log = ForbiddenPairLog;
        if (log == null || !state.IsActRun) return;
        List<string> pairs;
        try { pairs = ForbiddenPairs(state); }
        catch (Exception error) { log("forbidden-pair check failed: " + error.GetType().Name + ": " + error.Message); return; }
        foreach (string line in pairs) log("floor " + state.Floor + ": " + line);
    }

    /// <summary>
    /// C1, D17: every forbidden pair the state holds, one line each, in a fixed order; empty when there is none (every legacy
    /// run, and every act run the prevention gates kept clean). A pure predicate, order-independent (each pair is checked
    /// whichever side came first), checked by the tests at every step. Per act of the schedule, among the act's global rules
    /// (its own and the run-scope ones), its modifiers and its pre-rolled bosses:
    /// - rule x rule: either lists the other in "incompatibleRules";
    /// - rule x modifier: the rule lists it in "incompatibleModifiers";
    /// - rule x boss: the boss lists the rule in "incompatibleRules", or holds a flag the rule lists in "incompatibleFlags";
    /// - rule x fight-only hero: the rule's "incompatibleClasses" names the character of a boss's hero card or relay hero;
    /// - modifier x boss: the boss bans it act-wide (<see cref="ActWideModifierBans"/>: a released schedule, or a row with no v4 block);
    /// - rule x the run, on the act the rule is taken for: the board shrine ("noBoardShrine") and the run class ("incompatibleClasses").
    /// On the current act only (what the run holds now; the tests check every step, so each moment is seen): rule x treasure
    /// ("incompatibleTreasures"), rule x the run deck's opening flags ("noOpeningFlags"), and the current fight's boss x
    /// treasure (the row's "incompatibleTreasures").
    /// </summary>
    internal List<string> ForbiddenPairs(DungeonRunState state)
    {
        var lines = new List<string>();
        var schedule = ScheduleFor(state);
        if (schedule == null || Data.Schedules == null) return lines;
        int current = state.Finished || state.Node < 0 || state.Node >= schedule.Nodes.Count ? 0 : schedule.Nodes[state.Node].Act;
        for (int act = 1; act <= schedule.Acts.Count; act++)
        {
            var taken = state.ActiveRules.Where(r => r.Act == act || r.Kind != null && r.Act == 0).ToList();
            var rules = taken.Where(r => r.Kind != null).Select(r => Data.Schedules.Rule(r.Id)).Where(r => r != null).ToList();
            var modifiers = taken.Where(r => r.Kind == null && r.Act == act).Select(r => r.Id).ToList();
            var bosses = ActBosses(state, schedule, act);
            string at = "act " + act + ": ";
            for (int i = 0; i < rules.Count; i++)
            {
                var rule = rules[i];
                for (int j = i + 1; j < rules.Count; j++)
                    if (rule.IncompatibleRules.Contains(rules[j].Id) || rules[j].IncompatibleRules.Contains(rule.Id))
                        lines.Add(at + rule.Id + " x " + rules[j].Id + " (incompatibleRules)");
                foreach (string m in modifiers.Where(rule.IncompatibleModifiers.Contains)) lines.Add(at + rule.Id + " x " + m + " (incompatibleModifiers)");
                foreach (var boss in bosses)
                {
                    if (boss.IncompatibleRules.Contains(rule.Id)) lines.Add(at + rule.Id + " x " + boss.Id + " (the boss's incompatibleRules)");
                    foreach (string flag in boss.Flags.Where(rule.IncompatibleFlags.Contains)) lines.Add(at + rule.Id + " x " + boss.Id + " (flag " + flag + ")");
                    foreach (string hero in FightHeroes(boss).Where(h => rule.IncompatibleClasses.Any(c => SameCharacter(c, h))))
                        lines.Add(at + rule.Id + " x " + boss.Id + " (fight-only hero " + hero + ", incompatibleClasses)");
                }
                // The run itself: reported once, on the act the rule is taken for (a run-scope rule: act 1).
                if (rule.RunScope && act != 1) continue;
                if (rule.NoBoardShrine && state.HasBoardShrine) lines.Add(at + rule.Id + " x board shrine " + state.BoardShrine + " (noBoardShrine)");
                if (rule.IncompatibleClasses.Contains(state.Class)) lines.Add(at + rule.Id + " x run class " + state.Class + " (incompatibleClasses)");
            }
            foreach (var boss in bosses)
                foreach (string m in modifiers.Where(ActWideModifierBans(state, boss).Contains))
                    lines.Add(at + m + " x " + boss.Id + " (the boss's incompatibleModifiers)");
            if (act != current) continue;
            foreach (var rule in rules)
            {
                foreach (string t in rule.IncompatibleTreasures.Where(t => HoldsTreasure(state, t))) lines.Add(at + rule.Id + " x treasure " + t + " (incompatibleTreasures)");
                if (rule.NoOpeningFlags && state.Cards.Any(c => c.Opening)) lines.Add(at + rule.Id + " x an opening-flag card of the run deck (noOpeningFlags)");
            }
            if (state.Floor >= 1 && state.Floor <= state.BossSequence.Count)
            {
                var fight = Data.Bosses.FirstOrDefault(b => b.Id == state.BossSequence[state.Floor - 1]);
                foreach (string t in fight?.IncompatibleTreasures.Where(t => HoldsTreasure(state, t)) ?? Enumerable.Empty<string>())
                    lines.Add(at + fight.Id + " x treasure " + t + " (the boss's incompatibleTreasures)");
            }
        }
        return lines;
    }

    // The fight-only heroes a boss row gives the run player: Hagatha's hero cards and a finale's relay heroes.
    private static IEnumerable<string> FightHeroes(DungeonRunBoss boss) => boss.HeroCards.Select(h => h.Hero).Concat(boss.Relay.Select(h => h.Hero));

    // A run class id (a hero row's id is its hero card) and a fight-only hero card name the same character when they are equal,
    // or when both end in a letter followed by digits and differ only in those digits (GILA_600h, Darius Crowley's row, and
    // GILA_600h2, his card in Hagatha's fight). A class name (MAGE) or an id ending in "_<digits>" only matches itself.
    private static bool SameCharacter(string classId, string heroCard) =>
        classId != null && heroCard != null && (classId == heroCard || Stem(classId) != null && Stem(classId) == Stem(heroCard));

    private static string Stem(string id)
    {
        string trimmed = id.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
        return trimmed.Length > 0 && char.IsLetter(trimmed[trimmed.Length - 1]) ? trimmed : null;
    }
}
