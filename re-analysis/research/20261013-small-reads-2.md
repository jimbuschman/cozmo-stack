# R-SMALL-2: unlock names, failed JSON reads, and dispatch residue

## Item 1 coverage

| Step | Primary evidence | Result |
| --- | --- | --- |
| Lazy map construction | 0x00794D92..0x00794DA8; 0x0079576A..0x007957AC | Guarded static map, 81 initializer pairs; destructor registered, guard released. |
| Every name/value pair | Individual string construction and integer stores in the table below | Decoded all 81, including Invalid and Count. |
| Unity cross-check | `unity/scripts/csharp/Anki.Cozmo/UnlockId.cs` | All 81 names and integer values agree; no extra or missing entry. |
| Scoped readAsJson | PLT 0x004A64E4 -> 0x0084BFD4..0x0084C006 | Resolve resource path, pass the caller's root to the filename overload. |
| Open/parse/failure | 0x0084C01C..0x0084C20A; especially 0x0084C0C2..0x0084C0CA and 0x0084C1FE | Calls Reader even on failed open; logs false parse; returns saved parse boolean; does not replace root on failure. |
| Parse into caller-owned root | 0x008E0938; 0x008E082C..0x008E091A | Read stream to a string, install caller root in Reader's node stack, parse directly. No transaction or rollback. |
| Empty/invalid first token | 0x008E0ADE..0x008E0B50 | Records offsets/error, returns false; does not replace root's value payload/type. |
| Partial object | 0x008E0ED0..0x008E0F6A; 0x008E10AC..0x008E11DE | Replaces root with object before member parsing; retains partially built object on false return. |
| InitConfig after failure | 0x0064BBA0..0x0064BC32 | Logs failed read, then still inspects root/defaultUnlocks if global defaults have not already been initialized. |

## Item 2 coverage

| Step/recipient | Primary evidence | Can destroy another same-tag subscriber or its owner synchronously? |
| --- | --- | --- |
| C4 TurnInPlace | 0x0054D3F8..0x0054D464 | No in this body: matching armed action records ack flag. |
| C4 MoveHead | 0x0054D624..0x0054D690 | No in this body: matching armed action records ack flag. |
| C4 MoveLift | 0x0054D748..0x0054D7B4 | No in this body: matching armed action records ack flag. |
| IDock C5 | 0x00557C4C..0x00557D5A -> QueueAction 0x0053D93C, position 5 -> 0x0053DB14 -> AddConcurrentAction 0x0053DF2C | Queues a new lift-safe animation concurrently. Does not cancel existing action queues. Rejection can delete the new action; indirect completion-report callbacks are not exhaustively closed here, so whole transitive-path safety is UNKNOWN. |
| IDock DA | 0x00557E82..0x00557E9E | No: stores load-result flag only. |
| PlayAnimation CA | 0x005452F0..0x00545350 | No: matching animation ID sets started flag. |
| PlayAnimation CB | 0x0054541C..0x005454F6 | No: decrements finite loop count and eventually sets ended flag. |
| PlayAnimation D5 | 0x0054561C..0x005456A0 -> 0x00545750..0x00545794 | Broadcasts external AnimationEvent synchronously. Direct body does not delete an action; full downstream subscriber mutation answer UNKNOWN. |
| Firmware EF | 0x00678C0A -> 0x00678044..0x00678124 -> 0x00676D6C -> 0x00676E30 | Success records ack; failure changes stage and broadcasts completion. No immediate updater-handle cleanup in the body. Full completion-recipient mutation answer UNKNOWN. |
| IActionRunner and Init | 0x00540370..0x0054063E -> IAction::UpdateInternal 0x00540D1C..0x00540F9E; Reset 0x00540CE8 | Successful initialization is gated by byte +0x70. Retry/reset clears it; non-successful Init can be called again. An unconditional once-per-object rule is false. |
| ConnectToRobot registration | CozmoEngine ctor call 0x004EB5CC -> 0x004EBB6C, subscribe at 0x004EBB9E | G2E tag 0x4C registered during engine construction. Closure 0x004EE936 -> 0x004EE94A -> PLT 0x004A544C -> 0x004ED018. |
| Robot destructor r5 | ctor 0x00510198/0x005101A6; destructor 0x005111EA..0x00511216 | Yes: Robot+0x450 is BlockTapFilterComponent, and r5 holds it at 0x0051120E. |
| No robot in ProcessMessages | 0x0069D8BC..0x0069D8D8 | Pop packet, increment received count, GetFirstRobot; null branches back to PopData. Packet is consumed without unpack/filter/dispatch. |

