# R-DELOC-CALLEES — 2026-10-13

## Item 1 — AnyRemainingLocalizableObjects(uint): coverage

| Step | Behavior recovered | Native evidence | Record boundary |
|---|---|---|---|
| A1 | Construct empty ID/type/family/origin include/exclude sets; carried-object exclusion byte+6C=0; install one CanBeUsedForLocalization predicate. | 0x00626F14..0x00626FA0; predicate vtable0x0102E35C, call slot0x0102E374 ->0x0062C47E | M11-070 / existing M11-044 predicate |
| A2 | Set origin filter mode+6D=3. UnknownOriginID is0; nonzero argument inserts that ID into allowed-origin set; zero leaves it empty (all origins). | 0x00626FA2..0x00626FC4; GOT0x0103E978 -> data0x00C97B20 =0 | M11-070 |
| A3 | FindLocatedObjectHelper copies filter, adds carried exclusion only when+6C nonzero; that branch is not taken here. | 0x0061EB92..0x0061EBD0; copy preserves flags0x005C6CE8..0x005C6CEC | M11-070 |
| A4 | Traverse located buckets by ascending unsigned origin, signed family/type, signed ObjectID; mode3 honors allowed origins, empty allow means all. Family/type/ID filters are empty here. | 0x0061EBD0..0x0061ED48; successor loops0x0061EE12..0x0061EEAA | M11-070 |
| A5 | Null object warns with origin/family/type/ID, skips it; nonnull object invokes predicate. No pose-state/type/active-state test in wrapper beyond predicate. | 0x0061ED08..0x0061EDE4 | M11-070 |
| A6 | Predicate tail-calls object's vtable+18. ActiveCube's secondary group selects ActiveObject override; ordinary ObservableObject/CustomObject and Charger return false. | 0x0062C47E..0x0062C484; ActiveCube slot0x0101CE04 ->0x004E4ACC; Charger slot0x0101E1DC ->0x004EA66E; base0x004E024A | M11-007 / M11-044 dependency |
| A7 | Active override requires Known state1, IsMoving(NULL) !=1, Known again, signed activeID>=0, fromDistance>=0, then IsRestingFlat with virtual tolerance converted degrees-to-radians. Cube tolerance5deg; strict resting-flat comparison. | 0x004E49AC..0x004E4A58; movement0x004E3844; tolerance0x004E024E; resting-flat0x0087751C..0x00877562 | M11-007 dependency |
| A8 | First predicate success returns that object immediately (returnFirst=1); empty modify function makes no object mutation; pointer normalized to bool0/1. No match returns0. | 0x00626FC8..0x00626FD8; 0x0061ED72..0x0061ED8E; 0x0061EEB0..0x0061EEB6; 0x0062704A..0x00627060 | M11-070 |
| A9 | Current origin is retained without calling predicate; old bucket retained iff this result is1; otherwise whole bucket is erased. | 0x00620920..0x00620A04; call0x0062095E | M11-069 |

## Item 2 — SetPose and AddVisionOnlyStateToHistory: coverage

