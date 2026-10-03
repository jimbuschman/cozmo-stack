using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M6-025 (correction C23, re-analysis/inventory/M6-wwise-bank.md): the voice-to-bus connection creation.
/// Every expected value below is quoted from the inventory rows or the two research files the correction names
/// (20260929-M6-live-audio-bodies-extraction.md items 1 and 5, and 20260929-B-M6b-4-citation-check.md), never
/// read back from the code. The tests are structural (which list, which order, which bit, which count), not
/// numeric, so no emulator oracle applies.
/// </summary>
public class WwiseVoiceLinkerTests
{
    // ------------------------------------------------------------------ rig

    private sealed class Src : IWwiseVoiceSource
    {
        public int Channels => 1;
        public int SampleRate => 48000;
        public int Render(WwiseVoiceBuffer buffer) => 0x2D;
        /// <summary>The raw vt+0x28 result (M6-025 C27 step 7: any int); the arguments it was called with are recorded.</summary>
        public int StartStream(uint arg1DC, uint arg1E0) { Calls.Add((arg1DC, arg1E0)); return Code; }
        public int Code { get; set; } = 1;
        public readonly List<(uint, uint)> Calls = new();
        /// <summary>[source+0x10] bit0: written only by 0xA56650 (WwiseVoiceSourceStart), never by this double.</summary>
        public bool StartStreamSucceeded { get; set; }
        public int OrderKey6C { get; init; }
    }

    private sealed class Rig
    {
        public readonly WwiseMixBusHierarchy Buses = new();
        public readonly WwiseOutputDeviceList Devices = new();
        public readonly List<WwiseLiveVoice> Live = new();
        public readonly WwiseVoiceLinkSeams Seams = new();
        public WwisePbiRouting Routing = new() { Node = NoBus() };
        public readonly WwiseVoiceLinker Linker;
        public int VoiceInits;
        public readonly List<WwiseLineInitArgs> Inits = new();
        /// <summary>The stages 0xA4F0EC reached its unread steps at, in order (re-analysis/tools/emu/emu_line_init.py).</summary>
        public readonly List<int> Stages = new();
        public int AddRefs;
        public readonly Dictionary<IWwiseVoiceSource, WwisePlayingInstance> Owners = new();

        public Rig(bool mainDevice = true)
        {
            if (mainDevice) Devices.CreateMainDevice();
            Seams.InitVoiceA54A30 = _ => { VoiceInits++; return 1; };
            // Test doubles for the unread bodies 0xA4F0EC calls (C24.3): the AddRef, vt+0x98(3) (non-zero), the steps no row reads, the
            // FX holder (allocated). Tests of the failure paths replace them.
            Seams.BusAddRefVt8 = _ => AddRefs++;
            Seams.BusVt98Arg3 = _ => 1;
            Seams.LineInitUnreadSteps = (_, args, stage) => { Stages.Add(stage); if (stage == 1) Inits.Add(args); };
            Seams.LineFxHolder = (_, _) => true;
            Seams.BusVolumeParam5 = _ => -6.5f;
            // Test double for the unread 0xA22A3C build body behind 0x9EA23C (C24.5): a build that yields a value.
            // Tests of the missing-seam and failure paths replace it.
            Seams.BuildDeviceObjectA22A3C = (_, _) => new object();
            Seams.ConnectionChannels = (_, _) => (1, 1);
            Linker = new WwiseVoiceLinker(Buses, Devices, Live, _ => Routing, Seams);
        }

        /// <summary>
        /// The PBI's +0x1DC/+0x1E0 are written by 0xA1EC54 (unread); the test values are the ones the emulator runs of 0xA544BC
        /// and 0xA56650 use (re-analysis/tools/emu/emu_notready.py: vt+0x28 receives 0x11112222 and 0x33334444).
        /// </summary>
        public const uint Media1DC = 0x11112222, Media1E0 = 0x33334444;

        public static WwisePlayingInstance Pbi() => new(
            new WwisePlayInitParams { PlayingId = 1, TargetNodeId = 1 }, 1, new object(), new byte[0x44], null, false)
        { Word1DC = Media1DC, Word1E0 = Media1E0 };

        public static WwiseLiveVoice Voice() => new(1, 8) { FlagsCD = 1 };
    }

    /// <summary>A Master-like bus: bit6 = 1 (row 11), returns itself from vt+0x88 because [bus+0x38] is null.</summary>
    private static WwiseRoutingNode Master(uint id = 100, uint word68 = 0) => new() { Id = id, IsBus = true, Bit6 = true, Word68 = word68 };

    /// <summary>A node whose vt+0x88 returns 0 ("no bus", C24.9): no output bus and no parent.</summary>
    private static WwiseRoutingNode NoBus() => new() { Id = 1, IsBus = false };

    private static WwiseRoutingNode SoundUnder(WwiseRoutingNode bus) => new() { Id = 1, IsBus = false, OutputBus = bus };

    // ------------------------------------------------------------------ device list (C23.18, C23.20, C23.21)

    [Fact]
    public void TheMainDeviceIsId20WithMask0xFFAndConfigWord0x3102()
    {
        // C23.18: entry+0x1C = the constant 0x00003102; C23.21: mask 0xFF at entry+0x18; C23.20: id (2,0).
        var list = new WwiseOutputDeviceList();
        var main = list.CreateMainDevice();
        Assert.Equal(2u, main.Id.Lo);
        Assert.Equal(0u, main.Id.Hi);
        Assert.Equal(0xFFu, main.ListenerMask);
        Assert.Equal(0x00003102u, main.ConfigWord);
        Assert.Equal(0x00003102u, WwiseOutputDeviceList.MainConfigWord);
    }

    [Fact]
    public void ASecondaryDeviceIsAppendedAndClearsItsBitsFromTheMainMask()
    {
        // C23.21: success appends at the tail; a non-zero mask does main.mask &= ~mask (0x9EB450..0x9EB454).
        var list = new WwiseOutputDeviceList();
        var main = list.CreateMainDevice();
        var second = list.Add(new WwiseDeviceId(3, 0), 0x0F, 0x3102);
        Assert.Same(main, list.Entries[0]);
        Assert.Same(second, list.Entries[1]);
        Assert.Equal(0xF0u, main.ListenerMask);
        Assert.Same(second, list.Find(new WwiseDeviceId(3, 0)));
    }

    // ------------------------------------------------------------------ vt+0x88 (row 10) and bit6 (row 11 / 5.7)

    [Fact]
    public void Vt88FollowsTheOutputBusThenTheParentAndABusReturnsItselfOnAPredicate()
    {
        // Row 10: 0x9F1E3C: [node+0x38] if non-null, else [node+0x34], else null; a bus returns itself when
        // any of the 0x9C2A30 predicates holds ([bus+0x38]==0 among them).
        var root = new WwiseRoutingNode { Id = 1, IsBus = true };                       // [bus+0x38]==0
        Assert.Same(root, root.Vt88());

        var passThrough = new WwiseRoutingNode { Id = 2, IsBus = true, OutputBus = root };   // no predicate holds
        Assert.Same(root, passThrough.Vt88());                                          // tail-jumps to 0x9F1E3C

        var withFx = new WwiseRoutingNode { Id = 3, IsBus = true, OutputBus = root, AnyFxEntryNonZero = true };
        Assert.Same(withFx, withFx.Vt88());
        foreach (var setter in new Action<WwiseRoutingNode>[]
        {
            n => n.Vt44Is0C = true, n => n.Byte68 = 1, n => n.Byte46 = 0x80, n => n.Word40 = 0x20000, n => n.Word54 = 1,
        })
        {
            var bus = new WwiseRoutingNode { Id = 4, IsBus = true, OutputBus = root };
            setter(bus);
            Assert.Same(bus, bus.Vt88());
        }

        var viaParent = new WwiseRoutingNode { Id = 5, Parent = root };                // Sound: no [+0x38]
        Assert.Same(root, viaParent.Vt88());                                           // [node+0x34]
        Assert.Null(new WwiseRoutingNode { Id = 6 }.Vt88());                           // top: null
    }

    [Fact]
    public void TheFirstParentlessBusIsTheMasterWithBit6AndTheSecondHasBit6Clear()
    {
        // Row 11 / check row 5.7: 0x9C4314..0x9C4328 first parentless bus bit6 = 1; 0x9C405C..0x9C4064 second
        // (slot [0x108D9B0+0x10] empty) bit6 = 0; a bus given a parent inherits the parent's bit6 (0x9C5598).
        var reg = new WwiseMasterBusRegistry();
        var master = new WwiseRoutingNode { Id = 0xE2B7BC37, IsBus = true };
        var secondary = new WwiseRoutingNode { Id = 0x2FFE6EF7, IsBus = true, Bit6 = true };
        reg.RegisterParentless(master);
        reg.RegisterParentless(secondary);
        Assert.True(master.Bit6);
        Assert.False(secondary.Bit6);
        Assert.Same(master, reg.Master);
        Assert.Same(secondary, reg.Secondary);

        var child = new WwiseRoutingNode { Id = 7, IsBus = true, OutputBus = secondary, Bit6 = true };
        WwiseMasterBusRegistry.InheritFromParent(child, secondary, new[] { child });
        Assert.False(child.Bit6);
    }

    // ------------------------------------------------------------------ 0xA42DEC rows 4 to 6, 12, 16, 17

    [Fact]
    public void AVoiceUnderMasterConnectsToTheMainDeviceWithTheCtorFields()
    {
        // Rows 5-6: listener mask default 1 & main mask 0xFF != 0; bus bit6 = 1 -> the (2,0) device only.
        // Row 16: voice+0xCD |= 4. Row 17: conn+8..+0x17 = 1.0f, +0x48/+0x4C = id, +0x68 = arg5 = 0, +0x6C bit0 = 1,
        // bit2 = !(voice+0xCD bit0) = 0, bit4 = ip = 0. Row 18: +0x1C0++.
        var rig = new Rig();
        var master = Master();
        rig.Routing = new WwisePbiRouting { Node = SoundUnder(master) };
        var voice = Rig.Voice();

        Assert.Equal(1, rig.Linker.Link(voice, Rig.Pbi()));

        var conn = Assert.Single(voice.Connections);
        Assert.Equal(WwiseDeviceId.Main, conn.Device);
        Assert.Equal(new[] { 1f, 1f, 1f, 1f }, new[] { conn.C08, conn.C0C, conn.C10, conn.C14 });
        Assert.Equal(0u, conn.Arg68);
        Assert.Equal(1, conn.Flags6C);                                     // bit0 only
        Assert.False(conn.FadeIn);
        Assert.Equal(4, voice.FlagsCD & 4);                                // 0xA4C318
        Assert.Null(voice.DryLineC);                                       // ip = 0, r3 = 0 (no chain walk)
        var line = Assert.Single(rig.Buses.Buses);
        Assert.Same(line, conn.Bus);
        Assert.Equal(1, line.Connections);                                 // +0x1C0
        Assert.Equal(1, rig.VoiceInits);                                   // row 4: 0xA54A30 once
    }

    [Fact]
    public void ABusWithBit6ClearGetsNoConnectionOnTheMainDevice()
    {
        // Row 6: connect only when (devId == (2,0)) == flag; bit6 = 0 -> non-main devices only.
        var rig = new Rig();
        rig.Routing = new WwisePbiRouting { Node = SoundUnder(new WwiseRoutingNode { Id = 9, IsBus = true, Bit6 = false }) };
        var voice = Rig.Voice();

        Assert.Equal(1, rig.Linker.Link(voice, Rig.Pbi()));
        Assert.Empty(voice.Connections);
        Assert.Empty(rig.Buses.Buses);
        Assert.Single(rig.Live);                                           // row 5.9: still inserted, returns 1
    }

    [Fact]
    public void ANonMainDeviceGetsTheConnectionForABit6ClearBus()
    {
        // Row 6 and C23.21: device (3,0) exists; a bus with bit6 = 0 connects to non-main devices only.
        var rig = new Rig();
        rig.Devices.Add(new WwiseDeviceId(3, 0), 1, 0x3102);
        rig.Routing = new WwisePbiRouting { Node = SoundUnder(new WwiseRoutingNode { Id = 9, IsBus = true, Bit6 = false }) };
        var voice = Rig.Voice();

        // Row 14: a parentless line on device lo 3 takes 0xA42754() as its parent, which creates the default
        // line on (2,0); the (2,0) device's mask is now 0xFE (C23.21) but the default line needs no listener.
        rig.Linker.Link(voice, Rig.Pbi());

        var conn = Assert.Single(voice.Connections);
        Assert.Equal(new WwiseDeviceId(3, 0), conn.Device);
        Assert.Equal(new WwiseDeviceId(3, 0), conn.Bus.Device);
        Assert.NotNull(conn.Bus.Parent);
        Assert.True(conn.Bus.Parent!.Context.Byte == 1);                   // the default context {0,-1,byte 1}
    }

    [Fact]
    public void AListenerMaskWithNoCommonBitMakesNoConnection()
    {
        // Row 5: proceed only if (listenerMask & [entry+0x18]) != 0.
        var rig = new Rig();
        rig.Routing = new WwisePbiRouting { Node = SoundUnder(Master()), ListenerMask = 0 };
        var voice = Rig.Voice();
        rig.Linker.Link(voice, Rig.Pbi());
        Assert.Empty(voice.Connections);
    }

    [Fact]
    public void ADeviceTheVoiceAlreadyHasAConnectionToIsSkipped()
    {
        // Row 5.4: an existing connection with the same id at +0x48/+0x4C skips the entry.
        var rig = new Rig();
        rig.Routing = new WwisePbiRouting { Node = SoundUnder(Master()) };
        var voice = Rig.Voice();
        var bus = new WwiseMixBus(default, Array.Empty<WwiseBusFxSlot>(), 8);
        voice.Connections.Add(new WwiseVoiceConnection(bus, 1, 1) { Device = WwiseDeviceId.Main });

        rig.Linker.Link(voice, Rig.Pbi());

        Assert.Single(voice.Connections);
        Assert.Empty(rig.Buses.Buses);
    }

    [Fact]
    public void ALaterConnectionAfterTheVoiceBit0IsClearGetsCtorBit2AndTheFadeIn()
    {
        // Row 17: conn+0x6C bit2 = !(voice+0xCD bit0); row 2.5 (M6-012): bit2 makes the fade-in.
        var rig = new Rig();
        rig.Routing = new WwisePbiRouting { Node = SoundUnder(Master()) };
        var voice = new WwiseLiveVoice(1, 8) { FlagsCD = 0 };
        voice.Source = null;

        rig.Linker.Link(voice, Rig.Pbi());                                 // bit0 clear: 0xA54A30 is skipped (0xA42E00)

        var conn = Assert.Single(voice.Connections);
        Assert.Equal(1 | 4, conn.Flags6C);
        Assert.True(conn.FadeIn);
        Assert.Equal(0, rig.VoiceInits);
    }

    [Fact]
    public void TheDefaultContextMatchesAnyNullBusLineOnTheSameDevice()
    {
        // Row 12 (corrected by the check): with both bus pointers null both the key and the key2 tests are
        // skipped, so a second no-bus voice reuses the first's line.
        var rig = new Rig();
        rig.Routing = new WwisePbiRouting { Node = NoBus() };              // vt+0x88 returns 0: flag = 1, default context
        var a = Rig.Voice();
        var b = Rig.Voice();

        rig.Linker.Link(a, Rig.Pbi());
        rig.Linker.Link(b, Rig.Pbi());

        var line = Assert.Single(rig.Buses.Buses);
        Assert.Equal((byte)1, line.Context.Byte);
        Assert.Equal(2, line.Connections);                                 // two voices' connections (+0x1C0)
        Assert.Same(line, a.Connections[0].Bus);
        Assert.Same(line, b.Connections[0].Bus);
    }

    [Fact]
    public void ParentLinesAreCreatedBeforeChildrenAndTheChildConnectsToTheParent()
    {
        // Row 13: parents are created before children up to the root; row 14: the new line is appended and a
        // non-null parent is connected via 0xA4F664 (row 18: state != 1 -> +0x90 = 0xA68A44 result, +0xC0 bit3
        // cleared, +0x1C0++).
        var rig = new Rig();
        var root = Master(100);
        var child = new WwiseRoutingNode { Id = 200, IsBus = true, OutputBus = root, Byte68 = 1, Bit6 = true };
        rig.Routing = new WwisePbiRouting { Node = SoundUnder(child) };
        var voice = Rig.Voice();

        rig.Linker.Link(voice, Rig.Pbi());

        Assert.Equal(2, rig.Buses.Buses.Count);
        var rootLine = rig.Buses.Buses[0];
        var childLine = rig.Buses.Buses[1];
        Assert.Same(root, rootLine.Context.Bus);
        Assert.Same(child, childLine.Context.Bus);
        Assert.Same(rootLine, childLine.Parent);
        Assert.Same(rootLine, childLine.OutputBus);                        // vpl+0x1C8
        Assert.Equal(1, rootLine.Connections);                             // the child line
        Assert.Equal(1, childLine.Connections);                            // the voice's connection
        Assert.Equal(-6.5f, rootLine.VolumeDb90);                          // 0xA68A44 for a non-null bus: 0x9C39DC(bus,0,5)
        Assert.Equal(0u, (uint)(rootLine.FlagsC0 & 8));
        // C24.3: root W = entry word 0x3102, B = 0, cfgA = cfgB = 0x3102. Child: W = parent.Format64 = 0x3102,
        // B = [bus+0x68] = 1 (low byte non-zero), cfgA = B = 1, cfgB = W = 0x3102 (parent present).
        Assert.Equal(new[] { (0x3102u, 0x3102u), (1u, 0x3102u) }, rig.Inits.Select(i => (i.CfgA, i.CfgB)).ToArray());
        Assert.Equal(new[] { 0x3102u, 1u }, new[] { rootLine.Format64, childLine.Format64 });
        Assert.Equal(new[] { 0x3102u, 0x3102u }, new[] { rootLine.Config44, childLine.Config44 });
    }

    [Fact]
    public void TheParentlessMasterLineUsesTheDeviceConfigWordAndAnUnknownDeviceMakesNoLine()
    {
        // C23.24: with parentVpl == 0 the device entry's +0x1C config word is the new pipeline's channel config
        // (0x3102); a missing device entry returns 0 (no line).
        var rig = new Rig();
        rig.Routing = new WwisePbiRouting { Node = SoundUnder(Master()) };
        rig.Linker.Link(Rig.Voice(), Rig.Pbi());
        Assert.Equal(new[] { (0x3102u, 0x3102u) }, rig.Inits.Select(i => (i.CfgA, i.CfgB)).ToArray());   // C24.3

        var none = new Rig(mainDevice: false);
        none.Devices.Add(new WwiseDeviceId(3, 0), 1, 0x3102);              // present, but the (2,0) gate never opens
        none.Routing = new WwisePbiRouting { Node = SoundUnder(Master()) };
        var v = Rig.Voice();
        none.Linker.Link(v, Rig.Pbi());
        Assert.Empty(v.Connections);
    }

