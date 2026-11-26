using System;
using System.Collections.Generic;
using System.Linq;
using DiIiS_NA.LoginServer.Toons;
using DiIiS_NA.Core.Extensions;
using DiIiS_NA.Core.Helpers.Math;
using DiIiS_NA.Core.Logging;
using DiIiS_NA.Core.MPQ;
using DiIiS_NA.Core.MPQ.FileFormats;
using DiIiS_NA.GameServer.Core.Types.SNO;
using DiIiS_NA.GameServer.MessageSystem;
using static DiIiS_NA.Core.MPQ.FileFormats.GameBalance;

namespace DiIiS_NA.GameServer.GSSystem.ItemsSystem
{
	static class AffixGenerator
	{
		public static readonly Logger Logger = LogManager.CreateLogger();

		public static List<AffixTable> AllAffix = new List<AffixTable>();
		public static List<AffixTable> FullAffixList = new List<AffixTable>();

		public static List<AffixTable> AffixList = new List<AffixTable>();

		private static List<AffixTable> LegendaryAffixList = new List<AffixTable>();

		/// <summary>
		/// The class the item being created is for (Smart Loot), or null for none. Its affixes are that class's, as on
		/// live: its primary stat, resource and skill bonuses. Set around item creation by
		/// <see cref="ItemGenerator.CreateItemForClass"/>.
		/// </summary>
		[ThreadStatic] internal static ToonClass? OwnerClass;

		/// <summary>
		/// The Ancient_Rank a level-70 legendary or set item being created gets (0 normal, 1 ancient), or null to roll it
		/// (<see cref="AncientChance"/>). Set around item creation, as for <see cref="OwnerClass"/>, by !item.
		/// </summary>
		[ThreadStatic] internal static int? ForcedAncientRank;

		// Live: 10% of level-70 legendary and set items are ancient, from gambling, crafting and the cube and from drops on
		// Torment VI and up. Lower Torment levels are said to have lower odds, which no source gives.
		private const double AncientChance = 0.1;

		// Every affix by GBID, including those AllAffix leaves out (the 21x_ ancient tiers).
		private static readonly Dictionary<int, AffixTable> AffixesByHash = new();

		/// <summary>
		/// The tier a legendary rolls instead of <paramref name="affix"/>: as on live, a legendary or set item takes the
		/// tier its level covers and moves it up once (LegendaryUprankAffix), and an ancient one twice. A level-70 pair of
		/// boots rolls Intelligence 416-500 (1xx_Int 17 Secondary) and an ancient one 550-650 (1xx_Int 18 Secondary),
		/// rather than the level-70 tier's 350-415. Tiers that don't move up (percentages, such as critical hit chance)
		/// stay as they are, as an ancient's do on live.
		/// </summary>
		private static AffixTable Upranked(AffixTable affix, int times)
		{
			for (int i = 0; i < times && AffixesByHash.TryGetValue(affix.LegendaryUprankAffix, out var higher); i++)
				affix = higher;
			return affix;
		}

		private static readonly (ToonClass Toon, ItemFlags Flag, Class Class)[] Classes =
		{
			(ToonClass.DemonHunter, ItemFlags.DemonHunter, Class.DemonHunter),
			(ToonClass.Barbarian, ItemFlags.Barbarian, Class.Barbarian),
			(ToonClass.Wizard, ItemFlags.Wizard, Class.Wizard),
			(ToonClass.WitchDoctor, ItemFlags.WitchDoctor, Class.Witchdoctor),
			(ToonClass.Monk, ItemFlags.Monk, Class.Monk),
			(ToonClass.Crusader, ItemFlags.Crusader, Class.Crusader),
			(ToonClass.Necromancer, ItemFlags.Necromancer, Class.Necromancer)
		};

		/// <summary>
		/// The class whose affixes an item rolls: the one it's made for (<see cref="OwnerClass"/>), or else the only class
		/// that can use its type (a wand), or none.
		/// </summary>
		private static Class AffixClass(Item item)
		{
			if (OwnerClass is { } owner)
				return Classes.Where(c => c.Toon == owner).Select(c => c.Class).DefaultIfEmpty(Class.None).First();
			var users = Classes.Where(c => item.ItemType.Usable.HasFlag(c.Flag)).ToList();
			return users.Count == 1 ? users[0].Class : Class.None;
		}

