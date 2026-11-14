using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.CardSets.DungeonRun;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;

/// <summary>
/// Upgrades a version-1 Dungeon Run save (hero select) to version 2 (class, deck, power), a version-2 save to version 3
/// (the shared draft catalog) and a version-3 save to version 4 (the act/floor schedule), in place (design § Save versions).
/// Needs the loaded data for the version-1 hero map, so the runtime calls it once the rules exist, never the store. Every
/// step is caught and logged: a damaged old file loses at most the step that failed, and loading never throws. Idempotent:
/// each step only touches rows without a class, and a current save is left alone. Version 2 to 3 changes no value: every run
/// of an older save is a legacy run (CatalogVersion 0, pinned to the fixed bundles), and its drafts keep no stored offers.
/// Version 3 to 4 changes no value either: every run of an older save is a legacy-schedule run (ScheduleVersion 0, the fixed
/// eight floors), with no card records, taverns or modifiers, so its body serializes exactly as at version 3.
/// </summary>
internal static class DungeonRunMigration
{
    /// <summary>Upgrades <paramref name="save"/> when it is older than <see cref="DungeonRunSave.CurrentVersion"/>; true if it did.</summary>
    internal static bool Upgrade(DungeonRunSave save, DungeonRun rules, Action<string> log)
    {
        if (save == null || rules == null || save.Version >= DungeonRunSave.CurrentVersion) return false;
        if (save.Profile == null) save.Profile = new DungeonRunProfile();
        int from = save.Version;
        if (save.Version < 2 && !UpgradeV1(save, rules, log, from)) return true;
        // 2 -> 3: the shared draft catalog. Every run of an older save is a legacy run: CatalogVersion 0 (the value an absent
        // attribute reads as, stamped explicitly), its drafts without stored offers (they read the fixed bundles). A version-3
        // save's run keeps its CatalogVersion (a catalog run stays one).
        if (save.Version < 3)
        {
            try
            {
                if (save.Active != null)
                {
                    save.Active.CatalogVersion = 0;
                    foreach (var draft in save.Active.Drafts) if (draft.Offers == null) draft.Offers = new List<DungeonRunOffer>();
                    if (save.Active.BucketTakes == null) save.Active.BucketTakes = new List<DungeonRunTake>();
                }
            }
            catch (Exception failure)
            {
                log?.Invoke("Dungeon Run save upgrade: version 2 to 3 failed: " + failure.GetType().Name + ": " + failure.Message + "; the save stays at version " + save.Version);
                return true;
            }
            save.Version = 3;
        }
        // 3 -> 4: the act/floor schedule. Every run of an older save is a legacy-schedule run: ScheduleVersion 0 (the value an
        // absent attribute reads as, stamped explicitly), no schedule, node, card records, rules, tavern or modifier offer.
        // A version-4 save is already on its act schedule: this step runs only for an older one.
        if (save.Version < 4)
        {
            try
            {
                if (save.Active != null)
                {
                    save.Active.ScheduleVersion = 0;
                    save.Active.Schedule = null;
                    save.Active.Node = 0;
                    if (save.Active.Cards == null) save.Active.Cards = new List<DungeonRunCard>();
                    if (save.Active.ActiveRules == null) save.Active.ActiveRules = new List<DungeonRunRule>();
                }
            }
            catch (Exception failure)
            {
                log?.Invoke("Dungeon Run save upgrade: version 3 to 4 failed: " + failure.GetType().Name + ": " + failure.Message + "; the save stays at version " + save.Version);
                return true;
            }
            save.Version = 4;
        }
        // 4 -> 5 (phase 7: board shrines and teammates): the identity. Every run of an older save has no board shrine
        // (BoardShrine null) and no shrine upgrades, so a phase-6 Rikkar run keeps playing without a shrine, and its schedule
        // version (1) never opens a teammate or shrine-upgrade draft. Only the version (and the one-time backup) changes.
        if (save.Active != null && save.Active.ShrineUpgrades == null) save.Active.ShrineUpgrades = new List<string>();
        // 5 -> 6 (phase 8: encounters, anomalies and twists): the identity. Every run of an older save is on schedule version
        // 0, 1 or 2: it never draws an encounter (its pools hold none), never opens an anomaly node and never rolls a twist, so
        // it has no hero switch, no carried hero damage and no rule kind, and its profile no boss damage. Only the version
        // (and the one-time backup dungeon-run.v5.xml) changes.
        if (save.Profile.BossDamage == null) save.Profile.BossDamage = new List<DungeonRunBossDamage>();
        save.Version = DungeonRunSave.CurrentVersion;
        log?.Invoke("Dungeon Run save upgraded from version " + from + " to " + save.Version
            + (save.Active != null ? " (run in progress: " + save.Active.Class + ", power " + (save.Active.Power ?? save.Active.Shrine)
                + (save.Active.IsCatalogRun ? ", catalog v" + save.Active.CatalogVersion : ", legacy catalog")
                + (save.Active.IsActRun ? ", schedule v" + save.Active.ScheduleVersion : ", legacy schedule") + ")" : ""));
        return true;
    }

