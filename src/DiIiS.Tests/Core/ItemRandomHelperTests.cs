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
    public void ReinitSeed_ResetsB_To666_MakingSequenceMatchFreshHelper()
    {
        // After one Next() call _b is updated from 666 to a new value.
        // ReinitSeed() resets _b back to 666 while leaving _a unchanged.
        // The subsequent sequence must therefore match a fresh helper
        // seeded with the post-advance _a value (same _a, same _b=666).
        var irh = new ItemRandomHelper(777);
        uint postAdvanceA = irh.Next(); // advances _a; also returns the new _a

        irh.ReinitSeed(); // resets _b → 666, _a stays as postAdvanceA

        // A fresh helper with seed postAdvanceA starts in the identical state:
        // _a = postAdvanceA, _b = 666
        var freshHelper = new ItemRandomHelper((int)postAdvanceA);

        for (int i = 0; i < 10; i++)
            Assert.Equal(freshHelper.Next(), irh.Next());
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
    public void NextUint_ProducesVariedValues()
    {
        // Verifies the LCG actually advances state and produces distinct values,
        // rather than a trivial / stuck sequence.
        var irh = new ItemRandomHelper(9999);
        var samples = new HashSet<uint>();
        for (int i = 0; i < 100; i++)
            samples.Add(irh.Next());

        Assert.True(samples.Count > 50,
            $"Expected many distinct values, but only got {samples.Count} unique values in 100 draws");
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
