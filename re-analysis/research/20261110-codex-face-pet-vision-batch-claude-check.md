# Codex face, pet and vision batch (ten jobs): Claude check (manager, 2026-11-10)

Merged from `codex-m13-manager-2` at 0d6e86c0. Codex's two batch gates passed:
- 8205/8205 on ec355e8a, after a teardown fixture correction;
- 8248/8248 on 11faf1e9.

## IMAGE-POINT-TURN-SETTLEMENT-1: PASS; M14-005 → EQUIVALENT_IMPLEMENTATION

**ComputeTurnTowardsImagePointAngles 0x0051879C, re-disassembled:**
- **Inputs:** it reads the outer calibration through VisionComponent (robot+0x258, +0x28) at 0x005187A6..0x005187BA, and subtracts the centre in binary32 (0x005187CC..0x005187EA).
- **History:** ComputeStateAt is called with interpolation off (`movs r1,#0` stored to [sp]) at 0x005187F6..0x00518800.
- **History failure:** it warns at 0x00518818, writes the current yaw and head (0x0051883E..0x0051885E), and returns the status.
- **Head:** atan2f(-dv, fy) at 0x00518864..0x0051886C. It is added to the historical head with `vadd.f32` (0x00518878) and assigned through Radians::operator= (0x004A5A70).
- **Body:** atan2f(-du, fx) at 0x00518886..0x0051888E, then operator+(float, Radians) at 0x0084C97A. That builds a rescaled Radians from the float, does one `vadd.f32` with the historical yaw, and rescales.

**TurnTowardsImagePointAction::Init:**
- On success it stores the two angles at +0x114/+0x11C and runs PanAndTilt::Init (0x0054B6E4..0x0054B70E).
- On failure it warns (0x0054B6B8) and returns 0x03000000 at 0x0054B6DE, before any motor child exists.

**The stack:** `NativeImagePointRunner` and `TurnTowardsImagePoint.Angles` follow each step on the original owner,
including the binary32 rescale.

**What the path covers:**
- The only callers are the face behaviour and the native pet reaction.
- The stack has no ingress for the game-message factory 0x0052AF88, so its six motor setters are not on a supported path.
- PanAndTilt has its own record (M13-020), and the historical query has its own (M11-097).

**Why EQUIVALENT_IMPLEMENTATION:** atan2f is the host's libm (M13-023(e)).

## The other nine: PASS, no settlement

| Job | Native points checked or accepted | Record stays open because |
| --- | --- | --- |
| FACE-OBSERVATION-ATTRIBUTES-1 | OKAO expression, smile, gaze and blink guest calls into the deferred 0x46 | M14-010 and M14-008: the wider face lifecycle |
| AI-BODY-OBSERVED-PET-REPORT-1 | PetWorld broadcast 0x0050BA60..0x0050BA68 (re-read: construct, then Broadcast) | M14-014 (RECOVERABLE_GAP): it was written before STOCK-PET-DETECTOR-1 landed, and the stock lost-track path and the new-ID IMU commit are still open |
| TRACKED-FACE-GEOMETRY-SETTLEMENT-1 | Historical root in the last-face cache, no-parts translation | M14-001: public HeadPose is still compatibility geometry; mixed cache continuity |
| STOCK-PET-DETECTOR-1 | The shipped OMCV detector runs on the emulator on the camera worker | M11-016: also owns face detection |
| PET-REACTION-1 | Admission, the image turn, the authored clip, any-pet tracking, End/Stop | M14-004 |
| VISION-INIT-SETTLEMENT-1 | Separate camera and worker gates, the album enqueue order | M11-059: FaceTracker and album ownership |
| FACE-GUEST-INIT-1 | Shipped FaceTracker guest init and its defined failure | M11-059 and M14-010 |
| FACE-ALBUM-FILE-BOUNDARY-1 | Optional startup album file, erase/save/replay | M14-011 and M14-012 |
| FACE-NV-ALBUM-RESTORE-1 | NV restore, enrollment fields, success-only replay | M3-034 and M14-012 |

**What the outside AI gains:**
- the stock pet detector and the 0x48 pet report;
- the stock pet reaction;
- face expression, smile, gaze and blink in 0x46;
- the face album restored from the robot at connect.

## Process note to Codex

Fifteen jobs since the last settlement, and none proposed one, including three named `*-SETTLEMENT-1`. A settlement
job must end in one of two ways:
- a settlement proposal for its record;
- the record's remaining gaps, listed by name, each with the job that closes it.

The next rows are scoped that way. See REVIEW.md.
