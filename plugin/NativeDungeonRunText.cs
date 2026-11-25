// Dungeon Run screen copy and plank ids. Pure text (no Unity), so it can be checked off the client.
// English only, like every SoloStone string; planks and captions must fit the native menu board at 1280x720.
using System.Collections.Generic;
using System.Linq;
using System.Text;

internal static class NativeDungeonRunText
{
    internal const string HomeId = "home.dungeon-run";
    // The home Game Modes face while Dungeon Run is available.
    internal const string HomeLabel = "Dungeon Run";
    // Hover text on the home Fan-made modes face, which opens Dungeon Run.
    internal const string HomeTooltip = "Dungeon Run: three acts of boss fights, one life, a deck you draft as you go.";

    internal const string Header = "Dungeon Run";
    internal const string ClassHeader = "Choose a class", DeckChoiceHeader = "Choose a deck", PowerHeader = "Choose a hero power",
        ConfirmHeader = "Ready?", DeckHeader = "Your deck",
        TreasuresHeader = "Your treasures", AbandonHeader = "Abandon run?", TreasureDraftHeader = "Choose a treasure",
        BundleDraftHeader = "Choose a bundle", VictoryHeader = "Victory!", DefeatHeader = "Run over", UnavailableHeader = "Dungeon Run";
    /// <summary>Phase 7: the shrine-upgrade and teammate drafts, picked on the treasure screen.</summary>
    internal const string ShrineUpgradeDraftHeader = "Choose a shrine upgrade", TeammateDraftHeader = "Choose a teammate";

    internal const string FightLabel = "Fight", DeckLabel = "Deck", TreasuresLabel = "Treasures", AbandonLabel = "Abandon run",
        CloseLabel = "Close", BackLabel = "Back", PreviousLabel = "Previous", NextLabel = "Next", StartLabel = "Start run",
        NewRunLabel = "New run", ConfirmAbandonLabel = "Abandon run";

    // Plank ids (GameObject names of the rows).
    internal const string FightId = "dungeon-run.fight", DeckId = "dungeon-run.deck", TreasuresId = "dungeon-run.treasures",
        AbandonId = "dungeon-run.abandon", ConfirmAbandonId = "dungeon-run.abandon.confirm", CloseId = "dungeon-run.close",
        BackId = "dungeon-run.back", PreviousId = "dungeon-run.prev", NextId = "dungeon-run.next", StartId = "dungeon-run.start",
        NewRunId = "dungeon-run.new", ClassIdPrefix = "dungeon-run.class:", DeckPickIdPrefix = "dungeon-run.deckpick:",
        PowerIdPrefix = "dungeon-run.power:", PickIdPrefix = "dungeon-run.pick:";

    // Phase 6: the Hero step (a class row and its named adventure heroes) and the Signature step (Tombs loadouts).
    internal const string HeroHeader = "Choose a hero", SignatureHeader = "Choose a signature treasure",
        HeroIdPrefix = "dungeon-run.hero:", SignatureIdPrefix = "dungeon-run.signature:";
    internal const string HeroCaption = "The class hero, or a named adventure hero of this class.";
    /// <summary>A hero plank's caption line: "Reno Jackson (Tombs, Mage / Rogue, 30 Health)"; the class row has no adventure.</summary>
    internal static string HeroLine(string name, string adventure, string classes, int health) =>
        name + " (" + (adventure == null ? "" : adventure + ", ") + classes + (health > 0 ? ", " + health + " Health" : "") + ")";
    internal static string SignatureLine(string name, string text) => "Signature: " + Described(name, PowerText(text));
    /// <summary>The portrait tray's description of a run row (NativeDungeonRunPicker): "Mage. Heist hero. 3 decks, 3 hero powers."</summary>
    internal static string TrayDescription(RunHeroOption row) =>
        row == null ? "" : row.ClassName + ". " + (row.Adventure == null ? "Dungeon Run class hero" : row.Adventure + " hero") + ". "
            + row.DeckCount + (row.DeckCount == 1 ? " deck, " : " decks, ") + row.PowerCount + (row.PowerCount == 1 ? " hero power" : " hero powers")
            + (row.SignatureCount > 0 ? ", a signature treasure" : "") + ".";

