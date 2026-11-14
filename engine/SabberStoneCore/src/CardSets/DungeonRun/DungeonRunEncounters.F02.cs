using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.src.Loader;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.DungeonRun
{
	// Phase 8, family 02: the unscripted collectibles the imported boss decks and the catalog buckets list (33 ids). The cards
	// keep their place in the card-set files (TgtCardsGen, GvgCardsGen, LoeCardsGen, HofCardsGen, BoomsdayCardsGen,
	// DalaranCardsGen, GilneasCardsGen, TrollCardsGen), which name the task builders below; the rules they share with later
	// families are in Actions.EncounterRules (WrongTarget50, HeroDamageCap, SecretRepeats, CapturesOpening). Every random choice
	// uses the game RNG and reports a random event; every pool honours the run's generation filter (Game.BlocksGenerated) and
	// holds scripted cards only.
	public static partial class DungeonRunEncounters
	{
		/// <summary>GVG_050 Bouncing Blade: the most hits one cast makes (the original's own bound).</summary>
		public const int BouncingBladeHits = 80;
		/// <summary>EX1_112 Gelbin Mekkatorque's AWESOME inventions.</summary>
		public static readonly IReadOnlyList<string> Inventions = new[] { "Mekka1", "Mekka2", "Mekka3", "Mekka4" };
		/// <summary>DAL_431t Drustvar Horror: the generated minion Swampqueen Hagatha teaches two Shaman spells.</summary>
		public const string DrustvarHorror = "DAL_431t";

		private static void F02()
		{
			// The family registers no encounter card: its 33 cards are collectibles of the card-set files, and Arch-Thief Rafaam's
			// artifacts (LOEA16_3, LOEA16_4, LOEA16_5, LOEA16_5t) are scripted beside him in LoeCardsGen.
		}

		// ---------------------------------------------------------------- pools

		/// <summary>
		/// The scripted cards a Discover or a random effect of <paramref name="c"/>'s may yield: of its classes (the run player's
		/// run row: RunHelpers.RunPlayerClasses) and neutral, passing <paramref name="filter"/> and the run's generation filter;
		/// distinct, ordinal by id.
		/// </summary>
		internal static IReadOnlyList<Card> ScriptedPool(Game g, Controller c, IEntity source, Func<Card, bool> filter, bool anyClass = false)
		{
			var classes = RunHelpers.RunPlayerClasses(c);
			using (g.GenerateFor(c))
				return RandomCardTask.GetCardList(source)
					.Where(card => card.Implemented && card.Type != CardType.HERO && (anyClass || classes.Contains(card.Class) || card.Class == CardClass.NEUTRAL) && filter(card))
					.GroupBy(card => card.Id, StringComparer.Ordinal).Select(x => x.First()).OrderBy(card => card.Id, StringComparer.Ordinal).ToArray();
		}

		/// <summary>A Discover among three distinct cards of <see cref="ScriptedPool"/> (LOE_029: a 3-Cost card; LOE_047: a Beast).</summary>
		internal static ISimpleTask DiscoverScripted(Func<Card, bool> filter) => new CustomTask((g, c, s, t, stack) =>
		{
			var pool = ScriptedPool(g, c, s, filter);
			if (pool.Count == 0) return;
			var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
			ctx.Discover(ctx.SampleDistinct(pool, 3), ChoiceAction.HAND);
			g.OnRandomHappened(true);
		});

		// ---------------------------------------------------------------- The Grand Tournament

		/// <summary>AT_079 Mysterious Challenger: "Battlecry: Put one of each Secret from your deck into the battlefield."</summary>
		internal static ISimpleTask MysteriousChallenger => new CustomTask((g, c, s, t, stack) =>
		{
			// One Secret of each name the deck holds and the Secret zone does not; more names than room: a random subset.
			var found = new List<IPlayable>();
			foreach (IPlayable p in c.DeckZone)
				if (p is Spell spell && spell.Card.IsSecret && !spell.Card.IsQuest
					&& !c.SecretZone.Any(secret => secret.Card.AssetId == p.Card.AssetId)
					&& !found.Any(other => other.Card.AssetId == p.Card.AssetId)) found.Add(p);
			int room = c.SecretZone.FreeSpace;
			if (found.Count > room && room > 0) g.OnRandomHappened(true);
			while (found.Count > 0 && !c.SecretZone.IsFull)
			{
				int index = found.Count > c.SecretZone.FreeSpace ? g.Random.Next(found.Count) : 0;
				IPlayable pick = found[index];
				found.RemoveAt(index);
				c.DeckZone.Remove(pick);
				pick.Power?.Trigger?.Activate(pick);
				c.SecretZone.Add((Spell)pick);
				if (c == g.CurrentPlayer) pick.IsExhausted = true;
			}
		});

		// ---------------------------------------------------------------- The Boomsday Project

		/// <summary>BOT_436 Prismatic Lens: "Draw a minion and a spell from your deck. Swap their Costs."</summary>
		internal static ISimpleTask PrismaticLens => new CustomTask((g, c, s, t, stack) =>
		{
			IPlayable Pull(Func<IPlayable, bool> kind)
			{
				var candidates = c.DeckZone.Where(kind).ToList();
				if (candidates.Count == 0) return null;
				if (candidates.Count > 1) g.OnRandomHappened(true);
				IPlayable drawn = Generic.Draw(c, candidates[g.Random.Next(candidates.Count)]);
				return drawn != null && drawn.Zone == c.HandZone ? drawn : null;
			}
			IPlayable minion = Pull(p => p is Minion);
			IPlayable spell = Pull(p => p is Spell);
			if (minion == null || spell == null) return;
			int minionCost = minion.Cost, spellCost = spell.Cost;
			Card swapped = Cards.FromId("BOT_436e");
			Generic.AddEnchantmentBlock(g, swapped, (IPlayable)s, minion, spellCost);
			Generic.AddEnchantmentBlock(g, swapped, (IPlayable)s, spell, minionCost);
		});

		// ---------------------------------------------------------------- Rise of Shadows

		/// <summary>
		/// DAL_729 Madame Lazul: "Battlecry: Discover a copy of a card in your opponent's hand." Up to three different cards of
		/// the opponent's hand, as fresh copies. In a fair-view projection the opponent's hand holds no known card, so the
		/// Battlecry offers nothing there: the owner's search never reads the hidden hand, and the real Discover shows its owner
		/// exactly the cards the text allows.
		/// </summary>
		internal static ISimpleTask MadameLazul => new CustomTask((g, c, s, t, stack) =>
		{
			var cards = c.Opponent.HandZone.Where(p => !(p is Unknown) && p.Card != null)
				.GroupBy(p => p.Card.Id, StringComparer.Ordinal).Select(x => x.First().Card).ToArray();
			if (cards.Length == 0) return;
			var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
			if (cards.Length > 3) g.OnRandomHappened(true);
			if (!ctx.Discover(ctx.SampleDistinct(cards, Math.Min(3, cards.Length)), ChoiceAction.HAND)) return;
			Choice choice = c.Choice;
			while (choice.NextChoice != null) choice = choice.NextChoice;
			foreach (int offer in choice.Choices) PayloadRules.MarkCopy(c.Opponent, g.IdEntityDic[offer]);
		});

		/// <summary>
		/// DAL_731 Duel!: "Summon a minion from each player's deck. They fight!" A random minion of each deck (a deck with no
		/// minion, or a full board, summons none); with both summoned, the caster's attacks the opponent's (a forced attack).
		/// </summary>
		internal static ISimpleTask Duel => new CustomTask((g, c, s, t, stack) =>
		{
			Minion Pull(Controller owner)
			{
				if (owner.BoardZone.IsFull) return null;
				var minions = owner.DeckZone.Where(p => p is Minion).ToList();
				if (minions.Count == 0) return null;
				if (minions.Count > 1) g.OnRandomHappened(true);
				var pick = (Minion)minions[g.Random.Next(minions.Count)];
				Generic.RemoveFromZone.Invoke(owner, pick);
				Generic.SummonBlock.Invoke(g, pick, -1, s);
				return pick.Zone == owner.BoardZone ? pick : null;
			}
			Minion mine = Pull(c), theirs = Pull(c.Opponent);
			if (mine == null || theirs == null || mine.IsDead || theirs.IsDead || mine.Untouchable || theirs.Untouchable
				|| mine.Zone != c.BoardZone || theirs.Zone != c.Opponent.BoardZone || g.State == State.COMPLETE) return;
			EventMetaData before = g.CurrentEventData;
			Generic.AttackBlock.Invoke(c, mine, theirs, true, true);
			g.CurrentEventData = before;
		});

		/// <summary>
		/// DAL_431 Swampqueen Hagatha: "Battlecry: Add a 5/5 Horror to your hand. Teach it two Shaman spells." Two Discovers among
		/// the scripted Shaman spells (a targeted first spell leaves only untargeted ones for the second); the Horror is a
		/// generated definition (<see cref="Horror"/>) whose Battlecry casts both.
		/// </summary>
		internal static ISimpleTask SwampqueenHagatha => new CustomTask((g, c, s, t, stack) =>
		{
			var pool = ShamanSpells(g, c, s);
			if (pool.Count == 0) return;
			var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
			g.OnRandomHappened(true);
			ctx.Discover(ctx.SampleDistinct(pool, 3), ChoiceAction.STACK, HagathaSecond);
		});

		private static readonly ISimpleTask HagathaSecond = new CustomTask((g, c, s, first, stack) =>
		{
			if (first == null) return;
			bool targeted = first.Card.TargetingType != TargetingType.None;
			var pool = ShamanSpells(g, c, s).Where(card => !targeted || card.TargetingType == TargetingType.None).ToArray();
			var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
			// No second spell to teach: the Horror knows the first one only.
			if (pool.Length == 0 || !ctx.Discover(ctx.SampleDistinct(pool, 3), ChoiceAction.STACK, HagathaFinish)) GiveHorror(g, c, s, first.Card, null);
		});

		private static readonly ISimpleTask HagathaFinish = new CustomTask((g, c, s, second, stack) =>
		{
			Choice choice = c.Choice;
			if (second == null || choice == null || !g.IdEntityDic.TryGetValue(choice.LastChoice, out IPlayable first)) return;
			GiveHorror(g, c, s, first.Card, second.Card);
		});

		private static void GiveHorror(Game g, Controller c, IEntity source, Card first, Card second)
		{
			// The two spells are entity tags as well (the definition's modular parts), so a native packet of the entity carries
			// what the client's text builder names.
			var tags = new EntityData { [GameTag.CREATOR] = source.Id, [GameTag.DISPLAYED_CREATOR] = source.Id, [GameTag.MODULAR_ENTITY_PART_1] = first.AssetId };
			if (second != null) tags[GameTag.MODULAR_ENTITY_PART_2] = second.AssetId;
			IPlayable horror = Entity.FromCard(c, Horror(first, second), tags);
			Generic.AddHandPhase.Invoke(c, horror);
		}

		/// <summary>The scripted Shaman spells a run match may generate for <paramref name="c"/> (no Quest), ordinal by id.</summary>
		internal static IReadOnlyList<Card> ShamanSpells(Game g, Controller c, IEntity source) =>
			ScriptedPool(g, c, source, card => card.Type == CardType.SPELL && card.Class == CardClass.SHAMAN && !card.IsQuest && !card.IsSecret, anyClass: true);

		/// <summary>
		/// The Drustvar Horror that knows <paramref name="first"/> and <paramref name="second"/> (null: one spell): the client's
		/// DAL_431t with the two spells as its modular parts (the client's text builder names them), "Battlecry: Cast {0} and
		/// {1}". It takes a target when one of its spells does (the spell's own valid targets); a spell that cannot be cast
		/// (no legal target) is skipped. An immutable definition, as the engine's other generated cards.
		/// </summary>
		public static Card Horror(Card first, Card second)
		{
			if (first == null || first.Type != CardType.SPELL || second != null && second.Type != CardType.SPELL)
				throw new ArgumentException("A Drustvar Horror is taught spells.");
			Card template = Cards.FromId(DrustvarHorror);
			var tags = new Dictionary<GameTag, int>(template.Tags) { [GameTag.MODULAR_ENTITY_PART_1] = first.AssetId };
			if (second != null) tags[GameTag.MODULAR_ENTITY_PART_2] = second.AssetId;
			tags[GameTag.BATTLECRY] = 1;
			var requirements = new Dictionary<PlayReq, int>();
			if (first.TargetingType != TargetingType.None || second != null && second.TargetingType != TargetingType.None)
				requirements[PlayReq.REQ_TARGET_IF_AVAILABLE] = 0;
			var raw = tags.Select(p => new Tag(p.Key, p.Value)).Concat(new[]
			{
				new Tag(GameTag.CARDNAME, template.Name),
				new Tag(GameTag.CARDTEXT, "<b>Battlecry:</b> Cast " + first.Name + (second != null ? " and " + second.Name : "") + "."),
			}).ToArray();
			var result = new Card(template.Id, template.AssetId, raw, requirements, template.Entourage?.ToArray() ?? new string[0],
				template.RefTags.Select(p => new Tag(p.Key, p.Value)).ToArray(), preserveText: true);
			result.Implemented = true;
			result.ExcludeFromLegacyRandomPools = true;
			// Targets are the targeted spell's own; the minion itself is always playable.
			result.SourceTargetingPredicate = GeneratedDefinitionRules.ValidTarget;
			result.TargetingAvailabilityPredicate = GeneratedDefinitionRules.TargetAvailable;
			result.Power = new Power { PowerTask = HorrorBattlecry };
			return result;
		}

		private static readonly ISimpleTask HorrorBattlecry = new CustomTask((g, c, s, t, stack) =>
		{
			foreach (GameTag part in new[] { GameTag.MODULAR_ENTITY_PART_1, GameTag.MODULAR_ENTITY_PART_2 })
			{
				int dbf = s.Card[part];
				if (dbf == 0 || g.State == State.COMPLETE) continue;
				Card spellCard = Cards.FromAssetId(dbf);
				if (spellCard == null || spellCard.Type != CardType.SPELL) continue;
				var spell = (Spell)Entity.FromCard(c, spellCard, new EntityData { [GameTag.CREATOR] = s.Id, [GameTag.DISPLAYED_CREATOR] = s.Id });
				ICharacter target = null;
				if (spellCard.TargetingType != TargetingType.None)
				{
					// The Battlecry's target, while the spell may still have it; a targeted spell with no target is not cast.
					target = t as ICharacter;
					if (target == null || target.ToBeDestroyed || !spell.IsValidPlayTarget(target)) continue;
				}
				g.TaskQueue.StartEvent();
				try { Generic.CastSpell.Invoke(c, g, spell, target, 0); }
				finally { g.TaskQueue.EndEvent(); }
				// A choice the spell opens (a Discover) is settled before the next spell, by the game RNG (as a recast's: RecastRules).
				while (c.Choice != null && c.Choice.SourceId == spell.Id && g.State != State.COMPLETE)
				{
					g.OnRandomHappened(true);
					Generic.ChoicePick.Invoke(c, g, c.Choice.Choices.Choose(g.Random));
				}
			}
		});

		// ---------------------------------------------------------------- Hall of Fame

		/// <summary>EX1_112 Gelbin Mekkatorque: "Battlecry: Summon an AWESOME invention." (one of <see cref="Inventions"/>)</summary>
		internal static ISimpleTask GelbinMekkatorque => new CustomTask((g, c, s, t, stack) =>
		{
			if (c.BoardZone.IsFull) return;
			string pick = Inventions[g.Random.Next(Inventions.Count)];
			g.OnRandomHappened(true);
			new SummonTask(SummonSide.RIGHT, Cards.FromId(pick)).Process(g, c, s, null, stack ?? new TaskStack());
		});

		/// <summary>Mekka2 Repair Bot: "At the end of your turn, restore #6 Health to a damaged character." (a random one, either side)</summary>
		internal static ISimpleTask RepairBot => new CustomTask((g, c, s, t, stack) =>
		{
			var damaged = new List<ICharacter>();
			foreach (Controller side in new[] { c, c.Opponent })
			{
				if (side.Hero.Damage > 0 && !side.Hero.ToBeDestroyed) damaged.Add(side.Hero);
				foreach (Minion m in side.BoardZone) if (m.Damage > 0 && !m.ToBeDestroyed && !m.Untouchable) damaged.Add(m);
			}
			if (damaged.Count == 0) return;
			if (damaged.Count > 1) g.OnRandomHappened(true);
			new EffectContext(g, c.Id, s.Id, stack: stack).Heal(damaged[g.Random.Next(damaged.Count)].Id, 6);
		});

		/// <summary>A random minion of either board that an effect can touch (not dormant), or null.</summary>
		private static Minion RandomMinion(Game g, Controller c)
		{
			var minions = c.BoardZone.Concat(c.Opponent.BoardZone).Where(m => !m.Untouchable && !m.ToBeDestroyed).ToList();
			if (minions.Count == 0) return null;
			if (minions.Count > 1) g.OnRandomHappened(true);
			return minions[g.Random.Next(minions.Count)];
		}

		/// <summary>Mekka3 Emboldener 3000: "At the end of your turn, give a random minion +1/+1."</summary>
		internal static ISimpleTask Emboldener => new CustomTask((g, c, s, t, stack) =>
		{
			Minion pick = RandomMinion(g, c);
			if (pick != null) Generic.AddEnchantmentBlock(g, Cards.FromId("Mekka3e"), (IPlayable)s, pick);
		});

		/// <summary>Mekka4 Poultryizer: "At the start of your turn, transform a random minion into a 1/1 Chicken."</summary>
		internal static ISimpleTask Poultryizer => new CustomTask((g, c, s, t, stack) =>
		{
			Minion pick = RandomMinion(g, c);
			if (pick != null) new EffectContext(g, c.Id, s.Id, stack: stack).Transform(pick.Id, "Mekka4t");
		});

		// ---------------------------------------------------------------- Goblins vs Gnomes

		/// <summary>GVG_014 Vol'jin: "Battlecry: Swap Health with another minion." Each gets the other's current Health as its Health.</summary>
		internal static ISimpleTask Voljin => new CustomTask((g, c, s, t, stack) =>
		{
			if (!(s is Minion me) || !(t is Minion other) || ReferenceEquals(me, other) || me.Zone?.Type != Zone.PLAY || other.Zone?.Type != Zone.PLAY) return;
			int mine = me.Health, theirs = other.Health;
			Card shadowed = Cards.FromId("GVG_014a");
			Generic.AddEnchantmentBlock(g, shadowed, me, me, theirs);
			Generic.AddEnchantmentBlock(g, shadowed, me, other, mine);
		});

		/// <summary>GVG_017 Call Pet: "Draw a card. If it's a Beast, it costs (4) less."</summary>
		internal static ISimpleTask CallPet => new CustomTask((g, c, s, t, stack) =>
		{
			IPlayable drawn = Generic.Draw(c);
			if (drawn is Playable card && card.Zone == c.HandZone && card.Card.IsRace(Race.BEAST))
				CostRules.AddModifier(card, EffectOperator.SUB, 4);
		});

		/// <summary>GVG_049 Gahz'rilla: "Whenever this minion takes damage, double its Attack."</summary>
		internal static ISimpleTask Gahzrilla => new CustomTask((g, c, s, t, stack) =>
		{
			if (s is Minion me && me.Zone?.Type == Zone.PLAY && me.AttackDamage > 0)
				Generic.AddEnchantmentBlock(g, Cards.FromId("GVG_049e"), me, me, me.AttackDamage);
		});

		/// <summary>
		/// GVG_050 Bouncing Blade: "Deal $1 damage to a random minion. Repeat until a minion dies." Each hit picks among the
		/// minions that can take it (in play, alive, not Immune, not dormant); it ends when a minion is mortally wounded, when
		/// none can be hit, or after <see cref="BouncingBladeHits"/> hits.
		/// </summary>
		internal static ISimpleTask BouncingBlade => new CustomTask((g, c, s, t, stack) =>
		{
			var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
			bool Dying() => c.BoardZone.Concat(c.Opponent.BoardZone).Any(m => m.IsDead);
			for (int hit = 0; hit < BouncingBladeHits && g.State != State.COMPLETE && !Dying(); hit++)
			{
				var minions = c.BoardZone.Concat(c.Opponent.BoardZone).Where(m => !m.Untouchable && !m.IsImmune && !m.IsDead).ToList();
				if (minions.Count == 0) return;
				if (minions.Count > 1) g.OnRandomHappened(true);
				ctx.Damage(minions[g.Random.Next(minions.Count)].Id, 1, spellDamage: true);
			}
		});

		/// <summary>GVG_097 Lil' Exorcist: "Taunt. Battlecry: Gain +1/+1 for each enemy Deathrattle minion."</summary>
		internal static ISimpleTask LilExorcist => new CustomTask((g, c, s, t, stack) =>
		{
			int count = c.Opponent.BoardZone.Count(m => m.HasDeathrattle && !m.Untouchable);
			if (count > 0 && s is Minion me) Generic.AddEnchantmentBlock(g, Cards.FromId("GVG_101e"), me, me, count, count);
		});

		/// <summary>GVG_107 Enhance-o Mechano: "Battlecry: Give your other minions Windfury, Taunt, or Divine Shield (at random)."</summary>
		internal static ISimpleTask EnhanceOMechano => new CustomTask((g, c, s, t, stack) =>
		{
			foreach (Minion m in c.BoardZone.ToArray())
			{
				if (ReferenceEquals(m, s) || m.Untouchable || m.ToBeDestroyed) continue;
				g.OnRandomHappened(true);
				switch (g.Random.Next(3))
				{
					case 0: m.HasWindfury = true; break;
					case 1: m.HasTaunt = true; break;
					default: m.HasDivineShield = true; break;
				}
			}
		});

		/// <summary>GVG_108 Recombobulator: "Battlecry: Transform a friendly minion into a random minion with the same Cost."</summary>
		internal static ISimpleTask Recombobulator => new CustomTask((g, c, s, t, stack) =>
		{
			if (!(t is Minion target) || target.Zone != c.BoardZone) return;
			int cost = target.Card.Cost;
			var pool = ScriptedPool(g, c, s, card => card.Type == CardType.MINION && card.Cost == cost, anyClass: true);
			if (pool.Count == 0) return;
			g.OnRandomHappened(true);
			Generic.ChangeEntityBlock.Invoke(c, target, pool[g.Random.Next(pool.Count)], true);
		});

		/// <summary>
		/// GVG_111 Mimiron's Head: "At the start of your turn, if you have at least 3 Mechs, destroy them all and form
		/// V-07-TR-0N." The Mechs die first (their Deathrattles resolve), then V-07-TR-0N is summoned.
		/// </summary>
		internal static ISimpleTask MimironsHead => new CustomTask((g, c, s, t, stack) =>
		{
			if (!(s is Minion head) || head.Zone != c.BoardZone || head.ToBeDestroyed) return;
			var mechs = c.BoardZone.Where(m => m.IsRace(Race.MECHANICAL) && !m.Untouchable && !m.ToBeDestroyed).ToArray();
			if (mechs.Length < 3) return;
			foreach (Minion m in mechs) m.Destroy();
			g.DeathProcessingAndAuraUpdate();
			if (g.State == State.COMPLETE || c.BoardZone.IsFull) return;
			new SummonTask("GVG_111t").Process(g, c, s, null, stack ?? new TaskStack());
		});

		/// <summary>GVG_119 Blingtron 3000: "Battlecry: Equip a random weapon for each player." (its owner first)</summary>
		internal static ISimpleTask Blingtron => new CustomTask((g, c, s, t, stack) =>
		{
			foreach (Controller player in new[] { c, c.Opponent })
			{
				IReadOnlyList<Card> pool;
				using (g.GenerateFor(player))
					pool = RandomCardTask.GetCardList(s, CardType.WEAPON)
						.Where(card => card.Implemented && card.Collectible)
						.GroupBy(card => card.Id, StringComparer.Ordinal).Select(x => x.First()).OrderBy(card => card.Id, StringComparer.Ordinal).ToArray();
				if (pool.Count == 0 || player.Hero == null || player.Hero.ToBeDestroyed) continue;
				g.OnRandomHappened(true);
				player.Hero.AddWeapon((Weapon)Entity.FromCard(player, pool[g.Random.Next(pool.Count)]));
			}
		});

		// ---------------------------------------------------------------- League of Explorers

		/// <summary>LOE_053 Djinni of Zephyrs: "After you cast a spell on another friendly minion, cast a copy of it on this one."</summary>
		internal static ISimpleTask DjinniOfZephyrs => new CustomTask((g, c, s, t, stack) =>
		{
			if (!(t is Spell spell) || RecastRules.IsRecast(spell) || !(s is Minion djinni) || djinni.Zone != c.BoardZone
				|| djinni.ToBeDestroyed || djinni.Untouchable || g.State == State.COMPLETE) return;
			if (spell.CardTarget == 0 || !g.IdEntityDic.TryGetValue(spell.CardTarget, out var target)
				|| !(target is Minion friendly) || friendly.Controller != c || ReferenceEquals(friendly, djinni)) return;
			RecastRules.Recast(spell, djinni, RecastRules.ChosenOption(spell));
		});

		/// <summary>
		/// LOE_107 Eerie Statue: "Can't attack unless it's the only minion in the battlefield." A dormant minion (a shrine's
		/// dormant form) is no minion in play, so it does not hold the Statue back.
		/// </summary>
		internal static bool NotAlone(IPlayable statue) =>
			statue.Controller.BoardZone.CountExceptUntouchables + statue.Controller.Opponent.BoardZone.CountExceptUntouchables > 1;

		// ---------------------------------------------------------------- Rastakhan's Rumble

		/// <summary>
		/// TRL_318 Hex Lord Malacrass: "Battlecry: Add a copy of your opening hand to your hand (except this card)." The opening
		/// hand is the one the game recorded after both mulligans (TurnQueueRules.StartingHand; EncounterRules.CapturesOpening),
		/// without The Coin and without Malacrass himself; a fair view never holds the opponent's.
		/// </summary>
		internal static ISimpleTask HexLordMalacrass => new CustomTask((g, c, s, t, stack) =>
		{
			foreach (Card card in TurnQueueRules.StartingHand(c))
			{
				if (card == null || card.Id == EncounterRules.OpeningHandReader || card.Id.StartsWith("GAME_005", StringComparison.Ordinal)) continue;
				if (c.HandZone.IsFull) break;
				Generic.AddHandPhase.Invoke(c, Entity.FromCard(c, card, new EntityData { [GameTag.CREATOR] = s.Id, [GameTag.DISPLAYED_CREATOR] = s.Id }));
			}
		});
	}
}
