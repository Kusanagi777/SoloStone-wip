using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.CardSets.DungeonRun;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
// The client assembly has a global Card type that shadows the engine's (see LocalNativeText.cs).
using EngineCard = SabberStoneCore.Model.Card;

/// <summary>
/// Load-time checks (design R6, AC7). Any error disables the mode with the failing id and check named;
/// nothing is skipped silently. <c>clientKnows</c> is ClientBridge.ClientKnowsCard over NativeMatch.TransportCardId
/// (the id the client is actually sent) in the game, and null in
/// headless tests, where the client definition check is reported as not run.
/// </summary>
internal static class DungeonRunValidator
{
    /// <summary>Aya Blackpaw (JAIL_504): its seat rule can put the run player second (StableSeed.ApplyFirstPlayerRules).</summary>
    internal const string AyaId = "JAIL_504";

    // Secrets and deck-dependent cards are decided by the admission table (DungeonRunAdmission): a Secret or deck phrase only
    // makes a card a subject, and the table admits or rejects it per scope (player, boss) with the step that lifts a
    // rejection. Generated cards are filtered by the engine instead (GameConfig.Player1/Player2ExcludeGeneratedSecrets and
    // ExcludedGeneratedIds, filled from DungeonRunAdmission.GeneratedRejects). Catalog pool entries are checked with the
    // table's catalog lifts (a catalog run lets the player generate Secrets); everything a legacy run can hold is checked without.

    /// <summary>Plank width: a deck name longer than this would be cut on the menu board.</summary>
    internal const int MaxDeckName = 20, MaxDeckDescription = 100;

    /// <summary>
    /// Card text as the admission detector phrases (DungeonRunAdmission.Detect) are matched against it: lower case, markup tags removed, the client's hard
    /// spaces ('_') and line-break hints ('&amp;') read as spaces, and every whitespace run (line breaks too) one space, so
    /// "in your deck" split over two lines still matches.
    /// </summary>
    internal static string MatchText(string text)
    {
        string plain = System.Text.RegularExpressions.Regex.Replace((text ?? "").ToLowerInvariant(), "<[^>]*>", "");
        plain = plain.Replace('_', ' ').Replace('&', ' ');
        return System.Text.RegularExpressions.Regex.Replace(plain, @"\s+", " ").Trim();
    }

    // Rune needs of a card list; null when a card is unknown (reported by the card checks).
    private static SabberStoneCore.Actions.RuneRequirements? RuneNeeds(IEnumerable<string> ids)
    {
        try { return SabberStoneCore.Actions.RuneRules.FromCards(ids.Select(Cards.FromId)); }
        catch { return null; }
    }

    /// <summary>
    /// The checks a drafted (pool) card must pass, in order, each as a code and the validator's message: "missing" (not in
    /// the card data), "client-unknown", "not-scripted", "not-in-active-meta", "deck-size", "sideboard", "first-player",
    /// "admission:&lt;family&gt;" and "class". <paramref name="classes"/> is the set the card must fit: null for no class
    /// check, <c>["NEUTRAL"]</c> for neutral cards only, else a neutral card, a card allowed in any listed class (multi-class
    /// cards as in deckbuilding), or a <paramref name="grant"/>. <paramref name="clientKnows"/> null skips the client check.
    /// A "missing" code with a null message is a card the engine returns as null: the validator reports nothing for it.
    /// </summary>
    /// <param name="catalogRun">The card is drafted in a catalog run (a catalog pool entry): the admission table's catalog
    /// lifts apply (DungeonRunAdmission.Classify).</param>
    internal static IEnumerable<(string Code, string Message)> PoolCardFailures(string id, string where, IReadOnlyList<string> classes, bool grant,
        OfflinePoolPolicy policy, Func<string, bool> clientKnows, bool catalogRun = false)
    {
        EngineCard card;
        string missing = null;
        try { card = Cards.FromId(id); }
        catch { card = null; missing = $"not in the card data ({where})"; }
        if (card == null) { yield return ("missing", missing); yield break; }
        if (clientKnows != null && !clientKnows(id)) yield return ("client-unknown", $"no client entity definition ({where})");
        if (!card.Implemented) yield return ("not-scripted", $"not scripted ({where})");
        if (policy != null && !policy.AllowsCard(card)) yield return ("not-in-active-meta", $"not in the active meta ({where})");
        if (card[(GameTag)997] != 0) yield return ("deck-size", $"changes deck size ({where})");
        if (SabberStoneCore.Actions.DeckbuildingRules.HasSideboard(id)) yield return ("sideboard", $"needs deckbuilding selections ({where})");
        if (id == AyaId) yield return ("first-player", $"can change who goes first; the run player always goes first ({where})");
        var verdict = DungeonRunAdmission.Classify(card, id, AdmitScope.Player, null, catalogRun);
        if (!verdict.Admitted) yield return ("admission:" + verdict.Family, DungeonRunAdmission.Message(verdict, where));
        if (classes == null || grant) yield break;
        string required = string.Join("/", classes);
        if (classes.Count == 1 && classes[0] == "NEUTRAL")
        {
            if (card.Class != CardClass.NEUTRAL) yield return ("class", $"class {card.Class}, expected neutral ({where})");
            yield break;
        }
        bool allowed = false, known = classes.Count > 0;
        foreach (string name in classes)
        {
            if (!Enum.TryParse(name, out CardClass cls)) { known = false; break; }
            if (SabberStoneCore.Actions.CardClassRules.IsAllowedInDeck(card, cls)) allowed = true;
        }
        if (!known || !allowed) yield return ("class", $"class {card.Class}, not allowed in {required} ({where})");
    }

    /// <summary>
    /// The first failing check of a drafted card (<see cref="PoolCardFailures"/> without the client check), or null when it
    /// passes. The catalog tool and the runtime call the same predicate.
    /// </summary>
    internal static string PoolCardReason(string id, IReadOnlyList<string> classes, bool grant, OfflinePoolPolicy policy = null, bool catalogRun = false) =>
        PoolCardFailures(id, "catalog", classes, grant, policy ?? OfflinePoolPolicy.Load(), null, catalogRun).Select(f => f.Code).FirstOrDefault();

    /// <summary>A drafted card carries a Death Knight rune cost (a draft could break a Death Knight deck's runes).</summary>
    internal static bool HasRuneCost(string id)
    {
        var runes = RuneNeeds(new[] { id });
        return runes != null && runes.Value.Total != 0;
    }

    /// <summary>The mean cost of a pool entry's candidates (catalog "avgCost"); 0 with no candidate or an unknown card.</summary>
    internal static double CandidateAverageCost(DungeonRunBundle entry)
    {
        if (entry.Candidates.Count == 0) return 0;
        double sum = 0;
        foreach (var c in entry.Candidates)
        {
            EngineCard card;
            try { card = Cards.FromId(c.Id); } catch { return 0; }
            if (card == null) return 0;
            sum += card.Cost;
        }
        return sum / entry.Candidates.Count;
    }

    /// <summary>
    /// The one catalog fact only the game can check: whether the client knows a candidate. Drops every pool candidate the
    /// client does not know (<paramref name="clientKnows"/>), recomputes the entry's average cost, and withdraws an entry
    /// left with fewer than 3 candidates. A dropped quest card is no longer the entry's quest. Returns one line per drop,
    /// quest drop or withdrawal ("catalog: &lt;entry&gt; dropped &lt;id&gt; client-unknown", "catalog: &lt;entry&gt; quest
    /// &lt;id&gt; dropped client-unknown"). Fixed entries are untouched: a client-unknown fixed card stays a validation
    /// error. Keeps <paramref name="clientKnows"/> as <see cref="DungeonRunData.ClientKnows"/> for the pick's recheck.
    /// </summary>
    internal static List<string> DropClientUnknown(DungeonRunData data, Func<string, bool> clientKnows)
    {
        var lines = new List<string>();
        if (data == null || clientKnows == null) return lines;
        data.ClientKnows = clientKnows;
        foreach (var entry in data.Bundles.Where(b => b.IsPool))
        {
            int before = entry.Candidates.Count;
            foreach (var c in entry.Candidates.Where(c => !clientKnows(c.Id)).ToList())
            {
                entry.Candidates.Remove(c);
                lines.Add($"catalog: {entry.Id} dropped {c.Id} client-unknown");
                if (entry.Quest != null && entry.Quest.CardId == c.Id)
                {
                    entry.Quest = null;
                    lines.Add($"catalog: {entry.Id} quest {c.Id} dropped client-unknown");
                }
            }
            if (entry.Candidates.Count == before) continue;
            entry.AvgCost = CandidateAverageCost(entry);
            if (entry.Offerable && entry.Candidates.Count < 3)
            {
                entry.Offerable = false;
                entry.Withdrawn = "client-unknown";
                lines.Add($"catalog: {entry.Id} withdrawn ({entry.Candidates.Count} candidates left)");
            }
        }
        return lines;
    }

    /// <summary>
    /// What a generated starter (phase 6, DungeonRun.GenerateStarter) of row <paramref name="cls"/> draws its
    /// <paramref name="cardClass"/> cards from: the candidates of the catalog's offerable pool entries (grants excluded) and the
    /// cards of its fixed entries whose classes include <paramref name="cardClass"/>, of that card class (no neutral card),
    /// passing the drafted-card checks for the row's classes (<see cref="PoolCardReason"/>, catalog lifts) and carrying no rune
    /// cost; distinct, ordinal order. Empty without a catalog. Here, not in DungeonRun, so the validator (and the catalog tool,
    /// which compiles it without DungeonRun) checks the pool NewRun draws from.
    /// </summary>
    internal static List<string> GeneratorPool(DungeonRunData data, DungeonRunClass cls, string cardClass)
    {
        if (data == null || cls == null || data.CatalogVersion == 0) return new List<string>();
        var ids = data.Bundles.Where(b => b.Offerable && b.ClassSet.Contains(cardClass))
            .SelectMany(b => b.IsPool ? b.Candidates.Where(c => !c.Grant).Select(c => c.Id) : b.Cards)
            .Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToList();
        var policy = OfflinePoolPolicy.Load();
        return ids.Where(id =>
        {
            EngineCard card;
            try { card = Cards.FromId(id); } catch { return false; }
            return card != null && card.Class.ToString() == cardClass && !HasRuneCost(id)
                && PoolCardReason(id, cls.ClassSet, false, policy, catalogRun: true) == null;
        }).ToList();
    }

    /// <summary>The mechanics a catalog entry may require (DungeonRunBundle.RequiresMechanic).</summary>
    internal static readonly string[] Mechanics = { "secret" };

