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
	public static partial class DungeonRunContent
	{
		// ---------------------------------------------------------------- MonsterHuntShawToki
		// The Witchwood Monster Hunt heroes Houndmaster Shaw and Toki, Time-Tinker (phase 6, family F4): Shaw's power GILA_400p
		// Dog Whistle with its Bloodhound (GILA_400t), and the hero treasures of both. Toki plays the Mage basic power as a
		// stand-in for Temporal Loop (GILA_900p), whose turn restart step 9 rejects for good (NativeMatch has no client history
		// re-sync); Rewind (GILA_911), a game restart, is scripted by step 9 (DungeonRunDeferrals.cs, EncounterRules.S9.cs).
		// Every id is keyed and shelled by its own client id, so the run deck, the treasure picker and the match show the real
		// card; every name and text is the client's own, word for word (facts/violet-hold-2026-09.json). The treasures are the
		// run player's cards (DungeonRunAdmission); none enters a boss deck. Every random choice uses the game RNG; every piece
		// of state is an entity tag, so clones and AI simulations replay it.

		/// <summary>Shaw's Bloodhound (GILA_400t, a 1/1 Beast with Rush): Dog Whistle and Bubba summon it, Companionship buffs it.</summary>
		public const string Bloodhound = "GILA_400t";

		/// <summary>GILA_410 Bubba: how many Bloodhounds it summons.</summary>
		public const int BubbaHounds = 6;

		/// <summary>GILA_401 Sic 'Em: how many minions it summons from the deck.</summary>
		public const int SicEmSummons = 3;

		/// <summary>GILA_411 Princess: how many Deathrattles it copies.</summary>
		public const int PrincessDeathrattles = 3;

		/// <summary>GILA_910 Puzzle Box: how much more the new minions cost.</summary>
		public const int PuzzleBoxCostDelta = 3;

		/// <summary>On a controller: the game turn its Double Time (GILA_913) last copied a spell (its first spell of that turn).</summary>
		public const GameTag DoubleTimeTurn = (GameTag)34640;

		/// <summary>
		/// GILA_903 Enhance-a-matic's classes: every class of the game's class pools, in enum order (the client's text says
		/// "each class"; the engine has eleven).
		/// </summary>
		public static readonly IReadOnlyList<CardClass> EnhanceAMaticClasses = new[]
		{
			CardClass.DEATHKNIGHT, CardClass.DRUID, CardClass.HUNTER, CardClass.MAGE, CardClass.PALADIN, CardClass.PRIEST,
			CardClass.ROGUE, CardClass.SHAMAN, CardClass.WARLOCK, CardClass.WARRIOR, CardClass.DEMONHUNTER,
		};

		/// <summary>GILA_403 Butch: how many friendly Beasts died this game (the public death history).</summary>
		public static int FriendlyBeastDeaths(IPlayable p) =>
			p.Game.HistoryFacts.Query.Deaths().Count(f => f.ControllerId == p.Controller.Id && (f.Races.Contains(Race.BEAST) || f.Races.Contains(Race.ALL)));

		/// <summary>
		/// Casts <paramref name="spell"/> for <paramref name="caster"/> as a random cast does (CastRandomSpellTask, Yogg-Saron):
		/// a random Choose One mode and a random legal target, both from the game RNG; a spell that needs a target and has none
		/// fizzles (CastSpell's automatic rule). Discover choices the cast opens are picked at random. Works for a card in the
		/// caster's hand (it leaves the hand) and for a new copy alike.
		/// </summary>
		internal static void CastRandomly(Game g, Controller caster, Spell spell)
		{
			int chooseOne = 0;
			ICharacter target;
			if (spell.ChooseOne)
			{
				chooseOne = g.Random.Next(1, spell.ChooseOnePlayables.Length + 1);
				var profile = Generic.SpellTargetingProfile(spell, chooseOne);
				var legal = profile.ValidPlayTargets.Where(profile.IsValidPlayTarget).ToArray();
				target = legal.Length == 0 ? null : legal[g.Random.Next(legal.Length)];
				if (target != null) spell.CardTarget = target.Id;
			}
			else target = spell.GetRandomValidTarget();
			g.OnRandomHappened(true);

			Choice pending = caster.Choice;
			caster.Choice = null;
			g.TaskQueue.StartEvent();
			Generic.CastSpell.Invoke(caster, g, spell, target, chooseOne);
			g.DeathProcessingAndAuraUpdate();
			g.TaskQueue.EndEvent();
			while (caster.Choice != null)
			{
				g.TaskQueue.StartEvent();
				Generic.ChoicePick.Invoke(caster, g, caster.Choice.Choices.Choose(g.Random));
				g.ProcessTasks();
				g.TaskQueue.EndEvent();
				g.DeathProcessingAndAuraUpdate();
			}
			caster.Choice = pending;
		}

		/// <summary>
		/// GILA_401 Sic 'Em: the owner's <see cref="SicEmSummons"/> deck minions with the highest Attack are summoned, one at a
		/// time (a tie for the highest is broken with the game RNG); stops when the board is full or the deck has no minion.
		/// </summary>
		private static void SicEm(Game g, Controller c, IEntity s)
		{
			var deck = c.DeckZone.OfType<Minion>().ToList();
			for (int i = 0; i < SicEmSummons && deck.Count > 0 && !c.BoardZone.IsFull; i++)
			{
				int best = deck.Max(m => m.AttackDamage);
				var top = deck.Where(m => m.AttackDamage == best).ToList();
				var pick = top.Count == 1 ? top[0] : top[g.Random.Next(top.Count)];
				if (top.Count > 1) g.OnRandomHappened(true);
				deck.Remove(pick);
				if (pick.Zone != c.DeckZone) continue;
				Generic.RemoveFromZone.Invoke(c, pick);
				Generic.SummonBlock.Invoke(g, pick, -1, (IPlayable)s);
			}
		}

		/// <summary>
		/// GILA_410 Bubba: summons <see cref="BubbaHounds"/> Bloodhounds (as many as the board holds), then each attacks the chosen
		/// enemy minion in turn, until it leaves play. With no enemy minion to choose, the Bloodhounds are summoned only.
		/// </summary>
		private static void Bubba(Game g, Controller c, IEntity s, IPlayable t)
		{
			var hounds = new List<Minion>();
			var card = Cards.FromId(Bloodhound);
			for (int i = 0; i < BubbaHounds && !c.BoardZone.IsFull; i++)
			{
				var hound = (Minion)Entity.FromCard(c, card);
				Generic.SummonBlock.Invoke(g, hound, -1, (IPlayable)s);
				if (hound.Zone?.Type == Zone.PLAY) hounds.Add(hound);
			}
			g.DeathProcessingAndAuraUpdate();
			if (!(t is Minion target) || target.Controller == c) return;
			foreach (var hound in hounds)
			{
				if (target.Zone?.Type != Zone.PLAY || target.IsDead || target.ToBeDestroyed || target.Health <= 0) break;
				if (hound.Zone?.Type != Zone.PLAY || hound.IsDead || hound.ToBeDestroyed) continue;
				// The forced attack rule AttackTask uses: no attack option is spent.
				EventMetaData temp = g.CurrentEventData;
				Generic.AttackBlock.Invoke(c, hound, target, true, false);
				c.NumOptionsPlayedThisTurn--;
				g.CurrentEventData = temp;
				g.DeathProcessingAndAuraUpdate();
			}
		}

		/// <summary>
		/// GILA_411 Princess: <see cref="PrincessDeathrattles"/> random minions of the owner's deck that have a Deathrattle (distinct
		/// deck cards, game RNG) each give the Princess a Princess's Bone (GILA_411e) holding that card's Deathrattle, as
		/// Unearthed Raptor's copy (LOE_019e: the captured card's Deathrattle runs when the Princess dies).
		/// </summary>
		private static void Princess(Game g, Controller c, IEntity s)
		{
			if (!(s is Minion princess) || princess.Zone?.Type != Zone.PLAY) return;
			var candidates = c.DeckZone.OfType<Minion>().Where(m => m.Card.Power?.DeathrattleTask != null).ToList();
			if (candidates.Count == 0) return;
			var bone = Cards.FromId("GILA_411e");
			int n = Math.Min(PrincessDeathrattles, candidates.Count);
			if (candidates.Count > n) g.OnRandomHappened(true);
			for (int i = 0; i < n; i++)
			{
				var pick = candidates[g.Random.Next(candidates.Count)];
				candidates.Remove(pick);
				Generic.AddEnchantmentBlock(g, bone, princess, princess, pick.Id, 0, pick.Id);
			}
		}

		/// <summary>
		/// GILA_901 Chaos Theory: the owner, then the opponent, casts every spell that was in their hand when it resolved, left to
		/// right (<see cref="CastRandomly"/>: random modes and targets). Spells that reach a hand while it resolves are not cast. It
		/// stops when a hero dies. A cast Secret is revealed (CastSpell's automatic rule for a hidden zone).
		/// </summary>
		private static void ChaosTheory(Game g, Controller c)
		{
			foreach (var side in new[] { c, c.Opponent })
			{
				var spells = side.HandZone.OfType<Spell>().ToArray();
				foreach (var spell in spells)
				{
					if (HeroDown(c) || HeroDown(c.Opponent)) return;
					if (spell.Zone != side.HandZone || spell.Controller != side) continue;
					// The other side's spells generate for that side: a card passes only what both sides' run rules allow.
					using (g.GenerateFor(side)) CastRandomly(g, side, spell);
				}
			}
		}

		/// <summary>
		/// GILA_903 Enhance-a-matic: one random scripted spell of each class (<see cref="EnhanceAMaticClasses"/>) from the engine's
		/// generation pool (run exclusions honoured) into the owner's hand; a full hand burns the rest by the ordinary hand rule.
		/// </summary>
		private static void EnhanceAMatic(Game g, Controller c, IEntity s)
		{
			foreach (var cls in EnhanceAMaticClasses)
			{
				var pool = RandomCardTask.GetCardList(s, CardType.SPELL, cls).Where(card => card.Implemented && card.Type == CardType.SPELL && !card.IsQuest).ToList();
				if (pool.Count == 0) continue;
				Generic.AddHandPhase.Invoke(c, Entity.FromCard(c, pool[g.Random.Next(pool.Count)]));
				g.OnRandomHappened(true);
			}
		}

		/// <summary>
		/// GILA_906 Impetuous Companion: the two players swap hands. Every card changes controller by the hand-to-hand transfer
		/// rule Gloves of Mugging uses (a CardTransferredFact each, so the side that gave a card knows it: PublicHandKnowledge),
		/// keeps its enchantments and cost, and joins the other hand in its order. Both hands leave first, so the swap never burns
		/// a card (a hand holds at most ten, and this card has left its owner's).
		/// </summary>
		private static void SwapHands(Game g, Controller c, IEntity s)
		{
			var op = c.Opponent;
			var mine = c.HandZone.ToArray();
			var theirs = op.HandZone.ToArray();
			if (mine.Length == 0 && theirs.Length == 0) return;
			var moving = mine.Concat(theirs).ToArray();
			var from = moving.ToDictionary(card => card, card => card.Controller);
			var hadAura = new HashSet<IPlayable>(moving.Where(card => card.OngoingEffect != null && !(card.OngoingEffect is AdaptiveCostEffect)));
			var preserved = new List<IDisposable>();
			foreach (var card in moving)
			{
				var keep = TemporaryRules.PreserveHandTransfer(card);
				if (keep != null) preserved.Add(keep);
				card.ActivatedTrigger?.Remove();
				if (card.OngoingEffect is AdaptiveCostEffect cost) cost.Remove();
				else card.OngoingEffect?.Remove();
				from[card].HandZone.RemoveForTrade(card);
			}
			foreach (var card in theirs.Concat(mine))
			{
				var giver = from[card];
				var receiver = giver == c ? op : c;
				using var transfer = g.Facts.CardTransfer(card, giver.Id, Zone.HAND, s);
				TransferController(card, receiver);
				Generic.AddHandPhase.Invoke(receiver, card);
				if (hadAura.Contains(card) && card.OngoingEffect == null && card.Zone == receiver.HandZone) card.Power?.Aura?.Activate(card);
			}
			foreach (var keep in preserved) keep.Dispose();
			CostRules.RefreshHandAuras(g);
			g.AuraUpdate();
		}

		/// <summary>
		/// GILA_910 Puzzle Box: every minion in play (the owner's board first, then the opponent's, left to right) becomes a
		/// random scripted minion of the generation pool that costs (3) more than its card (as Evolve, OG_027); a minion with no
		/// such minion stays. Each side's new minions pass that side's generation rules.
		/// </summary>
		private static void PuzzleBox(Game g, Controller c, IEntity s)
		{
			var minions = c.BoardZone.GetAll().Concat(c.Opponent.BoardZone.GetAll()).ToArray();
			var pools = new Dictionary<Controller, IReadOnlyList<Card>>();
			foreach (var minion in minions)
			{
				if (minion.Zone?.Type != Zone.PLAY || minion.ToBeDestroyed) continue;
				var side = minion.Controller;
				if (!pools.TryGetValue(side, out var pool))
					using (g.GenerateFor(side))
						pools[side] = pool = RandomCardTask.GetCardList(s, CardType.MINION).Where(card => card.Implemented && card.Type == CardType.MINION).ToList();
				int cost = minion.Card.Cost + PuzzleBoxCostDelta;
				var options = pool.Where(card => card.Cost == cost).ToList();
				if (options.Count == 0) continue;
				Generic.ChangeEntityBlock(c, minion, options[g.Random.Next(options.Count)], true);
			}
			g.OnRandomHappened(true);
		}

		/// <summary>
		/// GILA_913 Double Time: after the owner casts their first spell of a turn (the first after-cast of that turn), a copy of
		/// it is cast (<see cref="CastRandomly"/>). A copy of a Secret fails as a duplicate; a Quest is not copied.
		/// </summary>
		private static void DoubleTime(Game g, Controller c, IEntity s, IPlayable t)
		{
			if (!(t is Spell cast) || cast.Controller != c || c[DoubleTimeTurn] == g.Turn) return;
			c[DoubleTimeTurn] = g.Turn;
			if (cast.Card.IsQuest || HeroDown(c) || HeroDown(c.Opponent)) return;
			var copy = (Spell)Entity.FromCard(c, cast.Card, new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id });
			CastRandomly(g, c, copy);
		}

		private static void MonsterHuntShawToki()
		{
			// ---------------- Houndmaster Shaw: Dog Whistle, the Bloodhound, his treasures
			Active(Bloodhound, "Bloodhound", "<b>Rush</b>", 1, () => new CardDef(), card =>
			{
				card.SetPrintedStats(1, 1);
				card.SetPrintedRush(true);
			});
			Power("GILA_400p", "GILA_400p", "Dog Whistle", "<b>Hero Power</b>\nSummon a 1/1 Bloodhound with <b>Rush</b>.", 2, Summon,
				() => new SummonTask(Bloodhound, SummonSide.DEFAULT), Bloodhound);

			Effect("fx_GILA_411e", "GILA_411e", "Princess's Bone", "Copied Deathrattle from {0}.",
				() => new Power { DeathrattleTask = ActivateCapturedDeathrattleTask.Task });
			Buff("fx_GILA_412e", "GILA_412e", "Blood Hungry", "+1/+1 and <b>Lifesteal</b>.",
				Effects.Attack_N(1), Effects.Health_N(1), new SabberStoneCore.Enchants.Effect(GameTag.LIFESTEAL, EffectOperator.SET, 1));
			Buff("fx_GILA_414e2", "GILA_414e2", "Companionship", "+2 Attack.", Effects.Attack_N(2));
			Effect("fx_GILA_414", "GILA_414e", "Companionship Player Enchant", null,
				() => new Power { Aura = new Aura(AuraType.BOARD, "GILA_414e2") { Condition = SelfCondition.IsCardId(Bloodhound) } });
			Effect("fx_GILA_415", "GILA_415e", "Lunar Signet Player Enchant", null,
				() => new Power { Trigger = Trig(TriggerType.AFTER_PLAY_MINION, new ActivateDeathrattleTask(EntityType.TARGET)) });

			Active("GILA_401", "Sic 'Em", "Summon 3 minions from your deck with the highest attack.", 6,
				() => Def(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => SicEm(g, c, s)) }, PlayReq.REQ_NUM_MINION_SLOTS));
			Active("GILA_403", "Butch", "<b>Rush</b>\nHas +1/+1 for each\nfriendly Beast that\ndied this game.", 4,
				() => Def(new Power
				{
					Aura = new MultiAura(
						new AdaptiveEffect(GameTag.ATK, EffectOperator.ADD, FriendlyBeastDeaths),
						new AdaptiveEffect(GameTag.HEALTH, EffectOperator.ADD, FriendlyBeastDeaths))
				}),
				card => card.SetPrintedRush(true));
			Active("GILA_410", "Bubba", "<b>Battlecry</b>: Summon six 1/1 Bloodhounds to attack an enemy minion.", 7,
				() => Def(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => Bubba(g, c, s, t)) },
					PlayReq.REQ_TARGET_IF_AVAILABLE, PlayReq.REQ_MINION_TARGET, PlayReq.REQ_ENEMY_TARGET),
				card => card.Tags[GameTag.BATTLECRY] = 1, Bloodhound);
			Active("GILA_411", "Princess", "[x]<b>Battlecry:</b> Gain the\n<b>Deathrattles</b> of 3 random\nminions in your deck.", 5,
				() => Def(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => Princess(g, c, s)) }),
				card => card.Tags[GameTag.BATTLECRY] = 1);
			Active("GILA_412", "Blood Moon", "Give your minions +1/+1 and <b>Lifesteal</b>.", 2,
				() => Def(new Power { PowerTask = new AddEnchantmentTask("GILA_412e", EntityType.MINIONS) }));
			// The weapon's Attack follows the owner's board (Spirit Claws' rule, KAR_063): untouchable board entities do not count.
			Active("GILA_413", "Shaw's Shank", "Has +1_Attack for each friendly minion you control.", 3,
				() => Def(new Power { Aura = new AdaptiveEffect(GameTag.ATK, EffectOperator.ADD, p => p.Controller.BoardZone.CountExceptUntouchables) }));
			Treasure("GILA_414", "Companionship", "<b>Passive</b>\nYour Bloodhounds have +2 Attack.", effect: "fx_GILA_414", ownShell: true);
			Treasure("GILA_415", "Lunar Signet", "<b>Passive</b>\nAfter you play a minion, trigger its <b>Deathrattle</b> effect.",
				effect: "fx_GILA_415", ownShell: true);

			// ---------------- Toki, Time-Tinker: her treasures (her power is the Mage basic power, a stand-in)
			Effect("fx_GILA_913", "GILA_913e", "Double Time Player Enchant", null,
				() => new Power { Trigger = Trig(TriggerType.AFTER_CAST, new CustomTask((g, c, s, t, stack) => DoubleTime(g, c, s, t))) });

			Active("GILA_901", "Chaos Theory", "Both players cast all spells in their hand <i>(targets chosen randomly).</i>", 2,
				() => Def(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => ChaosTheory(g, c)) }));
			Active("GILA_903", "Enhance-a-matic", "Add a random spell from each class to your_hand.", 1,
				() => Def(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => EnhanceAMatic(g, c, s)) }));
			Active("GILA_904", "Stroke of Midnight", "<b>Echo</b>\nDestroy a random enemy minion.", 2,
				() => new CardDef(new Dictionary<PlayReq, int> { [PlayReq.REQ_MINIMUM_ENEMY_MINIONS] = 1 }, new Power
				{
					PowerTask = ComplexTask.Create(new IncludeTask(EntityType.OP_MINIONS), new RandomTask(1, EntityType.STACK), new DestroyTask(EntityType.STACK)),
				}));
			Active("GILA_906", "Impetuous Companion", "<b>Battlecry:</b> Swap hands with your opponent.", 3,
				() => Def(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => SwapHands(g, c, s)) }),
				card => card.Tags[GameTag.BATTLECRY] = 1);
			Active("GILA_907", "Clockwork Assistant", "Has +1/+1 for each spell you've cast this game.", 1,
				() => Def(new Power
				{
					Aura = new MultiAura(
						new AdaptiveEffect(GameTag.ATK, EffectOperator.ADD, p => p.Controller.NumSpellsPlayedThisGame),
						new AdaptiveEffect(GameTag.HEALTH, EffectOperator.ADD, p => p.Controller.NumSpellsPlayedThisGame))
				}));
			Active("GILA_910", "Puzzle Box", "Transform all minions into random ones that cost (3) more.", 1,
				() => Def(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => PuzzleBox(g, c, s)) }));
			Treasure("GILA_913", "Double Time", "<b>Passive</b>\nAfter you cast your first spell in a turn, cast a copy of it\n(<i>targets chosen randomly</i>).",
				effect: "fx_GILA_913", ownShell: true);
		}
	}
}
