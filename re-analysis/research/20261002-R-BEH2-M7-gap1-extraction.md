# R-BEH2 M7-behaviour gap extraction, pass 1

- **Date:** 2026-10-02
- **Scope:** items 1-5 of the R-BEH2 gap-1 request (M7-021, M7-018, M7-014, M7-015, M7-019). Read-only.
- **Source:** `resources/lib/armeabi-v7a/libcozmoEngine.so` (Thumb-2). Every address below was read from the `.so`
  with capstone (scripts in the session scratch directory, not in the repo). `re-analysis/decomp/` was not used.
- **How the scans were done:** the whole `.text` (0x4d6860..0x6e3682) was swept linearly. xrefs include direct
  `bl/blx/b` to the address and to its PLT stub. "Enclosing function" in a scan is the nearest preceding exported
  symbol; where the real function is a local (non-exported) one, the row says so.
- Floats are given as bit patterns. Enum names for AnimationTrigger / ReactionTrigger / OffTreadsState / NeedId /
  BehaviorObjective / SayTextIntent / MessageEngineToGameTag come from the generated `unity/scripts/csharp/**` enums
  (authority 4). The immediate values themselves are from the instructions. The engine's own `EnumToString` tables were
  not re-read for these names (listed under open questions).

Classification column uses the manifest statuses. The record column quotes an id only where an existing record covers
the step. I read the current M7-014/015/018/019/021 records from `re-analysis/fidelity_manifest.json` before writing.

---

## 1. M7-021: every reader of IBehavior+0x58

### 1.1 The helper and what it logs

| step | what the original does | citation | record | class |
|---|---|---|---|---|
| Helper entry | `IBehavior::<state-name setter>(this=r0, const std::string& newState=r1)`. A local (non-exported) function; the exporter labels its address as part of `BehaviorDriveOffCharger::TransitionToDrivingForward`. | `0x005C0CA8: push {r4,r5,r6,lr}`; `0x005C0CAC: mov r6,r0`; `0x005C0CB4: mov r4,r1` | M7-021 | EXACT_SOURCE |
| Behavior-name argument | Converts the 1-byte BehaviorID at `this+0x3C` with `EnumToString(BehaviorID)` (first vararg). | `0x005C0CB8: ldrb.w r0,[r6,#0x3c]`; `0x005C0CBC: blx 0x4a7e58` | M7-021 | EXACT_SOURCE |
| Old-state argument | Reads the std::string at `this+0x58` (libc++ SSO: flag byte, inline data from +0x59, heap pointer at +0x60) and passes its c_str as the second vararg. | `0x005C0CC2: ldrb r1,[r5,#0x58]!`; `0x005C0CC6: tst.w r1,#1`; `0x005C0CCC: addeq r1,r5,#1`; `0x005C0CCE: ldrne r1,[r6,#0x60]` | M7-021 | EXACT_SOURCE |
| New-state argument | c_str of the argument string (third vararg). | `0x005C0CD2: ldrb r3,[r4]`; `0x005C0CD4: ldr r2,[r4,#8]`; `0x005C0CDE: addeq r2,r4,#1`; `0x005C0CE0: stm.w sp,{r0,r1,r2}` | M7-021 | EXACT_SOURCE |
| The Info line | `Anki::Util::sChanneledInfoF(channel, event, kv, fmt, ...)`: channel `"Behaviors"` (0x00BEE2C1), event `"Behavior.TransitionToState"` (0x005C0D4C), an empty key/value vector (sp+0xC..0x14 zeroed at 0x005C0CB0..0x005C0CB6), format `"Behavior:%s, FromState:%s ToState:%s"` (0x005C0D68). Argument order: BehaviorID name, old state, new state. | `0x005C0CD6: add r6,pc ; "Behaviors"`; `0x005C0CE4: adr r1,#0x64 ; "Behavior.TransitionToState"`; `0x005C0CE8: adr r3,#0x7c ; "Behavior:%s, FromState:%s ToState:%s"`; `0x005C0CEC: blx 0x4a505c` | M7-021 | EXACT_SOURCE |
| The store | After the log and after freeing the kv buffer, it calls the libc++ `string::assign(const string&)` with `r0 = this+0x58`, `r1 = newState`. The log therefore shows the previous value as FromState. No branch on string content, no other write. | `0x005C0D12: mov r0,r5`; `0x005C0D14: mov r1,r4`; `0x005C0D16: bl 0x4e462a`; `0x005C0D1C: pop {r4,r5,r6,pc}` | M7-021 (record lists the log line and +0x58 store as not reproduced) | EXACT_SOURCE |
| Callers | 175 call sites reach the helper (161 `bl`/`blx`, 14 tail `b`). Per class: DriveOffCharger 3; DrivePath 1; FindFaces 2; InteractWithFaces 5; KnockOverCubes 3; LookAround 7; PickUpCube 3; PopAWheelie 2; RollBlock 3; SearchForFace 3; StackBlocks 4; DockingTestSimple 1; LiftLoadTest 1; FeedingEat 5; FeedingSearchForCube 5; DriveInDesperation 7; DriveToFace 5; PickUpAndPutDownCube 2; BuildPyramidBase 4; RespondPossiblyRoll 3; ExploreLookAroundInPlace 7; VisitInterestingEdge 3; RequestGameSimple 12; CantHandleTallStack 2; Bouncer 1; GuardDog 18; PeekABoo 9; PounceOnMotion 16; TrackLaser 13; EnrollFace 12 (+5 more in local bound-transition thunks in 0x005FCA7E..0x00600638, attributed by the exporter to TrackLaser/EnrollFace `__func` symbols); OnboardingShowCube 1; AcknowledgeCubeMoved 5; ReactToCliff 2. M7-owned callers and the state strings they pass: ReactToCliff `"PlayingStopReaction"` 0x00604F90, `"PlayingCliffReaction"` 0x00605110; AcknowledgeCubeMoved `"TurningToLastLocationOfBlock"` 0x00602298, `"PlayingSenseReaction"` 0x0060241C, `"ReactingToBlockPresence"` 0x0060258A, SetState_internal tail-call 0x00602616, `"ReactingToBlockAbsence"` 0x006026D8; DriveOffCharger `"WaitForOnTreads"` 0x005C0B74 (InitInternal) and 0x005C0DE0 (UpdateInternal), `"DrivingForward"` 0x005C0BE2. No other M7 behavior (ReactToPickup, Impact, OnCharger, ...) calls it. | xref scan of every `bl/blx/b` to 0x005C0CA8 and its PLT stub; M7-owned sites re-read | M7-021 lists only ReactToCliff and DriveOffCharger | EXACT_SOURCE for the set; NEW for AcknowledgeCubeMoved |
| Writers other than the helper | `BehaviorDockingTestSimple::UpdateStateName` 0x005CDB2C builds a state string (table at 0x01027F60 indexed by `this+0x11C`, suffixes `"FromFailure"`, `'*'` or `' '`) and calls the helper at 0x005CDBC6. `BehaviorLiftLoadTest::UpdateStateName` 0x005D4D88 does the same (helper call 0x005D4E08). `BehaviorFactoryTest::UpdateStateName` 0x005D34B0 builds a string from `EnumToString(FactoryTestState)` but its body (0x005D34B0..0x005D3500) has no helper call and no store to +0x58. | `0x005CDBC6: bl 0x5c0ca8`; `0x005D4E08`; `0x005D34B0..0x005D3500` | NEW | EXACT_SOURCE |

### 1.2 The complete set of +0x58 string readers

Method: every `ldrb[.w] rX,[rY,#0x58]{!}` (the libc++ SSO flag read that any content read compiles to) in the whole
binary; every `add/adds/addw/add.w rX,rY,#0x58` and `ldr/str ..,[rY,#0x58]` in 0x005B0000..0x00612000 (84 hits, each
read in context); and every `add rX,rY,#0x58` followed within 5 instructions by a std::string call over the whole
`.text`. The hits that are not the IBehavior string: BehaviorManager spark fields (+0x58 active spark id, +0x5C mode
byte, +0x60/+0x64 requested id/mode), ActivitySparked / IActivity / IHelper / AIWhiteboard own fields, inlined
`BlockWorldFilter` constructions (`add.w rX,rN,#0x40/#0x4C/#0x58` on a local filter: Dance, Singing, StackBlocks,
BuildPyramidBase, ExploreBringCubeToBeacon, RequestGameSimple, DockingTestSimple, CheckForStackAtInterval), vtable
slot 0x58 in `IBehavior::HandleEvent<>` 0x005BF1E2, literal-pool words that capstone decodes as instructions (0x005BC364,
0x005BC73C, 0x005FE7D4, 0x0060891C, 0x00609124), and `[[robot+0x44]+0x58]` reads (BehaviorManager spark) in
`IBehavior::Init/Resume` 0x005BCCAE/0x005BCD20/0x005BCFBA, `ReactToPickup::StartAnim` 0x00607852,
`PeekABoo::IsRunnableInternal` 0x005F6600.

