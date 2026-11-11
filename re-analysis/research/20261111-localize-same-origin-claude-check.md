# LOCALIZE-SAME-ORIGIN-GEOMETRY-1: Claude check (manager, 2026-11-11)

Built by Claude while Codex was out of usage:
1. an extractor census (`.scratch/localize-same-origin-geometry-census.md`);
2. an implementer build;
3. an independent verifier, which found S0 to S15 matching the binary but failed the build on the cross-origin stop;
4. a manager correction;
5. the affected classes re-run, 238/238 passed.

## What is built

Automatic localization to a seen cube in the robot's current origin, Robot::LocalizeToObject 0x005154C4..0x00515AA4:

| Step | Address | Behaviour |
| --- | --- | --- |
| S0 | 0x005154C4..0x005154F4 | Debug log with the type and ID |
| S2 | 0x00515614..0x00515622 | The computed history state at the observation's time |
| S3–S8 | 0x005156AE..0x00515706 | The robot pose relative to the seen cube, re-parented to the known cube and flattened |
| S9 | 0x0051570A..0x00515760 | The f32 height blend (0x3DCCCCCD / 0x3F666666) while already localized |
| S10 | 0x0051513C | AddVisionOnlyStateToHistory |
| S12 | 0x0053053C | The computed state overwritten in place |
| S13 | 0x005135F8 | UpdateCurrPoseFromHistory |
| S14 | 0x0051238C | The native SetLocalizedTo with the current camera |
| S15 | 0x00515A88 | [Robot+0x2C6] := 1, so the next tick sends AbsoluteLocalizationUpdate (0x45) |

Every failure exit is built, keeping the writes the engine keeps. Before this, the automatic path jumped straight to
the SetLocalizedTo field writes: the robot's pose was never corrected from a seen cube and no localization update was
sent.

The verifier also found:
- **S9's zRobot:** the robot pose's own parent-relative z, the same field the engine reads.
- **The SetNewPose refactor:** no observable change.
- **Locking:** no inversion.

## The verifier's blocking finding and the fix

The engine tests the same-origin/cross-origin split after S10. The cross-origin branch (Rejigger and the origin updates,
0x00515854..0x005159B2) is not built. The built order therefore let an unbuilt cross-origin pair write a vision-only
state, a frame and the +0x2C6 flag before stopping. The next tick would then send the robot an AbsoluteLocalizationUpdate
in the wrong origin. In the edge case that leaves the robot and the stack on different origins, freezing MAIN and
dropping later marker frames.

**Fix:** the stack tests the split before S2. The split depends only on E and the current origin, which S2 to S10 don't
change. An unbuilt cross-origin pair now writes and sends nothing, fails visibly (MISSING names
LOCALIZE-CROSS-ORIGIN-1), and LocalizeRobot moves on to the next pair. A test checks no frame write, no vision-only
state and no localization.

**Consequence until LOCALIZE-CROSS-ORIGIN-1:** after Cozmo is picked up and put down, a cube he knew before does not
re-join the two origins. The current-origin pair in the same frame still localizes.

## Tests changed

- **M11RVisBuildTests (two tests) and VisionTests (third-frame count, 3 → 2):** they now assert what the offline
  ProcessImage rig does. That rig has no computed state, so it stops at S2f.
  - They are not engine claims; with a real computed state the engine localizes, and the count would be 3 again.
  - So no test now drives queued frames through to a successful automatic localization. Queued as LOCALIZE-PRODUCTION-PATH-TEST-1.
- **RFixBatch3Tests:** the origin-2 state carries the frame ID the robot would report back.
- **LocalizeToObjectSameOriginTests:** 10 unit tests with substituted poses. The S9 blend is checked bit-exactly; the pose compositions within a tolerance, since no emulator oracle exists for pose-tree functions.

## Decisions (recorded in M11-044)

1. **S8 failure:** not built. It needs the M11-066 loop-bound overflow.
2. **Computed-map readers:** two direct native callers. The stack's other readers are listed in M11-044's `unresolved`.
3. **The seen cube's pose:** carries a native pose and timestamp on the live worker path. Checked before building.
4. **Failure exits:** all built.

**No record settles.** M11-044 still needs the cross-origin branch, LocalizeToMat, OnRobotRelocalized and the
production-path test.
