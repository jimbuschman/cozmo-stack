# R-M11-HISTORY-INDEX — computed-state auxiliary index producer

Request: `requests/20261021-m11-computed-history-index.md`. Examined HEAD5e1b0a13. Independent read-only extraction, pending checking. Coverage opened before further tracing; OPEN entries replaced below.

| Step | Native range | Production / consumer | Disposition |
| --- | --- | --- | --- |
| Compute result and existing/new state | 531B84..1C2A | NativeRobotStateHistory.ComputeAndInsertCore | Recovered; current code caches pose only |
| Global key arithmetic / both writes | 531C2C..86 | Dictionaries currently seeded only by tests | Recovered increment and unique insert |
| Duplicate/error/return ownership | 531BAA..1CAE;532506..748 | Native outputs not represented by bool/origin adapter | Recovered conditional branches; exceptional concurrency reach UNKNOWN |
| Get / key validity | 531CCC..D92;53214C..186 | GetComputedStateAt pose-only; no IsValidKey recipient | Recovered relevant consumer boundary |
| Live marker caller / cull | 654D60..E92,654F04..5012; adopted cull | VisionSystem.cs:1087; CullCore | Actual call exists, key filter missing |
| Initial/reset semantics | BSS counter1059ED0;53088C..996 | No production allocator | Constructor/Clear do not reset global; exhaustive other writers UNKNOWN |

## Evidence and scope

Read native `resources/lib/armeabi-v7a/libcozmoEngine.so` instructions. Decompiled531B84/53254C/5326D0 files were navigation only. Reused checked ComputeStateAt/interpolation/culling, M11 gap1 marker path and F9 in the earlier pose-history answer. No camera assembly, pose serialization or whole history-map audit repeated.

Full selected function ranges were read, including error cleanup: ComputeAndInsert531B84..1CAE (following literal pool excluded); map find532506..54A; state unique emplace5325F2..662 and constructor532664..6C6; scalar default insert53254C..5C4; scalar unique emplace5326D0..74C; scalar lookup53274E..792; GetComputed531CCC..D92 (following strings excluded); IsValidKey53214C..186. Common allocator/tree balancing and PoseBase primitives retain their existing records, not newly extracted here.

Native naming: history+18 = timestamp→HistRobotState; +24 = generated-key→timestamp; +30 = timestamp→generated-key. Do not swap the two auxiliary maps based on the order of the C# field declaration.

## Native row table

Let T be the **returned-time reference** supplied to ComputeStateAt, S its temporary HistRobotState, C the process-global u32 counter, and optional K the generated-key output pointer. T is not necessarily the original requested timestamp. The state-output pointer is required by this native routine.

| Row | Behavior | Instructions / consequence |
| --- | --- | --- |
| H1 | Construct S, call checked ComputeStateAt(request,T,S,interpolate) | 531B84..BA6; incoming output-state pointer saved in r8, optional K loaded from entry-stack at531BC4 |
| H2 | Compute error propagates; state output becomes null | 531BAA..BB4 stores0 through r8, skips all maps and counter; returns original result through531C88..92. Does not write K. T retains whatever the called query did |
| H3 | Find computed state by T | 531BB6..BCE passes r5 (pointer to T) to map+18.find532506; helper uses unsigned exact lookup, sentinel=history+1C |
| H4 | Existing state: assign pose in place, copy remaining payload | 531BD0..BEA destination node+14; PoseBase.operator=845940 then memcpy4 of **0x5B bytes** from S+0C to node+20; current runner node and key retained |
| H5 | Return existing state's address before optional key work | 531BEE..BF6 stores node+14 through r8. If K null, goes directly to success; no counter/index touch |
| H6 | Existing state with K: timestamp→key subscript semantics | 531BF8..C16 uses map+30 default-emplace helper53254C. Existing timestamp retains its value; missing timestamp is inserted with value0 at5325AC..AE. Loads node+14 and writes K at531C82. No repair of map+24, no counter increment |
| H7 | Missing computed state: unique emplace T,S | 531C18..26 invokes5325F2; constructor allocates0x7C-byte node, copies pose via8458F0 at532692, installs vptr and copies **0x5C bytes** from S+0C at5326AA. State starts node+14 |
| H8 | State emplace false | 531C2A→C96 sets result1 and cleans S; no new key/indices. State-output pointer and K are **not written** on this branch. Not the same output contract as H2 |
| H9 | State emplace true | 531C2C..36 sets state-output pointer=node+14 before either auxiliary insertion |
| H10 | Increment global, then key→time insert | 531C2C..54 loads global counter address through GOT, reads C at531C42, adds1 atC44, stores atC46; emplaces (C,T) into map+24 atC54 |
| H11 | Time→key insert second | 531C58..70 constructs reference tuples for (T,C), emplaces into map+30 atC70. Both unique-insertion return flags ignored |
| H12 | Optional key output after both inserts | 531C74..82 if K nonnull reloads current global C and stores it. Both indices were written even when K=null |
| H13 | Success / cleanup | 531C86 sets result0; C88..92 destroys temporary pose and returns. C9A..CAE exception cleanup destroys S and rethrows; no rollback block reverses a state/counter/first index already inserted |

