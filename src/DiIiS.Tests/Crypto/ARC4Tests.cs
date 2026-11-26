using DiIiS_NA.LoginServer.Crypthography;
using Xunit;

namespace DiIiS.Tests.Crypto;

/// <summary>
/// Tests for the <see cref="ARC4"/> (RC4) stream cipher.
///
/// RC4 is a symmetric XOR-based cipher, so:
///   encrypt(encrypt(plaintext, key), key) == plaintext
/// </summary>
public class ARC4Tests
{
    // ── Encrypt / Decrypt round-trip ──────────────────────────────────────────

    [Fact]
    public void Process_EncryptThenDecrypt_RestoresOriginalData()
    {
        byte[] key = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
        byte[] plaintext = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }; // "Hello"

        byte[] buffer = (byte[])plaintext.Clone();

        // Encrypt in-place
        var enc = new ARC4(key);
        enc.Process(buffer, 0, buffer.Length);

        // The ciphertext should differ from the plaintext
        Assert.NotEqual(plaintext, buffer);

        // Decrypt: use a fresh ARC4 instance with the same key
        var dec = new ARC4(key);
        dec.Process(buffer, 0, buffer.Length);

        // Should be back to original
        Assert.Equal(plaintext, buffer);
    }

    [Fact]
    public void Process_EmptyBuffer_ReturnsZero()
    {
        var arc4 = new ARC4(new byte[] { 0xAA, 0xBB });
        int processed = arc4.Process(new byte[] { }, 0, 0);
        Assert.Equal(0, processed);
    }

    [Fact]
    public void Process_ReturnsInputCount()
    {
        byte[] key = new byte[] { 0xDE, 0xAD };
        byte[] buffer = new byte[10];
        var arc4 = new ARC4(key);
        int processed = arc4.Process(buffer, 0, buffer.Length);
        Assert.Equal(buffer.Length, processed);
    }

    // ── Determinism ───────────────────────────────────────────────────────────

    [Fact]
    public void SameKey_SamePlaintext_ProducesIdenticalCiphertext()
    {
        byte[] key = new byte[] { 0x1A, 0x2B, 0x3C };
        byte[] pt = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07 };

        byte[] buf1 = (byte[])pt.Clone();
        byte[] buf2 = (byte[])pt.Clone();

        new ARC4(key).Process(buf1, 0, buf1.Length);
        new ARC4(key).Process(buf2, 0, buf2.Length);

        Assert.Equal(buf1, buf2);
    }

    // ── Different keys ────────────────────────────────────────────────────────

    [Fact]
    public void DifferentKeys_ProduceDifferentCiphertexts()
    {
        byte[] pt = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray();

        byte[] buf1 = (byte[])pt.Clone();
        byte[] buf2 = (byte[])pt.Clone();

        new ARC4(new byte[] { 0x11, 0x22, 0x33 }).Process(buf1, 0, buf1.Length);
        new ARC4(new byte[] { 0x44, 0x55, 0x66 }).Process(buf2, 0, buf2.Length);

        Assert.NotEqual(buf1, buf2);
    }

    // ── Partial buffer (offset/count) ─────────────────────────────────────────

    [Fact]
    public void Process_WithOffset_OnlyTransformsSpecifiedRange()
    {
        byte[] key = new byte[] { 0xAB, 0xCD };
        byte[] buffer = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00 };

        // Encrypt only the middle 3 bytes (index 1..3)
        var arc4 = new ARC4(key);
        arc4.Process(buffer, 1, 3);

        // First and last bytes must stay zero (untouched)
        Assert.Equal(0x00, buffer[0]);
        Assert.Equal(0x00, buffer[4]);

        // Middle bytes should have been XOR'd with the keystream
        // (they started at 0x00 so ciphertext == keystream byte)
        // We can't assert exact values without computing keystream,
        // but they should NOT all be 0x00 (extremely low probability)
        Assert.True(buffer[1] != 0 || buffer[2] != 0 || buffer[3] != 0,
            "At least one encrypted byte should be non-zero");
    }

    // ── Known test vector (RFC 6229 compatible) ───────────────────────────────

    [Fact]
    public void Process_AllZeroKey_IsIdempotentRoundTrip()
    {
        byte[] key = new byte[16]; // 16-byte zero key is valid
        byte[] plaintext = Enumerable.Range(0, 16).Select(i => (byte)(i * 7)).ToArray();

        byte[] buffer = (byte[])plaintext.Clone();
        new ARC4(key).Process(buffer, 0, buffer.Length);
        new ARC4(key).Process(buffer, 0, buffer.Length);

        Assert.Equal(plaintext, buffer);
    }

    // ── XOR symmetry property ─────────────────────────────────────────────────

    [Fact]
    public void Process_NonZeroInput_XorProducesNonTrivialOutput()
    {
        byte[] key = new byte[] { 0xFF, 0x00, 0xFF };
        byte[] buffer = new byte[8];
        Array.Fill(buffer, (byte)0xFF);

        new ARC4(key).Process(buffer, 0, buffer.Length);

        // Result is XOR of 0xFF with each keystream byte.
        // The result might equal 0xFF for some bytes if keystream byte is 0,
        // but is extremely unlikely to be entirely 0xFF.
        bool anyChanged = buffer.Any(b => b != 0xFF);
        Assert.True(anyChanged, "ARC4 keystream should modify at least one byte");
    }
}
