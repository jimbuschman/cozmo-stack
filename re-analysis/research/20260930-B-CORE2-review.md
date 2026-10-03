# B-CORE2 review (Q1)

## Coverage audit (2026-10-01)

| item in scope | coverage | unchecked basis affecting conclusions |
| --- | --- | --- |
| M1-041, M3-026, M3-030, M3-033, M3-022, M3-025, M3-031, M3-034, M3-035 | CHECKED | All changed hunks, verifier objections, decisive binary branches, current C# entries and named tests were reconciled in the closure matrix below. |
| M4-001, M4-003, M4-016, M4-020, M1-024, M2-002, M3-018, M3-010 | CHECKED | Same; float widths/bits, ordering, failure returns and whole-path ownership are explicit below. |
| M13-028, M15-014 | CHECKED | These two originally omitted manifest changes are now reviewed below; both are DEFECT. |
| M3-028, M4-015, M11-039 secondary behavioral hunks | CHECKED | The behavior-changing secondary hunks were compared with their binary rows and tests. |
| M3-019, M3-027, M10-008, M13-002, M14-012, M15-017 incidental references | CHECKED | Diff inspection found no independent implementation change to these records: they occur only as a dependency/citation/type reference in a changed hunk. No Q1 verdict is inferred for them. |
| Batch-4 flaky test | CHECKED | Source race inspected and 20 isolated repetitions run (0 failures). The original failing output is unavailable, so the exact failed assertion is UNKNOWN; the report now states only the proved race risk. |

Q1 now has no unchecked scoped item. Conclusions that depend on behavior outside these commits remain visibly classified as cross-record defects, not silently accepted.

Date: 2026-09-30  
Baseline: `origin/main` / `9e7b4f5` after `git pull --ff-only`  
Builds reviewed: `aeaf977`, `bd12929`, `4d960de`, `e0f4333`, `caaef86`  
Prior rows checked: `20260930-B-CORE-verify-1-2.md`, `-3-4.md`, `-5-6.md`  
Binary: shipped `libcozmoEngine.so` 3.4.0-1204

## Result

The five batches repair most of the narrow defects named by the Opus verifiers, but the B-CORE2 status is not a fidelity pass. The original review counted 17 changed manifest records and omitted M13-028 and M15-014. The complete five-commit manifest diff changes 19 records: **9 HOLD and 10 have a DEFECT**. The recurring failure is the one called out in `AGENTS.md`: a source-backed inner operation was added, while an unowned or deliberately local bridge remained in the production path.

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
| M13-028 | “FlipBlockAction::Init and DriveAndFlipBlockAction's constructor guards”; Init `0x0055EDC8`, CheckIfDone `0x0055F074`, destructor `0x0055ED54` | **DEFECT** | Batch 4 correctly removes the invented 5 s wait and lets the initial lift inherit the M4-016 30 s engine-clock timeout/result `0x03000018`. It does not close this record: the engine destructor cancels the separately queued carry lift at `0x0055ED6C..0x0055ED7A` and removes the reaction lock at `0x0055ED88`; `FlipBlockAction.cs:188-193` explicitly records both as unmodelled and lets the lift continue. The live path also retains the unsourced lift-speed and poll stand-ins named in the current unresolved text. |
| M15-014 | “Needs connection and per-serial persistence lifecycle”; connection `0x0052E2F8..0x0052E3B2`, read/resolver `0x006943A0..0x00694EE4`, device JSON `0x006998B4..0x00699BB8`, robot write `0x00695494..0x00695763` | **DEFECT** | Batch 2's ownership adaptation does queue one `0x194000` read after Lab and buffers the result for a later `NeedsManager`; that narrow order holds. The whole record does not: `Needs.cs` still cannot read/write the shipped enum-keyed JSON shapes/version branches, lacks the stars/onboarding write callers, and anchors the 61 s device-write throttle to its own `_lastWriteSec` instead of the engine's stored DateTime. The current manifest states all three gaps. Tests exercise the stack's own array JSON and buffered seam, so they cannot settle the shipped persistence lifecycle. |

## Defects that block acceptance

