
namespace Cozmo.Robot.Vision;

// fidelity: M11-065
public readonly record struct Point3f(float X,float Y,float Z)
{
    public Point3f(Vec3 p):this((float)p.X,(float)p.Y,(float)p.Z){}
    public Vec3 ToVec3()=>new(X,Y,Z);
}

// fidelity: M11-065
/// <summary>Engine Rotation3d: double quaternion; constructor copies raw quaternion unless Normalize is called.</summary>
public readonly struct Rotation3d : IEquatable<Rotation3d>
{
    public double W {get;} public double X {get;} public double Y {get;} public double Z {get;}
    public Rotation3d(double w,double x,double y,double z){W=w;X=x;Y=y;Z=z;}
    public bool Equals(Rotation3d b)=>W==b.W&&X==b.X&&Y==b.Y&&Z==b.Z;
    public override bool Equals(object? b)=>b is Rotation3d q&&Equals(q);
    public override int GetHashCode()=>HashCode.Combine(W,X,Y,Z);
    public static bool operator ==(Rotation3d a,Rotation3d b)=>a.Equals(b);
    public static bool operator !=(Rotation3d a,Rotation3d b)=>!a.Equals(b);
    public static readonly Rotation3d Identity=new(1,0,0,0);
    private static readonly double NormalizeTolerance=BitConverter.Int64BitsToDouble(0x3E56A09E19D672B6);
    public Rotation3d(float angle,Vec3 axis):this(FromAngleAxis(angle,axis)){}
    public Rotation3d(Rotation3d q):this(q.W,q.X,q.Y,q.Z){}
    public static Rotation3d FromAngleAxis(float angle,Vec3 axis)
    {
        float half=angle*0.5f;
        float c=MathF.Cos(half),s=MathF.Sin(half); // EQUIVALENT_IMPLEMENTATION M13-023(e): bionic cosf/sinf.
        float x=s*(float)axis.X,y=s*(float)axis.Y,z=s*(float)axis.Z;
        return new Rotation3d(c,x,y,z).Normalized();
    }
    public Rotation3d Normalized()
    {
        double n=W*W;n+=X*X;n+=Y*Y;n+=Z*Z;
        if(Math.Abs(1-n)<NormalizeTolerance){double scale=2/(n+1);return new(scale*W,scale*X,scale*Y,scale*Z);}
        double length=Math.Sqrt(n); // EQUIVALENT_IMPLEMENTATION M13-023(e): M13-023(e) system-library boundary where platform last bits differ.
        return new(W/length,X/length,Y/length,Z/length);
    }
    public static Rotation3d operator *(Rotation3d a,Rotation3d b)
    {
        double w=a.W*b.W;w-=a.X*b.X;w-=a.Y*b.Y;w-=a.Z*b.Z;
        double x=b.W*a.X;x+=a.W*b.X;x+=a.Y*b.Z;x-=b.Y*a.Z;
        double y=a.W*b.Y;y-=a.X*b.Z;y=b.W*a.Y+y;y=b.X*a.Z+y;
        double z=a.X*b.Y;z+=a.W*b.Z;z-=b.X*a.Y;z=b.W*a.Z+z;
        return new Rotation3d(w,x,y,z).Normalized();
    }
    public Rotation3d Inverse()=>new(W,-X,-Y,-Z);
    public bool IsNearlyEqual(Rotation3d b,float tolerance)=>Math.Abs(W-b.W)<tolerance&&Math.Abs(X-b.X)<tolerance&&Math.Abs(Y-b.Y)<tolerance&&Math.Abs(Z-b.Z)<tolerance;
    public Point3f RotatePoint(Point3f p)=>new(Rotate(p.ToVec3()));
    public Vec3 Rotate(Vec3 p)
    {
        float w=(float)W,x=(float)X,y=(float)Y,z=(float)Z,px=(float)p.X,py=(float)p.Y,pz=(float)p.Z;
        float ww=w*w,xx=x*x,yy=y*y,zz=z*z,xy=x*y,wz=w*z,wy=w*y,xz=x*z,yz=y*z,wx=w*x;
        float m00=ww+xx;m00-=yy;m00-=zz;
        float m11=ww-xx;m11+=yy;m11-=zz;
        float m22=ww-xx;m22-=yy;m22+=zz;
        float xy2=xy+xy,wz2=wz+wz,wy2=wy+wy,xz2=xz+xz,yz2=yz+yz,wx2=wx+wx;
        float m01=xy2-wz2,m10=xy2+wz2,m02=wy2+xz2,m20=xz2-wy2,m12=yz2-wx2,m21=wx2+yz2;
        float ox=m00*px;ox+=m01*py;ox=m02*pz+ox;
        float oy=m11*py;oy+=m10*px;oy=m12*pz+oy;
        float oz=m20*px;oz+=m21*py;oz=m22*pz+oz;
        return new(ox,oy,oz);
    }
    public float Angle {get {double a=Math.Acos(W);return Radians.Rescale((float)(a+a));}} // EQUIVALENT_IMPLEMENTATION M13-023(e): bionic acos.
    public Vec3 Axis=>UnitPoint(new((float)X,(float)Y,(float)Z));
    internal static Vec3 UnitPoint(Vec3 p)
    {
        float x=(float)p.X,y=(float)p.Y,z=(float)p.Z,n=x*x;n+=y*y;n+=z*z;
        if(!(n>0))return new(x,y,z);
        float inv=1/MathF.Sqrt(n); // IEEE sqrtf / M13-023(e) system boundary.
        return new(inv*x,inv*y,inv*z);
    }
    public float AngleDiff(Rotation3d b)
    {
        if(W==b.W&&X==b.X&&Y==b.Y&&Z==b.Z)return 0;
        double d=W*b.W;d+=X*b.X;d+=Y*b.Y;d+=Z*b.Z;
        double c=d>-1?d:-1;c=c<1?c:1;double v=c*(c+c);v+=-1;
        return Radians.Rescale((float)Math.Acos(v)); // EQUIVALENT_IMPLEMENTATION M13-023(e): bionic acos.
    }
    public float AngleAroundZ
    {
        get
        {
            double xy=X*Y,zw=Z*W,xx=X*X,yy=Y*Y,zz=Z*Z;
            double t01=xy-zw,t11=xx+zz,t10=xy+zw,t00=yy+zz;
            float m01=(float)(t01+t01),m11=(float)(1-(t11+t11)),m10=(float)(t10+t10),m00=(float)(1-(t00+t00));
            float a=m10*m10;a+=m00*m00;float b=m01*m01;b+=m11*m11;
            return Radians.Rescale(a>b?MathF.Atan2(m10,m00):MathF.Atan2(-m01,m11)); // EQUIVALENT_IMPLEMENTATION M13-023(e): atan2f.
        }
    }
    public float RmatEntry(int row,int column)
    {
        double value=(row,column) switch{
            (0,0)=>((Y*Y+Z*Z)*-2)+1,(1,1)=>((X*X+Z*Z)*-2)+1,(2,2)=>((X*X+Y*Y)*-2)+1,
            (0,1)=>X*Y-Z*W,(1,0)=>X*Y+Z*W,(0,2)=>X*Z+Y*W,(2,0)=>X*Z-Y*W,(1,2)=>Y*Z-X*W,(2,1)=>Y*Z+X*W,_=>throw new ArgumentOutOfRangeException()};
        if(row!=column)value+=value;return (float)value;
    }
    public Mat3 Matrix()
    {
        float xx=(float)(X*X),yy=(float)(Y*Y),zz=(float)(Z*Z),xy=(float)(X*Y),zw=(float)(Z*W),xz=(float)(X*Z),yw=(float)(Y*W),yz=(float)(Y*Z),xw=(float)(X*W);
        float a=yy+zz,b=xx+zz,c=xx+yy,d=xy-zw,e=xz+yw,f=xy+zw,g=yz-xw,h=xz-yw,i=yz+xw;
        float m00=a*-2;m00+=1;b=b+b;c=c+c;
        return new Mat3(m00,d+d,e+e,f+f,1-b,g+g,h+h,i+i,1-c).Orthonormalized();
    }
    public static Rotation3d FromVector(Vec3 vector)=>FromRotationVector(new RotationVector3d(vector));
    public static Rotation3d FromRotationVector(RotationVector3d vector)=>FromAngleAxis(vector.Angle.Value,
        Radians.IsNear(vector.Angle,new Radians(),new Radians(Radians.Epsilon))?new Vec3(0,0,1):vector.Axis);
    public float AngleAroundX
    {
        get{double yz=Y*Z,xw=X*W,a=yz-xw,b=Y*Y+X*X,c=yz+xw,d=Z*Z+X*X;
            return ProjectedAngle((float)(a+a),(float)(1-(b+b)),(float)(c+c),(float)(1-(d+d)),false);}
    }
    public float AngleAroundY
    {
        get{double xz=X*Z,yw=Y*W,a=xz+yw,b=X*X+Y*Y,c=xz-yw,d=Z*Z+Y*Y;
            return ProjectedAngle((float)(a+a),(float)(1-(b+b)),(float)(c+c),(float)(1-(d+d)),true);}
    }
    private static float ProjectedAngle(float a,float b,float c,float d,bool y)
    {
        float first=a*a;first+=b*b;float second=c*c;second+=d*d;
        // EQUIVALENT_IMPLEMENTATION M13-023(e): unshipped atan2f; projections and sign flips are exact.
        return Radians.Rescale(second>first?(y?MathF.Atan2(-c,d):MathF.Atan2(c,d)):(y?MathF.Atan2(a,b):MathF.Atan2(-a,b)));
    }
    public float AngleAroundAxis(int axis)=>axis switch{1=>AngleAroundX,2=>AngleAroundY,3=>AngleAroundZ,
        -3=>Radians.Rescale(-AngleAroundZ),-2=>Radians.Rescale(-AngleAroundY),_=>Radians.Rescale(-AngleAroundX)};

    public static Rotation3d FromMatrix(Mat3 matrix)=>FromVector(matrix.ToRotationVector());
}

