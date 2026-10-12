# M4 cliffs, pose names, Save.Error and M3 DAS inputs

## 1. M4-038: ComputeCliffPose and AddCliff — coverage

| Step | Original behavior / coverage | Primary citation | Record |
| --- | --- | --- | --- |
| Flags input | Reads event byte +4; zero takes the NoCliff warning and returns false. Individual sensor bits are not used to position the cliff. | Engine 0x00634988..0x0063498C, 0x00634A00..0x00634A3A | M4-038 |
| Pose input | Uses event uint32 timestamp +0, robot's history at robot+0x390, ComputeStateAt with final argument true. | 0x00634996..0x006349A6 | M4-038; history interface |
| Success | ComputeStateAt result zero means success; assigns historical pose to output and returns 1. No sensor geometry or floating-point arithmetic in this function. | 0x006349AA..0x006349AC, 0x00634A3C..0x00634A50 | M4-038 |
| Historical-pose failure | Logs NoHistoricalPose, sets errG, optionally debug-breaks, returns 0. | 0x006349AE..0x006349FE | M4-038 |
| Caller ordering | Requires result exactly 1, then AddCliff; afterwards logs the computed pose's x/y and original flags. Failure logs ComputeCliffPoseFailed then sets errG. | 0x005355AC..0x00535602, 0x00535674..0x005356BC | M4-038 |
| AddCliff wrapper | Calls AddMarkerlessObject with type 15 and returns 0, regardless of its outcome. | 0x006249FE..0x00624A08 | M4-038 |
| Candidate object | Constructs MarkerlessObject; family 6, type 15, active ID -1, factory ID 0; copies robot timestamp to last-observed field. | 0x0062238A..0x0062239C; 0x00502958..0x00502998 | M4-038 |
| Size / placement | Type 15 size (20,40,50); creates zero-angle local translation (0,0,size.z*0.5), multiplies input pose by it, sets parent to current robot pose's parent; InitPose state 1. | 0x0050271E..0x0050274A; 0x006223A0..0x00622430 | M4-038 |
| Current-pose diagnostics | Robot.GetPose checks root ID and shared root against current world origin; failed checks call VerifyFailedReturnFalse before returning robot+0x298. | 0x004EA398..0x004EA46C | M4-038 / shared pose handoff |
| Existing cliffs | Looks up current origin/family 6/type 15; collects matching overlapping objects and refreshes their +0x1C timestamp. If any match, disposes candidate and returns 0. | 0x0062243E..0x006224C4; 0x0061FDFC..0x0061FE6E; 0x0061FD8C..0x0061FDFA | M4-038 |
| Overlap predicate | Same family/type and IsSameAs with existing object's distance/angular tolerance. Predicate internals are **UNKNOWN in this report**. | 0x0061FDB8..0x0061FDDA; IsSameAs PLT 0x004A70B4 | M4-038 / shared geometry boundary |
| Intersection suppression | If robot localized-object ID != -1, excludes families 2 and 6 from the intersection filter. Calls FindLocatedIntersectingObjects with padding float bits 0; a nonempty result suppresses addition. | 0x00622526..0x00622568 | M4-038 |
| New ID / storage | Calls candidate SetID; AddLocatedObject stores shared object under root-origin ID → family → type → ObjectID in BlockWorld+0x3C. | 0x0062256C..0x00622588; 0x00622C88..0x00622D30 | M4-038 |
| Shared deletion before storage | AddLocatedObject first deletes intersecting type 14 objects, with padding 0; deletion may produce RobotDeletedLocatedObject messages. | 0x00622C62..0x00622C84; 0x0062464C..0x006246C0; 0x0061D5A6..0x0061D5EA | M4-038 / NEW shared deletion handoff |
| Addition logs | Channeled info BlockWorld.AddLocatedObject, then event robot.object_located with type string. | 0x00622D3A..0x00622DD8, 0x00622E04..0x00622E1C | M4-038 |
| Internal pose broadcast | Calls BroadcastObjectPoseChanged(obj,null,0): BlockWorld.OnObjectPoseChanged, MapComponent.UpdateObjectPose, BroadcastObjectPoseStateChanged in that order. | 0x00622E42..0x00622E4E; 0x00506F88..0x00506FB8 | M4-038 |
| Broadcast consequences | Conditional FirstPoseForObject / MultipleChanges info; configuration-manager dirty ID. Map UpdateObjectPose returns immediately for family 6; markerless object's IsActive returns 0, so cube-light callback is skipped. | 0x00624808..0x006248B8; 0x00616D54..0x00616D7A; 0x0067EA90..0x0067EAA2; 0x00506288..0x005062DA; 0x004E0240..0x004E0242 | M4-038 |
| Navigation-map write | Sets world+0x54=1; creates MemoryMapData_Cliff with current robot forward XY and current timestamp; inserts projected quad using **input pose**, padding 0; updates world+0x58 to max(timestamp,map virtual +0x38 result). | 0x00622594..0x0062261A; 0x006226EA..0x0062271E; 0x00684984..0x006849A4 | M4-038 / navigation-map interface |
| Complete transitive closure | IsSameAs, filter predicates, deletion's recursive clearing/stacked-object consequences and navigation quadtree internals are **not fully recovered here**. This table does not establish every transitive log/broadcast on every world-state branch. | Read boundaries listed below | UNKNOWN |

