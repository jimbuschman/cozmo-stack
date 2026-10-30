# CANT-HANDLE-TALL-STACK-1 — head defaults supplement

2026-10-30. Narrow builder/manager question: does `CreateNativeHeadChild(target,tolerance,15f,20f)` preserve the native caller's third `Radians(0)`? Resumed reader requested **gpt-6.1-sol / medium**; actual backend unobserved. `known.py MoveHeadToAngleAction` run before tracing. Primary instructions checked in local `resources/lib/armeabi-v7a/libcozmoEngine.so`; existing `20261028-build-pyramid-1-motion-arguments.md` was navigation only. No implementation, .NET, tests, hardware, git mutation or official metadata changes. Prior reports unchanged.

## Answer

**Yes, for these two zero-variability callers.** The three angle arguments are target, tolerance, angular variability. Speed15, acceleration20 and duration0 are separate constructor-initialized fields. Third-angle zero suppresses the random target-offset branch; it is not a zero motor speed, acceleration or duration argument.

| Step | Primary instruction evidence |
|---|---|
| Cant down/up calls | `005ECF1C..20` and `005ECF76..7A` construct the third `Radians` from zero. `005ECF2A` / `005ECF84` put its pointer on the argument stack. `005ECF30` / `005ECF8A` call the PLT entry `004A94FC`, resolving to `MoveHeadToAngleAction(Robot&, Radians const&, Radians const&, Radians const&)`, body `00547E40`. |
| Target and tolerance | `00547E4C` saves r2; `00547E4E` saves r3. `00547EF4..F8` copies target to this+78; `00547EFC..00547F04` copies tolerance to this+80. |
| Third angle | `00547F08` loads the incoming stack argument, `00547F0A..10` copies its Radians value to this+88. |
| Independent defaults | `00547F14..20`: construct r0=0x41A00000 (20f), r1=0x41700000 (15f), `strd r1,r0,[fp,#0x90]`: speed at+90, acceleration at+94. `00547F24..28` stores zero at+98 (duration). |
| Variability use | `005480B0..BC` compares this+88 against Radians0 with `operator>`. `005480C0..C2` skips to00548190 unless true. Only positive variability enters `GetRNG` at005480C6 and `RandDblInRange(-variability,+variability)` at005480DE; its float result is added to target at005480F0. Supplied0 does not enter this branch. |
| Motor use of defaults | `00548552` loads target and sets r6=this+78; `0054855E` loads r2/r3 from r6+18 (this+90/+94); `00548562` loads duration from r6+20 (this+98); `0054856C` stores duration on argument stack; `00548570` calls `MovementComponent::MoveHeadToAngle(float,float,float,float,unsigned char*)`. |

Current candidate helper `Motion.cs:1168..1173` calls `BuildHeadAction(angle,speed,accel,0,null,tolerance)`. `BuildHeadAction` captures the supplied speed, acceleration and duration in the `SetHeadAngle` factory (`Motion.cs:807..812`) and has no variability input or random offset. Thus this helper with15/20 implements the native caller's independent defaults and zero variability. Its existing target normalization/clamp, tolerance, Init/ack/completion and action ownership remain shared dependencies; this answer does not newly recertify that full action path. No manifest record ownership/status/closure claim is made.
