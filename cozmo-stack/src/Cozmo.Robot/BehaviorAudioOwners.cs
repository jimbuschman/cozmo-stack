using Cozmo.Robot.Behavior;
namespace Cozmo.Robot;

// fidelity: M13-038, M13-039
public sealed partial class EngineRobot
{
    internal BehaviorPublicStateBroadcaster BehaviorPublicState {get;private set;}=null!;
    internal BehaviorAudioClient? BehaviorAudioA {get;private set;}
    internal BehaviorAudioClient? BehaviorAudioB {get;private set;}
    internal Action<BehaviorAudioRequest>? BehaviorAudioRecipient;
    // M13-010's existing WorkoutComponent.ShouldPlayEightiesMusic, including its shared cache, not a raw RNG draw.
    internal Func<bool>? EightiesMusicInput;
    internal Action<string>? BehaviorAudioRetiredForTest;
    private void InitBehaviorAudioOwners()
    {
        BehaviorPublicState=new(this); // robot+40 before manager A+44/client68, then B+48/client10
        try
        {
            BehaviorAudioA=new(this,BehaviorPublicState,"A",HasExternalInterface);
            BehaviorAudioB=new(this,BehaviorPublicState,"B",HasExternalInterface);
        }
        catch{BehaviorAudioB?.Dispose();BehaviorAudioA?.Dispose();throw;}
        Lifetime.Bind(0x44,address=>
        {
            var manager=BehaviorManagerOwner;BehaviorManagerOwner=null;manager?.Dispose();
            Lifetime.Missing(address,0x44); // retain the existing per-Robot remaining-owner diagnostic
            var client=BehaviorAudioA;BehaviorAudioA=null;client?.Dispose();
        });
        Lifetime.Bind(0x48,address=>
        {
            var client=BehaviorAudioB;BehaviorAudioB=null;client?.Dispose();
            Lifetime.Missing(address,0x48);
        });
        Lifetime.Bind(0x40,address=>{Lifetime.Missing(address,0x40);BehaviorPublicState.Retire();});
        SteppedBehavior.ReportMissing("M13-038 PublicStateBroadcaster::Update0x0064D5DC native manager/current-requested/carrying/reaction/needs input joins are unbound; checked broadcaster and both client owners are built, no host Freeplay state substituted");
    }
    internal void ReportBehaviorPublicStateInputJoin()=>SteppedBehavior.ReportMissing("M13-038 PublicStateBroadcaster Update0x0064D5DC at Robot::Update: native input owner/caller join is absent; checked Update(input) is built, no publication or tick ordering invented");
}

public sealed partial class CozmoRobot
{
    /// <summary>Supplies the app's SetSparkedMusicState input (G2E 0xA3), preserving all 32 payload bits.</summary>
    // fidelity: M13-039 — operator2026-10-16 app-boundary input policy.
    public void SetSparkedMusicState(uint state)=>Engine.PostGameMessage(()=>
        Engine.AnimationMessages.Deliver(0xA3,false,new SparkedMusicStateInput(state)));
}
