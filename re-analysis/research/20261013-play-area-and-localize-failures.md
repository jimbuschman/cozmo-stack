# Play-area metric, localization failures and cliff dispatch

## 1. Play-area value — coverage first

| Step | Native behavior / coverage | Citation | Owner |
| --- | --- | --- | --- |
| Event gate and sample order | Read: strict `now > last+60`, save now, fetch current helper and call +0x1C before clearing the pose-change list or processing this frame's markers. | `0x00624EE8..0x00624F7E`, especially `0x00624F16..0x00624F4E` | M11-064 |
| Virtual target | Resolved ELF relocation: MemoryMap installed vptr `0x01030964`, slot +0x1C at `0x01030980` → `0x006816E9` (Thumb). | vtable symbol `0x0103095C`; slot `0x01030980` | M11-064 |
| Complete getter | Read all 10 bytes: return cached double at MemoryMap+0x40. No traversal, calls, logging or recomputation. | `0x006816E8..0x006816F0` | M11-064 |
| Cache placement / initialization | MemoryMap contains QuadTree at +8; QuadTree contains processor at +8; processor+0x30 is the double accumulator, initially zero. | `0x00681380..0x00681396`; `0x00684C10..0x00684C24`; `0x00688E42` | M11-064 / M13 boundary |
| Type-change update | Read whole processor body, including known/leaf tests, float area arithmetic, double addition/subtraction and unrelated type-index/border maintenance. | `0x00688E50..0x006890A8`, area portion `0x00688E58..0x00688EBE` | M11-064 / M13 boundary |
| Destruction update | Read whole body: non-Unknown leaf subtracts area; tracked-type set/border updates also run. | `0x0068928C..0x0068934C`, area portion `0x006892D2..0x006892FE` | M11-064 / M13 boundary |
| Type names | Read string-table accessor and resolved all 12 table entries, rather than using C# enum names as evidence. | `0x00684950..0x00684962`; table `0x010309D0..0x010309FC` | M11-064 |
| Mutation order | Read subdivision/merge and force-set bodies: callback order can preserve an old parent contribution after subdivision. | `0x00687518..0x006875E6`; `0x00687BEC..0x00687DC2`; `0x00687F2A..0x0068809E` | M11-064 / M13 boundary |
| Global cache-writer closure | Bounded processor sweep found constructor/type-change/destruction cache stores. A whole-binary alias/copy writer proof and runtime numeric corpus were not performed. | local `20261013-play-area-and-localize-failures-data.txt`; UNKNOWN beyond named paths | M13 boundary |

### Answer

Request: `re-analysis/research/requests/20261013-play-area-and-localize-failures.md`. Read first: PROJECT_STATE, research README, the extractor rules, current M11-064/M11-063/M13-031 records and `20261013-blockworld-flag-and-map-helper.md`. This is an independent local extraction, not a fidelity approval. No code, manifest, inventory, project-state, branch or commit changes; no upload or network call. No pull was requested or performed on this turn.

Primary binary: `resources/lib/armeabi-v7a/libcozmoEngine.so`, SHA-256 `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`. Native citations use ELF virtual addresses. Thumb-bit pointer values are distinguished from instruction addresses. ELF segment mappings and relocations were used; decompiler data labels can differ from ELF addresses. Capstone **skipdata was enabled** for this request. Literal pools and switch tables were treated as data, not as extra executable branches.

The play-area getter is `MemoryMap::GetExploredRegionAreaM2`, not a bounding rectangle or a traversed union of polygons. The complete body reads the double at +0x40 and returns it in r0/r1 (`0x006816E8..0x006816F0`). The event formats that double with `%.2f`, empty metadata, key `robot.play_area_size` at `0x00624F4E`; it does not multiply the returned value by another unit conversion. It samples before this UpdateObservedMarkers call processes its observations. The strict 60-second gate emits at most one event per invocation and stores the actual current time; it is not a catch-up loop for missed intervals.

The cached quantity is the processor's **explored-area accumulator in square metres**. For a node with float side length S in mm, its contribution is computed as:

```
x = float32(S * float32(0.001))   // constant bits 0x3A83126F
a = float32(x * x)
A = float64(a)
```

