using System.Runtime.CompilerServices;
using System.Text.Json;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Behavior;

/// <summary>
/// What a behaviour constructor needs beyond the config: the engine's constructors take <c>(Robot&amp;, const Json::Value&amp;)</c>
/// (every case of <c>BehaviorContainer::CreateBehavior</c> 0x0059c888 passes the same two arguments). In this stack the robot reaches a
/// behaviour through <see cref="BehaviorContext"/> when it starts, so the construction context carries only the sub-systems a
/// config-built class wraps.
/// </summary>
// fidelity: M7-018
public sealed class BehaviorFactoryContext
{
    /// <summary>The face world and vision pipeline (the M14 layer), for the classes that act on faces.</summary>
    public VisionSystem? Vision { get; init; }
    /// <summary>The manipulation system (the M12/M13 layer), for the classes that dock, place or drive.</summary>
    public ManipulationSystem? Manipulation { get; init; }
    /// <summary>The engine's log: each line starts with its level (info, warning, error), as <see cref="CozmoEngine.LogLine"/> lines do.</summary>
    public Action<string>? Log { get; init; }
}

/// <summary>
/// <c>BehaviorClassFromString</c> 0x0076A2F4 and <c>IBehavior::ExtractBehaviorClassFromConfig</c> 0x005BBA74.
/// </summary>
// fidelity: M7-018
public static class BehaviorClassNames
{
    private static readonly Dictionary<string, BehaviorClass> ByName =
        Enum.GetValues<BehaviorClass>().ToDictionary(c => c.ToString(), c => c, StringComparer.Ordinal);

    [ThreadStatic] private static TextWriter? _cerr;

    /// <summary>
    /// The stream a miss writes to: the engine writes to <c>std::cerr</c> (GOT 0x0103FAD8, <c>_ZNSt6__ndk14cerrE</c>), here
    /// <see cref="Console.Error"/> unless a test redirects it on its own thread.
    /// </summary>
    public static TextWriter? CerrOverride { get => _cerr; set => _cerr = value; }

    /// <summary>
    /// <c>BehaviorClassFromString(const string&amp;)</c>: a hit returns the class (the byte at node+0x14, 0x0076AD64). A miss (an unknown, empty,
    /// absent or non-string <c>behaviorClass</c>) writes <c>error: string '&lt;s&gt;' is not a valid BehaviorClass value</c> and a newline to
    /// <c>std::cerr</c>, flushes it (0x0076ADD2), and returns 0 (0x0076ADD6), which is <see cref="BehaviorClass.Bouncer"/>
    /// (0x0076AD60..0x0076ADD6).
    /// </summary>
    public static BehaviorClass FromString(string s)
    {
        if (ByName.TryGetValue(s, out var c)) return c;
        WriteCerrError(s, "BehaviorClass");
        return 0;
    }

    /// <summary>The engine's enum-from-string miss line (0x0076AD74..0x0076ADD2, 0x0076CDDE..0x0076CE4E, 0x00764798..0x00764810): an error line naming the string and type, a newline, flushed.</summary>
    internal static void WriteCerrError(string s, string typeName)
    {
        var w = _cerr ?? Console.Error;
        w.Write("error: string '" + s + "' is not a valid " + typeName + " value");
        w.Write('\n');
        w.Flush();
    }

    /// <summary>
    /// <c>IBehavior::ExtractBehaviorClassFromConfig</c> 0x005BBA74: <c>json["behaviorClass"]</c>; when it <c>isString()</c> (0x005BBA7E..0x005BBA82)
    /// its <c>asCString()</c>, otherwise the empty string (the literal at 0x00BE3F00), then <see cref="FromString"/>.
    /// </summary>
    public static BehaviorClass ExtractFromConfig(JsonElement config)
    {
        string name = config.ValueKind == JsonValueKind.Object && config.TryGetProperty("behaviorClass", out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()! : "";
        return FromString(name);
    }
}

/// <summary>
/// <c>RobotDataLoader::LoadBehaviors</c> 0x005206BC and <c>IBehavior::ExtractBehaviorIDFromConfig</c> 0x005BB9D4: the behaviour config corpus read into
/// the <c>unordered_map&lt;BehaviorID, Json::Value const&gt;</c> at <c>RobotDataLoader</c>+0x1C.
/// </summary>
// fidelity: M7-018
public static class BehaviorConfigLoader
{
    /// <summary>The directory the loader resolves (0x005208BC, 0x27 characters), under the OBB's resource root.</summary>
    public const string RelativeDirectory = "config/engine/behaviorSystem/behaviors/";

