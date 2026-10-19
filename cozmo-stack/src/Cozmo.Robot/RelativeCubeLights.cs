using Cozmo.Robot.Vision;
using Cozmo.Transport;

namespace Cozmo.Robot;

// fidelity: M4-052
// OV128 L4-L7, corrected by 20261018-ov128-check.md. This is the live SetObjectLights callee.
internal static class RelativeCubeLights
{
    internal static int Corner(Radians a)
    {
        var p=new Radians(BitConverter.Int32BitsToSingle(0x3FC90FDB));
        return a>new Radians(0)?(a<p?3:6):(a>-p?9:12);
    }
    internal static int Face(Radians a)
    {
        var q=new Radians(BitConverter.Int32BitsToSingle(0x3F490FDB));
        var r=new Radians(BitConverter.Int32BitsToSingle(0x4016CBE4));
        if(a<q && a>=-q)return 1;
        if(a<r && a>=q)return 2;
        if(a< -q && a>=-r)return 8;
        return 4;
    }
    internal static LedPattern[] Rotate(LedPattern[] source,int mask,SourceLogSink log)
    {
        // 0x004E30DC..0x004E3102: false table C45B94, true table C45B90; dest[table[i]]=source[i].
        int shift=mask switch{1 or 3=>2,2 or 6=>3,4 or 12=>0,8 or 9=>1,_=>-1};
        if(shift<0)
        {
            log(EngineLogLevel.Error,null,"ActiveCube.MakeStateRelativeToXY",$"Unexpected reference LED {mask}.");
            return source;
        }
        var result=new LedPattern[4];
        for(int i=0;i<4;i++)result[(i+shift)%4]=source[i]; // copies both colours and all five timing words
        return result;
    }
    internal static LedPattern[]? Apply(ObservableObject cube,LedPattern[] leds,byte mode,System.Numerics.Vector2 point,SourceLogSink log,Action<string> policyLog,
        Func<KnownMarker,Pose3d,Vec3>? checkedNormalInput=null)
    {
        if(mode==0)return leds;
        if(mode is not(1 or 2))
        {
            log(EngineLogLevel.Error,null,"ActiveCube.MakeStateRelativeToXY",$"Unrecognized relative LED mode {mode}.");
            EngineErrorState.StoreAndMaybeBreak();
            return leds;
        }
        KnownMarker? selected=cube.Markers.FirstOrDefault();Pose3d? top=new Pose3d();
        float best=-float.MaxValue;
        // BlockWorld's standalone snapshots store a cube transform without a native origin parent.
        // Supply its identity origin so flattening the marker includes the cube transform. Existing
        // carried/native parent chains retain their real origin and are copied without replacement.
        using var origin=new Pose3d();using var objectPose=new Pose3d(cube.NativePose);
        if(!objectPose.HasParent)objectPose.SetParent(origin);
        try
        {
            foreach(var marker in cube.Markers)
            {
                using var placed=new Pose3d(marker.PoseOnObject);placed.SetParent(objectPose);
                using var root=placed.GetWithRespectToRoot();
                if(checkedNormalInput is null)
                {
                    // Reopened target of 0x004E69F8 is 0x0087E3FA: transformed corners and a cross product,
                    // not a rotated unit axis. OV128 does not check that callee's arithmetic. Do not invent it.
                    Behavior.SteppedBehavior.ReportMissing("M4-052 KnownMarker::ComputeNormal(Pose3d) 0x0087E3FA, called at0x004E69F8: transformed-corner arithmetic has no adopted row; relative light state/send skipped");
                    return null;
                }
                var normal=checkedNormalInput(marker,root);
                // GetTopMarker 0x004E69F8..0x004E6A32: include the zero products, then strict max (ties keep first).
                float dot=(float)normal.X*0;dot+=(float)normal.Y*0;dot+=(float)normal.Z*1;
                if(dot>best){best=dot;selected=marker;top?.Dispose();top=new Pose3d(root);}
            }
            var expected=cube.Markers.FirstOrDefault(m=>m.Face==BlockFace.Top);
            if(selected is null || expected is null || top is null)
            {
                policyLog("M1-034/M4-052: skipped relative lights with missing marker at native marker-code read 0x004E11CA/0x004E2108 (GetMarker null: 0x004E11D4/0x004E2124); prior LED state retained");
                return null;
            }
            int mask;
            if(selected.Code!=expected.Code)
            {
                string which=mode==1?"Corner":"Face";
                log(EngineLogLevel.Warning,null,$"ActiveCube.Get{which}ClosestToXY.IgnoringBecauseBlockOnSide","Ignoring because block is on its side");
                mask=mode==1?6:4;
            }
            else
            {
                float dx=point.X-(float)top.Translation.X,dy=point.Y-(float)top.Translation.Y;
                // M4-052: MathF.Atan2 stands for atan2f; phone/bionic last-ulp behaviour remains UNKNOWN.
                var direction=new Radians(MathF.Atan2(dy,dx));var yaw=new Radians((float)top.AngleAroundZ);
                mask=mode==1?Corner(direction-yaw):Face(-(yaw-direction));
            }
            return Rotate(leds,mask,log);
        }
        finally{top?.Dispose();}
    }
}
