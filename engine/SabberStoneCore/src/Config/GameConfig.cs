#region copyright
// SabberStone, Hearthstone Simulator in C# .NET Core
// Copyright (C) 2017-2019 SabberStone Team, darkfriend77 & rnilva
//
// SabberStone is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as
// published by the Free Software Foundation, either version 3 of the
// License.
// SabberStone is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
#endregion
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SabberStoneCore.Config
{
	/// <summary>
	/// Holds all configuration values to create a new <see cref="Game"/> instance.
	/// </summary>
	public class GameConfig
	{
		/// <summary>
		/// The default value for <see cref="StartPlayer"/>.
		/// -1 means the game will randomly pick one player as starting player.
		/// </summary>
		public const int START_PLAYER_DEFAULT = -1;

		/// <summary>
		/// The default name for players. The token will be replaced
		/// by the player index.
		/// </summary>
		public const string PLAYER_NAME_DEFAULT = "Player{0}";

		/// <summary>
		/// List of <see cref="Card.Id"/>s which result in unpredictable test outcomes when included
		/// in a <see cref="Controller"/>'s deck.
		/// </summary>
		public static readonly IReadOnlyCollection<string> UnPredictableCardIDs = new[] {
			"KAR_096", // Prince Malchezaar
			"CFM_637", // Patches the Pirate

			"UNG_028", // Quests
			"UNG_067",
			"UNG_116",
			"UNG_829",
			"UNG_934",
			"UNG_920",
			"UNG_940",
			"UNG_942",
			"UNG_954",
			"ULD_131",
			"ULD_155",
			"ULD_433",
			"ULD_431",
			"ULD_724",
			"ULD_326",
			"ULD_291",
			"ULD_140",
			"ULD_711",

			"LOOT_149", // Corridor Creeper (trigger on board)
			"CFM_064", // Blubber Baron (trigger on board)
		};

		/// <summary>
		/// Gets or sets the index of the starting player.
		/// This value is 1-indexed, where 1 indicates <see cref="Player1Name"/> will start.
		/// </summary>
		/// <value>The starting player index.</value>
		public int StartPlayer { get; set; } = START_PLAYER_DEFAULT;

		/// <summary>
		/// Gets or sets the name of the player with index 1.
		/// </summary>
		/// <value>The name of the player.</value>
		public string Player1Name { get; set; } = String.Format(PLAYER_NAME_DEFAULT, 1);

		/// <summary>
		/// Gets or sets the name of the player with index 2.
		/// </summary>
		/// <value>The name of the player.</value>
		public string Player2Name { get; set; } = String.Format(PLAYER_NAME_DEFAULT, 2);

		/// <summary>
		/// Gets or sets the card class of the player with index 1.
		/// </summary>
		/// <value><see cref="CardClass"/></value>
		public CardClass Player1HeroClass { get; set; } = CardClass.HUNTER;

		/// <summary>
		/// Gets or sets the card class of the player with index 2.
		/// </summary>
		/// <value><see cref="CardClass"/></value>
		public CardClass Player2HeroClass { get; set; } = CardClass.MAGE;

		/// <summary>
		/// Gets or sets the card class of the player with index 1.
		/// </summary>
		/// <value><see cref="CardClass"/></value>
		public Card Player1HeroCard { get; set; }

		/// <summary>
		/// Gets or sets the card class of the player with index 2.
		/// </summary>
		/// <value><see cref="CardClass"/></value>
		public Card Player2HeroCard { get; set; }

		// Run-mode hero setup (Dungeon Run bosses, treasures). Applied once when the Game is constructed;
		// clones and snapshots carry the resulting entity state, so these are construction inputs only.

		/// <summary>Hero power for player 1 instead of the hero card's own; null keeps the default.</summary>
		[field: NonSerialized]
		public Card Player1HeroPower { get; set; }

		/// <summary>Hero power for player 2 instead of the hero card's own; null keeps the default.</summary>
		[field: NonSerialized]
		public Card Player2HeroPower { get; set; }

		/// <summary>Starting maximum health for player 1's hero; null keeps the hero card's health.</summary>
		[field: NonSerialized]
		public int? Player1HeroHealth { get; set; }

		/// <summary>Starting maximum health for player 2's hero; null keeps the hero card's health.</summary>
		[field: NonSerialized]
		public int? Player2HeroHealth { get; set; }

		/// <summary>Extra cards dealt to player 1's opening hand, before the mulligan (0 to <see cref="MaxOpeningHandBonus"/>).</summary>
		[field: NonSerialized]
		public int Player1OpeningHandBonus { get; set; }

		/// <summary>Extra cards dealt to player 2's opening hand, before the mulligan (0 to <see cref="MaxOpeningHandBonus"/>).</summary>
		[field: NonSerialized]
		public int Player2OpeningHandBonus { get; set; }

		/// <summary>Largest opening-hand bonus: a second player's 4 cards plus The Coin, plus this, fill a 10-card hand.</summary>
		public const int MaxOpeningHandBonus = 5;

		/// <summary>
		/// Largest opening-hand bonus of the player <see cref="StartPlayer"/> names: a first player's 3 cards and no Coin, plus
		/// this, make a 9-card hand (phase 8 family 06: ULDA_715 Bountiful, "Both players start with 9 cards in their hand").
		/// </summary>
		public const int MaxFirstOpeningHandBonus = 6;

		/// <summary>
		/// Indices into <see cref="Player1Deck"/> (its order as given, before the Game constructor reverses an unshuffled deck)
		/// of run cards that always start in player 1's opening hand: the Dungeon Run opening flags (Right Hand Man, Study
		/// Break). At most <see cref="MaxOpeningSlots"/>, distinct, in range. Null or empty deals as before, consuming no RNG.
		/// </summary>
		[field: NonSerialized]
		public int[] Player1OpeningSlots { get; set; }

		/// <summary>Most opening-flag slots a run deck may carry (<see cref="Player1OpeningSlots"/>).</summary>
		public const int MaxOpeningSlots = 3;

		/// <summary>
		/// Dungeon Run facts card scripts may read (CardSets.DungeonRun.RunContext: bosses defeated, slot logs, the treasure
		/// pool, a next-fight bonus). Immutable, so a clone (MemberwiseClone) shares it with its source; null outside a run match.
		/// Not carried by snapshots: a script reads it through RunHelpers, which treats null as an empty run.
		/// </summary>
		[field: NonSerialized]
		public CardSets.DungeonRun.RunContext RunContext { get; set; }

		/// <summary>
		/// Dungeon Run encounter rules of this match (phase 8: boss forms, the start block, the player's hero relay, the global
		/// rules; CardSets.DungeonRun.RunEncounter). Immutable, so a clone (MemberwiseClone) shares it with its source; null
		/// outside an encounter match, where every Actions.EncounterRules hook is a no-op. All of its state is entity tags.
		/// </summary>
		[field: NonSerialized]
		public CardSets.DungeonRun.RunEncounter Encounter { get; set; }

		/// <summary>
		/// Gets or sets the format of the game.
		/// This value influences the game rules, see <see cref="FormatType"/>
		/// for more information.
		/// </summary>
		/// <value><see cref="FormatType"/></value>
		public FormatType FormatType { get; set; } = FormatType.FT_STANDARD;

		/// <summary>
		/// Gets or sets the deck of the player with index 1.</summary>
		/// <value><see cref="List{Card}"/></value>
		public List<Card> Player1Deck { get; set; } = null;

		/// <summary>
		/// Gets or sets the deck of the player with index 2.</summary>
		/// <value><see cref="List{Card}"/></value>
		/// <autogeneratedoc />
		public List<Card> Player2Deck { get; set; } = null;

        [field: NonSerialized]
        public IReadOnlyDictionary<string, string[]> Player1Sideboards { get; set; }
        [field: NonSerialized]
        public IReadOnlyDictionary<string, string[]> Player2Sideboards { get; set; }


		/// <summary>
		/// Gets or sets a value indicating whether the game should autofill the
		/// decks of all players.
		/// This option can be used in conjunction with <see cref="Player1Deck"/> and <see cref="Player2Deck"/>
		/// since it will only add cards until the deck limit is reached.
		/// </summary>
		/// <value><c>true</c> if decks need to be filled; otherwise, <c>false</c>.</value>
		public bool FillDecks { get; set; } = false;

		/// <summary>
		/// Gets or sets a value indicating whether the game should PREDICTABLY autofill the
		/// decks of all players. Set this property to true for predictable test cases!
		/// This option is only applicable when <see cref="FillDecks"/> is set to true.
		/// </summary>
		/// <value><c>true</c> if decks need to be filled PREDICTABLY; otherwise, <c>false</c>.</value>
		public bool FillDecksPredictably { get; set; } = false;

		/// <summary>
		/// Unimplemented feature, the intention was to have all possible allowed cards for a draw in
		/// a pool from which it draws.
		/// </summary>
		public bool DrawPool { get; set; } = false;

		/// <summary>
		/// Gets or sets a value indicating whether the <see cref="Game"/> should shuffle
		/// all decks before starting.</summary>
		/// <value><c>true</c> if shuffling is needed; otherwise, <c>false</c>.</value>
		public bool Shuffle { get; set; } = true;

		/// <summary>
		/// Gets or sets a value indicating whether <see cref="Game"/> must split itself
		/// when random events occur. Enabling this feature will reduce performance! At the moment
		/// this feature is only prototyped with the mad bomber tests.
		/// </summary>
		/// <value><c>true</c> if splitting is necessary; otherwise, <c>false</c>.</value>
		public bool Splitting { get; set; } = false;

		/// <summary>
		/// Gets or sets a value indicating whether <see cref="Game"/> should store log entries.</summary>
		/// <value><c>true</c> if logging is required; otherwise, <c>false</c>.</value>
		public bool Logging { get; set; } = true;

		/// <summary>
		/// Gets or sets a value indicating whether <see cref="Game"/> should store POWER history
		/// entries.
		/// </summary>
		/// <value><c>true</c> if POWER history building is required; otherwise, <c>false</c>.</value>
		public bool History { get; set; } = true;

		/// <summary>Opt-in current Corpse rules, frozen when the game is constructed.</summary>
		[field: NonSerialized] // Versioned snapshot header keeps legacy field layout unchanged.
		public bool EnableCorpses { get; set; } = false;

		/// <summary>
		/// Gets or sets a value indicating whether <see cref="Game"/> should skip the Mulligan phase.
		/// </summary>
		/// <value><c>true</c> if Mulligan must be skipped; otherwise, <c>false</c>.</value>
		public bool SkipMulligan { get; set; } = true;

		/// <summary>
		/// A random seed of the random number generator used for
		/// all random interactions within <see cref="Game"/>.
		/// </summary>
		public long? RandomSeed { get; set; }

		// Only schema-2 replay hosts may select this; MemberwiseClone preserves it.
		internal bool LegacyPoolReplay { get; set; }

		/// <summary>
		/// Run-mode rule (Dungeon Run): cards generated during the game (Discover, random cards, random casts) never
		/// include Secrets, whoever generates. An alias of <see cref="Player1ExcludeGeneratedSecrets"/> and
		/// <see cref="Player2ExcludeGeneratedSecrets"/>: setting it sets both, and it reads true only while both are set.
		/// Unlike the run construction inputs above it is live game state, so clones and snapshots keep it.
		/// </summary>
		public bool ExcludeGeneratedSecrets
		{
			get => Player1ExcludeGeneratedSecrets && Player2ExcludeGeneratedSecrets;
			set { Player1ExcludeGeneratedSecrets = value; Player2ExcludeGeneratedSecrets = value; }
		}

		/// <summary>
		/// Run-mode rule: cards generated while player 1 is the generating controller never include Secrets. Live game
		/// state: clones and snapshots keep it.
		/// </summary>
		public bool Player1ExcludeGeneratedSecrets { get; set; }

		/// <summary>
		/// Run-mode rule: cards generated while player 2 (a Dungeon Run boss) is the generating controller never include
		/// Secrets. Live game state: clones and snapshots keep it.
		/// </summary>
		public bool Player2ExcludeGeneratedSecrets { get; set; }

		/// <summary>
		/// True when generation by player <paramref name="generatorPlayer"/> (1, 2, or 0 when unknown) never yields a
		/// Secret. An unknown generator is blocked when either side is, so the filter never lets through a Secret a side
		/// excludes.
		/// </summary>
		internal bool ExcludesGeneratedSecrets(int generatorPlayer) =>
			generatorPlayer == 1 ? Player1ExcludeGeneratedSecrets
			: generatorPlayer == 2 ? Player2ExcludeGeneratedSecrets
			: Player1ExcludeGeneratedSecrets || Player2ExcludeGeneratedSecrets;

		/// <summary>Either side's generation is kept free of Secrets.</summary>
		internal bool ExcludesAnyGeneratedSecrets => Player1ExcludeGeneratedSecrets || Player2ExcludeGeneratedSecrets;

		// Sorted (ordinal) and distinct; null when empty. A plain array keeps the clone and snapshot carry exact. The keys
		// are computed once in the setters, so a filtered-pool cache lookup never joins the lists again.
		private string[] _excludedGeneratedIds, _player1ExcludedGeneratedIds, _player2ExcludedGeneratedIds;
		private string _excludedGeneratedKey = "", _player1ExcludedGeneratedKey = "", _player2ExcludedGeneratedKey = "";

		private static string[] SortedIds(IEnumerable<string> value)
		{
			var ids = value?.Where(id => id != null).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
			return ids == null || ids.Length == 0 ? null : ids;
		}

		private static string KeyOf(string[] ids) => ids == null ? "" : string.Join("|", ids);

		/// <summary>
		/// Run-mode rule (Dungeon Run admission, Generated scope): card ids that generation (Discover, random cards,
		/// random casts, random transforms) never yields in this game, whoever generates. Empty outside run matches. Live
		/// game state like <see cref="ExcludeGeneratedSecrets"/>: clones and snapshots keep it.
		/// </summary>
		public IReadOnlyCollection<string> ExcludedGeneratedIds
		{
			get => _excludedGeneratedIds ?? Array.Empty<string>();
			set { _excludedGeneratedIds = SortedIds(value); _excludedGeneratedKey = KeyOf(_excludedGeneratedIds); }
		}

		/// <summary>
		/// Run-mode rule: card ids generation never yields when player 1 is the generating controller, in addition to
		/// <see cref="ExcludedGeneratedIds"/>. Live game state: clones and snapshots keep it.
		/// </summary>
		public IReadOnlyCollection<string> Player1ExcludedGeneratedIds
		{
			get => _player1ExcludedGeneratedIds ?? Array.Empty<string>();
			set { _player1ExcludedGeneratedIds = SortedIds(value); _player1ExcludedGeneratedKey = KeyOf(_player1ExcludedGeneratedIds); }
		}

		/// <summary>
		/// Run-mode rule: card ids generation never yields when player 2 is the generating controller (a Dungeon Run
		/// boss), in addition to <see cref="ExcludedGeneratedIds"/>. Live game state: clones and snapshots keep it.
		/// </summary>
		public IReadOnlyCollection<string> Player2ExcludedGeneratedIds
		{
			get => _player2ExcludedGeneratedIds ?? Array.Empty<string>();
			set { _player2ExcludedGeneratedIds = SortedIds(value); _player2ExcludedGeneratedKey = KeyOf(_player2ExcludedGeneratedIds); }
		}

		private static bool Contains(string[] ids, string cardId) =>
			ids != null && cardId != null && Array.BinarySearch(ids, cardId, StringComparer.Ordinal) >= 0;

		/// <summary>True when generation by player <paramref name="generatorPlayer"/> (1, 2, or 0 when unknown) may not yield the id.</summary>
		internal bool ExcludesGenerated(string cardId, int generatorPlayer) =>
			Contains(_excludedGeneratedIds, cardId)
			|| generatorPlayer == 1 && Contains(_player1ExcludedGeneratedIds, cardId)
			|| generatorPlayer == 2 && Contains(_player2ExcludedGeneratedIds, cardId);

		internal bool HasExcludedGeneratedIds =>
			_excludedGeneratedIds != null || _player1ExcludedGeneratedIds != null || _player2ExcludedGeneratedIds != null;

		/// <summary>Stable key of the id sets that apply to one generating player, for per-set pool caches.</summary>
		internal string ExcludedGeneratedKey(int generatorPlayer) => _excludedGeneratedKey + "#"
			+ (generatorPlayer == 1 ? "1:" + _player1ExcludedGeneratedKey : generatorPlayer == 2 ? "2:" + _player2ExcludedGeneratedKey : "0:");

		// Replay hosts supply verified historical membership; ordinary games use the installed catalog.
		[field: NonSerialized]
		internal OfflinePoolManifest ReplayPools { get; set; }

		/// <summary>
		/// Instead of shuffling deck and drawing the top card,
		/// players draw a card using random access to the deck.
		/// This allows drawing process to be fully controlled by
		/// the random seed.
		/// Automatically disables <see cref="Shuffle"/>.
		/// </summary>
		public bool DrawWithRandom { get; set; } = false;

		/// <summary>
		/// Clones this instance.
		/// </summary>
		/// <returns></returns>
		public GameConfig Clone()
		{
			return (GameConfig)MemberwiseClone();
		}

	}

	public sealed class GameConfigBuilder
	{
		private int startPlayer = GameConfig.START_PLAYER_DEFAULT;
		private string player1Name = String.Format(GameConfig.PLAYER_NAME_DEFAULT, 1);
		private string player2Name = String.Format(GameConfig.PLAYER_NAME_DEFAULT, 2);
		private CardClass player1HeroClass = CardClass.HUNTER;
		private CardClass player2HeroClass = CardClass.MAGE;
		private Card player1HeroCard;
		private Card player2HeroCard;
		private FormatType formatType = FormatType.FT_STANDARD;
		private List<Card> player1Deck = null;
		private List<Card> player2Deck = null;
		private bool fillDecks = false;
		private bool fillDecksPredictably = false;
		private bool drawPool = false;
		private bool shuffle = true;
		private bool splitting = false;
		private bool logging = true;
		private bool history = true;
		private bool skipMulligan = true;
		private long? randomSeed = null;
		private bool drawWithRandom = false;

		private GameConfigBuilder() { }

		public static GameConfigBuilder Create()
		{
			return new GameConfigBuilder()
			{
				//player1HeroClass =
			};
		}
		public GameConfigBuilder StartPlayer(int startPlayer)
		{
			this.startPlayer = startPlayer;
			return this;
		}
		public GameConfigBuilder SetPlayer1(string playerName, string deckString)
		{
			player1Name = playerName;
			Deck deck = DeckSerializer.Deserialize(deckString);
			Card heroCard = deck.GetHero();
			player1HeroCard = heroCard;
			player1HeroClass = heroCard.Class;
			player1Deck = GetCardList(deck.CardDbfIds);
			formatType = deck.Format;
			return this;
		}
		public GameConfigBuilder SetPlayer1(string playerName, Card playerHeroCard, CardClass playerHeroClass, List<Card> cards, FormatType formatType)
		{
			player1Name = playerName;
			player1HeroCard = playerHeroCard;
			player1HeroClass = playerHeroClass;
			player1Deck = cards;
			this.formatType = formatType;
			return this;
		}
		public GameConfigBuilder SetPlayer2(string playerName, string deckString)
		{
			player2Name = playerName;
			Deck deck = DeckSerializer.Deserialize(deckString);
			Card heroCard = deck.GetHero();
			player2HeroCard = heroCard;
			player2HeroClass = heroCard.Class;
			player2Deck = GetCardList(deck.CardDbfIds);
			formatType = deck.Format;

			return this;
		}
		public GameConfigBuilder SetPlayer2(string playerName, Card playerHeroCard, CardClass playerHeroClass, List<Card> cards, FormatType formatType)
		{
			player2Name = playerName;
			player2HeroCard = playerHeroCard;
			player2HeroClass = playerHeroClass;
			player2Deck = cards;
			this.formatType = formatType;
			return this;
		}
		public GameConfigBuilder FillDecks(bool fillDecks)
		{
			this.fillDecks = fillDecks;
			return this;
		}
		public GameConfigBuilder Shuffle(bool shuffle)
		{
			this.shuffle = shuffle;
			return this;
		}
		public GameConfigBuilder Logging(bool logging)
		{
			this.logging = logging;
			return this;
		}
		public GameConfigBuilder History(bool history)
		{
			this.history = history;
			return this;
		}
		public GameConfigBuilder SkipMulligan(bool skipMulligan)
		{
			this.skipMulligan = skipMulligan;
			return this;
		}
		public GameConfigBuilder RandomSeed(int randomSeed)
		{
			this.randomSeed = randomSeed;
			return this;
		}

		public GameConfigBuilder DrawWithRandom(bool drawWithRandom)
		{
			this.drawWithRandom = drawWithRandom;
			return this;
		}
		public GameConfig Build()
		{
			return new GameConfig()
			{
				StartPlayer = startPlayer,
				Player1Name = player1Name,
				Player2Name = player2Name,
				Player1HeroClass = player1HeroClass,
				Player2HeroClass = player2HeroClass,
				Player1HeroCard = player1HeroCard,
				Player2HeroCard = player2HeroCard,
				FormatType = formatType,
				Player1Deck = player1Deck,
				Player2Deck = player2Deck,
				FillDecks = fillDecks,
				FillDecksPredictably = fillDecksPredictably,
				DrawPool = drawPool,
				Shuffle = shuffle,
				Splitting = splitting,
				Logging = logging,
				History = history,
				SkipMulligan = skipMulligan,
				RandomSeed = randomSeed,
				DrawWithRandom = drawWithRandom
			};
		}
		private List<Card> GetCardList(Dictionary<int, int> cardDbfIds)
		{
			var result = new List<Card>();
			foreach (int key in cardDbfIds.Keys)
			{
				int times = cardDbfIds[key];
				Card card = Cards.FromAssetId(key);
				for(int i = 0; i < times; i++)
				{
					result.Add(card); ;
				}
			}
			return result;
		}

	}
}
