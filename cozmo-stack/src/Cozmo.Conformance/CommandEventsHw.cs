using System.Text.Json;
using System.Text.Json.Nodes;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Vision;

namespace Cozmo.Conformance;

internal static class CommandEventsHw
{
    internal sealed record Step(string Id, string Plan, ushort Tag, string Report, string Record, string Address, int Seconds = 20);

    internal static readonly Step[] Steps =
    {
        new("face-seen", "Show one face Cozmo has never enrolled. Keep it centered in good light.", 0x46,
            "RobotObservedFace with an empty name", "M14-008", "0x004F4B7A–0x004F4B82"),
        new("face-lost", "The same guest should leave the camera view. Keep the robot still for over 15 seconds of fresh camera frames.", 0x4B,
            "RobotDeletedFace for the guest", "M14-008", "0x004F3C2C–0x004F3C34 (emit), 0x004F5380–0x004F53AC (expiry)", 35),
        new("face-recognised", "Show a previously enrolled face and keep it in view until Cozmo reports its name.", 0x46,
            "RobotObservedFace with a name", "M14-008", "0x004F4B7A–0x004F4B82"),
        new("cube-seen", "Set one connected light cube upright 20–25 cm ahead with a marker facing the camera.", 0x44,
            "RobotObservedObject for a light cube", "M11-038", "0x0061FFD6–0x0061FFDE"),
        new("cube-moved", "Slide the same cube to a new spot within view and leave it upright.", 0x0F,
            "ObjectMoved", "M4-009", "0x00534190–0x005341B0"),
        new("cube-tapped", "When the cube is still, tap it once and leave it in view.", 0x13,
            "ObjectTapped", "M4-023", "0x00630C6A–0x00630C72, 0x0063118A–0x00631192"),
        new("object-pose-lost", "Lift the cube out of sight, leaving its last position visible and unobstructed. Keep Cozmo still.", 0x52,
            "RobotMarkedObjectPoseUnknown after two eligible missed sightings", "M11-007", "0x005073AA–0x005073B8"),
    };

    // Compare the same selected ID and, where present, timestamp and name in the raw report and public event.
    internal sealed record Key(long Id, uint? RobotTimestamp = null, string? Name = null, uint? Family = null);
    internal sealed record Sample(Key Key, double AtMs, JsonObject Payload);

    internal sealed class Window(Step step, long? target, double start)
    {
        private readonly object gate = new();
        private readonly Dictionary<Key, Sample> raw = new();
        private readonly Dictionary<Key, Sample> api = new();
        private (Sample Raw, Sample Api)? match;
        private readonly double deadline = start + step.Seconds * 1000;

        private bool Matches(Key key) => (target is null || target == key.Id) && (step.Id switch
        {
            "face-seen" => key.Name == "",
            "face-recognised" => !string.IsNullOrEmpty(key.Name),
            "cube-seen" => key.Family == (uint)ObjectFamily.LightCube,
            _ => true,
        });

        internal void AddRaw(ushort tag, ReadOnlyMemory<byte> payload, double at)
        {
            if (tag != step.Tag || at < start || at > deadline || Decode(tag, payload) is not { } key || !Matches(key))
                return;
            lock (gate)
            {
                if (match is not null || raw.ContainsKey(key)) return;
                var sample = new Sample(key, at, new() { ["tag"] = $"0x{tag:X2}", ["bodyHex"] = Convert.ToHexString(payload.Span) });
                raw.Add(key, sample);
                if (api.TryGetValue(key, out var other)) match = (sample, other);
            }
        }

        internal void AddApi(ushort tag, Key key, double at, Func<JsonObject> payload)
        {
            if (tag != step.Tag || at < start || at > deadline) return;
            lock (gate)
            {
                if (match is not null || api.ContainsKey(key)) return;
                var sample = new Sample(key, at, payload());
                api.Add(key, sample);
                if (raw.TryGetValue(key, out var other)) match = (other, sample);
            }
        }

        internal (Sample? Raw, Sample? Api, bool IdsMatch) Read()
        {
            lock (gate) return match is { } pair ? (pair.Raw, pair.Api, true)
                : (raw.Values.FirstOrDefault(), api.Values.FirstOrDefault(), false);
        }

        internal bool Matched() => Read().IdsMatch;
    }

