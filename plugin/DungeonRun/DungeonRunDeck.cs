using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.CardSets.DungeonRun;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using EngineCard = SabberStoneCore.Model.Card;

/// <summary>The outcome of a persistent deck change: Ok, or a named error (<see cref="DungeonRunDeck"/> Err* codes) and nothing changed.</summary>
internal sealed class DeckEditResult
{
    internal bool Ok;
    /// <summary>The error code (DungeonRunDeck.Err*), null when Ok.</summary>
    internal string Error;
    /// <summary>A readable reason (the UI shows it), null when Ok.</summary>
    internal string Message;
    /// <summary>The card records the change added or changed, in order.</summary>
    internal List<int> Uids = new List<int>();

    internal static DeckEditResult Success(IEnumerable<int> uids = null) =>
        new DeckEditResult { Ok = true, Uids = uids == null ? new List<int>() : uids.ToList() };
    internal static DeckEditResult Fail(string error, string message) => new DeckEditResult { Ok = false, Error = error, Message = message };
    public override string ToString() => Ok ? "ok" : Error + ": " + Message;
}

/// <summary>A deck change a match asks for (phase 6: RunMatchResult.Edits; LOOTA_805/813 add, DALA_721 replace).</summary>
internal sealed class RunDeckEdit
{
    /// <summary>"remove", "replace" or "add" (an add names no slot: <see cref="SlotIndex"/> is ignored).</summary>
    internal string Op { get; set; }
    /// <summary>The card's index in the match plan's deck (RunMatchPlan.PlayerDeck), which is the run's record order.</summary>
    internal int SlotIndex { get; set; }
    /// <summary>The new card id of a "replace" or an "add".</summary>
    internal string NewId { get; set; }
}

/// <summary>
/// An act run's deck as card records (<see cref="DungeonRunState.Cards"/>, save version 4) and its persistent changes
/// (phase 5): add, remove, remove-where, copy, replace, buffs, discounts and opening-hand flags. Each checked change is
/// validated on the hypothetical result before anything is written (deck size, admission, class, Death Knight runes,
/// treasure shells, known enchantments, opening flags) and returns a <see cref="DeckEditResult"/>; on failure the state is
/// untouched. <see cref="Sync"/> is the single writer of the <see cref="DungeonRunState.Deck"/> mirror.
/// A legacy run (ScheduleVersion 0) has no card records: every writer keeps using <see cref="DungeonRunState.Deck"/>, and
/// every checked change refuses it.
/// </summary>
internal static class DungeonRunDeck
{
    internal const string OriginStarter = "starter", OriginDraft = "draft", OriginTreasure = "treasure", OriginTavern = "tavern",
        OriginCopy = "copy", OriginBombastic = "bombastic";
    /// <summary>Phase 6: a hero row's signature treasure card (a treasure card: never a change target), and a card a won match
    /// added or replaced (RunMatchResult.Edits).</summary>
    internal const string OriginSignature = "signature", OriginMatch = "match";
    /// <summary>Phase 7: a Rumble Run teammate taken in a teammate draft (an ordinary deck card: tavern and mutation ops apply).</summary>
    internal const string OriginTeammate = "teammate";
    /// <summary>A treasure or signature card: never a deck change's target, never removed by a remove-where.</summary>
    internal static bool IsTreasureOrigin(string origin) => origin == OriginTreasure || origin == OriginSignature;

    /// <summary>The most opening-hand flags a run deck may carry (the engine's GameConfig.MaxOpeningSlots).</summary>
    internal const int MaxOpeningFlags = SabberStoneCore.Config.GameConfig.MaxOpeningSlots;

    // Error codes of a refused change.
    internal const string ErrNotActRun = "not-act-run", ErrNoCard = "no-card", ErrTreasureShell = "treasure-shell",
        ErrDeckMin = "deck-min", ErrDeckMax = "deck-max", ErrUnknownId = "unknown-id", ErrInadmissible = "inadmissible",
        ErrClass = "class", ErrRunes = "runes", ErrUnknownEnchantment = "unknown-enchantment",
        ErrEnchantmentTarget = "enchantment-target", ErrOpeningFlags = "opening-flags", ErrCount = "count", ErrUnknownOp = "unknown-op";

    /// <summary>The deck enchantments a card may carry: Bob's original tavern enchantments (tavern-v1.json "ench").</summary>
    internal static IReadOnlyDictionary<string, DungeonRunEnchantment> KnownEnchantments => DungeonRunTavernTable.KnownEnchantments;

