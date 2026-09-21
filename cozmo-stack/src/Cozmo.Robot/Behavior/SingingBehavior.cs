using System.Text.Json;
using System.Text.RegularExpressions;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Animation.Wwise;

namespace Cozmo.Robot.Behavior;

/// <summary>
/// Cozmo sings one of the 39 shipped songs: the engine's <c>BehaviorSinging</c>, reconstructed from its
/// disassembly (addresses in WWISE_MUSIC.md).
///
/// What the engine does, and this does:
///
/// * The constructor (0x005EE8DC) turns the config's <c>audioSwitchGroup</c> and <c>audioSwitch</c> into
///   ids and picks the tempo animation trigger from the group: <c>Singing_80bpm</c>, <c>_100bpm</c> or
///   <c>_120bpm</c>. A group it does not recognise falls back to the 80 bpm group with switch 0, so the
///   default song plays rather than nothing.
/// * <c>InitInternal</c> (0x005EEB30) posts the switch state <b>first</b>
///   (<c>RobotAudioClient::PostRobotSwitchState</c>), locks reactions out
///   (<c>SmartDisableReactionsWithLock</c>), then runs one sequential compound action of three
///   <c>TriggerAnimationAction</c>s: <c>Singing_GetIn</c>, the tempo trigger, <c>Singing_GetOut</c>. The
///   tempo animation's own audio keyframe raises <c>Play__Robot_VO__Cozmo_Singing_*bpm</c>, and the switch
///   decides which song that event plays.
/// * <c>UpdateInternal</c> (0x005EF0C8) smooths the largest cube shake into the <c>Cozmo_Singing_Vibrato</c>
///   game parameter: <c>new = 0.5 * old + 0.5 * clamp(shake / 3000, 0, 1)</c>, posted every tick;
///   <c>StopInternal</c> posts 0. That formula is <see cref="NextVibrato"/>. The engine's shake comes from a
///   streamed cube accelerometer this stack does not receive (M4 has movement reports, not the stream), so
///   <see cref="ShakeInput"/> is left for a caller and stays 0 otherwise. The parameter is posted to the
///   audio source either way, and the banks act on it: it drives the depth of the vibrato LFO bound to the
///   singing sampler's pitch, so at 0 there is no vibrato and at 1 the pitch swings by the binding's full
///   580 cents. What is missing is the shake, not the vibrato (fidelity manifest M9-017).
///
/// Where the engine's compound action fails a step (a trigger with no clip), this moves to the next step
/// and says so, rather than inventing a substitute animation.
/// </summary>
public sealed class SingingBehavior : IBehavior
{
    private readonly object _gate = new();
    private CozmoAnimations? _animations;
    private long _generation;
    private bool _owns;
    private volatile bool _finished = true;
    private volatile bool _stopped;
    private BehaviorContext? _context;
    private readonly List<string> _steps = new();

    public SingingBehavior(string id, string switchGroup, string switchName, double score = 1.0)
    {
        Id = id;
        SwitchGroupName = switchGroup;
        SwitchName = switchName;
        Score = score;
        var (g, s) = EffectiveSwitch(WwiseHash.Of(switchGroup), WwiseHash.Of(switchName));
        SwitchGroupId = g;
        SwitchId = s;
        TempoTrigger = TempoTriggerFor(g);
    }

    public string Id { get; }
    public string Class => "Singing";
    public string SwitchGroupName { get; }
    public string SwitchName { get; }
    /// <summary>The switch group and switch actually posted (after the engine's unknown-group fallback).</summary>
    public uint SwitchGroupId { get; }
    public uint SwitchId { get; }
    /// <summary>The middle animation of the three: the tempo the song is sung at.</summary>
    public AnimationTrigger TempoTrigger { get; }
    public double Score { get; set; }