		/// <summary>
		/// Whether an affix rolls for a class: the affix's own class if it has one (skill bonuses, resources), and the
		/// class's flag for it (Intelligence has none for a Barbarian). With no class, only affixes of no class roll.
		/// </summary>
		private static bool RollsFor(AffixTable affix, Class affixClass)
		{
			if (affixClass == Class.None)
				return affix.PlayerClass == Class.None;
			if (affix.PlayerClass != Class.None && affix.PlayerClass != affixClass)
				return false;
			return affixClass switch
			{
				Class.DemonHunter => affix.DemonHunterFrequency,
				Class.Barbarian => affix.BarbarianFrequency,
				Class.Wizard => affix.WizardFrequency,
				Class.Witchdoctor => affix.WitchDoctorFrequency,
				Class.Monk => affix.MonkFrequency,
				Class.Crusader => affix.CrusaderFrequency,
				_ => affix.NecromancerFrequency
			} > 0;
		}

		// An affix's tiers each cover a range of item levels (a level 70 item rolls the 70-70 tier).
		private static bool CoversLevel(AffixTable affix, int level) => affix.AffixLevelMin <= level && level <= affix.AffixLevelMax;

		private const int SecondaryAffixGroup = 1; // AffixTable.AffixGroup: 0 for primary affixes, 1 for secondary ones

		/// <summary>
		/// Up to <paramref name="count"/> affixes from <paramref name="pool"/>, chosen by their Frequency as on live: one of
		/// each family (AffixFamily0), at most one of each wider family (AffixFamily1, such as the primary stats), and none
		/// of the families in <paramref name="taken"/>, which gains the families chosen.
		/// </summary>
		private static List<AffixTable> Roll(IEnumerable<AffixTable> pool, int count, HashSet<int> taken)
		{
			var rolled = new List<AffixTable>();
			// a weighted shuffle: each affix comes first with a chance in proportion to its Frequency
			foreach (var affix in pool.Where(a => a.Frequency > 0)
				         .OrderByDescending(a => Math.Pow(RandomHelper.NextDouble(), 1.0 / a.Frequency)))
			{
				if (rolled.Count >= count)
					break;
				if (taken.Contains(affix.AffixFamily0) || (affix.AffixFamily1 != -1 && taken.Contains(affix.AffixFamily1)))
					continue;
				rolled.Add(affix);
				taken.Add(affix.AffixFamily0);
				if (affix.AffixFamily1 != -1)
					taken.Add(affix.AffixFamily1);
			}
			return rolled;
		}

		static AffixGenerator()
		{
			foreach (var asset in MPQStorage.Data.Assets[SNOGroup.GameBalance].Values)
			{
				GameBalance data = asset.Data as GameBalance;
				if (data != null && data.Type == BalanceType.AffixList)
				{
					foreach (var affixDef in data.Affixes)
					{
						FullAffixList.Add(affixDef);
						AffixesByHash.TryAdd(affixDef.Hash, affixDef);

						if (affixDef.I0 > 8)
							affixDef.I0 -= 8;
						if (affixDef.I0 > 4)
							affixDef.I0 -= 4; //hacks for RoS affixes

						if (affixDef.AffixFamily0 == -1)
							continue;
						if (affixDef.Name.Contains("REQ"))
						{ continue; }// crashes the client // dark0ne

						if (!(affixDef.Name.StartsWith("1xx_") || affixDef.Name.StartsWith("X1_") ||
							affixDef.Name.StartsWith("CriticalD")))
							continue; //skip obsolete affixes except the old Critical Damage

						if (affixDef.Name.Contains("DamConversionHeal")) continue; // not in game
						if (affixDef.Name.Contains("CrushingBlow")) continue; // not in game
						if (affixDef.Name.Contains("LifeS")) continue; // not in game
						if (affixDef.Name.Contains("1xx_Inferior")) continue;
						//!a.Name.Contains("")

						if (affixDef.AttributeSpecifier[0].AttributeId == 50) continue; // Armor_Item_Percent not in game

						if (affixDef.Name.Contains("CriticalD") && (affixDef.AffixLevel == 63)) //add level 70 weapon chd
							affixDef.AffixLevelMax = 73; //same for 1H,2H and secondary affix

						if (affixDef.Name.Contains("Run") && (affixDef.AffixLevel == 71))   //add level 70 movement speed
							affixDef.AffixLevelMax = 73;

						if (affixDef.Name.Contains("Sockets") &&
							(affixDef.AffixLevel == 53 || affixDef.AffixLevel == 60)) //add level 70 sockets
							affixDef.AffixLevelMax = 73;

						if (affixDef.Name.Contains("Sockets Chest")) // hack for chest socket
							affixDef.I0 = 1;

						if (affixDef.Name.Contains("Sockets Helm V")) // hack for weapon socket
						{
							affixDef.ItemGroup[1] = 485534122;
						}

						if (affixDef.Name.Contains("Sockets Bracer")) // hack for legs socket
						{
							affixDef.ItemGroup[0] = 3994699;
						}

						if (affixDef.Name.Contains("Sockets Shield")) // hack for shield socket
						{
							affixDef.ItemGroup[0] = 1440677334;
						}

						if (affixDef.Name.Contains("Kings")) // hack for ApS
							affixDef.I0 = 2;

						AllAffix.Add(affixDef);

						if (affixDef.I0 == 2 || affixDef.I0 == 3)
							LegendaryAffixList.Add(affixDef);

						if (affixDef.AffixLevel > (74) && !affixDef.Name.EndsWith("_Jewel")) continue;

						if (affixDef.I0 == 1 || affixDef.I0 == 3)
							AffixList.Add(affixDef);
					}

				}
			}
		}

