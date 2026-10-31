# CUBE-LIFT-WORKOUT-1 managed candidate clarification

Read-only clarification, 2026-10-30, requested by the manager after the two narrow source answers. Native instruction facts in `20261030-cube-lift-workout-1-score-arithmetic-supplement.md` are unchanged; that supplement makes a conditional arithmetic statement and does not establish that the existing GraphEvaluator is broken. No prior report is edited or replaced.

Inspected the existing managed candidate in manager and builder checkouts: `cozmo-stack/src/Cozmo.Robot/Behavior/Activities.cs` contains Graph2d.EvaluateY delegating to GraphEvaluator.EvaluateY. Despite a double-facing API, GraphEvaluator casts the input and nodes to float, computes span, t, scaled and final sum in float, and uses float epsilon bits 0x3727C5AC. Therefore the builder's initial description of graph evaluation as double was too broad. No GraphEvaluator port/change is required by this narrow answer.

The old `WorkoutConfig.EvaluateEmotionScore` in `Manipulation/Workouts.cs` was the relevant difference: it subtracted current/history as double, used double epsilon 1e-5, accumulated graph outputs into double sum and divided in double. That function's delta, epsilon, ordered addition and mean require the binary32 boundaries cited in the native supplement. The inspected builder candidate now uses float current/history subtraction, float y, GraphEvaluator.Epsilon, float sum and a float count divisor. This statement checks the narrow arithmetic scope; it is not an independent whole-diff verification, test result or record-closure claim.

For the native-workout call chain, the float scorer result reaches the existing round-away-from-zero helper as an exactly representable double. No new double graph arithmetic follows from that API type. Exceptional conversion inputs remain outside the finite shipped-values claim in the native supplement.

No code, tests, official records, inventory, approval, git or .NET changes were made by the extractor.
