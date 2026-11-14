using System;
using System.Collections.Generic;
using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
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
	/// Phase 7 S2: the Mage, Paladin and Priest board shrines (Jan'alai, Shirvallah, Bwonsamdi), their client enchantment
	/// records and Jan'alai's Progeny's Frostfire. Each script sits on the client card itself; the shrine model (placement,
	/// dormancy, revival, guards) is <see cref="ShrineRules"/>. Every random choice uses the game RNG; every piece of state is
	/// an entity tag.
	/// </summary>
	public static partial class DungeonRunShrines
	{
		/// <summary>TRLA_129s Frostfire, the card TRLA_129 Jan'alai's Progeny puts into its owner's hand.</summary>
		public const string Frostfire = "TRLA_129s";

		/// <summary>The cards a scripted shrine makes during a game (the ledger reaches them through the shrine).</summary>
		public static readonly IReadOnlyDictionary<string, string[]> ShrineCards = new Dictionary<string, string[]>(StringComparer.Ordinal)
		{
			["TRLA_129"] = new[] { Frostfire },
		};

		private static void AddS2(Dictionary<string, CardDef> cards)
		{
			// ---------------- Mage
			// TRLA_108 Jan'alai's Mantle: "Has Spell Damage +1 for each spell in your hand." Its own SPELLPOWER tag follows the
			// count (an aura on the shrine itself, so Silence ends it and a revival starts it again).
			Shrine(cards, "TRLA_108", new Power
			{
				Aura = new OwnTagAura(GameTag.SPELLPOWER, p => SpellsInHand(p.Controller)),
			});

			// TRLA_128 Jan'alai's Flame: "Your Hero Power costs (0). Whenever it kills a minion, refresh it." The cost is an aura on
			// the owner's hero powers; the refresh answers a hit from the owner's hero power that leaves a minion mortally wounded.
			Shrine(cards, "TRLA_128", new Power
			{
				Aura = new Aura(AuraType.HEROPOWER, Effects.SetCost(0)),
				Trigger = new MultiTrigger(
					// A hit from the owner's hero power leaves a minion mortally wounded: mark this use (the use count it will have).
					new Trigger(TriggerType.DEAL_DAMAGE)
					{
						TriggerSource = TriggerSource.HERO_POWER,
						SingleTask = new CustomTask((g, c, s, t, stack) =>
						{
							if (!(t is HeroPower power) || power.Controller != c || !(s is Minion shrine) || !Active(shrine)) return;
							if (!(g.CurrentEventData?.EventTarget is Minion victim) || !(victim.ToBeDestroyed || victim.Health <= 0)) return;
							power.NativeTags[FlameKill] = c.NumTimesHeroPowerUsedThisGame + 1;
						}),
					},
					// After that use is spent (HeroPower.cs: exhausted, then the Inspire event), refresh it. A stale mark (the
					// shrine died mid-use) never matches a later use.
					new Trigger(TriggerType.INSPIRE)
					{
						TriggerSource = TriggerSource.HERO_POWER,
						SingleTask = new CustomTask((g, c, s, t, stack) =>
						{
							if (!(t is HeroPower power) || power.Controller != c || !(s is Minion shrine) || !Active(shrine)) return;
							if (!power.NativeTags.TryGetValue(FlameKill, out int use)) return;
							power.NativeTags.Remove(FlameKill);
							if (use != c.NumTimesHeroPowerUsedThisGame) return;
							power.IsExhausted = false;
							c.HeroPowerActivationsThisTurn = 0;
						}),
					}),
			});
			// The client's player-enchantment record of the Flame ("Your Hero Power costs (0) this turn."): no effect of its own,
			// the shrine's aura carries the cost.
			cards.Add("TRLA_128e", Implemented(new CardDef(new Power())));

			// TRLA_129 Jan'alai's Progeny: "Whenever you Freeze a character, put a Frostfire into your hand." Only a freeze its
			// owner causes counts (ShrineRules.Freezer: the freezer rides on the character for the freeze event), any character.
			Shrine(cards, "TRLA_129", new Power
			{
				Trigger = new Trigger(TriggerType.FROZEN)
				{
					TriggerSource = TriggerSource.ALL,
					FastExecution = true,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (!(s is Minion shrine) || !Active(shrine) || t == null || ShrineRules.FreezerOf(t) != c.Id) return;
						var card = Entity.FromCard(c, Cards.FromId(Frostfire), new EntityData
						{
							[GameTag.CREATOR] = shrine.Id, [GameTag.DISPLAYED_CREATOR] = shrine.Id,
						});
						Generic.AddHandPhase.Invoke(c, card);
					}),
				},
			});
			// TRLA_129s Frostfire: "Deal $1 damage." (any character; spell damage applies).
			cards.Add(Frostfire, Implemented(new CardDef(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 } },
				new Power { PowerTask = new DamageTask(1, EntityType.TARGET, true) })));

			// ---------------- Paladin
			// TRLA_105 Shirvallah's Protection: "After a friendly minion survives damage, give it Divine Shield." (the shrine
			// itself is a friendly minion).
			Shrine(cards, "TRLA_105", new Power
			{
				Trigger = new Trigger(TriggerType.TAKE_DAMAGE)
				{
					TriggerSource = TriggerSource.MINIONS,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (!(s is Minion shrine) || !Active(shrine)) return;
						if (!(t is Minion m) || m.Controller != c || m.Zone != c.BoardZone || m.ToBeDestroyed || m.Health <= 0 || m.Untouchable
							|| m.HasDivineShield) return;
						m.HasDivineShield = true;
					}),
				},
			});

			// TRLA_137 Shirvallah's Vengeance: "After your hero takes damage, deal 5 damage to the enemy hero." A Vengeance never
			// answers another one's damage (ShrineRules.VengeanceResolving on the game), so two of them (a copy on the other side)
			// cannot bounce damage between the heroes for ever.
			Shrine(cards, "TRLA_137", new Power
			{
				Trigger = new Trigger(TriggerType.TAKE_DAMAGE)
				{
					TriggerSource = TriggerSource.HERO,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (!(s is Minion shrine) || !Active(shrine) || g.State == State.COMPLETE) return;
						if (g.NativeTags.TryGetValue(ShrineRules.VengeanceResolving, out int busy) && busy != 0) return;
						g.NativeTags[ShrineRules.VengeanceResolving] = 1;
						try { Generic.DamageCharFunc(shrine, c.Opponent.Hero, VengeanceDamage, false); }
						finally { g.NativeTags.Remove(ShrineRules.VengeanceResolving); }
					}),
				},
			});

			// TRLA_138 Shirvallah's Grace: "After you cast a spell on a friendly minion, cast a copy of it on this one." A copy
			// cast (RecastRules: Lynessa's cast, target locked) never triggers it again.
			Shrine(cards, "TRLA_138", new Power
			{
				Trigger = new Trigger(TriggerType.AFTER_CAST)
				{
					TriggerSource = TriggerSource.FRIENDLY,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (!(t is Spell spell) || RecastRules.IsRecast(spell) || !(s is Minion shrine) || !Active(shrine)
							|| shrine.ToBeDestroyed) return;
						if (spell.CardTarget == 0 || !g.IdEntityDic.TryGetValue(spell.CardTarget, out var target)
							|| !(target is Minion friendly) || friendly.Controller != c) return;
						RecastRules.Recast(spell, shrine, RecastRules.ChosenOption(spell));
					}),
				},
			});

			// ---------------- Priest
			// TRLA_114 Bwonsamdi's Sanctum: "Your Deathrattles trigger two additional times." (EXTRA_DEATHRATTLES_ADDITIONAL +2,
			// read by DeathrattleRules.Repeats beside the doubling of Totem of the Dead and its kind).
			Shrine(cards, "TRLA_114", new Power
			{
				Aura = new Aura(AuraType.CONTROLLER, new Effect(GameTag.EXTRA_DEATHRATTLES_ADDITIONAL, EffectOperator.ADD, 2)),
			});
			// The client's player-enchantment record of the Sanctum: no effect of its own, the shrine's aura carries it.
			cards.Add("TRLA_114e", Implemented(new CardDef(new Power())));

			// TRLA_146 Bwonsamdi's Tome: "After you cast a spell, add a random Priest spell to your hand." (the run's generation
			// filter applies: RunMatchPlan.ExcludedGenerated).
			Shrine(cards, "TRLA_146", new Power
			{
				Trigger = new Trigger(TriggerType.AFTER_CAST)
				{
					TriggerSource = TriggerSource.FRIENDLY,
					SingleTask = ComplexTask.Create(
						new RandomCardTask(CardType.SPELL, CardClass.PRIEST),
						new AddStackTo(EntityType.HAND)),
				},
			});

			// TRLA_147 Bwonsamdi's Covenant: "Healing enemies damages them instead." HealingRules.DamagesEnemies reads the shrine
			// (awake, unsilenced) on the board of the healed character's opponent: any healing of the owner's enemy characters, by
			// the owner or by the enemy itself (its spells, Lifesteal, a boss passive's restore), deals damage instead.
			Shrine(cards, "TRLA_147", new Power());
			// The client's enchantment record of the Covenant's rule: no effect of its own.
			cards.Add("TRLA_147e", Implemented(new CardDef(new Power())));
		}

		// The shrine's text works while it is on its owner's board and awake: the run shrine itself, or a plain copy of it
		// (Faceless Manipulator), which keeps the text (ShrineRules: a copy is an ordinary minion).
		private static bool Active(Minion m) => m.Zone == m.Controller.BoardZone && !m.IsDormant && !m.Untouchable;

		/// <summary>TRLA_128 Jan'alai's Flame.</summary>
		public const string Flame = "TRLA_128";

		/// <summary>
		/// <paramref name="c"/>'s hero powers cost (0): an awake, unsilenced TRLA_128 Jan'alai's Flame (or a plain copy) is on its
		/// board. CostRules.AfterExternalCostAuras applies it after every set cost, so an older "costs (1)" (Justicar's Ring) does
		/// not override it; the shrine's own aura carries the same cost to the hero power's COST tag.
		/// </summary>
		public static bool FreeHeroPower(Controller c)
		{
			var board = c?.BoardZone;
			if (board == null) return false;
			for (int i = 0; i < board.Count; i++)
			{
				var m = board[i];
				if (m.Card.Id == Flame && !m.IsSilenced && Active(m)) return true;
			}
			return false;
		}

		/// <summary>On a hero power while a use of it that killed a minion is resolving: the use count that refreshes it.</summary>
		public const GameTag FlameKill = (GameTag)32623;

		/// <summary>TRLA_137 Shirvallah's Vengeance's damage to the enemy hero.</summary>
		public const int VengeanceDamage = 5;

		/// <summary>The spells in <paramref name="c"/>'s hand (TRLA_108 Jan'alai's Mantle's Spell Damage).</summary>
		public static int SpellsInHand(Controller c)
		{
			int n = 0;
			var hand = c.HandZone;
			for (int i = 0; i < hand.Count; i++) if (hand[i].Card.Type == CardType.SPELL) n++;
			return n;
		}

		/// <summary>
		/// An aura that keeps one of its owner's own tags equal to a value (TRLA_108's SPELLPOWER: a minion's Spell Damage is its
		/// own tag, which a minion's aura effects do not carry). Silence removes it (and clears SPELLPOWER); a clone re-activates it.
		/// </summary>
		private sealed class OwnTagAura : IAura
		{
			private readonly GameTag _tag;
			private readonly Func<Playable, int> _value;
			private readonly Playable _owner;
			private bool _on = true;

			internal OwnTagAura(GameTag tag, Func<Playable, int> value) { _tag = tag; _value = value; }
			private OwnTagAura(OwnTagAura prototype, Playable owner) { _tag = prototype._tag; _value = prototype._value; _owner = owner; }

			public IPlayable Owner => _owner;

			public void Activate(IPlayable owner)
			{
				if (!(owner is Playable p)) throw new InvalidOperationException("OwnTagAura needs a playable owner: " + owner);
				var instance = new OwnTagAura(this, p);
				owner.Game.Auras.Add(instance);
				owner.OngoingEffect = instance;
			}

			public void Update()
			{
				if (!_on) { _owner.Game.Auras.Remove(this); return; }
				int value = _value(_owner);
				if (_owner[_tag] != value || !_owner.NativeTags.ContainsKey(_tag)) _owner[_tag] = value;
			}

			public void Remove()
			{
				_owner.OngoingEffect = null;
				_on = false;
			}

			public void Clone(IPlayable clone) => Activate(clone);

			public override string ToString() => "[OTA:" + _owner?.Card.Name + "]";
		}
	}
}
