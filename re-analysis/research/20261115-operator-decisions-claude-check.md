# Operator decisions, 2026-11-15 (recorded by the manager)

The operator agreed with the manager's three recommendations:

1. **The charger's observed-object report (0x44) is in scope.** The engine sends it whenever the charger is seen
   (BroadcastObjectObservation 0x0061FED8; M11-038 scope census). It is API delivery ("What we reproduce" 3).
   Job CHARGER-OBSERVED-OBJECT-REPORT-1, after M11-065's ApplyTo arithmetic. Records: M11-038, M13-025.

2. **The stack-only BlockWorld.ObjectObserved and PoseStateChanged events stay,** with two conditions:
   - they carry the report's snapshot pose, not the live pose at delivery;
   - the stack's internal readers use the engine's local 0x44 payload (delivered synchronously at 0x006211DC), not
     these events (job OBSERVED-OBJECT-LOCAL-PAYLOAD-1).
   When that job lands, the two events are recorded as COMPATIBILITY_POLICY (stack additions nothing engine-side
   consumes). Record: M11-038.

3. **HOST-CONFIG-SAME-ROOT-1: route it through the engine's search.** The ConfigurationSeen event stays, computed by
   NativeConfigurationGeometry.BuildStack (the engine's BuildTallestStackForObject, 0x0061929E / 0x00619358); the host
   public-Pose stack search in BlockConfigurations.cs is deleted. Record: M13-007.

Released to the Claude worker as jobs 30–32 (CLAUDE-WORKER.md). No status changed; no record settled.
