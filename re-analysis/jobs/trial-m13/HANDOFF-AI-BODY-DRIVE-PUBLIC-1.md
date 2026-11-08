# AI-BODY-DRIVE-PUBLIC-1

## Native admission and public contract

- Motion.QueueAiDriveStraightAsync takes explicit finite f32 distance and speed and an animation flag defaulting to true. It preserves the checked native factory's signed inputs without adding a clamp. RobotActionUnion inner tag 5, thunk 0x00525C6C and factory 0x00529EF6–0x00529F1A select explicit constructor 0x00547278. The earlier drive handoff covers the runner's line, path and animation sequence.
- The host supplies AtEnd and zero retries. Native ActionList accepts the supplied queue position at 0x00527A74–0x00527A8C and appends the AtEnd action at 0x0053DB04 and 0x0053E16C–0x0053E248. The shipped Unity entry's default NOW is a distinct caller input. The public request uses the existing validated robot/readiness checks, original manipulation owner, external tag and queue, with no idle or extra calibration gate.
- Before native admission, a canceled, stale or tag-refused host request retires the unqueued object without invoking the shared native drive destructor. The original owner's readiness is checked again after the constructor's negative-speed diagnostic (0x0054728C–0x00547308). Once a live request reaches the reaction-disabled native gate, the queue discards it using the native destructor even though the public result is refused (0x0053D94C–0x0053D970).
- A cancellation token posts the original action's tag cancellation only while the original queue still contains the exact runner. The current action's Cancel precedes deletion, while pending deletion can report NOT_STARTED without Init (0x0053E83C–0x0053E96E). The retained native destructor aborts the original robot's shared active path and always tears down the original shared driving animation (0x00547530–0x00547562), including pending or animation-disabled drives. This behavior is documented on the public method and follows the manager's explicit REVIEW direction.
- The standard type 8 result uses completion variant 6 (0x0053FF0E–0x0053FF14). The AI task captures the stored scalar result and original owner retirement at the watcher tail, then observes the existing queue-unlink owner barrier (0x0053FA22–0x0053FA98). It does not substitute a host cancellation exception for the native action result.

## Files and verification

- Motion.cs adds the public request and result, with the shared AI admission and exact-runner cancellation. DriveActions.cs delivers the retained native completion through the existing owned runner.
- AiDriveControlTests.cs checks fake-robot AtEnd ordering and a real Clear/Append/Execute path followed by matching C6 and raw variant 6. It covers pending and active cancellation, the pending shared-path clear, native reaction discard with same-owner readiness changes and owner replacement, and pre-admission cancellation, construction replacement and host tag collision. The default animation case checks the flag on the admitted runner. A separate full animated drive through this new entry was not run.
- Final affected drive/head/lift/queue/path run passed 107/107. Independent source review passed.

## MISSING

- The earlier internal handoff's bionic transcendental and generic animation-selection/profile boundaries remain. Physical driving, stop distance and animation timing were not tested on hardware. No compatibility policy, fidelity status or approval changed.

Reader used requested gpt-6.1-sol medium. Builder and reviewer used configured defaults. Actual backends unobserved.
Independent manager affected rerun passed 160/160 with zero skips. Batch full gate pending.
