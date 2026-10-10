using System.Text.Json;
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
    /// bit 5, blue at bit 0, and bit 15 set when alpha is non-zero (LB4f, LC5); see <see cref="LightState.Rgb"/>.
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

/// <summary>One LED of a light pattern as the engine holds it: colours and times in ms (LB4d, LC4).</summary>
public readonly record struct LedPattern(LedColor OnColor, LedColor OffColor, uint OnMs, uint OffMs,
                                         uint TransitionOnMs, uint TransitionOffMs, int OffsetMs)
{
    /// <summary>A steady colour: both colours the same, every period 0.</summary>
    public static LedPattern Solid(LedColor c) => new(c, c, 0, 0, 0, 0, 0);
}

// fidelity: M4-017, M4-018
/// <summary>The time conversions both light paths use (LB4f, LC5).</summary>
internal static class LightWire
{
    /// <summary>u8((ms + 29) / 30) in u32 arithmetic, truncated; 0xFFFFFFFF gives 0xFF (0x7FFFFFFF gives 0x45).</summary>
    public static byte Frames(uint ms) => ms == 0xFFFFFFFFu ? (byte)0xFF : unchecked((byte)((ms + 29u) / 30u));

    /// <summary>The backpack offset: i16 of the signed, truncated (x + 29) / 30, with −1 giving 0x00FF (LB4f).</summary>
    public static short BackpackOffset(int ms) => ms == -1 ? (short)0x00FF : unchecked((short)((ms + 29) / 30));

    /// <summary>The cube offset: i16 of the signed, truncated (x + 29) / 30 (LC5, 0x0063A73A..0x0063A75A).</summary>
    public static short CubeOffset(int ms) => unchecked((short)((ms + 29) / 30));
}

/// <summary>
/// The engine's backpack-light patterns (BackpackLightAnimationContainer, LB4b..LB4e): "OffCharger" added by the
/// engine itself as the Off lights, then "Charging", "Charged" and "BadCharger" from backpackLightPatterns.json's
/// "charging", "charged" and "badCharger". Lookup is exact and case-sensitive (LB4c).
/// </summary>
public sealed class BackpackLightAnimations
{
    private readonly Dictionary<string, LedPattern[]> _byName = new(StringComparer.Ordinal);

    /// <summary>LB4e: the directory the engine loads backpack patterns from, under the resources path.</summary>
    public const string ResourceDirectory = "config/engine/lights/backpackLights";

    /// <summary>The container with only the engine's own OffCharger entry.</summary>
    public BackpackLightAnimations() => _byName["OffCharger"] = BodyLightComponent.OffLights;

    /// <summary>The names the container holds.</summary>
    public IReadOnlyCollection<string> Names => _byName.Keys;

    /// <summary>GetAnimation: exact lookup, null for an unknown name ("InvalidName", LB4c).</summary>
    public LedPattern[]? Get(string name) => _byName.GetValueOrDefault(name);

    // fidelity: M4-017
    /// <summary>
    /// DefineFromJson (LB4b, 0x00587558..0x00587638) for each file in <see cref="ResourceDirectory"/> (LB4e):
    /// AddBackpackLightStateValues ("Charging", json["charging"]), ("Charged", json["charged"]), ("BadCharger",
    /// json["badCharger"]). A directory or file that is missing loads nothing.
    /// </summary>
    public static BackpackLightAnimations Load(string? resourcesPath, Action<string>? log = null)
    {
        var c = new BackpackLightAnimations();
        if (resourcesPath is null) return c;
        var dir = Path.Combine(resourcesPath, "config", "engine", "lights", "backpackLights");
        if (!Directory.Exists(dir)) { log?.Invoke($"warning: backpack light patterns not found at {dir}"); return c; }
        foreach (var f in Directory.GetFiles(dir, "*.json").OrderBy(p => p, StringComparer.Ordinal))
        {
            if (!TryParse(File.ReadAllBytes(f), out var doc)) { log?.Invoke($"error: {f}: not JSON"); continue; }
            using (doc) c.DefineFromJson(doc.RootElement, log);
        }
        return c;
    }

    internal void DefineFromJson(JsonElement root, Action<string>? log)
    {
        AddStateValues("Charging", root, "charging", log);
        AddStateValues("Charged", root, "charged", log);
        AddStateValues("BadCharger", root, "badCharger", log);
    }

    // fidelity: M4-017
    /// <summary>
    /// AddBackpackLightStateValues (LB4d, 0x00586D20..0x005870D0): onColors, offColors, onPeriod_ms, offPeriod_ms,
    /// transitionOnPeriod_ms, transitionOffPeriod_ms and offset, each with exactly 5 entries, otherwise "Missing member
    /// field" and nothing is inserted; an existing key is not overwritten. Colours are floats: byte = u32(f · 255).
    /// </summary>
    private void AddStateValues(string name, JsonElement root, string key, Action<string>? log)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(key, out var e) || e.ValueKind != JsonValueKind.Object)
        {
            log?.Invoke($"error: BackpackLightAnimationContainer: Missing member field {key}");
            return;
        }
        string[] fields = { "onColors", "offColors", "onPeriod_ms", "offPeriod_ms", "transitionOnPeriod_ms", "transitionOffPeriod_ms", "offset" };
        foreach (var f in fields)
            if (!e.TryGetProperty(f, out var a) || a.ValueKind != JsonValueKind.Array || a.GetArrayLength() != 5)
            {
                log?.Invoke($"error: BackpackLightAnimationContainer: Missing member field {f} in {key}");
                return;
            }
        if (_byName.ContainsKey(name)) return;
        var leds = new LedPattern[5];
        for (int i = 0; i < 5; i++)
            leds[i] = new LedPattern(FloatColor(e.GetProperty("onColors")[i]), FloatColor(e.GetProperty("offColors")[i]),
                                     (uint)e.GetProperty("onPeriod_ms")[i].GetInt64(), (uint)e.GetProperty("offPeriod_ms")[i].GetInt64(),
                                     (uint)e.GetProperty("transitionOnPeriod_ms")[i].GetInt64(),
                                     (uint)e.GetProperty("transitionOffPeriod_ms")[i].GetInt64(),
                                     (int)e.GetProperty("offset")[i].GetInt64());
        _byName[name] = leds;
    }

    private static LedColor FloatColor(JsonElement c)
    {
        byte B(int i) => unchecked((byte)(uint)(c[i].GetSingle() * 255f));
        return new LedColor(B(0), B(1), B(2), B(3));
    }

    internal static bool TryParse(byte[] bytes, out JsonDocument doc)
    {
        try
        {
            doc = JsonDocument.Parse(bytes, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            return true;
        }
        catch (JsonException) { doc = null!; return false; }
    }
}

