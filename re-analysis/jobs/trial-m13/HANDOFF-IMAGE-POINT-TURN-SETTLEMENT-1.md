# IMAGE-POINT-TURN-SETTLEMENT-1

## Claims and evidence

- Robot::ComputeTurnTowardsImagePointAngles reads the original VisionComponent's outer calibration at 0x005187A6–0x005187BA. It subtracts the centre in binary32 at 0x005187D2–0x005187EA and queries historical state with interpolation disabled at 0x005187F6–0x00518800. Successful projection reads the returned pose yaw at 0x00518896–0x0051889E and head angle at 0x00518874. The attached path uses the original owner's outer calibration and history. A missing outer calibration is handled visibly under M1-034 before the source null read at 0x005187D4.
- Head uses atan2f(-dv, fy) then the historical head add and assignment (0x00518868–0x00518882). Body uses atan2f(-du, fx), an enabled Radians delta, historical yaw and enabled rescale at 0x0051888A–0x005188B0 and 0x0084C97A–0x0084C9AA. The existing binary32 Radians helper implements the source wrap at 0x0084C87C. Host MathF.Atan2 remains the declared libm boundary.
- TurnTowardsImagePointAction stores image point and timestamp at 0x0054B59C–0x0054B616 and constructs an absolute pan and tilt action of type 0x2A at 0x0054B5C4–0x0054B618. Successful Init stores the two output angles and runs PanAndTilt::Init at 0x0054B6F4–0x0054B70E. The attached route uses the retained compound and its head/body children. The standalone host adapter keeps its separate delivery.
- A history failure warns at 0x00518818 and returns its status. The helper writes current main yaw and current head at 0x0051883E–0x0051885E. Init discards those outputs, warns at 0x0054B6B8 and returns 0x03000000 before creating either motor child (0x0054B6DE).

## Files and tests

- Vision/FaceActions.cs applies binary32 wrapping and selects the attached action route. Vision/NativeImagePointRunner.cs binds original-owner input and the retained pan-and-tilt child lifecycle. Vision/VisionSystem.cs exposes the original outer calibration for that owner, and CozmoRobot.cs identifies canonical vision so an unavailable retained runtime cannot fall through to direct motor sends.
- The manager ran original ARM 0x0051879C under Unicorn with supplied history/rotation cells, declared host atan2f and ceilf, and the original Radians body. It covered signed zero, negative pi, opposite off-centre wrapping, the large finite rescale branch and history-failure fallback. The probe is local and does not claim Android libm equality.
- NativeImagePointTests checks independent ARM projection bits, actual queued body/head commands and completion, distinct outer calibration, nearest-history selection, history failure without motor traffic, unavailable attached runtime, owner replacement, and cancellation before and after start. The cancellation test checks the selected action return; unrelated action survival is inspected at the exact-runner cancellation guard, not asserted in that fixture. Affected 149/149, final focused 13/13, and manager integration 43/43 passed.

## MISSING

- Android atan2f last-bit behavior, the native game factory 0x0052AF88 and its six motor override setters, and the distinct TrackPetFace consumer at 0x00568590 are outside this slice. Stock pet detection and the native ReactToPet sequence at 0x006070E0–0x00607192 remain open. No hardware run was made.

## Workers

- Reader requested gpt-6.1-sol at medium effort. Builder and independent reviewer used the configured default request. The manager reopened the original projection instructions and ran the independent helper probe. One review round closed the canonical stale or unavailable runtime fallback. Builder raised no remaining questions beyond the recorded libm and separate caller gaps.

## Records

- M14-005 owns the image-point projection and action. M11-097 owns historical state. M13-020 owns the retained pan-and-tilt compound, and M14-004/M14-014 own adjacent pet behavior and reports. No whole record settlement is proposed.