The reference tuples matter:531C3E saves the **address of C**, not its old numerical value. Scalar helper5326E4..F0 dereferences the tuple members and stores actual u32 key/value. Thus H10 inserts the **incremented** counter. This is not a timestamp hash, per-history counter, post-increment old key, or pointer-valued key.

Scalar unique insertion5326D0 allocates/constructs the candidate first; unsigned tree compares5326FE..726 locate an equal key or empty child. If equal,53272A..2C returns existing node/false;53273C..746 frees the unused candidate without assigning the existing mapped value. If absent,53272E..73A links it and returns true. Hence each auxiliary map uses **first-existing-value wins**, not indexer overwrite, independently. No native consistency check is inserted between H10 and H11.

## Counter, wrapping, duplicates and reset

The ELF symbol `RobotStateHistory::currHistStateKey_` is at **0x01059ED0**, size4 in .bss (NOBITS; loader initial zero). Relocation at GOT0x0103EA58 is ARM_GLOB_DAT for that symbol. This is the address reached from531C30/C5C/C7C PC-relative loads. Ghidra navigation prints0x01069ED0; that rebased decompiler address is not the ELF symbol address.

531C42..46 is ordinary 32-bit load/add/store: modulo2^32, so FFFFFFFF→0. There is no zero skip, overflow branch, collision probing, per-object reset, atomic instruction or lock in this continuation. Initial C=0 makes the first successful new-state allocation C=1, conditional on no earlier writer. Histories share this symbol: constructing another history does not create an independent sequence.

Fresh complete constructor53088C..8C6 initializes five local trees, local+38=0 and window3000. Clear5308C8..96E plus exception cleanup530970..996 clears those five local maps and+38; neither routine writes the global symbol. A later post-Clear insertion therefore increments the existing global C. **UNKNOWN:** exhaustive global writer/reset census outside these functions and process-start .bss initialization; no “there can never be another writer” claim. Also UNKNOWN here: the full external locking/concurrency discipline among multiple history instances. Do not invent an atomic allocator policy or reproduce a proven race without the applicable project rule.

Consequences:
- Recompute T when its state exists: overwrite its payload, retain generated key, no increment.
- Existing state but missing time→key with K requested: insert T→0 and return0 as the key, success result0; key→time remains absent.
- New state when stale auxiliary entries already exist: both unique insertions preserve their existing mapped values, yet K returns current C, not necessarily the value retained in time→key.
- Counter wrap into an existing key: key→time keeps old timestamp; time→key may accept T→C. Function still returns success and C. No repair is visible.
- State-emplace duplicate after earlier find-miss returns1 without clearing outputs. The routine contains that branch, but ordinary single-thread execution has no intervening callback before emplace; **UNKNOWN:** a supported producer that makes this race/duplicate outcome occur. No concurrency behavior inferred.
- Exceptions after partial insertion do not have a transaction rollback in this body. Full allocation-failure policy is outside this job; implementation should follow the project's sensible host-failure rule rather than manufacture an exact exception framework.

## Consumers and actual live join

