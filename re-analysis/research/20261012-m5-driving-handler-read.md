# M5-063 and M5-064 read (Opus extractor, 2026-10-12)

- **Run:** read-only cozmo-extractor; every citation checked against the `.so`.
- **Adopted:** by the manager on 2026-10-12. This is the checked source for job B-M5-15.

## DrivingAnimationHandler state (ctor 0x004F06B0..0x004F0878, Init 0x004F0FC4..0x004F0FF4)

| Offset | Holds |
| --- | --- |
| +0 | State. ctor 5 (0x004F06DA), Init 0, start anim 1, loop 2, end anim 3, end done 4, ActionIsBeingDestroyed 5. |
| +8 | A stack (vector) of {DrivingAnimations (start, loop, end), lockName}. Elements are 0x18 bytes. |
| +0x14 / +0x18 / +0x1C | The current Start / Loop / End triggers. |
| +0x20 | Default: 0x95 DriveStartDefault, 0x91 DriveLoopDefault, 0x8D DriveEndDefault. |
| +0x2C | Sad: 0x94 DriveStartAngry, 0x90 DriveLoopAngry, 0x8C DriveEndAngry. |
| +0x38 | Needs met: 0x96 DriveStartHappy, 0x92 DriveLoopHappy, 0x8E DriveEndHappy. |
| +0x44 | Tag, Init's 2nd argument. |
| +0x48 | Tracks, Init's 1st argument. |
| +0x49 | !Init's 3rd argument. |
| +0x4A | Init's 5th argument. |
| +0x58 / +0x5C / +0x60 | The IDs of the start, loop and end actions. Init sets them to 0. |

Trigger 0x23F (Count) means "none". The names come from `unity/scripts/csharp/Anki.Cozmo/AnimationTrigger.cs`.

## M5-063 rows

1. **B5 → PushDrivingAnimations** (callback 0x004F1186..0x004F11AC). The message fields are three int32 triggers (start,
   loop, end) and the lockName (+0xC).
   - If state != 5, it warns `sWarningF("DrivingAnimationHandler.PushDrivingAnimations", {}, "Pushing new animations
     while currently playing")` (0x004F094C..0x004F0964; strings 0x4F0A34 / 0x4F0A64) and **still pushes**.
   - It then does push_back({anims, lockName}) (0x004F0988..0x004F09FA).
   - The current triggers are **not** changed.
2. **B6 → RemoveDrivingAnimations** (0x004F120E..0x004F1222):
   - **State not 5:** it warns `sWarningF("DrivingAnimationHandler.PopDrivingAnimations", {}, "Popping animations while
     currently playing")` (0x004F0A9E..0x004F0AB4; 0x4F0CA4 / 0x4F0CD4) and continues.
   - **Empty stack:** it warns `sWarningF("DrivingAnimationHandler.PopDrivingAnimations", {}, "Tried to pop animations but
     the stack is empty!")` (0xBE4672) and returns.
   - **The search:** the first entry from the front whose lockName is byte-equal is erased, alone
     (0x004F0AE4..0x004F0C6C). A miss returns silently.
   - **The loop bound:** after 1000 entries with no match, `sErrorF("LoopBoundOverflow", {}, "%s:%d",
     "../../../../engine/drivingAnimationHandler.cpp", 108)` → errG → break if enabled. Nothing is erased
     (0x004F0B5A..0x004F0BB6).
3. **UpdateCurrDrivingAnimations** (0x004F0D2C..0x004F0D74) is called **only from Init** (0x004F0FD0). It picks the
   first that applies:
   1. the back() of the stack;
   2. the Sad set, if MoodManager (robot+0x440) GetSimpleMood() == 1;
   3. the Needs-met set, if AreNeedsMet() == 1;
   4. the Default set.

   The result goes into +0x14..+0x1C. So a push or remove takes effect at the next Init.
4. **5A → HandleActionCompleted** (0x004F110E → 0x004F0D78). The result is at message +8 (`RobotCompletedAction.cs`).
   The tag compares, in order:
   - **ID == +0x58 and result == 0:** if the loop trigger is Count, return; otherwise PlayDrivingLoopAnim. A non-zero
     result falls through to the next compares (0x004F0D80..0x004F0D8C, 0x004F0E14..0x004F0E2A).
   - **ID == +0x5C:** if (+0x4A != 0 or PathComponent (robot+0x5C) HasPathToFollow() == 1) and result == 0, call
     PlayDrivingLoopAnim again. Otherwise call UnlockTracks(MovementComponent robot+0x254, +0x48, to_string(+0x44)) if
     +0x49 is set, then PlayEndAnim (0x004F0D8E..0x004F0DC6; helper 0x004F0EB2..0x004F0EE2).
   - **ID == +0x60:** state = 4, whatever the result. Then, if +0x49, LockTracks(…, +0x48, to_string(+0x44),
     "DrivingAnimations") (0x004F0DC8..0x004F0E12; helper 0x004F0F4C; string 0x4F0E44).
   - **Any other ID:** return, with no log.