1. **M3-033 — unconditional face reads.** Engine gate `0x006B0658`; C# `CozmoEngine.cs:828-844`.
2. **M3-022 — calibration read still crosses the unowned M3-027 request-vector step.** Engine request builder/read path `0x0064503E..0x00645484`; C# `NvStorage.cs:406-419`.
3. **M3-034 — four connection sinks are no-ops.** Engine sink callbacks at `0x0064CE34`, `0x0063D7C4`, `0x0051DF34`, `0x006A6486`; C# `CozmoEngine.cs:826-827,845,1427`.
4. **M4-003 — QueueNow replacement is absent.** Engine `0x0053E24C..0x0053E35E`; C# refusal `Motion.cs:831-844`.
5. **M4-016 — action start time is stamped before first UpdateInternal, and the second precondition-time gate is absent.** Engine `0x00540D4A..0x00540D64`; C# `Motion.cs:824-829`.
6. **M2-002 — unlatched status gate has a LOCAL completion bridge.** Engine `0x005549B0..0x005549E0`; C# `DockActions.cs:915-921`.
7. **M3-018 — OpenCV failure/codec/save behavior is replaced by StbImageSharp policy.** Engine `0x004F21DC`, `0x004F2250`, `0x004F2EFC..0x004F2FA8`; C# `Camera.cs:83-93,361-386`.
8. **M3-010 — live PCM is the wrong type.** Engine float-vector loop `0x00597DFC..0x00597E12`; C# short path `AnimationScheduler.cs:1548,1594-1617`.
9. **M13-028 — the separately queued carry lift is not cancelled at flip destruction.** Engine `0x0055ED6C..0x0055ED7A`; C# explicit omission `FlipBlockAction.cs:188-193`.
10. **M15-014 — the connection read is only one part of the claimed persistence lifecycle.** Engine JSON/resolver/write bodies `0x006998B4..0x00699BB8`, `0x0069481C..0x00694EE4`, `0x00695494..0x00695763`; C# omissions are quoted in the current manifest.

## Five-step closure matrix

This matrix records what was independently closed for the review rather than relying on the original one-paragraph verdicts. “Test” distinguishes a source/asset oracle from a candidate-derived one.

