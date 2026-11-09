using Cozmo.Protocol;
using Cozmo.Robot.Behavior;

namespace Cozmo.Robot.Manipulation;

// fidelity: M12-015, M15-022, M2-002
internal static class GroundPlacement
{
    internal const string ReactionLockName = "placeOnGroundAction";
    internal const float TimeoutSec = 30f;
    internal static readonly ReactionLockTable ReactionLockTable = new(ReactionLockName, "0xC55A21", new[] { 8 });
    internal static PlaceObjectOnGround Message(bool flag = false) => new()
    {
        RelX = 0, RelY = 0, RelAngle = 0,
        SpeedMmps = 100, AccelMmps2 = 200, DecelMmps2 = 500, Field6 = flag,
    };
}
