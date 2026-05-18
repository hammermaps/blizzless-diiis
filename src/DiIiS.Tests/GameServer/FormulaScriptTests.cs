using DiIiS_NA.Core.Helpers.Math;
using DiIiS_NA.GameServer.GSSystem.ItemsSystem;
using Xunit;

namespace DiIiS.Tests.GameServer;

/// <summary>
/// Tests for <see cref="FormulaScript.Evaluate"/>.
///
/// Bytecode opcode reference (from FormulaScript.cs):
///   0  – Return: pop top-of-stack → result, return true
///   1  – Function call; next byte is funcId:
///          3 = RandomIntMinRange(base, range): needs 2 stack values
///          4 = RandomIntMinMax(min, max):      needs 2 stack values
///   5  – Push float (next int reinterpreted as IEEE-754 single)
///   6  – Push float (next int reinterpreted as IEEE-754 single)
///  11  – Add:  pop numb2, pop numb1, push (numb1 + numb2)
///  12  – Sub:  pop numb2, pop numb1, push (numb1 - numb2)
///  13  – Mul:  pop numb2, pop numb1, push (numb1 * numb2)
///  14  – Div:  pop numb2, pop numb1, push (numb1 / numb2); error if numb1==0
/// </summary>
public class FormulaScriptTests
{
    // Pre-computed IEEE-754 single bit patterns used in scripts below.
    // BitConverter.SingleToInt32Bits(x)
    private const int Bits10f  = 1092616192;   // 10.0f
    private const int Bits5f   = 1084227584;   // 5.0f
    private const int Bits42f  = 1109917696;   // 42.0f
    private const int Bits3f   = 1077936128;   // 3.0f
    private const int Bits2f   = 1073741824;   // 2.0f

    private static readonly ItemRandomHelper Irh = new(0);

    // ── Return opcode (0) with values on stack ────────────────────────────────

    [Fact]
    public void Evaluate_PushConstantAndReturn_ReturnsTrueWithCorrectResult()
    {
        // Script: push 42.0f  → return
        int[] script = new int[] { 6, Bits42f, 0 };

        bool ok = FormulaScript.Evaluate(script, Irh, out float result);

        Assert.True(ok);
        Assert.Equal(42f, result);
    }

    // ── Stack underflow on Return opcode ──────────────────────────────────────

    [Fact]
    public void Evaluate_EmptyStackOnReturn_ReturnsFalse()
    {
        // Script: [0] – Return opcode with nothing on stack → underflow
        int[] script = new int[] { 0 };

        bool ok = FormulaScript.Evaluate(script, Irh, out float result);

        Assert.False(ok);
        Assert.Equal(0f, result);
    }

    // ── End of script without Return ─────────────────────────────────────────

    [Fact]
    public void Evaluate_ScriptWithNoReturnOpcode_ReturnsFalse()
    {
        // Script: push 10.0f  (no return opcode) → should return false
        int[] script = new int[] { 6, Bits10f };

        bool ok = FormulaScript.Evaluate(script, Irh, out float result);

        Assert.False(ok);
    }

    // ── Addition ─────────────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_Addition_ReturnsSum()
    {
        // Script: push 10f, push 5f, add, return  →  result = 15f
        int[] script = new int[] { 6, Bits10f, 6, Bits5f, 11, 0 };

        bool ok = FormulaScript.Evaluate(script, Irh, out float result);

        Assert.True(ok);
        Assert.Equal(15f, result);
    }

    [Fact]
    public void Evaluate_Addition_StackUnderflow_ReturnsFalse()
    {
        // Script: push 5f, add (needs 2 values but only 1 on stack)
        int[] script = new int[] { 6, Bits5f, 11 };

        bool ok = FormulaScript.Evaluate(script, Irh, out float result);

        Assert.False(ok);
    }

    // ── Subtraction ──────────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_Subtraction_ReturnsDifference()
    {
        // Script: push 10f, push 3f, sub, return  →  result = 7f
        int[] script = new int[] { 6, Bits10f, 6, Bits3f, 12, 0 };

        bool ok = FormulaScript.Evaluate(script, Irh, out float result);

        Assert.True(ok);
        Assert.Equal(7f, result);
    }

