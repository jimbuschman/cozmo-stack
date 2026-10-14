using Cozmo.Transport;
namespace Cozmo.Robot.Manipulation;

// fidelity: M13-034
// Checked dependency control/arithmetic; callers must supply separately owned source-backed geometry/ApplyAction.
internal static class NativePlanDependencies
{
    // ApplyAction(check=false), 0x008505BC..0x008505E6: StateID packs signed x14/y14 and theta4.
    internal static LatticeState Advance(LatticeState state,MotionPrimitive p)=>LatticeState.FromId(new LatticeState(unchecked((short)(state.X+p.EndX)),unchecked((short)(state.Y+p.EndY)),(byte)p.EndTheta).Id);
    private static StateC Continuous(MotionPrimitiveSet p,LatticeState s)=>new(p.ResolutionMm*s.X,p.ResolutionMm*s.Y,p.Angles[s.Theta]);
    internal static (int Index,float Distance) FindClosest(MotionPrimitiveSet prims,LatticePlan? plan,StateC query)
    {
        if(plan is null || plan.Actions.Count==0)return(0,float.MaxValue);
        short qx=unchecked((short)(int)MathF.Round(query.X*prims.InverseResolution,MidpointRounding.AwayFromZero)),qy=unchecked((short)(int)MathF.Round(query.Y*prims.InverseResolution,MidpointRounding.AwayFromZero));
        byte qt=(byte)EngineF.ThetaIndex(query.Theta,prims.InverseAngleStep,prims.NumAngles);
        int best=0;float distance=float.MaxValue;var state=plan.Start;
        for(int i=0;i<plan.Actions.Count;i++)
        {
            if(state.X==qx && state.Y==qy && state.Theta==qt)return(i,0);
            float dx=prims.ResolutionMm*state.X-query.X,dy=prims.ResolutionMm*state.Y-query.Y;
            float d=(float)Math.Sqrt((double)dx*dx+(double)dy*dy);
            if(d<distance){distance=d;best=i;}state=Advance(state,plan.Actions[i]);
        }
        state=plan.Start;
        for(int i=0;i<plan.Actions.Count;i++)
        {
            var action=plan.Actions[i];float rx=query.X-prims.ResolutionMm*state.X,ry=query.Y-prims.ResolutionMm*state.Y;
            for(int j=0;j<action.Intermediate.Count-1;j++)
            {
                var sample=action.Intermediate[j];
                // Native exact-match test uses primitive-local coordinates, without the world offset.
                short sx=unchecked((short)(int)MathF.Round(sample.X*prims.InverseResolution,MidpointRounding.AwayFromZero)),sy=unchecked((short)(int)MathF.Round(sample.Y*prims.InverseResolution,MidpointRounding.AwayFromZero));
                byte st=(byte)EngineF.ThetaIndex(sample.Theta,prims.InverseAngleStep,prims.NumAngles);
                if(sx==qx && sy==qy && st==qt)return(i,0);
                float dx=sample.X-rx,dy=sample.Y-ry;
                float d=MathF.Sqrt((float)((double)dx*dx+(double)dy*dy));
                if(d<distance){distance=d;best=i;}
            }
            state=Advance(state,action);
        }
        return(best,distance);
    }
    internal static bool PlanIsSafe(LatticePlan old,int startIndex,float tolerance,MotionPrimitiveSet prims,
        Func<LatticeState,MotionPrimitive,bool,(LatticeState State,float Penalty)> apply,ref LatticePlan prefix,ref StateC endpoint,Action<string>? missing=null)
    {
        if(old.Actions.Count==0)return false;
        var state=old.Start;
        for(int i=0;i<Math.Min(startIndex,old.Actions.Count);i++)state=apply(state,old.Actions[i],false).State;
        var prefixStart=state;var start=Continuous(prims,state);endpoint=start;
        var actions=new List<MotionPrimitive>();var penalties=new List<float>();bool retain=true;
        for(int i=startIndex;i<old.Actions.Count;i++)
        {
            var step=apply(state,old.Actions[i],true);
            float prior=old.Penalties[i];
            if((double)step.Penalty>(double)prior+0.5)
            {missing?.Invoke($"M13-034 stdout collision diagnostic 0x00850D84: action {i}, starting from {startIndex}, old penalty {prior}, new penalty {step.Penalty}; printf formatting unbuilt");prefix=new(prefixStart,actions,old.Cost,old.Expansions){Penalties=penalties};return false;}
            state=step.State;
            if(retain)
            {
                actions.Add(old.Actions[i]);penalties.Add(prior);endpoint=Continuous(prims,state);
                float dx=endpoint.X-start.X,dy=endpoint.Y-start.Y;
                if(MathF.Sqrt((float)((double)dx*dx+(double)dy*dy))>tolerance)retain=false;
            }
        }
        prefix=new(prefixStart,actions,old.Cost,old.Expansions){Penalties=penalties};return true;
    }
    internal static LatticePlan Append(LatticePlan destination,LatticePlan source)=>destination with
    {Start=destination.Actions.Count==0?source.Start:destination.Start,Actions=destination.Actions.Concat(source.Actions).ToArray(),Penalties=destination.Penalties.Concat(source.Penalties).ToArray()};

