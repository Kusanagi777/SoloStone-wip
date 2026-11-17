using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using HarmonyLib;
using SabberStoneCore.CardSets.DungeonRun;
using UnityEngine;
using EngineCard = SabberStoneCore.Model.Card;

// Dungeon Run screen over the HUB (no scene change): run setup (class, then hero when the class has named adventure heroes,
// then deck, then hero power, then a signature treasure for a Tombs hero, then confirm), the run hub
// between fights, drafts and the run summary. The page is chosen from DungeonRunRuntime.Save.Active on every rebuild; only
// the setup walk and the open hub view live here. Setup reads the same catalog the native picker uses
// (DungeonRunRuntime.Classes/DecksFor/PowersFor) and starts through DungeonRunRuntime.NewRun. Every mutation commits the
// sidecar through DungeonRunRuntime.Mutate and is caught, so a failure shows as a caption, never in the client.
internal static class NativeDungeonRunUI
{
    internal const int ClassesPerPage = 6, CardsPerPage = 6, TreasuresPerPage = 4;
    // Phase 6 adds Hero (a class with named adventure heroes) and Signature (a hero row with signature treasures); the order is
    // the walk's, so "step > Step.Deck" still means "past the deck choice".
    internal enum Step { Class, Hero, Deck, Power, Signature, Confirm }
    internal enum View { Main, Deck, Treasures, Abandon }

    internal sealed class RowSpec { internal string Id, Label; internal Action Action; internal bool Enabled = true; }
    internal sealed class RunPage
    {
        internal string Header = NativeDungeonRunText.Header;
        internal readonly List<string> Lines = new List<string>();
        internal readonly List<RowSpec> Rows = new List<RowSpec>();
        internal void Row(string id, string label, Action action, bool enabled = true) =>
            Rows.Add(new RowSpec { Id = id, Label = label, Action = action, Enabled = enabled && action != null });
    }

    private static ManualLogSource log;
    private static NativeDungeonRunMenu popup;
    private static bool initialized, reopenPending;
    private static float reopenSince;
    private const float ReopenWindow = 180;
    private static Step step;
    private static View view;
    private static int page, classPage;
    private static string classId, deckId, powerId, message;
    // The row the run starts on (the class row, or a named hero row under it) and the signature picked (Tombs).
    private static string rowId, signatureId;

    internal static bool IsOpen => popup != null && popup.IsShown();
    // Joins every home stacking guard: nothing else opens over the run screen.
    internal static bool BlocksHome => IsOpen;
    // The home Fan-made modes face opens Dungeon Run once the client is loaded (owner, 2026-11-09). Its label and look stay
    // as before; an unavailable mode (data failed validation) opens the disabled page with the reasons, which are also
    // logged by DungeonRunRuntime ("Dungeon Run disabled: ..."). The Game Modes slot keeps its decorative plate.
    internal static bool HomeReady { get { try { return ClientBridge.Ready; } catch (Exception) { return false; } } }
    private const int ShownReasons = 3;

    internal static void Initialize(Harmony harmony, ManualLogSource logger)
    {
        log = logger;
        // Every reason the mode disables itself (and its ready line) goes to the plugin log.
        DungeonRunRuntime.Log = message => logger?.LogWarning(message);
        if (initialized) return;
        initialized = true;
        harmony.Patch(AccessTools.Method(typeof(BnetBar), "HandleEscapeKey"),
            prefix: new HarmonyMethod(typeof(NativeDungeonRunUI), nameof(BeforeEscape)) { priority = Priority.First });
        DungeonRunRuntime.FightClosed += OnFightClosed;
    }

    internal static void Guard(Action action)
    {
        try { action(); } catch (Exception failure) { Failed(failure); }
    }
    internal static void Failed(Exception failure) => log?.LogWarning("DUNGEON_RUN_UI " + failure.GetType().Name + ": " + failure.Message);
    internal static void LogLayout(string line) => log?.LogInfo(line);

    private static bool BeforeEscape(ref bool __result)
    {
        if (!IsOpen) return true;
        Guard(() => { if (!Back()) Close(); });
        __result = true; return false;
    }

    // Raised while the board is being left: remember, and open once the HUB is idle (Update).
    // A fight's reopen never carries a picker refusal that an earlier, lapsed reopen left behind.
    private static void OnFightClosed() { pendingNotice = null; Reopen(); }
    private static void Reopen() { reopenPending = true; reopenSince = Time.realtimeSinceStartup; }
    // The native class picker (NativeDungeonRunPicker) hands back here after Play or a fallback: open once the HUB is idle.
    // A refusal from its Play (DungeonRunRuntime.NewRun) is shown as the caption when the screen opens.
    internal static void OpenWhenHome(string notice = null) { pendingNotice = notice; Reopen(); }
    private static string pendingNotice;

    internal static void Open() => Guard(() => {
        reopenPending = false;
        if (popup == null) popup = new GameObject("SoloStoneDungeonRunDialog").AddComponent<NativeDungeonRunMenu>();
        ResetFlow(); message = pendingNotice; pendingNotice = null;
        // Progress already earned (or carried over from a version-1 save) shows as unlocked now; the next commit writes it.
        DungeonRunRuntime.RefreshUnlocks();
        popup.Open();
    });

    internal static void Close() => Guard(() => popup?.Hide());

    // Ticked by NativeUIProbe.Update: applies deferred rebuilds (never inside a row's own release handler) and the reopen.
    internal static void Update() => Guard(() => {
        popup?.ApplyRefresh();
        if (!reopenPending) return;
        if (Time.realtimeSinceStartup - reopenSince > ReopenWindow) { reopenPending = false; pendingNotice = null; return; }
        if (ReopenReady()) Open();
    });

    private static bool ReopenReady()
    {
        if (IsOpen || !SceneMgr.IsInitialized()) return false;
        var scene = SceneMgr.Get();
        var box = Box.Get();
        var input = UniversalInputManager.Get();
        var shop = Shop.Get();
        // As ClientBridge.HomeIdle and NativeHelpUI.HomeReady: no pending scene change, no dialog, no open shop.
        return scene.GetMode() == SceneMgr.Mode.HUB && !scene.IsTransitionNowOrPending() && box != null && box.IsInStateWithButtons()
            && input != null && !input.IsDialogActive() && DialogManager.Get()?.ShowingDialog() != true
            && !(shop != null && shop.IsOpen())
            && LocalLadder.Screen == "Menu" && !NativeRewardUI.BlocksHome && !NativeAchievements.Active
            && !NativeProfileSelector.BlocksHome;
    }

