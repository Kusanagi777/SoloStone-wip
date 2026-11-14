using System;
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
	/// <summary>
	/// Phase 7 T1: the Druid, Hunter, Mage and Paladin teammates (the run's teammate draft offers them on a board-shrine run;
	/// TRLA_124/125/127 were scripted with the shrine model) and their client enchantment records. Each script sits on the client
	/// card itself. The start-of-game companions (TRLA_121, TRLA_135, TRLA_139, TRLA_164) are phase 7 D's. Every random choice
	/// uses the game RNG; every piece of state is an entity tag.
	/// </summary>
	public static partial class DungeonRunShrines
	{
		/// <summary>TRLA_133 Fireslinger's spell: the classic Fireball ("Deal $6 damage.").</summary>
		internal const string FireslingerSpell = "CS2_029";
		/// <summary>TRLA_130 Razzle Dazzler's card: the classic Arcane Missiles.</summary>
		internal const string DazzlerCard = "EX1_277";

		private static void AddT1(Dictionary<string, CardDef> cards)
		{
			// ---------------- Druid
			// TRLA_122 Direhorn Stomper: "Rush. Overkill: Summon a minion from your deck with the highest Attack." (Rush and Overkill
			// from the card data: the card's own Overkill). Ties are broken with the game RNG; nothing when the board is full.
			cards.Add("TRLA_122", Implemented(new CardDef(new Power
			{
				OverkillTask = new CustomTask((g, c, s, t, stack) => SummonHighestAttack(g, c, s as IPlayable)),
			})));
			// TRLA_123 Succoring Skyscreamer: "Whenever you gain Armor, draw a card. It costs (2) less." (the client's LOOT_204e
			// "Costs (2) less." on the drawn card; a burned or fatigue draw gets nothing).
			cards.Add("TRLA_123", Implemented(new CardDef(new Power
			{
				Trigger = new Trigger(TriggerType.ARMOR)
				{
					TriggerSource = TriggerSource.FRIENDLY,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (!(s is Minion me) || !Active(me) || g.State == State.COMPLETE) return;
						var drawn = Generic.Draw(c);
						if (drawn != null && drawn.Zone == c.HandZone) Generic.AddEnchantmentBlock(g, Cards.FromId(TreasureDiscount), me, drawn);
					}),
				},
			})));

			// ---------------- Hunter
			// TRLA_165 Battlestarved Lynx: "Battlecry: Draw Beasts until your hand is full." (random Beasts of the owner's deck).
			cards.Add("TRLA_165", Implemented(new CardDef(new Power
			{
				PowerTask = new CustomTask((g, c, s, t, stack) => DrawUntilFull(g, c, p => p.Card.IsRace(Race.BEAST))),
			})));
			// TRLA_166 Troll Harbinger: "Taunt. After a minion dies, add a random Hunter spell to your hand." Any minion, either side,
			// a run shrine going dormant included (a shrine death is a death everywhere but the resurrect pools); the spell comes
			// through the run's generation filter (RandomCardTask: GameConfig.ExcludedGeneratedIds). Its own death does not count.
			cards.Add("TRLA_166", Implemented(new CardDef(new Power
			{
				Trigger = new Trigger(TriggerType.DEATH)
				{
					TriggerSource = TriggerSource.ALL_MINIONS,
					SingleTask = ComplexTask.Create(
						new RandomCardTask(CardType.SPELL, CardClass.HUNTER),
						new AddStackTo(EntityType.HAND)),
				},
			})));
			// TRLA_167 Rabid Saurolisk: "Poisonous. Deathrattle: Deal 1 damage to all enemies." (Poisonous from the card data).
			cards.Add("TRLA_167", Implemented(new CardDef(new Power
			{
				DeathrattleTask = new DamageTask(1, EntityType.ENEMIES),
			})));
			// TRLA_168 Streetsmuggler: "After a friendly Secret is revealed, put a random Hunter Secret into the battlefield." Put, not
			// cast (as TRLA_110): a scripted Hunter Secret not already in play, from the Secrets this game lets the owner's generation
			// yield (DungeonRunContent.PlayRandomSecrets; a board-shrine run is a catalog run). Nothing when the Secret zone is full.
			cards.Add("TRLA_168", Implemented(new CardDef(new Power
			{
				Trigger = new Trigger(TriggerType.SECRET_REVEALED)
				{
					TriggerSource = TriggerSource.FRIENDLY,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (s is Minion me && Active(me) && g.State != State.COMPLETE) DungeonRunContent.PlayRandomSecrets(c, 1, CardClass.HUNTER);
					}),
				},
			})));
			// TRLA_169 Slamma Jamma: "Battlecry: Double the Attack of all your other minions." Hearthstone's doubling rule: each
			// other minion gains Attack equal to its current Attack, aura Attack included (Dire Wolf Alpha's +1 is doubled too), read
			// for all of them before any gain; TRLA_169e carries the gain (Attack_N with the script tag, as TRLA_116e), so it stays
			// when the aura leaves. A minion with 0 Attack gains nothing; the dormant forms are untouchable, so not among them.
			cards.Add("TRLA_169", Implemented(new CardDef(new Power
			{
				PowerTask = new CustomTask((g, c, s, t, stack) =>
				{
					var doubled = IncludeTask.GetEntities(EntityType.MINIONS_NOSOURCE, c, s, t, null)
						.OfType<Minion>().Where(m => !m.Untouchable).Select(m => (m, m.AttackDamage)).ToList();
					var gain = Cards.FromId("TRLA_169e");
					foreach (var (m, attack) in doubled)
						if (attack > 0 && m.Zone == c.BoardZone && g.State != State.COMPLETE) Generic.AddEnchantmentBlock(g, gain, (IPlayable)s, m, attack);
				}),
			})));
			cards.Add("TRLA_169e", Implemented(new CardDef(new Power { Enchant = new Enchant(Effects.Attack_N(0)) { UseScriptTag = true } })));

			// ---------------- Mage
			// TRLA_130 Razzle Dazzler: "Taunt. Battlecry: Fill your hand with Arcane Missiles." (EX1_277 until the hand is full).
			cards.Add("TRLA_130", Implemented(new CardDef(new Power
			{
				PowerTask = new CustomTask((g, c, s, t, stack) =>
				{
					var missiles = Cards.FromId(DazzlerCard);
					for (int i = 0; i < 10 && !c.HandZone.IsFull && g.State != State.COMPLETE; i++)
						Generic.AddHandPhase.Invoke(c, Entity.FromCard(c, missiles, new EntityData
						{
							[GameTag.CREATOR] = s.Id, [GameTag.DISPLAYED_CREATOR] = s.Id,
						}));
				}),
			})));
			// TRLA_131 Glyph Guardian: "Whenever a character is Frozen, gain Spell Damage +1." (any character, any freezer; each
			// freeze adds TRLA_131e "Increased Spell Damage.", which Silence removes).
			cards.Add("TRLA_131", Implemented(new CardDef(new Power
			{
				Trigger = new Trigger(TriggerType.FROZEN)
				{
					TriggerSource = TriggerSource.ALL,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (s is Minion me && Active(me)) Generic.AddEnchantmentBlock(g, Cards.FromId("TRLA_131e"), me, me);
					}),
				},
			})));
			cards.Add("TRLA_131e", Implemented(new CardDef(new Power { Enchant = new Enchant(new Effect(GameTag.SPELLPOWER, EffectOperator.ADD, 1)) })));
			// TRLA_132 Frostweaver: "Your Hero Power also Freezes the target. If the target is a Frozen minion, destroy it." When the
			// owner's hero power is aimed (its target event, before it resolves): a minion that is already Frozen is destroyed, any
			// other target is Frozen by the owner (so TRLA_129's "whenever you Freeze" and TRLA_131 see it). The destroy is the hero
			// power's: the hit then lands on a doomed minion, so TRLA_128 Jan'alai's Flame counts it as a kill and refreshes.
			cards.Add("TRLA_132", Implemented(new CardDef(new Power
			{
				Trigger = new Trigger(TriggerType.TARGET)
				{
					TriggerSource = TriggerSource.HERO_POWER,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (!(t is HeroPower power) || power.Controller != c || !(s is Minion me) || !Active(me) || g.State == State.COMPLETE) return;
						if (!(g.CurrentEventData?.EventTarget is ICharacter target) || target is Minion inPlay && inPlay.Zone?.Type != Zone.PLAY) return;
						if (target is Minion frozen && frozen.IsFrozen)
						{
							if (!frozen.ToBeDestroyed) g.TaskQueue.Execute(new DestroyTask(EntityType.TARGET), c, power, frozen);
						}
						else g.TaskQueue.Execute(new SetGameTagTask(GameTag.FROZEN, 1, EntityType.TARGET), c, power, target);
					}),
				},
			})));
			// TRLA_133 Fireslinger: "After you cast a spell, cast a Fireball at a random enemy." A Fireball (CS2_029) cast for the owner
			// at a random enemy character it may target (game RNG); the owner's Spell Damage applies. An effect's cast raises no
			// "after you cast" event (AuraSpellRules: hand casts only), so a Fireball never answers itself.
			cards.Add("TRLA_133", Implemented(new CardDef(new Power
			{
				Trigger = new Trigger(TriggerType.AFTER_CAST)
				{
					TriggerSource = TriggerSource.FRIENDLY,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (s is Minion me && Active(me) && g.State != State.COMPLETE) CastAtRandomEnemy(g, c, me, FireslingerSpell);
					}),
				},
			})));
			// TRLA_134 Showstopping Conjurer: "Rush. Overkill: Draw a spell from your deck." (a random spell of the owner's deck).
			cards.Add("TRLA_134", Implemented(new CardDef(new Power
			{
				OverkillTask = new CustomTask((g, c, s, t, stack) => DrawRandom(g, c, p => p.Card.Type == CardType.SPELL)),
			})));

			// ---------------- Paladin
			// TRLA_140 Blessed One: "Battlecry: Draw spells until your hand is full." (random spells of the owner's deck).
			cards.Add("TRLA_140", Implemented(new CardDef(new Power
			{
				PowerTask = new CustomTask((g, c, s, t, stack) => DrawUntilFull(g, c, p => p.Card.Type == CardType.SPELL)),
			})));
			// TRLA_141 Lightchucker: "After a character is healed, destroy a random enemy minion." (any healed character, either side;
			// the owner's enemy minions in play, the untouchable dormant forms excluded; game RNG).
			cards.Add("TRLA_141", Implemented(new CardDef(new Power
			{
				Trigger = new Trigger(TriggerType.HEAL)
				{
					TriggerSource = TriggerSource.ALL,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (!(s is Minion me) || !Active(me) || g.State == State.COMPLETE) return;
						var enemies = c.Opponent.BoardZone.GetAll(m => !m.ToBeDestroyed);
						if (enemies.Length == 0) return;
						if (enemies.Length > 1) g.OnRandomHappened(true);
						g.TaskQueue.Execute(new DestroyTask(EntityType.TARGET), c, me, enemies[g.Random.Next(enemies.Length)]);
					}),
				},
			})));
			// TRLA_142 The Walking Fort: "Taunt. This minion can only take 1 damage at a time." Each hit on it is capped at 1 before it
			// resolves (a PREDAMAGE clamp on itself, as Selfless Protector's); Silence ends it.
			cards.Add("TRLA_142", Implemented(new CardDef(new Power
			{
				Trigger = new Trigger(TriggerType.PREDAMAGE)
				{
					TriggerSource = TriggerSource.SELF,
					Condition = new Conditions.SelfCondition(p => p.Game.CurrentEventData.EventNumber > WalkingFortCap),
					FastExecution = true,
					SingleTask = new CustomTask((g, c, s, t, stack) => { g.CurrentEventData.EventNumber = WalkingFortCap; }),
				},
			})));
			// TRLA_143 Gloryseeker: "Whenever this minion takes damage, your hero takes it instead." Each hit on it goes to its
			// owner's hero instead (a PREDAMAGE redirect, as Bolf Ramshield's AT_124 the other way): the hero's Armor absorbs first,
			// and the minion takes nothing (so a Poisonous hit does not destroy it). A Divine Shield pops before any redirect.
			cards.Add("TRLA_143", Implemented(new CardDef(new Power
			{
				Trigger = new Trigger(TriggerType.PREDAMAGE)
				{
					TriggerSource = TriggerSource.SELF,
					Condition = new Conditions.SelfCondition(p => p.Game.CurrentEventData.EventNumber > 0),
					FastExecution = true,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						var data = g.CurrentEventData;
						int amount = data.EventNumber;
						data.EventNumber = 0;
						if (data.EventSource is IPlayable source && c.Hero != null) c.Hero.TakeDamage(source, amount, combat: false, outgoingApplied: true);
					}),
				},
			})));
			// TRLA_144 Exactor of Justice: "Divine Shield. Overkill: Gain Divine Shield." (Divine Shield from the card data). Combat
			// damage is simultaneous, so the Overkill gives the shield after the defender's damage back (which pops the old one):
			// the engine resolves the attacker's hit, Overkill included, before the defender's, so the Overkill only marks the
			// Exactor (ExactorShield) and its after-attack trigger gives the shield once both hits have landed.
			cards.Add("TRLA_144", Implemented(new CardDef(new Power
			{
				OverkillTask = new CustomTask((g, c, s, t, stack) => { if (s is Minion me) me.NativeTags[ExactorShield] = 1; }),
				Trigger = new Trigger(TriggerType.AFTER_ATTACK)
				{
					TriggerSource = TriggerSource.SELF,
					SingleTask = new CustomTask((g, c, s, t, stack) =>
					{
						if (!(s is Minion me) || !me.NativeTags.TryGetValue(ExactorShield, out int marked)) return;
						me.NativeTags.Remove(ExactorShield);
						if (marked != 0 && me.Zone == c.BoardZone) g.TaskQueue.Execute(new SetGameTagTask(GameTag.DIVINE_SHIELD, 1, EntityType.SOURCE), c, me, me);
					}),
				},
			})));
		}

		/// <summary>
		/// On a TRLA_144 Exactor of Justice whose attack overkilled: the after-attack trigger gives it Divine Shield (a private
		/// working tag, ShrineRules.IsPrivateTag: never sent in a native packet).
		/// </summary>
		public const GameTag ExactorShield = (GameTag)32625;

		/// <summary>TRLA_142 The Walking Fort's damage cap per hit.</summary>
		public const int WalkingFortCap = 1;

		/// <summary>
		/// TRLA_122 Direhorn Stomper's Overkill: summons the minion of <paramref name="c"/>'s deck with the highest Attack (ties: game
		/// RNG). Returns it, or null when the board is full or the deck holds no minion.
		/// </summary>
		internal static Minion SummonHighestAttack(Game g, Controller c, IPlayable source)
		{
			if (c.BoardZone.IsFull || g.State == State.COMPLETE) return null;
			var minions = c.DeckZone.OfType<Minion>().ToList();
			if (minions.Count == 0) return null;
			int best = minions.Max(m => m.AttackDamage);
			var top = minions.Where(m => m.AttackDamage == best).ToList();
			if (top.Count > 1) g.OnRandomHappened(true);
			var pick = top[g.Random.Next(top.Count)];
			Generic.RemoveFromZone.Invoke(c, pick);
			Generic.SummonBlock.Invoke(g, pick, -1, source);
			return pick;
		}

		/// <summary>Draws a random card of <paramref name="c"/>'s deck that matches (game RNG); null when none does.</summary>
		internal static IPlayable DrawRandom(Game g, Controller c, Func<IPlayable, bool> match)
		{
			if (g.State == State.COMPLETE) return null;
			var pool = c.DeckZone.Where(match).ToList();
			if (pool.Count == 0) return null;
			if (pool.Count > 1) g.OnRandomHappened(true);
			return Generic.Draw(c, pool[g.Random.Next(pool.Count)]);
		}

		/// <summary>Draws random matching cards of <paramref name="c"/>'s deck until the hand is full or none is left.</summary>
		internal static int DrawUntilFull(Game g, Controller c, Func<IPlayable, bool> match)
		{
			int drawn = 0;
			for (int i = 0; i < 10 && !c.HandZone.IsFull && g.State != State.COMPLETE; i++)
			{
				if (DrawRandom(g, c, match) == null) break;
				drawn++;
			}
			return drawn;
		}

		/// <summary>
		/// Casts a new <paramref name="spellId"/> for <paramref name="c"/> at a random enemy character it may target (game RNG), as
		/// Fyrakk's casts do; nothing when it has no enemy target. Returns the spell cast, or null.
		/// </summary>
		internal static Spell CastAtRandomEnemy(Game g, Controller c, IPlayable source, string spellId)
		{
			var spell = (Spell)Entity.FromCard(c, Cards.FromId(spellId), new EntityData
			{
				[GameTag.CREATOR] = source.Id, [GameTag.DISPLAYED_CREATOR] = source.Id,
			});
			var enemies = spell.ValidPlayTargets.Where(e => e.Controller != c && spell.IsValidPlayTarget(e)).ToArray();
			if (enemies.Length == 0) return null;
			if (enemies.Length > 1) g.OnRandomHappened(true);
			ICharacter target = enemies[g.Random.Next(enemies.Length)];
			spell.CardTarget = target.Id;
			Generic.CastAutomaticSpell(c, g, spell, target, 0);
			while (c.Choice != null && g.State != State.COMPLETE) Generic.ChoicePick(c, g, c.Choice.Choices.Choose(g.Random));
			return spell;
		}
	}
}
