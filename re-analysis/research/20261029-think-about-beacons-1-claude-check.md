# THINK-ABOUT-BEACONS-1: Claude check (manager, 2026-10-29)

**Job.** A Claude-run job:
1. cozmo-extractor read `research/20261029-think-about-beacons-1-read.md`, and the manager checked it against the
   .so (R5 0x005E5CC2, Init 0x005E5D94..0x005E5E4E, SelectNewBeacon 0x005E5F0C..0x005E5F3C, +0x28 0x005E5F4C);
2. cozmo-implementer built it.

**Review.** The manager reviewed the diff row by row against the read. **No independent cozmo-verifier ran:** the
operator asked to wrap up before usage reset. This is queued.

**Verdict: PASS (manager review only). Nothing settled** (M15-084, added with operator approval).

## Build (ThinkAboutBeaconsBehavior, CubeGameBehaviors.cs)
- **Gates.** +0x20, +0x24 and +0x28 all refuse (R1..R3). Runnable iff there is no beacon; the invented robot-pose
  condition is removed (R5).
- **Init.**
  - The "Selecting new beacon" trace (I1).
  - SelectNewBeacon: CopyCheckedMainPose, then GetWithRespectToRoot, then AddBeacon with the f32 radius
    (S1..S3). The vision-history fallback and the early Finish are removed.
  - The trigger name is resolved at Init. An empty name or "Count" means no action, and the behaviour completes on the
    next Update (I3/U1).
  - One plain TriggerAnimationAction(trigger, 1, true, 0, 60.0f, false) with no callback (I4/I5).
- **Stop and Resume.** Stop leaves the beacon (T1); Resume is the base one (P1).
- **Tests.** `ThinkAboutBeaconsNativeTests` is new. NavigationTests now checks the beacon against the native main pose
  w.r.t. its root. Results: focused 57/57 and 21/21. The full suite was not run here (it is pending; see
  PROJECT_STATE).

## Queued
1. ~~Run an independent cozmo-verifier pass~~ Done: **PASS**, 0 blocking (static review plus disassembly). Its findings are recorded in M15-084's `unresolved`:
   - an unknown trigger name plays trigger 0 (0x00764810), not a skip;
   - the canonical runtime lacks the class 0x35 constructor;
   - the config is fixed, not parsed.

   It also confirmed that CopyCheckedMainPose followed by GetWithRespectToRoot is the same pose Robot::GetPose returns.
2. **MISSING, now resolved by the verifier:** AnimationTriggerFromString returns 0 on a miss. The stack still
   reports MISSING there; building trigger-0 playback is queued. No shipped file reaches it.
3. **Beacon clearing (W1) is unbuilt in the stack:** AIWhiteboard.OnRobotDelocalized has no caller. In production
   TAB therefore runs only once per session. This is a follow-up job for the whiteboard and delocalization records.
4. The `AIWhiteboard.cs:397` text still names TAB as an unscanned reader of +0x10. TAB never reads it (R5).
5. M13-006 calls +0x10 "an int"; it is the f32 failure time. M15-011 says "copied robot pose"; it is the pose w.r.t.
   its root.