// fidelity: M4-017
/// <summary>
/// BodyLightComponent (M4-017): three sources, the best one chosen in priority {1, 0, 2}; the charging state machine
/// at source 2; one locator shared by the charging config and the game's SetBackpackLEDs; the conversion to
/// BackpackLightsMiddle 0x03 (LEDs 1..3) and BackpackLightsTurnSignals 0x11 (LEDs 0 and 4).
/// </summary>
public sealed class BodyLightComponent
{
    /// <summary>LB1: the priority order of the sources (0x00C7C800).</summary>
    private static readonly int[] Priority = { 1, 0, 2 };
    /// <summary>LB4a: state → name (the table at 0x0102EA18 indexed by state^2).</summary>
    public static readonly string[] StateNames = { "OffCharger", "Charging", "Charged", "BadCharger" };

    // fidelity: M4-017
    /// <summary>LB2/LB4h: every colour rev(BLACK 00 00 00 FF), every period and offset 0.</summary>
    public static readonly LedPattern[] OffLights = Enumerable.Repeat(LedPattern.Solid(new LedColor(0, 0, 0, 0xFF)), 5).ToArray();

    private sealed class Entry { public required LedPattern[] Leds; }

    private readonly Func<RobotMessage, bool> _send;
    private readonly object _gate = new();
    private readonly LinkedList<Entry>[] _sources = { new(), new(), new() };
    /// <summary>comp+0x24: the one locator the charging config, SetBackpackLights and SetBackpackLEDs use (LB4i).</summary>
    private (int Source, LinkedListNode<Entry> Node)? _locator;
    private Entry? _previous;
    /// <summary>+0x38: the charging state, 0 from the constructor (0x006319B2).</summary>
    private int _chargingState;

    internal BodyLightComponent(Func<RobotMessage, bool> send, BackpackLightAnimations animations)
    {
        _send = send;
        Animations = animations;
    }

    /// <summary>The patterns the charging state machine plays.</summary>
    public BackpackLightAnimations Animations { get; }
    /// <summary>The charging state: 0 OffCharger, 1 Charging, 2 Charged, 3 BadCharger (LB4).</summary>
    public int ChargingState { get { lock (_gate) return _chargingState; } }

    internal void ResetToConstructed()
    {
        lock (_gate)
        {
            foreach (var s in _sources) s.Clear();
            _locator = null;
            _previous = null;
            _chargingState = 0;
        }
    }

    // fidelity: M4-017
    /// <summary>
    /// StartLoopingBackpackLightsInternal (LB4i, 0x006322A4): StopLooping(locator), then emplace_front at the source.
    /// </summary>
    internal void StartLooping(LedPattern[] leds, int source)
    {
        lock (_gate)
        {
            if (_locator is { } l) _sources[l.Source].Remove(l.Node);
            var node = _sources[source].AddFirst(new Entry { Leds = leds });
            _locator = (source, node);
        }
    }

    // fidelity: M4-017
    /// <summary>
    /// UpdateChargingLightConfig (LB4, 0x00631B94..0x00631C36): on the charger, OOS gives 3, else IS_CHARGING 1, else 2;
    /// off it, a battery under 3.5 V gives 3, else 0. On a change the state is stored first (+0x38 at 0x00631BD8), then
    /// the pattern named for it is looped at source 2; an unknown name warns and plays nothing (LB4c).
    /// </summary>
    private void UpdateChargingLightConfig(RobotState stored, Action<string> log)
    {
        int state;
        if (stored.Has(RobotStatusFlag.IsOnCharger))
            state = stored.Has(RobotStatusFlag.IsChargerOos) ? 3 : stored.Has(RobotStatusFlag.IsCharging) ? 1 : 2;
        else state = stored.BatteryVoltage < 3.5f ? 3 : 0;
        string name;
        lock (_gate)
        {
            if (state == _chargingState) return;
            _chargingState = state;
            name = StateNames[state];
        }
        if (Animations.Get(name) is { } leds) StartLooping(leds, 2);
        else log($"warning: BodyLightComponent.UpdateChargingLightConfig: no pattern \"{name}\" (InvalidName)");
    }

    // fidelity: M4-017
    /// <summary>
    /// BodyLightComponent::Update (LB1, 0x00631D54..0x00631DDE, 0x00631E30..0x00631EB4), every Robot::Update after the
    /// first state: the charging config; best = the front of the first non-empty source in {1, 0, 2}; a change applies
    /// best, or Off when best is null; with best and the previous both null, Off is sent again.
    /// </summary>
    internal void Update(RobotState stored, Action<string> log)
    {
        UpdateChargingLightConfig(stored, log);
        LedPattern[]? apply;
        lock (_gate)
        {
            Entry? best = null;
            foreach (int p in Priority)
                if (_sources[p].First is { } n) { best = n.Value; break; }
            if (!ReferenceEquals(best, _previous)) apply = best?.Leds ?? OffLights;
            else apply = best is null ? OffLights : null;
            _previous = best;
        }
        if (apply is not null) SetBackpackLightsInternal(apply);
    }

    // fidelity: M4-017
    /// <summary>
    /// SetBackpackLightsInternal (LB3, LB4f, 0x00631F3A..0x006321BE): no white balance; LEDs 1, 2, 3 to 0x03 (3 × 10
    /// bytes + u8 0), then LEDs 0 and 4 to 0x11 (2 × 10 bytes + u8 0), both reliable.
    /// </summary>
    internal void SetBackpackLightsInternal(LedPattern[] leds)
    {
        _send(new BackpackLightsMiddle(Wire(leds[1]), Wire(leds[2]), Wire(leds[3])) { Field1 = 0 });
        _send(new BackpackLightsTurnSignals { Field0 = new[] { Wire(leds[0]), Wire(leds[4]) }, Field1 = 0 });
    }

    internal static LightState Wire(LedPattern p) => new()
    {
        OnColor = p.OnColor.Packed, OffColor = p.OffColor.Packed,
        OnFrames = LightWire.Frames(p.OnMs), OffFrames = LightWire.Frames(p.OffMs),
        TransitionOnFrames = LightWire.Frames(p.TransitionOnMs), TransitionOffFrames = LightWire.Frames(p.TransitionOffMs),
        Offset = LightWire.BackpackOffset(p.OffsetMs),
    };
}

/// <summary>
/// Cozmo's lights: the backpack LEDs (through <see cref="Body"/>, the engine's BodyLightComponent), the infrared
/// headlight, and the cube lights (<see cref="Cubes"/>, the engine's CubeLightComponent).
/// </summary>
public sealed class CozmoLights
{
    private readonly CozmoRobot _robot;

