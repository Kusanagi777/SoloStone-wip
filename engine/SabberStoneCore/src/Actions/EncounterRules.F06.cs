using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Auras;
using SabberStoneCore.CardSets.DungeonRun;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Actions
{
	// Phase 8, family 06 (the Tombs of Terror anomalies and plague twists): the engine rules its rule cards share with later
	// encounter families, each written once.
	//   ForcedAttack / ForcedAttackRandomEnemy   ULDA_802 Plague of Madness ("their right-most minion attacks a random enemy");
	//                                            the forced-attack bosses of families 11, 20, 22 and 24.
	//   HeroImmuneWhile / ControlsMinion         ULDA_706 No-Face ("Heroes are Immune while they control minions"); Glinda
	//                                            Crowskin (family 23) and Vesh's first phase (family 24).
	//   AttackEqualsHealth                       ULDA_709 Angelic Presence ("Minions' Attack is ALWAYS equal to their Health").
	//   HeroDamageTaken                          ULDA_714 In the Face ("Heroes take double damage"): the mark is a controller tag.
	//   MixDecks                                 ULDA_721 Aleatory ("shuffle both decks together and give half to each player").
	// All of it reads live board state and entity tags, so clones and AI simulations replay it. The family's private tags
	// (HeroDamageDouble 32641, RuleTombSummons 32680) are declared in the tag table of EncounterRules.cs.
	public static partial class EncounterRules
	{
		// ------------------------------------------------------------------ forced attacks

		// A character a forced attack may involve: in play, alive, and not a permanent or a dormant minion.
		private static bool CanFight(ICharacter x) =>
			x is Minion m ? m.Zone?.Type == Zone.PLAY && !m.Untouchable && !m.ToBeDestroyed && m.Health > 0
				: x is Hero h && ReferenceEquals(h.Controller.Hero, h) && !h.ToBeDestroyed && h.Health > 0;

		/// <summary>
		/// <paramref name="attacker"/> attacks <paramref name="defender"/> now, whatever its state (Mass Hysteria's rule: 0 Attack,
		/// Frozen, exhausted, "Can't attack" and Taunt do not matter; the attack's own triggers and its death phase run; no
		/// attack option of the turn is spent). Nothing when either is not in play, is dead, is a permanent or a dormant minion,
		/// or both are on one side. Returns whether the attack was made.
		/// </summary>
		public static bool ForcedAttack(ICharacter attacker, ICharacter defender)
		{
			Game g = attacker?.Game;
			if (g == null || defender == null || g.State == State.COMPLETE || attacker.Controller == defender.Controller
				|| !CanFight(attacker) || !CanFight(defender)) return false;
			var before = g.CurrentEventData;
			Generic.AttackBlock.Invoke(attacker.Controller, attacker, defender, true, false);
			attacker.Controller.NumOptionsPlayedThisTurn--;
			g.CurrentEventData = before;
			return true;
		}

		/// <summary>
		/// <paramref name="attacker"/> attacks a random enemy (the enemy hero or an enemy minion that is in play, alive and not
		/// dormant: uniform, game RNG, reported as a random event when there is a choice; Taunt, Stealth and Immune do not
		/// matter, an Immune defender just takes no damage). Returns the defender, or null when no attack was made.
		/// </summary>
		public static ICharacter ForcedAttackRandomEnemy(ICharacter attacker)
		{
			Game g = attacker?.Game;
			if (g == null || g.State == State.COMPLETE || !CanFight(attacker)) return null;
			Controller enemy = attacker.Controller.Opponent;
			var targets = new List<ICharacter>();
			if (CanFight(enemy.Hero)) targets.Add(enemy.Hero);
			foreach (Minion m in enemy.BoardZone.OrderBy(m => m.ZonePosition))
				if (CanFight(m)) targets.Add(m);
			if (targets.Count == 0) return null;
			if (targets.Count > 1) g.OnRandomHappened(true);
			ICharacter defender = targets[targets.Count == 1 ? 0 : g.Random.Next(targets.Count)];
			return ForcedAttack(attacker, defender) ? defender : null;
		}

		/// <summary>
		/// The right-most minion on <paramref name="c"/>'s board that can fight (a permanent or a dormant minion at the right end
		/// is passed over), or null.
		/// </summary>
		public static Minion RightmostMinion(Controller c) =>
			c.BoardZone.Where(m => CanFight(m)).OrderBy(m => m.ZonePosition).LastOrDefault();

		// ------------------------------------------------------------------ a hero that is Immune while it controls minions

		/// <summary>
		/// <paramref name="c"/> controls a minion that counts: on its board, alive, and not a permanent or a dormant minion (an
		/// untouchable card: a Cannon, a Cart, a Violet Prison, an Eternal Tomb, a dormant shrine form); with
		/// <paramref name="counts"/>, one that passes it too (Vesh: a Murloc).
		/// </summary>
		public static bool ControlsMinion(Controller c, Func<Minion, bool> counts = null)
		{
			foreach (Minion m in c.BoardZone)
				if (!m.Untouchable && !m.ToBeDestroyed && m.Health > 0 && (counts == null || counts(m))) return true;
			return false;
		}

		/// <summary>
		/// "Your hero is Immune while you control minions": a hero aura for its owner's controller, checked again at every aura
		/// update (Violet Illusionist's aura with <see cref="ControlsMinion"/> as its condition), so the hero loses it in the
		/// aura update that follows the death of its last minion and regains it with the next one. A replaced hero (a hero card,
		/// a boss form) is covered from the next update. <paramref name="counts"/> narrows the minions that count.
		/// </summary>
		public static Aura HeroImmuneWhile(Func<Minion, bool> counts = null) =>
			new Aura(AuraType.HERO, Effects.Immune) { Restless = true, Condition = new SelfCondition(p => ControlsMinion(p.Controller, counts)) };

		// ------------------------------------------------------------------ heroes take double damage

		/// <summary>
		/// Character.TakeDamage, for a hit on a hero, after the source's own modifiers and before Animated Armor's cap, Armor,
		/// Divine Shield and the pre-damage triggers: a hero whose controller carries <see cref="HeroDamageDouble"/> takes twice
		/// the damage (Armor absorbs the doubled amount). One controller-tag read per hero hit.
		/// </summary>
		internal static int HeroDamageTaken(Hero hero, int damage) =>
			damage > 0 && hero != null && hero.Controller[HeroDamageDouble] != 0 ? DamageArithmetic.Add(damage, damage) : damage;

		// ------------------------------------------------------------------ Attack is always equal to Health

		/// <summary>
		/// "Minions' Attack is ALWAYS equal to their Health", for the minions on its owner's controller's board (a global rule
		/// attaches one copy to each player): Lightspawn's rule for every one of them, recomputed at every aura update. A
		/// permanent or a dormant minion is left alone; a minion that leaves the board loses it.
		/// </summary>
		public static IAura AttackEqualsHealth() => new AttackEqualsHealthAura();

		// Lightspawn's adaptive effect (Auras.AdaptiveEffect: the minion's own Attack is zeroed and one SET aura effect carries
		// its Health) for a whole board, owned by one entity: the value this aura holds on each minion is kept per minion id, as
		// AdaptiveEffect keeps its one, and copied when the game is cloned.
		private sealed class AttackEqualsHealthAura : IAura
		{
			private readonly IPlayable _owner;
			private readonly Dictionary<int, int> _held;
			private bool _on = true;

			internal AttackEqualsHealthAura() { }

			private AttackEqualsHealthAura(AttackEqualsHealthAura prototype, IPlayable owner)
			{
				_owner = owner;
				_held = prototype._held == null ? new Dictionary<int, int>() : new Dictionary<int, int>(prototype._held);
				_on = prototype._on;
			}

			IPlayable IAura.Owner => _owner;

			public void Activate(IPlayable owner)
			{
				var instance = new AttackEqualsHealthAura(this, owner);
				owner.Game.Auras.Add(instance);
				owner.OngoingEffect = instance;
			}

			public void Clone(IPlayable clone) => Activate(clone);

			public void Remove()
			{
				_owner.OngoingEffect = null;
				_on = false;
			}

			public void Update()
			{
				Game g = _owner.Game;
				Controller c = _owner.Controller;
				int source = _owner.Id;
				// A minion that left this board (or every one, once the aura is off) loses the effect it holds.
				if (_held.Count > 0)
					foreach (int id in _held.Keys.OrderBy(id => id).ToArray())
					{
						g.IdEntityDic.TryGetValue(id, out IPlayable entity);
						if (_on && entity is Minion stays && stays.Zone == c.BoardZone && !stays.Untouchable) continue;
						if (entity?.AuraEffects != null) new Effect(GameTag.ATK, EffectOperator.SET, _held[id]).RemoveAuraFrom(entity, source);
						_held.Remove(id);
					}
				if (!_on)
				{
					g.Auras.Remove(this);
					return;
				}
				foreach (Minion m in c.BoardZone)
				{
					if (m.Untouchable) continue;
					if (m.AuraEffects == null) m.AuraEffects = new AuraEffects(CardType.MINION);
					m._modifiedATK = 0;
					if (_held.TryGetValue(m.Id, out int last)) new Effect(GameTag.ATK, EffectOperator.SET, last).RemoveAuraFrom(m, source);
					int value = m.Health - m.AuraEffects.ATK + m.AuraEffects.SetValue(GameTag.ATK);
					new Effect(GameTag.ATK, EffectOperator.SET, value).ApplyAuraTo(m, source);
					_held[m.Id] = value;
				}
			}

			public override string ToString() => "[AttackEqualsHealth:" + _owner + "]";
		}

		// ------------------------------------------------------------------ both decks shuffled together, half to each player

		/// <summary>
		/// "Shuffle both decks together and give half to each player": every card left in the two decks is dealt again, the
		/// first player getting the larger half of an odd total. The card entities themselves change sides (their enchantments
		/// with them), so nothing a card carries is lost. A card stays with its owner when the game's generation filter does not
		/// let the other side be given it (Game.BlocksGenerated for that receiver: a run match's lists hold what a boss may never
		/// be handed, every Secret among it); such cards count toward their owner's half. Both decks are then shuffled. Neither player learns which cards it got: each deck's exits and entries are
		/// ordinary hidden residence changes (the owner's own-deck ledger loses unknown cards and gains unknown ones), and what
		/// either side believed about the other's deck no longer holds (RunHelpers.BeliefReset). Game RNG; reported as a random
		/// event. Returns how many cards changed sides.
		/// </summary>
		public static int MixDecks(Game g)
		{
			if (g == null || g.State == State.COMPLETE) return 0;
			Controller first = g.FirstPlayer, second = first.Opponent;
			bool MayCross(IPlayable card)
			{
				Controller to = card.Controller.Opponent;
				if (to.DeckZone.IsFull) return false;
				using (g.GenerateFor(to)) return !g.BlocksGenerated(card.Card);
			}
			var free = new List<IPlayable>();
			int kept1 = 0, kept2 = 0;
			foreach (Controller c in new[] { first, second })
				foreach (IPlayable card in c.DeckZone.ToArray())
				{
					if (MayCross(card)) free.Add(card);
					else if (c == first) kept1++;
					else kept2++;
				}
			int total = free.Count + kept1 + kept2;
			if (free.Count == 0) return 0;
			g.OnRandomHappened(true);
			for (int i = free.Count - 1; i > 0; i--)
			{
				int j = g.Random.Next(i + 1);
				IPlayable swap = free[i]; free[i] = free[j]; free[j] = swap;
			}
			int toFirst = Math.Max(0, Math.Min(free.Count, (total + 1) / 2 - kept1));
			int moved = 0;
			for (int i = 0; i < free.Count; i++)
			{
				IPlayable card = free[i];
				Controller to = i < toFirst ? first : second;
				if (card.Controller == to) continue;
				Controller from = card.Controller;
				from.DeckZone.Remove(card);
				card.Controller = to;
				card[GameTag.CONTROLLER] = to.PlayerId;
				if (card.AppliedEnchantments != null)
					foreach (Enchantment effect in card.AppliedEnchantments) { effect.Controller = to; effect[GameTag.CONTROLLER] = to.PlayerId; }
				to.DeckZone.Add(card);
				moved++;
			}
			first.DeckZone.Shuffle();
			second.DeckZone.Shuffle();
			RunHelpers.BeliefReset(first, RunBeliefZones.Deck);
			RunHelpers.BeliefReset(second, RunBeliefZones.Deck);
			return moved;
		}
	}
}
