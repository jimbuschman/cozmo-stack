using System.Collections.Concurrent;
using System.Buffers.Binary;

namespace Cozmo.Robot.Vision;

/// <summary>The engine's processed-image report. ImageMean is a raw sampled pixel mean, not a Boolean.</summary>
public sealed record ProcessedImageReport(uint Timestamp,IReadOnlyList<uint> Modes,byte ImageMean)
{
    internal const ushort UnionTag=0x43;
    internal byte[] PackBody()
    {
        var body=new byte[6+4*Modes.Count];
        BinaryPrimitives.WriteUInt32LittleEndian(body,Timestamp);body[4]=checked((byte)Modes.Count);
        for(int i=0;i<Modes.Count;i++)BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(5+i*4),Modes[i]);
        body[^1]=ImageMean;return body;
    }
}

public sealed partial class VisionSystem
{
    private sealed class PreparedVisionResult(int removal,EngineRobot? owner,Pose3d ownedPose,object poseTreeGate,Func<bool> consume):IDisposable
    {
        internal int Removal {get;}=removal;
        internal EngineRobot? Owner {get;}=owner;
        internal bool Consume()=>consume();
        public void Dispose(){lock(poseTreeGate)ownedPose.Dispose();}
    }
    private readonly ConcurrentQueue<PreparedVisionResult> _preparedResults=new();
    private readonly object _resultGate=new();
    public event Action<ProcessedImageReport>? ProcessedImage;
    internal int PendingResultCount=>_preparedResults.Count;

    // 0x006542EC initializes false on EACH drain. A failure promotes it; later
    // modes/results and every consumed-result timestamp/API tail still execute.
    private bool DrainPreparedResults()
    {
        bool failed=false;
        var drainOwner=_robot.Engine.Robot;
        int removal=Volatile.Read(ref _removals);
        bool CurrentDrain()=>!_disposed && !RemovedSince(removal) && ReferenceEquals(_robot.Engine.Robot,drainOwner) &&
            (drainOwner is null || !drainOwner.Lifetime.Destroying);
        while(true)
        {
            PreparedVisionResult? result;
            lock(_resultGate)
            {
                if(!CurrentDrain() || !_preparedResults.TryDequeue(out result))break;
            }
            using(result)
                if(result.Removal==removal && result.Owner is { } owner && ReferenceEquals(drainOwner,owner))
                {
                    bool resultFailed=result.Consume();
                    if(CurrentDrain())failed|=resultFailed;
                }
        }
        return CurrentDrain() && failed;
    }
    private void DiscardPreparedResults(EngineRobot? retiringOwner=null, bool discardAll=true)
    {
        var discarded=new List<PreparedVisionResult>();
        lock(_resultGate)
        {
            while(_preparedResults.TryDequeue(out var result))discarded.Add(result);
            if(!discardAll)
            {
                foreach(var result in discarded.Where(r=>!ReferenceEquals(r.Owner,retiringOwner)))_preparedResults.Enqueue(result);
                discarded.RemoveAll(r=>!ReferenceEquals(r.Owner,retiringOwner));
            }
        }
        foreach(var result in discarded)result.Dispose();
    }

    // ComputeMean 0x006B2CDC; mode8 at 0x006B5136..514A. All sampled
    // coordinates start at zero and advance by10; integer division truncates.
    private static byte SampleImageMean(GrayImage image)
    {
        int sum=0,count=0;
        for(int y=0;y<image.Height;y+=10)for(int x=0;x<image.Width;x+=10)
        {sum=unchecked(sum+image[x,y]);count++;}
        return (byte)Math.Clamp(sum/count,0,255);
    }
    private void PublishProcessedImage(EngineRobot owner,uint timestamp,ushort mask,byte mean,Func<bool> current)
    {
        var modes=Array.AsReadOnly(Enumerable.Range(0,16).Where(i=>(mask&(1<<i))!=0).Select(i=>(uint)i).ToArray());
        var report=new ProcessedImageReport(timestamp,modes,(mask&0x100)!=0?mean:(byte)0);
        if(!current() || !owner.HasExternalInterface)return;
        _robot.Engine.DeliverAnimationReportToApp(ProcessedImageReport.UnionTag,report.PackBody(),
            _robot.Engine.CaptureAppEvent(ProcessedImage,report));
        if(current())_robot.Engine.AnimationMessages.Deliver(ProcessedImageReport.UnionTag,true,report);
    }
}
