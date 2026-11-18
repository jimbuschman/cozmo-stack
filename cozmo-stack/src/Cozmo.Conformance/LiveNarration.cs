using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;

namespace Cozmo.Conformance;

/// <summary>Host display only. Engine observers enqueue facts; a separate host
/// timer writes them. It sends no commands and never infers physical movement
/// from an authored track. MCP passes its stderr writer here.</summary>
public sealed class LiveNarration : IDisposable
{
    private sealed record Fact(double At,string Kind,object Value);
    public sealed record ClipInfo(byte Tag,string Name,uint DurationMs,AnimationTrack Tracks,bool MetadataAvailable=true);
    public sealed record Playback(ClipInfo Clip,string Behavior,double StartedAt,double? EndedAt=null,string? Ending=null);
    private readonly ConcurrentQueue<Fact> _pending=new();
    private readonly Dictionary<byte,ClipInfo> _clips=new();
    private readonly Dictionary<byte,int> _active=new();
    private readonly Dictionary<byte,int> _last=new();
    private readonly List<Playback> _history=new();
    private readonly List<(double At,string Kind)> _signals=new();
    private readonly HashSet<(int,string)> _faces=new();
    private readonly HashSet<uint> _objects=new();
    private readonly object _gate=new();
    private readonly Action<string> _write;
    private readonly Action<JsonObject>? _record;
    private readonly Func<double> _now;
    private readonly Timer? _timer;
    private readonly List<Action> _detach=new();
    private string _behavior="Cozmo";
    private Exception? _failure;
    private bool _disposed;

    public LiveNarration(Action<string> write,Func<double> milliseconds,
        Action<JsonObject>? record=null,bool automaticFlush=true)
    {
        _write=write;_now=milliseconds;_record=record;
        if(automaticFlush)_timer=new Timer(_=>{try{Flush();}catch(Exception error){Interlocked.CompareExchange(ref _failure,error,null);}},null,50,50);
    }

