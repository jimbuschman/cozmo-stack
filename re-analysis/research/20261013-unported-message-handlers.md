# R-HANDLERS — unported robot-message handlers

## Item 1 — coverage: Impl HandleFWVersionInfo, D2

| Step | Original behavior | Native citation | Record / classification |
|---|---|---|---|
| Registration | D2 binds HandleFWVersionInfo, with Impl owner and Robot argument | 0x00532AB6..0x00532AC4; body 0x00533348 | M2-019 / RECOVERABLE_GAP; native read |
| Read payload | Get_factoryFirmwareVersion returns union body at +4; handler passes event+0x0C | 0x0053335E..0x00533362; getter 0x007B41E0..0x007B41E2 | M2-019; native read |
| Copy state | Copy all 44 payload bytes into Impl+0x0C..+0x37, unconditionally | 0x00533366..0x00533376: LDM/STM of 5 then 6 words | M2-019; native read |
| Engine→robot hash check | Compare 16 bytes at Impl+0x18 against 0x00C51870 | 0x0053337A..0x00533398, memcmp at 0x00533392 | M2-019; native read |
| First mismatch formatting | Convert received then engine hash to spaced uppercase hexadecimal, mode=1, spaces=true, limit=0x100 | 0x0053339A..0x00533422; calls 0x005333AE and 0x005333F4; helper 0x007FB720..0x007FB842 | M2-019; native read |
| First mismatch log | Warning, no channel argument, key RobotFirmware.VersionMismatch; full format and arguments below | 0x00533424..0x00533454; key 0x00533750; format 0x00BE8F1F | M2-019; native read |
| First mismatch state | Store byte 1 at Impl+0x38; no matching-hash clear | 0x0053347A..0x0053347C, match jumps to 0x00533480 | M2-019; native read |
| Robot→engine hash check | Compare 16 bytes at Impl+0x28 against 0x00C51880 | 0x00533480..0x0053349E, memcmp at 0x00533498 | M2-019; native read |
| Second mismatch formatting/log | Same conversion; Warning, same key; direction-specific format and arguments below | 0x005334A0..0x00533558; conversions 0x005334B4 / 0x005334F8; format 0x00BE8F8E | M2-019; native read |
| Second mismatch state | Store byte 1 at Impl+0x39; no matching-hash clear | 0x0053357E..0x00533580, match jumps to 0x00533584 | M2-019; native read |
| Report gate | If both stored flag bytes are zero, skip report. Otherwise normalize them to booleans | 0x00533584..0x005335AC | M2-019; native read |
| Construct report | EngineRobotCLADVersionMismatch: flags, engine E→R hash, engine R→E hash, received E→R hash, received R→E hash | 0x00533592..0x005335AE; constructor helper 0x005376AC..0x00537758 | M2-019; native read |
| Broadcast | Wrap report in MessageEngineToGame and call Robot::Broadcast; ignore returned success/failure | 0x005335B2..0x005335C4; union move constructor 0x00723594..; Robot::Broadcast 0x00511DE8..0x00511DFC | M2-019; native read |
| Missing app interface | Robot::Broadcast returns false without delivering if context+4 is null; handler still retains its copied payload/flags | 0x00511DE8..0x00511DFC, especially CBZ 0x00511DEC | M2-019; native read |
| Cleanup/errors | Dispose temporary strings/union; stack-canary failure calls __stack_chk_fail. Exception cleanup resumes unwinding; no handler-specific recovery/rejection branch | 0x005335C8..0x0053364C; landing pads 0x00533650..0x0053373E | M2-019; native read |
| Repeated/matching later messages | Flags are sticky; current mismatches log every time. A later fully matching message still broadcasts if either stored flag was previously set, with empty current hash strings | Flag writes and report gate above; four local strings zero-initialized 0x00533386..0x0053338E / 0x00533488..0x00533494 | M2-019; instruction-derived consequence |

## Item 2 — coverage: Impl HandleDockingStatus, D3

