# FACE-WIRE-1

## Detector and consumer evidence

- FaceTracker::Impl starts the independent seed-1 RNG at 0x0086B2F0. Its
  detection init order is CO version, CO handle, DT video handle (5, 10),
  delay, search cycle, direction mask, pose extension, accuracy, angle,
  size, threshold and result handle (0x0086B4EA–0x0086BBBC).
- Update detects at 0x0086D776 and reads the count after success at 0x0086D7FC.
  Count one uses index zero. Multiple faces pre-read raw results for
  recognition state at 0x0086D898 and shuffle indices forward with the
  persistent RNG at 0x0086D8F6–0x0086D912, with count minus one draws. The output loop rereads each result
  at 0x0086D958. The raw +0x2C comparison with 0x100 supplies a flag, and
  does not suppress detection. 0x0086D980–0x0086D9DA converts center and
  size through the native corner helper and takes the LT and RB rectangle.
  No recognized ID means -raw track ID at 0x0086DD4E. Score reads raw+0xC
  at 0x0086DC6C.
- Part tracking uses PT confidence mode 1 at 0x0086BBD6. PT position uses
  DT result and the actual shuffled index at 0x0086CCC6. 0x0086CD28 detects
  points, treating -8 quietly, and 0x0086CD46 reads 12 points and
  confidences. Eyes zero and one are copied regardless of confidence.
  0x0086CF32 reads three direction values. The second is roll in radians,
  scaled by f32 0x3C8EFA35. The third feeds TrackedFace +0xDC. If the
  direction call fails, the eyes remain and roll keeps its default zero
  (0x0087DBB2, Radians 0x0084C828).
- On the drawn face_s10_2 fixture, native mode 1 returns twelve zero
  confidences with eyes (144,117) and (177,117) and directions (-5,0,1).
  Native mode 0 on the same fixture returns nonzero confidences.
  0x00922EEC passes the setting through, and 0x00922C58 reads the genuine
  result array. Zero is the shipped mode's output for this fixture.
- Shipped vision_config.json enables face detection and leaves expression,
  smile, gaze and blink off. The gates are 0x0086DA1A, 0x0086DA8E and
  0x0086DAFE. Recognition init at 0x00862A18 builds two feature handles
  and an album (1000,4). The shipped async feature extraction at
  0x00865034 publishes 2→0 on failure or 2→3 on success.
- The new FaceWorld entry gate uses body threshold 0x3F060A92, head
  threshold 0x3E32B8C2 and count 5 when raw +0x2C differs from 0x100,
  otherwise count 0 (0x004F456E–0x004F4596). The 0x006535AC wrapper
  tests head then body and respects vision while moving. Zero count
  uses the neighboring IMU samples and treats a missing bracket as
  rotating. Count five scans the first qualifying record and up to four
  predecessors. It tests the strict AND of the three absolute component
  thresholds (0x005391A6–0x00539248). Missing history in that branch
  returns false.
- Vision's tracking reset at 0x006B35F4 follows failed head or body pose
  similarity. The limits are strict 8 degrees and 10 mm. 0x0086CBF4
  calls DT_MV_ResetTracking through 0x004CF7F4 and clears recognition tracking.

Reader requested gpt-6.1-sol medium. Builder and reviewer use the configured default.
Actual backends are unobserved. Independent review passed after corrections to shuffle draw count, missing recognition diagnostics, optional-mode diagnostics, readiness and caller-supplied detector exception handling.

## Build and tests

- OkaoFaceDetection.cs runs the live DT and PT projection separately from the
  unchanged raw oracle method. Faces.cs owns the pinned-library detector,
  fault and retirement behavior, and anonymous track payload.
- VisionSystem.cs installs the stock detector, preserves a caller-supplied
  detector, and feeds detected rectangles into metering and tracked faces
  into the native world path. NativeMotionPoseState.cs shares the pose
  similarity comparisons for the tracking reset.
- OkaoFaceWireTests.cs uses the drawn native fixture to check the square,
  points, roll and temporary ID. It also drives the canonical camera
  pipeline through frame detection and metering rectangles and checks
  the source-derived IMU threshold/count cases. NativeMotionIntegrationTests.cs
  explicitly supplies an unavailable detector to retain the original
  unavailable-detector replacement test.
- Final affected tests passed 139/139, including the unchanged raw OKAO oracle, stock disposal and custom-detector replacement.
  The first 26/27 run exposed a fixture that assumed the default detector
  was unavailable.

## MISSING and records

The asynchronous recognition job, enrollment state and IDs, album ownership,
and its tracking reset are open. SetNext at 0x00865A1C, PT result swap at
0x0086DBA6, the 5 ms worker poll at 0x00862D70, and join-before-handle-deletion
at 0x00862852 identify the remaining owner chain. Enabled EX/SM/GB variants,
non-video detection configuration, the CO version prelude, CO square partial-error face publication, and reset with no prior admitted pose are explicit gaps.
The full engine-to-game face payload remains open. M14-010 and M14-011 remain
IMPLEMENTATION_GAP in the approved inventory. Only anonymous detection and the
checked consumers are proposed for Claude review.

Batch full gate on 4487b1fa failed (7801/7803, zero skipped). The old stock-detector availability fixture was corrected and its follow-up passed 61/61. The identical SolvePnP oracle mismatch also reproduces on origin/main a9dbec14. The batch remains below READY FOR CLAUDE.
## Gate correction and baseline evidence

The first gate failed FaceTests.TheStockFaceDetectorIsAnExplicitUnavailableBoundary
because the stock detector is now available with the local shipped libraries.
FaceTests.cs keeps the blank-frame, world, pet and behavior-readiness checks.
The unavailable-detector fallback is still exercised by NativeMotionIntegrationTests.
The focused correction run passed 61/61 including Acapela and OKAO oracles.

The other failure was NativeSolvePnPOracleTests.EveryOracleCaseMatchesTheShippedSolverBitForBit.
Both the batch and a clean archive of a9dbec14 on the same .NET 9.0.3 host
differ in wild_0 (133 ULP) and wild_15 (2,055,015 ULP). The solver and fixture
are identical to main. The prior solvepnp check records host math imports
but does not pin the generating host runtime or math library. Immediate
cause of the discrepancy remains unverified. The batch gate was run once
and the focused fixture correction was rerun afterward.