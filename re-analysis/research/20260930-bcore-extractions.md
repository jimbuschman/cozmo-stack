# B-CORE open extractions

Answers `re-analysis/research/requests/20260930-bcore-extractions.md`.

Read-only independent extraction. Native addresses are ELF virtual addresses in
`resources/lib/armeabi-v7a/libcozmoEngine.so`; Unity citations are the shipped decompiled application. Ghidra was
used only for navigation and the cited branches/constants were checked in the binary. `EXACT_SOURCE candidate` below
means the row is completely source-extracted for the manager to check; this report does not change or settle a
manifest record.

## 1. M3-027: the READ command's `Data` vector

Current manifest text, read before this finding:

> **M3-027** — **IMPLEMENTATION_GAP** — “NV ProcessRequest READ: the factory/non-factory Length, the reliable send,
> and the pending-read arm (5 s robot-clock deadline, retry counter 0).” Evidence says the READ command copies Data
> from `+0xE8`; unresolved says that `ProcessRequest` does not populate it, `Update`'s write path does, and a READ
> after a WRITE therefore carries unsettled data.

The `+0xE8/+0xEC/+0xF0` fields are one `std::vector<uint8_t>` (begin/end/capacity). A scan of all references to this
member found the constructor and the WRITE-state code below as its only writers. READ, resend, re-request and the
destructor only copy or free it.

| step | what the original does | citation | record | proposed classification |
| --- | --- | --- | --- | --- |
| Construct the shared command-data vector | Stores zero to begin, end and capacity. Thus a READ before any WRITE has zero Data bytes. | `0x006428AE: strd r7,r7,[r4,#0xE8]`; `0x006428B4: str.w r7,[r4,#0xF0]` (`r7=0` from `0x00642858`) | M3-027 | EXACT_SOURCE candidate |
| Begin each WRITE chunk | Takes the vector at `this+0xE8`; if `end != begin`, assigns `end = begin`. Capacity and the bytes in the allocation are retained, but logical length becomes zero. | `NVStorageComponent::Update` `0x00645766..0x00645782` | M3-027 | EXACT_SOURCE candidate |
| Optional non-factory first-chunk header | On offset zero and when `+0x15C == 0`, inserts the 16-byte `OMZC` header at the new end. The payload allowance is reduced from `0x400` to `0x3F0`. | `0x006457B0..0x00645804`; header word `0x435A4D4F`; limits `0x400/0x3F0` | M3-027 | EXACT_SOURCE candidate |
| Write the chunk bytes | Inserts the current source slice at the vector end. Therefore the vector contains this command's optional header plus this one chunk, not all earlier chunks. | `0x0064580C..0x00645822` | M3-027 | EXACT_SOURCE candidate |
| Send WRITE | Copies that vector into `NVCommand.Data`, sends reliable/non-hot, and leaves the vector intact. Terminal completion calls `SetState(0)`, which clears only `+0x48`, `+0x1C`, and `+0x78`; it does not clear `+0xE8`. | `0x006458B8..0x00645902`; `SetState` `0x00642B4C..0x00642B5E` | M3-027 | EXACT_SOURCE candidate |
| Send READ | `ProcessRequest` copies the same vector into READ `NVCommand.Data`; it does not clear or overwrite it first. | `0x0064537E..0x00645392` (copy begins at `0x00645386`) | M3-027 | EXACT_SOURCE candidate |
| Re-request and resend | Both again copy the same vector, so they preserve the same stale WRITE command data. | re-request `0x006438A4..0x006438BC`; resend `0x00645CDA..0x00645CEE` | M3-027 | EXACT_SOURCE candidate |

Result: a READ before any WRITE has empty Data. A READ after a WRITE has the most recently built WRITE *chunk* in
Data. For a one-chunk non-factory write that includes the `OMZC` header plus payload; for a multi-chunk write the last
chunk replaces the logical vector, so the later READ carries the final payload chunk without the first-chunk header.
Nothing in `SetState(0)`, a successful reply, timeout, or the READ arm clears it.

This resolves the manifest's stated uncertainty and contradicts the current C# test oracle that READ Data is always
empty.

## 2. M3-023: what “start camera” does

Current manifest text:

> **M3-023** — **IMPLEMENTATION_GAP** — “EnableColorImages is never sent at connection; it stores and sends the flag;
> only BehaviorTrackLaser reads it.” Unresolved: `CozmoRobot.StartCamera` sends `EnableColorImages` and an
> `ImageRequest`, but no row owns that path.

There are two distinct original paths. The official Unity stream API uses the one-field game `ImageRequest`; it does
not send `EnableColorImages` and the corresponding engine handler does not send another robot message.