    /// <summary>The smoothed vibrato value, as the engine would post it. See <see cref="NextVibrato"/>.</summary>
    public float Vibrato { get; private set; }
    /// <summary>
    /// The largest cube shake this tick, in the engine's units: the squared magnitude of the high-pass
    /// filtered acceleration, which is what <c>ShakeListener</c> hands its callback. Fed by the listeners
    /// this behaviour registers on every connected cube; a caller can still set it, which is how the
    /// offline tests drive it.
    /// </summary>
    public float ShakeInput { get; set; }

    private readonly Dictionary<uint, (CubeShakeListener Listener, float Value)> _shake = new();

    /// <summary>
    /// One <c>ShakeListener</c> per connected cube, with the constants
    /// <c>BehaviorSinging::InitInternal</c> passes (0x005EECF4..0x005EED08): filter coefficient 0.5, stop
    /// threshold 2.5, start threshold 3.9. Each cube keeps its own last value and
    /// <see cref="ShakeInput"/> is the largest of them, which is the "largest cube shake" UpdateInternal
    /// takes the maximum of.
    /// </summary>
    private void StartListeningForShake(BehaviorContext context)
    {
        var robot = context.Robot;
        foreach (var cube in robot.Cubes.ConnectedCubes)
        {
            if (cube.ObjectId is not { } id) continue;
            var listener = new CubeShakeListener(
                CubeShakeListener.SingingFilterCoefficient,
                CubeShakeListener.SingingLowThreshold,
                CubeShakeListener.SingingHighThreshold,
                magnitudeSquared =>
                {
                    lock (_gate)
                    {
                        if (_shake.TryGetValue(id, out var entry)) _shake[id] = (entry.Listener, magnitudeSquared);
                        ShakeInput = _shake.Values.Max(v => v.Value);
                    }
                });
            lock (_gate) _shake[id] = (listener, 0f);
            robot.CubeAccel.AddListener(id, listener);
            Trace?.Invoke($"listening for shake on cube {id}");
        }
    }

    /// <summary>Takes the listeners off again, which turns each cube's stream back off. StopInternal does the same.</summary>
    private void StopListeningForShake()
    {
        if (_context is not { } context) return;
        KeyValuePair<uint, (CubeShakeListener Listener, float Value)>[] entries;
        lock (_gate) { entries = _shake.ToArray(); _shake.Clear(); }
        foreach (var (id, entry) in entries) context.Robot.CubeAccel.RemoveListener(id, entry.Listener);
        ShakeInput = 0;
    }

    /// <summary>The clips played so far, in order, for tracing and tests.</summary>
    public IReadOnlyList<string> Steps { get { lock (_gate) return _steps.ToList(); } }
    /// <summary>What happened at each step, in words.</summary>
    public event Action<string>? Trace;

    public const uint Group80 = 0xC8A59578, Group100 = 0xE017E775, Group120 = 0xB215BB17;

    /// <summary>The engine's group-to-trigger table (constructor, 0x005EE9A0..0x005EE9FE): 0x1FF, 0x200, 0x201.</summary>
    public static AnimationTrigger TempoTriggerFor(uint groupId) => groupId switch
    {
        Group100 => AnimationTrigger.Singing_100bpm,
        Group120 => AnimationTrigger.Singing_120bpm,
        _ => AnimationTrigger.Singing_80bpm,
    };

    /// <summary>The engine's fallback: an unrecognised group becomes the 80 bpm group with switch 0 (the tree's default song).</summary>
    public static (uint Group, uint Switch) EffectiveSwitch(uint groupId, uint switchId) =>
        groupId is Group80 or Group100 or Group120 ? (groupId, switchId) : (Group80, 0u);

    /// <summary>The render started when the switch was posted, or null when no Wwise source is attached.</summary>
    public Task? Prewarm { get; private set; }

    /// <summary>The audio event the tempo animation's keyframe raises for a group.</summary>
    public static string TempoEventName(uint groupId) => groupId switch
    {
        Group100 => "Play__Robot_VO__Cozmo_Singing_100bpm",
        Group120 => "Play__Robot_VO__Cozmo_Singing_120bpm",
        _ => "Play__Robot_VO__Cozmo_Singing_80bpm",
    };