    /// <summary>The ID a config without a <c>behaviorID</c> string takes (0x005BB9DE).</summary>
    public const string NoBehaviorIdSpecified = "IBeh.NoBehaviorIdSpecified";

    private static readonly Dictionary<string, BehaviorID> IdByName =
        Enum.GetValues<BehaviorID>().ToDictionary(c => c.ToString(), c => c, StringComparer.Ordinal);

    /// <summary>
    /// <c>BehaviorIDFromString</c> 0x0076B340: a hit returns the ID. A miss (0x0076CDDE..0x0076CE4E) writes <c>error: string '&lt;s&gt;' is not a valid BehaviorID value</c> and a newline
    /// to <c>std::cerr</c>, flushes it and returns 0, which is <see cref="BehaviorID.AcknowledgeFace"/>.
    /// </summary>
    public static BehaviorID IdFromString(string name)
    {
        if (IdByName.TryGetValue(name, out var id)) return id;
        BehaviorClassNames.WriteCerrError(name, "BehaviorID");
        return 0;
    }

    /// <summary>
    /// <c>ExtractBehaviorIDFromConfig</c>: <c>JsonTools::ParseString(config, "behaviorID", "IBeh.NoBehaviorIdSpecified")</c> (0x005BB9DE..0x005BB9F8) and
    /// <c>BehaviorIDFromString</c> (0x005BB9FE). A missing key (or, as the stack reads it, a value that is not a string) takes the default, which is not one of the 179 names, so it takes the miss path of <see cref="IdFromString"/>.
    /// </summary>
    public static BehaviorID ExtractIdFromConfig(JsonElement config)
    {
        string name = config.ValueKind == JsonValueKind.Object && config.TryGetProperty("behaviorID", out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()! : NoBehaviorIdSpecified;
        return IdFromString(name);
    }

    /// <summary><c>Json::Value::empty()</c>: true for null, an empty array and an empty object, false for every other value.</summary>
    public static bool IsEmpty(JsonElement json) => json.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => true,
        JsonValueKind.Array => json.GetArrayLength() == 0,
        JsonValueKind.Object => !json.EnumerateObject().Any(),
        _ => false,
    };

    /// <summary>The behaviour configs directory under an OBB root (the stack's <c>pathToResource(Scope 1, ...)</c>).</summary>
    public static string DirectoryFor(string obbRoot) =>
        Path.Combine(obbRoot, "assets", "cozmo_resources", "config", "engine", "behaviorSystem", "behaviors");

    /// <summary>
    /// <c>LoadBehaviors</c>: every <c>*.json</c> under the directory, recursively (<c>FilesInDirectory(path, true, ".json", true)</c>, 0x0052072E; the
    /// recursion is the reading the extraction gives the second boolean), in the order the directory enumeration returns them (the engine's is the
    /// platform's, BLOCKED_EXTERNAL; this stack sorts the paths ordinally). Per file: a read that fails logs the warning
    /// <c>RobotDataLoader.Behavior: Failed to read '%s'</c> (0x005207BE) and goes on; an empty value is skipped silently
    /// (<c>Json::Value::empty</c>, 0x0052075E); otherwise the ID is extracted and the config is inserted only if that ID is absent
    /// (<c>__node_insert_unique</c> 0x00520792): a duplicate keeps the first and drops the new one with no log (0x005207E6..0x00520808).
    /// The files are read as jsoncpp reads them: comments and trailing commas are tolerated.
    /// </summary>
    public static Dictionary<BehaviorID, JsonElement> Load(string obbRoot, Action<string>? log = null)
    {
        var map = new Dictionary<BehaviorID, JsonElement>();
        var dir = DirectoryFor(obbRoot);
        if (!Directory.Exists(dir)) return map;
        var options = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        foreach (var path in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.Ordinal))
        {
            JsonElement json;
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path), options);
                json = doc.RootElement.Clone();
            }
            catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
            {
                log?.Invoke($"warning: RobotDataLoader.Behavior: Failed to read '{path}'");
                continue;
            }
            if (IsEmpty(json)) continue;
            var id = ExtractIdFromConfig(json);
            map.TryAdd(id, json);                       // a duplicate ID: the first stays, no log
        }
        return map;
    }
}