| step | what the original does | citation | record | proposed classification |
| --- | --- | --- | --- | --- |
| Connection starts the robot stream | After successful reliable/non-hot `SyncTime` and `InitController`, constructs RobotInterface `ImageRequest { mode = 1 (Stream), resolution = 4 (QVGA) }` and sends it reliable/non-hot. Failure of either preceding send skips it; the ImageRequest send result itself is not tested. No `EnableColorImages` precedes it. | `Robot::SendSyncTime` `0x00515270..0x00515312`; `{01 04}` store at `0x005152F0`; send `0x00515304` | M3-023 / NEW camera-start row | EXACT_SOURCE candidate |
| Unity starts capture | `CaptureStream` calls `Initialize(Stream)`; it registers the `ImageChunk` callback only when previously Off, stores the mode, then sends G2E `ImageRequest { sendMode }`. | `unity/scripts/csharp/ImageReceiver.cs:37-55` | NEW camera-start row | EXACT_SOURCE candidate |
| Engine consumes Unity `ImageRequest` | If a first robot exists, writes the one-byte mode to `Robot+0x340`. It sends no RobotInterface message and does not touch resolution `Robot+0x341`; no robot is a silent no-op. | `CozmoEngine::HandleMessage<ImageRequest>` `0x004EDEC4..0x004EDED8` | NEW camera-start row | EXACT_SOURCE candidate |
| Alternate G2E path | `SetRobotImageSendMode { mode, resolution }` stores both bytes at `Robot+0x340/+0x341`, then sends RobotInterface `ImageRequest {mode,resolution}` reliable/non-hot. The shipped Unity camera receiver has no call site for this message. | handler `0x004EDE1C..0x004EDE48`; send helper `0x004EDE4C..0x004EDEA4`; no Unity use found outside generated schema | NEW camera-start row | EXACT_SOURCE candidate |
| Color enable path | G2E `EnableColorImages {bool}` stores `VisionComponent+0x32A` and immediately sends RobotInterface `EnableColorImages {bool}`. This path is independent of ImageReceiver and has no shipped Unity call site. | `0x0065835C..0x00658378`; direct method `0x006582CC..0x006582E6`; send helper `0x006582E8..0x00658314` | M3-023 | EXACT_SOURCE candidate |
| Stop capture | Unity sends only G2E `ImageRequest {Off}` and removes its callback; the engine only updates `Robot+0x340`. | `ImageReceiver.cs:67-74`; `0x004EDEC4..0x004EDED8` | NEW camera-start row | EXACT_SOURCE candidate |

Therefore the source counterpart of stack `CozmoRobot.StartCamera` is not “EnableColorImages, then ImageRequest.” The
official app's live start is `ImageReceiver.CaptureStream -> ImageRequest(Stream)`; the actual robot stream was
already requested at connection as `Stream/QVGA`. Explicit color selection is a separate engine API unused by this
Unity path. M3-023's title holds narrowly, but its production-path ownership is incomplete and the stack's combined
call is contradicted.

## 3. M3-033/M3-034: FaceAlbum gate and the four sinks

Current manifest text:

> **M3-033** — **IMPLEMENTATION_GAP** — “At connection the engine queues 12 NV reads, then the CameraCalib read, then
> Lab and Needs; ready-to-stream waits for the whole queue.” Unresolved says the FaceAlbum gate
> `VisionSystem::Init == 0` exists only in prose and needs its own record.
>
> **M3-034** — **IMPLEMENTATION_GAP** — “The connection reads' callbacks and data sinks (progression, inventory,
> face album, backup, lab, needs).” Unresolved says progression, inventory, backup and lab sinks are missing.

### 3.1 Init gate (a record of its own)

`VisionComponent::Init` calls `VisionSystem::Init`, tests its exact return against zero, and only enters the
FaceAlbum branch on zero.