    [Fact]
    public void AZeroConfigByteOnTheDeviceEntryReturnsNoLine()
    {
        // C23.24: entry+0x1C byte == 0 returns 0.
        var rig = new Rig(mainDevice: false);
        rig.Devices.Add(WwiseDeviceId.Main, 0xFF, 0);
        rig.Routing = new WwisePbiRouting { Node = SoundUnder(Master()) };
        var voice = Rig.Voice();
        rig.Linker.Link(voice, Rig.Pbi());
        Assert.Empty(voice.Connections);
        Assert.Empty(rig.Buses.Buses);
    }

    [Fact]
    public void TheDefaultLineMovesToTheFrontAndAdoptsTheFirstParentlessMainDeviceLine()
    {
        // Row 15 (corrected): 0xA42864..0xA428BC store the new default line at index 0; from index 1 the first
        // main-device line with state != 2 and +0x1C8 == 0 gets +0x1C8 = default and 0xA4F664, then return.
        var rig = new Rig();
        rig.Routing = new WwisePbiRouting { Node = SoundUnder(Master()) };
        rig.Linker.Link(Rig.Voice(), Rig.Pbi());
        var masterLine = Assert.Single(rig.Buses.Buses);

        rig.Routing = new WwisePbiRouting { Node = NoBus() };
        var voice = Rig.Voice();
        rig.Linker.Link(voice, Rig.Pbi());

        Assert.Equal(2, rig.Buses.Buses.Count);
        var defaultLine = rig.Buses.Buses[0];
        Assert.Equal((byte)1, defaultLine.Context.Byte);
        Assert.Same(masterLine, rig.Buses.Buses[1]);
        Assert.Same(defaultLine, masterLine.OutputBus);
        Assert.Equal(2, defaultLine.Connections);                          // masterLine + the voice
    }

    [Fact]
    public void TheChainWalkFindsTheFirstLineWithBit1AndStoresItInVoicePlusC()
    {
        // Row 16 (a)/(c): pbi+0xE9 bit3 walks vpl then +0x1C8 for +0x1CC bit1; found -> ip = 1 (conn+0x6C bit4)
        // and voice+0xC = that line (device (2,0), arg5 = 0).
        var rig = new Rig();
        // +0x1CC bit1 has no writer in the rows; the holder double (called at the end of 0xA4F0EC with the line) stands for it.
        rig.Seams.LineFxHolder = (line, _) => { line.Bit1OfFlags1CC = true; return true; };
        rig.Routing = new WwisePbiRouting { Node = SoundUnder(Master()), ChainWalk = true };
        var voice = Rig.Voice();

        rig.Linker.Link(voice, Rig.Pbi());

        var conn = Assert.Single(voice.Connections);
        Assert.Equal(1 | 0x10, conn.Flags6C);
        Assert.Same(conn.Bus, voice.DryLineC);
    }

    [Fact]
    public void ATableCheckFailureDestroysTheJustMadeConnection()
    {
        // Row 19 / C24.5: a line whose +0x64 type nibble is 1 runs the device-table check; a 0x9EA23C result other
        // than 1 unlinks and destroys the connection (voice+0xCD |= 4, voice+0xC = 0 when conn+0x68 == 0).
        var rig = new Rig();
        rig.Seams.LineFxHolder = (line, _) => { line.Bit1OfFlags1CC = true; return true; };
        rig.Seams.BuildDeviceObjectA22A3C = (_, _) => null;                // a zero build value: 0x9EA23C returns 2 (C24.5); non-1 destroys the connection
        rig.Routing = new WwisePbiRouting { Node = SoundUnder(Master()), ChainWalk = true };
        var voice = Rig.Voice();

        rig.Linker.Link(voice, Rig.Pbi());

        Assert.Empty(voice.Connections);
        Assert.Null(voice.DryLineC);
        Assert.Equal(0, Assert.Single(rig.Buses.Buses).Connections);
        Assert.Equal(4, voice.FlagsCD & 4);
    }

    [Fact]
    public void ATableCheckSuccessKeepsTheConnectionAndAMissingSeamIsRefused()
    {
        var rig = new Rig();
        rig.Routing = new WwisePbiRouting { Node = SoundUnder(Master()) };

        // 0x3102 has type nibble 1 (bits 8..11): the check is reached and its build body 0xA22A3C is not in the rows.
        rig.Seams.BuildDeviceObjectA22A3C = null;
        Assert.Throws<NotSupportedException>(() => rig.Linker.Link(Rig.Voice(), Rig.Pbi()));

        var rig2 = new Rig();
        rig2.Routing = rig.Routing;
        var voice = Rig.Voice();
        rig2.Linker.Link(voice, Rig.Pbi());
        Assert.Single(voice.Connections);
    }

    [Fact]
    public void TheTableCheckSearchesKey3ForTheStereoWordAndKeys4Then0ForTheMonoWord()
    {
        // C24.5: mask = cfg >> 12; key 1 = mask & ~8; if mask & 4, key 2 = mask & ~0xC. Shipped: 0x3102 (mask 3)
        // searches key 3 only; 0x4101 (mask 4) searches key 4 then 0.
        var stereo = new Rig();
        var keys = new List<uint>();
        stereo.Seams.BuildDeviceObjectA22A3C = (_, k) => { keys.Add(k); return new object(); };
        stereo.Routing = new WwisePbiRouting { Node = SoundUnder(Master()) };
        stereo.Linker.Link(Rig.Voice(), Rig.Pbi());
        Assert.Equal(new uint[] { 3 }, keys);

        var mono = new Rig();
        var keys2 = new List<uint>();
        mono.Seams.BuildDeviceObjectA22A3C = (_, k) => { keys2.Add(k); return new object(); };
        mono.Routing = new WwisePbiRouting { Node = SoundUnder(Master(100, 0x4101)) };
        var voice = Rig.Voice();
        mono.Linker.Link(voice, Rig.Pbi());
        Assert.Equal(new uint[] { 4, 0 }, keys2);
        Assert.Single(voice.Connections);
    }

    [Fact]
    public void AKeyFoundInTheDeviceTableIsSuccessAndNeverCallsTheFindOrInsertBody()
    {
        // C25.6: the row-19 caller scans the device table for key 1 (0xA4C3D8..0xA4C410) and calls 0x9EA23C only
        // when the key is absent; a found key is success with no rebuild. 0x4101: mask 4 -> key 1 = 4, key 2 = 0.
        var rig = new Rig();
        var asked = new List<uint>();
        rig.Seams.BuildDeviceObjectA22A3C = (_, k) => { asked.Add(k); return new object(); };
        var entry = rig.Devices.Find(WwiseDeviceId.Main)!;
        entry.Table.Add(new WwiseDeviceTableEntry { Key = 4, Built = new object() });
        entry.TableCapacity = 1;
        rig.Routing = new WwisePbiRouting { Node = SoundUnder(Master(100, 0x4101)) };

        var voice = Rig.Voice();
        rig.Linker.Link(voice, Rig.Pbi());

        Assert.Equal(new uint[] { 0 }, asked);                             // key 1 found; only key 2 (0) was absent
        Assert.Single(voice.Connections);

        // Both keys are present now ({4, 0}; the linker's own 0x9EA23C appended key 0): the body is never called and the connection stays.
        Assert.Equal(new uint[] { 4, 0 }, entry.Table.Select(t => t.Key).ToArray());
        asked.Clear();
        var voice2 = Rig.Voice();
        rig.Linker.Link(voice2, Rig.Pbi());
        Assert.Empty(asked);
        Assert.Single(voice2.Connections);
    }

    [Fact]
    public void AFailedKey1BuildDestroysTheConnectionWithoutScanningKey2()
    {
        // C25.6 / C24.5: key 2 is scanned only if key 1 succeeded and mask & 4. Key 1 absent and its find-or-insert
        // returning 2 destroys the connection; key 2 is never asked.
        var rig = new Rig();
        var asked = new List<uint>();
        rig.Seams.BuildDeviceObjectA22A3C = (_, k) => { asked.Add(k); return null; };
        rig.Routing = new WwisePbiRouting { Node = SoundUnder(Master(100, 0x4101)) };
        var voice = Rig.Voice();
        rig.Linker.Link(voice, Rig.Pbi());
        Assert.Equal(new uint[] { 4 }, asked);
        Assert.Empty(voice.Connections);
        Assert.Empty(rig.Devices.Find(WwiseDeviceId.Main)!.Table);        // 0x9EA2EC..0x9EA368: the zero-valued entry was removed again
    }