### Answer

**ComputeCliffPose returns a historical robot pose; it does not compute an offset from cliff-sensor geometry.** Its signature is `(this, const CliffEvent&, Pose3d& output)`. It only tests whether detectedFlags is nonzero. The sole timestamp is the event's uint32 +0. The local HistRobotState is filled by `RobotStateHistory::ComputeStateAt(timestamp, outTimestamp, state, true)`; status 0 copies its PoseBase to output. There are **no float constants, averages, per-sensor weights, front/back branches or sensor-coordinate additions** in 0x00634980..0x00634A50. A sensor-geometry algorithm here would contradict this body.

Failure behavior is ordered:

- Flags zero: warning `CliffSensorComponent.ComputeCliffPose.NoCliff`, value `CliffEvent::detectedFlags == 0! Can't compute a cliff pose if there was no cliff detected.`, empty metadata vector, return 0. This branch does not set errG itself.
- ComputeStateAt nonzero: error `CliffSensorComponent.ComputeCliffPose.NoHistoricalPose`, value `Could not retrieve historical pose for timestamp %u`, original event timestamp, empty metadata; then errG=1 and conditional sDebugBreakOnError; return 0 after local pose destruction.

The success caller ignores AddCliff's result. Its exact log is channel `Unnamed`, event `RobotImplMessaging.HandleCliffEvent.Detected`, format **`at %.3f,%.3f. DetectedFlags = 0x%02X`** (string 0x005357F4). The word “Detected” is part of the event name, not the format prefix. It reads transform+0x20/+0x24, promotes both float values to doubles and supplies the unsigned flag byte (0x005355CC..0x00535602). This is the unshifted historical pose, including when object addition was suppressed. On result !=1, error `RobotImplMessaging.HandleCliffEvent.ComputeCliffPoseFailed`, value `Failed computing cliff pose!`, empty metadata, precedes errG=1 and optional debug break (0x0053567C..0x005356BC). Historical failure therefore produces both errors.

AddCliff makes a candidate markerless object. Type 15 has size x=20 (`0x41A00000`), y=40 (`0x42200000`), z=50 (`0x42480000`). The placement helper uses angle 0 (`0x00000000`), translation x/y=0 and half-height z=25 (`0x41C80000`), with multiplier 0.5 (`0x3F000000`); markerless corner construction also uses -0.5 (`0xBF000000`). These are **object volume constants, not cliff-sensor positions**. The candidate pose is `inputPose * localHalfHeightPose`; its parent is then explicitly replaced with the current robot pose's parent. InitPose(state=1) delegates to SetPose and makes the pose name `Object_-1` before SetID assigns a new object ID (0x004EF562..0x004EF57E; 0x004EF590..0x004EF620). SetID does not rename it.

