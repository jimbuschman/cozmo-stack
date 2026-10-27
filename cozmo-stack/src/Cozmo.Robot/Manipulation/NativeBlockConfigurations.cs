using Cozmo.Robot.Vision;
namespace Cozmo.Robot.Manipulation;

// fidelity: M12-049 -- CM13-05 rows4-14,27-29. No host time, midpoint, retained pose or global identity registry.
internal abstract class NativeBlockConfiguration(BlockConfigurationType type, uint[] ids)
{
    internal BlockConfigurationType Type { get; } = type;
    internal IReadOnlyList<uint> BlockIds { get; } = Array.AsReadOnly(ids);
    internal bool ContainsBlock(uint id) => BlockIds.Contains(id);
    internal abstract bool SourceEquals(NativeBlockConfiguration other);
}
internal sealed class NativeStackConfiguration(uint bottom,uint middle,uint top)
    : NativeBlockConfiguration(BlockConfigurationType.StackOfCubes,middle==uint.MaxValue?new[]{bottom,top}:new[]{bottom,middle,top})
{
    internal uint Bottom { get; }=bottom;
    internal uint Middle { get; }=middle;
    internal uint Top { get; }=top;
    internal StackOfCubes View { get; }=new(Array.AsReadOnly(middle==uint.MaxValue?new[]{bottom,top}:new[]{bottom,middle,top}));
    internal override bool SourceEquals(NativeBlockConfiguration other)=>other is NativeStackConfiguration s&&Bottom==s.Bottom&&Middle==s.Middle&&Top==s.Top;
}
internal sealed class NativePyramidBase : NativeBlockConfiguration
{
    internal uint MinId { get; }
    internal uint MaxId { get; }
    internal NativePyramidBase(uint a,uint b):this(Canonical(a,b)){}
    private NativePyramidBase((uint Min,uint Max) ids):base(BlockConfigurationType.PyramidBase,new[]{ids.Min,ids.Max})
    {MinId=ids.Min;MaxId=ids.Max;}
    private static (uint,uint) Canonical(uint a,uint b)=>a==b?(uint.MaxValue,uint.MaxValue):unchecked((int)a)<unchecked((int)b)?(a,b):(b,a);
    internal override bool SourceEquals(NativeBlockConfiguration other)=>MinId!=MaxId&&other is NativePyramidBase b&&MinId==b.MinId&&MaxId==b.MaxId;
}
internal sealed class NativePyramidConfiguration(NativePyramidBase basis,uint top)
    : NativeBlockConfiguration(BlockConfigurationType.Pyramid,new[]{basis.MinId,basis.MaxId,top})
{
    internal NativePyramidBase Base { get; }=new(basis.MinId,basis.MaxId); // native embeds IDs, not a strong reference to the base container entry
    internal uint TopBlockId { get; }=top;
    internal override bool SourceEquals(NativeBlockConfiguration other)=>other is NativePyramidConfiguration p&&Base.SourceEquals(p.Base)&&TopBlockId==p.TopBlockId;
}
internal sealed record NativeConfigurationSnapshot(IReadOnlyList<NativeStackConfiguration> Stacks,IReadOnlyList<NativePyramidBase> Bases,IReadOnlyList<NativePyramidConfiguration> Pyramids)
{
    // Constructor supplement00616B58 and concrete constructors: both vectors empty on actual manager construction.
    internal static readonly NativeConfigurationSnapshot Empty=new(Array.Empty<NativeStackConfiguration>(),Array.Empty<NativePyramidBase>(),Array.Empty<NativePyramidConfiguration>());
    internal IReadOnlyList<NativeBlockConfiguration> Cache(BlockConfigurationType type)=>type switch
    {BlockConfigurationType.StackOfCubes=>Stacks,BlockConfigurationType.PyramidBase=>Bases,BlockConfigurationType.Pyramid=>Pyramids,_=>Array.Empty<NativeBlockConfiguration>()};
}
internal sealed class NativeConfigurationUnavailable(string reason):Exception(reason);

// fidelity: M12-049, M11-037 --00616F00/0061682C, three independent queries/old vectors/processed sets.
internal sealed class NativeConfigurationProducer(NativeConfigurationGeometry geometry)
{
    internal NativeConfigurationSnapshot Build(NativeConfigurationSnapshot previous)
    {
        var stacks=Update(previous.Stacks,geometry.BuildStack);
        var bases=Update(previous.Bases,geometry.BuildBases);
        var pyramids=Update(previous.Pyramids,seed=>geometry.BuildPyramids(seed,bases.ToArray()));
        bases.RemoveAll(b=>pyramids.Any(p=>b.SourceEquals(p.Base))); // ordered post-all-three prune0062C72E
        return new(stacks.AsReadOnly(),bases.AsReadOnly(),pyramids.AsReadOnly());
    }
    private List<T> Update<T>(IReadOnlyList<T> old,Func<ObservableObject,IEnumerable<T>> build) where T:NativeBlockConfiguration
    {
        var seeds=geometry.Query(); // collect before old/current iteration, current-origin families1/2
        var current=new List<T>();var processed=new HashSet<uint>();
        foreach(var seed in seeds)
        {
            if(processed.Contains(seed.ObjectId))continue;
            foreach(var candidate in build(seed))
            {
                var entry=old.FirstOrDefault(prior=>candidate.SourceEquals(prior))??candidate;
                current.Add(entry); // no current-cache dedup; repeated equal candidates may alias first OLD
                foreach(var id in entry.BlockIds)processed.Add(id);
            }
        }
        return current;
    }
}
