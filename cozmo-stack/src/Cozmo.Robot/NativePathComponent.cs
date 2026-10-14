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
    private IReadOnlyList<PathSegment> _dolerPath=Array.Empty<PathSegment>();
    internal ushort LastDoledIndex=0xFFFF;
    internal uint DoleSegmentCount;
    private Func<PathPlannerModelResult>? _computeModel;
    private Func<PathPlannerModelResult>? _fallbackModel;
    private PathPlannerModelResult? _plan;
    private NativePlannerSet? _nativePlanners;
    internal NativeShortPlanner NativeFace=null!,NativeMinimal=null!;
    private void InitNativeShortPlanners()
    {
        SourceLogSink source=(level,channel,key,text)=>_robot.Engine.EmitSourceLog(level,channel,key,text);
        Action<string> missing=text=>_robot.Engine.EmitStackLog("MISSING: "+text);
        NativeFace=new(false,source,missing);NativeMinimal=new(true,source,missing);
    }
    private Action<string>? _modelMissing;
    internal Func<bool>? PlannerPathIsSafe;
    internal Func<bool>? PlannerSkipSafetyCheck;
    internal Func<uint>? CurrentOriginInput,RobotRootOriginInput;
    internal Func<int,uint>? GoalRootOriginInput;
    internal Func<uint,bool>? ContainsOriginInput;
    internal Func<bool>? ObjectsChangedInput,ComputeNewPathIfNeeded;
    internal Action? RejiggerInput;
    internal uint ParamsOrigin {get=>PlannerObjectsField0C;set=>PlannerObjectsField0C=value;}
    private RetainedSubscription? _pathSubscription;
    internal void Attach(EngineRobot owner){_pathSubscription?.Dispose();_pathSubscription=owner.SubscribeMessage(new PathFollowingEvent().Id,m=>owner.ActionList.WithGate(()=>OnPathEvent((PathFollowingEvent)m)),"Path");}
    private PathGoalIndex? _goalIndex;
    internal int PlannerStatus => _nativePlanners?.Current?.CheckPlanningStatus??_plan?.Status??1;
    private void ModelGap(){if(_nativePlanners?.Current is null)_modelMissing?.Invoke("M13-033 native lattice inputs unavailable; legacy model remains a labelled candidate");}
    internal void SetNativePlanners(NativePlannerSet planners)=>_nativePlanners=planners;
    private bool StatusMask(uint mask){int shift=DriveToPoseStatus&0x7F;return shift<32 && ((mask>>shift)&1)!=0;} // ARM register LSR, not C#'s modulo-32 shift.
    internal bool IsActive => StatusMask(0xE);
    internal bool HasPathToFollow => StatusMask(0xC);
    internal bool LastPathFailed => StatusMask(0x41);
    internal bool IsWaitingToCancelPath => StatusMask(0x60);
    internal bool IsWaitingForRobotResponse => StatusMask(0x64);
    private void Info(string key,string text)=>_robot.Engine.EmitSourceLog(EngineLogLevel.Info,"Planner",key,text);
    private void Warning(string key,string text)=>_robot.Engine.EmitSourceLog(EngineLogLevel.Warning,null,key,text);
    private void Error(string key,string text){_robot.Engine.EmitSourceLog(EngineLogLevel.Error,null,key,text);EngineErrorState.StoreAndMaybeBreak();}
    private void Verify(string key,string text){_robot.Engine.EmitSourceLog(EngineLogLevel.Error,null,key,text);EngineErrorState.ErrorFlagSet=true;_robot.Engine.Das.ForceFlushNow();}
    private string Name(int status)=>StatusName?.Invoke(status)??StatusNames[status];
    internal void SetStatus(int next)
    {
        if(next==DriveToPoseStatus)return;
        Info("PathComponent.TransitionStatus",$"{Name(DriveToPoseStatus)} -> {Name(next)}");DriveToPoseStatus=next;
    }
    private bool SendNative(RobotMessage message)=>SendPathMessage?.Invoke(message)??(message is AppendPathSegmentLine or AppendPathSegmentArc or AppendPathSegmentPointTurn?_robot.SendToHandler(message):_robot.SendMessage(message));
    private void ErasePath(){if(ErasePathHook is {} erase)erase(CozmoEngine.RobotId);else MissingOnce("M12-032 VizManager.ErasePath 0x006BFACE recipient is unbuilt");}
    private void ResetMachine(){_pathSubscription?.Dispose();_pathSubscription=null;_nativePlanners?.Dispose();_nativePlanners=null;InitNativeShortPlanners();PlannerSlot10=null;_computeModel=_fallbackModel=null;_plan=null;_goalIndex=null;ParamsOrigin=0;ClearDoler();}
    internal void ClearDoler(){_dolerPath=Array.Empty<PathSegment>();LastDoledIndex=0xFFFF;DoleSegmentCount=0;}
    internal void SetDolerPath(IReadOnlyList<PathSegment> path)
    {
        _dolerPath=path.ToArray();LastDoledIndex=0xFFFF;DoleSegmentCount=(uint)(byte)path.Count;Dole(6);
    }
    internal void Dole(int count)
    {
        int start=(short)LastDoledIndex+1;
        uint end=unchecked((uint)((short)LastDoledIndex+count));
        if(end>=DoleSegmentCount)end=unchecked(DoleSegmentCount-1);
        _robot.Engine.EmitSourceLog(EngineLogLevel.Debug,"Unnamed","PathDolerOuter",$"should dole from {start} to {end} (totalSegments = {DoleSegmentCount})");
        if(DoleSegmentCount==0){MissingOnce("M1-034/M12-002 Dole empty-path unchecked segment read 0x00507F10 skipped, prior counter retained");return;}
        for(int i=start;unchecked((uint)i)<=end && i<_dolerPath.Count;i++)
        {
            _robot.Engine.EmitSourceLog(EngineLogLevel.Debug,"Unnamed","PathDolerOuter",$"doling out basestation idx {i} :  ");
            MissingOnce("M12-002 PathSegment.Print 0x00507F36 -> 0x0085C034 text/body has no checked row; segment print is unbuilt");
            RobotMessage? message=_dolerPath[i] switch
            {
                PathSegment.Line l=>new AppendPathSegmentLine{XStartMm=l.FromX,YStartMm=l.FromY,XEndMm=l.ToX,YEndMm=l.ToY,Speed=new(){SpeedMmps=l.SpeedMmps,AccelMmps2=l.AccelMmps2,DecelMmps2=l.DecelMmps2}},
                PathSegment.Arc a=>new AppendPathSegmentArc{XCenterMm=a.CenterX,YCenterMm=a.CenterY,RadiusMm=a.RadiusMm,StartRad=a.StartAngleRad,SweepRad=a.SweepRad,Speed=new(){SpeedMmps=a.SpeedMmps,AccelMmps2=a.AccelMmps2,DecelMmps2=a.DecelMmps2}},
                PathSegment.PointTurn t=>new AppendPathSegmentPointTurn{XMm=t.X,YMm=t.Y,TargetAngleRad=t.TargetAngleRad,AngleToleranceRad=t.AngleToleranceRad,UseShortestDirection=t.UseShortestDirection,Speed=new(){SpeedMmps=t.SpeedRadPerSec,AccelMmps2=t.AccelRadPerSec2,DecelMmps2=t.DecelRadPerSec2}},_=>null
            };
            if(message is {} m && !SendNative(m))
            {Error("PathDolerOuter.Dole","ERROR: failed to send message!");return;}
            LastDoledIndex=unchecked((ushort)i);
        }
    }
    internal void UpdateDoler(sbyte segment)
    {
        int d=segment-(short)LastDoledIndex;
        if(d < -4 || unchecked(DoleSegmentCount-1)<=unchecked((uint)(short)LastDoledIndex))return;
        _robot.Engine.EmitSourceLog(EngineLogLevel.Debug,"Unnamed","PathDolerOuter.Update.Doling",$"currPathIdx: {segment}, doling upto {d+5} path segments");Dole(d+5);
    }
    internal int ExecutePath(IReadOnlyList<PathSegment> path,bool manual,Action<ushort>? reserve=null)
    {
        if((byte)path.Count==0){Warning("PathComponent.ExecutePath.EmptyPath","");Field47=false;ErasePath();SetStatus(4);return 0;} // Native Path+4 is u8.
        int r=ClearPath(SendNative);if(r!=0)return r;
        Field42=unchecked((ushort)(Field42+1));reserve?.Invoke(Field42);SetDolerPath(path);Field46=manual;
        Info("PathComponent.SendExecutePath",$"sending start execution message (pathID = {Field42}, manualSpeed == {(manual?1:0)})");
        r=SendNative(new ExecutePath{EventId=Field42,Unknown=manual})?0:1;
        if(r==0){Field3C=_robot.Engine.Timer.SecondsF;SetStatus(2);}
        MissingOnce("M12-032 VizManager.DrawPath 0x0064A474 recipient/color handoff is unbuilt");return r;
    }
    internal int ExecuteCustomPath(IReadOnlyList<PathSegment> path,bool manual,Action<ushort>? reserve=null)
    {
        if(Field47){PlannerSlot10?.Invoke();Field47=false;}_computeModel=null;_plan=null;PlannerSlot10=null;
        return ExecutePath(path,manual,reserve);
    }
    internal int StartDrivingToPose(int goals,PathGoalIndex index,bool manual,Func<PathPlannerModelResult> compute,Action stop,Action<string> missing)
    {
        if(goals==0){Warning("PathComponent.StartDrivingToPose.NoTargetPoses","Can't start driving with no provided targets");return 1;}
        _modelMissing=missing;
        if(Field47){PlannerSlot10?.Invoke();Field47=false;}
        if(IsActive){Info("PathComponent.StartDrivingToPose.AlreadyBusy",$"Path component status was '{Name(DriveToPoseStatus)}'. Aborting current plan");Abort(SendNative);}
        Field46=manual;_goalIndex=index;SharedPath=index;
        if(CurrentOriginInput is {} current)ParamsOrigin=current();
        else MissingOnce("M12-032 current-origin/ComputeDriveCenterPose handoff 0x0064ACC4..0x0064AF30 remains unbuilt; existing host planner geometry retained");
        if(RobotRootOriginInput is {} root && CurrentOriginInput is not null && root()!=ParamsOrigin)
        {MissingOnce("M12-032 DriveCenterOriginMismatch VERIFY formatting 0x0064AE5A unbuilt");SetStatus(0);return 1;}
        if(GoalRootOriginInput is {} goalRoot && ContainsOriginInput is {} contains)
        {
            for(int i=0;i<goals;i++)if(!contains(goalRoot(i))){MissingOnce("M12-032 OriginMismatch warning 0x0064AE08 argument handoff unbuilt");SetStatus(0);return 1;}
        }
        else MissingOnce("M12-032 goal origin/ComputeDriveCenterPose gates unbuilt; no native origin validation claim");
        _computeModel=compute;PlannerSlot10=stop;
        SelectPlanner(goals);
        if(!StartPlanner()){SetStatus(0);return 1;}
        SetStatus(1);if(!Field48)MissingOnce("M12-032 SpeedChooser.GetPathMotionProfile 0x0064ADE0 handoff represented by existing drive profile; custom profile field ownership remains unbuilt");
        return 0;
    }
    internal void SelectPlanner(int goals)
    {
        if(goals==0){Warning("PathComponent.SelectPlanner.NoTargets","can't select a planner with no target poses");return;}
        _nativePlanners?.Select();ModelGap();
    }
    internal bool StartPlanner()
    {
        if(_nativePlanners?.Current is not null){if(_nativePlanners.StartPlanning()==0)return ReplanWithFallbackPlanner();Field47=true;PlannerSlot10=_nativePlanners.Stop;return true;}
        if(_computeModel is null){Error("PathComponent.StartPlanner.NoSelectedPlanner","Must select planner before starting");return false;}
        _plan=_computeModel();if(_plan.Status==0)return ReplanWithFallbackPlanner();Field47=true;return true;
    }
    internal bool ReplanWithFallbackPlanner()
    {
        if(_nativePlanners?.Current is not null){bool ok=_nativePlanners.UseFallback();if(ok){Field47=true;PlannerSlot10=_nativePlanners.Stop;}return ok;}
        if(_fallbackModel is null)return false;_computeModel=_fallbackModel;_fallbackModel=null;
        if(!StartPlanner())return false;ModelGap();MissingOnce("M12-032 robot.fallback_planner_used 0x00649EBC native old-name payload unavailable");return true;
    }
    internal void SetPlannerModelForTest(Func<PathPlannerModelResult> compute,Func<PathPlannerModelResult>? fallback=null){_computeModel=compute;_fallbackModel=fallback;}
    internal void UpdatePlanning()
    {
        switch(PlannerStatus)
        {
            case 3:
                Info("PathComponent.Update.Planner.CompleteNoPlan","Running planner complete with no plan");
                if(DriveToPoseStatus is 2 or 3 or 5 or 6){Info("PathComponent.Update.Planner.NoPlanWhileTraversing","Planner completed with empty plan while we were already following a plan. clearing plan");Abort(SendNative);}
                Field47=false;ErasePath();SetStatus(4);break;
            case 2:Field47=false;HandlePlanComplete();break;
            case 0:
                if(ReplanWithFallbackPlanner()){SetStatus(1);Info("PathComponent.Update.Planner.Error","Running planner returned error status, using fallback planner instead");}
                else{Abort(SendNative);SetStatus(6);Info("PathComponent.Update.Planner.Error","Running planner returned error status");}break;
        }
    }
    internal void HandlePlanComplete()
    {
        if(_nativePlanners?.Current is not null)_plan=_nativePlanners.Read();
        if(_plan is not {} plan)return;
        if((byte)plan.Path.Count!=0)
        {
            if(PlannerSkipSafetyCheck is {} skip && !skip() && PlannerPathIsSafe is {} safe && !safe())
            {if(ReplanWithFallbackPlanner())Info("PathComponent.Update.Planner.Collisions","Planner returned a path with obstacles, using fallback planner instead");else{Abort(SendNative);SetStatus(6);Info("PathComponent.Update.Planner.Collisions.ReplanFail","Planner returned a path with obstacles, can't get valid fallback plan, failing");}return;}
            else if(_nativePlanners?.Current is {} selected)
            {
                if(!selected.SkipsSafetyCheck && _nativePlanners.Lattice is not null)MissingOnce("M13-034 slot 0x18 -> 0x1C PathIsSafe body needs separately-owned PathSegment geometry helpers; candidate short path safety is not claimed source-backed");
            }
            else if(PlannerSkipSafetyCheck is null)ModelGap();
        }
        if(_nativePlanners?.Current is {} chosen)_robot.Engine.EmitSourceLog(EngineLogLevel.Info,null,"robot.plan_complete",chosen.Name,new Dictionary<string,string>{{"$data",((byte)plan.Path.Count).ToString(System.Globalization.CultureInfo.InvariantCulture)}});
        else{ModelGap();MissingOnce("M12-032 robot.plan_complete 0x0064A024 native planner-name payload unavailable");}
        if((byte)plan.Path.Count!=0)
        {
            Info("PathComponent.Update.Planner.CompleteWithPlan","Running planner complete with a plan");
            int r=ExecutePath(plan.Path,Field46);if(r!=0){Warning("Robot.PathComponent.UnableToExecuteCompletedPath","Planner completed a path, but couldn't execute it for some reason");SetStatus(0);}
            if(_goalIndex is {} index)index.Value=plan.Index;
        }
        else
        {
            if(DriveToPoseStatus is 2 or 3){Info("PathComponent.Update.Planner.PlanComplete.EmptyPlan","Planner completed with empty plan while we were already following a plan. clearing plan");ClearPath(SendNative);}
            else Info("PathComponent.Update.Planner.CompleteWithPlan.EmptyPlan","Planner completed but with an empty plan");Field47=false;ErasePath();SetStatus(4);
        }
    }
    internal void Update()
    {
        if(Field47)UpdatePlanning();
        if(DriveToPoseStatus==3)HandlePossibleOriginChanges();
        if(IsWaitingForRobotResponse && _robot.Engine.Timer.SecondsF > Field3C+1f)
        {Error("PathComponent.SentUnreceivedPath",$"robot did not start executing path. Last send = {Field42}, last recv = {Field44}");Abort(SendNative);SetStatus(6);}
    }
    internal void UpdateCurrentPathSegment(sbyte segment)
    {
        if(Field42!=0 && Field44==Field42)
        {
            Field40=unchecked((byte)segment);
            if(segment<=-1 && DriveToPoseStatus==3 && Field47){Info("PathComponent.UpdateRobotData.ComputingNewPlan",$"Actively replanning and finished following path ID {Field42}");SetStatus(1);}
        }
        if(DriveToPoseStatus==3)UpdateDoler(unchecked((sbyte)Field40));
    }
    private static string EventName(byte type)=>type switch{0=>"PATH_STARTED",1=>"PATH_INTERRUPTED",2=>"PATH_COMPLETED",_=>"(null)"};
    internal void OnPathEvent(PathFollowingEvent e)
    {
        _robot.Engine.EmitSourceLog(EngineLogLevel.Debug,"Planner","PathComponent.ReceivedPathEvent",$"ID:{e.EventId} Event:{EventName(e.EventType)}");
        ushort id=e.EventId;
        if(e.EventType==0)
        {
            if(DriveToPoseStatus>4 || id==Field4A){Info("PathComponent.PathEvent.OldPathStartedWhileWaitingForCancel",$"The robot started path {id} which is about to be canceled. Status is {Name(DriveToPoseStatus)}");return;}
            if(DriveToPoseStatus is 2 or 3)
            {if(id!=Field42)Verify("PathComponent.PathEvent.StartingUnexpectedPathID",$"VERIFY(payload.pathID == _lastSentPathID): Last sent ID:{Field42}, starting ID:{id}");Field44=id;SetStatus(3);}
            else{Verify("PathComponent.PathEvent.UnexpectedStatus",$"VERIFY(ERobotDriveToPoseStatus::WaitingToBeginPath == _driveToPoseStatus || ERobotDriveToPoseStatus::FollowingPath == _driveToPoseStatus): Expecting to be WaitingToBeginPath or FollowingPath, not {Name(DriveToPoseStatus)}");Abort(SendNative);}return;
        }
        if(e.EventType is not (1 or 2) || SwallowCanceled(e))return;
        if(e.EventType==2)
        {if(id!=Field42)Verify("PathComponent.PathEvent.CompletingUnexpectedPathID",$"VERIFY(payload.pathID == _lastSentPathID): Last sent ID:{Field42}, completing ID:{id}");}
        else
        {if(id!=Field44)Verify("PathComponent.PathEvent.InterruptingUnexpectedPathID",$"VERIFY(payload.pathID == _lastRecvdPathID): Last received ID:{Field44}, completing ID:{id}");if(id!=Field42)return;}
        Field47=false;ErasePath();SetStatus(4);
    }
    internal bool SwallowCanceled(PathFollowingEvent e)
    {
        if(DriveToPoseStatus<5)return Field4A!=0 && e.EventId==Field4A;
        if(e.EventId==Field4A)SetStatus(DriveToPoseStatus==5?4:0);
        else Warning("PathComponent.PathEvent.CanceledPathDifferentPathComplete",$"We are in status '{Name(DriveToPoseStatus)}' waiting for path {Field4A} to cancel, but got message that path {e.EventId} is {EventName(e.EventType)}");
        return true;
    }
    internal void HandlePossibleOriginChanges()
    {
        if(_computeModel is null)return;
        if(CurrentOriginInput is null || ContainsOriginInput is null){MissingOnce("M12-032 origin validation/VERIFY 0x00649830/0x00649852 inputs unbuilt");return;}
        if(!ContainsOriginInput(ParamsOrigin)){Abort(SendNative);SetStatus(6);MissingOnce("M12-032 Replan.NotPossible info 0x00649898 arguments unbuilt");return;}
        if(ParamsOrigin!=CurrentOriginInput())
        {if(RejiggerInput is {} rejigger)rejigger();else MissingOnce("M12-032 RejiggerTargetsAndReplan 0x006499D0..0x00649B44 unbuilt origin transform helpers; logs/VERIFY at 0x00649A12/0x00649A9C/0x00649AFE/0x00649B14 not fabricated");}
        else if(ObjectsChangedInput?.Invoke()==true)RestartPlannerIfNeeded();
    }
    internal void RestartPlannerIfNeeded()
    {
        if(Field47)return;
        if(_nativePlanners?.Current is not null)
        {
            int code=_nativePlanners.Restart();
            if(code==1){_robot.Engine.EmitSourceLog(EngineLogLevel.Debug,"Planner","PathComponent.Replan.Running","ComputeNewPathIfNeeded running");Field47=true;}
            else if(code==0)
            {
                if(ReplanWithFallbackPlanner())Info("PathComponent.RestartIfNeeded.Error.Fallback","computing a new path resulted in an error, switching to fallback");
                else{Abort(SendNative);SetStatus(6);Info("PathComponent.RestartIfNeeded.Error.NoFallback","computing a new path resulted in an error but couldn't use fallback. Failing");}
            }
            return;
        }
        if(ComputeNewPathIfNeeded is null){ModelGap();return;}
        if(ComputeNewPathIfNeeded()){Field47=true;MissingOnce("M12-032 RestartPlannerIfNeeded log at 0x00649C64 arguments unbuilt");}
        else if(!ReplanWithFallbackPlanner()){Abort(SendNative);SetStatus(6);MissingOnce("M12-032 RestartPlannerIfNeeded failure info/debug at 0x00649C9E/0x00649CE0 arguments unbuilt");}
    }
}