## Scope and method

Answers `requests/20261013-small-reads-2.md` (P3). Primary source is the local ARMv7 `resources/lib/armeabi-v7a/libcozmoEngine.so`, SHA-256 `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`. Addresses are native virtual addresses, checked against Thumb instructions and PLT/relocation targets. Ghidra output was navigation only (its `void` return for readAsJson is wrong: native 0x0084C1FE returns r7). No shipped files were uploaded, no hardware was run, and no production or manifest change is proposed as already approved.

Current manifest read for this request: **M15-044**, RECOVERABLE_GAP, "UnlockId name-to-value table and InitConfig details: UnlockIdFromString map builder; the root state after a failed readAsJson"; its evidence points to the undecoded builder/failure path in the backup/unlock check. **M2-022**, RECOVERABLE_GAP, "Robot-message handlers that might synchronously destroy another subscriber of the same tag (C4, C5, DA, CA, CB, D5, EF); IActionRunner re-Init of a live action; ConnectToRobot registration site"; evidence is the dispatch-residue check's unread handlers. This report supplies evidence for manager review; it does not settle either manifest record.

## Unlock map

Each pair below comes from the native builder, rather than filling native gaps with the Unity enum. The builder makes stack entries of 16 bytes, with the string at +0 and the integer at +12; 0x0079576A loads count 0x51 for the map constructor at 0x0079576E. The construction/string addresses and value stores make the pairing independently checkable.

