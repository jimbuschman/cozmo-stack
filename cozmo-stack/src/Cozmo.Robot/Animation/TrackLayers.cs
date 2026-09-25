namespace Cozmo.Robot.Animation;

// fidelity: M5-029
/// <summary>
/// One layer of an <c>ITrackLayerManager</c> (gap1 layer node layout): its track (a copy, with its own iterator and live
/// flag), start time (+0x28), stream time (+0x2C), persistent flag (+0x30), tag (+0x31) and name (+0x34).
/// </summary>
internal sealed class TrackLayer<T> where T : class, IStreamKeyframe
{
    public required StreamTrack<T> Track;
    public int StartTime;
    public int StreamTime;
    public bool IsPersistent;
    public byte Tag;
    public string Name = "";
}

// fidelity: M5-029
/// <summary>
/// <c>ITrackLayerManager&lt;T&gt;</c> (gap1 L1..L8; gap2 Q1.2..Q1.5): layers keyed by a u8 tag in ascending order, and
/// the tag counter (+0x10, 0 at start).
/// </summary>
internal class TrackLayerManager<T> where T : class, IStreamKeyframe
{
    protected readonly SortedDictionary<byte, TrackLayer<T>> Layers = new();
    private byte _tagCounter;

    /// <summary>Q1.2: HaveLayersToSend = (layer-map size ≠ 0).</summary>
    public bool HaveLayersToSend => Layers.Count != 0;
    public int LayerCount => Layers.Count;
    public bool HasLayerWithTag(byte tag) => Layers.ContainsKey(tag);
    public IReadOnlyCollection<TrackLayer<T>> AllLayers => Layers.Values;

    /// <summary>L1: tag++, skipping 0 and any tag already in the map, so 1..255 with wrap.</summary>
    private byte NextTag()
    {
        do { _tagCounter = unchecked((byte)(_tagCounter + 1)); }
        while (_tagCounter == 0 || Layers.ContainsKey(_tagCounter));
        return _tagCounter;
    }

    /// <summary>
    /// L2 AddLayer(name, track, startTime): copies the track (iterator = begin) with isLive = 1, start = the argument,
    /// stream = 0, not persistent. Returns 0, not the tag.
    /// </summary>
    public int AddLayer(string name, StreamTrack<T> track, int startTime, Func<T, T> copy)
    {
        var t = track.CopyFromBegin(copy);
        t.IsLive = true;
        byte tag = NextTag();
        Layers[tag] = new TrackLayer<T> { Track = t, StartTime = startTime, StreamTime = 0, IsPersistent = false, Tag = tag, Name = name };
        return 0;
    }

    /// <summary>L3/Q1.3 AddPersistentLayer(name, track): isLive = 0, start 0, stream 0, persistent. Returns the tag.</summary>
    public byte AddPersistentLayer(string name, StreamTrack<T> track, Func<T, T> copy)
    {
        var t = track.CopyFromBegin(copy);
        t.IsLive = false;
        byte tag = NextTag();
        Layers[tag] = new TrackLayer<T> { Track = t, StartTime = 0, StreamTime = 0, IsPersistent = true, Tag = tag, Name = name };
        return tag;
    }

    /// <summary>
    /// L6 AddToPersistentLayer(tag, kf): when the tag is found, kf.trigger += last keyframe's trigger + 33, then
    /// AddKeyFrameToBack; otherwise nothing.
    /// </summary>
    public void AddToPersistentLayer(byte tag, T kf, Action<T, uint> setTrigger)
    {
        if (!Layers.TryGetValue(tag, out var layer)) return;
        uint last = layer.Track.Last?.Trigger ?? 0;
        setTrigger(kf, kf.Trigger + last + 33);
        layer.Track.AddKeyFrameToBack(kf);
    }

    /// <summary>
    /// L5 / Q1.4 ApplyLayersToFrame, per layer in ascending tag order: fn(track, start, stream); stream += 33; then, if
    /// the track is at its end: persistent and empty → a warning and the persistent flag cleared; persistent → the
    /// iterator on the last keyframe, every earlier keyframe erased, and the stream time put back to the value used this
    /// frame (the layer holds its last face and is never removed); not persistent → removed after the loop. Returns the
    /// OR of the fn results.
    /// </summary>
    public bool ApplyLayersToFrame(Func<StreamTrack<T>, int, int, bool> fn, Action<string>? log = null)
    {
        bool any = false;
        List<byte>? remove = null;
        foreach (var (tag, layer) in Layers)
        {
            int used = layer.StreamTime;
            any |= fn(layer.Track, layer.StartTime, used);
            layer.StreamTime += 33;
            if (!layer.Track.AtEnd) continue;
            if (layer.IsPersistent)
            {
                if (layer.Track.IsEmpty)
                {
                    log?.Invoke("warning: AnimationStreamer.UpdateFace.EmptyPersistentLayer");
                    layer.IsPersistent = false;
                }
                else
                {
                    layer.Track.PinLast();
                    layer.StreamTime = used;
                }
            }
            else (remove ??= new()).Add(tag);
        }
        if (remove is not null) foreach (var t in remove) Layers.Remove(t);
        return any;
    }