    internal static void AppendToPath(MotionPrimitiveSet prims,LatticePlan plan,List<PathSegment> output,int skip,
        Func<MotionPrimitive,IReadOnlyList<PathSegment>> cachedPath,Func<PathSegment,float,float,float,PathSegment> offsetStart,Action<string> missing)
    {
        var state=plan.Start;
        foreach(var action in plan.Actions)
        {
            if(state.Theta>=prims.ByAngle.Count || action.ActionIndex<0 || action.ActionIndex>=prims.ByAngle[state.Theta].Count)
            {missing($"M13-034 CoreTechPrint: ERROR: can't look up prim for angle {state.Theta} and action id {action.ActionIndex}\n");return;}
            var primitive=prims.ByAngle[state.Theta][action.ActionIndex];
            if(skip==0)AddSegmentsToPath(cachedPath(primitive),output,prims.ResolutionMm*state.X,prims.ResolutionMm*state.Y,prims.Angles[state.Theta],offsetStart,missing);
            else skip--;
            state=Advance(state,primitive);
        }
    }
    internal static byte AddSegmentsToPath(IReadOnlyList<PathSegment> primitive,List<PathSegment> output,float x,float y,float heading,
        Func<PathSegment,float,float,float,PathSegment> offsetStart,Action<string> missing)
    {
        byte initial=unchecked((byte)output.Count);bool attempted=false;
        foreach(var raw in primitive)
        {
            var next=offsetStart(raw,x,y,heading);bool merged=false;
            if(output.Count>0)
            {
                var prior=output[^1];
                if(prior.GetType()==next.GetType() && next is not (PathSegment.Line or PathSegment.Arc or PathSegment.PointTurn))missing("M13-034 stdout unknown-kind diagnostic 0x00855A70 remains unbuilt");
                if(prior is PathSegment.Line a && next is PathSegment.Line b && (a.SpeedMmps>0)==(b.SpeedMmps>0))
                {output[^1]=a with{ToX=b.ToX,ToY=b.ToY};merged=true;}
                else if(prior is PathSegment.PointTurn t && next is PathSegment.PointTurn u && MathF.Abs(t.X-u.X)<EngineF.Bits(0x3727C5AC) && MathF.Abs(t.Y-u.Y)<EngineF.Bits(0x3727C5AC) && MathF.Abs(t.SpeedRadPerSec-u.SpeedRadPerSec)<EngineF.Bits(0x3727C5AC))
                {output[^1]=t with{TargetAngleRad=u.TargetAngleRad};merged=true;}
            }
            if(!merged){attempted=true;NativePathBuffer.Append(output,next,missing);}
        }
        return initial>0 && !attempted?unchecked((byte)(initial-1)):initial;
    }

    internal readonly record struct Geometry(int Kind,StateC Start,StateC End,float Length,float Raw0C,float Raw14,float CenterX=0,float CenterY=0,float Radius=0,float StartRad=0);
    internal static float ApplyPathSegment(PathSegment segment,Geometry g,ref StateC state,bool check,float threshold,
        int numAngles,float inverseAngleStep,Func<int,float,float,IEnumerable<float>> containedPenalties,SourceLogSink source,Action<string> missing)
    {
        if(!check){state=g.End;return 0;}
        if(g.Kind==3)
        {
            missing("M1-034/M13-034 undefined turn-weight padding read 0x008506E4: skipping this turn's penalty contribution, retaining total");state=g.End;return 0;
        }
        if(g.Kind is not (1 or 2))
        {source(EngineLogLevel.Error,null,"xythetaEnvironment.ApplyPathSegment",$"Undefined segment {g.Kind}");EngineErrorState.StoreAndMaybeBreak();return 1000;}
        float delta=EngineF.RadiansMinus(g.Raw0C,state.Theta);
        int count=Math.Max(Math.Max((int)MathF.Ceiling(g.Length*4f+1f),(int)MathF.Ceiling(MathF.Abs(delta)*EngineF.Bits(0x4122F983))),2);
        float den=count-1,inv=1f/den,weight=den/g.Length,penalty=0;
        float dx=inv*(g.End.X-g.Start.X),dy=inv*(g.End.Y-g.Start.Y),angleStep=inv*g.Raw14;
        for(int i=1;i<count;i++)
        {
            float x,y,angle;
            if(g.Kind==1){x=state.X+i*dx;y=state.Y+i*dy;angle=state.Theta;}
            else{float a=g.StartRad+i*angleStep;x=g.CenterX+g.Radius*MathF.Cos(a);y=g.CenterY+g.Radius*MathF.Sin(a);angle=state.Theta+i*angleStep;}
            int bucket=EngineF.ThetaIndex(angle,inverseAngleStep,numAngles);
            foreach(float obstaclePenalty in containedPenalties(bucket,x,y))
            {penalty+=weight*obstaclePenalty;if(penalty>=threshold)return penalty;}
        }
        state=g.End;return penalty; // Corrected H10: retain the subthreshold accumulator.
    }
    internal static bool PathIsSafe(IReadOnlyList<PathSegment> input,List<PathSegment> output,float startAngle,
        Func<PathSegment,Geometry> geometry,Func<PathSegment,Geometry,StateC,(StateC End,float Penalty)> apply,SourceLogSink source,Action<string> missing)
    {
        if(input.Count==0)return false;
        output.Clear();var first=geometry(input[0]);var state=first.Start with{Theta=startAngle};float total=0,limit=EngineF.Bits(0x3727C5AC);
        for(int i=0;i<input.Count;i++)
        {
            var g=geometry(input[i]);var step=apply(input[i],g,state);state=step.End;total+=step.Penalty;
            if(total>limit){source(EngineLogLevel.Info,"Unnamed","xythetaEnvironment.PathIsSafe",FormattableString.Invariant($"Collision along path segment {i} with penalty {step.Penalty:F6}, total penalty {total:F6}"));return false;}
            NativePathBuffer.Append(output,input[i],missing);
        }
        return true;
    }
}
