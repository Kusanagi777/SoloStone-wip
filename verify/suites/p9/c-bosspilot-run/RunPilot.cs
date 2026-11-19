using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SabberStoneCore.CardSets.DungeonRun;
using SabberStoneCore.Model;

// Step 9 lane M (handoff/phase9/plan.md D3): the run player's fair pilot, the step-9 shims and the run walk of the run harness.
// None of it lives in plugin/: the harness builds against the tree it measures.

/// <summary>
/// Step-9 fields read by reflection, so the harness also builds and measures on a tree before step 9 (the f62275b export of plan
/// stage 1a): there every plan has the 100-turn cap, no form ratchet, and no fight ends in an escape or a restart.
/// </summary>
internal static class Step9
{
    private static readonly PropertyInfo turnCap = typeof(RunMatchPlan).GetProperty("TurnCap");
    private static readonly PropertyInfo formRatchet = typeof(RunMatchPlan).GetProperty("FormRatchet");
    private static readonly PropertyInfo restarts = typeof(RunMatchPlan).GetProperty("Restarts");
    private static readonly PropertyInfo escapeAllowed = typeof(RunMatchPlan).GetProperty("EscapeAllowed");

    /// <summary>The fight's turn cap: the plan's TurnCap, or 100 (NativeMatch.DungeonRunTurnCap) on a tree without it.</summary>
    internal static int TurnCap(RunMatchPlan plan) => turnCap == null ? 100 : (int)turnCap.GetValue(plan);
    internal static bool FormRatchet(RunMatchPlan plan) => formRatchet != null && (bool)formRatchet.GetValue(plan);
    internal static IList<string> Restarts(RunMatchPlan plan) => restarts == null ? new List<string>() : (IList<string>)restarts.GetValue(plan) ?? new List<string>();
    /// <summary>
    /// Closes the fight's escape: RunMatchPlan.EscapeAllowed false, as on a fight that allows none (the engine then makes ULDA_111
    /// Hearthstone unplayable). Returns whether the plan had allowed it; false on a tree without it.
    /// </summary>
    internal static bool CloseEscape(RunMatchPlan plan)
    {
        if (escapeAllowed == null || !(bool)escapeAllowed.GetValue(plan)) return false;
        escapeAllowed.SetValue(plan, false);
        return true;
    }
    internal static bool Flag(object o, string name) => o?.GetType().GetProperty(name)?.GetValue(o) is bool b && b;
    internal static string Text(object o, string name) => o?.GetType().GetProperty(name)?.GetValue(o) as string;
    internal static int Int(object o, string name)
    {
        var t = o?.GetType();
        object v = t?.GetField(name)?.GetValue(o) ?? t?.GetProperty(name)?.GetValue(o);
        return v is int i ? i : 0;
    }
    /// <summary>A treasure row's run-v1.json "shared" (step 9 D6); false on a tree without it.</summary>
    internal static bool Shared(DungeonRunTreasure t) => typeof(DungeonRunTreasure).GetField("Shared", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(t) is bool b && b;
    /// <summary>ULDA_111 Hearthstone's deck card id, or null on a tree without it.</summary>
    internal static string HearthstoneShell()
    {
        try { return DungeonRunContent.Get("ULDA_111")?.ShellId; } catch { return null; }
    }
}

/// <summary>
/// The run player's fair pilot (plan D3): the same AI as the boss's, built per fight from the run deck. Its deck plan is an
/// AutoDeckPlan.Derive of the cards the player holds (rebuilt when a hero switch or the relay changes its hero), its personality
/// never concedes, and its Secret prior is the boss's own Secrets (the boss's deck and forms' decks, weight 1 + copies): public
/// knowledge of the encounter, as a player who has met the boss knows it. Null when the boss lists no Secret (the open pool).
/// </summary>
internal static class RunPilot
{
    private const DeckArchetype Archetype = DeckArchetype.Midrange;

    /// <summary>
    /// The pilot of a fight's run deck (<paramref name="ids"/> = the plan's PlayerDeck): a deck list, so it passes the deck rules
    /// (LocalDeck: a Fabled bundle whole).
    /// </summary>
    internal static (LocalDeck Deck, DeckPlan Plan, NpcProfile Profile) Build(string tag, Card hero, IReadOnlyList<string> ids, Budget budget)
    {
        var deck = new LocalDeck("dungeon-player:" + tag, "Run player", hero.Id, "Run player", "Dungeon Run player", NpcDeckLibrary.StyleFor(Archetype),
            new string[0], ids.ToArray(), "dungeon-run");
        var (plan, profile) = Replan(tag, hero, ids, budget);
        return (deck, plan, profile);
    }

