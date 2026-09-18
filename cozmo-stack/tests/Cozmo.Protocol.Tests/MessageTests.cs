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
        Assert.Equal("SyncTimeAck", MessageCatalog.Lookup(0xC2)!.CladType);          // PyCozmo mislabels this RobotDelocalized
        Assert.Equal("DriveWheelsCurvature", MessageCatalog.Lookup(0x33)!.CladType); // PyCozmo mislabels this TurnInPlaceAtSpeed
        Assert.Equal(91, MessageCatalog.Lookup(0xF0)!.OfficialSize);
    }

    [Fact]
    public void EveryCatalogEntryHasAGeneratedCodec()
    {
        foreach (var info in MessageCatalog.ById.Values)
            Assert.True(GeneratedMessages.Parsers.ContainsKey(info.Id), $"no codec for {info.CladType}");
        Assert.Equal(161, GeneratedMessages.Parsers.Count);
    }

    /// <summary>Every fixed-size message must serialise to exactly the size the engine's own Size() returns.</summary>
    [Fact]
    public void GeneratedCodecsMatchOfficialSizes()
    {
        var mismatches = new List<string>();
        foreach (var info in MessageCatalog.ById.Values.Where(i => !i.VariableLength && i.OfficialSize >= 0
                                                                  && i.Confidence != LayoutConfidence.Partial))
        {
            var msg = GeneratedMessages.Parsers[info.Id](new CladReader(new byte[Math.Max(info.OfficialSize, 1) * 8]));
            int n = msg.ToBytes().Length - 1;
            if (n != info.OfficialSize) mismatches.Add($"{info.CladType}: wrote {n}, official {info.OfficialSize}");
        }
        Assert.Empty(mismatches);
    }

    /// <summary>Round-trip every message from a deterministic byte pattern: decode then re-encode must be identical.</summary>
    [Fact]
    public void GeneratedCodecsRoundTripFixedSizeMessages()
    {
        var rnd = new Random(1234);
        var bad = new List<string>();
        foreach (var info in MessageCatalog.ById.Values.Where(i => !i.VariableLength && i.OfficialSize >= 0
                                                                  && i.Confidence != LayoutConfidence.Partial))
        {
            var body = new byte[info.OfficialSize];
            rnd.NextBytes(body);
            var full = new byte[body.Length + 1];
            full[0] = (byte)info.Id;
            body.CopyTo(full, 1);
            var m = RobotMessage.Parse(full);
            if (m is RawRobotMessage raw) { bad.Add($"{info.CladType}: {raw.ParseNote}"); continue; }
            // Encoding is idempotent: bool/enum fields normalise on the first pass (a bool byte of 0x7f
            // comes back as 0x01), so compare the second round against the first.
            var once = m.ToBytes();
            if (once.Length != full.Length) bad.Add($"{info.CladType}: wrote {once.Length - 1}, expected {info.OfficialSize}");
            var twice = RobotMessage.Parse(once).ToBytes();
            if (!twice.SequenceEqual(once)) bad.Add($"{info.CladType}: not idempotent");
        }
        Assert.Empty(bad);
    }

    [Fact]
    public void VariableLengthMessagesRoundTrip()
    {
        var img = new ImageChunk { FrameTimestamp = 7, ImageId = 3, Data = new byte[] { 1, 2, 3, 4, 5 } };
        Assert.Equal(img.ToBytes(), RobotMessage.Parse(img.ToBytes()).ToBytes());
        var nv = new NVCommand { Tag = 0x180000, Length = 4, Data = new byte[] { 9, 9 } };
        Assert.Equal(nv.ToBytes(), RobotMessage.Parse(nv.ToBytes()).ToBytes());
        var tr = new PrintTrace { FormatId = 1, NameId = 2, Level = 3, Args = new[] { 10, 20 } };
        Assert.Equal(tr.ToBytes(), RobotMessage.Parse(tr.ToBytes()).ToBytes());
    }

    [Fact]
    public void LightStateIs10BytesAndBackpackMessageIs31()
    {
        var w = new CladWriter();
        LightState.Solid(0x7FFF).Write(w);
        Assert.Equal(10, w.Length);
        Assert.Equal(31, new BackpackLightsMiddle().ToBytes().Length - 1);   // 3 * LightState + 1
        Assert.Equal(21, new BackpackLightsTurnSignals().ToBytes().Length - 1); // 2 * LightState + 1
        Assert.Equal(40, new CubeLights().ToBytes().Length - 1);             // 4 * LightState
    }

    [Fact]
    public void RobotStateDecodesFieldOrder()
    {
        var w = new CladWriter().U8((byte)RobotMessageId.State)
            .U32(1000).U32(2).U32(3)
            .F32(10).F32(20).F32(30).F32(0.5f).F32(0.1f)
            .F32(11).F32(12).F32(-0.3f).F32(45)
            .F32(1).F32(2).F32(3).F32(4).F32(5).F32(6)
            .F32(3.9f).U32(0x1200)
            .U16(100).U16(200).U16(300).U16(400).U16(7).I8(9);
        var s = Assert.IsType<RobotState>(RobotMessage.Parse(w.ToArray()));
        Assert.Equal(1000u, s.Timestamp);
        Assert.Equal(3u, s.PoseOriginId);
        Assert.Equal(10f, s.PoseX);
        Assert.Equal(0.5f, s.PoseAngleRad);
        Assert.Equal(-0.3f, s.HeadAngle);
        Assert.Equal(45f, s.LiftAngle);
        Assert.Equal(3.9f, s.BatteryVoltage);
        Assert.True(s.Has(RobotStatusFlag.IsOnCharger));
        Assert.True(s.Has(RobotStatusFlag.HeadInPos));
        Assert.Equal(new ushort[] { 100, 200, 300, 400 }, s.CliffDataRaw);
        Assert.Equal(7, s.BackpackTouchSensorRaw);
        Assert.Equal(9, s.CurrPathSegment);
        Assert.Equal(91, s.ToBytes().Length - 1);
    }

    [Fact]
    public void UnknownTagsBecomeRawAndRoundTrip()
    {
        var bytes = new byte[] { 0x99, 1, 2, 3, 4 };   // animBodyMotion is 4 B; give it 4 payload bytes
        Assert.IsNotType<RawRobotMessage>(RobotMessage.Parse(bytes));
        var unknown = new byte[] { 0x13, 7, 7 };       // 0x13 is not an official tag
        var raw = Assert.IsType<RawRobotMessage>(RobotMessage.Parse(unknown));
        Assert.Equal(unknown, raw.ToBytes());
    }

    [Fact]
    public void FirmwareVersionParsesSignatureJson()
    {
        string json = "{\"version\": 2381, \"messageEngineToRobotHash\": \"9e4a965ace4e09d86997b87ba14235d5\", " +
                      "\"messageRobotToEngineHash\": \"a259247f16231db440957215baba12ab\", \"build\": \"DEVELOPMENT\"}";
        var fw = new FirmwareVersion { RobotId = 0x4D9D, Signature = System.Text.Encoding.UTF8.GetBytes(json) };
        var back = Assert.IsType<FirmwareVersion>(RobotMessage.Parse(fw.ToBytes()));
        Assert.Equal(2381, back.Version);
        Assert.Equal(0x4D9D, back.RobotId);
        Assert.Equal("9e4a965ace4e09d86997b87ba14235d5", back.EngineToRobotHash);
        Assert.Equal("DEVELOPMENT", back.Build);
        Assert.Equal(fw.ToBytes(), back.ToBytes());
    }

    [Fact]
    public void CatalogRecordsSubsystemSafetyAndVerification()
    {
        var state = MessageCatalog.Lookup(0xF0)!;
        Assert.Equal(Subsystem.RobotStateSensors, state.Subsystem);
        Assert.Equal(VerificationStatus.HardwareVerified, state.Verification);
        Assert.Equal(ProbeSafety.Destructive, MessageCatalog.Lookup(0xAF)!.Safety);   // OTA write
        Assert.Equal(ProbeSafety.ReadOnly, MessageCatalog.Lookup(0x25)!.Safety);      // GetManufacturingInfo
        Assert.Equal(Subsystem.CubesBle, MessageCatalog.Lookup(0x04)!.Subsystem);
        Assert.NotEmpty(MessageCatalog.BySubsystem(Subsystem.Camera));
    }
}