| Native name | Value | String construction | Integer store |
| --- | ---: | --- | --- |
| `Invalid` | 0 | 0x794dc6 | 0x794dd2 |
| `All` | 1 | 0x794de0 | 0x794df6 |
| `Defaults` | 2 | 0x794dfe | 0x794e12 |
| `BuildPyramid` | 3 | 0x794e1a | 0x794e30 |
| `KnockOverThreeCubeStack` | 5 | 0x794e38 | 0x794e4c |
| `PickupCube` | 6 | 0x794e54 | 0x794e6a |
| `RollCube` | 9 | 0x794e72 | 0x794e86 |
| `StackTwoCubes` | 10 | 0x794e8e | 0x794ea4 |
| `TakeCubeFromHand` | 11 | 0x794eac | 0x794ec0 |
| `PounceOnMotionAction` | 12 | 0x794ec8 | 0x794ede |
| `PopAWheelieAction` | 13 | 0x794ee6 | 0x794efa |
| `DroneModeGame` | 14 | 0x794f02 | 0x794f18 |
| `KeepawayGame` | 15 | 0x794f20 | 0x794f34 |
| `MeetCozmoGame` | 16 | 0x794f3c | 0x794f52 |
| `MemoryMatchGame` | 17 | 0x794f5a | 0x794f6e |
| `QuickTapGame` | 18 | 0x794f76 | 0x794f8c |
| `FaceEnrollmentSlot0` | 19 | 0x794f96 | 0x794fa8 |
| `FaceEnrollmentSlot1` | 20 | 0x794fb0 | 0x794fc6 |
| `FaceEnrollmentSlot2` | 21 | 0x794fce | 0x794fe2 |
| `FaceEnrollmentSlot3` | 22 | 0x794fea | 0x795000 |
| `FaceEnrollmentSlot4` | 23 | 0x795008 | 0x79501c |
| `FaceEnrollmentSlot5` | 24 | 0x795024 | 0x79503a |
| `FaceEnrollmentSlot6` | 25 | 0x795042 | 0x795056 |
| `FaceEnrollmentSlot7` | 26 | 0x79505e | 0x795074 |
| `FaceEnrollmentSlot8` | 27 | 0x79507c | 0x795090 |
| `FaceEnrollmentSlot9` | 28 | 0x795098 | 0x7950ae |
| `Workout` | 29 | 0x7950b6 | 0x7950ca |
| `MysteryUnlock1` | 30 | 0x7950d2 | 0x7950e8 |
| `MysteryUnlock2` | 31 | 0x7950f0 | 0x795104 |
| `MysteryUnlock3` | 32 | 0x79510c | 0x795122 |
| `MysteryUnlock4` | 33 | 0x79512a | 0x79513e |
| `CozmoSaysGame` | 34 | 0x795146 | 0x79515c |
| `FistBump` | 35 | 0x795164 | 0x795178 |
| `CodeLabGame` | 36 | 0x795180 | 0x795196 |
| `PeekABoo` | 37 | 0x79519e | 0x7951b2 |
| `SparksGatherCubes` | 38 | 0x7951ba | 0x7951d0 |
| `Singing_Camptown` | 39 | 0x7951d8 | 0x7951ec |
| `Singing_ItsyBitsySpider` | 40 | 0x7951f4 | 0x79520a |
| `Singing_Beethovens5th` | 41 | 0x795212 | 0x795226 |
| `Singing_PopGoesTheWeasel` | 42 | 0x79522e | 0x795244 |
| `Singing_RowYourBoat` | 43 | 0x79524c | 0x795260 |
| `Singing_PachebelCanon` | 44 | 0x795268 | 0x79527e |
| `Singing_OdeToJoy` | 45 | 0x795286 | 0x79529a |
| `Singing_TwinkleTwinkle` | 46 | 0x7952a2 | 0x7952b8 |
| `Singing_FrereJacques` | 47 | 0x7952c0 | 0x7952d4 |
| `Singing_MaryHadALittleLamb` | 48 | 0x7952dc | 0x7952f2 |
| `Singing_TakeMeOutToTheBallgame` | 49 | 0x7952fa | 0x79530e |
| `Singing_WilliamTell` | 50 | 0x795316 | 0x79532c |
| `Singing_YankeeDoodle` | 51 | 0x795334 | 0x795348 |
| `Singing_BeautifulDreamer` | 52 | 0x795350 | 0x795366 |
| `Singing_MountainKing` | 53 | 0x79536e | 0x795382 |
| `Singing_TaRaRaBoom` | 54 | 0x79538a | 0x7953a0 |
| `Singing_EntryOfTheGladiators` | 55 | 0x7953a8 | 0x7953bc |
| `Singing_HelloMyBaby` | 56 | 0x7953c4 | 0x7953da |
| `Singing_TurkeyInTheStraw` | 57 | 0x7953e2 | 0x7953f6 |
| `Singing_LondonBridge` | 59 | 0x7953fe | 0x795414 |
| `Singing_Sakura` | 61 | 0x79541c | 0x795430 |
| `Singing_MussIDenn` | 62 | 0x795438 | 0x79544e |
| `GuardDog` | 63 | 0x795456 | 0x79546a |
| `TrackLaser` | 64 | 0x795472 | 0x795488 |
| `CozmoSings` | 65 | 0x795490 | 0x7954a4 |
| `Singing_AbaDaba` | 66 | 0x7954ac | 0x7954c2 |
| `Singing_Bingo` | 67 | 0x7954ca | 0x7954de |
| `Singing_BuffaloGals` | 68 | 0x7954e6 | 0x795500 |
| `Singing_CanCan1` | 69 | 0x795508 | 0x79551c |
| `Singing_CanCan2` | 70 | 0x79552c | 0x795542 |
| `Singing_DannyBoy` | 71 | 0x795552 | 0x795566 |
| `Singing_FarmerInTheDell` | 72 | 0x795576 | 0x79558c |
| `Singing_LaPaloma` | 73 | 0x79559c | 0x7955b0 |
| `Singing_MuffinMan` | 74 | 0x7955c0 | 0x7955d6 |
| `Singing_MulberryBush` | 75 | 0x7955e6 | 0x7955fa |
| `Singing_SilveryMoon` | 76 | 0x79560a | 0x795620 |
| `Singing_TisketTasket` | 77 | 0x795630 | 0x795644 |
| `Singing_Toccata` | 78 | 0x795654 | 0x79566a |
| `Singing_VivaldiSpring` | 79 | 0x79567a | 0x79568e |
| `Singing_WaterMusic` | 80 | 0x79569e | 0x7956b4 |
| `Singing_WildAboutHarry` | 81 | 0x7956c4 | 0x7956d8 |
| `Singing_YellowRose` | 82 | 0x7956e8 | 0x7956fe |
| `FireTruckAlarm` | 83 | 0x79570e | 0x795722 |
| `CozmoPerforms` | 84 | 0x795732 | 0x795748 |
| `Count` | 85 | 0x795758 | 0x795762 |

The table has deliberate holes (4, 7, 8, 58, 60). Do not renumber it or repair shipped spellings. `Count=85` is itself in the string map. At 0x007957B4 the function searches the map; a hit loads the node's value at +0x14 (0x007957BC). Its miss path returns zero/Invalid after diagnostics, as in the prior check. Unity's enum decoration is not evidence for changing these exact name/value pairs into a bitmask parser.

