using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

/// <summary>
/// Bob's tavern page of an act run (phase 5): Bob's board, your board (with each card's changed stats), the gold, and each
/// offer as a row ("name - cost gold: what it does"), then "Leave tavern". While a purchase waits for a target the rows are
/// its choices plus "Back", which cancels it without spending gold. Every action goes through the run screen's node action
/// (DungeonRunRuntime.Mutate), so a refused or failed change rolls the run back and the page shows the reason.
/// </summary>
internal static class NativeDungeonRunTavernUI
{
    internal static void Compose(NativeDungeonRunUI.RunPage result, DungeonRun rules, DungeonRunState state,
        Action<Action<DungeonRunState>> nodeAction, Func<string, string> cardName, Action close)
    {
        result.Header = NativeDungeonRunText.TavernHeader;
        var act = rules.CurrentAct(state);
        var tavern = state.Tavern;
        result.Lines.Add(NativeDungeonRunText.TavernLine(act?.Id ?? "", tavern?.Gold ?? 0));
        int skipped = rules.SkippedDraftFloor(state);
        if (skipped > 0) result.Lines.Add(NativeDungeonRunText.SkippedDraftLine(skipped));
        var table = rules.Data.Tavern;
        if (tavern == null || table == null)
        {
            result.Lines.Add(NativeDungeonRunText.TavernClosedCaption);
            result.Row(NativeDungeonRunText.LeaveTavernId, NativeDungeonRunText.LeaveTavernLabel, () => nodeAction(rules.LeaveTavern));
            result.Row(NativeDungeonRunText.CloseId, NativeDungeonRunText.CloseLabel, close);
            return;
        }
        string Card(string candidate)
        {
            if (int.TryParse(candidate, NumberStyles.Integer, CultureInfo.InvariantCulture, out int uid))
            {
                var record = DungeonRunDeck.Find(state, uid);
                return record == null ? candidate : DungeonRunDeck.Describe(record, cardName);
            }
            return cardName(candidate);
        }
        result.Lines.Add(NativeDungeonRunText.BobBoardLine(tavern.Bob.Select(cardName)));
        result.Lines.Add(NativeDungeonRunText.FriendlyBoardLine(tavern.Friendly.Where(uid => DungeonRunDeck.Find(state, uid) != null)
            .Select(uid => DungeonRunDeck.Describe(DungeonRunDeck.Find(state, uid), cardName))));
        if (tavern.Bought.Count > 0) result.Lines.Add(NativeDungeonRunText.TavernBoughtLine(tavern.Bought.Select(b => cardName(b.Option))));

        if (tavern.Pending != null)
        {
            result.Lines.Add(NativeDungeonRunText.TavernPendingCaption(cardName(tavern.Pending.Option)));
            int n = 0;
            foreach (string candidate in tavern.Pending.Candidates)
            {
                string captured = candidate;
                result.Row(NativeDungeonRunText.TavernChooseIdPrefix + candidate + "#" + n++, Card(candidate), () => nodeAction(s => rules.ChooseTavern(s, captured)));
            }
            result.Row(NativeDungeonRunText.TavernBackId, NativeDungeonRunText.BackLabel, () => nodeAction(rules.BackTavern));
            return;
        }

        foreach (string id in tavern.Offers)
        {
            var option = table.Option(id);
            if (option == null) continue;
            string captured = id;
            bool bought = tavern.Bought.Any(b => b.Option == id);
            string text = OptionText(id);
            result.Lines.Add(NativeDungeonRunText.TavernOfferLine(cardName(id), option.Cost, text));
            // An offer an earlier purchase left with nothing to do (a full deck, an emptied board) is shown disabled.
            result.Row(NativeDungeonRunText.TavernBuyIdPrefix + id, cardName(id), () => nodeAction(s => rules.BuyTavern(s, captured)),
                enabled: !bought && option.Cost <= tavern.Gold && DungeonRunTavernNode.Applicable(rules, state, option));
        }
        result.Row(NativeDungeonRunText.LeaveTavernId, NativeDungeonRunText.LeaveTavernLabel, () => nodeAction(rules.LeaveTavern));
        result.Row(NativeDungeonRunText.CloseId, NativeDungeonRunText.CloseLabel, close);
    }

    // The tavern card's own text, plain (markup and line-break hints removed).
    private static string OptionText(string id)
    {
        try
        {
            string text = SabberStoneCore.Model.Cards.FromId(id)?.Text ?? "";
            text = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]*>", "").Replace("[x]", "");
            return System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();
        }
        catch (Exception) { return ""; }
    }
}
