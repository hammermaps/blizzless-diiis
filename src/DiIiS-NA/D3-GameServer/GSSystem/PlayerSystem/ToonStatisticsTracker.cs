using System;
using System.Collections.Generic;
using System.Linq;
using DiIiS_NA.Core.Logging;
using DiIiS_NA.Core.Storage;
using DiIiS_NA.Core.Storage.AccountDataBase.Entities;
using DiIiS_NA.GameServer.GSSystem.ActorSystem;
using DiIiS_NA.GameServer.GSSystem.ActorSystem.Implementations;
using NHibernate.Linq;

namespace DiIiS_NA.GameServer.GSSystem.PlayerSystem
{
	/// <summary>
	/// Collects the lifetime statistics of one character and writes them to the account database.
	/// Counters are accumulated in memory and flushed in batches (every 30 seconds and on logout),
	/// so the hot paths (gold pickup, monster death) never touch the database.
	/// </summary>
	public class ToonStatisticsTracker
	{
		private static readonly Logger Logger = LogManager.CreateLogger();

		private readonly ulong _toonId;
		private readonly object _lock = new();

		private long _pendingGold;
		private long _pendingSeconds;
		private int _pendingSessions;
		private int _pendingKills;
		private int _pendingKillsNormal;
		private int _pendingKillsElite;
		private int _pendingKillsChampion;
		private int _pendingKillsRare;
		private int _pendingKillsBoss;
		private int _pendingKillsUnique;
		private int _pendingDeaths;
		private int _pendingChests;
		private int _pendingEvents;
		private int _pendingLore;

		private readonly Dictionary<int, (string Name, int Count)> _pendingMonsterKills = new();

		private DateTime _sessionStart = DateTime.MinValue;

		public ToonStatisticsTracker(ulong toonId)
		{
			_toonId = toonId;
		}

		public ulong ToonId => _toonId;

		public void BeginSession()
		{
			lock (_lock)
			{
				_sessionStart = DateTime.UtcNow;
				_pendingSessions++;
			}
		}

		/// <summary>Counts one kill of the given actor, split by monster class and per monster type.</summary>
		public void RecordKill(Actor target)
		{
			if (target == null) return;

			lock (_lock)
			{
				_pendingKills++;

				if (target is Unique) _pendingKillsUnique++;
				else if (target is Boss) _pendingKillsBoss++;
				else if (target is Rare) _pendingKillsRare++;
				else if (target is Champion) _pendingKillsChampion++;
				else _pendingKillsNormal++;

				if (target is Champion or Rare or Boss or Unique)
					_pendingKillsElite++;

				var sno = (int)target.SNO;
				if (sno > 0)
				{
					var name = target.NameSNO.ToString();
					_pendingMonsterKills.TryGetValue(sno, out var existing);
					_pendingMonsterKills[sno] = (string.IsNullOrEmpty(name) ? existing.Name : name, existing.Count + 1);
				}
			}
		}

		public void RecordGold(int amount)
		{
			if (amount <= 0) return;
			lock (_lock) _pendingGold += amount;
		}

		public void RecordDeath()
		{
			lock (_lock) _pendingDeaths++;
		}

		public void RecordChest()
		{
			lock (_lock) _pendingChests++;
		}

		public void RecordEvent()
		{
			lock (_lock) _pendingEvents++;
		}

		/// <summary>Counts one newly collected lore book.</summary>
		public void RecordLore()
		{
			lock (_lock) _pendingLore++;
		}

		/// <summary>Adds the elapsed part of the current session to the pending playtime.</summary>
		public void AccumulateSessionTime()
		{
			lock (_lock)
			{
				if (_sessionStart == DateTime.MinValue) return;

				var now = DateTime.UtcNow;
				var elapsed = (long)(now - _sessionStart).TotalSeconds;
				if (elapsed <= 0) return;

				_pendingSeconds += elapsed;
				_sessionStart = now;
			}
		}

		public bool HasPending
		{
			get
			{
				lock (_lock)
					return _pendingGold > 0 || _pendingSeconds > 0 || _pendingSessions > 0 || _pendingKills > 0 ||
					       _pendingDeaths > 0 || _pendingChests > 0 || _pendingEvents > 0 || _pendingLore > 0 ||
					       _pendingMonsterKills.Count > 0;
			}
		}

		/// <summary>Guards against running two database writes at the same time.</summary>
		private int _persisting;

		/// <summary>
		/// Writes the pending counters to the database, off the game thread. The game loop calls this
		/// every 30 s; a synchronous NHibernate round trip there showed up as 60-90 ms tick spikes.
		/// While a write is in flight the next call returns immediately - the counters keep
		/// accumulating and are picked up by the following flush, so nothing is lost.
		/// </summary>
		public void Flush()
		{
			if (System.Threading.Interlocked.CompareExchange(ref _persisting, 1, 0) != 0)
				return;

			try
			{
				System.Threading.Tasks.Task.Run(() =>
				{
					try
					{
						FlushCore();
					}
					catch (Exception e)
					{
						Logger.ErrorException(e, "Background statistics flush failed for toon {0}:", _toonId);
					}
					finally
					{
						System.Threading.Interlocked.Exchange(ref _persisting, 0);
					}
				});
			}
			catch (Exception)
			{
				System.Threading.Interlocked.Exchange(ref _persisting, 0);
				throw;
			}
		}

		/// <summary>Writes the pending counters synchronously; used when a player leaves the game.</summary>
		public void FlushSynchronously()
		{
			FlushCore();
		}

