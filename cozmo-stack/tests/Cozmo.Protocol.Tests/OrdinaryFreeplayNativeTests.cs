using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class OrdinaryFreeplayNativeTests
{
    private sealed class SuppliedChooser(ChooserDecision decision):IBehaviorChooser
    {
        public BehaviorChooserType Type=>BehaviorChooserType.StrictPriority;
        public IReadOnlyList<string> BehaviorIds=>Array.Empty<string>();
        public ChooserDecision GetDesiredActiveBehavior(IBehavior? current,double duration,BehaviorContext context,double now)=>decision;
    }
    private static void Phase(NativeActivityObjectiveOwner owner,int value) =>
        typeof(NativeActivityObjectiveOwner).GetField("_phase",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(owner,value);
    private static void AssertHealthyNeeds(PickupPyramidNativeExecutionTests.Fixture f)
    {
        var needs=f.Rig.Engine.Needs;Assert.False(needs.SparksRewardPending);
        Assert.False(needs.State.IsNeedAtBracket(NeedId.Play,NeedBracketId.Critical));
        Assert.False(needs.State.IsNeedAtBracket(NeedId.Play,NeedBracketId.Warning));
        Assert.False(needs.State.IsNeedAtBracket(NeedId.Repair,NeedBracketId.Warning));
        Assert.False(needs.State.IsNeedAtBracket(NeedId.Energy,NeedBracketId.Warning));
    }

    [Fact]
    public void RetainedScoredFieldsReloadInOrderAndPreserveMissingFlat()
    {
        var fields=new NativeScoredFields();
        using var first=JsonDocument.Parse("""{"flatScore":3}""");
        using var missing=JsonDocument.Parse("{}");
        using var last=JsonDocument.Parse("""{"flatScore":7}""");
        fields.Read("FPPeekABoo",first.RootElement);
        fields.Reload("FPPeekABoo",missing.RootElement);Assert.Equal(3,fields.Value!.FlatScore);
        fields.Reload("FPPeekABoo",last.RootElement);Assert.Equal(7,fields.Value!.FlatScore);
        fields.Reload("FPPeekABoo",missing.RootElement);Assert.Equal(7,fields.Value!.FlatScore);
    }

    [Fact]
    public void ConfiguredBuiltBehaviorAndRetainedFieldsAreSameOwner()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        var behavior=Assert.IsAssignableFrom<SteppedBehavior>(f.Runtime.Bound["PutDownDispatch_LookForFaceAndCube"]);
        Assert.Same(behavior.NativeScoredFields,f.Robot.NativeConfiguredScoreFields![behavior.Id]);
        Assert.True(f.Robot.NativeConfiguredScoreFields.ContainsKey("FPPeekABoo"));
    }

    // Isolated scored-comparison boundary using the actual retained configured fields, not an executable proxy.
    [Fact]
    public void UnbuiltZeroUsesRetainedFieldsAndPublicAttemptPermanentlyInvalidatesHistory()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        var chooser=new ScoringChooser(Array.Empty<ScoredBehaviorEntry>(),f.Runtime.Bound,native:true);
        chooser.EnableOrdinaryUnbuiltBoundary(f.Robot);
        var evaluate=typeof(ScoringChooser).GetMethod("EvaluateUnbuilt",BindingFlags.Instance|BindingFlags.NonPublic)!;
        string Evaluate(double now=0)=>evaluate.Invoke(chooser,new object[]{"FPPeekABoo",f.Runtime.Context,now})!.ToString()!;
        var fields=f.Robot.NativeConfiguredScoreFields!["FPPeekABoo"];
        Assert.True(fields.ConstructorPristine);Assert.Equal("ScoringZero",Evaluate());Assert.Equal("Unavailable",Evaluate(double.NaN));
        using var changed=JsonDocument.Parse("""{"flatScore":1}""");fields.Reload("FPPeekABoo",changed.RootElement);
        Assert.Equal("Unavailable",Evaluate());
        using var nonfinite=JsonDocument.Parse("""{"flatScore":1e100}""");fields.Reload("FPPeekABoo",nonfinite.RootElement);
        Assert.Equal("Unavailable",Evaluate());
        using var zero=JsonDocument.Parse("""{"flatScore":0}""");fields.Reload("FPPeekABoo",zero.RootElement);
        Assert.Equal("ScoringZero",Evaluate());
        using var graph=JsonDocument.Parse("""{"flatScore":0,"repetitionPenalty":{"nodes":[{"x":0,"y":1e100}]}}""");
        fields.Reload("FPPeekABoo",graph.RootElement);Assert.Equal("Unavailable",Evaluate());
        fields.Reload("FPPeekABoo",zero.RootElement);Assert.Equal("ScoringZero",Evaluate());
        Assert.False(f.Robot.BehaviorManagerOwner!.StartAsync("FPPeekABoo",f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        Assert.False(fields.ConstructorPristine);Assert.Equal("Unavailable",Evaluate());
        fields.Reload("FPPeekABoo",zero.RootElement);Assert.Equal("Unavailable",Evaluate());
    }

    [Fact]
    public void GuardDogFalseOwnGateRequiresKnownNonrunningOwnerHistory()
    {
        const string id="GuardDog";
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        Assert.False(f.Runtime.Bound.ContainsKey(id));
        var fields=f.Robot.NativeConfiguredScoreFields![id];Assert.True(fields.ConstructorPristine);
        var chooser=new ScoringChooser(Array.Empty<ScoredBehaviorEntry>(),f.Runtime.Bound,native:true);chooser.EnableOrdinaryUnbuiltBoundary(f.Robot);
        var method=typeof(ScoringChooser).GetMethod("EvaluateUnbuilt",BindingFlags.Instance|BindingFlags.NonPublic)!;
        string Evaluate()=>method.Invoke(chooser,new object[]{id,f.Runtime.Context,0d})!.ToString()!;
        Assert.Equal("KnownFalse",Evaluate());
        Assert.False(f.Robot.BehaviorManagerOwner!.StartAsync(id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        Assert.False(fields.ConstructorPristine);Assert.Equal("Unavailable",Evaluate());
        using var reload=JsonDocument.Parse("""{"flatScore":0}""");fields.Reload(id,reload.RootElement);
        Assert.False(fields.ConstructorPristine);Assert.Equal("Unavailable",Evaluate());
    }

    [Fact]
    public void FireTruckLockedGateUsesOriginalProgressionAndPristineHistoryBeforeScoring()
    {
        const string id="SparksFireTruckAlarm";using var f=new PickupPyramidNativeExecutionTests.Fixture();
        Assert.Equal(83u,NativeUnlockNames.FromBytes(System.Text.Encoding.UTF8.GetBytes("FireTruckAlarm")));
        Assert.False(f.Runtime.Bound.ContainsKey(id));var fields=f.Robot.NativeConfiguredScoreFields![id];Assert.True(fields.ConstructorPristine);
        var progression=f.Robot.Progression!;Assert.Same(progression,fields.FireTruckProgression);
        Assert.True(progression.TryIsUnlocked(83,out bool unlocked));Assert.False(unlocked);
        var chooser=new ScoringChooser(new[]{fields.Value!},f.Runtime.Bound,native:true);chooser.EnableOrdinaryUnbuiltBoundary(f.Robot);
        int draws=0;chooser.RandomDraw=()=>{draws++;return 0;};
        ChooserDecision Choose()=>chooser.GetDesiredActiveBehavior(null,0,f.Runtime.Context,f.Rig.Engine.Timer.SecondsF);
        var locked=Choose();Assert.False(locked.InputUnavailable);Assert.Null(locked.Behavior);Assert.Equal("KnownFalse",Assert.Single(locked.Scores).Note);Assert.Equal(0,draws);
        progression.ReadSink(new NvResult(-1,Array.Empty<byte>())); // supplied missing-NV result, actual default-restoration recipient
        Assert.True(progression.TryIsUnlocked(83,out unlocked));Assert.False(unlocked);Assert.False(Choose().InputUnavailable);
        using var nonfinite=JsonDocument.Parse("""{"flatScore":1e100}""");fields.Reload(id,nonfinite.RootElement);
        Assert.False(Choose().InputUnavailable);Assert.Equal(0,draws); // source base rejection precedes scoring fields
        progression.Unlocked.Add(83);Assert.True(Choose().InputUnavailable);progression.Unlocked.Remove(83);
        var known=typeof(ProgressionUnlockOwner).GetProperty("OverridesKnownEmpty",BindingFlags.Instance|BindingFlags.NonPublic)!;
        known.SetValue(progression,false);Assert.True(Choose().InputUnavailable);known.SetValue(progression,true);
        var original=f.Robot.NativeConfiguredScoreFields![id];f.Robot.NativeConfiguredScoreFields[id]=new NativeScoredFields();
        Assert.True(Choose().InputUnavailable);f.Robot.NativeConfiguredScoreFields[id]=original;
        var oldProgression=f.Robot.Progression;f.Robot.Progression=new ProgressionUnlockOwner(f.Robot);
        Assert.True(Choose().InputUnavailable);f.Robot.Progression=oldProgression;
        Assert.False(f.Robot.BehaviorManagerOwner!.StartAsync(id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        Assert.False(fields.ConstructorPristine);Assert.True(Choose().InputUnavailable);
    }

    [Fact]
    public void FireTruckChangedConfigAndQueuedUnsupportedRequestWithdrawProof()
    {
        const string id="SparksFireTruckAlarm";using var f=new PickupPyramidNativeExecutionTests.Fixture();
        var fields=f.Robot.NativeConfiguredScoreFields![id];var chooser=new ScoringChooser(new[]{fields.Value!},f.Runtime.Bound,native:true);chooser.EnableOrdinaryUnbuiltBoundary(f.Robot);
        ChooserDecision Choose()=>chooser.GetDesiredActiveBehavior(null,0,f.Runtime.Context,f.Rig.Engine.Timer.SecondsF);
        Assert.False(Choose().InputUnavailable);
        var property=f.Rig.Engine.GetType().GetProperty("HostBehaviorMap",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var original=f.Rig.Engine.HostBehaviorMap!;var changed=original.ToDictionary(p=>p.Key,p=>p.Value);
        using var altered=JsonDocument.Parse("""{"behaviorClass":"FireTruckAlarm","requiredUnlockId":"Count"}""");
        changed[BehaviorID.SparksFireTruckAlarm]=altered.RootElement;
        try {property.SetValue(f.Rig.Engine,changed);Assert.True(Choose().InputUnavailable);}
        finally {property.SetValue(f.Rig.Engine,original);}
        Assert.False(Choose().InputUnavailable);
        f.Rig.Robot.ExecuteBehaviorById((byte)BehaviorID.SparksFireTruckAlarm,1);f.Rig.Tick();
        Assert.False(fields.ConstructorPristine);Assert.True(Choose().InputUnavailable);
    }

    [Fact]
    public void ReactToObstacleUsesActualConfiguredPlayAnimAndLiveAiFlag()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        var react=Assert.IsType<PlayAnimBehavior>(f.Runtime.Bound["ReactToObstacle"]);
        Assert.Same(react.NativeScoredFields,f.Robot.NativeConfiguredScoreFields![react.Id]);
        Assert.Equal("ObstacleDetected",react.WantsToRunStrategy);Assert.False(f.Runtime.AI.ObstacleDetected);
        var chooser=new ScoringChooser(new[]{react.NativeScoring!},f.Runtime.Bound,native:true);
        var decision=chooser.GetDesiredActiveBehavior(null,0,f.Runtime.Context,f.Rig.Engine.Timer.SecondsF);
        Assert.False(decision.InputUnavailable);Assert.Null(decision.Behavior);Assert.Equal("scored 0",Assert.Single(decision.Scores).Note);
    }

    [Fact]
    public void ActualInterestingEdgeOwnerDistinguishesUnknownInputsFromKnownEmpty()
    {
        // Isolated own-predicate unit on the retained configured owner; no native map producer is invented.
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        var edge=Assert.IsType<VisitInterestingEdgeBehavior>(f.Runtime.Bound["Hiking_VisitInterestingEdge"]);
        var method=typeof(VisitInterestingEdgeBehavior).GetMethod("IsRunnableInternal",BindingFlags.Instance|BindingFlags.NonPublic)!;
        (bool Available,bool Value) Evaluate()=>SteppedBehavior.EvaluateHostOperation(()=>(bool)method.Invoke(edge,new object[]{f.Runtime.Context})!);
        Assert.False(Evaluate().Available);
        var chooser=new ScoringChooser(new[]{edge.NativeScoring!},f.Runtime.Bound,native:true);chooser.EnableOrdinaryUnbuiltBoundary(f.Robot);
        f.Runtime.AI.Analyzer.AddEnableRequest(1,"edge-unit");
        var unavailableStacks=new List<string>();SteppedBehavior.ReadinessUnavailableObservedForTest=()=>unavailableStacks.Add(Environment.StackTrace);
        ChooserDecision Choose()=>chooser.GetDesiredActiveBehavior(null,0,f.Runtime.Context,f.Rig.Engine.Timer.SecondsF);
        Assert.True(Choose().InputUnavailable);
        var original=edge.NativeInputs;
        try
        {
            edge.NativeInputs=_=>new InterestingEdgeInputs{RobotPose=f.Robot.GetCheckedMainPose(),WorldOrigin=f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId),Regions=Array.Empty<InterestingEdgeRegion>(),Map=null,EraseDebugSegments=_=>{}};
            var empty=Evaluate();Assert.True(empty.Available);Assert.False(empty.Value);
            var decision=Choose();Assert.True(!decision.InputUnavailable,$"libraryNull={f.Rig.Robot.Animations.Library is null}; unavailable={string.Join(" | ",unavailableStacks)}");Assert.Null(decision.Behavior);Assert.Equal("scored 0",Assert.Single(decision.Scores).Note);
            edge.NativeInputs=_=>new InterestingEdgeInputs{RobotPose=f.Robot.GetCheckedMainPose(),WorldOrigin=f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId),Regions=new[]{new InterestingEdgeRegion(1,Array.Empty<InterestingEdgeSegment>())},Map=null,EraseDebugSegments=_=>{}};
            Assert.False(Evaluate().Available);
        }
        finally{SteppedBehavior.ReadinessUnavailableObservedForTest=null;edge.NativeInputs=original;f.Runtime.AI.Analyzer.RemoveEnableRequest(1,"edge-unit");}
    }

    [Fact]
    public void PureIdAndUnrelatedTypeLookupPreservePeekHistoryButUnavailableIdRequestWithdrawsProof()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        var fields=f.Robot.NativeConfiguredScoreFields!["FPPeekABoo"];
        var selection=Assert.IsType<SelectionChooser>(f.Runtime.Tree.Single(a=>a.Id=="Selection").Chooser);
        Assert.True(fields.ConstructorPristine);Assert.Null(f.Robot.BehaviorManagerOwner!.Find("FPPeekABoo"));
        try {selection.FindBehaviorByExecutableType!(12);} catch(NativeChooserUnavailable) { }
        Assert.True(fields.ConstructorPristine);
        f.Rig.Robot.ExecuteBehaviorById((byte)BehaviorID.FPPeekABoo,2);f.Rig.Tick();
        Assert.False(fields.ConstructorPristine);
    }

    // Handler seam supplies a checked unbuilt finder outcome; production finder order is tested separately.
    [Fact]
    public void UnavailableExecutableRequestWithdrawsProofInHandler()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        var fields=f.Robot.NativeConfiguredScoreFields!["FPPeekABoo"];
        var selection=Assert.IsType<SelectionChooser>(f.Runtime.Tree.Single(a=>a.Id=="Selection").Chooser);
        var original=selection.FindBehaviorByExecutableType;
        try
        {
            selection.FindBehaviorByExecutableType=_=>throw new NativeChooserUnavailable("unbuilt","FPPeekABoo");
            selection.HandleExecuteBehavior(new Cozmo.Protocol.ExecuteBehaviorByExecutableTypeMessage(12,2));
            Assert.False(fields.ConstructorPristine);
        }
        finally {selection.FindBehaviorByExecutableType=original;}
    }

    [Fact]
    public void OrdinaryInterludeChecksActualNeedsInSourceOrderAndSelectsBuiltOwner()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();AssertHealthyNeeds(f);
        var activity=f.Runtime.FreeplayObserver.Freeplay.SubActivities.Single(a=>a.Id=="Socialize");
        var chooser=Assert.IsType<StrictPriorityChooser>(activity.InterludeChooser);
        ChooserDecision Query()=>chooser.GetDesiredActiveBehavior(null,0,f.Runtime.Context,f.Rig.Engine.Timer.SecondsF);
        string[] order={"EarnedSparks","Needs_SevereLowPlayBored","Needs_SevereLowPlayRequest","Needs_MildLowRepairRequest","Needs_MildLowEnergyRequest","Needs_MildLowPlayRequest"};
        var healthy=Query();Assert.False(healthy.InputUnavailable);Assert.Null(healthy.Behavior);Assert.Equal(order,healthy.Scores.Select(s=>s.Id));
        f.Rig.Engine.Needs.SparksRewardPending=true;
        var reward=Query();Assert.True(reward.InputUnavailable);Assert.Single(reward.Scores);Assert.Equal("EarnedSparks",reward.Scores[0].Id);
        f.Rig.Engine.Needs.SparksRewardPending=false;
        f.Rig.Engine.Needs.State.SetNeedLevel(NeedId.Repair,0.27);
        Assert.True(f.Rig.Engine.Needs.State.IsNeedAtBracket(NeedId.Repair,NeedBracketId.Warning));
        f.Rig.Engine.Timer.UpdateTime(100_000_000_000); // Source cooldown must have elapsed.
        var warning=Query();Assert.False(warning.InputUnavailable);Assert.Same(f.Runtime.Bound["Needs_MildLowRepairRequest"],warning.Behavior);Assert.Equal("Needs_MildLowRepairRequest",warning.Scores[^1].Id);
        f.Rig.Engine.Needs.State.SetNeedLevel(NeedId.Repair,1.0);
        Assert.False(f.Robot.BehaviorManagerOwner!.StartAsync("EarnedSparks",f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        var unknown=Query();Assert.True(unknown.InputUnavailable);Assert.Single(unknown.Scores);Assert.Equal("EarnedSparks",unknown.Scores[0].Id);
    }

    [Fact]
    public void StrictNativeChooserSkipsAReallyAbsentConfiguredId()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        var missing=Enum.GetValues<BehaviorID>().First(id=>!f.Rig.Engine.HostBehaviorMap!.ContainsKey(id));
        var chooser=new StrictPriorityChooser(new[]{missing.ToString(),"Wait"},f.Runtime.Bound,true,f.Robot);
        var decision=chooser.GetDesiredActiveBehavior(null,0,f.Runtime.Context,f.Rig.Engine.Timer.SecondsF);
        Assert.Equal(missing.ToString(),decision.Scores[0].Id);Assert.Equal("native missing",decision.Scores[0].Note);
        Assert.False(decision.InputUnavailable);
    }

    // Isolated common-wrapper boundary: supplied unavailable interlude decision preserves the completed prior cache.
    [Fact]
    public void UnavailableInterludePreservesCurrentAndPriorCache()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        var current=f.Runtime.Bound["Wait"];var prior=f.Runtime.Bound["PutDownDispatch_LookForFaceAndCube"];
        var next=f.Runtime.Bound["FindFaces_socialize"];
        var activity=new Activity{Id="Hiking",Strategy=new(),Chooser=new SuppliedChooser(new(next,"next",Array.Empty<(string,double,string)>())),
            InterludeChooser=new SuppliedChooser(new ChooserDecision(null,"unavailable",Array.Empty<(string,double,string)>()){InputUnavailable=true})};
        activity.CachedInterlude=prior;
        var fp=new FreeplaySystem(f.Robot.BehaviorManagerOwner!,f.Runtime.Context,f.Runtime.FreeplayObserver.Freeplay,f.Runtime.Bound,new FreeplayInputs()){NativeOwner=true};
        var method=typeof(FreeplaySystem).GetMethod("ChooseActivityBehavior",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var result=Assert.IsType<ChooserDecision>(method.Invoke(fp,new object?[]{activity,current,0d}));
        Assert.True(result.InputUnavailable);Assert.Same(current,result.Behavior);Assert.Same(prior,activity.CachedInterlude);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ParentKeepsCurrentWhenExistingOrNewChildInterludeIsUnavailable(bool newChild)
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        var current=f.Runtime.Bound["Wait"];var prior=f.Runtime.Bound["PutDownDispatch_LookForFaceAndCube"];
        var next=f.Runtime.Bound["FindFaces_socialize"];
        var activity=new Activity{Id="Hiking",Priority=1,Strategy=new(),Chooser=new SuppliedChooser(new(next,"next",Array.Empty<(string,double,string)>())),
            InterludeChooser=new SuppliedChooser(new ChooserDecision(null,"unavailable",Array.Empty<(string,double,string)>()){InputUnavailable=true})};
        activity.CachedInterlude=prior;
        var tree=new Activity{Id="Freeplay",Strategy=new(),SubActivities=new[]{activity}};
        var fp=new FreeplaySystem(f.Robot.BehaviorManagerOwner!,f.Runtime.Context,tree,f.Runtime.Bound,new FreeplayInputs{Needs=f.Rig.Engine.Needs},false)
        {NativeOwner=true,NativeStillCurrent=()=>true};
        try
        {
            if(!newChild)typeof(FreeplaySystem).GetProperty("Current")!.SetValue(fp,activity);
            else fp.SetDesiredActivityCacheForTest("Hiking");
            Assert.Same(current,((IManagedActivity)fp).GetDesiredActiveBehavior(current,1));
            Assert.Same(activity,fp.Current);Assert.Same(prior,activity.CachedInterlude);
            if(newChild)Assert.Equal("Hiking",fp.DesiredActivityCacheForTest);
        }
        finally {fp.DetachHostHandle();}
    }

    [Fact]
    public void AnyNonNullBaseAnswerReturnsBeforeSpecialistInputsEvenWhenEqualCurrent()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        using var config=JsonDocument.Parse("{} ");
        var activity=new Activity{Id="Socialize",Type="Socialize",Strategy=new()};
        using var owner=new NativeActivityObjectiveOwner(activity,f.Robot,config.RootElement,f.Runtime.Bound);
        owner.OnSelectedInternal();
        var candidate=f.Runtime.Bound["PutDownDispatch_LookForFaceAndCube"];
        var answer=new ChooserDecision(candidate,"supplied base result",Array.Empty<(string,double,string)>());
        Assert.Same(candidate,owner.Desired(candidate,answer,f.Runtime.Context).Behavior);
        Assert.Same(candidate,owner.Desired(null,answer,f.Runtime.Context).Behavior);
        Assert.Equal(0,owner.Phase);
    }

    [Fact]
    public void PopulationUsesRealRandomEvenForEqualBoundsAndAccumulatesDuplicates()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        using var config=JsonDocument.Parse("""
          {"requiredObjectives":[
            {"objective":"PeekABooComplete","behaviorID":"FPPeekABoo","randomCompletionsNeededMin":1,"randomCompletionsNeededMax":1},
            {"objective":"PeekABooComplete","behaviorID":"FPPeekABoo","randomCompletionsNeededMin":1,"randomCompletionsNeededMax":1}]}
          """);
        var activity=new Activity{Id="Socialize",Type="Socialize",Strategy=new()};
        using var owner=new NativeActivityObjectiveOwner(activity,f.Robot,config.RootElement,f.Runtime.Bound);
        int words=0;f.Rig.Engine.ContextRandom.WordSourceForTest=()=>{words++;return 0;};
        try { owner.OnSelectedInternal();Assert.Equal(4,words);Assert.Equal(2u,owner.Counts[16]); }
        finally { f.Rig.Engine.ContextRandom.WordSourceForTest=null; }
    }

    [Fact]
    public void InitializedRecipientRetainsStateAcrossDeselectionAndRetires()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        using var config=JsonDocument.Parse("""{"requiredObjectives":[{"objective":"PeekABooComplete","behaviorID":"FPPeekABoo"}]}""");
        var activity=new Activity{Id="Socialize",Type="Socialize",Strategy=new()};
        var owner=new NativeActivityObjectiveOwner(activity,f.Robot,config.RootElement,f.Runtime.Bound);
        owner.OnSelectedInternal();Phase(owner,2);
        f.Rig.Engine.AnimationMessages.Deliver(0x8B,true,12u);
        Assert.Equal(3,owner.Phase);Assert.Equal(1u,owner.Counts[16]);
        f.Rig.Engine.AnimationMessages.Deliver(0x8B,true,16u);
        Assert.Empty(owner.Counts);
        owner.Dispose();f.Rig.Engine.AnimationMessages.Deliver(0x8B,true,12u);
        Assert.Equal(3,owner.Phase);
    }

    // Configured unit: the real FindFaces owner is used as an objective candidate to isolate phases 3–5.
    [Fact]
    public void ObjectivePhaseSelectsActualOwnerAndDefersStopUntilActionCompletes()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        var find=Assert.IsType<NativeFindFacesSocialize>(f.Runtime.Bound["FindFaces_socialize"]);
        Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync(find.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        using var config=JsonDocument.Parse("""{"requiredObjectives":[{"objective":"PeekABooComplete","behaviorID":"FindFaces_socialize"}]}""");
        var activity=new Activity{Id="Socialize",Type="Socialize",Strategy=new()};
        using var owner=new NativeActivityObjectiveOwner(activity,f.Robot,config.RootElement,f.Runtime.Bound);
        owner.OnSelectedInternal();Phase(owner,3);
        var empty=new ChooserDecision(null,"supplied null base",Array.Empty<(string,double,string)>());
        Assert.Same(find,owner.Desired(null,empty,f.Runtime.Context).Behavior);Assert.Equal(4,owner.Phase);
        f.Rig.Engine.AnimationMessages.Deliver(0x8B,true,16u);
        Assert.Equal(5,owner.Phase);Assert.True(find.EngineRunning);Assert.Equal(0,find.UpdateStatus());
        Assert.Same(find,owner.Desired(find,empty,f.Runtime.Context).Behavior);
        var action=Assert.IsAssignableFrom<ActionRunner>(find.OwnedRunner);action.State=0x03000018;f.Robot.ActionList.Update();
        Assert.Equal(2,find.UpdateStatus());Assert.Equal(0u,find.SearchIterations);
    }

    [Fact]
    public void ObjectivePhaseFourNullCurrentUsesSuccessfulCommonInitCounter()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        var find=Assert.IsType<NativeFindFacesSocialize>(f.Runtime.Bound["FindFaces_socialize"]);
        using var config=JsonDocument.Parse("""{"requiredObjectives":[{"objective":"PeekABooComplete","behaviorID":"FindFaces_socialize"}]}""");
        var activity=new Activity{Id="Socialize",Type="Socialize",Strategy=new()};
        using var owner=new NativeActivityObjectiveOwner(activity,f.Robot,config.RootElement,f.Runtime.Bound);
        owner.OnSelectedInternal();Phase(owner,3);
        var empty=new ChooserDecision(null,"supplied null base",Array.Empty<(string,double,string)>());
        Assert.Same(find,owner.Desired(null,empty,f.Runtime.Context).Behavior);int before=find.ScoreIncreaseCount;
        Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync(find.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        Assert.Equal(unchecked(before+1),find.ScoreIncreaseCount);
        Assert.Null(owner.Desired(null,empty,f.Runtime.Context).Behavior);Assert.Equal(6,owner.Phase);
    }

    [Fact]
    public void NoExternalInterfaceDoesNotSubscribeObjectiveRecipient()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        using var config=JsonDocument.Parse("{}");
        var activity=new Activity{Id="Socialize",Type="Socialize",Strategy=new()};
        f.Robot.HasExternalInterface=false;
        try
        {
            using var owner=new NativeActivityObjectiveOwner(activity,f.Robot,config.RootElement,f.Runtime.Bound);
            owner.OnSelectedInternal();Phase(owner,2);
            f.Rig.Engine.AnimationMessages.Deliver(0x8B,true,12u);
            Assert.Equal(2,owner.Phase);
        }
        finally {f.Robot.HasExternalInterface=true;}
    }

    [Fact]
    public void ConfiguredFindFacesReceivesRealSensorPhaseBeforeFreeplayAndRetires()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        var find=Assert.IsType<NativeFindFacesSocialize>(f.Runtime.Bound["FindFaces_socialize"]);
        var center=typeof(NativeFindFacesSocialize).GetField("_center",BindingFlags.Instance|BindingFlags.NonPublic)!;
        center.SetValue(find,1f);float? observed=null;
        void OnFreeplay(OffTreadsState from,OffTreadsState to)=>observed=(float)center.GetValue(find)!;
        f.Rig.Robot.Sensors.FreeplayOffTreadsStateChanged+=OnFreeplay;
        try
        {
            f.Rig.Robot.Sensors.DispatchOffTreadsStateChanged(OffTreadsState.InAir,OffTreadsState.OnTreads);
            Assert.Equal(0f,observed);find.Dispose();center.SetValue(find,1f);
            f.Rig.Robot.Sensors.DispatchOffTreadsStateChanged(OffTreadsState.InAir,OffTreadsState.OnTreads);
            Assert.Equal(1f,(float)center.GetValue(find)!);
        }
        finally {f.Rig.Robot.Sensors.FreeplayOffTreadsStateChanged-=OnFreeplay;}
    }

    [Fact]
    public void ChangedFindFacesWaitTriggerStaysUnavailable()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        var source=f.Rig.Engine.HostBehaviorMap![BehaviorID.FindFaces_socialize];
        var changed=JsonNode.Parse(source.GetRawText(),documentOptions:new JsonDocumentOptions{CommentHandling=JsonCommentHandling.Skip,AllowTrailingCommas=true})!;
        changed["params"]!["s2_WaitAnimTrigger"]="Count";
        using var config=JsonDocument.Parse(changed.ToJsonString());
        using var find=new NativeFindFacesSocialize(new BehaviorFactoryContext{Manipulation=f.M,NativeRobot=f.Robot},config.RootElement);
        Assert.False(find.ParametersAvailable);
    }

    [Fact]
    public void FindFacesWithoutExternalInterfaceAddsNoSensorRecipient()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        var eventField=f.Rig.Robot.Sensors.GetType().GetField("ConfiguredBehaviorOffTreadsStateChanged",BindingFlags.Instance|BindingFlags.NonPublic)!;
        int Count()=>((Delegate?)eventField.GetValue(f.Rig.Robot.Sensors))?.GetInvocationList().Length??0;
        int before=Count();f.Robot.HasExternalInterface=false;
        try
        {
            using var find=new NativeFindFacesSocialize(new BehaviorFactoryContext{Manipulation=f.M,NativeRobot=f.Robot},f.Rig.Engine.HostBehaviorMap![BehaviorID.FindFaces_socialize]);
            Assert.Equal(before,Count());
        }
        finally {f.Robot.HasExternalInterface=true;}
    }

    // Isolated helper boundary: the word source supplies a synchronous retirement during the first native draw.
    [Fact]
    public void FindFacesRetirementDuringFirstDrawStopsLaterDrawsAndActionConstruction()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        var find=Assert.IsType<NativeFindFacesSocialize>(f.Runtime.Bound["FindFaces_socialize"]);
        var method=typeof(NativeFindFacesSocialize).GetMethod("HeadTurn",BindingFlags.Instance|BindingFlags.NonPublic)!;
        int words=0;f.Rig.Engine.ContextRandom.WordSourceForTest=()=>{if(++words==1)find.Dispose();return 0;};
        try
        {
            Assert.Null(method.Invoke(find,new object[]{1f,6f,20f,40f,0f,0L,150f,90f}));
            Assert.Equal(2,words); // one native double draw, no body/head draws
        }
        finally {f.Rig.Engine.ContextRandom.WordSourceForTest=null;}
    }

    [Fact]
    public void ConfiguredFindFacesStartsWithAwaitedPresetLift()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        var find=Assert.IsType<NativeFindFacesSocialize>(f.Runtime.Bound["FindFaces_socialize"]);
        Assert.True(find.ParametersAvailable);
        Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync(find.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        Assert.Same(find,f.Robot.BehaviorManagerOwner.Current);
        Assert.NotNull(find.OwnedRunner);Assert.Equal(0,find.NativeState);Assert.Equal(0u,find.SearchIterations);
        Assert.Equal(32f,Assert.IsType<float>(find.OwnedRunner!.GetType().GetField("Target")!.GetValue(find.OwnedRunner)));
    }

    [Fact]
    public void EmptyOffChargerFreeplayNaturallyStartsConfiguredFindFaces()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        AssertHealthyNeeds(f);
        var find=Assert.IsType<NativeFindFacesSocialize>(f.Runtime.Bound["FindFaces_socialize"]);
        int mark=f.Rig.Mark();f.Rig.Robot.ActivateHighLevelActivity("Freeplay");
        for(int i=0;i<50 && !ReferenceEquals(f.Robot.BehaviorManagerOwner!.Current,find);i++)f.Rig.Tick(50);
        Assert.Same(f.Runtime.FreeplayObserver,f.Robot.BehaviorManagerOwner!.Activity);
        Assert.Equal("Socialize",f.Runtime.FreeplayObserver.Current?.Id);
        Assert.Same(find,f.Robot.BehaviorManagerOwner.Current);Assert.NotNull(find.OwnedRunner);
        f.Rig.Tick(50);
        Assert.Contains(f.Rig.SentSince(mark),m=>m is Cozmo.Protocol.SetLiftHeight);
        // Supplied terminal failure results exercise result-independent callbacks; the retained owner produces the counter.
        for(int i=0;i<30 && find.SearchIterations<2;i++)
        {
            var action=Assert.IsAssignableFrom<ActionRunner>(find.OwnedRunner);
            action.State=0x03000018;f.Robot.ActionList.Update();
        }
        Assert.Equal(2u,find.SearchIterations);
        f.Rig.Tick(50);
        Assert.NotEqual("Socialize",f.Runtime.FreeplayObserver.Current?.Id);
    }

    [Fact]
    public void LocatedCubeFreeplayNaturallyStartsAnActualOrdinaryOwner()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);AssertHealthyNeeds(f);var cube=f.Add();int mark=f.Rig.Mark();
        // RollCube unlock9 is denied; shipped KnockOver needs a stack of at least three cubes.
        // With the normal chooser null, Socialize phase0 reaches FindFaces own runnable 0x005C1A34.
        Assert.True(f.Robot.Progression!.TryIsUnlocked(9,out bool rollUnlocked));Assert.False(rollUnlocked);
        var find=Assert.IsType<NativeFindFacesSocialize>(f.Runtime.Bound["FindFaces_socialize"]);
        var trace=new List<string>();var missing=new List<string>();void Missing(string line)=>missing.Add(line);
        SteppedBehavior.MissingReported+=Missing;
        try
        {
        f.Rig.Robot.ActivateHighLevelActivity("Freeplay");
        for(int i=0;i<50 && !ReferenceEquals(f.Robot.BehaviorManagerOwner!.Current,find);i++)
        {
            f.Rig.Tick(50);var m=f.Robot.BehaviorManagerOwner;var b=m?.Current as SteppedBehavior;
            trace.Add($"{i}:activity={m?.Activity?.GetType().Name} child={f.Runtime.FreeplayObserver.Current?.Id} behavior={m?.Current?.Id} runner={b?.OwnedRunner?.GetType().Name}:{b?.CurrentRunnerTag} action={f.Robot.ActionList.GetCurrentAction()?.GetType().Name}");
        }
        var current=f.Robot.BehaviorManagerOwner!.Current;
        Assert.True(ReferenceEquals(current,find),"TRACE "+string.Join(" | ",trace)+" DECISIONS "+string.Join(" | ",f.Runtime.FreeplayObserver.Decisions)+" MISSING "+string.Join(" | ",missing));
        Assert.Same(current,f.Runtime.Bound[current!.Id]);
        Assert.Same(cube,f.M.World.GetLocatedObjectByIdHelper(cube.ObjectId));
        Assert.NotNull(Assert.IsAssignableFrom<SteppedBehavior>(current).OwnedRunner);
        for(int i=0;i<5;i++)f.Rig.Tick(50);
        Assert.Contains(f.Rig.SentSince(mark),m=>m is Cozmo.Protocol.SetLiftHeight);
        }
        finally {SteppedBehavior.MissingReported-=Missing;}
    }

    // Supplied terminal failure results exercise configured runners; no scan counter is injected.
    [Fact]
    public void FindFacesCompletionSequenceRunsPauseBeforeEachHeadMoveAndCountsIterations()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        var find=Assert.IsType<NativeFindFacesSocialize>(f.Runtime.Bound["FindFaces_socialize"]);
        int words=0;f.Rig.Engine.ContextRandom.WordSourceForTest=()=>{words++;return 0;};
        void Complete(){var action=Assert.IsAssignableFrom<ActionRunner>(find.OwnedRunner);action.State=0x03000018;f.Robot.ActionList.Update();}
        try
        {
            Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync(find.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
            Assert.Equal(2,words);Complete();var prelude=Assert.IsType<NativePanTiltRunner>(find.OwnedRunner);Assert.Equal(8,words);
            Assert.Equal((float)(-1d*BitConverter.Int64BitsToDouble(0x3F91DF46A2529D39)),(float)prelude.Parameters.PanAngleRad);
            Assert.Equal((float)(20d*BitConverter.Int64BitsToDouble(0x3F91DF46A2529D39)),(float)prelude.Parameters.HeadAngleRad);
            Assert.Equal(150f*BitConverter.UInt32BitsToSingle(0x3C8EFA35),(float)prelude.Parameters.MaxSpeedRadPerSec);
            Complete();Assert.Equal(1,find.NativeState);Assert.IsType<NativePanTiltRunner>(find.OwnedRunner);Assert.Equal(12,words);
            Complete();Assert.Equal(2,find.NativeState);Assert.IsType<TriggerLiftSafeAnimationAction>(find.OwnedRunner);Assert.Equal(12,words);
            Complete();Assert.Equal(3,find.NativeState);Assert.IsType<NativePanTiltRunner>(find.OwnedRunner);Assert.Equal(18,words);
            Complete();Assert.Equal(4,find.NativeState);Assert.IsType<TriggerLiftSafeAnimationAction>(find.OwnedRunner);Assert.Equal(18,words);
            Complete();Assert.IsType<NativePanTiltRunner>(find.OwnedRunner);Assert.Equal(24,words);
            Complete();Assert.IsType<TriggerLiftSafeAnimationAction>(find.OwnedRunner);Assert.Equal(24,words);
            Complete();Assert.IsType<NativePanTiltRunner>(find.OwnedRunner);Assert.Equal(30,words);
            Complete();Assert.Equal(5,find.NativeState);Assert.IsType<NativePanTiltRunner>(find.OwnedRunner);Assert.Equal(36,words);
            Complete();Assert.Equal(6,find.NativeState);Assert.IsType<NativePanTiltRunner>(find.OwnedRunner);Assert.Equal(40,words);
            Complete();Assert.Equal(1u,find.SearchIterations);Assert.Equal(1,find.NativeState);Assert.Equal(44,words);
            int starts=find.ScoreIncreaseCount;
            f.Robot.BehaviorManagerOwner.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);
            using var scope=new BehaviorScope();find.AttachRun(f.Runtime.Context,scope);
            Assert.False(find.Resume(null,f.Rig.Engine.Timer.SecondsF));
            Assert.Equal(1u,find.SearchIterations);Assert.Equal(starts,find.ScoreIncreaseCount);
            Assert.Equal(32f,Assert.IsType<float>(find.OwnedRunner!.GetType().GetField("Target")!.GetValue(find.OwnedRunner)));
        }
        finally {f.Rig.Engine.ContextRandom.WordSourceForTest=null;}
    }

    // Terminal failures are supplied to reach the result-independent cache callback.
    [Fact]
    public void FindFacesSnapshotsImageTimestampBeforeCachedFaceCopy()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        var face=new TrackedFace(new DetectedFace(31,new FaceRect(0,0,10,10),Name:"Ada"),100)
        {HeadPose=new Pose3d(Rotation3d.Identity,new Vec3(200,0,100),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId))};
        Assert.NotNull(f.M.Vision.Faces.AddOrUpdateFace(face,f.Robot.GetCheckedMainPose(),false));
        var find=Assert.IsType<NativeFindFacesSocialize>(f.Runtime.Bound["FindFaces_socialize"]);
        Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync(find.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        var lift=Assert.IsAssignableFrom<ActionRunner>(find.OwnedRunner);lift.State=0x03000018;f.Robot.ActionList.Update();
        var lookup=Assert.IsType<NativePanTiltRunner>(find.OwnedRunner);
        f.Rig.Robot.CameraSettings.LastProcessedImageTimestamp=180100;
        var oldScope=f.M.Vision.Faces.NativePoseScope;
        try
        {
            f.M.Vision.Faces.NativePoseScope=()=>{f.Rig.Robot.CameraSettings.LastProcessedImageTimestamp=180101;return oldScope!();};
            lookup.State=0x03000018;f.Robot.ActionList.Update();
            Assert.IsType<NativeLastFaceRunner>(find.OwnedRunner);
        }
        finally {f.M.Vision.Faces.NativePoseScope=oldScope;}
    }
}