    private static void ResetFlow()
    {
        step = Step.Class; view = View.Main; page = classPage = 0;
        classId = deckId = powerId = rowId = signatureId = null;
    }

    private static void Refresh() => popup?.Refresh();

    // A mutation: runs, then rebuilds; an exception becomes the caption error.
    private static void Mutate(Action action)
    {
        try { message = null; action(); }
        catch (Exception failure) { message = NativeDungeonRunText.Failed(failure.Message); Failed(failure); }
        Refresh();
    }

    // ------------------------------------------------------------------ navigation

    // One step back (setup walk) or out of a hub view; false when the page has nowhere to go back to.
    private static bool Back()
    {
        var active = DungeonRunRuntime.Available ? DungeonRunRuntime.Save?.Active : null;
        if (active == null && step != Step.Class)
        {
            switch (step)
            {
                case Step.Hero: step = Step.Class; classId = rowId = null; page = classPage; break;
                case Step.Deck:
                    deckId = null;
                    if (HasHeroStep(classId)) { step = Step.Hero; rowId = null; page = 0; }
                    else { step = Step.Class; classId = rowId = null; page = classPage; }
                    break;
                case Step.Power: step = Step.Deck; powerId = null; page = 0; break;
                case Step.Signature: step = Step.Power; signatureId = null; page = 0; break;
                default: step = HasSignatures(rowId) ? Step.Signature : Step.Power; page = 0; break;
            }
            message = null; Refresh(); return true;
        }
        if (active != null && active.Phase == DungeonRunPhase.PreMatch && view != View.Main)
        {
            view = View.Main; page = 0; message = null; Refresh(); return true;
        }
        return false;
    }

    private static void Turn(int delta) { page += delta; message = null; Refresh(); }
    private static void ShowView(View next) { view = next; page = 0; message = null; Refresh(); }

    // ------------------------------------------------------------------ actions (each commits)

    // A class with named heroes opens the Hero step; one with none starts on its class row straight away.
    private static void SelectClass(string id)
    {
        classPage = page; classId = id; deckId = powerId = signatureId = null;
        if (HasHeroStep(id)) { rowId = null; step = Step.Hero; } else { rowId = id; step = Step.Deck; }
        page = 0; message = null; Refresh();
    }
    private static void ChooseHero(string id) { rowId = id; deckId = powerId = signatureId = null; step = Step.Deck; page = 0; message = null; Refresh(); }
    private static void ChooseDeck(string id) { deckId = id; powerId = null; step = Step.Power; page = 0; message = null; Refresh(); }
    private static void ChoosePower(string id) { powerId = id; signatureId = null; step = HasSignatures(rowId) ? Step.Signature : Step.Confirm; page = 0; message = null; Refresh(); }
    private static void ChooseSignature(string id) { signatureId = id; step = Step.Confirm; page = 0; message = null; Refresh(); }
    private static bool HasHeroStep(string cls) => cls != null && DungeonRunRuntime.HeroesFor(cls).Count > 1;
    private static bool HasSignatures(string row) => row != null && DungeonRunRuntime.HeroesFor(classId ?? row).Any(h => h.Id == row && h.SignatureCount > 0);

    private static void StartRun()
    {
        try
        {
            message = DungeonRunRuntime.NewRun(rowId, deckId, powerId, signatureId);
            if (message == null) ResetFlow();
        }
        catch (Exception failure) { message = NativeDungeonRunText.Failed(failure.Message); Failed(failure); }
        Refresh();
    }

    private static void Fight()
    {
        try
        {
            message = null;
            string refusal = LocalLadder.StartRun();
            if (refusal == null) { Close(); return; }
            message = refusal;
        }
        catch (Exception failure) { message = NativeDungeonRunText.Failed(failure.Message); Failed(failure); }
        Refresh();
    }

    private static void ConfirmAbandon() => Mutate(() => {
        var rules = DungeonRunRuntime.Rules ?? throw new InvalidOperationException("Dungeon Run is unavailable.");
        DungeonRunRuntime.Mutate(save => rules.Abandon(save.Active ?? throw new InvalidOperationException("No run in progress."), save.Profile));
        view = View.Main; page = 0;
    });

    private static void Pick(string id, bool treasure) => Mutate(() => {
        var rules = DungeonRunRuntime.Rules ?? throw new InvalidOperationException("Dungeon Run is unavailable.");
        DungeonRunRuntime.Mutate(save =>
        {
            var state = save.Active ?? throw new InvalidOperationException("No run in progress.");
            if (treasure) rules.PickTreasure(state, id); else rules.PickBundle(state, id);
        });
        view = View.Main; page = 0;
    });

    private static void ClearFinished(bool close)
    {
        Mutate(() => {
            DungeonRunRuntime.Mutate(save =>
            {
                if (save.Active != null && !save.Active.Finished) throw new InvalidOperationException("The run is not over.");
                save.Active = null;
            });
            ResetFlow();
        });
        if (close && message == null) Close();
    }

    // ------------------------------------------------------------------ names

    // A content key (power, version-1 shrine tier, treasure) or a plain engine card id.
    private static string Describe(string key, out string text)
    {
        text = "";
        if (string.IsNullOrEmpty(key)) return "";
        try
        {
            if (DungeonRunContent.TryGet(key, out RunContent content))
            {
                text = NativeDungeonRunText.CleanCardText(content.Text);
                return content.Name;
            }
            EngineCard card = SabberStoneCore.Model.Cards.FromId(key);
            text = NativeDungeonRunText.CleanCardText(card?.Text);
            return card?.Name ?? key;
        }
        catch (Exception) { return key; }
    }

    private static string Named(string key) => Describe(key, out _);

    // Phase 7: the hub's and summary's shrine line (board shrine, then its upgrades in pick order); null with no board shrine.
    private static string ShrineLine(DungeonRunState state) => state.HasBoardShrine
        ? NativeDungeonRunText.ShrineLine(Named(state.BoardShrine), (state.ShrineUpgrades ?? new List<string>()).Select(Named)) : null;

    // Phase 7: the Treasures view's shrine block: the board shrine, then its upgrades under their own heading.
    private static IEnumerable<string> ShrineEntries(DungeonRunState state)
    {
        if (!state.HasBoardShrine) yield break;
        yield return NativeDungeonRunText.ShrineHeading + ": " + Described(state.BoardShrine);
        var upgrades = state.ShrineUpgrades ?? new List<string>();
        if (upgrades.Count == 0) yield break;
        yield return NativeDungeonRunText.ShrineUpgradesHeading;
        foreach (string key in upgrades) yield return Described(key);
    }

    private static string Described(string key) => NativeDungeonRunText.Described(Describe(key, out string text), text);