/// <summary>What <see cref="BehaviorFactory.CreateBehavior"/> did.</summary>
public enum BehaviorCreateStatus
{
    /// <summary>An object was built and added to the container.</summary>
    Created,
    /// <summary>The class is above 0x4E: the engine's error path (0x0059C89A, 0x0059D404).</summary>
    ClassOutOfRange,
    /// <summary>
    /// The engine constructs this class, but this stack has no constructor that builds it from a config (its code is another layer's, or is built by
    /// hand per id elsewhere). MISSING is reported and no stand-in is made. This is a gap of this stack: the engine has no such outcome.
    /// </summary>
    NotImplemented,
}

/// <summary>
/// The 79-way table of <c>BehaviorContainer::CreateBehavior(BehaviorClass, Robot&amp;, const Json&amp;)</c> 0x0059C888 (<c>cmp r5,#0x4e; bhi</c> 0x0059C89A; <c>tbh</c>
/// 0x0059C8A4 over 79 halfword entries at 0x0059C8A8): per <see cref="BehaviorClass"/> ordinal the engine class, its constructor address, the
/// <c>operator new</c> size and the address of its case. Every case is <c>operator new(size)</c>, <c>ctor(obj, robot, config)</c> and a
/// <c>shared_ptr&lt;IBehavior&gt;</c>; <see cref="BehaviorClass.PlayAnim"/>'s constructor also takes a <c>bool = true</c>, and
/// <see cref="BehaviorClass.Wait"/> constructs a plain <c>IBehavior</c> and overwrites its vptr with <c>BehaviorWait</c>'s (GOT 0x0103EDF0,
/// 0x0059D09A..0x0059D0A4).
/// </summary>
// fidelity: M7-018
public sealed class BehaviorFactory
{
    /// <summary>One case of the table. <see cref="Construct"/> is null when this stack cannot build the class from a config.</summary>
    public sealed record Entry(BehaviorClass Class, string EngineClass, uint ConstructorAddress, uint AllocationSize, uint CaseAddress,
                               Func<BehaviorFactoryContext, JsonElement, IBehavior>? Construct = null);

