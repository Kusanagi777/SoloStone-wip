using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
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
		// ---------------------------------------------------------------- DungeonRunActives
		// The original Dungeon Run (Kobolds and Catacombs) active treasures (phase 6, family F2), keyed and shelled by their own
		// client ids, so the run deck, the treasure picker and the match show the real card; every name and text is the client's
		// own, word for word (facts/violet-hold-2026-09.json). Each is a card the run player holds in the run deck (an active
		// treasure row in run-v1.json); none ever enters a boss deck (DungeonRunAdmission admits them for the run player only).
		// Run reads go through RunHelpers (H1: bosses defeated), run writes through RunHelpers.EditAdd (H7: LOOTA_805, LOOTA_813),
		// the hidden-deck rewrite through RunHelpers.BeliefReset (H8: LOOTA_820). Every random choice uses the game RNG.

		/// <summary>LOOTA_822 Rod of Roasting: the most Pyroblasts it casts (the admission row's loop guard, "RepeatUntil max=60").</summary>
		public const int RodOfRoastingMaxCasts = 60;

		/// <summary>LOOTA_834 Gloves of Mugging: how many cards it steals.</summary>
		public const int GlovesOfMuggingSteals = 3;

		/// <summary>Quel'Delar's pieces (LOOTA_842a blade, LOOTA_842b hilt) and the weapon they form in hand (LOOTA_842).</summary>
		public const string QuelDelarBlade = "LOOTA_842a", QuelDelarHilt = "LOOTA_842b", QuelDelar = "LOOTA_842";

		/// <summary>
		/// An active treasure keyed and shelled by its own client id (phase 6): a spell, minion or weapon the run player holds in
		/// the run deck. <paramref name="name"/> and <paramref name="text"/> are the client's own (the run's name override shows them).
		/// </summary>
		private static RunContent Active(string id, string name, string text, int cost, Func<CardDef> script, Action<Card> shape = null,
			params string[] tokens) =>
			Add(new RunContent
			{
				Key = id, Kind = RunContentKind.ActiveTreasure, ShellId = id, Name = name, Text = text, Cost = cost, Tokens = tokens,
				Script = script,
				Shape = card =>
				{
					card.SetPrintedCost(cost);
					shape?.Invoke(card);
				},
			});

		private static CardDef Def(Power power, params PlayReq[] reqs) =>
			reqs.Length == 0 ? new CardDef(power) : new CardDef(Reqs(reqs), power);

		/// <summary>
		/// H9 loop guard: runs <paramref name="step"/> until <paramref name="stop"/> holds (checked before each step), at most
		/// <paramref name="max"/> times; a step that returns false ends the loop too. Returns the steps run.
		/// </summary>
		internal static int RepeatUntil(Func<bool> stop, int max, Func<bool> step)
		{
			int done = 0;
			while (done < max && !stop())
			{
				done++;
				if (!step()) break;
			}
			return done;
		}

		private static void DungeonRunActives()
		{
			// Enchantments the actives attach (client shells, the client's own names and texts).
			Effect("fx_LOOTA_812", "LOOTA_812e", "Boots of Haste Enchantment", "Your minions cost (0) this turn.",
				() => new Power
				{
					Aura = new Aura(AuraType.HAND, Effects.SetCost(0))
					{
						Condition = SelfCondition.IsMinion,
						RemoveTrigger = (TriggerType.TURN_END, null),
					}
				}, oneTurn: true);
			Buff("fx_LOOTA_820", "LOOTA_820e", "Mysterious Power", "Costs (2) less.", Effects.ReduceCost(2));
			Buff("fx_LOOTA_842b", "LOOTA_842be", "Armed?", "+3/+3.", Effects.AttackHealth_N(3));

			Active("LOOTA_805", "Amulet of Domination", "Take control of an enemy minion. Add it to your Dungeon Deck.", 2,
				() => Def(new Power { PowerTask = ComplexTask.Create(RunAddTarget(), new ControlTask(EntityType.TARGET)) },
					PlayReq.REQ_TARGET_TO_PLAY, PlayReq.REQ_MINION_TARGET, PlayReq.REQ_ENEMY_TARGET, PlayReq.REQ_NUM_MINION_SLOTS));
			Active("LOOTA_806", "Wand of Disintegration", "<b>Silence</b> and destroy all enemy minions.", 3,
				() => Def(new Power { PowerTask = ComplexTask.Create(new SilenceTask(EntityType.OP_MINIONS), new DestroyTask(EntityType.OP_MINIONS)) }));
			Active("LOOTA_811", "Orb of Destruction", "Destroy 2 of your opponent's Mana Crystals and they discard 2 cards.", 3,
				() => Def(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => OrbOfDestruction(g, c)) }));
			Active("LOOTA_812", "Boots of Haste", "Your minions cost (0) this turn.", 1,
				() => Def(new Power { PowerTask = new AddEnchantmentTask("LOOTA_812e", EntityType.CONTROLLER) }));
			Active("LOOTA_813", "Magic Mirror", "Choose a minion and summon a copy of it. Add it to your Dungeon_Deck.", 1,
				() => Def(new Power { PowerTask = ComplexTask.Create(RunAddTarget(), new SummonCopyTask(EntityType.TARGET)) },
					PlayReq.REQ_TARGET_TO_PLAY, PlayReq.REQ_MINION_TARGET, PlayReq.REQ_NUM_MINION_SLOTS));
			Active("LOOTA_814", "Wish", "Fill your board with <b>Legendary</b> minions. Fully heal your hero.", 10,
				() => Def(new Power { PowerTask = ComplexTask.Create(new CustomTask((g, c, s, t, stack) => FillWithLegendaries(g, c, s, stack)),
					new HealFullTask(EntityType.HERO)) }));
			// The Summoning Stone rule (LOE_086): the minion's Cost is the spell's last known Cost in hand.
			Active("LOOTA_816", "Party Portal", "Whenever you cast a spell, summon a random minion of the same Cost.", 1,
				() => Def(new Power
				{
					Trigger = new Trigger(TriggerType.CAST_SPELL)
					{
						TriggerSource = TriggerSource.FRIENDLY,
						SingleTask = ComplexTask.Create(
							new GetGameTagTask(GameTag.TAG_LAST_KNOWN_COST_IN_HAND, EntityType.TARGET),
							new RandomMinionNumberTask(GameTag.COST),
							new SummonTask())
					}
				}));
			Active("LOOTA_817", "Primordial Wand",
				"[x]<b>Adapt</b> a friendly minion.\nRepeat for each boss\nyou've defeated this run.@<b>Adapt</b> a friendly minion {0} |4(time, times).", 4,
				() =>
				{
					var adapt = new AdaptTask(EntityType.TARGET);
					return Def(new Power
					{
						PowerTask = new CustomTask((g, c, s, t, stack) =>
						{
							// Adapt once, then once more per boss defeated (H1); each Adapt is its own choice, in order.
							int times = 1 + RunHelpers.Bosses(g);
							for (int i = 0; i < times; i++) g.TaskQueue.Enqueue(adapt, c, s, t);
						})
					}, PlayReq.REQ_TARGET_TO_PLAY, PlayReq.REQ_MINION_TARGET, PlayReq.REQ_FRIENDLY_TARGET);
				});
			Active("LOOTA_819", "Archmage Staff", "[x]At the start of your turn,\nadd a random Mage\nspell to your hand.", 0,
				() => Def(new Power
				{
					Trigger = new Trigger(TriggerType.TURN_START)
					{
						TriggerSource = TriggerSource.FRIENDLY,
						SingleTask = new CustomTask((g, c, s, t, stack) => AddRandomMageSpell(g, c, s)),
					}
				}));
			Active("LOOTA_820", "Aleatoric Cube", "Shuffle both decks together and give\nhalf to each player.\nYour cards cost (2) less.", 2,
				() => Def(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => AleatoricCube(g, c, s)) }));
			// Keyword tags on the weapon: the hero attack limit reads an equipped weapon's Mega-Windfury (Hero.WeaponHasMegaWindfury,
			// CombatRules.AttackLimit) as it reads its Windfury, and Poisonous is the weapon's own (Character.PoisonousDamageSource).
			Active("LOOTA_821", "Vorpal Dagger", "<b>Poisonous</b>\n<b>Mega-Windfury</b>", 2,
				() => Def(new Power()),
				card =>
				{
					card.Tags[GameTag.POISONOUS] = 1;
					card.Tags[GameTag.WINDFURY] = 1;
					card.Tags[GameTag.MEGA_WINDFURY] = 1;
				});
			Active("LOOTA_822", "Rod of Roasting", "Cast 'Pyroblast' randomly until a_hero_dies.", 10,
				() => Def(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => RodOfRoasting(g, c, s, stack)) }), null, "EX1_279");
			Active("LOOTA_823", "Bag of Stuffing", "Draw cards until your hand is full.", 1,
				() => Def(new Power
				{
					PowerTask = new CustomTask((g, c, s, t, stack) => new EffectContext(g, c.Id, s.Id, stack: stack).DrawUntilHandFull())
				}));
			// Taunt, Can't Attack and Freeze are the client card's own tags (a Freeze minion freezes what it damages in combat).
			Active("LOOTA_826", "Portable Ice Wall", "<b>Taunt</b>. Can't Attack.\n <b>Freeze</b> any character damaged by this_minion.", 1,
				() => Def(new Power()),
				card =>
				{
					card.SetPrintedTaunt(true);
					card.Tags[GameTag.CANT_ATTACK] = 1;
					card.Tags[GameTag.FREEZE] = 1;
				});
			Active("LOOTA_827", "Embers of Ragnaros", "Shoot three fireballs at random enemies that deal $8 damage each.", 3,
				() => Def(new Power { PowerTask = new EnqueueTask(3, ComplexTask.DamageRandomTargets(1, EntityType.ENEMIES, 8, true)) }));
			// +1/+1 per boss defeated (H1), read from the run context the whole match shares, as an aura on the minion in play.
			Active("LOOTA_829", "Loyal Sidekick", "<b>Taunt</b>\nHas +1/+1 for each <b>Boss</b>_you've defeated this_run.", 1,
				() => Def(new Power
				{
					Aura = new MultiAura(
						new AdaptiveEffect(GameTag.ATK, EffectOperator.ADD, p => RunHelpers.Bosses(p.Game)),
						new AdaptiveEffect(GameTag.HEALTH, EffectOperator.ADD, p => RunHelpers.Bosses(p.Game)))
				}),
				card => card.SetPrintedTaunt(true));
			Active("LOOTA_830", "Shifting Hourglass", "[x]Take an extra turn.\nCosts (1) less for each \n<b>Boss</b> you've defeated\nthis run.", 7,
				() => Def(new Power
				{
					PowerTask = new ExtraTurnEffectTask(),
					Aura = new AdaptiveCostEffect(p => RunHelpers.Bosses(p.Game)),
				}));
			Active("LOOTA_834", "Gloves of Mugging", "Steal 3 cards from your opponent's hand.", 1,
				() => Def(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => GlovesOfMugging(g, c, s)) }));
			Active("LOOTA_835", "Greedy Pickaxe", "After your hero attacks, gain an empty Mana_Crystal.", 2,
				() => Def(new Power
				{
					Trigger = new Trigger(TriggerType.AFTER_ATTACK) { TriggerSource = TriggerSource.HERO, SingleTask = new ManaCrystalEmptyTask(1) }
				}));
			Active("LOOTA_836", "Bag of Coins", "Fill your hand with_Coins.", 0,
				() => Def(new Power
				{
					PowerTask = new CustomTask((g, c, s, t, stack) =>
					{
						var coin = Cards.FromId("GAME_005");
						RepeatUntil(() => c.HandZone.IsFull, Controller.MaxHandSize, () => Generic.AddHandPhase(c, Entity.FromCard(c, coin)));
					})
				}), null, "GAME_005");
			Active("LOOTA_837", "Horn of Cenarius", "<b>Recruit</b> 3 minions.", 2,
				() => Def(new Power { PowerTask = new RecruitTask(3) }));
			Active("LOOTA_838", "Dr. Boom's Boombox", "[x]Summon 7 'Boom Bots'.", 4,
				() => Def(new Power { PowerTask = new SummonTask("GVG_110t", 7) }), null, "GVG_110t");
			Active("LOOTA_839", "Scroll of Confusion", "Each minion randomly chooses a side of_the_battlefield.", 0,
				() => Def(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => ScrollOfConfusion(g, c, s)) }));
			Active("LOOTA_840", "Wax Rager", "<b>Deathrattle:</b> Resummon this minion.", 3,
				() => Def(new Power { DeathrattleTask = new SummonTask("LOOTA_840", SummonSide.DEATHRATTLE) }), null, "LOOTA_840");
			Active("LOOTA_841", "Portable Forge", "<b>Discover</b> a Legendary weapon and equip it.", 1,
				() =>
				{
					var equip = new WeaponTask();
					return Def(new Power
					{
						PowerTask = new CustomTask((g, c, s, t, stack) =>
						{
							var pool = ScriptedLegendaryWeapons(s);
							var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
							if (pool.Count > 0) ctx.Discover(ctx.SampleDistinct(pool, 3), ChoiceAction.STACK, equip);
						})
					});
				});
			// Quel'Delar (the evolving artifact, design section 4 kind A): the blade and the hilt are separate treasures; when both
			// are in the owner's hand they combine into Quel'Delar there (CombineInHand: on either entering the hand, and at the
			// start of the owner's turn for an opening hand that dealt both). Nothing about it is stored in the run.
			Active(QuelDelar, "Quel'Delar", "After your hero attacks, deal 6 damage to all_enemies.", 1,
				() => Def(new Power
				{
					Trigger = new Trigger(TriggerType.AFTER_ATTACK) { TriggerSource = TriggerSource.HERO, SingleTask = new DamageTask(6, EntityType.ENEMIES) }
				}));
			Active(QuelDelarBlade, "Blade of Quel'Delar", null, 1,
				() => Def(new Power { Trigger = CombineTrigger() }), null, QuelDelar);
			Active(QuelDelarHilt, "Hilt of Quel'Delar", "Give a minion +3/+3.", 1,
				() => Def(new Power { PowerTask = new AddEnchantmentTask("LOOTA_842be", EntityType.TARGET), Trigger = CombineTrigger() },
					PlayReq.REQ_TARGET_TO_PLAY, PlayReq.REQ_MINION_TARGET), null, QuelDelar);
			Active("LOOTA_843", "THE CANDLE", "Deal $4 damage to all enemy minions. Shuffle this into your deck.", 1,
				() => Def(new Power
				{
					PowerTask = ComplexTask.Create(new DamageTask(4, EntityType.OP_MINIONS, true), new AddCardTo("LOOTA_843", EntityType.DECK))
				}));
			Active("LOOTA_847", "Mask of Mimicry", "Choose a minion.\nMinions in your hand become copies of it.", 1,
				() => Def(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => MaskOfMimicry(c, t)) },
					PlayReq.REQ_TARGET_TO_PLAY, PlayReq.REQ_MINION_TARGET));
		}

		// ---------------------------------------------------------------- F2 shared pieces

		/// <summary>H7: the targeted minion's card joins the run deck after a won fight (LOOTA_805, LOOTA_813). The run layer checks it.</summary>
		private static ISimpleTask RunAddTarget() => new CustomTask((g, c, s, t, stack) =>
		{
			if (t is Minion) RunHelpers.EditAdd(g, t.Card.Id);
		});

		/// <summary>LOOTA_811: two fewer Mana Crystals for the opponent (never below 0), then two random cards of its hand are discarded.</summary>
		private static void OrbOfDestruction(Game g, Controller c)
		{
			var op = c.Opponent;
			Generic.ChangeManaCrystal(op, -2, true);
			var hand = op.HandZone.ToList();
			for (int i = 0; i < 2 && hand.Count > 0; i++)
			{
				var pick = hand[g.Random.Next(hand.Count)];
				hand.Remove(pick);
				if (pick.Zone == op.HandZone) Generic.DiscardBlock(op, pick);
			}
			g.OnRandomHappened(true);
		}

		/// <summary>
		/// LOOTA_814: summons random scripted Legendary minions (the engine's generation pool, run exclusions honoured) until the
		/// owner's board is full. A summon that fails ends it, so it cannot loop.
		/// </summary>
		private static void FillWithLegendaries(Game g, Controller c, IEntity s, TaskStack stack)
		{
			var pool = RandomCardTask.GetCardList(s, CardType.MINION, rarity: Rarity.LEGENDARY).Where(card => card.Implemented).ToList();
			if (pool.Count == 0) return;
			var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
			RepeatUntil(() => c.BoardZone.IsFull, 7, () => ctx.Summon(pool[g.Random.Next(pool.Count)].Id) != 0);
			g.OnRandomHappened(true);
		}

		/// <summary>LOOTA_819: a random scripted Mage spell of the engine's generation pool (run exclusions honoured) into the hand.</summary>
		private static void AddRandomMageSpell(Game g, Controller c, IEntity s)
		{
			var pool = RandomCardTask.GetCardList(s, CardType.SPELL, CardClass.MAGE).Where(card => card.Implemented).ToList();
			if (pool.Count == 0) return;
			Generic.AddHandPhase(c, Entity.FromCard(c, pool[g.Random.Next(pool.Count)]));
			g.OnRandomHappened(true);
		}

		/// <summary>LOOTA_841's Discover pool: the scripted Legendary weapons of the engine's generation pool (run exclusions honoured).</summary>
		internal static IReadOnlyList<Card> ScriptedLegendaryWeapons(IEntity source) =>
			RandomCardTask.GetCardList(source, CardType.WEAPON, rarity: Rarity.LEGENDARY).Where(card => card.Implemented && card.Type == CardType.WEAPON).ToList();

		/// <summary>
		/// LOOTA_820 Aleatoric Cube: both decks are pooled, shuffled with the game RNG and dealt back, the owner taking the larger
		/// half; the owner's cards (hand and new deck) then cost (2) less (LOOTA_820e). Cards that changed side change controller;
		/// both players' deck beliefs are reset (H8), and the deck moves are history facts, so a moved card is unknown to the side
		/// that did not hold it.
		/// </summary>
		private static void AleatoricCube(Game g, Controller c, IEntity s)
		{
			var op = c.Opponent;
			var pooled = new List<IPlayable>();
			foreach (var deck in new[] { c.DeckZone, op.DeckZone })
				foreach (var card in deck.ToArray()) pooled.Add(deck.Remove(card));
			for (int i = 0; i < pooled.Count; i++)
			{
				int r = g.Random.Next(i, pooled.Count);
				var swap = pooled[i]; pooled[i] = pooled[r]; pooled[r] = swap;
			}
			int mine = (pooled.Count + 1) / 2;
			for (int i = 0; i < pooled.Count; i++)
			{
				var card = pooled[i];
				var receiver = i < mine ? c : op;
				if (card.Controller != receiver) TransferController(card, receiver);
				receiver.DeckZone.Add(card);
				g.Facts.Shuffled(card);
			}
			g.OnRandomHappened(true);
			var discount = Cards.FromId("LOOTA_820e");
			foreach (var card in c.HandZone.ToArray().Concat(c.DeckZone.ToArray()))
				Generic.AddEnchantmentBlock(g, discount, (IPlayable)s, card);
			RunHelpers.BeliefReset(c, RunBeliefZones.Deck);
			RunHelpers.BeliefReset(op, RunBeliefZones.Deck);
		}

		// A hidden card changing side (the controller rule SwapRules uses for hands).
		private static void TransferController(IPlayable card, Controller receiver)
		{
			card.Controller = receiver;
			card[GameTag.CONTROLLER] = receiver.PlayerId;
			foreach (var actor in card.AppliedEnchantments?.ToArray() ?? Array.Empty<Enchantment>())
				actor.RebindController(receiver, refreshAuras: false);
			if (card.ChooseOnePlayables != null)
				foreach (var option in card.ChooseOnePlayables)
				{
					option.Zone?.Remove(option);
					option.Controller = receiver;
					option[GameTag.CONTROLLER] = receiver.PlayerId;
					receiver.SetasideZone.Add(option);
				}
		}

		/// <summary>
		/// LOOTA_834 Gloves of Mugging: up to three random cards of the opponent's hand move to the owner's, by the hand-to-hand
		/// theft rule Rat Burglar uses (costs kept, a full hand burns the overflow by the ordinary hand rule).
		/// </summary>
		private static void GlovesOfMugging(Game g, Controller c, IEntity s)
		{
			var op = c.Opponent;
			var hand = op.HandZone.ToList();
			for (int i = 0; i < GlovesOfMuggingSteals && hand.Count > 0; i++)
			{
				var stolen = hand[g.Random.Next(hand.Count)];
				hand.Remove(stolen);
				if (stolen.Zone != op.HandZone) continue;
				using var historyTransfer = g.Facts.CardTransfer(stolen, op.Id, Zone.HAND, s);
				using var temporaryTransfer = TemporaryRules.PreserveHandTransfer(stolen);
				if (stolen.OngoingEffect is AdaptiveCostEffect costEffect) costEffect.Remove();
				op.HandZone.RemoveForTrade(stolen);
				stolen.Controller = c;
				stolen[GameTag.CONTROLLER] = c.PlayerId;
				Generic.AddHandPhase(c, stolen);
			}
			g.OnRandomHappened(true);
		}

		/// <summary>
		/// LOOTA_839 Scroll of Confusion: every minion in play, the owner's board first then the opponent's (left to right), picks
		/// a side with the game RNG; a minion whose side changes moves there by the control rule (ControlTask), unless that board is
		/// full, when it stays.
		/// </summary>
		private static void ScrollOfConfusion(Game g, Controller c, IEntity s)
		{
			var minions = c.BoardZone.GetAll().Concat(c.Opponent.BoardZone.GetAll()).ToArray();
			var control = new ControlTask(EntityType.TARGET);
			foreach (var minion in minions)
			{
				var side = g.Random.Next(2) == 0 ? c : c.Opponent;
				if (minion.Zone?.Type != Zone.PLAY || minion.ToBeDestroyed || minion.Controller == side || side.BoardZone.IsFull) continue;
				control.Process(g, side, s, minion);
			}
			g.OnRandomHappened(true);
		}

		/// <summary>
		/// LOOTA_822 Rod of Roasting: casts Pyroblast (EX1_279) at a random legal target, again and again until a hero dies, at
		/// most <see cref="RodOfRoastingMaxCasts"/> times (H9; an Ice Block or an immune hero cannot make it loop).
		/// </summary>
		private static void RodOfRoasting(Game g, Controller c, IEntity s, TaskStack stack)
		{
			var pyroblast = Cards.FromId("EX1_279");
			var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
			RepeatUntil(() => HeroDown(c) || HeroDown(c.Opponent), RodOfRoastingMaxCasts, () =>
			{
				var spell = (Spell)Entity.FromCard(c, pyroblast);
				spell[GameTag.DISPLAYED_CREATOR] = s.Id;
				int? target = ctx.RandomLegalTarget(spell.Id);
				if (target == null) return false;
				ctx.CastSpell(spell.Id, HistoryCastKind.Effect, target);
				g.DeathProcessingAndAuraUpdate();
				return true;
			});
		}

		private static bool HeroDown(Controller c) =>
			c.Hero == null || c.Hero.ToBeDestroyed || c.Hero.Health <= 0 || c.PlayState != PlayState.PLAYING;

		/// <summary>LOOTA_847 Mask of Mimicry: every minion in the owner's hand becomes a copy of the chosen minion's card.</summary>
		private static void MaskOfMimicry(Controller c, IPlayable target)
		{
			if (!(target is Minion chosen)) return;
			foreach (var card in c.HandZone.ToArray())
				if (card is Minion && card.Zone == c.HandZone) Generic.ChangeEntityBlock(c, card, chosen.Card, true);
		}

		// Quel'Delar's in-hand combine (H3 CombineInHand): fires when the piece itself enters its owner's hand, and at the start
		// of its owner's turn. Not while the opening hand is dealt or mulliganed (OpeningDeal): two dealt pieces must stay two
		// opening cards (a merge there would shrink the hand and the mulligan choice), so the turn-start combine joins them
		// once the mulligan is over.
		private static Trigger CombineTrigger()
		{
			var combine = new CustomTask((g, c, s, t, stack) =>
			{
				if (OpeningDeal(g)) return;
				CombineInHand(c, QuelDelarBlade, QuelDelarHilt, QuelDelar, s);
			});
			return new MultiTrigger(
				new Trigger(TriggerType.ZONE) { TriggerActivation = TriggerActivation.HAND, TriggerSource = TriggerSource.SELF, SingleTask = combine },
				new Trigger(TriggerType.TURN_START) { TriggerActivation = TriggerActivation.HAND, TriggerSource = TriggerSource.FRIENDLY, SingleTask = combine });
		}

		/// <summary>True while the opening hand is dealt or mulliganed (the steps before MAIN_BEGIN).</summary>
		internal static bool OpeningDeal(Game g) =>
			g.Step == Step.BEGIN_FIRST || g.Step == Step.BEGIN_SHUFFLE || g.Step == Step.BEGIN_DRAW || g.Step == Step.BEGIN_MULLIGAN;

		/// <summary>
		/// H3 CombineInHand: when <paramref name="c"/>'s hand holds both <paramref name="firstId"/> and <paramref name="secondId"/>,
		/// the leftmost of each leaves the game (as a Shatter half does when it merges) and <paramref name="resultId"/> joins the
		/// hand. Returns the new card, or null when the hand does not hold both.
		/// </summary>
		internal static IPlayable CombineInHand(Controller c, string firstId, string secondId, string resultId, IEntity source)
		{
			var hand = c.HandZone;
			var first = hand.FirstOrDefault(p => p.Card.Id == firstId);
			var second = hand.FirstOrDefault(p => p.Card.Id == secondId);
			if (first == null || second == null) return null;
			int creator = (source as IPlayable)?.Id ?? first.Id;
			Retire(first);
			Retire(second);
			var result = Entity.FromCard(c, Cards.FromId(resultId), new EntityData { [GameTag.DISPLAYED_CREATOR] = creator });
			Generic.AddHandPhase(c, result);
			return result;
		}

		private static void Retire(IPlayable card)
		{
			var c = card.Controller;
			foreach (var enchant in card.AppliedEnchantments?.ToArray() ?? Array.Empty<Enchantment>()) enchant.Remove();
			c.SetasideZone.Add(c.HandZone.Remove(card));
			card[GameTag.ZONE] = (int)Zone.REMOVEDFROMGAME;
			if (c.Game.History) c.Game.PowerHistory.Add(new PowerHistoryHideEntity { EntityID = card.Id, Zone = Zone.REMOVEDFROMGAME });
		}
	}
}
