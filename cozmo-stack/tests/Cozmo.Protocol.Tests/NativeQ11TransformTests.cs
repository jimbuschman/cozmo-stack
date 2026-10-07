using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Xunit;

namespace Cozmo.Protocol.Tests;

public class NativeQ11TransformTests
{
    [Fact]
    public void BudgetAndCompressionMatchCompleteShippedFunctions()
    {
        foreach (var row in NativeOracleFixtures.TransformRows.EnumerateArray())
        {
            if (row.GetProperty("function").GetString()=="budget")
            {
                var buffer=new StreamSendBuffer();
                buffer.UpdateAmountToSend(unchecked((int)row.GetProperty("played_bytes").GetUInt32()),
                    unchecked((int)row.GetProperty("played_frames").GetUInt32()));
                Assert.Equal(row.GetProperty("expected_bytes").GetInt32(),buffer.NumBytesToSend);
                Assert.Equal(row.GetProperty("expected_frames").GetInt32(),buffer.NumAudioFramesToSend);
            }
            else if (row.GetProperty("function").GetString()=="named_color")
            {
                string name=row.GetProperty("input").GetString()!;
                Assert.Equal(NativeOracleFixtures.NamedColor(name),NamedColors.GetByString(name));
            }
            else
            {
                string name=row.GetProperty("case").GetString()!;
                var canvas=Convert.FromHexString(row.GetProperty("canvas").GetString()!);
                Assert.Equal(0,row.GetProperty("expected_return").GetInt32());
                Assert.Equal(NativeOracleFixtures.Rle(name),FaceBitmapCodec.EncodeCanvas(canvas,64,128));
                Assert.Equal(NativeOracleFixtures.Rle(name),FaceBitmapCodec.Encode(NativeOracleFixtures.Bitmap(name)));
            }
        }
    }
}
