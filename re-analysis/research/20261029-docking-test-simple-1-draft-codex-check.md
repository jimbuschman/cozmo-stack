# DOCKING-TEST-SIMPLE-1 independent coherent draft check

2026-10-29. Initial full draft reviewed before compilation. Source packet: corrected reader c44c481a8f1f04e399169736b6393ab4cb6eec6d7048847c3486d710f6060590 and independent source check7f914a46c7b0d2f5057b30179d71f2e0586968e6c1190091ee1e6272ef0fcb9f. Reviewer requested model/effort omitted/default, actual backend unobserved, no switch. No .NET/test execution by reviewer.

## Initial draft verdict: one metadata correction required

`re-analysis/jobs/trial-m13/DOCKING-TEST-SIMPLE-1-builder.md` proposed combined affected filter omits the new DockingTestSimpleBoundaryTests class. This contradicts the requested single combined set. Add it to the same combined filter before first compile. Exact set: `FullyQualifiedName~Cozmo.Protocol.Tests.DockingTestSimpleBoundaryTests|FullyQualifiedName~Cozmo.Protocol.Tests.NavigationTests|FullyQualifiedName~Cozmo.Protocol.Tests.ActionBehaviorDiagnosticTests|FullyQualifiedName~Cozmo.Protocol.Tests.FreeplayTests`. Update counts to reviewer-required builder draft/test correction batches1, metadata/test-plan only; behavioral fix rounds0. Source-report citation correction1 stays separate. Preserve initial ledger/freeze bytes in ignored artifacts before amendment. No code/test correction required.

## All four files reviewed

- ChargerBehaviors.cs: whole changed XML and OnStart inspected against frozen diagnostic contract and baseline. Only executable delta is ReportMissing before unchanged DisableReactions, MountChargerAction(M,ChargerGeometry.ObjectId), RunAction/trace/result/Finish. Public class/constructor/default ID/class label/runnable predicate preserved. Text explicitly identifies excluded/unrecovered native M7-022 and historical/custom adapter, without inventing an SDK gate. Existing global missing-report dedup/deferral retained.
- Behaviors.cs: only Navigation XML changes, accurately identifies historical/manual list and adapter. List members, arguments, order and driving-animation wiring unchanged. No factory change.
- DockingTestSimpleBoundaryTests.cs: read entire two facts and inspected supporting StartAsync/RunAction/ReportMissing/Update paths. Explicit unit/custom label names direct Start substitution despite unrunnable missing charger, no native conformance claim. Test observes M7-022 missing callback before reaction lock and before action-start trace, checks lock during action-start, awaits real async work, confirms deferred result until Update and BadObject trace/result/Finish. Serial missing-report collection and finally event cleanup present. Second fact checks actual configured factory null/NotImplemented with no stand-in. No synthetic native caller/success/readiness fixture. Ordinary legacy result/async scheduling are preservation checks, not claims that native developer flow was recovered.
- Builder ledger: all sections read, records/models/source hashes/preparation actions and inherited frontiers disclosed. Only required amendment is combined filter/count as above.

## Extra test oracle: narrow M13-008 check

Read complete current M13-008 object, including title/status/evidence/unresolved: title `The mount sequence and the reverse onto the charger, with the retry result codes`; status IMPLEMENTATION_GAP. Evidence covers ConfigureTurnAndMount0054E500, BackupOntoCharger0054E7A8 and CheckIfDone0054E2D0; unresolved explicitly includes BadObject among built-awaiting-verification behavior and retains pose/write/ticking/turn/flags gaps. It does not certify the entire charger action or original DockingTestSimple.

Ran known.py0054E0CC first, then independently reopened raw same-SHA02263C07F6BB60F4D7F351A3667DCA84FD3E6C0FC6F838E18B5DE7CF4E2989E1 binary using Python313/LIEF/Capstone. Init0054E106..110 calls BlockWorld::GetLocatedObjectByIdHelper with family4. 0054E114 cbz target0054E12E. Missing-target path warns0054E140, then0054E166 movs r0,#4;0054E168 movt r0,#0x300;0054E16C/16E return03000004. This directly supports the supplied-no-charger BadObject oracle. No downstream mount/backup audit or settlement. C# ChargerActions RunAsync missing-target return inspected; original helper unchanged by draft.

## Raw/staged-equivalent and format freeze

