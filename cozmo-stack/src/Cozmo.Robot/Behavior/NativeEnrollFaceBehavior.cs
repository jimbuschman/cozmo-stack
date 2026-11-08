using System.Text.Json;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Behavior;

// Retained BehaviorEnrollFace owner, 0x5FCAD4..0x600614. Admission remains
// owned by the configured Meet chooser; 87 only stages this instance.
internal sealed class NativeEnrollFaceBehavior : SteppedBehavior, IDisposable
{
    private readonly ManipulationSystem _m;
    private readonly EngineRobot _owner;
    private readonly NativeEnrollmentTarget _target = new();
    private readonly RetainedSubscription? _request, _cancel, _changed;
    private readonly uint _maxFaces;
    private readonly float _recent, _tooManyTimeout;
    private string _stagedName = "", _name = "";
    private int _stagedObserved, _stagedSave, _save;
    private bool _stagedWrite, _stagedSay, _stagedMusic, _write, _say, _music, _initialSelected;
    private float _started, _window, _reverse, _previousPan;
    private uint _albumResult = EngineActionResult.NotStarted, _enrollmentResult = EngineActionResult.NotStarted;
    private Task? _musicRequest;
    private CannedSpeechOwner? _musicOwner;
    private long _generation;
    private int _retired;
    internal int Phase { get; private set; } = 1;
    internal int Selected => _target.Selected;
    internal string StagedName => _stagedName;

    internal NativeEnrollFaceBehavior(BehaviorFactoryContext source, JsonElement config) : base("EnrollFace", "EnrollFace")
    {
        _m = source.Manipulation!; _owner = source.NativeRobot!;
        var parameters = config;
        _maxFaces = parameters.GetProperty("maxFacesVisible").GetUInt32();
        _recent = parameters.GetProperty("tooManyFacesRecentTime_sec").GetSingle();
        _tooManyTimeout = parameters.GetProperty("tooManyFacesTimeout_sec").GetSingle();
        if (_owner.HasExternalInterface)
        {
            _request = _owner.Engine.AnimationMessages.SubscribeMessage(0x87, false, value =>
            {
                if (value is not NativeEnrollmentRequest request || !OwnerCurrent || EngineRunning || request.Name.Length == 0) return;
                _stagedName = request.Name; _stagedObserved = request.ObservedId; _stagedSave = request.SaveId;
                _stagedWrite = request.Save; _stagedSay = request.SayName; _stagedMusic = request.UseMusic;
            });
            _cancel = _owner.Engine.AnimationMessages.SubscribeMessage(0x88, false, _ =>
            { if (OwnerCurrent && EngineRunning) Phase = 11; });
            _changed = _owner.Engine.AnimationMessages.SubscribeMessage(0x47, true, value =>
            {
                if (!OwnerCurrent || value is not NativeChangedFace change) return;
                if (change.OldId == _target.Selected)
                {
                    var face = _m.Vision.Faces.GetNativeFace(_owner, change.NewId);
                    if (!OwnerCurrent) return;
                    if (face?.HasName == true && change.NewId != _save) { _wrongId = change.NewId; _wrongName = face.Name!; Phase = 9; }
                    else _target.Selected = change.NewId;
                }
                if (change.OldId == _save) Phase = 10;
            });
        }
    }
    private int _wrongId;
    private string _wrongName = "";
    private bool OwnerCurrent => Volatile.Read(ref _retired) == 0 && !_owner.Lifetime.Destroying && ReferenceEquals(_owner.Engine.Robot, _owner);
    private bool Current(long generation) => OwnerCurrent && Interlocked.Read(ref _generation) == generation && EngineRunning;
    protected override IDisposable? EnterHostExecutionOwner(BehaviorContext? context) => _owner.Engine.EnterSynchronousOwner();
    protected override void HostRunStarting() => Interlocked.Increment(ref _generation);
    protected override long CaptureHostExecutionVersion() => Interlocked.Read(ref _generation);
    protected override bool HostExecutionVersionCurrent(long version) => OwnerCurrent && Interlocked.Read(ref _generation) == version;
    protected override bool HostResumeContinuationCurrent(long version) => OwnerCurrent &&
        (Interlocked.Read(ref _generation) == version || Interlocked.Read(ref _generation) == version + 1);
    protected override bool HostStopContinuationCurrent(long version) => Volatile.Read(ref _retired) == 0 && Interlocked.Read(ref _generation) == version + 1;
    protected override bool KeepsRunningWithoutAction => HasCurrentAction || _musicRequest is not null;
    protected override bool RunnableGate20(BehaviorContext context) => false;
    protected override bool RunnableGate24(BehaviorContext context) => false;
    protected override bool RunnableGate28(BehaviorContext context) => false;
    protected override bool IsRunnableInternal(BehaviorContext context) => _stagedName.Length != 0;

