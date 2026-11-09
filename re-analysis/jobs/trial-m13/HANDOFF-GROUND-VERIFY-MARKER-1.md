# GROUND-VERIFY-MARKER-1

- Ground constructs the verify child with the retained carried ID and concrete saved marker code (0x55483A–0x55488A). The shared TurnTowardsObject runner queries matching markers and transforms each against actual MAIN (0x54A36C, 0x54A3C4–0x54A3D8). The first failed concrete transform takes MarkerOriginProblem and returns 0x03000005, even if another matching candidate could transform (0x54A65C–0x54A6C4, 0x54A704). The no-match and ANY_CODE branches have separate results (0x54A270–0x54A2F2, 0x54A582–0x54A5F0).
- Ground propagates the child result and clears the saved object's located state before returning (0x5549B0–0x554A3A). The failure issues no child turn.

## Files and tests

`NativeObjectRunners.cs` returns the defined result and emits the original diagnostic and global error flag. `GroundPlacementCallerTests.cs` admits the actual Ground action, latches firmware busy, disconnects only the saved concrete marker, then asserts BadPose after unlink, the diagnostic and error flag, cleared selection, retained world object and released reaction lock. Two focused cases check failure after an earlier valid concrete candidate and preserve the existing managed ANY_CODE skip branch. Affected classes passed 55/55. The final diagnostic assertion passed in the Ground class run, 15/15.

## Workers

Reader requested gpt-6.1-sol at medium effort. Builder and reviewer used the configured default request. Runtime model identity was not observed. One reviewer correction round added the missing source diagnostic and error flag. The reader resolved the exact flag store at 0x54A6C4. The first diagnostic assertion was adjusted for the existing logger thread prefix.

## Records

- M15-022 now covers this concrete failure in the retained Ground child. Its retained visual-verification and first-pass reset are already present. The generic TrackObject branch is outside Ground's track=false route. The broader record still needs the manual Bring placement-at-pose host sequence checked against the native compound, the managerless reaction-lock boundary, and operator evidence for firmware busy/release order against PickAndPlaceResult.
- M13-026 already covers the actual MAIN-neck camera solve. The retained child uses native M11-065/M11-066 quaternion and translation algebra, the rotated camera pose and -8 mm camera Z, f32 distance and projection, and f32 iteration step (0x51838E, 0x510FFC–0x511064, 0x518494–0x5184DA). The old double Pose3d narrowing note belongs to the flat adapter outside this path. The remaining numerical equivalence is limited to host atan2f and the sqrtf NaN fallback in M13-026 under M13-023(e). Source ownership of the incoming world pose and downstream physical motion remain separate checks.
- M12-015 still owns only the 0x44 wire. This job changes the retained verification result after that message.
