using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
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
	// Phase 8, family 18: Dalaran Heist bosses E and the Heist finale. Twelve encounters with their powers: Sael'orn (Web Grab),
	// Haro Setting-Sun (Darken), Dagg Cruelmight (Bully), Banker Biggs (Invest!), Valdera Highborne (Impervious), Marei Loom
	// (Next...), Lilayell Suntear (Mini Portal), Sky Captain Smiggs (Bombardment), Anarii Duskgrove (Summon Protectors), Kazamon
	// Steelskin (Steelskin), Hesutu Stonewind (Earthmother's Rage) and Kara Stamper (Soul Weave); and the four fixed bosses of the
	// Heist finale: Magistrix Norroa (Kaironomaly), Archmage Vargoth (Arcane Runes), Archmage Kalec (Dragonwrath) and Archmage
	// Khadgar (Summon Elemental, and the Kirin Tor Elemental it summons). The texts are the client's
	// (facts/violet-hold-2026-09.json); what a text leaves open is decided in the row's adaptation notes
	// (reference/encounters/F18.json). A clickable power that would do nothing is not offered, by a condition that reads only
	// what is exact in the boss's fair view (boards, heroes, its own hand, zone counts, its own hero power's tags). Every random
	// choice uses the game RNG and is reported as a random event. The only state is entity tags: Bombardment's last shot on the
	// hero power (TAG_SCRIPT_DATA_NUM_1), the run player's TIMEOUT under Kaironomaly, the hero's own Spell Damage under
	// Dragonwrath, and the rules of Actions/EncounterRules.F18.cs (SelfDamageImmune on Steelskin).
	public static partial class DungeonRunEncounters
	{
		/// <summary>DALA_BOSS_62e Darkened, "-2 Attack until next turn" (Darken).</summary>
		public const string Darkened = "DALA_BOSS_62e";
		/// <summary>DALA_BOSS_64e Capital Gains, "+4/+4." (Invest!).</summary>
		public const string CapitalGains = "DALA_BOSS_64e";
		/// <summary>DALA_BOSS_65p Impervious: the most damage the boss's hero takes from one hit.</summary>
		public const int ImperviousCap = 3;
		/// <summary>DALA_BOSS_70p Bombardment: the game turns after a shot before it is offered again (the run player's turn and the boss's next: it fires every other boss turn).</summary>
		public const int BombardmentReload = 4;
		/// <summary>DALA_BOSS_71p Summon Protectors: the 2/2 Treant with Taunt (the wiki's summoned minion: Cenarius's Treant).</summary>
		public const string ProtectorTreant = "EX1_573t";
		/// <summary>DALA_BOSS_73e Earthmother's Rancor, "Windfury" (Earthmother's Rage).</summary>
		public const string EarthmothersRancor = "DALA_BOSS_73e";
		/// <summary>DALA_BOSS_60p Summon Elemental: the Kirin Tor Elemental (2/3, "Your spells cast an additional time.").</summary>
		public const string KirinTorElemental = "DALA_504";

		/// <summary>
		/// DALA_BOSS_67p Arcane Runes, "Add a random Arcane spell to your hand": the wiki's list of the cards the power generates
		/// (Arcane Blast, Arcane Missiles, Arcane Explosion, Arcane Intellect, Greater Arcane Missiles).
		/// </summary>
		public static readonly IReadOnlyList<string> ArcaneRunes = new[] { "AT_004", "EX1_277", "CS2_025", "CS2_023", "CFM_623" };

		/// <summary>The Arcane spells Arcane Runes may add for <paramref name="c"/>: the scripted ones of <see cref="ArcaneRunes"/> the run's generation filter allows.</summary>
		public static IReadOnlyList<Card> ArcaneRunesPool(Game g, Controller c)
		{
			using (g.GenerateFor(c))
				return ArcaneRunes.Select(Cards.FromId).Where(card => card != null && card.Implemented && !g.BlocksGenerated(card)).ToArray();
		}

		/// <summary>DALA_BOSS_70p Bombardment is loaded: never fired, or <see cref="BombardmentReload"/> game turns have passed since its last shot.</summary>
		public static bool BombardmentReady(HeroPower power) =>
			power != null && (power[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0 || power.Game.Turn >= power[GameTag.TAG_SCRIPT_DATA_NUM_1] + BombardmentReload);

		/// <summary>
		/// DALA_BOSS_61p Web Grab, "Your opponent summons a minion from their hand": Dirty Rat's pull. One random minion of the
		/// opponent's hand (the game RNG) leaves it and is summoned to the opponent's board, without its Battlecry; nothing when
		/// that board is full or that hand holds no minion. In a fair view the opponent's hidden cards are no minions, so a
		/// search pulls nothing; the real pull is a public event.
		/// </summary>
		internal static Minion WebGrab(Game g, Controller c, IEntity s)
		{
			Controller op = c.Opponent;
			if (op.BoardZone.IsFull || g.State == State.COMPLETE) return null;
			var minions = op.HandZone.Where(p => p is Minion).ToList();
			if (minions.Count == 0) return null;
			var pulled = (Minion)minions[g.Random.Next(minions.Count)];
			g.OnRandomHappened(true);
			var stack = new TaskStack { Playables = new List<IPlayable> { pulled } };
			new RemoveFromHand(EntityType.STACK).Process(g, c, s, null, stack);
			if (stack.Playables.Count == 0) return null;
			new SummonOpTask().Process(g, c, s, null, stack);
			g.AuraUpdate();
			return pulled.Zone == op.BoardZone ? pulled : null;
		}

		/// <summary>
		/// DALA_BOSS_74p Soul Weave, "Discard a card. Summon a random minion of the same cost": one random card of the owner's
		/// hand is discarded (the engine's discard), then one random scripted minion of any class that costs what the card cost in
		/// hand (the run's generation filter for the owner) is summoned. Nothing is summoned when no minion has that Cost or the
		/// board is full. Returns the discarded card.
		/// </summary>
		internal static IPlayable SoulWeave(Game g, Controller c, IEntity s)
		{
			if (c.HandZone.Count == 0 || g.State == State.COMPLETE) return null;
			IPlayable discarded = c.HandZone[g.Random.Next(c.HandZone.Count)];
			g.OnRandomHappened(true);
			int cost = discarded.Cost;
			Generic.DiscardBlock.Invoke(c, discarded);
			if (g.State == State.COMPLETE || c.BoardZone.IsFull) return discarded;
			SummonRandom(g, c, s, RuleMinions(g, c, s, "cost" + cost, card => card.Cost == cost));
			return discarded;
		}

		/// <summary>
		/// DALA_504 Kirin Tor Elemental's trigger: after its controller casts a spell, a copy is cast again on the same target
		/// (RecastRules, Pirate's Mark's rule). A recast copy never triggers it, and a Secret or a Quest is not cast twice.
		/// </summary>
		private static Trigger ElementalRecast() => new Trigger(TriggerType.AFTER_CAST)
		{
			TriggerSource = TriggerSource.FRIENDLY,
			SingleTask = new CustomTask((g, c, s, t, stack) =>
			{
				if (!(t is Spell spell) || RecastRules.IsRecast(spell) || spell.Card.IsSecret || spell.Card.IsQuest || g.State == State.COMPLETE) return;
				ICharacter target = spell.CardTarget != 0 && g.IdEntityDic.TryGetValue(spell.CardTarget, out var e) ? e as ICharacter : null;
				RecastRules.Recast(spell, target, RecastRules.ChosenOption(spell));
			}),
		};

		// A passive power whose whole rule is a tag the engine reads (Impervious, Steelskin): it is started like any passive.
		private static void TagPassive(string id, GameTag tag, int value) =>
			Add(id, EncounterCardKind.Passive,
				() => new CardDef(new Power { PowerTask = new CustomTask((g, c, s, t, stack) => EncounterRules.ActivatePower(s as HeroPower)) }),
				card => card.Tags[tag] = value, activate: true);

		private static void F18()
		{
			var aFriendlyMinion = new[] { PlayReq.REQ_TARGET_TO_PLAY, PlayReq.REQ_MINION_TARGET, PlayReq.REQ_FRIENDLY_TARGET };

			// ---------------- Sael'orn (DALA_BOSS_61h)
			// Web Grab (0): "Your opponent summons a minion from their hand." Offered while the opponent's board has room and its
			// hand holds a card (both public).
			Power("DALA_BOSS_61p", null, () => new CustomTask((g, c, s, t, stack) => WebGrab(g, c, s)),
				usable: c => !c.Opponent.BoardZone.IsFull && c.Opponent.HandZone.Count > 0);

			// ---------------- Haro Setting-Sun (DALA_BOSS_62h)
			// Darken (2): "Give all minions -2 Attack until your next turn." Darkened on every minion of both boards; its turn-start
			// trigger is its creator's (the boss's), so it is removed when the boss's next turn starts.
			Power("DALA_BOSS_62p", null, () => new CustomTask((g, c, s, t, stack) =>
			{
				Card darkened = Cards.FromId(Darkened);
				foreach (Minion m in Touchable(c).Concat(Touchable(c.Opponent)).ToList())
					Generic.AddEnchantmentBlock(g, darkened, (IPlayable)s, m);
			}), usable: c => Touchable(c).Any() || Touchable(c.Opponent).Any());
			Enchantment(Darkened, () => new Power
			{
				Enchant = new Enchant(Effects.Attack_N(-2)),
				Trigger = new Trigger(TriggerType.TURN_START) { SingleTask = RemoveEnchantmentTask.Task, RemoveAfterTriggered = true },
			});

			// ---------------- Dagg Cruelmight (DALA_BOSS_63h)
			// Bully (2): "Deal $2 damage to a damaged minion." Either board; a hero power, so no Spell Damage.
			Power("DALA_BOSS_63p", new[] { PlayReq.REQ_TARGET_TO_PLAY, PlayReq.REQ_MINION_TARGET, PlayReq.REQ_DAMAGED_TARGET },
				() => new DamageTask(2, EntityType.TARGET));

			// ---------------- Banker Biggs (DALA_BOSS_64h)
			// Invest! (4): "Return a friendly minion to your hand. Give it +4/+4." The bounce is the engine's (its enchantments are
			// lost), then Capital Gains in hand. Offered only while the boss's hand has room (a full hand would destroy the minion).
			Power("DALA_BOSS_64p", aFriendlyMinion, () => new CustomTask((g, c, s, t, stack) =>
			{
				if (!(t is Minion m) || m.Controller != c) return;
				new ReturnHandTask(EntityType.TARGET).Process(g, c, s, m, stack);
				if (m.Zone == c.HandZone) Generic.AddEnchantmentBlock(g, Cards.FromId(CapitalGains), (IPlayable)s, m);
			}), usable: c => !c.HandZone.IsFull);
			Enchantment(CapitalGains, () => new Power { Enchant = new Enchant(Effects.AttackHealth_N(4)) });

			// ---------------- Valdera Highborne (DALA_BOSS_65h)
			// Impervious (passive): "Your hero can only take up to 3 damage at a time." Animated Armor's hero damage cap (the
			// family 02 rule EncounterRules.HeroDamageCap), carried by the hero power.
			TagPassive("DALA_BOSS_65p", EncounterRules.HeroDamageCap, ImperviousCap);

			// ---------------- Marei Loom (DALA_BOSS_66h)
			// Next... (0): "Swap the left-most card in each player's hand with one in their deck." Both players, the boss first
			// (EncounterRules.SwapLeftmostWithDeck); offered while either player has a card in hand and one in deck (zone counts).
			Power("DALA_BOSS_66p", null, () => new CustomTask((g, c, s, t, stack) =>
			{
				EncounterRules.SwapLeftmostWithDeck(c, s);
				EncounterRules.SwapLeftmostWithDeck(c.Opponent, s);
			}), usable: c => c.HandZone.Count > 0 && c.DeckZone.Count > 0 || c.Opponent.HandZone.Count > 0 && c.Opponent.DeckZone.Count > 0);

			// ---------------- Lilayell Suntear (DALA_BOSS_68h)
			// Mini Portal (passive): "After you cast a spell, summon a random 1-cost minion." Any class, behind the run's generation
			// filter for the boss; nothing with a full board.
			Passive("DALA_BOSS_68p", () => new Power
			{
				Trigger = new Trigger(TriggerType.AFTER_CAST)
				{
					TriggerSource = TriggerSource.FRIENDLY,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (!(t is Spell) || g.State == State.COMPLETE) return;
						SummonRandom(g, c, s, RuleMinions(g, c, s, "cost1", card => card.Cost == 1));
					}),
				},
			});

			// ---------------- Sky Captain Smiggs (DALA_BOSS_70h)
			// Bombardment (2): "Deal $2 damage to all minions. Takes 2 turns to reload." Both boards, no Spell Damage; the shot's
			// turn is kept on the power, which is offered again BombardmentReload game turns later (the boss's turn after next).
			Power("DALA_BOSS_70p", null, () => new CustomTask((g, c, s, t, stack) =>
			{
				if (s is HeroPower power) power[GameTag.TAG_SCRIPT_DATA_NUM_1] = g.Turn;
				foreach (Minion m in Touchable(c).Concat(Touchable(c.Opponent)).ToList())
					new DamageTask(2, EntityType.TARGET).Process(g, c, s, m, stack);
			}), usable: c => BombardmentReady(c.Hero.HeroPower) && (Touchable(c).Any() || Touchable(c.Opponent).Any()));

			// ---------------- Anarii Duskgrove (DALA_BOSS_71h)
			// Summon Protectors (2): "Summon a 2/2 Treant with Taunt." It needs a free board slot.
			Power("DALA_BOSS_71p", null, () => new SummonTask(ProtectorTreant), usable: c => !c.BoardZone.IsFull);

			// ---------------- Kazamon Steelskin (DALA_BOSS_72h)
			// Steelskin (passive): "You can't damage yourself." The power's SelfDamageImmune tag (EncounterRules.PreventsSelfDamage).
			TagPassive("DALA_BOSS_72p", EncounterRules.SelfDamageImmune, 1);

			// ---------------- Hesutu Stonewind (DALA_BOSS_73h)
			// Earthmother's Rage (2): "Give a friendly minion Windfury." Earthmother's Rancor.
			Power("DALA_BOSS_73p", aFriendlyMinion, () => new AddEnchantmentTask(EarthmothersRancor, EntityType.TARGET));
			Enchantment(EarthmothersRancor, () => new Power { Enchant = new Enchant(Effects.Windfury) });

			// ---------------- Kara Stamper (DALA_BOSS_74h)
			// Soul Weave (3): "Discard a card. Summon a random minion of the same cost." Offered while the boss holds a card and its
			// board has room.
			Power("DALA_BOSS_74p", null, () => new CustomTask((g, c, s, t, stack) => SoulWeave(g, c, s)),
				usable: c => c.HandZone.Count > 0 && !c.BoardZone.IsFull);

			// ================================================================ the Heist finale
			// ---------------- Magistrix Norroa (DALA_BOSS_43h)
			// Kaironomaly (passive): "Your opponent has 25 seconds to take their turn." At the start of each of the opponent's turns
			// its timer (TIMEOUT, the client's turn clock; NativeMatch enforces a short one) becomes 25 seconds unless it is shorter.
			Passive("DALA_BOSS_43p", () => new Power
			{
				Trigger = new Trigger(TriggerType.TURN_START)
				{
					EitherTurn = true,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (g.CurrentPlayer == c.Opponent) EncounterRules.ShortenTurn(c.Opponent, EncounterRules.KaironomalySeconds);
					}),
				},
			});

			// ---------------- Archmage Vargoth (DALA_BOSS_67h)
			// Arcane Runes (0): "Add a random Arcane spell to your hand." One of the wiki's five; offered while the hand has room.
			Power("DALA_BOSS_67p", null, () => new CustomTask((g, c, s, t, stack) =>
			{
				var pool = ArcaneRunesPool(g, c);
				if (pool.Count == 0 || c.HandZone.IsFull) return;
				g.OnRandomHappened(true);
				Generic.AddHandPhase.Invoke(c, Entity.FromCard(c, pool[g.Random.Next(pool.Count)]));
			}), usable: c => !c.HandZone.IsFull);

			// ---------------- Archmage Kalec (DALA_BOSS_69h)
			// Dragonwrath (2): "Give your Hero Spell Damage +1." The hero's own Spell Damage (the controller's Spell Damage counts
			// it), for the rest of the match, cumulative.
			Power("DALA_BOSS_69p", null, () => new CustomTask((g, c, s, t, stack) =>
			{
				int own = c.Hero.NativeTags.TryGetValue(GameTag.SPELLPOWER, out int v) ? v : 0;
				c.Hero[GameTag.SPELLPOWER] = own + 1;
			}));

			// ---------------- Archmage Khadgar (DALA_BOSS_60h)
			// Summon Elemental (3): "Summon a 2/3 Elemental. It copies your spells." The Kirin Tor Elemental; a free slot needed.
			Power("DALA_BOSS_60p", null, () => new SummonTask(KirinTorElemental), usable: c => !c.BoardZone.IsFull);
			// Kirin Tor Elemental: "Your spells cast an additional time." (its controller's spells: RecastRules).
			BossCard(KirinTorElemental, () => new CardDef(new Power { Trigger = ElementalRecast() }));
		}
	}
}
