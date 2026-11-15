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
	// Phase 8, family 22: Tombs of Terror bosses D (ULDA_BOSS_60 to ULDA_BOSS_79 without 64 and 67) and Siamat's Servant
	// (ULDA_026). Twelve clickable powers, three passive ones (Plaguebringer, Mistress of Magic, Shockwave) and three the boss
	// casts by rule, the wiki's "auto-cast" (Mirage; Lord of Destruction and Massive Tentacles through AutoCast). The texts are
	// the client's (facts/violet-hold-2026-09.json). A power works on public zones (the boards, the heroes) or on the boss's own
	// hand; Ogre-Sized Thief takes a hidden card of the run player's deck by the deck-steal rule of TRLA_186 (the boss learns that
	// card and nothing else), and Mirage copies a hidden card of the run player's hand into that same hand (the boss never learns
	// it). A power that would do nothing is not offered, by a condition that reads only public zones or the boss's own hand
	// (never a deck's cards). Every random choice uses the game RNG and is reported as a random event. State: the two growing
	// powers keep their number in the power's own client script number (the @ of their text); Mirage marks its illusion with the
	// private tag EncounterRules.MirageIllusion and keeps only the illusion's public hand position on the boss's power. A run
	// shrine destroyed by a power goes dormant (ShrineRules).
	public static partial class DungeonRunEncounters
	{
		/// <summary>
		/// Family 22 (Tombs of Terror bosses D): the constants and rules its scripts share, in one nested class so that no name of
		/// it can meet a name of another family's partial file. Tests read them.
		/// </summary>
		public static class TombsD
		{
			/// <summary>ULDA_BOSS_66p Masterful Djinn: the 1/4 servant it summons.</summary>
			public const string Servant = "ULDA_026";
			/// <summary>ULDA_BOSS_66p Masterful Djinn: the keywords one of which the servant gets (the power's client refs).</summary>
			public static readonly IReadOnlyList<GameTag> DjinnKeywords = new[] { GameTag.TAUNT, GameTag.RUSH, GameTag.WINDFURY, GameTag.DIVINE_SHIELD };
			/// <summary>ULDA_BOSS_78p Massive Tentacles: the minions on the battlefield it needs.</summary>
			public const int TentaclesAt = 8;
			/// <summary>ULDA_BOSS_63p Plaguebringer: how much less the boss's spells cost, and the Overload each one adds.</summary>
			public const int PlaguebringerDiscount = 2, PlaguebringerOverload = 1;
			/// <summary>ULDA_BOSS_70p Lord of the Sun: the Spell Damage it gives for the turn.</summary>
			public const int SunSpellDamage = 3;
			/// <summary>ULDA_BOSS_72e Cursed!: the damage it deals to its minion's controller's hero at the end of that player's turn.</summary>
			public const int CurseDamage = 2;
			/// <summary>ULDA_BOSS_79p Mech "Pilot": the Attack it gives for the turn.</summary>
			public const int PilotAttack = 2;
			/// <summary>ULDA_BOSS_61p Chain Lightning: a bound on the hits of one use (a chain ends earlier by the rules below).</summary>
			public const int ChainMaxHits = 60;

			/// <summary>The growing number of Forbidden Power (60p) and Hunt the Weak (75p): the power's script number, 1 at the start.</summary>
			public static int Number(HeroPower power) => Math.Max(1, power?[GameTag.TAG_SCRIPT_DATA_NUM_1] ?? 1);

			/// <summary>A minion an effect can pick: on its board, not dormant or permanent, not already dying.</summary>
			public static bool Pickable(Minion m) => m != null && m.Zone?.Type == Zone.PLAY && !m.Untouchable && !m.ToBeDestroyed && m.Health > 0;

			/// <summary>The minions of <paramref name="c"/>'s board an effect can pick, in board order.</summary>
			public static List<Minion> Pickables(Controller c) => c.BoardZone.Where(Pickable).OrderBy(m => m.ZonePosition).ToList();

			/// <summary>ULDA_BOSS_68p "Upgraded" Fauna: the Beasts <paramref name="c"/> controls (a dormant or permanent one does not count).</summary>
			public static int Beasts(Controller c) => Pickables(c).Count(m => m.IsRace(Race.BEAST));

			/// <summary>ULDA_BOSS_78p Massive Tentacles: the minions on the battlefield (both boards; a dormant or permanent one does not count).</summary>
			public static int Battlefield(Controller c) => Pickables(c).Count + Pickables(c.Opponent).Count;

			// One of the list (game RNG, reported as a random event when there is a choice), or null for an empty list.
			private static T RandomPick<T>(Game g, IReadOnlyList<T> list) where T : class
			{
				if (list.Count == 0) return null;
				if (list.Count == 1) return list[0];
				g.OnRandomHappened(true);
				return list[g.Random.Next(list.Count)];
			}

			/// <summary>
			/// ULDA_BOSS_61p Chain Lightning: 1 damage to <paramref name="first"/>; while the minion hit last survives, the next hit
			/// goes to a minion adjacent to it on its board (a random one of its two neighbours that an effect can pick), until one
			/// dies, no neighbour is left, or a hit does nothing at all (an Immune minion). Returns the minions hit, in order.
			/// </summary>
			public static List<Minion> ChainLightning(Game g, IPlayable source, Minion first)
			{
				var hits = new List<Minion>();
				Minion current = Pickable(first) ? first : null;
				while (current != null && hits.Count < ChainMaxHits && g.State != State.COMPLETE)
				{
					bool shield = current.HasDivineShield;
					int dealt = Generic.DamageCharFunc(source, current, 1, false);
					hits.Add(current);
					if (current.IsDead || current.Zone?.Type != Zone.PLAY) break;
					if (dealt <= 0 && !shield) break;
					int at = current.ZonePosition;
					current = RandomPick(g, current.Controller.BoardZone.Where(m => Pickable(m) && Math.Abs(m.ZonePosition - at) == 1).OrderBy(m => m.ZonePosition).ToList());
				}
				return hits;
			}

			/// <summary>
			/// ULDA_BOSS_66p Masterful Djinn: a 1/4 Servant of Siamat for <paramref name="c"/> with one of Taunt, Rush, Windfury or
			/// Divine Shield (game RNG; the minion's own keyword). Returns the servant, or null with a full board.
			/// </summary>
			public static Minion Djinn(Game g, Controller c, IEntity source)
			{
				if (c.BoardZone.IsFull) return null;
				g.OnRandomHappened(true);
				GameTag keyword = DjinnKeywords[g.Random.Next(DjinnKeywords.Count)];
				var servant = (Minion)Entity.FromCard(c, Cards.FromId(Servant), new EntityData { [GameTag.DISPLAYED_CREATOR] = source.Id });
				// Taunt is a character property (an override of the card's printed Taunt), the other three are plain tags.
				if (keyword == GameTag.TAUNT) servant.HasTaunt = true;
				else servant[keyword] = 1;
				Generic.SummonBlock.Invoke(g, servant, -1, source);
				return servant.Zone?.Type == Zone.PLAY ? servant : null;
			}

			/// <summary>
			/// ULDA_BOSS_63p Plaguebringer: the extra Overload of a spell <paramref name="c"/> played, as any Overload (Tunnel Trogg's
			/// "whenever you Overload" counts it, and Thunderhead sees the spell as a card with Overload).
			/// </summary>
			internal static void Overload(Controller c, IPlayable spell, int amount)
			{
				if (amount <= 0) return;
				c.OverloadOwed += amount;
				c.OverloadThisGame += amount;
				c.Game.TriggerManager.OnOverloadTrigger(spell, amount);
				foreach (Minion trogg in c.BoardZone.Where(m => m.Card.Id == "LOE_018" && !m.IsSilenced && Pickable(m)).ToList())
					Generic.AddEnchantmentBlock(c.Game, Cards.FromId("LOE_018e"), trogg, trogg, amount);
				// Thunderhead ("After you play a card with Overload, summon two 1/1 Sparks with Rush"): a spell that Overloads only by
				// this rule is now a card with Overload too; one with its own Overload already triggered Thunderhead itself.
				if (!spell.Card.HasOverload)
					foreach (Minion thunderhead in c.BoardZone.Where(m => m.Card.Id == "BOT_407" && !m.IsSilenced && Pickable(m)).ToList())
						new SummonTask("BOT_102t", 2, SummonSide.ALTERNATE).Process(c.Game, c, thunderhead, null, null);
			}

			/// <summary>ULDA_BOSS_69p Mistress of Magic: <paramref name="spell"/> is its player's first spell played this game.</summary>
			public static bool FirstSpell(IPlayable spell) => spell is Spell && spell.Controller.NumSpellsPlayedThisGame == 0;

			// ------------------------------------------------------------------ Mirage (ULDA_BOSS_62p)
			// The illusion card carries the mark (EncounterRules.MirageIllusion: its twin's entity id times four, plus two, plus one
			// when it is the added copy); a hidden card of the run player's hand, so the boss's fair view masks it with the rest of
			// that hand. The boss's power carries only the illusion's public hand position (1-based, 0 for none), kept current by
			// MirageSlot, an aura of the power: no private entity id is ever on a card the boss sees. In the boss's fair view the
			// masked slot at that position is the illusion (MirageSlot marks it there), so the boss knows which slot it made the
			// illusion and nothing more.

			private const int ViewMark = 2;

			/// <summary>The illusion card Mirage gave and has not cleared yet (a marked card of the opponent's hand), or null.</summary>
			public static IPlayable Illusion(HeroPower power)
			{
				if (power == null) return null;
				Controller op = power.Controller.Opponent;
				foreach (IPlayable p in op.HandZone)
					if (p[EncounterRules.MirageIllusion] != 0) return p;
				// The boss's fair view (the hand's cards are masked, marks and all): the masked slot at the power's position.
				int at = power[EncounterRules.MirageIllusion];
				return at > 0 && at <= op.HandZone.Count && op.HandZone[at - 1] is Unknown ? op.HandZone[at - 1] : null;
			}

			/// <summary>The illusion's hand position the boss's power holds (1-based; 0 for none): a public fact.</summary>
			public static int IllusionSlot(HeroPower power) => power?[EncounterRules.MirageIllusion] ?? 0;

			/// <summary>The illusion's twin (the other card of the pair), or null (also in the boss's fair view, where the link is masked).</summary>
			public static IPlayable Twin(IPlayable illusion)
			{
				int link = illusion?[EncounterRules.MirageIllusion] ?? 0;
				return link >> 2 > 0 && illusion.Game.IdEntityDic.TryGetValue(link >> 2, out IPlayable p) ? p : null;
			}

			/// <summary>The illusion is the copy Mirage added (else it is the run player's original card).</summary>
			public static bool IllusionIsCopy(IPlayable illusion) => ((illusion?[EncounterRules.MirageIllusion] ?? 0) & 1) == 1;

			/// <summary>Mirage can be cast: the opponent's hand holds a card and has room (both counts are public).</summary>
			public static bool MirageReady(Controller c) => c.Opponent.HandZone.Count > 0 && !c.Opponent.HandZone.IsFull;

			/// <summary>
			/// Mirage's cast: a copy of a random card of the opponent's hand joins that hand, and one of the two (game RNG) is the
			/// illusion. Returns the copy, or null.
			/// </summary>
			public static IPlayable Mirage(Game g, Controller c, HeroPower power)
			{
				Controller op = c.Opponent;
				if (!MirageReady(c) || g.State == State.COMPLETE) return null;
				// The boss's fair view: the hand is masked, so a search that reaches the boss's next turn start copies nothing.
				if (op.HandZone.Any(p => p is Unknown)) return null;
				Clear(power);
				IPlayable original = RandomPick(g, op.HandZone.ToList());
				IPlayable copy = Entity.FromCard(op, original.Card, new EntityData { [GameTag.CREATOR] = power.Id });
				if (!Generic.AddHandPhase(op, copy) || copy.Zone != op.HandZone) return null;
				g.OnRandomHappened(true);
				bool copyIsIllusion = g.Random.Next(2) == 0;
				IPlayable illusion = copyIsIllusion ? copy : original, twin = copyIsIllusion ? original : copy;
				illusion[EncounterRules.MirageIllusion] = (twin.Id << 2) | ViewMark | (copyIsIllusion ? 1 : 0);
				power[EncounterRules.MirageIllusion] = illusion.ZonePosition + 1;
				return copy;
			}

			/// <summary>
			/// The illusion was played: its player discards a random card of the hand (its twin too, if chosen). The pair is then
			/// resolved: the twin is an ordinary card.
			/// </summary>
			internal static void IllusionPlayed(Game g, HeroPower power, IPlayable played)
			{
				if (played == null || played[EncounterRules.MirageIllusion] == 0) return;
				Clear(power);
				Controller holder = played.Controller;
				IPlayable discard = RandomPick(g, holder.HandZone.ToList());
				if (discard != null) new DiscardTask(EntityType.TARGET).Process(g, holder, power, discard, null);
			}

			/// <summary>
			/// The end of the holder's turn: the illusion leaves the hand if it is still there; with both cards still there the copy
			/// leaves and the original stays (the hand is as before). The marks are cleared.
			/// </summary>
			internal static void IllusionFades(HeroPower power)
			{
				IPlayable illusion = Illusion(power);
				if (illusion == null) { Clear(power); return; }
				IPlayable twin = Twin(illusion);
				bool illusionIsCopy = IllusionIsCopy(illusion);
				Controller holder = illusion.Controller;
				bool twinHeld = twin != null && twin.Zone == holder.HandZone;
				IPlayable leaves = twinHeld && !illusionIsCopy ? twin : illusion;
				Clear(power);
				holder.SetasideZone.Add(leaves.Zone.Remove(leaves));
			}

			// Drops the marks of the current pair: the power's position and the mark of any card of the opponent's.
			private static void Clear(HeroPower power)
			{
				Controller op = power.Controller.Opponent;
				foreach (IPlayable p in op.HandZone.Concat(op.DeckZone).Concat(op.GraveyardZone).Concat(op.SetasideZone).Concat(op.BoardZone).ToList())
					if (p.NativeTags.ContainsKey(EncounterRules.MirageIllusion)) p.NativeTags.Remove(EncounterRules.MirageIllusion);
				if (power.NativeTags.ContainsKey(EncounterRules.MirageIllusion)) power.NativeTags.Remove(EncounterRules.MirageIllusion);
			}

			/// <summary>
			/// Mirage's aura: at every aura update the power's position follows the marked illusion in the opponent's hand (cards
			/// leaving the hand to its left move it); once the illusion has left the hand the position is cleared. In the boss's
			/// fair view, where the hand is masked, it marks the masked slot at the position instead, so the view's illusion is that
			/// slot. It reads only the opponent's hand order, which is public, and the mark the engine set.
			/// </summary>
			internal static IAura MirageSlot() => new MirageSlotAura();

			private sealed class MirageSlotAura : IAura
			{
				private readonly IPlayable _owner;
				private bool _on = true;
				internal MirageSlotAura() { }
				private MirageSlotAura(MirageSlotAura prototype, IPlayable owner) { _owner = owner; _on = prototype._on; }
				IPlayable IAura.Owner => _owner;
				public void Activate(IPlayable owner)
				{
					var instance = new MirageSlotAura(this, owner);
					owner.Game.Auras.Add(instance);
					owner.OngoingEffect = instance;
				}
				public void Clone(IPlayable clone) => Activate(clone);
				public void Remove() { _owner.OngoingEffect = null; _on = false; }
				public void Update()
				{
					if (!_on) { _owner.Game.Auras.Remove(this); return; }
					int at = _owner[EncounterRules.MirageIllusion];
					if (at == 0) return;
					Controller op = _owner.Controller.Opponent;
					IPlayable marked = op.HandZone.FirstOrDefault(p => p[EncounterRules.MirageIllusion] != 0);
					if (marked == null && at <= op.HandZone.Count && op.HandZone[at - 1] is Unknown)
					{
						marked = op.HandZone[at - 1];
						marked[EncounterRules.MirageIllusion] = ViewMark;
					}
					int now = marked == null ? 0 : marked.ZonePosition + 1;
					if (now != at)
					{
						if (now == 0) _owner.NativeTags.Remove(EncounterRules.MirageIllusion);
						else _owner[EncounterRules.MirageIllusion] = now;
					}
				}
				public override string ToString() => "[MirageSlot:" + _owner + "]";
			}
		}

		private static void F22()
		{
			var aMinion = new[] { PlayReq.REQ_TARGET_TO_PLAY, PlayReq.REQ_MINION_TARGET };
			var anEnemyMinion = new[] { PlayReq.REQ_TARGET_TO_PLAY, PlayReq.REQ_MINION_TARGET, PlayReq.REQ_ENEMY_TARGET };

			// ---------------- Zaraam (ULDA_BOSS_60h)
			// Forbidden Power (4): "Deal $@ damage to a random enemy (upgrades whenever a friendly minion dies)." N (the @) is the
			// power's script number, 1 at the start, +1 when a minion of the boss's dies. A random enemy character an effect can
			// pick (the hero or a minion).
			Add("ULDA_BOSS_60p", EncounterCardKind.Power, () => new CardDef(new Power
			{
				PowerTask = new CustomTask((g, c, s, t, stack) =>
				{
					var enemies = new List<ICharacter> { c.Opponent.Hero };
					enemies.AddRange(TombsD.Pickables(c.Opponent));
					if (enemies.Count > 1) g.OnRandomHappened(true);
					ICharacter target = enemies[enemies.Count == 1 ? 0 : g.Random.Next(enemies.Count)];
					Generic.DamageCharFunc((IPlayable)s, target, TombsD.Number(s as HeroPower), false);
				}),
				Trigger = new Trigger(TriggerType.DEATH)
				{
					TriggerSource = TriggerSource.FRIENDLY,
					Condition = new SelfCondition(p => p is Minion),
					SingleTask = new CustomTask((g, c, s, t, stack) => s[GameTag.TAG_SCRIPT_DATA_NUM_1] = TombsD.Number(s as HeroPower) + 1),
				},
			}), activate: true);

			// ---------------- Kyriss (ULDA_BOSS_61h)
			// Chain Lightning (4): "Deal $1 damage to a minion. Jumps to an adjacent minion until one dies." Any minion.
			Power("ULDA_BOSS_61p", aMinion, () => new CustomTask((g, c, s, t, stack) => TombsD.ChainLightning(g, (IPlayable)s, t as Minion)));

			// ---------------- A Beautiful Oasis (ULDA_BOSS_62h)
			// Mirage (0): "Give your opponent an illusion card. If played, they discard a random card." Auto-cast (the wiki), as
			// AutoCast does: at the start of the boss's turn while the run player's hand holds a card and has room. A copy of a
			// random card of that hand joins it; one of the two is the illusion (marked by the private tag MirageIllusion; the power
			// holds only its public hand position, TombsD.MirageSlot). Played, the
			// illusion makes its player discard a random card; at the end of its player's turn it leaves the hand (both still
			// there: the copy leaves).
			Passive("ULDA_BOSS_62p", () => new Power
			{
				Aura = TombsD.MirageSlot(),
				Trigger = new MultiTrigger(
					new Trigger(TriggerType.TURN_START)
					{
						SingleTask = new CustomTask((g, c, s, t, stack) =>
						{
							int cost = (s as HeroPower)?.Cost ?? 0;
							if (c.RemainingMana < cost || !TombsD.MirageReady(c)) return;
							c.UsedMana += cost;
							s[GameTag.TAG_SCRIPT_DATA_NUM_2] = s[GameTag.TAG_SCRIPT_DATA_NUM_2] + 1;
							TombsD.Mirage(g, c, (HeroPower)s);
						}),
					},
					new Trigger(TriggerType.AFTER_PLAY_CARD)
					{
						TriggerSource = TriggerSource.ENEMY,
						SingleTask = new CustomTask((g, c, s, t, stack) => TombsD.IllusionPlayed(g, (HeroPower)s, t)),
					},
					new Trigger(TriggerType.TURN_END)
					{
						EitherTurn = true,
						SingleTask = new CustomTask((g, c, s, t, stack) =>
						{
							if (g.CurrentPlayer == c.Opponent) TombsD.IllusionFades((HeroPower)s);
						}),
					}),
			});

			// ---------------- Vessina (ULDA_BOSS_63h)
			// Plaguebringer (passive): "Your spells Overload (1) but cost (2) less." A hand aura on the boss's spells, and each spell
			// it plays (not countered) Overloads it by 1 more.
			Passive("ULDA_BOSS_63p", () => new Power
			{
				Aura = new Aura(AuraType.HAND, Effects.ReduceCost(TombsD.PlaguebringerDiscount)) { Condition = SelfCondition.IsSpell },
				Trigger = new Trigger(TriggerType.AFTER_CAST)
				{
					TriggerSource = TriggerSource.FRIENDLY,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (t is Spell spell && !spell.IsCountered) TombsD.Overload(c, spell, TombsD.PlaguebringerOverload);
					}),
				},
			});

			// ---------------- Thrud (ULDA_BOSS_65h)
			// Ogre-Sized Thief (3): "Steal a card from your opponent's deck and put it into your hand." TRLA_186's deck-steal rule
			// (DungeonRunShrines.TakeFromDeck: the boss learns exactly the card it took; a full hand burns it). Offered while the
			// run player's deck holds a card (its count is public).
			Power("ULDA_BOSS_65p", null, () => new CustomTask((g, c, s, t, stack) => DungeonRunShrines.StealFromDeck(g, c, s, _ => true)),
				usable: c => !c.Opponent.DeckZone.IsEmpty);

			// ---------------- Siamat (ULDA_BOSS_66h)
			// Masterful Djinn (2): "Summon a 1/4 servant with Taunt, Rush, Windfury, or Divine Shield (at random)." Needs a free slot.
			Power("ULDA_BOSS_66p", new[] { PlayReq.REQ_NUM_MINION_SLOTS }, () => new CustomTask((g, c, s, t, stack) => TombsD.Djinn(g, c, s)));
			// Servant of Siamat (a 1/4 Elemental, no text): the servant.
			Token(TombsD.Servant);

			// ---------------- Weaponized Wasp (ULDA_BOSS_68h)
			// "Upgraded" Fauna (2): "Deal 1 damage to an enemy minion for each Beast you control." One hit of N, N the boss's Beasts.
			// Offered while it controls one.
			Power("ULDA_BOSS_68p", anEnemyMinion, () => new CustomTask((g, c, s, t, stack) =>
			{
				int beasts = TombsD.Beasts(c);
				if (beasts > 0 && t is Minion target) Generic.DamageCharFunc((IPlayable)s, target, beasts, false);
			}), usable: c => TombsD.Beasts(c) > 0);

			// ---------------- Isiset (ULDA_BOSS_69h)
			// Mistress of Magic (passive): "Whenever a player casts their first spell, add a copy to their hand." Both players: the
			// first spell a player plays from hand this game; the copy joins that player's hand (a full hand takes none).
			Passive("ULDA_BOSS_69p", () => new Power
			{
				Trigger = new Trigger(TriggerType.CAST_SPELL)
				{
					TriggerSource = TriggerSource.ALL,
					Condition = new SelfCondition(TombsD.FirstSpell),
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						Controller caster = t?.Controller;
						if (caster == null || caster.HandZone.IsFull) return;
						Generic.AddHandPhase.Invoke(caster, Entity.FromCard(caster, t.Card, new EntityData { [GameTag.CREATOR] = s.Id, [GameTag.DISPLAYED_CREATOR] = s.Id }));
					}),
				},
			});

			// ---------------- Rajh (ULDA_BOSS_70h)
			// Lord of the Sun (1): "Gain Spell Damage +3 this turn." (ULDA_BOSS_70e "Sunshine" on the boss's hero, one turn.) Offered
			// while the boss's hand holds a spell.
			Power("ULDA_BOSS_70p", null, () => new AddEnchantmentTask("ULDA_BOSS_70e", EntityType.HERO), usable: c => c.HandZone.Any(p => p is Spell));
			Enchantment("ULDA_BOSS_70e", () => new Power { Enchant = new Enchant(GameTag.SPELLPOWER, EffectOperator.ADD, TombsD.SunSpellDamage) { IsOneTurnEffect = true } });

			// ---------------- Ammunae (ULDA_BOSS_71h)
			// Lord of Life (2): "Restore a minion's Health to full." A damaged minion.
			Add("ULDA_BOSS_71p", EncounterCardKind.Power, () => new CardDef(Reqs(aMinion), new Power { PowerTask = new HealFullTask(EntityType.TARGET) }),
				card => card.SourceTargetingPredicate = (source, target) => target is Minion m && m.Damage > 0);

			// ---------------- Setesh (ULDA_BOSS_72h)
			// Lord of Destruction (0): "Curse a random enemy minion to damage its owner each turn." Auto-cast (the wiki): at the start
			// of the boss's turn while the run player controls a minion. Cursed! (ULDA_BOSS_72e) stacks on one minion.
			AutoCast("ULDA_BOSS_72p", c => TombsD.Pickables(c.Opponent).Count > 0, (g, c, s, stack) =>
			{
				var minions = TombsD.Pickables(c.Opponent);
				if (minions.Count == 0) return;
				if (minions.Count > 1) g.OnRandomHappened(true);
				Generic.AddEnchantmentBlock(g, Cards.FromId("ULDA_BOSS_72e"), (IPlayable)s, minions[minions.Count == 1 ? 0 : g.Random.Next(minions.Count)]);
			});
			// Cursed!: "Deal 2 damage to your hero at the end of your turn." "Your" is the cursed minion's controller (the enchantment's
			// creator is the boss's power): its hero takes 2 at the end of each of its turns.
			Enchantment("ULDA_BOSS_72e", () => new Power
			{
				Trigger = new Trigger(TriggerType.TURN_END)
				{
					EitherTurn = true,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (!((s as Model.Entities.Enchantment)?.Target is Minion cursed) || cursed.Zone?.Type != Zone.PLAY || g.CurrentPlayer != cursed.Controller) return;
						Generic.DamageCharFunc((IPlayable)s, cursed.Controller.Hero, TombsD.CurseDamage, false);
					}),
				},
			});

			// ---------------- General Husam (ULDA_BOSS_73h)
			// Shockwave (passive): "All your minions have Rush." (ULDA_BOSS_73e "Sonic Boom".)
			Passive("ULDA_BOSS_73p", () => new Power { Aura = new Aura(AuraType.BOARD, "ULDA_BOSS_73e") });
			Enchantment("ULDA_BOSS_73e", () => new Power { Enchant = new Enchant(Effects.Rush) });

			// ---------------- High Prophet Barim (ULDA_BOSS_74h)
			// Burst of Light (3): "Lifesteal. Deal $1 damage to all enemy minions." The power's Lifesteal heals the boss's hero.
			// Offered while the run player controls a minion.
			Power("ULDA_BOSS_74p", null, () => new CustomTask((g, c, s, t, stack) =>
			{
				foreach (Minion m in TombsD.Pickables(c.Opponent)) Generic.DamageCharFunc((IPlayable)s, m, 1, false);
			}), usable: c => TombsD.Pickables(c.Opponent).Count > 0);

			// ---------------- Avozu (ULDA_BOSS_75h)
			// Hunt the Weak (2): "Deal $@ damage to a minion and gain 1 bonus damage. If it dies, lose all bonus damage." Any minion.
			Power("ULDA_BOSS_75p", aMinion, () => new CustomTask((g, c, s, t, stack) =>
			{
				if (!(t is Minion target) || !(s is HeroPower power)) return;
				Generic.DamageCharFunc(power, target, TombsD.Number(power), false);
				power[GameTag.TAG_SCRIPT_DATA_NUM_1] = target.IsDead ? 1 : TombsD.Number(power) + 1;
			}));

			// ---------------- Tarkus (ULDA_BOSS_76h)
			// Noble Champion (4): "Deal damage to a random enemy minion equal to its Attack." Offered while the run player controls a
			// minion.
			Power("ULDA_BOSS_76p", null, () => new CustomTask((g, c, s, t, stack) =>
			{
				var minions = TombsD.Pickables(c.Opponent);
				if (minions.Count == 0) return;
				if (minions.Count > 1) g.OnRandomHappened(true);
				Minion target = minions[minions.Count == 1 ? 0 : g.Random.Next(minions.Count)];
				Generic.DamageCharFunc((IPlayable)s, target, target.AttackDamage, false);
			}), usable: c => TombsD.Pickables(c.Opponent).Count > 0);

			// ---------------- Gorebite (ULDA_BOSS_77h)
			// Snapping Jaw (2): "Double your hero's attack this turn." +N Attack this turn (ULDA_BOSS_77e "Strong Jaw"), N the hero's
			// Attack now. Offered while the hero has Attack.
			Power("ULDA_BOSS_77p", null, () => new CustomTask((g, c, s, t, stack) =>
			{
				int attack = c.Hero.AttackDamage;
				if (attack > 0) Generic.AddEnchantmentBlock(g, Cards.FromId("ULDA_BOSS_77e"), (IPlayable)s, c.Hero, attack);
			}), usable: c => c.Hero.AttackDamage > 0);
			// The client's Strong Jaw has no one-turn tag (Sunshine and Auto Piloted do): the script marks it so the enchantment
			// itself, not only its Attack, ends with the turn.
			Add("ULDA_BOSS_77e", EncounterCardKind.Enchantment, () => new CardDef(new Power { Enchant = new Enchant(Effects.Attack_N(0)) { UseScriptTag = true, IsOneTurnEffect = true } }),
				card => card.Tags[GameTag.TAG_ONE_TURN_EFFECT] = 1);

			// ---------------- Octosari (ULDA_BOSS_78h)
			// Massive Tentacles (0): "If there are 8 or more minions on the battlefield, destroy them all." Auto-cast (the wiki): at
			// the start of the boss's turn when the battlefield holds 8 or more minions (a dormant or permanent one does not count).
			// A run shrine so destroyed goes dormant.
			AutoCast("ULDA_BOSS_78p", c => TombsD.Battlefield(c) >= TombsD.TentaclesAt, (g, c, s, stack) =>
			{
				foreach (Minion m in TombsD.Pickables(c).Concat(TombsD.Pickables(c.Opponent)).ToList())
					new DestroyTask(EntityType.TARGET).Process(g, c, s, m, stack);
			});

			// ---------------- Armadillobot (ULDA_BOSS_79h)
			// Mech "Pilot" (1): "Give a Mech +2 Attack this turn and force it to attack a random enemy minion." Any Mech; it attacks a
			// random minion of its enemy at once (EncounterRules.ForcedAttack), if there is one that can fight.
			Add("ULDA_BOSS_79p", EncounterCardKind.Power, () => new CardDef(Reqs(aMinion), new Power
			{
				PowerTask = new CustomTask((g, c, s, t, stack) =>
				{
					if (!(t is Minion mech) || !TombsD.Pickable(mech)) return;
					Generic.AddEnchantmentBlock(g, Cards.FromId("ULDA_BOSS_79e"), (IPlayable)s, mech);
					var enemies = TombsD.Pickables(mech.Controller.Opponent);
					if (enemies.Count == 0) return;
					if (enemies.Count > 1) g.OnRandomHappened(true);
					EncounterRules.ForcedAttack(mech, enemies[enemies.Count == 1 ? 0 : g.Random.Next(enemies.Count)]);
				}),
			}), card => card.SourceTargetingPredicate = (source, target) => target is Minion m && m.IsRace(Race.MECHANICAL));
			Enchantment("ULDA_BOSS_79e", () => new Power { Enchant = new Enchant(Effects.Attack_N(TombsD.PilotAttack)) { IsOneTurnEffect = true } });
		}
	}
}