Three object outcomes exist:

1. Same-type existing matches in the current origin: all matched objects get the current robot timestamp at +0x1C. Their poses are not reassigned. No addition log, addition broadcast, dirty-map write or new object ID follows this branch.
2. No such matches, but FindLocatedIntersectingObjects returns entries: candidate discarded; no new ID/storage/addition logs.
3. No matches/intersections: assign ID, delete intersecting type-14 objects, insert the shared candidate in the located map; set candidate+0x54 to the context's VizManager pointer (BlockWorld→robot→context+0x24 at 0x00622D24..0x00622D30, context creation/store 0x004EA7AE..0x004EA7BC); log addition and notify the internal pose consumers. Then flag navigation data changed and insert cliff data.

Addition's info channel is `BlockWorld`, event `BlockWorld.AddLocatedObject`, format `Adding new %s%s object and ID=%d ActID=%d FacID=0x%x at (%.1f, %.1f, %.1f), in frame %s.` (0x00BF8EF7). Its first string is empty for this inactive object; second is EnumToString(type=15), IDs from +0x18/+0x40/+0x44, XYZ from the stored shifted pose, final string from FindRoot/GetName. The following event is `robot.object_located`, format `%s`, EnumToString(type=15). No direct game “cliff added” message occurs in these addition bodies.

The internal broadcast is meaningful despite its name: its MapComponent call returns for family 6, and its state-change callback cannot enter cube-light behavior because markerless IsActive is false. BlockWorld still marks the object dirty for configuration tracking. While BlockWorld+0x74 is enabled, a new ID absent from the tick's pose-change queue, with old state 0, logs `BlockWorld.OnObjectPoseChanged.FirstPoseForObject`, value `Object '%d' is setting its first pose. Not queueing change.` If an entry already exists it instead logs `BlockWorld.OnObjectPoseChanged.MultipleChanges`, value `Object '%d' is changing its pose again this tick. Ignoring second change`.

Navigation data is distinct from the candidate pose. Its normal is XY of `currentRobotRotation * X_AXIS_3D()`, not a rotation selected by cliff flags. MemoryMapData_Cliff stores type byte 8, current timestamp at +8 and +0xC, normal floats at +0x10/+0x14. GetBoundingQuadXY is called on the candidate but with the original input pose, padding `0x00000000` (0x006225D6..0x006225EC). It scales canonical ±0.5 vertices by size plus twice padding, rotates them, obtains their XY bounding quad, then adds input XY (0x005025F0..0x005026A4; 0x00502C32..0x00502D42). Thus the map polygon is based on the historical input, while map normal/current-origin selection use current robot state. MemoryMap::Insert constructs FastPolygon and delegates to QuadTree::Insert (0x006817C6..0x006817E0).

Shared deletion can add observable effects before the cliff-addition log: `RobotDeletedLocatedObject {objectID}` via robot Broadcast after DeletedObjectInCurrentOrigin and BroadcastObjectPoseChanged of each removed object's old pose/state (0x0061D5A6..0x0061D5EA). The deletion body has warnings `BlockWorld.DeleteLocatedObjects.NullObject` (`Deleting null object in origin %d with Family:%s Type:%s ID:%d`, 0x0061D488..0x0061D492), and `ObservableObject.SetActiveID.NotActive` / `ObservableObject.SetFactoryID.NotActive` for a cloned inactive object receiving connected IDs (0x0061D1E4..0x0061D2C0). ClearLocatedObjectHelper adds conditional localization/carried/selected-object logs (0x00625CBE, 0x00625D20, 0x00625D7E) and calls their subsystem handlers. These are shared consequences, not absent merely because AddCliff itself has no logger.

### Current claim and UNKNOWN