    protected override void OnStart()
    {
        long generation = CaptureHostExecutionVersion();
        if (!OwnerCurrent) { InitFailed = true; return; }
        if (Phase is 5 or 8) { Save(generation); return; }
        if (Phase == 4)
        {
            if (!RequireAssets(_save == 0 ? new[] { 0x10D, 0x10F, 0x110, 0x111 } : new[] { 0x10D, 0x112 })) return;
            if (Current(generation)) SayName(generation);
            return;
        }
        if (Phase == 7)
        {
            if (RequireAssets(new[] { 0x114 }) && Current(generation)) InterruptScan(generation);
            return;
        }
        Phase = 1;
        _name = _stagedName; _save = _stagedSave; _write = _stagedWrite; _say = _stagedSay; _music = _stagedMusic;
        if (_save != 0 && _m.Vision.Faces.GetNativeFace(_owner, _save) is { HasName: false })
        {
            _stagedName = "";
            _m.Vision.SetFaceEnrollmentMode(0);
            if (Current(generation)) InitFailed = true;
            return;
        }
        if (!Current(generation)) return;
        int[] required = _save == 0
            ? new[] { 0x10C, 0x10D, 0x10E, 0x10F, 0x110, 0x111, 0x114 }
            : new[] { 0x10C, 0x10D, 0x10E, 0x112, 0x114 };
        if (!RequireAssets(required) || !Current(generation)) return;
        _target.Reset(_stagedObserved); _initialSelected = _stagedObserved != 0;
        _albumResult = _enrollmentResult = EngineActionResult.NotStarted;
        _wrongId = 0; _wrongName = ""; _started = _owner.Engine.Timer.SecondsF;
        _window = 15; _reverse = 0; _previousPan = 0;
        _m.Vision.Faces.ResetEnrollmentCompletionForBehavior();
        _m.Vision.SetFaceEnrollmentMode(0);
        if (!Current(generation)) return;
        if (!Scope.SmartDisableReactionsWithLockGuarded(Id, ReactionLockTables.EnrollFace, () => Current(generation))) return;
        if (Current(generation)) Look(generation, false);
    }

    private bool RequireAssets(IEnumerable<int> triggers)
    {
        if (_m.Robot.Animations.Library is { } library && _owner.GetCannedSpeechOwner() is not null &&
            triggers.All(id => library.HasAnimationForTrigger((AnimationTrigger)id))) return true;
        InitFailed = true;
        ReportMissing("M14-006 enrollment authored animation/audio inputs unavailable");
        return false;
    }

