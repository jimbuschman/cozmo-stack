# TRACKED-FACE-GEOMETRY-SETTLEMENT-1

## Claims and evidence

- TrackedFace starts at +X pi/2 (0x0087DBCA–0x0087DC06). The eye distance at 0x0087DC68–0x0087DD98 uses binary32 distance, signed cosine except at the absolute cosine threshold, and a six-pixel fallback for coincident eyes. UpdateTranslation keeps the native eye/box branches, the box cancellation arithmetic and zero eye slots, the inverse-calibration ray at 0x0085EF3A, and the fx*62/distance scale at 0x0087DE24–0x0087E038. It writes local translation and parents the pose to the camera at 0x0087E042–0x0087E064. The checked managed path keeps the same camera-local arithmetic and declares host libm.
- DetectFaces composes the camera-parented pose relative to its root and detaches it at 0x006B37B0–0x006B37EC. The queued worker's original-owner, same-timestamp camera and robot transforms carry those detached native values without borrowing a camera wrapper. FaceWorld resolves the historical root, copies the detached transform and attaches that root at 0x004F42B4–0x004F42F2. Existing no-parts input retains the three previous local translation words while using the new rotation and parent at 0x004F4784–0x004F47BE. The prepared pose for the native cache now uses the same captured sidecar values as the native report.
- FaceWorld copies a face to the last-face cache only above count three or with a name at 0x004F4956–0x004F4972. Same-stamp candidates compare distances to actual MAIN at 0x004F4990–0x004F4A24. The ARM BLE preserves the old pose on ties or unordered results. The selected pose is copied at 0x004F4A26–0x004F4A38. The getter checks the original root or takes its explicit flatten-and-reparent option at 0x004F5AE2–0x004F5B38. The cached pose now carries the checked historical parent so requireCurrentOrigin readers can recognize it.
- The checked queued path withholds missing native geometry or a legacy-only prior no-parts translation. The public FaceObserved compatibility callback runs outside the world/pose gates with owner, removal generation and entry checks, and handler isolation under M1-034. Public HeadPose consumers retain their compatibility projection, including the old PreviousPose wrapper.

## Files and tests

- Vision/Faces.cs separates camera-local projection from the compatibility pose, prepares the cache carrier and defers attached face callbacks. Vision/VisionSystem.cs passes the detached native transform and original owner into queued consumption. Vision/NativeFaceCache.cs copies the prepared pose and follows the ARM same-stamp comparison.
- The manager ran the shipped ARM eye distance, UpdateTranslation with native inverse calibration and normalization, and two Transform3d precompositions. Fixtures covered coincident eyes, near-zero and negative cosine, off-centre eye and box projection, the six-pixel box floor, unchanged rotation at the translation store, and the camera parent call. Native axis-angle construction and composition produced independent f64 quaternion and f32 translation words. The translation probe hooked the pose cell and parent call boundary and the probes supplied host libm.
- NativeTrackedFaceGeometryTests compares managed projection and composition against independent guest words. A real F2 worker and normal robot tick exercise the original and changed historical roots, no-parts retained native translation, new rotation and parent, preserved public prior pose, cache visibility and cross-thread callback reentry. It also checks withholding a missing prior native carrier. PutDownBlockNativeTests exercises actual MAIN distance, ties and unordered comparison. The initial affected run found two manual Interact regressions with a parented public HeadPose; the final implementation restores that view and uses a separate native cache carrier.

- After restoring compatibility, the builder passed 181/181 affected cases. Final focused tests passed 15/15 after adding the captured-owner/MAIN fence and stale-owner regression. The manager independently passed 44/44, including the manual Interact variants.

## MISSING

- Public LastFaces.HeadPose and FaceEntry.HeadPose remain compatibility geometry. Their checked native sidecars and the retained last-face cache use the recovered transform. Mixed legacy/native cache continuity, generic head-pose solving, arbitrary pose chains and Android libm bit equality are still open. ID matching and unnamed expiry use the existing paths and are not changed here. No hardware run was made.

## Workers

- Reader used gpt-6.1-sol at medium effort. Builder and reviewer used the defaults. The independent review checked the historical parent, no-parts copy, cache selection and callback lock path.

## Records

- M14-001 owns the geometry and cache join. M14-008 owns the neighboring face result/ID/expiry path and M11-065/M11-066 own pose arithmetic and parent semantics. No whole-record settlement or status change is proposed.