Current manifest M4-038 title: **“CliffSensorComponent::ComputeCliffPose and BlockWorld::AddCliff (the detected-cliff path)”**, status **RECOVERABLE_GAP**. Its evidence is `research/20261012-m3m4-settling-2.md: HandleCliffEvent calls ComputeCliffPose (0x00634980; call 0x005355B4) and BlockWorld::AddCliff (0x005355C0); the "Detected at" info (0x00535602) and ComputeCliffPoseFailed with errG (0x00535682..0x005356BC) depend on it.` That evidence identifies interfaces; it did not establish an offset algorithm or the storage path.

Remaining **UNKNOWN**: full numeric/branch semantics of IsSameAs and its GetWithRespectTo/rotation comparisons; exact intersection-filter predicate and all filtering failures; recursive clearing of type-14 objects (ClearLocatedObjectHelper can search stacked objects and mark them unknown); quadtree merge/storage semantics and virtual map timestamp method. Those are recoverable native boundaries, not external/hardware unknowns. Navigation/deletion call sites and their immediate effects above are established, but the whole transitive path must not be called settled from this report. History interpolation and failure details inside ComputeStateAt are an explicit history-subsystem handoff; this report establishes how its status is consumed. No inventory, manifest or implementation was changed.

## 2. M4-037: pose-origin root names — coverage

| Step | Original behavior / coverage | Primary citation | Record |
| --- | --- | --- | --- |
| Warning selection | Compares located object's ID with connected ID; formats first located result and current differing located result. | Engine 0x00534022..0x00534040, 0x00534052..0x005340CC | M4-037 |
| FindRoot wrapper | Uses shared node at pose+4; if parent exists, follows node FindRoot; otherwise wraps the same node. | 0x0084520C..0x00845274 | M4-037 |
| Parent traversal | Follows node+0x30 until parent is null; 1000-step guard logs LoopBoundOverflow, sets errG and optionally breaks. | 0x008452C4..0x0084537C | M4-037 |
| GetName / SetName | GetName returns node+0x38 string reference; SetName assigns the supplied string there. | 0x00844956..0x0084495A; 0x0084497A..0x00844986 | M4-037 |
| Origin allocator | Counter starts at 1. AddNewOrigin passes current counter to AddOriginWithID; name is `Origin` + unsigned decimal ID. | 0x0084792C..0x00847940; 0x00847A84..0x00847AA0; 0x00847ADC..0x00847B6C | M4-037 |
| Live robot names | Robot ctor names body/drive center and calls Delocalize(false); Delocalize adds a fresh origin. | 0x0051027E..0x00510324; 0x00510A5E..0x00510A70 | M4-037 |
| Origin reparenting | Rejigger reparents old current origin under target and appends `_REJ` to old node; Flatten appends `_FLT` to flattened child origins. FindRoot still prints the ancestor root name. | 0x00847E10..0x00847E7E; 0x00847F34..0x00847F8C | M4-037 |
| Cube pose names | Initially empty pose; SetPose assigns `Object_` + signed decimal object ID. Root name is independently inherited from parent chain. | 0x008765D0..0x008765F6; 0x004EF590..0x004EF620; active-cube base ctor 0x004E0CBC | M4-037 |
| Printed bytes | IDs and root C strings from two located objects, format `First object=%d in '%s'. This object=%d in '%s'.` | 0x0053406A..0x005340CC; format 0x00BE95E1 | M4-037 |

### Answer

There is no automatic conversion of a name from an origin ID in GetName. The name is stored in the pose-tree node, independently of its numeric ID. FindRoot returns a wrapper sharing the ancestor node; GetName returns that node's string. The warning converts the two strings to C-string pointers using libc++ short/long-string representation (0x00534086..0x00534096, 0x005340AE..0x005340B8).

PoseOriginList starts next-ID=1, current ID=0/current pointer null. AddOriginWithID converts unsigned ID to decimal and inserts six characters `Origin` at position 0; string 0x00847BD8. It constructs a root Pose3d with that name, inserts under the requested ID, sets node ID, updates current ID/pointer and next-ID=max(oldNext,ID+1). Duplicate insertion reports VerifyFailedReturnFalse and does not perform the success updates. Robot construction calls Delocalize(false), yielding the first world root `Origin1`; subsequent delocalizations yield `Origin2`, `Origin3`, etc. Exact lifetime numbers depend on actual delocalization history.