    /// <summary>Drops every layer and the tag counter (a new manager).</summary>
    public void Clear()
    {
        Layers.Clear();
        _tagCounter = 0;
    }
}

// fidelity: M5-028, M5-029, M5-031
/// <summary>
/// <c>FaceLayerManager</c>: the face layers, the keep-alive (KeepFaceAlive, blinks, darts) and the eye shifts
/// (gap1 K1..K10, L7; gap3 K5; A33).
/// </summary>
internal sealed class FaceLayerManager : TrackLayerManager<FaceFrame>
{
    /// <summary>The manager's RNG (layout +0).</summary>
    public EngineRandom Rng { get; }

    /// <summary>The drawer's scan-line state the blink flips (K9).</summary>
    private readonly ScanLineState _scanLines;

    public FaceLayerManager(EngineRandom rng, ScanLineState scanLines)
    {
        Rng = rng;
        _scanLines = scanLines;
    }

    /// <summary>K4: blink timer (+0x14), dart timer (+0x18), both 0 from the ctor (0x0058CD78); dart tag (+0x1C).</summary>
    public int BlinkTimerMs, DartTimerMs;
    public byte DartTag;

    public Action<string>? Log;

    private static FaceFrame CopyFrame(FaceFrame f) => f.Copy();

    public int AddLayer(string name, StreamTrack<FaceFrame> track, int startTime) => AddLayer(name, track, startTime, CopyFrame);
    public byte AddPersistentLayer(string name, StreamTrack<FaceFrame> track) => AddPersistentLayer(name, track, CopyFrame);
    public void AddToPersistentLayer(byte tag, FaceFrame kf) => AddToPersistentLayer(tag, kf, (k, t) => k.Trigger = t);

    /// <summary>
    /// A33 / K4..K6 KeepFaceAlive(params), per Update: both timers lose 60. The dart: when EyeDartMaxDistance &gt; 0, the
    /// dart timer ≤ 0, and there is no layer or only the dart's own: GenerateEyeShift (K1); with a dart tag,
    /// AddToPersistentLayer, otherwise a track holding only that keyframe becomes the persistent layer "KeepAliveEyeDart"
    /// (no neutral keyframe at 0, so the first dart jumps); the dart timer = RandIntInRange((int)p20, (int)p21). The blink:
    /// when the blink timer ≤ 0, GenerateBlink and AddLayer("Blink", track, 0); the blink timer = RandIntInRange(BlinkMin,
    /// BlinkMax), with (7500, 30000) and the BadBlinkSpacingParams warning when max ≤ min.
    /// </summary>
    public void KeepFaceAlive(LiveIdleParams p)
    {
        BlinkTimerMs -= 60;
        DartTimerMs -= 60;

        if (p[LiveIdleParam.EyeDartMaxDistance_pix] > 0 && DartTimerMs <= 0
            && (LayerCount == 0 || (LayerCount == 1 && HasLayerWithTag(DartTag))))
        {
            var kf = GenerateEyeShift(p);
            if (DartTag != 0) AddToPersistentLayer(DartTag, kf);
            else
            {
                var track = new StreamTrack<FaceFrame>();
                track.AddKeyFrameToBack(kf);
                DartTag = AddPersistentLayer("KeepAliveEyeDart", track);
            }
            DartTimerMs = Rng.RandIntInRange((int)p[LiveIdleParam.EyeDartSpacingMinTime_ms], (int)p[LiveIdleParam.EyeDartSpacingMaxTime_ms]);
        }

        if (BlinkTimerMs <= 0)
        {
            AddLayer("Blink", GenerateBlink(), 0);
            int min = (int)p[LiveIdleParam.BlinkSpacingMinTime_ms], max = (int)p[LiveIdleParam.BlinkSpacingMaxTime_ms];
            if (max <= min)
            {
                Log?.Invoke($"warning: FaceLayerManager.KeepFaceAlive.BadBlinkSpacingParams: min {min}, max {max}");
                min = 7500;
                max = 30000;
            }
            BlinkTimerMs = Rng.RandIntInRange(min, max);
        }
    }

