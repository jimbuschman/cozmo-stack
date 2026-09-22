# Check B: outbound cube connection investigation, 2026-09-22

Decision: **do not implement a connection algorithm yet**. The sender and substantial
parts of the native policy are recovered; the complete radio-discovery lifecycle has
not been established. This is a partial source recovery, not a completed production
path. No robot was run. Hardware success would not close the source gap.

## Evidence and coverage

Primary evidence is the ARM library from the official 3.4.0-1204 APK, inspected at
native call sites before using Unity or repository notes. The binary hash and bounded
instruction ranges are in `disassembly/dis_cube_connection.txt`; regenerate with
`tools/recover_cube_connection.py --so <libcozmoEngine.so>` (lief, capstone,
cpp-demangle). The downloaded binary is not added to the repository.

The direct/PLT scan found the two production SetPropSlot constructor calls in
`Robot::ConnectToRequestedObjects`, not in the advertisement handler. It also found
the caller in `Robot::Update` and the BlockFilter callers of `Robot::ConnectToObjects`.
The scan does not resolve every linker thunk, indirect call or inlined construction;
absence from its output must not be interpreted as proof of absence from the engine.

Secondary primary evidence: decompiled Unity `Cozmo.BlockPool/BlockPoolTracker.cs`
(`EnableAutoBlockPool`, `ConnectToSpecificCube`, `SetObjectInPoolInternal`),
`ConnectionFlowController.cs::CubeConnectFlow`, and the generated game messages.
Unity's normal connection flow sends `BlockPoolEnabledMessage` with enabled equal
to `!IsManualBlockPoolEnabled`, with a default discovery interval of zero seconds.
The first-time pull-cube-tab flow can request seven seconds. Manual selection is a
different game-layer route; it is not justification for connecting every advertisement.

## Recovered native path

1. `HandleActiveObjectAvailable` **0x0053391C** accepts light cubes or a charger and
   writes factory ID, type, RSSI byte and the robot timestamp into the discovered
   map at Robot+0x47C. It optionally broadcasts to the game. It sends no connection
   request. `Robot::Update` **0x00514174..0x00514224** removes advertisements older
   than 10,000 robot milliseconds (comparison against 10,001).
2. `BlockFilter::Enable` **0x0061B59C** stores enabled, discovery duration and start
   times. `BlockFilter::Update` **0x0061A79A** does nothing when disabled; otherwise
   it runs discovery and connection updates on a two-second cadence.
3. `UpdateDiscovering` **0x0061A7EC** skips already pooled object types, obtains
   candidates through `Robot::GetClosestDiscoveredObjectsOfType` **0x00518314**
   with threshold **150**, and accumulates candidates by type until the discovery
   interval has elapsed. The helper reads RSSI as an **unsigned byte** and chooses
   `<=` the current minimum, not the conventional signed-RSSI maximum. Do not
   replace this with a familiar signal-strength heuristic. Equal values depend on
   native unordered-map iteration; the concrete type-list data and container tie
   order have not been recovered into a reproducible policy here.
4. `AddObjectToPersistentPool` **0x0061AC5C** rejects a factory ID or type already
   pooled and chooses the first empty entry of five. The pool contains factory ID
   and type; it is not a fixed `slot = cube type` mapping. `Init` **0x0061A1EC** loads
   a saved pool, copies it to the runtime pool, and requests its five factory IDs.
   New discovery similarly copies the pool and calls `ConnectToObjects` at
   **0x0061A9BA**. Persistent-pool load/save and cold-start policy are not reproduced
   by this stack; file parsing was inspected but not fully recovered/tested here.
5. `Robot::ConnectToObjects` **0x00517150**, specifically **0x005173EA..0x00517482**,
   compares each desired factory ID with the slot's current factory ID and queues
   changed entries in the five-element request table at Robot+0x454 (ID and flag).
6. `Robot::Update` calls BlockFilter, disconnected-slot cleanup, then
   `ConnectToRequestedObjects` **0x00514A70**, at **0x0051422A/30/36**.
   The sender loops slots **0..4**. Equal current/request IDs clear the pending
   entry. A nonzero requested ID must be in the discovered map; otherwise the
   request remains pending. It also examines connected slots for duplicate types.
   This guard must be ported with its actual control flow, not invented as a policy.
7. **SetPropSlot really is the outbound request**, tag **0x05**. At
   **0x00514BB4..0x00514BCE**, the sender copies the requested factory ID directly
   to word zero and stores loop index `sl` into the following byte. It sends with
   reliable=true, hot=false, copies advertisement info into the slot, sets state
   **1 (connecting)**, and clears the request. **No factory-ID transformation.**
   Disconnect at **0x00514C34..0x00514C58** sets state **3**, sends **factory ID 0**
   with the **same slot index**, and clears the pending entry.
   `SetPropSlot::Pack` **0x007A11A4** writes four bytes followed by one byte unchanged.
   The canonical/generated `Connect : bool` came from PyCozmo fallback naming and
   is **wrong for this engine**: the byte is a slot index. It would collapse slots
   2..4 and cannot describe slot-zero connection versus disconnection.
