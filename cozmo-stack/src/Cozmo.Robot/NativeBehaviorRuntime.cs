using Cozmo.Robot.Behavior;

namespace Cozmo.Robot;

public sealed partial class EngineRobot
{
    // AIWhiteboard ctor 0x0056A2B6; shared by every context for this Robot.
    internal float LastDriveOffChargerSeconds=-1f;
    internal bool MoodResourcesAvailable;
    internal bool NativeActivityRecipientsConstructed;
    internal Behavior.NativeBehaviorRuntime? NativeBehaviorRuntime;
    internal void UpdateNativeMood(float now)
    {
        MoodOwner?.Advance(now);
        MoodOwner?.SendEmotionsToGame();
    }
}

public sealed partial class CozmoRobot
{
    /// <summary>Explicit host/app activity admission. Construction alone selects nothing.
    /// Specialized activity bodies that are not implemented retain the prior activity and report MISSING.</summary>
    public void ActivateHighLevelActivity(string activityId)=>Engine.PostGameMessage(()=>
        (Engine.Robot?.NativeBehaviorRuntime??throw new InvalidOperationException("No native Robot activity owner")).AdmitActivity(activityId));

    private void BindNativeMood(EngineRobot owner)
    {
        // Host app-boundary input: the filesystem DataPlatform service always exists.
        // A null ResourcesPath means a missing resource root, not a null native platform.
        string? resources=Engine.Options.ResourcesPath;
        owner.MoodResourcesAvailable=resources is not null && File.Exists(Path.Combine(resources,"config","engine","mood_config.json"));
        var model=Engine.LoadNativeMoodModel(resources);
        if(!owner.MoodResourcesAvailable)
        {
            Engine.EmitSourceLog(EngineLogLevel.Error,null,"HostDataPlatform.MoodResourcesUnavailable","Mood configuration resource root/file unavailable");
            Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();
            // N1-N8: defined null JSON path retains source default/existing curves,
            // continues watcher registration and normal periodic updates. Separate
            // surviving emotion-event files are still loaded by LoadNative.
        }
        var mood=new MoodState(model){SourceLog=(l,c,k,t)=>Engine.EmitSourceLog(l,c,k,t),Log=Engine.EmitStackLog};
        mood.AttachRobot(this);
        // 0x0067AEE8..AF38: watcher registration is independent of the external gate,
        // and is not suppressed by the resource reader's failure result.
        int handle=owner.ActionList.RegisterActionEndedCallback(completed=>
        {
            var type=RobotActionType.NameOf(completed.ActionType);
            var category=ActionResultCategory.NameOf((int)(completed.Result>>24));
            if(type is not null && category is not null)
                mood.HandleActionEnded(type,category,completed.Tag.ToString(System.Globalization.CultureInfo.InvariantCulture),Engine.Timer.SecondsF);
        });
        mood.BindNativeLifetime(owner,handle);
    }
}

public sealed partial class CozmoEngine
{
    private MoodModel? _nativeStaticMoodModel;
    internal MoodModel LoadNativeMoodModel(string? resources)
        =>_nativeStaticMoodModel=MoodModel.LoadNative(resources,_nativeStaticMoodModel,message=>
        {
            EmitSourceLog(EngineLogLevel.Error,null,"HostDataPlatform.MoodResourceReadFailed",message);
            Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();
        });
}
