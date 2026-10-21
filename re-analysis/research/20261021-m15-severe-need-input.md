# R-M15-SEVERE-INPUT — current severe-need input

Request: `requests/20261021-m15-severe-need-input.md`. Examined HEAD4f41ecbd. Independent read-only extraction pending checking. Coverage opened before tracing.

| Step | Native / checked source | Production binding | Disposition |
| --- | --- | --- | --- |
| Selected scalar | ctor5729FC;Set5731B4;Clear572D6A | No SevereNeeds live owner | Recovered one NeedId, initial/cleared3 |
| Set side effects | 572E48..57323C; static init4D71B4..7202 | Built driving/idle/reaction recipients, caller unbound | Tables/order recovered |
| Clear side effects | 572C8C..DA6 | No common owner wired to all clearers | Recovered pop/pop/unlock then scalar reset |
| Supported callers | wake572E00;behavior5BFECA/5BFEF8;update572BD0;get-out54505E/5451E8;ExpressNeeds5EE7D8 | HashSet/caller seams instead | Named narrow callers recovered; full behavior reach census UNKNOWN |
| Driving-animation timing | checked driveoff5C0B34..50 and handler M5-063 | Caller remains MISSING | Mutable input must be shared and sampled at native order |
| Remaining ownership | M15-001/045, M7-014, M13-017 | current AI.Update lacks SevereNeeds.Update | Dedicated live owner/record needed; no constant delegate |

## Scope and authority

Primary source: `resources/lib/armeabi-v7a/libcozmoEngine.so` instructions. Reused NI2 in checked Q17 additions, existing needs bracket routines, M5 idle/driving handler bodies, and the drive-off answer/partial check. Read the existing `20261002-R-BEH2-verify-batch3a.md` writer lead and OV93's **adopted correction**, `overnight/20261016-batch-check.md`, for child identity/destructor. OV93 alone is not authority for new semantics.

Fresh full ranges read: Set572E48..5731C0 plus cleanup5731C4..323C (remaining symbol extent is literals); Clear572C8C..D6E plus cleanup572D70..DA6; Update572BD0..C3E plus cleanup572C40..64; Init572A64..AC4 plus cleanup572AC6..AEA; subscription helper572B10..B96 plus cleanup572B9C..BC0; Wake handler572E00..E42; ShouldGetIn5BFECA..EF6/StopInternal5BFEF8..FF16; ExpressNeeds.StopInternal5EE7D8..7E8; reaction-map static initializer4D71B4..71F0 and cleanup4D71F4..7202. Get-out animation action was read only at the indicated caller guard ranges, reusing its other checked animation semantics.

No all-needs/mood or drive-off extraction repeated. No code/tests, manifest/inventory changes, approval, hardware or settlement.

## Scalar, enums and writer ordering

Native AI+30 points to SevereNeedsComponent (AI ctor569B3A/3E). Its +4 holds Robot; +8 subscription vector; **+14 is one current expressed NeedId**. Constructor572A0E initializes3. Shipped `unity/scripts/csharp/Anki.Cozmo/NeedId.cs`:Repair0,Energy1,Play2,Count3; NeedBracketId.cs:Full0,Normal1,Warning2,Critical3. Native EnumToString(NeedId)7820CC indexes0..3 and returns null for greater values; no reinterpretation of3 as Energy or an animation phase.

