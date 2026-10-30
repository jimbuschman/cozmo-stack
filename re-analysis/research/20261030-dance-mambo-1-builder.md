# DANCE-MAMBO-1 builder handoff

Builder startup requested default model and omitted effort; actual backend unobserved. Reviewer fix rounds 2; source question 1 (listener registration/iteration, source checked); post-test harness batches 3; diagnostic attempts 2; compiler failures 0. No model switch.

## Changed files and source

- `Behavior/BehaviorContainer.cs`: canonical Dance class-7 configured construction (0x0059CA34..4E), actual Robot and manipulation owner, no invented automatic admission.
- `Behavior/NativeDanceBehavior.cs`: configured sequence, gates and resume (0x005ED438..456, 0x0102A104..174); six reaction locks (0x005ED476..47C / 0x00C6F1C8); connected family-2 cube enumeration, persistent per-ID selection, real Robot RNG and 100th-draw edge (0x005ED4DC..674, 0x005ED82C..914); lift-safe body action and listener delivery (0x005C0158..01A6, 0x005C0254..292, 0x005C0590); recursive active-byte callback (0x005EDC46..D00); Stop stage4/0, fades, unconditional global layer1 stop (0x005ED996..AB2, 0x005EDFCA..D0, 0x00638D58..74). Native listener pointer order and uniqueness are modelled on explicit supplied identities with weak listener-object ownership; expired owners log the M1-034 smallest skip.
- `Lights.cs`: exact-ID helper preserves callback and raw signed offset into duration-zero layer call (0x006389FC..0x00638A34); existing callers preserved.
- `Behavior/NativeBehaviorRuntime.cs`: retire this managed owner on runtime disposal; no claim that shared M15-045 retirement is complete.
- `tests/Cozmo.Protocol.Tests/DanceMamboNativeTests.cs`: supplied admissions and inputs exercise the configured owner, connected world, real light engine, native action and callbacks, 100th draw, historical IDs, re-entry, listener identity/order/forced GC, and natural fade. Reach labels distinguish supplied selection from unknown actual producer.
- `tests/Cozmo.Protocol.Tests/M7BatchThreeBTests.cs`: checked factory class list includes Dance (case 0x0059CA34).

All four production files remained unchanged after initial production static check. The four pre-existing touched files (BehaviorContainer, Lights, NativeBehaviorRuntime, M7BatchThreeBTests) each have actual before bytes plus SHA256 and raw/filtered git blobs captured before their first edit. The two newly created files (NativeDanceBehavior and DanceMamboNativeTests) were absent initially; absence metadata was entered only after creation, a provenance limitation. Each freeze and correction preserves actual raw file versions, SHA256 and raw/filtered blobs in the worker `.scratch/dance-mambo-*` directories; final six-file freeze is `dance-mambo-harness3/after/metadata.txt`.

## Explicit gaps and record proposal

The actual Dance_Mambo voice/request/automatic selection caller remains UNKNOWN and is visibly MISSING. Native listener finite registration/iteration and result-ignoring callback behavior are source-backed; actual listener producer, pointer identity mapping, and owner lifetime remain UNKNOWN/MISSING. Supplying listener identities and calling StartAsync in tests does not establish a native producer. Shared M8-005 and M8-012 lifecycle/report/input gaps remain. No existing whole-record closure is proposed. Propose one new otherwise-unowned Dance configured lifecycle record covering D1–D12, with these caller and shared-owner leftovers explicitly retained; no official record/status/inventory approval changed.

## Validation history

All actual Windows .NET invocations elevated. One offline cached restore only, five projects; exit0, shutdown0, pre/post/checkout0/0/0. Reference and diagnostic raw logs, TRX, before/after inventories and process arrays remain under the worker `.scratch/` paths.

- Reference1: build0, 1256 passed / 3 failed / 0 skipped / 1259 total, test1. Failures: stale M7 factory list; secondary stopped-pop hook incorrectly assumed universal stop notification; attempting Flash over non-overridable fade.
- Diagnostic1: build0, 0 passed / 1 failed / 0 skipped; test1 expected. `CubeLightComponent.PlayLightAnim.CantBeOverridden`, cube2 engine stack `dancing_03+fadeOff_1sec`, timer3040, count2. Post process array contained PID34964 dotnet with null command line, ownership unknown; no kill. Separate root actual follow-up recorded empty.
- Reference2: build0, 1257 passed / 2 failed / 0 skipped / 1259 total, test1. Layer2 Interacting play was refused; post-fade null oracle ignored active D11 callbacks that re-queue.
- Diagnostic2: build0, 0 passed / 2 failed / 0 skipped; test1 expected. Layer2 `wakeUp` was non-overridable; logs and callback IDs established current Dance callbacks re-queued after StopAll. Post process array contained PID7308 dotnet with null command line, ownership unknown; no kill. Separate root actual follow-up recorded empty.
- Reference3: build0, 1258 passed / 1 failed / 0 skipped / 1259 total, test1. Test asserted radio slots 0/1 instead of actual BlockWorld IDs 7/8; natural fade and fixed deadline preconditions passed. Actual object IDs now come from the connected world, not from the callbacks under test.
- Reference4: build0, 1259 passed / 0 failed / 0 skipped / 1259 total, test0, shutdown0, pre/post/checkout0/0/0. Input snapshot 8827 files before and after unchanged; 187 immutable binaries unchanged. Of 192 bin-area files, only `cozmo-stack/tests/Cozmo.Protocol.Tests/bin/Debug/net9.0/.scratch/sparks-stack-build/negative-vision-producer.log` changed, a previously identified generated output.

The five exact generated log names tracked separately are:
`cozmo-stack/tests/Cozmo.Protocol.Tests/bin/Debug/net9.0/.scratch/sparks-pop-a-wheelie-1/artifacts/scene-soft0.log`,
`cozmo-stack/tests/Cozmo.Protocol.Tests/bin/Debug/net9.0/.scratch/sparks-pop-a-wheelie-1/artifacts/scene-soft1.log`,
`cozmo-stack/tests/Cozmo.Protocol.Tests/bin/Debug/net9.0/.scratch/sparks-roll-build/configured-soft0.log`,
`cozmo-stack/tests/Cozmo.Protocol.Tests/bin/Debug/net9.0/.scratch/sparks-roll-build/configured-soft1.log`,
`cozmo-stack/tests/Cozmo.Protocol.Tests/bin/Debug/net9.0/.scratch/sparks-stack-build/negative-vision-producer.log`.

Independent B compiled replay passed 1259/1259, 0 failed, 0 skipped; exact artifacts: C:/Users/JimBu/source/repos/jimbuschman/cozmo-stack-codex2/.scratch/worktrees/codex-dance-mambo-1/.scratch/dance-mambo-replay1 (comparison.json, TRX and process arrays). Root independently compared the 63 classes, 8827 input files and 187 immutable binaries. This builder report was created after replay as metadata; it was not part of the replay input snapshot. Integrated full gate remains pending. No hardware, commit, or push. Earlier failed attempts and diagnostic outputs are not relabelled as passes.