    internal const string ClassCaption = "Pick a class. A run is three acts of fights; a loss ends it.";
    internal const string DeckCaption = "Each class has three starter decks. Play the class to unlock the others.";
    internal const string PowerCaption = "Your hero power for the whole run.";
    internal const string AbandonCaption = "Abandon this run? It counts as a defeat and cannot be undone.";
    internal const string InMatchCaption = "Fight in progress";
    internal const string NoSaveCaption = "No save is loaded.";
    internal const string EmptyDeckCaption = "Your deck is empty.";
    internal const string NoTreasuresCaption = "No treasures yet.";
    internal const string NoChoicesCaption = "Nothing is on offer.";
    internal const string LockedWord = "Locked";
    // The Build Deck option (a row's build deck: DungeonRunClass.BuildDeck), in every picker; always unlocked.
    internal const string BuildDeckName = "Build Deck", BuildDeckDescription = "Build a 10-card deck from your collection.";
    // The native playmat's filler slots for a row with fewer decks or powers than the prefab shows (NativeDungeonRunPicker).
    internal const string NoOtherDeckName = "No other deck", NoOtherDeckHint = "This hero has no other starting deck.",
        NoOtherPowerHint = "This hero has no other hero power.";

    internal static string Unavailable(string reason) =>
        "Dungeon Run is unavailable: " + (string.IsNullOrEmpty(reason) ? "unknown reason" : reason);
    internal static string Failed(string message) => "Something went wrong: " + message;
    /// <summary>The class page's catalog status: catalog drops at load (each is in the BepInEx log).</summary>
    internal static string CatalogDiagnosticsLine(int count) =>
        "Draft catalog: " + count + (count == 1 ? " note" : " notes") + " at load (cards this client lacks; see the BepInEx log)";

    // "WARRIOR" -> "Warrior"; "DEMONHUNTER" stays one word; a dual-class row "MAGE_ROGUE" -> "Mage / Rogue".
    // A named hero row id (phase 6: "DALA_Rakanishu", "TRLA_209h_Mage") is not a class name: it is returned unchanged, and a
    // picker names such a row by its classes (DungeonRunCatalog.RowClassName).
    internal static string ClassName(string id)
    {
        if (string.IsNullOrEmpty(id)) return "";
        if (id.Any(char.IsLower)) return id;
        int split = id.IndexOf('_');
        if (split > 0 && split < id.Length - 1) return ClassName(id.Substring(0, split)) + " / " + ClassName(id.Substring(split + 1));
        if (id == "DEMONHUNTER") return "Demon Hunter";
        if (id == "DEATHKNIGHT") return "Death Knight";
        return id.Substring(0, 1).ToUpperInvariant() + id.Substring(1).ToLowerInvariant();
    }

    /// <summary>A catalog bundle offer's plank label: the entry label and its adventure tag ("Mana Growth (DR)"); a SoloStone bundle's label alone.</summary>
    internal static string OfferLabel(string label, string source) =>
        string.IsNullOrEmpty(source) || source == "SoloStone" ? label : label + " (" + source + ")";

    internal static string Classes(IEnumerable<string> classes) => string.Join("/", (classes ?? Enumerable.Empty<string>()).Select(ClassName));

    // Planks stay short (no NativeButtonKit.Fit on these rows): "The Spellwright - Mage".
    internal static string ClassLabel(string name, string cls) => name + " - " + ClassName(cls);

    // Locked planks read short; HintLine puts the unlock hint in the caption.
    internal static string LockedChoiceLabel(string name) => name + " (locked)";

    internal static string HintLine(string name, string hint) => name + ": " + hint;

    internal static string MoreReasons(int count) => "...and " + count + " more (see the BepInEx log)";

    // A short unlock hint from one condition: {floorReached 3, class MAGE} -> "Reach floor 3 with Mage to unlock",
    // {runsWon 1, class MAGE} -> "Win a run with Mage to unlock", {classesWon 4} -> "Win runs with 4 different classes to unlock".
    // Combined or mixed conditions are just "Locked".
    internal static string UnlockHint(int? floorReached, int? runsWon, int? classesWon, string className, bool combined)
    {
        int set = (floorReached != null ? 1 : 0) + (runsWon != null ? 1 : 0) + (classesWon != null ? 1 : 0);
        if (combined || set != 1) return LockedWord;
        if (classesWon != null) return "Win runs with " + classesWon + " different classes to unlock";
        string who = string.IsNullOrEmpty(className) ? "" : " with " + className;
        if (floorReached != null) return "Reach floor " + floorReached + who + " to unlock";
        return (runsWon == 1 ? "Win a run" : "Win " + runsWon + " runs") + who + " to unlock";
    }

    internal static string Described(string name, string text) => string.IsNullOrEmpty(text) ? name : name + ": " + text;

    // " . " with a middle dot (U+00B7), kept as a char code so this file stays ASCII.
    private static readonly string Dot = " " + (char)0xB7 + " ";

