# Claude check: Freeplay follow-through batch

Date: 2026-11-01. Branch `codex-m13-manager-2` at `371a7e5d`, merged to main at `b45c1fa7`. Codex's full run on
`a3816235`: 7395/7395, 0 skipped. Jobs: SPARKS-LOOK-IN-PLACE-1, PLAYALONE-MOVES-ON-1, M8-019-END-1.

**Verdict: PASS for all three, with one test-rig defect that needs a small job.** A robot left alone now cycles through
Freeplay for the whole 5 minutes. M8-019, M15-058 and M15-011 settle.

## Plain probe (temporary, never committed)

Resource-backed `CozmoRobot`, ordinary Freeplay, no pickup or forced activity, 5 simulated minutes, a simulated robot that
acknowledges motors and completes animations. Motion is instant, so counts and times are not real pace.

| Scene, rig as built | Result |
|---|---|
| Off charger, empty or one cube | Loops Socialize, PlayAlone, Hiking, NothingToDo to the end. No behaviour running for 2691 of 3000 ticks. |

The idle ticks are a rig defect, not a robot defect:

- The rig calls `Animations.LoadFrom` after the robot connects. `CozmoRobot.BindNativeBehaviorManager`
  (`CozmoRobot.cs:209`) copies `Animations.DrivingTriggerMap ?? new AnimationTriggerMap()` into an init-only
  `BehaviorContext.Triggers` at bind time, so that context holds an empty map for good.
- Every `PlayAnimBehavior` on that context then fails to resolve its trigger, completes the same tick, plays nothing
  and sends nothing. Its trace says "the shipped trigger map names no group for this trigger", which is false:
  `AnimationTriggerMap.json` maps NothingToDoBoredIdle to `ag_hiking_lookaround` and HikingIntro to `ag_hiking_getin`.
- Native owned actions resolve through the loaded library, so they are not affected. That is why Codex's
  PLAYALONE-MOVES-ON-1 test saw Hiking Think first: its fixture has the same order.
- Production loads before connecting (`EngineReadiness.cs:129`; `M13DriveOffHw.cs:492`). Code that loads after connecting
  gets the empty map, and many Conformance tools do that.

| Scene, context given the loaded map, as production has | Result |
|---|---|
| Off charger, empty | 15 idle ticks of 3000. FindFaces x8, Hiking_FirstLookIntro x4, NothingToDo_Idle x3, NothingToDo_BoredAnim x1. |
| Off charger, one cube | 15 idle ticks of 3000. Same cycle. |
| Spark 9, off charger | SparksLookInPlace x9, then PlayArbitraryAnim, then the ordinary loop. Spark no longer holds. |

Open from the probe: each PlayAnim held for 37.5 s regardless of clip. That is not a clip length, and it is shorter than
Hiking's 60 s limit. Whether this is the rig's animation completion or the stack is not established. The next robot run
shows it, and TRIGGER-MAP-BIND-1 asks for it.

## Checked in the .so and the C#

| Path | Addresses | Result |
|---|---|---|
| End request | 0x005A3930..0x005A39A6 | Info, then null locals, trigger 0x16, SwitchToBehaviorBase with the result discarded, release +0x14 then +0xC. Matches. |
| RobotStopped | 0x0053539C..0x00535486 | Evaluate, then the sensor-enabled byte; if clear, nothing more. Same r4 for manager+0x44, request, ActionList+0x250 Cancel(-1), broadcast. `Sensors.cs` matches, with the original owner held across callbacks. |
| Null switch | 0x005A1E20..0x005A1F44 | Old pair captured, StopAndNullify, null incoming goes straight to SetRunningAndResumeInfo, then SendDas. Matches `SwitchToEmptyForEndRequest`. |
| StopAndNullify | 0x005A2028..0x005A206A | Stop only when +0xA1 is set; live pair cleared after Stop. Matches. |
| SetRunningAndResumeInfo | 0x005A20D6, 0x005A20DA..0x005A2110, 0x005A212E, 0x005A2158 | Reaction report first, live trigger reloaded, movement +0xD4 and Robot+0x2C7 cleared when a reaction ends, null incoming skips the light vector. Matches. |
| Common Stop | 0x005BD10C..0x005BD126 | +0xA1 clear, StopInternal, +0x30 stamp, then StopActing. Matches. |
| StopWithoutImmediateRepetitionPenalty | 0x005BEEA4..0x005BEEBC | Stop, fresh time, +1.0 f32 to +0x108. Matches. |
| Score gate | 0x005BEF6A..0x005BEFBA | Nonrunning: IsRunnableBase, then vtable+0x50, before anything else. Unlock at 0x005BD806..0x005BD816, 0x55 bypasses. Supports the FireTruck rejection. |
| Recent parent | 0x005BD848..0x005BD862 | Manager +0x50, f32 window plus epsilon against now minus stamp. Matches. |
| Spark fields | 0x005A08DA..0x005A0900 | +0x50, +0x54 = -1.0f; +0x58, +0x60 = 0x55; bytes +0x5C, +0x64, +0x65 zero. Matches. |
| SetRequestedSpark | 0x005A3EB2..0x005A3EE6 | Byte, then ID, then name and Info. Matches. |
| SwitchToRequestedSpark | 0x005A41A6.., 0x005A420E..0x005A4226 | Names, Info, stamp +0x50, clear +0x65, copy +0x60 and +0x64. Matches. |
| High-level adapter tail | 0x005A4FB6..0x005A4FDE | Call first. Freeplay stamps +0x54 only while negative; otherwise 0x55 to +0x60 then +0x58. Soft bytes retained. Matches. |
| SelectNewBeacon | 0x005E5F12..0x005E5F30 | Whiteboard, GetPose, GetWithRespectToRoot helper 0x004DF628, AddBeacon with radius +0x128. Matches. |

Read in the handoffs and tests, not in the .so here: SparksLookInPlace config and predicate, LookInPlaceMemoryMap, Pounce,
the analyzer's empty-border proof, the EarnedSparks pending proof.

## Records

- **M8-019 settled,** EXACT_SOURCE on its own path. M4-019 stays settled; its caller was narrowed to the original owner.
- **M15-058 settled,** EXACT_SOURCE. Its location moves to `SparkManagerInput.cs`, where the code and tags are.
- **M15-011 settled,** EXACT_SOURCE, on the centre only.
- **M15-105 to M15-107 added** as IMPLEMENTATION_GAP: SparksLookInPlace, LookInPlaceMemoryMap, PounceOnMotion. None settled:
  none has run on a robot, and the bodies were not read here.
- M8-012, M15-084, M8-001 and M8-016 stay open.

## Progress

3 records settled across these 3 jobs, and 9 across 26 start-path jobs. Still 1 of 178 behaviours finished.