    [Fact]
    public void AMissingDeviceEntryAtTheTableCheckIsANullDereferenceNotASkip()
    {
        // C24.5: no null check on the device entry (0xA4C3CC ldr [r4,#0x5c]). The line exists for a device whose
        // entry is later gone: modelled by removing the entry the check looks up.
        var rig = new Rig();
        rig.Routing = new WwisePbiRouting { Node = SoundUnder(Master()) };
        var entries = (List<WwiseOutputDeviceEntry>)typeof(WwiseOutputDeviceList)
            .GetField("_entries", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(rig.Devices)!;
        rig.Seams.LineFxHolder = (_, _) =>
        {
            entries.Clear();                                               // the entry disappears before the check
            return true;
        };
        var ex = Assert.Throws<InvalidOperationException>(() => rig.Linker.Link(Rig.Voice(), Rig.Pbi()));
        Assert.Contains("C24.5", ex.Message);
    }

    // ------------------------------------------------------------------ config words (C24.3), C24.8

    private static (uint CfgA, uint CfgB)[] Words(Rig rig) => rig.Inits.Select(i => (i.CfgA, i.CfgB)).ToArray();

    [Fact]
    public void WithNoParentAndNoBusConfigBothWordsAreTheDeviceEntryWord()
    {
        // C24.3: W = [deviceEntry+0x1C] = 0x3102; B = 0 (Word68 unset); cfgA = (B & 0xFF) ? B : W = W; cfgB = parent ? W : cfgA = cfgA.
        var rig = new Rig();
        rig.Routing = new WwisePbiRouting { Node = SoundUnder(Master()) };
        rig.Linker.Link(Rig.Voice(), Rig.Pbi());
        Assert.Equal(new[] { (0x3102u, 0x3102u) }, Words(rig));
    }

    [Fact]
    public void WithNoParentASetBusConfigIsBothWordsAndThenTheLineFormatIsTheBusWord()
    {
        // C24.3: B = [bus+0x68] = 0x4101 (Cozmo_Robot and Robot_Bus_1..4, Init.bnk) has a non-zero low byte, so
        // cfgA = B and, with no parent, cfgB = cfgA. Init stores +0x64 = cfgA and +0x44 = cfgB.
        var rig = new Rig();
        rig.Routing = new WwisePbiRouting { Node = SoundUnder(Master(100, 0x4101)) };
        rig.Linker.Link(Rig.Voice(), Rig.Pbi());
        Assert.Equal(new[] { (0x4101u, 0x4101u) }, Words(rig));
        var line = Assert.Single(rig.Buses.Buses);
        Assert.Equal((0x4101u, 0x4101u), (line.Format64, line.Config44));
    }

    [Fact]
    public void ABusWordWhoseLowByteIsZeroFallsBackToTheDeviceWord()
    {
        // C24.3: cfgA = (B & 0xFF) != 0 ? B : W; B = 0x4100 has low byte 0.
        var rig = new Rig();
        rig.Routing = new WwisePbiRouting { Node = SoundUnder(Master(100, 0x4100)) };
        rig.Linker.Link(Rig.Voice(), Rig.Pbi());
        Assert.Equal(new[] { (0x3102u, 0x3102u) }, Words(rig));
    }

    [Fact]
    public void ADefaultLineFoundForDeviceLo2IsTheParentAndSuppliesW()
    {
        // C24.3/C24.8 (lo == 2): the parent found by the 0xA42640..0xA42718 search counts as the parent, so
        // W = parent.Format64 (0x3102), not the entry word (changed to 0x2102 below), and cfgB = W.
        var rig = new Rig();
        rig.Routing = new WwisePbiRouting { Node = NoBus() };
        rig.Linker.Link(Rig.Voice(), Rig.Pbi());                            // creates the default line, Format64 = 0x3102
        var defaultLine = Assert.Single(rig.Buses.Buses);
        Assert.Equal(0x3102u, defaultLine.Format64);
        rig.Devices.Find(WwiseDeviceId.Main)!.ConfigWord = 0x2102;

        rig.Inits.Clear();
        rig.Routing = new WwisePbiRouting { Node = SoundUnder(Master()) };
        rig.Linker.Link(Rig.Voice(), Rig.Pbi());

        Assert.Equal(new[] { (0x3102u, 0x3102u) }, Words(rig));
        var masterLine = rig.Buses.Buses.Single(l => l.Context.Bus is not null);
        Assert.Same(defaultLine, masterLine.OutputBus);                    // the parent was connected (0xA4F664)
    }

    [Fact]
    public void ADefaultLineFoundForDeviceLo2WithABusWordGivesCfgAFromBAndCfgBFromTheParent()
    {
        // C24.3 with both a parent and B: cfgA = B (0x4101); cfgB = parent ? W : cfgA = W = parent.Format64 (0x3102).
        var rig = new Rig();
        rig.Routing = new WwisePbiRouting { Node = NoBus() };
        rig.Linker.Link(Rig.Voice(), Rig.Pbi());
        rig.Devices.Find(WwiseDeviceId.Main)!.ConfigWord = 0x2102;

        rig.Inits.Clear();
        rig.Routing = new WwisePbiRouting { Node = SoundUnder(Master(100, 0x4101)) };
        rig.Linker.Link(Rig.Voice(), Rig.Pbi());

        Assert.Equal(new[] { (0x4101u, 0x3102u) }, Words(rig));
    }

    [Fact]
    public void TheDefaultLineFromA42754IsTheParentForDeviceLo3()
    {
        // C24.3/C24.8 (lo == 3): 0xA42754() supplies the parent, so W = parent.Format64 = 0x3102 (the default line
        // built from the (2,0) entry), not the (3,0) entry word 0x2103. B unset: both words are W.
        var rig = new Rig();
        rig.Devices.Add(new WwiseDeviceId(3, 0), 1, 0x2103);
        rig.Routing = new WwisePbiRouting { Node = SoundUnder(new WwiseRoutingNode { Id = 9, IsBus = true, Bit6 = false }) };
        rig.Linker.Link(Rig.Voice(), Rig.Pbi());
        Assert.Equal(new[] { (0x3102u, 0x3102u), (0x3102u, 0x3102u) }, Words(rig));   // default line, then (3,0)

        // With B = 0x4101 on the bus: cfgA = B and cfgB = W (parent present).
        var rig2 = new Rig();
        rig2.Devices.Add(new WwiseDeviceId(3, 0), 1, 0x2103);
        rig2.Routing = new WwisePbiRouting { Node = SoundUnder(new WwiseRoutingNode { Id = 9, IsBus = true, Bit6 = false, Word68 = 0x4101 }) };
        rig2.Linker.Link(Rig.Voice(), Rig.Pbi());
        Assert.Equal(new[] { (0x3102u, 0x3102u), (0x4101u, 0x3102u) }, Words(rig2));
    }

    [Fact]
    public void AParentlessDeviceWhoseLoIsNeither2Nor3KeepsParent0AndAppendsWithoutConnecting()
    {
        // C24.8: parentless, lo not 2 or 3 (0xA425A0 cmp r3,#3; bne 0xA4223C): parent stays 0, the line is built
        // from the device word and appended on Init success without 0xA4F664; no exception, no default line.
        var rig = new Rig();
        rig.Devices.Add(new WwiseDeviceId(5, 0), 1, 0x3102);
        rig.Routing = new WwisePbiRouting { Node = SoundUnder(new WwiseRoutingNode { Id = 9, IsBus = true, Bit6 = false }) };
        var voice = Rig.Voice();

        rig.Linker.Link(voice, Rig.Pbi());

        var line = Assert.Single(rig.Buses.Buses);
        Assert.Equal(new WwiseDeviceId(5, 0), line.Device);
        Assert.Null(line.OutputBus);
        Assert.Equal(new[] { (0x3102u, 0x3102u) }, Words(rig));
        Assert.Equal(1, line.Connections);                                 // only the voice's connection
        Assert.Single(voice.Connections);
    }

    [Theory]
    [InlineData("vt98", new[] { 1 })]
    [InlineData("buffer", new[] { 1, 2 })]
    [InlineData("holder", new[] { 1, 2, 3 })]
    public void ALineInitResultOtherThan1DestroysTheLineAndMakesNoConnection(string failure, int[] stagesReached)
    {
        // C24.3: 0xA4F0EC returns 2 (bus vt+0x98(3) == 0) or 0x34 (buffer allocation failure, FX holder allocation failure); non-1
        // destroys the line and 0xA42210 returns 0, so nothing is appended and the voice gets no connection. The stage lists are the
        // engine's own call order under Unicorn (re-analysis/tools/emu/emu_line_init.py): vt+0x98 == 0 returns after {0x9C8108,
        // 0x9C817C} (stage 1); a failed buffer allocation after {.., 0xA19ECC} (stage 2); a failed holder after {.., 0xA68A44,
        // 0xA68B28, 0xA68B38} (stage 3).
        var rig = new Rig();
        if (failure == "vt98") rig.Seams.BusVt98Arg3 = _ => 0;
        if (failure == "buffer") rig.Linker.AllocationFails = () => true;
        if (failure == "holder") rig.Seams.LineFxHolder = (_, _) => false;
        rig.Routing = new WwisePbiRouting { Node = SoundUnder(Master()) };
        var voice = Rig.Voice();
        Assert.Equal(1, rig.Linker.Link(voice, Rig.Pbi()));
        Assert.Empty(rig.Buses.Buses);
        Assert.Empty(voice.Connections);
        Assert.Equal(stagesReached, rig.Stages);
    }

    [Fact]
    public void TheLineInitStoresMatchTheEnginesOwnRunsUnderUnicorn()
    {
        // C24.3, emu_line_init.py (the engine's own 0xA4F0EC): master line cfgA = cfgB = 0x3102 -> [line+0x44] = 0x3102,
        // [line+0x64] = 0x3102, [line+0x6C] = 0x400, [line+0x30] = self, [line+0x28/2C] = (2, 0); the child line cfgA = 1, cfgB = 0x3102 stores
        // +0x64 = 1 and +0x44 = 0x3102; the buffer is (cfgA & 0xFF) * frames * 4 bytes (8192 and 4096 for 0x400 frames); the default
        // context (self null) makes no AddRef and no vt+0x98 call and keeps [line+0x30] = 0, and stage 2 (0xA19ECC) is not reached.
        var rig = new Rig();
        var root = Master(100);
        var child = new WwiseRoutingNode { Id = 200, IsBus = true, OutputBus = root, Byte68 = 1, Bit6 = true };
        rig.Routing = new WwisePbiRouting { Node = SoundUnder(child) };
        rig.Linker.Link(Rig.Voice(), Rig.Pbi());

        var rootLine = rig.Buses.Buses[0];
        var childLine = rig.Buses.Buses[1];
        Assert.Equal((0x3102u, 0x3102u, (ushort)0x400), (rootLine.Format64, rootLine.Config44, rootLine.InitFrames6C));
        Assert.Equal((1u, 0x3102u, (ushort)0x400), (childLine.Format64, childLine.Config44, childLine.InitFrames6C));
        Assert.Same(rootLine, rootLine.SelfBus30);                          // [line+0x30] is the line itself (0xA4F0FC; callers pass r0 = r1 = line)
        Assert.Same(childLine, childLine.SelfBus30);
        Assert.Same(root, rootLine.Context.Bus);                           // the bus is ctx.Bus, kept separate
        Assert.Equal(WwiseDeviceId.Main, rootLine.Device);
        Assert.Equal(2, rig.AddRefs);                                      // one vt+8 per non-null self
        Assert.Equal(new[] { 1, 2, 3, 1, 2, 3 }, rig.Stages);

        var def = new Rig();
        def.Routing = new WwisePbiRouting { Node = NoBus() };
        def.Linker.Link(Rig.Voice(), Rig.Pbi());
        var defaultLine = Assert.Single(def.Buses.Buses);
        Assert.Same(defaultLine, defaultLine.SelfBus30);                    // non-null even for the default ctx (null Bus)
        Assert.Null(defaultLine.Context.Bus);
        Assert.Equal(0, def.AddRefs);
        Assert.Equal(new[] { 1, 3 }, def.Stages);                          // no 0xA19ECC for a null self
        Assert.Equal(0xFFFFFFFFu, defaultLine.Context.Key);                // [line+0x48] = 0xA68A2C(default ctx) = -byte = 0xFFFFFFFF
    }

    [Fact]
    public void TheLineInitBufferAllocationFailureHookAndTheFramesWidthAreTheU16Global()
    {
        // C24.3: the frames argument is the u16 [0x1052440]; a frames value that does not fit would not exist in the engine. A zero frames
        // value makes a 0-byte buffer whose allocation result is unread: refused, not defaulted.
        var rig = new Rig();
        rig.Linker.LineMaxFrames = 0;
        rig.Routing = new WwisePbiRouting { Node = SoundUnder(Master()) };
        Assert.Throws<NotSupportedException>(() => rig.Linker.Link(Rig.Voice(), Rig.Pbi()));
    }

    [Fact]
    public void TheLineInitUnreadStepsAndTheBusSeamsAreRequired()
    {
        // 0xA4F0EC steps no row reads (C24.3 does not state them), the AddRef, vt+0x98(3) and the holder: each throws when unset.
        foreach (var unset in new Action<WwiseVoiceLinkSeams>[]
        {
            s => s.LineInitUnreadSteps = null, s => s.BusAddRefVt8 = null, s => s.BusVt98Arg3 = null, s => s.LineFxHolder = null,
        })
        {
            var rig = new Rig();
            unset(rig.Seams);
            rig.Routing = new WwisePbiRouting { Node = SoundUnder(Master()) };
            Assert.Throws<NotSupportedException>(() => rig.Linker.Link(Rig.Voice(), Rig.Pbi()));
        }
    }

    // ------------------------------------------------------------------ 0x9EA23C (C24.5, C25.6): oracle = emu_device_table.py

    private static WwiseOutputDeviceEntry TableEntry(IEnumerable<uint> keys, int capacity)
    {
        var list = new WwiseOutputDeviceList();
        var e = list.CreateMainDevice();
        foreach (var k in keys) e.Table.Add(new WwiseDeviceTableEntry { Key = k, Built = new object() });
        e.TableCapacity = capacity;
        return e;
    }

    [Theory]
    // (label, keys before, capacity before, key, build yields a value, allocation fails) -> (result, keys after, capacity after, build calls, allocations)
    // Expected values: the engine's own 0x9EA23C under Unicorn, re-analysis/tools/emu/emu_device_table.py.
    [InlineData(new uint[0], 0, 3u, true, false, 1, new uint[] { 3 }, 1, 1, 1)]          // miss: capacity 0 -> 1, appended, built
    [InlineData(new uint[0], 0, 3u, true, true, 2, new uint[0], 0, 0, 1)]                // miss, growth allocation fails: table unchanged, no build
    [InlineData(new uint[0], 0, 3u, false, false, 2, new uint[0], 1, 1, 1)]              // miss, zero build: appended then removed, capacity stays 1
    [InlineData(new uint[] { 4 }, 1, 0u, true, false, 1, new uint[] { 4, 0 }, 2, 1, 1)]  // miss at capacity: grows 1 -> 2
    [InlineData(new uint[] { 4 }, 2, 0u, true, false, 1, new uint[] { 4, 0 }, 2, 1, 0)]  // miss with room: no allocation
    [InlineData(new uint[] { 4 }, 1, 0u, true, true, 2, new uint[] { 4 }, 1, 0, 1)]      // miss, growth fails: {4} untouched, no build
    [InlineData(new uint[] { 4 }, 1, 4u, true, false, 1, new uint[] { 4 }, 1, 1, 0)]     // hit: rebuilt in place, no allocation
    [InlineData(new uint[] { 4, 0 }, 2, 4u, false, false, 2, new uint[] { 0 }, 2, 1, 0)] // hit, zero build: the existing entry is removed
    [InlineData(new uint[] { 4, 0 }, 3, 9u, false, false, 2, new uint[] { 4, 0 }, 3, 1, 0)] // miss, zero build, others stay in order
    [InlineData(new uint[] { 4, 0 }, 2, 0u, false, false, 2, new uint[] { 4 }, 2, 1, 0)] // hit on the second entry, zero build: {4} remains
    public void The9EA23CScanAppendGrowAndRemoveMatchTheEnginesOwnRunUnderUnicorn(
        uint[] keysBefore, int capacityBefore, uint key, bool buildYields, bool allocFails, int expectedResult,
        uint[] expectedKeys, int expectedCapacity, int expectedBuilds, int expectedAllocs)
    {
        // C24.5, C25.6 and 0x9EA23C itself. The two trailing expectations are (build calls, growth allocations); the data columns above
        // are from emu_device_table.py. The same hook as the limiter's AllocationFails is used for the growth allocation.
        var rig = new Rig();
        int builds = 0, allocs = 0;
        rig.Seams.BuildDeviceObjectA22A3C = (_, _) => { builds++; return buildYields ? new object() : null; };
        rig.Linker.AllocationFails = () => { allocs++; return allocFails; };
        var e = TableEntry(keysBefore, capacityBefore);

        int result = rig.Linker.FindOrInsert9EA23C(e, key);

        Assert.Equal(expectedResult, result);
        Assert.Equal(expectedKeys, e.Table.Select(t => t.Key).ToArray());
        Assert.Equal(expectedCapacity, e.TableCapacity);
        Assert.Equal(expectedBuilds, builds);
        Assert.Equal(expectedAllocs, allocs);
    }

    [Fact]
    public void TheLineInitReceivesTheFramesAndTheFlagIsStoredAtBit3()
    {
        // C24.3: u16 frames arg = [0x1052440], .data initial 0x400 (C24 residuals); flag stored at +0x1CC bit3
        // (0xA422DC), 0 at every call site.
        var rig = new Rig();
        rig.Routing = new WwisePbiRouting { Node = SoundUnder(Master()) };
        rig.Linker.Link(Rig.Voice(), Rig.Pbi());
        Assert.Equal(0x400, Assert.Single(rig.Inits).Frames);
        Assert.False(Assert.Single(rig.Buses.Buses).Bit3OfFlags1CC);
    }

    // ------------------------------------------------------------------ 0xA68A44, 0xA4F6F0 (C24.6)

    [Fact]
    public void ANullBusLineTakesVolume0WithoutTheSeamAndABusLineTakesTheParam5Read()
    {
        // C24.6: 0xA68A44 returns 0.0f for a null bus; only a non-null bus goes to 0x9C39DC(bus, 0, 5).
        var rig = new Rig();
        rig.Seams.BusVolumeParam5 = _ => throw new InvalidOperationException("must not be read for a null bus");
        rig.Routing = new WwisePbiRouting { Node = NoBus() };
        var voice = Rig.Voice();
        rig.Linker.Link(voice, Rig.Pbi());
        Assert.Equal(0f, Assert.Single(rig.Buses.Buses).VolumeDb90);

        var bus = new Rig();
        WwiseRoutingNode? asked = null;
        bus.Seams.BusVolumeParam5 = n => { asked = n; return -12f; };
        var master = Master();
        bus.Routing = new WwisePbiRouting { Node = SoundUnder(master) };
        bus.Linker.Link(Rig.Voice(), Rig.Pbi());
        Assert.Same(master, asked);
        Assert.Equal(-12f, Assert.Single(bus.Buses.Buses).VolumeDb90);
    }

    [Fact]
    public void ADefaultLineAlwaysTakesTheNullBusVolumePath()
    {
        // C24.6: the default context has no bus, so 0xA68A44 returns 0.0f and the seam is not needed.
        var rig = new Rig();
        rig.Seams.BusVolumeParam5 = null;
        rig.Routing = new WwisePbiRouting { Node = NoBus() };
        rig.Linker.Link(Rig.Voice(), Rig.Pbi());
        Assert.Equal(0f, Assert.Single(rig.Buses.Buses).VolumeDb90);
    }

    [Fact]
    public void TheDisconnectDecrementsTheCountAndCallsTheMixObjectOnlyWithANonNullPlus0xC()
    {
        // C24.6 0xA4F6F0: vpl+0x1C0-- and, when [vpl+0x1A8] and [[vpl+0x1A8]+0xC] are non-null,
        // mixobj->vt+0x24(conn). Covered here through the voice teardown (0xA55F2C).
        var rig = new Rig();
        rig.Routing = new WwisePbiRouting { Node = SoundUnder(Master()) };
        var removed = new List<object>();
        rig.Seams.MixObjectRemoveInput = (_, c) => removed.Add(c);
        rig.Seams.CloseSource56414 = _ => { };
        var voice = Rig.Voice();
        rig.Linker.Link(voice, Rig.Pbi());
        var conn = voice.Connections[0];
        var line = conn.Bus;

        rig.Linker.TeardownVoice(voice);                                   // +0x1A8 unset: no mix-object call
        Assert.Equal(0, line.Connections);
        Assert.Empty(removed);

        var voice2 = Rig.Voice();
        rig.Linker.Link(voice2, Rig.Pbi());                                // reuses the line (row 12)
        var conn2 = voice2.Connections[0];
        Assert.Same(line, conn2.Bus);
        line.OutputMixObject1A8 = () => { };                               // +0x1A8 set but [+0xC] null: still no call
        rig.Linker.TeardownVoice(voice2);
        Assert.Empty(removed);

        rig.Seams.MixObjectAddInput = (_, _) => { };                       // the add side (0xA4F664) is reached now
        var voice3 = Rig.Voice();
        rig.Linker.Link(voice3, Rig.Pbi());
        var conn3 = voice3.Connections[0];
        line.MixObject1A8C = new object();                                 // [[+0x1A8]+0xC] non-null
        rig.Linker.TeardownVoice(voice3);
        Assert.Equal(new object[] { conn3 }, removed);
        Assert.Equal(0, line.Connections);
    }

    [Fact]
    public void TheMixObjectRemoveSeamIsRequiredWhenTheDisconnectReachesIt()
    {
        var rig = new Rig();
        rig.Seams.BuildDeviceObjectA22A3C = (_, _) => null;                // the row-19 destroy path also disconnects
        rig.Seams.LineFxHolder = (line, _) => { line.OutputMixObject1A8 = () => { }; line.MixObject1A8C = new object(); return true; };
        rig.Seams.MixObjectAddInput = (_, _) => { };
        rig.Routing = new WwisePbiRouting { Node = SoundUnder(Master()) };
        Assert.Throws<NotSupportedException>(() => rig.Linker.Link(Rig.Voice(), Rig.Pbi()));
    }

    // ------------------------------------------------------------------ null [pbi+0xE0] (C24.9)

    [Fact]
    public void ANullPbiNodeIsANullDereferenceNotNoBus()
    {
        // C24.9: 0xA42FA0 ldr r0,[sl,#0xe0]; 0xA42FA4 ldr r3,[r0] dereferences it. The no-bus case is vt+0x88
        // returning 0 (NoBus()), which links with no connection when flag = 1 has no device to serve.
        var rig = new Rig();
        rig.Routing = new WwisePbiRouting { Node = null! };
        Assert.Throws<InvalidOperationException>(() => rig.Linker.Link(Rig.Voice(), Rig.Pbi()));
    }

    [Fact]
    public void ThePbiNodeIsDereferencedOnlyAfterADevicePassesTheListenerMask()
    {
        // C25.7: the device loop of 0xA42DEC dereferences [pbi+0xE0] only after a device passes the listener mask
        // (0xA42FA0). With listener mask 0 no device passes, so a null node is never touched; with no device at all
        // the loop does not run either.
        var rig = new Rig();
        rig.Routing = new WwisePbiRouting { Node = null!, ListenerMask = 0 };
        var voice = Rig.Voice();
        Assert.Equal(1, rig.Linker.Link(voice, Rig.Pbi()));
        Assert.Empty(voice.Connections);

        var noDevice = new Rig(mainDevice: false);
        noDevice.Routing = new WwisePbiRouting { Node = null! };
        Assert.Equal(1, noDevice.Linker.Link(Rig.Voice(), Rig.Pbi()));
    }

    // ------------------------------------------------------------------ FadeIn is the live bit2 (C24.4, M6-012 2.5)

    [Fact]
    public void FadeInIsTheLiveConnectionBit2SoTheFrameRewriteChangesWhatRefreshUses()
    {
        // 0xA4C0C0 tst r3,#4 reads the live [conn+0x6C] bit2; 0xA4C584..0xA4C598 rewrites it (SetConnectionBit2) and
        // 0xA4BD54 (SetBit2). First Refresh: start = 0 with bit2 (M6-012 gapE 2.5, 0xA4C0D8..0xA4C104), else start = end.
        var bus = new WwiseMixBus(default, Array.Empty<WwiseBusFxSlot>(), 8);

        var set = new WwiseVoiceConnection(bus, 1, 1) { Flags6C = 1, TargetGain = 0.5f };
        var v1 = new WwiseLiveVoice(1, 8);
        v1.Connections.Add(set);
        Assert.False(set.FadeIn);
        WwiseVoiceBusPass.SetConnectionBit2(v1, true);
        Assert.True(set.FadeIn);
        set.Refresh();
        Assert.Equal(0f, set.Mixer.StartGain);
        Assert.Equal(0.5f, set.Mixer.EndGain);

        var cleared = new WwiseVoiceConnection(bus, 1, 1) { Flags6C = 1 | 4, TargetGain = 0.5f };
        var v2 = new WwiseLiveVoice(1, 8);
        v2.Connections.Add(cleared);
        Assert.True(cleared.FadeIn);
        WwiseVoiceBusPass.SetConnectionBit2(v2, false);
        Assert.False(cleared.FadeIn);
        cleared.Refresh();
        Assert.Equal(0.5f, cleared.Mixer.StartGain);                        // no ramp: start = end
    }

    [Fact]
    public void TheFrameStateMachineSetBit2AlsoChangesWhatRefreshUses()
    {
        // 0xA4BD54 SetBit2 (UpdateConnectionGains, voice->vt+0x3C != 0 and [voice+0xCD] bit3 clear): bit2 = vt3c & 1.
        var bus = new WwiseMixBus(default, Array.Empty<WwiseBusFxSlot>(), 8);
        var conn = new WwiseVoiceConnection(bus, 1, 1) { Flags6C = 1, TargetGain = 0.25f };
        var voice = new WwiseLiveVoice(1, 8) { VoiceRequest3C = () => 1 };
        voice.Connections.Add(conn);

        WwiseVoiceBusPass.UpdateConnectionGains(voice, bus, 1f);

        Assert.Equal(4, conn.Flags6C & 4);
        Assert.True(conn.FadeIn);
        conn.Refresh();
        Assert.Equal(0f, conn.Mixer.StartGain);
    }

    // ------------------------------------------------------------------ voice+0xF0 word (C25.5)

    [Fact]
    public void TheLinkerMakesNoStoreOnBehalfOfTheVoiceInitBody_C30_8()
    {
        // C30.8 (manager decision 2026-10-02): 0xA54A30 stays RECOVERABLE_GAP until C24.2 is verified, so Link calls the named seam and
        // stores nothing for it: [voice+0xF0] (C25.5, 0xA54A64/0xA54B70) is the seam's. The voice ctor leaves it 0 and Link keeps it 0.
        var rig = new Rig();
        var voice = Rig.Voice();
        Assert.Equal(0u, voice.Word0xF0);
        var pbi = Rig.Pbi();
        Assert.Equal(0x00004101u, pbi.Word15C);                            // C23.8: the ctor default, only until the source writes it
        rig.Linker.Link(voice, pbi);
        Assert.Equal(1, rig.VoiceInits);                                   // the init seam ran once
        Assert.Equal(0u, voice.Word0xF0);                                  // nothing was stored for it

        // The seam owns the store: a seam that stores the word is what the voice pass then reads.
        var rig2 = new Rig();
        rig2.Seams.InitVoiceA54A30 = v => { v.Word0xF0 = 0x00003102; return 1; };
        var voice2 = Rig.Voice();
        rig2.Linker.Link(voice2, Rig.Pbi());
        Assert.Equal(0x00003102u, voice2.Word0xF0);

        // A voice whose +0xCD bit0 is clear does not run the init at all.
        var noInit = new WwiseLiveVoice(1, 8) { FlagsCD = 0 };
        rig.Linker.Link(noInit, Rig.Pbi());
        Assert.Equal(1, rig.VoiceInits);
        Assert.Equal(0u, noInit.Word0xF0);
    }

    [Theory]
    [InlineData(0x00004101u, 0x3102u, 32)]   // inCh 1, outCh 2: ((2+3)>>2) * (1<<5) = 32
    [InlineData(0x00003102u, 0x3102u, 64)]   // inCh 2, outCh 2: 1 * (2<<5) = 64
    [InlineData(0x00004101u, 0x4101u, 32)]   // inCh 1, outCh 1: 1 * 32
    [InlineData(0x00000501u, 0x3108u, 64)]   // inCh 1, outCh 8: ((8+3)>>2) = 2 -> 64 (high bytes of the word are ignored)
    public void TheDescriptorInChIsTheLowByteOfTheVoiceWordNotAnId(uint word, uint lineWord, int expectedSize)
    {
        // C24.4 / C25.5: 0xA67B9C size = ((outCh+3)>>2) * (inCh<<5); inCh = low byte of [voice+0xF0], outCh = low byte
        // of [[conn+0x30]+0x64].
        var bus = new WwiseMixBus(default, Array.Empty<WwiseBusFxSlot>(), 8) { Format64 = lineWord };
        var conn = new WwiseVoiceConnection(bus, 1, 1) { Flags6C = 0 };
        var voice = new WwiseLiveVoice(1, 8) { Word0xF0 = word, FlagsCD = 8, VoiceRequest3C = () => 1 };   // vt3c != 0: main loop
        voice.Connections.Add(conn);

        WwiseVoiceBusPass.UpdateConnectionGains(voice, bus, 1f);

        Assert.Equal(expectedSize, conn.Descriptor.Size);
        Assert.Equal((int)(word & 0xFF), conn.C64);                        // [conn+0x64] = inCh once [conn+0x18] != 0
    }

    [Fact]
    public void AVoiceWhoseWord0xF0LowByteIsZeroRunsNoConnectionLoop()
    {
        // 0xA4BD74 cmp r6,#0; beq 0xA4BFD4: inCh 0 skips the loop, so no descriptor is sized.
        var bus = new WwiseMixBus(default, Array.Empty<WwiseBusFxSlot>(), 8) { Format64 = 0x3102 };
        var conn = new WwiseVoiceConnection(bus, 1, 1) { Flags6C = 0 };
        var voice = new WwiseLiveVoice(1, 8) { Word0xF0 = 0x4100, FlagsCD = 8, VoiceRequest3C = () => 1 };
        voice.Connections.Add(conn);
        WwiseVoiceBusPass.UpdateConnectionGains(voice, bus, 1f);
        Assert.False(conn.HasDry);
    }

    // ------------------------------------------------------------------ connection descriptor (C24.4)

    [Fact]
    public void TheDescriptorIsSizedByTheChannelCountsAndGatesTheWalks()
    {
        // C24.4 0xA67B9C: size = ((outCh+3)>>2) * (inCh<<5); returns 1 at once when equal to [desc+4]; else frees and
        // reallocates {data, size, data, data+size/2}. [conn+0x18] != 0 is HasDry.
        var bus = new WwiseMixBus(default, Array.Empty<WwiseBusFxSlot>(), 8);
        var conn = new WwiseVoiceConnection(bus, 1, 1);
        Assert.False(conn.HasDry);                                          // the ctor zeroes the descriptor (0xA6F998..0xA6F9A4)
        Assert.Equal(1, conn.Descriptor.Reserve(2, 1));                     // ((1+3)>>2) * (2<<5) = 64
        Assert.True(conn.HasDry);
        Assert.Equal(64, conn.Descriptor.Size);
        Assert.Equal((0, 32), (conn.Descriptor.PtrA, conn.Descriptor.PtrB));
        var data = conn.Descriptor.Data;
        Assert.Equal(1, conn.Descriptor.Reserve(2, 1));                     // equal: the block is kept
        Assert.Same(data, conn.Descriptor.Data);
        Assert.Equal(1, conn.Descriptor.Reserve(1, 8));                     // ((8+3)>>2) * 32 = 64: equal again
        Assert.Same(data, conn.Descriptor.Data);
        conn.Descriptor.Reserve(1, 9);                                      // ((9+3)>>2) * 32 = 96: reallocated
        Assert.Equal(96, conn.Descriptor.Size);
        Assert.NotSame(data, conn.Descriptor.Data);
        conn.Descriptor.SwapPointers();
        Assert.Equal((48, 0), (conn.Descriptor.PtrA, conn.Descriptor.PtrB));
    }

    [Theory]
    // (prior size, in, out, allocation fails) -> (result, Size, Data allocated, ptrA, ptrB, allocation attempts)
    // Expected values: the engine's own 0xA67B9C under Unicorn, re-analysis/tools/emu/emu_descriptor_reserve.py.
    [InlineData(0, 2, 1, false, 1, 64, true, 0, 32, 1)]    // fresh, size ((1+3)>>2)*(2<<5) = 64: {data, 0x40, data, data+0x20}
    [InlineData(0, 2, 1, true, 2, 0, false, 0, 0, 1)]      // fresh, allocation fails: all four words zero, result 2
    [InlineData(64, 2, 1, false, 1, 64, true, 0, 32, 0)]   // same size: 1 at once, no allocation
    [InlineData(64, 1, 9, true, 2, 0, false, 0, 0, 1)]     // different size, allocation fails: old block freed, words zeroed, result 2
    [InlineData(64, 1, 9, false, 1, 96, true, 0, 48, 1)]   // different size, allocation ok: reallocated to 96
    [InlineData(0, 0, 5, true, 1, 0, false, 0, 0, 0)]      // size 0 equals the zero descriptor: 1 at once, nothing allocated
    public void TheDescriptorReserveReturns2OnAnAllocationFailureLikeTheEnginesOwnRun(
        int priorSize, int inCh, int outCh, bool allocFails, int expectedResult, int expectedSize, bool expectedData, int expectedA, int expectedB, int attempts)
    {
        // C24.4: 0xA67B9C returns 2 when the allocation fails (0xA67BE8 cmp r0,#0; 0xA67BF0 beq 0xA67C40 mov r0,#2). The failure hook is the same
        // Func<bool> the limiter and the start list use.
        var bus = new WwiseMixBus(default, Array.Empty<WwiseBusFxSlot>(), 8);
        var conn = new WwiseVoiceConnection(bus, 1, 1);
        if (priorSize == 64) conn.Descriptor.Reserve(2, 1);               // a prior block of 64 bytes
        int seen = 0;
        conn.Descriptor.AllocationFails = () => { seen++; return allocFails; };

        Assert.Equal(expectedResult, conn.Descriptor.Reserve(inCh, outCh));

        Assert.Equal(expectedSize, conn.Descriptor.Size);
        Assert.Equal(expectedData, conn.Descriptor.IsAllocated);
        Assert.Equal((expectedA, expectedB), (conn.Descriptor.PtrA, conn.Descriptor.PtrB));
        Assert.Equal(attempts, seen);
        Assert.Equal(expectedData, conn.HasDry);                          // [conn+0x18] != 0 only with a block
    }

    [Fact]
    public void AConnectionWhoseReserveFailsIsSkippedByTheFramePassThroughTheLiveEntry()
    {
        // Through WwiseVoiceBusPass.UpdateConnectionGains (0xA4BC58): a connection whose 0xA67B9C returned 2 stays with [conn+0x18] == 0 and the loop
        // moves to the next connection (0xA4BE10 beq 0xA4C16C not taken, 0xA4BE14 ldr fp,[fp,#0x28]), so it gets no gain store.
        var bus = new WwiseMixBus(default, Array.Empty<WwiseBusFxSlot>(), 8) { Format64 = 0x3102 };
        var failing = new WwiseVoiceConnection(bus, 1, 1) { Flags6C = 0 };
        failing.Descriptor.AllocationFails = () => true;
        var ok = new WwiseVoiceConnection(bus, 1, 1) { Flags6C = 0 };
        var voice = new WwiseLiveVoice(1, 8) { Word0xF0 = 0x4101, FlagsCD = 8, VoiceRequest3C = () => 1, OutputGain = 1f };
        voice.Connections.Add(failing);
        voice.Connections.Add(ok);

        WwiseVoiceBusPass.UpdateConnectionGains(voice, bus, 0.5f);

        Assert.False(failing.HasDry);
        Assert.Equal(0f, failing.C0C);                                    // the gain store 0xA4BE6C was not reached
        Assert.True(ok.HasDry);
        Assert.Equal(0.5f, ok.C0C);                                       // [conn+0xC] = [voice+0x1C] * gain
    }

    [Fact]
    public void TheAuxAndDryWalksNeedTheDescriptorAndTheirConn68Tests()
    {
        // C24.4: aux walk needs [conn+0x68] != 0, [conn+0x18] != 0, ([conn+0x6C] & 6) != 6; the dry walk needs
        // [conn+0x68] == 0 and the same two tests.
        WwiseVoiceConnection Make(bool aux, bool descriptor, byte flags)
        {
            var c = new WwiseVoiceConnection(new WwiseMixBus(default, Array.Empty<WwiseBusFxSlot>(), 8), 1, 1)
            { HasAux = aux, Flags6C = flags, TargetGain = 1f };
            if (descriptor) c.Descriptor.Reserve(1, 1);
            return c;
        }

        int Mixed(params WwiseVoiceConnection[] conns)
        {
            var v = new WwiseLiveVoice(1, 8) { Source = new Src() };
            v.Connections.AddRange(conns);
            v.Render();
            return conns.Count(c => c.Bus.State == WwiseMixBus.StateActive);
        }

        Assert.Equal(1, Mixed(Make(aux: false, descriptor: true, flags: 1)));      // dry: conn+0x68 == 0, descriptor
        Assert.Equal(0, Mixed(Make(aux: false, descriptor: false, flags: 1)));     // no [conn+0x18]
        Assert.Equal(0, Mixed(Make(aux: false, descriptor: true, flags: 7)));      // (flags & 6) == 6
        Assert.Equal(1, Mixed(Make(aux: true, descriptor: true, flags: 1)));       // aux: conn+0x68 != 0
        Assert.Equal(0, Mixed(Make(aux: true, descriptor: false, flags: 1)));
        Assert.Equal(0, Mixed(Make(aux: true, descriptor: true, flags: 6)));
    }

    // ------------------------------------------------------------------ live list (row 20 / 5.9 / 5.10) and failure

    [Fact]
    public void NewVoicesAreInsertedAtTheHeadOfTheLiveList()
    {
        // Row 20 / 5.10: every source key is 0 (0xA56708 mov r0,#0); insert before the first existing voice whose
        // key >= the new key: newest first.
        var rig = new Rig(mainDevice: false);
        var v1 = Rig.Voice(); v1.Source = new Src();
        var v2 = Rig.Voice(); v2.Source = new Src();
        var v3 = Rig.Voice(); v3.Source = new Src { OrderKey6C = 5 };

        rig.Linker.Link(v1, Rig.Pbi());
        rig.Linker.Link(v2, Rig.Pbi());
        Assert.Equal(new[] { v2, v1 }, rig.Live);

        rig.Linker.Link(v3, Rig.Pbi());                                    // key 5 > 0 walks on past both (0xA42F10 bgt)
        Assert.Equal(new[] { v2, v1, v3 }, rig.Live);
    }

    [Fact]
    public void ANegativeSourceKeyClampsToZero()
    {
        // C24.10: the key starts at 0 for both the new and the existing voice (0xA42EA4 mov r6,#0, 0xA42EDC mov
        // fp,#0), so a negative vt+0x6C clamps to 0: a voice with key -5 ties with key 0 and goes before it.
        var rig = new Rig(mainDevice: false);
        var v1 = Rig.Voice(); v1.Source = new Src { OrderKey6C = -5 };
        var v2 = Rig.Voice(); v2.Source = new Src();
        rig.Linker.Link(v1, Rig.Pbi());
        rig.Linker.Link(v2, Rig.Pbi());
        Assert.Equal(new[] { v2, v1 }, rig.Live);                          // key 0 >= key 0: inserted before, not after
    }

    [Fact]
    public void AVoiceInitFailureTearsTheVoiceDownAndReturns2()
    {
        // Row 4 / 5.1: 0xA54A30 not 1 -> 0x9D40C4(voice,1) and return 2, before any connection or list insert.
        var rig = new Rig();
        rig.Seams.InitVoiceA54A30 = _ => 0;
        rig.Seams.CloseSource56414 = _ => { };
        var voice = Rig.Voice();
        voice.Source = new Src();

        Assert.Equal(2, rig.Linker.Link(voice, Rig.Pbi()));

        Assert.Empty(rig.Live);
        Assert.Empty(voice.Connections);
        Assert.Null(voice.Source);
    }

    [Fact]
    public void AnUnreadCalleeIsRefusedNotDefaulted()
    {
        // The body of 0xA54A30 is RECOVERABLE_GAP (row 4): with no seam the path throws.
        var rig = new Rig();
        rig.Seams.InitVoiceA54A30 = null;
        Assert.Throws<NotSupportedException>(() => rig.Linker.Link(Rig.Voice(), Rig.Pbi()));

        var rig2 = new Rig();
        rig2.Seams.LineInitUnreadSteps = null;
        rig2.Routing = new WwisePbiRouting { Node = SoundUnder(Master()) };
        Assert.Throws<NotSupportedException>(() => rig2.Linker.Link(Rig.Voice(), Rig.Pbi()));
    }

    // ------------------------------------------------------------------ pending list (row 3, C23.1, 5.12, 5.13)

    private static (Rig rig, WwiseLiveVoice voice, WwisePlayingInstance pbi) PendingRig(Src source)
    {
        var rig = new Rig(mainDevice: false);
        var voice = Rig.Voice();
        voice.Source = source;
        rig.Linker.PendingVoices.Add(voice);
        var pbi = Rig.Pbi();
        rig.Owners[source] = pbi;                                          // [source+0xC] = the owner PBI
        rig.Seams.SourceOwner = s => rig.Owners.GetValueOrDefault(s);
        voice.BusOwner8 = pbi;                                             // [voice+8] = pbi+0xC (AddSrc, C25.4)
        pbi.NodeE0 = new WwiseActorMixerNode(0xAABBCCDD, "t.bnk",
            new WwiseNodeParams(0, 0, 0, new Dictionary<byte, uint>(), new Dictionary<byte, (float, float)>(), Array.Empty<WwiseRtpc>(),
                Array.Empty<(uint, byte, IReadOnlyList<(uint, uint)>)>()), Array.Empty<uint>());   // [[pbi+0xE0]+8] = 0xAABBCCDD
        return (rig, voice, pbi);
    }

    // All expected values in the NotReady tests are the engine's own 0xA544BC run under Unicorn
    // (re-analysis/tools/emu/emu_notready.py), not the C#'s output.

    [Fact]
    public void ResultThreeFThenANonNegativeOffsetStaysPendingWithoutSideEffects()
    {
        // 0xA544BC under Unicorn: vt+0x28 returns 0x3F, [pbi+0x1D8] = 5 -> 0x3F, voice+0xE8 untouched, no 0xA0428C.
        var (rig, voice, pbi) = PendingRig(new Src { Code = 0x3F });
        pbi.StartOffset = 5;
        Assert.Equal(0x3F, rig.Linker.ProcessPending(pbi, voice));
        Assert.Contains(voice, rig.Linker.PendingVoices);
        Assert.False(voice.FlagE8);
        Assert.Empty(rig.Live);
        Assert.False(((Src)voice.Source!).StartStreamSucceeded);           // 0xA56650 sets the latch only for a result of exactly 1
        // vt+0x28 got ([owner+0x1DC], [owner+0x1E0]) = (0x11112222, 0x33334444) in the engine's run.
        Assert.Equal(new[] { (0x11112222u, 0x33334444u) }, ((Src)voice.Source!).Calls);
    }

    [Fact]
    public void ResultThreeFThenANegativeOffsetRuns0xA54580WithThePlayingIdAndTheNodeIdAndStillReturns3F()
    {
        // 0xA544BC under Unicorn: result 0x3F with [pbi+0x1D8] = -1 -> voice+0xE8 = 1, one 0xA0428C(mgr, 0xC0FFEE, 0xAABBCCDD) =
        // ([pbi'+0x134] = the playing id, 0x9BD138(pbi') = the node id), result 0x3F.
        var (rig, voice, pbi) = PendingRig(new Src { Code = 0x3F });
        pbi.StartOffset = 0xFFFFFFFF;
        var playingId = new WwisePlayingInstance(
            new WwisePlayInitParams { PlayingId = 0x00C0FFEE, TargetNodeId = 1 }, 1, new object(), new byte[0x44], null, false)
        { Word1DC = Rig.Media1DC, Word1E0 = Rig.Media1E0, NodeE0 = pbi.NodeE0, StartOffset = 0xFFFFFFFF };
        rig.Owners[voice.Source!] = playingId;
        voice.BusOwner8 = playingId;
        var calls = new List<(uint, uint)>();
        rig.Seams.SourceFlag10Bit1 = _ => false;
        rig.Seams.A0428C = (id, node) => calls.Add((id, node));

        Assert.Equal(0x3F, rig.Linker.ProcessPending(playingId, voice));
        Assert.True(voice.FlagE8);
        Assert.Equal(new[] { (0x00C0FFEEu, 0xAABBCCDDu) }, calls);
        Assert.Contains(voice, rig.Linker.PendingVoices);
    }

    [Fact]
    public void ASourceBit1SetSuppressesTheSideEffects()
    {
        // 0xA544BC under Unicorn: [source+0x10] bit1 set with [pbi+0x1D8] = -1: voice+0xE8 stays 0, no 0xA0428C, result as without it.
        var (rig, voice, pbi) = PendingRig(new Src { Code = 0x3F });
        pbi.StartOffset = 0xFFFFFFFF;
        rig.Seams.SourceFlag10Bit1 = _ => true;
        rig.Seams.A0428C = (_, _) => throw new InvalidOperationException("must not run");
        Assert.Equal(0x3F, rig.Linker.ProcessPending(pbi, voice));
        Assert.False(voice.FlagE8);
    }

    [Fact]
    public void ANullVoicePlus8ReachesTheDeliberateFaultAfterStoringE8()
    {
        // 0xA544BC under Unicorn: a null [voice+8] stores voice+0xE8 |= 1 (0xA545A0) and then faults at the udf (0xA545DC), so the C#
        // throws, with the E8 store already made.
        var (rig, voice, pbi) = PendingRig(new Src { Code = 0x3F });
        pbi.StartOffset = 0xFFFFFFFF;
        rig.Seams.SourceFlag10Bit1 = _ => false;
        rig.Seams.A0428C = (_, _) => throw new InvalidOperationException("the engine faults before the call");
        voice.BusOwner8 = null;
        Assert.Throws<InvalidOperationException>(() => rig.Linker.NotReadyCheck(voice, pbi));
        Assert.True(voice.FlagE8);
    }

    [Fact]
    public void ANullSourceIsANullDereferenceNotNotReady()
    {
        // 0xA544C4 ldr r0,[r0,#0xd4]; 0xA544D0 ldr r3,[r0,#0xc]: a voice with no source faults in the engine, so the C# does not return 0.
        var (rig, voice, pbi) = PendingRig(new Src());
        voice.Source = null;
        Assert.Throws<InvalidOperationException>(() => rig.Linker.NotReadyCheck(voice, pbi));
    }

    [Fact]
    public void AnUnreadOwnerLookupAndAnUnwrittenMediaWordAreRefused()
    {
        // [source+0xC] has no writer here and pbi+0x1DC/+0x1E0 are written by the unread 0xA1EC54: neither is defaulted.
        var (rig, voice, pbi) = PendingRig(new Src());
        rig.Seams.SourceOwner = null;
        Assert.Throws<NotSupportedException>(() => rig.Linker.NotReadyCheck(voice, pbi));

        var (rig2, voice2, pbi2) = PendingRig(new Src());
        pbi2.Word1DC = null;
        Assert.Throws<WwiseMissingBehaviourException>(() => rig2.Linker.NotReadyCheck(voice2, pbi2));
        var (rig3, voice3, pbi3) = PendingRig(new Src());
        pbi3.Word1E0 = null;
        Assert.Throws<WwiseMissingBehaviourException>(() => rig3.Linker.NotReadyCheck(voice3, pbi3));
        var (rig4, voice4, pbi4) = PendingRig(new Src());
        rig4.Owners.Remove(voice4.Source!);
        Assert.Throws<InvalidOperationException>(() => rig4.Linker.NotReadyCheck(voice4, pbi4));
    }

    [Theory]
    // (label in emu_notready.py order, vt+0x28 result, latched, [pbi+0x1D8], ratio bits, frames, look-ahead L) -> engine result
    // Every row is a case of re-analysis/tools/emu/emu_notready.py; the last column is that run's r0.
    [InlineData(1, true, 2047, 0x3F800000u, 0x400, 1u, 1)]                        // window 2048: offset 2047 -> 1
    [InlineData(1, true, 2048, 0x3F800000u, 0x400, 1u, 0x3F)]                     // offset == window -> 0x3F
    [InlineData(1, true, 0, 0x00000000u, 0x400, 1u, 0x3F)]                        // ratio 0: product 0 takes -0.5 -> window 0; 0 >= 0
    [InlineData(1, true, 0, 0x3EFFFFFFu, 1, 0u, 1)]                               // 0.49999997f: +0.5 rounds up to 1.0f, window 1 (MathF.Round gave 0)
    [InlineData(1, true, 1, 0x3EFFFFFFu, 1, 0u, 0x3F)]
    [InlineData(1, true, -2048, 0xBF800000u, 0x400, 1u, 0x3F)]                    // ratio -1.0f: trunc(-2048 - 0.5) = -2048
    [InlineData(1, true, 9, 0x3FC00000u, 3, 1u, 0x3F)]                            // 1.5f * 6 = 9.0 + 0.5 -> 9
    [InlineData(1, true, 8, 0x3FC00000u, 3, 1u, 1)]
    [InlineData(1, true, 0, 0x3F800000u, 0x400, 0xFFFFFFFFu, 0x3F)]               // (L + 1) wraps to 0: window 0
    [InlineData(1, true, 0, 0x7FC00000u, 0x400, 1u, 0x3F)]                        // NaN product: -0.5 branch, vcvt gives 0
    [InlineData(1, true, 0x7FFFFFFE, 0x4F32D05Eu, 0x400, 1u, 1)]                  // 3e9f * 2048 saturates to 0x7FFFFFFF: offset below -> 1
    [InlineData(1, true, 0x7FFFFFFF, 0x4F32D05Eu, 0x400, 1u, 0x3F)]
    [InlineData(1, true, int.MinValue, 0xCF32D05Eu, 0x400, 1u, 0x3F)]             // -3e9f saturates to INT_MIN: offset == window
    [InlineData(1, true, 8388609, 0x3F800000u, 3, 2796202u, 1)]                   // product 8388609 (odd, 2^23+1): +0.5 ties to even -> window 8388610
    [InlineData(1, true, 8388610, 0x3F800000u, 3, 2796202u, 0x3F)]
    [InlineData(1, true, 8388607, 0x3F800000u, 2, 4194303u, 1)]                   // product 8388608 (even): window 8388608
    [InlineData(1, true, 8388608, 0x3F800000u, 2, 4194303u, 0x3F)]
    [InlineData(1, true, 33422851, 0x437F0001u, 0xFFFF, 1u, 1)]                   // 255.00002f * 131070 = 33422852: window 33422852
    [InlineData(1, true, 33422852, 0x437F0001u, 0xFFFF, 1u, 0x3F)]
    [InlineData(1, true, 131069, 0x3F800001u, 0xFFFF, 1u, 1)]                     // 131070.015625 + 0.5 -> 131070
    [InlineData(1, true, 131070, 0x3F800001u, 0xFFFF, 1u, 0x3F)]
    [InlineData(1, false, 100, 0x3F800000u, 0x400, 1u, 1)]                        // vt+0x28 returns 1 and sets the latch
    [InlineData(7, false, 0, 0x3F800000u, 0x400, 1u, 2)]                          // any other raw result -> 2
    [InlineData(0, false, 0, 0x3F800000u, 0x400, 1u, 2)]                          // a raw 0 -> 2 (not the 0x3F or 1 of the C# bool)
    public void NotReadyCheckMatchesTheEnginesOwnRunUnderUnicorn(
        int code, bool latched, int offset, uint ratioBits, int frames, uint lookAhead, int expected)
    {
        var src = new Src { Code = code, StartStreamSucceeded = latched };
        var (rig, voice, pbi) = PendingRig(src);
        pbi.StartOffset = unchecked((uint)offset);
        pbi.Ratio = BitConverter.Int32BitsToSingle(unchecked((int)ratioBits));
        rig.Linker.LineMaxFrames = (ushort)frames;
        rig.Linker.ContinuousLookAhead = lookAhead;
        rig.Seams.SourceFlag10Bit1 = _ => true;                            // the side effect is checked in its own tests

        Assert.Equal(expected, rig.Linker.NotReadyCheck(voice, pbi));
    }

    [Fact]
    public void TheLookAheadAndFramesDefaultsAreTheInventoryValues()
    {
        // Inventory 2.3: [0x108D90C+0x1C] = 1; C24 residuals: [0x1052440] .data = 0x400. The window for ratio 1.0 is (1+1)*0x400 = 2048.
        var rig = new Rig();
        Assert.Equal(1u, rig.Linker.ContinuousLookAhead);
        Assert.Equal((ushort)0x400, rig.Linker.LineMaxFrames);
    }

    [Fact]
    public void ResultOneComparesTheOffsetWithTheWindowAndLinksWhenBelowIt()
    {
        // 0xA544BC under Unicorn (latched source): [pbi+0x1D8] = 2048 -> 0x3F, 2047 -> 1, and 1 tail-calls 0xA42DEC (0xA43248).
        int window = 2 * 0x400;
        var (rig, voice, pbi) = PendingRig(new Src { StartStreamSucceeded = true });
        pbi.StartOffset = (uint)window;
        rig.Seams.InitVoiceA54A30 = _ => 1;
        Assert.Equal(0x3F, rig.Linker.ProcessPending(pbi, voice));
        Assert.Contains(voice, rig.Linker.PendingVoices);

        pbi.StartOffset = (uint)(window - 1);
        Assert.Equal(1, rig.Linker.ProcessPending(pbi, voice));            // 0xA42DEC's result
        Assert.DoesNotContain(voice, rig.Linker.PendingVoices);
        Assert.Contains(voice, rig.Live);
    }

    [Fact]
    public void AnyOtherResultDestroysThePendingVoiceAndReturns2()
    {
        // Row 3: "anything else returns 2" from 0xA544BC; 0xA431A8 then runs 0x9D40C4(voice,1) and returns 2.
        var src = new Src { Code = 7 };
        var (rig, voice, pbi) = PendingRig(src);
        rig.Seams.CloseSource56414 = _ => { };
        Assert.Equal(2, rig.Linker.ProcessPending(pbi, voice));
        Assert.DoesNotContain(voice, rig.Linker.PendingVoices);
        Assert.Empty(rig.Live);
        Assert.Null(voice.Source);
    }

    [Fact]
    public void AVoiceWhoseAddSrcReturns3FJoinsThePendingListNotTheLiveList()
    {
        // Row 1 / 5.12: 0x3F appends to the pending list, returns 1, no connection made; the live list is
        // untouched. Row 4: the ctor sets voice+0xCD bit0.
        var bridge = new WwisePlaybackBridge { SourceFactory = _ => new Src { Code = 0x3F } }.WithTestSeams();
        var rig = new Rig();
        var linker = new WwiseVoiceLinker(rig.Buses, rig.Devices, bridge.Voices, _ => rig.Routing, rig.Seams);
        bridge.Linker = linker;
        bridge.LinkEngineA548B8 = _ => { };
        var pbi = bridge.CreatePbiWithMediaWords(new WwisePlayInitParams { PlayingId = 1, TargetNodeId = 1 }, 1,
            new WwiseSourceDescriptor(WwiseSourceFactory.AdpcmPlugin, 1, 1, 0, 0), continuous: false);

        Assert.Equal(1, bridge.AttachVoice(pbi));

        Assert.Empty(bridge.Voices);
        var voice = Assert.Single(linker.PendingVoices);
        Assert.Equal(1, voice.FlagsCD & 1);
        Assert.Empty(voice.Connections);
        // C25.1: the native voice ctor stores [voice+0xDC] = 0 (0xA54798) and [voice+0x1C] = 0 (0xA54678); 0xA548B8
        // (the no-op seam here) does not touch +0xDC. C25.5: [voice+0xF0] is 0 until the init runs.
        Assert.Equal(0, voice.State);
        Assert.Equal(0f, voice.OutputGain);
        Assert.Equal(0u, voice.Word0xF0);
    }

    [Fact]
    public void AVoiceWhoseAddSrcReturns1IsLinkedAndAFailedAddSrcIsTornDown()
    {
        // Row 1 / 5.11: AddSrc 1 -> 0xA42DEC, result 2 is folded to bit0 = (result == 1) = 0 and returned.
        var bridge = new WwisePlaybackBridge { SourceFactory = _ => new Src() }.WithTestSeams();
        var rig = new Rig();
        rig.Seams.InitVoiceA54A30 = _ => 0;                               // 0xA42DEC returns 2
        rig.Seams.CloseSource56414 = _ => { };
        bridge.Linker = new WwiseVoiceLinker(rig.Buses, rig.Devices, bridge.Voices, _ => rig.Routing, rig.Seams);
        bridge.LinkEngineA548B8 = _ => { };
        var pbi = bridge.CreatePbiWithMediaWords(new WwisePlayInitParams { PlayingId = 1, TargetNodeId = 1 }, 1,
            new WwiseSourceDescriptor(WwiseSourceFactory.AdpcmPlugin, 1, 1, 0, 0), continuous: false);
        bridge.StartList.Enqueue(0, pbi, 0);

        Assert.Equal(0, bridge.DrainStartList());                          // 0x9D36EC: only a result of 1 keeps the node
        Assert.Empty(bridge.Voices);

        // No source built: C26.4 (0xA559A4..0xA559B8) runs 0xA01800(pbi, 1) and AddSrc returns 2 (not 0); the voice is then
        // destroyed with 0x9D40C4 and AddSrc's result is returned by 0xA4304C (row 5.12).
        var notes2 = new List<(WwisePlayingInstance, int, int, int)>();
        var bridge2 = new WwisePlaybackBridge { SourceFactory = _ => null }.WithTestSeams();
        bridge2.Notify38600 = (p, a, b, c) => notes2.Add((p, a, b, c));
        bridge2.Linker = new WwiseVoiceLinker(rig.Buses, rig.Devices, bridge2.Voices, _ => rig.Routing, rig.Seams);
        bridge2.LinkEngineA548B8 = _ => { };
        var pbi2 = bridge2.CreatePbiWithMediaWords(new WwisePlayInitParams { PlayingId = 2, TargetNodeId = 1 }, 1,
            new WwiseSourceDescriptor(WwiseSourceFactory.AdpcmPlugin, 1, 1, 0, 0), continuous: false);
        pbi2.Field154 = new WwiseLiveVoice(1, 8);                          // 0xA558F8 had stored the voice: 0xA01800 clears it
        Assert.Equal(2, bridge2.AttachVoice(pbi2));                        // C26.4: 0xA559A4..0xA559B8 return 2
        Assert.Equal(new[] { (pbi2, 4, 1, 0) }, notes2);                   // 0xA01800 -> 0xA38600(pbi, 4, 1, 0)
        Assert.Null(pbi2.Field154);
        Assert.Empty(bridge2.Voices);
    }

    // ------------------------------------------------------------------ start-list pass 2 and the pending walk (C24.1, C24.7)

    private sealed class Frame
    {
        public readonly Rig Rg = new(mainDevice: false);
        public readonly WwisePlaybackBridge Bridge = new WwisePlaybackBridge().WithTestSeams();
        public readonly List<string> Calls = new();

        public Frame()
        {
            Rg.Seams.CloseSource56414 = _ => Calls.Add("close");
            Bridge.Linker = new WwiseVoiceLinker(Rg.Buses, Rg.Devices, Bridge.Voices, _ => Rg.Routing, Rg.Seams);
        }

        /// <summary>A voice parked on the pending list with its PBI (voice+8 = pbi+0xC, pbi+0x154 = voice).</summary>
        public (WwiseLiveVoice Voice, WwisePlayingInstance Pbi) Pending(Src source, int state = 0)
        {
            var pbi = Rig.Pbi();
            var voice = Rig.Voice();
            voice.Source = source;
            Bridge.RegisterSourceOwner(source, pbi);                        // [source+0xC] = the owner PBI (C26.1)
            voice.State = state;
            voice.BusOwner8 = pbi;
            pbi.Field154 = voice;
            Bridge.Linker!.PendingVoices.Add(voice);
            return (voice, pbi);
        }

        public WwiseStartListNode Node(WwisePlayingInstance pbi, int type = 0, long key = 1)
        {
            Bridge.StartList.Enqueue(type, pbi, key);
            return Bridge.StartList.Nodes[^1];
        }
    }

    [Fact]
    public void Pass2LeavesANodeWhoseBit5AndMinusOneSentinelHoldAndNeverCalls0xA431A8()
    {
        // C24.7: bit5 of pbi+0x1BC set and pbi+0x1F8 == -1 leaves the node (0x9D39B0..0x9D39C8). pbi+0x1F8 is the
        // constant -1 from the base ctor (C23.7).
        var f = new Frame();
        var (voice, pbi) = f.Pending(new Src { Code = 0x3F });
        pbi.Flags1BC = 0x20;
        f.Node(pbi);
        f.Bridge.RunStartListPass2();
        Assert.Single(f.Bridge.StartList.Nodes);
        Assert.Contains(voice, f.Bridge.Linker!.PendingVoices);
        Assert.False(((Src)voice.Source!).StartStreamSucceeded);
    }

    [Fact]
    public void Pass2FreesANodeWhoseVoiceIsMissingOrWhoseResultIs2AndRestartsFromTheHead()
    {
        // C24.7: [pbi+0x154] == 0 or 0xA431A8 == 2 -> unlink, free, restart the scan from the head
        // (0x9D39D4..0x9D39FC, 0x9D38A4). 0xA431A8 == 2 also runs 0x9D40C4 (0xA43214).
        var f = new Frame();
        var noVoice = Rig.Pbi();                                          // pbi+0x154 == 0
        f.Node(noVoice, key: 1);
        var (dead, deadPbi) = f.Pending(new Src { Code = 7 });              // 0xA544BC returns 2 for any other result
        f.Node(deadPbi, key: 2);
        var (ok, okPbi) = f.Pending(new Src { Code = 0x3F });
        var keep = f.Node(okPbi, key: 3);

        f.Bridge.RunStartListPass2();

        Assert.Same(keep, Assert.Single(f.Bridge.StartList.Nodes));         // the 0x3F node is kept, flag clear
        Assert.Equal(0, keep.D & 1);
        Assert.DoesNotContain(dead, f.Bridge.Linker!.PendingVoices);        // torn down
        Assert.Contains("close", f.Calls);
        Assert.Contains(ok, f.Bridge.Linker.PendingVoices);
    }

    [Fact]
    public void Pass2ASkippedGroupIsNotDispatchedButAFlaggedNodeInAnotherGroupIs()
    {
        // C24.7: result 0x3F keeps the node with its flag clear and the key group is skipped this frame
        // (0x9D39E4, 0x9D396C..0x9D3990); result 1 sets node+0xD bit0 (0x9D39E8..0x9D39F4); a group with no 0x3F
        // dispatches its flagged nodes and frees them.
        var f = new Frame();
        var started = new List<WwiseLiveVoice>();
        f.Bridge.StartSourceA56478 = started.Add;
        var (v1, p1) = f.Pending(new Src());                                // ready: result 1
        var (v2, p2) = f.Pending(new Src { Code = 0x3F });                  // waiting: 0x3F
        var n1 = f.Node(p1, key: 5);
        var n2 = f.Node(p2, key: 5);                                        // same key group as n1

        f.Bridge.RunStartListPass2();
        Assert.Empty(started);                                              // the group was skipped
        Assert.Equal(1, n1.D & 1);                                          // flagged, not yet dispatched
        Assert.Equal(2, f.Bridge.StartList.Nodes.Count);

        // The 0x3F node's own group is separate on the next frame: give it a different key by removing n2.
        f.Bridge.StartList.Nodes.Remove(n2);
        f.Bridge.RunStartListPass2();
        Assert.Equal(new[] { v1 }, started);                                // n1 (flag set) is dispatched now
        Assert.Empty(f.Bridge.StartList.Nodes);
    }

    [Fact]
    public void Pass2DispatchesByNodeStateThroughThe0x9D3AA4Table()
    {
        // C24.7: 0 0xA54480; 1 0xA54480 then vt+0x4C (0xA53558); 2 vt+0x4C; 3 vt+0x50 (0xA5358C);
        // 4 vt+0x54(voice,pbi) (0xA535D8); 5 vt+0x58 (0xA53698); then the node is freed.
        var f = new Frame();
        var log = new List<string>();
        f.Bridge.StartSourceA56478 = _ => log.Add("A56478");
        f.Bridge.VoiceVt4CA53558 = _ => log.Add("4C");
        f.Bridge.VoiceVt50A5358C = _ => log.Add("50");
        f.Bridge.VoiceVt54A535D8 = (_, _) => log.Add("54");
        f.Bridge.VoiceVt58A53698 = _ => log.Add("58");
        for (int state = 0; state <= 5; state++)
        {
            var (voice, pbi) = f.Pending(new Src(), state: 0);
            var node = f.Node(pbi, state, key: state + 1);
            node.D = 1;                                                     // flag set: dispatched without 0xA431A8
        }

        f.Bridge.RunStartListPass2();

        // State 0 voice: 0xA54480 runs 0xA56478 (voice+0xDC 0 -> 1). Node states 1: 0xA54480 first, then 4C.
        Assert.Equal(new[] { "A56478", "A56478", "4C", "4C", "50", "54", "58" }, log);
        Assert.Empty(f.Bridge.StartList.Nodes);
    }

    [Fact]
    public void Pass2TheUnreadDispatchBodiesAreRequiredAndThrowVisibly()
    {
        // C24.7: 0xA56478, 0xA53558, 0xA5358C, 0xA535D8, 0xA53698 are unread: without a seam the dispatch throws
        // WwiseMissingBehaviourException; a parked voice is never silently dead. (0xA41854 is built, C25.2.)
        foreach (int state in new[] { 0, 1, 2, 3, 4, 5 })
        {
            var f = new Frame();
            var (_, pbi) = f.Pending(new Src());
            f.Node(pbi, state).D = 1;
            Assert.Throws<WwiseMissingBehaviourException>(() => f.Bridge.RunStartListPass2());
        }

        // State 2 reached through 0xA41854 dispatches to the same unread body (C25.2): a live voice owned by the PBI
        // with a non-zero state flags the node, and vt+0x4C (0xA53558) is required.
        var g = new Frame();
        var (gv, gp) = g.Pending(new Src(), state: 1);
        g.Bridge.Voices.Add(gv);
        g.Node(gp, 2);
        Assert.Throws<WwiseMissingBehaviourException>(() => g.Bridge.RunStartListPass2());
    }

    [Fact]
    public void Pass2AStateAbove5TakesTheTableDefaultFreesTheNodeAndNeitherCallsNorThrows()
    {
        // C25.2: the table default 0x9D3AA8 (state above 5) is `b 0x9D3AD4`: it frees the node with no call and does
        // not throw.
        var f = new Frame();
        var log = new List<string>();
        f.Bridge.StartSourceA56478 = _ => log.Add("A56478");
        f.Bridge.VoiceVt4CA53558 = _ => log.Add("4C");
        f.Bridge.VoiceVt50A5358C = _ => log.Add("50");
        f.Bridge.VoiceVt54A535D8 = (_, _) => log.Add("54");
        f.Bridge.VoiceVt58A53698 = _ => log.Add("58");
        var (_, pbi) = f.Pending(new Src());
        f.Node(pbi, 6).D = 1;
        var (_, pbi2) = f.Pending(new Src());
        f.Node(pbi2, 99, key: 2).D = 1;

        f.Bridge.RunStartListPass2();

        Assert.Empty(log);
        Assert.Empty(f.Bridge.StartList.Nodes);
    }

    [Fact]
    public void Pass2AStateAbove1NodeWithItsFlagClearIsUnlinkedWhenA41854FindsNoVoice()
    {
        // C26.1: 0xA41854 finds no live voice whose source owner is the PBI, and the fallback [pbi+0x154] is 0: the
        // result is 0 and the caller unlinks the node (0x9D3BC4). A non-zero fallback keeps the node.
        var f = new Frame();
        var p1 = Rig.Pbi();                                                 // pbi+0x154 == 0, no live voice
        f.Node(p1, 2, key: 1);
        var (_, p2) = f.Pending(new Src());                                 // pbi+0x154 = voice whose source owner is p2
        var b = f.Node(p2, 3, key: 2);
        f.Bridge.RunStartListPass2();
        Assert.Same(b, Assert.Single(f.Bridge.StartList.Nodes));           // b: result non-zero, flag still clear: kept
        Assert.Equal(0, b.D & 1);
    }

    private static WwiseLiveVoice LiveVoice(Frame f, WwisePlayingInstance? owner, int state, Src? pendingSource = null,
        WwisePlayingInstance? pendingOwner = null)
    {
        var v = Rig.Voice();
        v.State = state;
        if (owner is not null)
        {
            var src = new Src();
            f.Bridge.RegisterSourceOwner(src, owner);
            v.Source = src;                                                 // [voice+0xD4], [[voice+0xD4]+0xC] = owner
        }
        if (pendingSource is not null && pendingOwner is not null)
        {
            f.Bridge.RegisterSourceOwner(pendingSource, pendingOwner);
            v.Pending = pendingSource;                                      // [voice+0xD8], [[voice+0xD8]+0xC] = pendingOwner
        }
        return v;
    }

    [Theory]
    [InlineData(1, true)]                                                   // [voice+0xDC] != 0: node+0xD bit0 set, dispatched
    [InlineData(2, true)]
    [InlineData(0, false)]                                                  // [voice+0xDC] == 0: bit0 stays clear, node kept
    public void Pass2A41854SetsTheNodeFlagFromTheLiveVoiceStateAndDispatchesWithTheReturnedVoice(int state, bool dispatched)
    {
        // C26.1 0xA4187C..0xA418C8: [[voice+0xD4]+0xC] == pbi is tested for every node; a hit sets node+0xD bit0 when
        // [voice+0xDC] != 0 and returns the voice; 0x9D38F8..0x9D3910 then re-tests bit0 and jumps to the state dispatch
        // (0x9D3A9C) with the returned value. Node state 3 -> vt+0x50 (0xA5358C).
        var f = new Frame();
        var (parked, pbi) = f.Pending(new Src(), state: 0);                 // pbi+0x154 = parked (the fallback value)
        var live = LiveVoice(f, pbi, state);
        f.Bridge.Voices.Add(Rig.Voice());                                   // a voice with a null source never matches (0xA53F04)
        f.Bridge.Voices.Add(LiveVoice(f, Rig.Pbi(), 1));                    // a voice whose source belongs to another PBI
        f.Bridge.Voices.Add(live);
        var got = new List<WwiseLiveVoice>();
        f.Bridge.VoiceVt50A5358C = got.Add;
        var node = f.Node(pbi, 3);

        f.Bridge.RunStartListPass2();

        if (dispatched)
        {
            Assert.Same(live, Assert.Single(got));                          // the returned voice, not [pbi+0x154]
            Assert.NotSame(parked, got[0]);
            Assert.Empty(f.Bridge.StartList.Nodes);                         // freed after the dispatch (0x9D3AD4)
        }
        else
        {
            Assert.Empty(got);
            Assert.Same(node, Assert.Single(f.Bridge.StartList.Nodes));
            Assert.Equal(0, node.D & 1);
        }
    }

    [Fact]
    public void Pass2A41854TestsThePendingSourceOwnerOnlyForANodeOfType4()
    {
        // C26.1: [[voice+0xD8]+0xC] == pbi is tested only when the node type is 4 (cmp r2,#4 at 0xA41898, test
        // 0xA4189C..0xA418B4). A type-4 node reaches the voice through its pending source and dispatches vt+0x54 with it;
        // a type-3 node does not match it, and its fallback ([pbi+0x154] = 0) makes the result 0 (the node is unlinked).
        foreach (int type in new[] { 4, 3 })
        {
            var f = new Frame();
            var pbi = Rig.Pbi();                                            // pbi+0x154 == 0
            var live = LiveVoice(f, Rig.Pbi(), state: 1, pendingSource: new Src(), pendingOwner: pbi);
            f.Bridge.Voices.Add(live);
            var got = new List<(WwiseLiveVoice, WwisePlayingInstance)>();
            f.Bridge.VoiceVt54A535D8 = (v, p) => got.Add((v, p));
            f.Bridge.VoiceVt50A5358C = _ => throw new InvalidOperationException("type 3 must not match the pending source");
            f.Node(pbi, type);

            f.Bridge.RunStartListPass2();

            Assert.Empty(f.Bridge.StartList.Nodes);                         // dispatched and freed, or unlinked on 0
            if (type == 4) Assert.Equal((live, pbi), Assert.Single(got));
            else Assert.Empty(got);
        }
    }

    [Fact]
    public void Pass2A41854FallbackReturnsNullWhenTheOwnerTestFailsAndTheTypeIsNot4ButAlwaysForType4AndNeverSetsBit0()
    {
        // C26.1 0xA418D4..0xA41908: r3 = [pbi+0x154]; returned when [[r3+0xD4]+0xC] == pbi or the node type is 4, else 0
        // (the caller unlinks the node); the fallback never sets node+0xD bit0, even for a voice with state != 0.
        var f = new Frame();
        var pbiA = Rig.Pbi();
        pbiA.Field154 = LiveVoice(f, Rig.Pbi(), state: 1);                  // its source is owned by another PBI
        f.Node(pbiA, 3, key: 1);                                            // owner test fails, type != 4: result 0, unlinked
        var pbiB = Rig.Pbi();
        pbiB.Field154 = LiveVoice(f, Rig.Pbi(), state: 1);
        var keptType4 = f.Node(pbiB, 4, key: 2);                            // type 4: returned without the owner test, node kept
        var pbiC = Rig.Pbi();
        pbiC.Field154 = LiveVoice(f, pbiC, state: 1);
        var keptOwned = f.Node(pbiC, 3, key: 3);                            // owner test holds: returned, node kept
        f.Bridge.VoiceVt50A5358C = _ => throw new InvalidOperationException("the fallback must not set bit0");
        f.Bridge.VoiceVt54A535D8 = (_, _) => throw new InvalidOperationException("the fallback must not set bit0");

        f.Bridge.RunStartListPass2();

        Assert.Equal(new[] { keptType4, keptOwned }, f.Bridge.StartList.Nodes);
        Assert.Equal(0, keptType4.D & 1);
        Assert.Equal(0, keptOwned.D & 1);
    }

    [Fact]
    public void Pass2AfterANonZeroResultWithBit0ClearTheScanContinuesWithTheNextNodeWithoutLooping()
    {
        // C26.2 0x9D3914..0x9D399C: the node stays, control moves to the NEXT node (no loop on the same node, no
        // head restart). Node A is kept (fallback hit, bit0 clear); node B, later in the list, is dispatched exactly once.
        var f = new Frame();
        var (_, pa) = f.Pending(new Src());
        var a = f.Node(pa, 3, key: 1);
        var pb = Rig.Pbi();
        var liveB = LiveVoice(f, pb, state: 1);
        f.Bridge.Voices.Add(liveB);
        f.Node(pb, 3, key: 2);
        var got = new List<WwiseLiveVoice>();
        f.Bridge.VoiceVt50A5358C = got.Add;

        f.Bridge.RunStartListPass2();

        Assert.Equal(new[] { liveB }, got);
        Assert.Same(a, Assert.Single(f.Bridge.StartList.Nodes));
        Assert.Equal(0, a.D & 1);
    }

    [Fact]
    public void Pass2A41854ASourceThatCannotReportItsOwnerPbiIsARequiredSeamThatThrows()
    {
        // C26.1: [[voice+0xD4]+0xC] needs the source's owner PBI; a source that was never registered cannot report it.
        var f = new Frame();
        var pbi = Rig.Pbi();
        var v = Rig.Voice();
        v.Source = new Src();                                               // never registered
        f.Bridge.Voices.Add(v);
        f.Node(pbi, 3);
        Assert.Throws<WwiseMissingBehaviourException>(() => f.Bridge.RunStartListPass2());
    }

    // ------------------------------------------------------------------ pass 1 type gate (C26.3) and AddSrc (C26.4, C26.5)

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Pass1KeepsANodeOfTypeAbove1UnattachedAndUnfreed(int type)
    {
        // C26.3 0x9D36C0 ldrb r2,[r4,#0xc]; cmp r2,#1; bhi 0x9D36A0: after the pbi+0x154 test and before the bit5 test,
        // a node of type above 1 is kept: no attach (no voice created), no destroy (no 0xA01800 notification), even with
        // bit5 and the -1 sentinel that would otherwise destroy it.
        var f = new Frame();
        f.Bridge.LinkEngineA548B8 = _ => throw new InvalidOperationException("no voice may be created");
        f.Bridge.Notify38600 = (_, _, _, _) => throw new InvalidOperationException("must not destroy");
        f.Bridge.SourceFactory = _ => throw new InvalidOperationException("no source may be built");
        var pbi = Rig.Pbi();
        pbi.Flags1BC = 0x20;
        var node = f.Node(pbi, type);

        Assert.Equal(0, f.Bridge.RunStartListPass1());

        Assert.Same(node, Assert.Single(f.Bridge.StartList.Nodes));
        Assert.Empty(f.Bridge.Voices);
        Assert.Empty(f.Bridge.Linker!.PendingVoices);
        Assert.Null(pbi.Field154);
    }

    private static WwisePlayingInstance BridgePbi(WwisePlaybackBridge bridge) =>
        bridge.CreatePbiWithMediaWords(new WwisePlayInitParams { PlayingId = 1, TargetNodeId = 1 }, 1,
            new WwiseSourceDescriptor(WwiseSourceFactory.AdpcmPlugin, 1, 1, 0, 0), continuous: false);

    /// <summary>
    /// The AddSrc rig for C27. Every seam logs its call so the order can be compared with the C27 step table; the
    /// seams stand for unread bodies and claim nothing beyond the value each test sets.
    /// </summary>
    private sealed class AddSrcRig
    {
        public readonly List<string> Log = new();
        public readonly Src Source;
        public readonly WwisePlaybackBridge Bridge;
        public readonly WwisePlayingInstance Pbi;
        public (int Code, int Index) Eda = (0, 0);
        public int Gate;
        public bool Flag100000;
        public WwiseVoiceSendTable? Alloc = new();
        public WwiseLiveVoice? Voice;
        public int Vt120Result;
        public readonly object A054D8Global = new();
        public readonly WwiseNode RoutingNode = new WwiseActorMixerNode(77, "t.bnk",
            new WwiseNodeParams(0, 0, 0, new Dictionary<byte, uint>(), new Dictionary<byte, (float, float)>(), Array.Empty<WwiseRtpc>(),
                Array.Empty<(uint, byte, IReadOnlyList<(uint, uint)>)>()), Array.Empty<uint>());
        public WwiseNode? SeenNode;
        public uint? SeenVt120Arg;

        public AddSrcRig(int startCode = 1)
        {
            Source = new Src { Code = startCode };
            Bridge = new WwisePlaybackBridge { SourceFactory = _ => Source }.WithTestSeams();
            Bridge.Linker = new WwiseVoiceLinker(
                new WwiseMixBusHierarchy(), new WwiseOutputDeviceList(), new List<WwiseLiveVoice>(),
                _ => new WwisePbiRouting { Node = new WwiseRoutingNode { Id = 77 } }, new WwiseVoiceLinkSeams());
            Pbi = BridgePbi(Bridge);
            Pbi.NodeE0 = RoutingNode;                                  // [pbi+0xE0]: the node 0x9EEDA4 and vt+0x120 receive
            Bridge.NextSource9EEDA4 = (WwiseNode? node, out int index) =>
            {
                Log.Add("9EEDA4:154=" + (Pbi.Field154 is not null ? "voice" : "null"));
                SeenNode = node;
                index = Eda.Index;
                return Eda.Code;
            };
            Bridge.NodeVt120A379D8 = (node, arg) => { Log.Add("vt120"); SeenNode = node; SeenVt120Arg = arg; return Vt120Result; };
            Bridge.NewVoiceAllocSendTable4C = _ => { Log.Add("alloc4C"); return Alloc; };
            Bridge.Call9BCA68 = p => { Log.Add("9BCA68"); return Gate; };
            Bridge.CallA0228C = _ => Log.Add("A0228C");
            Bridge.PbiFlag4Bit100000 = _ => Flag100000;
            Bridge.A054D8Context = A054D8Global;                       // test double: which global 0xA56454 reads is MISSING
            Bridge.CallA054D8 = (ctx, id, src) =>
            {
                Assert.Same(A054D8Global, ctx);
                Assert.Same(Source, src);
                Log.Add("A054D8:" + id);
            };
            Bridge.Notify38600 = (p, a, b, c) => Log.Add($"A38600:{a},{b},{c}:154=" + (p.Field154 is null ? "null" : "voice"));
            Bridge.SourceDestructAndPoolFree = s => Log.Add(ReferenceEquals(s, Source) ? "destruct" : "destruct-other");
            Bridge.SourceFormatWriter15C = (_, _) => Log.Add("writer15C");
        }

        public WwiseLiveVoice NewVoice(byte flagsCd = 1) => Voice = new WwiseLiveVoice(1, 8) { FlagsCD = flagsCd };
    }

    [Fact]
    public void AddSrcWithAMissingSourceRuns0xA01800AndReturns2()
    {
        // C27 step 2 (0xA558E4..0xA558E8, 0xA559A4..0xA559B0): a null source runs 0xA01800(pbi, 1) (pbi+0x154 = 0 and
        // 0xA38600(pbi, 4, 1, 0)) and returns 2; nothing else is stored. The 0xA38600 body is unread: a required seam.
        var bridge = new WwisePlaybackBridge { SourceFactory = _ => null }.WithTestSeams();
        var pbi = BridgePbi(bridge);
        Assert.Throws<WwiseMissingBehaviourException>(() => bridge.AddSrc(new WwiseLiveVoice(1, 8), pbi, bActive: true));

        var notes = new List<(WwisePlayingInstance, int, int, int)>();
        bridge.Notify38600 = (p, a, b, c) => notes.Add((p, a, b, c));
        var voice = new WwiseLiveVoice(1, 8);
        Assert.Equal(2, bridge.AddSrc(voice, pbi, bActive: true));
        Assert.Equal(new[] { (pbi, 4, 1, 0) }, notes);
        Assert.Null(pbi.Field154);
        Assert.Null(voice.Source);                                           // step 2: nothing else is stored
        Assert.Equal(0, pbi.NextSourceCache1BB);                             // step 4 (0xA01768) was not reached
        Assert.Null(voice.SendTable);
    }

    [Fact]
    public void AddSrcNewVoiceStoresPbi154First_ThenA01768ThenTheSendTableThenStartStream_C27Steps3To10()
    {
        // C27 step 3: [pbi+0x154] = voice on both paths before anything else, so 0x9EEDA4 (step 4, inside 0xA01768) already
        // sees it. Step 4: 0xA01768 out -> [voice+0xE0], result -> [voice+0xE4]; [voice+0x18] == 0 -> 0x4C allocation into
        // [voice+0x10] and [voice+0x18] = 1. Step 5: [voice+0xE4] != 0 -> 0x9BCA68; result 0 goes on to step 7 (StartStream,
        // 0xA56650). Step 10: [voice+0xD4] = source, [voice+8] = pbi, pbi+0x1BE bit3 cleared, return r6.
        var rig = new AddSrcRig();
        rig.Eda = (2, 5);
        rig.Gate = 0;
        var voice = rig.NewVoice();
        rig.Pbi.Flags1BE |= 8;

        Assert.Equal(1, rig.Bridge.AddSrc(voice, rig.Pbi, bActive: true));

        Assert.Equal(new[] { "9EEDA4:154=voice", "alloc4C", "9BCA68", "writer15C" }, rig.Log);
        Assert.Equal(5, voice.E0);                                           // out = pbi+0x1BB & 7
        Assert.Equal(2, voice.E4);                                           // return = (pbi+0x1BB >> 3) & 0xF
        Assert.Equal(0x80 | 5 | (2 << 3), rig.Pbi.NextSourceCache1BB);       // bit7 = the cached mark
        Assert.Equal(1, voice.SendTable!.Capacity);                          // [voice+0x18] = 1
        Assert.True(rig.Source.StartStreamSucceeded);                        // step 7 ran
        Assert.Same(rig.Source, voice.Source);                               // [voice+0xD4]
        Assert.Same(rig.Pbi, voice.BusOwner8);                               // [voice+8] = [source+0xC]+0xC
        Assert.Equal(0, rig.Pbi.Flags1BE & 8);                               // pbi+0x1BE bit3 cleared
        Assert.Same(voice, rig.Pbi.Field154);
        Assert.Equal(1, voice.FlagsCD & 1);                                  // only the step-5 shortcut clears bit0
    }

    [Fact]
    public void AddSrcA01768ComputesOnceThenAnswersFromThePbiPlus0x1BBCache_C27Step4()
    {
        // C27 step 4 / further facts: 0xA01768 is a cache in pbi+0x1BB; the first call computes through 0x9EEDA4 and sets
        // bit7, later calls read out = byte & 7 and return = (byte >> 3) & 0xF.
        var rig = new AddSrcRig();
        rig.Eda = (2, 3);
        rig.Bridge.AddSrc(rig.NewVoice(), rig.Pbi, bActive: true);
        rig.Log.Clear();
        rig.Eda = (7, 7);                                                    // must not be consulted again
        var second = rig.NewVoice();
        rig.Bridge.AddSrc(second, rig.Pbi, bActive: true);

        Assert.DoesNotContain(rig.Log, l => l.StartsWith("9EEDA4"));
        Assert.Equal(3, second.E0);
        Assert.Equal(2, second.E4);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(5, 2)]
    [InlineData(-1, 2)]
    public void AddSrcA01768Code3IsMappedThroughVt120To1Or2AndTheRawThreeIsNeverStored_C27Step4(int vt120, int stored)
    {
        // C27 step 4 further facts: 0x9EEDA4's result 3 is mapped through vt+0x120 to 1 or 2 (0xA017A4 cmp r0,#3;
        // 0xA017C8..0xA017E4: vt+0x120([pbi+0xE0], [pbi+0x14C]); 0 gives 1, otherwise 2; only the mapped value is stored).
        var rig = new AddSrcRig();
        rig.Eda = (3, 5);
        rig.Vt120Result = vt120;
        var voice = rig.NewVoice();

        rig.Bridge.AddSrc(voice, rig.Pbi, bActive: true);

        Assert.Same(rig.RoutingNode, rig.SeenNode);                          // [pbi+0xE0] is the node passed
        Assert.Equal(rig.Pbi.TargetNodeId, rig.SeenVt120Arg);                // [pbi+0x14C]
        Assert.Equal(stored, voice.E4);                                      // [voice+0xE4]
        Assert.Equal(5, voice.E0);                                           // the out index
        Assert.Equal(stored, (rig.Pbi.NextSourceCache1BB >> 3) & 0xF);       // pbi+0x1BB bits 3..6
        Assert.NotEqual(3, (rig.Pbi.NextSourceCache1BB >> 3) & 0xF);
        Assert.NotEqual(3, voice.E4);
    }

    [Fact]
    public void AddSrcA01768Vt120IsCalledOnlyForCode3AndIsARequiredSeam_C27Step4()
    {
        // C27 step 4: vt+0x120 is reached only when the 0x9EEDA4 result is 3; an unread body is a required seam.
        var rig = new AddSrcRig { Eda = (2, 0) };
        rig.Bridge.NodeVt120A379D8 = null;
        rig.Bridge.AddSrc(rig.NewVoice(), rig.Pbi, bActive: true);           // code 2: not reached, no throw
        Assert.Equal(2, (rig.Pbi.NextSourceCache1BB >> 3) & 0xF);

        rig = new AddSrcRig { Eda = (3, 0) };
        rig.Bridge.NodeVt120A379D8 = null;
        Assert.Throws<WwiseMissingBehaviourException>(() => rig.Bridge.AddSrc(rig.NewVoice(), rig.Pbi, bActive: true));
        Assert.Equal(0, rig.Pbi.NextSourceCache1BB);                         // nothing cached on the throw
    }

    [Fact]
    public void AddSrcSendTableFieldIsTheRawAllocationResultEvenWhenItFails_C27Step4()
    {
        // C27 step 4: 0xA559F0 str r0,[r4,#0x10] stores the allocation result even when it is 0; then 0xA55A70.
        var fail = new AddSrcRig { Alloc = null };
        var voice = fail.NewVoice();
        voice.SendTable = new WwiseVoiceSendTable { Capacity = 0 };          // [voice+0x18] == 0: allocation runs
        Assert.Equal(2, fail.Bridge.AddSrc(voice, fail.Pbi, bActive: true));
        Assert.Null(voice.SendTable);
    }

    [Fact]
    public void AddSrcDefaultSourceWithoutAMediaResolverThrowsButAnUnselectedKindIsTheNativeNullSource_C27Steps1And2()
    {
        // C27 step 2: only a null source from the factory (0xA562B8 selecting no class, 0xA558E4) takes the null path.
        // A missing MediaFor is not that condition and must not look like it.
        var noMedia = new WwisePlaybackBridge().WithTestSeams();
        var pbi = BridgePbi(noMedia);                                        // ADPCM plug-in, stream 1: a class is selected
        Assert.Throws<WwiseMissingBehaviourException>(() => noMedia.AddSrc(new WwiseLiveVoice(1, 8), pbi, bActive: true));

        var unselected = new WwisePlaybackBridge { MediaFor = _ => null }.WithTestSeams();
        var notes = new List<(int, int, int)>();
        unselected.Notify38600 = (_, a, b, c) => notes.Add((a, b, c));
        var pbi2 = unselected.CreatePbiWithMediaWords(new WwisePlayInitParams { PlayingId = 1, TargetNodeId = 1 }, 1,
            new WwiseSourceDescriptor(0, 0, 1, 0, 0), continuous: false);    // plug-in 0: mode 0, no class selected
        Assert.Equal(2, unselected.AddSrc(new WwiseLiveVoice(1, 8), pbi2, bActive: true));
        Assert.Equal(new[] { (4, 1, 0) }, notes);
    }

    [Fact]
    public void AddSrcSendTableIsAllocatedOnlyWhenPlus0x18IsZero_AndAFailureReturns2AfterTheSourceDestroy_C27Step4()
    {
        // C27 step 4 (0xA559E8..0xA55A04, 0xA55A70): [voice+0x18] != 0 -> no allocation. A failed allocation goes to
        // 0xA55A70 (r6 = 2): step 8 destroys the source and returns 2.
        var rig = new AddSrcRig();
        var voice = rig.NewVoice();
        voice.SendTable = new WwiseVoiceSendTable { Capacity = 4 };
        var existing = voice.SendTable;
        Assert.Equal(1, rig.Bridge.AddSrc(voice, rig.Pbi, bActive: true));
        Assert.DoesNotContain("alloc4C", rig.Log);
        Assert.Same(existing, voice.SendTable);

        var fail = new AddSrcRig { Alloc = null };
        var v2 = fail.NewVoice();
        Assert.Equal(2, fail.Bridge.AddSrc(v2, fail.Pbi, bActive: true));
        Assert.Equal(new[] { "9EEDA4:154=voice", "alloc4C", "A38600:4,1,0:154=null", "destruct" }, fail.Log);
        Assert.Null(fail.Pbi.Field154);
        Assert.Null(v2.Source);
        Assert.False(fail.Source.StartStreamSucceeded);                      // StartStream not reached
    }

    [Fact]
    public void AddSrcGate9BCA68WithE4Equal1Returns3AfterTheSourceDestroy_C27Step5()
    {
        // C27 step 5: [voice+0xE4] == 1 and a non-zero 0x9BCA68 result go to 0xA55960 (r6 = 3): step 8 destroys the source
        // and returns 3; StartStream is not called.
        var rig = new AddSrcRig();
        rig.Eda = (1, 0);
        rig.Gate = 1;
        var voice = rig.NewVoice();

        Assert.Equal(3, rig.Bridge.AddSrc(voice, rig.Pbi, bActive: true));

        Assert.Equal(new[] { "9EEDA4:154=voice", "alloc4C", "9BCA68", "A38600:4,1,0:154=null", "destruct" }, rig.Log);
        Assert.False(rig.Source.StartStreamSucceeded);
        Assert.Null(voice.Source);
        Assert.Null(rig.Pbi.Field154);
    }

    [Theory]
    [InlineData((byte)1, true)]
    [InlineData((byte)0, false)]
    public void AddSrcGate9BCA68ShortcutCallsA0228COnlyWhenBit0IsSetThenClearsBit0AndSkipsStartStream_C27Step5(
        byte flagsCd, bool a0228cRuns)
    {
        // C27 step 5: new voice, [voice+0xE0] == 0, [voice+0xE4] neither 0 nor 1, non-zero 0x9BCA68 result: 0xA0228C(pbi) only
        // if [voice+0xCD] bit0 is set, then [voice+0xCD] bit0 is cleared, r6 = 1 and StartStream (and so the 0x15C writer
        // inside it) is skipped; step 10 stores the source.
        var rig = new AddSrcRig();
        rig.Eda = (2, 0);
        rig.Gate = 1;
        var voice = rig.NewVoice(flagsCd);
        voice.FlagsCD |= 0x02;
        rig.Pbi.Flags1BE |= 8;

        Assert.Equal(1, rig.Bridge.AddSrc(voice, rig.Pbi, bActive: true));

        Assert.Equal(a0228cRuns, rig.Log.Contains("A0228C"));
        Assert.DoesNotContain("writer15C", rig.Log);
        Assert.False(rig.Source.StartStreamSucceeded);
        Assert.Equal(0, voice.FlagsCD & 1);
        Assert.Equal(2, voice.FlagsCD & 2);                                  // only bit0 is cleared
        Assert.Same(rig.Source, voice.Source);                               // step 10
        Assert.Same(rig.Pbi, voice.BusOwner8);
        Assert.Equal(0, rig.Pbi.Flags1BE & 8);
        Assert.Same(voice, rig.Pbi.Field154);
    }

    [Fact]
    public void AddSrcGate9BCA68WithNonZeroE0OrNoResultContinuesToStartStream_C27Step5()
    {
        // C27 step 5: r3 = [voice+0xE0] != 0 ? 0 : (bActive & 1); r3 == 0 goes on to step 7. A zero 0x9BCA68 result also
        // goes on to step 7. Neither touches [voice+0xCD] bit0 or runs 0xA0228C.
        var rig = new AddSrcRig();
        rig.Eda = (2, 3);
        rig.Gate = 1;
        var voice = rig.NewVoice();
        Assert.Equal(1, rig.Bridge.AddSrc(voice, rig.Pbi, bActive: true));
        Assert.True(rig.Source.StartStreamSucceeded);
        Assert.DoesNotContain("A0228C", rig.Log);
        Assert.Equal(1, voice.FlagsCD & 1);

        var zero = new AddSrcRig();
        zero.Eda = (2, 0);
        zero.Gate = 0;
        var v2 = zero.NewVoice();
        Assert.Equal(1, zero.Bridge.AddSrc(v2, zero.Pbi, bActive: true));
        Assert.True(zero.Source.StartStreamSucceeded);
        Assert.DoesNotContain("A0228C", zero.Log);
        Assert.Equal(1, v2.FlagsCD & 1);
    }

    [Fact]
    public void AddSrcReusePathStoresOnlyVoicePlus0xD8_AndIsAlsoGatedBy9BCA68_C27Steps5And9()
    {
        // C27 step 9: sb == 0 -> [voice+0xD8] = source and return r6; nothing else is stored, and steps 4 (0xA01768, the
        // allocation) are not run. Step 5 runs on both paths: [voice+0xE4] != 0 calls 0x9BCA68; with E4 == 1 and a
        // non-zero result the source is destroyed and 3 returned; with another E4 the reuse path has r3 = 0 (bActive is 0).
        var rig = new AddSrcRig();
        var voice = rig.NewVoice();
        var oldSource = new Src();
        voice.Source = oldSource;
        voice.BusOwner8 = "owner";
        voice.E4 = 0;
        rig.Pbi.Flags1BE |= 8;

        Assert.Equal(1, rig.Bridge.AddSrc(voice, rig.Pbi, bActive: false));
        Assert.Equal(new[] { "writer15C" }, rig.Log);                        // no 9EEDA4, no alloc, no 9BCA68 (E4 == 0)
        Assert.Same(rig.Source, voice.Pending);                              // [voice+0xD8]
        Assert.Same(oldSource, voice.Source);
        Assert.Equal("owner", voice.BusOwner8);
        Assert.Equal(8, rig.Pbi.Flags1BE & 8);                               // pbi+0x1BE bit3 not cleared
        Assert.Same(voice, rig.Pbi.Field154);                                // step 3 holds on the reuse path too

        var gated = new AddSrcRig { Gate = 1 };
        var v2 = gated.NewVoice();
        v2.E4 = 2;
        Assert.Equal(1, gated.Bridge.AddSrc(v2, gated.Pbi, bActive: false));
        Assert.Equal(new[] { "9BCA68", "writer15C" }, gated.Log);
        Assert.Same(gated.Source, v2.Pending);
        Assert.Equal(1, v2.FlagsCD & 1);                                     // no shortcut on the reuse path

        var destroy = new AddSrcRig { Gate = 1 };
        var v3 = destroy.NewVoice();
        v3.E4 = 1;
        Assert.Equal(3, destroy.Bridge.AddSrc(v3, destroy.Pbi, bActive: false));
        Assert.Equal(new[] { "9BCA68", "A38600:4,1,0:154=null", "destruct" }, destroy.Log);
        Assert.Null(v3.Pending);
    }

    [Fact]
    public void AddSrcStartStreamFailureRunsA56414ThenTheDestructorAndClearsPbi154WithTheNotification_C27Steps7And8()
    {
        // C27 step 7: a raw StartStream result other than 1 or 0x3F goes to step 8. Step 8: 0xA56414(source, 1) first calls
        // 0xA054D8([pbi+0x140]) when [pbi+4] & 0x100000, then 0xA01800(pbi, 1) (pbi+0x154 = 0 and 0xA38600(pbi, 4, 1, 0)),
        // then the source destructor and the pool free; return r6 = the raw result.
        // Test double: 7 stands for "a raw vt+0x28 result other than 1 or 0x3F" (C27 step 7); the value 7 itself is not a C27 fact.
        // C27 does not place the pbi+0x15C writer relative to these steps, so "writer15C" is filtered out and its position is
        // unasserted; what is asserted is C27 steps 4 and 8: 0xA01768, the allocation, then 0xA01800's notification with
        // pbi+0x154 already cleared, then the destructor and pool free.
        var rig = new AddSrcRig(startCode: 7);
        var voice = rig.NewVoice();
        Assert.Equal(7, rig.Bridge.AddSrc(voice, rig.Pbi, bActive: true));
        Assert.Equal(new[] { "9EEDA4:154=voice", "alloc4C", "A38600:4,1,0:154=null", "destruct" },
            rig.Log.Where(l => l != "writer15C").ToArray());
        Assert.Null(rig.Pbi.Field154);
        Assert.Null(voice.Source);
        Assert.Null(voice.Pending);

        var flagged = new AddSrcRig(startCode: 7) { Flag100000 = true };
        Assert.Equal(7, flagged.Bridge.AddSrc(flagged.NewVoice(), flagged.Pbi, bActive: false));
        // C27 step 8: 0xA054D8 first (when [pbi+4] & 0x100000), then 0xA01800's notification, then the destructor; the
        // 0x15C writer's position is unasserted (not stated by C27).
        Assert.Equal(
            new[] { "A054D8:" + flagged.Pbi.PlayingId, "A38600:4,1,0:154=null", "destruct" },
            flagged.Log.Where(l => l != "writer15C").ToArray());
    }

    [Fact]
    public void AddSrcStartStreamResult0x3FIsNotADestroy_C27Step7()
    {
        // C27 step 7: only results other than 1 and 0x3F destroy; 0x3F falls to step 10 with r6 = 0x3F, and pbi+0x154 stays set.
        var rig = new AddSrcRig(startCode: 0x3F);
        var voice = rig.NewVoice();
        Assert.Equal(0x3F, rig.Bridge.AddSrc(voice, rig.Pbi, bActive: true));
        Assert.DoesNotContain("destruct", rig.Log);
        Assert.Same(rig.Source, voice.Source);
        Assert.Same(voice, rig.Pbi.Field154);
    }

    [Fact]
    public void AddSrcCallsStartStreamWithTheOwnerPbiWords1DCAnd1E0_C27Step7()
    {
        // C27 step 7 / C30: 0xA56650(source, [owner+0x1DC], [owner+0x1E0]); the native loads r1/r2 from r7 = [source+0xC] (0xA5590C, 0xA55910).
        var rig = new AddSrcRig();
        rig.Pbi.Word1DC = 0xDEAD0001;
        rig.Pbi.Word1E0 = 0xBEEF0002;
        Assert.Equal(1, rig.Bridge.AddSrc(rig.NewVoice(), rig.Pbi, bActive: true));
        Assert.Equal(new[] { (0xDEAD0001u, 0xBEEF0002u) }, rig.Source.Calls);
        Assert.True(rig.Source.StartStreamSucceeded);                        // a raw 1 sets the latch (0xA56678..0xA56684)
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(7)]
    [InlineData(-1)]
    [InlineData(0x40)]
    [InlineData(0x3E)]
    public void AddSrcPassesAnyRawStartStreamResultOtherThan1Or0x3FToStep8Unchanged_C27Steps7And8(int raw)
    {
        // C27 step 7: r6 is the raw vt+0x28 result (mov r6,r0 at 0xA55920); `cmp r0,#1; cmpne r0,#0x3f; bne 0xA55964` sends every other int to step 8,
        // which returns r6 unchanged (0xA55998 mov r0,r6). The latch is set only by a raw 1.
        var rig = new AddSrcRig(startCode: raw);
        var voice = rig.NewVoice();
        Assert.Equal(raw, rig.Bridge.AddSrc(voice, rig.Pbi, bActive: true));
        Assert.Contains("destruct", rig.Log);
        Assert.Null(rig.Pbi.Field154);
        Assert.Null(voice.Source);
        Assert.False(rig.Source.StartStreamSucceeded);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(0x3F, false)]
    [InlineData(0, false)]
    [InlineData(2, false)]
    [InlineData(7, false)]
    [InlineData(-1, false)]
    public void AddSrcSetsTheLatchOnlyForARawResultOf1AndCallsVt28OnceWithTheOwnerWords_C27Step7(int raw, bool latch)
    {
        // 0xA56678..0xA56684: cmp r0,#1; orreq sets [source+0x10] bit0 only for exactly 1.
        var rig = new AddSrcRig(startCode: raw);
        rig.Bridge.AddSrc(rig.NewVoice(), rig.Pbi, bActive: true);
        Assert.Equal(latch, rig.Source.StartStreamSucceeded);
        Assert.Equal(new[] { (Rig.Media1DC, Rig.Media1E0) }, rig.Source.Calls);
    }

    [Fact]
    public void AddSrcDoesNotCallStartStreamForALatchedSourceAndReturns1_C27Step7()
    {
        // 0xA56650: [source+0x10] bit0 set -> r0 = 1 without calling vt+0x28 (0xA56650..0xA56660).
        var rig = new AddSrcRig(startCode: 7) { };
        rig.Source.StartStreamSucceeded = true;
        Assert.Equal(1, rig.Bridge.AddSrc(rig.NewVoice(), rig.Pbi, bActive: true));
        Assert.Empty(rig.Source.Calls);
    }

    [Fact]
    public void AddSrcRefusesAnUnwrittenMediaWordInsteadOfDefaultingIt_C30_1d()
    {
        // pbi+0x1DC/+0x1E0 are written by the unread 0xA1EC54; AddSrc reads them before calling 0xA56650 and does not invent values.
        var rig = new AddSrcRig();
        rig.Pbi.Word1DC = null;
        Assert.Throws<WwiseMissingBehaviourException>(() => rig.Bridge.AddSrc(rig.NewVoice(), rig.Pbi, bActive: true));
        Assert.Empty(rig.Source.Calls);

        var rig2 = new AddSrcRig();
        rig2.Pbi.Word1E0 = null;
        Assert.Throws<WwiseMissingBehaviourException>(() => rig2.Bridge.AddSrc(rig2.NewVoice(), rig2.Pbi, bActive: true));
        Assert.Empty(rig2.Source.Calls);
    }

    [Fact]
    public void AddSrcSeamsForUnreadBodiesAreRequiredAndThrowWhenReachedUnset_C27Residuals()
    {
        // C27 residuals: the bodies of 0x9BCA68, 0xA0228C, 0x9EEDA4, 0xA054D8 and the 0x4C allocation, the source destructor
        // and pool free, and the [pbi+4] bit are not read, so reaching one unset throws rather than defaulting.
        var rig = new AddSrcRig();
        rig.Bridge.NextSource9EEDA4 = null;
        Assert.Throws<WwiseMissingBehaviourException>(() => rig.Bridge.AddSrc(rig.NewVoice(), rig.Pbi, bActive: true));

        rig = new AddSrcRig();
        rig.Bridge.NewVoiceAllocSendTable4C = null;
        Assert.Throws<WwiseMissingBehaviourException>(() => rig.Bridge.AddSrc(rig.NewVoice(), rig.Pbi, bActive: true));

        rig = new AddSrcRig { Eda = (2, 0) };
        rig.Bridge.Call9BCA68 = null;
        Assert.Throws<WwiseMissingBehaviourException>(() => rig.Bridge.AddSrc(rig.NewVoice(), rig.Pbi, bActive: true));

        rig = new AddSrcRig { Eda = (2, 0), Gate = 1 };
        rig.Bridge.CallA0228C = null;
        Assert.Throws<WwiseMissingBehaviourException>(() => rig.Bridge.AddSrc(rig.NewVoice(), rig.Pbi, bActive: true));

        rig = new AddSrcRig(startCode: 7);
        rig.Bridge.PbiFlag4Bit100000 = null;
        Assert.Throws<WwiseMissingBehaviourException>(() => rig.Bridge.AddSrc(rig.NewVoice(), rig.Pbi, bActive: true));

        rig = new AddSrcRig(startCode: 7) { Flag100000 = true };
        rig.Bridge.CallA054D8 = null;
        Assert.Throws<WwiseMissingBehaviourException>(() => rig.Bridge.AddSrc(rig.NewVoice(), rig.Pbi, bActive: true));

        rig = new AddSrcRig(startCode: 7) { Flag100000 = true };
        rig.Bridge.A054D8Context = null;                                     // the *global argument of 0xA054D8
        Assert.Throws<WwiseMissingBehaviourException>(() => rig.Bridge.AddSrc(rig.NewVoice(), rig.Pbi, bActive: true));

        rig = new AddSrcRig(startCode: 7);
        rig.Bridge.SourceDestructAndPoolFree = null;
        Assert.Throws<WwiseMissingBehaviourException>(() => rig.Bridge.AddSrc(rig.NewVoice(), rig.Pbi, bActive: true));
    }

    [Fact]
    public void AddSrcTheSourceFormatWriterOfPbiPlus0x15CIsARequiredSeamCalledAfterStartStream()
    {
        // C26.5 (unresolved gap, cited by the code comment): the source StartStream classes overwrite pbi+0x15C..0x15F
        // (0xA72760..0xAB138C); those writers are not built, so AddSrc throws when StartStream ran and no writer is
        // supplied, and the writer's value replaces the ctor default 0x4101 (0xA00338..0xA00374). C27 does not place the
        // writer relative to AddSrc's other steps, so no ordering claim is made beyond "after StartStream returned true".
        var bridge = new WwisePlaybackBridge { SourceFactory = _ => new Src() }.WithTestSeams();
        var pbi = BridgePbi(bridge);
        bridge.SourceFormatWriter15C = null;
        Assert.Throws<WwiseMissingBehaviourException>(() => bridge.AddSrc(new WwiseLiveVoice(1, 8), pbi, bActive: true));

        bridge.SourceFormatWriter15C = (p, _) => p.Word15C = 0x00003102;
        Assert.Equal(1, bridge.AddSrc(new WwiseLiveVoice(1, 8), pbi, bActive: true));
        Assert.Equal(0x00003102u, pbi.Word15C);
    }

    // ------------------------------------------------------------------ pass 1 destroy path (C25.3)

    [Fact]
    public void Pass1UnlinksAndFreesABit5SentinelNodeAndRuns0xA01800WithTheNotification()
    {
        // C25.3: bit5 of pbi+0x1BC set and pbi+0x1F8 == -1 goes to 0x9D379C (from 0x9D36D4..0x9D36E0): the node is
        // unlinked and freed and bl 0xA01800(pbi, 1) runs: pbi+0x154 = 0, then 0xA38600(pbi, 4, 1, 0).
        var f = new Frame();
        var notes = new List<(WwisePlayingInstance, int, int, int)>();
        f.Bridge.Notify38600 = (p, a, b, c) => notes.Add((p, a, b, c));
        var pbi = Rig.Pbi();
        pbi.Flags1BC = 0x20;
        f.Node(pbi);
        var other = Rig.Pbi();
        f.Node(other, key: 2);
        f.Bridge.SourceFactory = _ => null;                                  // the other node's attach fails: freed too
        f.Bridge.LinkEngineA548B8 = _ => { };

        Assert.Equal(0, f.Bridge.RunStartListPass1());

        // The destroyed node notifies (C25.3); the other node's attach finds no source, so AddSrc runs 0xA01800(pbi, 1) too
        // and returns 2 (C26.4, 0xA559A4..0xA559B8).
        Assert.Equal(new[] { (pbi, 4, 1, 0), (other, 4, 1, 0) }, notes);
        Assert.Null(pbi.Field154);
        Assert.Empty(f.Bridge.StartList.Nodes);
    }

    [Fact]
    public void Pass1TheNotificationSeamIsRequiredWhenADestroyedNodeReachesIt()
    {
        var f = new Frame();
        var pbi = Rig.Pbi();
        pbi.Flags1BC = 0x20;
        f.Node(pbi);
        Assert.Throws<WwiseMissingBehaviourException>(() => f.Bridge.RunStartListPass1());
        Assert.Empty(f.Bridge.StartList.Nodes);                             // C26.6: 0xA01800 runs after the node is freed (0x9D37A0..0x9D3814)
    }

    [Fact]
    public void Pass1SkipsANodeWhoseVoiceExistsBeforeTheBit5Check()
    {
        // 0x9D36B4/0x9D36BC: pbi+0x154 != 0 is skipped and stays in the list, even with bit5 and the sentinel.
        var f = new Frame();
        var (_, pbi) = f.Pending(new Src());
        pbi.Flags1BC = 0x20;
        f.Node(pbi);
        f.Bridge.Notify38600 = (_, _, _, _) => throw new InvalidOperationException("must not notify");
        Assert.Equal(0, f.Bridge.RunStartListPass1());
        Assert.Single(f.Bridge.StartList.Nodes);
    }

    [Fact]
    public void Pass20xA54480ATwoStateVoiceIsNotStartedAndAnyOtherStateStops()
    {
        // C24.7 / research 5.14: [voice+0xDC] == 0 starts the source; 2 returns; else voice vt+0x48.
        var f = new Frame();
        var started = new List<WwiseLiveVoice>();
        int stops = 0;
        f.Bridge.StartSourceA56478 = started.Add;
        var (two, p2) = f.Pending(new Src(), state: 2);
        two.VoiceStop48 = () => stops++;
        f.Node(p2, 0).D = 1;
        var (one, p1) = f.Pending(new Src(), state: 1);
        one.VoiceStop48 = () => stops++;
        f.Node(p1, 0, key: 2).D = 1;

        f.Bridge.RunStartListPass2();

        Assert.Empty(started);
        Assert.Equal(1, stops);                                             // only the state-1 voice hits vt+0x48
        Assert.Equal(2, two.State);
    }

    [Fact]
    public void AVoiceParkedOn0x3FBecomesLiveWhenItsSourceSucceedsAndIsStartedInTheSameCall()
    {
        // C24.7 / research 6.6: AddSrc 0x3F -> pass 1 returns 1 with the voice pending and the flag clear; pass 2
        // calls 0xA431A8 in the same call (still 0x3F: nothing more). When the source later succeeds, 0xA431A8
        // tail-calls 0xA42DEC, the flag is set and 0xA54480 starts the source in that same 0x9D3C98.
        var src = new Src { Code = 0x3F };
        var bridge = new WwisePlaybackBridge { SourceFactory = _ => src }.WithTestSeams();
        var rig = new Rig(mainDevice: false);
        var started = new List<WwiseLiveVoice>();
        bridge.Linker = new WwiseVoiceLinker(rig.Buses, rig.Devices, bridge.Voices, _ => rig.Routing, rig.Seams);
        bridge.LinkEngineA548B8 = _ => { };                                 // 0xA548B8 does not touch +0xDC (C25.1)
        bridge.StartSourceA56478 = started.Add;
        var pbi = bridge.CreatePbiWithMediaWords(new WwisePlayInitParams { PlayingId = 1, TargetNodeId = 1 }, 1,
            new WwiseSourceDescriptor(WwiseSourceFactory.AdpcmPlugin, 1, 1, 0, 0), continuous: false);
        bridge.StartList.Enqueue(0, pbi, 1);

        Assert.Equal(1, bridge.DrainStartList());
        var voice = Assert.Single(bridge.Linker.PendingVoices);
        Assert.Empty(bridge.Voices);
        Assert.Empty(started);
        Assert.Single(bridge.StartList.Nodes);

        src.Code = 1;                                                       // the stream can now start
        Assert.Equal(0, bridge.DrainStartList());                           // gate clear: pass 2 only
        Assert.Empty(bridge.Linker.PendingVoices);
        Assert.Contains(voice, bridge.Voices);
        Assert.Equal(new[] { voice }, started);
        Assert.Empty(bridge.StartList.Nodes);
    }

    [Fact]
    public void ThePendingWalkFreesTheStartNodesAndDestroysAVoiceWithBit5AndTheSentinel()
    {
        // C24.1 0x9D3CC0: pbi+0x1BC bit5 and pbi+0x1F8 == -1 -> start nodes of that PBI freed, voice unlinked,
        // 0x9D40C4(voice, 0).
        var f = new Frame();
        var (voice, pbi) = f.Pending(new Src { Code = 0x3F });
        pbi.Flags1BC = 0x20;
        f.Node(pbi);
        var (other, otherPbi) = f.Pending(new Src { Code = 0x3F });
        var keep = f.Node(otherPbi, key: 2);

        f.Bridge.WalkPendingVoices();

        Assert.Same(keep, Assert.Single(f.Bridge.StartList.Nodes));
        Assert.DoesNotContain(voice, f.Bridge.Linker!.PendingVoices);
        Assert.Contains(other, f.Bridge.Linker.PendingVoices);
        Assert.Equal(new[] { "close" }, f.Calls);                           // TeardownVoice closed the source
        Assert.Null(voice.Source);
    }

    [Theory]
    [InlineData(5000u, 1.0f, (ushort)0x400, 3976u)]                         // k = round(1024 * 1.0)
    [InlineData(5u, 0.5f, (ushort)3, 3u)]                                   // 1.5 + 0.5 = 2.0 -> 2
    [InlineData(5u, 0.5f, (ushort)0, 5u)]                                   // product 0: -0.5 truncates to 0
    [InlineData(100000u, 0.5f, (ushort)0xFFFF, 67232u)]                     // unsigned: 65535 * 0.5 = 32767.5 + 0.5 -> 32768
    public void ThePendingWalkSubtractsTheRoundedFrameProductFromTheStartOffset(uint start, float ratio, ushort frames, uint expected)
    {
        // C24.1: pbi+0x1D8 -= round(frames * [pbi+0x164]) (+0.5, or -0.5 for a product of 0 or less, then truncate),
        // when bit7 of pbi+0x1BC is clear and pbi+0x1D8 >= 0 (0x9D3D30..0x9D3D68). C25.7: frames is the u16 at
        // [0x1052440] converted unsigned (vcvt.f32.u32), the same global as the line Init's frames.
        var f = new Frame();
        f.Bridge.Linker!.LineMaxFrames = frames;
        var (_, pbi) = f.Pending(new Src { Code = 0x3F });
        pbi.StartOffset = start;
        pbi.Ratio = ratio;
        f.Bridge.WalkPendingVoices();
        Assert.Equal(expected, pbi.StartOffset);
    }

    [Fact]
    public void ThePendingWalkTakesTheDefault0x400FromTheSameGlobalAsTheLineInit()
    {
        // C25.7: [0x1052440] .data = 0x400 is both the line Init's frames and the walk's.
        var f = new Frame();
        Assert.Equal((ushort)0x400, f.Bridge.Linker!.LineMaxFrames);
        var (_, pbi) = f.Pending(new Src { Code = 0x3F });
        pbi.StartOffset = 0x400 + 7;
        f.Bridge.WalkPendingVoices();                                       // ratio 1.0 (0xA00228): 1024
        Assert.Equal(7u, pbi.StartOffset);
    }

    [Fact]
    public void ThePendingWalkLeavesTheOffsetWhenBit7IsSetOrItIsNegative()
    {
        // C24.1: bit7 of pbi+0x1BC (set by the fade-in path 0xA006E0..0xA006EC) skips the decrement; so does a
        // negative pbi+0x1D8 (0x9D3D34).
        var f = new Frame();
        var (_, faded) = f.Pending(new Src { Code = 0x3F });
        faded.StartOffset = 5000;
        faded.Flags1BC = 0x80;
        var (_, negative) = f.Pending(new Src { Code = 0x3F });
        negative.StartOffset = 0xFFFFFF00;
        f.Bridge.WalkPendingVoices();
        Assert.Equal(5000u, faded.StartOffset);
        Assert.Equal(0xFFFFFF00u, negative.StartOffset);
    }

    [Fact]
    public void ThePendingWalkRunsAtTheVoicePassPrePassPosition()
    {
        // C24.1: 0x9D3CC0 is called at 0xA44978 in the voice pass (V5, before 0xA43D24 and 0xA39564); it is the pass's
        // AdvanceTickCounters hook.
        var f = new Frame();
        var (_, pbi) = f.Pending(new Src { Code = 0x3F });
        pbi.StartOffset = 3000;
        var pass = new WwiseVoiceBusPass(new WwiseMixBusHierarchy(), new WwiseOutputDeviceState());
        pass.AdvanceTickCounters = () => f.Bridge.WalkPendingVoices();
        pass.DuckPrePass = () => f.Calls.Add($"duck@{pbi.StartOffset}");
        pass.NodeCleanup = () => f.Calls.Add("cleanup");
        pass.VoicePass();
        Assert.Equal(new[] { "duck@1976", "cleanup" }, f.Calls);            // the walk ran first (0xA44978), then 0xA43D24, then 0xA39564
    }

    [Fact]
    public void ASetBusVolumeCoversTheAddSrcVoicePlus8AsThePbi()
    {
        // C24 header: [voice+8] = pbi+0xC, so the pending walk reads the PBI through it (0xA55934..0xA55948).
        var bridge = new WwisePlaybackBridge { SourceFactory = _ => new Src { Code = 0x3F } }.WithTestSeams();
        var pbi = bridge.CreatePbiWithMediaWords(new WwisePlayInitParams { PlayingId = 1, TargetNodeId = 1 }, 1,
            new WwiseSourceDescriptor(WwiseSourceFactory.AdpcmPlugin, 1, 1, 0, 0), continuous: false);
        var voice = Rig.Voice();
        bridge.AddSrc(voice, pbi, bActive: true);
        Assert.Same(pbi, voice.BusOwner8);
    }

    [Fact]
    public void AddSrcTheReusePathStoresOnlyVoicePlus0xD8AndLeavesVoicePlus8AndBit3()
    {
        // C25.4: voice+8 = [source+0xC]+0xC and the pbi+0x1BE bit3 clear are reached only on the new-voice path
        // (r2 != 0); the reuse path stores [voice+0xD8] and returns at once (0xA5592C streq r5,[r4,#0xd8]; beq 0xA55958).
        var reuseSrc = new Src();
        var bridge = new WwisePlaybackBridge { SourceFactory = _ => reuseSrc }.WithTestSeams();
        var pbi = bridge.CreatePbiWithMediaWords(new WwisePlayInitParams { PlayingId = 1, TargetNodeId = 1, ChainId = 0x55 }, 1,
            new WwiseSourceDescriptor(WwiseSourceFactory.AdpcmPlugin, 1, 1, 0, 0), continuous: false);
        Assert.Equal(8, pbi.Flags1BE & 8);                                  // set: the chain id came from params+0x7C
        var owner = Rig.Pbi();
        var voice = Rig.Voice();
        voice.BusOwner8 = owner;

        Assert.Equal(1, bridge.AddSrc(voice, pbi, bActive: false));

        Assert.Same(reuseSrc, voice.Pending);                               // voice+0xD8
        Assert.Null(voice.Source);                                          // voice+0xD4 untouched
        Assert.Same(owner, voice.BusOwner8);                                // voice+8 untouched
        Assert.Equal(8, pbi.Flags1BE & 8);                                  // bit3 not cleared

        Assert.Equal(1, bridge.AddSrc(voice, pbi, bActive: true));          // the new-voice path clears it (0xA5594C)
        Assert.Same(pbi, voice.BusOwner8);
        Assert.Equal(0, pbi.Flags1BE & 8);
    }

    // ------------------------------------------------------------------ teardown (rows 5.15, 5.17, 5.19)

    [Fact]
    public void TeardownClosesTheSourcesSetsBit0AndDestroysEveryConnectionHeadFirst()
    {
        // Row 5.17: Term closes [voice+0xD4], the four insert-FX slots get vt+0x2C, voice+0xCD |= 1, then the
        // pending source [voice+0xD8] gets the same close. Row 5.19: the [voice+0x10] array is freed and every
        // connection destroyed (unlink, count--, destructor 0xA4F6F0: vpl+0x1C0--).
        var rig = new Rig();
        rig.Routing = new WwisePbiRouting { Node = SoundUnder(Master()) };
        var closed = new List<IWwiseVoiceSource>();
        rig.Seams.CloseSource56414 = closed.Add;
        var voice = new WwiseLiveVoice(1, 8) { FlagsCD = 0, Source = new Src(), Pending = new Src(), SendTable = new WwiseVoiceSendTable() };
        var slot = new WwiseVoiceInsertFxSlot();
        int slotTerms = 0;
        slot.TeardownHook = () => slotTerms++;
        voice.InsertFxSlots[2] = slot;
        var current = voice.Source;
        var pending = voice.Pending;
        rig.Linker.Link(voice, Rig.Pbi());
        var line = voice.Connections[0].Bus;
        Assert.Equal(1, line.Connections);

        rig.Linker.TeardownVoice(voice);

        Assert.Equal(new[] { current, pending }, closed);
        Assert.Null(voice.Source);
        Assert.Null(voice.Pending);
        Assert.Equal(1, slotTerms);
        Assert.Null(voice.InsertFxSlots[2]);
        Assert.Equal(1, voice.FlagsCD & 1);
        Assert.Null(voice.SendTable);
        Assert.Empty(voice.Connections);
        Assert.Equal(0, line.Connections);
    }

    [Fact]
    public void TheSourceCloseBodyIsRefusedWhenItsSeamIsMissing()
    {
        // Row 5.18: 0xA56414's PBI side (0xA054D8, 0xA01800) is behind a seam.
        var rig = new Rig();
        var voice = Rig.Voice();
        voice.Source = new Src();
        Assert.Throws<NotSupportedException>(() => rig.Linker.TeardownVoice(voice));
    }
}