    /// <summary>
    /// K1 GenerateEyeShift(params): d = EyeDartMaxDistance; x = RandIntInRange((int)−d, (int)d), then y likewise;
    /// dur = RandIntInRange((int)p25, (int)p26); a default face through LookAt(x, y, 5, 5, p28 up, p29 down, p27 outer);
    /// the keyframe's trigger = dur. p23/p24 are not read.
    /// </summary>
    public FaceFrame GenerateEyeShift(LiveIdleParams p)
    {
        float d = p[LiveIdleParam.EyeDartMaxDistance_pix];
        int x = Rng.RandIntInRange((int)-d, (int)d);
        int y = Rng.RandIntInRange((int)-d, (int)d);
        int dur = Rng.RandIntInRange((int)p[LiveIdleParam.EyeDartMinDuration_ms], (int)p[LiveIdleParam.EyeDartMaxDuration_ms]);
        var face = new ProceduralFacePose();
        face.LookAt(x, y, 5f, 5f, p[LiveIdleParam.EyeDartUpMaxScale], p[LiveIdleParam.EyeDartDownMinScale],
                    p[LiveIdleParam.EyeDartOuterEyeScaleIncrease]);
        return new FaceFrame((uint)dur, face);
    }

    /// <summary>
    /// K2 GenerateEyeShift(x, y, xMax, yMax, up, down, outer, dur): the caller's xMax/yMax are replaced by
    /// max(xmin, 128 − xmax) and max(ymin, 64 − ymax) of a default face's eye box (17 and 12), then LookAt with those;
    /// trigger = dur.
    /// </summary>
    public static FaceFrame GenerateEyeShift(float x, float y, float xMax, float yMax, float up, float down, float outer, uint dur)
    {
        _ = xMax; _ = yMax;
        var bb = new ProceduralFacePose().GetEyeBoundingBox();
        float xm = MathF.Max(bb.XMin, ProceduralFacePose.CanvasWidth - bb.XMax);
        float ym = MathF.Max(bb.YMin, ProceduralFacePose.CanvasHeight - bb.YMax);
        var face = new ProceduralFacePose();
        face.LookAt(x, y, xm, ym, up, down, outer);
        return new FaceFrame(dur, face);
    }

    /// <summary>
    /// K10 AddOrUpdateEyeShift(tag, name, x, y, dur, xMax, yMax, up, down, outer): the K2 keyframe; with a tag,
    /// AddToPersistentLayer; otherwise a new track, with a default keyframe at trigger 0 first when dur ≠ 0, then the
    /// keyframe, AddPersistentLayer(name), and the tag stored.
    /// </summary>
    public void AddOrUpdateEyeShift(ref byte tag, string name, float x, float y, uint dur, float xMax, float yMax,
                                    float up, float down, float outer)
    {
        var kf = GenerateEyeShift(x, y, xMax, yMax, up, down, outer, dur);
        if (tag != 0) { AddToPersistentLayer(tag, kf); return; }
        var track = new StreamTrack<FaceFrame>();
        if (dur != 0) track.AddKeyFrameToBack(FaceFrame.Default(0));
        track.AddKeyFrameToBack(kf);
        tag = AddPersistentLayer(name, track);
    }

    /// <summary>
    /// L7 / Q1.11 / gap3 K5 RemovePersistentLayer(tag, dur): an unknown tag does nothing. Otherwise an info log, and a
    /// new live track: when dur ≥ 1 a copy of the layer's current keyframe at trigger 0, then a default keyframe at
    /// trigger dur; AddLayer("Remove" + name, track, 0); the persistent layer erased.
    /// </summary>
    public void RemovePersistentLayer(byte tag, int dur)
    {
        if (!Layers.TryGetValue(tag, out var layer)) return;
        Log?.Invoke($"info: FaceLayerManager.RemovePersistentLayer: {layer.Name}, Tag = {tag} (Layers remaining={Layers.Count - 1})");
        var track = new StreamTrack<FaceFrame>();
        if (dur >= 1 && layer.Track.Current is { } cur)
        {
            var c = cur.Copy();
            c.Trigger = 0;
            track.AddKeyFrameToBack(c);
        }
        track.AddKeyFrameToBack(FaceFrame.Default((uint)Math.Max(dur, 0)));
        AddLayer("Remove" + layer.Name, track, 0);
        Layers.Remove(tag);
    }

    /// <summary>RemoveKeepFaceAlive(dur) (L7, 0x0058CFAC..0x0058CFBE): RemovePersistentLayer(dart tag, dur), tag = 0.</summary>
    public void RemoveKeepFaceAlive(int dur)
    {
        RemovePersistentLayer(DartTag, dur);
        DartTag = 0;
    }

    /// <summary>RemoveEyeShift(tag, dur): RemovePersistentLayer (K10: the live idle removes with dur 0, an instant snap).</summary>
    public void RemoveEyeShift(ref byte tag, int dur)
    {
        RemovePersistentLayer(tag, dur);
        tag = 0;
    }

    // ------------------------------------------------------------------ the blink (K7..K9)

    private static readonly object BlinkGate = new();