| step | what the original does, including failure result | citation | record | proposed classification |
| --- | --- | --- | --- | --- |
| VisionComponent prerequisites | Missing any of `ImageQuality.TimeBeforeErrorMessage_ms`, misspelled `RepeatedErrorMessageInverval_ms`, `InitialExposureTime_ms`, or `PerformanceLogging.DropStatsWindowLength_sec` logs an error and returns `1`; it never calls `VisionSystem::Init`. | `0x00650D50..0x00650E30`, failure blocks `0x00650FBA..0x006510F8` | NEW Vision-init gate | EXACT_SOURCE candidate |
| Call and gate | Calls `VisionSystem::Init`; return `0` branches to FaceAlbum. Any nonzero is logged as `VisionSystemInitFailed` and returned unchanged. | call/test `0x00650E36..0x00650E3E`; nonzero return `0x00650E40..0x00650FB2` | NEW Vision-init gate | EXACT_SOURCE candidate |
| VisionSystem early setup | Clears `+0x338`, `+0x58`, `+0x328`. A null DataPlatform only warns and continues; it is not itself a failure. | `0x006B0668..0x006B06A8` | NEW Vision-init gate | EXACT_SOURCE candidate |
| Required image-quality fields | Missing `ImageQuality`, or any of `TooBrightValue`, `TooDarkValue`, `MeterFromDetections`, `LowPercentile`, `MidPercentile`, `HighPercentile`, `MidValue`, `MaxChangeFraction`, or `SubSample`, returns `1`. | `0x006B07D8..0x006B0A12`; common failure/return `0x006B0E3E..0x006B0E58` | NEW Vision-init gate | EXACT_SOURCE candidate |
| Exposure and profiler gates | Nonzero `SetAutoExposureParams`, or missing `TimeBetweenProfilerInfoPrints_sec` / `TimeBetweenProfilerDasLogs_sec`, returns `1`. Defaults loaded before the required reads include `5.0f = 0x40A00000` and `60.0f = 0x42700000`; milliseconds are computed with `1000.0f = 0x447A0000`. | `0x006B09F8..0x006B0A22`; `0x006B0A24..0x006B0B16`; failure to `0x006B0E40` | NEW Vision-init gate | EXACT_SOURCE candidate |
| Tracker/config gates | Nonzero `PetTracker::Init`, absent `InitialVisionModes`, absent `InitialModeSchedules`, or a schedule value that is not int/bool/array-of-bool returns `1`. An unknown initial vision-mode name only warns and is ignored. A missing per-mode schedule entry is allowed. | `0x006B0BDA..0x006B0C32`; modes `0x006B0C34..0x006B0CB8`; schedules `0x006B0CBA..0x006B0E3E` | NEW Vision-init gate | EXACT_SOURCE candidate |
| Success | Installs the all-modes schedule, initializes the camera parameters, writes `+0x350=4`, `+0x354=0x20`, `+0x58=1`, and returns `0`. | `0x006B0D8C..0x006B0DEE`, common return `0x006B0E40..0x006B0E58` | NEW Vision-init gate | EXACT_SOURCE candidate |
| FaceAlbum choice after success | Missing/empty `FaceAlbum`, or exact string `"robot"`, calls `LoadFaceAlbumFromRobot`, which queues the two robot NV reads. A nonzero load result only warns; VisionComponent still completes and returns `0`. Any other string erases faces, loads that file, broadcasts loaded IDs/names, and likewise only warns on failure. | `0x00650E66..0x00650F82`; initialized/return `0x00650F84..0x00650FB2` | M3-033 plus NEW gate | EXACT_SOURCE candidate |

Thus the two FaceAlbum reads are conditional on the *whole* VisionSystem initialization succeeding, not merely on a
FaceAlbum setting. M3-033's unconditional “queues 12 reads” title is too strong.

### 3.2 Missing NV sinks

For all four callbacks, a nonnegative result is the success class, `-1` means entry not found, and other negative
values are failures.

| sink | exact result handling | citation | record | proposed classification |
| --- | --- | --- | --- | --- |
| Progression/unlocks (`0x182000`) | `result >= 0`: unpack the saved unlock list, validate/install it, then `SendUnlockStatus`; malformed saved data logs an error. `result == -1`: install the built-in default unlocks, notify/send their status. Other negative results: log failure and do not install defaults. | callback `0x0064CE34..0x0064D06B`; success `0x0064CE4C..0x0064CFF0`; not-found `0x0064D00A..0x0064D040` | M3-034 / M15 progression | EXACT_SOURCE candidate |
| Inventory (`0x195000`) | First sets `InventoryComponent+0x104 = 1` on every terminal result. `result >= 0`: unpack, then send the full inventory to the game. `result == -1`: send current/default inventory, and if a robot is available send `RequestDefaultSparks` to the game. Other negative: log, then still send the full inventory. | callback `0x0063D7C4..0x0063D93B`; flag `0x0063D7D8..0x0063D7E0`; success `0x0063D7E2..0x0063D7F0`; not-found `0x0063D83E..0x0063D888` | M3-034 / M15 inventory | EXACT_SOURCE candidate |
| Backup/RDBM (eight tags) | Decrements the pending-read count first on every result. `result >= 0`: stores `{tag -> bytes}`; for tag `0x181000`, unpacks OnboardingData and stores its flag. `result == -1`: informational log only. Other negative: warning. When the pending set is empty, calls the backup-file writer. | callback `0x0051DF34..0x0051E0C7`; decrement `0x0051DF44..0x0051DF4A`; store/onboarding `0x0051DF54..0x0051DF8A`; final test/write `0x0051DFEE..0x0051DFFA` | M3-034 / M12 backup | EXACT_SOURCE candidate |
| Lab (`0x196000`) | Trampoline forwards bytes/length/result/tag. `result >= 0`: unpacks `LabAssignments` at component `+0x94`; for every assignment calls `RestoreActiveExperiment` with current seconds-since-epoch; returns `1`. `result == -1`: logs no assignments and returns `0`. Other negative: logs error and returns `0`. | trampoline `0x006A6486..0x006A64A0`; sink `0x006A5C34..0x006A5D30` | M3-034 / NEW lab sink | EXACT_SOURCE candidate |

## 4. M3-037: image-buffer ownership and short raw payloads

Current manifest text:

