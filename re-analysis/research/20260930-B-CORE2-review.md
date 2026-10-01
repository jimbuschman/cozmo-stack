# B-CORE2 review (Q1)

Date: 2026-09-30  
Baseline: `origin/main` / `9e7b4f5` after `git pull --ff-only`  
Builds reviewed: `aeaf977`, `bd12929`, `4d960de`, `e0f4333`, `caaef86`  
Prior rows checked: `20260930-B-CORE-verify-1-2.md`, `-3-4.md`, `-5-6.md`  
Binary: shipped `libcozmoEngine.so` 3.4.0-1204

## Result

The five batches repair most of the narrow defects named by the Opus verifiers, but the B-CORE2 status is not a fidelity pass. Of the 17 records touched by the five commits, **9 HOLD and 8 have a DEFECT**. The recurring failure is the one called out in `AGENTS.md`: a source-backed inner operation was added, while an unowned or deliberately local bridge remained in the production path.

`python re-analysis/tools/fidelity.py --check` passes. The complete test suite also passes at HEAD; that does not change the findings below.

## Current manifest baseline and per-record verdict

Every item below is currently `IMPLEMENTATION_GAP`; no verdict below silently upgrades it. The quoted text is the current title, followed by the current evidence relevant to this review.

| Record | Current title / evidence quoted from the manifest | Verdict | Binary and C# comparison |
|---|---|---|---|
| M1-041 | “Robot initialisation after a Success connection response, and the gates it opens”; CD17-CD23/CD30, including `0x00513BF6..0x00513C5A`, `0x0051410C..0x0051416A`, `0x006437EE`, `0x006456EC`, `0x00645C32` | **HOLDS** | Completion only returns NV to state 0 (`0x006437EA..0x006437EE`); state-0 Update invokes the idle path (`0x006456CC..0x006456EC`), and AddOneShot invokes it at `0x00645C20..0x00645C32`. `NvStorage.cs:449-466,480-511` matches. The streamer check precedes NV Update in `CozmoEngine.cs:1203-1215`. The SyncTime deadline is f32: timer store `0x0084BC80/0x0084BC8C`, load `0x0084BCA8`, and `vadd.f32 5.0` / `vcmpe.f32` at `0x00513BFE..0x00513C18`; `CozmoEngine.cs:1192-1196` matches. `5.0f` is `0x40A00000`.
| M3-026 | “NV Read(): tag validation, the invalid-tag callback (-6), and the FIFO queue with one request in flight”; `0x00644E2A..0x00644EEE`, `0x006456CC`, `0x006437EE` | **HOLDS** | `NvStorage.cs:339-350,395-419,480-511` validates then enqueues, sends one dequeued request only from state 0, and does not run idle callbacks from completion. Invalid read returns `-6` with empty data.
| M3-030 | “NV completion: the callback or the caller vector sink, the 0x400-chunk broadcast, and SetState(0)”; `0x00643568..0x006437EE` | **HOLDS** | The binary selects callback vs vector at `0x006436B6..0x00643714`, bounds broadcast chunks with `cmp #0x3e8` at `0x00643770`, reports line `0x4A7` at `0x00643790`, then SetState(0) at `0x006437EA..0x006437EE`. `NvStorage.cs:119-132,724-770` fills the sink as blobs arrive, caps at 1000, preserves result/order, and completes without starting the next request.
| M3-033 | “At connection the engine queues 12 NV reads, then the CameraCalib read, then Lab and Needs; ready-to-stream waits for the whole queue”; constructor/read evidence including `0x006512F0/0x00651330`, `0x006583FA`, `0x0052E3AA..0x0052E3B2` | **DEFECT** | The engine gates the FaceAlbum/Enrollment pair in `VisionSystem::Init` at `0x006B0658`. The stack always queues both at `CozmoEngine.cs:828-844`; therefore the claimed queue/order is wrong whenever that gate is false. The narrower fixes—one calibration read and Lab-before-Needs at `CozmoEngine.cs:1423-1439`—hold, but they do not repair the whole record.
| M3-022 | “At connection the NV CameraCalib read is queued; its callback enables vision on every path and on success installs the calibration and starts processing”; `0x006583E2..0x006583FA`, callback `0x0065AB68..0x0065AE80` | **DEFECT** | Callback order now matches: `Camera.cs:998-1021` logs the received coefficients before the hardware-`<=6` zeroing and enables vision on every terminal path. But the live read is sent through `NvStorage.cs:406-419`, whose `NVCommand.Data` behavior at the engine's `+0xE8` vector remains M3-027's unresolved source gap. The current record claims the connection read's whole production path; it cannot hold while that wire step is unowned.
| M3-025 | “NV entry-tag validity and the size tables…”; `0x00644148..0x006443F4`, tables at `0x004D7F41` / `0x00C81064` | **HOLDS** | The batch changed only the two missing failure logs. `NvStorage.cs:206-228` follows the factory/not-factory branches and emits FactoryTagNotFound vs TagIsTooSmall at the corresponding binary exits `0x006442EC` and `0x00644274`; the existing tables and membership logic remain source-derived.
| M3-031 | “NV read retry (7 resends / 8 transmissions, identical resend) and the 5 s timeout (-4, no retry)”; `0x006431E6..0x00643234`, `0x00645C6A..0x00645D7A`, `0x0064575A..0x006457C0` | **HOLDS** | The counter increments before comparing against 8 (`0x00645C6A..0x00645C7C`), hence seven resends and eight transmissions. `NvStorage.cs:560-576,658-685` preserves the last command and the retry/failure log order; `NvStorage.cs:487-510` delivers timeout `-4` without retry. The timeout comparison is strictly `robotClock > deadline`, as in the binary.
| M3-034 | “The connection reads' callbacks and data sinks (progression, inventory, face album, backup, lab, needs)”; six sink addresses including `0x0064CE34`, `0x0063D7C4`, `0x0051DF34`, `0x006A6486`, `0x0069BEB2` | **DEFECT** | Only FaceAlbum/Enrollment and Needs have live sinks. Progression and Inventory are explicit no-ops at `CozmoEngine.cs:826-827`; all eight backups are no-ops at `CozmoEngine.cs:845`; Lab is a no-op at `CozmoEngine.cs:1427`. The test itself admits this at `M3DeviceTests.cs:2162-2168`. The record claims all six classes of callback/sink, so clearing the face album and adding its logs is insufficient.
| M3-035 | “An NV read gets no callback on disconnect or destruction, and its timeout needs a live RobotState clock”; destructor `0x00643E80..0x00643F8C`, timeout `0x0064576A` | **HOLDS** | Removal reaches `NvStorageComponent.OnDisconnected`; `NvStorage.cs:519-522` drops queue, in-flight request, deadline and idle callbacks without invoking the request callback. `SyncedClock` at `NvStorage.cs:441` is the accepted RobotState clock, not wall time. The new test drives the transport removal path rather than calling the helper directly.
| M4-001 | “Head angle limits -0.436332..0.776672 rad…”; `0x00547F44..0x0054803A`, `0x0051007C..0x00510086`, `0x0051335E..0x005133E8` | **HOLDS** | The comparison helpers use epsilon bits `0x3727C5AC` (`1e-5f`) at `0x0084CC64..0x0084CC84` / `0x0084CCAE..0x0084CCD0`; RescaleRadians retains `vcvt.s32.f32` then `vcvt.f32.s32` at `0x0084C91E..0x0084C922`. `Motion.cs:483-579` reproduces these, including infinities and huge finite values, and the tests compare float bits.
| M4-003 | “The head and lift API follows the game-message path…”; game handlers `0x0052ABE0..0x0052AC9E`, teardown `0x00541138..0x0054122A` | **DEFECT** | The batch correctly stops before unlock and checks `AreAllTracksLockedBy` (`Motion.cs:904-919`). It does not implement the live game handler's `ActionQueue::QueueNow`: the engine deletes/replaces the running action at `0x0053E24C..0x0053E35E`, whereas a second stack move sees the first action's track lock and returns `0x03000019` at `Motion.cs:831-844`. `M4ControlTests.cs:713-730` tests an unrelated external-lock case and never tests repeated game moves.
| M4-016 | “Head and lift move semantics: no send when in position, ack matched by id, completion in position and stopped, tolerances and error codes”; `0x00540D4A..0x00540E80` plus MA15-MA17 | **DEFECT** | In the engine, `+0x74` starts negative and is set from the engine clock only on the first `IAction::UpdateInternal` (`0x00540D4A..0x00540D64`); timeout is checked before CheckIfDone and returns `0x03000018` at `0x00540E80`. The stack stamps it immediately in `RunAsync` (`Motion.cs:824-829`), before the action's first `CozmoEngine.Update`. On the game path this can be the prior tick's clock, shortening the 30 s interval by one tick. The second precondition-time gate identified by the Opus report is also still absent. The result code and teardown order themselves hold.
| M4-020 | “RobotState acceptance: time-sync gate, unknown origin ids dropped; origin 1…”; `0x0051293C..0x00512C4A`, `0x00510A24`, `0x0051526E..0x005153AE` | **HOLDS** | The invented special FailedToSend line is gone. `CozmoEngine.cs:983-1009` uses the generic Robot.SendMessage failure, preserves ImageRequest's ignored return, sends the absolute-localization update, and only then stamps `SyncTimeSentAt`. Origin 0/1 and the pre-origin-check state effects are unchanged.
| M1-024 | “Engine tick 60 ms: arrivals drained FIFO and handed up once per tick”; `0x0065B3B2..0x0065B63E`, `0x004ED4DE..0x004ED6C4`, `0x0084BC38..0x0084BCD4` | **HOLDS** | `CozmoEngine.NeedsUpdate` is `Action<float>` and is called with `Timer.SecondsF` at `CozmoEngine.cs:1706,1829`. The binary converts d1 to f32 at `0x0084BC80`, stores it at `+0x10` at `0x0084BC8C`, and returns it at `0x0084BCA8`. Needs stores and computes from the f32 `+0x3AC` value; no double-width seam remains in the live tick.
| M2-002 | “RobotStatusFlag names and values, and where the engine stores each consumed bit”; status stores plus PlaceObject gate `0x005549B0..0x00554A3B` in the current unresolved text | **DEFECT** | The narrow bit order is correct: bit `0x4` latches `+0x84` at `0x005549C0`; clear bit with clear latch stays RUNNING at `0x005549D0..0x005549D6`; then MovementComponent+9 must clear at `0x005549D8..0x005549E0`. `DockActions.cs:888-919` implements those checks, but lines 915-919 intentionally let an unlatched action complete with the dock result. The engine remains RUNNING forever if bit `0x4` never appeared. That LOCAL bridge changes the live result.
| M3-018 | “Colour decode: half-width JPEG, BGR to RGB, cv::resize INTER_LINEAR to 320x240; IsColor; Save at quality 90”; `0x004F21AE..0x004F2440`, `0x00870486..0x008704D6`, `0x004F2102..0x004F211E`, `0x004F2EFC..0x004F2FA8` | **DEFECT** | IsColor now holds, including encoding 0's verify call at `0x004F2120..0x004F2134` and the live VisionSystem call. The decode path does not: the engine calls OpenCV `imdecode` then unconditionally `cvtColor` at `0x004F21DC` / `0x004F2250`; the stack uses StbImageSharp and converts decode failure to a returned error at `Camera.cs:361-386`. `Camera.cs:83-93` also explicitly says Save bytes are not the engine's. Codec, failure result, and save wire bytes therefore remain different.
| M3-010 | “encodeMuLaw(float) exactly; no volume scaling; short frames zero-padded”; encoder `0x00597AD8..0x00597B8E`, PopRobotAudioMessage `0x00597DD4..0x00597E4E` | **DEFECT** | The binary iterates a `vector<float>` in 4-byte steps at `0x00597DFC..0x00597E12` and calls `encodeMuLaw(float)` for each element, then zero-pads to 744. The stack's live audio source is `short[]` and `PopFrame` calls `AnkiMuLaw.Encode(short)` at `AnimationScheduler.cs:1548,1594-1596,1603-1617`. Numerically related short samples do not make this the same production path: fractional, NaN, and the float encoder's warning cannot occur.