| Step | Original behavior | Native citation | Record / classification |
|---|---|---|---|
| Registration/read | D3 binds handler; obtain dockingStatus from event+0x0C | Bind 0x00532C84; body 0x00536F1C..0x00536F68; getter call 0x00536F2C | M2-019 / RECOVERABLE_GAP; native read |
| Status selection | Load payload byte +4 with sign extension; EnumToString(Status) | 0x00536F30..0x00536F34 | M2-019; native read |
| Status text | 0 → STATUS_BACKING_UP; 1 → STATUS_DOING_HANNS_MANEUVER; other values → null pointer | 0x007C115C..0x007C1172; strings 0x00C206BF / 0x00C206D1 | M2-019; native read |
| Log | Event API (DAS level 2), no channel argument, key robot.docking.status, format %s, argument the returned status pointer; empty structured-data vector | 0x00536F20..0x00536F40; key 0x00536F94; format 0x00536FAC | M2-019; native read |
| Frequency | Calls the log API on every message delivered to this handler; no duplicate/status-change/active-behavior gate | Entire body 0x00536F1C..0x00536F68 | M2-019; native read |
| State / messages | No owner/Robot state writes, sends, broadcasts or forwards; only stack-local log-vector initialization and cleanup | Entire body; cleanup 0x00536F44..0x00536F68 | M2-019; native read |
| Errors | Unknown status is still passed as null to %s, not replaced with an invented string. Cleanup landing pad frees vector and resumes unwinding | Enum return 0x007C1166..0x007C1172; 0x00536F6A..0x00536F8E | M2-019; native read; phone-libc rendering of null %s UNKNOWN here |
| Logger availability | A null gLoggerProvider suppresses emission; this is a common logging gate, not a docking-message filter | sEventF 0x0080D014..0x0080D060 | M2-019; native read |

## Item 3 — coverage: test-behavior raw handlers and inactivity