    /// <param name="contentEntries">The run content to check against (null: DungeonRunContent.All). Tests pass synthetic entries.</param>
    internal static List<string> Validate(DungeonRunData data, Func<string, bool> clientKnows, IReadOnlyList<RunContent> contentEntries = null)
    {
        var errors = new List<string>();
        var contentList = contentEntries ?? DungeonRunContent.All;
        var contentByKey = new Dictionary<string, RunContent>(StringComparer.Ordinal);
        foreach (var entry in contentList) if (entry?.Key != null && !contentByKey.ContainsKey(entry.Key)) contentByKey[entry.Key] = entry;
        bool TryContent(string key, out RunContent entry) { entry = null; return key != null && contentByKey.TryGetValue(key, out entry); }
        // DungeonRunSetup.PowerCardId over the checked content.
        string PowerCardId(string keyOrId) => keyOrId == null ? null : TryContent(keyOrId, out var c) ? c.ShellId : keyOrId;
        var policy = OfflinePoolPolicy.Load();
        void Fail(string id, string check) => errors.Add($"{id}: {check}");

        EngineCard Find(string id, string where)
        {
            try { return Cards.FromId(id); }
            catch { Fail(id, $"not in the card data ({where})"); return null; }
        }
        void Client(string id, string where)
        {
            if (clientKnows != null && !clientKnows(id)) Fail(id, $"no client entity definition ({where})");
        }
        // The admission table check (DungeonRunAdmission.Classify) in one scope.
        void Admission(EngineCard card, string id, AdmitScope scope, string where, string text = null)
        {
            var verdict = DungeonRunAdmission.Classify(card, id, scope, text);
            if (!verdict.Admitted) Fail(id, DungeonRunAdmission.Message(verdict, where));
        }
        // A treasure for the run player: admitted in a legacy run, or (DungeonRun.TreasureOfferedOn offers it there only) in a
        // catalog run; the message is the legacy verdict's when neither admits it.
        void TreasureAdmission(EngineCard card, string id, string where)
        {
            var verdict = DungeonRunAdmission.Classify(card, id, AdmitScope.Player, null);
            if (verdict.Admitted || DungeonRunAdmission.Classify(card, id, AdmitScope.Player, null, catalogRun: true).Admitted) return;
            Fail(id, DungeonRunAdmission.Message(verdict, where));
        }
        // A drafted or starter card (PoolCardFailures): the class set it must fit, or a grant of its own entry.
        void PoolCard(string id, string where, IReadOnlyList<string> classes, bool grant = false, bool catalogRun = false)
        {
            foreach (var failure in PoolCardFailures(id, where, classes, grant, policy, clientKnows, catalogRun))
                if (failure.Message != null) Fail(id, failure.Message);
        }
        // A hero power in the scope that uses it: player powers, legacy (version-1) powers and shrines ask Player, boss powers Boss.
        void Power(string keyOrId, string where, AdmitScope scope)
        {
            string id = PowerCardId(keyOrId);
            var card = Find(id, where);
            if (card == null) return;
            Client(id, where);
            if (card.Type != CardType.HERO_POWER) Fail(id, $"is {card.Type}, not a hero power ({where})");
            if (!card.Implemented) Fail(id, $"not scripted ({where})");
            Admission(card, id, scope, where);
            if (keyOrId != id) Admission(null, keyOrId, scope, where);
        }
        // A power a player starts a run with: clickable, never choose-one or a Secret, and every minion it summons is known. A
        // named hero row (phase 6, heroRow) may also hold a passive power the engine activates at game start
        // (DungeonRunCardsGen.PassiveHeroPowers: Elise_HP3, Brann_HP3) and a Choose One power whose two options are both
        // scripted and admitted (Elise_HP1).
        void PlayerPower(string keyOrId, string where, bool heroRow = false)
        {
            Power(keyOrId, where, AdmitScope.Player);
            if (TryContent(keyOrId, out var content))
            {
                bool passiveOk = heroRow && content.Kind == RunContentKind.PassivePower;
                if (content.Kind != RunContentKind.HeroPower && !passiveOk) Fail(keyOrId, $"is {content.Kind}, not a clickable hero power ({where})");
                foreach (string token in content.Tokens)
                {
                    var minion = Find(token, where + " token");
                    if (minion == null) continue;
                    Client(token, where + " token");
                    if (!minion.Implemented) Fail(token, $"not scripted ({where} token)");
                    if (minion.Type != CardType.MINION) Fail(token, $"is {minion.Type}, not a minion ({where} token)");
                    Admission(minion, token, AdmitScope.Player, where + " token");
                }
            }
            EngineCard shell;
            try { shell = Cards.FromId(PowerCardId(keyOrId)); } catch { return; }
            if (shell == null) return;
            if (shell[GameTag.CHOOSE_ONE] != 0)
            {
                if (!heroRow) Fail(keyOrId, $"is a choose-one power ({where})");
                else
                {
                    var options = DungeonRunSetup.ChooseOneOptionIds(shell);
                    if (options.Count != 2) Fail(keyOrId, $"is a choose-one power whose options do not resolve ({where})");
                    foreach (string option in options)
                    {
                        var optionCard = Find(option, where + " option");
                        if (optionCard == null) continue;
                        Client(option, where + " option");
                        if (!optionCard.Implemented) Fail(option, $"not scripted ({where} choose-one option)");
                        Admission(optionCard, option, AdmitScope.Player, where + " choose-one option");
                    }
                }
            }
            if (shell[GameTag.SECRET] != 0) Fail(keyOrId, $"is a Secret ({where})");
            if (SabberStoneCore.Model.Entities.HeroPower.ActivatesAtGameStart(shell)
                && !(heroRow && DungeonRunCardsGen.PassiveHeroPowers.Contains(shell.Id)))
                Fail(keyOrId, $"is a passive power ({where})");
        }
        void Hero(string id, string where)
        {
            var card = Find(id, where);
            if (card == null) return;
            Client(id, where);
            if (card.Type != CardType.HERO) Fail(id, $"is {card.Type}, not a hero ({where})");
        }

        // Starter decks and bundles
        var deckIds = new HashSet<string>();
        foreach (var deck in data.Decks)
        {
            string where = "deck " + deck.Id;
            if (!deckIds.Add(deck.Id)) Fail(where, "listed twice");
            var owner = data.FindClass(deck.Class);
            if (deck.Build)
            {
                // A build deck (Build Deck): the player builds the starter, so it lists no cards and has no generator; it shows
                // a display card and is never locked. The class loop below counts it apart from the fixed decks.
                if (deck.Cards.Count != 0) Fail(where, "a build deck lists cards");
                if (deck.IsGenerated) Fail(where, "a build deck has no generator");
                if (deck.DisplayCard == null) Fail(where, "a build deck needs a displayCard");
                else { Find(deck.DisplayCard, where + " display card"); Client(deck.DisplayCard, where + " display card"); }
                if (deck.Unlock != null) Fail(where, "a build deck is never locked");
            }
            else if (deck.IsGenerated)
            {
                // A generated starter (phase 6): no listed cards, a known generator, a display card, and a pool big enough
                // for a starter of every class it draws from (DungeonRun.GeneratorPool, the same predicate NewRun uses).
                if (deck.Cards.Count != 0) Fail(where, "a generated deck lists cards");
                if (deck.Generator != DungeonRunStarterDeck.GeneratorRandomClass && deck.Generator != DungeonRunStarterDeck.GeneratorRandomDual)
                    Fail(where, $"unknown generator {deck.Generator}");
                else if (deck.Generator == DungeonRunStarterDeck.GeneratorRandomDual && owner != null && owner.ClassSet.Count != 2)
                    Fail(where, "a random-dual deck needs a dual-class row");
                if (deck.DisplayCard == null) Fail(where, "a generated deck needs a displayCard");
                else { Find(deck.DisplayCard, where + " display card"); Client(deck.DisplayCard, where + " display card"); }
                if (data.CatalogVersion == 0) Fail(where, "a generated deck needs the shared draft catalog");
                else if (owner != null)
                {
                    var classes = deck.Generator == DungeonRunStarterDeck.GeneratorRandomDual ? owner.ClassSet.ToList() : new List<string> { owner.ClassSet[0] };
                    foreach (string c in classes)
                    {
                        int need = classes.Count == 1 ? data.Rules.StarterSize : (c == classes[0] ? data.Rules.StarterSize / 2 : data.Rules.StarterSize - data.Rules.StarterSize / 2);
                        int have = GeneratorPool(data, owner, c).Count;
                        if (have < need) Fail(where, $"its generator pool holds {have} {c} cards, needs {need}");
                    }
                }
            }
            else if (deck.Cards.Count != data.Rules.StarterSize) Fail(where, $"has {deck.Cards.Count} cards, expected {data.Rules.StarterSize}");
            if (string.IsNullOrEmpty(deck.Name) || deck.Name.Length > MaxDeckName) Fail(where, $"name must be 1 to {MaxDeckName} characters");
            if (string.IsNullOrEmpty(deck.Description) || deck.Description.Length > MaxDeckDescription) Fail(where, $"description must be 1 to {MaxDeckDescription} characters");
            if (owner == null) Fail(where, $"unknown class {deck.Class}");
            if (deck.Unlock != null && !data.Unlocks.ContainsKey(deck.Unlock)) Fail(where, $"unknown unlock {deck.Unlock}");
            // A dual-class row's deck (class "A_B") is checked against the row's class set in the class loop below; a single-class
            // hero row's deck (class = the row id, phase 6) against its one class. A hero row loads with the shared draft catalog
            // only, so every run of it is a catalog run and its starters are checked with the catalog lifts (the Heist's
            // Hydrologist UNG_011 and Desperate Measures DAL_141 are secret-generated cards, admitted on catalog runs).
            bool dualDeck = owner?.ClassSet.Count == 2;
            IReadOnlyList<string> deckClasses = owner?.IsHero == true ? owner.ClassSet : deck.Class == null ? null : new[] { deck.Class };
            if (!dualDeck) foreach (string id in deck.Cards) PoolCard(id, where, deckClasses, catalogRun: owner?.IsHero == true);
            // Death Knight runes: a deck may need at most 3 runes in all (RuneRules, as deckbuilding).
            if (Enum.TryParse(owner?.IsHero == true ? owner.ClassSet[0] : deck.Class, out CardClass deckClass))
            {
                var runes = RuneNeeds(deck.Cards);
                if (runes != null && !SabberStoneCore.Actions.RuneRules.IsLegalForClass(runes.Value, deckClass))
                    Fail(where, $"needs {runes.Value.Blood} Blood, {runes.Value.Frost} Frost and {runes.Value.Unholy} Unholy runes, more than 3 in all");
            }
        }
        var bundleIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var bundle in data.Bundles)
        {
            if (!bundleIds.Add(bundle.Id)) Fail(bundle.Id, "listed twice");
            if (bundle.IsPool) { CatalogEntry(bundle); continue; }
            if (bundle.Kind != DungeonRunBundle.KindFixed) Fail(bundle.Id, $"draw {bundle.Kind}, expected fixed or pool");
            if (bundle.Cards.Count != 3) Fail(bundle.Id, $"has {bundle.Cards.Count} cards, expected 3");
            IReadOnlyList<string> classes = bundle.Classes.Count > 0 ? bundle.Classes : bundle.Class == null ? null : new[] { bundle.Class };
            foreach (string id in bundle.Cards) PoolCard(id, bundle.Id, classes);
            // A drafted bundle must never push a starter deck over its rune limit, so bundle cards carry no rune cost.
            var runes = RuneNeeds(bundle.Cards);
            if (runes != null && runes.Value.Total != 0) Fail(bundle.Id, "has a card with a rune cost (a draft could break a Death Knight deck's runes)");
        }
        // A pool entry of the shared catalog (catalog-v1.json): its ids, label and theme, its classes and requirements, and
        // every candidate as a drafted card of the entry's class set (or a grant of the entry), with no rune cost. The
        // catalog tool admitted every candidate with the same predicate (PoolCardReason), so any failure here means the
        // catalog is stale against the engine or the admission rules: run tools/dungeon-run-catalog again.
        void CatalogEntry(DungeonRunBundle entry)
        {
            string id = entry.Id;
            if (id == null || !DungeonRunData.EntryIdPattern.IsMatch(id)) Fail(id ?? "(no id)", "catalog entry id must match " + DungeonRunData.EntryIdPattern);
            if (string.IsNullOrEmpty(entry.LabelKey)) Fail(id, "no labelKey");
            if (string.IsNullOrEmpty(entry.Label)) Fail(id, "no label");
            if (string.IsNullOrEmpty(entry.PrimaryTheme)) Fail(id, "no primary theme");
            if (entry.Legacy) Fail(id, "a pool entry cannot be legacy");
            if (entry.Cards.Count != 0) Fail(id, "a pool entry lists fixed cards");
            if (entry.Weight < 1) Fail(id, $"weight {entry.Weight}, must be at least 1");
            if (entry.Classes.Count == 0) Fail(id, "no classes");
            foreach (string c in entry.Classes)
                if (c != "NEUTRAL" && (!Enum.TryParse(c, out CardClass cc) || cc == CardClass.NEUTRAL || cc == CardClass.INVALID)) Fail(id, $"unknown class {c}");
            if (entry.Classes.Contains("NEUTRAL") && entry.Classes.Count > 1) Fail(id, "a neutral entry has no other class");
            foreach (string c in entry.ClassesAll.Where(c => !entry.Classes.Contains(c))) Fail(id, $"classesAll names {c}, not one of its classes");
            if (entry.RequiresMechanic != null && !Mechanics.Contains(entry.RequiresMechanic)) Fail(id, $"unknown mechanic {entry.RequiresMechanic}");
            if (!entry.Offerable && string.IsNullOrEmpty(entry.Withdrawn)) Fail(id, "withdrawn with no reason");
            int needed = 3;
            int count = entry.Distinct ? entry.Candidates.Select(c => c.Id).Distinct().Count() : entry.Candidates.Count;
            if (entry.Offerable && count < needed) Fail(id, $"has {count} {(entry.Distinct ? "distinct " : "")}candidates, needs at least {needed}");
            if (entry.Candidates.Select(c => c.Id).Distinct().Count() != entry.Candidates.Count) Fail(id, "lists a candidate twice");
            foreach (var c in entry.Candidates)
            {
                if (c.Weight < 1) Fail(id, $"candidate {c.Id} has weight {c.Weight}, must be at least 1");
                // A pool entry is offered to catalog runs only, so its candidates are checked with the catalog lifts.
                PoolCard(c.Id, id, entry.Classes, c.Grant, catalogRun: true);
                if (HasRuneCost(c.Id)) Fail(id, $"candidate {c.Id} has a rune cost (a draft could break a Death Knight deck's runes)");
            }
            if (entry.Quest != null && !entry.Candidates.Any(c => c.Id == entry.Quest.CardId)) Fail(id, $"quest {entry.Quest.CardId} is not a candidate");
            double average = CandidateAverageCost(entry);
            if (Math.Abs(average - entry.AvgCost) > 0.01)
                Fail(id, $"avgCost {entry.AvgCost.ToString(System.Globalization.CultureInfo.InvariantCulture)} but the candidates average {average.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}");
        }

