// fidelity: M6-022
using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M6-022 C41 (V7 on the owner PBI, the PBI ctor's initial values, the in-place FX wrapper). The V7 state machine itself is checked against the engine in <see cref="WwiseVoiceStateOracleTests"/>; these are the rows with hand values from the inventory's
/// citations and the live-entry tests of the FX build.
/// </summary>
public sealed class WwiseVoiceStateTests
{
    private static WwisePlayingInstance Pbi(bool continuous = false, uint delay = 0)
        => new(new WwisePlayInitParams { PlayingId = 7, TargetNodeId = 1, InitialDelaySamples = delay }, 1, new object(), new byte[0x44], null, continuous);

    [Fact]
    public void M6_022_D5_the_pbi_ctor_stores_the_budget_the_media_pair_and_the_stop_offset()
    {
        // Verification D5: [pbi+0x1D8] = [ctor-arg+0x74] (0xA002D8 / 0xA002EC), [pbi+0x1DC] / [pbi+0x1E0] zeroed (0xA002F0 / 0xA002F4), [pbi+0x1F8] = -1 (0xA00318).
        var pbi = Pbi(delay: 4800);
        Assert.Equal(4800u, pbi.StartOffset);
        Assert.Equal(0u, pbi.Word1DC);
        Assert.Equal(0u, pbi.Word1E0);
        Assert.Equal(0xFFFFFFFFu, pbi.Field1F8);
    }

    [Theory]
    [InlineData(1024, 1.0f, 1024)]
    [InlineData(5, 0.5f, 3)]            // 2.5 + 0.5 = 3.0: half away from zero (half-to-even would give 2)
    [InlineData(7, 0.5f, 4)]            // 3.5 -> 4
    [InlineData(0, 1.0f, 0)]            // 0 is not above 0: -0.5 -> 0 (truncation toward zero)
    [InlineData(5, -0.5f, -3)]          // -2.5 - 0.5 = -3.0
    [InlineData(3, 0.1f, 0)]            // 0.3 + 0.5 = 0.8 -> 0
    public void M6_022_1_7_the_scaled_frames_round_half_away_from_zero_in_single_precision(int frames, float ratio, int expected)
    {
        // Row 1.7 (0xA55090..0xA550C4): vcvt.f32.u32, vmul.f32 by [pbi+0x164], +0.5f when above 0 else -0.5f, vcvt.s32.f32.
        Assert.Equal(expected, WwiseVoiceBusPass.ScaledFramesA55090((ushort)frames, ratio));
        Assert.Equal(0, WwiseVoiceBusPass.ScaledFramesA55090(1, float.NaN));
    }

    [Fact]
    public void M6_022_D1_the_source_vt_4C_getter_is_bit_6_of_the_owners_1BE()
    {
        var pbi = Pbi();
        pbi.Flags1BE = 0x3F;
        Assert.Equal(0, WwiseVoiceBusPass.SourceVt4C(pbi));
        pbi.Flags1BE = 0x40;
        Assert.Equal(1, WwiseVoiceBusPass.SourceVt4C(pbi));
        pbi.Flags1BE = 0xBF;
        Assert.Equal(0, WwiseVoiceBusPass.SourceVt4C(pbi));
    }

    [Fact]
    public void M6_022_C41_3_pbi_vt_3C_is_class_specific_and_the_unread_class_is_a_visible_stop()
    {
        // Base / Sound PBI (0x103B768) and the 0xA6A8A0 class (0x103D3B0): 0x9FF544 returns 0. The 0x9883AC class (0x1039D98): 0x9882E0 -> 0x99CC40 (unread).
        var sound = Pbi();
        Assert.Equal(0, sound.RequestVt3C(1));
        Assert.Equal(0, sound.RequestVt3C(2));
        sound.PbiClass = WwisePbiClass.Class103D3B0;
        Assert.Equal(0, sound.RequestVt3C(1));

        var container = Pbi(continuous: true);
        Assert.Equal(WwisePbiClass.Container9883AC, container.PbiClass);
        Assert.Throws<WwiseMissingBehaviourException>(() => container.RequestVt3C(1));
        container.Seam99CC40 = _ => (0, 5, 6);
        Assert.Equal(2, container.RequestVt3C(1));                  // 0x9882E0: 0 -> returns 2, nothing stored
        Assert.Equal(0u, container.Word1B4);
        container.Flags1BE = 0xFF;
        container.Seam99CC40 = _ => (1, 5, 6);
        Assert.Equal(1, container.RequestVt3C(1));
        Assert.Equal(5u, container.StartOffset);
        Assert.Equal(6u, container.Word1B4);
        Assert.Equal(0xFC, container.Flags1BE);
        Assert.Equal(0x80, container.Flags1BD & 0x80);
    }

    // ------------------------------------------------------------------ the in-place FX wrapper (C41.4)

    private sealed class FakePlugin : IWwiseEffectPlugin
    {
        public List<string> Calls { get; } = new();
        public WwisePluginInfo Info { get; set; } = new(3, 0x7E002, 1, 0);
        public int InitResult { get; set; } = 1;
        public object? InitCtx, InitParams;
        public int Term(IWwisePluginMemAlloc alloc) { Calls.Add("Term"); return 1; }
        public int Reset() { Calls.Add("Reset"); return 1; }
        public int GetPluginInfo(out WwisePluginInfo info) { info = Info; return 1; }
        public int Slot14() => 0;
        public int Slot18() => 0;
        public int Init(IWwisePluginMemAlloc alloc, object? ctx, object? parameters, WwiseEffectFormat fmt) { Calls.Add("Init"); InitCtx = ctx; InitParams = parameters; return InitResult; }
        public void Execute(WwiseDecodeState state) { Calls.Add($"Execute:{state.Scratch08:x}"); state.Code28 = 0x77; state.ValidFrames = state.MaxFrames; }
        public int Slot24() { Calls.Add("TimeSkip"); return 0x2D; }
    }

    private sealed class StubSource : IWwiseVoiceSource
    {
        public int Channels => 1;
        public int SampleRate => 48000;
        public int Render(WwiseVoiceBuffer buffer) => 0x2D;
        public int StartStream(uint a, uint b) => 1;
        public bool StartStreamSucceeded { get; set; } = true;
    }

    private static (WwiseLiveVoice Voice, FakePlugin Plugin, WwiseVoiceFxDescriptor Fx) Rig(WwisePlayingInstance pbi, bool resolve = true)
    {
        var plugin = new FakePlugin();
        var fx = new WwiseVoiceFxDescriptor { Id = 0x006C0003, CloneParams = _ => new object(), DestroyParams = (_, _) => { } };
        var voice = new WwiseLiveVoice(1, 8)
        {
            Source = new StubSource(),
            BusOwner8 = pbi,
            StartResampler5321C = () => true,
            ResolveNodeFx9EEF2C = i => { if (i != 0 || !resolve) return (null, (byte)0); fx.AddRef(); return (fx, (byte)0); }, GainNode380Vt24 = _ => { },   // each resolve hands out a fresh reference (the descriptor table keeps its own: RefCount starts at 1)
            PluginRegistry9CC2AC = (id, _) => id == 0x006C0003 ? plugin : null,
        };
        return (voice, plugin, fx);
    }

    [Fact]
    public void M6_022_4_3_a_slot_is_built_in_place_by_info_byte_8_and_init_runs_before_reset()
    {
        // Rows 4.1, 4.3: byte [info+8] != 0 -> the 0x34-byte wrapper (0x103DB98); 0xA792B0: the holder (0xA793D4), the plug-in's Init (alloc, [W+0xC], [W+0x14], fmt), then Reset.
        var (voice, plugin, fx) = Rig(Pbi());
        Assert.Equal(1, WwiseVoiceBusPass.StartStreamAndBuildInsertFx(voice));

        var slot = voice.InsertFxSlots[0];
        Assert.NotNull(slot);
        Assert.Null(voice.InsertFxSlots[1]);
        Assert.Equal(new[] { "Init", "Reset" }, plugin.Calls);
        Assert.Same(plugin, slot.Plugin);
        Assert.Same(slot.Context, plugin.InitCtx);
        Assert.Same(slot.Params, plugin.InitParams);
        Assert.Equal(0, slot.Context!.Index);
        Assert.Equal(0x006C0003u, slot.PluginId);
        Assert.Equal(0x4101u, slot.Vt44());                          // [W+0x30] = [fmt+4]
        Assert.Equal(2, fx.RefCount);                                // the table's own plus the holder's (0x9CF644's AddRef); the caller's release (0xA54E88) and vt+0x70's (0xA533E8) balanced their resolves
        Assert.Equal(0x103DB98u, WwiseVoiceInsertFxSlot.Vtable);
        Assert.Equal(0x34, WwiseVoiceInsertFxSlot.Size);
    }

    [Fact]
    public void M6_022_4_3_a_failed_init_leaves_the_slot_null_and_tears_the_wrapper_down()
    {
        var (voice, plugin, fx) = Rig(Pbi());
        plugin.InitResult = 2;
        WwiseVoiceBusPass.StartStreamAndBuildInsertFx(voice);

        Assert.Null(voice.InsertFxSlots[0]);                         // there is no "no plug-in" form
        Assert.Equal(new[] { "Init", "Term" }, plugin.Calls);        // W->vt+0x2C: the plug-in's Term (0xA79088)
        Assert.Equal(1, fx.RefCount);                                // the holder's and the caller's references are both released
    }

    [Fact]
    public void M6_022_4_3_a_wrapper_allocation_failure_returns_2_from_the_whole_build_and_skips_the_later_slots()
    {
        // Binary 0xA54BE8 / 0xA54DFC -> 0xA54EC8..0xA54EFC (plug-in vt+8, release the descriptor) -> 0xA54A88: the whole 0xA54A30 returns 2 (C24.2).
        var (voice, plugin, fx) = Rig(Pbi());
        var asked = new List<int>();
        voice.ResolveNodeFx9EEF2C = i => { asked.Add(i); fx.AddRef(); return (fx, (byte)0); };
        voice.FxAllocationFails = () => true;
        Assert.Equal(2, WwiseVoiceBusPass.StartStreamAndBuildInsertFx(voice));
        Assert.Equal(new[] { 0 }, asked);                            // the later slots were not tried
        Assert.Null(voice.InsertFxSlots[0]);
        Assert.Equal(new[] { "Term" }, plugin.Calls);
        Assert.Equal(1, fx.RefCount);
        Assert.Equal(0u, voice.Word0xF0);                            // 0xA54B70 was not reached
    }

    [Fact]
    public void M6_022_4_1_an_init_failure_goes_on_to_the_next_slot_and_a_rejected_or_unregistered_plugin_is_released()
    {
        var pbi = Pbi();
        var (voice, plugin, fx) = Rig(pbi);
        var fx2 = new WwiseVoiceFxDescriptor { Id = 0x006C0003, CloneParams = _ => new object() };
        voice.ResolveNodeFx9EEF2C = i => { var d = i == 0 ? fx : i == 1 ? fx2 : null; d?.AddRef(); return (d, (byte)0); };
        plugin.InitResult = 2;                                       // slot 0 fails Init (0xA54CC4 -> 0xA54B38); slot 1 is tried
        Assert.Equal(1, WwiseVoiceBusPass.StartStreamAndBuildInsertFx(voice));
        Assert.Null(voice.InsertFxSlots[0]);
        Assert.Equal(new[] { "Init", "Term", "Init", "Term" }, plugin.Calls);
        Assert.Equal(1, fx.RefCount);
        Assert.Equal(1, fx2.RefCount);

        var (v2, p2, f2) = Rig(Pbi());
        p2.Info = new WwisePluginInfo(3, 0x7E002, 1, 1);             // byte [info+0xA] != 0: 0xA54D08 vt+8 then release, next slot
        Assert.Equal(1, WwiseVoiceBusPass.StartStreamAndBuildInsertFx(v2));
        Assert.Equal(new[] { "Term" }, p2.Calls);
        Assert.Equal(1, f2.RefCount);
        Assert.Null(v2.InsertFxSlots[0]);

        var (v3, _, f3) = Rig(Pbi());
        v3.PluginRegistry9CC2AC = (_, _) => null;                    // 0x9CC2AC != 1: release the descriptor, next slot
        Assert.Equal(1, WwiseVoiceBusPass.StartStreamAndBuildInsertFx(v3));
        Assert.Equal(1, f3.RefCount);
    }

    [Fact]
    public void M6_022_4_1_the_out_of_place_class_and_the_rtpc_records_are_visible_stops()
    {
        var (voice, plugin, _) = Rig(Pbi());
        plugin.Info = new WwisePluginInfo(3, 0x7E002, 0, 0);          // byte [info+8] == 0: the 0x103DC38 class, not extracted
        Assert.Throws<WwiseMissingBehaviourException>(() => WwiseVoiceBusPass.StartStreamAndBuildInsertFx(voice));
        var (v4, _, fx4) = Rig(Pbi());
        fx4.RtpcRecordCount = 1;                                      // 0x9CF644's record loops are unread
        Assert.Throws<WwiseMissingBehaviourException>(() => WwiseVoiceBusPass.StartStreamAndBuildInsertFx(v4));
    }

    [Fact]
    public void M6_022_4_7_the_process_step_allocates_saves_the_result_and_restores_it_after_the_plugin()
    {
        // Row 4.7 (0xA791A8): [S] == 0 -> a buffer of u16[S+0xC] * byte[S+4] * 4 bytes, u16[S+0xE] = 0; [S+8] = [S+0x28]; plugin vt+0x20(S); [S+0x28] = [S+8].
        var pbi = Pbi();
        var (voice, plugin, _) = Rig(pbi);
        WwiseVoiceBusPass.StartStreamAndBuildInsertFx(voice);
        var slot = voice.InsertFxSlots[0]!;
        var buf = new WwiseVoiceBuffer(1, 8);
        buf.State.MaxFrames = 4; buf.State.ChannelConfig = 0x4102; buf.State.Code28 = 0x2D; buf.State.Data = null;
        plugin.Calls.Clear();

        slot.Execute3C(buf);

        Assert.Equal(8, ((float[])buf.State.Data!).Length);          // 4 frames * 2 channels
        Assert.Same(buf.State.Data, slot.Buffer);
        Assert.Equal(new[] { "Execute:2d" }, plugin.Calls);          // [S+8] held the result during the call
        Assert.Equal(0x2D, buf.State.Code28);                        // restored from [S+8]
        Assert.Equal(4, buf.State.ValidFrames);                      // the fake plug-in's own write survives

        // The release (vt+0xC, 0xA7915C): a wrapper holding its buffer frees it and stops the walk.
        Assert.False(slot.ReleaseVtC());
        Assert.Null(slot.Buffer);
        Assert.True(slot.ReleaseVtC());
    }

    [Fact]
    public void M6_022_4_7_a_result_of_0x11_marks_done_and_vt_38_then_reports_0x11_through_the_process_step()
    {
        var (voice, plugin, _) = Rig(Pbi());
        WwiseVoiceBusPass.StartStreamAndBuildInsertFx(voice);
        var slot = voice.InsertFxSlots[0]!;
        var buf = new WwiseVoiceBuffer(1, 8);
        buf.State.MaxFrames = 2; buf.State.ChannelConfig = 1; buf.State.Data = new float[2];
        buf.State.Code28 = 0x2B;
        slot.Execute38(buf);                                          // 0xA790E8: not done: nothing runs
        Assert.Equal(0x2B, buf.State.Code28);
        buf.State.Code28 = 0x11;
        slot.Execute3C(buf);                                          // r3 == 0x11 -> [W+0x20] = 1
        Assert.Equal(1, slot.Done);
        plugin.Calls.Clear();
        buf.State.Code28 = 0x2B;
        slot.Execute38(buf);                                          // done: [S+0x28] = 0x11 then vt+0x3C
        Assert.Equal(new[] { "Execute:11" }, plugin.Calls);
        Assert.Equal(0x11, buf.State.Code28);
        Assert.Equal(0x11, slot.Vt10(0));                             // 0xA79108: done returns 0x11
        Assert.Equal(1, slot.Vt18(1, 1));                             // 0xA793B0: done returns 1
    }

    [Fact]
    public void M6_022_4_7_a_bypassed_slot_resets_once_and_leaves_the_audio_alone()
    {
        var pbi = Pbi();
        var (voice, plugin, _) = Rig(pbi);
        WwiseVoiceBusPass.StartStreamAndBuildInsertFx(voice);
        var slot = voice.InsertFxSlots[0]!;
        slot.Bypass = 1;
        var buf = new WwiseVoiceBuffer(1, 8);
        buf.State.MaxFrames = 2; buf.State.ChannelConfig = 1; buf.State.Code28 = 0x2D;
        plugin.Calls.Clear();
        slot.Execute3C(buf); slot.Execute3C(buf);
        Assert.Equal(new[] { "Reset" }, plugin.Calls);                // [W+0x22] makes the second pass silent
        Assert.Null(buf.State.Data);
        slot.Bypass = 0; pbi.Byte97 = 1;                              // byte [[[W+8]+8]+0x8B] = [pbi+0x97]
        plugin.Calls.Clear();
        slot.Execute3C(buf);
        Assert.Empty(plugin.Calls);                                   // [W+0x22] is still 1
    }

    [Fact]
    public void M6_022_4_6_the_term_sequence_terms_the_plugin_destroys_the_clone_and_releases_the_descriptor()
    {
        var (voice, plugin, fx) = Rig(Pbi());
        object? destroyed = null;
        fx.DestroyParams = (p, _) => destroyed = p;
        WwiseVoiceBusPass.StartStreamAndBuildInsertFx(voice);
        var slot = voice.InsertFxSlots[0]!;
        var clone = slot.Params;
        plugin.Calls.Clear();

        slot.Teardown();                                              // 0xA7933C: vt+0x30 (0xA79088) then 0xA79488

        Assert.Equal(new[] { "Term" }, plugin.Calls);
        Assert.Null(slot.Plugin);
        Assert.Same(clone, destroyed);
        Assert.Equal(1, fx.RefCount);
        Assert.Null(slot.Context);
    }

    // ------------------------------------------------------------------ V7 through the live entry (VoicePass)

    [Fact]
    public void M6_022_C41_2_the_voice_pass_runs_the_pre_pass_then_V7_on_the_owner_and_V7_builds_the_slot()
    {
        // The entry the live path uses: WwiseVoiceBusPass.VoicePass -> 0xA43D24 pre-pass (0xA55750: CalcEffectiveParams when [pbi+0xE8]&0x20 is clear) -> V7 0xA54F1C (its tail runs 0xA54A30 -> the wrapper's Init, then CalcEffectiveParams again).
        var pbi = Pbi();
        var (voice, plugin, _) = Rig(pbi);
        var log = new List<string>();
        var pass = new WwiseVoiceBusPass(new WwiseMixBusHierarchy(), new WwiseOutputDeviceState())
        {
            SourceOwner = _ => pbi,
            AdvanceTickCounters = () => { },
            NodeCleanup = () => { },
            DuckPrePassTailA43D6C = () => { },
            CalcEffectiveParamsVt24 = _ => log.Add("calc"),
            VoiceRefreshTailA4B9BC = _ => { },
            PostMixA5495C = _ => { },
            PostMixNoDataReadyA55CC4 = (_, _) => { },
        };
        voice.AllowRenderOrderApproximation = true;
        voice.StartResampler5321C = () => { log.Add("resampler"); return true; };
        pbi.Flags0E8 = 0x5D;                                          // bit 5 clear
        pbi.StartOffset = 0xFFFFFFFF;                                 // no budget: r5 stays 1
        pass.Voices.Add(voice);

        pass.VoicePass(1);

        Assert.Equal(new[] { "calc", "resampler", "calc" }, log.Take(3).ToArray());   // pre-pass, 0xA54A30's resampler start, the 0xA5561C tail's ctx vt+0x24
        Assert.Equal(new[] { "Init", "Reset" }, plugin.Calls.Take(2).ToArray());
        Assert.NotNull(voice.InsertFxSlots[0]);
        Assert.Equal(101f, pbi.FieldC4);                              // [pbi+0xC4] = 101.0f (0xA55620..0xA55628)
        Assert.NotEqual(0, voice.FlagsCD & 8);                      // [voice+0xCD] |= 8
    }

    [Fact]
    public void M6_022_C41_1_the_build_stores_voice_F0_and_V7_then_uses_it_through_the_live_entry()
    {
        // Binary 0xA54B60..0xA54B70: [voice+0xF0] = [pbi+0x15C]; filter A / B inits take it (0xA54B74, 0xA54D44). No StartStreamOverrideA54A30: the real build runs from V7 (0xA555C0).
        var pbi = Pbi();
        var (voice, plugin, _) = Rig(pbi);
        pbi.Word15C = 0x00003102;
        pbi.Flags0E8 = 0x5C;                                          // bit 5 clear as with 0x5D; bits 0-1 clear: the 2D path (the 3D branch of 0xA4BC58 is a required stop)
        pbi.StartOffset = 0xFFFFFFFF;
        pbi.Flags1BE = 0;
        voice.SendTable = new WwiseVoiceSendTable { Capacity = 1 };    // AddSrc's table: 0xA5975C reads entry 0 for a dry connection
        voice.SendTable.Entries.Add(new WwiseVoiceSendEntry());
        var conn = new WwiseVoiceConnection(new WwiseMixBus(default, Array.Empty<WwiseBusFxSlot>(), 8), 1, 1) { Flags6C = 0 };
        Assert.Equal(101f, pbi.FieldC4);                              // the ctx ctor's [P+0xB8] = 101.0f (0x9BCA48); the first 0xA4BC58 call (a zero low byte of [voice+0xF0]) skips the loop AND the B14 copy
        voice.Connections.Add(conn);
        var pass = new WwiseVoiceBusPass(new WwiseMixBusHierarchy(), new WwiseOutputDeviceState()) { SourceOwner = _ => pbi, CalcEffectiveParamsVt24 = _ => { } };
        Assert.Equal(0u, voice.Word0xF0);

        Assert.True(pass.RunVoiceStateMachine(voice));

        Assert.Equal(0x00003102u, voice.Word0xF0);
        Assert.Equal(0x00003102u, voice.FilterA.Word190);
        Assert.Equal(0x00003102u, voice.FilterB.Word190);
        Assert.Same(pbi, voice.Pbi388);                               // 0xA5676C
        Assert.Equal(0x00003102u, voice.Buffer.State.ChannelConfig);  // the tail's S+4 (0xA55634)
        Assert.Equal(0f, pbi.FieldC4);                                // the second 0xA4BC58 call's loop ran to the B14 tail (0xA4BFC4: [P+0xB8] <- [P+0xA8] = 0); the first call, before the build, had a zero word
        var buf = voice.Buffer;
        buf.State.Data = null; buf.State.MaxFrames = 8; buf.State.ChannelConfig = voice.Word0xF0;
        voice.InsertFxSlots[0]!.Execute3C(buf);                       // 0xA791A8: u16[S+0xC] * byte[S+4] * 4 bytes
        Assert.Equal(16, ((float[])buf.State.Data!).Length);
        Assert.Contains(plugin.Calls, c => c.StartsWith("Execute"));
    }

    [Fact]
    public void M6_022_C41_1_a_filter_init_failure_returns_its_result_from_the_build()
    {
        var pbi = Pbi();
        var (voice, _, _) = Rig(pbi);
        voice.FilterAllocationFails = () => true;                     // 0xA764D4 returns 2; 0xA54B78 returns it
        Assert.Equal(2, WwiseVoiceBusPass.StartStreamAndBuildInsertFx(voice));
        Assert.Equal(0x4101u, voice.Word0xF0);                        // 0xA54B70 ran before the filter init
        Assert.Null(voice.Pbi388);                                    // 0xA5676C was not reached
    }

    [Fact]
    public void M6_022_1_1_the_voice_pass_argument_is_anded_with_the_V7_result_before_the_render()
    {
        // 0xA44B54..0xA44B5C ldr r3,[sp,#4]; tst r0,r3; beq 0xA44BAC: with the argument 0 V7 still runs but the render and the post-mix are skipped. The argument is 0xA44DE0..0xA44DF4's (BusPassArg).
        WwiseVoiceBusPass Make(out List<string> log)
        {
            var pbi = Pbi();
            var (v, _, _) = Rig(pbi);
            var l = new List<string>();
            var pass = new WwiseVoiceBusPass(new WwiseMixBusHierarchy(), new WwiseOutputDeviceState())
            {
                SourceOwner = _ => pbi, AdvanceTickCounters = () => { }, NodeCleanup = () => { }, DuckPrePassTailA43D6C = () => { },
                CalcEffectiveParamsVt24 = _ => { }, VoiceRefreshTailA4B9BC = _ => { },
                PostMixA5495C = _ => l.Add("post"), PostMixNoDataReadyA55CC4 = (_, _) => l.Add("post"),
                StartStreamOverrideA54A30 = _ => { l.Add("v7"); return 1; },
            };
            v.AllowRenderOrderApproximation = true;
            pbi.StartOffset = 0xFFFFFFFF;
            pass.Voices.Add(v);
            log = l;
            return pass;
        }
        var off = Make(out var offLog);
        off.VoicePass(0);
        Assert.Equal(new[] { "v7" }, offLog);
        Assert.Equal(0, off.VoicesRendered);
        var on = Make(out var onLog);
        on.VoicePass(1);
        Assert.Equal(new[] { "v7", "post" }, onLog);
        Assert.Equal(1, on.VoicesRendered);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(1, false)]
    [InlineData(0, false)]
    public void M6_022_2_11_voice_vt_70_stores_the_resolvers_bypass_byte_even_for_a_null_descriptor_and_without_the_A019B8_gate(byte bypass, bool hasFx)
    {
        // 0xA5338C: node vt+0xE8 (0x9EEF2C) called directly; [W+0x21] = byte [out+4] stored before the null test (0xA533E0); no [pbi+0xE9] bit 2 gate (that is 0xA019B8's).
        var pbi = Pbi();
        var (voice, _, fx) = Rig(pbi);
        Assert.Equal(1, WwiseVoiceBusPass.StartStreamAndBuildInsertFx(voice));
        var slot = voice.InsertFxSlots[0]!;
        Assert.Equal(0, slot.Bypass);                                 // the build's own vt+0x6C stored the resolver's 0
        pbi.Flags0E9 = 4;                                             // 0xA019B8 would zero the out pointer; vt+0x70 does not look at it
        int refs = fx.RefCount;
        voice.ResolveNodeFx9EEF2C = _ => { if (hasFx) fx.AddRef(); return (hasFx ? fx : null, bypass); };
        WwisePlayPath.RefreshVoiceVt6C(voice);
        Assert.Equal(bypass, slot.Bypass);
        Assert.Equal(refs, fx.RefCount);                              // a non-null descriptor was released again
    }

    [Fact]
    public void M6_022_2_10_the_build_applies_the_A019B8_gate_and_the_380_node_link_and_the_gain_stage_reads_388_raw()
    {
        var pbi = Pbi();
        var (voice, _, _) = Rig(pbi);
        pbi.Flags0E9 = 4;                                             // 0xA019B8: bit 2 set -> no descriptor, the resolver is not called
        int calls = 0;
        voice.ResolveNodeFx9EEF2C = _ => { calls++; return (null, (byte)0); };
        Assert.Equal(1, WwiseVoiceBusPass.StartStreamAndBuildInsertFx(voice));
        Assert.Equal(0, calls);
        Assert.Null(voice.InsertFxSlots[0]);
        voice.GainNode380Vt24 = null;                                  // the 0x380 node's vt+0x24 is unread: a visible stop
        Assert.Throws<WwiseMissingBehaviourException>(() => WwiseVoiceBusPass.StartStreamAndBuildInsertFx(voice));
    }

    [Theory]
    [InlineData(new int[0])]
    [InlineData(new[] { 0 })]
    [InlineData(new[] { 1, 3 })]
    public void M6_022_2_10_the_link_loop_runs_the_380_node_first_then_the_compacted_chain_downwards(int[] filled)
    {
        // Binary 0xA54D3C..0xA54DB8: arr = [src, pitch node, filled slots compacted, holder 0x1C0, 0x380 node]; k = n+1 down to 1: arr[k]->vt+0x24(arr[k-1]).
        var pbi = Pbi();
        var (voice, _, fx) = Rig(pbi);
        voice.ResolveNodeFx9EEF2C = i => { if (!filled.Contains(i)) return (null, (byte)0); fx.AddRef(); return (fx, (byte)0); };
        object? seen = null;
        voice.GainNode380Vt24 = up => seen = up;
        Assert.Equal(1, WwiseVoiceBusPass.StartStreamAndBuildInsertFx(voice));
        var slots = filled.Select(i => (object)voice.InsertFxSlots[i]!).ToList();
        var expected = new List<(object, object)>();
        var arr = new List<object> { voice.Source!, voice.PitchNode };
        arr.AddRange(slots); arr.Add(voice.HolderNode1C0); arr.Add(voice.GainNode380);
        for (int k = arr.Count - 1; k >= 1; k--) expected.Add((arr[k], arr[k - 1]));
        Assert.Equal(expected, voice.ChainLinks);
        Assert.Same(voice.GainNode380, voice.ChainLinks[0].Node);     // the 0x380 node is called first, with the holder as upstream
        Assert.Same(voice.HolderNode1C0, voice.ChainLinks[0].Upstream);
        Assert.Same(voice.HolderNode1C0, seen);
        Assert.Same(slots.Count > 0 ? slots[^1] : voice.PitchNode, voice.ChainLinks[1].Upstream);   // the holder's upstream: the topmost slot, else the pitch node
        Assert.Same(voice.Source, voice.ChainLinks[^1].Upstream);
    }
}
