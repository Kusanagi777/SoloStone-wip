using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;

// Build Deck rules (pure): which cards a run deck may hold and when a deck is legal. No profile, catalog or client types:
// ownership and copy grouping come in as delegates, so the run start can re-check a saved deck without ownership.
internal static class DungeonRunBuild
{
    /// <summary>
    /// Why a card may not be in a deck of a run with <paramref name="classSet"/>, or null when it may: "missing" (not in the card
    /// data), "not-collectible" (a token, a treasure shell or any other card a collection cannot hold), "hero" (a hero card),
    /// "fabled" (a Fabled primary or companion), "rune-class" (a rune cost outside a Death Knight run), else the first
    /// <see cref="DungeonRunValidator.PoolCardReason"/> code (catalog-run checks).
    /// </summary>
    internal static string CardReason(string id, IReadOnlyList<string> classSet, OfflinePoolPolicy policy)
    {
        // Fully qualified: the client's Assembly-CSharp has its own global Card, which wins in the plugin build.
        SabberStoneCore.Model.Card card;
        try { card = Cards.FromId(id); } catch { card = null; }
        if (card == null) return "missing";
        if (!card.Collectible) return "not-collectible";
        if (card.Type == CardType.HERO) return "hero";
        if (SabberStoneCore.Actions.FabledRules.IsFabled(id) || SabberStoneCore.Actions.FabledRules.IsCompanion(id)) return "fabled";
        if (DungeonRunValidator.HasRuneCost(id) && !classSet.Contains("DEATHKNIGHT")) return "rune-class";
        return DungeonRunValidator.PoolCardReason(id, classSet, false, policy, catalogRun: true);
    }

    /// <summary>
    /// Every reason the deck is illegal (empty when legal): "size n of size", "copies group" (more than 2 of a copy group),
    /// "legendary group" (more than 1 of a legendary's group), "owned id" (more copies than owned), "runes" (a Death Knight
    /// run needing more than 3 runes) and "code id" (a card <see cref="CardReason"/> refuses). <paramref name="owned"/> null
    /// skips ownership; <paramref name="copyGroup"/> null groups by id.
    /// </summary>
    internal static IReadOnlyList<string> DeckReasons(IReadOnlyList<string> cards, IReadOnlyList<string> classSet, int size,
        Func<string, int> owned, Func<string, string> copyGroup, OfflinePoolPolicy policy)
    {
        var reasons = new List<string>();
        if (cards.Count != size) reasons.Add($"size {cards.Count} of {size}");
        Func<string, string> group = copyGroup ?? (id => id);

        foreach (var g in cards.GroupBy(group, StringComparer.Ordinal))
        {
            int n = g.Count();
            if (n > 2) reasons.Add("copies " + g.Key);
            if (n > 1 && g.Any(IsLegendary)) reasons.Add("legendary " + g.Key);
        }
        if (owned != null)
            foreach (var g in cards.GroupBy(id => id, StringComparer.Ordinal))
                if (g.Count() > owned(g.Key)) reasons.Add("owned " + g.Key);

        foreach (var id in cards.Distinct(StringComparer.Ordinal))
        {
            string reason = CardReason(id, classSet, policy);
            if (reason != null) reasons.Add(reason + " " + id);
        }

        if (classSet.Contains("DEATHKNIGHT"))
        {
            try
            {
                var needs = SabberStoneCore.Actions.RuneRules.FromCards(cards.Select(Cards.FromId));
                if (!SabberStoneCore.Actions.RuneRules.IsLegalForClass(needs, CardClass.DEATHKNIGHT)) reasons.Add("runes");
            }
            catch (Exception) { /* an unknown card is already reported as "missing <id>" */ }
        }
        return reasons;
    }

    /// <summary>
    /// The first reason adding <paramref name="id"/> to <paramref name="cards"/> breaks the deck (a deck already at
    /// <paramref name="size"/> is "full"; the codes of <see cref="DeckReasons"/> otherwise), or null when the add is legal.
    /// </summary>
    internal static string AddReason(IReadOnlyList<string> cards, string id, IReadOnlyList<string> classSet, int size,
        Func<string, int> owned, Func<string, string> copyGroup, OfflinePoolPolicy policy)
    {
        if (cards.Count >= size) return "size";
        var next = cards.Concat(new[] { id }).ToList();
        // Only reasons the add brings count (the deck so far may already hold others); size is decided above.
        var before = new HashSet<string>(DeckReasons(cards, classSet, cards.Count, owned, copyGroup, policy), StringComparer.Ordinal);
        return DeckReasons(next, classSet, next.Count, owned, copyGroup, policy).FirstOrDefault(r => !before.Contains(r));
    }

    private static bool IsLegendary(string id)
    {
        try { return Cards.FromId(id)?.Rarity == Rarity.LEGENDARY; } catch { return false; }
    }
}
