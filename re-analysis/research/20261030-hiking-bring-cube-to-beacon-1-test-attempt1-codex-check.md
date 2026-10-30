# Bring affected attempt 1: independent read-only classification

The first 26-class affected run was **aborted after reported failures**. The preserved `builder/.scratch/hiking-bring-beacon-1/affected.log` has no TRX and cannot establish totals or a pass count. This classification reads the log and the reviewed 29-path correction-2 freeze; it does not run tests or alter code. Actual observed failed test names and parameter multiplicities:

1. `PickupLeafExecutesTheSelectedPredockProfile(check: True)`
2. `PickupLeafExecutesTheSelectedPredockProfile(check: False)`
3. `ReentrantStopAtPickupNeedDoesNotQueueFloorAction`
4. `CanonicalStackBranchQueuesRealPlaceOnActionWithoutPickupNeed`
5. `PickupLeafNoMarkerWinnerSkipsWithoutFailureMemory`
6. `StrictFloorQueryUsesCurrentOriginAndDoesNotInventNoncubeFootprints`
7. `PlaceRelativeLeafExecutesTheSelectedPredockProfile(check: True)`
8. `PlaceRelativeLeafExecutesTheSelectedPredockProfile(check: False)`
9. `DefinitionCountBelongsToWorldAndIsIndependentOfLocatedObjectsAndOrigins`
10. `CanonicalCarriedFloorRunUsesRealOwnedActionsAndNeedsBeforeGround`
11. `NativeNoFreePoseStampsBeaconAndMoodAfterPickupNeedWithoutAction`
12. `GroundLeafUnavailableRetiresOnlyOwnedRunWithoutFalseCompletionOrFailureMemory`

## Four concrete harness defect categories

1. **Direct leaf calls without the engine owner:** tests 1, 2, 5, 7 and 8 call leaf `Init` directly (`HikingBringBeaconCandidatesTests.cs:370,387,473`). The log shows `EnterSynchronousOwner` throwing because `BlockWorld.FrameGate` is held (`CozmoEngine.cs:2263`, `NativePickupObjectRunner.cs:32`, `NativePlaceRelRunner.cs:43`). The actual action-list path enters the synchronous owner before leaf execution. Use that owner for the direct unit seam, preserving the actual leaf, expected native marker outcomes and lock order; the failure alone does not establish a production defect.
2. **Fixture pose-root identity:** test 9 builds `new Pose3d(robot.GetNativeOrigin(...))` at `HikingBringBeaconCandidatesTests.cs:50`; the copy constructor (`Geometry.cs:277-280`) makes a new node with the source root's parent, which is null. `AIWhiteboard.AddBeacon` copies that distinct root (`AIWhiteboard.cs:150-153`), so the real cubes' poses do not share it and the checked relative-pose test rejects them. The source Think producer gets the robot pose relative to its root and adds that child pose at 0x005E5F12..30. Supply a pose parented to the actual origin. Test 11 also stores its failure pose as that unrelated copied root (:404-405): `IsSameAsWithAmbiguity` (`Geometry.cs:403-418`) rejects the root mismatch, so the intended failure does not reject the candidate. Its supplied beacon and failure pose need actual origin ancestry. Test 4 likewise uses a cloned root at :258; that input is invalid for its intended same-origin branch. This explains the bad input, but the preserved trace does not prove it is the sole cause of test 4's observed floor selection; check the stack predicate with corrected inputs before concluding.
3. **Clear is not map deletion:** test 6 calls `ClearLocatedObjectByIdInCurrentOrigin` at `HikingBringBeaconCandidatesTests.cs:305-309`, assuming cube 8 has left the map before adding a charger. The implementation delegates to `ClearLocatedObjectHelper` (`BlockWorld.cs:2361-2390`), which does not remove the located entry. `NativeBringBeaconGeometry.cs:20-25` stops at the first intersecting object, so the still-present cube can return false before reaching the non-cube footprint, and the expected exception does not follow. Use a distinct clean fixture or an established real removal and assert the world input; do not alter the native first-intersection decision to satisfy this fixture.
4. **Manager Stop during initialization:** test 3 calls `manager.Stop` from the needs log during `StartAsync` (`HikingBringBeaconCandidatesTests.cs:135-148`). `BehaviorManager.cs:930-956` explicitly keeps current behavior null through Init, setting it afterwards. The manager Stop therefore cannot stop that initializing behavior; the expectation that the old continuation must already be absent is unsupported by that fixture. Use an actual source-valid replacement/stop boundary, retaining the reentry guard coverage; do not change the native manager order to make this test pass.

## UNKNOWN pending focused diagnostics

- Test 10: the log shows the outer action was destroyed and `scene.Grounds` was 0 instead of 1 (`HikingBringBeaconCandidatesTests.cs:244-246`). It does not include the nested Drive result, source diagnostic, goal/physical geometry or completion trace needed to distinguish production from scene/harness cause. No root cause is asserted.
- Test 12: the log shows Ground never initialized in 250 scene steps and prints repeated robot `(0,-70)`, `carry7`, `grounds0` (`HikingBringBeaconCandidatesTests.cs:517-519`). It does not expose the actual nested action/drive outcome or missing-input cause. The ground-unavailable boundary was not reached; this is neither proof it works nor a diagnosis of the scene.

For both UNKNOWNs, capture the actual nested runner/result and relevant existing diagnostics under a finite test-only diagnostic run after review and lease grant. Do not manufacture completion, a path or a physical result. The manager authorized test-only correction of the four established fixture categories and additional nested diagnostics, with production unchanged. No .NET, new primary extraction, hardware, network or git mutation was performed in this classification.
