namespace Cozmo.Robot;

// fidelity: M11-065, M11-068, M10-001
public struct Radians
{
    public float Value;
    public bool RescaleEnabled {get;private set;}
    public static readonly float Pi=BitConverter.Int32BitsToSingle(0x40490FDB),TwoPi=BitConverter.Int32BitsToSingle(0x40C90FDB),Epsilon=BitConverter.Int32BitsToSingle(0x3727C5AC);
    public Radians(){Value=0;RescaleEnabled=true;}
    public Radians(Radians value){Value=value.Value;RescaleEnabled=value.RescaleEnabled;}
    public void Assign(Radians value){Value=value.Value;RescaleEnabled=value.RescaleEnabled;}
    public Radians(float value,bool rescale=true){RescaleEnabled=rescale;Value=rescale?Rescale(value):value;}
    public void Assign(float value)=>Value=RescaleEnabled?Rescale(value):value;
    public static float Rescale(float v)
    {
        if(v>-Pi && v<=Pi)return v;
        if(MathF.Abs(v)<10)
        {while(v<=-Pi)v+=TwoPi;while(v>Pi)v-=TwoPi;return v;}
        float q=v/TwoPi;q-=0.5f;
        float ceiling=MathF.Ceiling(q); // EQUIVALENT_IMPLEMENTATION M13-023(e): unshipped bionic ceilf.
        int count=float.IsNaN(ceiling)?0:ceiling>=2147483648f?int.MaxValue:ceiling<=-2147483648f?int.MinValue:(int)ceiling;
        float amount=(float)count*TwoPi;return v-amount;
    }
    public static Radians operator +(Radians a,Radians b)=>new(a.Value+b.Value);
    public static Radians operator -(Radians a,Radians b)=>new(a.Value-b.Value);
    public static Radians operator *(Radians a,float b)=>new(a.Value*b);
    public static Radians operator -(Radians a)=>new(-a.Value,a.RescaleEnabled);
    // R2 0x0084CA66..0x0084CA96: Radians(a) (flag set, rescaled) first, then rescaled(a) - b.raw, flag set, rescaled.
    public static Radians operator -(float a,Radians b)=>new(new Radians(a).Value-b.Value);
    // R2 0x0084CC0E..0x0084CC1E: a copy of a with its flag forced to 1 is always rescaled, whatever a's own flag.
    public static bool IsNear(Radians a,Radians b,Radians tolerance)
    {float left=Rescale(a.Value);return MathF.Abs(Rescale(left-b.Value))<MathF.Abs(tolerance.Value);}
    public void Rescale(){if(RescaleEnabled)Value=Rescale(Value);}
    public static bool operator >(Radians a,Radians b)=>a.Value-b.Value>0&&!IsNear(a,b,new Radians(Epsilon));
    public static bool operator <(Radians a,Radians b)=>b>a;
    public static bool operator >=(Radians a,Radians b)=>a>b||IsNear(a,b,new Radians(Epsilon));
    public static bool operator <=(Radians a,Radians b)=>b>=a;
    public static bool IsNear(float a,float b,float tolerance)=>MathF.Abs(Rescale(Rescale(a)-b))<MathF.Abs(tolerance);
    public static bool Greater(float a,float b)=>a-b>0&&!IsNear(a,b,Epsilon);
    public static bool GreaterEqual(float a,float b)=>Greater(a,b)||IsNear(a,b,Epsilon);
    public static bool LessEqual(float a,float b)=>GreaterEqual(b,a);
}

// fidelity: M11-068
// A cell models a Radians output pointer, including aliases and its existing rescale flag.
public sealed class RadiansOutput
{
    public Radians Value;
    public RadiansOutput(){Value=new Radians();}
    public RadiansOutput(Radians value){Value=new Radians(value);}
}
