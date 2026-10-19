using System.Text.Json;
using Cozmo.Protocol;
using Cozmo.Robot.Vision;

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
    public LedPattern[]? Get(string name, Action<string>? log = null, SourceLogSink? sourceLog = null)
    {
        if (_byName.TryGetValue(name, out var lights)) return lights;
        SourceLogDispatch.GetSink(sourceLog, log)?.Invoke(EngineLogLevel.Error, null,
            "BackpackLightAnimationContainer.GetAnimation_Const.InvalidName", $"Animation requested for unknown animation '{name}'.");
        Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();
        return null;
    }

    // fidelity: M4-017
    /// <summary>
    /// DefineFromJson (LB4b, 0x00587558..0x00587638) for each file in <see cref="ResourceDirectory"/> (LB4e):
    /// AddBackpackLightStateValues ("Charging", json["charging"]), ("Charged", json["charged"]), ("BadCharger",
    /// json["badCharger"]). A directory or file that is missing loads nothing.
    /// </summary>
    // fidelity: M4-036
    public static BackpackLightAnimations Load(string? resourcesPath, Action<string>? log = null, SourceLogSink? sourceLog = null)
        => Load(resourcesPath, log, sourceLog, deferLoaderLogs: false, beforeStat: null);

    internal static BackpackLightAnimations Load(string? resourcesPath, Action<string>? log, SourceLogSink? sourceLog,
        bool deferLoaderLogs, Action<string>? beforeStat)
    {
        var c = new BackpackLightAnimations();
        if (resourcesPath is null) return c;
        var dir = Path.Combine(resourcesPath, "config", "engine", "lights", "backpackLights");
        var files = new List<string>();
        var mtimes = new Dictionary<string, long>(StringComparer.Ordinal);
        // CollectAnimFiles owns Results; standalone walks do not emit a second type-3 line.
        Collect(dir);
        var time = System.Diagnostics.Stopwatch.StartNew();
        foreach (var f in files)
        {
            byte[] bytes;
            try { bytes = File.ReadAllBytes(f); }
            catch (IOException) { ReadFailure(f, null); continue; }
            catch (UnauthorizedAccessException) { ReadFailure(f, null); continue; }
            FirmwareJsonDocument doc;
            try
            {
                int delimiter = Array.IndexOf(bytes, (byte)0xFF);
                if (delimiter >= 0) bytes = bytes[..delimiter]; // Reader::parse(istream): getline delimiter (char)-1.
                if (!Json.TryParseFirst(bytes, out doc)) { ReadFailure(f, "M5-051 G5b"); continue; }
            }
            catch (JsonRuntimeError e) { log?.Invoke($"event handler threw: {e.GetType().Name}: {e.Message}"); continue; }
            using (doc)
            {
                // fidelity: M1-034, M4-036 — section 13.4, operator pin: the native loader terminates;
                // isolate this file and keep states already inserted, then continue the enumeration.
                try { if (!JsonEmpty(doc.RootElement)) c.DefineFromJson(doc.RootElement, log, sourceLog); }
                catch (JsonLogicError e) { log?.Invoke($"event handler threw: {e.GetType().Name}: {e.Message}"); }
            }
        }
        SourceLogDispatch.GetSink(sourceLog, log)?.Invoke(EngineLogLevel.Info, "Animations", "RobotDataLoader.LoadBackpackLightAnimations.LoadTime", $"Time to load backpack light animations = {time.Elapsed.TotalMilliseconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)} ms");
        return c;

        void Collect(string path)
        {
            IEnumerable<string> entries;
            try { entries = Directory.EnumerateFileSystemEntries(path).ToArray(); }
            catch (IOException) { return; }
            catch (UnauthorizedAccessException) { return; }
            foreach (var f in entries)
            {
                if (Directory.Exists(f)) { Collect(f); continue; }
                if (!(f.EndsWith("json", StringComparison.Ordinal) || f.EndsWith("bin", StringComparison.Ordinal))) continue;
                long mtime;
                try
                {
                    beforeStat?.Invoke(f);
                    var stat = new FileInfo(f);
                    if (!stat.Exists) throw new IOException();
                    mtime = new DateTimeOffset(stat.LastWriteTimeUtc).ToUnixTimeSeconds();
                }
                catch (IOException) { StatFailure(f); continue; }
                catch (UnauthorizedAccessException) { StatFailure(f); continue; }
                if (mtimes.TryGetValue(f, out var old) && old >= mtime) continue;
                mtimes[f] = mtime; files.Add(f);
            }
        }
        void StatFailure(string f)
        {
            // CollectAnimFiles owns the only WalkAnimationDir warning in the deferred production path.
            if (!deferLoaderLogs)
                SourceLogDispatch.GetSink(sourceLog, log)?.Invoke(EngineLogLevel.Warning, null,
                    "RobotDataLoader.WalkAnimationDir", $"could not get mtime for {f}");
        }
        void ReadFailure(string f, string? errors)
        {
            SourceLogDispatch.GetSink(sourceLog, log)?.Invoke(EngineLogLevel.Error, null, "DataPlatform.readAsJson", $"Failed to read [{f}]");
            Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();
            if (!string.IsNullOrEmpty(errors))
                log?.Invoke("MISSING: M5-051 G5b | DataPlatform.readAsJson 0x0084C090..0x0084C15C: Json reader errors [%s] requires JsonCpp formatted reader-error bytes");
        }
    }

    private static bool JsonEmpty(FirmwareJsonValue root) => root.Kind switch
    {
        FirmwareJsonKind.Null => true,
        FirmwareJsonKind.Array => root.Elements.Count == 0,
        FirmwareJsonKind.Object => root.Members.Count == 0,
        _ => false
    };

    internal void DefineFromJson(FirmwareJsonValue root, Action<string>? log, SourceLogSink? sourceLog = null)
    {
        AddStateValues("Charging", root, "charging", log, sourceLog);
        AddStateValues("Charged", root, "charged", log, sourceLog);
        AddStateValues("BadCharger", root, "badCharger", log, sourceLog);
    }

    // fidelity: M4-017
    /// <summary>
    /// AddBackpackLightStateValues (LB4d, 0x00586D20..0x005870D0): onColors, offColors, onPeriod_ms, offPeriod_ms,
    /// transitionOnPeriod_ms, transitionOffPeriod_ms and offset, each with size()==5, otherwise "Missing member
    /// field" and nothing is inserted; an existing key is not overwritten. Colours are floats: byte = u32(f · 255).
    /// </summary>
    private void AddStateValues(string name, FirmwareJsonValue root, string key, Action<string>? log, SourceLogSink? sourceLog = null)
    {
        // Section 13.4: root["charging"] const/find and the state's first isMember require object or null.
        if (root.Kind is not (FirmwareJsonKind.Object or FirmwareJsonKind.Null))
            throw new JsonLogicError("in Json::Value::find(key, end, found): requires objectValue or nullValue");
        FirmwareJsonValue e = new();
        if (root.Kind == FirmwareJsonKind.Object) e = Member(root, key);
        if (e.Kind is not (FirmwareJsonKind.Object or FirmwareJsonKind.Null))
            throw new JsonLogicError("in Json::Value::find(key, end, found): requires objectValue or nullValue");
        // The native helpers convert each entire field before reading the next field; &= evaluates every read.
        var onColors = new LedColor[5]; var offColors = new LedColor[5];
        var on = new uint[5]; var off = new uint[5]; var transitionOn = new uint[5]; var transitionOff = new uint[5];
        var offsets = new int[5];
        bool Colors(string field, LedColor[] destination)
        {
            bool exists = e.Kind == FirmwareJsonKind.Object && e.Members.ContainsKey(Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(field)));
            if (!exists) return false;
            var array = Member(e, field);
            if (Json.Size(array) != 5)
            {
                SourceLogDispatch.GetSink(sourceLog, log)?.Invoke(EngineLogLevel.Error, null, "JsonTools.GetColorValuesToArrayOptional.DiffSizes", "The json array and destination array are different sizes");
                Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();
                return false;
            }
            for (uint i = 0; i < 5; i++) destination[i] = FloatColor(Json.Index(array, i));
            return true;
        }
        bool Unsigned(string field, uint[] destination)
        {
            var array = Member(e, field);
            if (array.Kind != FirmwareJsonKind.Array || Json.Size(array) != 5) return false;
            for (uint i = 0; i < 5; i++) destination[i] = Json.AsUInt(Json.Index(array, i));
            return true;
        }
        bool Signed(string field, int[] destination)
        {
            var array = Member(e, field);
            if (array.Kind != FirmwareJsonKind.Array || Json.Size(array) != 5) return false;
            for (uint i = 0; i < 5; i++) destination[i] = Json.AsInt(Json.Index(array, i));
            return true;
        }
        bool valid = Colors("onColors", onColors);
        valid &= Colors("offColors", offColors);
        valid &= Unsigned("onPeriod_ms", on);
        valid &= Unsigned("offPeriod_ms", off);
        valid &= Unsigned("transitionOnPeriod_ms", transitionOn);
        valid &= Unsigned("transitionOffPeriod_ms", transitionOff);
        valid &= Signed("offset", offsets);
        if (!valid)
        {
            SourceLogDispatch.GetSink(sourceLog, log)?.Invoke(EngineLogLevel.Error, null, "BackpackLightAnimationContainer.AddBackpackLightStateValues.InvalidJson", $"Missing member field in pattern definition for BackpackLightsState {name}");
            Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();
            return;
        }
        if (_byName.ContainsKey(name)) return;
        var leds = new LedPattern[5];
        for (int i = 0; i < 5; i++)
            leds[i] = new LedPattern(onColors[i], offColors[i], on[i], off[i], transitionOn[i], transitionOff[i], offsets[i]);
        _byName[name] = leds;
    }

    private static LedColor FloatColor(FirmwareJsonValue c)
    {
        byte B(int i) => unchecked((byte)(uint)(Json.AsFloat(Json.Index(c, (uint)i)) * 255f));
        return new LedColor(B(0), B(1), B(2), B(3));
    }

    private static FirmwareJsonValue Member(FirmwareJsonValue root, string key) =>
        root.Members.GetValueOrDefault(Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(key))) ?? new();
    // Test/standalone callers use the same production Reader.
    internal void DefineFromJson(JsonElement root, Action<string>? log, SourceLogSink? sourceLog = null)
    {
        if (!Json.TryParseFirst(System.Text.Encoding.UTF8.GetBytes(root.GetRawText()), out var doc)) return;
        using (doc) DefineFromJson(doc.RootElement, log, sourceLog);
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
    internal SourceLogSink? SourceLog { get; set; }
    internal void StartLooping(LedPattern[] leds, int source)
    {
        bool invalid;
        lock (_gate)
        {
            invalid = _locator is null;
            if (_locator is { } l) _sources[l.Source].Remove(l.Node);
            var node = _sources[source].AddFirst(new Entry { Leds = leds });
            _locator = (source, node);
        }
        if (invalid) SourceLog?.Invoke(EngineLogLevel.Info, "BodyLightComponent", "BodyLightComponent::RemoveBackpackLightConfig.InvalidLocator", "Trying to remove an invalid locator.");
    }

    // fidelity: M4-017
    /// <summary>
    /// UpdateChargingLightConfig (LB4, 0x00631B94..0x00631C36): on the charger, OOS gives 3, else IS_CHARGING 1, else 2;
    /// off it, a battery under 3.5 V gives 3, else 0. On a change the state is stored first (+0x38 at 0x00631BD8), then
    /// the pattern named for it is looped at source 2; an unknown name warns and plays nothing (LB4c).
    /// </summary>
    private void UpdateChargingLightConfig(RobotState stored, Action<string> log, SourceLogSink? sourceLog = null)
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
        if (Animations.Get(name, log, sourceLog) is { } leds) StartLooping(leds, 2);
        else SourceLogDispatch.GetSink(sourceLog, log)?.Invoke(EngineLogLevel.Warning, null, "BodyLightComponent.UpdateChargingLightConfig.NullAnim", $"Got null anim for state {name}");
    }

    // fidelity: M4-017
    /// <summary>
    /// BodyLightComponent::Update (LB1, 0x00631D54..0x00631DDE, 0x00631E30..0x00631EB4), every Robot::Update after the
    /// first state: the charging config; best = the front of the first non-empty source in {1, 0, 2}; a change applies
    /// best, or Off when best is null; with best and the previous both null, Off is sent again.
    /// </summary>
    internal void Update(RobotState stored, Action<string> log, SourceLogSink? sourceLog = null)
    {
        UpdateChargingLightConfig(stored, log, sourceLog);
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
        Body = new BodyLightComponent(m => _robot.SendMessage(m), backpack) { SourceLog = (level, channel, key, text) => _robot.Engine.EmitSourceLog(level, channel, key, text) };
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
    private readonly Queue<(int Mode, bool Enable)> _pendingLimitedExposure = new();

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
            foreach (var pending in _pendingLimitedExposure) vision.QueueVisionMode(pending.Mode,pending.Enable);
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

    // M3-021 uses the same recipient/attach gate as the existing M4-017 requests.
    internal void QueueVisionMode(int mode, bool enable)
    {
        lock (_pendingLimitedExposure)
        {
            if (_robot.VisionModeRecipient is { } vision) vision.QueueVisionMode(mode,enable);
            else _pendingLimitedExposure.Enqueue((mode,enable));
        }
    }

    public void SetHeadlight(bool on)
    {
        QueueVisionMode(14,on);
        _robot.SendMessage(new SetHeadlight(on));
        HeadlightOn = on;
    }
}

