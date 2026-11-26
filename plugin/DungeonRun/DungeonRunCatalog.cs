using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.CardSets.DungeonRun;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;

// What a run can start with, as plain rows for a picker (the plank menu now, the client's Adventure screens later):
// classes, each class's starter decks and hero powers, with unlock state and hint. Pure (no Unity), tested headlessly.

/// <summary>A selectable class.</summary>
internal sealed class RunClassOption
{
    /// <summary>The engine CardClass name ("MAGE").</summary>
    public string Id;
    /// <summary>The run hero's title ("The Spellwright").</summary>
    public string Name;
    /// <summary>"Mage".</summary>
    public string ClassName;
    /// <summary>Engine id of the portrait ("HERO_08").</summary>
    public string HeroCardId;
    /// <summary>The id the client knows the portrait by (NativeMatch.TransportCardId of <see cref="HeroCardId"/>): draw this one.</summary>
    public string ClientHeroCardId;
    public string DefaultDeckId, DefaultPowerId;
    public int SortOrder, DecksUnlocked, PowersUnlocked, DeckCount, PowerCount;
}

/// <summary>
/// A row a run can start on under one class (phase 6, the picker's Hero step): the class row itself, or a named adventure hero
/// whose primary class it is. <see cref="Id"/> is what DecksFor, PowersFor and NewRun take.
/// </summary>
internal sealed class RunHeroOption
{
    /// <summary>The row id: the class ("MAGE"), a hero portrait id ("DALA_Rakanishu") or a dual row ("MAGE_ROGUE").</summary>
    public string Id;
    /// <summary>The class step this row sits under (its primary class).</summary>
    public string ClassId;
    public string Name;
    /// <summary>"Mage", "Mage / Rogue".</summary>
    public string ClassName;
    /// <summary>The adventure group ("Heist", "Tombs", ...); null for the class row.</summary>
    public string Adventure;
    public string HeroCardId, ClientHeroCardId;
    public bool IsHero, IsDual;
    /// <summary>The run hero's base Health (30 for every named hero; the portrait's own for the class row).</summary>
    public int Health;
    public int DeckCount, PowerCount, SignatureCount, SortOrder;
}

/// <summary>A signature treasure a hero row's run may start with (phase 6, Tombs of Terror loadouts).</summary>
internal sealed class RunSignatureOption
{
    /// <summary>The signature id the row lists (what NewRun takes).</summary>
    public string Id;
    /// <summary>What the run starts with: the Sr. upgrade when its unlock holds, else <see cref="Id"/>.</summary>
    public string StartsAs;
    /// <summary>The card the run's deck gets (the treasure's content shell), or null for a passive signature.</summary>
    public string CardId;
    public string Name, Text;
    public int SortOrder;
}

/// <summary>A starter deck of one class.</summary>
internal sealed class RunDeckOption
{
    public string Id, ClassId, Name, Description;
    /// <summary>The starter's engine card ids in data order (a duplicate appears twice).</summary>
    public IReadOnlyList<string> CardIds;
    public bool IsDefault, Unlocked;
    /// <summary>
    /// The row's Build Deck option (its build-able deck, DungeonRunClass.BuildDeck: a build deck, or a Heist/Tombs generated
    /// deck): named <see cref="NativeDungeonRunText.BuildDeckName"/>, always unlocked, no cards; listed last.
    /// </summary>
    public bool IsBuild;
    /// <summary>
    /// The deck's display card (DungeonRunStarterDeck.DisplayCard: DALA_800 on a build deck, DALA_80x or ULDA_90x on a Heist or
    /// Tombs generated deck), a client card id; null on a deck without one. A native picker shows it as the Build Deck pouch's list.
    /// </summary>
    public string DisplayCard;
    /// <summary>The unlock key (null for the default deck) and, while locked, the hint ("Reach floor 3 with Mage to unlock").</summary>
    public string UnlockKey, UnlockHint;
    public int SortOrder;
}