    /// <summary>The three triggers, in the order the engine's compound action runs them.</summary>
    public static IReadOnlyList<AnimationTrigger> Sequence(uint groupId) =>
        new[] { AnimationTrigger.Singing_GetIn, TempoTriggerFor(groupId), AnimationTrigger.Singing_GetOut };

    /// <summary><c>BehaviorSinging::UpdateInternal</c> at 0x005EF0C8: half the previous value plus half the clamped shake.</summary>
    public static float NextVibrato(float previous, float maxShake) =>
        0.5f * previous + 0.5f * Math.Clamp(maxShake / 3000f, 0f, 1f);

    public bool IsRunnable(BehaviorContext context) => context.Robot.Animations.Library is not null;

    public double EvaluateScore(BehaviorContext context) => IsRunnable(context) ? Score : 0;

    public Task StartAsync(BehaviorContext context, BehaviorScope scope, CancellationToken cancel)
    {
        _finished = false;
        _stopped = false;
        _context = context;
        lock (_gate) _steps.Clear();

        // 1. the switch, before anything plays
        if (context.Robot.Animations.AudioSource is IAudioSwitchStates sink)
        {
            sink.SetSwitch(SwitchGroupId, SwitchId);
            Trace?.Invoke($"switch {SwitchGroupName} = {SwitchName} posted ({SwitchGroupId} = {SwitchId})");
            // The song is rendered whole before it streams (a 462 s sequence takes seconds). Rendering it on
            // the scheduler's thread when the tempo clip's audio keyframe fires would stall the timeline, so
            // the render starts here, on a worker, while the get-in animation plays. LOCAL: the engine hands
            // the event to Wwise, which streams; this stack renders ahead and says so.
            if (sink is WwiseAudioSource wwise && wwise.Library.IdOf(TempoEventName(SwitchGroupId)) is { } ev)
            {
                Prewarm = wwise.Prewarm(ev);
                Trace?.Invoke($"prewarming {TempoEventName(SwitchGroupId)} on a worker");
            }
        }
        else Trace?.Invoke("no switch-capable audio source is attached; the song cannot be selected and the tempo animation's audio event will not resolve");

        // 2. a shake listener per connected cube, as InitInternal does between the switch and the
        //    reaction lock: ShakeListener(0.5, 2.5, 3.9) on each, and adding the first turns that cube's
        //    accelerometer stream on.
        StartListeningForShake(context);

        // 3. reactions held off for the duration
        scope.DisableReactions();

        // 4. get-in, tempo, get-out
        StartStep(0);
        return Task.CompletedTask;
    }

    /// <summary>The index of the tempo animation in <see cref="Sequence"/> (get-in, tempo, get-out).</summary>
    private const int TempoStepIndex = 1;

