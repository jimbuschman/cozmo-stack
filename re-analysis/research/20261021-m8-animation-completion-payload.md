# R-M8-COMPLETION — animation completion payload path

Request: `requests/20261021-m8-animation-completion-payload.md`. Examined HEAD **6d43bd2a**. Independent extraction/read-only comparison, pending manager checking. Coverage was opened before tracing; the completed table follows. Production paths below are relative to `cozmo-stack/src/Cozmo.Robot/`.

| Step | Source instructions/literals | Production file/line and caller | Record | Disposition |
| --- | --- | --- | --- | --- |
| Runner defaults / supported non-animation boundary | Constructor cache initially FF at0x0053FDEA; TBB0x0053FED4, table0x0053FED8; type1A→0x0053FF36..FF56; types12/13 and compound−2→0x0053FF0E..FF18. | `Actions/IActionRunner.cs`:176–177,267; actual head/lift constructor `Motion.cs`:1234; compounds base constructor. | M8-015; M8-017 default use | UNBUILT typed defaults: animation1+empty string; supported head/lift and compound6+no payload. Other concrete overrides are not certified here. |
| Animation name sample | 0x005441DE..0x0054420E: copy this+78 at1EC/1F2; Set_animationCompleted at1FA. No dereference of cached Animation+90. | `Animation/TriggerLiftSafeAnimationAction.cs`:11,59–85 stores `_clip`; no getter override. | M8-020; inherited by M8-021 | UNBUILT payload. Getter always samples the current action string, regardless of result. |
| Variant ownership | Copy ctor0x0075C1A8..C218; move ctorC21C..C298; clearC29C..C2B6; copy assignmentC2B8..C342; move assignmentC344..C3E0; animation setterC544..C5AE. | Base/cache getter, record and dictionary all use uint. | M8-015/017/020 | UNBUILT owned value. Native union is40 bytes in memory, not a four-byte scalar or a wire-layout struct. |
| Wire widths | Pack0x0075C8D8..C94E; tag1 armC90E..C916 calls0x006C22DC..C2326; default6 no payload; SizeC950..C986. | `Actions/RobotCompletedAction.cs`:25–53 currently writes U32(CompletionUnion). | M8-015/020; existing M7-020 carrier label | UNBUILT: U8 variant, U8 byte length, name bytes; exact native overlength boundary below. |
| Prep cache | 0x00540750..C0: first virtual getter writes runner+1C at7AE..7BA, then marks55; repeated Prep logs only. Base getter0x0052B0A6..B0AE reaches copy assignment via0x008CB09C→PLT0x004A964C→0x0075C2B8. | `IActionRunner.cs`:267,432–442,489–502; called from Update/queue/compound. | M8-015 | Call order built; typed cache UNBUILT. Reset does not clear the prepared flag/cache. |
| Game snapshot | 0x00540AA8..B60: subresultsAD0, virtual unionAE4; record ctorAF4 copies, assignmentAFC moves. Helpers0x00540BAC..C1A. | `Actions/ActionWatcher.cs`:227–235; queue `ActionQueue.cs`:250–264. | M8-015 | Order built; typed independent snapshot UNBUILT. This is another derived getter invocation, not an automatic read of the Prep cache. |
| Watcher snapshot / lifetime | Base vptr installed0x0054109A; watcher called0x00541238 before cache clear0x00541272. Union541BF0 before descendants541BFA; deque541C04→541FA0..FF6→5437D6..380C copies union at5437F8. | `ActionWatcher.cs`:110–152; `IActionRunner.cs`:520–546. | M8-015 | Prepared-cache selection built; typed copies UNBUILT. Deque owns its value after runner deletion. Full watcher tree proof remains separate. |
| Compound store | 0x0054F050..F130: virtual child getterF072; independent copyF078; current tagF08C; move into node+14 F0AE, type+3C F0B4; child PrepF0C0; erase/unlock later. | `Actions/CompoundActions.cs`:24,154–171. | M8-017 | Ordering built, uint dictionary UNBUILT payload. Capture precedes child Prep/deletion and survives erasure. |
| Compound proxy | SetProxy0x0054F20C..F26A scans all live matches then cached type; getter0x0054F270..F3F0 picks first live match, else cached union, else warning/base cache. | `CompoundActions.cs`:138–188,213. | M8-017 | Selector/order built; typed live/cached/fallback values UNBUILT. Getter does not change actionType. |
| Concrete outcomes / re-Init | Play Init0x00543F20..F9E; zero tag failure0x00543FFC..4002; trigger0x0054443C..4498,0x005445F4..4638; Reset0x00540CE8..D0E. | `TriggerLiftSafeAnimationAction.cs`:59–85; base Reset489. | M8-020/021 | Name samples recoverable below. Cached native Animation identity remains a separate implementation obligation; no streamer re-audit. |
| API/local delivery | Existing adopted Broadcast0x006625B0..6262A; local0x0065D8B8; queue deletion0x0053FA24..FAB2. | `ActionCompletionReport.cs`:3,8–12; `Animation/CozmoAnimations.cs`:226–229; `AppDelivery.cs`:38–92. | M8-015/020; delivery owners unchanged | Delivery ordering exists, animationName absent from public report. Typed immutable payload must survive queued host delivery and synchronous local5A. |