    /// <summary>
    /// K8: the blink table at 0x00C5AAD8, {heightMul, widthMul, dur, action}: (0.85, 1.05, 33, 0), (0.60, 1.20, 33, 0),
    /// (0.10, 2.50, 33, 0), (0.05, 5.00, 33, 1), (0.15, 2.00, 33, 2), (0.70, 1.20, 33, 3), (0.90, 1.00, 100, 3).
    /// </summary>
    internal static readonly (float HeightMul, float WidthMul, int DurationMs, byte Action)[] BlinkTable =
    {
        (0.85f, 1.05f, 33, 0), (0.60f, 1.20f, 33, 0), (0.10f, 2.50f, 33, 0), (0.05f, 5.00f, 33, 1),
        (0.15f, 2.00f, 33, 2), (0.70f, 1.20f, 33, 3), (0.90f, 1.00f, 100, 3),
    };

    private static int _blinkIndex;
    private static ProceduralFacePose _blinkOrig = new();

    /// <summary>The six lid parameters the closed frame zeroes and the next restores (K9, table 0x00C5AB48).</summary>
    private static readonly EyeParam[] BlinkLidParams =
    {
        EyeParam.LowerLidY, EyeParam.LowerLidBend, EyeParam.LowerLidAngle,
        EyeParam.UpperLidY, EyeParam.UpperLidBend, EyeParam.UpperLidAngle,
    };

    /// <summary>
    /// K8/K9 ProceduralFaceDrawer::GetNextBlinkFrame(face, dur), a static iterator: at the begin the face is saved as
    /// orig; each frame sets both eyes' EyeScaleX = Clip(orig·widthMul) and EyeScaleY = Clip(orig·heightMul); action 1
    /// flips the drawer's _firstScanLine (at generation time), sets both EyeCenterY to the mean of orig's and zeroes the
    /// six lid parameters; action 2 restores them from orig; past the end: face = orig, dur = 33, the iterator reset,
    /// false.
    /// </summary>
    internal bool GetNextBlinkFrame(ProceduralFacePose face, out int durationMs)
    {
        if (_blinkIndex == 0) _blinkOrig = face.Clone();
        var orig = _blinkOrig;
        if (_blinkIndex >= BlinkTable.Length)
        {
            Assign(face, orig);
            durationMs = 33;
            _blinkIndex = 0;
            return false;
        }
        var (h, w, dur, action) = BlinkTable[_blinkIndex];
        foreach (var (eye, o) in new[] { (face.Left, orig.Left), (face.Right, orig.Right) })
        {
            eye[EyeParam.EyeScaleX] = Eye.Clip(EyeParam.EyeScaleX, o[EyeParam.EyeScaleX] * w, eye[EyeParam.EyeScaleX]);
            eye[EyeParam.EyeScaleY] = Eye.Clip(EyeParam.EyeScaleY, o[EyeParam.EyeScaleY] * h, eye[EyeParam.EyeScaleY]);
        }
        if (action == 1)
        {
            _scanLines.Drawer = 1 - _scanLines.Drawer;
            float mean = (orig.Left[EyeParam.EyeCenterY] + orig.Right[EyeParam.EyeCenterY]) / 2f;
            foreach (var eye in new[] { face.Left, face.Right })
            {
                eye[EyeParam.EyeCenterY] = mean;
                foreach (var p in BlinkLidParams) eye[p] = Eye.Clip(p, 0f, eye[p]);
            }
        }
        else if (action == 2)
        {
            foreach (var (eye, o) in new[] { (face.Left, orig.Left), (face.Right, orig.Right) })
            {
                eye[EyeParam.EyeCenterY] = o[EyeParam.EyeCenterY];
                foreach (var p in BlinkLidParams) eye[p] = o[p];
            }
        }
        durationMs = dur;
        _blinkIndex++;
        return true;
    }

    private static void Assign(ProceduralFacePose to, ProceduralFacePose from)
    {
        to.FaceAngle = from.FaceAngle;
        to.FaceCenterX = from.FaceCenterX;
        to.FaceCenterY = from.FaceCenterY;
        to.FaceScaleX = from.FaceScaleX;
        to.FaceScaleY = from.FaceScaleY;
        for (int i = 0; i < Eye.ParamCount; i++) { to.Left[i] = from.Left[i]; to.Right[i] = from.Right[i]; }
        to.Distorter = from.Distorter?.Clone();
    }

    /// <summary>
    /// K7 GenerateBlink: a default face; loop GetNextBlinkFrame: trigger += dur, then a keyframe of the face (the first at
    /// 33); the last keyframe, returned with false, is added too. Triggers 33, 66, 99, 132, 165, 198, 298, 331.
    /// </summary>
    public StreamTrack<FaceFrame> GenerateBlink()
    {
        lock (BlinkGate)
        {
            var track = new StreamTrack<FaceFrame>();
            var face = new ProceduralFacePose();
            uint trigger = 0;
            bool more;
            do
            {
                more = GetNextBlinkFrame(face, out int dur);
                trigger += (uint)dur;
                track.AddKeyFrameToBack(new FaceFrame(trigger, face.Clone()));
            }
            while (more);
            return track;
        }
    }

