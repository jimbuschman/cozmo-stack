| Requested step | Coverage / result | Native address, or UNKNOWN |
|---|---|---|
| 1. PlayAnimationAction constructor and stored defaults | Read, including inherited fields; **started/ended/failed are not initialized by this constructor**. | `0x00543C08..0x00543CBE`, base `0x00540C44..0x00540CC0`, `0x0053FDB0..0x0053FF9C`. |
| 1. loops / interrupt / timeout | Caller values stored; loops 0 plus timeout exactly 60.0f changes timeout to maximum finite float. General C++ declaration defaults UNKNOWN; sleep caller defaults are explicit. | `0x00543C74..0x00543CB2`; literal `0x00543CEC=0x42700000`. |
| 2. Init resets / custom-song remap / lookup | Read; cached animation bypasses lookup/remap. Missing lookup still calls streamer with null. | `0x00543F30..0x00543F7E`; lookup `0x00588074..0x005880E4`. |
| 2. SetStreamingAnimation args / zero tag / return | Read; zero tag sets failed and returns ANIM_ABORTED; nonzero tag registers callbacks and returns SUCCESS. | `0x00543F82..0x00543FA0`, `0x00543FFC..0x00544002`, `0x00544128..0x00544144`. |
| 2. R2E subscriptions | CA, CB, D5, in that order, using robot id and captured this. | `0x00543FA2..0x005440C8`; callback vtables `0x010216D8`, `0x01021718`, `0x01021758`. |
| 2. CA callback | Matching tag logs, then sets started=1. | `0x005452F0..0x0054534E`. |
| 2. CB callback | Requires started and matching tag; counts finite loops down, sets ended only at zero; initial zero loops never finishes here. | `0x0054541C..0x005454F4`. |
| 2. D5 callback | Requires started and matching tag; logs and forwards timestamp/event id; no completion-state change. | `0x0054561C..0x0054569E`; forwarding `0x00545750..0x0054577E`. |
| 2. External-interface subscription/callback | E2G AnimationAborted 0x5F; matching 32-bit message tag sets failed=1, without a started gate. | `0x005440CA..0x00544126`; callback `0x005457F4..0x00545852`. |
| 3. CheckIfDone | Ended wins over failed; SUCCESS / ANIM_ABORTED / RUNNING. | `0x005441C0..0x005441DC`. |
| 3. AnimationCompleted | Animation name only, from current action name field +0x78; union variant 1. | `0x005441DE..0x0054420E`; setter `0x0075C544..0x0075C572`, serializer `0x0075B7D6`, `0x006C22DC`. |
| 4. Destructor | Pointer equality, including null==null, gates info then SetStreamingAnimation(null,1,true,false); releases subscriptions in reverse order, then base teardown. | `0x00543DAC..0x00543E6A`. |
| 5. Trigger constructor | Empty base animation name, same action type/mask/timeout; stores trigger, empty group, strictCooldown; resolves group then names action PlayAnimation+group. | `0x00544228..0x005442D2`. |
| 5. Has/GetAnimationForTrigger | Read; Has false leaves existing group untouched; found-but-empty warns. | `0x0054432C..0x005443D4`; wrappers `0x0052FA70/76`; callees `0x00670AD0`, `0x00670810`. |
| 5. Trigger Init / GetAnimationNameFromGroup | Empty group/name returns NO_ANIM_NAME; otherwise copies choice, calls base Init. | `0x0054443C..0x00544498`, `0x005445F4..0x00544638`; M5 interface `0x0057AE74..0x0057AF02`. |
| 5. sEvent | Only base Init SUCCESS and trigger absent from DAS blacklist; event data is trigger-name:group-name. | `0x0054449A..0x005445F0`; call `0x005445BC`; blacklist loader `0x0052E9A0..0x0052EA0A`. |
| 6. Track mask/name/type/lock timing | Caller mask, no clip-derived mask; sleep passes 0. Locks before concrete Init; collision returns TRACKS_LOCKED, not a wait. | `0x00543C26..0x00543C32`, `0x0053FE0E..0x0053FE1C`, `0x00540428..0x00540598`. |
| 6. Timeout and slots | Timeout slot +0x2C reads +0x94; delay slots +0x24/+0x28 return 0; float clock checked before Init/CheckIfDone. | Vtables `0x01021584/0x010215BC`; getter `0x00545210`; `0x00540D4A..0x00540DAA`, `0x00540E7C..0x00540EB6`. |
| 6. ShouldEmitCompletionSignal | **UNKNOWN as a named member**: no matching shipped symbol, string, or identified slot. Actual game and watcher completion gates are read below. Do not invent a virtual predicate. | Game `0x0053FA24..0x0053FAB2`; watcher `0x00541238`, `0x00541BD8..0x00541C04`. |
| 7. Sleep factory triggers/counts | D2 GetIn, D5 Sleeping, D4 Off, each loops=1, interrupt=true, mask=0, timeout=60f, strictCooldown=false; sequential child inside parallel with lift. | `0x0052CEA2..0x0052CFBE`, ctor calls `0x0052CEDE`, `0x0052CF14`, `0x0052CF4A`. |
| Logs and message fields | Enumerated in separate tables below, with levels/channels/tails and direction-specific tags. | Calls and serializers cited per row. |

# PlayAnimationAction and TriggerAnimationAction — independent extraction

Answers `re-analysis/research/requests/20261012-m8-play-animation-actions.md`. Research only, on main after the
requested pull, HEAD `fd06494918e4ade0578be4026d68d4f822917e91`. Read PROJECT_STATE, research README, extractor
instructions, X-M5-9 §4g and the settling-pass M5-038 row. No production code, inventories, manifest or project
state were changed; no branches, worktrees, commits, pushes, hardware acceptance or uploads.

