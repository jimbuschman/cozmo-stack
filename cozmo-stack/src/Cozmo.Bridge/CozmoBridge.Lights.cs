using System.Runtime.CompilerServices;
using Cozmo.Robot;
using Cozmo.Robot.Vision;
using static Cozmo.Bridge.Argument;

namespace Cozmo.Bridge;

public sealed partial class CozmoBridge
{
    private static Argument[] LightPatternArguments =>
    [
        Rgb("onColor", "Six hex RGB digits, with an optional leading #."),
        Rgb("offColor", "Six hex RGB digits for the off phase. Omit for the app's unlit value.", optional: true),
        Integer("onMs", "On period in milliseconds. 4294967295 means steady on.", 0, uint.MaxValue, uint.MaxValue),
        Integer("offMs", "Off period in milliseconds.", 0, uint.MaxValue, 0),
        Integer("transitionOnMs", "Transition to the on colour in milliseconds.", 0, uint.MaxValue, 0),
        Integer("transitionOffMs", "Transition to the off colour in milliseconds.", 0, uint.MaxValue, 0),
    ];

    // The Unity client keeps requested LED values separately from the engine's light layers.
    // Robot.cs:407..412,1862..1891 and ObservableObject.cs:249..256,351..367.
    private readonly ConditionalWeakTable<object, LedPattern[]> requestedLights = new();

    private Task<CommandResult> SetGameLights(BridgeArguments args, CancellationToken token, uint? cubeId)
    {
        token.ThrowIfCancellationRequested();
        var engine = Robot.Engine;
        var owner = engine.Robot;
        if (engine.ConnectionState != 2 || !engine.IsReadyDelivered || engine.RobotValidated != true ||
            owner is null || owner.Lifetime.Destroying || !owner.FirstFullStateHandled)
            return Task.FromResult(CommandResult.NotReady());

        int mask = args.Int("ledMask");
        var off = args.Text("offColor");
        var pattern = new LedPattern(LedColor.FromHex(args.Text("onColor")),
            off.Length == 0 ? default : LedColor.FromHex(off), args.UInt("onMs"), args.UInt("offMs"),
            args.UInt("transitionOnMs"), args.UInt("transitionOffMs"), 0);
        LedPattern[] leds;
        if (cubeId is { } id)
        {
            // SetAllActiveObjectLEDs 0xB9 (0x0063A03C..0x0063A0D4).
            // The app's unrotated, nonrelative variant resolves a connected world ObjectID.
            var world = Robot.LocalizationWorld;
            using var mutation = world.EnterPoseMutation();
            var cube = world.GetConnectedActiveObjectByIdHelper(id);
            if (cube is null || !CubeGeometry.IsCube(cube.Type))
                return Task.FromResult(CommandResult.Action(false, null, $"No connected cube with world ID {id}", false));
            leds = UpdateRequestedLights(cube, 4, mask, pattern);
            var request = new CubeLightPattern(leds, 0, 0, false, false, "SetAllActiveObjectLEDs");
            if (Robot.Lights.Cubes.SetObjectLightsById(id, request) != 0)
                return Task.FromResult(CommandResult.Action(false, null, "The cube light pattern did not reach the robot send queue", false));
        }
        else
        {
            // SetBackpackLEDs 0xBA (0x006323C4..0x0063244E). Native Update chooses
            // the winning source and sends the robot messages on its regular tick.
            leds = UpdateRequestedLights(owner, 5, mask, pattern);
            Robot.Lights.SetGameBackpackLeds(leds);
        }

        return Task.FromResult(new CommandResult("submitted", null, null, null, Report: new
        {
            gameMessage = cubeId.HasValue ? "SetAllActiveObjectLEDs" : "SetBackpackLEDs",
            delivery = cubeId.HasValue ? "robot-send-queue" : "engine-light-layer",
            objectId = cubeId,
            ledMask = mask,
            leds = leds.Select((led, index) => new
            {
                index, onColor = led.OnColor.ToString(), offColor = led.OffColor.ToString(),
                led.OnMs, led.OffMs, led.TransitionOnMs, led.TransitionOffMs,
            }).ToArray(),
        }));
    }

    private LedPattern[] UpdateRequestedLights(object owner, int count, int mask, LedPattern pattern)
    {
        var cache = requestedLights.GetValue(owner, _ => new LedPattern[count]);
        for (int i = 0; i < cache.Length; ++i)
            if ((mask & (1 << i)) != 0) cache[i] = pattern;
        return (LedPattern[])cache.Clone();
    }
}