    // ------------------------------------------------------------------ unchecked writers (the run layer's own adds)

    /// <summary>
    /// Appends a card record with a new uid (and its enchantment ids, in order) and rewrites the mirror; returns the record.
    /// Unchecked: the run layer's starter, draft and treasure adds, already validated by their own rules. A legacy run appends
    /// the id to <see cref="DungeonRunState.Deck"/> only and returns null.
    /// </summary>
    internal static DungeonRunCard Add(DungeonRunState state, string id, string origin, IEnumerable<string> mods = null)
    {
        if (id == null) throw new ArgumentNullException(nameof(id));
        if (!state.IsActRun) { state.Deck.Add(id); return null; }
        var card = new DungeonRunCard
        {
            Uid = ++state.NextUid, Id = id, Origin = origin, AddedFloor = state.Floor,
            Mods = mods == null ? new List<string>() : mods.ToList(),
        };
        state.Cards.Add(card);
        Sync(state);
        return card;
    }

    /// <summary>The single writer of an act run's mirror: <see cref="DungeonRunState.Deck"/> = the records' ids, in record order.</summary>
    internal static void Sync(DungeonRunState state)
    {
        if (!state.IsActRun) return;
        state.Deck = state.Cards.Select(c => c.Id).ToList();
    }

    /// <summary>The record of <paramref name="uid"/>, or null.</summary>
    internal static DungeonRunCard Find(DungeonRunState state, int uid) => state.Cards.FirstOrDefault(c => c.Uid == uid);

    /// <summary>
    /// Why an act run's mirror is broken (Deck differs from the records' ids, a uid repeats or reaches past NextUid), or null.
    /// A legacy run has no records and always passes.
    /// </summary>
    internal static string MirrorError(DungeonRunState state)
    {
        if (state == null || !state.IsActRun) return null;
        var cards = state.Cards ?? new List<DungeonRunCard>();
        if (state.Deck == null || !state.Deck.SequenceEqual(cards.Select(c => c.Id)))
            return "the run deck (" + (state.Deck?.Count ?? 0) + " cards) does not mirror its card records (" + cards.Count + ")";
        if (cards.Select(c => c.Uid).Distinct().Count() != cards.Count) return "a card record uid repeats";
        if (cards.Any(c => c.Uid <= 0 || c.Uid > state.NextUid)) return "a card record uid is outside 1.." + state.NextUid;
        return null;
    }

    // ------------------------------------------------------------------ checked changes (phase 5)

    /// <summary>Adds a card (a recruit, a discovery): the id must pass the run's admission, class (unless <paramref name="classWaived"/>, Friendly Smith) and rune checks.</summary>
    internal static DeckEditResult TryAdd(DungeonRun run, DungeonRunState state, string id, string origin, IEnumerable<string> mods = null,
        bool classWaived = false)
    {
        if (!state.IsActRun) return NotActRun();
        var next = CloneCards(state);
        int nextUid = state.NextUid;
        var card = new DungeonRunCard { Uid = ++nextUid, Id = id, Origin = origin, AddedFloor = state.Floor, Mods = mods?.ToList() ?? new List<string>() };
        string modError = ModsError(card);
        if (modError != null) return Fail(ModsCode(card), modError);
        next.Add(card);
        return Commit(run, state, next, nextUid, new[] { id }, classWaived, new[] { card.Uid });
    }

    /// <summary>Adds several cards at once (Round of Drinks, Fast Food): all or none.</summary>
    internal static DeckEditResult TryAddAll(DungeonRun run, DungeonRunState state, IEnumerable<string> ids, string origin, IEnumerable<string> mods = null)
    {
        if (!state.IsActRun) return NotActRun();
        var next = CloneCards(state);
        int nextUid = state.NextUid;
        var added = new List<int>();
        var list = ids.ToList();
        foreach (string id in list)
        {
            var card = new DungeonRunCard { Uid = ++nextUid, Id = id, Origin = origin, AddedFloor = state.Floor, Mods = mods?.ToList() ?? new List<string>() };
            string modError = ModsError(card);
            if (modError != null) return Fail(ModsCode(card), modError);
            next.Add(card);
            added.Add(card.Uid);
        }
        return Commit(run, state, next, nextUid, list, false, added);
    }