Primary source is `resources/lib/armeabi-v7a/libcozmoEngine.so`, SHA-256
`02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`. Addresses are ELF virtual addresses;
instruction addresses clear the Thumb bit. `20261012-m8-play-animation-actions-native.txt` is the companion
instruction extract; `20261012-m8-actions-disasm.py` produced it locally. Linear extracts include literal pools,
embedded strings and EH cleanup pads; these are not all executable instructions. Ghidra files were navigation
aids only. Read/covered in this report means examined against the binary, not a fidelity classification.

## Current manifest, before ownership proposals

M5-038 currently has title **“CreateGoToSleepAnimSequence animation factory boundary”**, status
**IMPLEMENTATION_GAP**. Its evidence cites `0x0052CEA2..0x0052CFC0`, `0x0052CECA..0x0052CEDE`, `0x0052CF0E`,
`0x0052CF44`: parallel root containing sequential D2/D5/D4 triggers and a lift child, first-trigger arguments
1,1,0,60f,0; its only factory caller is `0x0052CE5E`. Other evidence is the manager-adopted
`20261005-B-M1M2-blockers-extraction.md` and `20261011-m5-settling-pass.md` §1 M5-038. Its current unresolved text
explicitly says the concrete M8 TriggerAnimationAction lifecycle is MISSING and the existing factory returns
null visibly on the stack fallback. This report does not claim that implementation has changed.

M8-017 currently has title **“Go-to-sleep compound execution ordering and failure propagation”**, status
**RECOVERABLE_GAP**. Its evidence is `0x0052CEA2..0x0052CFC0` and the same manager-adopted blockers extraction:
parallel/sequential child execution, ignore-failure predicate and completion/track interactions remain framework
obligations; M1 owns the factory handoff and NOW/retries0 submission. Its unresolved text sends general queue
lifecycle to M8-015. Recovering the concrete children below does not settle the whole compound runtime.

## 1. Construction, including inherited state

The inspected overload is `(Robot&, const string& animationName, uint32 loops, bool interrupt, uint8 tracksToLock,
float timeout)`. These are caller inputs, not hard-coded general defaults. The binary does not supply the original
C++ declaration's optional-argument defaults. The sleep factory explicitly passes 1,true,0,60f, as §7 shows.

| Step / stored field | Original operation | Citation | Proposed owner |
|---|---|---|---|
| C1: action name/type/mask | Builds literal `PlayAnimation` concatenated directly with animationName, with no separator. Calls IAction with type 0x1A (PLAY_ANIMATION) and the caller's track byte. | `0x00543C14..0x00543C32`; literal `0x00BE9C54`; type immediate at `0x00543C30`. | NEW M8 concrete animation action |
| C2: base vptr, robot, retries, status | IActionRunner stores its vptr, robot at +4, retries +8=0, empty status string +0xC..+0x14, state +0x18=0x02000001 NOT_STARTED, completion union +0x1C initially invalid 0xFF. | `0x0053FDB8..0x0053FDEA`. | Existing M8 base runtime boundary |
| C3: type/name | +0x44=caller type 0x1A; +0x48..+0x50 copies the constructed name, respecting libc++ short/long string layout. | `0x0053FDCC`, `0x0053FDE2..0x0053FE0A`. | M8 base |
| C4: control bytes | +0x54=tracksToLock; a 32-bit store of 0x00010000 at +0x55 initializes +0x55=0 (not prepped), +0x56=0 (do not suppress locking), +0x57=1 (diagnostics enabled), +0x58=0 (custom profile flag). | `0x0053FE0E..0x0053FE1C`. | M8 base |
| C5: callbacks/ids | Callback list +0x64/+0x68 points to its sentinel, +0x6C size=0. Allocates an unused global id: original +0x5C=old counter, advances counter, wraps next 0 to 0x002DC6C1; collision logs error, sets errG, optionally debug-breaks, and tries another id. +0x60=current id=original id. | `0x0053FE20..0x0053FEC8`; collision call `0x0053FE8A`. | M8 base |
| C6: default completion payload | For type 0x1A the switch initializes animationCompleted with empty string. | Switch `0x0053FECA..0x0053FED4`, type-0x1A table arm `0x0053FF36..0x0053FF56`. | M8 base |
| C7: IAction fields | +0x70=0 (Init not successful), +0x74=0xBF800000 (-1f start time), IAction vptr; invokes UnlockTracks, which is inert for NOT_STARTED, then stores NOT_STARTED again. | `0x00540C96..0x00540CBA`; UnlockTracks gate `0x005408EC..0x005408FC`. | M8 base |
| C8: concrete vptr/name | Installs PlayAnimationAction address point 0x01021584 through GOT 0x0103EB24; independently copies animationName at +0x78..+0x80. | `0x00543C44..0x00543C6C`. | NEW M8 concrete |
| C9: concrete fields | +0x84=loops; +0x8B=0 (streamer tag); +0x8C=interrupt; +0x90=null cached Animation pointer; +0x94=caller timeout; clears +0x98..+0xB7 (four shared subscription owners). | `0x00543C70..0x00543C92`. | NEW M8 concrete |
| C10: forever-loop timeout | If loops==0 and the binary32 timeout compares equal to 60.0f (0x42700000), replace +0x94 with 0x7F7FFFFF, maximum finite float. Other timeout values, including NaN, are retained. | `0x00543C96..0x00543CB2`. | NEW M8 concrete |
| C11: unwritten bytes | **No constructor store to +0x88 started, +0x89 ended or +0x8A failed.** They have no constructor-established value. Init clears all three before callbacks are installed. Padding bytes are not given invented defaults. | Complete constructor `0x00543C08..0x00543CBE`; Init `0x00543F30..0x00543F36`. | NEW M8 concrete |

The pointer-taking overload at 0x00543CF0 is outside the requested constructor extraction; the requested Init's
cached-pointer branch is nevertheless read. Do not interpret the unwritten flags as a production use of random
state: the normal runtime calls Init before CheckIfDone or these subscriptions. Calling those functions on a
never-initialized action is not assigned invented behavior here.

## 2. PlayAnimationAction::Init and callbacks

