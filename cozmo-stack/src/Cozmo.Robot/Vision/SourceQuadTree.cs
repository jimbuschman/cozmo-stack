using Cozmo.Transport;
using System.Globalization;
using Cozmo.Robot.Manipulation;
namespace Cozmo.Robot.Vision;

// fidelity: M13-030, M11-045, M11-089
internal class SourceMapData(MemoryMapContentType type, uint first, uint last)
{
    internal MemoryMapContentType Type = type;
    internal uint First = first, Last = last;
    internal virtual SourceMapData Clone() => new(Type, First, Last);
    // fidelity: M11-079 — base vt+0xC at 0x00680758..0x00680764; derived payload equality is separate.
    internal virtual bool Same(SourceMapData? other)
    {
        if (other is null)
        {
            StackDiagnostics.Write("M1-034 / M11-079: skip base data equality null RHS read at 0x0068075C; prior data retained");
            return false;
        }
        return Type == other.Type;
    }
}
internal sealed class CliffMapData(Vec2 normal, uint timestamp) : SourceMapData(MemoryMapContentType.Cliff, timestamp, timestamp)
{
    internal Vec2 Normal = new((float)normal.X, (float)normal.Y);
    internal override SourceMapData Clone() => new CliffMapData(Normal, First) { Type = Type, Last = Last };
    internal override bool Same(SourceMapData? other)
    {
        if (other is not CliffMapData cliff || cliff.Type != Type) return false;
        float tol = BitConverter.UInt32BitsToSingle(0x35A00000);
        return MathF.Abs((float)Normal.X-(float)cliff.Normal.X) < tol && MathF.Abs((float)Normal.Y-(float)cliff.Normal.Y) < tol;
    }
}
internal sealed class SourceNodeContent
{
    internal SourceMapData Data;
    internal SourceNodeContent(SourceMapData data) { Data = data.Clone(); } // native constructor vt+8
    internal bool Same(SourceNodeContent other) => Data.Type == other.Data.Type && (ReferenceEquals(Data, other.Data) || Data.Same(other.Data));
}

// fidelity: M11-045 — checked P bookkeeping, including native parent's retained subdivision credit.
internal sealed partial class SourceQuadProcessor
{
    internal double ExploredArea, EdgeArea;
    internal readonly Dictionary<MemoryMapContentType, List<SourceQuadNode>> TypeSets = new();
    internal int BorderGeneration;
    private static double Area(SourceQuadNode n) { float m = n.Side * BitConverter.UInt32BitsToSingle(0x3A83126F); return m*m; }
    private void Erase(SourceQuadNode n, MemoryMapContentType t) { if (((1 << (int)t) & 0x7C8) != 0 && TypeSets.TryGetValue(t, out var set)) set.Remove(n); }
    internal void Changed(SourceQuadNode n, MemoryMapContentType old, bool oldNoArea)
    {
        var type = n.Content.Data.Type; bool noArea = type == 0 || n.Children.Count != 0;
        double area = Area(n);
        if (oldNoArea && !noArea) ExploredArea += area;
        else if (!oldNoArea && noArea) ExploredArea -= area;
        if (old == MemoryMapContentType.InterestingEdge && type != old) EdgeArea -= area;
        if (type == MemoryMapContentType.InterestingEdge && old != type) EdgeArea += area;
        Erase(n, old);
        if (((1 << (int)type) & 0x7C8) != 0)
        {
            if (!TypeSets.TryGetValue(type, out var set)) TypeSets[type] = set = new();
            if(!set.Contains(n))set.Add(n);
        }
        InvalidateBorders();
    }
    internal void Destroyed(SourceQuadNode n)
    {
        var type = n.Content.Data.Type;
        if (n.Children.Count == 0 && type != 0) ExploredArea -= Area(n);
        if (type == MemoryMapContentType.InterestingEdge) EdgeArea -= Area(n);
        Erase(n, type); InvalidateBorders();
    }
}

