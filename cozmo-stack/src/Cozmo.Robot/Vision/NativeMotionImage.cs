using Cozmo.Robot.Behavior;

namespace Cozmo.Robot.Vision;

// fidelity: M11-098, M11-033
// M11-098: selected MotionDetector byte-image primitives, not the detector or an event producer.
internal sealed class NativeMotionImage
{
    internal NativeMotionImage(int rows,int columns,int channels,int stride,byte[] pixels,uint timestamp)
    {
        if (rows<=0 || columns<=0 || (channels!=1 && channels!=3)) throw new ArgumentOutOfRangeException(nameof(rows));
        if (stride<checked(columns*channels) || pixels.Length<checked((rows-1)*stride+columns*channels))
            throw new ArgumentException("Invalid image stride or storage.");
        Rows=rows;Columns=columns;Channels=channels;Stride=stride;Pixels=pixels;Timestamp=timestamp;
    }
    internal int Rows { get; }
    internal int Columns { get; }
    internal int Channels { get; }
    internal int Stride { get; }
    internal byte[] Pixels { get; }
    internal uint Timestamp { get; set; }

    internal NativeMotionImage DeepCopy()
    {
        int stride=checked(Columns*Channels);
        var bytes=new byte[checked(Rows*stride)];
        for(int y=0;y<Rows;y++) Array.Copy(Pixels,y*Stride,bytes,y*stride,stride);
        return new(Rows,Columns,Channels,stride,bytes,Timestamp);
    }
}

internal sealed class NativeMotionImageCache
{
    private NativeMotionImage? _full, _grayHalf, _rgbHalf;
    private bool _grayValid, _rgbValid;
    internal bool IsColor => _full?.Channels==3;
    internal void Reset(NativeMotionImage source)
    {
        // ImageBase assignment copies the header/timestamp while its Mat shares pixel storage.
        _full=new(source.Rows,source.Columns,source.Channels,source.Stride,source.Pixels,source.Timestamp);
        _grayValid=false;_rgbValid=false;
    }

    // 874304..348 / imgproc 821E8..82382: supplied destination size and method 0.
    internal NativeMotionImage GetHalf()
    {
        var source=_full ?? throw new InvalidOperationException("No current motion image.");
        bool gray=source.Channels==1;
        var half=gray?_grayHalf:_rgbHalf;
        if (gray?_grayValid:_rgbValid) return half!;
        int rows=(int)MathF.Round((float)source.Rows*0.5f,MidpointRounding.AwayFromZero);
        int cols=(int)MathF.Round((float)source.Columns*0.5f,MidpointRounding.AwayFromZero);
        int stride=checked(cols*source.Channels);
        if (half is null || half.Rows!=rows || half.Columns!=cols)
            half=new(rows,cols,source.Channels,stride,new byte[checked(rows*stride)],source.Timestamp);
        double fx=(double)cols/source.Columns,fy=(double)rows/source.Rows;
        double invX=1d/fx,invY=1d/fy;
        for(int y=0;y<rows;y++)
        {
            int sy=Math.Min((int)Math.Floor(y*invY),source.Rows-1);
            for(int x=0;x<cols;x++)
            {
                int sx=Math.Min((int)Math.Floor(x*invX),source.Columns-1);
                int si=sy*source.Stride+sx*source.Channels,di=y*half.Stride+x*source.Channels;
                for(int c=0;c<source.Channels;c++) half.Pixels[di+c]=source.Pixels[si+c];
            }
        }
        half.Timestamp=source.Timestamp;
        if(gray){_grayHalf=half;_grayValid=true;}else{_rgbHalf=half;_rgbValid=true;}
        return half;
    }
}

internal sealed class NativeMotionPrevious
{
    internal NativeMotionImage? Gray { get; private set; }
    internal NativeMotionImage? Rgb { get; private set; }
    internal bool GrayFiltered { get; private set; }
    internal bool RgbFiltered { get; private set; }
    internal void Set(NativeMotionImage current,bool filtered)
    {
        var copy=current.DeepCopy();
        if(current.Channels==1){Gray=copy;GrayFiltered=filtered;Rgb=null;RgbFiltered=false;}
        else {Rgb=copy;RgbFiltered=filtered;Gray=null;GrayFiltered=false;}
    }
}

internal static class NativeMotionRatio
{
    // 6ABA76 / 6AB8DE: channel gates precede strict f32 ratio.
    internal static (byte[] Mask,uint Count) Compare(NativeMotionImage previous,NativeMotionImage current)
    {
        if(previous.Rows!=current.Rows || previous.Columns!=current.Columns || previous.Channels!=current.Channels)
            throw new ArgumentException("Motion image dimensions and kinds must match.");
        var mask=new byte[checked(current.Rows*current.Columns)];uint count=0;
        for(int y=0;y<current.Rows;y++) for(int x=0;x<current.Columns;x++)
        {
            int a=y*previous.Stride+x*previous.Channels,b=y*current.Stride+x*current.Channels;
            bool eligible=true,changed=false;
            for(int c=0;c<current.Channels;c++) if(previous.Pixels[a+c]<11 || current.Pixels[b+c]<11) eligible=false;
            if(eligible) for(int c=0;c<current.Channels;c++)
            {
                float lo=Math.Min(previous.Pixels[a+c],current.Pixels[b+c]);
                float hi=Math.Max(previous.Pixels[a+c],current.Pixels[b+c]);
                if(hi/lo>1.25f){changed=true;break;}
            }
            if(changed){mask[y*current.Columns+x]=255;count++;}
        }
        return(mask,count);
    }

    // M11-098: selected full Mat, normalized 21x21, BORDER_REFLECT_101, RNE.
    internal static bool TryBoxFilter(NativeMotionImage image)
    {
        static int Reflect(int p,int length)
        {
            if(length==1)return 0;
            while(p<0 || p>=length)p=p<0?-p:2*length-p-2;
            return p;
        }
        int width=image.Columns, height=image.Rows, channels=image.Channels;
        var horizontal=new int[checked(width*height*channels)];
        var output=new byte[horizontal.Length];
        for(int y=0;y<height;y++)for(int c=0;c<channels;c++)
        {
            int sum=0,row=y*image.Stride;
            for(int dx=-10;dx<=10;dx++)sum+=image.Pixels[row+Reflect(dx,width)*channels+c];
            for(int x=0;x<width;x++)
            {
                horizontal[(y*width+x)*channels+c]=sum;
                sum+=image.Pixels[row+Reflect(x+11,width)*channels+c]-image.Pixels[row+Reflect(x-10,width)*channels+c];
            }
        }
        for(int x=0;x<width;x++)for(int c=0;c<channels;c++)
        {
            int sum=0;
            for(int dy=-10;dy<=10;dy++)sum+=horizontal[(Reflect(dy,height)*width+x)*channels+c];
            for(int y=0;y<height;y++)
            {
                output[(y*width+x)*channels+c]=(byte)((sum+220)/441);
                sum+=horizontal[(Reflect(y+11,height)*width+x)*channels+c]-horizontal[(Reflect(y-10,height)*width+x)*channels+c];
            }
        }
        for(int y=0;y<height;y++)Array.Copy(output,y*width*channels,image.Pixels,y*image.Stride,width*channels);
        return true;
    }
}