    internal static string FloorLine(int floor, int floors, int band, bool final) =>
        "Floor " + floor + " of " + floors + Dot + (final ? "Final" : "Band " + band);

    // An act run's progress line: "Act II . Floor 3 of 5 (8 of 15)"; the act's last floor adds ". Major".
    internal static string ActFloorLine(string act, int floorInAct, int actFloors, int floor, int floors, bool major) =>
        "Act " + act + Dot + "Floor " + floorInAct + " of " + actFloors + " (" + floor + " of " + floors + ")" + (major ? Dot + "Major" : "");

    // Act-run node pages (phase 4): the modifier choice and Bob's tavern (phase 5 fills the tavern's offers).
    internal const string ModifierHeader = "Choose a rule", TavernHeader = "Bob's Tavern";
    internal const string LeaveTavernLabel = "Leave tavern", LeaveTavernId = "dungeon-run.tavern.leave", ModifierIdPrefix = "dungeon-run.modifier:",
        KeepIdPrefix = "dungeon-run.keep:";
    internal static string ModifierCaption(string act) => "Act " + act + " begins. Choose a rule for this act.";
    // Phase 8: an anomaly node. A rule's name and text are its card's own (the client's "Anomaly - ..." card).
    internal static string AnomalyCaption(string act) => "Act " + act + " begins. Choose an anomaly for this act, or none.";
    internal const string NoAnomalyLabel = "No anomaly", NoAnomalyText = "Play this act by the usual rules.";
    // Step 9 (D7): a modifier node on a stamp-4 schedule also offers "none".
    internal const string NoModifierLabel = "No modifier", NoModifierText = "Play this act without a modifier.";
    internal static string RuleName(string ruleId)
    {
        try { return SabberStoneCore.Model.Cards.FromId(ruleId)?.Name ?? ruleId; } catch (System.Exception) { return ruleId; }
    }
    internal static string RuleText(string ruleId)
    {
        try { return DungeonRunValidator.MatchText(SabberStoneCore.Model.Cards.FromId(ruleId)?.Text ?? ""); } catch (System.Exception) { return ""; }
    }
    internal static string TavernLine(string act, int gold) => "Act " + act + Dot + "Gold: " + gold;
    internal const string TavernClosedCaption = "Bob has nothing for sale yet.";
    // Bob's tavern page (phase 5, NativeDungeonRunTavernUI): the boards, the offers ("Tell a Story - 1 gold: Give a friendly
    // minion +2/+2 for this run.") and a purchase's choices.
    internal const string TavernBuyIdPrefix = "dungeon-run.tavern.buy:", TavernChooseIdPrefix = "dungeon-run.tavern.choose:",
        TavernBackId = "dungeon-run.tavern.back";
    internal static string BobBoardLine(IEnumerable<string> names) => "Bob's board: " + Listed(names);
    internal static string FriendlyBoardLine(IEnumerable<string> names) => "Your board: " + Listed(names);
    internal static string TavernOfferLine(string name, int cost, string text) =>
        Described(name + " - " + cost + " gold", text);
    internal static string TavernBoughtLine(IEnumerable<string> names) => "Bought: " + Listed(names);
    internal static string TavernPendingCaption(string name) => name + ": choose one.";
    private static string Listed(IEnumerable<string> names)
    {
        var list = (names ?? Enumerable.Empty<string>()).ToList();
        return list.Count == 0 ? "(empty)" : string.Join(", ", list);
    }
    internal static string KeepCaption(int takes, int kept) => "Keep " + takes + " cards of one bundle" + (kept > 0 ? " (" + kept + " kept)" : "") + ".";
    internal static string RulesLine(IEnumerable<string> names)
    {
        var list = (names ?? Enumerable.Empty<string>()).Where(n => !string.IsNullOrEmpty(n)).ToList();
        return list.Count == 0 ? null : "Rules: " + string.Join(", ", list);
    }

    // A modifier's effect from its knobs, in a fixed order: "Bosses +5 Health; an extra treasure after the major".
    internal static string ModifierEffect(int bossHealth, int draftOptions, int tavernGold, int bossMana, int playerHealth, int openingHand, bool extraTreasure)
    {
        var parts = new List<string>();
        string Signed(int n) => (n > 0 ? "+" : "") + n;
        if (bossHealth != 0) parts.Add("Bosses " + Signed(bossHealth) + " Health");
        if (bossMana != 0) parts.Add("bosses start with " + Signed(bossMana) + " mana");
        if (playerHealth != 0) parts.Add("you have " + Signed(playerHealth) + " Health");
        if (openingHand != 0) parts.Add(Signed(openingHand) + " card in your opening hand");
        if (draftOptions != 0) parts.Add(Signed(draftOptions) + " bundle on offer");
        if (tavernGold != 0) parts.Add(Signed(tavernGold) + " tavern gold");
        if (extraTreasure) parts.Add("an extra treasure after the major");
        if (parts.Count == 0) return "";
        string text = string.Join("; ", parts);
        return text.Substring(0, 1).ToUpperInvariant() + text.Substring(1);
    }

