using System.Text.Json;
using Cozmo.Robot;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M3-041 L30: the number formatter of the IMU logger. Every expected string is the output of the engine's shipped
/// libc++_shared.so vsnprintf (0x82528 -> printf core 0x813B0) run under re-analysis/tools/emu/emu_libcxx_printf.py;
/// regenerate with <c>python -I re-analysis/tools/emu/emu_libcxx_printf.py --write
/// cozmo-stack/tests/Cozmo.Protocol.Tests/Fixtures/m3_041_libcxx_printf_oracle.json</c>. No expectation here comes from
/// the C#.
/// </summary>
public class M3ImuLogFormatterTests
{
    private static JsonDocument Load() => JsonDocument.Parse(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "m3_041_libcxx_printf_oracle.json")));

    // fidelity: M3-041
    [Fact]
    public void L30_FormatterMatchesShippedLibcxxPrintfOnTheWholeOracleCorpus()
    {
        using var doc = Load();
        Assert.Equal("8ac5090bbd0be7401af5fff6044ded6f1e6ced692fcd3581fa3cb937b519a16a",
            doc.RootElement.GetProperty("libcxx_sha256").GetString());
        int g = 0, ld = 0, u = 0;
        var mismatches = new List<string>();
        foreach (var row in doc.RootElement.GetProperty("rows").EnumerateArray())
        {
            string expected = row.GetProperty("expected").GetString()!;
            string actual;
            switch (row.GetProperty("fmt").GetString())
            {
                case "%.*g":
                    double v = BitConverter.Int64BitsToDouble(Convert.ToInt64(row.GetProperty("double_bits").GetString(), 16));
                    actual = LibcxxPrintf.FormatG(v, row.GetProperty("precision").GetInt32());
                    g++;
                    break;
                case "%ld":
                    actual = LibcxxPrintf.FormatLong(row.GetProperty("value").GetInt32());
                    ld++;
                    break;
                case "%u":
                    actual = LibcxxPrintf.FormatUnsigned(row.GetProperty("value").GetUInt32());
                    u++;
                    break;
                default: throw new InvalidOperationException("unknown format row");
            }
            if (actual != expected) mismatches.Add($"{row}: got '{actual}'");
        }
        Assert.True(g > 3000 && ld > 300 && u > 100, $"corpus too small: g={g} ld={ld} u={u}");
        Assert.True(mismatches.Count == 0, $"{mismatches.Count} mismatches, first: {string.Join(" | ", mismatches.Take(5))}");
    }

    // fidelity: M3-041
    [Fact]
    public void L30_FloatInsertionWidensF32AndUsesPrecisionSix()
    {
        using var doc = Load();
        int checkedRows = 0;
        foreach (var row in doc.RootElement.GetProperty("rows").EnumerateArray())
        {
            if (row.GetProperty("fmt").GetString() != "%.*g" || row.GetProperty("precision").GetInt32() != 6) continue;
            var bits = row.GetProperty("f32_bits");
            if (bits.ValueKind != JsonValueKind.String) continue;
            float f = BitConverter.Int32BitsToSingle(unchecked((int)Convert.ToUInt32(bits.GetString(), 16)));
            Assert.Equal(row.GetProperty("expected").GetString(), LibcxxPrintf.FormatFloat(f));
            checkedRows++;
        }
        Assert.True(checkedRows > 2500, $"only {checkedRows} f32 rows");
    }

    // L30: where the shipped core is not an exact conversion. The strings are the oracle's output for the f32 values
    // 999999.5, 100001.5, 123457.5 (fixture labels '999999.5', '100001.5', '123457.5'); an exact half-even conversion
    // would give 1e+06, 100002 and 123458.
    // fidelity: M3-041
    [Fact]
    public void L30_ShippedLimbBoundaryRoundingIsKept()
    {
        Assert.Equal("999999", LibcxxPrintf.FormatFloat(999999.5f));
        Assert.Equal("100001", LibcxxPrintf.FormatFloat(100001.5f));
        Assert.Equal("123457", LibcxxPrintf.FormatFloat(123457.5f));
        using var doc = Load();
        foreach (var label in new[] { "999999.5", "100001.5", "123457.5" })
            Assert.Contains(doc.RootElement.GetProperty("rows").EnumerateArray(),
                r => r.TryGetProperty("label", out var l) && l.GetString() == label);
    }
}
