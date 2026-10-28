# HIKING-ROLL-CUBE-1 — finite PlaceBlock and response-animation join check

2026-10-28, additive to the frozen main read packet and independent read check. Root requested a final bounded check of R4 and R6/R7 before builder release. Reviewer request remains DEFAULT model/effort; runtime telemetry unavailable. No code/test/official metadata edits, git mutation, builds/tests or hardware.

**PASS**, with these concrete joins included in the combined builder scope. Direct Ground alone and the empty PlaceRel converter are insufficient evidence. `known.py PlaceBlockHelper`, `RespondToResultWithAnim`, and `BehaviorEventAnimResponseDirector` preceded direct primary traces. ELF SHA remains02263C07F6BB60F4D7F351A3667DCA84FD3E6C0FC6F838E18B5DE7CF4E2989E1. Dumps `.scratch/hiking-review-newjoins.txt`/`hiking-review-response.txt` are instructions, not decompiler output.

## Concrete PlaceBlockHelper, not a direct-Ground adapter

Earlier actual checked rows are `20261007-q17-checked-additions.md` **PB1-PB4**, and `20261004-helpers-extraction.md` **G01**, not the later Stack direct-Ground recovery. Independent primary confirmation:

|Step|Native behavior|Evidence|
|---|---|---|
|Factory/constructor|Roll DelegateToPutDown calls IHelper.CreatePlaceBlockHelper at5B9DA6. Forwarder5B6EE6..EF0 passes the actual helper's behavior/factory to5B5588, which allocatesF0, constructs PlaceBlock, adds it to actual component. Place ctor uses base IHelper(name PlaceBlock,robot,behavior,factory) and only installs concrete vptr.|5B6EE8 ldrd r3,r1,[r1,#9C];5B5590 allocationF0;5B55A4 ctor;5B55B0 AddHelper;5B8C1C..6A|
|Init RNG|Use actual Robot.GetRNG twice: first RandDblInRange(binary64pi/4,pi/2), then RandDbl(1). If sign draw<.5, negate first by sign-bit xor; round selected angle once to f32. Start TurnInPlace(robot,angle,false) through actual helper-owned StartActing, callback RespondToTurnAction. Discard StartActing bool and return current status.|5B8CBE..D06; lower-bound literal5B8D60=3FE921FB54442D18; upper3FF921FB54442D18 built5B8CC8..DC;5B8D10..1E compare/select/narrow;5B8D32 turn;5B8D42 StartActing;5B8D46 returnstatus|
|Turn response|Ignores original turn result. Build sequential compound and actual PlaceObjectOnGroundAction(robot), insert with flags(false,true), start through actual helper with RespondToPlacedAction. A failed/cancelled turn callback is not translated into helper failure here.|5B8DF4..8E44;5B8E14 Ground ctor;5B8E1E r3=0,5B8E22 r0=1 stackbool,5B8E28 AddAction virtual20;5B8E3E StartActing|
|Ground response/update|Ignores Ground result and stores helperstatus2 unconditionally. Active update returns status1C; parent Roll delegate therefore takes the actual status2 callback, stores Roll+120=true, re-evaluates choice, returns1 as previously checked.|5B8E60..68;Roll callback5BAA82..92|
|Stop/destruction|No derived stop side effects: concrete vtable01025EF8+8+24 has ARM_RELATIVE addend005B5F1D, empty stop hook5B5F1C (`bx lr`). ShouldCancelDelegates5B8CA6 returnsfalse. Dtor5B8C94 branches common IHelper dtor5B5F20; deleting dtor5B8C98 calls same then delete. Actual shared IHelper stack retirement/owned action cancellation remains its checked lifecycle contract, not a new guessed derived Stop.|vtable slot01025F24,bytes5B5F1C=7047;5B8CA6..A8;5B8C94..CA2|

Actual Ground action/wire/carry completion is the already checked Ground recipient; these new rows establish how the helper constructs and responds to it. Its result-insensitive completion is source behavior, not manufactured terminal success. A regression should use actual turn and Ground runners/events, then verify that the callback result is ignored at this native boundary; do not directly set helperstatus2 to imitate it.

## Actual nonempty Roll response path and director lookup

Earlier generic response rows A01-A06 in20261004-helpers-extraction are useful and source-backed, but A01 explicitly left director policy as interface. This finite trace resolves that missing concrete recipient:

1. Roll StartActingWithResponseAnim5BA790 stores result callback atC0 and nonempty Roll mapper atA8, then starts with IHelper.RespondToResultWithAnim (5BA7A8..BC). Roll mapper5BAC3E..54 yields userresult1 iff original04000003, userresult2 iff original0, else0. No behavior/hard-spark/face tuple is passed to the director; the director's entire key is that mapped userresult.
2. IHelper.RespondToResultWithAnim5B7160 forwards original result and original result-reference to scalar template5B717C. At5B71DA read converter presence; nonempty5B71EA invokes it. Userresult3 bypasses lookup; otherwise5B71F4 actual robot+264,5B71F8 AI+0C,5B71FA GetAnimationToPlayForActionResult. This is the real Robot-owned director, not a private helper map.
3. Director ctor **0056D86C**: It initializes an empty map, inserts byte key1 at0056D894/898 and stores trigger1FA at0056D8B2/B8; then byte key0 at0056D8BC and trigger1F9 at0056D8D6/DA. There are exactly two entries and no config load in this ctor. Get0056D904..946 performs exact-key map lookup and returns23F Count for absent keys. The map does not contain2.

|Original Roll result|Mapped key|Actual director answer|Action|
|---|---|---|---|
|04000003|1|1FA (RollBlockRetry)|Schedule actual TriggerAnimationAction before original retry callback|
|0 success|2|23F Count (missing key)|Immediate original success callback; no helper response animation|
|Every other full result|0|1F9 (RollBlockRealign)|Schedule actual TriggerAnimationAction before original callback; cancellation is still passed to RespondToRollingResult after response|

4. Chosen trigger!=Count takes5B7282..72A4: TriggerAnimationAction(actualRobot,chosenTrigger,loops1,booltrue,byte0,float60,false). At5B72AC..BA capture ORIGINAL full scalar result in closure with pointer to stored original callback. Start through actual IHelper.StartActing5B72C8, then clear converter after scheduling5B72E0..F8. Do not clear original callback before the response has completed.
5. Response closure operator5B7752 forwards to5B777C..E8. It ignores the response animation result, copies original callback, destroys/clears its stored version5B7798..77B0, then invokes copied callback with the captured ORIGINAL scalar result and current Robot5B77B6..BE. Trigger failure therefore does not replace the Roll result. It does not create a typed full-public Roll payload; the independently established undefined-publication guard remains separate.
6. Converter absent, key3 or selectedCount bypass5B7208..7252 copies original callback, clears it and converter before invoking original callback immediately. Roll success0 reaches this path through key2 absent, then RespondToRollingResult sets helperstatus2, then actual parent success handles its own RollBlockSuccess animation/objective/needs.

The scalar template and checked common StartActing/Trigger/action-list/animation recipients are reused. These facts require concrete director keys and deferred-callback semantics in the combined Roll implementation; a placeholder MISSING claiming no actual response recipient or a forced Count result for all keys would leave newly recovered live behavior unbuilt. Existing unavailable animation-group/firmware inputs remain named boundaries; they are not converted to immediate completion.

This adds finite source evidence; no existing official broad record is settled or narrowed, no ID invented. Source follow-up questions are not builder reviewer fix rounds. The initial READ PASS's inherited-response shorthand is qualified by this explicit concrete join check.
