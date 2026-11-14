using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using EngineCard = SabberStoneCore.Model.Card;

/// <summary>
/// Bob's tavern nodes of an act run (phases 4 and 5). <see cref="Open"/> fills the saved <see cref="DungeonRunTavern"/>: its
/// gold (the act's tavern gold plus the active modifiers' tavernGoldDelta), the friendly board (deck minions), Bob's board
/// and the offered options (tavern-v1.json). <see cref="Buy"/> runs an option at once or asks for a target
/// (<see cref="DungeonRunTavern.Pending"/>); <see cref="Choose"/> resolves the target and <see cref="Back"/> cancels it
/// without charging. Every deck change goes through <see cref="DungeonRunDeck"/>, so it is validated and all or none.
/// Every random draw keys on <see cref="DungeonRun.TavernStream"/>, the node and <see cref="DungeonRunTavern.Step"/>, so a
/// save and resume continues exactly; an option's random choices are drawn once per visit (<see cref="DungeonRunTavern.Draws"/>),
/// so Back and Buy again never rerolls them. The run state's class shares the name DungeonRunTavern (it is what XmlSerializer
/// writes), so the logic lives here.
/// </summary>
internal static class DungeonRunTavernNode
{
    // Error codes of a refused tavern action (deck errors pass through as DungeonRunDeck.Err* codes).
    internal const string ErrNoTavern = "no-tavern", ErrNotOffered = "not-offered", ErrBought = "bought", ErrGold = "gold",
        ErrPending = "pending", ErrNoPending = "no-pending", ErrNotCandidate = "not-candidate", ErrNothing = "nothing";

    /// <summary>Discover options show this many cards.</summary>
    internal const int DiscoverChoices = 3;

    /// <summary>Opens the run's current tavern node and sets the phase to Tavern.</summary>
    internal static void Open(DungeonRun run, DungeonRunState state)
    {
        if (run == null) throw new ArgumentNullException(nameof(run));
        if (!state.IsActRun) throw new InvalidOperationException("Only an act run has taverns.");
        var tavern = new DungeonRunTavern { Node = state.Node, Gold = run.TavernGold(state) };
        state.Tavern = tavern;
        state.Phase = DungeonRunPhase.Tavern;
        var table = run.Data.Tavern;
        if (table == null) return;
        tavern.Friendly = Sample(state, FriendlyCandidates(state).Select(c => c.Uid).ToList(), table.FriendlyBoard);
        tavern.Bob = Sample(state, BobPool(run, state), table.BobBoard);
        // The offers: drawn by weight without replacement among the options this deck can use now.
        var options = table.Options.Where(o => o.Weight > 0 && o.Id != DungeonRunTavernTable.HouseSpecial && Applicable(run, state, o))
            .OrderBy(o => o.Id, StringComparer.Ordinal).ToList();
        int offers = run.TavernOffers(state);
        while (tavern.Offers.Count < offers && options.Count > 0)
        {
            int roll = Roll(state, options.Sum(o => o.Weight));
            var picked = options[options.Count - 1];
            foreach (var o in options) { roll -= o.Weight; if (roll < 0) { picked = o; break; } }
            tavern.Offers.Add(picked.Id);
            options.Remove(picked);
        }
    }

