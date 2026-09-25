using System.Text.Json;

namespace Cozmo.Robot.Animation;

// fidelity: M5-001, M5-002, M5-004, M5-006, M5-009, M5-016
/// <summary>
/// <c>CannedAnimationContainer::DefineFromJson</c> and <c>Animation::DefineFromJson</c> (gap2 J1..J4; gap4 J1.1..J1.10,
/// P1): the clip is named by the first top-level key in JsonCpp's (ordinal) order; "_PROCEDURAL_" is skipped; each array
/// element is a keyframe whose "Name" is its class name, built, then added with AddNewKeyFrameToBack. The first failure
/// ends the load, keeping what came before (J1.1). Numbers go through JsonCpp's conversions (J1.4): asInt/asUInt range-check
/// and truncate; asFloat narrows; s8/s16 and u8/u16 are the low bits.
/// </summary>
internal static class JsonClipLoader
{
    /// <summary>FaceAnimationManager::ProceduralAnimName (gap4 P1).</summary>
    public const string ProceduralAnimName = "_PROCEDURAL_";

    /// <summary>A JsonCpp conversion that throws (a value out of range, a non-number): the load stops there.</summary>
    private sealed class JsonCppException : Exception
    {
        public JsonCppException(string m) : base(m) { }
    }

    public static AnimationClip? Load(string file, Action<string>? log)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(file));
        var root = doc.RootElement;
        var names = root.ValueKind == JsonValueKind.Object
            ? root.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToList()
            : new List<string>();
        if (names.Count == 0)
        {
            log?.Invoke("error: CannedAnimationContainer.DefineFromJson.EmptyFile: Found no animations in JSON");
            return null;
        }
        if (names.Count > 1)
            log?.Invoke($"warning: CannedAnimationContainer.DefineFromJson.TooManyAnims: Expecting only one animation per json file, found {names.Count}. Will use first: {names[0]}");
        string name = names[0];
        if (name == ProceduralAnimName)
        {
            log?.Invoke($"warning: CannedAnimationContainer.ReservedName: Skipping animation with reserved name '{name}'");
            return null;
        }

        var probe = new StreamAnimation(name, isLive: false);
        var frames = new List<Keyframe>();
        bool failed = false;
        var arr = root.GetProperty(name);
        if (arr.ValueKind == JsonValueKind.Array)
        {
            int i = 0;
            foreach (var el in arr.EnumerateArray())
            {
                string? error = null;
                try
                {
                    if (el.ValueKind != JsonValueKind.Object) error = "Animation.DefineFromJson: a frame is not an object";
                    else if (!el.TryGetProperty("Name", out var n) || n.ValueKind != JsonValueKind.String)
                        error = "Animation.DefineFromJson.FrameNameMissing";
                    else
                    {
                        var k = Define(n.GetString()!, el, log, name, out error);
                        if (k is not null && !probe.AddNew(k)) error = $"Animation.DefineFromJson.AddKeyFrameFailure: {n.GetString()} frame {i}";
                        if (k is not null && error is null) frames.Add(k);
                    }
                }
                catch (JsonCppException e) { error = e.Message; }
                if (error is not null)
                {
                    log?.Invoke($"error: {name}: {error}");
                    failed = true;
                    break;
                }
                i++;
            }
        }

        var sorted = frames.OrderBy(f => f.TriggerTimeMs).ToList();
        AnimationTrack tracks = 0;
        uint end = 0;
        foreach (var f in sorted) { tracks |= f.Track; end = Math.Max(end, f.EndTimeMs); }
        return new AnimationClip { Name = name, Keyframes = sorted, Tracks = tracks, DurationMs = end, LoadTruncated = failed };
    }

    // ------------------------------------------------------------------ JsonCpp (J1.4)

    private static double Num(JsonElement e, string what)
    {
        if (e.ValueKind == JsonValueKind.Number) return e.GetDouble();
        if (e.ValueKind == JsonValueKind.True) return 1;
        if (e.ValueKind == JsonValueKind.False) return 0;
        throw new JsonCppException($"JsonCpp: '{what}' is not convertible to a number");
    }

    /// <summary>asUInt: range [0, 2^32 − 1] (else a throw), vcvt.u32.f64 (truncates).</summary>
    private static uint AsUInt(JsonElement e, string what)
    {
        double d = Num(e, what);
        if (d < 0 || d > 4294967295.0) throw new JsonCppException($"JsonCpp: '{what}' out of UInt range");
        return (uint)Math.Truncate(d);
    }

    /// <summary>asInt: range [−2^31, 2^31 − 1] (else a throw), vcvt.s32.f64 (truncates toward zero).</summary>
    private static int AsInt(JsonElement e, string what)
    {
        double d = Num(e, what);
        if (d < int.MinValue || d > int.MaxValue) throw new JsonCppException($"JsonCpp: '{what}' out of Int range");
        return (int)Math.Truncate(d);
    }

    private static float AsFloat(JsonElement e, string what) => (float)Num(e, what);

    private static ulong AsUInt64(JsonElement e, string what)
    {
        if (e.ValueKind == JsonValueKind.Number && e.TryGetUInt64(out var u)) return u;
        double d = Num(e, what);
        if (d < 0 || d > 18446744073709551615.0) throw new JsonCppException($"JsonCpp: '{what}' out of UInt64 range");
        return (ulong)Math.Truncate(d);
    }

    private static bool Present(JsonElement o, string key, out JsonElement v) =>
        o.TryGetProperty(key, out v) && v.ValueKind != JsonValueKind.Null;

    private static JsonElement Required(JsonElement o, string key)
    {
        if (!Present(o, key, out var v)) throw new JsonCppException($"IKeyFrame.GetMemberFromJsonMacro: Failed to get '{key}' from Json file.");
        return v;
    }

    // ------------------------------------------------------------------ keyframes (J1.1, J1.2, J1.3, J1.5..J1.10)

    private static Keyframe? Define(string type, JsonElement el, Action<string>? log, string clip, out string? error)
    {
        error = null;
        // J1.3: IKeyFrame::DefineFromJson: "triggerTime_ms" required, asUInt
        if (!Present(el, "triggerTime_ms", out var tr)) { error = "IKeyFrame.ReadFromJson: no triggerTime_ms"; return null; }
        uint trigger = AsUInt(tr, "triggerTime_ms");
        switch (type)
        {
            case "HeadAngleKeyFrame":                                        // J1.5: all three required
                return new HeadKeyframe(trigger, AsUInt(Required(el, "durationTime_ms"), "durationTime_ms"),
                                        unchecked((sbyte)AsInt(Required(el, "angle_deg"), "angle_deg")),
                                        unchecked((byte)AsUInt(Required(el, "angleVariability_deg"), "angleVariability_deg")));
            case "LiftHeightKeyFrame":                                       // J1.6
                return new LiftKeyframe(trigger, AsUInt(Required(el, "durationTime_ms"), "durationTime_ms"),
                                        unchecked((byte)AsUInt(Required(el, "height_mm"), "height_mm")),
                                        unchecked((byte)AsUInt(Required(el, "heightVariability_mm"), "heightVariability_mm")));
            case "BodyMotionKeyFrame":
                return DefineBody(trigger, el, log, clip, out error);
            case "ProceduralFaceKeyFrame":
                return DefineFace(trigger, el);
            case "BackpackLightsKeyFrame":
                return DefineBackpack(trigger, el, out error);
            case "RobotAudioKeyFrame":
                return DefineAudio(trigger, el, out error);
            case "FaceAnimationKeyFrame":
            case "EventKeyFrame":
            case "DeviceAudioKeyFrame":
            case "RecordHeadingKeyFrame":
            case "TurnToRecordedHeadingKeyFrame":
                throw new NotSupportedException(
                    $"MISSING (M5-001): {type}::SetMembersFromJson is not in the M5 inventory (gap4 J1 covers Head, Lift, Body, " +
                    "ProceduralFace, BackpackLights and RobotAudio; no shipped JSON clip holds this type)");
            default:
                error = $"Animation.DefineFromJson.UnrecognizedFrameName: {type}";
                return null;
        }
    }

    /// <summary>
    /// J1.7: "durationTime_ms" asInt (no INT_MAX conversion; a negative one never stops through the unsigned compares);
    /// "speed" s16, required; "radius_mm" required: a string through ProcessRadiusString (C4), otherwise s16 and CheckTurnSpeed.
    /// </summary>
    private static Keyframe? DefineBody(uint trigger, JsonElement el, Action<string>? log, string clip, out string? error)
    {
        error = null;
        uint duration = unchecked((uint)AsInt(Required(el, "durationTime_ms"), "durationTime_ms"));
        short speed = unchecked((short)AsInt(Required(el, "speed"), "speed"));
        if (!Present(el, "radius_mm", out var r))
        {
            error = $"BodyMotionKeyFrame.MissingRadius: {clip}: Missing 'radius_mm' field.";
            return null;
        }
        if (r.ValueKind == JsonValueKind.String)
        {
            var raw = r.GetString()!;
            if (!BodyKeyframe.CheckSpeedForRadiusString(raw, ref speed))
            {
                error = $"BodyMotionKeyFrame.ProcessRadiusString: {clip}: unrecognised radius '{raw}'";
                return null;
            }
            return new BodyKeyframe(trigger, duration, raw, speed);
        }
        short radius = unchecked((short)AsInt(r, "radius_mm"));
        if (Math.Abs((int)speed) >= 221) speed = (short)Math.Clamp((int)speed, -220, 220);   // CheckTurnSpeed
        return new BodyKeyframe(trigger, duration, radius.ToString(System.Globalization.CultureInfo.InvariantCulture), speed);
    }

    /// <summary>
    /// J1.8: ProceduralFace::SetFromJson on the default face: "leftEye"/"rightEye" (19 floats, else unchanged; clipped);
    /// "faceAngle"; "faceCenterX" and "faceCenterY" only both → SetFacePosition; "faceScaleX" and "faceScaleY" only both,
    /// a negative one 0. Nothing rejects the keyframe; "durationTime_ms" is not read.
    /// </summary>
    private static Keyframe DefineFace(uint trigger, JsonElement el)
    {
        var pose = new ProceduralFacePose();
        float[]? Floats(string key) =>
            Present(el, key, out var v) && v.ValueKind == JsonValueKind.Array ? v.EnumerateArray().Select(x => AsFloat(x, key)).ToArray() : null;
        if (Floats("leftEye") is { Length: Eye.ParamCount } l) pose.Left = Eye.FromAsset(l);
        if (Floats("rightEye") is { Length: Eye.ParamCount } r) pose.Right = Eye.FromAsset(r);
        if (Present(el, "faceAngle", out var a)) pose.FaceAngle = AsFloat(a, "faceAngle");
        if (Present(el, "faceCenterX", out var cx) && Present(el, "faceCenterY", out var cy))
            pose.SetFacePosition(AsFloat(cx, "faceCenterX"), AsFloat(cy, "faceCenterY"));
        if (Present(el, "faceScaleX", out var sx) && Present(el, "faceScaleY", out var sy))
        {
            pose.FaceScaleX = MathF.Max(0f, AsFloat(sx, "faceScaleX"));
            pose.FaceScaleY = MathF.Max(0f, AsFloat(sy, "faceScaleY"));
        }
        return new FaceKeyframe(trigger, pose);
    }

    /// <summary>
    /// J1.9: "Back", "Front", "Middle", "Left", "Right" in that order, each required, through GetColorOptional with one
    /// ColorRGBA reused (<see cref="BackpackColor.TryReadAll"/>); a string names a NamedColors entry (MISSING: the table is
    /// not in the inventory; no shipped keyframe uses one); then "durationTime_ms" asInt, required.
    /// </summary>
    private static Keyframe? DefineBackpack(uint trigger, JsonElement el, out string? error)
    {
        error = null;
        float[] Color(string key)
        {
            var v = Required(el, key);
            if (v.ValueKind == JsonValueKind.String)
                throw new NotSupportedException($"MISSING (M5-016): NamedColors::GetByString('{v.GetString()}') is not in the M5 inventory");
            if (v.ValueKind != JsonValueKind.Array) throw new JsonCppException($"BackpackLightsKeyFrame: '{key}' is not an array");
            return v.EnumerateArray().Select(x => AsFloat(x, key)).ToArray();
        }
        var back = Color("Back"); var front = Color("Front"); var middle = Color("Middle"); var left = Color("Left"); var right = Color("Right");
        uint duration = unchecked((uint)AsInt(Required(el, "durationTime_ms"), "durationTime_ms"));
        var k = new LightsKeyframe(trigger, duration, left, right, front, middle, back);
        if (k.EncodedLeds is null) { error = "BackpackLightsKeyFrame: a colour is not an array of 3 or 4 floats"; return null; }
        return k;
    }

    /// <summary>
    /// J1.10: "volume" (1.0), "hasAlts" (false); an "audioEventId" array: the "probability" vector or the scalar as one
    /// element, 1/N each when absent, a count mismatch or a running sum above 1 rejects; each id the low 32 bits of
    /// asUInt64. A scalar id: one ref with the scalar probability or 1.0, no sum check. "audioName" is ignored.
    /// </summary>
    private static Keyframe? DefineAudio(uint trigger, JsonElement el, out string? error)
    {
        error = null;
        float volume = Present(el, "volume", out var vv) ? AsFloat(vv, "volume") : 1f;
        bool hasAlts = Present(el, "hasAlts", out var ha) && Num(ha, "hasAlts") != 0;
        var idEl = Required(el, "audioEventId");
        if (idEl.ValueKind == JsonValueKind.Array)
        {
            var ids = idEl.EnumerateArray().Select(x => (long)(uint)(AsUInt64(x, "audioEventId") & 0xFFFFFFFF)).ToArray();
            float[] probs;
            if (Present(el, "probability", out var p))
                probs = p.ValueKind == JsonValueKind.Array ? p.EnumerateArray().Select(x => AsFloat(x, "probability")).ToArray()
                                                          : new[] { AsFloat(p, "probability") };
            else if (ids.Length > 0) { probs = new float[ids.Length]; Array.Fill(probs, 1.0f / ids.Length); }
            else probs = Array.Empty<float>();
            if (probs.Length != ids.Length) { error = "RobotAudioKeyFrame: the probability count differs from the id count"; return null; }
            float sum = 0f;
            foreach (var q in probs)
            {
                sum += q;
                if (sum > 1.0f) { error = "RobotAudioKeyFrame.TotalProbabilitiesTooHigh"; return null; }
            }
            return new AudioKeyframe(trigger, ids, volume, probs, hasAlts);
        }
        long id = (long)(uint)(AsUInt64(idEl, "audioEventId") & 0xFFFFFFFF);
        float prob = Present(el, "probability", out var sp) ? AsFloat(sp, "probability") : 1f;
        return new AudioKeyframe(trigger, new[] { id }, volume, new[] { prob }, hasAlts);
    }
}
