using System.Buffers.Binary;
using Cozmo.Transport;

namespace Cozmo.Robot;

public sealed record LatencyTimingInfo(float Average,float Minimum,float Maximum);
public sealed record ImageLatencyTimingInfo(float Average,float Minimum,float Maximum,float CurrentTimeMs);
public sealed record LatencyMessage(LatencyTimingInfo Wifi,LatencyTimingInfo ExternalSend,LatencyTimingInfo Send,
    LatencyTimingInfo ReceiveQueue,LatencyTimingInfo Unity,LatencyTimingInfo Sdk,ImageLatencyTimingInfo Image);
public sealed record RobotPokedReport;

public sealed partial class CozmoEngine
{
    // fidelity: M1-061 — the native static is shared across robots; one standalone engine owns its countdown.
    private int _latencyCountdown=10;
    private bool _gameBeingPaused;
    internal Func<double> NetTimeStampMs=()=>NetTimeStamp.NowMs; // M1-005: shared epoch, whole microseconds -> f64 ms.
    public event Action<LatencyMessage>? Latency;
    public event Action<RobotPokedReport>? RobotPoked;

    // fidelity: M1-062 — app input 85, handled at the normal engine game-input boundary (0x004EDF84).
    public void SetGameBeingPaused(bool paused)=>Post(()=>
    {
        _gameBeingPaused=paused; // engine+0x60, 0x004EDF88.
        Behavior.SteppedBehavior.ReportMissing("M1-062 / M6 SetGameBeingPaused AppIsInFocus 0x004EDF9A: audio focus recipient is unbuilt; no dedicated M6 focus record is assigned");
        Behavior.SteppedBehavior.ReportMissing("M15-016 SetGameBeingPaused: NeedsManager game-pause effects remain unbuilt");
    });

    // fidelity: M1-061 — 0x004ED842..0x004EDAB8. Called after UpdateAllRobots, state 3 only.
    private void UpdateLatencyInfo()
    {
        // kNetConnStatsUpdate is the shipped constant 1 (0x00C95F41).
        if(_gameBeingPaused)return;
        if(_latencyCountdown!=0){--_latencyCountdown;return;}
        _latencyCountdown=10;
        var queue=Rcm.ReceiveQueueStats.Selected;
        var zero=new LatencyTimingInfo(0,0,0);
        var empty=new LatencyTimingInfo(0,(float)double.MaxValue,(float)-double.MaxValue);
        var report=new LatencyMessage(zero,zero,zero,new((float)queue.Mean,(float)queue.Min,(float)queue.Max),empty,empty,
            new(0,(float)double.MaxValue,(float)-double.MaxValue,(float)((Robots.GetFirstRobot()?.CurrentImageLatencySeconds??0)*1000.0)));
        float[] words={report.Wifi.Average,report.Wifi.Minimum,report.Wifi.Maximum,
            report.ExternalSend.Average,report.ExternalSend.Minimum,report.ExternalSend.Maximum,
            report.Send.Average,report.Send.Minimum,report.Send.Maximum,
            report.ReceiveQueue.Average,report.ReceiveQueue.Minimum,report.ReceiveQueue.Maximum,
            report.Unity.Average,report.Unity.Minimum,report.Unity.Maximum,
            report.Sdk.Average,report.Sdk.Minimum,report.Sdk.Maximum,
            report.Image.Average,report.Image.Minimum,report.Image.Maximum,report.Image.CurrentTimeMs};
        var bytes=new byte[88];
        for(int i=0;i<words.Length;i++)BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(i*4),BitConverter.SingleToInt32Bits(words[i]));
        DeliverAnimationReportToApp(0x77,bytes,CaptureAppEvent(Latency,report));
        AnimationMessages.Deliver(0x77,true,report);
    }

    // fidelity: M2-024 — empty E2G 0x33; enqueue the app view before the local emit (0x00536754).
    internal void BroadcastRobotPoked()
    {
        var report=new RobotPokedReport();
        DeliverAnimationReportToApp(0x33,ReadOnlyMemory<byte>.Empty,CaptureAppEvent(RobotPoked,report));
        AnimationMessages.Deliver(0x33,true,report);
    }
}

public sealed partial class EngineRobot
{
    // fidelity: M1-061 — Impl+0x130 starts -1; Robot+0x3B8 starts zero.
    internal double LastCompletedImageEventTime { get; private set; }=-1.0;
    internal double CurrentImageLatencySeconds { get; private set; }
    internal void ObserveCompletedImageEvent(double eventTime)
    {
        if(eventTime!=LastCompletedImageEventTime)LastCompletedImageEventTime=eventTime;
    }
    private void UpdateCurrentImageLatency(float now)
    {
        double delta=(double)now-LastCompletedImageEventTime;
        CurrentImageLatencySeconds=delta>0?delta:0;
    }
}