		public static void Generate(Item item, int affixesCount, bool isCrafting = false)
		{
			if (!Item.IsWeapon(item.ItemType) &&
				!Item.IsArmor(item.ItemType) &&
				!Item.IsOffhand(item.ItemType) &&
				!Item.IsShard(item.ItemType) &&
				!Item.IsAccessory(item.ItemType))
				return;

			if (Item.IsAccessory(item.ItemType) && affixesCount <= 1) affixesCount = 2;

			// Legendaries and set items (some have no "Unique_" in their names, like P6_Necro_Set_1_Boots)
			bool isUnique = item.ItemDefinition.Name.Contains("Unique_") || item.Attributes[GameAttributes.Item_Quality_Level] >= 9;
			bool isEthereal = item.ItemDefinition.Name.ToLower().Contains("p71_ethereal");

			// As on live, a legendary rolls its fixed affixes (LegendaryAffixFamily), then as many random primary and
			// secondary ones as its record says.
			int primaryCount = 0, secondaryCount = 0;
			if (isUnique && !isCrafting && !isEthereal)
			{
				primaryCount = item.ItemDefinition.BonusMajorAffixes;
				secondaryCount = item.ItemDefinition.BonusMinorAffixes;
				affixesCount = item.ItemDefinition.BonusAffixes;
			}

			if (isEthereal)
			{
				affixesCount = 8;
				isUnique = true;
			}

			// Ancient (Ancient_Rank, which the client shows), and how far its tiers move up (see Upranked)
			if (isUnique && !isEthereal && item.ItemLevel >= 70)
				item.Attributes[GameAttributes.Ancient_Rank] =
					ForcedAncientRank ?? (RandomHelper.NextDouble() < AncientChance ? 1 : 0);
			int upranks = !isUnique ? 0 : item.Attributes[GameAttributes.Ancient_Rank] > 0 ? 2 : 1;


			if (item.GBHandle.GBID == -4139386) affixesCount = 6; //referral ring

			Class itemPlayerClass = AffixClass(item);


			List<int> itemTypes = ItemGroup.HierarchyToHashList(item.ItemType);
			//itemGroups.Add(ItemGroup.GetRootType(item.ItemType));
			int levelToFind = Math.Clamp(item.ItemLevel, 1, 70);
			if (item.GBHandle.GBID == -4139386) levelToFind = 10;
			if (itemTypes[0] == 828360981)
				itemTypes.Add(-1028103400);
			else if (itemTypes[0] == -947867741)
				itemTypes.Add(3851110);
			else if (itemTypes[0] == 110504)
				itemTypes.Add(395678127);
			else if (itemTypes[0] == 327487312)
			{ itemTypes.Add(140775496); itemTypes.Add(133016072); itemTypes.Add(485534122); }

			if (item.ItemDefinition.Name.ToLower().Contains("p71_ethereal"))
			{
				itemTypes.Add(133016072);
				itemTypes.Add(133016072);
			}
			IEnumerable<AffixTable> filteredList = null;

			filteredList = AllAffix.Where(a =>
				RollsFor(a, itemPlayerClass) &&
				itemTypes.ContainsAtLeastOne(a.ItemGroup) &&
				CoversLevel(a, levelToFind) &&
				(a.OverrideLevelReq <= item.ItemDefinition.RequiredLevel)
				//!a.Name.Contains("1xx_Inferior")
				);

			// Families the item already has, which random affixes don't repeat
			var taken = new HashSet<int>(item.AffixFamilies);
			if (isUnique)
			{
				var restrictedFamily = item.ItemDefinition.LegendaryAffixFamily.Where(af => af != -1).ToHashSet();
				for (int i = 0; i < 6; i++)
				{
					var fixedAffix = FindSuitableAffix(item, item.ItemDefinition.LegendaryAffixFamily[i], item.ItemDefinition.MaxAffixLevel[i], (item.ItemDefinition.I38[i] == 1));
					if (AffixesByHash.TryGetValue(fixedAffix, out var found))
						fixedAffix = Upranked(found, upranks).Hash;
					AddAffix(item, fixedAffix, true);
					if (AffixesByHash.TryGetValue(fixedAffix, out var added))
					{
						taken.Add(added.AffixFamily0);
						if (added.AffixFamily1 != -1)
							taken.Add(added.AffixFamily1);
					}
				}
				taken.UnionWith(restrictedFamily);

				if (restrictedFamily.Contains(1616088365) ||
				    restrictedFamily.Contains(-1461069734) ||
				    restrictedFamily.Contains(234326220) ||
				    restrictedFamily.Contains(1350281776) ||
				    restrictedFamily.Contains(-812845450) ||
				    restrictedFamily.Contains(1791554648) ||
				    restrictedFamily.Contains(125900958)) //MinMaxDam and ele damage
				{
					filteredList = filteredList
						.Where(a => !a.Name.Contains("FireD") &&
						            !a.Name.Contains("PoisonD") &&
						            !a.Name.Contains("HolyD") &&
						            !a.Name.Contains("ColdD") &&
						            !a.Name.Contains("LightningD") &&
						            !a.Name.Contains("ArcaneD") &&
						            !a.Name.Contains("MinMaxDam") &&
						            isCrafting ? !a.Name.ToLower().Contains("socket") : !a.Name.Contains("ASDHUIOPASDHIOU"));
				}
			}

			if (!isUnique && affixesCount <= 1)
				filteredList = filteredList.Where(
					a =>
					!a.Name.Contains("Secondary")
					);

			if (item.GBHandle.GBID == -4139386)
				filteredList = filteredList.Where(
					a =>
					!a.Name.Contains("Str") &&
					!a.Name.Contains("Dex") &&
					!a.Name.Contains("Int") &&
					!a.Name.Contains("Vit")
					);

			if (affixesCount + primaryCount + secondaryCount <= 3)
				filteredList = filteredList.Where(
					a =>
					!a.Name.Contains("Experience") &&
					!a.Name.Contains("Archon")
					);



			var pool = filteredList.ToList();
			List<AffixTable> selectedGroups = Roll(pool.Where(a => a.AffixGroup != SecondaryAffixGroup), primaryCount, taken);
			selectedGroups.AddRange(Roll(pool.Where(a => a.AffixGroup == SecondaryAffixGroup), secondaryCount, taken));
			selectedGroups.AddRange(Roll(pool, affixesCount, taken));

			int n = 0;
			foreach (var def in selectedGroups.Select(rolled => rolled == null ? null : Upranked(rolled, upranks)))
			{
				if (def != null)
				{
					//Logger.Debug("Generating affix " + def.Name + " (aLvl:" + def.AffixLevel + ")");
					List<float> Scores = new List<float>();
					foreach (var effect in def.AttributeSpecifier)
					{
						//if (def.Name.Contains("Sockets")) Logger.Info("socket affix attribute: {0}, {1}", effect.AttributeId, effect.SNOParam);
						if (effect.AttributeId > 0)
						{
							float result;
							float minValue;
							float maxValue;
							if (FormulaScript.Evaluate(effect.Formula.ToArray(), item.RandomGenerator, out result, out minValue, out maxValue))
							{
								if (effect.AttributeId == 369) continue; //Durability_Max
								if (effect.AttributeId == 380) //Sockets
								{
									result = Math.Max(result, 1f);
									if (def.Name.Contains("Chest"))
										result = (float)FastRandom.Instance.Next(1, 4);
									if (def.Name.Contains("Bracer"))
										result = (float)FastRandom.Instance.Next(1, 3);
								}
								float score = (minValue == maxValue ? 0.5f : ((result - minValue) / (maxValue - minValue)));
								Scores.Add(score);
								//Logger.Debug("Randomized value for attribute " + GameAttribute.Attributes[effect.AttributeId].Name + "(" + minValue + "," + maxValue + ")" + " is " + result + ", score is " + score);
								//var tmpAttr = GameAttribute.Attributes[effect.AttributeId];
								//var attrName = tmpAttr.Name;
								//


								if (GameAttributes.Attributes[effect.AttributeId] is GameAttributeF)
								{
									
									//
									var attr = GameAttributes.Attributes[effect.AttributeId] as GameAttributeF;
									if (effect.SNOParam != -1)
										item.Attributes[attr, effect.SNOParam] += result;
									else
										item.Attributes[attr] += result;
								}
								else if (GameAttributes.Attributes[effect.AttributeId] is GameAttributeI)
								{
									var attr = GameAttributes.Attributes[effect.AttributeId] as GameAttributeI;
									if (effect.SNOParam != -1)
										item.Attributes[attr, effect.SNOParam] += (int)result;
									else
										item.Attributes[attr] += (int)result;
								}
								//Logger.Debug("{0} - Str: {1} ({2})", n, result, item.Attributes[GameAttribute.Attributes[effect.AttributeId] as GameAttributeF]);
							}
						}
					}
					var affix = new Affix(def.Hash);
					affix.Score = (Scores.Count == 0 ? 0 : Scores.Average());
					//Logger.Debug("Affix  " + def.Hash + ", final score is" + affix.Score);
					item.AffixList.Add(affix);

					if (affixesCount > 0 && !isUnique && !item.ItemDefinition.Name.Contains("StaffOfCow"))
					{
						item.RareItemName = GenerateItemName();
					}
				}
				n++;
			}
		}