> **M3-037** — **COMPATIBILITY_POLICY** — “A raw gray/RGB payload shorter than rows*cols (or *3) reads bytes past
> its end in the engine (undefined); this stack zero-fills the missing bytes.” Unresolved asks whether
> `SetNextImage` copies or moves the Robot buffer, noting that only never-written bytes can be policy.

| step | what the original does | citation | record | proposed classification |
| --- | --- | --- | --- | --- |
| Start a new frame | On frame-id change, fixes QVGA geometry `320 x 240`, sets encoding flags, assigns `end = begin`, then calls `reserve(0x38400)` (230400, exactly QVGA RGB capacity). Existing capacity is retained. | `EncodedImage::AddChunk` `0x004F1D36..0x004F1DB4`; reset `0x004F1DA2`; reserve `0x004F1DAA` | M3-037 | EXACT_SOURCE candidate |
| Fill it | Appends only accepted chunk bytes. No geometry-sized fill occurs. | `0x004F1EAE..0x004F1EC8` | M3-037 | EXACT_SOURCE candidate |
| Hand off complete image | `HandleImageChunk` calls `VisionComponent::SetNextImage` only when `AddChunk` returns complete. | `0x00535B82..0x00535B9E` | M3-037 | EXACT_SOURCE candidate |
| Transfer to vision | Under the mutex, saves the destination's old vector, zeroes its three pointers, transfers the source begin/end/capacity into `VisionComponent+0x74`, zeroes the source three pointers, transfers metadata, then installs the old destination vector back into the source. This is an ownership swap/move, not a byte copy. | `SetNextImage` `0x006530CC..0x00653140` | M3-037 | EXACT_SOURCE candidate |
| Raw decode | Gray reads `rows*cols`; RGB reads `rows*cols*3`; neither compares that count with vector logical length. Long payloads are therefore deterministically truncated to the geometry. | gray `0x004F2BC0`; RGB `0x004F245A` | M3-037 | EXACT_SOURCE candidate |

Consequences for a short payload:

- `[0, payload_length)` is fixed by the current frame.
- Beyond logical end, positions previously written in the same retained allocation contain stale image bytes. Because
  `SetNextImage` swaps two vector allocations, “previous” means the prior frame that used that particular allocation,
  not necessarily the immediately preceding frame.
- Positions never written during that allocation's lifetime remain allocator contents. The shipped code does not
  determine their initial values. Reallocation can also replace the allocation and reset which bytes have history.

The current record is therefore partly contradicted: all past-end bytes are not uniformly “undefined heap.” Stale
previously written bytes follow exact source behavior; only never-written/reallocated storage remains
COMPATIBILITY_POLICY. Long-payload truncation is exact source behavior.

## 5. M4-003: `QueueNow` and app-used positions

Current manifest text:

> **M4-003** — **IMPLEMENTATION_GAP** — “The head and lift API follows the game-message path: caller
> speed/accel/duration; the original app passes head 10/20 and lift 10/20, duration 0.” Unresolved says the app uses
> `QueueActionPosition.NOW`, `QueueNow` deletes the running action first, and no record owns those queue semantics.

The shipped enum is `NOW=0`, `NOW_AND_CLEAR_REMAINING=1`, `NOW_AND_RESUME=2`, `NEXT=3`, `AT_END=4`,
`IN_PARALLEL=5` (`unity/scripts/csharp/Anki.Cozmo.ExternalInterface/QueueActionPosition.cs`). The app defaults both
head and lift to NOW (`Robot.cs:1435`, `:1635`).

