# BlockWorld+0x74 and the current map helper (Opus extractor, 2026-10-13)

- **Run:** read-only cozmo-extractor, on M11-063 and M13-031.
- **Adopted:** by the manager on 2026-10-13. This is a checked source for job B-M3M4-16.
- **Tool note:** `m5/da.py` stops at the first undecodable halfword, so a long range can end silently. That is why the
  earlier note found "no strb to +0x74". For long sweeps, use capstone with skipdata.

## Q1: BlockWorld+0x74 (the pose-change queue flag)

1. **The constructor** sets +0x74 = 0 (0x0061BD8A, `strb.w r0,[sl,#0x28]` with sl = this+0x4C). The PoseChange list is
   at +0x68/+0x6C, with its size at +0x70 = 0 (0x0061BD7C/0x0061BD86).
2. **UpdateObservedMarkers:**
   - it first runs the play-area event (row 9);
   - it then clears the list (0x00624F78) and sets +0x74 = 1 (0x00624F7E);
   - with the flag at 1 it runs CreateObjectsFromMarkers (0x00624FC4), AddAndUpdateObjects (0x0062505A),
     CheckForUnobservedObjects (0x006250CA, or 0x0062504E with no markers), and UpdatePoseOfStackedObjects (0x006250D0,
     marker path only);
   - then it clears the flag (0x006250D4..0x006250D8);
   - BlockConfigurationManager::Update (0x0062520C) and UpdateMarkerlessObjects (0x0062521A) run with the flag at 0.
3. **The error returns leave the flag at 1** (0x006250B6..0x006250C2 → 0x00625220). They skip the clear and the whole
   tail.
   - CreateObjectsFromMarkersFailed (0x00625338) is dead code: the function only returns 0 (0x00625616).
   - AddAndUpdateFailed (0x00BF9269) fires when PotentialObjectsForLocalizingTo::LocalizeRobot returns non-zero (0x00621304
     → 0x00621320), on a Robot::LocalizeToObject failure. Whether that is reachable is the new M11 RECOVERABLE_GAP.
4. **The reader, OnObjectPoseChanged** (0x00624814 `ldrb [r5,#0x74]`):
   - **Flag 0:** SetObjectPoseChanged only (0x00624888..0x00624892).
   - **Flag 1, ID already queued** (node+0xC vs obj+0x18, 0x0062482A..0x00624836): sChanneledInfoF, channel
     "BlockWorld", `BlockWorld.OnObjectPoseChanged.MultipleChanges` (0x006248EC), format "Object '%d' is changing its
     pose again this tick. Ignoring second change" (0x00BF8F50). **Not pushed.**
   - **Flag 1, old state 0:** `BlockWorld.OnObjectPoseChanged.FirstPoseForObject` (0x00BF8F99), format "Object '%d' is
     setting its first pose. Not queueing change." (0x00624924). **Not pushed.**
   - **Otherwise:** emplace_back(id, *oldPose, oldState) (0x00624884).

   Every path ends with BlockConfigurationManager::SetObjectPoseChanged(id, obj+0x24) (0x00624892).
5. **The consumer, UpdatePoseOfStackedObjects** (0x00621794..0x00621AF4). It walks the list from +0x6C to the
   sentinel, and reads `next` after each entry, so entries appended during the walk are processed too. For each entry:
   1. GetLocatedObjectByIdHelper(id, −1). A miss logs sChanneledInfoF "BlockWorld" `BlockWorld.UpdateStacks`
      (0x00BF8819), "'%d' does not exist in current frame. Ignoring change." (0x00621C20).
   2. Clone it (vt+0), then InitPose(clone, oldPose, oldState) (0x00621860).
   3. Build the filter:
      - ignored IDs = {id} (0x006218BC);
      - ignored families {6, 7} (0x006218C0..0x006218DC);
      - a predicate, `obj+0x1C + 100 < lastImageTimeStamp` (unsigned; vtable 0x0102DF9C, operator() 0x00629BAE).
   4. FindObjectOnTopOrUnderneathHelper(clone, 15.0f, filter, true) (0x00621902..0x0062190C).
   5. If an object is found, isn't carried (0x00621926), and isn't itself queued (0x00621930..0x0062194C): get the top
      pose relative to the old pose (0x0062196E). On failure, sWarningF `BlockWorld.UpdateStacks.OriginMismatch`
      (0x00621BDC) / "Can't obtain topPose wrt old, but that's exactly how we found the object in topPose."
      (0x00BF87C4).
   6. Otherwise PreComposeWith the current pose, SetParent, then ObjectPoseConfirmer::AddObjectRelativeObservation(top,
      pose, obj) (0x006219C0). On failure, sWarningF
      `BlockWorld.UpdateRotationOfObjectsStackedOn.AddRelativeObservationFailed` (0x00BF877B) / "Giving up on rest of
      stack" (0x00621C04), and the loop still continues.
   7. Delete the clone (0x00621A66).

   The list is not cleared here.