    // Version 1 -> 2. True when the save reached version 2; false when the run in progress could not be upgraded (the save
    // stays at its version, and every step runs again on the next load).
    private static bool UpgradeV1(DungeonRunSave save, DungeonRun rules, Action<string> log, int from)
    {
        var data = rules.Data;
        var profile = save.Profile;

        bool Step(string name, Action action)
        {
            try { action(); return true; }
            catch (Exception failure) { log?.Invoke("Dungeon Run save upgrade: " + name + " failed: " + failure.GetType().Name + ": " + failure.Message); return false; }
        }

        // 1. The run in progress keeps its deck, treasures, bosses, drafts, seed, floor and phase exactly.
        bool activeUpgraded = Step("active run", () =>
        {
            var state = save.Active;
            if (state == null || state.Class != null) return;
            var row = Resolve(data, state.Hero, state.Deck, log, fallbackToFirstClass: true);
            state.Class = row.Class;
            // A version-1 starter is none of the version-2 starter decks: the run records no starter deck id (so no deck
            // treasure tags or deck scopes apply to it, and its offers stay as they were).
            state.StarterDeck = null;
            var cls = data.Class(row.Class);
            if (state.Shrine != null && !DungeonRunContent.TryGet(state.Shrine + "_1", out _))
            {
                log?.Invoke("Dungeon Run save upgrade: unknown shrine " + state.Shrine + ", the run uses " + cls.DefaultPower);
                state.Shrine = null; state.Power = null;
            }
            if (state.Power != null && !IsHeroPower(state.Power))
            {
                log?.Invoke("Dungeon Run save upgrade: unknown hero power " + state.Power + ", the run uses " + cls.DefaultPower);
                state.Power = null;
            }
            // The power is kept even when the class no longer lists it (a Cartographer run on Ambush Point stays on it).
            if (state.Power == null && state.Shrine == null) state.Power = row.Power ?? cls.DefaultPower;
            int before = state.Deck.Count;
            state.Deck.RemoveAll(id => !IsCard(id));
            if (state.Deck.Count != before) log?.Invoke("Dungeon Run save upgrade: dropped " + (before - state.Deck.Count) + " unknown card(s) from the run deck");
        });

        // 2. Stats: one row per class. Same-class heroes (Trapper and Ranger, ...) merge: runs and wins add, best floor is the max.
        // A row of an unknown hero keeps its hero id and no class: it still counts for class-free conditions, never for a
        // class ladder (it is not credited to some other class).
        Step("stats", () =>
        {
            foreach (var row in profile.Heroes.Where(r => r.Class == null)) row.Class = Resolve(data, row.Hero, null, log, fallbackToFirstClass: false)?.Class;
            var merged = new List<DungeonRunHeroStats>(profile.Heroes.Where(r => r.Class == null));
            foreach (var group in profile.Heroes.Where(r => r.Class != null).GroupBy(r => r.Class))
            {
                var rows = group.ToList();
                if (rows.Count == 1 && rows[0].Hero == null && rows[0].Archetype == null) { merged.Add(rows[0]); continue; }
                merged.Add(new DungeonRunHeroStats
                {
                    Class = group.Key, Runs = rows.Sum(r => r.Runs), Wins = rows.Sum(r => r.Wins), BestFloor = rows.Max(r => r.BestFloor),
                });
            }
            profile.Heroes = merged;
        });

        // 3. Archive: each entry learns its class; the hero id stays.
        Step("archive", () =>
        {
            foreach (var entry in profile.Archive.Where(e => e.Class == null)) entry.Class = Resolve(data, entry.Hero, null, log, fallbackToFirstClass: false)?.Class;
        });

        // 4. Unlocks: what a version-1 key gave is kept through the class keys it maps to. Old keys stay (harmless).
        Step("unlocks", () =>
        {
            foreach (string old in profile.Unlocked.ToList())
                if (data.LegacyUnlocks.TryGetValue(old, out var keys))
                    foreach (string key in keys.Where(k => !profile.Unlocked.Contains(k))) profile.Unlocked.Add(key);
        });

        // 5. Progress already earned under the class ladder (e.g. a best Mage floor of 3) unlocks now, not after the next run.
        Step("refresh", () => rules.RefreshUnlocks(profile));

        // The version moves on only once the run in progress has a class: otherwise the upgrade runs again on the next load
        // (every step is idempotent), and the run can still be abandoned (DungeonRun.Finish needs no class).
        if (!activeUpgraded)
        {
            log?.Invoke("Dungeon Run save upgrade: the run in progress could not be upgraded; the save stays at version " + from);
            return false;
        }
        save.Version = 2;
        return true;
    }

