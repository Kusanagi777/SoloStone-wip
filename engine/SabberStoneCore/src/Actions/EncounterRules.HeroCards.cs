using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.CardSets.DungeonRun;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.Actions
{
	// The run player's fight-only hero cards (phase 8: Hagatha's monster hunters). The hero and power change is the engine's own
	// hero card play (Generic.PlayHeroCore); this file is the cards' shared Battlecry.
	public static partial class EncounterRules
	{
		/// <summary>The match's hero-card option of <paramref name="heroCardId"/> (its 12 cards and its passives), or null.</summary>
		public static RunFightHero HeroCardOf(Game game, string heroCardId) =>
			Of(game)?.HeroCardOptions.FirstOrDefault(o => o.Hero?.Id == heroCardId);

		/// <summary>
		/// A monster hunter's hero card Battlecry: "Deal <paramref name="damage"/> damage to all minions and choose a Passive
		/// Treasure." The hunter's own cards are shuffled into the deck; the passive is chosen first (a Discover over the card's
		/// passives, <see cref="RunFightHero.Passives"/>), then the damage is dealt. The chosen passive is a player enchantment
		/// for this match only (never a run treasure). With no passive on offer the damage is dealt at once.
		/// </summary>
		public static void PlayHunterCard(Game game, Controller c, IPlayable source, int damage, TaskStack stack)
		{
			RunFightHero option = HeroCardOf(game, source.Card.Id);
			if (option != null)
				foreach (Card card in option.Deck) Generic.ShuffleIntoDeck(c, source, Entity.FromCard(c, card));
			ISimpleTask aoe = new DamageTask(damage, EntityType.ALLMINIONS);
			Card[] passives = (option?.Passives ?? new string[0]).Select(Cards.FromId).ToArray();
			if (passives.Length > 0)
			{
				var attach = new CustomTask((g, owner, s, t, st) =>
				{
					IPlayable chosen = st?.Playables?.FirstOrDefault();
					if (chosen == null) return;
					AttachPassive(owner, chosen.Card.Id);
					// The passive is in force before the damage is dealt (an aura such as Entrenchment's +2 Health reaches the board now).
					g.AuraUpdate();
				});
				var ctx = new EffectContext(game, c.Id, source.Id, stack: stack);
				if (ctx.Discover(passives, ChoiceAction.STACK, ComplexTask.Create(attach, aoe))) return;
			}
			game.TaskQueue.Enqueue(aoe, c, source, null);
		}

		/// <summary>
		/// Attaches passive treasure <paramref name="key"/> (a DungeonRunContent passive treasure) to <paramref name="c"/> for the
		/// rest of this match: its one-off start change and its player effect, as DungeonRunSetup.ApplyAfterStart does for a run
		/// treasure. The run's own treasure list is never touched.
		/// </summary>
		public static void AttachPassive(Controller c, string key)
		{
			if (!DungeonRunContent.TryGet(key, out RunContent content) || content.Kind != RunContentKind.PassiveTreasure) return;
			content.OnMatchStart?.Invoke(c);
			if (content.EffectKey != null) DungeonRunSetup.AttachEffect(c, c.Hero, content.EffectKey);
		}
	}
}
