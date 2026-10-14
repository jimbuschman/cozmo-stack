using System.Globalization;
using Cozmo.Robot.Vision;
namespace Cozmo.Robot.Manipulation;

// fidelity: M13-033, M13-034
internal interface INativePathPlanner
{
    string Name {get;}
    bool SkipsSafetyCheck {get;}
    int ComputePath(Pose3d start,IReadOnlyList<Pose3d> goals);
    int ComputeNewPathIfNeeded(Pose3d start,bool force);
    int CheckPlanningStatus {get;}
    bool GetCompletePath(Pose3d start,PathMotionProfile profile,out IReadOnlyList<PathSegment> path,out byte goal);
    void StopPlanning();
}

// Checked short-planner calls generate their own geometry; no StraightLinePlanner substitution.
internal sealed class NativeShortPlanner(bool minimal,SourceLogSink source,Action<string> missing) : INativePathPlanner
{
    private Pose3d _goal;
    private byte _goalIndex;
    private bool _present,_error;
    private IReadOnlyList<PathSegment> _path=Array.Empty<PathSegment>();
    public string Name=>minimal?"MinimalAngle":"FaceAndApproach"; // native constructor strings
    public bool SkipsSafetyCheck=>false;
    public int CheckPlanningStatus=>_error?0:_present?2:3;
    public void StopPlanning(){} // Base virtual, short planners have no worker.
    private void Info(string key,string text)=>source(EngineLogLevel.Info,"Unnamed",key,text);
    private static string F(float n)=>n.ToString("F6",CultureInfo.InvariantCulture);
    public int ComputePath(Pose3d start,IReadOnlyList<Pose3d> goals)
    {
        _present=_error=false;
        if(goals.Count==0){missing("M1-034/M13-033 empty IPathPlanner goal-vector dereference 0x00508A60..0x00508A8E skipped");return 0;}
        int best=ClosestGoal(start,goals);
        missing("M13-033 ComputeClosestGoalPose Debug fields/rotation-axis formatting 0x00508870..0x0050890A not represented");
        _goalIndex=unchecked((byte)best);_goal=goals[best];return ComputeNewPathIfNeeded(start,true);
    }
    internal static int ClosestGoal(Pose3d start,IReadOnlyList<Pose3d> goals)
    {
        int best=0;float bestD=0;
        for(int i=0;i<goals.Count;i++)
        {
            float x=(float)goals[i].Translation.X-(float)start.Translation.X,y=(float)goals[i].Translation.Y-(float)start.Translation.Y,z=(float)goals[i].Translation.Z-(float)start.Translation.Z;
            float d=x*x;d+=y*y;d+=z*z;
            if(i==0 || d<bestD){best=i;bestD=d;}
        }
        return best;
    }
    public int ComputeNewPathIfNeeded(Pose3d start,bool force)
    {
        _present=false;
        if(!force){_present=true;return 2;}
        var output=new List<PathSegment>();
        float x=(float)start.Translation.X,y=(float)start.Translation.Y,yaw=(float)start.AngleAroundZ;
        float gx=(float)_goal.Translation.X,gy=(float)_goal.Translation.Y,gyaw=(float)_goal.AngleAroundZ;
        const float accel=200f;
        float tol=EngineF.Bits(0x3D0EFA35);
        void Turn(float px,float py,float target,float delta,float speed)=>NativePathBuffer.Append(output,new PathSegment.PointTurn(px,py,target,tol,delta<0?-speed:speed,10,10,true),missing,"AppendPointTurn");
        void Line(float ax,float ay,float bx,float by,float speed)=>NativePathBuffer.Append(output,new PathSegment.Line(ax,ay,bx,by,speed,accel,accel),missing,"AppendLine");
        if(!minimal)
        {
            float dx=gx-x,dy=gy-y,d2=(float)((double)dx*dx+(double)dy*dy);
            bool line=d2>100f;float travel=yaw;
            if(line){Info("FaceAndApproachPlanner.Straight",$"doing straight because distance^2 of {F(d2)} > {F(100)}");travel=MathF.Atan2(dy,dx);}
            float final=-EngineF.RadiansMinus(travel,gyaw),initial=EngineF.RadiansMinus(travel,yaw);
            double five=BitConverter.Int64BitsToDouble(0x3FB657184AE73FE4); // shipped double literal at 0x004F3928
            bool endTurn=(double)MathF.Abs(final)>five,firstTurn=line&&(double)MathF.Abs(initial)>five;
            if(endTurn)Info("FaceAndApproachPlanner.FinalTurn",$"doing final turn because delta theta of {F(MathF.Abs(final))} > {five.ToString("F6",CultureInfo.InvariantCulture)}");
            if(firstTurn)Info("FaceAndApproachPlanner.InitialTurn",$"doing initial turn because delta theta of {F(MathF.Abs(initial))} > {five.ToString("F6",CultureInfo.InvariantCulture)}");
            float speed=30;
            if(line && (double)MathF.Abs(initial)>BitConverter.Int64BitsToDouble(0x3FF0C152382DAF40))
            {
                missing($"M13-033 stdout: FaceAndApproachPlanner: deltaTheta of {F(initial)} above threshold, doing backup!");
                initial=EngineF.Rescale(initial+EngineF.Pi);final=EngineF.Rescale(final+EngineF.Pi);travel=EngineF.Rescale(travel+EngineF.Pi);speed=-30;
            }
            if(line){if(firstTurn)Turn(x,y,travel,initial,1.5f);Line(x,y,gx,gy,speed);if(endTurn)Turn(gx,gy,gyaw,final,1.5f);_path=output;}
            else if(endTurn){Turn(gx,gy,gyaw,final,1.5f);_path=output;} // no line/final turn retains old path
        }
        else
        {
            float q=(y-gy)/EngineF.Bits(0x3ED413CD),dx=x-gx,t=-q;if(q>dx)t=q;
            float b=dx+t,positive=0;if(b>0)positive=b;float backup=75;if(positive<backup)backup=positive;
            if(backup>2.5f){float nx=x+(-backup)*MathF.Cos(yaw),ny=y+(-backup)*MathF.Sin(yaw);Line(x,y,nx,ny,-45);Info("MinimalAnglePlanner.Plan.Backup",F(-backup));x=nx;y=ny;}
            float ddx=gx-x,ddy=gy-y,bearing=MathF.Atan2(ddy,ddx),delta=EngineF.RadiansMinus(bearing,yaw);
            float length=(float)Math.Sqrt((double)ddx*ddx+(double)ddy*ddy);
            if(length>2.5f) // Native BLE skips the step on unordered operands.
            {
                if((double)MathF.Abs(delta)>0.01){Turn(x,y,bearing,delta,1);Info("MinimalAnglePlanner.Plan.Turn0",F(delta));yaw=bearing;}
                float nx=x+length*MathF.Cos(yaw),ny=y+length*MathF.Sin(yaw);Line(x,y,nx,ny,45);Info("MinimalAnglePlanner.Plan.Straight",F(length));x=nx;y=ny;
            }
            float end=EngineF.RadiansMinus(gyaw,yaw);
            if((double)MathF.Abs(end)>0.01){Turn(x,y,gyaw,end,1);Info("MinimalAnglePlanner.Plan.Turn1",F(end));yaw=gyaw;}
            Info("MinimalAnglePlanner.FinalPosition",$"({F(x)}, {F(y)}, {F(yaw*EngineF.Bits(0x42652EE1))}deg)");_path=output;
        }
        _present=true;return 1;
    }
    public bool GetCompletePath(Pose3d start,PathMotionProfile profile,out IReadOnlyList<PathSegment> path,out byte goal)
    {
        goal=_goalIndex;path=_path;
        if(!_present)return false; // base internal +0x28==0: no copy
        path=NativePathBuffer.ApplyKnownProfile(_path,profile,missing,source);_path=path;return true;
    }
}

