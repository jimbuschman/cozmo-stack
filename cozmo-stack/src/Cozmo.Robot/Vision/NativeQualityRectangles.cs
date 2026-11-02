namespace Cozmo.Robot.Vision;

// fidelity: M11-022, M11-080
// Update-local rectangle producer and BlackOutRects 0x006B33AC.
internal static class NativeQualityRectangles
{
    internal static ImageQualityRectangle Marker(ObservedMarker marker)
    {
        var (x,y,w,h)=MarkerDetector.BoundingRect(marker.Corners);
        return new(x,y,w,h);
    }
    internal static ImageQualityRectangle Detection(FaceRect rect)
    {
        static int Round(double value)=>Animation.AnimationScheduler.VcvtS32(MathF.Round((float)value,MidpointRounding.AwayFromZero));
        return new(Round(rect.X),Round(rect.Y),Round(rect.Width),Round(rect.Height));
    }
    internal static ImageQualityRectangle Intersect(ImageQualityRectangle rect,int width,int height)
    {
        long left=Math.Max(0L,rect.X),top=Math.Max(0L,rect.Y);
        long right=Math.Min((long)width,(long)rect.X+rect.Width),bottom=Math.Min((long)height,(long)rect.Y+rect.Height);
        if(right-left<1 || bottom-top<1)return default;
        return new((int)left,(int)top,(int)(right-left),(int)(bottom-top));
    }
    internal static GrayImage BlackOut(GrayImage image,IReadOnlyList<ImageQualityRectangle> rectangles)
    {
        var copy=new GrayImage(image.Width,image.Height,(byte[])image.Pixels.Clone());
        foreach(var rect in rectangles)
        {
            var clipped=Intersect(rect,image.Width,image.Height);
            for(int y=clipped.Y;y<clipped.Y+clipped.Height;y++)
                Array.Clear(copy.Pixels,y*copy.Width+clipped.X,clipped.Width);
        }
        return copy;
    }
}