    private static readonly (BehaviorClass Class, string EngineClass, uint Ctor, uint Size, uint Case)[] Rows =
    {
        new(BehaviorClass.Bouncer, "BehaviorBouncer", 0x5f12fc, 0x158, 0x59c946),
        new(BehaviorClass.BringCubeToBeacon, "BehaviorExploreBringCubeToBeacon", 0x5defb0, 0x138, 0x59c968),
        new(BehaviorClass.BuildPyramid, "BehaviorBuildPyramid", 0x5dbc88, 0x168, 0x59c98a),
        new(BehaviorClass.BuildPyramidBase, "BehaviorBuildPyramidBase", 0x5dca40, 0x168, 0x59c9ac),
        new(BehaviorClass.CantHandleTallStack, "BehaviorCantHandleTallStack", 0x5ecb10, 0x148, 0x59c9ce),
        new(BehaviorClass.CheckForStackAtInterval, "BehaviorCheckForStackAtInterval", 0x5d71f8, 0x148, 0x59c9f0),
        new(BehaviorClass.CubeLiftWorkout, "BehaviorCubeLiftWorkout", 0x5d7e50, 0x130, 0x59ca12),
        new(BehaviorClass.Dance, "BehaviorDance", 0x5ed438, 0x148, 0x59ca34),
        new(BehaviorClass.DevTurnInPlaceTest, "BehaviorDevTurnInPlaceTest", 0x5ca550, 0x138, 0x59ca56),
        new(BehaviorClass.DockingTestSimple, "BehaviorDockingTestSimple", 0x5cafb0, 0x290, 0x59ca78),
        new(BehaviorClass.DriveInDesperation, "BehaviorDriveInDesperation", 0x5d8ae0, 0x140, 0x59ca9a),
        new(BehaviorClass.DriveOffCharger, "BehaviorDriveOffCharger", 0x5c0980, 0x128, 0x59cabc),
        new(BehaviorClass.DrivePath, "BehaviorDrivePath", 0x5c0f38, 0x218, 0x59cade),
        new(BehaviorClass.DriveToFace, "BehaviorDriveToFace", 0x5da550, 0x128, 0x59cb00),
        new(BehaviorClass.EarnedSparks, "BehaviorEarnedSparks", 0x5dae98, 0x120, 0x59cb22),
        new(BehaviorClass.EnrollFace, "BehaviorEnrollFace", 0x5fcad4, 0x180, 0x59cb44),
        new(BehaviorClass.ExploreLookAroundInPlace, "BehaviorExploreLookAroundInPlace", 0x5e1d80, 0x1f8, 0x59cb64),
        new(BehaviorClass.ExploreVisitPossibleMarker, "BehaviorExploreVisitPossibleMarker", 0x5e3cd8, 0x128, 0x59cb84),
        new(BehaviorClass.ExpressNeeds, "BehaviorExpressNeeds", 0x5ee144, 0x140, 0x59cba4),
        new(BehaviorClass.FactoryCentroidExtractor, "BehaviorFactoryCentroidExtractor", 0x5cf80c, 0x210, 0x59cbc4),
        new(BehaviorClass.FactoryTest, "BehaviorFactoryTest", 0x5d0438, 0x370, 0x59cbe4),
        new(BehaviorClass.FeedingEat, "BehaviorFeedingEat", 0x5d5794, 0x150, 0x59cc04),
        new(BehaviorClass.FeedingSearchForCube, "BehaviorFeedingSearchForCube", 0x5d6c64, 0x128, 0x59cc24),
        new(BehaviorClass.FindFaces, "BehaviorFindFaces", 0x5c1914, 0x200, 0x59cc44),
        new(BehaviorClass.FireTruckAlarm, "BehaviorFireTruckAlarm", 0x5daf88, 0x120, 0x59cc64),
        new(BehaviorClass.FistBump, "BehaviorFistBump", 0x5f1d8c, 0x158, 0x59cc84),
        new(BehaviorClass.GuardDog, "BehaviorGuardDog", 0x5f28dc, 0x158, 0x59cca4),
        new(BehaviorClass.InteractWithFaces, "BehaviorInteractWithFaces", 0x5c1ee8, 0x140, 0x59ccc4),
        new(BehaviorClass.KnockOverCubes, "BehaviorKnockOverCubes", 0x5c2ea0, 0x168, 0x59cce4),
        new(BehaviorClass.LiftLoadTest, "BehaviorLiftLoadTest", 0x5d3f10, 0x228, 0x59cd04),
        new(BehaviorClass.LookAround, "BehaviorLookAround", 0x5c3e50, 0x168, 0x59cd24),
        new(BehaviorClass.LookForFaceAndCube, "BehaviorLookForFaceAndCube", 0x5ef804, 0x1a0, 0x59cd44),
        new(BehaviorClass.LookInPlaceMemoryMap, "BehaviorLookInPlaceMemoryMap", 0x5e482c, 0x178, 0x59cd64),
        new(BehaviorClass.OnConfigSeen, "BehaviorOnConfigSeen", 0x5db0e8, 0x140, 0x59cd84),
        new(BehaviorClass.OnboardingShowCube, "BehaviorOnboardingShowCube", 0x600ac0, 0x130, 0x59cda4),
        new(BehaviorClass.PeekABoo, "BehaviorPeekABoo", 0x5f62d0, 0x170, 0x59cdc4),
        new(BehaviorClass.PickUpAndPutDownCube, "BehaviorPickUpAndPutDownCube", 0x5db65c, 0x128, 0x59cde4),
        new(BehaviorClass.PickUpCube, "BehaviorPickUpCube", 0x5c64d4, 0x130, 0x59ce04),
        new(BehaviorClass.PlayAnim, "BehaviorPlayAnimSequence", 0x5bff24, 0x140, 0x59ce24),
        new(BehaviorClass.PlayAnimOnNeedsChange, "BehaviorPlayAnimOnNeedsChange", 0x5bfdd4, 0x140, 0x59ce46),
        new(BehaviorClass.PlayAnimWithFace, "BehaviorPlayAnimSequenceWithFace", 0x5c0630, 0x140, 0x59ce66),
        new(BehaviorClass.PlayArbitraryAnim, "BehaviorPlayArbitraryAnim", 0x5c0784, 0x140, 0x59ce86),
        new(BehaviorClass.PopAWheelie, "BehaviorPopAWheelie", 0x5c7430, 0x138, 0x59cea6),
        new(BehaviorClass.PounceOnMotion, "BehaviorPounceOnMotion", 0x5f7f90, 0x190, 0x59cec6),
        new(BehaviorClass.PutDownBlock, "BehaviorPutDownBlock", 0x5c7f98, 0x120, 0x59cee6),
        new(BehaviorClass.PyramidThankYou, "BehaviorPyramidThankYou", 0x5de048, 0x128, 0x59cf06),
        new(BehaviorClass.RequestGameSimple, "BehaviorRequestGameSimple", 0x5ea168, 0x228, 0x59cf26),
        new(BehaviorClass.RespondPossiblyRoll, "BehaviorRespondPossiblyRoll", 0x5de324, 0x150, 0x59cf46),
        new(BehaviorClass.RespondToRenameFace, "BehaviorRespondToRenameFace", 0x600694, 0x130, 0x59cf66),
        new(BehaviorClass.RollBlock, "BehaviorRollBlock", 0x5c8598, 0x138, 0x59cf86),
        new(BehaviorClass.SearchForFace, "BehaviorSearchForFace", 0x5c9158, 0x120, 0x59cfa6),
        new(BehaviorClass.Singing, "BehaviorSinging", 0x5ee8dc, 0x148, 0x59cfc6),
        new(BehaviorClass.StackBlocks, "BehaviorStackBlocks", 0x5c93d0, 0x138, 0x59cfe6),
        new(BehaviorClass.ThinkAboutBeacons, "BehaviorThinkAboutBeacons", 0x5e5af8, 0x130, 0x59d006),
        new(BehaviorClass.TrackLaser, "BehaviorTrackLaser", 0x5fa250, 0x1d8, 0x59d026),
        new(BehaviorClass.TurnToFace, "BehaviorTurnToFace", 0x5ca33c, 0x120, 0x59d046),
        new(BehaviorClass.VisitInterestingEdge, "BehaviorVisitInterestingEdge", 0x5e5f64, 0x198, 0x59d066),
        new(BehaviorClass.Wait, "IBehavior", 0x5bbb74, 0x120, 0x59d086),
        new(BehaviorClass.AcknowledgeFace, "BehaviorAcknowledgeFace", 0x602884, 0x140, 0x59d0b2),
        new(BehaviorClass.AcknowledgeObject, "BehaviorAcknowledgeObject", 0x602fa4, 0x168, 0x59d0d2),
        new(BehaviorClass.RamIntoBlock, "BehaviorRamIntoBlock", 0x60473c, 0x120, 0x59d0f2),
        new(BehaviorClass.ReactToCliff, "BehaviorReactToCliff", 0x604cd0, 0x128, 0x59d112),
        new(BehaviorClass.ReactToCubeMoved, "BehaviorAcknowledgeCubeMoved", 0x60219c, 0x130, 0x59d132),
        new(BehaviorClass.ReactToFrustration, "BehaviorReactToFrustration", 0x605904, 0x148, 0x59d152),
        new(BehaviorClass.ReactToImpact, "BehaviorReactToImpact", 0x60617c, 0x120, 0x59d172),
        new(BehaviorClass.ReactToMotorCalibration, "BehaviorReactToMotorCalibration", 0x60658c, 0x120, 0x59d192),
        new(BehaviorClass.ReactToOnCharger, "BehaviorReactToOnCharger", 0x606a18, 0x130, 0x59d1b2),
        new(BehaviorClass.ReactToPet, "BehaviorReactToPet", 0x606e98, 0x140, 0x59d1d2),
        new(BehaviorClass.ReactToPickup, "BehaviorReactToPickup", 0x607724, 0x128, 0x59d1f2),
        new(BehaviorClass.ReactToPlacedOnSlope, "BehaviorReactToPlacedOnSlope", 0x607fb8, 0x128, 0x59d212),
        new(BehaviorClass.ReactToPyramid, "BehaviorReactToPyramid", 0x608310, 0x120, 0x59d232),
        new(BehaviorClass.ReactToReturnedToTreads, "BehaviorReactToReturnedToTreads", 0x6084e4, 0x120, 0x59d252),
        new(BehaviorClass.ReactToRobotOnBack, "BehaviorReactToRobotOnBack", 0x6087b8, 0x120, 0x59d272),
        new(BehaviorClass.ReactToRobotOnFace, "BehaviorReactToRobotOnFace", 0x608a9c, 0x120, 0x59d292),
        new(BehaviorClass.ReactToRobotOnSide, "BehaviorReactToRobotOnSide", 0x608d38, 0x120, 0x59d2b2),
        new(BehaviorClass.ReactToRobotShaken, "BehaviorReactToRobotShaken", 0x609104, 0x130, 0x59d2d2),
        new(BehaviorClass.ReactToSparked, "BehaviorReactToSparked", 0x6097e0, 0x120, 0x59d2f2),
        new(BehaviorClass.ReactToStackOfCubes, "BehaviorReactToStackOfCubes", 0x609880, 0x120, 0x59d312),
        new(BehaviorClass.ReactToUnexpectedMovement, "BehaviorReactToUnexpectedMovement", 0x609a58, 0x120, 0x59d332),
    };

