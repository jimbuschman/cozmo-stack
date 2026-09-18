using Cozmo.Protocol;
using Xunit;

namespace Cozmo.Protocol.Tests;

public class MessageTests
{
    [Fact]
    public void CatalogHasAll161OfficialMessages()
    {
        Assert.Equal(105, MessageCatalog.EngineToRobotCount);
        Assert.Equal(56, MessageCatalog.RobotToEngineCount);
        Assert.Equal(161, MessageCatalog.ById.Count);
        Assert.Equal("DriveWheels", MessageCatalog.Lookup(0x32)!.CladType);
        Assert.Equal("SyncTimeAck", MessageCatalog.Lookup(0xC2)!.CladType); // PyCozmo mislabels this RobotDelocalized
        Assert.Equal("DriveWheelsCurvature", MessageCatalog.Lookup(0x33)!.CladType); // PyCozmo mislabels this TurnInPlaceAtSpeed
        Assert.Equal(91, MessageCatalog.Lookup(0xF0)!.OfficialSize);
    }

    public static IEnumerable<object[]> FixedSizeMessages() => new[]
    {
        new object[] { new RobotState() },
        new object[] { new RobotAvailable() },
        new object[] { new ManufacturingId() },
        new object[] { new SyncTime(1, 2) },
        new object[] { new SyncTimeAck() },
        new object[] { new InitAnimController() },
        new object[] { new GetManufacturingInfo() },
        new object[] { new SetHeadAngle(0.5f, 1, 2, 3, 4) },
        new object[] { new SetLiftHeight(40f) },
        new object[] { new MotorActionAck(3) },
        new object[] { new SetHeadlight(true) },
        new object[] { new EnableStopOnCliff(true) },
        new object[] { new StopAllMotors() },
        new object[] { new DriveWheels(1, 2, 3, 4) },
        new object[] { new AnimationState() },
        new object[] { new RobotPoked() },
        new object[] { new BackpackButton(true) },
        new object[] { new MotorCalibration(1, true, false) },
        new object[] { new ObjectAvailable(1, 2, -3) },
        new object[] { new FallingStarted(1) },
        new object[] { new FallingStopped(1, 2, 3) },
        new object[] { new AbsoluteLocalizationUpdate(0, 0, 1, 0, 0, 0x80000000) },
        new object[] { new ImageRequest(1) },
        new object[] { new RobotStopped(1) },
    };

    [Theory]
    [MemberData(nameof(FixedSizeMessages))]
    public void TypedMessageBodySizeMatchesOfficialSize(RobotMessage m)
    {
        var info = MessageCatalog.Lookup((byte)m.Id)!;
        Assert.True(info.OfficialSize >= 0, $"{m.Id} is variable in the catalog");
        Assert.Equal(info.OfficialSize, m.ToBytes().Length - 1);
    }

    [Theory]
    [MemberData(nameof(FixedSizeMessages))]
    public void TypedMessagesRoundTrip(RobotMessage m)
    {
        var bytes = m.ToBytes();
        var back = RobotMessage.Parse(bytes);
        Assert.IsNotType<RawRobotMessage>(back);
        Assert.Equal(bytes, back.ToBytes());
    }

    [Fact]
    public void LightStateIs10BytesAndBackpackMessageIs31()
    {
        Assert.Equal(10, new CladWriterHelper().Size(w => LightState.Solid(0x7fff).Write(w)));
        var m = new SetBackpackLightsMiddle();
        Assert.Equal(31, m.ToBytes().Length - 1); // PyCozmo LightStateCenter 3*10+1 (official Size() is variable-length)
    }

    [Fact]
    public void RobotStateDecodesFieldOrder()
    {
        var w = new CladWriter().U8((byte)RobotMessageId.State)
            .U32(1000).U32(2).U32(3).F32(10).F32(20).F32(30).F32(0.5f).F32(0.1f)
            .F32(11).F32(12).F32(-0.3f).F32(45).F32(1).F32(2).F32(3).F32(4).F32(5).F32(6)
            .F32(3.9f).U32(0x1200).U16(100).U16(200).U16(300).U16(400).U16(7).U8(9);
        var s = Assert.IsType<RobotState>(RobotMessage.Parse(w.ToArray()));
        Assert.Equal(1000u, s.Timestamp); Assert.Equal(3u, s.PoseOriginId); Assert.Equal(-0.3f, s.HeadAngleRad); Assert.Equal(45f, s.LiftHeightMm);
        Assert.Equal(3.9f, s.BatteryVoltage); Assert.True(s.Has(RobotStatusFlag.IsOnCharger)); Assert.True(s.Has(RobotStatusFlag.HeadInPos));
        Assert.Equal(new ushort[] { 100, 200, 300, 400 }, s.CliffDataRaw); Assert.Equal(7, s.BackpackTouchSensorRaw); Assert.Equal(9, s.CurrPathSegment);
    }

    [Fact]
    public void UnknownTagsBecomeRawAndRoundTrip()
    {
        var bytes = new byte[] { 0x42, 1, 2, 3, 4 }; // dockWithObject, not modelled yet
        var m = RobotMessage.Parse(bytes);
        var raw = Assert.IsType<RawRobotMessage>(m);
        Assert.Equal(RobotMessageId.DockWithObject, raw.Id);
        Assert.Equal(bytes, raw.ToBytes());
    }

    [Fact]
    public void FirmwareVersionParsesSignatureJson()
    {
        string json = "{\"version\": 2381, \"messageEngineToRobotHash\": \"9e4a965ace4e09d86997b87ba14235d5\", \"messageRobotToEngineHash\": \"a259247f16231db440957215baba12ab\"}";
        var body = new CladWriter().U8((byte)RobotMessageId.FirmwareVersion).String16(json).ToArray();
        var fw = Assert.IsType<FirmwareVersion>(RobotMessage.Parse(body));
        Assert.Equal(2381, fw.Version);
        Assert.Equal("9e4a965ace4e09d86997b87ba14235d5", fw.EngineToRobotHash);
        Assert.Equal("string16", fw.LayoutNote);
    }

    private sealed class CladWriterHelper { public int Size(Action<CladWriter> f) { var w = new CladWriter(); f(w); return w.Length; } }
}