| Row | Source | Exact behavior-changing sequence |
| --- | --- | --- |
| S1 | 572E48..E84 | Set accepts requested NeedId, logs old and new enum names. No same-value or existing-active guard and no Clear call before mutation |
| S2 | 572EAA..F18 | Lazy initialize idle-trigger map from C596C4 pairs: **1→137h,0→143h**. Missing key skips idle push later |
| S3 | 572F18..F96 | Lazy initialize driving triplets: Need1 **134h/133h/132h**; Need0 **140h/13Fh/13Eh**. Temporary map keyed1 then0 built572F96..573080 |
| S4 | 573082..F0 | If requested key has triplet, direct DrivingAnimationHandler.PushDrivingAnimations at5730E2, fixed name **severe_need_component_lock** (literal BEE67C,26 bytes). Missing key skips driving push |
| S5 | 5730F4..3160 | If idle map has key, direct AnimationStreamer.PushIdleAnimation(trigger,same name) at573152. Missing key skips idle push. Driving push precedes idle push |
| S6 | 573164..31AE | Look up reaction table by requested NeedId in global map105A9D8; call BehaviorManager.DisableReactionsWithLock(same name,table,true) at5731A0 |
| S7 | 5731B2..B4 | **Only after S4/S5/S6** store requested NeedId to component+14. Destroy local map and return |
| C1 | 572C8C..CB0 | Clear logs currently selected NeedId; no “already3” early return |
| C2 | 572CD6..D04 | Direct handler.RemoveDrivingAnimations(same fixed name) at572CF6 |
| C3 | 572D08..34 | Direct streamer.RemoveIdleAnimation(same name) at572D26 |
| C4 | 572D38..64 | BehaviorManager.RemoveDisableReactionsLock(same name) at572D56 |
| C5 | 572D68..6A | **After removals** store3 to component+14. Return |

Source lock text is the same for both setters/removers. Reuse existing checked handler/streamer/reaction lock behavior, including duplicate-name semantics and warnings. Set is not a transaction that first clears a previous selection; Clear is not a test of current selection before removing. Neither body sends a direct robot message or explicit game report; recipient animation scheduling/reaction state and subsequent consumers supply the in-scope effects.

### Reaction-table producer, not guessed “disable all”

Global reaction map is initialized by4D71B4..71F0. It clears tree105A9D8 and inserts three ROM key/pointer pairs from **10237A0** in order Need1,Need0,Need2. Relocations10237A4/AC/B4 are ARM_RELATIVE:
- Need1 andNeed0→**C59670**,21 two-byte (ReactionTrigger enum, bool) pairs. Boolean sequence for indices0..20: **011111011011101111010**.
- Need2→**C5969A**,all21 false.

The generator/layout and DisableReactionsWithLock(table,true) recipient govern meaning; do not replace this with “all reactions disabled.” Raw entry enum bytes are0..20 in both tables. The C59704 address passed to map subscript is **piecewise-construct tag storage**, not the reaction mask or map initializer. Set's573192 obtains the matching table pointer; all three valid NeedIds have entries.

Need2 has no driving or idle map entry but still passes its all-false reaction table and stores2. **UNKNOWN:** shipped supported configuration witness that selects Play2 through the behavior caller; the defined valid-enum path is established, not declared unreachable. Unknown out-of-range enum behavior is not a new supported host input; no arbitrary fallback mask added.

Repeated Set can push same-name entries without removing old ones, depending on requested key. Since handler Remove removes first matching name, a blanket “one push always popped” ownership model would be wrong for arbitrary repeated inputs. This is a conditional sequence derived from Set and checked handlers, not proof a particular repeated schedule occurs in shipped activity selection.

## Supported producer and clearer table

