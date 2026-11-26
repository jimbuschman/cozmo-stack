using System.Text.Json;
using Cozmo.Robot.Behavior;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// BEHAVIOR-FACTORY-OWNERS-1: the production startup (CozmoRobot -> BindNativeBehaviorManager -> NativeBehaviorRuntime, the same path the MCP
/// session takes) over the shipped OBB behaviour configs. The expected IDs are read here from the OBB files themselves, not from the stack's loader.
/// </summary>
[Collection("ObjectID process statics")]
public class BehaviorFactoryOwnersTests
{
    private static string Resources()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null)
        {
            var root = Path.Combine(d.FullName, "re-analysis", "obb", "assets", "cozmo_resources");
            if (Directory.Exists(Path.Combine(root, "config", "engine", "behaviorSystem"))) return root;
            d = d.Parent;
        }
        throw new DirectoryNotFoundException("shipped behavior resources");
    }

    /// <summary>(behaviorID, behaviorClass) of every shipped behaviour config, read straight from the OBB files.</summary>
    private static List<(string Id, string Class)> ShippedConfigs()
    {
        var dir = Path.Combine(Resources(), "config", "engine", "behaviorSystem", "behaviors");
        var options = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        var list = new List<(string, string)>();
        foreach (var file in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file), options);
            list.Add((doc.RootElement.GetProperty("behaviorID").GetString()!, doc.RootElement.GetProperty("behaviorClass").GetString()!));
        }
        return list;
    }

    // reach: production startup composition (offline transport); no robot traffic is asserted.
    [Fact]
    public void ProductionStartupSuppliesEveryOwnerAndBuildsTheSevenGuardedClasses()
    {
        var guarded = new Dictionary<string, Type>
        {
            ["ReactToCliff"] = typeof(ReactToCliffBehavior),
            ["ReactToImpact"] = typeof(ReactToImpactBehavior),
            ["ReactToPlacedOnSlope"] = typeof(ReactToPlacedOnSlopeBehavior),
            ["ReactToUnexpectedMovement"] = typeof(ReactToUnexpectedMovementBehavior),
            ["AcknowledgeFace"] = typeof(AcknowledgeFaceBehavior),
            ["AcknowledgeObject"] = typeof(AcknowledgeObjectBehavior),
            ["DriveOffCharger"] = typeof(DriveOffChargerBehavior),
        };
        var shipped = ShippedConfigs().Where(c => guarded.ContainsKey(c.Class)).ToList();
        // the OBB ships eight configs of these seven classes (DriveOffCharger twice: DriveOffCharger and Hiking_DriveOffCharger)
        Assert.Equal(8, shipped.Count);
        Assert.Equal(guarded.Keys.OrderBy(k => k), shipped.Select(c => c.Class).Distinct().OrderBy(k => k));

        using var rig = new Rig(resources: Resources());
        var native = rig.Robot.Engine.Robot!;
        var runtime = native.NativeBehaviorRuntime!;
        // the three owners the factory guards: Vision (the canonical one), Manipulation (built whenever Vision is) and the native Robot
        Assert.True(rig.Robot.IsCanonicalVision(rig.Robot.Vision));
        Assert.NotNull(runtime.Manipulation);
        Assert.Same(native, rig.Robot.Engine.Robot);

        foreach (var (id, cls) in shipped)
        {
            Assert.True(runtime.Container.Behaviors.TryGetValue(Enum.Parse<BehaviorID>(id), out var built), $"{id} ({cls}) was not created");
            Assert.IsType(guarded[cls], built);
            Assert.Equal(id, built!.Id);
        }
    }

    // reach: production startup composition. Every shipped config the factory does not build belongs to a SCOPE.md C, D or E item
    // (the expected sets below are SCOPE.md's lists, 2026-11-25), so no SCOPE.md B class comes back NotImplemented.
    [Fact]
    public void EveryConfigNotCreatedInProductionIsOutsideScopeB()
    {
        var scopeC = new[] { "RamIntoBlock" };
        var scopeD = new[] { "PyramidThankYou", "ReactToPyramid", "RespondPossiblyRoll", "ReactToStackOfCubes", "RequestGameSimple", "FeedingEat",
            "FeedingSearchForCube", "OnboardingShowCube", "ExploreVisitPossibleMarker", "PickUpAndPutDownCube" };
        var scopeE = new[] { "FactoryTest", "FactoryCentroidExtractor", "LiftLoadTest", "DockingTestSimple" };
        var outside = scopeC.Concat(scopeD).Concat(scopeE).ToHashSet();

        using var rig = new Rig(resources: Resources());
        var container = rig.Robot.Engine.Robot!.NativeBehaviorRuntime!.Container;
        var notCreated = ShippedConfigs().Where(c => !container.Behaviors.ContainsKey(Enum.Parse<BehaviorID>(c.Id))).ToList();
        Assert.Equal(container.NotCreated, notCreated.Count);
        foreach (var (id, cls) in notCreated)
        {
            // FindFaces is a B class, but this config is the Feeding activity's: ActivityFeeding::ActivityFeeding looks up BehaviorID 0x11
            // (FeedingFindFacesSevere) at 0x005AA604..0x005AA606, and the Feeding activity is SCOPE.md D with FeedingEat and FeedingSearchForCube.
            if (cls == "FindFaces") { Assert.Equal("FeedingFindFacesSevere", id); continue; }
            Assert.True(outside.Contains(cls), $"{id} ({cls}) is not created in production and is not a SCOPE.md C/D/E class");
        }
        Assert.Equal(17, (int)BehaviorID.FeedingFindFacesSevere);
    }
}
