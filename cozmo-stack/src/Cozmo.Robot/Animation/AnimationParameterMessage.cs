namespace Cozmo.Robot.Animation;
// fidelity: M5-062
internal sealed record AnimationParameterMessage(byte[] Names, float[] Values, bool SetUnspecifiedToDefault);
