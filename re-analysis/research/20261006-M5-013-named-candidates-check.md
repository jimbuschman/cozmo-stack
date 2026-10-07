| Item | Coverage | Result |
|---|---|---|
| Three remaining named Thumb hypotheses | CHECKED | Diagnostic strings, not executed scalar stores. |
| OMR_F_DT_0801 hypothesis | CHECKED | Spill to the callee's own reserved stack area. |
| OMR_F_PT50_0003's two output stores | PARTIAL | Genuine matrix/output-buffer writes; caller ownership not closed here. M11 boundary. |
| Remaining derived-store hypotheses | PARTIAL | 60 after the additional four exclusions; no global absence claim. |

Follow-up to `20261006-M5-013-escape-check.md`, which quotes the current M5-013 title, status, evidence and unresolved text. The manifest has not been modified. These rows do not contradict or settle it. The task remains research-only; no marker arithmetic, DSP arithmetic or higher-layer implementation is adopted.

| Step | Address | Behaviour / disposition | Gates / order | Failure / UNKNOWN | Float bits |
|---|---|---|---|---|---|
| N1 | GetColorOptional ADR at 0x0084030E; candidate 0x008405A0 | ADR resolves to 0x00840568, the NUL-terminated diagnostic `Expecting color in Json to be a string or 3 or 4 element array`. Candidate is inside that ASCII string, not a store instruction. | Diagnostic data passed to logging; native instruction body/return precedes inline data. | Does not provide a flag writer. | None. |
| N2 | PoseOriginList::SanityCheckOwnership ADR at 0x00848082; candidate 0x0084814E | ADR resolves to 0x0084814C, `parent.IsOwned()`. Candidate decodes the second and subsequent characters as a hypothetical store. | Diagnostic assertion string, not executed code. | Does not provide a flag writer. | None. |
| N3 | MemoryStack::Allocate ADR at 0x00887968; candidate 0x00887A70 | ADR resolves to 0x00887A68, `Ran out of scratch space`. Candidate is inside that string. | Diagnostic text; actual allocator control flow is outside this receiver check. | No allocator policy or upper-layer change inferred. | None. |
| N4 | OMR_F_DT_0801 0x00919F28..0x00919F40 | Subtract8 from SP, push9 registers (36 bytes), subtract0x24 (36 bytes); spill r3 at current SP+0x4C. That destination is entrySP-4, within the callee's reserved area. | Spill before subsequent data processing. | This is not an object-member write to a keyframe; source value need not be resolved to establish the destination. | None. |
| N5 | OMR_F_PT50_0003 entry 0x00926B9C; 0x00926C5C; 0x00926CA0 / 0x00926CC0 | Genuine ARM word stores to an output buffer supplied in r1. The function advances r1 by0x40, then writes products at negative offsets; affine offsets +0x28/+0x30 are coincidences with the keyframe displacement. | Destination is an output-buffer argument. Complete production caller/ownership trace is not established. | UNKNOWN whether every caller's buffer allocation is disjoint from sprite objects. HIGHER-LAYER boundary: M11 marker/OMR pipeline; proposed ownership follow-up `NEW M11-OMR-output-buffer-ownership`, citing this entry and these stores. Do not mark this candidate excluded solely from the routine's name. | No numeric behaviour extracted. |

Companions retain full named function disassembly, raw bytes around the three string candidates, ARM prologue/output instructions and the direct-caller navigation result. The latter finds zero direct calls from symbol-bounded functions to OMR_F_PT50_0003 or its PLT veneer; that does **not** prove no callers. Indirect table dispatch/unnamed callers remain possible and require the M11 ownership trace. No binary-unavailability claim is made.

Of the 64 derived hypotheses left after E10/E11/E12, these rows exclude four and leave 60, including N5's two stores. The earlier 1,924 direct-wide hypotheses are still not checked. No enabling true-flag producer has been recovered on the checked sprite graph; M5-013 remains open pending the broader alias/bulk-store proof. Native emulation and its fixture remain the separate checks described in the preceding report; no additional hardware or production tests were run for these data/receiver exclusions.