| Step | Original behavior / active check / observable effect | Native citation | Record / classification |
|---|---|---|---|
| FactoryTest subscriptions | Constructor binds D6, F3, B8, EE, D2 directly; raw callbacks bypass IBehavior's active/inactive dispatcher | 0x005D064A..0x005D068A → helper 0x005D0798..0x005D0830; callable operator 0x005D373E..0x005D3752 | M7-027 / RECOVERABLE_GAP; native read |
| FT D6 | Read factoryTestParam; Info log on Unnamed, StationID: %d; write payload word 0 to FT+0x168. No active check | 0x005D0864..0x005D08B8; log 0x005D088A; store 0x005D08B2 | M7-027; native read |
| FT F3 | Read/copy activeObjectAvailable; if object-type word at payload+4 is valid light cube or charger, set FT+0x33E=1; otherwise leave it. No active check/log/send | 0x005D0930..0x005D097A; predicates 0x007D1D08..0x007D1D74; store 0x005D0974 | M7-027; native read |
| FT B8 | Read pickAndPlaceResult; FT+0x1B3 = payload byte +4 AND (payload byte +6 equals 2 ? 1 : 0). Overwrites on every delivery. No active check/log/send | Complete body 0x005D097C..0x005D099C, including store 0x005D0998 | M7-027; native read |
| FT EE parse | Construct string from firmwareVersion vector begin/end, parse with Json::Reader, collectComments=true. No active check | 0x005D09A0..0x005D09E2 | M7-027; native read |
| FT EE parse failure | EndTest(Robot at FT+0x368, result 0x46/PARSE_HEADER_FAILED), then cleanup; no version/build assignment | 0x005D0A0A..0x005D0A16; enum string table 0x01033A00 + 0x46*4 → 0x00C1BBBD | M7-027; native read |
| FT EE parsed fields | Replace FT string +0x350 with JSON version.asString(), then string +0x344 with build.asString() | 0x005D09E4..0x005D0A32; 0x005D0A34..0x005D0A78 | M7-027; native read |
| FT EE compare/state | If version differs from F1.5.1 OR build differs from exactly FACTORY, set FT+0x340=1. Matching input does not clear it | 0x005D0A7A..0x005D0B0C; expected-version initialization 0x004D7966..0x004D7996; FACTORY at 0x005D0BCC | M7-027; native read |
| FT EE JSON errors | asString converts scalar values; nonconvertible object/array values throw. Handler cleanup propagates exceptions rather than treating them as parse=false | 0x008E48B0..0x008E4A04, throwLogicError 0x008E4A04; handler 0x005D0B54..0x005D0BAE | M7-027; native read |
| FT D2 | Bare BX LR: no log/write/send/active check | 0x005D0BD4 | M7-027; native read |
| EndTest already complete | If FT+0x1C9 nonzero, Warning with old/new result names, then return without cancellation, queueing or state transition | 0x005D23EE..0x005D244A; log 0x005D2418 | M7-027; native read |
| EndTest first completion | Store result; Info logs PreWriteResult and PassFailStatus; timestamp, cancel Robot ActionList with type -1, derive pass-change flags, construct/queue factory-result entry, clear FT+0x33C, enter state 0x0F | 0x005D244C..0x005D2618; detailed writes/logs below | M7-027; native read |
| EndTest sends on EE failure | Result 0x46 is not pass, so direct SendQueuedWrites branch is unreachable. If previous-pass byte +0x16E is nonzero, WipeFactory is called; on immediate false result set +0x1C9=0x32 | 0x005D24BC..0x005D24FA; 0x005D254A; 0x005D25B6..0x005D260C | M7-027; native read; downstream NV transaction boundary below |
| FT later-run boundary | Station ID retained and copied into result; Init clears +0x1B3 and +0x33E; version/build and +0x340 are not cleared in that Init | Station read 0x005D256C..0x005D2570; resets 0x005D0C68..0x005D0C6C / 0x005D0E46..0x005D0E56; complete Init read 0x005D0C1C..0x005D113A | M7-027; native read; exhaustive later consumers UNKNOWN |
| DT constructor gate | Robot+0 is CozmoContext; context+0x20 is RobotManager; manager+0x60 is MessageHandler. Check each pointer before D3 subscription | 0x005CB186..0x005CB1BE; allocations/stores 0x004EA7A8..0x004EA7AC and 0x0052E4E8..0x0052E4FC | M7-027; native read |
| DT normal engine | Both gate inputs are allocated unconditionally during normal context/manager construction, before Robot is constructed | CozmoContext 0x004EA734..0x004EA89C; RobotManager 0x0052E47C..0x0052E604; AddRobot 0x0052EE9C | M7-027; native read; successful construction assumed |
| DT D3, status 0 | Increment uint32 counter DT+0x25C by one | 0x005CB368..0x005CB37A, store 0x005CB376 | M7-027; native read |
| DT D3, status 1 | Set byte DT+0x26C=1; other statuses leave both fields unchanged | 0x005CB36A..0x005CB382, store 0x005CB37E | M7-027; native read |
| DT inactive observability | No active check/log/send. Fields accumulate while inactive; result reporting reads them, but a new trial and manual-reset paths explicitly zero them | Entire handler 0x005CB35C..0x005CB382; reads 0x005CE53C / 0x005CE54E / 0x005CE82C..0x005CE832; resets 0x005CC310..0x005CC318 and 0x005CDC5A..0x005CDC68 | M7-027; native read |
| LL raw dispatch | Always virtual +0x60, then active +0x6C or inactive +0x78 selected by IBehavior+0xA1 | 0x005BF2F8..0x005BF320 | M7-027; native read |
| LL own slot targets | Installed vtable 0x01028288: +0x60 → 0x0059EC42 BX LR; +0x6C → 0x005D4FE4; +0x78 → 0x0059EC4E BX LR | Relocation slots 0x010282E8 / 0x010282F4 / 0x01028300; target instructions | M7-027; native read |
| LL active DA writes | Set +0x224=1; +0x225=payload has-load byte; increment +0x220 only when nonzero; increment +0x21C on every DA | 0x005D4FE4..0x005D501E | M7-027; native read |
| LL active DA log | Debug, Unnamed, key BehaviorLiftLoadTest.HandleLiftLoad.HasLoad, format %d / %d, updated load-count then updated total-count | 0x005D5010..0x005D5032; key 0x00BF398E; format 0x005D5118 | M7-027; native read |
| LL unexpected active tag | Info, Unnamed, InvalidRobotToEngineTag key, format Received unexpected event with tag %hhu.; no owner write | 0x005D5038..0x005D504C; key 0x005D50A4; format 0x005D50E4 | M7-027; native read; raw subscription takes DA only |
| LL inactive observability | Both called raw slots are no-ops: no DA log, state change, send, or buffering for a later run | Slots and raw dispatcher above; run initialization zeros counters 0x005D415A | M7-027; native read |