    internal static string NextLine(string boss, int health) => "Next: " + boss + Dot + health + " Health";

    internal static string HeroLine(string hero, string power) => string.IsNullOrEmpty(power) ? hero : hero + Dot + power;

    // The hub's run line: "The Spellwright . Rimeward . Fireblast"; a version-1 run has no starter deck name.
    internal static string ChoiceLine(string hero, string deck, string power) =>
        string.Join(Dot, new[] { hero, deck, power }.Where(part => !string.IsNullOrEmpty(part)));

    internal static string StarterDeckLine(string deck) => "Deck: " + deck;

    internal static string PowerLine(string name, string text) => "Power: " + Described(name, text);

    // "2x Ray of Frost, Breath of Sindragosa, ..." in deck order, repeats folded into a count.
    internal static string DeckCardsLine(IEnumerable<string> names)
    {
        var groups = new List<KeyValuePair<string, int>>();
        foreach (string name in names ?? Enumerable.Empty<string>())
        {
            int at = groups.FindIndex(g => g.Key == name);
            if (at < 0) groups.Add(new KeyValuePair<string, int>(name, 1));
            else groups[at] = new KeyValuePair<string, int>(name, groups[at].Value + 1);
        }
        return string.Join(", ", groups.Select(g => CountLine(g.Key, g.Value)));
    }

    internal static string UnlockedDeck(string deck, string cls) => deck + " (" + cls + " deck)";
    internal static string UnlockedPower(string power, string cls) => power + " (" + cls + " power)";
    internal static string UnlockedBoss(string boss) => boss + " (challenge boss)";
    internal static string UnlockedLine(IEnumerable<string> names)
    {
        var list = (names ?? Enumerable.Empty<string>()).Where(n => !string.IsNullOrEmpty(n)).ToList();
        return list.Count == 0 ? null : "Unlocked: " + string.Join(", ", list);
    }

    internal static string DeckLine(int cards) => "Deck: " + cards + (cards == 1 ? " card" : " cards");

    internal static string TreasureCountLine(int treasures) => "Treasures: " + treasures;

    // Phase 7: the run's board shrine and its upgrades (in pick order), kept apart from the treasures (they are not counted
    // as treasures). "Shrine: Gonk's Mark . Upgrades: Pact with the Loa, Windfall"; null on a run with no board shrine.
    internal const string ShrineUpgradesHeading = "Shrine upgrades", ShrineHeading = "Shrine";
    internal static string ShrineLine(string shrine, IEnumerable<string> upgrades)
    {
        if (string.IsNullOrEmpty(shrine)) return null;
        var list = (upgrades ?? Enumerable.Empty<string>()).Where(u => !string.IsNullOrEmpty(u)).ToList();
        return "Shrine: " + shrine + (list.Count == 0 ? "" : Dot + "Upgrades: " + string.Join(", ", list));
    }

    internal static string CountLine(string name, int count) => count > 1 ? count + "x " + name : name;

    internal static string PageLine(int page, int pages) => pages > 1 ? "Page " + (page + 1) + " of " + pages : null;

    internal static string ConfirmLine(string hero) => "Start a run as " + hero + "?";

    internal static string BundleLine(string theme, IEnumerable<string> cards) => theme + ": " + string.Join(", ", cards);

    // Pool themes are lower-case ids ("battlecry"): shown capitalised.
    internal static string ThemeName(string theme) =>
        string.IsNullOrEmpty(theme) ? "Cards" : theme.Substring(0, 1).ToUpperInvariant() + theme.Substring(1);