    // ------------------------------------------------------------------ the glitch face (G4)

    private static readonly object DistortionGate = new();

    /// <summary>
    /// G4 GenerateFaceDistortion(degree): one default face reused; loop GetNextDistortionFrame: trigger += dur, then a
    /// keyframe with a deep copy of the face and its distorter; the final false keyframe too.
    /// </summary>
    public static StreamTrack<FaceFrame> GenerateFaceDistortion(float degree)
    {
        lock (DistortionGate)
        {
            var track = new StreamTrack<FaceFrame>();
            var face = new ProceduralFacePose();
            uint trigger = 0;
            bool more;
            do
            {
                more = ScanlineDistorter.GetNextDistortionFrame(degree, face, out int dur);
                trigger += (uint)dur;
                track.AddKeyFrameToBack(new FaceFrame(trigger, face.Clone()));
            }
            while (more);
            return track;
        }
    }
}

// fidelity: M5-031
/// <summary><c>BackpackLayerManager</c> (gap2 Q2): the backpack layers and the glitch lights.</summary>
internal sealed class BackpackLayerManager : TrackLayerManager<BackpackFrame>
{
    public BackpackLayerManager(EngineRandom rng) => Rng = rng;

    public EngineRandom Rng { get; }

    /// <summary>NamedColors::RED (0x00C9742F, ff 00 00 ff) encoded: 0xFC00 (Q2).</summary>
    public const ushort Red = 0xFC00;

    /// <summary>LED indices (Q2): [0] Left, [1] Front, [2] Middle, [3] Back, [4] Right.</summary>
    private const int Left = 0, Front = 1, Middle = 2, Back = 3, Right = 4;

    /// <summary>
    /// GenerateGlitchLights (gap2 Q2 G1..G6, 0x0058CB64..0x0058CD2A): the track cleared; KF0 at 0 for 200 ms, all off;
    /// KF1 at 200 for 60, Middle red; KF2 at 260 for 60: from the list {Back, Front, Left, Right} (.rodata 0x00C5ABA8)
    /// i = RandInt(4) and that LED red added to the middle; KF3 at 320 for 60: that entry erased, Middle cleared,
    /// j = RandInt(3) and that LED red too (the first random LED stays red); KF4 at 380 for 60, all off.
    /// </summary>
    public StreamTrack<BackpackFrame> GenerateGlitchLights()
    {
        var track = new StreamTrack<BackpackFrame>();
        var leds = new ushort[5];
        track.AddKeyFrameToBack(new BackpackFrame(0, 200, (ushort[])leds.Clone()));
        leds[Middle] = Red;
        track.AddKeyFrameToBack(new BackpackFrame(200, 60, (ushort[])leds.Clone()));
        var list = new List<int> { Back, Front, Left, Right };
        int i = Rng.RandInt(4);
        leds[list[i]] = Red;
        track.AddKeyFrameToBack(new BackpackFrame(260, 60, (ushort[])leds.Clone()));
        list.RemoveAt(i);
        leds[Middle] = 0;
        int j = Rng.RandInt(3);
        leds[list[j]] = Red;
        track.AddKeyFrameToBack(new BackpackFrame(320, 60, (ushort[])leds.Clone()));
        track.AddKeyFrameToBack(new BackpackFrame(380, 60, new ushort[5]));
        return track;
    }

    public int AddLayer(string name, StreamTrack<BackpackFrame> track, int startTime) =>
        AddLayer(name, track, startTime, f => f.Copy());
}

// fidelity: M5-024, M5-028
/// <summary>
/// The two <c>_firstScanLine</c> statics (M3 B3): the drawer's (.bss 0x0105AB48) and the FaceAnimationManager's
/// (0x0105AB10), both 0 at process start. They are process-wide, as the engine's are: <see cref="Process"/> is shared by
/// every streamer, and a robot removal does not reset them (M5 fix B8).
/// </summary>
internal sealed class ScanLineState
{
    /// <summary>The process's two statics.</summary>
    public static readonly ScanLineState Process = new();

    public volatile int Drawer;
    public volatile int FaceAnimation;
}

