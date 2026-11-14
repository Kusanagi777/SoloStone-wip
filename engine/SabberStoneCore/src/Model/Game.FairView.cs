namespace SabberStoneCore.Model
{
    public partial class Game
    {
        public void RedactSetupDecksForFairView()
        {
            _gameConfig.Player1Deck = null;
            _gameConfig.Player2Deck = null;
        }

        /// <summary>
        /// Removes every sequence trigger whose owner is no longer the entity registered under its id: a fair view that drops
        /// a hidden entity (a Shifting Scroll in the opponent's hand, LOOT_104 and its LOOT_104e) must not keep its triggers,
        /// which would fire for an entity the view does not hold and fail the next clone. Returns how many were removed.
        /// </summary>
        public int RemoveDetachedTriggersForFairView()
        {
            int removed = 0;
            foreach (SabberStoneCore.Triggers.Trigger trigger in Triggers.ToArray())
            {
                if (trigger.HasLiveSource(this)) continue;
                try { trigger.Remove(); }
                catch (System.Exception) { }
                Triggers.Remove(trigger);
                removed++;
            }
            return removed;
        }
    }
}
