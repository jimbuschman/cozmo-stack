# Opus check of R-M8-PLAY (2026-10-12)

- **Report:** `research/20261012-m8-play-animation-actions.md` (4c6e6d56).
- **Result:** 109 behaviour-changing rows reopened in `libcozmoEngine.so`; 109 CONFIRMED, 0 WRONG, 0 UNVERIFIABLE.
- **Adopted:** by the manager on 2026-10-12. The report plus this addendum are the checked rows.

## Additions to the report

1. **L20, the ActionCompleted info at 0x00540600, is gated on +0x57:** `ldrb +0x57; cbz` at 0x005405A4..0x005405A8.
   L17 and L18 have the same gate.
2. **SetStreamingAnimation with a null animation** (0x0057B248..0x0057B304).
   - If an old stream existed, the null tail stores +0x73 = 1 (0x0057B2FC..0x0057B300).
   - A null with an idle streamer (+0x38 == 0) still calls Abort() (0x0057B1DE → 0x0057B242).
   - So the destructor's null == null case, and a lookup miss while idle, both reach Abort(). Every PlayAnimationAction
     that is destroyed without ever being Init'd, while the streamer is idle, triggers it. That effect belongs to
     M5-008/M5-023.
3. **DeleteActionAndIter has a guard set** at queue+0x10 (0x0053F9FE..0x0053FA1C, 0x0053FABE). It inserts the
   action's id, skips everything if the id was already present, and erases it at the end.
4. **Compound vtable +0x20 is AddAction(IActionRunner*, bool, bool)** (0x0054EC7C). It builds an empty predicate, then
   forwards to +0x1C, AddAction(function, bool) (0x0054ED90). That callee:
   - copies the parent's +0x57 into the child (0x0054EE10..0x0054EE14);
   - appends the child's name to the compound's name (0x0054EE2C..0x0054EE54).
5. **The trailing boolean of AddAction is never loaded** (sp+0x50 in 0x0054ED90), so it has no effect in this build.
   Its native name (ShouldEmitCompletionSignal?) stays UNKNOWN.

## Calls the report missed (base runtime, M8-015)

- **IActionRunner::Update:** ActionWatcher::ActionStartUpdating (0x00540386); ActionEndUpdating (0x0054057E on the
  TRACKS_LOCKED exit, and 0x00540630).
- **IAction::UpdateInternal's status string at +0x0C:** the name copy (0x00540D46), then name + ": check
  preconditions" (0x00540F14/0x00540F1C, string 0x0054101C), or name + ": check if done" (0x00540DD6/0x00540DDE,
  string 0x00541068).

## Correction

M8-007's evidence says the action "stays queued and is tried again". That is contradicted by 0x00540572..0x00540582
and 0x005405A4: TRACKS_LOCKED (0x03000019) goes straight to completion, with no wait and no retry.

## Still UNKNOWN

- the native name of the trailing boolean;
- the C++ default arguments of both constructors;
- where the DAS blacklist config comes from, and its contents;
- compound child execution and failure propagation (M8-017);
- what Abort() does on an idle streamer (M5-023);
- who owns CreateCustomSongAnimation;
- the delivery internals of the signal container;
- whether M1-023/M1-046/M1-050 own the subscription mechanics (0x00519F8C robot-id filter; external-interface vtable
  +0x28 and +0x1C). The manager checks this.
