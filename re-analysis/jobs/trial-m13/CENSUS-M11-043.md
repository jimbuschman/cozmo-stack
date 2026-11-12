# M11-043 settlement census (manager's extractor, 2026-11-12)

BlockWorld::UpdateObjectOrigins 0x00620534..0x006207E4 and lambda1 0x00628E18..0x006293F6, re-read in the `.so`.
"BW" is `cozmo-stack/src/Cozmo.Robot/Vision/BlockWorld.cs`.

## The verdict

**Not settleable yet.** Every reachable step is built and all but one match. The one that differs is L2.

**L2, the carried test.** The engine calls CarryingComponent::IsCarryingObject 0x00633F88 (0x00628E52..0x00628E64).
That function is true for the carried ID [+8] **or the ID resting on it [+0x14]**.

The stack's shared `BlockWorld.IsCarryingObject` binding tests only the carried ID:
- the binding is Docking.cs:227-228 → `Carrying.IsCarrying` (Docking.cs:89);
- Docking.cs:93 `IsCarryingObjectId` has the engine's body but is not bound.

So in a carried-stack origin move, the stack converts the top cube to the new origin where the engine copies its pose
as is, parent kept.

The same PLT 0x004A6FAC has other callers:
- HandleActiveObjectMoved 0x00533E30;
- MarkObjectDirty 0x005075A4 and MarkObjectUnknown 0x00507128;
- UpdatePoseInInstance 0x00505DE0;
- AddAndUpdateObjects and UpdatePoseOfStackedObjects;
- CheckForCollisionWithRobot;
- CycleSelectedObject;
- DriveToObjectAction::InitHelper.

So the rebinding needs a per-caller census (job CARRIED-ONTOP-BINDING-CENSUS-1). What happens to the top cube after
the old carried cube is erased (0x0062064C) depends on M11-084 A8-15 (NotLastOwner).

**Test gap:** no production test asserts the tail of UpdateObjectOrigins. The tail is BroadcastLocatedObjectStates
(E2G 0x53, 0x006206E6 → 0x0061E6C0) and then the BlockConfigurationManager force flag (0x006206EA..0x006206F0). Both
are built: the 0x53 since commit 0d22601b, and the flag through NativeConfigurationManager.ForceUpdate. The test is
job LCO-TAIL-PRODUCTION-TEST-1.

## Rows that match

| Row | What | Address | Verdict |
| --- | --- | --- | --- |
| U2 | Origin lookups | — | match |
| U3 | Confirmer clear | 0x0062058E → 0x005077EC | match |
| U4 | First walk, mode 3 | — | match |
| L1, L3–L9 | Pose copy or GetWithRespectTo; OriginFail; the counterpart filters; unique search; closest search with the MarkerlessObject tolerance 0x00502B06 | — | match |
| L11 | Re-key | — | match |
| L12–L15 | Clone, SetObservationTimes 0x00877706, CopyWithNewPose 0x00506EF8, AddLocatedObject | — | match |
| U5 | Old bucket erased only if the result word is 0 | — | match |
| U6 | AddInExistingPose walk | 0x00629862 → 0x00506F48 | match |
| U7 | 0x53 | — | match |
| U8 | Force flag | — | match |
| U9 | Return, ignored by the caller | — | match |

## Out of scope, to record

- **U1, the UnknownOriginID guard (0x00620554..0x00620570):** a branch our system never produces. The old ID is read
  before Rejigger, the new one after a successful Rejigger that requires ContainsOriginID, and IDs start at 1
  (0x00847934).
- **L9, the throw for a non-unique, non-markerless object:** reachable only through SDK custom objects (DefineCustom*
  0x0061E4A8/0x0061E5C4/0x0061E63C, CreateFixedCustomObject). Ramp and MatPiece are never constructed (library fill
  0x0061BDD8..0x0061BE6E). Record it as "not supported: SDK mode (custom objects), operator 2026-10-09".
- **L10, L11, L12, L15, U1, the log formats:** best effort, never blocking.

## Stale notes to fix

- **M11-043's OLD note:** "BroadcastLocatedObjectStates (no message type); force hook not wired". Both are built.
- **M11-038's first `unresolved` line:** "NOT BUILT: … no EngineToGame message types … for LocatedObjectStates (0x53),
  ConnectedObjectStates (0x54)". Built since 0d22601b, but with no `// fidelity: M11-038` tags in
  ObjectStateReports.cs / NativeObjectStateReports.cs.

## Inventory correction, needed when M11-043 settles

**Evidence[0]:** "0x00620534..0x006207E4 UpdateObjectOrigins (guard 0x00620554..0x00620570; Clear 0x00620594 ->
0x005077EC; first walk 0x0062063E; erase 0x00620642..0x0062064C; AddInExistingPose walk 0x006206E0 via 0x00629862 ->
0x00506F48; BroadcastLocatedObjectStates 0x006206E6 -> 0x0061E6C0; BlockConfigurationManager [+0xC] := 1
0x006206EA..0x006206F0); lambda1 0x00628E18..0x006293F6 (IsCarryingObject 0x00628E60 -> 0x00633F88; GetWithRespectTo
0x00628E82; OriginFail 0x00628FB2..0x00629026; unique search 0x00628F5E..0x0062904A; closest search
0x00628F8E..0x00628FAC -> 0x0061F8EC/0x00627F9A, MarkerlessObject tolerance 0x00502B06; re-key 0x00629162..0x006291E8;
SetObservationTimes 0x006292DC -> 0x00877706; CopyWithNewPose 0x006292EE -> 0x00506EF8; AddLocatedObject 0x0062931E);
call site 0x00515992".

**Authority, three changes:**
1. **Carried test.** After "runs a modify function (0x00628E18) on every object in the old origin", add: "that keeps
   the pose of a carried object as is (CarryingComponent::IsCarryingObject 0x00633F88: the carried ID or the ID resting
   on it) and converts any other with GetWithRespectTo to the new origin (a failure is the OriginFail error, result 1,
   object skipped), and".
2. **CopyWithNewPose.** Replace "(copies the pose and the source's PoseState)" with "(SetPose with the source's
   fromDistance and PoseState, then the confirmer entry; returns 0)".
3. **The tail order.** Replace "then BlockConfigurationManager [+0xC] = 1 (0x006206EA) and BroadcastLocatedObjectStates
   (0x006206E6)" with "then AddInExistingPose over the robot's current origin (0x006206E0), BroadcastLocatedObjectStates
   (0x006206E6), then BlockConfigurationManager [+0xC] := 1 (0x006206EA..0x006206F0)".