| Step | Behavior recovered | Native evidence | Record boundary |
|---|---|---|---|
| P1 | SetNewPose first converts supplied pose relative to its root; calls SetPose; ignores its return register; destroys temporary and always proceeds to history insertion. | 0x005126EC..0x00512704; root conversion0x004DF628 | M11-070 |
| P2 | SetPose checks newPose.HasSameRootAs(current world origin). Failure emits VERIFY and makes no robot-pose writes. | 0x00515034..0x00515040; 0x00515064..0x00515070 | M11-070 |
| P3 | Save current robot pose's name, assign new pose to Robot+298, restore saved name. | 0x00515042..0x00515062, 0x00515072..0x00515088 | M11-070 |
| P4 | Derive offset=-20 when carried ID [[Robot+284]+8] ==-1, otherwise0; copy main pose into Robot+2A4, set x/y to x+offset*cosf(yaw), y+offset*sinf(yaw), z=0. | 0x0051508C..0x005150AC; MoveRobotPoseForward0x00517EC0..0x00517F46 | M11-070 |
| P5 | SetNewPose history arguments: timestamp Robot+2C, stored pose+298, head angle+2FC, lift angle+300; return is history insertion result. | 0x00512708..0x00512722 | M11-070 / M11-044 history interface |
| P6 | Before constructing history state, set Robot+2C6=1 and increment uint pose-frame counter+2B0. No rollback on history failure. | 0x00515144..0x00515152 | M11-044 already names these stores |
| P7 | Construct default HistRobotState (default unnamed identity Pose3d plus default RobotState); SetPose writes copied pose, incremented frame ID at Hist+10, head/lift at+34/+38. | 0x00515156..0x0051516E; ctor0x0053044C..0x0053048A; SetPose0x0053053C..0x00530556; defaults0x005182B0..0x005182E8 | M11-070 / M11-044 adjoining history |
| P8 | AddVisionOnlyState rejects a pose with a parent that is not a root; no-parent or direct-root-parent pose proceeds. | 0x005310B4..0x005310D6; failure0x005311BA..0x00531222 | M11-044 history dependency |
| P9 | Reject too-old timestamp iff raw map nonempty AND newestRaw>window AND timestamp<newestRaw-window (unsigned comparisons). | 0x005310D8..0x00531106; log0x0053111C | M11-044 history dependency |
| P10 | Existing vision-only key: overwrite pose and copy0x5B bytes of RobotState; no cull call on this branch. Absent key: emplace state, failure logs/returns1; success CullToWindowSize then return0. | 0x0053114C..0x005311B8; error0x00531224..0x0053127C | M11-044 history dependency |
| P11 | Error paths set Util::_errG=1 and optionally DebugBreakOnError. Wrapper preserves result across temporary destruction, returns0 success/1 failure. | 0x0053126A..0x00531280; wrapper0x0051517A..0x0051518A | M11-070 |
| P12 | Complete pose-tree assignment and history cull internals remain separately owned dependencies; this read establishes calls/order and local writes, not their entire implementations. | Assignment PLT0x004A40E4; Cull call0x005311B2 | M11-066 and existing history records; whole dependency audit UNKNOWN here |

## Item 3 — AddInExistingPose emplace defaults: coverage

| Step | Behavior recovered | Native evidence | Record boundary |
|---|---|---|---|
| C1 | Use object ID at obj+14 as key; unique map insertion returns existing node unchanged if key already present. | 0x00506F5A..0x00506F6E; emplace0x00507B14..0x00507B86 | M11-070 / M11-007 |
| C2 | Absent key allocates0x3C-byte node, copies ObjectID key, constructs value at node+18; not the observation-taking constructor0x005062E0. | 0x00507B88..0x00507BCE; call0x00507BC6 ->0x00507C18 | M11-070 |
| C3 | Default value begins with unnamed default identity Pose3d. Zero24bytes starting value+C: count, misses, unsigned argument/time field, last-observed word, object pointer and shared control pointer. | 0x00507C18..0x00507C58; zero at0x00507C4A..0x00507C50; default Pose3d0x0084683C..0x0084686A | M11-070 |
| C4 | Link/rebalance node and increment map size only on new insertion. | 0x00507B70..0x00507B7E; insert helper0x00507BEC..0x00507C14 | M11-070 |
| C5 | Regardless of inserted/existing, assign obj.GetPose into node+18; other fields remain defaults or preexisting values; return0. | 0x00506F72..0x00506F80 | M11-007 / M11-069 caller |

## Item 4 — external-interface slot+18: coverage

