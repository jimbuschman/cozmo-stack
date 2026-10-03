# R-BEH2 check 1 of 3: the M8 rows of 20260929-audit-M7-M8.md Appendix C against libcozmoEngine.so

- Date: 2026-10-02
- Binary: resources/lib/armeabi-v7a/libcozmoEngine.so (Thumb-2; capstone via lief). Every address below is a VA and the
  instruction text is quoted from my own disassembly (the decompiler tree was not used).
- Rows: M8-001, 002, 003, 005, 006, 007, 009, 012. Also read: 20260930-R-BEH2-pre-extraction-check.md section 1.
- Scope: the audit rows mix engine facts with claims about the C# stack (file:line in src). I checked every engine fact and
  every address range. Statements that are only about C# are marked "C#-only, not in scope" and were not judged, except
  where I opened the cited line.
- Nothing in the repo was edited. My scripts are in a subfolder (m8/) of the scratch dir (shared with the other checkers).

Legend: HOLDS = the address range does what the row says. WRONG = contradicted or overbroad (corrected fact given).
OMITTED = native behaviour next to a cited function that the row does not state. UNVERIFIED = not checkable in the binary.

## 0. Summary of verdicts

| row | verdict | WRONG / OMITTED items |
| --- | --- | --- |
| M8-001 | HOLDS (engine facts), 2 OMITTED | ScoredActingStateChanged ignores its bool and StartActing also calls it (true) at 0x5bdb54..0x5bdb5c; Resume refusal also stamps +0x118 |
| M8-002 | HOLDS | extra: the runningPenalty graph (+0xf4) has the same flat-1.0 default |
| M8-003 | HOLDS | Emotion::GetHistoryValueTicksAgo body (0x6794f8) given below; no record covers it |
| M8-005 | PARTIAL, 1 WRONG | "num_loops 0 plays nothing" holds only on the StartSequenceLoop path; with exactly one trigger, numLoops 0 loops forever and the timeout becomes FLT_MAX |
| M8-006 | HOLDS | name of [[robot+0x264]+0x30]+0x14 is UNKNOWN in the binary |
| M8-007 | PARTIAL, 1 WRONG (overbroad) | "the engine never [sends DisableAnimTracks] for PlayAnim": it does for LIFT when carrying and robot+0x355==0 |
| M8-009 | HOLDS | none |
| M8-012 | PARTIAL: 1 imprecise, 3 OMITTED | per-trigger disable-lock gate exists at 0x5a359a..0x5a359e (the row says there is no gate counterpart); scored switch has THREE gates; StopAllMotors + conditional CompletelyUnlockAllTracks on a reaction switch; activity Update virtual is step 2 |

The pre-extraction correction (M8-008 section 1: vptr+0x80 is ScoredActingStateChanged(bool), called with false, relocation
0x01026568) HOLDS (M8-001 item 3 below).

## 1. Vtable facts (slot = (reloc - vtable_start - 8) / 4, from the relocations)

IBehavior vtable start 0x010264e0 (vptr = +8 = 0x010264e8), size 0x98. BehaviorPlayAnimSequence vtable start 0x01026918
(vptr 0x01026920). Both starts match the audit.

| vptr+ | slot | reloc (IBehavior) | IBehavior | reloc (PlayAnim) | PlayAnimSequence |
| --- | --- | --- | --- | --- | --- |
| 0x0c | 3 | 0x010264f4 | IBehavior::UpdateInternal 0x5bda56 (`ldr.w r1,[r0,#0x84]; movs r0,#2; cmp r1,#0; it ne; movne r0,#1`: 1 while +0x84 != 0, else 2) | 0x0102692c | same |
| 0x20 | 8 | 0x01026508 | 0x5bf04c `movs r0,#0` | 0x01026940 | same (called by IsRunnableBase 0x5bd86c when robot+0x355 != 0; must return 1) |
| 0x24 | 9 | 0x0102650c | 0x59ec12 `movs r0,#0` | 0x01026944 | same (called 0x5bd87e when robot+0x34a != 0) |
| 0x28 | 10 | 0x01026510 | __cxa_pure_virtual | 0x01026948 | 0x5bff1a `movs r0,#1` (called 0x5bd894 when [robot+0x284]+8 != -1, carrying) |
| 0x30 | 12 | 0x01026518 | 0x59ec28 `bx lr` | 0x01026950 | BehaviorPlayAnimSequence::AddListener |
| 0x48 | 18 | 0x01026530 | __cxa_pure_virtual | 0x01026968 | BehaviorPlayAnimSequence::InitInternal 0x5c014e |
| 0x4c | 19 | 0x01026534 | IBehavior::ResumeInternal 0x5bda94 | 0x0102696c | 0x5bff1e `movs r0,#1; bx lr` |
| 0x50 | 20 | 0x01026538 | __cxa_pure_virtual | 0x01026970 | BehaviorPlayAnimSequence::IsRunnableInternal 0x5c013a |
| 0x54 | 21 | 0x0102653c | 0x5bf050 `bx lr` (StopInternal) | 0x01026974 | same |
| 0x80 | 32 | 0x01026568 | IBehavior::ScoredActingStateChanged(bool) 0x5bf044 | 0x010269a0 | same |
| 0x84 | 33 | 0x0102656c | IBehavior::EvaluateRunningScoreInternal 0x5beede | 0x010269a4 | same |
| 0x88 | 34 | 0x01026570 | IBehavior::EvaluateScoreInternal 0x5beec2 | 0x010269a8 | same |
| 0x90 | 36 | | | 0x010269b0 | 0x5c03b4 `movs r0,#1` (tail-called by IsRunnableInternal) |

Scan of all 79 vtables whose class name starts "Behavior" (size >= 0x98): every one has vptr+0x80 =
IBehavior::ScoredActingStateChanged(bool); no behaviour overrides it.
The audit vtable-facts line (vptr+0x48 InitInternal at 0x01026968, pure virtual in the base; vptr+0x50 IsRunnableInternal;
vptr+0x80 ScoredActingStateChanged) HOLDS.

## 2. M8-001

