using Cozmo.Protocol;
using Cozmo.Robot.Manipulation;
using Cozmo.Transport;
namespace Cozmo.Robot;

internal sealed class PathGoalIndex { internal byte Value; }
internal sealed record PathPlannerModelResult(int Status,IReadOnlyList<PathSegment> Path,byte Index);
public sealed partial class CozmoEngine
{
    internal Action<EngineRobot>? PathComponentConstructed;
    internal Action<sbyte>? PathSegmentRoute;
    internal Action? PathComponentTick;
}
// fidelity: M12-032, M12-002 — extends the SAME Robot+0x5C object as Abort/ClearPath.
internal sealed partial class PathComponent
{
    internal Func<RobotMessage,bool>? SendPathMessage;
    private Func<bool>? _scopedCurrent;
    private Func<RobotMessage,bool>? _scopedSend;
    private long _scopeGeneration;
    internal long ScopeGeneration=>_scopeGeneration;
    private bool ScopeCurrent()=>_scopedCurrent?.Invoke()??true;
    private IReadOnlyList<PathSegment> _dolerPath=Array.Empty<PathSegment>();
    internal ushort LastDoledIndex=0xFFFF;
    internal uint DoleSegmentCount;
    private PathPlannerModelResult? _plan;
    private NativePlannerSet? _nativePlanners;
    internal NativeShortPlanner NativeFace=null!,NativeMinimal=null!;
    private void InitNativeShortPlanners()
    {
        SourceLogSink source=(level,channel,key,text)=>_robot.Engine.EmitSourceLog(level,channel,key,text);
        Action<string> missing=text=>_robot.Engine.EmitStackLog("MISSING: "+text);
        NativeFace=new(false,source,missing);NativeMinimal=new(true,source,missing);
    }
    internal Func<bool>? PlannerPathIsSafe;
    internal Func<bool>? PlannerSkipSafetyCheck;
    internal Func<bool>? ObjectsChangedInput;
    internal Action? RejiggerInput;
    // fidelity: M12-032 — the Robot whose PoseOriginList (Robot+0x294: current ID +0x10, current origin +0x14) and drive-center
    // pose (Robot+0x2A4, GetDriveCenterPose 0x005477A0) the origin dispatch and the Rejigger read. Bound by CozmoRobot.
    internal Func<EngineRobot?>? OriginOwnerInput;
    // RETIRE-LEGACY-DRIVE-API-1 (operator 2026-11-13): the legacy StartDrivingToPose(int, ...) entry, its borrowed-goal holds and its planner
    // model are retired; every path starts through StartNativeDrivingToPose.
    // fidelity: M12-032 — the selected planner pointer pc+0x20 (set by SelectPlannerHelper 0x0064A7EC, cleared by ExecuteCustomPath 0x0064B1E6).
    private bool PlannerSelected=>_nativePlanners?.Current is not null;
    internal uint ParamsOrigin {get=>PlannerObjectsField0C;set=>PlannerObjectsField0C=value;}
    private RetainedSubscription? _pathSubscription;
    // Native C6 subscribes once in the Path ctor; this host binding helper is not a native re-Init path.
    internal void Attach(EngineRobot owner){AttachSpeedChooser(owner);RetainedSubscription.Replace(ref _pathSubscription,owner.SubscribeMessage(new PathFollowingEvent().Id,m=>owner.ActionList.WithGate(()=>OnPathEvent((PathFollowingEvent)m)),"Path"));}
    // fidelity: M2-020 — destructor Abort precedes C6 release, 0x00649006..0x0064900A.
    internal void DestroyRawOwner(EngineRobot robot)
    {
        Abort(robot.SendChecked);
        _pathSubscription?.Dispose();_pathSubscription=null;
        _speedChooserSubscription?.Dispose();_speedChooserSubscription=null;
        Behavior.SteppedBehavior.ReportMissing("M2-020 PathComponent destructor after C6 release 0x0064900A: remaining planner/doler destruction at this lifetime point is not built; existing managed reset remains separate");
    }
    private PathGoalIndex? _goalIndex;
    internal int PlannerStatus => _nativePlanners?.Current?.CheckPlanningStatus??_plan?.Status??1;
    internal void SetNativePlanners(NativePlannerSet planners)=>_nativePlanners=planners;
    internal PathMotionProfile NativeMotionProfile=PathMotionProfile.Default;
    // 0x0064AB10 / 0x0064AC4A: copy the shared slot before enabling; clear only the flag.
    internal void SetNativeCustomMotionProfile(PathMotionProfile profile)
    {
        if(Field48) _robot.Engine.EmitStackLog("PathComponent.SetCustomMotionProfile: profile already set");
        if(!profile.IsCustom) _robot.Engine.EmitStackLog("PathComponent.SetCustomMotionProfile: profile is not custom");
        NativeMotionProfile=profile with {};
        Field48=true;
    }
    internal void ClearNativeCustomMotionProfile()=>Field48=false;
    // fidelity: M12-032 — P13 (signature at 0x0064ACC4: vector, shared_ptr<uchar>, bool): no ignore-object input; the stack's pass-through is dropped (Q5).
    internal int StartNativeDrivingToPose(ManipulationSystem m,IReadOnlyList<Vision.Pose3d> originalGoals,PathGoalIndex index,bool manual,EngineRobot? scopedOwner=null,Func<bool>? current=null)
    {
        // 64ACC4: the empty-vector failure precedes every write/old-plan cancellation.
        if(originalGoals.Count==0)return 1;
        if(current is not null&&!current())return 1;
        var owner=scopedOwner??m.Robot.Engine.Robot??throw new NativeActionUnavailable("M12-032 actual Robot owner unavailable");
        if(owner.CarryingOwner is null)throw new NativeActionUnavailable("M12-032 actual carrying owner unavailable");
        Func<RobotMessage,bool>? send=scopedOwner is null?null:message=>m.Paths.SendOwned(scopedOwner,current!,message);
        long generation=++_scopeGeneration;
        Func<bool>? scopedGuard=current is null?null:()=>generation==_scopeGeneration&&current();
        _scopedCurrent=scopedGuard;_scopedSend=send;
        if(Field47){PlannerSlot10?.Invoke();if(current is not null&&!current())return 1;Field47=false;}
        if(scopedGuard is not null&&!scopedGuard())return 1;
        if(IsActive)Abort(send??SendNative,scopedGuard);
        if(scopedGuard is not null&&!scopedGuard())return 1;
        Field46=manual;_goalIndex=index;SharedPath=index;ParamsOrigin=owner.CurrentOriginId;
        if(!owner.TryGetNativeOriginOwner(ParamsOrigin,out var currentOrigin)||currentOrigin is null)
            throw new NativeActionUnavailable("M12-032 actual current origin owner unavailable");
        using var start=new Vision.Pose3d(owner.GetCheckedForwardPose());
        if(!Vision.PoseTreeNode.HasSameRoot(start.Node,currentOrigin.Node)){SetStatus(0);return 1;}
        var goals=new List<Vision.Pose3d>();
        try
        {
            foreach(var original in originalGoals)
            {
                var goal=new Vision.Pose3d(original);goals.Add(goal);
                float offset=(owner.CarryingOwner.CarriedObjectId??uint.MaxValue)==uint.MaxValue?-20f:0f;
                float yaw=(float)goal.AngleAroundZ;
                float x=(float)goal.Translation.X+MathF.Cos(yaw)*offset,y=(float)goal.Translation.Y+MathF.Sin(yaw)*offset;
                goal.Node!.Transform=new(goal.Quaternion,new Vision.Vec3(x,y,0));
                if(!Vision.PoseTreeNode.HasSameRoot(goal.Node,currentOrigin.Node)){SetStatus(0);return 1;}
            }
            var planners=m.GetNativePlanners();planners.ConfigureNative(start,goals,NativeMotionProfile);SetNativePlanners(planners);
            if(scopedGuard is not null&&!scopedGuard())return 1;
            ObjectsChangedInput=()=>m.World.DidObjectsChange;
            PlannerSlot10=planners.Stop;
            SelectPlanner(goals.Count);
            if(!StartPlanner()){if(scopedGuard?.Invoke()??true)SetStatus(0,scopedGuard);return 1;}
            if(scopedGuard is not null&&!scopedGuard())return 1;
            SetStatus(1,scopedGuard);
            if(scopedGuard is not null&&!scopedGuard())return 1;
            if(!Field48)NativeMotionProfile=ChooseNativeProfile(owner,originalGoals);
            planners.Profile=NativeMotionProfile;
            return 0;
        }
        finally{foreach(var goal in goals)goal.Dispose();}
    }
    private bool StatusMask(uint mask){int shift=DriveToPoseStatus&0x7F;return shift<32 && ((mask>>shift)&1)!=0;} // ARM register LSR, not C#'s modulo-32 shift.
    internal bool IsActive => StatusMask(0xE);
    internal bool HasPathToFollow => StatusMask(0xC);
    internal bool LastPathFailed => StatusMask(0x41);
    internal bool IsWaitingToCancelPath => StatusMask(0x60);
    internal bool IsWaitingForRobotResponse => StatusMask(0x64);
    private void Info(string key,string text)=>_robot.Engine.EmitSourceLog(EngineLogLevel.Info,"Planner",key,text);
    private void Warning(string key,string text)=>_robot.Engine.EmitSourceLog(EngineLogLevel.Warning,null,key,text);
    private void Error(string key,string text){_robot.Engine.EmitSourceLog(EngineLogLevel.Error,null,key,text);EngineErrorState.StoreAndMaybeBreak();}
    private void Verify(string key,string text,Func<bool>? current=null){_robot.Engine.EmitSourceLog(EngineLogLevel.Error,null,key,text);if(current is not null&&!current())return;EngineErrorState.ErrorFlagSet=true;_robot.Engine.Das.ForceFlushNow();}
    private string Name(int status)=>StatusName?.Invoke(status)??StatusNames[status];
    internal void SetStatus(int next,Func<bool>? current=null)
    {
        if(next==DriveToPoseStatus)return;
        string text=$"{Name(DriveToPoseStatus)} -> {Name(next)}";
        if(current is not null&&!current())return;
        Info("PathComponent.TransitionStatus",text);
        if(current is null||current())DriveToPoseStatus=next;
    }
    private bool SendNative(RobotMessage message)=>!ScopeCurrent()?false:_scopedSend?.Invoke(message)??SendPathMessage?.Invoke(message)??(message is AppendPathSegmentLine or AppendPathSegmentArc or AppendPathSegmentPointTurn?_robot.SendToHandler(message):_robot.SendMessage(message));
    // fidelity: M13-029 - ErasePath builds a local Viz tag21 and sends nothing.
    private void ErasePath() => ErasePathHook?.Invoke(CozmoEngine.RobotId);
    private void ResetMachine(){_scopeGeneration++;_scopedCurrent=null;_scopedSend=null;_pathSubscription?.Dispose();_pathSubscription=null;_nativePlanners?.Dispose();_nativePlanners=null;InitNativeShortPlanners();PlannerSlot10=null;_plan=null;_goalIndex=null;ParamsOrigin=0;ClearDoler();}
    internal void ClearDoler(){_dolerPath=Array.Empty<PathSegment>();LastDoledIndex=0xFFFF;DoleSegmentCount=0;}
    internal void SetDolerPath(IReadOnlyList<PathSegment> path,Func<bool>? current=null,Func<RobotMessage,bool>? send=null)
    {
        _dolerPath=path.ToArray();LastDoledIndex=0xFFFF;DoleSegmentCount=(uint)(byte)path.Count;Dole(6,current,send);
    }
    internal void Dole(int count,Func<bool>? current=null,Func<RobotMessage,bool>? send=null)
    {
        int start=(short)LastDoledIndex+1;
        uint end=unchecked((uint)((short)LastDoledIndex+count));
        if(end>=DoleSegmentCount)end=unchecked(DoleSegmentCount-1);
        _robot.Engine.EmitSourceLog(EngineLogLevel.Debug,"Unnamed","PathDolerOuter",$"should dole from {start} to {end} (totalSegments = {DoleSegmentCount})");
        if(current is not null&&!current())return;
        if(DoleSegmentCount==0){MissingOnce("M1-034/M12-002 Dole empty-path unchecked segment read 0x00507F10 skipped, prior counter retained");return;}
        for(int i=start;unchecked((uint)i)<=end && i<_dolerPath.Count;i++)
        {
            _robot.Engine.EmitSourceLog(EngineLogLevel.Debug,"Unnamed","PathDolerOuter",$"doling out basestation idx {i} :  ");
            if(current is not null&&!current())return;
            MissingOnce("M12-002 PathSegment.Print 0x00507F36 -> 0x0085C034 text/body has no checked row; segment print is unbuilt");
            if(current is not null&&!current())return;
            RobotMessage? message=_dolerPath[i] switch
            {
                PathSegment.Line l=>new AppendPathSegmentLine{XStartMm=l.FromX,YStartMm=l.FromY,XEndMm=l.ToX,YEndMm=l.ToY,Speed=new(){SpeedMmps=l.SpeedMmps,AccelMmps2=l.AccelMmps2,DecelMmps2=l.DecelMmps2}},
                PathSegment.Arc a=>new AppendPathSegmentArc{XCenterMm=a.CenterX,YCenterMm=a.CenterY,RadiusMm=a.RadiusMm,StartRad=a.StartAngleRad,SweepRad=a.SweepRad,Speed=new(){SpeedMmps=a.SpeedMmps,AccelMmps2=a.AccelMmps2,DecelMmps2=a.DecelMmps2}},
                PathSegment.PointTurn t=>new AppendPathSegmentPointTurn{XMm=t.X,YMm=t.Y,TargetAngleRad=t.TargetAngleRad,AngleToleranceRad=t.AngleToleranceRad,UseShortestDirection=t.UseShortestDirection,Speed=new(){SpeedMmps=t.SpeedRadPerSec,AccelMmps2=t.AccelRadPerSec2,DecelMmps2=t.DecelRadPerSec2}},_=>null
            };
            if(message is null)
            {Error("Invalid path segment","Can't send path segment of unknown type");return;}
            bool sent=(send??SendNative)(message);
            if(current is not null&&!current())return;
            if(!sent){Error("PathDolerOuter.Dole","ERROR: failed to send message!");return;}
            LastDoledIndex=unchecked((ushort)i);
        }
    }
    internal void UpdateDoler(sbyte segment,Func<bool>? current=null,Func<RobotMessage,bool>? send=null)
    {
        int d=segment-(short)LastDoledIndex;
        if(d < -4 || unchecked(DoleSegmentCount-1)<=unchecked((uint)(short)LastDoledIndex))return;
        _robot.Engine.EmitSourceLog(EngineLogLevel.Debug,"Unnamed","PathDolerOuter.Update.Doling",$"currPathIdx: {segment}, doling upto {d+5} path segments");if(current is null||current())Dole(d+5,current,send);
    }
    internal int ExecutePath(IReadOnlyList<PathSegment> path,bool manual,Action<ushort>? reserve=null,Func<bool>? current=null,Func<RobotMessage,bool>? send=null)
    {
        if((byte)path.Count==0){Warning("PathComponent.ExecutePath.EmptyPath","");Field47=false;ErasePath();SetStatus(4);return 0;} // Native Path+4 is u8.
        int r=ClearPath(send??SendNative,current);if(r!=0||current is not null&&!current())return 1;
        Field42=unchecked((ushort)(Field42+1));reserve?.Invoke(Field42);if(current is not null&&!current())return 1;
        SetDolerPath(path,current,send);if(current is not null&&!current())return 1;Field46=manual;
        Info("PathComponent.SendExecutePath",$"sending start execution message (pathID = {Field42}, manualSpeed == {(manual?1:0)})");
        if(current is not null&&!current())return 1;
        r=(send??SendNative)(new ExecutePath{EventId=Field42,Unknown=manual})?0:1;
        if(current is not null&&!current())return 1;
        if(r==0){Field3C=_robot.Engine.Timer.SecondsF;SetStatus(2,current);}
        MissingOnce("M12-032 VizManager.DrawPath 0x0064A474 recipient/color handoff is unbuilt");return r;
    }
    internal int ExecuteCustomPath(IReadOnlyList<PathSegment> path,bool manual,Action<ushort>? reserve=null,Func<bool>? current=null,Func<RobotMessage,bool>? send=null)
    {
        _scopeGeneration++;_scopedCurrent=null;_scopedSend=null;
        if(Field47){PlannerSlot10?.Invoke();if(current is not null&&!current())return 1;Field47=false;}_plan=null;PlannerSlot10=null;
        // fidelity: M12-032 — strd r1,r1,[r6,#0x20] (0x0064B1E6): the selected planner pointer is cleared (the fallback +0x28 is not), so a custom
        // path never gets past HandlePossibleOriginChanges' planner test (0x00649812..0x00649816).
        if(_nativePlanners is {} selected)selected.Current=null;
        return ExecutePath(path,manual,reserve,current,send);
    }
    internal void SelectPlanner(int goals)
    {
        if(goals==0){Warning("PathComponent.SelectPlanner.NoTargets","can't select a planner with no target poses");return;}
        _nativePlanners?.Select();
    }
    internal bool StartPlanner()
    {
        if(_nativePlanners?.Current is null){Error("PathComponent.StartPlanner.NoSelectedPlanner","Must select planner before starting");return false;}
        if(_nativePlanners.StartPlanning()==0)return ReplanWithFallbackPlanner();Field47=true;PlannerSlot10=_nativePlanners.Stop;return true;
    }
    // fidelity: M12-032 — StartPlanner(const Pose3d&) 0x0064B090..0x0064B108 (census R6): [pc+0x20] vt+8 ComputePath(start, params.goals);
    // non-zero -> +0x47 = 1, true; zero -> tail ReplanWithFallbackPlanner. Only the Rejigger calls it with a pose here (native planners only).
    internal bool StartPlanner(Vision.Pose3d start)
    {
        if(_nativePlanners?.Current is null){Error("PathComponent.StartPlanner.NoSelectedPlanner","Must select planner before starting");return false;}
        if(_nativePlanners.StartPlanning(start)==0)return ReplanWithFallbackPlanner();
        Field47=true;PlannerSlot10=_nativePlanners.Stop;return true;
    }
    internal bool ReplanWithFallbackPlanner()
    {
        if(_nativePlanners?.Current is null)return false;
        bool ok=_nativePlanners.UseFallback();if(ok){Field47=true;PlannerSlot10=_nativePlanners.Stop;}return ok;
    }
    internal void UpdatePlanning()
    {
        var current=_scopedCurrent;var send=_scopedSend;
        bool IsCurrent()=>current?.Invoke()??true;
        if(!IsCurrent())return;
        int status=PlannerStatus;if(!IsCurrent())return;
        switch(status)
        {
            case 3:
                Info("PathComponent.Update.Planner.CompleteNoPlan","Running planner complete with no plan");
                if(!IsCurrent())return;
                if(DriveToPoseStatus is 2 or 3 or 5 or 6){Info("PathComponent.Update.Planner.NoPlanWhileTraversing","Planner completed with empty plan while we were already following a plan. clearing plan");if(!IsCurrent())return;Abort(send??SendNative,current);}
                if(!IsCurrent())return;Field47=false;ErasePath();if(IsCurrent())SetStatus(4,current);break;
            case 2:Field47=false;HandlePlanComplete();break;
            case 0:
                bool fallback=ReplanWithFallbackPlanner();if(!IsCurrent())return;
                if(fallback){SetStatus(1,current);if(IsCurrent())Info("PathComponent.Update.Planner.Error","Running planner returned error status, using fallback planner instead");}
                else{Abort(send??SendNative,current);if(!IsCurrent())return;SetStatus(6,current);if(IsCurrent())Info("PathComponent.Update.Planner.Error","Running planner returned error status");}break;
        }
    }
    internal void HandlePlanComplete()
    {
        var current=_scopedCurrent;
        bool IsCurrent()=>current?.Invoke()??true;
        if(!IsCurrent())return;
        if(_nativePlanners?.Current is not null)_plan=_nativePlanners.Read();
        if(!IsCurrent())return;
        if(_plan is not {} plan)return;
        if((byte)plan.Path.Count!=0)
        {
            bool? skipped=PlannerSkipSafetyCheck?.Invoke();if(!IsCurrent())return;
            bool? safe=skipped==false?PlannerPathIsSafe?.Invoke():null;if(!IsCurrent())return;
            if(skipped==false && safe==false)
            {bool fallback=ReplanWithFallbackPlanner();if(!IsCurrent())return;if(fallback)Info("PathComponent.Update.Planner.Collisions","Planner returned a path with obstacles, using fallback planner instead");else{Abort(_scopedSend??SendNative,current);if(!IsCurrent())return;SetStatus(6,current);if(IsCurrent())Info("PathComponent.Update.Planner.Collisions.ReplanFail","Planner returned a path with obstacles, can't get valid fallback plan, failing");}return;}
            else if(_nativePlanners?.Current is {} selected)
            {
                if(!selected.SkipsSafetyCheck && _nativePlanners.Lattice is not null)MissingOnce("M13-034 slot 0x18 -> 0x1C PathIsSafe body needs separately-owned PathSegment geometry helpers; candidate short path safety is not claimed source-backed");
            }
        }
        if(_nativePlanners?.Current is {} chosen)_robot.Engine.EmitSourceLog(EngineLogLevel.Event,null,"robot.plan_complete",chosen.Name,new Dictionary<string,string>{{"$data",((byte)plan.Path.Count).ToString(System.Globalization.CultureInfo.InvariantCulture)}});
        else MissingOnce("M12-032 robot.plan_complete 0x0064A024 native planner-name payload unavailable");
        if(!IsCurrent())return;
        if((byte)plan.Path.Count!=0)
        {
            Info("PathComponent.Update.Planner.CompleteWithPlan","Running planner complete with a plan");
            if(!IsCurrent())return;
            int r=ExecutePath(plan.Path,Field46,current:current,send:_scopedSend);if(!IsCurrent())return;if(r!=0){Warning("Robot.PathComponent.UnableToExecuteCompletedPath","Planner completed a path, but couldn't execute it for some reason");if(!IsCurrent())return;SetStatus(0,current);}
            if(IsCurrent()&&_goalIndex is {} index)index.Value=plan.Index;
        }
        else
        {
            if(DriveToPoseStatus is 2 or 3){Info("PathComponent.Update.Planner.PlanComplete.EmptyPlan","Planner completed with empty plan while we were already following a plan. clearing plan");if(!IsCurrent())return;ClearPath(SendNative,current);}
            else Info("PathComponent.Update.Planner.CompleteWithPlan.EmptyPlan","Planner completed but with an empty plan");if(!IsCurrent())return;Field47=false;ErasePath();if(IsCurrent())SetStatus(4,current);
        }
    }
    internal void Update()
    {
        var current=_scopedCurrent;
        bool IsCurrent()=>current?.Invoke()??true;
        if(!IsCurrent())return;
        // Stack machinery (thread safety): UpdatePlanning's fallback and HandlePlanComplete read the drive-center pose and origins; run it under the
        // world FrameGate like HandlePossibleOriginChanges (same lock order: tick -> FrameGate).
        if(Field47){using var frame=_robot.LocalizationWorld.EnterPoseMutation();UpdatePlanning();}
        if(!IsCurrent())return;
        if(DriveToPoseStatus==3)HandlePossibleOriginChanges();
        if(!IsCurrent())return;
        if(IsWaitingForRobotResponse && _robot.Engine.Timer.SecondsF > Field3C+1f)
        {Error("PathComponent.SentUnreceivedPath",$"robot did not start executing path. Last send = {Field42}, last recv = {Field44}");if(!IsCurrent())return;Abort(_scopedSend??SendNative,current);if(IsCurrent())SetStatus(6,current);}
    }
    internal void UpdateCurrentPathSegment(sbyte segment)
    {
        var current=_scopedCurrent;var send=_scopedSend;
        bool IsCurrent()=>current?.Invoke()??true;
        if(!IsCurrent())return;
        if(Field42!=0 && Field44==Field42)
        {
            Field40=unchecked((byte)segment);
            if(segment<=-1 && DriveToPoseStatus==3 && Field47){Info("PathComponent.UpdateRobotData.ComputingNewPlan",$"Actively replanning and finished following path ID {Field42}");if(!IsCurrent())return;SetStatus(1,current);}
        }
        if(IsCurrent()&&DriveToPoseStatus==3)UpdateDoler(unchecked((sbyte)Field40),current,send);
    }
    private static string EventName(byte type)=>type switch{0=>"PATH_STARTED",1=>"PATH_INTERRUPTED",2=>"PATH_COMPLETED",_=>"(null)"};
    internal void OnPathEvent(PathFollowingEvent e)
    {
        var current=_scopedCurrent;var send=_scopedSend;
        bool IsCurrent()=>current?.Invoke()??true;
        if(!IsCurrent())return;
        _robot.Engine.EmitSourceLog(EngineLogLevel.Debug,"Planner","PathComponent.ReceivedPathEvent",$"ID:{e.EventId} Event:{EventName(e.EventType)}");
        if(!IsCurrent())return;
        ushort id=e.EventId;
        if(e.EventType==0)
        {
            if(DriveToPoseStatus>4 || id==Field4A){Info("PathComponent.PathEvent.OldPathStartedWhileWaitingForCancel",$"The robot started path {id} which is about to be canceled. Status is {Name(DriveToPoseStatus)}");return;}
            if(DriveToPoseStatus is 2 or 3)
            {if(id!=Field42)Verify("PathComponent.PathEvent.StartingUnexpectedPathID",$"VERIFY(payload.pathID == _lastSentPathID): Last sent ID:{Field42}, starting ID:{id}",current);if(!IsCurrent())return;Field44=id;SetStatus(3,current);}
            else{Verify("PathComponent.PathEvent.UnexpectedStatus",$"VERIFY(ERobotDriveToPoseStatus::WaitingToBeginPath == _driveToPoseStatus || ERobotDriveToPoseStatus::FollowingPath == _driveToPoseStatus): Expecting to be WaitingToBeginPath or FollowingPath, not {Name(DriveToPoseStatus)}",current);if(IsCurrent())Abort(send??SendNative,current);}return;
        }
        if(e.EventType is not (1 or 2) || SwallowCanceled(e,current))return;
        if(!IsCurrent())return;
        if(e.EventType==2)
        {if(id!=Field42)Verify("PathComponent.PathEvent.CompletingUnexpectedPathID",$"VERIFY(payload.pathID == _lastSentPathID): Last sent ID:{Field42}, completing ID:{id}",current);}
        else
        {if(id!=Field44)Verify("PathComponent.PathEvent.InterruptingUnexpectedPathID",$"VERIFY(payload.pathID == _lastRecvdPathID): Last received ID:{Field44}, completing ID:{id}",current);if(!IsCurrent()||id!=Field42)return;}
        if(!IsCurrent())return;Field47=false;ErasePath();if(IsCurrent())SetStatus(4,current);
    }
    internal bool SwallowCanceled(PathFollowingEvent e,Func<bool>? current=null)
    {
        if(DriveToPoseStatus<5)return Field4A!=0 && e.EventId==Field4A;
        if(e.EventId==Field4A)SetStatus(DriveToPoseStatus==5?4:0,current);
        else Warning("PathComponent.PathEvent.CanceledPathDifferentPathComplete",$"We are in status '{Name(DriveToPoseStatus)}' waiting for path {Field4A} to cancel, but got message that path {e.EventId} is {EventName(e.EventType)}");
        return true;
    }
    // fidelity: M12-032 — HandlePossibleOriginChanges 0x0064980C (census H1..H7); called from Update only in status 3 (U1).
    internal void HandlePossibleOriginChanges()
    {
        // Stack machinery (thread safety): LocalizeToObject runs on the vision worker holding the world's FrameGate from S0 to S15, and every
        // origin-list writer (Delocalize, Rejigger, Flatten) holds it too. The dispatch, the Rejigger and the restart run under the same gate, so
        // they see either the whole pre-localization state or the whole post-localization state, as the engine's single thread does
        // (UpdateAllResults 0x00513C76 before PathComponent::Update 0x00514170). Lock order tick -> FrameGate, as DriveToPoseAction.InitNative.
        using var frame=_robot.LocalizationWorld.EnterPoseMutation();
        var current=_scopedCurrent;var send=_scopedSend;
        bool IsCurrent()=>current?.Invoke()??true;
        if(!IsCurrent())return;
        if(!PlannerSelected)return; // H1 0x00649812..0x00649816: no selected planner (pc+0x20), nothing.
        // H2 (NullParamsOrigin VERIFY 0x00649818..0x00649836) and H3 (CommonOriginNotInRobotPoseOriginList VERIFY 0x00649838..0x00649852) only log
        // and fall through. Neither condition has a producer in the stack (the native drive entry writes the params origin before status 3, and no
        // origin is ever removed from the list), so neither is built (recorded in M12-032).
        if(OriginOwnerInput?.Invoke() is not {} owner){MissingOnce("M12-032 HandlePossibleOriginChanges 0x0064980C: no Robot pose-origin owner (Robot+0x294) is bound; the origin dispatch is skipped");return;}
        uint paramsOrigin=ParamsOrigin,currentOrigin=owner.CurrentOriginId;  // H4 r5 0x0064985E, r6 0x00649860
        // H4 0x00649864..0x0064986C: IsPoseInWorldOrigin(GetOriginByID(params)) = HasSameRootAs(that origin, the current origin) 0x0051290C; an
        // unknown ID gives GetOriginByID's BadID VERIFY and a default root, which is not in the world.
        bool inWorld=Vision.PoseTreeNode.HasSameRoot(owner.GetNativeOrigin(paramsOrigin).Node,owner.GetNativeOrigin(currentOrigin).Node);
        if(!IsCurrent())return;
        if(paramsOrigin!=currentOrigin)
        {
            if(!inWorld)
            {
                // H5 0x00649870..0x006498BE: Abort (ClearPath, 3 -> 5), status 6, info.
                Abort(send??SendNative,current);if(!IsCurrent())return;SetStatus(6,current);
                if(IsCurrent())Info("PathComponent.Update.Replan.NotPossible","Our goal is in another coordinate frame that we can't get wrt current, we can't replan");
                return;
            }
            RejiggerTargetsAndReplan(owner); // H6 0x006498C0..0x006498D2
            return;
        }
        if(ObjectsChangedInput?.Invoke()==true&&IsCurrent())RestartPlannerIfNeeded(owner); // H7 0x006498D6..0x006498EE: DidObjectsChange() == 1
    }
    // fidelity: M12-032 — RejiggerTargetsAndReplan 0x006499D0..0x00649B44 (census R1..R8). Sends nothing to the robot: the old segments keep
    // running until the replan completes and HandlePlanComplete -> ExecutePath replaces them (R9).
    internal void RejiggerTargetsAndReplan(EngineRobot owner)
    {
        var current=_scopedCurrent;
        bool IsCurrent()=>current?.Invoke()??true;
        if(!IsCurrent())return;
        if(!PlannerSelected)return; // R1 0x006499D6..0x006499DA
        if(_nativePlanners is not {Current:not null} planners)return; // R1 above already required the selected planner; this only binds it.
        ParamsOrigin=owner.CurrentOriginId; // R2 0x006499DE..0x006499E8, before anything can fail
        if(Field47){PlannerSlot10?.Invoke();if(!IsCurrent())return;Field47=false;} // R3 0x006499EA..0x006499FA: planner vt+0x10 StopPlanning
        Info("PathComponent.Update.Replan.RejiggeringPlanner","Our goal is in another coordinate frame, but we are updating to current frame"); // R4
        if(!IsCurrent())return;
        // R5 0x00649A38..0x00649A5C: every params goal re-expressed in place w.r.t. the world origin; results ANDed, no early stop. The planners
        // read these goals parent-relative (Pose3d.Translation is the node's own transform), as the engine's GetTransform does (census F5).
        var world=owner.GetNativeOrigin(owner.CurrentOriginId);
        bool converted=true;
        foreach(var goal in planners.Goals)converted&=Vision.Pose3d.GetWithRespectTo(goal,world,goal);
        if(!IsCurrent())return;
        if(!converted)
        {
            // R8 0x00649AC6..0x00649B02, then status 0 (0x00649B3A..0x00649B3E). No producer found (census R8); built as the one-line exit.
            Verify("PathComponent.Update.Rejigger",$"VERIFY(rejiggerSuccess): Rejiggering {planners.Goals.Count} target poses to new origin '{world.Name}' failed",current);
            if(IsCurrent())SetStatus(0,current);
            return;
        }
        // R6 0x00649A5E..0x00649A74: a fresh GetDriveCenterPose (0x005477A0), StartPlanner(start); success -> status 1 (3 -> 1).
        if(StartPlanner(owner.GetCheckedForwardPose())){if(IsCurrent())SetStatus(1,current);return;}
        if(!IsCurrent())return;
        // R7 0x00649A76..0x00649AC4: a second ReplanWithFallbackPlanner (census: it always finds the fallback consumed).
        if(ReplanWithFallbackPlanner())
        {
            if(!IsCurrent())return;SetStatus(1,current);
            if(IsCurrent())Info("PathComponent.Update.Rejigger.Fallback","Planning to newly rejiggered poses failed, but fallback succeeded.");
            return;
        }
        if(!IsCurrent())return;
        Warning("PathComponent.Update.Replan.RejiggeringPlanner","We could not start driving to rejiggered pose."); // 0x00649B04..0x00649B14
        if(IsCurrent())SetStatus(0,current); // 0x00649B3A..0x00649B3E
    }
    // fidelity: M12-032 — RestartPlannerIfNeeded 0x00649C14..0x00649D14 (census S1): the start is GetDriveCenterPose (bl 0x005477A0 at
    // 0x00649C28..0x00649C3A) for every path, passed to planner vt+0xC ComputeNewPathIfNeeded(start, false).
    internal void RestartPlannerIfNeeded(EngineRobot owner)
    {
        var current=_scopedCurrent;var send=_scopedSend;
        bool IsCurrent()=>current?.Invoke()??true;
        if(!IsCurrent())return;
        if(Field47)return;
        if(_nativePlanners?.Current is not null)
        {
            int code=_nativePlanners.Restart(owner.GetCheckedForwardPose());
            if(!IsCurrent())return;
            if(code==1){_robot.Engine.EmitSourceLog(EngineLogLevel.Debug,"Planner","PathComponent.Replan.Running","ComputeNewPathIfNeeded running");if(IsCurrent())Field47=true;}
            else if(code==0)
            {
                bool fallback=ReplanWithFallbackPlanner();if(!IsCurrent())return;
                if(fallback)Info("PathComponent.RestartIfNeeded.Error.Fallback","computing a new path resulted in an error, switching to fallback");
                else{Info("PathComponent.RestartIfNeeded.Error.NoFallback","computing a new path resulted in an error but couldn't use fallback. Failing");if(!IsCurrent())return;Abort(send??SendNative,current);if(IsCurrent())SetStatus(6,current);}
            }
            return;
        }
    }
}
