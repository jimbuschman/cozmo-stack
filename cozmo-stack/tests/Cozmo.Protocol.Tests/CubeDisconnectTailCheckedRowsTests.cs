using System.Reflection;
using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Cozmo.Transport;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("ObjectID process statics")]
public sealed class CubeDisconnectTailCheckedRowsTests
{
    // Explicit connected-map input: known ObjectID differs from radio slot 2.
    private static void ConnectedInput(CozmoRobot robot, uint id=0x11223344)
    {
        var obj=new ObservableObject(id,ObjectType.Block_LIGHTCUBE1,Array.Empty<KnownMarker>()) { ActiveId=2,FactoryId=0xAB };
        var map=(SortedDictionary<(int,int,int),ObservableObject>)typeof(BlockWorld).GetField("_connectedObjects",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(robot.LocalizationWorld)!;
        map.Add(((int)obj.Family,(int)obj.Type,unchecked((int)id)),obj);
    }

    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void BodyAndUnionKeepRawByteAndWorldID()
    {
        var m=new ObjectConnectionStateMessage(0x11223344,0xAABBCCDD,(ObjectType)(-1),0xFE);
        Assert.Equal(new byte[]{0x44,0x33,0x22,0x11,0xDD,0xCC,0xBB,0xAA,0xFF,0xFF,0xFF,0xFF,0xFE},m.PackBody());
        Assert.Equal(new byte[]{0x0D,0,0x44,0x33,0x22,0x11,0xDD,0xCC,0xBB,0xAA,0xFF,0xFF,0xFF,0xFF,0xFE},m.ToUnionBytes());
    }

    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void LiveUnknownRemovalSkipsPoolAndGameButStillLogsRecvd()
    {
        using var robot=CozmoRobot.CreateOffline();
        // Native pool state2, matching factory0; the world has no active slot2.
        robot.Cubes.Connections.OnConnectionState(2,0,true,ObjectType.Block_LIGHTCUBE1);
        var logs=new List<string?>();var reports=new List<ObjectConnectionStateMessage>();
        robot.Engine.LogEntry+=e=>logs.Add(e.Name);
        robot.Cubes.ConnectionStateBroadcast+=reports.Add;
        using var sub=robot.Engine.AnimationMessages.SubscribeMessage(0x0D,true,_=>Assert.Fail("-1 must not broadcast"));
        robot.Engine.Broadcast(new ObjectConnectionState{ObjectID=2,FactoryID=0,ObjectType=ObjectType.Block_LIGHTCUBE1,Connected=false});
        Assert.Equal(ActiveObjectSlotState.Connected,robot.Cubes.Connections.Slots[2].State);
        Assert.Contains("Robot.HandleActiveObjectConnectionState.Recvd",logs);
        Assert.DoesNotContain("Robot.HandleDisconnectedFromObject",logs);
        Assert.Empty(reports);
    }

    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void LiveValidRemovalOrdersEventPoolRecvdAndGameEvenOnFactoryMismatch()
    {
        using var robot=CozmoRobot.CreateOffline();ConnectedInput(robot);
        var order=new List<string>();var reports=new List<ObjectConnectionStateMessage>();
        robot.Engine.LogEntry+=e=> {if(e.Name is "robot.accessory_connection" or "Robot.HandleDisconnectedFromObject" or "Robot.HandleActiveObjectConnectionState.Recvd")order.Add(e.Name);};
        using var sub=robot.Engine.AnimationMessages.SubscribeMessage(0x0D,true,p=>{
            Assert.Empty(robot.LocalizationWorld.ConnectedObjects);
            reports.Add(Assert.IsType<ObjectConnectionStateMessage>(p));order.Add("game");
        });
        robot.Engine.Broadcast(new ObjectConnectionState{ObjectID=2,FactoryID=0xAD,ObjectType=ObjectType.Block_LIGHTCUBE3,Connected=false});
        Assert.Equal(new[]{"robot.accessory_connection","Robot.HandleDisconnectedFromObject","Robot.HandleActiveObjectConnectionState.Recvd","game"},order);
        Assert.Equal(new ObjectConnectionStateMessage(0x11223344,0xAD,ObjectType.Block_LIGHTCUBE3,0),Assert.Single(reports));
        Assert.Equal(ActiveObjectSlotState.Empty,robot.Cubes.Connections.Slots[2].State);
    }

    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Theory]
    [InlineData(5u)] [InlineData(uint.MaxValue)]
    public void InvalidActiveSlotHasNoSourceTail(uint slot)
    {
        using var robot=CozmoRobot.CreateOffline();var logs=new List<string?>();
        robot.Engine.LogEntry+=e=>logs.Add(e.Name);
        robot.Cubes.ConnectionStateBroadcast+=_=>Assert.Fail("out-of-range slot");
        robot.Engine.Broadcast(new ObjectConnectionState{ObjectID=slot,FactoryID=0xAB,ObjectType=ObjectType.Block_LIGHTCUBE1});
        Assert.Empty(logs);Assert.Empty(robot.LocalizationWorld.ConnectedObjects);
    }