/// <summary>LiveIdleAnimationParameter (Unity LiveIdleAnimationParameter.cs): the 30 live-idle and keep-alive tunables.</summary>
public enum LiveIdleParam
{
    BlinkSpacingMinTime_ms, BlinkSpacingMaxTime_ms, TimeBeforeWiggleMotions_ms,
    BodyMovementSpacingMin_ms, BodyMovementSpacingMax_ms, BodyMovementDurationMin_ms, BodyMovementDurationMax_ms,
    BodyMovementSpeedMinMax_mmps, BodyMovementStraightFraction,
    LiftMovementDurationMin_ms, LiftMovementDurationMax_ms, LiftMovementSpacingMin_ms, LiftMovementSpacingMax_ms,
    LiftHeightMean_mm, LiftHeightVariability_mm,
    HeadMovementDurationMin_ms, HeadMovementDurationMax_ms, HeadMovementSpacingMin_ms, HeadMovementSpacingMax_ms,
    HeadAngleVariability_deg,
    EyeDartSpacingMinTime_ms, EyeDartSpacingMaxTime_ms, EyeDartMaxDistance_pix, EyeDartMinScale, EyeDartMaxScale,
    EyeDartMinDuration_ms, EyeDartMaxDuration_ms, EyeDartOuterEyeScaleIncrease, EyeDartUpMaxScale, EyeDartDownMinScale,
    NumParameters,
}

// fidelity: M5-028
/// <summary>The streamer's live-idle parameters (A34, SetDefaultParams 0x0057DB40..0x0057DCD6).</summary>
public sealed class LiveIdleParams
{
    private readonly float[] _p = new float[(int)LiveIdleParam.NumParameters];

    public float this[LiveIdleParam p]
    {
        get => _p[(int)p];
        set => SetParam(p, value);
    }

    /// <summary>SetParam: BlinkSpacingMaxTime_ms is clamped to 30000 (A34, 0x0057C064..0x0057C0C6).</summary>
    public void SetParam(LiveIdleParam p, float value)
    {
        if (p == LiveIdleParam.BlinkSpacingMaxTime_ms) value = MathF.Min(value, 30000f);
        _p[(int)p] = value;
    }

    /// <summary>
    /// SetDefaultParams (A34): blink 3000/4000; TimeBeforeWiggle 1000; body spacing 100/1000, duration 250/1500, speed 10,
    /// straight fraction 0.5; lift duration 50/500, spacing 250/2000, mean 35, variability 8; head duration 50/500,
    /// spacing 250/1000, variability 6; eye dart spacing 250/1000, distance 6, scale 0.92/1.08, duration 50/200,
    /// outer 0.1, up 1.1, down 0.85.
    /// </summary>
    public void SetDefaultParams()
    {
        SetParam(LiveIdleParam.BlinkSpacingMinTime_ms, 3000); SetParam(LiveIdleParam.BlinkSpacingMaxTime_ms, 4000);
        SetParam(LiveIdleParam.TimeBeforeWiggleMotions_ms, 1000);
        SetParam(LiveIdleParam.BodyMovementSpacingMin_ms, 100); SetParam(LiveIdleParam.BodyMovementSpacingMax_ms, 1000);
        SetParam(LiveIdleParam.BodyMovementDurationMin_ms, 250); SetParam(LiveIdleParam.BodyMovementDurationMax_ms, 1500);
        SetParam(LiveIdleParam.BodyMovementSpeedMinMax_mmps, 10); SetParam(LiveIdleParam.BodyMovementStraightFraction, 0.5f);
        SetParam(LiveIdleParam.LiftMovementDurationMin_ms, 50); SetParam(LiveIdleParam.LiftMovementDurationMax_ms, 500);
        SetParam(LiveIdleParam.LiftMovementSpacingMin_ms, 250); SetParam(LiveIdleParam.LiftMovementSpacingMax_ms, 2000);
        SetParam(LiveIdleParam.LiftHeightMean_mm, 35); SetParam(LiveIdleParam.LiftHeightVariability_mm, 8);
        SetParam(LiveIdleParam.HeadMovementDurationMin_ms, 50); SetParam(LiveIdleParam.HeadMovementDurationMax_ms, 500);
        SetParam(LiveIdleParam.HeadMovementSpacingMin_ms, 250); SetParam(LiveIdleParam.HeadMovementSpacingMax_ms, 1000);
        SetParam(LiveIdleParam.HeadAngleVariability_deg, 6);
        SetParam(LiveIdleParam.EyeDartSpacingMinTime_ms, 250); SetParam(LiveIdleParam.EyeDartSpacingMaxTime_ms, 1000);
        SetParam(LiveIdleParam.EyeDartMaxDistance_pix, 6);
        SetParam(LiveIdleParam.EyeDartMinScale, 0.92f); SetParam(LiveIdleParam.EyeDartMaxScale, 1.08f);
        SetParam(LiveIdleParam.EyeDartMinDuration_ms, 50); SetParam(LiveIdleParam.EyeDartMaxDuration_ms, 200);
        SetParam(LiveIdleParam.EyeDartOuterEyeScaleIncrease, 0.1f);
        SetParam(LiveIdleParam.EyeDartUpMaxScale, 1.1f); SetParam(LiveIdleParam.EyeDartDownMinScale, 0.85f);
    }
}

