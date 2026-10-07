| Item | Coverage | Result |
|---|---|---|
| Anonymous copy 0x004F50F8 recipient | CHECKED | Direct callers are FaceWorld's TrackedFace update, not sprite keyframes. |
| Anonymous constructor 0x0056D816 recipient | CHECKED | Diagnostic segment payload supplied by VizManager. |
| Anonymous configuration constructor 0x005D9F50 recipient | CHECKED | Independent 0x3C allocation by BehaviorDriveInDesperation. |
| Package-wide keyframe alias closure | PARTIAL | These exclusions do not cover all displacement candidates or computed aliases. |

Continues M5-013 without changing its manifest or extracting higher-layer behaviour. The current record quotation and binary hash are in `20261006-M5-013-bulk-callees-check.md`; this report makes no contradiction or settlement claim. Native caller dumps and the caller navigation script accompany this report.

| Step | Address | Behaviour / receiver provenance | Gates / order | Failure / uncertainty | Floats |
|---|---|---|---|---|---|
| O1 | FaceWorld::AddOrUpdateFace 0x004F4278; calls 0x004F4542 and 0x004F47B2; copy store 0x004F5136 | Both calls supply the TrackedFace update source; the latter destination is a newly inserted entry+0x14. Copy body writes the receiver's byte+0x30 and larger face members, including Point<2,float> vectors+0x44 onward and FaceRecognitionMatch list+0x100. It is not the 0x2C sprite object/list-node payload. | Native callers and receiver layout checked together, not excluded merely by anonymous helper name. | Excludes these direct call paths only. Unknown indirect callers remain outside the navigation script. | No numeric behaviour extracted. |
| O2 | VizManager::DrawSegment<float> 0x0056C90C..0x0056C97E; constructor 0x0056D816..0x0056D862 | DrawSegment supplies a diagnostic message object to the helper, which constructs its name/string and coordinate fields, then writes its byte+0x28. | Caller constructs the coordinate temporaries before passing them and the diagnostic object to the constructor. | Excludes this call path; no assertion about arbitrary reinterpretation/corrupt pointers. | Diagnostic coordinate arithmetic belongs outside this investigation; no values adopted. |
| O3 | BehaviorDriveInDesperation constructor 0x005D8B2A..0x005D8B36; stores 0x005D9FA0 and 0x005DA02A | Requests a separate 0x3C allocation, keeps its result in r6 and passes that allocation as the configuration constructor receiver. Its +0x28/+0x30 stores therefore do not target an existing sprite keyframe. | Allocation precedes configuration parsing. | Allocation failure/unwind and higher-layer parameter semantics are not extracted. | None adopted. |

The caller census finds four direct calls from symbol-bounded functions in their ELF-declared instruction state. It is navigation, not exhaustive indirect-call analysis. Remaining closure includes the other displacement candidates, instruction-state/literal-pool validation and writes through computed aliases; M5-013 remains open.
