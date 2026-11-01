using System.Text.Json;

namespace Cozmo.Robot.Behavior;

// Configured behavior's mutable scoring fields; this is not an executable behavior.
internal sealed class NativeScoredFields
{
    // Partial state of the same retained configured PeekABoo owner; never an executable proxy.
    internal float? SparkPeekDeadline;
    internal ScoredBehaviorEntry? Value {get;private set;}
    internal bool ExplicitFlatReload {get;private set;}
    internal bool ConstructorPristine {get;private set;}
    internal long Epoch {get;private set;}
    internal void MarkPristine()=>ConstructorPristine=true;
    internal void Invalidate(){ConstructorPristine=false;++Epoch;}
    internal void Reload(string id,JsonElement source)
    {
        if(source.ValueKind==JsonValueKind.Object && source.TryGetProperty("considerThisHasRunForBehaviorObjective",out var objective) && objective.ValueKind!=JsonValueKind.Null)
            Invalidate();
        if(source.ValueKind==JsonValueKind.Object && source.TryGetProperty("flatScore",out var flat) && flat.ValueKind!=JsonValueKind.Null)
            ExplicitFlatReload=true;
        Read(id,source);
    }
    internal void Read(string id,JsonElement source)
    {
        bool obj=source.ValueKind==JsonValueKind.Object;
        var parsed=obj ? ScoredBehaviorEntry.FromScoringJson(id,source) : new ScoredBehaviorEntry(id,0,null,null,null,Array.Empty<EmotionScorer>());
        double flat=obj && source.TryGetProperty("flatScore",out var score) && score.ValueKind!=JsonValueKind.Null
            ? (float)parsed.FlatScore : Value?.FlatScore??0f;
        Value=parsed with {FlatScore=flat};
    }
}
