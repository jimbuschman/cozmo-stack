# Claude check: row 39, R-M15-SEVERE-INPUT (the SevereNeeds selected need)

- **Checked:** `research/20261021-m15-severe-need-input.md` (answered 3609db0c). The manager (Opus) disassembled every
  range below from `resources/lib/armeabi-v7a/libcozmoEngine.so` (capstone, Thumb) and compared it with the answer.
- **Verdict: PASS.** New records M15-072 (the live component, to build) and M15-073 (the NotifyCozmoWakeup app input).
  Builder job B-M15-12. No record settles.

## Confirmed by disassembly

| Claim | Instructions |
| --- | --- |
| ctor +0x14 = 3 | 0x00572A0C `movs r2,#3`; 0x00572A0E `str r2,[r0,#0x14]` |
| AI phase order | 0x00569F2C Analyzer, 0x00569F32 Whiteboard, **0x00569F38 SevereNeeds::Update**, 0x00569F40 BehaviorHelper, 0x00569F46 FreeplayDataTracker |
| Update | 0x00572BD8 `cmp r0,#3; beq` return; 0x00572BEA IsNeedAtBracket(selected, 3); `cbnz` keeps; else log, then Clear at 0x00572C38. It never selects a need |
| Clear order | 0x00572CF6 RemoveDrivingAnimations (handler robot+0x24C), 0x00572D26 RemoveIdleAnimation (robot+0x60), 0x00572D56 RemoveDisableReactionsLock (robot+0x44), each with a 26-byte literal; then 0x00572D68/6A store 3. No early return when already 3 |
| Lock literal | 0x00BEE67C = `severe_need_component_lock` (26 bytes + NUL) |
| Wake handler | 0x00572E14 IsNeedAtBracket(Repair 0, 3), 0x00572E20 IsNeedAtBracket(Energy 1, 3); Repair == 1 → Set(0) (0x00572E30); else Energy == 1 → Set(1) (0x00572E42); else return. Both are queried before choosing |
| Idle map | 0x00C596C4: `(1, 0x137), (0, 0x143)` |
| Driving triplets | 0x00572F36..54 stores 0x134/0x133/0x132 at 0x0105A9F8; 0x00572F78..92 stores 0x140/0x13F/0x13E at 0x0105AA08. The temporary map takes key 1 (`[sp+0x20]=1`) with 0x0105A9F8 and key 0 (`[sp+0x30]=0`) with 0x0105AA08 (0x00572F96..0x00572FC8). So **Energy → 0x134/0x133/0x132, Repair → 0x140/0x13F/0x13E** |
| Set order | PushDrivingAnimations 0x005730E2 (only when the key is found), PushIdleAnimation 0x00573152 (only when found), DisableReactionsWithLock(lock, table, `movs r3,#1`) 0x005731A0, then `str r0,[r6,#0x14]` at 0x005731B4. No Clear first and no same-value guard |
| Reaction tables | 0x010237A0: `(1, 0xC59670), (0, 0xC59670), (2, 0xC5969A)`. 0xC59670 booleans 0..20 = `011111011011101111010`; 0xC5969A all false |
| PlayAnimOnNeedsChange | ShouldGetIn 0x005BFECA..F6: (config need +0x13C != selected) AND IsNeedAtBracket(config need, 3). StopInternal 0x005BFEFE calls it; if 1, tail-calls Set(config need) at 0x005BFF16. No stop-reason test |
| ExpressNeeds stop | 0x005EE7D8 `ldrb [r0,#0x134]`; nonzero → Clear tail 0x005EE7E4 |
| Get-out action | dtor 0x0054504C..5E: selected != 3 and own+0xC9 == 0 → Clear. CheckIfDone 0x005451C8..EE: trigger+0xB8 != 0x23F, own+0xC9 == 0, own+0x88 != 0 → Clear, then +0xC9 = 1 |

## Current stack (checked)

- `Needs.cs:670` keeps a per-need `HashSet` (`_severeExpressed`), cleared per need when its bracket leaves Critical
  (`:2094`). The engine keeps one selected value that Set overwrites. The HashSet is not that value.
- `AIComponent.Update` has no SevereNeeds phase.
- Consumers that read a seam or the HashSet:
  - `BehaviorContext.AiExpressedNeedValue` (M8-006; `Behaviors.cs:229`; `CliffPickupBehaviors.cs:161`);
  - `ChargerBehaviors.cs:82` (drive-off's `SevereNeedsState` MISSING);
  - `ExplorerBehaviors.cs:469/482` (ShouldGetIn / stop use the HashSet, and set only on played or Completed);
  - **`Activities.cs:805`**, the activity strategy `SevereNeedTransition`. The answer did not list it.
- The recipients exist: `CozmoAnimations.PushDrivingAnimations`/`RemoveDrivingAnimations` (`:253`/`:258`),
  `AnimationScheduler.PushIdleAnimation` (`:1106`), `BehaviorManager.DisableReactionsWithLock(lock, table, stop)` and
  `RemoveDisableReactionsLock`.
- No PlayNeedsGetOutAnimIfNeeded action exists in the stack.

## Decisions (manager)

- **M15-072** owns the live component's whole path: scalar, Set, Clear, Update, the AI phase slot and the native
  callers. M15-045 stays the teardown record.
- **M15-073**, the app input: the host delivers NotifyCozmoWakeup through a C# API call into the same G2E 166
  subscription; the stack does not send it by itself. This is recorded as a policy when built. **Auto-send after
  connection is left for the operator.**
- **Not taken from the answer:** nothing is a constant delegate. The ExpressNeeds +0x134 writer and the get-out action
  stay visible MISSINGs.
