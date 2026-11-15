# Worker jobs 26–29 merged (manager, 2026-11-15)

Worker tip 249c09ce. Gates recorded by the worker: job 26 8403/8403, job 28 8414/8414; jobs 27 and 29 are records only.

| Job | Verdict | Re-read / notes |
| --- | --- | --- |
| CARRY-TREE-FLAG-RESET-1 (26) | PASS | A placed once-carried cube now clears the stack-only NativeCarryingTree flag exactly when its native parent becomes a root (placement through AddRobotRelativeObservation; re-read 0x00506B82..0x00506BA2: GetPose `bl 0x4EA398`, then GetWithRespectToRoot `bl 0x4DF628`). Localization to a placed cube is no longer refused. Unit-reach tests with a negative control. |
| M11-038-SCOPE-CENSUS-1 (27) | PASS | The 0x53/0x54/0x44/0x52 builders and Packs match; B-M1-7's "PoseStateChanged host split at 0x0061D5C8" is contradicted (it is BroadcastObjectPoseChanged in DeleteLocatedObjects). Two gaps proposed (below). |
| CAN-INTERACT-ROOT-CHECK-1 (28) | PASS | CanInteract now checks the object's root against the robot's before the on-top find (0x0063C6A2..0x0063C6BE, re-read 2026-11-14) and returns false instead of throwing; FindFreeCubeToStackOn and FindUsableCubesOutOfBeacons walk only the current origin (the filters' mode bytes are zeroed at 0x005E0188 and 0x0056AF64). A real defect: a flat cube left in an old origin could throw. |
| FOOTPRINT-GROUP-SETTLEMENT-READINESS-1 (29) | PASS | `re-analysis/jobs/trial-m13/SETTLEMENT-READINESS-FOOTPRINT.md` ranks M13-023, M13-024, M11-105, M13-025, M13-007, M12-012 with seven proposed jobs. |

No record settled; no status changed.

## Next (from the readiness list)

M13-023 is closest ("only decision records, two ICs and a Claude check"), then M13-024 (one unread body: convexHull and
the incrementing Sklansky_). The proposed jobs stay PROPOSED in QUEUE.md until released.

## Questions carried to the operator

- M11-038 Q1: the engine sends an observed-object report (0x44) for the charger whenever it is seen; the stack sends
  none (CHARGER-OBSERVED-OBJECT-REPORT-1). In scope now?
- M11-038 Q2: do the stack-only BlockWorld.ObjectObserved / PoseStateChanged events stay as C# API?
- HOST-CONFIG-SAME-ROOT-1 (unchanged).
