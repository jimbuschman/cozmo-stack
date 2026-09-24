using Cozmo.Protocol;

namespace Cozmo.Robot;

/// <summary>
/// A colour for one of the robot's lights, held as 8-bit channels and packed to the wire on demand.
///
/// The alpha channel is here because the engine's <c>ColorRGBA</c> has one and the packing reads it: it
/// does not fade anything, it decides bit 15 of the light word. Every colour in every shipped light
/// config carries a non-zero alpha, so the engine sets that bit on everything it sends, black included.
/// </summary>
public readonly record struct LedColor(byte R, byte G, byte B, byte A = 255)
{
    /// <summary>Unlit. Still alpha 255, as the shipped configs write their off-colours ([0,0,0,255]).</summary>
    public static readonly LedColor Off = new(0, 0, 0);
    public static readonly LedColor Red = new(255, 0, 0);
    public static readonly LedColor Green = new(0, 255, 0);
    public static readonly LedColor Blue = new(0, 0, 255);
    public static readonly LedColor White = new(255, 255, 255);
    public static readonly LedColor Yellow = new(255, 255, 0);
    public static readonly LedColor Cyan = new(0, 255, 255);
    public static readonly LedColor Magenta = new(255, 0, 255);

    /// <summary>
    /// Packs to the 16-bit value the robot's LightState carries: five bits of red at bit 10, green at
    /// bit 5, blue at bit 0, and bit 15 set when alpha is non-zero. Read from the engine at
    /// <c>CubeLightComponent::SendTransitionMessage</c> 0x00638056; see <see cref="LightState.Rgb"/>.
    /// </summary>
    public ushort Packed => LightState.Rgb(R, G, B, A);

    public static LedColor FromHex(string hex)
    {
        var h = hex.TrimStart('#');
        if (h.Length != 6) throw new ArgumentException("a colour is six hex digits, optionally with a leading #", nameof(hex));
        return new LedColor(Convert.ToByte(h[..2], 16), Convert.ToByte(h.Substring(2, 2), 16), Convert.ToByte(h[4..], 16));
    }

    public override string ToString() => A == 255 ? $"#{R:X2}{G:X2}{B:X2}" : $"#{R:X2}{G:X2}{B:X2}{A:X2}";
}

/// <summary>
/// Cozmo's lights: the three backpack LEDs and the infrared headlight.
///
/// None of these produce an acknowledgement or appear in the robot's state stream, so nothing here can
/// report more than that the command was sent. That is stated plainly rather than dressed up as success;
/// the backpack is verified by looking at the robot, and the headlight by looking at a camera frame.
/// </summary>
public sealed class CozmoLights
{
    private readonly CozmoRobot _robot;
    internal CozmoLights(CozmoRobot robot) => _robot = robot;

    // fidelity: M1-025, M1-015
    /// <summary>Back to the state right after construction, for a removed robot (CB33, CC26, CC27): nothing recorded as sent.</summary>
    internal void ResetToConstructed()
    {
        Backpack = default;
        HeadlightOn = false;
    }

    /// <summary>The last backpack colours that were sent, so a caller can read back what it asked for.</summary>
    public (LedColor Top, LedColor Middle, LedColor Bottom) Backpack { get; private set; }
    /// <summary>Whether the headlight was last told to be on. The robot does not report its state.</summary>
    public bool HeadlightOn { get; private set; }

    /// <summary>Sets the three backpack LEDs to steady colours.</summary>
    public void SetBackpack(LedColor top, LedColor middle, LedColor bottom)
    {
        _robot.SendMessage(new BackpackLightsMiddle(
            LightState.Solid(top.Packed), LightState.Solid(middle.Packed), LightState.Solid(bottom.Packed)), flush: true);
        Backpack = (top, middle, bottom);
    }

    /// <summary>Sets all three backpack LEDs to the same colour.</summary>
    public void SetBackpack(LedColor all) => SetBackpack(all, all, all);

    /// <summary>Turns the backpack LEDs off.</summary>
    public void BackpackOff() => SetBackpack(LedColor.Off);

    /// <summary>
    /// Flashes the backpack between two colours, using the robot's own blink timing rather than a loop here.
    /// Frame counts are in animation frames, about 33 ms each.
    /// </summary>
    public void BlinkBackpack(LedColor on, LedColor off, byte onFrames = 15, byte offFrames = 15)
    {
        var state = new LightState
        {
            OnColor = on.Packed, OffColor = off.Packed,
            OnFrames = onFrames, OffFrames = offFrames,
            TransitionOnFrames = 0, TransitionOffFrames = 0, Offset = 0,
        };
        _robot.SendMessage(new BackpackLightsMiddle(state, state, state), flush: true);
        Backpack = (on, on, on);
    }

    /// <summary>
    /// Turns the infrared headlight on or off. It is infrared, so it looks unlit to the eye; a camera frame
    /// is what shows it working.
    /// </summary>
    public void SetHeadlight(bool on)
    {
        _robot.SendMessage(new SetHeadlight(on), flush: true);
        HeadlightOn = on;
    }
}
