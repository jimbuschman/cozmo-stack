using System.Collections.Concurrent;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;
using Xunit.Abstractions;
using Fixture=Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class ChargerMountRecordedReplayTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(true,0u)]
    [InlineData(false,0x04000002u)]
    public async Task CapturedLightingAndExplicitFirmwareModelKeepSuccessSeparateFromTiltRejection(bool stationaryFirstFrame,uint expectedResult)
    {
        string? bundle=Environment.GetEnvironmentVariable("COZMO_CHARGER_REPLAY_BUNDLE");
        if(string.IsNullOrEmpty(bundle))return; // Optional private-input replay. The script enables it.
        var input=ReadCapture(bundle);
        using var f=new Fixture(sourceNvEmpty:true,preloadAnimations:true,pumpAnimations:true,
            sourceNvRaw:tag=>tag==CameraCalibration.NvEntryTag?input.Calibration:null,bodyHwVersion:input.BodyHwVersion);
        f.Rig.Robot.SetEnableFreeplayActivity(false);
        f.Motors(); // Drain startup motor commands before installing the recorded head input.
        var vision=f.M.Vision;
        vision.SetNativeOuterCalibration(vision.Calibration!);
        vision.Enabled=true;vision.ModeEnableMask=1<<VisionSystem.DetectingMarkers;
        vision.Schedules=AllVisionModesSchedule.InitDefaultSchedules();
        var trace=new ConcurrentQueue<string>();var faults=new ConcurrentQueue<Exception>();
        var reports=new List<RobotObservedObjectReport>();
        f.Rig.Engine.RobotObservedObject+=reports.Add;
        f.Rig.Engine.StackLogLine+=trace.Enqueue;
        f.Rig.Engine.LogEntry+=e=>trace.Enqueue(e.Name+": "+e.Text);
        f.Rig.Robot.HandlerFaulted+=faults.Enqueue;
        f.Rig.Engine.Faulted+=faults.Enqueue;
        SteppedBehavior.ResetMissingForTests();
        uint imageId=700;
        void Camera(byte[] pixels,RobotStatusFlag extra=0)
        {
            f.Motors(RobotStatusFlag.IsBodyAccMode|extra);
            uint timestamp=f.Robot.StoredState!.Timestamp;
            f.Rig.Data(new ImageImuData{ImageId=imageId,Line2Number=239});
            f.Rig.Data(new ImageImuData{ImageId=imageId+10000,Line2Number=0});f.Rig.Tick();
            f.Rig.CameraGrayFrame(imageId,timestamp,pixels);vision.WaitForProcessorIdle();
            f.Rig.Data(new ImageImuData{ImageId=++imageId,Line2Number=0});f.Rig.Tick();
            f.Rig.Engine.VisionUpdateAllResultsFailed!();f.Rig.Tick(65);
        }
        // Actual pair is a negative control. Repeating frame 12622 is a separate
        // stationary-camera model, NOT a successful replay of the recorded run.
        output.WriteLine(stationaryFirstFrame?"INPUT MODEL: repeat brightened frame 12622 for confirmation; ideal firmware replies and rendered later views":"INPUT: brightened recorded frames 12622,12623; ideal path replies; native tilt rejection expected");
        foreach(uint frame in stationaryFirstFrame?new uint[]{12622,12622}:new uint[]{12622,12623})
        {
            var captured=input.Frames[frame];
            f.Head=captured.Head;f.Lift=captured.Lift;f.X=captured.X;f.Y=captured.Y;f.Yaw=captured.Yaw;
            byte[] pixels=captured.Pixels.ToArray();
            for(int i=0;i<pixels.Length;i++)pixels[i]=(byte)Math.Min(255,pixels[i]+12);
            Camera(pixels);
        }
        var target=Assert.Single(f.M.World.LocatedObjects,obj=>obj.Type==ObjectType.Charger_Basic);
        Assert.Contains(reports,report=>report.ObjectId==target.ObjectId&&report.Family==(uint)ObjectFamily.Charger&&report.Type==(uint)ObjectType.Charger_Basic);
        using var scene=new Pose3d(target.NativePose);
        float tilt=(float)(Math.Acos(Math.Clamp(1-2*(scene.Quaternion.X*scene.Quaternion.X+scene.Quaternion.Y*scene.Quaternion.Y),-1,1))*180/Math.PI);
        // ARM marker/PnP/camera composition oracle: frame12622 4.8581856 deg
        // (flattened by the native 5-deg rule); frame12623 10.2054400 deg (kept).
        Assert.InRange(tilt,stationaryFirstFrame?0:10.195f,stationaryFirstFrame?.01f:10.215f);
        output.WriteLine($"Captured head={f.Head}, pose={scene.Translation}, q={scene.Quaternion.W},{scene.Quaternion.X},{scene.Quaternion.Y},{scene.Quaternion.Z}");
        // Explicit robot-motion input: back away along the captured heading so
        // this exercises planning instead of GetPreActionPoses' already-close shortcut.
        f.X-=120*MathF.Cos(f.Yaw);f.Y-=120*MathF.Sin(f.Yaw);f.Motors(RobotStatusFlag.IsBodyAccMode);
        int mark=f.Rig.Mark(),readMark=mark;
        var task=f.Rig.Robot.Motion.QueueAiMountChargerAsync(target.ObjectId);
        var runner=Assert.IsType<NativeDriveToMountChargerRunner>(f.Robot.ActionList.GetCurrentAction());
        bool aligned=false,backing=false,docking=false,contacts=false;int paths=0,corrections=0;byte animationTag=0;
        float wireX=0,wireY=0,wireYaw=f.Yaw,backupDistance=0,backupTime=0;
        uint? backupPath=null;
        try
        {
            for(int tick=0;tick<300&&!task.IsCompleted;tick++)
            {
                Thread.Sleep(2);
                var stream=f.Rig.Robot.Animations.Scheduler.Stream;
                f.Rig.Data(new AnimationState{NumAudioFramesPlayed=stream.FramesStreamed,NumAnimBytesPlayed=stream.BytesStreamed});
                f.Motors(RobotStatusFlag.IsBodyAccMode|(docking&&!aligned?RobotStatusFlag.IsPickingOrPlacing:0)|(contacts?RobotStatusFlag.IsOnCharger:0));
                var sent=f.Rig.SentSince(readMark);readMark+=sent.Count;
                foreach(var message in sent)
                {
                    if(message is StartOfAnimation start){animationTag=start.AnimId;f.Rig.Data(new AnimationStarted{AnimId=animationTag});}
                    if(message is EndOfAnimation)f.Rig.Data(new AnimationEnded{AnimId=animationTag});
                    if(message is AppendPathSegmentLine line)
                    {
                        wireX=line.XEndMm;wireY=line.YEndMm;
                        wireYaw=MathF.Atan2(line.YEndMm-line.YStartMm,line.XEndMm-line.XStartMm)+(line.Speed.SpeedMmps<0?MathF.PI:0);
                        if(aligned)
                        {
                            Assert.Equal(-30f,line.Speed.SpeedMmps);
                            Assert.InRange(MathF.Sqrt(MathF.Pow(line.XEndMm-line.XStartMm,2)+MathF.Pow(line.YEndMm-line.YStartMm,2)),119.99f,120.01f);
                        }
                    }
                    if(message is AppendPathSegmentArc arc)
                    {
                        float end=arc.StartRad+arc.SweepRad;
                        wireX=arc.XCenterMm+arc.RadiusMm*MathF.Cos(end);wireY=arc.YCenterMm+arc.RadiusMm*MathF.Sin(end);
                        wireYaw=end+(arc.SweepRad>0?MathF.PI/2:-MathF.PI/2)+(arc.Speed.SpeedMmps<0?MathF.PI:0);
                    }
                    if(message is AppendPathSegmentPointTurn point){wireX=point.XMm;wireY=point.YMm;wireYaw=point.TargetAngleRad;}
                    if(message is ExecutePath execute)
                    {
                        paths++;output.WriteLine($"Execute {execute.EventId} tick={tick}");
                        f.Rig.Data(new PathFollowingEvent{EventId=execute.EventId,EventType=(byte)PathEventType.Started});
                        if(!aligned)
                        {
                            using var goal=runner.Drive.Drive!.Goals[0].GetWithRespectToRoot();
                            output.WriteLine($"goal={goal.Translation}, q={goal.Quaternion.W},{goal.Quaternion.X},{goal.Quaternion.Y},{goal.Quaternion.Z}");
                            // With no carried object, FORWARD is MAIN -20 mm along
                            // its heading (Robot::SetPose 0x0051508C); invert that offset.
                            // Feed the actual wire endpoint, not the action's expected goal.
                            f.X=wireX+20*MathF.Cos(wireYaw);f.Y=wireY+20*MathF.Sin(wireYaw);f.Yaw=wireYaw;
                            Assert.InRange(Math.Abs(f.X-goal.Translation.X),0,10);
                            Assert.InRange(Math.Abs(f.Y-goal.Translation.Y),0,10);
                        }
                        else {backing=true;backupPath=execute.EventId;backupTime=f.Rig.Engine.Timer.SecondsF;}
                        f.Motors(RobotStatusFlag.IsBodyAccMode);
                        if(!aligned)f.Rig.Data(new PathFollowingEvent{EventId=execute.EventId,EventType=(byte)PathEventType.Completed});
                    }
                    if(message is DockWithObject dock)
                    {
                        docking=true;
                        Assert.Equal((byte)DockAction.Align,dock.DockAction);
                        Assert.Equal(30f,dock.SpeedMmps);
                        Assert.Equal(0f,dock.UnusedZero);Assert.False(dock.Field5);
                        Assert.Equal((byte)0,dock.DockingMethod);Assert.False(dock.Field8);
                        output.WriteLine($"Dock {dock}");
                        // Firmware align completion is supplied only after camera corrections below.
                    }
                    if(message is DockingErrorSignal signal)
                    {
                        Assert.True(float.IsFinite(signal.XDist)&&float.IsFinite(signal.YDist)&&float.IsFinite(signal.ZDist)&&float.IsFinite(signal.Angle));
                        corrections++;output.WriteLine($"Correction {signal.XDist}/{signal.YDist}/{signal.Angle}");
                        if(!aligned)
                        {
                            f.X+=signal.XDist*MathF.Cos(f.Yaw)-signal.YDist*MathF.Sin(f.Yaw);
                            f.Y+=signal.XDist*MathF.Sin(f.Yaw)+signal.YDist*MathF.Cos(f.Yaw);
                            f.Yaw+=signal.Angle;
                        }
                        if(corrections>=2&&!aligned && MathF.Abs(signal.XDist)<2 && MathF.Abs(signal.YDist)<2)
                        {aligned=true;f.Rig.Data(new PickAndPlaceResult{Field0=f.Robot.StoredState!.Timestamp,Field1=true});}
                    }
                }
                if(backing&&!contacts)
                {
                    // Ideal firmware reverse at the commanded 30 mm/s. Contact is a
                    // modeled input at the native docked MAIN position, never an
                    // injected action result or direct write to Sensors.OnCharger.
                    float now=f.Rig.Engine.Timer.SecondsF,distance=30*(now-backupTime);backupTime=now;
                    f.X-=distance*MathF.Cos(f.Yaw);f.Y-=distance*MathF.Sin(f.Yaw);backupDistance+=distance;
                    using var docked=ChargerGeometry.DockedRobotPoseInTree(target);
                    using var root=docked.GetWithRespectToRoot();
                    float dx=(float)root.Translation.X-f.X,dy=(float)root.Translation.Y-f.Y;
                    contacts=dx*dx+dy*dy<16f;
                    Assert.InRange(backupDistance,0,120);
                    f.Motors(RobotStatusFlag.IsBodyAccMode|(contacts?RobotStatusFlag.IsOnCharger:0));
                }
                if(!aligned)
                {
                    using var state=HistRobotState.From(f.Robot.GetCheckedMainPose(),f.Robot.StoredState!);
                    using var camera=f.Robot.CopyHistoricalCameraPose(state);
                    using var flat=camera.GetWithRespectToRoot();
                    var image=new GrayImage(320,240);image.Fill(150);
                    MarkerRenderer.DrawObject(image,MarkerLibrary.EmbeddedOrNull!,new CameraModel(vision.Calibration!,flat),ObjectType.Charger_Basic,scene);
                    Camera(image.Pixels,docking?RobotStatusFlag.IsPickingOrPlacing:0);
                }
            }
            foreach(string line in trace.Where(x=>x.StartsWith("MISSING",StringComparison.Ordinal)||x.Contains("ActionCompleted")||x.Contains("DriveToPose")||x.Contains("Dock")).TakeLast(120))output.WriteLine(line);
            Assert.True(task.IsCompleted,"mount did not complete");
            var result=await task.WaitAsync(TimeSpan.FromSeconds(2));
            output.WriteLine($"result={result.EngineResult:X8} paths={paths} corrections={corrections}");
            Assert.Equal(expectedResult,result.EngineResult);
            Assert.True(result.Admitted);
            Assert.Equal(expectedResult==0,f.Rig.Robot.Sensors.OnCharger);
            if(expectedResult==0)
            {
                Assert.Equal(2,paths);Assert.True(corrections>=2);Assert.True(aligned);Assert.NotNull(backupPath);
                Assert.InRange(backupDistance,1,120);
                Assert.Contains(trace,x=>x.Contains("MountCharger")&&x.Contains("succeeded with state SUCCESS"));
                Assert.Contains(f.Rig.SentSince(mark),message=>message is SetLiftHeight lift&&lift.HeightMm==45);
                Assert.Contains(trace,x=>x.StartsWith("Robot.SetPoseOnCharger.SetPose:",StringComparison.Ordinal));
            }
            else {Assert.Equal(1,paths);Assert.Equal(0,corrections);Assert.Equal(-1,f.Rig.Robot.Cubes.ChargerObjectId);}
            Assert.Empty(faults);
            // Keep inherited gaps visible and reject new ones on this path.
            // Their owning records/addresses and limits are in HANDOFF-CHARGER-MOUNT-E2E-1.md.
            Assert.All(trace.Where(line=>line.StartsWith("MISSING:",StringComparison.Ordinal)),
                line=>Assert.True(KnownBoundary.Any(prefix=>line.StartsWith("MISSING: "+prefix,StringComparison.Ordinal)),line));
        }
        finally{f.Robot.ActionList.Clear();}
    }

    private static readonly string[] KnownBoundary=
    [
        "M11-077: RemoveOwner", "M11-055 Processor VisionPoseData", "M11-033/M11-078 native ImageCache",
        "M11-058/M11-055 DecodeJPEG", "M11-037 CheckForCollisionWithRobot", "M12-043 pyramid container",
        "M13-033 Replanning State/plan text", "M13-033 PrintPlan", "M13-033 Path::PrintPath",
        "M13-034 IPathPlanner.PrintPath", "M12-002 PathSegment.Print", "M12-032 VizManager.DrawPath",
        "M12-024 | EraseAllPlannerObstacles(",
        "M8-015 ActionCompleted full ActionResult EnumToString", "M8-017 sequential failure full EnumToString",
        "M8-021 trigger DAS", "M8-023/M8-027 ProtoHandle callable",
        "RobotAudioClient::CreateAudioAnimation post-construction check",
        "ReactToCliffBehavior: vtable+0x24", "ReactToImpactBehavior: vtable+0x24",
        "ReactToMotorCalibrationBehavior: vtable+0x24", "ReactToPickupBehavior: vtable+0x24",
        "ReactToRobotOnBackBehavior: vtable+0x24", "ReactToRobotOnFaceBehavior: vtable+0x24",
        "ReactToRobotOnSideBehavior: vtable+0x24", "ReactToRobotShakenBehavior: vtable+0x24",
    ];

    private sealed record FrameInput(byte[] Pixels,float Head,float Lift,float X,float Y,float Yaw);
    private sealed record Capture(byte[] Calibration,uint BodyHwVersion,Dictionary<uint,FrameInput> Frames);
    private Capture ReadCapture(string bundle)
    {
        string path=Path.Combine(bundle,"messages.jsonl");
        using(var file=File.OpenRead(path))output.WriteLine("messages SHA256="+Convert.ToHexString(SHA256.HashData(file)));
        var camera=new CozmoCamera();var images=new Dictionary<uint,(uint Timestamp,byte[] Pixels)>();
        var states=new List<RobotState>();byte[]? calibration=null;uint? bodyHwVersion=null;
        camera.FrameReceived+=frame=>
        {
            Assert.True(frame.TryDecodeGray(out var image,out var error),error);
            images.Add(frame.ImageId,(frame.Timestamp,image!.Pixels.ToArray()));
        };
        foreach(string line in File.ReadLines(path))
        {
            using var document=JsonDocument.Parse(line);var row=document.RootElement;
            if(row.GetProperty("outbound").GetBoolean())continue;
            string tag=row.GetProperty("tag").GetString()!;
            if(tag is not ("0xCD" or "0xF0" or "0xF2" or "0xED"))continue;
            string hex=row.GetProperty("payloadHex").GetString()!;
            if(tag=="0xF2")
            {
                uint id=BinaryPrimitives.ReadUInt32LittleEndian(Convert.FromHexString(hex[..18]).AsSpan(5));
                if(id is not (12622 or 12623))continue;
                camera.Handle(RobotMessage.Parse(Convert.FromHexString(hex)));
            }
            else if(tag=="0xED")bodyHwVersion=((ManufacturingID)RobotMessage.Parse(Convert.FromHexString(hex))).BodyHwVersion;
            else if(tag=="0xF0")states.Add((RobotState)RobotMessage.Parse(Convert.FromHexString(hex)));
            else if(RobotMessage.Parse(Convert.FromHexString(hex)) is NVOpResult nv&&nv.Tag==CameraCalibration.NvEntryTag&&nv.Result==0)calibration=nv.Data;
        }
        Assert.NotNull(calibration);Assert.NotNull(bodyHwVersion);Assert.Equal(2,images.Count);
        var frames=new Dictionary<uint,FrameInput>();
        foreach(var (id,image) in images)
        {
            var before=states.Last(s=>s.Timestamp<=image.Timestamp);
            var after=states.First(s=>s.Timestamp>=image.Timestamp);
            Assert.Equal(before.PoseFrameId,after.PoseFrameId);Assert.Equal(before.PoseOriginId,after.PoseOriginId);
            float fraction=before.Timestamp==after.Timestamp?0:(float)(image.Timestamp-before.Timestamp)/(after.Timestamp-before.Timestamp);
            float Blend(float a,float b)=>a+(b-a)*fraction; // native binary32 head/lift interpolation, 0x005306CA..0x005306E6.
            frames.Add(id,new(image.Pixels,Blend(before.HeadAngle,after.HeadAngle),Blend(before.LiftAngle,after.LiftAngle),
                Blend(before.Pose.X,after.Pose.X),Blend(before.Pose.Y,after.Pose.Y),Blend(before.Pose.Angle,after.Pose.Angle)));
            output.WriteLine($"image={id} robotTimestamp={image.Timestamp}; F0={before.Timestamp}..{after.Timestamp}; head={frames[id].Head}; image SHA256={Convert.ToHexString(SHA256.HashData(image.Pixels))}");
        }
        output.WriteLine($"Captured body hardware version={bodyHwVersion}; native <=6 distortion rule 0x0065AD54..0x0065AD9C applies normally");
        return new(calibration,bodyHwVersion.Value,frames);
    }
}
