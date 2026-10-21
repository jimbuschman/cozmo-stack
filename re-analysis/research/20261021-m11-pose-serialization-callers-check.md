# Pose serialization boundaries — bounded review

**Codex review; Claude spot-check pending**

Row31, baseline6a8d507f. Manager personally reopened the following .so
instructions. No record settlement or full caller-census approval.

Inbound846C70..846D16 widens the four floats, invokes normalized quaternion
construction, preserves translation words, obtains origin by input+1C and
constructs the parented pose. Outbound846D38..846DEE flattens, obtains root ID,
checks membership, checks the source-parent/flattened-parent invariant, then
copies translation and narrows the full quaternion. Both VERIFY paths continue
to output; neither authorizes a false return/drop. Missing-origin847C20..847CD8
logs and returns a lazily constructed static pose. Its runtime ID must not be
substituted with the requested ID or a guessed zero.

Robot.GetRobotState5180D8..518122 calls GetPose and this converter and copies
32 bytes. Current Sensors.PublicationPose flattens MainPose but regenerates a
yaw-only quaternion and supplies CurrentOriginId. That is a concrete incomplete
supplier, not evidence that full quaternion/root projection is absent from the
native API. Existing publication packing already narrows four doubles; avoid
rebuilding that packing or claiming the tick owner alone closes the supplier.

Direct boundary checks:

- Object report61FF7C..61FFDE calls converter, copies32 bytes to message+20,
  constructs the report and broadcasts it.
- Located-list closure628406..628496 converts the object pose and appends a
  native52-byte entry with pose at+10; native allocation padding is not wire
  schema. The closure installation/enumeration census is not freshly checked.
- Face report4F4B06..4F4B82 converts its stored pose into the constructed
  report. This slice does not approve the prior estimator/face-selection path.
- Whiteboard56C448..56C47A and56C48A..56C4B6 reconstruct from message+20,
  then call RemovePossibleObjectsMatching/ConsiderNewPossibleObject. Direct
  use of an original double world pose is not this numeric boundary.
- Reaction6114A0..611526 reconstructs only after its type-set gate, then
  selects the carried/docking or robot-aware observation overload. Entry
 61153C..611576 checks tag/current behavior first. Full receiver lifetime
  and parent/copy integration remain separate.
- Custom-object handler61E1EC..61E232 contains no SDK gate and uses inbound
  conversion before the create/reply calls. This does not establish a supported
  host API or exclusive SDK provenance; no exclusion is made here.

The report's four-input/four-output universal interpretation is not adopted:
only the named call instructions above were personally checked, not a complete
computed/indirect-call census. Quaternion constructors, full closure setup,
face pose provenance and generated schema checks remain with their existing
reviews until reopened. No new shared conversion implementation is released
from an incomplete owner/caller inventory. No new research queued under HOLD.

Next bounded build candidate: actual MainPose/full-quaternion/root-ID
publication supplier using the already recovered outbound contract, after
reconciling its owner and tests. Local object/whiteboard delivery must retain
the whole report contract and cannot be replaced by a helper-only success.
