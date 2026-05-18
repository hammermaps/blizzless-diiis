using System;
using System.Collections.Generic;
using System.Linq;
using DiIiS_NA.Core.Extensions;
using DiIiS_NA.Core.MPQ.FileFormats;
using DiIiS_NA.GameServer.Core.Types.Math;
using DiIiS_NA.D3_GameServer.Core.Types.SNO;

namespace DiIiS_NA.GameServer.GSSystem.GeneratorsSystem
{
	public enum DungeonLayoutShape
	{
		Small,
		Medium,
		Large,
		Branching,
		Looped
	}

	public class DungeonGenerationOptions
	{
		private const double SmallShapeThreshold = 0.20;
		private const double MediumShapeThreshold = 0.55;
		private const double LargeShapeThreshold = 0.75;
		private const double BranchingShapeThreshold = 0.92;
		private const int SmallChunkSizeThreshold = 120;
		private const int SmallChunkPathBonus = 2;
		private const int GeneratedWorldPathBonus = 8;
		private const int MinimumMaxChunkCount = 12;
		private const int AbsoluteMaxChunkCount = 40;
		private const int PathToChunkMultiplier = 4;

		public DungeonLayoutShape Shape { get; private set; }
		public int MainPathLength { get; private set; }
		public int MinChunkCount { get; private set; }
		public int MaxChunkCount { get; private set; }
		public int MaxAttempts { get; private set; }
		public float BranchingChance { get; private set; }
		public float LoopChance { get; private set; }
		public float EventRoomChance { get; private set; }
		public float TreasureRoomChance { get; private set; }
		public float EliteRoomChance { get; private set; }

		public static DungeonGenerationOptions Create(WorldSno worldSno, int levelArea, int chunkSize, int tileCount, Random random)
		{
			var shapeRoll = random.NextDouble();
			var shape = shapeRoll switch
			{
				< SmallShapeThreshold => DungeonLayoutShape.Small,
				< MediumShapeThreshold => DungeonLayoutShape.Medium,
				< LargeShapeThreshold => DungeonLayoutShape.Large,
				< BranchingShapeThreshold => DungeonLayoutShape.Branching,
				_ => DungeonLayoutShape.Looped
			};

			var basePath = shape switch
			{
				DungeonLayoutShape.Small => random.Next(4, 7),
				DungeonLayoutShape.Medium => random.Next(6, 10),
				DungeonLayoutShape.Large => random.Next(9, 14),
				DungeonLayoutShape.Branching => random.Next(7, 12),
				DungeonLayoutShape.Looped => random.Next(8, 13),
				_ => random.Next(6, 10)
			};

			if (chunkSize <= SmallChunkSizeThreshold)
				basePath += SmallChunkPathBonus;
			if (worldSno.IsGenerated())
				basePath += GeneratedWorldPathBonus;

			var maxChunkCount = Math.Max(MinimumMaxChunkCount, Math.Min(AbsoluteMaxChunkCount, Math.Max(tileCount, basePath * PathToChunkMultiplier)));
			return new DungeonGenerationOptions
			{
				Shape = shape,
				MainPathLength = basePath,
				MinChunkCount = Math.Max(4, basePath + 2),
				MaxChunkCount = maxChunkCount,
				MaxAttempts = worldSno.IsGenerated() ? 8 : 4,
				BranchingChance = shape == DungeonLayoutShape.Branching ? 0.65f : 0.35f,
				LoopChance = shape == DungeonLayoutShape.Looped ? 0.35f : 0.10f,
				EventRoomChance = levelArea > 0 ? 0.12f : 0.08f,
				TreasureRoomChance = 0.08f,
				EliteRoomChance = worldSno.IsGenerated() ? 0.18f : 0.10f
			};
		}
	}

	public class DungeonGenerationContext
	{
		private const float RarityPenaltyBase = 1.0f;
		private const float ExitRoomBonus = 0.10f;
		private const float MinimumTileWeight = 0.1f;

		private readonly Dictionary<int, int> _tileUseCounts = new();

		public int Seed { get; }
		public Random Random { get; }
		public DungeonGenerationOptions Options { get; }

