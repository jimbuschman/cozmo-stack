# Robot::Delocalize and BlockWorld::UpdateObjectOrigin (Opus extractor, 2026-10-13)

- **Run:** read-only cozmo-extractor, for B-M3M4-19's withdrawn item B2.
- **Adopted:** by the manager on 2026-10-13. This is the checked source for job B-M11-1.
- **Bodies read:** Delocalize 0x00510A24..0x00510DB4, and UpdateObjectOrigin 0x0062013C..0x00620420.

## Robot::Delocalize, in order

| # | Step | Address |
|---|---|---|
| D1 | robot+0x2B8 = −1; +0x2C4 = 0; +0x2C8 = −1.0f; +0x2C5 = 0 | 0x00510A42..0x00510A56 |
| D2 | ClearCliffRunningStats([+0x288]) | 0x00510A5A |
| D3 | **Save the OLD current origin ID** (PoseOriginList+0x10) | 0x00510A5E..0x00510A64 |
| D4 | AddNewOrigin: new ID = next; current = new; next = max(next, id+1) | 0x00510A66; 0x00847A84..0x00847B6C |
| D5 | sEventF "Robot.Delocalize" "Delocalizing robot %d. New origin: %s. NumOrigins=%zu" (robot ID, the new origin's name, the origin count) | 0x00510A7A..0x00510AA4 |
| D6/D7 | Robot poses +0x298 and +0x2A4 := identity (Rotation3d(0, Z), t = 0), parent = the new origin | 0x00510AD6..0x00510B44 |
| D8 | SetNewPose(+0x298) (SetPose + AddVisionOnlyStateToHistory). Non-zero → sWarningF "Robot.Delocalize.SetNewPose" "Failed to set new pose", then continue | 0x00510B48..0x00510B82 |
| D9 | Only if [+0x29]: Info "Unnamed" "Robot.Delocalize.SendingNewOriginID" "Sending new localization update at t=%u, with pose frame %u and origin ID=%u", then SendAbsLocalizationUpdate | 0x00510B86..0x00510BF4 |
| D10/D11 | VizManager SetText ("LocalizedTo: <nothing>", "WorldOrigin[%lu]: %s"); EraseAllVizObjects | 0x00510BF8..0x00510C4E |
| D12 | ObjectPoseConfirmer::Clear | 0x00510C52..0x00510C56 |
| D13 | If the passed isCarrying ≠ IsCarryingObject(): sWarningF "Robot.Delocalize.IsCarryingObjectMismatch" "Passed-in isCarryingObject=%c, IsCarryingObject()=%c" ('Y'/'N'). A warning only | 0x00510C5A..0x00510C98 |
| D14 | Only if a carried object exists ([[+0x284]+8] ≠ −1) | 0x00510CBE..0x00510CC6 |
| D15 | GetCarryingObjects = {the carried object, the object on top of it if any}, as a set ordered by signed ID | 0x00633BB0..0x00633BEC; 0x00507A34..0x00507A4A |
| D16 | For each, in set order: BlockWorld::UpdateObjectOrigin(id, **old** origin ID) | 0x00510CE8..0x00510CF0 |
| D17 | A non-zero return → sWarningF "Robot.Delocalize.UpdateObjectOriginFailed" "Object %d", and continue | 0x00510CF4..0x00510D2C |
| D19 | BlockWorld::OnRobotDelocalized(new ID): DeleteObjectsFromZombieOrigins, CreateLocalizedMemoryMap, the selection reset, then a **tail call to BroadcastLocatedObjectStates** (0x006249EC → veneer 0x008CCA6C → PLT 0x004B7E78) | 0x006249C4..0x006249EC |
| D20 | FaceWorld, AIComponent, BehaviorManager and MovementComponent OnRobotDelocalized, in that order | 0x00510D66..0x00510D80 |
| D21 | Broadcast E2G RobotDelocalized (tag 0x32, empty) if there is an external interface | 0x00510D84..0x00510D98 |

## BlockWorld::UpdateObjectOrigin(id, oldOrigin)

| # | Step | Address |
|---|---|---|
| U1/U2 | Look up the old origin's bucket. Missing → Info "BlockWorld" "BlockWorld.UpdateObjectOrigin.BadOrigin" "Origin %d not found"; return 1 | 0x00620146..0x0062025C |
| U3 | The old origin pose = GetOriginByID (a bad ID → the VERIFY and the default static pose) | 0x0062017C..0x0062018C; 0x00847C20..0x00847CD8 |
| U4/U5 | Search family → type → ID; the first hit wins. Not found → Info "BlockWorld.UpdateObjectOrigin.ObjectNotFound" "Object %d not found in origin %s"; return 1 | 0x0062018E..0x0062023E |
| U6 | **If HasSameRootAs(obj.pose, oldOrigin): return 0, with nothing moved** | 0x0062028A..0x006202A6 |
| U7/U8 | The family key is obj+0x48 and the type key obj+0x4C; newRoot = FindRoot(obj.pose). Info "BlockWorld.UpdateObjectOrigin.ObjectFound" "Updating ObjectID %d from origin %s to %s" | 0x006202AA..0x00620314 |
| U9 | Insert into bucket[newRoot.ID][family][type][id] = obj. **This overwrites any existing entry** | 0x0062033E..0x006203C8 |
| U10 | ObjectPoseConfirmer::AddInExistingPose(obj): emplace the key, and **always** set entry+0x18 = obj.pose | 0x006203CC..0x006203D6; 0x00506F48..0x00506F80 |
| U11 | Erase the ID from the old bucket, then erase each level that becomes empty: type, then family, then the origin | 0x006203DA..0x0062040A |
| U13 | Nothing else: no pose, parent or PoseState change, and no notification | the whole body |

**Why the move happens:** the carried object's pose chain is lift (+0x2F0) → pivot (+0x2E4) → robot pose (+0x298)
(0x0050FE9C, 0x0051000E). D6 re-parents +0x298 to the new origin, so FindRoot gives the new origin. The object on top is
parented to the carried one (M12-008 step 8). **This depends on the pose tree** (M11-066, job B-POSE-1).

## DeleteObjectsFromZombieOrigins

For each origin bucket:
- if it is the current origin, or AnyRemainingLocalizableObjects(id) == 1: Debug
  "BlockWorld.DeleteObjectsFromZombieOrigins.KeepingOrigin" "Origin %d is still good (keeping objects)";
- otherwise: Info "...DeletingOrigin" "Deleting objects from Origin %d because it was zombie", and erase the bucket.

(0x00620920..0x00620A04.)

## Corrections

- **M11-019's title says "located objects are forgotten". They are not:**
  - a delocalize keeps the old origin's bucket unless it is a zombie;
  - it writes no PoseState;
  - the objects simply stop being in the current origin;
  - the carried move also covers the object on top, and only happens when the root changed.
- **M13-032** omits OnRobotDelocalized's tail call to BroadcastLocatedObjectStates.
- **M12-008:** the lift pivot's parent is the robot pose (+0x298), not the origin.

## Unread

- Robot::SetPose 0x0051502C, and AddVisionOnlyStateToHistory 0x0051513C (in part M11-044's).
- AnyRemainingLocalizableObjects(uint) 0x00626EF4.
- The AddInExistingPose emplace's default fields (PLT 0x004A6FE8).
- The IExternalInterface vt+0x18 (0x00510D94).
- The VizManager colour argument (GOT 0x0103E7DC). Whether VizManager output is in scope is a question for the operator.