## Authority and scope

Used `decomp/libcozmoEngine/index.tsv` only to navigate. The claims above come from ARM/Thumb instructions in shipped `resources/lib/armeabi-v7a/libcozmoEngine.so`. TBB tables were decoded as byte displacements from their table base, not mistaken for instructions. Read the selected helper bodies through their returns, including copy/move cleanup; the normal and exception cleanup in compound Store were inspected. No execution of the original or hardware validation is claimed.

Prior adopted evidence reused: `20261012-m8-play-animation-actions.md` and its check (especially C6, D5, S1–S4, P2/P3, T7–T10); `overnight/20261014-ov18-m8-015-check.md` for deletion/watcher gates; `overnight/20261014-ov20-21-check.md` for compound selectors and AlreadyPrepped. Later adopted B-DELIV-1/B-M1-5/6 delivery checks establish the app-first/local-synchronous boundary. `20261021-m8-actionlist-completion-settling.md` B2/B3 is a pending lead, not an adopted oracle. Fresh helper reads here resolve the typed-payload boundary it named; no whole-framework settlement follows.

## Recovered payload and ownership inventory

1. **Default construction is type-specific.** The runner starts with INVALID=FF. The constructor switch initializes type0x1A to animationCompleted with an empty `std::string` (0x0053FF36..FF56). For the supported native head/lift types0x12/0x13 and compound type−2, the table reaches Set_defaultCompleted (0x0053FF0E..FF18→0x0075C84C..C86C), tag6 and no payload. These are defaults, not evidence that every derived non-animation getter returns6. The table also has other arms; they must not be filled with animation/default placeholders.
2. **Concrete animation getter always builds tag1.** At0x005441EC it addresses the action's own `animationName` string at+78;0x005441F2 copies it to a temporary,0x005441FA moves that temporary into the output union. No success flag, tag byte, trigger enum, group string+BC, debug name+48 or `Animation*`+90 is read by this getter. It works even before Init. Native +1C is the beginning of a40-byte variant object; treating only its first four bytes as the value loses its tag/payload semantics.
3. **Copies own their string.** Union storage has tag at+0, string at+4 (12-byte libc++ representation). Copy ctor and copy assignment duplicate a short representation or allocate/copy long string content (0x0075C1C8..C20C andC2F4..C336→0x004E02B2). Move transfers the representation, zeros the source string, and invalidates the source variant (C25A..C294;C396..C3D8). Assignment first releases any destination tag1 heap string; self-assignment is guarded. ClearCurrent frees a heap-backed tag1 string and setsFF (C29C..C2B6). The rvalue animation setter either assigns its existing tag1 string or clears the prior variant and transfers the new string (C544..C5AE). Internal allocator/shrink behavior of0x004E75EC is **UNKNOWN/not needed as a standalone implementation requirement**; visible independent string ownership and the moves are established by the surrounding stores and copy helpers. A managed immutable string plus immutable tagged value can reproduce this result without cloning native layout.
4. **Prep and game are distinct samples.** First Prep calls the derived getter directly into the cache and marks55. Further Prep never resamples. Game-message construction independently requests subresults and then invokes the live virtual getter. Its temporary RobotCompletedAction constructor copies the vector and union (0x00540BC0/0x00540BCA, specifically union0x00540BCA); move assignment transfers them to the caller (0x00540BF8..C14). Therefore a mutating test getter can legitimately produce different game and watcher values; don't optimize the game getter into a cache read.
5. **Watcher uses the prepared value after derived teardown.** IActionRunner destructor installs its own vptr at0x0054109A, eventually calls ActionEnding at0x00541238, and only later clears cache0x00541272. ActionEnding's virtual call therefore reaches the base getter, which copy-assigns the prepared union. It samples union before descendant results and deep-copies its temporary record into the deque (0x005437F8). Destruction of the runner/string/temp does not invalidate the deferred watcher report. A direct deletion that never called Prep has only the constructor-established cache, and is not equivalent to the normal queue path; don't silently call the derived getter after its destructor.
6. **Normal queue order remains:** reentry-tag guard→Prep→optional game snapshot→virtual destructor (including watcher enqueue)→clear/unlink→synchronous local game Broadcast→remove guard (0x0053F9E4..FB06). External-interface absence and INTERRUPTED suppress game snapshot/send, not the watcher destructor event. The existing real host delivery queues the public report before synchronous local5A; queued callbacks must carry an independent immutable payload.
7. **Compound caches a full pair, keyed by current action tag.** Store samples virtual child union, copy-constructs an independent local, obtains current child type/tag, moves the union into tree node+14 and writes type at+3C. It then Preps the child, updates parent type if that child is its configured proxy, and erases or retains/unlocks according to existing compound policy. The map value survives child's erasure. Replacing an existing key releases the prior string through move assignment. Clearing the cache releases its values; no retained reference into a deleted child is needed.
8. **Proxy choices are intentionally asymmetric.** SetCompletionUnionProxy scans all live children, so last matching live tag writes the parent type, then an existing cache entry overrides type. GetCompletionUnion scans for the first live match and invokes its virtual getter; otherwise it copy-assigns cached union; missing proxy warns and copies the base cache. No proxy also copies base cache. The getter never repairs actionType. Preserve captured payload/type independently; no redesign of shared-child retention is needed to build this payload path.

