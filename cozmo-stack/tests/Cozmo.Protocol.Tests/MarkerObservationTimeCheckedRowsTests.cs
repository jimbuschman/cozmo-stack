using System.Reflection;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;
namespace Cozmo.Protocol.Tests;
[Collection("ObjectID process statics")]
public sealed class MarkerObservationTimeCheckedRowsTests
{
    private const MarkerType Code=MarkerType.LightCubeI_Front;
    private static float Tol=>BitConverter.UInt32BitsToSingle(0x3DCCCCCD);
    private sealed record Log(EngineLogLevel Level,string? Channel,string Key,string Text);
    private sealed class Inputs:IDisposable
    {
        internal readonly Pose3d CameraPose=new();
        internal readonly List<Pose3d> Poses=new();
        internal readonly CameraModel Camera;
        internal readonly List<Log> Logs=new();
        internal Inputs(double[]? coefficients=null)=>Camera=new(new CameraCalibration{FocalLengthX=128,FocalLengthY=128,CenterX=0,CenterY=0,Rows=240,Columns=320,DistortionCoefficients=coefficients??Array.Empty<double>()},CameraPose);
        internal KnownMarker Marker(float x=0,float z=128,double width=10,double height=10,Pose3d? parent=null)
        {
            // Exact raw quaternion (.5,.5,.5,.5) maps marker X/Z to camera Y/X, no trig fixture dependency.
            var pose=new Pose3d(new Rotation3d(.5,.5,.5,.5),new Vec3(x,0,z),parent:parent??CameraPose);Poses.Add(pose);
            return new(Code,BlockFace.Front,pose,width){HeightMm=height};
        }
        internal ObservableObject Object(params KnownMarker[] markers)=>new(91,ObjectType.Block_LIGHTCUBE1,markers);
        internal void Mark(ObservableObject obj,ObservedMarker? observed=null,uint timestamp=73,float? tolerance=null,bool names=true)
            =>obj.SetMarkerAsObserved(observed??Seen(),timestamp,tolerance??Tol,Camera,(l,c,k,t)=>Logs.Add(new(l,c,k,t)),names?_=>"provided-code-name":null);
        public void Dispose(){foreach(var p in Poses)p.Dispose();CameraPose.Dispose();}
    }
    private static ObservedMarker Seen(float center=0,float size=10,MarkerType code=Code)
    {float lo=center-size/2,hi=center+size/2;return new(code,new[]{new Vec2(lo,-size/2),new Vec2(lo,size/2),new Vec2(hi,-size/2),new Vec2(hi,size/2)},19,0,0);}
    [Fact] public void NearestMarkerWinsAndFirstKeepsExactTie()
    {
        using var i=new Inputs();var obj=i.Object(i.Marker(3),i.Marker(1),i.Marker(-1));obj.LastObservedTimestamp=999;
        // A / reach: unit: old object parent=null kept [10,11,12]; this identity root is the supplied world origin.
        obj.NativePose.SetParent(i.CameraPose);
        for(int n=0;n<3;n++)obj.Markers[n].LastObservedTimestamp=(uint)(10+n);
        i.Mark(obj,timestamp:123);
        Assert.Equal(new uint?[]{10,123,12},obj.Markers.Select(m=>m.LastObservedTimestamp));Assert.Equal(999u,obj.LastObservedTimestamp);Assert.Empty(i.Logs);
    }
    [Fact] public void TimestampIsPlainStoreRatherThanMaximum()
    {
        using var i=new Inputs();var obj=i.Object(i.Marker());
        // A / reach: unit: old object parent=null kept 999; use the fixture's supplied identity world origin.
        obj.NativePose.SetParent(i.CameraPose);
        obj.Markers[0].LastObservedTimestamp=999;i.Mark(obj,timestamp:3);Assert.Equal(3u,obj.Markers[0].LastObservedTimestamp);
    }
    [Fact] public void AreaRatioAtLeastPointOneIsRejected()
    {
        using var i=new Inputs();var obj=i.Object(i.Marker(width:11,height:10));obj.Markers[0].LastObservedTimestamp=17;i.Mark(obj);
        Assert.Equal(17u,obj.Markers[0].LastObservedTimestamp);Assert.Equal("ObservableObject.SetMarkerAsObserved.NoMatchingMarkerFound",Assert.Single(i.Logs).Key);
    }
    [Theory][InlineData(0u)][InlineData(0x7FC00001u)]
    public void StrictZeroOrNaNToleranceRejectsExactAreaMatch(uint bits)
    {using var i=new Inputs();var obj=i.Object(i.Marker());i.Mark(obj,tolerance:BitConverter.UInt32BitsToSingle(bits));Assert.Null(obj.Markers[0].LastObservedTimestamp);Assert.Single(i.Logs);}
    [Fact] public void NearerAreaFailureDoesNotSuppressFartherValidCandidate()
    {
        using var i=new Inputs();var obj=i.Object(i.Marker(width:11,height:10),i.Marker(1));
        // A / reach: unit: old object parent=null left the second timestamp null.
        obj.NativePose.SetParent(i.CameraPose);i.Mark(obj);
        Assert.Null(obj.Markers[0].LastObservedTimestamp);Assert.Equal(73u,obj.Markers[1].LastObservedTimestamp);Assert.Empty(i.Logs);
    }
    [Fact] public void OutsideContainmentRejectsBeforeAnyTimestampStore()
    {using var i=new Inputs();var obj=i.Object(i.Marker(30));i.Mark(obj);Assert.Null(obj.Markers[0].LastObservedTimestamp);Assert.Equal("Marker:provided-code-name",Assert.Single(i.Logs).Text);}
    [Fact] public void ZeroObservedAreaIsNotAZeroRatioMatchEvenWithInfiniteTolerance()
    {using var i=new Inputs();var obj=i.Object(i.Marker());i.Mark(obj,Seen(size:0),tolerance:float.PositiveInfinity);Assert.Null(obj.Markers[0].LastObservedTimestamp);Assert.Single(i.Logs);}
    [Fact] public void UnknownCodeHasWarningOneWithExactFormatAndSignedArgumentInput()
    {
        using var i=new Inputs();var obj=i.Object(i.Marker());short? code=null;
        obj.SetMarkerAsObserved(Seen(code:(MarkerType)0xFFFF),5,Tol,i.Camera,(l,c,k,t)=>i.Logs.Add(new(l,c,k,t)),value=>{code=value;return "provided-code-name";});
        Assert.Equal((short)-1,code);Assert.Equal(new Log(EngineLogLevel.Warning,null,"ObservableObject.SetMarkerAsObserved","No markers found with code (-1:provided-code-name) of given 'nearestTo' observed marker"),Assert.Single(i.Logs));Assert.Null(obj.Markers[0].LastObservedTimestamp);
        // The name recipient is an explicit input; this does not certify its own invalid-code Error branch.
    }
    [Fact] public void EmptyCodeVectorUsesWarningTwo()
    {using var i=new Inputs();var obj=i.Object(i.Marker());obj.MarkerCodeIndex[(short)Code].Clear();i.Mark(obj);Assert.Equal(new Log(EngineLogLevel.Warning,null,"ObservableObject.SetMarkerAsObserved.NoMatchingMarkerFound","Marker:provided-code-name"),Assert.Single(i.Logs));}
    [Fact] public void KnownFrameMismatchSkipsProjectionIncludingMalformedCoefficients()
    {
        using var i=new Inputs(new double[]{1});using var other=new Pose3d();var obj=i.Object(i.Marker(parent:other));var policy=new List<string>();StackDiagnostics.Reported+=policy.Add;
        try{i.Mark(obj);Assert.Single(i.Logs);Assert.DoesNotContain(policy,s=>s.Contains("M1-034/M11-013"));Assert.Null(obj.Markers[0].LastObservedTimestamp);}
        finally{StackDiagnostics.Reported-=policy.Add;}
    }
    [Fact] public void BehindCameraSkipsMalformedCoefficientReadAndProducesNoMatchWarning()
    {
        using var i=new Inputs(new double[]{1});var obj=i.Object(i.Marker(z:-128));var policy=new List<string>();StackDiagnostics.Reported+=policy.Add;
        try{i.Mark(obj);Assert.Single(i.Logs);Assert.DoesNotContain(policy,s=>s.Contains("M1-034/M11-013"));Assert.Null(obj.Markers[0].LastObservedTimestamp);}
        finally{StackDiagnostics.Reported-=policy.Add;}
    }
    [Fact] public void NeededMalformedCoefficientReadUsesVisiblePolicyAndPreservesPriorTime()
    {
        using var i=new Inputs(new double[]{1});var obj=i.Object(i.Marker());obj.Markers[0].LastObservedTimestamp=88;var policy=new List<string>();StackDiagnostics.Reported+=policy.Add;
        // A / reach: unit: old object parent=null produced NoMatchingMarkerFound before coefficient access.
        obj.NativePose.SetParent(i.CameraPose);
        try{i.Mark(obj);i.Mark(obj);Assert.Equal(88u,obj.Markers[0].LastObservedTimestamp);Assert.Empty(i.Logs);Assert.Equal(2,policy.Count(s=>s.Contains("M1-034/M11-013")&&s.Contains("0085E2AE")));}
        finally{StackDiagnostics.Reported-=policy.Add;}
    }
    [Fact] public void LegacyUnboundCandidateWithholdsWholeChoiceRatherThanBiasingTowardKnownOne()
    {
        SteppedBehavior.ResetMissingForTests();using var i=new Inputs();using var legacy=new Pose3d(Mat3.Identity,new Vec3(0,0,128));var obj=i.Object(i.Marker(),new(Code,BlockFace.Front,legacy,10));obj.Markers[0].LastObservedTimestamp=1;obj.Markers[1].LastObservedTimestamp=2;
        var missing=new List<string>();SteppedBehavior.MissingReported+=missing.Add;
        try{i.Mark(obj);Assert.Equal(new uint?[]{1,2},obj.Markers.Select(m=>m.LastObservedTimestamp));Assert.Empty(i.Logs);Assert.Contains(missing,s=>s.Contains("M11-013 SetMarkerAsObserved")&&s.Contains("parent-root binding"));}
        finally{SteppedBehavior.MissingReported-=missing.Add;}
    }
    [Fact] public void UnboundNameRecipientDoesNotInventEnumTextOrNativeWarning()
    {
        SteppedBehavior.ResetMissingForTests();using var i=new Inputs();var obj=i.Object(i.Marker());var missing=new List<string>();SteppedBehavior.MissingReported+=missing.Add;
        try{i.Mark(obj,Seen(code:MarkerType.Arrow),names:false);Assert.Empty(i.Logs);Assert.Contains(missing,s=>s.Contains("GetCodeName0x0087E1D0"));}
        finally{SteppedBehavior.MissingReported-=missing.Add;}
    }
    [Fact] public void ProducerFeedsCheckedUnsignedPositionalConsumerWithoutChangingObjectTime()
    {
        using var i=new Inputs();var destination=i.Object(i.Marker());var source=i.Object(i.Marker());destination.Markers[0].LastObservedTimestamp=7;destination.LastObservedTimestamp=11;
        // A / reach: unit: old object parents=null left destination time 7; bind both to the supplied world origin.
        destination.NativePose.SetParent(i.CameraPose);source.NativePose.SetParent(i.CameraPose);
        i.Mark(source,timestamp:0xF0000000);BlockWorld.UpdateMarkerObservationTimes(destination,source);
        Assert.Equal(0xF0000000u,destination.Markers[0].LastObservedTimestamp);Assert.Equal(11u,destination.LastObservedTimestamp);
    }
    [Fact] public void CentroidUsesNativeZeroTwoOneThreeOrderAndAreaUsesBothTriangles()
    {
        var points=new[]{new Vec2(16777216,0),new Vec2(1,0),new Vec2(-16777216,0),new Vec2(3,0)};
        Assert.Equal(1f,ObservableObject.MarkerCentroid(points).X);
        Assert.Equal(0x41400000u,BitConverter.SingleToUInt32Bits(ObservableObject.MarkerQuadArea(new[]{new Vec2(0,0),new Vec2(0,3),new Vec2(4,0),new Vec2(4,3)})));
    }
    [Fact] public void ActualCreateObjectsEntryCallsProducerAndKeepsUnboundNativeInputsVisible()
    {
        SteppedBehavior.ResetMissingForTests();var camera=new CameraModel(CameraCalibration.Nominal(),Pose3d.Identity);var known=CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE1).Single(m=>m.Face==BlockFace.Top);using var pose=new Pose3d(Mat3.Identity,new Vec3(0,0,200));
        var corners=known.CornersOnObject().Select(p=>camera.Project(pose.Apply(p))!.Value).ToArray();var observed=new ObservedMarker(known.Code,corners,173,0,0);var world=new BlockWorld();var missing=new List<string>();SteppedBehavior.MissingReported+=missing.Add;
        try
        {
            var made=(List<ObservableObject>)typeof(BlockWorld).GetMethod("CreateObjectsFromMarkers",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(world,new object[]{new[]{observed},camera})!;
            var obj=Assert.Single(made);Assert.Equal(173u,obj.LastObservedTimestamp);
            //Native KnownMarker ctor87E27C clears timestamp48; public unknown-input fixtures above remain nullable.
            Assert.All(obj.Markers,m=>Assert.Equal(0u,m.LastObservedTimestamp));
            Assert.Contains(missing,s=>s.Contains("M11-013 SetMarkerAsObserved")&&s.Contains("parent-root binding"));Assert.DoesNotContain(missing,s=>s.Contains("writer/body is not supplied"));
        }
        finally{SteppedBehavior.MissingReported-=missing.Add;}
    }
}