    public void AttachEngine(CozmoEngine engine)
    {
        engine.BehaviorTransition+=Behavior;engine.ReactionTransition+=Reaction;
        engine.ActionCompleted+=Action;engine.RobotObservedFace+=Face;
        engine.RobotChangedObservedFaceId+=FaceId;engine.RobotObservedObject+=Object;
        engine.RobotDeletedFace+=FaceLost;engine.ObjectPoseUnknown+=ObjectLost;
        engine.LocatedObjectDeleted+=ObjectDeleted;engine.FaceRenamed+=Renamed;engine.FaceEnrollmentCompleted+=Enrolled;
        engine.EngineToGameMessage+=Game;
        _detach.Add(()=>{engine.BehaviorTransition-=Behavior;engine.ReactionTransition-=Reaction;
            engine.ActionCompleted-=Action;engine.RobotObservedFace-=Face;engine.RobotChangedObservedFaceId-=FaceId;
            engine.RobotDeletedFace-=FaceLost;engine.ObjectPoseUnknown-=ObjectLost;engine.LocatedObjectDeleted-=ObjectDeleted;
            engine.FaceRenamed-=Renamed;engine.FaceEnrollmentCompleted-=Enrolled;
            engine.RobotObservedObject-=Object;engine.EngineToGameMessage-=Game;});
    }
    public void AttachRobot(CozmoRobot robot)
    {
        robot.Animations.Scheduler.Diagnostic+=Diagnostic;
        robot.Message+=Message;robot.Sensors.OffTreadsStateChanged+=OffTreads;
        robot.Cubes.CubeTapped+=Tapped;
        _detach.Add(()=>{robot.Animations.Scheduler.Diagnostic-=Diagnostic;
            robot.Message-=Message;robot.Sensors.OffTreadsStateChanged-=OffTreads;robot.Cubes.CubeTapped-=Tapped;});
    }
    // Pure fact ingress is also used by the offline recording replay.
    public void RegisterClip(double at,ClipInfo clip)=>_pending.Enqueue(new(at,"clip",clip));
    public void ObserveBehavior(double at,BehaviorTransitionReport report)=>_pending.Enqueue(new(at,"behavior",report));
    public void ObserveReaction(double at,ReactionTransitionReport report)=>_pending.Enqueue(new(at,"reaction",report));
    public void RobotStarted(double at,byte tag)=>_pending.Enqueue(new(at,"started",tag));
    public void RobotEnded(double at,byte tag)=>_pending.Enqueue(new(at,"ended",tag));
    public void Aborted(double at,byte tag)=>_pending.Enqueue(new(at,"aborted",tag));
    private void Behavior(BehaviorTransitionReport report)=>ObserveBehavior(_now(),report);
    private void Reaction(ReactionTransitionReport report)=>ObserveReaction(_now(),report);
    private void Action(ActionCompletionReport report)=>_pending.Enqueue(new(_now(),"action",report));
    private void Face(RobotObservedFaceReport report)=>_pending.Enqueue(new(_now(),"face",report));
    private void FaceId(RobotChangedObservedFaceIdReport report)=>_pending.Enqueue(new(_now(),"face-id",report));
    private void FaceLost(RobotDeletedFaceReport report)=>_pending.Enqueue(new(_now(),"face-lost",report.FaceId));
    private void ObjectLost(ObjectPoseUnknownReport report)=>_pending.Enqueue(new(_now(),"object-lost",report.ObjectId));
    private void ObjectDeleted(LocatedObjectDeletedReport report)=>_pending.Enqueue(new(_now(),"object-deleted",report.ObjectId));
    private void Renamed(FaceRenamedReport report)=>_pending.Enqueue(new(_now(),"text",$"face {report.Id}: renamed to {report.Name}"));
    private void Enrolled(FaceEnrollmentCompletedReport report)=>_pending.Enqueue(new(_now(),"text",$"face enrollment: result {report.Result}, face {report.Id} {report.Name}"));
    private void Object(RobotObservedObjectReport report)=>_pending.Enqueue(new(_now(),"object",report));
    private void OffTreads(OffTreadsState old,OffTreadsState next)=>_pending.Enqueue(new(_now(),"text",$"off treads: {old} -> {next}"));
    private void Tapped(Cube cube)=>_pending.Enqueue(new(_now(),"cube-tap",cube.WorldObjectId is {} id?
        $"cube {id}: tapped":$"cube {cube.FactoryId:x8}: tapped (world ID unavailable)"));
    private void Message(RobotMessage message)
    {
        if(message is AnimationStarted started)RobotStarted(_now(),started.AnimId);
        else if(message is AnimationEnded ended)RobotEnded(_now(),ended.AnimId);
    }
    private void Diagnostic(AnimationDiagnostic observation)
    {
        if(observation is AnimationClipInitializedObservation clip)
            RegisterClip(_now(),new(clip.Tag,clip.Name,clip.DurationMs,clip.Tracks));
    }
    private void Game(ushort tag,ReadOnlyMemory<byte> body)
    {
        if(tag==0x5F&&body.Length==4)
        {uint id=System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(body.Span);if(id<=byte.MaxValue)Aborted(_now(),(byte)id);}
        else if(tag==0x0F&&body.Length==21)
        {var moved=ObjectMoved.Read(new CladReader(body));_pending.Enqueue(new(_now(),"cube-move",$"cube {moved.ObjectID}: moving"));}
        else if(tag==0x10&&body.Length==8)
        {var stopped=ObjectStoppedMoving.Read(new CladReader(body));_pending.Enqueue(new(_now(),"text",$"cube {stopped.ObjectID}: stopped"));}
        else if(tag==ObjectConnectionStateMessage.UnionTag&&body.Length==13)
        {
            var reader=new CladReader(body);uint id=reader.U32();reader.U32();reader.I32();bool connected=reader.U8()!=0;
            _pending.Enqueue(new(_now(),"text",$"cube {id}: {(connected?"connected":"disconnected")}"));
        }
    }
    public void Flush()
    {
        lock(_gate)
        {
            if(_disposed)return;
            if(_failure is {} failure)throw new InvalidOperationException("Live narration output failed.",failure);
            while(_pending.TryDequeue(out var fact))Apply(fact);
        }
    }
    private void Apply(Fact fact)
    {
        if(fact.Kind is "cube-tap" or "cube-move")_signals.Add((fact.At,fact.Kind));
        switch(fact.Kind)
        {
            case "clip":
                var clip=(ClipInfo)fact.Value;_clips[clip.Tag]=clip;
                _record?.Invoke(new(){["atMs"]=fact.At,["event"]="clip-initialized",["tag"]=clip.Tag,["name"]=clip.Name,
                    ["durationMs"]=clip.DurationMs,["tracks"]=TrackText(clip.Tracks),["trackMask"]=(int)clip.Tracks});
                return; // Initialization is not a robot start.
            case "behavior":
                var behavior=(BehaviorTransitionReport)fact.Value;
                _behavior=((BehaviorID)behavior.NewId).ToString();
                if(behavior.OldId!=behavior.NewId)Say(fact.At,$"behavior: {(BehaviorID)behavior.OldId} -> {_behavior}");
                return;
            case "reaction":
                var reaction=(ReactionTransitionReport)fact.Value;
                if(reaction.OldTrigger!=reaction.NewTrigger)Say(fact.At,$"reaction: {ReactionName(reaction.OldTrigger)} -> {ReactionName(reaction.NewTrigger)}");
                return;
            case "started":
                byte tag=(byte)fact.Value;
                var selected=_clips.GetValueOrDefault(tag)??new ClipInfo(tag,$"animation tag {tag} (clip unavailable)",0,AnimationTrack.None,false);
                if(_active.Remove(tag,out int previous))
                    _history[previous]=_history[previous] with{EndedAt=fact.At,Ending="restarted without an end report"};
                var playback=new Playback(selected,_behavior,fact.At);_active[tag]=_last[tag]=_history.Count;_history.Add(playback);
                Say(fact.At,$"{playback.Behavior}: playing {selected.Name} "+
                    (selected.MetadataAvailable?$"({Seconds(selected.DurationMs)} s, {TrackText(selected.Tracks)})":"(duration and tracks unavailable)"));
                return;
            case "ended":case "aborted":
                byte endTag=(byte)fact.Value;
                if(_active.Remove(endTag,out int index))
                {
                    var active=_history[index];_history[index]=active with{EndedAt=fact.At,Ending=fact.Kind};
                    Say(fact.At,$"{active.Behavior}: {fact.Kind} {active.Clip.Name} after {Seconds(fact.At-active.StartedAt)} s");
                }
                else if(_last.TryGetValue(endTag,out int earlier))
                {
                    var priorPlayback=_history[earlier];
                    Say(fact.At,$"{priorPlayback.Behavior}: {fact.Kind} report for {priorPlayback.Clip.Name} after {priorPlayback.Ending} "+
                        $"({Seconds(fact.At-priorPlayback.StartedAt)} s since robot start)");
                }
                else if(fact.Kind=="aborted"&&_clips.TryGetValue(endTag,out var pending))
                    Say(fact.At,$"{_behavior}: aborted {pending.Name} before a robot start report");
                else Say(fact.At,$"animation tag {endTag}: {fact.Kind} (start not observed)");
                return;
            case "action":
                var action=(ActionCompletionReport)fact.Value;
                string result=Enum.IsDefined(typeof(ActionResult),action.Result)?((ActionResult)action.Result).ToString():$"0x{action.Result:X8}";
                Say(fact.At,$"action {action.Tag}{(action.AnimationName is {} name?" "+name:"")}: {result}");
                return;
            case "face":
                var face=(RobotObservedFaceReport)fact.Value;
                if(_faces.Add((face.FaceId,face.Name)))Say(fact.At,$"face {face.FaceId}: seen{(string.IsNullOrEmpty(face.Name)?"":" "+face.Name)}");
                return;
            case "face-id":
                var identity=(RobotChangedObservedFaceIdReport)fact.Value;
                Say(fact.At,$"face identity: {identity.OldId} -> {identity.NewId}");return;
            case "face-lost":
                int lost=(int)fact.Value;_faces.RemoveWhere(face=>face.Item1==lost);
                Say(fact.At,$"face {lost}: lost");return;
            case "object-lost":case "object-deleted":
                uint lostObject=(uint)fact.Value;_objects.Remove(lostObject);
                Say(fact.At,$"object {lostObject}: {(fact.Kind=="object-lost"?"pose lost":"deleted")}");return;
            case "object":
                var observed=(RobotObservedObjectReport)fact.Value;
                if(_objects.Add(observed.ObjectId))Say(fact.At,$"{(ObjectType)observed.Type} {observed.ObjectId}: seen");return;
            default:Say(fact.At,(string)fact.Value);return;
        }
    }
    private void Say(double at,string text)
    {
        long seconds=(long)Math.Max(0,at)/1000;
        string line=$"[{seconds/60:00}:{seconds%60:00}] {text}";
        _write(line);_record?.Invoke(new(){["atMs"]=at,["event"]="line",["line"]=line});
    }
    private static string ReactionName(byte id)=>id==0x16?"None":((ReactionTrigger)id).ToString();
    private static string Seconds(double ms)=>(ms/1000).ToString("0.0",CultureInfo.InvariantCulture);
    public static string TrackText(AnimationTrack tracks)
    {
        var names=new List<string>();
        foreach(var (track,name) in new[]{(AnimationTrack.Audio,"sound"),(AnimationTrack.Face,"face"),
            (AnimationTrack.Head,"head"),(AnimationTrack.Lift,"lift"),(AnimationTrack.Body,"body"),(AnimationTrack.Lights,"lights")})
            if((tracks&track)!=0)names.Add(name);
        return names.Count==0?"no authored output tracks":string.Join("+",names);
    }
    public Playback[] Playbacks(double start,double end)
    {Flush();lock(_gate)return _history.Where(p=>p.StartedAt>=start&&p.StartedAt<=end).ToArray();}
    public bool SawSignal(string kind,double start,double end)
    {Flush();lock(_gate)return _signals.Any(signal=>signal.Kind==kind&&signal.At>=start&&signal.At<=end);}
    public bool ObservationSettled(double start,double end,int minimumMs=5000)
    {
        var plays=Playbacks(start,end);
        return end>=start+minimumMs&&!plays.Any(p=>p.Clip.MetadataAvailable&&p.Clip.DurationMs>0&&p.EndedAt is null);
    }
    public string ObservationQuestion(double start,double end,string fallback)
    {
        var clips=Playbacks(start,end);
        return clips.Length==0?"No animation start was reported in this window. "+fallback:
            "The robot was sent "+string.Join("; ",clips.Select(p=>$"{p.Clip.Name} with "+
                (p.Clip.MetadataAvailable?TrackText(p.Clip.Tracks):"tracks unavailable")+
                (p.Ending is null?" (end not reported in this window)":$" ({p.Ending})")))+". Did it do that?";
    }
    public void Dispose()
    {
        _timer?.Dispose();
        foreach(var detach in _detach)detach();
        Flush();lock(_gate)_disposed=true;
    }
}
