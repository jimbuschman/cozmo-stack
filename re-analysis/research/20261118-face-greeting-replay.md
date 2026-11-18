# FACE-GREETING-REPLAY-1

## Input and historical boundary

Use the operator-authorized available copy of
20261117-052405-AI-COMMANDS-HW-1-bdcbda. messages.jsonl SHA256 is
C67BA13DEC4EAE4538958E12CAD294883F7EB0C441A8A829E4DCD29BDB33AD63.
The test's 247000..255800 ms window, captured calibration, and reassembled
successful album/enrollment writes are unchanged. Loading that later saved
album before this window is an explicit counterfactual starting state. The
continuation repeats the actual recognized pixels with fresh timestamps and
supplies motor and matching animation ACKs. No detector result, score,
recognition identity, speech samples, or action success is injected.

The replay first exists at 5d640ba3 (2026-11-17). An isolated, unmodified checkout
of that commit, with the same recordings and shipped resources, fails 1/1 in
1.3048 minutes. Current a3625bba also fails before this fix. Thus there is no
reproduced passing private-case revision with which to seed a valid later
regression bisect; marking the introducing commit good would contradict the
actual run. Its older handoff reports a passing run, but that claim is not
reproducible on this host and is not used as a false good endpoint.

Source history identifies a439506d (2026-11-07, Bound emulator calls and contain
native runtime failures) as the introduction of the per-basic-block deadline
callback. It predates this recorded test. The evidence below establishes that
callback's causal overhead at the current revision, not an invented first-bad
private-case bisect result. The historical checkout is ignored scratch only.

## Production route and failing step

Engine libcozmoEngine.so 3.4.0-1204 SHA256:
02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1.
The unchanged recovered route is also tabulated in
HANDOFF-FACE-RECOGNITION-E2E-1.md. This investigation spot-reads the timeout and
child ownership that distinguish this failure, without re-extracting the
unchanged DT/PT/FR implementation.

| Input to visible result | Primary evidence | Recorded/replayed signal |
| --- | --- | --- |
| Calibration and album initial state | LoadFaceAlbumFromRobot 0x006512A0; SetSerializedData 0x00867F54 | Existing real NV assembly/header callback installs the captured data. |
| F0/F2/F4/D1 ingress and camera admission | HandleImageChunk 0x00535A7A; processor 0x0065204A..0x00652200; HistRobotState 0x0053044C | Recorded image payloads enter the normal parser/history/vision workers. |
| Native recognition and identity delivery | tracker 0x0086D740; match 0x008640E4; change drain before observations 0x006551E8..0x00655274 | Thirteen face reports; negative track changes to named ID 1. |
| Face reaction and turn/verification | FacePositionUpdated 0x0060CB84..0x0060CE20; AcknowledgeFace override of image count to 3 at 0x00602A94..0x00602A98; TurnTowardsFace Init 0x0054BD7C..0x0054BE2E | TurnInPlace and TurnTowardsPose complete SUCCESS; the named SayText child is constructed. Pose/head/body/observation verification is not the stalled stage. |
| Named speech child | 0x0054C596..0x0054C5D6 creates SayText(name,intent 3) and selected authored trigger; state 3 updates it at 0x0054C606 | SayText input is Test Robin 30c00a; observed style 1, duration scalar 1.1. |
| Asynchronous speech readiness | CreateSpeech dispatch 0x0069E7C2; GetOperationState 0x0069E944..0x0069E978; SayText Init 0x00561488..0x005614B8, pending return 0x005615DE | Worker is still preparing samples when the parent expires. |
| Parent timeout | LastFace vptr table 0x010204FC, slot +0x2C at 0x01020528 points to Thumb 0x0052B0C3; body 0x0052B0C2 returns f32 0x41F00000 (30) | IAction.Update.TimedOut / TIMEOUT, not a pose or motor failure. |
| Completion | Native state-3 child update 0x0054C606, final SetTurnedTowardsFace/SUCCESS 0x0054C622..0x0054C63C | Fixed replay requires speech SUCCESS, parent completion, no owned runner, TurnedTowards=true and ReactedAcknowledgedFace objective. |

Native IAction::UpdateInternal reads the clock at 0x00540D4A..0x00540D4E,
stamps a negative start at 0x00540D64, calls timeout slot +0x2C at
0x00540D94..0x00540D98 and compares f32 now with start+timeout at
0x00540D9E..0x00540DA2. TurnTowardsFace::SetAction 0x0054B934..0x0054B968 preps,
replaces/deletes the old child and sets the child's suppress-track-lock flag;
it does NOT reset this parent timer. The stack already matches those rules.

