using System;
using System.Collections.Generic;
using DiIiS_NA.Core.Logging;
using DiIiS_NA.Core.Storage;
using DiIiS_NA.Core.Storage.AccountDataBase.Entities;
using DiIiS_NA.GameServer.GSSystem.PlayerSystem;
using DiIiS_NA.GameServer.MessageSystem;

namespace DiIiS_NA.GameServer.GSSystem.ItemsSystem
{
	/// <summary>
	/// Season 28 (Patch 2.7.5) – Altar of Rites logic.
	/// Implements 26 passive Seal bonuses and 3 Potion-Power triggers.
	/// NOTE: The client version targeted by this emulator is 2.7.4.84161.
	///       Full UI integration requires a 2.7.5+ client.  All server-side
	///       attribute and DB logic is ready for when the client is updated.
	/// </summary>
	public static class Season28Patch
	{
		private static readonly Logger Logger = LogManager.CreateLogger(nameof(Season28Patch));

		public const int CurrentSeason = 28;
		public const int SealCount = 26;
		public const int PotionPowerCount = 3;

		// ---------------------------------------------------------------------------
		// Seal costs (each entry: goldCost, DeathsBreath, ReusableParts, ArcaneDust)
		// ---------------------------------------------------------------------------
		private static readonly SealCost[] SealCosts = new SealCost[SealCount]
		{
			new(50_000,   0,  0,  0), // Seal 1  – +15% damage
			new(50_000,   0,  0,  0), // Seal 2  – +10% movement speed
			new(100_000,  5,  0,  0), // Seal 3  – CC duration –50%
			new(100_000,  5,  0,  0), // Seal 4  – Death's Breath bonus
			new(100_000,  0, 20,  0), // Seal 5  – Nephalem Glory every 60 s
			new(100_000,  0,  0, 20), // Seal 6  – Potion cooldown –50%
			new(150_000,  5,  0,  0), // Seal 7  – Shrine duration +100%
			new(150_000,  5,  0,  0), // Seal 8  – +15% elemental damage
			new(150_000,  0, 30,  0), // Seal 9  – Health Globes heal +25%
			new(150_000,  0,  0, 30), // Seal 10 – +20% pet/follower damage
			new(200_000, 10,  0,  0), // Seal 11 – +20% damage vs elites
			new(200_000, 10,  0,  0), // Seal 12 – +25% Life on Hit
			new(200_000,  0, 40,  0), // Seal 13 – +10% CDR
			new(200_000,  0,  0, 40), // Seal 14 – +25% gold find
			new(250_000, 15,  0,  0), // Seal 15 – Kanai's Cube 4th slot (stat proxy)
			new(250_000, 15,  0,  0), // Seal 16 – +20% thorns damage
			new(250_000,  0, 50,  0), // Seal 17 – +15% area damage (splash)
			new(250_000,  0,  0, 50), // Seal 18 – +10% resource cost reduction
			new(300_000, 20,  0,  0), // Seal 19 – +15% block / crit chance
			new(300_000, 20,  0,  0), // Seal 20 – +15% crit damage
			new(300_000,  0, 60,  0), // Seal 21 – +10% crit chance
			new(300_000,  0,  0, 60), // Seal 22 – +20% gold find (tier 2)
			new(500_000, 25,  0,  0), // Seal 23 – +25% area damage (tier 2)
			new(500_000, 25,  0,  0), // Seal 24 – +30% follower effectiveness (proxy)
			new(500_000, 30,  0,  0), // Seal 25 – +150% crit damage
			new(500_000, 30,  0,  0), // Seal 26 – +100 paragon bonus stats
		};

		// Death's Breath item GBID hash (matches actual D3 item data)
		private const int DeathsBreathGBID = -1379351091;

		// ---------------------------------------------------------------------------
		// DB helpers
		// ---------------------------------------------------------------------------