Independently validated all four complete raw files with strict UTF-8 decoding, no CR, no BOM; SHA matches draft-freeze; read-only git hash-object raw and configured clean-filter/staged-equivalent values match. git diff --check PASS; git status exactly two modified production files plus new test and ledger. No staging or other Git mutation.

| File | SHA256 | raw = clean-filter blob |
|---|---|---|
| ChargerBehaviors.cs | 56f3d43c3e3c30f1233193576bc90672e43dd3454de777ffe704bbaf32db4791 | 7bd26c496b1b08f1677ea314f8d25c4c8a1f039c |
| Behaviors.cs | b0cd86d26075f94ce7704ea8185dcaf65e461638adc380c71b72b40ef4d22294 | c0e62cbccc3259fb1dad406be89aa454ab686ed1 |
| DockingTestSimpleBoundaryTests.cs | 855a51ff1c52e4a55ea78e796ba6a3ddbb3da414998622ae82039f465614ac5c | 1f3a10c73df8bc8b5480110959c8bcf77a118866 |
| Initial builder ledger | fb25026b0b0142e94c137e68dc6b4696e3f68a9ebc6f2381dc06c8198a3c643e | dc6b625fd7c55e846f65d44700059540dc889709 |

No unsupported/contradicted/omitted/circular behavior findings in complete code/test draft. Manifest unchanged, no settlement or new support decision; separate M7-027/028 live constructor effects retained. Original native M7-022 body remains unread/excluded by existing recorded scope. Factory unchanged, so M7BatchThreeBTests not required by factory-delta rule.

Initial verdict is HOLD for exactly one ledger/test-plan correction. After ledger-only amendment, verify amended ledger/counts/hash and unchanged three code/test hashes; do not repeat source/code audit. No compile authorized by this artifact until that check and root lease. No compiler/self-test correction occurred. Reviewer initial ledger read guessed nonexistent research path; corrected from freeze to jobs path, tooling-only attempt retained.

## Corrected draft verdict: PASS before first compile

Ledger-only amendment independently checked against preserved initial ledger. Exact four-class combined filter now includes DockingTestSimpleBoundaryTests, NavigationTests, ActionBehaviorDiagnosticTests and FreeplayTests. Appended current counts explicitly supersede initial counts: reviewer-required builder metadata/test-plan batch1; behavioral fixes0; source citation correction1 separate. Initial ledger/freeze preserved. No further changes requested.

Amended ledger SHA256 `5a66831c7caa49118c636f49d12fc82ed92f7214c6afd09aecb7626063a589f0`, raw/clean blob `140a4952af56227191782d2e843a95c39e5bfadf`; strict UTF8LF/noBOM. Rehashed the other three complete code/test files and raw/clean blobs: all unchanged at the values above. git diff --check PASS. No source/code audit repeated; no .NET run or lease. Full coherent corrected draft passes the bounded contract. Compile/test still requires root's explicit exclusive lease.

## Compiler-origin correction1 affected check: PASS before rebuild

Build1 failed native exit1 with 2260 warnings/1 CS0103 error at new test24: ChargerGeometry missing. Inspected retained build.log/build-exit; no tests launched. Retained final shutdown exit0 and explicit process arrays NativeProcesses=[]/OwnProcesses=[], counts0. Builder returned lease before correction. This was compiler-origin correction1, not reviewer-required batch2.

Affected change is exactly one added `using Cozmo.Robot.Vision;` in the new test. ChargerGeometry namespace independently confirmed from its existing file. Removing that exact single LF line from amended test bytes reproduces previous SHA855a51ff1c52e4a55ea78e796ba6a3ddbb3da414998622ae82039f465614ac5c; assertions/oracle unchanged. Current test SHA9ad372360e859cedb854fbc168095d45fd3861533c1a3096c6c9cf10ce784251 and raw=clean blob20fe54d5959a1da9a53dde7582269239c0a09e9c. Ledger retains failed attempt/stop/lease, exact counts/source/model boundaries; SHAfee38c79c9fe8fe45676f5dc9da02d1720b1b5715666ad4984ba970d41ee8b7d, raw=clean blob967bc946f968a256b8f5b28e6d4d28acfa43554e. Both production files remain at the original reviewed SHA/raw/clean values above. All four decode strict UTF8LF/noBOM; diffcheck PASS.

No repeated source/full code audit or reviewer .NET execution. Counts: sourcecitation1, reviewer metadata/test-plan batch1, compiler correction1, behavioral/selftest0. Affected correction PASS; rebuild remains subject to root exclusive lease.