| step | what the original does, including result/order | citation | record | proposed classification |
| --- | --- | --- | --- | --- |
| ActionList dispatch/gates | Null, duplicate, or “currently clearing” is rejected before queue dispatch. Position `0` -> QueueNow; `3` -> QueueNext; `4` -> QueueAtEnd; `5` -> AddConcurrentAction. Queue methods return `0` on success / `1` on failure; concurrent returns a positive queue key or `-1`. | `ActionList::QueueAction` `0x0053D93C..0x0053DADC` | NEW queue-semantics row | EXACT_SOURCE candidate |
| QueueNow: null | Logs/refuses null and returns `1`. | `0x0053E264..0x0053E2B4` | NEW | EXACT_SOURCE candidate |
| QueueNow: no pending list nodes | If a current action exists, calls `IActionRunner::Cancel` first; then `DeleteActionAndIter`; then appends the new action with QueueAtEnd. | `0x0053E2B6..0x0053E2EA`; cancel call `0x0053E2D6` | NEW | EXACT_SOURCE candidate |
| QueueNow: pending nodes exist | Logs replacement if current is non-null, deletes current without a separate Cancel in this branch, stores the caller's retry byte at new action `+8`, inserts new action at the front of the pending list, returns `0`. | `0x0053E2EC..0x0053E36E`; delete `0x0053E35E` | NEW | EXACT_SOURCE candidate |
| Cancel result | `IActionRunner::Cancel` changes a nonterminal action result to `0x02000000` (Cancelled); it does not manufacture `0x03000019` (track locked). Thus a repeated NOW move replaces the running move rather than failing its lock check. | `IActionRunner::Cancel` `0x00540350..0x00540372`; QueueNow ordering above | NEW / M4-003 | EXACT_SOURCE candidate |
| Completion construction | `DeleteActionAndIter` guards against deleting an id twice, calls `PrepForCompletion`, captures a `RobotCompletedAction` unless result is `0x03000009`, invokes the virtual destructor, erases the queue node, and only then broadcasts the captured completion to the game. | `0x0053F9E4..0x0053FB08` | NEW | EXACT_SOURCE candidate |
| Stop before unlock | `~IActionRunner` stops head/lift/body only if that track is moving and all its locks belong to the action's stringified id. It calls `StopHead`, then `StopLift`, then `StopBody`; only afterward, if `+0x56==0` and result is not `0x02000001`, it releases locks through the helper that sends `EnableAnimTracks`. | stop gates `0x00541128..0x005411B2`; unlock `0x0054120C..0x0054122A` | M4-003 / NEW | EXACT_SOURCE candidate |
| Observable replacement order | Robot wire: any needed `StopHead`/`StopLift`/`StopBody`, then the lock-release/`EnableAnimTracks`; game: `RobotCompletedAction` with Cancelled after destruction. The replacement action starts on a later queue update and then takes locks/sends its own movement. | `0x00541138..0x0054122A`; game broadcast `0x0053FA9C..0x0053FAD8` | NEW | EXACT_SOURCE candidate |
| NEXT | Null -> `1`. Stores retry byte. Empty pending list delegates to AtEnd; otherwise inserts immediately before the first pending action. It does not replace current. | `QueueNext` `0x0053E078..0x0053E10E` | NEW | EXACT_SOURCE candidate |
| AT_END | Null -> `1`; otherwise stores retry byte, appends to pending list, returns `0`. | `QueueAtEnd` `0x0053E16C..0x0053E1EA` | NEW | EXACT_SOURCE candidate |
| IN_PARALLEL | Null or duplicate/currently-clearing -> `-1`. Otherwise finds the first unused positive queue key starting at `1`, constructs a separate `ActionQueue`, appends the action there, and returns that key. | `ActionList::AddConcurrentAction` `0x0053DF2C..0x0053E006` | NEW | EXACT_SOURCE candidate |

The current M4-003 record owns construction constants but not the queue path that determines whether those actions
run, cancel, stop, unlock, and report completion. It cannot settle the public head/lift path by itself.

## 6. M1-029: exact jsoncpp reader used by firmware JSON

Current manifest text:

> **M1-029** — **IMPLEMENTATION_GAP** — “Firmware version check against the shipped firmware header.” Unresolved
> says System.Text.Json differs on nesting, `{"":1,}`, number typing/`-0`, literal suffixes, invalid UTF-8, leading
> zeros and raw control characters, and that the throw destination is not established.

The firmware loader uses the old `Json::Reader` default features: comments enabled, strict root disabled, dropped
null placeholders disabled, numeric object keys disabled (`Reader` constructor `0x008E075A..0x008E078A`).

