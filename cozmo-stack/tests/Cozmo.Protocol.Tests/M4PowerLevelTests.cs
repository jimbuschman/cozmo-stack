using Cozmo.Robot;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("ObjectID process statics")]
public class M4PowerLevelTests
{
    /// <summary>
    /// M4-008 A12..A15 (0x00537360..0x005373E4): the connected object whose activeID equals the message's is found in the world (FindConnectedObjectHelper order); the Broadcast
    /// log and the ObjectPowerLevel carry that object's game ObjectID (Anki::ObjectID slot 0, 0x004EF772), which differs from the activeID; an activeID with no connected object
    /// logs and broadcasts nothing.
    /// </summary>
    [Fact]
    public void M4_008_A12_A15_TheBroadcastCarriesTheConnectedObjectsObjectIdNotTheActiveId()
    {
        using var rig = new Rig();
        var logs = new List<string>();
        var sent = new List<ObjectPowerLevel>();
        rig.Robot.Engine.LogLine += l => { lock (logs) logs.Add(l); };
        rig.Robot.Cubes.PowerLevelBroadcast += sent.Add;
        uint objectId = rig.Vision.World.ConnectedObjectIdForActiveId(2)!.Value;
        Assert.NotEqual(2u, objectId);                                           // the rig seeds world IDs 7, 8, 9 for slots 1..3
        rig.Send(new ObjectPowerLevel { ObjectID = 2, MissedPackets = 5, BatteryLevel = 120 });
        var b = Assert.Single(sent);
        Assert.Equal((objectId, 5u, (byte)120), (b.ObjectID, b.MissedPackets, b.BatteryLevel));
        Assert.Contains($"debug: RobotToEngine.ObjectPowerLevel.Broadcast: RobotID 1 activeID 2 objectID {objectId} at 120 cv", logs);
        sent.Clear();
        logs.Clear();
        rig.Send(new ObjectPowerLevel { ObjectID = 4, MissedPackets = 0, BatteryLevel = 120 });   // no connected object in slot 4
        Assert.Empty(sent);
        Assert.DoesNotContain(logs, l => l.Contains("ObjectPowerLevel.Broadcast"));
    }
}