        // Classes: each with its portrait, PowerOptions powers (the basic one first, no unlock) and DeckOptions decks.
        var classIds = new HashSet<string>();
        foreach (var cls in data.Classes)
        {
            string where = "class " + cls.Id;
            if (!classIds.Add(cls.Id)) Fail(where, "listed twice");
            // A single-class row: Id is a class name and Classes is [Id] (or absent). A dual-class row: two distinct classes
            // and Id "A_B". Classes[0] is the primary class: the portrait's class.
            bool IsClass(string name) => Enum.TryParse(name, out CardClass c) && c != CardClass.NEUTRAL && c != CardClass.INVALID;
            var runClasses = cls.ClassSet;
            // A named hero row (phase 6, kind "hero") names its classes explicitly; its id is free (the portrait id for a
            // single-class hero, "A_B" for the Tombs dual rows, which keep their ids so pinned saves and the catalog hold).
            if (cls.Kind != DungeonRunClass.KindClass && cls.Kind != DungeonRunClass.KindHero) Fail(where, $"unknown kind {cls.Kind}");
            if (cls.IsHero)
            {
                if (cls.Classes.Count == 0) Fail(where, "a hero row must list its classes");
                if (cls.Adventure == null || !DungeonRunClass.Adventures.Contains(cls.Adventure)) Fail(where, $"adventure {cls.Adventure ?? "(none)"} must be one of {string.Join(", ", DungeonRunClass.Adventures)}");
                if (IsClass(cls.Id)) Fail(where, "a hero row cannot take a class name as its id");
            }
            else if (cls.Adventure != null || cls.Signatures.Count > 0 || cls.Health != 0) Fail(where, "only a hero row may set adventure, signatures or health");
            if (cls.Health < 0) Fail(where, $"health {cls.Health} must be positive (or absent)");
            // Wave 5: "excludeBosses" names boss rows (the same character as the hero row); only a hero row has a character. The
            // encounter rows load with encounters-v1.json, so an id is checked when encounters are loaded.
            foreach (string boss in cls.ExcludeBosses)
                if (!cls.IsHero) Fail(where, $"excludeBosses {boss}: only a hero row may exclude a boss row");
                else if (data.Bosses.Any(b => b.IsEncounter) && data.Bosses.All(b => b.Id != boss)) Fail(where, $"excludeBosses names unknown boss {boss}");
            // The close-out (family 25): a finale's fixed bosses are fought whatever the act draws exclude, so the default roll
            // leaves out a default schedule whose finale fixes an excluded row (DungeonRun.DefaultSchedule); one must stay open.
            if (cls.IsHero && cls.ExcludeBosses.Count > 0 && data.Schedules != null && data.Schedules.Defaults.Count > 0
                && data.Schedules.Defaults.All(id => data.Schedules.Find(id)?.Acts.Any(a => a.IsFinale && a.FixedBosses.Any(cls.ExcludeBosses.Contains)) == true))
                Fail(where, "excludeBosses: the finale of every default schedule fixes an excluded boss row, so a new run of this row has no schedule");
            if (runClasses.Count == 1)
            {
                if (!IsClass(runClasses[0])) Fail(where, $"classes [{runClasses[0]}] is not a class name");
                else if (!cls.IsHero && runClasses[0] != cls.Id) Fail(where, $"classes [{runClasses[0]}] must be [{cls.Id}]");
                if (!cls.IsHero && !IsClass(cls.Id)) Fail(where, "id is not a class name");
            }
            else if (runClasses.Count == 2)
            {
                if (!runClasses.All(IsClass) || runClasses[0] == runClasses[1]) Fail(where, $"classes [{string.Join(", ", runClasses)}] must be two distinct class names");
                else if (cls.Id != runClasses[0] + "_" + runClasses[1]) Fail(where, $"a dual-class id must be {runClasses[0]}_{runClasses[1]}");
            }
            else Fail(where, $"has {runClasses.Count} classes, expected 1 or 2");
            if (string.IsNullOrEmpty(cls.Name)) Fail(where, "no name");
            Hero(cls.HeroCard, where);
            // A run hero's portrait is a scripted hero card: an adventure hero the engine does not script keeps its adventure
            // stats (the Tombs explorers ULDA_Reno, ULDA_Finley, ULDA_Elise and ULDA_Brann have 15 Health), so a dual-class
            // row uses its primary class's portrait instead.
            try
            {
                var portrait = Cards.FromId(cls.HeroCard);
                if (portrait != null && runClasses.Count > 0 && portrait.Class.ToString() != runClasses[0]) Fail(where, $"portrait {cls.HeroCard} is {portrait.Class}");
                if (portrait != null && portrait.Type == CardType.HERO && !portrait.Implemented) Fail(cls.HeroCard, $"unscripted portrait ({where})");
            }
            catch { }
            // A hero row ships the powers its adventure gave the hero: 1 to PowerOptions (a picker shows the rest as locked).
            if (cls.IsHero ? cls.Powers.Count < 1 || cls.Powers.Count > data.Rules.PowerOptions : cls.Powers.Count != data.Rules.PowerOptions)
                Fail(where, $"has {cls.Powers.Count} hero powers, expected {(cls.IsHero ? "1 to " : "")}{data.Rules.PowerOptions}");
            if (cls.Powers.Distinct().Count() != cls.Powers.Count) Fail(where, "lists a hero power twice");
            for (int i = 0; i < cls.Powers.Count; i++)
            {
                string power = cls.Powers[i], key = cls.PowerUnlock(power);
                PlayerPower(power, where, cls.IsHero);
                if (i == 0 && key != null) Fail(where, $"its basic power {power} must not need an unlock");
                if (i > 0 && key == null) Fail(where, $"power {power} has no unlock");
                if (key != null && !data.Unlocks.ContainsKey(key)) Fail(where, $"unknown unlock {key}");
            }
            foreach (string power in cls.PowerUnlocks.Keys.Where(p => !cls.Powers.Contains(p))) Fail(where, $"unlock for unlisted power {power}");
            // A dual-class row (a Tombs explorer) and any named hero row ship the original starters that pass the pool checks:
            // 1 to DeckOptions fixed decks, and a hero row at most one generated deck beside them (the Heist and Tombs
            // "Random" deck, the adventure's fourth choice). A class row ships exactly DeckOptions fixed decks. Every row has
            // exactly one build-able deck (Build Deck), its last: its generated deck, or else a build deck; neither counts as fixed.
            bool someDecks = cls.IsDual || cls.IsHero;
            var rowDecks = cls.Decks.Select(data.FindDeck).ToList();
            int generatedDecks = cls.IsHero ? rowDecks.Count(d => d != null && d.IsGenerated && !d.Build) : 0;
            int buildDecks = rowDecks.Count(d => d != null && d.Build);
            int fixedDecks = cls.Decks.Count - generatedDecks - buildDecks;
            if (someDecks ? fixedDecks < 1 || fixedDecks > data.Rules.DeckOptions : fixedDecks != data.Rules.DeckOptions)
                Fail(where, $"has {fixedDecks} {(generatedDecks + buildDecks > 0 ? "fixed " : "")}decks, expected {(someDecks ? "1 to " : "")}{data.Rules.DeckOptions}");
            if (generatedDecks > 1) Fail(where, $"has {generatedDecks} generated decks, expected at most 1");
            int buildable = rowDecks.Count(d => d != null && d.IsBuildable);
            if (buildable != 1) Fail(where, $"has {buildable} build-able decks (build or generated), expected 1");
            else if (rowDecks[rowDecks.Count - 1]?.IsBuildable != true) Fail(where, $"its build-able deck {cls.BuildDeck(data).Id} must be its last deck");
            // Signatures (Tombs loadouts): each a signature treasure of this row alone, scripted and admitted (the treasure loop
            // checks its content and admission); the offer size is 1 to the signature count.
            foreach (string sig in cls.Signatures)
            {
                var t = data.FindTreasure(sig);
                if (t == null) { Fail(where, $"unknown signature {sig}"); continue; }
                if (!t.Signature) Fail(where, $"signature {sig} is not a signature treasure");
                if (!t.Scope.Classes.SequenceEqual(new[] { cls.Id }) || t.Scope.Decks.Count > 0 || t.Scope.Powers.Count > 0) Fail(where, $"signature {sig} must be scoped to [{cls.Id}] alone");
            }
            if (cls.Signatures.Distinct().Count() != cls.Signatures.Count) Fail(where, "lists a signature twice");
            if (cls.Signatures.Count > 0 && (cls.SignatureOffer < 1 || cls.SignatureOffer > cls.Signatures.Count))
                Fail(where, $"signatureOffer {cls.SignatureOffer} must be 1 to {cls.Signatures.Count}");
            if (!cls.Enabled) Fail(where, "a disabled row is listed as playable");
            for (int i = 0; i < cls.Decks.Count; i++)
            {
                var deck = data.FindDeck(cls.Decks[i]);
                if (deck == null) { Fail(where, $"unknown deck {cls.Decks[i]}"); continue; }
                if (deck.Class != cls.Id) Fail(where, $"deck {deck.Id} is {deck.Class}");
                // A dual-class row's starter may hold cards of either class (the deck loop checked only deck.Class). A dual hero
                // row (the Tombs explorers) loads with the shared draft catalog only, so its starters get the catalog lifts, as a
                // single-class hero row's do in the deck loop (phase 6 F10: Hydrologist UNG_011 and Hunter's Pack ULD_429).
                if (runClasses.Count == 2)
                    foreach (string id in deck.Cards.Distinct()) PoolCard(id, where + " deck " + deck.Id, runClasses, catalogRun: cls.IsHero);
                if (deck.IsGenerated && !cls.IsHero) Fail(where, $"deck {deck.Id} is generated; only a hero row may have one");
                if (i == 0 && deck.Unlock != null) Fail(where, $"default deck {deck.Id} must not need an unlock");
                if (i > 0 && deck.Unlock == null && !deck.Build) Fail(where, $"deck {deck.Id} has no unlock");
                // Rikkar's build deck names the board shrine of the row's default deck (a built run starts with it on the board).
                if (deck.Build && i > 0 && deck.Shrine != data.FindDeck(cls.Decks[0])?.Shrine)
                    Fail(where, $"build deck {deck.Id} names shrine {deck.Shrine ?? "(none)"}, the default deck {cls.Decks[0]} {data.FindDeck(cls.Decks[0])?.Shrine ?? "(none)"}");
            }
        }
        foreach (var deck in data.Decks)
        {
            int owners = data.Classes.Count(c => c.Decks.Contains(deck.Id));
            if (owners != 1) Fail("deck " + deck.Id, $"listed by {owners} classes, expected 1");
        }
        // Disabled catalog rows: never playable; each names why, and its id is not reused by a playable row.
        foreach (var cls in data.DisabledClasses)
        {
            string where = "disabled class " + cls.Id;
            if (string.IsNullOrEmpty(cls.DisabledReason)) Fail(where, "no disabledReason");
            if (data.FindClass(cls.Id) != null) Fail(where, "a playable row has the same id");
            if (cls.ClassSet.Count != 2 && !cls.IsHero) Fail(where, "only a dual-class catalog row or a hero row may ship disabled");
            // What a disabled hero row ships is never half-valid (phase 6 F9: the Tombs explorers' powers and Random decks ship
            // before family F10 scripts their fixed starters): its powers are run player powers, and its decks, kept aside with
            // it (DisabledDecks), are its own, a generated one with a pool big enough for its starter. Its unlock keys are made
            // with the class ladder once the row is enabled, so they are checked then.
            if (!cls.IsHero) continue;
            if (cls.Powers.Count > data.Rules.PowerOptions) Fail(where, $"has {cls.Powers.Count} hero powers, expected at most {data.Rules.PowerOptions}");
            for (int i = 0; i < cls.Powers.Count; i++)
            {
                PlayerPower(cls.Powers[i], where, heroRow: true);
                if (i == 0 && cls.PowerUnlock(cls.Powers[i]) != null) Fail(where, $"its basic power {cls.Powers[i]} must not need an unlock");
                if (i > 0 && cls.PowerUnlock(cls.Powers[i]) == null) Fail(where, $"power {cls.Powers[i]} has no unlock");
            }
            foreach (string id in cls.Decks)
            {
                var deck = data.DisabledDecks.FirstOrDefault(d => d.Id == id);
                if (deck == null) { Fail(where, $"unknown deck {id}"); continue; }
                if (deck.Class != cls.Id) Fail(where, $"deck {deck.Id} is {deck.Class}");
                if (!deck.IsGenerated) continue;
                if (deck.Cards.Count != 0) Fail(where, $"generated deck {deck.Id} lists cards");
                if (deck.Generator != DungeonRunStarterDeck.GeneratorRandomDual || cls.ClassSet.Count != 2) Fail(where, $"deck {deck.Id}: a disabled dual row's generated deck is random-dual");
                if (deck.DisplayCard == null) Fail(where, $"deck {deck.Id} needs a displayCard");
                else { Find(deck.DisplayCard, where + " display card"); Client(deck.DisplayCard, where + " display card"); }
                if (data.CatalogVersion == 0) { Fail(where, $"deck {deck.Id}: a generated deck needs the shared draft catalog"); continue; }
                for (int c = 0; c < cls.ClassSet.Count; c++)
                {
                    int need = c == 0 ? data.Rules.StarterSize / 2 : data.Rules.StarterSize - data.Rules.StarterSize / 2;
                    int have = GeneratorPool(data, cls, cls.ClassSet[c]).Count;
                    if (have < need) Fail(where, $"deck {deck.Id}: its generator pool holds {have} {cls.ClassSet[c]} cards, needs {need}");
                }
            }
        }
        foreach (string cls in data.ClassTags.Keys) if (data.FindClass(cls) == null) Fail("classTags " + cls, "no such class");
        // A hero row may read its primary class's tags (DungeonRunData.TagsOf).
        foreach (var cls in data.Classes)
            if (!data.ClassTags.ContainsKey(cls.Id) && !(cls.IsHero && cls.ClassSet.Count > 0 && data.ClassTags.ContainsKey(cls.ClassSet[0])))
                Fail("class " + cls.Id, "no classTags entry");
        foreach (var pair in data.PowerUpgrades)
        {
            string where = "power upgrade " + pair.Key;
            if (!data.Classes.Any(c => c.Powers.Contains(pair.Value.From))) Fail(where, $"no row has power {pair.Value.From}");
            if (pair.Value.To == null) Fail(where, "no target power");
            else PlayerPower(pair.Value.To, where);
            if (!DungeonRunTreasure.KnownTrigger(pair.Value.On)) Fail(where, $"unknown trigger {pair.Value.On}");
        }

        // Unlock conditions
        void Condition(string key, DungeonRunCondition c)
        {
            if (c.Class != null && data.FindClass(c.Class) == null) Fail("unlock " + key, $"unknown class {c.Class}");
            if (c.ClassesWon is int n && (n < 1 || n > data.Classes.Count)) Fail("unlock " + key, $"classesWon {n} is out of range");
            foreach (var sub in (c.AllOf ?? new List<DungeonRunCondition>()).Concat(c.AnyOf ?? new List<DungeonRunCondition>())) Condition(key, sub);
        }
        foreach (var pair in data.Unlocks) Condition(pair.Key, pair.Value);

        // Version-1 save map
        foreach (var pair in data.LegacyHeroes)
        {
            if (data.FindClass(pair.Value.Class) == null) { Fail("legacy hero " + pair.Key, $"unknown class {pair.Value.Class}"); continue; }
            if (pair.Value.Power != null) Power(pair.Value.Power, "legacy hero " + pair.Key, AdmitScope.Player);
        }
        foreach (var pair in data.LegacyUnlocks)
            foreach (string key in pair.Value) if (!data.Unlocks.ContainsKey(key)) Fail("legacy unlock " + pair.Key, $"unknown unlock {key}");

