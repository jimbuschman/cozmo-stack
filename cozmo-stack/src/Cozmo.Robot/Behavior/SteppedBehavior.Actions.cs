// fidelity: M8-015
namespace Cozmo.Robot.Behavior;

public abstract partial class SteppedBehavior
{
    private IActionRunner? _runner;
    private ActionList? _runnerActionList;
    // The callable at +88, with presence at +98. Init and StopOnNextActionComplete clear it.
    private Action<RobotCompletedAction>? _runnerCompletion88;
    internal IActionRunner? OwnedRunner => _runner;
    internal uint CurrentRunnerTag => unchecked((uint)Volatile.Read(ref _currentActionHandle));
    // The default is the real robot+250 ActionList. The seam exposes defined QueueAction failure results.
    internal Func<QueueActionPosition, IActionRunner, byte, int>? QueueOwnedAction;
    private RetainedSubscription? _actionCompletionSubscription;
    internal void BindActionCompletionOwner(CozmoRobot robot,EngineRobot? actualOwner=null)
    {
        if (_actionCompletionSubscription is not null) return;
        var owner=actualOwner??robot.Engine.Robot;
        if(owner is null)return; // An unbound standalone constructor is diagnosed at admission.
        // IBehavior ctor 0x005BBD5E..7A; the functor invokes HandleActionComplete
        // synchronously, independently of the later ActionWatcher callback drain.
        if (owner.HasExternalInterface)
            _actionCompletionSubscription=robot.Engine.AnimationMessages.SubscribeMessage(RobotCompletedAction.UnionTag,true,
                payload=>{if(payload is RobotCompletedAction completed)HandleActionComplete(completed);});
    }
    // Host callers without a native container can own a constructor-lifetime lease. Never release an existing/native binding.
    internal Action? AcquireActionCompletionOwner(CozmoRobot robot)
    {
        if(_actionCompletionSubscription is not null)return null;
        BindActionCompletionOwner(robot);
        var owned=_actionCompletionSubscription;
        if(owned is null)return null;
        return ()=>{if(!ReferenceEquals(_actionCompletionSubscription,owned))return;_actionCompletionSubscription=null;owned.Dispose();};
    }
    internal void RetireActionCompletionOwner()
    {
        _actionCompletionSubscription?.Dispose();_actionCompletionSubscription=null;
    }

