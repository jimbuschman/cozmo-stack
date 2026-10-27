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
    private sealed class PreparedVisionResult(int removal,Pose3d ownedPose,object poseTreeGate,Func<bool> consume):IDisposable
    {
        internal int Removal {get;}=removal;
        internal bool Consume()=>consume();
        public void Dispose(){lock(poseTreeGate)ownedPose.Dispose();}
    }
    private readonly ConcurrentQueue<PreparedVisionResult> _preparedResults=new();
    public event Action<ProcessedImageReport>? ProcessedImage;
    internal int PendingResultCount=>_preparedResults.Count;

    // 0x006542EC initializes false on EACH drain. A failure promotes it; later
    // modes/results and every consumed-result timestamp/API tail still execute.
    private bool DrainPreparedResults()
    {
        bool failed=false;
        while(_preparedResults.TryDequeue(out var result))
        {
            using(result)
                if(!RemovedSince(result.Removal))failed|=result.Consume();
        }
        return failed;
    }
    private void DiscardPreparedResults()
    {while(_preparedResults.TryDequeue(out var result))result.Dispose();}

    // ComputeMean 0x006B2CDC; mode8 at 0x006B5136..514A. All sampled
    // coordinates start at zero and advance by10; integer division truncates.
    private static byte SampleImageMean(GrayImage image)
    {
        int sum=0,count=0;
        for(int y=0;y<image.Height;y+=10)for(int x=0;x<image.Width;x+=10)
        {sum=unchecked(sum+image[x,y]);count++;}
        return (byte)Math.Clamp(sum/count,0,255);
    }
    private void PublishProcessedImage(uint timestamp,ushort mask,byte mean)
    {
        var modes=Array.AsReadOnly(Enumerable.Range(0,16).Where(i=>(mask&(1<<i))!=0).Select(i=>(uint)i).ToArray());
        var report=new ProcessedImageReport(timestamp,modes,(mask&0x100)!=0?mean:(byte)0);
        if(_robot.Engine.Robot?.HasExternalInterface!=true)return;
        _robot.Engine.DeliverAnimationReportToApp(ProcessedImageReport.UnionTag,report.PackBody(),
            _robot.Engine.CaptureAppEvent(ProcessedImage,report));
        _robot.Engine.AnimationMessages.Deliver(ProcessedImageReport.UnionTag,true,report);
    }
}
