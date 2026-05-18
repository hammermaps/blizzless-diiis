using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using DiIiS_NA.Core.Helpers.Hash;
using DiIiS_NA.Core.Helpers.Math;
using DiIiS_NA.Core.Logging;
using DiIiS_NA.Core.MPQ.FileFormats;
using DiIiS_NA.D3_GameServer.Core.Types.SNO;
using DiIiS_NA.GameServer.GSSystem.PlayerSystem;
using DiIiS_NA.GameServer.MessageSystem;
using DiIiS_NA.LoginServer.Toons;
using static DiIiS_NA.Core.MPQ.FileFormats.GameBalance;

namespace DiIiS_NA.GameServer.GSSystem.ItemsSystem
{
	public static class Season27Patch
	{
		private static readonly Logger Logger = LogManager.CreateLogger(nameof(Season27Patch));
		// Uses an otherwise unused high Item_LegendaryItem_Level_Override range to persist the selected sanctified power.
		private const float SanctifiedMarkerBase = 270000f;
		private const int PrimalAncientRank = 2;
		private const int LevelRequirementAttributeKey = 57;
		// Patch 2.7.4 retains 17% of Echoing Nightmare experience rewards (83% reduction).
		private const float EchoingNightmareExperienceMultiplier = 0.17f;
		public const float AngelicCrucibleDropChancePercent = 1f;
		// Minimum meaningful damage cap value – weapon types not in the lookup table return (0, 0).
		private const float MinWeaponDamageCap = 5f;
		private const int ShieldItemTypesGBID = 332825721;
		private const int CrusaderShieldItemTypesGBID = 602099538;
		private static readonly ConcurrentDictionary<WorldSno, bool> EchoingNightmareWorldCache = new();

		private static readonly string[] AngelicCrucibleNames =
		{
			"AngelicCrucible",
			"Angelic_Crucible",
			"p73_AngelicCrucible",
			"p73_Angelic_Crucible",
			"Consumable_Add_Sockets_Season_27"
		};

		public static bool IsAngelicCrucible(Item item)
		{
			if (item == null) return false;

			return AngelicCrucibleNames.Any(name => item.GBHandle.GBID == StringHashHelper.HashItemName(name)) ||
			       (item.ItemDefinition.Name.Contains("Angelic", StringComparison.OrdinalIgnoreCase) &&
			        item.ItemDefinition.Name.Contains("Crucible", StringComparison.OrdinalIgnoreCase));
		}

		public static Item CreateAngelicCrucible(Player player)
		{
			foreach (var name in AngelicCrucibleNames)
			{
				var definition = ItemGenerator.GetItemDefinition(StringHashHelper.HashItemName(name));
				if (definition == null) continue;

				return ItemGenerator.CookFromDefinition(player.World, definition);
			}

			return null;
		}

		public static bool TryUseAngelicCrucible(Player player, Item crucible, Item target)
		{
			if (!IsAngelicCrucible(crucible)) return false;
			if (!CanSanctify(player, target))
			{
				Logger.Debug("Rejected sanctify request for item {0}.", target?.ItemDefinition?.Name ?? "<null>");
				return true;
			}

			target.Attributes[GameAttributes.Ancient_Rank] = PrimalAncientRank;
			target.Attributes[GameAttributes.Item_Was_Primalized] = true;
			target.Attributes[GameAttributes.Item_LegendaryItem_Level_Override] = SanctifiedMarkerBase + GetRandomSanctifiedPower(player);
			target.Attributes[GameAttributes.Requirement, LevelRequirementAttributeKey] = 70;
			target.Attributes[GameAttributes.Item_Level_Requirement_Override] = 70;
			player.Attributes[GameAttributes.Sanctified_items_unlocked] = true;

			AffixGenerator.MaxRollAffixAttributes(target);
			ForceMaxBaseStats(target);

			target.Unidentified = false;
			target.Attributes.BroadcastChangedIfRevealed();
			ConsumeCrucible(player, crucible);
			PersistItem(player, target);
			player.SetAttributesByItems();

			return true;
		}

		public static bool IsSanctified(Item item) =>
			item != null &&
			item.Attributes[GameAttributes.Item_Was_Primalized] &&
			item.Attributes[GameAttributes.Item_LegendaryItem_Level_Override] >= SanctifiedMarkerBase;

		public static bool HasEquippedSanctifiedItem(Player player, Item except = null) =>
			player.Inventory.GetEquippedItems().Any(item => item != except && IsSanctified(item));