// fidelity: M11-065, M11-068
public struct Transform3d
{
    public Rotation3d Rotation {get;private set;}
    private Point3f _translation;
    public Vec3 Translation {get=>_translation.ToVec3();private set=>_translation=new(value);}
    public Transform3d(Rotation3d rotation,Vec3 translation){Rotation=rotation;_translation=new(translation);}
    internal static Vec3 Narrow(Vec3 p)=>new((float)p.X,(float)p.Y,(float)p.Z);
    internal static Vec3 Add(Vec3 a,Vec3 b)=>new((float)a.X+(float)b.X,(float)a.Y+(float)b.Y,(float)a.Z+(float)b.Z);
    internal static Vec3 Negate(Vec3 a)=>new(-(float)a.X,-(float)a.Y,-(float)a.Z);
    public Vec3 Apply(Vec3 p)=>Add(Translation,Rotation.Rotate(p));
    public Transform3d Compose(Transform3d b){var t=Add(Translation,Rotation.Rotate(b.Translation));return new(Rotation*b.Rotation,t);}
    public void MultiplyBy(in Transform3d b){Translation=Add(Translation,Rotation.Rotate(b.Translation));Rotation=Rotation*b.Rotation;}
    public void PreComposeWith(ref Transform3d p)
    {
        Rotation=p.Rotation*Rotation;
        Translation=p.Rotation.Rotate(Translation);
        Translation=Add(p.Translation,Translation); // R26: aliased P sees the just-rotated t; doubles it.
    }
    public Transform3d Inverse(){var r=Rotation.Inverse();return new(r,r.Rotate(Negate(Translation)));}
    public Transform3d RotateBy(Rotation3d r){var t=r.Rotate(Translation);return new(r*Rotation,t);}
    public Transform3d RotateBy(Radians a)=>RotateBy(Rotation3d.FromAngleAxis(a.Value,Rotation.Axis));
    public Transform3d TranslateForward(float distance)=>new(Rotation,Add(Rotation.Rotate(new(distance,0,0)),Translation));
    public void ApplyTo(IReadOnlyList<Vec3> input,List<Vec3> output)
    {
        int n=input.Count;
        if(n==output.Count){for(int i=0;i<n;i++){var p=Apply(input[i]);output[i]=p;}}
        else{output.Clear();for(int i=0;i<n;i++)output.Add(Apply(input[i]));}
    }
}