| Step | Behavior recovered | Native evidence | Record boundary |
|---|---|---|---|
| E1 | Build empty RobotDelocalized E2G message; if engine-context+4 external-interface pointer nonnull, call its slot18 with const message; afterward destroy message. | 0x00510D84..0x00510D9C; pointer access0x00516BCC | M11-069 / M11-070 |
| E2 | Shipped concrete UiMessageHandler slot18 is Broadcast(MessageEngineToGame const&). First DeliverToGame(message,UINT_MAX), then timestamped copied event to local subscribers. | Vtable header0x0102FE14, installed0x0102FE1C, slot0x0102FE34 ->0x00662508; body0x00662508..0x00662558 | M11-070 |
| E3 | DeliverToGame increments+FC, packs message into4096-byte payload, stores Size in uint16 packet prefix and engine ID at packet+1008; UINT_MAX selects destinations0,1,2 in that order, skipping null pointers, calling each slot28. | 0x006615C4..0x0066166C; UI slot48 relocation0x0102FE64 ->0x00661594 | M11-070; app delivery interface |
| E4 | Factory creates DirectGameComms for0, null for1, TcpSocketComms for2. Direct route requires state+98==2, passes packed payload/size into in-process GameMessagePort. TCP route requires deviceID!=-1 and HasClient(), sends size+2 bytes; return ignored by broadcaster. | 0x0065FEDC..0x0065FF14; direct0x0065C420..0x0065C43C; TCP0x0065F440..0x0065F46A | M11-070; SDK route outside supported scope |
| E5 | Direct port locks mutex; if overflow flag already set, drops; if capacity insufficient, sets flag and logs warning/error; otherwise copies payload and increments byte count. Direct wrapper returns1 even if port drops. | 0x0065D8B8..0x0065D994 | Engine-to-app handoff dependency |
| E6 | Following delivery, obtain engine timer binary32 seconds, widen to double, copy tag/payload into AnkiEvent, find signal by tag and synchronously call its linked-list callbacks, skipping empty callbacks. No deferred queue in this overload. | 0x00662528..0x00662558; tag lookup0x00662590..0x006625AE; signal0x00663C74..0x00663CB8 | M11-070; subscriber bodies separately owned |
| E7 | Absence of UI delivery connection does not suppress local event dispatch. Absence of external-interface pointer suppresses entire Broadcast. | E1, E2, E3, E6 control flow | M11-070 |

## Item 5 — visualizer color, transport and gate: coverage

| Step | Behavior recovered | Native evidence | Record boundary |
|---|---|---|---|
| V1 | Both SetText calls use NamedColors::YELLOW, bytes FF FF 00 FF at0x00C9743B. Label2 `LocalizedTo: <nothing>`; label3 `WorldOrigin[%lu]: %s`. | GOT0x0103E7DC relocation; 0x00510BF8..0x00510C46 | M11-070 |
| V2 | SetText initializes255-byte buffer, formats via vsnprintf(limit255), reads label/color, reverses color word into0xFFFF00FF, constructs SetLabel/MessageViz then immediately clears it. No send call. | 0x006C0314..0x006C0394 | M11-070 |
| V3 | SendMessage and SendVizMessage are unconditional `bx lr` stubs in this APK binary; neither checks a connection nor transmits. | 0x006BE610; 0x006C05EC | M11-070 |
| V4 | Connect checks manager byte0 for prior connection and optionally Disconnects; logs connected, sets byte0=1, returns0. No socket-connect call in this body. | 0x006BE2AC..0x006BE41E | M11-070 |
| V5 | Disconnect alone checks byte0, calls embedded UdpClient at+4 Disconnect, clears byte0 and logs; returns0. SetText never consults this byte. | 0x006BE51C..0x006BE56C | M11-070 |
| V6 | Consequently these label paths send nothing outside or inside a developer visualizer connection in the shipped build. UDP member/disconnect exists, but no send transport/gate is reachable from the requested SetText path. A different developer binary's implementation is UNKNOWN and is not evidence for this binary. | Entire V2/V3/V4 bodies | M11-070; scope decision supported by shipped no-send behavior |

## Evidence and result

Answers [requests/20261013-delocalize-callees.md](requests/20261013-delocalize-callees.md), continuing [the adopted Delocalize extraction](20261013-delocalize-and-update-object-origin.md). Read PROJECT_STATE and research rules before work. Binary `resources/lib/armeabi-v7a/libcozmoEngine.so`, 3.4.0-1204, SHA-256 `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`. ELF virtual addresses; code addresses omit Thumb bit. Used local LIEF relocation resolution and Capstone with `skipdata=True`. Ghidra files were navigation aids, checked against native instructions; their incorrect return types, arguments and relocated addresses were not adopted.

