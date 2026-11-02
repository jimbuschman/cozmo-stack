namespace Cozmo.Robot.Vision;

// fidelity: M11-098
// Native periphery component reduction: background label zero participates.
internal sealed class NativePeripheralMotion
{
    internal readonly record struct Point(float X,float Y);
    internal readonly record struct Output(float Top,short TopX,short TopY,float Left,short LeftX,short LeftY,float Right,short RightX,short RightY,bool Any);
    private readonly float _leftCut,_rightCut,_topCut,_increase,_decrease,_max,_alpha;
    private readonly int _area;
    internal float Top { get; private set; }
    internal float Left { get; private set; }
    internal float Right { get; private set; }
    internal Point TopPoint { get; private set; }=new(-1,-1);
    internal Point LeftPoint { get; private set; }=new(-1,-1);
    internal Point RightPoint { get; private set; }=new(-1,-1);
    internal NativePeripheralMotion(int width,int height,float horizontal,float vertical,float increase,float decrease,float max,float stability)
    {
        _leftCut=width*horizontal;_rightCut=width-_leftCut;_topCut=height*vertical;
        _increase=increase;_decrease=decrease;_max=max;_alpha=stability;_area=unchecked(width*height);
    }
    private float Step(float old,float portion,bool active)
    {
        float amount=active?portion*_increase:0f;
        float value=(old+amount)-_decrease;
        return value<=0f?0f:value>=_max?_max:value;
    }
    private Point Smooth(Point old,Point incoming)
    {
        if(old.X<0)return incoming;
        float beta=1f-_alpha;
        return new(beta*old.X+_alpha*incoming.X,beta*old.Y+_alpha*incoming.Y);
    }
    internal static short Quantize(float value,float scale)=>unchecked((short)Animation.AnimationScheduler.VcvtS32(MathF.Round(value*scale,MidpointRounding.AwayFromZero)));
    internal Output Reduce(IReadOnlyList<NativeMotionComponent> components,float scale)
    {
        bool any=false;
        foreach(var item in components)
        {
            float area=(float)item.Area*scale;
            if(area<500f)continue;
            any=true;float portion=area/(float)_area;
            bool top=item.CenterY<=_topCut,left=item.CenterX<=_leftCut,right=!left&&item.CenterX>=_rightCut;
            Top=Step(Top,portion,top);Left=Step(Left,portion,left);Right=Step(Right,portion,right);
            var point=new Point(item.CenterX,item.CenterY);
            if(top)TopPoint=Smooth(TopPoint,point);if(left)LeftPoint=Smooth(LeftPoint,point);if(right)RightPoint=Smooth(RightPoint,point);
        }
        if(!any){Top=Step(Top,0,false);Right=Step(Right,0,false);Left=Step(Left,0,false);}
        bool a=Top>=_max,b=Left>=_max,c=Right>=_max;
        return new(a?Top:0,a?Quantize(TopPoint.X,scale):(short)0,a?Quantize(TopPoint.Y,scale):(short)0,
            b?Left:0,b?Quantize(LeftPoint.X,scale):(short)0,b?Quantize(LeftPoint.Y,scale):(short)0,
            c?Right:0,c?Quantize(RightPoint.X,scale):(short)0,c?Quantize(RightPoint.Y,scale):(short)0,a||b||c);
    }
}
