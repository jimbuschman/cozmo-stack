using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Text.Json;
using Cozmo.Bridge;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Emulation;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class FaceEraseTests
{
    private static byte[] Enrollment()
    {
        var bytes=new List<byte>();
        bytes.AddRange(BitConverter.GetBytes(StoredEnrolledFace.VersionPrefix));
        bytes.AddRange(BitConverter.GetBytes(9));
        var erased=new StoredEnrolledFace {FaceID=7,Name="Drawn",Timestamp28Seconds=1,Timestamp30Seconds=1};
        erased.AlbumEntryIds.Add(3);erased.AlbumEntrySeconds.Add(1);erased.Pack(bytes);
        new StoredEnrolledFace {FaceID=8,Name="Survivor",Timestamp28Seconds=1,Timestamp30Seconds=1}.Pack(bytes);
        return bytes.ToArray();
    }

    private static PickupPyramidNativeExecutionTests.Fixture Fixture() => new(sourceNvEmpty:true,
        sourceNvStored: tag=>tag switch {VisionSystem.FaceAlbumNvTag=>FaceIdentityTests.Album(),
            VisionSystem.FaceEnrollmentNvTag=>Enrollment(),_=>null});

    [Fact]
    public void PublicEraseReportsBeforeNvRepliesAndRestoresRemainingIdentity()
    {
        if(ShippedLibraries.FindLibDir() is null)return;
        using var f=Fixture();
        var vision=f.M.Vision;
        var origin=f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId);
        var observed=new TrackedFace(new DetectedFace(7,new FaceRect(0,0,20,20),Name:"Drawn"),10)
        { HeadPose=new Pose3d(Rotation3d.Identity,new Vec3(100,0,50)) };
        Assert.NotNull(vision.Faces.AddOrUpdateNativeFace(observed,Pose3d.Identity,false,origin.Node!));
        var local=new List<NativeErasedEnrolledFace>();
        using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x57,true,value=>local.Add(Assert.IsType<NativeErasedEnrolledFace>(value)));
        int unrelated=0;
        vision.EnrolledFacesErasedInternal+=()=>unrelated++;
        using var enrollmentReport=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x56,true,_=>unrelated++);
        var app=new List<string>();var bodies=new List<byte[]>();
        f.Rig.Engine.EngineToGameMessage+=(tag,body)=>{if(tag==0x57){app.Add("raw");bodies.Add(body.ToArray());}};
        f.Rig.Engine.FaceErased+=report=>{app.Add("typed");Assert.Equal(new FaceErasedReport(7,""),report);};
        int mark=f.Rig.Mark(),seen=0;
        f.Rig.Robot.EraseEnrolledFaceById(7);f.Rig.Tick();
        Assert.Equal(new NativeErasedEnrolledFace(7,""),Assert.Single(local));
        Assert.False(f.Rig.Engine.NvStorage!.IsIdle);
        Assert.Equal("Survivor",Assert.Single(vision.Recognizer.EnrolledFaces).Name);
        Assert.Equal("Drawn",vision.Faces.GetFace(7)!.Name);
        Assert.Equal(0,unrelated);
        f.Rig.Engine.DrainAppDelivery();
        Assert.Equal(new[]{"raw","typed"},app);
        Assert.Equal(new byte[]{7,0,0,0,0},Assert.Single(bodies));
        var exported=vision.GetSerializedFaceData();
        var commands=new List<NVCommand>();
        for(int i=0;i<100 && !commands.Any(c=>c.Tag==VisionSystem.FaceEnrollmentNvTag && c.Op==NvStorageComponent.OpWrite);i++)
        {
            var sent=f.Rig.SentSince(mark);
            for(;seen<sent.Count;seen++)if(sent[seen] is NVCommand command)
            {commands.Add(command);f.Rig.Data(new NVOpResult{Tag=command.Tag,Op=command.Op,Result=0});}
            f.Rig.Tick();
        }
        var relevant=commands.Where(c=>c.Tag==VisionSystem.FaceAlbumNvTag||c.Tag==VisionSystem.FaceEnrollmentNvTag).ToArray();
        Assert.Equal(new[]{(VisionSystem.FaceAlbumNvTag,NvStorageComponent.OpErase),(VisionSystem.FaceAlbumNvTag,NvStorageComponent.OpWrite),
            (VisionSystem.FaceEnrollmentNvTag,NvStorageComponent.OpErase),(VisionSystem.FaceEnrollmentNvTag,NvStorageComponent.OpWrite)},relevant.Select(c=>(c.Tag,c.Op)));
        var album=relevant[1].Data.Skip(16).Take(exported.Album.Length).ToArray();
        var enrollment=relevant[3].Data.Skip(16).Take(exported.Enrollment.Length).ToArray();
        Assert.Equal(exported.Album,album);Assert.Equal(exported.Enrollment,enrollment);
        using var restored=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true,sourceNvStored:tag=>tag switch
        {VisionSystem.FaceAlbumNvTag=>album,VisionSystem.FaceEnrollmentNvTag=>enrollment,_=>null});
        var remaining=Assert.Single(restored.M.Vision.Recognizer.EnrolledFaces);
        Assert.Equal(8,remaining.FaceID);Assert.Equal("Survivor",remaining.Name);
    }

    [Fact]
    public async Task EraseThroughMcpPersistsDeletionOfARecordedEnrollment()
    {
        string? bundle = Environment.GetEnvironmentVariable("COZMO_FACE_REPLAY_BUNDLE");
        if (string.IsNullOrEmpty(bundle)) return;

        // The teach-face writes in 20261117-052405 are complete by 255799 ms
        // (FaceRecognitionRecordedReplayTests uses this same album window).
        var writes = new Dictionary<uint, byte[]>();
        foreach (string line in File.ReadLines(Path.Combine(bundle, "messages.jsonl")))
        {
            using var row = JsonDocument.Parse(line);
            var entry = row.RootElement;
            double at = entry.GetProperty("atMs").GetDouble();
            if (at > 256000) break;
            if (at < 255000 || !entry.GetProperty("outbound").GetBoolean() || entry.GetProperty("tag").GetString() != "0x81")
                continue;
            var payload = Convert.FromHexString(entry.GetProperty("payloadHex").GetString()!);
            if (RobotMessage.Parse(payload) is NVCommand { Op: NvStorageComponent.OpWrite } command)
                writes[command.Tag] = command.Data;
        }

        byte[] SavedPayload(uint tag)
        {
            var bytes = new List<byte>();
            for (uint next = tag; writes.TryGetValue(next, out var chunk); next += (uint)chunk.Length)
            {
                Assert.NotEmpty(chunk);
                bytes.AddRange(chunk);
            }
            byte[] raw = bytes.ToArray();
            Assert.True(raw.Length > NvStorageComponent.NvHeaderSize);
            Assert.Equal(NvStorageComponent.NonFactoryHeaderMagic, BinaryPrimitives.ReadUInt32LittleEndian(raw));
            int length = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(8)));
            Assert.Equal(NvStorageComponent.NvHeaderSize + length, raw.Length);
            return raw[NvStorageComponent.NvHeaderSize..];
        }

        byte[] album = SavedPayload(VisionSystem.FaceAlbumNvTag);
        byte[] enrollment = SavedPayload(VisionSystem.FaceEnrollmentNvTag);
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true, sourceNvStored: tag => tag switch
        {
            VisionSystem.FaceAlbumNvTag => album,
            VisionSystem.FaceEnrollmentNvTag => enrollment,
            _ => null,
        });
        var target = Assert.Single(f.M.Vision.Recognizer.EnrolledFaces);
        Assert.False(string.IsNullOrEmpty(target.Name));
        int faceId = target.FaceID;
        string originalName = target.Name;
        var reports = new ConcurrentQueue<byte[]>();
        var missing = new ConcurrentQueue<string>();
        f.Rig.Engine.StackLogLine += line =>
        {
            if (line.StartsWith("MISSING", StringComparison.Ordinal)) missing.Enqueue(line);
        };
        f.Rig.Engine.EngineToGameMessage += (tag, body) =>
        {
            if (tag == 0x57) reports.Enqueue(body.ToArray());
        };
        using var bridge = new CozmoBridge(f.Rig.Robot);
        await using var client = new McpServerTests.Client(bridge);
        await client.Initialize();
        int wire = f.Rig.Mark();
        int request = await client.Send("tools/call", new { name = "erase-face", arguments = new { faceId } });
        f.Rig.Tick();
        f.Rig.Engine.DrainAppDelivery();
        var reply = (await client.Receive(request)).GetProperty("result").GetProperty("structuredContent");
        var operation = reply.GetProperty("faceOperation");
        Assert.Equal("erased", operation.GetProperty("outcome").GetString());
        Assert.Equal(faceId, operation.GetProperty("faceId").GetInt32());
        Assert.Equal(originalName, operation.GetProperty("lastKnownName").GetString());
        byte[] report = Assert.Single(reports);
        Assert.Equal(5, report.Length);
        Assert.Equal(faceId, BinaryPrimitives.ReadInt32LittleEndian(report));
        Assert.Equal(0, report[4]); // 0x00657A98–0x00657ABE sends the empty native name.
        Assert.Empty(f.M.Vision.Recognizer.EnrolledFaces);

        // The report is sent after the save attempt, while the robot NV reply is
        // pending. The empty export's native queue erases the album then enrollment.
        var nv = f.Rig.Engine.NvStorage!;
        Assert.False(nv.IsIdle);
        for (int i = 0; i < 60 && !nv.IsIdle; i++)
        {
            f.AnswerInFlightNv();
            f.Rig.Tick();
        }
        Assert.True(nv.IsIdle);
        Assert.Equal(new[] { (VisionSystem.FaceAlbumNvTag, NvStorageComponent.OpErase),
            (VisionSystem.FaceEnrollmentNvTag, NvStorageComponent.OpErase) },
            f.Rig.SentSince(wire).OfType<NVCommand>()
                .Where(c => c.Tag is VisionSystem.FaceAlbumNvTag or VisionSystem.FaceEnrollmentNvTag)
                .Select(c => (c.Tag, c.Op)));
        Assert.DoesNotContain(missing, line => line.Contains("M14-011") || line.Contains("M14-012"));
    }

    [Fact]
    public void MissingSignedIdWarnsWithoutSavingOrReporting()
    {
        if(ShippedLibraries.FindLibDir() is null)return;
        using var f=Fixture();
        var logs=new List<EngineLogEntry>();f.Rig.Engine.LogEntry+=logs.Add;
        int local=0;using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x57,true,_=>local++);
        int mark=f.Rig.Mark();f.Rig.Robot.EraseEnrolledFaceById(-1);f.Rig.Tick();
        Assert.Contains(logs,e=>e.Name=="FaceRecognizer.EraseFace.NotFound" && e.Text.Contains("-1"));
        Assert.Equal(0,local);Assert.Equal(2,f.M.Vision.Recognizer.EnrolledFaces.Count);
        Assert.Empty(f.Rig.SentSince(mark).OfType<NVCommand>());
    }

    [Fact]
    public void ExistingUnnamedEnrollmentCanBeErased()
    {
        if(ShippedLibraries.FindLibDir() is null)return;
        using var f=Fixture();
        f.M.Vision.Recognizer.EnrolledFaces.Single(face=>face.FaceID==7).Name="";
        int local=0;using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x57,true,_=>local++);
        f.Rig.Robot.EraseEnrolledFaceById(7);f.Rig.Tick();
        Assert.Equal(1,local);
        Assert.Equal(8,Assert.Single(f.M.Vision.Recognizer.EnrolledFaces).FaceID);
    }

    [Fact]
    public void NoExternalInterfaceStillErasesAndSavesButDoesNotPublish()
    {
        if(ShippedLibraries.FindLibDir() is null)return;
        using var f=Fixture();f.Robot.HasExternalInterface=false;
        int local=0,host=0;using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x57,true,_=>local++);
        f.Rig.Engine.EngineToGameMessage+=(tag,_)=>{if(tag==0x57)host++;};
        f.Rig.Engine.FaceErased+=_=>host++;
        f.Rig.Robot.EraseEnrolledFaceById(7);f.Rig.Tick();f.Rig.Engine.DrainAppDelivery();
        Assert.Equal(8,Assert.Single(f.M.Vision.Recognizer.EnrolledFaces).FaceID);
        Assert.False(f.Rig.Engine.NvStorage!.IsIdle);Assert.Equal(0,local);Assert.Equal(0,host);
    }

    [Fact]
    public void NvFailureDoesNotWithdrawOrDuplicateEraseReport()
    {
        if(ShippedLibraries.FindLibDir() is null)return;
        using var f=Fixture();
        int reports=0;using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x57,true,_=>reports++);
        int mark=f.Rig.Mark();
        f.Rig.Robot.EraseEnrolledFaceById(7);f.Rig.Tick();
        Assert.Equal(1,reports);
        var command=Assert.Single(f.Rig.SentSince(mark).OfType<NVCommand>());
        f.Rig.Data(new NVOpResult {Tag=command.Tag,Op=command.Op,Result=-1});
        for(int i=0;i<5;i++)f.Rig.Tick();
        Assert.Equal(1,reports);
        Assert.Equal(8,Assert.Single(f.M.Vision.Recognizer.EnrolledFaces).FaceID);
    }

    // M14-012 G1: G2E 0x8A (subscribed 0x006505B4..0x006505BC, handler 0x00658076) -> EraseAllFaces 0x006514FC: the
    // recognizer erases every entry, SaveFaceAlbumToRobot takes the both-empty branch (erase 0x184000 0x00657474, then
    // 0x183000 only if admitted 0x006575E6..0x00657600), and one RobotErasedAllEnrolledFaces 0x58 goes out before any NV reply.
    // reach: production (public API -> G2E dispatch -> handler; NV replies through the fake robot's packets).
    [Fact]
    public void PublicEraseAllErasesBothTagsAndReportsOnceBeforeNvReplies()
    {
        if(ShippedLibraries.FindLibDir() is null)return;
        using var f=Fixture();
        var vision=f.M.Vision;
        Assert.Equal(2,vision.Recognizer.EnrolledFaces.Count);
        int local=0,loaded=0,host=0;var raw=new List<(ushort Tag,byte[] Body)>();
        using var erased=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x58,true,_=>local++);
        using var known=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x1C,true,_=>loaded++);
        f.Rig.Engine.EngineToGameMessage+=(tag,body)=>{if(tag is 0x58 or 0x1C)raw.Add((tag,body.ToArray()));};
        vision.EnrolledFacesErased+=()=>host++;
        int mark=f.Rig.Mark(),seen=0;
        f.Rig.Robot.EraseAllEnrolledFaces();f.Rig.Tick();
        Assert.Equal(1,local);Assert.Equal(0,loaded);
        Assert.False(f.Rig.Engine.NvStorage!.IsIdle);                 // 0x58 did not wait for the NV replies
        Assert.Empty(vision.Recognizer.EnrolledFaces);
        var exported=vision.GetSerializedFaceData();
        Assert.Empty(exported.Album);Assert.Empty(exported.Enrollment);
        f.Rig.Engine.DrainAppDelivery();
        Assert.Equal(1,host);
        var only=Assert.Single(raw);Assert.Equal((ushort)0x58,only.Tag);Assert.Empty(only.Body);
        var commands=new List<NVCommand>();
        for(int i=0;i<40 && !commands.Any(c=>c.Tag==VisionSystem.FaceEnrollmentNvTag);i++)
        {
            var sent=f.Rig.SentSince(mark);
            for(;seen<sent.Count;seen++)if(sent[seen] is NVCommand command)
            {commands.Add(command);f.Rig.Data(new NVOpResult{Tag=command.Tag,Op=command.Op,Result=0});}
            f.Rig.Tick();
        }
        var relevant=commands.Where(c=>c.Tag==VisionSystem.FaceAlbumNvTag||c.Tag==VisionSystem.FaceEnrollmentNvTag).Select(c=>(c.Tag,c.Op)).ToArray();
        Assert.Equal(new[]{(VisionSystem.FaceAlbumNvTag,NvStorageComponent.OpErase),(VisionSystem.FaceEnrollmentNvTag,NvStorageComponent.OpErase)},relevant);
        Assert.Equal(1,local);
    }

    // M14-012 G1 with the face library's init failed (StockStatus 2, the first create call refused): EraseAllFaces 0x006514FC has no availability
    // check, so the recognizer erase fails (FaceLibClearAlbumFailed), the save cannot export (GetSerializedFaceDataFailed, no NV traffic,
    // 0x00657168..0x006571D8) and one 0x58 is still broadcast (0x00651538).
    // reach: production (public API, G2E dispatch, the handler); the failed library init is a substituted create result.
    [Fact]
    public void EraseAllWithTheFaceLibraryFailedStillReportsOnceWithoutNv()
    {
        if(ShippedLibraries.FindLibDir() is null)return;
        bool oldError=Cozmo.Transport.EngineErrorState.ErrorFlagSet;
        try
        {
            using var backup=new BackupTestResources();
            using var rig=new M4ControlTests.Rig(engineOptions:backup.Options());
            rig.Robot.Vision.StockFaceCreateOverrideForTest=name=>name=="OKAO_FR_CreateAlbumHandle"?0u:null;
            var names=new List<string>();rig.Engine.LogEntry+=entry=>names.Add(entry.Name??"");
            rig.ToSynced();
            var state=rig.Robot.Vision.NativeInitializationForTest!;
            Assert.Equal(2,state.StockFace!.StockStatus);
            int erased=0,loaded=0;
            using var a=rig.Engine.AnimationMessages.SubscribeMessage(0x58,true,_=>erased++);
            using var b=rig.Engine.AnimationMessages.SubscribeMessage(0x1C,true,_=>loaded++);
            names.Clear();int mark=rig.Mark();
            rig.Robot.EraseAllEnrolledFaces();rig.Tick();rig.Tick();
            Assert.Equal(1,erased);Assert.Equal(0,loaded);
            Assert.Contains("FaceRecognizer.EraseAllFaces.FaceLibClearAlbumFailed",names);
            Assert.Contains("VisionComponent.SaveFaceAlbumToRobot.GetSerializedFaceDataFailed",names);
            Assert.DoesNotContain(rig.SentSince(mark).OfType<NVCommand>(),c=>c.Tag==VisionSystem.FaceAlbumNvTag||c.Tag==VisionSystem.FaceEnrollmentNvTag);
        }
        finally { Cozmo.Transport.EngineErrorState.ErrorFlagSet=oldError; }
    }

    // M14-012 G1: the NV wrapper 0x0065A478 broadcasts EngineErrorCode 3 (0x0065A4C4) on a nonzero erase result; the
    // enrollment erase was already admitted, so it still goes out (0x006575E6..0x00657600).
    // reach: production (public API; the failed reply is a fake robot packet).
    [Fact]
    public void EraseAllNvFailureReportsEngineErrorCodeThree()
    {
        if(ShippedLibraries.FindLibDir() is null)return;
        bool previous=Cozmo.Transport.EngineErrorState.ErrorFlagSet;
        try
        {
            using var f=Fixture();
            var errors=new List<object?>();
            using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x7C,true,errors.Add);
            int mark=f.Rig.Mark();
            f.Rig.Robot.EraseAllEnrolledFaces();f.Rig.Tick();
            var first=Assert.Single(f.Rig.SentSince(mark).OfType<NVCommand>());
            Assert.Equal((VisionSystem.FaceAlbumNvTag,NvStorageComponent.OpErase),(first.Tag,first.Op));
            int after=f.Rig.Mark();
            f.Rig.Data(new NVOpResult{Tag=first.Tag,Op=first.Op,Result=-1});
            for(int i=0;i<5;i++)f.Rig.Tick();
            Assert.Equal((byte)3,Assert.IsType<EngineErrorCodeReport>(Assert.Single(errors)).Code);
            Assert.Contains(f.Rig.SentSince(after).OfType<NVCommand>(),c=>c.Tag==VisionSystem.FaceEnrollmentNvTag && c.Op==NvStorageComponent.OpErase);
        }
        finally{Cozmo.Transport.EngineErrorState.ErrorFlagSet=previous;}
    }

    // M14-012 G1: 0x0065019E subscribes 0x8A only when the external interface exists.
    // reach: production (public API with the external interface absent).
    [Fact]
    public void EraseAllWithoutExternalInterfaceDoesNothing()
    {
        if(ShippedLibraries.FindLibDir() is null)return;
        using var f=Fixture();f.Robot.HasExternalInterface=false;
        int local=0;using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x58,true,_=>local++);
        int mark=f.Rig.Mark();
        f.Rig.Robot.EraseAllEnrolledFaces();f.Rig.Tick();
        Assert.Equal(0,local);Assert.Equal(2,f.M.Vision.Recognizer.EnrolledFaces.Count);
        Assert.Empty(f.Rig.SentSince(mark).OfType<NVCommand>());
    }

    [Fact]
    public void QueuedEraseCannotReachReplacementOwner()
    {
        using var f=Fixture();
        int reports=0;using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x57,true,_=>reports++);
        using(f.Rig.Engine.EnterSynchronousOwner())
        {
            f.Rig.Robot.EraseEnrolledFaceById(7);
            f.Rig.Engine.Robots.RemoveRobot(1,false);f.Rig.Engine.Robots.AddRobot(1,withRic:false);
        }
        f.Rig.Tick();Assert.Equal(0,reports);
    }
}
