using System;
using System.Collections.Generic;
using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.src.Loader;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.DungeonRun
{
	/// <summary>
	/// Phase 7 S3: the Rogue, Shaman and Warlock board shrines (Bottled Terror, Treasure from Below, Pirate's Mark; Krag'wa's Lure,
	/// Tribute from the Tides, Krag'wa's Grace; Blood Pact, Dark Reliquary, Hir'eek's Hunger) and their client enchantment
	/// records. Each script sits on the client card itself; the shrine model (placement, dormancy, revival, guards) is
	/// <see cref="ShrineRules"/>. Every random choice uses the game RNG; every piece of state is an entity tag.
	/// </summary>
	public static partial class DungeonRunShrines
	{
		/// <summary>TRLA_179 Hir'eek's Hunger.</summary>
		public const string Hunger = "TRLA_179";
		/// <summary>On the game while a hit Hir'eek's Hunger redirected resolves: that hit is never redirected again.</summary>
		public const GameTag HungerResolving = (GameTag)32624;
		/// <summary>TRLA_186 Treasure from Below's discount on the stolen card: the client's LOOT_204e "Costs (2) less."</summary>
		internal const string TreasureDiscount = "LOOT_204e";

		private static void AddS3(Dictionary<string, CardDef> cards)
		{
			// ---------------- Rogue
			// TRLA_106 Bottled Terror: "Your other minions have Stealth. Deathrattle: Give your minions +2 Attack." The Stealth is
			// an aura Stealth (Character.HasStealth: the attack-time clear and Silence leave it while the shrine lives), on the
			// other minions only. Its Deathrattle fires when it dies (it goes dormant: ShrineRules), on the minions still in play
			// (the dormant form is untouchable, so not among them).
			Shrine(cards, "TRLA_106", new Power
			{
				Aura = new Aura(AuraType.BOARD_EXCEPT_SOURCE, new Effect(GameTag.STEALTH, EffectOperator.ADD, 1)),
				DeathrattleTask = new AddEnchantmentTask("TRLA_106e", EntityType.MINIONS),
			});
			cards.Add("TRLA_106e", Implemented(new CardDef(new Power { Enchant = new Enchant(Effects.Attack_N(2)) })));

			// TRLA_186 Treasure from Below: "At the start of your turn, steal a card from your opponent's deck. It costs (2) less."
			// A random card of the opponent's deck (game RNG) moves to the owner's hand (a full hand burns it by the ordinary hand
			// rule) and gets the client's "Costs (2) less." (LOOT_204e). The card stays hidden from its old owner: the history
			// records a deck exit for it and a transfer to the owner's hand, whose definition only the new owner sees.
			Shrine(cards, "TRLA_186", new Power
			{
				Trigger = new Trigger(TriggerType.TURN_START)
				{
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (s is Minion shrine && Active(shrine)) Steal(g, c, shrine);
					}),
				},
			});

			// TRLA_187 Pirate's Mark: "After you cast a spell, cast it again (on the same target)." A copy of the spell, cast by
			// RecastRules (Lynessa's cast, the target locked: a target the copy may no longer have, dead or no longer meeting the
			// spell's requirement, loses only the targeted part), with the Choose One option the owner picked. A recast never
			// triggers another recast (RecastRules.IsRecast), so two Marks (TRLA_804) cast the spell three times, not for ever.
			Shrine(cards, "TRLA_187", new Power
			{
				Trigger = new Trigger(TriggerType.AFTER_CAST)
				{
					TriggerSource = TriggerSource.FRIENDLY,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (!(t is Spell spell) || RecastRules.IsRecast(spell) || !(s is Minion shrine) || !Active(shrine)
							|| g.State == State.COMPLETE) return;
						ICharacter target = spell.CardTarget != 0 && g.IdEntityDic.TryGetValue(spell.CardTarget, out var e) ? e as ICharacter : null;
						RecastRules.Recast(spell, target, RecastRules.ChosenOption(spell));
					}),
				},
			});

			// ---------------- Shaman
			// TRLA_109 Krag'wa's Lure: "After you Overload, give your minions that much Attack." The Overload event carries the
			// amount (Generic.OverloadBlock); each friendly minion in play, the shrine included, gets TRLA_109e "Overcharged" with
			// that much Attack.
			Shrine(cards, "TRLA_109", new Power
			{
				Trigger = new Trigger(TriggerType.OVERLOAD)
				{
					TriggerSource = TriggerSource.FRIENDLY,
					FastExecution = true,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						int amount = g.CurrentEventData?.EventNumber ?? 0;
						if (amount <= 0 || !(s is Minion shrine) || !Active(shrine)) return;
						var lure = Cards.FromId("TRLA_109e");
						foreach (var m in c.BoardZone.GetAll()) Generic.AddEnchantmentBlock(g, lure, shrine, m, amount);
					}),
				},
			});
			cards.Add("TRLA_109e", Implemented(new CardDef(new Power { Enchant = new Enchant(Effects.Attack_N(0)) { UseScriptTag = true } })));

			// TRLA_154 Tribute from the Tides: "Your Battlecries trigger two additional times." (EXTRA_BATTLECRIES_ADDITIONAL +2,
			// read by BattlecryRules beside the doubling of Brann Bronzebeard and Battle Totem).
			Shrine(cards, "TRLA_154", new Power
			{
				Aura = new Aura(AuraType.CONTROLLER, new Effect(GameTag.EXTRA_BATTLECRIES_ADDITIONAL, EffectOperator.ADD, 2)),
			});

			// TRLA_155 Krag'wa's Grace: "Your spells cost (2) less." (a hand aura; costs floor at (0)).
			Shrine(cards, "TRLA_155", new Power
			{
				Aura = new Aura(AuraType.HAND, Effects.ReduceCost(2)) { Condition = SelfCondition.IsSpell },
			});

			// ---------------- Warlock
			// TRLA_113 Blood Pact: "Your spells cost Health instead of Mana." (the SPELLS_COST_HEALTH controller aura of DALA_748
			// Elixir of Vile; a count, so two sources stack and one leaving keeps the other). Paying Health is not damage.
			Shrine(cards, "TRLA_113", new Power
			{
				Aura = new Aura(AuraType.CONTROLLER, new Effect(GameTag.SPELLS_COST_HEALTH, EffectOperator.ADD, 1)),
			});
			// The client's player-enchantment record of the Pact: no effect of its own, the shrine's aura carries it.
			cards.Add("TRLA_113e", Implemented(new CardDef(new Power())));

			// TRLA_178 Dark Reliquary: "Whenever you discard a card, summon a random Demon." One random Demon minion per discard
			// (game RNG), from the game's generation pool, so the run's generation filter applies (RunMatchPlan.ExcludedGenerated:
			// no unscripted card, no teammate, no shrine); nothing on a full board.
			Shrine(cards, "TRLA_178", new Power
			{
				Trigger = new Trigger(TriggerType.DISCARD)
				{
					TriggerSource = TriggerSource.FRIENDLY,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (!(s is Minion shrine) || !Active(shrine) || c.BoardZone.IsFull || g.State == State.COMPLETE) return;
						var pool = RandomCardTask.GetCardList(shrine, CardType.MINION, race: Race.DEMON);
						if (pool.Count == 0) return;
						var demon = (Minion)Entity.FromCard(c, pool[g.Random.Next(pool.Count)]);
						g.OnRandomHappened(true);
						Generic.SummonBlock(g, demon, -1, shrine);
					}),
				},
			});

			// TRLA_179 Hir'eek's Hunger: "Whenever your hero takes damage on your turn, the enemy hero takes it instead." A
			// replacement in Character.TakeDamage (HungerRedirect), before the hero's own prevention: the hit, its source and its
			// amount as dealt (spell damage already applied) go to the enemy hero; the owner's Armor is untouched. Paying Health
			// (TRLA_113 Blood Pact, DALA_748) is not damage and is not redirected; Life Tap's "take 2 damage" is damage and is.
			Shrine(cards, Hunger, new Power());
		}

		/// <summary>
		/// The enemy hero a hit on <paramref name="hero"/> goes to instead (TRLA_179 Hir'eek's Hunger): it is the hero's owner's
		/// turn and an awake, unsilenced Hunger (or a plain copy, which keeps the text) is on the owner's board; else null. A hit
		/// the Hunger already redirected is never redirected again.
		/// </summary>
		internal static Hero HungerRedirect(Hero hero)
		{
			var c = hero.Controller;
			var game = c?.Game;
			if (game == null || game.CurrentPlayer != c || hero != c.Hero || game.State == State.COMPLETE) return null;
			if (game.NativeTags.TryGetValue(HungerResolving, out int busy) && busy != 0) return null;
			var board = c.BoardZone;
			for (int i = 0; i < board.Count; i++)
			{
				var m = board[i];
				if (m.Card.Id == Hunger && !m.IsSilenced && Active(m)) return c.Opponent.Hero;
			}
			return null;
		}

		/// <summary>Deals a redirected hit (outgoing modifiers already applied) to <paramref name="enemy"/>.</summary>
		internal static int Redirect(Hero enemy, IPlayable source, int damage)
		{
			var game = enemy.Game;
			game.NativeTags[HungerResolving] = 1;
			try { return enemy.TakeDamage(source, damage, combat: false, outgoingApplied: true); }
			finally { game.NativeTags.Remove(HungerResolving); }
		}

		/// <summary>
		/// TRLA_186 Treasure from Below's steal: a random card of the opponent's deck goes to <paramref name="c"/>'s hand and
		/// costs (2) less (a full hand burns it). Returns the stolen card, or null on an empty deck.
		/// </summary>
		internal static IPlayable Steal(Game g, Controller c, Minion shrine)
		{
			var op = c.Opponent;
			if (op.DeckZone.IsEmpty || g.State == State.COMPLETE) return null;
			var stolen = op.DeckZone[g.Random.Next(op.DeckZone.Count)];
			g.OnRandomHappened(true);
			if (TakeFromDeck(g, c, stolen, shrine)) Generic.AddEnchantmentBlock(g, Cards.FromId(TreasureDiscount), shrine, stolen);
			return stolen;
		}
	}
}
