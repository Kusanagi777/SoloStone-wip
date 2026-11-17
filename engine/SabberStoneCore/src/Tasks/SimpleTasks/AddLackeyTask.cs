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

using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Tasks.SimpleTasks
{
	public class AddLackeyTask : SimpleTask
	{
		public static readonly Card[]
			Lackeys = LegacyGenerationV1.Get("Lackeys").ToArray();

		private readonly int _amount;
		public AddLackeyTask(int amount)
		{
			_amount = amount;
		}

		public override TaskState Process(in Game game, in Controller controller, in IEntity source, in IPlayable target,
			in TaskStack stack = null)
		{
			game.OnRandomHappened(true);

			// A run match never hands out a Lackey its generation filter blocks (step 9, generation hygiene); every other game
			// draws from the same seven as before.
			Card[] lackeys = OfflineCardPools.ForGame(game, Lackeys);
			if (lackeys.Length == 0) return TaskState.COMPLETE;

			for (int i = 0; i < _amount && !controller.HandZone.IsFull; i++)
				Entity.FromCard(in controller, lackeys.Choose(game.Random), zone: controller.HandZone);

			return TaskState.COMPLETE;
		}
	}
}