		/// <summary>
		/// An affix of a legendary's fixed family <paramref name="affixGroup"/>: one for its item type (or, with
		/// <paramref name="extendedFilter"/>, one meant for legendaries of the type), for the item's class, in the tier
		/// for its level. (DiIiS looked these up in a list of affixes that the game data leaves empty, so legendaries
		/// had none of them.)
		/// </summary>
		public static int FindSuitableAffix(Item item, int affixGroup, int affixLevel, bool extendedFilter)
		{
			if (affixGroup == -1) return -1;
			List<int> itemTypes = ItemGroup.HierarchyToHashList(item.ItemType);
			var allGroup = AllAffix
				.Where(a => (a.AffixFamily0 == affixGroup || a.AffixFamily1 == affixGroup) && (Item.Is2H(item.ItemType) ? !a.Name.EndsWith("1h") : (!a.Name.Contains("Two-Handed") && !a.Name.EndsWith("2h"))))
				.Where(a => itemTypes.ContainsAtLeastOne(a.ItemGroup) ||
				            (extendedFilter && itemTypes.ContainsAtLeastOne(a.LegendaryAllowedTypes)))
				.ToArray();
			var affixClass = AffixClass(item);
			var forClass = allGroup.Where(a => RollsFor(a, affixClass)).ToArray();
			if (forClass.Any())
				allGroup = forClass;

			if (!allGroup.Any()) return -1;

			// the tier for the item's level, or else the highest one below it
			int level = Math.Clamp(Math.Max(affixLevel, item.ItemLevel), 1, 70);
			var suitable = allGroup.Where(a => CoversLevel(a, level)).ToArray();
			if (!suitable.Any())
			{
				var below = allGroup.Where(a => a.AffixLevelMin <= level).ToArray();
				if (!below.Any()) return -1;
				int top = below.Max(a => a.AffixLevelMax);
				suitable = below.Where(a => a.AffixLevelMax == top).ToArray();
			}

			//if (i18 && !secondGroup)
			//	suitable = suitable.Where(a => a.MaxLevel <= (Program.MaxLevel + 4));

			//if (!ignoreItemTypeFilter)
			//else
			//suitable = suitable.Where(a => itemTypes.ContainsAtLeastOne(a.I10));

			/*int suitableMaxLevel = suitable.OrderByDescending(a => a.AffixLevel).First().MaxLevel;
			int suitableAffixLevel = suitable.OrderByDescending(a => a.AffixLevel).First().AffixLevel;
			
			if (suitableMaxLevel > (Program.MaxLevel + 4) && i18 && !secondGroup)
				suitable = all_group.Where(a => a.AffixLevel == 1);
			else
				suitable = all_group.Where(a => a.AffixLevel == suitableAffixLevel);
			
			if (suitable.Count() > 1 && i18 && !secondGroup && suitable.Where(a => a.Name.Contains("Secondary")).Count() > 0)
				suitable = suitable.Where(a => a.Name.Contains("Secondary"));*/

			if (suitable.TryPickRandom(out var randomAffix))
				return randomAffix.Hash;

			return -1;
		}

