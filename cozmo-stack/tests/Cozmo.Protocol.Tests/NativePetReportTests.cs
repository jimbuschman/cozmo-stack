using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Xunit;
using Fixture=Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("ObjectID process statics")]
public sealed class NativePetReportTests
{
    private sealed class Detector : IPetDetector
    {
        public bool IsAvailable { get; set; }=true;
        public bool ProvidesNativeResults { get; set; }=true;
        public IReadOnlyList<DetectedPet> Result { get; set; }=Array.Empty<DetectedPet>();
        public IReadOnlyList<DetectedPet> Detect(GrayImage image,uint timestamp)=>Result;
    }

    [Fact]
    public void QueuedCameraPublishesSignedOrderedReplacementAndEmptyClears()
    {
        using var scene=new Fixture();
        var vision=scene.M.Vision;
        var detector=new Detector { Result=new[] {
            new DetectedPet(5,PetType.Dog,new FaceRect(11,12,13,14),16777217){TrackerResultFlag=true},
            new DetectedPet(-3,PetType.Cat,new FaceRect(1,2,3,4),123){TrackerResultFlag=true},
            new DetectedPet(5,PetType.Cat,new FaceRect(91,92,93,94),9){TrackerResultFlag=true} } };
        vision.PetDetector=detector;vision.Enabled=true;vision.ModeEnableMask=1<<9;
        vision.Schedules=AllVisionModesSchedule.InitDefaultSchedules();
        scene.Rig.Robot.SetVisionWhileMoving(true);scene.Rig.Tick();
        var local=new List<RobotObservedPetReport>();var typed=new List<RobotObservedPetReport>();var raw=new List<byte[]>();
        int hostCount=0;
        vision.Pets.PetObserved+=(_,_)=>throw new InvalidOperationException("test host handler");
        vision.Pets.PetObserved+=(_,_)=>hostCount++;
        scene.Rig.Engine.RobotObservedPet+=typed.Add;
        scene.Rig.Engine.EngineToGameMessage+=(tag,body)=>{if(tag==0x48)raw.Add(body.ToArray());};
        using var subscription=scene.Rig.Engine.AnimationMessages.SubscribeMessage(0x48,true,p=>local.Add(Assert.IsType<RobotObservedPetReport>(p)));
        void Frame(uint id)
        {
            scene.Rig.State();scene.NativeCameraFrame(id,150);
            scene.Rig.Engine.VisionUpdateAllResultsFailed!();scene.Rig.Tick();
        }
        scene.Rig.State();scene.NativeCameraFrame(100,150);
        Assert.Empty(local);Assert.Empty(typed);
        scene.Rig.Tick(); // normal EngineRobot.Update drains the prepared result
        Assert.Equal(new[]{-3,5},local.Select(x=>x.PetId));
        Assert.Equal(new[]{-3,5},typed.Select(x=>x.PetId));
        Assert.Equal(2,hostCount);
        Assert.Equal(2,raw.Count);Assert.All(raw,b=>Assert.Equal(33,b.Length));
        using(var reader=new BinaryReader(new MemoryStream(raw[1])))
        {
            Assert.Equal(5,reader.ReadInt32());Assert.Equal(local[1].Timestamp,reader.ReadUInt32());
            Assert.Equal(1u,reader.ReadUInt32());Assert.Equal(16777216f,reader.ReadSingle());
            Assert.Equal(11f,reader.ReadSingle());Assert.Equal(12f,reader.ReadSingle());
            Assert.Equal(13f,reader.ReadSingle());Assert.Equal(14f,reader.ReadSingle());Assert.Equal((byte)PetType.Dog,reader.ReadByte());
        }
        Frame(101);Frame(102);
        Assert.Equal(3,vision.Pets.GetPetByID(5)!.TimesObserved);
        detector.Result=Array.Empty<DetectedPet>();Frame(103);
        Assert.Empty(vision.Pets.Pets);Assert.Equal(6,local.Count);
    }

    [Fact]
    public void NativeReplacementGatesOnlyNewPetsAndKeepsFirstDuplicate()
    {
        var world=new PetWorld();var published=new List<PetEntry>();
        var a=new DetectedPet(4,PetType.Cat,new FaceRect(1,2,3,4)){TrackerResultFlag=false};
        Assert.True(world.UpdateNative(new[]{a},1,_=>false,()=>true,(_,p,_)=>published.Add(p),a=>a()));
        var duplicate=a with { Rect=new FaceRect(9,9,9,9) };
        var fresh=new DetectedPet(9,PetType.Dog,new FaceRect(2,2,2,2)){TrackerResultFlag=true};
        Assert.True(world.UpdateNative(new[]{a,duplicate,fresh},2,_=>true,()=>true,(_,p,_)=>published.Add(p),a=>a()));
        Assert.Equal(2,world.GetPetByID(4)!.TimesObserved);
        Assert.Equal(a.Rect,world.GetPetByID(4)!.Rect);Assert.Null(world.GetPetByID(9));
    }

    [Fact]
    public void HostReentryOccursAfterAllNativePublications()
    {
        var world=new PetWorld();var published=new List<int>();
        var first=new DetectedPet(1,PetType.Cat,new FaceRect(1,2,3,4)){TrackerResultFlag=true};
        var second=new DetectedPet(2,PetType.Dog,new FaceRect(4,3,2,1)){TrackerResultFlag=true};
        world.PetObserved+=(_,_)=>world.Update(Array.Empty<DetectedPet>(),2,false);
        Assert.False(world.UpdateNative(new[]{first,second},1,_=>false,()=>true,(d,_,_)=>published.Add(d.Id),a=>a()));
        Assert.Equal(new[]{1,2},published);
    }