## Evidence and scope

Answers `requests/20261013-unported-message-handlers.md`, against shipped `resources/lib/armeabi-v7a/libcozmoEngine.so` 3.4.0-1204, SHA-256 `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`. Addresses are ELF virtual addresses with Thumb bit removed for code. Native instructions, PC-relative constants and ELF relocations are primary evidence. Capstone uses `skipdata=True`; decompilation/index files are navigation only. Disassembly can display data as instructions outside actual function bodies; only the cited code paths support the findings.

The corrected/adopted subscriber check is the context for double instantiation and registration order. It replaces the previous Codex report's incorrect assumption that behavior subscriptions follow components. This extraction does not repeat the whole loader/connection audit. In the checked construction path, each of the three test behaviors has two instances, BehaviorManager's first, BehaviorSystemManager's second; raw FT/DT/LL delivery precedes the respective later component/action handlers. Whether behavior-map loading is complete at every possible AddRobot entry remains the loader-gating question explicitly left open by that check.

All coverage classifications remain the current manifest **RECOVERABLE_GAP**. “Native read” reports recovered evidence; it is not a status promotion or a claim that downstream NV/action machinery was re-extracted in full.

## Item 1 details — payload, logs and report

The entire D2 payload is copied, including its first 12 metadata bytes. The two arrays occupy payload+0x0C..+0x1B and +0x1C..+0x2B, hence Impl+0x18 and +0x28. This handler does not branch on those first metadata words. The compared engine constants are:

```text
E→R @ 00C51870: D5 35 42 A1 7B B8 97 69 D8 09 4E CE 5A 96 4A 9E
R→E @ 00C51880: AB 12 BA BA 15 72 95 40 B4 1D 23 16 7F 24 59 A2
```

The mismatch bytes are initialized together to zero by the Impl constructor at 0x00532952. This handler subsequently only writes 1 to them. It does not reset a previously recorded mismatch when later data matches.

Its **only two log sites** are:

1. Call **0x00533454**, Warning (`sWarningF`, PLT 0x004A4540), no channel argument, key **`RobotFirmware.VersionMismatch`** at **0x00533750**. Format at **0x00BE8F1F**:
   `Engine to Robot CLAD version hash mismatch. Robot's EngineToRobot hash = %s. Engine's EngineToRobot hash = %s.`
   Arguments: received E→R hash string, engine E→R hash string, in that order. The first is passed in r3, the second on the stack at 0x00533444.
2. Call **0x00533558**, same level/channel/key. Format at **0x00BE8F8E**:
   `Robot to Engine CLAD version hash mismatch. Robot's RobotToEngine hash = %s. Engine's RobotToEngine hash = %s.`
   Arguments: received R→E hash string, engine R→E hash string, in that order; second argument stored at 0x00533548.

All structured-data vectors are empty at these sites. Conversion mode 1 produces one leading space and two uppercase hex digits per byte (`" XX XX ..."`), not lowercase, not a compact hash, and not a trailing space. The 16-byte hash produces 48 characters, so the 0x100 limit does not truncate it. Native conversion instructions: 0x007FB7B8..0x007FB81E; append and truncation condition 0x007FB814..0x007FB83A.

