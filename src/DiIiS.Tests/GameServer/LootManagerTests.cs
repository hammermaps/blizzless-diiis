using System.Reflection;
using DiIiS_NA.GameServer.GSSystem.ItemsSystem;
using Xunit;

namespace DiIiS.Tests.GameServer;

/// <summary>
/// Tests for <see cref="LootManager"/>.
///
/// <c>GetLootQualityCore</c> is a private static method so it is invoked via
/// reflection.  <c>LootManager.Common</c> always returns 1; Uncommon/Rare/Epic
/// are derived from <see cref="DiIiS_NA.Core.Helpers.Math.FastRandom.Instance"/>
/// so their exact return values are non-deterministic – we test them via their
/// range constraints instead.
///
/// MonsterQuality codes:
///   0 = Normal, 1 = Champion, 2/4 = Rare/Unique, 7 = Boss
/// Difficulty codes:
///   0-3 = Normal/Hard/Expert/Master, 4-9 = T1-T6
/// </summary>
public class LootManagerTests
{
    private static readonly MethodInfo GetLootQualityCore =
        typeof(LootManager).GetMethod(
            "GetLootQualityCore",
            BindingFlags.NonPublic | BindingFlags.Static)!;

    private static int InvokeCore(int monsterQuality, int difficulty, float roll)
    {
        return (int)GetLootQualityCore.Invoke(null, new object[] { monsterQuality, difficulty, roll })!;
    }

    // ── Normal monster quality ────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void NormalMonster_LowDifficulty_LowRoll_ReturnsCommon(int difficulty)
    {
        // roll < 0.2 → Common (= 1)
        Assert.Equal(LootManager.Common, InvokeCore(0, difficulty, 0.1f));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void NormalMonster_LowDifficulty_MidRoll_ReturnsUncommonRange(int difficulty)
    {
        // 0.2 <= roll < 0.5 → Uncommon (3-4)
        int result = InvokeCore(0, difficulty, 0.35f);
        Assert.InRange(result, 3, 4);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void NormalMonster_LowDifficulty_HighRoll_ReturnsRareRange(int difficulty)
    {
        // 0.5 <= roll < 0.9984 → Rare (5-7)
        int result = InvokeCore(0, difficulty, 0.7f);
        Assert.InRange(result, 5, 7);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    public void NormalMonster_EpicThreshold_Roll1_ReturnsEpicRange(int difficulty)
    {
        // roll >= epic threshold → Epic (8-10)
        // Use roll = 0.9999f which exceeds the highest threshold
        int result = InvokeCore(0, difficulty, 0.9999f);
        Assert.InRange(result, 8, 10);
    }

    [Fact]
    public void NormalMonster_T6Difficulty_LowRoll_ReturnsCommon()
    {
        Assert.Equal(LootManager.Common, InvokeCore(0, 9, 0.1f));
    }

    // ── Champion quality ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    public void Champion_LowRoll_ReturnsCommon(int difficulty)
    {
        // roll < 0.08 → Common
        Assert.Equal(LootManager.Common, InvokeCore(1, difficulty, 0.05f));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    public void Champion_MidRoll_ReturnsUncommonRange(int difficulty)
    {
        // 0.08 <= roll < 0.5 → Uncommon (3-4)
        int result = InvokeCore(1, difficulty, 0.3f);
        Assert.InRange(result, 3, 4);
    }

    // ── Rare/Unique quality ───────────────────────────────────────────────────

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public void RareOrUnique_VeryLowRoll_ReturnsCommon(int monsterQuality)
    {
        // roll < 0.05 → Common
        Assert.Equal(LootManager.Common, InvokeCore(monsterQuality, 0, 0.02f));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public void RareOrUnique_LowMidRoll_ReturnsUncommonRange(int monsterQuality)
    {
        // 0.05 <= roll < 0.35 → Uncommon (3-4)
        int result = InvokeCore(monsterQuality, 0, 0.2f);
        Assert.InRange(result, 3, 4);
    }

    // ── Boss quality ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    public void Boss_VeryLowRoll_ReturnsCommon(int difficulty)
    {
        // roll < 0.03 → Common
        Assert.Equal(LootManager.Common, InvokeCore(7, difficulty, 0.01f));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    public void Boss_HighRoll_ReturnsEpicRange(int difficulty)
    {
        // roll >= epic threshold → Epic (8-10)
        int result = InvokeCore(7, difficulty, 0.9999f);
        Assert.InRange(result, 8, 10);
    }

    // ── Unknown monster quality → Common ─────────────────────────────────────

    [Theory]
    [InlineData(99)]
    [InlineData(-1)]
    public void UnknownMonsterQuality_AlwaysReturnsCommon(int quality)
    {
        Assert.Equal(LootManager.Common, InvokeCore(quality, 0, 0.5f));
    }

    // ── Unknown difficulty → Common (via default case) ───────────────────────

    [Fact]
    public void NormalMonster_UnknownDifficulty_ReturnsCommon()
    {
        Assert.Equal(LootManager.Common, InvokeCore(0, 99, 0.5f));
    }

    // ── GetDropRates ─────────────────────────────────────────────────────────

    [Fact]
    public void GetDropRates_NormalMonsterHighLevel_ReturnsSingleEntry()
    {
        var rates = LootManager.GetDropRates(0, 70);
        Assert.Single(rates);
        Assert.Equal(0.06f, rates[0]);
    }

    [Fact]
    public void GetDropRates_BossHighLevel_ReturnsMultipleEntries()
    {
        var rates = LootManager.GetDropRates(7, 70);
        Assert.True(rates.Count >= 3);
    }

    [Fact]
    public void GetDropRates_LowLevelChampion_AllOnesOrHalf()
    {
        var rates = LootManager.GetDropRates(1, 5); // level < 6
        Assert.All(rates, r => Assert.InRange(r, 0f, 1f));
    }

    // ── GetBloodShardsAmount ──────────────────────────────────────────────────

    [Fact]
    public void GetBloodShardsAmount_Difficulty0_ReturnsZero()
    {
        Assert.Equal(0, LootManager.GetBloodShardsAmount(0));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    public void GetBloodShardsAmount_T1T2_ReturnsOne(int difficulty)
    {
        Assert.Equal(1, LootManager.GetBloodShardsAmount(difficulty));
    }

    [Theory]
    [InlineData(6)]
    [InlineData(7)]
    public void GetBloodShardsAmount_T3T4_ReturnsTwo(int difficulty)
    {
        Assert.Equal(2, LootManager.GetBloodShardsAmount(difficulty));
    }

    [Theory]
    [InlineData(8)]
    [InlineData(9)]
    public void GetBloodShardsAmount_T5T6_ReturnsThree(int difficulty)
    {
        Assert.Equal(3, LootManager.GetBloodShardsAmount(difficulty));
    }

    // ── GetEssenceDropChance ──────────────────────────────────────────────────

    [Fact]
    public void GetEssenceDropChance_Increases_WithDifficulty()
    {
        float prev = LootManager.GetEssenceDropChance(0);
        for (int d = 1; d <= 9; d++)
        {
            float current = LootManager.GetEssenceDropChance(d);
            Assert.True(current > prev,
                $"Difficulty {d}: chance {current} should exceed previous {prev}");
            prev = current;
        }
    }

    [Fact]
    public void GetEssenceDropChance_UnknownDifficulty_ReturnsZero()
    {
        Assert.Equal(0f, LootManager.GetEssenceDropChance(99));
    }
}
