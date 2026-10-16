using Cozmo.Robot.Animation.Wwise;
using Cozmo.Robot.Behavior;
using Xunit;
namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class MidiBoundaryAndLiveModulationTests
{
    private static (WwiseMidiRuntime R, WwiseMidiClipContext C, WwiseRoutingNode T) Rig()
    {
        var t = new WwiseRoutingNode { Id = 71, Category44 = 5 };
        var registry = new WwiseRuntimeRegistry(); registry.Insert(t);
        var track = new WwiseMidiMusicNode { Track = true, Flags = 1, Properties3C = new(new byte[] { 56 }, new uint[] { 71 }) };
        var r = new WwiseMidiRuntime(new WwiseRng(1));
        var c = new WwiseMidiClipContext(r, track, new object()) { GameObject8 = new object() };
        Assert.Equal(1, c.ResolveTarget(registry)); r.Clips.Add(c); return (r,c,t);
    }
    private static WwisePlayingInstance Pbi() => new WwisePlaybackBridge().CreatePbi(new(), 1, new object(), false);
    private static WwiseRtpc DepthCurve() => new(3255781855, 0, 0, 0, 460532349, 0,
        new[] { (0f,0f,4u), (1f,100f,4u) });
    private static WwiseRtpcStore Store() => new(new[] { new WwiseStmgParam(3255781855, 0, 0, 0, 0, false) });

    [Theory]
    [InlineData(63,0)] [InlineData(64,8)] [InlineData(127,8)]
    public void SustainPostsBeforeChangingItsChannelMaskAndThePumpUnlinks(int value, ushort mask)
    {
        var (r,c,_) = Rig(); var posts = new List<(uint,float)>();
        r.Admit(c, new(0xB0,3,64,(byte)value), 2000);
        r.PostControllerA149C8 = (id,v,key) =>
        { Assert.Equal((ushort)0, r.Targets[0].ChannelMask); Assert.Equal((71u,(byte)3,(byte)255), (key.Target,key.Channel,key.Note)); Assert.Same(c.GameObject8,key.Object); posts.Add((id,v)); };
        r.AdvanceHolders(8,8);
        Assert.Equal(new[] { (65u,(float)value) },posts); Assert.Equal(mask,r.Targets[0].ChannelMask);
        Assert.Empty(r.Targets[0].Holders); // nonzero StepHolder result unlinks even a non-note ahead of the frame
    }
    [Fact]
    public void SustainHoldsOnlyTheReleaseAndClearingItAllowsTheNextPumpToRetire()
    {
        var (r,c,_) = Rig(); r.PostControllerA149C8 = (_,_,_) => { };
        var on = r.Admit(c,new(0x90,2,60,100),0)!;
        r.Admit(c,new(0xB0,2,64,127),0); r.AdvanceHolders(8,8);
        var off = r.Admit(c,new(0x80,2,60,0),0)!; r.AdvanceHolders(8,8);
        Assert.Contains(off,r.Targets[0].Holders); Assert.Equal(0,off.Flags1C & 3); // sustain suppresses both due-processing and first propagation
        r.Admit(c,new(0xB0,2,64,0),0); r.AdvanceHolders(8,8);
        Assert.Contains(off,r.Targets[0].Holders); // clear is after this release in stored order
        r.AdvanceHolders(8,8); Assert.DoesNotContain(off,r.Targets[0].Holders);
    }
    [Fact]
    public void PitchBendAndResetUseTheirNativeIdsAndDescriptor()
    {
        var (r,c,_) = Rig(); float bend = 0; var reset = new List<uint>();
        r.PostControllerA149C8 = (id,v,_) => { Assert.Equal(131u,id); bend=v; };
        r.Admit(c,new(0xE0,1,127,127),0); r.AdvanceHolders(8,8); Assert.Equal(127.9921875f,bend);
        r.Targets[0].ChannelMask = ushort.MaxValue;
        r.ResetControllerA14098 = (id,_,duration,curve,flag) => { Assert.Equal((0,4,(byte)0),(duration,curve,flag)); reset.Add(id); };
        r.Admit(c,new(0xB0,1,121,0),0); r.AdvanceHolders(8,8);
        Assert.Equal(Enumerable.Range(1,133).Select(x=>(uint)x),reset); Assert.Equal((ushort)0,r.Targets[0].ChannelMask);
    }
    [Fact]
    public void ResetClearsRendererBytesAndContinuesIntoBothMainContexts()
    {
        var (r,c,_) = Rig(); c.Disabled40 = 1; r.Reset40=9;
        var other = new WwiseMidiRuntime(new WwiseRng(2)); var calls = new List<int>();
        r.ContextFlags36=1;other.ContextFlags36=1;
        r.RendererVirtual44 = _=>calls.Add(1); other.RendererVirtual44 = _=>calls.Add(2);
        var callback = new WwiseMidiFrameCallback { Suppressed=true, ResetByte0C=7,
            NormalContexts=new() { Renderer38=r, Scale3C=1, Next48=new() { Renderer38=other, Scale3C=1 } } };
        callback.Invoke(8,48000);
        Assert.Equal(new[]{1,2},calls); Assert.False(callback.Suppressed); Assert.Equal((byte)0,callback.ResetByte0C);
        Assert.Equal(((byte)0,(byte)7),(r.Reset40,r.Reset41)); Assert.Equal((byte)7,other.Reset41);
        Assert.Equal((byte)1,c.Disabled40); // reset +40 belongs to renderer, not the clip's admission gate
    }
    [Fact]
    public void StartingNewClipPurgesOldQueueAndSynthesizesSixteenSustainClearsAndOneRelease()
    {
        var (r,old,t) = Rig(); var track = old.Track78;
        var next = new WwiseMidiClipContext(r,track,new object()) { GameObject8=old.GameObject8 };
        var registry = new WwiseRuntimeRegistry(); registry.Insert(t); next.ResolveTarget(registry); r.Clips.Add(next);
        old.FlagsFC=4;
        var on=r.Admit(old,new(0x90,6,60,99),0)!; r.Targets[0].ChannelMask=0xFFFF;
        r.Enqueue(old,new(0xB0,1,1,1),100);
        int refs=old.References3C;
        r.StartClipA3CEB4(new() { Context=next,Event=new(0x90,6,61,100),Offset=20 });
        Assert.Empty(r.Pending); Assert.Equal(refs-1,old.References3C); Assert.Equal((ushort)0,r.Targets[0].ChannelMask);
        Assert.Equal(18,r.Targets[0].Holders.Count);
        Assert.Equal(Enumerable.Range(0,16).Select(i=>(byte)i), r.Targets[0].Holders.Where(x=>x.Event14.Status==0xB0).Select(x=>x.Event14.Channel));
        var off=Assert.Single(r.Targets[0].Holders.Where(x=>x.Event14.Status==0x80));
        Assert.Same(old,off.Context); Assert.Same(on.NoteState8,off.NoteState8); Assert.Equal(20,off.Offset18);
    }
    [Fact]
    public void ChaseReplacesSameControllerAcrossChannelsAndRestoresParser()
    {
        var (r,c,_) = Rig(); var q=new Queue<WwiseParsedMidiEvent>(new[] {
            new WwiseParsedMidiEvent(new(0xB0,1,7,10),0,1),new(new(0xB0,2,7,20),0,2),
            new(new(0x90,2,60,100),0,3),new(new(0xB0,3,8,30),0,4) });
        var token=new object(); var calls=new List<string>();
        r.ChaseParser.SaveA3F1B0=()=>{calls.Add("save");return token;};
        r.ChaseParser.ResetA3F0A0=()=>calls.Add("reset"); r.ChaseParser.NextA3FC4C=()=>q.Count==0?null:q.Dequeue();
        r.ChaseParser.RestoreA3F1CC=o=>{Assert.Same(token,o);calls.Add("restore");};
        var result=r.ChaseA3C760(c,50,4);
        Assert.Equal(new[]{"save","reset","restore"},calls);
        Assert.Equal(new[]{new WwiseMidiEvent(0xB0,2,7,20),new(0xB0,3,8,30)},result.Select(x=>x.Event));
        Assert.All(result,x=>Assert.Equal(50u,x.Offset)); Assert.Equal(3,c.References3C);
    }
    [Fact]
    public void NoteOffSetsDeadlineBeforePropagationAndRetireSetsSentinelBeforeStop()
    {
        var (r,c,_) = Rig(); var p=Pbi(); var on=r.Admit(c,new(0x90,0,60,100),0)!;
        on.NoteState8!.RecordedVoices2C.Add(new(3,p)); var order=new List<string>();
        r.HardStopQuery=_=>{ Assert.Equal(0u,p.Field38); order.Add("release");return false; };
        r.StopA01280=(_,flag)=>{Assert.Equal(0,flag);Assert.Equal(uint.MaxValue,p.Field1F8);};
        r.PropagateOwnerA8=(_,_)=>order.Add("propagate"); p.NodeE0 = new WwiseSoundNode(1,"fixture",new(0,0,0,new Dictionary<byte,uint>(),new Dictionary<byte,(float,float)>(),Array.Empty<WwiseRtpc>(),Array.Empty<(uint,byte,IReadOnlyList<(uint,uint)>)>()),0,0,0,0,0);
        var off=r.Admit(c,new(0x80,0,60,0),0)!;
        r.StepHolder(off,8,8,true); Assert.Equal(new[]{"release","propagate"},order);
        p.StopVirtual0=(_,_,_)=>Assert.Equal(uint.MaxValue,p.Field1F8);
        r.RetireA3EB4C(off);
    }
    [Fact]
    public void EnvelopeRampUsesBlocksAndClampsOnlyTheCarry()
    {
        float max=0; var a=new float[8];
        float carry=WwiseEnvelopeEmitters.Ramp(a,0,0.125f,ref max);
        Assert.Equal(new[]{0f,0.125f,0.25f,0.375f,0.5f,0.625f,0.75f,0.875f},a);
        Assert.Equal(1f,carry); Assert.Equal(1f,max);
        max=0; carry=WwiseEnvelopeEmitters.Ramp(a.AsSpan(0,4),1.25f,0.25f,ref max);
        Assert.Equal(new[]{1.25f,1.5f,1.75f,2f},a.Take(4)); Assert.Equal(1f,carry); Assert.Equal(1.25f,max);
    }
    [Fact]
    public void RampMaximumRetainsNaNAndUsesStartAndCarryRatherThanScanningSamples()
    {
        float max=float.NaN; var a=new float[4]; WwiseEnvelopeEmitters.Ramp(a,2,-1,ref max);
        Assert.True(float.IsNaN(max)); Assert.Equal(new[]{2f,1f,0f,-1f},a);
        max=0; Assert.Equal(2f,WwiseEnvelopeEmitters.Sustain(a,2,ref max)); Assert.Equal(2f,max);
    }
    [Fact]
    public void MaximumReaderUsesOutputMaximumEvenWithoutABufferAndArmShiftCounts()
    {
        var n=new WwiseModulationBindingNode { State=new() {Current4C=10,Output30=new() {RunningMax=2,Last=99} } };
        n.Bindings.Add(new(1,3,0)); n.Bindings.Add(new(100,100,64));
        Assert.Equal(7f,WwiseModulationMaximum.Product(n,1,0)); Assert.Equal(14f,WwiseModulationMaximum.VolumeFactor(2,n));
        // Register shifts use their low byte: parameter 256 selects the same bit as 0.
        n.Bindings.Clear();n.Bindings.Add(new(1,3,256)); Assert.Equal(7f,WwiseModulationMaximum.Product(n,1,0));
    }
    [Theory]
    [InlineData(-1,0)] [InlineData(0,0)] [InlineData(0.5f,0.5f)] [InlineData(2,1)]
    public void VibratoDepthIsZeroUnpostedAndCurveOver100WhenPosted(float posted,float expected)
    {
        var s=Store(); s.AddSubscription(new() {Key1=123,Param=0,Type=3,Curves=new[]{DepthCurve()},TargetModulator=new() {Scope=1}});
        Assert.Equal(0,BitConverter.SingleToInt32Bits(s.A11590(123,0,new(7,0))));
        // This fixture supplies the checked first-cell admission result; that gate is not inferred from a bank.
        s.TransitionGateA1B5FC=(_,_,_)=>true;
        s.SetParameter(3255781855,posted,7);
        Assert.Equal(expected,s.A11590(123,0,new(7,0))/100f);
    }
    [Fact]
    public void ImmediateLiveDepthChangesTheExistingStateBeforeThisTicksCapture()
    {
        var s=Store(); var state=new WwiseLiveLfoState(); var tree=new WwiseModulatorStateTree {Scope=1};
        tree.Objects.Add(7,new() {State=state});
        s.AddSubscription(new() {Key1=123,Param=0,Type=3,Curves=new[]{DepthCurve()},TargetModulator=tree});
        s.TransitionGateA1B5FC=(_,_,_)=>true;
        var record=new WwiseModulatorRecord0(); var order=new List<int>();
        s.SetParameter(3255781855,0.5f,7); order.Add(1);
        var frame=new WwiseEventRuntime(Array.Empty<WwiseBank>()) {NativePerformRenderGate=true};
        frame.MusicPhase8=()=>order.Add(2);frame.ModulatorTick=()=>{state.Capture(record);order.Add(3);};
        frame.AdvanceFrame();
        Assert.Equal(new[]{1,2,3},order); Assert.Equal(0.5f,record.Start18);
        s.SetParameter(3255781855,1,7); state.Capture(record); Assert.Equal(1f,record.Start18);
    }
    [Fact]
    public void ExactObjectDispatchDoesNotBroadcastAndScopeCollapseKeepsVisitOrder()
    {
        var tree=new WwiseModulatorStateTree {Scope=2}; var a=new WwiseLiveLfoState(); var b=new WwiseLiveLfoState();
        tree.Objects.Add(7,new() {State=a}); tree.Objects.Add(8,new() {State=b});
        tree.Dispatch(0,50,new(7,9,2,3,60,99)); Assert.Equal(0.5f,a.Depth74);Assert.Equal(0f,b.Depth74);
        tree.Dispatch(0,25,WwiseRtpcScopeKey.Wild); Assert.Equal(0.25f,a.Depth74);Assert.Equal(0.25f,b.Depth74);
    }
    [Fact]
    public void CollectorTailUsesParentThenBusWithForwardedMaskAndBusDisabled()
    {
        var node=new WwiseParameterCollectorNode {Node=new()}; var seen=new List<string>();
        var parent=new WwiseParameterCollectorNode {Node=new(),VirtualAC=c=>{seen.Add("parent");Assert.Equal(3u,c.Mask);Assert.False(c.IncludeBus);c.Flags24=9;}};
        var bus=new WwiseParameterCollectorNode {Node=new(),VirtualAC=c=>{seen.Add("bus");Assert.Equal((byte)9,c.Flags24);Assert.False(c.IncludeBus);}};
        node.Parent34=parent;node.Bus38=bus;node.Layers50.Add(_=>seen.Add("layer"));
        new WwiseParameterCollector(Store()).Tail9EFFD4(node,new(){Mask=0x43,IncludeBus=true},3);
        Assert.Equal(new[]{"parent","bus","layer"},seen);
    }
    [Fact]
    public void NativeScopeReaderFallsBackToTheNearestPresentAncestor()
    {
        var root=new WwiseRtpcCurrentScope {Value=1};var obj=new WwiseRtpcCurrentScope {Value=2}; root.Children.Add(7,obj);
        var playing=new WwiseRtpcCurrentScope(); obj.Children.Add(9,playing); playing.Children.Add(0,new(){Value=3});
        Assert.Equal(3,root.Lookup(new(7,9,100,2,60,0))); Assert.Equal(2,root.Lookup(new(7,8,100,2,60,0)));
        Assert.Equal(1,root.Lookup(new(8,9,100,2,60,0)));
    }
    [Fact]
    public void ExistingPerformEntryRunsMusicBeforeModulatorCapture()
    {
        var runtime=new WwiseEventRuntime(Array.Empty<WwiseBank>(),new WwiseRng(1)) {NativePerformRenderGate=true};
        var state=new WwiseLiveLfoState(); var output=new WwiseModulatorRecord0();var order=new List<int>();
        runtime.MusicPhase8=()=>{order.Add(8);state.SetParameter(0,75);};
        runtime.ModulatorTick=()=>{order.Add(28);state.Capture(output);};
        runtime.AdvanceFrame(); Assert.Equal(new[]{8,28},order);Assert.Equal(0.75f,output.Start18);Assert.Equal(1,runtime.CurrentTick);
    }
    [Fact]
    public void CollectorEntryWritesStateThenBaseBeforeTheUnreadMiddle()
    {
        var n=new WwiseParameterCollectorNode {Node=new() {BaseBundle3C=new(new byte[]{0,6},new uint[]{0x40000000,0x40400000})},StateEnabled46=true};
        n.StateTables18.Add(new(new byte[]{0,6},new uint[]{0x3F800000,0x40800000}));
        var call=new WwiseCollectorCall {Mask=1};
        Assert.False(new WwiseParameterCollector(Store()).Entry9EF258(n,call));
        Assert.Equal(3f,call.Output[0]);Assert.Equal(7f,call.Output[6]);Assert.Equal((byte)2,call.Flags1C);
    }
    [Fact]
    public void RangeDrawRoundsTheDeltaToFloatBeforeDoubleProduct()
    {
        var n=new WwiseGainNode {RangedProps=new Dictionary<byte,(float,float)> {{0,(float.MaxValue,-float.MaxValue)}}};
        var ranges=new WwiseGainRanges();var rng=new WwiseRng(1);
        ranges.Add(n,rng); Assert.Equal(float.NegativeInfinity,ranges.Volume);
        // Delta overflows in binary32; a double subtraction would yield a finite result.
    }
    [Theory]
    [InlineData(0u,0xFB)] [InlineData(1u,0xFF)] [InlineData(2u,0xFB)]
    public void PositionCollectorCopiesOnlyTheReturnedLowBitIntoAlternateBitTwo(uint returned,int expected)
    {
        var ctx=new WwisePositioningCollectorContext {Flags3C=0xFF};
        var p=Pbi();p.CtxD0=ctx;
        var node=new WwiseSoundNode(1,"fixture",new(0,0,0,new Dictionary<byte,uint>(),new Dictionary<byte,(float,float)>(),Array.Empty<WwiseRtpc>(),Array.Empty<(uint,byte,IReadOnlyList<(uint,uint)>)>()),0,0,0,0,0);
        var path=new WwisePlayPath(_=>null,new() {A9FB17CResult=(selected,voice)=>{Assert.Same(node,selected);Assert.Same(p,voice);return returned;}});
        path.A9FBE74(p,node);Assert.Equal((byte)expected,ctx.Flags3C);
    }

    [Fact]
    public void CollectorFilterPreservesNullPositioningGateAndFirstObjectSisOverride()
    {
        var parent=new WwiseParameterCollectorNode {Node=new()};
        var n=new WwiseParameterCollectorNode {Node=new(),Parent34=parent,GlobalSisFlags10=16};
        var call=new WwiseCollectorCall {Key=new(7,9),Filter5B=9};var collector=new WwiseParameterCollector(Store());
        Assert.False(collector.Filter9EF0E4(n,call));Assert.Equal((byte)9,call.Filter5B);
        n.Flags40=0x1000;
        Assert.True(collector.Filter9EF0E4(n,call));Assert.Equal((byte)0,call.Filter5B); // null N+28 skips SIS
        n.PositioningFlags24=0;n.ObjectSisFlags10.Add((7,0));n.ObjectSisFlags10.Add((7,16));
        Assert.True(collector.Filter9EF0E4(n,call));Assert.Equal((byte)0,call.Filter5B);
        n.ObjectSisFlags10.Clear();Assert.True(collector.Filter9EF0E4(n,call));Assert.Equal((byte)1,call.Filter5B);
    }
    [Theory]
    [InlineData(0,false,0u)] [InlineData(0,true,0u)] [InlineData(4,false,2u)] [InlineData(4,true,1u)]
    public void EnvelopeDescriptorControlUsesTheTransferredPlayBit(int flags,bool initial,uint expected)
    { var p=Pbi();p.Flags1BF=(byte)flags;Assert.Equal(expected,p.EnvelopeEventControl9FF4E0(initial)); }

}