    [Theory]
    [InlineData(2)] [InlineData(255)]
    // reach: unit (direct native-field handler input, bypassing wire bool normalization and receive queue; explicit app drain).
    public void NativeHandlerKeepsItsByteInRecvdAndGame(int raw)
    {
        using var robot=CozmoRobot.CreateOffline();ConnectedInput(robot);
        var logs=new List<string>();ObjectConnectionStateMessage? report=null;
        robot.Engine.LogEntry+=e=>{if(e.Name=="Robot.HandleActiveObjectConnectionState.Recvd")logs.Add(e.Text);};
        robot.Cubes.ConnectionStateBroadcast+=m=>report=m;
        // Explicit native field input, not a claim that Read<bool> preserves wire bytes.
        robot.HandleActiveObjectConnectionState(2,0xAB,ObjectType.Block_LIGHTCUBE1,(byte)raw);
        Assert.Null(report);robot.Engine.DrainAppDelivery();
        Assert.Equal($"FactoryID 0xab, connected {raw}",Assert.Single(logs));
        Assert.Equal(new ObjectConnectionStateMessage(0x11223344,0xAB,ObjectType.Block_LIGHTCUBE1,(byte)raw),report);
    }

    [Fact]
    // reach: unit (serializer followed by direct Broadcast, bypassing receive queue; explicit app drain).
    public void WireBoolReadStillNormalizesBeforeTheHandler()
    {
        using var robot=CozmoRobot.CreateOffline();ConnectedInput(robot);
        ObjectConnectionStateMessage? report=null;robot.Cubes.ConnectionStateBroadcast+=m=>report=m;
        // M2-001 Read<bool>, independently checked 0x0083C108..0x0083C10E.
        byte[] wire={0xD0,2,0,0,0,0xAB,0,0,0,1,0,0,0,0xFE};
        robot.Engine.Broadcast(RobotMessage.Parse(wire));
        Assert.Null(report);robot.Engine.DrainAppDelivery();
        Assert.NotNull(report);Assert.Equal((byte)1,report.ConnectedByte);
    }

    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void CheckedPoolBodyLogsOldStateAndResetsOnlyStateThree(int state)
    {
        bool oldError=EngineErrorState.ErrorFlagSet;
        try
        {
            var pool=new CubeConnections(_=>{},()=>BitConverter.Int32BitsToSingle(0x42F6E979));
            var slots=(Array)typeof(CubeConnections).GetField("_slots",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(pool)!;
            object input=slots.GetValue(2)!;
            void Set(string name,object value)=>input.GetType().GetField(name)!.SetValue(input,value);
            Set("FactoryId",0xABu);Set("Type",ObjectType.Block_LIGHTCUBE1);Set("State",(ActiveObjectSlotState)state);
            Set("Rssi",(byte)0xFE);Set("LastObservedTime",0x12345678u);Set("DisconnectedTime",9f);slots.SetValue(input,2);
            var lines=new List<(EngineLogLevel,string?,string,string)>();EngineErrorState.ErrorFlagSet=false;
            pool.SourceLog+=(l,c,k,t)=>{if(l==EngineLogLevel.Error)Assert.False(EngineErrorState.ErrorFlagSet);lines.Add((l,c,k,t));};
            pool.OnConnectionState(2,0xAB,false,ObjectType.Block_LIGHTCUBE3);
            bool invalid=state is not 2 and not 3;
            Assert.Equal(invalid,EngineErrorState.ErrorFlagSet);
            Assert.Equal(invalid?2:1,lines.Count);
            if(invalid)Assert.Equal((EngineLogLevel.Error,(string?)null,"Robot.HandleDisconnectedFromObject.InvalidState",$"Invalid state {state} when disconnected from object 0xab with active ID 2"),lines[0]);
            Assert.Equal((EngineLogLevel.Info,(string?)"BlockPool","Robot.HandleDisconnectedFromObject",$"Disconnected from active Id 2 with factory Id 0xab of type Block_LIGHTCUBE3. Connection State = {state}"),lines[^1]);
            object output=slots.GetValue(2)!;object Field(string n)=>output.GetType().GetField(n)!.GetValue(output)!;
            if(state==3)
            {
                Assert.Equal(0u,Field("FactoryId"));Assert.Equal(ObjectType.InvalidObject,Field("Type"));
                Assert.Equal(ActiveObjectSlotState.Empty,Field("State"));Assert.Equal((byte)0,Field("Rssi"));Assert.Equal(0u,Field("LastObservedTime"));
                Assert.Equal(0,BitConverter.SingleToInt32Bits((float)Field("DisconnectedTime")));
            }
            else
            {
                Assert.Equal(0xABu,Field("FactoryId"));Assert.Equal(ObjectType.Block_LIGHTCUBE1,Field("Type"));
                Assert.Equal(ActiveObjectSlotState.Disconnected,Field("State"));Assert.Equal((byte)0xFE,Field("Rssi"));Assert.Equal(0x12345678u,Field("LastObservedTime"));
                Assert.Equal(0x42F6E979,BitConverter.SingleToInt32Bits((float)Field("DisconnectedTime")));
            }
        }
        finally{EngineErrorState.ErrorFlagSet=oldError;}
    }
}
