using System;
using System.Collections.Generic;
using DiIiS_NA.Core.Helpers.Math;
using DiIiS_NA.D3_GameServer;
using DiIiS_NA.GameServer.GSSystem.PlayerSystem;
using DiIiS_NA.LoginServer.Toons;

namespace DiIiS_NA.GameServer.GSSystem.ItemsSystem
{
	public static class LootManager
	{

		static LootManager()
		{
		}

		/// <summary>
		/// Probability that a dropped item is filtered to the receiving player's class (Smart Drop).
		/// The remaining chance produces a fully random drop for variety.
		/// </summary>
		public const float SmartDropChance = 0.85f;

		/// <summary>
		/// Returns the class to use when spawning a smart-drop item.
		/// 85% of the time the item targets the player's class; 15% is fully random (ToonClass.Unknown).
		/// </summary>
		public static ToonClass GetSmartDropClass(Player player)
			=> FastRandom.Instance.NextDouble() < SmartDropChance ? player.Toon.Class : ToonClass.Unknown;

		public static int Common
		{
			get { return 1; }
			set { }
		}

		public static int Uncommon
		{
			get { return FastRandom.Instance.Next(3, 5); }
			set { }
		}

		public static int Rare
		{
			get { return FastRandom.Instance.Next(5, 8); }
			set { }
		}

		public static int Epic
		{
			get { return FastRandom.Instance.Next(8, 11); }
			set { }
		}

		// ---------------------------------------------------------------------------
		// Loot-quality data tables
		// ---------------------------------------------------------------------------
		// Dimensions: [monsterQualityIndex, difficultyIndex]
		//   monsterQualityIndex: 0=Normal, 1=Champion, 2=Rare/Unique, 3=Boss
		//   difficultyIndex: 0=Normal-Master, 1=T1, 2=T2, 3=T3, 4=T4, 5=T5, 6=T6
		//
		// Each entry holds the upper-exclusive thresholds for Common, Uncommon, and Rare.
		// A roll >= RareMax yields Epic (Legendary).

		private readonly struct QualityThresholds
		{
			public readonly float CommonMax;
			public readonly float UncommonMax;
			public readonly float RareMax;
			public QualityThresholds(float commonMax, float uncommonMax, float rareMax)
			{
				CommonMax   = commonMax;
				UncommonMax = uncommonMax;
				RareMax     = rareMax;
			}
		}

		// Maximum torment difficulty index (T6 = difficulty value 9 → index 6).
		private const int MaxTormentIndex = 6;

		// Maps monster-quality value → table row index; returns -1 for unknown types.
		private static int ToQualityIndex(int monsterQuality) => monsterQuality switch
		{
			0 => 0, // Normal
			1 => 1, // Champion
			2 => 2, // Rare (Elite)
			4 => 2, // Unique
			7 => 3, // Boss
			_ => -1,
		};

		// Maps difficulty value → table column index.
		// Difficulties 0-3 (Normal/Hard/Expert/Master) share column 0.
		// T1-T6 map to columns 1-6.
		private static int ToDifficultyIndex(int difficulty)
			=> difficulty <= 3 ? 0 : Math.Min(difficulty - 3, MaxTormentIndex);

		private static int EvaluateQuality(float roll, in QualityThresholds t)
		{
			if (roll < t.CommonMax)    return Common;
			if (roll < t.UncommonMax)  return Uncommon;
			if (roll < t.RareMax)      return Rare;
			return Epic;
		}

