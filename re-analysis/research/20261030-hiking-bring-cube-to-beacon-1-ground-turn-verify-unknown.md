# HIKING-BRING-CUBE-TO-BEACON-1 — Ground Turn verify flag

Read-only narrow response to the post-Ground NO_MARKERS diagnostic lead. Requested Sol/medium; backend unobserved. PROJECT_STATE read first; known.py before Ground00554870, Turn ctor,Check0054ADD4 and Init0054A1F0. Same verified native binary/SHA as prior Bring reports. No code/test/git/official edits.

**Conclusion: the existing literal Ground call `(Radians0,true,false)` is source-correct. First true directly enables the Turn visual-verification byte+0x180; second false clears track+0x192. Do not flip the literal true on the strength of M15-022's old prose.** Current M15-022 unresolved saying visual verification is clear/unreachable contradicts these concrete normal Ground operands. The current approved inventory row842 merely says “a TurnTowardsObject verify action” and does not prescribe false; its cited original extraction explicitly records true,false. Later checked Turn source also maps first bool→verify,second→track.

The initial builder allegation that the C# tuple contradicted source was withdrawn by the manager pending this mapping. This read confirms that withdrawal for the tuple: no implementation bug is established by this flag. A post-send0300001D alone does not identify the nested producer, so its scene/input/root cause remains unclassified pending an actual nested result trace. No success is inferred from tests and no failure expectation is weakened.

## Exact native call-to-field chain

| Step | Registers / stack / field / branch | Primary source |
|---|---|---|
| G1 Ground child inputs | Saved carried target ID comes from Ground+0x7C; selected marker short comes from actual Carrying+0xC; angle is Radians0. Ground sets r1=1, sb=0, then writes incoming Turn stack `[sp+0]=Radians pointer`, `[sp+4]=1`, `[sp+8]=0`. r3 is sign-extended actual marker. No bool inversion here. |0x0055483E marker;0x0055484C ID;0x00554856..5E Radians0/sb0;0x00554862 `movs r1,#1`;0x00554866 `stm.w sp,{r0,r1,sb}`;0x0055486A marker;0x00554870 six-arg ctor PLT0x004AB9D4 |
| G2 concrete constructor mapping | Six-arg Turn ctor pushes24 bytes then subtracts40 bytes: incoming stack offset4 is therefore current+0x44, incoming offset8 current+0x48. Loads first bool at+0x44 and stores it directly to+0x180. Loads second at+0x48 and stores directly to+0x192. Neither is inverted; +0x193 refine initialized1 is a third independent state field. |0x00549DC0 push6regs;0x00549DC4 sub0x28;0x00549DEE `ldr r3,[sp,#0x44]`;0x00549E00 `strb r3,[r4,#0x180]`;0x00549E26/28 second bool→+0x192;0x00549E2C refine1 |
| G3 normal real-ID Init | Turn+0x188 target value !=-1 takes actual located object lookup and, if found, proceeds directly to marker selection. It does NOT clear visual flag on this ordinary saved carried-ID path. Null located object is normal lookup failure. |0x0054A202..20E ID sentinel test;0x0054A210..226 lookup;success falls through0x0054A22A |
| G4 only relevant clearing branch | ID=-1 custom-object path requires a pre-supplied custom object pointer and same world origin. There, if+0x180 nonzero, diagnostic then clear+0x180. This is not normal Ground saved real cube ID. Blanket clearing at Ground construction would silently substitute this custom-only branch. |0x0054A20E→0x0054A300;0x0054A300..31A custom pointer/root gate;0x0054A31E..324 flag gate;0x0054A364/366 zero flag;0x0054A36A returns marker selection |
| G5 runtime verification gate | Existing checked Turn core turns/refines first; after finishing/refinement, checks+0x180. Zero skips verifier; nonzero constructs VisuallyVerifyObjectAction with actual stored target/marker. Thus Ground normal true reaches real verifier after its turn/refine sequence. |0x0054AE32..B8 refine;0x0054AECE `ldrb+0x180`;0x0054AED4 zero branch;0x0054AED8..FC construct verifier;0x0054AF14..1E suppress child+0x56 and immediate Update |
| G6 track independence | After verifier result processing, +0x192 controls TrackObject follow-on. Ground's second boolfalse prevents it. This does not disable visual verifier. |0x0054ADF2..F8 tracks read/gate;0x0054AE08 allocation;ctor second bool G2 |
| G7 suppression independence | Ground subsequently sets child+0x56=1 (SuppressTrackLocking), separate from verify+0x180. This is not a visual-verify flag or inversion of the constructor bool. |0x00554888 `movs r0,#1`;0x0055488A `strb r0,[r6,#0x56]`;shared runner L1 constructor-byte layout |

## Reuse and classification

Reuse `20261003-R-FIX2-M15-gap1-extraction.md`2.6: it explicitly says Turn(robot,ID,carryingMarker,Radians0,true,false). Its contemporaneous note says the Turn body was then a RECOVERABLE_GAP, so that report did not establish first true means disabled. Later `20261027-pickup-pyramid-2-read.md` P4 says180=caller verify,192=caller track;P6 checks actual visual child; `20261007-child-actions-rows.md` O0/O2 does likewise. These checked contracts are consistent with G1–G7; no full Ground/Turn rewrite or geometry extraction is needed.

Existing C# `NativePlaceGroundRunner.Init` constructs `(actualSavedId,actualCarryMarker,0f,true,false)` and suppresses child track locking. `NativeTurnTowardsObjectRunner` stores bools as `_verify` and `_track`. Those literal constructor semantics match native for this normal Ground input. This read does not certify the runner's entire Init/Check implementation or the unidentified0300001D source.