| # | reader | what it does with the string | changes a condition / result / action / message? | citation | record | class |
|---|---|---|---|---|---|---|
| R1 | `IBehavior::~IBehavior` | flag byte only, to free heap storage | no | `0x005BC7C6: ldrb.w r0,[r4,#0x58]`; `0x005BC7CE: ldrne r0,[r4,#0x60]`; `0x005BC7D0: blx operator delete` | M7-021 (pre-extraction) | EXACT_SOURCE |
| R2 | the helper | Info log, then overwrite | no | `0x005C0CC2` | M7-021 | EXACT_SOURCE |
| R3 | `IBehavior::StartActing` (all overloads share 0x005BDACC) | third `%s` of a Warning: event `"IBehavior.StartActing.Failure.AlreadyActing"` (0x005BDC44), format `"Behavior '%s' can't start %s action because it is already running an action in state %s"` (0x00BF2878), args (this+0x40 name, runner+0x48 name, this+0x58 state). The branch that selects the warning is `this+0x84 != 0` (0x005BDAF0), not the string. The function then deletes the action and returns 0. | no (log only) | `0x005BDB20: ldrb r2,[r1,#0x58]!`; `0x005BDB30: add r2,pc ; "Behavior '%s' can't start..."`; `0x005BDB3A: blx sWarningF`; `0x005BDB3E: b 0x5bdbe2` | M7-021 | EXACT_SOURCE |
| R3b | StartActing 0x005BDBD0 | **not a +0x58 read.** It reads the action runner's name at `r4+0x48` for the `"IBehavior.StartActing.Failure.NotRunning"` warning (format `"Behavior '%s' can't start %s action because it is not running"`). The NotQueued warning (0x005BDB9E, event `"IBehavior.StartActing.Failure.NotQueued"`, format `"Behavior '%s' can't queue action '%s' (error %d)"`) uses +0x40 and +0x48 only. | no | `0x005BDBC8: ldrb r1,[r0,#0x48]!`; `0x005BDBD8: add r0,pc ; "IBehavior.StartActing.Failure.NotRunning"` | corrects the request wording | EXACT_SOURCE |
| R4 | `BehaviorDockingTestSimple::SetCurrState` | stores `this+0x11C`, calls UpdateStateName, then Info (channel `"Unnamed"`, event `"BehaviorDockingTest.SetState"`, format `"set state to '%s'"`) | no (enum at +0x11C set first) | `0x005CD87C: ldrb r1,[r0,#0x58]!`; `0x005CD88C`, `0x005CD890`, `0x005CD894` | M7-021 | EXACT_SOURCE |
| R5 | `BehaviorLiftLoadTest::SetCurrState` | same, event `"BehaviorLiftLoadTest.SetState"` | no | `0x005D4C94: ldrb r1,[r0,#0x58]!`; `0x005D4CA8` | M7-021 | EXACT_SOURCE |
| R6 | `BehaviorFactoryTest::PrintAndLightResult` | Warning `"BehaviorFactoryTest.EndTest.TestFAILED"`, format `"%s (code %d, state %s)"` | no | `0x005D1644: ldrb r1,[r0,#0x58]!`; `0x005D1656`, `0x005D165A` | M7-021 (pre-extraction cites 0x005D15D8..0x005D1612, a `strd` block at 0x005D155E; the +0x58 read is 0x005D1644) | EXACT_SOURCE |
| R7 | `BehaviorEnrollFace::HasTimedOut` (two reads) | Info, channel `"FaceRecognizer"`: event `"BehaviorEnrollFace.HasTimedOut.BehaviorTimedOut"`, format `"TimedOut after %.1fsec in State:%s"` (0x005FE176); event `"BehaviorEnrollFace.HasTimedOut.TooManyFacesTooLong"` (0x005FE1C8). The bool result is computed from floats at +0x138..+0x144 before the logs. | no | `0x005FE176`, `0x005FE1C8`; result `0x005FE22A: orrs r0,r5` | M7-021 | EXACT_SOURCE |
| R8 | `BehaviorEnrollFace::StopInternal` | Debug (`sChanneledDebugF`), channel `"FaceRecognizer"`, event `"BehaviorEnrollFace.StopInternal.FinalState"`, format `"Stopping EnrollFace in state %s"` | no | `0x005FE5AC: ldrb r1,[r0,#0x58]!`; `0x005FE5C8: blx sChanneledDebugF` | M7-021 | EXACT_SOURCE |
| **R9** | **`Robot::Update`** (not in the pre-extraction list, not in the record) | On each tick that runs `BehaviorManager::Update`, builds `robot+0x4C` (std::string) = current activity name + a space + `EnumToString(current behavior's BehaviorID)` and, if the state string's size is non-zero, appends a hyphen and the state string. Size is read from the flag byte (inline: flag shifted right by 1; heap: `[this+0x5C]`). | **yes, output only**: see R9a/R9b | `0x00513EE6: blx BehaviorManager::Update`; `0x00513EF2: blx GetCurrentActivity`; `0x00513F1C: bl 0x4e4652` (assign robot+0x4C); `0x00513F24: blx GetCurrentBehavior`; `0x00513F42: addw r1,pc,#0x740` (literal 0x00514684 = 0x20), `0x00513F4A: bl 0x4e8048` (append); `0x00513F4E: ldrb.w r0,[r7,#0x3c]`; `0x00513F52: EnumToString(BehaviorID)`; `0x00513F6A: ldrb r0,[r2,#0x58]!`; `0x00513F76: ldrne r0,[r7,#0x5c]`; `0x00513F78: cbz r0,0x513fae`; `0x00513F7C: addw r1,pc,#0x708` (literal 0x00514688 = 0x2D); `0x00513F82: blx operator+(char const*, string const&)`; `0x00513F9C: bl 0x4e8048` | NEW | EXACT_SOURCE for the string build |
| R9a | `Robot::Update`: viz text | `VizManager::SetText(TextLabelType = byte 5, ColorRGBA = global at 0x0103E964, format "%s", robot+0x4C c_str)`; the VizManager is `CozmoContext+0x24`. | viz only | `0x00513FBA..0x00513FE6` (`strb.w r1,[sp,#0xb8]` with r1=5; `0x00513FC6: addw r3,pc,#0x6c8` = "%s"; `0x00513FE6: blx VizManager::SetText`) | NEW | EXACT_SOURCE for the call; **RECOVERABLE_GAP** for whether it draws anything in the shipped app (read the SetText body behind PLT 0x4a79cc) |
| R9b | `Robot::Update`: SDK status | Builds activity name + a colon (literal 0x00514694 = 0x3A) + `robot+0x4C` and calls `CozmoContext::SetSdkStatus(SdkStatusType 0 = Behavior, string&&)`. SetSdkStatus calls virtual slot 0x38 of the object at `CozmoContext+4` (the `IExternalInterface*` ctor argument). For `UiMessageHandler` (vtable 0x0102FE14) slot 0x38 is 0x0066340E: `adds r0,#0x68; b 0x663886`, which moves the string into 12-byte slot `[type]` at `SdkStatus+0x24`, unconditionally. When it is sent (message `ExternalInterface::SdkStatus`, `kSdkStatusSendFreq` 0x00C851CC) was not read. | the string reaches an SDK-status field | `0x00513FEE..0x0051407A` (`0x0051404A: ldrd r2,r1,[r4,#0x50]`, `0x0051407A: blx SetSdkStatus`); `0x004EAD90: ldr r0,[r0,#4]`, `0x004EAD96: ldr r3,[r3,#0x38]`, `0x004EAD98: bx r3`; `0x0066340E..0x00663410`; `0x00663886..0x006638C4` | NEW | EXACT_SOURCE for the store; **RECOVERABLE_GAP** for the send path (read the SdkStatus periodic sender and `ExternalInterface::SdkStatus::Pack` 0x0071B417) |
| R9c | `Robot::Update`: the skip path | If the global int at 0x01051010 is positive it is decremented (`0x00513EC4: subs r0,#1`, `0x00513ECA: str r0,[r1]`), `robot+0x4C` is set to the 10-character literal `<disabled>` (0x00514674), `BehaviorManager::Update` is skipped for that tick, and SetText/SetSdkStatus run with activity name empty (0x00BE3F00). The writer of 0x01051010 was not read. | tick gate on the behavior system | `0x00513EB0..0x00513EDC`; `0x00513EDC: b 0x513fba` | NEW | EXACT_SOURCE here; writer: RECOVERABLE_GAP (find stores to 0x01051010) |

**Net result.** The string never selects a condition, result or action inside the behavior code. It reaches (a) log
lines (R2-R8) and (b) the `Robot::Update` debug string, which goes to the visualizer and an SDK-status string (R9).
The record's claim "a full +0x58 scan found no behaviour-changing reader" is right for behavior selection, but its scan
missed R9, which changes message content (SdkStatus string, viz text).

---

## 2. M7-018: config load, class extraction, container, factory

### 2.1 Production path, in order

