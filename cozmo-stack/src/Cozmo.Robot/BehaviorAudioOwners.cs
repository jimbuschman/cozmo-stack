using Cozmo.Robot.Behavior;
namespace Cozmo.Robot;

// fidelity: M13-038, M13-039
public sealed partial class EngineRobot
{
    internal BehaviorPublicStateBroadcaster BehaviorPublicState {get;private set;}=null!;
    internal BehaviorAudioClient? BehaviorAudioA {get;private set;}
    internal BehaviorAudioClient? BehaviorAudioB {get;private set;}
    internal Action<BehaviorAudioRequest>? BehaviorAudioRecipient;
    internal Action<BehaviorAudioRequest, uint>? BehaviorAudioAppliedForTest;

    internal void SendBehaviorAudio(BehaviorAudioRequest request)
    {
        if (BehaviorAudioRecipient is { } observer)
        {
            observer(request);
            return;
        }

        if (Lifetime.Destroying || !ReferenceEquals(Engine.Robot, this)) return;
        if (Engine.IsOfflineEngine && CannedSpeechSinkForTest is null) return;
        var audio = GetBehaviorAudioOwner();
        if (audio is not null)
            audio.SubmitBehaviorAudio(request, BehaviorAudioAppliedForTest);
        else
            SteppedBehavior.ReportMissing("M13-039 Freeplay audio requires the local Wwise ARM libraries, archived banks and PCM output");
    }
    // M13-010's existing WorkoutComponent.ShouldPlayEightiesMusic, including its shared cache, not a raw RNG draw.
    internal Func<bool>? EightiesMusicInput;
    internal Action<string>? BehaviorAudioRetiredForTest;
    private void InitBehaviorAudioOwners()
    {
        BehaviorPublicState = new(this) // robot+40 before manager A+44/client68, then B+48/client10
        {
            UnlockNameInput = NativeUnlockNames.Lookup,
            ReactionNameInput = value => value switch
            {
                0x15 => "Count",
                0x16 => "NoneTrigger",
                < 0x15 => ((ReactionTrigger)value).ToString(),
                _ => null
            } // EnumToString at 0x0077065C, table at 0x010337C0.
        };
        try
        {
            BehaviorAudioA=new(this,BehaviorPublicState,"A",HasExternalInterface);
            BehaviorAudioB=new(this,BehaviorPublicState,"B",HasExternalInterface);
        }
        catch{BehaviorAudioB?.Dispose();BehaviorAudioA?.Dispose();throw;}
        Lifetime.Bind(0x44,address=>
        {
            var manager=BehaviorManagerOwner;BehaviorManagerOwner=null;manager?.Dispose();
            var runtime=NativeBehaviorRuntime;NativeBehaviorRuntime=null;runtime?.Dispose();
            Lifetime.Missing(address,0x44); // retain the existing per-Robot remaining-owner diagnostic
            var client=BehaviorAudioA;BehaviorAudioA=null;client?.Dispose();
        });
        Lifetime.Bind(0x48,address=>
        {
            var client=BehaviorAudioB;BehaviorAudioB=null;client?.Dispose();
            Lifetime.Missing(address,0x48);
        });
        Lifetime.Bind(0x40,address=>{Lifetime.Missing(address,0x40);BehaviorPublicState.Retire();});
    }

    // Robot::Update 0x00514478, after the cube and body lights.
    private void UpdateBehaviorPublicState()
    {
        if (Lifetime.Destroying || !ReferenceEquals(Engine.Robot, this)) return;
        if (BehaviorManagerOwner is not { } manager || CarryingOwner is not { } carrying)
        {
            SteppedBehavior.ReportMissing("M1-034/M13-038 PublicStateBroadcaster 0x0064D5F0/0x0064D6C4: manager or carrying owner unavailable");
            return;
        }

        // 0x0064D5E6..0x0064D5F2 reads manager+0x58/+0x60. 0x0064D6BA..0x0064D6C8 reads Carrying+8.
        // 0x0064D728..0x0064D742 uses manager's trigger getter. 0x0064D792..0x0064D7BA reads needs 1, 0, 2.
        var needs = Engine.Needs?.State;
        BehaviorPublicState.Update(new(manager.ActiveSparkId, manager.RequestedSparkId,
            unchecked((int)(carrying.CarriedObjectId ?? uint.MaxValue)),
            manager.CurrentReactionTrigger is { } trigger ? (byte)trigger : (byte)0x16,
            needs is null ? null : ((float)needs.GetNeedLevel(NeedId.Energy), (float)needs.GetNeedLevel(NeedId.Repair),
                (float)needs.GetNeedLevel(NeedId.Play))));
    }
}

public sealed partial class CozmoRobot
{
    /// <summary>Supplies the app's SetSparkedMusicState input (G2E 0xA3), preserving all 32 payload bits.</summary>
    // fidelity: M13-039 — operator2026-10-16 app-boundary input policy.
    public void SetSparkedMusicState(uint state)=>Engine.PostGameMessage(()=>
        Engine.AnimationMessages.Deliver(0xA3,false,new SparkedMusicStateInput(state)));
}
