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
	/// <summary>What a piece of Dungeon Run content is, which decides how the run layer uses it.</summary>
	public enum RunContentKind
	{
		/// <summary>A clickable hero power for a player hero or a boss.</summary>
		HeroPower,
		/// <summary>An unclickable hero power that applies its effect once at game start (shrines, boss passives).</summary>
		PassivePower,
		/// <summary>An enchantment the run layer or a power attaches (treasure triggers, buffs).</summary>
		Effect,
		/// <summary>A run-long passive treasure. It has no shell of its own; it may name an <see cref="Effect"/>.</summary>
		PassiveTreasure,
		/// <summary>A playable card treasure (spell or minion) added to the run deck.</summary>
		ActiveTreasure,
		/// <summary>
		/// A named adventure hero's portrait (phase 6): the real client hero card, scripted with no behaviour of its own so a
		/// run may play it, at its run <see cref="RunContent.Health"/>. Its powers, decks and treasures are other entries.
		/// </summary>
		Hero,
	}

	/// <summary>
	/// One piece of original Dungeon Run content (docs/design/dungeon-run.md § Content). The client shows the
	/// <see cref="ShellId"/> entity's art; SoloStone shows <see cref="Name"/> and <see cref="Text"/> over it during a run.
	/// Metadata only: the script is built by <see cref="DungeonRunCardsGen"/> while the card database loads.
	/// </summary>
	public sealed class RunContent
	{
		public string Key { get; internal set; }
		public RunContentKind Kind { get; internal set; }
		/// <summary>Existing, unscripted client entity this content borrows; null for a shell-less passive treasure.</summary>
		public string ShellId { get; internal set; }
		public string Name { get; internal set; }
		public string Text { get; internal set; }
		public int Cost { get; internal set; }
		/// <summary>Passive treasure: extra starting health.</summary>
		public int HeroHealthBonus { get; internal set; }
		/// <summary>Hero portrait: the printed Health the shell is given (every named run hero plays at 30).</summary>
		public int Health { get; internal set; }
		/// <summary>Passive treasure: extra opening-hand cards.</summary>
		public int OpeningHandBonus { get; internal set; }
		/// <summary>
		/// Passive treasure (LOOTA_800 Potion of Vitality, "Double your starting Health"): its Health bonus is the run hero's base
		/// Health (the row's run Health, else the portrait's), so the hero starts at twice that plus every other bonus.
		/// </summary>
		public bool DoublesHeroHealth { get; internal set; }
		/// <summary>
		/// Passive treasure (LOOTA_802 Justicar's Ring, "Your Hero Power is upgraded"): the run plan plays the upgraded form of
		/// the run's power (<see cref="DungeonRunContent.HeroPowerUpgrades"/>, applied by <see cref="DungeonRunSetup.PlanPower"/>).
		/// </summary>
		public bool UpgradesHeroPower { get; internal set; }
		/// <summary>Passive treasure or passive power: key of the <see cref="RunContentKind.Effect"/> attached to the player.</summary>
		public string EffectKey { get; internal set; }
		/// <summary>Passive treasure: a one-off change applied after the game starts, before the mulligan.</summary>
		internal Action<Controller> OnMatchStart { get; set; }
		/// <summary>Hero power: minion cards it can summon, so the run layer can check the client knows them.</summary>
		public IReadOnlyList<string> Tokens { get; internal set; } = new string[0];
		internal Func<CardDef> Script { get; set; }
		internal Action<Card> Shape { get; set; }
	}

	/// <summary>The original Dungeon Run content set: powers, shrines, boss passives, treasures and their effects.</summary>
	public static partial class DungeonRunContent
	{
		private static readonly List<RunContent> Entries = new List<RunContent>();
		private static readonly Dictionary<string, RunContent> ByKeyMap;

		/// <summary>Every content entry, in declaration order.</summary>
		public static IReadOnlyList<RunContent> All => Entries;

		/// <summary>Content by key; throws on an unknown key.</summary>
		public static RunContent Get(string key) =>
			TryGet(key, out var value) ? value : throw new KeyNotFoundException("Dungeon Run content: " + key);

		public static bool TryGet(string key, out RunContent content)
		{
			// While the static constructor runs, the map is not built yet: search the list.
			if (ByKeyMap != null) return ByKeyMap.TryGetValue(key, out content);
			content = Entries.FirstOrDefault(e => e.Key == key);
			return content != null;
		}

		/// <summary>Content whose shell is this entity id, for the client name and text override.</summary>
		public static RunContent ForShell(string shellId) => Entries.FirstOrDefault(e => e.ShellId == shellId);

		// ---------------------------------------------------------------- builders

		private static RunContent Add(RunContent content)
		{
			if (Entries.Any(e => e.Key == content.Key)) throw new InvalidOperationException("Duplicate content key " + content.Key);
			if (content.ShellId != null && Entries.Any(e => e.ShellId == content.ShellId))
				throw new InvalidOperationException("Shell used twice: " + content.ShellId);
			Entries.Add(content);
			return content;
		}

		private static Dictionary<PlayReq, int> Reqs(params PlayReq[] reqs) => reqs.ToDictionary(r => r, r => 0);

		private static readonly PlayReq[] AnyTarget = { PlayReq.REQ_TARGET_TO_PLAY };
		private static readonly PlayReq[] EnemyMinion = { PlayReq.REQ_TARGET_TO_PLAY, PlayReq.REQ_MINION_TARGET, PlayReq.REQ_ENEMY_TARGET };
		private static readonly PlayReq[] FriendlyMinion = { PlayReq.REQ_TARGET_TO_PLAY, PlayReq.REQ_MINION_TARGET, PlayReq.REQ_FRIENDLY_TARGET };
		private static readonly PlayReq[] AnyMinion = { PlayReq.REQ_TARGET_TO_PLAY, PlayReq.REQ_MINION_TARGET };
		// A summoning power needs a free board slot; the engine checks BoardZone.IsFull for this requirement, not its value.
		private static readonly PlayReq[] Summon = { PlayReq.REQ_NUM_MINION_SLOTS };

		/// <summary>A clickable hero power; <paramref name="tokens"/> lists the minion cards it summons.</summary>
		private static RunContent Power(string key, string shell, string name, string text, int cost, PlayReq[] reqs, Func<ISimpleTask> task,
			params string[] tokens) =>
			Add(new RunContent
			{
				Key = key, Kind = RunContentKind.HeroPower, ShellId = shell, Name = name, Text = text, Cost = cost, Tokens = tokens,
				Script = () => reqs == null || reqs.Length == 0
					? new CardDef(new Power { PowerTask = task() })
					: new CardDef(Reqs(reqs), new Power { PowerTask = task() }),
				Shape = card => card.SetPrintedCost(cost),
			});

		/// <summary>
		/// Drops tags a borrowed shell carries from its original use (a Tavern Brawl power's DISCOVER, DEATHRATTLE or
		/// AI_MUST_PLAY), so the engine entity, and what the client is sent for it, describe only the run power.
		/// </summary>
		private static void Without(RunContent content, params GameTag[] tags)
		{
			Action<Card> shape = content.Shape;
			content.Shape = card =>
			{
				shape?.Invoke(card);
				foreach (GameTag tag in tags) card.Tags.Remove(tag);
			};
		}

		/// <summary>An unclickable power: at game start it attaches <paramref name="effectKey"/> to its owner.</summary>
		private static void Passive(string key, string shell, string name, string text, string effectKey) =>
			Add(new RunContent
			{
				Key = key, Kind = RunContentKind.PassivePower, ShellId = shell, Name = name, Text = text, EffectKey = effectKey,
				Script = () => new CardDef(new Power
				{
					PowerTask = new CustomTask((game, controller, source, target, stack) =>
						DungeonRunSetup.AttachEffect(controller, (IPlayable)source, effectKey))
				}),
				Shape = card => card.SetPrintedCost(0),
			});

		/// <summary>
		/// An enchantment shell. Some borrowed shells carry TAG_ONE_TURN_EFFECT from their original use; the engine would then
		/// drop the enchantment entity at end of turn while its stats stay. Shape clears it unless the effect is meant to last
		/// one turn (<paramref name="oneTurn"/>), so the shown buff always matches what it does.
		/// </summary>
		private static void Effect(string key, string shell, string name, string text, Func<Power> power, bool oneTurn = false) =>
			Add(new RunContent
			{
				Key = key, Kind = RunContentKind.Effect, ShellId = shell, Name = name, Text = text,
				Script = () => new CardDef(power()),
				Shape = card =>
				{
					if (oneTurn) card.Tags[GameTag.TAG_ONE_TURN_EFFECT] = 1;
					else card.Tags.Remove(GameTag.TAG_ONE_TURN_EFFECT);
				},
			});

		private static void Buff(string key, string shell, string name, string text, params IEffect[] effects) =>
			Effect(key, shell, name, text, () => new Power { Enchant = new Enchant(effects) });

		private static Trigger Trig(TriggerType type, ISimpleTask task, TriggerSource source = TriggerSource.FRIENDLY,
			SelfCondition condition = null) =>
			new Trigger(type) { TriggerSource = source, SingleTask = task, Condition = condition };

		/// <summary>
		/// A passive treasure. <paramref name="ownShell"/> (phase 6: a real adventure passive keyed by its client id, LOOTA_800,
		/// DALA_735, ULDA_102, ...) makes the key its own display shell: the client shows the real card (a SPELL) in the
		/// treasure picker and the run's name override shows <paramref name="name"/> and <paramref name="text"/>, which must be the
		/// client's own (facts/violet-hold-2026-09.json). The shell gets an empty script so the validator sees it as scripted; it
		/// never enters a deck. Its aura is still an <see cref="RunContentKind.Effect"/> entry (<paramref name="effect"/>).
		/// </summary>
		private static void Treasure(string key, string name, string text, int health = 0, int opening = 0,
			string effect = null, Action<Controller> onStart = null, bool ownShell = false, bool doubleHealth = false, bool upgradePower = false) =>
			Add(new RunContent
			{
				Key = key, Kind = RunContentKind.PassiveTreasure, Name = name, Text = text,
				HeroHealthBonus = health, OpeningHandBonus = opening, EffectKey = effect, OnMatchStart = onStart,
				DoublesHeroHealth = doubleHealth, UpgradesHeroPower = upgradePower,
				ShellId = ownShell ? key : null,
				Script = ownShell ? () => new CardDef() : (Func<CardDef>)null,
			});

		/// <summary>
		/// Health every named adventure hero plays at: the portraits print 15 or 20 (adventure values that assume the original
		/// run rules); a SoloStone run hero starts at a class hero's 30 (an adaptation recorded in the restoration ledger).
		/// </summary>
		public const int NamedHeroHealth = 30;

		/// <summary>
		/// A named adventure hero portrait (<see cref="RunContentKind.Hero"/>): keyed and shelled by its own client id, so the
		/// client shows its real art; <paramref name="name"/> and <paramref name="text"/> are the client's own, word for word
		/// (facts/violet-hold-2026-09.json), so the run's name override shows exactly what the client would.
		/// </summary>
		private static void Hero(string id, string name, string text, int health = NamedHeroHealth) =>
			Add(new RunContent
			{
				Key = id, Kind = RunContentKind.Hero, ShellId = id, Name = name, Text = text, Health = health,
				Script = () => new CardDef(),
				Shape = card => card.SetPrintedHealth(health),
			});

		private static void Spell(string key, string shell, string name, string text, int cost, PlayReq[] reqs, Func<ISimpleTask> task) =>
			Add(new RunContent
			{
				Key = key, Kind = RunContentKind.ActiveTreasure, ShellId = shell, Name = name, Text = text, Cost = cost,
				Script = () => reqs == null || reqs.Length == 0
					? new CardDef(new Power { PowerTask = task() })
					: new CardDef(Reqs(reqs), new Power { PowerTask = task() }),
				Shape = card => card.SetPrintedCost(cost),
			});

		private static void Minion(string key, string shell, string name, string text, int cost, int attack, int health,
			bool taunt = false, bool rush = false, Func<ISimpleTask> battlecry = null) =>
			Add(new RunContent
			{
				Key = key, Kind = RunContentKind.ActiveTreasure, ShellId = shell, Name = name, Text = text, Cost = cost,
				Script = () => new CardDef(new Power { PowerTask = battlecry?.Invoke() }),
				Shape = card =>
				{
					card.SetPrintedCost(cost);
					card.SetPrintedStats(attack, health);
					card.SetPrintedTaunt(taunt);
					card.SetPrintedRush(rush);
					if (battlecry != null) card.Tags[GameTag.BATTLECRY] = 1;
				},
			});

		// ---------------------------------------------------------------- shared task shapes

		private static ISimpleTask BuffRandomFriendly(string buffKey) =>
			ComplexTask.BuffRandomMinion(EntityType.MINIONS, Get(buffKey).ShellId);

		private static ISimpleTask BuffAllFriendly(string buffKey) =>
			new AddEnchantmentTask(Get(buffKey).ShellId, EntityType.MINIONS);

		private static ISimpleTask DamageRandomEnemyMinion(int amount) => ComplexTask.Create(
			new IncludeTask(EntityType.OP_MINIONS), new RandomTask(1, EntityType.STACK), new DamageTask(amount, EntityType.STACK));

		private static ISimpleTask DamageRandomEnemy(int amount) => ComplexTask.Create(
			new IncludeTask(EntityType.ENEMIES), new RandomTask(1, EntityType.STACK), new DamageTask(amount, EntityType.STACK));

		private static ISimpleTask Custom(Action<Controller> action) =>
			new CustomTask((game, controller, source, target, stack) => action(controller));

		// ---------------------------------------------------------------- content

		static DungeonRunContent()
		{
			// Buff and cost enchantments used by the powers and treasures below.
			Buff("buff_1_1", "TB_CoOpv3_009e", "Blessed", "+1/+1.", Effects.AttackHealth_N(1));
			Buff("buff_2_2", "TB_CoOpv3_101e", "Overclocked", "+2/+2.", Effects.AttackHealth_N(2));
			Buff("buff_atk_1", "TB_CoOpv3_104e", "Honed", "+1 Attack.", Effects.Attack_N(1));
			Buff("buff_hp_1", "TB_CoOpv3_BOSS2e", "Fortified", "+1 Health.", Effects.Health_N(1));
			Buff("buff_hp_2", "TB_CoOpv3_BOSS3e", "Braced", "+2 Health.", Effects.Health_N(2));
			Effect("buff_atk_1_turn", "TB_CoOpv3_BOSS4e", "Rallied", "+1 Attack this turn.",
				() => new Power { Enchant = new Enchant(Effects.Attack_N(1)) { IsOneTurnEffect = true } }, oneTurn: true);
			Buff("cost_minus_1", "TB_CoOpv3_BOSSe", "Mapped", "Costs (1) less.", Effects.ReduceCost(1));

			// ---------------- Player hero powers
			Power("trapper_snare", "TB_CoopHero_HP_001", "Set Snare",
				"<b>Hero Power</b>\nFreeze an enemy minion. If it was already <b>Frozen</b>, deal 2 damage to it.", 2, EnemyMinion,
				() => ComplexTask.Create(
					new ConditionTask(EntityType.TARGET, SelfCondition.IsFrozen),
					new FlagTask(true, new DamageTask(2, EntityType.TARGET)),
					new FlagTask(false, ComplexTask.Freeze(EntityType.TARGET))));
			Power("sellsword_shield_wall", "TB_LevelUp_001", "Shield Wall",
				"<b>Hero Power</b>\nGive a friendly minion +2 Health and <b>Taunt</b>.", 2, FriendlyMinion,
				() => ComplexTask.Create(new AddEnchantmentTask("TB_CoOpv3_BOSS3e", EntityType.TARGET), ComplexTask.Taunt(EntityType.TARGET)));
			Power("sellsword_war_cry", "TB_John_002p", "War Cry",
				"<b>Hero Power</b>\nGive your minions +1 Attack this turn.", 2, null,
				() => new AddEnchantmentTask("TB_CoOpv3_BOSS4e", EntityType.MINIONS));
			Power("carto_survey", "TB_BuildaBoss_404p", "Survey",
				"<b>Hero Power</b>\n<b>Discover</b> a spell.", 2, null, () => new DiscoverTask(DiscoverType.SPELL));
			Power("carto_shortcut", "TB_CheaterChess_Black", "Shortcut",
				"<b>Hero Power</b>\nDraw a card.", 3, null, () => new DrawTask());
			Power("carto_ambush", "TB_BountyHunt_Hypnotize", "Ambush Point",
				"<b>Hero Power</b>\nDeal 1 damage to a minion and 1 damage to the enemy hero.",
				2, AnyMinion,
				() => ComplexTask.Create(new DamageTask(1, EntityType.TARGET), new DamageTask(1, EntityType.OP_HERO)));

			// Class powers (docs/design/dungeon-run.md § Classes, decks and powers): power 2 unlocks at floor 4 with the class,
			// power 3 with a win. Built only from engine tasks (no CustomTask), so they clone and replay deterministically.
			Power("druid_rootcall", "TB_Henchmania_ChuH", "Rootcall",
				"<b>Hero Power</b>\nSummon a 1/1 Sapling.", 2, Summon, () => new SummonTask("AT_037t", SummonSide.DEFAULT), "AT_037t");
			Power("druid_thornhide", "TB_Chupacabran_HP", "Thornhide",
				"<b>Hero Power</b>\nGain 1 Armor. Deal 1 damage to a random enemy.", 2, null,
				() => ComplexTask.Create(new ArmorTask(1), DamageRandomEnemy(1)));
			Power("hunter_pack_whistle", "TB_RoadToNR_Finley_HP", "Pack Whistle",
				"<b>Hero Power</b>\nSummon a 1/1 Hound with <b>Charge</b>.", 2, Summon, () => new SummonTask("EX1_538t", SummonSide.DEFAULT), "EX1_538t");
			Power("mage_rimebite", "TB_Firefest2_Ahune_HP", "Rimebite",
				"<b>Hero Power</b>\nDeal 1 damage to a minion and <b>Freeze</b> it.", 2, AnyMinion,
				() => ComplexTask.Create(new DamageTask(1, EntityType.TARGET), ComplexTask.Freeze(EntityType.TARGET)));
			Power("paladin_vow_of_light", "TB_RoadToNR_RenoHP", "Vow of Light",
				"<b>Hero Power</b>\nGive a friendly minion <b>Divine Shield</b>.", 2, FriendlyMinion, () => ComplexTask.DivineShield(EntityType.TARGET));
			Power("paladin_lantern_march", "TB_TempleOutrun_Lazul_HP3", "Lantern March",
				"<b>Hero Power</b>\nRestore 2 Health to all friendly characters.", 2, null, () => new HealTask(2, EntityType.FRIENDS));
			Power("priest_ward_of_faith", "TB_Henchmania_MyraH", "Ward of Faith",
				"<b>Hero Power</b>\nGive a friendly minion +2 Health.", 2, FriendlyMinion,
				() => new AddEnchantmentTask(Get("buff_hp_2").ShellId, EntityType.TARGET));
			Power("priest_penitent_lash", "TB_RoadToNR_MurgathaHP", "Penitent Lash",
				"<b>Hero Power</b>\nDeal 1 damage to an enemy minion. Restore 2 Health to your hero.", 2, EnemyMinion,
				() => ComplexTask.Create(new DamageTask(1, EntityType.TARGET), new HealTask(2, EntityType.HERO)));
			Power("shaman_spirit_kindling", "TB_TempleOutrun_Toggwaggle_HP", "Spirit Kindling",
				"<b>Hero Power</b>\nGive a random friendly minion +1/+1.", 2, null, () => BuffRandomFriendly("buff_1_1"));
			Power("shaman_spark_lash", "TB_EVILBRM_BoomHeroPower", "Spark Lash",
				"<b>Hero Power</b>\nDeal 2 damage to a random enemy minion.", 2, null, () => DamageRandomEnemyMinion(2));
			Power("warlock_fiend_gate", "TB_EVILBRM_RafaamHeroPower", "Fiend Gate",
				"<b>Hero Power</b>\nSummon a 1/1 Imp.", 2, Summon, () => new SummonTask("EX1_598", SummonSide.DEFAULT), "EX1_598");
			Power("warlock_sanguine_pact", "TB_FactionWar_Boss_Rag_0", "Sanguine Pact",
				"<b>Hero Power</b>\nDeal 2 damage to your hero. Give a friendly minion +2/+2.", 2, FriendlyMinion,
				() => ComplexTask.Create(new DamageTask(2, EntityType.HERO), new AddEnchantmentTask(Get("buff_2_2").ShellId, EntityType.TARGET)));

			// Death Knight and Demon Hunter (the class tray offers all eleven classes). Three are the last spare Tavern Brawl
			// shells; Bone Minions (BRMA17_5) is an unscripted Blackrock Mountain adventure power.
			Power("deathknight_grave_call", "BRMA17_5", "Grave Call",
				"<b>Hero Power</b>\nSummon a 1/1 Ant Husk.", 2, Summon, () => new SummonTask("EDR_813at", SummonSide.DEFAULT), "EDR_813at");
			Without(Power("deathknight_rime_wind", "TB_EVILBRM_OnyxiaHeroPower", "Rime Wind",
				"<b>Hero Power</b>\nDeal 1 damage to all enemy minions.", 3, null, () => new DamageTask(1, EntityType.OP_MINIONS)),
				GameTag.AI_MUST_PLAY);
			Without(Power("demonhunter_glaivewhirl", "TB_EVILBRM_RafaamHeroPower2", "Glaivewhirl",
				"<b>Hero Power</b>\n+1 Attack this turn. Draw a card.", 2, null,
				() => ComplexTask.Create(new AddEnchantmentTask("HERO_10bpe", EntityType.HERO), new DrawTask())),
				GameTag.DISCOVER);
			Without(Power("demonhunter_felbat_call", "TB_TempleOutrun_Lazul_HP", "Felbat Call",
				"<b>Hero Power</b>\nSummon a 1/1 Felbat with <b>Rush</b>.", 2, Summon, () => new SummonTask("TTN_843t1", SummonSide.DEFAULT), "TTN_843t1"),
				GameTag.DEATHRATTLE);

			// ---------------- Boss hero powers
			Power("boss_stomp", "TB_BossBattle_KoboldHP", "Stomp",
				"<b>Hero Power</b>\nDeal 1 damage to all enemy minions.", 2, null, () => new DamageTask(1, EntityType.OP_MINIONS));
			Power("boss_chain_spark", "TB_BossRumble_001hp", "Chain Spark",
				"<b>Hero Power</b>\nDeal 1 damage to two random enemies.", 2, null,
				() => ComplexTask.Create(new IncludeTask(EntityType.ENEMIES), new RandomTask(2, EntityType.STACK), new DamageTask(1, EntityType.STACK)));
			Power("boss_chill", "TB_BossRumble_002hp", "Chill",
				"<b>Hero Power</b>\n<b>Freeze</b> an enemy minion.", 2, EnemyMinion, () => ComplexTask.Freeze(EntityType.TARGET));
			Power("boss_overclock", "TB_MechWar_Boss1_HeroPower", "Overclock",
				"<b>Hero Power</b>\nGive a friendly minion +2/+2.", 2, FriendlyMinion,
				() => new AddEnchantmentTask("TB_CoOpv3_101e", EntityType.TARGET));
			Power("boss_cinder_rain", "TB_BossRumble_003hp", "Cinder Rain",
				"<b>Hero Power</b>\nDeal 2 damage to a random enemy.", 2, null, () => DamageRandomEnemy(2));

			// ---------------- Shrines (three per shrine, one per band). Effects first, then the passive powers that attach them.
			for (int tier = 1; tier <= 3; tier++)
			{
				int t = tier;
				Effect($"fx_dawn_{t}", new[] { "TB_DALA_Minion1e", "TB_DALA_Minion3e", "TB_Carousel_EnchA" }[t - 1], $"Shrine of Dawn {Roman(t)}",
					t < 3 ? $"At the start of your turn, give a random friendly minion +{t}/+{t}." : "At the start of your turn, give your minions +1/+1.",
					() => new Power { Trigger = Trig(TriggerType.TURN_START, t == 1 ? BuffRandomFriendly("buff_1_1") : t == 2 ? BuffRandomFriendly("buff_2_2") : BuffAllFriendly("buff_1_1")) });
				Effect($"fx_bulwark_{t}", new[] { "TB_Carousel_EnchB", "TB_BoomAnnoy_001e", "TB_BoomBotFestival_001e" }[t - 1], $"Shrine of the Bulwark {Roman(t)}",
					$"At the end of your turn, gain {2 * t} Armor.",
					() => new Power { Trigger = Trig(TriggerType.TURN_END, new ArmorTask(2 * t)) });
				Effect($"fx_embers_{t}", new[] { "TB_Blizzcon2016_GoonsEnchant", "TB_Blizzcon2016_KabalEnchant", "TB_Blizzcon2016_LotusEnchant" }[t - 1], $"Shrine of Embers {Roman(t)}",
					$"After you play a minion, deal {t} damage to a random enemy.",
					() => new Power { Trigger = Trig(TriggerType.AFTER_PLAY_MINION, DamageRandomEnemy(t)) });
			}
			string[] dawn = { "TB_SPT_BossHeroPower", "TB_SPT_DALA_BossHeroPower", "TB_SPT_MTH_BossHeroPower" };
			string[] bulwark = { "TB_SPT_DPromoHP", "TB_SPT_DPromoHP2", "TB_MammothParty_hp001" };
			string[] embers = { "TB_MammothParty_hp002", "TB_RoadToNR_DinoHP", "TB_RoadToNR_OrgrimmarHP" };
			for (int t = 1; t <= 3; t++)
			{
				Passive($"shrine_dawn_{t}", dawn[t - 1], $"Shrine of Dawn {Roman(t)}", "<b>Passive</b>\n" + Get($"fx_dawn_{t}").Text, $"fx_dawn_{t}");
				Passive($"shrine_bulwark_{t}", bulwark[t - 1], $"Shrine of the Bulwark {Roman(t)}", "<b>Passive</b>\n" + Get($"fx_bulwark_{t}").Text, $"fx_bulwark_{t}");
				Passive($"shrine_embers_{t}", embers[t - 1], $"Shrine of Embers {Roman(t)}", "<b>Passive</b>\n" + Get($"fx_embers_{t}").Text, $"fx_embers_{t}");
			}

			// ---------------- Boss passives (effect + passive power)
			BossPassive("pilfer", "TB_HeadlessHorseman_HP1", "TB_ArchivistEnch", "Pilfer",
				"At the end of your turn, add a Coin to your hand.",
				() => new Power { Trigger = Trig(TriggerType.TURN_END, new AddCardTo("GAME_005", EntityType.HAND)) });
			BossPassive("overgrowth", "TB_HeadlessHorseman_HP3", "TB_Amalgam_Ench", "Overgrowth",
				"At the start of your turn, give a random friendly minion +1 Health.",
				() => new Power { Trigger = Trig(TriggerType.TURN_START, BuffRandomFriendly("buff_hp_1")) });
			BossPassive("vigil", "TB_HeadlessHorseman_HP4", "TB_BlingBrawl_Blade1e", "Vigil",
				"At the start of your turn, restore 4 Health to your hero.",
				() => new Power { Trigger = Trig(TriggerType.TURN_START, new HealTask(4, EntityType.HERO)) });
			BossPassive("tithe", "TB_HeadlessHorseman_HP5", "TB_BlingBrawl_Blade2e", "Tithe",
				"At the end of your turn, deal 2 damage to the enemy hero.",
				() => new Power { Trigger = Trig(TriggerType.TURN_END, new DamageTask(2, EntityType.OP_HERO)) });
			BossPassive("discipline", "TB_HeadlessHorseman_HP6", "TB_BlingBrawl_Hero1e", "Stone Discipline",
				"At the start of your turn, give a random friendly minion +1/+1.",
				() => new Power { Trigger = Trig(TriggerType.TURN_START, BuffRandomFriendly("buff_1_1")) });
			BossPassive("command", "TB_BRMA01_2H_2", "TB_207MaskedBallE", "Hollow Command",
				"At the start of your turn, give your minions +1/+1.",
				() => new Power { Trigger = Trig(TriggerType.TURN_START, BuffAllFriendly("buff_1_1")) });
			BossPassive("bramble", "TB_BRMA10_3H", "TB_207TolBaradE", "Bramble Field",
				"At the end of your turn, deal 1 damage to all enemy minions.",
				() => new Power { Trigger = Trig(TriggerType.TURN_END, new DamageTask(1, EntityType.OP_MINIONS)) });
			BossPassive("devour", "TB_MechWar_Boss2_HeroPower", "TB_DoubleMinions_ench", "Devour",
				"At the start of your turn, deal 2 damage to all enemy minions.",
				() => new Power { Trigger = Trig(TriggerType.TURN_START, new DamageTask(2, EntityType.OP_MINIONS)) });
			BossPassive("hoard", "TB_LOEA13_2", "TB_DeathrattleYog_ench", "Hoard",
				"At the start of your turn, draw a card and gain 3 Armor.",
				() => new Power { Trigger = Trig(TriggerType.TURN_START, ComplexTask.Create(new DrawTask(), new ArmorTask(3))) });
			BossPassive("undying", "TB_BountyHunt_Consume", "TB_010e", "Undying",
				"After a friendly minion dies, summon a 1/1 Recruit.",
				() => new Power { Trigger = Trig(TriggerType.DEATH, new SummonTask("CS2_101t"), TriggerSource.FRIENDLY, SelfCondition.IsMinion) });
			BossPassive("sovereign", "TB_BlingBrawl_Hero1p", "TB_006e", "Sovereign's Due",
				"At the start of your turn, give your minions +1/+1 and deal 1 damage to the enemy hero.",
				() => new Power { Trigger = Trig(TriggerType.TURN_START, ComplexTask.Create(BuffAllFriendly("buff_1_1"), new DamageTask(1, EntityType.OP_HERO))) });

			// ---------------- Passive treasures. Effects first.
			Effect("fx_whetstone", "TB_007e", "Whetstone Charm", "At the start of your turn, give a random friendly minion +1 Attack.",
				() => new Power { Trigger = Trig(TriggerType.TURN_START, BuffRandomFriendly("buff_atk_1")) });
			Effect("fx_iron_skin", "TB_Champs_EX1_145e", "Iron Skin", "At the end of your turn, gain 2 Armor.",
				() => new Power { Trigger = Trig(TriggerType.TURN_END, new ArmorTask(2)) });
			Effect("fx_ember_brand", "TB_Chupacabran_HP_E", "Ember Brand", "After you cast a spell, deal 1 damage to a random enemy.",
				() => new Power { Trigger = Trig(TriggerType.AFTER_CAST, DamageRandomEnemy(1)) });
			Effect("fx_grave_candle", "TB_DiscoverMyDeck_Enchantment", "Grave Candle", "After a friendly minion dies, give a random friendly minion +1/+1.",
				() => new Power { Trigger = Trig(TriggerType.DEATH, BuffRandomFriendly("buff_1_1"), TriggerSource.FRIENDLY, SelfCondition.IsMinion) });
			Effect("fx_mending", "TB_DrawnDiscovery_Ench", "Mending Charm", "At the end of your turn, restore 3 Health to your hero.",
				() => new Power { Trigger = Trig(TriggerType.TURN_END, new HealTask(3, EntityType.HERO)) });
			Effect("fx_quiet_satchel", "TB_EndlessMinions01", "Quiet Satchel", "At the start of your turn, if you have 3 or fewer cards in hand, draw a card.",
				() => new Power { Trigger = Trig(TriggerType.TURN_START, Custom(c => { if (c.HandZone.Count <= 3) Generic.Draw(c); })) });
			Effect("fx_banner", "TB_Face_Ench1", "Champion's Banner", "After you summon a minion, give it +1 Health.",
				() => new Power { Trigger = Trig(TriggerType.AFTER_SUMMON, new AddEnchantmentTask("TB_CoOpv3_BOSS2e", EntityType.TARGET), TriggerSource.MINIONS) });
			Effect("fx_thrifty", "TB_Firefest2_003e", "Thrifty Gloves", "Your Hero Power costs (1) less.",
				() => new Power { Aura = new Aura(AuraType.HEROPOWER, Effects.ReduceCost(1)) });
			Effect("fx_spark_lantern", "TB_Firefest2_005e", "Spark Lantern", "At the start of your turn, deal 1 damage to a random enemy minion.",
				() => new Power { Trigger = Trig(TriggerType.TURN_START, DamageRandomEnemyMinion(1)) });
			Effect("fx_stoneskin", "TB_FireFestEnch", "Stoneskin Idol", "At the start of your turn, give your minions +1 Health.",
				() => new Power { Trigger = Trig(TriggerType.TURN_START, BuffAllFriendly("buff_hp_1")) });
			Effect("fx_old_map", "TB_FoxBlessing1", "Old Map", "At the start of your turn, reduce the Cost of a random card in your hand by (1).",
				() => new Power { Trigger = Trig(TriggerType.TURN_START, ComplexTask.Create(
					new IncludeTask(EntityType.HAND), new RandomTask(1, EntityType.STACK), new AddEnchantmentTask("TB_CoOpv3_BOSSe", EntityType.STACK))) });
			Effect("fx_keen_eye", "TB_FoxBlessing2", "Keen Eye", "After an enemy minion is <b>Frozen</b>, deal 1 damage to it.",
				() => new Power { Trigger = Trig(TriggerType.FROZEN, new DamageTask(1, EntityType.TARGET), TriggerSource.OP_MINIONS) });

			Treasure("hearty_tonic", "Hearty Tonic", "Your hero starts each match with +10 Health.", health: 10);
			Treasure("deep_satchel", "Deep Satchel", "Start each match with an extra card in your opening hand.", opening: 1);
			Treasure("spare_crystal", "Spare Crystal", "Start each match with an extra Mana Crystal.",
				onStart: c => c.BaseMana = Math.Min(c.BaseMana + 1, Controller.MaxResources));
			Treasure("lucky_coin", "Lucky Coin", "Start each match with a Coin in your hand.",
				onStart: c => Generic.AddHandPhase(c, Entity.FromCard(c, Cards.FromId("GAME_005"))));
			Treasure("whetstone_charm", "Whetstone Charm", Get("fx_whetstone").Text, effect: "fx_whetstone");
			Treasure("iron_skin", "Iron Skin", Get("fx_iron_skin").Text, effect: "fx_iron_skin");
			Treasure("ember_brand", "Ember Brand", Get("fx_ember_brand").Text, effect: "fx_ember_brand");
			Treasure("grave_candle", "Grave Candle", Get("fx_grave_candle").Text, effect: "fx_grave_candle");
			Treasure("mending_charm", "Mending Charm", Get("fx_mending").Text, effect: "fx_mending");
			Treasure("quiet_satchel", "Quiet Satchel", Get("fx_quiet_satchel").Text, effect: "fx_quiet_satchel");
			Treasure("champions_banner", "Champion's Banner", Get("fx_banner").Text, effect: "fx_banner");
			Treasure("thrifty_gloves", "Thrifty Gloves", Get("fx_thrifty").Text, effect: "fx_thrifty");
			Treasure("spark_lantern", "Spark Lantern", Get("fx_spark_lantern").Text, effect: "fx_spark_lantern");
			Treasure("stoneskin_idol", "Stoneskin Idol", Get("fx_stoneskin").Text, effect: "fx_stoneskin");
			Treasure("old_map", "Old Map", Get("fx_old_map").Text, effect: "fx_old_map");
			Treasure("keen_eye", "Keen Eye", Get("fx_keen_eye").Text, effect: "fx_keen_eye");

			// ---------------- Active treasures
			Spell("blazing_rod", "TB_CoOpv3_001", "Blazing Rod", "Deal $4 damage.", 2, AnyTarget, () => new DamageTask(4, EntityType.TARGET, true));
			Spell("iron_ward", "TB_CoOpv3_002", "Iron Ward", "Gain 6 Armor.", 1, null, () => new ArmorTask(6));
			Spell("bottomless_tome", "TB_CoOpv3_003", "Bottomless Tome", "Draw 3 cards.", 3, null, () => new DrawTask(3));
			Spell("mirror_shard", "TB_CoOpv3_004", "Mirror Shard", "Summon a copy of a friendly minion.", 2,
				new[] { PlayReq.REQ_TARGET_TO_PLAY, PlayReq.REQ_MINION_TARGET, PlayReq.REQ_FRIENDLY_TARGET, PlayReq.REQ_NUM_MINION_SLOTS },
				() => new SummonCopyTask(EntityType.TARGET));
			Spell("frost_lantern", "TB_CoOpv3_005", "Frost Lantern", "<b>Freeze</b> all enemy minions.", 1, null, () => ComplexTask.Freeze(EntityType.OP_MINIONS));
			Spell("quake_stone", "TB_CoOpv3_006", "Quake Stone", "Deal $2 damage to all minions.", 4, null, () => new DamageTask(2, EntityType.ALLMINIONS, true));
			Spell("healing_spring", "TB_CoOpv3_007", "Healing Spring", "Restore 8 Health to your hero.", 2, null, () => new HealTask(8, EntityType.HERO));
			Minion("warden_golem", "TB_015", "Warden Golem", "<b>Taunt</b>", 3, 3, 5, taunt: true);
			Minion("swift_hound", "TB_Coopv3_100", "Swift Hound", "<b>Rush</b>", 2, 3, 2, rush: true);
			Minion("gilded_idol", "TB_Coopv3_101", "Gilded Idol", "<b>Battlecry:</b> Draw a card.", 4, 4, 4, battlecry: () => new DrawTask());
			// Upgrading treasure: the run layer swaps each stage for the next after every 2 floors cleared.
			Spell("charm_seedling", "TB_CoOpv3_008", "Seedling Charm", "Gain 2 Armor. Draw a card.\n<i>Grows every 2 floors.</i>", 1, null,
				() => ComplexTask.Create(new ArmorTask(2), new DrawTask()));
			Spell("charm_sprouting", "TB_CoOpv3_009", "Sprouting Charm", "Gain 4 Armor. Draw a card.\n<i>Grows every 2 floors.</i>", 1, null,
				() => ComplexTask.Create(new ArmorTask(4), new DrawTask()));
			Spell("charm_blooming", "TB_CoOpv3_010", "Blooming Charm", "Gain 6 Armor. Draw 2 cards.", 1, null,
				() => ComplexTask.Create(new ArmorTask(6), new DrawTask(2)));
			// Cartographer signatures: no longer offered (data "legacy"); kept because version-1 saves still hold them
			Spell("sig_field_atlas", "TB_CoOpv3_011", "Field Atlas", "Draw 2 cards.", 1, null, () => new DrawTask(2));
			Spell("sig_smoke_flask", "TB_CoOpv3_012", "Smoke Flask", "Return an enemy minion to its owner's hand.", 2, EnemyMinion,
				() => new ReturnHandTask(EntityType.TARGET));
			Spell("sig_star_chart", "TB_CoOpv3_013", "Star Chart", "Deal $2 damage to all enemy minions.", 3, null,
				() => new DamageTask(2, EntityType.OP_MINIONS, true));
			// Offered only with Set Snare (data scope)
			Spell("hunting_net", "TB_CoOpv3_200", "Hunting Net", "<b>Freeze</b> two random enemy minions.", 1, null,
				() => ComplexTask.Create(new IncludeTask(EntityType.OP_MINIONS), new RandomTask(2, EntityType.STACK), ComplexTask.Freeze(EntityType.STACK)));

			TavernEnchantments();
			NamedHeroes();
			DungeonRunPassives();
			DungeonRunActives();
			MonsterHuntTessDarius();
			MonsterHuntShawToki();
			MonsterHuntShared();
			HeistHeroes();
			HeistActives();
			HeistPassives();
			TombsExplorers();
			TombsSignatures();
			TombsActives();
			TombsPassives();
			Deferrals();

			ByKeyMap = Entries.ToDictionary(e => e.Key);
		}

		/// <summary>The content keys of Bob's tavern deck enchantments, by enchantment card id (phase 5).</summary>
		public static readonly IReadOnlyDictionary<string, string> TavernEnchantmentKeys = new Dictionary<string, string>
		{
			["DALA_903e"] = "tavern_lucky", ["DALA_905e"] = "tavern_right_hand_man", ["DALA_906e"] = "tavern_merriment",
			["DALA_908e"] = "tavern_stories_told", ["DALA_913e"] = "tavern_gossip", ["DALA_914e"] = "tavern_upper_hand",
			["ULDA_603e"] = "tavern_friendly_upgrade", ["ULDA_605e"] = "tavern_satiated", ["ULDA_606e"] = "tavern_work_work",
		};

		// ---------------------------------------------------------------- TavernEnchantments
		// Bob's tavern deck enchantments (The Dalaran Heist, Tombs of Terror). The run layer stores their ids on a run card
		// (DungeonRunCard.Mods, applied in order) and DungeonRunSetup.ApplyAfterStart attaches them to the card's entity at
		// match start, so the AI, the fair view and the client all see a standard enchantment. No engine set scripts these
		// ids (they are client cards with no CardDefs entry); DungeonRunCardsGen.AddAll adds a duplicate id with
		// Dictionary.Add, which throws, so an existing definition could never be silently overridden.
		private static void TavernEnchantments()
		{
			Buff("tavern_lucky", "DALA_903e", "Lucky!", "Costs (0).", Effects.SetCost(0));
			// A marker: the run layer deals the flagged card into the opening hand (GameConfig.Player1OpeningSlots).
			Effect("tavern_right_hand_man", "DALA_905e", "Right Hand Man", "Always starts in your hand.", () => new Power());
			Buff("tavern_merriment", "DALA_906e", "Merriment", "+1/+1.", Effects.AttackHealth_N(1));
			Buff("tavern_stories_told", "DALA_908e", "Stories Told", "+2/+2.", Effects.AttackHealth_N(2));
			Buff("tavern_gossip", "DALA_913e", "Gossip", "+4/+4 and costs (2) more.", Effects.Attack_N(4), Effects.Health_N(4), Effects.AddCost(2));
			Buff("tavern_upper_hand", "DALA_914e", "The Upper Hand", "Costs (3) less.", Effects.ReduceCost(3));
			Buff("tavern_friendly_upgrade", "ULDA_603e", "Friendly Upgrade", "+2/+2.", Effects.Attack_N(2), new SabberStoneCore.Enchants.Effect(GameTag.DURABILITY, EffectOperator.ADD, 2));
			Buff("tavern_satiated", "ULDA_605e", "Satiated", "Costs (1) less.", Effects.ReduceCost(1));
			Buff("tavern_work_work", "ULDA_606e", "Work, Work!", "+2/+2.", Effects.AttackHealth_N(2));
		}

		// ---------------------------------------------------------------- NamedHeroes
		// The 26 adventure hero portraits a run hero row may use (phase 6, family F0): the 9 Dalaran Heist villains, the 4
		// Monster Hunt hunters, Rikkar's 9 class forms and the 4 Tombs of Terror explorers. Boss variants (GILA_500h4 and the
		// others) stay boss-only; the player/boss portrait rule (DungeonRunValidator) keeps a boss from sharing a run portrait.
		private static void NamedHeroes()
		{
			Hero("DALA_Rakanishu", "Rakanishu", null);
			Hero("DALA_Barkeye", "Ol' Barkeye", null);
			Hero("DALA_Vessina", "Vessina", null);
			Hero("DALA_Kriziki", "Kriziki", null);
			Hero("DALA_Eudora", "Captain Eudora", null);
			Hero("DALA_Chu", "Mr. Chu", null);
			Hero("DALA_Squeamlish", "Squeamlish", null);
			Hero("DALA_Tekahn", "Tekahn", null);
			Hero("DALA_George", "George the Fallen", null);
			Hero("GILA_500h3", "Tess Greymane", null);
			Hero("GILA_600h", "Darius Crowley", null);
			Hero("GILA_400h", "Houndmaster Shaw", null);
			Hero("GILA_900h", "Toki, Time-Tinker", null);
			foreach (string form in new[] { "Mage", "Druid", "Hunter", "Paladin", "Priest", "Rogue", "Shaman", "Warlock", "Warrior" })
				Hero("TRLA_209h_" + form, "Rikkar", null);
			Hero("ULDA_Reno", "Reno Jackson", "[x]With a bag full of treasures \nand a well-styled mustache,\n Reno is ready to take \nTekahn down!");
			Hero("ULDA_Finley", "Sir Finley", "[x]Sir Finley rides into battle,\nready to dispense justice \nagainst his final foe.");
			Hero("ULDA_Elise", "Elise Starseeker", "[x]Whether she needs magic\nor a trusty machete, Elise \nis ready to save Uldum.");
			Hero("ULDA_Brann", "Brann Bronzebeard", "[x]Riding on a dinosaur\nand looking for a fight, \nBrann's ready to finish this.");
		}

		// ---------------------------------------------------------------- DungeonRunPassives
		// The original Dungeon Run (Kobolds and Catacombs) passive treasures (phase 6, family F1), keyed and shelled by their own
		// client ids, so the treasure picker shows the real card; every name and text is the client's own, word for word
		// (facts/violet-hold-2026-09.json). An aura is an Effect entry attached to the player at match start
		// (DungeonRunSetup.ApplyAfterStart): on the client's own "<id>e" player enchantment where the client has one, else on a
		// borrowed, unscripted Tavern Brawl enchantment shell. Start-of-game changes are plan modifiers (Health, opening hand,
		// hero power) or OnMatchStart (an extra Mana Crystal, three Secrets), applied before the mulligan. No CustomTask: every
		// aura and trigger is an engine Aura or Trigger, so clones and AI simulations replay it.

		/// <summary>
		/// LOOTA_802 Justicar's Ring: each basic class power and its upgraded form (the table Genn Greymane's Worgen form uses,
		/// CATA_615t). A run power with no entry is never upgraded; the treasure's data scope lists exactly these powers, so the
		/// ring is offered only to a run it can upgrade (DungeonRunValidator checks the scope against this table).
		/// </summary>
		public static readonly IReadOnlyDictionary<string, string> HeroPowerUpgrades = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["CS2_017"] = "AT_132_DRUID", ["DS1h_292"] = "AT_132_HUNTER", ["CS2_034"] = "AT_132_MAGE", ["CS2_101"] = "AT_132_PALADIN",
			["CS1h_001"] = "AT_132_PRIEST", ["CS2_083b"] = "AT_132_ROGUE", ["CS2_049"] = "AT_132_SHAMAN", ["CS2_056"] = "AT_132_WARLOCK",
			["CS2_102"] = "AT_132_WARRIOR", ["HERO_11bp"] = "HERO_11bp2", ["HERO_10bp"] = "HERO_10bp2",
		};

		/// <summary>The Secrets LOOTA_833 Mysterious Tome puts into play at the start of the game.</summary>
		public const int MysteriousTomeSecrets = 3;

		private static void DungeonRunPassives()
		{
			// Effects: the auras the treasures attach. Shell names and texts are the client's (a null text keeps the client's own).
			Effect("fx_LOOTA_802", "LOOTA_802e", "Justicar's Ring Enchantment", "Your Hero Power costs (1).",
				() => new Power { Aura = new Aura(AuraType.HEROPOWER, Effects.SetCost(1)) });
			Effect("fx_LOOTA_803", "LOOTA_803e", "Scepter of Summoning Enchantment", null,
				() => new Power { Aura = new Aura(AuraType.HAND, Effects.SetCost(5)) { Condition = new SelfCondition(p => p is Minion && p.Cost >= 5) } });
			Effect("fx_LOOTA_818", "LOOTA_818e", "Grom's Amguards Enchantment", null,
				() => new Power { Aura = new Aura(AuraType.HAND, Effects.SetCost(1)) { Condition = SelfCondition.IsWeapon } });
			Effect("fx_LOOTA_824", "LOOTA_824e", "Khadgar's Scrying Orb Enchantment", "Your spells cost (1) less.",
				() => new Power { Aura = new Aura(AuraType.HAND, Effects.ReduceCost(1)) { Condition = SelfCondition.IsSpell } });
			Effect("fx_LOOTA_825", "LOOTA_825e", "Robe of the Magi Enchantment", "+3 Spell Damage.",
				() => new Power { Aura = new Aura(AuraType.CONTROLLER, new SabberStoneCore.Enchants.Effect(GameTag.SPELLPOWER, EffectOperator.ADD, 3)) });
			Effect("fx_LOOTA_828", "LOOTA_828e2", "Banner of Inspiration Player Enchant", "+1/+1.",
				() => new Power { Aura = new Aura(AuraType.BOARD, Effects.AttackHealth_N(1)) });
			// The client's own text for this enchantment is "+1/+1." (a client data slip); it is shown as the client has it.
			Effect("fx_LOOTA_831", "LOOTA_831e", "Glyph of Warding Enchantment", "+1/+1.",
				() => new Power { Aura = new Aura(AuraType.OP_HAND, Effects.AddCost(1)) { Condition = SelfCondition.IsMinion } });
			// Permanent Stealth: a BOARD aura, so it covers every friendly minion however it arrived (summoned, taken by a control
			// effect, made by a transform) and lasts while the treasure does. Aura Stealth sits apart from the minion's own flag
			// (AuraEffects STEALTH, read by Character.HasStealth), so the attack-time clear (Attack.cs) and Silence leave it.
			Effect("fx_LOOTA_832", "LOOTA_832e2", "Cloak of Invisiblity Player Enchant", null,
				() => new Power { Aura = new Aura(AuraType.BOARD, new SabberStoneCore.Enchants.Effect(GameTag.STEALTH, EffectOperator.ADD, 1)) });
			// No client enchantment for the two totems: borrowed, unscripted Tavern Brawl shells with the treasure's own text.
			Effect("fx_LOOTA_845", "TB_001", "Totem of the Dead", "Your <b>Deathrattles</b> trigger twice.",
				() => new Power { Aura = new Aura(AuraType.CONTROLLER, new SabberStoneCore.Enchants.Effect(GameTag.EXTRA_DEATHRATTLES_BASE, EffectOperator.ADD, 1)) });
			Effect("fx_LOOTA_846", "TB_013", "Battle Totem", "Your <b>Battlecries</b> trigger twice.",
				() => new Power { Aura = new Aura(AuraType.CONTROLLER, new SabberStoneCore.Enchants.Effect(GameTag.EXTRA_BATTLECRIES_BASE, EffectOperator.ADD, 1)) });

			Treasure("LOOTA_800", "Potion of Vitality", "<b>Passive</b>\nDouble your\nstarting Health.", doubleHealth: true, ownShell: true);
			Treasure("LOOTA_801", "Crystal Gem", "<b>Passive</b>\nStart with an extra Mana Crystal.", ownShell: true,
				onStart: c => c.BaseMana = Math.Min(c.BaseMana + 1, Controller.MaxResources));
			Treasure("LOOTA_802", "Justicar's Ring", "<b>Passive</b>\nYour Hero Power is upgraded and costs (1).", effect: "fx_LOOTA_802",
				upgradePower: true, ownShell: true);
			Treasure("LOOTA_803", "Scepter of Summoning", "<b>Passive</b>\nYour minions that\ncost (5) or more\ncost (5).", effect: "fx_LOOTA_803", ownShell: true);
			// "At the start of the game, draw 2 cards": two extra opening-hand cards (a plan modifier, dealt before the mulligan).
			Treasure("LOOTA_804", "Small Backpacks", "<b>Passive</b>\nAt the start of the game, draw 2 cards.", opening: 2, ownShell: true);
			Treasure("LOOTA_818", "Grommash's Armguards", "<b>Passive</b>\nYour weapons cost (1).", effect: "fx_LOOTA_818", ownShell: true);
			Treasure("LOOTA_824", "Khadgar's Scrying Orb", "<b>Passive</b>\nYour spells cost (1)_less.", effect: "fx_LOOTA_824", ownShell: true);
			Treasure("LOOTA_825", "Robe of the Magi", "<b>Passive</b>\n<b>Spell Damage +3</b>", effect: "fx_LOOTA_825", ownShell: true);
			Treasure("LOOTA_828", "Captured Flag", "<b>Passive</b>\nYour minions have_+1/+1.", effect: "fx_LOOTA_828", ownShell: true);
			Treasure("LOOTA_831", "Glyph of Warding", "<b>Passive</b>\nEnemy minions cost (1)_more.", effect: "fx_LOOTA_831", ownShell: true);
			Treasure("LOOTA_832", "Cloak of Invisibility", "<b>Passive</b>\nYour minions have permanent <b>Stealth</b>.", effect: "fx_LOOTA_832", ownShell: true);
			Treasure("LOOTA_833", "Mysterious Tome", "<b>Passive</b>\nAt the start of the game, play 3 random <b>Secrets</b>.", ownShell: true,
				onStart: c => PlayRandomSecrets(c, MysteriousTomeSecrets));
			Treasure("LOOTA_845", "Totem of the Dead", "<b>Passive</b>\nYour <b>Deathrattles</b> trigger twice.", effect: "fx_LOOTA_845", ownShell: true);
			Treasure("LOOTA_846", "Battle Totem", "<b>Passive</b>\nYour <b>Battlecries</b> trigger_twice.", effect: "fx_LOOTA_846", ownShell: true);
		}

		/// <summary>
		/// The Secrets <see cref="PlayRandomSecrets"/> may put into play for <paramref name="c"/>: the scripted Secrets of its hero's
		/// class in the game's format that this game lets its generation yield (a side that excludes generated Secrets gets none;
		/// GameConfig.ExcludedGeneratedIds is honoured), ordinal by id. A catalog run's boss prior holds every one of them
		/// (DungeonRunAdmission.GeneratedSecretPool over the run's classes).
		/// </summary>
		public static IReadOnlyList<Card> RandomSecretCandidates(Controller c) => RandomSecretCandidates(c, c.HeroClass);

		/// <summary>
		/// <see cref="RandomSecretCandidates(Controller)"/> for the Secrets of <paramref name="cardClass"/> instead of the hero's
		/// class (GILA_509 Frenzied Trapper puts Hunter Secrets into play for any hero).
		/// </summary>
		public static IReadOnlyList<Card> RandomSecretCandidates(Controller c, CardClass cardClass)
		{
			Game game = c.Game;
			if (game.ExcludesGeneratedSecretsFor(c.PlayerId)) return Array.Empty<Card>();
			if (!Cards.FormatTypeClassCards(game.FormatType).TryGetValue(cardClass, out var classCards)) return Array.Empty<Card>();
			var excluded = game.ExcludedGeneratedIds;
			return classCards.Where(card => card != null && card.IsSecret && card.Implemented && (excluded == null || !excluded.Contains(card.Id)))
				.GroupBy(card => card.Id, StringComparer.Ordinal).Select(g => g.First())
				.OrderBy(card => card.Id, StringComparer.Ordinal).ToArray();
		}

		/// <summary>
		/// LOOTA_833 Mysterious Tome: puts up to <paramref name="count"/> different random Secrets (<see cref="RandomSecretCandidates"/>)
		/// into <paramref name="c"/>'s Secret zone, drawn with the game RNG, as a Secret enters play when cast (its trigger
		/// activated, exhausted), without casting it: no "cast a spell" trigger fires before the mulligan. Stops when the zone is full.
		/// </summary>
		internal static void PlayRandomSecrets(Controller c, int count) => PlayRandomSecrets(c, count, c.HeroClass);

		/// <summary><see cref="PlayRandomSecrets(Controller, int)"/> with the Secrets of <paramref name="cardClass"/> (GILA_509).</summary>
		internal static void PlayRandomSecrets(Controller c, int count, CardClass cardClass)
		{
			var pool = RandomSecretCandidates(c, cardClass).ToList();
			for (int i = 0; i < count && pool.Count > 0 && !c.SecretZone.IsFull; i++)
			{
				Card pick = pool[c.Game.Random.Next(pool.Count)];
				pool.Remove(pick);
				if (c.SecretZone.Any(s => s.Card.AssetId == pick.AssetId)) { i--; continue; }
				var secret = (Spell)Entity.FromCard(c, pick);
				secret.Power?.Trigger?.Activate(secret);
				c.SecretZone.Add(secret);
				secret.IsExhausted = true;
				// The roll is a random event for whoever plans around it (phase 8: a boss may hold a card that calls this).
				c.Game.OnRandomHappened(true);
			}
		}

		/// <summary>The named hero portrait ids (<see cref="RunContentKind.Hero"/> entries), in declaration order.</summary>
		public static IEnumerable<string> NamedHeroIds => Entries.Where(e => e.Kind == RunContentKind.Hero).Select(e => e.ShellId);

		private static void BossPassive(string key, string powerShell, string effectShell, string name, string text, Func<Power> power)
		{
			Effect("fx_" + key, effectShell, name, text, power);
			Passive("passive_" + key, powerShell, name, "<b>Passive</b>\n" + text, "fx_" + key);
		}

		private static string Roman(int n) => n == 1 ? "I" : n == 2 ? "II" : "III";
	}
}