    // A deck card: an active-treasure shell shows its run name, any other card its engine name.
    private static string CardName(string id)
    {
        try
        {
            var shell = DungeonRunContent.ForShell(id);
            if (shell != null) return shell.Name;
            return SabberStoneCore.Model.Cards.FromId(id)?.Name ?? id;
        }
        catch (Exception) { return id; }
    }

    private static string BossName(DungeonRun rules, string id)
    {
        try { return rules.Data.Boss(id).Name; } catch (Exception) { return id; }
    }

    // ------------------------------------------------------------------ pages

    internal static RunPage Compose()
    {
        var result = new RunPage();
        try { ComposeInto(result); }
        catch (Exception failure)
        {
            Failed(failure);
            result = new RunPage();
            result.Lines.Add(NativeDungeonRunText.Failed(failure.Message));
            result.Row(NativeDungeonRunText.CloseId, NativeDungeonRunText.CloseLabel, Close);
            return result;
        }
        if (message != null) result.Lines.Add(message);
        return result;
    }

    private static void ComposeInto(RunPage result)
    {
        if (!DungeonRunRuntime.Available)
        {
            result.Header = NativeDungeonRunText.UnavailableHeader;
            var reasons = DungeonRunRuntime.DisabledReasons;
            result.Lines.Add(NativeDungeonRunText.Unavailable(reasons.FirstOrDefault()));
            foreach (string reason in reasons.Skip(1).Take(ShownReasons - 1)) result.Lines.Add(reason);
            if (reasons.Count > ShownReasons) result.Lines.Add(NativeDungeonRunText.MoreReasons(reasons.Count - ShownReasons));
            result.Row(NativeDungeonRunText.CloseId, NativeDungeonRunText.CloseLabel, Close);
            return;
        }
        var rules = DungeonRunRuntime.Rules;
        var save = DungeonRunRuntime.Save;
        if (rules == null || save == null)
        {
            result.Lines.Add(NativeDungeonRunText.NoSaveCaption);
            result.Row(NativeDungeonRunText.CloseId, NativeDungeonRunText.CloseLabel, Close);
            return;
        }
        var state = save.Active;
        if (state == null) { SetupFlow(result); return; }
        // InMatch with no fight staged (LocalLadder.FinishDungeonRun could not record the outcome): restage the floor as a
        // load would (Rules.Resume), instead of a "Fight in progress" page that nothing can leave until a relaunch.
        if (state.Phase == DungeonRunPhase.InMatch && !DungeonRunRuntime.InRunMatch && LocalLadder.Screen == "Menu"
            && LocalLadder.Profile?.HasPendingMatch == false)
        {
            rules.Resume(state);
            try { DungeonRunRuntime.Commit(); } catch (Exception failure) { Failed(failure); }
            log?.LogInfo("DUNGEON_RUN_UI stale fight restaged at floor " + state.Floor);
        }
        switch (state.Phase)
        {
            case DungeonRunPhase.PreMatch: RunHub(result, rules, state); return;
            case DungeonRunPhase.TreasureDraft:
            case DungeonRunPhase.BundleDraft: Draft(result, rules, state); return;
            case DungeonRunPhase.Victory:
            case DungeonRunPhase.Defeat: Summary(result, rules, state); return;
            // Act-run nodes: they resume exactly where they were (their offers are saved), so no restage is needed.
            case DungeonRunPhase.Tavern: TavernPage(result, rules, state); return;
            case DungeonRunPhase.Modifier: ModifierPage(result, rules, state); return;
            default:
                result.Lines.Add(NativeDungeonRunText.InMatchCaption);
                result.Row(NativeDungeonRunText.CloseId, NativeDungeonRunText.CloseLabel, Close);
                return;
        }
    }

    private static void Paging(RunPage result, int count, int perPage)
    {
        string line = NativeDungeonRunText.PageLine(page, NativeDungeonRunText.Pages(count, perPage));
        if (line != null) result.Lines.Add(line);
        if (page > 0) result.Row(NativeDungeonRunText.PreviousId, NativeDungeonRunText.PreviousLabel, () => Turn(-1));
        if ((page + 1) * perPage < count) result.Row(NativeDungeonRunText.NextId, NativeDungeonRunText.NextLabel, () => Turn(1));
    }

