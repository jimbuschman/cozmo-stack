using Cozmo.Robot.Vision;
namespace Cozmo.Robot.Manipulation;

// fidelity: M13-033, M13-034
internal sealed class NativePlannerSet : IDisposable
{
    private readonly ManipulationSystem _m;
    internal readonly NativeShortPlanner Face,Minimal;
    internal NativeLatticePlannerRuntime? Lattice;
    internal INativePathPlanner? Current,Fallback;
    internal Pose3d Start;
    internal IReadOnlyList<Pose3d> Goals=Array.Empty<Pose3d>();
    internal PathMotionProfile Profile=PathMotionProfile.Default;
    private readonly Action<string> _missing;
    private SourceLogSink Source= null!;
    internal bool IsDisposed;
    internal NativePlannerSet(ManipulationSystem m)
    {
        _m=m;_missing=s=>m.Robot.Engine.EmitStackLog("MISSING: "+s);
        Source=(level,channel,key,text)=>m.Robot.Engine.EmitSourceLog(level,channel,key,text);
        Face=m.Robot.Motion.Path.NativeFace;Minimal=m.Robot.Motion.Path.NativeMinimal;
        if(m.Planner is {} backend)Lattice=new(backend,m.World,()=>m.Docking.Carrying.CarriedObjectId,Source,_missing,m.Robot.Engine);
    }
    internal void Configure(Pose3d start,IReadOnlyList<Pose3d> goals,PathMotionProfile profile,IEnumerable<uint>? ignore)
    {
        Start=start;Goals=goals;Profile=profile;
        if(_m.Planner is {} backend && !ReferenceEquals(Lattice?.Backend,backend))
        {Lattice?.Dispose();Lattice=new(backend,_m.World,()=>_m.Docking.Carrying.CarriedObjectId,Source,_missing,_m.Robot.Engine);}
        Lattice?.SetIgnore(ignore);
    }
    internal void Select()
    {
        if(Goals.Count==0)return;
        int closest=NativeShortPlanner.ClosestGoal(Start,Goals);
        var selectedGoal=Goals[closest];
        float dx=(float)selectedGoal.Translation.X-(float)Start.Translation.X,dy=(float)selectedGoal.Translation.Y-(float)Start.Translation.Y;
        float d2=(float)((double)dx*dx+(double)dy*dy),heading=0,initial=0;
        if(!(d2<1600f))
        {
            Current=Lattice;
            if(Current is null){_missing("M13-033 lattice asset/context input is absent; existing legacy path producer remains an explicit candidate, native lattice construction is not claimed");return;}
            Source(EngineLogLevel.Info,"Planner","PathComponent.SelectPlanner.Long",FormattableString.Invariant($"distance^2 is {d2:F6}, selecting long planner '{Current.Name}'"));
        }
        else
        {
            float yaw=(float)Start.AngleAroundZ;
            heading=MathF.Abs(EngineF.RadiansMinus((float)selectedGoal.AngleAroundZ,yaw));initial=MathF.Abs(EngineF.RadiansMinus(MathF.Atan2(dy,dx),yaw));
            bool min=d2>1f && heading<=EngineF.Bits(0x3F490FDB) && initial>EngineF.Bits(0x3E490FDB);
            Current=min?Minimal:Face;
            Source(EngineLogLevel.Info,"Planner",min?"PathComponent.SelectPlanner.ShortMinAngle":"PathComponent.SelectPlanner.Short",FormattableString.Invariant($"distance^2 is {d2:F6}, angleDelta is {heading:F6}, intiialTurnAngle is {initial:F6}, selecting short{(min?" min_angle":"")} planner '{Current.Name}'"));
        }
        float rounded=MathF.Round(d2,MidpointRounding.AwayFromZero);
        int data=float.IsNaN(rounded)?0:rounded>=2147483648f?int.MaxValue:rounded<=-2147483648f?int.MinValue:(int)rounded;
        _m.Robot.Engine.EmitSourceLog(EngineLogLevel.Event,null,"robot.planner_selected",Current.Name,new Dictionary<string,string>{{"$data",data.ToString(System.Globalization.CultureInfo.InvariantCulture)}});
        Fallback=ReferenceEquals(Current,Lattice)?null:Lattice;
        _missing("M13-033 selection uses the stack pose input; source Robot root-conversion helper 0x004DF628..0x004DF6DC is separately incomplete, no full pose-origin claim");
    }
    internal int StartPlanning()=>Current?.ComputePath(Start,Goals)??0;
    internal int Restart()=>Current?.ComputeNewPathIfNeeded(_m.RobotPose()??Start,false)??0;
    internal PathPlannerModelResult Read()
    {
        int status=Current?.CheckPlanningStatus??0;
        IReadOnlyList<PathSegment> path=Array.Empty<PathSegment>();byte index=0;
        if((status is 2 or 3) && Current is {} planner && planner.GetCompletePath(_m.RobotPose()??Start,Profile,out var result,out var goal)){path=result;index=goal;}
        return new(status,path,index);
    }
    internal bool UseFallback()
    {
        if(Fallback is not {} next)return false;string old=Current?.Name??"";Current=next;Fallback=null;
        bool started=StartPlanning()!=0;
        if(started)_m.Robot.Engine.EmitSourceLog(EngineLogLevel.Event,null,"robot.fallback_planner_used",old);
        return started;
    }
    internal void Stop()=>Current?.StopPlanning();
    public void Dispose(){if(IsDisposed)return;IsDisposed=true;Lattice?.Dispose();}
}