// fidelity: M11-065
public readonly struct RotationVector3d
{
    public Radians Angle {get;} public Vec3 Axis {get;}
    internal static (Vec3 Axis,float Length) MakeUnit(Vec3 p)
    {
        float x=(float)p.X,y=(float)p.Y,z=(float)p.Z,n=x*x;n+=y*y;n+=z*z;
        if(!(n>0))return(new(x,y,z),0);
        float length=MathF.Sqrt(n); // IEEE sqrtf; NaN system-call fallback is M13-023(e).
        float k=1/length;return(new(k*x,k*y,k*z),length);
    }
    public RotationVector3d(Vec3 vector)
    {
        var unit=MakeUnit(vector);Angle=new Radians(unit.Length);
        Axis=Radians.IsNear(Angle,new Radians(),new Radians(Radians.Epsilon))?new Vec3(1,0,0):unit.Axis;
    }
    public RotationVector3d(Radians angle,Vec3 axis)
    {
        Angle=new Radians(angle);var unit=MakeUnit(axis);Axis=unit.Axis;
        if(unit.Length==0&&Radians.IsNear(angle,new Radians(),new Radians(Radians.Epsilon))){Axis=new(1,0,0);return;}
        if(!(MathF.Abs(unit.Length-1)<Radians.Epsilon))
            PoseDiagnostics.Warning("RotationVector3d.Constructor.AxisNotUnitLength","Provided axis was not unit length (norm="+PoseDiagnostics.Fixed(unit.Length)+").");
    }
    internal Vec3 MatrixVector()
    {
        var axis=Radians.IsNear(Angle,new Radians(),new Radians(Radians.Epsilon))?new Vec3(0,0,1):Axis;float a=Angle.Value;
        return new(a*(float)axis.X,a*(float)axis.Y,a*(float)axis.Z);
    }
}