    /// <summary>
    /// Buys an offered option: it must be on offer, not bought this visit and affordable. An option that needs a target opens
    /// <see cref="DungeonRunTavern.Pending"/> (no gold is spent until <see cref="Choose"/>); any other runs at once and is
    /// charged. On failure nothing changes.
    /// </summary>
    internal static DeckEditResult Buy(DungeonRun run, DungeonRunState state, string optionId)
    {
        var tavern = Open(state, out var error);
        if (error != null) return error;
        if (tavern.Pending != null) return Fail(ErrPending, "finish or cancel the pending purchase first");
        var option = run.Data.Tavern?.Option(optionId);
        if (option == null || !tavern.Offers.Contains(optionId)) return Fail(ErrNotOffered, (optionId ?? "(none)") + " is not on offer");
        if (tavern.Bought.Any(b => b.Option == optionId)) return Fail(ErrBought, "already bought this visit");
        if (option.Cost > tavern.Gold) return Fail(ErrGold, "not enough gold");
        // An earlier purchase this visit can leave an offer with nothing to do (the deck full, the friendly board emptied,
        // three opening flags): it is refused here, before any choice opens, and the page shows it disabled.
        if (!Applicable(run, state, option)) return Fail(ErrNothing, optionId + " can do nothing for this deck now");
        var op = Behaviour(run, option);
        int step = tavern.Step;
        var friendly = LiveFriendly(state);
        List<string> candidates = null;
        DeckEditResult result = null;
        var targets = new List<string>();
        switch (op.Op)
        {
            case "recruit":
                candidates = tavern.Bob.Distinct().ToList();
                break;
            case "remove": case "buff": case "replace": case "removeBuffNeighbours":
                candidates = friendly.Select(Text).ToList();
                break;
            case "copy":
                candidates = friendly.Where(uid => DungeonRunDeck.CopyError(run, state, uid) == null).Select(Text).ToList();
                break;
            case "opening":
                candidates = friendly.Where(uid => !DungeonRunDeck.Find(state, uid).Opening).Select(Text).ToList();
                break;
            // The random choices are drawn once per visit (DungeonRunTavern.Draws): Back and Buy again shows them again.
            case "discover": case "discoverOpening": case "discoverBuff":
            {
                var pool = new HashSet<string>(DiscoverPool(run, state, op.Pool), StringComparer.Ordinal);
                candidates = Drawn(tavern, optionId, 0, () => Sample(state, DiscoverPool(run, state, op.Pool), DiscoverChoices), pool.Contains);
                break;
            }
            case "removeFromSample": case "discountFromSample":
            {
                var sample = new HashSet<string>(SampleCards(state, op.Sample).Select(c => Text(c.Uid)), StringComparer.Ordinal);
                candidates = Drawn(tavern, optionId, 0, () => Sample(state, SampleCards(state, op.Sample).Select(c => c.Uid).ToList(), op.N).Select(Text).ToList(), sample.Contains);
                break;
            }
            case "rerollTribe":
            {
                var tribes = new HashSet<string>(TribeCandidates(run, state), StringComparer.Ordinal);
                candidates = Drawn(tavern, optionId, 0, () => Sample(state, TribeCandidates(run, state), DiscoverChoices), tribes.Contains);
                break;
            }
            case "setcost":
            {
                var pool = state.Cards.Where(c => c.Origin != DungeonRunDeck.OriginTreasure && DungeonRunDeck.EffectiveCost(c) > 0).Select(c => c.Uid).ToList();
                if (pool.Count == 0) pool = state.Cards.Where(c => c.Origin != DungeonRunDeck.OriginTreasure).Select(c => c.Uid).ToList();
                if (pool.Count == 0) { result = Fail(ErrNothing, "no card to change"); break; }
                int uid = pool[Roll(state, pool.Count)];
                result = DungeonRunDeck.Discount(run, state, uid, op.Ench);
                targets.Add(Text(uid));
                break;
            }
            case "health":
                state.HealthBonus += op.Amount;
                result = DeckEditResult.Success();
                break;
            case "recruitAll":
                if (tavern.Bob.Count == 0) { result = Fail(ErrNothing, "Bob's board is empty"); break; }
                result = DungeonRunDeck.TryAddAll(run, state, tavern.Bob, DungeonRunDeck.OriginTavern, new[] { op.Ench });
                if (result.Ok) { targets.AddRange(tavern.Bob); tavern.Bob = new List<string>(); }
                break;
            case "removeAllFriendly":
                if (friendly.Count == 0) { result = Fail(ErrNothing, "no friendly minion"); break; }
                result = DungeonRunDeck.RemoveWhere(run, state, c => friendly.Contains(c.Uid));
                if (result.Ok) { targets.AddRange(friendly.Select(Text)); tavern.Friendly = new List<int>(); }
                break;
            case "reroll":
            {
                var pool = BobPool(run, state);
                var fresh = pool.Where(id => !tavern.Bob.Contains(id)).ToList();
                var bob = Sample(state, fresh.Count >= (run.Data.Tavern?.BobBoard ?? 4) ? fresh : pool, run.Data.Tavern?.BobBoard ?? 4);
                if (bob.Count == 0) { result = Fail(ErrNothing, "Bob has no minions to offer"); break; }
                tavern.Bob = bob;
                tavern.Rerolled = true;
                targets.AddRange(bob);
                result = DeckEditResult.Success();
                break;
            }
            case "removeWhereCostAtMost":
            {
                int amount = op.Amount;
                result = DungeonRunDeck.RemoveWhere(run, state, c => DungeonRunDeck.EffectiveCost(c) <= amount);
                if (result.Ok && result.Uids.Count == 0) result = Fail(ErrNothing, "no card costs (" + amount + ") or less");
                if (result.Ok) { targets.AddRange(result.Uids.Select(Text)); tavern.Friendly = tavern.Friendly.Where(u => !result.Uids.Contains(u)).ToList(); }
                break;
            }
            default:
                result = Fail(DungeonRunDeck.ErrUnknownOp, "unknown tavern op " + op.Op);
                break;
        }
        if (candidates != null)
        {
            if (candidates.Count == 0) { tavern.Step = step; return Fail(ErrNothing, "nothing to choose"); }
            tavern.Pending = new DungeonRunTavernPending { Option = optionId, Candidates = candidates };
            return DeckEditResult.Success();
        }
        if (!result.Ok) { tavern.Step = step; return result; }
        Charge(tavern, option, targets);
        return result;
    }