    /// <summary>
    /// The class model row of a version-1 hero id. An unknown id falls back to a class id used as a hero id, then the class of
    /// the first class card in <paramref name="deck"/>, then (only with <paramref name="fallbackToFirstClass"/>, for the run in
    /// progress, which needs a class to be played) the first class; otherwise null. Every fallback is logged.
    /// </summary>
    internal static DungeonRunLegacyHero Resolve(DungeonRunData data, string heroId, IEnumerable<string> deck, Action<string> log,
        bool fallbackToFirstClass)
    {
        if (heroId != null && data.LegacyHeroes.TryGetValue(heroId, out var row) && data.FindClass(row.Class) != null) return row;
        string cls = data.FindClass(heroId)?.Id;
        if (cls == null && deck != null)
            foreach (string id in deck)
            {
                CardClass card = ClassOf(id);
                if (card != CardClass.NEUTRAL && card != CardClass.INVALID && data.FindClass(card.ToString()) != null) { cls = card.ToString(); break; }
            }
        if (cls == null && !fallbackToFirstClass)
        {
            log?.Invoke("Dungeon Run save: unknown hero " + (heroId ?? "(none)") + ", kept without a class");
            return null;
        }
        cls = cls ?? data.Classes[0].Id;
        log?.Invoke("Dungeon Run save: unknown hero " + (heroId ?? "(none)") + ", treated as " + cls);
        return new DungeonRunLegacyHero { Class = cls };
    }

    private static CardClass ClassOf(string id)
    {
        try { return Cards.FromId(id)?.Class ?? CardClass.INVALID; } catch (Exception) { return CardClass.INVALID; }
    }

    private static bool IsCard(string id)
    {
        try { return id != null && Cards.FromId(id) != null; } catch (Exception) { return false; }
    }

    private static bool IsHeroPower(string keyOrId)
    {
        try { return Cards.FromId(DungeonRunSetup.PowerCardId(keyOrId))?.Type == CardType.HERO_POWER; } catch (Exception) { return false; }
    }
}
