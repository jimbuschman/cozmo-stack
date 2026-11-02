using Cozmo.Robot.Animation;
namespace Cozmo.Robot.Vision;

// fidelity: M11-098
// Selected convex four-point, shift-zero, line8 byte mask (imgproc 0x42958).
internal static class NativeMotionPolygon
{
    internal readonly record struct Point(int X,int Y);
    private struct Edge {internal int Index,Step,End;internal long X,Dx;}
    internal static byte[] Fill(int rows,int columns,IReadOnlyList<Point> points)
    {
        if(rows<=0||columns<=0||points.Count!=4)throw new ArgumentException("Expected a nonempty byte image and four points.");
        var output=new byte[checked(rows*columns)];
        for(int i=0;i<4;i++)
        {
            var a=points[(i+3)%4];var b=points[i];
            OpenCv310.Line(output,rows,columns,a.X,a.Y,b.X,b.Y,255,8);
        }
        int minimum=0,minY=points[0].Y,maxY=minY,minX=points[0].X,maxX=minX;
        for(int i=1;i<4;i++)
        {
            var p=points[i];if(p.Y<minY){minY=p.Y;minimum=i;}maxY=Math.Max(maxY,p.Y);
            minX=Math.Min(minX,p.X);maxX=Math.Max(maxX,p.X);
        }
        if(maxY<0||minY>=rows||maxX<0||minX>=columns)return output;
        maxY=Math.Min(maxY,rows-1);int remaining=4;
        var edges=new[]{new Edge{Index=minimum,Step=1,End=minY},new Edge{Index=minimum,Step=3,End=minY}};
        for(int y=minY;y<=maxY;y++)
        {
            for(int side=0;side<2;side++)
            {
                var edge=edges[side];
                if(y>=edge.End)
                {
                    int start=edge.Index;bool found=false;
                    while(remaining>0)
                    {
                        int next=(start+edge.Step)%4;remaining--;
                        if(points[next].Y>y)
                        {
                            int dy=points[next].Y-y;long xs=(long)points[start].X<<16,xe=(long)points[next].X<<16;
                            edge.End=points[next].Y;edge.X=xs;edge.Dx=((xe-xs)*2+dy)/(2L*dy);edge.Index=next;found=true;break;
                        }
                        start=next;
                    }
                    if(!found)return output;
                    edges[side]=edge;
                }
            }
            if(remaining<0)break;
            if(y>=0)
            {
                int left=(int)((Math.Min(edges[0].X,edges[1].X)+0x8000)>>16);
                int right=(int)((Math.Max(edges[0].X,edges[1].X)+0x8000)>>16);
                left=Math.Max(0,left);right=Math.Min(columns-1,right);
                for(int x=left;x<=right;x++)output[y*columns+x]=255;
            }
            for(int side=0;side<2;side++){var edge=edges[side];edge.X+=edge.Dx;edges[side]=edge;}
        }
        return output;
    }
}
