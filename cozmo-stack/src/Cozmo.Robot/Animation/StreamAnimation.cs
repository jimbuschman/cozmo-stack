using System.Runtime.CompilerServices;

namespace Cozmo.Robot.Animation;

// fidelity: M5-025, M5-029
/// <summary>
/// A track of keyframes with the engine's iterator (<c>Animations::Track&lt;T&gt;</c>): a list, the current iterator
/// (+0x20 on a layer's track) and the live flag (+0x24). M5 inventory gap1 L4 (the face instantiation read, applied to
/// every type as the template it is):
/// <list type="bullet">
/// <item><see cref="MoveToNext"/>: a live track erases the current keyframe, any other moves the iterator on;</item>
/// <item><see cref="AddKeyFrameToBack"/> refuses (returns false) when the track already holds more than 1000 keyframes,
/// and puts the iterator on the first keyframe when the track's size becomes 1;</item>
/// <item><see cref="AddNewKeyFrameToBack"/> also refuses (BadTriggerTime) a keyframe whose trigger is not strictly after
/// the last one's.</item>
/// </list>
/// </summary>
internal sealed class StreamTrack<T> where T : class, IStreamKeyframe
{
    private readonly List<T> _frames = new();
    private int _cur;

    public bool IsLive { get; set; }
    public int Count => _frames.Count;
    public bool IsEmpty => _frames.Count == 0;
    public bool AtEnd => _cur >= _frames.Count;
    public T? Current => _cur < _frames.Count ? _frames[_cur] : null;
    public T? Next => _cur + 1 < _frames.Count ? _frames[_cur + 1] : null;
    public T? Last => _frames.Count > 0 ? _frames[^1] : null;
    public IReadOnlyList<T> Frames => _frames;

    /// <summary>Animation::Init: the iterator back to the first keyframe (A10).</summary>
    public void Init() => _cur = 0;

    /// <summary>MoveToNextKeyFrame (L4).</summary>
    public void MoveToNext()
    {
        if (_cur >= _frames.Count) return;
        if (IsLive) _frames.RemoveAt(_cur);
        else _cur++;
    }

    /// <summary>AddKeyFrameToBackHelper (L4): false (TooManyFrames) above 1000.</summary>
    public bool AddKeyFrameToBack(T kf)
    {
        if (_frames.Count > 1000) return false;
        _frames.Add(kf);
        if (_frames.Count == 1) _cur = 0;
        return true;
    }

    /// <summary>AddNewKeyFrameToBack (L4): false (BadTriggerTime) unless the trigger is after the last one's.</summary>
    public bool AddNewKeyFrameToBack(T kf)
    {
        if (_frames.Count > 0 && !(kf.Trigger > _frames[^1].Trigger)) return false;
        return AddKeyFrameToBack(kf);
    }

    /// <summary>
    /// A persistent layer that ran out (L5, Q1.4): the iterator on the last keyframe, every keyframe before it erased.
    /// </summary>
    public void PinLast()
    {
        if (_frames.Count == 0) return;
        _frames.RemoveRange(0, _frames.Count - 1);
        _cur = 0;
    }

    /// <summary>A copy of the track with its iterator on the first keyframe (AddLayer copies the track, L2).</summary>
    public StreamTrack<T> CopyFromBegin(Func<T, T> copy)
    {
        var t = new StreamTrack<T> { IsLive = IsLive };
        foreach (var f in _frames) t._frames.Add(copy(f));
        t._cur = 0;
        return t;
    }
}

/// <summary>A keyframe on a stream track: its trigger time (IKeyFrame +4).</summary>
internal interface IStreamKeyframe
{
    uint Trigger { get; }
}

/// <summary>
/// The per-keyframe state the engine keeps inside a keyframe object: the IsDoneHelper frame counter (+8, gap3 K1) and
/// the FaceAnimationKeyFrame frame index (+0x2C, C12). The keyframe's data is the loaded <see cref="Keyframe"/>.
/// </summary>
internal sealed class StreamKeyframe : IStreamKeyframe
{
    public StreamKeyframe(Keyframe source) => Source = source;

