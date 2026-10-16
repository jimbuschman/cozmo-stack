using System.Globalization;
using System.Text;
using Cozmo.Robot.Behavior;
using Cozmo.Transport;

namespace Cozmo.Robot;

internal sealed partial class RobotManager
{
    // fidelity: M8-022 — ctor 0x0052E504..516; load 0x0052E9A0..EA20.
    private readonly HashSet<AnimationTrigger> _dasBlacklistedTriggers = new();
    private readonly object _dasBlacklistGate = new();
    internal const string DasConfigRelativePath = "config/engine/das_event_config.json";
    private static readonly Dictionary<string, AnimationTrigger> TriggerNames = Enum.GetNames<AnimationTrigger>()
        .ToDictionary(name => Convert.ToHexString(Encoding.UTF8.GetBytes(name)), name => Enum.Parse<AnimationTrigger>(name), StringComparer.Ordinal);
    internal Stream? CerrOverride;
    internal IReadOnlySet<AnimationTrigger> DasBlacklistedTriggers
    { get { lock (_dasBlacklistGate) return new HashSet<AnimationTrigger>(_dasBlacklistedTriggers); } }

    // The loader's +0x220 starts null. Parse false retains the parser's partial root.
    internal static FirmwareJsonValue ReadDasEventConfig(string? resourceRoot, CozmoEngine engine)
    {
        FirmwareJsonValue root = new();
        if (resourceRoot is null)
        {
            SteppedBehavior.ReportMissing("M8-022/M3-062: no host resource root supplied for scope-1 config/engine/das_event_config.json; native file read has no path recipient");
            return root;
        }
        string path = resourceRoot + "/" + DasConfigRelativePath;
        byte[] bytes;
        try { bytes = File.ReadAllBytes(path); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { bytes = Array.Empty<byte>(); }
        bool ok;
        try
        {
            ok = Json.TryParseFirst(bytes, out var document);
            root = document.RootElement;
        }
        // fidelity: M1-034, M8-022 — the whole failed config is the smallest lost unit.
        catch (JsonRuntimeError)
        {
            Skip("reader depth > 1000 at 0x008E0D7A", path);
            return root;
        }
        catch (JsonMissingSource e)
        {
            SteppedBehavior.ReportMissing("M8-022 JSON reader dependency: " + e.Message);
            return root;
        }
        if (!ok)
        {
            engine.EmitSourceLog(EngineLogLevel.Error, null, "DataPlatform.readAsJson", $"Failed to read [{path}]");
            EngineErrorState.StoreAndMaybeBreak();
            SteppedBehavior.ReportMissing($"M8-022/M5-051 readAsJson 0x0084C12A..0x0084C15C for {path}: conditional Unnamed Debug Json reader errors [%s] requires unread getFormattedErrorMessages bytes; preserve partial root, no invented debug text");
            engine.EmitSourceLog(EngineLogLevel.Error, null, "RobotDataLoader.DasEventConfigJsonNotFound", $"DAS Event Json config file {DasConfigRelativePath} not found or failed to parse");
            EngineErrorState.StoreAndMaybeBreak();
        }
        return root;
    }

    // fidelity: M8-022, M8-025 — 0x0052E744..7B0. This is the checked tail, not all of engine Init.
    internal void InitCheckedDasTail(FirmwareJsonValue config, Func<long> nowNs)
    {
        long start = nowNs();
        SteppedBehavior.ReportMissing("M1-054/M8-022 RobotManager.Init vslot+8 at 0x0052E76A: native MessageHandler init/config recipient is unread; existing Initialised flag is retained, not a complete native Init");
        _engine.Handler.Initialised = true;
        SteppedBehavior.ReportMissing("M1-054/M8-022 RobotManager.BroadcastAvailableAnimations at 0x0052E76E: body/payload recipient unread; no fabricated broadcast");
        LoadDasBlacklistedAnimationTriggers(config);
        long elapsedMs = unchecked(nowNs() - start) / 1_000_000L;
        _engine.EmitSourceLog(EngineLogLevel.Event, null, "robot.init.time_spent_ms", elapsedMs.ToString(CultureInfo.InvariantCulture));
        // Caller starts the existing FirmwareUpdater.LoadHeader after this event (0x0052E7F8).
    }

    internal void LoadDasBlacklistedAnimationTriggers(FirmwareJsonValue root)
    {
        var staged = new HashSet<AnimationTrigger>();
        try
        {
            FirmwareJsonValue entries = new();
            if (root.Kind is not (FirmwareJsonKind.Object or FirmwareJsonKind.Null))
                throw new JsonLogicError("const root lookup throw 0x008EB06E");
            if (root.Kind == FirmwareJsonKind.Object && root.Members.TryGetValue(Convert.ToHexString("blacklisted_animation_triggers"u8), out var member)) entries = member;
            for (uint index = 0; index < (uint)Json.Size(entries); index++)
            {
                if (index > int.MaxValue) throw new JsonLogicError("negative signed index throw 0x008EA95C");
                var name = Json.AsStringBytes(Json.Index(entries, index));
                if (!TriggerNames.TryGetValue(Convert.ToHexString(name), out var trigger))
                {
                    var stderr = CerrOverride ?? Console.OpenStandardError();
                    stderr.Write("error: string '"u8);
                    stderr.Write(name);
                    stderr.Write("' is not a valid AnimationTrigger value\n"u8);
                    stderr.Flush();
                    trigger = 0; // FromString miss 0x00764810: AcknowledgeFaceInitPause, inserted normally.
                }
                staged.Add(trigger);
            }
        }
        catch (JsonLogicError e)
        {
            Skip("0x008EB06E / 0x008EA7D8 / 0x008E4A04 / 0x008EA95C: " + e.Message, DasConfigRelativePath);
            return;
        }
        lock (_dasBlacklistGate) _dasBlacklistedTriggers.UnionWith(staged); // no clear; unique insert.
    }

    private static void Skip(string site, string path) => SteppedBehavior.ReportMissing($"M1-034/M8-022: skipping malformed DAS config {path}; prior blacklist retained; {site}");
}
