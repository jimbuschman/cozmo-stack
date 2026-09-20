# M14 — the face / person / pet pipeline around the OKAO boundary (2026-09-20)

What M14 added to `cozmo-stack/`, what in it is the engine's, and where the boundary is. Code:
`src/Cozmo.Robot/Vision/Faces.cs` (`IFaceDetector`, `OkaoFaceDetector`, `DetectedFace`, `TrackedFace`,
`FaceWorld`, `FaceEntry`, `SmartFaceID`, `IPetDetector`, `PetWorld`), `Vision/FaceActions.cs`
(`TurnTowardsPoseAction`, `TurnTowardsFaceAction`, `TrackFaceAction`, `VisuallyVerifyFaceAction`),
`VisionSystem` (`Faces`, `Pets`, `FaceDetector`, `PetDetector`, `LastFaces`, `TurnOverride`),
`Behavior/FaceBehaviors.cs` (`ActionBehavior`, `FaceBehavior` and seven behaviour classes),
`ShippedBehaviors.Faces`; tests `tests/Cozmo.Protocol.Tests/FaceTests.cs` (12) with `FakeFaceDetector` in
`ManipRig.cs`.

## 1. The boundary

The engine detects, tracks, reads and recognises faces with Omron's OKAO Vision library.
`FaceTracker::Impl::Update` (0x0086D6xx) calls `OKAO_DT_Detect_GRAY`, `OKAO_DT_GetResultCount`,
`OKAO_DT_GetRawResultInfo`, `OKAO_CO_ConvertCenterToSquare`, then per face `DetectFaceParts`,
`EstimateExpression`, `DetectSmile`, `DetectGazeAndBlink`, `IsEnrollable` and hands enrollable faces to
`FaceRecognizer::SetNextFaceToRecognize` (its own thread, `FaceRecognizer::Run`). The pet detector is OKAO too
(`PetTracker` in `vision_config.json`: max 4 pets, face size 60–240 px, threshold 900). The library is 177
`OKAO_*` exports of proprietary code; per the instruction it is not reproduced or approximated.

This stack reproduces everything around it and names the hole: `IFaceDetector` / `IPetDetector` are the seam
where the engine calls the library; the stock implementations `OkaoFaceDetector` / `OkaoPetDetector` report
`IsAvailable = false` with the reason, `VisionSystem` runs the face path only while an available detector is
attached, and the face behaviours are therefore **implemented but not runnable** in the shipped stack. The
inventory counts them in their own row ("implemented on the face pipeline; runnable only with a face
detector (OKAO unavailable)"), not in the implementable total. Face **recognition** (names, enrolment,
`EnrollFace`, `RespondToRenameFace`, the album) sits entirely behind the boundary: `FaceWorld` carries names
when a detector supplies them and nothing else pretends to.

## 2. What was reproduced, with provenance

