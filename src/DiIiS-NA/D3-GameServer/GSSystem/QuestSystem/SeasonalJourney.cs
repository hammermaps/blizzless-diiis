using System.Collections.Generic;
using DiIiS_NA.Core.Logging;
using DiIiS_NA.GameServer.GSSystem.ItemsSystem;
using DiIiS_NA.GameServer.GSSystem.PlayerSystem;
using DiIiS_NA.LoginServer.Toons;

namespace DiIiS_NA.GameServer.GSSystem.QuestSystem
{
	public static class SeasonalJourney
	{
		private static readonly Logger Logger = LogManager.CreateLogger();

		private static readonly Dictionary<ulong, int> CriteriaChapters = new()
		{
			{ 3367569, 1 },
			{ 74987258781748, 2 },
			{ 74987258962046, 3 },
			{ 74987249642121, 4 }
		};

		private static readonly Dictionary<ToonClass, string[][]> HaedrigRewards = new()
		{
			{
				ToonClass.Barbarian, new[]
				{
					new[] { "Unique_Helm_Set_15_x1", "Unique_Gloves_Set_15_x1" },
					new[] { "Unique_Chest_Set_15_x1", "Unique_Pants_Set_15_x1" },
					new[] { "Unique_Boots_Set_15_x1", "Unique_Shoulder_Set_15_x1" }
				}
			},
			{
				ToonClass.Crusader, new[]
				{
					new[] { "Unique_Helm_Set_12_x1", "Unique_Gloves_Set_12_x1" },
					new[] { "Unique_Chest_Set_12_x1", "Unique_Pants_Set_12_x1" },
					new[] { "Unique_Boots_Set_12_x1", "Unique_Shoulder_Set_12_x1" }
				}
			},
			{
				ToonClass.DemonHunter, new[]
				{
					new[] { "Unique_Helm_Set_14_x1", "Unique_Gloves_Set_14_x1" },
					new[] { "Unique_Chest_Set_14_x1", "Unique_Pants_Set_14_x1" },
					new[] { "Unique_Boots_Set_14_x1", "Unique_Shoulder_Set_14_x1" }
				}
			},
			{
				ToonClass.Monk, new[]
				{
					new[] { "Unique_Helm_Set_11_x1", "Unique_Gloves_Set_11_x1" },
					new[] { "Unique_Chest_Set_11_x1", "Unique_Pants_Set_11_x1" },
					new[] { "Unique_Boots_Set_11_x1", "Unique_Shoulder_Set_11_x1" }
				}
			},
			{
				ToonClass.Necromancer, new[]
				{
					new[] { "P6_Necro_Set_3_Helm", "P6_Necro_Set_3_Gloves" },
					new[] { "P6_Necro_Set_3_Chest", "P6_Necro_Set_3_Pants" },
					new[] { "P6_Necro_Set_3_Boots", "P6_Necro_Set_3_Shoulders" }
				}
			},
			{
				ToonClass.WitchDoctor, new[]
				{
					new[] { "Unique_Helm_Set_09_x1", "Unique_Gloves_Set_09_x1" },
					new[] { "Unique_Chest_Set_09_x1", "Unique_Pants_Set_09_x1" },
					new[] { "Unique_Boots_Set_09_x1", "Unique_Shoulder_Set_09_x1" }
				}
			},
			{
				ToonClass.Wizard, new[]
				{
					new[] { "Unique_Helm_Set_06_x1", "Unique_Gloves_Set_06_x1" },
					new[] { "Unique_Chest_Set_06_x1", "Unique_Pants_Set_06_x1" },
					new[] { "Unique_Boots_Set_06_x1", "Unique_Shoulder_Set_06_x1" }
				}
			}
		};

		public static void OnCriteriaGranted(Player player, ulong criteriaId)
		{
			if (player?.Toon?.IsSeasoned != true) return;
			if (!CriteriaChapters.TryGetValue(criteriaId, out var chapter)) return;

			var toon = player.Toon.DBToon;
			var chapterBit = 1 << (chapter - 1);

			lock (toon)
			{
				if ((toon.SeasonalJourneyChaptersCompleted & chapterBit) != 0) return;

				toon.SeasonalJourneyChaptersCompleted |= chapterBit;
				toon.SeasonalJourneyLastUpdated = toon.CreatedSeason;
				player.InGameClient.Game.GameDbSession.SessionUpdate(toon);
			}

			if (chapter >= 2)
				GrantHaedrigReward(player, chapter);
		}

		private static void GrantHaedrigReward(Player player, int chapter)
		{
			if (!HaedrigRewards.TryGetValue(player.Toon.Class, out var chapterRewards)) return;

			var rewardIndex = chapter - 2;
			if (rewardIndex < 0 || rewardIndex >= chapterRewards.Length) return;

			foreach (var itemName in chapterRewards[rewardIndex])
			{
				var item = ItemGenerator.TryCook(player, itemName);
				if (item == null)
				{
					Logger.Warn($"Seasonal Journey reward item {itemName} not found.");
					continue;
				}

				player.Inventory.PickUp(item);
			}
		}
	}
}