    // Class -> Hero (named adventure heroes) -> Deck -> Power -> Signature (Tombs) -> Confirm, over the shared catalog. Locked
    // decks and powers show greyed with the hint in the caption.
    private static void SetupFlow(RunPage result)
    {
        var classes = DungeonRunRuntime.Classes;
        var cls = classId == null ? null : classes.FirstOrDefault(c => c.Id == classId);
        var heroes = cls == null ? new RunHeroOption[0] : DungeonRunRuntime.HeroesFor(cls.Id);
        var row = rowId == null ? null : heroes.FirstOrDefault(h => h.Id == rowId);
        var decks = row == null ? new RunDeckOption[0] : DungeonRunRuntime.DecksFor(row.Id);
        var powers = row == null ? new RunPowerOption[0] : DungeonRunRuntime.PowersFor(row.Id);
        var signatures = row == null || row.SignatureCount == 0 ? new RunSignatureOption[0] : DungeonRunRuntime.SignaturesFor(row.Id);
        var deck = decks.FirstOrDefault(d => d.Id == deckId && d.Unlocked);
        var power = powers.FirstOrDefault(p => p.Id == powerId && p.Unlocked);
        var signature = signatures.FirstOrDefault(s => s.Id == signatureId);
        // The save under the menu can change (profile switch): a choice no longer available walks back to where it was made.
        if (step != Step.Class && cls == null) { ResetFlow(); }
        else if (step > Step.Hero && row == null) { step = heroes.Count > 1 ? Step.Hero : Step.Class; rowId = deckId = powerId = signatureId = null; page = 0; }
        else if (step > Step.Deck && deck == null) { step = Step.Deck; deckId = powerId = signatureId = null; page = 0; }
        else if (step > Step.Power && power == null) { step = Step.Power; powerId = signatureId = null; page = 0; }
        else if (step > Step.Signature && signatures.Count > 0 && signature == null) { step = Step.Signature; signatureId = null; page = 0; }
        var back = new Action(() => Back());
        switch (step)
        {
            case Step.Class:
                ClassList(result, classes);
                return;
            case Step.Hero:
                result.Header = NativeDungeonRunText.HeroHeader;
                result.Lines.Add(NativeDungeonRunText.HeroCaption);
                foreach (var option in heroes)
                {
                    string id = option.Id;
                    result.Lines.Add(NativeDungeonRunText.HeroLine(option.Name, option.Adventure, option.ClassName, option.IsHero ? option.Health : 0));
                    result.Row(NativeDungeonRunText.HeroIdPrefix + id, option.Name, () => ChooseHero(id));
                }
                break;
            case Step.Deck:
                result.Header = NativeDungeonRunText.DeckChoiceHeader;
                result.Lines.Add(row.Name + " - " + row.ClassName);
                foreach (var option in decks)
                {
                    string id = option.Id;
                    if (option.Unlocked)
                    {
                        result.Lines.Add(NativeDungeonRunText.Described(option.Name, option.Description));
                        result.Row(NativeDungeonRunText.DeckPickIdPrefix + id, option.Name, () => ChooseDeck(id));
                    }
                    else
                    {
                        // The plank stays short; the unlock hint goes in the caption.
                        result.Lines.Add(NativeDungeonRunText.HintLine(option.Name, option.UnlockHint));
                        result.Row(NativeDungeonRunText.DeckPickIdPrefix + id, NativeDungeonRunText.LockedChoiceLabel(option.Name), null, false);
                    }
                }
                break;
            case Step.Power:
                result.Header = NativeDungeonRunText.PowerHeader;
                foreach (var option in powers)
                {
                    string id = option.Id;
                    if (option.Unlocked)
                    {
                        result.Lines.Add(NativeDungeonRunText.Described(option.Name, option.PlainText));
                        result.Row(NativeDungeonRunText.PowerIdPrefix + id, option.Name, () => ChoosePower(id));
                    }
                    else
                    {
                        result.Lines.Add(NativeDungeonRunText.HintLine(option.Name, option.UnlockHint));
                        result.Row(NativeDungeonRunText.PowerIdPrefix + id, NativeDungeonRunText.LockedChoiceLabel(option.Name), null, false);
                    }
                }
                break;
            case Step.Signature:
                result.Header = NativeDungeonRunText.SignatureHeader;
                foreach (var option in signatures)
                {
                    string id = option.Id;
                    result.Lines.Add(NativeDungeonRunText.Described(option.Name, NativeDungeonRunText.PowerText(option.Text)));
                    result.Row(NativeDungeonRunText.SignatureIdPrefix + id, option.Name, () => ChooseSignature(id));
                }
                break;
            default:
                result.Header = NativeDungeonRunText.ConfirmHeader;
                result.Lines.Add(NativeDungeonRunText.ConfirmLine(row.Name));
                result.Lines.Add(NativeDungeonRunText.StarterDeckLine(deck.Name));
                // A generated starter (Heist and Tombs "Random") is drawn when the run starts: it lists no cards here.
                if (deck.CardIds.Count > 0) result.Lines.Add(NativeDungeonRunText.DeckCardsLine(deck.CardIds.Select(CardName)));
                result.Lines.Add(NativeDungeonRunText.PowerLine(power.Name, power.PlainText));
                if (signature != null) result.Lines.Add(NativeDungeonRunText.SignatureLine(signature.Name, signature.Text));
                result.Row(NativeDungeonRunText.StartId, NativeDungeonRunText.StartLabel, StartRun);
                break;
        }
        result.Row(NativeDungeonRunText.BackId, NativeDungeonRunText.BackLabel, back);
    }

    private static void ClassList(RunPage result, IReadOnlyList<RunClassOption> classes)
    {
        result.Header = NativeDungeonRunText.ClassHeader;
        result.Lines.Add(NativeDungeonRunText.ClassCaption);
        // Catalog cards the client does not know are dropped at load (DungeonRunValidator.DropClientUnknown): never silently.
        int dropped = DungeonRunRuntime.CatalogDiagnostics.Count;
        if (dropped > 0) result.Lines.Add(NativeDungeonRunText.CatalogDiagnosticsLine(dropped));
        page = NativeDungeonRunText.ClampPage(page, classes.Count, ClassesPerPage);
        foreach (var cls in classes.Skip(page * ClassesPerPage).Take(ClassesPerPage))
        {
            string id = cls.Id;
            result.Row(NativeDungeonRunText.ClassIdPrefix + id, NativeDungeonRunText.ClassLabel(cls.Name, cls.Id), () => SelectClass(id));
        }
        Paging(result, classes.Count, ClassesPerPage);
        result.Row(NativeDungeonRunText.CloseId, NativeDungeonRunText.CloseLabel, Close);
    }