The observed failing native speech operation started at test-wall 9501 ms,
engine time 8.280 s. It became ready at wall 40839 ms / engine 39.558 s:
31339 ms preparation, 128736 samples. The parent timed out first. Its teardown
cleared the operation, so the later PCM could not rescue the action. A different
full-suite attempt got as far as starting AcknowledgeFaceNamed, but the same
parent deadline aborted it before normal completion. Changing the 30-second
value or ignoring that failure would contradict the engine.

## Controlled measurement

The shipped Android English Ryan profile is speed 50, shaping fallback 100;
Name_Normal scalar 1.1 gives rate 45 through native rate computation
0x0069F870..0x0069F8D0 / cast 0x006A0442. The direct guest probe uses that same
text/rate, shaping 100, pitch 100 and Java glue. With the old block callback:
23129 ms synthesis; with only that callback disabled for the measurement:
18208 ms. Both produce exactly 128736 s16 samples at 22050 Hz (5838.367 ms),
SHA256 A44DF02968C651A40C5E0815D904A23EF3A9778E1E7088C01A2C0FFE3861E76C.
Voice load was 1714 / 1523 ms respectively. These are host measurements, not
native-phone timing requirements. Preparation in the integrated replay also
includes concurrent vision work, queueing, and any cold provider work.

The same complete replay passes 1/1 when the per-block callback is removed in a
controlled experiment. The temporary environment switch and reflection-based
synthesis observer were removed before the final implementation/tests. The
private replay test itself remains unchanged.

## Correction and safety

ArmMachine previously crossed into managed code on EVERY guest translated
basic block, resolving the weak owner handle, testing its fault latch and
sampling Stopwatch every 64 blocks. That is unnecessary PC error machinery,
not engine computation. The normal guest code, imports, registers, instruction
budgets, audio DSP, engine clocks and engine timeout constants are unchanged.

The replacement arms a weak-owner timer for the outer call's existing deadline.
Nested calls share that same deadline. CallTimeout still defaults to five
minutes; the engine action still has its separate 30-second limit. The timer
does not run before expiry;
then it requests uc_emu_stop, repeating at 1 ms until the owner leaves the call
so a stop immediately before emu_start cannot be lost. The owner still performs
all pending-exception, fault-latch and deadline checks before/after execution
and around imports/address/observation callbacks. Only the owner reads guest
registers and creates the M1-034 exception. No faulted machine resumes.

One gate serializes arming, polling, disarming and native handle close. A queued
old callback rechecks the CURRENT deadline under that gate and cannot stop a
later call or touch a closed handle. Disarming removes the timer after the
outer call; weak ownership permits native-owner finalization after a completed
call. Managed imports which never return still cannot be forcibly interrupted,
the pre-existing documented limitation.

Cross-thread stop is the pinned Unicorn runtime's own mechanism: Unicorn 2.1.4
uc.c _timeout_fn calls uc_emu_stop from the thread created by enable_emu_timer.
Primary host-runtime source: [Unicorn 2.1.4 uc.c](https://github.com/unicorn-engine/unicorn/blob/2.1.4/uc.c#L1013-L1040).
This host timeout machinery is free under AGENTS.md What we reproduce /
Implementation is free and M1-034; it is not a new DSP equivalence or an engine
behavior departure.

## Verification and limits

Final focused 37/37: all existing emulation deadline/nested/fault/budget/dispose
checks, two new completed-call/finalization checks, unchanged native Acapela
sample oracle, a new original-PCM hash for this long name, all three private
face cases, and literal lint. The late named replay passes without changing its
clock, random choices, motor/animation ACKs, input pixels or assertions.
Fidelity check passes 764 records; no manifest/approval/inventory changes.
Full suite with all 15 available private replay cases: 8725 passed / 6 failed /
8731 total, 40.3739 minutes. All three private face cases pass, including the
previously failing late case (37 s). All six failures are the existing fixed
UDP 47817 conflict with the live MCP process, which was left running. No clean
full-suite pass is claimed. Log .scratch/m13-face-greeting-full-a3625bba.log;
immutable DLL snapshot bin/Debug/m13-face-greeting-full-a3625bba. Tested source
hashes are unchanged after the snapshot.

No hardware acceptance was run; no bundle is committed; SCOPE/PROJECT_STATE are
unchanged. M14-006 remains BLOCKED_EXTERNAL and larger face/framework records
remain at their existing statuses. This fixes a measured host bookkeeping cost,
not all possible slow-host speech latency. A sufficiently delayed input can
still legitimately hit the unchanged native timeout. Physical acceptance and
manager spot-read remain required before claiming a scope item finished.