The robot's own pose is `Robot_<unsignedRobotID>` (string 0x00510A04) and drive-center child is `RobotDriveCenter_<unsignedRobotID>` (0x00510A0C). Those are node names, not root-origin names. Cube base ObservableObject creates an empty-name pose. SetPose copies the incoming pose, stores state and then renames it `Object_<signedObjectID>`; an object initialized while ID=-1 gets `Object_-1`. SetID itself does not rename the pose. Parenting/localization puts a located cube below an engine origin; its local `Object_...` name is consequently not printed by this warning.

Rejigger names the **old origin node** `<oldName>_REJ` after giving it a parent under the target origin. Flatten names successfully flattened child-origin nodes `<oldName>_FLT`; both are child names. FindRoot traverses them to the surviving root, so a warning between live engine world frames normally prints `Origin<n>` and `Origin<m>`, not the `_REJ` or `_FLT` child names, and not `Robot_...` / `Object_...`.

The first message ID/root is from the vector's **first located matching object**, loaded at 0x00534052..0x00534074. The second is the currently iterated located object, loaded at 0x00534086..0x005340AA. The connected ID is used by the mismatch test; it is not automatically the first ID printed. The exact value is:

`First object=<firstLocatedID> in '<firstLocatedRootName>'. This object=<thisLocatedID> in '<thisLocatedRootName>'.`

For example, a runtime state containing IDs 7 and 9 under Origin1 and Origin2 would format `First object=7 in 'Origin1'. This object=9 in 'Origin2'.` This is an illustrative substitution, not a capture or a claim those IDs occur. Event suffix is `ActiveObjectInDifferentFramesWithDifferentIDs`, concatenated to the caller's existing event prefix (0x00534042..0x0053404E).

### Current claim and UNKNOWN

Current M4-037 title: **“Pose-origin root names for the ActiveObjectInDifferentFramesWithDifferentIDs warning”**, status **RECOVERABLE_GAP**. Evidence: `research/20261012-m3m4-settling-2.md: the warning at 0x00534042..0x005340CC prints the root names of two pose origins (FindRoot 0x4A40F0, GetName 0x4A4C60) when a located ID differs from the connected ID.` The bodies and origin naming above fill that named read; no status change is asserted.

**UNKNOWN:** actual IDs/root names in a particular warning require that runtime pose tree. A standalone unparented cube can have its own name as root; the warning does not substitute `Origin...` text for it. Invalid parent pointers/cycles invoke shared pose-tree diagnostics; this report does not establish their full recovery behavior beyond the traversal limit. No new compatibility naming policy is inferred.

## 3. M4-043: failed-open exception text — coverage

| Step | Original behavior / coverage | Primary citation | Record |
| --- | --- | --- | --- |
| Exception mask | Sets exception mask to all bits (0xFFFFFFFF), then clear(current state), then open with out mode 0x10. | Engine 0x0061B090..0x0061B0C6 | M4-043 |
| Failed open | Null filebuf open result makes state `oldState OR 4`, then ios_base::clear. | Engine 0x0061B560..0x0061B598 | M4-043 |
| Throw construction | clear sees mask/state intersection; constructs system_error with error_code {1,iostream_category}, prefix `ios_base::clear`, then installs failure vtable/type and throws. | libc++ 0x0003B6D0..0x0003B71A | M4-043 |
| failure constructor | Public char* failure ctor calls same system_error constructor and replaces vptr. | libc++ 0x00043F90..0x00043FA8 | M4-043 |
| Formatting | Nonempty prefix + `: ` + category message. | libc++ 0x00067E6C..0x00067EB0; 0x00067CF0..0x00067D5C | M4-043 |
| Category bytes | Iostream error code 1 returns `unspecified iostream_category error`. | libc++ 0x00043ECC..0x00043EEE; string 0x00043EF0 | M4-043 |
| Constructed message accessor | runtime_error::what returns stored string at exception+4. | libc++ 0x0007D5DC..0x0007D5DE | M4-043 |
| Actual Save accessor | Catch copies runtime_error base and code, then directly calls **std::exception::what**, not runtime_error::what or virtual what. | Engine 0x0061B220..0x0061B254; PLT 0x004A8C98 import relocation | M4-043 |
| Actual log bytes | Base exception::what returns literal `std::exception`; Save logs it with `%s`, then sets errG and conditional debug-break. | libc++ 0x0007C3C4..0x0007C3C6, string 0x0007C3C8; engine 0x0061B258..0x0061B29A | M4-043 |