The accumulator update is double addition/subtraction of A, in callback order. It is not double multiplication of S or a final traversal sum, so arithmetic/order differences must not be silently normalized.

The type-change callback receives `(node, oldType, oldNoArea)`; ForceSetDetectedContentType constructs `oldNoArea = (oldType==0 || node currently has children)` at `0x00687530..0x0068754A`, and passes it only if the resulting type byte differs (`0x006875C0..0x006875D8`). The processor computes `newNoArea = (newType==0 || node currently has children)` and applies:

- oldNoArea true, newNoArea false: add A;
- oldNoArea false, newNoArea true: subtract A;
- otherwise: leave the explored-area cache unchanged.

The callback's other operations update the separate InterestingEdge area (+0x38), tracked-type sets (mask `0x7C8`) and border validity. **That mask is not the explored-area type filter.** OnNodeDestroyed independently subtracts A only when the node is a leaf and its content type is nonzero; it also maintains the type sets and border cache. Neither callback directly emits a DAS log or game/robot message.

Thus the area predicate counts **every nonzero content type**, not merely navigable/free cells. The shipped type table is:

| Value | Shipped name | Area predicate |
| --- | --- | --- |
| 0 | Unknown | Excluded |
| 1 | ClearOfObstacle | Included if leaf |
| 2 | ClearOfCliff | Included if leaf |
| 3 | ObstacleObservable | Included if leaf |
| 4 | ObstacleCharger | Included if leaf |
| 5 | ObstacleChargerRemoved | Nonzero predicate; ForceSet converts incoming 5 to stored 1 |
| 6 | ObstacleProx | Included if leaf |
| 7 | ObstacleUnrecognized | Included if leaf |
| 8 | Cliff | Included if leaf |
| 9 | InterestingEdge | Included if leaf; separate edge-area cache also exists |
| 10 | NotInterestingEdge | Included if leaf |
| 11 | ERROR_COUNT_SHOULD_NOT_BE_USED | No range exclusion in the area predicate; not established as legitimate stored content |

The getter has **no traversal order**. Where callbacks walk children, they use vector order. Subdivide creates quadrants ++,+−,−+,−−, then force-sets child contents in that order (`0x00687D90..0x00687D9E`), then changes the parent to Unknown (`0x00687DA0..0x00687DAE`). Auto-merge calls ClearDescendants before force-setting the parent (`0x0068807E`, `0x00688088`). Descendant clearing is child-vector depth-first, with OnNodeDestroyed after each child's descendants and before that child is freed (`0x00687E1C..0x00687E76`, independently read in the preceding cliff-helper pass). The resulting delta order is material to floating-point accumulation.

**Source-derived counter wrinkle:** do not claim the cached value is always a freshly computed sum of current non-Unknown leaves. When an already known leaf is subdivided, all four new children begin Unknown and are then force-set to the old known content, crediting their areas. At the subsequent parent force-set, children already exist, so oldNoArea is true even though the parent formerly contributed area as a leaf. NewNoArea is also true because it becomes Unknown. The callback does not remove that old contribution. There is no preliminary OnNodeDestroyed or area subtraction in the inspected Subdivide body. Consequently that sequence adds the children's contributions without deleting the prior parent contribution. This is a deduction from the complete bodies above, not a runtime measurement or a proposed correction. Initial subdivision of an Unknown leaf does not have a prior known contribution to retain. The emitted metric must preserve the actual cached value; an idealized geometric area is a different claim.

### UNKNOWN / record impact for item 1

The getter, units, byte predicate and named cache updates are answered. No emulator/numeric corpus was run to measure accumulation drift. A global proof that no alias/copy operation elsewhere changes processor+0x30 was not completed. The bounded processor store scan found the three direct stores above; unrelated +0x30 stores to stack/other objects were not treated as cache stores. The full map insertion/merge lifecycle remains owned by navigation records; this request does not approve it wholesale.