/// <summary>
/// A card the player may put in a Build Deck starter (DungeonRunRuntime.BuildCandidates): a collectible card the row admits
/// (<see cref="DungeonRunBuild.CardReason"/> null) that the player owns.
/// </summary>
internal sealed class RunBuildCard
{
    /// <summary>The engine card id (what the built list holds), the card id the client is sent for it, and its name.</summary>
    public string Id, ClientId, Name;
    public int Cost;
    /// <summary>A legendary: at most one copy of its copy group.</summary>
    public bool Legendary;
    /// <summary>Copies owned, golden and normal together (always more than 0).</summary>
    public int Owned;
    /// <summary>The copy-limit group (a Core reprint and its original share one); the client id when it has none.</summary>
    public string CopyGroup;
    /// <summary>
    /// A plank that stands for several printings of one copy group (<see cref="NativeDungeonRunText.BuildMergePrintings"/>): each
    /// printing as its own card (its own id and owned count), the plank's id first; null for a card with one printing.
    /// </summary>
    public IReadOnlyList<RunBuildCard> Printings;
}

/// <summary>A hero power of one class.</summary>
internal sealed class RunPowerOption
{
    /// <summary>Content key ("mage_rimebite") or basic power card id ("CS2_034"): what NewRun takes.</summary>
    public string Id;
    /// <summary>The engine card the match uses (DungeonRunSetup.PowerCardId of <see cref="Id"/>).</summary>
    public string ShellCardId;
    /// <summary>
    /// The id the client knows the power by (NativeMatch.TransportCardId of <see cref="ShellCardId"/>, e.g. CS2_034 -> HERO_08bp):
    /// the card a native picker shows. Outside a run match the client draws that card's own name, text and cost (a shell's
    /// Tavern Brawl ones), so a picker overlays <see cref="Name"/>, <see cref="Text"/> and <see cref="Cost"/>.
    /// </summary>
    public string ClientCardId;
    public string Name;
    /// <summary>Card text with its markup (for native card text); <see cref="PlainText"/> is cleaned for planks.</summary>
    public string Text, PlainText;
    /// <summary>The run cost. A shell's own client cost can differ: a native picker must draw this one.</summary>
    public int Cost;
    /// <summary>The power needs a target (the shell has REQ_TARGET_TO_PLAY).</summary>
    public bool Targeted;
    public bool IsDefault, Unlocked;
    public string UnlockKey, UnlockHint;
    public int SortOrder;
}

internal static class DungeonRunCatalog
{
    // Engine id -> client id. The runtime passes NativeMatch.TransportCardId; headless callers may pass null (identity).
    private static string Client(Func<string, string> clientId, string engineId) =>
        engineId == null ? null : clientId == null ? engineId : clientId(engineId) ?? engineId;

    internal static IReadOnlyList<RunClassOption> Classes(DungeonRun rules, DungeonRunProfile profile, Func<string, string> clientId = null)
    {
        if (rules == null || profile == null) return new RunClassOption[0];
        return rules.AvailableClasses(profile).Select((cls, i) => new RunClassOption
        {
            Id = cls.Id, Name = cls.Name, ClassName = NativeDungeonRunText.ClassName(cls.Id), HeroCardId = cls.HeroCard,
            ClientHeroCardId = Client(clientId, cls.HeroCard),
            DefaultDeckId = cls.DefaultDeck, DefaultPowerId = cls.DefaultPower, SortOrder = i,
            DecksUnlocked = rules.AvailableDecks(cls, profile).Count(), PowersUnlocked = rules.AvailablePowers(cls, profile).Count(),
            DeckCount = cls.Decks.Count, PowerCount = cls.Powers.Count,
        }).ToList();
    }

    /// <summary>
    /// The rows a run can start on under class <paramref name="classId"/> (phase 6): the class row first, then its playable named
    /// hero rows (DungeonRun.AvailableHeroes); empty for an unknown class.
    /// </summary>
    internal static IReadOnlyList<RunHeroOption> Heroes(DungeonRun rules, DungeonRunProfile profile, string classId, Func<string, string> clientId = null)
    {
        if (rules == null || profile == null) return new RunHeroOption[0];
        return rules.AvailableHeroes(classId, profile).Select((cls, i) =>
        {
            int health = cls.Health;
            if (health <= 0) { try { health = Cards.FromId(cls.HeroCard)?.Health ?? 0; } catch (Exception) { health = 0; } }
            return new RunHeroOption
            {
                Id = cls.Id, ClassId = classId, Name = cls.Name, ClassName = RowClassName(cls), Adventure = cls.Adventure,
                HeroCardId = cls.HeroCard, ClientHeroCardId = Client(clientId, cls.HeroCard), IsHero = cls.IsHero, IsDual = cls.IsDual,
                Health = health, DeckCount = cls.Decks.Count, PowerCount = cls.Powers.Count, SignatureCount = cls.Signatures.Count, SortOrder = i,
            };
        }).ToList();
    }