## Defects that block acceptance

1. **M3-033 — unconditional face reads.** Engine gate `0x006B0658`; C# `CozmoEngine.cs:828-844`.
2. **M3-022 — calibration read still crosses the unowned M3-027 request-vector step.** Engine request builder/read path `0x0064503E..0x00645484`; C# `NvStorage.cs:406-419`.
3. **M3-034 — four connection sinks are no-ops.** Engine sink callbacks at `0x0064CE34`, `0x0063D7C4`, `0x0051DF34`, `0x006A6486`; C# `CozmoEngine.cs:826-827,845,1427`.
4. **M4-003 — QueueNow replacement is absent.** Engine `0x0053E24C..0x0053E35E`; C# refusal `Motion.cs:831-844`.
5. **M4-016 — action start time is stamped before first UpdateInternal, and the second precondition-time gate is absent.** Engine `0x00540D4A..0x00540D64`; C# `Motion.cs:824-829`.
6. **M2-002 — unlatched status gate has a LOCAL completion bridge.** Engine `0x005549B0..0x005549E0`; C# `DockActions.cs:915-921`.
7. **M3-018 — OpenCV failure/codec/save behavior is replaced by StbImageSharp policy.** Engine `0x004F21DC`, `0x004F2250`, `0x004F2EFC..0x004F2FA8`; C# `Camera.cs:83-93,361-386`.
8. **M3-010 — live PCM is the wrong type.** Engine float-vector loop `0x00597DFC..0x00597E12`; C# short path `AnimationScheduler.cs:1548,1594-1617`.

