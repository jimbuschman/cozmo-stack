# BOUNCER-1: Claude check (manager, 2026-10-29)

Branch `codex-m13-manager-2` at 4484c679 (build 26eb9265 plus test-only fa6df082). Read:
`research/20261029-bouncer-1-read.md`, with the eight checked supplements.

**Verdict: PASS, merged. Nothing settled.**

## Checked by Claude against the .so
IsRunnableInternal 0x005F133C..0x005F139E:
- IsFeatureEnabled(8) must be 1;
- the eligible-face set;
- AIWhiteboard::GetBestFaceToTrack(set, true);
- FaceWorld::GetSmartFaceID, assigned into +0x120;
- runnable iff SmartFaceID::IsValid.

UpdateInternal 0x005F19A4..0x005F1A5C:
- the f32 duration compare;
- the warnings for an invalid, missing or too-old face;
- the image-age cutoff GetLastImageTimeStamp − observed ≥ 0x1389 (5001).

The stack's NativeBouncerBehavior follows both in order: the 60 s timeout, then invalid, then missing, then
`imageTime − lastObserved >= 5001u`. It also has:
- all three gates refusing;
- the Bouncer reaction locks and the Count idle at Init;
- the trigger and display actions through the ActionList;
- an empty native Stop.

The shared changes (BehaviorManager, IBehavior, AnimationScheduler, TriggerLiftSafeAnimationAction, Faces,
VisionSystem) were reviewed by Codex in 6 correction batches. Codex's independent replay passed 969/969 and the full
gate passed 6795/6795. Not blocking.

## Queued
1. **More than one eligible face:** the stack refuses visibly ("M14-001/M11-066 multi-face ranking"), where the
   engine ranks the faces with GetBestFaceToTrack. That ranking is already built for AcknowledgeFace (M15-080,
   0x0056C110). Reuse it once the face-pose inputs are accepted.
2. The shipped Bouncer feature is disabled, so the live path is gated off by configuration. There is no host
   feature-toggle dispatch.
3. Codex's named MISSING boundaries stay as recorded in HANDOFF-BOUNCER-1.md:
   - detector DC production (M11-016/M14-010);
   - the multi-face geometry;
   - a historical origin;
   - the shared-allocation callback;
   - the M8-020 union/destructor gaps.

## Record
Codex proposes one Bouncer own-path lifecycle record. **This needs the operator's OK.** It would cover the configured
lifecycle, the state and game arithmetic, and the image-construction and raster steps, without duplicating the shared
face, RLE, idle or action records.