        // Treasures
        foreach (var t in data.Treasures)
        {
            // The treasure id itself first: a ledger treasure id (GILA_511, ULDA_045t, ...) is judged by its rule even
            // before its content entry exists. A treasure the table admits for catalog runs only (LOOTA_833's generated
            // Secrets) passes: DungeonRun.TreasureOfferedOn offers it on a catalog run only.
            TreasureAdmission(null, t.Id, t.Id);
            if (t.Runs != DungeonRunTreasure.RunsAll && t.Runs != DungeonRunTreasure.RunsAct && t.Runs != DungeonRunTreasure.RunsLegacy
                && t.Runs != DungeonRunTreasure.RunsEncounters && t.Runs != DungeonRunTreasure.RunsV4)
                Fail(t.Id, $"runs {t.Runs} must be all, act, legacy, encounters or v4");
            // Step 9: a "v4" row needs a schedule file that can stamp a run 4 (no run of a released schedule may meet it).
            // (Data loaded without schedule-v1.json has no act run at all, so it cannot meet the row either.)
            else if (t.Runs == DungeonRunTreasure.RunsV4 && data.Schedules != null && data.Schedules.ScheduleVersion < 4)
                Fail(t.Id, "runs v4 needs scheduleVersion 4");
            // Step 9 (D9): a boss's reward row is its scripted card itself (no content entry), granted only by an encounter row's
            // v4 "reward" (DungeonRun.GrantEncounterReward) to a stamp-4 run as a treasure card of the run deck.
            if (t.Reward)
            {
                var rewardCard = Find(t.Id, t.Id + " reward");
                if (rewardCard != null)
                {
                    Client(t.Id, t.Id + " reward");
                    if (!rewardCard.Implemented) Fail(t.Id, "a reward card must be scripted");
                    if (rewardCard.Type == CardType.HERO || rewardCard.Type == CardType.HERO_POWER || rewardCard.Type == CardType.ENCHANTMENT)
                        Fail(t.Id, $"a reward is a deck card, not a {rewardCard.Type}");
                    TreasureAdmission(rewardCard, t.Id, t.Id + " reward");
                }
                if (t.Offered) Fail(t.Id, "a reward is never offered in a draft (offered: false)");
                if (t.Runs != DungeonRunTreasure.RunsV4) Fail(t.Id, "a reward is granted on a stamp-4 run only (runs v4)");
                if (!t.Active) Fail(t.Id, "a reward is a deck card (active: true)");
                if (t.Signature || t.Shared || t.Universal || t.UpgradesTo != null || !t.Scope.IsEmpty) Fail(t.Id, "a reward has no scope, signature, shared, universal or upgrade");
                if (data.Bosses.Any(b => b.IsEncounter) && !data.Bosses.Any(b => b.V4?.Reward == t.Id)) Fail(t.Id, "a reward no encounter row's v4 block grants");
                continue;
            }
            // Step 9 (D6): a shared treasure is a scoped generic one (a stamp-4 run lifts its scope); an upgrade keeps the flag.
            if (t.Shared)
            {
                if (t.Signature || t.Legacy) Fail(t.Id, "a signature or legacy treasure is never shared");
                if (t.Scope.IsEmpty) Fail(t.Id, "shared names a treasure with no scope (it is offered to every run already)");
                if (t.UpgradesTo != null && data.FindTreasure(t.UpgradesTo) is DungeonRunTreasure next && !next.Shared) Fail(t.Id, $"shared, its upgrade {t.UpgradesTo} is not");
            }
            if (!TryContent(t.Id, out var content)) { Fail(t.Id, "no engine content entry"); continue; }
            // LOOTA_802 Justicar's Ring: offered only to a run whose power it can upgrade, so its scope lists powers and only
            // powers, each with an upgraded form that a run player may use.
            if (content.UpgradesHeroPower)
            {
                if (t.Scope.Powers.Count == 0 || t.Scope.Classes.Count > 0 || t.Scope.Decks.Count > 0)
                    Fail(t.Id, "a hero-power upgrade treasure is scoped by powers only");
                foreach (string p in t.Scope.Powers)
                {
                    if (!DungeonRunContent.HeroPowerUpgrades.TryGetValue(p, out string upgraded)) { Fail(t.Id, $"scope power {p} has no upgraded form (DungeonRunContent.HeroPowerUpgrades)"); continue; }
                    Power(upgraded, t.Id + " upgrade of " + p, AdmitScope.Player);
                }
            }
            // DALA_738 A Prince's Ring: every Death Knight power it may swap in at match start is a run player power.
            if (t.Id == DungeonRunContent.PrincesRingId)
                foreach (string p in DungeonRunContent.PrincesRingPowers) PlayerPower(p, t.Id + " power pool");
            bool active = content.Kind == RunContentKind.ActiveTreasure;
            if (active != t.Active) Fail(t.Id, $"data says {(t.Active ? "active" : "passive")}, content is {content.Kind}");
            if (active)
            {
                var card = Find(content.ShellId, t.Id);
                if (card != null) { Client(content.ShellId, t.Id); if (!card.Implemented) Fail(content.ShellId, $"not scripted ({t.Id})"); }
                // An active treasure shelled by its own id is judged as its treasure id is: a catalog-only admission (GILA_509's
                // generated Secrets) passes, because DungeonRun.TreasureOfferedOn offers it on a catalog run only.
                if (card != null && content.ShellId == t.Id) TreasureAdmission(card, content.ShellId, t.Id);
                else if (card != null) Admission(card, content.ShellId, AdmitScope.Player, t.Id);
                foreach (string token in content.Tokens)
                {
                    var tokenCard = Find(token, t.Id + " token");
                    if (tokenCard != null) Admission(tokenCard, token, AdmitScope.Player, t.Id + " token");
                }
            }
            else if (content.EffectKey != null)
            {
                if (!TryContent(content.EffectKey, out var fx)) Fail(t.Id, $"unknown effect {content.EffectKey}");
                else
                {
                    var fxCard = Find(fx.ShellId, t.Id);
                    if (fxCard != null) { Client(fx.ShellId, t.Id); Admission(fxCard, fx.ShellId, AdmitScope.Player, t.Id + " effect"); }
                }
            }
            if (t.UpgradesTo != null && !data.Treasures.Any(x => x.Id == t.UpgradesTo)) Fail(t.Id, $"upgrades to unknown {t.UpgradesTo}");
            // Phase 6: upgrade triggers and slots, profile upgrades and signatures.
            if (!t.UpgradeOnKnown) Fail(t.Id, $"unknown upgradeOn {t.UpgradeOn}");
            if (t.UpgradeSlot != DungeonRunTreasure.SlotDeck && t.UpgradeSlot != DungeonRunTreasure.SlotPassive)
                Fail(t.Id, $"upgradeSlot {t.UpgradeSlot} must be deck or passive (a power upgrade is a powerUpgrades row)");
            else if ((t.UpgradeSlot == DungeonRunTreasure.SlotDeck) != t.Active && t.UpgradesTo != null) Fail(t.Id, $"upgradeSlot {t.UpgradeSlot} does not fit a {(t.Active ? "active" : "passive")} treasure");
            if (t.UpgradeUnlock != null)
            {
                if (t.UpgradesTo == null) Fail(t.Id, "upgradeUnlock with no upgradesTo");
                if (!data.Unlocks.ContainsKey(t.UpgradeUnlock)) Fail(t.Id, $"unknown upgradeUnlock {t.UpgradeUnlock}");
            }
            if (t.Unlock != null && !data.Unlocks.ContainsKey(t.Unlock)) Fail(t.Id, $"unknown unlock {t.Unlock}");
            if (t.Signature)
            {
                if (t.Offered) Fail(t.Id, "a signature treasure is offered through its loadout only (offered: false)");
                bool listed = data.Classes.Concat(data.DisabledClasses).Any(c => c.Signatures.Contains(t.Id))
                    || data.Treasures.Any(x => x.Signature && x.UpgradesTo == t.Id);
                if (!listed) Fail(t.Id, "a signature treasure no hero row lists");
            }
            if (t.Rarity != "common" && t.Rarity != "rare") Fail(t.Id, $"rarity {t.Rarity}");
            if (t.Legacy && t.Offered) Fail(t.Id, "a legacy treasure must not be offered");
            foreach (string c in t.Scope.Classes) if (data.FindClass(c) == null) Fail(t.Id, $"scope names unknown class {c}");
            foreach (string d in t.Scope.Decks) if (data.FindDeck(d) == null) Fail(t.Id, $"scope names unknown deck {d}");
            foreach (string p in t.Scope.Powers) if (!data.Classes.Any(c => c.Powers.Contains(p))) Fail(t.Id, $"scope names power {p}, which no class has");
            if (t.Pool != null && !DungeonRunClass.Adventures.Contains(t.Pool)) Fail(t.Id, $"pool {t.Pool} must be one of {string.Join(", ", DungeonRunClass.Adventures)}");
        }

