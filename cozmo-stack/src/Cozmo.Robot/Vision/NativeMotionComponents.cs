namespace Cozmo.Robot.Vision;

// fidelity: M11-098
// Supplied-mask unit: imgproc 35146..35360 and engine Image.GetConnectedComponents 8720F8.
internal readonly record struct NativeMotionComponent(uint Area,float CenterX,float CenterY,int Left,int Top,int Width,int Height);

internal static class NativeMotionComponents
{
    internal static bool TryAppend(NativeMotionImage mask,ICollection<NativeMotionComponent> output,out int[] labels)
    {
        if(mask.Channels!=1) throw new ArgumentException("A one-channel mask is required.",nameof(mask));
        ArgumentNullException.ThrowIfNull(output);
        int rows=mask.Rows,cols=mask.Columns;
        labels=new int[checked(rows*cols)];
        var parent=new List<int>{0};
        int Root(int label)
        {
            while(parent[label]<label) label=parent[label];
            return label;
        }
        int Union(int a,int b)
        {
            int ra=Root(a),rb=Root(b),root=Math.Min(ra,rb);
            while(parent[a]<a){int next=parent[a];parent[a]=root;a=next;}parent[a]=root;
            while(parent[b]<b){int next=parent[b];parent[b]=root;b=next;}parent[b]=root;
            return root;
        }
        bool Foreground(int x,int y)=>mask.Pixels[y*mask.Stride+x]!=0;
        for(int y=0;y<rows;y++) for(int x=0;x<cols;x++)
        {
            int i=y*cols+x;
            if(!Foreground(x,y)) continue;
            bool ul=y>0&&x>0&&Foreground(x-1,y-1);
            bool u=y>0&&Foreground(x,y-1);
            bool ur=y>0&&x+1<cols&&Foreground(x+1,y-1);
            bool l=x>0&&Foreground(x-1,y);
            if(u) labels[i]=labels[i-cols];
            else if(ur)
            {
                int r=labels[i-cols+1];
                labels[i]=ul?Union(r,labels[i-cols-1]):l?Union(r,labels[i-1]):r;
            }
            else if(ul) labels[i]=labels[i-cols-1];
            else if(l) labels[i]=labels[i-1];
            else {labels[i]=parent.Count;parent.Add(parent.Count);}
        }
        int next=1;
        for(int i=1;i<parent.Count;i++)
            if(parent[i]<i) parent[i]=parent[parent[i]];
            else parent[i]=next++;
        var left=Enumerable.Repeat(int.MaxValue,next).ToArray();
        var top=Enumerable.Repeat(int.MaxValue,next).ToArray();
        var right=Enumerable.Repeat(int.MinValue,next).ToArray();
        var bottom=Enumerable.Repeat(int.MinValue,next).ToArray();
        var area=new uint[next];var sumX=new ulong[next];var sumY=new ulong[next];
        for(int y=0;y<rows;y++)for(int x=0;x<cols;x++)
        {
            int i=y*cols+x,label=parent[labels[i]];labels[i]=label;
            left[label]=Math.Min(left[label],x);top[label]=Math.Min(top[label],y);
            right[label]=Math.Max(right[label],x);bottom[label]=Math.Max(bottom[label],y);
            area[label]++;sumX[label]+=(uint)x;sumY[label]+=(uint)y;
        }
        for(int i=0;i<next;i++)
        {
            double cx=(double)sumX[i]/area[i],cy=(double)sumY[i]/area[i];
            static float Deliver(double value,int limit)
            {
                if(value>=limit-1) value=limit-1;
                return (float)(value<=0?0:value); // unordered NaN follows the native HI path
            }
            output.Add(new(area[i],Deliver(cx,cols),Deliver(cy,rows),left[i],top[i],
                unchecked(right[i]-left[i]+1),unchecked(bottom[i]-top[i]+1)));
        }
        return true;
    }
}
