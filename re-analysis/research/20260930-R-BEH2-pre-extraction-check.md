# Manager's check of the R-BEH2 pre-extraction

- **Date:** 2026-09-30
- **Checks:** `20260929-R-BEH2-pre-extraction.md` (Codex, b805f1f)
- **How:** an Opus `cozmo-verifier` pass read every cited range in `libcozmoEngine.so`. The manager then re-read the
  two central findings in the binary: the FistBump tilt literal at 0x005F23B0/0x005F23B6, and the action-active gate
  at 0x005F1F7E..0x005F1F8E.

**Verdict: use the answer as build rows only with the corrections below.** Where a correction and the answer
disagree, the correction wins. The semantics mostly hold. The defects are:
- one float literal, one ULP off;
- one omitted gate that every state depends on;
- wrong citations;
- two stale "contradiction" claims.

## Corrections by section

1. **M8-008.**
   - **The timeout result.** `0x03000018` is built in r5 (`movs r5,#0x18` 0x00540E80; `movt r5,#0x300`
     0x00540E82). It is passed as `mov r1,r5` at 0x00540EF4 and returned at 0x00540F00. There is no
     `movw/movt r1` in 0x00540EEA..0x00540EFE.
   - **vptr+0x80** is `IBehavior::ScoredActingStateChanged(bool)`, called with false (relocation 0x01026568). It does
     not "mark acting false".
   - **The adapter** is `StartActing(IActionRunner*, function<void(Robot&)>)::$_9`, typed `void(ActionResult)` (vtable
     0x010267E0; typeinfo 0x00C66B80). It discards the ActionResult. The chain is 0x00608924 → 0x005BE0E4 →
     0x005BDDD8. The conclusion, that the result is discarded, holds.
   - **The four CalibrateMotorAction call sites** (via PLT 0x004A9304) are 0x00607C9A, 0x0060808C, 0x00608658 and
     0x00608892. The non-behaviour site is 0x00529D38. The addresses in the answer are wrong; its set of four is right.
   - **ReactToPlacedOnSlope.** The calibration is in InitInternal at 0x0060807C..0x006080A2, gated by
     `(now − *(double*)+0x120) < 10.0 && byte +0x11C` (0x00608016..0x0060802C). +0x11C is a byte (`strb`).
2. **M7-014.** The value-byte test is at 0x005A283C..0x005A2846 (0x005A2868 is a log call). The first lock's
   `EnabledStateChanged(robot,false)` (0x005A28A8..0x005A28B4) is covered by M10-derived row 4b.
3. **M7-012 / M7-020.**
   - **Add the one-shot suppression:** when the tag is in the +0x140 set, HandleActionEnded erases it and returns (tail
     call 0x008CD6AC, `__tree<unsigned>::erase`).
   - **Drop the claim that M7-020 is contradicted.** Its current text is about the stack having no live caller, not
     about the native path.
4. **M7-019.**
   - **Citations.** The direct backup is `blx TransitionToBackingUp` at 0x00605132, not 0x006051B8. The constructor
     zeroing is `memclr4` at 0x005BBD06.
   - **Add the cliff animation's callback and parameters.** Its completion callback is TransitionToBackingUp, with
     parameters `(trigger, 1, true, …, 60.0f = 0x42700000)`.
   - **Add the trigger choice.** `[[robot+0x264]+0x30]+0x14` selects NeedsSevereLowRepair (0x13D) or
     NeedsSevereLowEnergy (0x131) CliffReact. The field's name is UNKNOWN.
5. **M7-021.** The helper at 0x005C0CA8 holds. The +0x58 read is at 0x005BDB20; 0x005BDBD0.. reads the name at +0x48.
   The claim that there is **no behaviour-changing +0x58 reader is unverified**: the scan was not reproduced, so the
   build job repeats it.
6. **M8-014.**
   - **Floats, by bit pattern:** 2500 0x451C4000; 10° 0x3E32B8C2; 50.0 0x42480000; π 0x40490FDB; 35.0 0x420C0000;
     1e-5 0x3727C5AC. The ones without a bit pattern above are 30.0, −0.5 and −1.0.
   - **The tilt test** goes through `Anki::operator<(Radians,Radians)`, not a raw float compare.
   - **The pose base** comes from `bl 0x004EA398`, which is not identified. This is an open step.
7. **M8-013.** The citations are transposed: 0x94 (ID) is 0x0060AA6E..0x0060AA88, 0x93 is 0x0060AAAE..0x0060AAC8, and
   unknown-tag is 0x0060AB28..0x0060AB7A. Drop the claim that the record is "backwards"; its text is about the stack.
8. **M7-018.**
   - **The PanAndTilt tilt is 0x3F1C61AA** (`movw r1,#0x61aa` 0x005F23B0, `movt r1,#0x3f1c` 0x005F23B6). The
     answer's 0.6108652 is 0x3F1C61A9, one ULP off.
   - **Other floats, by bit pattern:** 0x3C0EFA35, 0x3E32B8C2, 0xBE860A92/0x3F060A92 (data 0x00C6FDC0), 4000.0 =
     0x457A0000.
   - **Add the gate.** UpdateInternal dispatches its state switch (`tbh` 0x005F1F98) only when the state is 5 or 6, or
     when +0x84 == 0, i.e. no action is running (0x005F1F7E..0x005F1F8E). Otherwise it returns at 0x005F2506.
   - **The idle trigger** 0x23F that Init pushes (0x005F1EEE) is `AnimationTrigger::Count`. What SmartPushIdleAnimation
     does with Count is UNKNOWN.
   - **Minor.** CureHiccups sends a DAS event first. RobotHiccupsChanged is gated on strategy +0x44 being non-null
     (0x006107DE). The not-runnable path logs (0x0061086A). hiccupParams are at lines 61–79 of
     `reactionTrigger_behavior_map.json`.
   - **Not checked:** ExtractBehaviorClassFromConfig, the container load and the AddToFactory rows. The build job
     checks them.

## Coverage

All eight requested items are answered.