// fidelity: M11-047 -- OV11 a-g; approved seed insertion order, not pointer-hash order.
internal sealed class SourceBorderWaypoint(SourceQuadNode from,SourceQuadNode to,int direction)
{
    internal readonly SourceQuadNode From=from,To=to;
    internal readonly int Direction=direction;
    internal bool End,Start;
}
internal sealed class SourceBorderCombination
{
    internal bool Dirty=true;
    internal readonly List<SourceBorderWaypoint> Waypoints=new();
}
internal sealed partial class SourceQuadProcessor
{
    internal readonly SortedDictionary<ulong,SourceBorderCombination> Borders=new();
    internal SourceBorderCombination? ActiveBorder;
    internal bool BorderWalkStarted;
    internal void InvalidateBorders()
    {
        BorderGeneration++;
        foreach(var entry in Borders.Values){entry.Dirty=true;entry.Waypoints.Clear();}
    }
    private sealed class BorderFault(string address) : Exception(address);
    private sealed class CheckedDirection
    {
        internal bool[] Bits=Array.Empty<bool>();
        internal int Remaining=-1;
        internal void Init(int count){if(Bits.Length==0){Bits=new bool[count];Remaining=count;}}
        internal bool Mark(int i)
        {
            if((uint)i>=(uint)Bits.Length)throw new BorderFault("0x0068AF10 reverse contact/index");
            if(Bits[i])return false;Bits[i]=true;Remaining--;return true;
        }
    }
    private static readonly byte[] NeighborTable={2,0,1,1,2,1,1,0,3,0,0,0,3,1,0,1,0,1,3,1,0,0,3,0,1,1,2,0,1,0,2,1};
    private static SourceQuadNode? SingleNeighbor(SourceQuadNode node,int d)
    {
        if((uint)d>3)throw new BorderFault("0x00688190 direction");
        if(node.Parent is not {} parent)return null; // native root quadrant 4
        int q=parent.Children.IndexOf(node);
        if((uint)q>3)throw new BorderFault("0x006881E4 parent/quadrant");
        int child=NeighborTable[q*8+d*2];
        if(NeighborTable[q*8+d*2+1]!=0)return parent.Children.Count==0?null:parent.Children[child];
        var neighbor=SingleNeighbor(parent,d);
        return neighbor is {Children.Count:>0}?neighbor.Children[child]??neighbor:neighbor;
    }
    private static void Descendants(SourceQuadNode node,int d,bool clock,List<SourceQuadNode> result)
    {
        if(node.Children.Count==0){result.Add(node);return;}
        var pair=d switch {0=>(0,1),1=>(1,3),2=>(3,2),3=>(2,0),_=>throw new BorderFault("0x00688190 direction")};
        if(clock)pair=(pair.Item2,pair.Item1);
        foreach(int i in new[]{pair.Item1,pair.Item2})
            Descendants(node.Children[i]??throw new BorderFault("0x00688190 null child"),d,clock,result);
    }
    internal static List<SourceQuadNode> SmallestNeighbors(SourceQuadNode node,int d)
    {
        var result=new List<SourceQuadNode>();
        if(SingleNeighbor(node,d) is {} next)Descendants(next,(d+2)%4,true,result);
        return result;
    }
    internal SourceBorderCombination RefreshBorders(MemoryMapContentType seedType,uint flags)
    {
        ulong key=((ulong)(uint)seedType<<32)|flags;
        if(!Borders.TryGetValue(key,out var combination))Borders.Add(key,combination=new());
        if(!combination.Dirty)return combination;
        if(!TypeSets.TryGetValue(seedType,out var seeds))TypeSets.Add(seedType,seeds=new());
        var output=new List<SourceBorderWaypoint>();
        var checkedNodes=new Dictionary<SourceQuadNode,CheckedDirection[]>();
        int marks=0;
        CheckedDirection[] Info(SourceQuadNode n)
        {
            if(!checkedNodes.TryGetValue(n,out var value))checkedNodes.Add(n,value=Enumerable.Range(0,4).Select(_=>new CheckedDirection()).ToArray());
            return value;
        }
        List<SourceQuadNode> Side(SourceQuadNode n,int d)
        {var side=SmallestNeighbors(n,d);Info(n)[d].Init(side.Count);return side;}
        void Mark(SourceQuadNode n,int d,int i){if(Info(n)[d].Mark(i))marks++;}
        MemoryMapContentType Type(SourceQuadNode n)=>n.Content?.Data?.Type??throw new BorderFault("0x0068AD2C/0x0068AE4C/0x0068AE62/0x0068AF30 null content");
        bool Masked(SourceQuadNode n)=>(flags&(1u<<(int)Type(n)))!=0;
        void Finish(){if(output.Count!=0)output[^1].End=true;}
        ActiveBorder=combination;BorderWalkStarted=true;
        try
        {
            foreach(var seed in seeds)
            while(Info(seed).Any(x=>x.Remaining!=0))
            {
                int direction=0,index=0;List<SourceQuadNode> side=new();bool found=false;
                for(;direction<4 && !found;direction++)
                {
                    if(Info(seed)[direction].Remaining==0)continue;
                    side=Side(seed,direction);
                    for(index=0;index<side.Count;index++)
                    {
                        if(Info(seed)[direction].Bits[index])continue;
                        Mark(seed,direction,index); // mark BEFORE type test, no same-type check
                        if(Masked(side[index])){found=true;break;}
                    }
                }
                if(!found)continue;
                direction--;
                output.Add(new(seed,side[index],direction){Start=true});index++;
                var current=seed;
                // M1-034: detect an actual repeated unchanged traversal state, never an arbitrary step limit.
                var visited=new HashSet<(SourceQuadNode,int,int,int,int,bool)>();
                while(true)
                {
                    if(!visited.Add((current,direction,index,marks,output.Count,output.Count!=0&&output[^1].End)))
                        throw new BorderFault("0x0068ADC4..0x0068B03A non-progressing walk");
                    if(index>=side.Count){direction=(direction+1)&3;side=Side(current,direction);index=0;continue;}
                    var next=side[index];
                    if(Type(next)==seedType)
                    {
                        direction=(direction+2)%4;side=Side(next,direction);
                        index=side.FindIndex(n=>ReferenceEquals(n,current));
                        if(index<0)throw new BorderFault("0x0068AF10 reverse contact missing");
                        Mark(next,direction,index);current=next;index++;continue;
                    }
                    bool stop=false;
                    if(Masked(next))
                    {
                        if(Info(current)[direction].Bits[index])
                        {
                            if(output.Count!=0&&!output[^1].End)output.Add(new(current,next,direction));
                            Finish();stop=true;
                        }
                        else output.Add(new(current,next,direction));
                    }
                    else Finish();
                    Mark(current,direction,index);
                    if(stop)break;index++;
                }
            }
            combination.Waypoints.Clear();combination.Waypoints.AddRange(output);combination.Dirty=false;
        }
        catch(BorderFault error)
        {StackDiagnostics.Write("M1-034 M11-047 skip border refresh at "+error.Message+"; prior map/cache retained");}
        finally{ActiveBorder=null;}
        return combination;
    }
}

