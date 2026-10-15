using System.Collections.Concurrent;
using System.Reflection;
using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Cozmo.Transport;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("ObjectID process statics")]
public sealed class M11LifetimeRowsTests
{
    private static CameraFrame Frame(uint id = 1) => new() { ImageId=id, Timestamp=id, Width=320, Height=240, Encoding=8 };
    private static readonly CameraCalibration Calibration = CameraCalibration.Nominal();

    [Fact]
    public async Task DisposalWaitsForTheWorkerAndItsExitLogBeforePrintingTimers()
    {
        using var robot = CozmoRobot.CreateOffline();
        using var vision = new VisionSystem(robot, Calibration) { Enabled=true };
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var entries = new ConcurrentQueue<(EngineLogEntry Entry,int Thread,string? ThreadName)>();
        robot.Engine.LogEntry += e => entries.Enqueue((e,Environment.CurrentManagedThreadId,Thread.CurrentThread.Name));
        vision.Log += text => { if (text.Contains("policy M3-020")) { entered.Set(); release.Wait(); } };
        // Explicit native timer input; no assertion about unbuilt Tic/Toc producers.
        var timer=vision.LifetimeProfiler.CreateTimer("held-frame"); timer.Total=10;timer.Calls=3;
        vision.HandOverFrame(Frame());
        Assert.True(entered.Wait(3000));
        var disposal=Task.Run(vision.Dispose);
        try
        {
            // The old removal wait is two seconds; destructor join must outlast it.
            await Task.Delay(2150);
            Assert.False(disposal.IsCompleted);
            Assert.DoesNotContain(entries,e=>e.Entry.Text.Contains("Terminated Robot"));
        }
        finally { release.Set(); }
        await disposal.WaitAsync(TimeSpan.FromSeconds(5));
        var logs=entries.ToArray();
        var debug=logs.Where(e=>e.Entry.Name=="ImageBase.CloseAllDisplayWindows.NoDisplayOnAndroidOrIOS").ToArray();
        Assert.Equal(2,debug.Length);
        Assert.All(debug,e=> { Assert.Equal(EngineLogLevel.Debug,e.Entry.Level);Assert.Equal("Unnamed",e.Entry.Channel);Assert.Equal("",e.Entry.Text); });
        int exit=Array.FindIndex(logs,e=>e.Entry.Text=="Terminated Robot VisionComponent::Processor thread");
        int print=Array.FindIndex(logs,e=>e.Entry.Text.StartsWith("held-frame averaged"));
        Assert.True(exit>Array.FindLastIndex(logs,e=>e.Entry.Level==EngineLogLevel.Debug));
        Assert.True(print>exit);
        Assert.Equal(EngineLogLevel.Info,logs[exit].Entry.Level);
        Assert.Equal("Unnamed",logs[exit].Entry.Channel);
        Assert.Equal("VisionComponent.Processor",logs[exit].Entry.Name);
        Assert.NotEqual(debug[0].Thread,logs[exit].Thread);
        Assert.Equal("VisionSystem",logs[exit].ThreadName);
        Assert.DoesNotContain(logs,e=>e.Entry.Text.Contains("MISSING")||e.Entry.Name.Contains("MISSING"));
        int count=logs.Length;
        vision.Dispose(); vision.HandOverFrame(Frame(2));
        Assert.Equal(count,entries.Count);
    }

    [Fact]
    public void EmptyWorkerStillLivesUntilShutdownAndTheSkipByteRetainsTheMailbox()
    {
        using var robot=CozmoRobot.CreateOffline();
        using var vision=new VisionSystem(robot) { Enabled=true, SkipProcessorPass=true };
        var entries=new ConcurrentQueue<EngineLogEntry>();robot.Engine.LogEntry+=entries.Enqueue;
        var messages=new ConcurrentQueue<string>();vision.Log+=messages.Enqueue;
        vision.HandOverFrame(Frame());
        Assert.True(SpinWait.SpinUntil(()=>entries.Any(e=>e.Text=="Starting Robot VisionComponent::Processor thread..."),3000));
        Thread.Sleep(20);
        Assert.Empty(messages);
        Assert.DoesNotContain(entries,e=>e.Text.Contains("Terminated Robot"));
        vision.SkipProcessorPass=false;
        vision.WaitForProcessorIdle();
        Assert.Contains(messages,e=>e.Contains("Must be initialized"));
        Assert.DoesNotContain(entries,e=>e.Text.Contains("Terminated Robot"));
        vision.Dispose();
        Assert.Single(entries,e=>e.Text=="Terminated Robot VisionComponent::Processor thread");
    }

    [Fact]
    public void NoThreadHandleEmitsOnlyTheTwoWindowLogs()
    {
        using var robot=CozmoRobot.CreateOffline();
        var vision=new VisionSystem(robot);
        var logs=new List<EngineLogEntry>();robot.Engine.LogEntry+=logs.Add;
        vision.Dispose();
        Assert.Equal(2,logs.Count);
        Assert.All(logs,e=>Assert.Equal("ImageBase.CloseAllDisplayWindows.NoDisplayOnAndroidOrIOS",e.Name));
    }

