# R-M11-CHARGER-MAP — supported map-add prerequisites

Independent read-only comparison; answer `research/20261021-m11-charger-map-add-join.md`.
Read current M11-074/088 and search research/inventory first. Reuse `overnight/20261014-ov42-map-producers.md` O5-O7 and `overnight/20261016-batch-check.md`, which already recover the robot-relative pose, height gate and imported polygon/cache path. This is a production-gap comparison, not permission to re-extract those rows.

Starting with the real charger observation added by B-M13-9, map the remaining AddObjectToMemoryMap dependencies at0x0067EDA2/EDC0/EDC8/EEC4 to existing pose, height, polygon insertion and cache owners. Which checked pieces can be bound directly, and which exact behavior-changing callee is still unknown? Include add rejection versus cache added=false and subsequent valid/valid update obligations. Distinguish generic shared-owner requirements from charger-specific inputs; do not hardcode charger acceptance or bypass map admission.

Return current record title/status/evidence, existing checked source rows, production writer/consumer joins, explicit UNKNOWNs and bounded build recommendations. No whole memory-map audit or new pose arithmetic extraction. No implementation/tests, inventory/manifest changes, approvals, hardware or settlement. Use actual native instructions only for missing narrow questions; decomp/libcozmoEngine is navigation. Publish research files only.