// fidelity: M11-045, M13-030
internal sealed class SourceQuadNode(float x, float y, float z, float side, byte depth)
{
    internal float X = x, Y = y, Z = z, Side = side;
    internal SourceQuadBounds Bounds = new(x, y, side);
    internal byte Depth = depth;
    internal SourceQuadNode? Parent;
    internal List<SourceQuadNode?> Children = new();
    internal SourceNodeContent Content = new(new SourceMapData(MemoryMapContentType.Unknown, 0, 0));
    internal void ForceSet(SourceNodeContent content, SourceQuadProcessor proc)
    {
        var old = Content.Data.Type; bool oldNoArea = old == 0 || Children.Count != 0;
        Content = content.Data.Type == MemoryMapContentType.ObstacleChargerRemoved
            ? new SourceNodeContent(new SourceMapData(MemoryMapContentType.ClearOfObstacle, content.Data.Last, content.Data.Last)) : content;
        if (old != Content.Data.Type) proc.Changed(this, old, oldNoArea);
    }
    internal void Subdivide(SourceQuadProcessor proc)
    {
        float q = Side*.25f, s = Side*.5f;
        foreach (var (dx,dy) in new[] { (q,q),(q,-q),(-q,q),(-q,-q) })
        {
            var child = new SourceQuadNode(X+dx,Y+dy,Z,s,unchecked((byte)(Depth-1))) { Parent = this };
            Children.Add(child); child.ForceSet(Content, proc);
        }
        uint ts = Content.Data.Last;
        ForceSet(new SourceNodeContent(new SourceMapData(MemoryMapContentType.Unknown, ts, ts)), proc);
    }
    internal void ClearDescendants(SourceQuadProcessor proc)
    {
        foreach (var child in Children) { child!.ClearDescendants(proc); proc.Destroyed(child); }
        while (Children.Count != 0) Children.RemoveAt(Children.Count-1); // callback pass precedes reverse free pass
    }
    internal void DestroyNodes(SourceQuadProcessor proc)
    {
        for (int i = 0; i < Children.Count; i++)
        {
            var child = Children[i]!; child.DestroyNodes(proc); proc.Destroyed(child); Children[i] = null;
        } // vectors stay full of nulls, unlike ClearDescendants
    }
    internal void SwapChildrenAndContent(SourceQuadNode other, SourceQuadProcessor proc)
    {
        (Children,other.Children) = (other.Children,Children);
        foreach (var c in Children) c!.Parent = this;
        foreach (var c in other.Children) c!.Parent = other;
        var old = Content; ForceSet(other.Content, proc); other.ForceSet(old, proc);
    }
    internal void TryAutoMerge(SourceQuadProcessor proc)
    {
        if (Children.Count == 0 || Children.Any(c => c!.Children.Count != 0)) return;
        uint first = Children[^1]!.Content.Data.First, last = Children[^1]!.Content.Data.Last;
        bool equal = true;
        for (int i = 0; i < Children.Count-1; i++)
        {
            first = (uint)Math.Min((double)first, Children[i]!.Content.Data.First);
            last = (uint)Math.Max((double)last, Children[i]!.Content.Data.Last);
            equal &= Children[i]!.Content.Same(Children[i+1]!.Content);
        }
        if (!equal) return;
        var content = Children[0]!.Content;
        content.Data.First = first; content.Data.Last = last; // modifies shared child[0], not a new copy
        ClearDescendants(proc); ForceSet(content, proc);
    }
    internal bool CanOverride(SourceNodeContent incoming, int overlap)
    {
        int old = (int)Content.Data.Type, next = (int)incoming.Data.Type;
        if (next == 8) return true;
        if (old == 8) return next == 2 && overlap == 2;
        return next switch
        {
            10 => old == 9,
            9 => old is 0 or 1 or 2 or 5 or 6 or 9,
            1 => old is 0 or 1 or 5 or 6 || overlap == 2 && old is 3 or 4 or 7 or 9 or 10,
            5 => old == 4,
            _ => true,
        };
    }
    private bool CanOverrideAll(SourceNodeContent incoming, int overlap) => overlap == 2 && CanOverride(incoming, overlap) && Children.All(c => c!.CanOverrideAll(incoming, overlap));
    internal bool InsertRecursive(SourceNodeContent incoming, SourceQuadTree tree, SourceFastPolygon polygon)
    {
        if (ReferenceEquals(Content.Data,incoming.Data)) return false;
        var saved = Content;
        int overlap = tree.OverlapForTest is { } substitute
            ? substitute(this, polygon.Points.Select(p => new Vec2(p.X, p.Y)).ToArray())
            : polygon.GetOverlapType(this);
        if (overlap == 0) return false;
        Content.Data.Last = incoming.Data.Last;
        if (Children.Count != 0 && CanOverrideAll(incoming,overlap)) { ClearDescendants(tree.Processor); ForceSet(incoming,tree.Processor); }
        else
        {
            if (overlap != 2 && Children.Count == 0 && Depth != 0) Subdivide(tree.Processor);
            bool changed = false;
            if (Children.Count != 0)
            {
                foreach (var child in Children) changed |= child!.InsertRecursive(incoming,tree,polygon);
                TryAutoMerge(tree.Processor);
            }
            else if (CanOverride(incoming,overlap)) ForceSet(incoming,tree.Processor); // result ignored
            return changed | !Content.Same(saved);
        }
        return !Content.Same(saved);
    }
}