## Outcome and name timing matrix

The result word belongs to RobotCompletedAction, not inside AnimationCompleted. The getter's string rule does not branch on result.

| Situation | String at the concrete getter / normal queue Prep | Primary evidence / limits |
| --- | --- | --- |
| Play action deleted from pending queue without Init | Constructor-supplied animation name. Although base cache starts empty, normal queue Prep invokes the derived getter before deletion. | Play ctor copies supplied name into+78 (adopted C8,0x00543C08..CBE); getter5441DE; queue53FA24. |
| Trigger action not Init'd | Empty animation name, not its group/debug text. | Adopted trigger constructor T1–T6 and base empty-name construction; getter only+78. Direct base-cache teardown also retains initial empty payload. |
| Empty stored trigger group | NO_ANIM_NAME0x0300000C; does not assign+78 on this branch. First attempt reports empty; if the same instance retained an earlier name, this branch itself does not clear it. | 0x00544444..445E→0x005445F4..4638. No claim that all live callers mutate groups between attempts. |
| Nonempty group chooses empty name | Copies that empty choice to+78, then returns NO_ANIM_NAME; completion string is empty. | 0x0054446E..448E. |
| Nonempty name whose canned lookup misses | Name remains the attempted name; lookup returns null, streamer is still called. A zero returned animation tag causes ANIM_ABORTED0x03000001. Payload remains tag1 plus that name. | 0x00543F6E..F9E;0x00543FFC..4002; adopted canned lookup588074..80E4. Streamer internals not reopened. |
| Custom-song sentinel on uncached Play Init | `cozmo_sings_custom` changes to `anim_cozmosings_120_song_01`; getter reports the latter. | Compare/store0x00543F44..F6C; literals0x0054417C/4190, adopted P2. |
| Successful / failed / timed-out / cancelled after Init | Current+78 string at each getter call. There is no special empty-on-failure or success-bit field. Cancel of a current started action changes result, not this string; pending deletion preserves NOT_STARTED. | Getter5441DE..420E; adopted Cancel5409E2..A40 and queue gates. |
| Generic retry / explicit Reset and re-Init | Reset clears initialized/start/state, not cache55 or+78/+90. Ordinary category4 retry is not an intermediate completion. On re-Init, Trigger chooses/copies a new name; Play with non-null+90 skips remap/lookup and reuses its cached Animation pointer. Payload can therefore name the newly chosen string while playback still uses the old cached pointer. | Reset0x00540CE8..D0E; retry adopted OV18 L rows; Trigger44462..4498; Play43F3A..F42. NO_ANIM_NAME/ANIM_ABORTED are category3, not category4 retry requests. Actual callers' retries=0 remain as recorded; this row is not a new positive retry reachability claim. |