**GetComputedStateAt(timestamp,state**,optionalKey*)**,531CCC..D6C:
unsigned lower_bound over map+18; fails unless exact T. On hit stores borrowed node+14 first (531D0C..12). If key pointer null, returns0 without needing either index. Otherwise finds T in map+30 (531D16..28); found value is stored at531D2A..2C and returns0. If absent, warns531D32..42 then returns1; state pointer already identifies the state, key output untouched. Initial timestamp miss returns1 and leaves both outputs untouched. Const overload's mutable wrapper531DD0 is a tail veneer; no second allocator.

**IsValidKey(K)**53214C..186 is exact unsigned lookup in map+24. It checks only key existence, not timestamp→key reciprocity or a pointer identity. C=0 is not intrinsically invalid; it can pass if map+24 has key0.

**UpdateVisionMarkers** native live producer:
654D60..D92 tests a non-empty marker list (D74..76), calls history at robot+390 with requested result timestamp [result], T at sp+BC, state pointer sp+B8 and non-null generated-key output sp+B4, interpolate=true. Failure06000000 silently exits (654D9A..9E); other failure logs HistoricalPoseNotFound and exits (D A2..DE6). Success checks state origin (654E50..5C), rotation gate using returned T (E60..7E), then constructs historical camera from returned **full state** (E82..8E). Per marker, timestamp mismatch drops; camera-id match compares copied marker camera first word with component camera first word654F10..18. Only that match requires IsValidKey(K) at654F1A..2A; false warns InvalidHistStateKey654FDA..FEC and drops it. Different camera-id skips that check. Valid marker substitutes historical camera/pose and appends to list654F2C..8C. BlockWorld.UpdateObservedMarkers654DF4 receives the kept list. These fresh relevant ranges agree with R-VIS gap1 4B.2/4B.5; full unrelated camera/marker geometry not re-extracted.

**Other concrete dependencies:** GetComputedStateAt calls without key output can continue to work even while indices are absent, explaining why pose-only tests pass. Reused checked gap1 1A.5: BlockWorld's stationary-robot insert comparison queries last-updated and current timestamps (50CEB4..CF84). Gap1 1B.2: Robot localization queries computed state for observed object's timestamp515612..774. These establish state-map consumers, not proof of index production. The adopted culler erases the timestamp prefix and corresponding **generated-key prefix**, not arbitrary paired deletes; production NativeRobotStateHistory.cs:136..144 already implements that distinct consumer. Reuse its checked semantics rather than replace them with cleanup by pair.

Finite direct immediate-branch scan found ComputeAndInsert body/PLT calls4F42B4,5667F4,566BA4,566C1E,56768C,654D92,67F8A2; IsValidKey654F24. This is not a complete indirect-call or consumer census. This job closes the marker producer/use path above; remaining callers' full feature semantics are **UNKNOWN here**, not excluded or settled.

## Production comparison

Current paths under `cozmo-stack/src/Cozmo.Robot/`:
- `Vision/NativeRobotStateHistory.cs`:27 stores only Pose3d in _computed;29 declares both auxiliary dictionaries. ComputeAndInsertCore301..312 computes full S and returned time but then stores **requested timestamp** with pose alone at310, emits MISSING for native index ownership, returns bool/origin. It does not produce C, either index, full HistRobotState return, native result codes, or generated key. The returned-time vs requested-time requirement is independent of whether particular test inputs currently make them equal.
- InsertComputedStateAt317 replaces/disposes the stored pose; native H4 assigns the existing state's pose in place. A faithful implementation must preserve observable/native borrowed-state lifetime for callers; managed internal containers can differ provided the resulting pose-node/state aliases behave the same.
- GetComputedStateAt318 exposes only Pose3d, no key-result contract. This facade is useful to existing consumers but does not settle the native optional-key overload.
- CullCore136..144 consumes the dictionaries, but no production insertion into either was found by their names. Clear118..119 clears both. There is no corresponding global generated-key allocator.
- `Vision/VisionSystem.cs`:1087 uses offline pose insertion or actual History.ComputeAndInsertStateAt for live results;1088..1108 drops missing history/old origin/mismatched marker timestamps. There is no generated-key output or matching-camera IsValidKey filter. Its stale explanatory comment saying the key table has no counterpart does not constitute implementation.
- World.ComputedRobotPoseAt is genuinely bound to history getter at VisionSystem.cs:94, not just a test setter.