Logs depend on **the current comparison**, while app reports depend on **stored flags**. Therefore repeated unequal messages warn repeatedly. Once a flag is set, a later equal message still reports that flag; its corresponding strings are empty because this invocation only constructs hash strings in the unequal branch. Both directions are processed independently, in E→R then R→E order, followed by one app report if either stored flag is nonzero.

The report helper copies two boolean bytes at output+0/+1, then four std::string fields at +4, +0x10, +0x1C, +0x28. Argument setup at 0x00533592..0x005335A0 proves their order: engine E→R, engine R→E, robot E→R, robot R→E. The original generated app class corroborates the names/order in `unity/scripts/csharp/Anki.Cozmo.ExternalInterface/EngineRobotCLADVersionMismatch.cs:145`; the native helper, not that file, establishes the behavior. The MessageEngineToGame move constructor at 0x00723594 assigns tag **0x2A**. Delivery through Robot::Broadcast reaches context's external-interface virtual +0x18 when non-null (0x00511DF0..0x00511DF4).

There is **no EngineToRobot send, disconnect request, firmware acceptance/rejection decision, or numeric firmware-version comparison in this body**. A CLAD mismatch writes flags, warns and reports it. The separate initial-connection firmware policy is not this handler. The native getter here is simply pointer+4, with no runtime tag-validation branch. Memory allocation/copy/logging failures are not converted into a handler result; cleanup landing pads propagate exceptions. Stack corruption reaches __stack_chk_fail at 0x0053364C.

## Item 2 details — log API and error boundary

The D3 handler has exactly one logging call, **0x00536F40**, and calls it for every delivered D3, even if the status equals the preceding message. It does not log a robot ID, action ID or object ID: the sole variadic argument is the enum text. Both test-behavior D3 handlers may change their own counters first, but they do not suppress this later Impl event call.

The logging call is **Event**, not an Info call merely inferred from its name. `sEventF` formats with vsnprintf into 0x400 bytes and invokes logger virtual +8 at 0x0080D048..0x0080D05E; DasLoggerProvider's corresponding slot resolves to 0x006671D0 and passes DAS level **2**. Warning's slot resolves to 0x006671E8 and passes level **3**. Channel is **absent from these non-channeled APIs**, not a handler-supplied `Unnamed`. The test-behavior Info/Debug calls below explicitly supply `Unnamed`.

When Status is neither 0 nor 1, the handler still calls Event with EnumToString's **null** argument. It has no invalid-status warning or fallback string. Exact phone-libc null-%s formatting is **UNKNOWN in this extraction**; do not replace it with a guessed enum name. The native docking getter is pointer+4 at 0x007B42A8, with no tag check. The function's exception landing pad, 0x00536F6A..0x00536F8E, cleans temporary storage and resumes unwinding; it does not change docking/robot state.

Common logger details apply to both items: a null global logger suppresses emission; vsnprintf limits the formatted message to 1023 characters plus terminator. Warning calls additionally route through 0x0080D334 → 0x0080DB28 and prepend the common tick prefix: with gTickTimeProvider non-null, `(tc` + zero-filled decimal tick count with minimum width 4 + `) `, then `: `; without it, just `: `. The branch/formatting is 0x0080DCC4..0x0080DD2E. Event does not use this Warning prefix helper. Sink filtering/storage policy belongs to the existing logging records; a handler log call is not a guarantee that a particular sink displays it.

## Item 3 details — FactoryTest

**D6:** `sChanneledInfoF` at **0x005D088A**, channel **`Unnamed`** at **0x00BE3FEC**, key **`BehaviorFactoryTest.HandleFactoryTestParameter.Recvd`** at **0x005D08E8**, format **`StationID: %d`** at **0x005D0920**, argument the 32-bit word at factoryTestParam+0, placed on stack at 0x005D0882. The store to FT+0x168 occurs after logging/cleanup. No active flag is read. Two subscribed instances therefore make two Info calls and each stores its own station ID. EndTest later reads this field at 0x005D256C and writes it into result-entry field FT+0x224 at 0x005D2570.

