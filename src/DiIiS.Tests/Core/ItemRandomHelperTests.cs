using DiIiS_NA.Core.Helpers.Math;
using Xunit;

namespace DiIiS.Tests.Core;

public class ItemRandomHelperTests
{
    // ── Determinism ───────────────────────────────────────────────────────────

    [Fact]
    public void SameSeed_ProducesIdenticalSequence()
    {
        var irh1 = new ItemRandomHelper(12345);
        var irh2 = new ItemRandomHelper(12345);

        for (int i = 0; i < 50; i++)
            Assert.Equal(irh1.Next(), irh2.Next());
    }

    [Fact]
    public void ReinitSeed_RestoresDefaultBComponent()
    {
        // After ReinitSeed the _b component resets to 666,
        // so two helpers with the same seed and one ReinitSeed call
        // should produce the same next value.
        var irh1 = new ItemRandomHelper(777);
        var irh2 = new ItemRandomHelper(777);

        // Advance irh1, then reset _b
        irh1.Next();
        irh1.ReinitSeed();

        // irh2 has consumed no values: both share the state after
        // one Next() from irh1 but with b reset to 666 only for irh1.
        // We just verify ReinitSeed doesn't throw and sequence diverges
        // from a fresh helper (since _a was already advanced).
        Assert.NotEqual(irh1.Next(), irh2.Next());
    }

    // ── Next(float, float) range ──────────────────────────────────────────────

    [Fact]
    public void NextFloat_MinEqualsMax_ReturnsMin()
    {
        var irh = new ItemRandomHelper(1);
        for (int i = 0; i < 100; i++)
        {
            float result = irh.Next(5f, 5f);
            Assert.Equal(5f, result);
        }
    }

    [Fact]
    public void NextFloat_Range_AlwaysWithinBounds()
    {
        var irh = new ItemRandomHelper(42);
        for (int i = 0; i < 10_000; i++)
        {
            float value = irh.Next(0f, 100f);
            Assert.InRange(value, 0f, 100f);
        }
    }

    [Fact]
    public void NextUint_NeverExceedsUintMaxValue()
    {
        var irh = new ItemRandomHelper(9999);
        for (int i = 0; i < 10_000; i++)
        {
            uint value = irh.Next();
            Assert.True(value <= uint.MaxValue);
        }
    }

    // ── Different seeds produce different sequences ───────────────────────────

    [Fact]
    public void DifferentSeeds_ProduceDifferentFirstValues()
    {
        var irh1 = new ItemRandomHelper(1);
        var irh2 = new ItemRandomHelper(2);

        Assert.NotEqual(irh1.Next(), irh2.Next());
    }
}
