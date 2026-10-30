# BOUNCER-1 independent compiled replay — 2026-10-29

**PASS:** one exclusive no-build/no-restore combined replay returned 969 passed, 0 failed, 0 skipped. The complete TRX testName/outcome multiset, including parameterized names and multiplicities, equals the supplied 969-test expected composite exactly (969 distinct entries, no missing or extra). The comparison is against the explicitly constructed composite of run 2 outside the frame class (962) plus corrected frame-class run 3 (7), not a claim that the builder previously ran a single 969-pass combined test.

The manager granted the exclusive .NET lease; the builder's servers and processes had cleared before this run. No build, restore, source edit, hardware or retry was performed. The command/filter were frozen before execution; the test was run elevated for test IPC, with `--no-build --no-restore`, exact `final-filter.txt`, TRX output and unique log. Shutdown completed exit 0; actual native process array `[]` (count 0) and checkout-owned array `[]` (count 0). Lease returned to manager.

| Evidence | SHA256 |
| --- | --- |
| frozen source manifest | `e4cdd9f8e2d544c2620099f24b349ddc2e01f2ec30cdd457b016539fed448b34` |
| final filter | `d919602f861474dccab10e29d7b28583fb4436cf7d37093eb4f696f53fd29b88` |
| expected composite | `0096b604e1f83468012927f4b276af27bfe4a09b399107f46d0bc3e6d5256fe0` |
| frozen replay command record | `030b32f1a2472ee89158d059c8c2824ff966737fa1ac022e40a973ca4d385998` |
| replay TRX | `9a02a8906b7df0b37cb6f8c5d6ebb6fd7ca15b972d2e8e89c9b1065d3e32fbb2` |
| replay log | `085fa7d16d84fbe096faf1b6c6c2a31bc62c1089edfb9b879372c846fdd8eca0` |
| structured result comparison | `877cefdd73d50f5b584e0002aecaba18e317f7d24f5ba81962f1bc8686f57eea` |
| pre/post snapshot, identical | `43f53c7d12181795c8cad18b589154f8ea8e05968a9d01784385ba76d1974aad` |
| process clearance metadata | `a946f4da9406ececaf1b9c00c127e8aa29df28a892ceb8706c50746fdf1ce0ef` |

Pre/post snapshots are identical: 1,040 tracked/untracked nonignored `cozmo-stack` files, all 103 `bin/Debug` DLLs, all 56 frozen files and 20 native grayscale fixtures, the available engine native binary and marker library. Builder's 103 post-frame-run DLL hashes were independently checked against the pre-replay files. `libcozmoEngine.so` SHA256 `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`; marker library `1b7166ac37b166975f0e5313a3d4486ebae73010e42ffb89bbd0d4536a29c81d`. This builder checkout has the engine .so, not the separate imgproc .so; the native-pixel corpus and its original imgproc provenance were checked separately during source review.

Durable detailed artifacts in builder checkout `.scratch/bouncer-1/`: `replay-command-frozen.txt`, `replay-test.log`, `replay-test.exit`, `replay-results/bouncer-independent-replay.trx`, `replay-comparison.json`, `replay-pre-hashes.json`, `replay-post-hashes.json`, `replay-shutdown.log`, `replay-shutdown.exit`, and `replay-processes.json`. These tests use explicit simulated firmware and detector/feature seams where recorded; their pass does not establish source fidelity, full native detector/action/report paths, hardware acceptance or record settlement. The manager's full suite and final gate remain separate.
