using System;
using System.Collections.Generic;
using System.Linq;
using D3.GameBalance;
using DiIiS_NA.Core.Helpers.Hash;
using DiIiS_NA.Core.MPQ;
using DiIiS_NA.Core.Storage;
using DiIiS_NA.Core.Storage.AccountDataBase.Entities;
using DiIiS_NA.Core.Storage.AccountDataBase;
using DiIiS_NA.GameServer.Core.Types.SNO;
using DiIiS_NA.GameServer.Core.Types.TagMap;
using DiIiS_NA.GameServer.GSSystem.ItemsSystem;
using DiIiS_NA.GameServer.MessageSystem;
using DiIiS_NA.GameServer.MessageSystem.Message.Definitions.Base;
using DiIiS_NA.GameServer.MessageSystem.Message.Definitions.Artisan;
using DiIiS_NA.GameServer.MessageSystem.Message.Definitions.Skill;
using DiIiS_NA.LoginServer.Toons;
using NHibernate;
using static DiIiS_NA.Core.MPQ.FileFormats.GameBalance;

namespace DiIiS_NA.GameServer.GSSystem.PlayerSystem
{
	internal static partial class KanaiCube
	{
		// 2.8.0.99920: item eligibility 0x755a10, power resolution 0x75ba20,
		// best-roll direction 0x75bc30. These are item-definition flags, not item-quality flags.
		private const int CannotExtract = 0x1000000;
		private const int LowerPowerRollIsBetter = 0x10000000;

		internal sealed record LegendaryPower(ItemTable Item, int PowerSno, float Value, int Slot, ItemFlags Classes, int ClassRestriction);
		private static readonly Lazy<Dictionary<int, LegendaryPower>> Powers = new(BuildPowers);

		private static Dictionary<int, LegendaryPower> BuildPowers()
		{
			var result = new Dictionary<int, LegendaryPower>();
			var items = MPQStorage.Data.Assets[SNOGroup.GameBalance].Values
				.Select(a => a.Data).OfType<DiIiS_NA.Core.MPQ.FileFormats.GameBalance>()
				.Where(g => g.Item != null).SelectMany(g => g.Item).ToDictionary(i => i.Hash);
			foreach (var item in items.Values)
			{
				if ((item.Flags & 1) == 0 || (item.Flags & CannotExtract) != 0 || item.LegendaryPowerItemReplacement != -1 ||
					!MPQStorage.Data.Assets[SNOGroup.Actor].ContainsKey(item.SNOActor))
					continue;
				var type = ItemGroup.FromHash(item.ItemTypesGBID);
				if (type == null) continue;
				var slot = Item.IsWeapon(type) || Item.IsOffhand(type) ? 0 : Item.IsArmor(type) ? 1 : Item.IsAccessory(type) ? 2 : -1;
				if (slot == -1) continue;
				var effect = item.Attribute.FirstOrDefault(a => a.AttributeId == GameAttributes.Item_Power_Passive.Id);
				// Older legendary powers live in a fixed affix instead of the base item.
				if (effect == null)
					effect = AffixGenerator.FullAffixList
						.Where(a => a.AffixFamily0 != -1 && item.LegendaryAffixFamily.Contains(a.AffixFamily0))
						.SelectMany(a => a.AttributeSpecifier)
						.FirstOrDefault(a => a.AttributeId == GameAttributes.Item_Power_Passive.Id);
				if (effect == null || !MPQStorage.Data.Assets[SNOGroup.Power].ContainsKey(effect.SNOParam)) continue;
				if (!FormulaScript.Evaluate(effect.Formula.ToArray(), null, out var value, out _, out _,
					maximize: (item.Flags & LowerPowerRollIsBetter) == 0))
					throw new InvalidOperationException($"Cannot evaluate cube power for {item.Name}.");
				var tags = ((DiIiS_NA.Core.MPQ.FileFormats.Power)MPQStorage.Data.Assets[SNOGroup.Power][effect.SNOParam].Data).Powerdef.GeneralTagMap;
				var restriction = tags.ContainsKey(PowerKeys.ClassRestriction) ? tags[PowerKeys.ClassRestriction] : -1;
				result.Add(item.Hash, new LegendaryPower(item, effect.SNOParam, value, slot, type.Usable, restriction));
			}
			// The collection stores the terminal item GBID; old versions resolve to that entry.
			foreach (var item in items.Values.Where(i => i.LegendaryPowerItemReplacement != -1 && (i.Flags & CannotExtract) == 0))
			{
				var replacement = item;
				var visited = new HashSet<int>();
				while (replacement.LegendaryPowerItemReplacement != -1)
				{
					if (!visited.Add(replacement.Hash)) throw new InvalidOperationException($"Cyclic cube replacement for {item.Name}.");
					replacement = items[replacement.LegendaryPowerItemReplacement];
				}
				if (result.TryGetValue(replacement.Hash, out var power)) result.Add(item.Hash, power);
			}
			return result;
		}