/// <summary>What ApplyLayersToAnim produced for one frame (Q1.6).</summary>
internal sealed class LayeredFrame
{
    public bool Audio;
    public byte[]? AudioSample;
    public bool Backpack;
    public ushort[]? BackpackLeds;
    public bool Face;
    public ProceduralFacePose? FaceOut;
}

// fidelity: M5-019, M5-028, M5-029, M5-031
/// <summary>
/// <c>TrackLayerComponent</c> (C11, gap2 Q1.1..Q1.6, gap1 G1..G3): the audio (+4), backpack (+8) and face (+0xC) layer
/// managers and the stored layer-base face (+0x10).
/// </summary>
internal sealed class TrackLayerComponent
{
    public TrackLayerComponent(EngineRandom rng, ScanLineState scanLines)
    {
        Face = new FaceLayerManager(rng, scanLines);
        Backpack = new BackpackLayerManager(rng);
    }

    public FaceLayerManager Face { get; }
    public BackpackLayerManager Backpack { get; }

    /// <summary>The audio layer manager (+4): nothing in M5 adds an audio layer, so its count is 0.</summary>
    public int AudioLayerCount => 0;

    /// <summary>TLC+0x10: the layer-base face, the last streamed face.</summary>
    public ProceduralFacePose LastFace { get; private set; } = new();

    /// <summary>
    /// The DesiredFaceDistortion degree (G1/G2: DesiredFaceDistortionComponent, an M7 interface). Unset gives 0, so no
    /// glitch is added.
    /// </summary>
    public Func<float>? DesiredFaceDistortion { get; set; }

    public Action<string>? Log
    {
        get => Face.Log;
        set => Face.Log = value;
    }

    /// <summary>
    /// TrackLayerComponent::Init with the neutral face (A2): the stored face Reset to ProceduralFace's reset data. Without
    /// a neutral face the reset data is not in the inventory; the default face is kept then.
    /// </summary>
    public void Init(ProceduralFacePose? resetData) => LastFace = resetData?.Clone() ?? new ProceduralFacePose();

    /// <summary>Q1.1: audio OR backpack OR face, in that order.</summary>
    public bool HaveLayersToSend => AudioLayerCount != 0 || Backpack.HaveLayersToSend || Face.HaveLayersToSend;

    /// <summary>
    /// G1 TrackLayerComponent::Update: a DesiredFaceDistortion above 1e-5 calls AddGlitch(degree).
    /// </summary>
    public void Update()
    {
        float degree = DesiredFaceDistortion?.Invoke() ?? 0f;
        if (degree > 1e-5f) AddGlitch(degree);
    }

    /// <summary>
    /// G3 AddGlitch(degree): the debug log; GenerateFaceDistortion then AddLayer("Glitch", track, 0) on the face manager;
    /// GenerateGlitchLights then AddLayer("Glitch", track, 0) on the backpack manager. The only caller is the M7 degree above.
    /// </summary>
    public void AddGlitch(float degree)
    {
        Log?.Invoke($"debug: TrackLayerComponent.AddGlitch: Degree {degree:F2}");
        Face.AddLayer("Glitch", FaceLayerManager.GenerateFaceDistortion(degree), 0);
        Backpack.AddLayer("Glitch", Backpack.GenerateGlitchLights(), 0);
    }

    /// <summary>
    /// GetCurrentKeyFrame(t) (gap4 B3, 0x0064F2B4..0x0064F2E4): when the current keyframe exists and trigger ≤ t, IsDone
    /// (counter &lt; duration → counter += 33 and false; otherwise counter = 0 and true) and, when done, MoveToNextKeyFrame;
    /// the keyframe is returned for this frame, its final frame included. Null otherwise.
    /// </summary>
    internal static BackpackFrame? GetCurrentKeyFrame(StreamTrack<BackpackFrame> track, long t)
    {
        var kf = track.Current;
        if (kf is null || kf.Trigger > t) return null;
        if (kf.IsDoneHelper()) track.MoveToNext();
        return kf;
    }