Current M11-064 is **RECOVERABLE_GAP**, title “UpdateObservedMarkers unread pieces: the robot.play_area_size metric (MemoryMap vt+0x1C) and the LocalizeToObject failure returns that leave BlockWorld+0x74 stuck at 1”. Its evidence names the event at `0x00624F16..0x00624F32`, unread helper slot, and failure path. It makes no contrary settled metric claim. The cache-subdivision finding is additional behavior for the manager to check; no manifest status is changed.

## 2. LocalizeToObject failures — coverage first

| Step | Coverage / behavior | Citation | Owner |
| --- | --- | --- | --- |
| Parameters and common return | Read: r0 Robot, r1 observed object (may be null in API), r2 existing object; return is saved r7 at common epilogue. | `0x005154BA..0x005154BE`; `0x00515A9C..0x00515AA4` | M11-064 |
| All nonzero exits | Read full native body and enumerated eight failure conditions in the table below, with direct logs and code sources. | `0x005154B0..0x00515AA4` | M11-064 |
| Rejigger failure | Read: verification failure is logged, but processing continues; it is not an immediate failure return. | `0x0051590C..0x0051592A` → `0x005159B6` | M11-064 |
| Localization candidate gates | Read outer Insert/CouldUse eligibility, recent-tap filter, nonnull matched lookup, observed-object lifetime and two call sites. Complete mutation/invariant closure is UNKNOWN. | `0x0050CE00..0x0050D12C`; `0x0050D170..0x0050D1C8`; `0x006316B8..0x006316E0`; `0x0050D2D4..0x0050D4DA` | M11-064 |
| Caller failure logs | Read single-current-origin error, cross-origin warning/continuation, missing matched-object warning and final result. | `0x0050D30E`, `0x0050D438`, `0x0050D47C`, `0x0050D4D2..0x0050D4DA` | M11-064 |
| History checks | Read computed lookup, AddVisionOnlyState failure gates, current-pose update and SetLocalizedTo; read Vision's pre-localization computed-state insertion. | `0x00531CCC..0x00531D6C`; `0x005310A8..0x00531280`; `0x005135F8..0x005136F8`; `0x0051238C..0x005125A8`; `0x00654D60..0x006550AE` | M11-064 / history boundary |
| Normal reachability conclusion | Null existing and null observed API routes are excluded by the studied caller. Other guards narrow failures substantially. A proof or concrete normal sequence for every remaining invariant failure is UNKNOWN; no claim that the flag can never stick. | Gates above; per-row assessment below | M11-064 remains open question |
| Flag on failure / next call | Read nonzero propagation, error tail bypass, next invocation's clear before flag=1 and fresh processing. | `0x00621304..0x00621320`; `0x00625060..0x006250C2`; `0x00624F74..0x00624F7E` | M11-064 / M11-063 |

### Every nonzero-return path

Use **O** for the observed object (r1) and **E** for the existing/matched object (r2). All eight direct failures use `sErrorF`, empty metadata, no explicit channel argument, set global errG and conditionally call sDebugBreakOnError. Empty format below means the shipped zero-length C string at `0x00BE3F00`, not omitted recovery. errG's ELF symbol is `0x0105DD34`; break-on-error is `0x01051E78`.