		internal static LegendaryPower GetPower(int itemGbid) => Powers.Value.GetValueOrDefault(itemGbid);

		private static int Season(Player player) => player.Toon.IsSeasoned ? player.Toon.SeasonCreated : 0;

		private static DBCraft Collection(Player player) => DBSessions.SessionQueryWhere<DBCraft>(c =>
			c.DBGameAccount.Id == player.Toon.GameAccount.PersistentID && c.Artisan == "KanaiCube" &&
			c.isHardcore == player.World.Game.IsHardcore && c.isSeasoned == player.Toon.IsSeasoned && c.Level == Season(player))
			.SingleOrDefault();

		internal static int[] ReadCollection(DBCraft data) => data == null ? Array.Empty<int>() :
			BitPackedGbidArray.ParseFrom(data.LearnedRecipes).ElementsList.ToArray();

		internal static byte[] CollectionBytes(IEnumerable<int> gbids) => BitPackedGbidArray.CreateBuilder()
			.AddRangeElements(gbids).Build().ToByteArray();

		private static int ExtractPower(Player player, TransmuteRecipesTable recipe, Item item)
		{
			if (!player.Inventory.GetBag().Contains(item) || item.DBInventory == null) return ErrorCannotFindItem;
			var power = GetPower(item.GBHandle.GBID);
			if (power == null) return ErrorPowerUnavailable;
			var result = StoreExtractedPower(player.Toon.GameAccount.PersistentID, item.DBInventory.Id,
				player.World.Game.IsHardcore, player.Toon.IsSeasoned, Season(player), recipe, power);
			if (result != Done) return result;
			item.DBInventory = null; // The transaction already deleted it; remove only the in-memory item now.
			player.Inventory.DestroyInventoryItem(item);
			player.Inventory.UpdateCurrencies();
			player.InGameClient.SendMessage(new GenericBlobMessage(Opcodes.ExtractedLegendariedDataPartial)
				{ Data = CollectionBytes(new[] { power.Item.Hash }) });
			player.InGameClient.SendMessage(new TransmuteResultsMessage { annItem = -1, Type = ResultPower,
				GBIDPower = power.Item.Hash, GBIDFakeItem = -1, FakeItemStackCount = -1 });
			return Done;
		}

		internal static int StoreExtractedPower(ulong accountId, ulong itemId, bool hardcore, bool seasonal, int season,
			TransmuteRecipesTable recipe, LegendaryPower power)
		{
			using (var session = SessionProvider.SessionFactory.OpenStatelessSession())
			using (var transaction = session.BeginTransaction())
			{
				// Serialize extraction for this account, and commit the unlock, costs and item deletion together.
				var account = session.Get<DBGameAccount>(accountId, LockMode.Upgrade);
				var source = session.Get<DBInventory>(itemId);
				if (source == null) return ErrorCannotFindItem;
				var collection = session.QueryOver<DBCraft>().Where(c => c.DBGameAccount.Id == accountId &&
					c.Artisan == "KanaiCube" && c.isHardcore == hardcore && c.isSeasoned == seasonal && c.Level == season)
					.SingleOrDefault();
				var learned = ReadCollection(collection);
				if (learned.Any(i => GetPower(i)?.PowerSno == power.PowerSno)) return ErrorPowerAlreadyExtracted;
				if (!SpendExtractionMaterials(account, hardcore, recipe)) return ErrorMissingCoreIngredients;
				var bytes = CollectionBytes(learned.Append(power.Item.Hash));
				if (collection == null)
					session.Insert(new DBCraft { DBGameAccount = account, Artisan = "KanaiCube", isHardcore = hardcore,
						isSeasoned = seasonal, Level = season, LearnedRecipes = bytes });
				else
				{
					collection.LearnedRecipes = bytes;
					session.Update(collection);
				}
				session.Update(account);
				session.Delete(source);
				transaction.Commit();
			}
			return Done;
		}

