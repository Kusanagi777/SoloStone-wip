using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SabberStoneCore.CardSets.DungeonRun;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;

/// <summary>
/// The Dungeon Run rules over <see cref="DungeonRunState"/> (docs/design/dungeon-run.md). Pure C# over the
/// engine's card database: no Unity or client types, so it is tested headlessly. Every random choice derives
/// from the run seed through <see cref="StableSeed"/> (design F1); nothing reads a clock or System.Random.
/// </summary>
internal sealed class DungeonRun
{
    // RNG stream ids (design F1)
    // BucketStep: the cards a catalog bundle offer draws from its pool entry (card k of slot s: component s * 16 + k).
    private const long BossStream = 1, MatchStream = 2, DraftStream = 3, TreasureStep = 1, BundleStep = 2, BucketStep = 3;
    // Act runs (phases 4 and 5). Tavern and modifier randomness keys on the schedule node (StableSeed component Node), never on
    // the floor; the act boss sequence keys on act * 10 + slot (the major: act * 10 + 9). Streams 1-3 keep their legacy ids.
    internal const long TavernStream = 6, ModifierStream = 7, ActBossStream = 8;
    // Phase 6: a hero row's signature offer (component: the row's signature count) and a generated starter (component: 100 x
    // the class index, so the dual generator's second class draws apart from its first).
    internal const long SignatureStream = 9, GeneratorStream = 10;
    // Phase 7: the teammate and shrine-upgrade draft offers (component: the floor), drawn once and stored in the draft.
    internal const long TeammateStream = 11, ShrineUpgradeStream = 12;
    // Phase 8: the default schedule of a new run (component 0), the provenance-mix boss draw (act * 10 + slot, then the pick
    // index; a boss's shrine variant: 1000 + floor), the anomaly offer (Node, draw) and the twist roll (act, 0 chance / 1 pick).
    internal const long ScheduleStream = 13, EncounterStream = 14, AnomalyStream = 15, TwistStream = 16;
    /// <summary>Rules an anomaly node rolls (the offer adds "none").</summary>
    internal const int AnomalyOptions = 2;
    /// <summary>ULDA_720 Battle Scars (a run-scope rule): the run hero starts at <see cref="BattleScarsHealth"/> and keeps its damage between fights.</summary>
    internal const string BattleScars = "ULDA_720";
    internal const int BattleScarsHealth = 100;
    /// <summary>ULDA_729 Multiclassing (a run-scope rule): "Loot can be from any class" (<see cref="LootClasses"/>).</summary>
    internal const string Multiclassing = "ULDA_729";
    /// <summary>ULDA_115 "Gnomebliterator": each cast outside a Plague Lord fight banks this much (plus its Spell Damage) for the run's next persistent-damage boss (DungeonRunState.PlagueLordDamage).</summary>
    internal const string Gnomebliterator = "ULDA_115";
    internal const int GnomebliteratorDamage = 10;
    /// <summary>The drafts phase 7 adds (DungeonRunDraft.Kind): picked on the treasure screen.</summary>
    internal const string TeammateKind = "teammate", ShrineUpgradeKind = "shrine-upgrade", TreasureKind = "treasure";
    /// <summary>Options a teammate or shrine-upgrade draft offers.</summary>
    internal const int Phase7Options = 3;
    /// <summary>The run deck size a legacy run's match accepts (NativeMatch).</summary>
    internal const int LegacyDeckMax = 40;
    /// <summary>Modifiers offered at a modifier node.</summary>
    internal const int ModifierOptions = 2;

    internal DungeonRunData Data { get; }
    internal DungeonRunRules Rules => Data.Rules;

    internal DungeonRun(DungeonRunData data) { Data = data; }

    // ------------------------------------------------------------------ unlocks (design R13)

    internal bool IsUnlocked(DungeonRunProfile profile, string unlockKey) =>
        unlockKey == null || profile.Unlocked.Contains(unlockKey);

    /// <summary>
    /// Every class is always selectable; decks and powers within it unlock (design R13). The class rows only: a named hero row
    /// (phase 6) is chosen under its primary class (<see cref="AvailableHeroes"/>).
    /// </summary>
    internal IEnumerable<DungeonRunClass> AvailableClasses(DungeonRunProfile profile) => Data.ClassRows;

    /// <summary>
    /// The rows a picker offers under class <paramref name="classId"/> (phase 6, the plank menu's Hero step): the class row
    /// first, then every playable hero row whose primary class it is (a dual row sits under its primary class), in data order.
    /// Empty for an unknown class.
    /// </summary>
    internal IEnumerable<DungeonRunClass> AvailableHeroes(string classId, DungeonRunProfile profile)
    {
        var cls = Data.ClassRows.FirstOrDefault(c => c.Id == classId);
        if (cls == null) return Enumerable.Empty<DungeonRunClass>();
        return new[] { cls }.Concat(Data.HeroRows.Where(h => h.ClassSet.Count > 0 && h.ClassSet[0] == classId));
    }

    /// <summary>The class's starter decks this profile may start with, in data order; the default is always first.</summary>
    internal IEnumerable<DungeonRunStarterDeck> AvailableDecks(DungeonRunClass cls, DungeonRunProfile profile) =>
        cls.Decks.Select(Data.FindDeck).Where(d => d != null && IsUnlocked(profile, d.Unlock));

    /// <summary>Powers this class may start with, in data order; the basic power is always available.</summary>
    internal IEnumerable<string> AvailablePowers(DungeonRunClass cls, DungeonRunProfile profile) =>
        cls.Powers.Where(p => IsUnlocked(profile, cls.PowerUnlock(p)));

    internal bool Holds(DungeonRunCondition c, DungeonRunProfile profile)
    {
        if (c.AllOf != null && !c.AllOf.All(x => Holds(x, profile))) return false;
        if (c.AnyOf != null && !c.AnyOf.Any(x => Holds(x, profile))) return false;
        var stats = profile.Heroes.Where(s => c.Class == null || s.Class == c.Class).ToList();
        if (c.FloorReached is int floor && stats.Select(s => s.BestFloor).DefaultIfEmpty(0).Max() < floor) return false;
        if (c.RunsWon is int wins && stats.Sum(s => s.Wins) < wins) return false;
        if (c.ClassesWon is int classes && profile.Heroes.Where(s => s.Class != null && s.Wins > 0).Select(s => s.Class).Distinct().Count() < classes) return false;
        if (c.MajorsCleared is int majors && stats.Sum(s => s.MajorsCleared) < majors) return false;
        return true;
    }

    /// <summary>Adds every unlock whose condition now holds; returns the new ones.</summary>
    internal List<string> RefreshUnlocks(DungeonRunProfile profile)
    {
        var added = Data.Unlocks.Where(u => !profile.Unlocked.Contains(u.Key) && Holds(u.Value, profile)).Select(u => u.Key).OrderBy(k => k, StringComparer.Ordinal).ToList();
        profile.Unlocked.AddRange(added);
        return added;
    }

    // ------------------------------------------------------------------ new run

    // Refusal texts double as the picker's and the menu's messages (DungeonRunRuntime.NewRun).
    internal const string LockedDeck = "That deck is locked.", LockedPower = "That hero power is locked.",
        OtherClassDeck = "That deck belongs to another class.", ChooseSignature = "Choose a signature treasure.",
        NotOffered = "That signature treasure is not on offer.";

    /// <summary>
    /// Starts a run with a class, one of its starter decks and one of its powers (design R2). An unknown id throws
    /// <see cref="ArgumentException"/>; a locked deck or power, or a deck of another class, throws
    /// <see cref="InvalidOperationException"/>. The run deck is a copy of the starter deck.
    /// <paramref name="schedule"/> picks the act/floor schedule: null is schedule-v1.json "default" when schedules are loaded and
    /// the legacy fixed eight floors when none are; <see cref="DungeonRunSchedules.LegacyId"/> is always the legacy floors. An
    /// act run is stamped with ScheduleVersion, Schedule and its first node, never changed afterwards, and keeps its deck as
    /// card records (<see cref="DungeonRunDeck"/>).
    /// </summary>
    /// <param name="signatureId">A hero row with signatures (Tombs of Terror): one of <see cref="SignatureOffer"/> for this seed,
    /// required; any other row: must be null.</param>
    internal DungeonRunState NewRun(DungeonRunProfile profile, string classId, string deckId, string powerId, long seed, bool typedSeed = false,
        string schedule = null, string signatureId = null)
    {
        var state = NewRunCore(profile, classId, deckId, powerId, seed, typedSeed, schedule);
        ApplyLoadout(profile, state, signatureId);
        return state;
    }

    private DungeonRunState NewRunCore(DungeonRunProfile profile, string classId, string deckId, string powerId, long seed, bool typedSeed,
        string schedule)
    {
        var cls = Data.FindClass(classId) ?? throw new ArgumentException("Unknown class " + (classId ?? "(none)") + ".");
        var deck = Data.FindDeck(deckId) ?? throw new ArgumentException("Unknown deck " + (deckId ?? "(none)") + ".");
        if (powerId == null || !cls.Powers.Contains(powerId)) throw new ArgumentException("Unknown hero power " + (powerId ?? "(none)") + " for " + cls.Id + ".");
        if (deck.Class != cls.Id || !cls.Decks.Contains(deck.Id)) throw new InvalidOperationException(OtherClassDeck);
        if (!IsUnlocked(profile, deck.Unlock)) throw new InvalidOperationException(LockedDeck);
        if (!IsUnlocked(profile, cls.PowerUnlock(powerId))) throw new InvalidOperationException(LockedPower);
        DungeonRunSchedule acts = null;
        if (schedule != DungeonRunSchedules.LegacyId && (schedule != null || Data.Schedules != null))
        {
            string id = schedule ?? DefaultSchedule(seed);
            acts = Data.Schedules?.Find(id) ?? throw new ArgumentException("Unknown schedule " + id + ".");
        }
        // A generated starter (phase 6) is drawn from the run seed; a fixed one is the deck's cards.
        var starter = deck.IsGenerated ? GenerateStarter(cls, deck, seed) : deck.Cards;
        if (acts == null)
            return new DungeonRunState
            {
                // A new run drafts from the loaded catalog (catalog-v1.json "catalogVersion"); with no catalog loaded it is a
                // legacy run (0), as every run of an older save is.
                DataVersion = Data.DataVersion, CatalogVersion = Data.CatalogVersion, Seed = seed, TypedSeed = typedSeed,
                Class = cls.Id, StarterDeck = deck.Id, Power = powerId,
                Deck = new List<string>(starter),
                BossSequence = BossSequence(profile, seed),
            };
        var state = new DungeonRunState
        {
            DataVersion = Data.DataVersion, CatalogVersion = Data.CatalogVersion, Seed = seed, TypedSeed = typedSeed,
            Class = cls.Id, StarterDeck = deck.Id, Power = powerId,
            // A schedule an older version released unchanged keeps that version's stamp (DungeonRunSchedule.StampVersion).
            ScheduleVersion = acts.StampVersion, Schedule = acts.Id, Node = 0,
        };
        // Wave 4 review: a Rumble Run champion appears only "if the player has not chosen one of the <class> shrines" (the
        // champions' wiki pages): a run that will hold a board shrine never draws a boss row with a shrine variant of that class.
        string runShrine = BoardShrineFor(state);
        // Wave 5: a run never meets a boss row of the character it plays (the hero row's "excludeBosses").
        state.BossSequence = ActBossSequence(profile, seed, acts, ledger: !typedSeed,
            excludeShrineClass: runShrine == null ? (CardClass?)null : Cards.FromId(runShrine).Class, excludeBosses: cls.ExcludeBosses);
        foreach (string id in starter) DungeonRunDeck.Add(state, id, DungeonRunDeck.OriginStarter);
        // Phase 8: the acts' twists are rolled now, against the bosses just drawn (none on a schedule without twist pools).
        RollTwists(state, acts);
        EnterNode(state);
        return state;
    }

    /// <summary>
    /// The schedule a new run with no named schedule follows (phase 8): one of schedule-v1.json "defaults", picked from the run
    /// seed on <see cref="ScheduleStream"/>, or "default" when the file lists none (every file before scheduleVersion 3).
    /// </summary>
    internal string DefaultSchedule(long seed)
    {
        var defaults = Data.Schedules.Defaults;
        return defaults.Count == 0 ? Data.Schedules.Default : defaults[StableSeed.Index(StableSeed.Derive(seed, ScheduleStream, 0), defaults.Count)];
    }

    // ------------------------------------------------------------------ loadouts (phase 6)