        // Bosses
        foreach (var boss in data.Bosses)
        {
            // Phase 8: an imported encounter is checked by its own rules (ValidateEncounter below): deck sizes by tier, no
            // class-fit check (the original lists are mixed-class), admission as a catalog run sees it.
            if (boss.IsEncounter) { ValidateEncounter(boss); continue; }
            Hero(boss.HeroCard, "boss " + boss.Id);
            if (boss.Power != null) Power(boss.Power, "boss " + boss.Id, AdmitScope.Boss);
            if (!data.BossDecks.TryGetValue(boss.Deck, out var deck)) { Fail("boss " + boss.Id, $"unknown deck {boss.Deck}"); continue; }
            if (deck.Count != 30) Fail("boss " + boss.Id, $"deck {boss.Deck} has {deck.Count} cards, expected 30");
            data.BossSideboards.TryGetValue(boss.Deck, out var boards);
            try { SabberStoneCore.Actions.DeckbuildingRules.Validate(deck, boards, FormatType.FT_WILD); }
            catch (Exception e) { Fail("boss " + boss.Id, $"deck {boss.Deck}: {e.Message}"); }
            var heroCard = Find(boss.HeroCard, "boss " + boss.Id);
            foreach (string id in deck.Distinct())
            {
                var card = Find(id, "boss " + boss.Id);
                if (card == null) continue;
                Client(id, "boss " + boss.Id);
                if (!card.Implemented) Fail(id, $"not scripted (boss {boss.Id})");
                if (id == AyaId) Fail(id, $"can change who goes first (boss {boss.Id})");
                Admission(card, id, AdmitScope.Boss, "boss " + boss.Id);
                // The portrait sets the engine's class (Discover and secret pools), so it must match the deck.
                if (heroCard != null && !SabberStoneCore.Actions.CardClassRules.IsAllowedInDeck(card, heroCard.Class))
                    Fail("boss " + boss.Id, $"portrait {boss.HeroCard} is {heroCard.Class} but deck card {id} is {card.Class}");
            }
            foreach (var board in boards ?? new Dictionary<string, string[]>())
                foreach (string id in board.Value) { Find(id, "boss " + boss.Id); Client(id, "boss " + boss.Id); }
            if (boss.Archetype == null) Fail("boss " + boss.Id, "no AI archetype (set \"archetype\" or use a library deck that has one)");
            if (SabberStoneCore.Actions.FabledRules.DeckSize(deck) != 30) Fail("boss " + boss.Id, $"deck {boss.Deck} changes the deck size");
            if (boss.Unlock != null && !data.Unlocks.ContainsKey(boss.Unlock)) Fail("boss " + boss.Id, $"unknown unlock {boss.Unlock}");
        }
        // ---- phase 8: one imported encounter. An encounter is fought on catalog runs only (its pools are schedule-version-3
        // pools), so every admission check reads the catalog verdict.
        void EncounterCard(string id, string where, AdmitScope scope, bool scripted = true)
        {
            var card = Find(id, where);
            if (card == null) return;
            Client(id, where);
            if (scripted && !card.Implemented) Fail(id, $"not scripted ({where})");
            if (id == AyaId) Fail(id, $"can change who goes first ({where})");
            var verdict = DungeonRunAdmission.Classify(card, id, scope, null, catalogRun: true);
            if (!verdict.Admitted) Fail(id, DungeonRunAdmission.Message(verdict, where));
        }
        void EncounterPower(string keyOrId, string where, AdmitScope scope)
        {
            string id = PowerCardId(keyOrId);
            var card = Find(id, where);
            if (card == null) return;
            if (card.Type != CardType.HERO_POWER) Fail(id, $"is {card.Type}, not a hero power ({where})");
            EncounterCard(id, where, scope);
        }
        void EncounterDeck(string deckId, string where, bool combat)
        {
            if (deckId == null || !data.BossDecks.TryGetValue(deckId, out var list)) { Fail(where, $"unknown deck {deckId ?? "(none)"}"); return; }
            if (combat ? list.Count != 30 : list.Count < DungeonRunEncounterData.MinDeck || list.Count > DungeonRunEncounterData.MaxDeck)
                Fail(where, combat ? $"deck {deckId} has {list.Count} cards, expected 30"
                    : $"deck {deckId} has {list.Count} cards, expected {DungeonRunEncounterData.MinDeck} to {DungeonRunEncounterData.MaxDeck}");
            data.BossSideboards.TryGetValue(deckId, out var sideboards);
            try { SabberStoneCore.Actions.DeckbuildingRules.Validate(list, sideboards, FormatType.FT_WILD); }
            catch (Exception e) { Fail(where, $"deck {deckId}: {e.Message}"); }
            if (SabberStoneCore.Actions.FabledRules.DeckSize(list) != 30) Fail(where, $"deck {deckId} changes the deck size");
            foreach (string id in list.Distinct()) EncounterCard(id, where, AdmitScope.Boss);
        }
        void ValidateEncounter(DungeonRunBoss boss)
        {
            string where = "boss " + boss.Id;
            if (!DungeonRunEncounterData.Provenances.Contains(boss.Provenance)) Fail(where, $"unknown provenance {boss.Provenance}");
            if (!DungeonRunEncounterData.Tiers.Contains(boss.Tier)) { Fail(where, $"unknown tier {boss.Tier ?? "(none)"}"); return; }
            if (boss.Band != 0 || boss.Final || boss.Challenge) Fail(where, "an encounter has no band and is never a final or challenge row");
            if (boss.Pools.Count != 1 || boss.Pools[0] != DungeonRunEncounterData.PoolOf(boss.Tier)) Fail(where, "an encounter's pool is its tier's");
            if (boss.Archetype == null) Fail(where, "no AI archetype");
            else try { DungeonRunArchetypes.Check(boss.Archetype); } catch (Exception e) { Fail(where, e.Message); }
            if (boss.Unlock != null && !data.Unlocks.ContainsKey(boss.Unlock)) Fail(where, $"unknown unlock {boss.Unlock}");
            foreach (string flag in boss.Flags.Where(f => !DungeonRunEncounterData.Flags.Contains(f))) Fail(where, $"unknown flag {flag}");
            if (boss.Source == null || !DungeonRunDeckSource.Kinds.Contains(boss.Source.Kind)) Fail(where, $"unknown deck kind {boss.Source?.Kind ?? "(none)"}");
            else if ((boss.Source.Kind != DungeonRunDeckSource.KindAuthentic) != (boss.Source.Substitutions.Count > 0 || boss.Source.Kind == DungeonRunDeckSource.KindCurated))
                Fail(where, $"deck kind {boss.Source.Kind} does not match its {boss.Source.Substitutions.Count} substitutions");
            bool combat = DungeonRunEncounterData.ThirtyCardDeck(boss.Tier, boss.Flags);
            if (boss.FixedHealth && boss.Health == null) Fail(where, "fixedHealth with no health");
            if (boss.HasFlag(DungeonRunEncounterData.PersistentDamage) && boss.Forms.Count == 0 && !boss.FixedHealth)
                Fail(where, "persistentDamage needs forms or fixedHealth");
            if (boss.HasFlag(DungeonRunEncounterData.PersistentDamage) && boss.Health == null) Fail(where, "persistentDamage needs the boss's own health");
            if (boss.HasFlag(DungeonRunEncounterData.Relay) != (boss.Relay.Count > 0)) Fail(where, "flag relay and the relay heroes must go together");
            if (boss.Relay.Count > 0 && boss.Tier != DungeonRunEncounterData.FinaleTier) Fail(where, "a relay belongs to a finale row");
            if (boss.HasFlag(DungeonRunEncounterData.HeroSwitch) != (boss.HeroCards.Count > 0)) Fail(where, "flag heroSwitch and the hero cards must go together");
            if (boss.HasFlag(DungeonRunEncounterData.FormsFlag) != (boss.Forms.Count > 0)) Fail(where, "flag forms and the forms must go together");
            if (boss.HasFlag(DungeonRunEncounterData.ShrineFlag) != boss.Variants.Any(v => v.Shrine != null)) Fail(where, "flag shrine and a shrine variant must go together");
            // Step 9 (lane R): the two prevention flags that follow from the row's own data need that data (one way only: a test
            // fixture's row may leave a flag out), and the row's v4 block.
            bool addsHand = boss.Start != null && boss.Start.BossHandAdd.Count + boss.Start.PlayerHandAdd.Count > 0 || boss.HeroCards.Count > 0;
            if (boss.HasFlag(DungeonRunEncounterData.HandAdd) && !addsHand) Fail(where, "flag handAdd needs a start hand add or hero cards");
            if (boss.HasFlag(DungeonRunEncounterData.ManaIdentity) && boss.Start?.BossMana == null) Fail(where, "flag manaIdentity needs a start bossMana");
            if (data.Treasures.Count > 0)
                foreach (string id in boss.IncompatibleTreasures.Where(id => data.FindTreasure(id) == null)) Fail(where, $"incompatibleTreasures names unknown treasure {id}");
            if (boss.V4 != null)
            {
                foreach (string key in boss.V4.UnknownKeys) Fail(where, $"v4: unknown key {key}");
                if (boss.V4.Tier != null && !DungeonRunEncounterData.Tiers.Contains(boss.V4.Tier)) Fail(where, $"v4: unknown tier {boss.V4.Tier}");
                if (boss.V4.Reward != null && data.Treasures.Count > 0 && data.FindTreasure(boss.V4.Reward) == null) Fail(where, $"v4: reward names unknown treasure {boss.V4.Reward}");
            }

            Hero(boss.HeroCard, where);
            if (boss.Power == null) Fail(where, "an encounter needs a hero power");
            else EncounterPower(boss.Power, where, AdmitScope.Boss);
            EncounterDeck(boss.Deck, where, combat);
            foreach (string id in boss.Cards) EncounterCard(id, where + " card", AdmitScope.Boss);

            // Forms: each names how it is entered; Health thresholds fall; every hero, power and deck card is checked.
            int lastThreshold = boss.Health ?? int.MaxValue;
            for (int k = 0; k < boss.Forms.Count; k++)
            {
                var form = boss.Forms[k];
                string at = where + " form " + (k + 1);
                int ways = (form.HealthAtMost > 0 ? 1 : 0) + (form.OnDeath ? 1 : 0) + (form.Script ? 1 : 0);
                if (ways != 1) Fail(at, "names " + ways + " of healthAtMost, onDeath and script; needs exactly one");
                if (form.HealthAtMost > 0)
                {
                    if (form.HealthAtMost >= lastThreshold) Fail(at, $"healthAtMost {form.HealthAtMost} does not fall below {lastThreshold}");
                    lastThreshold = form.HealthAtMost;
                    if (form.Health > 0 && form.Health < form.HealthAtMost) Fail(at, $"health {form.Health} is below its threshold {form.HealthAtMost}");
                }
                if (form.Hero == null && form.Power == null) Fail(at, "changes neither the hero nor the power");
                if (form.Hero != null) Hero(form.Hero, at);
                if (form.Power != null) EncounterPower(form.Power, at, AdmitScope.Boss);
                if (form.Deck != null) EncounterDeck(form.Deck, at, false);
                if (form.Draw < 0 || form.Draw > 10) Fail(at, $"draw {form.Draw} is outside 0..10");
            }
            // Shrine variants: a scripted board shrine the table admits for a boss; its deck is a full deck of the row's tier.
            var variantIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var v in boss.Variants)
            {
                string at = where + " variant " + (v.Id ?? "(none)");
                if (v.Id == null || !variantIds.Add(v.Id)) Fail(at, "variant ids must be distinct");
                EncounterDeck(v.Deck, at, combat);
                if (v.Shrine == null) continue;
                if (!SabberStoneCore.Actions.ShrineRules.IsScripted(v.Shrine)) Fail(at, $"shrine {v.Shrine} is not a scripted board shrine");
                else EncounterCard(v.Shrine, at + " shrine", AdmitScope.Boss);
            }
            // The start block: boss-side cards as boss cards, player-side cards as the run player's.
            if (boss.Start != null)
            {
                foreach (string id in boss.Start.BossCards.Distinct()) EncounterCard(id, where + " start", AdmitScope.Boss);
                foreach (string id in boss.Start.PlayerCards.Distinct()) EncounterCard(id, where + " start (player)", AdmitScope.Player);
                foreach (string id in boss.Start.BossBoard.Concat(boss.Start.PlayerBoard).Distinct())
                    if (Find(id, where + " start") is EngineCard minion && minion.Type != CardType.MINION) Fail(id, $"is {minion.Type}, a start board needs minions ({where})");
                if (boss.Start.BossBoard.Count > 7 || boss.Start.PlayerBoard.Count > 7) Fail(where, "a start board holds at most 7 minions");
                if (boss.Start.BossWeapon != null && Find(boss.Start.BossWeapon, where + " start") is EngineCard weapon && weapon.Type != CardType.WEAPON)
                    Fail(boss.Start.BossWeapon, $"is {weapon.Type}, not a weapon ({where})");
                if (boss.Start.BossMana is int mana && (mana < 0 || mana > 10)) Fail(where, $"start bossMana {mana} is outside 0..10");
                if (boss.Start.BossOpeningHandBonus < 0 || boss.Start.BossOpeningHandBonus > SabberStoneCore.Config.GameConfig.MaxOpeningHandBonus)
                    Fail(where, $"start bossOpeningHandBonus {boss.Start.BossOpeningHandBonus} is outside 0..{SabberStoneCore.Config.GameConfig.MaxOpeningHandBonus}");
            }
            // The run player's fight-only hero cards: a scripted hero card whose own power the run player may hold, the cards
            // that join the deck, and the passive treasures its Battlecry offers.
            foreach (var h in boss.HeroCards)
            {
                string at = where + " hero card " + h.Hero;
                var card = Find(h.Hero, at);
                if (card == null) continue;
                Client(h.Hero, at);
                if (card.Type != CardType.HERO) Fail(h.Hero, $"is {card.Type}, not a hero card ({at})");
                if (!card.Implemented || DungeonRunEncounters.Find(h.Hero)?.Kind != EncounterCardKind.PlayerHeroCard) Fail(h.Hero, $"not scripted as a fight-only hero card ({at})");
                var power = SabberStoneCore.Model.Cards.GetHeroPower(card[GameTag.HERO_POWER]);
                if (power == null) Fail(h.Hero, $"names no hero power ({at})");
                else EncounterPower(power.Id, at + " power", AdmitScope.Player);
                foreach (string id in h.Deck.Distinct()) EncounterCard(id, at + " deck", AdmitScope.Player);
                foreach (string key in h.Passives)
                    if (!TryContent(key, out var passive) || passive.Kind != RunContentKind.PassiveTreasure) Fail(key, $"is not a passive treasure ({at})");
                foreach (string skip in h.SkipFor) Find(skip, at + " skipFor");
            }
            foreach (var h in boss.Relay)
            {
                string at = where + " relay " + h.Hero;
                Hero(h.Hero, at);
                EncounterPower(h.Power, at, AdmitScope.Player);
                if (h.Health < 1) Fail(at, "a relay hero needs its Health");
                if (h.Deck == null || !data.BossDecks.TryGetValue(h.Deck, out var relayDeck)) { Fail(at, $"unknown deck {h.Deck ?? "(none)"}"); continue; }
                if (relayDeck.Count < DungeonRunEncounterData.MinDeck || relayDeck.Count > DungeonRunEncounterData.MaxDeck) Fail(at, $"deck {h.Deck} has {relayDeck.Count} cards");
                foreach (string id in relayDeck.Distinct()) EncounterCard(id, at + " deck", AdmitScope.Player);
            }
            // Boss-side generated Secrets (DungeonRunAdmission.BossNeedsGeneratedSecrets). A card that needs them on a row that
            // keeps them off would do nothing; a row that turns them on must have a closed, scripted, boss-admitted yield pool.
            var subjects = new List<string> { PowerCardId(boss.Power) };
            subjects.AddRange(boss.Forms.Where(f => f.Power != null).Select(f => f.Power));
            subjects.AddRange(boss.Variants.Where(v => v.Shrine != null).Select(v => v.Shrine));
            foreach (string deckId in new[] { boss.Deck }.Concat(boss.Forms.Select(f => f.Deck)).Concat(boss.Variants.Select(v => v.Deck)).Where(d => d != null))
                if (data.BossDecks.TryGetValue(deckId, out var cards)) subjects.AddRange(cards.Distinct());
            var needing = subjects.Where(id => id != null && DungeonRunAdmission.RuleFor(id)?.BossNeeds == DungeonRunAdmission.BossNeedsGeneratedSecrets).Distinct().ToList();
            if (!boss.BossGeneratedSecrets)
                foreach (string id in needing) Fail(id, $"generates a Secret for the boss, but the row keeps the boss side's generated Secrets off: it would do nothing ({where}; set bossGeneratedSecrets)");
            else
            {
                if (needing.Count == 0) Fail(where, "bossGeneratedSecrets with no card that generates a Secret for the boss");
                // One pool per class a needing card draws from: the class its rule names (Professor Putricide, Streetsmuggler:
                // Hunter), else the boss portrait's class (a power that says "a random class Secret").
                var heroCard = Find(boss.HeroCard, where);
                var classes = new List<CardClass>();
                foreach (string id in needing)
                {
                    CardClass? named = DungeonRunAdmission.RuleFor(id).BossSecretClass ?? heroCard?.Class;
                    if (named is CardClass cls && !classes.Contains(cls)) classes.Add(cls);
                }
                foreach (CardClass cls in classes)
                {
                    var pool = BossSecretPool(cls);
                    if (pool.Count == 0) Fail(where, $"bossGeneratedSecrets, but no scripted {cls} Secret is left for the boss's generation");
                    foreach (var secret in pool)
                        if (!DungeonRunAdmission.Classify(secret, secret.Id, AdmitScope.Boss, null, catalogRun: true).Admitted)
                            Fail(secret.Id, $"the boss's generation can yield it, but the table does not admit it for a boss ({where})");
                }
            }
        }

        // In-match names are keyed by card id: a boss sharing a player hero's portrait would rename the player's hero.
        var playerPortraits = new HashSet<string>(data.Classes.Select(c => c.HeroCard));
        foreach (var boss in data.Bosses.Where(b => playerPortraits.Contains(b.HeroCard)))
            Fail("boss " + boss.Id, $"portrait {boss.HeroCard} is also a player hero's; the in-match name would collide");

        // Every borrowed entity a run can put in play: powers, shrine and boss passives with the effects they attach,
        // treasure effects and the buff and cost enchantments the scripts create. A client patch that drops one must
        // disable the mode at load, never fail mid-fight.
        // Admission by use: content a boss power names (or its effect) is boss content; class and legacy powers, treasures
        // and their effects are the run player's, and so is everything no boss names (version-1 shrines are the player's).
        var bossContent = new HashSet<string>(StringComparer.Ordinal);
        foreach (var boss in data.Bosses.Where(b => b.Power != null))
            if (TryContent(boss.Power, out var used)) { bossContent.Add(used.Key); if (used.EffectKey != null) bossContent.Add(used.EffectKey); }
        var playerContent = new HashSet<string>(data.Classes.SelectMany(c => c.Powers)
            .Concat(data.LegacyHeroes.Values.Where(h => h.Power != null).Select(h => h.Power)).Concat(data.Treasures.Select(t => t.Id)), StringComparer.Ordinal);
        foreach (string key in playerContent.ToList()) if (TryContent(key, out var used) && used.EffectKey != null) playerContent.Add(used.EffectKey);
        AdmitScope ContentScope(RunContent c) =>
            (bossContent.Contains(c.Key) ? AdmitScope.Boss : AdmitScope.None)
            | (playerContent.Contains(c.Key) || !bossContent.Contains(c.Key) ? AdmitScope.Player : AdmitScope.None);
        foreach (var content in contentList.Where(c => c.ShellId != null))
        {
            var card = Find(content.ShellId, "content " + content.Key);
            if (card == null) continue;
            Client(content.ShellId, "content " + content.Key);
            if (!card.Implemented) Fail(content.ShellId, $"not scripted (content {content.Key})");
            // A treasure shelled by its own id (a passive, or an active from phase 6 F3 on: GILA_509) is judged as the treasure is
            // (catalog-only admission allowed).
            if ((content.Kind == RunContentKind.PassiveTreasure || content.Kind == RunContentKind.ActiveTreasure)
                && content.ShellId == content.Key && ContentScope(content) == AdmitScope.Player)
                TreasureAdmission(card, content.ShellId, "content " + content.Key);
            else Admission(card, content.ShellId, ContentScope(content), "content " + content.Key);
            // A named hero portrait (phase 6) is a HERO; a passive treasure keyed by its real id may use that id as its display
            // shell, which the client knows as a SPELL (GILA, DALA, ULDA and LOOTA passives); its aura is an Effect entry.
            if (content.Kind == RunContentKind.PassiveTreasure && content.ShellId != content.Key)
                Fail("content " + content.Key, $"a passive treasure's shell must be its own id, not {content.ShellId}");
            // An active treasure is a spell, a minion or (phase 6 F2: LOOTA_819, 821, 835, 842, 842a) a weapon.
            CardType expected = content.Kind == RunContentKind.Effect ? CardType.ENCHANTMENT
                : content.Kind == RunContentKind.ActiveTreasure ? card.Type == CardType.MINION || card.Type == CardType.WEAPON ? card.Type : CardType.SPELL
                : content.Kind == RunContentKind.Hero ? CardType.HERO
                : content.Kind == RunContentKind.PassiveTreasure ? CardType.SPELL
                : CardType.HERO_POWER;
            if (content.Kind == RunContentKind.Hero && content.Health < 1) Fail(content.ShellId, $"content {content.Key} has no run Health");
            if (card.Type != expected) Fail(content.ShellId, $"is {card.Type}, content {content.Key} needs {expected}");
            if (content.EffectKey != null && !TryContent(content.EffectKey, out _))
                Fail("content " + content.Key, $"unknown effect {content.EffectKey}");
        }

        // Phase 8: a global rule's card and its player enchantment are client cards (ValidateSchedules checks the rest).
        foreach (var rule in data.Schedules?.Rules ?? new List<DungeonRunGlobalRule>())
        {
            if (!rule.Admitted) continue;
            Client(rule.Id, "rule " + rule.Id);
            string effect = DungeonRunGlobalRules.EffectOf(rule.Id);
            if (effect != null) Client(effect, "rule " + rule.Id + " effect");
            // Family 05: the cards a rule creates (its tokens, the enchantments it puts on cards) are scripted client cards too.
            foreach (string made in DungeonRunGlobalRules.CardsOf(rule.Id))
            {
                var madeCard = Find(made, "rule " + rule.Id + " card");
                if (madeCard == null) continue;
                Client(made, "rule " + rule.Id + " card");
                if (!madeCard.Implemented) Fail(made, $"not scripted (rule {rule.Id})");
            }
        }

        for (int band = 1; band <= data.Rules.BandSplit.Length; band++)
        {
            int count = data.Bosses.Count(b => !b.Final && b.Band == band && b.Unlock == null);
            if (count < data.Rules.BandSplit[band - 1]) Fail($"band {band}", $"has {count} bosses, needs {data.Rules.BandSplit[band - 1]}");
        }
        if (!data.Bosses.Any(b => b.Final && b.Unlock == null)) Fail("finals", "no final boss available without an unlock");
        if (data.Rules.BandSplit.Sum() + 1 != data.Rules.FinalFloor || data.Rules.FinalFloor != data.Rules.Floors)
            Fail("rules", "band split plus the final floor must equal the floor count");
        // The eight-floor invariant above is the legacy rules' (ScheduleVersion 0); an act schedule is checked on its own.
        if (data.Schedules != null) errors.AddRange(ValidateSchedules(data));
        if (data.Tavern != null) errors.AddRange(ValidateTavern(data.Tavern, clientKnows));
        errors.AddRange(ValidateShrines(data, clientKnows));
        return errors;
    }

    /// <summary>
    /// Phase 8: the Secrets a boss of <paramref name="cls"/> can put into play on a catalog run with its generated Secrets on
    /// (what DungeonRunContent.RandomSecretCandidates yields for player 2 there): the scripted Secrets of that class in the wild
    /// lists, minus RunMatchPlan.ExcludedGenerated of a catalog run (every unscripted Secret and every card generation never
    /// yields). The boss-only list (ExcludedGeneratedBoss) holds every Secret card, because a boss's Discovers and random cards
    /// never yield one; a power that names "a random Secret" reads this pool instead, and the validator requires each member to
    /// be admitted for a boss. Ordinal order.
    /// </summary>
    internal static List<EngineCard> BossSecretPool(CardClass cls)
    {
        if (!Cards.FormatTypeClassCards(FormatType.FT_WILD).TryGetValue(cls, out var classCards)) return new List<EngineCard>();
        var blocked = new HashSet<string>(DungeonRunAdmission.GeneratedRejects(catalogRun: true), StringComparer.Ordinal);
        return classCards.Where(c => c != null && c.IsSecret && c.Implemented && !blocked.Contains(c.Id))
            .GroupBy(c => c.Id, StringComparer.Ordinal).Select(g => g.First()).OrderBy(c => c.Id, StringComparer.Ordinal).ToList();
    }

    /// <summary>The classes of the Rumble Run teammate table (run-v1.json "teammates"): six teammates each.</summary>
    internal static readonly string[] TeammateClasses = { "DRUID", "HUNTER", "MAGE", "PALADIN", "PRIEST", "ROGUE", "SHAMAN", "WARLOCK", "WARRIOR" };
    internal const int TeammatesPerClass = 6, ShrineUpgradeCount = 9;

    /// <summary>
    /// Phase 7 board shrines, shrine upgrades and teammates. A deck's "shrine" is a client MINION of the deck row's (single)
    /// class, carrying a starter deck (tag 1099), whose number is the deck id's ("rumble_druid_115" names TRLA_115); its dormant
    /// form "&lt;id&gt;t" exists and points back to it (tag 1132 = the shrine's dbf id); a shrine the engine scripts has exactly that
    /// form (ShrineRules.FormOf). The teammate table holds the nine classes, six distinct MINION teammates each, of that class,
    /// 54 in all, none a shrine; every scripted teammate is in it. The shrine-upgrade pool lists nine distinct cards; every
    /// scripted upgrade is in it. With <paramref name="clientKnows"/>, every id is a client card. Data with none of the three
    /// keys (a test's synthetic data) checks nothing here.
    /// </summary>
    internal static List<string> ValidateShrines(DungeonRunData data, Func<string, bool> clientKnows = null)
    {
        var errors = new List<string>();
        void Fail(string id, string check) => errors.Add($"{id}: {check}");
        EngineCard Find(string id) { try { return Cards.FromId(id); } catch { return null; } }
        var shrines = new HashSet<string>(StringComparer.Ordinal);
        foreach (var deck in data.Decks.Concat(data.DisabledDecks).Where(d => d.Shrine != null))
        {
            string where = "deck " + deck.Id;
            shrines.Add(deck.Shrine);
            var card = Find(deck.Shrine);
            if (card == null || card.Type != CardType.MINION) { Fail(where, $"shrine {deck.Shrine} is not a minion card"); continue; }
            if (clientKnows != null && !clientKnows(deck.Shrine)) Fail(where, $"shrine {deck.Shrine} is not a client card");
            var row = data.FindClass(deck.Class) ?? data.DisabledClasses.FirstOrDefault(c => c.Id == deck.Class);
            if (row == null) Fail(where, $"shrine {deck.Shrine}: the deck's class row {deck.Class} is unknown");
            else if (row.ClassSet.Count != 1) Fail(where, $"shrine {deck.Shrine}: a board shrine needs a single-class row ({row.Id} has {row.ClassSet.Count})");
            else if (card.Class.ToString() != row.ClassSet[0]) Fail(where, $"shrine {deck.Shrine} is {card.Class}, the run class is {row.ClassSet[0]}");
            if (card[(GameTag)1099] <= 0) Fail(where, $"shrine {deck.Shrine} names no starter deck (tag 1099)");
            // A build deck's id names no shrine: it copies its row's default deck's (the class loop of Validate checks that).
            int underscore = deck.Id.LastIndexOf('_');
            if (!deck.Build && (underscore < 0 || "TRLA_" + deck.Id.Substring(underscore + 1) != deck.Shrine)) Fail(where, $"shrine {deck.Shrine} is not the shrine the deck id names");
            var form = Find(deck.Shrine + "t");
            if (form == null || form.Type != CardType.MINION) Fail(where, $"shrine {deck.Shrine} has no dormant form {deck.Shrine}t");
            else if (form[(GameTag)1132] != card.AssetId) Fail(where, $"dormant form {form.Id} points to dbf {form[(GameTag)1132]}, not {deck.Shrine} ({card.AssetId})");
            string scripted = SabberStoneCore.Actions.ShrineRules.FormOf(deck.Shrine);
            if (scripted != null && scripted != deck.Shrine + "t") Fail(where, $"shrine {deck.Shrine} is scripted with form {scripted}, expected {deck.Shrine}t");
        }
        if (data.Teammates.Count > 0)
        {
            var all = new HashSet<string>(StringComparer.Ordinal);
            foreach (string cls in TeammateClasses.Where(c => !data.Teammates.ContainsKey(c))) Fail("teammates", $"class {cls} is missing");
            foreach (var pair in data.Teammates.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                string where = "teammates " + pair.Key;
                if (!TeammateClasses.Contains(pair.Key)) Fail(where, "is not a Rumble Run class");
                if (pair.Value.Count != TeammatesPerClass || pair.Value.Distinct().Count() != TeammatesPerClass)
                    Fail(where, $"lists {pair.Value.Count} ({pair.Value.Distinct().Count()} distinct), expected {TeammatesPerClass}");
                foreach (string id in pair.Value)
                {
                    if (!all.Add(id)) Fail(where, $"{id} is listed twice");
                    if (shrines.Contains(id) || SabberStoneCore.Actions.ShrineRules.IsScripted(id)) Fail(where, $"{id} is a shrine");
                    var card = Find(id);
                    if (card == null || card.Type != CardType.MINION) { Fail(where, $"{id} is not a minion card"); continue; }
                    if (card.Class.ToString() != pair.Key) Fail(where, $"{id} is {card.Class}");
                    if (clientKnows != null && !clientKnows(id)) Fail(where, $"{id} is not a client card");
                }
            }
            if (all.Count != TeammateClasses.Length * TeammatesPerClass) Fail("teammates", $"{all.Count} teammates, expected {TeammateClasses.Length * TeammatesPerClass}");
            foreach (string id in DungeonRunShrines.ScriptedTeammates.Where(id => !all.Contains(id)).OrderBy(i => i, StringComparer.Ordinal))
                Fail("teammates", $"scripted teammate {id} is not in the table");
        }
        if (data.ShrineUpgrades.Count > 0)
        {
            if (data.ShrineUpgrades.Count != ShrineUpgradeCount || data.ShrineUpgrades.Distinct().Count() != ShrineUpgradeCount)
                Fail("shrineUpgrades", $"lists {data.ShrineUpgrades.Count}, expected {ShrineUpgradeCount} distinct");
            foreach (string id in data.ShrineUpgrades)
            {
                if (Find(id) == null) Fail("shrineUpgrades", $"{id} is not a card");
                else if (clientKnows != null && !clientKnows(id)) Fail("shrineUpgrades", $"{id} is not a client card");
            }
            foreach (string id in DungeonRunShrines.ScriptedUpgrades.Keys.Where(id => !data.ShrineUpgrades.Contains(id)).OrderBy(i => i, StringComparer.Ordinal))
                Fail("shrineUpgrades", $"scripted upgrade {id} is not in the pool");
        }
        return errors;
    }

    /// <summary>
    /// Bob's tavern table (tavern-v1.json): every option id is a known card (and a client card, when
    /// <paramref name="clientKnows"/> is given), listed once, with a known op, a non-negative cost and weight, and known pool
    /// and sample names; an alias names another option; every "ench" is a client card and a known deck enchantment
    /// (DungeonRunTavernTable.KnownEnchantments); House Special (ULDA_607) is never an option; the board sizes are 1 to 7 and
    /// Bob's cost caps positive. How many cards each pool holds per class depends on the run's catalog and admission, so it
    /// is measured at runtime (DungeonRunTavernNode) and by the c-deckops suite, not here.
    /// </summary>
    internal static List<string> ValidateTavern(DungeonRunTavernTable table, Func<string, bool> clientKnows)
    {
        var errors = new List<string>();
        void Fail(string id, string check) => errors.Add($"tavern {id}: {check}");
        bool Known(string id)
        {
            try { return id != null && Cards.FromId(id) != null; } catch { return false; }
        }
        if (table.FriendlyBoard < 1 || table.FriendlyBoard > 7) Fail("board", $"friendly board {table.FriendlyBoard} is outside 1..7");
        if (table.BobBoard < 1 || table.BobBoard > 7) Fail("board", $"Bob's board {table.BobBoard} is outside 1..7");
        if (table.BobMaxCostByAct.Any(c => c < 1)) Fail("board", "bobMaxCostByAct holds a cap below 1");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var o in table.Options)
        {
            string id = o.Id ?? "(none)";
            if (!ids.Add(id)) Fail(id, "listed twice");
            if (o.Id == DungeonRunTavernTable.HouseSpecial) Fail(id, "House Special has no source decks; it must stay deferred");
            if (!Known(o.Id)) Fail(id, "not in the card data");
            else if (clientKnows != null && !clientKnows(o.Id)) Fail(id, "no client entity definition");
            if (!DungeonRunTavernTable.KnownOps.Contains(o.Op)) Fail(id, $"unknown op {o.Op ?? "(none)"}");
            if (o.Cost < 0) Fail(id, "negative cost");
            if (o.Weight < 0) Fail(id, "negative weight");
            // Step 9: weightByAct (a stamp-4 run's weights by act) and maxPerRun (a stamp-4 run's cap, counted from the run state).
            if (o.WeightByAct.Any(w => w < 0)) Fail(id, "weightByAct holds a negative weight");
            if (o.WeightByAct.Length > 0 && o.AliasOf != null) Fail(id, "an alias is never offered (weight 0); it names no weightByAct");
            if (o.MaxPerRun < 0) Fail(id, "negative maxPerRun");
            if (o.MaxPerRun > 0 && o.Op != "health") Fail(id, $"maxPerRun on op {o.Op}: only a health option's buys are counted by the run (HealthBonus)");
            if (o.Pool != null && !DungeonRunTavernTable.KnownPools.Contains(o.Pool)) Fail(id, $"unknown pool {o.Pool}");
            if (o.Sample != null && !DungeonRunTavernTable.KnownSamples.Contains(o.Sample)) Fail(id, $"unknown sample {o.Sample}");
            // The close-out: "runs" as a treasure's; an alias runs where its target does, so it names none.
            if (!DungeonRunTavernTable.KnownRuns.Contains(o.Runs)) Fail(id, $"unknown runs {o.Runs ?? "(none)"}");
            else if (o.AliasOf != null && o.Runs != DungeonRunTreasure.RunsAll) Fail(id, "an alias names no runs of its own");
            if (o.Ench != null)
            {
                if (!DungeonRunTavernTable.KnownEnchantments.ContainsKey(o.Ench)) Fail(id, $"ench {o.Ench} is not a known deck enchantment");
                if (!Known(o.Ench)) Fail(id, $"ench {o.Ench} is not in the card data");
                else if (clientKnows != null && !clientKnows(o.Ench)) Fail(id, $"ench {o.Ench} has no client entity definition");
            }
        }
        foreach (var o in table.Options.Where(o => o.AliasOf != null))
        {
            var target = table.Option(o.AliasOf);
            if (target == null || target.AliasOf != null) Fail(o.Id, $"aliasOf {o.AliasOf} is not a plain option");
            else if (target.Op != o.Op) Fail(o.Id, $"aliasOf {o.AliasOf} runs {target.Op}, not {o.Op}");
        }
        if (!table.Deferred.Any(d => d.Key == DungeonRunTavernTable.HouseSpecial)) Fail(DungeonRunTavernTable.HouseSpecial, "must be listed as deferred");
        return errors;
    }

    /// <summary>
    /// The act/floor schedules (schedule-v1.json) against the data: the default exists; every schedule passes
    /// <see cref="ValidateSchedule"/>; every modifier id is unique, named, and uses only known knobs (none of which can touch a
    /// boss's deck, power, passive or hero); every boss's incompatibleModifiers names a known modifier; every schedule the
    /// loaded scheduleVersion released (<see cref="DungeonRunSchedules.ReleasedShapes"/>) is still there with the same node
    /// shape, so a schedule edit that would re-map a saved run's Node has to bump scheduleVersion.
    /// </summary>
    internal static List<string> ValidateSchedules(DungeonRunData data)
    {
        var errors = new List<string>();
        var schedules = data.Schedules;
        if (schedules.Find(schedules.Default) == null) errors.Add($"schedules: default {schedules.Default ?? "(none)"} is not a schedule");
        // Phase 8 (scheduleVersion 3): the rolled defaults, the pool sets, the per-schedule stamps and the global rules.
        foreach (string id in schedules.Defaults.Where(id => schedules.Find(id) == null)) errors.Add($"schedules: defaults names unknown schedule {id}");
        // The close-out: the schedule of a data set loaded without encounters must need none (no pool set, no finale).
        if (schedules.DefaultWithoutEncounters != null)
        {
            var plain = schedules.Find(schedules.DefaultWithoutEncounters);
            if (plain == null) errors.Add($"schedules: defaultWithoutEncounters names unknown schedule {schedules.DefaultWithoutEncounters}");
            else if (plain.MixesProvenance || plain.StampVersion >= 3 || plain.Acts.Any(a => a.IsFinale || a.Combat.Any(c => schedules.PoolSets.ContainsKey(c.Pool))))
                errors.Add($"schedules: defaultWithoutEncounters {plain.Id} needs the encounter file (a pool set, the provenance mix or a finale)");
        }
        foreach (var set in schedules.PoolSets.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            if (set.Value.Count == 0) errors.Add($"pool set {set.Key}: is empty");
            if (set.Key == DungeonRunSchedules.LeftoverPool || data.Bosses.Any(b => b.Pools.Contains(set.Key))) errors.Add($"pool set {set.Key}: is also a pool name");
            foreach (string pool in set.Value.Where(p => schedules.PoolSets.ContainsKey(p))) errors.Add($"pool set {set.Key}: {pool} is a pool set (sets do not nest)");
        }
        foreach (var x in schedules.Schedules.Values.OrderBy(x => x.Id, StringComparer.Ordinal))
        {
            if (x.StampVersion < DungeonRunSchedules.OldestScheduleVersion || x.StampVersion > schedules.ScheduleVersion)
                errors.Add($"schedule {x.Id}: version {x.StampVersion} is outside {DungeonRunSchedules.OldestScheduleVersion}..{schedules.ScheduleVersion}");
            else if (!DungeonRunSchedules.ReleasedShapes.TryGetValue(x.StampVersion, out var stamped) || !stamped.ContainsKey(x.Id))
                errors.Add($"schedule {x.Id}: version {x.StampVersion} never released it (DungeonRunSchedules.ReleasedShapes)");
            if (x.Draw != null && x.Draw != DungeonRunSchedule.DrawProvenanceMix && x.Draw != DungeonRunSchedule.DrawProvenanceMixV2) errors.Add($"schedule {x.Id}: unknown draw {x.Draw}");
            // Step 9: the v2 draw and every step-9 tuning key need a stamp-4 schedule (DungeonRun.Tuned reads them; a released
            // schedule must never carry one, or it would read differently from the release).
            else if (x.MixesProvenanceV2 && x.StampVersion < 4) errors.Add($"schedule {x.Id}: draw {x.Draw} needs version 4");
            var v4Keys = new List<string>(x.V4Keys);
            if (x.Acts.Any(a => a.TreasureRareWeight != 0 || a.TreasureCommonWeight != 0)) v4Keys.Add("treasureRarity");
            if (x.Acts.Any(a => !a.BundleAfterTreasure)) v4Keys.Add("bundleAfterTreasure");
            if (x.Acts.Any(a => a.TurnCap != 0)) v4Keys.Add("finale.turnCap");
            if (x.Acts.Any(a => a.FormRatchet)) v4Keys.Add("finale.formRatchet");
            if (x.Acts.Any(a => a.BossDeck.Length > 0)) v4Keys.Add("bossDeck");
            v4Keys = v4Keys.Distinct(StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal).ToList();
            if (v4Keys.Count > 0 && x.StampVersion < 4) errors.Add($"schedule {x.Id}: {string.Join(", ", v4Keys)} need version 4");
            if (x.V4Keys.Contains("drawTuning"))
            {
                if (!x.MixesProvenanceV2) errors.Add($"schedule {x.Id}: drawTuning without draw {DungeonRunSchedule.DrawProvenanceMixV2}");
                if (x.DrawAlphaPermille < 0 || x.DrawAlphaPermille > 1000) errors.Add($"schedule {x.Id}: drawTuning alphaPermille {x.DrawAlphaPermille} is outside 0..1000");
                if (x.DrawPenaltyPermille < 0) errors.Add($"schedule {x.Id}: drawTuning drawnPenaltyPermille {x.DrawPenaltyPermille} is negative");
                if (x.LedgerMajorChance < 0 || x.LedgerMajorChance > 100) errors.Add($"schedule {x.Id}: drawTuning ledgerMajorChance {x.LedgerMajorChance} is outside 0..100");
                if (x.MaxTrlPerAct < 0) errors.Add($"schedule {x.Id}: drawTuning maxTrlPerAct {x.MaxTrlPerAct} is negative");
            }
            if (x.V4Keys.Contains("upgradeEveryFloors") && x.UpgradeEveryFloors < 1) errors.Add($"schedule {x.Id}: upgradeEveryFloors {x.UpgradeEveryFloors} must be at least 1");
            if (x.OffsetCap is int cap && cap < 0) errors.Add($"schedule {x.Id}: offsetCap {cap} is negative");
            foreach (var a in x.Acts)
            {
                if ((a.TreasureRareWeight != 0 || a.TreasureCommonWeight != 0) && (a.TreasureRareWeight < 1 || a.TreasureCommonWeight < 1))
                    errors.Add($"schedule {x.Id} act {a.Id}: treasureRarity needs rare and common weights of at least 1");
                if (a.IsFinale && !a.BundleAfterTreasure) errors.Add($"schedule {x.Id} act {a.Id}: a finale drafts no bundle (bundleAfterTreasure)");
                if (a.TurnCap != 0 && (a.TurnCap < 1 || a.TurnCap > 1000)) errors.Add($"schedule {x.Id} act {a.Id}: turnCap {a.TurnCap} is outside 1..1000");
                if (a.BossDeck.Length > 0 && a.BossDeck.Length != a.Floors) errors.Add($"schedule {x.Id} act {a.Id}: bossDeck has {a.BossDeck.Length} values, needs {a.Floors}");
                if (a.BossDeck.Any(n => n < DungeonRunSchedule.MinBossDeck || n > 60)) errors.Add($"schedule {x.Id} act {a.Id}: bossDeck values must be {DungeonRunSchedule.MinBossDeck} to 60");
            }
            bool usesV3 = x.MixesProvenance || x.Acts.Any(a => a.AnomalyAtStart || a.TwistPool.Count > 0 || a.IsFinale
                || a.Combat.Any(c => schedules.PoolSets.ContainsKey(c.Pool)) || a.MajorPool != null && schedules.PoolSets.ContainsKey(a.MajorPool));
            if (usesV3 && x.StampVersion < 3) errors.Add($"schedule {x.Id}: pool sets, the provenance mix, anomaly nodes, twists and a finale need version 3");
        }
        errors.AddRange(ValidateRules(data));
        // Every released version from DungeonRunSchedules.OldestScheduleVersion to the loaded one: a saved run on an older version
        // still loads (DungeonRun.ScheduleFor), so its schedules must keep their shapes too (phase 7: version 1's under version 2).
        // A schedule released unchanged by several versions is reported once, under the first.
        var pinned = new HashSet<string>(StringComparer.Ordinal);
        for (int version = DungeonRunSchedules.OldestScheduleVersion; version <= schedules.ScheduleVersion; version++)
            if (DungeonRunSchedules.ReleasedShapes.TryGetValue(version, out var released))
                foreach (var pair in released.OrderBy(p => p.Key, StringComparer.Ordinal).Where(p => pinned.Add(p.Key + "	" + p.Value)))
                {
                    var schedule = schedules.Find(pair.Key);
                    if (schedule == null) errors.Add($"schedule {pair.Key}: released with scheduleVersion {version} and missing; saved runs name it (bump scheduleVersion)");
                    else if (DungeonRunSchedules.ShapeDigest(schedule) != pair.Value)
                        errors.Add($"schedule {pair.Key}: its node shape changed (digest {DungeonRunSchedules.ShapeDigest(schedule)}, scheduleVersion {version} shipped {pair.Value}); bump scheduleVersion and keep version {version} loadable");
                }
        // Phase 7's draft flags need version 2 (DungeonRunSchedules.Parse reads them only from 2 on).
        if (schedules.ScheduleVersion < 2 && schedules.Schedules.Values.Any(x => x.Acts.Any(a => a.TeammateAfter.Length + a.ShrineUpgradeAfter.Length > 0)))
            errors.Add("schedules: teammateAfter and shrineUpgradeAfter need scheduleVersion 2");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var m in schedules.Modifiers)
        {
            if (string.IsNullOrEmpty(m.Id)) { errors.Add("modifier: an id is missing"); continue; }
            if (!ids.Add(m.Id)) errors.Add($"modifier {m.Id}: listed twice");
            if (string.IsNullOrEmpty(m.Name)) errors.Add($"modifier {m.Id}: no name");
            // Step 9 (D7): "none" is the modifier offer's skip option on a stamp-4 schedule.
            if (m.Id == DungeonRunGlobalRule.None) errors.Add($"modifier {m.Id}: \"none\" is the offer's skip option");
            foreach (string knob in m.UnknownKnobs) errors.Add($"modifier {m.Id}: unknown knob {knob}");
        }
        foreach (var boss in data.Bosses)
            foreach (string id in boss.IncompatibleModifiers.Where(id => schedules.Modifier(id) == null))
                errors.Add($"boss {boss.Id}: incompatibleModifiers names unknown modifier {id}");
        errors.AddRange(ValidateModifierReasons(data));
        // Step 9: an encounter row's "v4" block (read on a stamp-4 run only): known keys, a combat or major tier, a reward row.
        foreach (var boss in data.Bosses.Where(b => b.V4 != null))
        {
            string where = "boss " + boss.Id + " v4";
            if (!boss.IsEncounter) errors.Add($"{where}: only an encounter row has a v4 block");
            // (Unknown keys and tiers are refused with the encounter rows, in Validate.)
            if (boss.V4.Tier != null)
            {
                if (!DungeonRunEncounterData.Tiers.Contains(boss.V4.Tier) || boss.V4.Tier == DungeonRunEncounterData.FinaleTier) errors.Add($"{where}: tier {boss.V4.Tier} is not a combat or major tier");
                if (boss.Tier == DungeonRunEncounterData.FinaleTier) errors.Add($"{where}: a finale row keeps its tier");
                if (DungeonRunEncounterData.IsCombatTier(boss.V4.Tier) != DungeonRunEncounterData.IsCombatTier(boss.Tier))
                    errors.Add($"{where}: tier {boss.V4.Tier} changes the row's deck size rule (tier {boss.Tier}); a combat row stays a combat row");
            }
            if (boss.V4.Reward != null && data.FindTreasure(boss.V4.Reward)?.Reward != true) errors.Add($"{where}: reward {boss.V4.Reward} is not a reward row of run-v1.json");
            if (boss.V4.Reward != null && boss.Tier == DungeonRunEncounterData.FinaleTier) errors.Add($"{where}: a finale fight grants no reward");
        }
        foreach (var schedule in schedules.Schedules.Values.OrderBy(x => x.Id, StringComparer.Ordinal)) errors.AddRange(ValidateSchedule(schedule, data));
        return errors;
    }

    // A rule states a non-empty reason for one of its exclusion entries (C7).
    private static bool HasReason(DungeonRunGlobalRule rule, string entry) =>
        rule.ExclusionReasons.TryGetValue(entry, out string why) && !string.IsNullOrWhiteSpace(why);

    /// <summary>
    /// Phase 8: the global rules (schedule-v1.json "rules"). Every id is unique and a card; its kind, scope and effect are
    /// known; an admitted rule has an admission row that admits scope Rule (a rule with no row is refused: the table states why
    /// each rule is safe), and one that acts in a match is scripted (DungeonRunGlobalRules); a rejected rule states its reason;
    /// incompatibility lists name known rules, modifiers and boss flags; a boss's incompatibleRules names known rules.
    /// </summary>
    internal static List<string> ValidateRules(DungeonRunData data)
    {
        var errors = new List<string>();
        var schedules = data.Schedules;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rule in schedules.Rules)
        {
            string where = "rule " + (rule.Id ?? "(none)");
            if (string.IsNullOrEmpty(rule.Id)) { errors.Add("rule: an id is missing"); continue; }
            if (!ids.Add(rule.Id)) errors.Add($"{where}: listed twice");
            if (rule.Id == DungeonRunGlobalRule.None) errors.Add($"{where}: \"none\" is the offer's skip option");
            if (rule.Kind != DungeonRunGlobalRule.KindAnomaly && rule.Kind != DungeonRunGlobalRule.KindTwist) errors.Add($"{where}: unknown kind {rule.Kind}");
            if (rule.Scope != DungeonRunGlobalRule.ScopeAct && rule.Scope != DungeonRunGlobalRule.ScopeRun) errors.Add($"{where}: unknown scope {rule.Scope}");
            if (rule.Effect != DungeonRunGlobalRule.EffectMatch && rule.Effect != DungeonRunGlobalRule.EffectRun) errors.Add($"{where}: unknown effect {rule.Effect}");
            if (rule.Status != DungeonRunGlobalRule.StatusAdmitted && rule.Status != DungeonRunGlobalRule.StatusRejected) errors.Add($"{where}: unknown status {rule.Status}");
            if (rule.Weight < 1) errors.Add($"{where}: weight {rule.Weight} must be at least 1");
            EngineCard card;
            try { card = Cards.FromId(rule.Id); } catch { card = null; }
            if (card == null) { errors.Add($"{where}: not in the card data"); continue; }
            var verdict = DungeonRunAdmission.Classify(card, rule.Id, AdmitScope.Rule, null, catalogRun: true);
            if (rule.Admitted)
            {
                if (!verdict.Subject) errors.Add($"{where}: no admission row (DungeonRunAdmission states why each rule is admitted)");
                else if (!verdict.Admitted) errors.Add($"{where}: {DungeonRunAdmission.Message(verdict, "schedule rules")}");
                if (rule.Effect == DungeonRunGlobalRule.EffectMatch && !DungeonRunGlobalRules.IsScripted(rule.Id)) errors.Add($"{where}: not scripted (DungeonRunGlobalRules)");
            }
            else
            {
                if (string.IsNullOrEmpty(rule.Reason)) errors.Add($"{where}: a rejected rule states its reason");
                if (verdict.Subject && verdict.Admitted) errors.Add($"{where}: rejected in the schedule but admitted by the admission table");
            }
            foreach (string flag in rule.IncompatibleFlags.Where(f => !DungeonRunEncounterData.Flags.Contains(f))) errors.Add($"{where}: unknown boss flag {flag}");
            foreach (string m in rule.IncompatibleModifiers.Where(m => schedules.Modifier(m) == null)) errors.Add($"{where}: incompatibleModifiers names unknown modifier {m}");
            // Checked against the loaded classes (a caller that validates the rules alone loads none).
            if (data.Classes.Count > 0)
                foreach (string c in rule.IncompatibleClasses.Where(c => data.FindClass(c) == null)) errors.Add($"{where}: incompatibleClasses names unknown class {c}");
            // Step 9 (lane R): the treasure exclusions, against the loaded treasures (none loaded: not checked).
            if (data.Treasures.Count > 0)
                foreach (string t in rule.IncompatibleTreasures.Where(t => data.FindTreasure(t) == null)) errors.Add($"{where}: incompatibleTreasures names unknown treasure {t}");
            // Step 9 (C7): a reason names an exclusion entry of the rule; from scheduleVersion 4 every entry has one.
            var entries = rule.ExclusionEntries.ToList();
            foreach (string key in rule.ExclusionReasons.Keys.Where(k => !entries.Contains(k)).OrderBy(k => k, StringComparer.Ordinal))
                errors.Add($"{where}: exclusionReasons names {key}, which is not one of its exclusions");
            // (The rule-rule entries are checked below, with their other side.)
            if (schedules.ScheduleVersion >= 4)
                foreach (string entry in entries.Distinct().Where(e => !rule.IncompatibleRules.Contains(e) && !HasReason(rule, e)))
                    errors.Add($"{where}: exclusion {entry} states no reason (exclusionReasons)");
        }
        foreach (var rule in schedules.Rules)
            foreach (string other in rule.IncompatibleRules.Where(o => !ids.Contains(o))) errors.Add($"rule {rule.Id}: incompatibleRules names unknown rule {other}");
        // Step 9 (C7): a rule-rule list is symmetric (both rules list each other; a reason on either side) or declared one-way
        // (only one rule lists the other, and its exclusionReasons says why). Both are read both ways (DungeonRun.RuleCandidates,
        // ForbiddenPairs). From scheduleVersion 4 only, as the reasons are.
        if (schedules.ScheduleVersion >= 4)
            foreach (var rule in schedules.Rules.Where(r => !string.IsNullOrEmpty(r.Id)))
                foreach (string other in rule.IncompatibleRules.Distinct().Where(ids.Contains))
                {
                    var back = schedules.Rule(other);
                    bool symmetric = back != null && back.IncompatibleRules.Contains(rule.Id);
                    if (symmetric && !HasReason(rule, other) && !HasReason(back, rule.Id))
                        errors.Add($"rule {rule.Id}: exclusion {other} (listed on both rules) states no reason on either (exclusionReasons)");
                    else if (!symmetric && !HasReason(rule, other))
                        errors.Add($"rule {rule.Id}: lists {other} one-way (not listed back) with no reason declaring it (exclusionReasons)");
                }
        foreach (var boss in data.Bosses)
            foreach (string id in boss.IncompatibleRules.Where(id => !ids.Contains(id))) errors.Add($"boss {boss.Id}: incompatibleRules names unknown rule {id}");
        return errors;
    }

    /// <summary>
    /// Step 9 (D7, C7): a boss's v4 "incompatibleModifiers" names known modifiers; its "modifierReasons" name known modifiers of
    /// one of its two lists; and a row a schedule of stamp 4 and above can reach (<see cref="ReachableOnTuned"/>) states a
    /// reason for every modifier its v4 block neutralises. A data set with no stamp-4 schedule (every fixture before step 9)
    /// reaches no row, so it needs no reason.
    /// </summary>
    internal static List<string> ValidateModifierReasons(DungeonRunData data)
    {
        var errors = new List<string>();
        var schedules = data.Schedules;
        var reachable = ReachableOnTuned(data);
        foreach (var boss in data.Bosses)
        {
            string where = "boss " + boss.Id;
            var v4 = boss.V4?.IncompatibleModifiers ?? new List<string>();
            foreach (string id in v4.Where(id => schedules.Modifier(id) == null)) errors.Add($"{where}: v4 incompatibleModifiers names unknown modifier {id}");
            foreach (string id in boss.ModifierReasons.Keys.OrderBy(k => k, StringComparer.Ordinal))
                if (schedules.Modifier(id) == null || !boss.IncompatibleModifiers.Contains(id) && !v4.Contains(id))
                    errors.Add($"{where}: modifierReasons names {id}, which is not one of its incompatible modifiers");
            if (!reachable.Contains(boss.Id)) continue;
            foreach (string id in v4.Where(id => !boss.ModifierReasons.TryGetValue(id, out string why) || string.IsNullOrWhiteSpace(why)))
                errors.Add($"{where}: v4 neutralises {id} with no reason (modifierReasons)");
        }
        return errors;
    }

    /// <summary>
    /// The boss rows a schedule of stamp 4 and above can draw (step 9): those of a pool its acts name (a pool set expanded; a row's
    /// v4 tier counts as its pool) and its finales' fixed bosses. Empty when no schedule has stamp 4.
    /// </summary>
    internal static HashSet<string> ReachableOnTuned(DungeonRunData data)
    {
        var schedules = data.Schedules;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var schedule in schedules.Schedules.Values.Where(x => x.StampVersion >= 4))
        {
            var pools = new HashSet<string>(StringComparer.Ordinal);
            foreach (var act in schedule.Acts)
            {
                foreach (string pool in act.Combat.Select(c => c.Pool).Concat(act.MajorPool == null ? new string[0] : new[] { act.MajorPool }))
                    if (schedules.PoolSets.TryGetValue(pool, out var set)) pools.UnionWith(set); else pools.Add(pool);
                ids.UnionWith(act.FixedBosses);
            }
            foreach (var boss in data.Bosses)
                if (boss.Pools.Any(pools.Contains) || boss.V4?.Tier != null && pools.Contains(DungeonRunEncounterData.PoolOf(boss.V4.Tier))) ids.Add(boss.Id);
        }
        return ids;
    }

    /// <summary>
    /// One schedule: deckMin at least the starter size and at most deckMax, deckMax at most 99 (the engine's deck capacity);
    /// at least one act; per act a positive floor count, combat counts summing to floors - 1, bossHealth and bossMana arrays of
    /// length floors (health positive, mana 0 to 10), treasureAfter in 1..floors, taverns in 1..floors - 1 (a tavern never
    /// follows the major), draft options and takes 1 to 3, and known pools; and the distinct-boss feasibility of the whole
    /// sequence: a greedy draw (ordinal id order) over bosses that need no unlock, challenge finals excluded, meets every
    /// combat count and leaves every major pool at least one boss not used by combat.
    /// </summary>
    internal static List<string> ValidateSchedule(DungeonRunSchedule schedule, DungeonRunData data)
    {
        var errors = new List<string>();
        string where = "schedule " + schedule.Id;
        void Fail(string id, string check) => errors.Add($"{id}: {check}");
        if (schedule.DeckMin < data.Rules.StarterSize) Fail(where, $"deckMin {schedule.DeckMin} is below the starter size {data.Rules.StarterSize}");
        if (schedule.DeckMax > 99) Fail(where, $"deckMax {schedule.DeckMax} is above the engine deck capacity 99");
        if (schedule.DeckMax < schedule.DeckMin) Fail(where, $"deckMax {schedule.DeckMax} is below deckMin {schedule.DeckMin}");
        if (schedule.Acts.Count == 0) Fail(where, "has no acts");
        // Phase 8: a finale may be enabled on a version-3 schedule whose fixed bosses all exist as finale rows.
        if (schedule.FinaleEnabled)
        {
            if (schedule.StampVersion < 3) Fail(where, "the finale is enabled; it needs schedule version 3");
            if (schedule.FinaleBosses.Count == 0) Fail(where, "the finale is enabled with no fixed boss");
            foreach (string id in schedule.FinaleBosses)
            {
                var boss = data.Bosses.FirstOrDefault(b => b.Id == id);
                // The encounter rows load with encounters-v1.json, so a finale boss is checked when encounters are loaded (as
                // excludeBosses is); a data set without them never follows this schedule by default (defaultWithoutEncounters).
                if (boss == null) { if (data.Bosses.Any(b => b.IsEncounter)) Fail(where, $"finale boss {id} is not in the data"); }
                else if (boss.Tier != DungeonRunEncounterData.FinaleTier) Fail(where, $"finale boss {id} is not a finale row (tier {boss.Tier ?? "(none)"})");
                else if (boss.Unlock != null) Fail(where, $"finale boss {id} needs an unlock");
            }
            if (schedule.FinaleBosses.Distinct().Count() != schedule.FinaleBosses.Count) Fail(where, "a finale boss is listed twice");
            if (schedule.FinaleHeroRelay && data.Bosses.Any(b => b.IsEncounter) && !schedule.FinaleBosses.Any(id => data.Bosses.FirstOrDefault(b => b.Id == id)?.Relay.Count > 0))
                Fail(where, "heroRelay with no finale boss that lists relay heroes");
        }
        var schedules = data.Schedules;
        bool KnownPool(string pool) => pool == DungeonRunSchedules.LeftoverPool || data.Bosses.Any(b => b.Pools.Contains(pool))
            || schedules != null && schedules.PoolSets.TryGetValue(pool, out var set) && set.All(p => p == DungeonRunSchedules.LeftoverPool || data.Bosses.Any(b => b.Pools.Contains(p)) || p.StartsWith("enc-", StringComparison.Ordinal));
        // Step 9: the v2 draw (a stamp-4 schedule) reads an encounter row's v4 tier.
        bool Member(DungeonRunBoss b, string pool) => schedules == null ? DungeonRunSchedules.InPool(b, pool)
            : schedule.MixesProvenanceV2 ? schedules.MemberTuned(b, pool) : schedules.Member(b, pool);
        var used = new HashSet<string>(StringComparer.Ordinal);
        for (int a = 0; a < schedule.Acts.Count; a++)
        {
            var act = schedule.Acts[a];
            string at = where + " act " + (act.Id ?? (a + 1).ToString());
            if (act.Floors < 1) { Fail(at, $"floors {act.Floors} must be at least 1"); continue; }
            if (act.IsFinale)
            {
                // A synthesized finale act: one fixed boss per floor, curves of that length, nothing drawn and nothing drafted.
                if (act.Floors != act.FixedBosses.Count) Fail(at, $"floors {act.Floors} for {act.FixedBosses.Count} fixed bosses");
                if (act.BossHealth.Length != act.Floors || act.BossHealth.Any(h => h < 1)) Fail(at, "bossHealth needs one positive value per fixed boss");
                if (act.BossMana.Length != act.Floors || act.BossMana.Any(m => m < 0 || m > 10)) Fail(at, "bossMana needs one value 0 to 10 per fixed boss");
                if (act.TavernAtStart && (act.TavernGold < 0 || act.TavernOffers < 1)) Fail(at, "a finale tavern needs tavern gold 0 or more and at least 1 offer");
                // Family 18: the finale's own twist (its "twist" key: acts-v3h's DALA_830 The Finale) is checked as an act's.
                foreach (string id in act.TwistPool.Where(id => schedules?.Rule(id)?.Kind != DungeonRunGlobalRule.KindTwist))
                    Fail(at, $"twist pool names {id}, which is not a twist rule");
                if (act.TwistPool.Count > 0 && (act.TwistChance < 1 || act.TwistChance > 100)) Fail(at, $"twist chance {act.TwistChance} must be 1 to 100");
                foreach (string id in act.FixedBosses) used.Add(id);
                continue;
            }
            foreach (string id in act.TwistPool)
            {
                var twist = schedules?.Rule(id);
                if (twist == null || twist.Kind != DungeonRunGlobalRule.KindTwist) Fail(at, $"twist pool names {id}, which is not a twist rule");
            }
            if (act.TwistPool.Count > 0 && (act.TwistChance < 1 || act.TwistChance > 100)) Fail(at, $"twist chance {act.TwistChance} must be 1 to 100");
            if (act.Combat.Sum(c => c.Count) != act.Floors - 1) Fail(at, $"combat counts sum to {act.Combat.Sum(c => c.Count)}, need floors - 1 = {act.Floors - 1}");
            if (act.BossHealth.Length != act.Floors) Fail(at, $"bossHealth has {act.BossHealth.Length} values, needs {act.Floors}");
            if (act.BossMana.Length != act.Floors) Fail(at, $"bossMana has {act.BossMana.Length} values, needs {act.Floors}");
            if (act.BossHealth.Any(h => h < 1)) Fail(at, "bossHealth values must be positive");
            if (act.BossMana.Any(m => m < 0 || m > 10)) Fail(at, "bossMana values must be 0 to 10");
            foreach (int f in act.TreasureAfter.Where(f => f < 1 || f > act.Floors)) Fail(at, $"treasureAfter {f} is outside 1..{act.Floors}");
            foreach (int f in act.TeammateAfter.Where(f => f < 1 || f > act.Floors)) Fail(at, $"teammateAfter {f} is outside 1..{act.Floors}");
            foreach (int f in act.ShrineUpgradeAfter.Where(f => f < 1 || f > act.Floors)) Fail(at, $"shrineUpgradeAfter {f} is outside 1..{act.Floors}");
            foreach (int f in act.Taverns.Where(f => f < 1 || f >= act.Floors)) Fail(at, $"taverns {f} is outside 1..{act.Floors - 1}");
            if (act.DraftOptions < 1 || act.DraftOptions > 3) Fail(at, $"draft options {act.DraftOptions} must be 1 to 3");
            if (act.DraftTakes < 1 || act.DraftTakes > 3) Fail(at, $"draft takes {act.DraftTakes} must be 1 to 3");
            if (act.Taverns.Length > 0 && (act.TavernGold < 0 || act.TavernOffers < 1)) Fail(at, "a tavern act needs tavern gold 0 or more and at least 1 offer");
            foreach (var slot in act.Combat)
            {
                if (slot.Count < 0) Fail(at, $"pool {slot.Pool} count {slot.Count} is negative");
                if (!KnownPool(slot.Pool)) { Fail(at, $"unknown pool {slot.Pool}"); continue; }
                var pool = data.Bosses.Where(b => b.Unlock == null && !b.Challenge && !used.Contains(b.Id) && Member(b, slot.Pool))
                    .Select(b => b.Id).OrderBy(id => id, StringComparer.Ordinal).ToList();
                if (pool.Count < slot.Count) Fail(at, $"pool {slot.Pool} is infeasible: needs {slot.Count} distinct bosses, has {pool.Count}");
                foreach (string id in pool.Take(slot.Count)) used.Add(id);
            }
            if (act.MajorPool == null || !KnownPool(act.MajorPool)) { Fail(at, $"unknown major pool {act.MajorPool ?? "(none)"}"); continue; }
            var majors = data.Bosses.Where(b => b.Unlock == null && !b.Challenge && !used.Contains(b.Id) && Member(b, act.MajorPool))
                .Select(b => b.Id).OrderBy(id => id, StringComparer.Ordinal).ToList();
            if (majors.Count == 0) Fail(at, $"major pool {act.MajorPool} has no boss left after the combat pools");
            else used.Add(majors[0]);
        }
        return errors;
    }
}

/// <summary>The AI archetypes a boss row may name (DungeonRunBossPilot.ParseArchetype reads the same words).</summary>
internal static class DungeonRunArchetypes
{
    internal static readonly string[] Known = { "aggro", "tempo", "midrange", "control", "combo" };
    internal static void Check(string value)
    {
        if (!Known.Contains(value)) throw new System.IO.InvalidDataException("unknown AI archetype " + (value ?? "(none)"));
    }
}
