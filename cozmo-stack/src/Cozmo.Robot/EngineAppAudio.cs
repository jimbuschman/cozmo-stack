using Cozmo.Robot.Animation.Wwise;
using Cozmo.Robot.Behavior;
namespace Cozmo.Robot;

// fidelity: M1-054 — AudioUnityInput registration0x004EDC7E..96 /0x005915DC..0x00591824; M6 owns execution.
public sealed partial class CozmoEngine
{
    private readonly SubscriptionOwners _appAudioSubscriptions=new();
    internal Action<ushort,ReadOnlyMemory<byte>,WwiseAudioInputResult>? AppAudioDispatchObservedForTest;
    private void InitAppAudioInput()
    {
        if(Options.AudioInputDispatch is not {} dispatch)return; // source only constructs input when controller exists.
        foreach(var tag in WwiseAudioInputDispatch.SubscribedTags)
        {
            ushort id=(ushort)tag;
            _appAudioSubscriptions.Add(AnimationMessages.SubscribeMessage(id,false,p=>
            {
                var bytes=(byte[])p!;
                try
                {
                    var result=dispatch.HandleGameEvents(id,bytes);
                    AppAudioDispatchObservedForTest?.Invoke(id,bytes,result);
                    if(result.Outcome==WwiseAudioInputOutcome.Posted)
                        SteppedBehavior.ReportMissing("M6-023 app audio Posted: no robot audio-output owner is wired after this host dispatch; event submission is not audible playback");
                    if(result.Outcome==WwiseAudioInputOutcome.SubscribedNotHandled)
                        SteppedBehavior.ReportMissing($"M6-023 app audio tag{id}: M1 registration/dispatch is connected, but this M6 command body remains unbuilt");
                }
                catch(WwiseMissingBehaviourException error)
                {SteppedBehavior.ReportMissing("M6-023 app audio execution: "+error.Message);}
            }));
        }
    }
    private void SubmitAppAudio(ushort tag,ReadOnlyMemory<byte> body)
    {
        byte[] copy=body.ToArray();
        Post(()=>
        {
            if(Options.AudioInputDispatch is null)
            {SteppedBehavior.ReportMissing("M1-054/M6-023 AudioUnityInput: no live host audio-controller/dispatch owner supplied; request not executed");return;}
            AnimationMessages.Deliver(tag,false,copy);
        });
    }
    /// <summary>Submit the checked PostAudioEvent payload. M6 callback/flags and live playback gaps remain explicit.</summary>
    public void PostAudioEvent(WwisePostAudioEvent request)=>SubmitAppAudio(1,request.ToBytes());
    /// <summary>Submit the serialized M6 request unchanged; its command execution is still M6-023's gap.</summary>
    public void StopAllAudioEvents(ReadOnlyMemory<byte> request)=>SubmitAppAudio(2,request);
    public void PostAudioGameState(ReadOnlyMemory<byte> request)=>SubmitAppAudio(3,request);
    public void PostAudioSwitchState(ReadOnlyMemory<byte> request)=>SubmitAppAudio(4,request);
    public void PostAudioParameter(ReadOnlyMemory<byte> request)=>SubmitAppAudio(5,request);
    public void PostAudioMusicState(ReadOnlyMemory<byte> request)=>SubmitAppAudio(6,request);
    private void DisposeAppAudioInput()=>_appAudioSubscriptions.Retire();
}