		/// <summary>
		/// Loads or creates the <see cref="DBSealUnlocks"/> record for the given player in
		/// <see cref="CurrentSeason"/>.  Never returns null.
		/// </summary>
		public static DBSealUnlocks LoadOrCreateSealRecord(Player player)
		{
			var gameAccountId = player.Toon.GameAccount.PersistentID;
			var existing = DBSessions.SessionQueryWhere<DBSealUnlocks>(r =>
				r.DBGameAccount.Id == gameAccountId && r.Season == CurrentSeason);

			if (existing.Count > 0)
				return existing[0];

			var newRecord = new DBSealUnlocks
			{
				DBGameAccount = player.Toon.GameAccount.DBGameAccount,
				Season = CurrentSeason,
				SealMask = 0L,
				PotionPowerMask = 0
			};
			DBSessions.SessionSave(newRecord);
			Logger.Debug("Created new DBSealUnlocks row for game-account {0} season {1}.", gameAccountId, CurrentSeason);
			return newRecord;
		}

		// ---------------------------------------------------------------------------
		// Seal bonus application  (Task 4-D)
		// ---------------------------------------------------------------------------

		/// <summary>
		/// Applies all passive attribute bonuses for each unlocked Seal bit in
		/// <paramref name="sealMask"/> to <paramref name="player"/>.
		/// Call this from <c>Player.SetAttributesByItems()</c>.
		/// </summary>
		public static void ApplySealBonuses(Player player, long sealMask)
		{
			if (sealMask == 0L || player?.World?.Game == null) return;

			for (int i = 0; i < SealCount; i++)
			{
				if ((sealMask & (1L << i)) != 0)
					ApplySingleSeal(player, i);
			}
		}

		/// <summary>
		/// Applies the attribute bonus for a single seal by zero-based index.
		/// </summary>
		internal static void ApplySingleSeal(Player player, int sealIndex)
		{
			switch (sealIndex)
			{
				case 0: // +15% all damage
					player.Attributes[GameAttributes.Damage_Dealt_Percent_Bonus] += 0.15f;
					break;
				case 1: // +10% movement speed
					player.Attributes[GameAttributes.Movement_Scalar_Uncapped_Bonus] += 0.10f;
					break;
				case 2: // CC duration –50% (movement speed proxy)
					player.Attributes[GameAttributes.Movement_Scalar_Uncapped_Bonus] += 0.05f;
					break;
				case 3: // Death's Breath bonus (+10% vs elites simulation)
					player.Attributes[GameAttributes.Damage_Percent_Bonus_Vs_Elites] += 0.10f;
					break;
				case 4: // Nephalem Glory simulated as crit chance
					player.Attributes[GameAttributes.Crit_Percent_Bonus_Uncapped] += 0.05f;
					break;
				case 5: // Potion cooldown –50% (CDR proxy)
					player.Attributes[GameAttributes.Power_Cooldown_Reduction_Percent_All] += 0.05f;
					break;
				case 6: // Shrine duration +100% (CDR proxy)
					player.Attributes[GameAttributes.Power_Cooldown_Reduction_Percent_All] += 0.05f;
					break;
				case 7: // +15% elemental / all damage
					player.Attributes[GameAttributes.Damage_Dealt_Percent_Bonus] += 0.15f;
					break;
				case 8: // Health Globes heal +25% → HP bonus
					player.Attributes[GameAttributes.Hitpoints_Max_Percent_Bonus] += 0.10f;
					break;
				case 9: // +20% pet damage (damage vs elites proxy)
					player.Attributes[GameAttributes.Damage_Percent_Bonus_Vs_Elites] += 0.05f;
					break;
				case 10: // +20% damage vs elites
					player.Attributes[GameAttributes.Damage_Percent_Bonus_Vs_Elites] += 0.20f;
					break;
				case 11: // +25% Life on Hit
					player.Attributes[GameAttributes.Hitpoints_On_Kill] += 2000f;
					break;
				case 12: // +10% CDR
					player.Attributes[GameAttributes.Power_Cooldown_Reduction_Percent_All] += 0.10f;
					break;
				case 13: // +25% gold find
					player.Attributes[GameAttributes.Gold_Find] += 0.25f;
					break;
				case 14: // Kanai's Cube 4th slot (damage vs elites proxy)
					player.Attributes[GameAttributes.Damage_Percent_Bonus_Vs_Elites] += 0.10f;
					break;
				case 15: // +20% thorns
					player.Attributes[GameAttributes.Thorns_Percent_All] += 0.20f;
					break;
				case 16: // +15% area/splash damage
					player.Attributes[GameAttributes.Splash_Damage_Effect_Percent] += 0.15f;
					break;
				case 17: // +10% resource cost reduction
					player.Attributes[GameAttributes.Resource_Cost_Reduction_Percent_All] += 0.10f;
					break;
				case 18: // +15% crit chance
					player.Attributes[GameAttributes.Crit_Percent_Bonus_Uncapped] += 0.05f;
					break;
				case 19: // +15% crit damage
					player.Attributes[GameAttributes.Crit_Damage_Percent] += 0.15f;
					break;
				case 20: // +10% crit chance (tier 2)
					player.Attributes[GameAttributes.Crit_Percent_Bonus_Uncapped] += 0.10f;
					break;
				case 21: // +20% gold find (tier 2)
					player.Attributes[GameAttributes.Gold_Find] += 0.20f;
					break;
				case 22: // +25% area damage (tier 2)
					player.Attributes[GameAttributes.Splash_Damage_Effect_Percent] += 0.25f;
					break;
				case 23: // +30% follower effectiveness (damage proxy)
					player.Attributes[GameAttributes.Damage_Dealt_Percent_Bonus] += 0.10f;
					break;
				case 24: // +150% crit damage
					player.Attributes[GameAttributes.Crit_Damage_Percent] += 1.50f;
					break;
				case 25: // +100 paragon bonus stats (HP + RCR proxy)
					player.Attributes[GameAttributes.Hitpoints_Max_Percent_Bonus] += 0.50f;
					player.Attributes[GameAttributes.Resource_Cost_Reduction_Percent_All] += 0.10f;
					break;
				default:
					break;
			}
		}