    private bool SafeTreads => _m.Robot.Sensors.OffTreadsState == OffTreadsState.OnTreads && !_m.Robot.Sensors.CliffDetectedByEvent;
    private TriggerAnimationAction Animation(int trigger, int loops = 1)
    {
        var action = new TriggerAnimationAction(_m.Robot, new((AnimationTrigger)trigger, loops, true, 0, 60f, false));
        action.UseOrdinaryAuthoredAudio(_owner);
        return action;
    }
    private bool Add(CompoundAction compound, ActionRunner child, long generation)
    {
        if (compound.AddActionGuarded(child, false, () => Current(generation))) return true;
        child.RetireNeverAdmittedTree(); return false;
    }
    private ActionRunner Turn(int id, float maximum) => new NativeLastFaceRunner(_m, _owner, id, maximum);
    private CompoundActionParallel? LookTurn(long generation, bool interrupted)
    {
        var parallel = new CompoundActionParallel(() => _owner.Engine.Timer.SecondsF);
        if (!Add(parallel, _m.Robot.Motion.CreateNativeLiftChild(32, 5, _owner), generation))
        { parallel.RetireNeverAdmittedTree(); return null; }
        if (interrupted && !Add(parallel, Animation(0x114), generation))
        { parallel.RetireNeverAdmittedTree(); return null; }
        if (!SafeTreads) return parallel;
        int id = _target.Selected; float max = BitConverter.UInt32BitsToSingle(0x3F490FDB);
        if (id != 0 && _m.Vision.Faces.GetNativeFace(_owner, id) is null) id = 0;
        else if (id == 0)
        {
            if (_save != 0 && _m.Vision.Faces.GetNativeFace(_owner, _save) is not null) id = _save;
            else
            {
                uint newest = 0;
                foreach (int candidate in _m.Vision.Faces.GetNativeFaceIds(_owner))
                {
                    var face = _m.Vision.Faces.GetNativeFace(_owner, candidate);
                    if (face is null || face.HasName || face.LastObservedTimestamp < newest) continue;
                    if (id != 0 && face.LastObservedTimestamp == newest && !(_owner.Engine.ContextRandom.RandDbl(1) < .5)) continue;
                    id = candidate; newest = face.LastObservedTimestamp;
                }
            }
            if (id != 0) max = BitConverter.UInt32BitsToSingle(0x3FC90FDB);
        }
        if (!Add(parallel, Turn(id, max), generation))
        { parallel.RetireNeverAdmittedTree(); return null; }
        return parallel;
    }
    private void Look(long generation, bool interrupted)
    {
        if (!Current(generation)) return;
        Phase = 2;
        var turn = LookTurn(generation, interrupted);
        if (turn is null) return;
        var sequence = new CompoundActionSequential(() => _owner.Engine.Timer.SecondsF);
        if (!Add(sequence, turn, generation) || !Add(sequence, new NativeWaitForImagesRunner(_m, 3, (VisionMode)2, 0) { ScopedOwner = _owner }, generation))
        { sequence.RetireNeverAdmittedTree(); return; }
        StopActing(false, false);
        if (!Current(generation)) { sequence.RetireNeverAdmittedTree(); return; }
        StartActing(sequence, _ => { if (Current(generation)) LookComplete(generation); }, () => Current(generation));
    }
    private bool TimedOut()
    {
        float now = _owner.Engine.Timer.SecondsF;
        return now > _started + _window || (_target.TooManySince > 0 && now > _target.TooManySince + _tooManyTimeout);
    }
    private void LookComplete(long generation)
    {
        if (_target.LastObserved == 0)
        {
            if (TimedOut()) { Phase = 6; return; }
            LookAround(generation); return;
        }
        _window = MathF.Min(35, _window + 8);
        if (!SafeTreads) { StartActing(Animation(0x10C), _ => { if (Current(generation)) Scan(generation); }, () => Current(generation)); return; }
        var sequence = new CompoundActionSequential(() => _owner.Engine.Timer.SecondsF);
        var parallel = new CompoundActionParallel(() => _owner.Engine.Timer.SecondsF);
        if (!Add(sequence, Turn(_target.Selected, Radians.Pi), generation) || !Add(parallel, Animation(0x10C), generation) ||
            !Add(parallel, new DriveStraightAction(_m, _owner, 14, 75, false), generation) || !Add(sequence, parallel, generation))
        { sequence.RetireNeverAdmittedTree(); parallel.RetireNeverAdmittedTree(); return; }
        StartActing(sequence, _ => { if (Current(generation)) Scan(generation); }, () => Current(generation));
    }

