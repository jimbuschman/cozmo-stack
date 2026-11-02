namespace Cozmo.Robot.Vision;

// fidelity: M11-021, M11-033
// ComputePixelShifts 0x00539258 and its captured-deque helper 0x005394BC.
internal static class NativeRollingShutter
{
    internal readonly record struct Shift(float X,float Y);
    private static readonly float QueryStep=BitConverter.UInt32BitsToSingle(0x3F0AAAAB);
    private static readonly float Dt=BitConverter.UInt32BitsToSingle(0x3C088889);
    private static readonly float Epsilon=BitConverter.UInt32BitsToSingle(0xB727C5AC);

    internal static bool Step(IReadOnlyList<NativeImageImu> history,uint query,int targetRow,int height,bool correctY,out Shift shift)
    {
        shift=default;if(history.Count==0)return false;
        int index=0;while(index<history.Count && history[index].ComputedTimestamp<query)index++;
        float y,z;
        if(index==history.Count){var last=history[^1];y=last.RateY;z=last.RateZ;}
        else
        {
            if(index==0)return false;
            var after=history[index];var before=history[index-1];
            int a=after.Line,b=before.Line;
            if(after.ComputedTimestamp==0)a=unchecked(a+height);
            else if(before.ImageId<after.ImageId)b=unchecked(b-height);
            float n=unchecked(targetRow-b),d=unchecked(a-b);
            float dy=after.RateY-before.RateY;float dz=after.RateZ-before.RateZ;
            float py=dy*n;float pz=dz*n;py=py/d;pz=pz/d;
            y=before.RateY+py;z=before.RateZ+pz;
        }
        float x=z*22f;float vertical=y*22f;
        if(!correctY)vertical=0f;
        x=x*Dt;vertical=vertical*Dt;shift=new(x,vertical);return true;
    }

    internal static Shift[] Compute(uint timestamp,int height,IReadOnlyList<NativeImageImu> history,bool correctY,out bool incomplete)
    {
        var result=new Shift[120];float x=0,y=0;incomplete=false;
        uint rows=(uint)height/120u;
        for(int i=1;i<=120;i++)
        {
            float row=(float)rows*(float)(120-i);
            float offset=(float)i*QueryStep;
            float rounded=MathF.Round(offset,MidpointRounding.AwayFromZero);
            float query=(float)timestamp-rounded;
            bool ok=Step(history,ObservableObject.SaturatingU32(query),Animation.AnimationScheduler.VcvtS32(row),height,correctY,out var delta);
            if(!ok)incomplete=true;
            x=x+delta.X;y=y+delta.Y;result[i-1]=new(x,y);
        }
        return result;
    }

    // Returns false for the source's invalid-corner/throw-smallest-marker branches.
    internal static bool TryCorrect(ObservedMarker marker,int width,int height,IReadOnlyList<Shift> shifts,out ObservedMarker corrected,out bool badIndex)
    {
        corrected=marker;badIndex=false;var corners=new Vec2[marker.Corners.Length];
        int rows=height/120;
        for(int i=0;i<corners.Length;i++)
        {
            float rawX=(float)marker.Corners[i].X,rawY=(float)marker.Corners[i].Y;
            float stripe=rawY/(float)rows;
            int index=Animation.AnimationScheduler.VcvtS32(MathF.Floor(stripe));
            if(unchecked((uint)index)>=(uint)shifts.Count){badIndex=true;return false;}
            float x=rawX-shifts[index].X,y=rawY-shifts[index].Y;
            float maxX=(float)width+Epsilon,maxY=(float)height+Epsilon;
            // VCMPE unordered has N=0,Z=0,C=1,V=1: both source bounds branches pass NaN.
            if(!(float.IsNaN(x) || x>=Epsilon && x<maxX) || !(float.IsNaN(y) || y>=Epsilon && y<maxY))return false;
            corners[i]=new Vec2(x,y);
        }
        corrected=marker with {Corners=corners};return true;
    }
}
