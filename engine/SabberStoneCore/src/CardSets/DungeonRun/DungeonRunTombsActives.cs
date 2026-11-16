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
		// ---------------------------------------------------------------- TombsActives
		// The Tombs of Terror active treasures (phase 6, family F12): the treasures a Tombs explorer's act run is offered in its
		// treasure drafts (run-v1.json: scoped by the explorers' sixteen starters, pool Tombs), with their Twinspell copies
		// (ULDA_035ts, 036ts, 041ts, 307ts: the engine's Twinspell rule puts the copy in hand from the client's TWINSPELL_COPY)
		// and their tokens and enchantments. Every card is keyed and shelled by its own client id, so the run deck, the treasure
		// picker and the match show the real card; every name and text is the client's own, word for word
		// (facts/violet-hold-2026-09.json). A Tombs active is the run player's alone (DungeonRunAdmission); no boss deck lists one.
		// Run reads go through RunHelpers (H1: bosses defeated, ULDA_301), run writes through RunHelpers (ULDA_306's hero power
		// for the rest of the run, ULDA_504's one-fight bonus; both kept after a won fight only). Every random choice uses the
		// game RNG over the scripted cards of the receiving side's generation pool; every piece of match state is an entity, a
		// tag or an enchantment, so clones and AI simulations replay it. ULDA_115 "Gnomebliterator" was left out here: its second
		// half damages the Plague Lord, phase 8 state; phase 8 family 24 scripts it (DungeonRunEncounters.F24.cs, run state
		// DungeonRunState.PlagueLordDamage) and admits it (DungeonRunAdmission.F24.cs).

		/// <summary>The 29 family F12 ids, in the order of the family list (ULDA_115, the one id phase 6 rejected, is family 24's since phase 8).</summary>
		public static readonly IReadOnlyList<string> TombsActiveIds = new[]
		{
			"ULDA_001", "ULDA_004", "ULDA_006", "ULDA_008", "ULDA_012", "ULDA_034", "ULDA_035", "ULDA_035ts", "ULDA_036", "ULDA_036ts",
			"ULDA_041", "ULDA_041ts", "ULDA_043", "ULDA_044", "ULDA_110", "ULDA_112", "ULDA_113", "ULDA_114", "ULDA_115", "ULDA_118",
			"ULDA_202", "ULDA_301", "ULDA_306", "ULDA_307", "ULDA_307ts", "ULDA_403", "ULDA_406", "ULDA_503", "ULDA_504",
		};

		/// <summary>The family F12 id that stays rejected (phase 8: the Plague Lord finale state).</summary>
		public const string Gnomebliterator = "ULDA_115";

		/// <summary>The Ancient Snake ULDA_008 Staff of Scales summons (three), and the Giant Locust of ULDA_036 LOCUUUUSTS!!!.</summary>
		public const string AncientSnake = "ULDA_008t", GiantLocust = "ULDA_036t";
		/// <summary>ULDA_008 Staff of Scales: how many Snakes it summons.</summary>
		public const int StaffOfScalesSnakes = 3;
		/// <summary>ULDA_001 Amakir the Light: how many healing spells each attack adds.</summary>
		public const int AmakirSpells = 2;
		/// <summary>ULDA_012 Stone Fox Statue: how many copies it adds.</summary>
		public const int StoneFoxCopies = 2;
		/// <summary>ULDA_006 Book of the Dead: the damage it deals to each enemy.</summary>
		public const int BookOfTheDeadDamage = 7;
		/// <summary>ULDA_034 Runaway Gyrocopter: the damage its Deathrattle deals to each enemy minion.</summary>
		public const int GyrocopterDamage = 5;
		/// <summary>ULDA_114 Aegis of Death: the damage its Deathrattle deals to the owner's hero.</summary>
		public const int AegisDeathDamage = 100;
		/// <summary>ULDA_406 Murky's Battle Horn: how many Murlocs each wave summons (board permitting).</summary>
		public const int BattleHornMurlocs = 7;
		/// <summary>ULDA_307 Bauble of Beetles: how many of the first friendly minions that died it resurrects.</summary>
		public const int BaubleResurrects = 4;
		/// <summary>ULDA_504 Tea Time: the next fight's extra Mana Crystals and extra cards drawn.</summary>
		public const int TeaTimeMana = 4, TeaTimeDraws = 2;
		/// <summary>ULDA_503 Advanced Targeting Monocle: the most copies one cast casts (H9 loop guard, "RepeatUntil max=60").</summary>
		public const int MonocleMaxCasts = 60;
		/// <summary>The 0-Cost enchantment ULDA_001 and ULDA_012 put on the cards they add: the client's own "Costs (0)." (Wand's Wonder).</summary>
		public const string ZeroCostEnchantment = "LOOT_998le";
		/// <summary>The spell ULDA_112 Bob's Bouncer casts.</summary>
		public const string Brawl = "EX1_407";
		/// <summary>On a hand card: set aside by ULDA_035 Mystical Mirage (the original hand, returned at the end of the turn).</summary>
		public const GameTag MirageKeptTag = (GameTag)34800;
		/// <summary>On a card: in the owner's Mystical Mirage hand (it leaves at the end of the turn, shuffled into the deck).</summary>
		public const GameTag MirageHandTag = (GameTag)34801;

		/// <summary>
		/// ULDA_118 Flex-plosion's "Blow up half your opponent's stuff", defined precisely (an adaptation: the client text names no
		/// numbers): half of the opponent's minions, of the cards in their hand and of the cards in their deck, each rounded up and
		/// picked at random, are destroyed (minions), discarded (hand) and destroyed (deck, revealed as a burn); then the opponent
		/// loses half of their Mana Crystals and their hero takes damage equal to half of its Health, both rounded down (so it can
		/// never kill a hero at full health alone).
		/// </summary>
		public static int FlexHalfUp(int n) => (n + 1) / 2;
		/// <inheritdoc cref="FlexHalfUp"/>
		public static int FlexHalfDown(int n) => n / 2;

		/// <summary>
		/// ULDA_001 Amakir the Light: the healing spells it adds: scripted spells of the owner's generation pool whose text restores
		/// Health ("Restore"), no Secret and no Quest; distinct, ordinal by id.
		/// </summary>
		internal static IReadOnlyList<Card> HealingSpellPool(Game g, Controller c, IEntity source)
		{
			using (g.GenerateFor(c))
				return RandomCardTask.GetCardList(source, CardType.SPELL)
					.Where(card => card.Implemented && card.Type == CardType.SPELL && !card.IsSecret && !card.IsQuest
						&& card.Text != null && card.Text.IndexOf("Restore", StringComparison.Ordinal) >= 0)
					.GroupBy(card => card.Id, StringComparer.Ordinal).Select(x => x.First()).OrderBy(card => card.Id, StringComparer.Ordinal).ToArray();
		}

		/// <summary>ULDA_001: <see cref="AmakirSpells"/> random healing spells into the owner's hand, each costing (0).</summary>
		private static void AmakirHeals(Game g, Controller c, IEntity s)
		{
			var pool = HealingSpellPool(g, c, s);
			if (pool.Count == 0) return;
			var free = new AddEnchantmentTask(ZeroCostEnchantment, EntityType.TARGET);
			for (int i = 0; i < AmakirSpells; i++)
			{
				var card = (Playable)Entity.FromCard(c, pool[g.Random.Next(pool.Count)], new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id });
				if (Generic.AddHandPhase(c, card)) free.Process(g, c, s, card);
			}
			g.OnRandomHappened(true);
		}

		/// <summary>ULDA_004e's Deathrattle: a random scripted Legendary minion of the owner's generation pool, where the minion died.</summary>
		private static void CanopicLegendary(Game g, Controller c, IEntity s, TaskStack stack)
		{
			if (c.BoardZone.IsFull) return;
			IReadOnlyList<Card> pool;
			using (g.GenerateFor(c))
				pool = RandomCardTask.GetCardList(s, CardType.MINION, rarity: Rarity.LEGENDARY)
					.Where(card => card.Implemented && card.Type == CardType.MINION)
					.GroupBy(card => card.Id, StringComparer.Ordinal).Select(x => x.First()).OrderBy(card => card.Id, StringComparer.Ordinal).ToArray();
			if (pool.Count == 0) return;
			var pick = pool[g.Random.Next(pool.Count)];
			g.OnRandomHappened(true);
			new EffectContext(g, c.Id, s.Id, stack: stack).Summon(pick.Id, EffectContext.DeathrattlePosition(c, s));
		}

		/// <summary>ULDA_006 Book of the Dead: the minions (both sides) that died this game (the public death history), the Cost it costs less.</summary>
		public static int MinionsDiedThisGame(Controller c) => c.Game.HistoryFacts.Query.Deaths().Count();

		/// <summary>ULDA_012 Stone Fox Statue: <see cref="StoneFoxCopies"/> copies of the chosen minion's card into the hand, each costing (0).</summary>
		private static void StoneFox(Game g, Controller c, IEntity s, IPlayable t)
		{
			if (!(t is Minion minion)) return;
			var free = new AddEnchantmentTask(ZeroCostEnchantment, EntityType.TARGET);
			for (int i = 0; i < StoneFoxCopies; i++)
			{
				var copy = (Playable)Entity.FromCard(c, minion.Card, new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id });
				if (Generic.AddHandPhase(c, copy)) free.Process(g, c, s, copy);
			}
		}

		/// <summary>
		/// ULDA_035 Mystical Mirage: the owner's Mana Crystals are refreshed; the cards in hand are set aside (kept) and as many
		/// cards are drawn from the deck as the hand held (an empty hand draws none). The Mirage Player Enchant (ULDA_035e) ends it
		/// at the end of the turn (<see cref="MirageEnd"/>). A second Mirage the same turn (the Twinspell copy) sets the Mirage
		/// hand aside the same way; its cards still go back to the deck at the end of the turn.
		/// </summary>
		private static void Mirage(Game g, Controller c, IEntity s, TaskStack stack)
		{
			var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
			ctx.RefreshMana(c.UsedMana);
			// The engine's Twinspell rule put this cast's copy in hand before the effect resolves: it is part of the Mirage hand,
			// so it can still be cast this turn (and goes into the deck at the turn's end unless it is).
			var twin = s is IPlayable cast && cast.Card[GameTag.TWINSPELL_COPY] != 0
				? c.HandZone.Where(p => p.Card.AssetId == cast.Card[GameTag.TWINSPELL_COPY]).OrderByDescending(p => p.Id).FirstOrDefault() : null;
			if (twin != null) twin[MirageHandTag] = 1;
			var hand = c.HandZone.Where(p => p != twin).ToArray();
			// The hand escrow HandTimelineRules uses: held modifiers stay, hand auras reattach once the card returns.
			using (c.HandZone.BeginCardStateMutation())
				foreach (var card in hand)
				{
					if (card[MirageHandTag] == 0) card[MirageKeptTag] = 1;
					CardStateBoundary.EnrollAttachments(card);
					c.HandZone.DetachForEscrow(card);
					c.SetasideZone.Add(card);
				}
			for (int i = 0; i < hand.Length && c.DeckZone.Count > 0 && !c.HandZone.IsFull; i++)
			{
				var drawn = Generic.Draw(c);
				if (drawn != null) drawn[MirageHandTag] = 1;
			}
			new AddEnchantmentTask("ULDA_035e", EntityType.CONTROLLER).Process(g, c, s, null);
		}

		/// <summary>
		/// The end of a Mystical Mirage turn (ULDA_035e, at the owner's turn end): the Mirage hand (every card in hand, drawn or
		/// gained this turn, and every Mirage card set aside by a second Mirage) is shuffled into the deck; the kept hand comes
		/// back, in its order (a card that finds the hand full is shuffled into the deck). Runs once however many Mirages were cast.
		/// </summary>
		internal static void MirageEnd(Game g, Controller c, IEntity s)
		{
			// Every Mirage of the turn left an enchantment; the first to fire ends the Mirage and removes them all, so the others,
			// already queued for this turn end, find theirs gone and do nothing.
			if (!(s is Enchantment self) || c.AppliedEnchantments?.Contains(self) != true) return;
			foreach (var mark in c.AppliedEnchantments.Where(e => e.Card.Id == "ULDA_035e").ToArray()) mark.Remove();
			var kept = c.SetasideZone.Where(p => p[MirageKeptTag] == 1).ToList();
			var mirage = c.HandZone.ToList().Concat(c.SetasideZone.Where(p => p[MirageHandTag] == 1 && p[MirageKeptTag] == 0)).ToList();
			foreach (var card in mirage)
			{
				card[MirageHandTag] = 0;
				card.Zone.Remove(card);
				Generic.ShuffleIntoDeck(c, s, card);
			}
			using (c.HandZone.BeginCardStateMutation())
				foreach (var card in kept)
				{
					card[MirageKeptTag] = 0;
					card[MirageHandTag] = 0;
					c.SetasideZone.Remove(card);
					if (c.HandZone.IsFull) { Generic.ShuffleIntoDeck(c, s, card); continue; }
					card[GameTag.ZONE] = (int)Zone.HAND;
					Generic.AddHandPhase(c, card);
				}
		}

		/// <summary>
		/// ULDA_036 LOCUUUUSTS!!!: the owner's board fills with Giant Locusts (2/2); then each Locust, in board order, attacks the
		/// chosen enemy while both still stand (a forced attack, as Mogor's: no Rush needed, Taunt ignored).
		/// </summary>
		private static void Locusts(Game g, Controller c, IEntity s, IPlayable t)
		{
			if (!(t is ICharacter target)) return;
			var locusts = new List<Minion>();
			var card = Cards.FromId(GiantLocust);
			while (!c.BoardZone.IsFull)
			{
				var locust = (Minion)Entity.FromCard(c, card, new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id });
				Generic.SummonBlock(g, locust, -1, s);
				if (locust.Zone?.Type != Zone.PLAY) break;
				locusts.Add(locust);
			}
			g.DeathProcessingAndAuraUpdate();
			var attack = new AttackTask(EntityType.SOURCE, EntityType.TARGET);
			foreach (var locust in locusts)
			{
				if (!Standing(target) || !Standing(locust)) continue;
				attack.Process(g, c, locust, target);
				g.DeathProcessingAndAuraUpdate();
			}
		}

		// A hero is in no zone; a minion stands while it is in play.
		private static bool Standing(ICharacter character) =>
			character != null && !character.ToBeDestroyed && character.Health > 0 && (character is Hero || character.Zone?.Type == Zone.PLAY);

		/// <summary>
		/// ULDA_112 Bob's Bouncer: Brawl (EX1_407) is cast by the effect; if the chosen minion survives it, the owner's board fills
		/// with copies of it (as the minion now stands).
		/// </summary>
		private static void BobsBouncer(Game g, Controller c, IEntity s, IPlayable t, TaskStack stack)
		{
			if (!(t is Minion chosen)) return;
			var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
			var brawl = (Spell)Entity.FromCard(c, Cards.FromId(Brawl), new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id });
			ctx.CastSpell(brawl.Id, HistoryCastKind.Effect, null);
			g.DeathProcessingAndAuraUpdate();
			if (!Standing(chosen)) return;
			var copy = new SummonCopyTask(EntityType.TARGET);
			RepeatUntil(() => c.BoardZone.IsFull || !Standing(chosen), 7, () =>
				copy.Process(g, c, s, chosen) == TaskState.COMPLETE);
		}

		/// <summary>ULDA_113 Crusty the Crustacean's Battlecry: the target is destroyed; Crusty gains twice its Attack and Health (Om Nom Nom).</summary>
		private static void Crusty(Game g, Controller c, IEntity s, IPlayable t)
		{
			if (!(t is Minion victim) || !(s is Minion crusty)) return;
			int attack = 2 * Math.Max(0, victim.AttackDamage), health = 2 * Math.Max(0, victim.Health);
			victim.Destroy();
			if (attack > 0 || health > 0) Generic.AddEnchantmentBlock(g, Cards.FromId("ULDA_113e"), crusty, crusty, attack, health);
		}

		/// <summary>
		/// ULDA_202 Kodo Hide Whip: after the owner's hero attacks a minion that survives, that minion leaves the board for the
		/// owner's hand (as a return to hand: its enchantments and damage gone, now the owner's card). A full hand leaves it.
		/// </summary>
		private static void KodoWhip(Game g, Controller c, IEntity s)
		{
			if (!(g.CurrentEventData?.EventTarget is Minion minion) || !Standing(minion) || minion.Controller == c || c.HandZone.IsFull) return;
			var from = minion.Controller;
			DarkGifts.RemoveForBackwardMove(minion, Zone.PLAY, Zone.HAND);
			if (!Generic.RemoveFromZone(from, minion)) return;
			g.AuraUpdate();
			minion.Reset();
			minion.Controller = c;
			minion[GameTag.CONTROLLER] = c.PlayerId;
			using var historyReturn = g.Facts.HandEntry(minion, HistoryHandEntryKind.Other, Zone.PLAY);
			Generic.AddHandPhase(c, minion);
		}

		/// <summary>
		/// ULDA_118 Flex-plosion (<see cref="FlexHalfUp"/>): half the opponent's minions, hand and deck (rounded up, at random),
		/// then half their Mana Crystals and half their hero's Health (rounded down).
		/// </summary>
		private static void FlexPlosion(Game g, Controller c, IEntity s, TaskStack stack)
		{
			var op = c.Opponent;
			var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
			var minions = op.BoardZone.GetAll().Where(m => !m.ToBeDestroyed && !m.Untouchable).ToList();
			var kill = minions.ChooseNElements(FlexHalfUp(minions.Count), g.Random).ToList();
			foreach (var m in kill) ctx.Destroy(m.Id);
			var hand = op.HandZone.ToList();
			var discard = hand.ChooseNElements(FlexHalfUp(hand.Count), g.Random).ToList();
			foreach (var card in discard) if (card.Zone == op.HandZone) Generic.DiscardBlock(op, card);
			var deck = op.DeckZone.ToList();
			var burn = deck.ChooseNElements(FlexHalfUp(deck.Count), g.Random).ToList();
			// A burn from the deck is revealed (as RemoveFromDeck reveals): the boss's own-deck ledger reads the SHOW before the exit.
			if (g.History) foreach (var card in burn) g.PowerHistory.Add(PowerHistoryBuilder.ShowEntity(card));
			if (burn.Count > 0) new MoveToGraveYard(EntityType.STACK, destroy: true).Process(g, c, s, null, new TaskStack { Playables = burn });
			g.OnRandomHappened(true);
			g.DeathProcessingAndAuraUpdate();
			int crystals = FlexHalfDown(op.BaseMana);
			if (crystals > 0) Generic.ChangeManaCrystal(op, -crystals, true);
			if (op.Hero != null && Standing(op.Hero))
			{
				int damage = FlexHalfDown(op.Hero.Health);
				if (damage > 0) ctx.Damage(op.Hero.Id, damage);
			}
		}

		/// <summary>
		/// ULDA_301 Map of Uldum: one Discover of a card (minion, spell or weapon of the owner's class or neutral) and one empty
		/// Mana Crystal for each boss the run has defeated before this fight (RunHelpers.Bosses; none outside a run match).
		/// </summary>
		private static void MapOfUldum(Game g, Controller c, IEntity s, TaskStack stack)
		{
			int bosses = RunHelpers.Bosses(g);
			if (bosses <= 0) return;
			var pool = ClassDiscoverPool(g, c, s, CardType.MINION).Concat(ClassDiscoverPool(g, c, s, CardType.SPELL))
				.Concat(ClassDiscoverPool(g, c, s, CardType.WEAPON)).OrderBy(card => card.Id, StringComparer.Ordinal).ToArray();
			var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
			if (pool.Length > 0)
				for (int i = 0; i < bosses; i++) ctx.Discover(ctx.SampleDistinct(pool, 3), ChoiceAction.HAND);
			Generic.ChangeManaCrystal(c, bosses, false);
		}

		/// <summary>
		/// ULDA_306 Stolen Titan Secrets: the owner's hero power becomes a copy of the opponent's (a start-of-game passive runs
		/// now, as it would have at the start); for the run player it stays for the rest of the run (RunHelpers.OverridePower,
		/// kept after a won fight). A boss that came to hold it copies for this match only.
		/// </summary>
		private static void StolenTitanSecrets(Game g, Controller c, IEntity s)
		{
			var theirs = c.Opponent.Hero?.HeroPower;
			if (theirs == null) return;
			var card = theirs.Card;
			new ReplaceHeroPower(card).Process(g, c, s, null);
			if (HeroPower.ActivatesAtGameStart(card)) c.Hero.HeroPower.ActivateTask();
			if (c == g.Player1) RunHelpers.OverridePower(g, card.Id);
		}

		/// <summary>
		/// ULDA_307 Bauble of Beetles: the first <see cref="BaubleResurrects"/> friendly minions that died this game (the public
		/// death history, oldest first) are summoned anew (their cards, as resurrection does), each given Reborn, board permitting.
		/// </summary>
		private static void BaubleOfBeetles(Game g, Controller c, IEntity s)
		{
			// A dead run shrine (phase 7) is never resurrected.
			var first = g.HistoryFacts.Query.Deaths().Where(f => f.ControllerId == c.Id && f.DefinitionKnown
				&& !(g.IdEntityDic.TryGetValue(f.EntityId, out var dead) && ShrineRules.IsMarked(dead))).Take(BaubleResurrects).ToList();
			foreach (var death in first)
			{
				if (c.BoardZone.IsFull) break;
				var minion = (Minion)Entity.FromCard(c, death.Definition.Materialize(), new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id });
				Generic.SummonBlock(g, minion, -1, s);
				if (minion.Zone?.Type == Zone.PLAY) minion[GameTag.REBORN] = 1;
			}
		}

		/// <summary>ULDA_403 Ancient Reflections: the owner's board fills with copies of the chosen minion, each made 1/1 (Titan Hologram).</summary>
		private static void AncientReflections(Game g, Controller c, IEntity s, IPlayable t)
		{
			if (!(t is Minion chosen)) return;
			var hologram = new AddEnchantmentTask("ULDA_403e", EntityType.STACK);
			var copy = new SummonCopyTask(EntityType.TARGET, addToStack: true);
			RepeatUntil(() => c.BoardZone.IsFull, 7, () =>
			{
				var stack = new TaskStack { Playables = new List<IPlayable>() };
				if (copy.Process(g, c, s, chosen, stack) != TaskState.COMPLETE || stack.Playables.Count == 0) return false;
				hologram.Process(g, c, s, null, stack);
				return true;
			});
		}

		/// <summary>ULDA_406 Murky's Battle Horn: one wave: up to <see cref="BattleHornMurlocs"/> random scripted Murlocs, each given Rush (To Mrrgl!).</summary>
		private static void BattleHornWave(Game g, Controller c, IEntity s)
		{
			IReadOnlyList<Card> pool;
			using (g.GenerateFor(c))
				pool = RandomCardTask.GetCardList(s, CardType.MINION).Where(card => card.Implemented && card.Type == CardType.MINION && card.IsRace(Race.MURLOC))
					.GroupBy(card => card.Id, StringComparer.Ordinal).Select(x => x.First()).OrderBy(card => card.Id, StringComparer.Ordinal).ToArray();
			if (pool.Count == 0) return;
			var rush = new AddEnchantmentTask("ULDA_406e", EntityType.TARGET);
			for (int i = 0; i < BattleHornMurlocs && !c.BoardZone.IsFull; i++)
			{
				var murloc = (Minion)Entity.FromCard(c, pool[g.Random.Next(pool.Count)], new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id });
				Generic.SummonBlock(g, murloc, -1, s);
				if (murloc.Zone?.Type == Zone.PLAY) rush.Process(g, c, s, murloc);
			}
			g.OnRandomHappened(true);
		}

		/// <summary>
		/// ULDA_503 Advanced Targeting Monocle: a copy of each spell in the owner's deck (as the deck holds them at the cast, deck
		/// order) is cast with random targets and choices (<see cref="CastRandomly"/>), guarded at <see cref="MonocleMaxCasts"/>
		/// copies; another Monocle, a Quest and a Secret already in play are not cast (a Monocle's copy would cast the deck again).
		/// </summary>
		private static void Monocle(Game g, Controller c, IEntity s)
		{
			var spells = c.DeckZone.OfType<Spell>().Select(p => p.Card).Where(card => card.Id != "ULDA_503" && !card.IsQuest).ToList();
			int index = 0;
			RepeatUntil(() => index >= spells.Count || HeroDown(c) || HeroDown(c.Opponent), MonocleMaxCasts, () =>
			{
				var card = spells[index++];
				if (card.IsSecret && c.SecretZone.Any(p => p.Card.Id == card.Id)) return true;
				var copy = (Spell)Entity.FromCard(c, card, new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id });
				CastRandomly(g, c, copy);
				return true;
			});
		}

		private static void TombsActives()
		{
			// ---------------- enchantments (the client's own shells, names and texts)
			Effect("fx_ULDA_004e", "ULDA_004e", "Canopic Jars", "<b>Deathrattle:</b> Summon a random <b>Legendary</b> minion.",
				() => new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => CanopicLegendary(g, c, s, stack)) });
			Effect("fx_ULDA_035e", "ULDA_035e", "Mirage Player Enchant", null,
				() => new Power
				{
					Trigger = new Trigger(TriggerType.TURN_END)
					{
						TriggerSource = TriggerSource.FRIENDLY,
						SingleTask = new CustomTask((g, c, s, t, stack) => MirageEnd(g, c, s)),
					},
				});
			Buff("fx_ULDA_041e", "ULDA_041e", "Royal Fervor", "<b>Windfury</b> and <b>Immune</b> this turn.", Effects.Windfury, Effects.Immune);
			Effect("fx_ULDA_041e2", "ULDA_041e2", "Royal Fervor", "Your minions have <b>Windfury</b> and <b>Immune</b> this turn.",
				() => new Power { Aura = new Aura(AuraType.BOARD, "ULDA_041e") { RemoveTrigger = (TriggerType.TURN_END, null) } }, oneTurn: true);
			Buff("fx_ULDA_043e", "ULDA_043e", "Phaoris' Fury", "Increased stats.",
				Effects.Attack_N(2), new SabberStoneCore.Enchants.Effect(GameTag.DURABILITY, EffectOperator.ADD, 1));
			Buff("fx_ULDA_044e", "ULDA_044e", "Sun's Flames", "Increased attack.", Effects.Attack_N(2));
			Effect("fx_ULDA_113e", "ULDA_113e", "Om Nom Nom", "Increased stats.", () => new Power { Enchant = Enchants.Enchants.AddAttackHealthScriptTag });
			Buff("fx_ULDA_403e", "ULDA_403e", "Titan Hologram", "1/1.", Effects.SetAttackHealth(1));
			Buff("fx_ULDA_406e", "ULDA_406e", "To Mrrgl!", "<b>Rush</b>", Effects.Rush);
			Effect("fx_ULDA_406e2", "ULDA_406e2", "Murky's Battle Horn Player Enchantment", null,
				() => new Power
				{
					Trigger = new Trigger(TriggerType.TURN_START)
					{
						TriggerSource = TriggerSource.FRIENDLY,
						RemoveAfterTriggered = true,
						SingleTask = new CustomTask((g, c, s, t, stack) =>
						{
							if (s is Enchantment self) self.Remove();
							BattleHornWave(g, c, s);
						}),
					},
				});

			// ---------------- tokens
			Active(AncientSnake, "Ancient Snake", "<b>Rush</b>\n<b>Poisonous</b>\n<b>Reborn</b>", 1, () => new CardDef());
			Active(GiantLocust, "Giant Locust", null, 2, () => new CardDef());

			// ---------------- minions
			Active("ULDA_001", "Amakir the Light", "After this minion attacks, add 2 random healing spells to your hand. They cost (0).", 3,
				() => Def(new Power
				{
					Trigger = new Trigger(TriggerType.AFTER_ATTACK) { TriggerSource = TriggerSource.SELF, SingleTask = new CustomTask((g, c, s, t, stack) => AmakirHeals(g, c, s)) },
				}));
			Active("ULDA_034", "Runaway Gyrocopter", "<b>Rush</b>. <b>Deathrattle:</b> Deal 5 damage to all enemy minions, then shuffle\nthis into your deck.", 5,
				() => Def(new Power { DeathrattleTask = ComplexTask.Create(new DamageTask(GyrocopterDamage, EntityType.OP_MINIONS), new AddCardTo("ULDA_034", EntityType.DECK)) }));
			Active("ULDA_110", "Crawling Claw", "[x]<b>Rush</b>. <b>Reborn</b>.\n<b>Deathrattle:</b> Steal a card from\nyour opponent's hand.", 3,
				() => Def(new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => LassoSteal(g, c, s)) }));
			Active("ULDA_113", "Crusty the Crustacean", "<b>Battlecry:</b> Destroy a minion.\nGain double its Attack and Health.", 1,
				() => Def(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => Crusty(g, c, s, t)) }, PlayReq.REQ_TARGET_IF_AVAILABLE, PlayReq.REQ_MINION_TARGET));

			// ---------------- weapons
			Active("ULDA_043", "Phaoris' Blade", "<b>Windfury</b>.\nAfter your hero attacks and kills a minion, this gains +2/+1.", 5,
				() => Def(new Power
				{
					Trigger = new Trigger(TriggerType.AFTER_ATTACK)
					{
						TriggerSource = TriggerSource.HERO,
						Condition = SelfCondition.IsEventTargetIs(CardType.MINION),
						SingleTask = ComplexTask.Create(
							new ConditionTask(EntityType.EVENT_TARGET, SelfCondition.IsDead),
							new FlagTask(true, new AddEnchantmentTask("ULDA_043e", EntityType.WEAPON))),
					},
				}),
				card => card.Tags[GameTag.WINDFURY] = 1);
			Active("ULDA_044", "Blade of the Burning Sun", "<b>Battlecry:</b> Give all minions in your deck +2 Attack.\n<b>Deathrattle:</b> Return this to your hand.", 4,
				() => Def(new Power
				{
					PowerTask = ComplexTask.Create(new IncludeTask(EntityType.DECK), new FilterStackTask(SelfCondition.IsMinion), new AddEnchantmentTask("ULDA_044e", EntityType.STACK)),
					DeathrattleTask = new AddCardTo("ULDA_044", EntityType.HAND),
				}),
				// The client card names both in its text but carries the tags only as references.
				card => { card.Tags[GameTag.BATTLECRY] = 1; card.SetPrintedDeathrattle(); });
			Active("ULDA_114", "Aegis of Death", "[x]Your hero is <b>Immune</b>.\nEach turn, lose 1 Durabilty.\n<b>Deathrattle:</b> Deal 100\ndamage to your hero.", 1,
				() => Def(new Power
				{
					Aura = new Aura(AuraType.HERO, Effects.Immune),
					// "Each turn": at the end of every turn, the owner's and the opponent's.
					Trigger = new Trigger(TriggerType.TURN_END) { EitherTurn = true, SingleTask = new DamageWeaponTask(false) },
					DeathrattleTask = new DamageTask(AegisDeathDamage, EntityType.HERO),
				}));
			Active("ULDA_202", "Kodo Hide Whip", "After your hero attacks a minion and it survives, put that minion in your hand.", 2,
				() => Def(new Power
				{
					Trigger = new Trigger(TriggerType.AFTER_ATTACK)
					{
						TriggerSource = TriggerSource.HERO,
						Condition = SelfCondition.IsEventTargetIs(CardType.MINION),
						SingleTask = new CustomTask((g, c, s, t, stack) => KodoWhip(g, c, s)),
					},
				}));

			// ---------------- spells
			Active("ULDA_004", "Canopic Jars", "[x]Give your minions\n\"<b>Deathrattle:</b> Summon\na random <b>Legendary</b>\nminion.\"", 3,
				() => Def(new Power { PowerTask = new AddEnchantmentTask("ULDA_004e", EntityType.MINIONS) }));
			Active("ULDA_006", "Book of the Dead", "Deal $7 damage to all enemies. Costs (1) less for each minion that's died this game.", 12,
				() => Def(new Power
				{
					PowerTask = new DamageTask(BookOfTheDeadDamage, EntityType.ENEMIES, true),
					Aura = new AdaptiveCostEffect(p => MinionsDiedThisGame(p.Controller)),
				}),
				card => card.ConsumesHistory = true);
			Active("ULDA_008", "Staff of Scales", "Summon three 1/1 Snakes with <b>Rush</b>, <b>Poisonous</b> and <b>Reborn</b>.", 5,
				() => Def(new Power { PowerTask = new SummonTask(AncientSnake, StaffOfScalesSnakes) }), null, AncientSnake);
			Active("ULDA_012", "Stone Fox Statue", "Choose a minion and add 2 copies of it to your hand that cost (0).", 5,
				() => Def(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => StoneFox(g, c, s, t)) }, PlayReq.REQ_TARGET_TO_PLAY, PlayReq.REQ_MINION_TARGET));
			Active("ULDA_035", "Mystical Mirage", "<b>Twinspell</b>\nRefresh your Mana Crystals and get a new hand this turn.", 0,
				() => Def(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => Mirage(g, c, s, stack)) }), null, "ULDA_035ts");
			Active("ULDA_035ts", "Mystical Mirage", "Refresh your Mana Crystals and get a new hand this turn.", 0,
				() => Def(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => Mirage(g, c, s, stack)) }));
			Active("ULDA_036", "LOCUUUUSTS!!!", "<b>Twinspell</b>.\nChoose an enemy.\nFill your board with 2/2 Locusts that attack it.", 3,
				() => Def(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => Locusts(g, c, s, t)) }, PlayReq.REQ_TARGET_TO_PLAY, PlayReq.REQ_ENEMY_TARGET),
				null, "ULDA_036ts", GiantLocust);
			Active("ULDA_036ts", "LOCUUUUSTS!!!", "Choose an enemy.\nFill your board with 2/2 Locusts that attack it.", 3,
				() => Def(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => Locusts(g, c, s, t)) }, PlayReq.REQ_TARGET_TO_PLAY, PlayReq.REQ_ENEMY_TARGET),
				null, GiantLocust);
			Active("ULDA_041", "Staff of Ammunae", "<b>Twinspell</b>.\nYour minions have <b>Windfury</b> and <b>Immune</b> this turn.", 4,
				() => Def(new Power { PowerTask = new AddEnchantmentTask("ULDA_041e2", EntityType.CONTROLLER) }), null, "ULDA_041ts");
			Active("ULDA_041ts", "Staff of Ammunae", "Your minions have <b>Windfury</b> and <b>Immune</b> this turn.", 4,
				() => Def(new Power { PowerTask = new AddEnchantmentTask("ULDA_041e2", EntityType.CONTROLLER) }));
			Active("ULDA_112", "Bob's Bouncer", "[x]Choose a minion\nand then cast 'Brawl.'\nIf that minion survives, fill\nyour board with copies of it.", 4,
				() => Def(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => BobsBouncer(g, c, s, t, stack)) }, PlayReq.REQ_TARGET_TO_PLAY, PlayReq.REQ_MINION_TARGET));
			Active("ULDA_118", "Flex-plosion", "Blow up half your opponent's stuff.", 7,
				() => Def(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => FlexPlosion(g, c, s, stack)) }));
			Active("ULDA_301", "Map of Uldum",
				"<b>Discover</b> a card and gain a Mana Crystal for each <b>Boss</b> you've defeated this run.@<b>Discover</b> {0} |4(card, cards).\nGain {0} Mana |4(Crystal, Crystals).", 0,
				() => Def(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => MapOfUldum(g, c, s, stack)) }));
			Active("ULDA_306", "Stolen Titan Secrets", "Copy the <b>Boss's</b> Hero Power for the rest of this run.", 1,
				() => Def(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => StolenTitanSecrets(g, c, s)) }));
			Active("ULDA_307", "Bauble of Beetles", "[x]<b>Twinspell</b>. Resurrect\nthe first 4 friendly minions\nthat died this game.\nGive them <b>Reborn</b>.", 4,
				() => Def(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => BaubleOfBeetles(g, c, s)) }),
				card => card.ConsumesHistory = true, "ULDA_307ts");
			Active("ULDA_307ts", "Bauble of Beetles", "[x]Resurrect the first\n4 friendly minions\nthat died this game.\nGive them <b>Reborn</b>.", 4,
				() => Def(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => BaubleOfBeetles(g, c, s)) }),
				card => card.ConsumesHistory = true);
			Active("ULDA_403", "Ancient Reflections", "Choose a minion.\nFill your board with 1/1 copies of it.", 5,
				() => Def(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => AncientReflections(g, c, s, t)) }, PlayReq.REQ_TARGET_TO_PLAY, PlayReq.REQ_MINION_TARGET));
			Active("ULDA_406", "Murky's Battle Horn", "Summon 7 Murlocs and give them <b>Rush</b>.\nAt the start of your next turn, do it again.", 7,
				() => Def(new Power
				{
					PowerTask = ComplexTask.Create(
						new CustomTask((g, c, s, t, stack) => BattleHornWave(g, c, s)),
						new AddEnchantmentTask("ULDA_406e2", EntityType.CONTROLLER)),
				}));
			Active("ULDA_503", "Advanced Targeting Monocle", "Cast a copy of all spells in your deck <i>(targets chosen randomly)</i>.", 5,
				() => Def(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => Monocle(g, c, s)) }));
			Active("ULDA_504", "Tea Time", "Gain 4 Mana Crystals and draw 2 extra cards <b>for the next boss only</b>.", 2,
				() => Def(new Power
				{
					// For the next fight only (RunHelpers.GrantNextFight, applied by the run layer after a won fight); nothing in this match.
					PowerTask = new CustomTask((g, c, s, t, stack) => { if (c == g.Player1) RunHelpers.GrantNextFight(g, TeaTimeMana, TeaTimeDraws); }),
				}));
		}
	}
}