| record | manifest + inventory | binary gates/order/failure/numbers | C# live entry and whole path | test oracle | result |
| --- | --- | --- | --- | --- | --- |
| M1-041 | current title/evidence reread; CD17-23/30 | streamer check precedes NV Update; idle only at `0x006456EC`/`0x00645C32`; deadline uses f32 `0x40A00000` | `EngineRobot.Update` and SyncTime deadline use the recovered order/width | source-addressed timing tests | HOLDS |
| M3-026 | invalid `-6`, FIFO and one-in-flight claim reread | state-0 dispatch at `0x006456BC..EC`; invalid path `0x00644E2A..EEE` | `NvStorage.Read/Update`; no completion-side dequeue | source branch/result | HOLDS |
| M3-030 | callback/sink/chunk claim reread | callback-vs-sink `0x006436B6..0x00643714`; chunks cap at 1000; SetState(0) last | `CompleteLocked/DispatchCompletion`; no idle callback | source sizes/order | HOLDS |
| M3-033 | 15-read order claim reread | FaceAlbum pair is gated by `VisionSystem::Init` at `0x006B0658` | connection queues pair unconditionally | test follows candidate queue and misses false gate | DEFECT |
| M3-022 | camera read/callback claim reread | queue then params; callback always enables; read builder crosses `+0xE8` | callback order fixed, but live request uses unsettled M3-027 Data | callback test source-derived; wire test circular on empty Data | DEFECT |
| M3-025 | tag/table/log claim reread | factory/non-factory exits `0x00644274/EC`; integer tables | validity table and distinct logs match | source table/branch | HOLDS |
| M3-031 | resend/timeout claim reread | increment-before-8 `0x00645C6A..7C`; identical resend copies `+0xE8`; strict clock deadline, `-4` | last command, seven resends, no timeout retry | source counts/results/order | HOLDS |
| M3-034 | all six sink classes claim reread | sink callbacks at `0x0064CE34`, `0x0063D7C4`, `0x0051DF34`, `0x006A6486`, `0x0069BEB2` | four sink classes remain deliberate no-ops | test explicitly accepts those no-ops | DEFECT |
| M3-035 | no callback on teardown/live clock claim reread | destructor frees function/vector without invoke `0x00643E80..0x00643F8C` | removal calls `OnDisconnected`, clearing without delivery | live removal path, source outcome | HOLDS |
| M4-001 | clip/rescale claim reread | epsilon `0x3727C5AC`; `vcvt.s32.f32` then back; NaN/inf behavior | `Motion` comparison helpers and conversion match | native-bit edge cases | HOLDS |
| M4-003 | game queue/track/teardown claim reread | `QueueNow` destroys prior runner at `0x0053E2D6/0x0053E35E`; stop before unlock, owner gate | repeated game move instead sees existing lock and returns `0x03000019` | external-lock test misses repeated-message branch | DEFECT |
| M4-016 | move lifecycle claim reread | start time first Update `0x00540D4A..68`; timeout before CheckIfDone, result `0x03000018` | C# stamps before first update; second precondition-time gate absent | timeout test checks constant, not live timing | DEFECT |
| M4-020 | RobotState/SyncTime claim reread | ImageRequest result ignored; localization send controls stamp | generic send-failure log/order now match | source send-failure/order | HOLDS |
| M1-024 | f32 tick claim reread | timer converts/stores f32 at `0x0084BC80/8C`; Needs reads f32 | `SecondsF` reaches Needs and stored `NowSec` | source bit-width and live tick | HOLDS |
| M2-002 | status-bit latch claim reread | bit 4 latch then clear + MovementComponent+9; absent latch stays RUNNING | local fallback completes without ever seeing bit 4 | test omits no-bit-ever case | DEFECT |
| M3-018 | color decode/save claim reread | OpenCV imdecode then unconditional cvtColor; save quality 90 | Stb codec and returned-error policy differ | IsColor tests source-derived; codec/save untested | DEFECT |
| M3-010 | float mu-law production claim reread | float-vector loop and zero pad | live scheduler supplies `short[]` | byte oracle is source-derived but enters wrong seam | DEFECT |
| M13-028 | flip Init/Check/destructor claim reread | destructor cancels id and removes lock; f32 literals include tolerance `0x40A00000` and timeout result comes from M4 | carry-lift cancel and reaction lock remain explicit omissions | tests accept omission counters/stand-ins | DEFECT |
| M15-014 | entire per-serial persistence claim reread | exact mfgId order, resolver, enum-keyed JSON/version gates and write scheduling | buffered connection read holds; JSON/callers/throttle ownership do not | fake NV/own JSON seam, not shipped-file compatibility | DEFECT |

## Secondary changed-hunk audit

The five commits mention more fidelity ids than the 19 records whose manifest `unresolved` changed. Diff inspection separates actual secondary behavior from dependency prose:

| record | changed hunk | check |
| --- | --- | --- |
| M3-028 | `PendingRequest.Clear` also clears the caller sink | HOLDS: `0x00643478` clears the same `+0x54` vector; M3-028 header-failure tests exercise it. |
| M4-015 | timeout/teardown now calls established StopHead/StopLift behavior before unlock | HOLDS for the referenced stop operation; no direct Stop API layout/order was changed. |
| M11-039 | the replacement calibration wait calls `UpdateCameraCalibration` for catch-up | HOLDS: this preserves the `VisionSystem::UpdateCameraCalibration` → `MarkerDetector::Init` endpoint rather than assigning calibration alone; the manifest still has no named test, although `VisionModeScheduleTests` covers the endpoint. |
| M3-019, M3-027, M10-008, M13-002, M14-012, M15-017 | dependency/citation/type references only | No independent implementation body owned by these records changed in the five commits; no extra verdict is inferred. |

## Tests and circularity

I found **no remaining literal implementation-vs-implementation equality test** in these five commits. Batch 5 did fix the earlier circular mu-law test: its expected bytes are now hand-derived rather than obtained from the other overload.

Four tests are nevertheless non-probative for the claimed record and would allow the defects above to pass:

