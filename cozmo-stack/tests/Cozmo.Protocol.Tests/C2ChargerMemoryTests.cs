using System.Text.Json;
using Cozmo.Bridge;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Xunit;
using Fixture = Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class C2ChargerMemoryTests
{
    private static CozmoEngineOptions Settings(bool enabled) => new()
    {
        ResourcesPath=PickupPyramidNativeExecutionTests.Resources(),
        BlockPoolPath="", C2=new CozmoC2Settings { ChargerMemory=enabled }
    };
    private static JsonElement Data(JsonElement reply) => reply.GetProperty("result").GetProperty("structuredContent");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MapPickupKeepsLandmarksInTheirFrameUntilChargerIsSeenAgain(bool enabled)
    {
        using var f = new Fixture(sourceNvEmpty:true,engineOptions:new CozmoEngineOptions {
            ResourcesPath=PickupPyramidNativeExecutionTests.Resources(), BlockPoolPath="",
            C2=new CozmoC2Settings { MapReanchor=enabled } });
        using var bridge = new CozmoBridge(f.Rig.Robot);
        await using var client = new McpServerTests.Client(bridge);
        await client.Initialize();
        uint original = f.Robot.CurrentOriginId, frame = 500;
        void See(ObjectType type, float x)
        {
            for (int i=0;i<4;i++)
            {
                f.Motors(); f.Image(++frame,cube:true,cubeX:x,markerObjectType:type);
            }
        }
        See(ObjectType.Block_LIGHTCUBE1, 130);
        See(ObjectType.Charger_Basic, 160);
        f.Robot.Delocalize(false); f.Motors();
        var before = Data(await client.Tool("get_map", new { })).GetProperty("roomMaps");
        if (!enabled) { Assert.Equal(JsonValueKind.Null, before.ValueKind); return; }
        var archived = Assert.Single(before.EnumerateArray(), l => l.GetProperty("origin").GetProperty("id").GetUInt32()==original);
        Assert.False(archived.GetProperty("anchored").GetBoolean());
        Assert.Equal(JsonValueKind.Null, archived.GetProperty("toCurrent").ValueKind);
        Assert.True(archived.GetProperty("landmarks").GetArrayLength() >= 2);
        See(ObjectType.Charger_Basic, 195);
        var map = Data(await client.Tool("get_map", new { }));
        var rejoined = Assert.Single(map.GetProperty("roomMaps").EnumerateArray(), l => l.GetProperty("origin").GetProperty("id").GetUInt32()==original);
        Assert.True(rejoined.GetProperty("anchored").GetBoolean());
        Assert.Equal(JsonValueKind.String, rejoined.GetProperty("rejoinedAt").ValueKind);
        var transform = rejoined.GetProperty("toCurrent");
        var cube = Assert.Single(map.GetProperty("objects").EnumerateArray(),
            item => item.GetProperty("type").GetString()==ObjectType.Block_LIGHTCUBE1.ToString());
        Assert.True(cube.GetProperty("memoryOnly").GetBoolean());
        var heading = transform.GetProperty("headingRad").GetDouble();
        var cubeId = cube.GetProperty("worldObjectId").GetUInt32();
        var local = Assert.Single(archived.GetProperty("landmarks").EnumerateArray(),
            item => item.GetProperty("worldObjectId").GetUInt32() == cubeId).GetProperty("pose");
        double x = local.GetProperty("xMm").GetDouble(), y = local.GetProperty("yMm").GetDouble();
        Assert.Equal(transform.GetProperty("xMm").GetDouble() + Math.Cos(heading)*x - Math.Sin(heading)*y,
            cube.GetProperty("xMm").GetDouble(), 5);
        Assert.Equal(transform.GetProperty("yMm").GetDouble() + Math.Sin(heading)*x + Math.Cos(heading)*y,
            cube.GetProperty("yMm").GetDouble(), 5);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeMarkerCommitRemembersChargerAfterNativeDeletionAndPickup(bool enabled)
    {
        using var f=new Fixture(sourceNvEmpty:true,engineOptions:Settings(enabled));
        using var bridge=new CozmoBridge(f.Rig.Robot);
        await using var client=new McpServerTests.Client(bridge);
        await client.Initialize();
        uint frame=1000;
        for(int i=0;i<8;i++)
        {
            f.Motors();
            f.Image(++frame,cube:true,cubeX:130+i*0.5f,markerObjectType:ObjectType.Charger_Basic);
            if (f.M.World.LocatedObjects.Any(o=>o.Type==ObjectType.Charger_Basic) &&
                (!enabled || f.Rig.Engine.C2ChargerMemory.Get(f.Robot) is { Sightings: >= 2 })) break;
        }
        var charger=Assert.Single(f.M.World.LocatedObjects.Where(o=>o.Type==ObjectType.Charger_Basic && o.OriginId==f.Robot.CurrentOriginId));
        var state=Data(await client.Tool("get_state",new { }));
        Assert.Single(state.GetProperty("knownChargers").EnumerateArray());
        if(!enabled)
        {
            Assert.Equal(JsonValueKind.Null,state.GetProperty("chargerMemory").ValueKind);
            Assert.DoesNotContain(CozmoC2Settings.ChargerMemoryName,state.GetProperty("c2Extensions").EnumerateArray().Select(e=>e.GetString()));
            Assert.Null(f.Rig.Engine.C2ChargerMemory.Get(f.Robot));
            return;
        }
        Assert.Contains(CozmoC2Settings.ChargerMemoryName,state.GetProperty("c2Extensions").EnumerateArray().Select(e=>e.GetString()));
        var fused=state.GetProperty("chargerMemory");
        Assert.True(fused.GetProperty("anchored").GetBoolean());
        long count=fused.GetProperty("sightings").GetInt64();
        Assert.True(count>=2);
        Assert.Equal(0,fused.GetProperty("zMm").GetDouble());
        Assert.Equal(0,fused.GetProperty("pitchDeg").GetDouble());
        Assert.Equal(0,fused.GetProperty("rollDeg").GetDouble());
        uint original=f.Robot.CurrentOriginId;
        Assert.Equal(original,fused.GetProperty("originId").GetUInt32());
        var map=Data(await client.Tool("get_map",new { }));
        var mapped=Assert.Single(map.GetProperty("objects").EnumerateArray(),e=>e.GetProperty("charger").GetBoolean());
        Assert.Equal(fused.GetProperty("xMm").GetDouble(),mapped.GetProperty("xMm").GetDouble());
        Assert.Equal(count,mapped.GetProperty("fusedCharger").GetProperty("sightings").GetInt64());
        Assert.False(mapped.GetProperty("memoryOnly").GetBoolean());

        // Native delete path deliberately exercised rather than retaining a fake located object.
        var filter=new BlockWorldFilter(); filter.AllowedIds.Add(charger.ObjectId);
        f.M.World.DeleteLocatedObjects(filter);
        state=Data(await client.Tool("get_state",new { }));
        Assert.Empty(state.GetProperty("knownChargers").EnumerateArray());
        Assert.Equal(count,state.GetProperty("chargerMemory").GetProperty("sightings").GetInt64());
        map=Data(await client.Tool("get_map",new { }));
        Assert.True(Assert.Single(map.GetProperty("objects").EnumerateArray(),e=>e.GetProperty("charger").GetBoolean()).GetProperty("memoryOnly").GetBoolean());

        // A pickup changes origins (0x00510D5A). Old coordinates are not treated as current.
        f.Robot.Delocalize(false); f.Motors();
        Assert.NotEqual(original,f.Robot.CurrentOriginId);
        state=Data(await client.Tool("get_state",new { }));
        Assert.False(state.GetProperty("chargerMemory").GetProperty("anchored").GetBoolean());
        Assert.Equal(count,state.GetProperty("chargerMemory").GetProperty("sightings").GetInt64());
        Assert.Null(f.Rig.Engine.C2ChargerMemory.CopyAnchoredPose(f.Robot));
        map=Data(await client.Tool("get_map",new { }));
        Assert.DoesNotContain(map.GetProperty("objects").EnumerateArray(),e=>e.GetProperty("charger").GetBoolean());
        Assert.False(map.GetProperty("chargerMemory").GetProperty("anchored").GetBoolean());

        for(int i=0;i<8;i++)
        {
            f.Motors(); f.Image(++frame,cube:true,cubeX:160,markerObjectType:ObjectType.Charger_Basic);
            if(f.Rig.Engine.C2ChargerMemory.Get(f.Robot) is { Anchored:true }) break;
        }
        var current=Assert.IsType<C2ChargerEstimate>(f.Rig.Engine.C2ChargerMemory.Get(f.Robot));
        Assert.True(current.Anchored);
        Assert.Equal(count,current.UnanchoredSightings); // unrelated roots remain separate
        Assert.True(current.Sightings>=1);
        using var target=f.Rig.Engine.C2ChargerMemory.CopyAnchoredPose(f.Robot);
        Assert.NotNull(target);
        Assert.True(PoseTreeNode.HasSameRoot(target.Node,f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId).Node));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RememberedCliffBlocksPlannerAfterNativeExpiryAndRejoinsWithTheCharger(bool enabled)
    {
        using var f = new Fixture(sourceNvEmpty:true,engineOptions:new CozmoEngineOptions {
            ResourcesPath=PickupPyramidNativeExecutionTests.Resources(), BlockPoolPath="",
            C2=new CozmoC2Settings { MapReanchor=true, ObstacleMemory=enabled } });
        using var bridge=new CozmoBridge(f.Rig.Robot);
        await using var client=new McpServerTests.Client(bridge);
        await client.Initialize();
        uint frame=800;
        void SeeCharger(float x)
        {
            for(int i=0;i<4;i++) { f.Motors(); f.Image(++frame,cube:true,cubeX:x,markerObjectType:ObjectType.Charger_Basic); }
        }
        SeeCharger(150);
        var world=f.M.World;
        var map=world.Maps.Current!;
        Vec2[] footprint=[new(300,0),new(320,0),new(320,20),new(300,20)];
        map.InsertCliff(footprint,new CliffMapData(new(1,0),100));
        map.TransformAging(SourceMapAging.At(31000));
        Assert.False(map.HasContentType(MemoryMapContentType.Cliff));
        Assert.Equal(enabled,map.HasCollisionRayWithTypes(new(280,10),new(340,10),t=>t==MemoryMapContentType.Cliff));
        var obb=Path.GetFullPath(Path.Combine(PickupPyramidNativeExecutionTests.Resources(),"..",".."));
        var env=new Cozmo.Robot.Manipulation.LatticeEnvironment(Cozmo.Robot.Manipulation.MotionPrimitiveSet.FromObb(obb)!);
        Assert.True(env.ImportBlockWorldObstaclesWithCarrying(world, () => Cozmo.Robot.Manipulation.LatticeCarryingState.FromRaw(null), false));
        Assert.Equal(enabled,env.IsInCollision(310,10,0));
        if (!enabled) return;
        f.Robot.Delocalize(false); f.Motors();
        async Task<JsonElement> Cliff() => Assert.Single(Data(await client.Tool("get_map",new { }))
            .GetProperty("rememberedHazards").EnumerateArray(), h=>h.GetProperty("localPolygon")[0].GetProperty("x").GetDouble()==300);
        Assert.Equal(JsonValueKind.Null,(await Cliff()).GetProperty("currentPolygon").ValueKind);
        SeeCharger(190);
        var entry=await Cliff();
        Assert.True(entry.GetProperty("ageMs").GetInt64()>=0);
        var pt=entry.GetProperty("currentPolygon")[0];
        Assert.True(env.ImportBlockWorldObstaclesWithCarrying(world, () => Cozmo.Robot.Manipulation.LatticeCarryingState.FromRaw(null), false));
        Assert.True(env.IsInCollision(pt.GetProperty("x").GetSingle(),pt.GetProperty("y").GetSingle(),0));
    }

    [Fact]
    public async Task RestartLoadsOnlyTheIdentifiedRobotsMapAndRequiresFreshLandmark()
    {
        var directory = Directory.CreateTempSubdirectory("cozmo-map-");
        try
        {
            Fixture NewRobot(bool enabled=true) => new(sourceNvEmpty:true,engineOptions:new CozmoEngineOptions
            {
                ResourcesPath=PickupPyramidNativeExecutionTests.Resources(), BlockPoolPath="", FilesPath=directory.FullName,
                C2=new CozmoC2Settings { MapReanchor=true, ObstacleMemory=true, MapSave=enabled }
            });
            void See(Fixture f, ObjectType type, uint firstFrame, float x)
            {
                for(uint i=0;i<4;i++) { f.Motors(); f.Image(firstFrame+i,cube:true,cubeX:x,markerObjectType:type); }
            }
            using (var first=NewRobot())
            using (first.Rig.Robot.OpenRoomMapSession())
            {
                See(first,ObjectType.Charger_Basic,700,150);
                See(first,ObjectType.Block_LIGHTCUBE1,704,130);
                first.M.World.Maps.Current!.InsertCliff([new(300,0),new(320,0),new(320,20),new(300,20)],
                    new CliffMapData(new(1,0),100));
            }
            Assert.Single(Directory.GetFiles(directory.FullName,"*.json",SearchOption.AllDirectories),
                file=>Path.GetFileName(file)=="00000002.json");
            using (var different=NewRobot())
            {
                different.Rig.Engine.RaiseSerialNumberAcquired(3);
                using var otherSession=different.Rig.Robot.OpenRoomMapSession();
                Assert.Empty(different.Rig.Engine.RoomMap.Snapshot(different.Robot));
            }
            using (var baseline=NewRobot(false)) Assert.Null(baseline.Rig.Robot.OpenRoomMapSession());
            using var second=NewRobot();
            using var session=second.Rig.Robot.OpenRoomMapSession();
            using var bridge=new CozmoBridge(second.Rig.Robot);
            await using var client=new McpServerTests.Client(bridge);
            await client.Initialize();
            async Task<JsonElement> ReadMap() => Data(await client.Tool("get_map",new { }));
            var before=await ReadMap();
            var old=Assert.Single(before.GetProperty("roomMaps").EnumerateArray());
            Assert.False(old.GetProperty("anchored").GetBoolean());
            Assert.Equal(2,old.GetProperty("landmarks").GetArrayLength());
            Assert.Equal(JsonValueKind.Null,before.GetProperty("rememberedHazards")[0].GetProperty("currentPolygon").ValueKind);
            See(second,ObjectType.Charger_Basic,800,195);
            var after=await ReadMap();
            Assert.All(after.GetProperty("roomMaps").EnumerateArray(), l=>Assert.True(l.GetProperty("anchored").GetBoolean()));
            Assert.Contains(after.GetProperty("objects").EnumerateArray(),o=>o.GetProperty("memoryOnly").GetBoolean() &&
                o.GetProperty("type").GetString()==ObjectType.Block_LIGHTCUBE1.ToString());
            Assert.Equal(JsonValueKind.Array,after.GetProperty("rememberedHazards")[0].GetProperty("currentPolygon").ValueKind);
        }
        finally { directory.Delete(true); }
    }

    [Fact]
    public void NativeCrossOriginLocalizationMergesChargerSightingsBeforeFlatten()
    {
        using var f=new Fixture(sourceNvEmpty:true,engineOptions:Settings(true));
        uint image=2000;
        for(int i=0;i<5;i++){ f.Motors(); f.Image(++image,cube:true,cubeX:130,markerObjectType:ObjectType.Charger_Basic); }
        // Native localization uses a connected cube. Charger localization itself
        // is not forced by the extension; it retains the actual engine gate.
        M4ControlTests.ConnectCube(f.Rig,slot:0);
        f.Image(++image,cube:true,cubeX:150); f.Motors();
        typeof(CozmoEngine).GetProperty("IsOfflineEngine",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!
            .SetValue(f.Robot.Engine,false);
        f.Image(++image,cube:true,cubeX:150); f.Motors();
        uint? cubeId=f.M.World.ConnectedObjectIdForActiveId(0);
        Assert.NotNull(cubeId);
        Assert.Equal(cubeId,f.M.World.LocalizedToObjectId);
        uint o1=f.Robot.CurrentOriginId;
        var remembered=Assert.IsType<C2ChargerEstimate>(f.Robot.Engine.C2ChargerMemory.Get(f.Robot));
        Assert.True(remembered.Anchored);
        Assert.True(remembered.Sightings>=2);
        f.Robot.Delocalize(false); f.Motors();
        uint o2=f.Robot.CurrentOriginId;
        Assert.NotEqual(o1,o2);
        Assert.False(f.Robot.Engine.C2ChargerMemory.Get(f.Robot)!.Anchored);
        // One accepted charger observation in the new coordinate root.
        for(int i=0;i<3&&f.Robot.Engine.C2ChargerMemory.Get(f.Robot) is not { Anchored:true };i++)
        {
            f.Image(++image,cube:true,cubeX:130,markerObjectType:ObjectType.Charger_Basic);
            f.Motors();
        }
        var current=Assert.IsType<C2ChargerEstimate>(f.Robot.Engine.C2ChargerMemory.Get(f.Robot));
        Assert.True(current.Anchored);
        Assert.Equal(o2,current.OriginId);
        Assert.Equal(remembered.Sightings,current.UnanchoredSightings);
        for(int i=0;i<6&&f.Robot.CurrentOriginId==o2;i++)
        {
            f.Image(++image,cube:true,cubeX:150); f.Motors();
        }
        Assert.Equal(o1,f.Robot.CurrentOriginId);
        var joined=Assert.IsType<C2ChargerEstimate>(f.Robot.Engine.C2ChargerMemory.Get(f.Robot));
        Assert.True(joined.Anchored);
        Assert.Equal(o1,joined.OriginId);
        Assert.Equal(remembered.Sightings+current.Sightings,joined.Sightings);
        Assert.Equal(0,joined.UnanchoredSightings);
    }

    [Fact]
    public void WeightingUsesObservationTimeRangeAndIncidenceWithCircularYawAndNativeOriginTransform()
    {
        using var f=new Fixture(sourceNvEmpty:true,engineOptions:Settings(true));
        using var lease=f.Rig.Engine.EnterSynchronousOwner();
        using var poses=f.M.World.EnterPoseMutation();
        var owner=f.Robot;
        var store=f.Rig.Engine.C2ChargerMemory;
        uint firstOrigin=owner.CurrentOriginId;
        double firstWeight=Add(1,100,0,Math.PI-.02,100,0);
        double secondWeight=Add(2,140,40,-Math.PI+.02,200,Math.PI/3);
        Assert.Equal(firstWeight/16,secondWeight,8);
        var estimate=Assert.IsType<C2ChargerEstimate>(store.Get(owner));
        Assert.Equal(2,estimate.Sightings);
        Assert.Equal((100*firstWeight+140*secondWeight)/(firstWeight+secondWeight),estimate.XMm,3);
        Assert.Equal((40*secondWeight)/(firstWeight+secondWeight),estimate.YMm,3);
        Assert.True(Math.Abs(estimate.HeadingRad)>3.1); // no averaging +/-pi to zero
        Assert.Equal(200,estimate.LastDistanceMm,3);
        Assert.Equal(60,estimate.LastViewingAngleDeg,3);
        Assert.Equal(0,estimate.ZMm);
        Assert.Equal(0,estimate.PitchDeg);
        Assert.Equal(0,estimate.RollDeg);
        Add(2,999,999,0,1,0); // duplicate timestamp rejected
        Assert.Equal(estimate,store.Get(owner));

        owner.Delocalize(false);
        uint secondOrigin=owner.CurrentOriginId;
        Assert.False(store.Get(owner)!.Anchored);
        double newWeight=Add(3,20,30,0,100,0);
        Assert.Equal(2,store.Get(owner)!.UnanchoredSightings);
        // Explicit checked native root relationship (the production hook is after Rejigger).
        Assert.Equal(0u,owner.RejiggerOrigins(owner.GetNativeOrigin(firstOrigin),
            new Transform3d(Rotation3d.FromAngleAxis(MathF.PI/2,new Vec3(0,0,1)),new Vec3(300,200,8))));
        using var relation=new Pose3d();
        Assert.True(Pose3d.GetWithRespectTo(owner.GetNativeOrigin(secondOrigin),owner.GetNativeOrigin(firstOrigin),relation));
        store.Reanchor(owner,secondOrigin,firstOrigin,relation);
        var merged=Assert.IsType<C2ChargerEstimate>(store.Get(owner));
        Assert.True(merged.Anchored);
        Assert.Equal(3,merged.Sightings);
        Assert.Equal(0,merged.UnanchoredSightings);
        var point=relation.Rotation*new Vec3(20,30,0)+relation.Translation;
        Assert.Equal((100*firstWeight+140*secondWeight+point.X*newWeight)/(firstWeight+secondWeight+newWeight),merged.XMm,3);
        Assert.Equal((40*secondWeight+point.Y*newWeight)/(firstWeight+secondWeight+newWeight),merged.YMm,3);
        Assert.Equal(0,merged.ZMm);

        double Add(uint stamp,double x,double y,double heading,double distance,double angle)
        {
            var rotation=Rotation3d.FromAngleAxis((float)heading,new Vec3(0,0,1))*
                Rotation3d.FromAngleAxis(.18f,new Vec3(0,1,0))*
                Rotation3d.FromAngleAxis(.1f,new Vec3(1,0,0));
            var origin=owner.GetNativeOrigin(owner.CurrentOriginId);
            var position=new Vec3(x,y,12);
            using var observedPose=new Pose3d(rotation,position,parent:origin);
            var charger=new ObservableObject(13,ObjectType.Charger_Basic,ChargerGeometry.Markers)
            { OriginId=owner.CurrentOriginId,LastObservedTimestamp=stamp,Pose=observedPose };
            var robotPos=position+rotation.Rotate(new Vec3(-distance*Math.Cos(angle),distance*Math.Sin(angle),0));
            using var robotAtSighting=new Pose3d(Rotation3d.Identity,robotPos,parent:origin);
            store.Observe(owner,charger,13,robotAtSighting);
            return Math.Pow(Math.Max(.05,Math.Cos(angle)),2)/(distance*distance);
        }
    }
}