| Step | Original operation, including gates and result | Citation | Proposed owner |
|---|---|---|---|
| P1 | Clears failed +0x8A and clears the two bytes started/ended +0x88/+0x89. Does not reset loops or cached Animation pointer. | `0x00543F30..0x00543F3A`. | NEW M8 |
| P2 | If +0x90 is non-null, reuse it, set custom-song flag false and skip name comparison/lookup. Otherwise compare +0x78 string with `cozmo_sings_custom`. If equal, assign `anim_cozmosings_120_song_01` and set the local custom-song flag true. The action's base debug name +0x48 is not renamed. | `0x00543F3A..0x00543F6C`; strings `0x0054417C`, `0x00544190`. | NEW M8 |
| P3 | Gets canned container via robot→context→manager (+0x20)→canned (+0x48), calls GetAnimation(name), caches result at +0x90. Lookup returns node+0x14 on hit; on miss logs L1 below, errG+optional break, and returns null. There is no action-side early return on missing animation. | `0x00543F6E..0x00543F7E`; GetAnimation nonconst wrapper `0x0058806E`, const body `0x00588074..0x005880E4`. | M8 call/failure consequence; M5 canned lookup interface |
| P4 | Calls SetStreamingAnimation(robot+0x60, cachedAnimation, loops +0x84, interrupt +0x8C, localCustomSongFlag), including a null lookup result. Stores its returned byte at +0x8B. | `0x00543F82..0x00543F9C`. | M8 arguments; M5 streamer effects |
| P5 | A returned tag of zero sets failed=1 and returns 0x03000001 ANIM_ABORTED, without installing these callbacks. Nonzero tag proceeds. | `0x00543FA0`, `0x00543FFC..0x00544002`; result built at `0x00543F84..0x00543F8A`. | NEW M8 |
| P6 | Subscribes R2E CA, CB, D5 via robot MessageHandler and robot+0x10 id, in that order, each callback captures this. Subscription owners go to +0x98, +0xA0, +0xA8. Each new pair replaces the prior pair and releases the prior control block if any. Temporary std::function copies are destroyed after registration. | `0x00543FA2..0x005440C8`; registration calls `0x00543FCA`, `0x00544032`, `0x00544092`. | NEW M8 |
| P7 | Unconditionally gets ExternalInterface and subscribes its E2G tag 0x005F, using interface vtable+0x28, capturing this; replaces owner +0xB0, releases prior control block. **No HasExternalInterface gate in this Init.** | `0x005440CA..0x00544126`; call `0x005440F2`. | NEW M8 |
| P8 | Returns SUCCESS (0) if the stored tag is nonzero, else ANIM_ABORTED. Stack-guard failure calls __stack_chk_fail; EH pads destroy temporary callable objects and resume unwinding rather than reporting animation success/failure. | `0x00544128..0x00544146`; cleanup `0x0054414A..0x00544174`. | NEW M8 |
| P9: CA start | On tag match only, info log L2, then started +0x88=1. Other tags do nothing. Does not clear failed or ended. | Invoke slot callback `0x005452F0..0x0054534E`, compare `0x00545304`, store `0x00545348`. | NEW M8 |
| P10: CB end | If started==0 or message tag mismatches, return unchanged. If loops>0, decrement the uint32 loop counter. If it becomes zero, log L5 and set ended +0x89=1. If still positive, log L3 only. If loops was zero, log L4, do not decrement or set ended. No SetStreamingAnimation call in this callback. | `0x00545422..0x0054544C`, `0x00545466`, `0x00545488`, `0x005454C6..0x005454EE`. | NEW M8 |
| P11: D5 event | Extracts AnimEvent payload; requires started!=0 and matching tag. Logs L6, creates E2G AnimationEvent(timestamp,event_id), broadcasts it via external vtable+0x1C. No writes to started/ended/failed/loops. No deduplication or terminal-state gate. | `0x00545622..0x00545698`; helper `0x00545762..0x0054577E`. | NEW M8 report forwarding |
| P12: E2G aborted | Compares the full uint32 AnimationAborted.tag with zero-extended +0x8B. On equality logs L7 and sets failed +0x8A=1. Does not require started, set ended, or clear the tag. Other tags do nothing. | `0x005457FA..0x0054580A`, log `0x00545824`, store `0x0054584C`. | NEW M8 |

Callback address points and invoke slots (+0x18) were read, not guessed from subscription order:
CA `0x010216D8+0x18 → 0x005452F1`; CB `0x01021718+0x18 → 0x0054541D`;
D5 `0x01021758+0x18 → 0x0054561D`; E2G `0x01021798+0x18 → 0x005457F5`.

The two counters are distinct: the action decrements its own +0x84 on received CB messages; the streamer receives
an initial copy of loops. The callback itself never restarts a loop. Repeated matching starts/ends are not
deduplicated. Once a finite counter reaches zero, a further matching end takes the zero-loop diagnostic branch;
ended remains set. CheckIfDone still reports SUCCESS because ended has priority.

### M5 interface consequence of lookup failure

SetStreamingAnimation with null is not just a failed lookup observation. At `0x0057B17E..0x0057B186`, an existing
stream with new null bypasses the “do not interrupt” refusal and goes through Abort at `0x0057B242..0x0057B244`.
It stores null at +0x38, takes the null tail and returns zero (`0x0057B248..0x0057B24C`,
`0x0057B2FC..0x0057B304`). Thus a missing requested animation can clear an existing stream before this action
returns ANIM_ABORTED. The M8 side owns issuing that call; the native Abort descendants remain the M5 interface,
not an added direct 0x8D send from the action.

## 3. Completion and messages

CheckIfDone does exactly this, at `0x005441C0..0x005441DC`:

| State tested in order | Returned full ActionResult |
|---|---|
| ended (+0x89) !=0 | 0x00000000 SUCCESS |
| else failed (+0x8A) !=0 | 0x03000001 ANIM_ABORTED |
| else | 0x01000000 RUNNING |

No test of started, loops, streamer pointer, duration or idle state appears here. If both ended and failed are
set, SUCCESS wins. The enum names are additionally corroborated by shipped
`unity/scripts/csharp/Anki.Cozmo/ActionResult.cs:5..33`; the return bit patterns are read from the instructions.