// fidelity: M11-065
public readonly record struct QuaternionF(float W,float X,float Y,float Z)
{
    public QuaternionF Normalized()
    {
        float n=W*W;n+=X*X;n+=Y*Y;n+=Z*Z;
        double d=n;
        if(Math.Abs(1-d)<BitConverter.Int64BitsToDouble(0x3E56A09E19D672B6))
        {float k=(float)(2/(d+1));return new(k*W,k*X,k*Y,k*Z);}
        float length=MathF.Sqrt(n); // IEEE sqrtf / M13-023(e) NaN fallback boundary.
        return new(W/length,X/length,Y/length,Z/length);
    }
    public static QuaternionF operator *(QuaternionF a,QuaternionF b)
    {
        float w=a.W*b.W;w-=a.X*b.X;w-=a.Y*b.Y;w-=a.Z*b.Z;
        float x=b.W*a.X;x+=a.W*b.X;x+=a.Y*b.Z;x-=b.Y*a.Z;
        float y=a.W*b.Y;y-=a.X*b.Z;y=b.W*a.Y+y;y=b.X*a.Z+y;
        float z=a.X*b.Y;z+=a.W*b.Z;z-=b.X*a.Y;z=b.W*a.Z+z;
        return new QuaternionF(w,x,y,z).Normalized();
    }
}
