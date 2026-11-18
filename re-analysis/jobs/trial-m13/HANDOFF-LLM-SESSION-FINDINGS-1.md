# LLM-SESSION-FINDINGS-1 — READY FOR CLAUDE

Base cb08a4e6, with origin/main 9aababc8 merged. Evidence and bounded production
paths: re-analysis/research/20261118-llm-session-findings.md.

- Pickup: ObjectPositionUpdated selected AcknowledgeObject, whose NOW admission
  cancelled DriveToPickupObject[5]. Native StartActing 0x005BDB5E..6C -> QueueNow
  cancel/delete 0x0053E350/35E does the same after selection. Fixed upstream
  PoseHelper conversion/failure/frame order (0x006124BA..FC), native ReactionData
  pose value ownership (0x006123B6/C8, 0x006123DA..0x00612426, 0x00612ADE), and
  exact ctor angle 0x3F490FDB (0x00612182..8A). Preserved unsigned >600000 ms
  fallback. The capture lacks private reacted pose/time snapshots, so these
  differences are not claimed as the cause of this particular target selection.
- Sentence: 15.545 s total; 10.981 s to ready Init, then normal tag-1 Start/End
  and CA/CB, 115 audio chunks, no BF. Direct style 0/scalar 1 uses Android Ryan
  rate 50; it does not select random Name_Normal traits. Shipped synthesis of
  the whole input yields 83532 samples / 3.788 s. Exact preparation sub-phases
  and whether every word was audible cannot be recovered from this capture.
- Unknown face: native no-pose default is SUCCESS/state 3 with no fallback or
  turn (0x0054BD7C -> 0x0054BEB6..BC -> 0x0054C606..63C). Preserve that result.
  Added nullable FacePoseAvailable from actual Init and explicit no-target detail
  to the public result/bridge. Constructor/deconstruction and native union stay
  intact; no face motor-path change.

Validation: affected face/bridge/Vision/AcknowledgeObject/CanonicalReaction/
Correction/ActionList tests 315/315 passed. Full suite, including all 15 available
private replay cases: **8720 passed / 8 failed / 8728 total**, 40.6242 minutes,
snapshot bin/Debug/m13-findings-native-full-cb08a4e6 and
log .scratch/m13-findings-native-full-cb08a4e6.log.
Six failures are the live MCP's UDP 47817 conflict; one is the known private
247000..255800 named-face replay timeout (next queue item). MCP was left running.
The eighth was the long-decimal literal lint: notation now uses MathF.PI / 4f,
with the native word/address and literal-bit test. Follow-up lint + position test
2/2 passed. All 51678 robot method/field entries match the full snapshot; other
production assemblies and test DLL are byte-identical. **No clean full pass is
claimed.** Two earlier partial full runs were stopped for source corrections.
Fidelity check (764 records) and diff check pass.

No manifest/inventory/approval/SCOPE/PROJECT_STATE edits or hardware bundles.
M10-003/M13-014/M8-015 remain IMPLEMENTATION_GAP; M14-006 remains BLOCKED_EXTERNAL.
No hardware scope item is marked finished. Claude: spot-read the native reaction
comparison/pose assignment, NOW cancellation, and no-pose face path before merge.
Operator observation/recording is still needed for exact audible completeness.
