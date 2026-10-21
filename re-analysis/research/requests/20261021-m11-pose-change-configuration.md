# R-M11-POSE-CONFIG — configuration recipient reached by charger observation

Independent read-only job; answer `research/20261021-m11-pose-change-configuration.md`.
Search research/inventory first and quote current M11-037/063 and applicable M13 configuration records. Start at the explicit B-M13-9 remaining recipient, BlockWorld::OnObjectPoseChanged call0x00624892, SetObjectPoseChanged body0x00616D54 (decomp index navigation only).

Trace the state it writes, its supported reader/update consumer and resulting robot/API/readiness or world-state effects. Does a Known charger observation matter to this recipient, and what does the correct general shared owner require? Recover a bounded minimal owner path, including initialization, state filtering and update/reset order. Do not infer no effect from a currently unbound C# delegate. If a downstream routine is already recovered, cite/reuse it instead of retracing. If the effect chain cannot be closed narrowly, return explicit UNKNOWN and the next exact boundary.

Compare the existing SetObjectPoseChanged binding/MISSING and current configuration manager code. Separate supported behavior from logging-only or unreachable branches with evidence. Return native instructions, current records, live path, scoped build recommendation and independent regression oracle. No whole block-configuration audit, implementation/tests, policy decision, manifest/inventory edit, approval or hardware. Research files only, publish under lane rules.