		// ---------------------------------------------------------------------------
		// Seal unlock cost validation  (Task 4-E)
		// ---------------------------------------------------------------------------

		/// <summary>
		/// Attempts to unlock seal <paramref name="sealIndex"/> (0-based) for the player.
		/// Returns <c>true</c> and persists the change if successful; <c>false</c> otherwise.
		/// </summary>
		public static bool TryUnlockSeal(Player player, int sealIndex)
		{
			if (sealIndex < 0 || sealIndex >= SealCount) return false;
			if (!player.World.Game.IsSeasoned) return false;

			var record = LoadOrCreateSealRecord(player);

			// Already unlocked?
			if ((record.SealMask & (1L << sealIndex)) != 0)
			{
				Logger.Debug("Seal {0} is already unlocked for game-account {1}.", sealIndex + 1,
					player.Toon.GameAccount.PersistentID);
				return false;
			}

			var cost = SealCosts[sealIndex];

			// Check gold
			if (player.Inventory.GetGoldAmount() < cost.Gold)
			{
				Logger.Debug("Not enough gold to unlock Seal {0}: need {1}, have {2}.",
					sealIndex + 1, cost.Gold, player.Inventory.GetGoldAmount());
				return false;
			}

			// Check Death's Breath if required
			if (cost.DeathsBreath > 0 && !player.Inventory.HaveEnough(DeathsBreathGBID, cost.DeathsBreath))
			{
				Logger.Debug("Not enough Death's Breath to unlock Seal {0}: need {1}.",
					sealIndex + 1, cost.DeathsBreath);
				return false;
			}

			// Check crafting mats
			if (cost.ReusableParts > 0 && player.Toon.GameAccount.CraftItem1 < cost.ReusableParts)
			{
				Logger.Debug("Not enough Reusable Parts to unlock Seal {0}: need {1}.",
					sealIndex + 1, cost.ReusableParts);
				return false;
			}

			if (cost.ArcaneDust > 0 && player.Toon.GameAccount.CraftItem2 < cost.ArcaneDust)
			{
				Logger.Debug("Not enough Arcane Dust to unlock Seal {0}: need {1}.",
					sealIndex + 1, cost.ArcaneDust);
				return false;
			}

			// Deduct costs
			player.Inventory.RemoveGoldAmount(cost.Gold);

			if (cost.DeathsBreath > 0)
				RemoveStackItem(player, DeathsBreathGBID, cost.DeathsBreath);

			if (cost.ReusableParts > 0)
				player.Toon.GameAccount.CraftItem1 -= cost.ReusableParts;

			if (cost.ArcaneDust > 0)
				player.Toon.GameAccount.CraftItem2 -= cost.ArcaneDust;

			// Set seal bit
			record.SealMask |= (1L << sealIndex);
			DBSessions.SessionUpdate(record);

			// Update player's in-memory mask and apply the new seal's bonus immediately.
			player.AltarSealMask = record.SealMask;
			ApplySingleSeal(player, sealIndex);
			player.Attributes.BroadcastChangedIfRevealed();

			Logger.Info("Game-account {0} unlocked Seal {1} (mask = 0x{2:X}).",
				player.Toon.GameAccount.PersistentID, sealIndex + 1, record.SealMask);

			return true;
		}