| step | what the original does | citation | record | class |
|---|---|---|---|---|
| Who calls the loader | `RobotDataLoader::LoadNonConfigData` (0x0051F38C) runs, in this order: a main-thread VERIFY (`!_context->IsMainThread()`, 0x0051F39A..0x0051F3AA), CollectAnimFiles, LoadAnimationsInternal, LoadAnimationGroups, LoadCubeLightAnimations, LoadBackpackLightAnimations, LoadFaceAnimations, LoadEmotionEvents (0x0051F44C), **LoadBehaviors (0x0051F452)**, LoadActivities (0x0051F458), LoadReactionTriggerMap (0x0051F45E), LoadAnimationTriggerResponses, LoadCubeAnimationTriggerResponses, SayTextAction::LoadMetadata (0x0051F476). | `0x0051F452: blx 0x4a883c` (RobotDataLoader::LoadBehaviors) and neighbours | NEW | EXACT_SOURCE |
| Directory and files | `RobotDataLoader::LoadBehaviors` (0x005206BC) lazily builds the static string `config/engine/behaviorSystem/behaviors/` (0x27 chars, literal 0x005208BC), resolves it with `DataPlatform::pathToResource(Scope = 1, path)` (scope word `movs r7,#1; str r7,[sp,#0x10]` at 0x00520716/0x0052071C, DataPlatform = `this+4`), then `FileUtils::FilesInDirectory(path, true, ".json", true)` (r2=1, r3=".json" 0x00BE5869, stack arg 1: both bools true; recursion is the only reading I can give the second bool without the header). All `*.json` found are processed in the vector's order. | `0x005206EA: adr r1,#0x1d0 ; "config/engine/behaviorSystem/behaviors/"`; `0x0052071E: blx pathToResource`; `0x0052072E: blx FilesInDirectory` | NEW | EXACT_SOURCE |
| Shipped file set | `obb_filelist.txt` lists 178 `.json` under that directory (2 at top level: `wait.json`, `playArbitraryAnim.json`; `freeplay` 113, `reactions` 24, `voiceCommands` 18, `feeding` 11, `devBehaviors` 5, `meetCozmo` 4, `onboarding` 1). A regex check of the extracted copies under `re-analysis/obb/assets/cozmo_resources/config/engine/behaviorSystem/behaviors/` finds `"behaviorClass": "<string>"` and `"behaviorID": "<string>"` in all 178 (strict JSON parsers reject many of them, they carry comments or trailing commas, which jsoncpp accepts). | `re-analysis/obb_filelist.txt` lines matching `behaviorSystem/behaviors/`; extracted files above | M7-018 (Appendix C) | EXACT_SOURCE (asset) |
| Per-file load | For each path: `Json::Value(null)`, `DataPlatform::readAsJson(path, json)`. On failure: Warning, event `"RobotDataLoader.Behavior"`, format `"Failed to read '%s'"` (0x00BE7D67 region; 0x00520744/0x00520746), then continue. On success and non-empty: `id = IBehavior::ExtractBehaviorIDFromConfig(json, path)`, then `unordered_map<BehaviorID, Json::Value const>` at `RobotDataLoader+0x1C` insert-if-absent (`__construct_node` 0x00520788, `__node_insert_unique` 0x00520792). A duplicate ID keeps the first and destroys the new node, with no log. An empty JSON value is skipped silently. | `0x00520754: blx readAsJson`; `0x0052075E: blx Json::Value::empty`; `0x0052076A: blx ExtractBehaviorIDFromConfig`; `0x005207BE: blx sWarningF`; `0x005207E6..0x00520808` (duplicate node freed) | NEW | EXACT_SOURCE |
| BehaviorID extraction | `ExtractBehaviorIDFromConfig` (0x005BB9D4): `JsonTools::ParseString(config, "behaviorID", default "IBeh.NoBehaviorIdSpecified")` then `BehaviorIDFromString(string)`. | `0x005BB9DE: adr r1 ; "IBeh.NoBehaviorIdSpecified"`; `0x005BB9F0: add r2,pc ; "behaviorID"`; `0x005BB9F8: blx ParseString`; `0x005BB9FE: blx BehaviorIDFromString` | NEW | EXACT_SOURCE |
| Where containers are built | `Robot::Robot` builds **two** `BehaviorContainer`s: `BehaviorManager::BehaviorManager` (called 0x0050FCAC, 0x88 bytes, stored at `robot+0x44`) calls the container ctor at 0x005A08D6; `BehaviorSystemManager::BehaviorSystemManager` (called 0x0050FCBE, stored at `robot+0x48`) calls it at 0x005A5812. Both pass `[[robot]+0x1C]+0x1C` (CozmoContext RobotDataLoader's behavior-config map) as the config map. So every behavior object is constructed twice, once per manager. `Robot::Update` calls only `BehaviorManager::Update` (0x00513EE6). A scan of every `bl/blx/b` (direct and via PLT) finds **no caller** of `BehaviorSystemManager::Update` 0x005A5F44, `InitConfiguration` 0x005A58D4, `InitializeEventHandlers` 0x005A590E, `GetCurrentBehavior` 0x005A5910 or `FindBehaviorByID` 0x005A6024. | `0x0050FCAC`, `0x0050FCBE`; `0x005A08CA: ldr r0,[r6]`, `0x005A08CC: ldr r0,[r0,#0x1c]`, `0x005A08CE: add.w r2,r0,#0x1c`; `0x005A5806..0x005A580A` | NEW (M7-018 evidence names only CreateBehavior) | EXACT_SOURCE for the construction; the second manager's use is UNKNOWN (no direct callers; vtable-only use not ruled out) |
| Container load loop | `BehaviorContainer::BehaviorContainer(Robot&, const unordered_map<BehaviorID, Json const>&)` 0x0059C324: stores robot at `[this+0]`, zeroes the id-to-behavior map (`this+4`, sentinel `this+8`), then walks the config map's node list (`ldr r5,[r7,#8]` 0x0059C34C; next `ldr r5,[r5]` 0x0059C422). Per node (key byte at node+8, Json at node+0x10): (1) `Json::Value::empty()` true: Warning, event `"Robot.LoadBehavior"`, format `"Failed to read behavior file for behavior id '%s'"`, skip (0x0059C368..0x0059C386); (2) else `cls = ExtractBehaviorClassFromConfig(json)` (0x0059C3B6), `CreateBehavior(cls, robot, json)` (0x0059C3C6); (3) result pointer null: Error, event `"Robot.LoadBehavior.CreateFailed"`, format `"Failed to create a behavior for behavior id '%s'"`, then sets the global error flag byte and, if the debug-break flag byte is set, `sDebugBreakOnError` (0x0059C3DE..0x0059C416). Iteration order is the libc++ unordered_map node order, which depends on insertion order and the platform's directory order. | `0x0059C34C..0x0059C428` | M7-018 | EXACT_SOURCE |
| After the loop | If the config map is non-empty (`ldr r0,[r7,#0xc]` 0x0059C428) it calls `VerifyExecutableBehaviors()` (0x0059C42E). Then, if `Robot::HasExternalInterface()` (0x0059C434), it subscribes one handler on the external interface (virtual slot 0x2C of the interface, 0x0059C676) for `MessageGameToEngine` tag 0x9C = `RequestAllBehaviorsList` (`movs r3,#0x9c` 0x0059C64C; handler `HandleMessage<RequestAllBehaviorsList>` 0x0059EA9C, handle kept in the vector at `this+0x10`). | `0x0059C42E`, `0x0059C44C: bl 0x59c644` | NEW | EXACT_SOURCE |
| `ExtractBehaviorClassFromConfig` | `IBehavior::ExtractBehaviorClassFromConfig(const Json::Value&)` (0x005BBA74, static): `json["behaviorClass"]` (key literal 0x00BF2A86); if `isString()` is true (`cmp r0,#1`) the `asCString()`, otherwise the empty literal at 0x00BE3F00; builds a std::string and returns `BehaviorClassFromString(str)` (u8). | `0x005BBA78: blx Json::Value::operator[]`; `0x005BBA7E: blx isString`; `0x005BBA82: cmp r0,#1`; `0x005BBA88: blx asCString`; `0x005BBA90: ldr r4,[pc,#0x50]`, `0x005BBA92: add r4,pc ; ""`; `0x005BBAB0: blx BehaviorClassFromString` | M7-018 (pre-extraction row, checked here) | EXACT_SOURCE |
| `BehaviorClassFromString` miss path | `BehaviorClassFromString(const string&)` (0x0076A2F4) builds a function-local static `unordered_map<string, BehaviorClass>` of the 79 names below on first call (guard 0x0105CDCC), then `find` (0x0076AD5C). Hit: returns the byte at node+0x14. **Miss (unknown, empty, absent or non-string behaviorClass): writes `error: string '<s>' is not a valid BehaviorClass value` plus newline to `std::cerr` (GOT 0x0103FAD8 = `_ZNSt6__ndk14cerrE`), flushes, and returns 0.** Ordinal 0 is `Bouncer`, so such a config constructs a `BehaviorBouncer` in the original. All 178 shipped configs carry a string `behaviorClass`, so this path is not hit by shipped data. | `0x0076AD60: cmp r0,#0`; `0x0076AD64: ldrb r0,[r0,#0x14]`; `0x0076AD74: add r1,pc ; "error: string '"`; `0x0076AD90: adr r1 ; "' is not a valid BehaviorClass value"`; `0x0076ADD2: bl 0x4e44be` (flush); `0x0076ADD6: movs r0,#0` | NEW | EXACT_SOURCE |
| `CreateBehavior(BehaviorClass, Robot&, const Json&)` | 0x0059C888. `r1`=container, `r2`=class, `r3`=robot, stack arg `[sp+0x38]` = config. `cmp r5,#0x4e; bhi 0x59d404` (class above 0x4E: no object, falls to the error); else `tbh [pc, r5, lsl #1]` over 79 halfword entries at 0x0059C8A8. Every case: `operator new(size)`, `ctor(obj, robot, config)` (PlayAnim's ctor also gets `bool=true`; `Wait` constructs a plain `IBehavior` then overwrites the vptr with the BehaviorWait vtable via GOT 0x0103EDF0, 0x0059D09A..0x0059D0A4), wrap in `shared_ptr<IBehavior>`, `b 0x59d350`. Common tail: if the pointer is non-null, `AddToFactory(container, shared_ptr)`; the returned shared_ptr is stored to the out slot. Null or class above 0x4E: Error, event `"behaviorContainer.CreateBehavior.Failed"`, format `"Failed to create Behavior of type '%s'"` with `EnumToString(BehaviorClass)`, global error flag, optional `sDebugBreakOnError`, returns null. | `0x0059C89A: cmp r5,#0x4e`; `0x0059C8A4: tbh [pc,r5,lsl #1]`; `0x0059D350..0x0059D3FE`; table below | M7-018 | EXACT_SOURCE |
| `CreateBehavior(const Json&, Robot&)` | 0x0059C560: `ExtractBehaviorClassFromConfig(json)` then the 3-arg CreateBehavior. No `bl/blx/b` caller found in the whole binary. | `0x0059C56E`, `0x0059C57C` | NEW | EXACT_SOURCE (no live caller) |
| `AddToFactory` | `AddToFactory(shared_ptr<IBehavior> in r2, container r1, out r0)` 0x0059E90C: reads the ID byte `[behavior+0x3C]` (0x0059E91C), `emplace_unique(map at container+4, {id, shared_ptr})` (0x0059E936). If inserted: Info, channel `"Unnamed"` (0x0059E9D0), event `"behaviorContainer::AddToFactory"` (0x0059E9D8), format `"Added new behavior '%s' %p"` (0x0059E9F8) with `EnumToString(id)` and the raw pointer. If the ID was already present: nothing is logged or replaced (the existing map entry stays; the new object is still returned to the caller and stored in the out slot). Returns the shared_ptr moved. | `0x0059E91C: ldrb.w r6,[r1,#0x3c]`; `0x0059E936: blx emplace_unique`; `0x0059E946: cbz r7,0x59e98a`; `0x0059E964: blx sChanneledInfoF`; `0x0059E98A..0x0059E998` | M7-018 | EXACT_SOURCE |
| `VerifyExecutableBehaviors` | 0x0059C584. Walks the container's id-to-behavior map; for each behavior reads the executable-type byte `[behavior+0x6C]` (0x0059C5BC) and, if it is not `0x0C` (`ExecutableBehaviorType::Count`), inserts it into a **local** `std::map<ExecutableBehaviorType, BehaviorID>` and stores the container key (BehaviorID) as the mapped byte (0x0059C5E0..0x0059C5E6; a later behavior with the same executable type overwrites). The local map is destroyed at 0x0059C61E. The function has no log, no error call, no member write and no return value. It has no observable effect. | `0x0059C5BC: ldrb.w r0,[r7,#0x6c]`; `0x0059C5C4: cmp r0,#0xc`; `0x0059C5E0: blx emplace_unique`; `0x0059C5E6: strb r7,[r0,#0xe]`; `0x0059C61E` | NEW | EXACT_SOURCE |
| `FindBehaviorByExecutableType` | 0x0059C836: linear walk of the same map comparing `[behavior+0x6C]` with the argument, returns the first match in map (BehaviorID) order. This is the function the chooser (M8-013) uses. | `0x0059C844: ldrb.w r1,[lr,#0x6c]`; `0x0059C848: cmp r1,r2` | NEW | EXACT_SOURCE |
| Common IBehavior ctor facts | `IBehavior::IBehavior` (0x005BBB74) itself calls `ExtractBehaviorIDFromConfig` (0x005BBBA4, stores at `+0x3C`) and `ExtractBehaviorClassFromConfig` (0x005BBC92, stores at `+0x64`) per the pre-extraction. Not re-read here. | 0x005BBBA4, 0x005BBC92 | M7-018 | EXACT_SOURCE per pre-extraction, not re-read |

### 2.2 The 79-way table (CreateBehavior)

Ordinal = position in the name list built by `BehaviorClassFromString` (0x0076A2F4; the ordinals are the 1-byte values
stored with each key, read as 0,1,2,...,78 in order). Cross-check: the manifest's "class 0x39 BehaviorWait" and the
pre-extraction's FistBump 0x19, PlayAnim 0x26, ReactToSparked 0x4C all agree. `new` is the `operator new` size passed
at the start of the case; `ctor` is the real function address (the PLT stub the case calls is given last).

```
0x00 Bouncer                    case@0x59c946 new=0x158 BehaviorBouncer ctor=0x5f12fc plt=0x4af9f4
0x01 BringCubeToBeacon          case@0x59c968 new=0x138 BehaviorExploreBringCubeToBeacon ctor=0x5defb0 plt=0x4afa0c
0x02 BuildPyramid               case@0x59c98a new=0x168 BehaviorBuildPyramid ctor=0x5dbc88 plt=0x4afa24
0x03 BuildPyramidBase           case@0x59c9ac new=0x168 BehaviorBuildPyramidBase ctor=0x5dca40 plt=0x4afa3c
0x04 CantHandleTallStack        case@0x59c9ce new=0x148 BehaviorCantHandleTallStack ctor=0x5ecb10 plt=0x4afa54
0x05 CheckForStackAtInterval    case@0x59c9f0 new=0x148 BehaviorCheckForStackAtInterval ctor=0x5d71f8 plt=0x4afa6c
0x06 CubeLiftWorkout            case@0x59ca12 new=0x130 BehaviorCubeLiftWorkout ctor=0x5d7e50 plt=0x4afa84
0x07 Dance                      case@0x59ca34 new=0x148 BehaviorDance ctor=0x5ed438 plt=0x4afa9c
0x08 DevTurnInPlaceTest         case@0x59ca56 new=0x138 BehaviorDevTurnInPlaceTest ctor=0x5ca550 plt=0x4afab4
0x09 DockingTestSimple          case@0x59ca78 new=0x290 BehaviorDockingTestSimple ctor=0x5cafb0 plt=0x4afacc
0x0A DriveInDesperation         case@0x59ca9a new=0x140 BehaviorDriveInDesperation ctor=0x5d8ae0 plt=0x4afae4
0x0B DriveOffCharger            case@0x59cabc new=0x128 BehaviorDriveOffCharger ctor=0x5c0980 plt=0x4afafc
0x0C DrivePath                  case@0x59cade new=0x218 BehaviorDrivePath ctor=0x5c0f38 plt=0x4afb14
0x0D DriveToFace                case@0x59cb00 new=0x128 BehaviorDriveToFace ctor=0x5da550 plt=0x4afb2c
0x0E EarnedSparks               case@0x59cb22 new=0x120 BehaviorEarnedSparks ctor=0x5dae98 plt=0x4afb44
0x0F EnrollFace                 case@0x59cb44 new=0x180 BehaviorEnrollFace ctor=0x5fcad4 plt=0x4afb5c
0x10 ExploreLookAroundInPlace   case@0x59cb64 new=0x1f8 BehaviorExploreLookAroundInPlace ctor=0x5e1d80 plt=0x4afb74
0x11 ExploreVisitPossibleMarker case@0x59cb84 new=0x128 BehaviorExploreVisitPossibleMarker ctor=0x5e3cd8 plt=0x4afb8c
0x12 ExpressNeeds               case@0x59cba4 new=0x140 BehaviorExpressNeeds ctor=0x5ee144 plt=0x4afba4
0x13 FactoryCentroidExtractor   case@0x59cbc4 new=0x210 BehaviorFactoryCentroidExtractor ctor=0x5cf80c plt=0x4afbbc
0x14 FactoryTest                case@0x59cbe4 new=0x370 BehaviorFactoryTest ctor=0x5d0438 plt=0x4afbd4
0x15 FeedingEat                 case@0x59cc04 new=0x150 BehaviorFeedingEat ctor=0x5d5794 plt=0x4afbec
0x16 FeedingSearchForCube       case@0x59cc24 new=0x128 BehaviorFeedingSearchForCube ctor=0x5d6c64 plt=0x4afc04
0x17 FindFaces                  case@0x59cc44 new=0x200 BehaviorFindFaces ctor=0x5c1914 plt=0x4afc1c
0x18 FireTruckAlarm             case@0x59cc64 new=0x120 BehaviorFireTruckAlarm ctor=0x5daf88 plt=0x4afc34
0x19 FistBump                   case@0x59cc84 new=0x158 BehaviorFistBump ctor=0x5f1d8c plt=0x4afc4c
0x1A GuardDog                   case@0x59cca4 new=0x158 BehaviorGuardDog ctor=0x5f28dc plt=0x4afc64
0x1B InteractWithFaces          case@0x59ccc4 new=0x140 BehaviorInteractWithFaces ctor=0x5c1ee8 plt=0x4afc7c
0x1C KnockOverCubes             case@0x59cce4 new=0x168 BehaviorKnockOverCubes ctor=0x5c2ea0 plt=0x4afc94
0x1D LiftLoadTest               case@0x59cd04 new=0x228 BehaviorLiftLoadTest ctor=0x5d3f10 plt=0x4afcac
0x1E LookAround                 case@0x59cd24 new=0x168 BehaviorLookAround ctor=0x5c3e50 plt=0x4afcc4
0x1F LookForFaceAndCube         case@0x59cd44 new=0x1a0 BehaviorLookForFaceAndCube ctor=0x5ef804 plt=0x4afcdc
0x20 LookInPlaceMemoryMap       case@0x59cd64 new=0x178 BehaviorLookInPlaceMemoryMap ctor=0x5e482c plt=0x4afcf4
0x21 OnConfigSeen               case@0x59cd84 new=0x140 BehaviorOnConfigSeen ctor=0x5db0e8 plt=0x4afd0c
0x22 OnboardingShowCube         case@0x59cda4 new=0x130 BehaviorOnboardingShowCube ctor=0x600ac0 plt=0x4afd24
0x23 PeekABoo                   case@0x59cdc4 new=0x170 BehaviorPeekABoo ctor=0x5f62d0 plt=0x4afd3c
0x24 PickUpAndPutDownCube       case@0x59cde4 new=0x128 BehaviorPickUpAndPutDownCube ctor=0x5db65c plt=0x4afd54
0x25 PickUpCube                 case@0x59ce04 new=0x130 BehaviorPickUpCube ctor=0x5c64d4 plt=0x4afd6c
0x26 PlayAnim                   case@0x59ce24 new=0x140 BehaviorPlayAnimSequence ctor=0x5bff24 plt=0x4afd84
0x27 PlayAnimOnNeedsChange      case@0x59ce46 new=0x140 BehaviorPlayAnimOnNeedsChange ctor=0x5bfdd4 plt=0x4afd9c
0x28 PlayAnimWithFace           case@0x59ce66 new=0x140 BehaviorPlayAnimSequenceWithFace ctor=0x5c0630 plt=0x4afdb4
0x29 PlayArbitraryAnim          case@0x59ce86 new=0x140 BehaviorPlayArbitraryAnim ctor=0x5c0784 plt=0x4afdcc
0x2A PopAWheelie                case@0x59cea6 new=0x138 BehaviorPopAWheelie ctor=0x5c7430 plt=0x4afde4
0x2B PounceOnMotion             case@0x59cec6 new=0x190 BehaviorPounceOnMotion ctor=0x5f7f90 plt=0x4afdfc
0x2C PutDownBlock               case@0x59cee6 new=0x120 BehaviorPutDownBlock ctor=0x5c7f98 plt=0x4afe14
0x2D PyramidThankYou            case@0x59cf06 new=0x128 BehaviorPyramidThankYou ctor=0x5de048 plt=0x4afe2c
0x2E RequestGameSimple          case@0x59cf26 new=0x228 BehaviorRequestGameSimple ctor=0x5ea168 plt=0x4afe44
0x2F RespondPossiblyRoll        case@0x59cf46 new=0x150 BehaviorRespondPossiblyRoll ctor=0x5de324 plt=0x4afe5c
0x30 RespondToRenameFace        case@0x59cf66 new=0x130 BehaviorRespondToRenameFace ctor=0x600694 plt=0x4afe74
0x31 RollBlock                  case@0x59cf86 new=0x138 BehaviorRollBlock ctor=0x5c8598 plt=0x4afe8c
0x32 SearchForFace              case@0x59cfa6 new=0x120 BehaviorSearchForFace ctor=0x5c9158 plt=0x4afea4
0x33 Singing                    case@0x59cfc6 new=0x148 BehaviorSinging ctor=0x5ee8dc plt=0x4afebc
0x34 StackBlocks                case@0x59cfe6 new=0x138 BehaviorStackBlocks ctor=0x5c93d0 plt=0x4afed4
0x35 ThinkAboutBeacons          case@0x59d006 new=0x130 BehaviorThinkAboutBeacons ctor=0x5e5af8 plt=0x4afeec
0x36 TrackLaser                 case@0x59d026 new=0x1d8 BehaviorTrackLaser ctor=0x5fa250 plt=0x4aff04
0x37 TurnToFace                 case@0x59d046 new=0x120 BehaviorTurnToFace ctor=0x5ca33c plt=0x4aff1c
0x38 VisitInterestingEdge       case@0x59d066 new=0x198 BehaviorVisitInterestingEdge ctor=0x5e5f64 plt=0x4aff34
0x39 Wait                       case@0x59d086 new=0x120 IBehavior ctor=0x5bbb74 plt=0x4aff4c
0x3A AcknowledgeFace            case@0x59d0b2 new=0x140 BehaviorAcknowledgeFace ctor=0x602884 plt=0x4aff64
0x3B AcknowledgeObject          case@0x59d0d2 new=0x168 BehaviorAcknowledgeObject ctor=0x602fa4 plt=0x4aff7c
0x3C RamIntoBlock               case@0x59d0f2 new=0x120 BehaviorRamIntoBlock ctor=0x60473c plt=0x4aff94
0x3D ReactToCliff               case@0x59d112 new=0x128 BehaviorReactToCliff ctor=0x604cd0 plt=0x4affac
0x3E ReactToCubeMoved           case@0x59d132 new=0x130 BehaviorAcknowledgeCubeMoved ctor=0x60219c plt=0x4affc4
0x3F ReactToFrustration         case@0x59d152 new=0x148 BehaviorReactToFrustration ctor=0x605904 plt=0x4affdc
0x40 ReactToImpact              case@0x59d172 new=0x120 BehaviorReactToImpact ctor=0x60617c plt=0x4afff4
0x41 ReactToMotorCalibration    case@0x59d192 new=0x120 BehaviorReactToMotorCalibration ctor=0x60658c plt=0x4b000c
0x42 ReactToOnCharger           case@0x59d1b2 new=0x130 BehaviorReactToOnCharger ctor=0x606a18 plt=0x4b0024
0x43 ReactToPet                 case@0x59d1d2 new=0x140 BehaviorReactToPet ctor=0x606e98 plt=0x4b003c
0x44 ReactToPickup              case@0x59d1f2 new=0x128 BehaviorReactToPickup ctor=0x607724 plt=0x4b0054
0x45 ReactToPlacedOnSlope       case@0x59d212 new=0x128 BehaviorReactToPlacedOnSlope ctor=0x607fb8 plt=0x4b006c
0x46 ReactToPyramid             case@0x59d232 new=0x120 BehaviorReactToPyramid ctor=0x608310 plt=0x4b0084
0x47 ReactToReturnedToTreads    case@0x59d252 new=0x120 BehaviorReactToReturnedToTreads ctor=0x6084e4 plt=0x4b009c
0x48 ReactToRobotOnBack         case@0x59d272 new=0x120 BehaviorReactToRobotOnBack ctor=0x6087b8 plt=0x4b00b4
0x49 ReactToRobotOnFace         case@0x59d292 new=0x120 BehaviorReactToRobotOnFace ctor=0x608a9c plt=0x4b00cc
0x4A ReactToRobotOnSide         case@0x59d2b2 new=0x120 BehaviorReactToRobotOnSide ctor=0x608d38 plt=0x4b00e4
0x4B ReactToRobotShaken         case@0x59d2d2 new=0x130 BehaviorReactToRobotShaken ctor=0x609104 plt=0x4b00fc
0x4C ReactToSparked             case@0x59d2f2 new=0x120 BehaviorReactToSparked ctor=0x6097e0 plt=0x4b0114
0x4D ReactToStackOfCubes        case@0x59d312 new=0x120 BehaviorReactToStackOfCubes ctor=0x609880 plt=0x4b012c
0x4E ReactToUnexpectedMovement  case@0x59d332 new=0x120 BehaviorReactToUnexpectedMovement ctor=0x609a58 plt=0x4b0144
```

Notes on the table: `Wait` (0x39) is the only case whose constructor is the base `IBehavior::IBehavior` (0x005BBB74);
`PlayAnim` is the only case that passes an extra `bool` (true); `ReactToCubeMoved` (0x3E) constructs
`BehaviorAcknowledgeCubeMoved`; `BringCubeToBeacon` (0x01) constructs `BehaviorExploreBringCubeToBeacon`;
`PlayAnimWithFace` (0x28) constructs `BehaviorPlayAnimSequenceWithFace`. There is no `BehaviorHiccup` case; `FistBump` is case 0x19 (size 0x158).

---

## 3. M7-014: every call site of the reaction-lock APIs

### 3.1 Mechanism (re-verified against the pre-extraction)

| step | what the original does | citation | record | class |
|---|---|---|---|---|
| `IBehavior::SmartDisableReactionsWithLock(const string& name, const Array<ReactionTrigger,bool,21>& table)` | Builds `name + "_behaviorLock"` (literal 0x00BF2B20), calls `BehaviorManager::DisableReactionsWithLock(lock, table, true)` on `[[this+0x2C]+0x44]` (the robot's BehaviorManager), then inserts the un-suffixed `name` into the std::set at `this+0xA4`. | `0x005BCE4C: ldr r0,[r5,#0x2c]`; `0x005BCE52: ldr r7,[r0,#0x44]`; `0x005BCE56: blx operator+`; `0x005BCE62: blx DisableReactionsWithLock` (r3=1); `0x005BCE74: add.w r1,r5,#0xa4`; `0x005BCE7E: blx set::insert` | M7-014 | EXACT_SOURCE |
| `IBehavior::SmartRemoveDisableReactionsLock(name)` | Rebuilds `name + "_behaviorLock"`, calls `BehaviorManager::RemoveDisableReactionsLock`, then erases `name` from the set at `this+0xA4`. | `0x005BD470..0x005BD4AA` | M7-014 | EXACT_SOURCE |
| Automatic removal | `IBehavior::Stop` (0x005BD08C) logs Info (channel `"Behaviors"`, event = behavior name + `".Stop"` (literal 0x005BD204), format `"Stopping..."`), then, if `[this+0xC8]` is non-null and `[[this+0xC8]+4] != -1`, calls `IBehavior::StopHelperWithoutCallback()` (0x005BD0F4..0x005BD102); clears `this+0xA1`, calls the virtual at vptr+0x54 (StopInternal), stores the stop time at `this+0x30`, calls `StopActing(false,false)`, then loops `while (set size at this+0xAC != 0) SmartRemoveDisableReactionsLock(*begin)` using the set at `this+0xA4`, then `SmartRemoveIdleAnimation` if `this+0xB0`. So every lock added through `IBehavior::SmartDisable...` is released at Stop, whichever call site added it. | `0x005BD0A0..0x005BD0C0`; `0x005BD106..0x005BD126`; `0x005BD12C..0x005BD140` (remove loop); `0x005BD142..0x005BD14C` | M7-014 | EXACT_SOURCE |
| Table form | Each table is 21 consecutive `{ReactionTrigger byte = ordinal, bool byte}` pairs (42 bytes). I verified that for every table below the first byte of each pair equals its index. The mask column is the 21 bool bytes in trigger order: CliffDetected, CubeMoved, FacePositionUpdated, FistBump, Frustration, Hiccup, MotorCalibration, NoPreDockPoses, ObjectPositionUpdated, PlacedOnCharger, PetInitialDetection, RobotFalling, RobotPickedUp, RobotPlacedOnSlope, ReturnedToTreads, RobotOnBack, RobotOnFace, RobotOnSide, RobotShaken, Sparked, UnexpectedMovement. `1` = that trigger is disabled by this lock. | data at the table addresses; `unity/scripts/csharp/Anki.Cozmo/ReactionTrigger.cs` for the names | M7-014 | EXACT_SOURCE |

### 3.2 Which tables the pre-extraction already gave, and which classes remain

The pre-extraction gave five tables: ReactToImpact 0x00C73D36, DriveOffCharger 0x00C672F0, Singing 0x00C6F590,
AcknowledgeCubeMoved 0x00C72BB2, ReactToOnCharger 0x00C74182. Re-read here: addresses, install points and contents
all agree with the rows below. The record M7-014 names nine classes whose table is "among the 13 recovered"
(CubeLiftWorkout, PeekABoo, PutDownBlock, FistBump, Bouncer, GuardDog, Dance, EnrollFace, OnboardingShowCube); their tables
are also in the list below, as are the four wired M7 reactions (ReactToCliff 0x00C73746, PlacedOnSlope 0x00C745E0, Shaken
0x00C750E0, MotorCalibration 0x00C74032). **Not in the record at all and not in the pre-extraction:** IBehavior::Init and
Resume ("SparkBehaviorDisables", table 0x00C65F90), DriveOffCharger is given, KnockOverCubes (two sites), PopAWheelie,
DevTurnInPlaceTest (all-triggers table), FeedingEat, BuildPyramid, RequestGameSimple (two sites, one table), RamIntoBlock,
the five developer/test behaviors using non-smart locks, and the activity/action/component callers. The table of the
`BehaviorManager::DisableReactionsWithLock` callers outside behaviors is included for completeness.

I did not have the contents of "Appendix F" (the 13 recovered tables) to compare against, so I cannot say which of
the rows below are the 13; compare by address.

### 3.3 All call sites

`mask` is in the trigger order given above. "name" for IBehavior sites is the behavior's own name at `this+0x40`
(`add.w r1,rX,#0x40` before the call) unless the install-point note says otherwise.

| API | caller | call site | table | mask by trigger ordinal 0..20 |
|---|---|---|---|---|
| IBehavior::SmartDisable | IBehavior::Init | 0x005bcd44 | 0x00c65f90 | 000000001000000000000 |
| IBehavior::SmartDisable | IBehavior::Resume | 0x005bcfde | 0x00c65f90 | 000000001000000000000 |
| IBehavior::SmartDisable | BehaviorDriveOffCharger::InitInternal | 0x005c0b2a | 0x00c672f0 | 111001001100000000001 |
| IBehavior::SmartDisable | BehaviorKnockOverCubes::InitializeMemberVars | 0x005c31fa | 0x00c67cb2 | 010001001000000000000 |
| IBehavior::SmartDisable | BehaviorKnockOverCubes::PrepareForKnockOverAttempt | 0x005c37f0 | 0x00c67cdc | 000000000000000000000 |
| IBehavior::SmartDisable | BehaviorPopAWheelie::SetupRetryAction | 0x005c7be8 | 0x00c6883d | 110100000001101100001 |
| IBehavior::SmartDisable | BehaviorPutDownBlock::InitInternal | 0x005c7fe2 | 0x00c68b20 | 000000000000000000000 |
| IBehavior::SmartDisable | BehaviorDevTurnInPlaceTest::InitInternal | 0x005ca924 | UNRESOLVED (mov r2, r0 @5ca920) | |
| IBehavior::SmartDisable | BehaviorFeedingEat::TransitionToEating | 0x005d6144 | 0x00c6ad18 | 100000000001111000000 |
| IBehavior::SmartDisable | BehaviorCubeLiftWorkout::InitInternal | 0x005d7ec8 | 0x00c6b7e0 | 011000001010000000001 |
| IBehavior::SmartDisable | BehaviorBuildPyramid::TransitionToPlacingTopBlock | 0x005dc0d6 | 0x00c6c691 | 000000001000000000000 |
| IBehavior::SmartDisable | BehaviorRequestGameSimple::RequestGame_InitInternal | 0x005ea6e2 | 0x00c6e820 | 011100001010000000000 |
| IBehavior::SmartDisable | BehaviorRequestGameSimple::TransitionToPlayingRequstAnim | 0x005ebf26 | 0x00c6e820 | 011100001010000000000 |
| IBehavior::SmartDisable | BehaviorDance::InitInternal | 0x005ed47c | 0x00c6f1c8 | 011100001010000000001 |
| IBehavior::SmartDisable | BehaviorSinging::InitInternal | 0x005eeb5e | 0x00c6f590 | 011000001010000000001 |
| IBehavior::SmartDisable | BehaviorBouncer::InitInternal | 0x005f14b8 | 0x00c6fb90 | 011111001010000000001 |
| IBehavior::SmartDisable | BehaviorFistBump::InitInternal | 0x005f1ee6 | 0x00c6fdc8 | 011000001011101000001 |
| IBehavior::SmartDisable | BehaviorGuardDog::InitInternal | 0x005f2d0e | 0x00c6ff06 | 011111001010000000001 |
| IBehavior::SmartDisable | BehaviorPeekABoo::InitInternal | 0x005f67a8 | 0x00c70960 | 011100001010000000000 |
| IBehavior::SmartDisable | BehaviorEnrollFace::InitInternal | 0x005fd10a | 0x00c71b6c | 111111001001101111001 |
| IBehavior::SmartDisable | BehaviorOnboardingShowCube::InitInternal | 0x00600ca8 | 0x00c72454 | 011011001010000000000 |
| IBehavior::SmartDisable | BehaviorAcknowledgeCubeMoved::InitInternal | 0x00602242 | 0x00c72bb2 | 000000001000000000000 |
| IBehavior::SmartDisable | BehaviorRamIntoBlock::TransitionToRammingIntoBlock | 0x00604a44 | 0x00c73520 | 101000001000000000001 |
| IBehavior::SmartDisable | BehaviorReactToCliff::InitInternal | 0x00604da0 | 0x00c73746 | 011000001000000000001 |
| IBehavior::SmartDisable | BehaviorReactToImpact::InitInternal | 0x00606216 | 0x00c73d36 | 111111111011111111011 |
| IBehavior::SmartDisable | BehaviorReactToMotorCalibration::InitInternal | 0x00606642 | 0x00c74032 | 100001000000101111000 |
| IBehavior::SmartDisable | BehaviorReactToOnCharger::InitInternal | 0x00606cb0 | 0x00c74182 | 011111011010000000011 |
| IBehavior::SmartDisable | BehaviorReactToPlacedOnSlope::InitInternal | 0x00607ffe | 0x00c745e0 | 100000000000101000000 |
| IBehavior::SmartDisable | BehaviorReactToRobotShaken::InitInternal | 0x00609144 | 0x00c750e0 | 111111001011111111001 |
| IActivity::SmartDisable | ActivityFeeding::OnSelectedInternal | 0x005aae1e | 0x00c61da0 | 011111001010000000010 |
| IActivity::SmartDisable | ActivityFeeding::SetupSevereAnims | 0x005ab482 | 0x00c61dd4 | 011111011011101111010 |
| IActivity::SmartDisable | ActivitySparked::OnSelectedInternal | 0x005b16a8 | 0x00c624bc | 011111000010000000000 |
| IActivity::SmartDisable | ActivitySparked::CheckIfSparkShouldEnd | 0x005b199c | 0x00c624e6 | 011010001000000000000 |
| BehaviorManager::DisableReactionsWithLock | IDockAction::Init | 0x0055189a | UNRESOLVED (ldr.w r2, [r5, #0xc4] @551890) | |
| BehaviorManager::DisableReactionsWithLock | IDockAction::Init | 0x00551c4c | 0x00c55a21 | 000000001000000000000 |
| BehaviorManager::DisableReactionsWithLock | PlaceObjectOnGroundAction::Init | 0x005548c6 | 0x00c55a21 | 000000001000000000000 |
| BehaviorManager::DisableReactionsWithLock | FlipBlockAction::Init | 0x0055eec6 | 0x00c56ae0 | 010000000000000000001 |
| BehaviorManager::DisableReactionsWithLock | SevereNeedsComponent::SetSevereNeedExpression | 0x005731a0 | UNRESOLVED (ldr r2, [r0, #0x14] @573198) | |
| BehaviorManager::DisableReactionsWithLock | BehaviorManager::SwitchToUIGameRequestBehavior | 0x005a2756 | 0x00c60d6a | 011110011010000000011 |
| BehaviorManager::DisableReactionsWithLock | std::__ndk1::__tree<std::__ndk1::__value_type<HighLevelActivity, std::__ndk1::shared_ptr<IActivity> >, std::__ndk1::__map_value_compare<HighLevelActivity, std::__ndk1::__value_type<HighLevelActivity, std::__ndk1::shared_ptr<IActivity> >, std::__ndk1::less<HighLevelActivity>, true>, std::__ndk1::allocator<std::__ndk1::__value_type<HighLevelActivity, std::__ndk1::shared_ptr<IActivity> > > >::__insert_node_at | 0x005a4e3e | UNRESOLVED (add r2, sp, #4 @5a4e2e) | |
| BehaviorManager::DisableReactionsWithLock | ActivityBuildPyramid::OnSelectedInternal | 0x005a7a00 | 0x00c61790 | 000100000000000000000 |
| BehaviorManager::DisableReactionsWithLock | ActivityBuildPyramid::UpdateActiveBehaviorGroup | 0x005a82c4 | 0x00c617ba | 000000001000000000000 |
| BehaviorManager::DisableReactionsWithLock | BehaviorDockingTestSimple::InitInternal | 0x005cb434 | UNRESOLVED (mov r2, r0 @5cb42c) | |
| BehaviorManager::DisableReactionsWithLock | BehaviorFactoryCentroidExtractor::InitInternal | 0x005cfaa4 | UNRESOLVED (mov r2, r0 @5cfa9c) | |
| BehaviorManager::DisableReactionsWithLock | BehaviorFactoryTest::InitInternal | 0x005d1090 | UNRESOLVED (mov r2, r0 @5d1088) | |
| BehaviorManager::DisableReactionsWithLock | BehaviorLiftLoadTest::InitInternal | 0x005d413a | UNRESOLVED (mov r2, r0 @5d4132) | |

Install points and matching removals, by group (all in the table above):

| step | what the original does | citation | record | class |
|---|---|---|---|---|
| Install at InitInternal | Most behaviors install their lock as one of the first statements of `InitInternal` (`add.w r1,this,#0x40` = own name). Removal is the automatic loop in `IBehavior::Stop` (virtual at vptr+0x54 = StopInternal per the ReactToCliff vtable relocation 0x0102653C+, resolved from `vtable(BehaviorReactToCliff)`; the loop is at 0x005BD12C..0x005BD140). | table rows; `0x005BD106: ldr r0,[r4]`, `0x005BD110: ldr r2,[r0,#0x54]`, `0x005BD114: blx r2` | M7-014 | EXACT_SOURCE |
| IBehavior::Init / Resume lock | `IBehavior::Init` and `IBehavior::Resume(ReactionTrigger)`: if `this+0x70` (spark id, 0x55 = none) is not 0x55 and equals `[[this+0x2C]+0x44]+0x58` (the BehaviorManager's active spark), install lock name `"SparkBehaviorDisables"` (literal 0x00BF2B0A) with table 0x00C65F90 (only ObjectPositionUpdated). Init: `0x005BCD16..0x005BCD44`. Resume: `0x005BCFAC..0x005BCFDE`, reached only when `ResumeInternal` (vptr+0x4C) returned 0; the same two tests on `this+0x70` and the active spark (`0x005BCFAE: cmp r1,#0x55`, `0x005BCFBE: cmp r1,r2`). Removed by `IBehavior::Stop`. | `0x005BCD16: ldr r0,[r4,#0x70]`; `0x005BCD18: cmp r0,#0x55`; `0x005BCD20: ldr r1,[r1,#0x58]`; `0x005BCD22: cmp r0,r1`; `0x005BCD2E: add r1,pc ; "SparkBehaviorDisables"`; `0x005BCD44: blx SmartDisable` | NEW | EXACT_SOURCE |
| Explicit remove, KnockOverCubes | `BehaviorKnockOverCubes::PrepareForKnockOverAttempt` first removes the lock named `"preparingToKnockOverDisable"` (literal 0x005C3820) with `SmartRemoveDisableReactionsLock` (0x005C37C2), then installs the same name with the all-zero table 0x00C67CDC (0x005C37F0). `InitializeMemberVars` installs table 0x00C67CB2 under the behavior's own name (0x005C31FA). | `0x005C37AE..0x005C37F0`; `0x005C31FA` | NEW | EXACT_SOURCE |
| Explicit remove, PopAWheelie | `SetupRetryAction` (a local function whose `this+4` is the behavior) installs table 0x00C6883D under the behavior's own name (0x005C7BE8); a callback at `0x005C7CBC` removes it (0x005C7CDA) when `[arg+8]` is non-null. | `0x005C7BE2..0x005C7BE8`; `0x005C7CD4..0x005C7CDA` | NEW | EXACT_SOURCE |
| Transition-time installs | FeedingEat installs at `TransitionToEating` (0x005D6144), BuildPyramid at `TransitionToPlacingTopBlock` (0x005DC0D6), RamIntoBlock at `TransitionToRammingIntoBlock` (0x00604A44), RequestGameSimple at `RequestGame_InitInternal` (0x005EA6E2) and again at `TransitionToPlayingRequstAnim` (0x005EBF26; same name, same table, so a set insert of the same name). | table rows | NEW | EXACT_SOURCE |
| Test/dev behaviors | DevTurnInPlaceTest uses `IBehavior::SmartDisable` with the table returned by `ReactionTriggerHelpers::GetAffectAllArray()` (0x005A0404 returns 0x00C60D40: all 21 = 1). DockingTestSimple, FactoryCentroidExtractor, FactoryTest and LiftLoadTest call `BehaviorManager::DisableReactionsWithLock` directly with the same all-triggers table, under the names `"Docking test simple"`, `"Factory centroid extractor"`, `"Behavior factory test"`, `"LiftLoadTest"`. Only DockingTestSimple (0x005CDA5E) and LiftLoadTest (0x005D4D58) call `RemoveDisableReactionsLock` in StopInternal; FactoryCentroidExtractor and FactoryTest have no matching removal call. | `0x005CA918: blx GetAffectAllArray`; `0x005CB428`, `0x005CFA98`, `0x005D1084`, `0x005D412E`; table 0x00C60D40 | NEW | EXACT_SOURCE |
| Non-behavior callers of `BehaviorManager::DisableReactionsWithLock` | IDockAction::Init (member table `[this+0xC4]`, lock name `"reactionsToSuppress"`, 0x0055189A; table 0x00C55A21 under `"dockActions"`, 0x00551C4C; removals 0x00551540/0x00551578 and in the AlignWithObject/PlaceObjectOnGround destructors 0x0055753C/0x00557570/0x00554708); PlaceObjectOnGroundAction::Init `"placeOnGroundAction"` (0x005548C6); FlipBlockAction::Init (0x0055EEC6; table 0x00C56AE0; destructor removal 0x0055ED88); SevereNeedsComponent::SetSevereNeedExpression `"severe_need_component_lock"` with a per-NeedId table (0x005731A0; removal in ClearSevereNeedExpression 0x00572D56); BehaviorManager::SwitchToUIGameRequestBehavior `"bm_ui_request_game_lock"` (0x005A2756, table 0x00C60D6A; removal EnsureRequestGameIsClear 0x005A32F0); a table copied from a config at 0x005A4E3E; ActivityBuildPyramid (`"lockTriggersFullPyramid"` 0x005A7A00, `"lockTriggersPyramidSetup"` 0x005A82C4; removals 0x005A7B34/0x005A7B64/0x005A82EC). IActivity::SmartDisable callers: ActivityFeeding `"feeding_severe_disables"` (0x005AAE1E and SetupSevereAnims 0x005AB482; removal ClearSevereAnims 0x005AB4F2), ActivitySparked (0x005B16A8; `"finalAnimLockReactions"` 0x005B199C; IActivity::OnDeselected removes at 0x005B34BE). | table rows and the call addresses given | NEW (outside M7-owned classes) | EXACT_SOURCE |
| Unresolved tables | Two sites pass a table that is not a static array: IDockAction::Init 0x0055189A (the table pointer is a member read at `this+0xC4`) and the two config/map-driven sites (SevereNeedsComponent 0x005731A0: value of a `map<NeedId, Array<ReactionTrigger,bool,21>>` entry; 0x005A4E3E: a 21-pair array copied onto the stack from a config object). Their runtime contents come from config or the caller and are not a binary constant. | `0x00551890: ldr.w r2,[r5,#0xc4]`; `0x00573198: ldr r2,[r0,#0x14]`; `0x005A4DE0..0x005A4E2E` | NEW | RECOVERABLE_GAP (read what writes `IDockAction+0xC4`, the severe-need table map, and the object copied at 0x005A4DE0) |

---

## 4. M7-015: ReactToPickup gating and the robot fields it reads

Class layout: constructor 0x00607724 (`IBehavior::IBehavior`, vtable, then `+0x11C = 0` (float 0.0, bits 0x00000000), `+0x120/+0x124 = 0x00000000/0x3FF00000` (double 1.0, bits 0x3FF0000000000000); `0x0060772E: movt r2,#0x3ff0`, `0x00607734: str.w r2,[r0,#0x124]`, `0x0060773C: strd r2,r2,[r0,#0x11c]`). `IsRunnableInternal` 0x0060774C returns 1 unconditionally (`movs r0,#1; bx lr`). `StopInternal` 0x00607D9C is `bx lr` (2 bytes).

| step | what the original does | citation | record | class |
|---|---|---|---|---|
| InitInternal | Resets `+0x120` to double 1.0 (the `+0x11C` deadline is not reset), starts `WaitAction(robot, 0.5f = 0x3F000000)` through `StartActing<ReactToPickup>(action, &BehaviorReactToPickup::StartAnim)`, returns 0. It reads no robot field. | `0x0060775E: strd r1,r0,[r4,#0x120]`; `0x0060776C: mov.w r2,#0x3f000000`; `0x00607770: blx WaitAction::WaitAction`; `0x00607780: blx StartActing<>`; `0x00607784: movs r0,#0` | M7-015 / M7-019 | EXACT_SOURCE |
| UpdateInternal gate 1 | If `this+0x84 != 0` (an action is in flight) return 1 immediately (continue). **Gates 2 and 3 are evaluated only when no action is in flight.** | `0x00607BBA: ldr.w r0,[r4,#0x84]`; `0x00607BBE: cbz r0,0x607bc4`; `0x00607BC0: movs r0,#1` | M7-015 (the record's wording "stays alive only while OffTreadsState==InAir" omits the in-flight short-circuit) | EXACT_SOURCE |
| Gate 2: off-treads | Reads the byte `robot+0x355` (committed OffTreadsState, one byte); if it is not 1 (`InAir`) return 2 (finished). | `0x00607BC4: ldrb.w r0,[r5,#0x355]`; `0x00607BC8: cmp r0,#1`; `0x00607BCA: bne 0x607c08` (`0x00607C08: movs r0,#2`) | M7-015 / M7-021 | EXACT_SOURCE |
| Gate 3: on charger | Reads the byte `robot+0x338` (on-charger contacts flag). If non-zero: Info, channel `"Unnamed"` (0x00607D14), event `"BehaviorReactToPickup.OnCharger"` (0x00607D1C), format `"Stopping behavior because we are on the charger"` (0x00607D3C), then return 2. | `0x00607BCC: ldrb.w r0,[r5,#0x338]`; `0x00607BD0: cbz r0,0x607c20`; `0x00607BE2: blx sChanneledInfoF`; `0x00607C08` | M7-015 / M7-021 | EXACT_SOURCE |
| Retry timer test | `now = BaseStationTimer::GetCurrentTimeInSeconds()` (float); `if !(now > this+0x11C) return 1` (`vcmpe s2,s0; ble`: taken for `now <= deadline` and for NaN). | `0x00607C20..0x00607C38`; `0x00607C38: ble 0x607bc0` | M7-019 | EXACT_SOURCE |
| Cliff test | `CliffSensorComponent` at `robot+0x288`; `GetCliffDataRaw(0)` returns the u16 at `component+0xE` (`add.w r0,r0,r1,lsl #1; ldrh r0,[r0,#0xe]` at 0x006343B0). If `(raw >> 4) <= 0x18` (unsigned `bhi`) call `StartAnim(robot)` (0x00607C52). Otherwise emit DAS event `"BehaviorReactToPickup.CalibratingHead"` with format `"%d"` and the raw value as argument (`sEventF`, 0x00607C64; r3 = raw), then start `CalibrateMotorAction(robot, true, false)` through the plain `StartActing` overload with an empty callback (0x00607C9A..0x00607CA8). In both cases return 1. | `0x00607C3A: ldr.w r0,[r5,#0x288]`; `0x00607C42: blx GetCliffDataRaw`; `0x00607C48: lsrs r0,r3,#4`; `0x00607C4A: cmp r0,#0x18`; `0x00607C4C: bhi 0x607c58`; `0x00607C5E: adr r0 ; "BehaviorReactToPickup.CalibratingHead"`; `0x00607C62: adr r2 ; "%d"`; `0x00607C64: blx sEventF` | M7-019 | EXACT_SOURCE |

### 4.1 Writers of the robot fields read

| field | writers (functions) | citation | record | class |
|---|---|---|---|---|
| `robot+0x355` (committed OffTreadsState) | The constructor zeroes `+0x354..+0x355` (`strh.w r4,[r5,#0x354]`). The only store to `+0x355` is **`Robot::CheckAndUpdateTreadsState(const RobotState&)`**, which copies the pending state `robot+0x356` to `+0x355` and then broadcasts `RobotOffTreadsStateChanged` (`MessageEngineToGame` ctor at 0x00512098, send through `[CozmoContext+4]` vtable slot 0x18 at 0x005120A6). `+0x356` (pending) is written only inside the same function (0x00511F30, 0x00511F44, 0x00511F94, 0x00511FA8, 0x00511FCC) and by the ctor (0x005100E6). `CheckAndUpdateTreadsState` is called from `Robot::UpdateFullRobotState` (0x00512A72). Its inputs (gyro and accel from the RobotState, `robot+0x314`, `+0x384/+0x388` filtered accel, falling timer `+0x35C`, `SetOnChargerPlatform`) are the M10 DerivedState classifier and were not decoded here. | `0x00512088: ldrb.w r0,[sb,#0x356]`; `0x0051208E: strb.w r0,[sb,#0x355]`; `0x00512A72: blx CheckAndUpdateTreadsState`; `0x005100EA: strh.w r4,[r5,#0x354]` | M7-015, M7-021 | EXACT_SOURCE for the writer set; RECOVERABLE_GAP for the classifier (read 0x00511E00..0x005122B0, the body of this function) |
| `robot+0x338` (on-charger contacts) | Constructor zeroes `+0x338/+0x339` (`strh.w r4,[r5,#0x338]`, 0x005100C4). The only other store is in **`Robot::SetOnCharger(bool)`** (0x00511990): `r4 = robot+0x338` (0x00511A6A), and at the end `strb.w fp,[r4]` (0x00511C14) stores the new value (after possibly sending a `ChargerEvent` message, 0x00511BFA..0x00511C0A). `SetOnCharger` is called from `UpdateFullRobotState` with bit 12 of `RobotState+0x4C` (`ubfx r1,r0,#0xc,#1` at 0x00512AAE, call 0x00512AB4). | `0x00511C14: strb.w fp,[r4]`; `0x00512AAE: ubfx r1,r0,#0xc,#1`; `0x00512AB4: blx SetOnCharger` | M7-021 | EXACT_SOURCE |
| cliff raw value (`CliffSensorComponent+0xE`) | Written by `CliffSensorComponent::UpdateRobotData(const RobotState&)` (0x00634016): `[component+0xE] = state+0x50`, `[component+0x12] = state+0x54` (four u16 raw values), and `[component+6] = bit 14 of state+0x4C`. Called from `UpdateFullRobotState` at 0x0051298C. | `0x0063401A..0x00634030`; `0x0051298C: blx UpdateRobotData` | NEW | EXACT_SOURCE |
| `this+0x11C` / `this+0x120` | Written only by `StartAnim` (0x00607ADC, 0x00607AE0), the ctor and InitInternal. See section 5.2. | | M7-019 | EXACT_SOURCE |

---

## 5. M7-019 gaps

### 5.1 ReactToCliff: the complete state machine

Class (0x128 bytes; `IBehavior` is 0x11C). Fields (names are mine, from the readers and writers below): `+0x11C` u32 state; `+0x120` u8 reactNow; `+0x121` u8 copy of the cliff-detected byte; `+0x122` u16 saved cliff threshold; `+0x124` u8 suspicious-cliff / quit flag; `+0x125` u8 finish flag. Vtable relocations from `vtable(BehaviorReactToCliff)`: vptr+0xC UpdateInternal 0x00605408, +0x48 InitInternal 0x00604D50, +0x50 IsRunnableInternal 0x00604D4C, +0x54 StopInternal 0x006053FC, +0x68 HandleWhileRunning 0x00605580, +0x74 HandleWhileNotRunning 0x0060541C.

| step | what the original does | citation | record | class |
|---|---|---|---|---|
| Constructor | `IBehavior::IBehavior`; `+0x124` (strh) and `+0x11C..+0x123` (strd) zeroed; subscribes to three `MessageEngineToGame` tags read from the 6-byte table at 0x00C73740 = u16 {0x22 CliffEvent, 0x34 RobotStopped, 0x39 ChargerEvent} via `IBehavior::SubscribeToTags`. `IsRunnableInternal` returns 1. | `0x00604CEA: strh.w r7,[r8,#0x124]`; `0x00604CF0: strd r7,r7,[r8,#0x11c]`; `0x00604D00..0x00604D16`; `0x00604D4C: movs r0,#1` | M7-019 | EXACT_SOURCE |
| HandleWhileNotRunning (0x0060541C) | Dispatch on the u16 tag at `[event+0x10]`. 0x34 RobotStopped: `+0x11C = 0`, `+0x124 = 0`. 0x22 CliffEvent: reads the byte at `CliffEvent+4` (detected); if 0, nothing; if `+0x124 != 0`, nothing; else Warning event `"BehaviorReactToCliff.CliffWithoutStop"` with format `"Got a cliff event but stop isn't running, skipping straight to cliff react (bad latency?)"`, then `+0x120 = 1`, `+0x121 = detected`, `+0x11C = 1`. 0x39 ChargerEvent: nothing. Any other tag: Error `"BehaviorReactToCliff.ShouldRunForEvent.BadEventType"` with `"Calling ShouldRunForEvent with an event we don't care about, this is a bug"`, global error flag, optional debug break. | `0x00605422..0x00605430`; `0x00605434: blx Get_CliffEvent`; `0x00605438: ldrb r5,[r0,#4]`; `0x0060543E..0x00605486`; `0x0060548C..0x00605496`; `0x00605498..0x006054E2` | M7-019 | EXACT_SOURCE |
| HandleWhileRunning (0x00605580) | 0x22 CliffEvent: if detected is non-zero and `+0x120 == 0`: Debug (`sChanneledDebugF`) channel `"Unnamed"`, event `"BehaviorReactToCliff.GotCliff"`, format `"Got cliff event while running"`; then `+0x121 = detected`, `+0x120 = 1`. 0x39 ChargerEvent: if the byte at `ChargerEvent+0` (onCharger) is non-zero, `+0x125 = 1`. Other tags ignored. | `0x00605594: blx Get_CliffEvent`; `0x0060559C..0x006055DE`; `0x006055E6: blx Get_ChargerEvent`; `0x006055EA: ldrb r0,[r0]`; `0x006055F0: strb.w r0,[r4,#0x125]` | M7-019 | EXACT_SOURCE |
| InitInternal (0x00604D50) | (1) `MoodManager::TriggerEmotionEvent(robot+0x440, "CliffReact", MoodManager::GetCurrentTimeInSeconds())`. (2) `IBehavior::SmartDisableReactionsWithLock(own name, table 0x00C73746 = CubeMoved, FacePositionUpdated, ObjectPositionUpdated, UnexpectedMovement)`. (3) Switch on `this+0x11C`. **State 0**: `this+0x122 = u16 [[robot+0x288]+0xC]` (a CliffSensorComponent field written by the ctor 0x00633FB4, `SendCliffDetectThresholdToRobot` 0x006342C6, `ClearCliffRunningStats` 0x006347BC and `IncrementSuspiciousCliffCount` 0x0063456A; I read it as the cliff-detect threshold; the name is not in a symbol); `sev = [[[robot+0x264]+0x30]+0x14]` (SevereNeedsComponent's current NeedId; 3 = none, set by its ctor 0x00572A0E and by `ClearSevereNeedExpression` 0x00572D6A); build `WaitForLambdaAction(robot, lambda $_0 capturing this, timeout FLT_MAX = 0x7F7FFFFF)` and `StartActing<ReactToCliff>(action, sev >= 2 ? &TransitionToPlayingStopReaction : &TransitionToPlayingCliffReaction)` (member-pointer words 0x0103F5C8 and 0x0103F5C4 by relocation); return 0. **State 1**: `+0x120 = 1`, call `TransitionToPlayingCliffReaction(robot)` directly, return 0. **Any other value**: Error `"BehaviorReactToCliff.Init.InvalidState"` with `"Init called with invalid state"`, global error flag, optional debug break, return 1. | `0x00604D6A: ldr.w r6,[r5,#0x440]`; `0x00604D5C: adr r1 ; "CliffReact"`; `0x00604D84: blx TriggerEmotionEvent`; `0x00604DA0: blx SmartDisable` (r2 = 0xC73746); `0x00604DA4..0x00604DAA`; `0x00604DC0..0x00604E10` (`0x00604E08: movt r3,#0x7f7f`, `0x00604E10: bl 0x55b554`); `0x00604DEC: cmp r6,#2`; `0x00604E82: blx StartActing<>`; `0x00604DAE..0x00604DBC`; `0x00604E22..0x00604E6A` | M7-019 | EXACT_SOURCE |
| WaitForLambdaAction (0x0055B554) | `WaitForLambdaAction(Robot&, std::function<bool(Robot&)>, float timeout)`: `IAction(robot, "WaitForLambda", type 0x33, tracks 0)`, vtable via GOT 0x0103EBEC, function at `+0x78`, timeout at `+0x90`. `CheckIfDone` (0x0055DADE) calls the function: true gives result 0 (done), false gives 0x01000000 (still running). The timeout getter returns `+0x90`. | `0x0055B55E: adr r1 ; "WaitForLambda"`; `0x0055B57C: movs r3,#0x33`; `0x0055B5AE: vstr s0,[r4,#0x90]`; `0x0055DAE4: blx function::operator()`; `0x0055DAE8..0x0055DAF2`; `0x0055DAF6: ldr.w r0,[r0,#0x90]` | M7-021 (evidence 0x0055B554 / 0x0055DADE / 0x0055DAF6), M7-019 | EXACT_SOURCE |
| Lambda $_0 (Init's wait; invoke at 0x006056B8) | If `[[robot+0x254]+0xC] != 0` return false (robot+0x254 is the MovementComponent, ctor call 0x0050FD48; the byte is written by `DirectDriveCheckSpeedAndLockTracks` 0x0063F15C, `UnlockTracks` 0x00640088, `CompletelyUnlockAllTracks` 0x006410CC; its name is not recovered). Else if `this+0x122 == u16 [[robot+0x288]+0xC]` return true. Else Info (channel `"Behaviors"`, event `"BehaviorReactToCliff.QuittingDueToSuspiciousCliff"`, empty format) and set `this+0x124 = 1`, return true. | `0x006056BC: ldr.w r2,[r1,#0x254]`; `0x006056C0: ldrb r2,[r2,#0xc]`; `0x006056CE: ldrh.w r1,[r4,#0x122]`; `0x006056D2: ldrh r0,[r0,#0xc]`; `0x006056D4: cmp r1,r0`; `0x006056F0: blx sChanneledInfoF`; `0x00605718: strb.w r0,[r4,#0x124]` | M7-019 | EXACT_SOURCE for control flow; field names UNKNOWN |
| TransitionToPlayingStopReaction (0x00604F64) | (1) helper 0x005C0CA8 with `"PlayingStopReaction"` (0x00604F90). (2) If `+0x124 != 0`: `SendFinishedReactToCliffMessage(robot)` and return (no animation). (3) Else `CompoundActionParallel(robot)`; member 0 = `TriggerLiftSafeAnimationAction(robot, trigger 0x19E = ReactToCliffDetectorStop, numLoops 1, interrupt true, tracksToLock 0, timeout 60.0f = 0x42700000, strictCooldown false)` added with `AddAction(action, ignoreFailure = false, emitCompletion = false)`; member 1 = `WaitForLambdaAction(robot, lambda $_1, timeout 0.55f = 0x3F0CCCCD)` added with `AddAction(action, ignoreFailure = true, emitCompletion = false)`. The AddAction used is vptr+0x20 of `vtable(CompoundActionParallel)` = `ICompoundAction::AddAction(IActionRunner*, bool, bool)` (relocation 0x0102209C). (4) `StartActing<ReactToCliff>(compound, &TransitionToPlayingCliffReaction)`. | `0x00604F90: bl 0x5c0ca8`; `0x00604FA2: ldrb.w r0,[r4,#0x124]`; `0x00604FBA: blx CompoundActionParallel`; `0x00604FE4: mov.w r2,#0x19e`; `0x00604FD4: movt r0,#0x4270`; `0x00604FEA: blx TriggerLiftSafeAnimationAction`; `0x00604FF8: blx r8` (vptr+0x20, r3 = 0); `0x00605026: movw r3,#0xcccd`; `0x0060502C: movt r3,#0x3f0c`; `0x00605034: bl 0x55b554`; `0x00605044: blx sl` (r3 = 1); `0x0060506E: blx StartActing<>` | M7-019 | EXACT_SOURCE |
| Lambda $_1 (Stop's wait; body 0x0060586A) | Returns the byte `this+0x120` (`ldr r0,[r0,#4]; ldrb.w r0,[r0,#0x120]`). The wait ends when `+0x120` becomes 1 (CliffEvent while running, or Init state 1) or after the 0.55 s timeout. | `0x0060586A..0x00605870` | M7-019 | EXACT_SOURCE |
| TransitionToPlayingCliffReaction (0x006050F0) | (1) helper with `"PlayingCliffReaction"` (0x00605110). (2) If `this+0xD9` (`alwaysStreamline`, JSON key) or `this+0xD8` (hard-spark flag computed in `IBehavior::Init`) is non-zero: `TransitionToBackingUp(robot)` directly (call 0x00605132). (3) Else: DAS event `"robot.cliff_detected"` (`sEvent`, empty key/values, 0x0060514A); `sev = [[[robot+0x264]+0x30]+0x14]`; trigger = 0x13D (NeedsSevereLowRepairCliffReact) if `sev == 0`, 0x131 (NeedsSevereLowEnergyCliffReact) if `sev == 1`, else 0x19D (ReactToCliff); `TriggerLiftSafeAnimationAction(robot, trigger, 1, true, 0, 60.0f = 0x42700000, false)` started with `StartActing<ReactToCliff>(action, &TransitionToBackingUp)` (member-pointer word 0x0103F5CC by relocation). | `0x00605110: bl 0x5c0ca8`; `0x00605122: ldrb.w r0,[r4,#0xd9]`; `0x00605128: ldrb.w r0,[r4,#0xd8]`; `0x00605132: blx TransitionToBackingUp`; `0x0060514A: blx sEvent`; `0x00605176: ldr r7,[r0,#0x14]`; `0x0060518E: movw r2,#0x19d`; `0x0060519A: movweq r2,#0x13d`; `0x006051A2: movweq r2,#0x131`; `0x006051AC: blx TriggerLiftSafeAnimationAction`; `0x006051BC: blx StartActing<>` | M7-019 | EXACT_SOURCE |
| TransitionToBackingUp (0x00605318) | If the byte `[[robot+0x288]+5]` is non-zero: `DriveStraightAction(robot, distance -60.0f = 0xC2700000, speed 100.0f = 0x42C80000, shouldPlayAnimation true)` started with the plain `StartActing(action, std::function<void()>)` overload; the callback (lambda $_2, vtable 0x0102BD0C, captures this and robot; invoke at 0x006058DA loads robot from `[functor+8]` and tail-calls `SendFinishedReactToCliffMessage`) sends the finished message. Else: `SendFinishedReactToCliffMessage(robot)` then tail-call `IBehavior::BehaviorObjectiveAchieved(BehaviorObjective 0x1C = ReactedToCliff, true)`. **The BehaviorObjectiveAchieved call is only on the no-backup branch.** The byte `CliffSensorComponent+5` is initialised to 0 by the component ctor (`strh r0=1,[r4,#4]` at 0x00633FAC writes +4 = 1 and +5 = 0, `CliffSensorComponent::CliffSensorComponent` 0x00633FA0) and is not stored by `UpdateRobotData` (that stores `+6`) or by any `strb [rX,#5]` inside 0x00633FA0..0x00634B80; any other writer was not located. | `0x0060532A: ldr.w r0,[r5,#0x288]`; `0x0060532E: ldrb r0,[r0,#5]`; `0x00605340: movt r2,#0xc270`; `0x00605346: movt r3,#0x42c8`; `0x0060534E: blx DriveStraightAction`; `0x0060536C: blx StartActing(function<void()>)`; `0x00605380: blx SendFinishedReactToCliffMessage`; `0x006053A0: b.w 0x8cc10c` (ARM veneer to PLT 0x4B2C64 = `BehaviorObjectiveAchieved(BehaviorObjective, bool)`; r1 = 0x1C, r2 = 1); `0x006058DA..0x006058DC` | M7-019 | EXACT_SOURCE for the flow; writer of `CliffSensorComponent+5` outside the component: RECOVERABLE_GAP (find stores to `[[robot+0x288]+5]` and `strh` stores to `+4` in other classes) |
| SendFinishedReactToCliffMessage (0x006052BC) | Builds an empty `RobotCliffEventFinished`, wraps it in `MessageEngineToGame`, `Robot::Broadcast(message)`, clears. The function does not use `this`. | `0x006052D2: blx MessageEngineToGame(RobotCliffEventFinished&&)`; `0x006052DA: blx Robot::Broadcast` | M7-019 | EXACT_SOURCE |
| UpdateInternal (0x00605408) | If `+0x125 != 0`: clear it and return 2 (finished). Otherwise tail-call `IBehavior::UpdateInternal` (veneer 0x008CC12C to PLT 0x4B2A90). | `0x00605408: ldrb.w r2,[r0,#0x125]`; `0x00605410: strb.w r1,[r0,#0x125]`; `0x00605414: movs r0,#2`; `0x00605418: b.w 0x8cc12c` | M7-019 | EXACT_SOURCE |
| StopInternal (0x006053FC) | `+0x11C = 0` and `+0x120/+0x121 = 0` (`strh.w r1,[r0,#0x120]`). `+0x122`, `+0x124`, `+0x125` are not cleared here (`+0x124` is cleared by RobotStopped, `+0x125` by UpdateInternal). | `0x006053FC: movs r1,#0`; `0x006053FE: str.w r1,[r0,#0x11c]`; `0x00605402: strh.w r1,[r0,#0x120]` | M7-019 | EXACT_SOURCE |
| `IBehavior+0xD8/+0xD9` | Pre-extraction content re-verified: `+0xD9` is the JSON key `alwaysStreamline`; `+0xD8` is the hard-active-spark flag from `IBehavior::Init` 0x005BCCAA..0x005BCCC2. Polarity: `SetRequestedSpark(id, bool soft)` stores the bool at BehaviorManager `+0x64` and logs `soft` for non-zero (0x005A3EB2, 0x005A3ED4..0x005A3ED8); `+0x5C` is the active copy; the XOR with 1 makes `+0xD8` true for a hard spark. | `0x005BCCAE: ldr r1,[r0,#0x58]`; `0x005BCCB4..0x005BCCBC`; `0x005A3EB2: strb.w r4,[r0,#0x64]`; `0x005A3ED4: cmp r4,#0`; `0x005A3ED8: movne r2,r1` | M7-019 / M7-021 | EXACT_SOURCE |

### 5.2 ReactToPickup::StartAnim (0x00607820..0x00607B98), in full

`StartAnim(this = r8, robot = sl)`. Locals: `hard` (sb) = hard spark active; `faces`, `pets`, `name`.

| step | what the original does | citation | record | class |
|---|---|---|---|---|
| 1. Drop a carried object | `carry = [robot+0x284]` (CarryingComponent); if `[carry+8] != -1` (carried-object ID; `adds r1,#1; itt ne`) call `CarryingComponent::SetCarriedObjectAsUnattached(true)`. | `0x00607838: ldr.w r0,[sl,#0x284]`; `0x0060783C: ldr r1,[r0,#8]`; `0x0060783E: adds r1,#1`; `0x00607844: blxne SetCarriedObjectAsUnattached` | M7-019 | EXACT_SOURCE |
| 2. Hard-spark flag | `bm = [robot+0x44]` (BehaviorManager); `hard = (bm+0x58 != 0x55) && (byte bm+0x5C == 0)`, i.e. an active spark whose soft flag is 0 (hard). | `0x00607848: ldr.w r0,[sl,#0x44]`; `0x00607852: ldr r1,[r0,#0x58]`; `0x00607854: cmp r1,#0x55`; `0x00607858: ldrb.w r0,[r0,#0x5c]`; `0x00607862: moveq.w sb,#1` | M7-019 | EXACT_SOURCE |
| 3. Recent faces | `t = Robot::GetLastImageTimeStamp()`; `since = (t > 500) ? t - 500 : 0` (unsigned: `subs r2,r0,#0x1f4; it ls; movls r2,r4(0)`); `faces = FaceWorld::GetFaceIDsObservedSince(since, false)` on `[robot+0x38]` (a `set<int>`). | `0x0060786A: blx GetLastImageTimeStamp`; `0x0060786E: subs.w r2,r0,#0x1f4`; `0x00607878: ldr.w r1,[sl,#0x38]`; `0x00607880: blx GetFaceIDsObservedSince` | M7-019 (M14 boundary: the face list is produced by the vision/face layer) | EXACT_SOURCE |
| 4. Face branch | Taken when `!hard` and `faces` is non-empty (`0x00607884: cmp.w sb,#0`, `0x0060788A: ldr r0,[sp,#0x24]`). Iterates `faces` in ascending order; for each id `FaceWorld::GetFace(id)` (0x006078A8); if the face exists and its name string (`TrackedFace+0x14`, size from the SSO flag byte) is non-empty, copies the name into the local `name` (`bl 0x4e462a`, 0x006079EC) and stops. | `0x0060789C: beq 0x6079f0`; `0x006078A8: blx GetFace`; `0x006078B0: ldrb r2,[r1,#0x14]!`; `0x006078BE: cmp r0,#0`; `0x006078C0: bne 0x6079ea`; `0x006079EC: bl 0x4e462a` | M7-019 | EXACT_SOURCE |
| 4a. Named face | If `name` is non-empty (0x006079F0..0x006079FE): `SayTextAction(robot, name, SayTextIntent 3 = Name_Normal)` (0x194 bytes; 3-argument ctor 0x00560F94 reached via PLT 0x4A94E4), `SetAnimationTrigger(AnimationTrigger 1 = AcknowledgeFaceNamed, tracksToLock 4)` (0x00607A1C), `StartActing(action, empty function<void(const RobotCompletedAction&)>)` (0x00607A2C). | `0x00607A00: mov.w r0,#0x194`; `0x00607A10: movs r3,#3`; `0x00607A12: blx SayTextAction::SayTextAction`; `0x00607A18: movs r1,#1`; `0x00607A1A: movs r2,#4`; `0x00607A1C: blx SayTextAction::SetAnimationTrigger`; `0x00607A2C: blx StartActing` | M7-019 | EXACT_SOURCE |
| 4b. No named face | If no listed face had a name (0x00607A34..0x00607A66): `TriggerAnimationAction(robot, trigger 2 = AcknowledgeFaceUnnamed, numLoops 1, interrupt true, tracksToLock 4, timeout 60.0f = 0x42700000, strictCooldown false)` through `StartActing` with an empty callback. | `0x00607A34: mov.w r0,#0xcc`; `0x00607A40: movt r0,#0x4270`; `0x00607A44: movs r1,#4`; `0x00607A56: movs r2,#2`; `0x00607A58: blx TriggerAnimationAction`; `0x00607A66: blx StartActing` | M7-019 | EXACT_SOURCE |
| 5. Pet branch | Reached when `hard` or `faces` is empty (0x006078E4): copies the map at `[robot+0x3C]` (a `std::map<int, Vision::TrackedPet>`; begin at `[obj+4]`, end sentinel `obj+8`; copy loop 0x006078F6..0x00607928, insert helper 0x004B6780) into a local map. If `!hard` and the local map is non-empty: `petType = byte [firstNode+0x24]` (lowest key); `TriggerAnimationAction(robot, trigger = petType == 1 ? 0x184 PetDetectionShort_Cat : 0x185 PetDetectionShort_Dog, numLoops 1, interrupt true, tracksToLock 4, timeout 60.0f = 0x42700000, strictCooldown false)` through `StartActing` with an empty callback. | `0x006078E4: ldr.w r0,[sl,#0x3c]`; `0x00607936: orrs.w r0,sb,r1`; `0x0060793A: beq 0x607986`; `0x00607988: ldrb.w r4,[r0,#0x24]`; `0x006079A4: movw r2,#0x185`; `0x006079AC: cmp r4,#1`; `0x006079B0: moveq.w r2,#0x184`; `0x006079BA: blx TriggerAnimationAction`; `0x006079C8: blx StartActing` | M7-019 | EXACT_SOURCE |
| 6. Fallback | If `hard`, or no face and no pet: `flag = byte [[[robot+0x264]+0x18]+0x74]` (AIComponent+0x18 = AIWhiteboard; its ctor stores the member at AIComponent+0x18, 0x00569ADA; per the pre-extraction the Hiccup strategy sets this whiteboard byte in `ShouldTriggerBehaviorInternal` and clears it in `ResetHiccups` (0x006101A0..0x0061022C); I did not re-derive that, nor name the byte); `TriggerAnimationAction(robot, trigger = flag != 0 ? 0xE6 HiccupRobotPickedUp : 0x1A9 ReactToPickup, numLoops 1, interrupt true, tracksToLock 0, timeout 60.0f = 0x42700000, strictCooldown false)` through `StartActing` with an empty callback. | `0x0060793C: ldr.w r0,[sl,#0x264]`; `0x00607940: ldr r0,[r0,#0x18]`; `0x00607942: ldrb.w r4,[r0,#0x74]`; `0x00607958: movw r2,#0x1a9`; `0x00607964: cmp r4,#0`; `0x00607968: movne r2,#0xe6`; `0x00607970: blx TriggerAnimationAction`; `0x0060797E: blx StartActing` | M7-019 | EXACT_SOURCE; byte +0x74 semantics: RECOVERABLE_GAP (find its writers in AIWhiteboard) |
| 7. Retry deadline | After whichever action was started: `x = double [this+0x120]`; `r = GetRNG()->RandDblInRange(3.0*x, 6.0*x)` (doubles 0x4008000000000000 and 0x4018000000000000 times x); `this+0x11C = (float)now + (float)r` (`vcvt.f32.f64`; `vadd.f32`); then `[this+0x120] = x + 0.33000001311302185` (double bits 0x3FD51EB860000000, the promoted float 0x3EA8F5C3; literal at 0x00607B98). No cap. | `0x00607A8C: vldr d8,[r8,#0x120]`; `0x00607A92: blx GetRNG`; `0x00607A96..0x00607AAE: blx RandDblInRange`; `0x00607ABA: blx GetCurrentTimeInSeconds`; `0x00607AC2: vldr d2,[pc,#0xd4]`; `0x00607ACE: vcvt.f32.f64 s0,d0`; `0x00607AD4: vadd.f64 d2,d3,d2`; `0x00607AD8: vadd.f32 s0,s2,s0`; `0x00607ADC: vstr s0,[r8,#0x11c]`; `0x00607AE0: vstr d2,[r8,#0x120]` | M7-019 | EXACT_SOURCE |
| 8. Cleanup | Destroys the local sets/maps and the temporary function objects. | `0x00607AE6..0x00607B02` | | EXACT_SOURCE |

---

## 6. Existing records contradicted by the source

Each quote is from `re-analysis/fidelity_manifest.json` as read on 2026-10-02.

1. **M7-021** ("Live reaction robot fields and the WaitForLambda/state-name helpers", IMPLEMENTATION_GAP). Its
   `unresolved` says the +0x58 store and log are "log-only; a full +0x58 scan found no behaviour-changing reader".
   Contradicted in part: `Robot::Update` reads the string (size and content) at `0x00513F6A..0x00513F9C` and puts it
   into `robot+0x4C`, then into `VizManager::SetText` (0x00513FE6) and `SetSdkStatus(Behavior)` (0x0051407A). It does
   not change a behavior condition, result or action, but it changes message content, and the record's scan missed it
   (section 1.2, R9). The citation `ReactToCliff 0x00604f90/0x00605110` is right but the caller list is not complete
   (175 sites; AcknowledgeCubeMoved's five are also callers).
2. **M7-019** (IMPLEMENTATION_GAP). Its `unresolved` says "ReactToPickup - StartAnim 0x607820 chooses its face path
   (0x1A9/0xE6) from a tracked face". Contradicted: 0x1A9 (`ReactToPickup`) and 0xE6 (`HiccupRobotPickedUp`) are the
   fallback at 0x00607958/0x00607968 when a hard spark is active or neither a face nor a pet was seen. The face path is
   `SayTextAction(name, Name_Normal)` + `AcknowledgeFaceNamed` (named face) or `AcknowledgeFaceUnnamed` (trigger 2); a pet
   gives `PetDetectionShort_Cat/Dog` (section 5.2).
3. **M7-019**, same record: "`robot+0xD9/+0xD8`" and "`[0x120] + RandDbl(3x,6x)`". Both already corrected by the
   pre-extraction; I re-read them: `+0xD8/+0xD9` are IBehavior fields (0x005BCCC2, 0x005BC21E per the pre-extraction),
   and the deadline is `now + RandDblInRange(3x,6x)` in `+0x11C` with `+0x120 += 0.33` after (0x00607ADC/0x00607AE0).
4. **M7-021** (`unresolved`: "stays alive only while OffTreadsState==InAir and +0x338==0; with +0x338 set it completes
   without playing"). Incomplete: the two checks run only when no action is in flight (`this+0x84 == 0`, 0x00607BBA);
   while an action runs the behavior returns 1 regardless of both fields (section 4).

## 7. Existing records whose evidence is too weak for their status

- **M7-014** evidence line "13 concrete 21-entry tables in M7 inventory Appendix F" is not an address or a file line
  in the `.so`. Every table is now cited by address in section 3.3, but the record's `unresolved` group (b) ("Their
  tables are not recovered") is stale: all five are recovered, and the record omits 11 further `IBehavior::SmartDisable` call sites
  (IBehavior::Init, Resume, KnockOverCubes x2, PopAWheelie, DevTurnInPlaceTest, FeedingEat, BuildPyramid, RequestGameSimple x2,
  RamIntoBlock), 4 `IActivity::SmartDisable` sites and the non-smart `DisableReactionsWithLock` callers (section 3.3).
- **M7-018** evidence names `CreateBehavior 0x0059c888` and an Appendix B table, but nothing in the record covers
  `RobotDataLoader::LoadBehaviors`, the container constructor, `AddToFactory`, `VerifyExecutableBehaviors`, the
  `BehaviorClassFromString` miss path (returns Bouncer) or the second container. A claim of "factory binding" owns
  those, per the settled-record rule in AGENTS.md; they need their own records before it can be settled.
- **M7-021** `evidence` cites `0x005C0CA8` and the string addresses but not the `Robot::Update` reader (above).

## 8. Open questions and UNKNOWNs

UNKNOWN or RECOVERABLE_GAP, each with what to read:

1. Whether R9 reaches the wire in the shipped app. Read (a) the SdkStatus periodic sender that uses `kSdkStatusSendFreq`
   (0x00C851CC) and whether it is gated on `IsInSdkMode` (UiMessageHandler vtable 0x0102FE14 slot 0x34 = 0x006633F8:
   `[this+0xE1] || [this+0xE2]`, presumably IsInSdkMode; not confirmed by name), and (b) the body behind `VizManager::SetText` (PLT 0x004A79CC).
2. Writer of the global at 0x01051010 (the `Robot::Update` behavior-system skip counter): scan stores to that address.
3. Whether the second container (`BehaviorSystemManager`, `robot+0x48`) has any live user. There is no direct
   `bl/blx/b` caller of its Update, InitConfiguration, InitializeEventHandlers, GetCurrentBehavior or FindBehaviorByID. A
   vtable-only user was not ruled out (this class is not given a vtable here). Its construction runs every behavior
   constructor a second time; whether the duplicated `IBehavior::SubscribeToTags` registrations in those constructors
   (e.g. ReactToCliff 0x00604D16) have a visible effect was not read.
4. Directory enumeration order (`FileUtils::FilesInDirectory`) decides load order and hence the unordered_map node
   order that `BehaviorContainer` iterates; only log order and duplicate-ID arbitration depend on it. Platform
   behavior: BLOCKED_EXTERNAL unless the manager wants the Android implementation read.
5. Field names with no symbol: `CliffSensorComponent+0xC` (read as the cliff-detect threshold), `CliffSensorComponent+5`,
   `MovementComponent+0xC`, `AIWhiteboard+0x74`. Offsets, readers and writers are exact; the names are inference or
   UNKNOWN as marked.
6. The AnimationTrigger / BehaviorObjective / NeedId / SayTextIntent names above come from the generated C# enums. Read
   the engine `EnumToString` tables if the manager wants authority 1 for the labels (the immediate values are authority 1).
7. Writers of the three `IDockAction+0xC4`, severe-need-table and config-copied lock tables (section 3.3, last rows).
8. The pre-extraction's `IBehavior` constructor addresses (0x005BBC84..0x005BBC92, 0x005BBBA4, 0x005BBC92) were not
   re-read in this pass.

## 9. NEW rows for the manager to turn into records

`Robot::Update` behavior debug string and tick-skip counter (R9, R9a-c); `RobotDataLoader::LoadBehaviors` file set,
read and duplicate-ID rules; container load loop failure paths; `BehaviorClassFromString` miss returns Bouncer and
writes to cerr; `VerifyExecutableBehaviors` is a no-op; `AddToFactory` duplicate behavior; two containers per robot;
`IBehavior::Init/Resume` SparkBehaviorDisables lock; KnockOverCubes and PopAWheelie explicit lock add/remove; the
non-M7 lock callers; `CliffSensorComponent::UpdateRobotData` fields; ReactToCliff full state machine (section 5.1);
ReactToPickup StartAnim branches and `UpdateInternal` gate order (sections 4 and 5.2).