GetCompletionUnion copies current +0x78 into an AnimationCompleted string and calls the **rvalue** setter at
`0x005441FA`; this is not the base debug name or trigger/group string. The setter is `0x0075C544..0x0075C572`,
which assigns union tag 1. `AnimationCompleted::Pack` `0x0075B7D6..0x0075B7EA` delegates its string to
`0x006C22DC..0x006C2316`: one-byte length followed by that many raw string bytes. There is no tag/loop count or
success flag inside AnimationCompleted. If lookup/remap changed +0x78, the completion reports that current name.

| Message / direction | Tag | Payload fields used / serialized | Primary citation |
|---|---:|---|---|
| R2E AnimationStarted | 0xCA | `u8 tag`, only field. | Registration `0x00543FAE`; getter `0x007B3C36`; pack `0x007B1442..0x007B1462`; match `0x00545302..0x00545304`. |
| R2E AnimationEnded | 0xCB | `u8 tag`, only field. | Registration `0x00544016`; getter `0x007B3CBC`; pack `0x007B1564..0x007B1584`; match `0x00545436..0x00545438`. |
| R2E AnimationEvent | 0xD5 | Wire/in-memory payload `u32 timestamp`, `u8 tag` (+4), `u8 event_id` (+5), in that order. | Registration `0x00544076`; getter `0x007B43D4`; accesses `0x00545630..0x0054563C`; serializer `0x007B167E..0x007B16C0`. |
| E2G AnimationAborted, consumed by action | 0x005F | `u32 tag`, compared against action's byte tag. | Registration `0x005440D6`; getter `0x00725EEE`; pack `0x0071655C..0x00716578`; callback `0x00545806..0x00545808`. |
| E2G AnimationEvent, emitted by action callback | 0x0060 | `u32 timestamp`, `u8 event_id`; robot animation tag is dropped. | Action stores `0x0054568A..0x0054568E`; constructor `0x00725F7C..0x00725F88` sets tag 0x60; pack `0x00716668..0x00716698`. |
| animationCompleted, nested ActionCompletedUnion | variant 1 | `animationName` as length-u8 + bytes. | `0x005441EC..0x005441FA`, `0x0075C56E..0x0075C570`, `0x006C22DC..0x006C2316`. |
| E2G RobotCompletedAction (queue boundary) | 0x005A | Current action idTag +0x60, actionType +0x44, full result +0x18, subActionResults, completionInfo union. This is not the streamer byte tag. | Snapshot `0x00540AC2..0x00540AF4`, constructor helper `0x00540BAC..0x00540BCA`; queue broadcast `0x0053FAA0..0x0053FAB2`; outer-tag immediate/store `0x00725B08..0x00725B0A`. |

No action-specific direct robot send is in the requested Init/CheckIfDone/destructor bodies. Robot animation
start/end/keyframe/abort wire emission is downstream of the M5 streamer and was not re-extracted in full here.

## 4. Destruction

| Step | Original operation | Citation | Owner |
|---|---|---|---|
| D1 | Reinstalls PlayAnimationAction vptr. Compares streamer current Animation pointer (robot+0x98 = streamer+0x38) with cached +0x90. Inequality skips log and streamer call. Equality has **no non-null, started, ended or tag gate**. | `0x00543DB2..0x00543DC8`. | NEW M8 |
| D2 | On equality logs L8 with current +0x78 name, then calls SetStreamingAnimation(robot+0x60,null,1,true,false), ignoring return. Includes null==null. A custom song can replace the streamer's pointer; the equality gate is pointer identity, not name identity. | `0x00543DCA..0x00543E26`. | M8 call; M5 effects |
| D3 | Releases E2G owner +0xB0, then D5 +0xA8, CB +0xA0, CA +0x98 (control blocks +0xB4/+0xAC/+0xA4/+0x9C). Frees long animation-name storage; invokes IActionRunner destructor. | `0x00543E2A..0x00543E64`. | NEW M8 |
| D4 | Trigger-specific destructor first frees its long group-name storage, then invokes PlayAnimationAction destructor. It does not issue an independent streamer call. | Trigger vtable slots +0/+4; bodies `0x00545218..0x00545240`, `0x00545258..0x0054527C`. | NEW M8 |
| D5 | Base destructor verifies Prep was performed, handles custom-profile clearing, erases current/original ids, stops owned tracks still moving (HEAD then LIFT then BODY), then unlocks declared mask if +0x56==0 and state!=NOT_STARTED, then calls ActionWatcher::ActionEnding. | `0x00541084..0x00541238`; stop calls `0x00541146/0x0054116C/0x00541192`; unlock `0x0054122A`; watcher `0x00541238`. | Existing M8 base runtime boundary |

The observed release order is settled at the action call sites. Generic signal-container unlinking, factory
ownership and exceptional last-owner behavior are not newly exhaustively extracted by this request; they remain
the shared subscription infrastructure boundary. The cached Animation pointer is not deleted by this destructor.

## 5. TriggerAnimationAction

