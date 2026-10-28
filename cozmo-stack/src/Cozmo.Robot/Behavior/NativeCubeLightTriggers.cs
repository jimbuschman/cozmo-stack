namespace Cozmo.Robot.Behavior;

// Native EnumToString7655F0/table01032F90, independently checked lower-light-bindings.
internal static class NativeCubeLightTriggers
{
    private static readonly string[] Names={"Carrying","Connected","CubePouncePlayerLose","CubePouncePlayerWin","Dance_01","Dance_02","Dance_03","Dance_04","Dance_05","Dance_06","DanceFadeOff","DrivingTo","FeedingBlank","FeedingCyanCycle","FeedingCyanSolid","Flash","GatherCubesAllCubesInBeacon","GatherCubesCubeInBeacon","GuardDogBeingMoved","GuardDogBusted","GuardDogOff","GuardDogSetup","GuardDogSleeping","GuardDogSuccessfullyFlipped","Interacting","InteractingBehaviorLock","Onboarding","PyramidBaseBottom","PyramidFlourish","PyramidOnSideLocated","PyramidOnSideNotLocated","PyramidPickup","PyramidSingle","Sleep","SleepNoFade","SpeedTapWin","SpeedTapLose","Visible","WakeUp","Workout","Count"};
    internal static string? Name(uint raw)=>raw<Names.Length?Names[raw]:null;
    internal static uint FromName(string name)
    {int index=Array.IndexOf(Names,name);return index>=0?(uint)index:throw new ArgumentException("Unknown native cube light trigger "+name,nameof(name));}
}
