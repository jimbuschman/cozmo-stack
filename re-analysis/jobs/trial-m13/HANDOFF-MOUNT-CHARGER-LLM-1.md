# MOUNT-CHARGER-LLM-1

READY FOR CLAUDE on codex-m13-manager-2; Windows full-suite gate remains blocked.
SCOPE.md A: return to and mount the charger. No new robot acceptance is claimed.

The three reported outcomes follow the engine's checks with the recorded inputs.
The stack difference found here is the missing final DriveToObject failure log.
The change restores that source log and adds a clearly host-named snapshot of the
evaluated offsets, thresholds, tested yaw and MAIN/object input poses. No threshold,
motion, retry count or success/failure result was changed.

## Recorded checks

Input: private 20261118-075139-LLM-SESSION. The committed JSON contains only selected
numeric test inputs and native expected outputs, not a hardware result bundle.

The last outbound AbsoluteLocalizationUpdate before all three attempts is frame
18028 at 159878.7428 ms: pose frame 5, origin 1. The selected F0 inputs remain in
frame 5/origin 1, as do the charger reports. The only observed cube, ID 1, has an
E2G 0x52 pose-unknown report at 162038.413 ms (events line 9341), after its final
0x44 at 161854.55 ms. That object is removed from the located map on this report's
native MarkObjectUnknown path (0x005073AA..0x005073B8 and DeleteLocatedObjects).
There is no later cube observation before the charger checks. The replay supplies
the located charger and no other obstacle; the target itself is excluded by its ID.

| Check time (ms) | MAIN x,y (mm) | Charger x,y,z (mm) | Predock goal x,y,z (mm) | Absolute planar offset / threshold (mm) | Actual decision |
| --- | --- | --- | --- | --- | --- |
| 578853.94, attempt 1 | 191.879,-0.618 | 14.976,2.096,-18.467 | 174.896,-34.260,-11.967 | (16.984,33.642) / (42.846,21.423) | Y is outside. Angle test is not reached. DriveToObject returns 0x04000001. |
| 600394.90, attempt 2 | 168.969,-16.038 | 14.302,7.918,-18.906 | 176.807,-14.173,-12.406 | (7.838,1.865) / (42.846,21.423) | Inside box; relative yaw 1.094127 deg is inside 7.499427 deg. DriveToObject succeeds and mount begins. |
| 647734.94, attempt 3 | 176.580,-5.572 | 3.404,16.528,7.028 | 155.665,-21.213,55.302 | (20.915,15.641) / (42.846,21.423) | Inside box; relative yaw -10.787345 deg is outside 7.499427 deg. DriveToObject returns 0x04000001. |

The final check obtains the located charger and calls the pose function again after
visual verification (0x00559A86..0x00559AA6). It therefore tests the current goal, not
only the pose used when the drive was planned. For example, attempt 3 began with
the last charger report at 606514.2753 ms, but its final check uses the new report
at 647675.5247 ms. A successful preceding drive/turn does not override that check.

The native distance threshold uses the 3-D **object relative to predock goal**
translation norm, then (2*d*sin(7.5deg), d*sin(7.5deg)); it does not use robot-to-goal
distance. World Z translation alone cancels in that relative distance. The final
box test compares only X and Y; there is no robot-to-goal Z check here. Full charger
orientation is retained, including the third observation's tilt. The native
pre-action tilt-validity check accepts each supplied candidate. The heading test
uses the goal relative to MAIN and strict abs(yaw) < tolerance + the negative
1e-5-radian slack; seeing/turning to the object does not guarantee this heading.

For attempt 2, raw frame line 75832 at 612212.6858 ms (robot timestamp 614020)
reports pitch **-0.2777020335197449 rad = -15.911154 deg**, status 33681, contacts off.
The next action check at 612214.56 ms therefore returns BackupPitchedTooFar
(0x0400000A), before calling the base drive completion check. The cutoff is
0xBE860A92 = -0.2617993950843811 rad, a strict less-than comparison. It is read from
the pose pitch supplied by the robot, not accelerometer-derived pitch.

The charger/robot heading difference at that failure exceeds pi/2. The native mount
state machine selects its forward positioning drive: +120 mm, 100 mm/s,
shouldPlayAnimation=false. When that drive succeeds, CheckIfDone changes 0 into
**0x04000006 NOT_ON_CHARGER** and returns. It never calls ConfigureTurnAndMountAction
again on that path. The AI admission queues the command with retry count 0. The
operator's expectation of a second backup is not what this invocation requests.

The repeated 'active object ... not connected' warning does not abort this path:
0x00620E78 checks the connected counterpart; null logs the warning at 0x00620E9E,
stores the warning cooldown, and falls through to the same 0x00620EDE continuation
as a non-null counterpart. The mount reads the **located** charger by world ID.
That warning is not evidence that charger ID 0 was rejected.

## Production path and evidence

