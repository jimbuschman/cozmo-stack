# M8 animation completion — partial independent check (2026-10-21)

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