**Official annotation issue:** current M15-022 unresolved says “with the visual-verify byte clear” and “its unreachable VisuallyVerifyObjectAction branch (0x0054AED8)”. On normal Ground real-ID child, G1/G2 establish the byte is set and G5 establishes the branch is reachable. This is a current recorded-scope annotation contradiction to send to Claude; do not silently alter the official record or force code to match contradicted wording. No new operator departure is requested. Current record is IMPLEMENTATION_GAP, not settled EXACT_SOURCE.

## Literal current claim and inventory quote

Approved `re-analysis/inventory/M15-freeplay.md:842`:

> **M15-022** PlaceObjectOnGroundAtPoseAction composition (0x005DFF14) and PlaceObjectOnGroundAction Init/CheckIfDone (message 0x44, a TurnTowardsObject verify action, StopAllMotors). M12-015's evidence covers only the wire message.

Current manifest title/status/evidence/unresolved (read before above contradiction claim):

```json
{
  "id": "M15-022",
  "title": "PlaceObjectOnGroundAtPoseAction composition and PlaceObjectOnGroundAction Init/CheckIfDone",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "PlaceObjectOnGroundAction Init sends message 0x44 (0, 0, 0, 100, 200, 500 as raw bits), builds a TurnTowardsObject verify action and calls StopAllMotors; CheckIfDone never reads DockingComponent+5, its result is the verify action's (extraction sections 2.3..2.8; construction of PlaceObjectOnGroundAtPoseAction at 0x005DFF14)",
    "M12-015 owns only the wire message of PlaceObjectOnGround (its evidence does not cover the action)",
    "extraction report re-analysis/research/20261003-R-FIX2-M15-gap1-extraction.md"
  ],
  "unresolved": "built, awaiting strong verification (R-FIX2, 2026-10-04): PlaceObjectOnGroundAction as the engine's one class: Init (0x00554794) sends message 0x44 then builds the verify action, StopAllMotors, the reaction lock 'placeOnGroundAction' (table 0x00C55A21, only trigger 8 set) removed in the destructor (0x005546D0) when the state is not NOT_STARTED; CheckIfDone (0x005549B0) gates on the IS_PICKING_OR_PLACING latch, never reads DockingComponent+5, the result is the verify action's; the 30.0f timeout slot (0x0052B0C2); the verify child is TurnTowardsObjectAction built from Init 0x0054A1F0 and CheckIfDone 0x0054ADD4 with the visual-verify byte clear (the marker choice with vsqrt.f32; the head-angle fallback GetAbsoluteHeadAngleToLookAtPose 0x0054B428 and ComputeHeadAngleToSeePose 0x00518344 built, confirmed by a Unicorn run of the shipped instructions; the head-cam z used there is the engine's -8.0: see M11-054); every caller takes the lock through ManipulationSystem.ReactionLocks when a freeplay stack exists. Still open: TurnTowardsObjectAction's TrackObjectAction branch (+0x192 set, 0x0054AE08) and its unreachable VisuallyVerifyObjectAction branch (0x0054AED8) are unbuilt, the single RUNNING tick (0x0054AE40..0x0054AEB8) is not modelled, a marker whose with-respect-to fails (0x03000005) is not coded; ComputeHeadAngleToSeePose's pose algebra is the stack's double Pose3d narrowed to f32; M12-015 owns only the wire message; whether the firmware raises IS_PICKING_OR_PLACING for message 0x44, and its order against PickAndPlaceResult, is HARDWARE_ONLY; callers without a manager (ManipTool, Motion with no stack) do not take the lock (reported MISSING)."
}
```

## Approval/hash context and permission consequence

`M15-freeplay.approved.json` records approval2026-10-29 and inventory SHA256 `fcd16c422144ff55a6cf19315fbcf9bccbf817cf2d99d78aef5571004e126a36`. The current inventory file's independently calculated SHA256 is exactly the same. The current manifest subsystem review is INVENTORY_APPROVED, approved2026-10-29, pointing to that snapshot. This read did not run a checker or modify a snapshot.

Frozen M15-022 evidence is reproduced below. It points to the original extraction, whose2.6 literal call is true,false; the approved inventory row itself does not say normal Ground disables verification. The contrary phrase lives in the current mutable `unresolved` annotation, not an explicit frozen requirement to passfalse. Therefore retaining the native/source-backed true,false code is already within the existing approved tuple contract: **no new operator behavioral departure or changed constructor contract is needed**. Claude can reconcile the obsolete unresolved annotations through the normal record-maintenance process. This is not permission to alter frozen evidence/status or to change code based on the unlocalized0300001D observation.

Frozen approved M15-022 fields:

```json
{
  "status": "IMPLEMENTATION_GAP",
  "title": "PlaceObjectOnGroundAtPoseAction composition and PlaceObjectOnGroundAction Init/CheckIfDone",
  "authority": "libcozmoEngine.so 3.4.0-1204",
  "evidence": [
    "PlaceObjectOnGroundAction Init sends message 0x44 (0, 0, 0, 100, 200, 500 as raw bits), builds a TurnTowardsObject verify action and calls StopAllMotors; CheckIfDone never reads DockingComponent+5, its result is the verify action's (extraction sections 2.3..2.8; construction of PlaceObjectOnGroundAtPoseAction at 0x005DFF14)",
    "M12-015 owns only the wire message of PlaceObjectOnGround (its evidence does not cover the action)",
    "extraction report re-analysis/research/20261003-R-FIX2-M15-gap1-extraction.md"
  ],
  "live_path": true,
  "hardware_required": false
}
```
