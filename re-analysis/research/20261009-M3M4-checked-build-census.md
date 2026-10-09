# M3/M4 checked build slice: call accounting, 2026-10-09

Operator build scope: M4-016 checked defect, M4-009/017/018/019 and M3-010 checked lower slices only. Authority remains the Opus-confirmed defect text and manager inventory C10-C13 identified by the build plan. This is builder self-review, not adoption of October6 research rows or a settlement.

The [native companion](20261009-M3M4-checked-build-native.txt) lists **104 call instructions**, their targets and reopened bodies (43 resolved targets), the checked ranges, mode names and diagnostic literals. ELF SHA256 `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`. Unnamed string helpers are reopened at their entry, not granted an invented semantic body. Imported allocator/string/memclr helpers map to managed allocation/string construction/zero arrays; no claim about native allocation failure behavior. No file/stream open or close occurs in these bounded ranges.

## Call counterparts and side effects

| Range / calls | Live C# counterpart or disposition |
|---|---|
| M4-016 00540D4A/4E, virtual 00540D72/82/98 | `MoveAction.EngineClockSeconds` -> Timer.SecondsF; ActionRunner.UpdateInternal stamps only negative StartTime. Move slots24/28 are zero, slot2C is 30f. Native virtual targets reopened. First-update regression covers stale enqueue clock and inclusive timeout. Already repaired by ActionList integration; no duplicate clock implementation. |
| Head 005485F0 / lift 00549412 / 00549454 | Existing real position tests and lift getter; helpers reopened. Latched completion remains unchanged. |
| Head 00548644, 00548836; lift 00549538, 0054948A | New WaitingForAck / NotInPosition `debug` logs; native event names and formats in companion. Four process-wide u16 counters 01051028/2A/2C/2E, increment with wrap, below11 no log, >=11 log then reset. Tags use signed `%d`; action IDs use the actual wire ID. Head degrees use f32 multiplication by bits42652EE1 before promotion. Variability is zero on this checked game/compound construction path. |
| Head 00548784 / 00548884; lift 005494DC | New `info` HeadMovingInPosition and `warning` StoppedMakingProgress logs before the existing result. Formats copied literally; normal F1/F6 formatting invariant. **MISSING** phone printf exceptional rounding/spelling/locale, matching the already-open M4-001 formatter boundary. |
| Head 0054867E/8E/A8/B2/EC, 0054871E | Checked C11.3/H4-H8: dead eye-shift block, +A8 has only zero writers. No RemoveEyeShift or its debug log is introduced. Lift has no corresponding eye block. |
| M4-009 0053417C / 0053498C | Actual Carrying owner predicate plus actual Docking target; activeID translated by BlockWorld. Existing live DockAsync/Abort test confirms target retention after Abort. No second target state. **MISSING** whole ObjectID/slot admission and ownership remains M11. |
| M4-018 0063791A/1E and refresh gate | Engine timer existing; pending refresh gate now reads World.Robot2C4 when VisionSystem exists, the owner changed by SetLocalizedTo and read by publication. Without VisionSystem, existing OffTreads owner remains. |
| M4-018 006399CE | Existing ObjectInfo lookup. **MISSING** no-ObjectInfo single-object branch and full ObjectID traversal ownership; absence of Unity caller is not proof of unreachability in the replacement API. |
| M4-018 006399F2/00639A2E | Existing SetObjectLights with static off payload, eight u32 FF, remainder0 (C12.1). |
| M4-018 stops 00639A00/0E/80/92/AA2/AB0/AE6 | Existing StopAllAnimsOnLayer. C13.1: all disable stops only layer0 once; single disable stops0 and2; enable stops1 and2. Idempotent gates preserved. |
| M4-018 00639AC6 / 00639B0E | Existing PickNextAnimForDefaultLayer, per-object flag/current-layer writes before repick. All disable now writes comp+22 last at 00639B32, after per-object calls. **MISSING** M11 carried/visible default-selection and full ObjectID traversal, retained explicitly. |
| M4-018 0063AFF8..0063B008 | Existing live SetEnableFreeplayLightStates API inversion, checked tagBB receiver. Shared recipient is not SDK-unreachable. |
| M4-019 00512BA6 / 00512F96 | Existing treads/mismatch Delocalize triggers. Counter0 on treads change, no frame increment and unconditional stats jump. **MISSING** whole M11 Delocalize/origin/geometry body; no invented recipient. |
| M4-019 00512F40 / 00512F7E | Corrected `error: Robot.UpdateFullRobotState.MismatchedFrameIDs`, exact `%u` state and engine frame fields. Store process `_errG` at 00512F7A, gate shipped debug-break (0080DAB4 bx lr), then reset counter at 00512F88 before Delocalize trigger. |
| M4-019 00512FA6/AC | Pose temporary destruction: managed lifetime; full pose/history production semantics remain M11. |
| M4-019 00512FBA / 00512FC4 | **MISSING:** DetectGyroDrift / DetectBias have no live counterpart. Need manager allocation (M10) and checked recipient rows. Reopened functions are evidence leads, not new build authority. |
| M4-019 00512FCE / 00512FD6 | Existing UpdateCliffRunningStats then UpdateCliffDetectThreshold; first100 mismatch gate, reset on match and101, 3000ms parallel history retained. **MISSING** actual AddRobotStateToHistory / GetLastStateWithFrameID results, no synthetic success. H4 behavior end/Cancel(-1) remain M7/M8. |
| M4-017 00632364 | New call to actual VisionSystem queue, before wire construction/send. Null recipient logs empty-format `error: VisionComponent.EnableMode.NullVisionSystem: ` (006527CA, format00BE3F00), stores `_errG` then debug-break gate (00652800/804); ignored return still sends. |
| M4-017 00632374/80/86 | Existing SetHeadlight serialization / reliable, not-hot SendMessage / managed message lifetime. Reopened constructors and send target, no wire changes. |
| M4-017 006B2290, 006B4FDA/FE0 | New real-owner queue for bounded mode14 input; EnableMode behavior precedes pop. Drained in image update before mode dispatch. No direct mask mutation at SetHeadlight. |
| M4-017 006B1A9A/AAA/AD0 and 006B1C4C/5C/82 | Ascending native mode-name table joined by `+`, info EnablingMode/DisablingMode exact formats, log before mask store; unchanged bit skips log. Enable clearsIdle/sets14; disable clears14 and restoresIdle if empty. |
| M4-017 006B1B30/5C, 006B1B80/BAC, 006B1BD0/BFC, 006B1C14 | Not reached for mode14 (native entry switch targets modes11/12/13; Idle-disable warning is mode0). Other mode producers/FaceTracker recipients stay M11; this bounded queue supplies only mode14. E9 reader/effect remains **MISSING** M11. |
| M4-017 006B239C/23C6/23CE/23D6/23E0/23E6 | Managed empty string / append / native enum names / length / increment through16. Reopened string entries and enum/increment targets. |
| M3-010 00597ADE / 00597AF2 | Existing float encoder's isnanf -> float.IsNaN, exact NaN warning/zero return now reachable through scheduler float input. No `_errG` store on warning. |
| M3-010 00597DFE / 00597E34 | Float vector elements reach float encoder without another volume multiply; new byte[744] gives raw zero tail. Readiness/stop/replacement/completion include float stream. |
| M3-010 00597E3A/3E and all listed `_ZdlPv` calls | Managed vector/string/log temporary lifetimes and release of consumed source reference. **MISSING** actual M6 Wwise f32 producer; short source retained as candidate, never normalized by an invented adapter. |

## Builder review and tests

Live entries: CozmoMotion.SetHeadAngleAsync -> ActionList tick -> MoveAction; Lights.SetHeadlight -> installed VisionSystem -> ProcessImage; routed RobotState -> Sensors; ManipulationSystem DockAsync and cube telemetry; Lights.SetEnableFreeplayLightStates and cube Update; AnimationScheduler.Play/Advance -> PopFrame -> encoder. No helper-only implementation replaces a live component.

New tests cover stale action clock, null vision/error flag plus continued send, mode14 deferred image application/idempotence/Idle mask/logs, actual SetLocalizedTo owner, and fractional/NaN float audio with readiness and zero tail. Existing dock-retention, all/single light-layer, frame-reset/treads-change/first100 cliff tests retained. Expected bytes6F/EF/00/7F/FF and masks4000/1 are independently derived from native instructions/table, not implementation outputs. Head info/warning regressions assert literal source text. No hardware run.

No six-record settlement or frozen evidence/inventory edits. Remaining source and layer gaps are above and in current unresolved/status. This bounded slice does not meet CODEX-BUILDER rule9's whole-layer completion condition: same-layer unchecked rows and gaps remain, so no premature whole-M3/M4 verification packet.
