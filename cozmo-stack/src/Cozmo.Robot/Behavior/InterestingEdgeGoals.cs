using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Vision;
using Cozmo.Transport;
using System.Text.Json;
namespace Cozmo.Robot.Behavior;

// fidelity: M11-089
public readonly record struct InterestingEdgeSegment(Point3f A,Point3f B,Point3f Normal);
public sealed class InterestingEdgeRegion(float area,IReadOnlyList<InterestingEdgeSegment> segments)
{
    public float Area {get;}=area;
    public IReadOnlyList<InterestingEdgeSegment> Segments {get;}=segments;
}
public readonly record struct InterestingEdgeGoal(InterestingEdgeRegion Region,uint Segment,float Score);
public sealed record InterestingEdgeConfig(bool AllowBehind,float DistanceMin,float DistanceMax,float LookInside,
    float FrontClearance,float BehindClearance,float AngleStep,byte AngleTries)
{
    // 5E6236..5E62CC maps these eight fields; these defaults are the checked shipped config values.
    public static InterestingEdgeConfig Shipped {get;}=new(false,105f,110f,15f,0f,10f,20f,1);
    public static InterestingEdgeConfig Read(JsonElement json)=>new(
        json.GetProperty("allowGoalsBehindOtherEdges").GetBoolean(),
        json.GetProperty("distanceFromLookAtPointMin_mm").GetSingle(),json.GetProperty("distanceFromLookAtPointMax_mm").GetSingle(),
        json.GetProperty("distanceInsideGoalToLookAt_mm").GetSingle(),json.GetProperty("additionalClearanceInFront_mm").GetSingle(),
        json.GetProperty("additionalClearanceBehind_mm").GetSingle(),json.GetProperty("vantagePointAngleOffsetPerTry_deg").GetSingle(),
        json.GetProperty("vantagePointAngleOffsetTries").GetByte());
}

/// <summary>Native map slots+24/+30 and the exact type-pair tables, not legacy polygon-map queries.</summary>
public interface IInterestingEdgeMap
{
    float Scalar24();
    uint Ray30(Point2f first,Point2f second,IReadOnlyList<bool> contentTypes);
}
public sealed class InterestingEdgeInputs
{
    public required Pose3d RobotPose {get;init;}
    public required Pose3d WorldOrigin {get;init;}
    public required IReadOnlyList<InterestingEdgeRegion> Regions {get;init;}
    public required IInterestingEdgeMap? Map {get;init;}
    public Func<EngineRandom>? GetRng {get;init;}
    public Action<string>? EraseDebugSegments {get;init;}
}

/// <summary>Checked goal consumers, with lifecycle/state producers deliberately outside this unit.</summary>
public sealed class InterestingEdgeGoalSelector
{
    private static readonly float Epsilon=BitConverter.UInt32BitsToSingle(0x3727C5AC);
    private static readonly float Max=BitConverter.UInt32BitsToSingle(0x7F7FFFFF);
    private static readonly float Milli=BitConverter.UInt32BitsToSingle(0x3A83126F);
    private static readonly float Degree=BitConverter.UInt32BitsToSingle(0x3C8EFA35);
    private static readonly float Front=BitConverter.UInt32BitsToSingle(0x42006666),Behind=BitConverter.UInt32BitsToSingle(0x425F999A);
    public static readonly IReadOnlyList<bool> GoalRayTypes=Array.AsReadOnly(new[]{false,false,false,false,false,false,false,true,true,false,true});
    public static readonly IReadOnlyList<bool> VantageRayTypes=Array.AsReadOnly(new[]{false,false,false,true,true,false,true,true,true,true,true});
    public InterestingEdgeConfig Config {get;set;}=InterestingEdgeConfig.Shipped;
    public List<Pose3d> Vantage168 {get;}=new();
    public InterestingEdgeGoalSort Sort {get;}=new();
    private static Point3f Sub(Point3f a,Point3f b)=>new(a.X-b.X,a.Y-b.Y,a.Z-b.Z);
    private static Point3f Add(Point3f a,Point3f b)=>new(a.X+b.X,a.Y+b.Y,a.Z+b.Z);
    private static Point3f Mul(Point3f a,float b)=>new(a.X*b,a.Y*b,a.Z*b);
    private static Point3f Neg(Point3f a)=>new(-a.X,-a.Y,-a.Z);
    private static Point3f Mid(InterestingEdgeSegment s)=>Mul(Add(s.A,s.B),0.5f);
    private static float Dot(Point3f a,Point3f b){float v=a.X*b.X;v+=a.Y*b.Y;v+=a.Z*b.Z;return v;}
    private static void Erase(InterestingEdgeInputs i)
    {
        if(i.EraseDebugSegments is {} erase)erase("BehaviorVisitInterestingEdge.kVieDrawDebugInfo");
        else SteppedBehavior.ReportMissing("M11-089 Viz erase recipient in 0x005E6520/0x005E675E, key BehaviorVisitInterestingEdge.kVieDrawDebugInfo");
    }
    private static void Clear(List<Pose3d> poses){foreach(var pose in poses)pose.Dispose();poses.Clear();}

