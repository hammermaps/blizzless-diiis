using System;
using System.Collections.Generic;
using System.Linq;
using DiIiS_NA.Core.Logging;
using DiIiS_NA.Core.MPQ;
using DiIiS_NA.Core.Storage;
using DiIiS_NA.Core.Storage.AccountDataBase.Entities;
using DiIiS_NA.GameServer.Core.Types.SNO;
using DiIiS_NA.GameServer.GSSystem.GeneratorsSystem;
using DiIiS_NA.GameServer.GSSystem.ItemsSystem;
using NHibernate.Linq;

namespace DiIiS_NA.Core.Diagnostics
{
	internal static class DebugStartupSelfTests
	{
		private static readonly Logger Logger = LogManager.CreateLogger(nameof(DebugStartupSelfTests));

		public static void RunOrThrow()
		{
			Logger.Info("Running debug startup self-tests...");
			var failures = new List<string>();

			RunCheck("Database", ValidateDatabase, failures);
			RunCheck("Monster spawn", ValidateMonsterSpawns, failures);
			RunCheck("Item drops/items", ValidateItems, failures);

			if (failures.Count > 0)
			{
				foreach (var failure in failures)
					Logger.Error(failure);

				throw new InvalidOperationException(
					$"Debug startup self-tests failed with {failures.Count} issue(s). See logs above.");
			}

			Logger.Success("Debug startup self-tests passed.");
		}

		private static void RunCheck(string name, Action check, List<string> failures)
		{
			try
			{
				check();
				Logger.Success($"Debug self-test '{name}' passed.");
			}
			catch (Exception ex)
			{
				failures.Add($"Debug self-test '{name}' failed: {ex.Message}");
			}
		}

		private static void ValidateDatabase()
		{
			DBSessions.SessionExecute(session =>
			{
				_ = session.Query<DBAccount>().Take(1).ToList();
				return true;
			});

			_ = DBSessions.WorldSession.CreateSQLQuery("SELECT 1").UniqueResult();
		}

		private static void ValidateMonsterSpawns()
		{
			if (SpawnGenerator.Spawns.Count == 0)
				throw new InvalidOperationException("Spawn map is empty.");

			var actorAssets = MPQStorage.Data.Assets[SNOGroup.Actor];
			var invalidSpawnAreas = new List<int>();
			var invalidMonsterIds = new List<int>();
			var missingActors = new List<int>();

			foreach (var spawn in SpawnGenerator.Spawns)
			{
				var layout = spawn.Value;
				if (layout?.Melee == null || layout.Range == null || layout.Dangerous == null)
				{
					invalidSpawnAreas.Add(spawn.Key);
					continue;
				}

				var total = SpawnGenerator.TotalMonsters(spawn.Key);
				if (total.Count == 0)
					invalidSpawnAreas.Add(spawn.Key);

				foreach (var monsterId in total)
				{
					if (monsterId <= 0)
					{
						invalidMonsterIds.Add(monsterId);
						continue;
					}

					if (!actorAssets.ContainsKey(monsterId))
						missingActors.Add(monsterId);
				}
			}

			if (invalidSpawnAreas.Count > 0)
				throw new InvalidOperationException(
					$"Invalid spawn layouts in areas: {string.Join(", ", invalidSpawnAreas.Take(10))}");

			if (missingActors.Count > 0)
			{
				var sample = string.Join(", ", missingActors.Distinct().Take(10));
				throw new InvalidOperationException($"Spawn layouts contain unknown actor ids, e.g.: {sample}");
			}

			if (invalidMonsterIds.Count > 0)
			{
				var sample = string.Join(", ", invalidMonsterIds.Distinct().Take(10));
				throw new InvalidOperationException($"Spawn layouts contain invalid actor ids, e.g.: {sample}");
			}
		}

		private static void ValidateItems()
		{
			if (ItemGenerator.Items.Count == 0)
				throw new InvalidOperationException("No items were loaded.");

			if (ItemGenerator.AllowedItems.Count == 0)
				throw new InvalidOperationException("Allowed item pool is empty.");

			var issues = new List<string>();

			foreach (var pair in ItemGenerator.AllowedItems)
			{
				var definition = pair.Value;
				if (definition == null)
				{
					issues.Add($"Allowed item {pair.Key} has null definition.");
					continue;
				}

				if (!ItemGenerator.Items.ContainsKey(pair.Key))
					issues.Add($"Allowed item {pair.Key} is missing in item registry.");

				if (ItemGenerator.GetItemDefinition(pair.Key) == null)
					issues.Add($"GetItemDefinition failed for allowed item {pair.Key} ({definition.Name}).");

				if (ItemGroup.FromHash(definition.ItemTypesGBID) == null)
					issues.Add($"Item type hash {definition.ItemTypesGBID} not found for {definition.Name}.");

				var itemClass = ItemGenerator.GetItemClass(definition);
				if (itemClass == null || !typeof(Item).IsAssignableFrom(itemClass))
					issues.Add($"Invalid item class mapping for {definition.Name} ({pair.Key}).");

				if (ItemGenerator.GetItemHash(definition.Name) == -1)
					issues.Add($"Item hash lookup failed for {definition.Name}.");
			}

			foreach (var pair in ItemGenerator.AllowedUniqueItems)
			{
				if (!ItemGenerator.Items.ContainsKey(pair.Key))
					issues.Add($"Allowed unique item {pair.Key} is missing in item registry.");
			}

			if (issues.Count > 0)
			{
				var sample = string.Join(" | ", issues.Take(10));
				throw new InvalidOperationException(
					$"Item validation found {issues.Count} issue(s). Sample: {sample}");
			}
		}
	}
}