    /// <summary>Removes one card (Dismiss, Kindle).</summary>
    internal static DeckEditResult Remove(DungeonRun run, DungeonRunState state, int uid)
    {
        if (!state.IsActRun) return NotActRun();
        var (card, error) = Target(state, uid);
        if (error != null) return error;
        var next = CloneCards(state).Where(c => c.Uid != uid).ToList();
        return Commit(run, state, next, state.NextUid, null, false, new[] { card.Uid });
    }

    /// <summary>
    /// Removes every card matching <paramref name="match"/> at once (You're All Fired!, Do the Math), never a treasure shell;
    /// all or none: a result below deckMin is refused.
    /// </summary>
    internal static DeckEditResult RemoveWhere(DungeonRun run, DungeonRunState state, Func<DungeonRunCard, bool> match)
    {
        if (!state.IsActRun) return NotActRun();
        var removed = state.Cards.Where(c => !IsTreasureOrigin(c.Origin) && match(c)).Select(c => c.Uid).ToList();
        var next = CloneCards(state).Where(c => !removed.Contains(c.Uid)).ToList();
        return Commit(run, state, next, state.NextUid, null, false, removed);
    }

    /// <summary>
    /// Adds <paramref name="n"/> copies of a card (The Gang's All Here!): origin copy, CopyOf the source, its enchantments
    /// when <paramref name="keepMods"/>, never its opening flag. A copy of a card already in the deck needs no admission, but
    /// it obeys the run's class rule (<see cref="CopyError"/>): an off-class card the deck holds by exception (a catalog
    /// grant, a class-waived Friendly Smith weapon) is never multiplied.
    /// </summary>
    internal static DeckEditResult Copy(DungeonRun run, DungeonRunState state, int uid, int n, bool keepMods = true)
    {
        if (!state.IsActRun) return NotActRun();
        if (n < 1) return Fail(ErrCount, "a copy needs at least one card");
        var (source, error) = Target(state, uid);
        if (error != null) return error;
        string classError = CopyError(run, state, uid);
        if (classError != null) return Fail(ErrClass, classError);
        var next = CloneCards(state);
        int nextUid = state.NextUid;
        var added = new List<int>();
        for (int i = 0; i < n; i++)
        {
            var copy = new DungeonRunCard
            {
                Uid = ++nextUid, Id = source.Id, Origin = OriginCopy, CopyOf = source.Uid, AddedFloor = state.Floor, Opening = false,
                Mods = keepMods ? new List<string>(source.Mods) : new List<string>(),
            };
            next.Add(copy);
            added.Add(copy.Uid);
        }
        return Commit(run, state, next, nextUid, null, false, added);
    }

    /// <summary>
    /// Replaces a card in its slot (Hiring Replacements): the uid stays, the id becomes <paramref name="newId"/>, and its
    /// enchantments, opening flag and copy link are dropped: it is a new card at the same place.
    /// </summary>
    internal static DeckEditResult Replace(DungeonRun run, DungeonRunState state, int uid, string newId, string origin, bool classWaived = false)
    {
        if (!state.IsActRun) return NotActRun();
        var (_, error) = Target(state, uid);
        if (error != null) return error;
        var next = CloneCards(state);
        var card = next.First(c => c.Uid == uid);
        card.Id = newId; card.Origin = origin; card.CopyOf = 0; card.Opening = false; card.AddedFloor = state.Floor;
        card.Mods = new List<string>();
        return Commit(run, state, next, state.NextUid, new[] { newId }, classWaived, new[] { uid });
    }

    /// <summary>A stat enchantment (Tell a Story, Tall Tales, Work, Work!; ULDA_603e on a weapon): appended to the card's mods.</summary>
    internal static DeckEditResult Buff(DungeonRun run, DungeonRunState state, int uid, string enchId) => AddMod(run, state, uid, enchId, cost: false);

    /// <summary>A cost enchantment (DALA_914e -3, ULDA_605e -1, DALA_903e sets 0): appended to the card's mods.</summary>
    internal static DeckEditResult Discount(DungeonRun run, DungeonRunState state, int uid, string enchId) => AddMod(run, state, uid, enchId, cost: true);

    /// <summary>Sets or clears a card's opening-hand flag (at most <see cref="MaxOpeningFlags"/> on the deck).</summary>
    internal static DeckEditResult SetOpening(DungeonRun run, DungeonRunState state, int uid, bool opening)
    {
        if (!state.IsActRun) return NotActRun();
        var (_, error) = Target(state, uid);
        if (error != null) return error;
        var next = CloneCards(state);
        next.First(c => c.Uid == uid).Opening = opening;
        return Commit(run, state, next, state.NextUid, null, false, new[] { uid });
    }