| step | exact reader behavior | citation | record | proposed classification |
| --- | --- | --- | --- | --- |
| Root/trailing input | Reads one value, skips comments, and does not require EOF. Any JSON value type is accepted at root. Thus `truex` parses as true and `5garbage` parses as 5 because the unconsumed suffix is ignored. | `Reader::parse` `0x008E082C..0x008E0918`; tokenizer literal cases `0x008E1660..0x008E17BC` | NEW jsoncpp-reader row / M1-029 | EXACT_SOURCE candidate |
| Whitespace/comments | Whitespace is exactly `09 0A 0D 20`. Both `//...` and `/*...*/` comments are accepted. Unterminated comments fail. | tokenizer `0x008E1660..0x008E18FE` | NEW | EXACT_SOURCE candidate |
| Literal grammar | `true`, `false`, and `null` are matched as byte prefixes without checking a following delimiter. | `0x008E17A0..0x008E183C` | NEW | EXACT_SOURCE candidate |
| Object keys/commas | Quoted keys only because numeric keys are disabled. Ordinary trailing object commas are rejected, but the prior key string is tested before it is reset: exactly the empty-key form `{"":1,}` is accepted at `0x008E0FBA`; e.g. `{"a":1,}` is rejected. | `readObject` `0x008E0F8E..0x008E10F6`, especially `0x008E0FBA` | NEW | EXACT_SOURCE candidate |
| Arrays | `[]` is accepted; a trailing comma is rejected because dropped-null placeholders are disabled. | `readArray` `0x008E1274..0x008E13FC`; default feature byte | NEW | EXACT_SOURCE candidate |
| Number token grammar | Number starts at `-` or a digit. The scanner consumes an arbitrary digit run (so leading zeros are accepted), optional `.`, optional exponent `e/E`, and optional exponent sign; the conversion stage decides whether an incomplete token is an error. | tokenizer `0x008E1660..0x008E17BC`; `readNumber` `0x008E190A..0x008E19A6` | NEW | EXACT_SOURCE candidate |
| Integer typing | Accumulates against a sign-specific uint64 limit. Negative values through `INT64_MIN` become `intValue` (type 1). Nonnegative `0..INT64_MAX` also become type 1. `INT64_MAX+1..UINT64_MAX` become `uintValue` (type 2). | `decodeNumber` `0x008E1CF2..0x008E1ECA` | NEW | EXACT_SOURCE candidate |
| `-0` | Accumulates magnitude zero and stores signed integer zero, type 1; it is not a real and does not throw. | negative integer store `0x008E1E02..0x008E1E4A` | NEW | EXACT_SOURCE candidate |
| Integer overflow/real typing | A decimal/exponent token, a negative magnitude beyond `2^63`, or a positive integer beyond `2^64-1` falls through to double conversion. Successful stream extraction stores `realValue` (type 3); conversion failure adds a parse error and returns false. `18446744073709551616` is therefore a real, not an overflow rejection. | fallback branches `0x008E1D70..0x008E1DA0`; `decodeDouble` `0x008E2258..0x008E242C` | NEW | EXACT_SOURCE candidate |
| Strings/control bytes/UTF-8 | Recognizes JSON escapes and `\u`; a bad escape fails. Every other raw byte except quote/backslash is appended unchanged: raw control bytes are not rejected and UTF-8 is not validated. Invalid UTF-8 therefore survives as bytes in a string. | token string scan `0x008E183E..0x008E1908`; decode `0x008E1B34..0x008E1C9E` | NEW | EXACT_SOURCE candidate |
| Nesting limit | Before each `readValue`, compares the value-pointer deque size with `1000`; `>1000` throws `Json::RuntimeError`. The root is already in the deque, so root depth counts as 1: depth 1000 is accepted; the attempted 1001st value throws. | `0x008E09A4..0x008E09B8`; throw block `0x008E0D92..0x008E0DB4` | NEW | EXACT_SOURCE candidate |
| Firmware input bound | Loader parses bytes from the firmware file header up to the first NUL or `0x800`, whichever comes first; short/read/parse failure logs and leaves expected version/time unchanged. | `FirmwareUpdater::LoadHeaderData` `0x00677C3C..0x00677D34` | M1-029 | EXACT_SOURCE candidate |
| Catch/termination | `LoadHeaderData` contains no catch around `Reader::parse`, and its observed loader-thread proxy contains no source-level conversion of `Json::RuntimeError` into `parse == false`. `0x00677A4E` is a `std::terminate` landing path associated with another `std::thread` lifetime, not a demonstrated catch for this parse. The final unwinder/personality route for an exception escaping this thread entry was not exhaustively decoded. | no catch in `0x00677C3C..0x00677D34`; thread path `0x0067861C`, `0x0067889C`; unrelated landing path `0x00677A4E` | NEW | **UNKNOWN / RECOVERABLE_GAP** (decode exception tables for loader entry) |

This directly contradicts several current C# parser assumptions: `-0` is integer zero; the special empty-key trailing
comma is accepted; literal suffixes and root suffix text are ignored; leading zeros, raw controls and invalid UTF-8
are accepted; and the 1001st nested value throws rather than returning false.

## 7. M1-044 and M1-045 build rows

### 7.1 M1-044 teardown

Current manifest text:

> **M1-044** — **IMPLEMENTATION_GAP** — “RemoveRobot's upper-layer teardown: Robot::~Robot aborts all actions and
> destroys the behaviour, mood, AI/freeplay, path, map, docking, carrying and vision components.” Unresolved says it
> is named, not built, including AbortAll, ForceUpdate, PerfMetric and DAS, and has no test.

