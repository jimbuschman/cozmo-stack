using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Xunit;
namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class AppDeliveryTests
{
    private static AnimationClip Clip(string name)=>new(){Name=name,Keyframes=new List<Keyframe>{new EventKeyframe(5000,"TAPPED_BLOCK")},
        DurationMs=5000,Tracks=AnimationTrack.Event};

    // reach: production — public Play -> actual scheduler AbortLocked -> real robot8D/local5F -> engine tick app boundary.
    [Fact]
    public void AnAbortHostCanReplaceTheAnimationWithoutRecursiveDeliveryOrAnIncompleteMutation()
    {
        using var f=new M4ControlTests.Rig();f.ToSynced();f.Robot.Animations.ManualTicking=true;
        var animations=f.Robot.Animations;var host=new List<uint>();var local=new List<uint>();
        using var subscription=f.Engine.AnimationMessages.SubscribeMessage(0x5F,true,p=>local.Add((uint)p!));
        var first=animations.Play(Clip("a"));uint firstTag=animations.CurrentTag;
        int depth=0,maxDepth=0;
        f.Engine.EngineToGameMessage+=(tag,body)=>
        {
            if(tag!=0x5F)return;
            depth++;maxDepth=Math.Max(depth,maxDepth);host.Add(BinaryPrimitives.ReadUInt32LittleEndian(body.Span));
            animations.Play(Clip("from-host"));depth--;
        };
        int before=f.Port.Sent.Count(p=>p.Length>0&&p[0]==0x8D);
        var second=animations.Play(Clip("b"));uint secondTag=animations.CurrentTag;
        Assert.Empty(host);Assert.Equal(new[]{firstTag},local);Assert.True(first!.IsCompletedSuccessfully);Assert.Equal(AnimationEndReason.Replaced,first.Result);
        Assert.Equal(before+1,f.Port.Sent.Count(p=>p.Length>0&&p[0]==0x8D));
        f.Tick();
        Assert.Equal(new[]{firstTag},host);Assert.Equal(new[]{firstTag,secondTag},local);
        Assert.True(second!.IsCompletedSuccessfully);Assert.Equal(AnimationEndReason.Replaced,second.Result);Assert.Equal(1,maxDepth);
        Assert.Equal(before+2,f.Port.Sent.Count(p=>p.Length>0&&p[0]==0x8D));
        uint thirdTag=animations.CurrentTag;
        f.Tick();Assert.Equal(new[]{firstTag,secondTag},host);Assert.Equal(new[]{firstTag,secondTag,thirdTag},local);
        Assert.Equal(1,maxDepth); // New publications wait for a later drain, including unconditionally replacing hosts.
    }

    // reach: unit (substitutes a second-thread Engine.Tick that the production ticker cannot produce; checks lock release only).
    [Fact]
    public void AWaitingHostDoesNotHoldTheSchedulerOrTickLock()
    {
        using var f=new M4ControlTests.Rig();f.ToSynced();f.Robot.Animations.ManualTicking=true;
        f.Robot.Animations.Play(Clip("a"));int calls=0;bool completedOutsideLocks=false;Task? work=null;
        f.Engine.EngineToGameMessage+=(tag,_)=>
        {
            if(tag!=0x5F||++calls!=1)return;
            work=Task.Run(()=>{f.Robot.Animations.Play(Clip("worker"));f.Engine.Tick();});
            completedOutsideLocks=work.Wait(TimeSpan.FromSeconds(2));
        };
        f.Robot.Animations.Play(Clip("b"));f.Tick();
        Assert.True(completedOutsideLocks);Assert.NotNull(work);Assert.True(work!.Wait(TimeSpan.FromSeconds(2)));
        Assert.Equal(1,calls);f.Tick();Assert.Equal(2,calls);
    }

    // reach: unit (direct checked publication entries/announcement recipient and state setter; substitutes the native name producer588190).
    [Fact]
    public void InterleavedViewsKeepPublicationOrderAndBytesWhileLocalDeliveryStaysSynchronous()
    {
        using var f=new M4ControlTests.Rig();var e=f.Engine;var host=new List<string>();var local=new List<ushort>();
        byte[]? receivedRaw=null;byte[]? receivedName=null;
        e.EngineToGameMessage+=(tag,body)=>{host.Add($"raw:{tag:X2}");if(tag==0x6D)receivedRaw=body.ToArray();};
        e.AnimationEvent+=_=>host.Add("typed:60");
        e.AnimationAvailable+=p=>{host.Add("typed:5D");receivedName=p.Name.ToArray();};
        e.AnimationAnnouncementsEnded+=_=>host.Add("typed:9A");e.EngineStateChanged+=_=>host.Add("typed:7B");
        var subscriptions=new List<IDisposable>();
        foreach(ushort tag in new ushort[]{0x32,0x60,0x6D,0x5D,0x9A,0x7B})
            subscriptions.Add(e.AnimationMessages.SubscribeMessage(tag,true,p=>
            {
                Assert.Empty(host);local.Add(tag);
                if(tag==0x5D&&p is AnimationAvailableMessage name&&MemoryMarshal.TryGetArray(name.Name,out var bytes))bytes.Array![bytes.Offset]=255;
            }));
        try
        {
            uint count=e.NativeUiDeliveryCount;
            e.DeliverDelocalizationToApp();e.AnimationMessages.Deliver(0x32,true);
            e.BroadcastAnimationEvent(new(0x11223344,2));
            byte[] data={1,2,3};e.DeliverNurture(0x6D,data,null);data[0]=99;
            byte[] name=Encoding.UTF8.GetBytes("clip");var output=e.Robots.AnnouncementRecipient!()!;output.Available(name);name[0]=99;output.End(0);
            e.SetEngineState(2);
            Assert.Empty(host);Assert.Equal(new ushort[]{0x32,0x60,0x6D,0x5D,0x9A,0x7B},local);
            Assert.Equal(count+6,e.NativeUiDeliveryCount);
            e.DrainAppDelivery();
            Assert.Equal(new[]{"raw:32","raw:60","typed:60","raw:6D","typed:5D","typed:9A","raw:9A","raw:7B","typed:7B"},host);
            Assert.Equal(new byte[]{1,2,3},receivedRaw);Assert.Equal(Encoding.UTF8.GetBytes("clip"),receivedName);
            Assert.Equal(count+6,e.NativeUiDeliveryCount); // Dispatch does not count again for raw/typed views.
        }
        finally{foreach(var subscription in subscriptions)subscription.Dispose();}
    }

    // reach: unit (manual scheduler Advance substitutes its engine/ticker clock; actual stack string observer is buffered without claiming a native E2G contract).
    [Fact]
    public void TheStackStringObserverUsesTheSameSafeAppBoundary()
    {
        using var f=new M4ControlTests.Rig();f.ToSynced();f.Robot.Animations.ManualTicking=true;
        var seen=new List<string>();f.Robot.Animations.Event+=seen.Add;
        f.Robot.Animations.Play(new AnimationClip{Name="event",Keyframes=new List<Keyframe>{new EventKeyframe(0,"TAPPED_BLOCK")},
            Tracks=AnimationTrack.Event,DurationMs=5000});
        f.Robot.Animations.Scheduler.Advance(0);Assert.Empty(seen);
        f.Tick();Assert.Equal(new[]{"TAPPED_BLOCK"},seen);
    }

    // reach: unit (direct queued publication and disposal; no native source timing is claimed for disposed host callbacks).
    [Fact]
    public void DisposalDropsPendingHostCallbacks()
    {
        var f=new M4ControlTests.Rig();int calls=0;f.Engine.EngineToGameMessage+=(_,_)=>calls++;
        f.Engine.DeliverAnimationReportToApp(0x5F,new byte[4]);f.Dispose();f.Engine.DrainAppDelivery();Assert.Equal(0,calls);
    }
}