## Tests and circularity

I found **no remaining literal implementation-vs-implementation equality test** in these five commits. Batch 5 did fix the earlier circular mu-law test: its expected bytes are now hand-derived rather than obtained from the other overload.

Four tests are nevertheless non-probative for the claimed record and would allow the defects above to pass:

- `M3DeviceTests.M3_034_TheConnectionReadCallbacksReachTheirLayers` (`M3DeviceTests.cs:2162-2168`) explicitly treats Progression, Inventory, backups and Lab as no-op sinks, then presents the test as coverage of M3-034's callbacks. It validates the candidate's omission, not the source path.
- `M4ControlTests.M4_003_MA_ALockedTrackFailsTheHeadMoveWith03000019` (`M4ControlTests.cs:713-730`) injects an external lock. It never exercises the production repeated-game-message path where QueueNow replaces the running action.
- `M4ControlTests.M4_016_MA17_TheDefaultActionTimeoutIs30Seconds` (`M4ControlTests.cs:764-772`) checks a public constant only. It cannot detect that `StartTime` is captured before first UpdateInternal.
- `M3DeviceTests.M3_010_C5_C6_PopFrameZeroPadsAndEncodesThroughTheScheduler` drives only the candidate's `short[]` seam. Its byte oracle is source-derived, but the test cannot detect the source's `vector<float>` production-path mismatch. It must not be used to settle M3-010.

