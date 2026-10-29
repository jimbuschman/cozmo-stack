# ACKNOWLEDGE-FACE-1 — Claude check (manager, 2026-10-29)

**Verdict: PASS. Nothing settled. One new lifecycle record proposed (operator).**

This is the first job Claude ran end to end after Codex ran out of usage. The read was done by cozmo-extractor
(`research/20261029-acknowledge-face-1-read.md`). The B2 cost inputs were read by Claude. cozmo-implementer built it and
cozmo-verifier checked it (PASS, 0 blocking, 7 queued).

## Built

AcknowledgeFace now follows the engine:
- Init only sets begin-pending. The first Update starts BeginIteration.
- **BeginIteration:**
  - picks the face with GetBestFaceToTrack(set, false);
  - builds TurnTowardsFaceAction(id, π, sayName always 1) with 3 images to wait (the ctor default stays 10 for other
    callers);
  - applies the greeting gate;
  - sets the AcknowledgeFaceNamed / AcknowledgeFaceUnnamed triggers.
- **FinishIteration:** erase the face, notify the listeners, objective ReactedAcknowledgedFace (0x1A), next face. This
  runs on success and failure alike.
- **Runnability:** never runnable while carrying, on the charger or with robot+0x355 set; otherwise runnable iff the
  target set is non-empty.
- **Stop:** clears the strategy's desired faces. The greeting flag is never reset.

The old stack inventions are removed: lowest-id choice, one face per run, the greeting tied to sayName, the per-face
re-arm and the FaceDeleted handling.

## Checked by Claude

- **BeginIteration 0x00602950..0x00602AA6:**
  - GetBestFaceToTrack(+0x120, false); 0 → return;
  - action 0x1D8 with Radians 0x40490FDB and sayName 1;
  - greet = (now − [[robot+0x44]+0x54] ≤ 60.0f) && t ≥ 0 && !HasTurnedTowardsFace && !+0x12D;
  - greet → SetSayNameTriggerCallback, else SetSayNameAnimationTrigger(1); SetNoNameAnimationTrigger(2) always;
  - +0x188 = 3; StartActing with FinishIteration.
- **GetBestFaceToTrack 0x0056C110..0x0056C27E:**
  - empty → 0; size 1 → that id;
  - best starts at FLT_MAX with a strict `<`; the return starts at 0;
  - GetFace null or GetWithRespectTo false → skip;
  - cost = |Radians(head) − robot+0x2FC| + 3·|body| + (name empty ? (bool ? 1000 : 0) : 0), all f32.
- **GetRelativeBodyAngleToLookAtPose 0x0054B584:** Radians(atan2f(y, x)). The head angle is the existing M13-026 helper.

The verifier re-read the ctor, Init, Update, Stop, FinishIteration, the vtable 0x0102B834 slots, the strategy
callbacks and the greeting lambda 0x00602D8C, and recomputed the test literals independently.

## Open

- **B5 UNKNOWN:** no writer of BehaviorManager+0x54 is known besides the ctor's −1.0f. The stack keeps −1 and reports
  MISSING, so the greeting never plays. If the engine has no other writer, that is exact.
- **Not built:** the T5 forced trigger (no stack entry reaches it).
- **libm:** the atan2f body angle falls under the libm policy, so B2 can be at most EQUIVALENT_IMPLEMENTATION.
- **Inherited and unsettled:** TurnTowardsFaceAction (M13-014), text to speech (M14-006 BLOCKED_EXTERNAL), the strategy
  (M10-003) and the lifecycle base (M8-001).
- **Queued, non-blocking:**
  - Q2: a disposed turn can still see a late face callback (no robot effect);
  - Q4: the OBB-gated production test returns silently without assets;
  - Q5: the shared EmotionEvent time base;
  - Q7: T5 is undocumented in code.
  The Q1 log channel ("Behaviors") is fixed in this commit.
- **M14-002** (EXACT_SOURCE) says TurnTowardsFaceAction waits ten frames. That holds for the ctor default only, since
  AcknowledgeFace writes 3. Correcting the record needs the operator.
