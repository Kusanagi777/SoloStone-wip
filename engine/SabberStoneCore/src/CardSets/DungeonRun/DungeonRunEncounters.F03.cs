using System;
using System.Collections.Generic;
using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.DungeonRun
{
	// Phase 8, family 03 (unscripted collectibles B, Uldum ULD_131..ULD_274): the family has no encounter card of its own, so
	// nothing is registered here. Its thirty collectibles are scripted where their stubs were (Standard/UldumCardsGen.cs);
	// the rules that need more than a task list are in UldumCollectiblesB below, so that file's edits stay inside the cards.
	public static partial class DungeonRunEncounters
	{
		private static void F03()
		{
		}
	}

	/// <summary>
	/// Phase 8 family 03 (ULD_173 Vessina): an aura on its owner's OTHER minions whose condition is checked again on every
	/// aura update ("while you're Overloaded"). Aura's own Restless mode re-checks BOARD, HAND, HERO, WEAPON and SELF auras
	/// only, so this one queues the adds and removals itself and leaves the rest to the base class (minions entering and
	/// leaving the board, removal with its owner, cloning: the applied set is copied with the instance).
	/// </summary>
	internal sealed class RestlessOthersAura : Aura
	{
		internal RestlessOthersAura(string enchantmentId, SelfCondition condition) : base(AuraType.BOARD_EXCEPT_SOURCE, enchantmentId)
		{
			Condition = condition;
		}

		private RestlessOthersAura(RestlessOthersAura prototype, IPlayable owner) : base(prototype, owner)
		{
		}

		public override void Activate(IPlayable owner, bool cloning = false)
		{
			var instance = new RestlessOthersAura(this, owner);
			if (instance.Effects == null) instance.Effects = EnchantmentCard.Power.Enchant.Effects;
			AddToGame(owner, instance);
			if (!cloning) instance.AuraUpdateInstructionsQueue.Enqueue(new AuraUpdateInstruction(Instruction.AddAll), 1);
		}

		public override void Update()
		{
			if (On && Owner.Zone == Owner.Controller.BoardZone)
				foreach (Minion minion in Owner.Controller.BoardZone)
				{
					if (minion == Owner) continue;
					bool applied = AppliedEntityIdCollection.Contains(minion.Id), holds = Condition.Eval(minion);
					if (holds && !applied) AuraUpdateInstructionsQueue.Enqueue(new AuraUpdateInstruction(minion, Instruction.Add), 2);
					else if (!holds && applied) AuraUpdateInstructionsQueue.Enqueue(new AuraUpdateInstruction(minion, Instruction.Remove), 2);
				}
			base.Update();
		}
	}

	/// <summary>
	/// Phase 8 family 03: the rules of the Uldum collectibles scripted in Standard/UldumCardsGen.cs (ULD_131 Untapped Potential,
	/// ULD_138 Anubisath Defender, ULD_172 Plague of Murlocs, ULD_173 Vessina, ULD_195 Frightened Flunky, ULD_209 Vulpera
	/// Scoundrel, ULD_212 Wild Bloodstinger, ULD_216 Puzzle Box of Yogg-Saron, ULD_236 Tortollan Pilgrim, ULD_262 High Priest
	/// Amet, ULD_268 Psychopomp). Every generated card comes from the game's generation pool (RandomCardTask.GetCardList inside
	/// Game.GenerateFor: a run match's exclusions and the receiver's Secret rule), scripted cards only, in ordinal order, and
	/// every roll uses the game RNG and reports a random event. No state is kept outside entities.
	/// </summary>
	internal static class UldumCollectiblesB
	{
		/// <summary>ULD_216: how many random spells one Puzzle Box casts.</summary>
		internal const int PuzzleBoxCasts = 10;
		internal const string PuzzleBox = "ULD_216";
		/// <summary>ULD_209t Mystery Choice!, the fourth option of Vulpera Scoundrel's Discover.</summary>
		internal const string MysteryChoice = "ULD_209t";

		private static bool HeroDown(Controller c) =>
			c.Hero == null || c.Hero.ToBeDestroyed || c.Hero.Health <= 0 || c.PlayState != PlayState.PLAYING;

		// ------------------------------------------------------------------ ULD_131 Untapped Potential (a Quest whose Reward is a hero power)

		/// <summary>
		/// One step of an Uldum Quest whose Reward is a hero power (the legacy quest slot, counted as QuestProgressTask counts).
		/// At the total the Quest is revealed and leaves play, and its owner's hero power is replaced by the reward
		/// (ReplaceHeroPower: the old power goes to the set-aside zone; an encounter power's own trigger and aura are stopped
		/// first), whose aura and trigger start at once (EncounterRules.ActivatePower: a passive reward does nothing otherwise).
		/// </summary>
		internal static void QuestStep(Game g, IEntity source, string rewardPowerId)
		{
			if (!(source is Spell quest)) return;
			Controller owner = quest.Controller;
			if (quest.Zone != owner.SecretZone || owner.SecretZone.Quest != quest) return;
			quest.QuestProgress++;
			if (quest.QuestProgress < quest.QuestTotalProgress) return;
			quest.ActivatedTrigger?.Remove();
			quest[GameTag.REVEALED] = 1;
			owner.SecretZone.Quest = null;
			owner.GraveyardZone.Add(quest);
			HeroPower old = owner.Hero.HeroPower;
			if (old != null && old[EncounterRules.PowerActive] != 0) EncounterRules.DeactivatePower(old);
			new ReplaceHeroPower(Cards.FromId(rewardPowerId)).Process(g, owner, quest, null);
			EncounterRules.ActivatePower(owner.Hero.HeroPower);
		}

		// ------------------------------------------------------------------ ULD_138 Anubisath Defender, ULD_173 Vessina

		/// <summary>
		/// ULD_138: its owner has played, this turn, a spell that cost (5) or more when it was played (the play history's paid
		/// cost, so a discounted spell counts at what it cost). A public fact of the owner's own plays.
		/// </summary>
		internal static bool CastBigSpellThisTurn(IPlayable p)
		{
			HistoryQuery query = p.Game.HistoryFacts.Query;
			foreach (CardPlayedFact play in query.Plays(query.ThisTurn))
				if (play.ControllerId == p.Controller.Id && play.CardType == CardType.SPELL && play.PaidCost >= 5) return true;
			return false;
		}

		/// <summary>ULD_173: the minion's controller is Overloaded (Mana Crystals locked this turn, or owed for the next).</summary>
		internal static bool Overloaded(IPlayable p) => p.Controller.OverloadLocked > 0 || p.Controller.OverloadOwed > 0;

		// ------------------------------------------------------------------ Discovers (ULD_195, ULD_209, ULD_236)

		/// <summary>
		/// The scripted cards of <paramref name="type"/> a Discover of <paramref name="c"/>'s may offer
		/// (DungeonRunContent.ClassDiscoverPool: its classes' and the neutral ones, run exclusions honoured), narrowed by
		/// <paramref name="filter"/>, never the discovering card itself.
		/// </summary>
		internal static List<Card> DiscoverPool(Game g, Controller c, IEntity source, CardType type, Func<Card, bool> filter = null)
		{
			var pool = new List<Card>();
			foreach (Card card in DungeonRunContent.ClassDiscoverPool(g, c, source, type))
				if (card.Id != source.Card.Id && (filter == null || filter(card))) pool.Add(card);
			return pool;
		}

		/// <summary>ULD_195 Frightened Flunky: Discover a Taunt minion (to hand).</summary>
		internal static void DiscoverTaunt(Game g, Controller c, IEntity s, TaskStack stack)
		{
			List<Card> pool = DiscoverPool(g, c, s, CardType.MINION, card => card[GameTag.TAUNT] == 1);
			if (pool.Count == 0) return;
			var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
			ctx.Discover(ctx.SampleDistinct(pool, 3), ChoiceAction.HAND);
		}

		/// <summary>
		/// ULD_209 Vulpera Scoundrel: a Discover of three spells with a fourth option, Mystery Choice! (ULD_209t). A picked spell
		/// goes to the hand; the mystery choice adds a random spell of the same pool instead (<see cref="MysterySpell"/>).
		/// </summary>
		internal static void Scoundrel(Game g, Controller c, IEntity s, TaskStack stack)
		{
			List<Card> pool = DiscoverPool(g, c, s, CardType.SPELL);
			var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
			var offers = new List<Card>(ctx.SampleDistinct(pool, 3)) { Cards.FromId(MysteryChoice) };
			ctx.Discover(offers.ToArray(), ChoiceAction.STACK, new CustomTask((g2, c2, s2, picked, st) => ScoundrelPick(g2, c2, s2, picked)));
		}

		private static void ScoundrelPick(Game g, Controller c, IEntity s, IPlayable picked)
		{
			if (picked == null) return;
			if (picked.Card.Id == MysteryChoice) { MysterySpell(g, c, s); return; }
			if (Generic.RemoveFromZone.Invoke(c, picked)) Generic.AddHandPhase.Invoke(c, picked);
		}

		/// <summary>ULD_209t Mystery Choice!: a random spell of the owner's Discover pool to the owner's hand.</summary>
		internal static void MysterySpell(Game g, Controller c, IEntity s)
		{
			List<Card> pool = DiscoverPool(g, c, s, CardType.SPELL, card => card.Id != MysteryChoice);
			if (pool.Count == 0) return;
			IPlayable spell = Entity.FromCard(c, pool[g.Random.Next(pool.Count)], new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id });
			Generic.AddHandPhase.Invoke(c, spell);
			g.OnRandomHappened(true);
		}

		/// <summary>
		/// ULD_236 Tortollan Pilgrim: a Discover among the different spells of the owner's deck (one offer per card, ordinal
		/// order before the draw; the deck keeps its cards); the pick is a copy, cast at once with random targets and a random
		/// Choose One mode (DungeonRunContent.CastRandomly). The owner's deck is its own known cards; a fair view's masked deck
		/// slots are not spells and are never offered.
		/// </summary>
		internal static void Pilgrim(Game g, Controller c, IEntity s, TaskStack stack)
		{
			var ids = new SortedSet<string>(StringComparer.Ordinal);
			foreach (IPlayable p in c.DeckZone)
				if (p is Spell) ids.Add(p.Card.Id);
			if (ids.Count == 0) return;
			var pool = new List<Card>();
			foreach (string id in ids) pool.Add(Cards.FromId(id));
			var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
			ctx.Discover(ctx.SampleDistinct(pool, 3), ChoiceAction.STACK, new CustomTask((g2, c2, s2, picked, st) =>
			{
				if (picked is Spell spell && !HeroDown(c2) && !HeroDown(c2.Opponent)) DungeonRunContent.CastRandomly(g2, c2, spell);
			}));
		}

		// ------------------------------------------------------------------ ULD_172 Plague of Murlocs

		private static List<Card> Scripted(Game g, Controller receiver, IEntity source, CardType type, Race race = Race.INVALID)
		{
			var byId = new SortedDictionary<string, Card>(StringComparer.Ordinal);
			using (g.GenerateFor(receiver))
				foreach (Card card in RandomCardTask.GetCardList(source, type, race: race))
					if (card.Implemented && card.Type == type && !byId.ContainsKey(card.Id)) byId.Add(card.Id, card);
			return new List<Card>(byId.Values);
		}

		/// <summary>
		/// ULD_172: every minion in play (the caster's board first, then the opponent's, left to right) is transformed into a
		/// random scripted Murloc; each side's new minions pass that side's generation rules. A dormant minion stays, and a
		/// run board shrine goes dormant instead of transforming (Generic.TransformBlock, ShrineRules.InterceptTransform).
		/// </summary>
		internal static void PlagueOfMurlocs(Game g, Controller c, IEntity s)
		{
			var minions = new List<Minion>(c.BoardZone.GetAll());
			minions.AddRange(c.Opponent.BoardZone.GetAll());
			var pools = new Dictionary<int, List<Card>>();
			bool rolled = false;
			foreach (Minion minion in minions)
			{
				if (minion.Zone?.Type != Zone.PLAY || minion.ToBeDestroyed || minion.IsDormant || minion.Untouchable) continue;
				Controller side = minion.Controller;
				if (!pools.TryGetValue(side.PlayerId, out List<Card> pool))
					pools[side.PlayerId] = pool = Scripted(g, side, s, CardType.MINION, Race.MURLOC);
				if (pool.Count == 0) continue;
				Generic.TransformBlock.Invoke(side, pool[g.Random.Next(pool.Count)], minion);
				rolled = true;
			}
			if (rolled) g.OnRandomHappened(true);
		}

		// ------------------------------------------------------------------ ULD_212 Wild Bloodstinger

		/// <summary>
		/// ULD_212: a random minion of the opponent's hand is summoned for the opponent (the hand-to-board pull of Dirty Rat,
		/// CFM_790: a public summon), then the Bloodstinger attacks it at once (a forced attack: no attack is spent), if both
		/// are in play and the minion can be attacked. Nothing with a full opposing board or no minion in that hand. In a fair
		/// view the opponent's hidden cards are no minions, so a search pulls nothing; the real pull is a public event.
		/// </summary>
		internal static void Bloodstinger(Game g, Controller c, IEntity s)
		{
			Controller op = c.Opponent;
			if (op.BoardZone.IsFull) return;
			var minions = new List<IPlayable>();
			foreach (IPlayable p in op.HandZone)
				if (p is Minion) minions.Add(p);
			if (minions.Count == 0) return;
			var pulled = (Minion)minions[g.Random.Next(minions.Count)];
			g.OnRandomHappened(true);
			var stack = new TaskStack { Playables = new List<IPlayable> { pulled } };
			new RemoveFromHand(EntityType.STACK).Process(g, c, s, null, stack);
			if (stack.Playables.Count == 0) return;
			new SummonOpTask().Process(g, c, s, null, stack);
			g.AuraUpdate();
			if (!(s is Minion self) || self.Zone != c.BoardZone || self.ToBeDestroyed || self.IsDormant) return;
			if (pulled.Zone != op.BoardZone || pulled.ToBeDestroyed || pulled.IsDormant || pulled.Untouchable) return;
			if (g.State == State.COMPLETE) return;
			PastColossalCombat.Force(self, pulled);
		}

		// ------------------------------------------------------------------ ULD_216 Puzzle Box of Yogg-Saron

		/// <summary>
		/// The spells a Puzzle Box casts for <paramref name="c"/>: the scripted spells of the generation pool (no Quest; a
		/// Secret only for a side that may generate one; run exclusions honoured), without hidden-stat cards and without the
		/// Box itself (a Box never casts another Box: one cast is at most <see cref="PuzzleBoxCasts"/> spells).
		/// </summary>
		internal static List<Card> PuzzleBoxPool(Game g, Controller c, IEntity s)
		{
			List<Card> pool = Scripted(g, c, s, CardType.SPELL);
			pool.RemoveAll(card => card.Id == PuzzleBox || card.HideStat || card.IsQuest);
			return pool;
		}

		/// <summary>
		/// ULD_216: ten random spells, each a new copy cast for the owner with a random Choose One mode and a random legal
		/// target (DungeonRunContent.CastRandomly, the random cast of Yogg-Saron: a spell that needs a target and has none
		/// fizzles, a Secret already in play fails, Discovers the cast opens are picked at random). It stops when a hero is down.
		/// </summary>
		internal static void CastPuzzleBox(Game g, Controller c, IEntity s)
		{
			List<Card> pool = PuzzleBoxPool(g, c, s);
			for (int i = 0; i < PuzzleBoxCasts && pool.Count > 0; i++)
			{
				if (g.State == State.COMPLETE || HeroDown(c) || HeroDown(c.Opponent)) return;
				var spell = (Spell)Entity.FromCard(c, pool[g.Random.Next(pool.Count)], new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id });
				using (g.GenerateFor(c)) DungeonRunContent.CastRandomly(g, c, spell);
			}
		}

		// ------------------------------------------------------------------ ULD_262 High Priest Amet, ULD_268 Psychopomp

		/// <summary>
		/// ULD_262: a minion its owner summons (not Amet itself) has its Health set to Amet's current Health (ULD_262e, a
		/// set-Health enchantment carrying the value).
		/// </summary>
		internal static void Amet(Game g, IEntity source, IPlayable summoned)
		{
			if (!(source is Minion amet) || !(summoned is Minion minion) || minion == amet) return;
			if (amet.Zone?.Type != Zone.PLAY || minion.Zone?.Type != Zone.PLAY || minion.Controller != amet.Controller) return;
			Generic.AddEnchantmentBlock(g, Cards.FromId("ULD_262e"), amet, minion, amet.Health, 0, 0);
		}

		/// <summary>
		/// ULD_268: a random friendly minion that died this game (the owner's graveyard, a public zone; never a dead run
		/// shrine) is summoned anew to the Psychopomp's right, as resurrection does, and given Reborn.
		/// </summary>
		internal static void Psychopomp(Game g, Controller c, IEntity s)
		{
			if (c.BoardZone.IsFull) return;
			var dead = new List<IPlayable>();
			foreach (IPlayable p in c.GraveyardZone)
				if (p.ToBeDestroyed && p.Card.Type == CardType.MINION && !ShrineRules.IsCorpse(p)) dead.Add(p);
			if (dead.Count == 0) return;
			Card card = dead[g.Random.Next(dead.Count)].Card;
			g.OnRandomHappened(true);
			var minion = (Minion)Entity.FromCard(c, card, new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id });
			int position = s is Minion self && self.Zone == c.BoardZone ? self.ZonePosition + 1 : -1;
			Generic.SummonBlock.Invoke(g, minion, position, s);
			if (minion.Zone?.Type != Zone.PLAY) return;
			minion[GameTag.REBORN] = 1;
			minion[GameTag.HAS_BEEN_REBORN] = 0;
		}
	}
}