    private void LookAround(long generation)
    {
        float pitch = (float)_owner.Engine.ContextRandom.RandDblInRange(0.6021386384963989, 0.7766715288162231);
        float magnitude = (float)_owner.Engine.ContextRandom.RandDblInRange(0, 0.1745329201221466);
        float pan = MathF.CopySign(magnitude, -_previousPan);
        float delta = new Radians(pan - _previousPan).Value;
        _previousPan = new Radians(pan).Value;
        bool treads = SafeTreads;
        var sequence = new CompoundActionSequential(() => _owner.Engine.Timer.SecondsF);
        ActionRunner first = treads
            ? new NativePanTiltRunner(_m, new PanAndTiltAction { PanAngleRad = delta, HeadAngleRad = pitch, PanIsAbsolute = false, HeadIsAbsolute = true }) { ScopedOwner = _owner }
            : _m.Robot.Motion.CreateNativeHeadChild(pitch, BitConverter.UInt32BitsToSingle(0x3D0EFA35), 15, 20, _owner);
        if (!Add(sequence, first, generation)) { sequence.RetireNeverAdmittedTree(); return; }
        if (treads && _reverse <= 50)
        {
            float amount = (float)_owner.Engine.ContextRandom.RandDblInRange(5, 15);
            _reverse += amount;
            if (!Add(sequence, new DriveStraightAction(_m, _owner, -amount, 100, false), generation))
            { sequence.RetireNeverAdmittedTree(); return; }
        }
        if (!Add(sequence, new NativeWaitForImagesRunner(_m, 3, (VisionMode)2, 0) { ScopedOwner = _owner }, generation))
        { sequence.RetireNeverAdmittedTree(); return; }
        StartActing(sequence, _ => { if (Current(generation)) Look(generation, false); }, () => Current(generation));
    }
    private void Scan(long generation)
    {
        if (!Current(generation)) return;
        Phase = 3;
        _m.Vision.SetFaceEnrollmentMode(_target.Selected);
        if (!Current(generation)) return;
        var parallel = new CompoundActionParallel(() => _owner.Engine.Timer.SecondsF);
        if (!Add(parallel, new NativeTrackFaceRunner(_m, _owner, _target.Selected, enrollment: true), generation) ||
            !Add(parallel, Animation(0x10E, 0), generation))
        { parallel.RetireNeverAdmittedTree(); return; }
        StartActing(parallel, _ => { }, () => Current(generation));
    }
    private void InterruptScan(long generation)
    {
        if (!Current(generation)) return;
        Phase = 7;
        StopActing(false, false);
        if (Current(generation)) StartActing(Animation(0x114), _ => { if (Current(generation)) Phase = 6; }, () => Current(generation));
    }
    protected override void OnUpdate()
    {
        long generation = CaptureHostExecutionVersion();
        if (!Current(generation)) return;
        if (Phase is 0 or 1 or 6 or 9 or 10 or 11) { Finish(); return; }
        if (Phase == 4 && _musicRequest is { IsCompleted: true } pending)
        {
            _musicRequest = null;
            if (!pending.IsCompletedSuccessfully)
            { ReportMissing("M14-006 enrollment music initialization: " + pending.Exception?.GetBaseException().Message); return; }
            StageMusicAndStartSpeech(generation);
            return;
        }
        if (Phase == 3 && _m.Vision.Faces.EnrollmentCompleted)
        {
            _target.ClearWrong(); _wrongId = 0; _wrongName = "";
            _m.Vision.AssignStockFaceName(_target.Selected, _name, _save);
            if (Current(generation)) SayName(generation);
            return;
        }
        if (Phase == 3 && TimedOut()) { InterruptScan(generation); return; }
        if (Phase is 2 or 3)
        {
            uint image = _m.Robot.CameraSettings.LastProcessedImageTimestamp;
            try { _target.Update(_m, _owner, _save, image, _maxFaces, _recent, _owner.Engine.Timer.SecondsF, () => Current(generation)); }
            catch (Exception ex) when (ex is InvalidOperationException or NativeActionUnavailable)
            { ReportMissing("M14-008 enrollment target unavailable: " + ex.Message); return; }
            if (!Current(generation)) return;
            if (Phase == 3 && unchecked(image - _target.LastObserved) >= 1501)
            {
                if (!_initialSelected) _target.Selected = 0;
                Look(generation, true);
            }
        }
    }