    /// <summary>5BDACC..5BDC12: ownership of incoming passes to this call, including every reject path.</summary>
    internal bool StartActing(IActionRunner? incoming, Action<RobotCompletedAction>? completion, Func<bool>? admissionCurrent = null)
    {
        if (incoming is null)
        {
            if (!_engineActingFlag)
                ReportMissing("M1-034 M8-015: skip StartActing null action dereference at 0x005BDBC8/0x005BDB10/0x005BDB4E; preserve prior behavior state");
            return false;
        }
        var list = Context?.Robot.Engine.Robot?.ActionList;
        if (incoming is ActionRunner runner && list is not null) runner.Watcher = list.Watcher;
        if (_engineActingFlag) { DeleteRejectedRunner(incoming); return false; }
        if (!_resuming && !_engineRunning)
        {
            OwnedActionWarning("IBehavior.StartActing.Failure.NotRunning", incoming,
                name => $"Behavior '{Id}' can't start {name} action because it is not running");
            DeleteRejectedRunner(incoming); return false;
        }
        if (HasCurrentAction)
        {
            OwnedActionWarning("IBehavior.StartActing.Failure.AlreadyActing", incoming,
                name => $"Behavior '{Id}' can't start {name} action because it is already running an action in state {_stateName}");
            DeleteRejectedRunner(incoming); return false;
        }

        // Assignment to +88 destroys/replaces the old callable BEFORE publishing +84 or calling slot80.
        _runnerCompletion88 = completion;
        _sharedHandle = completion;
        _runner = incoming;
        _runnerActionList=QueueOwnedAction is null?list:null;
        Volatile.Write(ref _currentActionHandle, unchecked((int)incoming.Tag));
        ScoredActingStateChanged(true);
        bool RetireStaleBeforeQueue()
        {
            if(admissionCurrent is null||admissionCurrent())return false;
            if(ReferenceEquals(_runner,incoming))
            {
                _runner=null;_runnerActionList=null;_runnerCompletion88=null;_sharedHandle=null;
                Volatile.Write(ref _currentActionHandle,0);
            }
            if(incoming is ActionRunner stale&&stale.CanDetachWatcherForRetirement()){stale.Watcher=null;stale.RetireNeverAdmittedTree();}
            return true;
        }
        if(RetireStaleBeforeQueue())return false;        if (_actionCompletionSubscription is null && Context.Robot.Engine.Robot?.HasExternalInterface==true)
            ReportMissing("M8-015 IBehavior constructor completion subscription is unavailable for this action owner");
        if(RetireStaleBeforeQueue())return false;
        int result;
        if (QueueOwnedAction is { } queue) { if(incoming is ActionRunner ownedRunner)ownedRunner.MarkQueueAdmission(); result = queue(QueueActionPosition.Now, incoming, 0); }
        else if (list is not null) result = admissionCurrent is null?list.QueueAction(QueueActionPosition.Now,incoming,0):list.QueueActionGuardedNow(incoming,0,admissionCurrent);
        else
        {
            ReportMissing("M8-015 owned StartActing 0x005BDB68: robot+0x250 ActionList owner absent");
            // The native path dereferences null. M1-034 skips the queue call with prior state preserved.
            ReportMissing("M1-034 M8-015: skip null ActionList dereference at 0x005BDB6C; preserve assigned callback/tag");
            return false;
        }
        if(admissionCurrent is not null&&!admissionCurrent())
        {
            // Guarded queue admission retires the never-inserted tree itself.
            if(result!=0 && result!=2 && incoming is ActionRunner {DestructionObserved:false})DeleteRejectedRunner(incoming);
            return false;
        }
        if (result == 0) return true;
        if (incoming is ActionRunner { DestructionObserved: true })
        {
            ReportMissing("M1-034 M8-015: skip freed action name at 0x005BDB8C and second delete at 0x005BDC04 after queue rejection deleted it at 0x0053DCFE; preserve callback/tag");
            if (ReferenceEquals(_runner, incoming)) _runner = null;
            return false;
        }
        OwnedActionWarning("IBehavior.StartActing.Failure.NotQueued", incoming,
            name => FormattableString.Invariant($"Behavior '{Id}' can't queue action '{name}' (error {result})"));
        DeleteRejectedRunner(incoming);
        // 5BDC04 does not clear +84 or restore the prior callable on queue failure.
        return false;
    }

    private void DeleteRejectedRunner(IActionRunner incoming)
    {
        if (incoming is ActionRunner { DestructionObserved: true })
        {
            ReportMissing("M1-034 M8-015: skip second IActionRunner delete at 0x005BDC04 after queue rejection already deleted it (0x0053DCFE)");
            if (ReferenceEquals(_runner, incoming)) _runner = null;
            return;
        }
        // The native deleting destructor is direct here. Prep/snapshot/broadcast are queue-deletion steps,
        // and adding them to a rejected action would invent a completion event.
        incoming.WatcherEnding();
        if (ReferenceEquals(_runner, incoming)) _runner = null;
    }

    private void OwnedActionWarning(string key, IActionRunner action, Func<string, string> format)
    {
        if (action.NativeName is not { } name)
        {
            ReportMissing($"M8-015 {key}: IActionRunner native name+0x48 absent at 0x005BDB10/0x005BDB8C/0x005BDBC8");
            return;
        }
        if (Context is null)
        {
            ReportMissing($"M8-015 {key}: IBehavior robot/log owner+0x2C absent");
            return;
        }
        Context.Robot.Engine.EmitSourceLog(EngineLogLevel.Warning, null, key, format(name));
    }

    /// <summary>5BE1EC..5BE224: clear matching tag, slot80(false), then invoke the installed callable in place.</summary>
    internal void HandleActionComplete(RobotCompletedAction completed)
    {
        long hostVersion = CaptureHostExecutionVersion();
        using var executionLease = EnterHostExecutionOwner(Context);
        if (!HostExecutionVersionCurrent(hostVersion)) return;
        uint tag = CurrentRunnerTag;
        if (tag == 0 || completed.Tag != tag) return;
        Volatile.Write(ref _currentActionHandle, 0);
        _runner = null;
        _runnerActionList=null;
        ScoredActingStateChanged(false);
        if (_engineRunning) _runnerCompletion88?.Invoke(completed);
        // Deliberately no clear/restore of +88 after invocation: a nested StartActing may have replaced it.
    }
}