    /// <summary>
    /// The signature treasures a run of <paramref name="cls"/> on <paramref name="seed"/> is offered (Tombs of Terror): the
    /// row's signatures whose own unlock holds, ordinal order, seeded-shuffled on <see cref="SignatureStream"/>, the first
    /// <see cref="DungeonRunClass.SignatureOffer"/>. Ids are the row's (a Jr. id: the Sr. upgrade is applied when the run
    /// starts, <see cref="SignatureCard"/>). Empty for a row with no signatures.
    /// </summary>
    internal List<string> SignatureOffer(DungeonRunProfile profile, DungeonRunClass cls, long seed)
    {
        if (cls == null || cls.Signatures.Count == 0) return new List<string>();
        var pool = cls.Signatures.Where(id => IsUnlocked(profile, Data.FindTreasure(id)?.Unlock)).Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal).ToList();
        Shuffle(pool, seed, SignatureStream, cls.Signatures.Count);
        return pool.Take(Math.Max(0, cls.SignatureOffer)).ToList();
    }

    /// <summary>
    /// The treasure a picked signature starts the run as: its <see cref="DungeonRunTreasure.UpgradesTo"/> when its
    /// <see cref="DungeonRunTreasure.UpgradeUnlock"/> holds for the profile (Jr. to Sr., design section 4 kind C), else itself.
    /// </summary>
    internal string SignatureCard(DungeonRunProfile profile, string signatureId)
    {
        var t = Data.FindTreasure(signatureId);
        return t?.UpgradeUnlock != null && t.UpgradesTo != null && IsUnlocked(profile, t.UpgradeUnlock) ? t.UpgradesTo : signatureId;
    }

    // A hero row's loadout: the row id, the picked signature (checked against this seed's offer), the signature's card (or
    // passive) and its upgrade schedule, and any run-persistent power upgrade. A class row with no power upgrade keeps
    // Loadout null, so its save is byte for byte what it was before phase 6.
    private void ApplyLoadout(DungeonRunProfile profile, DungeonRunState state, string signatureId)
    {
        var cls = RunClass(state);
        var offer = SignatureOffer(profile, cls, state.Seed);
        if (offer.Count > 0 && signatureId == null) throw new InvalidOperationException(ChooseSignature);
        if (signatureId != null && !offer.Contains(signatureId)) throw new InvalidOperationException(NotOffered);
        if (cls.IsHero) state.Loadout = new DungeonRunLoadout { Id = cls.Id };
        if (signatureId != null)
        {
            string key = SignatureCard(profile, signatureId);
            var treasure = Data.Treasure(key);
            state.Loadout.Signature.Add(key);
            if (treasure.Active)
            {
                state.ActiveTreasures.Add(key);
                var card = DungeonRunDeck.Add(state, DungeonRunContent.Get(key).ShellId, DungeonRunDeck.OriginSignature);
                ScheduleUpgrade(state, treasure, card);
            }
            else
            {
                state.Treasures.Add(key);
                ScheduleUpgrade(state, treasure, null);
            }
        }
        if (state.Power != null && Data.PowerUpgrades.ContainsKey(state.Power))
            state.Upgrades.Add(new DungeonRunUpgrade { Key = state.Power, SinceFloor = state.Floor, Slot = DungeonRunTreasure.SlotPower });
        state.BoardShrine = BoardShrineFor(state);
    }

    // ------------------------------------------------------------------ board shrines and teammates (phase 7)

    /// <summary>
    /// The board shrine a new run starts with (phase 7): the shrine its starter deck names (run-v1.json deck "shrine"), when
    /// the run is an act run on schedule version 2 or later whose schedule carries the phase-7 drafts (<see cref="HasPhase7Drafts"/>:
    /// acts-v2; acts-v1 and act1 ship unchanged for runs saved on version 1, so a new run on them plays as in phase 6), its
    /// row is single-class (Rikkar's rows are; a dual row never has a shrine), the engine scripts the shrine
    /// (ShrineRules.IsScripted) and the admission table admits it for the run player. Null otherwise: every other run, a run
    /// whose deck's shrine is not scripted yet (it plays as in phase 6), and every run saved before phase 7 (NewRun is the
    /// only writer).
    /// </summary>
    internal string BoardShrineFor(DungeonRunState state)
    {
        if (!state.IsActRun || state.ScheduleVersion < 2 || !HasPhase7Drafts(Data.Schedules?.Find(state.Schedule))) return null;
        string shrine = Data.FindDeck(state.StarterDeck)?.Shrine;
        if (shrine == null || RunClass(state).ClassSet.Count != 1 || !SabberStoneCore.Actions.ShrineRules.IsScripted(shrine)) return null;
        return DungeonRunAdmission.Classify(null, shrine, AdmitScope.Player, null, state.IsCatalogRun).Admitted ? shrine : null;
    }

    /// <summary>
    /// The schedule has a teammate or shrine-upgrade draft (phase 7, scheduleVersion 2 on: acts-v2). A schedule without them
    /// is a released shape kept for older saves; a new run on it gets no board shrine, so it never plays a shrine it can
    /// never upgrade or a run with no teammate.
    /// </summary>
    internal static bool HasPhase7Drafts(DungeonRunSchedule schedule) =>
        schedule != null && schedule.Acts.Any(a => a.TeammateAfter.Length + a.ShrineUpgradeAfter.Length > 0);

    /// <summary>Whether the node's phase-7 drafts may open on this run: a board-shrine run on schedule version 2 or later.</summary>
    internal static bool OffersPhase7Drafts(DungeonRunState state) => state.IsActRun && state.ScheduleVersion >= 2 && state.HasBoardShrine;

    /// <summary>
    /// The shrine upgrades a shrine-upgrade draft may offer (phase 7): the run-v1.json "shrineUpgrades" ids the engine scripts
    /// (DungeonRunShrines.ScriptedUpgrades) and the admission table admits for the run player, minus those the run holds;
    /// ordinal order. Empty on a run with no board shrine.
    /// </summary>
    internal List<string> ShrineUpgradePool(DungeonRunState state)
    {
        if (!state.HasBoardShrine) return new List<string>();
        return Data.ShrineUpgrades.Distinct(StringComparer.Ordinal)
            .Where(id => DungeonRunShrines.ScriptedUpgrades.ContainsKey(id) && !state.ShrineUpgrades.Contains(id)
                && DungeonRunAdmission.Classify(null, id, AdmitScope.Player, null, state.IsCatalogRun).Admitted)
            .OrderBy(id => id, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// The teammates a teammate draft may offer (phase 7): the run class's six (run-v1.json "teammates"; a single-class row's
    /// class) that the engine scripts (DungeonRunShrines.ScriptedTeammates, Implemented), that the admission table admits for
    /// the run player, and that the run has not taken; ordinal order. Empty on a run with no board shrine.
    /// </summary>
    internal List<string> TeammatePool(DungeonRunState state)
    {
        if (!state.HasBoardShrine) return new List<string>();
        var classes = RunClass(state).ClassSet;
        if (classes.Count != 1 || !Data.Teammates.TryGetValue(classes[0], out var ids)) return new List<string>();
        var taken = new HashSet<string>(state.Drafts.Where(d => d.Kind == TeammateKind && d.Picked != null).Select(d => d.Picked), StringComparer.Ordinal);
        return ids.Distinct(StringComparer.Ordinal).Where(id => DungeonRunShrines.ScriptedTeammates.Contains(id) && !taken.Contains(id)
                && (Cards.FromId(id)?.Implemented ?? false)
                && DungeonRunAdmission.Classify(null, id, AdmitScope.Player, null, state.IsCatalogRun).Admitted)
            .OrderBy(id => id, StringComparer.Ordinal).ToList();
    }

    // Up to Phase7Options of the pool, drawn from the run seed (the floor is the component) and stored in ordinal order.
    private static List<string> DrawOffer(List<string> pool, long seed, long stream, int floor)
    {
        var shuffled = new List<string>(pool);
        Shuffle(shuffled, seed, stream, floor);
        return shuffled.Take(Phase7Options).OrderBy(id => id, StringComparer.Ordinal).ToList();
    }

    /// <summary>Opens a shrine-upgrade draft (phase 7); false when nothing is on offer.</summary>
    internal bool OpenShrineUpgradeDraft(DungeonRunState state)
    {
        var pool = ShrineUpgradePool(state);
        if (pool.Count == 0) return false;
        state.Drafts.Add(new DungeonRunDraft { Floor = state.Floor, Kind = ShrineUpgradeKind, Offered = DrawOffer(pool, state.Seed, ShrineUpgradeStream, state.Floor) });
        state.Phase = DungeonRunPhase.TreasureDraft;
        return true;
    }

    /// <summary>Opens a teammate draft (phase 7); false when nothing is on offer or the deck is full (deckMax).</summary>
    internal bool OpenTeammateDraft(DungeonRunState state)
    {
        if (state.Deck.Count + 1 > DeckMax(state)) return false;
        var pool = TeammatePool(state);
        if (pool.Count == 0) return false;
        state.Drafts.Add(new DungeonRunDraft { Floor = state.Floor, Kind = TeammateKind, Offered = DrawOffer(pool, state.Seed, TeammateStream, state.Floor) });
        state.Phase = DungeonRunPhase.TreasureDraft;
        return true;
    }

    // After a fight's treasure draft (taken or not offered): the node's shrine-upgrade draft, then its teammate draft (each
    // once per floor), then the bundle draft. A run without a board shrine goes straight to the bundle draft, as before.
    private void OpenRewardsOrBundle(DungeonRunState state)
    {
        if (OffersPhase7Drafts(state))
        {
            var node = CurrentNode(state);
            bool Opened(string kind) => state.Drafts.Any(d => d.Kind == kind && d.Floor == state.Floor);
            if (node.IsFight && node.ShrineUpgradeDraft && !Opened(ShrineUpgradeKind) && OpenShrineUpgradeDraft(state)) return;
            if (node.IsFight && node.TeammateDraft && !Opened(TeammateKind) && OpenTeammateDraft(state)) return;
        }
        OpenBundleDraftOrAdvance(state);
    }

    /// <summary>
    /// A generated starter (phase 6, the Heist DALA_80x and Tombs ULDA_90x "Random" decks): <see cref="DungeonRunRules.StarterSize"/>
    /// distinct cards from <see cref="GeneratorPool"/>, each class's pool in ordinal order and seeded-shuffled on
    /// <see cref="GeneratorStream"/>; "random-dual" takes half from each of the row's two classes (the primary first). Throws
    /// <see cref="InvalidOperationException"/> when a pool is too small (the validator checks it at load).
    /// </summary>
    internal List<string> GenerateStarter(DungeonRunClass cls, DungeonRunStarterDeck deck, long seed)
    {
        int size = Rules.StarterSize;
        var classes = deck.Generator == DungeonRunStarterDeck.GeneratorRandomDual ? cls.ClassSet.ToList() : new List<string> { cls.ClassSet[0] };
        var cards = new List<string>();
        for (int c = 0; c < classes.Count; c++)
        {
            int take = c == classes.Count - 1 ? size - cards.Count : size / classes.Count;
            var pool = GeneratorPool(cls, classes[c]).Where(id => !cards.Contains(id)).ToList();
            if (pool.Count < take) throw new InvalidOperationException($"The {deck.Id} generator has {pool.Count} {classes[c]} cards, needs {take}.");
            Shuffle(pool, seed, GeneratorStream, 100 * c);
            cards.AddRange(pool.Take(take));
        }
        return cards;
    }

    /// <summary>
    /// What a generated starter of <paramref name="cls"/> draws its <paramref name="cardClass"/> cards from: the candidates of the
    /// catalog's offerable pool entries (grants excluded) and the cards of its fixed entries whose classes include
    /// <paramref name="cardClass"/>, of that card class (no neutral card), passing the drafted-card checks for the row's classes
    /// and carrying no rune cost; distinct, ordinal order. Empty without a catalog.
    /// </summary>
    internal List<string> GeneratorPool(DungeonRunClass cls, string cardClass) => DungeonRunValidator.GeneratorPool(Data, cls, cardClass);

    /// <summary>Design F2: per band, sort by id, seeded shuffle, take the band's floors; then one final boss.</summary>
    internal List<string> BossSequence(DungeonRunProfile profile, long seed)
    {
        var sequence = new List<string>();
        for (int band = 1; band <= Rules.BandSplit.Length; band++)
        {
            var pool = Data.Bosses.Where(b => !b.Final && b.Band == band && IsUnlocked(profile, b.Unlock)).Select(b => b.Id).OrderBy(id => id, StringComparer.Ordinal).ToList();
            if (pool.Count < Rules.BandSplit[band - 1]) throw new InvalidDataException($"Band {band} needs {Rules.BandSplit[band - 1]} bosses, has {pool.Count}.");
            Shuffle(pool, seed, BossStream, band);
            sequence.AddRange(pool.Take(Rules.BandSplit[band - 1]));
        }
        var finals = Data.Bosses.Where(b => b.Final && IsUnlocked(profile, b.Unlock)).Select(b => b.Id).OrderBy(id => id, StringComparer.Ordinal).ToList();
        if (finals.Count == 0) throw new InvalidDataException("No final boss.");
        sequence.Add(finals[StableSeed.Index(StableSeed.Derive(seed, BossStream, 99), finals.Count)]);
        return sequence;
    }

    /// <summary>
    /// An act run's bosses, one per combat floor, pre-rolled at NewRun (indexed by Floor - 1 like the legacy sequence). For each
    /// act in order: each combat slot takes the unlocked bosses of its pool not drawn yet, sorted by id, seeded-shuffled on
    /// <see cref="ActBossStream"/> component act * 10 + slot, and keeps "count"; then the major is one unlocked unused boss
    /// of the major pool (component act * 10 + 9). Pool "leftover" is every band-pool boss (band1, band2, band3) not drawn so
    /// far. A challenge final needs its unlock, as on a legacy run. No boss repeats in a run.
    /// </summary>
    /// <param name="ledger">The run reads the profile's persistent boss damage (a random-seed run): a major pool prefers the
    /// boss with the most banked damage. Only a provenance-mix schedule reads it.</param>
    /// <param name="excludeShrineClass">The class of the run's board shrine (wave 4 review): a boss row with a shrine variant of
    /// that class is never drawn (a Rumble Run champion meets only runs that did not choose its class's shrines). Null: no filter,
    /// every run without a board shrine draws exactly as before.</param>
    /// <param name="excludeBosses">Boss rows the run's hero row excludes (wave 5: the same character, DungeonRunClass.ExcludeBosses):
    /// never drawn, as if already used. Null or empty: every other run draws exactly as before.</param>
    internal List<string> ActBossSequence(DungeonRunProfile profile, long seed, DungeonRunSchedule schedule, bool ledger = false,
        CardClass? excludeShrineClass = null, IEnumerable<string> excludeBosses = null)
    {
        var sequence = new List<string>();
        var used = new HashSet<string>(StringComparer.Ordinal);
        if (excludeBosses != null) used.UnionWith(excludeBosses);
        // Phase 8: how many bosses of each provenance the run holds so far (the provenance-mix draw evens them out).
        var drawn = new Dictionary<string, int>(StringComparer.Ordinal);
        // A finale's fixed bosses are never drawn by an earlier act.
        foreach (var finaleAct in schedule.Acts.Where(x => x.IsFinale)) used.UnionWith(finaleAct.FixedBosses);
        for (int a = 0; a < schedule.Acts.Count; a++)
        {
            var act = schedule.Acts[a];
            int actNo = a + 1;
            if (act.IsFinale)
            {
                // A finale act: its fixed bosses in order (the validator checks each exists; an unlock never hides one).
                foreach (string id in act.FixedBosses)
                {
                    if (Data.Bosses.All(b => b.Id != id)) throw new InvalidDataException($"Schedule {schedule.Id} finale: unknown boss {id}.");
                    sequence.Add(id);
                }
                continue;
            }
            if (schedule.MixesProvenance)
            {
                for (int slot = 0; slot < act.Combat.Count; slot++)
                    foreach (string id in MixedDraw(profile, seed, schedule, act, actNo * 10 + slot, act.Combat[slot].Pool, act.Combat[slot].Count, used, drawn, null,
                        excludeShrineClass))
                        sequence.Add(id);
                sequence.AddRange(MixedDraw(profile, seed, schedule, act, actNo * 10 + 9, act.MajorPool, 1, used, drawn, ledger ? profile : null, excludeShrineClass));
                continue;
            }
            for (int slot = 0; slot < act.Combat.Count; slot++)
            {
                var pool = PoolCandidates(profile, act.Combat[slot].Pool, used, excludeShrineClass);
                if (pool.Count < act.Combat[slot].Count)
                    throw new InvalidDataException($"Schedule {schedule.Id} act {act.Id}: pool {act.Combat[slot].Pool} needs {act.Combat[slot].Count} bosses, has {pool.Count}.");
                Shuffle(pool, seed, ActBossStream, actNo * 10 + slot);
                foreach (string id in pool.Take(act.Combat[slot].Count)) { sequence.Add(id); used.Add(id); }
            }
            var majors = PoolCandidates(profile, act.MajorPool, used, excludeShrineClass);
            if (majors.Count == 0) throw new InvalidDataException($"Schedule {schedule.Id} act {act.Id}: no boss left in major pool {act.MajorPool}.");
            string major = majors[StableSeed.Index(StableSeed.Derive(seed, ActBossStream, actNo * 10 + 9), majors.Count)];
            sequence.Add(major); used.Add(major);
        }
        return sequence;
    }

    /// <summary>
    /// The unlocked bosses of pool <paramref name="pool"/> (a pool, or a schedule pool set: DungeonRunSchedules.Member) not in
    /// <paramref name="used"/>, in ordinal id order.
    /// </summary>
    internal List<string> PoolCandidates(DungeonRunProfile profile, string pool, ICollection<string> used, CardClass? excludeShrineClass = null) =>
        Data.Bosses.Where(b => IsUnlocked(profile, b.Unlock) && !used.Contains(b.Id) && (Data.Schedules?.Member(b, pool) ?? InPool(b, pool))
                && !(excludeShrineClass != null && HasShrineOfClass(b, excludeShrineClass.Value)))
            .Select(b => b.Id).OrderBy(id => id, StringComparer.Ordinal).ToList();

    /// <summary>The boss row has a shrine variant (flag shrine) whose board shrine is of <paramref name="cls"/> (a Rumble Run
    /// champion of that class).</summary>
    internal static bool HasShrineOfClass(DungeonRunBoss boss, CardClass cls) =>
        boss.HasFlag(DungeonRunEncounterData.ShrineFlag) && boss.Variants.Any(v => v.Shrine != null && Cards.FromId(v.Shrine)?.Class == cls);

    /// <summary>
    /// The provenance-mix draw of one slot (phase 8, schedule "draw": "provenance-mix"): <paramref name="count"/> bosses of the
    /// pool, one at a time. Each pick takes the provenance the run holds fewest bosses of so far (ties: the slot's seeded order
    /// of the provenances, <see cref="EncounterStream"/> component, index 100), then a seeded boss of it (index = the pick's
    /// number in the slot). So every act mixes its sources and a run uses each about equally; the boss's own power, deck, start
    /// block, forms and shrine travel with its row wherever it lands. A major slot of a run that reads the ledger
    /// (<paramref name="ledgerProfile"/>) takes the candidate with the most banked persistent damage first.
    /// </summary>
    private List<string> MixedDraw(DungeonRunProfile profile, long seed, DungeonRunSchedule schedule, DungeonRunAct act, long component, string pool,
        int count, HashSet<string> used, Dictionary<string, int> drawn, DungeonRunProfile ledgerProfile, CardClass? excludeShrineClass = null)
    {
        var picks = new List<string>();
        var candidates = PoolCandidates(profile, pool, used, excludeShrineClass);
        if (candidates.Count < count)
            throw new InvalidDataException($"Schedule {schedule.Id} act {act.Id}: pool {pool} needs {count} bosses, has {candidates.Count}.");
        var order = candidates.Select(id => Data.Boss(id).Provenance).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList();
        Shuffle(order, seed, EncounterStream, component * 1000 + 100);
        void Take(string id)
        {
            string provenance = Data.Boss(id).Provenance;
            picks.Add(id); used.Add(id); candidates.Remove(id);
            drawn[provenance] = (drawn.TryGetValue(provenance, out int n) ? n : 0) + 1;
        }
        if (ledgerProfile != null)
        {
            var banked = candidates.Where(id => Data.Boss(id).HasFlag(DungeonRunEncounterData.PersistentDamage) && ledgerProfile.DamageOf(id) != null)
                .OrderBy(id => ledgerProfile.DamageOf(id).Remaining).ThenBy(id => id, StringComparer.Ordinal).FirstOrDefault();
            if (banked != null && picks.Count < count) Take(banked);
        }
        while (picks.Count < count)
        {
            string provenance = order.Where(x => candidates.Any(id => Data.Boss(id).Provenance == x))
                .OrderBy(x => drawn.TryGetValue(x, out int n) ? n : 0).ThenBy(x => order.IndexOf(x)).First();
            var group = candidates.Where(id => Data.Boss(id).Provenance == provenance).ToList();
            Take(group[StableSeed.Index(StableSeed.Derive(seed, EncounterStream, component * 1000 + picks.Count), group.Count)]);
        }
        return picks;
    }

    /// <summary>The boss is a member of <paramref name="pool"/> ("leftover": any band pool, never a final).</summary>
    internal static bool InPool(DungeonRunBoss boss, string pool) => DungeonRunSchedules.InPool(boss, pool);

    // ------------------------------------------------------------------ act schedule accessors

    /// <summary>
    /// An act run's schedule; null for a legacy run. The run is pinned to the schedule version it began on: its Node, Floor and
    /// boss sequence index into that version's nodes, so a run whose ScheduleVersion is not the loaded one, whose schedule the
    /// data no longer has, or whose boss sequence does not hold one boss per floor is refused (a data error, shown on the run
    /// page; the save is never rewritten). A later scheduleVersion must keep the older versions loadable, as the catalog
    /// keeps CatalogVersion 0; the validator pins each released version's node shape (DungeonRunSchedules.ReleasedShapes).
    /// </summary>
    internal DungeonRunSchedule ScheduleFor(DungeonRunState state)
    {
        if (!state.IsActRun) return null;
        // Phase 7: a run on an older released version still loads when that version shipped its schedule (the validator keeps
        // every released version's shapes: DungeonRunSchedules.ReleasedShapes), e.g. a version-1 run on acts-v1 under version 2.
        bool older = Data.Schedules != null && state.ScheduleVersion >= DungeonRunSchedules.OldestScheduleVersion
            && state.ScheduleVersion < Data.Schedules.ScheduleVersion
            && DungeonRunSchedules.ReleasedShapes.TryGetValue(state.ScheduleVersion, out var released) && state.Schedule != null && released.ContainsKey(state.Schedule);
        if (Data.Schedules == null || state.ScheduleVersion != Data.Schedules.ScheduleVersion && !older)
            throw new InvalidDataException("Dungeon Run save: the run is on schedule version " + state.ScheduleVersion + "; this build has "
                + (Data.Schedules == null ? "no act schedules" : "version " + Data.Schedules.ScheduleVersion) + ".");
        var schedule = Data.Schedules.Find(state.Schedule)
            ?? throw new InvalidDataException("Dungeon Run save: the run's schedule " + (state.Schedule ?? "(none)") + " is not in the loaded data.");
        if (state.BossSequence == null || state.BossSequence.Count != schedule.FloorCount)
            throw new InvalidDataException("Dungeon Run save: the run's boss sequence holds " + (state.BossSequence?.Count ?? 0) + " bosses; schedule "
                + schedule.Id + " has " + schedule.FloorCount + " floors.");
        return schedule;
    }

    /// <summary>An act run's current node; null for a legacy run. Its floor must be the run's Floor (else the save is refused).</summary>
    internal DungeonRunNode CurrentNode(DungeonRunState state)
    {
        var node = NodeAt(state);
        if (node != null && node.Floor != state.Floor)
            throw new InvalidDataException("Dungeon Run save: node " + state.Node + " is on floor " + node.Floor + ", the run on floor " + state.Floor + ".");
        return node;
    }

    // The run's node by index only (EnterNode reads it before it sets Floor).
    private DungeonRunNode NodeAt(DungeonRunState state)
    {
        var schedule = ScheduleFor(state);
        if (schedule == null) return null;
        if (state.Node < 0 || state.Node >= schedule.Nodes.Count) throw new InvalidDataException("Dungeon Run save: node " + state.Node + " is outside the schedule.");
        return schedule.Nodes[state.Node];
    }

    /// <summary>An act run's current act; null for a legacy run.</summary>
    internal DungeonRunAct CurrentAct(DungeonRunState state)
    {
        var node = CurrentNode(state);
        return node == null ? null : ScheduleFor(state).Acts[node.Act - 1];
    }

    /// <summary>The modifiers active on the current act (in the order they were taken); empty for a legacy run.</summary>
    internal List<DungeonRunModifier> ActiveModifiers(DungeonRunState state)
    {
        var node = CurrentNode(state);
        if (node == null || Data.Schedules == null) return new List<DungeonRunModifier>();
        return state.ActiveRules.Where(r => r.Act == node.Act).Select(r => Data.Schedules.Modifier(r.Id)).Where(m => m != null).ToList();
    }

    /// <summary>The run deck size the match accepts: the schedule's deckMax, or <see cref="LegacyDeckMax"/> on a legacy run.</summary>
    internal int DeckMax(DungeonRunState state) => ScheduleFor(state)?.DeckMax ?? LegacyDeckMax;

    /// <summary>The smallest run deck: the schedule's deckMin, or the starter size on a legacy run.</summary>
    internal int DeckMin(DungeonRunState state) => ScheduleFor(state)?.DeckMin ?? Rules.StarterSize;

    /// <summary>
    /// The current floor's boss Health. Legacy: <see cref="BossHealth(DungeonRunBoss)"/>. Act run: the act curve at the floor in
    /// act plus the boss's own Health offset (<see cref="HealthOffset"/>: Pit Champion's +10 over its band keeps it apart from
    /// its peers in whatever slot it lands), or the boss's own Health when it has "fixedHealth"; then the active modifiers'
    /// bossHealthDelta.
    /// </summary>
    internal int BossHealth(DungeonRunState state)
    {
        var boss = CurrentBoss(state);
        if (!state.IsActRun) return BossHealth(boss);
        var node = CurrentNode(state);
        var act = CurrentAct(state);
        // Phase 8: Health that is part of an encounter's identity (a Plague Lord's 300, Hagatha's 100) and a phased boss's
        // Health ignore the modifiers too (bossHealthDelta would move its form thresholds).
        if (boss.IsEncounter && boss.FixedHealth && boss.Health is int identity) return identity;
        int health = boss.FixedHealth && boss.Health is int own ? own : act.BossHealth[node.FloorInAct - 1] + HealthOffset(boss);
        return Math.Max(1, health + ActiveModifiers(state).Sum(m => m.BossHealthDelta));
    }

    /// <summary>
    /// How far a boss's own Health ("health") is from its band's default (rules bandHealth, or finalHealth for a final):
    /// Pit Champion 50 against band 2's 40 is +10, Hollow Sovereign 70 against the finals' 60 is +10; 0 without a "health".
    /// </summary>
    /// <remarks>Phase 8: an imported encounter has no band; it carries its offset itself ("healthOffset", 0 by default).</remarks>
    internal int HealthOffset(DungeonRunBoss boss) =>
        boss.IsEncounter ? boss.HealthOffset
        : boss.Health is int own ? own - (boss.Final ? Rules.FinalHealth : Rules.BandHealth[Math.Max(1, Math.Min(boss.Band, Rules.BandHealth.Length)) - 1]) : 0;

    /// <summary>
    /// The boss's start-mana bonus this floor (RunMatchPlan.BossStartManaBonus, phase 5 engine side): the act curve plus the
    /// modifiers' bossManaDelta, capped by the boss's bossManaCap (default <see cref="DungeonRunSchedules.DefaultBossManaCap"/>).
    /// 0 on a legacy run.
    /// </summary>
    internal int BossStartManaBonus(DungeonRunState state)
    {
        if (!state.IsActRun) return 0;
        var node = CurrentNode(state);
        // Phase 8: an encounter's own start Mana Crystals ("start.bossMana": Hagatha's 2) replace the act curve and the modifiers.
        var boss = CurrentBoss(state);
        int bonus = boss.Start?.BossMana ?? CurrentAct(state).BossMana[node.FloorInAct - 1] + ActiveModifiers(state).Sum(m => m.BossManaDelta);
        return Math.Max(0, Math.Min(bonus, boss.BossManaCap ?? DungeonRunSchedules.DefaultBossManaCap));
    }

    /// <summary>Extra player hero Health this floor: the run's HealthBonus plus the modifiers' playerHealthDelta (0 on a legacy run).</summary>
    internal int PlayerHealthBonus(DungeonRunState state) =>
        state.IsActRun ? state.HealthBonus + ActiveModifiers(state).Sum(m => m.PlayerHealthDelta) : 0;

    /// <summary>Extra opening-hand cards this floor from the modifiers (0 on a legacy run).</summary>
    internal int OpeningHandBonus(DungeonRunState state) => state.IsActRun ? ActiveModifiers(state).Sum(m => m.OpeningHandBonus) : 0;

    /// <summary>Bundle offers per draft: 3 on a legacy run; the act's draft options plus draftOptionsDelta, 1 to 3, on an act run.</summary>
    internal int DraftOptions(DungeonRunState state)
    {
        var act = CurrentAct(state);
        if (act == null) return 3;
        return Math.Max(1, Math.Min(3, act.DraftOptions + ActiveModifiers(state).Sum(m => m.DraftOptionsDelta)));
    }

    /// <summary>Cards one bundle pick keeps: 3 on a legacy run; the act's draft takes (1 to 3) on an act run.</summary>
    internal int DraftTakes(DungeonRunState state) => Math.Max(1, Math.Min(3, CurrentAct(state)?.DraftTakes ?? 3));

    /// <summary>
    /// The current tavern's gold: the act's tavern gold plus tavernGoldDelta (0 on a legacy run), plus
    /// <see cref="VipExtraGold"/> while the run holds ULDA_011 VIP Membership (<see cref="HasVip"/>).
    /// </summary>
    internal int TavernGold(DungeonRunState state)
    {
        var act = CurrentAct(state);
        return act == null ? 0 : Math.Max(0, act.TavernGold + ActiveModifiers(state).Sum(m => m.TavernGoldDelta)) + (HasVip(state) ? VipExtraGold : 0);
    }

    /// <summary>
    /// ULDA_011 VIP Membership ("Taverns you visit are upgraded!", phase 6 F13): a run-layer flag, no match effect. The client
    /// data does not say what an upgraded tavern is (the original was server-side); here an upgraded tavern gives
    /// <see cref="VipExtraGold"/> more gold and offers <see cref="VipExtraOffers"/> more option (an adaptation recorded in the
    /// catalog curation's "adaptations"). Read by <see cref="TavernGold"/> and DungeonRunTavernNode.Open.
    /// </summary>
    internal static bool HasVip(DungeonRunState state) => state.Treasures.Contains(DungeonRunContent.VipMembership);
    internal const int VipExtraGold = 1, VipExtraOffers = 1;

    /// <summary>The options the current tavern offers: the act's tavern offers, plus <see cref="VipExtraOffers"/> with VIP Membership.</summary>
    internal int TavernOffers(DungeonRunState state)
    {
        var act = CurrentAct(state);
        return act == null ? 0 : act.TavernOffers + (HasVip(state) ? VipExtraOffers : 0);
    }

    // ------------------------------------------------------------------ matches

    internal DungeonRunBoss CurrentBoss(DungeonRunState state) => Data.Boss(state.BossSequence[state.Floor - 1]);

    /// <summary>The engine seed for the current floor. A restaged floor keeps it (design R11).</summary>
    internal int MatchSeed(DungeonRunState state) => StableSeed.Derive(state.Seed, MatchStream, state.Floor) & int.MaxValue;

    /// <summary>Design F3.</summary>
    internal int BossHealth(DungeonRunBoss boss) =>
        boss.Health ?? (boss.Final ? Rules.FinalHealth : Rules.BandHealth[Math.Min(boss.Band, Rules.BandHealth.Length) - 1]);

    /// <summary>
    /// The player's hero power for this floor: the chosen power (or the power a won fight put in its place for the rest of the
    /// run, <see cref="DungeonRunState.PowerOverride"/>), or (a version-1 Pilgrim run) the shrine at this floor's tier.
    /// </summary>
    internal string PlayerPower(DungeonRunState state) =>
        state.Shrine != null ? $"{state.Shrine}_{Rules.ShrineTier(state.Floor)}" : state.PowerOverride ?? state.Power;

    /// <summary>
    /// Bosses the run has defeated before the current fight (RunContext.BossesDefeated): an act run's won fights; a legacy
    /// run's Floor - 1 (every earlier floor was won). No save field.
    /// </summary>
    internal static int BossesDefeated(DungeonRunState state) =>
        state.IsActRun ? state.Matches.Count(m => m.Outcome == DungeonRunOutcome.Win && !m.NoLoss) : Math.Max(0, state.Floor - 1);

    /// <summary>
    /// The active treasures a treasure-Discover may offer this run (RunContext.TreasurePool, design H2): every offered,
    /// non-legacy, non-signature active treasure in the run's scope whose content shell is scripted, as shell card ids, ordinal
    /// order. <paramref name="pool"/> narrows it to one adventure tag (DungeonRunTreasure.Pool).
    /// </summary>
    internal List<string> TreasurePool(DungeonRunState state, string pool = null)
    {
        var cls = RunClass(state);
        var ids = new List<string>();
        foreach (var t in Data.Treasures.Where(t => t.Active && t.Offered && !t.Legacy && !t.Signature && (pool == null || t.Pool == pool)
            && t.Scope.Matches(cls.ClassSet, state.StarterDeck, state.Power) && TreasureOfferedOn(t, state)))
        {
            if (!DungeonRunContent.TryGet(t.Id, out var content) || content.ShellId == null) continue;
            SabberStoneCore.Model.Card card;
            try { card = Cards.FromId(content.ShellId); } catch { continue; }
            if (card != null && card.Implemented) ids.Add(content.ShellId);
        }
        return ids.Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToList();
    }

    /// <summary>The run facts the match's card scripts read (RunHelpers): bosses defeated, slot logs, the treasure pool, the next-fight bonus.</summary>
    internal RunContext BuildContext(DungeonRunState state) => new RunContext(
        BossesDefeated(state),
        state.IsActRun ? state.Cards.Select(c => (IEnumerable<string>)(c.Log ?? new List<string>())) : null,
        TreasurePool(state),
        NextFightOf(state),
        state.Deck,
        state.IsActRun ? state.Cards.Select(c => c.Origin) : null,
        TreasurePoolTags(state),
        RunClassesOf(state));

    /// <summary>
    /// The run row's classes as engine classes, primary first (RunContext.PlayerClasses): a dual row's class Discovers
    /// (ULDA_301, ULDA_303) offer both. Unknown names are dropped.
    /// </summary>
    internal IEnumerable<SabberStoneCore.Enums.CardClass> RunClassesOf(DungeonRunState state)
    {
        var cls = RunClass(state);
        if (cls == null) yield break;
        foreach (var name in cls.ClassSet)
            if (Enum.TryParse(name, out SabberStoneCore.Enums.CardClass c) && c != SabberStoneCore.Enums.CardClass.INVALID) yield return c;
    }

    /// <summary>The adventure tag (DungeonRunTreasure.Pool) of each <see cref="TreasurePool"/> id that has one (RunContext.TreasurePoolTags).</summary>
    internal List<KeyValuePair<string, string>> TreasurePoolTags(DungeonRunState state)
    {
        var pool = new HashSet<string>(TreasurePool(state), StringComparer.Ordinal);
        var tags = new List<KeyValuePair<string, string>>();
        foreach (var t in Data.Treasures.Where(t => t.Pool != null && t.Active && t.Offered && !t.Legacy && !t.Signature))
            if (DungeonRunContent.TryGet(t.Id, out var content) && content.ShellId != null && pool.Contains(content.ShellId)
                && tags.All(p => p.Key != content.ShellId))
                tags.Add(new KeyValuePair<string, string>(content.ShellId, t.Pool));
        return tags.OrderBy(p => p.Key, StringComparer.Ordinal).ToList();
    }

    private static RunNextFight NextFightOf(DungeonRunState state) =>
        state.NextFight == null || state.NextFight.Mana <= 0 && state.NextFight.Draws <= 0 ? null
            : new RunNextFight(Math.Max(0, state.NextFight.Mana), Math.Max(0, state.NextFight.Draws));

    /// <param name="profile">Phase 8: the profile whose persistent boss damage a random-seed run reads (null: no ledger, as in
    /// every call before phase 8). Only a boss with flag "persistentDamage" reads it.</param>
    internal RunMatchPlan BuildPlan(DungeonRunState state, DungeonRunProfile profile = null)
    {
        if (state.Phase != DungeonRunPhase.PreMatch) throw new InvalidOperationException("A match starts only from pre-match.");
        var cls = RunClass(state);
        var boss = CurrentBoss(state);
        if (state.IsActRun && (state.Deck.Count < DeckMin(state) || state.Deck.Count > DeckMax(state)))
            throw new InvalidOperationException($"The run deck holds {state.Deck.Count} cards; the schedule allows {DeckMin(state)} to {DeckMax(state)}.");
        var plan = BuildBasePlan(state, cls, boss);
        if (boss.IsEncounter || state.ActiveRules.Any(r => r.Kind != null)) ApplyEncounter(state, profile, boss, plan);
        return plan;
    }

    // The plan of every run before phase 8, unchanged; ApplyEncounter adds an imported encounter's fields and the global rules.
    private RunMatchPlan BuildBasePlan(DungeonRunState state, DungeonRunClass cls, DungeonRunBoss boss)
    {
        // The act-run fields come from the pure accessors and are 0/empty on a legacy run, which then plays exactly as before:
        // the per-card records (enchantments, opening flags), the boss's start mana, extra hero Health and opening cards.
        return new RunMatchPlan
        {
            PlayerHero = Cards.FromId(cls.HeroCard),
            // LOOTA_802 Justicar's Ring plays the power's upgraded form (DungeonRunContent.HeroPowerUpgrades); every other run plays it as chosen.
            PlayerHeroPowerId = DungeonRunSetup.PlanPower(PlayerPower(state), state.Treasures),
            PlayerTreasures = new List<string>(state.Treasures),
            PlayerDeck = state.Deck.Select(Cards.FromId).ToList(),
            BossHero = Cards.FromId(boss.HeroCard),
            BossHeroPowerId = boss.Power,
            BossHealth = BossHealth(state),
            BossDeck = Data.BossDecks[boss.Deck].Select(Cards.FromId).ToList(),
            BossSideboards = Data.BossSideboards.TryGetValue(boss.Deck, out var boards) ? boards : null,
            // Generation never yields a card the admission table rejects in the Generated scope; a rejection that is only a
            // boss AI limit (the card is admitted for the run player) applies to the boss's generation alone.
            // A catalog run's admission lifts apply to generation too (DungeonRunAdmission.CatalogAdmit); a legacy run's lists
            // are the pre-catalog ones.
            ExcludedGenerated = DungeonRunAdmission.GeneratedRejects(state.IsCatalogRun),
            ExcludedGeneratedBoss = DungeonRunAdmission.GeneratedRejectsBoss(state.IsCatalogRun),
            // A catalog run's player may generate Secrets (the boss's prior covers them: DungeonRunAdmission.CatalogSecretPrior).
            PlayerGeneratedSecrets = state.IsCatalogRun,
            PlayerDeckSlots = DungeonRunDeck.PlanSlots(state),
            BossStartManaBonus = BossStartManaBonus(state),
            PlayerHealthBonus = PlayerHealthBonus(state),
            PlayerOpeningHandBonus = OpeningHandBonus(state),
            // Phase 6: a named hero's run Health, the run facts scripts read, and the one-fight bonus a won fight granted.
            PlayerHeroHealth = cls.Health > 0 ? cls.Health : (int?)null,
            RunContext = BuildContext(state),
            NextFight = NextFightOf(state),
            // Phase 7: the board shrine (Start of Game), its copies (TRLA_804) and its upgrades; none on every other run.
            PlayerShrine = state.BoardShrine,
            PlayerShrineCopies = state.BoardShrine != null ? DungeonRunShrines.Copies(state.ShrineUpgrades) : 1,
            PlayerShrineUpgrades = state.BoardShrine != null ? new List<string>(state.ShrineUpgrades) : new List<string>(),
        };
    }

    /// <summary>The run's classes: one for a single-class row (and every legacy run), two for a dual-class row (primary first).</summary>
    internal IReadOnlyList<string> RunClasses(DungeonRunState state) => RunClass(state).ClassSet;

    /// <summary>
    /// Phase 8 family 06: the classes whose cards the run is offered as loot and may hold: the run row's classes, and while
    /// ULDA_729 Multiclassing is active every class of the data after them (the class rows, in data order). The card drafts
    /// (<see cref="OpenCatalogDraft"/>), a deck copy's class check and the boss's Secret prior (<see cref="MatchClasses"/>) read
    /// it; the match's class Discovers, the tavern pools and the treasure pool keep the run's own classes.
    /// </summary>
    internal IReadOnlyList<string> LootClasses(DungeonRunState state)
    {
        var own = RunClasses(state);
        if (!state.IsActRun || state.ActiveRules.All(r => r.Id != Multiclassing) || !RuleActive(state, Multiclassing)) return own;
        var all = new List<string>(own);
        foreach (string name in Data.ClassRows.SelectMany(c => c.ClassSet))
            if (!all.Contains(name)) all.Add(name);
        return all;
    }

    /// <summary>The run's class; a version-1 run must be migrated first (DungeonRunMigration).</summary>
    internal DungeonRunClass RunClass(DungeonRunState state) =>
        Data.FindClass(state.Class) ?? throw new InvalidDataException("The run has no known class (" + (state.Class ?? state.Hero ?? "none") + ").");

    internal void BeginMatch(DungeonRunState state)
    {
        if (state.Phase != DungeonRunPhase.PreMatch) throw new InvalidOperationException("Not at pre-match.");
        state.Phase = DungeonRunPhase.InMatch;
        state.Attempt++;
        // Phase 8: the fight-only heroes of this floor (null on every fight without hero cards or a relay).
        state.HeroSwitch = HeroSwitchFor(state);
    }

    /// <summary>A run loaded mid-match resumes at that floor's pre-match with the same seed (design R11, E6).</summary>
    internal void Resume(DungeonRunState state)
    {
        // An unfinished act run must still fit the loaded schedule (ScheduleFor, CurrentNode): a run of another schedule version
        // or a damaged node is refused here, at load, with the reason, and is never rewritten.
        if (state.IsActRun && !state.Finished) CurrentNode(state);
        if (state.Phase == DungeonRunPhase.InMatch) state.Phase = DungeonRunPhase.PreMatch;
    }

    /// <summary>
    /// Design R10. A tie, concede or turn limit is a loss; a fault restages the floor. <paramref name="result"/> is what the
    /// match asked the run to keep (phase 6, Game.RunResult): applied on a win only, before the upgrades
    /// (<see cref="ApplyMatchResult"/>); a loss drops it. The one-fight bonus the fight began with is spent by any outcome but
    /// a fault. Returns the result parts that were refused (empty when everything applied).
    /// </summary>
    /// <param name="end">Phase 8: what the fight left (DungeonRunSetup.Harvest of the live game): the boss's remaining Health and
    /// form, the run hero's damage, the heroes used, the no-loss flag. Null (every call before phase 8) records none of it.</param>
    internal List<string> RecordOutcome(DungeonRunState state, DungeonRunProfile profile, DungeonRunOutcome outcome, RunMatchResult result = null,
        RunMatchEnd end = null)
    {
        var notes = new List<string>();
        if (state.Phase != DungeonRunPhase.InMatch) throw new InvalidOperationException("No match in progress.");
        // A fight a script ended with no loss (Treasure Vault's "End the game") counts as won, with no boss kill credit.
        // (Family 09: the engine ends such a fight as the run player's win, so a reported Win with the mark is a no-loss ending too.)
        bool noLoss = end != null && end.NoLoss && outcome != DungeonRunOutcome.Fault;
        if (noLoss) outcome = DungeonRunOutcome.Win;
        var summary = new DungeonRunMatchSummary
        {
            Floor = state.Floor, Boss = state.BossSequence[state.Floor - 1], Outcome = outcome, Attempt = state.Attempt,
            Family = state.IsActRun ? Data.Bosses.FirstOrDefault(b => b.Id == state.BossSequence[state.Floor - 1])?.Family : null,
            NoLoss = noLoss,
        };
        state.Matches.Add(summary);
        // A fault restages the floor: nothing else changes (the hero switch of the floor is kept).
        if (outcome == DungeonRunOutcome.Fault) { state.Phase = DungeonRunPhase.PreMatch; return notes; }
        RecordEncounterEnd(state, profile, outcome, end, summary);
        state.NextFight = null;
        if (outcome != DungeonRunOutcome.Win) { Finish(state, profile, won: false); return notes; }

        notes.AddRange(ApplyMatchResult(state, result));
        ApplyUpgrades(state);
        if (state.IsActRun) { RecordActWin(state, profile); return notes; }
        if (state.Floor >= Rules.FinalFloor) { Finish(state, profile, won: true); return notes; }
        if (Rules.TreasureFloors.Contains(state.Floor) && OpenTreasureDraft(state)) return notes;
        OpenBundleDraftOrAdvance(state);
        return notes;
    }

    /// <summary>
    /// A won match's requests (phase 6, design section 3): its run deck edits (all or none, through
    /// DungeonRunDeck.ApplyMatchEdits, so every add and replace passes the run's deck checks), its slot log lines (appended to
    /// the card records by the match plan's slot order), a hero power override and a one-fight bonus for the next fight. A
    /// legacy run has no card records: its edits and logs are refused, the rest applies. A refused part changes nothing and is
    /// returned as a note, so the fight is still recorded. RecordOutcome calls it on a win only.
    /// </summary>
    internal List<string> ApplyMatchResult(DungeonRunState state, RunMatchResult result)
    {
        var notes = new List<string>();
        if (result == null || result.IsEmpty) return notes;
        // Logs first: their slots are the plan's order, which an edit below can change.
        if (result.SlotLogAppends.Count > 0)
        {
            if (!state.IsActRun) notes.Add("slot logs refused: only an act run keeps card records");
            else if (result.SlotLogAppends.Any(l => l.Key < 0 || l.Key >= state.Cards.Count)) notes.Add("slot logs refused: a log names a slot outside the deck");
            else foreach (var line in result.SlotLogAppends) state.Cards[line.Key].Log.Add(line.Value);
        }
        if (result.Edits.Count > 0)
        {
            var edits = result.Edits.Select(e => new RunDeckEdit { Op = e.Op, SlotIndex = e.SlotIndex, NewId = e.CardId }).ToList();
            var applied = DungeonRunDeck.ApplyMatchEdits(this, state, edits);
            if (!applied.Ok) notes.Add("edits refused: " + applied);
        }
        if (result.PowerOverride != null)
        {
            string refused = PowerOverrideError(result.PowerOverride);
            if (refused == null) state.PowerOverride = result.PowerOverride;
            else notes.Add("power override refused: " + refused);
        }
        if (result.NextFight != null && !result.NextFight.IsEmpty)
            state.NextFight = new DungeonRunNextFight { Mana = result.NextFight.Mana, Draws = result.NextFight.Draws };
        return notes;
    }

    /// <summary>
    /// Why a won fight's hero power override (ULDA_306 Stolen Titan Secrets copies the boss's power) cannot be the run player's
    /// power for the rest of the run, or null when it can: it must be a scripted hero power card that the admission table admits
    /// for the run player (a boss power is boss content; one the table rejects for the player never becomes the run's).
    /// </summary>
    internal static string PowerOverrideError(string powerId)
    {
        SabberStoneCore.Model.Card card;
        try { card = Cards.FromId(DungeonRunSetup.PowerCardId(powerId)); } catch { card = null; }
        if (card == null) return powerId + " is not in the card data";
        if (card.Type != SabberStoneCore.Enums.CardType.HERO_POWER) return powerId + " is not a hero power";
        if (!card.Implemented) return powerId + " is not scripted";
        var verdict = DungeonRunAdmission.Classify(card, card.Id, AdmitScope.Player, null);
        return verdict.Admitted ? null : powerId + " is not admitted for the run player (" + verdict.Family + ")";
    }

    // An act run's won fight: a major counts toward MajorsCleared; the schedule's last node ends the run; otherwise the node's
    // treasure draft (or a modifier's extra treasure after a major), then the bundle draft, then the next node.
    private void RecordActWin(DungeonRunState state, DungeonRunProfile profile)
    {
        var schedule = ScheduleFor(state);
        var node = CurrentNode(state);
        bool major = node.Kind == DungeonRunNodeKind.Major;
        if (major) state.MajorsCleared++;
        if (node.Index == schedule.Nodes.Count - 1) { Finish(state, profile, won: true); return; }
        // Phase 8: a finale act's fights follow one another with no treasure, reward or card draft.
        if (schedule.Acts[node.Act - 1].IsFinale) { Advance(state); return; }
        bool treasure = node.TreasureDraft || major && ActiveModifiers(state).Any(m => m.ExtraTreasureAfterMajor);
        // A full deck (deckMax) has no room for an active treasure's card: the draft offers passive treasures only.
        bool full = state.Deck.Count + 1 > DeckMax(state);
        if (treasure && OpenTreasureDraft(state, passiveOnly: node.PassiveOnly || full)) return;
        // Phase 7: a board-shrine run's shrine-upgrade and teammate drafts, then the bundle draft.
        OpenRewardsOrBundle(state);
    }

    /// <summary>Legal outside a match only; counts as a defeat.</summary>
    internal void Abandon(DungeonRunState state, DungeonRunProfile profile)
    {
        if (state.Phase == DungeonRunPhase.InMatch) throw new InvalidOperationException("Finish or concede the match first.");
        if (!state.Finished) Finish(state, profile, won: false);
    }

    /// <summary>
    /// The boss a finished run names as its last: legacy, the boss of the run's floor (as before); act run, the last boss
    /// actually fought (a run abandoned at a tavern or modifier node, whose Floor is the next fight, names the fight before
    /// it), or null when no fight was played.
    /// </summary>
    internal static string LastBoss(DungeonRunState state)
    {
        if (state.IsActRun) return state.Matches.LastOrDefault(m => m.Outcome != DungeonRunOutcome.Fault)?.Boss;
        return state.BossSequence.Count >= state.Floor && state.Floor > 0 ? state.BossSequence[state.Floor - 1] : null;
    }

    private void Finish(DungeonRunState state, DungeonRunProfile profile, bool won)
    {
        state.Phase = won ? DungeonRunPhase.Victory : DungeonRunPhase.Defeat;
        // A run abandoned at a tavern or modifier node keeps no open node.
        state.Tavern = null;
        state.ModifierOffer = null;
        state.HeroSwitch = null;
        // A run whose class the data no longer knows (a damaged or future save) can still end: it is archived, but it adds
        // no stats row, so it cannot feed a class ladder it was never part of.
        var cls = Data.FindClass(state.Class);
        var stats = cls != null ? profile.StatsFor(cls.Id) : null;
        // Stats, personal bests and unlocks count only for random seeds (design R13): a typed seed can be replayed at will,
        // so its runs must not feed the runsWon/floorReached conditions on a later random run either.
        if (!state.TypedSeed && stats != null)
        {
            stats.Runs++;
            if (won) stats.Wins++;
            stats.BestFloor = Math.Max(stats.BestFloor, state.Floor);
            // Act majors beaten (0 on every legacy run, so no legacy unlock changes).
            stats.MajorsCleared += state.MajorsCleared;
            profile.MajorsCleared += state.MajorsCleared;
        }
        profile.Archive.Add(new DungeonRunArchiveEntry
        {
            Class = state.Class, StarterDeck = state.StarterDeck, Power = state.Power ?? state.Shrine, Hero = state.Hero,
            Seed = state.SeedText, TypedSeed = state.TypedSeed, FloorReached = state.Floor, Won = won,
            LastBoss = LastBoss(state), Treasures = new List<string>(state.Treasures.Concat(state.ActiveTreasures)),
            BoardShrine = state.BoardShrine, ShrineUpgrades = new List<string>(state.ShrineUpgrades ?? new List<string>()),
        });
        while (profile.Archive.Count > DungeonRunProfile.ArchiveCap) profile.Archive.RemoveAt(0);
        state.UnlockedThisRun = state.TypedSeed ? new List<string>() : RefreshUnlocks(profile);
    }

    // ------------------------------------------------------------------ treasures (design F6)

    /// <summary>
    /// Opens a treasure draft; false when nothing is on offer. <paramref name="passiveOnly"/> (an act's "passiveAtStart"
    /// draft) offers passive treasures only.
    /// </summary>
    internal bool OpenTreasureDraft(DungeonRunState state, bool passiveOnly = false)
    {
        var cls = RunClass(state);
        var offeredBefore = new HashSet<string>(state.Drafts.Where(d => d.Kind == "treasure").SelectMany(d => d.Offered));
        // Affinity tags: the class's, plus the starter deck's (a migrated version-1 run has no starter deck id).
        // A hero row reads its primary class's tags (plus its own treasureTags) when it has no classTags entry of its own.
        var classTags = new HashSet<string>(Data.TagsOf(cls));
        classTags.UnionWith(Data.FindDeck(state.StarterDeck)?.TreasureTags ?? new List<string>());
        var candidates = Data.Treasures
            .Where(t => t.Offered && !t.Legacy && !t.Signature && !offeredBefore.Contains(t.Id) && !state.Treasures.Contains(t.Id) && !state.ActiveTreasures.Contains(t.Id))
            .Where(t => t.Scope.Matches(cls.ClassSet, state.StarterDeck, state.Power) && TreasureOfferedOn(t, state))
            .Where(t => !passiveOnly || !t.Active)
            .OrderBy(t => t.Id, StringComparer.Ordinal).ToList();
        if (candidates.Count == 0) return false;

        int Weight(DungeonRunTreasure t)
        {
            int rarity = t.Rarity == "rare" ? 30 : 70;
            // A scoped treasure is a candidate only when its scope matched, so it always counts as affine.
            bool affine = !t.Scope.IsEmpty || t.Tags.Any(classTags.Contains);
            return (int)Math.Round(rarity * (100 + 50 * (affine ? 1 : 0)) / 100.0, MidpointRounding.AwayFromZero);
        }
        var offer = new List<string>();
        int draw = 0;
        var universal = candidates.Where(t => t.Universal).ToList();
        if (universal.Count > 0) offer.Add(WeightedPick(universal, Weight, state.Seed, state.Floor, TreasureStep, draw++).Id);
        while (offer.Count < 3)
        {
            var rest = candidates.Where(t => !offer.Contains(t.Id)).ToList();
            if (rest.Count == 0) break;
            offer.Add(WeightedPick(rest, Weight, state.Seed, state.Floor, TreasureStep, draw++).Id);
        }
        state.Drafts.Add(new DungeonRunDraft { Floor = state.Floor, Kind = "treasure", Offered = offer.OrderBy(x => x, StringComparer.Ordinal).ToList() });
        state.Phase = DungeonRunPhase.TreasureDraft;
        return true;
    }

    /// <summary>
    /// Whether <paramref name="state"/>'s run may be offered <paramref name="t"/> (phase 6): its <see cref="DungeonRunTreasure.Runs"/>
    /// fits the run (act or legacy), and the admission table admits it for the run player in this run (a legacy run reads the
    /// legacy verdict, a catalog run the catalog one): LOOTA_833 Mysterious Tome's generated Secrets need a catalog run, whose
    /// boss prior holds them (DungeonRunAdmission.CatalogSecretPrior).
    /// </summary>
    internal static bool TreasureOfferedOn(DungeonRunTreasure t, DungeonRunState state) =>
        t.OfferedOn(state.IsActRun, state.ScheduleVersion >= 3) && DungeonRunAdmission.Classify(null, t.Id, AdmitScope.Player, null, state.IsCatalogRun).Admitted;

    internal void PickTreasure(DungeonRunState state, string treasureId)
    {
        var draft = state.OpenDraft;
        // Phase 7's shrine-upgrade and teammate drafts are picked on the treasure screen too (DungeonRunPhase.TreasureDraft).
        if (draft != null && (draft.Kind == ShrineUpgradeKind || draft.Kind == TeammateKind)) { PickReward(state, treasureId); return; }
        if (state.Phase != DungeonRunPhase.TreasureDraft || draft == null || draft.Kind != TreasureKind || !draft.Offered.Contains(treasureId))
            throw new InvalidOperationException(treasureId + " is not on offer.");
        draft.Picked = treasureId;
        var treasure = Data.Treasure(treasureId);
        if (treasure.Active) AddActiveTreasure(state, treasureId, state.Floor);
        else { state.Treasures.Add(treasureId); ScheduleUpgrade(state, treasure, null); }
        OpenRewardsOrBundle(state);
    }

    /// <summary>
    /// Picks from an open shrine-upgrade draft (the upgrade joins <see cref="DungeonRunState.ShrineUpgrades"/>) or teammate
    /// draft (the teammate joins the deck with origin "teammate", under deckMax), then opens the node's next draft. The pick
    /// takes the stored offer, so a save and resume never redraws it.
    /// </summary>
    internal void PickReward(DungeonRunState state, string id)
    {
        var draft = state.OpenDraft;
        if (state.Phase != DungeonRunPhase.TreasureDraft || draft == null || draft.Kind != ShrineUpgradeKind && draft.Kind != TeammateKind
            || !draft.Offered.Contains(id))
            throw new InvalidOperationException(id + " is not on offer.");
        if (draft.Kind == TeammateKind && state.Deck.Count + 1 > DeckMax(state))
            throw new InvalidOperationException($"The run deck is full ({DeckMax(state)} cards).");
        if (draft.Kind == ShrineUpgradeKind && state.ShrineUpgrades.Contains(id)) throw new InvalidOperationException(id + " is already held.");
        draft.Picked = id;
        if (draft.Kind == ShrineUpgradeKind) state.ShrineUpgrades.Add(id);
        else DungeonRunDeck.Add(state, id, DungeonRunDeck.OriginTeammate);
        OpenRewardsOrBundle(state);
    }

    private void AddActiveTreasure(DungeonRunState state, string key, int floor)
    {
        state.ActiveTreasures.Add(key);
        var card = DungeonRunDeck.Add(state, DungeonRunContent.Get(key).ShellId, DungeonRunDeck.OriginTreasure);
        ScheduleUpgrade(state, Data.FindTreasure(key), card);
    }

    /// <summary>
    /// Records a growing treasure's run upgrade (design section 4 kind B): a treasure with an UpgradesTo that is not a
    /// profile-level (UpgradeUnlock) upgrade. The record names its slot (deck: the card record <paramref name="card"/>, by uid;
    /// passive: the treasure in Treasures). The deck slot of an earlier save is written as no Slot, so a class run's file is
    /// unchanged.
    /// </summary>
    private static void ScheduleUpgrade(DungeonRunState state, DungeonRunTreasure treasure, DungeonRunCard card)
    {
        if (treasure?.UpgradesTo == null || treasure.UpgradeUnlock != null) return;
        string slot = treasure.Active ? null : DungeonRunTreasure.SlotPassive;
        state.Upgrades.Add(new DungeonRunUpgrade { Key = treasure.Id, SinceFloor = state.Floor, Uid = card?.Uid ?? 0, Slot = slot });
    }

    /// <summary>
    /// After a won fight, each growing treasure or power due an upgrade swaps for its next stage (design section 4). When it
    /// is due (<see cref="DungeonRunTreasure.UpgradeOn"/>): "floors" every rules.upgradeEveryFloors floors since its last stage
    /// (the behaviour of every run before phase 6); "actClear" when the fight won is an act's major (never on a legacy run);
    /// "bossWins:N" on its N-th won fight since its last stage (<see cref="DungeonRunUpgrade.Progress"/>). What it swaps
    /// (<see cref="DungeonRunUpgrade.Slot"/>): deck (the card record by uid, a legacy run's first shell by id), power (the
    /// run's Power) or passive (the entry in Treasures). Tavern and modifier nodes win no fight, so they never count.
    /// </summary>
    private void ApplyUpgrades(DungeonRunState state)
    {
        bool majorWon = state.IsActRun && CurrentNode(state)?.Kind == DungeonRunNodeKind.Major;
        foreach (var up in state.Upgrades.ToList())
        {
            string slot = up.Slot ?? DungeonRunTreasure.SlotDeck;
            string on = UpgradeTrigger(up, slot);
            int wins = DungeonRunTreasure.BossWinsOf(on);
            if (wins > 0) up.Progress++;
            bool due = on == DungeonRunTreasure.UpgradeOnActClear ? majorWon
                : wins > 0 ? up.Progress >= wins
                : state.Floor - up.SinceFloor >= Rules.UpgradeEveryFloors;
            if (!due) continue;
            string next = NextStage(up.Key, slot);
            if (next == null) { state.Upgrades.Remove(up); continue; }
            switch (slot)
            {
                case DungeonRunTreasure.SlotPower:
                    if (state.Power != up.Key) { state.Upgrades.Remove(up); continue; }
                    state.Power = next;
                    break;
                case DungeonRunTreasure.SlotPassive:
                    int passive = state.Treasures.IndexOf(up.Key);
                    if (passive < 0) { state.Upgrades.Remove(up); continue; }
                    state.Treasures[passive] = next;
                    break;
                default:
                    if (state.IsActRun)
                    {
                        // An act run finds the shell by its card record, so a copy or a second shell of the same id is never swapped.
                        var card = DungeonRunDeck.Find(state, up.Uid);
                        if (card == null) { state.Upgrades.Remove(up); continue; }
                        card.Id = DungeonRunContent.Get(next).ShellId;
                        DungeonRunDeck.Sync(state);
                    }
                    else
                    {
                        int at = state.Deck.IndexOf(DungeonRunContent.Get(up.Key).ShellId);
                        if (at < 0) { state.Upgrades.Remove(up); continue; }
                        state.Deck[at] = DungeonRunContent.Get(next).ShellId;
                    }
                    int held = state.ActiveTreasures.IndexOf(up.Key);
                    if (held >= 0) state.ActiveTreasures[held] = next;
                    if (state.Loadout != null)
                    {
                        int sig = state.Loadout.Signature.IndexOf(up.Key);
                        if (sig >= 0) state.Loadout.Signature[sig] = next;
                    }
                    break;
            }
            up.Key = next;
            up.SinceFloor = state.Floor;
            up.Progress = 0;
            if (NextStage(next, slot) == null) state.Upgrades.Remove(up);
        }
    }

    // When an upgrade record fires: a power upgrade's "on", a treasure's upgradeOn.
    private string UpgradeTrigger(DungeonRunUpgrade up, string slot) =>
        slot == DungeonRunTreasure.SlotPower
            ? Data.PowerUpgrades.TryGetValue(up.Key, out var power) ? power.On : DungeonRunTreasure.UpgradeOnActClear
            : Data.FindTreasure(up.Key)?.UpgradeOn ?? DungeonRunTreasure.UpgradeOnFloors;

    // The next stage of an upgrade record's key, or null for its last: a power's powerUpgrades "to", a treasure's upgradesTo.
    private string NextStage(string key, string slot) =>
        slot == DungeonRunTreasure.SlotPower
            ? Data.PowerUpgrades.TryGetValue(key, out var power) ? power.To : null
            : Data.FindTreasure(key)?.UpgradesTo;

    // ------------------------------------------------------------------ bundles (design F5)

    private void OpenBundleDraftOrAdvance(DungeonRunState state)
    {
        if (!OpenBundleDraft(state)) Advance(state);
    }

    /// <summary>
    /// Opens the bundle draft after a cleared floor; false when nothing is on offer. A legacy run (CatalogVersion 0) drafts the
    /// 52 fixed SoloStone bundles exactly as before the catalog; a catalog run drafts from the shared catalog
    /// (<see cref="OpenCatalogDraft"/>).
    /// </summary>
    internal bool OpenBundleDraft(DungeonRunState state)
    {
        // An act run whose deck has no room for a pick (deckMax) skips the draft: drafts never push a deck past what its
        // matches accept (taverns refuse such adds the same way). A legacy run never reaches its limit of 40.
        if (state.IsActRun && state.Deck.Count + DraftTakes(state) > DeckMax(state)) return false;
        bool opened = state.IsCatalogRun ? OpenCatalogDraft(state, DraftOptions(state)) : OpenLegacyBundleDraft(state, DraftOptions(state));
        // An act whose picks keep fewer than three cards records it on the draft (absent on every legacy draft).
        if (opened && DraftTakes(state) < 3) state.OpenDraft.Takes = DraftTakes(state);
        return opened;
    }

    /// <summary>
    /// The band a draft's cost weighting (<see cref="BandMult"/>) uses. Legacy schedule: the floor's band on the fixed eight
    /// floors (final floor band 3), unchanged. Act run: the act of the fight just won (act I 1, act II 2, act III and later 3),
    /// so the curve follows the acts instead of the eight-floor bands on a fifteen-floor run.
    /// </summary>
    internal int DraftBand(DungeonRunState state) =>
        state.IsActRun ? Math.Max(1, Math.Min(3, CurrentNode(state).Act)) : state.Floor >= Rules.FinalFloor ? 3 : Rules.BandOf(state.Floor);

    /// <summary>
    /// An act run's last won floor whose card draft was skipped (nothing left on offer for the class, or no room in the deck),
    /// so the next page can say so instead of moving on silently; 0 when the last won fight drafted, on a legacy run, and when
    /// nothing was won yet. Derived from the saved matches and drafts (no save field).
    /// </summary>
    internal int SkippedDraftFloor(DungeonRunState state)
    {
        // Only between nodes: during the floor's treasure or bundle draft the bundle draft is still to come or open.
        if (!state.IsActRun || state.Phase != DungeonRunPhase.PreMatch && state.Phase != DungeonRunPhase.Tavern && state.Phase != DungeonRunPhase.Modifier) return 0;
        var won = state.Matches.LastOrDefault(m => m.Outcome != DungeonRunOutcome.Fault);
        if (won == null || won.Outcome != DungeonRunOutcome.Win) return 0;
        return state.Drafts.Any(d => d.Kind == "bundle" && d.Floor == won.Floor) ? 0 : won.Floor;
    }

    /// <summary>
    /// The ids cooling down at the current floor ("id@floor" offered and not taken; offerable again from that floor). The id
    /// is everything before the last '@'. A malformed entry is a data error (InvalidDataException), never a parse crash.
    /// </summary>
    internal static HashSet<string> Cooling(DungeonRunState state)
    {
        var cooling = new HashSet<string>(StringComparer.Ordinal);
        foreach (string entry in state.BundleCooldowns)
        {
            int at = entry?.LastIndexOf('@') ?? -1;
            if (at <= 0 || !int.TryParse(entry.Substring(at + 1), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int until))
                throw new InvalidDataException("Dungeon Run save: malformed bundle cooldown \"" + (entry ?? "(null)") + "\" (expected id@floor).");
            if (until > state.Floor) cooling.Add(entry.Substring(0, at));
        }
        return cooling;
    }

    private bool OpenLegacyBundleDraft(DungeonRunState state, int options = 3)
    {
        var cls = RunClass(state);
        var cooling = Cooling(state);
        // Legacy runs draft the fixed SoloStone bundles only (catalog pool entries are a catalog run's).
        var candidates = Data.LegacyBundles
            .Where(b => b.OfferedTo(cls.Id) && !state.TakenBundles.Contains(b.Id) && !cooling.Contains(b.Id))
            .OrderBy(b => b.Id, StringComparer.Ordinal).ToList();
        if (candidates.Count == 0) return false;

        int band = DraftBand(state);
        int Weight(DungeonRunBundle b)
        {
            int nTag = state.DraftedThemes.Count(t => t == b.Theme);
            double affinity = 1 + 0.25 * Math.Min(nTag, 6);
            return Math.Max(1, (int)Math.Round(100 * affinity * BandMult(band, AverageCost(b)) / 100.0, MidpointRounding.AwayFromZero));
        }

        string dominant = DominantTheme(state);
        var offer = new List<DungeonRunBundle>();
        int draw = 0;
        // Slot 1: pity forces the dominant theme after PityWindow drafts without it.
        var pityPool = dominant == null ? new List<DungeonRunBundle>() : candidates.Where(b => b.Theme == dominant).ToList();
        offer.Add(state.DraftsWithoutDominant >= Rules.PityWindow && pityPool.Count > 0
            ? WeightedPick(pityPool, Weight, state.Seed, state.Floor, BundleStep, draw++)
            : WeightedPick(candidates, Weight, state.Seed, state.Floor, BundleStep, draw++));
        // Slot 2: weighted.
        var rest = candidates.Except(offer).ToList();
        if (rest.Count > 0 && options >= 2) offer.Add(WeightedPick(rest, Weight, state.Seed, state.Floor, BundleStep, draw++));
        // Slot 3: coverage — a different theme from slot 1 when slots 1 and 2 share one.
        rest = candidates.Except(offer).ToList();
        if (rest.Count > 0 && options >= 3)
        {
            var coverage = offer.Count == 2 && offer[0].Theme == offer[1].Theme ? rest.Where(b => b.Theme != offer[0].Theme).ToList() : rest;
            offer.Add(WeightedPick(coverage.Count > 0 ? coverage : rest, Weight, state.Seed, state.Floor, BundleStep, draw++));
        }
        state.Drafts.Add(new DungeonRunDraft { Floor = state.Floor, Kind = "bundle", Offered = offer.Select(b => b.Id).OrderBy(x => x, StringComparer.Ordinal).ToList() });
        state.Phase = DungeonRunPhase.BundleDraft;
        return true;
    }

    /// <summary>
    /// A catalog run's bundle draft (phase 3): up to three entries, each three cards. Candidates are the catalog entries
    /// eligible for the run's classes (<see cref="DungeonRunBundle.EligibleFor"/>), a fixed entry only while not taken, a pool
    /// entry only while taken fewer than its take limit (<see cref="MaxTakes"/>: draft.bucketMaxTakes, once for a distinct
    /// entry), none cooling down; ordinal id order. Weight = entry weight x theme affinity (1 + 0.25 n, n = the most drafted
    /// cards of any of its themes, capped at 6) x the band multiplier of its average cost x draft.dualMatchWeight when the run
    /// has its classesAll pair. Slots as the legacy draft (pity, weighted, a different theme); within one draft no entry
    /// repeats, no label repeats within a shared class, and no two pool entries draw from the same cards
    /// (<see cref="SameDraw"/>). A pool entry's three cards are three different candidates drawn by weight
    /// (<see cref="DrawCards"/>); a fixed entry offers its cards. The offers, their cards and which cards are grants are
    /// stored on the draft, so a resume never redraws them.
    /// </summary>
    internal bool OpenCatalogDraft(DungeonRunState state, int options = 3)
    {
        var cls = RunClass(state);
        var classes = LootClasses(state);
        var cooling = Cooling(state);
        bool secretHolds = DungeonRunAdmission.MechanicHolds(Data, classes, "secret");
        Func<string, bool> mechanic = m => m == "secret" && secretHolds;
        var candidates = Data.Bundles
            .Where(b => b.EligibleFor(classes, mechanic))
            .Where(b => b.IsPool ? state.TakesOf(b.Id) < MaxTakes(b) : !state.TakenBundles.Contains(b.Id))
            .Where(b => !cooling.Contains(b.Id))
            .OrderBy(b => b.Id, StringComparer.Ordinal).ToList();
        if (candidates.Count == 0) return false;

        int band = DraftBand(state);
        var drafted = state.DraftedThemes.GroupBy(t => t, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var weights = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var b in candidates)
        {
            var themes = b.Themes.Count > 0 ? b.Themes : new List<string> { b.Theme };
            int nTag = themes.Where(t => t != null).Select(t => drafted.TryGetValue(t, out int n) ? n : 0).DefaultIfEmpty(0).Max();
            double affinity = 1 + 0.25 * Math.Min(nTag, 6);
            int dual = b.ClassesAll.Count > 0 && b.ClassesAll.All(cls.ClassSet.Contains) ? Rules.DualMatchWeight : 1;
            double cost = b.IsPool ? b.AvgCost : AverageCost(b);
            weights[b.Id] = Math.Max(1, (int)Math.Round(b.Weight * affinity * BandMult(band, cost) * dual, MidpointRounding.AwayFromZero));
        }
        int Weight(DungeonRunBundle b) => weights[b.Id];

        string dominant = DominantTheme(state);
        var offer = new List<DungeonRunBundle>();
        var pool = new List<DungeonRunBundle>(candidates);
        int draw = 0;
        // After each pick, nothing that repeats its id, its label within a shared class, or its draw (a pool entry of another
        // adventure with the same class set and candidates) stays a candidate.
        void Take(DungeonRunBundle picked)
        {
            offer.Add(picked);
            pool.RemoveAll(b => b.Id == picked.Id || b.LabelKey != null && b.LabelKey == picked.LabelKey && b.ClassSet.Any(picked.ClassSet.Contains)
                || SameDraw(b, picked));
        }
        // Slot 1: pity forces the dominant theme after PityWindow drafts without it.
        var pityPool = dominant == null ? new List<DungeonRunBundle>() : pool.Where(b => b.Themes.Contains(dominant) || b.Theme == dominant).ToList();
        Take(state.DraftsWithoutDominant >= Rules.PityWindow && pityPool.Count > 0
            ? WeightedPick(pityPool, Weight, state.Seed, state.Floor, BundleStep, draw++)
            : WeightedPick(pool, Weight, state.Seed, state.Floor, BundleStep, draw++));
        // Slot 2: weighted.
        if (pool.Count > 0 && options >= 2) Take(WeightedPick(pool, Weight, state.Seed, state.Floor, BundleStep, draw++));
        // Slot 3: coverage — a different primary theme from slot 1 when slots 1 and 2 share one.
        if (pool.Count > 0 && options >= 3)
        {
            var coverage = offer.Count == 2 && offer[0].PrimaryTheme == offer[1].PrimaryTheme ? pool.Where(b => b.PrimaryTheme != offer[0].PrimaryTheme).ToList() : pool;
            Take(WeightedPick(coverage.Count > 0 ? coverage : pool, Weight, state.Seed, state.Floor, BundleStep, draw++));
        }

        var draft = new DungeonRunDraft { Floor = state.Floor, Kind = "bundle" };
        for (int slot = 0; slot < offer.Count; slot++)
        {
            var entry = offer[slot];
            draft.Offered.Add(entry.Id);
            var cards = entry.IsPool ? DrawCards(entry, state.Seed, state.Floor, slot) : new List<string>(entry.Cards);
            draft.Offers.Add(new DungeonRunOffer
            {
                Id = entry.Id, Label = entry.Label ?? entry.PrimaryTheme, Theme = entry.PrimaryTheme,
                Kind = entry.IsPool ? DungeonRunBundle.KindPool : DungeonRunBundle.KindFixed, Source = SourceTag(entry),
                Cards = cards,
                Grants = cards.Where(id => entry.Candidates.Any(c => c.Grant && c.Id == id)).Distinct().ToList(),
            });
        }
        state.Drafts.Add(draft);
        state.Phase = DungeonRunPhase.BundleDraft;
        return true;
    }

    /// <summary>
    /// How many times a catalog run may take pool entry <paramref name="entry"/>: draft.bucketMaxTakes, or once for a distinct
    /// (Highlander) entry, whose second take would repeat its no-duplicates cards and switch their condition off.
    /// </summary>
    internal int MaxTakes(DungeonRunBundle entry) => entry.Distinct ? 1 : Rules.BucketMaxTakes;

    /// <summary>
    /// The three cards of a pool entry's offer in slot <paramref name="slot"/>: card k is a weighted draw over the candidates
    /// not drawn yet (StableSeed component slot * 16 + k on BucketStep), so one offer never repeats a card. A bucket repeats
    /// across steps instead (a later take draws again).
    /// </summary>
    internal static List<string> DrawCards(DungeonRunBundle entry, long seed, int floor, int slot)
    {
        var list = new List<DungeonRunCandidate>(entry.Candidates);
        var cards = new List<string>();
        for (int k = 0; k < 3 && list.Count > 0; k++)
        {
            int total = list.Sum(c => Math.Max(1, c.Weight));
            int roll = StableSeed.Index(StableSeed.Derive(seed, DraftStream, floor, BucketStep, slot * 16 + k), total);
            var picked = list[list.Count - 1];
            foreach (var c in list)
            {
                roll -= Math.Max(1, c.Weight);
                if (roll < 0) { picked = c; break; }
            }
            cards.Add(picked.Id);
            list.Remove(picked);
        }
        return cards;
    }

    /// <summary>Two pool entries draw from the same cards: the same class set and the same candidate ids.</summary>
    internal static bool SameDraw(DungeonRunBundle a, DungeonRunBundle b) =>
        a.IsPool && b.IsPool && a.ClassSet.SequenceEqual(b.ClassSet) && a.Candidates.Count == b.Candidates.Count
        && new HashSet<string>(a.Candidates.Select(c => c.Id), StringComparer.Ordinal).SetEquals(b.Candidates.Select(c => c.Id));

    /// <summary>The short tag of an entry's first provenance ("DR", "MH", "RR", "DH", "ToT"; "SoloStone" for a fixed bundle).</summary>
    internal static string SourceTag(DungeonRunBundle entry)
    {
        switch (entry?.Provenance.FirstOrDefault()?.Adventure)
        {
            case "Dungeon Run": return "DR";
            case "Monster Hunt": return "MH";
            case "Rumble Run": return "RR";
            case "The Dalaran Heist": return "DH";
            case "Tombs of Terror": return "ToT";
            default: return "SoloStone";
        }
    }

    internal void PickBundle(DungeonRunState state, string bundleId)
    {
        var draft = state.OpenDraft;
        if (state.Phase != DungeonRunPhase.BundleDraft || draft == null || draft.Kind != "bundle" || !draft.Offered.Contains(bundleId))
            throw new InvalidOperationException(bundleId + " is not on offer.");
        if (draft.Takes > 0) throw new InvalidOperationException("This draft keeps " + draft.Takes + " cards of one bundle: pick them one at a time.");
        if (draft.Offers.Count > 0) { PickCatalogBundle(state, draft, bundleId, null); return; }
        PickLegacyBundle(state, draft, bundleId, null);
    }

    /// <summary>The cards offer <paramref name="bundleId"/> of the open draft holds (stored offer, else the fixed bundle).</summary>
    internal List<string> OfferCards(DungeonRunDraft draft, string bundleId) =>
        draft.Offer(bundleId)?.Cards.ToList() ?? Data.LegacyBundles.FirstOrDefault(b => b.Id == bundleId)?.Cards.ToList() ?? new List<string>();

    /// <summary>
    /// One card of a <see cref="DungeonRunDraft.Takes"/> draft (an act whose picks keep two of a bundle's three cards). The first
    /// card fixes the bundle; each card must be one of that offer's cards not kept yet (a repeated card can be kept twice).
    /// The pick that reaches Takes closes the draft as a pick of that bundle with the kept cards, and the run moves on.
    /// </summary>
    internal void PickDraftCard(DungeonRunState state, string bundleId, string cardId)
    {
        var draft = state.OpenDraft;
        if (state.Phase != DungeonRunPhase.BundleDraft || draft == null || draft.Kind != "bundle" || !draft.Offered.Contains(bundleId))
            throw new InvalidOperationException(bundleId + " is not on offer.");
        if (draft.Takes <= 0) throw new InvalidOperationException("This draft takes a whole bundle.");
        if (draft.Choosing != null && draft.Choosing != bundleId) throw new InvalidOperationException("Cards are being kept from " + draft.Choosing + ".");
        var left = OfferCards(draft, bundleId);
        foreach (string kept in draft.Chosen) left.Remove(kept);
        if (!left.Contains(cardId)) throw new InvalidOperationException(cardId + " is not left in " + bundleId + ".");
        draft.Choosing = bundleId;
        draft.Chosen.Add(cardId);
        if (draft.Chosen.Count < Math.Min(draft.Takes, OfferCards(draft, bundleId).Count)) return;
        var cards = draft.Chosen.ToList();
        if (draft.Offers.Count > 0) PickCatalogBundle(state, draft, bundleId, cards); else PickLegacyBundle(state, draft, bundleId, cards);
    }

    /// <summary>Drops a partial card pick (nothing was added to the deck), so another bundle can be chosen.</summary>
    internal void ClearDraftCards(DungeonRunState state)
    {
        var draft = state.OpenDraft;
        if (state.Phase != DungeonRunPhase.BundleDraft || draft == null) throw new InvalidOperationException("No bundle draft is open.");
        draft.Choosing = null;
        draft.Chosen.Clear();
    }

    private void PickLegacyBundle(DungeonRunState state, DungeonRunDraft draft, string bundleId, List<string> kept)
    {
        draft.Picked = bundleId;
        var bundle = Data.LegacyBundles.First(b => b.Id == bundleId);
        string dominantBefore = DominantTheme(state);
        var cards = kept ?? bundle.Cards;
        AddDrafted(state, cards);
        state.DraftedThemes.AddRange(cards.Select(_ => bundle.Theme));
        state.TakenBundles.Add(bundleId);
        foreach (string other in draft.Offered.Where(o => o != bundleId))
            state.BundleCooldowns.Add($"{other}@{state.Floor + Rules.ReofferAfterFloors}");
        // A draft counts against pity only when a dominant theme already existed and was not taken: the first draft, which
        // sets the dominant theme, is not a draft "without" it (PITY_WINDOW = 2 such drafts, design F5).
        state.DraftsWithoutDominant = dominantBefore == null || bundle.Theme == dominantBefore ? 0 : state.DraftsWithoutDominant + 1;
        Advance(state);
    }

    // A catalog draft's pick: the stored cards and theme, never the data's current entry. Each card is checked again against
    // the current data (the entry's class set, a grant as the offer stored it, with no class check once the entry is gone;
    // the client check of DungeonRunData.ClientKnows): a card that no longer passes is a validation error, and the caller's
    // Mutate rolls the run back.
    private void PickCatalogBundle(DungeonRunState state, DungeonRunDraft draft, string bundleId, List<string> kept)
    {
        var offer = draft.Offer(bundleId) ?? throw new InvalidDataException("Dungeon Run save: " + bundleId + " is offered with no stored cards.");
        if (offer.Cards == null || offer.Cards.Count == 0) throw new InvalidDataException("Dungeon Run save: the offer of " + bundleId + " has no cards.");
        var entry = Data.Bundles.FirstOrDefault(b => b.Id == bundleId);
        var policy = OfflinePoolPolicy.Load();
        foreach (string card in offer.Cards)
        {
            var candidate = entry?.Candidates.FirstOrDefault(c => c.Id == card);
            // The grant flag as offered (a dropped or changed candidate keeps it); a save from before Grants were stored falls
            // back to the current candidate.
            bool grant = offer.Grants.Contains(card) || (candidate?.Grant ?? false);
            string reason = DungeonRunValidator.PoolCardFailures(card, "catalog", entry?.ClassSet, grant, policy, Data.ClientKnows, catalogRun: true)
                .Select(f => f.Code).FirstOrDefault();
            if (reason == null && DungeonRunValidator.HasRuneCost(card)) reason = "rune-cost";
            if (reason != null) throw new InvalidDataException($"Dungeon Run: the offered card {card} of {bundleId} no longer passes the pool checks ({reason}).");
        }
        draft.Picked = bundleId;
        string dominantBefore = DominantTheme(state);
        var cards = kept ?? offer.Cards;
        AddDrafted(state, cards);
        state.DraftedThemes.AddRange(cards.Select(_ => offer.Theme));
        bool pool = entry != null ? entry.IsPool : offer.Kind == DungeonRunBundle.KindPool;
        if (pool) state.AddTake(bundleId); else state.TakenBundles.Add(bundleId);
        foreach (string other in draft.Offered.Where(o => o != bundleId))
            state.BundleCooldowns.Add($"{other}@{state.Floor + Rules.ReofferAfterFloors}");
        state.DraftsWithoutDominant = dominantBefore == null || offer.Theme == dominantBefore ? 0 : state.DraftsWithoutDominant + 1;
        Advance(state);
    }

    // Drafted cards: appended to Deck on a legacy run, added as card records (origin draft) on an act run.
    private static void AddDrafted(DungeonRunState state, IEnumerable<string> cards)
    {
        if (!state.IsActRun) { state.Deck.AddRange(cards); return; }
        foreach (string id in cards.ToList()) DungeonRunDeck.Add(state, id, DungeonRunDeck.OriginDraft);
    }

    /// <summary>
    /// Moves on: a legacy run to the next floor's pre-match; an act run to its next node (<see cref="EnterNode"/>). Called after
    /// a floor's drafts, a tavern and a modifier node.
    /// </summary>
    internal void Advance(DungeonRunState state)
    {
        if (state.IsActRun)
        {
            var schedule = ScheduleFor(state);
            if (state.Node + 1 >= schedule.Nodes.Count) throw new InvalidOperationException("The schedule has no node after " + state.Node + ".");
            state.Node++;
            EnterNode(state);
            return;
        }
        state.Floor++;
        state.Attempt = 0;
        state.Phase = DungeonRunPhase.PreMatch;
    }

    /// <summary>
    /// Enters the act run's current node: <see cref="DungeonRunState.Floor"/> becomes the node's floor (a tavern or modifier
    /// node's is the fight it precedes, so Floor never goes back); a fight is staged at pre-match, a tavern opens
    /// (<see cref="DungeonRunTavern.Open"/>), a modifier node offers its modifiers (none left: the node is passed).
    /// </summary>
    private void EnterNode(DungeonRunState state)
    {
        var node = NodeAt(state);
        state.Floor = node.Floor;
        state.Attempt = 0;
        switch (node.Kind)
        {
            case DungeonRunNodeKind.Tavern:
                DungeonRunTavernNode.Open(this, state);
                return;
            case DungeonRunNodeKind.Modifier:
                if (!OpenModifier(state)) Advance(state);
                return;
            case DungeonRunNodeKind.Anomaly:
                if (!OpenAnomaly(state)) Advance(state);
                return;
            default:
                state.Phase = DungeonRunPhase.PreMatch;
                return;
        }
    }

    // ------------------------------------------------------------------ modifier nodes (act runs)

    /// <summary>
    /// The modifiers a modifier node may offer: every schedule modifier, ordinal id order, minus those a boss of this act lists
    /// in "incompatibleModifiers" and those already active on it.
    /// </summary>
    internal List<DungeonRunModifier> ModifierCandidates(DungeonRunState state)
    {
        var node = CurrentNode(state);
        var schedule = ScheduleFor(state);
        var floors = schedule.Nodes.Where(n => n.Act == node.Act && n.IsFight).Select(n => n.Floor).ToList();
        var banned = new HashSet<string>(floors.Where(f => f >= 1 && f <= state.BossSequence.Count)
            .SelectMany(f => Data.Bosses.FirstOrDefault(b => b.Id == state.BossSequence[f - 1])?.IncompatibleModifiers ?? new List<string>()), StringComparer.Ordinal);
        var taken = new HashSet<string>(state.ActiveRules.Where(r => r.Act == node.Act).Select(r => r.Id), StringComparer.Ordinal);
        // Phase 8: a modifier an active global rule excludes (ULDA_715 Bountiful and mod_open_road) is left out too.
        foreach (var rule in ActiveGlobalRules(state)) banned.UnionWith(rule.IncompatibleModifiers);
        return Data.Schedules.Modifiers.Where(m => !banned.Contains(m.Id) && !taken.Contains(m.Id)).OrderBy(m => m.Id, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Opens the current modifier node: <see cref="ModifierOptions"/> modifiers drawn without replacement (StableSeed
    /// <see cref="ModifierStream"/>, Node, draw). False when no modifier is left to offer.
    /// </summary>
    internal bool OpenModifier(DungeonRunState state)
    {
        var candidates = ModifierCandidates(state);
        if (candidates.Count == 0) return false;
        var offer = new DungeonRunModifierOffer { Node = state.Node };
        for (int k = 0; k < ModifierOptions && candidates.Count > 0; k++)
        {
            var picked = candidates[StableSeed.Index(StableSeed.Derive(state.Seed, ModifierStream, state.Node, k), candidates.Count)];
            offer.Offered.Add(picked.Id);
            candidates.Remove(picked);
        }
        state.ModifierOffer = offer;
        state.Phase = DungeonRunPhase.Modifier;
        return true;
    }

    /// <summary>Takes an offered modifier: it is active for the rest of this act. The run moves to the next node.</summary>
    internal void PickModifier(DungeonRunState state, string modifierId)
    {
        var offer = state.ModifierOffer;
        if (state.Phase != DungeonRunPhase.Modifier || offer == null || offer.Node != state.Node || !offer.Offered.Contains(modifierId))
            throw new InvalidOperationException(modifierId + " is not on offer.");
        if (offer.Kind == DungeonRunGlobalRule.KindAnomaly)
        {
            // Phase 8: an anomaly node's pick. "none" takes no rule; a run-scope rule (Act 0) lasts the whole run.
            if (modifierId != DungeonRunGlobalRule.None)
            {
                var rule = Data.Schedules.Rule(modifierId) ?? throw new InvalidDataException("Dungeon Run save: unknown rule " + modifierId + ".");
                state.ActiveRules.Add(new DungeonRunRule
                {
                    Id = modifierId, Act = rule.RunScope ? 0 : CurrentNode(state).Act, FromNode = state.Node, Kind = DungeonRunGlobalRule.KindAnomaly,
                });
            }
            state.ModifierOffer = null;
            Advance(state);
            return;
        }
        state.ActiveRules.Add(new DungeonRunRule { Id = modifierId, Act = CurrentNode(state).Act, FromNode = state.Node });
        state.ModifierOffer = null;
        Advance(state);
    }

    /// <summary>Buys a tavern option (<see cref="DungeonRunTavernNode.Buy"/>); a refusal throws, so the runtime's Mutate rolls back.</summary>
    internal void BuyTavern(DungeonRunState state, string optionId) => Require(DungeonRunTavernNode.Buy(this, state, optionId));

    /// <summary>Resolves the tavern's pending purchase (<see cref="DungeonRunTavernNode.Choose"/>); a refusal throws.</summary>
    internal void ChooseTavern(DungeonRunState state, string candidate) => Require(DungeonRunTavernNode.Choose(this, state, candidate));

    /// <summary>Cancels the tavern's pending purchase (<see cref="DungeonRunTavernNode.Back"/>); nothing was charged.</summary>
    internal void BackTavern(DungeonRunState state) => Require(DungeonRunTavernNode.Back(state));

    private static void Require(DeckEditResult result)
    {
        if (!result.Ok) throw new InvalidOperationException(result.Message ?? result.Error);
    }

    /// <summary>Leaves the open tavern node: the tavern state is cleared and the run moves on.</summary>
    internal void LeaveTavern(DungeonRunState state)
    {
        if (state.Phase != DungeonRunPhase.Tavern || state.Tavern == null) throw new InvalidOperationException("No tavern is open.");
        if (state.Tavern.Pending != null) throw new InvalidOperationException("Finish or cancel the pending purchase first.");
        state.Tavern = null;
        Advance(state);
    }

    internal static string DominantTheme(DungeonRunState state) =>
        state.DraftedThemes.GroupBy(t => t).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).Select(g => g.Key).FirstOrDefault();

    private static double AverageCost(DungeonRunBundle b) => b.Cards.Average(id => Cards.FromId(id).Cost);

    /// <summary>Design F5 band multiplier table: rows band 1-3, columns low (&lt; 2.5), mid, high (&gt; 4.0) average cost.</summary>
    internal static int BandMult(int band, double averageCost)
    {
        int column = averageCost < 2.5 ? 0 : averageCost <= 4.0 ? 1 : 2;
        int[][] table = { new[] { 120, 100, 70 }, new[] { 100, 110, 100 }, new[] { 80, 100, 130 } };
        return table[Math.Max(1, Math.Min(3, band)) - 1][column];
    }

    // ------------------------------------------------------------------ encounters, global rules, run-owned fight state (phase 8)

    /// <summary>Whether the run reads and writes the profile's persistent boss damage: a random-seed act run (a typed seed can be replayed at will).</summary>
    internal static bool ReadsLedger(DungeonRunState state) => state.IsActRun && !state.TypedSeed;

    /// <summary>
    /// The global rules (anomalies and twists) active on the current act, in the order they were taken: the act's own and the
    /// run-scope ones (Act 0). Empty on a legacy run and on every run before phase 8.
    /// </summary>
    internal List<DungeonRunGlobalRule> ActiveGlobalRules(DungeonRunState state)
    {
        if (!state.IsActRun || Data.Schedules == null || state.ActiveRules.All(r => r.Kind == null)) return new List<DungeonRunGlobalRule>();
        int act = CurrentNode(state).Act;
        return state.ActiveRules.Where(r => r.Kind != null && (r.Act == 0 || r.Act == act)).Select(r => Data.Schedules.Rule(r.Id)).Where(r => r != null).ToList();
    }

    /// <summary>A rule is active on the current act (a run-scope rule: on the whole run).</summary>
    internal bool RuleActive(DungeonRunState state, string ruleId) => ActiveGlobalRules(state).Any(r => r.Id == ruleId);

    // The bosses pre-rolled for the fights of act actNo.
    private List<DungeonRunBoss> ActBosses(DungeonRunState state, DungeonRunSchedule schedule, int actNo) =>
        schedule.Nodes.Where(n => n.Act == actNo && n.IsFight && n.Floor >= 1 && n.Floor <= state.BossSequence.Count)
            .Select(n => Data.Bosses.FirstOrDefault(b => b.Id == state.BossSequence[n.Floor - 1])).Where(b => b != null).ToList();

    /// <summary>
    /// The rules of <paramref name="kind"/> that may become active on act <paramref name="actNo"/> of this run, in file order:
    /// admitted in the data and by the admission table (scope Rule), scripted when they act in a match
    /// (DungeonRunGlobalRules.IsScripted), a run-scope rule only on act 1, not active already, and compatible with the run (a
    /// board shrine), with the rules and modifiers active on the act, and with every boss pre-rolled for it (its flags and its
    /// incompatibleRules).
    /// </summary>
    internal List<DungeonRunGlobalRule> RuleCandidates(DungeonRunState state, DungeonRunSchedule schedule, int actNo, string kind)
    {
        var active = state.ActiveRules.Where(r => r.Act == 0 || r.Act == actNo).ToList();
        var activeRules = active.Where(r => r.Kind != null).Select(r => Data.Schedules.Rule(r.Id)).Where(r => r != null).ToList();
        var activeIds = new HashSet<string>(active.Select(r => r.Id), StringComparer.Ordinal);
        var bosses = ActBosses(state, schedule, actNo);
        var result = new List<DungeonRunGlobalRule>();
        foreach (var rule in Data.Schedules.Rules.Where(r => r.Kind == kind && r.Admitted))
        {
            if (rule.RunScope && actNo != 1) continue;
            if (activeIds.Contains(rule.Id)) continue;
            if (rule.Effect == DungeonRunGlobalRule.EffectMatch && !DungeonRunGlobalRules.IsScripted(rule.Id)) continue;
            if (!DungeonRunAdmission.Classify(null, rule.Id, AdmitScope.Rule, null, state.IsCatalogRun).Admitted) continue;
            if (rule.NoBoardShrine && state.HasBoardShrine) continue;
            if (rule.IncompatibleClasses.Contains(state.Class)) continue;
            if (rule.IncompatibleRules.Any(activeIds.Contains) || activeRules.Any(r => r.IncompatibleRules.Contains(rule.Id))) continue;
            if (rule.IncompatibleModifiers.Any(activeIds.Contains)) continue;
            if (bosses.Any(b => b.IncompatibleRules.Contains(rule.Id) || b.Flags.Any(rule.IncompatibleFlags.Contains))) continue;
            result.Add(rule);
        }
        return result;
    }

    /// <summary>
    /// Opens the current anomaly node (phase 8): <see cref="AnomalyOptions"/> rules drawn by weight without replacement
    /// (<see cref="AnomalyStream"/>, Node, draw) from the act's candidates, then "none". The offer is stored (a resume never
    /// redraws it) and picked like a modifier offer (<see cref="PickModifier"/>). False when no rule can be offered.
    /// </summary>
    internal bool OpenAnomaly(DungeonRunState state)
    {
        var node = CurrentNode(state);
        var candidates = RuleCandidates(state, ScheduleFor(state), node.Act, DungeonRunGlobalRule.KindAnomaly);
        if (candidates.Count == 0) return false;
        var offer = new DungeonRunModifierOffer { Node = state.Node, Kind = DungeonRunGlobalRule.KindAnomaly };
        for (int k = 0; k < AnomalyOptions && candidates.Count > 0; k++)
        {
            int total = candidates.Sum(r => Math.Max(1, r.Weight));
            int roll = StableSeed.Index(StableSeed.Derive(state.Seed, AnomalyStream, state.Node, k), total);
            var picked = candidates[candidates.Count - 1];
            foreach (var rule in candidates)
            {
                roll -= Math.Max(1, rule.Weight);
                if (roll < 0) { picked = rule; break; }
            }
            offer.Offered.Add(picked.Id);
            candidates.Remove(picked);
        }
        offer.Offered.Add(DungeonRunGlobalRule.None);
        state.ModifierOffer = offer;
        state.Phase = DungeonRunPhase.Modifier;
        return true;
    }

    // NewRun: each act with a twist pool rolls its chance (TwistStream, act, 0) and, on a hit, one compatible twist of the pool
    // (act, 1). A rolled twist is an active rule of that act from the start (FromNode -1).
    private void RollTwists(DungeonRunState state, DungeonRunSchedule schedule)
    {
        for (int a = 0; a < schedule.Acts.Count; a++)
        {
            var act = schedule.Acts[a];
            if (act.TwistPool.Count == 0 || act.TwistChance <= 0) continue;
            if (StableSeed.Index(StableSeed.Derive(state.Seed, TwistStream, a + 1, 0), 100) >= act.TwistChance) continue;
            var candidates = RuleCandidates(state, schedule, a + 1, DungeonRunGlobalRule.KindTwist).Where(r => act.TwistPool.Contains(r.Id)).ToList();
            if (candidates.Count == 0) continue;
            var twist = candidates[StableSeed.Index(StableSeed.Derive(state.Seed, TwistStream, a + 1, 1), candidates.Count)];
            state.ActiveRules.Add(new DungeonRunRule { Id = twist.Id, Act = twist.RunScope ? 0 : a + 1, FromNode = -1, Kind = DungeonRunGlobalRule.KindTwist });
        }
    }

    /// <summary>The shrine variant of the current fight (a Rumble Run champion): drawn from the run seed and the floor, so a restage keeps it. Null for a boss without variants.</summary>
    internal DungeonRunVariant CurrentVariant(DungeonRunState state)
    {
        var boss = CurrentBoss(state);
        return boss.Variants.Count == 0 ? null
            : boss.Variants[StableSeed.Index(StableSeed.Derive(state.Seed, EncounterStream, 1000 + state.Floor), boss.Variants.Count)];
    }

    /// <summary>
    /// A persistent-damage boss's Health at the start of the current fight: its full Health, or what the profile's record left
    /// (a run that reads the ledger), less the damage the run's ULDA_115 casts banked for it (DungeonRunState.PlagueLordDamage); at least 1.
    /// </summary>
    internal int PersistentRemaining(DungeonRunState state, DungeonRunProfile profile)
    {
        var boss = CurrentBoss(state);
        int full = BossHealth(state);
        if (!boss.HasFlag(DungeonRunEncounterData.PersistentDamage)) return full;
        int remaining = ReadsLedger(state) && profile?.DamageOf(boss.Id) is DungeonRunBossDamage record ? Math.Min(full, Math.Max(1, record.Remaining)) : full;
        if (state.PlagueLordDamage > 0) remaining -= state.PlagueLordDamage;
        return Math.Max(1, remaining);
    }

    /// <summary>
    /// The fight-only heroes the current boss gives this run (phase 8), as the state records them: Hagatha's hero cards (kind
    /// "hunters") or a finale's relay heroes (kind "relay"), each without the options the run's own hero card is listed to
    /// skip. Null for a fight with neither. Pure: BeginMatch stores it, BuildPlan derives the same options.
    /// </summary>
    internal DungeonRunHeroSwitch HeroSwitchFor(DungeonRunState state)
    {
        if (!state.IsActRun) return null;
        var boss = CurrentBoss(state);
        string own = RunClass(state).HeroCard;
        var cards = boss.HeroCards.Where(h => !h.SkipFor.Contains(own)).ToList();
        var relay = RelayFor(state, boss);
        if (cards.Count == 0 && relay.Count == 0) return null;
        var result = new DungeonRunHeroSwitch { Floor = state.Floor, Kind = relay.Count > 0 ? DungeonRunHeroSwitch.KindRelay : DungeonRunHeroSwitch.KindHunters };
        foreach (var h in cards)
        {
            SabberStoneCore.Model.Card card;
            try { card = Cards.FromId(h.Hero); } catch { card = null; }
            string power = card == null ? null : Cards.GetHeroPower(card[SabberStoneCore.Enums.GameTag.HERO_POWER])?.Id;
            result.Options.Add(new DungeonRunHeroOption { Hero = h.Hero, Power = power });
        }
        foreach (var h in relay) result.Options.Add(new DungeonRunHeroOption { Hero = h.Hero, Power = h.Power, Deck = h.Deck, Health = h.Health });
        return result;
    }

    /// <summary>
    /// The relay entries of the current fight against <paramref name="boss"/> (phase 8), in list order, less the ones the run's
    /// own hero card is listed to skip. A hero listed more than once (family 24: a Tombs explorer with its three client finale
    /// decks and powers) is one relay slot: which of its entries enters is drawn by the match's game RNG when it enters
    /// (Actions.EncounterRules.ResolveRelay), so the boss's fair view cannot know it beforehand. Pure.
    /// </summary>
    internal List<DungeonRunRelayHero> RelayFor(DungeonRunState state, DungeonRunBoss boss)
    {
        string own = RunClass(state).HeroCard;
        return boss.Relay.Where(h => !h.SkipFor.Contains(own)).ToList();
    }

    /// <summary>
    /// Who the run player is in the current fight (phase 8): the run's own hero, power, Health and deck (never changed by a
    /// fight), plus the fight-only heroes this boss gives (<see cref="HeroSwitchFor"/>). Every per-fight reader of "the run's
    /// classes" that must cover a switched hero reads <see cref="MatchClasses"/>; the treasure pool, drafts and the archive
    /// keep the run's own class.
    /// </summary>
    internal DungeonRunMatchIdentity MatchIdentity(DungeonRunState state)
    {
        var cls = RunClass(state);
        return new DungeonRunMatchIdentity
        {
            Name = cls.Name, HeroCard = cls.HeroCard, Power = PlayerPower(state), Health = cls.Health, Deck = state.Deck,
            Classes = cls.ClassSet, Switch = state.Phase == DungeonRunPhase.PreMatch || state.Phase == DungeonRunPhase.InMatch ? HeroSwitchFor(state) : null,
        };
    }

    /// <summary>
    /// Every class the run player's hero can be in the current fight, the run's own first: the run row's classes, then the
    /// class of each fight-only hero (the boss's Secret prior must cover all of them: DungeonRunBossPilot.Build).
    /// </summary>
    internal IReadOnlyList<string> MatchClasses(DungeonRunState state)
    {
        var classes = new List<string>(LootClasses(state));
        var heroes = MatchIdentity(state).Switch;
        foreach (var option in heroes?.Options ?? new List<DungeonRunHeroOption>())
        {
            SabberStoneCore.Model.Card card;
            try { card = Cards.FromId(option.Hero); } catch { card = null; }
            string name = card?.Class.ToString();
            if (name != null && name != "NEUTRAL" && name != "INVALID" && !classes.Contains(name)) classes.Add(name);
        }
        return classes;
    }

    // An imported encounter's plan fields and the active global rules, on top of the base plan.
    private void ApplyEncounter(DungeonRunState state, DungeonRunProfile profile, DungeonRunBoss boss, RunMatchPlan plan)
    {
        var rules = ActiveGlobalRules(state);
        plan.GlobalRules = rules.Where(r => r.Effect == DungeonRunGlobalRule.EffectMatch).Select(r => r.Id).ToList();
        // ULDA_720 Battle Scars: the run hero starts at 100 Health and carries its damage (never onto a fight-only hero).
        if (rules.Any(r => r.Id == BattleScars))
        {
            plan.PlayerHeroHealth = BattleScarsHealth;
            plan.PlayerCarriedDamage = Math.Max(0, state.HeroDamage);
        }
        if (!boss.IsEncounter) return;
        plan.EncounterId = boss.Id;
        plan.EncounterDeck = true;
        plan.BossGeneratedSecrets = boss.BossGeneratedSecrets;
        var variant = CurrentVariant(state);
        string deckId = variant?.Deck ?? boss.Deck;
        plan.BossDeck = Data.BossDecks[deckId].Select(Cards.FromId).ToList();
        plan.BossSideboards = Data.BossSideboards.TryGetValue(deckId, out var boards) ? boards : null;
        plan.BossShrine = variant?.Shrine;
        // Family 11: a mirror-deck boss (Face Collector) plays a copy of the run deck as this fight plays it, less the cards the
        // admission table does not admit for a boss; a Fabled bundle the filter split is left out whole (FabledRules); with
        // fewer than MinDeck cards left, or a copy the Fabled rules still refuse, it keeps the row's own deck.
        if (boss.HasFlag(DungeonRunEncounterData.MirrorDeck))
        {
            var mirror = plan.PlayerDeck.Where(c => DungeonRunAdmission.Classify(c, c.Id, AdmitScope.Boss, null, state.IsCatalogRun).Admitted).ToList();
            if (SabberStoneCore.Actions.FabledRules.DeckErrors(mirror.Select(c => c.Id)).Any())
                mirror.RemoveAll(c => SabberStoneCore.Actions.FabledRules.IsFabled(c.Id) || SabberStoneCore.Actions.FabledRules.IsCompanion(c.Id));
            if (mirror.Count >= DungeonRunEncounterData.MinDeck && !SabberStoneCore.Actions.FabledRules.DeckErrors(mirror.Select(c => c.Id)).Any())
            {
                plan.BossDeck = mirror;
                plan.BossSideboards = null;
            }
        }
        if (boss.Start != null)
        {
            IReadOnlyList<SabberStoneCore.Model.Card> CardsOf(List<string> ids) => ids.Select(Cards.FromId).ToList();
            plan.Start = new RunStart
            {
                BossBoard = CardsOf(boss.Start.BossBoard), PlayerBoard = CardsOf(boss.Start.PlayerBoard), BossDeckAdd = CardsOf(boss.Start.BossDeckAdd),
                PlayerDeckAdd = CardsOf(boss.Start.PlayerDeckAdd), BossHandAdd = CardsOf(boss.Start.BossHandAdd), PlayerHandAdd = CardsOf(boss.Start.PlayerHandAdd),
                BossWeapon = boss.Start.BossWeapon == null ? null : Cards.FromId(boss.Start.BossWeapon), BossArmor = boss.Start.BossArmor,
            };
            plan.BossOpeningHandBonus = boss.Start.BossOpeningHandBonus;
        }
        // Forms: form 0 is the row itself. Persistent damage puts the boss in the form its remaining Health belongs to, with
        // that form's hero, power, Health and deck already in the plan (RunMatchPlan.BossFormStart).
        int full = plan.BossHealth;
        if (boss.Forms.Count > 0)
        {
            var forms = new List<RunForm> { new RunForm { Hero = plan.BossHero, Power = Cards.FromId(DungeonRunSetup.PowerCardId(boss.Power)), Health = full } };
            foreach (var f in boss.Forms)
                forms.Add(new RunForm
                {
                    Hero = f.Hero == null ? null : Cards.FromId(f.Hero), Power = f.Power == null ? null : Cards.FromId(f.Power), Health = f.Health,
                    HealthAtMost = f.HealthAtMost, OnDeath = f.OnDeath, KeepExcess = f.KeepExcess, Draw = f.Draw,
                    Deck = f.Deck == null ? null : Data.BossDecks[f.Deck].Select(Cards.FromId).ToList(),
                });
            plan.BossForms = forms;
        }
        if (boss.HasFlag(DungeonRunEncounterData.PersistentDamage))
        {
            int remaining = PersistentRemaining(state, profile);
            int start = 0;
            for (int k = 0; k < boss.Forms.Count; k++)
                if (boss.Forms[k].HealthAtMost > 0 && remaining <= boss.Forms[k].HealthAtMost) start = k + 1;
            if (start > 0)
            {
                var form = boss.Forms[start - 1];
                // The hero, power and deck of the last form at or before the start form that names one.
                string hero = boss.Forms.Take(start).Select(f => f.Hero).LastOrDefault(h => h != null);
                string power = boss.Forms.Take(start).Select(f => f.Power).LastOrDefault(x => x != null);
                string deck = boss.Forms.Take(start).Select(f => f.Deck).LastOrDefault(d => d != null);
                if (hero != null) plan.BossHero = Cards.FromId(hero);
                if (power != null) plan.BossHeroPowerId = power;
                if (deck != null) plan.BossDeck = Data.BossDecks[deck].Select(Cards.FromId).ToList();
                if (form.Health > 0) plan.BossHealth = form.Health;
                plan.BossFormStart = start;
            }
            plan.BossCarriedDamage = Math.Max(0, plan.BossHealth - remaining);
        }
        // The fight-only heroes: hero cards go to the hand after the mulligan; relay heroes enter when the hero before dies.
        string own = RunClass(state).HeroCard;
        foreach (var h in boss.HeroCards.Where(h => !h.SkipFor.Contains(own)))
            plan.PlayerHeroCards.Add(new RunFightHero { Hero = Cards.FromId(h.Hero), Deck = h.Deck.Select(Cards.FromId).ToList(), Passives = h.Passives.ToList() });
        foreach (var h in RelayFor(state, boss))
            plan.PlayerRelay.Add(new RunFightHero
            {
                Hero = Cards.FromId(h.Hero), Power = Cards.FromId(DungeonRunSetup.PowerCardId(h.Power)), Health = h.Health,
                Deck = Data.BossDecks[h.Deck].Select(Cards.FromId).ToList(),
            });
    }

    // After a fight that was not a fault: the summary's encounter facts, persistent boss damage (profile scope), Battle Scars'
    // carried hero damage, and the end of the floor's hero switch. The run's Class, Hero, Power, Deck and Cards are untouched.
    private void RecordEncounterEnd(DungeonRunState state, DungeonRunProfile profile, DungeonRunOutcome outcome, RunMatchEnd end, DungeonRunMatchSummary summary)
    {
        var boss = Data.Bosses.FirstOrDefault(b => b.Id == summary.Boss);
        var rules = state.IsActRun && state.ActiveRules.Any(r => r.Kind != null) ? ActiveGlobalRules(state) : new List<DungeonRunGlobalRule>();
        if (rules.Count > 0) summary.Rules = string.Join(",", rules.Select(r => r.Id));
        bool ownHero = end == null || end.HeroesUsed.Count <= 1;
        if (end != null)
        {
            if (!ownHero) summary.HeroesUsed = string.Join(",", end.HeroesUsed);
            summary.BossForm = end.BossForm;
            if (outcome != DungeonRunOutcome.Win) summary.BossRemaining = end.BossRemainingHealth;
        }
        if (boss != null && boss.HasFlag(DungeonRunEncounterData.PersistentDamage) && ReadsLedger(state) && profile != null)
        {
            if (profile.BossDamage == null) profile.BossDamage = new List<DungeonRunBossDamage>();
            var record = profile.DamageOf(boss.Id);
            if (outcome == DungeonRunOutcome.Win && !summary.NoLoss) profile.BossDamage.RemoveAll(d => d.Boss == boss.Id);
            else if (end != null && outcome != DungeonRunOutcome.Win)
            {
                int full = boss.Health ?? end.BossRemainingHealth;
                int remaining = Math.Max(1, Math.Min(end.BossRemainingHealth, record?.Remaining ?? int.MaxValue));
                if (remaining < full)
                {
                    if (record == null) profile.BossDamage.Add(record = new DungeonRunBossDamage { Boss = boss.Id });
                    record.Remaining = remaining;
                    record.Attempts++;
                }
            }
        }
        // Family 24: a persistent-damage fight spends the damage ULDA_115 banked for it; every other fight banks its casts.
        if (boss != null && boss.HasFlag(DungeonRunEncounterData.PersistentDamage)) state.PlagueLordDamage = 0;
        else if (boss != null && end != null && end.GnomebliteratorDamage > 0) state.PlagueLordDamage += end.GnomebliteratorDamage;
        if (outcome == DungeonRunOutcome.Win && end != null && ownHero && rules.Any(r => r.Id == BattleScars))
            state.HeroDamage = Math.Max(0, Math.Min(end.PlayerHeroDamage, BattleScarsHealth - 1));
        state.HeroSwitch = null;
    }

    // ------------------------------------------------------------------ seeded helpers

    private static T WeightedPick<T>(IList<T> items, Func<T, int> weight, long seed, int floor, long step, int draw)
    {
        int total = items.Sum(weight);
        int roll = StableSeed.Index(StableSeed.Derive(seed, DraftStream, floor, step, draw), total);
        foreach (var item in items)
        {
            roll -= weight(item);
            if (roll < 0) return item;
        }
        return items[items.Count - 1];
    }

    private static void Shuffle<T>(IList<T> items, long seed, long stream, long component)
    {
        for (int i = items.Count - 1; i > 0; i--)
        {
            int j = StableSeed.Index(StableSeed.Derive(seed, stream, component, i), i + 1);
            T swap = items[i]; items[i] = items[j]; items[j] = swap;
        }
    }
}

/// <summary>Who the run player is in one fight (DungeonRun.MatchIdentity): the run's own values, and the fight-only heroes.</summary>
internal sealed class DungeonRunMatchIdentity
{
    internal string Name, HeroCard, Power;
    /// <summary>The run row's Health (0: the portrait's printed Health).</summary>
    internal int Health;
    internal IReadOnlyList<string> Deck, Classes;
    /// <summary>The fight-only heroes of the current fight, or null.</summary>
    internal DungeonRunHeroSwitch Switch;
}