		public static void AddAffix(Item item, int affixGbId, bool findFromTotal = false)
		{
			if (affixGbId == -1) return;
			AffixTable definition = null;

			if (findFromTotal)
			{
				definition = AffixesByHash.GetValueOrDefault(affixGbId);
			}
			else
			{
				definition = AffixList.FirstOrDefault(def => def.Hash == affixGbId);

				if (definition == null)
				{
					definition = LegendaryAffixList.FirstOrDefault(def => def.Hash == affixGbId);

				}
			}

			if (definition == null)
			{
				Logger.Warn("Affix {0} was not found!", affixGbId);
				return;
			}

			item.AffixFamilies.Add(definition.AffixFamily0);
			//if (item.AffixList.Where(a => a.AffixGbid == AffixGbId).ToList().Count > 0) return;
			List<float> Scores = new List<float>();
			foreach (var effect in definition.AttributeSpecifier)
			{
				if (effect.AttributeId <= 0)
					continue;

				float result;
				float minValue;
				float maxValue;
				if (FormulaScript.Evaluate(effect.Formula.ToArray(), item.RandomGenerator, out result, out minValue, out maxValue))
				{
					var attribute = GameAttributes.Attributes[effect.AttributeId];

					if (effect.AttributeId == 369) continue; //Durability_Max
					if (effect.AttributeId == 380) //Sockets
					{
						result = Math.Max(result, 1f);
						if (definition.Name.Contains("Bracer") && item.GBHandle.GBID == 219884787) //Inna's Legs
							result = 2;
					}
					float score = (minValue == maxValue ? 0.5f : ((result - minValue) / (maxValue - minValue)));
					Scores.Add(score);
					if (attribute is GameAttributeF)
					{
						var attr = GameAttributes.Attributes[effect.AttributeId] as GameAttributeF;
						if (effect.SNOParam != -1)
							item.Attributes[attr, effect.SNOParam] += result;
						else
							item.Attributes[attr] += result;
					}
					else if (GameAttributes.Attributes[effect.AttributeId] is GameAttributeI)
					{
						var attr = GameAttributes.Attributes[effect.AttributeId] as GameAttributeI;
						if (effect.SNOParam != -1)
							item.Attributes[attr, effect.SNOParam] += (int)result;
						else
							item.Attributes[attr] += (int)result;
					}
					else if (GameAttributes.Attributes[effect.AttributeId] is GameAttributeB)
					{
						var attr = GameAttributes.Attributes[effect.AttributeId] as GameAttributeB;
						if (result == 1)
							item.Attributes[attr] = true;
						else
							item.Attributes[attr] = false;
					}
				}
			}
			var affix = new Affix(affixGbId);
			affix.Score = (Scores.Count == 0 ? 0 : Scores.Average());
			item.AffixList.Add(affix);
			//item.Attributes[GameAttribute.Item_Quality_Level]++;
		}

