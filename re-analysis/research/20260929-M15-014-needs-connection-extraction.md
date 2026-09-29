# M15-014 extraction — needs connection and per-serial persistence

Read-only extraction, 2026-09-29. Citations are Thumb VAs in the shipped
`resources/lib/armeabi-v7a/libcozmoEngine.so`. The Ghidra decompilation was used
only to navigate; the cited ranges were opened in the binary disassembly.

## Production path

| step | what the original does | citation | record | classification |
|---|---|---|---|---|
| Startup device read | `NeedsManager::InitInternal` resets the state, clears the device-data and upgrade flags, attempts to read the fixed `needsState.json`, stores the Boolean result at +0x1C9, sends the default state if the read failed, and then calls `WriteToDevice(this, true)` unconditionally. Thus the ordinary device copy exists before a robot serial is acquired. | `0x00693444..0x0069348E`, especially read `0x00693454..0x0069346C`, failure send `0x00693470..0x00693478`, write `0x0069347A..0x00693480` | M15-014 | EXACT_SOURCE |
| Device read result | `AttemptReadFromDevice` first tests whether the named file exists. If present it calls `ReadFromDevice`; only a successful read snapshots its timestamp, applies elapsed-time decay, sends the needs state, increments the background counter and returns 1. Missing and failed files return 0. | `0x00693690..0x00693790`; existence branch `0x0069369A..0x006936A0`; read `0x006936A2..0x006936AE`; success work `0x006936B0..0x0069371E`; failures `0x00693720..0x0069378E` | M15-014 | EXACT_SOURCE |
| `ReadFromDevice` failure contract | `ReadFromDevice` clears its out `versionUpdated` Boolean first. A failed `readAsJson`, or a state-file version greater than 5, returns 0. A supported version loads `_DateTime`, `_SerialNumber`, needs/repair/star fields and version-dependent fields, marks the device NeedsState dirty, updates brackets, sets the out Boolean only where an old format needs rewriting, and returns 1. | `0x006998B4..0x00699BB8`; out flag `0x006998CE`; JSON read/failure `0x006998E8..0x00699988`; version check `0x006999B0..0x006999C0`; serial load `0x00699A1C..0x00699A2C`; successful tail `0x00699B90..0x00699BB0` | M15-014 | EXACT_SOURCE |
| Successful-handshake registration | Once firmware validation succeeds, `RobotInitialConnection::OnNotified(0, fw)` sets validation true, registers a one-shot external-interface subscription for RobotToEngine tag `0xED`, retains its handle, and sends EngineToRobot `GetManufacturingInfo` tag `0x25`. This subscription, rather than a generic generated `MessageHandler` dispatch table, owns the missing edge. | registration/tag `0x0052DE04..0x0052DE40`; request construction/send `0x0052DE5E..0x0052DE80` | M15-014 (also M1-028) | EXACT_SOURCE |
| Inbound message and field | RobotToEngine tag `0xED` is `mfgId`. Its payload is exactly three little-endian `u32` fields (12 bytes). The callback obtains the payload, stores word 0 at `RobotInitialConnection+0x24` (serial), word 1 at +0x28 (hardware version), and validates the low byte of word 2 as body color. The serial supplied to needs is therefore `mfgId.word0`, not a `BodySerialNumber` message. | callback payload accessor/store `0x0052E2F8..0x0052E31C`; generated three-word pack/unpack/size `0x007B1750..0x007B177C`, `0x007B17E6..0x007B1820`, `0x007B1856..0x007B1858` | M15-014 | EXACT_SOURCE |
| Direct caller of the supposedly uncalled wrapper | The registered `mfgId` callback sends the successful connection response, calls `ReadLabAssignmentsFromRobot(serial)`, then loads the same serial from +0x24 and directly calls thunk `0x004A9B2C`, whose body is `RobotInterface::MessageHandler::ConnectRobotToNeedsManager` at `0x0069DEE4`. The earlier “no direct caller” result missed this `blx`. | callback ordering `0x0052E39C..0x0052E3B2`; direct call `0x0052E3AE..0x0052E3B2`; wrapper `0x0069DEE4..0x0069DEE8` | M15-014 | EXACT_SOURCE |
| Wrapper chain | The message-handler wrapper replaces `this` with its `RobotManager` at +0x20 while preserving the serial in `r1`; `RobotManager::ConnectRobotToNeedsManager` obtains the context's `NeedsManager` and tail-calls `InitAfterSerialNumberAcquired(serial)`. | `0x0069DEE4..0x0069DEE8`; `0x0052FADC..0x0052FAE6` | M15-014 | EXACT_SOURCE |
| Ordering versus the connection response | `SendConnectionResponse(Success)` runs before both the lab read and needs connection. Its UI broadcast delivers to the game and the engine subscribers synchronously before returning to the `mfgId` callback. Only afterward does the callback call the needs wrapper. | callback `0x0052E39C..0x0052E3B2`; response body `0x0052DF2E..0x0052DF64`; UI broadcast `0x006625C6..0x006625FA` | M15-014 (ordering also M1-028/M1-041) | EXACT_SOURCE |
| Serial installation | `InitAfterSerialNumberAcquired` copies the prior device-loaded serial at +0x34 to +0x1CC, stores the inbound `mfgId.word0` at +0x34, clears robot-data and robot-upgrade flags +0x1C8/+0x1CA, and calls `StartReadFromRobot`. | `0x006943A0..0x006943F8`, especially `0x006943A8..0x006943B0` and `0x006943EC..0x006943F8` | M15-014 | EXACT_SOURCE |
| Robot persistence read | `StartReadFromRobot` queues NVStorage read key `0x194000` on the connected robot's NV component. If queuing succeeds it returns 1 and resolution waits for the callback. If queuing fails it logs an error, clears +0x3D0, returns 0, and `InitAfterSerialNumberAcquired` immediately calls `InitAfterReadFromRobotAttempt`. | NV read `0x006944B4..0x006944D0`; success `0x006944E6..0x006944EA`; failure `0x006944EC..0x0069453E`; immediate fallback `0x006943F8..0x00694402` | M15-014 | EXACT_SOURCE |
| Robot-read callback | The NV callback clears +0x3D0, calls `FinishReadFromRobot(data,size,result)`, stores its Boolean result at +0x1C8, and always tail-calls `InitAfterReadFromRobotAttempt`. A missing NV item, other NV failure, future version, or unsupported old version makes `FinishReadFromRobot` return false; supported versions 1–5 are unpacked (old versions set +0x1CA for rewrite). | callback `0x0069BEB2..0x0069BED4`; result handling/version gate `0x00699DB0..0x00699F06`; supported unpack/conversion `0x00699E56..0x0069A192`; Boolean return `0x0069A192..0x0069A1AC` | M15-014 | EXACT_SOURCE |
| Per-serial filename and alternate read | `NeedsFilenameFromSerialNumber` reads the newly installed serial from `this+0x34` and returns `needsState_` + its unsigned decimal form + `.json`. During resolution, if the robot has no data, the startup device read did, and the stored file serial differs from the incoming serial, the manager attempts this alternate per-serial file. A missing or failed alternate is logged as possible for a brand-new robot and resolution continues with a false alternate-read result. | filename `0x00695224..0x006952BA`, serial load `0x0069523A`; alternate condition/read `0x006949FA..0x00694A0A`, `0x00694BCE..0x00694BE4`; failed alternate `0x00694C3A..0x00694C90` | M15-014 | EXACT_SOURCE |
| Resolution and device write | `InitAfterReadFromRobotAttempt` resolves the robot/device copies. Neither copy uses current defaults and schedules writes to both; robot-only selects robot data for a device write; matching copies compare timestamps and select the newer; mismatched stored serials select robot data and clear the old disconnect/app-background timing fields. When the resolved-device-write flag is set it stamps the selected state with `system_clock::now`, calls `WriteToDevice(this,false)`, and marks +0x1C9. Independently, its robot-write flag can call `StartWriteToRobot`. | decision tree `0x0069481C..0x00694D06`; neither `0x00694B06..0x00694BC8`; serial compare `0x00694A4E..0x00694AA6`; timestamp choice `0x00694960..0x006949B8`, `0x00694BEA..0x00694CE2`; device write `0x00694DA2..0x00694E4C`; robot write `0x00694E50..0x00694EE4` | M15-014 | EXACT_SOURCE |
| Missing reply versus zero serial | If the robot never supplies `mfgId` tag `0xED`, the registered callback never runs: there is no successful connection response, no `ConnectRobotToNeedsManager`, no per-serial resolution, timer, or retry; only the startup fixed-file state remains. If `mfgId` arrives with word 0 equal to zero, there is no zero guard: zero is installed and the per-serial filename is `needsState_0.json`. | subscription/request and callback edge `0x0052DE04..0x0052DE80`, `0x0052E2F8..0x0052E3B2`; no timer/retry in `0x0052D168..0x0052E3B8`; unguarded serial store/pass `0x0052E308..0x0052E3B2`; decimal filename `0x00695224..0x006952BA` | M15-014 | EXACT_SOURCE for engine behavior; whether/when the robot omits the reply is HARDWARE_ONLY |

