using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Conformance;
using Cozmo.Transport;
using Xunit;
namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class StartupDeliveryRegressionTests
{
    private static string ShippedResources()
    {
        for(var dir=new DirectoryInfo(AppContext.BaseDirectory);dir is not null;dir=dir.Parent)
        {
            string root=Path.Combine(dir.FullName,"re-analysis","obb","assets","cozmo_resources");
            if(Directory.Exists(Path.Combine(root,"assets","animations")))return root;
        }
        throw new DirectoryNotFoundException("B-M1-5 requires the shipped OBB resources tree.");
    }

    // reach: production — public BeforeStart/Create, real startup thread, shipped files and transport framing/handler tick.
    // Robot input is a loopback scripted peer; this makes no hardware-acceptance claim.
    [Fact]
    public async Task ShippedStartupPublishesTheWholeListBeforeReadyAndThenProcessesRobotTraffic()
    {
        var order=new ConcurrentQueue<string>();var names=new ConcurrentQueue<string>();var progress=new ConcurrentQueue<float>();
        var stateLogs=new ConcurrentQueue<EngineLogEntry>();
        var ready=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var robot=CozmoRobot.Create(engineOptions:new CozmoEngineOptions
        {
            ResourcesPath=ShippedResources(),BlockPoolPath="",LabLocaleLanguage="en",LabLocaleCountry="US",
            BeforeStart=e=>
            {
                e.LogEntry+=entry=>{if(entry.Name=="app.engine.state")stateLogs.Enqueue(entry);};
                e.EngineStateChanged+=s=>{order.Enqueue($"{s.OldState}->{s.NewState}");if(s.NewState==3){Assert.True(e.IsReady);ready.TrySetResult();}};
                e.EngineStartupFailed+=f=>ready.TrySetException(new IOException(f.Reason));
                e.EngineLoading+=p=>progress.Enqueue(p.Ratio);
                e.AnimationAvailable+=a=>{Assert.False(e.IsReady);names.Enqueue(Encoding.UTF8.GetString(a.Name.Span));order.Enqueue("name");};
                e.AnimationAnnouncementsEnded+=_=>{Assert.False(e.IsReady);order.Enqueue("end");};
            }
        });
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(60));
        // Independent flatbuffer root-vector/name-field and JSON-file census: 289 .bin + 4 .json files, 997 distinct names.
        // Expected values are not obtained from AnimationLibrary, ClipNames or another production loader.
        Assert.Equal(997,names.Count);Assert.Equal(997,names.Distinct().Count());Assert.DoesNotContain("_PROCEDURAL_",names);
        Assert.Equal(new[]{"anim_bored_01","anim_bored_02","anim_bored_event_01","anim_bored_event_02","anim_bored_event_03"},names.Take(5));
        Assert.Equal(new[]{"anim_qa_firmwaremessaging_01","anim_triple_backup","soundTestAnim"},names.TakeLast(3));
        Assert.Equal(new[]{"0->1","1->2"}.Concat(Enumerable.Repeat("name",997)).Concat(new[]{"end","2->3"}),order);
        Assert.Equal(new[]{"Stopped","WaitingForUIDevices","LoadingData"},stateLogs.Select(l=>l.Text));
        Assert.Equal(new[]{"WaitingForUIDevices","LoadingData","Running"},stateLogs.Select(l=>l.Data["$data"]));
        // Poll copies progress before joining the worker; the completion tick need not broadcast ratio 1.
        Assert.Equal(0f,progress.First());Assert.Contains(progress,p=>p>0&&p<=1);Assert.Equal(1f,robot.Engine.LoadingProgress);
        robot.ConnectToRobot(IPAddress.Loopback,isSimulated:true);
        Assert.True(SpinWait.SpinUntil(()=>robot.Transport.Connection is not null,TimeSpan.FromSeconds(5)));
        robot.Transport.ProcessIncoming(FrameCodec.Encode(Frame.Single(new SubMessage(ReliableMessageType.ConnectionResponse,Array.Empty<byte>(),1),1)));
        Assert.True(SpinWait.SpinUntil(()=>robot.Engine.ConnectionState==2,TimeSpan.FromSeconds(5)));
        robot.Transport.ProcessIncoming(FrameCodec.Encode(Frame.Single(new SubMessage(ReliableMessageType.SingleReliableMessage,
            new RobotAvailable{SerialNumberHead=0x12345678,HwVersion=5}.ToBytes(),2),1)));
        Assert.True(SpinWait.SpinUntil(()=>robot.State.Available?.SerialNumberHead==0x12345678,TimeSpan.FromSeconds(5)));
    }

    // reach: production — ConnectAsync caller subscribes only through the public options route; cancellation follows delivered ready.
    [Fact]
    public async Task ConnectAsyncCallerCanObserveNamesAndEveryStartupTransition()
    {
        string root=Directory.CreateTempSubdirectory("public-startup-").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(root,"animations"));
            File.WriteAllText(Path.Combine(root,"animations","a.json"),"{\"a\":[]}");
            File.WriteAllText(Path.Combine(root,"animations","b.json"),"{\"b\":[]}");
            var order=new ConcurrentQueue<string>();using var cancel=new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>CozmoRobot.ConnectAsync(IPAddress.Loopback,isSimulated:true,
                cancellationToken:cancel.Token,engineOptions:new CozmoEngineOptions{ResourcesPath=root,BlockPoolPath="",BeforeStart=e=>
                {
                    e.EngineStateChanged+=s=>{order.Enqueue($"{s.OldState}->{s.NewState}");if(s.NewState==3)cancel.Cancel();};
                    e.AnimationAvailable+=a=>order.Enqueue(Encoding.UTF8.GetString(a.Name.Span));
                    e.AnimationAnnouncementsEnded+=_=>order.Enqueue("end");
                }}));
            Assert.Equal(new[]{"0->1","1->2","a","b","end","2->3"},order);
        }
        finally{Directory.Delete(root,true);}
    }

    // reach: unit (direct loader input substitutes startup; exercises duplicate definition ownership, not host startup).
    [Fact]
    public void DuplicateNameKeepsItsFirstDefinitionAndItsSingleListPosition()
    {
        string root=Directory.CreateTempSubdirectory("first-definition-").FullName;
        try
        {
            string dir=Directory.CreateDirectory(Path.Combine(root,"animations")).FullName;
            File.WriteAllText(Path.Combine(dir,"a.json"),"{\"same\":[{\"Name\":\"EventKeyFrame\",\"triggerTime_ms\":1,\"event_id\":\"TAPPED_BLOCK\"}]}");
            File.WriteAllText(Path.Combine(dir,"z.json"),"{\"same\":[{\"Name\":\"EventKeyFrame\",\"triggerTime_ms\":99,\"event_id\":\"TAPPED_BLOCK\"}]}");
            var library=AnimationLibrary.Open(root);Assert.Equal(new[]{"same"},library.ClipNames);
            Assert.Equal(1u,library.GetClip("same")!.Keyframes.Single().TriggerTimeMs);
        }
        finally{Directory.Delete(root,true);}
    }

    // reach: production — the shared live-tool option resolver, with the actual shipped root and OBB container path.
    [Fact]
    public void LiveToolsResolveResourcesAndExplainAMissingInput()
    {
        string root=ShippedResources();
        Assert.Equal(root,EngineStartupResources.Options(new[]{"--resources",root}).ResourcesPath);
        Assert.Equal(root,EngineStartupResources.Options(new[]{"--obb",Directory.GetParent(Directory.GetParent(root)!.FullName)!.FullName}).ResourcesPath);
        Assert.Contains("--resources",Assert.Throws<ArgumentException>(()=>EngineStartupResources.Options(Array.Empty<string>())).Message);
    }

    // reach: production — live-tool argv parsers, with deliberately invalid later arguments; returns before any run or connection.
    [Fact]
    public void StrictLiveToolParsersAcceptTheResourcesOption()
    {
        var original=Console.Error;using var error=new StringWriter();Console.SetError(error);
        try
        {
            Assert.Equal(2,M3M4Hw.Run(new[]{"m3m4-hw","--resources",ShippedResources(),"--only","INVALID_CHECK"}));
            Assert.Contains("unknown check",error.ToString());error.GetStringBuilder().Clear();
            Assert.Equal(2,ControlCheck.Run(new[]{"control-check","--resources",ShippedResources(),"INVALID_IP"}));
            Assert.Contains("not an IP address",error.ToString());
        }
        finally{Console.SetError(original);}
    }
}