### Answer

There are **two different strings**. The failed-open exception's constructed runtime_error message is:

`ios_base::clear: unspecified iostream_category error`

These are 52 ASCII bytes followed by NUL: prefix at libc++ 0x0003B730, separator at 0x00067DA0, category string at 0x00043EF0. No filename, errno value, strerror text or newline is appended on this iostream code-1 branch. The throw site has inlined failure construction: system_error constructor followed by installing the ios_base::failure vptr/typeinfo; its public constructor at 0x00043F90..0x00043FA8 has the same construction pattern.

However, **BlockFilter.Save.Error receives `std::exception`**, 14 ASCII bytes followed by NUL, not that 52-byte message. The decisive instruction is `0x0061B254: blx 0x004A8C98`. The engine's `.rel.plt` import for that PLT entry is `_ZNKSt9exception4whatEv` (`std::exception::what() const`). The shipped libc++ implementation at 0x0007C3C4 loads literal 0x0007C3C8 and returns immediately, ignoring the object. There is no intervening virtual call. Runtime_error's different accessor at 0x0007D5DC would load object+4, but this catch does not call it.

The catch logs event `BlockFilter.Save.Error`, empty metadata, format `%s`, base accessor result (0x0061B258..0x0061B260). Only afterwards it sets errG=1 and optionally debug-breaks (0x0061B286..0x0061B29A). This distinction follows native call/import resolution, rather than assuming a C++ source-level `failure.what()` necessarily dispatches virtually in this binary.

### Current claim and UNKNOWN

Current M4-043 title: **“BlockFilter::Save failure text: the ios_base::failure what() bytes from libc++_shared.so”**, status **RECOVERABLE_GAP**. Evidence: `research/20261012-m3m4-settling-2.md: Save.Error (0x0061B21C..0x0061B2A4) logs what() of the caught ios_base::failure; the bytes are in the shipped libc++_shared.so.` The broad statement “logs what()” omits the direct base accessor; this read supplies that material distinction. It does not change the manifest.

**UNKNOWN:** which host path/open operation fails in a particular run is outside this static read. No shipped code was executed and no failure was induced. The failed-open string and actual Save accessor/log path above are established statically for these shipped binaries; allocation failures and unrelated exception types are not claimed to take this catch.

## 4. M3-055: DasTransferTask and $player_id — coverage