    // ── Multiplication ────────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_Multiplication_ReturnsProduct()
    {
        // Script: push 3f, push 2f, mul, return  →  result = 6f
        int[] script = new int[] { 6, Bits3f, 6, Bits2f, 13, 0 };

        bool ok = FormulaScript.Evaluate(script, Irh, out float result);

        Assert.True(ok);
        Assert.Equal(6f, result);
    }

    [Fact]
    public void Evaluate_Multiplication_StackUnderflow_ReturnsFalse()
    {
        // Script: push 5f, mul (needs 2 values)
        int[] script = new int[] { 6, Bits5f, 13 };

        bool ok = FormulaScript.Evaluate(script, Irh, out float result);

        Assert.False(ok);
    }

    // ── Division ─────────────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_Division_ReturnsQuotient()
    {
        // Script: push 10f, push 2f, div, return  →  result = 5f
        int[] script = new int[] { 6, Bits10f, 6, Bits2f, 14, 0 };

        bool ok = FormulaScript.Evaluate(script, Irh, out float result);

        Assert.True(ok);
        Assert.Equal(5f, result);
    }

    [Fact]
    public void Evaluate_Division_StackUnderflow_ReturnsFalse()
    {
        int[] script = new int[] { 6, Bits5f, 14 };

        bool ok = FormulaScript.Evaluate(script, Irh, out float result);

        Assert.False(ok);
    }

    // ── Unknown opcode ────────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_UnknownOpcode_ReturnsFalse()
    {
        // Opcode 99 is not implemented
        int[] script = new int[] { 99 };

        bool ok = FormulaScript.Evaluate(script, Irh, out float result);

        Assert.False(ok);
    }

    // ── Function call opcode (1) – unimplemented funcId ──────────────────────

    [Fact]
    public void Evaluate_UnknownFunctionId_ReturnsFalse()
    {
        // Opcode 1, funcId 99 → "Unimplemented function"
        int[] script = new int[] { 1, 99 };

        bool ok = FormulaScript.Evaluate(script, Irh, out float result);

        Assert.False(ok);
    }

    // ── Min/Max output ────────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_PushConstant_MinAndMaxDefaultToZero()
    {
        // For a plain push-and-return script the min/max outputs stay 0
        int[] script = new int[] { 6, Bits10f, 0 };

        FormulaScript.Evaluate(script, Irh, out _, out float minValue, out float maxValue);

        Assert.Equal(0f, minValue);
        Assert.Equal(0f, maxValue);
    }

    [Fact]
    public void Evaluate_RandomMinRange_SetsMinAndMaxValues()
    {
        // Script: push base=3f, push range=2f, func RandomIntMinRange(3), return
        // funcId 3: numb1 = minValue = base(3f), maxValue = base + range = 5f
        int[] script = new int[] { 6, Bits3f, 6, Bits2f, 1, 3, 0 };

        bool ok = FormulaScript.Evaluate(script, Irh, out float result, out float minValue, out float maxValue);

        Assert.True(ok);
        Assert.Equal(3f, minValue);
        Assert.Equal(5f, maxValue);   // base + range
        Assert.InRange(result, 3f, 5f);
    }

    [Fact]
    public void Evaluate_RandomMinMax_SetsMinAndMaxValues()
    {
        // Script: push min=2f, push max=10f, func RandomIntMinMax(4), return
        // funcId 4: numb1 = minValue = min(2f), numb2 = maxValue = max(10f)
        int[] script = new int[] { 6, Bits2f, 6, Bits10f, 1, 4, 0 };

        bool ok = FormulaScript.Evaluate(script, Irh, out float result, out float minValue, out float maxValue);

        Assert.True(ok);
        Assert.Equal(2f, minValue);
        Assert.Equal(10f, maxValue);
        Assert.InRange(result, 2f, 10f);
    }
}