First Prep fixes the future watcher value. A subsequent live game getter may see a later name, including a name changed by a subsequent Init if such a lifecycle is used; repeated Prep does not update the cache. This answer does not infer a new supported mutation API or certify every reentrant lifecycle. Native terminal callbacks precede the outer queue deletion; retain existing order instead of moving sampling into a callback or streamer completion ticket.

## Bytes and minimum typed interfaces

Native union Pack writes **one byte** tag (0x0075C8EE). Tag1 writes a **one-byte byte-count** and the selected string bytes without a terminator (0x006C22F8/2304/2316). Tag6 writes nothing further. INVALID FF writes only FF. Union Size for tag1 is full string byte length+2; default/invalid size1 (0x0075C950..C986). Native `AnimationCompleted::Size` is full byte length+1 (0x0075B822..B830).

Important exact boundary: native Pack explicitly narrows the length with `uxtb r2,r6` at0x006C2304 and passes **that narrowed count** to WriteBytes. For a256-byte string it writes length0 and no characters;257 writes length1 and the first byte. This differs from `unity/scripts/csharp/Anki.Cozmo/AnimationCompleted.cs`, whose setter rejects UTF-8 lengths>255 and whose Pack writes the whole encoded array. Native instructions are authority1. Do not infer clamp-to255, throw, or writing all bytes from the app source. Normal representable strings agree: the Unity generated type establishes UTF-8 at its boundary; native helper copies stored bytes without validating encoding. No shipped-animation name-length census or non-UTF-8 host input policy was performed here; those reachability/input questions remain **UNKNOWN**, rather than reasons to change the observed native packing. Buffer-too-small handling stays with SafeMessageBuffer; no claim of successful partial serialization.

RobotCompletedAction field order remains U32 action tag, I32 actionType, U32 result, U8 subresult count, all U32 subresults, then tagged completionInfo. This is generated `unity/scripts/csharp/Anki.Cozmo/ExternalInterface/RobotCompletedAction.cs`:174–192 and native adopted D8/D9; outer MessageEngineToGame tag is U16 0x005A (native0x00725B08..0A). Native struct widths/layout are not this wire schema.

Minimum build surface (proposal from recovered rows, no implementation made):

- Replace the uint getter return, base prepared cache, compound dictionary Union and RobotCompletedAction.CompletionUnion with an owned immutable tagged payload that can represent recovered animation1/string and default6/empty, plus an explicit invalid/unbuilt distinction. Add the concrete animation getter using `_clip` only where `_clip` is the faithful+78 field; fixing cached-pointer replay behavior is separately required before claiming all Play/Trigger semantics built.
- Carry the full value through Prep, independent live game sample, base watcher cache sample, compound store/proxy and record copies. Keep Type separately captured. No consumer should store only the first native word or a mutable union shared with the next getter.
- Expose completionInfo/animationName in `ActionCompletionReport` and preserve it in the app-first/local5A delivery path. Existing `NativeCompletionCache` is a public uint API; its migration/versioning is a manager/API decision, not a source-derived scalar meaning. Do not claim the old scalar is a native completion payload.
- Implement/replace PackBody's final U32 only for supported recovered tags. Unrecovered concrete tags must remain visibly owned gaps instead of silently serializing default6. This request does not authorize a universal union implementation.

Complete current production scalar-consumer census from `rg` of `CompletionUnion|NativeCompletionCache|GetCompletionUnion|PreparedCompletionUnion` under `cozmo-stack/src`: interface default `IActionRunner.cs`:79; base176–177/267/440; compound24/156–157/178–188; watcher129/132/233–234; record27/41; public report3/11. These are every direct scalar read/write found by those named-symbol searches. Additional whole-record carriers are ActionList.GameSend/callback registration (30,50,61), watcher node/deque/callbacks, queue DeleteRunner, and SteppedBehavior.Actions.cs100–109. Their callback signatures already carry the record. Driving listener `CozmoAnimations.cs`:229 consumes only done.Tag/Result, FreeplayStack.cs154–159 passes type/result category/tag to mood, and SteppedBehavior matches tag then passes the complete record. No production name consumer was found because the payload is absent; this does not justify omitting the public API value. No assertion about dynamically constructed/reflection consumers is made.