- `M3DeviceTests.M3_034_TheConnectionReadCallbacksReachTheirLayers` (`M3DeviceTests.cs:2162-2168`) explicitly treats Progression, Inventory, backups and Lab as no-op sinks, then presents the test as coverage of M3-034's callbacks. It validates the candidate's omission, not the source path.
- `M4ControlTests.M4_003_MA_ALockedTrackFailsTheHeadMoveWith03000019` (`M4ControlTests.cs:713-730`) injects an external lock. It never exercises the production repeated-game-message path where QueueNow replaces the running action.
- `M4ControlTests.M4_016_MA17_TheDefaultActionTimeoutIs30Seconds` (`M4ControlTests.cs:764-772`) checks a public constant only. It cannot detect that `StartTime` is captured before first UpdateInternal.
- `M3DeviceTests.M3_010_C5_C6_PopFrameZeroPadsAndEncodesThroughTheScheduler` drives only the candidate's `short[]` seam. Its byte oracle is source-derived, but the test cannot detect the source's `vector<float>` production-path mismatch. It must not be used to settle M3-010.
- The `M13_028_*` tests prove several extracted branches, but the unfinished-lift test treats `QueuedLiftCancelsNotModelled` as an expected observation. It therefore documents, rather than rejects, the missing `ActionList::Cancel` production effect.
- The M15 Needs tests use the stack's own JSON array/object choices and fake NV seam. They do not open a shipped-app `needsState.json` with enum-keyed objects, so they cannot settle M15-014's device persistence claim.

The M2-002 test is source-derived for the latch ordering, but it omits the decisive no-bit-ever-arrives case. The M3-018 IsColor tests are source-derived and hold; there is no source test for OpenCV-vs-Stb decode failure or byte-identical Save output.

## Batch-4 flaky test

The flaky-test candidate is **`EngineAppLayerTests.M1_025_M1_015_CB33_AFrameInFlightAtTheRemovalLeavesNothingBehind`** (`EngineAppLayerTests.cs:1707-1744`). The B-CORE2 status records one batch-4 push-gate flake followed by a green rerun, and `PROJECT_STATE.md` independently queues this exact test for a deterministic release because its 300 ms timer is a flake risk.

Proved race risk: after the detector signals `Entered`, the test starts a background task that waits 300 ms and releases the blocked frame (`EngineAppLayerTests.cs:1725-1727`). It then asserts `!frame.IsCompleted` and calls the removal tick (`:1728-1730`). A sufficiently delayed test thread can therefore observe the release first. Twenty isolated repetitions at the audited HEAD produced 20 passes and 0 failures. The original failing output is unavailable, so **which assertion actually failed is UNKNOWN** and this report does not claim reproduction. The deterministic repair is still to release from an explicit removal-path handshake rather than elapsed wall time.

## Commit-level comparison with the Opus verifier rows

| Commit | Result |
|---|---|
| `aeaf977` | Narrow on-idle correction **HOLDS**. |
| `bd12929` | Single calibration read, Lab/Needs order and calibration log order **HOLD**; M3-022/M3-033 still fail whole-path ownership, and the materially changed M15-014 still fails its broader persistence claim. |
| `4d960de` | Sink persistence, 1000 bound, outcome/retry/face logs and disconnect cleanup **HOLD**; M3-034 still lacks four production sinks. |
| `e0f4333` | Float comparisons, vcvt round trip, stop-before-unlock, owner gate/result and generic send log **HOLD**; QueueNow and exact first-Update timeout start remain defects. M13-028's 5 s stand-in removal holds narrowly, but its destructor cancellation/reaction-lock omissions keep that touched record defective. |
| `caaef86` | Needs/SyncTime f32 and IsColor correction **HOLD**; M2-002 local completion, M3-018 codec/failure semantics and M3-010 short PCM remain defects. |

## Verification run

- `python re-analysis/tools/fidelity.py --check`: pass, 418 records.
- `dotnet test cozmo-stack/Cozmo.sln --no-restore --verbosity minimal`: **2623/2623 pass** at this HEAD (warnings only).
- Direct binary recheck on 2026-10-01: Thumb disassembly re-read `0x00513BF6..0x00513C18`, `0x006456BC..0x006456EC`, `0x00643568..0x006437EE`, `0x00645C54..0x00645D7C`, `0x0053E24C..0x0053E36A`, `0x00540D4A..0x00540E88`, `0x005549B0..0x005549FE`, `0x0055ED54..0x0055ED98`, and `0x006943A0..0x00694404` from the shipped `.so`.
- Flake probe: the named test ran 20 isolated repetitions, **20 pass / 0 fail**; exact historical failed assertion remains UNKNOWN.