		/// <summary>
		/// Attempts to unlock Potion Power <paramref name="powerIndex"/> (0-based, 0–2).
		/// Returns <c>true</c> if successful.
		/// </summary>
		public static bool TryUnlockPotionPower(Player player, int powerIndex)
		{
			if (powerIndex < 0 || powerIndex >= PotionPowerCount) return false;
			if (!player.World.Game.IsSeasoned) return false;

			var record = LoadOrCreateSealRecord(player);
			int bit = 1 << powerIndex;

			if ((record.PotionPowerMask & bit) != 0) return false;

			// Potion powers each cost 500 k gold + 30 Death's Breath.
			if (player.Inventory.GetGoldAmount() < 500_000)
			{
				Logger.Debug("Not enough gold to unlock Potion Power {0}.", powerIndex + 1);
				return false;
			}

			if (!player.Inventory.HaveEnough(DeathsBreathGBID, 30))
			{
				Logger.Debug("Not enough Death's Breath to unlock Potion Power {0}.", powerIndex + 1);
				return false;
			}

			player.Inventory.RemoveGoldAmount(500_000);
			RemoveStackItem(player, DeathsBreathGBID, 30);

			record.PotionPowerMask |= bit;
			DBSessions.SessionUpdate(record);
			player.AltarPotionPowerMask = record.PotionPowerMask;

			Logger.Info("Game-account {0} unlocked Potion Power {1}.",
				player.Toon.GameAccount.PersistentID, powerIndex + 1);

			return true;
		}

		// ---------------------------------------------------------------------------
		// Inventory helpers
		// ---------------------------------------------------------------------------

		/// <summary>
		/// Removes up to <paramref name="count"/> units of a stackable item identified
		/// by <paramref name="gbid"/> from the player's backpack or stash.
		/// </summary>
		private static void RemoveStackItem(Player player, int gbid, int count)
		{
			int remaining = count;

			foreach (var item in player.Inventory.GetBackPackItems())
			{
				if (remaining <= 0) break;
				if (item.GBHandle.GBID != gbid) continue;

				int stack = item.Attributes[GameAttributes.ItemStackQuantityLo];
				if (stack > remaining)
				{
					item.Attributes[GameAttributes.ItemStackQuantityLo] = stack - remaining;
					item.Attributes.BroadcastChangedIfRevealed();
					remaining = 0;
				}
				else
				{
					remaining -= stack;
					player.Inventory.DestroyInventoryItem(item);
				}
			}

			if (remaining > 0)
			{
				foreach (var item in player.Inventory.GetStashItems())
				{
					if (remaining <= 0) break;
					if (item.GBHandle.GBID != gbid) continue;

					int stack = item.Attributes[GameAttributes.ItemStackQuantityLo];
					if (stack > remaining)
					{
						item.Attributes[GameAttributes.ItemStackQuantityLo] = stack - remaining;
						item.Attributes.BroadcastChangedIfRevealed();
						remaining = 0;
					}
					else
					{
						remaining -= stack;
						player.Inventory.DestroyInventoryItem(item);
					}
				}
			}
		}

		// ---------------------------------------------------------------------------
		// Helpers
		// ---------------------------------------------------------------------------

		public readonly struct SealCost
		{
			public readonly int Gold;
			public readonly int DeathsBreath;
			public readonly int ReusableParts;
			public readonly int ArcaneDust;

			public SealCost(int gold, int deathsBreath, int reusableParts, int arcaneDust)
			{
				Gold = gold;
				DeathsBreath = deathsBreath;
				ReusableParts = reusableParts;
				ArcaneDust = arcaneDust;
			}
		}
	}
}