// fidelity: M11-045, M13-030
internal sealed class SourceQuadTree
{
    private readonly object _geometryGate = new();
    internal readonly SourceQuadProcessor Processor = new();
    internal readonly SourceQuadNode Root = new(0,0,1,160,4); // 0x00684C28..48
    internal SourceLogSink? SourceLog;
    internal Action<string>? Missing;
    internal Func<SourceQuadNode,Vec2[],int>? OverlapForTest;
    internal bool Dirty;
    internal Func<Vec2[],Vec2>? CentroidForTest;
    internal Func<Vec2,byte,bool>? UpgradeBodyForTest;
    internal void Insert(Vec2[] polygon, SourceMapData data, int maxShifts)
    {
        LogCallbackDeferral.RunGated(_geometryGate,()=>InsertCore(polygon,data,maxShifts));
    }
    private void InsertCore(Vec2[] polygon, SourceMapData data, int maxShifts)
    {
        // MemoryMap::Insert constructs FastPolygon before the tree's NaN/containment gate.
        var native = new SourceFastPolygon(polygon);
        if (!native.Valid && OverlapForTest is null) return;
        bool nan = false;
        foreach (var p in polygon) { nan |= float.IsNaN((float)p.X); nan |= float.IsNaN((float)p.Y); }
        if (nan)
        {
            SourceLog?.Invoke(EngineLogLevel.Error,null,"QuadTree.Insert.NaNPoly","Poly is not valid, at least one coordinate is NaN.");
            EngineErrorState.StoreAndMaybeBreak();
            Missing?.Invoke("MISSING: M11-045 sDumpCallstack(QuadTree::Insert) 0x006850A6 host native callstack unavailable");
            return;
        }
        if (!Contains(native))
        {
            if (data.Type == MemoryMapContentType.ObstacleChargerRemoved)
                SourceLog?.Invoke(EngineLogLevel.Warning,null,"QuadTree.Insert.RemovalPolyNotContained","Poly is not fully contained in root, removal does not cause expansion.");
            else Expand(CentroidForTest?.Invoke(polygon) ?? new Vec2(native.CenterX,native.CenterY),maxShifts,()=>Contains(native));
        }
        Dirty |= Root.InsertRecursive(new SourceNodeContent(data),this,native);
    }
    // fidelity: M11-047 -- 0x00689FAC..0x0068A2D6, snapshot before direct node inserts.
    internal int FillBorder(MemoryMapContentType seed,uint flags,SourceMapData data)
    {
        long start=System.Diagnostics.Stopwatch.GetTimestamp();
        int count=0;
        LogCallbackDeferral.RunGated(_geometryGate,()=>
        {
            var combination=Processor.RefreshBorders(seed,flags);
            if(combination.Dirty)return; // M1-034 failed refresh cannot publish a partial fill
            var seen=new HashSet<SourceQuadNode>();var points=new List<Vec2>();
            foreach(var waypoint in combination.Waypoints)
                if(seen.Add(waypoint.From))points.Add(new(waypoint.From.X,waypoint.From.Y));
            foreach(var point in points)
            {
                var root=Root; // reload for every saved centre; Insert result intentionally ignored
                root.InsertRecursive(new SourceNodeContent(data),this,new SourceFastPolygon(new[]{point}));
                count++;
            }
            Dirty=true; // FillBorderInternal's map+8 write, even with no points
        });
        RecordBorderTiming(start);
        return count;
    }
    // Static Profiler timer (OV11 row22); reuse the existing PrintTimerData owner.
    private static readonly object FillProfilerGate=new();
    private static readonly LifetimeProfiler FillProfiler=new();
    private static readonly LifetimeProfiler.Timer FillTimer=FillProfiler.CreateTimer("QuadTreeProcessor.FillBorder");
    private static long LastFillPrint=System.Diagnostics.Stopwatch.GetTimestamp();
    private void RecordBorderTiming(long start)
    {
        long elapsed=System.Diagnostics.Stopwatch.GetElapsedTime(start).Ticks/TimeSpan.TicksPerMillisecond;
        string? message=null;
        lock(FillProfilerGate)
        {
            FillTimer.Total=unchecked(FillTimer.Total+elapsed);FillTimer.Calls=unchecked(FillTimer.Calls+1);
            long now=System.Diagnostics.Stopwatch.GetTimestamp();
            if(System.Diagnostics.Stopwatch.GetElapsedTime(LastFillPrint,now).Ticks/TimeSpan.TicksPerMillisecond>5000)
            {
                LastFillPrint=now;
                FillProfiler.SourceLog=(_,_,_,text)=>message=text;
                FillProfiler.Print(FillTimer);
            }
        }
        if(message is not null)SourceLog?.Invoke(EngineLogLevel.Info,"Profiler","Profiler",message);
    }
    internal bool UpgradeRootLevel(Vec2 direction, byte limit)
    {
        if (Root.Depth == 255 || Root.Depth >= limit) return false;
        if (UpgradeBodyForTest is null)
        { Missing?.Invoke("MISSING: M11-048 UpgradeRootLevel body 0x006876FA..0x006879C0; root unchanged"); return false; }
        bool upgraded = UpgradeBodyForTest(direction,limit);
        if (upgraded) Root.Bounds = new(Root.X,Root.Y,Root.Side);
        if (upgraded) SourceLog?.Invoke(EngineLogLevel.Info,"QuadTree","QuadTree.UpdgradeRootLevel",
            string.Format(CultureInfo.InvariantCulture,"Root expanded to level {0}. Allowing {1:F2}m",Root.Depth,(double)(Root.Side*BitConverter.UInt32BitsToSingle(0x3A83126F))));
        return upgraded;
    }
    private bool Contains(SourceFastPolygon polygon)
    {
        foreach (var p in polygon.Points)
            if (!Root.Bounds.Contains(p)) return false;
        return true;
    }
    // fidelity: M11-046 — 0x00689D80..0x00689DBA, 0x00689F0A..0x00689F98.
    internal bool HasCollisionRayWithTypes(Vec2 from, Vec2 to, uint mask)
    {
        var polygon = new SourceFastPolygon(new[] { from, to });
        lock (_geometryGate) return HasCollisionRay(Root,polygon,mask);
    }
    private static bool HasCollisionRay(SourceQuadNode node, SourceFastPolygon polygon, uint mask)
    {
        bool selected = (mask & (1u << (int)node.Content.Data.Type)) != 0;
        if (!selected && node.Children.Count == 0) return false;
        int overlap = polygon.GetOverlapType(node);
        if (selected) return overlap != 0;
        if (overlap == 0) return false;
        foreach (var child in node.Children)
            if (HasCollisionRay(child!,polygon,mask)) return true;
        return false;
    }
    internal void Expand(Vec2 centroid, int limit, Func<bool> contains)
    {
        bool upgraded;
        do { upgraded = UpgradeRootLevel(centroid-new Vec2(Root.X,Root.Y),8); } while (upgraded && !contains());
        for (int i = 0; !contains() && i < limit; i++)
            Missing?.Invoke("MISSING: M11-048 ShiftRoot 0x00686DF8..0x0068743E; return ignored, root unchanged");
        if (!contains()) SourceLog?.Invoke(EngineLogLevel.Warning,null,"QuadTree.Expand.InsufficientExpansion",
            string.Format(CultureInfo.InvariantCulture,"Quad caused expansion, but expansion was not enough PolyCenter({0:F2}, {1:F2}), Root({2:F2},{3:F2}) with sideLen({4:F2}).",centroid.X,centroid.Y,Root.X,Root.Y,Root.Side));
        Dirty = true;
    }
}

