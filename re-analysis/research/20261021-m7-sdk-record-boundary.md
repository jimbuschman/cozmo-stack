# R-M7-SDK-BOUNDARY — M7-024 supported recipient boundary

Request: `requests/20261021-m7-sdk-record-boundary.md`. Examined HEADb50df484. Independent read-only scope check, pending checking. Coverage opened before tracing.

| Step | Native / existing source | Supported reach / disposition |
| --- | --- | --- |
| Named sdk-lock dispatch | ResetRobot65DCF8;true branch65DD12..DDA8 | Identified producer belongs SDK reset lifecycle |
| Entry / disconnect gates | Enter65E104..E1F8;OnDisconnect65E4F0/5C6/5D0 | Unsupported external/internal SDK mode; not ordinary Robot teardown |
| Connection-success latch |660C6A..C88→IsExternalSdkConnection65FE10 | Positive caller gate excludes UI connection0 |
| Shared remove receiver | G2E160;5A4EA6..EBA→5A3A48 | Supported general receiver, already M7-014/M10-004; cannot exclude by SDK name |
| Special name state |5A3A52..8E→manager+4C;5A3558..357A | “sdk” opens sticky reaction gate, even if no named lock exists |
| Scope conclusion | Existing operator SDK exclusion | Bound M7-024 to identified SDK dispatch; preserve shared supported owners and indirect UNKNOWN |

## Conclusion

**The current evidence's specific ResetRobot(true)→RemoveDisableReactionsLock("sdk") producer is an unsupported SDK lifecycle path. The common receiver is supported and has real downstream effects.** M7-024's receiver-UNKNOWN text is stale: its state transitions are already recovered under current M10-004/M7-014 and present in C#.

Recommend scoping M7-024 explicitly to **“SdkStatus.ResetRobot(true) sdk-lock removal dispatch”** and applying the existing SDK compatibility policy to that producer only, after manager checking. No new product exclusion is needed or decided here. Do not mark the generic named-lock API or special-name semantics unsupported; they have their existing supported owners.

If the manager intends M7-024 to own the **whole generic receiver** rather than its one listed dispatch, the record is mixed/duplicative: transfer receiver semantics to existing M10-004/M7-014 and keep the SDK-origin dispatch as the policy slice. Creating a second generic implementation record would duplicate current ownership.

## Evidence boundary

Primary instructions in `resources/lib/armeabi-v7a/libcozmoEngine.so`; decomp only navigates. Reused checked E/Reset rows, the SDK-boundary S1–S4 in `20261009-M3M4-remaining-rows.md`, current M1-051 policy and Q17 LK4/M10 C3 sticky-gate evidence. Fresh selected reads:
- Reset entry/true removal branch65DCF8..DD42,65DD78..DDA8 and false-branch prefix65DD44..76; no whole Reset re-extraction.
- Enter prefix through mode writes/reset65E104..E204; OnDisconnect65E4D8..E60A through reset gates/connected clear; Exit65E2A4..2EA and narrow OnDisconnect(true) call65E35C..368. No complete telemetry/exit body claim.
- SDK ctor65DC8A..DCCC; connection callback prefixes65E6E0..E76E and commit tail65E8C8..93C, with logging middle reused only as existing checked SDK scope.
- UiMessageHandler exact SDK-type gate660C6A..C88; IsExternalSdkConnection valid-enum branch65FE10..2A.
- Remove union constructor74E1EA..206, typed callback5A4EA6..EBA, receiver special-name prefix5A3A48..9A and reaction-gate read5A3550..3580. General receiver body reused LK4, not extracted again.

No code/tests, manifest/inventory changes, approvals or hardware. No other SDK-labelled record is reclassified in bulk.

## Positive producer/caller gates