| Step | Original behavior / coverage | Primary citation | Record |
| --- | --- | --- | --- |
| Creation | CozmoContext creates TransferQueueMgr at +0x28, then allocates 16-byte DasTransferTask and stores at +0x2C, unconditionally. | Engine 0x004EA7BE..0x004EA7E8 | M3-055 |
| Registration | Context calls TransferTask::Init(task,queue); binds virtual slot +8 and registers function. | 0x004EA886..0x004EA88A; 0x006A4358..0x006A438E | M3-055 |
| App trigger | Android background service availability broadcast invokes native ExecuteBackgroundTransfers on a worker thread. | sources/com/anki/cozmo/BackgroundConnectivity.java:36–46,85–117; CozmoJava.java:9 | M3-055 / app input |
| Native forwarding | JNI log then cozmo_execute_background_transfers; null engine returns; otherwise engine→context+0x28 queue→ExecuteTransfers→StartDataTransfer. | 0x0066789C..0x006678DC; 0x006669F0..0x00666A02; 0x004EE33C..0x004EE340; 0x006A38CE..0x006A38D0 | M3-055 |
| Virtual dispatch | Queue invokes registered function, binder selects task vtable+8, DasTransferTask slot points to 0x006A25B5. | 0x006A41B0..0x006A41F0; 0x006A4446..0x006A445E; relocated vtable 0x01031310, slot 0x01031320 | M3-055 |
| Flush / completion | OnTransferReady copies completion function, calls DASForceFlushWithCallback; callback emits das.upload / das.upload.fail with value background, then invokes completion. | 0x006A25B4..0x006A25FE; callback vtable slot 0x01031350→0x006A27D1; 0x006A27D0..0x006A2822 | M3-055 |
| Digest input | Startup obtains DAS platform, virtual +0x10 string, copies strlen bytes, hashes and stores hexdigest under $player_id. | 0x00665F52..0x00665FAC | M3-055 |
| Platform accessor | Android platform vtable+8+0x10→0x00028AF7, returns string member +0x1C. | libDAS vtable 0x00036AB8; 0x00028AF6..0x00028B06 | M3-055 |
| Input population | JNI getDeviceID(String path), UTF bytes converted to native string, stored at platform+0x1C. | libDAS 0x0002817C..0x00028198; 0x00028318..0x0002837E; 0x000288E4..0x00028940 | M3-055 / app input |
| App source | Reads first line of provided device-ID file; otherwise UUID.randomUUID().toString(), attempts to persist line, returns it. | sources/com/anki/daslib/DAS.java:159–242; smali/com/anki/daslib/DAS.smali:552 onwards, getDeviceID labels :cond_20/:cond_34/:goto_23 | M3-055 / app input |
| MD5 bytes / hex | Constructor updates over exact string length and finalizes; hexdigest formats 16 digest bytes with lowercase %02x, 32 characters. | 0x0080EE56..0x0080EEAC; 0x0080F760..0x0080F7B2, format 0x0080F7F0 | M3-055 |
| RemoveRobot correction | 0x0052F2D8 is sSetGlobal($session_id,null); it neither hashes nor clears $player_id. Then PauseUploading(false). | 0x0052F2D4..0x0052F2EE; key 0x0052F3E4 | M3-055 |

### Answer

DasTransferTask is **an engine-owned object**, not an SDK-only object. CozmoContext creates it even when the optional external-interface-dependent audio construction is skipped. It registers its OnTransferReady callback with the engine's transfer queue. The virtual bound-member encoding in TransferTask::Init is offset 8 / virtual flag 1; the installed DasTransferTask vptr is the symbol vtable+8, and virtual slot +8 resolves to Thumb entry 0x006A25B5. This explains why the decompiler's direct-callers list is empty.

The recovered invocation chain is:

`CozmoJava.install → BackgroundConnectivity.install → connectivity broadcast listener → JNI ExecuteBackgroundTransfers → cozmo_execute_background_transfers → CozmoEngine::ExecuteBackgroundTransfers → context TransferQueueMgr::ExecuteTransfers → StartDataTransfer → registered task function → virtual OnTransferReady → DASForceFlushWithCallback`.

The Android service installs an inexact repeating alarm, first delay 5000 ms, interval 900000 ms, and ignores requests less than 450000 ms since its last update. Its listener runs native transfers on a new thread if called on the main looper, otherwise directly. The Android connectivity test is app-side evidence only; it was **read, never executed**. No endpoint was contacted. The JNI path logs `AndroidBinding.BackgroundConnectivity`, `received transfer request`, before forwarding. The exported native function returns if the engine global is null.

Stack scope conclusion: the engine transfer path is not intrinsically unreachable and has no SDK gate in these bodies. Its Android scheduling/connectivity stimulus is an **app-provided input** under the approved app-boundary rule. A replacement host event can supply that input; the binary does not establish the replacement host's scheduling policy. Removing the entire task as “Android/SDK code” would discard an engine path. No new host policy is selected by this research. A failed flush completion still invokes the copied task-completion function after event `das.upload.fail`, value `background`; successful completion uses `das.upload` with the same value (0x006A27DE..0x006A2822).