## Contradictions with the current record text

- The current M15-014 unresolved text says the generated or indirect registration
  calling `0x0069DEE4` was not recovered. That is contradicted. The successful
  initial-connection path registers tag `0xED` at `0x0052DE04..0x0052DE40`, and its
  callback directly executes `blx #0x004A9B2C` at `0x0052E3B2`.
- The serial is not supplied by a standalone body-serial response. It is word 0 of
  RobotToEngine `mfgId` (tag `0xED`), requested by EngineToRobot
  `GetManufacturingInfo` (tag `0x25`).
- The record title's broader persistence lifecycle is supported, but its present
  `RECOVERABLE_GAP` reason is no longer live: the missing production-path edge and
  its ordering are now recovered.

## Weak evidence

- The names `mfgId` and `GetManufacturingInfo` come from the generated CLAD symbols;
  their behavioral facts are independently fixed by the tag immediates, the
  three-word generated serializer, and the opened callback instructions.
- The robot's decision to send or omit tag `0xED` is firmware behavior outside this
  native image. The app-side consequence of omission is exact; occurrence is
  HARDWARE_ONLY.
- The complete field-by-field conflict resolver is large. The rows above cover every
  branch that changes which persistence source is selected or whether device/robot
  writes occur; telemetry-string construction and cleanup do not change behavior.

## Open questions

- None for the requested M15-014 edge. The subscription, inbound tag and serial field,
  wrapper chain, read ordering, write ordering, and failure paths are established.
- Robot-firmware production of `mfgId` remains outside scope and HARDWARE_ONLY.