		internal static bool SpendExtractionMaterials(DBGameAccount account, bool hardcore, TransmuteRecipesTable recipe)
		{
			var costs = recipe.TransmuteRecipeIngredients.Take(recipe.IngredientsCount).Where(i => i.Type == IngredientItemGbid)
				.ToDictionary(i => i.TypeValue, i => i.Quantity);
			var names = new[] { "Crafting_Looted_Reagent_01", "p2_ActBountyReagent_01", "p2_ActBountyReagent_02",
				"p2_ActBountyReagent_03", "p2_ActBountyReagent_04", "p2_ActBountyReagent_05" };
			var values = hardcore
				? new[] { account.HardCraftItem4, account.HardHoradricA1, account.HardHoradricA2, account.HardHoradricA3, account.HardHoradricA4, account.HardHoradricA5 }
				: new[] { account.CraftItem4, account.HoradricA1, account.HoradricA2, account.HoradricA3, account.HoradricA4, account.HoradricA5 };
			for (var i = 0; i < values.Length; i++)
			{
				values[i] -= costs[StringHashHelper.HashItemName(names[i])];
				if (values[i] < 0) return false;
			}
			if (hardcore)
			{
				account.HardCraftItem4 = values[0]; account.HardHoradricA1 = values[1]; account.HardHoradricA2 = values[2];
				account.HardHoradricA3 = values[3]; account.HardHoradricA4 = values[4]; account.HardHoradricA5 = values[5];
			}
			else
			{
				account.CraftItem4 = values[0]; account.HoradricA1 = values[1]; account.HoradricA2 = values[2];
				account.HoradricA3 = values[3]; account.HoradricA4 = values[4]; account.HoradricA5 = values[5];
			}
			return true;
		}

		public static void LoadPowers(Player player)
		{
			player.InGameClient.SendMessage(new GenericBlobMessage(Opcodes.ExtractedLegendariedDataFull)
				{ Data = CollectionBytes(ReadCollection(Collection(player))) });
			player.InGameClient.SendMessage(new SimpleMessage(Opcodes.LoadLegendaryPowerIconsMessage));
		}

		internal static bool CanAssign(LegendaryPower power, ToonClass toonClass, int slot)
		{
			var flag = toonClass switch
			{
				ToonClass.Barbarian => ItemFlags.Barbarian,
				ToonClass.Crusader => ItemFlags.Crusader,
				ToonClass.DemonHunter => ItemFlags.DemonHunter,
				ToonClass.Monk => ItemFlags.Monk,
				ToonClass.Necromancer => ItemFlags.Necromancer,
				ToonClass.WitchDoctor => ItemFlags.WitchDoctor,
				ToonClass.Wizard => ItemFlags.Wizard,
				_ => (ItemFlags)0
			};
			return power != null && power.Slot == slot && (power.Classes & flag) != 0 &&
				(power.ClassRestriction == -1 || power.ClassRestriction == StringHashHelper.HashItemName(toonClass.ToString()));
		}

		public static void ApplyPowers(Player player)
		{
			player.CubedPowerValues.Clear();
			for (var slot = 0; slot < 3; slot++)
			{
				if (player.SkillSet.LegendaryPowers[slot] == -1) continue;
				var power = GetPower(player.SkillSet.LegendaryPowers[slot]);
				if (CanAssign(power, player.Toon.Class, slot)) player.CubedPowerValues[power.PowerSno] = power.Value;
			}
		}

		public static void AssignPower(Player player, AssignLegendaryPowerMessage message)
		{
			var slot = message.Field1;
			if (slot < 0 || slot >= 3) return;
			var gbid = message.SNOSkill; // The wire field is an item GBID, despite the old field name.
			if (gbid != -1)
			{
				var power = GetPower(gbid);
				if (!CanAssign(power, player.Toon.Class, slot) ||
					!ReadCollection(Collection(player)).Any(i => GetPower(i)?.PowerSno == power.PowerSno)) return;
				gbid = power.Item.Hash;
			}
			var skills = player.Toon.DBActiveSkills;
			switch (slot)
			{
				case 0: skills.LegendaryPower0 = gbid; break;
				case 1: skills.LegendaryPower1 = gbid; break;
				case 2: skills.LegendaryPower2 = gbid; break;
			}
			DBSessions.SessionUpdate(skills);
			player.SkillSet.LegendaryPowers[slot] = gbid;
			ApplyPowers(player);
			player.SetAttributesByItemProcs();
			player.UpdateHeroState();
		}
	}
}
