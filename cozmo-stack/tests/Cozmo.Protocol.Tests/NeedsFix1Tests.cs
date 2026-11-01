using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot;
using Cozmo.Transport;
using System.Net;
using Xunit;

namespace Cozmo.Protocol.Tests;

public sealed class NeedsFix1Tests
{
    // reach: action API on standalone manager; raw state seed substitutes for the unbuilt reward/persistence input.
    [Theory]
    [InlineData("CozmoSings", 0)] [InlineData("CozmoSingsCompleted_Sparked", 0)]
    [InlineData("CozmoPerformsComplete", 65)] [InlineData("DizzyHard", 65)]
    [InlineData("FireTruckAlarm", 65)] [InlineData("RepairHead", 65)]
    public void M15_046_SongActionsClearRawUnlockOnly(string action, int expected)
    {
        var n=Create(action); int words=0;
        n.DistortionRng=new EngineRandom(1){WordSourceForTest=()=>{words++;return 0;}};
        Assert.Equal(0,n.State.ForceNextSong); // ctor 0x0069BF38..42
        n.State.ForceNextSong=65; // input boundary: UnlockId CozmoSings, not a production reward
        Assert.True(n.RegisterNeedsActionCompleted(action));
        Assert.Equal(expected,n.State.ForceNextSong);
        Assert.Equal(6,words); // three RandDbl calls, 0x00696886..0x00696904
    }

    // reach: action API; raw input and clock supplied. 0x006960E0..0x0069615A.
    [Theory]
    [InlineData("CozmoSings")] [InlineData("CozmoSingsCompleted_Sparked")]
    public void M15_046_GuardsRetainAndCooldownRejectionClears(string action)
    {
        var n=Create(action,cooldown:60); int words=0;
        n.DistortionRng=new EngineRandom(1){WordSourceForTest=()=>{words++;return 0;}};
        n.State.ForceNextSong=65; n.Connected=false;
        Assert.False(n.RegisterNeedsActionCompleted(action)); Assert.Equal(65,n.State.ForceNextSong);
        n.Connected=true; n.SetPaused(true);
        Assert.False(n.RegisterNeedsActionCompleted(action)); Assert.Equal(65,n.State.ForceNextSong); Assert.Equal(0,words);
        n.SetPaused(false); n.Update(100);
        Assert.True(n.RegisterNeedsActionCompleted(action)); Assert.Equal(0,n.State.ForceNextSong); Assert.Equal(6,words);
        n.State.ForceNextSong=65; n.Update(159);
        var before=n.State.GetNeedLevel(NeedId.Energy);
        Assert.False(n.RegisterNeedsActionCompleted(action)); Assert.Equal(0,n.State.ForceNextSong);
        Assert.Equal(before,n.State.GetNeedLevel(NeedId.Energy)); Assert.Equal(6,words);
        n.Update(160); Assert.True(n.RegisterNeedsActionCompleted(action)); Assert.Equal(12,words);
    }

    // reach: action API; absent host config is a gap, after the source-defined clear prefix.
    [Theory]
    [InlineData("CozmoSings")] [InlineData("CozmoSingsCompleted_Sparked")]
    public void M15_046_MissingHostConfigStillClearsPrefix(string action)
    {
        var n=Create("SayName"); string? log=null; n.Log+=s=>log=s;
        n.State.ForceNextSong=65;
        Assert.False(n.RegisterNeedsActionCompleted(action));
        Assert.Equal(0,n.State.ForceNextSong); Assert.Contains("M15-046 MISSING host needs action config",log);
    }

    // reach: action API; supplied clock and raw state. Non-song cooldown rejection leaves the slot intact.
    [Fact]
    public void M15_046_OrdinaryCooldownRejectionRetainsSong()
    {
        var n=Create("SayName",cooldown:60); int words=0;
        n.DistortionRng=new EngineRandom(1){WordSourceForTest=()=>{words++;return 0;}};
        n.Update(100); Assert.True(n.RegisterNeedsActionCompleted("SayName"));
        n.State.ForceNextSong=65; n.Update(159);
        Assert.False(n.RegisterNeedsActionCompleted("SayName"));
        Assert.Equal(65,n.State.ForceNextSong); Assert.Equal(6,words);
    }

    private sealed class Port : IEngineTransport
    {
        public bool TimedOut => false;
        public event Action<ReceiverEvent>? Received { add { } remove { } }
        public void Start() { }
        public void Connect(IPAddress ip, bool simulated) { }
        public void Disconnect(IPEndPoint endpoint) { }
        public void SendData(byte[] data) { }
    }
    private static NeedsManager Create(string id, double repair=0, double energy=0, double play=0, double cooldown=0)
    {
        var actions = new Dictionary<string, NeedsActionDelta> { [id] = new(id, repair, 0, energy, 0, play, 0, cooldown, 0) };
        return new NeedsManager(() => 0, actions: actions);
    }

    // reach: unit; standalone manager and supplied RNG stand in for the engine owner and context RNG.
    [Fact]
    public void M15_046_ZeroPairsConsumeThreeDrawsAndGuardsConsumeNone()
    {
        var n=Create("SayName"); int words=0;
        n.DistortionRng=new EngineRandom(1){WordSourceForTest=()=>{words++;return 0;}};
        n.Connected=false; Assert.False(n.RegisterNeedsActionCompleted("SayName")); Assert.Equal(0,words);
        n.Connected=true; n.SetPaused(true); Assert.False(n.RegisterNeedsActionCompleted("SayName")); Assert.Equal(0,words);
        n.SetPaused(false); Assert.True(n.RegisterNeedsActionCompleted("SayName")); Assert.Equal(6,words); // 0x00696886..0x00696904
    }