    /// <summary>
    /// The signature treasures a run of <paramref name="rowId"/> on <paramref name="seed"/> is offered (DungeonRun.SignatureOffer),
    /// with what each starts as for this profile; empty for a row with no signatures.
    /// </summary>
    internal static IReadOnlyList<RunSignatureOption> Signatures(DungeonRun rules, DungeonRunProfile profile, string rowId, long seed)
    {
        var cls = rules?.Data.FindClass(rowId);
        if (cls == null || profile == null) return new RunSignatureOption[0];
        return rules.SignatureOffer(profile, cls, seed).Select((id, i) =>
        {
            string startsAs = rules.SignatureCard(profile, id);
            var option = new RunSignatureOption { Id = id, StartsAs = startsAs, SortOrder = i, Name = startsAs, Text = "" };
            if (DungeonRunContent.TryGet(startsAs, out RunContent content))
            {
                option.Name = content.Name ?? startsAs; option.Text = content.Text ?? "";
                option.CardId = content.Kind == RunContentKind.ActiveTreasure ? content.ShellId : null;
            }
            return option;
        }).ToList();
    }

    /// <summary>A row's classes as a picker names them ("Mage", "Mage / Rogue"), for a class row and a named hero row alike.</summary>
    internal static string RowClassName(DungeonRunClass cls) =>
        cls == null ? "" : string.Join(" / ", cls.ClassSet.Select(NativeDungeonRunText.ClassName));

    /// <summary>
    /// The class's decks, default first and the Build Deck option (the row's build-able deck) last; empty for an unknown class.
    /// </summary>
    internal static IReadOnlyList<RunDeckOption> Decks(DungeonRun rules, DungeonRunProfile profile, string classId)
    {
        var cls = rules?.Data.FindClass(classId);
        if (cls == null || profile == null) return new RunDeckOption[0];
        var result = new List<RunDeckOption>();
        foreach (string id in cls.Decks)
        {
            var deck = rules.Data.FindDeck(id);
            if (deck == null) continue;
            if (deck.IsBuildable)
            {
                // Build Deck: always unlocked (a Heist/Tombs generated deck's own unlock never gates it), no listed cards.
                result.Add(new RunDeckOption
                {
                    Id = deck.Id, ClassId = cls.Id, Name = NativeDungeonRunText.BuildDeckName, Description = NativeDungeonRunText.BuildDeckDescription,
                    CardIds = new string[0], IsDefault = false, Unlocked = true, IsBuild = true, DisplayCard = deck.DisplayCard,
                    SortOrder = result.Count,
                });
                continue;
            }
            bool unlocked = rules.IsUnlocked(profile, deck.Unlock);
            result.Add(new RunDeckOption
            {
                Id = deck.Id, ClassId = cls.Id, Name = deck.Name, Description = deck.Description, CardIds = deck.Cards.ToList(),
                IsDefault = id == cls.DefaultDeck, Unlocked = unlocked, UnlockKey = deck.Unlock,
                UnlockHint = unlocked ? null : Hint(rules, deck.Unlock), DisplayCard = deck.DisplayCard, SortOrder = result.Count,
            });
        }
        return result;
    }

    /// <summary>The class's powers, the basic power first; empty for an unknown class.</summary>
    internal static IReadOnlyList<RunPowerOption> Powers(DungeonRun rules, DungeonRunProfile profile, string classId,
        Func<string, string> clientId = null)
    {
        var cls = rules?.Data.FindClass(classId);
        if (cls == null || profile == null) return new RunPowerOption[0];
        var result = new List<RunPowerOption>();
        foreach (string id in cls.Powers)
        {
            string key = cls.PowerUnlock(id);
            bool unlocked = rules.IsUnlocked(profile, key);
            var option = new RunPowerOption
            {
                Id = id, ShellCardId = DungeonRunSetup.PowerCardId(id), ClientCardId = Client(clientId, DungeonRunSetup.PowerCardId(id)), IsDefault = id == cls.DefaultPower, Unlocked = unlocked,
                UnlockKey = key, UnlockHint = unlocked ? null : Hint(rules, key), SortOrder = result.Count,
            };
            Describe(id, out option.Name, out option.Text, out option.Cost);
            option.PlainText = NativeDungeonRunText.PowerText(option.Text);
            try { option.Targeted = Cards.FromId(option.ShellCardId)?.PlayRequirements?.ContainsKey(PlayReq.REQ_TARGET_TO_PLAY) == true; }
            catch (Exception) { option.Targeted = false; }
            result.Add(option);
        }
        return result;
    }