    internal static string ResultLine(bool won, int floor) => won ? "You won the run!" : "Defeated on floor " + floor + ".";
    // An act run's result, in the hub's act wording: "Defeated in Act II . Floor 3 of 5 (8 of 15)." for a lost fight, "Run
    // abandoned in Act II . Floor 4 of 5 (9 of 15)." when it ended outside a lost fight (the floor the run stood on).
    internal static string ActResultLine(bool won, bool abandoned, string act, int floorInAct, int actFloors, int floor, int floors) =>
        won ? "You won the run!" : (abandoned ? "Run abandoned in " : "Defeated in ") + "Act " + act + Dot + "Floor " + floorInAct + " of " + actFloors
            + " (" + floor + " of " + floors + ").";
    // An act run's skipped card draft (DungeonRun.SkippedDraftFloor), shown on the next page.
    internal static string SkippedDraftLine(int floor) =>
        "No card draft after floor " + floor + ": nothing is left on offer for your class, or your deck is full.";
    internal static string LastBossLine(string boss) => "Last boss: " + boss;
    internal static string SeedLine(string seed) => "Seed: " + seed;
    internal static string TreasureNamesLine(IEnumerable<string> names)
    {
        var list = (names ?? Enumerable.Empty<string>()).ToList();
        return list.Count == 0 ? "Treasures: none" : "Treasures: " + string.Join(", ", list);
    }

    // Engine card text carries markup the menu cannot show: $/# spell-damage marks, [x] layout tags and line breaks.
    internal static string CleanCardText(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var builder = new StringBuilder(text.Replace("[x]", "").Replace("\r", "").Replace("\n", " "));
        for (int i = builder.Length - 1; i >= 0; i--)
            if ((builder[i] == '$' || builder[i] == '#') && i + 1 < builder.Length && char.IsDigit(builder[i + 1])) builder.Remove(i, 1);
        string clean = builder.ToString();
        while (clean.Contains("  ")) clean = clean.Replace("  ", " ");
        return clean.Trim();
    }

    // A hero power's text for a caption: the "Hero Power" line and bold marks dropped ("Deal 1 damage.").
    internal static string PowerText(string text)
    {
        string clean = (text ?? "").Replace("<b>Hero Power</b>\n", "").Replace("<b>Hero Power</b>", "").Replace("<b>", "").Replace("</b>", "")
            .Replace("<i>", "").Replace("</i>", "");
        return CleanCardText(clean);
    }

    internal static int Pages(int count, int perPage) => count <= 0 ? 1 : (count + perPage - 1) / perPage;
    internal static int ClampPage(int page, int count, int perPage) => page < 0 ? 0 : page >= Pages(count, perPage) ? Pages(count, perPage) - 1 : page;

    // ---- Build Deck's plank builder (NativeDungeonRunUI Step.Build): the row's candidate cards (DungeonRunRuntime.BuildCandidates)
    // as planks, 6 a page, under a filter plank that cycles the filters; the picked list in the caption.
    internal const string BuildHeader = "Build your deck", BuildRemoveLabel = "Remove last", BuildDoneLabel = "Done";
    internal const string BuildFilterId = "dungeon-run.build.filter", BuildRemoveId = "dungeon-run.build.remove", BuildDoneId = "dungeon-run.build.done",
        BuildCardIdPrefix = "dungeon-run.build.card:";
    /// <summary>The builder's card filters, in the order the filter plank cycles them: every card, the row's class cards (an engine
    /// card whose class is not neutral), neutral cards, then mana-cost buckets (0 and 1 together, 2 to 6 alone, 7 and more).</summary>
    internal enum BuildFilter { All, Class, Neutral, Cost0to1, Cost2, Cost3, Cost4, Cost5, Cost6, Cost7Plus }
    /// <summary>The filter after <paramref name="filter"/> (the last wraps to <see cref="BuildFilter.All"/>).</summary>
    internal static BuildFilter NextBuildFilter(BuildFilter filter) => filter >= BuildFilter.Cost7Plus ? BuildFilter.All : filter + 1;
    /// <summary>The filter plank's label: "Filter: All cards", "Filter: Cost 0-1", "Filter: Cost 7+".</summary>
    internal static string BuildFilterLabel(BuildFilter filter)
    {
        switch (filter)
        {
            case BuildFilter.All: return "Filter: All cards";
            case BuildFilter.Class: return "Filter: Class cards";
            case BuildFilter.Neutral: return "Filter: Neutral cards";
            case BuildFilter.Cost0to1: return "Filter: Cost 0-1";
            case BuildFilter.Cost7Plus: return "Filter: Cost 7+";
            default: return "Filter: Cost " + (filter - BuildFilter.Cost2 + 2);
        }
    }

    private static bool BuildNeutral(RunBuildCard card)
    {
        try { return SabberStoneCore.Model.Cards.FromId(card.Id)?.Class == SabberStoneCore.Enums.CardClass.NEUTRAL; }
        catch (System.Exception) { return false; }
    }