| Step | Original operation, gates and result | Citation | Proposed owner |
|---|---|---|---|
| T1 | Constructs an empty string for the base and calls PlayAnimationAction with the caller's loops, interrupt, mask and timeout. Retains type PLAY_ANIMATION 0x1A. | `0x00544236..0x00544264`. | NEW M8 trigger action |
| T2 | Stores trigger uint32 +0xB8, initializes empty group-name string +0xBC..+0xC4, installs TriggerAnimationAction address point 0x010215BC, stores strictCooldown byte +0xC8. | `0x00544276..0x0054429E`. | NEW M8 |
| T3 | Calls SetAnimGroupFromTrigger(trigger), then replaces base debug name +0x48 with literal PlayAnimation concatenated with the resulting group name. | `0x005442A2..0x005442BC`. | NEW M8 |
| T4 | SetAnimGroupFromTrigger always stores new trigger +0xB8 first. Calls RobotManager::HasAnimationForTrigger. Only return==1 calls GetAnimationForTrigger and replaces +0xBC string. Has false leaves prior group untouched; it does not clear it or warn here. | `0x00544332..0x00544382`, skip `0x00544344 → 0x005443D2`. | NEW M8; mapping is M5 interface |
| T5 | Has wrapper fetches manager+0x54 response map. HasResponse converts enum to its shipped string, hashes/finds that key and returns found!=null; presence is not non-empty response. GetResponse returns a copy of mapped group and logs L9; missing returns empty with L10. | Wrappers `0x0052FA70/76`; Has `0x00670AD0..0x00670B1A`; Get `0x00670810..0x00670920`. | M5 mapping interface |
| T6 | After a found response is installed, if its length is zero, warning L11; otherwise silent at this action site. No error flag/break. | `0x00544382..0x005443AC`. | NEW M8 |
| T7 | Init starts result=0x0300000C NO_ANIM_NAME. Empty stored group logs L12 and returns that result. | `0x00544444..0x0054445E`, `0x005445F4..0x00544638`. | NEW M8 |
| T8 | Non-empty group calls GetAnimationNameFromGroup(streamer robot+0x60, group +0xBC, **robot reference**, strictCooldown +0xC8); copies returned string to animationName +0x78. Empty chosen name returns NO_ANIM_NAME without an additional warning at this action site. | `0x00544462..0x0054448E`. | M8 inputs/result; M5 choice |
| T9 | Streamer interface returns a shared static empty string for missing or empty group. Non-empty group delegates to GetAnimationName(group, robot MoodManager +0x440, group container streamer+0x30, raw float robot+0x2FC, strictCooldown). Full weighted/cooldown choice is M5, not re-extracted here. | `0x0057AE7C..0x0057AE90`, `0x0057AEE4..0x0057AF02`. | M5 choice boundary |
| T10 | Non-empty chosen name calls PlayAnimationAction::Init and retains its result. It does not clear cached Animation +0x90, reset loops, or rename base debug name +0x48 before this handoff. | `0x00544472..0x00544498`. | NEW M8 |
| T11 | Traverses RobotManager trigger blacklist tree at +0x8C (sentinel +0x90), comparing node+0x10 with trigger +0xB8. Only base result==SUCCESS and absence from this tree emits sEvent L13. Otherwise return base result unchanged. | `0x0054449A..0x005444DA`. | NEW M8 analytics gate |
| T12 | Forms `$data` as EnumToString(trigger)+":"+group name. Event name robot.play_animation, event value current animationName. Emits once per successful Init invocation allowed by blacklist; then returns SUCCESS. | `0x005444DE..0x005445BC`, final `0x005445F0`. | NEW M8 |
| T13 | Blacklist source is RobotManager::LoadDasBlacklistedAnimationTriggers: iterate the configured JSON array, convert each string to trigger, insert into +0x8C set. Its complete configuration-load caller chain is outside this action report. | `0x0052E9A0..0x0052EA0A`, insert `0x0052E9EA`. | Manager input boundary |

In particular, `0x00544462` loads the robot pointer into r2 and `0x00544466` loads strictCooldown into r3.
The Ghidra rendering that truncates the robot reference to a byte is wrong; the instructions and exported callee
signature establish the actual argument. Trigger Init's group selection and cached base pointer are separate:
if the same instance is initialized again after a prior successful lookup, the name may be selected again while
base Init reuses its cached Animation pointer. This report preserves that observed combination without assuming
how often callers reinitialize these particular instances.

## 6. Base runtime, vtable slots, locks and completion signals

The vtable address points are **0x01021584** (Play) and **0x010215BC** (Trigger), not 0x01031584/0x010315BC.
Read ELF relocation targets as well as raw words: many exported function pointers are zero in file bytes until
relocated. The constructor's GOT references at 0x0103EB24/28 resolve to vtable starts 0x0102157C/0x010215B4,
then add 8 to obtain the address points.

| Slot from address point | Play target | Trigger target | Meaning established from caller/body |
|---:|---|---|---|
| +0x00 | 0x00543DAC | 0x00545258 | Nondeleting destructor. |
| +0x04 | 0x00543F10 | 0x00545218 | Deleting destructor used by queue. |
| +0x08 | 0x00540CE8 | same | IAction::Reset(bool). |
| +0x0C | 0x005441DE | same | GetCompletionUnion, called by Prep and game snapshot. |
| +0x10 | 0x00540D1C | same | IAction::UpdateInternal. |
| +0x14 | 0x0052B0B2 | same | Returns false; **interruptibility**, used by Interrupt/NOW_AND_RESUME. Not a completion-signal predicate. |
| +0x18 | 0x0052B0B6 | same | Motion-profile setter returns true; animation action accepts it with no state change. |
| +0x1C | 0x00543F20 | 0x0054443C | Concrete Init. |
| +0x20 | 0x005441C0 | same | Concrete CheckIfDone. |
| +0x24 | 0x0052B0BA | same | Precondition delay=0f. |
| +0x28 | 0x0052B0BE | same | Completion delay=0f. |
| +0x2C | 0x00545210 | same | Timeout getter, `ldr r0,[r0,#0x94]; bx lr`. |

Track locking is the supplied byte mask +0x54. No requested constructor or Init computes a mask from an animation
clip. HEAD/LIFT/BODY mask bits 1/2/4 are corroborated by the base destructor's ownership calls
`0x00541134..0x00541192`. The sleep actions pass mask 0; this does not mute animation keyframes or claim their
head/lift/body tracks on their behalf.

At the first starting/restarting IActionRunner::Update, it stores RUNNING; unless suppression +0x56 is set,
checks AreAnyTracksLocked(mask) at `0x00540440`. A locked mask logs L15/L16, stores and returns
0x03000019 TRACKS_LOCKED (`0x00540572..0x00540582`). An unlocked mask is taken before UpdateInternal with owner
current action id +0x60 and name +0x48 (`0x00540584..0x0054058E`). Already-RUNNING actions go directly to
UpdateInternal. Diagnostics about SDK-on-charger at `0x00540472` are unreachable under the project's SDK exclusion.