    private static readonly Dictionary<BehaviorClass, Func<BehaviorFactoryContext, JsonElement, IBehavior>> Constructors = new()
    {
        // fidelity: M7-018, M8-005
        // case 0x26: BehaviorPlayAnimSequence(robot, config, true): config animTriggers and num_loops (0x005bff24..0x005c004c). This is also
        // the whole of the shipped Hiccup behaviour (reactions/hiccup.json: animTriggers ["Hiccup"]); the timed hiccup/cure logic is the M10
        // strategy ReactionTriggerStrategyHiccup, which is not this class.
        [BehaviorClass.PlayAnim] = (ctx, json) => PlayAnimBehavior.FromConfig(json, ctx.Log),
        // case 0x19: BehaviorFistBump(robot, config), 0x158 bytes (0x0059cc84..0x0059cca2).
        [BehaviorClass.FistBump] = (ctx, json) => FistBumpBehavior.FromConfig(json, ctx.Vision, ctx.Manipulation),
        // case 0x4C: BehaviorReactToSparked(robot, config), 0x120 bytes (0x0059d2f2..0x0059d310).
        [BehaviorClass.ReactToSparked] = (ctx, json) => new ReactToSparkedBehavior(BehaviorConfigLoader.ExtractIdFromConfig(json).ToString()),
    };

