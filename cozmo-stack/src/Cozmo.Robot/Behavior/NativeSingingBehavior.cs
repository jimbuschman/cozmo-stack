using System.Globalization;
using System.Text.Json;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Animation.Wwise;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Behavior;

// fidelity: M9-001, M9-002, M9-003, M9-017, M9-028
internal sealed class NativeSingingBehavior : SteppedBehavior, IDisposable
{
    private const uint VibratoParameter = 0xC20F49DF;
    private readonly EngineRobot _robot;
    private readonly CozmoRobot _facade;
    private readonly uint _switchGroup, _songSwitch;
    private string? _needsAction;
    private readonly List<CubeShake> _shakes = new();
    private long _version;
    private bool _retired;
    private float _vibrato;
    private uint _shakeStartedMs;

    private sealed class CubeShake(uint objectId, uint activeId)
    {
        internal readonly uint ObjectId = objectId;
        internal readonly uint ActiveId = activeId;
        internal CubeShakeListener Listener = null!;
        internal float Mean;
        internal int Count = 1;
    }

    internal NativeSingingBehavior(BehaviorFactoryContext source, JsonElement config)
        : base(BehaviorConfigLoader.ExtractIdFromConfig(config).ToString(), "Singing")
    {
        _robot = source.NativeRobot!;
        _facade = source.Manipulation!.Robot;
        string group = Read("audioSwitchGroup");
        string song = Read("audioSwitch");
        // 0x005EE94E..0x005EE9FE: the shipped group and switch names map to
        // the IDs used by the authored tempo animation.
        _switchGroup = group switch
        {
            "Cozmo_Sings_100Bpm" => SingingBehavior.Group100,
            "Cozmo_Sings_120Bpm" => SingingBehavior.Group120,
            _ => SingingBehavior.Group80
        };
        // The three native FromString tables (0x006F9B5C, 0x006F8BE8,
        // 0x006F93E4) use case-sensitive keys and return the first enum on a miss.
        string[] entries = group switch
        {
            "Cozmo_Sings_80Bpm" => Songs80,
            "Cozmo_Sings_100Bpm" => Songs100,
            "Cozmo_Sings_120Bpm" => Songs120,
            _ => []
        };
        const string prefix = "Cozmo_Sings_";
        if (entries.Length != 0)
        {
            if (song.StartsWith(prefix, StringComparison.Ordinal) &&
                entries.Contains(song[prefix.Length..], StringComparer.Ordinal))
                _songSwitch = WwiseHash.Of(song);
            else
            {
                BehaviorClassNames.WriteCerrError(song, group);
                _songSwitch = WwiseHash.Of(prefix + entries[0]);
            }
        }
        string need = Read("needsActionID");
        if (Enum.TryParse<NeedsActionId>(need, false, out var action) && action != 0 && Enum.GetName(action) == need)
            _needsAction = need;

        string Read(string property) => config.TryGetProperty(property, out var value) &&
            value.ValueKind == JsonValueKind.String ? value.GetString()! : string.Empty;
    }

    private static readonly string[] Songs80 =
    [
        "Aba_Daba", "Danny_Boy", "Farmer_In_The_Dell", "Frere_Jacques", "Habanera",
        "Mary_Had_A_Little_Lamb", "Muffin_Man", "Mulberry_Bush", "Pachebel_Canon",
        "Take_Me_Out_To_The_Ballgame", "Vivaldi_Spring", "Water_Music", "William_Tell", "Yankee_Doodle"
    ];
    private static readonly string[] Songs100 =
    [
        "Beautiful_Dreamer", "Beethovens_5Th", "Bingo", "Buffalo_Gals", "Camptown_Races",
        "Can_Can_1", "Can_Can_2", "Farmer_In_The_Dell", "Itsy_Bitsy_Spider", "London_Bridge",
        "Mountain_King", "Muss_I_Denn", "Pop_Goes_The_Weasel", "Row_Your_Boat", "Ta_Ra_Ra_Boom",
        "Tisket_Tasket", "Yellow_Rose"
    ];
    private static readonly string[] Songs120 =
    [
        "Cozmambo", "Entry_Of_The_Gladiators", "Hello_My_Baby", "Hush_Little_Baby", "La_Paloma",
        "Moonlight_Bay", "Ode_To_Joy", "Sakura", "Silvery_Moon", "Toccata", "Turkey_In_The_Straw",
        "Twinkle_Twinkle", "Wild_About_Harry"
    ];

    private bool OwnerCurrent => !_retired && !_robot.Lifetime.Destroying && ReferenceEquals(_robot.Engine.Robot, _robot);
    protected override IDisposable EnterHostExecutionOwner(BehaviorContext? context) => _robot.Engine.EnterSynchronousOwner();
    protected override void HostRunStarting() => ++_version;
    protected override long CaptureHostExecutionVersion() => _version;
    protected override bool HostExecutionVersionCurrent(long version) => OwnerCurrent && _version == version;
    protected override bool KeepsRunningWithoutAction => HasCurrentAction;
    // 0x0102A350..0x0102A358: the native readiness overrides all reject.
    protected override bool RunnableGate20(BehaviorContext context) => false;
    protected override bool RunnableGate24(BehaviorContext context) => false;
    protected override bool RunnableGate28(BehaviorContext context) => false;

    public override bool IsRunnable(BehaviorContext context) =>
        OwnerCurrent && IsRunnableBase(context) && IsRunnableInternal(context);

