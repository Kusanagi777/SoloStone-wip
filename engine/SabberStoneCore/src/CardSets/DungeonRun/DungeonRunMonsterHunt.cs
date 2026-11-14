using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Zones;
using SabberStoneCore.src.Loader;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.DungeonRun
{
	public static partial class DungeonRunContent
	{
		// ---------------------------------------------------------------- MonsterHuntTessDarius
		// The Witchwood Monster Hunt heroes Tess Greymane and Darius Crowley (phase 6, family F3): their special powers
		// (GILA_500p2 Scavenge, GILA_600p Fire!), Darius's Cannons (GILA_601, Cannons below), and the hero treasures of both.
		// Every id is keyed and shelled by its own client id, so the run deck, the treasure picker and the match show the real
		// card; every name and text is the client's own, word for word (facts/violet-hold-2026-09.json). The treasures are the
		// run player's cards only (DungeonRunAdmission admits them for the player); none enters a boss deck. Every random
		// choice uses the game RNG; every piece of state is an entity tag, so clones and AI simulations replay it.

		/// <summary>The Secrets GILA_509 Frenzied Trapper puts into the battlefield.</summary>
		public const int FrenziedTrapperSecrets = 5;

		/// <summary>Tess's three weapons, the options GILA_510 Tools of the Trade discovers (Stake Thrower, Handgonne, Blunderbuss).</summary>
		public static readonly IReadOnlyList<string> TessWeapons = new[] { "GILA_500t", "GILA_500t2", "GILA_500t3" };

		/// <summary>GILA_500p2 Scavenge's card when no class spell has been played this game ("A Small Rock").</summary>
		public const string ScavengeFallback = "GILA_500p2t";

		/// <summary>
		/// H6: "You can use your Hero Power twice each turn. It costs (<paramref name="costDelta"/>) less." (GILA_513 Cult of the
		/// Wolf; the Heist passive DALA_736 is the same rule). An aura on the owner's hero power, and the Garrison Commander
		/// (AT_080) rule: after the first use of a turn, the power is readied again.
		/// </summary>
		internal static Power HeroPowerTwice(int costDelta) => new Power
		{
			Aura = new Aura(AuraType.HEROPOWER, Effects.ReduceCost(costDelta)),
			Trigger = new Trigger(TriggerType.INSPIRE)
			{
				TriggerSource = TriggerSource.FRIENDLY,
				Condition = new SelfCondition(p => p.Controller.HeroPowerActivationsThisTurn == 1),
				SingleTask = new SetGameTagTask(GameTag.EXHAUSTED, 0, EntityType.HERO_POWER),
			},
		};

		/// <summary>
		/// GILA_500p2 Scavenge's Discover pool: every class (non-neutral) spell either player has played this game (the public
		/// play history, both sides), scripted, that this game's generation may yield for the owner; distinct, ordinal by id.
		/// </summary>
		public static IReadOnlyList<Card> ScavengeCandidates(Controller c)
		{
			Game game = c.Game;
			return game.Player1.PlayHistory.Concat(game.Player2.PlayHistory).Select(e => e.SourceCard)
				.Where(card => card != null && card.Type == CardType.SPELL && card.Class != CardClass.NEUTRAL && card.Class != CardClass.INVALID
					&& card.Implemented && !(game.FiltersGenerated && game.BlocksGenerated(card)))
				.GroupBy(card => card.Id, StringComparer.Ordinal).Select(g => g.First())
				.OrderBy(card => card.Id, StringComparer.Ordinal).ToArray();
		}

		private static void Scavenge(Game g, Controller c, IEntity s, TaskStack stack)
		{
			var pool = ScavengeCandidates(c);
			if (pool.Count == 0)
			{
				// Nothing to scavenge: the power's own fallback card.
				Generic.AddHandPhase(c, Entity.FromCard(c, Cards.FromId(ScavengeFallback)));
				return;
			}
			var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
			ctx.Discover(ctx.SampleDistinct(pool, 3), ChoiceAction.HAND);
		}

		/// <summary>GILA_501 Exterminate: $10 damage to a minion, then any excess (damage beyond its Health) to the enemy hero.</summary>
		private static void Exterminate(Controller c, IEntity s, IPlayable t)
		{
			if (!(t is Minion minion) || !(s is IPlayable spell)) return;
			int health = minion.Health;
			int dealt = Generic.DamageCharFunc(spell, minion, 10, true);
			int excess = Math.Max(0, dealt - health);
			if (excess > 0) Generic.DamageCharFunc(spell, c.Opponent.Hero, excess, false);
		}

		/// <summary>GILA_608 Safe Harbor: the chosen minion goes to the owner's hand at (0). An enemy minion leaves the board
		/// (no Deathrattle) and its card is put into the owner's hand.</summary>
		private static void SafeHarbor(Game g, Controller c, IEntity s, IPlayable t)
		{
			if (!(t is Minion minion) || minion.Zone?.Type != Zone.PLAY) return;
			Card zero = Cards.FromId(Get("fx_GILA_608").ShellId);
			IPlayable inHand;
			if (minion.Controller == c)
			{
				Generic.ReturnToHandBlock.Invoke(c, minion);
				inHand = minion.Zone == c.HandZone ? minion : null;
			}
			else
			{
				var removed = minion.Controller.BoardZone.Remove(minion);
				minion.Controller.SetasideZone.Add(removed);
				inHand = Entity.FromCard(c, minion.Card);
				Generic.AddHandPhase(c, inHand);
				if (inHand.Zone != c.HandZone) inHand = null;
			}
			if (inHand != null) Generic.AddEnchantmentBlock(g, zero, (IPlayable)s, inHand);
		}

		/// <summary>GILA_607 Pillage the Fallen: a random scripted weapon of the generation pool, equipped (run exclusions honoured).</summary>
		private static void PillageTheFallen(Game g, Controller c, IEntity s, TaskStack stack)
		{
			stack.Playables = Array.Empty<IPlayable>();
			// Only a weapon that broke: one replaced by a newly equipped weapon leaves the hero armed, and equipping over it
			// would destroy that weapon in turn.
			if (c.Hero.Weapon != null) return;
			var pool = RandomCardTask.GetCardList(s, CardType.WEAPON).Where(card => card.Implemented && card.Type == CardType.WEAPON)
				.GroupBy(card => card.Id, StringComparer.Ordinal).Select(x => x.First()).OrderBy(card => card.Id, StringComparer.Ordinal).ToList();
			if (pool.Count == 0) return;
			stack.Playables = new[] { Entity.FromCard(c, pool[g.Random.Next(pool.Count)]) };
			g.OnRandomHappened(true);
		}

		private static void MonsterHuntTessDarius()
		{
			// ---------------- Tess Greymane: Scavenge, her weapons, her treasures
			Power("GILA_500p2", "GILA_500p2", "Scavenge", "[x]<b>Hero Power</b>\n<b>Discover</b> a class spell\nthat has been played\nthis game.", 2,
				null, () => new CustomTask((g, c, s, t, stack) => Scavenge(g, c, s, stack)));
			Active(ScavengeFallback, "A Small Rock", "Deal $1 damage.", 0,
				() => Def(new Power { PowerTask = new DamageTask(1, EntityType.TARGET, true) }, PlayReq.REQ_TARGET_TO_PLAY));
			Active("GILA_500t", "Stake Thrower", "<b>Windfury</b>", 1, () => new CardDef(), card => card.Tags[GameTag.WINDFURY] = 1);
			Active("GILA_500t2", "Handgonne", null, 1, () => new CardDef());
			// "Also damages the minions next to whomever your hero attacks": the engine's cleave rule (CombatRules.Cleave), which a
			// hero has while its weapon carries it (CombatRules.CleaveTargets).
			Active("GILA_500t3", "Blunderbuss", "Also damages the minions next to whomever your hero_attacks.", 1,
				() => new CardDef(), card => card.Tags[CombatRules.Cleave] = 1);

			Buff("fx_GILA_503e", "GILA_503e", "Insightful", "This card costs (3) less.", Effects.ReduceCost(3));
			Buff("fx_GILA_507e", "GILA_507e", "Veteran's Intuition", "Increased Spell Damage.", new SabberStoneCore.Enchants.Effect(GameTag.SPELLPOWER, EffectOperator.ADD, 2));
			Effect("fx_GILA_506", "GILA_506e", "First Aid Kit Player Enchantment", null,
				() => new Power { Trigger = Trig(TriggerType.TURN_END, new HealTask(2, EntityType.FRIENDS)) });
			Effect("fx_GILA_513", "GILA_513e", "Cult of the Wolf Player Enchant", null, () => HeroPowerTwice(1));

			Active("GILA_501", "Exterminate", "<b>Echo</b>.\nDeal $10 damage to a minion and any excess to the enemy hero.", 3,
				() => Def(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => Exterminate(c, s, t)) },
					PlayReq.REQ_TARGET_TO_PLAY, PlayReq.REQ_MINION_TARGET));
			Active("GILA_503", "Hunter's Insight", "Reduce the cost of all cards in your hand by_(3).", 3,
				() => Def(new Power { PowerTask = new AddEnchantmentTask("GILA_503e", EntityType.HAND) }));
			Treasure("GILA_506", "First Aid Kit", "<b>Passive</b>\nAt the end of your turn, restore 2_Health to all friendly characters.",
				effect: "fx_GILA_506", ownShell: true);
			Active("GILA_507", "Hunter of Old", "Whenever you cast a spell, gain <b>Spell Damage +2</b>.", 3,
				() => Def(new Power
				{
					Trigger = new Trigger(TriggerType.CAST_SPELL) { TriggerSource = TriggerSource.FRIENDLY, SingleTask = new AddEnchantmentTask("GILA_507e", EntityType.SOURCE) }
				}));
			Active("GILA_508", "The Exorcisor", "<b>Silence</b> any minion attacked by this weapon.", 3,
				() => Def(new Power
				{
					Trigger = new Trigger(TriggerType.ATTACK)
					{
						TriggerSource = TriggerSource.HERO,
						SingleTask = ComplexTask.Create(
							new ConditionTask(EntityType.EVENT_TARGET, SelfCondition.IsMinion),
							new FlagTask(true, new SilenceTask(EntityType.EVENT_TARGET))),
					}
				}));
			Active("GILA_509", "Frenzied Trapper", "<b>Battlecry:</b> Put 5 random Hunter <b>Secrets</b> into the_battlefield.", 5,
				() => Def(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => PlayRandomSecrets(c, FrenziedTrapperSecrets, CardClass.HUNTER)) }),
				card => card.Tags[GameTag.BATTLECRY] = 1);
			Active("GILA_510", "Tools of the Trade", "<b>Discover</b> one of Tess's weapons and equip it. Return this to your hand.", 2,
				() =>
				{
					var equip = new WeaponTask();
					return Def(new Power
					{
						PowerTask = ComplexTask.Create(
							new CustomTask((g, c, s, t, stack) =>
							{
								var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
								ctx.Discover(TessWeapons.Select(Cards.FromId).ToArray(), ChoiceAction.STACK, equip);
							}),
							new AddCardTo("GILA_510", EntityType.HAND))
					});
				}, null, TessWeapons.ToArray());
			Treasure("GILA_513", "Cult of the Wolf", "<b>Passive</b>\nYou can use your Hero Power twice each turn.\nIt costs (1) less.",
				effect: "fx_GILA_513", ownShell: true);

			// ---------------- Darius Crowley: Fire!, the Cannons, his treasures
			Power("GILA_600p", "GILA_600p", "Fire!", "<b>Hero Power</b>\nFire your Cannons!\nIf they kill any minions, refresh this.", 2,
				null, () => new CustomTask((g, c, s, t, stack) => Cannons.Fire(c, s as IPlayable, fromPower: true)));
			// The Cannon's own trigger readies Fire! again after a use whose shots killed (the power is exhausted only after its
			// task, so the refresh runs on the Inspire event that follows; Cannons.RefreshPending carries the kill).
			Active(Cannons.CannonId, "Cannon", "Deal @ damage to the enemies opposite of this_minion.", 0,
				() => new CardDef(new Power
				{
					Trigger = new Trigger(TriggerType.INSPIRE)
					{
						TriggerSource = TriggerSource.FRIENDLY,
						SingleTask = new CustomTask((g, c, s, t, stack) => Cannons.Refresh(c)),
					}
				}), card => card.SetPrintedStats(0, 3));
			// Darius's start of game: his two Cannons, one at each edge of the board (Cannons.StartOfGame).
			var darius = Get("GILA_600h");
			var dariusShape = darius.Shape;
			darius.Shape = card =>
			{
				dariusShape?.Invoke(card);
				if (!StartOfGameRules.HasEffect(card)) StartOfGameRules.Declare(card, Cannons.StartOfGame);
			};

			Effect("fx_GILA_602", "GILA_602e2", "Victorious Rally Player Enchant", null, () => new Power());
			Effect("fx_GILA_602e3", "GILA_602e3", "Strategic Initiative", "Increased Attack this turn.",
				() => new Power { Enchant = new Enchant(Effects.Attack_N(1)) { IsOneTurnEffect = true } }, oneTurn: true);
			Effect("fx_GILA_603", "GILA_603e2", "Tactical Reinforcements Player Enchant", null, () => new Power());
			Buff("fx_GILA_603e3", "GILA_603e3", "Inspired", "Costs (2) less.", Effects.ReduceCost(2));
			Effect("fx_GILA_604", "GILA_604e2", "Extra Powder Player Enchant", null, () => new Power(), oneTurn: true);
			Effect("fx_GILA_605", "GILA_605e2", "Dragonfire Ammo Player Enchant", null, () => new Power());
			Effect("fx_GILA_607", "GILA_607e2", "Pillage the Fallen Player Enchantment", null,
				() => new Power
				{
					Trigger = new Trigger(TriggerType.DEATH)
					{
						TriggerSource = TriggerSource.WEAPON,
						SingleTask = ComplexTask.Create(new CustomTask((g, c, s, t, stack) => PillageTheFallen(g, c, s, stack)), new WeaponTask()),
					}
				});
			// No client enchantment for Safe Harbor's discount: a borrowed, unscripted Battlegrounds shell with the effect's text.
			Buff("fx_GILA_608", "TB_BaconShopBadsongE", "Safe Harbor", "Costs (0).", Effects.SetCost(0));

			Treasure("GILA_602", "Victorious Rally", "[x]<b>Passive</b>\nWhenever you fire your\nCannons, give your minions\n+1 Attack this turn.",
				effect: "fx_GILA_602", ownShell: true);
			Treasure("GILA_603", "Tactical Reinforcements", "[x]<b>Passive</b>\nWhenever you fire your\nCannons, reduce the cost of\na card in your hand by (2).",
				effect: "fx_GILA_603", ownShell: true);
			Active("GILA_604", "Extra Powder", "[x]Your Cannons do 2\nextra damage this turn.", 1,
				() => Def(new Power { PowerTask = new AddEnchantmentTask("GILA_604e2", EntityType.CONTROLLER) }));
			Treasure("GILA_605", "Dragonfire Ammo", "<b>Passive</b>\nYour Cannons do\n1 extra damage.", effect: "fx_GILA_605", ownShell: true);
			Treasure("GILA_607", "Pillage the Fallen", "<b>Passive</b>\nWhenever your weapon is destroyed, equip a random weapon.",
				effect: "fx_GILA_607", ownShell: true);
			Active("GILA_608", "Safe Harbor", "Choose a minion and put it into your hand.\nIt costs (0).", 2,
				() => Def(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => SafeHarbor(g, c, s, t)) },
					PlayReq.REQ_TARGET_TO_PLAY, PlayReq.REQ_MINION_TARGET));
			Active("GILA_610", "Grizzled Reinforcement", "<b>Battlecry:</b> Summon an extra Cannon.", 3,
				() => Def(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => Cannons.SummonExtra(c, s as IPlayable)) }),
				card => card.Tags[GameTag.BATTLECRY] = 1, Cannons.CannonId);
			Active("GILA_611", "Tuskarr Raider", "<b>Rush</b>\nAfter this minion attacks, fire your Cannons.", 4,
				() => Def(new Power
				{
					Trigger = new Trigger(TriggerType.AFTER_ATTACK)
					{
						TriggerSource = TriggerSource.SELF,
						SingleTask = new CustomTask((g, c, s, t, stack) => Cannons.Fire(c, s as IPlayable, fromPower: false)),
					}
				}));
			Active("GILA_612", "Seabreaker Goliath", "Costs (1) less for each time you've fired your Cannons.", 10,
				() => Def(new Power { Aura = new AdaptiveCostEffect(p => p.Controller[Cannons.FiredTag]) }));
		}
	}

	/// <summary>
	/// H11: Darius Crowley's Cannons (GILA_601, The Witchwood Monster Hunt). A Cannon is an untouchable board minion pinned to
	/// an edge of its owner's board (<see cref="EdgeTag"/>): Darius starts the game with one at each edge, and a minion that
	/// enters the board is placed between them (<see cref="Clamp"/>, called by <see cref="BoardZone"/>). Firing
	/// (<see cref="Fire"/>) makes each Cannon deal its damage to the enemy minions opposite it: the enemy board entities whose
	/// centred position is within half a slot of the Cannon's (one minion straight across, or the two either side of it).
	/// Every fire counts on the owner's <see cref="FiredTag"/> (GILA_612) and runs the fire hooks of the owner's Cannon
	/// treasures (GILA_602, GILA_603). All state is entity tags, so clones replay it.
	/// </summary>
	public static class Cannons
	{
		public const string CannonId = "GILA_601";
		/// <summary>On a Cannon: 1 left edge, 2 right edge.</summary>
		public const GameTag EdgeTag = (GameTag)34600;
		/// <summary>On a controller: how many times its Cannons have fired this game.</summary>
		public const GameTag FiredTag = (GameTag)34601;
		/// <summary>On a hero power: its last use fired Cannons that killed a minion (Fire!'s refresh, read by the Cannon trigger).</summary>
		public const GameTag RefreshPending = (GameTag)34602;
		public const int Left = 1, Right = 2;

		internal const string VictoriousRally = "GILA_602e2", TacticalReinforcements = "GILA_603e2", ExtraPowder = "GILA_604e2", DragonfireAmmo = "GILA_605e2";

		public static bool IsCannon(IPlayable entity) => entity is Minion m && m.Card.Id == CannonId;

		private static int Edge(IPlayable entity) => IsCannon(entity) ? entity[EdgeTag] : 0;

		/// <summary>The owner's Cannons, in board order.</summary>
		public static IReadOnlyList<Minion> Of(Controller c) => c.BoardZone.Slots.Where(IsCannon).Cast<Minion>().ToArray();

		/// <summary>
		/// The zone position a board entry takes when its owner has edge Cannons: a left Cannon at the far left, a right Cannon at
		/// the far right, anything else between the left and right Cannons. Unchanged for a board with no Cannon.
		/// </summary>
		internal static int Clamp(BoardZone zone, IPlayable entity, int position)
		{
			int count = zone.SlotCount;
			int edge = Edge(entity);
			if (edge == Left) return 0;
			if (edge == Right) return -1;
			if (count == 0) return position;
			int left = 0, right = 0;
			while (left < count && Edge(zone.GetSlot(left)) == Left) left++;
			while (right < count - left && Edge(zone.GetSlot(count - 1 - right)) == Right) right++;
			if (left == 0 && right == 0) return position;
			int last = count - right;
			if (position < 0 || position > last) return last;
			return Math.Max(left, position);
		}

		/// <summary>The damage each Cannon deals: the Cannon's printed value (TAG_SCRIPT_DATA_NUM_1), +1 per Dragonfire Ammo, +2 per
		/// Extra Powder this turn.</summary>
		public static int Damage(Controller c)
		{
			int printed = Cards.FromId(CannonId)[GameTag.TAG_SCRIPT_DATA_NUM_1];
			return Math.Max(1, printed) + Stacks(c, DragonfireAmmo) + 2 * Stacks(c, ExtraPowder);
		}

		private static int Stacks(Controller c, string enchantmentId) => c.AppliedEnchantments?.Count(e => e.Card.Id == enchantmentId) ?? 0;

		/// <summary>The enemy minions opposite <paramref name="cannon"/> (centred board positions within half a slot).</summary>
		public static IReadOnlyList<Minion> Opposite(Minion cannon)
		{
			var mine = cannon.Controller.BoardZone.Slots;
			var theirs = cannon.Controller.Opponent.BoardZone.Slots;
			int i = -1;
			for (int k = 0; k < mine.Count; k++) if (mine[k] == cannon) { i = k; break; }
			if (i < 0) return Array.Empty<Minion>();
			int x = 2 * i - (mine.Count - 1);
			var result = new List<Minion>();
			for (int j = 0; j < theirs.Count; j++)
				if (theirs[j] is Minion enemy && !enemy.Untouchable && !enemy.IsDead && !enemy.ToBeDestroyed
					&& Math.Abs(x - (2 * j - (theirs.Count - 1))) <= 1)
					result.Add(enemy);
			return result;
		}

		/// <summary>
		/// Fires <paramref name="c"/>'s Cannons: every shot is aimed first, then dealt in board order. Counts the fire, runs the
		/// fire hooks and returns how many enemy minions the shots killed. A Fire! use (<paramref name="fromPower"/>) whose shots
		/// killed marks the power for refresh.
		/// </summary>
		public static int Fire(Controller c, IPlayable source, bool fromPower)
		{
			Game game = c.Game;
			int damage = Damage(c);
			var shots = Of(c).SelectMany(cannon => Opposite(cannon).Select(target => (cannon, target))).ToList();
			foreach (var (cannon, target) in shots)
				if (!target.IsDead && target.Zone?.Type == Zone.PLAY) Generic.DamageCharFunc(cannon, target, damage, false);
			int kills = shots.Select(s => s.target).Distinct().Count(t => t.IsDead || t.ToBeDestroyed || t.Health <= 0);
			c[FiredTag] = c[FiredTag] + 1;
			Hooks(c, source);
			if (fromPower && kills > 0 && source is HeroPower power) power[RefreshPending] = 1;
			return kills;
		}

		// The fire hooks of the owner's Cannon treasures, once per stack, in that order.
		private static void Hooks(Controller c, IPlayable source)
		{
			Game game = c.Game;
			for (int n = Stacks(c, VictoriousRally); n > 0; n--)
				foreach (var minion in c.BoardZone.GetAll())
					Generic.AddEnchantmentBlock(game, Cards.FromId("GILA_602e3"), source ?? c.Hero, minion);
			for (int n = Stacks(c, TacticalReinforcements); n > 0; n--)
			{
				var hand = c.HandZone.ToArray();
				if (hand.Length == 0) break;
				Generic.AddEnchantmentBlock(game, Cards.FromId("GILA_603e3"), source ?? c.Hero, hand[game.Random.Next(hand.Length)]);
				game.OnRandomHappened(true);
			}
		}

		/// <summary>Readies Fire! again after a use whose shots killed (the Cannon's Inspire trigger).</summary>
		internal static void Refresh(Controller c)
		{
			var power = c.Hero?.HeroPower;
			if (power == null || power[RefreshPending] == 0) return;
			power[RefreshPending] = 0;
			power.IsExhausted = false;
		}

		/// <summary>Summons a Cannon at <paramref name="edge"/>; false when the board is full.</summary>
		public static bool Summon(Controller c, int edge, IPlayable source)
		{
			if (c.BoardZone.IsFull) return false;
			var cannon = (Minion)Entity.FromCard(c, Cards.FromId(CannonId));
			cannon[EdgeTag] = edge;
			Generic.SummonBlock(c.Game, cannon, edge == Left ? 0 : -1, source ?? c.Hero);
			return true;
		}

		/// <summary>GILA_610 Grizzled Reinforcement: one more Cannon, on the edge that has fewer (the left one on a tie).</summary>
		internal static void SummonExtra(Controller c, IPlayable source)
		{
			var cannons = Of(c);
			int left = cannons.Count(m => m[EdgeTag] == Left), right = cannons.Count(m => m[EdgeTag] == Right);
			Summon(c, left <= right ? Left : Right, source);
		}

		/// <summary>Darius's Start of Game (the hero portrait GILA_600h, marked by DungeonRunSetup.ApplyAfterStart): a Cannon at each edge.</summary>
		internal static void StartOfGame(IPlayable hero)
		{
			var c = hero.Controller;
			Summon(c, Left, hero);
			Summon(c, Right, hero);
		}
	}
}