6. **A cliff in normal running:**
   - HandleCliffEvent (the only AddCliff caller, 0x005355C0) runs from robot message processing at 0x004ED62E;
   - that comes before UpdateAllRobots at 0x004ED648, which leads to UpdateObservedMarkers, on the same thread. So the
     flag is 0;
   - AddLocatedObject calls BroadcastObjectPoseChanged(obj, null, 0) (0x00622E4A..0x00622E4E).

   So the cliff logs nothing and is not queued. Even with the flag stuck at 1, it would log FirstPoseForObject, and
   still not be queued. How ProcessMessages (0x0069D85C) dispatches to HandleCliffEvent is unread; only the same-thread
   ordering is shown.
7. **The play-area event** (0x00624F16..0x00624F32):
   - it fires if now > [this+4] + [this+8]. The constructor sets +4 = 0 and +8 = 60.0f (0x0061BD20..0x0061BD3A);
   - it stores now in +4;
   - it calls GetCurrentMemoryMapHelper(), then vt+0x1C on the result with no null check;
   - it emits sEventF `robot.play_area_size` (0x00625314), "%.2f" (0x0062532C), with the result as a double.

   The body of vt+0x1C is unread.

## Q2: GetCurrentMemoryMapHelper is never null with a robot

1. **The helper** (0x0067EA5C..0x0067EA88) returns null if +0x1C == UnknownOriginID (0, at 0x00C97B20). Otherwise it
   does find(+0x10, +0x1C), returning null at end, else node+0x14.
2. **The MapComponent constructor** leaves the map empty, with +0x1C = Unknown, +0x38 = 1, +0x3C = −1.0f and +0x40 = 0
   (0x0067D9A4..0x0067D9CA).
3. **CreateLocalizedMemoryMap(origin)** (0x0067E85C..0x0067E9B4):
   - **First,** for each existing map: if IsZombiePoseOrigin(key), sEventF `MapComponent.memory_map.deleting_zombie_map`
     (0x0067EA28) "%d" and erase (0x0067E8CC); otherwise `MapComponent.memory_map.keeping_alive_map` (0x0067E9F8). Both
     events print the **argument** origin, not the map key.
   - **Then,** if the origin isn't Unknown: CreateMemoryMap, then emplace. If the key already exists, the new map is
     deleted (0x0067E98C..0x0067E9AC). Then +0x1C = origin (0x0067E9AE).
   - If the origin is Unknown, it returns without writing +0x1C.
4. **IsZombiePoseOrigin** (0x00620AB8..0x00620AD2): false for the current origin, otherwise
   !AnyRemainingLocalizableObjects(id). So the current map is never erased.
5. **CreateMemoryMap** (0x0068133C..0x00681354) never returns null and checks no flag.
6. **The only caller** is BlockWorld::OnRobotDelocalized (0x006249C4..0x006249EC): DeleteObjectsFromZombieOrigins
   (0x00620920..0x00620A04, a plain erase of whole buckets), then CreateLocalizedMemoryMap(id), then the selection
   reset.
7. **Robot::Delocalize:** id = AddNewOrigin() (0x00510A66), always ≥ 1, because next starts at 1 (0x00847934), then
   straight to OnRobotDelocalized (0x00510D62).
8. **Robot::Robot** (0x0050FBF0) constructs BlockWorld (+0x34), MapComponent (+0x25C) and PoseOriginList (+0x294), then
   calls Delocalize(false) (0x00510324) with no branch around it. So map 1 exists and +0x1C = 1 before the constructor
   returns.
9. **UpdateMapOrigins(old, new)** (0x0067DE3C..0x0067DF96). Its only caller is LocalizeToObject, after a successful
   Rejigger (0x0051590C), with old ≠ new (0x00847D9C..0x00847DA2).
   1. UpdateOriginsOfObjects (0x0067DE70).
   2. sb = map[old] (0x0067DE8E).
   3. map[new]. If it's null: sErrorF `MapComponent.UpdateObjectOrigins.NullMapFound` (0x0067DFE0) / "Origin '%s' did
      not have a memory map. Creating empty" (0x0067E010), then create and store.
   4. newMap vt+0xC(sb, pose) (0x0067DF80, unread).
   5. +0x1C = new (0x0067DF84).
   6. erase(old) (0x0067DF8A).
10. **The current origin changes only on these two paths** (AddNewOrigin, Rejigger), and each is followed by the
    matching MapComponent update.

**Conclusion:** AddMarkerlessObject, ClearLocatedObjectHelper and the play-area event can't see a null helper while a
robot exists. The only exceptions are a 2^32 origin-ID wrap, which is theoretical, and teardown, which is unread.