    private void SayName(long generation)
    {
        if (!Current(generation)) return;
        Phase = 4;
        StopActing(false, false);
        if (!Current(generation)) return;
        if (!_say) { ReportMissing("M14-006 enrollment without the stock say-name input is unjoined"); return; }
        if (_save == 0 && _music)
        {
            var audio = _owner.GetCannedSpeechOwner();
            if (audio is null) { ReportMissing("M14-006 enrollment shared music owner unavailable"); return; }
            _musicOwner = audio;
            if (audio.Ready.IsCompletedSuccessfully) StageMusicAndStartSpeech(generation);
            else _musicRequest = audio.Ready;
            return;
        }
        StartSpeech(generation);
    }
    private void StageMusicAndStartSpeech(long generation)
    {
        if (!Current(generation) || _musicOwner is not { } audio) return;
        if (!audio.MusicAvailable) { ReportMissing("M14-006 enrollment Music.bnk unavailable"); return; }
        // Once accepted, the state belongs to the persistent audio owner even
        // if this behavior is cancelled during its eager child constructors.
        Task stage = audio.RequestMusicState(0x99EA6572, false, 0, () => OwnerCurrent);
        try
        {
            if (!stage.Wait(TimeSpan.FromSeconds(2)))
            { ReportMissing("M14-006 enrollment music stage timed out"); return; }
        }
        catch (AggregateException ex)
        { ReportMissing("M14-006 enrollment music stage failed: " + ex.GetBaseException().Message); return; }
        if (Current(generation)) StartSpeech(generation);
    }
    private void StartSpeech(long generation)
    {
        if (!Current(generation)) return;
        CompoundActionSequential? sequence;
        try { sequence = NativeEnrollmentSpeech.Create(_m, _owner, _name, _save != 0, () => Current(generation)); }
        catch (Exception ex) when (ex is NativeActionUnavailable or IOException or InvalidOperationException)
        { ReportMissing("M14-006 enrollment authored speech unavailable: " + ex.Message); return; }
        if (sequence is null) return;
        StartActing(sequence, result =>
        {
            if (!Current(generation) || result.Result != 0) return;
            if (_write) Save(generation); else Phase = 0;
        }, () => Current(generation));
    }
    private void Save(long generation)
    {
        if (!Current(generation)) return;
        Phase = 5;
        _m.Vision.SaveFaceAlbumToRobot(r => { if (OwnerCurrent) _albumResult = r.Result == 0 ? 0u : 0x03000000; },
            r => { if (OwnerCurrent) _enrollmentResult = r.Result == 0 ? 0u : 0x03000000; });
        if (!Current(generation)) return;
        StartActing(new SaveWait(_m, _owner, () => _albumResult != EngineActionResult.NotStarted &&
            _enrollmentResult != EngineActionResult.NotStarted && (_save != 0 || (_albumResult == 0 && _enrollmentResult == 0))),
            result => { if (Current(generation)) Phase = result.Result == 0 ? 0 : 8; }, () => Current(generation));
    }
    private sealed class SaveWait : NativeVisionRunner
    {
        private readonly Func<bool> _done;
        internal SaveWait(ManipulationSystem m, EngineRobot owner, Func<bool> done) : base(m, 0x33, 0)
        { ScopedOwner = owner; _done = done; TimeoutSeconds = 5; }
        protected override string ActionName => "WaitForLambda";
        public override int Init() => 0;
        public override uint CheckIfDone() => _done() ? 0u : EngineActionResult.Running;
    }
    protected override void OnStop(BehaviorStopReason reason)
    {
        long stoppedGeneration = Interlocked.Increment(ref _generation);
        bool StopCurrent() => OwnerCurrent && Interlocked.Read(ref _generation) == stoppedGeneration;
        if (!StopCurrent()) return;
        _m.Vision.SetFaceEnrollmentMode(0);
        if (!StopCurrent()) return;
        if (_m.Robot.Sensors.OnChargerContacts) Phase = 11;
        if (Phase is 2 or 3 or 4 or 5 or 7 or 8) return;
        int id = _save != 0 ? _save : _target.Selected;
        string name = _name;
        byte result = Phase switch
        {
            0 => 0,
            6 when _target.TooManySince > 0 => 2,
            6 when _target.WrongId != 0 && _target.WrongName.Length != 0 => 1,
            6 => 3,
            9 => 1,
            11 => 6,
            _ => 7,
        };
        if (Phase == 9) { id = _wrongId; name = _wrongName; }
        else if (Phase == 6 && result == 1) { id = _target.WrongId; name = _target.WrongName; }
        _stagedName = "";
        _m.Vision.SetFaceEnrollmentMode(0);
        if (!StopCurrent()) return;
        if (result != 0 && _target.Selected != 0 && _save == 0)
            _m.Vision.EraseStockEnrolledFace(_owner, _target.Selected, StopCurrent);
        if (!StopCurrent()) return;
        if (result == 0) _owner.Engine.ReportBehaviorObjective(5, "EnrolledFaceWithName", Id);
        if (!StopCurrent()) return;
        _owner.PublishEnrollmentResult(result, id, name);
        if (StopCurrent()) Phase = 1;
    }
    public void Dispose()
    {
        Interlocked.Exchange(ref _retired, 1);
        _request?.Dispose(); _cancel?.Dispose(); _changed?.Dispose();
        RetireActionCompletionOwner();
    }
}