    /// <summary>The 79 cases in ordinal order.</summary>
    public static IReadOnlyList<Entry> Table { get; } = Rows
        .Select(r => new Entry(r.Class, r.EngineClass, r.Ctor, r.Size, r.Case, Constructors.GetValueOrDefault(r.Class)))
        .ToArray();

    /// <summary>
    /// <c>CreateBehavior</c> 0x0059C888: a class above 0x4E builds nothing and takes the error path (Error <c>behaviorContainer.CreateBehavior.Failed</c>,
    /// <c>Failed to create Behavior of type '%s'</c>, 0x0059D350..0x0059D3FE); otherwise the case builds the object and, when it is non-null,
    /// <c>AddToFactory(container, behavior)</c> stores it (0x0059D350). A class this stack cannot build from a config is
    /// <see cref="BehaviorCreateStatus.NotImplemented"/> and is reported MISSING once per class.
    /// </summary>
    public IBehavior? CreateBehavior(BehaviorContainer container, BehaviorClass cls, BehaviorFactoryContext ctx, JsonElement config,
                                     out BehaviorCreateStatus status)
    {
        if ((byte)cls > 0x4E)
        {
            ctx.Log?.Invoke($"error: behaviorContainer.CreateBehavior.Failed: Failed to create Behavior of type '{cls}'");
            status = BehaviorCreateStatus.ClassOutOfRange;
            return null;
        }
        var entry = Table[(int)cls];
        if (entry.Construct is null)
        {
            SteppedBehavior.ReportMissing($"BehaviorContainer::CreateBehavior case 0x{entry.CaseAddress:X} ({cls} -> {entry.EngineClass}, constructor 0x{entry.ConstructorAddress:X}, {entry.AllocationSize} bytes): class not implemented. This stack has no constructor that builds {entry.EngineClass} from its config (the class is another layer's, or is built by hand per behaviour id outside the factory); no stand-in is made");
            status = BehaviorCreateStatus.NotImplemented;
            return null;
        }
        var behavior = entry.Construct(ctx, config);
        container.AddToFactory(behavior);
        status = BehaviorCreateStatus.Created;
        return behavior;
    }
}