## Independent examples and regression designs

Literal expected union bytes from native tag dispatch/string helper, not generated by the candidate serializer:

| Value | Expected bytes (hex) |
| --- | --- |
| animationCompleted empty | `01 00` |
| animationCompleted name `a` | `01 01 61` |
| animationCompleted name `anim` | `01 04 61 6E 69 6D` |
| supported head/lift or unproxied compound default | `06` |
| native INVALID | `FF` |

A complete normal body with actionTag0x01020304, actionTypePLAY_ANIMATION0x1A, resultNOT_STARTED0x02000001, no subresults, name`a` is **`04 03 02 01 1A 00 00 00 01 00 00 02 00 01 01 61`**. Outer union adds **`5A 00`** first. SUCCESS changes result bytes to `00 00 00 00` but does not remove/name-change the union. These are fixtures, not a claim that a constructor supplies tag0x01020304 by default.

Proposed regressions through actual production queue/image-independent inputs:

1. Queue a named Play-equivalent runner and delete pending before Init: game and watcher both retain name; a never-Init trigger reports empty. Distinguish normal Prep deletion from direct rejection deletion. Assert exact fixtures above and preserved NOT_STARTED result.
2. Initialize a real trigger with a supplied group/library choice; missing group, empty choice, missing clip/zero tag, successful CA/CB, current cancel and timeout should retain the source matrix's string/result. Feed the existing real robot-message dispatcher and queue/tick, rather than directly completing a playback ticket. Expectations are the matrix/literals, not serializer roundtrips.
3. A controlled typed getter returns `first` on Prep and `second` on game sample: game receives second, base watcher receives first on next watcher drain. Hold both reports after deletion; overwrite the originating action name and ensure both strings remain unchanged. Existing counting-getter test `M8ActionCheckedRowsTests`:171–173 tests order only with uint values; retain its distinction with full names.
4. Compound proxy to a live child, then Store/erase child, then query cached proxy: same owned payload/type; overwrite same tag cache with a different name, clear cache, test missing-proxy fallback and duplicate-tag first-live/last-type asymmetry. Existing `CompoundActionTests`:148/177/181 currently tests uint sentinels, not recovered payload content/lifetime.
5. Exercise Reset/re-Init before and after Prep separately: normal retry has no intermediate completed report; Trigger's newly sampled name and cached pointer identity are independent observations. Do not bless current ticket behavior as a cached Animation oracle. Treat unrepresented pointer identity as the remaining M8-020/021 gap.
6. Queue public API delivery and synchronously process local5A, destroy action/library-owned temporaries, then drain host batch and assert exact retained name and existing publication order. Inspect public report and watcher callback, not only internal serializer output.
7. Serializer fixtures include empty/default/invalid and representable multibyte UTF-8 (length counts bytes), plus native256/257-byte behavior if that input becomes supported. Verify exact externally specified bytes and byte counts. Current `ActionCompletionTests`:415–444 explicitly expects U32 DEADBEEF/zero and a four-byte union suffix; that test documents the old placeholder and is not evidence for native union fidelity. Replace its expectation when the typed path is built.

No tests/builds were run: this is a research-only answer.

## Remaining owners and approval impact

M8-015 owns typed cache/defaults, game/watcher sampling and carriers; M8-017 owns typed compound storage/proxy and lifetime; M8-020 owns animation getter/name and remaining native cached Animation identity; M8-021 inherits the getter and owns trigger Init selection. Public report/API migration and existing M7-020-tagged serializer carrier need manager allocation with these rows. No new record/status or settlement is proposed automatically. Other union variants are **UNKNOWN here as complete production paths**: objectInteractionCompleted0, deviceAudioCompleted2, trackFaceCompleted3, readToolCodeCompleted4 and turnInPlaceCompleted5. Their schemas are named in generated ActionCompletedUnion, but their concrete getters/defaults/reach and recipients must stay with their action owners. Do not call them built from this animation read. Precise record ownership for each untraced concrete variant is **UNKNOWN**, requiring current-record lookup by the manager when that variant is scoped, not a bulk reclassification.

