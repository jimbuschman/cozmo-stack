# Request: M8, PlayAnimationAction and TriggerAnimationAction

- **Answer file:** `re-analysis/research/20261012-m8-play-animation-actions.md`.
- **Read first:**
  - `research/20261012-m5-x9-extraction.md` section 4g;
  - the current manifest records M5-038 and M8-017;
  - `research/20261011-m5-settling-pass.md`, row M5-038.
- **Rules:**
  - research only;
  - coverage table first;
  - no fidelity claims;
  - nothing leaves this machine.
- **Why:** M5-038 (the go-to-sleep compound) can't settle until the concrete actions it queues are recovered. No
  record covers them yet.

## Task

Trace every behaviour-changing step, with an address or UNKNOWN, of the functions below in
`resources/lib/armeabi-v7a/libcozmoEngine.so`. For each log call give the key, level, channel, format, arguments and
the error tail. For each message, give the tag and fields.

1. **The `PlayAnimationAction` constructor** 0x00543C08: every field it stores, including +0x84 loops, +0x8C interrupt
   and +0x8A failed, with defaults.
2. **`PlayAnimationAction::Init`** 0x00543F20:
   - the `cozmo_sings_custom` → `anim_cozmosings_120_song_01` remap;
   - the animation lookup and its failure path;
   - the `SetStreamingAnimation` arguments;
   - what a returned tag of 0 does;
   - the R2E 0xCA/0xCB/0xD5 subscriptions and the external-interface handler: what each callback does to the action's
     state;
   - the return codes.
3. **`CheckIfDone`** 0x005441C0 and **`GetCompletionUnion`** 0x005441DE: the result codes and the AnimationCompleted
   payload.
4. **`~PlayAnimationAction`** 0x00543DAC: the info at 0x00543DF2, and when `SetStreamingAnimation` is called
   (0x00543E26).
5. **The `TriggerAnimationAction` constructor** 0x00544228, **`SetAnimGroupFromTrigger`** 0x0054432C (the
   Has/GetAnimationForTrigger calls and the warning at 0x005443AC), and **`Init`** 0x0054443C:
   - `GetAnimationNameFromGroup`;
   - the sEvent at 0x005445BC;
   - the hand-off to PlayAnimationAction::Init.
6. **The `IAction` / `IActionRunner` members these actions use:**
   - the tracks they lock (which tracks, and when);
   - the action name and type;
   - the timeout;
   - the `ShouldEmitCompletionSignal` behaviour.

   Name the vtable slots you read.
7. **The go-to-sleep factory's use of these actions** (CreateGoToSleepAnimSequence 0x0052CEA2..0x0052CFBE): which
   triggers and which loop counts it passes.

## Report

- The coverage table: each step with its address, or UNKNOWN.
- The proposed record split: what belongs to M8 (the action runtime) and what to M5 (the streamer interface).
- What stays UNKNOWN.
