using Cozmo.Transport;
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
    private static void CalculateInterestingRegions()
    {
        // 0x00574FCC..0x00575054: clear results, GetBorders(mask 7, seed 9), then seed 6.
        // No invented empty result: the native map's refresh/waypoint owner and type-9 data are absent.
        SteppedBehavior.ReportMissing("M11-079/M11-089 CalculateInterestingRegions 0x00574FCC..0x00575054: persistent region vector, native GetBorders refresh/waypoint/neighbor producer and InterestingEdge data owner are unbuilt; mask 7 / seed 9 then 6 cannot be joined to the host map");
    }
}
