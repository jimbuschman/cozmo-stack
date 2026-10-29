# EXPRESS-NEEDS-1: Claude check (manager, 2026-10-29)

**Job.** A Claude-run job:
1. cozmo-extractor read `research/20261029-express-needs-1-read.md`, and the manager checked it against the .so;
2. cozmo-implementer built it;
3. cozmo-verifier gave a PASS with 0 blocking findings and 4 queued.

**Verdict: PASS, merged. Nothing settled.**

## What the build does now (ExpressNeedsBehavior, ExplorerBehaviors.cs)
- **R1 gates.** Not runnable off treads, with robot+0x34A, or while carrying (+0x20/+0x24 base 0, EN +0x28
  0x005EE860 returns 0).
- **R4.** With +0x135 and Critical, runnable only when the need is the selected severe need, and IsNeedAtBracket is
  skipped. Otherwise IsNeedAtBracket.
- **R5/R6.** Runnable iff `EvaluateY(level) + last < BaseStationTimer seconds`, as an f32 strict `<`. +0x138 starts at
  0.0f.
- **I1–I4.** One CompoundActionSequential:
  - the TurnTowardsLastFacePose turn (face 0, pi, no name), ignoreFailure, always added;
  - then each trigger as a plain TriggerAnimationAction(1, true, 0, 60.0f, false), not ignored.
  - The invented HasAnyFaces gate, the per-trigger chain, the `using` disposal and the stack-only log are removed.
- **I5.** +0x138 is stamped only when the result category byte is 0 (SUCCESS).
- **P1.** Resume is refused.
- **S1.** Stop clears the severe-need expression when shouldClearExpressedState is set (unchanged).
- **U1.** The behaviour ends when its action ends.

## Checked by Claude
Against the .so, before the build:
- IsRunnableInternal 0x005EE570..0x005EE5DC;
- InitInternal 0x005EE690..0x005EE788;
- Resume/Stop 0x005EE7D4..0x005EE7E8;
- slot +0x28 0x005EE860;
- the callback 0x005EE89E..0x005EE8B6.

The verifier independently re-opened each citation and the vtable 0x0102A24C. In the diff, the SUCCESS test
`(Result >> 24) == 0` matches `ldrb r1,[r1,#3]` at 0x005EE89E. Production (FreeplayStack.cs:129) passes the
ManipulationSystem to the loader, so the compound runs through the engine ActionList.

## Manager rulings on the builder's choices
- **m == null:** Init fails with a visible MISSING. This is a host-configuration gap; production always supplies m.
- **R2 stays inside IsRunnableInternal:** this loader path does not bind the base's native inputs. It has no
  observable effect today (the verifier's item 4 is queued for a comment fix).

## Queued
1. **No test enters the live Freeplay interlude chooser** (Activities.cs:357-368). All the new tests are labelled
   unit. **This blocks settling any EN row or M15-072** until a production-reach test exists.
2. **Container gap.** Factory row C1 (BehaviorContainer.cs:221) is not built in the native container
   (NativeBehaviorRuntime reports NotImplemented). The live EN path is the standalone FreeplayStack loader.
3. **Completion owner.** It is bound only in the constructor. With no robot at construction, a run would stay open
   until it is interrupted (FreeplayTool always connects first). It is also never released.
4. **Comment accuracy.**
   - The R2 "order is immaterial" comment: natively R2 is skipped while the behaviour is running.
   - The base's "+0x74 not tested" message misstates EN.

## Open from the read
- A3: ForcedGetOut reachability is UNKNOWN.
- C3: the ReadFromJson +0x74 store was not re-disassembled.
- The turn child with face 0 (M13-014), and the compound result after an ignored turn failure (M8-017).

**No manifest record owns the EN lifecycle.** A lifecycle record is proposed to the operator: ctor/LoadConfig C2–C8,
gates R1/R4/R5/R6, Init I1–I4, the callback I5, Resume P1 and Stop S1.