    private static void RunHub(RunPage result, DungeonRun rules, DungeonRunState state)
    {
        var back = new Action(() => Back());
        if (view == View.Deck)
        {
            result.Header = NativeDungeonRunText.DeckHeader;
            // An act run lists each card as changed by the tavern ("Name (3/4, cost 2) [opening]"); equal texts group.
            var groups = (state.IsActRun
                    ? state.Cards.Select(c => DungeonRunDeck.Describe(c, CardName)).GroupBy(label => label).Select(g => new KeyValuePair<string, int>(g.Key, g.Count()))
                    : state.Deck.GroupBy(id => id).Select(g => new KeyValuePair<string, int>(CardName(g.Key), g.Count())))
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).ToList();
            page = NativeDungeonRunText.ClampPage(page, groups.Count, CardsPerPage);
            result.Lines.Add(NativeDungeonRunText.DeckLine(state.Deck.Count));
            if (groups.Count == 0) result.Lines.Add(NativeDungeonRunText.EmptyDeckCaption);
            foreach (var pair in groups.Skip(page * CardsPerPage).Take(CardsPerPage)) result.Lines.Add(NativeDungeonRunText.CountLine(pair.Key, pair.Value));
            Paging(result, groups.Count, CardsPerPage);
            result.Row(NativeDungeonRunText.BackId, NativeDungeonRunText.BackLabel, back);
            return;
        }
        if (view == View.Treasures)
        {
            result.Header = NativeDungeonRunText.TreasuresHeader;
            // The treasures, then (phase 7) the board shrine and its upgrades under their own headings: they are listed with
            // the treasures but are not treasures, so the treasure count and caption read the treasures alone.
            var treasures = state.Treasures.Concat(state.ActiveTreasures).ToList();
            var all = treasures.Select(Described).ToList();
            if (treasures.Count == 0) all.Add(NativeDungeonRunText.NoTreasuresCaption);
            all.AddRange(ShrineEntries(state));
            page = NativeDungeonRunText.ClampPage(page, all.Count, TreasuresPerPage);
            foreach (string line in all.Skip(page * TreasuresPerPage).Take(TreasuresPerPage)) result.Lines.Add(line);
            Paging(result, all.Count, TreasuresPerPage);
            result.Row(NativeDungeonRunText.BackId, NativeDungeonRunText.BackLabel, back);
            return;
        }
        if (view == View.Abandon)
        {
            result.Header = NativeDungeonRunText.AbandonHeader;
            result.Lines.Add(NativeDungeonRunText.AbandonCaption);
            result.Row(NativeDungeonRunText.ConfirmAbandonId, NativeDungeonRunText.ConfirmAbandonLabel, ConfirmAbandon);
            result.Row(NativeDungeonRunText.BackId, NativeDungeonRunText.BackLabel, back);
            return;
        }
        result.Header = NativeDungeonRunText.Header;
        var boss = rules.CurrentBoss(state);
        result.Lines.Add(ProgressLine(rules, state, boss));
        int skipped = rules.SkippedDraftFloor(state);
        if (skipped > 0) result.Lines.Add(NativeDungeonRunText.SkippedDraftLine(skipped));
        result.Lines.Add(NativeDungeonRunText.NextLine(boss.Name, rules.BossHealth(state)));
        // Phase 8: the global rules active on this act are shown with the modifiers.
        string rulesLine = NativeDungeonRunText.RulesLine(rules.ActiveModifiers(state).Select(m => m.Name)
            .Concat(rules.ActiveGlobalRules(state).Select(r => NativeDungeonRunText.RuleName(r.Id))));
        if (rulesLine != null) result.Lines.Add(rulesLine);
        if (!string.IsNullOrEmpty(boss.Power)) result.Lines.Add(Described(boss.Power));
        // A migrated version-1 run has no starter deck id: class and power only.
        result.Lines.Add(NativeDungeonRunText.ChoiceLine(rules.Data.FindClass(state.Class)?.Name ?? state.Hero,
            rules.Data.FindDeck(state.StarterDeck)?.Name, Named(rules.PlayerPower(state))));
        result.Lines.Add(NativeDungeonRunText.DeckLine(state.Deck.Count));
        result.Lines.Add(NativeDungeonRunText.TreasureCountLine(state.Treasures.Count + state.ActiveTreasures.Count));
        string shrineLine = ShrineLine(state);
        if (shrineLine != null) result.Lines.Add(shrineLine);
        result.Row(NativeDungeonRunText.FightId, NativeDungeonRunText.FightLabel, Fight);
        result.Row(NativeDungeonRunText.DeckId, NativeDungeonRunText.DeckLabel, () => ShowView(View.Deck));
        result.Row(NativeDungeonRunText.TreasuresId, NativeDungeonRunText.TreasuresLabel, () => ShowView(View.Treasures));
        result.Row(NativeDungeonRunText.AbandonId, NativeDungeonRunText.AbandonLabel, () => ShowView(View.Abandon));
        result.Row(NativeDungeonRunText.CloseId, NativeDungeonRunText.CloseLabel, Close);
    }

    // The floor line: a legacy run's "Floor 3 of 8 . Band 1"; an act run's "Act II . Floor 3 of 5 (8 of 15)".
    private static string ProgressLine(DungeonRun rules, DungeonRunState state, DungeonRunBoss boss)
    {
        if (!state.IsActRun) return NativeDungeonRunText.FloorLine(state.Floor, rules.Rules.Floors, rules.Rules.BandOf(state.Floor), boss.Final);
        var schedule = rules.ScheduleFor(state);
        var node = rules.CurrentNode(state);
        var act = schedule.Acts[node.Act - 1];
        return NativeDungeonRunText.ActFloorLine(act.Id, node.FloorInAct, act.Floors, node.Floor, schedule.FloorCount, node.Kind == DungeonRunNodeKind.Major);
    }

    private static void ModifierPage(RunPage result, DungeonRun rules, DungeonRunState state)
    {
        result.Header = NativeDungeonRunText.ModifierHeader;
        var act = rules.CurrentAct(state);
        // Phase 8: an anomaly node offers global rules by their card (its own name and text) and "none".
        bool anomaly = state.ModifierOffer?.Kind == DungeonRunGlobalRule.KindAnomaly;
        result.Lines.Add(anomaly ? NativeDungeonRunText.AnomalyCaption(act.Id) : NativeDungeonRunText.ModifierCaption(act.Id));
        int skipped = rules.SkippedDraftFloor(state);
        if (skipped > 0) result.Lines.Add(NativeDungeonRunText.SkippedDraftLine(skipped));
        foreach (string id in state.ModifierOffer?.Offered ?? new List<string>())
        {
            string captured = id;
            if (anomaly)
            {
                bool none = id == DungeonRunGlobalRule.None;
                string label = none ? NativeDungeonRunText.NoAnomalyLabel : NativeDungeonRunText.RuleName(id);
                result.Lines.Add(NativeDungeonRunText.Described(label, none ? NativeDungeonRunText.NoAnomalyText : NativeDungeonRunText.RuleText(id)));
                result.Row(NativeDungeonRunText.ModifierIdPrefix + id, label, () => NodeAction(s => rules.PickModifier(s, captured)));
                continue;
            }
            // Step 9 (D7): a stamp-4 modifier offer ends with "none".
            if (id == DungeonRunGlobalRule.None)
            {
                result.Lines.Add(NativeDungeonRunText.Described(NativeDungeonRunText.NoModifierLabel, NativeDungeonRunText.NoModifierText));
                result.Row(NativeDungeonRunText.ModifierIdPrefix + id, NativeDungeonRunText.NoModifierLabel, () => NodeAction(s => rules.PickModifier(s, captured)));
                continue;
            }
            var m = rules.Data.Schedules.Modifier(id);
            string name = m?.Name ?? id;
            result.Lines.Add(NativeDungeonRunText.Described(name, m == null ? "" : NativeDungeonRunText.ModifierEffect(m.BossHealthDelta, m.DraftOptionsDelta,
                m.TavernGoldDelta, m.BossManaDelta, m.PlayerHealthDelta, m.OpeningHandBonus, m.ExtraTreasureAfterMajor)));
            result.Row(NativeDungeonRunText.ModifierIdPrefix + id, name, () => NodeAction(s => rules.PickModifier(s, captured)));
        }
        result.Row(NativeDungeonRunText.CloseId, NativeDungeonRunText.CloseLabel, Close);
    }

    // Bob's tavern (phase 5): the boards, the offers and a pending purchase's choices (NativeDungeonRunTavernUI).
    private static void TavernPage(RunPage result, DungeonRun rules, DungeonRunState state) =>
        NativeDungeonRunTavernUI.Compose(result, rules, state, NodeAction, CardName, Close);

    // A node or draft action on the active run, committed through the runtime (rolled back on failure).
    private static void NodeAction(Action<DungeonRunState> action) => Mutate(() => {
        DungeonRunRuntime.Mutate(save => action(save.Active ?? throw new InvalidOperationException("No run in progress.")));
        view = View.Main; page = 0; keepOffer = null;
    });

    // The offer whose cards a takes draft is showing (UI only until the first card is kept; then the draft stores it).
    private static string keepOffer;

    private static void Draft(RunPage result, DungeonRun rules, DungeonRunState state)
    {
        bool treasure = state.Phase == DungeonRunPhase.TreasureDraft;
        result.Header = treasure ? NativeDungeonRunText.TreasureDraftHeader : NativeDungeonRunText.BundleDraftHeader;
        var draft = state.OpenDraft;
        // Phase 7's shrine-upgrade and teammate drafts reuse the treasure screen (their ids are real client cards, shown as is).
        if (treasure && draft?.Kind == DungeonRun.ShrineUpgradeKind) result.Header = NativeDungeonRunText.ShrineUpgradeDraftHeader;
        if (treasure && draft?.Kind == DungeonRun.TeammateKind) result.Header = NativeDungeonRunText.TeammateDraftHeader;
        // An act draft that keeps fewer cards than a bundle holds: choose an offer, then its cards one at a time.
        if (!treasure && draft != null && draft.Takes > 0)
        {
            result.Lines.Add(NativeDungeonRunText.KeepCaption(draft.Takes, draft.Chosen.Count));
            string chosen = draft.Choosing ?? (keepOffer != null && draft.Offered.Contains(keepOffer) ? keepOffer : null);
            if (chosen != null)
            {
                var left = rules.OfferCards(draft, chosen);
                foreach (string kept in draft.Chosen) left.Remove(kept);
                int n = 0;
                foreach (string card in left)
                {
                    string bundle = chosen, captured = card;
                    result.Row(NativeDungeonRunText.KeepIdPrefix + card + "#" + n++, CardName(card), () => NodeAction(s => rules.PickDraftCard(s, bundle, captured)));
                }
                result.Row(NativeDungeonRunText.BackId, NativeDungeonRunText.BackLabel, () => {
                    keepOffer = null;
                    if (draft.Chosen.Count > 0) NodeAction(rules.ClearDraftCards); else Refresh();
                });
                return;
            }
        }
        if (draft == null || draft.Offered.Count == 0) result.Lines.Add(NativeDungeonRunText.NoChoicesCaption);
        else
        {
            var used = new Dictionary<string, int>();
            foreach (string id in draft.Offered)
            {
                string captured = id, label, line;
                if (treasure)
                {
                    label = Describe(id, out string text);
                    line = NativeDungeonRunText.Described(label, text);
                }
                else if (draft.Offer(id) is DungeonRunOffer offer)
                {
                    // A catalog draft: the stored offer (its label, adventure tag and drawn cards; a repeated card shows twice).
                    label = NativeDungeonRunText.OfferLabel(offer.Label ?? NativeDungeonRunText.ThemeName(offer.Theme), offer.Source);
                    line = NativeDungeonRunText.BundleLine(label, offer.Cards.Select(CardName));
                }
                else
                {
                    var bundle = rules.Data.Bundles.FirstOrDefault(b => b.Id == id);
                    label = NativeDungeonRunText.ThemeName(bundle?.Theme);
                    line = NativeDungeonRunText.BundleLine(label, (bundle?.Cards ?? new List<string>()).Select(CardName));
                }
                // Two offers can share a theme: number the repeats so each plank reads differently.
                used[label] = used.TryGetValue(label, out int seen) ? seen + 1 : 1;
                if (used[label] > 1) { label += " (" + used[label] + ")"; if (!treasure) line = label + line.Substring(line.IndexOf(':')); }
                result.Lines.Add(line);
                if (!treasure && draft.Takes > 0) result.Row(NativeDungeonRunText.PickIdPrefix + id, label, () => { keepOffer = captured; message = null; Refresh(); });
                else result.Row(NativeDungeonRunText.PickIdPrefix + id, label, () => Pick(captured, treasure));
            }
        }
        result.Row(NativeDungeonRunText.CloseId, NativeDungeonRunText.CloseLabel, Close);
    }

    // The summary's result line: a legacy run's "Defeated on floor 8."; an act run's act wording, as the hub showed it. A
    // finished run the loaded schedules cannot place (another schedule version) falls back to the floor wording, so its
    // summary, and New run, still work.
    private static string ResultLine(DungeonRun rules, DungeonRunState state, bool won)
    {
        if (!state.IsActRun || won) return NativeDungeonRunText.ResultLine(won, state.Floor);
        try
        {
            var schedule = rules.ScheduleFor(state);
            var node = rules.CurrentNode(state);
            var act = schedule.Acts[node.Act - 1];
            var last = state.Matches.LastOrDefault(m => m.Outcome != DungeonRunOutcome.Fault);
            bool abandoned = last == null || last.Outcome == DungeonRunOutcome.Win;
            return NativeDungeonRunText.ActResultLine(false, abandoned, act.Id, node.FloorInAct, act.Floors, node.Floor, schedule.FloorCount);
        }
        catch (Exception) { return NativeDungeonRunText.ResultLine(false, state.Floor); }
    }

    private static void Summary(RunPage result, DungeonRun rules, DungeonRunState state)
    {
        bool won = state.Phase == DungeonRunPhase.Victory;
        result.Header = won ? NativeDungeonRunText.VictoryHeader : NativeDungeonRunText.DefeatHeader;
        result.Lines.Add(ResultLine(rules, state, won));
        string last = DungeonRun.LastBoss(state);
        if (last != null) result.Lines.Add(NativeDungeonRunText.LastBossLine(BossName(rules, last)));
        result.Lines.Add(NativeDungeonRunText.SeedLine(state.SeedText));
        result.Lines.Add(NativeDungeonRunText.DeckLine(state.Deck.Count));
        result.Lines.Add(NativeDungeonRunText.TreasureNamesLine(state.Treasures.Concat(state.ActiveTreasures).Select(Named)));
        string shrineLine = ShrineLine(state);
        if (shrineLine != null) result.Lines.Add(shrineLine);
        string unlocked = NativeDungeonRunText.UnlockedLine((state.UnlockedThisRun ?? new List<string>()).Select(key => DungeonRunCatalog.UnlockName(rules, key)));
        if (unlocked != null) result.Lines.Add(unlocked);
        result.Row(NativeDungeonRunText.NewRunId, NativeDungeonRunText.NewRunLabel, () => ClearFinished(false));
        // Close keeps the summary, as Escape and a click outside the board do; only New run clears the finished run.
        result.Row(NativeDungeonRunText.CloseId, NativeDungeonRunText.CloseLabel, Close);
    }
}

