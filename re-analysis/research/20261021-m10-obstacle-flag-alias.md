# R-M10-OBSTACLE-ALIAS — AI obstacle byte ownership

Request: `requests/20261021-m10-obstacle-flag-alias.md`. Examined HEADbec5a0dc; independent read-only extraction, pending checking. Coverage opened before native tracing.

| Boundary | Instructions / alias | Disposition |
| --- | --- | --- |
| Robot publishes AI pointer | 50FD94..FDA4; constructor return569B40..44 | Exact direct owning slot Robot+264 recovered |
| Named AI methods and outgoing arguments | 569A88..9CB0,569CB2..9DAC,569DB0..9E9A,569F24..56 | No positive byte4 writer or direct byte-address argument in these bodies |
| Child-held Robot aliases | 569ABE..AC0,569AD0..AD2,569B38..3A;56F60C,56A272,5729FE | Positive publication of Robot reference, not proof of a flag writer |
| Relocation and helper callbacks | 569F4E..56→56A686/56AA28;56DE20..70 | Robot-capable transitive call boundary remains UNKNOWN |
| Named accessor / fetch paths | OD1–OD3 and fresh154 syntax-match scan | No indexed GetAIComponent symbol found; inline loads exist; complete escape closure UNKNOWN |
| Live C# / claim boundary | AIComponent.cs:43..51; Behaviors strategy; test-only setter | Preserve sourced default/read, universal never-written claim unsupported by present census |

## Result and primary evidence boundary

**No new supported writer was established. A universal no-writer result is still UNKNOWN.** The direct ownership methods keep the parent AI pointer in local saved registers and pass child pointers or Robot references to callees; the actual Robot reference is intentionally shared. This closes the narrow direct-argument question for those methods, not all transitive access through Robot+264.

Evidence is actual instructions in `resources/lib/armeabi-v7a/libcozmoEngine.so`. Reused OD1's constructor-zero and predicate read without extracting those known instructions again. OD2/OD3 in `20261007-M10-M15-rows.md`:722..730 already state their limits; their companions are navigation/leads, not a substitute for following aliases. F4 in the classifier answer correctly retains the limitation.

Read the selected named ownership ranges in full: constructor normal tail569A88..9B44 and its cleanup569B46..9CB0; destructor569CB2..9DAC; Init569DB0..9E72 and exception cleanup569E74..9E9A; Update569F24..4C; relocation stubs569F4E..56. Literal/string pools between functions were not treated as executable stores. Read Whiteboard constructor56A270..2EA and both relocation forwarding bodies56A686..69C,56AA28..38; helper Update56DE20..34 and CheckInactiveStackHelpers56DE36..70. The large ObjectInteractionInfoCache constructor and helper UpdateStack are used only for the explicitly stated prefix boundaries below, not claimed fully read.

## Pointer publication / alias table

