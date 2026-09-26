# M11 gap analysis

## Outcome

The existing vision stack is a useful behavioral prototype, but it is not ready to be accepted as a source-faithful M11 implementation. The largest risk is not a missing constant. It is that source-backed pieces are joined by locally authored frame scheduling, state association, pose solving, object identity, and lifecycle rules. That is precisely the failure mode warned about in `AGENTS.md`.

## Highest-risk gaps

| Priority | Gap | Observable consequence | Required disposition |
|---:|---|---|---|
| 1 | Managed planar homography/LM replaces native OpenCV `solvePnP`. | Different poses, failure cases, and downstream object association near perspective/distortion limits. | Recover exact flags/input order/initial-guess behavior; port or bind an equivalent solver and validate against native golden vectors. |
| 1 | State history chooses a nearest entry instead of native interpolation. | Camera pose and motion gates are wrong between robot-state samples; every detector result can be spatially displaced. | Implement only after tracing `GetRawStateAt`/`ComputeStateAt` completely. |
| 1 | Frame admission and mode scheduling are local. | Frames can run without native gates; expensive modes run at the wrong cadence; result order differs. | Reproduce native admission, scheduling, and mailbox behavior as the first implementation batch. |
| 1 | First marker observation immediately becomes `Known`. | Objects become actionable one frame earlier than the native pose confirmer permits. | Port the full confirmer state machine and non-circular tests. |
| 1 | Lift occluder is absent from the live visibility path. | A marker hidden by the lift can be counted as missed and an object dirtied/forgotten incorrectly. | Recover lift geometry insertion and call order. |
| 2 | Marker front-end pixel algorithms are local reimplementations. | Border cases, capacity, and duplicate candidates can diverge despite decoder correctness. | Use native-oracle image fixtures, then port each stage or retain only proven equivalence. |
| 2 | Robot pitch is omitted from camera/world pose. | Projection and observation pose drift on ramps, pickup, or non-level surfaces. | Trace and reproduce native pose composition. |
| 2 | Connected-object IDs/association and disconnect effects are local. | Marker observations can attach to the wrong cube or survive disconnect differently. | Close M11-028 before BlockWorld implementation. |
| 2 | Charger geometry and ID policy contain inferred/local values. | Charger observations/platform clearing can disagree with control/navigation. | Close M11-027; coordinate with M4 charger-platform residual. |
| 2 | Delocalization behavior is reconstructed rather than fully traced. | Objects/maps may persist, transform, or disappear incorrectly across origins. | Port `OnRobotDelocalized` only after enumerating every branch. |
| 3 | Overhead edges are not enabled by the vision system's shipped schedule. | Map obstacles/clear areas depend on freeplay glue rather than the native vision lifecycle. | Integrate in the scheduling batch; retain equivalent detector pending golden tests. |
| 3 | Native motion and image-quality/exposure paths are absent. | Behavior inputs and exposure feedback do not exist. | Preserve as M11 implementation batches, not `BLOCKED_EXTERNAL`. |
| 3 | Tool-code/laser/calibration/statistics modes are unclassified. | Silent scope loss and false “M11 complete” claims. | Trace or establish an explicit later-layer/tool boundary before approval. |

## Unsupported local policies found

- `QuadDetector.Parameters.MaxQuads = 512` and local duplicate suppression.
- `BlockWorld.MaxReprojectionRmsPx = 3.0` and the doubled multi-marker threshold.
- Fixed charger object ID 100 and markerless IDs beginning at 1000.
- Timestamp-zero frames paired with the latest state even though the engine's bad-timestamp behavior differs.
- Local 2-second history retention and 66 ms association windows.
- `Task.Run` plus a managed busy flag as a stand-in for the native worker/result mailbox.
- A fixed detector subset, with externally installed overhead detection, instead of config-driven schedules.

These may eventually become `COMPATIBILITY_POLICY`, but they must not be left inside a path claimed `EXACT_SOURCE`.

## Circular or weak tests

- Synthetic marker images rendered from the same extracted library validate internal consistency, not fidelity to native decoding.
- Pose tests project with the managed camera model and solve with the managed implementation; they do not detect a shared convention error or native `solvePnP` difference.
- Constant-only tests show that a value was copied, not that the production caller uses it in native order.
- World tests often insert already-known poses or use local object IDs, bypassing native association and confirmation.
- Overhead-edge tests exercise handcrafted images and managed mapping without a native output oracle.

Keep these tests for regression value, but add primary-source or original-app/native-oracle fixtures before settling equivalence.

## Recoverable work still open

The research did not exhaust three available native paths: charger construction/identity (M11-027), cube connection lifecycle (M11-028), and non-marker tool/laser/calibration/statistics modes (M11-031). They are `RECOVERABLE_GAP`, not unknown guesses and not implementation tasks yet. The extractor must close them or document why a narrower subsystem boundary is source-supported.

## External and hardware boundaries

- Face and pet classifier internals remain `BLOCKED_EXTERNAL` at OKAO. Scheduling, frame inputs, and result forwarding remain recoverable and testable.
- Pixel-exact camera behavior on a physical robot, calibration quality, rolling-shutter behavior, and final projection tolerance need hardware confirmation. Hardware success cannot upgrade provenance.
- Motion detection is not an external boundary; its native implementation is present.

## Recommended implementation batches after approval

1. **Frame truth:** M11-021, 022, 032, 033, 035. Admission, interpolated state, schedules, worker/mailbox, and result ordering.
2. **Marker front end:** M11-001, 002, 005, 018, 020, 023, 026. Preserve the library/decoder while replacing or proving local image stages.
3. **Geometry and pose:** M11-003, 024, 025, 027. Camera composition, distortion, native PnP behavior, charger geometry.
4. **World lifecycle:** M11-004, 006–010, 019, 028. Association, confirmer, misses/occlusion, radio lifecycle, delocalization.
5. **Ground/map integration:** M11-017, 034. Scheduled overhead detection, memory-map writes, charger-platform clearing.
6. **Remaining native modes:** M11-029–031. Motion, quality/exposure, and explicitly resolved tool/laser/calibration/statistics scope.

Each batch uses the extractor → approved inventory → implementer → verifier separation. A behavioral correction triggers verification only of that affected diff; the full suite runs once immediately before the eventual implementation commit.
