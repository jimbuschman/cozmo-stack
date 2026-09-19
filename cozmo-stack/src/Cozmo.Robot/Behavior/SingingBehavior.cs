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
///   <see cref="ShakeInput"/> is left for a caller and stays 0 otherwise; and the vibrato LFO the parameter
///   drives in the banks is not rendered yet, so the value has no audible effect. Both are stated in
///   WWISE_MUSIC.md rather than hidden.
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
    /// <summary>The largest cube shake this tick, in the engine's units; a caller feeds it, nothing here measures it.</summary>
    public float ShakeInput { get; set; }

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
        }
        else Trace?.Invoke("no switch-capable audio source is attached; the song cannot be selected and the tempo animation's audio event will not resolve");

        // 2. reactions held off for the duration
        scope.DisableReactions();

        // 3. get-in, tempo, get-out
        StartStep(0);
        return Task.CompletedTask;
    }

    private void StartStep(int index)
    {
        if (_stopped) return;
        var context = _context!;
        var sequence = Sequence(SwitchGroupId);
        if (index >= sequence.Count) { _finished = true; Trace?.Invoke("finished"); return; }

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
        return !_finished;
    }

    public void Stop(BehaviorStopReason reason)
    {
        _stopped = true;
        _finished = true;
        Vibrato = 0;                                    // StopInternal posts the parameter back to 0
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