UpdateInternal uses the engine BaseStationTimer float seconds at `0x00540D4A..0x00540D68`; first invocation stamps
+0x74 if negative. It computes `f32(start + timeout)` and tests current time against it before concrete Init or
CheckIfDone (`0x00540D8E..0x00540DAA`). At equality it times out: 0x03000018, warning L17 if diagnostics enabled,
then invokes completion callbacks. There is no wall-clock wait and no automatic action-streamer stop at the
timeout check. The destructor's pointer gate is the later stop mechanism.

The zero-delay precondition path calls Init once until it returns SUCCESS. On SUCCESS it logs L18, sets
+0x70=1 and calls CheckIfDone **in that same UpdateInternal** (`0x00540F2E..0x00540F9C → 0x00540DCE`). RUNNING
does not invoke action completion callbacks; terminal results do (`0x00540EDE..0x00540EFE`). Category-0x04 results
can consume retries and reset start/Init/state (`0x00540DFA..0x00540E78`); ANIM_ABORTED and NO_ANIM_NAME are
category 0x03 and do not enter that retry branch. The sleep submission uses retries=0 at the M1 boundary.

### The requested ShouldEmitCompletionSignal name

**UNKNOWN as a named native member.** Neither exported symbols, inspected class vtables nor repo/shipped app
text searches identified such a function. The classes have the slots listed above. Do not map it to +0x14 just
because that slot returns a boolean: `IActionRunner::Interrupt` uses +0x14 at `0x00540256`, and it is false here.

The observed behavior relevant to completion emission is nevertheless concrete:

| Step | Actual gate and order | Citation |
|---|---|---|
| S1: Prep | Once, while +0x55==0, call virtual completion getter +0x0C into cache +0x1C, then set +0x55=1; repeated Prep only logs L19. | `0x00540750..0x005407C0`. |
| S2: game snapshot | Queue deletion first preps. If HasExternalInterface==1 and full result!=0x03000009 INTERRUPTED, capture RobotCompletedAction before destructor. There is no success-only, started-only, external-id-only or animation-specific boolean gate. | `0x0053FA24..0x0053FA6A`; fields `0x00540AA8..0x00540AF4`. |
| S3: game broadcast | Invoke deleting destructor, null queue's caller pointer, erase iterator, then broadcast saved message through external vtable+0x1C. | `0x0053FA6E..0x0053FAB2`. |
| S4: watcher signal | Base destructor after track stop/unlock calls ActionWatcher::ActionEnding; it snapshots id/type/result/union and enqueues a completion entry regardless of the game-send gate. That later watcher drain is a framework boundary, not CheckIfDone's own signal. | `0x00541238`; snapshot `0x00541BD8..0x00541C04`. |

Thus SUCCESS, ANIM_ABORTED, NO_ANIM_NAME, TIMEOUT and cancellation can produce the normal queue completion report.
Interruptibility false is separate from game emission. The request's method-name attribution remains open; these
observed gates are not renamed into a fictitious method.

## Logs: exact call contracts

`empty kv` means the passed vector of key/value pairs is empty. For non-channeled sErrorF/sWarningF the channel is
the wrapper's Unnamed route; no explicit channel argument is passed at the site. Ordinary info/debug/warning/event
calls have **no errG/debug-break tail**. For `errG+break` below, the caller sets `_errG=1` via GOT 0x0103E790,
then calls sDebugBreakOnError only if `_errBreakOnError` via GOT 0x0103E78C is nonzero. The debug-break body is
`0x0080DAB4: bx lr` in this build. Logging-provider/sink internals remain the existing logging boundary.

