namespace Cozmo.Robot.Vision;

// fidelity: M11-098
internal static class NativeMotionGroundMath
{
    // 0x6AAB64: independent signed x/y quantiles, roundf rank.
    internal static (uint Count,float X,float Y) Quantile(NativeMotionImage mask,float qx,float qy)
    {
        if(mask.Channels!=1)throw new ArgumentException("A gray mask is required.");
        var xs=new List<int>();var ys=new List<int>();
        for(int y=0;y<mask.Rows;y++)for(int x=0;x<mask.Columns;x++)if(mask.Pixels[y*mask.Stride+x]!=0){xs.Add(x);ys.Add(y);}
        if(xs.Count==0)return(0,0,0);
        int ix=(int)MathF.Round((float)(xs.Count-1)*qx,MidpointRounding.AwayFromZero);
        int iy=(int)MathF.Round((float)(ys.Count-1)*qy,MidpointRounding.AwayFromZero);
        xs.Sort();ys.Sort();return((uint)xs.Count,xs[ix],ys[iy]);
    }
    // imgproc LU32f 0x96AC0, square 3x3; false leaves a zero solution as cv::solve does.
    internal static bool TrySolve(ReadOnlySpan<float> matrix,ReadOnlySpan<float> vector,Span<float> output)
    {
        if(matrix.Length!=9||vector.Length!=3||output.Length<3)throw new ArgumentException("Expected 3x3 and 3x1 inputs.");
        var a=matrix.ToArray();var b=vector.ToArray();
        for(int i=0;i<3;i++)
        {
            int pivot=i;float best=MathF.Abs(a[i*3+i]);
            for(int k=i+1;k<3;k++){float candidate=MathF.Abs(a[k*3+i]);if(candidate>best){best=candidate;pivot=k;}}
            if(best<BitConverter.UInt32BitsToSingle(0x35A00000)){output[..3].Clear();return false;}
            if(pivot!=i)
            {
                for(int j=i;j<3;j++)(a[i*3+j],a[pivot*3+j])=(a[pivot*3+j],a[i*3+j]);
                (b[i],b[pivot])=(b[pivot],b[i]);
            }
            float inverse=-1f/a[i*3+i];
            for(int k=i+1;k<3;k++)
            {
                float alpha=a[k*3+i]*inverse;
                for(int j=i+1;j<3;j++)a[k*3+j]=a[k*3+j]+alpha*a[i*3+j];
                b[k]=b[k]+alpha*b[i];
            }
            a[i*3+i]=-inverse;
        }
        for(int i=2;i>=0;i--)
        {
            float sum=b[i];for(int j=i+1;j<3;j++)sum-=a[i*3+j]*b[j];
            b[i]=sum*a[i*3+i];
        }
        b.AsSpan().CopyTo(output);return true;
    }
}