		// Normal (non-seasonal) quality thresholds
		private static readonly QualityThresholds[,] _qualityThresholds = new QualityThresholds[,]
		{
			// Normal mob                                      Norm-Mstr   T1          T2          T3          T4          T5          T6
			{ new(0.05f, 0.30f, 0.9950f), new(0.05f, 0.30f, 0.9940f), new(0.05f, 0.30f, 0.9930f), new(0.05f, 0.30f, 0.9920f), new(0.05f, 0.30f, 0.9910f), new(0.05f, 0.30f, 0.9905f), new(0.05f, 0.30f, 0.9900f) },
			// Champion
			{ new(0.02f, 0.25f, 0.9930f), new(0.02f, 0.25f, 0.9920f), new(0.02f, 0.25f, 0.9905f), new(0.02f, 0.25f, 0.9890f), new(0.02f, 0.25f, 0.9875f), new(0.02f, 0.25f, 0.9860f), new(0.02f, 0.25f, 0.9850f) },
			// Rare / Unique
			{ new(0.02f, 0.20f, 0.9900f), new(0.02f, 0.20f, 0.9880f), new(0.02f, 0.20f, 0.9860f), new(0.02f, 0.20f, 0.9840f), new(0.02f, 0.20f, 0.9820f), new(0.02f, 0.20f, 0.9810f), new(0.02f, 0.20f, 0.9800f) },
			// Boss
			{ new(0.01f, 0.10f, 0.9900f), new(0.01f, 0.10f, 0.9860f), new(0.01f, 0.10f, 0.9820f), new(0.01f, 0.10f, 0.9750f), new(0.01f, 0.10f, 0.9700f), new(0.01f, 0.10f, 0.9600f), new(0.01f, 0.10f, 0.9400f) },
		};

		// Seasonal quality thresholds (higher legendary rates)
		private static readonly QualityThresholds[,] _seasonalQualityThresholds = new QualityThresholds[,]
		{
			// Normal mob                                      Norm-Mstr   T1          T2          T3          T4          T5          T6
			{ new(0.05f, 0.30f, 0.9940f), new(0.05f, 0.30f, 0.9925f), new(0.05f, 0.30f, 0.9908f), new(0.05f, 0.30f, 0.9886f), new(0.05f, 0.30f, 0.9862f), new(0.05f, 0.30f, 0.9835f), new(0.05f, 0.30f, 0.9800f) },
			// Champion
			{ new(0.02f, 0.25f, 0.9920f), new(0.02f, 0.25f, 0.9900f), new(0.02f, 0.25f, 0.9878f), new(0.02f, 0.25f, 0.9850f), new(0.02f, 0.25f, 0.9820f), new(0.02f, 0.25f, 0.9785f), new(0.02f, 0.25f, 0.9745f) },
			// Rare / Unique
			{ new(0.02f, 0.20f, 0.9870f), new(0.02f, 0.20f, 0.9840f), new(0.02f, 0.20f, 0.9808f), new(0.02f, 0.20f, 0.9770f), new(0.02f, 0.20f, 0.9728f), new(0.02f, 0.20f, 0.9683f), new(0.02f, 0.20f, 0.9630f) },
			// Boss
			{ new(0.01f, 0.10f, 0.9800f), new(0.01f, 0.10f, 0.9750f), new(0.01f, 0.10f, 0.9695f), new(0.01f, 0.10f, 0.9630f), new(0.01f, 0.10f, 0.9560f), new(0.01f, 0.10f, 0.9480f), new(0.01f, 0.10f, 0.9400f) },
		};

		public static int GetLootQuality(int MonsterQuality, int difficulty)
		{
			int qi = ToQualityIndex(MonsterQuality);
			if (qi < 0) return Common;
			int di = ToDifficultyIndex(difficulty);
			float roll = (float)FastRandom.Instance.NextDouble();
			return EvaluateQuality(roll, _qualityThresholds[qi, di]);
		}

		public static int GetSeasonalLootQuality(int MonsterQuality, int difficulty)
		{
			int qi = ToQualityIndex(MonsterQuality);
			if (qi < 0) return Common;
			int di = ToDifficultyIndex(difficulty);
			float roll = (float)FastRandom.Instance.NextDouble();
			return EvaluateQuality(roll, _seasonalQualityThresholds[qi, di]);
		}