		public static void ApplyEquippedSanctifiedBonus(Player player)
		{
			var item = player.Inventory.GetEquippedItems().FirstOrDefault(IsSanctified);
			if (item == null) return;

			switch (GetSanctifiedPower(item) % 3)
			{
				case 1:
					player.Attributes[GameAttributes.Damage_Weapon_Percent_Bonus] += 0.25f;
					break;
				case 2:
					player.Attributes[GameAttributes.Resource_Cost_Reduction_Percent_All] += 0.15f;
					break;
				default:
					player.Attributes[GameAttributes.Movement_Scalar_Uncapped_Bonus] += 0.25f;
					break;
			}
		}

		public static int GetEchoingNightmareExperience(int experience, WorldSno worldSno)
		{
			if (!IsEchoingNightmareWorld(worldSno)) return experience;
			return (int)Math.Min(int.MaxValue, (long)experience * EchoingNightmareExperienceMultiplier);
		}

		private static bool CanSanctify(Player player, Item item)
		{
			if (player?.World?.Game == null || item == null) return false;
			if (!player.World.Game.IsSeasoned) return false;
			if (player.Level < 70) return false;
			if (item.Attributes[GameAttributes.IsCrafted]) return false;
			if (item.Attributes[GameAttributes.Item_Equipped]) return false;
			if (item.ItemDefinition.RequiredLevel > 0 && item.ItemDefinition.RequiredLevel < 70) return false;
			if (!IsValidSanctifiableItemType(item)) return false;

			return item.Attributes[GameAttributes.Item_Quality_Level] >= 9 ||
			       item.ItemDefinition.Quality is ItemTable.ItemQuality.Legendary or ItemTable.ItemQuality.Set or ItemTable.ItemQuality.Special ||
			       item.ItemDefinition.Name.Contains("Unique_", StringComparison.OrdinalIgnoreCase) ||
			       item.ItemDefinition.Name.Contains("_Set_", StringComparison.OrdinalIgnoreCase);
		}

		private static bool IsValidSanctifiableItemType(Item item) =>
			Item.IsArmor(item.ItemType) ||
			Item.IsWeapon(item.ItemType) ||
			Item.IsOffhand(item.ItemType) ||
			Item.IsAccessory(item.ItemType);

		private static int GetRandomSanctifiedPower(Player player)
		{
			var classOffset = player.Toon.Class switch
			{
				ToonClass.Barbarian => 0,
				ToonClass.Crusader => 3,
				ToonClass.DemonHunter => 6,
				ToonClass.Monk => 9,
				ToonClass.Necromancer => 12,
				ToonClass.WitchDoctor => 15,
				ToonClass.Wizard => 18,
				_ => 0
			};

			return classOffset + FastRandom.Instance.Next(0, 3);
		}

		private static int GetSanctifiedPower(Item item)
		{
			var marker = item.Attributes[GameAttributes.Item_LegendaryItem_Level_Override];
			return marker >= SanctifiedMarkerBase ? (int)(marker - SanctifiedMarkerBase) : -1;
		}

		private static void ConsumeCrucible(Player player, Item crucible)
		{
			if (crucible.Attributes[GameAttributes.ItemStackQuantityLo] > 1)
			{
				crucible.UpdateStackCount(crucible.Attributes[GameAttributes.ItemStackQuantityLo] - 1);
				crucible.Attributes.SendChangedMessage(player.InGameClient);
				PersistItem(player, crucible);
				return;
			}

			player.Inventory.DestroyInventoryItem(crucible);
		}

		private static void PersistItem(Player player, Item item)
		{
			if (item.DBInventory == null) return;

			ItemGenerator.SaveToDB(item);
			player.World.Game.GameDbSession.SessionUpdate(item.DBInventory);
		}

		private static bool IsEchoingNightmareWorld(WorldSno worldSno)
		{
			return EchoingNightmareWorldCache.GetOrAdd(worldSno, sno =>
			{
				var name = sno.ToString();
				return name.Contains("echoing", StringComparison.OrdinalIgnoreCase) &&
				       name.Contains("nightmare", StringComparison.OrdinalIgnoreCase);
			});
		}

		private static void ForceMaxBaseStats(Item item)
		{
			if (Item.IsWeapon(item.ItemType))
			{
				var (capMin, capDelta) = Item.GetWeaponDamageCaps(item.ItemDefinition.ItemTypesGBID);
				if (capMin > MinWeaponDamageCap && capDelta > MinWeaponDamageCap)
				{
					item.Attributes[GameAttributes.Damage_Weapon_Min, 0] = capMin;
					item.Attributes[GameAttributes.Damage_Weapon_Delta, 0] = capDelta;
				}
			}
			else if (item.ItemDefinition.ItemTypesGBID == ShieldItemTypesGBID ||
			         item.ItemDefinition.ItemTypesGBID == CrusaderShieldItemTypesGBID)
			{
				item.Attributes[GameAttributes.Block_Amount_Item_Min] = 14000f;
				item.Attributes[GameAttributes.Block_Amount_Item_Delta] = 7000f;
			}
		}
	}
}
