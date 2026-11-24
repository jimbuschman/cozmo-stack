using Cozmo.Robot.Animation;
using Xunit;

namespace Cozmo.Protocol.Tests;

public sealed class SayTextVoiceStyleTests
{
    // Expected rows read from libcozmoEngine.so at 0x00C573B0 ({style, switch} words), consumed by
    // SayTextAction::Init at 0x0056172A..0x00561736 (SetSwitch group 0x15D4FD9E).
    [Theory]
    [InlineData((byte)0, 0x334C99DCu)] // Unprocessed
    [InlineData((byte)1, 0x2F8B3BF4u)] // CozmoProcessing_Name
    [InlineData((byte)2, 0x2F8B3BF4u)] // CozmoProcessing_Name_Question (Name_FirstIntroduction_1)
    [InlineData((byte)3, 0x36089802u)] // CozmoProcessing_Sentence
    public void EachStyleUsesItsTableSwitch(byte style, uint expected) =>
        Assert.Equal(expected, SayTextAction.VoiceSwitchForStyle(style));

    [Fact]
    public void AStyleOutsideTheTableHasNoSwitch() => Assert.Null(SayTextAction.VoiceSwitchForStyle(4));
}