| ordered step | original behavior visible outside the destroyed object | citation | record | proposed classification |
| --- | --- | --- | --- | --- |
| RemoveRobot notification prelude | Depending on the RIC result, broadcasts `RobotDisconnected {timeSinceLastMsg_sec = 0.0f (0x00000000)}` and clears `$session_id`; then always calls `NeedsManager::OnRobotDisconnected`, `PerfMetric::OnRobotDisconnected`, and `DASPauseUploadingToServer(0)`. | `0x0052F23A..0x0052F2F2` | M1-044 | EXACT_SOURCE candidate |
| Enter destructor | `RemoveRobot` calls `Robot::~Robot`, then `operator delete`; map/id/RIC erasure and `$phys`/`$group` clearing occur afterward. | `0x0052F2F2..0x0052F360` | M1-044 | EXACT_SOURCE candidate |
| Flush freeplay data | `FreeplayDataTracker::ForceUpdate` directly calls `SendData`; this is telemetry/app data, not robot wire. | destructor call `0x0051111A`; body `0x0056EEB8..0x0056EEC6` | M1-044 | EXACT_SOURCE candidate |
| AbortAll order | Exactly: `ActionList::Cancel(all)`, `PathComponent::Abort`, `DockingComponent::AbortDocking`, `SendAbortAnimation`, `MovementComponent::StopAllMotors`. Return is OR of path/docking/animation results, but destructor ignores it. | `0x0051194C..0x0051198C`; call `0x00511120` | M1-044 | EXACT_SOURCE candidate |
| Action cancellation effects | Cancelled actions report their completion to the game; action destruction conditionally sends per-track Stop messages, then unlock/`EnableAnimTracks`, as extracted in section 5. | ActionList cancel/delete path `0x0053DADC..0x0053DEFE`; `0x0053F9E4..0x0053FB08`; `0x00541084..0x00541286` | M1-044 / M4 queue interface | EXACT_SOURCE candidate |
| Path abort | Cancels its planner, clears path/shared references, maps active statuses to aborted/failure, drains callbacks. It sends no robot message directly. `~PathComponent` later calls Abort again; after the first abort that is state cleanup. | `0x00649100..0x006491BA`; destructor `0x00648FFC..0x00649080` | M1-044 | EXACT_SOURCE candidate |
| Docking/animation/motors wire order | Sends reliable/non-hot RobotInterface `AbortDocking`, then `AbortAnimation`, then unconditionally reliable/non-hot `StopAllMotors`. `StopAllMotors` first releases any direct-drive track ownership, then sends the message. | docking `0x0063BE10..0x0063BE5C`; animation `0x00517DE4..0x00517E30`; motors `0x0063FBD8..0x0063FE18`, send helper `0x0064099C..0x006409E8` | M1-044 | EXACT_SOURCE candidate |
| Behavior managers | Destroys BehaviorManager then BehaviorSystemManager. Their direct destructor bodies free behavior containers, activities, triggers and subscriptions; no direct Robot send or game broadcast call occurs in those bodies. Whether every virtual child behavior destructor is externally silent was not exhaustively resolved. | calls `0x0051112C`, `0x0051113A`; bodies `0x005A0D22..0x005A0DF8`, `0x005A5886..0x005A58D2` | M1-044 | direct bodies exact; child virtual destructors UNKNOWN |
| ActionList | Calls `ActionList::Clear`, then destroys it. Clear destroys all remaining queue nodes under the clearing flag. Any action object destruction follows its destructor semantics; AbortAll has already cancelled the active actions. | `0x00511146..0x00511156`; Clear `0x0053D916..0x0053D93A` | M1-044 | EXACT_SOURCE candidate |
| Vision | Deleting destructor joins the processor thread with no timeout, destroys the owned VisionSystem, and frees it. No robot/game message is sent by this direct destructor. | call `0x0051115E..0x0051116C`; `0x00652554..0x006527D2`, join `0x0065257C`, VisionSystem destruction `0x0065258E` | M1-044 | EXACT_SOURCE candidate |
| Mood | Unregisters its ActionList callback and frees history/maps/subscriptions; no wire/game message. | call `0x005111B6`; `0x0067AE14..0x0067AE74` | M1-044 | EXACT_SOURCE candidate |
| AI/freeplay, BlockWorld, Map | AIComponent destroys owned analysis/freeplay/helper objects and caches; BlockWorld and MapComponent free their object/map state. Their direct bodies contain no Robot send/game broadcast. Effects of the AIComponent virtual child destructors are not fully resolved. | AI call `0x00511276`, body `0x00569CB2..0x00569DAC`; BlockWorld call `0x005112CE`, body `0x0061CCFC..0x0061CDBA`; Map call `0x005114E8`, body `0x0067DC00..0x0067DC28` | M1-044 | direct bodies exact; child virtual destructors UNKNOWN |
| Carrying/docking | Frees CarryingComponent then DockingComponent after AbortDocking was already sent. These sites call `operator delete` directly—there is no intervening destructor call—so they add no wire/game effect. | `0x005113FA..0x00511414` | M1-044 | EXACT_SOURCE candidate |
| AnimationStreamer | Clears its unsent buffer and local track/song/live-idle state; the direct destructor sends nothing. It occurs after AbortAnimation/StopAllMotors. | call `0x0051154A`; `0x0057AF48..0x0057AFDA` | M1-044 | EXACT_SOURCE candidate |

The existing title's broad order is right. The build-relevant visible behavior is much narrower than “destroy every
component”: the mandatory sends are produced by AbortAll before component destruction, while most direct component
destructors only release local state/subscriptions. The virtual child destructors under the behavior and AI owners
remain honest recoverable gaps rather than assumed silence.

### 7.2 M1-045 idle go-to-sleep action

Current manifest text:

> **M1-045** — **IMPLEMENTATION_GAP** — “The idle-timeout go-to-sleep action:
> CreateGoToSleepAnimSequence queued on the ActionList.” Evidence names the three triggers and lift action; unresolved
> says the action is named, not built, and existing tests never exercise it.

