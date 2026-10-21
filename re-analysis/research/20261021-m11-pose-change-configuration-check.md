# R-M11-POSE-CONFIG bounded review

**Codex review; Claude spot-check pending**

Reviewed against 56258024. The pending-ID, threshold, reset and root-pose cache findings are supported. This is not approval of the complete configuration production path and releases no build yet.

## Personally checked native boundaries

The manager read the actual Thumb instructions, including the earlier portion of this review before B-M13-11 was closed: owner construction 61BDB6..61BDC6; manager construction 616B58..616C34; subscription 616C44..616CF4; setter/update/threshold 616D54..616EFC inclusive; dispatch 616F00..616F42; cache writer 616F44..617130 and cleanup 617132..6171E0; located-ID wrapper 61F3E0..61F57E; frame-tail calls 625206..62521A; Notify 64DCAA..64DD76. Literal pools are data, not executed instructions. Common container and pose primitives retain their separate evidence obligations.

This continuation rechecked the full OnObjectPoseChanged normal body 624808..6248B8, setter/update/threshold including their return instructions, both cache root-conversion call sites 617050/617070, the root helper normal path 4DF628..4DF6DC, force setter 61723A..61723E and actual tag-50 invoker 6172A6..6172B6. The invoker obtains Get_50 before writing the captured manager's +C; the simple setter is not by itself proof of subscription delivery. Raw bytes at C777D0 are 0200000001000000: families 2,1.

Findings:

- Every normal OnObjectPoseChanged branch reaches the manager with the object's current PoseState. The world+74 flag controls the separate old-pose queue and logging. The manager inserts the ID uniquely and erases its cached pose only for state zero.
- No IDs and no force returns immediately. Otherwise force bypasses the threshold; threshold false still consumes pending work. Full update order is containers, prune, last poses, Notify, then reset force/pending. Reset before Notify is wrong.
- Threshold lookup has family=-1. A located ID without a cached pose marks movement; a cached pose uses IsSameAs with three f32 5 mm values and angular bits 3F060A92. A missing object queries container membership. This is not a Known-only or cube-only admission gate.
- The cache producer filters families 2,1, converts each pose with respect to its root, then assigns/inserts without globally clearing the map. A located charger (family 4) absent from that cache can therefore request repeated recomputation without physical movement. This is conditional on lookup/cache state, not an every-frame claim.
- Notify's dispatch bridge 64DE5E..64DE7C looks up the event key at event+8 and invokes the matching signal if present. The reread 64E656..64E69A retains callback nodes while walking; function dispatch remains a subscriber boundary. No inferred game message or API event is authorized.

## Current code and record ownership

BlockWorld.SetObjectPoseChanged remains unbound. ManipulationSystem binds the frame-tail Update but also invokes it immediately from ObjectObserved and PoseStateChanged. BlockConfigurations.Update clears ForceUpdate at entry and rebuilds unconditionally. Its host ConfigurationSeen snapshots are not the native public-state signal. These are implementation gaps, not evidence that the recipient has no effect.

Current manifest re-read:

- **M11-037**, "The BlockWorld frame sequence for observed markers", **IMPLEMENTATION_GAP**. Evidence explicitly includes "BlockConfigurationManager::Update body 0x616D7C (DidAnyObjectsMovePastThreshold gate, UpdateAllBlockConfigs, PruneFullPyramids, UpdateLastConfigCheckBlockPoses, NotifyBroadcasterOfConfigurationManagerUpdate)" and the adopted pre-items4-5 check. This is the existing owner; no new record is inherently needed.
- **M11-063**, "BlockWorld+0x74: the writer of the pose-change queue flag that gates OnObjectPoseChanged logs and queue", **IMPLEMENTATION_GAP**. Evidence says flag zero reaches SetObjectPoseChanged and clears the world flag before configuration Update. It does not own the entire manager.
- **M11-071**, "RobotDelocalized broadcast order (app delivery first, then the synchronous in-engine tag-0x32 emit) and BlockConfigurationManager's delocalized flag", **IMPLEMENTATION_GAP**. Current evidence names lambda 6172A6 setting +C; consistent with the reread.
- **M12-049**, "BehaviorBuildPyramid helper callbacks and the pyramid cache contents (Container::Update, CastNewEntryToBestSharedPointer, PruneFullPyramids, PyramidBase::operator==)", **RECOVERABLE_GAP**. Evidence names 616F00 -> 61682C -> 62C56E -> 62C68C and prune 62C72E. This review does not recover those full producers.
- **M11-042**, "Still unread next to the BlockWorld observation path: IsVisibleFrom projection/occlusion, FindLocatedObjectHelper returnFirst=0 result, subscriber bodies behind 0x0064E704, calibration null at CheckForUnobservedObjects", **IMPLEMENTATION_GAP**. Its evidence retains the subscriber boundary; it remains open.

A separate read-only scope review confirmed existing M11-037 ownership. The older adopted extraction's short map[id]=GetPose description omits the intervening root conversion; its verifier lists the assignment/insertion but not that join. Do not silently freeze the corrected meaning under the old approval.

## Concrete proposed inventory correction (not applied)

Add to M11-037's configuration-manager inventory/evidence: "UpdateLastConfigCheckBlockPoses 0x00616F44..0x00617130 selects current-origin families {2,1} (table 0x00C777D0). Before either assignment or insertion, it converts GetPose with respect to root via 0x004DF628 at 0x00617050/0x00617070; it does not clear the map globally. SetObjectPoseChanged 0x00616D54 erases an entry only for Invalid."

This is a bounded correction to an approved inventory, requiring the AGENTS.md inventory checkpoint before implementation relies on it. Status, live_path and hardware_required would remain unchanged. No manifest, inventory or approval snapshot changed in this review.

## Open questions / release boundary

The full container population, membership recipients, public-state owner/subscriber effects, retained pose lifetime and callback synchronization must be accounted for in a concrete build scope. Do not install a gate around the host all-pairs candidate and declare the native path complete. Common filter/pose primitive internals were not all re-audited here; the reviewed report's reused OV10 rows are not newly certified by this bounded check. No new research queued under HOLD.

No code change or regression run was appropriate for this review-only item. Builds verified: 0 additional (2 total in this chain); records settled: 0. Next ordered held-backlog review: row 44 retained pose owner; any configuration build also needs the above inventory correction approved. M13-017 remains not ready for hardware, and no robot check was written.