The M2-002 test is source-derived for the latch ordering, but it omits the decisive no-bit-ever-arrives case. The M3-018 IsColor tests are source-derived and hold; there is no source test for OpenCV-vs-Stb decode failure or byte-identical Save output.

## Batch-4 flaky test

The flaky test is **`EngineAppLayerTests.M1_025_M1_015_CB33_AFrameInFlightAtTheRemovalLeavesNothingBehind`** (`EngineAppLayerTests.cs:1707-1744`). The B-CORE2 status records one batch-4 push-gate flake followed by a green rerun, and `PROJECT_STATE.md` independently queues this exact test for a deterministic release because its 300 ms timer is a flake risk.

Cause: after the detector signals `Entered`, the test starts a background task that waits 300 ms and releases the blocked frame (`EngineAppLayerTests.cs:1725-1727`). It then asserts `!frame.IsCompleted` and calls the removal tick (`:1728-1730`). On a loaded suite runner, scheduling or GC can consume the 300 ms before the assertion/removal acquires the path, allowing the frame to finish and making either the in-flight assertion or the discarded-frame assertions fail. The green rerun is consistent with that race. The release needs an explicit handshake from the removal path, not elapsed wall time.

## Commit-level comparison with the Opus verifier rows

| Commit | Result |
|---|---|
| `aeaf977` | Narrow on-idle correction **HOLDS**. |
| `bd12929` | Single calibration read, Lab/Needs order and calibration log order **HOLD**; records M3-022/M3-033 still fail whole-path ownership. |
| `4d960de` | Sink persistence, 1000 bound, outcome/retry/face logs and disconnect cleanup **HOLD**; M3-034 still lacks four production sinks. |
| `e0f4333` | Float comparisons, vcvt round trip, stop-before-unlock, owner gate/result and generic send log **HOLD**; QueueNow and exact first-Update timeout start remain defects. |
| `caaef86` | Needs/SyncTime f32 and IsColor correction **HOLD**; M2-002 local completion, M3-018 codec/failure semantics and M3-010 short PCM remain defects. |

## Verification run

- `python re-analysis/tools/fidelity.py --check`: pass, 418 records.
- `dotnet test cozmo-stack/Cozmo.sln --no-restore --verbosity minimal`: **2623/2623 pass** at this HEAD (warnings only).
