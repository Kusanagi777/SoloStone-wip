using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.CardSets.DungeonRun
{
	/// <summary>
	/// What one Dungeon Run fight needs from the engine. The run layer (plugin) fills it from the run state;
	/// <see cref="DungeonRunSetup"/> turns it into <see cref="GameConfig"/> fields and start-of-match effects.
	/// Player 1 is always the run player, player 2 the boss.
	/// </summary>
	public sealed class RunMatchPlan
	{
		public Card PlayerHero { get; set; }
		/// <summary>Hero power card id (a standard power or a content shell); null keeps the hero's own.</summary>
		public string PlayerHeroPowerId { get; set; }
		/// <summary>Passive treasure content keys, in pick order.</summary>
		public IList<string> PlayerTreasures { get; set; } = new List<string>();
		public List<Card> PlayerDeck { get; set; }

		public Card BossHero { get; set; }
		public string BossHeroPowerId { get; set; }
		public int BossHealth { get; set; } = 30;
		public List<Card> BossDeck { get; set; }
		/// <summary>Deckbuilding selections for boss deck cards that need them (e.g. a library deck's Beatrix pick).</summary>
		public IReadOnlyDictionary<string, string[]> BossSideboards { get; set; }
		/// <summary>
		/// Card ids generation never yields in this fight, whoever generates (the run's admission table, Generated scope;
		/// see plugin/DungeonRun/DungeonRunAdmission.cs). Copied into <see cref="GameConfig.ExcludedGeneratedIds"/>.
		/// </summary>
		public IReadOnlyCollection<string> ExcludedGenerated { get; set; }
		/// <summary>
		/// Card ids generation never yields when the boss generates, in addition to <see cref="ExcludedGenerated"/>: cards
		/// the table admits for the run player but whose Generated rejection is a boss AI limit. Copied into
		/// <see cref="GameConfig.Player2ExcludedGeneratedIds"/>.
		/// </summary>
		public IReadOnlyCollection<string> ExcludedGeneratedBoss { get; set; }
		/// <summary>
		/// The run player's generation may yield Secrets (a catalog run, DungeonRunState.CatalogVersion 1 or later): sets
		/// <see cref="GameConfig.Player1ExcludeGeneratedSecrets"/> off. False (a legacy run) keeps it on. The boss side's
		/// generated Secrets stay off either way (<see cref="GameConfig.Player2ExcludeGeneratedSecrets"/>).
		/// </summary>
		public bool PlayerGeneratedSecrets { get; set; }

		/// <summary>
		/// An act run's per-card records (phase 5), index-aligned with <see cref="PlayerDeck"/>: each card's persistent
		/// enchantments and opening-hand flag. Null or empty on a legacy run, which then plays exactly as before.
		/// </summary>
		public IList<RunDeckSlot> PlayerDeckSlots { get; set; }
		/// <summary>
		/// Extra Mana Crystals the boss starts with (an act's boss mana curve and its modifiers), clamped to
		/// <see cref="Controller.MaxResources"/>. 0 (every legacy run) changes nothing.
		/// </summary>
		public int BossStartManaBonus { get; set; }
		/// <summary>Extra starting hero Health for the run player beyond the treasures' (tavern Good Food, modifiers); 0 on a legacy run.</summary>
		public int PlayerHealthBonus { get; set; }
		/// <summary>Extra opening-hand cards beyond the treasures' (act modifiers); 0 on a legacy run.</summary>
		public int PlayerOpeningHandBonus { get; set; }
		/// <summary>
		/// The run hero's base Health before treasure and run bonuses (a named hero row's "health", phase 6); null keeps the
		/// portrait's printed Health, as every class row does.
		/// </summary>
		public int? PlayerHeroHealth { get; set; }
		/// <summary>
		/// The run facts card scripts read (RunHelpers); copied into
		/// <see cref="GameConfig.RunContext"/>. Null (every run before phase 6) leaves it unset.
		/// </summary>
		public RunContext RunContext { get; set; }
		/// <summary>
		/// A one-fight bonus a treasure granted last fight (ULDA_504): <see cref="RunNextFight.Mana"/> extra Mana Crystals at the
		/// start, <see cref="RunNextFight.Draws"/> extra opening-hand cards. Null for none.
		/// </summary>
		public RunNextFight NextFight { get; set; }
		/// <summary>
		/// The run player's board shrine (phase 7: a Rumble Run shrine named by the run's starter deck, a scripted one,
		/// <see cref="ShrineRules.IsScripted"/>); null for none, as on every run before phase 7. It lands at Start of Game.
		/// </summary>
		public string PlayerShrine { get; set; }
		/// <summary>How many copies of <see cref="PlayerShrine"/> start on the board: 1, or 2 with TRLA_804 Everlasting Devotion.</summary>
		public int PlayerShrineCopies { get; set; } = 1;
		/// <summary>The run's shrine upgrades (TRLA_801..809 ids), in pick order; empty without a shrine. Not passive treasures.</summary>
		public IList<string> PlayerShrineUpgrades { get; set; } = new List<string>();

		// ---- phase 8: encounters (all null, empty, 0 or false on every run before phase 8, which then plays exactly as before)
		/// <summary>The boss's board shrine (a Rumble Run champion's; a scripted shrine, <see cref="ShrineRules.IsScripted"/>); null for none.</summary>
		public string BossShrine { get; set; }
		/// <summary>
		/// The boss's generation may yield Secrets (an encounter row's "bossGeneratedSecrets": Whirt's Prediction): sets
		/// <see cref="GameConfig.Player2ExcludeGeneratedSecrets"/> off. False keeps the boss side's generated Secrets off.
		/// </summary>
		public bool BossGeneratedSecrets { get; set; }
		/// <summary>Extra opening-hand cards for the boss (0 to <see cref="GameConfig.MaxOpeningHandBonus"/>).</summary>
		public int BossOpeningHandBonus { get; set; }
		/// <summary>
		/// The boss's forms, form 0 first (the row's own hero and power); null or one entry for a boss with no forms. With
		/// <see cref="BossFormStart"/> above 0 the plan's <see cref="BossHero"/>, power, Health and deck are already that form's.
		/// </summary>
		public IList<RunForm> BossForms { get; set; }
		/// <summary>The form the boss starts in (persistent boss damage carried into a later phase); 0 on every other fight.</summary>
		public int BossFormStart { get; set; }
		/// <summary>Damage the boss starts with (persistent boss damage, ULDA_115's 10); at least 1 Health is always left.</summary>
		public int BossCarriedDamage { get; set; }
		/// <summary>Damage the run hero starts with (ULDA_720 Battle Scars); at least 1 Health is always left.</summary>
		public int PlayerCarriedDamage { get; set; }
		/// <summary>The encounter's start block (boards, weapon, Armor, deck and hand adds), or null.</summary>
		public RunStart Start { get; set; }
		/// <summary>The active global rules (anomalies and twists) as their card ids, in pick order; each lands on both players.</summary>
		public IList<string> GlobalRules { get; set; } = new List<string>();
		/// <summary>
		/// Hero cards the run player is given after the mulligan (Hagatha: the other monster hunters): playing one changes the
		/// hero and power for this match only (the run layer's HeroSwitch state records it; the run's hero is never written).
		/// Each option's <see cref="RunFightHero.Hero"/> is the hero card, its Deck the cards that join the deck when it is
		/// played, its Passives the passive treasures its Battlecry offers.
		/// </summary>
		public IList<RunFightHero> PlayerHeroCards { get; set; } = new List<RunFightHero>();
		/// <summary>The run player's relay heroes (the Tombs finale), in the order they enter when the hero before dies.</summary>
		public IList<RunFightHero> PlayerRelay { get; set; } = new List<RunFightHero>();
		/// <summary>The boss is an imported encounter: its deck may hold 10 to 60 cards (NativeMatch's 30-card check is relaxed).</summary>
		public bool EncounterDeck { get; set; }
		/// <summary>The encounter's run id (DungeonRunBoss.Id), for logs and the harvest; null on a custom boss.</summary>
		public string EncounterId { get; set; }
	}

	/// <summary>One boss form (encounters-v1.json "forms"): who the boss becomes, when, and what changes with it.</summary>
	public sealed class RunForm
	{
		/// <summary>The form's hero card; null keeps the hero entity (a power-only form).</summary>
		public Card Hero { get; set; }
		/// <summary>The form's hero power; null keeps the current power.</summary>
		public Card Power { get; set; }
		/// <summary>The form's maximum Health; 0 keeps the hero's.</summary>
		public int Health { get; set; }
		/// <summary>Enter when the living boss's Health is at or below this (0: not a Health form).</summary>
		public int HealthAtMost { get; set; }
		/// <summary>Enter when the boss dies (instead of losing).</summary>
		public bool OnDeath { get; set; }
		/// <summary>A Health form keeps the Health left (the Plague Lords); otherwise the form starts at full Health.</summary>
		public bool KeepExcess { get; set; }
		/// <summary>The form's deck (the old deck is set aside), or null to keep the deck.</summary>
		public IReadOnlyList<Card> Deck { get; set; }
		/// <summary>Cards drawn after the deck replacement.</summary>
		public int Draw { get; set; }
	}

	/// <summary>An encounter's start block (encounters-v1.json "start"); it lands after both mulligans (EncounterRules.ResolveStart).</summary>
	public sealed class RunStart
	{
		private static readonly IReadOnlyList<Card> None = new Card[0];
		public static readonly RunStart Empty = new RunStart();
		public IReadOnlyList<Card> BossBoard { get; set; } = None;
		public IReadOnlyList<Card> PlayerBoard { get; set; } = None;
		public IReadOnlyList<Card> BossDeckAdd { get; set; } = None;
		public IReadOnlyList<Card> PlayerDeckAdd { get; set; } = None;
		public IReadOnlyList<Card> BossHandAdd { get; set; } = None;
		public IReadOnlyList<Card> PlayerHandAdd { get; set; } = None;
		public Card BossWeapon { get; set; }
		public int BossArmor { get; set; }
		public bool IsEmpty => BossBoard.Count + PlayerBoard.Count + BossDeckAdd.Count + PlayerDeckAdd.Count + BossHandAdd.Count
			+ PlayerHandAdd.Count == 0 && BossWeapon == null && BossArmor == 0;
	}

	/// <summary>A hero the run player may become for one fight (a relay hero): portrait, power, Health and its own deck.</summary>
	public sealed class RunFightHero
	{
		public Card Hero { get; set; }
		public Card Power { get; set; }
		public int Health { get; set; }
		public IReadOnlyList<Card> Deck { get; set; } = new Card[0];
		/// <summary>A hero card's passive treasure offer (DungeonRunContent passive treasure keys); empty for a relay hero.</summary>
		public IReadOnlyList<string> Passives { get; set; } = new string[0];
	}

	/// <summary>
	/// The encounter rules of one match (<see cref="GameConfig.Encounter"/>): built once by <see cref="DungeonRunSetup.Configure"/>
	/// from the plan and never changed, so every clone shares it. Actions.EncounterRules reads it; its state is entity tags.
	/// </summary>
	public sealed class RunEncounter
	{
		public string Id { get; }
		public IReadOnlyList<RunForm> BossForms { get; }
		public RunStart Start { get; }
		/// <summary>The hero cards the run player is given after the mulligan, and their options (deck, passives).</summary>
		public IReadOnlyList<Card> PlayerHeroCards { get; }
		public IReadOnlyList<RunFightHero> HeroCardOptions { get; }
		public IReadOnlyList<RunFightHero> PlayerRelay { get; }
		public IReadOnlyList<string> GlobalRules { get; }
		/// <summary>The start block or the hero cards hold something to place after the mulligan.</summary>
		public bool HasStart => (Start != null && !Start.IsEmpty) || PlayerHeroCards.Count > 0 || GlobalRules.Any(DungeonRunGlobalRules.HasStart);

		internal RunEncounter(RunMatchPlan plan)
		{
			Id = plan.EncounterId;
			BossForms = (plan.BossForms ?? new List<RunForm>()).ToArray();
			Start = plan.Start;
			HeroCardOptions = (plan.PlayerHeroCards ?? new List<RunFightHero>()).ToArray();
			PlayerHeroCards = HeroCardOptions.Select(o => o.Hero).ToArray();
			PlayerRelay = (plan.PlayerRelay ?? new List<RunFightHero>()).ToArray();
			GlobalRules = (plan.GlobalRules ?? new List<string>()).ToArray();
		}

		/// <summary>The plan needs encounter rules at all (false on every run before phase 8: the config field stays null).</summary>
		internal static bool Needed(RunMatchPlan plan) =>
			(plan.BossForms?.Count ?? 0) > 1 || plan.Start != null && !plan.Start.IsEmpty || (plan.PlayerHeroCards?.Count ?? 0) > 0
			|| (plan.PlayerRelay?.Count ?? 0) > 0 || (plan.GlobalRules?.Count ?? 0) > 0;
	}

	/// <summary>What a fight leaves for the run layer (Actions.EncounterRules.Harvest; DungeonRun.RecordOutcome's "end").</summary>
	public sealed class RunMatchEnd
	{
		/// <summary>The boss hero's Health at the end (0 when it died): a phased boss's forms share one falling Health total.</summary>
		public int BossRemainingHealth { get; }
		/// <summary>The boss's form index at the end, and how many form changes it made.</summary>
		public int BossForm { get; }
		public int BossFormChanges { get; }
		/// <summary>The run player's current hero's damage at the end.</summary>
		public int PlayerHeroDamage { get; }
		/// <summary>Every hero card the run player's hero was this fight, in order (one entry when the hero never changed).</summary>
		public IReadOnlyList<string> HeroesUsed { get; }
		/// <summary>A script ended the fight with no loss for the run player.</summary>
		public bool NoLoss { get; }
		/// <summary>Family 24: the ULDA_115 "Gnomebliterator"s the run player cast this fight (each banks damage for the run's next Plague Lord).</summary>
		public int Gnomebliterators { get; }
		public RunMatchEnd(int bossRemainingHealth, int bossForm, int bossFormChanges, int playerHeroDamage, IEnumerable<string> heroesUsed, bool noLoss,
			int gnomebliterators = 0)
		{
			BossRemainingHealth = bossRemainingHealth; BossForm = bossForm; BossFormChanges = bossFormChanges; PlayerHeroDamage = playerHeroDamage;
			HeroesUsed = (heroesUsed ?? new string[0]).ToArray(); NoLoss = noLoss; Gnomebliterators = gnomebliterators;
		}
	}

	/// <summary>What an own-deck fact says (Actions.EncounterRules.DeckReplaced, DeckRewritten, DeckTopKnown).</summary>
	public enum RunDeckFactKind { Replaced, Rewritten, TopKnown }

	/// <summary>
	/// One own-deck fact: at history sequence <see cref="HistorySequence"/> the deck of player <see cref="PlayerId"/> became
	/// (Replaced, Rewritten) or is topped by (TopKnown) <see cref="Definitions"/>. Its owner's knowledge only.
	/// </summary>
	public sealed class RunDeckFact
	{
		public int PlayerId { get; }
		public int ControllerId { get; }
		public RunDeckFactKind Kind { get; }
		public long HistorySequence { get; }
		public IReadOnlyList<string> Definitions { get; }
		internal RunDeckFact(int playerId, int controllerId, RunDeckFactKind kind, long sequence, IEnumerable<string> definitions)
		{
			PlayerId = playerId; ControllerId = controllerId; Kind = kind; HistorySequence = sequence;
			Definitions = Array.AsReadOnly((definitions ?? new string[0]).ToArray());
		}
	}

	/// <summary>One run deck card's persistent state (an act run's DungeonRunCard), aligned with <see cref="RunMatchPlan.PlayerDeck"/>.</summary>
	public sealed class RunDeckSlot
	{
		/// <summary>Client enchantment card ids attached to the card's entity at match start, in order (tavern buffs, discounts).</summary>
		public string[] Mods { get; set; } = new string[0];
		/// <summary>The card always starts in the opening hand (<see cref="GameConfig.Player1OpeningSlots"/>).</summary>
		public bool Opening { get; set; }
		/// <summary>Where the card came from (starter, draft, treasure, tavern, copy); informational.</summary>
		public string Origin { get; set; }
	}

	/// <summary>Builds and applies a <see cref="RunMatchPlan"/>.</summary>
	public static class DungeonRunSetup
	{
		/// <summary>Hero power card id for a content key, or the id unchanged when it is already a card id.</summary>
		public static string PowerCardId(string keyOrCardId) =>
			keyOrCardId == null ? null : DungeonRunContent.TryGet(keyOrCardId, out var c) ? c.ShellId : keyOrCardId;

		/// <summary>
		/// The hero power a run plays this fight: <paramref name="power"/> (a content key or card id), or its upgraded form
		/// (<see cref="DungeonRunContent.HeroPowerUpgrades"/>) when one of <paramref name="treasures"/> upgrades the hero power
		/// (LOOTA_802 Justicar's Ring) and the table has an entry for it. A power with no entry is played unchanged.
		/// </summary>
		public static string PlanPower(string power, IEnumerable<string> treasures)
		{
			if (power == null || treasures == null) return power;
			bool upgrades = treasures.Any(key => DungeonRunContent.TryGet(key, out var t) && t.UpgradesHeroPower);
			return upgrades && DungeonRunContent.HeroPowerUpgrades.TryGetValue(power, out string upgraded) ? upgraded : power;
		}

		/// <summary>
		/// The two option card ids a Choose One card offers (the engine's own resolution), or an empty list for a card that is
		/// not Choose One or whose options do not resolve. The run validator checks a Choose One hero power's options with it.
		/// </summary>
		public static IReadOnlyList<string> ChooseOneOptionIds(Card card)
		{
			if (card == null || !card.ChooseOne) return Array.Empty<string>();
			try
			{
				Card[] options = Loader.ChooseOneOptions.Resolve(card);
				return options == null || options.Any(o => o == null) ? Array.Empty<string>() : options.Select(o => o.Id).ToArray();
			}
			catch (Exception) { return Array.Empty<string>(); }
		}

		/// <summary>Fills the run fields of <paramref name="config"/>. Call before constructing the Game.</summary>
		public static void Configure(GameConfig config, RunMatchPlan plan)
		{
			if (plan.PlayerHero == null || plan.BossHero == null) throw new ArgumentException("Run plan needs both heroes.");
			var treasures = plan.PlayerTreasures.Select(DungeonRunContent.Get).ToList();
			if (treasures.Any(t => t.Kind != RunContentKind.PassiveTreasure))
				throw new ArgumentException("Run plan treasure list holds a non-passive entry.");

			config.StartPlayer = 1; // the run player always goes first (design R9)
			// A legacy run keeps generated Secrets off for both sides: its boss's closed Secret prior lists only the run
			// pool's Secrets. A catalog run lets the run player generate Secrets; its boss's prior adds every Secret that
			// generation can yield (DungeonRunAdmission.CatalogSecretPrior). The boss side keeps generated Secrets off (the
			// boss-side lift belongs to the boss and finale encounters; DungeonRunAdmission.BossGeneratedSecretsLift).
			config.Player1ExcludeGeneratedSecrets = !plan.PlayerGeneratedSecrets;
			// Phase 8: an encounter row with "bossGeneratedSecrets" (the validator checks its closed, scripted yield pool) turns
			// the boss side on; every other boss keeps it off.
			config.Player2ExcludeGeneratedSecrets = !plan.BossGeneratedSecrets;
			config.ExcludedGeneratedIds = plan.ExcludedGenerated;
			config.Player2ExcludedGeneratedIds = plan.ExcludedGeneratedBoss;
			// Death Knight decks gain and spend Corpses; the live match already enables them (NativeMatch), and every run
			// match, headless ones included, plays by the same rule.
			config.EnableCorpses = true;
			config.Player1HeroCard = plan.PlayerHero;
			config.Player1HeroClass = plan.PlayerHero.Class;
			config.Player1Deck = plan.PlayerDeck;
			config.Player1HeroPower = PowerCard(plan.PlayerHeroPowerId);
			if (plan.PlayerHeroHealth is int own && own < 1) throw new ArgumentException("Run plan hero Health must be positive.");
			// A named hero's row Health replaces the portrait's; with no bonus and no row Health the hero keeps its own (null).
			int baseHealth = plan.PlayerHeroHealth ?? plan.PlayerHero.Health;
			// LOOTA_800 Potion of Vitality doubles the starting Health: its bonus is the base Health (phase 6 F1, a plan modifier).
			int health = treasures.Sum(t => t.HeroHealthBonus) + plan.PlayerHealthBonus + (treasures.Any(t => t.DoublesHeroHealth) ? baseHealth : 0);
			config.Player1HeroHealth = health > 0 || plan.PlayerHeroHealth != null ? baseHealth + health : (int?)null;
			config.Player1OpeningHandBonus = Math.Min(GameConfig.MaxOpeningHandBonus,
				treasures.Sum(t => t.OpeningHandBonus) + Math.Max(0, plan.PlayerOpeningHandBonus) + (plan.NextFight?.Draws ?? 0));
			config.RunContext = plan.RunContext;
			config.Player1OpeningSlots = OpeningSlots(plan);

			if (plan.PlayerShrine != null && !ShrineRules.IsScripted(plan.PlayerShrine))
				throw new ArgumentException("Run plan shrine " + plan.PlayerShrine + " is not a scripted board shrine.");
			if ((plan.PlayerShrineUpgrades?.Count ?? 0) > 0 && plan.PlayerShrine == null)
				throw new ArgumentException("Run plan shrine upgrades need a shrine.");
			if (plan.PlayerShrineCopies < 1 || plan.PlayerShrineCopies > 2) throw new ArgumentException("Run plan shrine copies must be 1 or 2.");

			config.Player2HeroCard = plan.BossHero;
			config.Player2HeroClass = plan.BossHero.Class;
			config.Player2Deck = plan.BossDeck;
			config.Player2HeroPower = PowerCard(plan.BossHeroPowerId);
			config.Player2HeroHealth = plan.BossHealth;
			config.Player2Sideboards = plan.BossSideboards;

			// Phase 8: the encounter rules. Nothing is set for a plan that needs none, so every earlier run's config is unchanged.
			if (plan.BossShrine != null && !ShrineRules.IsScripted(plan.BossShrine))
				throw new ArgumentException("Run plan boss shrine " + plan.BossShrine + " is not a scripted board shrine.");
			if (plan.BossOpeningHandBonus < 0 || plan.BossOpeningHandBonus > GameConfig.MaxOpeningHandBonus)
				throw new ArgumentException("Run plan boss opening-hand bonus must be 0 to " + GameConfig.MaxOpeningHandBonus + ".");
			if (plan.BossOpeningHandBonus > 0) config.Player2OpeningHandBonus = plan.BossOpeningHandBonus;
			int forms = plan.BossForms?.Count ?? 0;
			if (plan.BossFormStart < 0 || plan.BossFormStart > 0 && plan.BossFormStart >= forms)
				throw new ArgumentException("Run plan boss start form " + plan.BossFormStart + " is outside its " + forms + " forms.");
			if (plan.BossCarriedDamage < 0 || plan.PlayerCarriedDamage < 0) throw new ArgumentException("Run plan carried damage must not be negative.");
			foreach (string rule in plan.GlobalRules ?? new List<string>())
				if (!DungeonRunGlobalRules.IsScripted(rule)) throw new ArgumentException("Run plan global rule " + rule + " is not a scripted rule.");
			// Family 05: a rule's extra opening cards go to both players (DALA_859 Gorged), within the engine's cap.
			int ruleCards = DungeonRunGlobalRules.OpeningCards(plan.GlobalRules);
			if (ruleCards > 0)
			{
				config.Player1OpeningHandBonus = Math.Min(GameConfig.MaxOpeningHandBonus, config.Player1OpeningHandBonus + ruleCards);
				config.Player2OpeningHandBonus = Math.Min(GameConfig.MaxOpeningHandBonus, config.Player2OpeningHandBonus + ruleCards);
			}
			// Family 06: a rule that states the opening hand's size (ULDA_715 Bountiful: 9 cards each). The run player goes first
			// (3 cards before the bonus). The boss goes second (4 cards and The Coin): The Coin, which the engine deals before the
			// mulligan, is one of its nine, because a full hand cannot take a mulligan's replacement card. A larger bonus stays.
			int ruleHand = DungeonRunGlobalRules.OpeningHand(plan.GlobalRules);
			if (ruleHand > 0)
			{
				config.Player1OpeningHandBonus = Math.Max(config.Player1OpeningHandBonus, Math.Min(GameConfig.MaxFirstOpeningHandBonus, ruleHand - 3));
				config.Player2OpeningHandBonus = Math.Max(config.Player2OpeningHandBonus, Math.Max(0, Math.Min(GameConfig.MaxOpeningHandBonus, ruleHand - 5)));
			}
			if (RunEncounter.Needed(plan)) config.Encounter = new RunEncounter(plan);
		}

		/// <summary>
		/// Applies the player's passive treasures, in pick order. Call after <see cref="Game.StartGame"/> and
		/// before the mulligan completes, so every trigger is live from turn 1.
		/// </summary>
		public static void ApplyAfterStart(Game game, RunMatchPlan plan)
		{
			Controller player = game.Player1;
			ApplyDeckMods(game, plan);
			if (plan.NextFight != null && plan.NextFight.Mana > 0)
				player.BaseMana = Math.Min(player.BaseMana + plan.NextFight.Mana, Controller.MaxResources);
			if (plan.BossStartManaBonus > 0)
			{
				Controller boss = game.Player2;
				boss.BaseMana = Math.Min(boss.BaseMana + plan.BossStartManaBonus, Controller.MaxResources);
			}
			foreach (RunContent treasure in plan.PlayerTreasures.Select(DungeonRunContent.Get))
			{
				treasure.OnMatchStart?.Invoke(player);
				if (treasure.EffectKey == null) continue;
				AttachEffect(player, player.Hero, treasure.EffectKey);
				// A Start of Game treasure (phase 6 F8: DALA_737, 739, 743, 745): its attached player enchantment is a pending Start
				// of Game source, which StartOfGameRules.Resolve runs after both mulligans, as retail does.
				Card shell = Cards.FromId(DungeonRunContent.Get(treasure.EffectKey).ShellId);
				if (StartOfGameRules.HasEffect(shell))
				{
					Enchantment source = player.AppliedEnchantments?.LastOrDefault(e => e.Card.Id == shell.Id);
					if (source != null && source[StartOfGameRules.Pending] == 0) source[StartOfGameRules.Pending] = 1;
				}
			}
			// Phase 7: the shrine upgrades' player enchantments (TRLA_805e), then the board shrine, armed in the set-aside zone as a
			// pending Start of Game source that lands first, after both mulligans (ShrineRules.Arm, ShrineRules.SetupPriority).
			if (plan.PlayerShrine != null)
			{
				DungeonRunShrines.ApplyUpgrades(player, plan.PlayerShrineUpgrades);
				ShrineRules.Arm(player, plan.PlayerShrine, plan.PlayerShrineCopies);
			}
			// Phase 8, in this order: the boss's board shrine (armed like the player's: a pending Start of Game source that lands
			// after both mulligans), the starting powers that need their trigger or aura started, the global rules on both players,
			// then the carried damage and the boss's start form. Nothing here runs on a plan without encounter fields.
			if (plan.BossShrine != null) ShrineRules.Arm(game.Player2, plan.BossShrine, 1);
			EncounterRules.ActivateStartingPowers(game);
			foreach (string rule in plan.GlobalRules ?? new List<string>()) DungeonRunGlobalRules.Apply(game, rule);
			if (plan.BossFormStart > 0 || plan.BossCarriedDamage > 0 || plan.PlayerCarriedDamage > 0)
				EncounterRules.ApplyCarried(game, plan.BossFormStart, plan.BossCarriedDamage, plan.PlayerCarriedDamage);
			// A named run hero with a Start of Game effect (phase 6 F3: Darius Crowley's Cannons, GILA_600h): the hero is marked
			// as a pending Start of Game source, which StartOfGameRules.Resolve runs after both mulligans, as retail does.
			if (DungeonRunContent.TryGet(player.Hero.Card.Id, out RunContent hero) && hero.Kind == RunContentKind.Hero
				&& StartOfGameRules.HasEffect(player.Hero.Card))
				player.Hero[StartOfGameRules.Pending] = 1;
		}

		/// <summary>
		/// The plan's opening-flag slot indices (<see cref="RunDeckSlot.Opening"/>), in slot order; null when none is flagged.
		/// </summary>
		public static int[] OpeningSlots(RunMatchPlan plan)
		{
			if (plan.PlayerDeckSlots == null || plan.PlayerDeckSlots.Count == 0) return null;
			if (plan.PlayerDeck == null || plan.PlayerDeckSlots.Count != plan.PlayerDeck.Count)
				throw new ArgumentException("Run plan deck slots must align with the deck.");
			var slots = Enumerable.Range(0, plan.PlayerDeckSlots.Count).Where(i => plan.PlayerDeckSlots[i]?.Opening == true).ToArray();
			return slots.Length == 0 ? null : slots;
		}

		/// <summary>
		/// Attaches each run card's persistent enchantments (<see cref="RunDeckSlot.Mods"/>, in order) to its entity, in
		/// whatever zone it is now (deck or opening hand), found through <see cref="Game.Player1DeckSlotIds"/>. Runs before the
		/// treasures and before the first history send, so the client gets the buffs in the opening batch; a deck card shows
		/// its enchantment when drawn. No slots (a legacy run) changes nothing.
		/// </summary>
		private static void ApplyDeckMods(Game game, RunMatchPlan plan)
		{
			if (plan.PlayerDeckSlots == null || plan.PlayerDeckSlots.Count == 0) return;
			IReadOnlyList<int> ids = game.Player1DeckSlotIds;
			if (ids.Count != plan.PlayerDeckSlots.Count) throw new InvalidOperationException("Run deck slots do not match the game's deck.");
			Controller player = game.Player1;
			for (int i = 0; i < ids.Count; i++)
			{
				string[] mods = plan.PlayerDeckSlots[i]?.Mods;
				if (mods == null || mods.Length == 0) continue;
				IPlayable entity = game.IdEntityDic[ids[i]];
				if (entity == null || entity.Controller != player) continue;
				foreach (string mod in mods)
				{
					Card enchantment = Cards.FromId(mod) ?? throw new ArgumentException("Unknown run deck enchantment " + mod + ".");
					if (enchantment.Power == null) throw new ArgumentException("Run deck enchantment " + mod + " has no engine script.");
					Generic.AddEnchantmentBlock(game, enchantment, player.Hero, entity);
				}
			}
		}

		/// <summary>
		/// Re-applies the cost effects of a card's run deck enchantments (Bob's tavern ids, <see cref="DungeonRunContent.TavernEnchantmentKeys"/>),
		/// in the order they are attached, after <see cref="Model.Zones.HandZone"/> resets its cost on leaving the hand. Those
		/// enchantments stay attached through a mulligan or a shuffle into the deck and keep their stat effects, so their cost
		/// effects must stay too: a bought discount (The Upper Hand, Satiated, Lucky!) or Gossip's +2 lasts the whole match, as
		/// the run's deck view shows. A card with no tavern enchantment (every non-run match) is untouched.
		/// </summary>
		internal static void ReplayDeckCostEnchantments(Playable entity)
		{
			var applied = entity.AppliedEnchantments;
			if (applied == null || applied.Count == 0) return;
			bool changed = false;
			foreach (Enchantment enchantment in applied.ToArray())
			{
				if (!DungeonRunContent.TavernEnchantmentKeys.ContainsKey(enchantment.Card.Id)) continue;
				var effects = enchantment.Card.Power?.Enchant?.Effects;
				if (effects == null) continue;
				foreach (var effect in effects)
					if (effect.Tag == Enums.GameTag.COST) { effect.ApplyTo(entity); changed = true; }
			}
			if (changed && entity.Game.History)
				entity.Game.PowerHistory.Add(Kettle.PowerHistoryBuilder.TagChange(entity.Id, Enums.GameTag.COST, entity.Cost));
		}

		/// <summary>What the finished fight leaves for the run layer (phase 8: Actions.EncounterRules.Harvest), read from the live game.</summary>
		public static RunMatchEnd Harvest(Game game) => EncounterRules.Harvest(game);

		/// <summary>Attaches an effect to a player for the rest of the game, activating its trigger and aura.</summary>
		public static void AttachEffect(Controller controller, IPlayable source, string effectKey)
		{
			RunContent effect = DungeonRunContent.Get(effectKey);
			if (effect.Kind != RunContentKind.Effect) throw new ArgumentException(effectKey + " is not an effect.");
			Card card = Cards.FromId(effect.ShellId);
			bool fresh = controller.AppliedEnchantments?.Any(e => e.Card.Id == card.Id) != true;
			QuestRules.AddPlayerEffect(controller, source, card.Id);
			if (fresh && card.Power?.Aura != null)
				card.Power.Aura.Activate(controller.AppliedEnchantments.Last(e => e.Card.Id == card.Id));
		}

		private static Card PowerCard(string keyOrCardId) =>
			keyOrCardId == null ? null : Cards.FromId(PowerCardId(keyOrCardId));
	}
}