The five coverage tables are first. “Recovered” means independently read primary evidence for manager verification, not a manifest status change. Current M11-070 is RECOVERABLE_GAP; M11-069, M11-044 and M11-007 are IMPLEMENTATION_GAP. Their dependency ownership does not certify an unread adjoining path.

### 1. Exactly which objects preserve an old origin

For an ordinary nonzero origin argument the query is:

```text
any located object in bucket[originId]
whose virtual CanBeUsedForLocalization() returns nonzero
```

There is no family/type whitelist, active-only family flag, carried-object exclusion, or object-state test inserted into the wrapper. It searches **located** objects, not the connected-object registry. UnknownOriginID=0 makes the same overload an all-origin query; do not confuse it with the robot-frame filter default used elsewhere. Mode3 and an empty allowed set allow every bucket. A missing specified bucket or no qualifying object gives0.

The concrete active-cube predicate establishes Known pose state1, stationary movement flag, signed active ID>=0, nonnegative fromDistance and resting-flat condition. Known is read both before and after the movement call. Moving==1 warns `ActiveObject.CanBeUsedForLocalization.PoseStateKnownButMoving` with empty text (0x004E49DE), then returns0. It does not require an additional connection-state Boolean. `IsMoving(NULL)` is the byte read at0x004E384A, reached through the active-object group. Ordinary object/default and custom-object vtables use0x004E024A=false; Charger secondary slot uses0x004EA66E=false. The ActiveCube localization slot is a **secondary** group at0x0101CDEC, not primary vtable header0x0101CD24: its +18 at0x0101CE04 selects override thunk0x004E4ACC. Treating every class's primary +18 as this predicate would misidentify a destructor.

After adjustment to the ObservableObject group, tolerance slot+1C is0x004E024E, returning float5. Multiply by float0x3C8EFA35 (degrees-to-radians) before constructing Radians. IsRestingFlat flattens object pose to its root, obtains rotation matrix and the rotated parent Z-axis's projection, clears its sign bit, calls acosf and compares the resulting Radians **strictly less** than tolerance (0x00877548..0x0087755C). This accepts the appropriate flat axis orientations, not just upright yaw; no host “tilt <=5” approximation is claimed.

Nested std::map traversal determines the first qualifying object: ascending unsigned origin, ascending signed family, signed type, signed object ID. A failed predicate advances, success stops before later objects. Null located entries warn `BlockWorld.FindObjectHelper.NullExistingObject` / `Origin:%s Family:%s Type:%s ID:%d is NULL` at0x0061EDE4 and continue; they do not count. Neither this function nor its empty modifier changes a surviving object.

DeleteObjectsFromZombieOrigins retains the current bucket without evaluating any object, and retains a noncurrent bucket if this query succeeds. Thus **one qualifying object keeps every object in that bucket**, including objects that individually would fail the predicate. The query does not selectively delete failed objects. Carried objects are not excluded by this query; their prior move to a new bucket is the separate M11-069 pose-tree path.

### 2. Pose writes, history insertion and failures

SetPose performs no ordinary success log. Root mismatch calls `sVerifyFailedReturnFalse` with key `Robot.SetPose.NewPoseOriginAndWorldOriginMismatch`, format `VERIFY(%s): `, argument `newPose.HasSameRootAs(GetWorldOrigin())` (0x0051506C). It then exits without assigning either robot pose. This is a verification failure return from the logging helper; **SetPose itself does not establish a Boolean/status return contract**. Its caller does not test r0. Normal path's cleanup also clobbers r0; do not make its final register a success code.

Success preserves main pose's existing name around PoseBase assignment. Main Robot+298 gets the new transform/parent through the pose-tree assignment dependency. Derived Robot+2A4 is assigned from that pose, then translation is rewritten using yaw, cosf/sinf and separate binary32 multiplication/addition. Its z is explicitly0; no extra rotation change. Offset comes from the **actual carried ID**, not Delocalize's supplied isCarrying argument. Therefore after Delocalize first resets both poses to identity, SetNewPose's SetPose recomputes the second pose: with no carried object it is20mm behind the new main pose. The earlier identity reset alone is not the final second-pose value.

