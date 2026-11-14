using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.CardSets.DungeonRun;

/// <summary>
/// Builds a Dungeon Run boss for the existing NPC AI without touching the NPC world: a namespaced LocalDeck, an
/// NpcProfile and a DeckPlan that are passed straight to NativeMatch (no LocalDecks/NpcCatalog/NpcWorldHooks lookup),
/// so a boss can never be mistaken for a ladder NPC, its memory, skin, emotes or concession personality.
/// </summary>
internal sealed class DungeonRunBossPilot
{
    internal const string DeckPrefix = "dungeon-boss:";
    internal const string ProfilePrefix = "dungeon.";

    internal LocalDeck Deck { get; private set; }
    internal NpcProfile Profile { get; private set; }
    internal DeckPlan Plan { get; private set; }
    /// <summary>
    /// The run player's Secret pool for the boss's fair search, from the loaded, validated run data and the run's classes: a
    /// legacy run's closed prior (<see cref="DungeonRunAdmission.SecretPrior(DungeonRunData, SabberStoneCore.Enums.CardClass)"/>),
    /// or a catalog run's widened one (<see cref="DungeonRunAdmission.CatalogSecretPrior"/>, RunMatchPlan.PlayerGeneratedSecrets).
    /// Null when no data is given, when the pool holds no Secret, or for a migrated version-1 run (Build's legacyDeck), whose
    /// deck comes from no listed source: the boss then keeps the open class pool.
    /// </summary>
    internal IReadOnlyDictionary<string, double> SecretPrior { get; private set; }

    /// <param name="legacyDeck">The run is a migrated version-1 run (DungeonRunState.StarterDeck is null).</param>
    /// <param name="runClasses">The run's classes (DungeonRun.RunClasses: a dual-class row's two); null = the player hero's class.</param>
    internal static DungeonRunBossPilot Build(DungeonRunBoss boss, RunMatchPlan plan, DungeonRunData data = null, bool legacyDeck = false,
        IReadOnlyList<string> runClasses = null)
    {
        if (boss == null) throw new ArgumentNullException(nameof(boss));
        if (plan?.BossDeck == null || plan.BossHero == null) throw new ArgumentException("Run plan has no boss.");
        DeckArchetype archetype = ParseArchetype(boss.Archetype);
        string deckId = DeckPrefix + boss.Id;
        var ids = plan.BossDeck.Select(card => card.Id).ToArray();

        var deck = new LocalDeck(deckId, boss.Name, plan.BossHero.Id, boss.Name, "Dungeon Run boss",
            NpcDeckLibrary.StyleFor(archetype), new string[0], ids, "dungeon-run");
        var boards = plan.BossSideboards ?? new Dictionary<string, string[]>();
        if (boards.Count > 0)
            deck.WithSideboards(boards.Select(row => new LocalSideboard { Source = row.Key, Cards = row.Value.ToList() }), complete: true);

        var entry = new NpcDeckEntry(deckId, 1, boss.Name, plan.BossHero.Class.ToString(), plan.BossHero.Id, "dungeon-run", "dungeon-run",
            null, new NpcDeckTags(archetype, 3, 1, 0, 3, LadderBand.Bronze, LadderBand.Legend), null,
            ids.GroupBy(id => id, StringComparer.Ordinal).Select(g => new NpcDeckCard(g.Key, g.Count())),
            boards.Select(row => new NpcDeckSideboard(row.Key, row.Value.GroupBy(id => id, StringComparer.Ordinal).Select(g => new NpcDeckCard(g.Key, g.Count())))));
        DeckPlan derived = AutoDeckPlan.Derive(entry);
        // A library deck with a hand-written plan (the golden decks) keeps its plan hints over the derived base.
        DeckPlan handWritten = DeckPlans.All.FirstOrDefault(p => p.DeckId == boss.Deck);
        DeckPlan deckPlan = handWritten == null ? derived : AutoDeckPlan.Overlay(derived, handWritten);

        // Steady pacing, no emotes, never concedes: a boss fight always ends on the board (design R10).
        var personality = new Personality(.6, 1.2, .25, .5, new EmoteProfile(0, 30), .5, 0, int.MaxValue);
        var profile = new NpcProfile(ProfilePrefix + boss.Id, 1, boss.Name, deckId, null, 0, personality,
            NpcCatalog.All[0].Budget, deckPlan.Id);
        var classes = runClasses ?? (plan.PlayerHero == null ? null : new[] { plan.PlayerHero.Class.ToString() });
        var prior = data == null || classes == null || legacyDeck ? null : DungeonRunAdmission.SecretPrior(data, classes, plan.PlayerGeneratedSecrets);
        return new DungeonRunBossPilot { Deck = deck, Profile = profile, Plan = deckPlan, SecretPrior = prior };
    }

    internal static DeckArchetype ParseArchetype(string value)
    {
        switch (value)
        {
            case "aggro": return DeckArchetype.Aggro;
            case "tempo": return DeckArchetype.Tempo;
            case "midrange": return DeckArchetype.Midrange;
            case "control": return DeckArchetype.Control;
            case "combo": return DeckArchetype.Combo;
            default: throw new InvalidOperationException("Boss archetype missing or unknown: " + (value ?? "(none)"));
        }
    }
}
