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
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;

namespace SabberStoneCore.Model.Entities
{
	/// <summary>
	/// Entity representing the special ability for a hero.
	/// </summary>
	/// <seealso cref="Playable" />
	public class HeroPower : Playable
	{
		/// <summary>Initializes a new instance of the <see cref="HeroPower"/> class.</summary>
		/// <param name="controller">The controller.</param>
		/// <param name="card">The card.</param>
		/// <param name="tags">The tags.</param>
		public HeroPower(in Controller controller, in Card card, in IDictionary<GameTag, int> tags, in int id = -1)
			: base(in controller, in card, in tags, in id)
		{
			Game.Log(LogLevel.VERBOSE, BlockType.PLAY, "HeroPower", !Game.Logging? "":$"{this} ({ Card.Class}) was created.");
		}

		/// <summary>
		/// A copy constructor.
		/// </summary>
		/// <param name="controller">The target <see cref="T:SabberStoneCore.Model.Entities.Controller" /> instance.</param>
		/// <param name="heroPower">The source <see cref="T:SabberStoneCore.Model.Entities.HeroPower" />.</param>
		private HeroPower(in Controller controller, in Playable heroPower) : base(in controller, in heroPower) { }

		/// <summary>Targetings the requirements.</summary>
		/// <param name="target">The target.</param>
		/// <returns></returns>
		public override bool TargetingRequirements(ICharacter target)
		{
			return !target.CantBeTargetedByHeroPowers && base.TargetingRequirements(target);
		}

		/// <summary>
		/// Gets a value indicating whether this entity is playable by the player. Some entities require specific
		/// requirements before they can be played. This method will process the requirements and produce
		/// a result for the current state of the game.
		/// </summary>
		/// <value><c>true</c> if this entity is playable; otherwise, <c>false</c>.</value>
		public override bool IsPlayableByPlayer =>
			System.Linq.Enumerable.Contains(Controller.HeroPowers, this) && !IsExhausted && !Controller.HeroPowerDisabled && base.IsPlayableByPlayer && !IsPassiveHeroPower;

		public bool IsPassiveHeroPower => Card.HideStat || PassiveIds.Contains(Card.Id)
			// Phase 8: an existing client passive a Dungeon Run boss starts with (GIL_504h Bewitch), once EncounterRules started its trigger.
			|| this[Actions.EncounterRules.PowerActive] != 0 && CardSets.DungeonRun.DungeonRunEncounters.IsExistingPassive(Card.Id);

		/// <summary>Hero powers that are passive without the HIDE_STATS tag: the client-era ones plus
		/// the Dungeon Run boss passives registered in <see cref="CardSets.DungeonRun.DungeonRunCardsGen"/>.</summary>
		private static readonly HashSet<string> PassiveIds = new HashSet<string>(
			new[] { "BE_004p", "JAIL_800hp1", "JAIL_800hp2" }.Concat(CardSets.DungeonRun.DungeonRunCardsGen.PassiveHeroPowers));

		/// <summary>True for a passive hero power that runs its power once at the start of the game
		/// when a hero starts with it (Dungeon Run boss passives). Other passives activate only when their hero card is played.</summary>
		public static bool ActivatesAtGameStart(Card power) =>
			power != null && CardSets.DungeonRun.DungeonRunCardsGen.PassiveHeroPowers.Contains(power.Id);

		public override IPlayable Clone(in Controller controller)
		{
			return new HeroPower(in controller, this);
		}
	}
}
