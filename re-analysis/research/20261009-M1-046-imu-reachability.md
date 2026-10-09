# M1-046: IMU file logging reachability (2026-10-09)

Read-only extraction answering the operator's 2026-10-09 final-Opus corrections request. No production, inventory, approval or manifest change. These rows need manager checking before adoption.

## Coverage and conclusion

| Boundary | Result |
|---|---|
| Live robot channel registration | Both IMU handlers are registered unconditionally during Robot construction. |
| Handler-to-file gates | Only packet fields gate opens/closes; no app debug, SDK, robot state/time-sync or debug-enabled flag is read on either handler's normal entry path. |
| File destructor | The messaging file buffer calls close on destruction; close synchronizes and calls fclose when a FILE pointer is present. |
| Requests/default robot emission | UNKNOWN for an ordinary robot without a request. Neither absence of a Unity caller nor unsupported app debug UI proves the inbound engine handler unreachable. |
| Proposed owning layer | NEW M3 devices: diagnostic IMU chunk reception and file stream lifetime. M1-046 points to the new owner's destructor recipient. |

**The engine file-logging sink is reachable without enabling app debug features:** a received tag 0xBF packet with sequenceId 1 (constructor cache is 0), or a tag 0xC7 packet with order 0, reaches file open with no debug flag test. This is an engine ingress-path proof, not a claim that firmware emits such packets spontaneously. The available primary engine source cannot support an unconditional `unreachable` classification for this sink. The phone app's missing producer is not a gate in the engine receiver.

Primary binary: `resources/lib/armeabi-v7a/libcozmoEngine.so`, SHA256 `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`. Fresh function/range transcript: [20261009-M1-046-imu-native.txt](20261009-M1-046-imu-native.txt). Function-size dumps include literal pools and exception landing pads after normal returns; the cited normal-path instruction ranges distinguish them from executable normal flow. Addresses below are ELF addresses; Thumb symbol bit 0 is removed.

## Current claim reread before extraction

```json
{
  "id": "M1-046",
  "title": "Channel/member subscription retirement recipients",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "Manager adopted U1-U8: re-analysis/jobs/B-M1M2.md Rows checked (manager, 2026-10-09); re-analysis/research/20261009-M1-final-extraction.md. Robot destruction 0x005112E6..0x00511308 -> InitSubscriptions/RobotLifetime.Destroy, idle owner +0x51C before messaging +0x518.",
    "Reverse shared-handle release 0x004EAE94..0x004EAEBA; last-owner virtual slots 0x004EF184..0x004EF1CC; vtable 0x0101FE28 -> Thumb 0x0051D8F8, deleting slot 0x0101FE24 -> Thumb 0x0051D8D4. Messaging unsubscribe 0x0051D8F8..0x0051D906 -> 0x0051D338 callback-clear and unlink; idle target 0x00664EB8 -> 0x0066365C. RobotSubscriptions.cs RetainedSubscription/SubscriptionSignal/SubscriptionOwners implement these checked recipients.",
    "Messaging member retirement 0x00532A10..0x00532A74: +0x144/+0x138/+0x110/+0x104, +0xF8 vector, filebuf/ios, base. Native storage containers and stream/base subobjects have no separately retained managed resources; managed endpoint retires the owned vector in reverse order without invoking callbacks."
  ],
  "unresolved": "Final M1 Opus pass (2026-10-09, re-analysis/research/20261009-M1-final-opus-pass.md): NOT YET. The retirement path holds (vtable 0x0101FE18 slot -> 0x0051D8F8, 0x0051D338, the container dtor 0x004EF1B8, reverse release 0x004EAEA2, +0x51C before +0x518). OMITTED: ~basic_filebuf (0x00532A64) closes the Messaging ofstream that HandleImuData/HandleImuRawData open (open 0x00535E76/0x0053649C, close 0x00535F5C/0x005361F4); no record covers IMU file logging. Add one (or show it unreachable) and point M1-046 at it. Before: built, awaiting strong verification: U1-U8 last-owner unsubscribe, callback-clear/unlink, reverse vector release and idle-before-messaging retirement. Native member containers/filebuf/ios/base are managed storage with no separate destructor effects; this representation correspondence needs strong review. Higher-layer recipients remain with their own records; no settlement."
}
```