/// <summary>
/// <c>BehaviorContainer</c> (<c>BehaviorContainer::BehaviorContainer(Robot&amp;, const unordered_map&lt;BehaviorID, Json const&gt;&amp;)</c> 0x0059C324): the
/// id-to-behaviour map at +4, a <c>std::map</c> (its iteration is in <see cref="BehaviorID"/> order, which is the order
/// <c>FindBehaviorByExecutableType</c> 0x0059C836 walks it in).
///
/// The constructor walks the config map's nodes (0x0059C34C..0x0059C428; the engine's order is the libc++ <c>unordered_map</c> node order, which depends
/// on insertion order and the platform's directory order and is not reproduced: this stack walks in insertion order, so only the order of log lines
/// differs): an empty config logs the warning <c>Robot.LoadBehavior: Failed to read behavior file for behavior id '%s'</c> and is skipped
/// (0x0059C368..0x0059C386); otherwise the class is extracted (<see cref="BehaviorClassNames.ExtractFromConfig"/>, 0x0059C3B6) and
/// <see cref="BehaviorFactory.CreateBehavior"/> runs (0x0059C3C6); a null result logs the error <c>Robot.LoadBehavior.CreateFailed: Failed to create a
/// behavior for behavior id '%s'</c> (0x0059C3DE..0x0059C416; the engine also sets its global error flag and, if set, breaks into the debugger: not
/// modelled). A non-empty config map then calls <see cref="VerifyExecutableBehaviors"/> (0x0059C428..0x0059C42E).
///
/// <b>Not built, and why.</b> After that the engine subscribes the container's <c>HandleMessage&lt;RequestAllBehaviorsList&gt;</c> (game-to-engine tag
/// 0x9C, 0x0059C44C..0x0059C676) when the robot has an external interface; this stack has no game-to-engine channel. <c>Robot::Robot</c> builds
/// <b>two</b> containers over the same config map: the BehaviorManager's (robot+0x44, constructor 0x005A08D6) and the BehaviorSystemManager's
/// (robot+0x48, 0x005A5812), so every behaviour object is constructed twice. Only the first is built here: the second manager has no direct caller of
/// its Update, InitConfiguration, InitializeEventHandlers, GetCurrentBehavior or FindBehaviorByID (a vtable-only user was not ruled out), its use is
/// UNKNOWN, and a second set of behaviour objects with no consumer would only duplicate every object and its log lines. That is reported MISSING.
/// </summary>
// fidelity: M7-018
public sealed class BehaviorContainer
{
    private readonly SortedDictionary<BehaviorID, IBehavior> _behaviors = new();
    private readonly BehaviorFactoryContext _ctx;