| ID / call address | Level; channel; key | Format/value; arguments; kv; tail |
|---|---|---|
| L1 `0x005880A0` | Error; Unnamed; `CannedAnimationContainer.GetAnimation_Const.InvalidName` | `Animation requested for unknown animation '%s'.`; requested name; empty kv; errG+break at `0x005880C6..0x005880DA`, return null. |
| L2 `0x00545320` | Info; Unnamed; `PlayAnimation.StartAnimationHandler` | `Animation tag %d started`; action streamer tag; empty kv; then started=1. |
| L3 `0x00545466` | Debug; Unnamed; `PlayAnimation.FinishedLoop` | `Animation tag %d finished a loop, %d left`; tag, decremented uint32 loops (formatted %d); empty kv; no further action. |
| L4 `0x00545488` | Info; Unnamed; `PlayAnimation.EndAnimationHandler.LoopingForever` | `Animation tag %d finished a loop, continuing until timeout or cancel`; tag; empty kv; loop counter remains zero, no ended write. |
| L5 `0x005454C6` | Info; Unnamed; `PlayAnimation.EndAnimationHandler` | `Animation tag %d ended`; tag; empty kv; then ended=1. |
| L6 `0x00545664` | Info; Unnamed; `PlayAnimation.AnimationEventHandler` | `Event %s received at time %d while playing animation tag %d`; EnumToString(event_id), raw uint32 timestamp formatted %d, tag; empty kv; then external event send. |
| L7 `0x00545824` | Info; Unnamed; `PlayAnimation.AbortAnimationHandler` | `Animation tag %d was aborted from running`; tag; empty kv; then failed=1. |
| L8 `0x00543DF2` | Info; Unnamed; `PlayAnimationAction.Destructor.StillStreaming` | `Action destructing, but AnimationStreamer is still streaming this animation(%s). TellingAnimationStreamer to stream null.`; animationName; empty kv; then null-stream call. Space after `Telling` is absent in the shipped string. |
| L9 `0x0067088A` | Info; Animations; `GetResponseForAnimationTrigger.Found` | `%s -> %s`; trigger enum string, mapped group string; empty kv; copy response. |
| L10 `0x006708E2` | Warning; Unnamed; `AnimationTriggerResponsesContainer::GetResponse` | `Animation requested for unknown response '%s'`; trigger enum string; empty kv; returns empty, no errG/break. This miss is bypassed by the action's prior Has gate in the stable-map path. |
| L11 `0x005443AC` | Warning; Unnamed; `TriggerAnimationAction.EmptyAnimGroupNameForTrigger` | `Event: %s`; trigger enum string; empty kv; returns with empty group, no error tail. |
| L12 `0x0054460E` | Warning; Unnamed; `TriggerAnimationAction.NoAnimationForTrigger` | `Event: %s`; trigger enum string; empty kv; returns NO_ANIM_NAME, no error tail. |
| L13 `0x005445BC` | sEvent / DAS level 2 (Info); no channel parameter; `robot.play_animation` | Literal event value is current animationName, **not a printf format**. kv exactly `{"$data": "<trigger enum string>:<group name>"}`. No error tail. Wrapper `0x0080D114..0x0080D134` dispatches provider+8; MultiLogger `0x00667330..0x0066735A` forwards provider+8; DAS provider `0x006671D0..0x006671D8` sets level 2. |
| L14 `0x0053FE8A` | Error; Unnamed; `IActionRunner.TagCounterClash` | `TagCounters shouldn't overlap`; no format args; empty kv; errG+break, then id-allocation retry (`0x0053FEB0..0x0053FEC2`). |
| L15 `0x005404BC` | Warning; Unnamed; `IActionRunner.Update.TracksLocked` | `Action %s [%d] not running because required tracks are locked`; base debug name, current action id; empty kv; followed by L16 and TRACKS_LOCKED result. |
| L16 `0x00540530` | Warning; Unnamed; `IActionRunner.Update.TracksLockedBecause` | `Required tracks %s locked because %s`; AnimTrackFlagsToString(mask), MovementComponent::WhoIsLocking(mask); empty kv; stores/returns TRACKS_LOCKED. No errG/break. |
| L17 `0x00540EB6` | Warning; Unnamed; `IAction.Update.TimedOut` | `%s timed out after %.1f seconds.`; base debug name, timeout float promoted to double; empty kv; callbacks with TIMEOUT. |
| L18 `0x00540F70` | Debug; Actions; `IAction.Update.PreconditionsMet` | `Preconditions for %s [%d] successfully met.`; base debug name, current id; empty kv; sets Init-success flag. |
| L19 `0x00540784` | Debug; Actions; `IActionRunner.PrepForCompletion.AlreadyPrepped` | `%s [%d]`; base debug name, current id; empty kv; returns unchanged. |
| L20 `0x00540600` | Info; Actions; `IActionRunner.Update.ActionCompleted` | `%s [%d] %s with state %s.`; name, current id, `succeeded` for zero / `was cancelled` for 0x02000000 / `failed` otherwise, EnumToString(full result); empty kv; Prep. |
| L21 `0x005410AE` | Error; Unnamed; `IActionRunner.Destructor.NotPreppedForCompletion` | `[%d]`; current action id; empty kv; errG+break at `0x005410D4..0x005410E8`, then continues base teardown. |
| L22 `0x005411E6` | Info; Actions; `IActionRunner.Destroy.StopMovement` | `Stopping movement on the following tracks since they were locked and are still moving: %s[%s][%d]`; stopped-track list with native trailing separators, base debug name, **original** id +0x5C; empty kv; unlock/watcher follows. |
| L23 `0x0054093C` | Warning; Unnamed; `IActionRunner.SetTracksToLock` | `Trying to set tracks to lock while running`; no args; empty kv; leaves mask unchanged. Only NOT_STARTED permits setter store. |

Additional base logs in the inspected Update body: `0x00540402`, Info/Actions,
`IActionRunner.SetMotionProfile.Unused`, `Action %s [%d] unable to set motion profile. Perhaps speeds already set manually?`,
args name/current id, empty kv, no error tail; unreachable for this class's true-returning +0x18 slot.
`0x00540472`, Info/Actions, `IActionRunner.Update.TracksLockedOnChargerInSDK`,
`Use of head/lift/body motors is limited while on charger in SDK mode`, no args, empty kv, no error tail;
SDK mode is excluded by project scope.
`0x00540E3E`, Info/Actions, `IAction.Update.CurrentActionFailedRetrying`,
`Robot %d failed running action %s. Retrying.`, args robot id/name, empty kv, no error tail; requires category-0x04
result and nonzero retries, not the concrete ANIM_ABORTED/NO_ANIM_NAME paths recovered here.

For the M5 interface SetStreamingAnimation, the directly inspected calls are:
`0x0057B1B2`, Info/Unnamed, `AnimationStreamer.SetStreamingAnimation.NotInterrupting`,
`Already streaming %s, will not interrupt with %s`, args old/new animation names, empty kv, returns tag0;
`0x0057B21C`, Info/Unnamed, `AnimationStreamer.SetStreamingAnimation.Aborting`,
`Animation %s is interrupting animation %s`, args new name (or literal NULL)/old name, empty kv, then Abort;
`0x0057B272`, Info/Unnamed, `AnimationStreamer.SetStreamingAnimation`,
`About to play the custom song`, no args, empty kv;
`0x0057B2C0`, sVerifyFailedReturnFalse key `AnimationStreamer.SetStreamingAnimation.LoopingLiveAnimation`,
format `VERIFY(%s): Animation %s is marked 'live' but numLoops=%d. Clamping to 1.`, args literal `numLoops == 1`,
animation name, requested loops; callee error/errG/flush semantics belong to the already extracted M5-008 helper
boundary (X-M5-9 §4c). This call clamps streamer loops to 1 and does not change the action's +0x84 counter.

## 7. Go-to-sleep factory

