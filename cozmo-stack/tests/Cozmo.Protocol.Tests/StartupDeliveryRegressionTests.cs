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
        var publications=new ConcurrentQueue<string>();
        var stateLogs=new ConcurrentQueue<EngineLogEntry>();
        bool readyEngineSeen=false,readyHostSeen=false;
        var ready=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var robot=CozmoRobot.Create(engineOptions:new CozmoEngineOptions
        {
            ResourcesPath=ShippedResources(),BlockPoolPath="",LabLocaleLanguage="en",LabLocaleCountry="US",
            BeforeStart=e=>
            {
                e.LogEntry+=entry=>{if(entry.Name=="app.engine.state")stateLogs.Enqueue(entry);};
                e.EngineStateChanged+=s=>
                {
                    order.Enqueue($"{s.OldState}->{s.NewState}");publications.Enqueue($"{s.OldState}->{s.NewState}");
                    if(s.NewState==3){readyEngineSeen=e.IsReady;readyHostSeen=e.IsReadyDelivered;}
                };
                e.EngineStartupFailed+=f=>ready.TrySetException(new IOException(f.Reason));
                e.EngineLoading+=p=>{progress.Enqueue(p.Ratio);publications.Enqueue("progress");if(e.IsReadyDelivered)ready.TrySetResult();};
                e.AnimationAvailable+=a=>{Assert.True(e.IsReady);Assert.False(e.IsReadyDelivered);names.Enqueue(Encoding.UTF8.GetString(a.Name.Span));order.Enqueue("name");publications.Enqueue("name");};
                e.AnimationAnnouncementsEnded+=_=>{Assert.True(e.IsReady);Assert.False(e.IsReadyDelivered);order.Enqueue("end");publications.Enqueue("end");};
            }
        });
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(60));
        Assert.True(readyEngineSeen);Assert.True(readyHostSeen);
        // Independent flatbuffer root-vector/name-field and JSON-file census: 289 .bin + 4 .json files, 997 distinct names.
        // Expected values are not obtained from AnimationLibrary, ClipNames or another production loader.
        Assert.Equal(997,names.Count);Assert.Equal(997,names.Distinct().Count());Assert.DoesNotContain("_PROCEDURAL_",names);
        Assert.Equal(new[]{"anim_bored_01","anim_bored_02","anim_bored_event_01","anim_bored_event_02","anim_bored_event_03"},names.Take(5));
        Assert.Equal(new[]{"anim_qa_firmwaremessaging_01","anim_triple_backup","soundTestAnim"},names.TakeLast(3));
        Assert.Equal(new[]{"0->1","1->2"}.Concat(Enumerable.Repeat("name",997)).Concat(new[]{"end","2->3"}),order);
        Assert.Equal(new[]{"end","2->3","progress"},publications.TakeLast(3));
        Assert.Contains("progress",publications.TakeWhile(p=>p!="name"));
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

    // reach: production — public ConnectAsync and BeforeStart; a slow startup observer lets native ready publish before ConnectAsync installs its waiter.
    [Fact]
    public async Task ConnectAsyncDoesNotMissReadyPublishedWhileCreateIsDeliveringAnEarlierReport()
    {
        string root=Directory.CreateTempSubdirectory("late-ready-waiter-").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(root,"animations"));File.WriteAllText(Path.Combine(root,"animations","a.json"),"{\"a\":[]}");
            CozmoEngine? engine=null;bool sourceReadySeen=false;using var cancel=new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var connecting=CozmoRobot.ConnectAsync(IPAddress.Loopback,isSimulated:true,cancellationToken:cancel.Token,
                engineOptions:new CozmoEngineOptions{ResourcesPath=root,BlockPoolPath="",BeforeStart=e=>
                {
                    engine=e;e.EngineStateChanged+=s=>
                    {
                        if(s.OldState==0&&s.NewState==1)sourceReadySeen=SpinWait.SpinUntil(()=>e.IsReady,TimeSpan.FromSeconds(5));
                    };
                }});
            try
            {
                Assert.True(sourceReadySeen);Assert.NotNull(engine);
                Assert.True(SpinWait.SpinUntil(()=>engine!.ConnectionState>=1,TimeSpan.FromSeconds(5)));
                Assert.True(engine!.IsReadyDelivered);
            }
            finally{cancel.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>connecting);}
        }
        finally{Directory.Delete(root,true);}
    }

    // reach: production — public ConnectAsync/BeforeStart, real ticker and transport packets; barriers force
    // ready publication before waiter subscription, and subscription before delivery. Reflection only observes
    // the waiter and retrieves the real transport for the scripted peer; it substitutes no engine result.
    [Fact]
    public async Task ConnectAsyncCompletesWhenSubscriptionFallsBetweenReadyCaptureAndDelivery()
    {
        string root=Directory.CreateTempSubdirectory("forced-ready-interleave-").FullName;
        using var releaseNames=new ManualResetEventSlim();
        using var sourceReadyPublished=new ManualResetEventSlim();
        using var cancel=new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var namesEntered=new TaskCompletionSource<CozmoEngine>(TaskCreationOptions.RunContinuationsAsynchronously);
        var initialReleased=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<CozmoRobot>? connecting=null;
        IDisposable? sourceReadyObserver=null;
        try
        {
            Directory.CreateDirectory(Path.Combine(root,"animations"));
            File.WriteAllText(Path.Combine(root,"animations","a.json"),"{\"a\":[]}");
            connecting=Task.Run(()=>CozmoRobot.ConnectAsync(IPAddress.Loopback,isSimulated:true,cancellationToken:cancel.Token,
                engineOptions:new CozmoEngineOptions{ResourcesPath=root,BlockPoolPath="",BeforeStart=e=>
                {
                    // This synchronous local publication follows CaptureAppEvent, so it proves capture
                    // finished, unlike observing the earlier engine-state byte store alone.
                    sourceReadyObserver=e.AnimationMessages.SubscribeMessage(0x7B,true,p=>
                        {if(p is EngineStateTransition{NewState:3})sourceReadyPublished.Set();});
                    e.EngineStateChanged+=s=>
                    {
                        if(s.OldState!=0||s.NewState!=1)return;
                        if(!sourceReadyPublished.Wait(TimeSpan.FromSeconds(10)))
                        {initialReleased.TrySetException(new TimeoutException("native ready not published"));return;}
                        initialReleased.TrySetResult();
                    };
                    e.AnimationAvailable+=_=>{namesEntered.TrySetResult(e);releaseNames.Wait(cancel.Token);};
                }}));
            await initialReleased.Task.WaitAsync(cancel.Token);
            var engine=await namesEntered.Task.WaitAsync(cancel.Token);
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var waiter=typeof(CozmoEngine).GetField("ReadyDeliveredInternal",flags)!;
            Assert.True(SpinWait.SpinUntil(()=>waiter.GetValue(engine) is Delegate,TimeSpan.FromSeconds(5)));
            Assert.True(engine.IsReady);Assert.False(engine.IsReadyDelivered);
            releaseNames.Set();
            Assert.True(SpinWait.SpinUntil(()=>engine.ConnectionState==1,TimeSpan.FromSeconds(5)));
            var port=typeof(CozmoEngine).GetField("_t",flags)!.GetValue(engine)!;
            var transport=(ReliableTransport)port.GetType().GetField("_t",flags)!.GetValue(port)!;
            transport.ProcessIncoming(FrameCodec.Encode(Frame.Single(new SubMessage(ReliableMessageType.ConnectionResponse,Array.Empty<byte>(),1),1)));
            Assert.True(SpinWait.SpinUntil(()=>engine.ConnectionState==2,TimeSpan.FromSeconds(5)));
            void Data(RobotMessage message,ushort seq)=>transport.ProcessIncoming(FrameCodec.Encode(Frame.Single(
                new SubMessage(ReliableMessageType.SingleReliableMessage,message.ToBytes(),seq),1)));
            Data(new FirmwareVersion{RobotId=1,Signature=Encoding.UTF8.GetBytes("{\"sim\":true}")},2);
            Assert.True(SpinWait.SpinUntil(()=>engine.RobotValidated==true,TimeSpan.FromSeconds(5)));
            Data(new ManufacturingID{SerialNumber=0x1234,BodyHwVersion=5,BodyColor=0},3);
            using var robot=await connecting.WaitAsync(cancel.Token);
            Assert.True(robot.Engine.IsReadyDelivered);
        }
        finally
        {
            releaseNames.Set();cancel.Cancel();
            if(connecting is not null)try{(await connecting).Dispose();}catch(OperationCanceledException){}
            sourceReadyObserver?.Dispose();
            Directory.Delete(root,true);
        }
    }

    // reach: unit (direct loader input substitutes startup; exercises duplicate definition ownership, not host startup).
    [Fact]
    public void DuplicateNameKeepsItsFirstListPositionAndLastDefinition()
    {
        string root=Directory.CreateTempSubdirectory("first-definition-").FullName;
        try
        {
            string dir=Directory.CreateDirectory(Path.Combine(root,"animations")).FullName;
            File.WriteAllText(Path.Combine(dir,"a.json"),"{\"same\":[{\"Name\":\"EventKeyFrame\",\"triggerTime_ms\":1,\"event_id\":\"TAPPED_BLOCK\"}]}");
            File.WriteAllText(Path.Combine(dir,"z.json"),"{\"same\":[{\"Name\":\"EventKeyFrame\",\"triggerTime_ms\":99,\"event_id\":\"TAPPED_BLOCK\"}]}");
            var library=AnimationLibrary.Open(root);Assert.Equal(new[]{"same"},library.ClipNames);
            Assert.Equal(99u,library.GetClip("same")!.Keyframes.Single().TriggerTimeMs);
        }
        finally{Directory.Delete(root,true);}
    }

    // reach: unit (direct library loads substitute startup; checks the manager's resource-relative ordering for both accepted root shapes).
    [Fact]
    public void RelativeDefinitionOrderIsTheSameForResourcesAndAssetsRoots()
    {
        string root=Directory.CreateTempSubdirectory("relative-definition-order-").FullName;
        try
        {
            string assets=Directory.CreateDirectory(Path.Combine(root,"assets","animations")).FullName;
            string config=Directory.CreateDirectory(Path.Combine(root,"config","engine","animations")).FullName;
            File.WriteAllText(Path.Combine(assets,"z.json"),"{\"asset_clip\":[]}");
            File.WriteAllText(Path.Combine(config,"a.json"),"{\"config_clip\":[]}");
            Assert.Equal(new[]{"asset_clip","config_clip"},AnimationLibrary.Open(root).ClipNames);
            Assert.Equal(new[]{"asset_clip","config_clip"},AnimationLibrary.Open(Path.Combine(root,"assets")).ClipNames);
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

