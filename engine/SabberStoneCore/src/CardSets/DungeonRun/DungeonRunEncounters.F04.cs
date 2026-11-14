using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.DungeonRun
{
	// Phase 8, family 04 (collectibles C: Uldum ULD_282..ULD_728). The family has no encounter-only card: its 29 cards are
	// collectible and scripted where the card set is (CardSets/Standard/UldumCardsGen.cs); the rules they share are below.
	public static partial class DungeonRunEncounters
	{
		private static void F04()
		{
		}
	}

	/// <summary>
	/// Phase 8 family 04: the rules the Uldum collectibles ULD_282..ULD_728 share. Three groups:
	/// the Uldum Quests, whose reward is a hero power (<see cref="QuestProgress"/>); cards drawn from the run's scripted
	/// generation pool with a filter of their own (<see cref="RandomScripted"/>); and "summon when drawn"
	/// (<see cref="ShuffleSummonWhenDrawn"/>, <see cref="SummonWhenDrawn"/>, <see cref="SummonDrawn"/>), which later families
	/// reuse (Sandshaper Rhogi's Sand Shapin', Surprise! Murlocs!).
	/// All state is on entities (the Quest's own progress tags, the shuffled card's script tag), so search clones replay it.
	/// </summary>
	public static class UldumRules
	{
		// ------------------------------------------------------------------ Quests with a hero power reward

		/// <summary>
		/// One step (or <paramref name="amount"/> steps) of an Uldum Quest: the trigger task of the Quest in its owner's Quest
		/// slot. At its total the owner's hero power is replaced by <paramref name="rewardPowerId"/> (a new, ready power: the old
		/// one's trigger and aura stop first, the new one's start) and the Quest goes to the graveyard.
		/// </summary>
		/// <param name="amount">The steps this event is worth (the Health a heal restored); null = 1. 0 or less = none.</param>
		public static ISimpleTask QuestProgress(string rewardPowerId, Func<Game, Controller, IEntity, IPlayable, int> amount = null)
		{
			Card reward = Cards.FromId(rewardPowerId);
			return new CustomTask((g, c, s, t, stack) => Advance(s as Spell, reward, amount == null ? 1 : amount(g, c, s, t)));
		}

		/// <summary>Advances <paramref name="quest"/> by <paramref name="amount"/>; see <see cref="QuestProgress"/>.</summary>
		public static void Advance(Spell quest, Card reward, int amount = 1)
		{
			if (quest == null || reward == null || amount <= 0) return;
			Controller c = quest.Controller;
			// Only the Quest in the slot counts: a completed one is in the graveyard, and its queued triggers do nothing.
			if (c.SecretZone.Quest != quest) return;
			int total = quest.QuestTotalProgress;
			if (total <= 0 || quest.QuestProgress >= total) return;
			quest.QuestProgress = (int)Math.Min(total, (long)quest.QuestProgress + amount);
			if (quest.QuestProgress < total) return;

			quest.ActivatedTrigger?.Remove();
			EncounterRules.DeactivatePower(c.Hero.HeroPower);
			new ReplaceHeroPower(reward).Process(c.Game, c, quest, null);
			HeroPower power = c.Hero.HeroPower;
			if (power.Power?.Trigger != null || power.Power?.Aura != null) EncounterRules.ActivatePower(power);
			quest[GameTag.REVEALED] = 1;
			c.SecretZone.Quest = null;
			c.GraveyardZone.Add(quest);
		}

		/// <summary>
		/// A card "from another class" for <paramref name="c"/>: a class card of none of its classes (its hero's, and for the
		/// run player every class of its run row). Neutral cards are of no class.
		/// </summary>
		public static bool IsOtherClass(Controller c, Card card)
		{
			if (c == null || card == null || card.Class == CardClass.NEUTRAL || card.Class == CardClass.INVALID) return false;
			if (Array.IndexOf(Cards.HeroClasses, card.Class) < 0) return false;
			return !RunHelpers.RunPlayerClasses(c).Contains(card.Class);
		}

		// ------------------------------------------------------------------ random scripted cards

		/// <summary>
		/// The scripted cards of <paramref name="type"/> the generation of <paramref name="source"/>'s controller may yield (the
		/// format's pool behind the run's generation filters, then Implemented only), narrowed by <paramref name="keep"/>.
		/// </summary>
		public static List<Card> ScriptedPool(IEntity source, CardType type, Func<Card, bool> keep = null)
		{
			var pool = new List<Card>();
			using (source.Game.GenerateFor(null))
				foreach (Card p in RandomCardTask.GetCardList(source, type))
					if (p.Implemented && (keep == null || keep(p))) pool.Add(p);
			return pool;
		}

		/// <summary>
		/// Puts one random scripted card of <paramref name="type"/> that passes <paramref name="keep"/> on the stack (for the
		/// task's controller); an empty stack when the pool is empty. <paramref name="keep"/> also gets the controller.
		/// </summary>
		public static ISimpleTask RandomScripted(CardType type, Func<Controller, Card, bool> keep) =>
			new CustomTask((g, c, s, t, stack) =>
			{
				List<Card> pool = ScriptedPool(s, type, keep == null ? (Func<Card, bool>)null : p => keep(c, p));
				if (pool.Count == 0) { stack.Playables = new List<IPlayable>(); return; }
				stack.Playables = new List<IPlayable> { Entity.FromCard(c, pool[g.Random.Next(pool.Count)]) };
				g.OnRandomHappened(true);
			});

		/// <summary>
		/// King Phaoris (ULD_304): for each spell in <paramref name="c"/>'s hand (hand order), summons a random scripted minion of
		/// that spell's Cost while the board has room. A Cost with no minion summons nothing.
		/// </summary>
		public static void SummonForSpellsInHand(Game g, Controller c, IEntity source)
		{
			int[] costs = c.HandZone.Where(p => p is Spell).Select(p => p.Cost).ToArray();
			if (costs.Length == 0) return;
			List<Card> minions = ScriptedPool(source, CardType.MINION);
			bool rolled = false;
			foreach (int cost in costs)
			{
				if (c.BoardZone.IsFull) break;
				List<Card> pool = minions.Where(p => p.Cost == cost).ToList();
				if (pool.Count == 0) continue;
				var minion = (Minion)Entity.FromCard(c, pool[g.Random.Next(pool.Count)]);
				rolled = true;
				Generic.SummonBlock.Invoke(g, minion, -1, source);
			}
			if (rolled) g.OnRandomHappened(true);
		}

		/// <summary>
		/// Body Wrapper (ULD_727): Discover among <paramref name="c"/>'s minions that died this game (the graveyard is public; one
		/// offer per card, up to three) and shuffle a fresh copy of the chosen card into its deck. Nothing when none died.
		/// </summary>
		public static void DiscoverDeadToDeck(Game g, Controller c, IEntity source, TaskStack stack)
		{
			Card[] dead = c.GraveyardZone.Where(p => p.ToBeDestroyed && p.Card.Type == CardType.MINION && !ShrineRules.IsCorpse(p))
				.Select(p => p.Card).Distinct().ToArray();
			if (dead.Length == 0) return;
			var ctx = new EffectSystem.EffectContext(g, c.Id, source.Id, stack: stack);
			ctx.Discover(ctx.SampleDistinct(dead, Math.Min(3, dead.Length)), ChoiceAction.STACK, ShuffleChosen);
		}

		private static readonly ISimpleTask ShuffleChosen = new CustomTask((g, c, s, t, stack) =>
		{
			IPlayable chosen = stack?.Playables?.FirstOrDefault();
			if (chosen != null) Generic.ShuffleIntoDeck(c, s, Entity.FromCard(c, chosen.Card));
		});

		// ------------------------------------------------------------------ summon when drawn

		/// <summary>
		/// Where a "summon when drawn" card keeps what it summons: the summoned card's dbfId in the card's first script tag
		/// (ULD_286t "Summon a {0}": PayloadRules never sends that tag for it).
		/// </summary>
		public const GameTag SummonedCardTag = GameTag.TAG_SCRIPT_DATA_NUM_1;

		/// <summary>
		/// Shuffles <paramref name="copies"/> cards <paramref name="shellId"/> into <paramref name="c"/>'s deck, each of which
		/// summons one <paramref name="summoned"/> when drawn (Shadow of Death: three Shadows of the chosen minion's card, a plain
		/// copy without its enchantments or damage). Returns how many were shuffled (a full deck takes none).
		/// </summary>
		public static int ShuffleSummonWhenDrawn(Controller c, IEntity source, string shellId, Card summoned, int copies)
		{
			if (c == null || summoned == null || summoned.Type != CardType.MINION) return 0;
			Card shell = Cards.FromId(shellId);
			int done = 0;
			for (int i = 0; i < copies; i++)
			{
				IPlayable card = Entity.FromCard(c, shell, new EntityData { [SummonedCardTag] = summoned.AssetId });
				if (Generic.ShuffleIntoDeck(c, source, card)) done++;
			}
			return done;
		}

		/// <summary>The minion card a "summon when drawn" card names, or null (a card that was never given one).</summary>
		public static Card SummonedCard(IEntity card)
		{
			int dbf = card?[SummonedCardTag] ?? 0;
			if (dbf <= 0) return null;
			Card named;
			try { named = Cards.FromAssetId(dbf); } catch { return null; }
			return named != null && named.Type == CardType.MINION ? named : null;
		}

		/// <summary>
		/// Summons the minion the task's source names (<see cref="SummonedCard"/>) for its controller: the cast of a "summon when
		/// drawn" card from hand. Nothing with a full board or no named card.
		/// </summary>
		public static ISimpleTask SummonNamed => new CustomTask((g, c, s, t, stack) =>
		{
			Card named = SummonedCard(s);
			if (named == null || c.BoardZone.IsFull) return;
			Generic.SummonBlock.Invoke(g, (Minion)Entity.FromCard(c, named), -1, s);
		});

		/// <summary>
		/// The Topdeck task of a "Casts When Drawn: summon X" card whose X is on the card (<see cref="SummonedCard"/>): summon
		/// it, then draw a card (the Casts When Drawn rule).
		/// </summary>
		public static ISimpleTask SummonWhenDrawn => ComplexTask.Create(SummonNamed, new DrawTask());

		/// <summary>
		/// "Summon it when drawn" for a minion card itself (Sand Shapin'): <paramref name="drawn"/>, just drawn into
		/// <paramref name="c"/>'s hand, is summoned from there while the board has room. True when it was summoned; the caller
		/// draws the replacement card if its rule has one.
		/// </summary>
		public static bool SummonDrawn(Controller c, IPlayable drawn)
		{
			if (c == null || !(drawn is Minion minion) || minion.Zone != c.HandZone || c.BoardZone.IsFull) return false;
			c.HandZone.Remove(minion);
			Generic.SummonBlock.Invoke(c.Game, minion, -1, c.Hero);
			return true;
		}
	}
}
