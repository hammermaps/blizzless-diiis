using DiIiS_NA.Core.Helpers.Math;
using Xunit;

namespace DiIiS.Tests.Core;

public class FastRandomTests
{
    // ── Next(lowerBound, upperBound) ──────────────────────────────────────────

    [Fact]
    public void Next_Range_AlwaysWithinBounds()
    {
        var rng = new FastRandom(42);
        for (int i = 0; i < 10_000; i++)
        {
            int value = rng.Next(10, 20);
            Assert.InRange(value, 10, 19);
        }
    }

    [Fact]
    public void Next_SameBounds_AlwaysReturnsSameValue()
    {
        var rng = new FastRandom(1);
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(5, rng.Next(5, 5));
        }
    }

    [Fact]
    public void Next_UpperBound_ThrowsWhenLowerGreaterThanUpper()
    {
        var rng = new FastRandom(1);
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.Next(10, 5));
    }

    [Fact]
    public void Next_UpperBoundOnly_ThrowsWhenNegative()
    {
        var rng = new FastRandom(1);
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.Next(-1));
    }

    [Fact]
    public void Next_UpperBoundZero_ReturnsZero()
    {
        var rng = new FastRandom(1);
        Assert.Equal(0, rng.Next(0));
    }

    // ── NextDouble ────────────────────────────────────────────────────────────

    [Fact]
    public void NextDouble_AlwaysInUnitInterval()
    {
        var rng = new FastRandom(99);
        for (int i = 0; i < 10_000; i++)
        {
            double value = rng.NextDouble();
            Assert.True(value >= 0.0 && value < 1.0,
                $"Expected value in [0.0, 1.0) but got {value}");
        }
    }

    [Fact]
    public void NextDouble_Range_AlwaysWithinBounds()
    {
        var rng = new FastRandom(7);
        for (int i = 0; i < 10_000; i++)
        {
            double value = rng.NextDouble(5.0, 10.0);
            Assert.InRange(value, 5.0, 10.0);
        }
    }

    // ── Determinism ───────────────────────────────────────────────────────────

    [Fact]
    public void SameSeed_ProducesIdenticalSequence()
    {
        var rng1 = new FastRandom(12345);
        var rng2 = new FastRandom(12345);

        for (int i = 0; i < 50; i++)
        {
            Assert.Equal(rng1.NextDouble(), rng2.NextDouble());
        }
    }

    [Fact]
    public void Reinitialise_RestoresSequence()
    {
        const int seed = 999;
        var rng = new FastRandom(seed);

        double first = rng.NextDouble();
        double second = rng.NextDouble();

        rng.Reinitialise(seed);

        Assert.Equal(first, rng.NextDouble());
        Assert.Equal(second, rng.NextDouble());
    }

    // ── Statistical distribution ──────────────────────────────────────────────

    [Fact]
    public void Next_Range_MeanApproximatesMidpoint()
    {
        const int lower = 0;
        const int upper = 100;
        const int sampleCount = 100_000;
        var rng = new FastRandom(321);

        long sum = 0;
        for (int i = 0; i < sampleCount; i++)
            sum += rng.Next(lower, upper);

        double mean = (double)sum / sampleCount;
        // mean should be close to 50 (midpoint of [0,100))
        Assert.InRange(mean, 47.0, 53.0);
    }

    // ── NextBool ─────────────────────────────────────────────────────────────

    [Fact]
    public void NextBool_ProducesBothTrueAndFalse()
    {
        var rng = new FastRandom(1337);
        bool seenTrue = false;
        bool seenFalse = false;

        for (int i = 0; i < 1000; i++)
        {
            bool v = rng.NextBool();
            seenTrue |= v;
            seenFalse |= !v;
            if (seenTrue && seenFalse) break;
        }

        Assert.True(seenTrue);
        Assert.True(seenFalse);
    }

    // ── Chance ────────────────────────────────────────────────────────────────

    [Fact]
    public void Chance_Zero_AlwaysReturnsFalse()
    {
        var rng = new FastRandom(1);
        for (int i = 0; i < 1000; i++)
            Assert.False(rng.Chance(0f));
    }

    [Fact]
    public void Chance_HundredPercent_AlwaysReturnsTrue()
    {
        var rng = new FastRandom(1);
        for (int i = 0; i < 1000; i++)
            Assert.True(rng.Chance(100f));
    }

    [Fact]
    public void Chance_FiftyPercent_ReturnsApproximatelyHalfTrue()
    {
        // Verifies the comparison operator is correct at intermediate values:
        // a broken >= vs > at boundary 50 would shift the distribution noticeably.
        var rng = new FastRandom(42);
        int trueCount = 0;
        const int samples = 10_000;
        for (int i = 0; i < samples; i++)
            if (rng.Chance(50f)) trueCount++;

        double ratio = (double)trueCount / samples;
        Assert.InRange(ratio, 0.47, 0.53);
    }

    // ── NextBytes ─────────────────────────────────────────────────────────────

    [Fact]
    public void NextBytes_FillsBufferWithNonZeroBytes()
    {
        var rng = new FastRandom(77);
        byte[] buffer = new byte[64];
        rng.NextBytes(buffer);

        // Statistical check: with 64 bytes it would be astronomically unlikely
        // that every byte is zero
        Assert.Contains(buffer, b => b != 0);
    }

    [Fact]
    public void NextBytes_SameSeed_ProducesSameBytes()
    {
        byte[] buf1 = new byte[32];
        byte[] buf2 = new byte[32];

        new FastRandom(555).NextBytes(buf1);
        new FastRandom(555).NextBytes(buf2);

        Assert.Equal(buf1, buf2);
    }
}