    /// <summary>Name, text and run cost of a power: the content entry's, or the basic power card's.</summary>
    internal static void Describe(string keyOrId, out string name, out string text, out int cost)
    {
        name = keyOrId; text = ""; cost = 0;
        if (string.IsNullOrEmpty(keyOrId)) return;
        try
        {
            if (DungeonRunContent.TryGet(keyOrId, out RunContent content)) { name = content.Name; text = content.Text ?? ""; cost = content.Cost; return; }
            var card = Cards.FromId(keyOrId);
            if (card == null) return;
            name = card.Name ?? keyOrId; text = card.Text ?? ""; cost = card.Cost;
        }
        catch (Exception) { }
    }

    /// <summary>The unlock hint for a key ("Reach floor 3 with Mage to unlock", "Win runs with 4 different classes to unlock").</summary>
    internal static string Hint(DungeonRun rules, string unlockKey)
    {
        if (rules == null || unlockKey == null || !rules.Data.Unlocks.TryGetValue(unlockKey, out DungeonRunCondition c))
            return NativeDungeonRunText.LockedWord;
        // An "any of" condition with nothing of its own ("win": a run won, or an act major beaten) is hinted by its first branch,
        // which is enough on its own ("Win a run with Mage to unlock").
        if (c.AnyOf != null && c.AnyOf.Count > 0 && c.AllOf == null && c.FloorReached == null && c.RunsWon == null && c.ClassesWon == null
            && c.MajorsCleared == null)
            c = c.AnyOf[0];
        return NativeDungeonRunText.UnlockHint(c.FloorReached, c.RunsWon, c.ClassesWon, c.Class == null ? null : RowName(rules, c.Class),
            c.AllOf != null || c.AnyOf != null || c.MajorsCleared != null);
    }

    /// <summary>
    /// How an unlock hint or summary names a row: a class row or a dual row by its classes ("Mage", "Mage / Rogue"), a
    /// single-class named hero row by its hero's name ("Rakanishu", "Tess Greymane"), never by its portrait id.
    /// </summary>
    internal static string RowName(DungeonRun rules, string rowId)
    {
        var cls = rules?.Data.FindClass(rowId);
        return cls != null && cls.IsHero && !cls.IsDual && !string.IsNullOrEmpty(cls.Name) ? cls.Name : NativeDungeonRunText.ClassName(rowId);
    }

    /// <summary>
    /// How the run hub names a run's starter deck: <see cref="NativeDungeonRunText.BuildDeckName"/> for a built run (its StarterDeck
    /// is the row's build deck, on a Heist or Tombs row the generated deck, which the pickers show as Build Deck too), else the
    /// starter deck's name; null for no run or a run with no starter deck id (a migrated version-1 run).
    /// </summary>
    internal static string RunDeckName(DungeonRun rules, DungeonRunState state) =>
        state == null ? null : state.BuiltDeck ? NativeDungeonRunText.BuildDeckName : rules?.Data.FindDeck(state.StarterDeck)?.Name;

    /// <summary>
    /// What an unlock key opens, for the run summary ("Kindled Sparks (Mage deck)"); null when it opens nothing. A build-able deck
    /// is never named: Build Deck is always open, and a Heist/Tombs generated deck keeps its unlock key in data only.
    /// </summary>
    internal static string UnlockName(DungeonRun rules, string unlockKey)
    {
        if (rules == null || unlockKey == null) return null;
        var names = new List<string>();
        foreach (var deck in rules.Data.Decks.Where(d => d.Unlock == unlockKey && !d.IsBuildable))
            names.Add(NativeDungeonRunText.UnlockedDeck(deck.Name, RowName(rules, deck.Class)));
        foreach (var cls in rules.Data.Classes)
            foreach (string power in cls.Powers.Where(p => cls.PowerUnlock(p) == unlockKey))
            {
                Describe(power, out string name, out _, out _);
                names.Add(NativeDungeonRunText.UnlockedPower(name, RowName(rules, cls.Id)));
            }
        foreach (var boss in rules.Data.Bosses.Where(b => b.Unlock == unlockKey)) names.Add(NativeDungeonRunText.UnlockedBoss(boss.Name));
        return names.Count == 0 ? null : string.Join(", ", names);
    }
}
