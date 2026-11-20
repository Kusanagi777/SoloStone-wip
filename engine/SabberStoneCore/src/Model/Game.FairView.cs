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

        /// <summary>
        /// Drops every aura's applied id whose entity is no longer registered: a fair view that retires hidden entities
        /// (Kalecgos's DAL_609 on the opponent's hand) must call this before it reissues their ids, or an aura removal looks up a
        /// retired id (KeyNotFoundException) or strips its effect from the entity reissued under it. Returns how many were dropped.
        /// </summary>
        public int RemoveDetachedAuraTargetsForFairView()
        {
            int removed = 0;
            foreach (SabberStoneCore.Auras.IAura aura in Auras)
                removed += ForgetUnregisteredTargets(aura);
            return removed;
        }

        private static int ForgetUnregisteredTargets(SabberStoneCore.Auras.IAura aura)
        {
            if (aura is SabberStoneCore.Auras.Aura single) return single.ForgetUnregisteredTargets();
            int removed = 0;
            // MultiAura's children are not registered in Auras.
            if (aura is SabberStoneCore.Auras.MultiAura multi)
                foreach (SabberStoneCore.Auras.IAura child in multi)
                    removed += ForgetUnregisteredTargets(child);
            return removed;
        }
    }
}
