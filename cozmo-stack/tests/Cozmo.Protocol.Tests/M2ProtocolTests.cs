using System.Buffers.Binary;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Manipulation;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M2 protocol (re-analysis/inventory/M2-protocol.md, frozen 2026-09-24). Every expected value below is taken
/// from an inventory row, named in the test; none is read back from the code under test.
/// </summary>
public class M2ProtocolTests
{
    private static byte[] Msg(byte tag, params byte[] body) => new[] { tag }.Concat(body).ToArray();
    private static byte[] U32(uint v) { var b = new byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(b, v); return b; }
    private static byte[] F32(float v) { var b = new byte[4]; BinaryPrimitives.WriteSingleLittleEndian(b, v); return b; }
    private static byte[] U16(ushort v) { var b = new byte[2]; BinaryPrimitives.WriteUInt16LittleEndian(b, v); return b; }
    private static byte[] Cat(params byte[][] parts) => parts.SelectMany(p => p).ToArray();

    private static RobotMessage Kept(byte[] data)
    {
        Assert.True(MessageHandler.TryUnpack(data, out var m), $"tag 0x{data[0]:X2} ({data.Length} bytes) should be kept");
        return m;
    }

    private static void Dropped(byte[] data) =>
        Assert.False(MessageHandler.TryUnpack(data, out _), $"tag 0x{data[0]:X2} ({data.Length} bytes) should be dropped");

    // ------------------------------------------------------------------ M2-001

    /// <summary>M2-001 D9: Read&lt;bool&gt; stores (byte != 0) (0x0083C0F0..0x0083C118): 0x02 is true.</summary>
    [Fact]
    public void M2_001_D9_ABoolReadOfTwoIsTrue()
    {
        var err = Assert.IsType<RobotErrorReport>(Kept(Msg(0xD9, Cat(U32(7), new byte[] { 0x02 }))));
        Assert.True(err.Field1);
        Assert.Equal(7u, err.Field0);
        Assert.True(Assert.IsType<LiftLoad>(Kept(Msg(0xDA, 0x80))).Field0);            // LiftLoad inline Read<bool> 0x007B1BA4
        Assert.False(Assert.IsType<LiftLoad>(Kept(Msg(0xDA, 0x00))).Field0);
    }

    /// <summary>M2-001 S4: Write&lt;bool&gt; stores the C++ bool byte, 0 or 1 (0x0083C0DE).</summary>
    [Fact]
    public void M2_001_S4_ABoolIsWrittenAsZeroOrOne()
    {
        Assert.Equal(new byte[] { 0x0B, 0x01 }, new SetHeadlight(true).ToBytes());
        Assert.Equal(new byte[] { 0x0B, 0x00 }, new SetHeadlight(false).ToBytes());
    }

    /// <summary>M2-001 D13 / S4: fields are little-endian (memcpy of the native value, EI_DATA = 1).</summary>
    [Fact]
    public void M2_001_D13_FieldsAreLittleEndian()
    {
        var b = new IMURequest { LengthMs = 0x11223344 }.ToBytes();
        Assert.Equal(new byte[] { 0x4A, 0x44, 0x33, 0x22, 0x11 }, b);
    }