5. **PlayEndAnim** (0x004F0EFA..0x004F0F4A):
   1. If state is 3, 4 or 5: return false.
   2. ActionList (robot+0x250) Cancel(+0x58), then Cancel(+0x5C).
   3. If the end trigger is Count: return false.
   4. UnlockTracks (if +0x49), then PlayDrivingEndAnim.

   On the loop-done path **UnlockTracks therefore runs twice** with the same arguments.
6. **PlayDrivingLoopAnim** (0x004F0E58..0x004F0EA0):
   1. state = 2;
   2. `new TriggerLiftSafeAnimationAction(robot, +0x18, 1, true, 0, 60.0f, false)` (positional);
   3. +0x5C = the action's ID (action+0x60);
   4. QueueAction(IN_PARALLEL = 5, action, 0).
7. **PlayDrivingEndAnim** (0x004F106C..0x004F10C0): if state is 3, 4 or 5, return. Otherwise state = 3, then the same
   action with +0x1C, its ID into +0x60, IN_PARALLEL.
8. **ActionIsBeingDestroyed** (0x004F0F94..0x004F0FC0): state = 5, then Cancel +0x58, +0x5C and +0x60.

**Engine-internal callers.** These call the handler directly, without G2E, so they are live in scope. Each caller's
arguments belong to its own layer.
- **Push:**
  - SevereNeedsComponent 0x5730E2;
  - ActivitySparked 0x5B1616;
  - IActivity::OnSelected 0x5B319A;
  - DriveOffCharger 0x5C0B4A;
  - PounceOnMotion 0x5F8472;
  - TrackLaser 0x5FB452;
  - OnboardingShowCube 0x600C9C;
  - ReactToFrustration 0x605C20.
- **Remove:**
  - SevereNeedsComponent 0x572CF6;
  - ActivitySparked 0x5B17DE;
  - IActivity::OnDeselected 0x5B3450;
  - DriveOffCharger 0x5C0DA0;
  - PounceOnMotion 0x5F87C2;
  - TrackLaser 0x5FBDD0;
  - OnboardingShowCube 0x600EAC;
  - ReactToFrustration 0x605D8E.

**Lifecycle callers** (M12-024 owns the call sites):
- **Init:** DriveStraight 0x5475F6, DriveToPose 0x55A888, ITrackAction 0x564D54.
- **PlayStartAnim:** 0x547980, 0x55AC08, 0x564DF8.
- **PlayEndAnim:** 0x5479E0, 0x55B0C0, 0x564EF6, 0x565120, 0x5654BE, 0x5656C8.
- **ActionIsBeingDestroyed:** from the action destructors at 0x547558, 0x55A63C, 0x564852.

**Not read:**
- what MovementComponent does on a second UnlockTracks with the same tag;
- the parameter names of TriggerLiftSafeAnimationAction.

## M5-064 rows

- **BaseStationTimer.** ~CozmoEngine clears gTickTimeProvider if it is the instance (0x004EC6A0..0x004EC6B2). Then
  removeInstance deletes the instance and nulls the pointer (0x0084BBE4..0x0084BC04). Its D2 is `bx lr`. Nothing
  observable.
  - **Lead for M5-055:** getInstance() re-creates a zeroed instance (0x0084BBA4..0x0084BBD0), so a later teardown read
    would see time 0.
- **Engine +0x38.** Only 0 is written to it within CozmoEngine and CozmoAPI. There is no step. A whole-binary writer
  search was not done.
- **TransferQueueMgr.** Dispatch::Stop → TaskExecutor::StopExecution (0x007FC684..0x007FC762):
  1. running = 0;
  2. pending and deferred tasks are destroyed without running;
  3. notify;
  4. join both threads.

  A batch already taken finishes first (Run 0x007FD0BE). The ProtoSignal +0xC is unlinked without invoking anything.
  Nothing is logged or sent.
- **GameLogTransferTask.** It releases its HttpAdapter (+0x10, from CreateHttpAdapter 0x006A4488..0x006A44A2).
  ~HttpAdapter (0x00805F88..0x00805FE8) sets state 0 and runs no callbacks; its only external effect is a JNI
  DeleteGlobalRef (lambda 0x00808300), which belongs to the Java layer the stack replaces. Then it unlinks its handles.
