namespace Cozmo.Robot.Vision;

// fidelity: M11-098
internal static class NativeMotionGround
{
    internal readonly record struct Result(float Fraction,float X,float Y);
    internal static Result Extract(NativeMotionImage closedMask,NativeGroundFrame current,int fullColumns,int fullRows,float scale,Action<string> log)
    {
        var h=current.Homography;
        var quad=NativeGroundPlaneProjection.Project(h,fullColumns,fullRows);
        float inv=1f/scale;
        var x=new float[4];var y=new float[4];
        int minX=int.MaxValue,minY=int.MaxValue,maxX=int.MinValue,maxY=int.MinValue;
        for(int i=0;i<4;i++)
        {
            x[i]=quad[i].X*inv;y[i]=quad[i].Y*inv;
            int xi=Animation.AnimationScheduler.VcvtS32(x[i]),yi=Animation.AnimationScheduler.VcvtS32(y[i]);minX=Math.Min(minX,xi);maxX=Math.Max(maxX,xi);minY=Math.Min(minY,yi);maxY=Math.Max(maxY,yi);
        }
        int left=Math.Max(0,minX),top=Math.Max(0,minY),right=Math.Min(closedMask.Columns,maxX),bottom=Math.Min(closedMask.Rows,maxY);
        if(right<=left||bottom<=top){log("MotionDetector.ExtractGroundPlaneMotion.EmptyROI");return new(0,0,0);}
        int width=right-left,height=bottom-top;
        for(int i=0;i<4;i++){x[i]-=left;y[i]-=top;}
        static int R(float n)=>Animation.AnimationScheduler.VcvtS32(MathF.Round(n,MidpointRounding.ToEven));
        var points=new[]{0,2,3,1}.Select(i=>new NativeMotionPolygon.Point(R(x[i]),R(y[i]))).ToArray();
        var polygon=NativeMotionPolygon.Fill(height,width,points);var pixels=new byte[checked(width*height)];
        for(int row=0;row<height;row++)for(int col=0;col<width;col++)
            if(polygon[row*width+col]!=0)pixels[row*width+col]=closedMask.Pixels[(row+top)*closedMask.Stride+col+left];
        var copy=new NativeMotionImage(height,width,1,width,pixels,closedMask.Timestamp);
        var q=NativeMotionGroundMath.Quantile(copy,.5f,.95f);if(q.Count==0)return new(0,0,0);
        float px=(q.X+left)*scale,py=(q.Y+top)*scale;
        float v1x=x[1]-x[0],v1y=y[1]-y[0],v2x=x[2]-x[0],v2y=y[2]-y[0],v3x=x[3]-x[0],v3y=y[3]-y[0];
        float a=MathF.Abs(v3y*v2x-v3x*v2y)*.5f,b=MathF.Abs(v1x*v3y-v1y*v3x)*.5f;
        float fraction=(float)q.Count/(b+a);Span<float> solution=stackalloc float[3];
        if(!NativeMotionGroundMath.TrySolve(h,new float[]{px,py,1},solution)){log("MotionDetector.ExtractGroundPlaneMotion.SolveFailed");return new(0,0,0);}
        if(!(solution[2]>0)){log("MotionDetector.ExtractGroundPlaneMotion.BadProjectedZ");return new(0,0,0);}
        float inverse=1f/solution[2];return new(fraction,inverse*solution[0],inverse*solution[1]);
    }
}