		public DungeonGenerationContext(int seed, DungeonGenerationOptions options)
		{
			Seed = seed;
			Random = new Random(seed);
			Options = options;
		}

		public TileInfo PickTile(IReadOnlyCollection<TileInfo> tiles)
		{
			return PickTile(tiles, tile => Math.Max(tile.Probability, 1));
		}

		public TileInfo PickTile(IReadOnlyCollection<TileInfo> tiles, Func<TileInfo, float> baseWeight)
		{
			if (tiles == null || tiles.Count == 0)
				return null;

			// Lower the weight of scenes already used in this layout to reduce repetition,
			// while lightly boosting special room tiles so event/elite rooms appear more often.
			var weighted = tiles.Select(tile =>
			{
				_tileUseCounts.TryGetValue(tile.SNOScene, out var useCount);
				var rarityPenalty = RarityPenaltyBase / (RarityPenaltyBase + useCount);
				var roomBonus = tile.TileType switch
				{
					(int)TileTypes.EventTile1 => Options.EventRoomChance,
					(int)TileTypes.EventTile2 => Options.EliteRoomChance,
					(int)TileTypes.Exit => ExitRoomBonus,
					_ => 0.0f
				};
				return new
				{
					Tile = tile,
					Weight = Math.Max(MinimumTileWeight, baseWeight(tile)) * rarityPenalty * (RarityPenaltyBase + roomBonus)
				};
			}).ToList();

			var totalWeight = weighted.Sum(entry => entry.Weight);
			var roll = Random.NextDouble() * totalWeight;
			var cumulative = 0.0;
			foreach (var entry in weighted)
			{
				cumulative += entry.Weight;
				if (roll <= cumulative)
				{
					RecordTile(entry.Tile);
					return entry.Tile;
				}
			}

			var fallback = weighted.Last().Tile;
			RecordTile(fallback);
			return fallback;
		}

		public void RecordTile(TileInfo tile)
		{
			if (tile == null)
				return;

			if (!_tileUseCounts.ContainsKey(tile.SNOScene))
				_tileUseCounts[tile.SNOScene] = 0;
			_tileUseCounts[tile.SNOScene]++;
		}
	}

	public class DungeonGenerationMetrics
	{
		private const int ReachabilityScore = 100;
		private const int ExitPresenceScore = 50;
		private const int TileCountScore = 2;
		private const int MissingMinimumChunkPenalty = 3;
		private const int OpenExitPenalty = 10;
		private const int DuplicateScenePenalty = 2;
		private const int MaxRewardedDeadEnds = 6;
		private const int DeadEndScore = 2;

		public int TileCount { get; set; }
		public int FillerCount { get; set; }
		public int ExitCount { get; set; }
		public int DeadEndCount { get; set; }
		public int OpenExitCount { get; set; }
		public int DuplicateSceneCount { get; set; }
		public bool EntranceReachableToExit { get; set; }

		public int Score(DungeonGenerationOptions options)
		{
			var score = 0;
			score += EntranceReachableToExit ? ReachabilityScore : -ReachabilityScore;
			score += ExitCount > 0 ? ExitPresenceScore : -ExitPresenceScore;
			score += Math.Min(TileCount, options.MaxChunkCount) * TileCountScore;
			score -= Math.Abs(options.MinChunkCount - Math.Min(TileCount, options.MinChunkCount)) * MissingMinimumChunkPenalty;
			score -= OpenExitCount * OpenExitPenalty;
			score -= DuplicateSceneCount * DuplicateScenePenalty;
			score += Math.Min(DeadEndCount, MaxRewardedDeadEnds) * DeadEndScore;
			return score;
		}

		public bool IsUsable(DungeonGenerationOptions options)
		{
			return EntranceReachableToExit &&
			       ExitCount > 0 &&
			       TileCount >= options.MinChunkCount &&
			       TileCount <= options.MaxChunkCount &&
			       OpenExitCount == 0;
		}
	}

