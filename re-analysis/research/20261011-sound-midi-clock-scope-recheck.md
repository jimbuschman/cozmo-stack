# Q14-043: original scheduling scope and complete native decision contracts

Checkpoint840; guard reread before each step; decisions only. Primary `-clock-scope-recheck-native.txt` reopens the original reader range, its preceding/trailing blocks, tempo handoff, effective-tempo walks and live-rate conversion. Engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. Existing RI/TP/MR/PD/SK/CRS/FC, MJ515, TE591 and the concrete target/held-state joins are evidence to reconcile, not substitutes for the reopened instructions.

Original043 quotation, unchanged:

> | M9-005 | **KEEP** | 0x00A3F76C..0x00A3FBC0 | MIDI tick-to-time, division9600, effective tempo and property55 are exact scheduling; header tempo cannot be discarded as a data-fit approximation. |

Current manifest record, read before discussing its unresolved statement:

> M9-005 — Singing MIDI sources use SMF division 9600 and the effective meter tempo
> Status: IMPLEMENTATION_GAP
> Evidence: ["Cozmo.bnk MIDI source plugin 0x00100001 and division 0x2580", "re-analysis/research/20260928-X4-M9-wwise-music-extraction.md"]
> Unresolved: Audit 2026-09-29 (re-analysis/research/20260929-audit-complete.md): the settlement did not hold. the tick-to-time code (0x00A3F76C..0x00A3FBC0) and property 55 are unread; the header tempo float is dropped because the bank durations fit without it, a data fit rather than a runtime reading.

That native reading is now available in the cited research. This does not verify the production implementation or change the manifest: M9-005 remains IMPLEMENTATION_GAP, and manager comparison/acceptance is still required.

| Original decision | Complete retained source contract / input boundary |
| --- | --- |
| Packed division/header tempo | RI1-5 read the actual six-byte packed source header: big-endian U16 division, little-endian rawF32 tempo. Division0 fails;9600 is the shipped value, not a reader code constant. Raw header tempo participates in binary32 ratios using476A6000=60000. It is not reconstructed from duration or ignored. Existing SRC/TPL/CLP/CCF bind source bytes, clip metadata, trim and actual reader product. |
| Property55 | TE591A/B and freshA3C8F0..C9B8 preserve independent track/parent override bits, first-property lookup and the nearest override walk. Nonzero property55 returns rawzero as the retain-header-tempo sentinel. Property55zero starts a separate parent-only meter walk; an explicit meter override stops that walk even if its tempo iszero. No nearest-property or inferred default substitute. |
| Authored meter input | TE591C/E bind the actual23-byte serialized meter, override/constructor publication and fresh hash-checked100/120/80 parent witnesses. Meter/sample conversions retain their exact F64/F32 operation sequence and signed rounding. These are positive inputs, not an assertion that every runtime tempo equals one of those three values. |
| Successful initialization/tempo handoff | A3CD80..CDA0 tests reader initialization1 before effective-tempo query andA3EF80. A3EF90..9C uses ordinary floating comparison tozero: exact+0/−0 retains stored header tempo; any nonzero includingNaN replaces it. A3EFA0..B0 recomputes ratios using live reader division and effective/header tempo. No finite/positive clamp. |
| Reader clock/event decisions | OriginalA3F76C..FBC0, precedingA3F658..76C and trailingFBC0..FC40 retain the already recovered MR/PD normal/system/running-status decode, variable deltas, loop/restore/seek state, exact clock accumulation and event/list/result branches. MJ515 pays the formerly unread normal/system/seek/restoration remainder. No new decoder or seek obligation is inferred from the historical prose. |
| Duration live-rate input | A3C4B8..C53C resolves GOT1040068→105243C and reads its currentU32 for the signed wrapping frame difference conversion. FCbit1 and retain/release interfaces remain FC/NQ. The recovered expression is parameterized by that actual load; it neither assumes48000 nor substitutes robot22320. |
| Per-event live-rate input | A3C608..C650 independently reloads the same pointer for each popped event, then uses F64(eventTimeF32)*F64(U32rate)/1000 and native sign-dependent half adjustment/VCVT. A write between duration and event conversions would be observed by these separate loads. This exact read cadence is recorded; there is no whole-batch snapshot or immutability assumption. |
| Concrete recipients | FC/NQ/CWT plus MJ515/GM574/CD585/ER586/HN587/HN589 retain actual queued target dispatch, scope/context ownership, acquired state, held Sound replay, stop/fade and source-lifecycle interfaces. Their formerly unpaid051/053 target/replay work was positively closed. This does not claim whole M9 production acceptance. |

591 kept043 PARTIAL solely for an inherited unrestricted renderer-rate writer/alias census. Rechecking the unchanged originals distinguishes that from the above scheduling contract:092 explicitly requests a **complete writer census**, and099 explicitly requests **writer absence/copies**. Those remain PARTIAL with the actual unresolved producer/alias inputs fully visible;120/124/130 also retain their explicit census/zero-input requirements. This report does not settle, weaken or move those gaps, certify a fixed live rate, or claim that no additional writer exists. It removes the duplicate all-writer demand from043, whose original named header/property/tempo/clock/recipient decision contracts now have complete rows with live inputs and read order preserved. This is the same bounded-original-scope discipline used by114 and116/123, not a whole-path fidelity promotion.

**Q14-043 CHECKED for its original research decision obligation:** no unpaid original header-tempo/property55/tick-time/frame-conversion or concrete recipient decision remains; live rate stays an exact separately reloaded input. out of scope: backing/storage/I-O and per-sample DSP. No unreachability disposition or default-only closure is used.117 CHECKED /23 PARTIAL /0 NOT DONE;140 AUDIBLE /0 INTERNAL. Q14 as a whole remains incomplete; Q15 stays behindQ14.

Research only; no production, fidelity, inventory, PROJECT_STATE, hardware or Q15 changes. Continue the genuinely unpaid original caller/writer/input obligations after the push.