    private static bool BuildMatches(RunBuildCard card, BuildFilter filter)
    {
        switch (filter)
        {
            case BuildFilter.All: return true;
            case BuildFilter.Class: return !BuildNeutral(card);
            case BuildFilter.Neutral: return BuildNeutral(card);
            case BuildFilter.Cost0to1: return card.Cost <= 1;
            case BuildFilter.Cost7Plus: return card.Cost >= 7;
            default: return card.Cost == filter - BuildFilter.Cost2 + 2;
        }
    }

    /// <summary>The cards of <paramref name="all"/> that <paramref name="filter"/> keeps, by cost, then name, then id.</summary>
    internal static IReadOnlyList<RunBuildCard> BuildFiltered(IReadOnlyList<RunBuildCard> all, BuildFilter filter) =>
        (all ?? new RunBuildCard[0]).Where(c => c != null && BuildMatches(c, filter))
            .OrderBy(c => c.Cost).ThenBy(c => c.Name, System.StringComparer.Ordinal).ThenBy(c => c.Id, System.StringComparer.Ordinal).ToList();

    /// <summary>Page <paramref name="page"/> (0-based; clamped to the pages there are) of <see cref="BuildFiltered"/>, <paramref name="perPage"/> cards.</summary>
    internal static IReadOnlyList<RunBuildCard> BuildPage(IReadOnlyList<RunBuildCard> all, BuildFilter filter, int page, int perPage)
    {
        var filtered = BuildFiltered(all, filter);
        page = ClampPage(page, filtered.Count, perPage);
        return filtered.Skip(page * perPage).Take(perPage).ToList();
    }

    /// <summary>The copies of one card a built list may hold: what is owned, at most 2, at most 1 for a legendary.</summary>
    internal static int BuildCopyLimit(RunBuildCard card) => System.Math.Min(card.Owned, card.Legendary ? 1 : 2);

    /// <summary>
    /// A card plank: "Fireball (4) 1/2" is the name, the mana cost, then n/m where n (<paramref name="inDeck"/>) is the copies of
    /// this card in the list and m the copies this list may hold of it (<see cref="BuildCopyLimit"/>: min(owned, 2, 1 for a
    /// legendary)). The copy group's shared limit is not folded in: an add past it is refused with its reason.
    /// </summary>
    internal static string BuildCardLabel(RunBuildCard c, int inDeck) => c.Name + " (" + c.Cost + ") " + inDeck + "/" + BuildCopyLimit(c);

    /// <summary>The caption's list line: "Picked 3 of 10: 2x Fireball, Frostbolt" (repeats folded, first pick first); "Picked 0 of 10".</summary>
    internal static string BuildCaption(IReadOnlyList<string> names, int size)
    {
        int count = names?.Count ?? 0;
        return "Picked " + count + " of " + size + (count == 0 ? "" : ": " + DeckCardsLine(names));
    }

    /// <summary>
    /// The legal copies <paramref name="all"/> can put in one deck: per copy group, min(the owned counts of its cards summed, 2,
    /// or 1 when a card of the group is legendary), summed over the groups. <paramref name="copyGroup"/> maps a card id to its
    /// group; null uses each card's own <see cref="RunBuildCard.CopyGroup"/> (its id when it has none). Rune limits are not counted.
    /// </summary>
    internal static int BuildReachable(IReadOnlyList<RunBuildCard> all, System.Func<string, string> copyGroup)
    {
        System.Func<RunBuildCard, string> group = c => (copyGroup != null ? copyGroup(c.Id) : c.CopyGroup) ?? c.Id;
        return (all ?? new RunBuildCard[0]).Where(c => c != null && c.Owned > 0).GroupBy(group, System.StringComparer.Ordinal)
            .Sum(g => System.Math.Min(g.Sum(c => c.Owned), g.Any(c => c.Legendary) ? 1 : 2));
    }

    /// <summary>The caption line when the row's owned legal copies (<see cref="BuildReachable"/>) cannot fill the deck: "Not enough
    /// cards in your collection for this hero (7 of 10)"; null when they can.</summary>
    internal static string BuildNotEnoughLine(int reachable, int size) =>
        reachable >= size ? null : "Not enough cards in your collection for this hero (" + reachable + " of " + size + ")";

    /// <summary>
    /// The caption when a card plank's add is refused, from <see cref="DungeonRunBuild.AddReason"/>'s code: "Cannot add Fireball:
    /// the deck is full." for "size"; the copies, legendary, owned, runes and not-collectible codes in words; any other code as given.
    /// </summary>
    internal static string BuildAddRefusal(string name, string reason)
    {
        string why = BuildReasonWords((reason ?? "").Split(' ')[0]);
        return "Cannot add " + name + ": " + (why == null ? reason : why + ".");
    }

