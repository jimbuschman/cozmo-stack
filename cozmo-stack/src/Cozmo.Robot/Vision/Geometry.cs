using Cozmo.Transport;
namespace Cozmo.Robot.Vision;

/// <summary>A 3-vector in double precision. Millimetres for positions.</summary>
public readonly record struct Vec3(double X, double Y, double Z)
{
    public static readonly Vec3 Zero = new(0, 0, 0);
    public static Vec3 operator +(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vec3 operator -(Vec3 a) => new(-a.X, -a.Y, -a.Z);
    public static Vec3 operator *(Vec3 a, double s) => new(a.X * s, a.Y * s, a.Z * s);
    public static Vec3 operator *(double s, Vec3 a) => a * s;
    public static Vec3 operator /(Vec3 a, double s) => new(a.X / s, a.Y / s, a.Z / s);
    public double Dot(Vec3 b) => X * b.X + Y * b.Y + Z * b.Z;
    public Vec3 Cross(Vec3 b) => new(Y * b.Z - Z * b.Y, Z * b.X - X * b.Z, X * b.Y - Y * b.X);
    public double Length => Math.Sqrt(Dot(this));
    public Vec3 Normalized() { var l = Length; return l > 0 ? this / l : this; }
    public override string ToString() => $"({X:F2}, {Y:F2}, {Z:F2})";
}

/// <summary>A 2-vector: image coordinates in pixels, or a point on a plane.</summary>
public readonly record struct Vec2(double X, double Y)
{
    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec2 operator *(Vec2 a, double s) => new(a.X * s, a.Y * s);
    public double Length => Math.Sqrt(X * X + Y * Y);
    public double LengthSq => X * X + Y * Y;
    public override string ToString() => $"({X:F1}, {Y:F1})";
}

// fidelity: M11-065, M11-067, M11-068
/// <summary>Engine RotationMatrix3d: row-major binary32. Quaternion-backed poses rotate points directly.</summary>
public readonly struct Mat3
{
    private readonly float[] _m;
    public Mat3(double a,double b,double c,double d,double e,double f,double g,double h,double i)
        =>_m=new[]{(float)a,(float)b,(float)c,(float)d,(float)e,(float)f,(float)g,(float)h,(float)i};
    public Mat3(ReadOnlySpan<double> values)
    {if(values.Length!=9)throw new ArgumentException("9 values",nameof(values));_m=new float[9];for(int i=0;i<9;i++)_m[i]=(float)values[i];}
    // R-POSE-REST section3 / RotationMatrix3d initializer_list: copy nine, then Renormalize.
    // Plain constructors remain raw matrix carriers for helper fixtures and CV boundaries.
    internal static Mat3 FromInitializerRows(ReadOnlySpan<double> values)
    {
        if(values.Length<9){PoseDiagnostics.Missing("M1-034/M11-068 short matrix initializer would read beyond the list at 0x00849900..0x00849936; initializer skipped");throw new ArgumentException("Nine source words required",nameof(values));}
        return new Mat3(values[..9]).Orthonormalized();
    }
    public static readonly Mat3 Identity=new(1,0,0,0,1,0,0,0,1);
    public double this[int row,int col]=>_m[row*3+col];
    public Vec3 Row(int r)=>new(_m[r*3],_m[r*3+1],_m[r*3+2]);
    public Vec3 Column(int c)=>new(_m[c],_m[c+3],_m[c+6]);
    // Generic matrix/Point3d API is retained as a candidate; engine pose rotation is Rotation3d.Rotate (R7/R8).
    public static Vec3 operator *(Mat3 m,Vec3 v)=>new(m._m[0]*v.X+m._m[1]*v.Y+m._m[2]*v.Z,
        m._m[3]*v.X+m._m[4]*v.Y+m._m[5]*v.Z,m._m[6]*v.X+m._m[7]*v.Y+m._m[8]*v.Z);
    public static Mat3 operator *(Mat3 a,Mat3 b)
    {
        var result=new double[9];
        for(int i=0;i<3;i++)for(int j=0;j<3;j++)
        {float v=0;v+=a._m[i*3]*b._m[j];v+=a._m[i*3+1]*b._m[3+j];v+=a._m[i*3+2]*b._m[6+j];result[i*3+j]=v;}
        return new Mat3(result).Orthonormalized();
    }
    public Mat3 Transposed()=>new(_m[0],_m[3],_m[6],_m[1],_m[4],_m[7],_m[2],_m[5],_m[8]);
    public static Mat3 AxisAngle(Vec3 axis,double angle)=>NativeRodrigues.VectorToMatrix(new RotationVector3d(new Radians((float)angle),axis).MatrixVector());
    public static Mat3 AboutX(double angle)=>AxisAngle(new(1,0,0),angle);
    public static Mat3 AboutY(double angle)=>AxisAngle(new(0,1,0),angle);
    public static Mat3 AboutZ(double angle)=>AxisAngle(new(0,0,1),angle);
    public static Mat3 FromRotationVector(Vec3 vector)=>NativeRodrigues.VectorToMatrix(new RotationVector3d(vector).MatrixVector());
    public Vec3 ToRotationVector()=>NativeRodrigues.MatrixToVector(this);
    private float Projected(float a,float b,float c,float d,bool negateFirst,bool negateSecond)
    {
        float n1=a*a;n1+=b*b;float n2=c*c;n2+=d*d;
        // EQUIVALENT_IMPLEMENTATION M13-023(e): unshipped atan2f; R42/X/Y widths and branches remain exact.
        float value=n1>n2?MathF.Atan2(negateFirst?-a:a,b):MathF.Atan2(negateSecond?-c:c,d);
        return Radians.Rescale(value);
    }
    public double AngleAroundZ=>Projected(_m[3],_m[0],_m[1],_m[4],false,true);
    public float AngleAroundX=>Projected(_m[7],_m[4],_m[5],_m[8],false,true);
    public float AngleAroundY=>Projected(_m[6],_m[0],_m[2],_m[8],true,false);
    public float Angle
    {
        get{float v=_m[0]+_m[4];v+=_m[8];v+=-1;v*=0.5f;
            return Radians.Rescale(MathF.Acos(v));} // EQUIVALENT_IMPLEMENTATION M13-023(e): unshipped acosf, no clamp.
    }
    public double AngularDistance(Mat3 other)=>(this*other.Transposed()).Angle;
    public Mat3 Abs(){var values=new double[9];for(int i=0;i<9;i++)values[i]=MathF.Abs(_m[i]);return new(values);}
    public Mat3 Orthonormalized()
    {
        for(int i=0;i<3;i++)
        {
            float v=_m[i*3]*_m[i*3];v+=_m[i*3+1]*_m[i*3+1];v+=_m[i*3+2]*_m[i*3+2];
            float norm=MathF.Sqrt(v); // IEEE vsqrt / unshipped NaN sqrtf fallback M13-023(e).
            float error=norm-1;
            if(!(MathF.Abs(error)<BitConverter.Int32BitsToSingle(0x3C23D70A)))
            {
                string row="MISSING: M11-083 shipped std::to_string(float) %f row elements (0x008495CE)";
                PoseDiagnostics.Warning("RotationMatrixBase.Renormalize.BadRowNorm","Norm of row "+i+" = "+PoseDiagnostics.Fixed(norm)+"! (Expecting near 1.0) Row = ["+row+"]");
                return NativeRodrigues.VectorToMatrix(NativeRodrigues.MatrixToVector(this));
            }
            if(norm<BitConverter.Int32BitsToSingle(0x3F7FFFEF)||norm>1)
                return NativeRodrigues.VectorToMatrix(NativeRodrigues.MatrixToVector(this));
        }
        return this;
    }
    public static Mat3 FromEuler(Radians x,Radians y,Radians z)
    {
        // EQUIVALENT_IMPLEMENTATION M13-023(e): unshipped cosf/sinf; native call order y,x,z.
        float cb=MathF.Cos(y.Value),sb=MathF.Sin(y.Value),ca=MathF.Cos(x.Value),sa=MathF.Sin(x.Value),cc=MathF.Cos(z.Value),sc=MathF.Sin(z.Value);
        float u=sb*ca,v=sb*sa;
        float m01=v*cc;m01-=ca*sc;float m02=u*cc;m02+=sa*sc;
        float m11=ca*cc;m11+=v*sc;float m12=u*sc;m12-=sa*cc;
        return new(cb*cc,m01,m02,cb*sc,m11,m12,-sb,cb*sa,cb*ca);
    }
    public bool GetEulerAngles(ref Radians x,ref Radians y,ref Radians z)
    {
        float m20=_m[6];
        if(MathF.Abs(MathF.Abs(m20)-1)<Radians.Epsilon)
        {
            z.Assign(0);y.Assign(m20>0?-Radians.Pi*0.5f:Radians.Pi*0.5f);
            x.Assign(m20>0?MathF.Atan2(-_m[1],-_m[2]):MathF.Atan2(_m[1],_m[2])); // M13-023(e) atan2f.
            return true;
        }
        y.Assign(-MathF.Asin(m20)); // M13-023(e) asinf; no clamp. Destination's existing flag is retained.
        float k=1/MathF.Cos(y.Value); // M13-023(e) cosf.
        x.Assign(MathF.Atan2(_m[7]*k,_m[8]*k));z.Assign(MathF.Atan2(_m[3]*k,_m[0]*k)); // M13-023(e) atan2f.
        return false;
    }
    public bool GetEulerAngles(RadiansOutput x,RadiansOutput y,RadiansOutput z,RadiansOutput? x2=null,RadiansOutput? y2=null,RadiansOutput? z2=null)
    {
        bool singular=GetEulerAngles(ref x.Value,ref y.Value,ref z.Value);
        if(singular)
        {
            if(x2 is not null)x2.Value.Assign(x.Value);
            if(y2 is not null)y2.Value.Assign(y.Value);
            if(z2 is not null)z2.Value.Assign(z.Value);
        }
        else if(x2 is not null)
        {
            if(y2 is null||z2 is null)
            {PoseDiagnostics.Missing("M1-034/M11-068 GetEulerAngles alternate-x without alternate-y/z would dereference null at 0x0084B23C..0x0084B310; alternate solution skipped, previous outputs preserved");return false;}
            y2.Value.Assign(Radians.Pi-y.Value); // Float-minus-Radians constructs a rescaled flag-set result.
            float k=1/MathF.Cos(y2.Value.Value); // EQUIVALENT_IMPLEMENTATION M13-023(e): unshipped cosf.
            x2.Value.Assign(MathF.Atan2(_m[7]*k,_m[8]*k)); // EQUIVALENT_IMPLEMENTATION M13-023(e): atan2f.
            z2.Value.Assign(MathF.Atan2(_m[3]*k,_m[0]*k)); // EQUIVALENT_IMPLEMENTATION M13-023(e): atan2f.
        }
        return singular;
    }
    public override string ToString()=>"["+Row(0)+"; "+Row(1)+"; "+Row(2)+"]";
}

// fidelity: M11-065, M11-066, M11-067, M11-068, M11-004, M11-077
internal sealed class PoseTreeNode
{
    // B-M11-8 verifier round 2 (reader-side race): the transform is published as an immutable box with Volatile.Write and
    // read once per access with Volatile.Read, so a reader on another thread (the vision worker flattening a carried pose)
    // sees either the old or the new Transform3d, never a torn mix of their fields. Values are copied unchanged (bit-identical).
    // Stack plumbing; the native reads and writes these on one thread.
    private sealed class TransformBox(Transform3d value){internal readonly Transform3d Value=value;}
    private TransformBox _transformBox=new(default);
    internal Transform3d Transform{get=>Volatile.Read(ref _transformBox).Value;set=>Volatile.Write(ref _transformBox,new TransformBox(value));}
    internal PoseTreeNode? Parent;
    internal string Name;
    internal uint Id,Owners;
    internal PoseTreeNode(Transform3d transform,string name,PoseTreeNode? parent)
    {Transform=transform;Name=name;SetParent(parent);}
    internal void SetParent(PoseTreeNode? parent)
    {
        Parent=parent;
    }
    // Checked 0x008460C8..0x00846124: the diagnostic returns false but every listed caller continues.
    internal void AssertParent()
    {
        if(Parent is not {} parent||Pose3d.UnownedParentsAllowed||parent.Owners!=0)return;
        PoseDiagnostics.Verify("PoseBase.Dev_AssertIsValidParentPointer.UnownedParent",
            "VERIFY(parent->_ownerCount > 0): Pose "+PoseDiagnostics.Integer(Id)+"("+PoseDiagnostics.CString(Name)+") has parent "+PoseDiagnostics.Integer(parent.Id)+"("+PoseDiagnostics.CString(parent.Name)+") which is not owned by any PoseBase wrapper");
    }
    internal void RemoveOwner()
    {
        if(Owners==0){PoseDiagnostics.Verify("PoseBase.PoseTreeNode.RemoveOwner.ZeroOwners","VERIFY(_ownerCount > 0): ");return;}
        if(--Owners!=0||Pose3d.UnownedParentsAllowed||Id==0)return;
        PoseDiagnostics.Verify("PoseBase.PoseTreeNode.RemoveOwner.ZeroOwnersWithID",
            "VERIFY(GetID() == 0): Pose "+PoseDiagnostics.CString(Name)+" has non-zero ID="+PoseDiagnostics.Integer(Id)+" but just had its last owner removed");
    }
    internal bool HasSameParent(PoseTreeNode other)
    {
        if(ReferenceEquals(this,other))return true;
        AssertParent();if(Parent is null)return false;
        other.AssertParent();return ReferenceEquals(Parent,other.Parent);
    }
    internal static bool HasSameRoot(PoseTreeNode? a,PoseTreeNode? b)
    {
        if(a is null||b is null)return false;
        static PoseTreeNode Walk(PoseTreeNode n,int line)
        {
            int count=0;
            while(true)
            {n.AssertParent();if(n.Parent is null)break;n=n.Parent;if(++count==1000){PoseDiagnostics.Loop("poseTreeNode.h",line);break;}}
            return n;
        }
        return ReferenceEquals(Walk(a,0x107),Walk(b,0x10E));
    }
    internal int Depth()
    {
        int depth=1;var n=this;
        while(n.Parent is not null)
        {n=n.Parent;if(++depth==1001){PoseDiagnostics.Loop("poseTreeNode.h",0x189);return 1001;}}
        return depth;
    }
    internal PoseTreeNode Root()
    {
        var n=Parent??this;int count=0;
        while(true)
        {n.AssertParent();if(n.Parent is null)break;n.AssertParent();n=n.Parent;if(++count==1000){PoseDiagnostics.Loop("poseTreeNode.h",0x145);break;}}
        return n;
    }
}

/// <summary>Native Pose3d wrapper: a shared node containing a double quaternion and binary32 translation.</summary>
public sealed class Pose3d : IDisposable
{
    internal PoseTreeNode? Node;
    private readonly bool _legacyFrameInput;
    internal static bool UnownedParentsAllowed;
    public static Pose3d Identity=>new(Mat3.Identity,Vec3.Zero);
    public Pose3d Value=>this; // Existing nullable-value API unwrap; no new node is made.
    public Mat3 Rotation=>Quaternion.Matrix();
    public Rotation3d Quaternion=>RequireNode().Transform.Rotation;
    public Vec3 Translation=>RequireNode().Transform.Translation;
    public Transform3d Transform=>RequireNode().Transform;
    public string Name {get=>RequireNode().Name;set=>RequireNode().Name=value;}
    public uint Id {get=>RequireNode().Id;set=>RequireNode().Id=value;}
    public uint Owners=>RequireNode().Owners;
    public bool IsRoot{get{Node?.AssertParent();return Node is null||Node.Parent is null;}}
    public bool HasParent{get{Node?.AssertParent();return Node?.Parent is not null;}}
    public static IDisposable UseSourceLog(SourceLogSink source,Action? forceFlush=null)=>PoseDiagnostics.Use(source,forceFlush);
    public Pose3d(string name="",Pose3d? parent=null):this(new Transform3d(Rotation3d.Identity,Vec3.Zero),name,parent){}
    public Pose3d(Transform3d transform,string name="",Pose3d? parent=null)
    {Node=new(transform,name,parent?.Node);Node.Owners=1;}
    public Pose3d(Rotation3d rotation,Vec3 translation,string name="",Pose3d? parent=null):this(new Transform3d(rotation,translation),name,parent){}
    // API migration for a retained rotation: R23/R30 copy the quaternion, not a matrix round trip.
    public Pose3d(Pose3d rotationFrom,Vec3 translation):this(rotationFrom.Quaternion,translation)
    {_legacyFrameInput=rotationFrom._legacyFrameInput;}
    public Pose3d(Mat3 rotation,Vec3 translation):this(Rotation3d.FromMatrix(rotation),translation){_legacyFrameInput=true;}
    public Pose3d(double angleRad,Vec3 axis,Vec3 translation):this(Rotation3d.FromAngleAxis(new Radians((float)angleRad).Value,axis),translation){_legacyFrameInput=true;}
    public Pose3d(Pose2d pose):this(Rotation3d.FromAngleAxis(pose.Transform.Angle.Value,new Vec3(0,0,1)),new Vec3(pose.Transform.Translation.X,pose.Transform.Translation.Y,0))
    {
        var n=pose.PlaneNormal;float dot=n.X*0f;dot+=n.Y*0f;dot+=n.Z*1f;
        var angle=new Radians(MathF.Acos(dot)); // EQUIVALENT_IMPLEMENTATION M13-023(e): unshipped acosf.
        if(MathF.Abs(angle.Value)<Radians.Epsilon)return;
        var axis=new Vec3(n.Z*0f-n.Y,n.X-n.Z*0f,n.Y*0f-n.X*0f);
        var plane=new Transform3d(Rotation3d.FromAngleAxis(angle.Value,axis),pose.PlaneOrigin.ToVec3());
        {var t=Node!.Transform;t.PreComposeWith(ref plane);Node.Transform=t;} // one read, one publish (B-M11-8)
    }
    public Pose3d(Pose3d source)
    {
        var other=source.RequireNode();Node=new(other.Transform,CopyName(other.Name),other.Parent);Node.Owners=1;
        _legacyFrameInput=source._legacyFrameInput;
    }
    private Pose3d(PoseTreeNode node){Node=node;node.Owners++;}
    internal static Pose3d WrapExisting(PoseTreeNode node)=>new(node);
    private static string CopyName(string name)=>name.Length==0?name:name+"_COPY";
    private PoseTreeNode RequireNode()=>Node??throw new InvalidOperationException("MISSING: M1-034/M11-067 null pose node operation skipped");
    public Pose3d DeepCopy()=>new(this);
    public void Assign(Pose3d source)
    {
        if(ReferenceEquals(this,source))return;
        if(source.Node is null)
        {
            var n=RequireNode();
            n.RemoveOwner();
            if(n.Owners==0&&!UnownedParentsAllowed)
                PoseDiagnostics.Missing("M11-067 assignment-null NotLastOwner VERIFY 0x0084594A..0x008459B8 requires native shared_ptr use_count/diagnostic ownership, not inferred from C# references");
            Node=null;return;
        }
        var other=source.Node;var node=RequireNode();
        node.SetParent(other.Parent);node.Transform=other.Transform;node.Name=CopyName(other.Name);
    }
    public void SetParent(Pose3d parent)
    {
        var n=RequireNode();
        if(parent.Node is null)
        {PoseDiagnostics.Missing("M1-034/M11-068 SetParent null node fatal read 0x00844A48: skipped, previous parent preserved");return;}
        if(!UnownedParentsAllowed&&parent.Node.Owners==0)
            PoseDiagnostics.Verify("PoseBase.SetParent.UnownedParent","VERIFY(otherPose.IsOwned()): Setting parent of "+PoseDiagnostics.Integer(n.Id)+"("+PoseDiagnostics.CString(n.Name)+") to unowned pose "+PoseDiagnostics.Integer(parent.Node.Id)+"("+PoseDiagnostics.CString(parent.Node.Name)+")");
        n.SetParent(parent.Node);
    }
    public void ClearParent()=>RequireNode().SetParent(null);
    // fidelity: M11-076
    public bool TryGetParent(ref Pose3d output)
    {
        if(Node is null){output=new Pose3d();return true;} // Defined null-source case leaves a fresh identity result.
        Node.AssertParent();
        if(Node.Parent is null)
        {PoseDiagnostics.Missing("M1-034/M11-076 GetParent(root) fatal owner read 0x008452BA: skipped, previous output preserved");return false;}
        output=WrapExisting(Node.Parent);return true;
    }
    public Pose3d FindRoot(){var n=RequireNode();n.AssertParent();return WrapExisting(n.Parent is null?n:n.Root());}
    public Vec3 Apply(Vec3 point)=>Transform.Apply(point);
    public void ApplyTo(IReadOnlyList<Vec3> input,List<Vec3> output)=>Transform.ApplyTo(input,output);
    public Pose3d Compose(Pose3d other)
    {var p=new Pose3d(Transform.Compose(other.Transform));return _legacyFrameInput&&other._legacyFrameInput?Legacy(p.Transform):p;}
    internal static Pose3d FromFlatRotation(Rotation3d q,Vec3 t)=>new(q,t,true); // Existing flat caller inputs still lack their checked parent binding.
    private static Pose3d Legacy(Transform3d t)=>new(t.Rotation,t.Translation,true);
    private Pose3d(Rotation3d q,Vec3 t,bool legacy):this(q,t){_legacyFrameInput=legacy;}
    public void MultiplyBy(Pose3d other){var n=RequireNode();var t=n.Transform;var b=other.RequireNode().Transform;t.MultiplyBy(in b);n.Transform=t;} // MultiplyBy reads b before writing this, so the copy matches an aliased call
    public void PreComposeWith(Pose3d other)
    {
        var n=RequireNode();var o=other.RequireNode();var t=n.Transform;
        if(ReferenceEquals(n,o))t.PreComposeWith(ref t); // R26: the aliased P sees the just-rotated t, as before
        else{var p=o.Transform;t.PreComposeWith(ref p);}
        n.Transform=t;
    }
    public void Invert(){var n=RequireNode();n.Transform=n.Transform.Inverse();n.SetParent(null);}
    public Pose3d Inverse(){var result=new Pose3d(this);result.Invert();return result;}
    public Pose3d RotateBy(Rotation3d rotation){var result=new Pose3d(this);result.Node!.Transform=result.Transform.RotateBy(rotation);return result;}
    // R28 RotateBy(RotationVector3d) 0x0084BB3A on a copy of this pose: r from R22, then t = r*t (R7), R = r*R normalised.
    public Pose3d RotateBy(RotationVector3d rotation){var result=new Pose3d(this);result.Node!.Transform=result.Transform.RotateBy(rotation);return result;}
    public double AngleAroundZ=>Quaternion.AngleAroundZ;
    public static bool GetWithRespectTo(Pose3d from,Pose3d to,Pose3d output)
    {
        if(from.Node is null||to.Node is null||output.Node is null)
        {
            PoseDiagnostics.Verify("PoseBase.GetWithRespectTo.NullInputPose","VERIFY(!fromPose.IsNull() && !toPose.IsNull() && !P_wrt_other.IsNull()): FromNull:"+(from.Node is null?"Y":"N")+" ToNull:"+(to.Node is null?"Y":"N")+" WrtOtherNull:"+(output.Node is null?"Y":"N"));return false;
        }
        if(ReferenceEquals(from,to))
        {PoseDiagnostics.Warning("PoseBase.GetWithRespectTo.FromEqualsTo","Pose w.r.t. itself requested.");output.Assign(from);output.SetParent(from);return true;}
        if(!PoseTreeNode.HasSameRoot(from.Node,to.Node))return false;
        var tf=from.Node.Transform;var tt=to.Node.Transform;var f=from.Node;var t=to.Node;
        int d=f.Depth()-t.Depth(),i=0;
        while(d>=1)
        {
            {var p=f.Parent!.Transform;tf.PreComposeWith(ref p);}f.AssertParent();f=f.Parent;
            f.AssertParent();
            if(ReferenceEquals(f.Parent,t)){output.Node.Transform=tf;output.SetParent(to);return true;}
            i++;d--;if(i==1000){PoseDiagnostics.Loop("poseBase_impl.h",0x1A0);break;}
        }
        i=0;
        while(d+i<=-1)
        {
            {var p=t.Parent!.Transform;tt.PreComposeWith(ref p);}t.AssertParent();t=t.Parent;
            t.AssertParent();
            if(ReferenceEquals(t.Parent,f)){output.Node.Transform=tt.Inverse();output.SetParent(to);return true;}
            if(++i==1000){PoseDiagnostics.Loop("poseBase_impl.h",0x1B6);break;}
        }
        i=0;
        while(!ReferenceEquals(f,t)&&!t.HasSameParent(f))
        {
            {var pf=f.Parent!.Transform;tf.PreComposeWith(ref pf);var pt=t.Parent!.Transform;tt.PreComposeWith(ref pt);}t.AssertParent();t=t.Parent;f.AssertParent();f=f.Parent;
            if(++i==1000){PoseDiagnostics.Loop("poseBase_impl.h",0x1D4);break;}
        }
        {var r=tt.Inverse();r.MultiplyBy(in tf);output.Node.Transform=r;}output.SetParent(to);return true;
    }
    public Pose3d GetWithRespectToRoot()
    {
        var output=new Pose3d("");
        if(IsRoot)output.Assign(this);
        else {using var root=FindRoot();if(!GetWithRespectTo(this,root,output))PoseDiagnostics.Error("Pose3d.GetWithRespectToRoot.Failed","Could not get pose w.r.t. its own root. This should never happen.");}
        return output;
    }
    public bool TryWithRespectTo(Pose3d frame,Pose3d output)=>GetWithRespectTo(this,frame,output);
    public Pose3d WithRespectTo(Pose3d frame)
    {
        var output=new Pose3d("");if(GetWithRespectTo(this,frame,output))return output;
        if(_legacyFrameInput&&frame._legacyFrameInput&&!HasParent&&!frame.HasParent)
        {
            // Existing host API supplies flattened values, not native frame nodes. This old path is still a gap.
            PoseDiagnostics.Missing("M11-066 legacy flattened WithRespectTo inputs have no checked root-node binding; existing flat reduction remains an unverified caller candidate");
            return Legacy(frame.Transform.Inverse().Compose(Transform));
        }
        throw new InvalidOperationException("MISSING: M11-066 caller has not handled unrelated pose roots");
    }
    public bool IsSameAs(Pose3d other,double distance,double angle)=>IsSameAs(other,new Vec3(distance,distance,distance),angle,out _);
    public bool IsSameAs(Pose3d other,Vec3 distance,double angle,out Vec3 delta)
    {delta=Vec3.Zero;var radians=new Radians();return IsSameAsWithAmbiguity(other,distance,new Radians((float)angle),ref delta,ref radians,Array.Empty<Mat3>(),false);}
    public bool IsSameAsWithAmbiguity(Pose3d other,Vec3 distance,Radians tolerance,ref Vec3 delta,ref Radians angle,IReadOnlyList<Mat3> ambiguities,bool absolute)
    {
        if(ReferenceEquals(this,other))return true;
        // R38 / 0x00846F7E: a null node takes the cbz to GetWithRespectTo(P, this), whose NullInputPose VERIFY returns false;
        // then PosesHaveDifferentOrigins and false. No throw.
        var n=Node;using var relative=new Pose3d("");n?.AssertParent();
        if(n?.Parent is not null&&ReferenceEquals(n.Parent,other.Node))relative.SetParent(other);
        else if(n?.Parent is not null)
        {n.AssertParent();Pose3d parent=null!;if(!TryGetParent(ref parent))return false;using(parent){if(!GetWithRespectTo(other,parent,relative))return false;}}
        else if(n is not null&&_legacyFrameInput&&other._legacyFrameInput&&!other.HasParent)
        {
            PoseDiagnostics.Missing("M11-065/M11-066 legacy flattened IsSameAs inputs lack checked parent/root bindings; arithmetic only is built");
            relative.Node!.Transform=other.Transform;
        }
        else if(!GetWithRespectTo(other,this,relative))
        {PoseDiagnostics.Warning("Pose3d.IsSameAs.PosesHaveDifferentOrigins","Could not get other pose w.r.t. this pose. Returning not same.");return false;}
        float x=(float)relative.Translation.X-(float)Translation.X,y=(float)relative.Translation.Y-(float)Translation.Y,z=(float)relative.Translation.Z-(float)Translation.Z;
        delta=new(x,y,z);float tx=(float)distance.X,ty=(float)distance.Y,tz=(float)distance.Z;
        if(MathF.Abs(tx-ty)<Radians.Epsilon&&MathF.Abs(tx-tz)<Radians.Epsilon)
        {float sum=x*x;sum+=y*y;sum+=z*z;if(sum>tx*tx)return false;}
        else
        {var local=Quaternion.Inverse().Rotate(delta);if(MathF.Abs((float)local.X)>tx||MathF.Abs((float)local.Y)>ty||MathF.Abs((float)local.Z)>tz)return false;}
        if(tolerance>=new Radians(Radians.Pi))return true;
        angle.Assign(new Radians(Quaternion.AngleDiff(relative.Quaternion)));
        if(angle<=tolerance)return true;
        if(ambiguities.Count==0)return false;
        var matrix=(Quaternion.Inverse()*relative.Quaternion).Matrix();if(absolute)matrix=matrix.Abs();
        foreach(var a in ambiguities)if(new Radians((float)matrix.AngularDistance(a))<tolerance)return true;
        return false;
    }
    public void Dispose()
    {
        if(Node is not {} n)return;
        PoseDiagnostics.Missing("M11-077 pose destruction/owner-release side effects are pending their checked owning job; explicit wrapper release only");
        n.RemoveOwner();Node=null;
    }
    public override string ToString()=>"T="+Translation+" yaw="+(AngleAroundZ*180/Math.PI).ToString("F1")+"deg";
}