    /// <summary>
    /// Applies the deck changes a won match asked for (phase 6: RunMatchResult.Edits through DungeonRun.ApplyMatchResult). Slot
    /// indices are the match plan's deck order (the run's record order when the match began), resolved to uids before any
    /// change, so the edits apply in order to the cards they named; an add appends a checked card (origin match); all or none.
    /// </summary>
    internal static DeckEditResult ApplyMatchEdits(DungeonRun run, DungeonRunState state, IReadOnlyList<RunDeckEdit> edits)
    {
        if (!state.IsActRun) return NotActRun();
        if (edits == null || edits.Count == 0) return DeckEditResult.Success();
        var uids = new List<int>();
        foreach (var edit in edits)
        {
            if (edit != null && edit.Op == "add") { uids.Add(0); continue; }
            if (edit == null || edit.SlotIndex < 0 || edit.SlotIndex >= state.Cards.Count) return Fail(ErrNoCard, "a match edit names a slot outside the deck");
            uids.Add(state.Cards[edit.SlotIndex].Uid);
        }
        var before = CloneCards(state);
        int beforeUid = state.NextUid;
        var changed = new List<int>();
        for (int i = 0; i < edits.Count; i++)
        {
            var result = edits[i].Op == "remove" ? Remove(run, state, uids[i])
                : edits[i].Op == "replace" ? Replace(run, state, uids[i], edits[i].NewId, OriginMatch)
                : edits[i].Op == "add" ? TryAdd(run, state, edits[i].NewId, OriginMatch)
                : Fail(ErrUnknownOp, "unknown match edit " + edits[i].Op);
            if (!result.Ok)
            {
                state.Cards = before;
                state.NextUid = beforeUid;
                Sync(state);
                return result;
            }
            changed.AddRange(result.Uids);
        }
        return DeckEditResult.Success(changed);
    }

    // ------------------------------------------------------------------ derived view (display and selection)

    /// <summary>The card's cost after its enchantments, in order (a set, then the deltas after it), at least 0.</summary>
    internal static int EffectiveCost(DungeonRunCard card)
    {
        int cost = BaseCard(card.Id)?.Cost ?? 0;
        foreach (string mod in card.Mods)
            if (KnownEnchantments.TryGetValue(mod, out var e)) cost = e.SetCost is int set ? set : cost + e.CostDelta;
        return Math.Max(0, cost);
    }

    /// <summary>The card's Attack after its enchantments (a minion's or weapon's; 0 for a spell).</summary>
    internal static int EffectiveAttack(DungeonRunCard card) =>
        (BaseCard(card.Id)?.ATK ?? 0) + card.Mods.Sum(m => KnownEnchantments.TryGetValue(m, out var e) ? e.Attack : 0);

    /// <summary>The card's Health (a weapon's Durability) after its enchantments.</summary>
    internal static int EffectiveHealth(DungeonRunCard card)
    {
        var c = BaseCard(card.Id);
        int baseHealth = c == null ? 0 : c.Type == CardType.WEAPON ? c[GameTag.DURABILITY] : c.Health;
        return baseHealth + card.Mods.Sum(m => KnownEnchantments.TryGetValue(m, out var e) ? e.Health : 0);
    }

    /// <summary>
    /// The deck view's text of a card: its name, and when it changed, "(attack/health, cost c)" and "[opening]"
    /// ("Chillwind Yeti (6/7, cost 4) [opening]").
    /// </summary>
    internal static string Describe(DungeonRunCard card, Func<string, string> name)
    {
        string text = name(card.Id);
        if (card.Mods.Count > 0)
        {
            var c = BaseCard(card.Id);
            bool stats = c != null && (c.Type == CardType.MINION || c.Type == CardType.WEAPON);
            text += " (" + (stats ? EffectiveAttack(card) + "/" + EffectiveHealth(card) + ", " : "") + "cost " + EffectiveCost(card) + ")";
        }
        if (card.Opening) text += " [opening]";
        return text;
    }