    protected override bool IsRunnableInternal(BehaviorContext context)
    {
        // 0x005EEB12..0x005EEB2C: zero allows any otherwise runnable song.
        uint forced = unchecked((uint)_robot.Engine.Needs.State.ForceNextSong);
        return forced == 0 || forced == NativeBaseInputs!.RequiredUnlock;
    }

    protected override void OnStart()
    {
        long version = _version;
        bool Current() => HostExecutionVersionCurrent(version);
        _robot.SendBehaviorAudio(new(Id, 0x14, _switchGroup, _songSwitch, 1)); // 0x005EEB46..0x005EEB4E
        if (!Current() || !Scope.SmartDisableReactionsWithLockGuarded(Id, ReactionLockTables.Singing, Current)) return;

        // 0x005EEB88..0x005EEC5C: query connected family 2, including cubes without a located pose.
        var filter = new BlockWorldFilter();
        filter.AllowedFamilies.Add(ObjectFamily.LightCube);
        var cubes = new List<ObservableObject>();
        _facade.LocalizationWorld.FindConnectedObjectHelper(filter, cubes.Add, false);
        foreach (var cube in cubes)
        {
            if (cube.ActiveId < 0) continue;
            var shake = new CubeShake(cube.ObjectId, (uint)cube.ActiveId);
            shake.Listener = new CubeShakeListener(.5f, 2.5f, 3.9f, sample =>
            {
                if (Current() && _facade.LocalizationWorld.ConnectedObjectIdForActiveId(shake.ActiveId) == shake.ObjectId)
                    SingingBehavior.AccumulateShake(ref shake.Mean, ref shake.Count, sample);
            }); // 0x005EECCE..0x005EED08
            _shakes.Add(shake);
            _facade.CubeAccel.AddListener(shake.ActiveId, shake.Listener); // 0x005EED56, radio ID at 0x00635556
            if (!Current()) return;
        }

        // 0x005EEDA8..0x005EEE8C: ordinary TriggerAnimationAction, one loop,
        // interrupt true, tracks 0, 60 seconds, strict cooldown false.
        var sequence = new CompoundActionSequential(() => _robot.Engine.Timer.SecondsF);
        sequence.SeedNativeEmptyCompletion();
        foreach (var trigger in SingingBehavior.Sequence(_switchGroup))
        {
            var child = new TriggerAnimationAction(_facade, new(trigger, 1, true, 0, 60f, false));
            child.SetScopedOwner(_robot);
            sequence.AddAction(child, false, false);
            if (!Current()) { sequence.RetireNeverAdmittedTree(); return; }
        }
        StartActing(sequence, report => OnSongActionEnded(report.Result, Current), Current);
    }

    private void OnSongActionEnded(uint result, Func<bool> current)
    {
        // 0x005EF7B6..0x005EF7DC: only success reports objective + needs action.
        if (result == 0)
        {
            _robot.Engine.ReportBehaviorObjective(0x27, "Singing", Id);
            if (!current()) return;
            NeedActionCompleted(_needsAction ?? "CozmoSings");
        }
        if (!current()) return;
        // 0x005EF7DC -> 0x004B5424 -> 0x005EF070: clear +0x68 on all
        // configured Singing instances after either compound result.
        foreach (var song in _robot.NativeBehaviorRuntime!.Bound.Values.OfType<NativeSingingBehavior>())
            song._needsAction = null;
    }

    protected override void OnUpdate()
    {
        long version = _version;
        float maximum = 0;
        foreach (var shake in _shakes)
        {
            if (shake.Mean > maximum) maximum = shake.Mean;
            shake.Mean = 0;
            shake.Count = 1;
        }
        _vibrato = SingingBehavior.NextVibrato(_vibrato, maximum); // 0x005EF0CC..0x005EF17E
        PostVibrato(_vibrato);
        if (!HostExecutionVersionCurrent(version)) return;
        uint now = unchecked((uint)_robot.Engine.Timer.TimeStampMs);
        if (_vibrato > .1f)
        {
            if (_shakeStartedMs == 0) _shakeStartedMs = now;
        }
        else if (_shakeStartedMs != 0)
        {
            uint duration = unchecked(now - _shakeStartedMs);
            _shakeStartedMs = 0;
            if (duration > 500)
                _robot.Engine.EmitSourceLog(EngineLogLevel.Event, null, "robot.song_shake_duration_ms",
                    duration.ToString(CultureInfo.InvariantCulture), new Dictionary<string, string> { ["$data"] = Id });
        } // 0x005EF190..0x005EF240
    }

    private void PostVibrato(float value) =>
        _robot.SendBehaviorAudio(new(Id, 0x18, VibratoParameter, BitConverter.SingleToUInt32Bits(value), 0));

    protected override void OnStop(BehaviorStopReason reason)
    {
        ++_version;
        var prior = _shakes.ToArray();
        _shakes.Clear();
        PostVibrato(0); // 0x005EF2BA..0x005EF2CA, followed by listener removal.
        foreach (var shake in prior)
            _facade.CubeAccel.RemoveSingingListener(shake.ActiveId, shake.Listener,
                OwnerCurrent && _facade.LocalizationWorld.ConnectedObjectIdForActiveId(shake.ActiveId) == shake.ObjectId);
    }

    public void Dispose()
    {
        if (_retired) return;
        _retired = true;
        ++_version;
        foreach (var shake in _shakes)
            _facade.CubeAccel.RemoveSingingListener(shake.ActiveId, shake.Listener, false);
        _shakes.Clear();
        RetireActionCompletionOwner();
    }
}
