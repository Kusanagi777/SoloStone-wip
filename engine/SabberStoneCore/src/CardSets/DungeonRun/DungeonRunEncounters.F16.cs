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
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.DungeonRun
{
	// Phase 8, family 16: Dalaran Heist bosses C (DALA_BOSS_31 to 42, 44 and 45). Fourteen encounters and their boss-only cards:
	// Timothy Jones (Bling it On!), Kizi Copperclip (Makeover, auto-cast, and the four haircuts), Gold Elemental (Made of Coins),
	// the Carousel Gryphon (Merry Go Round: the board rotation of Actions.EncounterRules.Rotate), Soothsayer Zoie (Healing Hands),
	// Draemus (Import "Pet", Build-A-Beast's craft, and the Exotic Pet), Ol' Toomba (Tales of Fortune: the old Treasures),
	// Tipsi Wobblerune (Portal Party and PARTY PORTAL!), Aki the Brilliant (For The Light!, auto-cast), Oxana Demonslay
	// (Immolation Aura), Ungan Oddkind (Summon Companion), P.O.G.O. (Pogoshuffle), Erekem (Dark Tidings) and Moragg (Ray of
	// Suffering). The texts are the client's (facts/violet-hold-2026-09.json); what a text leaves open is decided in each row's
	// adaptation notes (data/dungeon-run/reference/encounters/F16.json). A clickable power that would do nothing is not offered,
	// by a condition that reads only what is exact in the boss's fair view (the boards, its own hand, zone counts); every random
	// choice uses the game RNG and is reported as a random event; the only state is public tags (the Golden tag, Ray of
	// Suffering's count on the power). The member names carry the family's bosses (every family's file is part of one class).
	public static partial class DungeonRunEncounters
	{
		/// <summary>DALA_BOSS_31p Bling it On!: the enchantment of a minion that is already Golden ("Has +4/+4. This minion is STUNNING!").</summary>
		public const string BlingedOut = "DALA_BOSS_31e";
		/// <summary>DALA_BOSS_32p Makeover: the four haircuts (Stealth, Taunt, +2 Health, +2 Attack), each with the same chance.</summary>
		public static readonly IReadOnlyList<string> KiziHaircuts = new[] { "DALA_BOSS_32e1", "DALA_BOSS_32e2", "DALA_BOSS_32e3", "DALA_BOSS_32e4" };
		/// <summary>DALA_BOSS_33p Made of Coins: the Coin it adds.</summary>
		public const string GoldElementalCoin = "GAME_005";
		/// <summary>DALA_BOSS_36p Import "Pet": the crafted pet's client card (no text; its name names the crafted minion).</summary>
		public const string ExoticPet = "DALA_BOSS_36t";
		/// <summary>
		/// DALA_BOSS_37p Tales of Fortune: the "old Treasures" it shuffles into the boss's deck (the wiki's generated cards, the
		/// Kobolds and Catacombs Dungeon Run's treasures, less Gloves of Mugging and Aleatoric Cube: the row's power note).
		/// </summary>
		public static readonly IReadOnlyList<string> ToombaTreasures = new[]
		{
			"LOOTA_806", "LOOTA_811", "LOOTA_812", "LOOTA_816", "LOOTA_819", "LOOTA_821", "LOOTA_826", "LOOTA_827", "LOOTA_835", "LOOTA_836",
			"LOOTA_837", "LOOTA_838", "LOOTA_839", "LOOTA_841", "LOOTA_842a", "LOOTA_842b", "LOOTA_843", "LOOTA_847", "LOOT_998k", "LOOT_998l",
		};
		/// <summary>DALA_BOSS_38p Portal Party: the seven Portals (the wiki's generated cards), each with the same chance.</summary>
		public static readonly IReadOnlyList<string> TipsiPortals = new[] { "KAR_073", "GVG_003", "DALA_BOSS_38t", "KAR_077", "KAR_091", "KAR_075", "KAR_076" };
		/// <summary>DALA_BOSS_38t PARTY PORTAL!: the Partygoers (the wiki's summoned minions), each with the same chance.</summary>
		public static readonly IReadOnlyList<string> TipsiPartygoers = new[] { "CFM_659", "BRM_019", "KARA_13_22", "GIL_805", "DAL_721" };
		/// <summary>KARA_13_22 Mime ("Always silenced."): a Partygoer; it enters play silenced.</summary>
		public const string PartyMime = "KARA_13_22";
		/// <summary>DALA_BOSS_41p Summon Companion: Animal Companion's three (Huffer, Leokk, Misha).</summary>
		public static readonly IReadOnlyList<string> UnganCompanions = new[] { "NEW1_034", "NEW1_033", "NEW1_032" };
		/// <summary>DALA_BOSS_42p Pogoshuffle: the copies it shuffles in.</summary>
		public const int PogoshuffleCopies = 3;
		/// <summary>DALA_BOSS_44p Dark Tidings: the enchantment of the revealed minion ("+2/+2.").</summary>
		public const string ErekemForseen = "DALA_BOSS_44e";
		/// <summary>DALA_BOSS_34p Merry Go Round: the direction, as the run player sees the board (the row's power note).</summary>
		public const bool CarouselClockwise = false;

		/// <summary>One of <paramref name="list"/> by the game RNG (a random event when there is a choice); default for an empty list.</summary>
		private static T HeistCPick<T>(Game g, IReadOnlyList<T> list)
		{
			if (list.Count == 0) return default;
			if (list.Count == 1) return list[0];
			g.OnRandomHappened(true);
			return list[g.Random.Next(list.Count)];
		}

		/// <summary>DALA_BOSS_31p Bling it On!: the minion is Golden (the client's PREMIUM tag).</summary>
		public static bool IsGolden(IPlayable p) => p != null && p[GameTag.PREMIUM] != 0;

		/// <summary>
		/// DALA_BOSS_36p Import "Pet": one Build-A-Beast part pool (the engine's pinned "First" or "Second" Beasts of this game's
		/// format) less the cards the run's generation filter denies <paramref name="c"/>.
		/// </summary>
		public static List<Card> ExoticPetParts(Controller c, bool first)
		{
			Game g = c.Game;
			string format = LegacyGenerationV1.Format(g.FormatType);
			var pool = LegacyGenerationV1.Get(format + (first ? "/Beasts/First" : "/Beasts/Second"));
			using (g.GenerateFor(c)) return OfflineCardPools.ForGame(g, pool).Where(card => card.Implemented).ToList();
		}

		/// <summary>
		/// DALA_BOSS_44p Dark Tidings: moves <paramref name="card"/> to the top of its owner's deck (the next card drawn) without
		/// leaving the deck, as a shuffle reorders it: no zone change is recorded.
		/// </summary>
		public static void PutOnTopInPlace(IPlayable card)
		{
			Model.Zones.DeckZone deck = card.Controller.DeckZone;
			if (card.Zone != deck) return;
			int count = deck.Count, from = -1;
			for (int i = 0; i < count; i++) if (ReferenceEquals(deck[i], card)) { from = i; break; }
			if (from < 0 || from == count - 1) return;
			for (int i = from; i < count - 1; i++) deck.SetEntity(i, deck[i + 1]);
			deck.SetEntity(count - 1, card);
		}

		// The Exotic Pet: Build-A-Beast's composed minion (ChoiceAction.BUILDABEAST, the Zombeast rule) takes the client name of
		// DALA_BOSS_36t. Runs after the second Discover is answered; the pet is the hand's newest card made of that answer.
		private static ISimpleTask NameExoticPet => new CustomTask((g, c, s, t, stack) =>
		{
			if (t == null) return;
			IPlayable pet = c.HandZone.LastOrDefault(p => p.Card[GameTag.MODULAR_ENTITY_PART_2] == t.Card.AssetId && p.Card.Name == "Zombeast");
			if (pet != null) pet.Card.Name = Cards.FromId(ExoticPet).Name;
		});

		private static void F16()
		{
			var aMinion = new[] { PlayReq.REQ_TARGET_TO_PLAY, PlayReq.REQ_MINION_TARGET };

			// ---------------- Timothy Jones (DALA_BOSS_31h)
			// Bling it On! (2): "Make a minion Golden. If it's already Golden, give it +4/+4." Any minion. Golden is the client's
			// PREMIUM tag (drawn golden, no rule of its own); an already Golden minion gets Blinged Out! (+4/+4).
			Power("DALA_BOSS_31p", aMinion, () => new CustomTask((g, c, s, t, stack) =>
			{
				if (!(t is Minion m)) return;
				if (IsGolden(m)) Generic.AddEnchantmentBlock(g, Cards.FromId(BlingedOut), (IPlayable)s, m);
				else m[GameTag.PREMIUM] = 1;
			}));
			Enchantment(BlingedOut, () => new Power { Enchant = new Enchant(Effects.AttackHealth_N(4)) });

			// ---------------- Kizi Copperclip (DALA_BOSS_32h)
			// Makeover (1): "Give a friendly minion a random haircut." Auto-cast (the wiki): at the start of her turn, with (1) Mana
			// and a friendly minion, a random friendly minion gets one of the four haircuts. Never an option of the boss's AI.
			AutoCast("DALA_BOSS_32p", c => Touchable(c).Any(), (g, c, s, stack) =>
			{
				Minion m = HeistCPick(g, Touchable(c).ToList());
				if (m != null) Generic.AddEnchantmentBlock(g, Cards.FromId(HeistCPick(g, KiziHaircuts)), (IPlayable)s, m);
			});
			Enchantment("DALA_BOSS_32e1", () => new Power { Enchant = new Enchant(Effects.StealthEff) });
			Enchantment("DALA_BOSS_32e2", () => new Power { Enchant = new Enchant(Effects.TauntEff) });
			Enchantment("DALA_BOSS_32e3", () => new Power { Enchant = new Enchant(Effects.Health_N(2)) });
			Enchantment("DALA_BOSS_32e4", () => new Power { Enchant = new Enchant(Effects.Attack_N(2)) });

			// ---------------- Gold Elemental (DALA_BOSS_33h)
			// Made of Coins (passive): "Whenever you take damage, add a Coin to your hand." One Coin per hit to his hero.
			Passive("DALA_BOSS_33p", () => new Power
			{
				Trigger = new Trigger(TriggerType.TAKE_DAMAGE)
				{
					TriggerSource = TriggerSource.HERO,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (c.HandZone.IsFull || g.State == State.COMPLETE) return;
						Generic.AddHandPhase.Invoke(c, Entity.FromCard(c, Cards.FromId(GoldElementalCoin),
							new EntityData { [GameTag.CREATOR] = s.Id, [GameTag.DISPLAYED_CREATOR] = s.Id }));
					}),
				},
			});

			// ---------------- Carousel Gryphon (DALA_BOSS_34h)
			// Merry Go Round (passive): "At the end of your turn, merrily rotate all minions around the board." One step of the
			// ring of both rows, counter-clockwise as the run player sees it (EncounterRules.Rotate); a run shrine never moves.
			Passive("DALA_BOSS_34p", () => new Power
			{
				Trigger = new Trigger(TriggerType.TURN_END)
				{
					SingleTask = new CustomTask((g, c, s, t, stack) => EncounterRules.Rotate(c.Opponent, CarouselClockwise, s)),
				},
			});

			// ---------------- Soothsayer Zoie (DALA_BOSS_35h)
			// Healing Hands (passive): "At the start of your turn, restore #2 Health to all friendly minions."
			Passive("DALA_BOSS_35p", () => new Power
			{
				Trigger = new Trigger(TriggerType.TURN_START) { TriggerSource = TriggerSource.FRIENDLY, SingleTask = new HealTask(2, EntityType.MINIONS) },
			});

			// ---------------- Draemus (DALA_BOSS_36h)
			// Import "Pet" (2): "Craft a custom Exotic Pet." Build-A-Beast's craft (the wiki): a Discover among three first-part
			// Beasts, then among three second-part Beasts, each pool through the boss's generation filter; the composed minion
			// (the Zombeast rule) is named Exotic Pet. Not offered with a full hand.
			Power("DALA_BOSS_36p", new[] { PlayReq.REQ_HAND_NOT_FULL }, () => new CustomTask((g, c, s, t, stack) =>
			{
				if (c.HandZone.IsFull) return;
				List<Card> first = ExoticPetParts(c, true), second = ExoticPetParts(c, false);
				if (first.Count == 0 || second.Count == 0) return;
				var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
				g.OnRandomHappened(true);
				ctx.Discover(ctx.SampleDistinct(first, 3), ChoiceAction.BUILDABEAST);
				ctx.Discover(ctx.SampleDistinct(second, 3), ChoiceAction.BUILDABEAST, NameExoticPet);
			}));
			// Exotic Pet: the client's untexted 1/1 the crafted pet is named after.
			Token(ExoticPet);

			// ---------------- Ol' Toomba (DALA_BOSS_37h)
			// Tales of Fortune (2): "Shuffle a random old Treasure into your deck." Shown as it is shuffled (his own-deck ledger
			// holds it).
			Power("DALA_BOSS_37p", null, () => new CustomTask((g, c, s, t, stack) =>
			{
				if (c.DeckZone.IsFull) return;
				using var shuffle = g.Facts.Shuffle(c.Id);
				IPlayable treasure = Entity.FromCard(c, Cards.FromId(HeistCPick(g, ToombaTreasures)));
				treasure[GameTag.DISPLAYED_CREATOR] = s.Id;
				Generic.ShuffleIntoDeck(c, s, treasure, revealGenerated: true);
			}), usable: c => !c.DeckZone.IsFull);

			// ---------------- Tipsi Wobblerune (DALA_BOSS_38h)
			// Portal Party (2): "Add a random Portal to your hand." Not offered with a full hand.
			Power("DALA_BOSS_38p", null, () => new CustomTask((g, c, s, t, stack) =>
			{
				if (c.HandZone.IsFull) return;
				Generic.AddHandPhase.Invoke(c, Entity.FromCard(c, Cards.FromId(HeistCPick(g, TipsiPortals)),
					new EntityData { [GameTag.CREATOR] = s.Id, [GameTag.DISPLAYED_CREATOR] = s.Id }));
			}), usable: c => !c.HandZone.IsFull);
			// PARTY PORTAL! (4): "Summon a random Partygoer." One of the wiki's five; a Mime enters play silenced.
			BossCard("DALA_BOSS_38t", () => new CardDef(Reqs(PlayReq.REQ_NUM_MINION_SLOTS), new Power
			{
				PowerTask = new CustomTask((g, c, s, t, stack) =>
				{
					if (c.BoardZone.IsFull) return;
					var guest = (Minion)Entity.FromCard(c, Cards.FromId(HeistCPick(g, TipsiPartygoers)));
					Generic.SummonBlock(g, guest, -1, s);
					if (guest.Card.Id == PartyMime && guest.Zone == c.BoardZone) guest.IsSilenced = true;
				}),
			}));
			// Mime (5/6): "Always silenced." No behaviour of its own (PARTY PORTAL! silences it as it enters play).
			Token(PartyMime);

			// ---------------- Aki the Brilliant (DALA_BOSS_39h)
			// For The Light! (2): "Give all minions in your hand +1/+1." Auto-cast (the wiki): at the start of her turn, with (2)
			// Mana and a minion in her hand. Never an option of the boss's AI.
			AutoCast("DALA_BOSS_39p", c => c.HandZone.Any(p => p is Minion), (g, c, s, stack) =>
			{
				Card light = Cards.FromId("DALA_BOSS_39e");
				foreach (IPlayable p in c.HandZone.Where(p => p is Minion).ToList()) Generic.AddEnchantmentBlock(g, light, (IPlayable)s, p);
			});
			Enchantment("DALA_BOSS_39e", () => new Power { Enchant = new Enchant(Effects.AttackHealth_N(1)) });

			// ---------------- Oxana Demonslay (DALA_BOSS_40h)
			// Immolation Aura (passive): "After a minion attacks you, deal 2 damage to it." An enemy minion whose attack on her hero
			// has resolved, while it is still on the board; a hero power's damage (no Spell Damage).
			Passive("DALA_BOSS_40p", () => new Power
			{
				Trigger = new Trigger(TriggerType.AFTER_ATTACK)
				{
					TriggerSource = TriggerSource.ENEMY,
					Condition = new Conditions.SelfCondition(p => p is Minion && p.Game.CurrentEventData?.EventTarget is Hero h && h.Controller != p.Controller),
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (!(t is Minion m) || m.Controller == c || m.Zone != m.Controller.BoardZone || m.ToBeDestroyed || g.State == State.COMPLETE) return;
						Generic.DamageCharFunc.Invoke((IPlayable)s, m, 2, false);
					}),
				},
			});

			// ---------------- Ungan Oddkind (DALA_BOSS_41h)
			// Summon Companion (4): "Summon a random Animal Companion." Needs a free board slot.
			Power("DALA_BOSS_41p", new[] { PlayReq.REQ_NUM_MINION_SLOTS }, () => new CustomTask((g, c, s, t, stack) =>
			{
				if (c.BoardZone.IsFull) return;
				Generic.SummonBlock(g, (Minion)Entity.FromCard(c, Cards.FromId(HeistCPick(g, UnganCompanions))), -1, s);
			}));

			// ---------------- P.O.G.O. (DALA_BOSS_42h)
			// Pogoshuffle (2): "Choose a minion. Shuffle 3 copies of it into your deck." Any minion; new copies of its card, shown
			// as they are shuffled (the target was public; his own-deck ledger holds them).
			Power("DALA_BOSS_42p", aMinion, () => new CustomTask((g, c, s, t, stack) =>
			{
				if (!(t is Minion m)) return;
				using var shuffle = g.Facts.Shuffle(c.Id);
				for (int i = 0; i < PogoshuffleCopies && !c.DeckZone.IsFull; i++)
				{
					IPlayable copy = Entity.FromCard(c, m.Card);
					copy[GameTag.DISPLAYED_CREATOR] = s.Id;
					Generic.ShuffleIntoDeck(c, s, copy, revealGenerated: true);
				}
			}));

			// ---------------- Erekem (DALA_BOSS_44h)
			// Dark Tidings (1): "Reveal a minion from your deck. Give it +2/+2 and put it on top." A random minion of his own deck
			// is shown to both players, gets Forseen and becomes the next card drawn (recorded for the own-deck ledger). Offered
			// while the deck holds a card (its count is public; whether a minion is left is the boss's own estimate).
			Power("DALA_BOSS_44p", null, () => new CustomTask((g, c, s, t, stack) =>
			{
				IPlayable pick = HeistCPick(g, c.DeckZone.Where(p => p is Minion).ToList());
				if (pick == null) return;
				Generic.RevealCardBlock((IPlayable)s, pick);
				Generic.AddEnchantmentBlock(g, Cards.FromId(ErekemForseen), (IPlayable)s, pick);
				PutOnTopInPlace(pick);
				EncounterRules.DeckTopKnown(c, new[] { pick.Card.Id });
			}), usable: c => c.DeckZone.Count > 0);
			Enchantment(ErekemForseen, () => new Power { Enchant = new Enchant(Effects.AttackHealth_N(2)) });

			// ---------------- Moragg (DALA_BOSS_45h)
			// Ray of Suffering (3): "Deal @ damage to the enemy hero. (increases each use)" The number is the power's
			// TAG_SCRIPT_DATA_NUM_1 (printed 1): dealt, then raised by 1. A hero power's damage (no Spell Damage).
			Power("DALA_BOSS_45p", null, () => new CustomTask((g, c, s, t, stack) =>
			{
				int amount = Math.Max(1, s[GameTag.TAG_SCRIPT_DATA_NUM_1]);
				s[GameTag.TAG_SCRIPT_DATA_NUM_1] = amount + 1;
				Generic.DamageCharFunc.Invoke((IPlayable)s, c.Opponent.Hero, amount, false);
			}));
		}
	}
}