    /// <summary>
    /// M2-001 S5: the byte-vector writer 0x0071A890 stores the u16 count as end-begin with strh, truncated with no
    /// range check (0x0071A898..0x0071A8A4), then writes every element (0x00732108..0x00732130).
    /// </summary>
    [Fact]
    public void M2_001_S5_TheU16CountIsTruncatedOnWrite()
    {
        var image = new byte[65536 + 3];
        image[^1] = 0xAB;
        var b = new FaceImage { Image = image }.ToBytes();
        Assert.Equal(0x97, b[0]);
        Assert.Equal(3, BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(1)));          // 65539 & 0xFFFF
        Assert.Equal(1 + 2 + image.Length, b.Length);                                   // every element is written
        Assert.Equal(0xAB, b[^1]);
    }

    /// <summary>
    /// M2-001 D8: ReadBytes is all-or-nothing (0x0083C09A..0x0083C0CA): on overrun it copies nothing and does not
    /// advance, and there is no sticky error, so a later smaller read still succeeds.
    /// </summary>
    [Fact]
    public void M2_001_D8_AFailedReadDoesNotAdvanceAndIsNotSticky()
    {
        var r = new CladReader(new byte[] { 0x05, 0x06, 0x07 });
        r.U32();
        Assert.True(r.LastReadFailed);
        Assert.Equal(0, r.Position);
        Assert.Equal(0x0605, r.U16());
        Assert.False(r.LastReadFailed);
        Assert.Equal(0x07, r.U8());
        Assert.Equal(0, r.Remaining);
    }

    /// <summary>M2-001 D12: a fixed array has no prefix and its loop stops at the first failed element (0x007D6EB6..0x007D6EDA).</summary>
    [Fact]
    public void M2_001_D12_AFixedArrayStopsAtTheFirstFailedElement()
    {
        var r = new CladReader(new byte[] { 1, 0, 2, 0, 3 });
        var a = r.Array(4, r.U16);
        Assert.Equal(4, a.Length);
        Assert.Equal((ushort)1, a[0]);
        Assert.Equal((ushort)2, a[1]);
        Assert.Equal(1, r.Remaining);                                                    // the odd byte is not consumed
    }

    /// <summary>
    /// M2-001 D10: a u8-count string reads its chars one at a time (0x006C235C) and stops at the first failure,
    /// so PrintText with a count of 5 and two chars present is consumed exactly and kept (D11).
    /// </summary>
    [Fact]
    public void M2_001_D10_AnOverCountedStringIsKeptShorter()
    {
        var t = Assert.IsType<PrintText>(Kept(Msg(0xB1, 0x09, 0x05, (byte)'a', (byte)'b')));
        Assert.Equal("ab", t.Field1);
    }

    // ------------------------------------------------------------------ M2-002

    /// <summary>M2-002: the 17 RobotStatusFlag names and values (EnumToString 0x007D57C8; Unity RobotStatusFlag.cs:8-25).</summary>
    [Fact]
    public void M2_002_TheStatusFlagsHaveTheEnginesValues()
    {
        var expected = new Dictionary<RobotStatusFlag, uint>
        {
            [RobotStatusFlag.IsMoving] = 0x1, [RobotStatusFlag.IsCarryingBlock] = 0x2, [RobotStatusFlag.IsPickingOrPlacing] = 0x4,
            [RobotStatusFlag.IsPickedUp] = 0x8, [RobotStatusFlag.IsBodyAccMode] = 0x10, [RobotStatusFlag.IsFalling] = 0x20,
            [RobotStatusFlag.IsAnimating] = 0x40, [RobotStatusFlag.IsPathing] = 0x80, [RobotStatusFlag.LiftInPos] = 0x100,
            [RobotStatusFlag.HeadInPos] = 0x200, [RobotStatusFlag.IsAnimBufferFull] = 0x400, [RobotStatusFlag.IsAnimatingIdle] = 0x800,
            [RobotStatusFlag.IsOnCharger] = 0x1000, [RobotStatusFlag.IsCharging] = 0x2000, [RobotStatusFlag.CliffDetected] = 0x4000,
            [RobotStatusFlag.AreWheelsMoving] = 0x8000, [RobotStatusFlag.IsChargerOos] = 0x10000,
        };
        Assert.Equal(17, Enum.GetValues<RobotStatusFlag>().Length);
        foreach (var (flag, value) in expected) Assert.Equal(value, (uint)flag);
    }

    // ------------------------------------------------------------------ M2-003

    /// <summary>
    /// M2-003 RS7: GetLiftHeight is 66 sin(angle) + 45 + 0 with no clamp (0x00516F64..0x00516F8E); the 32..92 clamp
    /// is only on height-to-angle (0x005170B0).
    /// </summary>
    [Fact]
    public void M2_003_RS7_AngleToHeightIsNotClamped()
    {
        Assert.Equal(66f + 45f, RobotState.LiftHeightMmFromAngle(MathF.PI / 2), 3);      // 111 mm, above 92
        Assert.Equal(45f - 66f, RobotState.LiftHeightMmFromAngle(-MathF.PI / 2), 3);     // -21 mm, below 32
        Assert.Equal(111f, new RobotState { LiftAngle = MathF.PI / 2 }.LiftHeightMm, 3);
    }

    // ------------------------------------------------------------------ M2-004

    /// <summary>M2-004: SyncTime {u32 timestamp, f32 -20.0} (SendSyncTime 0x0051524C; 0xC1A00000; vcmp.f32 at 0x007A3B0A).</summary>
    [Fact]
    public void M2_004_SyncTimeIsATimestampAndMinusTwenty()
    {
        var b = new SyncTime(0x01020304).ToBytes();
        Assert.Equal(Cat(new byte[] { 0x4B }, U32(0x01020304), U32(0xC1A00000)), b);
        Assert.IsType<float>(new SyncTime(0).Unknown);
    }

    // ------------------------------------------------------------------ M2-006

    /// <summary>M2-006: FirmwareVersion 0xEE is {2B, u16-count u8[]} (Unpack 0x007B907E).</summary>
    [Fact]
    public void M2_006_FirmwareVersionIsAU16AndAU16CountedByteArray()
    {
        var f = Assert.IsType<FirmwareVersion>(Kept(Msg(0xEE, Cat(U16(0x4D9D), U16(2), new byte[] { (byte)'{', (byte)'}' }))));
        Assert.Equal(0x4D9D, f.RobotId);
        Assert.Equal(new byte[] { (byte)'{', (byte)'}' }, f.Signature);
    }

    // ------------------------------------------------------------------ M2-008

    /// <summary>M2-008 S1/S3/S7: the tag byte first, then the member; size = 1 + member size.</summary>
    [Fact]
    public void M2_008_TheTagComesFirstAndTheSizeIsOnePlusTheMember()
    {
        Assert.Equal(new byte[] { 0x3B }, new StopAllMotors().ToBytes());                // empty member (0x007AB868)
        Assert.Equal(new byte[] { 0x25 }, new GetManufacturingInfo().ToBytes());
        Assert.Equal(Cat(new byte[] { 0x34 }, F32(1.5f)), new MoveLift { SpeedRadPerSec = 1.5f }.ToBytes());   // 4-byte inline (0x007AB834)
        Assert.Equal(Cat(new byte[] { 0x64 }, U16(0x1234)), new SetAudioVolume { Level = 0x1234 }.ToBytes());  // 2-byte inline (0x007AB84A)
    }

    // ------------------------------------------------------------------ M2-009

    /// <summary>M2-009: the sizes of the 42 engine-to-robot messages this stack sends (Appendix A section 1), with empty arrays for the counted ones.</summary>
    [Fact]
    public void M2_009_OutboundSizesAreTheEngines()
    {
        var sizes = new Dictionary<byte, int>
        {
            [0x03] = 31, [0x05] = 5, [0x08] = 5, [0x0A] = 1, [0x0B] = 1, [0x25] = 0, [0x32] = 16, [0x34] = 4, [0x35] = 4,
            [0x36] = 17, [0x37] = 17, [0x39] = 20, [0x3B] = 0, [0x3C] = 2, [0x3D] = 28, [0x3E] = 32, [0x3F] = 29, [0x41] = 3,
            [0x42] = 21, [0x43] = 0, [0x44] = 25, [0x45] = 24, [0x48] = 22, [0x4A] = 4, [0x4B] = 8, [0x4C] = 2, [0x58] = 2,
            [0x60] = 1, [0x64] = 2, [0x66] = 1, [0x80] = 4, [0x81] = 12, [0x8E] = 744, [0x8F] = 0, [0x93] = 3, [0x94] = 3,
            [0x97] = 2, [0x99] = 4, [0x9A] = 0, [0x9B] = 1, [0x9F] = 0, [0xA0] = 16,
        };
        Assert.Equal(42, sizes.Count);
        foreach (var (tag, size) in sizes)
        {
            var m = GeneratedMessages.Parsers[(RobotMessageId)tag](new CladReader(new byte[size]));
            Assert.Equal(1 + size, m.ToBytes().Length);
        }
    }

    /// <summary>
    /// M2-009 section 6: the engine writes bools where the definition had u8 (SetBodyAngle 0x007A29BE, PointTurn
    /// 0x007A343C, DockWithObject 0x007C06B4/0x007C06DC, PlaceObjectOnGround 0x007C0986, DockingErrorSignal
    /// 0x007C0B78/0x007C0B80), and f32 in DockWithObject word 0 (vcmp.f32, 0x007C0722) and SyncTime word 1.
    /// </summary>
    [Fact]
    public void M2_009_TheOutboundFieldTypesAreTheEngines()
    {
        Assert.Equal(typeof(bool), typeof(SetBodyAngle).GetField(nameof(SetBodyAngle.IsAbsolute))!.FieldType);
        Assert.Equal(typeof(bool), typeof(AppendPathSegmentPointTurn).GetField(nameof(AppendPathSegmentPointTurn.UseShortestDirection))!.FieldType);
        Assert.Equal(typeof(float), typeof(DockWithObject).GetField(nameof(DockWithObject.UnusedZero))!.FieldType);
        Assert.Equal(typeof(bool), typeof(DockWithObject).GetField(nameof(DockWithObject.Field5))!.FieldType);
        Assert.Equal(typeof(bool), typeof(DockWithObject).GetField(nameof(DockWithObject.Field8))!.FieldType);
        Assert.Equal(typeof(bool), typeof(PlaceObjectOnGround).GetField(nameof(PlaceObjectOnGround.Field6))!.FieldType);
        Assert.Equal(typeof(bool), typeof(DockingErrorSignal).GetField(nameof(DockingErrorSignal.Field5))!.FieldType);
        Assert.Equal(typeof(bool), typeof(DockingErrorSignal).GetField(nameof(DockingErrorSignal.Field6))!.FieldType);
        Assert.Equal(typeof(float), typeof(SyncTime).GetField(nameof(SyncTime.Unknown))!.FieldType);
        // SetBodyAngle: f32 x4, u16, bool, u8 - the bool is the byte at member offset 18
        var b = new SetBodyAngle { IsAbsolute = true, ActionId = 9 }.ToBytes();
        Assert.Equal(0x01, b[1 + 18]);
        Assert.Equal(9, b[1 + 19]);
    }

    // ------------------------------------------------------------------ M2-010

    /// <summary>
    /// M2-010 D4/D5: tags outside 0xB0..0xF5 and the 14 in-range tags without a codec (0xCC, 0xDF..0xEB) take the
    /// default at 0x007B1BBE and consume the tag byte alone: kept at 1 byte, a size error at 2.
    /// </summary>
    [Fact]
    public void M2_010_D5_ATagWithoutACodecConsumesOnlyTheTag()
    {
        var noCodec = new List<byte> { 0xCC };
        for (int t = 0xDF; t <= 0xEB; t++) noCodec.Add((byte)t);
        Assert.Equal(14, noCodec.Count);
        foreach (var t in noCodec.Concat(new byte[] { 0x10, 0xAF, 0xF6, 0xFF }))
        {
            Kept(new[] { t });
            Dropped(new byte[] { t, 0x00 });
        }
    }

    /// <summary>M2-010 D5: the other 56 in-range tags are exactly the protocol definition's robot-to-engine codecs.</summary>
    [Fact]
    public void M2_010_D5_TheInRangeCodecsAreTheFiftySix()
    {
        var expected = Enumerable.Range(0xB0, 0xF5 - 0xB0 + 1).Where(t => t != 0xCC && (t < 0xDF || t > 0xEB)).ToHashSet();
        var codecs = GeneratedMessages.Parsers.Keys.Select(k => (int)k).Where(t => t >= 0xB0).ToHashSet();
        Assert.Equal(56, expected.Count);
        Assert.Equal(expected.OrderBy(x => x), codecs.OrderBy(x => x));
    }

    // ------------------------------------------------------------------ M2-011

    /// <summary>M2-011 D1/D11: a byte beyond the codec's fields is never consumed, so the message is dropped.</summary>
    [Fact]
    public void M2_011_D11_ATrailingByteDropsTheMessage()
    {
        Kept(Msg(0xDE, new byte[12]));
        Dropped(Msg(0xDE, new byte[13]));
    }

    /// <summary>
    /// M2-011 D10/D11: a u8 array whose count exceeds the data present stops at the first failed element and is
    /// consumed exactly, so it is kept shorter (ImageChunk.data, u16 count, 0x0071A8F6).
    /// </summary>
    [Fact]
    public void M2_011_D11_AnOverCountedU8ArrayIsKeptShorter()
    {
        var body = Cat(U32(1), U32(2), U32(3), new byte[] { 8, 4, 7, 0 }, U16(2), U16(100), new byte[] { 1, 2, 3, 4, 5 });
        var c = Assert.IsType<ImageChunk>(Kept(Msg(0xF2, body)));
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, c.Data);
    }

    /// <summary>
    /// M2-011 D10/D11: an over-counted i32 array (PrintTrace, u8 count, 0x0073923A) with a partial element left
    /// over stops at that element; the leftover bytes are not consumed and the message is dropped.
    /// </summary>
    [Fact]
    public void M2_011_D11_APartialI32ElementLeavesBytesAndIsDropped()
    {
        Kept(Msg(0xB0, Cat(U32(1), U16(2), new byte[] { 3 }, new byte[] { 1 }, U32(42))));          // count 1, one element
        Dropped(Msg(0xB0, Cat(U32(1), U16(2), new byte[] { 3 }, new byte[] { 2 }, U32(42), new byte[] { 0 })));   // count 2, 4 + 1 bytes
    }

    /// <summary>
    /// M2-011 D8/D11: a truncated fixed message is kept when later, smaller reads consume exactly the leftover.
    /// RobotErrorReport {4B, bool}: with one body byte the 4-byte read fails without advancing and the bool
    /// consumes the byte, so 2 == 2.
    /// </summary>
    [Fact]
    public void M2_011_D11_ATruncatedFixedMessageCanStillBeConsumedExactly()
    {
        var err = Assert.IsType<RobotErrorReport>(Kept(Msg(0xD9, 0x01)));
        Assert.True(err.Field1);
        // three body bytes: the 4-byte read fails, the bool takes one byte, two are left over
        Dropped(Msg(0xD9, 0x01, 0x02, 0x03));
    }

    // ------------------------------------------------------------------ M2-012

    /// <summary>M2-012: the sizes of the 56 robot-to-engine codecs (Appendix B section 2), counted arrays empty.</summary>
    [Fact]
    public void M2_012_InboundSizesAreTheEngines()
    {
        var sizes = new Dictionary<byte, int>
        {
            [0xB0] = 7 + 1, [0xB1] = 1 + 1, [0xB2] = 16, [0xB3] = 21, [0xB4] = 21, [0xB5] = 8, [0xB6] = 12, [0xB7] = 4 + 1,
            [0xB8] = 7, [0xB9] = 10, [0xBA] = 4, [0xBB] = 5, [0xBC] = 4, [0xBD] = 5, [0xBE] = 9 + 1, [0xBF] = 195,
            [0xC0] = 6, [0xC1] = 0, [0xC2] = 0, [0xC3] = 0, [0xC4] = 1, [0xC5] = 1, [0xC6] = 3, [0xC7] = 14, [0xC8] = 29,
            [0xC9] = 6, [0xCA] = 1, [0xCB] = 1, [0xCD] = 10 + 2, [0xCE] = 9, [0xCF] = 7 + 1, [0xD0] = 13, [0xD1] = 3,
            [0xD2] = 44, [0xD3] = 5, [0xD4] = 1, [0xD5] = 6, [0xD6] = 4, [0xD7] = 9, [0xD8] = 2, [0xD9] = 5, [0xDA] = 1,
            [0xDB] = 1, [0xDC] = 4, [0xDD] = 4, [0xDE] = 12, [0xEC] = 4, [0xED] = 12, [0xEE] = 2 + 2, [0xEF] = 7,
            [0xF0] = 91, [0xF1] = 15, [0xF2] = 18 + 2, [0xF3] = 9, [0xF4] = 17, [0xF5] = 20,
        };
        Assert.Equal(56, sizes.Count);
        foreach (var (tag, size) in sizes)
        {
            Kept(Msg(tag, new byte[size]));
            Dropped(Msg(tag, new byte[size + 1]));
        }
    }

    /// <summary>
    /// M2-012 section 2: the inbound bools the definition had as u8 (GoalPose 0x007C2206, PickAndPlaceResult
    /// 0x007C1ADA, Ramp/BridgeTraverseComplete, TimeProfileStat field2, RobotErrorReport 0x007D3E70, LiftLoad
    /// 0x007B1BA4) all read a non-zero byte as true; PrintTrace's first field is one 4-byte field (0x007D4BF6).
    /// </summary>
    [Fact]
    public void M2_012_TheInboundBoolsReadNonZeroAsTrue()
    {
        Assert.True(Assert.IsType<GoalPose>(Kept(Msg(0xB3, Cat(new byte[20], new byte[] { 0x05 })))).Field1);
        Assert.True(Assert.IsType<RampTraverseComplete>(Kept(Msg(0xBB, Cat(U32(0), new byte[] { 0x05 })))).Field1);
        Assert.True(Assert.IsType<BridgeTraverseComplete>(Kept(Msg(0xBD, Cat(U32(0), new byte[] { 0x05 })))).Field1);
        Assert.True(Assert.IsType<TimeProfileStat>(Kept(Msg(0xBE, Cat(U32(0), U32(0), new byte[] { 0x05, 0x00 })))).Field2);
        var t = Assert.IsType<PrintTrace>(Kept(Msg(0xB0, Cat(U32(0xAABBCCDD), U16(7), new byte[] { 1, 0 }))));
        Assert.Equal(0xAABBCCDDu, t.FormatId);
        Assert.Equal(7, t.NameId);
    }

    // ------------------------------------------------------------------ M2-013

    /// <summary>
    /// M2-013: FallingStopped 0xDE is {u32 timestamp, u32 duration_ms, f32 impactIntensity} (Unpack 0x007B0EB6;
    /// HandleFallingStopped ldrd r1,r2,[r5] 0x0053506C and vldr s0,[r5,#8] 0x00535068), and the sensor report
    /// carries the duration and the intensity from those fields.
    /// </summary>
    [Fact]
    public void M2_013_FallingStoppedBytesAreTimestampDurationIntensity()
    {
        var data = Msg(0xDE, Cat(U32(123456), U32(250), F32(1500.5f)));
        var f = Assert.IsType<FallingStopped>(Kept(data));
        Assert.Equal(123456u, f.Timestamp);
        Assert.Equal(250u, f.DurationMs);
        Assert.Equal(1500.5f, f.ImpactIntensity);

        using var robot = CozmoRobot.CreateOffline();
        FallingStoppedReport? report = null;
        robot.Sensors.FallingStopped += r => report = r;
        robot.Sensors.Handle(f);
        Assert.NotNull(report);
        Assert.Equal(250u, report!.DurationMs);
        Assert.Equal(1500.5f, report.ImpactIntensity);
        Assert.Equal(123456u, report.Timestamp);
    }

    // ------------------------------------------------------------------ M2-014

    /// <summary>
    /// M2-014: BlockStatus 0 NO_BLOCK, 1 BLOCK_PLACED, 2 BLOCK_PICKED_UP (EnumToString table 0x01034984), and
    /// PickAndPlaceResult {u32, bool, i8, u8} (Unpack 0x007C1AC6; the handler reads +5 with ldrsb).
    /// </summary>
    [Fact]
    public void M2_014_BlockStatusValuesAndThePickAndPlaceLayout()
    {
        Assert.Equal(0, (byte)BlockStatus.NoBlock);
        Assert.Equal(1, (byte)BlockStatus.BlockPlaced);
        Assert.Equal(2, (byte)BlockStatus.BlockPickedUp);
        var r = Assert.IsType<PickAndPlaceResult>(Kept(Msg(0xB8, Cat(U32(9), new byte[] { 0x03, 0xFF, 0x02 }))));
        Assert.Equal(9u, r.Field0);
        Assert.True(r.Field1);
        Assert.Equal((sbyte)-1, r.Field2);
        Assert.Equal(BlockStatus.BlockPickedUp, (BlockStatus)r.Field3);
    }

    // ------------------------------------------------------------------ M2-015

    /// <summary>M2-015: the builders copy the caller's values verbatim (MoveHeadToAngle 0x006407CC stm 0x00640838; DriveWheels 0x0063F174).</summary>
    [Fact]
    public void M2_015_BuildersCopyTheCallersValuesVerbatim()
    {
        var h = new SetHeadAngle(0.25f, 2.5f, 3.5f, 0.75f, 9).ToBytes();
        Assert.Equal(Cat(new byte[] { 0x37 }, F32(0.25f), F32(2.5f), F32(3.5f), F32(0.75f), new byte[] { 9 }), h);
        var l = new SetLiftHeight(50f, 1.5f, 4.5f, 0.5f, 7).ToBytes();
        Assert.Equal(Cat(new byte[] { 0x36 }, F32(50f), F32(1.5f), F32(4.5f), F32(0.5f), new byte[] { 7 }), l);
        var d = new DriveWheels(10f, -20f, 30f, 40f).ToBytes();
        Assert.Equal(Cat(new byte[] { 0x32 }, F32(10f), F32(-20f), F32(30f), F32(40f)), d);
    }

    // ------------------------------------------------------------------ M2-016

    /// <summary>
    /// M2-016: ImageChunk is u32 ts, u32 id, i32 chunkDebug, u8 imageEncoding, i8 resolution, u8 count, u8 chunkId,
    /// i16 status, u16-count data (Appendix C section 2; MD6 for chunkDebug and status).
    /// </summary>
    [Fact]
    public void M2_016_ImageChunkFieldSignedness()
    {
        var body = Cat(U32(0xFFFFFFF0), U32(5), U32(0xFFFFFFFF), new byte[] { 0xF0, 0xFF, 0x80, 0x81 }, U16(0xFFFF), U16(0));
        var c = Assert.IsType<ImageChunk>(Kept(Msg(0xF2, body)));
        Assert.Equal(0xFFFFFFF0u, c.FrameTimestamp);
        Assert.Equal(5u, c.ImageId);
        Assert.Equal(-1, c.ChunkDebug);
        Assert.Equal((byte)0xF0, c.ImageEncoding);                                     // u8: 240, not -16
        Assert.Equal((sbyte)-1, c.ImageResolution);
        Assert.Equal((byte)0x80, c.ImageChunkCount);
        Assert.Equal((byte)0x81, c.ChunkId);
        Assert.Equal((short)-1, c.Status);
        Assert.Empty(c.Data);
    }
}