| Failure condition | Exact key / format | Log call | Return |
| --- | --- | --- | --- |
| E == null | `Robot.LocalizeToObject.ExistingObjectPieceNullPointer`; `""` | `0x00515598` (key `0x00BE6B23`) | 1, set at `0x005155CC` |
| E.CanBeUsedForLocalization() != exactly 1 **or** double-tap ignore query == 1 | `Robot.LocalizeToObject.UnlocalizedObject`; `Refusing to localize to object %d, which claims not to be localizable.`; E.ID | `0x0051554E` (key `0x00BE6B59`, format `0x00BE6B82`) | 1 via same common early branch |
| O != null and GetComputedStateAt(O.lastObservedTime, &history, &key) != 0 | `Robot.LocalizeToObject.CouldNotFindHistoricalPose`; `Time %d`; O timestamp | `0x0051563E` (key formed at `0x00515638`, format `0x00BE6C34`) | Propagates lookup result saved at `0x00515622`; inspected lookup returns 1 for its failures |
| O == null and robot pose cannot be expressed relative to E pose | `Robot.LocalizeToObject.ExistingObjectOriginMismatch`; `Could not get robot pose w.r.t. to existing object %d.`; E.ID | `0x005157F4` (key `0x00BE6BC9`, format `0x00BE6BFD`) | 1 (`0x0051581E`) |
| O != null, history found, but temporary history pose cannot be expressed relative to O pose | `Robot.LocalizeToObject.ObjectPoseOriginMisMatch`; `Could not get HistRobotState w.r.t. seen object pose.` | `0x0051593E` (key `0x00BE6C3C`, format `0x00BE6C6C`) | 1 (`0x00515982`) |
| O != null and AddVisionOnlyStateToHistory(O timestamp, flattened localized pose, head/lift values) != 0 | `Robot.LocalizeToObject.FailedAddingVisionOnlyPoseToHistory`; `""` | `0x00515794` (key `0x00BE6CA2`) | Propagates result saved at `0x00515778`; inspected AddVisionOnlyState failures return 1 |
| UpdateCurrPoseFromHistory() == false | `Robot.LocalizeToObject.FailedUpdateCurrPoseFromHistory`; `""` | `0x00515A46` (key `0x00BE6D82`) | 1 (`0x00515A72`, also `0x00515A84` after debug break) |
| SetLocalizedTo(E) != 0 | `Robot.LocalizeToObject.SetLocalizedToFail`; `""` | `0x005159F4` (key `0x00BE6DB9`) | Propagates result saved at `0x005159DC`; inspected setter failures return 1 |

The only success epilogue sets r7=0 and Robot+0x2C6=1 at `0x00515A88..0x00515A8C`. Failure cleanup destroys any constructed pose temporaries before returning r7. A malformed O pointer has no protective null/error path beyond the explicit O==null API branch; it must not be invented as a ninth clean failure return.

Rejigger != 0 logs `Robot.LocalizeToObject.RejiggerFailed` through `sVerifyFailedReturnFalse` at `0x00515926`, format `VERIFY(%s): ` with `RESULT_OK == result`. The caller branches to `0x005159B6`, updates history/current pose and calls SetLocalizedTo. It does **not** return the Rejigger code immediately, and a later successful setter writes r7=0. Conversely a later failure produces one of the rows above. The full logging-runtime expansion of the verification helper is a separate boundary.

Other direct nonfailure logs in the body are the debug message on a changed localized-object ID (`sChanneledDebugF`, channel `Unnamed`, key `Robot.LocalizeToObject`, `Robot attempting to localize to %s object %d`, `0x005154F4`) and event `Robot.LocalizeToObject.RejiggeringOrigins` (`Robot %d's current origin is %s, about to localize to origin %s.`, `0x005158A8`). No SDK condition surrounds these failure branches.

### LocalizeRobot's own decisions and failures

The current request's `0x0050D4D2..0x0050D4D6` citation is the **aggregate result epilogue**, not the call instruction. The actual calls to LocalizeToObject are `0x0050D2E4` (single/current origin) and `0x0050D412` (multi/cross-origin path).

- No stored candidates returns 0.
- One origin equal to the current origin uses the single path. It calls with the owned observed-object pointer and nonnull matched pointer. Nonzero result logs **error** `PotentialObjectsForLocalizingTo.LocalizeRobot.LocalizeFailure` (key at `0x0050D54C`), format `Failed to localize to %s object %d.` (at `0x0050D58C`), call `0x0050D30E`; the `%s` uses the observed object's type, `%d` the existing object's ID. It sets errG, conditionally breaks, and returns the original failure code (`0x0050D34C` → `0x0050D4D6`).
- Otherwise it builds a distance-keyed multimap with `greater<float>` and iterates it in that order. If the farthest matched pose is outside the current origin, a current-origin pair's matched pointer may be replaced with a stored ID for relookup after origin changes. Each pair calls LocalizeToObject with a nonnull match, or performs GetLocatedObjectByIdHelper when that pointer was deliberately cleared.
- Missing relookup logs **warning** `PotentialObjectsForLocalizingTo.LocalizeToRobot.MissingMatchedObjectInCurrentFrame`, format `Matched object %d no longer exists. Skipping match pair.`, call `0x0050D47C`; the printed ID is taken from the observed object (`0x0050D470`). It skips the pair without setting a localization failure.
- Each actual failed cross-origin call logs **warning** `PotentialObjectsForLocalizingTo.LocalizeRobot.CrossFrameLocalizeFailure` (key `0x00BE6450`), same failure format, call `0x0050D438`; type and ID both come from E. It sets the aggregate flag to 1 and continues to subsequent pairs. Later success does not erase that earlier failure. The cross-origin warning itself has no local errG write; the inner LocalizeToObject failure already sets it. Final return masks the aggregate flag to one bit (`0x0050D4D2..0x0050D4D6`).
- The preliminary cross-origin adjustment also logs info on channel `BlockWorld`, key `PotentialObjectsForLocalizingTo.LocalizeRobot.StoringMatchedObjectID`, format `Match in current frame not farthest. Storing ID=%d to recheck when encountered while localizing.` Its adjustment is not itself a nonzero return.