    /// <summary>The id-to-behaviour map (+4), in <see cref="BehaviorID"/> order.</summary>
    public IReadOnlyDictionary<BehaviorID, IBehavior> Behaviors => _behaviors;

    /// <summary>How many configs did not create a behaviour (the engine's null result, or a class this stack cannot build).</summary>
    public int NotCreated { get; private set; }

    public BehaviorContainer(BehaviorFactory factory, BehaviorFactoryContext ctx, IEnumerable<KeyValuePair<BehaviorID, JsonElement>> configs)
    {
        _ctx = ctx;
        int count = 0;
        foreach (var (id, json) in configs)
        {
            count++;
            if (BehaviorConfigLoader.IsEmpty(json))
            {
                ctx.Log?.Invoke($"warning: Robot.LoadBehavior: Failed to read behavior file for behavior id '{id}'");
                continue;
            }
            var cls = BehaviorClassNames.ExtractFromConfig(json);
            var behavior = factory.CreateBehavior(this, cls, ctx, json, out var status);
            if (behavior is null)
            {
                NotCreated++;
                // only the engine's own null (a class above 0x4E) is the engine's error; a class this stack cannot build is reported MISSING by the factory
                if (status == BehaviorCreateStatus.ClassOutOfRange)
                    ctx.Log?.Invoke($"error: Robot.LoadBehavior.CreateFailed: Failed to create a behavior for behavior id '{id}'");
            }
        }
        if (count != 0) VerifyExecutableBehaviors();
        SteppedBehavior.ReportMissing("Robot::Robot builds a second BehaviorContainer (robot+0x48, BehaviorSystemManager, constructor 0x005A5812) over the same configs, so the engine constructs every behaviour twice; only the BehaviorManager's (robot+0x44) is built here: the second manager's use is UNKNOWN (no direct caller of its Update, InitConfiguration, InitializeEventHandlers, GetCurrentBehavior or FindBehaviorByID)");
    }

    /// <summary>
    /// <c>BehaviorContainer::AddToFactory</c> 0x0059E90C: reads the ID from the object (+0x3C, 0x0059E91C) and inserts it into the map only when that ID is absent
    /// (<c>emplace_unique</c>, 0x0059E936). An insert logs the Info line <c>behaviorContainer::AddToFactory: Added new behavior '%s' %p</c> (channel
    /// <c>Unnamed</c>, 0x0059E964; the pointer is shown as the object's identity hash). A duplicate ID logs nothing, replaces nothing and the new
    /// object is still returned to the caller (0x0059E98A..0x0059E998).
    /// </summary>
    public IBehavior AddToFactory(IBehavior behavior)
    {
        var id = BehaviorConfigLoader.IdFromString(behavior.Id);
        if (_behaviors.TryAdd(id, behavior))
            _ctx.Log?.Invoke($"info: [Unnamed] behaviorContainer::AddToFactory: Added new behavior '{id}' 0x{RuntimeHelpers.GetHashCode(behavior):X8}");
        return behavior;
    }

    /// <summary>
    /// <c>BehaviorContainer::VerifyExecutableBehaviors</c> 0x0059C584: walks the map and fills a <b>local</b> <c>std::map&lt;ExecutableBehaviorType, BehaviorID&gt;</c> (a
    /// later behaviour with the same executable type overwrites), which is destroyed at 0x0059C61E. It logs nothing, raises nothing, writes no member and returns
    /// nothing: it has no observable effect, so there is nothing to do.
    /// </summary>
    public void VerifyExecutableBehaviors() { }

    /// <summary>
    /// The engine's path for a real robot: <see cref="BehaviorConfigLoader.Load"/> (the <c>RobotDataLoader</c>'s map) then the container over it.
    /// </summary>
    public static BehaviorContainer LoadShipped(string obbRoot, BehaviorFactoryContext ctx) =>
        new(new BehaviorFactory(), ctx, BehaviorConfigLoader.Load(obbRoot, ctx.Log));
}
