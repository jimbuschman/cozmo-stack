# DOCKING-TEST-SIMPLE-1 compiler correction1 affected check

PASS before rebuild. Reviewer model/effort requested default/omitted; backend unobserved; no switches. No reviewer .NET invocation.

One-line change verified: test adds `using Cozmo.Robot.Vision;`; existing ChargerGeometry declares that namespace. Removing exactly this single added LF line restores previously reviewed test SHA855a51ff1c52e4a55ea78e796ba6a3ddbb3da414998622ae82039f465614ac5c. No assertion/oracle change. Current test SHA9AD372360E859CEDB854FBC168095D45FD3861533C1A3096C6C9CF10CE784251; raw=clean blob20fe54d5959a1da9a53dde7582269239c0a09e9c.

Ledger SHA FEE38C79C9FE8FE45676F5DC9DA02D1720B1B5715666AD4984BA970D41EE8B7D; raw=clean blob967bc946f968a256b8f5b28e6d4d28acfa43554e. It preserves build1 native exit1/2260 warnings/1 CS0103, stop before tests/correction/retry, shutdown0, process arrays[]/counts0 and lease return. Retained build log/exit/shutdown/process artifacts inspected; focused/combined not launched at failed build. Existing initial counts explicitly superseded by current counts.

Unchanged production SHA256 values: ChargerBehaviors56F3D43C3E3C30F1233193576BC90672E43DD3454DE777FFE704BBAF32DB4791; BehaviorsB0CD86D26075F94CE7704EA8185DCAF65E461638ADC380C71B72B40EF4D22294. Raw/clean blobs unchanged at prior reviewed values. Four current complete files decode strict UTF8LF/noBOM; git diff --check PASS. Hash-object calls read-only; no Git mutation.

Counts remain sourcecitation1, reviewer-required metadata/test-plan batch1, compiler-origin correction1, behavioral/selftest0. No new reviewer-origin finding or correction batch. No source/full-code audit repeated. Root exclusive lease still required for rebuild. Detailed affected check also appended to draft-check artifact, current SHA DDE3EFC60FCB8C28C06C5DCE3C82F4DCE1FEE421C761E5BF98DB6F77732F1565.