## Extracted rows

| Row | Original step | Reopened primary citation | Owner / classification of evidence |
|---|---|---|---|
| I1 | Every Robot constructs Messaging, stores it at +0x518, and initializes it with the engine MessageHandler and robot id. | Robot constructor 0x0051020A..0x00510218, ctor PLT 0x004A77EC -> 0x00532944; 0x00510334..0x00510348, init PLT 0x004A781C -> 0x00532A7C. There is no debug branch around these calls. | M1-046 owner lifetime; EXACT_SOURCE row, not record settlement. |
| I2 | Init installs HandleImuData for tag 0xBF and HandleImuRawData for tag 0xC7 using the same retained subscription helper as the normal message handlers. | 0x00532BC8..0x00532BE8: GOT 0x0103EAA0 / 0x0103EAA4 names these two handlers; both call 0x00533234. The straight-line init path has no app-debug test. Helper 0x00533234..0x0053330C constructs the bound callback, calls subscription through MessageHandler path 0x00519F8C and retains returned handle. | NEW M3 diagnostic IMU receive endpoint; EXACT_SOURCE row. |
| I3 | Messaging's cached processed-IMU sequence begins at 0; the stream/filebuf begins closed. | 0x0053294E movs r6,#0; 0x00532950 stores +0x3C. 0x0053299A constructs basic_filebuf; reopened target 0x00537E48. | NEW M3; EXACT_SOURCE row. |
| I4 | Processed-IMU handler reads payload sequenceId at +0xC0 and compares to Messaging+0x3C. A differing byte is stored to the cache, then it obtains context DataPlatform and creates/opens a log path. Equal sequence skips to sample writing. No debug/global/SDK test exists on this path. | 0x00535C5E getter PLT 0x004AA288; 0x00535C64..0x00535C76 compare/store; 0x00535C7C -> Robot::GetContextDataPlatform, reopened 0x00516BD2 returns context+8. 0x00535C82..0x00535CD0 uses kP_IMU_LOGS_DIR, scope 2 and CreateDirectory. Open 0x00535E6C..0x00535E76 calls basic_ofstream::open with mode 0x10. | NEW M3; EXACT_SOURCE entry/gate row. Full filename/format/error contract remains RECOVERABLE_GAP for new record. |
| I5 | Processed data writes samples on both new and continuing sequences; when payload chunkId equals numChunks-1, it logs and closes the filebuf. A failed close ORs failbit 4 into ios state. | Samples 0x00535EA2..0x00535F0E; final-chunk comparison 0x00535F10..0x00535F1C; close 0x00535F58..0x00535F70. | NEW M3; EXACT_SOURCE boundary row; byte-exact output formatting not claimed complete. |
| I6 | Raw handler reads packet `order` at +0xC: 0 starts a log, nonzero goes directly to row output; order 2 subsequently closes the log. Neither branch is selected by app state. | Getter 0x00536122 (PLT 0x004AA2A0); 0x00536128..0x0053612C order 0 -> 0x00536212. DataPlatform call 0x00536214; raw open 0x00536484..0x0053649C, mode0x10; header write0x005364A0..0x005364A6 then branch0x005364C8 ->0x00536132. | NEW M3; EXACT_SOURCE entry/gate row. Filename collision handling/format/errors remain RECOVERABLE_GAP for full new record. |
| I7 | Raw row prints packet timestamp, three accel shorts and three gyro shorts. `order==2` calls ClosingLogFile info and close; failed close sets failbit4. | 0x00536132..0x005361A4; 0x005361A8..0x005361AC final-order branch; 0x005361BE log name; 0x005361F0..0x00536208 close/error-state. | NEW M3; EXACT_SOURCE boundary row. |
| I8 | Both handlers use one Messaging ofstream at +0x40, filebuf +0x44. ofstream open adds output-mode0x10, opens its filebuf and clears state on success / sets failbit4 on failure. | Both call PLT 0x004AA294; reopened body0x0053779E..0x005377C6 -> filebuf open0x00538A3C..0x00538B22. | NEW M3; EXACT_SOURCE shared ownership row. |
| I9 | Messaging destructor releases member maps and subscription vector first, then destroys its filebuf, then ios/base. Filebuf destructor calls close even if no final chunk arrived. | 0x00532A44..0x00532A6A: vector release0x00532A48 before filebuf destructor0x00532A64, PLT0x004A6760 ->0x005010B4. Reopened0x005010C2..0x005010C4 calls close. | M1-046 dispatch/order + NEW M3 diagnostic file lifetime recipient; EXACT_SOURCE row. |
| I10 | Close checks filebuf FILE pointer+0x40; null returns null, nonnull invokes virtual sync slot+0x18, then fclose; successful fclose clears pointer and returns this only when sync result==0. | PLT0x004A69C4 ->0x0050111C..0x00501154; sync call0x0050112A; fclose0x00501130, reopened as phone stdio boundary (code does not ship); pointer clear0x00501140. The supplied basic_filebuf vtable0x0101F2BC address point+8 has slot+0x18 relocation0x0101F2DC ARM_ABS32 -> sync0x00501398; reopened sync invokes fwrite0x00501416 and fflush0x00501428 on its output path. | NEW M3; EXACT_SOURCE lifecycle/interface row; phone fclose semantics outside shipped engine are equivalent runtime boundary, not omitted close. |
| I11 | An app-to-engine IMURequest handler finds first robot and forwards length to Robot::RequestIMU with no debug gate. This is a request-input producer separate from inbound handler reachability. | 0x00529114..0x00529128 -> Thumb/ARM veneer0x008CB02C/30 -> PLT0x004A925C -> RequestIMU0x00517108..0x00517116 -> helper0x00516B50..0x00516B9C constructs request and calls Robot::SendMessage (PLT0x004A5368). | NEW M3 or app input COMPATIBILITY_POLICY if exposed by host; producer availability not asserted. |