### Normal-engine reachability, separated from the existence of branches

The checked context establishes the normal Vision → UpdateObservedMarkers → AddAndUpdateObjects → LocalizeRobot chain. This pass read the caller-side gates rather than interpreting each error branch as proof of normal reachability:

| Failure row | What the normal caller establishes | Can happen during connected, non-SDK running? |
| --- | --- | --- |
| E null | Insert stores a matched pointer. Single path dereferences it before calling. Cross-origin relookup explicitly skips a null result (`0x0050D3EE..0x0050D402`). | **No through these call sites.** No null E is passed by their normal branches. |
| UnlocalizedObject | CouldUseObjectForLocalization checks E's virtual localization permission and Robot::WasObjectTappedRecently. The latter tail-forwards to the same double-tap filter. Insert also rejects disallowed/moving/too-distant candidates. | Guards exclude an ineligible object at insertion. **UNKNOWN for all subsequent mutations**, particularly callbacks between cross-origin attempts. The two checks must not simply be deleted as redundant without an invariant proof. |
| CouldNotFindHistoricalPose | UpdateVisionMarkers computes/inserts the image-time history before producing the marker list, checks current origin and marker timestamp, and checks the cached key before accepting applicable markers (`0x00654D60..0x006550AE`). | First-attempt missing-history theory is guarded. **UNKNOWN for the full multi-attempt/cache-lifetime invariant.** GetComputedStateAt is a lookup, not a recomputation. A connected robot alone is not proof the requested cache entry/key exists. |
| ExistingObjectOriginMismatch | O is an owned observation in both LocalizeRobot call sites. | **No through the studied caller**: its O==null API route is not used. Other callers, including indirect callers, were not globally proven absent. |
| ObjectPoseOriginMisMatch | The temporary historical pose is explicitly reparented to the robot's current origin (`0x005156C2..0x005156CA`). Accepted observed markers use a historical camera pose in the current origin. Earlier cross-origin attempts can Rejigger/Flatten and update objects. | First-attempt roots are guarded by Vision. **UNKNOWN for a complete pose-parent invariant across every later cross-origin callback/mutation.** A concrete normal origin-mismatch sequence was not established. |
| FailedAddingVisionOnlyPoseToHistory | LocalizeToObject obtains a root-relative pose using `0x004DF628`, then AddVisionOnlyStateToHistory constructs a HistRobotState and calls AddVisionOnlyState. AddVisionOnlyState returns 1 for a parent that is not root, timestamp older than its raw-history window, or an unexpected failed unique insertion. | The nonflattened case is guarded by the root-relative helper; timestamp had to pass Vision history checks. **UNKNOWN for a full proof excluding the TooOld and insertion failure paths after all intervening callbacks.** No normal sequence was demonstrated. |
| FailedUpdateCurrPoseFromHistory | The helper computes newest raw timestamp, runs ComputeStateAt, then converts that pose to current world origin. It returns false for computation or conversion failure (`0x005135F8..0x005136F8`). | No SDK gate. **UNKNOWN whether those failures can occur here with all normal history/origin invariants intact.** Connection alone does not settle it. |
| SetLocalizedToFail | Setter returns 1 for E.ID==-1 or a selected marker pose that cannot be expressed relative to robot camera. Matched located objects ordinarily have assigned IDs; localization has already attempted world-origin alignment. | Unassigned-ID theory is guarded by located-object ownership. **UNKNOWN for the complete marker/camera parent invariant** following all previous localization mutations. No normal failure sequence was established. |