| Row | Instructions | Reach established |
| --- | --- | --- |
| K1 |65DD06/12 | ResetRobot takes bool. Argument exactly1 takes remove-lock branch; other values go to SDK disable-lock branch65DD44. The record's removal is not every Reset call |
| K2 |65DD14..26,65DD90..DDA6 | Build fixed name “sdk”, move into RemoveDisableReactionsLock union at65DD9A, invoke external interface virtual+C at65DDA2, then ClearCurrent.65DD9A is **message construction**, not the actual manager Remove body |
| K3 |74E1EA..206 | RemoveDisableReactionsLock constructor sets union tag **A0h=160**; payload is the moved lock-name string |
| K4 |65E11A..17A | EnterMode records external flag+79 or internal+7A/+78. If neither mode was already active, it pushes SDK idle override then calls ResetRobot(**false**) at65E1F8. Thus entry's known call cannot execute M7-024's remove branch |
| K5 |661510..1E | UiMessageHandler EnterSdkMode handler extracts typed message and passes external/internal byte to SdkStatus.EnterMode; this is explicit SDK mode entry. Existing operator policy excludes external programs **and internal CodeLab/Edu SDK mode** |
| K6 |65DCC0/2 | SdkStatus constructor zeroes connected+78/mode+79/+7A and sets reset-on-disconnect+7C=1. Default disconnected state does not reset merely because a normal robot disconnects |
| K7 |65E4F0..4F6;65E5C6..D0 | OnDisconnect returns when connected+78=0. Otherwise reset requires+7C!=0; forwards its own bool argument to ResetRobot at65E5D0.65E5FE..600 clears connected after reset/block-pool handoffs |
| K8 |65E35C..360 | ExitMode passes **true** to OnDisconnect. Combined with K7, this is the identified removal producer. It is not a Robot::~Robot call |
| K9 |660C6A..C88 | UiMessageHandler UiDeviceConnectionSuccess handling extracts connection type and calls IsExternalSdkConnection. Only result==1 tail-calls SdkStatus.OnConnectionSuccess via8CD37C→PLT4BB208 |
| K10 |65FE14..28;UiConnectionType.cs | Valid type0 UI→false,1 SdkOverUdp→true,2 SdkOverTcp→true. Invalid>=3 enters defined error branch, not a supported ordinary-UI bypass |
| K11 |65E6F8..710,65E750..56,65E8CC..8FA | SDK success callback can set+78=1 even when+79==0. Therefore the **outer connection-type gate K9** is necessary; its name alone would not prove SDK-only reach. Commit sets connected, timestamps/counters and reset-on-disconnect flag |
| K12 |65E170..176 | Internal SDK entry directly sets connected+78 and internal+7A; this also requires excluded SDK entry. It is not a supported standalone needs/readiness source |

Fresh finite immediate-branch scan found ResetRobot body/PLT callers at65E1F8 and65E5D0, consistent with checked S1. The only direct EnterMode PLT call found is66151E. The known OnConnectionSuccess veneer is8CD37C, reached at660C88 after K9.

**UNKNOWN:** all indirect/export/manual callers, arbitrary mode/latch aliases and unindexed fragments. These finite searches are not a universal proof. The policy recommendation applies to the positively gated **identified production paths**, leaving other purported supported input paths for specific evidence rather than assuming absence.

## Shared receiver and effects that remain supported

The typed G2E callback5A4EA6..EBA extracts RemoveDisableReactionsLock at5A4EAE and forwards captured manager/string to its common receiver via8CBCBC→RemoveDisableReactionsLock5A3A48. No SDK-mode check in this callback.

The common receiver is not “sdk-only”:
1. Fresh prefix5A3A52..7E tests exact string length3/content “sdk”.5A3A84..8E ORs that match into manager's sticky reaction gate+4C, regardless of whether any trigger actually holds that name.
2. Existing checked LK4 then traverses trigger map in order; where the name exists, removes it. When resulting lock count0, re-enables each registered strategy through virtual+1C(robot,true) in vector order. Missing-name ordinary locks have no effect on those strategies.
3. Existing M10 C3 / fresh5A3558..357A ORs sticky gate with **!ActionList.IsEmpty**; if gate staysfalse, no strategy scan. A prior “sdk” removal can therefore enable a later supported reaction scan. It is not merely log cleanup.
4. Ordinary behavior Stop removes its named scoped lock through IBehavior.SmartRemoveDisableReactionsLock5BD48C; SevereNeeds.Clear572D56 and other supported owners use the same manager receiver. Their robot/run/reaction consequences remain supported.

