# M8 animation completion — partial independent check (2026-10-21)

**Codex review; Claude spot-check pending**

Reviewed at dc3004dd, against the current manifest and native instructions in
resources/lib/armeabi-v7a/libcozmoEngine.so. Report:
20261021-m8-animation-completion-payload.md. This is a partial check, not adoption
of the whole report, inventory approval or builder release.

## Coverage and verdict

- PASS, getter source: 0x005441DE..0x0054420E copies the string at action+0x78
  (0x005441EC/1F2) into the animation setter (0x005441FA). No result gate or
  cached Animation+0x90 read occurs in this getter. This does not establish all
  writers of the name or the trigger retry lifecycle.
- PASS, animation wire payload: 0x006C22DC..0x006C2326 writes the low byte of
  string length at 22F8/22FC, then explicitly narrows the byte count with uxtb
  at 2304 before WriteBytes at 2316. Length256 writes zero length/no name bytes;
  257 writes length1/first byte, provided buffer writes succeed. No clamp255 or
  string validation is present in this body. Host encoding and reachable name
  lengths remain unreviewed.
- PASS, tag1 dispatch: union Pack 0x0075C8D8..0x0075C94E writes one tag byte.
  Decoding the seven raw TBB bytes at 0x0075C8FC (not disassembling the table as
  instructions) gives tag1 target 0x0075C90E, calling 0x006C22DC at C912.
  Tag6 targets C940; this check does not adopt the other variants or Size bodies.
- Confirmed existing implementation gap: Actions/RobotCompletedAction.cs carries
  uint CompletionUnion and PackBody writes U32, so it cannot express the checked
  tag1/name payload. TriggerLiftSafeAnimationAction retains its visible missing
  string-completion diagnostic. This is an existing incomplete path, not a newly
  introduced builder regression. No tests were run: this is a source check.

## Current record ownership and correction

M8-020 is IMPLEMENTATION_GAP, titled “PlayAnimationAction: construction, Init,
robot/game animation handlers, CheckIfDone, animationCompleted, destructor
streamer stop”. Its current evidence explicitly includes GetCompletionUnion
0x005441DE (0x0075C544, 0x006C22DC), plus the 20261012 play-animation report/check.
M8-015 is IMPLEMENTATION_GAP, “ActionList/ActionQueue/IAction lifecycle,
cancellation and Robot teardown”; its current evidence includes the adopted
OV18/OV56 lifecycle and deletion paths. Neither record is settled here.

The report mentions the carrier's existing M7-020 label. Current M7-020 is
IMPLEMENTATION_GAP, “Mood event production, repetition penalty and affector
application”, with MoodManager addresses 0x0067B85C/BDCA/C770/B724 and
EmotionAffector 0x00679910. That tag is not evidence for completion serialization;
retain the M8 ownership above when preparing any inventory/build packet.

## Remaining gaps

Union copy/move ownership, Prep cache lifetime, live game snapshot versus watcher
cache, compound forwarding, public API lifetime and complete concrete action
name producers are not independently checked here. No scalar-to-typed migration
is released by this partial review. Existing broader report remains pending;
no fidelity, verification, inventory or acceptance state changed.

## Follow-up: Prep versus game sampling (5f06c9ab)

Independent bounded PASS for sampling order, not typed ownership. Read native
0x00540750..0x005407C0, 0x00540AA8..0x00540B60 and Reset
0x00540CE8..0x00540D0E against current IActionRunner.cs and ActionWatcher.cs.

Prep tests +0x55 at 0756/075A. The first call invokes vtable+0x0C with output
runner+0x1C at 07AE..07B6, then stores prepared=1 at 07BA. The repeat branch
logs/cleans temporary diagnostics and does not invoke that getter. Managed Prep
at lines432..442 agrees in order, though its cache is still uint.

Game completion construction obtains subresults at 0x00540AD0 and invokes the
live virtual union getter at 0x00540AE4 into a separate temporary. It does not
read the Prep cache itself. BuildCompletedAction at ActionWatcher.cs227..235
preserves this ordering. Calls to record constructor/move helpers at AF4/AFC
and temporary cleanup at B04/B2A are visible, but deep-copy correctness is not
adopted without reviewing those helpers and variant operations.

Reset's direct stores affect +0x74, +0x70 and result+0x18; its conditional call
is UnlockTracks. It does not directly reset +0x55 or +0x1C. Managed Reset
likewise retains _prepped and _completionUnion. This bounded read does not
claim a complete alias-write census for every indirect callee.

Current M8-015 remains IMPLEMENTATION_GAP with the same title/evidence quoted
above (re-read at this revision); its adopted OV18 evidence includes
0x00540750..0x005407C0. No new native behavior correction found in these sampling
steps. A future typed migration must preserve first-Prep sample versus later
live game sample. Suggested independent unit oracle: a changing getter returns
A to first Prep, repeated Prep does not sample, game construction obtains B
only after subresults. This is a proposed unit check, not a test run or proof
of whole watcher delivery. Watcher/destructor cache selection, typed lifetime,
compound proxy and API delivery remain pending. No builder release.

## Follow-up: animation variant ownership (b7b32940)

Partial independent PASS for tag1 copy/move/clear and game record helpers.
Current M8-015/M8-020 titles, IMPLEMENTATION_GAP statuses and evidence quoted
above were re-read unchanged. No whole action lifecycle approval.

Read native75C1A8..C3E0,75C544..C5AE,540BAC..C1A and string-copy helper
4E02B2..0304. Raw TBB bytes select tag1 arms C1C8, C25A, C2F4, C396 for copy
ctor, move ctor, copy assignment and move assignment respectively. Other
variants are not adopted by this check.

