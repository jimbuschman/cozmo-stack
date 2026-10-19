using Cozmo.Robot.Vision;
using Cozmo.Transport;

namespace Cozmo.Robot;

// fidelity: M4-052
// OV128 L4-L7, corrected by 20261018-ov128-check.md. This is the live SetObjectLights callee.
internal static class RelativeCubeLights
{
    // OV129 C1-C5: supplied pose is already flattened by GetTopMarker. Do not compose Marker.Pose again.
    internal static Point3f[] Get3dCorners(KnownMarker marker,Pose3d pose)
    {
        float width=(float)marker.SizeMm,height=(float)(double.IsNaN(marker.HeightMm)?marker.SizeMm:marker.HeightMm);
        // Canonical words BF000000/3F000000 and +0; all scale operations are binary32.
        var corners=new Point3f[]{new(width*-0.5f,0,height*0.5f),new(width*-0.5f,0,height*-0.5f),
            new(width*0.5f,0,height*0.5f),new(width*0.5f,0,height*-0.5f)};
        var transform=pose.Transform; // GetTransform 0x0084499A; no parent walk here.
        // ApplyTo 0x0050343C..0x00503520 visits physical slots 0,2,1,3, including unused C3.
        foreach(int i in new[]{0,2,1,3})corners[i]=new Point3f(transform.Apply(corners[i].ToVec3()));
        return corners;
    }
    internal static Vec3 ComputeNormal(KnownMarker marker,Pose3d pose)
    {
        var c=Get3dCorners(marker,pose);
        var a=new Point3f(c[2].X-c[0].X,c[2].Y-c[0].Y,c[2].Z-c[0].Z);
        var b=new Point3f(c[1].X-c[0].X,c[1].Y-c[0].Y,c[1].Z-c[0].Z);
        // OV129 N2-N4, 0x0087E418..0x0087E4A6. Separate products: no FMA or normalization.
        float t0=a.Z*b.X,t1=b.Y*a.Z,t2=b.Z*a.X,t3=a.Y*b.Z,t4=b.Y*a.X,t5=a.Y*b.X;
        float ny=t2-t0,nx=t1-t3,nz=t5-t4;
        return new(nx,ny,nz);
    }
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
                var normal=checkedNormalInput is null?ComputeNormal(marker,root):checkedNormalInput(marker,root);
                // GetTopMarker 0x004E69F8..0x004E6A32: include the zero products, then strict max (ties keep first).
                float dot=(float)normal.X*0;dot+=(float)normal.Y*0;dot+=(float)normal.Z*1;
                if(dot>best){best=dot;selected=marker;top?.Dispose();top=new Pose3d(root);}
            }
            var expected=cube.Markers.FirstOrDefault(m=>m.Face==BlockFace.Top);
            if(selected is null || expected is null || top is null)
            {
                policyLog("M1-034/M4-052: skipped relative lights at sentinel marker-code read 0x004E11CA/0x004E2108 or GetMarker uncaught throw 0x004E5EDC; prior LED state retained");
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
