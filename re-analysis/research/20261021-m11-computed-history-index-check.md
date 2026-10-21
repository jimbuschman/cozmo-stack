# Computed history index producer review — partial (2026-10-21)

Examined HEAD19961e92 and report `20261021-m11-computed-history-index.md`
(bec5a0dc). Independent instructions from shipped libcozmoEngine.so, Thumb.
Coverage: main successful/error-return flow531B84..1C94 and scalar map helpers
53254C..5C4/5326D0..74A. Not the report's complete constructor/reset/global
writer census, state-emplace constructor, Get/IsValidKey or marker consumer.

**Verdict: producer contract checked for the rows below; implementation gap
confirmed.** No new behavioral defect introduced by B-M13-9. No builder release,
manifest edits, tests or hardware work.

## Independently checked rows

- ComputeStateAt result at531BAA: nonzero sets state-output null atBB0 and
  returns that result. No optional-key store occurs on this branch.
- Find uses the returned-time reference r5, not the original request value.
  Existing state keeps its node, assigns pose and copies0x5B bytes atBD0..BEA.
  State address is written before optional-key handlingBF2/BF6.
- Existing state with no key-output pointer leaves indices/counter alone.
  With key requested, map+30 subscript helper53254C keeps an existing value or
  constructs (timestamp,0) at5325AC/5AE, then531C14/C82 writes that key. This
  branch does not increment or repair map+24.
- A newly inserted state sets its output address at531C36 before auxiliary
  work. The global is loaded, incremented and storedC42/C44/C46. The tuple
  retains its addressC3E, not a preincrement scalar snapshot.
- First insertionC54 targets map+24 and dereferences (counter,time); second
  C70 targets map+30 and dereferences (time,counter). The generic helper loads
  each referenced scalar at5326E4..6F0. Thus both use the incremented value.
- Scalar unique-emplace helper5326FE..726 searches unsigned keys; duplicate
  returns existing node/false at72A/72C without mapped-value overwrite, then
  frees the unused candidate. Both caller results are ignored. Do not replace
  these two unique insertions with dictionary indexer overwrite.
- Optional key is checked only after both new-state insertionsC74/C78;
  nonnull reloads the counter and writesC82. Null does not suppress allocation.
  Success result0 is setC86. No zero-skip/collision-retry branch exists here.

The report's exceptional state-emplace-false, unwind, initial/reset and
cross-instance claims were not certified by this slice. The instruction-local
increment is u32; no complete other-writer/locking proof is inferred.

## Current record and code

Current M11-044 is IMPLEMENTATION_GAP, titled
“PotentialObjectsForLocalizingTo::Insert, UseDiscardedObservation, LocalizeRobot
and the confirmed-object pose refresh”. Its evidence starts with
0x0050CD70..0x0050CDFC, Insert0x0050CE00..0x0050D170, LocalizeRobot0x0050D1CC..0x0050D4DA,
AddAndUpdateObjects0x00620AD4..0x00621330 and the gap3 report; another entry names
GetComputed0x00531CCC..0x00531D06. Its unresolved explicitly retains the missing
auxiliary producer after0x00531BAA. Existing evidence/ownership must be reconciled
before treating these newly reviewed rows as approved build scope.

NativeRobotStateHistory.ComputeAndInsertCore currently computes a full temporary
then inserts only its Pose into a timestamp cache and emits a specific MISSING.
It exposes bool/origin output rather than the native result/state/key contract.
The two index dictionaries have cull/clear uses but no live producer. This agrees
with the report's gap claim; seeded index tests cannot close it. No new key or
state carrier should be fabricated from timestamp or origin ID.

Next review unit: state-emplace/full-state ownership, native Get/IsValidKey
outputs, initialization/reset and the actual marker key consumer. Then prepare
an explicit owning inventory record and integration approval for one bounded
producer/reader/consumer build. This partial check alone does not release it.


## Follow-up: core reader/state/reset checks (HEAD3609db0c)

Independent native reads extend coverage:
- GetComputed531CCC..1D6C: exact timestamp miss leaves outputs untouched;
  hit stores state address1D12 before optional key lookup. Null key-output
  succeeds without the index. Missing timestamp→key warns and returns1 after
  the state address was written, preserving key output. Hit copies key1D2C.
- IsValidKey53214C..2186 tests exact unsigned membership in map+24 only. It
  does not check the reciprocal index or reject0 specially.
- New state node constructor532664..26B6 allocates0x7C, stores timestamp,
  copies pose via532692 and0x5C state bytes via5326AA. Existing-state update
  remains the previously checked in-place assignment and0x5B copy. Internal
  C# layout need not reproduce allocator/padding, but state and alias effects do.
- State-emplace-false branch531C96/98 sets result1 and cleans the temporary;
  it does not overwrite state/key outputs. Supported single-thread reach of
  this exceptional branch is still unestablished; do not manufacture a race.
- Constructor53088C..8C6 initializes five local trees, local+38=0/window3000;
  Clear5308C8..96C clears the local trees and+38. No direct global store exists
  in these bodies. Reused tree destruction is not a new global alias census.
  ELF dynamic symbol currHistStateKey_ is at1059ED0, size4, as reported.

Marker boundary independently inspected at654D60..DA0,654E50..E92,
654F04..F64 and654FDA..5012: it requests a generated key with flag1, checks
result/origin/rotation, then invokes IsValidKey on the returned key only when
its marker camera word matches the component camera word. Invalid takes the
warning/skip branch. Current code has no such key filter. This verifies the
existence/order of this consumer, not its entire camera-copy provenance or
marker lifecycle. That consumer remains M11-050 work, outside the proposed core.

**Core handoff conclusion:** the reviewed producer/reader semantics are enough
for a bounded full-state/key-store implementation using existing checked pose
and query primitives. Proposed owning record M11-098 and HELD job B-M11-18
are specified in the job packet. This is not whole M11-044 settlement. Full
marker assembly, camera identity, borrowed-state consumer census, arbitrary
counter writers/aliases and native inter-thread race behavior remain unclaimed.
A shared serialized counter is required for the known engine-owned calls;
no global absence proof or custom concurrent host API is being promised.