| Caller / input | Instructions and gates | Live effect / source limit |
| --- | --- | --- |
| NotifyCozmoWakeup | Init572A6A..86 subscribes only HasExternalInterface()==1. Helper572B18/30 registers G2E **A6h=166** and retains scoped handle. Captured callback invoke5734F2..3504 extracts typed G2E166 at5734FA and forwards the saved component to the wake handler through8CB83C. Handler572E0A..20 checks Repair critical and Energy critical; if Repair result exactly1 choose0 via572E30; else if Energy exactly1 choose1 via572E42; else return | Both queried before selection; Repair wins if both critical. No clear when neither critical, no scan for Play |
| PlayAnimOnNeedsChange.StopInternal | ShouldGetIn5BFECE..EF6 reads current scalar and configured behavior+13C NeedId, then NeedState.IsNeedAtBracket(configNeed,Critical); bool result AND(needs differ). Stop5BFF02 requires exactly1, then tails Set through5BFF16→8CB81C→PLT4AD2DC | Stop-time condition, **no requirement this stop was successful completion** in this body. Full configured behavior factory/activities remain their existing M7 owner |
| SevereNeeds.Update | 572BD6 checks selected!=3. Read **selected need's current bracket** at572BE6..EA. If nonzero result, keep; false logs and Clear572C38. If selected3, no query/clear | It does not choose a newly critical need; no highest-score or periodic “pick severe” algorithm |
| BehaviorExpressNeeds.StopInternal | 5EE7D8 tests own+134; nonzero gets shared component and tails Clear5EE7E4→8CC62C→PLT4AAEDC | Caller flag's complete config/behavior lifecycle **UNKNOWN here**; explicit conditional clearer, not unconditional all-behavior stop |
| PlayNeedsGetOutAnimIfNeeded destructor | 54504C..5E: get shared selected; if!=3 and own+C9==0 call Clear54505E | Reuse checked action destruction; cancelling/destructing can reach a clear, not just successful game completion |
| Same get-out CheckIfDone | 5451C8..EE: trigger+B8 !=23Fh, own+C9==0, own+88!=0 →Clear5451E8, then own+C9=1 | Narrow guard preserved; full trigger action lifecycle belongs existing animation/action records |
| DriveOffCharger | Partial manager check5C0B34..50 | Consumer only: scalar==3 pushes its launch triplet/name, then own+120=1. Does not set/clear SevereNeeds scalar |

A finite .text immediate branch scan through body/known PLTs/veneers found Set branches572E30,572E42,5BFF16 and Clear54505E,5451E8,572C38,5EE7E4. It does not prove completeness against indirect callers, adjusted aliases or unindexed fragments. The old broader behavior notes mentioning another clearer are leads until an actual native call is found; no additional source-backed caller is invented.

Native scalar mutation sites traced here: ctor572A0E, Clear572D6A, Set5731B4. **UNKNOWN:** exhaustive arbitrary-alias/bulk-store census. These positive named writers are sufficient to refute treating constructor3 as a permanently recovered value.

## App input and engine scheduling

Ordinary supported app wakeup exists: `unity/scripts/csharp/ConnectionFlowController.cs`:WakeupSequence600..611, outside its SDK and simulator branches, sends wake animation then NotifyCozmoWakeup. `Cozmo.Hub/NeedsHub.cs`:95 sends it in the non-GUID deep-link/near-charger branch. These are authority2 sender evidence, not a requirement to reproduce the Unity UI. The standalone input replacing this app notification needs an explicit app-input compatibility record; this research neither excludes the input nor decides to send it automatically at every connection.

No new network field is needed for the scalar itself: it is engine state derived from needs/bracket state and supported event/behavior calls. Missing wakeup transport alone does not justify assuming3 because the StopInternal producer is independent.

Checked engine timing569F24..4C calls AI analyzer→whiteboard→**SevereNeeds.Update**→helper→tracker, before BehaviorManager and ActionList (existing TK2). Thus a need that has left Critical can clear its expression **before** drive-off Init samples the scalar. A behavior stop may Set after that AI phase; drive-off later sees the committed value when selected. Preserve actual input/recipient calls rather than periodically copying a host HashSet.

Set publishes scalar after driving/idle pushes and reaction lock; Clear publishes3 after all three removals. Drive-off's own launch override uses its own behavior name, distinct from severe_need_component_lock; neither pop is a substitute for the other. Handler.Current is selected at drive.Init, so direct push ordering affects concrete robot animation traffic later. Existing idle streamer timing remains checked M5 ownership, not a new immediate robot-send assumption.

Scalar also feeds StrategyExpressNeedsTransition6136D8..3706, IBehavior required-severe-need gate5BD7D4..802 and cliff/docking selection (reused NI1/RB2 and prior checked behavior rows). These are runnability/response/downstream-state effects in project scope. No explicit readiness flag or direct scalar API event was found in the named writer bodies; full recipient/API consequences beyond checked existing boundaries are **UNKNOWN here**, not called “logging only.”