		public static void CloneIntoItem(Item source, Item target)
		{
			target.AffixList.Clear();
			foreach (var affix in source.AffixList)
			{
				var newItemAffix = new Affix(affix.AffixGbid);
				target.AffixList.Add(newItemAffix);
			}
			foreach (var affix in target.AffixList)
			{
				var definition = AffixesByHash[affix.AffixGbid];

				foreach (var effect in definition.AttributeSpecifier)
				{
					if (effect.AttributeId <= 0)
						continue;

					var attribute = GameAttributes.Attributes[effect.AttributeId];

					if (attribute.ScriptFunc != null && !attribute.ScriptedAndSettable)
						continue;

					if (attribute is GameAttributeF)
					{
						var attr = GameAttributes.Attributes[effect.AttributeId] as GameAttributeF;
						if (effect.SNOParam != -1)
							target.Attributes[attr, effect.SNOParam] = source.Attributes[attr, effect.SNOParam];
						else
							target.Attributes[attr] = source.Attributes[attr];
					}
					else if (GameAttributes.Attributes[effect.AttributeId] is GameAttributeI)
					{
						var attr = GameAttributes.Attributes[effect.AttributeId] as GameAttributeI;
						if (effect.SNOParam != -1)
							target.Attributes[attr, effect.SNOParam] = source.Attributes[attr, effect.SNOParam];
						else
							target.Attributes[attr] = source.Attributes[attr];
					}

				}
			}
		}