## Failed JSON read: preserve parser side effects

There is no single universal failed-read root. DataPlatform does not clear it before parsing and does not replace it on false return. The scope overload forwards the same root. The stream Reader overload passes its accumulated text to the pointer-range overload; that overload clears **Reader internal state**, not the supplied Json::Value.

For a missing/unopenable file or an empty stream, no value token is parsed. The default readValue branch stores diagnostic offsets at root+0x10/+0x14, reports a syntax error, and returns false without changing payload/type. Thus a fresh null root remains null; a pre-existing value is not automatically erased. Comments/diagnostic metadata should not be confused with value equivalence.

For `{` followed by malformed content, readObject has already replaced the value with a new object at 0x008E0F32..0x008E0F40. Each member is resolved into that object and parsed in place. The error exit at 0x008E11C0 destroys the saved **old** payload temporary, not the partially parsed new root. For example, the static control flow predicts a missing final brace after a completed `defaultUnlocks` array leaves that array present in the object even though parse returns false. This example is a consequence of the instructions, not a claimed emulator test. Array parsing likewise operates in place (0x008E1274); no rollback should be invented.

InitConfig saves the failed boolean at 0x0064BBA4, logs UnlockConfigJsonNotFound, and continues at 0x0064BC06. If the global defaults collection is empty, it checks root nullness and `defaultUnlocks` being an array. Therefore false read does **not** alone prevent defaults processing. The supplied root's prior state matters. This settles the asked failure-root rule without claiming that every malformed JSON path is nonfatal; JsonCpp type errors or stack-limit throws remain distinct terminating paths under the standing M1-034 policy.

## Handler effects and the remaining dispatch boundary

C4 Turn checks byte +0xDB and compares ack ID to +0xDA, then sets +0xDC=1 at 0x0054D45C. Head uses +0xAA/+0xA9 and sets +0xAB at 0x0054D688. Lift uses +0x95/+0x94 and sets +0x96 at 0x0054D7AC. The other calls are payload access and diagnostics; the deletes in these bodies free local diagnostic vectors, not action owners or subscription handles. None calls Cancel, Init, reset, completion callbacks, or an event broadcast.

IDock DA maps a nonzero lift-load byte to +0xC2=1 and zero to +0xC2=2. There is no action-ID filter in this body. IDock C5 first rejects the no-trigger sentinel 0x23F, verifies that the trigger has a response, and compares movingLiftPostDock to byte +0x80. A match allocates a fresh TriggerLiftSafeAnimationAction and queues position 5 at 0x00557CFA. QueueAction's position-5 branch at 0x0053DB14 calls AddConcurrentAction, which finds an unused positive queue ID and appends the new action without executing Init. Its duplicate/clearing helper (0x0053DCE4) can PrepForCompletion and delete the **argument** while clearing; QueueAction's disabled/bad-tag path can also dispose of the argument. These are not evidence that an older C5 dock action is synchronously canceled. However, the rejection completion-report route has not been closed through every receiver here. This is the precise remaining boundary, rather than labeling all queue calls safe.

CA compares animation ID to +0x8B and sets +0x88=1 at 0x00545348. CB first requires +0x88, then matching ID. A zero loop count at +0x84 is infinite; otherwise it decrements the count and sets +0x89=1 at 0x005454EE only when the count reaches zero. It does not immediately destroy the action upon setting ended.

D5 requires started and matching payload tag. It copies timestamp and event into an external AnimationEvent and calls the external interface's +0x1C broadcast slot at 0x0054577A. The external tag is **0x60**, as the native generated setter 0x00725F52..0x00725F7C confirms. UiMessageHandler's synchronous E2G broadcast at 0x00662508 first delivers to the game via +0x48, then builds/emits a local AnkiEvent (0x00662552). The local emitter must be included in a lifetime claim. This report did not establish the exhaustive subscriber set for tag 0x60, including subscriptions registered through computed tag sets. A search for statically named SubscribeEngineToGame specializations is insufficient to prove none exist. **UNKNOWN: whether a reachable tag-0x60 recipient deletes another D5 subscriber/owner synchronously.** No such witness was established, and no categorical "cannot" is justified.

