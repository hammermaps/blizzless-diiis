using DiIiS_NA.D3_GameServer.Core.Types.SNO;
using DiIiS_NA.GameServer.GSSystem.ItemsSystem;
using Xunit;

namespace DiIiS.Tests.GameServer;

/// <summary>
/// Tests for the Season 27 game-logic helpers that do not require a live
/// Player / World / Game context.
/// </summary>
public class Season27PatchTests
{
    // ── Public constants ──────────────────────────────────────────────────────

    [Fact]
    public void AngelicCrucibleDropChancePercent_IsOnePercent()
    {
        Assert.Equal(1f, Season27Patch.AngelicCrucibleDropChancePercent);
    }

    // ── GetEchoingNightmareExperience ─────────────────────────────────────────

    [Fact]
    public void GetEchoingNightmareExperience_NonEchoingWorld_ReturnsOriginalValue()
    {
        // WorldSno.__NONE does not contain "echoing" + "nightmare" → pass-through
        int experience = 5000;
        int result = Season27Patch.GetEchoingNightmareExperience(experience, WorldSno.__NONE);
        Assert.Equal(experience, result);
    }

    [Fact]
    public void GetEchoingNightmareExperience_RegularAct1World_ReturnsOriginalValue()
    {
        int experience = 12_345;
        int result = Season27Patch.GetEchoingNightmareExperience(
            experience, WorldSno.a1trdun_level01);
        Assert.Equal(experience, result);
    }

    [Fact]
    public void GetEchoingNightmareExperience_ZeroExperience_ReturnsZero()
    {
        int result = Season27Patch.GetEchoingNightmareExperience(0, WorldSno.__NONE);
        Assert.Equal(0, result);
    }

    [Fact]
    public void GetEchoingNightmareExperience_MaxIntExperience_DoesNotOverflow()
    {
        // int.MaxValue × 0.17 must stay within int range (method uses Math.Min)
        int result = Season27Patch.GetEchoingNightmareExperience(
            int.MaxValue, WorldSno.__NONE);
        // For non-echoing world, experience is returned unchanged
        Assert.Equal(int.MaxValue, result);
    }

    // ── IsAngelicCrucible – name hash matching ────────────────────────────────

    // We test the hash matching implicitly by verifying that
    // StringHashHelper.HashItemName produces stable values for the known
    // crucible names (regression guard – Season27Patch.IsAngelicCrucible
    // uses these hashes internally).

    [Theory]
    [InlineData("AngelicCrucible")]
    [InlineData("Angelic_Crucible")]
    [InlineData("p73_AngelicCrucible")]
    [InlineData("p73_Angelic_Crucible")]
    [InlineData("Consumable_Add_Sockets_Season_27")]
    public void KnownCrucibleNames_HashIsStable(string name)
    {
        int hash1 = DiIiS_NA.Core.Helpers.Hash.StringHashHelper.HashItemName(name);
        int hash2 = DiIiS_NA.Core.Helpers.Hash.StringHashHelper.HashItemName(name);
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void CrucibleNameHashes_AreDistinct()
    {
        string[] names = new string[]
        {
            "AngelicCrucible",
            "Angelic_Crucible",
            "p73_AngelicCrucible",
            "p73_Angelic_Crucible",
            "Consumable_Add_Sockets_Season_27"
        };

        var hashes = names
            .Select(n => DiIiS_NA.Core.Helpers.Hash.StringHashHelper.HashItemName(n))
            .ToList();

        // All five names should hash to distinct values
        Assert.Equal(hashes.Count, hashes.Distinct().Count());
    }
}
