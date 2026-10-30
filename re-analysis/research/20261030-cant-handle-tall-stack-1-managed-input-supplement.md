# CANT-HANDLE-TALL-STACK-1 — narrow managed-input supplement

2026-10-30, resumed reader request **gpt-6.1-sol / medium**; actual backend unobserved. Answers the manager's bounded question about existing host readiness and offline checked-lock inputs. Read-only review of candidate C# interfaces; no native tracing, new recovered behavior, implementation, tests, .NET, hardware, git mutation, official record or approval edits. Historical extraction and source-check reports remain unchanged; their old approved-inventory checkpoint is resolved by the resumption packet, not reopened here.

## Existing host availability mechanism

`Manipulation/NativeBlockConfigurations.cs:45` defines the typed `NativeConfigurationUnavailable`. `Behavior/SteppedBehavior.cs:201..216` has a host-only scoped readiness frame: `MarkReadinessUnavailable()` records missing input, and `EvaluateHostReadiness(context)` returns separate `(Available, Runnable)` values. A direct `IsRunnable` boolean alone does not retain this distinction.

`Behavior/NativeBringCubeToBeaconBehavior.cs:43..47` is an existing usage pattern: catch `NativeConfigurationUnavailable`, mark readiness unavailable, report MISSING and return false. This is a host inability to evaluate the predicate, not evidence that the native predicate evaluates false. `Behavior/BehaviorManager.cs:937..945` consumes `EvaluateHostReadiness` when the selected host activity implements the enabled `IHostReadinessOwner`; unavailable retires that host selection before Init. The ordinary boolean-false branch immediately afterward logs but falls through toward Init. Therefore every admission route used by a new unavailable input must guard Init as well; using only `return false` is insufficient. Existing manager behavior is candidate infrastructure, not new native evidence.

`Manipulation/NativeConfigurationManager.cs:22` exposes `InputsAvailable`, but its `Update()` catch at lines91..97 concerns configuration publication inputs and preserves the previous publication for retry. Its `Tallest()` at lines35..41 returns a currently published object. Neither interface supplies a native weak-control-block lock result or establishes the last native strong owner's release. Publication availability must not be relabeled weak-target availability.

## Bounded offline-input precedent

There is no existing checked weak-lock supplier on `NativeConfigurationManager`, `NativeStackConfiguration`, or `TallStackDelocalizationData`. The latter currently stores a strong host `.Stack` view and explicitly records its native weak-expiry gap (`DelocalizationRecipients.cs:7..18`). Reusing that strong value as proof of native liveness would erase the known gap.

`Behavior/NativeBehaviorRuntime.cs:18..22` already uses explicit nullable native-input suppliers (`PyramidTargetInput`, `PyramidBaseStrongInput`, `PyramidFullStrongInput`). Their comment states that injected offline inputs do not recover their production producers. `BuildPyramidBaseNativeTests.cs:19..20` and `BuildPyramidNativeTests.cs:18..19` supply them explicitly for bounded tests. This is a reusable interface pattern, not a pre-existing weak-lock facility.

An analogous explicit checked-lock input seam may therefore support tests of the already checked tall-stack own lifecycle, provided its contract distinguishes **supplier unavailable** from **supplied weak target expired** and **supplied weak target live**, and permits the live-to-expired state to change between admission and Init. Tests must identify their supplied lock state as an input seam; they do not validate production canonical lifetime or automatic selection. The exact type and plumbing are the builder's host implementation choice. No production supplier has been established by this review.

## Retained boundaries

Canonical last-strong-owner expiry and the actual automatic caller remain **MISSING/UNKNOWN** as in the existing checked reports. Do not infer expiry from GC collection, snapshot membership, or a strong facade reference. No broad owner/caller census was performed. No manifest title/status/evidence/ownership/closure claim is made, so no record reclassification is proposed. The remaining gaps do not prevent building and handing off the bounded own lifecycle with explicit unavailable production input and clearly labeled offline supplied inputs.