/// <summary>One pattern of a cube light animation (LC3): four LEDs, rotation, duration, canBeOverridden.</summary>
public sealed record CubeLightPattern(LedPattern[] Leds, uint RotationPeriodMs, uint DurationMs, bool CanBeOverridden, bool MakeRelative, string DebugName)
{
    // fidelity: M4-046 — mode byte and raw XY supplied by ObjectLights, not by JSON.
    public byte RelativeMode { get; init; }
    public System.Numerics.Vector2 RelativePoint { get; init; }
}
// fidelity: M4-046
public sealed record CubeLightModifier(LedPattern[] Leds, uint RotationPeriodMs, byte RelativeMode, System.Numerics.Vector2 RelativePoint);


/// <summary>
/// The engine's cube light animations: CubeAnimationTriggerMap.json (trigger → animation name) and the patterns
/// under config/engine/lights/cubeLights (LC2 (c), LC3).
/// </summary>
// fidelity: M7-002
public sealed class CubeLightAnimations
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _triggerToAnim = new(StringComparer.Ordinal);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, CubeLightPattern[]> _anims = new(StringComparer.Ordinal);

    private Func<Action>? _standaloneLoad;
    private readonly object _loadGate = new();
    private bool _loadRan;
    internal void EnsureLoaded()
    {
        Action? emit = null;
        lock (_loadGate)
        {
            var load = _standaloneLoad;
            _standaloneLoad = null;
            if (load is not null){_loadRan=true;emit=load();}
        }
        emit?.Invoke(); // No external callout while the initialization lock is held.
    }
    internal static CubeLightAnimations CreateDeferred(string? path, Action<string>? log, SourceLogSink? sourceLog)
    {
        var c = new CubeLightAnimations();
        if (path is null) return c; // No resource input means no standalone initialization to retain.
        // Host-only access before LoadFrom performs the one standalone load. A later LoadCollected
        // waits for this initialization lock, keeps these definitions, and does not parse/log them twice.
        c._standaloneLoad = () =>
        {
            var calls = new System.Collections.Concurrent.ConcurrentQueue<Action>();
            LoadInto(c, path, text => calls.Enqueue(() => log?.Invoke(text)),
                (level, channel, key, text) => calls.Enqueue(() => SourceLogDispatch.GetSink(sourceLog, log)?.Invoke(level, channel, key, text)), null);
            return () => { foreach (var call in calls) call(); };
        };
        return c;
    }
    internal void LoadCollected(string? path, IReadOnlyList<string> files, Action<string>? log, SourceLogSink? sourceLog)
    {
        lock (_loadGate)
        {
            _standaloneLoad=null;
            if(_loadRan)return;
            _loadRan=true;
        }
        // DispatchFiles logs on worker threads. Do not hold an initialization/state lock across those callouts.
        // Host reads during collected-load callbacks see the definitions already installed, as before;
        // they never start a second load or wait on the worker emitting the callback.
        LoadInto(this, path, log, sourceLog, files);
    }

    public const string TriggerMapPath = "assets/cubeAnimationGroupMaps/CubeAnimationTriggerMap.json";
    public const string AnimationDirectory = "config/engine/lights/cubeLights";

    /// <summary>GetCubeAnimationForTrigger → CubeLightAnimationContainer::GetAnimation; null for "NoAnimForTrigger".</summary>
    public CubeLightPattern[]? ForTrigger(string trigger)
    { EnsureLoaded(); return _triggerToAnim.TryGetValue(trigger, out var n) ? _anims.GetValueOrDefault(n) : null; }

    public IReadOnlyCollection<string> AnimationNames { get { EnsureLoaded(); return _anims.Keys.ToArray(); } }
    internal string AnimationForTrigger(string trigger) { EnsureLoaded(); return _triggerToAnim.GetValueOrDefault(trigger) ?? ""; }

    // fidelity: M4-018
    /// <summary>
    /// Loads the trigger map and every animation file. The pattern parser (LC3, 0x00589B5C..0x00589BBE,
    /// 0x0058948A..0x005894C6): duration_ms and rotationPeriod_ms are required, canBeOverridden defaults to true,
    /// makeRelative to false; the colours are the file's 0..255 channels.
    /// D1..D5: the directory is <c>config/engine/lights/cubeLights</c> (0x0051F80C, length 31), read recursively for
    /// json and bin through <c>DataPlatform::pathToResource(Scope::Resources, ...)</c>; on the shipped files that is
    /// <c>&lt;persistentDataPath&gt;/cozmo/cozmo_resources/config/engine/lights/cubeLights</c>.
    /// </summary>
    // fidelity: M4-044, M1-034
    public static CubeLightAnimations Load(string? resourcesPath, Action<string>? log = null, SourceLogSink? sourceLog = null)
    {
        var c = new CubeLightAnimations();
        LoadInto(c, resourcesPath, log, sourceLog, null);
        return c;
    }
    private static void LoadInto(CubeLightAnimations c, string? resourcesPath, Action<string>? log,
        SourceLogSink? sourceLog, IReadOnlyList<string>? collectedFiles)
    {
        var time = System.Diagnostics.Stopwatch.StartNew();
        if (resourcesPath is null) { EmitLoadTime(); return; }
        var map = Path.Combine(resourcesPath, TriggerMapPath);
        if (File.Exists(map) && BackpackLightAnimations.TryParse(File.ReadAllBytes(map), out var md))
        {
            using (md)
                if (md.RootElement.TryGetProperty("Pairs", out var pairs) && pairs.ValueKind == JsonValueKind.Array)
                    foreach (var pair in pairs.EnumerateArray())
                        if (pair.TryGetProperty("CladEvent", out var ev) && pair.TryGetProperty("AnimName", out var an))
                            c._triggerToAnim[ev.GetString() ?? ""] = an.GetString() ?? "";
        }
        else Stack("Stack.CubeLightLoader.TriggerMapNotFound", $"cube light trigger map not found at {map}");
        var directory = Path.Combine(resourcesPath, AnimationDirectory);
        if (!Directory.Exists(directory))
        { Stack("Stack.CubeLightLoader.AnimationsNotFound", $"cube light animations not found at {directory}"); }
        var files = collectedFiles?.ToList() ?? new List<string>();
        if (collectedFiles is null) Collect(directory);
        time.Restart();
        object definitionsGate = new();
        Animation.AnimationLibrary.DispatchFiles(files, file =>
        {
            // No group-loader abort gate. The parser is the M1-029/M4-036 JsonCpp port.
            try
            {
                byte[] bytes = File.ReadAllBytes(file);
                int end = Array.IndexOf(bytes, (byte)0xFF);
                if (end >= 0) bytes = bytes[..end];
                if (!Json.TryParseFirst(bytes, out var doc))
                { Animation.AnimationLibrary.ReportReadAsJsonFailure(file, null, log, sourceLog); return; }
                using (doc)
                    if (doc.RootElement.Kind != FirmwareJsonKind.Null &&
                        !(doc.RootElement.Kind is FirmwareJsonKind.Array or FirmwareJsonKind.Object && Json.Size(doc.RootElement) == 0))
                        lock (definitionsGate) c.Define(doc.RootElement, log, sourceLog, file);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            { Animation.AnimationLibrary.ReportReadAsJsonFailure(file, null, log, sourceLog); }
            // fidelity: M1-034, M4-044 — approved fatal-path isolation; no partial registration.
            catch (Exception e) when (e is JsonLogicError or JsonRuntimeError)
            { Skip(file, e); }
        });
        EmitLoadTime();

        void Emit(EngineLogLevel level, string? channel, string key, string text) => SourceLogDispatch.GetSink(sourceLog, log)?.Invoke(level, channel, key, text);
        void Stack(string key, string text)
        { string line=key+": "+text;if(log is {} sink)sink(line);else StackDiagnostics.Write(line); }
        void EmitLoadTime() => Emit(EngineLogLevel.Info, "Animations", "RobotDataLoader.LoadCubeLightAnimations.LoadTime",
            $"Time to load cube light animations = {time.Elapsed.TotalMilliseconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)} ms");
        void Skip(string file, Exception e) => SkipFatalFile(log,file,e);
        void Collect(string path)
        {
            string[] entries;
            try { entries = Directory.EnumerateFileSystemEntries(path).ToArray(); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return; }
            foreach (var file in entries)
            {
                // Shared WalkAnimationDir: DT_LNK is neither DT_REG nor DT_DIR.
                try { if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) continue; }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                { Emit(EngineLogLevel.Warning, null, "RobotDataLoader.WalkAnimationDir", $"could not get mtime for {file}"); continue; }
                if (Directory.Exists(file)) { Collect(file); continue; }
                if (!(file.EndsWith("json", StringComparison.Ordinal) || file.EndsWith("bin", StringComparison.Ordinal))) continue;
                try { var stat = new FileInfo(file); if (!stat.Exists) throw new IOException(); _ = stat.LastWriteTimeUtc; }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                { Emit(EngineLogLevel.Warning, null, "RobotDataLoader.WalkAnimationDir", $"could not get mtime for {file}"); continue; }
                files.Add(file);
            }
        }
    }

    // fidelity: M1-034, M4-044 — policy diagnostics never enter the engine source stream.
    private static void SkipFatalFile(Action<string>? log,string file,Exception error)
    {
        string text=$"Stack.CubeLightLoader.SkipFatalFile: M1-034 / M4-044: skipping cube light file {file}: {error.Message}; keeping any previous definition";
        if(log is {} sink)sink(text);else StackDiagnostics.Write(text);
    }

    internal void Define(JsonElement root, Action<string>? log, SourceLogSink? sourceLog = null)
    {
        if (Json.TryParseFirst(System.Text.Encoding.UTF8.GetBytes(root.GetRawText()), out var doc))
            using (doc) Define(doc.RootElement, log, sourceLog);
    }

    // fidelity: M4-044, M1-034
    internal void Define(FirmwareJsonValue root, Action<string>? log, SourceLogSink? sourceLog = null, string? file = null)
    {
        void Emit(EngineLogLevel level, string key, string text) => SourceLogDispatch.GetSink(sourceLog, log)?.Invoke(level, null, key, text);
        void Error(string suffix, string text)
        { Emit(EngineLogLevel.Error, (suffix == "EmptyFile" ? "CubeLightAnimationContainer.DefineFromJson." : "CubeLightAnimationContainer.ParseJsonToAnim.") + suffix, text); Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak(); }
        try
        {
            if (root.Kind is not (FirmwareJsonKind.Object or FirmwareJsonKind.Null)) throw new JsonLogicError("getMemberNames requires objectValue");
            var names = root.Members.Keys.OrderBy(x => x, StringComparer.Ordinal).ToArray(); // hex preserves CZString byte order
            if (names.Length == 0) { Error("EmptyFile", "Found no animations in JSON"); return; }
            string name = System.Text.Encoding.UTF8.GetString(Convert.FromHexString(names[0]));
            if (names.Length > 1) Emit(EngineLogLevel.Warning, "CubeLightAnimationContainer.DefineFromJson.TooManyAnims",
                $"Expecting only one animation per json file, found {names.Length}. Will use first: {name}");
            var value = root.Members[names[0]];
            var patterns = new List<CubeLightPattern>();
            for (int i = 0; i < Json.Size(value); i++)
            {
                var entry = Json.Index(value, (uint)i);
                if (!Has(entry, "pattern") || !Has(entry, "duration_ms")) { Error("InvalidJson", $"Missing member field in {name} json file"); return; }
                if (!TryPattern(Get(entry, "pattern"), name, i, entry, Emit, out var pattern))
                { Error("InvalidPattern", $"Missing member field in one of the pattern definitions in {name} json file"); return; }
                patterns.Add(pattern);
            }
            _anims[name] = patterns.ToArray();
        }
        // fidelity: M1-034, M4-044 — skip the complete definition, retaining the prior list.
        catch (JsonLogicError e)
        { SkipFatalFile(log,file??"<definition>",e); }
    }

    private static bool Has(FirmwareJsonValue value, string key)
    {
        if (value.Kind == FirmwareJsonKind.Null) return false;
        if (value.Kind != FirmwareJsonKind.Object) throw new JsonLogicError("find requires objectValue");
        return value.Members.ContainsKey(Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(key)));
    }
    private static FirmwareJsonValue Get(FirmwareJsonValue value, string key) =>
        Has(value, key) ? value.Members[Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(key))] : new();
    private static bool AsBool(FirmwareJsonValue value) => value.Kind switch
    {
        FirmwareJsonKind.Null => false, FirmwareJsonKind.Boolean => value.Boolean,
        FirmwareJsonKind.Int => value.Signed != 0, FirmwareJsonKind.UInt => value.Unsigned != 0,
        FirmwareJsonKind.Real => value.Real != 0,
        _ => throw new JsonLogicError("Value is not convertible to bool."),
    };

    private static bool TryPattern(FirmwareJsonValue pat, string name, int index, FirmwareJsonValue entry,
        Action<EngineLogLevel, string, string> emit, out CubeLightPattern pattern)
    {
        pattern = null!;
        bool ok = Colors("onColors", out var on);
        ok &= Colors("offColors", out var off);
        ok &= Array("onPeriod_ms", false, out var onMs);
        ok &= Array("offPeriod_ms", false, out var offMs);
        ok &= Array("transitionOnPeriod_ms", false, out var transOn);
        ok &= Array("transitionOffPeriod_ms", false, out var transOff);
        ok &= Array("offset", true, out var offsets);
        ok &= Has(pat, "rotationPeriod_ms");
        if (!ok) return false;
        uint rotation = Json.AsUInt(Get(pat, "rotationPeriod_ms"));
        var leds = Enumerable.Range(0, 4).Select(i => new LedPattern(on[i], off[i], onMs[i], offMs[i], transOn[i], transOff[i], unchecked((int)offsets[i]))).ToArray();
        uint duration = Json.AsUInt(Get(entry, "duration_ms"));
        bool over = !Has(entry, "canBeOverridden") || AsBool(Get(entry, "canBeOverridden"));
        string debugName = Has(entry, "patternDebugName") ? System.Text.Encoding.UTF8.GetString(Json.AsStringBytes(Get(entry, "patternDebugName"))) : name + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
        pattern = new(leds, rotation, duration, over, false, debugName);
        return true;

        bool Colors(string field, out LedColor[] colors)
        {
            colors = new LedColor[4];
            if (!Has(pat, field)) return false;
            var array = Get(pat, field);
            if (Json.Size(array) != 4)
            {
                emit(EngineLogLevel.Error, "JsonTools.GetColorValuesToArrayOptional.DiffSizes", "The json array and destination array are different sizes");
                Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak(); return false;
            }
            for (uint i = 0; i < 4; i++)
            {
                var color = Json.Index(array, i);
                colors[i] = new(unchecked((byte)Json.AsUInt(Json.Index(color, 0))), unchecked((byte)Json.AsUInt(Json.Index(color, 1))),
                    unchecked((byte)Json.AsUInt(Json.Index(color, 2))), unchecked((byte)Json.AsUInt(Json.Index(color, 3))));
            }
            return true;
        }
        bool Array(string field, bool signed, out uint[] values)
        {
            values = new uint[4]; var array = Get(pat, field);
            if (array.Kind != FirmwareJsonKind.Array || Json.Size(array) != 4) return false;
            for (uint i = 0; i < 4; i++) values[i] = signed ? unchecked((uint)Json.AsInt(Json.Index(array, i))) : Json.AsUInt(Json.Index(array, i));
            return true;
        }
    }

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

    private sealed class BlendedSlot
    {
        public CubeLightPattern[] Patterns = Array.Empty<CubeLightPattern>();
    }
    private sealed class Playing
    {
        public required CubeLightPattern[] BaseAnim;
        public BlendedSlot? Slot;
        // Native list::assign reuses the per-layer slot; buried entries retain their iterator/index.
        public CubeLightPattern[] Anim => Slot?.Patterns ?? BaseAnim;
        public string Name = "";
        public string Trigger = "";
        public int TimerOffset;
        public bool Blended;
        public bool Dangling; // A freed native list node stays freed even if a later assign grows the slot.
        public int Index;
        public uint TimerMs;
        public bool CanBeOverridden;
        /// <summary>node+0x41 (CurrentAnimInfo+0x39): the stop flag. W2: only StopAllAnimsOnLayer and StopLightAnim set it.</summary>
        public bool Stop;
        // Host atomicity: Update cannot advance this push until SetObjectLights' result has
        // committed its timer. The synchronous send/callouts remain outside the state lock.
        public bool PendingSend;
        public Action? Completed; // fidelity: M4-051 — std::function copy at 0x0063858C.
        public uint CompletionTag; // Generic play starts at 0; FromAction writes the successful layer-0 back entry.
    }

    private sealed class ObjectInfo
    {
        public uint ObjectId;
        public int CurrentLayer = StateLayer;
        public bool GameLayerOnly;
        public readonly List<Playing>[] Layers = { new(), new(), new() };
        public readonly BlendedSlot[] BlendedSlots = { new(), new(), new() };
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
    // comp+0x20: ctor 0x0063710A; only game-message writer 0x0063A2EA. No ingress is wired in this stack.
    private bool _transitionMessagesEnabled;
    private int _transitionIngressMissingReported;

    internal CubeLightComponent(CozmoRobot robot, CubeLightAnimations animations)
    {
        _robot = robot;
        Animations = animations;
    }

    public CubeLightAnimations Animations { get; }

    /// <summary>
    /// The default-layer inputs the engine reads from other components (LC3 PickNextAnimForDefaultLayer). IsCarried
    /// is wired by the M12 manipulation layer (CarryingComponent). MISSING interface: the located object's +0x24
    /// pose state (M11 BlockWorld) is not wired (M11-004 owns the BlockWorld by-id helpers); until it is, IsVisible stays unset and the default layer reports MISSING.
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
            _transitionMessagesEnabled = false;
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

    // fidelity: M4-041 — ActiveObject identity owns the state, discarded with that object.
    // The connected ActiveObject owns ActiveLedState, including its all-zero constructor value.
    internal static uint ColorWord(LedColor color) => ((uint)color.R << 24) | ((uint)color.G << 16) | ((uint)color.B << 8) | color.A;
    private static LedColor FromWord(uint word) => new((byte)(word >> 24), (byte)(word >> 16), (byte)(word >> 8), (byte)word);
    internal static LedPattern GetLEDState(LedPattern[] current, int index, SourceLogSink? sourceLog = null)
    {
        if (index >= 4) { sourceLog?.Invoke(EngineLogLevel.Warning, null, "ActiveObject.GetLEDState.IndexTooLarge", $"Requested LED index is too large ({index} > 3). Returning 3."); index = 3; }
        else if (index < 0) { sourceLog?.Invoke(EngineLogLevel.Warning, null, "ActiveObject.GetLEDState.NegativeIndex", $"LED index should be >= 0, not {index}. Using 0."); index = 0; }
        return current[index];
    }
    internal static CubeLightPattern[] BlendAnimWithCurLights(CubeLightPattern[] source, LedPattern[] current, out bool changed)
    {
        changed = false;
        var result = new CubeLightPattern[source.Length];
        for (int n = 0; n < source.Length; n++)
        {
            var leds = (LedPattern[])source[n].Leds.Clone();
            for (int i = 0; i < 4; i++)
            {
                var state = GetLEDState(current, i);
                // LedColor is the canonical RGBA spelling of rev(native little-endian ColorRGBA word).
                if (ColorWord(leds[i].OnColor) == 0) { leds[i] = leds[i] with { OnColor = state.OnColor }; changed = true; }
                if (ColorWord(leds[i].OffColor) == 0) { leds[i] = leds[i] with { OffColor = state.OffColor }; changed = true; }
            }
            result[n] = source[n] with { Leds = leds };
        }
        return result;
    }
    // fidelity: M4-046 — 0x00638A8C..0x00638B3A.
    internal static CubeLightPattern[] ApplyAnimModifier(CubeLightPattern[] source, CubeLightModifier modifier)
    {
        return source.Select(pattern =>
        {
            var leds = new LedPattern[4]; uint rotation = pattern.RotationPeriodMs;
            for (int i = 0; i < 4; i++)
            {
                var led = pattern.Leds[i]; var mod = modifier.Leds[i];
                leds[i] = led with { OnColor = FromWord(ColorWord(led.OnColor) | ColorWord(mod.OnColor)),
                    OffColor = FromWord(ColorWord(led.OffColor) | ColorWord(mod.OffColor)),
                    OnMs = unchecked(led.OnMs + mod.OnMs), OffMs = unchecked(led.OffMs + mod.OffMs),
                    TransitionOnMs = unchecked(led.TransitionOnMs + mod.TransitionOnMs), TransitionOffMs = unchecked(led.TransitionOffMs + mod.TransitionOffMs),
                    OffsetMs = unchecked(led.OffsetMs + mod.OffsetMs) };
                rotation = unchecked(rotation + modifier.RotationPeriodMs);
            }
            return pattern with { Leds = leds, RotationPeriodMs = rotation, MakeRelative = modifier.RelativeMode != 0,
                RelativeMode = modifier.RelativeMode, RelativePoint = modifier.RelativePoint };
        }).ToArray();
    }
    internal (string Name, bool Blended, uint Timer, CubeLightPattern[] Patterns, int Count)? PlayingForTest(ObjectType type, int layer)
    {
        lock (_gate) { if (!_infos.TryGetValue(type, out var info) || info.Layers[layer].Count == 0) return null;
            var p = info.Layers[layer][^1]; return (p.Name, p.Blended, p.TimerMs, p.Anim, info.Layers[layer].Count); }
    }

    private uint NowMs => _robot.Engine.Timer.TimeStampMs;
    private void Log(string l) => _robot.Engine.Log(l);
    private void SourceLog(EngineLogLevel level, string? channel, string key, string text) => _robot.Engine.EmitSourceLog(level, channel, key, text);

    // fidelity: M4-018, M4-048
    /// <summary>
    /// S7 (0x0063A3A8): the engine's RobotDelocalized handler sets comp+0x21, requesting that the default layer
    /// be re-picked for every object. <see cref="Update"/> applies it once robot+0x2C4 is non-zero (C11.2 L6).
    /// </summary>
    internal void OnRobotDelocalized()
    {
        KeyValuePair<ObjectType,ObjectInfo>[] objects;
        lock(_gate){_refreshRequested=true;objects=_infos.OrderBy(kv=>unchecked((int)kv.Value.ObjectId)).ToArray();}
        foreach(var pair in objects)
            if(!_robot.Carrying.IsCarryingObjectId(pair.Value.ObjectId))PlayLightAnimCore(pair.Key,"Connected",2,0,false,null,0,null);
    }

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
            if (!_infos.ContainsKey(type)) _infos[type] = new ObjectInfo { GameLayerOnly = _gameLayerOnlyDefault, ObjectId = TryObjectId(type) ?? uint.MaxValue };
        PlayLightAnimCore(type,"WakeUp",StateLayer,0,false,null,0,null);
    }

    // fidelity: M4-018
    /// <summary>
    /// PlayLightAnim (LC2, 0x006382EA..0x006387D8), its gates in order: (a) the ObjectInfo exists ("InvalidObjectID");
    /// (b) the top of the layer's stack can be overridden ("CantBeOverridden"); (c) an animation exists for the trigger
    /// ("NoAnimForTrigger"); (d) gameLayerOnly with layer ≠ 0 ("OnlyGameLayerEnabled"), or not with layer 0
    /// ("NotPlayingUserAnim"); (e) blend every play; reject a second data-blended animation. Push with timer = now + the first
    /// pattern's duration plus the stored offset and canBeOverridden from the last pattern; (f) a layer above the current one is "LightsNotSet",
    /// otherwise current layer := layer and SetObjectLights(first pattern). SendTransitionMessage is game-only and there
    /// is no game here.
    /// (b) is the requested layer's stack (0x006382FC..0x0063831C).
    /// </summary>
    // fidelity: M4-041, M4-045, M4-046
    // fidelity: M4-049 — OV01 S3; BlockWorld applies the ActiveCube/Charger virtual gate.
    internal Action<uint,string,int,bool,CubeLightModifier,int>? PoseStatePlayObservedForTest;
    internal void OnActiveObjectPoseStateChanged(uint objectId, PoseState oldState, PoseState state)
    {
        if (oldState == state) return;
        var zero = new CubeLightModifier(new LedPattern[4],0,0,System.Numerics.Vector2.Zero);
        string trigger = state==PoseState.Known?"Visible":"Connected";
        PoseStatePlayObservedForTest?.Invoke(objectId,trigger,2,false,zero,0);
        PlayLightAnimForObjectId(objectId,trigger,2,false,zero,0);
    }
    private bool PlayLightAnimForObjectId(uint id,string trigger,int layer,bool modifier,CubeLightModifier lights,int offset)
    {
        ObjectType? type=null;
        lock(_gate) foreach(var entry in _infos) if(entry.Value.ObjectId==id){type=entry.Key;break;}
        if(type is not {} found)
        {
            SourceLog(EngineLogLevel.Info,"CubeLightComponent","CubeLightComponent.PlayLightAnim.InvalidObjectID",$"No object with id {id} exists in _objectInfo map can't play animation");
            return false;
        }
        return PlayLightAnimCore(found,trigger,layer,0,modifier,lights,offset,id);
    }

    public bool PlayLightAnim(ObjectType type, string trigger, bool modifier = false, CubeLightModifier? lights = null, int timerOffsetMs = 0, Action? completed = null)
        => PlayLightAnim(type, trigger, EngineLayer, 0, modifier, lights, timerOffsetMs,completed);

    /// <summary>Host API compatibility: durationMs and timerOffsetMs are added with u32 wrap and
    /// stored as the single native signed int at CurrentAnimInfo+0x44. Both initial and subsequent
    /// patterns use that same offset; durationMs does not replace a pattern's duration.</summary>
    public bool PlayLightAnim(ObjectType type, string trigger, int layer, uint durationMs = 0, bool modifier = false, CubeLightModifier? lights = null, int timerOffsetMs = 0, Action? completed = null)
        => _robot.Engine.InvokeEngineRequest(()=>PlayLightAnimCore(type,trigger,layer,durationMs,modifier,lights,timerOffsetMs,null,completed));
    // fidelity: M4-051 — 0x00638220..0x00638290: tag is assigned only after generic play succeeds.
    public bool PlayLightAnimFromAction(ObjectType type,string trigger,uint tag,Action? completed=null)
        => _robot.Engine.InvokeEngineRequest(()=>PlayLightAnimCore(type,trigger,UserLayer,0,false,null,0,null,completed,tag));
    private bool PlayLightAnimCore(ObjectType type,string trigger,int layer,uint durationMs,bool modifier,CubeLightModifier? lights,int timerOffsetMs,uint? nativeObjectId,Action? completed=null,uint actionTag=0)
    {
        Animations.EnsureLoaded(); // Standalone host initialization precedes the component state lock.
        Playing pushed;
        CubeLightPattern first;
        uint objectId;
        var logs = new List<Action>();
        void Log(string line) => logs.Add(() => this.Log(line));
        void SourceLog(EngineLogLevel level, string? channel, string key, string text) => logs.Add(() => this.SourceLog(level, channel, key, text));
        try
        {
            lock (_gate)
            {
                var connected = _robot.LocalizationWorld.ConnectedObjects.FirstOrDefault(o => o.Type == type);
                if (nativeObjectId is null && connected is null && (!_infos.TryGetValue(type, out var retained) || retained.ObjectId == uint.MaxValue))
                {
                    if(_robot.VisionModeRecipient is null)Log("MISSING: M4-009 / M11-004 | PlayLightAnim requires the real BlockWorld ObjectID; active-slot fallback is not used");
                    else Log($"Stack.CubeLightComponent.PlayLightAnim.NoObjectForType: no connected or retained world ObjectID for {type}; play refused");
                    return false;
                }
                uint id = nativeObjectId ?? connected?.ObjectId ?? _infos[type].ObjectId;
                objectId = id;
                string layerName = layer switch { 0 => "User/Game", 1 => "Engine", 2 => "State", _ => "Invalid" };
                if (!_infos.TryGetValue(type, out var info) || (nativeObjectId is not null && info.ObjectId != id)) { SourceLog(EngineLogLevel.Info, "CubeLightComponent", "CubeLightComponent.PlayLightAnim.InvalidObjectID", $"No object with id {id} exists in _objectInfo map can't play animation"); return false; }
                var stack = info.Layers[layer];
                if (stack.Count > 0 && !stack[^1].CanBeOverridden) { SourceLog(EngineLogLevel.Info, "CubeLightComponent", "CubeLightComponent.PlayLightAnim.CantBeOverridden", $"Current anim {stack[^1].Name} can not be overridden on object {id} layer {layerName}"); return false; }
                string name = Animations.AnimationForTrigger(trigger);
                var anim = Animations.ForTrigger(trigger);
                if (anim is null || anim.Length == 0) { SourceLog(EngineLogLevel.Warning, null, "CubeLightComponent.NoAnimForTrigger", $"No animation found for trigger {name}"); return false; }
                if (info.GameLayerOnly && layer != UserLayer) { SourceLog(EngineLogLevel.Info, "CubeLightComponent", "CubeLightComponent.OnlyGameLayerEnabled", $"Only game layer is enabled, refusing to play anim {name} on object {id} layer {layerName}"); return false; }
                if (!info.GameLayerOnly && layer == UserLayer) { SourceLog(EngineLogLevel.Info, "CubeLightComponent", "CubeLightComponent.PlayLightAnim.NotPlayingUserAnim", "Refusing to play anim on User/Game layer when it is not enabled"); return false; }
                bool backBlended = stack.Count > 0 && stack[^1].Blended;
                var input = anim;
                if (modifier)
                {
                    if (lights is null) { Log("MISSING: M4-046 | modifier argument requires the caller's ObjectLights"); return false; }
                    input = ApplyAnimModifier(anim, lights);
                }
                CubeLightPattern[] blended = Array.Empty<CubeLightPattern>();
                bool blendResult = false;
                if (connected is null)
                    SourceLog(EngineLogLevel.Info, "CubeLightComponent", "CubeLightComponent.BlendAnimWithCurLights.NullActiveObject",
                        $"No active object with id {id} found in block world unable to blend anims");
                else
                {
                    var current = connected.ActiveLedState; // Zero from ActiveObject construction; normalized by SetLEDs later.
                    blended = BlendAnimWithCurLights(input, current, out blendResult);
                }
                if (backBlended && blendResult) { SourceLog(EngineLogLevel.Info, "CubeLightComponent", "CubeLightComponent.PlayLightAnim.AlreadyPlayingBlendedAnim", "Can't play a blended anim over already playing blended anim"); return false; }
                bool useBlended = (modifier || blendResult) && !backBlended;
                if (useBlended && connected is null)
                { Log("Stack.CubeLightComponent.SkipNullModifier: M1-034 / M4-045: modifier with NullActiveObject reads past native node at 0x006386DA; push skipped, prior state retained"); return false; }
                string playingName = name;
                if (useBlended)
                {
                    if(blended.Length<info.BlendedSlots[layer].Patterns.Length)
                        foreach(var buried in stack)
                            if(buried.Blended&&buried.Index>=blended.Length)buried.Dangling=true;
                    info.BlendedSlots[layer].Patterns = blended;
                    playingName = stack.Count == 0 ? name : stack[^1].Name + "+" + name;
                }
                else if (anim[0].DurationMs == 0) stack.Clear();
                var selected = useBlended ? info.BlendedSlots[layer].Patterns : anim;
                first = selected[0];
                uint offset = unchecked(durationMs + (uint)timerOffsetMs);
                uint duration = unchecked(first.DurationMs + offset);
                var playing = new Playing { BaseAnim = anim, Slot = useBlended ? info.BlendedSlots[layer] : null, Name = playingName, Trigger = trigger, Blended = useBlended, Index = 0,
                    TimerMs = (first.DurationMs | offset) == 0 ? 0 : unchecked(NowMs + duration), TimerOffset = unchecked((int)offset),
                    CanBeOverridden = anim[^1].CanBeOverridden, Completed=completed };
                stack.Add(playing);
                // FromAction's post-success back-entry store, committed with the successful push under the host lock.
                // All rejection paths precede this point; both remaining returns are true (even when SetLights fails).
                // Keeping the store here prevents a concurrent host Update from invoking it with the temporary tag 0.
                playing.CompletionTag=actionTag;
                pushed = playing;
                if (layer > info.CurrentLayer) { SourceLog(EngineLogLevel.Info, "CubeLightComponent", "CubeLightComponent.PlayLightAnim.LightsNotSet", $"Lights not set on object {id} layer {layerName} because current layer is {unchecked((uint)info.CurrentLayer)}"); return true; }
                SourceLog(EngineLogLevel.Info, "CubeLightComponent", "CubeLightComponent.PlayLightAnim.SettingLights", $"Playing trigger {trigger} on layer {layerName} for object {id}");
                info.CurrentLayer = layer;
                pushed.PendingSend = true;

            }
        }
        finally { foreach (var call in logs) call(); }
        // Every state/send path takes FrameGate before the light gate and _sendGate; internal reentry preserves that order.
        uint result;
        try { SendTransitionMessage(objectId); result = SetObjectLights(type, first); }
        catch { lock (_gate) pushed.PendingSend = false; throw; }
        lock (_gate)
        {
            if (result != 0) pushed.TimerMs = 0;
            pushed.PendingSend = false; // timer clear + exposing the completed push are one locked commit
        }
        // Section 13.2: Update's treatment of the cleared timer is unread; its existing behavior stays.
        return true;
    }

    // fidelity: M4-018
    /// <summary>
    /// CubeLightComponent::Update(true) (LC3, 0x00637998..0x00637E02) each Robot::Update: the top of the current layer
    /// advances to its next pattern when its timer expires (timer = now + that pattern's duration + the stored offset when their OR is nonzero, SetObjectLights),
    /// and is popped after its last pattern; an empty layer goes back to layer 2 and PickNextAnimForDefaultLayer
    /// plays Sleep/SleepNoFade, Carrying (0), Visible (0x25) or Connected (1).
    /// A timer expires at now ≥ timer (0x00637998 <c>bhs</c>). The timer is 0 exactly when (duration_ms | offset) == 0,
    /// and then holds unless +0x41 is set (0x0063798A..0x00637994, W4); +0x41 is set only by
    /// <see cref="StopAllAnimsOnLayer"/> and <see cref="StopLightAnim"/> (W2), and a pop with +0x41 set happens even
    /// though patterns remain.
    /// W4/P2/P3: after a pop, consecutive stopped entries are popped too (P2), and if an animation is left on the layer
    /// its current pattern is resent immediately - SetObjectLights, with no timer or iterator reset (P3).
    /// </summary>
    internal void Update() => Update(pickDefaultLayer: true);

    internal void Update(bool pickDefaultLayer)
    {
        // The vision worker uses World.FrameGate before delivering pose-state callbacks. Preserve that
        // order before taking the light gate, including during reentrant completion sends.
        using var poseRead=_robot.LocalizationWorld.EnterPoseMutation();
        lock(_gate)UpdateLocked(pickDefaultLayer);
    }
    private void UpdateLocked(bool pickDefaultLayer)
    {
        var calls = new List<Action>();
        var defaults = new HashSet<ObjectType>();
        // fidelity: M4-051 — copy callback, pop, invoke, then inspect the remaining stack (including reentrant changes).
        void Pop(List<Playing> stack,bool invoke=true)
        {
            if(!invoke){stack.RemoveAt(stack.Count-1);return;}
            var callback=stack[^1].Completed;
            uint tag=stack[^1].CompletionTag;
            stack.RemoveAt(stack.Count-1);
            if(callback is null||!invoke)return;
            var pending=calls.ToArray();calls.Clear();
            foreach(var call in pending)call();
            if(tag==0 || ActionRunnerTagCounter.Global.IsInUse(tag))_robot.Engine.RunIsolated(callback);
        }
        void Send(ObjectType type, CubeLightPattern pattern, bool transition)
        {
            uint? id = TryObjectId(type);
            calls.Add(() => {
                if (transition) SendTransitionMessage(id);
                if (SetObjectLights(type, pattern,out bool skipped) != 0)
                {
                    if(skipped)Log("Stack.CubeLightComponent.Update.RelativeSendSkipped: M1-034/M4-052 skipped invalid marker access before wire send; prior light state retained");
                    else SourceLog(EngineLogLevel.Warning, null, "CubeLightComponent.Update.SetLightsFailed", "");
                }
            });
        }
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
            foreach (var (type, info) in _infos.ToArray())
            {
                var stack = info.Layers[info.CurrentLayer];
                if (stack.Count > 0)
                {
                    var top = stack[^1];
                    if (top.PendingSend) continue;
                    bool waiting = top.TimerMs == 0 || now < top.TimerMs;
                    if (waiting && !top.Stop) continue;                 // W4: holds
                    // fidelity: M1-034, M4-045 — advance also dereferences the freed node, even with +0x41 set.
                    if(top.Dangling)
                    {
                        string name=top.Name;int index=top.Index;bool stopped=top.Stop;
                        calls.Add(()=>Log(FormattableString.Invariant($"Stack.CubeLightComponent.SkipDanglingBlendedAdvance: M1-034 / M4-045: skipped {(stopped?"Stop":"advance")} of dangling {name}, index {index}; native freed-node read 0x006379A6..0x006379AA; retiring this entry, preserving lower entries")));
                    }
                    else if (!top.Stop)
                    {
                        top.Index++;
                        if (top.Index < top.Anim.Length)
                        {
                            var next = top.Anim[top.Index];
                            top.TimerMs = (next.DurationMs | unchecked((uint)top.TimerOffset)) == 0 ? 0
                                : unchecked(now + next.DurationMs + (uint)top.TimerOffset);
                            Send(type, next, transition: true);
                            continue;
                        }
                    }
                    // W4: +0x41 set, or the last pattern was reached. Pop, then P2: pop consecutive stopped entries.
                    Pop(stack,invoke:!top.Dangling); // M1-034/M4-045: a fault-retired entry cannot claim normal completion.
                    // 0x00637A0A / 0x00637A4A: callback may change CurrentLayer; never retain the old list.
                    stack=info.Layers[info.CurrentLayer];
                    while (stack.Count > 0 && stack[^1].Stop)
                    {
                        // Native copies callback/tag from the freed first node here, not this stopped entry.
                        Pop(stack,invoke:false);
                        Log("Stack.CubeLightComponent.SkipStoppedCallback: M1-034/M4-051 skipped stopped-loop callback; freed first-node reads 0x00637A06/0x00637A0C; entry popped");
                        stack=info.Layers[info.CurrentLayer];
                    }
                    if (stack.Count > 0)
                    {
                        // P3: resend the lower animation's current pattern, with no timer or iterator reset.
                        var lower = stack[^1];
                        uint? id = TryObjectId(type);
                        int layer = info.CurrentLayer;
                        if (id is { } previousId) calls.Add(() => SourceLog(EngineLogLevel.Info, "CubeLightComponent", "CubeLightComponent.Update.PlayingPrevious", $"Have previous anim {lower.Name} on layer {LayerName(layer)} id {previousId}"));
                        else calls.Add(() => Log("MISSING: M4-018 | Update.PlayingPrevious 0x00637AA4: native ObjectID unavailable"));
                        // fidelity: M1-034
                        // fidelity: M4-045 — list::assign shortened the shared slot beneath this entry.
                        if(lower.Dangling)
                        {
                            string skippedName=lower.Name;int skippedIndex=lower.Index,slotLength=lower.Anim.Length;
                            calls.Add(()=>Log(FormattableString.Invariant($"Stack.CubeLightComponent.SkipDanglingBlendedResend: M1-034 / M4-045: skipped resend of dangling {skippedName}, index {skippedIndex}, current shared slot length {slotLength}; native freed-node read 0x00637ACC..0x00637AE4; previous state retained")));
                            continue;
                        }
                        Send(type, lower.Anim[lower.Index], transition: true);
                        continue;
                    }
                }
                // fidelity: M4-018
                // P5 (0x00637B04..0x00637C06): an empty layer with gameLayerOnly set sends the static off ObjectLights;
                // otherwise the current layer goes back to 2 and the default layer-2 animation is picked.
                if (info.GameLayerOnly)
                {
                    Send(type, OffLights, transition: false);
                    continue;
                }
                int previousLayer = info.CurrentLayer;
                uint? objectId = TryObjectId(type);
                info.CurrentLayer = StateLayer;
                if (objectId is { } emptyId) calls.Add(() => SourceLog(EngineLogLevel.Info, "CubeLightComponent", "CubeLightComponent.Update.NoAnimsLeftOnLayer", $"No more animations left on layer {LayerName(previousLayer)} for object {emptyId} going to State layer"));
                else calls.Add(() => Log("MISSING: M4-018 | Update.NoAnimsLeftOnLayer 0x00637BD6: native ObjectID unavailable"));
                if (pickDefaultLayer) defaults.Add(type);
            }
        }
        foreach (var call in calls) call();
        foreach (var t in defaults) PickNextAnimForDefaultLayer(t);
    }

    // fidelity: M4-018
    /// <summary>
    /// W2: <c>StopAllAnimsOnLayer(layer, id)</c> (0x00638D80): every entry in that layer's stack gets +0x41 = 1,
    /// then Update runs. For a specific cube type only that object's layer is walked.
    /// </summary>
    internal void StopAllAnimsOnLayer(int layer, ObjectType? type = null)
    {
        if (type is null) SourceLog(EngineLogLevel.Info, "CubeLightComponent", "CubeLightComponent.StopAllAnimsOnLayer.AllObjects", $"Stopping all anims for all objects on layer {LayerName(layer)}");
        else
        {
            bool exists;
            lock (_gate) exists = _infos.ContainsKey(type.Value);
            if (!exists)
            {
                if (TryObjectId(type.Value) is { } id) SourceLog(EngineLogLevel.Info, "CubeLightComponent", "CubeLightComponent.StopAllAnimsOnLayer.BadObject", $"ObjectId {id} does not exist in _objectInfo map unable to stop anims");
                else Log("MISSING: M4-018 | StopAllAnimsOnLayer.BadObject 0x00638E72: type-only caller has no native ObjectID");
                return;
            }
        }
        lock (_gate)
        {
            foreach (var (t, info) in _infos)
                if (type is null || t == type)
                    foreach (var p in info.Layers[layer]) p.Stop = true;
        }
        Update(pickDefaultLayer: true);
    }

    // fidelity: M4-018
    /// <summary>
    /// W2: <c>StopLightAnim(trigger, layer, id)</c> (0x00638F58): every stack entry whose trigger matches gets
    /// +0x41 = 1, then Update runs. The engine's stop flag is per entry, keyed by the animation's trigger; this stack
    /// keys a playing animation by its animation name, so the match is on the pattern's animation name.
    /// </summary>
    internal void StopLightAnim(string trigger, int layer, ObjectType? type = null, bool pickDefaultLayer = true)
    {
        int? id = type is null ? -1 : TryObjectId(type.Value) is { } value ? unchecked((int)value) : null;
        if (id is { } objectId) SourceLog(EngineLogLevel.Info, "CubeLightComponent", "CubeLightComponent.StopLightAnim", $"Stopping {trigger} on object {objectId} on layer {LayerName(layer)}.");
        else Log("MISSING: M4-018 | StopLightAnim 0x00638F9C: type-only caller has no native ObjectID");
        bool found = false;
        lock (_gate)
        {
            foreach (var (t, info) in _infos)
                if (type is null || t == type)
                    foreach (var p in info.Layers[layer])
                        if (MatchesTrigger(p, trigger)) { p.Stop = true; found = true; }
        }
        Update(pickDefaultLayer); // native Update precedes the state-string serialization.
        string state;
        lock (_gate)
        {
            var text = new System.Text.StringBuilder();
            foreach (var info in _infos.Values.Where(i => id == -1 || unchecked((int)i.ObjectId) == id).OrderBy(i => unchecked((int)i.ObjectId)))
            {
                text.Append($"object={unchecked((int)info.ObjectId)} layer={LayerName(layer)} currLayer={LayerName(info.CurrentLayer)} [");
                foreach (var p in info.Layers[layer]) text.Append($"{p.Name}:{p.Trigger}@{p.TimerMs}{(p.Stop ? ":STOP_NOW" : "")} ");
                text.Append(']');
            }
            state = text.ToString();
        }
        SourceLog(EngineLogLevel.Debug, "CubeLightComponent", "CubeLightComponent.StopLignAnim.Result",
            $"{(found ? "found" : "did not find")} anim '{trigger}'. Current state: {state}"); // 0x006390BE..0x006390C2 forwards the native bool parameter.
    }

    /// <summary>The animation name a trigger maps to; a playing entry matches when its name is the trigger's name.</summary>
    private bool MatchesTrigger(Playing p, string trigger) => p.Trigger == trigger;

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
        => _robot.Engine.InvokeEngineRequest(()=>{EnableCubeSleepCore(enable,skipAnimation);return true;});
    private void EnableCubeSleepCore(bool enable,bool skipAnimation)
    {
        SourceLog(EngineLogLevel.Info, "CubeLightComponent", "CubeLightComponent.EnableCubeSleep", $"{(enable ? "Enabling" : "Disabling")} cube sleep");
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
        => _robot.Engine.InvokeEngineRequest(()=>{EnableGameLayerOnlyCore(enable,type,objectId);return true;});
    private void EnableGameLayerOnlyCore(bool enable,ObjectType? type,int objectId)
    {
        // fidelity: M4-018
        // Entry log (0x0063997A sChanneledInfoF, before the C13.4 gates): channel "CubeLightComponent" (printed as
        // "[CubeLightComponent]", the IHelper channel convention), event "CubeLightComponent.EnableGameLayerOnly" (0x00639BAC), format
        // "%s game layer only for %s" (0x00639BD4): "Enabling" (0x00BFAA93) when enable != 0 else "Disabling"
        // (0x00BFAA9C); "all objects" (0x00639B94) when ObjectID == -1, else "object " (0x00639BA0) + the id.
        SourceLog(EngineLogLevel.Info, "CubeLightComponent", "CubeLightComponent.EnableGameLayerOnly", $"{(enable ? "Enabling" : "Disabling")} game layer only for " +
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
        => _robot.Engine.InvokeEngineRequest(()=>{SetEnableFreeplayLightStatesCore(enable,objectID);return true;});
    private void SetEnableFreeplayLightStatesCore(bool enable,int objectID)
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
            PlayLightAnimCore(type,_sleepSkipFade?"SleepNoFade":"Sleep",StateLayer,0,false,null,0,null);
            return;
        }
        // MISSING: M11-004 | the located object's +0x24 == 1 (IsVisible) and the located object itself are M11-004's (BlockWorld's ById helpers); with no hook the
        // default layer never reads Visible.
        if (IsVisible is null)
            Behavior.SteppedBehavior.ReportMissing("M11-004 | CubeLightComponent::PickNextAnimForDefaultLayer reads the located object's +0x24 (0x00637DA6..0x00637E02); no BlockWorld hook is wired (IsVisible), so the Visible trigger is never chosen");
        string trigger = IsCarried?.Invoke(type) == true ? "Carrying"
                       : IsVisible?.Invoke(type) == true ? "Visible"
                       : "Connected";
        PlayLightAnimCore(type,trigger,StateLayer,0,false,null,0,null);
    }

    // fidelity: M4-018
    /// <summary>
    /// SetObjectLights (LC4, 0x00637E6C..0x00637EFC): connected with mode 0, otherwise located in current origin;
    /// both require ActiveCube, else send nothing. ActiveObject::SetLEDs (0x004E48B0..0x004E49A2):
    /// both periods 0 gives colours 0 and periods 0x7FFFFFFF; on = 0 gives onColor := offColor, on := 0x7FFFFFFF;
    /// off = 0 gives offColor := onColor, off := 0x7FFFFFFF; gamma 0x80. MakeStateRelativeToXY with mode 0 returns.
    /// Then SetLights with the pattern's rotation.
    /// </summary>
    private uint? TryObjectId(ObjectType type) => _robot.LocalizationWorld.ConnectedObjects.FirstOrDefault(o => o.Type == type)?.ObjectId;
    private static string LayerName(int layer) => layer switch { 0 => "User/Game", 1 => "Engine", 2 => "State", _ => "Invalid" };
    private void SendTransitionMessage(uint? objectId)
    {
        bool enabled;
        lock (_gate) enabled = _transitionMessagesEnabled;
        if (!enabled)
        {
            if (Interlocked.Exchange(ref _transitionIngressMissingReported, 1) == 0)
                // fidelity: M4-040 — app transition ingress is unavailable; native flag stays false.
                Log("MISSING: M4-040 | EnableCubeLightsStateTransitionMessages ingress (0x0063A2EA) is not wired; comp+0x20 remains false");
            return;
        }
        if (objectId is not { } id)
        {
            Log("MISSING: M4-018 | SendTransitionMessage.NullObject 0x00638150: type-only caller has no native ObjectID");
            return;
        }
        if (_robot.VisionModeRecipient?.World.ConnectedObjects.Any(o => o.ObjectId == id) != true)
        {
            SourceLog(EngineLogLevel.Warning, null, "CubeLightComponent.SendTransitionMessage.NullObject", $"Got null object using id {unchecked((int)id)}");
            return;
        }
        Log("MISSING: M4-018 | SendTransitionMessage 0x00638758..0x006387D8 game payload has no adopted row");
    }

    private uint SetObjectLights(ObjectType type, CubeLightPattern p)
        =>SetObjectLights(type,p,out _);
    private uint SetObjectLights(ObjectType type,CubeLightPattern p,out bool skipped)
    {
        uint? id;
        lock(_gate)id=_infos.TryGetValue(type,out var info)?info.ObjectId:null;
        // Static-off has no animation ObjectInfo when no resources loaded. Its native caller still supplies
        // the real connected ObjectID; obtain that identity here, never the radio slot as a stand-in.
        id??=_robot.LocalizationWorld.ConnectedObjects.FirstOrDefault(o=>o.Type==type)?.ObjectId;
        return SetObjectLightsByIdCore(id??uint.MaxValue,p,out skipped);
    }

    // fidelity: M4-052, M4-018 — native ObjectID entry; relative sends the selected located object's own ActiveId.
    internal uint SetObjectLightsById(uint id,CubeLightPattern p,Func<KnownMarker,Pose3d,Vec3>? checkedNormalInput=null)
        =>SetObjectLightsByIdCore(id,p,out _,checkedNormalInput);
    private uint SetObjectLightsByIdCore(uint id,CubeLightPattern p,out bool skipped,Func<KnownMarker,Pose3d,Vec3>? checkedNormalInput=null)
    {
        skipped=false;
        var world=_robot.LocalizationWorld;
        using var poseRead=world.EnterPoseMutation(); // same gate as every world pose writer and the vision worker
        var cube=p.RelativeMode==0?world.GetConnectedActiveObjectByIdHelper(id):world.GetLocatedObjectByIdHelper(id);
        if (cube is null || !CubeGeometry.IsCube(cube.Type))
        { SourceLog(EngineLogLevel.Info, "CubeLightController", "CubeLightController.SetObjectLights.NullActiveObject", "Null active object pointer"); return 0x04000000; }
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
        // Host atomicity: the retained state and its grouped wire send describe the same final writer.
        lock (_sendGate)
        {
            var relative=RelativeCubeLights.Apply(cube,leds,p.RelativeMode,p.RelativePoint,SourceLog,Log,checkedNormalInput);
            if(relative is null){skipped=true;return 0x04000000;} // missing callee/native marker fault, before any wire send
            lock (_gate) cube.ActiveLedState=relative;
            return SetLights(unchecked((uint)cube.ActiveId),relative,gamma:0x80,p.RotationPeriodMs);
        }
    }

    // fidelity: M4-018
    /// <summary>
    /// SetLights (LC5, 0x0063A654..0x0063A800): SetCubeGamma {gamma} only when it differs from comp+0x28, then
    /// CubeID {u32 activeID, u8 (rotation + 29) / 30}, then CubeLights with LEDs 0..3 in order: WhiteBalanceColor
    /// scales G and B by 0.6 (truncating) when R ≠ 0, then the colour word; the frame conversion; the signed offset.
    /// All reliable. The gamma test and the up-to-three sends are one group under <see cref="_sendGate"/>, so no other
    /// SetLights (another cube's CubeID and CubeLights, from another thread) can come between them.
    /// </summary>
    private uint SetLights(uint activeId, LedPattern[] leds, byte gamma, uint rotationMs)
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
            return _robot.SendMessage(new CubeLights { Lights = wire }) ? 0u : 1u;
        }
    }

    /// <summary>Serialises SetLights' SetCubeGamma + CubeID + CubeLights group (the CubeID selects the cube the CubeLights is for).</summary>
    private readonly object _sendGate = new();

    /// <summary>WhiteBalanceColor (0x0063A894..0x0063A98E): G and B × 0.6, truncated, when R ≠ 0.</summary>
    internal static LedColor WhiteBalance(LedColor c) =>
        c.R == 0 ? c : c with { G = (byte)(c.G * 0.6f), B = (byte)(c.B * 0.6f) };
}