    internal CozmoLights(CozmoRobot robot, BackpackLightAnimations backpack, CubeLightAnimations cubes)
    {
        _robot = robot;
        Body = new BodyLightComponent(m => _robot.SendMessage(m), backpack);
        Cubes = new CubeLightComponent(robot, cubes);
    }

    /// <summary>The engine's BodyLightComponent (M4-017).</summary>
    public BodyLightComponent Body { get; }
    /// <summary>The engine's CubeLightComponent (M4-018).</summary>
    public CubeLightComponent Cubes { get; }

    // fidelity: M1-025, M1-015
    /// <summary>Back to the state right after construction, for a removed robot (CB33, CC26, CC27): nothing recorded as sent.</summary>
    internal void ResetToConstructed()
    {
        Backpack = default;
        HeadlightOn = false;
        Body.ResetToConstructed();
        Cubes.ResetToConstructed();
    }

    /// <summary>The last backpack colours that were asked for, so a caller can read back what it asked for.</summary>
    public (LedColor Top, LedColor Middle, LedColor Bottom) Backpack { get; private set; }
    /// <summary>Whether the headlight was last told to be on. The robot does not report its state.</summary>
    public bool HeadlightOn { get; private set; }

    // fidelity: M4-017
    /// <summary>
    /// The game SetBackpackLEDs (LB5, 0x006323C4..0x0063244E): the pattern is looped at source 2 on the shared
    /// locator, and BodyLightComponent::Update sends it at the next Robot::Update. Top, middle and bottom are LEDs
    /// 1, 2 and 3 (the three of 0x03); LEDs 0 and 4 (0x11) are unlit, as in the Off lights.
    /// </summary>
    public void SetBackpackPattern(LedPattern top, LedPattern middle, LedPattern bottom)
    {
        var off = BodyLightComponent.OffLights[0];
        Body.StartLooping(new[] { off, top, middle, bottom, off }, 2);
    }

    /// <summary>Sets the three backpack LEDs to steady colours (<see cref="SetBackpackPattern"/>).</summary>
    public void SetBackpack(LedColor top, LedColor middle, LedColor bottom)
    {
        SetBackpackPattern(LedPattern.Solid(top), LedPattern.Solid(middle), LedPattern.Solid(bottom));
        Backpack = (top, middle, bottom);
    }

    /// <summary>Sets all three backpack LEDs to the same colour.</summary>
    public void SetBackpack(LedColor all) => SetBackpack(all, all, all);

    /// <summary>Turns the backpack LEDs off (a black pattern at source 2).</summary>
    public void BackpackOff() => SetBackpack(LedColor.Off);

    /// <summary>
    /// Flashes the backpack between two colours, using the robot's own blink timing. Frame counts are the robot's
    /// 30 ms frames; they go into the pattern as frames × 30 ms, which the conversion (ms + 29) / 30 gives back.
    /// </summary>
    public void BlinkBackpack(LedColor on, LedColor off, byte onFrames = 15, byte offFrames = 15)
    {
        var p = new LedPattern(on, off, onFrames * 30u, offFrames * 30u, 0, 0, 0);
        SetBackpackPattern(p, p, p);
        Backpack = (on, on, on);
    }

    /// <summary>Mode-14 requests made while no VisionSystem was attached (see AttachVisionSystem).</summary>
    private readonly Queue<bool> _pendingLimitedExposure = new();

    // fidelity: M4-017
    /// <summary>
    /// The headlight (LB6, 0x00632344..0x00632380): SetHeadlight, reliable, not hot.
    /// E2..E7: the engine first calls <c>VisionComponent::EnableMode(14)</c> (LimitedExposure) on robot+0x258, which
    /// queues (mode, bool) on <c>VisionSystem::SetNextMode</c> (the deque at VisionSystem+0xB0); the mask bit is
    /// applied when <c>VisionSystem::Update</c> drains the queue.
    /// The queue lives on the actual VisionSystem; with none attached the request is held and the wire send still proceeds.
    /// </summary>
    /// <summary>
    /// Sets the mode recipient and hands it the mode-14 requests made while none was attached, in order. Under the same lock as
    /// <see cref="SetHeadlight"/>'s recipient test and enqueue, so a request is either queued on the recipient or held and
    /// taken here, never lost.
    /// </summary>
    internal void AttachVisionSystem(Vision.VisionSystem vision)
    {
        lock (_pendingLimitedExposure)
        {
            _robot.VisionModeRecipient = vision;
            foreach (var pending in _pendingLimitedExposure) vision.QueueLimitedExposure(pending);
            _pendingLimitedExposure.Clear();
        }
    }

    /// <summary>Clears the recipient if it is <paramref name="vision"/>, under the SetHeadlight lock.</summary>
    internal bool DetachVisionSystem(Vision.VisionSystem vision)
    {
        lock (_pendingLimitedExposure)
        {
            if (!ReferenceEquals(_robot.VisionModeRecipient, vision)) return false;
            _robot.VisionModeRecipient = null;
            return true;
        }
    }

    public void SetHeadlight(bool on)
    {
        // The recipient test and the enqueue are atomic with AttachVisionSystem (same lock), so no request is lost.
        lock (_pendingLimitedExposure)
        {
            if (_robot.VisionModeRecipient is { } vision) vision.QueueLimitedExposure(on);
            else
            {
                // Manager decision (B-M3M4 fix round 1, item 3): the engine's NullVisionSystem branch (0x006527AC..
                // 0x00652808) is unreachable (the VisionComponent ctor always allocates the VisionSystem, 0x00650184..
                // 0x00650196), so no engine error and no _errG store. The requested mode is kept and handed to the
                // VisionSystem that attaches (AttachVisionSystem).
                _pendingLimitedExposure.Enqueue(on);
            }
        }
        _robot.SendMessage(new SetHeadlight(on));
        HeadlightOn = on;
    }
}

/// <summary>One pattern of a cube light animation (LC3): four LEDs, rotation, duration, canBeOverridden.</summary>
public sealed record CubeLightPattern(LedPattern[] Leds, uint RotationPeriodMs, uint DurationMs, bool CanBeOverridden, bool MakeRelative, string DebugName);

/// <summary>
/// The engine's cube light animations: CubeAnimationTriggerMap.json (trigger → animation name) and the patterns
/// under config/engine/lights/cubeLights (LC2 (c), LC3).
/// </summary>
// fidelity: M7-002
public sealed class CubeLightAnimations
{
    private readonly Dictionary<string, string> _triggerToAnim = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CubeLightPattern[]> _anims = new(StringComparer.Ordinal);

