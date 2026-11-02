using Cozmo.Transport;
using Cozmo.Robot.Vision;
namespace Cozmo.Robot.Behavior;

// fidelity: M11-079
// Checked OV51: 0x005741DA..0x00574B08. Owned by AIComponent+8; Robot::Update thread only.
public sealed class AIInformationAnalyzer
{
    private sealed class Process
    {
        internal readonly HashSet<string> Enable = new(StringComparer.Ordinal), Disable = new(StringComparer.Ordinal);
        internal Action? Callable;
        internal bool Running => Disable.Count == 0 && Enable.Count != 0;
    }
    private readonly SortedDictionary<int, Process> _processes = new();
    private readonly SourceLogSink? _source;
    private Func<MemoryMap?>? _originalMap;
    private Func<bool>? _originalOwnerCurrent;
    // Analyzer+C. Empty from the constructor (0x005741CE/D2); replaced only by a run of the enabled process.
    private readonly List<InterestingEdgeRegion> _regions=new();
    internal IReadOnlyList<InterestingEdgeRegion> Regions=>_regions;
    internal void BindOriginalMap(Func<MemoryMap?> map,Func<bool> current){_originalMap=map;_originalOwnerCurrent=current;}
    internal bool? ActualBorderResultEmpty()=>_originalOwnerCurrent?.Invoke()==true ? _regions.Count==0 : null;
    internal AIInformationAnalyzer(SourceLogSink? source)
    {
        _source = source;
        _processes.Add(1, new Process { Callable = CalculateInterestingRegions });
    }
    public static string ProcessName(int process) => process switch { 0 => "Invalid", 1 => "CalculateInterestingRegions", _ => "ERROR" };
    private Process Get(int id)
    {
        if (!_processes.TryGetValue(id, out var process)) _processes.Add(id, process = new());
        return process;
    }
    public void AddEnableRequest(int process, string id) => Request(process, id, true, true);
    public void RemoveEnableRequest(int process, string id) => Request(process, id, true, false);
    public void AddDisableRequest(int process, string id) => Request(process, id, false, true);
    public void RemoveDisableRequest(int process, string id) => Request(process, id, false, false);
    private void Request(int process, string id, bool enable, bool add)
    {
        var entry = Get(process);
        var set = enable ? entry.Enable : entry.Disable;
        bool changed = add ? set.Add(id) : set.Remove(id);
        string kind = enable ? "Enable" : "Disable", verb = enable ? "enable" : "disable", gerund = enable ? "enabling" : "disabling";
        if (changed)
            _source?.Invoke(EngineLogLevel.Info, "AIInfoAnalysis", "AIInformationAnalyzer." + (add ? "Add" : "Remove") + kind + "Request",
                $"'{id}' " + (add ? "requested to " : "removed the request to ") + $"{verb} process '{ProcessName(process)}'");
        else
            _source?.Invoke(EngineLogLevel.Warning, null, "AIInformationAnalyzer.Add" + kind + "Request." + (add ? "DuplicatedID" : "IDNotFound"),
                $"{id} had " + (add ? $"already requested {gerund} " : $"not requested {gerund} ") + process.ToString(System.Globalization.CultureInfo.InvariantCulture) + (add ? "" : " before or had already removed the request."));
    }
    public bool IsProcessRunning(int process)
    {
        if (_processes.TryGetValue(process, out var entry)) return entry.Running;
        _source?.Invoke(EngineLogLevel.Warning, null, "AIInformationAnalyzer.IsProcessRunning.ProcessNotRegistered", $"Process '{ProcessName(process)}' is not even registered");
        return false;
    }
    internal void Update()
    {
        foreach (var entry in _processes.Values)
        {
            if (!entry.Running) continue;
            if (entry.Callable is { } run) run();
            else
            {
                _source?.Invoke(EngineLogLevel.Error, null, "AIInformationAnalyzer.Update", "Process wants to run, but no code provided");
                EngineErrorState.StoreAndMaybeBreak();
            }
        }
    }
    // AIInfoAnalysisProcCalculateInterestingRegions 0x00574FCC..0x00575054. Table 0x00C59AE0 enables types 0, 1 and 2: mask 7.
    private const uint BorderNeighborMask=7;
    private void CalculateInterestingRegions()
    {
        _regions.Clear(); // 0x00574FEC..0x00575004, from the back, before either query
        if(_originalMap is null)
        {SteppedBehavior.ReportMissing("M11-079 CalculateInterestingRegions: no MapComponent owner bound on this standalone analyzer");return;}
        var map=_originalOwnerCurrent?.Invoke()==true?_originalMap():null; // read once, kept for both calls (0x00574FD2..0x00574FDA)
        if(map is null)
        {StackDiagnostics.Write("M1-034 M11-079 skip null map read at 0x00575006/0x00575026; regions stay cleared");return;}
        try
        {
            foreach(var seed in new[]{MemoryMapContentType.InterestingEdge,MemoryMapContentType.ObstacleProx}) // 0x00575012 then 0x0057502A
                if(map.CalculateBorders(seed,BorderNeighborMask) is {} regions)_regions.AddRange(regions);
        }
        catch(Exception e) when(e is InvalidOperationException or ArgumentException)
        {SteppedBehavior.ReportMissing("M1-034 M11-079 CalculateInterestingRegions map query unavailable: "+e.Message);}
    }
}