    /// <summary>
    /// Resolves the pending purchase with one of its candidates: the change is made, the gold is spent and the purchase is
    /// recorded. Hiring Replacements asks twice: the friendly minion, then the discovered replacement.
    /// </summary>
    internal static DeckEditResult Choose(DungeonRun run, DungeonRunState state, string candidate)
    {
        var tavern = Open(state, out var error);
        if (error != null) return error;
        var pending = tavern.Pending;
        if (pending == null) return Fail(ErrNoPending, "nothing is waiting for a choice");
        if (candidate == null || !pending.Candidates.Contains(candidate)) return Fail(ErrNotCandidate, (candidate ?? "(none)") + " is not a choice");
        var option = run.Data.Tavern.Option(pending.Option);
        var op = Behaviour(run, option);
        int step = tavern.Step;
        DeckEditResult result;
        var targets = new List<string> { candidate };
        int uid = int.TryParse(candidate, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ? parsed : 0;
        switch (op.Op)
        {
            case "recruit":
                result = DungeonRunDeck.TryAdd(run, state, candidate, DungeonRunDeck.OriginTavern);
                if (result.Ok) tavern.Bob.Remove(candidate);
                break;
            case "remove":
            case "removeFromSample":
                result = DungeonRunDeck.Remove(run, state, uid);
                if (result.Ok) tavern.Friendly.Remove(uid);
                break;
            case "opening":
                result = DungeonRunDeck.SetOpening(run, state, uid, true);
                break;
            case "buff":
                result = DungeonRunDeck.Buff(run, state, uid, op.Ench);
                break;
            case "discountFromSample":
                result = DungeonRunDeck.Discount(run, state, uid, op.Ench);
                break;
            case "copy":
                result = DungeonRunDeck.Copy(run, state, uid, op.N, op.KeepMods);
                break;
            case "discover":
                result = DungeonRunDeck.TryAdd(run, state, candidate, DungeonRunDeck.OriginTavern);
                break;
            case "discoverOpening":
            {
                var saved = Save(state);
                result = DungeonRunDeck.TryAdd(run, state, candidate, DungeonRunDeck.OriginTavern);
                if (result.Ok)
                {
                    var flagged = DungeonRunDeck.SetOpening(run, state, result.Uids[0], true);
                    if (!flagged.Ok) { Restore(state, saved); result = flagged; }
                }
                break;
            }
            case "discoverBuff":
                // Friendly Smith: a weapon of any class (class waived; runes still apply), with its +2/+2.
                result = DungeonRunDeck.TryAdd(run, state, candidate, DungeonRunDeck.OriginTavern, new[] { op.Ench }, classWaived: true);
                break;
            case "replace":
                if (pending.Target == 0)
                {
                    // Step one: the friendly minion; step two discovers its replacement (drawn once per visit, whatever the
                    // target: Back and another target shows the same replacements).
                    var pool = new HashSet<string>(DiscoverPool(run, state, op.Pool), StringComparer.Ordinal);
                    var choices = Drawn(tavern, pending.Option, 1, () => Sample(state, DiscoverPool(run, state, op.Pool), DiscoverChoices), pool.Contains);
                    if (choices.Count == 0) { tavern.Step = step; return Fail(ErrNothing, "no replacement to discover"); }
                    pending.Target = uid;
                    pending.Candidates = choices;
                    return DeckEditResult.Success();
                }
                result = DungeonRunDeck.Replace(run, state, pending.Target, candidate, DungeonRunDeck.OriginTavern);
                if (result.Ok) targets.Insert(0, Text(pending.Target));
                break;
            case "removeBuffNeighbours":
            {
                // Work, Work!: the neighbours on the friendly board (one at an edge), found before the removal.
                int at = tavern.Friendly.IndexOf(uid);
                var neighbours = new[] { at - 1, at + 1 }.Where(i => at >= 0 && i >= 0 && i < tavern.Friendly.Count)
                    .Select(i => tavern.Friendly[i]).Where(n => DungeonRunDeck.Find(state, n) != null).ToList();
                var saved = Save(state);
                result = DungeonRunDeck.Remove(run, state, uid);
                if (!result.Ok) break;
                foreach (int n in neighbours)
                {
                    var buffed = DungeonRunDeck.Buff(run, state, n, op.Ench);
                    if (!buffed.Ok) { Restore(state, saved); result = buffed; break; }
                    targets.Add(Text(n));
                }
                if (result.Ok) tavern.Friendly.Remove(uid);
                break;
            }
            case "rerollTribe":
            {
                var race = DungeonRunDeck.BaseCard(candidate)?.GetRawRace() ?? Race.INVALID;
                var bob = Sample(state, BobPool(run, state).Where(id => DungeonRunDeck.BaseCard(id)?.IsRace(race) == true).ToList(), run.Data.Tavern.BobBoard);
                if (bob.Count == 0) { tavern.Step = step; return Fail(ErrNothing, "Bob has no minions of that type"); }
                tavern.Bob = bob;
                tavern.Rerolled = true;
                targets.AddRange(bob);
                result = DeckEditResult.Success();
                break;
            }
            default:
                result = Fail(DungeonRunDeck.ErrUnknownOp, "unknown tavern op " + op.Op);
                break;
        }
        if (!result.Ok) { tavern.Step = step; return result; }
        tavern.Pending = null;
        Charge(tavern, option, targets);
        return result;
    }

    /// <summary>
    /// Cancels the pending purchase. Nothing was charged, so nothing is refunded; its random choices stay drawn for the visit
    /// (<see cref="DungeonRunTavern.Draws"/>), so cancelling never rerolls them.
    /// </summary>
    internal static DeckEditResult Back(DungeonRunState state)
    {
        var tavern = Open(state, out var error);
        if (error != null) return error;
        if (tavern.Pending == null) return Fail(ErrNoPending, "nothing is waiting for a choice");
        tavern.Pending = null;
        return DeckEditResult.Success();
    }

    // ------------------------------------------------------------------ pools and applicability

    /// <summary>
    /// Whether an option can do something for this deck now (offers never include one that cannot): Kindle and The Upper Hand
    /// need a spell, removals keep deckMin, adds keep deckMax, opening flags stay at most three, discoveries need a pool.
    /// </summary>
    internal static bool Applicable(DungeonRun run, DungeonRunState state, DungeonRunTavernOption option)
    {
        var tavern = state.Tavern;
        var op = Behaviour(run, option);
        int size = state.Cards.Count, min = run.DeckMin(state), max = run.DeckMax(state);
        var friendly = LiveFriendly(state);
        int openings = state.Cards.Count(c => c.Opening);
        switch (op.Op)
        {
            case "recruit": return tavern.Bob.Count > 0 && size + 1 <= max;
            case "remove": return friendly.Count > 0 && size - 1 >= min;
            case "setcost": return state.Cards.Any(c => c.Origin != DungeonRunDeck.OriginTreasure);
            case "health": return true;
            case "opening": return openings < DungeonRunDeck.MaxOpeningFlags && friendly.Any(u => !DungeonRunDeck.Find(state, u).Opening);
            case "recruitAll": return tavern.Bob.Count > 0 && size + tavern.Bob.Count <= max;
            case "discover": return size + 1 <= max && DiscoverPool(run, state, op.Pool).Count > 0;
            case "buff": return friendly.Count > 0;
            case "removeAllFriendly": return friendly.Count > 0 && size - friendly.Count >= min;
            case "copy": return size + Math.Max(1, op.N) <= max && friendly.Any(uid => DungeonRunDeck.CopyError(run, state, uid) == null);
            case "removeFromSample": return SampleCards(state, op.Sample).Count > 0 && size - 1 >= min;
            case "discountFromSample": return SampleCards(state, op.Sample).Count > 0;
            case "reroll": return BobPool(run, state).Count > 0;
            case "replace": return friendly.Count > 0 && DiscoverPool(run, state, op.Pool).Count > 0;
            case "discoverOpening": return size + 1 <= max && openings < DungeonRunDeck.MaxOpeningFlags && DiscoverPool(run, state, op.Pool).Count > 0;
            case "discoverBuff": return size + 1 <= max && DiscoverPool(run, state, op.Pool).Count > 0;
            case "rerollTribe": return TribeCandidates(run, state).Count > 0;
            case "removeBuffNeighbours": return friendly.Count > 0 && size - 1 >= min;
            case "removeWhereCostAtMost":
            {
                int n = state.Cards.Count(c => c.Origin != DungeonRunDeck.OriginTreasure && DungeonRunDeck.EffectiveCost(c) <= op.Amount);
                return n > 0 && size - n >= min;
            }
            default: return false;
        }
    }

    /// <summary>The deck minions a tavern's friendly board samples (never a treasure shell), in record order.</summary>
    internal static List<DungeonRunCard> FriendlyCandidates(DungeonRunState state) =>
        state.Cards.Where(c => c.Origin != DungeonRunDeck.OriginTreasure && DungeonRunDeck.BaseCard(c.Id)?.Type == CardType.MINION).ToList();

    /// <summary>
    /// Bob's minions for this node: the run's admitted catalog minions (<see cref="DiscoverPool"/> "minion") up to the act's
    /// cost cap (tavern-v1.json "bobMaxCostByAct"), ordinal id order.
    /// </summary>
    internal static List<string> BobPool(DungeonRun run, DungeonRunState state)
    {
        var caps = run.Data.Tavern?.BobMaxCostByAct ?? new int[0];
        int act = run.CurrentNode(state)?.Act ?? 1;
        int cap = caps.Length == 0 ? int.MaxValue : caps[Math.Min(act, caps.Length) - 1];
        return DiscoverPool(run, state, "minion").Where(id => (DungeonRunDeck.BaseCard(id)?.Cost ?? int.MaxValue) <= cap).ToList();
    }

    /// <summary>
    /// A discover pool, computed from the catalog plus admission (no catalog file change): "minion" and "spell" are the run's
    /// draftable cards of that type (every catalog entry the run is eligible for, the predicate the catalog draft uses; a
    /// legacy-catalog run's fixed bundles offered to its class) that pass the deck-change admission
    /// (<see cref="DungeonRunDeck.AddError"/>); "legendaryMinion" the legendary minions of those; "anyClassWeapon" every
    /// catalog weapon of any class, class waived. A card is left out when it would break the deck's Death Knight runes.
    /// Ordinal id order.
    /// </summary>
    internal static List<string> DiscoverPool(DungeonRun run, DungeonRunState state, string pool)
    {
        var baseList = BasePool(run, state, pool);
        if (!baseList.Any(DungeonRunValidator.HasRuneCost)) return baseList;
        var deck = state.Cards.Select(c => c.Id).ToList();
        return baseList.Where(id => !DungeonRunValidator.HasRuneCost(id) || DungeonRunDeck.RunesLegal(run, state, deck.Concat(new[] { id }))).ToList();
    }

    // The pools before the rune filter, cached per run rules, class set and catalog mode (they depend on nothing else).
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<DungeonRun, Dictionary<string, List<string>>> Pools =
        new System.Runtime.CompilerServices.ConditionalWeakTable<DungeonRun, Dictionary<string, List<string>>>();

    private static List<string> BasePool(DungeonRun run, DungeonRunState state, string pool)
    {
        var classes = run.RunClasses(state);
        string key = pool + "|" + string.Join("/", classes) + "|" + (state.IsCatalogRun ? "catalog" : "legacy");
        var cache = Pools.GetValue(run, _ => new Dictionary<string, List<string>>(StringComparer.Ordinal));
        lock (cache)
        {
            if (cache.TryGetValue(key, out var hit)) return hit;
            List<string> list;
            if (pool == "anyClassWeapon")
                list = run.Data.Bundles.Where(b => b.Offerable).SelectMany(EntryCards).Distinct().OrderBy(id => id, StringComparer.Ordinal)
                    .Where(id => DungeonRunDeck.BaseCard(id)?.Type == CardType.WEAPON && DungeonRunDeck.AddError(run, state, id, true, out _) == null).ToList();
            else
            {
                var type = pool == "spell" ? CardType.SPELL : CardType.MINION;
                IEnumerable<DungeonRunBundle> entries;
                if (state.IsCatalogRun)
                {
                    bool secretHolds = DungeonRunAdmission.MechanicHolds(run.Data, classes, "secret");
                    entries = run.Data.Bundles.Where(b => b.EligibleFor(classes, m => m == "secret" && secretHolds));
                }
                else entries = run.Data.LegacyBundles.Where(b => b.OfferedTo(run.RunClass(state).Id));
                list = entries.SelectMany(EntryCards).Distinct().OrderBy(id => id, StringComparer.Ordinal)
                    .Where(id => DungeonRunDeck.BaseCard(id) is EngineCard c && c.Type == type && (pool != "legendaryMinion" || c.Rarity == Rarity.LEGENDARY))
                    .Where(id => DungeonRunDeck.AddError(run, state, id, false, out _) == null).ToList();
            }
            cache[key] = list;
            return list;
        }
    }

    // A catalog entry's cards: a pool entry's candidates (grants excluded: they are legal inside their entry only), a fixed entry's cards.
    private static IEnumerable<string> EntryCards(DungeonRunBundle entry) =>
        entry.IsPool ? entry.Candidates.Where(c => !c.Grant).Select(c => c.Id) : entry.Cards;

    /// <summary>The deck cards an option samples ("spell": the deck's spells, never a treasure shell), in record order.</summary>
    internal static List<DungeonRunCard> SampleCards(DungeonRunState state, string sample)
    {
        var type = sample == "spell" ? CardType.SPELL : CardType.MINION;
        return state.Cards.Where(c => c.Origin != DungeonRunDeck.OriginTreasure && DungeonRunDeck.BaseCard(c.Id)?.Type == type).ToList();
    }

    /// <summary>Party of Four: minions with a type that Bob can offer at least one minion of.</summary>
    internal static List<string> TribeCandidates(DungeonRun run, DungeonRunState state)
    {
        var bob = BobPool(run, state).Select(DungeonRunDeck.BaseCard).Where(c => c != null).ToList();
        return DiscoverPool(run, state, "minion").Where(id =>
        {
            var race = DungeonRunDeck.BaseCard(id)?.GetRawRace() ?? Race.INVALID;
            return race != Race.INVALID && race != Race.BLANK && race != Race.ALL && bob.Any(c => c.IsRace(race));
        }).ToList();
    }

    // ------------------------------------------------------------------ helpers

    // An alias (ULDA_912 Recruit) runs its target's behaviour; its own cost applies.
    private static DungeonRunTavernOption Behaviour(DungeonRun run, DungeonRunTavernOption option) =>
        option.AliasOf != null ? run.Data.Tavern?.Option(option.AliasOf) ?? option : option;

    // The friendly board's uids still in the deck, in board order.
    private static List<int> LiveFriendly(DungeonRunState state) =>
        (state.Tavern?.Friendly ?? new List<int>()).Where(uid => DungeonRunDeck.Find(state, uid) != null).ToList();

    private static DungeonRunTavern Open(DungeonRunState state, out DeckEditResult error)
    {
        error = null;
        if (state.Phase != DungeonRunPhase.Tavern || state.Tavern == null) { error = Fail(ErrNoTavern, "no tavern is open"); return null; }
        return state.Tavern;
    }

    private static void Charge(DungeonRunTavern tavern, DungeonRunTavernOption option, List<string> targets)
    {
        tavern.Gold -= option.Cost;
        tavern.Bought.Add(new DungeonRunTavernBuy { Option = option.Id, Targets = targets });
    }

    // An option's random choices this visit: the kept draw (stage 0: the purchase, 1: Hiring Replacements' second step), less
    // any choice that is no longer valid (a sampled card removed since, a card the rune filter now refuses); otherwise drawn
    // now and kept. A draw with no choice is not kept (the purchase is refused and its step restored).
    private static List<string> Drawn(DungeonRunTavern tavern, string optionId, int stage, Func<List<string>> draw, Func<string, bool> valid)
    {
        var kept = tavern.Draws.FirstOrDefault(d => d.Option == optionId && d.Stage == stage);
        if (kept != null) return kept.Candidates.Where(valid).ToList();
        var drawn = draw();
        if (drawn.Count > 0) tavern.Draws.Add(new DungeonRunTavernDraw { Option = optionId, Stage = stage, Candidates = new List<string>(drawn) });
        return drawn;
    }

    private static int Roll(DungeonRunState state, int count) =>
        StableSeed.Index(StableSeed.Derive(state.Seed, DungeonRun.TavernStream, state.Tavern.Node, state.Tavern.Step++), count);

    // Draws up to n items without replacement, in draw order.
    private static List<T> Sample<T>(DungeonRunState state, List<T> items, int n)
    {
        var rest = new List<T>(items);
        var picked = new List<T>();
        while (picked.Count < n && rest.Count > 0)
        {
            int i = Roll(state, rest.Count);
            picked.Add(rest[i]);
            rest.RemoveAt(i);
        }
        return picked;
    }

    private static string Text(int uid) => uid.ToString(CultureInfo.InvariantCulture);

    // The deck before a two-step change (every DungeonRunDeck change writes a new list of new records, so the old list is intact).
    private static (List<DungeonRunCard> cards, int next) Save(DungeonRunState state) => (state.Cards, state.NextUid);

    private static void Restore(DungeonRunState state, (List<DungeonRunCard> cards, int next) saved)
    {
        state.Cards = saved.cards;
        state.NextUid = saved.next;
        DungeonRunDeck.Sync(state);
    }

    private static DeckEditResult Fail(string code, string message) => DeckEditResult.Fail(code, message);
}
