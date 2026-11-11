# HANDOFF INITPOSE-OBSERVED-FLAG-1 (Claude worker, 2026-11-11)

**Verdict: no effect, so nothing was built.** The census is `.scratch/INITPOSE-OBSERVED-FLAG-1-census.md`. An independent
verifier re-checked the claim with a different method. The owning record is **M11-062**: its title names InitPose, its
evidence covers 0x004EF562..0x004EF644, and the stack field carries its tag. M11-013 gets a cross-reference.

## Evidence

**Writers:**
- **InitPose (0x004EF562):** the virtual `SetPose(pose, -1.0f, state)` (0x004EF576), then `strb 1, [this,#0x50]`
  (0x004EF578/0x004EF57A).
- **The queue row understated this:** Cozmo::ObservableObject::SetPose writes the same byte after its common body
  (0x004EF588..0x004EF58A). ActionableObject::SetPose (0x004E01E4..0x004E0226) tail-calls it.
- **Every constructor writes 0:**
  - at +0x50: CustomObject 0x004EF7C4, HumanHead 0x004F8586, MarkerlessObject 0x00502986;
  - at the complete-object offsets (vbase + 0x50): ActiveCube +0x178, Charger +0x154, Block +0xF4, Bridge +0xE4,
    FlatMat/Platform +0xDC, Ramp +0xE0.

**InitPose callers:** six, all through PLT 0x004ABE78:

| Address | Caller |
| --- | --- |
| 0x00559F2C | DriveToPlaceCarriedObjectAction::Init |
| 0x00603086 | BehaviorAcknowledgeObject |
| 0x0061E2AE | CreateFixedCustomObject |
| 0x00621860 | UpdatePoseOfStackedObjects |
| 0x00622430 | AddMarkerlessObject |
| 0x00625582 | CreateObjectsFromMarkers |

**Readers: none.**
- **The census** did a linear sweep of `.text`. Every other load at +0x50 belongs to another class.
- **The verifier**, independently:
  - decoded per symbol (28,402 FUNC symbols, Thumb/ARM by the low bit) and at every halfword;
  - covered all load forms at the field's offsets, plus add-then-load, register-index, ldm/vldm spans and
    address-taken;
  - checked every vtable slot of the object classes;
  - checked the clone path (fresh construction) and CopyWithNewPose (only SetPose).
- **The limit both state:** a reader through a computed offset or a member-pointer constant in data would not be seen.
  Neither found any sign of one.

So the byte touches none of "What we reproduce" 1–4.

## The stack

- `ObservableObject.PoseInitialized` (BlockWorld.cs) is written by InitPose and SetCarryingObservationPose, and not by
  SetPose. Like the engine's byte, it has no reader.
- It is left as is. No code changed.

## Records

| Record | Change |
| --- | --- |
| M11-062 | `unresolved` records the no-effect claim with the writers, callers, both scan methods and the limit. No settlement. |
| M11-013 | `unresolved` cross-reference. No settlement. |