**F3:** the copied message's object-type word is at payload+4. With each predicate's fallback boolean explicitly false, native IsValidLightCube accepts numeric object types **1, 2, 3**, and IsCharger accepts **13**. TBB at 0x007D1D12 uses table 0x007D1D16, with those cube cases returning 1 at 0x007D1D40; charger comparison/return is 0x007D1D64..0x007D1D6A. Valid input sets +0x33E=1; unrelated input does not clear it. No log or message is emitted. This occurs while inactive as well as active.

**B8:** the true entry is **0x005D097C**, not the store instruction 0x005D0998 named in the request. It computes `payload[4] & (payload[6] == 2 ? 1 : 0)` and overwrites +0x1B3. That is a bitwise byte result, not a comparison against an inferred result-enum name. No active check, log or message exists in the complete 34-byte body. Factory Init resets +0x1B3 at 0x005D0C6C. Init also clears +0x33E via a halfword store **at +0x33D**, 0x005D0E48, then may set +0x33E/+0x33F to 1 at 0x005D0E56 when Robot byte +0x14 is zero. Thus an inactive F3/B8 delivery does not by itself prove that its accumulated flags survive the next Init.

**EE:** the received firmwareVersion byte vector is copied into a string and parsed. On success the JSON `version` and `build` members are converted by Json::Value::asString and stored in the behavior's two string objects. The version target is exactly **`F1.5.1`**, a six-character short string constructed in `_INIT_24`: byte 0x0C length encoding at global 0x0105B18C, ASCII bytes written at +1..+6, terminator at +7 (0x004D7978..0x004D7992). This is not the app's release label. Build must be exactly seven-character **`FACTORY`**, case-sensitive. Any inequality sets +0x340 to 1; later matching firmware leaves that byte unchanged. There is no direct log or send on the success path, and no active check. The handler can replace version/build and latch the mismatch state in both inactive instances.

`asString` is not restricted to string JSON values: null becomes empty, bool becomes true/false text, numeric scalars use native number-to-text functions, and strings are copied (0x008E48B0..0x008E4960). Object/array conversion goes to throwLogicError (0x008E4964..0x008E4A04). Exceptions propagate through the handler's cleanup; they are not rewritten into PARSE_HEADER_FAILED. Changes made before a later conversion failure are not rolled back; e.g. version may already have been replaced when build conversion throws.

**D2:** factoryFirmwareVersion handler **0x005D0BD4** is a no-op. Two registrations exist, but neither creates logs/state/messages.

### EE parse failure: complete EndTest branch at the handler boundary

The parse-false path is observable even while inactive. It calls EndTest with the stored Robot pointer (+0x368, set in constructor at 0x005D05AA) and code **0x46, PARSE_HEADER_FAILED**. The constructor sizes its timestamp vector to 18 entries at 0x005D0692, so EndTest's vector accesses do not require a prior successful run.

EndTest first reads result byte +0x1C9:

