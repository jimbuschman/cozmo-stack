# M12-024: the action side of the driving-animation lifecycle (Opus extractor, 2026-10-13)

- **Run:** read-only cozmo-extractor, after the B-M5-16 check (`research/20261013-b-m5-16-check.md`).
- **Adopted:** by the manager on 2026-10-13. This is the checked source for job B-M5-17.

## Every call site

PLTs: Init 0x004AB110, PlayStartAnim 0x004AB134, PlayEndAnim 0x004A5764, ActionIsBeingDestroyed 0x004AB104. There are
no veneers to them.

| Site | Function | Arguments, and what the return does |
|---|---|---|
| 0x005475F6 | DriveStraightAction::Init | Init(+0x54 tracks, +0x60 tag, +0x56 suppress, repeat 0) |
| 0x0055A888 | DriveToPoseAction::Init | the same, with repeat 0 |
| 0x00564D54 | ITrackAction::Init | only if +0xA8; repeat **1** |
| 0x00547980 | DriveStraight CheckIfDone | PlayStartAnim, once, when +0x89 first becomes HasPathToFollow()==1, if +0x8A |
| 0x0055AC08 | DriveToPose CheckIfDone | PlayStartAnim **every tick** in status 3; the return is ignored |
| 0x00564DF8 | ITrackAction::Init | PlayStartAnim if vtbl+0x30 returned 0 and +0xA8 |
| 0x005479E0 | DriveStraight CheckIfDone | PlayEndAnim: ≠0 → RUNNING, 0 → Success |
| 0x0055B0C0 | DriveToPose CheckIfDone tail | PlayEndAnim: ≠0 → RUNNING |
| 0x00565120, 0x005654BE, 0x005656C8 | ITrackAction::CheckIfDone | PlayEndAnim, return ignored; result stored at +0xAC; RUNNING |
| 0x00564EF6 | ITrackAction::CheckIfDoneReturnHelper | dead: no caller |
| 0x00547558 | ~DriveStraightAction | IsActive → Abort, then ActionIsBeingDestroyed |
| 0x0055A63C | ~DriveToPoseAction | IsActive → Abort; EraseAllPlannerObstacles(1), then (0); then ActionIsBeingDestroyed |
| 0x00564852 | ~ITrackAction | ActionIsBeingDestroyed, **unconditionally**, even with +0xA8 = 0 |

**Init's arguments** (0x004F0FC4..0x004F0FF4): +0x48 = tracks; +0x44 = tag; +0x49 = !suppressTrackLocking; +0x4A =
repeat. The action's fields are IActionRunner's: +0x54 tracksToLock, +0x56 suppressTrackLocking, +0x60 tag
(0x005408EC..0x00540908).

**Other users:**
- the DriveToObject/IDriveToInteract family, through DriveToPose/DriveStraight sub-actions;
- the Track* actions, through ITrackAction. EnableDrivingAnimation (+0xA8; constructor default 0) has one caller,
  BehaviorTrackLaser 0x005FBF3A;
- DriveStraight's +0x8A is 1 from the 2-argument constructor, or the bool parameter of the 4-argument one.

## The handler entry points (M5-063)

- **H1, PlayStartAnim** (0x004F0FF6..0x004F1014):
  - state ≠ 0 → return;
  - otherwise, if start ≠ 0x23F: PlayDrivingStartAnim;
  - else, if loop ≠ 0x23F: PlayDrivingLoopAnim.

  In effect it is void.
- **H2, PlayDrivingStartAnim** (0x004F1016..0x004F105A):
  1. state = 1;
  2. `new TriggerLiftSafeAnimationAction(robot, start, 1, true, 0, 60.0f, false)`;
  3. +0x58 = its tag;
  4. QueueAction(5, action, 0).
- **H3, PlayEndAnim** (0x004F0EFA..0x004F0F4A):
  1. state ∈ {3,4,5} → return 0;
  2. Cancel(+0x58), then Cancel(+0x5C);
  3. end == 0x23F → return 0;
  4. otherwise Unlock if +0x49, then PlayDrivingEndAnim, and return 1.

  From state 0 it plays the end animation even though nothing started.
- **H5:** Cancel of an unknown tag is silent (0x0053E704..0x0053E7B0).
- **H6, ActionIsBeingDestroyed:** state = 5, then Cancel +0x58, +0x5C and +0x60. No relock, and no end animation.

## DriveToPoseAction (M12-024 build rows)

- **D1** (IAction::UpdateInternal 0x00540D1C..0x00540F9E):
  - the TIMEOUT check (0x03000018) runs before CheckIfDone, so it bypasses the tail;
  - Init result 0 → CheckIfDone runs in the same tick;
  - a non-zero Init result is the result.
- **D2, retry** (0x00540DFA..0x00540E7A): for a 0x04xxxxxx result with retries left, the next tick re-runs Init, so
  handler.Init runs again: state 0 and the IDs zeroed, with no cancel.
- **D3:** DriveToPose::Init calls handler.Init **first**, before every goal, pose and path check (0x0055A86C..0x0055A888).
  An Init that then fails still leaves the handler in state 0.