    private void StartStep(int index)
    {
        if (_stopped) return;
        var context = _context!;
        var sequence = Sequence(SwitchGroupId);
        if (index >= sequence.Count) { _finished = true; Trace?.Invoke("finished"); return; }

        // The tempo animation is the one whose audio keyframe asks for the song. Waiting here, on the
        // behaviour's own continuation, is the difference between a late get-in and a stalled animation
        // scheduler: the keyframe must find the render already in the cache, because the scheduler thread
        // will not render or block for it.
        if (index == TempoStepIndex && Prewarm is { IsCompleted: false } pending)
        {
            Trace?.Invoke("waiting for the song render before the tempo animation");
            pending.ContinueWith(_ =>
            {
                if (_stopped) return;
                Trace?.Invoke("song render ready");
                StartStep(index);
            }, TaskScheduler.Default);
            return;
        }

        var lib = context.Robot.Animations.Library;
        if (lib is null) { _finished = true; return; }
        var trigger = sequence[index];
        var resolved = context.Triggers.Resolve(trigger, lib, context.Random);
        if (!resolved.Resolved)
        {
            Trace?.Invoke($"step {index + 1}: {trigger} resolves to no clip in these assets; skipped");
            StartStep(index + 1);
            return;
        }
        var ticket = context.Robot.Animations.PlayTracked(resolved.Selected!);
        if (ticket is null)
        {
            Trace?.Invoke($"step {index + 1}: {trigger} -> {resolved.Selected} was refused by the scheduler");
            _finished = true;
            return;
        }
        lock (_gate)
        {
            _steps.Add(resolved.Selected!);
            _animations = context.Robot.Animations;
            _generation = ticket.Generation;
            _owns = true;
        }
        Trace?.Invoke($"step {index + 1}: {trigger} -> {resolved.Selected}");
        ticket.Completion.ContinueWith(t =>
        {
            lock (_gate) _owns = false;
            if (t.Status == TaskStatus.RanToCompletion && t.Result == AnimationEndReason.Completed && !_stopped)
                StartStep(index + 1);
            else
            {
                Trace?.Invoke($"step {index + 1} ended {(t.Status == TaskStatus.RanToCompletion ? t.Result.ToString() : t.Status.ToString())}; the sequence stops here");
                _finished = true;
            }
        }, TaskScheduler.Default);
    }

    public bool Update(BehaviorContext context, double nowMs)
    {
        Vibrato = NextVibrato(Vibrato, ShakeInput);
        PostVibrato(context);
        return !_finished;
    }

    /// <summary>
    /// Posts the vibrato where the banks can act on it, which is what
    /// <c>RobotAudioClient::PostRobotParameter(Cozmo_Singing_Vibrato, value)</c> does every tick
    /// (<c>BehaviorSinging::UpdateInternal</c> 0x005EF0C8). In the banks the parameter drives the depth of
    /// <c>cozmo_singing_vibrato_lfo</c>, which is bound to Pitch on the singing sampler; at 0 the depth is
    /// 0 and the LFO does nothing, which is the state a Cozmo nobody is shaking sings in.
    /// </summary>
    private void PostVibrato(BehaviorContext context)
    {
        if (context.Robot.Animations.AudioSource is IAudioSwitchStates sink)
            sink.SetParameter(VibratoParameter, Vibrato);
    }

    /// <summary>FNV-1 of <c>Cozmo_Singing_Vibrato</c>; the id the engine posts (0x005EF0C8).</summary>
    public const uint VibratoParameter = 0xC20F49DF;

    public void Stop(BehaviorStopReason reason)
    {
        _stopped = true;
        _finished = true;
        StopListeningForShake();                        // StopInternal removes the cube listeners
        Vibrato = 0;                                    // StopInternal posts the parameter back to 0
        if (_context is { } c) PostVibrato(c);
        PlayAnimBehavior.StopOwnAnimation(ref _animations, ref _generation, ref _owns, _gate);
    }

    /// <summary>
    /// The 39 shipped Singing behaviours, built from their configs under
    /// <c>config/engine/behaviorSystem/behaviors/freeplay/singing/</c>. Empty when the directory is absent.
    /// </summary>
    public static IReadOnlyList<SingingBehavior> LoadShipped(string obbRoot)
    {
        var dir = Path.Combine(obbRoot, "assets", "cozmo_resources", "config", "engine", "behaviorSystem", "behaviors", "freeplay", "singing");
        var list = new List<SingingBehavior>();
        if (!Directory.Exists(dir)) return list;
        foreach (var f in Directory.EnumerateFiles(dir, "*.json").OrderBy(x => x, StringComparer.Ordinal))
        {
            var text = Regex.Replace(File.ReadAllText(f), "//[^\n\r]*", "");
            try
            {
                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;
                if (root.GetProperty("behaviorClass").GetString() != "Singing") continue;
                list.Add(new SingingBehavior(root.GetProperty("behaviorID").GetString()!,
                                             root.GetProperty("audioSwitchGroup").GetString()!,
                                             root.GetProperty("audioSwitch").GetString()!));
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException) { }
        }
        return list;
    }
}