- **Already nonzero:** Warning at **0x005D2418**, no channel argument, key **`BehaviorFactoryTest.EndTest.TestAlreadyComplete`** at **0x005D2688**, format **`Existing result %s (new result %s)`** at **0x005D26B8**. Arguments: enum text for existing result, then enum text for new 0x46 result. Return after temporary cleanup. No further owner write or action cancellation occurs on this branch.
- **Zero:** store 0x46 to +0x1C9 at **0x005D244E**. Info at **0x005D2472**, `Unnamed`, key **`BehaviorFactoryTest.EndTest.PreWriteResult`** at **0x005D26E0**, format **`%s`** at **0x005D270C**, argument `PARSE_HEADER_FAILED`. Store current BaseStationTimer timestamp into timestamp-vector data+0x44 at **0x005D24AE**. Call **Robot ActionList::Cancel(-1)** at **0x005D24B8**, with list from Robot+0x250. This call is not limited to FactoryTest-owned actions and is not gated by active state.
- Read previous-pass byte +0x16E; derive current-pass `(result == 1)`, store pass-change boolean at **+0x16F** (**0x005D24E4**) and previous-pass-and-current-failure boolean at **+0x170** (**0x005D24FA**). Info at **0x005D2516**, `Unnamed`, key **`BehaviorFactoryTest.EndTest.PassFailStatus`** at **0x00BF36EB**, format **`prevPassed: %d, currPassed: %d`** at **0x00BF3716**, arguments original previous-pass byte and normalized current-pass. For 0x46 the second argument is zero.
- Set **+0x1D8=0** (0x005D2554), copy result byte to **+0x228** (0x005D2558), store sign-extended phone `time(nullptr)` into **+0x1D0/+0x1D4** (0x005D255E..0x005D2564), store station ID into **+0x224** (0x005D2570), and copy 0x48 bytes of timestamp-vector data into **+0x1DC..+0x223** (0x005D2578..0x005D257E).
- Size and pack the FactoryTestResultEntry starting at +0x1D0 (0x005D2582..0x005D259E). QueueWriteToRobot at **0x005D25B0** takes entry tag **0x80000010** and the packed bytes. QueueWriteToRobot **appends a retained WriteEntry to FT's list +0x330**, increments count +0x338 at 0x005D1978..0x005D198C, and installs a completion callback; it does not itself call MessageHandler/NVStorage::Write. Clear **+0x33C=0** at **0x005D25BA**.
- The SendQueuedWrites call at 0x005D25C4 requires current-pass and not previous-pass, so it **cannot execute for result 0x46**. If +0x170 is true, call **NVStorageComponent::WipeFactory** using Robot+0x260 at **0x005D25EC**. If it returns false immediately, set **+0x1C9=0x32** at **0x005D260C**. This conditional branch can run while the behavior is inactive if its previous-pass state is still true.
- Call SetCurrState(0x0F) at **0x005D2614**. Helper **0x005D14A8..0x005D14E8** stores the state byte at **+0x128**, updates deadline **+0x1AC = current seconds + 20** if state changes, and stores current timestamp into timestamp-vector entry 15. It calls UpdateStateName (0x005D34B0..0x005D3500), which constructs/discards a temporary name string; it is not another log or app-message send. Stack-canary failure at **0x005D2634** aborts.

For a freshly constructed, never-run FactoryTest, previous-pass +0x16E and result +0x1C9 are zero (constructor stores 0x01000000 at +0x16C at 0x005D04CE and clears +0x1C8/+0x1C9 at 0x005D050A). A parse failure therefore produces the two Info logs, result/state writes, action cancellation and **queued result entry**, without immediate SendQueuedWrites or WipeFactory. On another failed message, result is already set and the Warning branch runs instead. Two instances can each perform these effects; a shared ActionList mutation is visible to the second callback.

The exact canceled-action cleanup and NV wipe/write wire lifecycle are separate component boundaries. Their entry calls/conditions are established here; **UNKNOWN in this extraction** are all possible wire messages generated by cancellation of arbitrary current actions and the full asynchronous WipeFactory/completion-callback path. A queued WriteEntry is not evidence of an immediate robot write. No direct app-facing broadcast/send exists in the five raw FT bodies.

### DockingTestSimple and LiftLoadTest

**Docking gate:** it is a **RobotManager / MessageHandler** gate, not an external-interface/app/SDK gate. Normal CozmoContext construction allocates RobotManager, then stores it at context+0x20 at 0x004EA7AC. RobotManager allocates MessageHandler and stores it at +0x60 at 0x0052E4FC. Both happen irrespective of context's external-interface pointer. Null inputs skip subscription; successfully constructed normal engine inputs pass the gate.

**Docking D3:** status 0 increments the 32-bit backing-up counter at +0x25C; status 1 sets the Hanns-maneuver-seen byte +0x26C. It ignores payload's other fields. No active check, logging or sending occurs in this handler. Each inactive instance can accumulate these fields. Trial-result reporting reads them at 0x005CE53C/0x005CE54E and 0x005CE82C..0x005CE840; starting a new trial resets them at 0x005CC310..0x005CC318, and the manual-reset event path resets them at 0x005CDC5A..0x005CDC68. Do not infer that all pre-run accumulation will survive those resets. The handler does not itself trigger reporting.