// The native menu board and planks (as NativeReportMenu), with one native UberText caption above the planks.
// The caption is NOT a MultiSliceElement slice: the slice stack drops a slice whose bounds it cannot compute and leaves it at
// the container origin, which is where the first plank goes, so the caption ended up behind the top planks. Here the planks
// are the only slices; the caption is rendered once the menu is visible, measured, and placed by hand one plank gap above
// the first plank, at the plank labels' depth; the board then grows both ways to cover it.
internal sealed class NativeDungeonRunMenu : ButtonListMenu
{
    private readonly List<UIBButton> rows = new List<UIBButton>();
    private readonly List<GameObject> owned = new List<GameObject>();
    private readonly List<GameObject> slices = new List<GameObject>();
    private GameObject caption;
    private Component captionText;
    private int captionLines;
    private bool ownsDialog, dirty;
    protected override void Awake() => NativeDungeonRunUI.Guard(() => { m_showAnimation = false; base.Awake(); Hide(); });
    protected override List<UIBButton> GetButtons() => rows;
    internal void Open() { dirty = false; Build(); Show(); }
    // Deferred to NativeDungeonRunUI.Update: the pressed row's release handler may still be on the stack.
    internal void Refresh() => dirty = true;
    internal void ApplyRefresh()
    {
        if (!dirty) return;
        dirty = false; bool shown = IsShown(); if (shown) Hide(); Build(); if (shown) Show(false);
    }
    private void Row(NativeDungeonRunUI.RowSpec spec)
    {
        var action = spec.Action;
        var row = CreateMenuButton(spec.Id, spec.Label, _ => { if (action != null) NativeDungeonRunUI.Guard(action); });
        row.SetText(spec.Label);
        row.SetEnabled(spec.Enabled); if (!spec.Enabled) NativeButtonTint.Apply(row, NativeButtonTint.Tint.Grey);
        rows.Add(row); owned.Add(row.gameObject); slices.Add(row.gameObject);
    }
    // About 44 characters per caption line at this width (the old 48 undercounted wrapped lines).
    internal const int CaptionLineChars = 44;
    internal static int WrappedLines(IEnumerable<string> lines) => lines.Sum(line => Math.Max(1, (line.Length + CaptionLineChars - 1) / CaptionLineChars));
    private void Text(string value, int wrappedLines, UIBButton template)
    {
        var source = ProfileFormKit.Label(template);
        if (source == null) throw new InvalidOperationException("Native Dungeon Run label template missing");
        var copy = UnityEngine.Object.Instantiate(source.gameObject, m_menu.m_buttonContainer.transform, true);
        copy.transform.localPosition = Vector3.zero; copy.transform.localScale *= .7f;
        var text = copy.GetComponent(ProfileFormKit.TextType);
        ProfileFormKit.ScaleRect(text, "Width", 1.65f);
        ProfileFormKit.ScaleRect(text, "Height", Mathf.Clamp(wrappedLines * .9f + .4f, 1.5f, 9f));
        ProfileFormKit.SetText(text, value); ProfileFormKit.SetColor(text, new Color(.22f, .15f, .09f, 1));
        // Not rendered here: the menu is hidden while it builds, and a mesh generated in an inactive hierarchy reports empty
        // bounds. LayoutRun renders it once the menu is shown.
        owned.Add(copy); caption = copy; captionText = text; captionLines = wrappedLines;
    }
    private void Build()
    {
        // Native Show dereferences every cached button, including rows Unity destroyed after an earlier rebuild.
        var all = AccessTools.Field(typeof(ButtonListMenu), "m_allButtons").GetValue(this) as List<UIBButton>;
        all?.Clear();
        foreach (var item in owned) if (item != null) { item.SetActive(false); UnityEngine.Object.Destroy(item); }
        owned.Clear(); rows.Clear(); slices.Clear(); caption = null; captionText = null; captionLines = 0;
        var page = NativeDungeonRunUI.Compose();
        var header = AccessTools.Field(typeof(ButtonListMenuDef), "m_headerText").GetValue(m_menu) as Component;
        ProfileFormKit.SetText(header, page.Header);
        if (page.Rows.Count == 0) page.Row(NativeDungeonRunText.CloseId, NativeDungeonRunText.CloseLabel, NativeDungeonRunUI.Close);
        foreach (var spec in page.Rows) Row(spec);
        var lines = page.Lines.Where(line => !string.IsNullOrEmpty(line)).ToList();
        if (lines.Count > 0) Text(string.Join("\n", lines), WrappedLines(lines), rows[0]);
    }
    public override void Show(bool playSound = true)
    {
        if (ownsDialog) return;
        int before = DeckExperience.GameDialogCount;
        try { base.Show(playSound); ownsDialog = true; }
        catch (Exception failure) { if (DeckExperience.GameDialogCount > before) { ownsDialog = true; Hide(); } else gameObject.SetActive(false); NativeDungeonRunUI.Failed(failure); }
    }
    public override void Hide()
    {
        if (!ownsDialog) { gameObject.SetActive(false); return; }
        try { NativeDungeonRunUI.Guard(() => base.Hide()); } finally { ownsDialog = false; }
    }
    protected override void OnDestroy() => NativeDungeonRunUI.Guard(() => { Hide(); base.OnDestroy(); });
    protected override void OnBlockerRelease(UIEvent e) => NativeDungeonRunUI.Close();
    protected override void LayoutMenu() => NativeDungeonRunUI.Guard(LayoutRun);

