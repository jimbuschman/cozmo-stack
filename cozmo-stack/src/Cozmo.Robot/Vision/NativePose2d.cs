using Point2f=Cozmo.Robot.Manipulation.Point2f;
namespace Cozmo.Robot.Vision;

// fidelity: M11-068
public readonly struct RotationMatrix2d
{
    private readonly float _c,_s;
    public RotationMatrix2d(Radians angle)
    {
        _c=MathF.Cos(angle.Value);_s=MathF.Sin(angle.Value); // EQUIVALENT_IMPLEMENTATION M13-023(e): unshipped cosf/sinf, in native order.
    }
    public Point2f Apply(Point2f p)
    {float x=p.X*_c;x+=p.Y*(-_s);float y=p.X*_s;y+=p.Y*_c;return new(x,y);}
}

// fidelity: M11-068
public struct Transform2d
{
    public Radians Angle;
    public Point2f Translation;
    public Transform2d(Radians angle,Point2f translation){Angle=new(angle);Translation=translation;}
    public void TranslateForward(float distance)
    {
        float x=Translation.X+MathF.Cos(Angle.Value)*distance; // EQUIVALENT_IMPLEMENTATION M13-023(e): cosf.
        Translation=new(x,Translation.Y);
        float y=Translation.Y+MathF.Sin(Angle.Value)*distance; // EQUIVALENT_IMPLEMENTATION M13-023(e): sinf, after the x write.
        Translation=new(Translation.X,y);
    }
    public void MultiplyBy(ref Transform2d other)
    {
        Angle=Angle+other.Angle;
        var p=new RotationMatrix2d(Angle).Apply(other.Translation);
        Translation=new(Translation.X+p.X,Translation.Y+p.Y);
    }
    public Transform2d Compose(Transform2d other)
    {
        var angle=Angle+other.Angle;
        _=new RotationMatrix2d(angle); // Native unused summed-angle trig calls at 0x0084B6FA..0x0084B71A.
        var p=new RotationMatrix2d(Angle).Apply(other.Translation);
        return new(angle,new(Translation.X+p.X,Translation.Y+p.Y));
    }
    public void PreComposeWith(ref Transform2d other)
    {
        Angle=Angle+other.Angle;
        Translation=new RotationMatrix2d(other.Angle).Apply(Translation);
        Translation=new(Translation.X+other.Translation.X,Translation.Y+other.Translation.Y);
    }
    public void Invert()
    {
        Angle=Angle*-1f;Translation=new(-Translation.X,-Translation.Y);
        Translation=new RotationMatrix2d(Angle).Apply(Translation);
    }
    public Transform2d Inverse(){var result=this;result.Invert();return result;}
    public Point2f Apply(Point2f p)
    {var rotated=new RotationMatrix2d(Angle).Apply(p);return new(Translation.X+rotated.X,Translation.Y+rotated.Y);}
}

// fidelity: M11-068, M11-065
// The checked 2d constructor/plane payload only; the wider 2d PoseBase tree API has no row in this job.
public sealed class Pose2d
{
    public Transform2d Transform {get;}
    public Point3f PlaneOrigin {get;}
    public Point3f PlaneNormal {get;}
    public Pose2d():this(new Radians(),new Point2f(0,0)){}
    public Pose2d(Radians angle,Point2f translation)
    {Transform=new(angle,translation);PlaneOrigin=new(0,0,0);PlaneNormal=new(0,0,1);}
    public Pose2d(Transform2d transform)
    {Transform=transform;PlaneOrigin=new(0,0,0);PlaneNormal=new(0,0,0);}
    public Pose2d(Pose3d pose):this(new Radians((float)pose.AngleAroundZ),new Point2f((float)pose.Translation.X,(float)pose.Translation.Y)){}
}
