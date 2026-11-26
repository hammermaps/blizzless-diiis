using DiIiS_NA.Core.Helpers.Hash;
using Xunit;

namespace DiIiS.Tests.Core;

public class StringHashHelperTests
{
    // ── HashItemName ──────────────────────────────────────────────────────────

    [Fact]
    public void HashItemName_EmptyString_ReturnsZero()
    {
        Assert.Equal(0, StringHashHelper.HashItemName(""));
    }

    [Fact]
    public void HashItemName_IsCaseInsensitive()
    {
        // HashItemName lowercases the input before hashing
        Assert.Equal(
            StringHashHelper.HashItemName("Unique_Chest_Set_06_x1"),
            StringHashHelper.HashItemName("unique_chest_set_06_x1"));
    }

    [Fact]
    public void HashItemName_IsCaseInsensitive_AllUppercase()
    {
        Assert.Equal(
            StringHashHelper.HashItemName("ITEM"),
            StringHashHelper.HashItemName("item"));
    }

    [Fact]
    public void HashItemName_IsDeterministic()
    {
        const string name = "AngelicCrucible";
        Assert.Equal(
            StringHashHelper.HashItemName(name),
            StringHashHelper.HashItemName(name));
    }

    [Fact]
    public void HashItemName_DifferentInputs_ProduceDifferentHashes()
    {
        Assert.NotEqual(
            StringHashHelper.HashItemName("SwordA"),
            StringHashHelper.HashItemName("SwordB"));
    }

    [Fact]
    public void HashItemName_SingleCharacter_KnownValue()
    {
        // hash = (0 << 5) + 0 + (int)'a' = 97
        Assert.Equal(97, StringHashHelper.HashItemName("a"));
    }

    [Fact]
    public void HashItemName_TwoCharacters_KnownValue()
    {
        // After 'a': hash = 97
        // After 'b': hash = (97 << 5) + 97 + 98 = 3104 + 97 + 98 = 3299
        Assert.Equal(3299, StringHashHelper.HashItemName("ab"));
    }

    // ── HashNormal ────────────────────────────────────────────────────────────

    [Fact]
    public void HashNormal_EmptyString_ReturnsZero()
    {
        Assert.Equal(0, StringHashHelper.HashNormal(""));
    }

    [Fact]
    public void HashNormal_IsCaseSensitive()
    {
        // HashNormal does NOT lowercase – uppercase 'A' (65) ≠ lowercase 'a' (97)
        Assert.NotEqual(
            StringHashHelper.HashNormal("Hello"),
            StringHashHelper.HashNormal("hello"));
    }

    [Fact]
    public void HashNormal_IsDeterministic()
    {
        const string s = "SomeName";
        Assert.Equal(StringHashHelper.HashNormal(s), StringHashHelper.HashNormal(s));
    }

    [Fact]
    public void HashNormal_SingleLowerChar_KnownValue()
    {
        // Same algorithm as HashItemName but without lowercasing
        // 'a' = 97  →  (0 << 5) + 0 + 97 = 97
        Assert.Equal(97, StringHashHelper.HashNormal("a"));
    }

    [Fact]
    public void HashNormal_SingleUpperChar_KnownValue()
    {
        // 'A' = 65  →  (0 << 5) + 0 + 65 = 65
        Assert.Equal(65, StringHashHelper.HashNormal("A"));
    }

    // ── HashIdentity ─────────────────────────────────────────────────────────

    [Fact]
    public void HashIdentity_EmptyString_ReturnsFnvOffsetBasis()
    {
        // FNV-1a: empty input returns the offset basis 0x811C9DC5
        Assert.Equal(0x811C9DC5u, StringHashHelper.HashIdentity(""));
    }

    [Fact]
    public void HashIdentity_IsDeterministic()
    {
        const string s = "testaccount@";
        Assert.Equal(StringHashHelper.HashIdentity(s), StringHashHelper.HashIdentity(s));
    }

    [Fact]
    public void HashIdentity_DifferentInputs_ProduceDifferentHashes()
    {
        Assert.NotEqual(
            StringHashHelper.HashIdentity("user1@"),
            StringHashHelper.HashIdentity("user2@"));
    }
}