8. `Robot::SendMessage` **0x0051349C** dispatches to the interface;
   `RobotInterface::MessageHandler::SendMessage` **0x0069DCF6** checks the connection
   and message filter, packs the union and calls `RobotConnectionManager::SendData`
   at **0x0069DD58**. No reinterpretation of the slot byte appears on this path.
9. `HandleActiveObjectConnectionState` **0x00533B3C** bounds the radio slot at four,
   updates BlockWorld and invokes Robot's connected/disconnected handling.
   `HandleConnectedToObject` **0x005179C0** checks the factory ID against the slot,
   expects connecting/disconnected state, removes the advertisement, sets state
   **2 (connected)** and clears its disconnect timestamp. Merely sending a request
   must never set `Cube.Connected` true.
10. `UpdateConnecting` **0x0061AA8C** waits until five seconds after its connection
    epoch; for an unconnected runtime-pool entry it looks for an alternative of the
    same type using threshold 150. It requests a replacement only if a nonzero,
    **different** candidate is found. This is not a blind same-ID retransmission
    loop. `HandleDisconnectedFromObject` **0x00517BBC** resets an intentionally
    disconnecting slot; an unexpected disconnect sets state **4** and a timestamp.
    `CheckDisconnectedObjects` **0x00514924** checks at two-second intervals and
    resets state-4 slots whose disconnect age is strictly greater than two seconds.

## Decision gate and precise remaining source work

The above establishes the missing request mechanism, but does **not** establish the
complete start-to-finish flow required by this task:

- No production direct/PLT caller of SetAccessoryDiscovery was found in this
  inspection. Robot construction, the update loop, BlockFilter entry points and
  Unity block-pool callers were examined. None establishes when the official
  engine enables/disables radio scanning, or whether it remains enabled throughout
  connection/reconnection. `BlockFilter::Enable` is a pool-policy flag, not proof
  of a radio-discovery command. Follow inline/indirect construction and startup
  configuration/firmware boundary before claiming this part is recovered.
- Finish resolving the native automatic type list, equal-RSSI iteration order and
  persisted-pool initialization, including the native/linker-thunk routes reached
  by `BlockPoolEnabledMessage`. These affect which cube and slot are selected;
  a new managed collection or first-advertisement callback cannot silently stand
  in for them. This remains **RECOVERABLE_GAP**, not hardware-only uncertainty.

Accordingly no connection implementation or codec change is made. The known
sender/codec omissions are separately **IMPLEMENTATION_GAP**. A single proven
sender must not upgrade the still-partial surrounding production path. The next
investigation can resume at these specific boundaries without repeating the
incoming-handler work. The shipped assets/generated schema and repository notes
do not supersede the native slot-byte evidence.

## Current stack and acceptance linkage

Check B -> `Control.Cubes` -> `CozmoCubes.SetDiscovery(true)` -> transport
`SetAccessoryDiscovery` -> robot reports `ObjectAvailable` -> `CozmoRobot.OnData`
-> `CozmoCubes.Handle` records the cube -> tool waits 15 seconds -> discovery off
-> connected/telemetry verdict. There is no request between discovery and waiting
for `ObjectConnectionState`. `WaitForConnectionAsync` also only observes reports.
Incoming reports drive telemetry by object ID. The discovery command has hardware
evidence in the protocol manifest; that is not evidence of native discovery policy.

Before: M4-009 claimed equivalent incoming tracking, M4 source/build gates were
true, and B pointed at **nonexistent M4-010**. After: M4-009 is explicitly scoped to
the incoming-handler subset; B references M4-009, **M4-CUBE-001** (known omitted
request/incorrect codec) and **M4-CUBE-002** (incomplete lifecycle recovery). Both
M4 gates are false. B's briefing and failure diagnostic expose the gaps instead of
advising another battery/range retry for this known omission.

Catalog tests validate the compiled catalog against the real manifest, with a
negative test for M4-010 even on a blocked check. The standalone fidelity validator
also checks literal catalog references. No other hardware behavior was investigated.

The relevant hardware check remains **B / Cube telemetry**:
`hardware-test 172.31.1.1 --only B`. **Do not rerun for a claimed fix from this
commit:** connection behavior is unchanged. Rerun B after the remaining recovery
and implementation are complete; no other hardware check is requested.