Current `Behavior/BehaviorManager.cs`:256..272 has the common Remove implementation;258 sets _reactionGateOpen when lockName=="sdk";130 initializesfalse;305..306 opens it after a nonempty ActionList and returns early otherwise. The statement in its comment that sender “sdk” is outside the app is not a blanket exclusion: SDK Reset itself is a native positive sender. Source semantics of common C# API RemoveDisableReactionsLock(name) must remain exact for any supported name admitted by that API.

Authority2 ordinary sender witnesses: `unity/scripts/csharp/ConnectionFlowController.cs`:622/662 removes onboarding/wakeup names, and `Cozmo/PauseManager.cs`:260/377 uses common named-lock removal. These show shared API use beyond SDK; standalone app-input substitution is governed by existing policy, not newly scoped here. No unsupported UI flow is being ported by this report.

No supported ordinary producer of **this fixed sdk name** was established outside the identified SDK lifecycle. That limits the dispatch record; it does not mean the name test or generic receiver has no observable effect. A host manually selecting the string is not automatically SDK entry, and this answer does not remove a supported C# API feature.

## Current C# / manifest boundary

- Current M7-024:RECOVERABLE_GAP/live_path=true, only evidence “RemoveDisableReactionsLock(\"sdk\") dispatch at0x0065DD9A; concrete body UNKNOWN”. The body is now recovered elsewhere and current title “SDK reaction-lock recipient” obscures sender versus shared receiver.
- M10-004:IMPLEMENTATION_GAP/live_path=true owns sticky gate/map order/lock messages. M7-014:IMPLEMENTATION_GAP/live_path=true owns reaction-lock manager and concrete tables. Both must retain supported receiver semantics; no downgrading or new exemption.
- M1-051:COMPATIBILITY_POLICY/live_path=false already records SDK entry/lifecycle exclusion under operator's app boundary. This is the applicable existing decision, not a new research decision.
- `SdkExit.cs`:ResetSdkRobot86 emits SdkHandoff("RemoveDisableReactionsLock","sdk"). It is private and called only when ExitSdkStatus sees SdkWasConnected and SdkResetOnDisconnect. Those fields defaultfalse/true; no production setter to connectedtrue was found by inspected source names. Public ExitSdkMode exists, but that alone does not bypass the connected guard. Unit field seeding or custom SdkExitExternalInterface delegate is not supported production reach.
- Its generic SdkHandoff emits MISSING when unbound; binding every SDK reset recipient is not required to reproduce supported SDK-excluded features. Do not remove the common lock API or reaction gate to silence it.
- Current M8-018 also names SDK Reset dispatch but is outside this request; no status recommendation applied to it in bulk.

## Bounded recommendation and checks

Manager can make M7-024 the explicit SDK-origin dispatch compatibility-policy slice, citing the existing operator exclusion, positive gates K1–K12 and finite-census limits. If preserving a separate receiver scope is desired, split that scope out and use current M10-004/M7-014 citations; no duplicate implementation invented. Record unresolved indirect reach as UNKNOWN rather than “no effect.”

Independent source-derived regression expectations (no tests added/run):
- Generic supported named-lock removal must still re-enable strategies only after final lock removal; “sdk” opens sticky reaction gate even absent that name in map. Both are receiver-unit facts, not a test of SDK support.
- A normal UI type0 connection-success path does not invoke SdkStatus.OnConnectionSuccess; types1/2 select it on the original. Existing unsupported-mode policy is applied at that typed input boundary.
- No connected SDK state: ResetRobot handoff does not happen during ordinary ExitSdkMode/status cleanup. Field-seeded tests are explicitly unit reach.
- EnterMode's initial Reset(false) is not the fixed-name removal branch; Exit's OnDisconnect(true) can reach it only with connected/reset flags. No inferred direct Robot destructor reset.
- Ensure any future generic C# lock API tests remain tied to M10-004/M7-014 rather than requiring unsupported SDK mode.

Remaining uncertainty is explicit above. No claim about other SDK-labelled telemetry, camera/light/cube APIs, SDK-status debug readers or arbitrary external-message admission is made.

## Current manifest snapshots

Exact current M7-024 fields include unresolved; related supported/policy owners retain their existing classifications. No edits performed.

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

### M10-004

```json
{
  "title": "CheckReactionTriggerStrategies: sticky action gate, map order, disable locks, predicates, StopAllMotors and track unlock, no-break loop, IsReactionTriggerEnabled and lock messages",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "C3 gate 0x5A355A..0x5A357A; C4..C8 0x5A359A..0x5A37CA",
    "gap1 4a IsReactionTriggerEnabled 0x5A40B8; 4b DisableReactionsWithLock 0x5A27F6..0x5A2960; 4c RemoveDisableReactionsLock 0x5A3A52..0x5A3B94; 4e game messages 0x5A4D2A..0x5A4F3A",
    "R-ANIM pre-extraction part 2 item 3.1..3.7: CompletelyUnlockAllTracks 0x640F84..0x641092 (sets MC+0x28+12k, index payload 0x64101A)"
  ]
}
```

### M1-051

```json
{
  "title": "SDK mode entry and lifecycle state writers",
  "status": "COMPATIBILITY_POLICY",
  "evidence": [
    "EnterMode 0x0065E104..0x0065E224 writes +0x79 or +0x78/+0x7A on distinct branches and may ResetRobot; ExitMode 0x0065E2A4..0x0065E43E clears +0x79/+0x7B and dispatches OnDisconnect; OnDisconnect 0x0065E4D8..0x0065E620 gates reset and clears +0x78; OnConnectionSuccess 0x0065E6E0..0x0065E940 writes +0x74/+0x78/+0x70/+0x7B/+0x58/+0x7C and may send communication callback. UiMessageHandler OnEnter calls EnterMode then UpdateIsSdkCommunicationEnabled (0x0066150C..0x00661538). +0xE1 writer census and full callback descendants remain UNKNOWN. Source: 20261006-M1M2-missing-triage.md E6; reopened libcozmoEngine.so."
  ],
  "unresolved": "App-boundary scope rule (operator, 2026-10-09; AGENTS.md \"Scope: the app boundary\"): SDK mode (the bridge for third-party SDK programs) is not supported by the stack; SDK mode is never entered, so its entry and state writers are unreachable here. The robot-side exit path (M1-025) stays exact. Before: RECOVERABLE_GAP: build confirmed local SDK state transitions and gates. Keep UI byte +0xE1 writers and untraced callers UNKNOWN; OnConnectionSuccess callback tail requires the checked E7 target records.\nManager (2026-10-18; research/20261018-m1m2-policy-confirmation.md): confirmed for the entry writers: EnterMode/OnConnectionSuccess are unreachable. ExitMode (0x0065E2A4..0x0065E43E) is built and reachable through the public CozmoEngine.ExitSdkMode (SdkExit.cs:27) under M1-025; SdkWasConnected is never set in production, so the reset branch is unreachable.",
  "live_path": false
}
```

### M7-024

```json
{
  "title": "SDK reaction-lock recipient",
  "status": "RECOVERABLE_GAP",
  "evidence": [
    "RemoveDisableReactionsLock(\"sdk\") dispatch at 0x0065DD9A; concrete body UNKNOWN (triage E2)."
  ],
  "unresolved": "Recover named-lock receiver/state transitions; do not infer from message name.",
  "live_path": true
}
```
