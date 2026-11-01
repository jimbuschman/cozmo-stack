# Claude check: ChatGPT check 12 (production binding sweep)

Date: 2026-11-01. Report: `20261101-check-12-chatgpt-extraction.md`, run at `6af67958`, before the follow-up batch. It
lists 60 UNBOUND and 55 CONDITIONAL inputs, gives 55 BOUND as a count, and leaves 21 classes unfinished. Spot checks
were by search on `077ba547`. This is a lead list and settles nothing.

**Best result:** row 11. It is a new silent gap, and the kind of defect this sweep was meant to find.

## Rows checked

| Row | Claim | Verdict |
|---|---|---|
| 11 | `BehaviorContext.NeedsActionIds` is set only by FreeplayStack | Right, and new. The canonical context has `Needs` but no IDs, so `BehaviorNeedsActions.Complete` (`Needs.cs:79`) finds no configured action and reports nothing, with no diagnostic. |
| 53-55 | `PyramidTargetInput`, `PyramidBaseStrongInput`, `PyramidFullStrongInput` have no assignment | Right. The Pyramid strategy can now pass, but `NativeBuildPyramidBaseBehavior` still cannot read its targets on the canonical path. |
| 56, 57 | `TallStackOwnerInput`, `TallStackWeakInput` have no assignment | Right. Shipped `cantHandleTallStack.json` reaches this; readiness fails with MISSING. |
| 45 | `RegisterGuardDogDisconnect` is omitted from the canonical factory | Right; only `FreeplayStack.cs:104` binds it. Shipped `guardDog.json` reaches it, with a visible MISSING. |
| 59 | `SelectionChooser.SetAnalyzerEnabled` is unbound on the canonical path | Right; only `FreeplayStack.cs:227`. Visible MISSING. |
| 61 | `RobotState34a` is unbound for reaction-owned behaviours | Right. Without `NativeBaseInputs`, the charger gate is skipped with a MISSING (`SteppedBehavior.cs:277`). |
| 42-44 | Pyramid strategy delegates unbound | Fixed after the sweep's commit (`Activities.cs:725`, `:732`, `:733`) |
| 19 | `ObservationFinished` unbound | Half wrong: `NativeBuildPyramidBehavior.cs:14` assigns it. The base class alone has no assignment. |
| 16-18, 47, 48 | UI game request inputs unbound | Right, and intended: M15-096 does not support that flow, and nothing sets the pending flag. |
| 46 | `DasTransition` unbound | Right, and not reproduced: it has a visible MISSING and only DAS output. |

## Reading it

Most UNBOUND rows are harmless: an unset test override with a correct production fallback (rows 3, 12, 13, 24, 26,
29-32, 34, 35), or a default that matches a known value (rows 20-23). They should have been CONDITIONAL or BOUND, so the
top of the table overstates the problem. Its 21 unfinished classes include most of the native behaviours, which is
where this class of bug matters.

FreeplayStack binds several inputs the canonical path lacks: `NeedsActionIds`, `RegisterGuardDogDisconnect`,
`SetAnalyzerEnabled`. CANONICAL-FREEPLAY-PARENT-1 is the natural job to move them.