SetNewPose root-converts input, calls SetPose, destroys root temporary, then always calls AddVisionOnlyStateToHistory with Robot timestamp+2C and **stored main pose**+298. If SetPose verification failed, insertion uses the previous stored pose. Head/lift float arguments are +2FC/+300 respectively (the decompiler's extraout_s1 is wrong; native loads/stack argument settle both). SetNewPose returns the result from AddVisionOnlyStateToHistory. The Delocalize warning `Robot.Delocalize.SetNewPose` / `Failed to set new pose` therefore tests this **history result**, not a SetPose Boolean.

Robot wrapper writes+2C6=1 then counter+2B0=counter+1 before any history checks. uint addition wraps as native integer addition. It constructs a HistRobotState and calls HistRobotState::SetPose with that new frame ID. All default RobotState writes, relative to the state portion (Hist+0C), are:

| Offset / width | Default write | Evidence |
|---|---|---|
| +00 uint32 | 1 | 0x005182B6..0x005182BA |
| +04 uint32 | 0; subsequently overwritten by frame ID at Hist+10 | 0x005182BA; 0x0053054E |
| +08 uint32 | 1 | 0x005182BE |
| +0C..+47 bytes | 0 | 0x005182C0..0x005182C4, memset length0x3C |
| +48 float | 5.0 | 0x005182D0..0x005182DA |
| +4C uint32 | 0x300 | 0x005182D6..0x005182DA |
| +50 and+54 uint32 | UINT_MAX | 0x005182CA..0x005182D4 |
| +58 uint16 | 0 | 0x005182E0 |
| +5A byte | 0xFF | 0x005182DE..0x005182E4 |

Hist SetPose also overwrites head/lift fields at Hist+34/+38 (state+28/+2C). This is a default-state-plus-pose record, not a snapshot of all live robot sensor fields. The wrapper then calls history at Robot+390, destroys temporary Pose3d, preserves insertion result and returns it. Counter/update flag are not restored on failure; neither main nor derived robot pose is rolled back.

History AddVisionOnlyState accepts no-parent poses and poses whose immediate parent is root. It rejects a nonroot parent with Error `RobotStateHistory.AddVisionOnlyState.NonFlattenedPose` / `Pose object inside pose stamp should be flattened (%s)` (0x005311E6). It does not flatten the supplied history state itself. Too-old Error is `RobotStateHistory.AddVisionOnlyState.TooOld` / `Pose at t=%d too old to add. Newest time=%d, windowSize=%d` (0x0053111C), gated by the exact unsigned condition in P9. Equality to cutoff is accepted; when newestRaw<=window subtraction is not used for rejection. On an existing vision-only key, assign PoseBase and copy0x5B state bytes; no new node and no cull. On absent key, emplace, then cull only on successful insertion. Defensive failed-emplace Error is `RobotStateHistory.AddVisionOnlyState.EmplaceFailed` / `Emplace of pose with t=%d, frameID=%d failed` (0x0053123C). All these errors set `_errG`, optionally break, return1; success returns0. The PoseTree assignment and full CullToWindowSize are named dependencies, not re-audited here.

**M11-044 overlap:** current title is “PotentialObjectsForLocalizingTo::Insert, UseDiscardedObservation, LocalizeRobot and the confirmed-object pose refresh”, status IMPLEMENTATION_GAP. Evidence already names LocalizeRobot0x0050D1CC..0x0050D4DA and history-composition/cull ranges including0x00531785..0x00531C86 and0x005309D1..0x00530BBE. Its unresolved R-PLAY-AREA entry explicitly records “the Robot+0x2B0/+0x2C6 writes before AddVisionOnlyState” and “the vision-only state insertion and its composition into computed states (none exist, a no-op)”. Those stores and adjoining history remain existing ownership; this report adds the Robot wrapper/SetPose/SetNewPose ordering, defaults and insertion-body reads. M11-044 does **not** already establish SetPose's carried-ID offset/name behavior from its listed evidence. No stronger status is warranted by merely observing the two earlier frame-counter stores.

### 3. New confirmer node fields

The empty-tuple emplace constructor is not `PoseConfirmation(observed,1,0)` and does not initialize sighting count to1. Its fields are:

| Value offset | Node offset | Newly constructed value |
|---|---|---|
| +00..+0B | +18..+23 | default unnamed identity Pose3d, later overwritten by obj pose |
| +0C | +24 | confirmation count0 |
| +10 | +28 | consecutive miss count0 |
| +14 | +2C | unsigned field (observation constructor's third argument)0 |
| +18 | +30 | last-observed word (observation constructor copies observed+1C here)0 |
| +1C | +34 | observed shared object pointer null |
| +20 | +38 | observed shared control pointer null |

All six trailing words are zeroed by one memset at0x00507C50. ObjectID key is separately copied at node+10/+14. ObjectPoseConfirmer::AddInExistingPose then **always** assigns only pose at node+18, returns0, emits no log, notification or sighting increment. Existing key does not reconstruct/zero fields; it replaces reference pose while retaining counters/time/shared object. No last-observed value is copied from the supplied object on this path. Observation-taking constructor0x005062E0 remains the different M11-007 path.

### 4. What RobotDelocalized broadcast actually does

Native external pointer is `[Robot context+4]`, as GetExternalInterface0x00516BCC shows. UiMessageHandler constructor installs vtable0x0102FE1C (0x0065FF28 onward); slot18 selects the const-reference immediate Broadcast at0x00662508. The abstract SimpleExternalInterface vtable contains pure virtuals; its empty DeliverToGame implementation is not this concrete UiMessageHandler path.

Order is observable: **deliver packed message to game transports first, then emit an engine-side event synchronously**. Delivery counter increments even with no active transport; packet contains uint16 Size followed by packed E2G message, with engine ID in the internal MsgPacket metadata. UINT_MAX delivery loops connection indices0,1,2. Factory in this APK makes0 DirectGameComms,1 null,2 TCP. Direct route sends the serialized E2G payload (without the internal uint16 length) to GameMessagePort only when connection state+98==2; TCP sends prefix+payload when device/client gates pass. This extracted SDK TCP branch is outside the supported app-boundary policy; its presence does not turn the direct in-process app delivery into a developer-visualizer message.

GameMessagePort buffers bytes under mutex; full capacity sets sticky overflow flag+20 and warning or error depending+21, `GameMessagePort.PushToGameMessage.send_buffer_full` / `No room for size %zd`. When already overflowed it drops silently. Successful nonempty append uses memmove and increments used count; a zero size does not move bytes. DirectGameComms returns1 after calling the port regardless of whether the port dropped; UiMessageHandler ignores transport result. Port drain/Unity consumption and TCP socket internals are outside this callee question's handoff boundary.

Broadcast then samples BaseStationTimer.GetCurrentTimeInSeconds, widening binary32 to double, stores the message tag, copy-constructs the MessageEngineToGame payload in an AnkiEvent, and finds the engine-to-game signal by tag. If present it invokes callbacks immediately in the stored circular-list traversal order, skipping empty function entries, with reference bookkeeping around current/next nodes. If no signal, nothing is queued. It does not use BroadcastDeferred. The message copy is cleared after callbacks complete. Thus UI connection absence does not stop in-engine RobotDelocalized subscribers; external-interface absence suppresses both routes. Concrete subscribers' effects are separate ownership; this finding settles the broadcast interface, not every subscriber body.

### 5. Yellow labels and shipped visualizer scope

The GOT relocation resolves to exported `Anki::NamedColors::YELLOW` at0x00C9743B. Stored bytes are RGBA `(255,255,0,255)`, and SetText reverses the loaded little-endian word0xFF00FFFF to numeric0xFFFF00FF for SetLabel's color. Both labels use that same color reference. The label3 format's integer argument is loaded from PoseOriginList+0C at0x00510C22; this report preserves that load rather than assuming it is a different origin-list field. The new origin's name supplies `%s`.

SetText's formatting/construction still runs without a connection: buffer initialized for255bytes, vsnprintf limit255, SetLabel constructed, MessageViz constructed, then immediately cleared at0x006C0394. There is **no** call to SendMessage or SendVizMessage between construction and destruction, no retained text assignment in VizManager, and no socket send. Both named send bodies are a single `bx lr` anyway.

VizManager retains an embedded UDP client (+4) and nominal connected byte0. Connect can disconnect an existing client, logs `VizManager.Connect` / `VizManager connected.`, sets byte0=1 and returns0, but in this binary makes no UDP Connect call. Disconnect tests byte0, calls UdpClient::Disconnect, clears byte0, logs Info/`Unnamed`/`VizManager.Connect` / `VizManager disconnected.`, returns0. These nominal flags do not gate the SetText body or make its destroyed MessageViz transmit. Accordingly no requested label payload leaves the engine, even if nominally connected to a developer visualizer. This is shipped dead transport behavior, not an approved compatibility departure. Whether a future developer visualizer feature belongs in scope remains a product decision; reproducing network label output cannot be justified by this APK's SetText path.

## Current record claims and disposition

Current records were reread; none was modified:

| Record | Current title / status | Current evidence and finding |
|---|---|---|
| M11-070 | “Delocalize unread callees: Robot::SetPose, AddVisionOnlyStateToHistory, AnyRemainingLocalizableObjects(uint), AddInExistingPose emplace defaults, IExternalInterface vt+0x18; VizManager colour and scope” — RECOVERABLE_GAP | Evidence is context `Unread`:0x0051502C,0x0051513C,0x00626EF4, PLT0x004A6FE8,0x00510D94,GOT0x0103E7DC. This report supplies those reads for checking. |
| M11-069 | “BlockWorld::UpdateObjectOrigin: bucket lookup, same-root gate, FindRoot re-key with overwrite, AddInExistingPose, cascaded erase; and Robot::Delocalize's full sequence (old-origin capture, pose resets, logs, carried set, OnRobotDelocalized tail broadcast)” — IMPLEMENTATION_GAP | Evidence is adopted U1-U13/D1-D21 context and0x00620920..0x00620A04. No contradiction to the listed caller sequence; P4 clarifies final derived pose after the identity reset. |
| M11-044 | “PotentialObjectsForLocalizingTo::Insert, UseDiscardedObservation, LocalizeRobot and the confirmed-object pose refresh” — IMPLEMENTATION_GAP | Predicate mode3/all origins and frame-counter stores already appear in unresolved corrections; new explicit-origin branch and SetPose/history wrapper detail do not imply whole localization completion. |
| M11-007 | “Two misses before an object pose is forgotten, and two sightings before one is confirmed” — IMPLEMENTATION_GAP | Evidence includes0x004E49AC..0x004E4A30 and confirmer observation ctor0x005062E0; unresolved already mentions AddInExistingPose. The observed-count1 constructor must not be substituted for this empty-tuple default ctor. |

No settled record is newly challenged by these reads. One context premise needs refinement: visualizer output does not merely require a developer connection; the requested label/send paths are compiled out in this shipped binary. Source-backed survival and history behavior do not establish live wiring or permit status promotion. No additional operator approval is requested by this extraction.

Remaining boundaries: full pose-tree primitives (M11-066), full history cull/composition (existing history records), port drain/app consumption, each in-engine RobotDelocalized subscriber, and external TCP runtime remain outside the bounded callee reads. Exact source field names for the confirmer's unsigned+14 and last-observed+18 words are UNKNOWN; their initializer values and later constructor stores are established above. No hardware-only assumption fills them.

Local evidence: [disassembler](20261013-delocalize-callees-disasm.py), [initial reads](20261013-delocalize-callees-initial.txt), [helper/default entry/history](20261013-delocalize-callees-bodies.txt), [interface](20261013-delocalize-callees-interface.txt), [final focus](20261013-delocalize-callees-final-focus.txt), [default state](20261013-delocalize-callees-defaults.txt), [slot checks](20261013-delocalize-callees-slot-check.txt), [transport](20261013-delocalize-callees-transport.txt), [tail](20261013-delocalize-callees-tail.txt), [relocations](20261013-delocalize-callees-meta.txt). No binary execution, builds, tests, hardware, network contact, uploads, branches, worktrees, commits or pushes in this research turn. Writes only under research; stop after answer.
