namespace Cozmo.Robot;
//Host availability failure, deliberately not a native ActionResult.
internal sealed class NativeActionUnavailable(string message):NotSupportedException(message);
