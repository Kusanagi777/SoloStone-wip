using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.Actions
{
	// Phase 8, family 02 (unscripted collectibles A): the engine rules its cards share with later encounter families.
	//   WrongTarget50  GVG_054 Ogre Warmaul ("50% chance to attack the wrong enemy"); family 05 DALA_861, family 14 DALA_BOSS_14t.
	//   HeroDamageCap  LOE_119 Animated Armor ("Your hero can only take 1 damage at a time"); family 18 DALA_BOSS_65p.
	//   SecretRepeats  DAL_573 Commander Rhyssa ("Your Secrets trigger twice").
	//   CapturesOpening TRL_318 Hex Lord Malacrass (a copy of the opening hand).
	// All of it reads live board state or one private card tag, so clones and AI simulations replay it.
	public static partial class EncounterRules
	{
		// The family's one private tag, HeroDamageCap (32637), is declared in the tag table of EncounterRules.cs.

		/// <summary>Commander Rhyssa: while an active one is on its owner's board, that player's Secrets trigger twice.</summary>
		public const string SecretsTwiceMinion = "DAL_573";
		/// <summary>Hex Lord Malacrass: its Battlecry reads the opening hand (TurnQueueRules.StartingHand).</summary>
		public const string OpeningHandReader = "TRL_318";

		// ------------------------------------------------------------------ damage cap

		/// <summary>
		/// The most damage <paramref name="c"/>'s hero can take from one hit, or 0 for no cap: the smallest
		/// <see cref="HeroDamageCap"/> among its active minions (not silenced, not dormant), its hero power and its hero.
		/// </summary>
		public static int HeroDamageCapOf(Controller c)
		{
			if (c?.Hero == null) return 0;
			int cap = 0;
			void Read(IEntity e)
			{
				int v = e[HeroDamageCap];
				if (v > 0 && (cap == 0 || v < cap)) cap = v;
			}
			foreach (Minion m in c.BoardZone)
				if (!m.IsSilenced && !m.IsDormant && !m.ToBeDestroyed) Read(m);
			if (c.Hero.HeroPower != null) Read(c.Hero.HeroPower);
			Read(c.Hero);
			return cap;
		}

		/// <summary>
		/// Character.TakeDamage, for a hit on a hero, after the source's own modifiers and before Armor, Divine Shield and the
		/// pre-damage triggers: the damage one hit may deal (<see cref="HeroDamageCapOf"/>). One board scan per hero hit.
		/// </summary>
		internal static int CapHeroDamage(Hero hero, int damage)
		{
			if (damage <= 1 || hero == null) return damage;
			int cap = HeroDamageCapOf(hero.Controller);
			return cap > 0 && damage > cap ? cap : damage;
		}

		// ------------------------------------------------------------------ 50% chance to attack the wrong enemy

		/// <summary>
		/// "50% chance to attack the wrong enemy", for <paramref name="attacker"/>'s attack that is being prepared (call it from
		/// an ATTACK trigger, where Misdirection changes the defender): with another enemy character to hit (the enemy hero or an
		/// enemy minion that is in play, alive and not dormant; Taunt and Stealth do not matter), a coin of the game RNG decides,
		/// and on a miss the defender becomes one of the others, uniformly. No other enemy: no roll and no random event.
		/// Returns whether the defender changed.
		/// </summary>
		public static bool WrongTarget50(ICharacter attacker)
		{
			Game g = attacker?.Game;
			if (g?.CurrentEventData == null || !(g.CurrentEventData.EventTarget is ICharacter intended)) return false;
			if (!ReferenceEquals(g.CurrentEventData.EventSource, attacker) || intended.Controller == attacker.Controller) return false;
			Controller enemy = attacker.Controller.Opponent;
			var others = new List<ICharacter>();
			foreach (Minion m in enemy.BoardZone)
				if (!ReferenceEquals(m, intended) && !m.Untouchable && !m.ToBeDestroyed && m.Health > 0) others.Add(m);
			if (!ReferenceEquals(enemy.Hero, intended) && !enemy.Hero.ToBeDestroyed) others.Add(enemy.Hero);
			if (others.Count == 0) return false;
			g.OnRandomHappened(true);
			if (g.Random.Next(2) == 0) return false;
			ICharacter pick = others[g.Random.Next(others.Count)];
			g.ProposedDefender = pick.Id;
			g.CurrentEventData.EventTarget = pick;
			return true;
		}

		/// <summary>The task of an ATTACK trigger whose event source is the attacker (<see cref="WrongTarget50"/>).</summary>
		public static readonly ISimpleTask WrongTargetTask = new CustomTask((g, c, s, t, stack) =>
		{
			if (t is ICharacter attacker) WrongTarget50(attacker);
		});

		// ------------------------------------------------------------------ Secrets trigger twice

		/// <summary>
		/// How many times a Secret of <paramref name="c"/> resolves its effect when it triggers (ComplexTask.Secret): 2 while an
		/// active Commander Rhyssa is on its board, else 1. Two of them still give 2 (the card says "twice").
		/// </summary>
		public static int SecretRepeats(Controller c)
		{
			if (c == null) return 1;
			foreach (Minion m in c.BoardZone)
				if (m.Card.Id == SecretsTwiceMinion && !m.IsSilenced && !m.IsDormant && !m.ToBeDestroyed) return 2;
			return 1;
		}

		// ------------------------------------------------------------------ opening hand

		/// <summary>
		/// Game.MainBegin (TurnQueueRules.CaptureOpening): the opening hands are recorded when a starting deck holds Hex Lord
		/// Malacrass or the game is a Dungeon Run match (a Discover or a draft can give one later). Every other game keeps its
		/// controller tags as before.
		/// </summary>
		internal static bool CapturesOpening(Game game) =>
			game.RunContextOrNull != null
			|| game.Player1.DeckCards.Concat(game.Player2.DeckCards).Any(card => card.Id == OpeningHandReader);
	}

	/// <summary>
	/// A Secret's trigger body (ComplexTask.Secret): the effect steps, then the reveal and the move to the graveyard, with the
	/// chain rule of StateTaskList (a step that does not complete ends the body: a Secret whose effect cannot happen stays in
	/// play, unrevealed). When the effect completed and its owner's Secrets trigger twice (EncounterRules.SecretRepeats:
	/// Commander Rhyssa), the effect steps run once more before the reveal.
	/// </summary>
	public sealed class SecretTriggerTask : ISimpleTask
	{
		private readonly ISimpleTask[] _effect;
		private readonly ISimpleTask _reveal = new SetGameTagTask(GameTag.REVEALED, 1, EntityType.SOURCE);
		private readonly ISimpleTask _retire = new MoveToGraveYard(EntityType.SOURCE);

		public SecretTriggerTask(ISimpleTask[] effect)
		{
			// The same refusal as StateTaskList.Chain: a Secret's body has no cursor to resume.
			StateTaskList.Chain(effect);
			_effect = effect;
		}

		public TaskState State { get; set; } = TaskState.READY;

		public bool IsTrigger { get; set; }

		public TaskState Process(in Game game, in Controller controller, in IEntity source, in IPlayable target, in TaskStack stack = null)
		{
			TaskStack current = stack ?? new TaskStack();
			for (int i = 0; i < _effect.Length; i++)
				if (_effect[i].Process(in game, in controller, in source, in target, in current) != TaskState.COMPLETE)
					return TaskState.COMPLETE;
			// The same stack: a Secret nested in a longer body (Misdirection) reads what the steps before it left there.
			for (int n = EncounterRules.SecretRepeats(controller); n > 1 && game.State != Enums.State.COMPLETE; n--)
				for (int i = 0; i < _effect.Length; i++)
					if (_effect[i].Process(in game, in controller, in source, in target, in current) != TaskState.COMPLETE)
						break;
			if (_reveal.Process(in game, in controller, in source, in target, in current) == TaskState.COMPLETE)
				_retire.Process(in game, in controller, in source, in target, in current);
			return TaskState.COMPLETE;
		}

		public override string ToString() => "[SecretTriggerTask:" + _effect.Length + "]";
	}
}