| step | exact behavior | citation | record | proposed classification |
| --- | --- | --- | --- | --- |
| Arm deadlines | G2E `StartIdleTimeout {faceOffTime_s, disconnectTime_s}` uses timer seconds converted to `float`. A nonnegative faceOff is armed only after the first full state (`Robot+0x34E != 0`); disconnect needs no such gate. Each deadline becomes `now + duration` and only replaces `-1.0f (0xBF800000)` or a later existing deadline. | `0x0052CFDE..0x0052D06A` | M1-045 / M1-031 interface | EXACT_SOURCE candidate |
| Cancel | G2E Cancel writes both deadlines to `-1.0f (0xBF800000)`. | `0x0052D06C..0x0052D076` | M1-045 / M1-031 | EXACT_SOURCE candidate |
| Trigger | On Update, if faceOff deadline is `>0.0f (0x00000000)` and `<= now`, clears it to `0.0f`, creates the fixed action, and queues it NOW (`position 0`) with retry byte `0`. It then independently checks disconnect; if both expire in one tick, sleep is queued before disconnect. | `0x0052CE3C..0x0052CE6A`; disconnect `0x0052CE6E..0x0052CE98` | M1-045 | EXACT_SOURCE candidate |
| Sequential child | Allocates `CompoundActionSequential(robot)` and adds three `TriggerAnimationAction`s in order: trigger `0xD2`, then `0xD5`, then `0xD4`. Each constructor receives `(trigger, 1, 1, 0, 60.0f, 0)` with `60.0f = 0x42700000`. Shipped trigger mapping identifies these as `GoToSleepGetIn`, `GoToSleepSleeping`, `GoToSleepOff`. | `0x0052CEA2..0x0052CF58`; constructors `0x0052CECA..0x0052CEDE`, `0x0052CF0E..0x0052CF14`, `0x0052CF44..0x0052CF4A` | M1-045 | EXACT_SOURCE candidate |
| Parallel root | Allocates `CompoundActionParallel(robot)`, adds the sequential as one child, and adds `MoveLiftToHeightAction(robot, Preset 0, tolerance 5.0f)` as the other; `5.0f = 0x40A00000`. Returns the parallel root. | `0x0052CF62..0x0052CFBA`; lift `0x0052CF8E..0x0052CFA2` | M1-045 | EXACT_SOURCE candidate |
| Caller set | The only caller of the factory is the faceOff expiry at `0x0052CE5E`. There is no alternate trigger/name parameter. | factory `0x0052CEA2..0x0052CFC0`; call `0x0052CE5E` | M1-045 | EXACT_SOURCE candidate |

## Existing records contradicted by the source

1. **M3-027:** READ Data is not always empty. It is initially empty, then persists the most recent WRITE command
   chunk because `SetState(0)` does not clear the shared vector.
2. **M3-023:** the official Unity camera start does not send `EnableColorImages`, and its G2E `ImageRequest` only
   stores mode. The robot stream request is the earlier connection-time `ImageRequest {1,4}`.
3. **M3-033:** “queues 12 reads” is unconditional wording for a conditional path. FaceAlbum/FaceEnrollment are only
   queued after all VisionComponent prerequisites and `VisionSystem::Init == 0`.
4. **M3-037:** past-end bytes are not all undifferentiated heap contents. Retained/swapped allocations preserve
   earlier written bytes; only never-written allocation bytes are not source-determined.
5. **M4-003:** the current record omits QueueNow, which cancels/deletes a running NOW action before the replacement
   can encounter its track lock; Stop precedes unlock and the game completion follows destruction.
6. **M1-029:** the current C# reader's strict behavior is contradicted by the shipped Reader cases enumerated above,
   especially integer `-0`, prefix literals/trailing root text, leading zeroes, raw control/invalid UTF-8 bytes, the
   empty-key object comma special case, and the throwing depth limit.

## Existing records whose evidence is too weak to settle the claimed path

- **M3-023** proves the color-enable helper but not the camera-start production path.
- **M3-033/M3-034** need a separate Vision-init gate record; a settled queue/sink claim cannot hide that conditional
  behavior in prose.
- **M3-037** mixes exact buffer-reuse/truncation behavior with only the never-written-byte policy.
- **M4-003** proves motion arguments but not queue ownership, cancellation, result, stop/unlock ordering or completion
  wire/game effects.
- **M1-029** proves the firmware comparison but not the parser that determines its input values.
- **M1-044** still needs child-destructor extraction if settlement is meant to assert that every nested destructor is
  externally silent.

## Open questions / remaining source work

1. Decode the ARM EHABI tables for the firmware loader thread to establish the final fate of a depth-limit
   `Json::RuntimeError`. No local catch was found, but this report does not infer the final handler.
2. Resolve the virtual child destructors owned by BehaviorManager/BehaviorSystemManager and AIComponent if M1-044 is
   intended to claim absence of every possible game/wire side effect rather than only the directly called bodies.
3. Decide record boundaries: the camera-start path, Vision-init gate, jsoncpp Reader, and action-queue semantics are
   each behavior-changing production paths that the current records only partially own.
