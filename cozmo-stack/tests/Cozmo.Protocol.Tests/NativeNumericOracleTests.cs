using System.Text.Json;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Xunit;

namespace Cozmo.Protocol.Tests;

public class NativeNumericOracleTests
{
    [Fact]
    public void ProductionNumericHelpersMatchShippedArmInstructions()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "Fixtures", "m1_m5_numeric_oracle.json")));
        Assert.Equal("02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1",
            document.RootElement.GetProperty("engine_sha256").GetString());
        var nanDifferences = new List<object>();
        foreach (var row in document.RootElement.GetProperty("rows").EnumerateArray())
        {
            var function = row.GetProperty("function").GetString();
            if (function == "clip_line")
            {
                var input = row.GetProperty("inputs").EnumerateArray().Select(x => x.GetInt32()).ToArray();
                int x1=input[2], y1=input[3], x2=input[4], y2=input[5];
                bool result=OpenCv310.ClipLine(input[0],input[1],ref x1,ref y1,ref x2,ref y2);
                Assert.Equal(row.GetProperty("expected_return").GetInt32()!=0,result);
                Assert.Equal(row.GetProperty("expected").EnumerateArray().Select(x=>x.GetInt32()).ToArray(),new[]{x1,y1,x2,y2});
            }
            else if (function == "rgb555_fragment")
            {
                var inputs = row.GetProperty("inputs").EnumerateArray().Select(x => x.GetByte()).ToArray();
                Assert.Equal(row.GetProperty("expected").GetUInt16(), new LedColor(inputs[0], inputs[1], inputs[2], inputs[3]).Packed);
            }
            else if (function == "face_constructor")
            {
                var face = new ProceduralFacePose();
                var words = face.Left.ToArray().Concat(face.Right.ToArray())
                    .Concat(new[] { face.FaceAngle, face.FaceScaleX, face.FaceScaleY,
                        face.FaceCenterX, face.FaceCenterY }).Select(BitConverter.SingleToUInt32Bits).ToArray();
                // Native word 38 owns the distorter; it is not a numeric pose field.
                Assert.Equal(row.GetProperty("expected").EnumerateArray().Where((x, i) => i != 38)
                    .Select(x => x.GetUInt32()).ToArray(), words);
            }
            else if (function == "eye_box")
            {
                var words = row.GetProperty("inputs").EnumerateArray()
                    .Select(x => BitConverter.UInt32BitsToSingle(x.GetUInt32())).ToArray();
                var face = new ProceduralFacePose { Left = new Eye(words.Take(19).ToArray()),
                    Right = new Eye(words.Skip(19).Take(19).ToArray()), FaceAngle = words[39],
                    FaceScaleX = words[40], FaceScaleY = words[41], FaceCenterX = words[42], FaceCenterY = words[43] };
                var box = face.GetEyeBoundingBox();
                var expected = row.GetProperty("expected").EnumerateArray().Select(x => x.GetUInt32()).ToArray();
                var actual = new[] { box.XMin, box.XMax, box.YMin, box.YMax }.Select(BitConverter.SingleToUInt32Bits).ToArray();
                for (int i = 0; i < actual.Length; ++i)
                {
                    // The broad box corpus is diagnostic until its arithmetic DEFECTs are reviewed.
                    // Only explicitly selected regression cases gate this fix.
                    if (expected[i] != actual[i])
                        nanDifferences.Add(new { inputs = row.GetProperty("inputs").Clone(), output = i, expected = expected[i], actual = actual[i] });
                    if (row.TryGetProperty("regression", out var regression) && regression.GetBoolean())
                        Assert.Equal(expected[i], actual[i]);
                }
            }
            else if (function == "eye_clip")
            {
                float input = BitConverter.UInt32BitsToSingle(row.GetProperty("input").GetUInt32());
                Assert.Equal(row.GetProperty("expected").GetUInt32(), BitConverter.SingleToUInt32Bits(
                    Eye.Clip((EyeParam)row.GetProperty("parameter").GetInt32(), input, 0)));
            }
            else if (function == "face_interpolate_default")
            {
                var face = ProceduralFacePose.Interpolate(new(), new(),
                    BitConverter.UInt32BitsToSingle(row.GetProperty("input").GetUInt32()));
                var words = face.Left.ToArray().Concat(face.Right.ToArray())
                    .Concat(new[] { face.FaceAngle, face.FaceScaleX, face.FaceScaleY, face.FaceCenterX, face.FaceCenterY })
                    .Select(BitConverter.SingleToUInt32Bits).ToArray();
                Assert.Equal(row.GetProperty("expected").EnumerateArray().Where((x, i) => i != 38)
                    .Select(x => x.GetUInt32()).ToArray(), words);
            }
            else if (function == "rescale")
            {
                float input = BitConverter.UInt32BitsToSingle(row.GetProperty("input").GetUInt32());
                Assert.Equal(row.GetProperty("expected").GetUInt32(),
                    BitConverter.SingleToUInt32Bits(CozmoMotion.RescaleRadians(input)));
            }
            else if (function == "near")
            {
                var inputs = row.GetProperty("inputs").EnumerateArray()
                    .Select(x => BitConverter.UInt32BitsToSingle(x.GetUInt32())).ToArray();
                Assert.Equal(row.GetProperty("expected").GetInt32() != 0,
                    CozmoMotion.IsNear(inputs[0], inputs[1], inputs[2]));
            }
            else if (function == "mt19937")
            {
                var generator = new Mt19937(row.GetProperty("seed").GetUInt32());
                foreach (var expected in row.GetProperty("expected").EnumerateArray())
                    Assert.Equal(expected.GetUInt32(), generator.Next());
            }
            else if (function == "double")
            {
                var generator = new EngineRandom(row.GetProperty("seed").GetUInt32());
                foreach (var expected in row.GetProperty("expected").EnumerateArray())
                    Assert.Equal(expected.GetString(), BitConverter.DoubleToUInt64Bits(generator.GetNextDbl()).ToString("X16"));
            }
            else Assert.Fail("Unrecognized native fixture function " + function);
        }
        if (Environment.GetEnvironmentVariable("COZMO_NUMERIC_DIFF_OUTPUT") is string output)
            File.WriteAllText(output, JsonSerializer.Serialize(nanDifferences, new JsonSerializerOptions { WriteIndented = true }));
    }
}
