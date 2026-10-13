using System.Security.Cryptography;
using System.Text;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Transport;
using Xunit;
namespace Cozmo.Protocol.Tests;
[Collection("M5 process statics")]
public sealed class M3M4X15BuilderTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("das15-").FullName;
    public void Dispose() => Directory.Delete(_root,true);
    [Theory]
    [InlineData(1u,"00F8C16A-EB80-47FF-0030-67B84814BCEE")]
    [InlineData(0u,"00087F8C-2FAA-C497-00A8-16B7C0CC21D8")]
    public void NativeUuidUsesIndependentEntropyTwoDoublesLittleEndianAndUppercase(uint entropy,string expected)
    {
        // Native mt19937 first four words, entropy1: 6AC1F425 FF4780EB B8672F8C EEBC1448;
        // binary64 rounding gives FF4780EB6AC1F800 and EEBC1448B8673000.
        // Entropy0: 8C7F0AAC 97C4AA2F B716A675 D821CCC0 -> ...0800 and ...A800.
        int calls=0;var random=new EngineRandom(0,()=>{calls++;return entropy;});
        string uuid=EngineUuid.GetUUIDString(random);Assert.Equal(expected,uuid);Assert.Equal(1,calls);
        Assert.Matches("^[0-9A-F]{8}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{12}$",uuid);
        if(entropy==0)Assert.Equal('C',uuid[14]); // no v4 bit insertion.
        Assert.Equal(ulong.MaxValue,EngineUuid.ToUUIDWord(18446744073709551616.0));
        Assert.Equal(0xfffffffffffff800UL,EngineUuid.ToUUIDWord(double.BitDecrement(18446744073709551616.0)));
    }
    [Fact]
    public void ZeroByteDeviceFileIsOverwrittenInJavaFormAndEmptyLineIsPreserved()
    {
        string path=Path.Combine(_root,"id");File.WriteAllBytes(path,Array.Empty<byte>());
        using(var robot=Create(path))
        {
            string id=File.ReadLines(path).First();Assert.Matches("^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$",id);
            Assert.Equal(Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(id))),robot.Engine.Das.GetGlobalsForThisRun()["$player_id"]);
        }
        File.WriteAllText(path,"\n");using var empty=Create(path);
        Assert.Equal("d41d8cd98f00b204e9800998ecf8427e",empty.Engine.Das.GetGlobalsForThisRun()["$player_id"]);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void FailedDirectoryCreateOrFileWriteStillStartsWithNewDeviceId(bool directoryTarget)
    {
        string path;
        if(directoryTarget)path=Directory.CreateDirectory(Path.Combine(_root,"dir")).FullName;
        else {var parent=Path.Combine(_root,"file");File.WriteAllText(parent,"block mkdir");path=Path.Combine(parent,"id");}
        using var robot=Create(path);
        Assert.Matches("^[0-9a-f]{32}$",robot.Engine.Das.GetGlobalsForThisRun()["$player_id"]);
        Assert.False(File.Exists(path));
    }
    [Fact]
    public void NativeHeadBodyEventsFollowGlobalsAndUseSameBufferForTextAndData()
    {
        using var robot=Create(Path.Combine(_root,"id"));var engine=robot.Engine;var order=new List<string>();var entries=new List<EngineLogEntry>();
        engine.Das.CallObserved=c=>{if(c.Name=="_DAS_SetGlobal")order.Add((string)c.Arguments[0]!);};
        engine.LogEntry+=e=>
        {
            if(e.Name is not ("robot.handle_robot_set_head_id" or "robot.handle_robot_set_body_id"))return;
            order.Add(e.Name);entries.Add(e);
            Assert.Equal(e.Text,engine.Das.GetGlobalsForThisRun()[e.Name.EndsWith("head_id")?"$group":"$phys"]);
        };
        engine.Broadcast(new RobotAvailable{HwVersion=0xabcd,SerialNumberHead=0x12345678});
        engine.Broadcast(new ManufacturingID{BodyColor=unchecked((int)0x87654321),BodyHwVersion=0x12345678,SerialNumber=0x87654321});
        Assert.Equal(new[]{"$group","robot.handle_robot_set_head_id","$phys","robot.handle_robot_set_body_id"},order);
        Assert.Equal(new[]{"0xbeefabcd12345678","0xbeef4321567887654321"},entries.Select(e=>e.Text));
        Assert.All(entries,e=>{Assert.Equal(EngineLogLevel.Event,e.Level);Assert.Null(e.Channel);Assert.Equal(e.Text,Assert.Single(e.Data).Value);Assert.Contains("$data",e.Data.Keys);});
    }
    private static CozmoRobot Create(string path)
    {
        var robot=CozmoRobot.CreateForTest(new Port(),()=>0,new CozmoEngineOptions{DasDeviceIdPath=path});
        robot.Engine.InitOfflineLink(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback,5551));return robot;
    }
    private sealed class Port:IEngineTransport
    {
        public bool TimedOut=>false;public event Action<ReceiverEvent>? Received{add{}remove{}}
        public void Start(){} public void Connect(System.Net.IPAddress ip,bool simulated){} public void Disconnect(System.Net.IPEndPoint e){} public void SendData(byte[] d){}
    }
}