| Construction order | Trigger / action | Arguments explicitly passed | Native calls |
|---|---|---|---|
| 1 | New CompoundActionSequential(robot) | No child yet. | `0x0052CEAC..0x0052CEB4`. |
| 2 | TriggerAnimationAction: D2 GoToSleepGetIn | loops=1, interrupt=1, mask=0, timeout=0x42700000 (60f), strictCooldown=0. | Ctor `0x0052CEDE`, AddAction virtual+0x20 `0x0052CEEC`. |
| 3 | TriggerAnimationAction: D5 GoToSleepSleeping | Same arguments; **one** loop, not forever. | Ctor `0x0052CF14`, AddAction `0x0052CF22`. |
| 4 | TriggerAnimationAction: D4 GoToSleepOff | Same arguments. | Ctor `0x0052CF4A`, AddAction `0x0052CF58`. |
| 5 | New CompoundActionParallel(robot), add sequence | AddAction passes empty ignore-failure callable and boolean false. The sequential AddAction calls do likewise. | `0x0052CF64..0x0052CF80`. |
| 6 | MoveLiftToHeightAction(robot, preset=0, 5.0f) | Float bits 0x40A00000; lift action belongs to M4 boundary. Add to parallel with empty ignore predicate/false. | Ctor `0x0052CFA2`, AddAction `0x0052CFB0`. |
| 7 | Return parallel root | No action execution or animation wire send in this factory. | `0x0052CFBA..0x0052CFBE`. |

Trigger enum names are corroborated by shipped `unity/scripts/csharp/Anki.Cozmo/AnimationTrigger.cs:215..218`
(D2/D4/D5 respectively); the numeric values and construction order come from the native immediates. An R2E D5
AnimationEvent is a different enum/direction from trigger D5 GoToSleepSleeping.

## Proposed record split and findings for the manager

This is an ownership proposal, not a manifest edit or a status recommendation.

| Proposed owner | Bounded obligation |
|---|---|
| NEW M8 PlayAnimationAction runtime | C1/C8..C11; P1..P12; state-priority results; animationCompleted name; pointer-gated destructor call and reverse subscription release. Includes concrete action logs and forwarding E2G AnimationEvent. |
| NEW M8 TriggerAnimationAction runtime | T1..T4, T6..T8, T10..T12; action/group/name distinctions, strictCooldown input, Init errors and DAS blacklist gate/event. Trigger destructor's group-string release before base. |
| M8 base runtime (existing framework boundary) | Base ctor/state/id allocation, masks, Init/CheckIfDone tick sequencing and timeout; Prep/cache, normal game snapshot and watcher completion; teardown stop-before-unlock. Do not repeat these under the concrete-action record as an unverified dependency. |
| M8-017 compound runtime | Sequential/parallel execution and failure propagation beyond construction. This request recovers its concrete animation children, not all compound execution. |
| M5-038 factory | Exactly the construction table above and recipient handoff. Concrete runtime is owned by NEW M8 records rather than being implicitly claimed by the factory. |
| M5 streamer interface | GetAnimationNameFromGroup and downstream choice, CannedAnimation lookup, SetStreamingAnimation call effects/tag allocation/looping/live clamp, Abort/custom song and robot output. M8 owns arguments and consumption of return/messages. |
| Shared signal / external-interface infrastructure | Robot-id-filtered registration, owner/container unlinking and delivery mechanics; independent from action callback state decisions. |

Current M8-007, **“An action's track mask is a lock, taken while it runs”**, is **IMPLEMENTATION_GAP**.
Its current evidence text says a collision “stays queued and is tried again on the next tick” and describes a
clip-track-derived lock. The native collision path here instead stores/returns 0x03000019 at
`0x00540572..0x00540582`, and these constructors preserve the caller's mask; the sleep mask is 0. That current
evidence prose is contradicted on the wait/retry interpretation. It must not be used as a recovered wait policy.

Current M8-015, **“ActionList/ActionQueue/IAction lifecycle, cancellation and Robot teardown”**, is
**RECOVERABLE_GAP**. Its current evidence includes queue deletion `0x0053E6A8..0x0053E6E0`, cancel
`0x005409E2..0x00540A40`, base stop/unlock `0x00541146..0x00541238`, game broadcast `0x0053FAB2`, and added
clock/timeout `0x00540D4A..0x00540E80`. This is the appropriate existing base-runtime boundary; the concrete
Play/Trigger lifecycle needs separately named ownership, as the request already observes.

Current M5-008, **“One streaming animation: refuse or interrupt, tags 1..0xFE returned, loops with the same tag,
live forced to 1 loop, ReplayLastAnimation”**, is **IMPLEMENTATION_GAP**; current evidence A4/A5/A6/A8/A9 cites
`0x0057B17E..0x0057B304` and X-M5-9 §4c verifies the VERIFY helper tail. Current M5-034,
**“AnimationTrigger to group name from assets/animationGroupMaps Pairs; first entry wins; unknown gives a warning
and \"\"”**, has the manifest's status **EXACT_SOURCE**, evidence `0x00670438..0x0067075C` and
`0x00670810..0x00670920`. These quoted current claims locate the interfaces; this report does not raise, approve
or independently settle their larger paths. M5 choice and abort descendants are likewise interface boundaries,
not omitted steps relabelled as action-runtime recovery.

## Explicit UNKNOWNs / limits

1. The exact native member intended by `ShouldEmitCompletionSignal` is UNKNOWN. All actual completion gates read
   for this path are given in S1..S4; +0x14 is demonstrably interruptibility. No invented boolean is proposed.
2. General C++ declaration defaults for both constructors are UNKNOWN. Caller-stored inputs and the sleep
   factory's explicit defaults are established; the constructor's unwritten flag bytes are explicitly identified.
3. Full generic subscription-container last-owner/unlinking implementation and exception delivery are outside
   this concrete-action extraction. Registration arguments, callback bodies, replacement and destruction order
   are established at the action sites. This report does not vouch for the entire signal subsystem.
4. The blacklist configuration-load caller chain/asset contents, full group RNG/cooldown/mood selection, custom
   song construction, and complete streamer-to-robot output remain their existing boundaries. No fallback
   behavior is supplied for them here. Robot acceptance/echo behavior is not proven by reading an app callback.
5. No regression tests or production build ran; this is a native-source extraction. There is no commit. The
   manager must check the citations before adopting the proposed record split. Then this task stops.
