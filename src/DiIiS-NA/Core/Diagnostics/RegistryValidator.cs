using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DiIiS_NA.D3_GameServer.Core.Types.SNO;
using DiIiS_NA.GameServer.CommandManager;
using DiIiS_NA.GameServer.GSSystem.ActorSystem;
using DiIiS_NA.GameServer.MessageSystem;

namespace DiIiS_NA.Core.Diagnostics
{
	/// <summary>
	/// Provides pure-logic registry validation helpers used by
	/// <see cref="DebugStartupSelfTests"/> and the unit-test suite.
	/// Each method either accepts pre-built data (for fast unit testing) or an
	/// <see cref="Assembly"/> (for in-process startup validation).
	/// </summary>
	public static class RegistryValidator
	{
		// ── Actor SNO handler registry ─────────────────────────────────────────

		/// <summary>
		/// Finds duplicate <see cref="HandledSNOAttribute"/> SNO ids across the
		/// supplied (snoid, ownerTypeName) pairs.
		/// </summary>
		public static List<string> FindActorSnoDuplicates(
			IEnumerable<(ActorSno Sno, string OwnerTypeName)> registrations)
		{
			var issues = new List<string>();
			var seen = new Dictionary<ActorSno, string>();

			foreach (var (sno, owner) in registrations)
			{
				if (seen.TryGetValue(sno, out var existing))
					issues.Add(
						$"Duplicate HandledSNO {sno} on '{owner}' " +
						$"(already registered by '{existing}').");
				else
					seen[sno] = owner;
			}

			return issues;
		}

		/// <summary>
		/// Scans <paramref name="assembly"/> for all <see cref="Actor"/> subclasses
		/// with a <see cref="HandledSNOAttribute"/> and reports any duplicate SNO ids.
		/// </summary>
		public static List<string> FindActorSnoDuplicates(Assembly assembly)
		{
			var registrations = assembly.GetTypes()
				.Where(t => t.IsSubclassOf(typeof(Actor)))
				.SelectMany(t =>
				{
					var attr = t.GetCustomAttribute<HandledSNOAttribute>(true);
					if (attr == null) return Enumerable.Empty<(ActorSno, string)>();
					return attr.SNOIds.Select(sno => (sno, t.FullName ?? t.Name));
				});

			return FindActorSnoDuplicates(registrations);
		}

		// ── GameMessage opcode registry ────────────────────────────────────────

		/// <summary>
		/// Finds duplicate opcode mappings in the supplied (opcode, ownerTypeName) pairs.
		/// </summary>
		public static List<string> FindOpcodeDuplicates(
			IEnumerable<(Opcodes Opcode, string OwnerTypeName)> registrations)
		{
			var issues = new List<string>();
			var seen = new Dictionary<Opcodes, string>();

			foreach (var (opcode, owner) in registrations)
			{
				if (seen.TryGetValue(opcode, out var existing))
					issues.Add(
						$"Duplicate opcode {opcode} on '{owner}' " +
						$"(already registered by '{existing}').");
				else
					seen[opcode] = owner;
			}

			return issues;
		}

		/// <summary>
		/// Scans <paramref name="assembly"/> for all <see cref="GameMessage"/> subclasses
		/// with a <see cref="MessageAttribute"/> and reports any duplicate opcodes.
		/// </summary>
		public static List<string> FindOpcodeDuplicates(Assembly assembly)
		{
			var registrations = assembly.GetTypes()
				.Where(t => t.IsSubclassOf(typeof(GameMessage)))
				.SelectMany(t =>
					t.GetCustomAttributes<MessageAttribute>(true)
					 .SelectMany(attr => attr.Opcodes.Select(op => (op, t.FullName ?? t.Name))));

			return FindOpcodeDuplicates(registrations);
		}

		// ── Command group registry ─────────────────────────────────────────────

		/// <summary>
		/// Finds command-group name issues (spaces in name, duplicate names) in the
		/// supplied (groupName, ownerTypeName) pairs.
		/// </summary>
		public static List<string> FindCommandGroupIssues(
			IEnumerable<(string Name, string OwnerTypeName)> groups)
		{
			var issues = new List<string>();
			var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			foreach (var (name, owner) in groups)
			{
				if (name != null && name.Contains(' '))
					issues.Add($"Command group name '{name}' on '{owner}' contains spaces.");

				if (!seen.Add(name ?? string.Empty))
					issues.Add($"Duplicate command group name '{name}' on '{owner}'.");
			}

			return issues;
		}

		/// <summary>
		/// Scans <paramref name="assembly"/> for all <see cref="CommandGroup"/> subclasses
		/// with a <see cref="CommandGroupAttribute"/> and reports name collisions.
		/// </summary>
		public static List<string> FindCommandGroupIssues(Assembly assembly)
		{
			var groups = assembly.GetTypes()
				.Where(t => t.IsSubclassOf(typeof(CommandGroup)))
				.SelectMany(t =>
					t.GetCustomAttributes<CommandGroupAttribute>(true)
					 .Select(attr => (attr.Name, t.FullName ?? t.Name)));

			return FindCommandGroupIssues(groups);
		}

		// ── Configuration sanity ──────────────────────────────────────────────

		/// <summary>
		/// Validates that the six most critical server configuration values are sane
		/// (non-empty IPs and ports within the 1–65535 range).
		/// </summary>
		public static List<string> FindConfigIssues(
			string gameServerBindIp, int gameServerPort,
			string loginServerBindIp, int loginServerPort,
			string restIp, int restPort)
		{
			var issues = new List<string>();

			if (string.IsNullOrWhiteSpace(gameServerBindIp))
				issues.Add("GameServer BindIP is empty.");
			if (gameServerPort is < 1 or > 65535)
				issues.Add($"GameServer Port {gameServerPort} is out of valid range (1–65535).");

			if (string.IsNullOrWhiteSpace(loginServerBindIp))
				issues.Add("LoginServer BindIP is empty.");
			if (loginServerPort is < 1 or > 65535)
				issues.Add($"LoginServer Port {loginServerPort} is out of valid range (1–65535).");

			if (string.IsNullOrWhiteSpace(restIp))
				issues.Add("REST server IP is empty.");
			if (restPort is < 1 or > 65535)
				issues.Add($"REST server Port {restPort} is out of valid range (1–65535).");

			return issues;
		}
	}
}
