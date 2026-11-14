using System;
using System.Collections.Generic;
using System.Linq;
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
	// Phase 8, family 15: Dalaran Heist bosses B. Fourteen encounters and their two boss cards: Alchemist Wendy (Equivalent
	// Exchange, cast by rule), Tala Stonerage (Twin Paths), Disidra Stormglory (Totemic Summons, cast by rule), Linzi Redgrin
	// (Lil' Eviscerate), Vas'no (Stormswell), Bookmaster Bae Chao (Shhh!), Sharky McFin (Sharkbite), Ranger Ar'ha (Ar'ha's
	// Call), Jepetto Joybuzz (Assembly; Hoard of Toys), Dalaran Fountain Golem (Cold Water), Rasil Fireborne (Masterpiece!),
	// Xur'ios (Scroll Savvy), Mama Diggs (Excavate) and The Rat King (A Tale of Kings; Underbelly Rat, whose hero replacement
	// is Actions/EncounterRules.F15.cs). The texts are the client's (facts/violet-hold-2026-09.json); what a text leaves open
	// is decided in the row's adaptation notes (reference/encounters/F15.json). A clickable power that would do nothing is not
	// offered, by a condition that reads only what is exact in the boss's fair view (boards, heroes, its own hand, hand counts,
	// its own player enchantments). Every random choice uses the game RNG and is reported as a random event. The only state is
	// entity tags: the cast-by-rule count and Cold Water's last turn on the hero power (TAG_SCRIPT_DATA_NUM_2 / _NUM_1), the
	// form index of the Rat King.
	public static partial class DungeonRunEncounters
	{
		/// <summary>DALA_BOSS_25t Hoard of Toys: how many 1/1 copies it summons.</summary>
		public const int HoardOfToysCopies = 5;
		/// <summary>DALA_BOSS_25e Toyification, "Stats changed to 1/1." (Assembly's copies and Hoard of Toys').</summary>
		public const string Toyification = "DALA_BOSS_25e";
		/// <summary>DALA_BOSS_24e Ar'ha's Call's player enchantment ("Triggers additional Battlecries.").</summary>
		public const string ArhasCall = "DALA_BOSS_24e";
		/// <summary>DALA_BOSS_28e Savvy, "Costs (0) this turn." (Scroll Savvy).</summary>
		public const string Savvy = "DALA_BOSS_28e";
		/// <summary>GILA_500p2t A Small Rock: one of Mama Diggs's excavations (the wiki's list).</summary>
		public const string SmallRock = "GILA_500p2t";
		/// <summary>DALA_BOSS_29p Excavate: the six excavations of the wiki's list, each equally likely (the wiki gives no odds).</summary>
		public const int ExcavateResults = 6;
		public const int ExcavateNothing = 0, ExcavateRock = 1, ExcavateDraw = 2, ExcavateShaman = 3, ExcavateCopy = 4, ExcavateTreasure = 5;

		/// <summary>DALA_BOSS_18p Totemic Summons, "ANY random Totem": the four basic Totems join the Totem minions of the card pool.</summary>
		public static readonly IReadOnlyList<string> BasicTotems = new[] { "CS2_050", "CS2_051", "CS2_052", "NEW1_009" };

		/// <summary>The Totems Totemic Summons may summon for <paramref name="c"/>: scripted Totem minions of any class and the basic Totems the run's generation filter allows.</summary>
		public static IReadOnlyList<Card> TotemPool(Game g, Controller c, IEntity source)
		{
			var pool = RuleMinions(g, c, source, "totem", card => card.IsRace(Race.TOTEM)).ToList();
			using (g.GenerateFor(c))
				foreach (Card basic in BasicTotems.Select(Cards.FromId))
					if (basic != null && basic.Implemented && !g.BlocksGenerated(basic) && pool.All(p => p.Id != basic.Id)) pool.Add(basic);
			return pool;
		}

		private static readonly Dictionary<string, IReadOnlyList<Card>> ShamanPools = new Dictionary<string, IReadOnlyList<Card>>(StringComparer.Ordinal);

		/// <summary>Excavate's "Random shaman cards": the scripted Shaman cards (no hero cards) the run's generation filter allows for <paramref name="c"/>.</summary>
		public static IReadOnlyList<Card> ShamanPool(Game g, Controller c, IEntity source)
		{
			string key;
			using (g.GenerateFor(c)) key = g.FormatType + "|" + g.GeneratedFilterKey;
			lock (ShamanPools)
				if (ShamanPools.TryGetValue(key, out var cached)) return cached;
			var pool = ScriptedPool(g, c, source, card => card.Class == CardClass.SHAMAN, anyClass: true);
			lock (ShamanPools) ShamanPools[key] = pool;
			return pool;
		}

		/// <summary>
		/// DALA_BOSS_16p Equivalent Exchange, "Swap a random card in each player's hand": one random card of each hand changes
		/// sides by the hand-to-hand transfer rule of GILA_906 Impetuous Companion (a CardTransferredFact each, so the side that gave
		/// a card knows it; the enchantments and the Cost stay with the card). Both cards leave their hands first, so a full hand
		/// never burns one. False, and nothing happens, when either hand is empty.
		/// </summary>
		public static bool ExchangeRandomCards(Game g, Controller c, IEntity s)
		{
			Controller op = c.Opponent;
			if (c.HandZone.Count == 0 || op.HandZone.Count == 0 || g.State == State.COMPLETE) return false;
			g.OnRandomHappened(true);
			IPlayable mine = c.HandZone[g.Random.Next(c.HandZone.Count)];
			IPlayable theirs = op.HandZone[g.Random.Next(op.HandZone.Count)];
			var moving = new[] { mine, theirs };
			var from = moving.ToDictionary(card => card, card => card.Controller);
			var hadAura = new HashSet<IPlayable>(moving.Where(card => card.OngoingEffect != null && !(card.OngoingEffect is AdaptiveCostEffect)));
			var preserved = new List<IDisposable>();
			foreach (IPlayable card in moving)
			{
				IDisposable keep = TemporaryRules.PreserveHandTransfer(card);
				if (keep != null) preserved.Add(keep);
				card.ActivatedTrigger?.Remove();
				if (card.OngoingEffect is AdaptiveCostEffect cost) cost.Remove();
				else card.OngoingEffect?.Remove();
				from[card].HandZone.RemoveForTrade(card);
			}
			foreach (IPlayable card in new[] { theirs, mine })
			{
				Controller giver = from[card];
				Controller receiver = giver == c ? op : c;
				using (g.Facts.CardTransfer(card, giver.Id, Zone.HAND, s))
				{
					GiveTo(card, receiver);
					Generic.AddHandPhase.Invoke(receiver, card);
				}
				if (hadAura.Contains(card) && card.OngoingEffect == null && card.Zone == receiver.HandZone) card.Power?.Aura?.Activate(card);
			}
			foreach (IDisposable keep in preserved) keep.Dispose();
			CostRules.RefreshHandAuras(g);
			g.AuraUpdate();
			return true;
		}

		// A hidden card changing side (GILA_906's controller rule).
		private static void GiveTo(IPlayable card, Controller receiver)
		{
			card.Controller = receiver;
			card[GameTag.CONTROLLER] = receiver.PlayerId;
			foreach (Enchantment actor in card.AppliedEnchantments?.ToArray() ?? Array.Empty<Enchantment>())
				actor.RebindController(receiver, refreshAuras: false);
			if (card.ChooseOnePlayables != null)
				foreach (IPlayable option in card.ChooseOnePlayables)
				{
					option.Zone?.Remove(option);
					option.Controller = receiver;
					option[GameTag.CONTROLLER] = receiver.PlayerId;
					receiver.SetasideZone.Add(option);
				}
		}

		/// <summary>
		/// DALA_BOSS_25p Assembly: <paramref name="drawn"/>'s controller summons a 1/1 copy of it (a fresh entity of its card with
		/// Toyification), as Dollmaster Dorian does. Nothing with a full board or a card that is not a minion.
		/// </summary>
		public static Minion Assemble(IPlayable drawn, IEntity source)
		{
			if (!(drawn is Minion) || drawn.Game.State == State.COMPLETE) return null;
			Controller owner = drawn.Controller;
			if (owner.BoardZone.IsFull) return null;
			Game g = drawn.Game;
			var copy = (Minion)Entity.FromCard(owner, drawn.Card);
			Generic.SummonBlock(g, copy, -1, source);
			if (copy.Zone != owner.BoardZone) return null;
			Generic.AddEnchantmentBlock(g, Cards.FromId(Toyification), (IPlayable)source, copy);
			return copy;
		}

		/// <summary>DALA_BOSS_29p Excavate's result <paramref name="result"/> (0..5) for the boss <paramref name="c"/>; <paramref name="dug"/> is the damaged minion's card.</summary>
		public static void Excavated(Game g, Controller c, IEntity s, int result, Card dug)
		{
			void Give(Card card)
			{
				if (card == null) return;
				Generic.AddHandPhase.Invoke(c, Entity.FromCard(c, card, new EntityData { [GameTag.CREATOR] = s.Id, [GameTag.DISPLAYED_CREATOR] = s.Id }));
			}
			switch (result)
			{
				case ExcavateRock:
					Give(Cards.FromId(SmallRock));
					break;
				case ExcavateDraw:
					int draws = 1 + g.Random.Next(2);
					for (int i = 0; i < draws && g.State != State.COMPLETE; i++) Generic.Draw(c);
					break;
				case ExcavateShaman:
					var pool = ShamanPool(g, c, s);
					if (pool.Count > 0) Give(pool[g.Random.Next(pool.Count)]);
					break;
				case ExcavateCopy:
					Give(dug);
					break;
				case ExcavateTreasure:
					Give(Cards.FromId(TogwaggleTreasures[g.Random.Next(TogwaggleTreasures.Count)]));
					break;
			}
		}

		private static void F15()
		{
			// ---------------- Alchemist Wendy (DALA_BOSS_16h)
			// Equivalent Exchange (1): "Swap a random card in each player's hand." The wiki's auto-cast: cast by rule at the start of
			// the boss's turn while both hands hold a card (AutoCast); one random card of each hand changes sides.
			AutoCast("DALA_BOSS_16p", c => c.HandZone.Count > 0 && c.Opponent.HandZone.Count > 0,
				(g, c, s, stack) => ExchangeRandomCards(g, c, s));

			// ---------------- Tala Stonerage (DALA_BOSS_17h)
			// Twin Paths: "Your Choose One cards and powers have both effects combined." Fandral Staghelm's controller aura.
			Passive("DALA_BOSS_17p", () => new Power
			{
				Aura = new Aura(AuraType.CONTROLLER, new Effect(GameTag.CHOOSE_BOTH, EffectOperator.ADD, 1)),
			});

			// ---------------- Disidra Stormglory (DALA_BOSS_18h)
			// Totemic Summons (2): "Summon ANY random Totem." The wiki's auto-cast: cast by rule at the start of the boss's turn while
			// its board has room; a random Totem of any class and set, the basic Totems included (TotemPool).
			AutoCast("DALA_BOSS_18p", c => !c.BoardZone.IsFull, (g, c, s, stack) => SummonRandom(g, c, s, TotemPool(g, c, s)));

			// ---------------- Linzi Redgrin (DALA_BOSS_19h)
			// Lil' Eviscerate (2): "Deal 1 damage. Combo: 2 instead." Any character; Combo when the boss played a card this turn.
			Power("DALA_BOSS_19p", new[] { PlayReq.REQ_TARGET_TO_PLAY }, () => new CustomTask((g, c, s, t, stack) =>
				new DamageTask(c.IsComboActive ? 2 : 1, EntityType.TARGET).Process(g, c, s, t, stack)));

			// ---------------- Vas'no (DALA_BOSS_20h)
			// Stormswell: "Your spells with Overload cast twice." After the boss casts a spell with Overload, a copy is cast again on
			// the same target (Pirate's Mark's recast: the target locked, the Choose One option kept, a copy never casts again).
			Passive("DALA_BOSS_20p", () => new Power
			{
				Trigger = new Trigger(TriggerType.AFTER_CAST)
				{
					TriggerSource = TriggerSource.FRIENDLY,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (!(t is Spell spell) || spell.Card[GameTag.OVERLOAD] <= 0 || RecastRules.IsRecast(spell) || spell.Card.IsSecret
							|| spell.Card.IsQuest || g.State == State.COMPLETE) return;
						ICharacter target = spell.CardTarget != 0 && g.IdEntityDic.TryGetValue(spell.CardTarget, out var e) ? e as ICharacter : null;
						RecastRules.Recast(spell, target, RecastRules.ChosenOption(spell));
					}),
				},
			});

			// ---------------- Bookmaster Bae Chao (DALA_BOSS_22h)
			// Shhh! (3): "Silence ALL minions." Both boards; offered only while a minion is on a board.
			Power("DALA_BOSS_22p", null, () => new CustomTask((g, c, s, t, stack) =>
			{
				foreach (Minion m in Touchable(c).Concat(Touchable(c.Opponent)).ToList())
					new SilenceTask(EntityType.TARGET).Process(g, c, s, m, stack);
			}), usable: c => Touchable(c).Any() || Touchable(c.Opponent).Any());

			// ---------------- Sharky McFin (DALA_BOSS_23h)
			// Sharkbite (2): "Destroy a random damaged minion." Either board; offered only while a minion is damaged.
			Power("DALA_BOSS_23p", null, () => new CustomTask((g, c, s, t, stack) =>
			{
				var damaged = Touchable(c).Concat(Touchable(c.Opponent)).Where(m => m.Damage > 0).ToList();
				if (damaged.Count == 0) return;
				Minion victim = damaged[0];
				if (damaged.Count > 1)
				{
					victim = damaged[g.Random.Next(damaged.Count)];
					g.OnRandomHappened(true);
				}
				DeathContextRules.RecordDestroy(victim, s);
				victim.Destroy();
			}), usable: c => Touchable(c).Concat(Touchable(c.Opponent)).Any(m => m.Damage > 0));

			// ---------------- Ranger Ar'ha (DALA_BOSS_24h)
			// Ar'ha's Call (2): "Your next Battlecry triggers an additional time." Its player enchantment adds one Battlecry resolution
			// (EXTRA_BATTLECRIES_ADDITIONAL) until the boss's next Battlecry minion is played, then removes itself. Not offered while
			// one is waiting (its own player enchantments are public).
			Power("DALA_BOSS_24p", null, () => new CustomTask((g, c, s, t, stack) =>
			{
				QuestRules.AddPlayerEffect(c, (IPlayable)s, ArhasCall);
				Enchantment call = c.AppliedEnchantments.Last(e => e.Card.Id == ArhasCall);
				if (call.OngoingEffect == null) call.Card.Power.Aura?.Activate(call);
			}), usable: c => c.AppliedEnchantments?.Any(e => e.Card.Id == ArhasCall) != true);
			Enchantment(ArhasCall, () => new Power
			{
				Aura = new Aura(AuraType.CONTROLLER, new Effect(GameTag.EXTRA_BATTLECRIES_ADDITIONAL, EffectOperator.ADD, 1)),
				Trigger = new Trigger(TriggerType.AFTER_PLAY_MINION)
				{
					TriggerSource = TriggerSource.FRIENDLY,
					Condition = SelfCondition.IsBattlecryMinion,
					SingleTask = RemoveEnchantmentTask.Task,
				},
			});

			// ---------------- Jepetto Joybuzz (DALA_BOSS_25h)
			// Assembly: "After a player draws a minion, they summon a 1/1 copy of it." Either player's draws of the game (mulligan
			// replacements are not); the drawer summons the copy (Dollmaster Dorian's rule, with Toyification).
			Passive("DALA_BOSS_25p", () => new Power
			{
				Trigger = new Trigger(TriggerType.DRAW)
				{
					TriggerSource = TriggerSource.ALL,
					Condition = new SelfCondition(p => p is Minion && p.Game.Step != Step.BEGIN_MULLIGAN),
					SingleTask = new CustomTask((g, c, s, t, stack) => Assemble(t, s)),
				},
			});
			Enchantment(Toyification, () => new Power { Enchant = new Enchant(Effects.SetAttackHealth(1)) });
			// Hoard of Toys (4): "Summon 1/1 copies of five random minions." Five random scripted minions of any class the run's
			// generation filter allows for the caster, each with Toyification; fewer with less board room.
			BossCard("DALA_BOSS_25t", () => new CardDef(new Power
			{
				PowerTask = new CustomTask((g, c, s, t, stack) =>
				{
					var pool = RuleMinions(g, c, s, "anyminion", card => true);
					for (int i = 0; i < HoardOfToysCopies && pool.Count > 0 && !c.BoardZone.IsFull && g.State != State.COMPLETE; i++)
					{
						g.OnRandomHappened(true);
						var toy = (Minion)Entity.FromCard(c, pool[g.Random.Next(pool.Count)]);
						Generic.SummonBlock(g, toy, -1, s);
						if (toy.Zone == c.BoardZone) Generic.AddEnchantmentBlock(g, Cards.FromId(Toyification), (IPlayable)s, toy);
					}
				}),
			}));

			// ---------------- Dalaran Fountain Golem (DALA_BOSS_26h)
			// Cold Water: "Freeze the first minion that attacks you each turn." After an enemy minion's attack on the boss's hero, the
			// first such attacker of the turn is Frozen; the hero power keeps that turn in TAG_SCRIPT_DATA_NUM_1.
			Passive("DALA_BOSS_26p", () => new Power
			{
				Trigger = new Trigger(TriggerType.AFTER_ATTACK)
				{
					TriggerSource = TriggerSource.OP_MINIONS,
					Condition = new SelfCondition(p => p.Game.CurrentEventData?.EventTarget is Hero h && h.Controller != p.Controller),
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (s[GameTag.TAG_SCRIPT_DATA_NUM_1] == g.Turn) return;
						s[GameTag.TAG_SCRIPT_DATA_NUM_1] = g.Turn;
						if (t is Minion m && m.Zone == m.Controller.BoardZone && !m.ToBeDestroyed && !m.IsDead)
							ComplexTask.Freeze(EntityType.TARGET).Process(g, c, s, m, stack);
					}),
				},
			});

			// ---------------- Rasil Fireborne (DALA_BOSS_27h)
			// Masterpiece! (0): "Spend all your Mana. Summon a random minion that costs that much." All remaining Mana is spent; one
			// random scripted minion of exactly that Cost the generation filter allows; needs a free board slot.
			Power("DALA_BOSS_27p", null, () => new CustomTask((g, c, s, t, stack) =>
			{
				int mana = Math.Max(0, c.RemainingMana);
				c.UsedMana += mana;
				SummonRandom(g, c, s, RuleMinions(g, c, s, "cost" + mana, card => card.Cost == mana));
			}), usable: c => !c.BoardZone.IsFull);

			// ---------------- Xur'ios (DALA_BOSS_28h)
			// Scroll Savvy: "Spells cost (0) the turn they are drawn." The text names no player, so both: a spell either player draws
			// (not a mulligan replacement) costs (0) until the turn ends (Savvy).
			Passive("DALA_BOSS_28p", () => new Power
			{
				Trigger = new Trigger(TriggerType.DRAW)
				{
					TriggerSource = TriggerSource.ALL,
					Condition = new SelfCondition(p => p is Spell && p.Game.Step != Step.BEGIN_MULLIGAN),
					SingleTask = new AddEnchantmentTask(Savvy, EntityType.TARGET),
				},
			});
			Enchantment(Savvy, () => new Power { Enchant = new Enchant(Effects.SetCost(0)) { IsOneTurnEffect = true } });

			// ---------------- Mama Diggs (DALA_BOSS_29h)
			// Excavate (1): "Deal 2 damage to a friendly Elemental. Find out what's inside." The 2 damage, then one of the wiki's six
			// excavations, each equally likely (Excavated).
			Add("DALA_BOSS_29p", EncounterCardKind.Power, () => new CardDef(
				new Dictionary<PlayReq, int>
				{
					{ PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 }, { PlayReq.REQ_FRIENDLY_TARGET, 0 }, { PlayReq.REQ_TARGET_WITH_RACE, (int)Race.ELEMENTAL },
				},
				new Power
				{
					PowerTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (!(t is Minion dug)) return;
						Card card = dug.Card;
						new DamageTask(2, EntityType.TARGET).Process(g, c, s, t, stack);
						if (g.State == State.COMPLETE) return;
						g.OnRandomHappened(true);
						Excavated(g, c, s, g.Random.Next(ExcavateResults), card);
					}),
				}));

			// ---------------- The Rat King (DALA_BOSS_30h)
			// A Tale of Kings (3): "Summon an Underbelly Rat." Needs a free board slot.
			Power("DALA_BOSS_30p", null, () => new SummonTask(EncounterRules.UnderbellyRat), usable: c => !c.BoardZone.IsFull);
			// Underbelly Rat (10/1): "When your hero dies, the eldest Underbelly Rat replaces your hero." No script of its own: the
			// replacement is EncounterRules.ResolveUnderbellyRats, beside the hero forms.
			BossCard(EncounterRules.UnderbellyRat, () => new CardDef());
		}
	}
}
