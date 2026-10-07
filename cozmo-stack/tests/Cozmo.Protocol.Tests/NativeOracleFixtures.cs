using System.Text.Json;
using Cozmo.Robot;

namespace Cozmo.Protocol.Tests;

/// <summary>Expected data read from shipped-code fixtures; no production encoder computes it.</summary>
internal static class NativeOracleFixtures
{
    private static readonly JsonDocument Numeric = Load("m1_m5_numeric_oracle.json");
    private static readonly JsonDocument Transforms = Load("q11_transforms_oracle.json");
    private static JsonDocument Load(string file) => JsonDocument.Parse(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", file)));

    internal static ushort Packed(uint rgba)
    {
        byte[] input = { (byte)(rgba >> 24), (byte)(rgba >> 16), (byte)(rgba >> 8), (byte)rgba };
        return Numeric.RootElement.GetProperty("rows").EnumerateArray().First(row =>
            row.GetProperty("function").GetString() == "rgb555_fragment" &&
            row.GetProperty("inputs").EnumerateArray().Select(x => x.GetByte()).SequenceEqual(input))
            .GetProperty("expected").GetUInt16();
    }

    // Native object bytes 0x00..0x4B are the first eye; 0x4C..0x97 the second.
    internal static float[] DefaultEye => Numeric.RootElement.GetProperty("rows").EnumerateArray()
        .Single(row => row.GetProperty("function").GetString() == "face_constructor")
        .GetProperty("expected").EnumerateArray().Take(19)
        .Select(x => BitConverter.UInt32BitsToSingle(x.GetUInt32())).ToArray();

    internal static JsonElement TransformRows => Transforms.RootElement.GetProperty("rows");
    internal static uint NamedColor(string name) => Convert.ToUInt32(TransformRows.EnumerateArray().Single(row =>
        row.GetProperty("function").GetString() == "named_color" && row.GetProperty("input").GetString() == name)
        .GetProperty("expected_bytes").GetString(),16);
    internal static JsonElement RleCase(string name) => TransformRows.EnumerateArray().Single(row =>
        row.GetProperty("function").GetString() == "compress_rle" && row.GetProperty("case").GetString() == name);
    internal static byte[] Rle(string name) => Convert.FromHexString(RleCase(name).GetProperty("expected").GetString()!);
    internal static FaceBitmap Bitmap(string name)
    {
        var input = Convert.FromHexString(RleCase(name).GetProperty("bitmap").GetString()!);
        var image = new FaceBitmap();
        for (int i=0;i<input.Length;++i) image[i%128,i/128]=input[i];
        return image;
    }
    internal static int FrameBudget(int played) => TransformRows.EnumerateArray().Single(row =>
        row.GetProperty("function").GetString() == "budget" && row.GetProperty("played_bytes").GetUInt32() == 0 &&
        row.GetProperty("played_frames").GetUInt32() == unchecked((uint)played))
        .GetProperty("expected_frames").GetInt32();
}