Full child-retention/shared-owner graphs, watcher tree verification, shutdown delivery and streamer fault behavior are intentionally not expanded; retain their already named owners/uncertainty. They do not prevent building an independently owned animation payload along the traced edges. Conversely, a payload build alone cannot settle a record claiming those complete paths. Manager must check these instructions, reconcile inventory/evidence under its layer approval process, and approve any newly frozen evidence before implementation where required. Existing statuses remain unchanged.

## Current record snapshot

The following is the current manifest title/status/evidence at examined HEAD, quoted verbatim as JSON fields. Unresolved prose is not replaced by this answer.

```json
{
  "id": "M8-015",
  "title": "ActionList/ActionQueue/IAction lifecycle, cancellation and Robot teardown",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x0053E6A8..0x0053E6E0; 0x005409E2..0x00540A40; 0x00541146..0x00541238; 0x0053FAB2; Current Cancel then delete; pending direct deletion preserves existing result. Cancelled0x02000000 except NotStarted0x02000001; stop before unlock, watcher then broadcast. Existing B-ACTIONS candidate must be checked for full teardown effects and concrete descendants.",
    "Manager-adopted re-analysis/research/20261005-B-M1M2-blockers-extraction.md T1–T9 and ownership table; instruction companion re-analysis/research/20261005-B-M1M2-blockers-native.txt",
    "P3/P4 full ActionList/ActionQueue/IAction lifecycle: ActionList 0x0053F580..0x0053F5F2, ActionQueue 0x0053F5F4..0x0053F70C, IActionRunner 0x00540370..0x0054063E, clock/timeout 0x00540D4A..0x00540E80, CheckIfDone 0x005549B8..0x005549F4 (20261006-M1M2-missing-triage.md P3/P4).",
    "re-analysis/research/20261012-m8-play-animation-actions.md rows C2..C7, L14..L23, D5, S2..S4 and re-analysis/research/20261012-m8-play-animation-actions-check.md (the +0x57 gate on L17/L18/L20, the DeleteActionAndIter guard set at queue+0x10, ActionWatcher Start/EndUpdating 0x00540386/0x0054057E/0x00540630, the UpdateInternal status string 0x00540D46..0x00540F1C).",
    "re-analysis/research/overnight/20261014-ov18-m8-015-check.md: L1-L9 0x00540370..0x0054063A, 0x00540D1C..0x00540F9C, 0x00540750..0x005407C0, 0x005409DC..0x00540A44; Q1-Q7 0x0053F580..0x0053F708, 0x0053DCE4..0x0053DD92, 0x0053E24C..0x0053E2F6, 0x0053DF2C..0x0053DFC8, 0x0053DE10..0x0053DE5C, 0x0053E690..0x0053E700; D1-D6, D12 0x0053F9E4..0x0053FB06, 0x00541084..0x00541286, 0x00541BB4..0x00541C04, 0x0054187E..0x005418DE; T2 List Clear 0x0053D916..0x0053D93A, 0x0053F938..0x0053F950, 0x0053FB5C..0x0053FB82; QueueAction front end 0x0053D93C..0x0053DAC8; position table 0x0053DA24..0x0053DB70.",
    "re-analysis/research/overnight/20261015-ov56-58-check.md: teardown mechanics from OV56: ~Robot order 0x005110EE..0x00511156; AbortAll 0x00511952..0x0051198C (0x0063BE2A..0x0063BE36, 0x00517DFE..0x00517E0A); ActionList::Cancel clearing gate and slot order 0x0053DE18..0x0053DE54; ActionQueue::Cancel 0x0053E69A..0x0053E6FA (early exit 0x0053E6E4); QueueNow/QueueAtEnd 0x0053E25A..0x0053E36E; DeleteActionAndIter 0x0053F9E4..0x0053FAB2 (re-entrancy set, INTERRUPTED/no-interface gates); result codes 0x0053FDBA..0x0053FDD4, 0x005409E2..0x00540A40; Clear lock-out 0x0053D91A..0x0053D938, 0x0053DCEA..0x0053DD00, 0x0053DCAA, 0x0053DF40, 0x0053F93C, 0x0053F970..0x0053F9AC; ~ActionList double Clear 0x0053D8F6..0x0053D90E; IBehavior StartActing reject-delete 0x005BDC04, Init failure 0x005BCCC6..0x005BCD06."
  ]
}
```