    public const string TriggerMapPath = "assets/cubeAnimationGroupMaps/CubeAnimationTriggerMap.json";
    public const string AnimationDirectory = "config/engine/lights/cubeLights";

    /// <summary>GetCubeAnimationForTrigger → CubeLightAnimationContainer::GetAnimation; null for "NoAnimForTrigger".</summary>
    public CubeLightPattern[]? ForTrigger(string trigger) =>
        _triggerToAnim.TryGetValue(trigger, out var n) ? _anims.GetValueOrDefault(n) : null;

    public IReadOnlyCollection<string> AnimationNames => _anims.Keys;

    // fidelity: M4-018
    /// <summary>
    /// Loads the trigger map and every animation file. The pattern parser (LC3, 0x00589B5C..0x00589BBE,
    /// 0x0058948A..0x005894C6): duration_ms and rotationPeriod_ms are required, canBeOverridden defaults to true,
    /// makeRelative to false; the colours are the file's 0..255 channels.
    /// D1..D5: the directory is <c>config/engine/lights/cubeLights</c> (0x0051F80C, length 31), read recursively for
    /// json and bin through <c>DataPlatform::pathToResource(Scope::Resources, ...)</c>; on the shipped files that is
    /// <c>&lt;persistentDataPath&gt;/cozmo/cozmo_resources/config/engine/lights/cubeLights</c>.
    /// </summary>
    public static CubeLightAnimations Load(string? resourcesPath, Action<string>? log = null)
    {
        var c = new CubeLightAnimations();
        if (resourcesPath is null) return c;
        var map = Path.Combine(resourcesPath, "assets", "cubeAnimationGroupMaps", "CubeAnimationTriggerMap.json");
        if (File.Exists(map) && BackpackLightAnimations.TryParse(File.ReadAllBytes(map), out var md))
        {
            using (md)
            {
                if (md.RootElement.TryGetProperty("Pairs", out var pairs) && pairs.ValueKind == JsonValueKind.Array)
                    foreach (var p in pairs.EnumerateArray())
                        if (p.TryGetProperty("CladEvent", out var ev) && p.TryGetProperty("AnimName", out var an))
                            c._triggerToAnim[ev.GetString() ?? ""] = an.GetString() ?? "";
            }
        }
        else log?.Invoke($"warning: cube light trigger map not found at {map}");
        var dir = Path.Combine(resourcesPath, "config", "engine", "lights", "cubeLights");
        if (!Directory.Exists(dir)) { log?.Invoke($"warning: cube light animations not found at {dir}"); return c; }
        foreach (var f in Directory.GetFiles(dir, "*.json", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
        {
            if (!BackpackLightAnimations.TryParse(File.ReadAllBytes(f), out var doc)) { log?.Invoke($"error: {f}: not JSON"); continue; }
            using (doc) c.Define(doc.RootElement, log);
        }
        return c;
    }

    internal void Define(JsonElement root, Action<string>? log)
    {
        if (root.ValueKind != JsonValueKind.Object) return;
        foreach (var anim in root.EnumerateObject())
        {
            if (anim.Value.ValueKind != JsonValueKind.Array) continue;
            var patterns = new List<CubeLightPattern>();
            bool ok = true;
            foreach (var e in anim.Value.EnumerateArray())
            {
                if (!TryPattern(e, out var p)) { ok = false; break; }
                patterns.Add(p);
            }
            if (!ok) { log?.Invoke($"error: CubeLightAnimationContainer: {anim.Name}: a pattern lacks a required field"); continue; }
            _anims.TryAdd(anim.Name, patterns.ToArray());
        }
    }

    private static bool TryPattern(JsonElement e, out CubeLightPattern p)
    {
        p = null!;
        if (!e.TryGetProperty("pattern", out var pat) || !e.TryGetProperty("duration_ms", out var dur)) return false;
        if (!pat.TryGetProperty("rotationPeriod_ms", out var rot)) return false;
        string[] fields = { "onColors", "offColors", "onPeriod_ms", "offPeriod_ms", "transitionOnPeriod_ms", "transitionOffPeriod_ms", "offset" };
        foreach (var f in fields)
            if (!pat.TryGetProperty(f, out var a) || a.ValueKind != JsonValueKind.Array || a.GetArrayLength() != 4) return false;
        var leds = new LedPattern[4];
        for (int i = 0; i < 4; i++)
            leds[i] = new LedPattern(ByteColor(pat.GetProperty("onColors")[i]), ByteColor(pat.GetProperty("offColors")[i]),
                                     (uint)pat.GetProperty("onPeriod_ms")[i].GetInt64(), (uint)pat.GetProperty("offPeriod_ms")[i].GetInt64(),
                                     (uint)pat.GetProperty("transitionOnPeriod_ms")[i].GetInt64(),
                                     (uint)pat.GetProperty("transitionOffPeriod_ms")[i].GetInt64(),
                                     (int)pat.GetProperty("offset")[i].GetInt64());
        bool over = !e.TryGetProperty("canBeOverridden", out var cbo) || cbo.ValueKind != JsonValueKind.False;
        bool rel = pat.TryGetProperty("makeRelative", out var mr) && mr.ValueKind == JsonValueKind.True;
        string name = e.TryGetProperty("patternDebugName", out var dn) ? dn.GetString() ?? "" : "";
        p = new CubeLightPattern(leds, (uint)rot.GetInt64(), (uint)dur.GetInt64(), over, rel, name);
        return true;
    }

    private static LedColor ByteColor(JsonElement c) =>
        new((byte)c[0].GetInt32(), (byte)c[1].GetInt32(), (byte)c[2].GetInt32(), (byte)c[3].GetInt32());
}

// fidelity: M4-018
/// <summary>
/// CubeLightComponent (M4-018). Per light cube an ObjectInfo {current layer, gameLayerOnly, a stack of playing
/// animations per layer}; layers 0 User/Game, 1 Engine, 2 State (0x0102EE64). A connecting light cube gets WakeUp
/// (trigger 0x26) on layer 2 (LC1); patterns advance on their timers in Update; when the layer empties the default
/// layer-2 animation takes over (LC3). The lights go out as SetCubeGamma (first call and on a change), CubeID and
/// CubeLights (LC5).
/// ObjectInfo is keyed by the cube's type: the engine keys it by ObjectID, and every ActiveCube of a type has one
/// ObjectID for the process lifetime (LC8e), so the two keys are one-to-one.
/// </summary>
public sealed class CubeLightComponent
{
    public const int UserLayer = 0, EngineLayer = 1, StateLayer = 2;

    // fidelity: M4-018
    /// <summary>
    /// C12.1: the static off <c>ObjectLights</c> at 0x0105B4A0 (<c>_INIT_36</c> 0x004D7EB4..0x004D7EE5): eight u32
    /// <c>0x000000FF</c> (<c>rev(NamedColors::BLACK)</c>), every period/transition/offset 0, rotation 0; through SetLEDs
    /// it is solid off with gamma 0x80. It is the same static <see cref="Update"/>'s P5 passes when an empty layer is
    /// game-layer-only, so the stack reuses the backpack Off pattern (LB2) for all four cube LEDs.
    /// </summary>
    internal static readonly CubeLightPattern OffLights = new(
        Enumerable.Repeat(BodyLightComponent.OffLights[0], 4).ToArray(), 0, 0, true, false, "Off");

    private sealed class Playing
    {
        public required CubeLightPattern[] Anim;
        public int Index;
        public uint TimerMs;
        public bool CanBeOverridden;
        /// <summary>node+0x41 (CurrentAnimInfo+0x39): the stop flag. W2: only StopAllAnimsOnLayer and StopLightAnim set it.</summary>
        public bool Stop;
    }

    private sealed class ObjectInfo
    {
        public int CurrentLayer = StateLayer;
        public bool GameLayerOnly;
        public readonly List<Playing>[] Layers = { new(), new(), new() };
    }

    private readonly CozmoRobot _robot;
    private readonly object _gate = new();
    private readonly Dictionary<ObjectType, ObjectInfo> _infos = new();
    /// <summary>comp+0x28: the gamma last sent, 0 from the constructor (0x00637106).</summary>
    private byte _gammaCache;
    /// <summary>comp+0x22, gameLayerOnly for a new ObjectInfo: 0 from the constructor (0x0063710A); set by EnableGameLayerOnly (S5).</summary>
    private bool _gameLayerOnlyDefault = false;
    /// <summary>
    /// comp+0x21: the RobotDelocalized refresh request. Set by <see cref="OnRobotDelocalized"/> (0x0063A3A8);
    /// held while robot+0x2C4 is 0 and applied by <see cref="Update"/> when it returns to 1 (C11.2 L6).
    /// </summary>
    private bool _refreshRequested;

    internal CubeLightComponent(CozmoRobot robot, CubeLightAnimations animations)
    {
        _robot = robot;
        Animations = animations;
    }

    public CubeLightAnimations Animations { get; }

    /// <summary>
    /// The default-layer inputs the engine reads from other components (LC3 PickNextAnimForDefaultLayer). IsCarried
    /// is wired by the M12 manipulation layer (CarryingComponent). MISSING interface: the located object's +0x24
    /// pose state (M11 BlockWorld) is not wired; until it is, IsVisible stays unset.
    /// </summary>
    internal Func<ObjectType, bool>? IsCarried { get; set; }
    internal Func<ObjectType, bool>? IsVisible { get; set; }

    /// <summary>
    /// robot+0x2C4, the robot-is-localized flag (C11.2 L6): the RobotDelocalized refresh is held while this is
    /// false and applied on the first <see cref="Update"/> after it returns true. Wired by CozmoRobot to the
    /// stack's <c>OffTreadsClassifier.Robot2C4</c> (the engine field's counterpart).
    /// </summary>
    internal Func<bool>? IsLocalized { get; set; }

    /// <summary>comp+0x23: the cube-sleep flag, 0 from the ctor (0x00637106..0x0063710A); written only by EnableCubeSleep (S3).</summary>
    private bool _sleepEnabled;
    /// <summary>comp+0x24: the skip-fade flag, 0 from the ctor; written only by EnableCubeSleep (S3).</summary>
    private bool _sleepSkipFade;

    internal void ResetToConstructed()
    {
        lock (_gate)
        {
            _infos.Clear();
            _gammaCache = 0;
            _sleepEnabled = false;
            _sleepSkipFade = false;
            _gameLayerOnlyDefault = false;
            _refreshRequested = false;
        }
    }

    /// <summary>The trigger of the animation on top of the current layer's stack, for a cube type; null for none.</summary>
    public string? TopPatternName(ObjectType type)
    {
        lock (_gate)
        {
            if (!_infos.TryGetValue(type, out var info)) return null;
            var stack = info.Layers[info.CurrentLayer];
            return stack.Count == 0 ? null : stack[^1].Anim[stack[^1].Index].DebugName;
        }
    }

    private uint NowMs => _robot.Engine.Timer.TimeStampMs;
    private void Log(string l) => _robot.Engine.Log(l);

    // fidelity: M4-018
    /// <summary>
    /// S7 (0x0063A3A8): the engine's RobotDelocalized handler sets comp+0x21, requesting that the default layer
    /// be re-picked for every object. <see cref="Update"/> applies it once robot+0x2C4 is non-zero (C11.2 L6).
    /// </summary>
    internal void OnRobotDelocalized() { lock (_gate) _refreshRequested = true; }

    // fidelity: M4-018
    /// <summary>
    /// The game-side ObjectConnectionState handler (LC1, 0x00639CE4..0x00639D8A): a disconnection returns at once;
    /// a connected light cube gets an ObjectInfo {layer 2, gameLayerOnly comp+0x22} unless one exists, then
    /// PlayLightAnim(WakeUp, layer 2) - on every connection, reconnects included (LC8).
    /// </summary>
    internal void OnObjectConnectionState(ObjectType type, bool connected)
    {
        if (!connected || !CozmoCubes.IsLightCube(type)) return;
        lock (_gate)
            if (!_infos.ContainsKey(type)) _infos[type] = new ObjectInfo { GameLayerOnly = _gameLayerOnlyDefault };
        PlayLightAnim(type, "WakeUp", StateLayer);
    }

    // fidelity: M4-018
    /// <summary>
    /// PlayLightAnim (LC2, 0x006382EA..0x006387D8), its gates in order: (a) the ObjectInfo exists ("InvalidObjectID");
    /// (b) the top of the layer's stack can be overridden ("CantBeOverridden"); (c) an animation exists for the trigger
    /// ("NoAnimForTrigger"); (d) gameLayerOnly with layer ≠ 0 ("OnlyGameLayerEnabled"), or not with layer 0
    /// ("NotPlayingUserAnim"); (e) no blended animation here. Then the animation is pushed with timer = now + the first
    /// pattern's duration and canBeOverridden from the first pattern; (f) a layer above the current one is "LightsNotSet",
    /// otherwise current layer := layer and SetObjectLights(first pattern). SendTransitionMessage is game-only and there
    /// is no game here.
    /// (b) is the requested layer's stack (0x006382FC..0x0063831C).
    /// </summary>
    public bool PlayLightAnim(ObjectType type, string trigger, int layer)
    {
        CubeLightPattern first;
        lock (_gate)
        {
            if (!_infos.TryGetValue(type, out var info)) { Log($"warning: CubeLightComponent.PlayLightAnim.InvalidObjectID {type}"); return false; }
            var stack = info.Layers[layer];
            if (stack.Count > 0 && !stack[^1].CanBeOverridden) { Log($"info: CubeLightComponent.PlayLightAnim.CantBeOverridden {trigger}"); return false; }
            var anim = Animations.ForTrigger(trigger);
            if (anim is null || anim.Length == 0) { Log($"warning: CubeLightComponent.PlayLightAnim.NoAnimForTrigger {trigger}"); return false; }
            if (info.GameLayerOnly && layer != UserLayer) { Log("info: CubeLightComponent.PlayLightAnim.OnlyGameLayerEnabled"); return false; }
            if (!info.GameLayerOnly && layer == UserLayer) { Log("info: CubeLightComponent.PlayLightAnim.NotPlayingUserAnim"); return false; }
            first = anim[0];
            stack.Add(new Playing { Anim = anim, Index = 0, TimerMs = unchecked(NowMs + first.DurationMs), CanBeOverridden = first.CanBeOverridden });
            if (layer > info.CurrentLayer) { Log($"info: CubeLightComponent.PlayLightAnim.LightsNotSet {trigger}"); return true; }
            info.CurrentLayer = layer;
        }
        SetObjectLights(type, first);
        return true;
    }

    // fidelity: M4-018
    /// <summary>
    /// CubeLightComponent::Update(true) (LC3, 0x00637998..0x00637E02) each Robot::Update: the top of the current layer
    /// advances to its next pattern when its timer expires (timer = now + that pattern's duration, SetObjectLights),
    /// and is popped after its last pattern; an empty layer goes back to layer 2 and PickNextAnimForDefaultLayer
    /// plays Sleep/SleepNoFade, Carrying (0), Visible (0x25) or Connected (1).
    /// A timer expires at now ≥ timer (0x00637998 <c>bhs</c>). A pattern whose timer is 0 - one with duration_ms 0, such
    /// as connected.json's - holds unless +0x41 is set (0x0063798A..0x00637994, W4); +0x41 is set only by
    /// <see cref="StopAllAnimsOnLayer"/> and <see cref="StopLightAnim"/> (W2), and a pop with +0x41 set happens even
    /// though patterns remain.
    /// W4/P2/P3: after a pop, consecutive stopped entries are popped too (P2), and if an animation is left on the layer
    /// its current pattern is resent immediately - SetObjectLights, with no timer or iterator reset (P3).
    /// </summary>
    internal void Update()
    {
        var sends = new List<(ObjectType, CubeLightPattern)>();
        var defaults = new HashSet<ObjectType>();
        lock (_gate)
        {
            // fidelity: M4-018
            // S7/C11.2 L6 (0x00637924..0x00637940): a RobotDelocalized refresh request (+0x21) is held while
            // robot+0x2C4 is 0 and applied on the first Update after it returns to 1: clear +0x21 and re-pick the
            // default layer for every object.
            if (_refreshRequested && IsLocalized?.Invoke() == true)
            {
                _refreshRequested = false;
                foreach (var t in _infos.Keys) defaults.Add(t);
            }
            uint now = NowMs;
            foreach (var (type, info) in _infos)
            {
                var stack = info.Layers[info.CurrentLayer];
                if (stack.Count > 0)
                {
                    var top = stack[^1];
                    var cur = top.Anim[top.Index];
                    bool waiting = cur.DurationMs == 0 || unchecked((int)(now - top.TimerMs)) < 0;
                    if (waiting && !top.Stop) continue;                 // W4: holds
                    if (!top.Stop)
                    {
                        top.Index++;
                        if (top.Index < top.Anim.Length)
                        {
                            var next = top.Anim[top.Index];
                            top.TimerMs = unchecked(now + next.DurationMs);
                            sends.Add((type, next));
                            continue;
                        }
                    }
                    // W4: +0x41 set, or the last pattern was reached. Pop, then P2: pop consecutive stopped entries.
                    stack.RemoveAt(stack.Count - 1);
                    while (stack.Count > 0 && stack[^1].Stop) stack.RemoveAt(stack.Count - 1);
                    if (stack.Count > 0)
                    {
                        // P3: resend the lower animation's current pattern, with no timer or iterator reset.
                        var lower = stack[^1];
                        sends.Add((type, lower.Anim[lower.Index]));
                        continue;
                    }
                }
                // fidelity: M4-018
                // P5 (0x00637B04..0x00637C06): an empty layer with gameLayerOnly set sends the static off ObjectLights;
                // otherwise the current layer goes back to 2 and the default layer-2 animation is picked.
                if (info.GameLayerOnly)
                {
                    sends.Add((type, OffLights));
                    continue;
                }
                info.CurrentLayer = StateLayer;
                if (info.Layers[StateLayer].Count == 0) defaults.Add(type);
            }
        }
        foreach (var (t, p) in sends) SetObjectLights(t, p);
        foreach (var t in defaults) PickNextAnimForDefaultLayer(t);
    }

    // fidelity: M4-018
    /// <summary>
    /// W2: <c>StopAllAnimsOnLayer(layer, id)</c> (0x00638D80): every entry in that layer's stack gets +0x41 = 1,
    /// then Update runs. For a specific cube type only that object's layer is walked.
    /// </summary>
    internal void StopAllAnimsOnLayer(int layer, ObjectType? type = null)
    {
        lock (_gate)
        {
            foreach (var (t, info) in _infos)
                if (type is null || t == type)
                    foreach (var p in info.Layers[layer]) p.Stop = true;
        }
        Update();
    }

    // fidelity: M4-018
    /// <summary>
    /// W2: <c>StopLightAnim(trigger, layer, id)</c> (0x00638F58): every stack entry whose trigger matches gets
    /// +0x41 = 1, then Update runs. The engine's stop flag is per entry, keyed by the animation's trigger; this stack
    /// keys a playing animation by its animation name, so the match is on the pattern's animation name.
    /// </summary>
    internal void StopLightAnim(string trigger, int layer, ObjectType? type = null)
    {
        lock (_gate)
        {
            foreach (var (t, info) in _infos)
                if (type is null || t == type)
                    foreach (var p in info.Layers[layer])
                        if (MatchesTrigger(p, trigger)) p.Stop = true;
        }
        Update();
    }

    /// <summary>The animation name a trigger maps to; a playing entry matches when its name is the trigger's name.</summary>
    private bool MatchesTrigger(Playing p, string trigger) => Animations.ForTrigger(trigger) is { Length: > 0 } a
        && ReferenceEquals(a, p.Anim);

    // fidelity: M4-018
    /// <summary>
    /// S3/S4: the game <c>EnableCubeSleep</c> message (<c>HandleMessage&lt;EnableCubeSleep&gt;</c> 0x0063A4A5) is the
    /// only writer of comp+0x23 (enable) and comp+0x24 (skipAnimation) (0x0063A4F8..0x0063A502); both default to 0.
    /// On enable = 0 it stops Sleep (trigger 0x21) and SleepNoFade (trigger 0x22) on layer 2 for every object, then
    /// calls EnableGameLayerOnly(all, false) (0x0063A538..0x0063A576); it sends no cube message of its own. On
    /// enable != 0 it only stores, and the flags take effect the next time the default layer is picked.
    /// This stack has no app/engine game-message bus, so this public API mirrors the original app's caller
    /// (unity/scripts/csharp/Robot.cs:2090 <c>EnableCubeSleep(bool enable, bool skipAnimation = false)</c>); no
    /// game-message id is invented.
    /// </summary>
    public void EnableCubeSleep(bool enable, bool skipAnimation = false)
    {
        lock (_gate)
        {
            _sleepEnabled = enable;
            _sleepSkipFade = skipAnimation;
        }
        if (enable) return;
        StopLightAnim("Sleep", StateLayer);
        StopLightAnim("SleepNoFade", StateLayer);
        EnableGameLayerOnly(enable: false);
    }

    // fidelity: M4-018
    /// <summary>
    /// S5/C13.1: <c>EnableGameLayerOnly(id, enable)</c> (0x006399C0..0x00639B36). For every object (id −1) or the named
    /// object: enable = 1 sends the static off <see cref="OffLights"/> through SetObjectLights, sets ObjectInfo
    /// gameLayerOnly, stops layers 1 and 2, and (id −1 only) sets comp+0x22 := 1 (C12.1, C12.2). enable = 0 clears
    /// ObjectInfo.gameLayerOnly, sets the current layer back to 1 when the layer-1 stack is non-empty and to 2
    /// otherwise, then picks the default layer-2 animation; the all-objects path stops only layer 0 and sets
    /// comp+0x22 := 0, while the single-object path stops layers 0 and 2 and does not write comp+0x22.
    /// </summary>
    internal void EnableGameLayerOnly(bool enable, ObjectType? type = null, int objectId = -1)
    {
        // fidelity: M4-018
        // Entry log (0x0063997A sChanneledInfoF, before the C13.4 gates): channel "CubeLightComponent" (printed as
        // "[CubeLightComponent]", the IHelper channel convention), event "CubeLightComponent.EnableGameLayerOnly" (0x00639BAC), format
        // "%s game layer only for %s" (0x00639BD4): "Enabling" (0x00BFAA93) when enable != 0 else "Disabling"
        // (0x00BFAA9C); "all objects" (0x00639B94) when ObjectID == -1, else "object " (0x00639BA0) + the id.
        Log($"info: [CubeLightComponent] CubeLightComponent.EnableGameLayerOnly: {(enable ? "Enabling" : "Disabling")} game layer only for " +
            (objectId == -1 ? "all objects" : FormattableString.Invariant($"object {objectId}")));
        // An id other than -1 with no ObjectInfo (type null): the lookup fails, complete no-op after the entry log.
        if (type is null && objectId != -1) return;
        // fidelity: M4-018 (C13.4)
        // C13.4: the engine branches to the epilogue without acting when the target is already in the requested
        // state: the all-objects (id -1) comp+0x22 for the all-objects case (0x00639A16..0x00639A1C), the object's
        // ObjectInfo+0x1C otherwise (0x006399DC..0x006399E0).
        lock (_gate)
        {
            if (type is null) { if (_gameLayerOnlyDefault == enable) return; }
            // A single object with no ObjectInfo: complete no-op (cmp r7,end; beq at 0x006399D8..0x006399E0).
            else if (!_infos.TryGetValue(type.Value, out var current) || current.GameLayerOnly == enable) return;
        }
        if (enable)
        {
            // C10.3 S5 / C12.1 / C12.2: the enable = 1 branch. Both sub-branches send SetObjectLights with the static
            // off ObjectLights at 0x0105B4A0 and stop layers 1 and 2. The all-objects sub-branch (id −1, 0x006399EA..
            // 0x00639B36, the public API's default) sets each ObjectInfo's gameLayerOnly before the stops - which is
            // what P5 needs to send the static off when the stopped layer empties - and sets comp+0x22 at the end; the
            // single-object sub-branch (0x006399F2) sets its gameLayerOnly after the stops and leaves comp+0x22 alone.
            if (type is null)
            {
                List<ObjectType> targets;
                lock (_gate) targets = _infos.Keys.ToList();
                foreach (var t in targets) SetObjectLights(t, OffLights);
                lock (_gate) foreach (var t in targets) if (_infos.TryGetValue(t, out var info)) info.GameLayerOnly = true;
                StopAllAnimsOnLayer(EngineLayer, null);
                StopAllAnimsOnLayer(StateLayer, null);
                lock (_gate) _gameLayerOnlyDefault = true;   // comp+0x22 (id −1)
            }
            else
            {
                var only = type.Value;
                SetObjectLights(only, OffLights);
                StopAllAnimsOnLayer(EngineLayer, only);
                StopAllAnimsOnLayer(StateLayer, only);
                lock (_gate) if (_infos.TryGetValue(only, out var info)) info.GameLayerOnly = true;
            }
            return;
        }
        // fidelity: M4-018
        // C13.1: the all-objects (id −1) enable = 0 path stops only layer 0, then clears each ObjectInfo+0x1C and
        // sets comp+0x22 := 0; the single-object path stops layer 0 and layer 2, clears that ObjectInfo+0x1C, and
        // does not write comp+0x22.
        if (type is null) StopAllAnimsOnLayer(UserLayer, null);
        else
        {
            StopAllAnimsOnLayer(UserLayer, type);
            StopAllAnimsOnLayer(StateLayer, type);
        }
        var defaults = new List<ObjectType>();
        lock (_gate)
        {
            defaults.AddRange(_infos.Keys.Where(t => type is null || t == type));
        }
        foreach (var t in defaults)
        {
            lock (_gate)
            {
                if (!_infos.TryGetValue(t, out var info)) continue;
                info.GameLayerOnly = false;
                info.CurrentLayer = info.Layers[EngineLayer].Count > 0 ? EngineLayer : StateLayer;
            }
            // 00639AFA..00639B0E: clear and repick each object before advancing to the next.
            PickNextAnimForDefaultLayer(t);
        }
        if (type is null) lock (_gate) _gameLayerOnlyDefault = false; // 00639B32, after the per-object calls.
    }

    // fidelity: M4-018
    /// <summary>
    /// The app's <c>Robot.SetEnableFreeplayLightStates(enable, objectID)</c> (unity/scripts/csharp/Robot.cs:1902-1907),
    /// which sends game tag 0xBB <c>EnableLightStates</c> (C12.2). The engine receives
    /// <c>enable = (msg.byte0 == 0)</c>, so the app's <paramref name="enable"/> = true is the engine's enable = 0
    /// (restore the default layer) and false is the engine's enable = 1 (static off lights, game layer only).
    /// <paramref name="objectID"/> −1 is the engine's all-objects id; an id with no connected cube logs the entry line
    /// and then does nothing (the engine's ObjectID find fails).
    /// </summary>
    public void SetEnableFreeplayLightStates(bool enable, int objectID = -1)
    {
        // Only -1 means all objects; any other id (negative too) is logged and looked up like any id, and an unknown
        // one is a no-op after the entry log (EnableGameLayerOnly).
        ObjectType? type = objectID == -1 ? null : _robot.Cubes.ByObjectId(unchecked((uint)objectID))?.Type;
        EnableGameLayerOnly(!enable, type, objectID);
    }

    // fidelity: M4-018
    /// <summary>
    /// S1 (0x00637D60..0x00637D9E): when comp+0x23 (sleep enabled) is set, the default layer plays Sleep (0x21) or
    /// SleepNoFade (0x22) according to comp+0x24, on layer 2, and does not look at the carried or located object.
    /// S2 (0x00637DA6..0x00637E02): otherwise Connected (1), Visible (0x25) when the located object's +0x24 == 1, or
    /// Carrying (0); Carrying beats Visible, which beats Connected.
    /// </summary>
    internal void PickNextAnimForDefaultLayer(ObjectType type)
    {
        if (_sleepEnabled)
        {
            PlayLightAnim(type, _sleepSkipFade ? "SleepNoFade" : "Sleep", StateLayer);
            return;
        }
        string trigger = IsCarried?.Invoke(type) == true ? "Carrying"
                       : IsVisible?.Invoke(type) == true ? "Visible"
                       : "Connected";
        PlayLightAnim(type, trigger, StateLayer);
    }

    // fidelity: M4-018
    /// <summary>
    /// SetObjectLights (LC4, 0x00637E6C..0x00637EFC): the connected object (the located one only with makeRelative,
    /// which is an M11 interface and not built); none sends nothing. ActiveObject::SetLEDs (0x004E48B0..0x004E49A2):
    /// both periods 0 gives colours 0 and periods 0x7FFFFFFF; on = 0 gives onColor := offColor, on := 0x7FFFFFFF;
    /// off = 0 gives offColor := onColor, off := 0x7FFFFFFF; gamma 0x80. MakeStateRelativeToXY with mode 0 returns.
    /// Then SetLights with the pattern's rotation.
    /// </summary>
    private void SetObjectLights(ObjectType type, CubeLightPattern p)
    {
        if (p.MakeRelative) Log("warning: MISSING: a makeRelative cube light pattern needs the located object (M11); sent to the connected cube");
        var cube = _robot.Cubes.ConnectedCubes.FirstOrDefault(c => c.Type == type && c.ObjectId is not null);
        if (cube is null) return;
        var leds = new LedPattern[4];
        for (int i = 0; i < 4; i++)
        {
            var l = p.Leds[i];
            if (l.OnMs == 0 && l.OffMs == 0)
                l = l with { OnColor = new LedColor(0, 0, 0, 0), OffColor = new LedColor(0, 0, 0, 0), OnMs = 0x7FFFFFFF, OffMs = 0x7FFFFFFF };
            else if (l.OnMs == 0) l = l with { OnColor = l.OffColor, OnMs = 0x7FFFFFFF };
            else if (l.OffMs == 0) l = l with { OffColor = l.OnColor, OffMs = 0x7FFFFFFF };
            leds[i] = l;
        }
        SetLights(cube.ObjectId!.Value, leds, gamma: 0x80, p.RotationPeriodMs);
    }

    // fidelity: M4-018
    /// <summary>
    /// SetLights (LC5, 0x0063A654..0x0063A800): SetCubeGamma {gamma} only when it differs from comp+0x28, then
    /// CubeID {u32 activeID, u8 (rotation + 29) / 30}, then CubeLights with LEDs 0..3 in order: WhiteBalanceColor
    /// scales G and B by 0.6 (truncating) when R ≠ 0, then the colour word; the frame conversion; the signed offset.
    /// All reliable. The gamma test and the up-to-three sends are one group under <see cref="_sendGate"/>, so no other
    /// SetLights (another cube's CubeID and CubeLights, from another thread) can come between them.
    /// </summary>
    private void SetLights(uint activeId, LedPattern[] leds, byte gamma, uint rotationMs)
    {
        var wire = new LightState[4];
        for (int i = 0; i < 4; i++)
        {
            var l = leds[i];
            wire[i] = new LightState
            {
                OnColor = WhiteBalance(l.OnColor).Packed, OffColor = WhiteBalance(l.OffColor).Packed,
                OnFrames = LightWire.Frames(l.OnMs), OffFrames = LightWire.Frames(l.OffMs),
                TransitionOnFrames = LightWire.Frames(l.TransitionOnMs), TransitionOffFrames = LightWire.Frames(l.TransitionOffMs),
                Offset = LightWire.CubeOffset(l.OffsetMs),
            };
        }
        lock (_sendGate)
        {
            bool sendGamma;
            lock (_gate)
            {
                sendGamma = gamma != _gammaCache;
                if (sendGamma) _gammaCache = gamma;
            }
            if (sendGamma) _robot.SendMessage(new SetCubeGamma { Field0 = gamma });
            _robot.SendMessage(new CubeID { ObjectID = activeId, RotationPeriodFrames = LightWire.Frames(rotationMs) });
            _robot.SendMessage(new CubeLights { Lights = wire });
        }
    }

    /// <summary>Serialises SetLights' SetCubeGamma + CubeID + CubeLights group (the CubeID selects the cube the CubeLights is for).</summary>
    private readonly object _sendGate = new();

    /// <summary>WhiteBalanceColor (0x0063A894..0x0063A98E): G and B × 0.6, truncated, when R ≠ 0.</summary>
    internal static LedColor WhiteBalance(LedColor c) =>
        c.R == 0 ? c : c with { G = (byte)(c.G * 0.6f), B = (byte)(c.B * 0.6f) };
}