| Row | Native instructions | Alias and exact claim | Remaining boundary |
| --- | --- | --- | --- |
| A1 | 50FD94 alloc0x34;50FD9A..FDA0 passes allocated pointer r4 as this and Robot r5 as r1;50FDA4 stores r4 at Robot+264 | AI is a separately allocated0x34-byte object, not a subobject at Robot+264 or a MemoryMap proximity record | Whole Robot allocation/exception lifecycle already has its own ownership records |
| A2 | 569A88..9B44 | Parent this stays r4. Child constructors receive freshly allocated child this; cache/whiteboard/workout/request-game/trick-selector/severe-needs receive r1=Robot r5. Analyzer/animation-response/helper/feeding-sound/tracker constructors do not receive parent this as an explicit argument | A callee with Robot can refetch Robot+264 after publication. Parent pointer not directly passed is not “cannot reach parent” |
| A3 | 569ABE..AC0→56F5EC; prefix56F5F4..614 | ObjectInteractionInfoCache saves supplied Robot in fp and writes it to cache+0 at56F60C. This is a positive retained Robot alias | Constructor remainder after56F614 and methods/callbacks through that retained Robot are **UNKNOWN in this bounded extraction**; no claim they write AI+4 |
| A4 | 569AD0..AD6→56A270;56A272 | Whiteboard stores Robot in whiteboard+4, then initializes its own fields. Entire constructor uses **whiteboard** as base; its +4 is a pointer, not the AI bool | Transitive whiteboard consumers through stored Robot are not closed by inspecting its own stores |
| A5 | 569B38..3E→5729FC;5729FE..2A14 | SevereNeeds stores Robot at severe-needs+4 and initializes state14=3. Its +4 store is not AI+4 | SevereNeeds.Init572A64/Update572BD0 are named re-entry boundaries, not discharged by the outer wrapper |
| A6 | 5103D8..DC→569DB0;569DB6..DD0,569E60..68 | Init gets Robot from AI+0, context from Robot+0, calls Whiteboard.Init, SevereNeeds.Init, Workout.InitConfiguration with child pointer/config. No passed AI+4 address or direct mutation of parent byte4 in complete wrapper | Child Init may access retained Robot or external callbacks. Do not infer purity from the wrapper |
| A7 | 513EA8..AC→569F24;569F28..46 | Update passes Robot to analyzer (+8) and helper (+10); calls Whiteboard(+18), SevereNeeds(+30), tracker(+2C) updates using child this. No call receives the parent AI pointer directly | Analyzer.Update57428C and helper.Update56DE20 receive Robot; other children may retain it |
| A8 | 56DE20..34,56DE36..70 | Helper.Update forwards Robot to CheckInactiveStackHelpers then UpdateStack (tail veneer8CB75C). CheckInactiveStackHelpers passes that Robot as r1 to helper virtual+28 at56DE5A. This is an actual indirect Robot-capable call | Concrete helper dispatch/targets and retained callbacks are **UNKNOWN here**; a whole helper census is not authorized by a no-writer guess |
| A9 | 510D6E..72 and512464..46C→569F4E/54 | AI relocation stubs immediately load child+18 and tail-call Whiteboard.OnRobotDelocalized56A686 / OnRobotRelocalized56AA28 via8CB6CC/6DC→PLT4ACA54/60 | Child receives whiteboard, not parent. Whiteboard-held Robot remains accessible |
| A10 | 56A686..69C and56AA28..38 | Delocalized calls RemovePossibleObjectsFromZombieMaps56A69E, UpdatePossibleObjectRender56A6D4, then tail forwards; Relocalized renders then tail forwards. These wrappers have no direct parent-byte store | Exact subsequent bodies/tail callees' Robot alias closure **UNKNOWN**; this report does not label their world work “no effect” |
| A11 | 51126A..27A and5114B8..4C8 | Robot clears owning slot264 before AI destructor then frees allocation; destructor receives parent as this | Child callbacks can still retain other Robot references; clearing this slot does not prove whole-binary non-aliasing |
| A12 | 569CB2..DAC and constructor cleanup569B46..CB0 | Parent saved r4. Child slots30,2C,28,24,20,1C,18,14,10,C,8 cleared before destruction; virtual calls and captured-function destruction receive child/capture pointers, not parent+4. Direct stores use those parent child-slot offsets, not byte4 | Virtual deleting destructors569CC2/9D1C/9D8A and captured functions569D4E/9D66 are indirect boundaries; no invented assumption of purity |

In A2 the child pointer is written into the parent only after its constructor returns. AI itself is published to Robot only at50FDA4, after its constructor returns. That publication order matters to a callee refetch during construction: do not assume a child constructor can already obtain this newly built AI through Robot+264. Its saved Robot can obtain it later.

## Fetch/accessor census and exact limits

No named `GetAIComponent` symbol was found in the ELF symbol lookup used here. This is not proof no accessor exists: direct inline Robot+264 loads are the known access mechanism, and stripped/unnamed or indirect accessors remain possible.

Fresh whole-.text Thumb linear decode, excluding PC/SP-relative loads, found **154 syntactic** `ldr[.w] Rx,[Ry,#0x264]` matches. The count is not 154 verified Robot→AI references: other types can have that offset; decoding embedded literal/data bytes can also generate matches. No universal absence result follows from this pattern. The older OD3 indexed-function census is likewise not a provenance-aware interprocedural alias analysis.

Representative fresh ownership call-site reads:
-5103D8→Init,510D6E→OnRobotDelocalized,512464→OnRobotRelocalized,513EA8→Update match A6/A7/A9.
-51126A/5114B8 load the whole parent for its known destructor, then clear the Robot slot as above.
-6136DC fetches Robot+264 but6136E2 immediately selects AI+30 (SevereNeeds); its later +4 is not automatically the parent bool. This prefix was read only through6136EC, not the entire strategy.
-The predicate6143CA is the reused OD1 positive byte4 read. It does not supply a writer.