    /// <summary>
    /// The plan and profile of the cards the player holds mid-game (deck and hand, after a hero switch or the relay). Not a deck
    /// list, so no deck rule applies: a card the game gave the player (the boss's TIME_209 dealt to the player by ULDA_721
    /// Aleatory's deck mix, its TIME_209t left with the boss) splits a Fabled bundle legally.
    /// </summary>
    internal static (DeckPlan Plan, NpcProfile Profile) Replan(string tag, Card hero, IReadOnlyList<string> ids, Budget budget)
    {
        const DeckArchetype archetype = Archetype;
        string deckId = "dungeon-player:" + tag;
        var cards = ids.ToArray();
        var entry = new NpcDeckEntry(deckId, 1, "Run player", hero.Class.ToString(), hero.Id, "dungeon-run", "dungeon-run", null,
            new NpcDeckTags(archetype, 3, 1, 0, 3, LadderBand.Bronze, LadderBand.Legend), null,
            cards.GroupBy(id => id, StringComparer.Ordinal).Select(g => new NpcDeckCard(g.Key, g.Count())), new NpcDeckSideboard[0]);
        var plan = AutoDeckPlan.Derive(entry);
        // As the boss's personality (DungeonRunBossPilot): steady pacing, no emotes, never concedes.
        var personality = new Personality(.6, 1.2, .25, .5, new EmoteProfile(0, 30), .5, 0, int.MaxValue);
        var profile = new NpcProfile("dungeon.player." + tag, 1, "Run player", deckId, null, 0, personality, NpcCatalog.All[0].Budget, plan.Id)
            .WithBudget(budget);
        return (plan, profile);
    }

    internal static IReadOnlyDictionary<string, double> BossSecretPrior(RunMatchPlan plan)
    {
        var copies = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var decks = new List<IEnumerable<Card>> { plan.BossDeck ?? new List<Card>() };
        if (plan.BossForms != null) decks.AddRange(plan.BossForms.Where(f => f?.Deck != null).Select(f => (IEnumerable<Card>)f.Deck));
        foreach (var card in decks.SelectMany(d => d))
            if (card != null && card.IsSecret) { copies.TryGetValue(card.Id, out int n); copies[card.Id] = n + 1; }
        if (copies.Count == 0) return null;
        return new System.Collections.ObjectModel.ReadOnlyDictionary<string, double>(copies.ToDictionary(p => p.Key, p => 1.0 + p.Value, StringComparer.Ordinal));
    }
}

/// <summary>
/// The harness's run walk: every non-fight node is played as a player would, with a seeded pick (StableSeed of the run seed, the
/// node and a salt, so it is stable across runs and reloads): a treasure, shrine-upgrade or teammate draft takes one offer; a
/// bundle draft takes one bundle and then one of its cards per take; a modifier or anomaly node takes one offer ("none"
/// included where offered); a tavern buys every offer it can afford in offer order, a choice taking a seeded candidate.
/// A fight before the target floor is won without a game (as the encounter harness walks).
/// </summary>
internal static class RunWalk
{
    internal static int Pick(DungeonRunState s, int salt, int count) =>
        count <= 1 ? 0 : (int)((uint)StableSeed.Derive(s.Seed, 9_000_001 + salt, s.Floor, s.Node, s.Matches.Count) % (uint)count);

    /// <summary>Plays one non-fight node; false when the run is at PreMatch, Victory or Defeat.</summary>
    internal static bool Step(DungeonRun run, DungeonRunState s)
    {
        switch (s.Phase)
        {
            case DungeonRunPhase.TreasureDraft: run.PickTreasure(s, s.OpenDraft.Offered[Pick(s, 1, s.OpenDraft.Offered.Count)]); return true;
            case DungeonRunPhase.Modifier: run.PickModifier(s, s.ModifierOffer.Offered[Pick(s, 2, s.ModifierOffer.Offered.Count)]); return true;
            case DungeonRunPhase.Tavern:
                int k = 0;
                foreach (string offer in s.Tavern.Offers.ToList())
                {
                    var bought = DungeonRunTavernNode.Buy(run, s, offer);
                    while (bought.Ok && s.Tavern.Pending != null)
                        bought = DungeonRunTavernNode.Choose(run, s, s.Tavern.Pending.Candidates[Pick(s, 10 + k++, s.Tavern.Pending.Candidates.Count)]);
                    if (s.Tavern.Pending != null) DungeonRunTavernNode.Back(s);
                }
                run.LeaveTavern(s);
                return true;
            case DungeonRunPhase.BundleDraft:
                if (s.OpenDraft.Takes == 0) { run.PickBundle(s, s.OpenDraft.Offered[Pick(s, 3, s.OpenDraft.Offered.Count)]); return true; }
                string bundle = s.OpenDraft.Choosing ?? s.OpenDraft.Offered[Pick(s, 4, s.OpenDraft.Offered.Count)];
                var left = run.OfferCards(s.OpenDraft, bundle);
                foreach (string kept in s.OpenDraft.Chosen) left.Remove(kept);
                run.PickDraftCard(s, bundle, left[Pick(s, 5 + s.OpenDraft.Chosen.Count, left.Count)]);
                return true;
            default: return false;
        }
    }

    /// <summary>Walks to the next fight (PreMatch) or the run's end.</summary>
    internal static void ToFight(DungeonRun run, DungeonRunState s)
    {
        for (int guard = 0; Step(run, s); guard++) if (guard > 10000) throw new Exception("the walk does not reach a fight");
    }

    /// <summary>Walks to <paramref name="floor"/>'s PreMatch, winning every earlier fight without a game.</summary>
    internal static void ToFloor(DungeonRun run, DungeonRunState s, DungeonRunProfile profile, int floor)
    {
        while (true)
        {
            ToFight(run, s);
            if (s.Phase != DungeonRunPhase.PreMatch) throw new Exception("the walk ended at " + s.Phase + " before floor " + floor);
            if (s.Floor == floor) return;
            if (s.Floor > floor) throw new Exception("the walk passed floor " + floor);
            run.BeginMatch(s); run.RecordOutcome(s, profile, DungeonRunOutcome.Win);
        }
    }
}
