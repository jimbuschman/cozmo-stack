# AI-BODY-TURN-TO-OBJECT-1

## Claims and evidence

- Direct G2E 0x30 and nested action 0x27 share registry row 0x010203FC and wrappers 0x00526234–0x0052625C. Their factory at 0x0052B0CC–0x0052B154 accepts a signed object ID, normalizes maximum radians, forwards verify and tracking flags, then applies the six motor setters. The bounded C# command supplies an explicit nonnegative signed ID, finite maximum, false for both flags and six zero setter inputs. The native -1 selected-object branch at 0x0052B0D4–0x0052B0DC stays separate. The host command uses the existing guarded AtEnd, zero-retry path.
- The short constructor at 0x00549D78–0x00549DAE supplies AnyCode to the retained object turn. Init looks up the object in the current origin at 0x0054A21C. Missing object returns 0x03000004. The AnyCode path calls the original closest-marker profile at 0x0054A258–0x0054A27C. It skips failed relative-pose conversions, ranks remaining markers by planar f32 squared distance and keeps the first tie at 0x0087782C–0x00877892. No valid marker returns 0x03000002 through 0x0054A2B8–0x0054A2F2. The retained TurnPose runner uses the actual MAIN pose and calibration after goal assignment at 0x0054A6D8–0x0054A6EA.
- First success or angular skip reaches one Reset(false) and pan tolerance update at 0x0054AE48–0x0054AE66. This re-enters the live object and marker selection on the next pass. The second pass returns success without optional tracking or verification through 0x0054AF60. Other results, motor ownership, cancellation and timeout remain on the retained runner.
- Action type 0x2C uses the ObjectInteractionCompleted union branch at 0x0053FF1A. The virtual at 0x0054B020–0x0054B036 writes the object ID, count 1 and flag false. Four intervening words are undefined native stack contents. The typed completion exposes the defined fields with IsPayloadDefined false under M1-034. The task observes the terminal state after the exact original-owner runner is torn down and unlinked.

## Files and tests

- Motion.cs exposes the queued command and typed result through the existing AI admission, cancellation and completion helper. Vision/NativeObjectRunners.cs scopes the factory profile, source diagnostics and completion to this request.
- The manager executed original ARM factory 0x0052B0CC with ID 17 and -1, with the latter substituting selected ID 55. It observed all six zero setter calls in order. Original completion 0x0054B020 kept ID, count and flag constant with stack poison 0 and 0xA5, and the four intervening words matched poison. Original marker selection 0x00877774 skipped a failed root, chose the first planar tie, and returned no valid pose when all roots failed. Inputs and owner calls were controlled fixtures.

- AiTurnToObjectControlTests uses the public queue and fake robot transport with current-origin BlockWorld markers. It observes real motor messages and acknowledgments, checks a changed marker on the refinement pass, and verifies missing target, foreign-root failures, angular skip, pending and active cancellation, and defined-only completion. Separate native ARM marker inputs establish the tie rule. Builder passed 116/116 affected and 7/7 final focused. The manager independently passed 70/70, including unchanged OKAO and Acapela oracles.

## MISSING

- Selected-object/custom requests, arbitrary motor overrides, verification and tracking profiles, raw request ingress and broader marker or pose gaps remain separate. The undefined completion words have no fabricated raw payload. A separate managed owner-replacement case was not run for this action type. No hardware run was made.

## Records and workers

- M8-015 covers the action queue/completion boundary, M13-020 and M13-026 the shared pose/motion path, and M12-033 the adjacent marker queries. No status or approval changed. Reader used gpt-6.1-sol at medium effort. Builder and reviewer used defaults.
