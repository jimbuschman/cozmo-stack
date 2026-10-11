# Q14-104/131: a global counter and two separately allocated record fields

Checkpoint676; decisions only; guard reread before each step. Primary `20261010-sound-attachment-next-unmatched-stores-native.txt`, `20261010-sound-attachment-segment-store-base-native.txt`, `20261010-sound-attachment-segment-store-array-native.txt`. Engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. Capture filenames are provisional; no new segment/playlist semantic classification is inferred from them.

| Candidate | Concrete destination |
| --- | --- |
| 97F184 | Actual r6 forms fixed global108D7D8 at97F104+8+70E6CC; stores its34=108D810. This extends AW673–675's positive fixed-global attribution, not an audio-node parent writer. |
| 97F658/97F664 | Reader97F424 saves incoming owner inSP8, runs actual common9877B4, then separately allocates76*serializedcount at97F530/538/540/54C and publishes that allocation to owner84. Loop97F6D0..700 selects the physical record at owner84+76*oldowner88, increments88 before the pointer gate, and saves that record inr4. Candidate38 copies rawword from current serializedcursor+21; candidate34 copies rawr0 from the immediately preceding conversion. Both destinations are fields of the separate76-byte record, not owner/node34/38. |

This is a destination/alias result only. Do not equate owner84 records with audio nodes, assume allocator success, assign an unstated rule/transition name or simplify their possible timing effects. Exact serialized record/parameter behavior belongs the retained music interfaces; it is not silently marked harmless or accepted as all Q15 production behavior here. Stop at the proven separate output destination rather than descending into music consumers for this parent/output-bus writer question.

Original104/131 remain PARTIAL for genuinely unresolved actual node/bus aliases and admitted9FBB30 output destination. Counts107 native+8 scope CHECKED/25 PARTIAL;140 AUDIBLE/0 INTERNAL. out of scope: record backing/allocation mechanics and per-sample DSP. No production/fidelity/inventory/hardware/Q15 edits.