- **D4:** CheckIfDone first checks handler state 3 → RUNNING (0x0055AB5A..0x0055AB6A).
- **D5–D10, the status switch** (M12-023):
  - status > 4 → RUNNING, directly;
  - status 0 → 0x03000013, **directly, without the tail**;
  - status 1: the timeout gives 0x03000013, which the tail skips;
  - status 2 → RUNNING, directly;
  - status 3: PlayStartAnim, +0xB0 = −1, then the counter. On every 10th tick, sChanneledInfoF "Unnamed"
    "DriveToPoseAction.CheckIfDone.WaitingForPathCompletion" (0xBECBFB), with the format at 0xBECC32 and its arguments:
    tag, count, (s8)[pc+0x40], (u16)[pc+0x42] last sent, (u16)[pc+0x44] last received. RUNNING;
  - status 4: IsSameAs → 0; otherwise, with +0x42 == +0x44, 0x04000002; otherwise an error and 0x03000008.
- **D11, the tail** (0x0055B0AA..0x0055B0CC): for a result ∉ {RUNNING, 0x03000013}, PlayEndAnim. If it returns ≠0,
  return RUNNING; otherwise return the result.
- **D12:** nothing is stored.
  - While the end animation plays, the handler is in state 3, so D4 gives RUNNING.
  - When its completion (E2G 0x5A) sets state 4, the next CheckIfDone **re-evaluates the whole status switch** with
    the current pose and status. PlayEndAnim then returns 0, so that fresh result is returned.
- **D13:** how many ticks RUNNING is held depends on the ActionList order (M8-015). UNKNOWN.
- **D14:** the IAction timeout during the end animation finishes the drive with TIMEOUT. The destructor then cancels
  the end animation.
- **D15:** there is no Cancel override. Destructor order: IsActive → Abort; EraseAllPlannerObstacles(1), then (0); then
  ActionIsBeingDestroyed.
- **D16:** 0x03000013 = PATH_PLANNING_FAILED_ABORT. A planning failure ends the drive with no end animation, and the
  start or loop animation runs until destruction cancels it.

## DriveStraightAction (M13-013) and ITrackAction (M14-003)

- **S1, DriveStraight CheckIfDone** (0x005478C0..0x005479EE):
  1. handler state 3 → RUNNING;
  2. LastPathFailed → 0x04000002, with **no** PlayEndAnim;
  3. not started (+0x89 = 0): +0x89 = HasPathToFollow(); if that gives 1 and +0x8A is set, PlayStartAnim. RUNNING
     while +0x89 = 0;
  4. started: IsActive → RUNNING; otherwise, if +0x8A, PlayEndAnim (≠0 → RUNNING, 0 → Success 0).

  After the end animation the result is Success. The log strings at 0x005478F0..0x005479AE are unread.
- **S2, ~DriveStraightAction:** IsActive → Abort, then ActionIsBeingDestroyed.
- **T1, the ITrackAction gate:** if +0xA8 is set: handler state 3 → RUNNING; state 4 → return the stored +0xAC (the
  constructor sets it to 0x02000001).
- **T2, the ITrackAction end sites:** each ignores PlayEndAnim's return, stores the result at +0xAC, and returns
  RUNNING. If PlayEndAnim returned 0, the tracking logic runs again on the next tick.
- **T3, ~ITrackAction:** ActionIsBeingDestroyed **always**, even with driving animation disabled. So any Track*
  action's destruction resets the shared handler, which affects a drive that is running at the time.

## Tracks (M4-014, M8-007)

- **L1:** the handler's unlock releases the action's own IActionRunner lock (same tracks, same tag string)
  (0x004F0EB2..0x004F0EE2).
- **L2, UnlockTracks** (0x0063FE5C..0x0063FFD8). For each bit, find the LockInfo whose who equals the tag string
  (compared on the first string only).
  - **A hit** erases one entry.
  - **A miss** logs sChanneledInfoF "Unnamed" "MovementComponent.UnlockTracks" (0x640040), "Tracks 0x%x are not
    currently locked by %s" (0x640060), with the full mask and who, then calls PrintLockState() (0x006410D8, unread).
  - Either way, a track whose set is empty is ORed into a mask. If the mask ≠ 0, EnableAnimTracks(mask) is sent.
  - It returns (any erased track still has owners) & 1.
- **L3, the double unlock** (PlayEndAnim's): the second call misses on every bit, logs the info line, calls
  PrintLockState, and **sends EnableAnimTracks again** for the free tracks.
- **L4, LockTracks** (0x006400D0..0x0064018E): one LockInfo per bit; a set whose size becomes 1 contributes to
  DisableAnimTracks(mask), sent if ≠ 0.
- **L5, the relock at state 4:** who = to_string(tag), debugName "DrivingAnimations". IActionRunner::UnlockTracks
  (0x005408EC; skipped if +0x56 is set, or if [+0x18] == 0x02000001) removes it later.
  - A cancel or destroy during state 3 has no relock, so the final unlock misses (the L3 path).

## Unread

- **Re-entrance:** PlayEndAnim's Cancel → ActionQueue::DeleteActionAndIter (0x0053F9E4) broadcasts
  RobotCompletedAction (0x0053FA5E..0x0053FAB2). Whether that synchronously reaches HandleActionCompleted inside
  PlayEndAnim is unread (M8-015).
- PrintLockState 0x006410D8..0x00641500.
- The DriveToPose and DriveStraight timeouts (vtbl+0x2c).
- The IActionRunner::Update lock gate (0x00540428..0x00540584; M8-007).
- The initial value of sTagCounter.
