# HANDOFF DRIVE-TO-OBJECT-MANUAL-SPEED-1 (Claude worker, 2026-11-12)

**Verdict: no effect, so nothing was built.** The census is the worker's local
`.scratch/DRIVE-TO-OBJECT-MANUAL-SPEED-1-census.md`; its summary is below. Following the narrow-check rule, it stopped
once every reachable source was known.

## Evidence

**Where the value lands.** DriveToObjectAction stores useManualSpeed at +0x8C:
- the 7-arg ctor reads it at 0x00558540 and stores it at 0x005585A8. It also sets its own track mask to `manual ? 0 : 4`
  (0x00558544..0x0055854C);
- the 4-arg ctor does the same at 0x005587EC..0x005587F8 and 0x00558854;
- the only reader is InitHelper at 0x005592F6, which passes it to the inner DriveToPoseAction (D4, already built).

**Who passes it.** Every caller was scanned: the PLT stubs and direct calls; no dynamic relocation names the ctors.
Every source the stack runs passes a constant 0:

| Engine site | Stack |
| --- | --- |
| DriveToHelper 0x005B5886 | NativeDriveToHelper.cs:39 |
| RollBlockHelper 0x005B9F44 | NativeDriveToRollRunner |
| BehaviorPopAWheelie 0x005C7846 | NativeDriveToPopAWheelieRunner |
| BehaviorKnockOverCubes 0x005C3532 | NativeDriveAndFlipRunner |
| BringCubeToBeacon pickup 0x005DF97A | NativeDriveToPickupBeaconRunner |
| BringCubeToBeacon TryToStackOn 0x005DFDFE | NativeDriveToPlaceOnBeaconRunner |
| TryToPlaceAt 0x005DFF06 → PlaceObjectOnGroundAtPose 0x00554B36 → DriveToPlaceCarried 0x00559CF6 | NativePlaceGroundAtBeaconRunner |

- FeedingEat (0x005D5ACE, not built) and DockingTestSimple (excluded, M7-022) also pass 0.
- **A true value can come only from the app/SDK RobotActionUnion factories:** GotoObject 0x0052A0EE, PickupObject
  0x0052A392, PlaceRelObject 0x0052A6B8, TraverseObject 0x0052ADF6, PlaceObjectOnGroundAtPose 0x0052A4FE, and five more.
  They copy a message byte, and SDK mode is not supported (operator, 2026-10-09).

**So on every reachable path** the engine's mask is 4 and +0x8C is false. `NativeDriveToObjectRunner`'s `base(m,0x0A,4)`
and `new(m,id,type)` already produce exactly that. The stack's constructor has no manual argument, but that shape
difference is not observable.

## What changed

- **M12-022 `unresolved`:**
  - the no-effect record, with the sites above;
  - the stale item (b) ("+0x8C … not plumbed") and the "other callers not checked" note are marked superseded.
- **Code:** none.
- **Verifier:** no separate round. Nothing was built, and the claim is a caller scan the census lists site by site.

## Records

**M12-022:** no settlement. What still blocks it is in its `unresolved`. The legacy flow's contradictions go with
RETIRE-LEGACY-DRIVE-API-1.
