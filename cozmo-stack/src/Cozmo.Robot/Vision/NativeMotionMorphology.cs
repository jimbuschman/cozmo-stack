using Cozmo.Robot.Behavior;

namespace Cozmo.Robot.Vision;

// fidelity: M11-098
// Supplied sparse-kernel unit, imgproc 948A8/964C4 and u8 workers 90528/90B8C.
internal enum NativeMotionMorphOperation { Dilate, Erode, Close }

internal static class NativeMotionMorphology
{
    internal static bool TryApply(NativeMotionImage source,NativeMotionImage destination,
        int kernelRows,int kernelColumns,ReadOnlySpan<byte> kernel,int anchorX,int anchorY,NativeMotionMorphOperation operation)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(destination);
        if(kernelRows<=0||kernelColumns<=0||kernel.Length!=checked(kernelRows*kernelColumns))
            throw new ArgumentException("Invalid kernel dimensions or storage.");
        if(anchorX<0||anchorX>=kernelColumns||anchorY<0||anchorY>=kernelRows)
            throw new ArgumentOutOfRangeException(nameof(anchorX));
        if(source.Rows!=destination.Rows||source.Columns!=destination.Columns)
            throw new ArgumentException("Source and destination dimensions must match.");
        if(!Enum.IsDefined(operation)) throw new ArgumentOutOfRangeException(nameof(operation));
        var offsets=new List<(int X,int Y)>();
        for(int y=0;y<kernelRows;y++)for(int x=0;x<kernelColumns;x++)
            if(kernel[y*kernelColumns+x]!=0) offsets.Add((x-anchorX,y-anchorY));
        if(source.Channels!=1||destination.Channels!=1||offsets.Count==0||offsets.Count==kernel.Length)
        {
            SteppedBehavior.ReportMissing("M11-098 byte morphology: only supplied nonempty sparse one-channel kernel profile is available");
            return false;
        }
        int rows=source.Rows,cols=source.Columns;
        var input=new byte[checked(rows*cols)];
        for(int y=0;y<rows;y++) Array.Copy(source.Pixels,y*source.Stride,input,y*cols,cols);
        byte[] Pass(byte[] from,bool dilate)
        {
            var result=new byte[from.Length];
            for(int y=0;y<rows;y++)for(int x=0;x<cols;x++)
            {
                byte value=dilate?(byte)0:(byte)255;
                foreach(var (dx,dy) in offsets)
                {
                    long sx=(long)x+dx,sy=(long)y+dy;
                    byte sample=sx>=0&&sx<cols&&sy>=0&&sy<rows?from[(int)sy*cols+(int)sx]:(dilate?(byte)0:(byte)255);
                    value=dilate?Math.Max(value,sample):Math.Min(value,sample);
                }
                result[y*cols+x]=value;
            }
            return result;
        }
        byte[] output=operation switch
        {
            NativeMotionMorphOperation.Dilate=>Pass(input,true),
            NativeMotionMorphOperation.Erode=>Pass(input,false),
            _=>Pass(Pass(input,true),false)
        };
        for(int y=0;y<rows;y++) Array.Copy(output,y*cols,destination.Pixels,y*destination.Stride,cols);
        return true;
    }
}