Additional primary details used in those assessments: `GetComputedStateAt` (`0x00531CCC..0x00531D6C`) requires the exact timestamp entry in the computed map and, when a key output is requested, a timestamp→key entry. The latter miss warns `RobotStateHistory.GetComputedStateAt.KeyNotFound` and returns 1. `ComputeAndInsertStateAt` (`0x00531B84..0x00531C98`) is a separate operation; Vision calls it with interpolation enabled and obtains a key. `MovementComponent::WasCameraMoving` reads **raw** state through `0x00641898`, so it cannot on its own prove a computed entry exists. `AddVisionOnlyState` error keys are `RobotStateHistory.AddVisionOnlyState.NonFlattenedPose`, `.TooOld`, and `.EmplaceFailed` (`0x005310A8..0x00531280`). The observed timestamp is not changed to a guessed nearest raw timestamp by this report.

**Reachability conclusion: partial, explicitly UNKNOWN.** The instruction bodies and all nonzero results are recovered. This pass has not established a connected, non-SDK execution sequence producing one of the remaining failures, and has not proved all such failures unreachable. Source guards above are evidence against simplistic failure scenarios, not a license to classify the whole stuck-flag path dead. The outstanding work is specific: prove the history-key/window and pose-parent/marker invariants across all LocalizeRobot attempts and their synchronous callbacks. No hardware outcome or plausible scenario is substituted for that proof.

### If a failure occurs: flag and next frame

AddAndUpdateObjects preserves LocalizeRobot's result at `0x00621308`, destroys its candidate map, and returns that result at `0x00621320`. UpdateObservedMarkers sees nonzero at `0x00625060`, logs error `BlockWorld.UpdateObservedMarkers.AddAndUpdateFailed`, empty format (`0x00625078`), sets errG/conditional debug break, cleans up its observed-object map, and branches from `0x006250C2` to the return tail. It bypasses CheckForUnobservedObjects, UpdatePoseOfStackedObjects, the flag=0 store at `0x006250D8`, BlockConfigurationManager::Update and UpdateMarkerlessObjects. The byte therefore stays **1** and any queue entries already produced are retained.

Between that failed call and the next UpdateObservedMarkers invocation, OnObjectPoseChanged still sees flag 1: existing queued ID logs MultipleChanges and is not pushed again; an unqueued first pose (old state 0) logs FirstPoseForObject and is not pushed; an unqueued nonzero old state is appended. SetObjectPoseChanged still runs in every case (`0x00624808..0x006248B8`, checked context). This applies even before the next vision update if robot message processing changes an object; a new cliff still has old state 0 and therefore is not queued.

At the **next invocation**, the optional play-area event runs first, then the list is cleared unconditionally at `0x00624F78`, with no call to the stacked-object consumer beforehand. That discards both old failed-frame entries and intervening entries. It sets flag=1 at `0x00624F7E`, starts a fresh batch, and, on success, consumes that fresh batch on the marker path and clears flag=0. An empty-marker update also clears the old list and later flag, but does not call the stacked-object consumer. Another failure leaves it at 1 again. “Next frame” here means the next UpdateObservedMarkers invocation; this function does not supply a scheduler guarantee for when that invocation occurs.

### UNKNOWN / record impact for item 2

Current M11-063 is **IMPLEMENTATION_GAP**, title “BlockWorld+0x74: the writer of the pose-change queue flag that gates OnObjectPoseChanged logs and queue”. Its current evidence expressly separates the failure reachability question into M11-064. No settled claim is contradicted by finding the failure branches. The remaining per-row reachability UNKNOWNs above must remain visible; this report does not answer them by assuming a normal connection establishes every pose/history invariant. No manifest, test, policy or production change was made.