    /// <summary>
    /// ApplyLayersToAnim(anim, start, streamTime, storeFace) (C11, Q1.6), in the order audio, backpack, face:
    /// <list type="bullet">
    /// <item>audio = the sample is non-null, then 1 whenever the audio manager has a layer;</item>
    /// <item>backpack = the animation's backpack keyframe is due (current and start + trigger ≤ t; sent every frame
    /// while current, the track advancing when IsDoneHelper(duration) says so, C18) OR a backpack layer result;</item>
    /// <item>face: a copy of the stored face; the animation's face replaces it (GetFaceHelper, replace), and only a
    /// streaming animation (storeFace) writes that back as the new stored face; then every face layer is Combined onto
    /// it; face = the animation's result OR the layers'.</item>
    /// </list>
    /// </summary>
    public LayeredFrame ApplyLayersToAnim(StreamAnimation? anim, int start, int streamTime, byte[]? audioSample,
                                          bool storeFace, Action<StreamKeyframe>? fired = null, Action<FaceFrame>? firedFace = null)
    {
        var frame = new LayeredFrame { Audio = audioSample is not null, AudioSample = audioSample };
        if (AudioLayerCount != 0) frame.Audio = true;

        // backpack (gap4 B1): the animation's current keyframe for stream − start, copied whole, flag 1
        if (anim is not null && anim.Backpack.Current is { } bk && start + bk.Trigger <= streamTime)
        {
            var lights = (LightsKeyframe)bk.Source;
            frame.Backpack = true;
            frame.BackpackLeds = lights.EncodedLeds;
            fired?.Invoke(bk);
            if (bk.IsDoneHelper(lights.DurationTimeMs)) anim.Backpack.MoveToNext();
        }
        // backpack layers (B2): ApplyLayersToFrame with the lambda: each layer's current keyframe for layerStream −
        // layerStart overwrites the whole layered keyframe (all five LEDs) and returns true; ascending tags, so the highest
        // tag with a current keyframe wins
        if (Backpack.HaveLayersToSend)
        {
            ushort[]? layered = null;
            bool any = Backpack.ApplyLayersToFrame((track, ls, lt) =>
            {
                var kf = GetCurrentKeyFrame(track, (long)lt - ls);
                if (kf is null) return false;
                layered = (ushort[])kf.Leds.Clone();
                return true;
            }, Log);
            if (layered is not null) frame.BackpackLeds = layered;
            frame.Backpack |= any;
        }

        // face
        var face = LastFace.Clone();
        bool animFace = false;
        if (anim is not null)
        {
            animFace = GetFaceHelper(anim.ProcFace, start, streamTime, ref face, replace: true, Log, firedFace);
            if (animFace && storeFace) LastFace = face.Clone();
        }
        var composed = face;
        bool layerFace = Face.ApplyLayersToFrame((track, ls, lt) => GetFaceHelper(track, ls, lt, ref composed, replace: false, Log), Log);
        frame.Face = animFace || layerFace;
        frame.FaceOut = composed;
        return frame;
    }

    /// <summary>
    /// GetFaceHelper(track, start, t, face, replace) (C7, 0x0058CD80..0x0058CEE4): the current keyframe is due when
    /// start + trigger ≤ t. With no next keyframe its face is used and the track advances. When the next is due as well,
    /// a warning, the track advances and there is no face this frame. Otherwise the face is interpolated with
    /// frac = (t − start − trigger)/(next − trigger) capped at 1 (GetInterpolatedFace, Interpolate), and the track advances
    /// when the next is due by t + 33. The face replaces <paramref name="face"/> (an animation) or is Combined onto it (a
    /// layer). Returns whether a face was produced.
    /// </summary>
    internal static bool GetFaceHelper(StreamTrack<FaceFrame> track, int start, int t, ref ProceduralFacePose face,
                                       bool replace, Action<string>? log = null, Action<FaceFrame>? fired = null)
    {
        var kf = track.Current;
        if (kf is null || start + (long)kf.Trigger > t) return false;
        fired?.Invoke(kf);
        var next = track.Next;
        ProceduralFacePose use;
        if (next is null)
        {
            use = kf.Face;
            track.MoveToNext();
        }
        else if (start + (long)next.Trigger <= t)
        {
            log?.Invoke("warning: AnimationStreamer.GetFaceHelper: the next procedural face keyframe is also due");
            track.MoveToNext();
            return false;
        }
        else
        {
            float frac = MathF.Min((float)(t - start - (long)kf.Trigger) / (float)((long)next.Trigger - kf.Trigger), 1f);
            use = ProceduralFacePose.Interpolate(kf.Face, next.Face, frac);
            if (start + (long)next.Trigger <= t + 33) track.MoveToNext();
        }
        if (replace) face = use.Clone();
        else face.Combine(use);
        return true;
    }

    /// <summary>InitStream's RemoveKeepFaceAlive(99) and the other removal callers (L7).</summary>
    public void RemoveKeepFaceAlive(int dur) => Face.RemoveKeepFaceAlive(dur);

    /// <summary>FaceLayerManager::KeepFaceAlive (A33).</summary>
    public void KeepFaceAlive(LiveIdleParams p) => Face.KeepFaceAlive(p);

    /// <summary>Back to the constructed state (a removed robot): no layers, the default stored face, timers 0.</summary>
    public void Reset()
    {
        Face.Clear();
        Backpack.Clear();
        Face.BlinkTimerMs = Face.DartTimerMs = 0;
        Face.DartTag = 0;
        LastFace = new ProceduralFacePose();
    }
}
