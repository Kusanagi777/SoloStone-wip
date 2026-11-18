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
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Tasks.SimpleTasks
{
	public class RandomMinionNumberTask : SimpleTask
	{
		public RandomMinionNumberTask(GameTag tag)
		{
			Tag = tag;
		}

		public GameTag Tag { get; set; }

		public override TaskState Process(in Game game, in Controller controller, in IEntity source,
			in IPlayable target,
			in TaskStack stack = null)
		{
			IReadOnlyList<Card> cardsList;
			if (Tag == GameTag.COST)
			{
				Cards.CostMinionCards(game.FormatType).TryGetValue(stack.Number, out List<Card> costCards);
				// A run match never generates a card its generation filter blocks (step 9, generation hygiene).
				cardsList = OfflineCardPools.ForGame(game, costCards);
				if (cardsList == null || cardsList.Count == 0)
					return TaskState.STOP;
			}
			else
			{
				IEnumerable<Card> cards = game.FormatType == FormatType.FT_STANDARD ? Cards.AllStandard : Cards.AllWild;
				int num = stack.Number;
				Game self = game;
				cardsList = cards.Where(p => p.Type == CardType.MINION && p[Tag] == num && !(self.FiltersGenerated && self.BlocksGenerated(p))).ToList();
				if (!cardsList.Any())
					return TaskState.STOP;
			}

			IPlayable playable = Entity.FromCard(controller, cardsList.Choose(game.Random));
			stack.Playables = new List<IPlayable> {playable};

			game.OnRandomHappened(true);

			return TaskState.COMPLETE;
		}
	}
}