## Current C# / owner comparison

- `Behavior/AIComponent.cs`:Update calls Analyzer.Update and Helpers.Update, omitting SevereNeeds.Update. Constructor builds no live SevereNeedsComponent. Teardown units under M15-045 are storage/finalizer models, not a current scalar provider.
- `Behavior/Needs.cs`:670 _severeExpressed is **HashSet<NeedId>**.2079..80 Is/SetSevereExpressed and2094 per-bracket removal represent multiple independent flags; they cannot store one currently selected NeedId with overwrite and ordered side effects.
- `Behavior/ExplorerBehaviors.cs`:469 ShouldGetIn checks critical and !IsSevereExpressed(Need).482 sets that per-need flag only if _played or stop reason Completed. Native ShouldGetIn compares selected scalar; native StopInternal re-evaluates that condition independent of stop result. Current behavior may both retain multiple expressed flags and skip a native interrupted-stop Set.
- `Behavior/IBehavior.cs`:69 AiExpressedNeedValue and `Manipulation/ManipulationSystem.cs`:76 equivalent seam exist; production assignment search found none. Other consumers use visible missing/default3. A constant delegate would conceal mutable source behavior.
- DriveOffCharger70 still reports its push input MISSING. The accepted handler is present; live input owner is missing.
- No production G2E A6 subscription/wakeup equivalent for this owner was found by NotifyCozmoWakeup-name search. A test event/direct setter would not establish that binding.

Current record search: no dedicated live SevereNeeds Set/Clear record by title/evidence was found. M15-045 mentions the SevereNeeds **destructor**, not these live writer semantics. M15-001 owns actual needs state/brackets, M7-014 reaction locking, M13-017 drive-off consumer. Manager should assign the live owner and its whole path before settling its consumers; do not stretch a teardown record to cover live Set/Clear.

## Minimal live owner and independent oracles

After manager checking: an AI-owned SevereNeeds component with one selected NeedId initialized3; shared NeedsState bracket recipient, actual fixed-name driving/idle/reaction recipients; exact Set/Clear order; AI phase Update; supported A6/behavior/get-out clear/set callers. All consumers read that same mutable scalar. Reuse existing checked needs/animation/reaction implementations. Additional record/app-input choice belongs manager/operator process, not an invented constant or product exclusion.

Independent regression expectations (not tests added/run):
1. Set0/1 invokes exact triplet then idle trigger then exact reaction table before scalar publication; Set2 skips triplet/idle but still reaction call and scalar2.
2. Clear even from3 performs remove-driving→remove-idle→remove-reaction→store3; callbacks observe old scalar until removals finish.
3. Both Repair and Energy critical at wake: query both, SetRepair; Energy-only setsEnergy; neither critical leaves selection unchanged.
4. Stop needs-change while configured need is Critical and differs from selected sets it even after interruption; same selected or no longer critical does not. Multi-valued expressed-set fixtures are not the oracle.
5. Selected need leaves Critical: AI update clears before next behavior Init; no automatic selection of another need. Selected3 skips bracket query.
6. Get-out action destruction/check guards invoke shared Clear exactly as source, retaining M8 queue destruction/cancellation ordering. Use actual concrete action, not success-only task mock.
7. Drive-off gate observes selected scalar and its preceding pushes/removals; severe lock name and behavior lock name remain independent. Test repeated Set conditional stack behavior without inventing deduplication.
8. Wakeup input fixture must exercise real delivery/subscription; direct handler invocation is unit reach only. App-input policy remains explicit.

Remaining UNKNOWN: full current behavior/config witness for Play2; exact full-production callback/API consequences beyond existing accepted recipients; exhaustive indirect caller/alias census; standalone replacement timing of app wakeup input pending compatibility decision; full ownership integration for get-out action/behavior lifetimes. No all-needs/mood audit, no claim that every supported consumer is complete.

## Current manifest snapshots

Exact title/status/evidence fields from current manifest, unchanged. These are related owners; no dedicated live record is silently created or status upgraded here.