		/// <summary>Writes all pending counters to the database (blocking).</summary>
		private void FlushCore()
		{
			AccumulateSessionTime();

			lock (_lock)
			{
				if (_pendingGold == 0 && _pendingSeconds == 0 && _pendingSessions == 0 && _pendingKills == 0 &&
				    _pendingDeaths == 0 && _pendingChests == 0 && _pendingEvents == 0 && _pendingLore == 0 &&
				    _pendingMonsterKills.Count == 0)
					return;

				var gold = _pendingGold;
				var seconds = _pendingSeconds;
				var sessions = _pendingSessions;
				var kills = _pendingKills;
				var normal = _pendingKillsNormal;
				var elite = _pendingKillsElite;
				var champion = _pendingKillsChampion;
				var rare = _pendingKillsRare;
				var boss = _pendingKillsBoss;
				var unique = _pendingKillsUnique;
				var deaths = _pendingDeaths;
				var chests = _pendingChests;
				var events = _pendingEvents;
				var lore = _pendingLore;
				var monsterKills = _pendingMonsterKills.ToList();

				_pendingGold = 0;
				_pendingSeconds = 0;
				_pendingSessions = 0;
				_pendingKills = 0;
				_pendingKillsNormal = 0;
				_pendingKillsElite = 0;
				_pendingKillsChampion = 0;
				_pendingKillsRare = 0;
				_pendingKillsBoss = 0;
				_pendingKillsUnique = 0;
				_pendingDeaths = 0;
				_pendingChests = 0;
				_pendingEvents = 0;
				_pendingLore = 0;
				_pendingMonsterKills.Clear();

				try
				{
					DBSessions.SessionExecute(session =>
					{
						var toon = session.Get<DBToon>(_toonId);
						if (toon == null)
						{
							Logger.Warn("Statistics for unknown toon {0} dropped.", _toonId);
							return 0;
						}

						var isNew = false;
						var stats = session.Query<DBToonStatistics>().FirstOrDefault(s => s.DBToon.Id == _toonId);
						if (stats == null)
						{
							stats = new DBToonStatistics
							{
								DBToon = toon,
								FirstPlayed = DateTime.UtcNow,
								LastPlayed = DateTime.UtcNow
							};
							isNew = true;
						}

						stats.GoldCollected += gold;
						stats.PlayedSeconds += seconds;
						stats.Sessions += sessions;
						stats.KillsTotal += kills;
						stats.KillsNormal += normal;
						stats.KillsElite += elite;
						stats.KillsChampion += champion;
						stats.KillsRare += rare;
						stats.KillsBoss += boss;
						stats.KillsUnique += unique;
						stats.Deaths += deaths;
						stats.ChestsOpened += chests;
						stats.EventsCompleted += events;
						stats.LoreCollected += lore;
						stats.LastPlayed = DateTime.UtcNow;
						stats.Updated = DateTime.UtcNow;

						if (isNew)
							session.Insert(stats);
						else
							session.Update(stats);

						foreach (var entry in monsterKills)
						{
							var row = session.Query<DBMonsterKill>()
								.FirstOrDefault(k => k.DBToon.Id == _toonId && k.MonsterSno == entry.Key);
							if (row == null)
							{
								row = new DBMonsterKill
								{
									DBToon = toon,
									MonsterSno = entry.Key,
									MonsterName = entry.Value.Name ?? string.Empty,
									Kills = entry.Value.Count,
									Updated = DateTime.UtcNow
								};
								session.Insert(row);
							}
							else
							{
								row.Kills += entry.Value.Count;
								if (!string.IsNullOrEmpty(entry.Value.Name))
									row.MonsterName = entry.Value.Name;
								row.Updated = DateTime.UtcNow;
								session.Update(row);
							}
						}

						return 0;
					});
				}
				catch (Exception e)
				{
					// Re-add the counters so a failed flush does not silently lose statistics.
					_pendingGold += gold;
					_pendingSeconds += seconds;
					_pendingSessions += sessions;
					_pendingKills += kills;
					_pendingKillsNormal += normal;
					_pendingKillsElite += elite;
					_pendingKillsChampion += champion;
					_pendingKillsRare += rare;
					_pendingKillsBoss += boss;
					_pendingKillsUnique += unique;
					_pendingDeaths += deaths;
					_pendingChests += chests;
					_pendingEvents += events;
					_pendingLore += lore;

					foreach (var entry in monsterKills)
					{
						_pendingMonsterKills.TryGetValue(entry.Key, out var existing);
						_pendingMonsterKills[entry.Key] = (entry.Value.Name ?? existing.Name, existing.Count + entry.Value.Count);
					}

					Logger.ErrorException(e, "Failed to persist statistics for toon {0}:", _toonId);
				}
			}
		}

		/// <summary>Reads the persisted statistics of a character straight from the database.</summary>
		public static DBToonStatistics Load(ulong toonId)
		{
			return DBSessions.SessionExecute(session =>
				session.Query<DBToonStatistics>().FirstOrDefault(s => s.DBToon.Id == toonId));
		}

		/// <summary>Reads the per-monster kills of a character, most killed first.</summary>
		public static List<DBMonsterKill> LoadMonsterKills(ulong toonId, int limit = 10)
		{
			return DBSessions.SessionExecute(session =>
				session.Query<DBMonsterKill>()
					.Where(k => k.DBToon.Id == toonId)
					.OrderByDescending(k => k.Kills)
					.Take(limit)
					.ToList());
		}
	}
}