    // CLAD field order from the cited senders and the E2G serializers. Timestamp and object ID use
    // the same leading two words in ObjectMoved and ObjectTapped.
    internal static Key? Decode(ushort tag, ReadOnlyMemory<byte> body)
    {
        var reader = new CladReader(body);
        switch (tag)
        {
            case 0x46 when body.Length >= 94 && body.Length >= 94 + body.Span[56]:
                return new(reader.I32(), reader.U32(), new CladReader(body[56..]).String8());
            case 0x4B when body.Length == 4:
                return new(reader.I32());
            case 0x44 when body.Length == 69:
                uint timestamp = reader.U32();
                uint family = reader.U32();
                reader.U32(); // ObjectType
                return new(reader.U32(), timestamp, Family: family);
            case 0x0F when body.Length == 21:
            case 0x13 when body.Length == 12:
                uint eventTime = reader.U32();
                return new(reader.U32(), eventTime);
            case 0x52 when body.Length == 4:
                return new(reader.U32());
            default:
                return null;
        }
    }
}

internal sealed partial class M15CubeRun
{
    private CommandEventsHw.Window? activeCommandEvent;

    private void RecordCommandApi<T>(ushort tag, CommandEventsHw.Key key, T report) =>
        Volatile.Read(ref activeCommandEvent)?.AddApi(tag, key, Now,
            () => JsonSerializer.SerializeToNode(report, CommandPayloadOptions)!.AsObject());

    internal CommandEventsHw.Window OpenCommandEvent(string id, long? target = null)
    {
        var step = CommandEventsHw.Steps.Single(s => s.Id == id);
        var window = new CommandEventsHw.Window(step, target, Now);
        Volatile.Write(ref activeCommandEvent, window);
        return window;
    }

    private void RunCommandCubeEvents()
    {
        var cube = RunCommandEvent("cube-seen");
        foreach (var id in new[] { "cube-moved", "cube-tapped", "object-pose-lost" })
        {
            if (cube is null) SkipCommandEvent(id, "No light-cube world ID was reported in cube-seen.");
            else RunCommandEvent(id, cube);
        }
    }

    private void RunCommandFaceEvents()
    {
        var guest = RunCommandEvent("face-seen");
        if (guest is not null) RunCommandEvent("face-lost", guest);
        else SkipCommandEvent("face-lost", "No guest face ID was reported in face-seen.");
        RunCommandEvent("face-recognised");
    }

    private long? RunCommandEvent(string id, long? target = null)
    {
        var step = CommandEventsHw.Steps.Single(s => s.Id == id);
        PromptCommand($"[{id}] {step.Plan} Wait for 'Go' before changing the setup. Press Enter when ready.");
        double begin = Now;
        var window = OpenCommandEvent(id, target);
        try
        {
            Console.WriteLine($"Go. Waiting up to {step.Seconds} s for {step.Report}.");
            Poll(window.Matched, step.Seconds * 1000);
        }
        finally { Volatile.Write(ref activeCommandEvent, null); }

        var (raw, api, matches) = window.Read();
        var phase = CommandPhase(id, begin, raw is null ? "No matching engine report during the window."
            : matches ? "Engine report and C# event IDs match." : "Engine report arrived without a matching C# event.");
        phase["verdict"] = matches ? "PASS" : "FAIL";
        phase["reportReceived"] = raw is not null;
        phase["idsMatch"] = matches;
        phase["engineReport"] = Sample(raw);
        phase["apiEvent"] = Sample(api);
        SaveCommandEvent(step, phase);
        return raw?.Key.Id;
    }

    private void SkipCommandEvent(string id, string detail)
    {
        var step = CommandEventsHw.Steps.Single(s => s.Id == id);
        var phase = CommandPhase(id, Now, detail);
        phase["verdict"] = "SKIPPED";
        SaveCommandEvent(step, phase);
    }

    private void SaveCommandEvent(CommandEventsHw.Step step, JsonObject phase)
    {
        phase["source"] = new JsonObject { ["record"] = step.Record, ["address"] = step.Address,
            ["tag"] = $"0x{step.Tag:X2}", ["report"] = step.Report };
        phases.Add(phase);
        Event("command-event-check", (JsonObject)phase.DeepClone());
        Console.WriteLine($"    [{step.Id}] {phase["verdict"]} {phase["detail"]}");
    }

    private static JsonObject? Sample(CommandEventsHw.Sample? sample) => sample is null ? null : new()
    {
        ["id"] = sample.Key.Id, ["robotTimestamp"] = sample.Key.RobotTimestamp, ["atMs"] = sample.AtMs,
        ["name"] = sample.Key.Name, ["payload"] = sample.Payload.DeepClone(),
    };
}