`$player_id` is lowercase MD5 hex of the **DAS device ID string**. Startup reads DASGetPlatform() virtual slot +0x10, which on the initialized Android platform is its +0x1C string accessor. The value was populated through Java `DAS.getDeviceID(path)`. Java returns the first line of an existing file if non-null; otherwise it generates a UUID string, tries to create parent directories/write the line, and returns the generated string even if persistence fails through the caught errors. It is not the robot serial, cube factory ID, user profile ID or an engine-generated player name.

Native JNI conversion uses GetStringUTFChars and strlen (libDAS 0x000288FA..0x00028916), followed by release of Java chars. Startup takes strlen again and hashes exactly those copied bytes, excluding trailing NUL (0x00665F66..0x00665F7A; MD5 ctor 0x0080EE8A..0x0080EEA8). For normal persisted UUID strings these are ASCII bytes. The engine owns digest construction; the persistent source identity belongs to the app/platform input seam. A host replacement identity must therefore be named as an input policy, not guessed from the robot ID.

**The RemoveRobot premise is incorrect:** 0x0052F2D4 loads `$session_id` and 0x0052F2D6 supplies null before sSetGlobal at 0x0052F2D8. Startup sets `$player_id`; this RemoveRobot branch clears the session global and resumes uploading at 0x0052F2EE. It contains no second player digest input.

### Current claim and UNKNOWN

Current M3-055 title: **“The engine's calls into libDAS: startup configuration, archive config, pause and network mask schedule, globals, flushes”**, status **IMPLEMENTATION_GAP**. Current evidence cites `20261012-m3-051-libdas-sink-b-check.md`, startup configuration/enable/disable addresses, platform init, Pause(false) 0x665F4E, `$player_id` 0x665FAC, flushes 0x665FCC/0x6A25FE, ConnectToRobot pause, RemoveRobot 0x52F2D8/0x52F2EE, shutdown and provider methods. Its unresolved text specifically names the DasTransferTask caller and digest input. The two named input reads above are supplied; no build or settling claim is made. The request/notes must distinguish RemoveRobot's **session** global from startup's **player** global.

**UNKNOWN:** the actual persistent device-ID value on any particular phone; the replacement host identity/connectivity/scheduling policy; full transfer-queue concurrency and all unrelated tasks registered with it. The Das task's creation, registration, dispatch seam and flush completion body are read. The Android availability implementation is evidence of the original stimulus, not authorization to reproduce its network probe.

## Research provenance and boundaries

Answers request `re-analysis/research/requests/20261012-m4-cliff-and-names.md`, after reading PROJECT_STATE.md, research README, extractor rules, current four manifest records and `20261012-m3m4-settling-2.md`. Native addresses are ELF virtual addresses; Thumb symbol low bits are cleared for disassembly. Ghidra output was navigation only; the decisive instruction ranges/imports/data were checked in local binaries. Coverage labels describe this read, not fidelity classifications or approvals.

Observed HEAD during research: `2cacdf698436a177017f3fb4566b7cdfeb89837f`. This shared checkout may be advanced by the builder independently. Research issued no git pull/branch/worktree/commit/push, no build/test/hardware run, no uploads and no network request. The builder's B-M5-13/B-M3M4-12 work was not executed by this research lane.

SHA-256 of inspected shipped files:

- libcozmoEngine.so: `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`
- libc++_shared.so: `8ac5090bbd0be7401af5fff6044ded6f1e6ced692fcd3581fa3cb937b519a16a`
- libDAS.so: `0a0bc63b301fb7f9ffb559462f712a8bf9dd4954b11c43face8f4783852f496c`

Local reproducibility aids beside this report: `20261012-m4-cliff-and-names-disasm.py`, corresponding `-libcpp-disasm.py` / `-das-disasm.py`, and their `*-native.txt` dumps. They read binaries locally; dumps can include decoded literal pools after function tails, which are not instruction evidence. Address annotations are navigation aids; imported symbols and relocation-backed vtable slots were checked separately.
