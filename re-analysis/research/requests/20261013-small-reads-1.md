# Research request R-SMALL-1 (M11-072, M11-073)

- **Binary:** libcozmoEngine.so 3.4.0-1204.
- **Context:** `research/20261013-delocalize-callees-check.md` (E5/E6) and `research/20261013-remaining-pose-bodies-check.md`,
  and the M11-072 and M11-073 records in `re-analysis/fidelity_manifest.json`.
- **Answer file:** `research/20261013-small-reads-1.md`.
- **Format:** one coverage table per item, first. Each step gets an address, or UNKNOWN.

## Items

1. **The RobotDelocalized (E2G tag 0x32) subscribers.**
   - Find every Subscribe call for tag 0x32 (UiMessageHandler vt+0x28) in the engine, and their registration order.
   - Include the CubeLightComponent (lambda 0x0063AC46) and BlockConfigurationManager (lambda 0x006172A6) setup.
   - Find whether ReactionTriggerStrategyCubeMoved::HandleRobotDelocalized (0x0060C038) is reached, through the
     behaviour strategy event dispatch.
   - Give the order in which the subscribers run.
2. **PoseBase::GetParent() on a root pose.** GetParent (0x008453D4) wraps the parent node with no null check, so on a
   root it faults at 0x008452BA. For each of the roughly 60 GetParent and FindRoot call sites (0x004DF674 to
   0x008775F6), say whether a HasParent/IsRoot (or equivalent) guard prevents a root from reaching it. List any
   unguarded site that a root can reach in normal running.