    // reach: unit; supplied clock ticks and standalone owner; 0x00696146..0x006961DA.
    [Fact]
    public void M15_046_ActionCooldownRejectsBeforeDeadlineAndAdmitsEquality()
    {
        var n=Create("SayName", cooldown:60); int words=0;
        n.DistortionRng=new EngineRandom(1){WordSourceForTest=()=>{words++;return 0;}};
        n.Update(100); Assert.True(n.RegisterNeedsActionCompleted("SayName")); Assert.Equal(6,words);
        n.Update(159); Assert.False(n.RegisterNeedsActionCompleted("SayName")); Assert.Equal(6,words);
        n.Update(160); Assert.True(n.RegisterNeedsActionCompleted("SayName")); Assert.Equal(12,words);
    }

    // reach: unit; supplied cooldown values and clock. Native signed stamp and epsilon compare 0x006961B2..0x006961DA.
    [Theory]
    [InlineData(0x3727C5AC, false)]
    [InlineData(0x3727C5AB, true)]
    [InlineData(unchecked((int)0xB727C5AC), true)]
    public void M15_046_CooldownEpsilonAndSignedDeadline(int bits,bool repeatAccepted)
    {
        var n=Create("SayName",cooldown:BitConverter.Int32BitsToSingle(bits));
        n.DistortionRng=new EngineRandom(1); Assert.True(n.RegisterNeedsActionCompleted("SayName"));
        Assert.Equal(repeatAccepted,n.RegisterNeedsActionCompleted("SayName"));
    }

    // reach: unit; manually damaged parts and supplied RNG substitute for prior robot actions.
    [Theory]
    [InlineData("RepairHead",0)][InlineData("RepairLift",1)][InlineData("RepairTreads",2)]
    public void M15_029_RepairClearsSelectedPartAndLastPartForcesMaximum(string action,int part)
    {
        var n=Create(action); n.DistortionRng=new EngineRandom(1); n.State.SetNeedLevel(NeedId.Repair,.5);
        n.State.SetPartDamageForTest(part,1);
        Assert.True(n.RegisterNeedsActionCompleted(action));
        Assert.Equal((byte)0,n.State.PartDamage[part]); Assert.Equal(n.Config.MaximumNeedLevel,n.State.GetNeedLevel(NeedId.Repair)); // 0x006967DE..0x00696878, 0x00696906..0x0069693E
    }

    // reach: unit; supplied damaged-part state and RNG; native prefix/tail 0x006967DE..0x00696878 and 0x00696906..0x0069693E.
    [Fact]
    public void M15_029_RepairLeavesOtherDamageAndDoesNotForceMaximum()
    {
        var n=Create("RepairHead"); n.DistortionRng=new EngineRandom(1);
        n.State.SetNeedLevel(NeedId.Repair,.5); n.State.SetPartDamageForTest(0,1); n.State.SetPartDamageForTest(1,1);
        Assert.True(n.RegisterNeedsActionCompleted("RepairHead"));
        Assert.Equal((byte)0,n.State.PartDamage[0]); Assert.Equal((byte)1,n.State.PartDamage[1]);
        Assert.Equal(.5,n.State.GetNeedLevel(NeedId.Repair));
    }

    // reach: production; engine-owned manager, shipped needs files, public API and ConnectToRobot dispatch; no pointer fixture.
    [Fact]
    public void M15_046_EngineOwnerRejectsBeforeConnectAndAdmitsAfterConnect()
    {
        using var files=new BackupTestResources(); var options=files.Options();
        var source=Path.GetDirectoryName(BackupTestResources.ShippedConfig())!;
        foreach(var name in new[]{"needs_config.json","needs_decay_config.json","needs_action_config.json"})
            File.Copy(Path.Combine(source,name),Path.Combine(options.ResourcesPath!,"config/engine",name));
        using var robot=CozmoRobot.CreateForTest(new Port(),()=>1_000_000_000,options);
        var needs=robot.Engine.Needs; var before=needs.State.GetNeedLevel(NeedId.Energy);
        Assert.False(needs.RegisterNeedsActionCompleted("SayName"));
        Assert.Equal(before,needs.State.GetNeedLevel(NeedId.Energy));
        robot.Engine.ConnectToRobot(IPAddress.Loopback,true); robot.Engine.Tick();
        Assert.True(needs.RegisterNeedsActionCompleted("SayName"));
        Assert.True(needs.State.GetNeedLevel(NeedId.Energy)<before); // shipped SayName energyDelta -0.001
    }

    // reach: unit; explicit rate table and clock replace the production tick inputs.
    [Fact]
    public void M15_046_FillStartsAndRepeatedFillExtendsCooldown()
    {
        var rates = new Dictionary<NeedId,IReadOnlyList<(double,double)>> { [NeedId.Energy]=new[]{(0.0,.01)} };
        var action=new NeedsActionDelta("SayName",0,0,.5,0,0,0,0,0);
        var n=new NeedsManager(()=>0,decay:new DecayConfig(rates,rates),actions:new Dictionary<string,NeedsActionDelta>{{"SayName",action}});
        n.DistortionRng=new EngineRandom(1); n.Update(100); n.State.SetNeedLevel(NeedId.Energy,.5);
        Assert.True(n.RegisterNeedsActionCompleted("SayName"));
        n.Update(200); Assert.True(n.RegisterNeedsActionCompleted("SayName"));
        n.ApplyDecayAllNeeds(true,1400); Assert.Equal(1,n.State.GetNeedLevel(NeedId.Energy)); // 0x006968E2..0x006968EE, 0x006970C0..0x00697100
    }
}