// fidelity: M13-034
internal static class NativePathBuffer
{
    private static float Speed(PathSegment s)=>s switch{PathSegment.Line l=>l.SpeedMmps,PathSegment.Arc a=>a.SpeedMmps,PathSegment.PointTurn t=>t.SpeedRadPerSec,_=>float.NaN};
    private static PathSegment Profile(PathSegment s,float speed,float accel,float decel)=>s switch
    {PathSegment.Line l=>l with{SpeedMmps=speed,AccelMmps2=accel,DecelMmps2=decel},PathSegment.Arc a=>a with{SpeedMmps=speed,AccelMmps2=accel,DecelMmps2=decel},PathSegment.PointTurn t=>t with{SpeedRadPerSec=speed,AccelRadPerSec2=accel,DecelRadPerSec2=decel},_=>s};
    // The full checked arithmetic accepts separately recovered GetLength/GetEndPose metadata.
    // Until M13-011 supplies those helpers, production explicitly uses only ApplyKnownProfile's bounded slice.
    internal static IReadOnlyList<PathSegment> ApplyMotionProfile(IReadOnlyList<PathSegment> input,PathMotionProfile p,
        Func<PathSegment,NativePlanDependencies.Geometry> geometry,SourceLogSink source,Action<string> missing)
    {
        var reverse=new List<PathSegment>();bool boundary=true;float eps=EngineF.Bits(0x3727C5AC);
        for(int i=input.Count-1;i>=0;i--)
        {
            var original=input[i];
            if(original is not (PathSegment.Line or PathSegment.Arc or PathSegment.PointTurn))
            {source(EngineLogLevel.Warning,null,"IPathPlanner.ApplyMotionProfile.UnknownSegment","Path has invalid segment");return Array.Empty<PathSegment>();}
            if(original is PathSegment.PointTurn)
            {reverse.Add(Profile(original,MathF.CopySign(MathF.Abs(p.PointTurnSpeedRadPerSec),Speed(original)),p.PointTurnAccelRadPerSec2,p.PointTurnDecelRadPerSec2));boundary=true;continue;}
            float speed=MathF.CopySign(1,Speed(original))*p.SpeedMmps;
            if(speed<0 && MathF.Abs(p.ReverseSpeedMmps)<=eps)missing("M13-034 reverse-speed warning 0x00508BB4..0x00508C0C has an unresolved source key; profile+0 magnitude is still used");
            float downstream=boundary?0:Speed(reverse[^1]);
            float upstream=input.Count>1 && (i==0 || input[i-1] is PathSegment.PointTurn)?0:p.SpeedMmps;
            float length=geometry(original).Length;
            float ds=downstream*downstream,us=upstream*upstream,den=length*-2f,required=(ds-us)/den;
            if(!(MathF.Abs(required)<eps) && required>p.DecelMmps2 && MathF.Abs(required-p.DecelMmps2)>1f)
            {
                float v=length*-2f;v*=p.DecelMmps2;v-=ds;v=-v;
                speed=MathF.CopySign(MathF.Sqrt(v),Speed(original));
            }
            reverse.Add(Profile(original,speed,p.AccelMmps2,p.DecelMmps2));boundary=false;
        }
        reverse.Reverse();var output=new List<PathSegment>();
        for(int i=0;i<reverse.Count;i++)
        {
            var segment=reverse[i];float speed=Speed(segment);
            if(segment is PathSegment.PointTurn){Append(output,segment,missing);continue;}
            float downstream=i+1==reverse.Count || reverse[i+1] is PathSegment.PointTurn?0:Speed(reverse[i+1]);
            if((speed>0)!=(downstream>0))downstream=MathF.CopySign(MathF.Abs(downstream),speed);
            var g=geometry(segment);float tail=(downstream*downstream-speed*speed)/(p.DecelMmps2*-2f);
            if(segment is PathSegment.Line line && tail>0 && tail<g.Length && MathF.Abs(tail-g.Length)>=5f)
            {
                float distance=MathF.CopySign(g.Length-tail,speed);
                float x=line.FromX+distance*MathF.Cos(g.End.Theta),y=line.FromY+distance*MathF.Sin(g.End.Theta);
                Append(output,line with{ToX=x,ToY=y},missing);
                float next=downstream==0?MathF.CopySign(20,speed):MathF.CopySign(MathF.Abs(downstream),speed);
                float secondX=line.FromX+distance*MathF.Cos(g.End.Theta),secondY=line.FromY+distance*MathF.Sin(g.End.Theta);
                Append(output,line with{FromX=secondX,FromY=secondY,SpeedMmps=next},missing);
            }
            else
            {
                if(MathF.Abs(downstream)<20 || i==reverse.Count-1)downstream=MathF.CopySign(20,speed);
                bool beforeTurn=i+1<reverse.Count && reverse[i+1] is PathSegment.PointTurn;
                bool lastAfterTurn=i==reverse.Count-1 && i>0 && reverse[i-1] is PathSegment.PointTurn;
                if(input.Count>=2 && !beforeTurn && !lastAfterTurn)segment=Profile(segment,downstream,p.AccelMmps2,p.DecelMmps2);
                Append(output,segment,missing);
            }
        }
        missing("M13-034 IPathPlanner.PrintPath 0x005090A0 output formatting remains unbuilt");return output;
    }
    internal static bool Append(List<PathSegment> output,PathSegment segment,Action<string> missing,string name="AppendSegment")
    {
        if(output.Count>=128){missing($"M13-034 CoreTechPrint: ERROR ({name}): Exceeded path size\n");return false;}
        output.Add(segment);return true;
    }
    internal static IReadOnlyList<PathSegment> ApplyKnownProfile(IReadOnlyList<PathSegment> path,PathMotionProfile p,Action<string> missing,SourceLogSink? source=null)
    {
        if(path.Any(s=>s is not (PathSegment.Line or PathSegment.Arc or PathSegment.PointTurn)))
        {source?.Invoke(EngineLogLevel.Warning,null,"IPathPlanner.ApplyMotionProfile.UnknownSegment","Path has invalid segment");return Array.Empty<PathSegment>();}
        missing("M13-034 ApplyMotionProfile two-pass length/end-pose/split calls require source-backed M13-011 PathSegment helpers (0x0085BC00..0x0085C034); only checked profile-field assignment built, continuity/deceleration/splitting remain unbuilt");
        var output=new List<PathSegment>();
        foreach(var s in path)
        {
            float Signed(float speed,float magnitude)=>MathF.CopySign(magnitude,speed);
            PathSegment next=s switch{
                PathSegment.Line l=>l with{SpeedMmps=MathF.CopySign(1,l.SpeedMmps)*p.SpeedMmps,AccelMmps2=p.AccelMmps2,DecelMmps2=p.DecelMmps2},
                PathSegment.Arc a=>a with{SpeedMmps=MathF.CopySign(1,a.SpeedMmps)*p.SpeedMmps,AccelMmps2=p.AccelMmps2,DecelMmps2=p.DecelMmps2},
                PathSegment.PointTurn t=>t with{SpeedRadPerSec=Signed(t.SpeedRadPerSec,MathF.Abs(p.PointTurnSpeedRadPerSec)),AccelRadPerSec2=p.PointTurnAccelRadPerSec2,DecelRadPerSec2=p.PointTurnDecelRadPerSec2},_=>s};
            Append(output,next,missing);
        }
        return output;
    }
}