    [Fact]
    public void ProfilerUsesF64SignedDeltaGuardsAndSavesAfterTheCall()
    {
        using var profiler=new LifetimeProfiler();
        var timer=profiler.CreateTimer("T");
        timer.Total=10;timer.Calls=3;timer.SavedTotal=3;timer.SavedCalls=1;
        var logs=new List<string>();
        profiler.SourceLog=(level,channel,key,text)=>
        {
            Assert.Equal(EngineLogLevel.Info,level);Assert.Equal("Profiler",channel);Assert.Equal("Profiler",key);
            if(logs.Count==0) { Assert.Equal(3,timer.SavedTotal);Assert.Equal(1,timer.SavedCalls); }
            logs.Add(text);
        };
        profiler.Print(timer);
        Assert.Equal("T averaged 3.3333ms over 3 calls (3.5000ms over 2 calls since last print)",logs[0]);
        Assert.Equal(10,timer.SavedTotal);Assert.Equal(3,timer.SavedCalls);
        timer.Total=-1;timer.Calls=-2;
        profiler.Print(timer);
        Assert.Equal("T averaged 0.0000ms over -2 calls (0.0000ms over -5 calls since last print)",logs[1]);
        timer.Total=9007199254740993;timer.Calls=1;
        profiler.Print(timer);
        Assert.Equal("T averaged 9007199254740992.0000ms over 1 calls (3002399751580331.5000ms over 3 calls since last print)",logs[2]);
    }

    [Fact]
    public void ProfilerPreservesTimerIdentityAndTheApprovedInsertionOrderIncludingZeroCalls()
    {
        using var robot=CozmoRobot.CreateOffline();
        var vision=new VisionSystem(robot);
        vision.LifetimeProfiler.ApplyVisionPerformanceLoggingNames();
        vision.LifetimeProfiler.CreateTimer("same");
        vision.LifetimeProfiler.CreateTimer("middle");
        vision.LifetimeProfiler.CreateTimer("same");
        var logs=new List<EngineLogEntry>();robot.Engine.LogEntry+=logs.Add;
        vision.Dispose();
        var timers=logs.Where(e=>e.Name=="VisionSystem.Profiler").ToArray();
        Assert.Equal(new[]{"same","middle","same"},timers.Select(e=>e.Text.Split(' ')[0]));
        Assert.All(timers,e=> { Assert.Equal("VisionSystem",e.Channel);Assert.EndsWith("0.0000ms over 0 calls since last print)",e.Text); });
    }

    public class DeleteProxy : DispatchProxy
    {
        internal int Result, Deletes;
        internal Action? BeforeDelete;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch
        {
            "get_IsAvailable" => true,
            "get_Description" => "native handle input",
            "CreateCommon" => new OkaoCommonHandle(),
            "CreateAlbum" => new OkaoAlbumHandle(),
            "DeleteAlbumHandle" => Delete(),
            _ => throw new InvalidOperationException(method.Name)
        };
        private int Delete() { Deletes++;BeforeDelete?.Invoke();return Result; }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    public void AlbumDeleteRetainsTheHandleDuringTheCallThenClearsEvenOnError(int result)
    {
        bool old=EngineErrorState.ErrorFlagSet;
        try
        {
            var seam=DispatchProxy.Create<IOkaoFaceRecognizer,DeleteProxy>();
            var proxy=(DeleteProxy)(object)seam;proxy.Result=result;
            using var robot=CozmoRobot.CreateOffline();
            var vision=new VisionSystem(robot,okaoRecognizer:seam);
            var original=vision.Recognizer.AlbumHandle;
            proxy.BeforeDelete=()=>Assert.Same(original,vision.Recognizer.AlbumHandle);
            var logs=new List<EngineLogEntry>();robot.Engine.LogEntry+=logs.Add;
            var source=new List<(EngineLogLevel Level,string? Channel,string Key,string Text)>();
            vision.Recognizer.SourceLog=(l,c,k,t)=> { source.Add((l,c,k,t));robot.Engine.EmitSourceLog(l,c,k,t); };
            EngineErrorState.ErrorFlagSet=false;
            vision.Dispose();
            Assert.Null(vision.Recognizer.AlbumHandle);Assert.Equal(1,proxy.Deletes);
            var errors=logs.Where(e=>e.Level==EngineLogLevel.Error).ToArray();
            Assert.Equal(result!=0,EngineErrorState.ErrorFlagSet);
            if(result==0)Assert.Empty(errors);
            else { var e=Assert.Single(errors);Assert.Equal("FaceRecognizer.Shutdown.FaceLibAlbumHandleDeleteFail",e.Name);Assert.Null(e.Channel);var raw=Assert.Single(source);Assert.Equal("",raw.Text);Assert.Equal(e.Name,raw.Key); }
            Assert.Equal(0,vision.Recognizer.Shutdown());Assert.Equal(1,proxy.Deletes);
        }
        finally { EngineErrorState.ErrorFlagSet=old; }
    }
}