```json
{
  "id": "M8-017",
  "title": "The compound action runtime: ICompoundAction, CompoundActionSequential and CompoundActionParallel (construction, AddAction naming, update/result tables, retry, completion store, teardown), as used by the go-to-sleep sequence and other compounds",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x0052CEA2..0x0052CFC0; Parallel/sequential child execution, ignore-failure predicate and completion/track interactions remain higher framework obligations; M1 owns only factory handoff and NOW/retries0 submission.",
    "Manager-adopted re-analysis/research/20261005-B-M1M2-blockers-extraction.md T1–T9 and ownership table; instruction companion re-analysis/research/20261005-B-M1M2-blockers-native.txt",
    "re-analysis/research/20261012-m8-play-animation-actions-check.md addition 4..5: AddAction(IActionRunner*, bool, bool) 0x0054EC7C -> AddAction(function, bool) 0x0054ED90 (empty predicate; child inherits +0x57; compound name appended); the trailing boolean is never loaded.",
    "re-analysis/research/overnight/20261014-ov20-21-check.md: C1 0x0052CECA..0x0052CFA2; C2 0x0054E990..0x0054EA86; C3 0x0053FDB8..0x0053FE1C; C4 0x0054EC7C..0x0054EEAC; C5 0x0054F4F8..0x0054F54C; S1-S8 0x0054F70C..0x0054F992, 0x0054F5A0..0x0054F696, 0x0054F560..0x0054F59E; P1-P5 0x0054FAB0..0x0054FBCC, 0x0054EC56..0x0054EC7A; D1-D4 0x0054F050..0x0054F130, 0x0054F170..0x0054F1BC, 0x0054F270..0x0054F3EA, 0x0054EB78..0x0054EC2A, 0x0054FC2C..0x0054FC66; the seven log sites; AlreadyPrepped call sites 0x0054F0C0, 0x0054FB3C."
  ]
}
```

```json
{
  "id": "M8-020",
  "title": "PlayAnimationAction: construction, Init, robot/game animation handlers, CheckIfDone, animationCompleted, destructor streamer stop",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/20261012-m8-play-animation-actions.md rows C1, C8..C11, P1..P12, D1..D3, L2..L8: ctor 0x00543C08..0x00543CBE (+0x84 loops, +0x8B 0, +0x8C interrupt, +0x90 null, +0x94 timeout; 60.0f -> FLT_MAX only when loops == 0), Init 0x00543F20 (cozmo_sings_custom remap 0x0054417C/0x00544190, lookup robot[0]->+0x20->+0x48, SetStreamingAnimation(robot+0x60, anim, loops, interrupt, flag) -> +0x8B, tag 0 -> failed, 0x03000001; R2E 0xCA/0xCB/0xD5 via 0x00519F8C and E2G 0x5F via external-interface vtable +0x28; callbacks 0x005452F1, 0x0054541D, 0x0054561D, 0x005457F5 and AnimationEvent forward 0x00545750), CheckIfDone 0x005441C0, GetCompletionUnion 0x005441DE (0x0075C544, 0x006C22DC), destructor 0x00543DAC (StillStreaming info 0x00543DF2 on robot+0x98 == +0x90; SetStreamingAnimation(null, 1, true, false) at 0x00543E26; releases +0xB4, +0xAC, +0xA4, +0x9C), vtable 0x01021584 slots +0x1C/+0x20/+0x2C (0x00545210).",
    "re-analysis/research/20261012-m8-play-animation-actions-check.md additions 2 (the null SetStreamingAnimation tail: +0x73 store and the idle Abort)."
  ]
}
```

```json
{
  "id": "M8-021",
  "title": "TriggerAnimationAction: trigger to group, Init NO_ANIM_NAME paths, the group choice hand-off, the DAS blacklist gate and the robot.play_animation event",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/20261012-m8-play-animation-actions.md rows T1..T12, D4, L11..L13: ctor 0x00544228, SetAnimGroupFromTrigger 0x0054432C (Has == 1 before Get; empty-group warning TriggerAnimationAction.EmptyAnimGroupNameForTrigger \"Event: %s\" at 0x005443AC), Init 0x0054443C (NO_ANIM_NAME 0x0300000C; GetAnimationNameFromGroup with the robot and strictCooldown; blacklist set walk +0x8C/+0x90; sEvent robot.play_animation, value +0x78, $data trigger + \":\" + group at 0x005445BC), vtable 0x010215BC."
  ]
}
```