		public static List<float> GetDropRates(int MonsterQuality, int level = 60)
		{
			if (level < 6)
				switch (MonsterQuality)
				{
					case 1: //Champion
						return new List<float> { 1f, 1f, 0.5f };
					case 2: //Rare (Elite)
					case 4: //Unique
						return new List<float> { 1f, 1f, 0.7f, 0.5f };
					default: return new List<float> { 0.04f };
				}

			switch (MonsterQuality)
			{
				case 0: //Normal
					return new List<float> { 0.06f };
				case 1: //Champion
					return new List<float> { 1f, 1f, 0.7f, 0.5f };
				case 2: //Rare (Elite)
				case 4: //Unique
					return new List<float> { 1f, 1f, 0.9f, 0.7f };
				case 7: //Boss
					return new List<float> { 1f, 1f, 1f, 0.7f, 0.5f, 0.3f };
				default:
					return new List<float> { 0.04f };
			}
		}

		public static List<float> GetSeasonalDropRates(int MonsterQuality, int level)
		{
			if (level < 10)
				switch (MonsterQuality)
				{
					case 1: //Champion
						return new List<float> { 1f, 1f, 1f };
					case 2: //Rare (Elite)
					case 4: //Unique
						return new List<float> { 1f, 1f, 1f, 1f };
					default: return new List<float> { 0.08f };
				}

			switch (MonsterQuality)
			{
				case 0: //Normal
					return new List<float> { 0.18f * GameModsConfig.Instance.Rate.ChangeDrop };
				case 1: //Champion
					return new List<float> { 1f, 1f, 1f, 1f, 0.75f * GameModsConfig.Instance.Rate.ChangeDrop };
				case 2: //Rare (Elite)
				case 4: //Unique
					return new List<float> { 1f, 1f, 1f, 1f, 1f };
				case 7: //Boss
					return new List<float> { 1f, 1f, 1f, 1f, 1f, 0.75f * GameModsConfig.Instance.Rate.ChangeDrop, 0.4f * GameModsConfig.Instance.Rate.ChangeDrop };
				default:
					return new List<float> { 0.12f * GameModsConfig.Instance.Rate.ChangeDrop };
			}
		}

		public static int GetGoldAmount(int level)
		{
			return Math.Max(1, (int)(DiIiS_NA.Core.Helpers.Math.FastRandom.Instance.Next(level, level * 2) * (DiIiS_NA.Core.Helpers.Math.FastRandom.Instance.NextDouble(7, 10) / 10f)));
		}

		public static int GetBloodShardsAmount(int difficulty)
		{
			switch (difficulty)
			{
				case 0:
					return 0;
				case 1:
					return DiIiS_NA.Core.Helpers.Math.FastRandom.Instance.NextDouble() < 0.25 ? 1 : 0;
				case 2:
					return DiIiS_NA.Core.Helpers.Math.FastRandom.Instance.NextDouble() < 0.5 ? 1 : 0;
				case 3:
					return DiIiS_NA.Core.Helpers.Math.FastRandom.Instance.NextDouble() < 0.75 ? 1 : 0;
				case 4: //T1
				case 5: //T2
					return 1;
				case 6: //T3
				case 7: //T4
					return 2;
				case 8: //T5
				case 9: //T6
					return 3;
				default: return 0;
			}
		}

		public static float GetEssenceDropChance(int difficulty)
		{
			switch (difficulty)
			{
				case 0:
					return 0.15f;
				case 1:
					return 0.18f;
				case 2:
					return 0.21f;
				case 3:
					return 0.25f;
				case 4: //T1
					return 0.31f;
				case 5: //T2
					return 0.37f;
				case 6: //T3
					return 0.44f;
				case 7: //T4
					return 0.53f;
				case 8: //T5
					return 0.64f;
				case 9: //T6
					return 0.77f;
				default: return 0f;
			}
		}
	}
}
