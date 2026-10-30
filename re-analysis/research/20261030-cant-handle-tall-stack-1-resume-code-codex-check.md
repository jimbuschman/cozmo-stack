# CANT-HANDLE-TALL-STACK-1 resumed independent code check — draft 1

2026-10-30. Reviewer requested configured default model and effort (omitted); actual backend unobserved. Independent read-only verifier. Historical source check remains unchanged. The corrected M7 rows 856/857 are approved and REVIEW releases the build. No .NET run or hardware run by this reviewer.

## Scope and immutable input

Reviewed `.scratch/tall-stack-build-draft1` in the builder worktree: BehaviorContainer.cs, NativeBehaviorRuntime.cs, DelocalizationRecipients.cs, NativeCantHandleTallStackBehavior.cs, CantHandleTallStackNativeTests.cs, and archived builder report. SHA-256 manifest matches all six archive files; at review, all five live code/test files matched, while the live builder report differed. This is an uncompiled draft, not a final gate. Supplied target and recipient seams are not native caller, owner-join or weak-expiry evidence.

## Verdict: FAIL, reviewer correction batch 1

1. ClearStack marks TargetKnown false when it sets the weak target null. Native 0x005ED1E4..F8 explicitly clears the pair; Init 0x005ECE42..62,84/88 refuses a null weak target. A delocalization between admitted readiness and Init should therefore be a known null refusal, not an unavailable-input result. Check also a constructed supplied owner before first refresh. Builder independently identified and changed the first case after freezing; those edits are not part of draft 1.
2. FiveRealChildrenFailureStillStartsDisappointmentAndDelocalizeDoesNotCancel forces the root sequence State. It verifies completion dispatch after a supplied terminal root state, but does not prove a child failure causes compound completion, or that waits begin after head children complete. Drive actual children and completion, preserving the separately labelled terminal-root seam. Exercise Resume and changed/unavailable owner/lock at both actual entry routes; bottom missing, byte/default config, all three gates, action-presence completion and suppression across target replacement need meaningful coverage against the approved read.

## Narrow open question

Native 0x005ECEFE..005ECF30 and 0x005ECF58..8A supply a third Radians argument of zero. The candidate CreateNativeHeadChild passes BuildHeadAction(angle,speed,accel,0,null,tolerance). The constructor/default mapping has not been verified against native instructions; this is not a finding until resolved. `known.py MoveHeadToAngleAction` was attempted before any new native tracing but local python launcher returned access denied. Manager asked for a narrow source check.

## Existing record boundaries

Current M15-012 title: "Put-down image wait and CantHandleTallStack animation trigger"; EXACT_SOURCE; evidence includes BehaviorPutDownBlock::CreateLookAfterPlaceAction 0x005C8174 and BehaviorCantHandleTallStack::TransitionToDisapointment 0x005ED0F0. It claims the disappointment trigger and action-presence update, not the earlier looking phases. M11-072 title: "RobotDelocalized subscriber list and order: the Subscribe(tag 0x32) sites, and the reachability of ReactionTriggerStrategyCubeMoved::HandleRobotDelocalized"; IMPLEMENTATION_GAP; evidence includes the existing subscriber census and class 0x005BF268, 0x005ED240..4E, 0x005ED1E4..F8. Its unresolved retains weak expiry/producer semantics and subscription retirement. No closure is proposed. M7-019 title: "M7 concrete reaction-behaviour state machines"; IMPLEMENTATION_GAP; its current evidence identifies reaction rows and specific reaction-class paths; its unresolved describes the partial legacy recipient backing. No closure is proposed. Mechanically matched current complete snapshots are in the checked source read and manager freeze.

## Affected reference plan

Archive exact build/test identities and immutable input bytes. Concrete candidate classes: CantHandleTallStackNativeTests; M11SmallReadsBuilderTests (both recipient owners and Freeplay facade); SharedChildrenNativeTests (settled trigger); NavigationTests (legacy behavior); CompoundActionTests and BehaviorFrameworkTests (compound/lifecycle); PickupPyramidNativeExecutionTests (configured fixture); CoreReviewTests and NativeBouncerFactoryTests (factory boundary); SparksGatherNativeTests (unavailable chooser). Final reference/replay must use exactly the same compiled names, parameters, outcomes and multiplicity, under manager's exclusive lease. No test results claimed here.

## Nonblocking cleanup

The archived builder report predates the implementation and states no implementation existed; update the final report. Add narrowly applicable existing M15-012 and M11-072 fidelity tags without adding or settling a record. Neither wording nor tag cleanup substitutes for the behavioral and testing corrections.

## Correction batch 1 — static affected-diff PASS, test results pending

The builder's separate self-correction preceded reviewer batch 1; both archives remain. Correction1 `hashes.json`: all five live code/test files and the correction report match their archived SHA-256. The constructor initializes a known empty weak pair and ClearStack returns to known empty. Tests now distinguish the forced root-terminal seam from child-failure dispatch, and exercise head/wait sequence progression, input boundaries, and pose suppression. These tests are not yet compiled or run; static review is not a test pass. No additional blocking finding in affected diff.

The narrow head-argument question is closed: independently checked retained native instruction dump: 0x00547F08..10 copies third Radians into +88; 0x00547F14..28 sets 15/20/0 in +90/+94/+98; 0x005480BC..C2 bypasses RNG for zero; 0x0054855E..70 passes those defaults. CreateNativeHeadChild(angle,tolerance,15,20) is supported for this zero-variability caller. This does not settle the shared action's whole path.

Correction1-LF transport verified: the five code/test files and builder correction report match the LF archive hashes and live bytes; each matches the prior correction1 bytes after CRLF-to-LF conversion only. Static verdict remains PASS, pending compiled reference and independent replay. Counts at this point: reviewer correction batch 1, separate builder self-correction 1, compiler corrections 0, test corrections 0.
