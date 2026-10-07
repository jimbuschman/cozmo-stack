| Item | Coverage | Result |
|---|---|---|
| 87 OpenCV baseline keys / 435 occurrences | CHECKED | Replaced with primary binary32 pool words; 87 baseline lines removed. |
| Remaining 16 words in the same 451-word table | CHECKED | Same representation change, no value change. |
| Four neutral-face asset literals | CHECKED | Outside Q12 opcode/immediate/pool scope; unchanged. |
| Current M1/M2 lint baseline entries | CHECKED | None; no selected entries to change. |
| Selected literal width | CHECKED | All binary32; no width defect. |

Q12 changes only OpenCv310.SinTable, used by ellipse2Poly on the procedural face drawing path. Each word cites its own shipped libopencv_imgproc.so address, 0x000E7910..0x000E801C (exclusive). Direct literal-pool reads are the explicit Q12 authority exception to the execution-only oracle rule. No sine calculation or C# output generates these words. The preceding baseline compiled all 451 words byte-identically; this is a representation-only change. SHA256: 3c4e3ff7e639c61e21c5ef91ea2cc4125bb9bd259ff53f5c0b1eeb9a95054830.

Four asset literals, including the previously reported one-ULP neutral scale-X defect, remain for manager review; they are not opcode/immediate/pool entries. No higher-layer caller was traced, no production gate or operation changed, and no manifest status changed. Existing face rasterization tests exercise the table through drawing. Self-review: existing live table only, binary32 width unchanged, explicit address per element, no newly computed expectation and no parallel implementation. Validation and commit are logged in jobs/status/CODEX-Q4.md.
