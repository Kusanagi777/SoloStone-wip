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
		// ---------------------------------------------------------------- TombsSignatures
		// The Tombs of Terror signature treasures (phase 6, family F11): the client's ADVENTURE_LOADOUT_TREASURES rows of adventure
		// 513, six per explorer (classId 4 Reno, 5 Finley, 2 Elise, 3 Brann), with the Jr. to Sr. upgrades (ULDA_016 to 021,
		// ULDA_015 to 020, ULDA_017 to 022, ULDA_018 to 023) and Pack Mule (ULDA_019, upgrading to ULDA_024 when an act is
		// cleared). ULDA_302 Academic Research (Elise's fifth) is a Start of Game draw and belongs to family F15. Every card,
		// token and enchantment is keyed and shelled by its own client id, so the run deck, the loadout picker and the match
		// show the real card; every name and text is the client's own, word for word (facts/violet-hold-2026-09.json). A
		// signature is the run player's alone (DungeonRunAdmission; run-v1.json scopes it to its explorer's dual row and never
		// offers it in a draft); no boss deck lists one. Every random choice uses the game RNG over the scripted cards of the
		// generation pool for the receiving side (run exclusions honoured); every piece of match state is an entity tag or an
		// enchantment, so clones and AI simulations replay it.

		/// <summary>The 29 family F11 ids: the explorers' signatures (Reno, Finley, Elise, Brann, Jr. then Sr.) and Pack Mule.</summary>
		public static readonly IReadOnlyList<string> TombsSignatureIds = new[]
		{
			"ULDA_203", "ULDA_201", "ULDA_204", "ULDA_016", "ULDA_021", "ULDA_206", "ULDA_207",
			"ULDA_502", "ULDA_501", "ULDA_505", "ULDA_015", "ULDA_020", "ULDA_507", "ULDA_010",
			"ULDA_303", "ULDA_305", "ULDA_003", "ULDA_017", "ULDA_022", "ULDA_304",
			"ULDA_401", "ULDA_402", "ULDA_404", "ULDA_018", "ULDA_023", "ULDA_002", "ULDA_405",
			"ULDA_019", "ULDA_024",
		};

		/// <summary>ULDA_016 Jr. and ULDA_021 Sr. Tomb Diver: the Secrets played this game their Battlecry needs.</summary>
		public const int JrTombDiverSecrets = 6, SrTombDiverSecrets = 3;
		/// <summary>ULDA_204 Reno's Magical Torch: the damage the printed card deals, and the more each Combo copy deals.</summary>
		public const int MagicalTorchDamage = 4, MagicalTorchStep = 2;
		/// <summary>ULDA_207 The Gatling Wand: the damage before any spell was cast this game (one more for each).</summary>
		public const int GatlingWandBase = 3;
		/// <summary>ULDA_304 Staff of Renewal: how many minions it resurrects.</summary>
		public const int StaffOfRenewalCount = 7;
		/// <summary>ULDA_507 Karl the Lost: how many Silver Hand Recruits it summons.</summary>
		public const int KarlRecruits = 6;
		/// <summary>ULDA_404 Titan-Forged Grapnel: how many enemy minions it destroys.</summary>
		public const int GrapnelKills = 2;
		/// <summary>The minion Scales of Justice transforms every minion into: the 1/1 Murloc Scout (an adaptation: the client names no token).</summary>
		public const string ScalesMurloc = "EX1_506a";
		/// <summary>The 0-Cost enchantment Scales of Justice puts on its Murlocs: the client's own "Costs (0)." (Wand's Wonder).</summary>
		public const string ScalesCostEnchantment = "LOOT_998le";
		/// <summary>The Treant Elise's Machete summons, and the "Has Rush." enchantment it gives each (the client's Reaving).</summary>
		public const string MacheteTreant = "EX1_158t", MacheteRush = "DAL_070e";
		/// <summary>The Taunt enchantment Staff of Renewal gives each minion (the client's "Training Complete").</summary>
		public const string StaffTaunt = "AT_069e";
		/// <summary>King Krush, the minion Brann's Epic Egg summons.</summary>
		public const string KingKrush = "EX1_543";
		/// <summary>On a spell: cast by Lei Flamepaw as the "again" of another cast, so it is never cast again itself.</summary>
		public const GameTag LeiRecastTag = (GameTag)34700;
		/// <summary>The run deck origin Pack Mule (ULDA_019) reads (DungeonRunDeck.OriginStarter in the run layer).</summary>
		public const string StarterOrigin = "starter";

		/// <summary>
		/// The treasures a Tombs effect gives (ULDA_002 Flo Slatebrand; ULDA_039 Uldum Treasure Cache): the run's Tombs of
		/// Terror treasure pool when it holds at least <paramref name="atLeast"/>, else the run's whole treasure pool (an
		/// adaptation while the Tombs actives, family F12, are not in the run: Pack Mule alone would make every Discover the same).
		/// </summary>
		internal static IReadOnlyList<Card> TombsTreasureCandidates(Controller c, int atLeast = 3, params string[] exclude)
		{
			var tombs = TreasureCandidates(c, TombsPool, null, exclude);
			return tombs.Count >= atLeast ? tombs : TreasureCandidates(c, null, null, exclude);
		}

		/// <summary>
		/// ULDA_002 Flo Slatebrand: a random treasure of <see cref="TombsTreasureCandidates"/> to the owner's hand. The pool is
		/// the run player's (Player1, the side RunContext describes); a boss that came to hold the card adds none.
		/// </summary>
		private static void AddRandomTombsTreasure(Game g, Controller c, IEntity s)
		{
			if (c != g.Player1 || c.HandZone.IsFull) return;
			var pool = TombsTreasureCandidates(c);
			if (pool.Count == 0) return;
			var card = pool[g.Random.Next(pool.Count)];
			g.OnRandomHappened(true);
			Generic.AddHandPhase(c, Entity.FromCard(c, card, new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id }));
		}

		/// <summary>
		/// ULDA_201 Reno's Crafty Lasso: after the owner's hero attacks, a random card of the opponent's hand moves to the
		/// owner's hand by the hand-to-hand theft rule (Gloves of Mugging's): its cost kept, a full hand burns it.
		/// </summary>
		private static void LassoSteal(Game g, Controller c, IEntity s)
		{
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

		/// <summary>ULDA_204 Reno's Magical Torch: the damage this entity deals (its script data, the printed 4 when unset).</summary>
		public static int MagicalTorchAmount(IPlayable torch) =>
			torch[GameTag.TAG_SCRIPT_DATA_NUM_1] > 0 ? torch[GameTag.TAG_SCRIPT_DATA_NUM_1] : MagicalTorchDamage;

		/// <summary>
		/// ULDA_204 Reno's Magical Torch: its damage (Spell Damage applies) to the target; with Combo, a copy that deals
		/// <see cref="MagicalTorchStep"/> more is shuffled into the owner's deck (the copy carries its damage as script data).
		/// </summary>
		private static void MagicalTorch(Game g, Controller c, IEntity s, IPlayable t, TaskStack stack)
		{
			var torch = (IPlayable)s;
			int amount = MagicalTorchAmount(torch);
			bool combo = c.IsComboActive;
			var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
			if (t is ICharacter) ctx.Damage(t.Id, amount, spellDamage: true);
			if (!combo) return;
			var copy = Entity.FromCard(c, torch.Card, new EntityData
			{
				[GameTag.TAG_SCRIPT_DATA_NUM_1] = amount + MagicalTorchStep, [GameTag.DISPLAYED_CREATOR] = s.Id,
			});
			Generic.ShuffleIntoDeck(c, s, copy);
		}

		/// <summary>ULDA_207 The Gatling Wand: its damage now (the base plus the spells its owner has cast this game).</summary>
		public static int GatlingWandAmount(Controller c) => GatlingWandBase + c.NumSpellsPlayedThisGame;

		/// <summary>
		/// ULDA_206 Lei Flamepaw: after its owner casts a spell, a copy of it is cast again by the owner on the same target
		/// (the cast spell's target when it is still a legal target, else nothing for a targeted spell; an untargeted spell is
		/// cast again as it is). A copy Lei casts carries <see cref="LeiRecastTag"/> and is never cast again (two Leis cast the
		/// original twice, never each other's copies); a Secret or a Quest is not cast again (a second copy could not be played).
		/// </summary>
		private static void LeiFlamepaw(Game g, Controller c, IEntity s, IPlayable cast, TaskStack stack)
		{
			if (!(cast is Spell spell) || spell.Controller != c || spell[LeiRecastTag] != 0 || spell.IsSecret || spell.IsQuest) return;
			if (g.State == State.COMPLETE || !(s is Minion lei) || lei.Zone?.Type != Zone.PLAY) return;
			var copy = (Spell)Entity.FromCard(c, spell.Card, new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id, [LeiRecastTag] = 1 });
			if (spell[GameTag.TAG_SCRIPT_DATA_NUM_1] != 0) copy[GameTag.TAG_SCRIPT_DATA_NUM_1] = spell[GameTag.TAG_SCRIPT_DATA_NUM_1];
			var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
			int? target = null;
			bool targeted = spell.Card.TargetingType != TargetingType.None || spell.Card.RequiresTarget;
			if (spell.CardTarget > 0 && g.IdEntityDic.TryGetValue(spell.CardTarget, out var was) && was is ICharacter character
				&& !character.ToBeDestroyed && (character is Hero || character.Zone?.Type == Zone.PLAY))
				target = ctx.RandomLegalTarget(copy.Id, new[] { character.Id });
			if (targeted && target == null) return;
			ctx.CastSpell(copy.Id, HistoryCastKind.Effect, target);
			g.DeathProcessingAndAuraUpdate();
		}

		/// <summary>ULDA_017 Jr. and ULDA_022 Sr. Navigator: two copies of the spell its owner targeted it with, to the hand.</summary>
		private static void Navigator(Game g, Controller c, IEntity s, IPlayable cast)
		{
			if (!(cast is Spell spell) || spell.Controller != c) return;
			for (int i = 0; i < 2 && !c.HandZone.IsFull; i++)
				Generic.AddHandPhase(c, Entity.FromCard(c, spell.Card, new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id }));
		}

		/// <summary>
		/// The scripted cards of <paramref name="type"/> a Discover of <paramref name="c"/>'s may offer: its classes' and the
		/// neutral ones of the generation pool it may be given (run exclusions honoured); distinct, ordinal by id. The run
		/// player's classes are its run row's (RunHelpers.RunPlayerClasses), so an explorer of a dual row (Elise DRUID_PRIEST,
		/// Reno MAGE_ROGUE, ...) discovers cards of both classes.
		/// </summary>
		internal static IReadOnlyList<Card> ClassDiscoverPool(Game g, Controller c, IEntity source, CardType type)
		{
			var classes = RunHelpers.RunPlayerClasses(c);
			using (g.GenerateFor(c))
				return RandomCardTask.GetCardList(source, type)
					.Where(card => card.Implemented && card.Type == type && (classes.Contains(card.Class) || card.Class == CardClass.NEUTRAL))
					.GroupBy(card => card.Id, StringComparer.Ordinal).Select(x => x.First()).OrderBy(card => card.Id, StringComparer.Ordinal).ToArray();
		}

		/// <summary>
		/// ULDA_303 Starseeker's Tools: a Discover of a minion, then a Discover of a spell (one choice chain); each chosen card
		/// costs (2) less (Planned, ULDA_303e).
		/// </summary>
		private static void StarseekersTools(Game g, Controller c, IEntity s, TaskStack stack)
		{
			var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
			var planned = new AddEnchantmentTask("ULDA_303e", EntityType.TARGET);
			var minions = ClassDiscoverPool(g, c, s, CardType.MINION);
			if (minions.Count > 0) ctx.Discover(ctx.SampleDistinct(minions, 3), ChoiceAction.HAND, planned);
			var spells = ClassDiscoverPool(g, c, s, CardType.SPELL);
			if (spells.Count > 0) ctx.Discover(ctx.SampleDistinct(spells, 3), ChoiceAction.HAND, planned);
		}

		/// <summary>
		/// ULDA_304 Staff of Renewal: of the owner's minions that died this game, the <see cref="StaffOfRenewalCount"/> with the
		/// highest Cost (ties in a game-RNG order) are summoned anew (their cards, as resurrection does), each given Taunt.
		/// </summary>
		private static void StaffOfRenewal(Game g, Controller c, IEntity s)
		{
			var dead = c.GraveyardZone.Where(p => p is Minion && SelfCondition.IsDead.Eval(p) && !ShrineRules.IsCorpse(p)).ToList();
			if (dead.Count == 0) return;
			// A Fisher-Yates shuffle on the game RNG, then a stable sort by Cost: equal Costs keep the shuffled order.
			for (int i = dead.Count - 1; i > 0; i--)
			{
				int j = g.Random.Next(i + 1);
				var x = dead[i]; dead[i] = dead[j]; dead[j] = x;
			}
			g.OnRandomHappened(true);
			var chosen = dead.Select((p, i) => (p, i)).OrderByDescending(x => x.p.Card.Cost).ThenBy(x => x.i).Take(StaffOfRenewalCount).Select(x => x.p).ToList();
			var taunt = new AddEnchantmentTask(StaffTaunt, EntityType.TARGET);
			foreach (var corpse in chosen)
			{
				if (c.BoardZone.IsFull) break;
				var minion = (Minion)Entity.FromCard(c, corpse.Card, new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id });
				Generic.SummonBlock(g, minion, -1, s);
				if (minion.Zone?.Type == Zone.PLAY) taunt.Process(g, c, s, minion);
			}
		}

		/// <summary>
		/// ULDA_404 Titan-Forged Grapnel: <see cref="GrapnelKills"/> random enemy minions (not already dying, not untouchable)
		/// are destroyed; the owner gains Armor equal to their Attack at that moment, summed.
		/// </summary>
		private static void TitanForgedGrapnel(Game g, Controller c, IEntity s, TaskStack stack)
		{
			var live = c.Opponent.BoardZone.GetAll().Where(m => !m.ToBeDestroyed && !m.Untouchable).ToList();
			var picks = live.Count <= GrapnelKills ? live : live.ChooseNElements(GrapnelKills, g.Random).ToList();
			if (live.Count > GrapnelKills) g.OnRandomHappened(true);
			int armor = picks.Sum(m => Math.Max(0, m.AttackDamage));
			var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
			foreach (var m in picks) ctx.Destroy(m.Id);
			g.DeathProcessingAndAuraUpdate();
			if (armor > 0) ctx.RunLegacy(new ArmorTask(armor));
		}

		/// <summary>
		/// ULDA_402 Brann's Saddle's Deathrattle (Saddled Up!): another friendly Beast on the board, at random, gets Saddled Up!
		/// (+3/+3 and this Deathrattle); nothing when there is none.
		/// </summary>
		private static void SaddleOn(Game g, Controller c, IEntity s)
		{
			var dead = (s as Enchantment)?.Target as IPlayable ?? s as IPlayable;
			var beasts = c.BoardZone.GetAll().Where(m => m != dead && !m.ToBeDestroyed && m.IsRace(Race.BEAST)).ToList();
			if (beasts.Count == 0) return;
			var beast = beasts[g.Random.Next(beasts.Count)];
			g.OnRandomHappened(true);
			new AddEnchantmentTask("ULDA_402e", EntityType.TARGET).Process(g, c, s, beast);
		}

		/// <summary>
		/// ULDA_010 Scales of Justice: every minion on the board (both sides) becomes a 1/1 Murloc (<see cref="ScalesMurloc"/>);
		/// then the owner's hand fills with random scripted Murlocs of its generation pool, each costing (0).
		/// </summary>
		private static void ScalesOfJustice(Game g, Controller c, IEntity s)
		{
			var murloc = Cards.FromId(ScalesMurloc);
			foreach (var m in c.BoardZone.GetAll().Concat(c.Opponent.BoardZone.GetAll()).ToArray())
				if (m.Zone?.Type == Zone.PLAY && !m.ToBeDestroyed) Generic.TransformBlock(m.Controller, murloc, m);
			g.DeathProcessingAndAuraUpdate();
			IReadOnlyList<Card> pool;
			using (g.GenerateFor(c))
				pool = RandomCardTask.GetCardList(s, CardType.MINION).Where(card => card.Implemented && card.Type == CardType.MINION && card.IsRace(Race.MURLOC))
					.GroupBy(card => card.Id, StringComparer.Ordinal).Select(x => x.First()).OrderBy(card => card.Id, StringComparer.Ordinal).ToArray();
			if (pool.Count == 0) return;
			var free = new AddEnchantmentTask(ScalesCostEnchantment, EntityType.TARGET);
			RepeatUntil(() => c.HandZone.IsFull, Controller.MaxHandSize, () =>
			{
				var card = (Playable)Entity.FromCard(c, pool[g.Random.Next(pool.Count)], new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id });
				if (!Generic.AddHandPhase(c, card)) return false;
				free.Process(g, c, s, card);
				return true;
			});
			g.OnRandomHappened(true);
		}

		/// <summary>
		/// ULDA_019 Pack Mule: each of the owner's cards in hand or deck dealt from a run deck slot of origin "starter" (the ten
		/// starter cards; RunHelpers.SlotOrigin, the per-card deck records of phase 5) costs (1) less (Unencumbered). Outside a run
		/// match, or on a run without card records, no card has a slot origin, so it does nothing.
		/// </summary>
		private static void PackMule(Game g, Controller c, IEntity s)
		{
			var mule = new AddEnchantmentTask("ULDA_019e", EntityType.TARGET);
			foreach (var p in c.HandZone.ToArray().Concat(c.DeckZone.ToArray()))
				if (RunHelpers.SlotOrigin(p) == StarterOrigin) mule.Process(g, c, s, p);
		}

		/// <summary>ULDA_305 Elise's Machete: two 2/2 Treants (<see cref="MacheteTreant"/>), each given Rush (<see cref="MacheteRush"/>), board permitting.</summary>
		private static void MacheteTreants(Game g, Controller c, IEntity s)
		{
			var rush = new AddEnchantmentTask(MacheteRush, EntityType.TARGET);
			for (int i = 0; i < 2 && !c.BoardZone.IsFull; i++)
			{
				var treant = (Minion)Entity.FromCard(c, Cards.FromId(MacheteTreant), new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id });
				Generic.SummonBlock(g, treant, -1, s);
				if (treant.Zone?.Type == Zone.PLAY) rush.Process(g, c, s, treant);
			}
		}

		/// <summary>
		/// ULDA_507 Karl the Lost's Battlecry: up to <see cref="KarlRecruits"/> Silver Hand Recruits (as many as the board holds),
		/// then Taunt and Divine Shield on every friendly minion, Karl and the Recruits included.
		/// </summary>
		private static void KarlTheLost(Game g, Controller c, IEntity s)
		{
			var recruit = Cards.FromId(SilverHandRecruit);
			for (int i = 0; i < KarlRecruits && !c.BoardZone.IsFull; i++)
				Generic.SummonBlock(g, (Minion)Entity.FromCard(c, recruit, new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id }), -1, s);
			foreach (var m in c.BoardZone.GetAll())
			{
				if (m.ToBeDestroyed) continue;
				m[GameTag.TAUNT] = 1;
				m[GameTag.DIVINE_SHIELD] = 1;
			}
		}

		/// <summary>ULDA_016/ULDA_021: the Tomb Diver's Battlecry, its power becoming ULDA_039 once enough Secrets were played.</summary>
		private static ISimpleTask TombDiver(int secrets) => new CustomTask((g, c, s, t, stack) =>
		{
			if (c.NumSecretsPlayedThisGame < secrets) return;
			new ReplaceHeroPower(Cards.FromId(UldumTreasureCache)).Process(g, c, s, null);
		});

		/// <summary>ULDA_018/ULDA_023: the Excavator's Battlecry: three draws; each minion drawn gets <paramref name="enchantment"/>.</summary>
		private static ISimpleTask Excavator(string enchantment) => ComplexTask.Create(
			new DrawTask(true, 3),
			new FilterStackTask(SelfCondition.IsMinion),
			new AddEnchantmentTask(enchantment, EntityType.STACK));

		/// <summary>A minion signature whose client card names a Battlecry in its text but carries no BATTLECRY tag.</summary>
		private static void WithBattlecry(Card card) => card.Tags[GameTag.BATTLECRY] = 1;

		private static void TombsSignatures()
		{
			// ---------------- enchantments (the client's own shells, names and texts)
			Buff("fx_ULDA_203e", "ULDA_203e", "Reno's Lucky Hat", "+2/+2 and <b>Spell Damage +2</b>.",
				Effects.Attack_N(2), Effects.Health_N(2), new SabberStoneCore.Enchants.Effect(GameTag.SPELLPOWER, EffectOperator.ADD, 2));
			Buff("fx_ULDA_018e", "ULDA_018e", "Excavated", "Costs (2) less.", Effects.ReduceCost(2));
			Buff("fx_ULDA_023e", "ULDA_023e", "Very Excavated", "Costs (5) less.", Effects.ReduceCost(5));
			Buff("fx_ULDA_019e", "ULDA_019e", "Unencumbered", "Costs (1) less.", Effects.ReduceCost(1));
			Buff("fx_ULDA_024e", "ULDA_024e", "Unencumbered", "Costs (1) less.", Effects.ReduceCost(1));
			Buff("fx_ULDA_303e", "ULDA_303e", "Planned", "Costs (2) less.", Effects.ReduceCost(2));
			Buff("fx_ULDA_502e", "ULDA_502e", "Desert Explorer", "+2 Health", Effects.Health_N(2));
			Buff("fx_ULDA_505e", "ULDA_505e", "Mighty!", "Increased Durability.", new SabberStoneCore.Enchants.Effect(GameTag.DURABILITY, EffectOperator.ADD, 2));
			Effect("fx_ULDA_402e", "ULDA_402e", "Saddled Up!", "+3/+3. <b>Deathrattle:</b> Give Brann's Saddle to another friendly Beast.",
				() => new Power
				{
					Enchant = new Enchant(Effects.AttackHealth_N(3)),
					DeathrattleTask = new CustomTask((g, c, s, t, stack) => SaddleOn(g, c, s)),
				});

			// ---------------- Reno Jackson (MAGE_ROGUE)
			Active("ULDA_203", "Reno's Lucky Hat", "Give a minion +2/+2 and <b>Spell Damage +2</b>.", 1,
				() => Def(new Power { PowerTask = new AddEnchantmentTask("ULDA_203e", EntityType.TARGET) }, PlayReq.REQ_TARGET_TO_PLAY, PlayReq.REQ_MINION_TARGET));
			Active("ULDA_201", "Reno's Crafty Lasso", "After your hero attacks, steal a card from your opponent's hand.", 2,
				() => Def(new Power
				{
					Trigger = new Trigger(TriggerType.AFTER_ATTACK) { TriggerSource = TriggerSource.HERO, SingleTask = new CustomTask((g, c, s, t, stack) => LassoSteal(g, c, s)) },
				}));
			Active("ULDA_204", "Reno's Magical Torch", "Deal $@ damage.\n<b>Combo:</b> Shuffle a copy of this card into your deck that deals 2 more damage.", 2,
				() => Def(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => MagicalTorch(g, c, s, t, stack)) }, PlayReq.REQ_TARGET_TO_PLAY),
				card => card.Tags[GameTag.TAG_SCRIPT_DATA_NUM_1] = MagicalTorchDamage);
			Active("ULDA_016", "Jr. Tomb Diver",
				"[x]<b>Battlecry:</b> If you played\n6 Secrets this game,\nyour Hero Power becomes\n'<b>Discover</b> a Treasure'.", 4,
				() => Def(new Power { PowerTask = TombDiver(JrTombDiverSecrets) }), WithBattlecry);
			Active("ULDA_021", "Sr. Tomb Diver",
				"[x]<b>Battlecry:</b> If you played\n3 Secrets this game,\nyour Hero Power becomes\n'<b>Discover</b> a Treasure'.", 4,
				() => Def(new Power { PowerTask = TombDiver(SrTombDiverSecrets) }), WithBattlecry);
			Active("ULDA_206", "Lei Flamepaw", "After you cast a spell, cast it again <i>(on the same target)</i>.", 2,
				() => Def(new Power
				{
					Trigger = new Trigger(TriggerType.AFTER_CAST)
					{
						TriggerSource = TriggerSource.FRIENDLY,
						SingleTask = new CustomTask((g, c, s, t, stack) => LeiFlamepaw(g, c, s, t, stack)),
					},
				}));
			Active("ULDA_207", "The Gatling Wand",
				"[x]Deal $@ damage split\nrandomly among all enemies\n<i>(improves for each spell\nyou've cast this game)</i>.", 5,
				() => Def(new Power
				{
					PowerTask = new CustomTask((g, c, s, t, stack) => SplitHits.Inline(g, c, s,
						SpellDamageRules.Amount((IPlayable)s, GatlingWandAmount(c)), ComplexTask.DamageRandomTargets(1, EntityType.ENEMIES, 1), deathProcessing: false)),
					// The shown damage follows the spells cast, in hand and in deck (the client fills "$@" from the card's script data).
					Trigger = new Trigger(TriggerType.AFTER_CAST)
					{
						TriggerSource = TriggerSource.FRIENDLY,
						TriggerActivation = TriggerActivation.HAND_OR_DECK,
						SingleTask = new CustomTask((g, c, s, t, stack) =>
						{
							if (s is IPlayable wand) wand[GameTag.TAG_SCRIPT_DATA_NUM_1] = GatlingWandAmount(c) + 1;
						}),
					},
				}),
				card => card.Tags[GameTag.TAG_SCRIPT_DATA_NUM_1] = GatlingWandBase);

			// ---------------- Sir Finley (PALADIN_SHAMAN)
			Active("ULDA_502", "Finley's Pith Helmet", "Give all friendly minions +2 Health.\nShuffle this card into your deck.", 2,
				() => Def(new Power { PowerTask = ComplexTask.Create(new AddEnchantmentTask("ULDA_502e", EntityType.MINIONS), new AddCardTo("ULDA_502", EntityType.DECK)) }));
			Active("ULDA_501", "Maxwell, Mighty Steed", "<b>Rush</b>. <b>Reborn</b>.\nHas +2 Attack for each other minion on the battlefield.", 3,
				() => Def(new Power
				{
					Aura = new AdaptiveEffect(GameTag.ATK, EffectOperator.ADD,
						p => 2 * (Math.Max(0, p.Controller.BoardZone.Count - 1) + p.Controller.Opponent.BoardZone.Count)),
				}));
			Active("ULDA_505", "Truesilver Lance", "<b>Lifesteal</b>\nAfter you <b>Overload</b>, gain +2 Durability.", 3,
				() => Def(new Power
				{
					// FastExecution: the Overload event is raised inside the cast's own block (Generic.OverloadBlock), as Lesser Sapphire
					// Spellstone's (LOOT_064) reads it.
					Trigger = new Trigger(TriggerType.OVERLOAD)
					{
						TriggerSource = TriggerSource.FRIENDLY, FastExecution = true, SingleTask = new AddEnchantmentTask("ULDA_505e", EntityType.SOURCE),
					},
				}));
			Active("ULDA_015", "Jr. Scout", "[x]<b>Charge</b>. <b>Windfury</b>.\nAt the end of your turn, deal\n4 damage to a random\nenemy minion.", 3,
				() => Def(new Power { Trigger = Trig(TriggerType.TURN_END, DamageRandomEnemyMinion(4)) }));
			Active("ULDA_020", "Sr. Scout", "[x]<b>Charge</b>. <b>Windfury</b>.\nAt the end of your turn, deal\n6 damage to a random\nenemy minion.", 3,
				() => Def(new Power { Trigger = Trig(TriggerType.TURN_END, DamageRandomEnemyMinion(6)) }));
			Active("ULDA_507", "Karl the Lost",
				"[x]<b>Battlecry:</b> Summon six 1/1\nSilver_Hand_Recruits and give\nall friendly minions <b>Taunt</b>\nand <b>Divine Shield</b>.", 5,
				() => Def(new Power
				{
					PowerTask = new CustomTask((g, c, s, t, stack) => KarlTheLost(g, c, s)),
				}), null, SilverHandRecruit);
			Active("ULDA_010", "Scales of Justice", "Transform all minions into 1/1 Murlocs.\nFill your hand with random Murlocs that cost (0).", 4,
				() => Def(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => ScalesOfJustice(g, c, s)) }), null, ScalesMurloc);

			// ---------------- Elise Starseeker (DRUID_PRIEST); ULDA_302 Academic Research is family F15's
			Active("ULDA_303", "Starseeker's Tools", "<b>Discover</b> a minion\nand a spell.\nThey cost (2) less.", 2,
				() => Def(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => StarseekersTools(g, c, s, stack)) }));
			Active("ULDA_305", "Elise's Machete", "[x]After your hero attacks,\nsummon two 2/2 Treants.\nGive them <b>Rush</b>.", 3,
				() => Def(new Power
				{
					Trigger = new Trigger(TriggerType.AFTER_ATTACK)
					{
						TriggerSource = TriggerSource.HERO,
						SingleTask = new CustomTask((g, c, s, t, stack) => MacheteTreants(g, c, s)),
					},
				}), null, MacheteTreant);
			Active("ULDA_003", "Addarah", "<b>Battlecry:</b> Shuffle all enemy minions into your deck.", 3,
				() => Def(new Power { PowerTask = ComplexTask.Create(new IncludeTask(EntityType.OP_MINIONS), new MoveToDeck(EntityType.STACK)) }));
			Active("ULDA_017", "Jr. Navigator", "[x]Whenever you target\nthis minion with a spell,\nadd 2 copies of the spell\nto your hand.", 3,
				() => Def(new Power
				{
					Trigger = new Trigger(TriggerType.CAST_SPELL)
					{
						TriggerSource = TriggerSource.FRIENDLY_SPELL_CASTED_ON_THE_OWNER,
						SingleTask = new CustomTask((g, c, s, t, stack) => Navigator(g, c, s, t)),
					},
				}));
			Active("ULDA_022", "Sr. Navigator", "[x]Whenever you target\nthis minion with a spell,\nadd 2 copies of the spell\nto your hand.", 3,
				() => Def(new Power
				{
					Trigger = new Trigger(TriggerType.CAST_SPELL)
					{
						TriggerSource = TriggerSource.FRIENDLY_SPELL_CASTED_ON_THE_OWNER,
						SingleTask = new CustomTask((g, c, s, t, stack) => Navigator(g, c, s, t)),
					},
				}));
			Active("ULDA_304", "Staff of Renewal", "Resurrect 7 friendly minions with\nthe highest Cost.\nGive them <b>Taunt</b>.", 7,
				() => Def(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => StaffOfRenewal(g, c, s)) }));

			// ---------------- Brann Bronzebeard (HUNTER_WARRIOR)
			Active("ULDA_401", "Ol' Faithful", "After your hero attacks, deal 1 damage to all enemies.", 3,
				() => Def(new Power
				{
					Trigger = new Trigger(TriggerType.AFTER_ATTACK) { TriggerSource = TriggerSource.HERO, SingleTask = new DamageTask(1, EntityType.ENEMIES) },
				}));
			Active("ULDA_402", "Brann's Saddle", "Give a friendly Beast +3/+3 and \"<b>Deathrattle:</b> Give Brann's Saddle to another friendly Beast.\"", 2,
				() => new CardDef(new Dictionary<PlayReq, int>
				{
					{ PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 }, { PlayReq.REQ_FRIENDLY_TARGET, 0 }, { PlayReq.REQ_TARGET_WITH_RACE, (int)Race.BEAST },
				}, new Power { PowerTask = new AddEnchantmentTask("ULDA_402e", EntityType.TARGET) }));
			Active("ULDA_404", "Titan-Forged Grapnel", "Destroy 2 random enemy minions.\nGain Armor equal to their combined attack.", 3,
				() => Def(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => TitanForgedGrapnel(g, c, s, stack)) }));
			Active("ULDA_018", "Jr. Excavator", "<b>Battlecry:</b> Draw 3 cards.\nAny minions you draw\ncost_(2)_less.", 4,
				() => Def(new Power { PowerTask = Excavator("ULDA_018e") }), WithBattlecry);
			Active("ULDA_023", "Sr. Excavator", "<b>Battlecry:</b> Draw 3 cards.\nAny minions you draw\ncost_(5)_less.", 4,
				() => Def(new Power { PowerTask = Excavator("ULDA_023e") }), WithBattlecry);
			Active("ULDA_002", "Flo Slatebrand", "<b>Battlecry and Deathrattle:</b> Add a random Treasure to your hand.", 2,
				() => Def(new Power
				{
					PowerTask = new CustomTask((g, c, s, t, stack) => AddRandomTombsTreasure(g, c, s)),
					DeathrattleTask = new CustomTask((g, c, s, t, stack) => AddRandomTombsTreasure(g, c, s)),
				}));
			Active("ULDA_405", "Brann's Epic Egg", "After this takes damage, summon King Krush.", 6,
				() => Def(new Power
				{
					Trigger = new Trigger(TriggerType.TAKE_DAMAGE) { TriggerSource = TriggerSource.SELF, SingleTask = new SummonTask(KingKrush, SummonSide.RIGHT) },
				}), null, KingKrush);

			// ---------------- Pack Mule (a Tombs treasure of the four explorers; upgrades when an act is cleared)
			Active("ULDA_019", "Pack Mule", "Reduce the Cost of your 10 starter cards by (1).", 1,
				() => Def(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => PackMule(g, c, s)) }));
			Active("ULDA_024", "Upgraded Pack Mule", "Reduce the Cost of all cards in your deck by (1).", 2,
				() => Def(new Power { PowerTask = new AddEnchantmentTask("ULDA_024e", EntityType.DECK) }));
		}
	}
}