1. HOLDS. IBehavior::Init 0x5bcb54: `ldr r2,[r4]` (0x5bccf2) ... `ldr r2,[r2,#0x48]` 0x5bccfe, `blx r2` 0x5bcd00, r1 = robot
   ([r4,#0x2c], 0x5bccf4). Slot 18 = InitInternal. `mov r5,r0` 0x5bcd02; `cbz r5,0x5bcd0c` 0x5bcd04: r5 != 0 ->
   `strb.w r6,[r4,#0xa1]` (r6 = 0) 0x5bcd06; r5 == 0 -> +0x80 += 1 (0x5bcd0c..0x5bcd12). Init returns r5 (0x5bcd5c). Before the
   call: `strh.w r1,[r4,#0xa0]` with r1 = 0x100 (0x5bccca): +0xa0 = 0, +0xa1 = 1.
2. HOLDS. SwitchToBehaviorBase 0x5a1e20: `IBehavior::Init()` 0x5a1e94, `cbz r0,0x5a1f0c` 0x5a1e98. Non-zero: sErrorF with the
   event BehaviorManager.SetCurrentBehavior.InitFailed and the text "Failed to initialize %s behavior." (0x5a1eb4), debug-break flag
   (0x5a1eda..0x5a1eee), info.current := null via 0x5a207c (0x5a1efc), r4 = 0. In both cases SetRunningAndResumeInfo(info) 0x5a1f12 and
   SendDasTransitionMessage(old,new) 0x5a1f1c.
3. HOLDS, with OMISSION. IBehavior::StopActing 0x5bd34c: `ldr r0,[r4]` 0x5bd354, `movs r1,#0` 0x5bd358, `ldr.w r2,[r0,#0x80]` 0x5bd35a,
   `mov r0,r4` 0x5bd35e, `blx r2` 0x5bd360. ScoredActingStateChanged 0x5bf044: `movs r1,#0; str.w r1,[r0,#0x104]; bx lr` (0x5bf044..0x5bf04a): +0x104 := 0.
   OMITTED: the body never reads its argument, and IBehavior::StartActing (first overload 0x5bdacc) calls the same slot with TRUE:
   `str.w r1,[r5,#0x84]` 0x5bdb50, `movs r1,#1` 0x5bdb54, `ldr.w r2,[r0,#0x80]` 0x5bdb56, `blx r2` 0x5bdb5c. So +0x104 is cleared at the START of every
   action as well as at every StopActing (StopActing has about 40 callers, not only Stop 0x5bd126). IncreaseScoreWhileActing 0x5bf02c adds to +0x104 only when
   +0x84 != 0 (`ldr.w r2,[r0,#0x84]; cbz r2` 0x5bf030; `vadd.f32 s0,s2,s0; vstr s0,[r0,#0x104]` 0x5bf03a..0x5bf03e).
4. HOLDS. After `ActionList::Cancel(r6)` (0x5bd3f0): `ldr.w r1,[r4,#0x84]; cmp r1,r6; itt eq; moveq r1,#0; streq.w r1,[r4,#0x84]` (0x5bd3f4..0x5bd3fe).
   keepAction (r1 of StopActing, kept in r5) only decides whether +0x84 is cleared BEFORE the Cancel (`cbnz r5,0x5bd3e8` 0x5bd3e0; `str.w r0,[r4,#0x84]` 0x5bd3e4).
5. HOLDS. IBehavior::Update 0x5bd074: `ldrb.w r1,[r0,#0xa0]; cbz r1,0x5bd080; ldr.w r1,[r0,#0x84]; cbz r1,0x5bd088` (0x5bd074..0x5bd07e); 0x5bd088
   `movs r0,#2; bx lr`; else 0x5bd080 `ldr r2,[r0]; ldr r1,[r0,#0x2c]; ldr r2,[r2,#0xc]; bx r2` (UpdateInternal, slot 3). It is step 8 of the tick (section 9), after the
   activity/reaction steps.
6. HOLDS, with detail. Resume 0x5bceac: `cmp r5,#0x14; it ne; cmpne r5,#0; bne 0x5bcf7e` (0x5bcf16..0x5bcf1c): resume trigger 0x14 (UnexpectedMovement) or 0
   (CliffDetected) (EnumToString(ReactionTrigger) 0x77065c, table 0x010337c0: 0 CliffDetected ... 20 UnexpectedMovement, 21 Count, 22 NoneTrigger).
   `ldr.w r0,[r4,#0x114]` 0x5bcf1e; `cmp r0,#1`; `add.w r1,r0,#1`; `str.w r1,[r4,#0x114]` (store BEFORE the test); `blt 0x5bcf7e` (signed): old counter >= 1 refuses the
   second and every later such resume: `vmov.f32 s0,#15.0` 0x5bcf36 (0x41700000); `vadd.f32 s0,s2,s0; vstr s0,[r4,#0x118]` 0x5bcf48..0x5bcf4c (+0x118 = now + 15.0, the
   IsRunnableBase cooldown: OMITTED by the row); MoodManager::TriggerEmotionEvent(TooManyResumesCliffOrMovement (length 0x1d), now) call 0x5bcf68; `movs r0,#1` 0x5bcf7a,
   return 1 (fail). Counter zeroed at Init end (`str.w r0,[r4,#0x114]` 0x5bcd58) and in InitScored 0x5bcea4..0x5bcea6. Asset:
   re-analysis/obb/assets/cozmo_resources/config/engine/emotionevents/reaction_events.json line 22 name TooManyResumesCliffOrMovement, lines 25-26 emotionType Confident,
   value -1.0. HOLDS.
   Otherwise (0x5bcf7e): +0xa2 = 1; +0x34 = now; ResumeInternal via vptr+0x4c (0x5bcf94..0x5bcf96); +0xa2 = 0 (0x5bcf9c); r0 != 0 -> +0xa1 = 0, return r0 (0x5bcfa2);
   r0 == 0 -> +0xa1 = 1 (0x5bcfa8) and the spark gate (0x5bcfac..0x5bcfde, as in Init); returns 0.
7. C#-only, not in scope: SteppedBehavior.cs:275, 496-504, 210-222, 502, 314-315; BehaviorManager.cs:639-642; IBehavior.cs:583-594; BehaviorFrameworkTests.cs:511-519, 541;
   Activities.cs:264. Engine side of the Init action-tag guard: Init has a warn-only scan of the ActionList queues for action tags above 0x002dc6c0 (movw r6,#0xc6c0 /
   movt r6,#0x2d at 0x5bcbd0..0x5bcbda; compares [action+0x60] at 0x5bcbe8 and 0x5bcc08), ending in sWarningF (0x5bcc84); it never changes the Init result (flows to 0x5bccac).
   I did not trace its exact trigger condition: UNVERIFIED beyond warn-only.

## 3. M8-002

1. HOLDS. Ctor 0x5bbb74: `GraphEvaluator2d::GraphEvaluator2d(unsigned int)` with r1 = 0 at 0x5bbd12 (second graph 0x5bbd22); +0x100 = 0, +0x104 = 0, +0x108 = 0,
   +0x10c = 0x29, strh +0x110 = 0x0101 (both flags true), +0x114 = 0, +0x118 = 0 (0x5bbd28..0x5bbd38). ReadFromScoredJson 0x5bc488 keys (read from the operands):
   emotionScorers (+0xdc), flatScore (+0x100), repetitionPenalty (graph +0xe8), considerThisHasRunForBehaviorObjective (+0x10c), runningPenalty (graph +0xf4).
2. HOLDS. 0x5bc4ea Clear(); isNull 0x5bc4fc; `cbnz r0,0x5bc554` 0x5bc500: a null key skips to 0x5bc554: `ldrd r0,r1,[r4,#0xe8]; cmp r1,r0; bne`, graph empty ->
   `movs r1,#0; mov.w r2,#0x3f800000; movs r3,#1; blx GraphEvaluator2d::AddNode(float,float,bool)` (0x5bc55c..0x5bc566): node (0.0f, 1.0f, true). No warning on the null path; the
   warning IScoredBehavior.BadRepetitionPenalty (0x5bc524..0x5bc52e) is only for a present key whose ReadFromJson returns false (0x5bc506..0x5bc50a). Extra: runningPenalty
   has the same default (0x5bc666..0x5bc678).
3. HOLDS. MoodManager::UpdateEventTimeAndCalculateRepetitionPenalty 0x67bedc: when EmotionEventMapper::FindEvent (0x67beee) returns 0, `adds r0,#0x78` on the
   StaticMoodData base (0x105bfa8) then veneer 0x8cbf9c = GraphEvaluator2d::EvaluateY (0x67befe..0x67bf0a). A scan of every pc-relative reference to 0x105bfa8 finds only
   MoodManager functions (GetStaticMoodData, Init, LoadEmotionEvents, Update, TriggerEmotionEvent, this one): no IBehavior use. "Only" is supported.
4. HOLDS. EvaluateRepetitionPenalty 0x5beee6: `vldr s0,[r4,#0x30]; vcmpe.f32 s0,#0; vmrs; itt le; movle.w r0,#0x3f800000; pople {r4,pc}` (0x5beeea..0x5beef8): stamp <= 0 (also NaN)
   returns 1.0f. Else now - [+0x30], r0 = r4+0xe8, tail call EvaluateY (0x5bef12..0x5bef1a). EvaluateRunningPenalty 0x5bef22: same on +0x34 / graph +0xf4.
5. HOLDS. StopWithoutImmediateRepetitionPenalty 0x5beea0: `blx IBehavior::Stop()` 0x5beea4, then `vmov.f32 s0,#1.0` 0x5beeb0, `vadd.f32`, `vstr s0,[r4,#0x108]` 0x5beebc
   (+0x108 = now + 1.0f). Consumer: EvaluateScore 0x5befd4..0x5beffe applies the penalty only when now >= +0x108 (`vcmpe.f32 s2,s0; bpl 0x5beffe`).
6. Assets HOLD: activities/freeplay/socialize.json: 7 behaviour entries (lines 8,14,20,26,32,39,46), 0 repetitionPenalty keys; hiking.json: 12 behaviorID entries, one
   repetitionPenalty (line 71); knockOverCubes.json, putDownBlock.json, driveOffCharger.json have none.
7. C#-only, not in scope: IBehavior.cs:613-615, 628-629; Activities.cs:75; SteppedBehavior.cs:255-259.

## 4. M8-003

HOLDS. Bodies:
- EvaluateScoreInternal 0x5beec2..0x5beed4: `ldr r2,[r0,#0xdc]!; ldr r3,[r0,#4]; cmp r2,r3; itt ne; ldrne.w r1,[r1,#0x440]; bne.w 0x8cbf8c` = tail call
  MoodScorer::EvaluateEmotionScore(MoodManager const&) (veneer -> PLT 0x4ad3cc) on the scorer vector at +0xdc with the robot MoodManager [robot+0x440]; empty vector:
  `vldr s0,[r0,#0x24]` (= +0x100 flatScore) returned.
- EvaluateScore 0x5bef60..0x5bf004: +0xa1 (running): score = vptr+0x84(robot) + [+0x104] (`vadd.f32 s16,s0,s2` 0x5bef88); if [+0x111] then score *= EvaluateRunningPenalty()
  (0x5bef8c..0x5bef98). Not running: s16 = 0.0 (0x5befa6); IsRunnableBase(robot) must be 1 (0x5befa2..0x5befaa), vptr+0x50 IsRunnableInternal must be 1 (0x5befb2..0x5befb8), score =
  vptr+0x88 (0x5befc6..0x5befd0); if [+0x110] and now >= [+0x108]: score *= EvaluateRepetitionPenalty() (0x5befd4..0x5bf004).
- MoodScorer::EvaluateEmotionScore 0x67c9b8: per scorer (stride 0x10: type byte +0, graph vector +4, trackDelta byte +0x10): value = emotion[type*32 + 0x18]; if trackDelta:
  value -= Emotion::GetHistoryValueTicksAgo(60) (`movs r1,#0x3c` 0x67c9ee; `vldr s22,[r0,#0x18]` 0x67c9f0; call 0x67c9f4; `vsub.f32 s22,s22,s0` 0x67c9fc); s = graph.EvaluateY(value);
  if |s| < 1e-5f (0x3727c5ac) return 0.0 at once (0x67ca3a..0x67ca58); else sum += s; result sum / (float)count (`vdiv.f32` 0x67ca72); empty -> 0.0.
- Emotion::GetHistoryValueTicksAgo 0x6794f8: ticks == 0 or count [+0x10] == 0 -> current value [+0x18]; else index = ([+0xc] + (count > ticks ? count - ticks : 0)) mod [+0x14]
  (__aeabi_uidivmod 0x679512), returns the float at [[+0] + index*8] (0x6794f8..0x679524).
- HandleBehaviorObjective 0x5bf00c..0x5bf028: `ldr.w r0,[r4,#0x10c]; cmp r0,#0x29; beq`; `ldr r1,[r1]; cmp r1,r0; it ne; popne`; `str r0,[r4,#0x30]` = now.
C#-only, not in scope: Activities.cs:264, 75, 181-186.

## 5. M8-005 and request (c): the PlayAnim arguments

Argument order is fixed by the mangled name: TriggerLiftSafeAnimationAction(Robot&, AnimationTrigger, unsigned int, bool, unsigned char, float, bool). ARM EABI: r0 this,
r1 robot, r2 trigger, r3 numLoops, stack [sp+0] bool, [sp+4] u8, [sp+8] float, [sp+0xc] bool. In the source these are (numLoops, interruptRunning, tracksToLock, timeout,
strictCooldown); there is no ignoreFailure parameter on this constructor (ignoreFailure belongs to ICompoundAction::AddAction, below).

BehaviorPlayAnimSequence ctor 0x5bff24 (robot, Json cfg): IBehavior ctor; zero +0x134/+0x138; `memclr4(this+0x11c, 0x14)` 0x5bff4a (vector +0x11c..+0x124, num_loops +0x128,
loop index +0x12c); listener set root +0x130 = this+0x134; key animTriggers (array): each element asString -> AnimationTriggerFromString; skipped when the result is 0x23f
(movw r0,#0x23f; cmp fp,r0; beq at 0x5bffe8..0x5bffec = AnimationTrigger::Count), else push_back; key num_loops with default Json::Value(1) (0x5c001c..0x5c0036) -> asInt -> +0x128.

InitInternal 0x5c014e (vptr+0x48): `StartPlayingAnimations(robot)` 0x5c0150, `movs r0,#0` (always RESULT_OK).
IsRunnableInternal 0x5c013a (vptr+0x50): trigger vector empty -> 0; else tail call vptr+0x90 (0x5c03b4: 1). IsRunnable also needs IsRunnableBase: vptr+0x20 / +0x24 base = false
when robot+0x355 / robot+0x34a are set; vptr+0x28 (0x5bff1a) = true when carrying (section 1).
ResumeInternal (vptr+0x4c) 0x5bff1e: `movs r0,#1; bx lr`. TryToResumeBehavior 0x5a2b40: `blx IBehavior::Resume` 0x5a2c76, `cmp r0,#0; beq 0x5a2d10` (0x5a2c7a): 1 -> failure path 0x5a2c7e.

StartPlayingAnimations 0x5c0158: `ldrd r0,r1,[r4,#0x11c]; subs r1,r1,r0; cmp r1,#4; bne 0x5c01aa` (0x5c0160..0x5c0168):
- exactly ONE trigger (byte size 4): `new` 0xcc (0x5c016e); r2 = trigger [r0]; r3 = [r4,#0x128] (num_loops, 0x5c0174); stack: `movs r2,#1; strd r2,r0,[sp]` (0x5c017c..0x5c0182: bool 1, u8
  tracksToLock 0), `movs r1,#0; movt r1,#0x4270; strd r1,r0,[sp,#8]` (0x5c0178..0x5c0188: float 60.0 = 0x42700000, bool 0). Ctor PLT 0x4a577c (0x5c0190). Then `b.w 0x8cc0cc` (veneer ->
  PLT 0x4b2b44 = IBehavior::StartActing<BehaviorPlayAnimSequence>(IActionRunner*, void (BehaviorPlayAnimSequence::*)(Robot&))) with r1 = action, r2 = GOT entry 0x103f198 =
  &BehaviorPlayAnimSequence::CallToListeners, r3 = 0 (PMF adjustment). The completion callback is CallToListeners only.
- otherwise: [r4+0x12c] = 0 (0x5c01ac..0x5c01ae), `b.w 0x8cc0dc` (veneer -> PLT 0x4b2b50 StartSequenceLoop).

StartSequenceLoop 0x5c0294: ldrd r0,r1,[sb,#0x128]; cmp r1,r0; bge 0x5c034a (0x5c02a8..0x5c02ae): signed; loop index >= num_loops -> do nothing. `new` 0xac (0x5c02b0) then
CompoundActionSequential(robot) (0x5c02ba). Per trigger (0x5c02ca..0x5c030e): `new` 0xcc; movs r0,#1; strd r0,r8,[sp] (0x5c02d4..0x5c02d8: bool 1, u8 tracksToLock = r8 = 0);
movs r0,#0; movt r0,#0x4270; strd r0,r8,[sp,#8] (0x5c02dc..0x5c02e4: 60.0f = 0x42700000, bool 0); r3 = 1 (numLoops, movs r3,#1 at 0x5c02ea); r2 = trigger; ctor PLT 0x4a577c (0x5c02ec); then
ldr r0,[r5]; ldr r4,[r0,#0x20]; movs r3,#0 (0x5c02f6); str.w r8,[sp] (0x5c02fc); blx r4: CompoundActionSequential vptr+0x20 (slot 8; vtable start 0x1022044, reloc 0x102206c) =
ICompoundAction::AddAction(IActionRunner*, bool, bool) with bool1 = 0 and bool2 = 0. bool1 is ignoreFailure: AddAction 0x54ec7c does cmp r3,#1; bne 0x54ecc4 (0x54ec8c..0x54ec9a): only r3 == 1 installs
the ignore-all function; 0 leaves it empty. Then ldr.w r0,[sb,#0x12c]; adds r0,#1; str (0x5c0310..0x5c031a, index++), closure {vptr 0x10269c8, this} (0x5c0320..0x5c0328),
StartActing(seq, closure) PLT 0x4b2b5c (0x5c0332). The closure operator() (vtable slot 6, 0x5c0552): CallToListeners(this, robot) then b.w 0x8cc0dc = StartSequenceLoop again (next loop).
A failed child: CompoundActionSequential::UpdateInternal 0x54f70c: IActionRunner::Update 0x54f7de, result>>24 dispatch (tbb 0x54f7fe), RunCallbacks 0x54f80c, ShouldIgnoreFailure 0x54f81a,
cmp r0,#1; bne 0x54f88a (0x54f81e..0x54f820): not ignored -> the sequence returns the failure.

Claims:
- 60.0 s timeout (0x5c017e / 0x5c02de): HOLDS (0x5c017e movt r1,#0x4270 after movs r1,#0 at 0x5c017a; 0x5c02de movt r0,#0x4270 after movs r0,#0 at 0x5c02dc). Bits 0x42700000.
- tracksToLock is 0 (0x5c02d8): HOLDS (the strd at 0x5c02d8 stores 1 and 0). LIFT ORed in only while carrying and robot+0x355 == 0 (0x544714..0x544732): HOLDS. TriggerLiftSafeAnimationAction ctor 0x544710:
  ldr.w r4,[r1,#0x284] ... ldr r5,[r4,#8]; adds r5,#1; beq 0x544736 (0x544714..0x544728): not carrying (-1) skips; ldrb.w r5,[r1,#0x355]; cmp r5,#0; it eq; orreq ip,ip,#2 (0x54472a..0x544732):
  LIFT (2) is OR-ed into the u8 only when robot+0x355 == 0; then TriggerAnimationAction ctor 0x544228 via PLT 0x4aae70 (0x544742) with (robot, trigger, numLoops, interruptRunning, tracksToLock, timeout,
  strictCooldown). TracksToLock(robot,u8) 0x544758 does the same OR.
- ignoreFailure = 0 (0x5c02f6, 0x5c02fc): HOLDS (both AddAction bools are 0); a failed child ends the sequence (0x54f81a / 0x54f820 verified above): HOLDS.
- ResumeInternal (0x5bff1e) returns 1, so the engine never resumes PlayAnim (0x5a2c7a): HOLDS. What the engine does instead: 0x5a2c5c StopAndNullifyCurrentBehavior (0x5a2028: Stop() only if +0xa1),
  0x5a2c76 Resume(trigger = [info+0x10], the reaction that just ended); failure: info.resume := null via 0x5a270e (0x5a2cd6), then SwitchToBehaviorBase({null,null,0x16}) (0x5a2ce2..0x5a2cf4).
  Success (0x5a2d10..0x5a2d94): info = {behaviour, null, 0x16} (trigger reset to NoneTrigger), SendDasTransitionMessage then SetRunningAndResumeInfo.
- WRONG: "Math.Max(1, NumLoops) (:249): the engine loop (0x5c02a8..0x5c02ae) plays nothing for num_loops 0". True only for the StartSequenceLoop path (zero or two-or-more triggers; with zero triggers the
  behaviour is not runnable anyway, 0x5c013a). With exactly ONE trigger the engine builds one TriggerLiftSafeAnimationAction with numLoops = num_loops (0x5c0174) and the loop is inside the action:
  PlayAnimationAction ctor 0x543c08: cmp.w r8,#0; bne 0x543cb6; vldr s0 = 0x42700000; vcmp.f32 s16,s0; then (eq) movw r0,#0xffff; movt r0,#0x7f7f; str.w r0,[r4,#0x94] (0x543c96..0x543cb2):
  numLoops == 0 and timeout == 60.0f -> timeout := FLT_MAX (0x7f7fffff). PlayAnimationAction::Init 0x543f20 -> AnimationStreamer::SetStreamingAnimation(anim, numLoops, interrupt, flag) 0x543f96; the
  streamer stores numLoops at +0x68 (0x57b2f6) and tests in AnimationStreamer::Update 0x57d24e..0x57d25a: ldrd r0,r1,[r4,#0x68]; adds r1,#1; str r1,[r4,#0x6c]; subs r0,#1; cmp r0,r1; bhs 0x57d262
  (unsigned): numLoops 0 gives 0xffffffff >= played, so it loops forever (until cancelled), with the 60 s timeout disabled. numLoops N >= 1 plays N loops inside ONE action (not N separate actions).
- Resolves every trigger up front (:215-223, C#): engine side: the trigger -> animation GROUP is resolved in the TriggerAnimationAction constructor (SetAnimGroupFromTrigger 0x54432c: HasAnimationForTrigger
  0x54433e, GetAnimationForTrigger 0x544350, a warning at 0x5443ac when the group is empty); the clip is chosen when the action starts, in TriggerAnimationAction::Init 0x54443c
  (AnimationStreamer::GetAnimationNameFromGroup(group, strictCooldown) 0x54446e); all actions of ONE loop are constructed at the start of that loop (0x5c02ca..0x5c030e), not all loops up front.
  The action name (+0x48) is PlayAnimation + the group name (0x5442aa..0x5442bc).
- C#-only, not in scope: Behaviors.cs:244-272, :215-223, :249 (opened: loops over all clips with Math.Max(1, NumLoops)), BehaviorManager.cs:699-701, BehaviorFrameworkTests.cs:359.

## 6. M8-006

HOLDS (engine facts):
- StrategyExpressNeedsTransition::WantsToRunInternal 0x6136d8: ldr.w r0,[r1,#0x264] at 0x6136dc; ldr r5,[r0,#0x30] at 0x6136e2; NeedsManager = [[robot]+0x34] -> GetCurNeedsStateMutable
  (0x6136e4..0x6136e6); ldr r1,[r4,#0x18]; movs r2,#3; blx NeedsState::IsNeedAtBracket(NeedId,NeedBracketId) (0x6136ea..0x6136ee); cmp r1,#1; it ne; popne returns false (0x6136f6); else
  ldr r1,[r4,#0x18]; ldr r2,[r5,#0x14]; cmp r2,r1; it ne; movne r0,#1 (0x6136fc..0x613704): true iff [[robot+0x264]+0x30]+0x14 != this+0x18 (the strategy need id). Bracket 3 = Critical
  (EnumToString(NeedBracketId) 0x782514: 0 Full, 1 Normal, 2 Warning, 3 Critical, 4 Count). Key need at 0x6136cc (ctor 0x613600: ParseString(json, need, default) -> NeedIdFromString -> +0x18).
- StrategyInNeedsBracket::WantsToRunInternal 0x6141a0..0x6141b4: GetCurNeedsStateMutable then tail call IsNeedAtBracket([this+0x18], [this+0x1c]) via veneer 0x8cc9ec. HOLDS.
- UNKNOWN: the name of the object at [robot+0x264]+0x30 and of its +0x14 field (not named in the dynamic symbols).
- C#-only, not in scope: Behaviors.cs:193-197, Needs.cs:1490-1491.

## 7. M8-007 and request (d): track locks and the wire messages

IActionRunner::Update 0x540370 (r6 = [robot+0x254] = MovementComponent, r1 = [robot+0x250] ActionList):
- First-update gate: state at +0x18 (ctor 0x53fdd4 sets 0x02000001 = NOT_STARTED). cmp.w r7,#0x1000000; beq 0x540592 (already RUNNING: no lock). NOT_STARTED (0x02000001), 0x04000000 (RETRY) and 0x03000009
  (INTERRUPTED) go to 0x5403b8. Names from EnumToString(ActionResult) 0x7586ac: 0 SUCCESS, 0x01000000 RUNNING, 0x02000000 CANCELLED_WHILE_RUNNING, 0x02000001 NOT_STARTED, 0x03000019 TRACKS_LOCKED,
  0x03000018 TIMEOUT, 0x04000000 RETRY.
- 0x540428 str r1,[r4,#0x18] with r1 = 0x1000000 (RUNNING); ldrb.w r0,[r4,#0x56]; cmp r0,#0; bne 0x540592 (suppress-lock flag; ctor default 0: str.w r0,[r5,#0x55] with 0x10000 at 0x53fe18..0x53fe1c gives
  +0x56 = 0, +0x57 = 1).
- 0x540438 ldrb.w sb,[r4,#0x54] = the mask; AreAnyTracksLocked(sb) 0x540440; cmp r0,#1; bne 0x540584. Any requested track already locked: sdk-mode info (0x54044e..0x540472), two sWarningF with
  AnimTrackFlagsToString and WhoIsLocking (0x5404f2..0x540530), then movs r6,#0x19; movt r6,#0x300; str r6,[r4,#0x18] (0x540572..0x54057c), ActionEndUpdating, b 0x540636: returns 0x03000019 = TRACKS_LOCKED.
  HOLDS. This path has no PrepForCompletion; ~IActionRunner 0x541084 then logs sErrorF when +0x55 == 0 (0x54109c..0x5410ae). UNVERIFIED: whether and where the owning behaviour sees a completion for it.
- No retry: IActionRunner::RetriesRemain 0x540836 has only two callers (CompoundActionSequential::UpdateInternal 0x54f906, CompoundActionParallel 0x54fb1a). ActionQueue::Update 0x53f5f4: any result != RUNNING
  (cmp.w r6,#0x1000000; bne 0x53f67e) leads to DeleteActionAndIter 0x53f6d0. HOLDS "it does not retry".
- Not locked: 0x540584 ldr r2,[r4,#0x60] (action id), 0x540586 add.w r3,r4,#0x48 (std::string name), 0x54058a mov r0,r6 (MovementComponent), mov r1,sb (mask), 0x54058e bl 0x4f0f4c. The thunk 0x4f0f4c: std::to_string(int)
  of r2 (the id), then MovementComponent::LockTracks(unsigned char mask, string const& who = to_string(id), string const& name = the +0x48 string) (0x4f0f5c..0x4f0f68). So: owner key = decimal text of +0x60;
  debug name = +0x48; mask = +0x54. Unlock: IActionRunner::UnlockTracks 0x5408ec: [+0x56] == 0 and state != 0x02000001 -> ldr r3,[r0,#4]; ldr r2,[r0,#0x60]; ldrb.w r1,[r0,#0x54]; ldr.w r0,[r3,#0x254];
  bl 0x4f0eb2 (0x5408ec..0x54090c) = UnlockTracks(mask, to_string(id)). Other callers of the unlock thunk 0x4f0eb2: IActionRunner::Interrupt 0x54027e, ~IActionRunner 0x54122a,
  ICompoundAction::StoreUnionAndDelete 0x54f110, TurnTowardsFaceAction 0x54c53e / 0x54d2ca, DrivingAnimationHandler 0x4f0dbc / 0x4f0f3e. Other callers of the lock thunk 0x4f0f4c: ICompoundAction::DeleteActions
  0x54ec08 (re-lock of an unfinished child before PrepForCompletion, guarded by AreAllTracksLockedBy), TurnTowardsFaceAction::Init 0x54be24, DrivingAnimationHandler 0x4f0e00.
- The action mask at +0x54 is the constructor tracksToLock (0x53fe0e / 0x53fe14): HOLDS (ldr r0,[sp,#0x60] at 0x53fe0e is the 5th ctor argument; strb.w r0,[r5,#0x54] at 0x53fe14). PlayAnimationAction ctor 0x543c08 passes
  its own u8 (stack arg [sp+0x34]) through to IAction / IActionRunner; TriggerAnimationAction passes it through (0x544228). "SetTracksToLock is called only by TurnTowardsFace and DriveOffChargerContacts":
  HOLDS (only two callers of PLT 0x4ab314: 0x54b822 in the TurnTowardsFaceAction ctor, 0x558288 in the DriveOffChargerContactsAction ctor). SetTracksToLock 0x540918 only writes +0x54 while the state is NOT_STARTED.
- Where the wire messages are sent (exhaustive scan of all bl/blx targets in .text):
  DisableAnimTracks: the only constructor call is EngineToRobot::EngineToRobot(AnimKeyFrame::DisableAnimTracks&&) at 0x64017c in MovementComponent::LockTracks 0x640098, then Robot::SendMessage(msg, reliable=true
  (r2=1), hot=false (r3=0)) at 0x640184..0x640188. The byte is the OR of the bits of tracks whose lock set went from empty to size 1 in this call (loop 0x6400d0..0x640166: __emplace_multi 0x64012e; ldr r0,[r7]; cmp r0,#1;
  orr.w r0,r0,sb at 0x64014e..0x640158); sent only if non-zero (lsls r0,r1,#0x18; beq at 0x64016a..0x64016c). LockTracks callers: the thunk 0x4f0f4c, IBehavior::SmartLockTracks 0x5be624 (callers: BehaviorPounceOnMotion
  0x5f8f46 and 0x5f9662), MovementComponent::HandleMessage<ChargerEvent> 0x640290, HandleMessage<EnterSdkMode> 0x6404ec.
  EnableAnimTracks: constructor calls at 0x63ffa8 (MovementComponent::UnlockTracks 0x63fe5c: the byte is the OR of the bits whose lock set became empty, sent if non-zero, same SendMessage(..,1,0) at 0x63ffb4) and 0x64101e
  (CompletelyUnlockAllTracks 0x640f84: per locked track index; strb.w r8,[sp,#4] stores the LOOP INDEX r8 (0..7), not the flag bit 1<<i held in sb; then SendMessage(..,1,0) at 0x64102a). The latter is called only from
  BehaviorManager::CheckReactionTriggerStrategies 0x5a3682. UnlockTracks callers: the unlock thunk 0x4f0eb2, IBehavior::Stop 0x5bd174, IBehavior::SmartUnLockTracks 0x5be706,
  MovementComponent::DirectDriveCheckSpeedAndLockTracks 0x63eff8, HandleMessage<ChargerEvent> 0x6402c8, <ExitSdkMode> 0x640570, DrivingAnimationHandler::PlayDrivingLoopAnim 0x4f0eca.
- WRONG (overbroad): "the stack sends it for the clip's own tracks before each play (Motion.cs:213), which the engine never does for PlayAnim". For PlayAnim the mask is 0 in general (LockTracks with mask 0 inserts nothing and sends
  nothing), BUT the TriggerLiftSafe ctor ORs LIFT (2) in when carrying and robot+0x355 == 0 (0x544714..0x544732), so the engine does send DisableAnimTracks{trackFlags = 2} (via LockTracks at the first update) and
  EnableAnimTracks{2} at completion in that case, as a side effect of IActionRunner and not for the clip tracks. The M8-005 row already states the LIFT OR, so M8-007 contradicts it.
- C#-only, not in scope: SteppedBehavior.cs:558-575, Behaviors.cs:254-258, CozmoAnimations.cs:418-421, Motion.cs:213 and 250, BehaviorFrameworkTests.cs:815, BehaviorHardeningTests.

## 8. M8-009 (behaviour stop order)

IBehavior::Stop 0x5bd08c, in order: sChanneledInfoF (0x5bd0c0); ldr r0,[r4]; movs r2,#0; ldr r1,[r4,#0x2c]; strb.w r2,[r4,#0xa1] (0x5bd106..0x5bd10c: running := false); ldr r2,[r0,#0x54]; blx r2 StopInternal (slot 21,
0x5bd110..0x5bd114); +0x30 = now (0x5bd11e, the repetition-penalty stamp); StopActing(false,false) 0x5bd126 (which calls ScoredActingStateChanged(false), StopHelper if +0xc8, ActionList::Cancel(+0x84)); then:
1. Reaction locks: loop ldr.w r0,[r4,#0xac]; cmp r0,#0; bne 0x5bd12c (0x5bd13a..0x5bd140): each pass takes the FIRST node of the std::set of strings at +0xa4 (ldr.w r0,[r4,#0xa4]; add.w r1,r0,#0x10 at 0x5bd12c..0x5bd130)
   and calls SmartRemoveDisableReactionsLock(name): BehaviorManager::RemoveDisableReactionsLock(name + _behaviorLock) then erase from the set (0x5bd470..0x5bd4a8). Lowest key first = std::less name order. HOLDS.
2. ldrb.w r0,[r4,#0xb0]; cbz (0x5bd142): SmartRemoveIdleAnimation(robot) 0x5bd14c.
3. ldrb.w r0,[r4,#0xc0] (0x5bd150): SmartClearMotionProfile 0x5bd158.
4. Track locks: ldr r2,[r5,#0xb4]! (0x5bd15e): iterate the std::map from string to u8 at +0xb4 in order (successor walk 0x5bd178..0x5bd196): ldrb r1,[r2,#0x1c] (mask), adds r2,#0x10 (name), ldr.w r0,[r0,#0x254],
   blx UnlockTracks(mask, name) at 0x5bd174. In-order = map key (name) order. HOLDS. Each call can send one EnableAnimTracks (section 7).
5. The map is destroyed and reset (0x5bd198..0x5bd1a8), then the vector at +0xcc/+0xd0 is emptied (0x5bd1a2..0x5bd1c6); no light removal here.
HOLDS, including the category order and the addresses 0x5bd12c, 0x5bd142, 0x5bd150, 0x5bd15c, 0x5bd1a2, 0x5bd168..0x5bd196. Related: SmartLockTracks 0x5be5bc (map insert of name and mask; if new, LockTracks(mask, name, debug
name); a duplicate name warns and returns false) and SmartUnLockTracks 0x5be6e0 (find, UnlockTracks(stored mask, name), erase). C#-only, not in scope: IBehavior.cs:553-559.

## 9. M8-012 and request (b): BehaviorManager::Update tick order

BehaviorManager::Update(Robot&) starts at 0x5a2f68 (not 0x5a3000: 0x5a3000..0x5a3040 are the cold error path of step 1) and ends at 0x5a31be; 0x5a3550 is the entry of CheckReactionTriggerStrategies.

Order (VA; this = r4, robot = r5):
1. 0x5a2f70 ldrb r0,[r4]; cbz r0,0x5a2fe4: when [this+0] is 0: sErrorF BehaviorManager.Update.NotInitialized (0x5a2ff6), debug-break flag, returns 1 (r2 = 1; 0x5a301e / 0x5a31ba). Otherwise continue (the normal
   return value is 0, 0x5a31b8).
2. 0x5a2f74..0x5a2f84 GetCurrentActivity() (PLT 0x4a7e40), then ldr r0,[sp,#0x18]; ldr r1,[r0]; ldr r2,[r1,#0x20]; mov r1,r5; blx r2: activity vptr+0x20 = slot 8 = IActivity::Update(Robot&). Reloc VAs: IActivity vtable
   0x1025820 -> 0x1025848 = 0x5a7373 (0x5a7372: movs r0,#0; bx lr); overrides: ActivityBuildPyramid reloc 0x1025340, ActivityFeeding 0x1025518, ActivityFreeplay 0x10255e0 (Update 0x5ad9d0: ldr.w r0,[r0,#0x80]; cbz;
   ldr r2,[r0]; ldr r2,[r2,#0x20]; bx r2, forwards to the sub-activity, else returns 0), ActivityGatherCubes 0x1025668, ActivitySparked 0x1025738; no override: BehaviorsOnly, ExpressNeeds, Socialize, StrictPriority.
   Result ignored. (Not in the audit.)
3. 0x5a2f8e ldrb.w r0,[r5,#0x355]; cbnz r0,0x5a2f9c; else ldrb.w r0,[r4,#0x20]; cmp r0,#1; beq 0x5a2fa2; 0x5a2f9c..0x5a2f9e EnsureRequestGameIsClear(): runs when robot+0x355 != 0 or this+0x20 != 1.
4. 0x5a2fa2 ldrb.w r0,[r4,#0x38]; cmp r0,#0; beq 0x5a3060: when +0x38 != 0: SelectUIRequestGameBehavior() 0x5a2faa; +0x38 = 0 (0x5a2fb4); when the current behaviour class byte ([behaviour+0x64]) == 0x2e
   (BehaviorClass 0x2e = RequestGameSimple; EnumToString(BehaviorClass) 0x76a2dc) then byte [[this+0x30]+0x220] = 1 (0x5a3054..0x5a305c).
5. 0x5a3060..0x5a3062 CheckReactionTriggerStrategies() (PLT 0x4b0ff0) -> r5. Called every tick, unconditionally at this level.
   Inside (0x5a3550): IsEmpty() of the ActionList [robot+0x250]; [this+0x4c] |= !IsEmpty() (sticky, 0x5a355e..0x5a356c); when the latch is 0 or the trigger map ([this+0x40..0x44]) is empty -> return 0 (0x5a3570..0x5a357e).
   Per map node (trigger): ldr r1,[node,#0x28]; cmp r1,#0; bne skip (0x5a359a..0x5a359e) = triggers with disable locks are skipped (IsReactionTriggerEnabled 0x5a40b8 reads the same field: ldr r1,[r1,#0x28] ... moveq r0,#1).
   Per strategy (stride 0xc, vector [node+0x14..0x18]): if the running trigger ([[this+0x1c]+0x10]) != 0x16: if it equals the strategy trigger ([strategy+0x18]) -> strategy vptr+0x10 else vptr+0xc (slots 4 / 3 of
   IReactionTriggerStrategy, vtable 0x102cd4c; base relocs 0x102cd64 -> 0x60b732 movs r0,#0 and 0x102cd60 -> 0x60b72e movs r0,#1); a result != 1 skips (0x5a35bc..0x5a35dc). Then
   IReactionTriggerStrategy::ShouldTriggerBehavior(robot, behaviour) 0x60b63a (it calls vptr+0x24, and vptr+0x20 when a sticky flag +0x29 is set; both pure virtual in the base: relocs 0x102cd78 / 0x102cd74), then
   MovementComponent::StopAllMotors() 0x5a3616 and AreAnyTracksLocked(0xff) 0x5a3622: when any is locked and (+0xb8, +0xb9, +0xba are all 0, or +0xd4 != 0): a warning and CompletelyUnlockAllTracks() 0x5a3682 (sends
   EnableAnimTracks per locked index, section 7); then SwitchToReactionTrigger(strategy, behaviour) 0x5a369a (does not break the loop; a second switch in one tick logs a warning, 0x5a3760).
6. 0x5a3068..0x5a3078: cbnz r5; ldr r0,[r4,#0x30]; cbnz r0; ldr r0,[r4,#0x1c]; ldrb r0,[r0,#0x10]; cmp r0,#0x16; itt eq; blxeq ChooseNextScoredBehaviorAndSwitch. THREE gates: no reaction fired AND no UI-game behaviour
   ([this+0x30] == 0) AND running trigger == NoneTrigger. ChooseNextScoredBehaviorAndSwitch 0x5a2a20: GetCurrentActivity, activity GetDesiredActiveBehavior(robot, current behaviour) 0x5a2a4a; when the desired
   behaviour differs from the current one: SwitchToBehaviorBase with info (desired, null, 0x16) 0x5a2ab4.
7. 0x5a307c..0x5a309a: cbnz r5,0x5a30a6; ldr r0,[r4,#0x30]; cbz r0; ... cmp r6,r0; beq; blx SwitchToUIGameRequestBehavior: no reaction fired, a UI-game behaviour exists and it is not the running one.
8. 0x5a30a6..0x5a30bc: r6 = running behaviour ([[this+0x1c]]), r7 = running trigger byte ([[this+0x1c]+0x10]); if r6 == 0 skip (0x5a30b4); blx IBehavior::Update() 0x5a30bc (0x5bd074; virtual vptr+0xc UpdateInternal, slot 3, reloc
   0x10264f4 / 0x102692c for PlayAnim). Result 0 (Failure): sErrorF BehaviorManager.Update.FailedUpdate, text Behavior %s failed to Update() (0x5a314e), debug-break flag, then FinishCurrentBehavior(shared_ptr copy of the behaviour,
   bool = r7 not equal 0x16) 0x5a31a6. Result 2 (Complete): sChanneledDebugF BehaviorManager.Update.BehaviorComplete (0x5a30e8), FinishCurrentBehavior with the same arguments 0x5a3128. Any other value (1 Running): return 0
   (0x5a312e..0x5a31b8).
9. FinishCurrentBehavior 0x5a38c4 (r2 = bool): cmp r2,#1; bne 0x5a38da. r2 == 1: b.w 0x8cbc9c = veneer (bx pc; ldr ip; add pc) -> PLT 0x4b1068 = TryToResumeBehavior() (HOLDS: tail-calls TryToResumeBehavior, 0x5a38d6). r2 == 0:
   if [this+0x30] != 0 and the argument behaviour == [this+0x30]: EnsureRequestGameIsClear (0x5a38dc..0x5a38e4); then info (null, null, 0x16) (movs r0,#0x16; strb.w r0,[sp,#0x10] at 0x5a38f4..0x5a38f6) ->
   SwitchToBehaviorBase 0x5a38fe. HOLDS: movne r2,#1 when trigger != 0x16 (0x5a311e..0x5a3122) and the second argument means try-to-resume.
   TryToResumeBehavior 0x5a2b40 (not fully stated by the row): first, when [this+8] differs from FLT_MAX (0x7f7fffff, literal 0x5a2e58) and the ActionList is empty: info log, then MoveHeadToAngleAction(robot, [this+8], tolerance
   Radians(0x3d0efa35), variability Radians(0)) and MoveLiftToHeightAction(robot, [this+0xc], tolerance 5.0f = 0x40a00000, variability 0) in a CompoundActionParallel queued with QueueAction(position 0, action, 0)
   (0x5a2b48..0x5a2c3a); then the resume described in section 5. When the resume pointer [info+8] is null, jump to the empty switch (0x5a2c58: cbz r6,0x5a2ccc).
10. SwitchToBehaviorBase 0x5a1e20: copy the old info; StopAndNullifyCurrentBehavior 0x5a1e6a; null behaviour -> 0x5a1f0c (ok); else IsRunnable(robot) 0x5a1e76; false: sVerifyFailedReturnFalse
    BehaviorManager.SwitchToBehaviorBase.BehaviorNotRunnable 0x5a1e8e and CONTINUE (0x5a1e7a..0x5a1e94 HOLDS, the fall-through); Init() 0x5a1e94; failure as section 2; SetRunningAndResumeInfo; DAS transition.

Row claims (M8-012):
- Class 0x16 is the running info ReactionTrigger (NoneTrigger): HOLDS. EnumToString(ReactionTrigger) 0x77065c (bound cmp r0,#0x16; table 0x010337c0): 21 = Count, 22 (0x16) = NoneTrigger. Info = current at +0 (ptr and ctrl),
  resume at +8, trigger byte at +0x10 (0x5a38f4/0x5a38f6 movs r0,#0x16 and strb.w r0,[sp,#0x10]; 0x5a2c46 ldrd r6,r5,[r0,#8]; 0x5a2c68 ldrb r7,[r0,#0x10]).
- TryToResume calls IBehavior::Resume with no IsRunnable pre-check (0x5a2c5c..0x5a2c76; the test is in ResumeInternal, 0x5bda9c..0x5bdab0); on failure it switches to empty (0x5a2c7c..0x5a2cf4): HOLDS. IBehavior::ResumeInternal
  base 0x5bda94: IsRunnableBase 0x5bda9c, vptr+0x50 0x5bdaaa, then strb.w r1,[r5,#0xa1] with 1 and a tail call of vptr+0x48 (InitInternal) 0x5bdabc..0x5bdac4; else movs r0,#1.
- CheckReactionTriggerStrategies runs every tick (0x5a3060): HOLDS.
- ChooseNextScoredBehaviorAndSwitch after CheckReactions and only when no reaction fired (0x5a3068..0x5a3078): HOLDS but incomplete (three gates, step 6).
- EnsureRequestGameIsClear (0x5a2f8e..0x5a2f9e), SelectUIRequestGameBehavior (0x5a2fa2..0x5a305c), SwitchToUIGameRequestBehavior (0x5a307e..0x5a309a): HOLDS.
- "The arbiter ReactionsDisabled gate has no counterpart at 0x5a3550 (:268)": IMPRECISE. There is no global any-reaction-lock gate in the engine (the C# Arbiter.ReactionsDisabled is _reactionLocks.Count > 0,
  BehaviorArbiter.cs:149, which I opened), but the engine DOES gate per trigger on the disable-lock count at 0x5a359a..0x5a359e (node +0x28; IsReactionTriggerEnabled 0x5a40b8 reads the same field). A fix aimed at no gate would be wrong.
- On Init failure the engine still stores the running info with only the behaviour nulled (0x5a1efc..0x5a1f12): HOLDS.
- Checked and supported list: IsRunnable 0x5bd750..0x5bd770 HOLDS (IsRunnableBase then vptr+0x50). IsRunnableBase with +0x118, 0x5bd778..0x5bd8c8: HOLDS (end: vcmpe s2,s0; movs r2,#0; it pl; movpl r2,#1 at 0x5bd8be..0x5bd8c8,
  now >= +0x118; constants -1e-5f 0xb727c5ac (literal 0x5bda1c), 1e-5f 0x3727c5ac (0x5bda20), double -1e-5 0xbee4f8b588e368f1 (0x5bd998); gates in order: running -> true 0x5bd7d0; AI process 0x5bd7de; unlock 0x5bd810; robot+0x355 -> vptr+0x20;
  robot+0x34a -> vptr+0x24; carrying -> vptr+0x28; strategy WantsToRun 0x5bd8a4). Resume counter 0x5bcf16..0x5bcfde: HOLDS. Init spark gate 0x5bccac..0x5bcd58: HOLDS (cmp r1,#0x55 at 0x5bccb0; +0xd8 = (X+0x58 != 0x55) ? not byte[X+0x5c] : 0;
  later +0x70 != 0x55 and +0x70 == [mgr+0x58] -> SmartDisableReactionsWithLock(name + _behaviorLock, table 0x6a924e) 0x5bcd18..0x5bcd44). SwitchToBehaviorBase fall-through: HOLDS. Sticky reaction latch: HOLDS (0x5a355e..0x5a356c).
  ReactionTrigger ordinals: HOLDS (section 2 item 6 and the table 0x010337c0). HandleBehaviorObjective: HOLDS. +0x108 check: HOLDS.
- C#-only, not in scope: Behaviors.cs:313-320, FreeplayStack.cs:102, BehaviorManager.cs:354, 593-613, 629-633, 656, 561-569, 675-679, 268 (opened), FreeplaySystem.cs:142, 149-227, 233, BehaviorFrameworkTests.cs:754.
  (BehaviorManager.cs:255-268 documents C1..C8 of the engine CheckReactionTriggerStrategies in the C# CheckReactions; its C5, self versus other interrupt slot, matches the slot resolution above.)

## 10. Float literals as bit patterns (every float constant the M8 rows touch)

| value | bits | where |
| --- | --- | --- |
| timeout 60.0f | 0x42700000 | 0x5c017e (movt r1,#0x4270), 0x5c02de (movt r0,#0x4270), literal 0x543c9c (PlayAnimationAction compare) |
| FLT_MAX | 0x7f7fffff | 0x543caa/0x543cae (movw #0xffff, movt #0x7f7f), literal 0x5a2e58 (TryToResume vcmp at 0x5a2b50) |
| 1.0f | 0x3f800000 | 0x5bc560, 0x5bc672 (mov.w r2,#0x3f800000), 0x5beef8, 0x5bef34 (movle.w r0,#0x3f800000), vmov.f32 s0,#1.0 at 0x5beeb0 |
| 0.0f | 0x00000000 | 0x5bc55e, 0x5bc670 (movs r1,#0, the x argument), literal 0x5befa6, 0x5bda84, 0x67c9ca |
| 15.0f | 0x41700000 | vmov.f32 s0,#15.0 at 0x5bcf36 |
| 1e-5f | 0x3727c5ac | literal 0x67cab4 (MoodScorer, vldr s20 at 0x67c9da), 0x5bda20 (IsRunnableBase) |
| -1e-5f | 0xb727c5ac | literal 0x5bda1c (IsRunnableBase vldr s4 at 0x5bd81a) |
| -1e-5 (double) | 0xbee4f8b588e368f1 | literal 0x5bd998 (IsRunnableBase vldr d2 at 0x5bd836) |
| 2.0 degrees in rad | 0x3d0efa35 | 0x5a2bc8 / 0x5a2bce (TryToResume head tolerance) |
| 5.0f | 0x40a00000 | 0x5a2bf8 / 0x5a2bfe (movs r3,#0; movt r3,#0x40a0, lift tolerance) |
| emotion history ticks | integer 60 (0x3c) | 0x67c9ee movs r1,#0x3c |
All values were read as instruction or literal-pool bits; none is a rounded decimal.

## 11. Findings list (WRONG / OMITTED / IMPRECISE), with the corrected fact

1. WRONG, M8-005: "num_loops 0 plays nothing" (and the Math.Max(1,...) justification). Correct: only on the StartSequenceLoop path (StartPlayingAnimations size != 1, 0x5c0160..0x5c0168 -> 0x5c0294; bge 0x5c02ae, signed). Exactly
   one trigger -> a single TriggerLiftSafeAnimationAction(numLoops = +0x128, 1, 0, 60.0f, 0) (0x5c0174..0x5c0190) and num_loops 0 means loop forever (0x57d24e..0x57d25a unsigned test; 0x543c96..0x543cb2 timeout 60.0 -> FLT_MAX).
2. WRONG (overbroad), M8-007: "which the engine never does for PlayAnim". Correct: the engine sends DisableAnimTracks 2 and EnableAnimTracks 2 for TriggerLiftSafe animations when carrying with robot+0x355 == 0 (0x544714..0x544732 ->
   LockTracks 0x640098, send 0x640188; UnlockTracks send 0x63ffb4); mask 0 otherwise, no message.
3. IMPRECISE, M8-012: the ReactionsDisabled claim. Correct: per-trigger lock count at 0x5a359a..0x5a359e, no global gate.
4. OMITTED, M8-001: ScoredActingStateChanged ignores its bool (0x5bf044..0x5bf04a) and is also called with true from StartActing (0x5bdb54..0x5bdb5c).
5. OMITTED, M8-001: the Resume refusal also stamps +0x118 = now + 15.0f (0x5bcf48..0x5bcf4c) and refuses every cliff/unexpected-movement resume after the first, not only the second.
6. OMITTED, M8-012: scored switch gates (0x5a3068..0x5a3074: r5 == 0, [this+0x30] == 0, running trigger == 0x16).
7. OMITTED, M8-012: the activity Update virtual (vptr+0x20, slot 8) is the first step (0x5a2f74..0x5a2f84); a reaction switch does StopAllMotors 0x5a3616 and a conditional CompletelyUnlockAllTracks 0x5a3682 (EnableAnimTracks per locked
   track index, byte = loop index not bit, 0x64101a).
8. OMITTED, M8-012 (TryToResume): the head/lift restore actions at 0x5a2b48..0x5a2c3a before the resume.
UNVERIFIED (not checkable in the binary, C#-only): all file:line statements about src/, tests, and what the stack does; the exact condition of Init warn-only action-tag guard; completion delivery to a behaviour for a TRACKS_LOCKED action.
