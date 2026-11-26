using DiIiS_NA.GameServer.CommandManager;
using Xunit;

namespace DiIiS.Tests.GameServer;

/// <summary>
/// Tests for <see cref="CommandManager.ExtractCommandAndParameters"/>.
///
/// The method strips the command prefix character (default <c>!</c>),
/// returns the lower-cased command token, and the remainder of the line
/// as the parameters string.
/// </summary>
public class CommandManagerTests
{
    // ── Valid command lines ───────────────────────────────────────────────────

    [Fact]
    public void ExtractCommandAndParameters_CommandOnly_ExtractsNameAndEmptyParams()
    {
        bool ok = CommandManager.ExtractCommandAndParameters("!test", out var cmd, out var parameters);

        Assert.True(ok);
        Assert.Equal("test", cmd);
        Assert.Equal(string.Empty, parameters);
    }

    [Fact]
    public void ExtractCommandAndParameters_CommandWithParams_ExtractsBoth()
    {
        bool ok = CommandManager.ExtractCommandAndParameters(
            "!account add user@ pass Tag", out var cmd, out var parameters);

        Assert.True(ok);
        Assert.Equal("account", cmd);
        Assert.Equal("add user@ pass Tag", parameters);
    }

    [Fact]
    public void ExtractCommandAndParameters_CommandNameIsCaseNormalized()
    {
        bool ok = CommandManager.ExtractCommandAndParameters(
            "!ACCOUNT add user@ pass Tag", out var cmd, out var _);

        Assert.True(ok);
        Assert.Equal("account", cmd);
    }

    [Fact]
    public void ExtractCommandAndParameters_LeadingWhitespace_IsStripped()
    {
        bool ok = CommandManager.ExtractCommandAndParameters(
            "   !levelup 10", out var cmd, out var parameters);

        Assert.True(ok);
        Assert.Equal("levelup", cmd);
        Assert.Equal("10", parameters);
    }

    [Fact]
    public void ExtractCommandAndParameters_TrailingWhitespace_IsStrippedFromParams()
    {
        bool ok = CommandManager.ExtractCommandAndParameters(
            "!spawn 6632  ", out var cmd, out var parameters);

        Assert.True(ok);
        Assert.Equal("spawn", cmd);
        Assert.Equal("6632", parameters);
    }

    [Fact]
    public void ExtractCommandAndParameters_CommandWithoutParams_HasEmptyParams()
    {
        bool ok = CommandManager.ExtractCommandAndParameters("!help", out var cmd, out var _);

        Assert.True(ok);
        Assert.Equal("help", cmd);
    }

    // ── Invalid command lines ────────────────────────────────────────────────

    [Fact]
    public void ExtractCommandAndParameters_EmptyString_ReturnsFalse()
    {
        bool ok = CommandManager.ExtractCommandAndParameters(string.Empty, out _, out _);

        Assert.False(ok);
    }

    [Fact]
    public void ExtractCommandAndParameters_WhitespaceOnly_ReturnsFalse()
    {
        bool ok = CommandManager.ExtractCommandAndParameters("   ", out _, out _);

        Assert.False(ok);
    }

    [Fact]
    public void ExtractCommandAndParameters_NoPrefixChar_ReturnsFalse()
    {
        bool ok = CommandManager.ExtractCommandAndParameters("account add", out _, out _);

        Assert.False(ok);
    }

    [Fact]
    public void ExtractCommandAndParameters_PrefixInMiddle_ReturnsFalse()
    {
        bool ok = CommandManager.ExtractCommandAndParameters("foo!bar", out _, out _);

        Assert.False(ok);
    }

    // ── Output variable state on failure ─────────────────────────────────────

    [Fact]
    public void ExtractCommandAndParameters_OnFailure_OutputsAreEmpty()
    {
        CommandManager.ExtractCommandAndParameters("no prefix here", out var cmd, out var parameters);

        Assert.Equal(string.Empty, cmd);
        Assert.Equal(string.Empty, parameters);
    }
}