		private static List<int> PrefixAffixLists = new List<int> { 214115, 1113787, 130726, 130727, 130761, 130748, 130729, 130730, 130731, 130735, 130734, 214211, 214213, 130737, 130749, 130739, 214091, 130742, 130744, 214093, 130745, 222658, 130740, 214136, 222661, 130746, 130733, 214215, 130752, 130753, 130755, 130756, 214120, 214102, 214088, 130758, 130759, 130760, 214189, 130762, 130763, 214108, 214106, 130764, 214202, 214206, 214209, 130765, 130766 };

		private static List<int> SuffixAffixLists = new List<int> { 214116, 213605, 213606, 213608, 213638, 213610, 213611, 213612, 213613, 213614, 213617, 214212, 214214, 213618, 213619, 213621, 214092, 213622, 213623, 214094, 213624, 222659, 213625, 214137, 222660, 213626, 213615, 214216, 213628, 213629, 213631, 213633, 214121, 214103, 214089, 213635, 213636, 213637, 214197, 213639, 213640, 214109, 214107, 213607, 214203, 214208, 214210, 213641, 213642 };

		public static D3.Items.RareItemName GenerateItemName()
		{
			bool itemIsPrefix = (FastRandom.Instance.Next(0, 2) > 0);
			var randomName = D3.Items.RareItemName.CreateBuilder()
				.SetItemNameIsPrefix(itemIsPrefix)
				.SetSnoAffixStringList(itemIsPrefix ? SuffixAffixLists.PickRandom() : PrefixAffixLists.PickRandom())
				.SetAffixStringListIndex(FastRandom.Instance.Next(0, 6))
				.SetItemStringListIndex(FastRandom.Instance.Next(0, 6));
			return randomName.Build();
		}

		public static void MaxRollAffixAttributes(Item item)
		{
			// Use a fresh generator so we don't advance item.RandomGenerator's state.
			// maxValue is derived purely from formula constants, so the seed value only
			// needs to match the type expected by FormulaScript (any seed is fine here).
			var evalRng = new ItemRandomHelper(item.Attributes[GameAttributes.Seed]);

			foreach (var affix in item.AffixList)
			{
				var definition = affix.Definition;
				if (definition == null) continue;

				foreach (var effect in definition.AttributeSpecifier)
				{
					if (effect.AttributeId <= 0) continue;
					if (effect.AttributeId == 369) continue; // Durability_Max
					if (effect.Formula == null || effect.Formula.Count == 0) continue;

					if (!FormulaScript.Evaluate(effect.Formula.ToArray(), evalRng,
						out float result, out float minValue, out float maxValue))
						continue;

					if (minValue == maxValue) continue; // fixed roll, no change needed

					// Apply only the delta so that other additive contributors
					// (base stats, other affixes) are not clobbered.
					float delta = maxValue - result;
					if (delta <= 0f) continue;

					var attribute = GameAttributes.Attributes[effect.AttributeId];
					if (attribute is GameAttributeF attrF)
					{
						if (effect.SNOParam != -1)
							item.Attributes[attrF, effect.SNOParam] += delta;
						else
							item.Attributes[attrF] += delta;
					}
					else if (attribute is GameAttributeI attrI)
					{
						if (effect.SNOParam != -1)
							item.Attributes[attrI, effect.SNOParam] += (int)delta;
						else
							item.Attributes[attrI] += (int)delta;
					}
				}
			}
		}
	}
}
