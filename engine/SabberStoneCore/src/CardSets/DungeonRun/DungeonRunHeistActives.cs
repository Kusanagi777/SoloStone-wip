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
		// ---------------------------------------------------------------- HeistActives
		// The Dalaran Heist active treasures (phase 6, family F7), keyed and shelled by their own client ids, so the run deck, the
		// treasure picker and the match show the real card; every name and text is the client's own, word for word
		// (facts/violet-hold-2026-09.json). They are the run player's cards (DungeonRunAdmission admits them for the player); none
		// enters a boss deck, except Fly-By's Kadoom Bot, which it shuffles into the opponent's deck. Evolving cards stay in the
		// match (THE... Candles? walks its Twinspell stages DALA_714 -> 714a -> 714b -> 714c through the engine's Twinspell rule;
		// Master Scheme upgrades each turn in hand through the Scheme rule). The run reads and writes go through RunHelpers: the
		// Heist treasure pool (H2: DALA_701, DALA_708), the slot logs (H7: DALA_718's spells, DALA_719's kills, kept after a won
		// fight), the run deck replace (H7: DALA_721) and the belief reset (H8: DALA_709). Every random choice uses the game RNG;
		// every piece of match state is an entity tag, an enchantment or the turn queue, so clones and AI simulations replay it.

		/// <summary>The Dalaran Heist treasure-pool tag (DungeonRunTreasure.Pool) DALA_701 and DALA_708 draw from.</summary>
		public const string HeistPool = "Heist";

		/// <summary>DALA_716 Fly-By's card: the Kadoom Bot, shuffled into the opponent's deck; it casts when drawn.</summary>
		public const string KadoomBot = "DALA_716t";
		/// <summary>The Kadoom Bot's damage to whoever draws it.</summary>
		public const int KadoomBotDamage = 50;

		/// <summary>DALA_704 Super Simian Sphere's minion: King Mukla.</summary>
		public const string KingMukla = "EX1_014";

		/// <summary>DALA_722 Annoy-o Horn's annoying minion: Annoy-o-Tron (Taunt, Divine Shield).</summary>
		public const string AnnoyoTron = "GVG_085";

		/// <summary>DALA_726 Master Scheme's minion: the Boom Bot.</summary>
		public const string BoomBot = "GVG_110t";

		/// <summary>DALA_703 The Muscle: how many cards it adds.</summary>
		public const int TheMuscleCards = 3;
		/// <summary>DALA_708 Untold Splendor: how many treasures it shuffles.</summary>
		public const int UntoldSplendorTreasures = 5;
		/// <summary>DALA_711 Dreamgrove Ring: the copies it summons of the discovered minion.</summary>
		public const int DreamgroveCopies = 5;
		/// <summary>DALA_712 Orb of the Untold: the damage it splits (one point per hit, as Avenging Wrath).</summary>
		public const int OrbOfTheUntoldDamage = 20;

		/// <summary>
		/// DALA_718 Elistra the Immortal: the most spells one Battlecry recasts (the admission row's loop guard,
		/// "RepeatUntil max=60"): the run log grows with every won fight, so the guard bounds a long run.
		/// </summary>
		public const int ElistraMaxRecasts = 60;
		/// <summary>DALA_719 Soulreaper's Scythe: the most minions one Deathrattle reads from the log (the board bounds the summons).</summary>
		public const int ScytheMaxSummons = 60;

		/// <summary>THE... Candles? stages: each Twinspell stage and the copy it leaves in hand (the client's TWINSPELL_COPY).</summary>
		public static readonly IReadOnlyDictionary<string, string> CandleStages = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["DALA_714"] = "DALA_714a", ["DALA_714a"] = "DALA_714b", ["DALA_714b"] = "DALA_714c",
		};

		/// <summary>
		/// DALA_717 Case Study's Quest Rewards: the rewards of the Un'Goro quests, the only Quest Rewards when the Heist shipped
		/// (each quest's QUEST_REWARD_DATABASE_ID). The Discover offers the scripted ones only (<see cref="QuestRewardCandidates"/>).
		/// </summary>
		public static readonly IReadOnlyList<string> QuestRewards = new[]
		{
			"UNG_028t", "UNG_067t1", "UNG_116t", "UNG_829t1", "UNG_920t1", "UNG_934t1", "UNG_940t8", "UNG_942t", "UNG_954t1",
		};

		/// <summary>The 31 Heist active treasure ids (the family's ids), in the client's order.</summary>
		public static readonly IReadOnlyList<string> HeistActiveIds = new[]
		{
			"DALA_700", "DALA_701", "DALA_702", "DALA_703", "DALA_704", "DALA_705", "DALA_706", "DALA_708", "DALA_709", "DALA_711",
			"DALA_712", "DALA_713", "DALA_714", "DALA_714a", "DALA_714b", "DALA_714c", "DALA_715", "DALA_716", "DALA_717", "DALA_718",
			"DALA_719", "DALA_720", "DALA_721", "DALA_722", "DALA_723", "DALA_724", "DALA_725", "DALA_726", "DALA_727", "DALA_729",
			"DALA_730",
		};

		/// <summary>The scripted Quest Rewards (<see cref="QuestRewards"/>) this game's generation may give <paramref name="c"/>, ordinal by id.</summary>
		public static IReadOnlyList<Card> QuestRewardCandidates(Controller c)
		{
			Game game = c.Game;
			var list = new List<Card>();
			using (game.GenerateFor(c))
				foreach (string id in QuestRewards)
				{
					Card card = Cards.FromId(id);
					if (card == null || !card.Implemented || game.FiltersGenerated && game.BlocksGenerated(card)) continue;
					list.Add(card);
				}
			return list.OrderBy(card => card.Id, StringComparer.Ordinal).ToArray();
		}

		/// <summary>The scripted Legendary minions of the generation pool <paramref name="receiver"/> may be given, ordinal by id.</summary>
		internal static List<Card> ScriptedLegendaryMinions(Game g, Controller receiver, IEntity source)
		{
			using (g.GenerateFor(receiver))
				return RandomCardTask.GetCardList(source, CardType.MINION, rarity: Rarity.LEGENDARY)
					.Where(card => card.Implemented && card.Type == CardType.MINION && card.Rarity == Rarity.LEGENDARY)
					.GroupBy(card => card.Id, StringComparer.Ordinal).Select(x => x.First()).OrderBy(card => card.Id, StringComparer.Ordinal).ToList();
		}

		/// <summary>DALA_703 The Muscle's pool: the scripted cards of the generation pool (any class; no hero power or enchantment).</summary>
		private static List<Card> ScriptedRandomCards(Game g, Controller receiver, IEntity source)
		{
			using (g.GenerateFor(receiver))
				return RandomCardTask.GetCardList(source).Where(card => card.Implemented && card.Type != CardType.HERO_POWER
						&& card.Type != CardType.ENCHANTMENT && card.Type != CardType.INVALID)
					.GroupBy(card => card.Id, StringComparer.Ordinal).Select(x => x.First()).OrderBy(card => card.Id, StringComparer.Ordinal).ToList();
		}

		/// <summary>
		/// DALA_700 Continuum Collider: the owner takes an extra turn, then the opponent takes its turn and an extra turn after it
		/// (the turn queue: owner, opponent, opponent, then the usual alternation). Both extra turns are granted turns (history
		/// facts), as Shifting Hourglass's.
		/// </summary>
		private static void ContinuumCollider(Game g, Controller c, IEntity s)
		{
			long mine = g.Facts.AllocateOperation(), theirs = g.Facts.AllocateOperation();
			TurnQueueRules.PrependGranted(g, c.Opponent, theirs);
			TurnQueueRules.Prepend(g, c.Opponent);
			TurnQueueRules.PrependGranted(g, c, mine);
			g.Facts.ExtraTurnGranted(mine, c, s);
			g.Facts.ExtraTurnGranted(theirs, c.Opponent, s);
		}

		/// <summary>
		/// DALA_701 The Box's turn in hand: the card (<paramref name="type"/>: The Box itself, or the card its Sifting enchantment is
		/// on) becomes a random treasure of the run's Heist pool (H2, never The Box), then Sifting (DALA_701e) goes on the new card
		/// so it transforms again next turn. Outside a run match the pool is empty and it stays.
		/// </summary>
		private static ISimpleTask Sift(EntityType type) => new CustomTask((g, c, s, t, stack) =>
		{
			foreach (IPlayable card in IncludeTask.GetEntities(type, in c, s, t, stack?.Playables).ToArray())
			{
				if (card.Zone?.Type != Zone.HAND) continue;
				var options = TreasureCandidates(card.Controller, HeistPool, null, "DALA_701");
				if (options.Count == 0) continue;
				// A change to another card type makes a new entity (Generic.ChangeEntity): Sifting goes on the one returned.
				var changed = Generic.ChangeEntityBlock(card.Controller, card, options[g.Random.Next(options.Count)], true);
				Generic.AddEnchantmentBlock(g, Cards.FromId("DALA_701e"), changed, changed);
				g.OnRandomHappened(true);
			}
		});

		/// <summary>DALA_703 The Muscle: three random scripted cards of the generation pool into the hand, each costing (0).</summary>
		private static void TheMuscle(Game g, Controller c, IEntity s)
		{
			var pool = ScriptedRandomCards(g, c, s);
			if (pool.Count == 0) return;
			for (int i = 0; i < TheMuscleCards; i++)
			{
				var card = Entity.FromCard(c, pool[g.Random.Next(pool.Count)], new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id });
				card.Cost = 0;
				g.OnRandomHappened(true);
				Generic.AddHandPhase(c, card);
			}
		}

		/// <summary>DALA_704 Super Simian Sphere: King Mukla, Immune and untargetable by spells and Hero Powers.</summary>
		private static void SuperSimianSphere(Game g, Controller c, IEntity s, TaskStack stack)
		{
			int id = new EffectContext(g, c.Id, s.Id, stack: stack).Summon(KingMukla);
			if (id == 0 || !(g.IdEntityDic[id] is Minion mukla)) return;
			mukla[GameTag.IMMUNE] = 1;
			mukla[GameTag.CANT_BE_TARGETED_BY_SPELLS] = 1;
			mukla[GameTag.CANT_BE_TARGETED_BY_HERO_POWERS] = 1;
		}

		/// <summary>DALA_708 Untold Splendor: five random treasures of the run's Heist pool (never itself) shuffled into the deck.</summary>
		private static void UntoldSplendor(Game g, Controller c, IEntity s)
		{
			var pool = TreasureCandidates(c, HeistPool, null, "DALA_708");
			if (pool.Count == 0) return;
			for (int i = 0; i < UntoldSplendorTreasures; i++)
			{
				var card = Entity.FromCard(c, pool[g.Random.Next(pool.Count)], new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id });
				g.OnRandomHappened(true);
				if (!Generic.ShuffleIntoDeck(c, s, card)) break;
			}
		}

		/// <summary>
		/// DALA_709 Golden Candle: every other card of the owner's hand and deck becomes a random scripted Legendary minion of the
		/// generation pool, then the owner's hand and deck beliefs are reset (H8: what the opponent learned about them is void).
		/// </summary>
		private static void GoldenCandle(Game g, Controller c, IEntity s)
		{
			var pool = ScriptedLegendaryMinions(g, c, s);
			if (pool.Count == 0) return;
			foreach (var card in c.HandZone.ToArray().Concat(c.DeckZone.ToArray()))
			{
				if (card.Id == s.Id) continue;
				Generic.ChangeEntityBlock(c, card, pool[g.Random.Next(pool.Count)], true);
			}
			g.OnRandomHappened(true);
			RunHelpers.BeliefReset(c, RunBeliefZones.HandAndDeck);
		}

		/// <summary>
		/// DALA_715 Overpowered (DALA_715e's trigger): the card just played (<paramref name="played"/>) is replayed once: a fresh
		/// copy is played automatically, targets and modes chosen with the game RNG, a minion's Battlecry included (AutoPlayRules).
		/// An automatic play is not a play, so the copy never triggers it again; Overpowered itself is never replayed.
		/// </summary>
		private static void Overpowered(Game g, Controller c, IEntity s, IPlayable played)
		{
			if (played == null || played.Controller != c || g.State == State.COMPLETE) return;
			if (s is Enchantment e && e.Creator?.Id == played.Id) return;
			if (played.Card.Type == CardType.HERO_POWER || played.Card.Type == CardType.ENCHANTMENT) return;
			if (c.Choice != null || g.Resolution.HasSuspendedEffect) return;
			var replay = Entity.FromCard(c, played.Card, new EntityData { [GameTag.DISPLAYED_CREATOR] = played.Id }, c.SetasideZone);
			AutoPlayRules.Play(replay, s, battlecry: true);
		}

		/// <summary>
		/// DALA_718 Elistra the Immortal's Battlecry: each spell of its run log (the spells its owner cast on it in earlier won
		/// fights, oldest first) is cast again on it, as an effect cast; a spell that cannot target it now is skipped. At most
		/// <see cref="ElistraMaxRecasts"/> (H9).
		/// </summary>
		private static void ElistraRecast(Game g, Controller c, IEntity s, TaskStack stack)
		{
			if (!(s is Minion elistra)) return;
			var log = RunHelpers.SlotLog(elistra);
			var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
			int next = 0;
			RepeatUntil(() => next >= log.Count || g.State == State.COMPLETE, ElistraMaxRecasts, () =>
			{
				Card card = Cards.FromId(log[next++]);
				if (card == null || !card.Implemented || card.Type != CardType.SPELL) return true;
				if (elistra.Zone?.Type != Zone.PLAY || elistra.ToBeDestroyed) return false;
				var spell = (Spell)Entity.FromCard(c, card, new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id });
				int? target = ctx.RandomLegalTarget(spell.Id, new[] { elistra.Id });
				if (target == null && card.TargetingType != TargetingType.None) return true;
				ctx.CastSpell(spell.Id, HistoryCastKind.Effect, target);
				g.DeathProcessingAndAuraUpdate();
				return true;
			});
		}

		/// <summary>DALA_719 Soulreaper's Scythe: after its hero attacks, a minion the attack killed joins the weapon's run log.</summary>
		private static void ScytheKill(Game g, Controller c, IEntity s)
		{
			if (!(s is Weapon scythe) || c.Hero.Weapon != scythe) return;
			if (!(g.CurrentEventData?.EventTarget is Minion victim)) return;
			if (victim.IsDead || victim.ToBeDestroyed || victim.Health <= 0) RunHelpers.AppendSlotLog(scythe, victim.Card.Id);
		}

		/// <summary>
		/// DALA_719 Soulreaper's Scythe's Deathrattle: every minion of its run log (earlier won fights) and of this fight's kills,
		/// in order, is summoned for its owner until the board is full.
		/// </summary>
		private static void ScytheSummons(Game g, Controller c, IEntity s, TaskStack stack)
		{
			if (!(s is IPlayable scythe)) return;
			var killed = RunHelpers.SlotLogThisRun(scythe);
			var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
			int next = 0;
			RepeatUntil(() => next >= killed.Count || c.BoardZone.IsFull || g.State == State.COMPLETE, ScytheMaxSummons, () =>
			{
				Card card = Cards.FromId(killed[next++]);
				if (card == null || !card.Implemented || card.Type != CardType.MINION) return true;
				ctx.Summon(card.Id);
				return true;
			});
		}

		/// <summary>
		/// DALA_720 Swampqueen's Call: each friendly minion becomes a random scripted Legendary minion of the generation pool;
		/// "Repeatable this turn": a Ghostly copy goes to the hand and is discarded at the end of the turn (the Witch's Brew rule).
		/// </summary>
		private static void SwampqueensCall(Game g, Controller c, IEntity s)
		{
			var pool = ScriptedLegendaryMinions(g, c, s);
			if (pool.Count > 0)
			{
				foreach (var minion in c.BoardZone.GetAll().Where(m => !m.Untouchable && !m.ToBeDestroyed).ToArray())
					Generic.TransformBlock(c, pool[g.Random.Next(pool.Count)], minion);
				g.OnRandomHappened(true);
			}
			if (c.HandZone.IsFull) return;
			IPlayable echo = Entity.FromCard(in c, s.Card, new EntityData { { GameTag.GHOSTLY, 1 } }, c.HandZone);
			echo[GameTag.DISPLAYED_CREATOR] = s.Id;
			g.AuraUpdate();
			g.GhostlyCards.Add(echo.Id);
		}

		/// <summary>
		/// DALA_721 Duplatransmogrifier: after a won fight, every minion of the run deck (not a treasure or signature card) becomes
		/// a copy of the chosen friendly minion's card (H7 replace edits; the run layer applies them all or none).
		/// </summary>
		private static void Duplatransmogrifier(Game g, Controller c, IPlayable t)
		{
			if (!(t is Minion chosen) || c.PlayerId != g.Player1.PlayerId) return;
			var run = RunHelpers.Context(g);
			for (int slot = 0; slot < run.RunDeckIds.Count; slot++)
			{
				string origin = slot < run.RunDeckOrigins.Count ? run.RunDeckOrigins[slot] : null;
				if (origin == "treasure" || origin == "signature") continue;
				if (Cards.FromId(run.RunDeckIds[slot])?.Type != CardType.MINION) continue;
				RunHelpers.EditReplaceSlot(g, slot, chosen.Card.Id);
			}
		}

		/// <summary>DALA_726 Master Scheme: @ damage (Spell Damage applies), @ cards, @ Armor and @ Boom Bots; @ is the scheme's count.</summary>
		private static void MasterScheme(Game g, Controller c, IEntity s, IPlayable t, TaskStack stack)
		{
			int n = Math.Max(1, s[GameTag.TAG_SCRIPT_DATA_NUM_1]);
			var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
			if (t is ICharacter) ctx.Damage(t.Id, n, spellDamage: true);
			ctx.Draw(n);
			ctx.RunLegacy(new ArmorTask(n));
			for (int i = 0; i < n && !c.BoardZone.IsFull; i++) ctx.Summon(BoomBot);
		}

		/// <summary>
		/// DALA_729 Dagwik Stickytoe: after it attacks a hero, a random card of the opponent's hand moves to its owner's hand by
		/// the hand-to-hand theft rule (Gloves of Mugging's).
		/// </summary>
		private static void DagwikSteal(Game g, Controller c, IEntity s)
		{
			if (!(g.CurrentEventData?.EventTarget is Hero)) return;
			var op = c.Opponent;
			var hand = op.HandZone.ToArray();
			if (hand.Length == 0) return;
			var stolen = hand[g.Random.Next(hand.Length)];
			g.OnRandomHappened(true);
			using var historyTransfer = g.Facts.CardTransfer(stolen, op.Id, Zone.HAND, s);
			using var temporaryTransfer = TemporaryRules.PreserveHandTransfer(stolen);
			if (stolen.OngoingEffect is AdaptiveCostEffect costEffect) costEffect.Remove();
			op.HandZone.RemoveForTrade(stolen);
			stolen.Controller = c;
			stolen[GameTag.CONTROLLER] = c.PlayerId;
			Generic.AddHandPhase(c, stolen);
		}

		private static void HeistActives()
		{
			// ---------------- enchantments (client shells, the client's own names and texts)
			Effect("fx_DALA_701e", "DALA_701e", "Sifting", "Transforming into random treasures.",
				() => new Power
				{
					Enchant = new Enchant(Array.Empty<IEffect>()) { RemoveWhenPlayed = true },
					Trigger = new Trigger(TriggerType.TURN_START) { SingleTask = Sift(EntityType.TARGET) },
				});
			Buff("fx_DALA_702e", "DALA_702e", "Tooled Up!",
				"Granted <b>Charge</b>,\n<b>Windfury</b>, <b>Divine Shield</b>,\n<b>Lifesteal</b>, <b>Poisonous</b>,\n<b>Taunt</b>, and <b>Stealth</b>.",
				new SabberStoneCore.Enchants.Effect(GameTag.CHARGE, EffectOperator.SET, 1),
				new SabberStoneCore.Enchants.Effect(GameTag.WINDFURY, EffectOperator.SET, 1),
				new SabberStoneCore.Enchants.Effect(GameTag.DIVINE_SHIELD, EffectOperator.SET, 1),
				new SabberStoneCore.Enchants.Effect(GameTag.LIFESTEAL, EffectOperator.SET, 1),
				new SabberStoneCore.Enchants.Effect(GameTag.POISONOUS, EffectOperator.SET, 1),
				new SabberStoneCore.Enchants.Effect(GameTag.TAUNT, EffectOperator.SET, 1));
			// One turn: the client's shell carries TAG_ONE_TURN_EFFECT; the turn-end trigger removes it explicitly as well.
			Effect("fx_DALA_715e", "DALA_715e", "Overpowered", null,
				() => new Power
				{
					Trigger = new MultiTrigger(
						new Trigger(TriggerType.AFTER_PLAY_CARD)
						{
							TriggerSource = TriggerSource.FRIENDLY,
							SingleTask = new CustomTask((g, c, s, t, stack) => Overpowered(g, c, s, t)),
						},
						new Trigger(TriggerType.TURN_END) { SingleTask = RemoveEnchantmentTask.Task }),
				}, oneTurn: true);
			Effect("fx_DALA_723e", "DALA_723e", "Hyperblaster Enchantment", "<b>Immune</b>.",
				() => new Power
				{
					Enchant = new Enchant(Effects.Immune),
					Trigger = new Trigger(TriggerType.AFTER_ATTACK) { TriggerSource = TriggerSource.HERO, SingleTask = RemoveEnchantmentTask.Task },
				});
			Effect("fx_DALA_724e", "DALA_724e", "Big Boomba Player Enchantment", "At the start of your turn, destroy all minions.",
				() => new Power
				{
					Trigger = new Trigger(TriggerType.TURN_START)
					{
						SingleTask = ComplexTask.Create(new DestroyTask(EntityType.ALLMINIONS), RemoveEnchantmentTask.Task),
						RemoveAfterTriggered = true,
					},
				});
			Buff("fx_DALA_725e", "DALA_725e", "Glowing Green", "+2/+2.", Effects.AttackHealth_N(2));
			Buff("fx_DALA_727e", "DALA_727e", "Seeds Sown", "+3/+3.", Effects.AttackHealth_N(3));

			// ---------------- tokens
			// Casts When Drawn (TOPDECK): whoever draws it takes the 50 damage and draws the next card in its place (the engine's
			// Casts When Drawn rule, as Undermine's Improvised Explosive); played from a hand it only deals the damage.
			Active(KadoomBot, "Kadoom Bot", "<b>Casts When Drawn</b>\nYou take 50 damage.", 1,
				() => new CardDef(new Power
				{
					PowerTask = new DamageTask(KadoomBotDamage, EntityType.HERO),
					TopdeckTask = ComplexTask.Create(new DamageTask(KadoomBotDamage, EntityType.HERO), new DrawTask()),
				}));

			// ---------------- spells
			Active("DALA_700", "Continuum Collider", "Take an extra turn. Your opponent takes an extra turn after.", 4,
				() => Def(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => ContinuumCollider(g, c, s)) }));
			Active("DALA_701", "The Box", "Each turn this is in your hand, transform it into a random treasure.", 0,
				() => new CardDef(new Power
				{
					Trigger = new Trigger(TriggerType.TURN_START) { TriggerActivation = TriggerActivation.HAND, SingleTask = Sift(EntityType.SOURCE) },
				}));
			Active("DALA_702", "Gnomish Army Knife",
				"[x]Give a minion <b>Charge</b>,\n<b>Windfury</b>, <b>Divine Shield</b>,\n<b>Lifesteal</b>, <b>Poisonous</b>,\n<b>Taunt</b>, and <b>Stealth</b>.", 5,
				// Stealth is a state the engine clears when the minion attacks (Attack.cs), not an enchantment effect: it is set on
				// the minion itself, beside Tooled Up!'s six keywords.
				() => Def(new Power
				{
					PowerTask = ComplexTask.Create(new AddEnchantmentTask("DALA_702e", EntityType.TARGET), new SetGameTagTask(GameTag.STEALTH, 1, EntityType.TARGET)),
				}, PlayReq.REQ_TARGET_TO_PLAY, PlayReq.REQ_MINION_TARGET));
			Active("DALA_704", "Super Simian Sphere",
				"[x]Summon King Mukla.\nGive it <b>Immune</b> and\n'Can't be targeted by\nspells or Hero Powers.'", 5,
				() => Def(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => SuperSimianSphere(g, c, s, stack)) },
					PlayReq.REQ_NUM_MINION_SLOTS), null, KingMukla);
			// Taking control with a full board destroys the minion (the control rule, ControlTask).
			Active("DALA_706", "EVIL Propaganda", "Take control of all enemy minions.", 10,
				() => Def(new Power { PowerTask = new ControlTask(EntityType.OP_MINIONS) }));
			Active("DALA_708", "Untold Splendor", "Shuffle 5 random treasures into your deck.", 1,
				() => Def(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => UntoldSplendor(g, c, s)) }));
			Active("DALA_711", "Dreamgrove Ring", "<b>Discover</b> a <b>Legendary</b> minion. Summon five copies of it.", 9,
				() => Def(new Power
				{
					// The pick is summoned (ChoiceAction.SUMMON), then four more copies of it (the Power of Creation rule, DAL_578).
					PowerTask = new CustomTask((g, c, s, t, stack) =>
					{
						var pool = ScriptedLegendaryMinions(g, c, s);
						var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
						if (pool.Count > 0) ctx.Discover(ctx.SampleDistinct(pool, 3), ChoiceAction.SUMMON, new CopyTask(EntityType.TARGET, Zone.PLAY, DreamgroveCopies - 1));
					}),
				}, PlayReq.REQ_NUM_MINION_SLOTS));
			// "$20 split between all enemies": twenty 1-damage hits at random living enemies; Spell Damage adds hits (Avenging Wrath).
			Active("DALA_712", "Orb of the Untold", "Deal $20 damage split between all enemies.", 4,
				() => Def(new Power { PowerTask = new EnqueueTask(OrbOfTheUntoldDamage, ComplexTask.DamageRandomTargets(1, EntityType.ENEMIES, 1), true) }));
			// Twinspell: casting a stage puts the next stage in hand (the engine's Twinspell rule reads the client's TWINSPELL_COPY).
			Active("DALA_714", "THE... Candles?", "<b>Twinspell.</b> <b>Twinspell.</b> <b>Twinspell.</b>\nDeal $3 damage to all enemy minions.", 3,
				() => Def(new Power { PowerTask = new DamageTask(3, EntityType.OP_MINIONS, true) }), null, "DALA_714a");
			Active("DALA_714a", "THE... Candles?", "<b>Twinspell.</b> <b>Twinspell.</b>\nDeal $3 damage to all enemy minions.", 3,
				() => Def(new Power { PowerTask = new DamageTask(3, EntityType.OP_MINIONS, true) }), null, "DALA_714b");
			Active("DALA_714b", "THE... Candles?", "<b>Twinspell.</b>\nDeal $3 damage to all enemy minions.", 3,
				() => Def(new Power { PowerTask = new DamageTask(3, EntityType.OP_MINIONS, true) }), null, "DALA_714c");
			Active("DALA_714c", "THE... Candles?", "Deal $3 damage to all enemy minions.", 3,
				() => Def(new Power { PowerTask = new DamageTask(3, EntityType.OP_MINIONS, true) }));
			Active("DALA_715", "Overpowered", "After you play a card this turn, replay it\n<i>(targets chosen randomly)</i>.", 0,
				() => Def(new Power { PowerTask = new AddEnchantmentTask("DALA_715e", EntityType.CONTROLLER) }));
			Active("DALA_716", "Fly-By", "[x]Shuffle a Kadoom Bot\ninto your opponent's deck.\nWhen drawn, it explodes\nfor 50 damage.", 5,
				() => Def(new Power { PowerTask = new AddCardTo(KadoomBot, EntityType.OP_DECK) }), null, KadoomBot);
			Active("DALA_717", "Case Study", "<b>Discover</b> a <b>Quest</b> <b>Reward</b>.", 1,
				() => Def(new Power
				{
					PowerTask = new CustomTask((g, c, s, t, stack) =>
					{
						var pool = QuestRewardCandidates(c);
						var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
						if (pool.Count > 0) ctx.Discover(ctx.SampleDistinct(pool, 3), ChoiceAction.HAND);
					}),
				}));
			Active("DALA_720", "Swampqueen's Call", "Transform your minions into random <b>Legendary</b> minions. Repeatable this turn.", 2,
				() => Def(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => SwampqueensCall(g, c, s)) }));
			Active("DALA_721", "Duplatransmogrifier", "Choose a friendly minion. Replace all minions in your Adventure deck with copies of it.", 4,
				() => Def(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => Duplatransmogrifier(g, c, t)) },
					PlayReq.REQ_TARGET_TO_PLAY, PlayReq.REQ_MINION_TARGET, PlayReq.REQ_FRIENDLY_TARGET));
			Active("DALA_722", "Annoy-o Horn", "Fill your board with annoying minions.", 7,
				() => Def(new Power
				{
					PowerTask = new CustomTask((g, c, s, t, stack) =>
					{
						var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
						RepeatUntil(() => c.BoardZone.IsFull, 7, () => ctx.Summon(AnnoyoTron) != 0);
					}),
				}, PlayReq.REQ_NUM_MINION_SLOTS), null, AnnoyoTron);
			// "Do it again at the start of your next turn": a player enchantment that destroys every minion once, then leaves.
			Active("DALA_724", "Big Boomba", "Destroy ALL minions. Do it again at the start of your next turn.", 2,
				() => Def(new Power
				{
					PowerTask = ComplexTask.Create(new DestroyTask(EntityType.ALLMINIONS), new AddEnchantmentTask("DALA_724e", EntityType.CONTROLLER)),
				}));
			Active("DALA_725", "Banana Split", "Give a friendly minion +2/+2. Summon two copies of it.", 2,
				() => Def(new Power
				{
					PowerTask = ComplexTask.Create(new AddEnchantmentTask("DALA_725e", EntityType.TARGET),
						new SummonCopyTask(EntityType.TARGET), new SummonCopyTask(EntityType.TARGET)),
				}, PlayReq.REQ_TARGET_TO_PLAY, PlayReq.REQ_MINION_TARGET, PlayReq.REQ_FRIENDLY_TARGET));
			// @ is TAG_SCRIPT_DATA_NUM_1 (1 as printed) and grows by 1 each turn in hand (the Scheme rule, TriggerLibrary.UpgradeEachTurn).
			Active("DALA_726", "Master Scheme",
				"[x]Deal $@ damage. Draw\n@ |4(card, cards). Gain @ Armor.\nSummon @ <b>Boom |4(Bot, Bots)</b>.\n<i>(Upgrades each turn!)</i>", 3,
				() => Def(new Power
				{
					PowerTask = new CustomTask((g, c, s, t, stack) => MasterScheme(g, c, s, t, stack)),
					Trigger = TriggerLibrary.UpgradeEachTurn,
				}, PlayReq.REQ_TARGET_TO_PLAY), null, BoomBot);
			Active("DALA_727", "Sow the Seeds", "Give all minions in your deck +3/+3.", 5,
				() => Def(new Power
				{
					PowerTask = ComplexTask.Create(new IncludeTask(EntityType.DECK), new FilterStackTask(SelfCondition.IsMinion),
						new AddEnchantmentTask("DALA_727e", EntityType.STACK)),
				}));

			// ---------------- weapons
			Active("DALA_719", "Soulreaper's Scythe", "<b>Deathrattle:</b> Summon all minions killed by this weapon this run.", 5,
				() => Def(new Power
				{
					Trigger = new Trigger(TriggerType.AFTER_ATTACK) { TriggerSource = TriggerSource.HERO, SingleTask = new CustomTask((g, c, s, t, stack) => ScytheKill(g, c, s)) },
					DeathrattleTask = new CustomTask((g, c, s, t, stack) => ScytheSummons(g, c, s, stack)),
				}));
			// Poisonous is the weapon's own (the client's tag; Character.PoisonousDamageSource reads an equipped weapon's).
			Active("DALA_723", "Hyperblaster", "<b>Poisonous</b>.\nYour hero is <b>Immune</b> while attacking.", 3,
				() => Def(new Power
				{
					Trigger = new Trigger(TriggerType.ATTACK) { TriggerSource = TriggerSource.HERO, SingleTask = new AddEnchantmentTask("DALA_723e", EntityType.HERO) },
				}),
				card => card.Tags[GameTag.POISONOUS] = 1);

			// ---------------- minions
			Active("DALA_703", "The Muscle", "<b>Battlecry:</b> Add three random cards to your hand. They cost (0).", 4,
				() => Def(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => TheMuscle(g, c, s)) }), Stats(4, 5));
			// One copy per Patron per turn end: a copy summoned during the turn end does not trigger until the next one.
			Active("DALA_705", "Grimmer Patron", "At the end of your turn, summon a copy of this minion.", 3,
				() => Def(new Power
				{
					Trigger = new Trigger(TriggerType.TURN_END)
					{
						SingleTask = new CustomTask((g, c, s, t, stack) =>
						{
							if (s is Minion patron && patron.Zone?.Type == Zone.PLAY && !patron.ToBeDestroyed && !c.BoardZone.IsFull)
								new SummonCopyTask(EntityType.SOURCE).Process(g, c, s, null, stack);
						}),
					},
				}), Stats(3, 3));
			Active("DALA_709", "Golden Candle", "<b>Taunt</b>. <b>Battlecry:</b> Replace ALL other cards with random Legendary minions.", 4,
				() => Def(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => GoldenCandle(g, c, s)) }),
				card => { card.SetPrintedStats(7, 7); card.SetPrintedTaunt(true); });
			Active("DALA_713", "Elder Taggawag",
				"[x]<b>Battlecry:</b> Deal 4 damage\nto all enemy minions.\n<b>Deathrattle:</b> Shuffle\nthis into your deck", 3,
				() => Def(new Power
				{
					PowerTask = new DamageTask(4, EntityType.OP_MINIONS),
					DeathrattleTask = new AddCardTo("DALA_713", EntityType.DECK),
				}), Stats(4, 4));
			// The run log (H7): a spell its owner casts on it is kept after a won fight; its Battlecry recasts the log on it.
			Active("DALA_718", "Elistra the Immortal", "<b>Battlecry:</b> Recast all spells you've cast on this minion this run.", 3,
				() => Def(new Power
				{
					PowerTask = new CustomTask((g, c, s, t, stack) => ElistraRecast(g, c, s, stack)),
					Trigger = new Trigger(TriggerType.CAST_SPELL)
					{
						TriggerSource = TriggerSource.FRIENDLY_SPELL_CASTED_ON_THE_OWNER,
						SingleTask = new CustomTask((g, c, s, t, stack) =>
						{
							if (s is Minion elistra && t is Spell spell && spell.Controller == c) RunHelpers.AppendSlotLog(elistra, spell.Card.Id);
						}),
					},
				}), Stats(1, 1));
			Active("DALA_729", "Dagwik Stickytoe",
				"[x]<b>Stealth</b>\nAfter this minion attacks\na hero, steal a card from\n   your opponent's hand.", 3,
				() => Def(new Power
				{
					Trigger = new Trigger(TriggerType.AFTER_ATTACK) { TriggerSource = TriggerSource.SELF, SingleTask = new CustomTask((g, c, s, t, stack) => DagwikSteal(g, c, s)) },
				}), card => { card.SetPrintedStats(2, 5); card.Tags[GameTag.STEALTH] = 1; });
			Active("DALA_730", "Murgatha", "Whenever you play\na card, <b>Overload</b> one\nof your opponent's\nMana Crystals.", 3,
				() => Def(new Power
				{
					Trigger = new Trigger(TriggerType.PLAY_CARD)
					{
						TriggerSource = TriggerSource.FRIENDLY,
						SingleTask = new CustomTask((g, c, s, t, stack) =>
						{
							if (s is Minion m && m.Zone?.Type == Zone.PLAY && t != null && t.Id != s.Id) c.Opponent.OverloadOwed += 1;
						}),
					},
				}), Stats(3, 6));
		}
	}
}