### M7-014

```json
{
  "title": "Reaction-lock manager lifetime and concrete per-class lock tables",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "IBehavior::SmartDisableReactionsWithLock 0x005bce3c",
    "BehaviorManager::DisableReactionsWithLock 0x005a27e8",
    "BehaviorManager::RemoveDisableReactionsLock 0x005a3a48",
    "13 concrete 21-entry tables in M7 inventory Appendix F"
  ]
}
```

### M13-017

```json
{
  "title": "BehaviorDriveOffCharger: runnable on the charger, the drive distance and the leaving-the-contacts update",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "ctor 0x005C0980 stores 96.0 + json extraDistanceToDrive_mm at +0x11C (0x005C09D4/0x005C09E2/0x005C09E6); IsRunnableInternal 0x005C0B10 returns robot+0x34A (the on-charger-platform flag; contacts are +0x338)",
    "InitInternal 0x005C0B18 takes the reaction lock, pushes driving animations when the animation state is 3 (0x005C0B4A) and transitions only when robot+0x355 == 0 (0x005C0B54); +0x355 is the robot's OffTreadsState (OnTreads = 0), written only by Robot::CheckAndUpdateTreadsState 0x00512088/0x0051208E and named by OffTreadsStateFromString 0x0078DF58 (OnTreads 0, InAir 1, OnBack 2, OnLeftSide 3, OnRightSide 4, OnFace 5, Falling 6)",
    "TransitionToDrivingForward 0x005C0BB8 drives +0x11C (0x005C0C02/0x005C0C08)",
    "UpdateInternal 0x005C0DA8: once off the platform (robot+0x34A == 0) it records the time at robot->[+0x264]->[+0x18]+0x44 and returns 2 (0x005C0DF4..0x005C0E0A); while still on the platform and robot+0x355 != 0 it calls StopActing(false,false) (0x005C0DB6/0x005C0DC4) and waits; on the platform with +0x355 == 0 it transitions to driving forward (0x005C0E18)"
  ]
}
```

### M15-001

```json
{
  "title": "NeedsManager initialization, periodic update and need-bracket rules",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "CozmoEngine::Init 0x004EC9E6..0x004ECA0A",
    "NeedsManager::Init 0x00692574",
    "NeedsManager::Update 0x00695C9C",
    "NeedsState::UpdateCurNeedsBrackets 0x0069C12C",
    "needs_config.json",
    "NeedsManager::Init time threading 0x004EC9DA..0x004ECA0A, 0x0069257E, 0x006926CE, 0x006934B0, 0x0069358A..0x0069359E",
    "NeedsManager::PossiblyWriteToDevice 0x00695DC4 (0x03A2C940 against a microsecond clock = 61 s; ApplyDecayForTimeSinceLastDeviceWrite divides by 1e6 at 0x0069532C)"
  ]
}
```

### M15-045

```json
{
  "title": "AIComponent teardown recursive recipients: BehaviorHelper IHelper types and callables, ObjectInteraction entries and ProcessInfo callables, BorderSegment release, SevereNeeds/RequestGame/AIWhiteboard handle inventories, Workout/ObjectInteraction/Whiteboard/Analyzer inner bodies",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261014-ov25-26-27-check.md: 0x00569D38..0x00569D66; 0x0056A0AE..; 0x00569FD2..; 0x00569F9A; the inner bodies named.",
    "re-analysis/research/overnight/20261016-batch-check.md: member map 0x00569A88..0x00569B3E; BehaviorHelperComponent 0x00569D30..0x00569D7E; ~vector<BorderSegment> 0x00569F86..0x00569FB8; ProcessInfo tree 0x00569FBA..0x0056A006; ObjectInteractionCacheEntry 0x0056A06E..0x0056A13E; SevereNeeds 0x00572A1C..0x00572A36; AIWhiteboard 0x0056A32C..0x0056A37C; RequestGame 0x0056A1DC..0x0056A1FA; Workout 0x0056A140..0x0056A1D8."
  ]
}
```
