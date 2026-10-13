# Opus check of R-DELOC-CALLEES (2026-10-13)

- **Checked:** `research/20261013-delocalize-callees.md` (Codex), against libcozmoEngine.so. Read-only cozmo-extractor;
  helpers are in `.scratch/deloc/`.
- **Adopted:** by the manager on 2026-10-13. This file is a checked source for job B-M11-1. Use the Codex report together
  with the corrections below.
- **Result:** all five items hold. There are two corrections, and one new path.

## The rows (as the Codex report, with the corrections)

- **A1–A10, AnyRemainingLocalizableObjects(uint)** (0x00626EF4):
  - **The filter:** a fresh filter (carried byte 0; mode 3; the allowed-origin set = {arg} unless arg == Unknown, in which
    case every origin) plus one predicate, obj vtbl+0x18 (CanBeUsedForLocalization);
  - **the search:** FindLocatedObjectHelper with returnFirst. A null entry logs the NullExistingObject warning and is
    skipped;
  - **the result:** the first object that passes → 1;
  - **the overload:** the no-argument overload (0x006270F4) passes Unknown, i.e. every origin;
  - **zombie deletion:** DeleteObjectsFromZombieOrigins keeps the current origin without asking, and keeps any other
    bucket only on 1.
  - **Correction:** IsRestingFlat's `<` is Radians operator<, i.e. `tol − angle > 0 && !IsNear(1e-5)` after the rescale
    (0x0084CC90..0x0084CCDA), not a plain float `<`. The primitive is M4-001's.
- **P1–P10, SetNewPose → SetPose → AddVisionOnlyStateToHistory:**
  - **SetNewPose and SetPose:**
    - GetWithRespectToRoot, with its error log;
    - the SetPose gate HasSameRootAs(world origin), with the VERIFY on failure;
    - the name kept across operator=;
    - MoveRobotPoseForward(d = −20.0 when not carrying, otherwise 0.0; x += d·cosf(yaw), y += d·sinf(yaw), z = 0).
  - **AddVisionOnlyStateToHistory:**
    - the wrapper sets +0x2C6 = 1 and +0x2B0++ first;
    - HistRobotState = Pose3d("") + GetDefaultRobotState (0x005182B0..0x005182E8: +0 = 1, +4 = 0, +8 = 1, +0xC..+0x47 =
      0, +0x48 = 5.0f, +0x4C = 0x300, +0x50/+0x54 = 0xFFFFFFFF, +0x58 = 0, +0x5A = 0xFF);
    - SetPose, then AddVisionOnlyState: the NonFlattenedPose and TooOld errors; an existing key → overwrite in place,
      return 0, no cull; otherwise emplace (EmplaceFailed → 1) and CullToWindowSize.
- **C1–C4, AddInExistingPose:** a new entry = Pose3d("") + zeroed words (+0x24..+0x3B). An existing entry is unchanged,
  apart from node+0x18 := obj.pose. *(The meanings of +0x34/+0x38 are UNKNOWN; their value is 0.)*
- **E1–E4, the RobotDelocalized broadcast:**
  - E2G tag 0x32, empty payload;
  - UiMessageHandler::Broadcast (0x00662508): **DeliverToGame first** (to the app connections), then an AnkiEvent and a
    **synchronous emit to the in-engine tag-50 subscribers**.
- **V1–V4, VizManager:**
  - SetText's colour is YELLOW;
  - **SendMessage and SendVizMessage are `bx lr`;**
  - no VizManager code reaches a transport (Connect only logs; it is reachable only through G2E RedirectViz).
  - **Not reproducing VizManager output is exact.** It is not a departure.

## New

- **E5, the in-engine RobotDelocalized subscribers:**
  - (a) BlockConfigurationManager (lambda 0x006172A6) sets +0xC = 1;
  - (b) CubeLightComponent HandleMessage<RobotDelocalized> (0x0063A390) sets +0x21 = 1, then, for each ObjectInfo entry
    in ObjectID order that is **not carried** (0x0063A3D0), calls PlayLightAnim(id, CubeAnimationTrigger 1 "Connected",
    layer 2, empty callback, false, zeroed ObjectLights, 0) (0x0063A3A6..0x0063A438).
- **E6:** the complete subscriber list and the order between (a) and (b) are UNKNOWN. The Subscribe(tag 0x32) sites,
  and the reachability of ReactionTriggerStrategyCubeMoved::HandleRobotDelocalized 0x0060C038, are unread.

## Contradicted

- **M5-042:** its SetText half ("not reproduced (viz out of scope)") is in fact exact: the original's SetText produces
  nothing. Its SetSdkStatus half wasn't checked.
- M3-032, M12-032 and M11-062 list VizManager calls as unbuilt or MISSING. They have no observable behaviour.