	public static class DungeonLayoutValidator
	{
		public static DungeonGenerationMetrics Analyze(Dictionary<Vector3D, TileInfo> worldTiles, int chunkSize)
		{
			var metrics = new DungeonGenerationMetrics();
			var playableTiles = worldTiles.Where(pair => pair.Value != null && pair.Value.TileType != (int)TileTypes.Filler).ToDictionary(pair => pair.Key, pair => pair.Value);
			metrics.TileCount = playableTiles.Count;
			metrics.FillerCount = worldTiles.Count(pair => pair.Value != null && pair.Value.TileType == (int)TileTypes.Filler);
			metrics.ExitCount = playableTiles.Count(pair => pair.Value.TileType == (int)TileTypes.Exit);
			metrics.DeadEndCount = playableTiles.Count(pair => CountExits(pair.Value.ExitDirectionBits) == 1);
			metrics.DuplicateSceneCount = playableTiles.GroupBy(pair => pair.Value.SNOScene).Sum(group =>
			{
				var count = group.Count();
				return Math.Max(0, count - 1);
			});
			metrics.OpenExitCount = CountOpenExits(playableTiles, chunkSize);
			metrics.EntranceReachableToExit = HasPathFromEntranceToExit(playableTiles, chunkSize);
			return metrics;
		}

		private static int CountExits(int exitDirectionBits)
		{
			var count = 0;
			foreach (TileExits exit in Enum.GetValues(typeof(TileExits)))
				if ((exitDirectionBits & (int)exit) > 0)
					count++;
			return count;
		}

		private static int CountOpenExits(Dictionary<Vector3D, TileInfo> tiles, int chunkSize)
		{
			var openExits = 0;
			foreach (var tile in tiles)
			{
				foreach (TileExits exit in Enum.GetValues(typeof(TileExits)))
				{
					if ((tile.Value.ExitDirectionBits & (int)exit) == 0)
						continue;

					var adjacentPosition = GetAdjacentPosition(tile.Key, exit, chunkSize);
					if (!tiles.TryGetValue(adjacentPosition, out var adjacentTile) || (adjacentTile.ExitDirectionBits & (int)GetOppositeExit(exit)) == 0)
						openExits++;
				}
			}
			return openExits;
		}

		private static bool HasPathFromEntranceToExit(Dictionary<Vector3D, TileInfo> tiles, int chunkSize)
		{
			var entrance = tiles.FirstOrDefault(pair => pair.Value.TileType == (int)TileTypes.Entrance);
			if (entrance.Value == null)
				return false;

			var visited = new HashSet<Vector3D>();
			var queue = new Queue<Vector3D>();
			queue.Enqueue(entrance.Key);
			visited.Add(entrance.Key);

			while (queue.Count > 0)
			{
				var position = queue.Dequeue();
				var tile = tiles[position];
				if (tile.TileType == (int)TileTypes.Exit)
					return true;

				foreach (TileExits exit in Enum.GetValues(typeof(TileExits)))
				{
					if ((tile.ExitDirectionBits & (int)exit) == 0)
						continue;

					var adjacentPosition = GetAdjacentPosition(position, exit, chunkSize);
					if (!tiles.TryGetValue(adjacentPosition, out var adjacentTile) || (adjacentTile.ExitDirectionBits & (int)GetOppositeExit(exit)) == 0)
						continue;

					if (visited.Add(adjacentPosition))
						queue.Enqueue(adjacentPosition);
				}
			}

			return false;
		}

		private static Vector3D GetAdjacentPosition(Vector3D position, TileExits exit, int chunkSize)
		{
			return exit switch
			{
				TileExits.East => new Vector3D(position.X - chunkSize, position.Y, position.Z),
				TileExits.West => new Vector3D(position.X + chunkSize, position.Y, position.Z),
				TileExits.North => new Vector3D(position.X, position.Y + chunkSize, position.Z),
				TileExits.South => new Vector3D(position.X, position.Y - chunkSize, position.Z),
				_ => position
			};
		}

		private static TileExits GetOppositeExit(TileExits exit)
		{
			return exit switch
			{
				TileExits.East => TileExits.West,
				TileExits.West => TileExits.East,
				TileExits.North => TileExits.South,
				TileExits.South => TileExits.North,
				_ => exit
			};
		}
	}
}