    public bool PickGoals(InterestingEdgeInputs input,List<InterestingEdgeGoal> output)
    {
        output.Clear();if(input.Regions.Count==0)return true;
        Point3f position;
        using(var robot=new Pose3d(input.RobotPose))position=new Point3f(robot.Translation);
        if(input.Map is null){StackDiagnostics.Write("M1-034 M11-089: skip PickGoals null map read at 0x005E68A6; cleared output retained");return false;}
        float r=input.Map.Scalar24(),t=r*Milli;
        float area=(t*t)*4f+Epsilon,length=(r*r)+(r*r)+Epsilon;
        foreach(var region in input.Regions)
        {
            if(area>=region.Area)continue; // unordered does not take native BGE
            float best=Max;uint bestIndex=0;
            for(int index=0;index<region.Segments.Count;index++)
            {
                var segment=region.Segments[index];var d=Sub(segment.B,segment.A);
                if(length>=Dot(d,d))continue;
                var v=Sub(position,segment.A);float q=Dot(v,d),score;
                if(!(q>0f))score=Dot(v,v); // BLS includes unordered
                else
                {
                    float norm=Dot(d,d);
                    if(!(norm>q)){var toB=Sub(position,segment.B);score=Dot(toB,toB);}
                    else {float fraction=q/norm;var projected=Add(segment.A,Mul(d,fraction));var delta=Sub(position,projected);score=Dot(delta,delta);}
                }
                if(best+(-Epsilon)>score){best=score;bestIndex=(uint)index;}
            }
            if(MathF.Abs(best-Max)<Epsilon)continue;
            output.Add(new(region,bestIndex,best));
        }
        return true;
    }

    public bool? CheckGoalReachable(InterestingEdgeInputs input,Point3f goal)
    {
        Vec3 robot;
        using(var flat=input.RobotPose.GetWithRespectToRoot())robot=flat.Translation;
        if(input.Map is null){StackDiagnostics.Write("M1-034 M11-089: skip CheckGoalReachable null map read at 0x005E6C58");return null;}
        uint value=input.Map.Ray30(new((float)robot.X,(float)robot.Y),new(goal.X,goal.Y),GoalRayTypes);
        return (value^1u)!=0; // XOR 1, not !value
    }

    public bool GenerateVantagePoints(InterestingEdgeInputs input,InterestingEdgeGoal goal,Point3f center,List<Pose3d> output)
    {
        Clear(output);
        if(goal.Segment>=goal.Region.Segments.Count)
        {StackDiagnostics.Write("M1-034 M11-089: skip stale segment read at 0x005E6D34..0x005E6D44; cleared output retained");return false;}
        var segment=goal.Region.Segments[(int)goal.Segment];
        for(int attempt=0;attempt<=2*Config.AngleTries;attempt++)
        {
            var normal=segment.Normal;
            if(attempt!=0)
            {
                float angle=Config.AngleStep*((attempt+1)/2);
                if((attempt&1)!=0)angle=-angle;
                angle*=Degree;
                normal=new Point3f(new Rotation3d(angle,new Vec3(0,0,1)).Rotate(normal.ToVec3()));
            }
            if(input.GetRng is not {} getRng)
            {SteppedBehavior.ReportMissing("M11-089 Behavior GetRNG owner at 0x005E6DA2..0x005E6DD8; no host Random substitute");return false;}
            float draw=(float)getRng().RandDblInRange((double)Config.DistanceMin,(double)Config.DistanceMax);
            var position=Add(center,Mul(normal,draw));
            var first=Add(position,Mul(normal,Config.BehindClearance+Behind));
            var second=Sub(position,Mul(normal,Config.FrontClearance+Front));
            if(input.Map is null){StackDiagnostics.Write("M1-034 M11-089: skip vantage ray null map read at 0x005E6EC0; RNG draw retained");return false;}
            if(input.Map.Ray30(new(first.X,first.Y),new(second.X,second.Y),VantageRayTypes)!=0)continue;
            var direction=Neg(normal);float x=Dot(direction,new(1,0,0)),minusY=Dot(direction,new(0,-1,0));
            float angleToLook=MathF.Acos(x);if(minusY>=0f)angleToLook=-angleToLook;
            output.Add(new Pose3d(new Rotation3d(angleToLook,new Vec3(0,0,1)),position.ToVec3(),"",input.WorldOrigin));
            return true;
        }
        return true;
    }

    public bool? IsRunnable(InterestingEdgeInputs input)
    {
        Erase(input);Clear(Vantage168);
        var scores=new List<InterestingEdgeGoal>();if(!PickGoals(input,scores))return null;
        if(scores.Count!=0 && !Sort.TrySort(scores))return null;
        foreach(var goal in scores)
        {
            if(goal.Segment>=goal.Region.Segments.Count)
            {StackDiagnostics.Write("M1-034 M11-089: skip stale goal read at 0x005E65C4..0x005E65C6");continue;}
            var segment=goal.Region.Segments[(int)goal.Segment];var midpoint=Mid(segment);
            if(!Config.AllowBehind)
            {
                var reachable=CheckGoalReachable(input,midpoint);
                if(reachable is null)return null;
                if(!reachable.Value)continue;
            }
            var center=Add(midpoint,Mul(Neg(segment.Normal),Config.LookInside));var poses=new List<Pose3d>();
            if(!GenerateVantagePoints(input,goal,center,poses))return null;
            if(poses.Count==0)continue;
            Vantage168.AddRange(poses);break;
        }
        if(Vantage168.Count==0)Erase(input);
        return Vantage168.Count!=0;
    }
}