EF HandleFlashWriteAck searches tracked robot entries. Unknown robot IDs only warn. Status 0 or 0x0A records processed bytes at updater+0x50. Other status logs the signed status/sequence and calls GotoFailedState with result 4. GotoFailedState stores stage byte +0x58=2 at 0x00676DCE, then calls SendCompleteResultToGame. That function walks tracked robot IDs, looks each up, emits diagnostics, constructs FirmwareUpdateComplete, and broadcasts through Robot. Its external tag is **0x81** (setter 0x00727C4A..0x00727C7E). After the ack path, the selected tracked entry's awaiting-ack flag is cleared. No immediate clear of the updater subscription vector occurs in these bodies; the later Update/CleanupOnExit lifecycle from the checked report is a different call. **UNKNOWN: transitive lifetime effects of all synchronous tag-0x81 recipients.** Stage change is not proof that this broadcast is harmless. This is a recoverable native subscription/callee inventory question, not a hardware-only or external-system blocker.

## Init can recur, but not on every running update

IActionRunner::Update dispatches the action's UpdateInternal via vtable +0x10. For IAction-derived actions, that implementation tests initialized byte +0x70 at 0x00540DC4. When zero, after the applicable start delay and before timeout, it calls vtable +0x1C at 0x00540F34: the concrete Init override (for example TriggerAnimationAction::Init at 0x0054443C in its relocated vtable slot). A successful zero result sets +0x70=1 at 0x00540F98; the same call then proceeds to CheckIfDone through +0x20. Subsequent ordinary updates bypass Init while that byte remains set.

There are three distinctions relevant to a live object's subscriptions:

1. If Init returns RUNNING without making the initialized byte true, a later eligible update can invoke Init again.
2. A result in class 0x04000000 with retries remaining decrements byte +8, sets start time to -1, clears +0x70, sets state 0x02000001 (0x00540E04..0x00540E78), and returns RUNNING. The same object can initialize on a later update.
3. IAction::Reset at 0x00540CE8 clears +0x70 and time, optionally unlocks tracks, and sets 0x02000001. It also permits a later Init.

PlayAnimationAction::Init actually replaces its stored subscription shared handles after new subscriptions are acquired (0x00543F20..0x0054414A), releasing the old handles. Therefore re-Init is relevant to dispatch mutation, but its existence alone does not establish that one of the listed incoming-message callbacks synchronously causes it. The simple flag-only bodies above do not. The unresolved nested broadcasts must not be silently bridged to a deletion or safety conclusion.

## Registration, destructor identity, and no-robot packets

CozmoEngine's constructor builds the event-util tuple and calls 0x004EBB6C at 0x004EB5CC. That helper writes tag 0x4C, installs closure address point 0x0101E548 (table 0x0101E540), calls the external interface's subscribe slot +0x2C at 0x004EBB9E, and retains the returned shared handle in the supplied vector (0x004EBBA0..0x004EBBC2). The invoker at 0x004EE936 extracts ConnectToRobot from event+0x10 and tail-calls its engine handler through 0x004EE94A. This is constructor registration, not a registration performed by the handler or only after assets finish loading.

Robot construction allocates 0x38 bytes, calls BlockTapFilterComponent's constructor at 0x00510198, and stores it into Robot+0x450 at 0x005101A6. Destruction loads that field into r5 at 0x005111EA. It clears ObjectTapped list +0x2C, DoubleTapInfo tree +0x20, releases +0x14, destroys the scoped-handle vector at +4 via 0x0051120E/0x00511210, and deletes r5. The nearby writes to Robot+0x448/+0x450 are previous/current owner-slot clearing; they do not change r5's identity.

ProcessMessages, when enabled, updates the connection manager and repeatedly PopData. Each successful pop increments this+0x38 before GetFirstRobot. The null branch at 0x0069D8D8 returns to 0x0069D8BC, not to the function's exit and not to a deferred message queue. It never reads the tag, checks size, filters, unpacks, or broadcasts that popped packet. Thus these packets are discarded while no robot exists, including malformed/empty payloads that would otherwise receive validation. Processing continues with the next pop; the local vector is freed at the ordinary exit 0x0069DAEE. This is an engine queue statement, not a claim about transport-layer acknowledgements.

## Handoff

Item 1's requested map and failure-root behavior are recovered. Item 2's named direct bodies and the four ancillary questions are addressed, but its complete transitive lifetime claim remains **partially recovered**: C5 rejection completion recipients, D5 external tag 0x60 recipients, and EF tag 0x81 recipients need an exhaustive subscription-and-handler trace. No unsupported absence-of-reentrancy claim is made. Recommended manager action is to retain M2-022's recoverable gap with those precise boundaries; review the recovered M15-044 evidence before any inventory/status change. Validation here was native static reading plus all-81 Unity comparison, not production tests or hardware. Only this answer is to be committed for P3.