| piece | engine source | label |
| --- | --- | --- |
| `TrackedFace.UpdateTranslation` | 0x0087DE24: eye midpoint and intra-eye distance from the detected eyes, else from the rectangle (midpoint = centre − 0.125 h, eyes at ±0.25 w → 0.5 w), floored at 6 px; head = camera ray through the midpoint × (62 mm (0x42780000) × f / eye px); parented to the camera pose | NATIVE |
| `FaceWorld.AddOrUpdateFace` | 0x004F4278: robot state at the face's timestamp; "IgnoringFaceBelowRobot z=%f"; `WasRotatingTooFast(ts, 0.174533, 0.523599)`; match by id, else by pose within 220 mm (48400, 0x473D1000) or rectangle overlap 0.5; "Added new face with ID=%d at t=%d"; "Face observed before previous observation" rejected; `RobotObservedFace` with the max expression | NATIVE rules and constants; session ids for detectors without ids LOCAL |
| `FaceWorld.Update` | 0x004F52xx: unnamed faces unseen for 15000 ms (0x3A98) removed ("Removing unnamed face %d at t=%d, because it hasn't been seen since t=%d") | NATIVE |
| `FaceWorld` queries | `GetFaceIDsObservedSince`, `HasAnyFaces`, `GetLastObservedFace`, `GetFace`, `GetSmartFaceID`, `SetTurnedTowardsFace` / `HasTurnedTowardsFace`, `ChangeFaceID` ("Updating old face %d to new ID %d"), `RemoveFaceByID`, `OnRobotDelocalized` | NATIVE interface; origin handling reduced to one origin (a delocalisation clears) |
| `SmartFaceID` | follows `ChangeFaceID` | NATIVE |
| `PetWorld` | 0x0050B7E4: the same rotating-too-fast skip, `RobotObservedPet`, `GetPetByID`, `GetKnownPetsWithType`; `TrackLostCount` 2 from vision_config | NATIVE + ASSET |
| `TurnTowardsPoseAction` | 0x00549F10 / `Init` 0x0054A8FC: pose w.r.t. the robot, `atan2(y, x)` body turn skipped beyond the maximum, `ComputeHeadAngleToSeePose` (0.01 tolerance, 25 iterations) clamped −0.436332..0.776672; default pan tolerance 5° (0x3DB2B8C2) | NATIVE; the head solve is the M11 bisection |
| `TurnTowardsFaceAction` | 0x0054B754..0x0054C780: `GetFace` / `GetLastObservedFace` ("Required face pose, don't have one, failing"), turn, observation check ("Observed ID=%s at distSq=%.1f"), `CreateFineTuneAction` ("Will fine tune", emotion event "LookAtFaceVerified", 45° = 0.785398 max), `WaitForImagesAction` ("Will wait no more than %d frames"), say-name `SayTextAction` + trigger / no-name lift-safe trigger unless 0x23F, `SetTurnedTowardsFace` | NATIVE flow; frames waited (5) INFERRED; text-to-speech DEFERRED (the app's) |
| `TrackFaceAction` | 0x00565ADC / `UpdateTracking` 0x00565DBC: pan = atan2(y, x) + heading, tilt = atan((z − 49) / dist) (0xC2440000); `ITrackAction` minimum tolerance 2° (0x3D0EFA35), clamp period 0.4 / 0.15, eye shift ±32 / ±16 px, sound 0x23F | NATIVE math; update period LOCAL 100 ms; eye shift and driving animation DEFERRED |
| `VisuallyVerifyFaceAction` | 0x005DA812 (constructor only) | frame budget INFERRED |

## 3. The behaviours (14 shipped configs, not counted as implementable)

| class (configs) | engine | what was read |
| --- | --- | --- |
| `PlayAnimWithFaceBehavior` (FeedingPlayRequestAtFace ×2, VC_AlrightyResponse, VC_HowAreYouDoing ×4) | `BehaviorPlayAnimSequenceWithFace::InitInternal` 0x005C0648 | `TurnTowardsFaceAction(−1, π, false)` then the sequence; the three needs variants ship `NeutralFace` "overridden programmatically" from the needs level (M15's needs system) |
| `AcknowledgeFaceBehavior` (AcknowledgeFace) | 0x00602950..0x00602CA0 | `GetBestFaceToTrack`, `HasTurnedTowardsFace` + 60 s (0x42700000) → "currTime = %f, alreadyTurned:%d, shouldPlayGreeting:%d", triggers 1 / 2 (AcknowledgeFaceNamed / Unnamed), objective ReactedAcknowledgedFace; the reaction's `FacePositionUpdated` trigger re-arms it (`ReArm`, LOCAL_POLICY) |
| `InteractWithFacesBehavior` (InteractWithFaces, MeetCozmo_InteractWithFaces) | 0x005C1F00..0x005C2A60 | config keys; "VerifyFace" 0xF8 / 0xF9; `CanDriveIdealDistanceForward` → 40 mm (else −15) with a `TrackFaceAction` (4°, 0.0698132) stopped by the drive; "will track for %f seconds" in [8, 15] with 0xF7; emotion events InteractWithNamedFace / InteractWithUnnamedFace; objective InteractedWithFace. The memory-map check is DEFERRED (the ideal distance is driven) |
| `DriveToFaceBehavior` (VC_ComeHere) | 0x005DA550..0x005DAE00 | turn, `VisuallyVerifyFaceAction`, turn; `IsCozmoAlreadyCloseEnoughToFace` 200 mm (0x43480000); `DriveStraightAction(distance − 200, 60)` with decel 166.667; track 5 s |
| `SearchForFaceBehavior` (VC_SearchForFace) | 0x005C9158..0x005C9400 | 0x65 ComeHere_SearchForFace, `HasAnyFaces` → 0x66 ComeHere_SearchForFace_FoundFace; repeat bound INFERRED (3) |
| `ReactToPetBehavior` (ReactToPet) | 0x00606ED8..0x00607560 | `GetPetByID`, `TurnTowardsImagePointAction`, `GetAnimationTrigger` (Cat / Dog; `RandInt(20)` against 0x14 → PetDetectionSneeze INFERRED), `TrackPetFaceAction` with update timeout (DEFERRED: pets have no head pose) |
| `PyramidThankYouBehavior` (PyramidThankYou) | 0x005DE078..0x005DE320 | `HasAnyFaces` + the pyramid's block; `TurnTowardsFaceAction(π)`, 0x18 BuildPyramidThankUser, `TurnTowardsObjectAction(π)`, 0x18 |

Still behind the boundary or elsewhere: `FindFaces` ×3 and `LookForFaceAndCube` (they extend
`ExploreLookAroundInPlace`, M15), `PeekABoo` ×2 and `FistBump` ×2 (face games with the accelerometer and
timing state machines, not read), `PounceOnMotion` ×3 and `TrackLaser` (motion / laser detection, other
OKAO-free vision modes not built), `EnrollFace` and `RespondToRenameFace` (recognition), `Bouncer` (a face-tracked
display game).

## 4. Offline evidence

`FaceTests` (12): the stock detector is unavailable and nothing runs without one; a 62 px rectangle at
f = 290 lands 290 mm away; detected eyes win and the 6 px floor holds; the face world matches by pose within
220 mm, treats a face 500 mm away as another person, ignores rotating-too-fast and below-robot faces,
rejects stale timestamps, forgets an unnamed face after 15 s and keeps a named one, follows `ChangeFaceID`
through a `SmartFaceID`; frames through the fake detector localise a placed face to within the engine's own
depth-versus-range approximation; `TurnTowardsFaceAction` turns, fine-tunes on a fresh observation, fires
"LookAtFaceVerified" and picks the named greeting; `TrackFaceAction` follows a moving face; each behaviour's
transcribed flow on the fake robot side; the 14-config set needs a detector. Regression: the full suite, 619 tests, passes (606 after M13).

## 5. Hardware pending

None can be run: without a detector the face path does nothing on the robot. When a detector exists (a
future non-OKAO one, or an authorised OKAO build), item Y in `HARDWARE_TEST_PLAN.md` describes the check.

## 6. Next

M15: the freeplay / explorer layer — activities, choosers, strategies and the needs system from
`behaviorSystem/activities_config.json` and `activities/**` — so the stack selects and runs behaviours on its
own; `ExploreLookAroundInPlace` (which unlocks `FindFaces` on the face pipeline), `DriveInDesperation`,
`ExpressNeeds`, `PlayAnimOnNeedsChange`, `Wait`, `EarnedSparks`.