## 3. Received cliff-event dispatch — coverage first

| Step | Native behavior / coverage | Citation | Owner |
| --- | --- | --- | --- |
| Tick entry | Connection/message processing precedes NeedsManager and UpdateAllRobots. Virtual message-handler call recovered from RobotManager. | `0x004ED62E`, `0x004ED640`, `0x004ED648`; `0x0052F850..0x0052F856` | M11-064 dispatch question / M1 boundary |
| Pop/filter/unpack | Read whole ProcessMessages: enabled gate, connection update, ordered PopData loop, first robot, size/filter gates, exact consumed-size check. | `0x0069D85C..0x0069DB16`, particularly `0x0069D8BC..0x0069D910` | M1 boundary / M11-064 |
| Cliff tag selection in union | Read generated Unpack's native table selection for tag C0 and its CliffEvent constructor/unpack arm. | `0x007B1AF0..0x007B1B0C`; table entry `0x007B1B30`; arm `0x007B1CCE..0x007B1CDC`, constructor `0x007B1EF4` | M2 boundary |
| Subscription registration | Read tag C0, resolved HandleCliffEvent pointer, callable captures and scoped subscription ownership. | `0x00532B7C..0x00532B8A`; GOT `0x0103EA90`; `0x00533234..0x005332C0`; `0x00519F8C..0x00519FEA`; `0x0051CD2A..0x0051CD4E` | M11-064 |
| Broadcast lookup | Read event construction and tag→robot-ID signal lookup, then wildcard signal. | `0x0069DC6C..0x0069DCC8`; `0x0069DE18..0x0069DE7A` | M1 boundary / M11-064 |
| Callback order | Read signal append and synchronous circular-list emission; registered callback's nonvirtual member dispatch resolved. | `0x0051D874..0x0051D8AA`; `0x0069E274..0x0069E2B8`; `0x005379F2..0x00537A0C` | M11-064 |
| Handler receives event | Read entry through Get_cliffEvent; payload is at AnkiEvent+0xC, typed union data+4. | `0x00535548..0x00535580`; getter `0x007B3696..0x007B3698` | M4-038 / M11-064 |
| Other concrete subscribers | Relative rule established (per-robot before wildcard; within signal registration order). Inventory of all dynamically active subscribers is UNKNOWN here. | Signal bodies above | boundary |

### Answer: tag 0xC0 → registered callback, synchronously

ProcessMessages only runs its processing body when enabled byte MessageHandler+0x28 is nonzero. It first updates RobotConnectionManager, then repeatedly PopData. PopData != exactly 1 ends the loop. For each popped packet it increments its received-message count, obtains the first robot, and skips the packet if no robot exists. Empty payload logs an error and continues. For a nonempty payload it obtains robot ID and first-byte tag, calls RobotManager::ShouldFilterMessage, and skips filtered packets before deserializing (`0x0069D8E2..0x0069D8F4`). The manager's filter looks up the initial-connection object and returns 0 if absent (`0x0052FA90..0x0052FAB0`); its pending-initial-connection filter policy is a boundary, not a cliff-specific switch invented here.

The generated RobotToEngine union must unpack exactly the popped buffer length (`0x0069D902..0x0069D910`). Its native switch subtracts 0xB0, bounds-checks the resulting index through 0x45, then uses TBH (`0x007B1B04..0x007B1B0C`). Cliff tag **0xC0** uses index 0x10 and halfword table entry `0x007B1B30`, reaching `0x007B1CCE`. That arm unpacks an existing CliffEvent or constructs it if the old union tag differs. With ProcessMessages' initial tag 0xFF, the constructor arm at `0x007B1EF4` is selected. On success the stored union tag is used for dispatch. Failure to consume exactly the packet length logs MessageUnpack and does not broadcast that message.

ProcessMessages has a special branch only for robotError tag **0xD9** at `0x0069D916`; cliff C0 takes the ordinary broadcast route. A fatal robotError is broadcast, then pending data are cleared and the robot disconnected; that ends the loop, so packets after it are not dispatched in that invocation. This is an actual ordering condition, not a new policy for cliffs.

