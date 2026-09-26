using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// Many tests return early, and so pass, when the shipped assets they need are absent: the unpacked OBB and the
/// fiducial-marker library (built from the user's own libcozmoEngine.so, never committed). On a machine without
/// them the suite reports green while those tests never ran. That is how two M10 regressions reached main
/// (b85decd). This test makes the absence a failure.
///
/// A run that deliberately has no assets sets COZMO_TESTS_WITHOUT_ASSETS=1, and says so in its report.
/// </summary>
public class AssetPresenceTests
{
    [Fact]
    public void TheShippedAssetsTheTestsNeedArePresent()
    {
        if (Environment.GetEnvironmentVariable("COZMO_TESTS_WITHOUT_ASSETS") == "1") return;

        var missing = new List<string>();
        if (ObbRoot() is null)
            missing.Add("the unpacked OBB: no re-analysis/obb/assets/cozmo_resources/assets/animationGroups in any directory above the test binaries");
        if (MarkerLibrary.EmbeddedOrNull is null)
            missing.Add("the marker library: Cozmo.Robot was built without Vision/Data/marker_nn_library.bin; the build extracts it from "
                        + "resources/lib/armeabi-v7a/libcozmoEngine.so at the repo root when python and that file are present");

        Assert.True(missing.Count == 0,
            "asset-dependent tests skip themselves silently without these, so a green run would not mean they passed:\n- "
            + string.Join("\n- ", missing)
            + "\nProvide them, or set COZMO_TESTS_WITHOUT_ASSETS=1 for a run that knowingly has none.");
    }

    // the same search the asset-dependent tests use
    private static string? ObbRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null)
        {
            var r = Path.Combine(d.FullName, "re-analysis", "obb");
            if (Directory.Exists(Path.Combine(r, "assets", "cozmo_resources", "assets", "animationGroups"))) return r;
            d = d.Parent;
        }
        return null;
    }
}
