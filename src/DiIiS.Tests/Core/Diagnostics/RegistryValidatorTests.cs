using System.Collections.Generic;
using System.Reflection;
using DiIiS_NA.Core.Diagnostics;
using DiIiS_NA.D3_GameServer.Core.Types.SNO;
using DiIiS_NA.GameServer.GSSystem.ActorSystem;
using DiIiS_NA.GameServer.MessageSystem;
using Xunit;

namespace DiIiS.Tests.Core.Diagnostics;

/// <summary>
/// Unit tests for <see cref="RegistryValidator"/>.
///
/// Each group has:
///  • a positive case (no issues expected)
///  • a negative case with synthetic duplicate / invalid data
///  • an assembly-level regression case (real assembly must have no issues)
/// </summary>
public class RegistryValidatorTests
{
    // ── Actor SNO duplicate detection ─────────────────────────────────────────

    [Fact]
    public void FindActorSnoDuplicates_EmptyList_ReturnsNoIssues()
    {
        var issues = RegistryValidator.FindActorSnoDuplicates(
            new List<(ActorSno, string)>());

        Assert.Empty(issues);
    }

    [Fact]
    public void FindActorSnoDuplicates_NoDuplicates_ReturnsNoIssues()
    {
        var registrations = new List<(ActorSno, string)>
        {
            (ActorSno._waypoint,    "TypeA"),
            (ActorSno._townportal,  "TypeB"),
        };

        var issues = RegistryValidator.FindActorSnoDuplicates(registrations);

        Assert.Empty(issues);
    }

    [Fact]
    public void FindActorSnoDuplicates_WithDuplicate_ReturnsOneIssue()
    {
        var registrations = new List<(ActorSno, string)>
        {
            (ActorSno._waypoint, "TypeA"),
            (ActorSno._waypoint, "TypeB"),   // duplicate
        };

        var issues = RegistryValidator.FindActorSnoDuplicates(registrations);

        Assert.Single(issues);
        Assert.Contains("_waypoint", issues[0]);
        Assert.Contains("TypeB", issues[0]);
        Assert.Contains("TypeA", issues[0]);
    }

    [Fact]
    public void FindActorSnoDuplicates_MultipleDuplicates_ReturnsAllIssues()
    {
        var registrations = new List<(ActorSno, string)>
        {
            (ActorSno._waypoint,   "TypeA"),
            (ActorSno._waypoint,   "TypeB"),
            (ActorSno._townportal, "TypeC"),
            (ActorSno._townportal, "TypeD"),
        };

        var issues = RegistryValidator.FindActorSnoDuplicates(registrations);

        Assert.Equal(2, issues.Count);
    }

    /// <summary>
    /// Regression: the live assembly must have no duplicate HandledSNO registrations.
    /// </summary>
    [Fact]
    public void FindActorSnoDuplicates_ActualAssembly_ReturnsNoIssues()
    {
        var assembly = typeof(ActorFactory).Assembly;
        var issues = RegistryValidator.FindActorSnoDuplicates(assembly);

        Assert.Empty(issues);
    }

    // ── GameMessage opcode duplicate detection ────────────────────────────────

    [Fact]
    public void FindOpcodeDuplicates_EmptyList_ReturnsNoIssues()
    {
        var issues = RegistryValidator.FindOpcodeDuplicates(
            new List<(Opcodes, string)>());

        Assert.Empty(issues);
    }

    [Fact]
    public void FindOpcodeDuplicates_NoDuplicates_ReturnsNoIssues()
    {
        var registrations = new List<(Opcodes, string)>
        {
            (Opcodes.Ping,          "MsgA"),
            (Opcodes.GameTickMessage, "MsgB"),
        };

        var issues = RegistryValidator.FindOpcodeDuplicates(registrations);

        Assert.Empty(issues);
    }

    [Fact]
    public void FindOpcodeDuplicates_WithDuplicate_ReturnsOneIssue()
    {
        var registrations = new List<(Opcodes, string)>
        {
            (Opcodes.Ping, "MsgA"),
            (Opcodes.Ping, "MsgB"),   // duplicate
        };

        var issues = RegistryValidator.FindOpcodeDuplicates(registrations);

        Assert.Single(issues);
        Assert.Contains("Ping", issues[0]);
        Assert.Contains("MsgB", issues[0]);
        Assert.Contains("MsgA", issues[0]);
    }

    /// <summary>
    /// Regression: the live assembly must have no duplicate opcode registrations.
    /// </summary>
    [Fact]
    public void FindOpcodeDuplicates_ActualAssembly_ReturnsNoIssues()
    {
        var assembly = typeof(GameMessage).Assembly;
        var issues = RegistryValidator.FindOpcodeDuplicates(assembly);

        Assert.Empty(issues);
    }

    // ── Command group registry validation ────────────────────────────────────

    [Fact]
    public void FindCommandGroupIssues_EmptyList_ReturnsNoIssues()
    {
        var issues = RegistryValidator.FindCommandGroupIssues(
            new List<(string, string)>());

        Assert.Empty(issues);
    }

    [Fact]
    public void FindCommandGroupIssues_ValidNames_ReturnsNoIssues()
    {
        var groups = new List<(string, string)>
        {
            ("account",   "AccountGroup"),
            ("levelup",   "LevelUpGroup"),
        };

        var issues = RegistryValidator.FindCommandGroupIssues(groups);

        Assert.Empty(issues);
    }