    public Keyframe Source { get; }
    public uint Trigger => Source.TriggerTimeMs;

    /// <summary>IKeyFrame +8: IsDoneHelper's counter, 0 from the ctor.</summary>
    public int Counter;

    /// <summary>FaceAnimationKeyFrame +0x2C: the next sprite frame.</summary>
    public int FaceAnimIndex;

    /// <summary>Whether <see cref="AnimationScheduler.KeyframeFired"/> has been raised for this keyframe in this play.</summary>
    public bool Fired;

    /// <summary>
    /// IsDoneHelper(d) (gap3 K1, 0x004F8BC8): counter &lt; d → counter += 33 and false; otherwise counter = 0 and true.
    /// </summary>
    public bool IsDoneHelper(uint duration)
    {
        if ((long)Counter < duration)
        {
            Counter += 33;
            return false;
        }
        Counter = 0;
        return true;
    }
}

/// <summary>
/// A ProceduralFaceKeyFrame on a face track: its trigger (IKeyFrame +4) and its face (+0xC). No duration field (gap3 K3).
/// </summary>
internal sealed class FaceFrame : IStreamKeyframe
{
    public FaceFrame(uint trigger, ProceduralFacePose face, Keyframe? source = null)
    {
        Trigger = trigger;
        Face = face;
        Source = source;
    }

    public uint Trigger { get; set; }
    public ProceduralFacePose Face { get; }
    public Keyframe? Source { get; }
    public bool Fired;

    public FaceFrame Copy() => new(Trigger, Face.Clone(), Source);

    /// <summary>
    /// The default ProceduralFaceKeyFrame (ctor 0x00578FE4; gap3 K1..K5): trigger 0, a default <c>ProceduralFace()</c>.
    /// </summary>
    public static FaceFrame Default(uint trigger = 0) => new(trigger, new ProceduralFacePose());
}

/// <summary>A BackpackLightsKeyFrame on a backpack layer track (gap2 Q2): trigger, duration and the five LED words.</summary>
internal sealed class BackpackFrame : IStreamKeyframe
{
    public BackpackFrame(uint trigger, uint duration, ushort[] leds)
    {
        Trigger = trigger;
        Duration = duration;
        Leds = leds;
    }

    public uint Trigger { get; }
    public uint Duration { get; }
    public ushort[] Leds { get; }

    /// <summary>IKeyFrame +8, the IsDoneHelper counter (0 from the ctor).</summary>
    public int Counter;

    /// <summary>BackpackLightsKeyFrame::IsDone (vslot 2, 0x004FB12E..0x004FB148): IsDoneHelper(duration).</summary>
    public bool IsDoneHelper()
    {
        if ((long)Counter < Duration) { Counter += 33; return false; }
        Counter = 0;
        return true;
    }

    public BackpackFrame Copy() => new(Trigger, Duration, (ushort[])Leds.Clone());
}

// fidelity: M5-025, M5-008, M5-024
/// <summary>
/// The engine's <c>Animation</c> as the streamer runs it (D2, 0x00577E04..0x00577ECA): eleven tracks, Head, Lift,
/// FaceAnim, ProcFace, Event, Backpack, Body, RecHeading, TurnTo, DeviceAudio and RobotAudio; <see cref="IsEmpty"/> is
/// "all empty", <see cref="HasFramesLeft"/> "any track iterator ≠ end" (the RobotAudio track included). It is the
/// container's object: one per clip, so its keyframes' state persists from one play to the next as the engine's does
/// (only Abort resets the face-animation index, A24).
/// </summary>
internal sealed class StreamAnimation
{
    private static readonly ConditionalWeakTable<AnimationClip, StreamAnimation> ForClip = new();

    public StreamAnimation(string name, bool isLive)
    {
        Name = name;
        IsLive = isLive;
        foreach (var t in KeyframeTracks) t.IsLive = isLive;
        ProcFace.IsLive = isLive;
    }