    private static Rect Union(Rect a, Rect b) =>
        Rect.MinMaxRect(Math.Min(a.xMin, b.xMin), Math.Min(a.yMin, b.yMin), Math.Max(a.xMax, b.xMax), Math.Max(a.yMax, b.yMax));

    // Where the prefab put the board, its border and the header plate (local positions). LayoutRun moves them to make room
    // for the caption, and native layout never moves them back, so every layout starts again from these: a rebuild (each
    // click) must not add its move to the last one.
    private Vector3? boardHome, borderHome, headerHome;

    private void RestoreBoard()
    {
        var header = m_menu.m_header;
        if (boardHome == null)
        {
            boardHome = m_menu.m_background.transform.localPosition;
            borderHome = m_menu.m_border.transform.localPosition;
            headerHome = header != null ? header.transform.localPosition : (Vector3?)null;
            return;
        }
        m_menu.m_background.transform.localPosition = boardHome.Value;
        m_menu.m_border.transform.localPosition = borderHome.Value;
        if (header != null && headerHome.HasValue) header.transform.localPosition = headerHome.Value;
    }

    // Planks as native slices; then the caption placed above them; then the board grown over both, fit and centred on screen.
    private void LayoutRun()
    {
        var container = m_menu.m_buttonContainer; container.ClearSlices();
        foreach (var slice in slices)
        {
            if (slice == null) continue;
            slice.SetActive(true); // Native Show hides every cached plank immediately before LayoutMenu.
            container.AddSlice(slice, Vector3.zero, Vector3.zero, false, true, false, false);
        }
        RestoreBoard();
        container.UpdateSlices(); LayoutMenuBackground();
        var camera = CameraUtils.FindFirstByLayerMask(1 << m_menu.gameObject.layer);
        if (camera == null) return;
        Rect frame = DeckLayout.ScreenRect(m_menu.m_background.gameObject, camera);
        // Top row first, whatever order the slice stack used.
        var planks = rows.Where(r => r != null).Select(r => DeckLayout.ScreenRect(r.gameObject, camera)).Where(r => r.height > 0)
            .OrderByDescending(r => r.yMax).ToList();
        Rect plankSpan = planks.Count == 0 ? frame : planks.Aggregate(Union);
        Rect captionRect = Rect.zero;
        if (caption != null && planks.Count > 0)
        {
            caption.SetActive(true);
            ProfileFormKit.UpdateNow(captionText);
            Bounds? drawn = ProfileFormKit.RenderedBounds(captionText, true);
            captionRect = drawn.HasValue ? ProfileFormKit.ScreenRect(drawn.Value, camera) ?? Rect.zero : Rect.zero;
            if (captionRect.height <= 0) captionRect = DeckLayout.ScreenRect(caption, camera);
            if (captionRect.height > 0)
            {
                // One plank gap above the top plank (planks[0] is the top row); a single plank uses a quarter of its height.
                float gap = planks.Count > 1 ? Math.Max(2f, planks[0].yMin - planks[1].yMax) : planks[0].height / 4f;
                var label = ProfileFormKit.Label(rows[0]);
                float depth = camera.WorldToScreenPoint(label != null ? label.transform.position : rows[0].transform.position).z;
                float dx = plankSpan.center.x - captionRect.center.x, dy = planks[0].yMax + gap + captionRect.height / 2f - captionRect.center.y;
                // Move by that screen delta and put it at the plank label's depth, so it draws in front of the board, never behind a plank.
                Vector3 pivot = camera.WorldToScreenPoint(caption.transform.position);
                caption.transform.position = camera.ScreenToWorldPoint(new Vector3(pivot.x + dx, pivot.y + dy, depth));
                captionRect = new Rect(captionRect.x + dx, captionRect.y + dy, captionRect.width, captionRect.height);
            }
        }
        // The board covers the caption and every plank with the margins LayoutMenuBackground gave the planks, on both edges.
        float marginX = Math.Max(0, plankSpan.xMin - frame.xMin), marginTop = Math.Max(0, frame.yMax - plankSpan.yMax),
            marginBottom = Math.Max(0, plankSpan.yMin - frame.yMin);
        Rect content = captionRect.height > 0 ? Union(plankSpan, captionRect) : plankSpan;
        Rect want2 = Rect.MinMaxRect(Math.Min(frame.xMin, content.xMin - marginX), Math.Min(frame.yMin, content.yMin - marginBottom),
            Math.Max(frame.xMax, content.xMax + marginX), Math.Max(frame.yMax, content.yMax + marginTop));
        if (frame.width > 0 && frame.height > 0 && (want2.width > frame.width + .5f || want2.height > frame.height + .5f))
        {
            Vector3 atFrame = camera.WorldToScreenPoint(m_menu.m_background.transform.position);
            float worldPerPixel = (camera.ScreenToWorldPoint(atFrame + Vector3.up) - camera.ScreenToWorldPoint(atFrame)).magnitude;
            float width = want2.width * worldPerPixel, height = want2.height * worldPerPixel;
            for (int pass = 0; pass < 2; pass++)
            {
                m_menu.m_background.SetSize(width, height); m_menu.m_border.SetSize(width, height);
                // SetSize is in the board's local units: correct once by what the screen shows.
                Rect got = DeckLayout.ScreenRect(m_menu.m_background.gameObject, camera);
                if (got.width <= 0 || got.height <= 0) break;
                width *= want2.width / got.width; height *= want2.height / got.height;
            }
            Rect sized = DeckLayout.ScreenRect(m_menu.m_background.gameObject, camera);
            Vector3 move = camera.ScreenToWorldPoint(atFrame + new Vector3(want2.center.x - sized.center.x, want2.center.y - sized.center.y, 0))
                - camera.ScreenToWorldPoint(atFrame);
            m_menu.m_background.transform.position += move; m_menu.m_border.transform.position += move;
        }
        Rect board = DeckLayout.ScreenRect(m_menu.m_background.gameObject, camera);
        // The header plate sits on the board's top edge, and native layout never places it: when the board grew upward over
        // the caption, lift the plate by the same amount, so it stays on the edge instead of over the caption.
        var plate = m_menu.m_header;
        float lift = board.height > 0 && frame.height > 0 ? board.yMax - frame.yMax : 0f;
        if (plate != null && Math.Abs(lift) > .5f)
        {
            Vector3 atPlate = camera.WorldToScreenPoint(plate.transform.position);
            plate.transform.position += camera.ScreenToWorldPoint(atPlate + Vector3.up * lift) - camera.ScreenToWorldPoint(atPlate);
        }
        Rect plateRect = plate != null ? DeckLayout.ScreenRect(plate.gameObject, camera) : Rect.zero;
        NativeDungeonRunUI.LogLayout("DUNGEON_RUN_UI layout caption=" + captionRect + " planks=" + plankSpan.yMax.ToString("F0") + ".." + plankSpan.yMin.ToString("F0")
            + " frame=" + frame + " board=" + board + " header=" + plateRect + " lift=" + lift.ToString("F0") + " lines=" + captionLines);
        Rect rect = DeckLayout.ScreenRect(m_menu.gameObject, camera);
        if (rect.width <= 0 || rect.height <= 0) return;
        float scale = Math.Min(Screen.width * .86f / rect.width, Screen.height * .86f / rect.height);
        m_menu.transform.localScale *= scale;
        rect = DeckLayout.ScreenRect(m_menu.gameObject, camera);
        var at = camera.WorldToScreenPoint(m_menu.transform.position);
        m_menu.transform.position += camera.ScreenToWorldPoint(new Vector3(Screen.width / 2f, Screen.height / 2f, at.z))
            - camera.ScreenToWorldPoint(new Vector3(rect.center.x, rect.center.y, at.z));
    }
}
