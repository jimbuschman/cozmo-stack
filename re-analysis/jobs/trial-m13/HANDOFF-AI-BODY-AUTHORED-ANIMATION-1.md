# AI-BODY-AUTHORED-ANIMATION-1

## Source path

- The external PlayAnimationTrigger request carries uint32 loops and trigger followed by the safe-lift, ignore-body, ignore-head and ignore-lift bytes (0x706A5A–0x706B8E). Nested tag 0x12 and direct tag 0x18 converge at the factory (0x525EA4–0x525ECC, 0x52A7DE–0x52A850). It constructs the ordinary or lift-safe runner with interrupt true, timeout 60 seconds and strict cooldown false. Ignore flags supply body 4, head 1 and lift 2. The lift-safe constructor adds lift when the robot carries on treads (0x544714–0x544732).
- The host request selects the nested queue's explicit AtEnd position, external tag and zero retries (0x52796C–0x527A8C). The original app's default is Now. Track admission and acquisition use the retained action runner (0x540440, 0x540572–0x54058E). Loop count retains the unsigned word. A zero count selects the factory timeout's FLT_MAX variant (0x543C96–0x543CB2).
- Trigger mapping and live group selection resolve the actual authored clip (0x54432C, 0x54443C). Play caches the chosen animation at the streaming boundary and uses the stream tag returned by the scheduler (0x543F20–0x543FAE). The audio-backed path uses the existing shared renderer. An audio-free clip has no audio recipient to acquire. The ordinary trigger does not provide speech samples.
- Firmware start and end callbacks match the retained tag, with end ignored before start. Finite loops count down, and zero loops await cancellation or another terminal effect (0x5452FA–0x545348, 0x545422–0x5454F4). D5 emits the matching animation event only after start (0x54561C–0x545750). The full-width 0x5F abort tag can fail the action independently of start (0x5457D8–0x545862). Completion reports the selected animation as variant 1 (0x5441C0–0x5441DE, 0x75C544, 0x75C90E–0x75C916).
- Cancellation targets the admitted runner and returns after unlink. The native destructor compares the cached animation identity with the current stream and can clear the shared stream on an identity match, including null equality (0x543DB2–0x543E26). Its subscriptions retire in reverse order (0x543E2A–0x543E4E). The host's asynchronous audio opening keeps the selection separate from the playback cache until it can attempt the original streaming operation.

## Files and tests

- `Motion.cs` creates the exact authored runner and uses the existing tagged queue completion. `TriggerLiftSafeAnimationAction.cs` carries the unsigned loops and scoped clip selection through firmware callbacks and terminal name. `CozmoAnimations.cs` and `AnimationScheduler.cs` carry the original owner into audio-free stream start and identity-based teardown.
- `CannedSpeechOwner.cs` distinguishes a still-active conflicting lease from a retiring predecessor. The public adapter waits asynchronously for the latter with a two-second host limit. It reports an initialization failure if that retirement fails to complete.
- `AiAuthoredAnimationTests.cs` reaches public AtEnd admission, pending and active cancellation, actual track acquisition and refusal, safe-lift carry and treads, high-bit loops, mismatched and early firmware events, raw 0x60 and 0x5A, and replacement during streaming callbacks. It also checks the no-audio route and refusal of a speech-source clip without a provider.
- Two consecutive stock F7 actions reach the shared native renderer and real 744-byte audio blocks. An occupied live lease is reported and leaves its same-clip stream in place. The affected run passed 177/177. Final rebuilt focused tests passed 11/11. The manager rerun passed 46/46 across authored animation, pickup voice, cube observation, shutdown and fidelity.

## MISSING

- Immediate native interruption of a concurrently active, exclusive shared-audio generation has no proven host handoff. The bounded public route reports that gap rather than waiting on a live incumbent. A retiring predecessor is a separate condition.
- Provider-dependent clips require a separate source-backed speech action. Other audio recipient and platform behaviors remain under their existing records.
- Physical playback and firmware timing require operator evidence.

## Workers

Reader requested gpt-6.1-sol at medium effort. Builder and reviewer used the configured default request. Runtime model identity was not observed. Manager review corrected scheduler owner reentry and distinguished retiring audio from a live lease. The builder corrected two interim fixture expectations for the lift-safe subclass and the airborne queued action state. The reader recovered the direct and nested factory and identified the audio handoff and cached-clip boundary.

## Records

- M8-020 and M8-021 supply the retained animation callback and trigger paths. This job adds the public factory/queue entry and source-ordered playback for the checked route. The active shared-audio handoff, other trigger modes, common callbacks and broader DAS paths remain open.
- M8-015 and M8-022 supply the retained action state and list. The host returns the exact admitted action result and name under the selected external-tag queue policy. Other action families stay separate.
- M5-011 and M5-034 own group selection and trigger mapping. M5-008, M5-023 and M5-036 own broader streamer and firmware behavior. M6 and M14-006 continue to own their audio and speech boundaries.
