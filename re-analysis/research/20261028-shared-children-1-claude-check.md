# SHARED-CHILDREN-1 — Claude check

Manager, 2026-10-28. Handoff `jobs/trial-m13/HANDOFF-SHARED-CHILDREN-1.md` (branch `codex-m13-manager-2` 9200f76e;
implementation eab6e59b). Verdict: **PASS. Settled: M13-027 and M15-012, both EXACT_SOURCE.**

Records re-read in the current manifest before acting: M13-027 "TurnTowardsFaceWrapperAction" (IMPLEMENTATION_GAP,
evidence 0x0054C7DC..0x0054C8FC), M15-012 "Put-down image wait and CantHandleTallStack animation trigger"
(IMPLEMENTATION_GAP, evidence 0x005C8174 and 0x005ED0F0), M14-006 (BLOCKED_EXTERNAL, unchanged).

## Checked against the binary

- **Wrapper ctor 0x0054C7DC..0x0054C8CE.** CompoundActionSequential base, wrapper vtable. `cmp r7(A),#1` at
  0x0054C7F0: turn-before only when A==1, a 0x1D8 TurnTowardsFaceAction (face 0, Radians copied twice, bool C at
  [sp]) with vtable overwritten, added via ICompoundAction::AddAction(action,false,false). Wrapped action added via the
  vtable AddAction (+0x20) with (false,false). `cmp r7(B),#1` at 0x0054C86E: turn-after built the same way.
  SetProxyTag([wrapped+0x60]) at 0x0054C8CA. C# `NativeTurnTowardsFaceWrapperRunner` matches, order and flags.
- **Completion proxy 0x0054F27A..0x0054F3EA.** With the proxy flag (+0x98), a live child whose tag equals the proxy
  tag (+0x94) writes its whole completion union through its vtable (+0xC); otherwise the cached CompletionData for the
  tag (map at +0x7C) is used. The C# cache and live lookup now carry the animation name and variant as well, so the
  whole union passes through.
- **Put-down caller 0x005C8264..0x005C82CC.** Keep-alive TriggerAnimationAction(0x199, loops 1, interrupt true,
  tracks 0, 60.0f, false), wrapped with A=1, B=0, Radians PI (0x40490FDB), C=0. `NativeFaceBeforeAnimationRunner`
  passes exactly these. The look itself (gate 0x005C818C, head 0xBEB2B8C2/0x3D0EFA35 at 15/20, drive -30,
  WaitForImages(2, mode 1)) was checked in `20261027-putdown-block-1-claude-check.md`; the wait is now a sequence
  child after the parallel.
- **TransitionToDisapointment 0x005ED0F0..0x005ED184.** SetDebugStateName (25 chars), then a 0xCC
  TriggerAnimationAction(trigger 0x1B, 1, true, 0, 60.0f, false) and StartActing with an empty std::function. 0x1B is
  ordinal 27 of the generated AnimationTrigger enum, `CantHandleTallStack`. C# `TransitionToDisappointment` passes
  the same arguments and a null callback.
- **Update 0x005BDA56** (vtable slot 0x01029FEC): returns 1 (Running) when +0x84 is non-zero, else 2 (Complete).
  C# `KeepsRunningWithoutAction` in the disappointment phase is `HasCurrentAction`.

## Settled, each on its own path

- **M13-027 EXACT_SOURCE.** Own claim: construction, flags and completion proxy. Location moved to
  `NativeLastFaceRunner.cs` (tagged). The turn's contents stay with M13-014; its named speech stays with M14-006.
- **M15-012 EXACT_SOURCE.** Own claim: the look composition and the tall-stack disappointment trigger. The two items
  its old `unresolved` left open in this path are closed (the keep-alive wrapper is built; the image wait starts after
  the parallel). The failed-look outcome is M15-075's release callback. Children internals stay with their records.

## Not settled

M13-014 (turn contents), M14-006 (speech, blocked), M14-008, and the pickup-approach work this branch also carries
(`PICKUP-APPROACH-1` reading only; no code claim).
