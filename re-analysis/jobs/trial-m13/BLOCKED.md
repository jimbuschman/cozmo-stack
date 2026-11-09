# Operator decisions

## Far-side drive pickup

PICKUP-PREDOCK-SIDE-1 reproduced the native far-side-only approach from both captured cube orientations. The third run fell off the table after the cliff stop. The hardware harness now requires explicit opt-in for its drive round. Decide whether the standalone AI drive-pickup API should reject far-side-only approaches as a COMPATIBILITY_POLICY, or retain the native result. If approved, define the geometric filter and API result before building it. No general table-edge safety is established by either capture.

## Next capability and settlement proposals

1. **AI-BODY-TRACK-FACE-REQUEST-1.** Trace the original explicit TrackToFace direct 0x26 and nested 0x1E admission into factory 0x52ACC0–0x52ACE6. Verify the app recipient and exact tag/queue semantics, then expose the source-supported signed face ID and head-only options through the retained TrackFace runner. Exercise current-root observations, face-ID remap, cancellation and exact variant-3 completion. Keep the RequestGameSimple and PeekABoo behavior owners separate.
2. **BLOCKWORLD-POSE-QUEUE-SETTLEMENT-1 (M11-063).** The frame flag writer and queue gates are already built. Verify the complete live call path against 0x624F78–0x624F7E, 0x6250D4–0x6250D8 and 0x624808–0x6248B8. Exercise first pose, repeated writes, a real frame with failure supplied at the localization result boundary, and cliff ingress with the flag clear. Check that the flag, retained old pose and configuration force recipient match source. Limit settlement to the queue writer and its own defined state, with the separate M11-037 consumers, M11-064 failure handling and M11-038 app reports identified by record.