    [Fact]
    public void FindCommandGroupIssues_NameWithSpaces_ReturnsIssue()
    {
        var groups = new List<(string, string)>
        {
            ("bad name", "SomeGroup"),
        };

        var issues = RegistryValidator.FindCommandGroupIssues(groups);

        Assert.Single(issues);
        Assert.Contains("bad name", issues[0]);
        Assert.Contains("spaces", issues[0]);
    }

    [Fact]
    public void FindCommandGroupIssues_DuplicateName_ReturnsIssue()
    {
        var groups = new List<(string, string)>
        {
            ("account", "GroupA"),
            ("account", "GroupB"),   // duplicate
        };

        var issues = RegistryValidator.FindCommandGroupIssues(groups);

        Assert.Single(issues);
        Assert.Contains("account", issues[0]);
        Assert.Contains("Duplicate", issues[0]);
    }

    [Fact]
    public void FindCommandGroupIssues_SpaceAndDuplicate_ReturnsThreeIssues()
    {
        var groups = new List<(string, string)>
        {
            ("bad cmd", "GroupA"),
            ("bad cmd", "GroupB"),
        };

        // First entry:  spaces issue
        // Second entry: spaces issue + duplicate issue (seen for "bad cmd" twice)
        var issues = RegistryValidator.FindCommandGroupIssues(groups);

        Assert.Equal(3, issues.Count);
    }

    /// <summary>
    /// Regression: the live assembly must have no command group name issues.
    /// </summary>
    [Fact]
    public void FindCommandGroupIssues_ActualAssembly_ReturnsNoIssues()
    {
        var assembly = typeof(ActorFactory).Assembly;
        var issues = RegistryValidator.FindCommandGroupIssues(assembly);

        Assert.Empty(issues);
    }

    // ── Configuration sanity validation ──────────────────────────────────────

    [Fact]
    public void FindConfigIssues_AllValid_ReturnsNoIssues()
    {
        var issues = RegistryValidator.FindConfigIssues(
            gameServerBindIp:   "127.0.0.1", gameServerPort:   1345,
            loginServerBindIp:  "127.0.0.1", loginServerPort:  1119,
            restIp:             "127.0.0.1", restPort:          8081);

        Assert.Empty(issues);
    }

    [Fact]
    public void FindConfigIssues_EmptyGameServerIp_ReturnsIssue()
    {
        var issues = RegistryValidator.FindConfigIssues(
            gameServerBindIp:   "", gameServerPort:   1345,
            loginServerBindIp:  "127.0.0.1", loginServerPort: 1119,
            restIp:             "127.0.0.1", restPort:         8081);

        Assert.Single(issues);
        Assert.Contains("GameServer BindIP", issues[0]);
    }

    [Fact]
    public void FindConfigIssues_WhitespaceIp_ReturnsIssue()
    {
        var issues = RegistryValidator.FindConfigIssues(
            gameServerBindIp:   "   ", gameServerPort:   1345,
            loginServerBindIp:  "127.0.0.1", loginServerPort: 1119,
            restIp:             "127.0.0.1", restPort:         8081);

        Assert.Single(issues);
        Assert.Contains("GameServer BindIP", issues[0]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    [InlineData(99999)]
    public void FindConfigIssues_InvalidGameServerPort_ReturnsIssue(int port)
    {
        var issues = RegistryValidator.FindConfigIssues(
            gameServerBindIp:   "127.0.0.1", gameServerPort:   port,
            loginServerBindIp:  "127.0.0.1", loginServerPort:  1119,
            restIp:             "127.0.0.1", restPort:          8081);

        Assert.Single(issues);
        Assert.Contains("GameServer Port", issues[0]);
        Assert.Contains(port.ToString(), issues[0]);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(80)]
    [InlineData(1345)]
    [InlineData(65535)]
    public void FindConfigIssues_ValidGameServerPort_ReturnsNoIssue(int port)
    {
        var issues = RegistryValidator.FindConfigIssues(
            gameServerBindIp:   "127.0.0.1", gameServerPort:   port,
            loginServerBindIp:  "127.0.0.1", loginServerPort:  1119,
            restIp:             "127.0.0.1", restPort:          8081);

        Assert.Empty(issues);
    }

    [Fact]
    public void FindConfigIssues_AllInvalid_ReturnsSixIssues()
    {
        var issues = RegistryValidator.FindConfigIssues(
            gameServerBindIp:   "",  gameServerPort:   0,
            loginServerBindIp:  "",  loginServerPort:  0,
            restIp:             "",  restPort:          0);

        Assert.Equal(6, issues.Count);
    }

    [Fact]
    public void FindConfigIssues_RestPortNegative_ReturnsIssue()
    {
        var issues = RegistryValidator.FindConfigIssues(
            gameServerBindIp:   "127.0.0.1", gameServerPort:   1345,
            loginServerBindIp:  "127.0.0.1", loginServerPort:  1119,
            restIp:             "127.0.0.1", restPort:         -1);

        Assert.Single(issues);
        Assert.Contains("REST server Port", issues[0]);
    }
}