InitRobotMessageComponent registers cliff handling with `movs r1,#0xC0` at `0x00532B80`, handler pointer from GOT `0x0103EA90` at `0x00532B84..0x00532B86`, and bind adjustment word r3=0. Relocation resolves the handler to `0x00535549` (Thumb entry `0x00535548`). Registration helper `0x00533234` allocates the callable and captures member-function pointer, adjustment word, RobotToEngineImplMessaging this, and Robot pointer. Subscription helper `0x0051CD2A` obtains the **outer tag key**, then the **inner robot-ID key**, and connects it to that signal. The resulting scoped handle is retained in the messaging component's handle vector. The order of different tags in InitRobotMessageComponent does not reorder later received packets.

MessageHandler::Broadcast constructs AnkiEvent with the current timer value widened from float to double at +0, tag at +8, and moved RobotToEngine union at +0xC (`0x0069DC82..0x0069DCAE`). It immediately invokes the lookup/emitter, then clears that event's union. In the emitter:

1. Find the outer tag bucket using `event+8` (`0x0069DE24..0x0069DE30`); no bucket returns without delivery.
2. For an ordinary robot ID, find and synchronously emit that robot's signal (`0x0069DE50..0x0069DE62`).
3. Then find and emit the wildcard-ID signal (`0x0069DE66..0x0069DE74`). The constructor sets wildcard ID **65999** at MessageHandler+0x1C; emitter's base is MessageHandler+4, hence its +0x18 field.
4. If Broadcast itself is called with wildcard ID, iterate all signals in that tag's inner unordered-map chain (`0x0069DE32..0x0069DE4E`); no sorted robot-ID order is imposed on that special branch.

Signal connect inserts the callback before the circular sentinel (`0x0051D874`), retaining previous registration order. Emission (`0x0069E274`) starts at the stored sentinel, skips its empty function, follows its next pointer to the first callback, calls nonempty function objects, reads next after each callback, and releases temporary references. There is no deferred queue between ProcessMessages' broadcast and HandleCliffEvent. The bound callable at `0x005379F2` reads captured member pointer/adjustment/this/Robot and tail-calls the member at `0x00537A0C`; adjustment is zero and the member is nonvirtual for this registration. HandleCliffEvent then calls Get_cliffEvent on event+0xC (`0x00535548..0x00535580`); the generated getter returns union+4 (`0x007B3696`).

Therefore, within an ordinary processing tick, **each popped accepted packet completes its callbacks before the next packet is popped**. The cliff callback runs at its registration position among that robot's C0 subscribers; wildcard C0 subscribers run afterward. Packet order determines order relative to other message tags, not a fixed “cliff priority” among handler names. The tick's native engine sequence processes connection/messages at `0x004ED62E`, NeedsManager at `0x004ED640`, then UpdateAllRobots at `0x004ED648`, so cliff handling is before that tick's robot updates/vision work on this call chain.

### UNKNOWN / record impact for item 3

The concrete tag, union switch, registration, lookup, binding and synchronous ordering are recovered. This pass does not inventory every active C0 subscriber's construction time/lifetime, so it does not claim HandleCliffEvent is first among all possible per-robot C0 subscribers. Pending-initial-connection filtering and transport buffer framing are named interfaces; their complete bodies are not re-extracted here. No handler exception-survival policy is inferred from these shipped calls. The existing same-thread ordering lead is extended with the missing dispatch mechanism; no manifest status is applied.

## Local evidence and stopping point

Local annotated native reads accompany this answer in `20261013-play-area-and-localize-failures-native.txt` and focused `-initial.txt`, `-followups.txt`, `-final-reads.txt`, `-invariants.txt`, `-last.txt`, `-area-order.txt`, `-dispatch.txt`, `-data.txt`. The `-disasm.py` helper enables skipdata and uses ELF LOAD mappings. Disassembly of literal/switch tables is not itself an execution path; conclusions above use the referenced branches and data entries.

No build, regression test, hardware run, commit or fidelity-manifest edit was performed. Item 2's remaining normal-reachability invariants are explicitly UNKNOWN, not replaced with an assumption. Research stops with this report.