    [Fact]
    public void OmittedPartialAndNoInterfaceDoNotFabricateNativeReports()
    {
        using var scene=new Fixture();
        var vision=scene.M.Vision;
        var pet=new DetectedPet(7,PetType.Cat,new FaceRect(1,2,3,4)){TrackerResultFlag=true};
        var detector=new Detector {Result=new[]{pet}};
        vision.PetDetector=detector;vision.Enabled=true;vision.ModeEnableMask=1<<9;
        vision.Schedules=AllVisionModesSchedule.InitDefaultSchedules();
        scene.Rig.Robot.SetVisionWhileMoving(true);scene.Rig.Tick();
        var reports=new List<RobotObservedPetReport>();
        using var subscription=scene.Rig.Engine.AnimationMessages.SubscribeMessage(0x48,true,p=>reports.Add(Assert.IsType<RobotObservedPetReport>(p)));
        void Frame(uint id){scene.Rig.State();scene.NativeCameraFrame(id,150);scene.Rig.Engine.VisionUpdateAllResultsFailed!();scene.Rig.Tick();}
        Frame(200);Assert.Single(reports);Assert.Equal(1,vision.Pets.GetPetByID(7)!.TimesObserved);
        vision.ModeEnableMask=0;detector.Result=Array.Empty<DetectedPet>();Frame(201);
        Assert.Equal(1,vision.Pets.GetPetByID(7)!.TimesObserved);
        vision.ModeEnableMask=1<<9;detector.Result=new[]{pet with { TrackerResultFlag=null }};Frame(202);
        Assert.Equal(1,vision.Pets.GetPetByID(7)!.TimesObserved);Assert.Single(reports);
        detector.IsAvailable=false;Frame(203);Assert.Equal(1,vision.Pets.GetPetByID(7)!.TimesObserved);
        detector.IsAvailable=true;detector.Result=new[]{pet};scene.Robot.HasExternalInterface=false;
        Frame(204);Assert.Equal(2,vision.Pets.GetPetByID(7)!.TimesObserved);Assert.Single(reports);
    }

    [Fact]
    public void ReplacementInLocalDeliveryStopsOldReportsAndClearsNativeMap()
    {
        using var scene=new Fixture();
        var vision=scene.M.Vision;
        vision.PetDetector=new Detector {Result=new[]{
            new DetectedPet(1,PetType.Cat,new FaceRect(1,2,3,4)){TrackerResultFlag=true},
            new DetectedPet(2,PetType.Dog,new FaceRect(4,3,2,1)){TrackerResultFlag=true}}};
        vision.Enabled=true;vision.ModeEnableMask=1<<9;vision.Schedules=AllVisionModesSchedule.InitDefaultSchedules();
        scene.Rig.Robot.SetVisionWhileMoving(true);scene.Rig.Tick();
        var local=new List<int>();
        using var subscription=scene.Rig.Engine.AnimationMessages.SubscribeMessage(0x48,true,p=>
        {
            local.Add(Assert.IsType<RobotObservedPetReport>(p).PetId);
            scene.Rig.Engine.Robots.RemoveRobot(1,false);scene.Rig.Engine.Robots.AddRobot(1,withRic:false);
        });
        scene.Rig.State();scene.NativeCameraFrame(300,150);scene.Rig.Engine.VisionUpdateAllResultsFailed!();
        Assert.Equal(new[]{1},local);Assert.Empty(vision.Pets.Pets);
    }

    [Fact]
    public void TrackerFlagControlsNativeTelemetryAndVerify()
    {
        using var scene=new Fixture();
        var vision=scene.M.Vision;
        vision.PetDetector=new Detector {Result=new[]{new DetectedPet(-9,PetType.Cat,new FaceRect(1,2,3,4)){TrackerResultFlag=false}}};
        vision.Enabled=true;vision.ModeEnableMask=1<<9;vision.Schedules=AllVisionModesSchedule.InitDefaultSchedules();
        scene.Rig.Robot.SetVisionWhileMoving(true);scene.Rig.Tick();
        var logs=new List<EngineLogEntry>();scene.Rig.Engine.LogEntry+=logs.Add;
        void Frame(uint id){scene.Rig.State();scene.NativeCameraFrame(id,150);scene.Rig.Engine.VisionUpdateAllResultsFailed!();scene.Rig.Tick();}
        bool before=Cozmo.Transport.EngineErrorState.ErrorFlagSet;
        try
        {
            Frame(400);Frame(401);
            var events=logs.Where(l=>l.Name=="robot.vision.detected_pet").ToArray();
            Assert.Equal(2,events.Length);Assert.All(events,e=>{Assert.Equal("-9",e.Text);Assert.Equal("Cat",e.Data["$data"]);});
            Assert.Contains(logs,l=>l.Name=="PetWorld.Update.NewPetDetectionShouldBeObservedOnce" && l.Text.Contains("ID:-9 NumTimesObserved:2"));
            Assert.True(Cozmo.Transport.EngineErrorState.ErrorFlagSet);
        }
        finally { Cozmo.Transport.EngineErrorState.ErrorFlagSet=before; }
    }
}