    public string Name { get; }
    /// <summary>+0xD: SetIsLive.</summary>
    public bool IsLive { get; }
    /// <summary>+0xC: set by Init.</summary>
    public bool Initialised { get; private set; }
    public AnimationClip? Clip { get; private init; }

    public readonly StreamTrack<StreamKeyframe> Head = new(), Lift = new(), FaceAnim = new(), Event = new(),
        Backpack = new(), Body = new(), RecHeading = new(), TurnTo = new(), DeviceAudio = new(), RobotAudio = new();
    public readonly StreamTrack<FaceFrame> ProcFace = new();

    private IEnumerable<StreamTrack<StreamKeyframe>> KeyframeTracks =>
        new[] { Head, Lift, FaceAnim, Event, Backpack, Body, RecHeading, TurnTo, DeviceAudio, RobotAudio };

    /// <summary>D2: IsEmpty is "all tracks empty".</summary>
    public bool IsEmpty => KeyframeTracks.All(t => t.IsEmpty) && ProcFace.IsEmpty;

    /// <summary>D2: HasFramesLeft is "any track iterator ≠ end", the RobotAudio track included.</summary>
    public bool HasFramesLeft => KeyframeTracks.Any(t => !t.AtEnd) || !ProcFace.AtEnd;

    /// <summary>
    /// GetLastKeyFrameEndTime_ms (gap4 E1, 0x00578D44..0x00578E3A): the unsigned max, over every non-empty track, of its
    /// last element's vslot 0, whatever the iterator: trigger + duration (a 32-bit add) for Head, Lift, Body, TurnTo and
    /// Backpack, the trigger alone for DeviceAudio, RobotAudio, FaceAnimation, ProceduralFace, Event and RecordHeading;
    /// 0 when every track is empty.
    /// </summary>
    public uint LastKeyFrameEndTime
    {
        get
        {
            uint max = 0;
            void WithDuration(StreamTrack<StreamKeyframe> t)
            {
                if (t.Last is { } k) max = Math.Max(max, unchecked(k.Trigger + k.Source.DurationMs));
            }
            void TriggerOnly(StreamTrack<StreamKeyframe> t)
            {
                if (t.Last is { } k) max = Math.Max(max, k.Trigger);
            }
            WithDuration(Head); WithDuration(Lift); WithDuration(Body); WithDuration(TurnTo); WithDuration(Backpack);
            TriggerOnly(DeviceAudio); TriggerOnly(RobotAudio); TriggerOnly(FaceAnim); TriggerOnly(Event); TriggerOnly(RecHeading);
            if (ProcFace.Last is { } f) max = Math.Max(max, f.Trigger);
            return max;
        }
    }

    /// <summary>Animation::Init (A10, 0x00577BC4..0x00577C16): every iterator to begin, +0xC set; always returns 0.</summary>
    public int Init()
    {
        foreach (var t in KeyframeTracks) t.Init();
        ProcFace.Init();
        foreach (var t in KeyframeTracks) foreach (var k in t.Frames) k.Fired = false;
        foreach (var f in ProcFace.Frames) f.Fired = false;
        Initialised = true;
        return 0;
    }

    /// <summary>The track a loaded keyframe belongs to (D1/D2).</summary>
    public void Add(Keyframe k)
    {
        switch (k)
        {
            case FaceKeyframe f: ProcFace.AddKeyFrameToBack(new FaceFrame(f.TriggerTimeMs, f.Pose, f)); break;
            case HeadKeyframe: Head.AddKeyFrameToBack(new StreamKeyframe(k)); break;
            case LiftKeyframe: Lift.AddKeyFrameToBack(new StreamKeyframe(k)); break;
            case FaceAnimationKeyframe: FaceAnim.AddKeyFrameToBack(new StreamKeyframe(k)); break;
            case EventKeyframe: Event.AddKeyFrameToBack(new StreamKeyframe(k)); break;
            case LightsKeyframe: Backpack.AddKeyFrameToBack(new StreamKeyframe(k)); break;
            case BodyKeyframe: Body.AddKeyFrameToBack(new StreamKeyframe(k)); break;
            case RecordHeadingKeyframe: RecHeading.AddKeyFrameToBack(new StreamKeyframe(k)); break;
            case TurnToRecordedHeadingKeyframe: TurnTo.AddKeyFrameToBack(new StreamKeyframe(k)); break;
            case AudioKeyframe: RobotAudio.AddKeyFrameToBack(new StreamKeyframe(k)); break;
        }
    }

