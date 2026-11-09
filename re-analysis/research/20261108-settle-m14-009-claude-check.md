# M14-009 face return eligibility and enrollment forwarding: built and settled EXACT_SOURCE (Claude, manager, 2026-11-08)

**Verdict: M14-009 settled EXACT_SOURCE.** Claude built FACE-RETURN-ORIGIN-1 itself (Codex idle), the last open gate.

## The native path, re-disassembled

- **ShouldReturnFace 0x004F55B8..0x004F55E2:** reject when entry+8 < time (`blo`); when the bool is 1, reject when
  entry+0 < 1; then `IsPoseInWorldOrigin(robot, entry+0xF4)` decides.
- **IsPoseInWorldOrigin:** ARM thunk 0x004A5C80 to 0x0051290C..0x00512916. It reads the PoseOriginList at robot+0x294,
  takes the current origin at +0x14, and tail-calls `pose.HasSameRootAs(currentOrigin)` through the veneer 0x008CAE2C to
  PLT 0x004A4C78. It compares roots only, not coordinates.
- **GetFaceIDsObservedSince 0x004F59EE..0x004F5A6E:** walks the map and applies the same three gates inline:
  - node+0x1C below ts rejects;
  - with the bool set, node+0x14 below 1 rejects;
  - `IsPoseInWorldOrigin(node+0x108)`.

  It inserts the key (node+0x10) into a `std::set<int>`, so the ids come out ascending.
- **HasAnyFaces 0x004F5A84..0x004F5ADE:** the same gates, returning 1 at the first entry that passes.
- **Enroll 0x004F5C9E..0x004F5CB2:** mode 4 for a nonzero id and -1 for zero, then a tail call into VisionComponent
  (robot+0x258). The forward ends at FaceRecognizer::SetAllowedEnrollments, which belongs to M14-011.

## The stack (this commit)

- **Gate overload:** `FaceWorld.ShouldReturnFace(entry, time, requireId, currentOrigin)` applies all three gates in
  the native order. The third is `PoseTreeNode.HasSameRoot(entry.NativeObservationOrigin, currentOrigin)`, the same
  test `GetNativeFace` already used.
- **Both queries use it:** `GetFaceIDsObservedSince` and `HasAnyFaces`. They take the robot from `NativeOwner`
  (wired in VisionSystem) and the current origin from `TryGetNativeOriginOwner`, under the same lock order as
  `GetNativeFace`. The ids come back ascending.
- **The bool is the engine's id test, not a name test.** The parameter was `namedOnly` and filtered on HasName, which
  the engine never does (0x004F5A24..0x004F5A2E). It is now `requireId`, with id ≥ 1. No production caller passed
  true. The FaceTests expectation that came from the old code was corrected.
- **Offline frames also anchor faces.** The offline frame path (tests and Cozmo.Conformance) now attaches the
  observation origin when a native robot exists, so the gate can be evaluated there. Its other gates are unchanged.
- **Inputs that can't occur in production:** a face with no native origin, or a robot with no current origin owner,
  reports MISSING and the face is not returned. The robot-less public overload keeps a MISSING. Production always
  has the robot: live faces come only through `AddOrUpdateNativeFace` with an origin.
- **Enroll:** production calls `VisionSystem.SetFaceEnrollmentMode` (NativeEnrollFaceBehavior.cs:235 and
  NativeEnrollmentTarget.cs:45), which selects 4 or -1 as the native does.

## Whole path

The record owns the three gates of the two queries and the enroll mode selection. SetAllowedEnrollments is M14-011's.
The callers' use of the ids belongs to the callers' records.

**A lead, not this record:** native GetFace (0x004F597E) and GetLastObservedFace (0x004F5AE2) also call
IsPoseInWorldOrigin. The stack's GetNativeFace has the gate; `GetLastObservedFace` does not, so it should be checked
under M14-008.

## Tests (not circular)

- `NativeBouncerFaceInputTests.PublicFaceQueriesReturnOnlyFacesInTheCurrentOrigin`: a face is not returned under a
  foreign root and is returned once that root is parented to the current origin; the timestamp gate; id 0 is dropped
  only with requireId.
- `FaceTests.M14_009_ShouldReturnFaceAndEnrollUseTheEnginesGates`.
- The face, Meet, enrollment, bouncer and cliff test classes: 772 of 772 pass.

## Progress

25 records settled (was 24).