    /// <summary>
    /// The match plan's per-card records (RunMatchPlan.PlayerDeckSlots), aligned with the deck: each card's mods, plus the
    /// opening marker (DALA_905e) on a flagged card, its opening flag and origin. Null on a legacy run.
    /// </summary>
    internal static List<RunDeckSlot> PlanSlots(DungeonRunState state)
    {
        if (!state.IsActRun) return null;
        return state.Cards.Select(c => new RunDeckSlot
        {
            Mods = (c.Opening && !c.Mods.Contains(DungeonRunTavernTable.OpeningMarker) ? c.Mods.Concat(new[] { DungeonRunTavernTable.OpeningMarker }) : c.Mods).ToArray(),
            Opening = c.Opening,
            Origin = c.Origin,
        }).ToList();
    }

    internal static EngineCard BaseCard(string id)
    {
        try { return id == null ? null : Cards.FromId(id); } catch { return null; }
    }

    /// <summary>
    /// Why a card id may not be added to (or replace a card of) this run's deck, or null: the drafted-card predicate
    /// (DungeonRunValidator.PoolCardFailures: client, scripting, meta, deck size, sideboards, seat order, admission for the
    /// player deck, and the run's class set unless <paramref name="classWaived"/>), plus the run's generation exclusions.
    /// Returns the error code (<see cref="ErrUnknownId"/>, <see cref="ErrClass"/>, <see cref="ErrInadmissible"/>:code).
    /// </summary>
    internal static string AddError(DungeonRun run, DungeonRunState state, string id, bool classWaived, out string message)
    {
        message = null;
        if (BaseCard(id) == null) { message = (id ?? "(none)") + " is not a card"; return ErrUnknownId; }
        var failure = DungeonRunValidator.PoolCardFailures(id, "tavern", run.RunClasses(state), classWaived, Policy, run.Data.ClientKnows, state.IsCatalogRun)
            .FirstOrDefault();
        if (failure.Code != null)
        {
            message = failure.Message ?? id + " is not a card";
            return failure.Code == "missing" ? ErrUnknownId : failure.Code == "class" ? ErrClass : ErrInadmissible + ":" + failure.Code;
        }
        if (GeneratedRejects(state.IsCatalogRun).Contains(id)) { message = id + " is never generated in a run match"; return ErrInadmissible + ":generated"; }
        return null;
    }

    /// <summary>
    /// Why the card record <paramref name="uid"/> may not be copied for its class, or null: its id must be allowed in the run's
    /// class set (the class part of <see cref="AddError"/>; admission is not asked again for a card already in the deck).
    /// </summary>
    internal static string CopyError(DungeonRun run, DungeonRunState state, int uid)
    {
        var card = Find(state, uid);
        if (card == null) return "no card " + uid + " in the run deck";
        var failure = DungeonRunValidator.PoolCardFailures(card.Id, "copy", run.LootClasses(state), false, null, null, state.IsCatalogRun)
            .FirstOrDefault(f => f.Code == "class");
        return failure.Code == null ? null : card.Id + " cannot be copied: " + failure.Message;
    }

    /// <summary>The deck <paramref name="ids"/> keeps a Death Knight run legal (at most 3 runes); any other class always passes.</summary>
    internal static bool RunesLegal(DungeonRun run, DungeonRunState state, IEnumerable<string> ids)
    {
        if (!Enum.TryParse(run.RunClasses(state)[0], out CardClass cls)) return true;
        try { return SabberStoneCore.Actions.RuneRules.IsLegalForClass(SabberStoneCore.Actions.RuneRules.FromCards(ids.Select(Cards.FromId)), cls); }
        catch { return false; }
    }

    // ------------------------------------------------------------------ helpers

    private static OfflinePoolPolicy policy;
    private static OfflinePoolPolicy Policy => policy ?? (policy = OfflinePoolPolicy.Load());
    private static readonly Dictionary<bool, HashSet<string>> generatedRejects = new Dictionary<bool, HashSet<string>>();
    private static HashSet<string> GeneratedRejects(bool catalogRun)
    {
        lock (generatedRejects)
        {
            if (!generatedRejects.TryGetValue(catalogRun, out var set))
                generatedRejects[catalogRun] = set = new HashSet<string>(DungeonRunAdmission.GeneratedRejects(catalogRun), StringComparer.Ordinal);
            return set;
        }
    }

    private static DeckEditResult NotActRun() => Fail(ErrNotActRun, "only an act run keeps card records");
    private static DeckEditResult Fail(string code, string message) => DeckEditResult.Fail(code, message);