    /// <summary>
    /// The load's add (J1.2, gap1 C2): AddNewKeyFrameToBack on the keyframe's track (BadTriggerTime, TooManyFrames). False
    /// when refused.
    /// </summary>
    public bool AddNew(Keyframe k) => k switch
    {
        FaceKeyframe f => ProcFace.AddNewKeyFrameToBack(new FaceFrame(f.TriggerTimeMs, f.Pose, f)),
        HeadKeyframe => Head.AddNewKeyFrameToBack(new StreamKeyframe(k)),
        LiftKeyframe => Lift.AddNewKeyFrameToBack(new StreamKeyframe(k)),
        FaceAnimationKeyframe => FaceAnim.AddNewKeyFrameToBack(new StreamKeyframe(k)),
        EventKeyframe => Event.AddNewKeyFrameToBack(new StreamKeyframe(k)),
        LightsKeyframe => Backpack.AddNewKeyFrameToBack(new StreamKeyframe(k)),
        BodyKeyframe => Body.AddNewKeyFrameToBack(new StreamKeyframe(k)),
        RecordHeadingKeyframe => RecHeading.AddNewKeyFrameToBack(new StreamKeyframe(k)),
        TurnToRecordedHeadingKeyframe => TurnTo.AddNewKeyFrameToBack(new StreamKeyframe(k)),
        AudioKeyframe => RobotAudio.AddNewKeyFrameToBack(new StreamKeyframe(k)),
        _ => false,
    };

    /// <summary>
    /// Animation::AddKeyFrameToBack&lt;T&gt; (gap4 L7): the keyframe's track's AddKeyFrameToBackHelper, which refuses only above
    /// 1000 keyframes and does no trigger-order check. False when refused.
    /// </summary>
    public bool AddLive(Keyframe k)
    {
        switch (k)
        {
            case FaceKeyframe f: return ProcFace.AddKeyFrameToBack(new FaceFrame(f.TriggerTimeMs, f.Pose, f));
            case HeadKeyframe: return Head.AddKeyFrameToBack(new StreamKeyframe(k));
            case LiftKeyframe: return Lift.AddKeyFrameToBack(new StreamKeyframe(k));
            case FaceAnimationKeyframe: return FaceAnim.AddKeyFrameToBack(new StreamKeyframe(k));
            case EventKeyframe: return Event.AddKeyFrameToBack(new StreamKeyframe(k));
            case LightsKeyframe: return Backpack.AddKeyFrameToBack(new StreamKeyframe(k));
            case BodyKeyframe: return Body.AddKeyFrameToBack(new StreamKeyframe(k));
            case RecordHeadingKeyframe: return RecHeading.AddKeyFrameToBack(new StreamKeyframe(k));
            case TurnToRecordedHeadingKeyframe: return TurnTo.AddKeyFrameToBack(new StreamKeyframe(k));
            case AudioKeyframe: return RobotAudio.AddKeyFrameToBack(new StreamKeyframe(k));
            default: return false;
        }
    }

    /// <summary>The container's Animation for a clip, built once from its keyframes in their loaded order.</summary>
    public static StreamAnimation Of(AnimationClip clip) => ForClip.GetValue(clip, c =>
    {
        var a = new StreamAnimation(c.Name, isLive: false) { Clip = c };
        foreach (var k in c.Keyframes) a.Add(k);
        return a;
    });

    /// <summary>A fresh live Animation (A1: named EnumToString(ProceduralLive) and SetIsLive(true)).</summary>
    public static StreamAnimation Live() => new("ProceduralLive", isLive: true);

    /// <summary>The loaded keyframe count across tracks (for KeyframesFired checks).</summary>
    public int KeyframeCount => KeyframeTracks.Sum(t => t.Count) + ProcFace.Count;
}