## App cross-check and limits

The extracted APK app code lives outside this clone at `C:/Users/JimBu/Downloads/com.anki.cozmo_3.4.0-1204_minAPI21(armeabi-v7a)(nodpi)_apkmirror.com.apk_Decompiler.com/unity/scripts/csharp/`.

A search for `IMURequest`/`imuRequest` there finds the generated schema/union support but no non-generated Unity IMURequest producer. `Cozmo.Settings/SDKModal.cs:224..251`'s `SendIMUData` coroutine sends **phone** accelerometer/user-accelerometer/gyro messages, not robot IMURequest. It supplies no proof about robot diagnostic logging. These observations are authority2 cross-checks, not a replacement for the native gates in I1-I10. Native outgoing caller scan found no direct Thumb BL to the differently named SendIMURequest wrapper at0x00516B40; the actual handler uses an interworking veneer to RequestIMU at0x00517108, so that negative scan cannot establish no producer.

**UNKNOWN:** the complete firmware-side conditions for spontaneous IMU diagnostic chunk generation (firmware production implementation is outside the provided engine binary); whether a particular unrequested hardware connection ever sends those chunks; complete request-producer reachability beyond the named external-interface handler; complete filename/CSV/error handling for the new higher-layer record. Nothing here assumes a debug flag's default.

## Manifest impact for manager

Current M1-046's evidence phrase “Native storage containers and stream/base subobjects have no separately retained managed resources” is too broad for the filebuf: primary 0x005010C4 has an actual close effect when an input packet has opened the stream. Keep M1-046 IMPLEMENTATION_GAP pending the cross-layer split and its strong review. A new M3 record can own “IMU diagnostic chunk reception, file logging and stream close on robot removal”, initially RECOVERABLE_GAP for its full format/filename/error obligations, citing I1-I10; M1-046 owns the existing precise vector-before-filebuf-before-ios dispatch position and points to that record. No unreachable disposition is supported by the current engine receiver evidence.

No tests, builds or hardware work performed. Manager must check these rows before inventory adoption; this report is not implementation authorization or a settlement verdict.