    // The words of a DungeonRunBuild reason code shared by the add and run-start refusals ("at most 2 copies of a card"); null
    // for a code they word differently or not at all.
    private static string BuildReasonWords(string code)
    {
        switch (code)
        {
            case "size": return "the deck is full";
            case "copies": return "at most 2 copies of a card";
            case "legendary": return "at most 1 copy of a legendary";
            case "owned": return "you own no more copies";
            case "runes": return "a Death Knight deck needs at most 3 runes";
            case "not-collectible": return "not a collectible card";
            default: return null;
        }
    }

    /// <summary>
    /// The run-start refusal of a built list, from <see cref="DungeonRunBuild.DeckReasons"/>: <see cref="DungeonRun.IllegalBuiltList"/>,
    /// then each reason in words with its card's name, joined by "; ": "That deck cannot start a run: Fireball: you no longer own
    /// that many copies." The add refusal's words are reused (<see cref="BuildReasonWords"/>). <paramref name="name"/> names an
    /// engine id or a copy group; a name it cannot give (null, empty, or it throws) is "a card", so no engine id is shown. Null
    /// when there are no reasons.
    /// </summary>
    internal static string BuildStartRefusal(IReadOnlyList<string> reasons, System.Func<string, string> name)
    {
        if (reasons == null || reasons.Count == 0) return null;
        string Named(string id)
        {
            string text = null;
            try { text = id == null || name == null ? null : name(id); } catch (System.Exception) { text = null; }
            return string.IsNullOrEmpty(text) ? "a card" : text;
        }
        var parts = new List<string>();
        foreach (string reason in reasons)
        {
            string text = reason ?? "";
            int space = text.IndexOf(' ');
            string code = space < 0 ? text : text.Substring(0, space), rest = space < 0 ? null : text.Substring(space + 1);
            if (code == "size") { parts.Add("it has " + rest + " cards"); continue; }
            string why;
            switch (code)
            {
                case "owned": why = "you no longer own that many copies"; break;
                case "class": why = "not a card of this hero's class"; break;
                case "hero": why = "a hero card"; break;
                case "fabled": why = "a Fabled card"; break;
                case "rune-class": why = "a rune card, only in a Death Knight deck"; break;
                case "missing": why = "an unknown card"; break;
                default: why = BuildReasonWords(code) ?? "not allowed in a run"; break;
            }
            parts.Add(rest == null ? why : Named(rest) + ": " + why);
        }
        return DungeonRun.IllegalBuiltList + string.Join("; ", parts) + ".";
    }

    // ---- the portrait tray's pages (NativeDungeonRunPicker, owner 2026-11-22). The tray's prefab lays out at most `perPage`
    // heroes (its layout capacity, 12 on Dalaran's tray), so the run rows are paged. Page order: the class rows first, then the
    // named heroes, each kept in row order, so page 1 is the 12 the tray showed before paging (every class row and the first
    // named hero). A page lists its rows in row order. When the prefab cannot lay out the last page's size (laysOut), the last
    // page is the smallest size it can lay out, ending on the last row (it repeats rows of the page before), so no row is lost.
    internal const string TrayPreviousLabel = "Previous", TrayNextLabel = "Next";
    internal static string TrayPageLog(int page, int pages) => "tray page " + (page + 1) + " of " + pages;
    /// <summary>The indices (into the tray's rows, in row order) of the rows page <paramref name="page"/> shows; every row when
    /// they fit one page or no capacity is known (<paramref name="perPage"/> &lt;= 0).</summary>
    internal static List<int> TrayPageRows(IList<bool> isHero, int perPage, int page, System.Func<int, bool> laysOut = null)
    {
        int count = isHero == null ? 0 : isHero.Count;
        if (perPage <= 0 || perPage >= count) return Enumerable.Range(0, count).ToList();
        var order = Enumerable.Range(0, count).OrderBy(i => isHero[i] ? 1 : 0).ToList(); // OrderBy is stable
        page = ClampPage(page, count, perPage);
        int start = page * perPage, size = System.Math.Min(perPage, count - start);
        if (laysOut != null && !laysOut(size))
        {
            int fit = size + 1;
            while (fit < perPage && !laysOut(fit)) fit++;
            size = fit; start = count - fit;
        }
        return order.Skip(start).Take(size).OrderBy(i => i).ToList();
    }

