using System.Numerics;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Cozmo.Robot.Vision.Jpeg;
using Cozmo.Transport;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class M3M4RelativeBuilderTests
{
    private const ObjectType Type=ObjectType.Block_LIGHTCUBE1;
    private static LedPattern[] Words()=>Enumerable.Range(0,4).Select(i=>new LedPattern(
        new LedColor((byte)(i+1),2,3,4),new LedColor(5,(byte)(i+6),7,8),
        (uint)(30+i),(uint)(60+i),(uint)(90+i),(uint)(120+i),-i-1)).ToArray();
    private static SourceLogSink Quiet=(_,_,_,_)=>{};
    private static float F(int bits)=>BitConverter.Int32BitsToSingle(bits);
    private static string Shipped()
    {
        for(var d=new DirectoryInfo(AppContext.BaseDirectory);d!=null;d=d.Parent)
        {var p=Path.Combine(d.FullName,"re-analysis/obb/assets/cozmo_resources");if(Directory.Exists(p))return p;}
        throw new DirectoryNotFoundException();
    }

    // reach: unit (substitutes native WhichCubeLEDs result; all masks including invalid are checked directly).
    [Theory]
    [InlineData(1,2,3,0,1)][InlineData(2,1,2,3,0)][InlineData(3,2,3,0,1)]
    [InlineData(4,0,1,2,3)][InlineData(5,0,1,2,3)][InlineData(6,1,2,3,0)]
    [InlineData(7,0,1,2,3)][InlineData(8,3,0,1,2)][InlineData(9,3,0,1,2)]
    [InlineData(10,0,1,2,3)][InlineData(11,0,1,2,3)][InlineData(12,0,1,2,3)]
    [InlineData(0,0,1,2,3)][InlineData(13,0,1,2,3)][InlineData(-1,0,1,2,3)]
    public void NativeMasksMoveAllSevenWords(int mask,int a,int b,int c,int d)
    {
        var source=Words();var logs=new List<string>();
        var result=RelativeCubeLights.Rotate(source,mask,(_,_,key,text)=>logs.Add(key+":"+text));
        Assert.Equal(new[]{source[a],source[b],source[c],source[d]},result);
        if(mask is 1 or 2 or 3 or 4 or 6 or 8 or 9 or 12)Assert.Empty(logs);
        else Assert.Equal($"ActiveCube.MakeStateRelativeToXY:Unexpected reference LED {mask}.",Assert.Single(logs));
    }

    // reach: unit (native Radians input substituted; atan2f and pose producer are outside this predicate test).
    [Theory]
    [InlineData(0x00000000,9)][InlineData(0x3727C5AB,9)][InlineData(0x3727C5AC,3)]
    [InlineData(unchecked((int)0xB727C5AC),9)]
    [InlineData(0x3FC90F87,3)][InlineData(0x3FC90F88,6)][InlineData(0x3FC90FDB,6)]
    [InlineData(unchecked((int)0xBFC90F87),9)][InlineData(unchecked((int)0xBFC90F88),12)]
    [InlineData(0x40490FDA,6)][InlineData(0x40490FDB,6)]
    [InlineData(unchecked((int)0xC0490FDB),6)][InlineData(unchecked((int)0xC0490FDA),12)]
    public void CornerPreservesStrictEpsilonAndPiWrap(int bits,int mask)
        => Assert.Equal(mask,RelativeCubeLights.Corner(new Radians(F(bits))));

    // reach: unit (native Radians input substituted; checked q/r branches and circular comparison at +/-pi).
    [Theory]
    [InlineData(0x3F490F33,1)][InlineData(0x3F490F34,2)][InlineData(0x3F490FDB,2)]
    [InlineData(unchecked((int)0xBF491082),1)][InlineData(unchecked((int)0xBF491083),8)]
    [InlineData(0x4016CBBA,2)][InlineData(0x4016CBBB,4)][InlineData(0x4016CBE4,4)]
    [InlineData(unchecked((int)0xC016CC0D),8)][InlineData(unchecked((int)0xC016CC0E),4)]
    [InlineData(0x40490FDB,4)][InlineData(unchecked((int)0xC0490FDB),4)]
    public void FacePreservesEpsilonAtAllFourBoundaries(int bits,int mask)
        => Assert.Equal(mask,RelativeCubeLights.Face(new Radians(F(bits))));

    // reach: unit (substitutes ComputeNormal output to isolate strict tie selection; actual normal and production join covered in B35).
    [Theory][InlineData(false,(byte)1)][InlineData(true,(byte)1)][InlineData(false,(byte)2)]
    public void TopMarkerStrictTieAndRootPoseFeedTheNativeChoice(bool otherFirst,byte mode)
    {
        using var parent=new Pose3d(0,new Vec3(0,0,1),new Vec3(10,20,0));
        using var markerPose=new Pose3d(F(unchecked((int)0xBFC90FDB)),new Vec3(1,0,0),Vec3.Zero);
        var top=new KnownMarker((MarkerType)9,BlockFace.Top,markerPose,25);
        var other=new KnownMarker((MarkerType)6,BlockFace.Front,markerPose,25);
        var cube=new ObservableObject(1,Type,otherFirst?new[]{other,top}:new[]{top,other}){Pose=parent};
        var source=Words();var logs=new List<string>();
        var result=RelativeCubeLights.Apply(cube,source,mode,new Vector2(11,20),(_,_,key,_)=>logs.Add(key),_=>{},(_,_)=>new Vec3(0,0,625));
        // atan2(0,1)-yaw=0: corner9 => false once; face1 => half turn. Equal normals keep the first marker.
        int[] order=otherFirst?new[]{1,2,3,0}:mode==1?new[]{3,0,1,2}:new[]{2,3,0,1};
        Assert.Equal(order.Select(i=>source[i]),result!);
        Assert.Equal(otherFirst,logs.Any(l=>l.EndsWith("IgnoringBecauseBlockOnSide")));
    }

    // reach: unit (substitutes ComputeNormal output to isolate located/active-slot routing; production normal join covered in B35).
    [Theory][InlineData((byte)1)][InlineData((byte)2)]
    public void RelativeUnitUsesLocatedStateAndItsOwnActiveSlot(byte mode)
    {
        using var rig=new M4ControlTests.Rig(Shipped());rig.ToSynced();rig.State();M4ControlTests.ConnectCube(rig);
        var world=rig.Robot.LocalizationWorld;var connected=world.ConnectedObjects.Single(o=>o.ActiveId==0);
        var original=(LedPattern[])connected.ActiveLedState.Clone();
        using var pose=new Pose3d(F(0x3FC90FDB),new Vec3(0,1,0),Vec3.Zero);
        var located=new ObservableObject(connected.ObjectId,Type,CubeGeometry.MarkersFor(Type))
            {PoseState=PoseState.Known,OriginId=world.CurrentOriginId,Pose=pose};
        world.AddLocatedObject(located);
        var leds=Enumerable.Range(0,4).Select(i=>new LedPattern(LedColor.White,LedColor.Blue,(uint)(100+i*30),(uint)(100+i*60),30,60,i*30)).ToArray();
        int mark=rig.Mark();
        Assert.Equal(0u,rig.Robot.Lights.Cubes.SetObjectLightsById(located.ObjectId,new(leds,0,600,true,true,""){RelativeMode=mode},
            (m,_)=>new Vec3(0,0,m.Face==BlockFace.Front?625:0)));
        var sent=rig.SentSince(mark).Where(m=>m is CubeID or CubeLights or SetCubeGamma).ToArray();
        Assert.Equal(2,sent.Length);Assert.Equal(0u,Assert.IsType<CubeID>(sent[0]).ObjectID);
        var wire=Assert.IsType<CubeLights>(sent[1]).Lights;
        // Front marker is on top after +pi/2 Y: mismatch gives corner6 (true once), face4 (unchanged).
        int[] order=mode==1?new[]{1,2,3,0}:new[]{0,1,2,3};
        for(int j=0;j<4;j++)
        {
            int i=order[j];Assert.Equal((ushort)0xFE73,wire[j].OnColor);Assert.Equal((ushort)0x801F,wire[j].OffColor);
            Assert.Equal((byte)(4+i),wire[j].OnFrames);Assert.Equal((byte)(4+2*i),wire[j].OffFrames);
            Assert.Equal((byte)1,wire[j].TransitionOnFrames);Assert.Equal((byte)2,wire[j].TransitionOffFrames);
            Assert.Equal((short)i,wire[j].Offset);
        }
        Assert.Equal(original,connected.ActiveLedState);Assert.NotEqual(original,located.ActiveLedState);
    }

    // reach: unit (direct checked SetObjectLights entry; absent located object and Charger exercise native lookup/cast gates).
    [Fact]
    public void NullLocatedAndNonCubeProduceNoLightSend()
    {
        using var rig=new M4ControlTests.Rig(Shipped());rig.ToSynced();rig.State();M4ControlTests.ConnectCube(rig);
        var id=rig.Robot.LocalizationWorld.ConnectedObjects.Single(o=>o.ActiveId==0).ObjectId;
        var p=new CubeLightPattern(Words(),0,1,true,true,""){RelativeMode=1};int mark=rig.Mark();
        Assert.Equal(0x04000000u,rig.Robot.Lights.Cubes.SetObjectLightsById(id,p));
        var charger=new ObservableObject(0x123456,ObjectType.Charger_Basic,Array.Empty<KnownMarker>()){OriginId=rig.Robot.LocalizationWorld.CurrentOriginId,PoseState=PoseState.Known};
        rig.Robot.LocalizationWorld.AddLocatedObject(charger);
        Assert.Equal(0x04000000u,rig.Robot.Lights.Cubes.SetObjectLightsById(charger.ObjectId,p));
        Assert.DoesNotContain(rig.SentSince(mark),m=>m is CubeID or CubeLights);
    }

    // reach: unit (located-only identity and substituted ComputeNormal output to isolate active slot; no missing normal join remains).
    [Fact]
    public void RelativeSendUsesLocatedSlotWithoutAConnectedCopy()
    {
        using var rig=new M4ControlTests.Rig(Shipped());rig.ToSynced();rig.State();var world=rig.Robot.LocalizationWorld;
        var located=new ObservableObject(0x123456,Type,CubeGeometry.MarkersFor(Type)){OriginId=world.CurrentOriginId,PoseState=PoseState.Known,ActiveId=2};
        world.AddLocatedObject(located);int mark=rig.Mark();
        Assert.Equal(0u,rig.Robot.Lights.Cubes.SetObjectLightsById(located.ObjectId,new(Words(),0,1,true,true,""){RelativeMode=2},
            (m,_)=>new Vec3(0,0,m.Face==BlockFace.Front?625:0)));
        Assert.Equal(2u,Assert.Single(rig.SentSince(mark).OfType<CubeID>()).ObjectID);
    }

    // reach: unit (direct SetObjectLights with byte mode 7; confirms errG, unchanged LED order and continued wire send).
    [Fact]
    public void InvalidRelativeModeLogsSetsErrorAndStillSends()
    {
        bool old=EngineErrorState.ErrorFlagSet;
        try
        {
            using var rig=new M4ControlTests.Rig(Shipped());rig.ToSynced();rig.State();M4ControlTests.ConnectCube(rig);
            var world=rig.Robot.LocalizationWorld;var connected=world.ConnectedObjects.Single(o=>o.ActiveId==0);
            var located=new ObservableObject(connected.ObjectId,Type,CubeGeometry.MarkersFor(Type)){OriginId=world.CurrentOriginId,PoseState=PoseState.Known,ActiveId=0};world.AddLocatedObject(located);
            var words=Words();EngineErrorState.ErrorFlagSet=false;int mark=rig.Mark();
            Assert.Equal(0u,rig.Robot.Lights.Cubes.SetObjectLightsById(connected.ObjectId,new(words,0,1,true,true,""){RelativeMode=7}));
            Assert.True(EngineErrorState.ErrorFlagSet);Assert.Equal(words,located.ActiveLedState);
            Assert.Single(rig.SentSince(mark).OfType<CubeLights>());Assert.Contains(rig.Log,l=>l.Contains("Unrecognized relative LED mode"));
        }
        finally{EngineErrorState.ErrorFlagSet=old;}
    }

    // reach: unit (direct native SetObjectLights; substituted rotation u32 covers O1 on mode0 as well as relative mode).
    [Theory][InlineData(0u,0)][InlineData(1u,1)][InlineData(30u,1)][InlineData(uint.MaxValue,255)][InlineData(0xFFFFFFFEu,0)]
    public void CubeIdRotationUsesNativeUnsignedFrames(uint rotation,int frames)
    {
        using var rig=new M4ControlTests.Rig(Shipped());rig.ToSynced();rig.State();M4ControlTests.ConnectCube(rig);
        var id=rig.Robot.LocalizationWorld.ConnectedObjects.Single(o=>o.ActiveId==0).ObjectId;int mark=rig.Mark();
        rig.Robot.Lights.Cubes.SetObjectLightsById(id,new(Words(),rotation,1,true,false,""));
        Assert.Equal((byte)frames,Assert.Single(rig.SentSince(mark).OfType<CubeID>()).RotationPeriodFrames);
    }

    private sealed class Owner:ActionRunner
    {
        public Owner():base(0,0){}
        public override uint CheckIfDone()=>0;
        public override bool CanInterrupt()=>false;
    }
    // reach: unit (public action-light entry with minimal ActionRunner subclass; native action bodies remain their owners' scope).
    [Theory][InlineData(false)][InlineData(true)]
    public void ActionTagLifetimeControlsCallbackButNeverPop(bool delete)
    {
        using var rig=new M4ControlTests.Rig(Shipped());rig.ToSynced();rig.State();M4ControlTests.ConnectCube(rig);
        var lights=rig.Robot.Lights.Cubes;lights.EnableGameLayerOnly(true);var owner=new Owner();
        int calls=0;bool popped=false;
        try
        {
            Assert.True(lights.PlayLightAnimFromAction(Type,"Flash",owner.Tag,()=>{calls++;popped=lights.PlayingForTest(Type,0) is null;}));
            if(delete){owner.Prep();owner.WatcherEnding();}
            lights.StopLightAnim("Flash",0,Type);
            Assert.Equal(delete?0:1,calls);Assert.Null(lights.PlayingForTest(Type,0));Assert.Equal(!delete,popped);
        }
        finally{if(!delete){owner.Prep();owner.WatcherEnding();}}
    }

    // reach: production — generic public play explicitly retains tag0, both empty and real callback complete normally.
    [Theory][InlineData(false)][InlineData(true)]
    public void GenericTagZeroAndEmptyCallback(bool hasCallback)
    {
        using var rig=new M4ControlTests.Rig(Shipped());rig.ToSynced();rig.State();M4ControlTests.ConnectCube(rig);int calls=0;
        var lights=rig.Robot.Lights.Cubes;
        Assert.True(lights.PlayLightAnim(Type,"Flash",completed:hasCallback?()=>calls++:null));
        rig.Tick(610);Assert.Equal(hasCallback?1:0,calls);Assert.Null(lights.PlayingForTest(Type,1));
    }

    // reach: production — failed public FromAction call cannot replace a prior generic entry's tag0/callback.
    [Fact]
    public void FailedActionPlayDoesNotRetagTheExistingEntry()
    {
        using var rig=new M4ControlTests.Rig(Shipped());rig.ToSynced();rig.State();M4ControlTests.ConnectCube(rig);
        var lights=rig.Robot.Lights.Cubes;lights.EnableGameLayerOnly(true);int calls=0;
        Assert.True(lights.PlayLightAnim(Type,"Flash",0,completed:()=>calls++));
        Assert.False(lights.PlayLightAnimFromAction(Type,"DoesNotExist",uint.MaxValue,()=>calls+=100));
        lights.StopLightAnim("Flash",0,Type);Assert.Equal(1,calls);
    }

    // reach: production - public PlayLightAnim with shipped Flash and relative modifier, real native normal,
    // located/connected cube state and normal CubeID/CubeLights wire output. Located pose is a known world input;
    // this test does not claim the independent vision-localization producer or physical robot acceptance.
    [Fact]
    public void PublicRelativePlaySendsRotatedLedPayloadUsingNativeNormal()
    {
        using var rig=new M4ControlTests.Rig(Shipped());rig.ToSynced();rig.State();M4ControlTests.ConnectCube(rig);
        var world=rig.Robot.LocalizationWorld;var connected=world.ConnectedObjects.Single(o=>o.ActiveId==0);
        using var pose=new Pose3d(F(0x3FC90FDB),new Vec3(0,1,0),Vec3.Zero);
        var located=new ObservableObject(connected.ObjectId,Type,CubeGeometry.MarkersFor(Type)){OriginId=world.CurrentOriginId,PoseState=PoseState.Known,Pose=pose};
        world.AddLocatedObject(located);
        // Shipped Flash is white/black, on/off100ms. Modifier adds unique timings so rotation is observable.
        var modifier=Enumerable.Range(0,4).Select(i=>new LedPattern(default,default,(uint)(30*i),(uint)(60*i),30,60,30*i)).ToArray();
        var missing=new List<string>();Cozmo.Robot.Behavior.SteppedBehavior.ResetMissingForTests();
        Cozmo.Robot.Behavior.SteppedBehavior.MissingReported+=missing.Add;int mark=rig.Mark();
        try{Assert.True(rig.Robot.Lights.Cubes.PlayLightAnim(Type,"Flash",modifier:true,lights:new(modifier,0,1,Vector2.Zero)));}
        finally{Cozmo.Robot.Behavior.SteppedBehavior.MissingReported-=missing.Add;}
        var sent=rig.SentSince(mark).Where(m=>m is CubeID or CubeLights).ToArray();
        Assert.Equal(2,sent.Length);Assert.Equal(0u,Assert.IsType<CubeID>(sent[0]).ObjectID);
        var wire=Assert.IsType<CubeLights>(sent[1]).Lights;
        // +pi/2 about Y puts Front up; native side fallback corner6 moves source [1,2,3,0].
        int[] order={1,2,3,0};
        for(int j=0;j<4;j++)
        {
            int i=order[j];Assert.Equal((ushort)0xFE73,wire[j].OnColor);Assert.Equal((ushort)0x8000,wire[j].OffColor);
            Assert.Equal((byte)(4+i),wire[j].OnFrames);Assert.Equal((byte)(4+2*i),wire[j].OffFrames);
            Assert.Equal((byte)1,wire[j].TransitionOnFrames);Assert.Equal((byte)2,wire[j].TransitionOffFrames);Assert.Equal((short)i,wire[j].Offset);
        }
        Assert.DoesNotContain(missing,m=>m.Contains("KnownMarker::ComputeNormal"));
    }

    // reach: unit (raw encoding/geometry supplied to checked decoder; actual firmware short-frame producer UNKNOWN M3-050).
    [Theory][InlineData((byte)1,false)][InlineData((byte)1,true)][InlineData((byte)2,false)][InlineData((byte)2,true)]
    public void ShortRawSkipsBothDecoderTargetsAndLongTruncates(byte encoding,bool rgb)
    {
        int need=encoding==1?4:12;var shortPayload=Enumerable.Repeat((byte)99,need-1).ToArray();
        var skipped=rgb?EncodedImageDecode.DecodeRgb(encoding,shortPayload,Array.Empty<byte>(),2,2):EncodedImageDecode.DecodeGray(encoding,shortPayload,Array.Empty<byte>(),2,2);
        Assert.True(skipped.PolicySkipped);Assert.False(skipped.Ok);Assert.Null(skipped.Image);Assert.Contains("M1-034/M3-037",skipped.Error);
        byte[] payload=Enumerable.Range(1,need+5).Select(i=>(byte)i).ToArray();
        var result=rgb?EncodedImageDecode.DecodeRgb(encoding,payload,Array.Empty<byte>(),2,2):EncodedImageDecode.DecodeGray(encoding,payload,Array.Empty<byte>(),2,2);
        Assert.True(result.Ok);Assert.False(result.PolicySkipped);
        byte[] expected=encoding==1?(rgb?new byte[]{1,1,1,2,2,2,3,3,3,4,4,4}:new byte[]{1,2,3,4})
            :(rgb?new byte[]{1,2,3,4,5,6,7,8,9,10,11,12}:new byte[]{2,5,8,11});
        Assert.Equal(expected,result.Image!.Data);
    }

    // reach: unit (real synchronous vision entry and prior successful raw decode; short firmware input is UNKNOWN M3-050).
    [Theory][InlineData((byte)1)][InlineData((byte)2)]
    public void ShortRawPreservesThePreviouslyPublishedImage(byte encoding)
    {
        using var robot=CozmoRobot.CreateOffline();using var vision=new VisionSystem(robot,CameraCalibration.Nominal()){Enabled=false};
        var full=new CameraFrame{ImageId=1,Timestamp=1,Width=2,Height=2,Encoding=encoding,RawPayload=Enumerable.Repeat((byte)75,encoding==1?4:12).ToArray()};
        vision.ProcessFrame(full);var prior=vision.EncodedFrameCache;Assert.Equal(2,prior.Width);
        var logs=new List<EngineLogEntry>();robot.Engine.LogEntry+=logs.Add;var policy=new List<string>();StackDiagnostics.Reported+=policy.Add;
        try{Assert.Null(vision.ProcessFrame(new CameraFrame{ImageId=2,Timestamp=2,Width=2,Height=2,Encoding=encoding,RawPayload=new byte[]{8}}));}
        finally{StackDiagnostics.Reported-=policy.Add;}
        Assert.Same(prior,vision.EncodedFrameCache);Assert.Contains(policy,l=>l.Contains("M1-034/M3-037"));
        Assert.DoesNotContain(logs,e=>e.Name=="VisionComponent.UpdateVisionSystem.UpdateFailed");
    }
}
