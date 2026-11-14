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
using SabberStoneCore.src.Loader;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.DungeonRun
{
	public static partial class DungeonRunContent
	{
		// ---------------------------------------------------------------- MonsterHuntShared
		// The Witchwood Monster Hunt treasures every hunter is offered (phase 6, family F5): the shared actives and passives and
		// the five evolving artifacts. Four are "upgrade this and shuffle it into your deck" chains (Coin Pouch GILA_816a-c,
		// Curio GILA_817-819, Mob GILA_821a-c, Militia Horn GILA_852a-c) and one is a minion that transforms itself (Tracker
		// GILA_851a-c). The evolution happens inside the match only (H3 Evolve, a transform): nothing about it is kept by the run,
		// so the run deck always holds the first stage. Every id is keyed and shelled by its own client id, so the run deck, the
		// treasure picker and the match show the real card; every name and text is the client's own, word for word
		// (facts/violet-hold-2026-09.json). The treasures are the run player's cards (DungeonRunAdmission admits them for the
		// player); none enters a boss deck. GILA_802 Cartographer discovers from the run's treasure pool (H2 DiscoverTreasure,
		// RunHelpers.TreasurePool). GILA_811, GILA_812 and GILA_827 copy cards out of the opponent's hidden hand or deck: the
		// copy is face down in the owner's hand (GILA_811, GILA_827) or a public summon (GILA_812), and the copied card's
		// owner learns nothing more than the client would show it. Every random choice uses the game RNG; every piece of state
		// is an entity tag or an enchantment, so clones and AI simulations replay it.

		/// <summary>GILA_820 Ricochet Shot: the hits it deals ("Deal $3 damage to a random character. Repeat this 12 times": the
		/// first hit and 12 repeats).</summary>
		public const int RicochetShotHits = 13;

		/// <summary>GILA_820 Ricochet Shot: the damage of each hit (before Spell Damage).</summary>
		public const int RicochetShotDamage = 3;

		/// <summary>GILA_806 Brass Lantern: how many Discovers it makes ("Repeat this 2 more times").</summary>
		public const int BrassLanternDiscovers = 3;

		/// <summary>GILA_810 Sneak Attack: how many minions it puts into the battlefield from the hand.</summary>
		public const int SneakAttackMinions = 2;

		/// <summary>GILA_853b Pristine Compass: how many extra copies of the drawn card it adds.</summary>
		public const int PristineCompassCopies = 2;

		/// <summary>The Coin (GILA_803 Gilnean Vigilante fills the hand with it).</summary>
		public const string TheCoin = "GAME_005";

		/// <summary>The Monster Hunt treasure-pool tag (DungeonRunTreasure.Pool) GILA_802 Cartographer discovers from.</summary>
		public const string MonsterHuntPool = "MonsterHunt";

		/// <summary>
		/// The evolving artifacts: each stage and the stage it becomes. A spell stage (Coin Pouch, Curio, Mob, Militia Horn)
		/// shuffles its next stage into the owner's deck after it is cast; a Tracker stage transforms into its next stage at the
		/// start of its owner's turn. The last stage of each chain has no entry.
		/// </summary>
		public static readonly IReadOnlyDictionary<string, string> EvolvesTo = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["GILA_816a"] = "GILA_816b", ["GILA_816b"] = "GILA_816c",
			["GILA_817"] = "GILA_818", ["GILA_818"] = "GILA_819",
			["GILA_821a"] = "GILA_821b", ["GILA_821b"] = "GILA_821c",
			["GILA_852a"] = "GILA_852b", ["GILA_852b"] = "GILA_852c",
			["GILA_851a"] = "GILA_851b", ["GILA_851b"] = "GILA_851c",
		};

		/// <summary>
		/// H2: the candidates of a treasure Discover for <paramref name="c"/>: the run's treasure pool narrowed to
		/// <paramref name="pool"/> (null: every adventure), scripted, that this game's generation may yield for the generating
		/// controller (a boss that holds the card through a hand swap never discovers a run treasure: the boss's generation
		/// rejects them), less <paramref name="exclude"/>; distinct, ordinal by id. Empty outside a run match.
		/// </summary>
		public static IReadOnlyList<Card> TreasureCandidates(Controller c, string pool, Func<Card, bool> filter = null, params string[] exclude)
		{
			Game game = c.Game;
			var list = new List<Card>();
			foreach (string id in RunHelpers.TreasurePool(game, pool))
			{
				if (Array.IndexOf(exclude, id) >= 0) continue;
				Card card = Cards.FromId(id);
				if (card == null || !card.Implemented || game.FiltersGenerated && game.BlocksGenerated(card)) continue;
				if (filter != null && !filter(card)) continue;
				list.Add(card);
			}
			return list.OrderBy(card => card.Id, StringComparer.Ordinal).ToArray();
		}

		/// <summary>H2: Discover <paramref name="count"/> treasures of <see cref="TreasureCandidates"/> into the hand; false when there is none.</summary>
		internal static bool DiscoverTreasure(Game g, Controller c, IEntity source, TaskStack stack, int count, string pool,
			Func<Card, bool> filter = null, params string[] exclude)
		{
			var candidates = TreasureCandidates(c, pool, filter, exclude);
			if (candidates.Count == 0) return false;
			var ctx = new EffectContext(g, c.Id, source.Id, stack: stack);
			return ctx.Discover(ctx.SampleDistinct(candidates, count), ChoiceAction.HAND);
		}

		/// <summary>
		/// H3: "Upgrade this and shuffle it into your deck": the next stage of <paramref name="source"/>'s chain
		/// (<see cref="EvolvesTo"/>) is shuffled into <paramref name="c"/>'s deck. False at the last stage or with a full deck.
		/// </summary>
		internal static bool Evolve(Controller c, IEntity source)
		{
			if (!EvolvesTo.TryGetValue(source.Card.Id, out string next)) return false;
			var upgraded = Entity.FromCard(c, Cards.FromId(next), new EntityData { [GameTag.DISPLAYED_CREATOR] = source.Id });
			return Generic.ShuffleIntoDeck(c, source, upgraded);
		}

		/// <summary>H3 for the Tracker: the minion transforms into its next stage (<see cref="EvolvesTo"/>) where it stands.</summary>
		private static void EvolveInPlace(Controller c, IEntity source)
		{
			if (!(source is Minion minion) || minion.Zone?.Type != Zone.PLAY || minion.ToBeDestroyed) return;
			if (!EvolvesTo.TryGetValue(minion.Card.Id, out string next)) return;
			Generic.TransformBlock(c, Cards.FromId(next), minion);
		}

		/// <summary>The scripted minions of <paramref name="cost"/> the generating controller's generation may yield, ordinal by id.</summary>
		private static IReadOnlyList<Card> RandomMinionPool(IEntity source, int cost) =>
			RandomCardTask.GetCardList(source, CardType.MINION)
				.Where(card => card.Implemented && card.Type == CardType.MINION && card.Cost == cost)
				.GroupBy(card => card.Id, StringComparer.Ordinal).Select(x => x.First())
				.OrderBy(card => card.Id, StringComparer.Ordinal).ToArray();

		/// <summary>GILA_816a-c: summons a random <paramref name="cost"/>-Cost minion; nothing with a full board or an empty pool.</summary>
		private static void SummonRandomOfCost(Game g, Controller c, IEntity s, int cost)
		{
			if (c.BoardZone.IsFull) return;
			var pool = RandomMinionPool(s, cost);
			if (pool.Count == 0) return;
			var minion = (Minion)Entity.FromCard(c, pool[g.Random.Next(pool.Count)]);
			g.OnRandomHappened(true);
			Generic.SummonBlock(g, minion, -1, s);
		}

		/// <summary>The enemy minions a random effect may pick: on the board, not untouchable, not already dying.</summary>
		private static List<Minion> LiveEnemyMinions(Controller c) =>
			c.Opponent.BoardZone.GetAll().Where(m => !m.Untouchable && !m.IsDead && !m.ToBeDestroyed).ToList();

		/// <summary>GILA_801 Gattling Gunner: the played card's Cost as damage to a random enemy minion.</summary>
		private static void GattlingGunner(Game g, Controller c, IEntity s, IPlayable played)
		{
			if (played == null || played.Controller != c || !(s is Minion gunner) || gunner.Zone?.Type != Zone.PLAY) return;
			int damage = played.Cost;
			if (damage <= 0) return;
			var targets = LiveEnemyMinions(c);
			if (targets.Count == 0) return;
			var target = targets[g.Random.Next(targets.Count)];
			g.OnRandomHappened(true);
			Generic.DamageCharFunc(gunner, target, damage, false);
		}

		/// <summary>GILA_803 Gilnean Vigilante: destroys the chosen minion, then Coins until the hand is full.</summary>
		private static void GilneanVigilante(Controller c, IEntity s, IPlayable t)
		{
			if (t is Minion minion && minion.Zone?.Type == Zone.PLAY) minion.Destroy();
			while (!c.HandZone.IsFull)
				if (!Generic.AddHandPhase(c, Entity.FromCard(c, Cards.FromId(TheCoin)))) break;
		}

		/// <summary>
		/// GILA_804 Holy Book: the chosen minion is silenced and destroyed; its owner's foe (the caster) summons a copy of it,
		/// silenced as the original was, with Attack and Health set to 10 (GILA_804e).
		/// </summary>
		private static void HolyBook(Game g, Controller c, IEntity s, IPlayable t)
		{
			if (!(t is Minion minion) || minion.Zone?.Type != Zone.PLAY) return;
			Card card = minion.Card;
			minion.Silence();
			minion.Destroy();
			if (c.BoardZone.IsFull) return;
			var copy = (Minion)Entity.FromCard(c, card, new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id });
			Generic.SummonBlock(g, copy, -1, s);
			if (copy.Zone?.Type != Zone.PLAY) return;
			copy.Silence();
			Generic.AddEnchantmentBlock(g, Cards.FromId("GILA_804e"), (IPlayable)s, copy);
		}

		/// <summary>GILA_806 Brass Lantern: three Discovers, each of three distinct cards of the owner's deck (by card id), copies to the hand.</summary>
		private static void BrassLantern(Game g, Controller c, IEntity s, TaskStack stack)
		{
			var deck = c.DeckZone.Select(e => e.Card).GroupBy(card => card.Id, StringComparer.Ordinal).Select(x => x.First())
				.OrderBy(card => card.Id, StringComparer.Ordinal).ToArray();
			if (deck.Length == 0) return;
			var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
			for (int i = 0; i < BrassLanternDiscovers; i++) ctx.Discover(ctx.SampleDistinct(deck, 3), ChoiceAction.HAND);
		}

		/// <summary>GILA_810 Sneak Attack: two random minions of the owner's hand are put into the battlefield (no Battlecry) with Charge.</summary>
		private static void SneakAttack(Game g, Controller c, IEntity s)
		{
			var minions = c.HandZone.OfType<Minion>().ToList();
			for (int i = 0; i < SneakAttackMinions && minions.Count > 0 && !c.BoardZone.IsFull; i++)
			{
				var pick = minions[g.Random.Next(minions.Count)];
				minions.Remove(pick);
				g.OnRandomHappened(true);
				c.HandZone.Remove(pick);
				Generic.SummonBlock(g, pick, -1, s);
				if (pick.Zone?.Type == Zone.PLAY) Generic.AddEnchantmentBlock(g, Cards.FromId("GILA_810e"), (IPlayable)s, pick);
			}
		}

		/// <summary>
		/// GILA_811 Spyglass: a copy of a random card of the opponent's hand (its enchantments kept, as a hand-to-hand copy is)
		/// goes face down to the owner's hand, costing (3) less (GILA_503e). The opponent sees a card enter that hand, never
		/// which (no public hand-entry identity: PublicHandKnowledge).
		/// </summary>
		private static void Spyglass(Game g, Controller c, IEntity s)
		{
			var hand = c.Opponent.HandZone.ToArray();
			if (hand.Length == 0 || c.HandZone.IsFull) return;
			var pick = hand[g.Random.Next(hand.Length)];
			g.OnRandomHappened(true);
			var copy = Generic.Copy(c, s, pick, Zone.HAND);
			if (copy?.Zone == c.HandZone) Generic.AddEnchantmentBlock(g, Cards.FromId("GILA_503e"), (IPlayable)s, copy);
		}

		/// <summary>GILA_812 Bestiary: until the owner's board is full, a copy of a random minion of the opponent's deck is summoned.</summary>
		private static void Bestiary(Game g, Controller c, IEntity s)
		{
			var minions = c.Opponent.DeckZone.OfType<Minion>().ToArray();
			if (minions.Length == 0) return;
			int guard = 0;
			while (!c.BoardZone.IsFull && guard++ < 7)
			{
				var pick = minions[g.Random.Next(minions.Length)];
				g.OnRandomHappened(true);
				if (Generic.Copy(c, s, pick, Zone.PLAY) == null) break;
			}
		}

		/// <summary>GILA_820 Ricochet Shot: <see cref="RicochetShotHits"/> hits of $3 at a random living character; stops when a hero dies.</summary>
		private static void RicochetShot(Game g, Controller c, IEntity s)
		{
			if (!(s is IPlayable spell)) return;
			RepeatUntil(() => c.Hero.Health <= 0 || c.Opponent.Hero.Health <= 0, RicochetShotHits, () =>
			{
				var targets = new List<ICharacter>();
				foreach (var side in new[] { c, c.Opponent })
				{
					if (!side.Hero.IsDead && !side.Hero.ToBeDestroyed && !side.Hero.IsImmune) targets.Add(side.Hero);
					targets.AddRange(side.BoardZone.GetAll().Where(m => !m.Untouchable && !m.IsDead && !m.ToBeDestroyed && !m.IsImmune));
				}
				if (targets.Count == 0) return false;
				var target = targets[g.Random.Next(targets.Count)];
				g.OnRandomHappened(true);
				Generic.DamageCharFunc(spell, target, RicochetShotDamage, true);
				return true;
			});
		}

		/// <summary>GILA_821a, GILA_821b: <paramref name="count"/> random enemy minions are destroyed.</summary>
		private static void DestroyRandomEnemies(Game g, Controller c, int count)
		{
			var minions = LiveEnemyMinions(c);
			for (int i = 0; i < count && minions.Count > 0; i++)
			{
				var pick = minions[g.Random.Next(minions.Count)];
				minions.Remove(pick);
				g.OnRandomHappened(true);
				pick.Destroy();
			}
		}

		/// <summary>GILA_825e's Deathrattle: the Top Hat (a new GILA_825e) moves to another random living friendly minion of the dead minion's owner.</summary>
		private static void TopHatMoves(Game g, Controller c, IEntity s)
		{
			var others = c.BoardZone.GetAll().Where(m => m.Id != s.Id && !m.Untouchable && !m.IsDead && !m.ToBeDestroyed).ToList();
			if (others.Count == 0) return;
			var pick = others[g.Random.Next(others.Count)];
			g.OnRandomHappened(true);
			Generic.AddEnchantmentBlock(g, Cards.FromId("GILA_825e"), s as IPlayable ?? c.Hero, pick);
		}

		/// <summary>GILA_853b Pristine Compass: draws a card; two copies of it, costing (0), go to the hand.</summary>
		private static void PristineCompass(Game g, Controller c, IEntity s)
		{
			var drawn = Generic.Draw(c);
			if (drawn == null) return;
			for (int i = 0; i < PristineCompassCopies && !c.HandZone.IsFull; i++)
				Generic.Copy(c, s, drawn, Zone.HAND, initialize: copy => copy.Cost = 0);
		}

		/// <summary>GILA_854 Beastly Beauty: after it attacks a minion and survives, it transforms into an 8/8 (GILA_854t).</summary>
		private static void BeastlyBeauty(Game g, Controller c, IEntity s)
		{
			if (!(s is Minion beauty) || beauty.Zone?.Type != Zone.PLAY || beauty.IsDead || beauty.ToBeDestroyed || beauty.Health <= 0) return;
			if (!(g.CurrentEventData?.EventTarget is Minion)) return;
			Generic.TransformBlock(c, Cards.FromId("GILA_854t"), beauty);
		}

		// GILA_821a, GILA_821b: playable only with an enemy minion in play, as Deadly Shot (the requirement's value is the minimum).
		private static Dictionary<PlayReq, int> MinimumEnemyMinion => new Dictionary<PlayReq, int> { [PlayReq.REQ_MINIMUM_ENEMY_MINIONS] = 1 };

		/// <summary>A minion stage's shape: its run cost and the client's printed stats.</summary>
		private static Action<Card> Stats(int attack, int health) => card => card.SetPrintedStats(attack, health);

		private static void MonsterHuntShared()
		{
			// ---------------- enchantments (client shells, the client's own names and texts)
			Buff("fx_GILA_804e", "GILA_804e", "Holy Essence", "Attack and Health set to 10.", Effects.SetAttackHealth(10));
			Buff("fx_GILA_805e2", "GILA_805e2", "Entrenched", "+2 Health.", Effects.Health_N(2));
			Effect("fx_GILA_805", "GILA_805e", "Entrenchment Player Enchantment", "Your minions have +2 Health.",
				() => new Power { Aura = new Aura(AuraType.BOARD, "GILA_805e2") });
			Buff("fx_GILA_810e", "GILA_810e", "Ambush!", "Has <b>Charge</b>.", Effects.Charge);
			Effect("fx_GILA_813", "GILA_813e", "Expedite Player Enchantment", "Your minions cost (1) less.",
				() => new Power { Aura = new Aura(AuraType.HAND, Effects.ReduceCost(1)) { Condition = SelfCondition.IsMinion } });
			Effect("fx_GILA_814", "GILA_814e", "Caltrops Player Enchantment", null,
				() => new Power
				{
					Trigger = Trig(TriggerType.AFTER_PLAY_MINION, ComplexTask.Create(
						new ConditionTask(EntityType.TARGET, SelfCondition.IsNotDead, SelfCondition.IsNotUntouchable),
						new FlagTask(true, new DamageTask(1, EntityType.TARGET))), TriggerSource.ENEMY)
				});
			Effect("fx_GILA_824e", "GILA_824e", "Royalty", "Increased stats.", () => new Power { Enchant = Enchants.Enchants.AddAttackHealthScriptTag });
			Effect("fx_GILA_825e", "GILA_825e", "Gentleman's Top Hat", "+2/+2. <b>Deathrattle</b>: Give your Top_Hat to another random friendly minion.",
				() => new Power
				{
					Enchant = new Enchant(Effects.AttackHealth_N(2)),
					DeathrattleTask = new CustomTask((g, c, s, t, stack) => TopHatMoves(g, c, s)),
				});
			Buff("fx_GILA_852ae", "GILA_852ae", "Morale Boost", "+1/+1 and <b>Taunt</b>.",
				Effects.Attack_N(1), Effects.Health_N(1), Effects.TauntEff);
			Buff("fx_GILA_852be", "GILA_852be", "Rallied", "+2/+2 and <b>Taunt</b>.",
				Effects.Attack_N(2), Effects.Health_N(2), Effects.TauntEff);
			Buff("fx_GILA_852ce", "GILA_852ce", "United", "+4/+4, <b>Taunt</b>, and <b>Divine Shield</b>.",
				Effects.Attack_N(4), Effects.Health_N(4), Effects.TauntEff, new SabberStoneCore.Enchants.Effect(GameTag.DIVINE_SHIELD, EffectOperator.SET, 1));

			// ---------------- tokens
			Active("GILA_817t", "Weeping Ghost", null, 1, () => new CardDef(), Stats(1, 1));
			Active("GILA_818t", "Weeping Ghost", null, 1, () => new CardDef(), Stats(2, 2));
			Active("GILA_819t", "Weeping Ghost", "<b>Lifesteal</b>", 1, () => new CardDef(), card =>
			{
				card.SetPrintedStats(3, 3);
				card.SetPrintedLifesteal(true);
			});
			Active("GILA_854t", "Beautiful Beast", null, 2, () => new CardDef(), Stats(8, 8));

			// ---------------- minions
			Active("GILA_801", "Gattling Gunner", "Whenever you play a card, deal damage equal to its Cost to a random enemy minion.", 4,
				() => Def(new Power
				{
					Trigger = new Trigger(TriggerType.PLAY_CARD)
					{
						TriggerSource = TriggerSource.FRIENDLY,
						SingleTask = new CustomTask((g, c, s, t, stack) => GattlingGunner(g, c, s, t)),
					}
				}), Stats(5, 3));
			Active("GILA_802", "Cartographer", "<b>Battlecry:</b> <b>Discover</b> a Treasure.", 2,
				() => Def(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => DiscoverTreasure(g, c, s, stack, 3, MonsterHuntPool, null, "GILA_802")) }),
				card =>
				{
					card.SetPrintedStats(1, 5);
					card.Tags[GameTag.BATTLECRY] = 1;
				});
			Active("GILA_803", "Gilnean Vigilante", "<b>Battlecry:</b> Destroy a\nminion and fill your\nhand with Coins.", 4,
				() => Def(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => GilneanVigilante(c, s, t)) },
					PlayReq.REQ_TARGET_IF_AVAILABLE, PlayReq.REQ_MINION_TARGET),
				card =>
				{
					card.SetPrintedStats(6, 3);
					card.Tags[GameTag.BATTLECRY] = 1;
				}, TheCoin);
			Active("GILA_826", "Nalaa the Redeemer", "[x]<b>Divine Shield</b>. <b>Taunt</b>.\nAfter this minion survives\n damage, gain <b>Divine Shield</b>.", 6,
				() => Def(new Power
				{
					Trigger = new Trigger(TriggerType.TAKE_DAMAGE)
					{
						TriggerSource = TriggerSource.SELF,
						Condition = SelfCondition.IsNotDead,
						SingleTask = new SetGameTagTask(GameTag.DIVINE_SHIELD, 1, EntityType.SOURCE),
					}
				}), Stats(9, 9));
			Active("GILA_827", "Murloc Holmes", "Whenever your opponent draws a card, add a copy to your hand.", 2,
				() => Def(new Power
				{
					Trigger = new Trigger(TriggerType.DRAW)
					{
						TriggerSource = TriggerSource.ENEMY,
						SingleTask = new CopyTask(EntityType.TARGET, Zone.HAND),
					}
				}), Stats(3, 4));
			Active("GILA_854", "Beastly Beauty", "[x]<b>Rush</b>\nAfter this attacks a minion\nand survives, transform\nthis into an 8/8.", 2,
				() => Def(new Power
				{
					Trigger = new Trigger(TriggerType.AFTER_ATTACK)
					{
						TriggerSource = TriggerSource.SELF,
						SingleTask = new CustomTask((g, c, s, t, stack) => BeastlyBeauty(g, c, s)),
					}
				}), card =>
				{
					card.SetPrintedStats(2, 5);
					card.SetPrintedRush(true);
				}, "GILA_854t");

			// The Tracker: at the start of its owner's turn it transforms into its next stage.
			Func<CardDef> tracker = () => Def(new Power
			{
				Trigger = new Trigger(TriggerType.TURN_START) { SingleTask = new CustomTask((g, c, s, t, stack) => EvolveInPlace(c, s)) }
			});
			Active("GILA_851a", "Gilnean Tracker", "[x]<b>Rush</b>\nAt the start of your turn,\ntransform this into a 5/5.", 3, tracker,
				card => { card.SetPrintedStats(3, 3); card.SetPrintedRush(true); }, "GILA_851b");
			Active("GILA_851b", "Transforming Tracker", "<b>Rush</b>\nAt the start of your turn, transform this into a 7/7 with <b>Windfury</b>.", 3, tracker,
				card => { card.SetPrintedStats(5, 5); card.SetPrintedRush(true); }, "GILA_851c");
			Active("GILA_851c", "Worgen Tracker", "<b>Rush</b>\n<b>Windfury</b>", 3, () => new CardDef(),
				card => { card.SetPrintedStats(7, 7); card.SetPrintedRush(true); card.Tags[GameTag.WINDFURY] = 1; });

			// ---------------- spells
			Active("GILA_804", "Holy Book", "<b>Silence</b> and destroy a minion. Summon a 10/10 copy of it.", 6,
				() => Def(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => HolyBook(g, c, s, t)) },
					PlayReq.REQ_TARGET_TO_PLAY, PlayReq.REQ_MINION_TARGET));
			Active("GILA_806", "Brass Lantern", "<b>Discover</b> a copy of a card in your deck. Repeat this 2 more times.", 1,
				() => Def(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => BrassLantern(g, c, s, stack)) }));
			Active("GILA_810", "Sneak Attack", "Put 2 random minions from your hand into the battlefield and give them <b>Charge</b>.", 3,
				() => Def(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => SneakAttack(g, c, s)) }));
			Active("GILA_811", "Spyglass", "<b>Echo</b>\nPut a copy of a random card in your opponent's hand into yours. It costs (3) less.", 2,
				() => Def(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => Spyglass(g, c, s)) }));
			Active("GILA_812", "Bestiary", "[x]Fill your board with\ncopies of random minions\nfrom your opponent's deck.", 8,
				() => Def(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => Bestiary(g, c, s)) }));
			Active("GILA_820", "Ricochet Shot", "Deal $3 damage to a random character. Repeat this 12 times.", 5,
				() => Def(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => RicochetShot(g, c, s)) }));
			Active("GILA_824", "Royal Gift", "Give a minion +2/+2 for each minion you control.", 4,
				() => Def(new Power
				{
					PowerTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (!(t is Minion target) || target.Zone?.Type != Zone.PLAY) return;
						int n = 2 * c.BoardZone.CountExceptUntouchables;
						if (n > 0) Generic.AddEnchantmentBlock(g, Cards.FromId("GILA_824e"), (IPlayable)s, target, n, n);
					})
				}, PlayReq.REQ_TARGET_TO_PLAY, PlayReq.REQ_MINION_TARGET));
			Active("GILA_825", "Gentleman's Top Hat", "[x]Give a minion +2/+2\nand \"<b>Deathrattle:</b> Give\nyour Top Hat to another\nrandom friendly minion.\"", 1,
				() => Def(new Power { PowerTask = new AddEnchantmentTask("GILA_825e", EntityType.TARGET) },
					PlayReq.REQ_TARGET_TO_PLAY, PlayReq.REQ_MINION_TARGET));
			Active("GILA_850b", "Hallowed Water", "Restore your hero\nto full Health.\n Draw 5 cards.", 7,
				() => Def(new Power
				{
					PowerTask = ComplexTask.Create(
						new CustomTask((g, c, s, t, stack) =>
						{
							int missing = c.Hero.Damage;
							if (missing > 0) new EffectContext(g, c.Id, s.Id, stack: stack).Heal(c.Hero.Id, missing);
						}),
						new DrawTask(5))
				}));
			Active("GILA_853b", "Pristine Compass", "Draw a card and add 2 extra copies of it to your hand. They cost (0).", 3,
				() => Def(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => PristineCompass(g, c, s)) }));

			// ---------------- evolving artifacts: upgrade this and shuffle it into your deck
			ISimpleTask Then(Action<Game, Controller, IEntity, IPlayable> effect) =>
				new CustomTask((g, c, s, t, stack) => { effect(g, c, s, t); Evolve(c, s); });
			Active("GILA_816a", "Coin Pouch", "Summon a random 3-Cost minion. Upgrade this and shuffle it into your deck.", 2,
				() => Def(new Power { PowerTask = Then((g, c, s, t) => SummonRandomOfCost(g, c, s, 3)) }), null, "GILA_816b");
			Active("GILA_816b", "Sack of Coins", "Summon a random 6-Cost minion. Upgrade this and shuffle it into your deck.", 2,
				() => Def(new Power { PowerTask = Then((g, c, s, t) => SummonRandomOfCost(g, c, s, 6)) }), null, "GILA_816c");
			Active("GILA_816c", "Hefty Sack of Coins", "Summon a random 3, 6, and 9-Cost minion.", 3,
				() => Def(new Power
				{
					PowerTask = new CustomTask((g, c, s, t, stack) =>
					{
						foreach (int cost in new[] { 3, 6, 9 }) SummonRandomOfCost(g, c, s, cost);
					})
				}));
			Active("GILA_817", "Creepy Curio", "Summon three 1/1 Ghosts. Upgrade this and shuffle it into your deck.", 1,
				() => Def(new Power { PowerTask = ComplexTask.Create(new SummonTask("GILA_817t", 3), new CustomTask((g, c, s, t, stack) => Evolve(c, s))) }),
				null, "GILA_817t", "GILA_818");
			Active("GILA_818", "Haunted Curio", "Summon three 2/2 Ghosts. Upgrade this and shuffle it into your deck.", 1,
				() => Def(new Power { PowerTask = ComplexTask.Create(new SummonTask("GILA_818t", 3), new CustomTask((g, c, s, t, stack) => Evolve(c, s))) }),
				null, "GILA_818t", "GILA_819");
			Active("GILA_819", "Cursed Curio", "Summon three 3/3 Ghosts with <b>Lifesteal</b>.", 1,
				() => Def(new Power { PowerTask = new SummonTask("GILA_819t", 3) }), null, "GILA_819t");
			Active("GILA_821a", "Surly Mob", "Destroy a random enemy minion. Upgrade this and shuffle it into your deck.", 2,
				() => new CardDef(MinimumEnemyMinion, new Power { PowerTask = Then((g, c, s, t) => DestroyRandomEnemies(g, c, 1)) }),
				null, "GILA_821b");
			Active("GILA_821b", "Angry Mob", "Destroy 2 random enemy minions. Upgrade this and shuffle it into your deck.", 2,
				() => new CardDef(MinimumEnemyMinion, new Power { PowerTask = Then((g, c, s, t) => DestroyRandomEnemies(g, c, 2)) }),
				null, "GILA_821c");
			Active("GILA_821c", "Crazed Mob", "<b>Silence</b> and destroy all enemy minions.", 2,
				() => Def(new Power { PowerTask = ComplexTask.Create(new SilenceTask(EntityType.OP_MINIONS), new DestroyTask(EntityType.OP_MINIONS)) }));
			Active("GILA_852a", "Old Militia Horn", "Give your minions +1/+1 and <b>Taunt</b>. Upgrade this and shuffle it into your deck.", 2,
				() => Def(new Power { PowerTask = ComplexTask.Create(new AddEnchantmentTask("GILA_852ae", EntityType.MINIONS), new CustomTask((g, c, s, t, stack) => Evolve(c, s))) }),
				null, "GILA_852b");
			Active("GILA_852b", "Militia Horn", "Give your minions +2/+2 and <b>Taunt</b>. Upgrade this and shuffle it into your deck.", 2,
				() => Def(new Power { PowerTask = ComplexTask.Create(new AddEnchantmentTask("GILA_852be", EntityType.MINIONS), new CustomTask((g, c, s, t, stack) => Evolve(c, s))) }),
				null, "GILA_852c");
			Active("GILA_852c", "Veteran's Militia Horn", "Give your minions +4/+4, <b>Taunt</b>, and <b>Divine Shield</b>.", 2,
				() => Def(new Power { PowerTask = new AddEnchantmentTask("GILA_852ce", EntityType.MINIONS) }));

			// ---------------- passives
			Treasure("GILA_805", "Entrenchment", "<b>Passive</b>\nYour minions have +2_Health.", effect: "fx_GILA_805", ownShell: true);
			Treasure("GILA_813", "Expedite", "<b>Passive</b>\nYour minions cost\n(1) less.", effect: "fx_GILA_813", ownShell: true);
			Treasure("GILA_814", "Caltrops", "<b>Passive</b>\nAfter your opponent\nplays a minion, deal 1\ndamage to it.", effect: "fx_GILA_814", ownShell: true);
		}
	}
}