    // The record of a change's target, or the error: unknown uid, or an active-treasure shell (never a target: its upgrade
    // finds it by uid, and a copy would duplicate a treasure).
    private static (DungeonRunCard, DeckEditResult) Target(DungeonRunState state, int uid)
    {
        var card = Find(state, uid);
        if (card == null) return (null, Fail(ErrNoCard, "no card " + uid + " in the run deck"));
        if (IsTreasureOrigin(card.Origin)) return (null, Fail(ErrTreasureShell, "a treasure card cannot be changed"));
        return (card, null);
    }

    private static DeckEditResult AddMod(DungeonRun run, DungeonRunState state, int uid, string enchId, bool cost)
    {
        if (!state.IsActRun) return NotActRun();
        var (card, error) = Target(state, uid);
        if (error != null) return error;
        if (enchId == null || !KnownEnchantments.TryGetValue(enchId, out var e) || e.Target == "marker")
            return Fail(ErrUnknownEnchantment, (enchId ?? "(none)") + " is not a tavern deck enchantment");
        if (cost != (e.Target == "cost")) return Fail(ErrEnchantmentTarget, enchId + (cost ? " is not a cost enchantment" : " is not a stat enchantment"));
        var next = CloneCards(state);
        var target = next.First(c => c.Uid == uid);
        target.Mods.Add(enchId);
        string modError = ModsError(target);
        if (modError != null) return Fail(ModsCode(target), modError);
        return Commit(run, state, next, state.NextUid, null, false, new[] { card.Uid });
    }

    // Why a card's mods are wrong (an unknown id, a stat enchantment on the wrong card type), or null.
    private static string ModsError(DungeonRunCard card)
    {
        var c = BaseCard(card.Id);
        foreach (string mod in card.Mods)
        {
            if (mod == null || !KnownEnchantments.TryGetValue(mod, out var e) || e.Target == "marker") return (mod ?? "(none)") + " is not a tavern deck enchantment";
            if (e.Target == "minion" && c?.Type != CardType.MINION) return mod + " buffs minions only";
            if (e.Target == "weapon" && c?.Type != CardType.WEAPON) return mod + " buffs weapons only";
        }
        return null;
    }

    private static string ModsCode(DungeonRunCard card) =>
        card.Mods.Any(m => m == null || !KnownEnchantments.TryGetValue(m, out var e) || e.Target == "marker") ? ErrUnknownEnchantment : ErrEnchantmentTarget;

    // Validates the hypothetical deck and writes it: size, opening flags, every added id's admission and class, runes.
    private static DeckEditResult Commit(DungeonRun run, DungeonRunState state, List<DungeonRunCard> next, int nextUid, IEnumerable<string> addedIds,
        bool classWaived, IEnumerable<int> uids)
    {
        // A change may not shrink the deck below deckMin or grow it past deckMax (one that moves toward the range, or keeps the
        // size, is never refused for it).
        int min = run.DeckMin(state), max = run.DeckMax(state);
        if (next.Count < min && next.Count < state.Cards.Count) return Fail(ErrDeckMin, "the deck would hold " + next.Count + " cards; it needs at least " + min);
        if (next.Count > max && next.Count > state.Cards.Count) return Fail(ErrDeckMax, "the deck would hold " + next.Count + " cards; it holds at most " + max);
        if (next.Count(c => c.Opening) > MaxOpeningFlags) return Fail(ErrOpeningFlags, "at most " + MaxOpeningFlags + " cards can always start in your hand");
        var added = addedIds?.Distinct().ToList() ?? new List<string>();
        foreach (string id in added)
        {
            string code = AddError(run, state, id, classWaived, out string message);
            if (code != null) return Fail(code, message);
        }
        if (added.Any(DungeonRunValidator.HasRuneCost) && !RunesLegal(run, state, next.Select(c => c.Id)))
            return Fail(ErrRunes, "the deck would need more than three Death Knight runes");
        state.Cards = next;
        state.NextUid = nextUid;
        Sync(state);
        return DeckEditResult.Success(uids);
    }

    private static DungeonRunCard Clone(DungeonRunCard c) => new DungeonRunCard
    {
        Uid = c.Uid, Id = c.Id, Origin = c.Origin, CopyOf = c.CopyOf, AddedFloor = c.AddedFloor, Opening = c.Opening,
        Mods = new List<string>(c.Mods ?? new List<string>()), Log = new List<string>(c.Log ?? new List<string>()),
    };

    private static List<DungeonRunCard> CloneCards(DungeonRunState state) => state.Cards.Select(Clone).ToList();
}