    // Where the tray's Previous and Next plates go (screen pixels, bottom-left origin, y up): Previous at the hero grid's left end,
    // Next at its right end, each Back's size, on the first clear row of: just under the grid (on the tray's empty bottom plate),
    // then level with Back. marginX/marginY are how far a plate's art reaches past its hit body (NativeButtonKit.FrameMarginOf);
    // the art is kept off the grid, Back, every rect in avoid (Choose, the buttons shown) and the screen edge. A row where both
    // ends are clear is preferred on every page, so the plates keep their row; failing that, only the plates
    // that will be shown must be clear (needPrevious: not on page 1; needNext: not on the last page), and the grid must hold two
    // plates side by side only when both are. grid is the full-capacity grid (TrayPagerGrid), so the plates sit at the same place
    // on every page. Null when no row is clear, or when neither plate is needed.
    internal static (float PreviousX, float NextX, float Y, string Where)? TrayPagerPlace((float X, float Y, float W, float H) grid,
        (float X, float Y, float W, float H) back, float marginX, float marginY, IEnumerable<(float X, float Y, float W, float H)> avoid, float screenHeight,
        bool needPrevious = true, bool needNext = true)
    {
        float w = back.W, h = back.H;
        if (w <= 0 || h <= 0 || grid.W <= 0 || grid.H <= 0 || !needPrevious && !needNext) return null;
        var obstacles = (avoid ?? Enumerable.Empty<(float X, float Y, float W, float H)>()).ToList();
        obstacles.Add(grid); obstacles.Add(back);
        float left = grid.X + marginX, right = grid.X + grid.W - w - marginX;
        bool twoFit = right - marginX >= left + w + marginX;
        // the grid is narrower than two plates side by side
        if (needPrevious && needNext && !twoFit) return null;
        float gap = System.Math.Max(h * .25f, 2 * marginY);
        var rows = new[] { ("under the grid", grid.Y - gap - h), ("level with Back", back.Y) };
        // First the row where both ends are clear (the same row on every page that can hold both), then, for a page that shows
        // one plate only, the first row where that plate's end is clear.
        for (int pass = 0; pass < 2; pass++)
        {
            bool both = pass == 0;
            if (both && !twoFit) continue;
            foreach (var (where, bottom) in rows)
            {
                if (bottom - marginY < 0 || bottom + h + marginY > screenHeight) continue;
                bool Clear(float x) => !obstacles.Any(o => x - marginX < o.X + o.W && o.X < x + w + marginX && bottom - marginY < o.Y + o.H && o.Y < bottom + h + marginY);
                if (both ? Clear(left) && Clear(right) : (!needPrevious || Clear(left)) && (!needNext || Clear(right))) return (left, right, bottom, where);
            }
            if (needPrevious && needNext) break; // both plates shown: only the first pass applies
        }
        return null;
    }

    /// <summary>The tray's full-capacity hero grid as an offset from Back's rect (Back moves with the tray, so the offset holds
    /// while the tray slides in), with the screen size it was measured at. Kept across a page change (NativeDungeonRunPicker).</summary>
    internal sealed class TrayGridMemo
    {
        internal (float X, float Y, float W, float H)? FromBack;
        internal float ScreenW, ScreenH;
    }

    // The grid TrayPagerPlace places against. A page showing the tray's full capacity (liveButtons >= perPage, perPage > 0) is the
    // full grid itself, and memo remembers it from Back's rect; a shorter page (the last one: 1 of 37 rows at 12 a page) uses the
    // remembered full grid at the same offset from Back, so the plates stay where they were; with nothing remembered at this
    // screen size, the buttons shown. source names which ("full", "remembered", "shown"). Null with no grid at all.
    internal static (float X, float Y, float W, float H)? TrayPagerGrid(TrayGridMemo memo, (float X, float Y, float W, float H)? live, int liveButtons,
        int perPage, (float X, float Y, float W, float H) back, float screenW, float screenH, out string source)
    {
        if (live != null && perPage > 0 && liveButtons >= perPage)
        {
            if (memo != null)
            {
                var g = live.Value;
                memo.FromBack = (g.X - back.X, g.Y - back.Y, g.W, g.H); memo.ScreenW = screenW; memo.ScreenH = screenH;
            }
            source = "full"; return live;
        }
        if (memo?.FromBack != null && memo.ScreenW == screenW && memo.ScreenH == screenH)
        {
            var o = memo.FromBack.Value;
            source = "remembered"; return (back.X + o.X, back.Y + o.Y, o.W, o.H);
        }
        source = "shown"; return live;
    }

    /// <summary>True when two screen rects share area (edges that only touch do not count).</summary>
    internal static bool TrayRectsOverlap((float X, float Y, float W, float H) a, (float X, float Y, float W, float H) b) =>
        a.X < b.X + b.W && b.X < a.X + a.W && a.Y < b.Y + b.H && b.Y < a.Y + a.H;
}