| Step | Native evidence | C# path/result |
| --- | --- | --- |
| Default compound | 0x0052A292..0x0052A2EE; DriveToAndMountCharger ctor 0x0054E7F8 | QueueAiMountChargerAsync -> NativeDriveToMountChargerRunner: drive, then mount; unchanged. |
| Predock generation | Charger ctor 0x004E9CB0, static Pose2d init 0x004D6BC4, GeneratePreActionPoses 0x004E9FB0; PreActionPose copy-with-pose 0x0050DF00 | CubePreActionPoses / ChargerGeometry; one Docking candidate, unchanged. |
| Candidate validity | 0x004DF2C0..0x004DF4F0, R22 at 0x004DF2F2..0x004DF318 | Native oracle executes this check for each candidate; no other obstacle. |
| Threshold and final check | ComputePreActionPoseDistThreshold 0x00550098; GetPreActionPoses 0x00550FD2..0x00551196 | DockPreActionPoses.Get/Evaluate; strict decisions unchanged. |
| Default pose function | DriveToObjectAction::GetPossiblePoses 0x00558C80 (flag A false, 7.5deg tolerance) | DriveToObjectAction.GetPossiblePoses captures a diagnostic snapshot from the results already computed. |
| Drive completion | 0x00559880; 0x00559A86..0x00559AEC | NativeDriveToObjectRunner.CheckIfDone rechecks after the successful drive+turn. False inPosition overwrites the pose function result with 0x04000001. The native info at 0x00559ABE is restored, including when the function failed. |
| Pitch source and backup | F0 pitch writer 0x00512974..0x00512982 -> robot+0x304; GetPitchAngle 0x0051237E; backup CheckIfDone 0x0054E7A8 | MountChargerAction.BackupCheckIfDone: contacts, pitch, then base drive. Matches the recorded failure. |
| Retry branch | 0x0054E324..0x0054E3F2; forward-drive ctor 0x0054E72C..0x0054E770 | NativeDriveToMountChargerRunner.MountRunner chooses Retry and returns RetryDriveDone after success; unchanged. |

The host snapshot is named CozmoStack.DriveToObject.PoseCheck so it is not presented
as recovered engine log text. A yaw not reached by the native box test is reported
as 'not-tested'. Strings are captured while MAIN is owned; no extra pose evaluation
or acceptance decision is introduced. Reentrant parent retirement after either log
suppresses further diagnostics and returns the existing cancelled result. Stale
snapshots are cleared before each pose-function invocation.

## Independent native oracle and regression

emu_mount_charger_llm.py --check runs the real native charger constructor/generator,
copy-with-pose constructor, candidate tilt check, threshold, final box/yaw branch,
DriveToObject caller, backup pitch check, and mount retry state machine. The shipped
engine hash is pinned in the input fixture. It reproduces the three decisions and
returns 0x0400000A for the backup, without invoking the base drive. The real mount
state machine consumes one failed compound and one successful retry, configures
the retry once and returns 0x04000006.

Boundaries: input object quaternion has the recorded E2G float precision; MAIN uses
the matching recorded F0 root coordinates/yaw. The oracle is a post-vision check,
not a PnP/localisation replay or a physical-motion emulator. One candidate is
already selected, planar offsets are supplied as binary32 subtractions of the
native goal and MAIN, Robot::GetPose returns that MAIN, and transport/compound
completion inputs are the recorded outcomes. Child deletion/retry-child
construction are replaced at the explicitly named boundaries. The acceptance
math, angle extraction, pitch cutoff and retry result mapping run natively.

LlmMountChargerReplayTests compares all three geometry/results against that native
fixture and calls the actual C# NativeDriveToObjectRunner.CheckIfDone. It also tests
the recorded pitch through the normal packet handler/tick and backup check, with
only fixture routing IDs remapped, the native false-flag result overwrite, stale
diagnostic clearing, and parent retirement during logging. The private raw-packet
case is enabled with COZMO_LLM_SESSION_BUNDLE and reads frames.jsonl without taking
an exclusive share against the operator's still-open capture writer.

Validation: 56/56 focused tests (7 new cases plus charger/contact and existing
backup regressions); native oracle --check passes with zero and 0xA5 heap/stack
fills. Fidelity check passes (763 records); git diff --check passes.

Full Windows suite, immutable compiled snapshot, all 14 available private input
cases enabled: **8698 passed, 7 failed, 8705 total** (42.2075 minutes). All seven new
charger cases pass. Six fixed-port transport failures conflict with the operator's
live Cozmo MCP process owning UDP 47817. No process was stopped. The seventh is
FaceRecognitionRecordedReplayTests' named 247000..255800 case: line 208 expects the
greeting to end, but TurnTowardsLastFacePose times out at its native 30 seconds.
A separate, unchanged-limit run of the face class repeats that failure (2/3 pass).
This work does not modify face/speech behavior or suppress the failure. A clean
Windows full-suite gate is required before main.

The same six fixed-port tests pass 6/6 in Ubuntu using an isolated socket namespace
and workspace-local Linux .NET 9.0.3 (matching the Windows runtime), with the same
compiled test assembly and Windows ReadyToRun code disabled. A broader Linux
transport-class probe is 91/92: B17 oversized-datagram truncation differs on Linux
while that case passes on Windows. That separate host-platform observation is not
diagnosed or excluded here. Neither run is claimed as a full-suite pass.

Local logs (ignored): .scratch/mount-llm-focused3.log,
.scratch/m13-mount-llm-full-5ecfab65.log, .scratch/mount-llm-face-isolated.log,
.scratch/mount-llm-linux-six.log, .scratch/mount-llm-linux-transport2.log.

Fidelity-manifest impact: none. M12-029 (EXACT_SOURCE), M12-031/M12-020,
M13-008/M13-009 (IMPLEMENTATION_GAP) keep their current classifications. This bounded
failure explanation does not settle their larger paths. Existing M8-015/M8-017
EnumToString, M13-033/034 path-print and M12-024 visualizer MISSING diagnostics remain
visible; no broader record was silently closed.

Remaining uncertainty: no physical successful mount, camera/PnP replay or new
hardware acceptance is claimed. This explains the recorded failures and repairs
their missing diagnostics; it does not mark the SCOPE.md item finished.