Copy ctor/assignment duplicate short-string representation; long strings call
4E02B2 with source bytes/size. That helper allocates destination storage and
copies bytes at4E02E6..02FA, then terminates at0300. Copies therefore do not
retain a pointer into the old union's long string. Assignment self-check C2C0
precedes destination release. Move ctor/assignment transfer three string words,
zero the source string (C268/C26C; C3A4/C3A8), then invalidate source tagFF
(C294/C3D8). Move assignment self-check C34C likewise precedes release.
ClearCurrent frees only heap-backed tag1 data, then setsFF (C29C..C2B6).

The animation setter writes tag1 after transferring a different variant's
incoming string; existing tag1 uses C574, which clears old content, invokes
4E75EC and transfers/zeros the incoming representation. Allocator/shrink helper
4E75EC itself is NOT newly verified; do not claim exact allocator behavior or
all self-aliasing inputs from this read. Defined independent string-value
ownership is supported; managed immutable ownership can preserve that result.

Game-record constructor copies the union through540BCA; move assignment uses
540C14 after transferring subresult storage. Combined with the preceding
sampling check, the game report owns its animation payload independently of
the sampled action/cache. This does not certify watcher deque or compound map
copies; those call paths still need review. Current production remains uint,
so these are recovered requirements, not implemented fidelity. No tests run,
new builder release or manifest change. Remaining: constructor default variants,
watcher/deque lifetime, compound store/proxy, name producers and API delivery.

## Follow-up: watcher cache sample and deferred ownership (2e0a1114)

Partial independent PASS for the sampled value's lifetime, 2026-10-21.
Current M8-015 remains IMPLEMENTATION_GAP with title/evidence above; re-read
its adopted OV18 destructor/ActionEnding/deque evidence before this check.

Native base destructor installs IActionRunner vptr at54109A before calling
ActionEnding541238, and clears completion cache only later at541272. The base
vtable symbol102147C plus header8 and getter slotC contains52B0A7 (Thumb).
Getter52B0A6..B0AE copies runner+1C through veneer8CB09C: the ARM body loads
FFBDE5A0 and adds PC8CB0AC, targeting PLT4A964C (const union assignment).
Thus the destructor-time getter samples the prepared base cache, not the
already-destructed derived action string. This does not certify every earlier
destructor side effect or an unprepared action's default variant.

ActionEnding541BB4..1C04 captures tag/type/result, invokes union getter541BF0,
then obtains descendant results541BFA and pushes a const record541C04. Deque
push541FA0..1FF6 calls record-copy5437D6 at1FEA before incrementing size. That
record helper copies the union at5437F8. Combined with the independently
checked tag1 copy helper, deferred animation payload remains owned after the
action and temporary report are destroyed. Allocator/deque growth internals
are not an observable-layout requirement and were not newly extracted.

Current ActionWatcher.ActionEnding selects PreparedCompletionUnion for
ActionRunner, samples it before GetSubActionResults, and enqueues the record.
WatcherEnding calls it after stop/unlock work. These selected-value/order steps
agree, but uint still cannot carry the recovered name. This comparison does
NOT approve the entire watcher tree algorithm: current EnsureNode/attachment
occurs before the sample, while this native slice only reaches later tree work
after enqueue. Whether that earlier bookkeeping changes descendant results is
still outside this bounded payload check; retain the lifecycle review boundary.

No new correction, builder release, test run or fidelity change. Remaining
completion obligations: default variants, compound store/proxy, full concrete
name producers and public delivery, plus separate watcher-tree effects. The
whole report remains only partially reviewed.

## Follow-up: compound payload store and proxy selection (d2fa68f3)

Partial independent PASS for the payload/type capture and selector rules.
Current M8-017 is IMPLEMENTATION_GAP, “The compound action runtime:
ICompoundAction, CompoundActionSequential and CompoundActionParallel
(construction, AddAction naming, update/result tables, retry, completion store,
teardown), as used by the go-to-sleep sequence and other compounds”. Re-read
its OV20/21 evidence D1-D4 (54F050..F130,54F270..F3EA, related teardown).

Read native54F050..F0DC and54F20C..F3EE against CompoundActions.cs138..188.
Store invokes child union getterF072, independent copyF078, reads typeF086 and
current tagF08C, moves union into map node+14 atF0AE, writes type+3C atF0B4,
then Preps childF0C0. Proxy match updates parent typeF0CA..F0DA. This agrees
with current capture-before-Prep sequence; uint storage still omits the name.
Map insertion internals and subsequent delete/retain tail are not approved by
this bounded read; the existing external-owner retention MISSING remains.

SetCompletionUnionProxy stores tag/enabled then scans all childrenF228..F24E,
updating type on each match. A cached entry subsequently overrides typeF256..F264.
GetCompletionUnion instead invokes the first live matching child's getterF322
and exits after releasing its temporary shared referenceF32A..F334. With no
live match it searches the cacheF344 and copies node+14; absent cache warns
and copies base+1C atF3E4..F3EA. No proxy takes the same base fallback directly.
Getter has no parent-type store. Current setter/getter agree on these distinct
selection rules. Previously checked union copy/move requirements apply to the
captured value; a typed port must retain value and type independently.

No whole compound runtime, callback-reentry, retained-child destruction or
watcher-tree approval; no new defect in the inspected selector steps. No tests,
builder release or manifest changes. Remaining completion review: constructor
default variants, concrete name/retry producers, public delivery and the
separate lifecycle effects already named. Whole report remains partial.