// fidelity: M13-032, M13-031
internal sealed partial class SourceMapComponent
{
    internal readonly SortedDictionary<uint,MemoryMap> Maps = new();
    internal uint CurrentOrigin; // Unknown=0; constructor +38=1,+3C=-1,+40=0
    internal byte Enabled = 1;
    internal float BroadcastInterval = -1; // +0x3C, raw f32
    internal float BroadcastDeadline; // +0x40, raw f32
    internal SourceLogSink? SourceLog;
    internal Action<string>? Missing;
    internal MemoryMap? Current => Maps.GetValueOrDefault(CurrentOrigin); // null only while the host defers reconstruction/teardown.
    internal void CreateLocalizedMemoryMap(uint origin, Func<uint,bool> zombie)
    {
        foreach (var id in Maps.Keys.ToArray())
        {
            bool dead = zombie(id);
            SourceLog?.Invoke(EngineLogLevel.Event,null,dead ? "MapComponent.memory_map.deleting_zombie_map" : "MapComponent.memory_map.keeping_alive_map",unchecked((int)origin).ToString(CultureInfo.InvariantCulture));
            if (dead) Maps.Remove(id);
        }
        if (origin == 0) return;
        var map = new MemoryMap { SourceLog = SourceLog, Missing = Missing };
        Maps.TryAdd(origin,map); CurrentOrigin = origin;
    }
    internal void UpdateMapOrigins(uint old, uint next, Action updateObjects)
    {
        updateObjects();
        Maps.TryGetValue(old,out var oldMap);
        if (!Maps.ContainsKey(old)) Maps[old] = null!; // native operator[]
        if (!Maps.TryGetValue(next,out var newMap) || newMap is null)
        {
            SourceLog?.Invoke(EngineLogLevel.Error,null,"MapComponent.UpdateObjectOrigins.NullMapFound",$"Origin '{PoseOriginNames.NameOf(next)}' did not have a memory map. Creating empty");
            EngineErrorState.StoreAndMaybeBreak();
            Maps[next] = new MemoryMap { SourceLog = SourceLog, Missing = Missing };
        }
        Missing?.Invoke("MISSING: M11-048 MemoryMap merge vt+0xC 0x0067DF80 (including null old-map handling); merge not guessed");
        CurrentOrigin = next; Maps.Remove(old);
    }
}