Explicit unclosed classes:
1. **Computed offsets:** `Robot+0x260` then `+4`, register-indexed loads, pair/vector loads spanning264, aliases adjusted before a store, memcpy/memset covering byte4. Neither a +264 load pattern nor a strb+4 scan excludes these.
2. **Escaped aliases:** AI pointers stored in locals/heap/captures or returned by a wrapper, and Robot aliases retained by child components. A complete named-method read does not follow every retained/callback alias.
3. **Unindexed fragments / mode:** Thumb linear decode and indexed extents do not prove code coverage across branch tables, ARM code, veneers, unnamed fragments or external/runtime calls.
4. **Indirect calls:** helper virtual+28 at56DE5A and child/capture destruction above are real unresolved boundaries, not merely hypothetical syntax.
5. **Bulk/overlapping writes:** byte4 can be changed without a literal strb #4. No broad memory-write provenance proof was performed.
6. **Supported input:** no source-backed external/robot/proximity/config input that sets the flag was found. That is **UNKNOWN**, not a newly approved exclusion and not permission to make a sensor-based writer.

## Current C# and bounded record recommendation

`cozmo-stack/src/Cozmo.Robot/Behavior/AIComponent.cs`:43..51 documents global never-written behavior and has an internal ObstacleDetected setter. Current source assignment search found no production setter; the explicit true setter is in `cozmo-stack/tests/Cozmo.Protocol.Tests/DerivedStateTests.cs`:1483. The current strategy reads AI.ObstacleDetected through context, as F4 reports. `Behavior/Behaviors.cs`:626 defines ReactToObstacle with that strategy. Preserve the established default/read and do not connect it to proximity/map data without a native writer.

The current manifest title/evidence below assert more than the completed alias closure. Recommend a manager-reviewed scope such as **“AI obstacle latch: constructor default and StrategyObstacleDetected reader; writer reach unresolved”**, with the existing implementation gap retained for the universal no-writer claim. This is a proposed narrow claim, not a manifest edit, status change or “not supported” decision. Existing default-false production behavior need not be replaced by an invented writer merely because the stronger title is unproved.

Source-supported follow-up, only if a universal lifetime claim is required: start from the **actual retained Robot/indirect boundary A3/A5/A8**, and track whether a callee ever obtains/stores the parent AI pointer or its byte4 address. The next exact boundaries are cache constructor continuation56F614, SevereNeeds.Init572A64/Update572BD0, and helper virtual+28 call56DE5A (resolve concrete targets before reading full behavior bodies). Prefer narrowing the record now to launching another repository-wide obstacle audit.

Regression limits: constructor-default and strategy-read tests can verify those claims; an ordinary packet/behavior run that stays false verifies that finite path only. A unit setter true tests the reader, not a native producer. No test can prove the whole-binary negative in the current evidence set. No production/tests/manifest/inventory edits or hardware performed, no fidelity settlement claimed.

## Current record snapshot

Exact current M10-009 title/status/evidence/unresolved follows; read from manifest rather than recalled.

```json
{
  "id": "M10-009",
  "title": "Nothing in this build ever raises the obstacle-detected flag",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "StrategyObstacleDetected 0x006141F8 is a StrategyGeneric whose predicate is the three-instruction function object at 0x006143CA: return *(bool*)(*(void**)(robot + 0x264) + 4)",
    "Robot+0x264 is the AIComponent: Robot::Delocalize calls AIComponent::OnRobotDelocalized on it at 0x00510D72",
    "AIComponent+4 is a plain bool the constructor zeroes at 0x00569A80, and that store is the only byte written at +4 anywhere in AIComponent code",
    "no wide strb to +4 exists in the binary outside MemoryMapData_ProxObstacle's constructor, and no function that fetches the component from Robot+0x264 writes a byte at its +4 within forty-five instructions of doing so",
    "so the strategy never becomes true, and ReactToObstacle - listed by hiking, nothingToDo, playAlone and socialize, with reactToObstacle.json giving it this strategy - never runs"
  ],
  "unresolved": "built, awaiting strong verification (R-FIX2, 2026-10-03): AIComponent+4 is zeroed (0x00569A80) and never written; the writable BehaviorContext.ObstacleDetected seam is removed; the flag is AIComponent.ObstacleDetected with an internal setter (read as context.AI?.ObstacleDetected, 0x006143CA), settable only by the test assembly. Still open: none known (nothing in production raises it, as in the engine)."
}
```