**LiftLoad DA:** the relocated vtable header is **0x01028280**, installed pointer **0x01028288**. Raw +0x60 is **0x0059EC42**, raw inactive +0x78 is **0x0059EC4E**, both single `bx lr` instructions. Raw active +0x6C is **0x005D4FE4**, complete through return 0x005D5074. Therefore both inactive instances discard DA without log, update, queue or message.

When active, the handler always records receipt (+0x224), overwrites the load byte (+0x225), increments total (+0x21C), conditionally increments load-count (+0x220), then logs Debug at **0x005D5032**: channel **`Unnamed`**, key **`BehaviorLiftLoadTest.HandleLiftLoad.HasLoad`** at **0x00BF398E**, format **`%d / %d`** at **0x005D5118**, updated load-count then total. Its unexpected-tag path logs Info at **0x005D504C**: `Unnamed`, key **`BehaviorLiftLoadTest.HandleWhileRunning.InvalidRobotToEngineTag`** at **0x005D50A4**, format **`Received unexpected event with tag %hhu.`** at **0x005D50E4**, argument unsigned raw tag byte. Since this subscription is DA-only, that branch is not reached through correct ordinary DA dispatch. Neither branch sends messages. Init resets counts +0x21C/+0x220 at 0x005D415A.

Do not substitute LiftLoadTest's other overloads: **0x005D4E64** handles EngineToGame RobotOffTreadsStateChanged; **0x005D4F30** handles GameToEngine input and sets +0x219. Neither is a raw DA slot.

## Manifest disposition and remaining uncertainty

Current **M2-019**, **RECOVERABLE_GAP**, title “RobotToEngineImplMessaging subscribers the stack lacks: HandleFWVersionInfo (D2) and HandleDockingStatus (D3)”, cites the adopted subscriber check's D2 bind/body 0x00532AC4/0x00533348 and D3 bind/body 0x00532C84/0x00536F1C. The bodies establish the missing state/log/app-report behavior, including persistent mismatch flags and every-message docking logging. No current claim in that record is contradicted.

Current **M7-027**, **RECOVERABLE_GAP**, title “Dev test behaviours (FactoryTest, DockingTestSimple, LiftLoadTest) built twice at Robot construction: their raw robot-message subscriptions and handler bodies”, cites the same checked double-container construction, recursive asset loading, FT binds, DT gate, LL dispatcher, FT D2 no-op/B8 store and DT counters. These claims agree with the recovered bodies. LL's inactive DA no-op and FT's ungated firmware parse-failure effects materially refine the observable boundary; they do not justify dropping the subscriptions or replacing double construction with one instance.

Remaining recoverable boundaries: load/connect gating already left open by the adopted check; exhaustive later consumers of retained FT version/build/mismatch state; downstream arbitrary-action cancellation and asynchronous NV wipe/completion behavior; exact scheduling of later reports relative to new-trial resets. Platform libc's rendering of null `%s` is not established here. These are explicit limits, not invented behavior or grounds for a status upgrade.

Local supporting reads: `20261013-unported-message-handlers-disasm.py`, `-native.txt`, `-body.txt`, `-callees.txt`, `-init.txt`, `-constants.txt`, `-slots.txt`, `-getters.txt`, `-errors.txt`, `-warning-prefix.txt`, `-readers.txt`, `-factory-readers.txt`, `-final-focus.txt`, `-final-check.txt`. The slots file records ELF relocation-resolved targets; the readers files isolate native state reads/resets. All are under the research directory.

Research only: no production code, manifest, inventory or PROJECT_STATE changes; no branch, worktree, commit, upload, build/test, engine execution or hardware run. No network endpoint was contacted. Regression test / commit: not applicable; the manager must check these citations before incorporating them into an inventory.