Tests:
- `cozmo-stack/tests/Cozmo.Protocol.Tests/RobotStateHistoryAdmissionTests.cs`:117..127 labels its cull fixture unit reach and **seeds both maps directly**, using generated keys40/20/10 for timestamps500/1500/3000. It verifies the checked prefix-erasure behavior, deliberately not natural key allocation. It cannot prove H10/H11.
- `M11RVisBuildTests.cs`:500..530 tests computed exact-timestamp presence/query failure; later stationary-insert cases use the pose getter. No generated-key or both-index oracle there. Their successful pose results do not demonstrate the missing producer.

## Bounded build recommendation and independent oracles

After manager checking, add the native-shaped internal result/returned-time/state/key contract around the existing checked query; retain the host convenience facade as appropriate. Own full computed state, global counter and both unique-insert maps in the actual history component; bind marker key validity to the returned key. Do not add a guessed per-history counter, latest-only retention, zero skip, overwrite-every-index behavior or blanket map repair. Keep existing checked culling and pose primitives.

Required independent regression expectations, not tests implemented or run by research:
1. Native reference fixture: first new timestamp increments a controlled C and fills both indices even with K=null; a second history uses the same sequence.
2. Recompute same T updates full state at the same returned native state address, retains key, and does not increment. Check the state payload beyond pose.
3. Missing time→key on existing state returns/inserts0 only when K requested; null K leaves map untouched and never creates reverse entry.
4. Controlled stale entries / wrap fixture proves first-existing-value wins in each index, modulo increment and returned K, without inventing collision resolution.
5. Query failure preserves K, nulls state only on Compute failure; exact Get miss and missing-index Get distinguish their already-written state output. Impossible-to-trigger single-thread emplace duplicate should not get a fake production-success test.
6. Live marker test takes actual RobotState/history/result path through production producer and validity consumer. Exercise same-camera-id invalid-key drop and different-camera-id bypass using a native-derived fixture, separately from seeded-index unit tests.
7. Build state/index entries through producer, then run the already checked culler; Clear removes entries but next insertion continues C. No extra full culling extraction needed.

No implementation, tests, hardware, approvals or fidelity statuses changed. Remaining UNKNOWNs above are explicit limits; no whole M11-044 settlement is claimed.

## Current manifest snapshot

Exact current title/status/evidence fields follow; no reclassification.

### M11-036

```json
{
  "title": "VisionComponent::UpdateVisionMarkers into BlockWorld",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "VisionComponent::UpdateVisionMarkers 0x00654D60 calls BlockWorld::UpdateObservedMarkers(list<ObservedMarker>) 0x00654DF4"
  ]
}
```

### M11-044

```json
{
  "title": "PotentialObjectsForLocalizingTo::Insert, UseDiscardedObservation, LocalizeRobot and the confirmed-object pose refresh",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x0050CD70..0x0050CDFC ctor and UseDiscardedObservation; 0x0050CE00..0x0050D170 Insert; 0x0050D170..0x0050D1C6 CouldUseObjectForLocalization; 0x0050D1CC..0x0050D4DA LocalizeRobot; 0x00620AD4..0x00621330 AddAndUpdateObjects (A1..A8)",
    "re-analysis/research/20260929-R-VIS-M11-gap3-insert.md (whole file)",
    "0x00531CCC..0x00531D06, 0x00516EC0, 0x005062E0..0x00506310, 0x00512458, 0x00512468, 0x00512B56..0x00512B8E",
    "0x00512112..0x00512188, 0x00510A24..0x00510A46, 0x0050609C..0x005060C4, 0x0050612C..0x0050614A, 0x0051238C..0x0051254E, 0x00512B56..0x00512BA6, 0x00531785..0x00531C86, 0x005309D1..0x00530BBE, 0x00654D92..0x0065507E (round-2 verifier)"
  ]
}
```

### M11-050

```json
{
  "title": "UpdateVisionMarkers steps between ComputeAndInsertStateAt and UpdateObservedMarkers",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x00654D92..0x006550A0 UpdateVisionMarkers",
    "re-analysis/research/20260929-R-VIS-verify-M11-batchA4.md"
  ]
}
```
